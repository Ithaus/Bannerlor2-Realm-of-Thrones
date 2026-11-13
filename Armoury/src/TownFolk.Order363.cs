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
    /// 363b (Jeff 08.11 "tak rob 363b"): zasieg 600, frach - praca wozakow wytworcy: dni drogi = TradeCarts.DaysFor(linia prosta), frach = VillageOrderFreightPerDay x dni;
    /// sztuka zuzywa (1 + frach) jednostek produkcji wytworcy i kosztuje cena jego targu x (1 + frach) - cale zloto z kiesy zamawiajacej do kiesy wytworcy (wozacy to jego
    /// ludzie, ich rece juz w H - popyt liczy jednostki z frachtem). Sufit BuildMaterialPriceCap dotyczy ceny z frachtem - wytworca za drogi pominiety (jak sufit polki).
    /// Towar na miejscu tej samej doby - czas drogi tylko w koszcie (uproszczenie swiadome, bez wozow w drodze i bez zapisu).
    /// </summary>
    internal static partial class TownFolk
    {
        private const int OrNearMax = 8;                  // najwyzej tylu najblizszych wytworcow na zamawiajacego
        private const int OrNearDays = 28;                // pamiec zasiegu - odswiezana co tyle dob

        private struct OrNear { public Village V; public float D; public int Days; }   // 363b: Days - dni drogi wozu (TradeCarts.DaysFor linii prostej)
        private static readonly Dictionary<Village, List<OrNear>> _orNear = new Dictionary<Village, List<OrNear>>();
        private static readonly List<Village> _orPool = new List<Village>();
        private static int _orNearDay = int.MinValue; private static float _orNearRange = -1f;

        // ---- liczniki doby (linia 363)
        private static readonly double[] _orMade = new double[4], _orCarry0 = new double[4], _orCarry1 = new double[4], _orIdle = new double[4];
        private static readonly long[] _orSold = new long[4];
        private static long _orOut, _orIn, _orBack, _orUnmet, _orNoProd; private static double _orWage;
        private static int _orOrders, _orNoDue, _orNoProdN, _orSt, _orStSess; private static long _orTicks;
        private static long _orLastPaid;                   // DaRunOne -> DaWorksRunCore: zloto tego zlecenia do kies wytworcow
        private static long _orLastBase;                   // poprawka 363b (recenzje 1 i 2): z tego zlota czesc bez frachtu (cena targu wytworcy) - podstawa placy rzemieslnikow
        // 363b: frach - jednostki pracy wozakow na towar (OrdNames), zloto frachtu (czesc ceny ponad cene targu wytworcy), sztuki x dni drogi i sztuki z frachtem, pominiete przez sufit
        private static readonly double[] _orFrU = new double[4]; private static long _orFrGold, _orFrDays, _orFrPcs; private static int _orCapSkip;
        private static readonly HashSet<string> _orErr = new HashSet<string>();

        // ---- liczniki do linii Akumulator (zerowane w DaZero)
        private static long _dwOrdOut, _dwOrdIn; private static int _dwOrdN;

        private static void OrReset()
        {
            _orNear.Clear(); _orPool.Clear(); _orNearDay = int.MinValue; _orNearRange = -1f;
            OrZero(); _orStSess = 0; _orErr.Clear(); _orLastPaid = 0; _orLastBase = 0;
        }

        private static void OrZero()
        {
            Array.Clear(_orMade, 0, 4); Array.Clear(_orCarry0, 0, 4); Array.Clear(_orCarry1, 0, 4); Array.Clear(_orIdle, 0, 4); Array.Clear(_orSold, 0, 4); Array.Clear(_orFrU, 0, 4);
            _orFrGold = _orFrDays = _orFrPcs = 0; _orCapSkip = 0;
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

        /// <summary>363b: sufit ceny sztuki z frachtem - BuildMaterialPriceCap x wartosc (jak OrPrice; 0 - bez sufitu).</summary>
        private static int OrCapD(ItemObject it, Settings s)
        {
            if (it == null || s == null || !(s.BuildMaterialPriceCap > 0f)) return 0;
            return Math.Max(1, (int)Math.Ceiling(s.BuildMaterialPriceCap * Math.Max(1, it.Value)));
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
                    if (d <= range) l.Add(new OrNear { V = p, D = d, Days = TradeCarts.DaysFor(d) });
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
            _orLastPaid = 0; _orLastBase = 0;
            if (o == null || o.Sept || o.Vil == null || !(matLeft >= 1)) return 0;
            long t0 = Stopwatch.GetTimestamp();
            long paid = 0, paidBase = 0;
            try
            {
                var s = Settings.Current;
                if (s == null || !StartCalibration.OrderOn || !StartCalibration.KeyOn) return 0;
                var near = OrNearOf(o.Vil, s.VillageOrderRange);
                double fpd = Math.Max(0.0, Math.Min(1.0, (double)s.VillageOrderFreightPerDay));   // 363b: frach na dobe drogi (czesc ceny i jednostek produkcji)
                Village open = null, any = null; int openPr = 0, anyPr = 0; double openFr = 0, anyFr = 0;
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
                            // 363b: frach (wozacy wytworcy) - cena i jednostki produkcji x (1 + frach); sufit ceny z frachtem - wytworca za drogi pominiety
                            double frk = fpd * Math.Max(1, e.Days);
                            if (double.IsNaN(frk) || double.IsInfinity(frk) || frk < 0) frk = 0;
                            int priceD = price;
                            if (frk > 0)
                            {
                                double pd = Math.Round(price * (1.0 + frk), MidpointRounding.AwayFromZero);
                                priceD = pd >= int.MaxValue ? int.MaxValue : Math.Max(price, (int)pd);
                                int capD = OrCapD(it, s);
                                if (capD > 0 && priceD > capD) { _orCapSkip++; continue; }
                            }
                            if (any == null) { any = p; anyPr = priceD; anyFr = frk; }
                            if (!atCap && open == null) { open = p; openPr = priceD; openFr = frk; }
                            int n = (int)Math.Min(Math.Floor(left / (1.0 + frk)), Math.Floor(matLeft / priceD));
                            if (n <= 0) continue;
                            long xb;
                            long x = OrPay(o.Vil, p, it, slot, n, priceD, price, frk, e.Days, out xb);
                            paid += x; paidBase += xb; matLeft -= x;
                        }
                        catch (Exception ex) { OrStumble("ProducerBuy(wytworca)", ex); }
                    }
                // popyt niezaspokojony - najblizszemu wytworcy ponizej pulapu (albo najblizszemu), bez wytworcy w zasiegu - licznik
                var tgt = open ?? any; int tp = open != null ? openPr : anyPr; double tfr = open != null ? openFr : anyFr;
                if (tgt != null) { if (tp > 0 && matLeft >= tp) { StartCalibration.NoteOrderDemand(tgt, matLeft / tp * (1.0 + tfr)); _orUnmet += (long)Math.Floor(matLeft); } }   // 363b: popyt w jednostkach z frachtem
                else if (matLeft >= 1) { _orNoProd += (long)Math.Floor(matLeft); _orNoProdN++; }
            }
            catch (Exception e) { OrStumble("ProducerBuy", e); }
            finally { _orTicks += Stopwatch.GetTimestamp() - t0; }
            _orLastPaid = paid; _orLastBase = Math.Min(paid, paidBase);
            if (paid > 0) { _dwOrdN++; try { DsVillage.NoteWorksOrder(paid); } catch (Exception e) { OrStumble("NoteWorksOrder", e); } }
            return paid;
        }

        /// <summary>Zaplata n sztuk wytworcy p: kiesa zamawiajacej -> kiesa wytworcy (zmierzone po obu stronach); sztuki wedlug zaplaty. Zwraca zloto, ktore zeszlo z kiesy
        /// zamawiajacej (poprawka 363, recenzja 1: budzet, place, warunek daniny i linia DS od wyplywu z kiesy kupujacej; przyjete u wytworcy - tylko kontrola zlota).</summary>
        /// 363b: price - cena sztuki z frachtem (cena targu wytworcy `basePr` x (1 + frk)); wytworca zuzywa k x (1 + frk) jednostek produkcji (wozacy z jego rak).
        private static long OrPay(Village buyer, Village p, ItemObject it, int slot, int n, int price, int basePr, double frk, int days, out long payBase)
        {
            payBase = 0;
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
                payBase = Math.Min(pay, (long)k * Math.Max(0, basePr));   // poprawka 363b: zloto bez frachtu (frach = praca wozakow wytworcy, nie material)
                StartCalibration.OrderTake(p, k * (1.0 + frk));
                if (slot >= 0 && slot < _orSold.Length) { _orSold[slot] += k; _orFrU[slot] += k * frk; }
                if (frk > 0) { _orFrGold += (long)k * Math.Max(0, price - basePr); _orFrDays += (long)k * Math.Max(0, days); _orFrPcs += k; }
                GoodsLedger.NoteMadeToOrder(it, k);
                OreLedger.NoteVillageOrder(it, k);
                return pay;
            }
            finally { P2Close(); }
        }

        // ================================================================ 478-S2: pan dokupuje material budow u wytworcow 363 (jak roboty wsi)
        private static int _orOpenedEarlyDay = int.MinValue;   // 478-S2: doba, w ktorej sztuki wytworcow otwarto przed budowami pana (wyjatek w DaWorksRun nie zostawia stanu na jutro)
        internal static long LordOrdPaid, LordOrdPcs, LordOrdN, LordOrdNoProd;

        /// <summary>478-S2: ArmouryBehavior przed BuildFunding.Daily - sztuki doby wytworcow otwarte PRZED budowami pana (kolejnosc pan -> lud -> wies);
        /// DaWorksRun ich drugi raz nie otwiera. Klucz NIE - nic (otwarcie jak dotad w DaWorksRun).</summary>
        internal static void OrderDayOpenEarly()
        {
            var s = Settings.Current;
            if (s == null || !s.BuildMaterialsFromProducers || !StartCalibration.OrderOn || !StartCalibration.KeyOn) return;
            OrDayOpen();
            _orOpenedEarlyDay = (int)CampaignTime.Now.ToDays;
        }

        /// <summary>
        /// 478-S2 (analiza at-120c pkt 2): budowa pana w osadzie `st` - reszta budzetu materialu `matLeft` (po polce targu) u wytworcow 363 w zasiegu, ta sama
        /// droga ceny, frachtu i sufitu co roboty wsi (OrPrice, OrCapD, OrderOffer, OrderTake); zloto: pan -> kiesa wytworcy (zmierzone). Polozenie zamawiajacego
        /// - pierwsza wies osady (OrNearOf wedlug wsi). Zwraca zloto, ktore zeszlo z kiesy pana. Nie rzuca.
        /// </summary>
        internal static long LordProducerBuy(Hero lord, Settlement st, double matLeft)
        {
            long paid = 0;
            if (lord == null || st == null || !(matLeft >= 1)) return 0;
            long t0 = Stopwatch.GetTimestamp();
            try
            {
                var s = Settings.Current;
                if (s == null || !s.BuildMaterialsFromProducers || !StartCalibration.OrderOn || !StartCalibration.KeyOn || _orOpenedEarlyDay != (int)CampaignTime.Now.ToDays) return 0;
                Village anchor = null;
                if (st.BoundVillages != null) foreach (var v in st.BoundVillages) { if (v != null && v.Settlement != null) { anchor = v; break; } }
                if (anchor == null) return 0;
                var near = OrNearOf(anchor, s.VillageOrderRange);
                double fpd = Math.Max(0.0, Math.Min(1.0, (double)s.VillageOrderFreightPerDay));
                var lf = lord.MapFaction;
                bool any = false;
                if (near != null)
                    foreach (var e in near)
                    {
                        if (matLeft < 1 || lord.Gold < 1) break;
                        try
                        {
                            var p = e.V;
                            if (p == null || p.Settlement == null || p.VillageState != Village.VillageStates.Normal) continue;
                            if (p.Bound != null && p.Bound.IsUnderSiege) continue;
                            var fb = p.Settlement.MapFaction;
                            if (lf != null && fb != null && !ReferenceEquals(lf, fb) && FactionManager.IsAtWarAgainstFaction(lf, fb)) continue;
                            ItemObject it; double left; bool atCap; int slot;
                            if (!StartCalibration.OrderOffer(p, out it, out left, out atCap, out slot)) continue;
                            any = true;
                            int price = OrPrice(p, it, s);
                            double frk = fpd * Math.Max(1, e.Days);
                            if (double.IsNaN(frk) || double.IsInfinity(frk) || frk < 0) frk = 0;
                            int priceD = price;
                            if (frk > 0)
                            {
                                double pd = Math.Round(price * (1.0 + frk), MidpointRounding.AwayFromZero);
                                priceD = pd >= int.MaxValue ? int.MaxValue : Math.Max(price, (int)pd);
                                int capD = OrCapD(it, s);
                                if (capD > 0 && priceD > capD) { _orCapSkip++; continue; }
                            }
                            int n = (int)Math.Min(Math.Floor(left / (1.0 + frk)), Math.Min(Math.Floor(matLeft / priceD), Math.Floor((double)lord.Gold / priceD)));
                            if (n <= 0) continue;
                            long want = (long)n * priceD;
                            int had = lord.Gold;
                            lord.ChangeHeroGold(-(int)want);
                            long got = Math.Max(0, (long)had - lord.Gold);
                            int k = (int)Math.Min(n, got / priceD);
                            long pay = (long)k * priceD, back = got - pay;
                            if (back > 0) lord.ChangeHeroGold((int)back);
                            if (k <= 0) continue;
                            int ph = p.Gold;
                            try { p.ChangeGold((int)pay); }
                            catch (Exception ex) { lord.ChangeHeroGold((int)pay); OrStumble("LordProducerBuy(wytworca)", ex); continue; }
                            CirculationWindows.NoteHeroGold(lord, -pay);   // glowa poza swiatem - zloto weszlo do swiata (jak budowy pana)
                            StartCalibration.OrderTake(p, k * (1.0 + frk));
                            if (slot >= 0 && slot < _orSold.Length) { _orSold[slot] += k; _orFrU[slot] += k * frk; }
                            if (frk > 0) { _orFrGold += (long)k * Math.Max(0, priceD - price); _orFrDays += (long)k * Math.Max(0, e.Days); _orFrPcs += k; }
                            GoodsLedger.NoteMadeToOrder(it, k);
                            OreLedger.NoteVillageOrder(it, k);
                            paid += pay; matLeft -= pay; LordOrdPcs += k;
                        }
                        catch (Exception ex) { OrStumble("LordProducerBuy(wytworca)", ex); }
                    }
                if (paid > 0) LordOrdN++;
                else if (!any) LordOrdNoProd++;
            }
            catch (Exception e) { OrStumble("LordProducerBuy", e); }
            finally { _orTicks += Stopwatch.GetTimestamp() - t0; }
            LordOrdPaid += paid;
            return paid;
        }

        /// <summary>DaWorksRun: poczatek doby zamowien (sztuki doby wytworcow).</summary>
        private static void OrDayOpen()
        {
            long t0 = Stopwatch.GetTimestamp();
            try { StartCalibration.OrderDayOpen(_orMade, _orCarry0); }
            catch (Exception e) { OrStumble("OrDayOpen", e); }
            finally { _orTicks += Stopwatch.GetTimestamp() - t0; }
        }

        /// <summary>DaWorksRun (koniec): sztuki niesprzedane przepadaja, EMA popytu; linia "Zamowienia wsi (363/363b): dzien N" (klucz czynny albo ruch tej doby); zeruje.</summary>
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
                var by = new int[4]; int wh, hg, nLand, nPlan; double hands, field, g0, g1;
                StartCalibration.OrderStats(by, out wh, out hands, out field, out hg, out g0, out g1, out nLand, out nPlan);
                int pn = by[0] + by[1] + by[2] + by[3];
                var inv = Inv;
                var sb = new StringBuilder(1200);
                sb.Append("Zamowienia wsi (363/363b): dzien ").Append((int)CampaignTime.Now.ToDays - 1).Append(" | ").Append(on ? "CZYNNE" : "WYLACZONE")
                  .Append(" | wytworcow KS ").Append(pn).Append(" (").Append(string.Join("/", StartCalibration.OrdCraftNames)).Append(" ").Append(by[0]).Append('/').Append(by[1]).Append('/').Append(by[2]).Append('/').Append(by[3])
                  .Append("; w zasiegu ").Append(s.VillageOrderRange.ToString("0", inv)).Append("; wytworcow wedlug ziemi ").Append(nLand).Append(" (w planie ").Append(nPlan).Append(")")
                  .Append(s.VillageOrderByLand ? "" : " [by land OFF]").Append("), z rekami ").Append(wh)
                  .Append(", rece z pola ").Append(N0(hands)).Append(" (").Append(field > 0 ? (100 * hands / field).ToString("0.00", inv) : "-").Append("% rak pola tych okregow, pulap ")
                  .Append((100 * Math.Max(0, Math.Min(0.25, (double)s.VillageOrderFieldShare))).ToString("0.#", inv)).Append("%), glod - bez rak ").Append(hg);
                sb.Append(" | sztuki/d ").Append(string.Join("/", StartCalibration.OrdNames)).Append(": zrobione ").Append(J(_orMade, "0.0")).Append(" (ulamek z wczoraj ").Append(J(_orCarry0, "0.00"))
                  .Append("), sprzedane ").Append(_orSold[0]).Append('/').Append(_orSold[1]).Append('/').Append(_orSold[2]).Append('/').Append(_orSold[3])
                  .Append(", rece bez roboty ").Append(J(_orIdle, "0")).Append(", ulamek na jutro ").Append(J(_orCarry1, "0.00"));
                sb.Append(" | frach (wozacy z rak wytworcow, ").Append((100 * Math.Max(0, Math.Min(1.0, (double)s.VillageOrderFreightPerDay))).ToString("0.#", inv)).Append("%/doba drogi) ").Append(_orFrGold)
                  .Append(" zl, srednio ").Append(_orFrPcs > 0 ? ((double)_orFrDays / _orFrPcs).ToString("0.0", inv) : "-").Append(" dob drogi, jednostek pracy na frach ").Append(J(_orFrU, "0.0"))
                  .Append(", pominiete - sufit ceny z frachtem (pary zlecenie-wytworca) ").Append(_orCapSkip);
                sb.Append(" | zloto: kiesy zamawiajacych -").Append(_orOut).Append(" = kiesy wytworcow +").Append(_orIn).Append(" (zwrot do zamawiajacych ").Append(_orBack)
                  .Append("); rzemieslnicy miast od materialu u wytworcow bez frachtu (czesc placy robot) ok. ").Append(N0(_orWage))
                  .Append(" | zlecen z wytworca ").Append(_orOrders).Append(" (ponad 1/2 zlecenia dzieki wytworcy - bez daniny ").Append(_orNoDue).Append(")")
                  .Append(", popyt niezaspokojony ").Append(_orUnmet).Append(" zl (przypisany wytworcom; bez wytworcy w zasiegu ").Append(_orNoProd).Append(" zl, ").Append(_orNoProdN).Append(" zlecen)")
                  .Append(" | zboze z pola mniej: ").Append(N0(g0)).Append(" kg/d przed zima / ").Append(N0(g1)).Append(" po zimie");
                double ctlP = 0; for (int i = 0; i < 4; i++) ctlP += _orMade[i] + _orCarry0[i] - _orSold[i] - _orFrU[i] - _orIdle[i] - _orCarry1[i];
                sb.Append(" | kontrola zlota (z kies zamawiajacych - do kies wytworcow) = ").Append(_orOut - _orIn)
                  .Append(", kontrola sztuk (zrobione + ulamek z wczoraj - (sprzedane + frach) - bez roboty - ulamek na jutro) = ").Append(Math.Abs(ctlP) < 0.005 ? "0" : ctlP.ToString("0.00", inv))
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
