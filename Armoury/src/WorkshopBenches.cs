using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Workshops;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// 435-L8 - LANCUCH PODAZY (LP), krok 8: STANOWISKA WARSZTATOW z kapitalu warsztatu. Klucz Workshop Benches (NIE = mnoznik 1, zadnego zakupu ani cofniecia, stan
    /// w zapisie zamrozony - kod co do znaku jak przed 435-L8). Projekt: PROJEKT-LANCUCH-po-krytyce (11.11), rozdz. II.8, II.10, II.11, II.19 wiersz L8. Polecenie Jeffa:
    /// "jak produkuja warsztaty juz na 100% to powstaja nowe warsztaty" - Town.Workshops gry nie da sie powiekszyc (tablica 4 miejsc, znaczniki sceny), wiec "nowy warsztat"
    /// to kolejne stanowiska w warsztacie, ktory pracuje pelna para, a jego wyrobu brakuje.
    ///
    /// Regula (warsztat notabla albo lorda w MIESCIE, nieukryty, z choc jedna linia towarowa z wsadem; warsztaty gracza i zamki poza regula):
    ///  - poziom 1-4; linie towarowe pod tempem rynku (WorkshopPace) biegna x (1 + 0.5 x (poziom - 1)) PRZED zaciskiem F - sufit F 4 zostaje. Place cyklu bez zmian na cykl
    ///    (wiecej cykli = wiecej wsadu z polki i wiecej plac, jak przy wiekszym F);
    ///  - wykorzystanie u = cykle udane / (udane + nieudane) doby (pomiar SupplySignal.CycleEnd, L1); pamiec to srednie kroczace dobowego u o stalej 56 i 182 doby
    ///    (doby bez proby cyklu nie wchodza do srednich);
    ///  - NOWY POZIOM, gdy razem: u56 &gt;= 0.90 i co najmniej 56 dob z proba od ostatniej zmiany poziomu; N glownego wyrobu ktorejs linii w tym miescie &gt;= 1.3 (tablica
    ///    SupplySignal - cena poza N); caly wsad ktorejs linii jest w okregu miasta (WorkshopInputs.InputsNear / kontrakt); kraina ma wolne rece (FreeHands.Braked);
    ///    stanowisk ponad pierwsze w miescie mniej niz 2 x m_t (ludzie rynku, PeopleMarket.WorkersScale; 1-8);
    ///  - PLATNIK: kapital warsztatu PONAD znacznik kapitalu poczatkowego (Workshop.InitialCapital - to niewyplacony zysk; gra wyplaca wlascicielowi 1/5 na dobe), nigdy kiesa
    ///    notabla (gra trzyma ja w pasie 4500-10500 i moze dosypac - krytyka B10). Cena poziomu = koszt zmiany produkcji typu (GetConvertProductionCost, juz x Workshop Trade
    ///    Equipment Scale). Placona RATAMI co 7 dob (1/7 miast na dobe): polowa raty to material z polki WLASNEGO miasta (narzedzia, deski albo drewno - nie towar, ktory
    ///    warsztat sam wyrabia; zapasy polki i sufit ceny jak roboty gminy) -> kasa miasta, druga polowa -> rzemieslnicy (kiesy ludu; czesc nieprzyjeta zostaje w kasie).
    ///    Bez materialu - bez raty. Koncowka ceny mniejsza od 5% (mniej niz sztuka materialu) - same place. Poziom rosnie po zaplaceniu calej ceny;
    ///  - ZANIK: u182 &lt; 0.30 przy co najmniej 182 dobach z proba od ostatniej zmiany poziomu, albo 182 doby bez zadnej proby cyklu -> poziom - 1 (wplacone raty przepadaja -
    ///    odpis w logu); zmiana typu warsztatu (przezbrojenie, bankructwo) -> poziom 1.
    /// Zamkniecie: kapital warsztatu -> kasa miasta + kiesy ludu (TownFolk.BenchPayRun, zmierzone na zywych kiesach); sztuki zdjete z polki = sztuki kupione (ksiega towarow
    /// FBuild "stanowiska warsztatow"); poziomy dzis = wczoraj + kupione - cofniete - utracone zmiana typu. Kazda kontrola w linii "LP warsztaty" ma dac 0.
    /// Zapis arm_lpw ("lpw1|warsztat~typ~poziom~wplacone~u56~u182~doby z proba~doby bez proby;..."); brak klucza = stan pusty (kazdy warsztat poziom 1); zly wpis pominiety
    /// i policzony. Bez klucza Supply Chain Log pomiar u stoi: nic nie jest kupowane ani cofane, kupione poziomy dzialaja dalej. Zadnej nowej latki Harmony.
    /// Watek glowny (odczyt mnoznika pod blokada); wyjatek per warsztat = potkniecie liczone w linii, bez globalnego wylacznika (CLAUDE.md 7).
    /// </summary>
    internal static class WorkshopBenches
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private const string Ver = "lpw1";
        internal const int MaxLevel = 4;
        private const double UpU = 0.90, DownU = 0.30, NeedN = 1.3, Step = 0.5;
        private const int UpDays = 56, DownDays = 182, TownCycle = 7;

        private sealed class Rec
        {
            public string Key, Type;
            public int Level = 1;
            public long Paid;                  // wplacone na nastepny poziom (raty)
            public float U56 = -1f, U182 = -1f; // srednie kroczace dobowego u; -1 = bez pomiaru
            public int Seen, Idle;             // doby z proba cyklu od ostatniej zmiany poziomu; kolejne doby bez proby
            public int DDone, DFail;           // cykle tej doby (sesja)
        }

        private static readonly object Gate = new object();
        private static readonly Dictionary<string, Rec> _byKey = new Dictionary<string, Rec>(StringComparer.Ordinal);
        private static readonly Dictionary<Workshop, Rec> _byShop = new Dictionary<Workshop, Rec>();
        private static string _line, _loadNote = "nowa kampania (bez zapisu)";
        private static int _loadBad, _st, _stSess;
        private static bool _startLine, _frozenLine;
        private static readonly HashSet<string> _err = new HashSet<string>();
        private static ItemObject[] _items; private static int _itemsDay = int.MinValue;

        // powody odmowy raty (linia doby)
        private const int WFull = 0, WUse = 1, WShort = 2, WInput = 3, WHands = 4, WTownCap = 5, WCapital = 6, WNoShelf = 7, WPriceCap = 8, WPace = 9, WOther = 10, WKinds = 11;
        private static readonly string[] WName =
        {
            "poziom 4", "u56 < 0.90 albo mniej niz 56 dob z proba", "N wyrobu < 1.3", "bez wsadu w okregu", "kraina bez wolnych rak", "pulap stanowisk miasta (2 x m_t)",
            "kapital nie ponad znacznik (albo rata za mala)", "bez materialu na polce", "material ponad sufit ceny", "tempo rynku nieczynne / bez linii z wsadem", "inne (droga materialu, lud miasta, blad)"
        };

        internal static bool On { get { var s = Settings.Current; return s != null && s.WorkshopBenches; } }

        private static void Stumble(string where, Exception e)
        {
            _st++; _stSess++;
            try { if (_err.Add(where)) Log.Error("WorkshopBenches." + where, e); } catch { }
        }

        /// <summary>Nowa kampania / przed wczytaniem (konstruktor ArmouryBehavior): stan i liczniki od zera.</summary>
        internal static void Reset()
        {
            lock (Gate)
            {
                _byKey.Clear(); _byShop.Clear(); _line = null; _loadNote = "nowa kampania (bez zapisu)"; _loadBad = 0;
                _st = _stSess = 0; _startLine = false; _frozenLine = false; _err.Clear(); _items = null; _itemsDay = int.MinValue;
            }
        }

        private static bool IdOk(string s) { return !string.IsNullOrEmpty(s) && s.IndexOf('~') < 0 && s.IndexOf(';') < 0 && s.IndexOf('|') < 0; }

        private static string KeyOf(Workshop w)
        {
            var st = w.Settlement;
            return (st != null ? st.StringId : "?") + ":" + (w.Tag ?? "?");
        }

        private static string TypeOf(Workshop w) { return w.WorkshopType != null ? (w.WorkshopType.StringId ?? "?") : "?"; }

        /// <summary>Rekord warsztatu (pod blokada): z pamieci obiektu, inaczej z zapisu po kluczu; `create` - nowy rekord poziomu 1.</summary>
        private static Rec Find(Workshop w, bool create)
        {
            Rec r;
            if (_byShop.TryGetValue(w, out r)) { if (r != null || !create) return r; }
            string k = KeyOf(w);
            if (!_byKey.TryGetValue(k, out r) && create && IdOk(k))
            {
                r = new Rec { Key = k, Type = TypeOf(w) };
                _byKey[k] = r;
            }
            _byShop[w] = r;
            return r;
        }

        /// <summary>WorkshopPace.LineFactor: mnoznik tempa linii towarowych warsztatu od stanowisk - 1 + 0.5 x (poziom - 1); 1 przy wylaczonym kluczu, bez rekordu,
        /// poziomie 1 albo typie innym niz zapamietany (zmiana typu - Daily sprowadzi poziom do 1). Sam odczyt, nie rzuca.</summary>
        internal static float Factor(Workshop w)
        {
            try
            {
                if (!On || w == null || w.WorkshopType == null) return 1f;
                Rec r;
                lock (Gate) { r = Find(w, false); }
                if (r == null || r.Level <= 1 || !string.Equals(r.Type, w.WorkshopType.StringId, StringComparison.Ordinal)) return 1f;
                return 1f + (float)Step * (Math.Min(MaxLevel, r.Level) - 1);
            }
            catch (Exception e) { Stumble("Factor", e); return 1f; }
        }

        /// <summary>SupplySignal.CycleEnd (linia towarowa warsztatu w miescie, nieukryty; tylko przy kluczu Supply Chain Log): wynik cyklu do licznika doby. Nie rzuca.</summary>
        internal static void Observe(Workshop w, bool ok)
        {
            try
            {
                if (!On || w == null || w.Owner == null || w.Owner == Hero.MainHero) return;
                lock (Gate)
                {
                    var r = Find(w, true);
                    if (r == null) return;
                    if (ok) r.DDone++; else r.DFail++;
                }
            }
            catch (Exception e) { Stumble("Observe", e); }
        }

        /// <summary>SupplySignal.WorkshopText: czesc linii "LP warsztaty" policzona tej doby (null - brak).</summary>
        internal static string TakeLine() { var l = _line; _line = null; return l; }

        private static bool Paced(WorkshopType.Production p) { return p.Inputs != null && p.Inputs.Count > 0 && WorkshopTrade.IsTradeLine(p); }

        /// <summary>Materialy stanowisk (narzedzia, deski, drewno) w kolejnosci BuildFunding.DsWorksItems; pozostale materialy budow - null. Raz na dobe.</summary>
        private static ItemObject[] BenchItems(int today)
        {
            if (_items != null && _itemsDay == today) return _items;
            var all = BuildFunding.DsWorksItems();
            var names = BuildFunding.DsWorksMatNames.Split('/');
            for (int k = 0; k < all.Length; k++)
            {
                string n = k < names.Length ? names[k] : "";
                if (n != "tools" && n != "planks" && n != "hardwood") all[k] = null;
            }
            _items = all; _itemsDay = today;
            return all;
        }

        private static int SumLevels()
        {
            int x = 0;
            foreach (var r in _byKey.Values) x += Math.Max(0, r.Level - 1);
            return x;
        }

        // ============================================================ doba
        /// <summary>ArmouryBehavior.OnDailyTick (po budowach, zleceniach gminy i fundatorach sprzetu tej doby, przed migawka MBuild): pamiec u, cofniecia, raty. Nie rzuca.</summary>
        internal static void Daily()
        {
            var s = Settings.Current;
            if (s == null || Campaign.Current == null) return;
            long t0 = Stopwatch.GetTimestamp();
            int today = (int)CampaignTime.Now.ToDays;
            lock (Gate)
            {
                try
                {
                    if (!s.WorkshopBenches)
                    {
                        int above = 0;
                        foreach (var r in _byKey.Values) { if (r.Level > 1) above++; r.DDone = r.DFail = 0; }
                        _line = "stanowiska (435-L8): WYLACZONE (MCM) - mnoznik 1, stan w zapisie zamrozony (warsztatow z poziomem ponad 1: " + above + ")";
                        return;
                    }
                    if (!_startLine)
                    {
                        _startLine = true;
                        Log.Info("LP stanowiska start (435-L8): klucz Workshop Benches TAK | warsztat notabla albo lorda w miescie, linie towarowe z wsadem x (1 + 0.5 x (poziom - 1)), poziom 1-4, sufit F 4"
                                 + " | nowy poziom: u56 >= 0.90 (>= 56 dob z proba), N wyrobu w miescie >= 1.3, wsad w okregu, wolne rece krainy, pulap miasta 2 x m_t; cena = koszt zmiany produkcji typu,"
                                 + " raty co 7 dob z kapitalu warsztatu ponad znacznik: polowa material z polki miasta (narzedzia, deski, drewno), polowa rzemieslnicy; zanik: u182 < 0.30 albo 182 doby bez proby"
                                 + " | stan: " + _loadNote + ", rekordow " + _byKey.Count + ", zlych wpisow " + _loadBad
                                 + " | tempo rynku (Workshop Pace By Market) " + (WorkshopPace.On ? (WorkshopPace.Wired ? "czynne" : "LATKA NIE WPIETA - stanowiska bez skutku, bez zakupow") : "WYLACZONE - stanowiska bez skutku, bez zakupow")
                                 + ", droga materialu (Ds Works Real Material) " + (TownFolk.DaWorksOn ? "czynna" : "NIECZYNNA - bez zakupow")
                                 + ", pomiar (Supply Chain Log) " + (SupplySignal.On ? "czynny" : "WYLACZONY - pamiec u stoi, bez zakupow i cofniec")
                                 + " | kolejnosc doby: po VillageGear.FundDaily, przed migawka MBuild.");
                    }
                    bool measure = SupplySignal.On;
                    if (!measure && !_frozenLine)
                    {
                        _frozenLine = true;
                        Log.Info("LP stanowiska (435-L8): dzien " + today + " | klucz Supply Chain Log wylaczony - pomiar wykorzystania stoi: zadnych rat ani cofniec, kupione poziomy dzialaja dalej (linia raz na sesje).");
                    }
                    Run(s, today, measure, t0);
                }
                catch (Exception e) { Stumble("Daily", e); }
                finally { foreach (var r in _byKey.Values) r.DDone = r.DFail = 0; }
            }
        }

        private static void Run(Settings s, int today, bool measure, long t0)
        {
            int before = SumLevels();
            var lv = new int[MaxLevel + 1];
            var why = new int[WKinds];
            int recs = 0, playerRecs = 0, inProg = 0, towns = 0, cand = 0, up = 0, inst = 0, down = 0, downIdle = 0, lostType = 0, lostLevels = 0, pcs = 0, drop = 0, dropUnknown = 0, measured = 0, withF = 0;
            long progGold = 0, mat = 0, wFolk = 0, wCof = 0, gap = 0, lostPaid = 0, downPaid = 0;
            double uSum = 0;
            bool canBuy = measure && WorkshopPace.On && WorkshopPace.Wired && WorkshopTrade.On && TownFolk.DaWorksOn;
            var table = measure ? SupplySignal.Table : null;
            var model = Campaign.Current.Models != null ? Campaign.Current.Models.WorkshopModel : null;
            int ti = -1;
            foreach (var t in Town.AllTowns)
            {
                if (t == null || t.Settlement == null || !t.IsTown || t.Workshops == null) continue;
                ti++;
                bool slot = ((ti % TownCycle) + TownCycle) % TownCycle == ((today % TownCycle) + TownCycle) % TownCycle;
                if (slot) towns++;
                int extra = 0;                                         // stanowiska ponad pierwsze w miescie (po cofnieciach tej doby)
                var live = new List<KeyValuePair<Workshop, Rec>>(4);
                foreach (var w in t.Workshops)
                {
                    try
                    {
                        if (w == null || w.WorkshopType == null || w.WorkshopType.IsHidden) continue;
                        var r = Find(w, false);
                        if (r == null) continue;
                        recs++;
                        string type = TypeOf(w);
                        if (!string.Equals(r.Type, type, StringComparison.Ordinal))
                        {
                            // zmiana typu (przezbrojenie, bankructwo, cudzy kod): stanowiska starego rzemiosla i wplacone raty przepadaja, pamiec u od nowa
                            if (r.Level > 1 || r.Paid > 0) { lostType++; lostLevels += r.Level - 1; lostPaid += r.Paid; }
                            r.Type = type; r.Level = 1; r.Paid = 0; r.U56 = r.U182 = -1f; r.Seen = r.Idle = 0;
                        }
                        if (w.Owner == null || w.Owner == Hero.MainHero) { playerRecs++; lv[Math.Max(1, Math.Min(MaxLevel, r.Level))]++; extra += r.Level - 1; continue; }   // gracz: stan zamrozony, mnoznika nie ma (WorkshopPace)
                        if (measure)
                        {
                            int a = r.DDone + r.DFail;
                            if (a > 0)
                            {
                                float u = r.DDone / (float)a;
                                r.U56 = r.U56 < 0f ? u : r.U56 + (u - r.U56) / UpDays;
                                r.U182 = r.U182 < 0f ? u : r.U182 + (u - r.U182) / DownDays;
                                if (r.Seen < int.MaxValue) r.Seen++;
                                r.Idle = 0;
                            }
                            else if (r.Idle < int.MaxValue) r.Idle++;
                            bool idle = r.Idle >= DownDays;
                            if (r.Level > 1 && (idle || (r.Seen >= DownDays && r.U182 >= 0f && r.U182 < DownU)))
                            {
                                r.Level--; down++; if (idle) downIdle++;
                                downPaid += r.Paid; r.Paid = 0; r.Seen = 0; r.Idle = 0;
                            }
                        }
                        if (r.U56 >= 0f) { measured++; uSum += r.U56; }
                        extra += r.Level - 1;
                        live.Add(new KeyValuePair<Workshop, Rec>(w, r));
                    }
                    catch (Exception e) { Stumble("Daily(warsztat)", e); }
                }
                Dictionary<ItemCategory, float> near = null;
                foreach (var kv in live)
                {
                    var w = kv.Key; var r = kv.Value;
                    try
                    {
                        if (slot && measure)
                        {
                            cand++;
                            int res = TryBuy(t, w, r, today, canBuy, table, model, ref near, ref extra, ref inst, ref up, ref pcs, ref drop, ref dropUnknown, ref mat, ref wFolk, ref wCof, ref gap);
                            if (res >= 0 && res < WKinds) why[res]++;
                        }
                    }
                    catch (Exception e) { Stumble("Daily(rata)", e); }
                    lv[Math.Max(1, Math.Min(MaxLevel, r.Level))]++;
                    if (r.Level > 1) withF++;
                    if (r.Paid > 0) { inProg++; progGold += r.Paid; }
                }
            }
            int after = SumLevels();
            var sb = new StringBuilder(900);
            sb.Append("stanowiska (435-L8): ").Append(measure ? (canBuy ? "CZYNNE" : "BEZ ZAKUPOW (tempo rynku, warsztaty towarowe albo droga materialu nieczynne)") : "POMIAR STOI (Supply Chain Log wylaczony)")
              .Append(" | warsztatow z pamiecia ").Append(recs).Append(" (rekordow w zapisie ").Append(_byKey.Count).Append(", gracza - zamrozone ").Append(playerRecs).Append("), poziomy 1/2/3/4 = ")
              .Append(lv[1]).Append('/').Append(lv[2]).Append('/').Append(lv[3]).Append('/').Append(lv[4])
              .Append(", z mnoznikiem ponad 1: ").Append(withF).Append(", w trakcie rat ").Append(inProg).Append(" (wplacone ").Append(progGold).Append(" zl)")
              .Append(" | u56 srednie ").Append(measured > 0 ? (uSum / measured).ToString("0.00", Inv) : "-").Append(" z ").Append(measured).Append(" warsztatow")
              .Append(" | dzis miast w kolejce ").Append(towns).Append(" (1/7), warsztatow ocenionych ").Append(cand)
              .Append(": rat ").Append(inst).Append(" za ").Append(mat + wFolk + wCof).Append(" zl = material ").Append(mat).Append(" (sztuk ").Append(pcs).Append(") + rzemieslnicy do ludu ").Append(wFolk)
              .Append(" + rzemieslnicy zatrzymane w kasie miasta ").Append(wCof).Append("; nowych poziomow ").Append(up)
              .Append(" | odmowy:");
            bool f = true;
            for (int i = 0; i < WKinds; i++) if (why[i] > 0) { sb.Append(f ? " " : ", ").Append(WName[i]).Append(' ').Append(why[i]); f = false; }
            if (f) sb.Append(" -");
            sb.Append(" | cofniete ").Append(down).Append(" (w tym 182 doby bez proby ").Append(downIdle).Append("; przepadle raty ").Append(downPaid).Append(" zl), utracone zmiana typu ").Append(lostType)
              .Append(" (poziomow ").Append(lostLevels).Append(", raty ").Append(lostPaid).Append(" zl)")
              .Append(" | kontrola zlota (z kapitalu - kasa miasta - lud) ").Append(gap)
              .Append(", sztuk (kupione - zmiana polek) ").Append(pcs - drop).Append(dropUnknown > 0 ? " (polka nie przeczytana " + dropUnknown + " razy)" : "")
              .Append(", poziomow (dzis - (wczoraj + kupione - cofniete - utracone)) ").Append(after - (before + up - down - lostLevels))
              .Append(" | koszt ").Append(((Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency).ToString("0.00", Inv)).Append(" ms")
              .Append(" | potkniecia ").Append(_st).Append(" (sesja ").Append(_stSess).Append(")");
            _st = 0;
            _line = sb.ToString();
            // bez linii "LP warsztaty" (log wylaczony albo pomiar stoi) zdarzenia i tak ida do logu - nic nie ginie po cichu
            if ((!measure || !(s.LogEnabled && LogGate.Lines)) && (inst > 0 || up > 0 || down > 0 || lostType > 0 || gap != 0) && s.LogEnabled)
            {
                Log.Info("LP warsztaty: dzien " + today + " | " + _line);
                _line = null;
            }
        }

        /// <summary>Ocena jednego warsztatu w dobie jego miasta: rata na nastepny poziom albo powod odmowy (indeks WName); -1 = rata zaplacona.</summary>
        private static int TryBuy(Town t, Workshop w, Rec r, int today, bool canBuy, SupplySignal.NTable table, TaleWorlds.CampaignSystem.ComponentInterfaces.WorkshopModel model,
                                  ref Dictionary<ItemCategory, float> near, ref int extra, ref int inst, ref int up, ref int pcs, ref int drop, ref int dropUnknown,
                                  ref long mat, ref long wFolk, ref long wCof, ref long gap)
        {
            if (r.Level >= MaxLevel) return WFull;
            var type = w.WorkshopType;
            if (!canBuy || model == null || !WorkshopPace.HasTradeLine(type)) return WPace;
            if (r.Seen < UpDays || r.U56 < UpU) return WUse;
            // N glownego wyrobu linii towarowych z wsadem w tym miescie
            double nMax = 1.0;
            var outs = new HashSet<ItemCategory>();
            foreach (var p in type.Productions)
            {
                if (!Paced(p)) continue;
                foreach (var o in p.Outputs) if (o.Item1 != null) outs.Add(o.Item1);
                var main = p.Outputs[0].Item1;
                if (main == null) continue;
                double n = SupplySignal.TownN(table, t.Settlement, main);
                if (n > nMax) nMax = n;
            }
            if (table == null || nMax < NeedN) return WShort;
            if (near == null) near = WorkshopInputs.InputsNear(t);
            if (!WorkshopInputs.HasInput(type, near)) return WInput;
            if (FreeHands.Braked(t.Settlement)) return WHands;
            float m = PeopleMarket.WorkersScale(t);
            if (!(m > 0f) || float.IsInfinity(m)) m = 1f;
            int capTown = Math.Max(1, Math.Min(8, (int)Math.Floor(2.0 * m + 0.5)));
            if (extra >= capTown) return WTownCap;
            int cost = Math.Max(0, model.GetConvertProductionCost(type));
            if (cost < 20) return WOther;
            long excess = (long)w.Capital - Math.Max(0, w.InitialCapital);
            long remaining = Math.Max(0L, cost - r.Paid);
            long tail = Math.Max(2L, cost / 20);
            TownFolk.BenchPay pay = null;
            if (remaining > tail)
            {
                long budget = Math.Min(remaining, excess);
                if (budget < Math.Min(remaining, Math.Max(20L, cost / 10))) return WCapital;
                // materialy: narzedzia, deski, drewno - bez towaru, ktory warsztat sam wyrabia (kuznia narzedzi nie wykupuje narzedzi, ktorych miastu brakuje)
                var basis = BenchItems(today);
                var items = new ItemObject[basis.Length];
                bool any = false;
                for (int k = 0; k < basis.Length; k++)
                {
                    var it = basis[k];
                    if (it == null || it.ItemCategory == null || outs.Contains(it.ItemCategory)) continue;
                    items[k] = it; any = true;
                }
                if (!any) return WNoShelf;
                pay = TownFolk.BenchPayRun(w, t.Settlement, Math.Floor(budget / 2.0), items, 0);
                if (pay.Why != TownFolk.GfOk)
                    return pay.Why == TownFolk.GfNoShelf ? WNoShelf : pay.Why == TownFolk.GfPriceCap ? WPriceCap : pay.Why == TownFolk.GfNoGold ? WCapital : WOther;
                Book(pay, r, ref inst, ref pcs, ref drop, ref dropUnknown, ref mat, ref wFolk, ref wCof, ref gap);
                remaining = Math.Max(0L, cost - r.Paid);
                excess = (long)w.Capital - Math.Max(0, w.InitialCapital);
            }
            if (remaining > 0 && remaining <= tail)
            {
                // koncowka ceny mniejsza od sztuki materialu: same place rzemieslnikow (cala cena poziomu zawsze zaplacona)
                if (excess >= remaining)
                {
                    var end = TownFolk.BenchPayRun(w, t.Settlement, 0, BenchItems(today), remaining);
                    if (end.Why == TownFolk.GfOk) { Book(end, r, ref inst, ref pcs, ref drop, ref dropUnknown, ref mat, ref wFolk, ref wCof, ref gap); remaining = Math.Max(0L, cost - r.Paid); if (pay == null) pay = end; }
                    else if (pay == null) return end.Why == TownFolk.GfNoGold ? WCapital : WOther;
                }
                else if (pay == null) return WCapital;
            }
            if (remaining <= 0)
            {
                r.Level++; r.Paid = 0; r.Seen = 0; r.Idle = 0; extra++; up++;
                try
                {
                    Log.Info("LP stanowiska (435-L8): dzien " + today + " | " + (t.Name != null ? t.Name.ToString() : "?") + " (" + t.Settlement.StringId + "), " + TypeOf(w) + " - poziom " + r.Level
                             + " (mnoznik linii x" + (1.0 + Step * (r.Level - 1)).ToString("0.0", Inv) + "), cena " + cost + " zl z kapitalu warsztatu, u56 " + r.U56.ToString("0.00", Inv) + ".");
                }
                catch { }
            }
            return -1;
        }

        private static void Book(TownFolk.BenchPay p, Rec r, ref int inst, ref int pcs, ref int drop, ref int dropUnknown, ref long mat, ref long wFolk, ref long wCof, ref long gap)
        {
            r.Paid += p.Paid;
            inst++; pcs += p.Pcs; mat += p.Mat; wFolk += p.WageFolk; wCof += p.WageCof; gap += p.Gap;
            if (p.Drop < 0) { dropUnknown++; drop += p.Pcs; } else drop += p.Drop;
        }

        // ============================================================ zapis
        private static string D(float v) { return v.ToString("0.####", Inv); }

        /// <summary>Zapis arm_lpw; "" - bez stanu (klucz nie jest pisany).</summary>
        internal static string Export()
        {
            try
            {
                lock (Gate)
                {
                    var sb = new StringBuilder(_byKey.Count * 48 + 16);
                    bool f = true;
                    foreach (var r in _byKey.Values)
                    {
                        if (r.Level <= 1 && r.Paid <= 0 && r.Seen <= 0 && r.Idle <= 0) continue;
                        if (!IdOk(r.Key) || !IdOk(r.Type)) continue;
                        if (f) { sb.Append(Ver).Append('|'); f = false; } else sb.Append(';');
                        sb.Append(r.Key).Append('~').Append(r.Type).Append('~').Append(r.Level).Append('~').Append(r.Paid).Append('~').Append(D(r.U56)).Append('~').Append(D(r.U182))
                          .Append('~').Append(r.Seen).Append('~').Append(r.Idle);
                    }
                    return sb.ToString();
                }
            }
            catch (Exception e) { Stumble("Export", e); return ""; }
        }

        /// <summary>Wczytanie (po Reset z konstruktora): brak klucza albo pusty - stan pusty; zly wpis pominiety i policzony.</summary>
        internal static void Import(string data)
        {
            try
            {
                lock (Gate)
                {
                    _byKey.Clear(); _byShop.Clear(); _loadBad = 0;
                    if (string.IsNullOrEmpty(data)) { _loadNote = "zapis bez arm_lpw (kazdy warsztat poziom 1)"; return; }
                    int bar = data.IndexOf('|');
                    if (bar <= 0 || data.Substring(0, bar) != Ver) { _loadNote = "arm_lpw w nieznanej wersji '" + (bar > 0 ? data.Substring(0, bar) : "") + "' - pominiety (kazdy warsztat poziom 1)"; return; }
                    string body = data.Substring(bar + 1);
                    int n = 0;
                    if (body.Length > 0)
                        foreach (var e in body.Split(';'))
                        {
                            var a = e.Split('~');
                            int level, seen, idle; long paid; float u56, u182;
                            if (a.Length < 8 || !IdOk(a[0]) || !IdOk(a[1])
                                || !int.TryParse(a[2], NumberStyles.Integer, Inv, out level) || !long.TryParse(a[3], NumberStyles.Integer, Inv, out paid)
                                || !float.TryParse(a[4], NumberStyles.Float, Inv, out u56) || !float.TryParse(a[5], NumberStyles.Float, Inv, out u182)
                                || !int.TryParse(a[6], NumberStyles.Integer, Inv, out seen) || !int.TryParse(a[7], NumberStyles.Integer, Inv, out idle)
                                || level < 1 || level > MaxLevel || paid < 0 || seen < 0 || idle < 0 || float.IsNaN(u56) || float.IsNaN(u182) || _byKey.ContainsKey(a[0]))
                            { _loadBad++; continue; }
                            _byKey[a[0]] = new Rec { Key = a[0], Type = a[1], Level = level, Paid = paid, U56 = u56 < 0f ? -1f : Math.Min(1f, u56), U182 = u182 < 0f ? -1f : Math.Min(1f, u182), Seen = seen, Idle = idle };
                            n++;
                        }
                    _loadNote = "wczytano arm_lpw: warsztatow " + n + (_loadBad > 0 ? ", zlych wpisow " + _loadBad : "");
                }
            }
            catch (Exception e) { Stumble("Import", e); }
        }
    }
}
