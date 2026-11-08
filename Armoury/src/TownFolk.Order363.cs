using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// 363 (Jeff 08.11, wybor "Ukryte zloto do lupu": "produkcja materialu na zamowienie u wsi drwali i gliniarzy, zeby wsie mogly budowac wiecej") - strona zakupu:
    /// zlecenie robot gminy wsi DS (DaRunOne, nie septy), ktorego polka targu nie pokryla calego budzetu materialu, kupuje reszte u wsi-wytworcow KS w linii prostej
    /// do VillageOrderRange (najblizsi; frakcje nie w wojnie, wytworca Normal, jego osada macierzysta nie oblezona; wies nie zamawia u siebie) - bez wozu i bez polki:
    /// kiesa zamawiajacej -> kiesa wytworcy po cenie jego targu (VillageCrafts.Price, najwyzej BuildMaterialPriceCap x wartosc), sztuki zuzywa robota. Ksiega towarow:
    /// +n zrodlo i -n ujscie w grupie "roboty wsi na zamowienie (363)" (zapas bez zmian), drewno takze w linii "Drewno:". Rzemieslnicy miasta liczeni od calego
    /// materialu (polka + wytworca) - jedna regula. Reszta budzetu (popyt niezaspokojony) przypisana najblizszemu wytworcy ponizej pulapu (EMA popytu - rece przy
    /// nastepnym rachunku okregu). Pamiec zasiegu odswiezana co 28 dob. Linia doby "Zamowienia wsi (363): dzien N". Kazdy wytworca i zlecenie we wlasnym try,
    /// licznik potkniec, bez globalnego wylacznika (CLAUDE.md 7).
    /// </summary>
    internal static partial class TownFolk
    {
        private const int OrNearMax = 8;                  // najwyzej tylu najblizszych wytworcow na zamawiajacego
        private const int OrNearDays = 28;                // pamiec zasiegu - odswiezana co tyle dob

        private struct OrNear { public Village V; public float D; }
        private static readonly Dictionary<Village, List<OrNear>> _orNear = new Dictionary<Village, List<OrNear>>();
        private static readonly List<Village> _orPool = new List<Village>();
        private static int _orNearDay = int.MinValue; private static float _orNearRange = -1f;

        // ---- liczniki doby (linia 363)
        private static readonly double[] _orMade = new double[4], _orCarry0 = new double[4], _orCarry1 = new double[4], _orIdle = new double[4];
        private static readonly long[] _orSold = new long[4];
        private static long _orOut, _orIn, _orBack, _orUnmet, _orNoProd; private static double _orWage;
        private static int _orOrders, _orNoDue, _orNoProdN, _orSt, _orStSess; private static long _orTicks;
        private static long _orLastPaid;                   // DaRunOne -> DaWorksRunCore: zloto tego zlecenia do kies wytworcow
        private static readonly HashSet<string> _orErr = new HashSet<string>();

        // ---- liczniki do linii Akumulator (zerowane w DaZero)
        private static long _dwOrdOut, _dwOrdIn; private static int _dwOrdN;

        private static void OrReset()
        {
            _orNear.Clear(); _orPool.Clear(); _orNearDay = int.MinValue; _orNearRange = -1f;
            OrZero(); _orStSess = 0; _orErr.Clear(); _orLastPaid = 0;
        }

        private static void OrZero()
        {
            Array.Clear(_orMade, 0, 4); Array.Clear(_orCarry0, 0, 4); Array.Clear(_orCarry1, 0, 4); Array.Clear(_orIdle, 0, 4); Array.Clear(_orSold, 0, 4);
            _orOut = _orIn = _orBack = _orUnmet = _orNoProd = 0; _orWage = 0;
            _orOrders = _orNoDue = _orNoProdN = _orSt = 0; _orTicks = 0;
        }

        private static void OrStumble(string where, Exception e)
        {
            _orSt++; _orStSess++;
            try { if (_orErr.Add(where)) Log.Error("TownFolk.Order363." + where, e); } catch { }
        }

        /// <summary>Cena sztuki u wytworcy: cena jego targu (wartosc x indeks kategorii), najwyzej BuildMaterialPriceCap x wartosc; co najmniej 1.</summary>
        private static int OrPrice(Village p, ItemObject it, Settings s)
        {
            var mk = p.TradeBound ?? p.Bound;
            double pr = VillageCrafts.Price(mk != null ? mk.Town : null, it);
            float capK = s != null ? s.BuildMaterialPriceCap : 0f;
            if (capK > 0f) pr = Math.Min(pr, capK * Math.Max(1, it.Value));
            if (double.IsNaN(pr) || double.IsInfinity(pr)) pr = Math.Max(1, it.Value);
            return Math.Max(1, (int)Math.Round(pr, MidpointRounding.AwayFromZero));
        }

        /// <summary>Najblizsi wytworcy zamawiajacej w zasiegu (linia prosta), z pamieci; pamiec od nowa co 28 dob, przy zmianie zasiegu i (raz na dobe) gdy brak wytworcow -
        /// po wczytaniu zajecia okregow znane dopiero po pierwszym rachunku okregu (StartCalibration.Daily biegnie po DaWorksRun).</summary>
        private static List<OrNear> OrNearOf(Village buyer, float range)
        {
            int today = (int)CampaignTime.Now.ToDays;
            if (today - _orNearDay >= OrNearDays || today < _orNearDay || Math.Abs(range - _orNearRange) > 0.5f || (_orPool.Count == 0 && today != _orNearDay))
            {
                _orNear.Clear();
                StartCalibration.OrderProducers(_orPool);
                _orNearDay = today; _orNearRange = range;
            }
            List<OrNear> l;
            if (_orNear.TryGetValue(buyer, out l)) return l;
            l = new List<OrNear>();
            if (buyer.Settlement != null && _orPool.Count > 0)
            {
                var pos = buyer.Settlement.GetPosition2D;
                foreach (var p in _orPool)
                {
                    if (p == null || ReferenceEquals(p, buyer) || p.Settlement == null) continue;
                    float d = pos.Distance(p.Settlement.GetPosition2D);
                    if (d <= range) l.Add(new OrNear { V = p, D = d });
                }
                l.Sort((a, b) => a.D.CompareTo(b.D));
                if (l.Count > OrNearMax) l.RemoveRange(OrNearMax, l.Count - OrNearMax);
            }
            _orNear[buyer] = l;
            return l;
        }

        /// <summary>Wytworca moze dzis sprzedac zamawiajacej: Normal, osada macierzysta nie oblezona, frakcje nie w wojnie.</summary>
        private static bool OrUsable(Village buyer, Village p)
        {
            if (p == null || p.Settlement == null || ReferenceEquals(p, buyer)) return false;
            if (p.VillageState != Village.VillageStates.Normal) return false;
            if (p.Bound != null && p.Bound.IsUnderSiege) return false;
            var fa = buyer.Settlement != null ? buyer.Settlement.MapFaction : null;
            var fb = p.Settlement.MapFaction;
            if (fa != null && fb != null && !ReferenceEquals(fa, fb) && FactionManager.IsAtWarAgainstFaction(fa, fb)) return false;
            return true;
        }

        /// <summary>
        /// DaRunOne (roboty gminy wsi, po zakupie z polki targu): reszta budzetu materialu `matLeft` u wytworcow w zasiegu. Zwraca zloto, ktore zeszlo z kiesy zamawiajacej
        /// do kies wytworcow (zmierzone). Nie rzuca.
        /// </summary>
        private static long ProducerBuy(DaOrder o, double matLeft)
        {
            _orLastPaid = 0;
            if (o == null || o.Sept || o.Vil == null || !(matLeft >= 1)) return 0;
            long t0 = Stopwatch.GetTimestamp();
            long paid = 0;
            try
            {
                var s = Settings.Current;
                if (s == null || !StartCalibration.OrderOn || !StartCalibration.KeyOn) return 0;
                var near = OrNearOf(o.Vil, s.VillageOrderRange);
                Village open = null, any = null; int openPr = 0, anyPr = 0;
                if (near != null)
                    foreach (var e in near)
                    {
                        if (matLeft < 1) break;
                        try
                        {
                            var p = e.V;
                            if (!OrUsable(o.Vil, p)) continue;
                            ItemObject it; double left; bool atCap; int slot;
                            if (!StartCalibration.OrderOffer(p, out it, out left, out atCap, out slot)) continue;
                            int price = OrPrice(p, it, s);
                            if (any == null) { any = p; anyPr = price; }
                            if (!atCap && open == null) { open = p; openPr = price; }
                            int n = (int)Math.Min(Math.Floor(left), Math.Floor(matLeft / price));
                            if (n <= 0) continue;
                            long x = OrPay(o.Vil, p, it, slot, n, price);
                            paid += x; matLeft -= x;
                        }
                        catch (Exception ex) { OrStumble("ProducerBuy(wytworca)", ex); }
                    }
                // popyt niezaspokojony - najblizszemu wytworcy ponizej pulapu (albo najblizszemu), bez wytworcy w zasiegu - licznik
                var tgt = open ?? any; int tp = open != null ? openPr : anyPr;
                if (tgt != null) { if (tp > 0 && matLeft >= tp) { StartCalibration.NoteOrderDemand(tgt, matLeft / tp); _orUnmet += (long)Math.Floor(matLeft); } }
                else if (matLeft >= 1) { _orNoProd += (long)Math.Floor(matLeft); _orNoProdN++; }
            }
            catch (Exception e) { OrStumble("ProducerBuy", e); }
            finally { _orTicks += Stopwatch.GetTimestamp() - t0; }
            _orLastPaid = paid;
            if (paid > 0) { _dwOrdN++; try { DsVillage.NoteWorksOrder(paid); } catch (Exception e) { OrStumble("NoteWorksOrder", e); } }
            return paid;
        }

        /// <summary>Zaplata n sztuk wytworcy p: kiesa zamawiajacej -> kiesa wytworcy (zmierzone po obu stronach); sztuki wedlug zaplaty. Zwraca zloto, ktore zeszlo z kiesy
        /// zamawiajacej (poprawka 363, recenzja 1: budzet, place, warunek daniny i linia DS od wyplywu z kiesy kupujacej; przyjete u wytworcy - tylko kontrola zlota).</summary>
        private static long OrPay(Village buyer, Village p, ItemObject it, int slot, int n, int price)
        {
            long want = (long)n * price;
            if (want <= 0 || want > int.MaxValue) return 0;
            P2Open(P2Vil); P2TouchSt(buyer.Settlement); P2TouchSt(p.Settlement);   // 322b: pomiar (tylko odczyt) - kiesy wsi
            try
            {
                int had = buyer.Gold;
                buyer.ChangeGold(-(int)want);
                long got = Math.Max(0, (long)had - buyer.Gold);
                int k = (int)Math.Min(n, got / price);                        // kiesa sprawdzona przed zakupem (budzet <= kiesa ponad skrzynie) - k == n
                long pay = (long)k * price, back = got - pay;
                if (back > 0) { buyer.ChangeGold((int)back); _orBack += back; }
                _orOut += pay; _dwOrdOut += pay;
                if (k <= 0) return 0;
                long pin;
                try
                {
                    int ph = p.Gold;
                    p.ChangeGold((int)pay);
                    pin = (long)p.Gold - ph;
                }
                catch (Exception e) { buyer.ChangeGold((int)pay); _orOut -= pay; _dwOrdOut -= pay; OrStumble("OrPay(wytworca)", e); return 0; }
                _orIn += pin; _dwOrdIn += pin;
                StartCalibration.OrderTake(p, k);
                if (slot >= 0 && slot < _orSold.Length) _orSold[slot] += k;
                GoodsLedger.NoteMadeToOrder(it, k);
                OreLedger.NoteVillageOrder(it, k);
                return pay;
            }
            finally { P2Close(); }
        }

        /// <summary>DaWorksRun: poczatek doby zamowien (sztuki doby wytworcow).</summary>
        private static void OrDayOpen()
        {
            long t0 = Stopwatch.GetTimestamp();
            try { StartCalibration.OrderDayOpen(_orMade, _orCarry0); }
            catch (Exception e) { OrStumble("OrDayOpen", e); }
            finally { _orTicks += Stopwatch.GetTimestamp() - t0; }
        }

        /// <summary>DaWorksRun (koniec): sztuki niesprzedane przepadaja, EMA popytu; linia "Zamowienia wsi (363): dzien N" (klucz czynny albo ruch tej doby); zeruje.</summary>
        private static void OrDayClose()
        {
            long t0 = Stopwatch.GetTimestamp();
            try
            {
                StartCalibration.OrderDayClose(_orIdle, _orCarry1);
                var s = Settings.Current;
                bool on = StartCalibration.OrderOn && StartCalibration.KeyOn;
                double mk = 0; foreach (var x in _orMade) mk += x;
                if (s == null || !s.LogEnabled || Campaign.Current == null || (!on && mk <= 0 && _orOut <= 0)) return;
                var by = new int[4]; int wh, hg; double hands, field, g0, g1;
                StartCalibration.OrderStats(by, out wh, out hands, out field, out hg, out g0, out g1);
                int pn = by[0] + by[1] + by[2] + by[3];
                var inv = Inv;
                var sb = new StringBuilder(1200);
                sb.Append("Zamowienia wsi (363): dzien ").Append((int)CampaignTime.Now.ToDays - 1).Append(" | ").Append(on ? "CZYNNE" : "WYLACZONE")
                  .Append(" | wytworcow KS ").Append(pn).Append(" (").Append(string.Join("/", StartCalibration.OrdCraftNames)).Append(" ").Append(by[0]).Append('/').Append(by[1]).Append('/').Append(by[2]).Append('/').Append(by[3])
                  .Append("; w zasiegu ").Append(s.VillageOrderRange.ToString("0", inv)).Append("), z rekami ").Append(wh)
                  .Append(", rece z pola ").Append(N0(hands)).Append(" (").Append(field > 0 ? (100 * hands / field).ToString("0.00", inv) : "-").Append("% rak pola tych okregow, pulap ")
                  .Append((100 * Math.Max(0, Math.Min(0.25, (double)s.VillageOrderFieldShare))).ToString("0.#", inv)).Append("%), glod - bez rak ").Append(hg);
                sb.Append(" | sztuki/d ").Append(string.Join("/", StartCalibration.OrdNames)).Append(": zrobione ").Append(J(_orMade, "0.0")).Append(" (ulamek z wczoraj ").Append(J(_orCarry0, "0.00"))
                  .Append("), sprzedane ").Append(_orSold[0]).Append('/').Append(_orSold[1]).Append('/').Append(_orSold[2]).Append('/').Append(_orSold[3])
                  .Append(", rece bez roboty ").Append(J(_orIdle, "0")).Append(", ulamek na jutro ").Append(J(_orCarry1, "0.00"));
                sb.Append(" | zloto: kiesy zamawiajacych -").Append(_orOut).Append(" = kiesy wytworcow +").Append(_orIn).Append(" (zwrot do zamawiajacych ").Append(_orBack)
                  .Append("); rzemieslnicy miast od materialu u wytworcow (czesc placy robot) ok. ").Append(N0(_orWage))
                  .Append(" | zlecen z wytworca ").Append(_orOrders).Append(" (ponad 1/2 zlecenia dzieki wytworcy - bez daniny ").Append(_orNoDue).Append(")")
                  .Append(", popyt niezaspokojony ").Append(_orUnmet).Append(" zl (przypisany wytworcom; bez wytworcy w zasiegu ").Append(_orNoProd).Append(" zl, ").Append(_orNoProdN).Append(" zlecen)")
                  .Append(" | zboze z pola mniej: ").Append(N0(g0)).Append(" kg/d przed zima / ").Append(N0(g1)).Append(" po zimie");
                double ctlP = 0; for (int i = 0; i < 4; i++) ctlP += _orMade[i] + _orCarry0[i] - _orSold[i] - _orIdle[i] - _orCarry1[i];
                sb.Append(" | kontrola zlota (z kies zamawiajacych - do kies wytworcow) = ").Append(_orOut - _orIn)
                  .Append(", kontrola sztuk (zrobione + ulamek z wczoraj - sprzedane - bez roboty - ulamek na jutro) = ").Append(Math.Abs(ctlP) < 0.005 ? "0" : ctlP.ToString("0.00", inv))
                  .Append(" | czas ").Append(Ms(_orTicks + (Stopwatch.GetTimestamp() - t0))).Append(" ms | potkniecia ").Append(_orSt).Append(" (sesja ").Append(_orStSess).Append(").");
                Log.Info(sb.ToString());
            }
            catch (Exception e) { OrStumble("OrDayClose", e); }
            finally { OrZero(); }
        }

        private static string J(double[] a, string fmt)
        {
            var p = new string[a.Length];
            for (int i = 0; i < a.Length; i++) p[i] = a[i].ToString(fmt, Inv);
            return string.Join("/", p);
        }

        /// <summary>Dopisek linii Akumulator kies (po "zwrot do wsi"): material u wytworcow i jego kontrola; klucz wylaczony i 0 - pusty.</summary>
        private static string OrAkNote()
        {
            if (!StartCalibration.OrderOn && _dwOrdOut == 0 && _dwOrdIn == 0) return "";
            return "; material u wytworcow na zamowienie (363) " + _dwOrdIn + " (" + _dwOrdN + " zlecen), kontrola robot u wytworcow: z kies wsi - do kies wytworcow = " + (_dwOrdOut - _dwOrdIn);
        }
    }
}
