using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// f5-ruda (noc 18.10, fala 5; koncowy przeglad ekonomii 18.10 "ruda znika z polowy miast", fin88 NOP-7 / NOP-8) - DYMARKI POD KS.
    ///
    /// Pomiar fin88 (Armoury-2026-10-18_03-13-38.log, sciezka Jeffa, KS sam wlaczony 109109) wobec fin84 (bez KS, 21-11-36), srednie 109181-109235:
    /// produkcja lup 240 wobec 271/d, "kopalnia bez wegla" 43 wobec 27 lup/d - na koniec biegu cale 68 lup/d to cztery kopalnie Essos z PUSTA kiesa
    /// (Akirys [Volantis] 33, Bir Seif [Yunkai] 16, Polisia [Norvos] 11, Mivanjan [Vaes Diaf] 8; "pusta kiesa wsi 397" ladunkow drewna/d). Kopalnia bez zlota nie kupi
    /// drewna, bez drewna nie wytopi lup, bez lup nie ma utargu (Bir Seif: utarg 28-254 zl/d przy potrzebie drewna 85 ladunkow po 15 zl) - zamkniete kolo, choc lupa
    /// w polowie miast idzie po 3-10 x wartosci (Ceny surowcow: mediana indeksu rudy 3.18, za pierwsza sztuke 140 d).
    ///
    /// 1. KREDYT KUPCOW NA DREWNO (klucz Bloomery Wood Credit, tylko wsie z KS zastosowanym): w Bloomery.Daily, gdy kiesa wsi nie starcza na drewno z polki miasta
    ///    handlowego, kupcy tego miasta (kasa miasta) daja brakujace drewno na kredyt - drewno z polki do dymarki (ramka ksiegi towarow FBloom, jak zakup), bez zaplaty
    ///    w tej chwili; wies jest winna kupcom wartosc po cenie targu. Warunki: lupa sprzedana temu miastu (cena sprzedazy gry) jest warta wiecej niz drewno, ktore
    ///    spala (WoodPerBloom x cena drewna) - kredyt tylko na wytop, ktory sie oplaca przy dzisiejszych cenach; dlug najwyzej 7 dob potrzeby dymarki ponad las okregu;
    ///    kasa miasta >= dlug (kupcy maja z czego czekac); jeden wierzyciel naraz; bez splaty od 30 dob - nowego kredytu nie ma.
    ///    Splata (zawsze, gdy jest dlug - takze przy kluczu wylaczonym: drewno juz wydane): (a) przy powrocie wozu do wsi macierzystej (VillageTakings, po F4b, przed
    ///    pracami okregu i danina DS - koszt wytopu pierwszy, jak rachunek F4b) - z przyrostu kiesy w tym oknie; (b) w Bloomery.Daily przed nowym zakupem - najwyzej
    ///    1/4 kiesy (jak rata przebudowy A7). Zloto: kiesa wsi -> kasa miasta-wierzyciela (TownFolk.Sale - jak zaplata za drewno z polki). Splacone zloto to
    ///    zaplata za drewno - przy F4b dopisane do rachunku (pan bierze dzialke od utargu bez kosztu drewna, jak przy zakupie za gotowke).
    ///    Odpis: 60 dob bez splaty albo wierzyciel nie jest juz miastem - dlug znika (kupcy wydali drewno bez zaplaty; zadna moneta nie powstaje ani nie znika).
    ///    Zapis: nowy klucz arm_f5_woodcredit ("f5c1|wies:wierzyciel:dlug:ostatnia splata:od"); stary zapis bez klucza - bez dlugow; starsza DLL klucza nie czyta.
    /// 2. POMIAR WYTOPU 7 DOB (dla StartCalibration.F5IronWeek, klucz Ks Iron Hands Follow Market): z Cap - model (lupy chciane) i lupy bez drewna kazdej kopalni;
    ///    pierscien 7 dob w pamieci sesji (bez zapisu).
    /// Linia doby "Ruda kredyt drewna (f5-ruda): dzien N" z tozsamoscia: otwarty wczoraj + nowy - splaty - odpisane = otwarty dzis (reszta 0).
    /// Wyjatki liczone per kopalnia (pierwsze w logu), bez globalnych wylacznikow.
    /// </summary>
    internal static partial class Bloomery
    {
        private const int F5Window = 7;            // doby pierscienia wytopu
        private const int F5CreditDays = 7;        // dlug najwyzej tyle dob potrzeby dymarki ponad las okregu (po cenie drewna dnia kredytu)
        private const int F5StaleDays = 30;        // bez splaty od tylu dob - bez nowego kredytu
        private const int F5WriteOffDays = 60;     // bez splaty od tylu dob - odpis (strata kupcow)

        internal static bool F5RingOn { get { var s = Settings.Current; return _applied && s != null && s.KsIronHandsFollowMarket; } }
        internal static bool F5CreditOn { get { var s = Settings.Current; return _applied && s != null && s.BloomeryWoodCredit; } }

        // ------------------------------------------------------------ pierscien wytopu (pamiec sesji)
        private sealed class F5Ring
        {
            public float ModelDay, UnwonDay;
            public readonly float[] M = new float[F5Window], U = new float[F5Window];
            public int N, At;
            public void Push() { M[At] = ModelDay; U[At] = UnwonDay; At = (At + 1) % F5Window; if (N < F5Window) N++; ModelDay = 0f; UnwonDay = 0f; }
            public float SumM { get { float a = 0f; for (int i = 0; i < N; i++) a += M[i]; return a; } }
            public float SumU { get { float a = 0f; for (int i = 0; i < N; i++) a += U[i]; return a; } }
        }
        private static readonly Dictionary<Village, F5Ring> _f5ring = new Dictionary<Village, F5Ring>();

        // ------------------------------------------------------------ kredyt kupcow (w zapisie arm_f5_woodcredit)
        private sealed class F5Credit { public int Owed; public Settlement Creditor; public int LastPay = -1; public int Since = -1; }
        private static readonly Dictionary<Village, F5Credit> _f5cred = new Dictionary<Village, F5Credit>();
        private static string _f5pending;
        // liczniki doby (linia) i stan "otwarty wczoraj" (-1 = pierwsza linia sesji)
        private static int _f5Lent, _f5LentN, _f5NoProfit, _f5AtCap, _f5Stale, _f5TownPoor, _f5PayCartN, _f5PayPurseN, _f5WriteN, _f5Stum, _f5StumDay;
        private static long _f5LentZl, _f5PayCart, _f5PayPurse, _f5Write, _f5Prev = -1;
        private static readonly HashSet<Village> _f5LentV = new HashSet<Village>();

        private static void F5Reset()
        {
            _f5ring.Clear(); _f5cred.Clear(); _f5pending = null; _f5LentV.Clear();
            _f5Lent = _f5LentN = _f5NoProfit = _f5AtCap = _f5Stale = _f5TownPoor = _f5PayCartN = _f5PayPurseN = _f5WriteN = _f5Stum = _f5StumDay = 0;
            _f5LentZl = _f5PayCart = _f5PayPurse = _f5Write = 0; _f5Prev = -1;
        }

        private static void F5Stumble(string where, Exception e)
        {
            _f5Stum++; _f5StumDay++;
            if (_f5Stum <= 3) { try { Log.Error("Bloomery.F5" + where, e); } catch { } }
        }

        private static int Today5 { get { return (int)CampaignTime.Now.ToDays; } }

        /// <summary>Cap (tick produkcji tej wsi, watek glowny - _tv): model lup i lupy bez drewna do pierscienia doby kopalni. Klucz NIE - nic.</summary>
        private static void F5Note(Village v, float model, float unwon)
        {
            if (v == null || !(model > 0f) || !F5RingOn) return;
            try
            {
                F5Ring r;
                if (!_f5ring.TryGetValue(v, out r)) { r = new F5Ring(); _f5ring[v] = r; }
                r.ModelDay += model; if (unwon > 0f) r.UnwonDay += unwon;
            }
            catch (Exception e) { F5Stumble("Note", e); }
        }

        /// <summary>StartCalibration.F5IronWeek: czesc lup bez drewna w ostatnich (najwyzej 7) dobach kopalni v; -1 - brak danych (kopalnia bez dob w pierscieniu
        /// albo bez modelu), -2 - wytop przy kopalni nieczynny (lupy bez dymarki - kopalnia moze wydobywac). model7 - lupy chciane w oknie.</summary>
        internal static float F5Starved(Village v, out float model7, out int days)
        {
            model7 = 0f; days = 0;
            if (!_applied) return -2f;
            F5Ring r;
            if (v == null || !_f5ring.TryGetValue(v, out r) || r.N <= 0) return -1f;
            model7 = r.SumM; days = r.N;
            if (!(model7 > 0.01f)) return -1f;
            return Math.Max(0f, Math.Min(1f, r.SumU / model7));
        }

        /// <summary>StartCalibration.F5IronWeek (linia): suma okna wytopu wedlug rynku osady (0 Westeros, 1 Essos): model, bez drewna, kopalni z drewnem (< 10% bez drewna -
        /// te dostaja rece przy braku rudy), czesciowo (10-50%), bez drewna (>= 50%).</summary>
        internal static void F5Sums(double[] model, double[] unwon, int[] ok, int[] part, int[] starv)
        {
            foreach (var kv in _f5ring)
            {
                var v = kv.Key; var r = kv.Value;
                if (v == null || r.N <= 0) continue;
                int m = StartCalibration.MarketOfSettlement(v.Settlement);
                if (m < 0 || m > 1) continue;
                float sm = r.SumM, su = r.SumU;
                model[m] += sm; unwon[m] += su;
                if (!(sm > 0.01f)) continue;
                float f = su / sm;
                if (f >= 0.5f) starv[m]++; else if (f >= 0.1f) part[m]++; else ok[m]++;
            }
        }

        // ------------------------------------------------------------ kredyt: udzielenie (Bloomery.Daily, brak zlota)
        /// <summary>Bloomery.Daily (krok b): wies nie ma zlota na `shortN` ladunkow drewna z polki miasta handlowego (cena `price`) - kupcy daja na kredyt, ile wolno.
        /// Drewno z polki do dymarki w ramce ksiegi FBloom (jak zakup), bez zlota w tej chwili; liczniki doby kopalni (Got, NoGold) i linii (brak zlota) przesuniete
        /// o to, co weszlo na kredyt. Nie rzuca.</summary>
        private static void F5Lend(Village v, Mine m, Settlement mk, Town town, ItemRoster shelf, ItemObject wood, int price, int shortN)
        {
            if (shortN <= 0 || v == null || m == null || mk == null || town == null || shelf == null || wood == null || price <= 0 || !F5CreditOn) return;
            try
            {
                if (v.VillageState != Village.VillageStates.Normal || !StartCalibration.AppliedFor(v)) return;
                if (_f5pending != null) F5Resolve();
                int today = Today5;
                F5Credit c;
                _f5cred.TryGetValue(v, out c);
                if (c != null && c.Owed > 0)
                {
                    if (!ReferenceEquals(c.Creditor, mk)) { _f5Stale += shortN; return; }                 // jeden wierzyciel naraz (miasto handlowe sie zmienilo)
                    if (c.LastPay >= 0 && today - c.LastPay > F5StaleDays) { _f5Stale += shortN; return; }
                }
                // oplaca sie: lupa sprzedana temu miastu warta wiecej niz drewno, ktore spala
                int wpb = WoodPerBloom();
                int sell = _iron != null ? Math.Max(0, town.MarketData.GetPrice(_iron, null, true, null)) : 0;
                if (wpb <= 0 || sell <= wpb * price) { _f5NoProfit += shortN; return; }
                float need = m.Want - Math.Min(m.ForAvg, m.Want);   // potrzeba doby ponad las okregu (ta sama miara co linia "kiesy kopaln")
                long cap = (long)F5CreditDays * Math.Max(1, (int)Math.Ceiling(need)) * price;
                long owed = c != null ? c.Owed : 0;
                long room = cap - owed;
                if (room < price) { _f5AtCap += shortN; return; }
                long townRoom = (long)town.Gold - owed;                                                   // kupcy maja z czego czekac: kasa miasta >= dlug
                if (townRoom < price) { _f5TownPoor += shortN; return; }
                int k = (int)Math.Min(shortN, Math.Min(room / price, townRoom / price));
                k = Math.Min(k, shelf.GetItemNumber(wood));
                if (k <= 0) return;
                if (k < shortN) { if (room / price < shortN) _f5AtCap += shortN - k; else _f5TownPoor += shortN - k; }
                var gf = GoodsLedger.Begin(GoodsLedger.FBloom, town);
                try { shelf.AddToCounts(wood, -k); }
                finally { GoodsLedger.End(gf); }
                m.Bank += k;
                m.LocDay += k;                 // F4a: drewno na miejscu (wlasna polka)
                NoteShelf(town, k);            // R1-7b: to, co dymarka naprawde zdjela z polki tego miasta
                m.Got += k; m.NoGold -= k; _dShortGold -= k;
                OreLedger.NoteBloomeryWood(wood, k);
                if (c == null) { c = new F5Credit(); _f5cred[v] = c; }
                if (c.Owed <= 0) { c.Since = today; c.LastPay = today; c.Creditor = mk; }   // zegar zaleglosci od pierwszego kredytu
                c.Owed += k * price;
                _f5Lent += k; _f5LentZl += (long)k * price;
                if (_f5LentV.Add(v)) _f5LentN++;
            }
            catch (Exception e) { F5Stumble("Lend", e); }
        }

        // ------------------------------------------------------------ kredyt: splata
        /// <summary>Splata `ask` zl dlugu wsi v: kiesa wsi -> kasa miasta-wierzyciela (najwyzej tyle, ile kiesa ma); przy F4b splacone zloto dopisane do rachunku
        /// za drewno (jak zaplata za drewno z polki). Zwraca zaplacone.</summary>
        private static int F5Pay(Village v, F5Credit c, int ask, int today)
        {
            if (v == null || c == null || ask <= 0 || c.Owed <= 0) return 0;
            var town = c.Creditor != null ? c.Creditor.Town : null;
            if (town == null) return 0;
            ask = Math.Min(ask, Math.Min(c.Owed, Math.Max(0, v.Gold)));
            if (ask <= 0) return 0;
            int had = v.Gold;
            v.ChangeGold(-ask);
            int paid = had - v.Gold;
            if (paid <= 0) return 0;
            town.ChangeGold(paid);
            c.Owed -= paid; c.LastPay = today;
            if (c.Owed <= 0) { c.Owed = 0; c.Since = -1; }
            try { TownFolk.Sale(c.Creditor, paid, TownFolk.MixGoods); } catch { }   // drewno zaplacone kupcom miasta (tylko licznik) - jak zakup za gotowke
            Mine m;
            if (CoalFirst && _mines.TryGetValue(v, out m)) { m.Bill += paid; m.BuyDay += paid; _bAdded += paid; }   // F4b: koszt drewna przed dzialka pana
            return paid;
        }

        /// <summary>Bloomery.Daily (przed zakupem): splata dlugu z kiesy wsi - najwyzej 1/4 kiesy (odpis - F5Roll). Dziala przy kazdym dlugu (takze przy kluczu
        /// wylaczonym - drewno juz wydane). Nie rzuca.</summary>
        private static void F5RepayPurse(Village v, Mine m)
        {
            if (v == null || (_f5cred.Count == 0 && _f5pending == null)) return;
            try
            {
                if (_f5pending != null) F5Resolve();
                F5Credit c;
                if (!_f5cred.TryGetValue(v, out c) || c.Owed <= 0) return;
                int today = Today5;
                int paid = F5Pay(v, c, Math.Max(0, v.Gold) / 4, today);
                if (paid > 0) { _f5PayPurse += paid; _f5PayPurseN++; }
            }
            catch (Exception e) { F5Stumble("RepayPurse", e); }
        }

        /// <summary>VillageTakings.EnterPostfix (woz oddal utarg we wsi MACIERZYSTEJ, po F4b, przed pracami okregu i danina DS): splata dlugu z przyrostu kiesy w tym oknie
        /// (`grown` - utarg wsi po dzialce pana, 112 i zwrocie F4b). Brak dlugu - nic. Nie rzuca.</summary>
        internal static void F5CreditAtCart(Village home, long grown)
        {
            if (home == null || grown <= 0 || (_f5cred.Count == 0 && _f5pending == null)) return;
            try
            {
                if (_f5pending != null) F5Resolve();
                F5Credit c;
                if (!_f5cred.TryGetValue(home, out c) || c.Owed <= 0) return;
                int paid = F5Pay(home, c, (int)Math.Min(int.MaxValue, grown), Today5);
                if (paid > 0) { _f5PayCart += paid; _f5PayCartN++; }
            }
            catch (Exception e) { F5Stumble("CreditAtCart", e); }
        }

        private static long F5Open(out int n)
        {
            long s = 0; n = 0;
            foreach (var c in _f5cred.Values) if (c.Owed > 0) { s += c.Owed; n++; }
            return s;
        }

        // ------------------------------------------------------------ koniec doby (NewDay - po linii "Zelazo (lupy)")
        /// <summary>Bloomery.NewDay: pierscien wytopu (przy kluczu Ks Iron Hands Follow Market) i linia kredytu drewna (gdy klucz kredytu TAK albo jest dlug); liczniki od zera.</summary>
        private static void F5Roll()
        {
            if (F5RingOn) foreach (var r in _f5ring.Values) r.Push();
            if (!_applied) return;
            if (_f5pending != null) F5Resolve();
            // odpis: 60 dob bez splaty albo wierzyciel nie jest juz miastem - kupcy wydali drewno bez zaplaty (zloto sie nie rusza)
            int today = Today5;
            foreach (var c in _f5cred.Values)
            {
                if (c == null || c.Owed <= 0) continue;
                if (c.Creditor == null || c.Creditor.Town == null || (c.LastPay >= 0 && today - c.LastPay > F5WriteOffDays))
                {
                    _f5Write += c.Owed; _f5WriteN++;
                    c.Owed = 0; c.Since = -1;
                }
            }
            int openN;
            long open = F5Open(out openN);
            bool on = F5CreditOn;
            // linia: KS zastosowany (kredyt mozliwy) albo jest dlug / ruch; gra przed KS i klucz NIE bez dlugu - bez linii
            if ((on && StartCalibration.Applied) || open > 0 || _f5Prev > 0 || _f5PayCart + _f5PayPurse + _f5Write > 0)
            {
                var inv = CultureInfo.InvariantCulture;
                var sb = new StringBuilder(700);
                sb.Append("Ruda kredyt drewna (f5-ruda): dzien ").Append(Today5).Append(" | klucz Bloomery Wood Credit ").Append(on ? "TAK" : "NIE (bez nowego kredytu, splaty dalej)")
                  .Append(" | drewno na kredyt dzis ").Append(_f5Lent).Append(" ladunkow za ").Append(_f5LentZl).Append(" zl w ").Append(_f5LentN).Append(" kopalniach (z polki miasta handlowego do dymarki, bez zaplaty w tej chwili)")
                  .Append("; odmowy (ladunki): bez zysku przy dzisiejszych cenach ").Append(_f5NoProfit).Append(", limit ").Append(F5CreditDays).Append(" dob potrzeby ").Append(_f5AtCap)
                  .Append(", inny wierzyciel albo bez splaty ponad ").Append(F5StaleDays).Append(" dob ").Append(_f5Stale).Append(", kasa miasta mniejsza od dlugu ").Append(_f5TownPoor)
                  .Append(" | splaty (kiesy wsi -> kasy miast-wierzycieli): przy powrocie wozu ").Append(_f5PayCart).Append(" zl (").Append(_f5PayCartN).Append("), z kiesy wsi ")
                  .Append(_f5PayPurse).Append(" zl (").Append(_f5PayPurseN).Append(")")
                  .Append(" | odpisane (").Append(F5WriteOffDays).Append(" dob bez splaty albo wierzyciel nie jest miastem - drewno wydane bez zaplaty, strata kupcow, zadna moneta nie powstaje) ")
                  .Append(_f5Write).Append(" zl (").Append(_f5WriteN).Append(")")
                  .Append(" | otwarty kredyt ").Append(open).Append(" zl w ").Append(openN).Append(" kopalniach");
                if (_f5Prev >= 0)
                    sb.Append("; kontrola: otwarty wczoraj ").Append(_f5Prev).Append(" + nowy ").Append(_f5LentZl).Append(" - splaty ").Append(_f5PayCart + _f5PayPurse)
                      .Append(" - odpisane ").Append(_f5Write).Append(" - otwarty dzis = reszta ").Append(_f5Prev + _f5LentZl - _f5PayCart - _f5PayPurse - _f5Write - open);
                else sb.Append("; kontrola od nastepnej doby");
                // najwieksze dlugi (3)
                var top = new List<KeyValuePair<Village, F5Credit>>();
                foreach (var kv in _f5cred) if (kv.Key != null && kv.Value.Owed > 0) top.Add(kv);
                top.Sort((a, b) => b.Value.Owed.CompareTo(a.Value.Owed));
                sb.Append(" | najwieksze dlugi: ");
                if (top.Count == 0) sb.Append("-");
                for (int i = 0; i < top.Count && i < 3; i++)
                {
                    var v = top[i].Key; var c = top[i].Value;
                    if (i > 0) sb.Append(", ");
                    sb.Append(v.Name != null ? v.Name.ToString() : "?").Append(" [").Append(c.Creditor != null && c.Creditor.Name != null ? c.Creditor.Name.ToString() : "?").Append("] ")
                      .Append(c.Owed).Append(" zl (od ").Append(c.Since >= 0 ? (Today5 - c.Since).ToString(inv) : "-").Append(" dob, splata ")
                      .Append(c.LastPay >= 0 ? (Today5 - c.LastPay).ToString(inv) : "-").Append(" dob temu, kiesa ").Append(v.Gold).Append(")");
                }
                sb.Append(" | potkniecia dzis ").Append(_f5StumDay).Append(" (sesja ").Append(_f5Stum).Append(").");
                Log.Info(sb.ToString());
            }
            _f5Prev = open;
            _f5Lent = _f5LentN = _f5NoProfit = _f5AtCap = _f5Stale = _f5TownPoor = _f5PayCartN = _f5PayPurseN = _f5WriteN = _f5StumDay = 0;
            _f5LentZl = _f5PayCart = _f5PayPurse = _f5Write = 0; _f5LentV.Clear();
        }

        // ------------------------------------------------------------ zapis (arm_f5_woodcredit)
        internal static string F5Export()
        {
            try
            {
                if (_f5pending != null) return _f5pending;   // wczytane, jeszcze nie rozwiazane - oddajemy jak bylo
                var sb = new StringBuilder("f5c1");
                int n = 0;
                foreach (var kv in _f5cred)
                {
                    var st = kv.Key != null ? kv.Key.Settlement : null; var c = kv.Value;
                    if (st == null || st.StringId == null || c == null || c.Owed <= 0 || c.Creditor == null || c.Creditor.StringId == null) continue;
                    sb.Append('|').Append(st.StringId).Append(':').Append(c.Creditor.StringId).Append(':').Append(c.Owed.ToString(CultureInfo.InvariantCulture))
                      .Append(':').Append(c.LastPay.ToString(CultureInfo.InvariantCulture)).Append(':').Append(c.Since.ToString(CultureInfo.InvariantCulture));
                    n++;
                    if (sb.Length > 60000) { Log.Info("Bloomery (f5-ruda): zapis kredytu drewna ponad 60000 znakow - reszta dlugow pominieta (" + n + " zapisanych)."); break; }   // straznik rozmiaru
                }
                return n > 0 ? sb.ToString() : "";
            }
            catch (Exception e) { F5Stumble("Export", e); return ""; }
        }

        internal static void F5Import(string s)
        {
            _f5cred.Clear();
            _f5pending = !string.IsNullOrEmpty(s) && s.StartsWith("f5c1", StringComparison.Ordinal) ? s : null;
        }

        private static void F5Resolve()
        {
            var p = _f5pending;
            if (p == null) return;
            _f5pending = null;
            int ok = 0, bad = 0; long sum = 0;
            var inv = CultureInfo.InvariantCulture;
            var recs = p.Split('|');
            for (int i = 1; i < recs.Length; i++)
            {
                try
                {
                    var f = recs[i].Split(':');
                    if (f.Length != 5) { bad++; continue; }
                    var vs = Settlement.Find(f[0]); var cs = Settlement.Find(f[1]);
                    int owed = int.Parse(f[2], inv);
                    if (vs == null || vs.Village == null || cs == null || owed <= 0) { bad++; continue; }
                    _f5cred[vs.Village] = new F5Credit { Owed = owed, Creditor = cs, LastPay = int.Parse(f[3], inv), Since = int.Parse(f[4], inv) };
                    ok++; sum += owed;
                }
                catch { bad++; }
            }
            Log.Info("Bloomery (f5-ruda): z zapisu kredyt drewna kupcow - dlugow " + ok + " (" + sum + " zl)" + (bad > 0 ? ", nieczytelnych " + bad : "") + ".");
        }
    }
}
