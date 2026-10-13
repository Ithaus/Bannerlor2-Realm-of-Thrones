using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// GL-0 (fundament projektu "gospodarka od ludzi": docs/gospodarka-od-ludzi/PROJEKT-GOSPODARKA-OD-LUDZI-2026-10-13.md rozdz. 3.2-3.5, 6, paczka P0):
    /// WSPOLNA USLUGA SKAL "LUDZIE OKREGU". Jedno miejsce, z ktorego paczki P1-P4 biora, ilu ludzi stoi za wsia, rynkiem miasta i podzamczem wobec
    /// przecietnej osady swiata. Usluga NICZEGO w grze nie zmienia (zloto, towar, hearth, dobrobyt, ksiega ludzi) - liczy, pamieta i (P0) pisze do logu.
    ///
    /// Trzy skale, liczone raz na dobe na watku glownym (Daily; po wczytaniu takze SessionStart):
    ///  u_v  (wies)   = ludzie okregu wsi / srednia wsi swiata. Ludzie okregu wsi = hearth x k_v (dom - ten sam rachunek co PopulationLaw.PeopleOf)
    ///                  + uchodzcy tej wsi poza domem (PeopleBook.RefugeesOf). Uchodzcy wchodza, bo Devastation.YieldFactor juz zdejmuje brakujace
    ///                  rece ((dom / (dom + uchodzcy)) ^ e) - skala od samego domu liczylaby ubytek drugi raz (projekt 3.2). Granice: dolnej BRAK
    ///                  (wies to symbol - czysta proporcja), gorna VillageMax = 6.0 (bezpiecznik przelicznika; kazda wies ponad nim w linii doby).
    ///  m_t  (miasto) = ludzie rynku / sredni rynek swiata. Ludzie rynku = ludzie miasta (stan ksiegi, PopulationLaw.TownPeople) + ludzie W DOMU wsi
    ///                  przypisanych do tego targu - ta sama definicja co WorkshopLaw.MarketPeople, ale z przypisaniem ZAMROZONYM (nizej). Rynek liczy
    ///                  dom bez uchodzcow: kto uciekl, nie kupuje w tym miescie (projekt rozdz. 6, "region spustoszony"). Granice MarketMin-MarketMax = 0.05-4.0.
    ///  podzamcze     = ludzie w domu wsi zamku (BoundVillages) / srednie podzamcze swiata (srednia po WSZYSTKICH zamkach, takze bez wsi - suma skal
    ///                  podzamczy w dniu kalibracji = liczba zamkow). Granice jak m_t; zamek bez wsi = 0.05 (projekt rozdz. 6).
    /// Odniesienia (srednia wies, sredni rynek, srednie podzamcze) liczone RAZ, przy kalibracji kampanii - pierwszy spis po kalibracji ludnosci
    /// (PopulationLaw.Calibrated; jak WorldPeoplePerHearth) - i trzymane w zapisie (arm_peoplescale, SaveText.Sync, format "1|"). Stary zapis bez pola:
    /// kalibracja przy pierwszym wczytaniu. Odniesienia licza wszystkie osady z przelicznikiem (takze Straz) - definicja nie zalezy od kluczy MCM;
    /// swiat, ktory sie wyludnia, ma mniejsze skale (projekt 3.4 - nic nie wraca sztucznie do sumy).
    ///
    /// WYGLADZENIE m_t (DECYZJA GL-0, pelne uprawnienia): przypisanie wsi do targu ZAMROZONE w dniu kalibracji (id wsi -> id miasta TradeBound tego dnia,
    /// w zapisie). Gra przypisuje wsie ZAMKOW od nowa przy kazdej wojnie, pokoju, zmianie pana i krolestwa rodu (VillageTradeBoundCampaignBehavior:
    /// najblizsze miasto wlasnej frakcji w zasiegu, inaczej najblizsze niewrogie, inaczej BRAK targu; wsie miast sa przypiete do swojego miasta na
    /// stale - Village.TradeBound) - rynek idacy za tym skakalby z polityka o ludzi calych okregow zamkow. Okreg = geografia dnia startu; ruch ludzi
    /// (przyrost, glod, spustoszenie, pobor) przechodzi dalej co dobe. Wies bez targu w dniu kalibracji dostaje pierwszy targ, jaki da jej gra
    /// (i od tej chwili ma go zamrozony). Srednia 28 dob odrzucona: w biegu rocznym 311 z 318 rodow bylo w wojnie - srednia tez wedrowalaby za polityka,
    /// tylko wolniej; po wczytaniu startowalaby od stanu dnia (inny bieg po zapisie i wczytaniu) albo musialaby trzymac w zapisie 28 x 97 liczb.
    /// Ile razy wies zmienia targ gry - linia doby (pomiar P0 pkt d).
    ///
    /// WYJATEK STRAZY (klucz WatchKeepsFlatOutput, decyzja Jeffa 13.10 pyt. 3): osady krainy "nightswatch" z dnia kalibracji (Dar: wsie, Castle Black,
    /// zamki Strazy) sa poza regula - Try* zwraca false, *Scale zwraca 1 (Dar zostaje przy dzisiejszej stalej produkcji, rynek Castle Black m_t = 1).
    ///
    /// Pamiec doby: jedna referencja (_mem) podmieniana raz na dobe; slowniki w niej nigdy nie sa zmieniane po publikacji - odczyt z watkow AI bez
    /// zamka (postfiksy produkcji bywaja wolane z watkow AI). Zadnego liczenia ksiegi w odczycie. Wyjatek na jednej osadzie = potkniecie (licznik w linii
    /// doby), osada bez wpisu (skala neutralna 1), reszta swiata liczy sie dalej - zadnego globalnego wylacznika.
    /// </summary>
    internal static class PeopleScale
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>u_v gorna (projekt 3.5): najwieksza wies biegu rocznego 4.18.</summary>
        internal const float VillageMax = 6f;
        /// <summary>m_t i podzamcze (projekt 3.5): miasto musi dzialac jako rynek (dol), bezpiecznik przelicznika (gora; najwiekszy rynek ok. 2.4-3).</summary>
        internal const float MarketMin = 0.05f, MarketMax = 4f;

        private const string Version = "1";
        private const string WatchRealm = "nightswatch";
        private const string CsvName = "skale-ludzi.csv";
        private const string CsvHeader = "dzien;osada_id;osada;rodzaj;krolestwo;kultura_start;ludzie;skala;skala_surowa;dom;uchodzcy;targ_zamrozony;targ_gry;uwagi";

        // ------------------------------------------------------------ stan kampanii (zapis: arm_peoplescale)
        private static bool _cal;                                   // odniesienia skalibrowane
        private static double _refV, _refM, _refC;                  // srednia wies, sredni rynek, srednie podzamcze (ludzie)
        private static int _calDay = -1, _calV, _calT, _calC;       // dzien kalibracji, ile wsi / miast / zamkow weszlo do srednich
        private static string _calHow = "";
        private static readonly Dictionary<string, string> _bound = new Dictionary<string, string>();   // id wsi -> id miasta (targ zamrozony)
        private static string _loadNote;                            // null = nie wczytywano (nowa kampania); inaczej skad odniesienia po wczytaniu

        // ------------------------------------------------------------ pamiec sesji (bez zapisu)
        private static readonly Dictionary<string, string> _gameBound = new Dictionary<string, string>();   // id wsi -> targ gry z poprzedniego spisu
        private static int _movesSession;                           // zmiany targu gry od startu sesji
        private static int _stumbles;                               // potkniecia spisu i raportu od ostatniej linii (watek glowny)
        private static int _readStumbles;                           // potkniecia odczytow (watki AI - Interlocked)
        private static readonly HashSet<string> _err = new HashSet<string>();
        private static bool _waitLogged, _calFailLogged, _weekDone;
        private static Census _last;                                // ostatni spis (do linii doby)

        private sealed class Entry
        {
            public float Scale;     // skala po granicach - to oddaja Try*
            public float Raw;       // ludzie / odniesienie, bez granic
            public float People;    // wies: dom + uchodzcy; miasto: ludzie rynku; zamek: ludzie w domu wsi zamku
            public bool Watch;      // osada krainy Strazy (z dnia kalibracji)
        }

        private sealed class Memory
        {
            public int Day;
            public Dictionary<Settlement, Entry> Map;
        }

        private static volatile Memory _mem;

        private sealed class Row
        {
            public Settlement S;
            public char Kind;                   // 'w' wies, 'm' miasto, 'z' zamek
            public bool HasK;                   // wies z przelicznikiem ludzi (k_v > 0)
            public double People;               // jak Entry.People
            public double Home, Away;           // wies: dom i uchodzcy; miasto: ludzie wlasni (Home)
            public bool Watch;
            public string Realm;                // kraina z dnia kalibracji
            public string Frozen, Game;         // wies: targ zamrozony i targ gry dzis (id miasta albo null)
        }

        private sealed class Census
        {
            public readonly List<Row> Rows = new List<Row>();
            public int NoK, NoMarket, Differ, Moved, Late;
            public double Home, Away, TownOwn, InMarkets;
        }

        internal static void Reset()
        {
            _mem = null; _cal = false; _refV = _refM = _refC = 0.0; _calDay = -1; _calV = _calT = _calC = 0; _calHow = "";
            _bound.Clear(); _loadNote = null;
            _gameBound.Clear(); _movesSession = 0; _stumbles = 0; _readStumbles = 0; _err.Clear();
            _waitLogged = false; _calFailLogged = false; _weekDone = false; _last = null;
        }

        private static bool DryRun { get { var s = Settings.Current; return s != null && s.PeopleScaleDryRun; } }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;                                                            // liczymy potkniecia, niczego nie gasimy
            try { if (_err.Add(where)) { if (e != null) Log.Error("PeopleScale." + where, e); else Log.Info("PeopleScale: potkniecie - " + where + "."); } } catch { }
        }

        private static int Today() { return (int)CampaignTime.Now.ToDays; }

        // ============================================================ API dla paczek (odczyt z dowolnego watku, bez zamka)

        /// <summary>Gotowe: odniesienia skalibrowane i pamiec doby zbudowana. Przed tym kazde Try* zwraca false, a *Scale - 1.</summary>
        internal static bool Ready { get { return _mem != null; } }

        /// <summary>Dzien gry, z ktorego jest pamiec skal (-1 = brak).</summary>
        internal static int MemoryDay { get { var m = _mem; return m != null ? m.Day : -1; } }

        /// <summary>GL-1 (tylko odczyt, watek glowny): srednia wies swiata z kalibracji (ludzie; 0 = nieskalibrowane). P1 trzyma ja obok g typow - inna liczba
        /// po wczytaniu = usluga skalibrowala sie od nowa (brak albo blad pola arm_peoplescale), wiec g typow trzeba policzyc od nowa.</summary>
        internal static double RefVillage { get { return _cal ? _refV : 0.0; } }

        /// <summary>Klucz WatchKeepsFlatOutput: Nocna Straz poza regula (decyzja Jeffa 13.10, pyt. 3).</summary>
        internal static bool WatchExceptionOn { get { var s = Settings.Current; return s != null && s.WatchKeepsFlatOutput; } }

        /// <summary>
        /// Osada objeta wyjatkiem Strazy TERAZ: kraina z dnia kalibracji "nightswatch" (Dar, Castle Black, zamki Strazy) i klucz wlaczony. Paczka, ktora
        /// liczy wiecej niz sam mnoznik (np. P1 z g typu i zdjeciem progu hearth), ma przy true zostawic dzisiejszy wzor gry w calosci. Osada spoza
        /// pamieci doby (przed pierwsza doba, wies bez przelicznika) - false.
        /// </summary>
        internal static bool WatchException(Settlement s)
        {
            if (s == null || !WatchExceptionOn) return false;
            var m = _mem;
            Entry e;
            return m != null && m.Map.TryGetValue(s, out e) && e.Watch;
        }

        /// <summary>
        /// Skala wsi u_v = ludzie okregu wsi (dom + uchodzcy) / srednia wsi swiata z kalibracji; granice: bez dolnej, gora 6.0. false (u = 1) = regula
        /// tej wsi nie dotyczy i wolajacy zostaje przy dzisiejszym wzorze gry: usluga nie gotowa, wies bez przelicznika ludzi (k_v = 0), potkniecie
        /// przy spisie albo wyjatek Strazy. Nie rzuca.
        /// </summary>
        internal static bool TryVillage(Village v, out float u)
        {
            u = 1f;
            try
            {
                var m = _mem;
                if (m == null || v == null) return false;
                var s = v.Settlement;
                Entry e;
                if (s == null || !m.Map.TryGetValue(s, out e)) return false;
                if (e.Watch && WatchExceptionOn) return false;
                u = e.Scale;
                return true;
            }
            catch (Exception ex) { ReadStumble(ex); u = 1f; return false; }
        }

        /// <summary>
        /// Skala rynku miasta m_t = ludzie rynku (miasto + ludzie w domu wsi przypisanych do targu przy kalibracji) / sredni rynek swiata z kalibracji;
        /// granice 0.05-4.0. false (m = 1) = usluga nie gotowa, potkniecie przy spisie albo wyjatek Strazy (Castle Black). Nie rzuca.
        /// </summary>
        internal static bool TryMarket(Town t, out float m)
        {
            m = 1f;
            try
            {
                var mem = _mem;
                if (mem == null || t == null) return false;
                var s = t.Settlement;
                Entry e;
                if (s == null || !s.IsTown || !mem.Map.TryGetValue(s, out e)) return false;
                if (e.Watch && WatchExceptionOn) return false;
                m = e.Scale;
                return true;
            }
            catch (Exception ex) { ReadStumble(ex); m = 1f; return false; }
        }

        /// <summary>
        /// Skala podzamcza zamku = ludzie w domu wsi zamku / srednie podzamcze swiata z kalibracji; granice 0.05-4.0 (zamek bez wsi 0.05). false (w = 1)
        /// = to nie zamek, usluga nie gotowa, potkniecie albo wyjatek Strazy. Nie rzuca.
        /// </summary>
        internal static bool TryCastleWard(Settlement castle, out float w)
        {
            w = 1f;
            try
            {
                var m = _mem;
                Entry e;
                if (m == null || castle == null || !castle.IsCastle || !m.Map.TryGetValue(castle, out e)) return false;
                if (e.Watch && WatchExceptionOn) return false;
                w = e.Scale;
                return true;
            }
            catch (Exception ex) { ReadStumble(ex); w = 1f; return false; }
        }

        /// <summary>u_v albo 1 (patrz TryVillage).</summary>
        internal static float VillageScale(Village v) { float u; return TryVillage(v, out u) ? u : 1f; }

        /// <summary>m_t albo 1 (patrz TryMarket).</summary>
        internal static float MarketScale(Town t) { float m; return TryMarket(t, out m) ? m : 1f; }

        /// <summary>Skala podzamcza albo 1 (patrz TryCastleWard).</summary>
        internal static float CastleWardScale(Settlement castle) { float w; return TryCastleWard(castle, out w) ? w : 1f; }

        /// <summary>
        /// Ludzie, od ktorych liczy sie skala osady w pamieci doby: wies - dom + uchodzcy, miasto - ludzie rynku, zamek - ludzie w domu wsi zamku
        /// (np. P2: glowy kiesy = stala symbolu x ludzie rynku). false = osady nie ma w pamieci. Wyjatku Strazy nie sprawdza. Nie rzuca.
        /// </summary>
        internal static bool TryPeople(Settlement s, out float people)
        {
            people = 0f;
            try
            {
                var m = _mem;
                Entry e;
                if (m == null || s == null || !m.Map.TryGetValue(s, out e)) return false;
                people = e.People;
                return true;
            }
            catch (Exception ex) { ReadStumble(ex); people = 0f; return false; }
        }

        private static void ReadStumble(Exception e)
        {
            // pierwszy wyjatek odczytu w dobie do logu (Log ma wlasny zamek), kolejne tylko w liczniku
            if (Interlocked.Increment(ref _readStumbles) == 1) { try { Log.Error("PeopleScale.Odczyt", e); } catch { } }
        }

        // ============================================================ doba i start sesji (watek glowny)

        /// <summary>Z OnDailyTickBody, po zamknieciu doby ksiegi ludzi (OthersRise.Daily) i praw Strazy i niewolnych: spis, pamiec doby, linia P0.</summary>
        internal static void Daily()
        {
            Refresh("doba");
            try { if (DryRun) Report(); } catch (Exception e) { Stumble("Raport", e); }
        }

        /// <summary>Z OnSessionLaunched (po McmSettings.Apply i rozwiazaniu ksiag): pamiec doby od pierwszej godziny sesji - po wczytaniu paczki nie czekaja
        /// na pierwsza dobe. W nowej kampanii przed kalibracja ludnosci nic nie robi (pierwsza doba).</summary>
        internal static void SessionStart()
        {
            Refresh("start sesji");
            try { if (DryRun) StartLine(); } catch (Exception e) { Stumble("Raport(start)", e); }
        }

        private static void Refresh(string why)
        {
            try
            {
                if (!PopulationLaw.Calibrated)
                {
                    // ludnosci sami nie kalibrujemy (jak ksiega "Ludzie:") - czekamy na PopulationLaw
                    if (!_waitLogged) { _waitLogged = true; Log.Info("Skale ludzi (GL0): czekam na kalibracje ludnosci krain (" + why + ") - do tej chwili paczki licza po staremu (skala 1)."); }
                    return;
                }
                var c = TakeCensus();
                if (!_cal && !Calibrate(c)) return;
                int day = Today();
                var map = new Dictionary<Settlement, Entry>(c.Rows.Count);
                foreach (var r in c.Rows)
                {
                    try
                    {
                        if (r.Kind == 'w' && !r.HasK) continue;                             // wies bez przelicznika: poza pamiecia (skala neutralna)
                        double rf = r.Kind == 'w' ? _refV : (r.Kind == 'm' ? _refM : _refC);
                        if (!(rf > 0.0)) continue;                                          // brak odniesienia (np. swiat bez wsi zamkow) - skala neutralna
                        double raw = r.People / rf;
                        if (double.IsNaN(raw) || double.IsInfinity(raw) || raw < 0.0) { Stumble("Skala(" + (r.S != null ? r.S.StringId : "?") + ")", null); continue; }
                        float sc = r.Kind == 'w' ? (float)Math.Min(raw, VillageMax) : (float)Math.Max(MarketMin, Math.Min(MarketMax, raw));
                        map[r.S] = new Entry { Scale = sc, Raw = (float)raw, People = (float)r.People, Watch = r.Watch };
                    }
                    catch (Exception e) { Stumble("Pamiec(osada)", e); }
                }
                _mem = new Memory { Day = day, Map = map };        // jedna referencja na dobe
                _last = c;
            }
            catch (Exception e) { Stumble("Refresh", e); }          // pamiec wczorajsza zostaje
        }

        // kraina z dnia kalibracji bez zadnej pamieci podrecznej (FreeHands.RealmOf zapamietuje - tu nie wolno nic zmieniac w cudzym stanie)
        private static string RealmOf(Settlement s)
        {
            if (s == null) return "?";
            if (s.IsVillage || s.IsTown) return PeopleBook.CultureAtCalibration(s);
            var bv = s.BoundVillages;
            if (bv != null && bv.Count > 0 && bv[0] != null && bv[0].Settlement != null) return PeopleBook.CultureAtCalibration(bv[0].Settlement);
            return s.Culture != null ? s.Culture.StringId : "?";
        }

        // ludzie wlasni miasta bez zakladania stanu (PopulationLaw.TownPeople zaklada stan przy pierwszym pytaniu - tu tylko odczyt, ta sama liczba)
        private static double TownOwn(Settlement s)
        {
            float k = PopulationLaw.KFor(s);
            if (!(k > 0f) || float.IsInfinity(k)) return 0.0;
            float p;
            if (PopulationLaw.TownsFrozen && PeopleBook.TryTown(s, out p)) return Math.Max(0f, p);
            double live = (double)Math.Max(0f, s.Town.Prosperity) * k;
            return double.IsNaN(live) || double.IsInfinity(live) ? 0.0 : live;
        }

        private static bool SafeId(string id) { return !string.IsNullOrEmpty(id) && id.IndexOfAny(new[] { '|', ';', '=' }) < 0; }

        private static Census TakeCensus()
        {
            var c = new Census();
            var towns = new Dictionary<string, Row>();
            var castles = new Dictionary<Settlement, Row>();
            // 1. warownie: miasta (ludzie wlasni) i zamki
            foreach (var s in Settlement.All)
            {
                if (s == null) continue;
                try
                {
                    if (s.IsTown && s.Town != null)
                    {
                        var r = new Row { S = s, Kind = 'm', Realm = RealmOf(s) };
                        r.Watch = r.Realm == WatchRealm;
                        r.Home = TownOwn(s); r.People = r.Home; c.TownOwn += r.Home; c.InMarkets += r.Home;
                        c.Rows.Add(r);
                        if (s.StringId != null) towns[s.StringId] = r;
                    }
                    else if (s.IsCastle)
                    {
                        var r = new Row { S = s, Kind = 'z', Realm = RealmOf(s) };
                        r.Watch = r.Realm == WatchRealm;
                        c.Rows.Add(r);
                        castles[s] = r;
                    }
                }
                catch (Exception e) { Stumble("Spis(warownia)", e); }
            }
            // 2. wsie: ludzie okregu, targ zamrozony (rynek), wies zamku (podzamcze)
            foreach (var s in Settlement.All)
            {
                if (s == null || !s.IsVillage || s.Village == null) continue;
                try
                {
                    var v = s.Village;
                    var r = new Row { S = s, Kind = 'w', Realm = PeopleBook.CultureAtCalibration(s) };
                    r.Watch = r.Realm == WatchRealm;
                    float k = PopulationLaw.KFor(s);
                    r.HasK = k > 0f && !float.IsInfinity(k);
                    if (r.HasK)
                    {
                        r.Home = (double)Math.Max(0f, v.Hearth) * k;
                        r.Away = Math.Max(0.0, PeopleBook.RefugeesOf(v));
                        r.People = r.Home + r.Away;
                        if (double.IsNaN(r.People) || double.IsInfinity(r.People))
                        {
                            r.HasK = false; r.People = r.Home = r.Away = 0.0;
                            Stumble("Spis(liczba ludzi " + s.StringId + ")", null);
                        }
                    }
                    if (!r.HasK) c.NoK++;
                    c.Home += r.Home; c.Away += r.Away;
                    // targ gry dzis i targ zamrozony
                    string game = null;
                    var tb = v.TradeBound;
                    if (tb != null && tb.IsTown && SafeId(tb.StringId)) game = tb.StringId;
                    r.Game = game;
                    string id = s.StringId, fr = null;
                    if (SafeId(id))
                    {
                        if (!_bound.TryGetValue(id, out fr) && game != null)
                        {
                            _bound[id] = game; fr = game;              // przy kalibracji - przypisanie dnia; pozniej - pierwszy targ wsi bez targu
                            if (_cal) c.Late++;
                        }
                        string was;
                        if (_gameBound.TryGetValue(id, out was) && was != game) { c.Moved++; _movesSession++; }
                        _gameBound[id] = game;
                    }
                    r.Frozen = fr;
                    if (fr != game) c.Differ++;
                    Row t;
                    if (fr != null && towns.TryGetValue(fr, out t)) { t.People += r.Home; c.InMarkets += r.Home; }
                    else c.NoMarket++;
                    Row z;
                    var b = v.Bound;
                    if (b != null && b.IsCastle && castles.TryGetValue(b, out z)) z.People += r.Home;
                    c.Rows.Add(r);
                }
                catch (Exception e) { Stumble("Spis(wies)", e); }
            }
            return c;
        }

        private static bool Calibrate(Census c)
        {
            double sv = 0.0, sm = 0.0, sc = 0.0;
            int nv = 0, nm = 0, nc = 0;
            foreach (var r in c.Rows)
            {
                if (r.Kind == 'w') { if (r.HasK) { sv += r.People; nv++; } }
                else if (r.Kind == 'm') { sm += r.People; nm++; }
                else { sc += r.People; nc++; }
            }
            double rv = nv > 0 ? sv / nv : 0.0, rm = nm > 0 ? sm / nm : 0.0, rc = nc > 0 ? sc / nc : 0.0;
            if (!(rv > 0.0) || !(rm > 0.0) || double.IsInfinity(rv) || double.IsInfinity(rm) || double.IsNaN(rc) || double.IsInfinity(rc))
            {
                if (!_calFailLogged) { _calFailLogged = true; Log.Info("Skale ludzi (GL0): kalibracja odlozona - brak ludzi wsi albo rynkow (wsi z przelicznikiem " + nv + ", miast " + nm + "); probuje co dobe."); }
                _bound.Clear();         // przypisanie do targu zalozy sie od nowa razem z udana kalibracja
                return false;
            }
            _refV = rv; _refM = rm; _refC = rc;
            _calDay = Today(); _calV = nv; _calT = nm; _calC = nc;
            _calHow = _loadNote == null ? "nowa kampania" : _loadNote;
            _cal = true;
            Log.Info("Skale ludzi (GL0): kalibracja (" + _calHow + ", dzien " + _calDay + ") - srednia wies " + rv.ToString("0", Inv) + " ludzi (wsi z przelicznikiem " + nv
                     + ", dom + uchodzcy), sredni rynek " + rm.ToString("0", Inv) + " (miast " + nm + ", miasto + ludzie w domu wsi jego targu), srednie podzamcze "
                     + rc.ToString("0", Inv) + " (zamkow " + nc + "); targ zamrozony dla " + _bound.Count + " wsi (wsi bez targu gry: " + c.NoMarket
                     + " - dostana pierwszy targ, jaki da im gra); odniesienia w zapisie (arm_peoplescale).");
            return true;
        }

        // ============================================================ linie P0 (tylko log, klucz PeopleScaleDryRun)

        private static string F(double x, string fmt) { return double.IsNaN(x) ? "-" : x.ToString(fmt, Inv); }

        private static string Spread(List<float> l, string fmt, bool p10p90)
        {
            if (l.Count == 0) return "-";
            l.Sort();
            Func<double, float> q = p => l[(int)Math.Round(p * (l.Count - 1))];
            return p10p90
                ? F(l[0], fmt) + "/" + F(q(0.1), fmt) + "/" + F(q(0.5), fmt) + "/" + F(q(0.9), fmt) + "/" + F(l[l.Count - 1], fmt)
                : F(l[0], fmt) + "/" + F(q(0.5), fmt) + "/" + F(l[l.Count - 1], fmt);
        }

        private static string Name(Settlement s) { try { return s != null && s.Name != null ? s.Name.ToString() : "?"; } catch { return "?"; } }

        private static string Clean(string s) { return string.IsNullOrEmpty(s) ? "" : s.Replace(';', ',').Replace('\n', ' ').Replace('\r', ' '); }

        private static void StartLine()
        {
            var m = _mem;
            if (m == null || !_cal) return;
            int nv = 0, nt = 0, nc = 0;
            foreach (var kv in m.Map) { if (kv.Key.IsVillage) nv++; else if (kv.Key.IsTown) nt++; else nc++; }
            Log.Info("Skale ludzi (GL0): start sesji - pamiec doby " + m.Day + " (wsi " + nv + ", miast " + nt + ", zamkow " + nc + "); odniesienia (" + _calHow
                     + ", kalibracja dnia " + _calDay + "): wies " + _refV.ToString("0", Inv) + ", rynek " + _refM.ToString("0", Inv) + ", podzamcze " + _refC.ToString("0", Inv)
                     + "; targ zamrozony " + _bound.Count + " wsi; wyjatek Strazy (WatchKeepsFlatOutput) " + (WatchExceptionOn ? "tak" : "nie") + ".");
        }

        private static void Report()
        {
            var m = _mem; var c = _last;
            int day = Today();
            bool watchOn = WatchExceptionOn;
            int readSt = Interlocked.Exchange(ref _readStumbles, 0);
            if (m == null || c == null || !_cal)
            {
                Log.Info("Skale ludzi (GL0): dzien " + day + " - brak pamieci skal (" + (PopulationLaw.Calibrated ? "kalibracja odlozona" : "ludnosc nieskalibrowana")
                         + "), potkniecia " + (_stumbles + readSt) + ".");
                _stumbles = 0;
                return;
            }
            var uv = new List<float>(); var mt = new List<float>(); var wd = new List<float>();
            int over = 0, mLo = 0, mHi = 0, wLo = 0, wHi = 0, wVill = 0, wTown = 0, wCast = 0;
            double wRawV = 0.0;
            var overNames = new List<string>(); var watchTowns = new List<string>();
            foreach (var r in c.Rows)
            {
                Entry e;
                if (r.S == null || !m.Map.TryGetValue(r.S, out e)) continue;
                bool exc = e.Watch && watchOn;
                if (r.Kind == 'w')
                {
                    if (e.Raw > VillageMax) { over++; if (overNames.Count < 5) overNames.Add(Name(r.S) + " " + F(e.Raw, "0.00")); }
                    if (exc) { wVill++; wRawV += e.Raw; continue; }
                    uv.Add(e.Scale);
                }
                else if (r.Kind == 'm')
                {
                    if (exc) { wTown++; watchTowns.Add(Name(r.S) + " " + F(e.Scale, "0.00")); continue; }
                    if (e.Raw < MarketMin) mLo++; else if (e.Raw > MarketMax) mHi++;
                    mt.Add(e.Scale);
                }
                else
                {
                    if (exc) { wCast++; continue; }
                    if (e.Raw < MarketMin) wLo++; else if (e.Raw > MarketMax) wHi++;
                    wd.Add(e.Scale);
                }
            }
            var sb = new StringBuilder(900);
            sb.Append("Skale ludzi (GL0): dzien ").Append(day)
              .Append(" - wsi ").Append(uv.Count).Append(", u_v min/p10/med/p90/max ").Append(Spread(uv, "0.000", true))
              .Append(", miast ").Append(mt.Count).Append(", m_t min/p10/med/p90/max ").Append(Spread(mt, "0.00", true))
              .Append(", odniesienia: wies ").Append(_refV.ToString("0", Inv)).Append(", rynek ").Append(_refM.ToString("0", Inv))
              .Append(", potkniecia ").Append(_stumbles + readSt);
            sb.Append(" | podzamcza ").Append(wd.Count).Append(" min/med/max ").Append(Spread(wd, "0.00", false)).Append(", odniesienie ").Append(_refC.ToString("0", Inv))
              .Append("; wsi bez przelicznika ").Append(c.NoK)
              .Append("; u_v ponad ").Append(VillageMax.ToString("0.0", Inv)).Append(": ").Append(over);
            if (overNames.Count > 0) sb.Append(" [").Append(string.Join(", ", overNames.ToArray())).Append(']');
            sb.Append("; m_t na granicy ").Append(MarketMin.ToString("0.00", Inv)).Append("/").Append(MarketMax.ToString("0.0", Inv)).Append(": ").Append(mLo).Append('/').Append(mHi)
              .Append(", podzamcza: ").Append(wLo).Append('/').Append(wHi);
            if (watchOn)
                sb.Append("; Straz poza regula (WatchKeepsFlatOutput): wsi ").Append(wVill).Append(" skala 1 (bez wyjatku u_v sr. ")
                  .Append(wVill > 0 ? F(wRawV / wVill, "0.000") : "-").Append("), miasta ").Append(wTown).Append(" skala 1")
                  .Append(watchTowns.Count > 0 ? " (bez wyjatku " + string.Join(", ", watchTowns.ToArray()) + ")" : "").Append(", zamkow ").Append(wCast);
            else sb.Append("; Straz bez wyjatku (WatchKeepsFlatOutput wylaczone)");
            sb.Append("; targ zamrozony (dzien ").Append(_calDay).Append("): wsi ").Append(_bound.Count).Append(", z innym targiem gry dzis ").Append(c.Differ)
              .Append(", zmiany targu gry od poprzedniego spisu ").Append(c.Moved).Append(" (sesja ").Append(_movesSession).Append("), wsi bez targu ").Append(c.NoMarket);
            if (c.Late > 0) sb.Append(", przypisane dzis pierwszy raz ").Append(c.Late);
            sb.Append("; ludzie: wsi w domu ").Append(F(c.Home / 1e6, "0.000")).Append(" mln + uchodzcy ").Append(F(c.Away / 1e6, "0.000"))
              .Append(" mln, miast ").Append(F(c.TownOwn / 1e6, "0.000")).Append(" mln, w rynkach ").Append(F(c.InMarkets / 1e6, "0.000")).Append(" mln");
            sb.Append("; kalibracja: ").Append(_calHow).Append('.');
            Log.Info(sb.ToString());
            _stumbles = 0;
            bool week = day % 7 == 0 || !_weekDone;
            if (!week) return;
            _weekDone = true;
            try { KingdomLine(day, m, c, watchOn); } catch (Exception e) { Stumble("Raport(krolestwa)", e); }
            try { CsvRows(day, m, c, watchOn); } catch (Exception e) { Stumble("Raport(csv)", e); }
        }

        private sealed class KAcc { public string Name; public double People; public int V, T, C; public double U, M, W; }

        private static void KingdomLine(int day, Memory m, Census c, bool watchOn)
        {
            var acc = new Dictionary<string, KAcc>();
            foreach (var r in c.Rows)
            {
                try
                {
                    var f = r.S != null ? r.S.MapFaction : null;
                    string id = f != null && f.StringId != null ? f.StringId : "-";
                    KAcc k;
                    if (!acc.TryGetValue(id, out k)) { k = new KAcc { Name = f != null && f.Name != null ? f.Name.ToString() : "bez krolestwa" }; acc[id] = k; }
                    if (r.Kind == 'w' || r.Kind == 'm') k.People += r.Home;        // ludzie ksiegi w domu: wsie i ludzie wlasni miast
                    Entry e;
                    if (!m.Map.TryGetValue(r.S, out e) || (e.Watch && watchOn)) continue;
                    if (r.Kind == 'w') { k.V++; k.U += e.Scale; }
                    else if (r.Kind == 'm') { k.T++; k.M += e.Scale; }
                    else { k.C++; k.W += e.Scale; }
                }
                catch (Exception e) { Stumble("Raport(krolestwo)", e); }
            }
            var list = new List<KAcc>(acc.Values);
            list.Sort((a, b) => b.People.CompareTo(a.People));
            var sb = new StringBuilder(2500);
            sb.Append("Skale ludzi wedlug krolestw (GL0): dzien ").Append(day).Append(" - ");
            for (int i = 0; i < list.Count; i++)
            {
                var k = list[i];
                if (i > 0) sb.Append(" | ");
                sb.Append(k.Name).Append(": ludzie ").Append((k.People / 1000.0).ToString("0", Inv)).Append(" tys., wsi ").Append(k.V)
                  .Append(" u_v sr. ").Append(k.V > 0 ? F(k.U / k.V, "0.00") : "-")
                  .Append(", miast ").Append(k.T).Append(" m_t sr. ").Append(k.T > 0 ? F(k.M / k.T, "0.00") : "-")
                  .Append(", zamkow ").Append(k.C).Append(" podzamcze sr. ").Append(k.C > 0 ? F(k.W / k.C, "0.00") : "-");
            }
            sb.Append(" (krolestwo = wlasciciel osady dzis; srednie po osadach pod regula; plik ").Append(CsvName).Append(").");
            Log.Info(sb.ToString());
        }

        private static void CsvRows(int day, Memory m, Census c, bool watchOn)
        {
            var sb = new StringBuilder(c.Rows.Count * 110);
            foreach (var r in c.Rows)
            {
                try
                {
                    var s = r.S;
                    var f = s.MapFaction;
                    Entry e;
                    bool has = m.Map.TryGetValue(s, out e);
                    bool exc = has && e.Watch && watchOn;
                    string note = "";
                    if (r.Kind == 'w' && !r.HasK) note = "bez_k";
                    else if (exc) note = "straz";
                    else if (has && r.Kind == 'w' && e.Raw > VillageMax) note = "sufit";
                    else if (has && r.Kind != 'w' && e.Raw < MarketMin) note = "dol";
                    else if (has && r.Kind != 'w' && e.Raw > MarketMax) note = "gora";
                    if (r.Kind == 'w' && r.Frozen == null) note += note.Length > 0 ? ",bez_targu" : "bez_targu";
                    else if (r.Kind == 'w' && r.Frozen != r.Game) note += note.Length > 0 ? ",inny_targ_gry" : "inny_targ_gry";
                    float api = has && !exc ? e.Scale : 1f;
                    sb.Append(day).Append(';').Append(s.StringId).Append(';').Append(Clean(Name(s))).Append(';')
                      .Append(r.Kind == 'w' ? "wies" : (r.Kind == 'm' ? "miasto" : "zamek")).Append(';')
                      .Append(Clean(f != null && f.Name != null ? f.Name.ToString() : "")).Append(';').Append(Clean(r.Realm)).Append(';')
                      .Append(r.People.ToString("0", Inv)).Append(';').Append(api.ToString("0.0000", Inv)).Append(';')
                      .Append(has ? e.Raw.ToString("0.0000", Inv) : "").Append(';')
                      .Append(r.Home.ToString("0", Inv)).Append(';').Append(r.Kind == 'w' ? r.Away.ToString("0", Inv) : "").Append(';')
                      .Append(r.Frozen ?? "").Append(';').Append(r.Game ?? "").Append(';').Append(note).Append(Environment.NewLine);
                }
                catch (Exception e) { Stumble("Raport(csv wiersz)", e); }
            }
            Log.Csv(CsvName, CsvHeader, sb.ToString());
        }

        // ============================================================ zapis (arm_peoplescale)

        /// <summary>Zapis: "1|srednia wies|sredni rynek|srednie podzamcze|dzien kalibracji|wsi|miast|zamkow|wies=miasto;wies=miasto;...". Pusty przed kalibracja.</summary>
        internal static string Export()
        {
            if (!_cal) return "";
            var sb = new StringBuilder(80 + _bound.Count * 40);
            sb.Append(Version).Append('|').Append(_refV.ToString("R", Inv)).Append('|').Append(_refM.ToString("R", Inv)).Append('|').Append(_refC.ToString("R", Inv))
              .Append('|').Append(_calDay).Append('|').Append(_calV).Append('|').Append(_calT).Append('|').Append(_calC).Append('|');
            bool first = true;
            foreach (var kv in _bound)
            {
                if (!first) sb.Append(';');
                first = false;
                sb.Append(kv.Key).Append('=').Append(kv.Value);
            }
            return sb.ToString();
        }

        /// <summary>Wczytanie: brak pola (stary zapis), inny format albo blad - kalibracja od nowa przy pierwszym spisie (z linia w logu).</summary>
        internal static void Import(string v)
        {
            _cal = false; _refV = _refM = _refC = 0.0; _calDay = -1; _calV = _calT = _calC = 0; _bound.Clear(); _calHow = "";
            if (string.IsNullOrEmpty(v)) { _loadNote = "zapis bez pola arm_peoplescale - kalibracja przy pierwszym wczytaniu"; return; }
            try
            {
                var p = v.Split('|');
                if (p.Length < 9 || p[0] != Version)
                {
                    _loadNote = "nieznany format pola arm_peoplescale (" + (p.Length > 0 ? p[0] : "?") + ") - kalibracja od nowa";
                    Log.Info("Skale ludzi (GL0): " + _loadNote + ".");
                    return;
                }
                double rv = double.Parse(p[1], NumberStyles.Float, Inv), rm = double.Parse(p[2], NumberStyles.Float, Inv), rc = double.Parse(p[3], NumberStyles.Float, Inv);
                if (!(rv > 0.0) || !(rm > 0.0) || !(rc >= 0.0) || double.IsInfinity(rv) || double.IsInfinity(rm) || double.IsInfinity(rc))
                {
                    _loadNote = "zle odniesienia w polu arm_peoplescale - kalibracja od nowa";
                    Log.Info("Skale ludzi (GL0): " + _loadNote + ".");
                    return;
                }
                var map = new Dictionary<string, string>();
                if (p[8].Length > 0)
                    foreach (var part in p[8].Split(';'))
                    {
                        int eq = part.IndexOf('=');
                        if (eq <= 0 || eq >= part.Length - 1) continue;
                        map[part.Substring(0, eq)] = part.Substring(eq + 1);
                    }
                _refV = rv; _refM = rm; _refC = rc;
                _calDay = int.Parse(p[4], Inv); _calV = int.Parse(p[5], Inv); _calT = int.Parse(p[6], Inv); _calC = int.Parse(p[7], Inv);
                foreach (var kv in map) _bound[kv.Key] = kv.Value;
                _loadNote = "z zapisu";
                _calHow = "z zapisu";
                _cal = true;
            }
            catch (Exception e)
            {
                _cal = false; _refV = _refM = _refC = 0.0; _calDay = -1; _bound.Clear();
                _loadNote = "blad odczytu pola arm_peoplescale - kalibracja od nowa";
                Stumble("Import", e);
            }
        }
    }
}
