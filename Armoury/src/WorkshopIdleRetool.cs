using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Workshops;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// 435-L9 - LANCUCH PODAZY (LP), krok 9: PRZEZBROJENIE STOJACEGO WARSZTATU i PRZEJECIE. Klucze Workshop Retool Idle i Workshop Takeover (oba NIE = nic nie jest
    /// mierzone, kupowane ani zmieniane - kod co do znaku jak przed 435-L9). Projekt: PROJEKT-LANCUCH-po-krytyce (11.11), II.8 (ostatnie punkty), II.10, II.13-II.17,
    /// II.19 wiersz L9; warunek "po A2" spelniony - 427 ustalila, ze dochod notabla z aktywow to przelew. Polecenie Jeffa: "warsztaty zmienialy sie i produkcja tak
    /// jak jest zapotrzebowanie na swiecie".
    ///
    /// Regula (warsztat notabla albo lorda w MIESCIE, nieukryty, bez linii broni (WorkshopLaw), nie kopalnia; gracz i jego rod poza regula):
    ///  - wykorzystanie u = cykle udane / (udane + nieudane) doby linii z wsadem (pomiar SupplySignal.CycleEnd, ten sam co L8); pamiec u60 = srednia kroczaca o stalej 60
    ///    dob (doby bez proby cyklu jej nie zmieniaja); STOJACY = co najmniej 60 dob pamieci w obecnym typie i u60 &lt; 0.10 nieprzerwanie od 60 dob;
    ///  - NOWY TYP: typ z linia towarowa z wsadem, ktorej glownego wyrobu brakuje w TYM miescie (N &gt;= 1.3, tablica SupplySignal), caly wsad ktorejs linii w okregu
    ///    (WorkshopInputs.HasInput), typu jeszcze nie ma w miescie (hamulec boomu), nie kopalnia, bez linii broni; najwyzsze N wygrywa;
    ///  - KOSZT jak stanowisko L8: cena = koszt zmiany produkcji nowego typu (GetConvertProductionCost, juz x Workshop Trade Equipment Scale); polowa material z polki
    ///    WLASNEGO miasta (narzedzia, deski, drewno; bez wyrobu nowego typu; zapasy polki i sufit ceny jak roboty gminy) -> kasa miasta, reszta -> rzemieslnicy (kiesy
    ///    ludu; czesc nieprzyjeta zostaje w kasie). PLATNIK: kapital warsztatu (TownFolk.BenchPayRun). Bez materialu (polka ponizej polowy ceny) - bez przezbrojenia;
    ///    sprawdzane PRZED kazdym przelewem;
    ///  - Workshop Retool Idle: placi kapital warsztatu, gdy po zaplacie zostaje w nim co najmniej podloga = max(prog kapitalu modelu, polowa znacznika nowego typu);
    ///  - Workshop Takeover: gdy kapitalu nie starcza - inny notabl TEGO miasta z kiesa przejmuje warsztat: placi sprzedajacemu cene = kapital w kasie warsztatu
    ///    (stojacy warsztat jest wart tyle, ile ma w kasie), wklada do kapitalu tyle, zeby po zaplacie za przezbrojenie zostala podloga, i przezbraja z kapitalu jak wyzej.
    ///    Kupujacemu musi zostac &gt;= 4 500 (dolna granica pasma gry - ponizej gra dosypalaby z niczego albo z kasy miasta); sprzedajacy-notabl przy wylaczonym
    ///    Notable Gold Real nie moze przekroczyc 10 500 (gra zamienilaby nadwyzke na sile - zloto w nicosc). Zmiana wlasciciela akcja gry ApplyByDeath (kapital zostaje,
    ///    bez ceny gry - jak DebtLadder D4); zmiana typu ChangeProductionTypeOfWorkshopAction.Apply(.., ignoreCost: true) - gra ani latka WorkshopTrade.ConvertPostfix
    ///    nie pobieraja drugi raz (ConvertPrefix: ignoreCost = true -&gt; bez kosztu), znacznik kapitalu idzie za nowym typem (SyncInitialCapital, jak dotad);
    ///  - Town.Workshops bez zmian liczby (kontrola w linii). Najwyzej 3 warsztaty na dobe na swiat, 1 na miasto na dobe; po zmianie typu pamiec od zera (60 dob).
    /// Zamkniecie (linia "LP przezbrojenia (435-L9)", kazda kontrola = 0): z kapitalu - (kasa miasta + lud); kupujacy - (sprzedajacy + kapital); sztuki kupione -
    /// zmiana polek; zmiany typu zrobione - zmierzone; Town.Workshops dzis - przed. Zloto tylko miedzy posiadaczami swiata (kiesy, kapital, kasa, lud).
    /// Zapis arm_lpr ("lpr1|warsztat~typ~u60~dob pamieci~dob stojacych;..."); brak klucza = pamiec od zera; zly wpis pominiety i policzony.
    /// Bez Supply Chain Log pomiar i tablica N stoja: nic nie jest zmieniane. Zadnej nowej latki Harmony. Watek glowny; wyjatek per warsztat = potkniecie.
    /// 435-L9-p (recenzja): wspolne liczniki z E3-D3 (WorkshopRetool: limit 3 na dobe na swiat razem, przy wlaczonym E3-D3 przerwa miasta 30 dob i warsztatu 120 dob);
    /// przerwa pomiaru > 2 doby (klucze NIE, Supply Chain Log NIE, stary zapis) - pamiec od nowa; zaplata nieudana albo typ bez zmiany - wklad wraca do kupujacego,
    /// typ nie zmienia sie bez pelnej ceny; przejecie i zmiana typu ksiegowane takze przy wyjatku; wybor typu raz na miasto na dobe, odmowa trwala - ponowna proba za 7 dob.
    /// </summary>
    internal static class WorkshopIdleRetool
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private const string Ver = "lpr2", VerOld = "lpr1";   // 435-L9-p: lpr2 = dzien ostatniego pomiaru + rekordy
        internal const string P9 = "LP przezbrojenia (435-L9): ";
        private const double IdleU = 0.10, NeedN = 1.3;
        private const int IdleDays = 60, PerDay = 3, PurseFloor = 4500, BandHigh = 10500;
        private const int StaleGap = 2, RetryDays = 7;   // 435-L9-p: przerwa pomiaru dluzsza niz 2 doby = pamiec od nowa; odmowa trwala - ponowna proba za 7 dob
        private const string MinesId = "mines";
        private const int WarePerDay = 1;                // 435-N-C: najwyzej 1 przezbrojenie na dobe na swiat uzasadnione TYLKO N wyrobu (fala stojacych rozlozona na tygodnie)

        private sealed class Rec
        {
            public string Key, Type;
            public float U60 = -1f;     // srednia kroczaca dobowego u; -1 = bez pomiaru
            public int Age, Low;        // doby pamieci w obecnym typie; kolejne doby z u60 < 0.10
            public int DDone, DFail;    // cykle tej doby (sesja)
            public int NextTry;         // 435-L9-p: najwczesniejsza doba ponownej proby po odmowie trwalej (sesja, bez zapisu)
        }

        private static readonly object Gate = new object();
        private static readonly Dictionary<string, Rec> _byKey = new Dictionary<string, Rec>(StringComparer.Ordinal);
        private static readonly Dictionary<Workshop, Rec> _byShop = new Dictionary<Workshop, Rec>();
        private static string _loadNote = "nowa kampania (bez zapisu)";
        private static int _loadBad, _st, _stSess, _sessDone, _sessTake;
        private static bool _startLine, _frozenLine;
        private static int _lastRun = -1;   // 435-L9-p: doba ostatniego pomiaru (Run); -1 = brak; w zapisie lpr2
        private static readonly HashSet<string> _err = new HashSet<string>();

        // powody odmowy (linia doby)
        private const int WNoType = 0, WNoShelf = 1, WCapital = 2, WNoBuyer = 3, WSeller = 4, WPay = 5, WLimit = 6, WSiege = 7, WKeyOff = 8, WOther = 9, WShared = 10, WWare = 11, WKinds = 12;
        private static readonly string[] WName =
        {
            "bez typu z N >= 1.3, wsadem w okregu i nowego w miescie", "polka bez polowy ceny w materiale", "kapital za maly, przejecie wylaczone",
            "zaden notabl miasta z kiesa (po zaplacie >= 4500)", "sprzedajacy ponad pasmem 10500 (bez Notable Gold Real)", "zaplata nieudana (polka / sufit ceny / sztuka / droga)",
            "limit (3 na swiat razem z E3-D3, 1 na miasto na dobe)", "miasto oblezone", "kapital starcza, ale Workshop Retool Idle wylaczone (przejecie tylko przy braku kapitalu)",
            "inne (koszt, model, bez wlasciciela, blad)", "przerwa wspolna z E3-D3 (miasto 30 dob, warsztat 120 dob)",
            "limit N wyrobu (435-N-C: 1 przezbrojenie z samego N wyrobu na dobe na swiat)"
        };

        internal static bool RetoolOn { get { var s = Settings.Current; return s != null && s.WorkshopRetoolIdle; } }
        internal static bool TakeoverOn { get { var s = Settings.Current; return s != null && s.WorkshopTakeover; } }
        private static bool AnyOn { get { var s = Settings.Current; return s != null && (s.WorkshopRetoolIdle || s.WorkshopTakeover); } }

        private static void Stumble(string where, Exception e)
        {
            _st++; _stSess++;
            try { if (_err.Add(where)) Log.Error("WorkshopIdleRetool." + where, e); } catch { }
        }

        /// <summary>Nowa kampania / przed wczytaniem (konstruktor ArmouryBehavior): stan i liczniki od zera.</summary>
        internal static void Reset()
        {
            lock (Gate)
            {
                _byKey.Clear(); _byShop.Clear(); _loadNote = "nowa kampania (bez zapisu)"; _loadBad = 0;
                _st = _stSess = _sessDone = _sessTake = 0; _startLine = false; _frozenLine = false; _err.Clear(); _lastRun = -1;
            }
        }

        private static bool IdOk(string s) { return !string.IsNullOrEmpty(s) && s.IndexOf('~') < 0 && s.IndexOf(';') < 0 && s.IndexOf('|') < 0; }
        private static string KeyOf(Workshop w) { var st = w.Settlement; return (st != null ? st.StringId : "?") + ":" + (w.Tag ?? "?"); }
        private static string TypeOf(Workshop w) { return w.WorkshopType != null ? (w.WorkshopType.StringId ?? "?") : "?"; }
        private static string Name(Settlement st) { return st == null ? "?" : (st.Name != null ? st.Name.ToString() : st.StringId); }
        private static string HName(Hero h) { return h == null ? "?" : (h.Name != null ? h.Name.ToString() : h.StringId); }

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

        private static bool PlayerSide(Hero h) { return h == null || h == Hero.MainHero || (h.Clan != null && h.Clan == Clan.PlayerClan); }

        /// <summary>SupplySignal.CycleEnd (linia towarowa z wsadem warsztatu w miescie; tylko przy kluczu Supply Chain Log): wynik cyklu do licznika doby. Oba klucze
        /// L9 wylaczone - nic. Nie rzuca.</summary>
        internal static void Observe(Workshop w, bool ok)
        {
            try
            {
                if (!AnyOn || w == null || PlayerSide(w.Owner)) return;
                lock (Gate)
                {
                    var r = Find(w, true);
                    if (r == null) return;
                    if (ok) r.DDone++; else r.DFail++;
                }
            }
            catch (Exception e) { Stumble("Observe", e); }
        }

        private static bool Paced(WorkshopType.Production p) { return p.Inputs != null && p.Inputs.Count > 0 && p.Outputs != null && p.Outputs.Count > 0 && WorkshopTrade.IsTradeLine(p); }

        private static int ShopCount()
        {
            int n = 0;
            foreach (var t in Town.AllTowns) if (t != null && t.Workshops != null) n += t.Workshops.Length;
            return n;
        }

        private sealed class Cand { public Town T; public Workshop W; public Rec R; }

        // liczniki doby
        private static int _dRet, _dTake, _dTakeNoRet, _dPcs, _dDrop, _dDropUnknown, _dChanged, _dMeasured;
        private static long _dMat, _dWFolk, _dWCof, _dGap, _dPrice, _dInject, _dGapTake;
        // 435-L9-p: zwrot wkladu, zaplacone bez zmiany typu, cena i zaplata zmian zrobionych, odlozone (ponowna proba), pamiec od nowa po przerwie
        private static long _dRefund, _dPaidNoType, _dCostDone, _dPaidDone;
        private static int _dDeferred, _dStale, _dStaleGap;
        // 435-N-C: zmiany typu wedlug galezi wyboru (N towaru / N wyrobu), "bylby" przy kluczu NIE (435-N-C-p: miasta, nie kandydaci)
        private static int _dPickTown, _dPickWare, _dWould;
        private static readonly List<string> _dDone = new List<string>();

        private static void ZeroDay()
        {
            _dRet = _dTake = _dTakeNoRet = _dPcs = _dDrop = _dDropUnknown = _dChanged = _dMeasured = 0;
            _dMat = _dWFolk = _dWCof = _dGap = _dPrice = _dInject = _dGapTake = 0;
            _dRefund = _dPaidNoType = _dCostDone = _dPaidDone = 0; _dDeferred = _dStale = _dStaleGap = 0;
            _dPickTown = _dPickWare = _dWould = 0;
            _dDone.Clear();
        }

        // ============================================================ doba
        /// <summary>ArmouryBehavior.OnDailyTick (zaraz po WorkshopBenches.Daily: po budowach, zleceniach gminy, fundatorach i stanowiskach tej doby - nie zabiera im
        /// materialu; przed migawka MBuild): pamiec u60, przezbrojenia i przejecia, linia "LP przezbrojenia (435-L9)". Oba klucze NIE - nic. Nie rzuca.</summary>
        internal static void Daily()
        {
            var s = Settings.Current;
            if (s == null || Campaign.Current == null) return;
            if (!s.WorkshopRetoolIdle && !s.WorkshopTakeover) { lock (Gate) { foreach (var r in _byKey.Values) r.DDone = r.DFail = 0; } return; }
            long t0 = Stopwatch.GetTimestamp();
            int today = (int)CampaignTime.Now.ToDays;
            lock (Gate)
            {
                try
                {
                    ZeroDay();
                    bool measure = SupplySignal.On;
                    bool canAct = measure && WorkshopTrade.On && TownFolk.DaWorksOn;
                    if (!_startLine)
                    {
                        _startLine = true;
                        Log.Info(P9 + "start | klucze: Workshop Retool Idle " + (s.WorkshopRetoolIdle ? "TAK" : "NIE") + ", Workshop Takeover " + (s.WorkshopTakeover ? "TAK" : "NIE")
                                 + " | regula: warsztat notabla albo lorda w miescie (bez linii broni, nie kopalnia; gracz poza regula) z u60 < 0.10 nieprzerwanie od " + IdleDays
                                 + " dob przezbraja sie na typ, ktorego wyrobu brakuje w jego miescie (N >= 1.3), z wsadem w okregu i jeszcze nieobecny w miescie; cena = koszt zmiany produkcji,"
                                 + " polowa material z polki miasta (narzedzia, deski, drewno) -> kasa miasta, reszta rzemieslnicy -> lud; platnik kapital warsztatu (zostaje podloga);"
                                 + " bez kapitalu inny notabl miasta przejmuje: cena = kapital warsztatu do sprzedajacego, wklad do kapitalu, kiesa kupujacego >= " + PurseFloor
                                 + "; najwyzej " + PerDay + " na dobe | zapis: " + _loadNote + ", rekordow " + _byKey.Count + ", zlych wpisow " + _loadBad
                                 + " | pomiar (Supply Chain Log) " + (measure ? "czynny" : "WYLACZONY - pamiec stoi, bez zmian") + ", warsztaty towarowe (Workshop Trade) " + (WorkshopTrade.On ? "czynne" : "NIECZYNNE - bez zmian")
                                 + ", droga materialu (Ds Works Real Material) " + (TownFolk.DaWorksOn ? "czynna" : "NIECZYNNA - bez zmian")
                                 + ", Notable Gold Real " + (NotableGold.On ? "TAK" : "NIE (sprzedajacy-notabl najwyzej do " + BandHigh + ")") + " | kolejnosc doby: po WorkshopBenches.Daily, przed migawka MBuild.");
                    }
                    if (!measure)
                    {
                        if (!_frozenLine)
                        {
                            _frozenLine = true;
                            Log.Info(P9 + "dzien " + today + " | klucz Supply Chain Log wylaczony - pomiar wykorzystania i tablica N stoja: zadnych przezbrojen ani przejec (linia raz na sesje).");
                        }
                        return;
                    }
                    Run(s, today, canAct, t0);
                }
                catch (Exception e) { Stumble("Daily", e); }
                finally { foreach (var r in _byKey.Values) r.DDone = r.DFail = 0; }
            }
        }

        private static void Run(Settings s, int today, bool canAct, long t0)
        {
            // 435-L9-p: pamiec stala (klucze L9 NIE, Supply Chain Log NIE albo stary zapis) - po przerwie dluzszej niz 2 doby 60 dob liczy sie od nowa
            if (_lastRun >= 0 && today - _lastRun > StaleGap)
            {
                _dStaleGap = today - _lastRun;
                foreach (var r in _byKey.Values) if (r.U60 >= 0f || r.Age > 0 || r.Low > 0) { r.U60 = -1f; r.Age = 0; r.Low = 0; _dStale++; }
                Log.Info(P9 + "dzien " + today + " | przerwa pomiaru " + _dStaleGap + " dob (ostatni pomiar dzien " + _lastRun + ") - pamiec od nowa: rekordow " + _dStale + ".");
            }
            _lastRun = today;
            int shops0 = ShopCount();
            int recs = 0, player = 0, idle = 0, seen = 0;
            double uSum = 0;
            var why = new int[WKinds];
            var cands = new List<Cand>();
            foreach (var t in Town.AllTowns)
            {
                if (t == null || t.Settlement == null || !t.IsTown || t.Workshops == null) continue;
                foreach (var w in t.Workshops)
                {
                    try
                    {
                        if (w == null || w.WorkshopType == null || w.WorkshopType.IsHidden) continue;
                        var r = Find(w, false);
                        if (r == null) continue;
                        recs++;
                        string type = TypeOf(w);
                        if (!string.Equals(r.Type, type, StringComparison.Ordinal)) { r.Type = type; r.U60 = -1f; r.Age = 0; r.Low = 0; }   // zmiana typu (cudza albo nasza) - pamiec od nowa
                        if (PlayerSide(w.Owner)) { player++; continue; }
                        int a = r.DDone + r.DFail;
                        if (a > 0)
                        {
                            float u = r.DDone / (float)a;
                            r.U60 = r.U60 < 0f ? u : r.U60 + (u - r.U60) / IdleDays;
                        }
                        if (r.U60 >= 0f)
                        {
                            if (r.Age < int.MaxValue) r.Age++;
                            if (r.U60 < IdleU) { if (r.Low < int.MaxValue) r.Low++; } else r.Low = 0;
                            seen++; uSum += r.U60;
                        }
                        if (r.Age >= IdleDays && r.Low >= IdleDays)
                        {
                            idle++;
                            if (w.WorkshopType.StringId == MinesId || WorkshopTrade.ProbeHasArmsLine(w.WorkshopType)) continue;   // kopalnia i linie broni poza regula
                            cands.Add(new Cand { T = t, W = w, R = r });
                        }
                    }
                    catch (Exception e) { Stumble("Run(warsztat)", e); }
                }
            }
            _dMeasured = seen;
            cands.Sort((x, y) => y.R.Low.CompareTo(x.R.Low));   // najdluzej stojace najpierw
            int done = 0;
            var townsToday = new HashSet<Town>();
            var model = Campaign.Current.Models != null ? Campaign.Current.Models.WorkshopModel : null;
            var table = SupplySignal.Table;
            var picks = new Dictionary<Town, TownPick>();   // 435-L9-p: wybor typu raz na miasto na dobe
            if (canAct && model != null && table != null)
                foreach (var c in cands)
                {
                    Hero o0 = null; WorkshopType ty0 = null;
                    int res = -2;
                    try
                    {
                        if (c.R.NextTry > today) { _dDeferred++; continue; }   // 435-L9-p: odmowa trwala - ponowna proba za RetryDays dob
                        if (WorkshopRetool.SharedBlocked(c.T, c.W, today)) { why[WShared]++; continue; }
                        if (done + WorkshopRetool.E3Today(today) >= PerDay || townsToday.Contains(c.T)) { why[WLimit]++; continue; }
                        o0 = c.W.Owner; ty0 = c.W.WorkshopType;
                        res = TryOne(s, c, today, model, table, picks);
                    }
                    catch (Exception e) { Stumble("Run(zmiana)", e); }
                    // 435-L9-p: zmiana zmierzona na warsztacie (takze po wyjatku) - limit doby i miasta, wspolny licznik z E3-D3
                    try
                    {
                        if (ty0 == null) continue;
                        bool tyCh = c.W.WorkshopType != ty0, owCh = c.W.Owner != o0;
                        if (tyCh) { _dChanged++; WorkshopRetool.SharedMark(c.T, c.W, today); }
                        if (tyCh)                                                     // 435-N-C: galaz wyboru zmiany (takze po wyjatku) - licznik limitu WarePerDay
                        {
                            TownPick tq;
                            if (picks.TryGetValue(c.T, out tq) && tq.ByWare && c.W.WorkshopType == tq.Pick) _dPickWare++; else _dPickTown++;
                        }
                        if (tyCh || owCh) { done++; townsToday.Add(c.T); }
                        if (res >= 0 && res < WKinds)
                        {
                            why[res]++;
                            if (res != WSiege && res != WLimit && res != WShared && res != WWare && !owCh && !tyCh) c.R.NextTry = today + RetryDays;
                        }
                    }
                    catch (Exception e) { Stumble("Run(po zmianie)", e); }
                }
            int shops1 = ShopCount();
            var sb = new StringBuilder(1200);
            sb.Append(P9).Append("dzien ").Append(today).Append(" | ")
              .Append(!canAct ? "BEZ ZMIAN (warsztaty towarowe albo droga materialu nieczynne)" : table == null ? "BEZ ZMIAN (brak tablicy N - pierwsza doba sesji albo pomiar bez danych)" : "CZYNNE")
              .Append(" | klucze: Workshop Retool Idle ").Append(s.WorkshopRetoolIdle ? "TAK" : "NIE").Append(", Workshop Takeover ").Append(s.WorkshopTakeover ? "TAK" : "NIE")
              .Append(" | warsztatow z pamiecia ").Append(recs).Append(" (rekordow ").Append(_byKey.Count).Append(", gracza poza regula ").Append(player).Append("), u60 srednie ")
              .Append(seen > 0 ? (uSum / seen).ToString("0.00", Inv) : "-").Append(" z ").Append(seen)
              .Append(" | stojacych (u60 < 0.10 od >= ").Append(IdleDays).Append(" dob) ").Append(idle).Append(", kandydatow (bez kopalni i linii broni) ").Append(cands.Count)
              .Append(" | dzis: przezbrojone z kapitalu ").Append(_dRet).Append(", przejete i przezbrojone ").Append(_dTake).Append(", przejete bez przezbrojenia ").Append(_dTakeNoRet)
              .Append(_dDone.Count > 0 ? " [" + string.Join("; ", _dDone.ToArray()) + "]" : "")
              .Append(" | odmowy:");
            bool f = true;
            for (int i = 0; i < WKinds; i++) if (why[i] > 0) { sb.Append(f ? " " : ", ").Append(WName[i]).Append(' ').Append(why[i]); f = false; }
            if (f) sb.Append(" -");
            if (_dDeferred > 0) sb.Append(" | odlozone (ponowna proba za ").Append(RetryDays).Append(" dob) ").Append(_dDeferred);
            if (_dStale > 0) sb.Append(" | pamiec od nowa po przerwie pomiaru ").Append(_dStaleGap).Append(" dob: rekordow ").Append(_dStale);
            sb.Append(" | N wyrobu (435-N-C, klucz Workshops Follow Goods Shortage ").Append(SupplySignal.WareKey ? "TAK" : "NIE").Append("): zmiany typu wybrane przez N towaru ").Append(_dPickTown)
              .Append(", przez N wyrobu ").Append(_dPickWare).Append(" (najwyzej ").Append(WarePerDay).Append(" na dobe), miast z typem z N wyrobu przy kluczu NIE (bylby) ").Append(_dWould)
              .Append(", odmowa limitu N wyrobu ").Append(why[WWare]);
            sb.Append(" | zloto: z kapitalu ").Append(_dMat + _dWFolk + _dWCof).Append(" = material ").Append(_dMat).Append(" (sztuk ").Append(_dPcs).Append(") + rzemieslnicy do ludu ").Append(_dWFolk)
              .Append(" + zatrzymane w kasie miasta ").Append(_dWCof).Append("; kupujacy -> sprzedajacy ").Append(_dPrice).Append(", kupujacy -> kapital warsztatu ").Append(_dInject)
              .Append(", zwrot wkladu (kapital -> kupujacy) ").Append(_dRefund).Append(", zaplacone bez zmiany typu ").Append(_dPaidNoType)
              .Append(" | kontrola zlota (z kapitalu - kasa miasta - wynik Earn) ").Append(_dGap).Append(", (z kiesy kupujacych - kiesy sprzedajacych - kapital) ").Append(_dGapTake)
              .Append(", cena - zaplacone (zmiany zrobione) ").Append(_dCostDone - _dPaidDone)
              .Append(", sztuk (kupione - zmiana polek) ").Append(_dPcs - _dDrop).Append(_dDropUnknown > 0 ? " (polka nie przeczytana " + _dDropUnknown + " razy)" : "")
              .Append(", zmian typu (zrobione - zmierzone) ").Append(_dRet + _dTake - _dChanged)
              .Append(", straznik liczby warsztatow Town.Workshops (dzis - przed; L9 jej nie zmienia) ").Append(shops1 - shops0)
              .Append(" | w sesji: przezbrojen ").Append(_sessDone).Append(", przejec ").Append(_sessTake)
              .Append(" | koszt ").Append(((Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency).ToString("0.00", Inv)).Append(" ms")
              .Append(" | potkniecia ").Append(_st).Append(" (sesja ").Append(_stSess).Append(")");
            _st = 0;
            bool events = _dRet > 0 || _dTake > 0 || _dTakeNoRet > 0 || _dGap != 0 || _dGapTake != 0 || shops1 != shops0 || _dPaidNoType > 0 || _dRefund > 0 || _dStale > 0
                          || _dCostDone != _dPaidDone || _dRet + _dTake != _dChanged;
            if (s.LogEnabled && (LogGate.Lines || events)) Log.Info(sb.ToString());
        }

        private sealed class TownPick { public WorkshopType Pick, Ware; public bool ByWare, Would; }   // 435-N-C-p: Would - miasto juz policzone w "bylby"   // 435-N-C: Ware - typ z N wyrobu (liczony, gdy z N towaru brak); ByWare - Pick z N wyrobu

        /// <summary>435-L9-p (recenzja kosztu doby): nowy typ dla miasta - raz na miasto na dobe (typ obecny kazdego warsztatu miasta jest w `have`, wiec wynik nie zalezy
        /// od warsztatu). Najwyzsze N glownego wyrobu linii towarowej z wsadem w tym miescie, wsad w okregu, typu nie ma jeszcze w miescie.</summary>
        private static TownPick PickFor(Town t, Settlement st, SupplySignal.NTable table, Dictionary<Town, TownPick> cache)
        {
            TownPick tp;
            if (cache.TryGetValue(t, out tp)) return tp;
            tp = new TownPick();
            cache[t] = tp;
            var have = new HashSet<WorkshopType>();
            foreach (var x in t.Workshops) if (x != null && x.WorkshopType != null) have.Add(x.WorkshopType);
            Dictionary<ItemCategory, float> near = null;
            double pickN = 0;
            foreach (var wt in WorkshopType.All)
            {
                if (wt == null || wt.IsHidden || wt.StringId == MinesId || have.Contains(wt) || WorkshopTrade.ProbeHasArmsLine(wt)) continue;
                double nMax = 1.0;
                foreach (var p in wt.Productions)
                {
                    if (!Paced(p)) continue;
                    var main = p.Outputs[0].Item1;
                    if (main == null) continue;
                    double n = SupplySignal.TownN(table, st, main);
                    if (n > nMax) nMax = n;
                }
                if (nMax < NeedN || (tp.Pick != null && nMax <= pickN)) continue;
                if (near == null) near = WorkshopInputs.InputsNear(t);
                if (!WorkshopInputs.HasInput(wt, near)) continue;
                tp.Pick = wt; pickN = nMax;
            }
            if (tp.Pick != null || table.Ware == null) return tp;
            // 435-N-C: bez typu z N towaru (sprawdzony niedobor wsadu ma pierwszenstwo, krytyka 9) - typ, ktorego glownego wyrobu brakuje na targu: bramka N wyrobu
            // (N_w >= 1.5 i U >= wartosc cyklu), ranga (U - S) / wartosc cyklu; te same filtry co wyzej. Klucz NIE - typ tylko liczony ("bylby"), Pick bez zmian.
            double best = double.NegativeInfinity;
            foreach (var wt in WorkshopType.All)
            {
                if (wt == null || wt.IsHidden || wt.StringId == MinesId || have.Contains(wt) || WorkshopTrade.ProbeHasArmsLine(wt)) continue;
                double sc = double.NegativeInfinity, x;
                foreach (var p in wt.Productions)
                {
                    if (!Paced(p)) continue;
                    var main = p.Outputs[0].Item1;
                    if (main == null) continue;
                    if (SupplySignal.WareGate(table, st, main, p.Outputs[0].Item2, false, out x) && x > sc) sc = x;
                }
                if (double.IsNegativeInfinity(sc) || (tp.Ware != null && sc <= best)) continue;
                if (near == null) near = WorkshopInputs.InputsNear(t);
                if (!WorkshopInputs.HasInput(wt, near)) continue;
                tp.Ware = wt; best = sc;
            }
            if (tp.Ware != null && SupplySignal.WareKey) { tp.Pick = tp.Ware; tp.ByWare = true; }
            return tp;
        }

        /// <summary>Jeden kandydat: przezbrojenie z kapitalu albo przejecie i przezbrojenie. Zwraca indeks powodu odmowy albo -1 (zmiana zrobiona). Zmiane (typ, wlasciciel)
        /// liczy Run z warsztatu, takze po wyjatku (435-L9-p).</summary>
        private static int TryOne(Settings s, Cand c, int today, TaleWorlds.CampaignSystem.ComponentInterfaces.WorkshopModel model, SupplySignal.NTable table,
                                  Dictionary<Town, TownPick> picks)
        {
            var t = c.T; var w = c.W; var st = t.Settlement;
            if (st.IsUnderSiege) return WSiege;
            var tpk = PickFor(t, st, table, picks);
            var pick = tpk.Pick;
            if (pick == null) { if (tpk.Ware != null && !tpk.Would) { tpk.Would = true; _dWould++; } return WNoType; }   // 435-N-C(-p): klucz NIE - bylby typ z N wyrobu (raz na miasto)
            if (tpk.ByWare && _dPickWare >= WarePerDay) return WWare;             // 435-N-C: limit przezbrojen z samego N wyrobu
            int cost = Math.Max(0, model.GetConvertProductionCost(pick));
            if (cost < 20) return WOther;
            // materialy: narzedzia, deski, drewno - bez wyrobu nowego typu; polowa ceny musi lezec na polce PRZED kazdym przelewem
            var outs = new HashSet<ItemCategory>();
            foreach (var p in pick.Productions) if (p.Outputs != null) foreach (var o in p.Outputs) if (o.Item1 != null) outs.Add(o.Item1);
            var basis = BuildFunding.DsWorksItems();
            var names = BuildFunding.DsWorksMatNames.Split('/');
            var items = new ItemObject[basis.Length];
            for (int k = 0; k < basis.Length; k++)
            {
                string n = k < names.Length ? names[k] : "";
                var it = basis[k];
                if (it == null || it.ItemCategory == null || outs.Contains(it.ItemCategory) || (n != "tools" && n != "planks" && n != "hardwood")) continue;
                items[k] = it;
            }
            long half = (long)Math.Floor(cost / 2.0);
            if (BuildFunding.DsWorksPeek(st, items) < half) return WNoShelf;
            int lowLimit = 0;
            try { lowLimit = Math.Max(0, model.CapitalLowLimit); } catch { lowLimit = 0; }
            long floor = Math.Max(lowLimit, WorkshopTrade.StartCapitalFor(pick) / 2);
            long capital = w.Capital;
            Hero seller = w.Owner, buyer = null;
            long price = 0, inject = 0;
            if (capital - cost >= floor)
            {
                if (!s.WorkshopRetoolIdle) return WKeyOff;      // kapital jest, ale przezbrojenie z kapitalu wylaczone (przejecie tylko przy braku kapitalu)
            }
            else
            {
                if (!s.WorkshopTakeover) return WCapital;
                if (seller == null) return WOther;              // 435-L9-p: bez wlasciciela cena nie ma odbiorcy (zloto w nicosc) - bez przejecia
                price = Math.Max(0L, capital);
                inject = Math.Max(0L, floor + cost - capital);
                long need = price + inject;
                foreach (var h in st.Notables)
                {
                    if (h == null || !h.IsAlive || !h.IsNotable || h == seller || PlayerSide(h)) continue;
                    if ((long)h.Gold - need < PurseFloor) continue;
                    if (buyer == null || h.Gold > buyer.Gold) buyer = h;
                }
                if (buyer == null) return WNoBuyer;
                if (seller.IsNotable && !NotableGold.On && (long)seller.Gold + price > BandHigh) return WSeller;
            }
            string from = TypeOf(w);
            long capPre = w.Capital;   // 435-L9-p: kapital przed wkladem - przy porazce wraca do kupujacego tylko to, co ponad nim
            // przejecie: cena kupujacy -> sprzedajacy, zmiana wlasciciela (kapital zostaje), wklad kupujacy -> kapital warsztatu
            if (buyer != null)
            {
                long b0 = buyer.Gold, s0 = seller.Gold, c0 = w.Capital;
                bool priceOut = false, owned = false, injected = false;
                // 435-L9-p (recenzja stabilnosci): ksiegowanie w finally - wyjatek (sluchacze OnWorkshopOwnerChanged, np. BK ChangeRelationAction) nie gubi przelewu
                try
                {
                    if (price > 0)
                    {
                        buyer.ChangeHeroGold(-(int)price); CirculationWindows.NoteHeroGold(buyer, -price); priceOut = true;
                        seller.ChangeHeroGold((int)price); CirculationWindows.NoteHeroGold(seller, price);
                    }
                    // gra 1.4.8: ApplyInternal najpierw zmienia wlasciciela, potem zdarzenie - wyjatek sluchacza zostawia warsztat u kupujacego
                    try { ChangeOwnerOfWorkshopAction.ApplyByDeath(w, buyer); }   // kapital = w.Capital, bez ceny gry (DebtLadder D4)
                    catch (Exception e) { Stumble("Przejecie(ApplyByDeath)", e); }
                    owned = w.Owner == buyer;
                    if (!owned)
                    {
                        // wlasciciel bez zmiany - cena wraca do kupujacego
                        if (priceOut)
                        {
                            seller.ChangeHeroGold(-(int)price); CirculationWindows.NoteHeroGold(seller, -price);
                            buyer.ChangeHeroGold((int)price); CirculationWindows.NoteHeroGold(buyer, price);
                            priceOut = false;
                        }
                    }
                    else if (inject > 0)
                    {
                        buyer.ChangeHeroGold(-(int)inject); CirculationWindows.NoteHeroGold(buyer, -inject);
                        w.ChangeGold((int)inject); injected = true;
                        WorkshopTrade.ShiftCapEnd(w, (int)inject);         // wklad wlasciciela - nie wynik warsztatu (linia "Warsztaty towarowe")
                    }
                }
                finally
                {
                    long bOut = b0 - buyer.Gold, sIn = (long)seller.Gold - s0, cIn = (long)w.Capital - c0;
                    _dGapTake += bOut - sIn - cIn;
                    if (priceOut) _dPrice += price;
                    if (injected) _dInject += inject;
                    if (owned) _sessTake++;
                }
                if (!owned) return WOther;
            }
            // zaplata za przezbrojenie z kapitalu warsztatu: material (najwyzej polowa ceny - gorna granica, nie wymog) i rzemieslnicy, potem reszta ceny samymi placami
            var p1 = TownFolk.BenchPayRun(w, st, half, items, 0);
            long paid = 0;
            if (p1.Why != TownFolk.GfOk)
            {
                if (p1.Paid > 0) { Book(p1); paid += p1.Paid; }
                return Failed(c, today, t, w, from, buyer, seller, price, inject, capPre, cost, paid, "zaplata nieudana (powod " + p1.Why + ")", WPay);
            }
            Book(p1); paid += p1.Paid;
            long rest = cost - paid;
            if (rest > 0)
            {
                var p2 = TownFolk.BenchPayRun(w, st, 0, items, rest);
                if (p2.Why == TownFolk.GfOk || p2.Paid > 0) { Book(p2); paid += p2.Paid; }
            }
            // 435-L9-p: bez pelnej ceny typ sie nie zmienia (ignoreCost: gra nie pobiera - "nic z niczego")
            if (paid < cost) return Failed(c, today, t, w, from, buyer, seller, price, inject, capPre, cost, paid, "druga rata nieudana", WPay);
            try { ChangeProductionTypeOfWorkshopAction.Apply(w, pick, true); }   // ignoreCost: koszt zaplacony wyzej (ConvertPrefix bez poboru)
            catch (Exception e) { Stumble("Przezbrojenie(Apply)", e); }        // gra 1.4.8: ChangeWorkshopProduction przed zdarzeniem - typ liczy sie z warsztatu
            if (w.WorkshopType != pick) return Failed(c, today, t, w, from, buyer, seller, price, inject, capPre, cost, paid, "typ bez zmiany po zaplacie", WOther);
            if (buyer != null) _dTake++; else _dRet++;
            _dCostDone += cost; _dPaidDone += paid;
            _sessDone++;
            c.R.Type = TypeOf(w); c.R.U60 = -1f; c.R.Age = 0; c.R.Low = 0;
            Note(today, t, w, from, pick, buyer, seller, price, inject, cost, paid, null);
            return -1;
        }

        /// <summary>435-L9-p: przezbrojenie nie wyszlo. Po przejeciu wklad (to, co w kapitale ponad stan sprzed wkladu) wraca do kiesy kupujacego - kapital nie zostaje
        /// zawyzony (znacznik starego typu, ProfitMade gry). Przejete albo zaplacone - pamiec od nowa (bez drugiej zaplaty i ponownego przejecia przez 60 dob).</summary>
        private static int Failed(Cand c, int today, Town t, Workshop w, string from, Hero buyer, Hero seller, long price, long inject, long capPre, int cost, long paid, string why, int code)
        {
            long back = 0;
            if (buyer != null)
            {
                _dTakeNoRet++;
                if (inject > 0)
                {
                    back = Math.Min(inject, Math.Max(0L, (long)w.Capital - capPre));
                    if (back > 0)
                    {
                        long b0 = buyer.Gold, c0 = w.Capital;
                        try
                        {
                            w.ChangeGold(-(int)back); WorkshopTrade.ShiftCapEnd(w, -(int)back);
                            buyer.ChangeHeroGold((int)back); CirculationWindows.NoteHeroGold(buyer, back);
                        }
                        finally
                        {
                            _dGapTake += (b0 - buyer.Gold) - ((long)w.Capital - c0);   // kapital -> kiesa kupujacego: ma byc 0
                            _dRefund += Math.Max(0L, (long)buyer.Gold - b0);
                        }
                    }
                }
            }
            if (paid > 0 && w.WorkshopType != null && TypeOf(w) == from) _dPaidNoType += paid;
            if (buyer != null || paid > 0)
            {
                c.R.Type = TypeOf(w); c.R.U60 = -1f; c.R.Age = 0; c.R.Low = 0;
                Note(today, t, w, from, null, buyer, seller, price, inject, cost, paid, why + (back > 0 ? ", wklad zwrocony " + back : ""));
            }
            return code;
        }

        private static void Book(TownFolk.BenchPay p)
        {
            _dPcs += p.Pcs; _dMat += p.Mat; _dWFolk += p.WageFolk; _dWCof += p.WageCof; _dGap += p.Gap;
            if (p.Drop < 0) { _dDropUnknown++; _dDrop += p.Pcs; } else _dDrop += p.Drop;
        }

        private static void Note(int today, Town t, Workshop w, string from, WorkshopType to, Hero buyer, Hero seller, long price, long inject, int cost, long paid, string fail)
        {
            try
            {
                string txt = Name(t.Settlement) + " " + from + " -> " + (to != null ? to.StringId : "-") + (buyer != null ? " (przejal " + HName(buyer) + " od " + HName(seller) + " za " + price + " zl, wklad " + inject + ")" : " (z kapitalu)")
                             + ", cena " + cost + ", zaplacone " + paid + (fail != null ? ", " + fail : "");
                if (_dDone.Count < 8) _dDone.Add(txt);
                Log.Info(P9 + "dzien " + today + " | " + txt + " | kapital teraz " + w.Capital + ", znacznik " + w.InitialCapital + ".");
            }
            catch { }
        }

        // ============================================================ zapis
        private static string D(float v) { return v.ToString("0.####", Inv); }

        /// <summary>Zapis arm_lpr; "" - bez stanu (klucz nie jest pisany).</summary>
        internal static string Export()
        {
            try
            {
                lock (Gate)
                {
                    var sb = new StringBuilder(_byKey.Count * 40 + 16);
                    bool f = true;
                    foreach (var r in _byKey.Values)
                    {
                        if (r.Age <= 0 && r.U60 < 0f) continue;
                        if (!IdOk(r.Key) || !IdOk(r.Type)) continue;
                        if (f) { sb.Append(Ver).Append('|').Append(_lastRun.ToString(Inv)).Append('|'); f = false; } else sb.Append(';');   // 435-L9-p: lpr2|dzien ostatniego pomiaru|rekordy
                        sb.Append(r.Key).Append('~').Append(r.Type).Append('~').Append(D(r.U60)).Append('~').Append(r.Age).Append('~').Append(r.Low);
                    }
                    return sb.ToString();
                }
            }
            catch (Exception e) { Stumble("Export", e); return ""; }
        }

        /// <summary>Wczytanie (po Reset z konstruktora): brak klucza albo pusty - pamiec od zera; zly wpis pominiety i policzony.</summary>
        internal static void Import(string data)
        {
            try
            {
                lock (Gate)
                {
                    _byKey.Clear(); _byShop.Clear(); _loadBad = 0; _lastRun = -1;
                    if (string.IsNullOrEmpty(data)) { _loadNote = "zapis bez arm_lpr (pamiec od zera)"; return; }
                    int bar = data.IndexOf('|');
                    string ver = bar > 0 ? data.Substring(0, bar) : "";
                    if (ver != Ver && ver != VerOld) { _loadNote = "arm_lpr w nieznanej wersji '" + ver + "' - pominiety (pamiec od zera)"; return; }
                    string body = data.Substring(bar + 1);
                    if (ver == Ver)
                    {
                        // 435-L9-p: dzien ostatniego pomiaru (przerwa > 2 doby przy pierwszej dobie = pamiec od nowa)
                        int bar2 = body.IndexOf('|'), last;
                        if (bar2 < 0 || !int.TryParse(body.Substring(0, bar2), NumberStyles.Integer, Inv, out last)) { _loadNote = "arm_lpr lpr2 bez dnia pomiaru - pominiety (pamiec od zera)"; return; }
                        _lastRun = last < 0 ? -1 : last;
                        body = body.Substring(bar2 + 1);
                    }
                    else _lastRun = 0;   // lpr1 (bez dnia pomiaru): pierwsza doba uzna przerwe - pamiec od nowa
                    int n = 0;
                    if (body.Length > 0)
                        foreach (var e in body.Split(';'))
                        {
                            var a = e.Split('~');
                            int age, low; float u60;
                            if (a.Length < 5 || !IdOk(a[0]) || !IdOk(a[1])
                                || !float.TryParse(a[2], NumberStyles.Float, Inv, out u60) || !int.TryParse(a[3], NumberStyles.Integer, Inv, out age)
                                || !int.TryParse(a[4], NumberStyles.Integer, Inv, out low)
                                || age < 0 || low < 0 || low > age || float.IsNaN(u60) || _byKey.ContainsKey(a[0]))
                            { _loadBad++; continue; }
                            _byKey[a[0]] = new Rec { Key = a[0], Type = a[1], U60 = u60 < 0f ? -1f : Math.Min(1f, u60), Age = age, Low = low };
                            n++;
                        }
                    _loadNote = "wczytano arm_lpr (" + ver + "): warsztatow " + n + (_loadBad > 0 ? ", zlych wpisow " + _loadBad : "") + (ver == Ver ? ", ostatni pomiar dzien " + _lastRun : ", stara wersja - pamiec od nowa przy pierwszej dobie");
                }
            }
            catch (Exception e) { Stumble("Import", e); }
        }
    }
}
