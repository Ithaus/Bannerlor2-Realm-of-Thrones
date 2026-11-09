using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Workshops;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// 345 (07.11, Jeff przy Seagard: "dostaje -prawie 3k, a pokazuje mi, ze mam dostac na plusie") - PRZEWIDYWANY DOCHOD GRACZA = PRAWDZIWY OBIEG. Klucz GoldForecastReal.
    /// Podpowiedz zlota (pasek u gory: CampaignUIHelper.GetDenarTooltip i MapInfoVM - CalculateClanGoldChange rodu gracza z opisami, bez pobrania) i panel Finance ekranu rodu
    /// (ClanManagementVM.RefreshDailyValues: CalculateClanIncome i CalculateClanExpenses, Daily Change = |przychod| - |wydatki|) pokazywaly SAM model w chwili najechania:
    ///  - "Village Demesnes" liczy podatek WSZYSTKICH wsi z GetActualVillages, a BK (ClanFinancesPatches.VillageIncomePrefix) przy prawdziwej wyplacie daje podatek wsi,
    ///    ktorej tytul de iure ma ktos inny niz glowa rodu, temu posiadaczowi (ApplyWithdrawal(..., title.deJure); deJure null - nikomu) - ten warunek dziala tylko z pobraniem;
    ///    354: z kluczem VillageTaxToLandHolder tylko posiadaczowi zywemu z frakcji wsi; wrogi / niezywy / brak - graczowi przez "Village Demesnes" (poprawka ich nie zdejmuje);
    ///  - warsztaty i licznik podatku wsi to stan "do tej chwili" (ProfitMade / 2, licznik w polowie doby), a wyplata liczy stan w chwili rozliczenia rodu;
    ///  - wplywy i wydatki modulow Armoury w naszym takcie doby (podatek od nadwyzki ludu, prawa panskie, podatek warsztatow, powinnosci wobec korony, rada, renty, Bank,
    ///    dlugi ...) ida prosto do kiesy, poza modelem.
    /// Zmiana (TYLKO WIDOK - ile gracz dostaje, sie nie zmienia): ksiega kiesy gracza i linie poprawek dopisane do wyniku modelu.
    ///  - Ksiega (zawsze, tani odczyt Hero.MainHero.Gold): doba = od poczatku naszego taktu doby do nastepnego. (a) takt doby rozbity na moduly (DayProfile.Lap - po kazdym
    ///    wywolaniu X.Daily(); zmiana kiesy od poprzedniej granicy idzie na ten modul); (b) rozliczenie rodu gracza (para prefiks / postfiks na
    ///    ClanVariablesCampaignBehavior.DailyTickClan - to, co naprawde weszlo, i podglad modelu tuz przed wyplata: warsztaty, wsie do gracza i do posiadaczy tytulu);
    ///    (c) wyplata prac okregu przy powrocie wozu (DistrictWorks.OnCartReturn - NoteWorksCart); reszta zmiany kiesy (handel, lupy, okupy, zakupy gracza) - "poza obiegiem".
    ///    Historia 7 dob w zapisie (arm_prog345; brak klucza - pusto, linie poza modelem od pierwszej pelnej doby).
    ///  - Poprawki (cache na godzine gry i po kazdej dobie ksiegi): wsie do posiadaczy tytulu (ta sama regula co BK, teraz), warsztaty i wsie gracza - reszta do wyplaty
    ///    (347: srednia wyplata x czesc doby do rozliczenia; tuz przed wyplata ~0), inne roznice przy wyplacie (srednia), pozycje taktu doby wedlug modulu (srednia z dob
    ///    historii; 347: tylko regularne - modul w >= polowie dob historii; okupy, wojna i inne rzadkie poza prognoza, w linii dobowej osobno, rozjazd bez nich). Prace okregu i wioska poboczna
    ///    maja juz wlasne linie (DistrictWorks.AddView, SideVillageFief.AddLines) - w poprawkach pominiete, w ksiedze liczone.
    ///  - Panel Finance: dodatnie poprawki do przychodu, ujemne do wydatkow (tylko przy FinanceBox.In - nasze ksiegi liczace przychod rodu ich nie widza).
    /// Linia dobowa "Prognoza zlota gracza (345): dzien N | ..." - prognoza zrobiona na poczatku doby (koniec naszego taktu) wobec prawdziwej zmiany obiegu tej doby.
    /// 361 (Jeff 08.11: "zuleny rozjad z tego co zarabiam a to co pokazuje" - komunikat gry "Daily Gold Change" to tylko wyplata rodu, bez naszego taktu doby):
    ///  (a) komunikat "Today in total" po wyplacie rodu gracza (klucz GoldDailySummary): wyplata, takt doby i wozy prac od ostatniej wyplaty, zmiana kiesy od ostatniej wyplaty;
    ///  (b) podatek warsztatow z kolejki Seal31B (po wyplacie) albo udzial x przewidywana wyplata warsztatow (przed wyplata) zamiast sredniej 3 dob;
    ///  (c) linia kontrolna "Your purse actually changed" (srednia kiesy z dob ksiegi) poza suma; (d) danina od skarbca wsi (TownFolk.DaWorksRun) osobno, z ostatniej doby;
    ///  (e) reszta warsztatow i wsi do wyplaty z mierzonego przyrostu zapasu (podglad tuz po wyplacie), przy < 3 pomiarach stara metoda.
    /// Kazdy blad liczony (potkniecia), bez globalnych wylacznikow. Watek gry.
    /// </summary>
    internal static class GoldForecast
    {
        private const string P = "Prognoza zlota gracza (345): ";
        private const int Days = 7;
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        internal static bool On { get { var s = Settings.Current; return s != null && s.GoldForecastReal; } }

        /// <summary>Podglad modelu bez naszych linii widoku (prace, wioska poboczna, poprawki 345) - podglad tuz przed wyplata rodu gracza.</summary>
        [ThreadStatic] private static int _raw;
        internal static bool Raw { get { return _raw > 0; } }

        // ------------------------------------------------------------ doba ksiegi
        private sealed class DayRec
        {
            public int Day;
            public long Clan, Pre, WPay, VMe, VOther, Resid, Works, Purse, Tick;
            public int ClanN;
            // 352: krolestwo rodu gracza na poczatku naszego taktu tej doby (StringId; "" - rod bez krolestwa; null - nieznane: doba z zapisu sprzed 352)
            public string Kng;
            // 361e: zmierzony przyrost zapasu warsztatow (GW) i wsi gracza (GV) od poprzedniej wyplaty (podglad tuz po niej) do tej (podglad tuz przed); GN - ile wyplat z pomiarem
            public int GN;
            public long GW, GV;
            // 361b (recenzja): doba utworzona w TickBegin TEJ sesji (bez zapisu; doby z zapisu - false). Para (doba z zapisu, pierwsza doba sesji) nie wchodzi do udzialu
            // podatku warsztatow: kolejka _otPay nie jest zapisywana - podatek od ostatniej wyplaty przed zapisem ginie albo pobiera go doba niezapisana.
            public bool Fresh;
            public Dictionary<string, long> Laps = new Dictionary<string, long>(StringComparer.Ordinal);
        }

        private static readonly List<DayRec> _hist = new List<DayRec>();
        private static DayRec _cur;                 // doba w toku (od TickBegin)
        private static long _purse0, _lapGold;
        private static bool _inTick;
        private static bool _inTickEnd;             // 361d: Corrections() wolane z TickEnd (prognoza na te dobe)
        private static int _stamp;                  // zmiana historii - poprawki od nowa
        private static int _stumbles, _stumblesSess;
        private static string _clanDiag;            // linia wsi przy wyplacie (do linii dobowej)

        // prognoza zrobiona na koniec naszego taktu doby (na te dobe)
        private static bool _fcHave;
        private static long _fcTotal, _fcModel, _fcCorr;
        private static string _fcParts;
        // 352: krolestwo, z ktorym zrobiono prognoze, doby historii w tym krolestwie i moduly korony wliczone do prognozy (do linii dobowej)
        private static string _fcKng;
        private static int _fcKngDays, _fcCrownKeys;
        // 347: moduly taktu doby wliczone do prognozy (pozycje regularne) - reszta dzisiejszego taktu = nieregularne, poza prognoza
        private static HashSet<string> _fcKeys, _corrKeys;
        // 347: godzina gry ostatniego rozliczenia rodu gracza (NaN - nieznana: od wczytania nie bylo wyplaty) - czesc doby do reszty warsztatow i wsi
        private static double _lastPayH = double.NaN;
        // srednie rozjazdu (sesja) - do linii
        private static int _cmpN;
        private static long _cmpAbs, _cmpAbsT;

        // 361a: komunikat "razem dzis" - stan od ostatniej wyplaty rodu gracza (tylko pamiec, bez zapisu: po wczytaniu pierwszy komunikat bez zmiany kiesy)
        private static long _sumTick, _sumWorks;          // takt doby i wozy prac od ostatniej wyplaty
        private static bool _sumTickSeen;                 // byl takt doby od ostatniej wyplaty (albo od wczytania)
        private static long _purseAtPay = long.MinValue;  // kiesa tuz po ostatniej wyplacie; MinValue - nieznana (od wczytania nie bylo wyplaty)
        // 361b: wyplata rodu gracza od poczatku ostatniego taktu doby (pusta kolejka podatku warsztatow = zero do pobrania, a nie "wyplata jeszcze przed nami")
        private static bool _paySinceTick;
        // 361e: podglad warsztatow i wsi gracza tuz PO ostatniej wyplacie (NaN godziny - nieznany: od wczytania nie bylo wyplaty)
        private static long _postW, _postV;
        private static double _postH = double.NaN;
        private static HashSet<object> _postSet;    // 361e: warsztaty glowy rodu i wsie placace graczowi przy ostatniej wyplacie (zmiana zbioru - doba bez pomiaru)
        // 361: opis nowych pozycji poprawek (ostatnie przeliczenie; w TickEnd kopiowany do linii dobowej)
        private static string _diag361, _fc361;
        private const string OtKey = "Seal31B.OwnerTaxDaily", HoardKey = "TownFolk.DaWorksRun";
        /// <summary>368: rata firmy miasta / zamku do kiesy gracza (DsTowns.Pay, co 7 dob na osade) - wlasny klucz taktu (NoteSub), prognoza tylko w dzien raty.</summary>
        internal const string RateKey = "DsTowns.FarmRate";
        // 368: siatka na skoki pozycji regularnych - wartosc doby odstaje od mediany historii o wiecej niz max(SpikeMin, SpikeX x |mediana|) i jest JEDYNA taka doba
        // w historii (dwie i wiecej - zmiana poziomu, zostaje) - w sredniej zastapiona mediana
        private const long SpikeMin = 2000; private const int SpikeX = 5;
        private static List<KeyValuePair<string, long>> _spikeYest = new List<KeyValuePair<string, long>>();   // 368: skoki wczorajszej doby (etykieta, kwota) - linia poza suma
        private static string _rateInfo;   // 368: linia rat firm poza suma (harmonogram 7 dob)
        // 368: szacunek rat tej doby zrobiony na POCZATKU taktu (przed Pay - ta sama formula co podpowiedz "jutro"); -1 - brak. Prognoza TickEnd bierze go (rozjazd
        // linii dobowej sprawdza formule), linia dobowa wypisuje go obok raty naprawde
        private static long _rateTick = -1; private static int _rateTickN;
        private const int GrowMin = 3;

        internal static void Reset()
        {
            _sumTick = _sumWorks = 0; _sumTickSeen = false; _purseAtPay = long.MinValue; _paySinceTick = false;
            _postW = _postV = 0; _postH = double.NaN; _postSet = null; _diag361 = _fc361 = null;
            _hist.Clear(); _cur = null; _purse0 = 0; _lapGold = 0; _inTick = false; _stamp++;
            _fcHave = false; _fcTotal = _fcModel = _fcCorr = 0; _fcParts = null; _clanDiag = null; _fcKng = null; _fcKngDays = _fcCrownKeys = 0; _cacheKng = null;
            _fcKeys = null; _corrKeys = null; _lastPayH = double.NaN;
            _spikeYest = new List<KeyValuePair<string, long>>(); _rateInfo = null; _rateTick = -1; _rateTickN = 0;
            _cmpN = 0; _cmpAbs = _cmpAbsT = 0; _stumbles = 0;
            _cacheHour = int.MinValue; _corr = new List<Corr>();
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++; _stumblesSess++;
            if (_stumblesSess == 1 || _stumblesSess % 200 == 0) Log.Error("GoldForecast." + where + " (potkniecie " + _stumblesSess + ")", e);
        }

        private static long Gold()
        {
            var h = Hero.MainHero;
            return h != null ? h.Gold : 0;
        }

        private static int Today() { try { return (int)CampaignTime.Now.ToDays; } catch { return -1; } }

        // ------------------------------------------------------------ takt doby (ArmouryBehavior.OnDailyTick / DayProfile.Lap)
        /// <summary>Poczatek naszego taktu doby: zamkniecie poprzedniej doby (linia), nowa doba ksiegi. Nie rzuca.</summary>
        internal static void TickBegin()
        {
            try
            {
                if (Campaign.Current == null || Hero.MainHero == null) { _inTick = false; return; }
                long g = Gold();
                if (_cur != null)
                {
                    _cur.Purse = g - _purse0;
                    var done = _cur;
                    _hist.Add(done);
                    while (_hist.Count > Days) _hist.RemoveAt(0);
                    _stamp++;
                    Line(done);
                }
                _cur = new DayRec { Day = Today(), Kng = KngNow(), Fresh = true };
                _purse0 = g; _lapGold = g; _inTick = true;
                _sumTickSeen = true; _paySinceTick = false;   // 361a/b: takt doby od ostatniej wyplaty byl; kolejka podatku warsztatow pobrana w tym takcie
                _rateTick = -1; _rateTickN = 0;
                try { if (On && DsTowns.FarmsLive) _rateTick = DsTowns.PlayerRateDue(0, out _rateTickN, null); }   // 368: przed Pay tej doby
                catch (Exception e) { _rateTick = -1; Stumble("TickBegin(368)", e); }
            }
            catch (Exception e) { _inTick = false; Stumble("TickBegin", e); }
        }

        /// <summary>Granica modulu taktu doby (DayProfile.Lap, kazde wywolanie - takze bez profilu doby): zmiana kiesy od poprzedniej granicy na ten modul.</summary>
        internal static void Lap(string name)
        {
            if (!_inTick || _cur == null) return;
            try
            {
                long g = Gold();
                long d = g - _lapGold;
                _lapGold = g;
                if (d == 0) return;
                string k = Key(name);
                long o; _cur.Laps.TryGetValue(k, out o); _cur.Laps[k] = o + d;
                _cur.Tick += d;
                if (IsWorksKey(k)) _sumWorks += d; else _sumTick += d;   // 361a (recenzja: prace okregu w takcie - pod "village works", jak w podpowiedzi)
            }
            catch (Exception e) { Stumble("Lap", e); }
        }

        /// <summary>355 (Jeff 07.11: tarczowe W6b, ktore gracz PLACI, stalo w podpowiedzi jako "Rent from the crown"): zmiana kiesy gracza d (ujemna - pobor) wewnatrz
        /// odcinka innego modulu idzie na wlasny klucz k, a granica odcinka przesuwa sie o d - nastepny Lap (np. "CrownRents.Daily") jej juz nie widzi. Suma taktu
        /// (Tick) bez zmian - ta sama moneta tylko pod inna nazwa, nic liczone dwa razy. Wolac TUZ PO zmianie kiesy, tylko dla Hero.MainHero. Poza taktem - nic
        /// (zmiana kiesy idzie wtedy "poza obiegiem", jak kazda inna poza taktem). Nie rzuca.</summary>
        internal static void NoteSub(string name, long d)
        {
            if (!_inTick || _cur == null || d == 0) return;
            try
            {
                string k = Key(name);
                long o; _cur.Laps.TryGetValue(k, out o); _cur.Laps[k] = o + d;
                _cur.Tick += d;
                if (IsWorksKey(k)) _sumWorks += d; else _sumTick += d;   // 361a
                _lapGold += d;
            }
            catch (Exception e) { Stumble("NoteSub", e); }
        }

        /// <summary>Koniec naszego taktu doby: ostatni odcinek, potem prognoza na te dobe (model z poprawkami, tak jak podpowiedz zlota). Nie rzuca.</summary>
        internal static void TickEnd()
        {
            if (!_inTick) return;
            Lap("reszta taktu");
            _inTick = false;
            _fcHave = false;
            try
            {
                if (!On || Campaign.Current == null || Clan.PlayerClan == null || Hero.MainHero == null) return;
                var fin = Campaign.Current.Models.ClanFinanceModel;
                if (fin == null) return;
                _cacheHour = int.MinValue;   // poprawki z dzisiejsza historia
                long all = (long)Math.Round(fin.CalculateClanGoldChange(Clan.PlayerClan, true, false, false).ResultNumber);
                long corr = 0; var sb = new StringBuilder();
                List<Corr> cs;
                _inTickEnd = true;
                try { cs = Corrections(); }
                finally { _inTickEnd = false; _cacheHour = int.MinValue; }   // 361d: podpowiedz liczy od nowa (danina z _cur, nie z historii)
                foreach (var c in cs)
                {
                    corr += c.Amt;
                    if (sb.Length > 0) sb.Append(", ");
                    sb.Append(c.Log).Append(' ').Append(c.Amt >= 0 ? "+" : "").Append(c.Amt);
                }
                _fcTotal = all; _fcCorr = corr; _fcModel = all - corr; _fcParts = sb.Length > 0 ? sb.ToString() : "-";
                _fc361 = _diag361;   // 361: sposob liczenia nowych pozycji (Corrections tuz wyzej)
                _fcKeys = _corrKeys != null ? new HashSet<string>(_corrKeys, StringComparer.Ordinal) : new HashSet<string>(StringComparer.Ordinal);
                _fcKng = KngNow(); _fcKngDays = 0; _fcCrownKeys = 0;
                // 352r (przeglad): bez krolestwa doby bez krolestwa (Kng "") nie sa "dobami w krolestwie" - N = 0 (G7)
                if (_fcKng.Length > 0) foreach (var d in _hist) if (SameKng(d, _fcKng)) _fcKngDays++;
                foreach (var k in _fcKeys) if (IsCrownKey(k)) _fcCrownKeys++;
                _fcHave = true;
            }
            catch (Exception e) { _fcHave = false; Stumble("TickEnd", e); }
        }

        /// <summary>361a: modul prac okregu (DistrictWorks.Daily / BeforeProduction) - w komunikacie pod "village works" razem z wozami (podpowiedz ma dla nich wlasna linie).</summary>
        private static bool IsWorksKey(string k) { return k != null && k.StartsWith("DistrictWorks.", StringComparison.Ordinal); }

        private static string Key(string name)
        {
            if (string.IsNullOrEmpty(name)) return "?";
            var b = new StringBuilder(name.Length);
            foreach (char ch in name) b.Append(ch == '~' || ch == '#' || ch == ';' || ch == '=' || ch == ',' || ch == '|' ? '_' : ch);
            return b.ToString();
        }

        // ------------------------------------------------------------ 352: pozycje korony tylko z dob w tym samym krolestwie
        // Jeff 07.11 (rod KARDA odszedl z Polnocy z lennami, Mend S9 - rod bez krolestwa): podpowiedz dalej pokazywala "Rent from the crown -1025" i "Dues to the crown -17"
        // ze sredniej dob, gdy rod byl wasalem. Moduly, ktorych przeplyw z kiesa gracza istnieje TYLKO przez czlonkostwo w krolestwie (sprawdzone w kodzie modulow):
        //  - KingdomTreasury.* - powinnosci rodow krolestwa (c.Kingdom), daniny/clo/mennica do wladcy, zwrot zoldu wasalom, W5/W6/W7 (plan krolestwa w wojnie);
        //  - CrownRents. (renty 180 rodom krolestwa wedlug lenn; w tym kroku W5/W6b tarczowe), CrownWar314. (c.Kingdom == k), CrownGifts. (rody krolestwa odbiorcy),
        //    CrownBorrow. (kredyt korony), CrownCallToWar. (portfele krolestw), MercContract./MercTenure. (sluzba najemna u korony);
        //  - Ransom178.ThirdsSettle (1/9 lupow do skarbca OBECNEGO krolestwa gracza; bez krolestwa - wpis skreslony).
        // NIE: CrownIncome.Reparations (rata okupu krola placi wierzyciela-bohatera bez wzgledu na jego krolestwo; Begin/End kiesy nie ruszaja),
        // Ransom178.CrownStep (nagroda za wielkiego jenca z zapamietanej korony - placona takze po odejsciu; 367c: niewyplacona za jenca, ktorego trzymajacy opuscil korone - anulowana),CouncilWages35, GentryService (miedzy bohaterami).
        private static readonly string[] CrownKeys =
        {
            "KingdomTreasury.", "CrownRents.", "CrownWar314.", "CrownGifts.", "CrownBorrow.", "CrownCallToWar.", "MercContract.", "MercTenure.", "Ransom178.ThirdsSettle",
        };

        internal static bool IsCrownKey(string k)
        {
            if (k == null) return false;
            foreach (var p in CrownKeys) if (k.StartsWith(p, StringComparison.Ordinal)) return true;
            return false;
        }

        /// <summary>Krolestwo rodu gracza teraz: StringId, "" - bez krolestwa (najemnik ma Kingdom pracodawcy).</summary>
        private static string KngNow()
        {
            try { var c = Clan.PlayerClan; var k = c != null ? c.Kingdom : null; return k != null && k.StringId != null ? Key(k.StringId) : ""; }
            catch { return ""; }
        }

        /// <summary>Doba historii liczy sie do pozycji korony: to samo krolestwo co teraz. Doba z zapisu sprzed 352 (Kng null) - tylko gdy rod jest teraz w krolestwie
        /// (historia to najwyzej 7 dob - zwykle to samo krolestwo; rod bez krolestwa nie ma z korona zadnego przeplywu, wiec stare doby nie moga mu nic dac).</summary>
        private static bool SameKng(DayRec d, string now)
        {
            if (d.Kng == null) return now.Length > 0;
            return string.Equals(d.Kng, now, StringComparison.Ordinal);
        }

        /// <summary>DistrictWorks.OnCartReturn: wyplata prac okregu glowie rodu przy powrocie wozu (poza taktem doby). Tylko gracz.</summary>
        internal static void NoteWorksCart(Hero leader, long got)
        {
            try
            {
                if (got == 0 || leader == null || leader != Hero.MainHero || _inTick) return;
                if (_cur != null) _cur.Works += got;
                _sumWorks += got;   // 361a: komunikat "razem dzis" (takze przed pierwszym taktem po wczytaniu)
            }
            catch (Exception e) { Stumble("NoteWorksCart", e); }
        }

        // ------------------------------------------------------------ rozliczenie rodu gracza (DailyTickClan)
        internal struct TickState { public bool On; public long G0; public long Pre, W, VMe, VOther; public List<VilNow> Vils; public long[] C0; public long[] H0; }

        internal struct VilNow { public Village V; public int Amt; public Hero DeJure; public bool HasTitle; public bool ToMe; }

        private static bool _clanWired;
        private static string _clanWhy = "nie probowano";

        internal static void ApplyAll(Harmony h)
        {
            try
            {
                var m = AccessTools.Method(typeof(ClanVariablesCampaignBehavior), "DailyTickClan", new[] { typeof(Clan) });
                if (m == null) _clanWhy = "brak DailyTickClan";
                else
                {
                    // prefiks przed innymi (stan kiesy przed wszystkim, co rozliczenie robi), postfiks po innych (SoldierPay i reszta - rejestrowane wczesniej)
                    h.Patch(m, prefix: new HarmonyMethod(typeof(GoldForecast), nameof(ClanTickPrefix)) { priority = 10000 },
                               postfix: new HarmonyMethod(typeof(GoldForecast), nameof(ClanTickPostfix)) { priority = -10000 });
                    _clanWired = true; _clanWhy = null;
                }
            }
            catch (Exception e) { _clanWired = false; _clanWhy = "blad latki"; Log.Error("GoldForecast.ApplyAll", e); }
            Log.Info(P + "start | rozliczenie rodu gracza (DailyTickClan) " + (_clanWired ? "wpiete" : "BRAK (" + _clanWhy + ")")
                     + " | tarczowe gracza osobna pozycja (355).");   // 355: znacznik dla sprawdzarki (G8 - pobor W6b gracza pod kluczem KingdomTreasury.ScutagePlayer)
        }

        public static void ClanTickPrefix(Clan __0, out TickState __state)
        {
            __state = default(TickState);
            try
            {
                if (__0 == null || !ReferenceEquals(__0, Clan.PlayerClan) || Hero.MainHero == null || Campaign.Current == null) return;
                __state.On = true;
                __state.G0 = Gold();
                var fin = Campaign.Current.Models.ClanFinanceModel;
                if (fin != null)
                {
                    _raw++;
                    try { __state.Pre = (long)Math.Round(fin.CalculateClanGoldChange(__0, true, false, false).ResultNumber); }
                    finally { _raw--; }
                    __state.W = WorkshopsNow(fin);
                }
                List<VilNow> vl; long me, other;
                VillagesNow(out vl, out me, out other);
                __state.Vils = vl; __state.VMe = me; __state.VOther = other;
                if (vl != null)
                {
                    __state.C0 = new long[vl.Count]; __state.H0 = new long[vl.Count];
                    for (int i = 0; i < vl.Count; i++)
                    {
                        __state.C0[i] = vl[i].V != null ? vl[i].V.TradeTaxAccumulated : 0;
                        __state.H0[i] = vl[i].DeJure != null ? vl[i].DeJure.Gold : 0;
                    }
                }
            }
            catch (Exception e) { __state.On = false; Stumble("ClanTickPrefix", e); }
        }

        public static void ClanTickPostfix(TickState __state)
        {
            if (!__state.On) return;
            try
            {
                long c = Gold() - __state.G0;
                try { _lastPayH = CampaignTime.Now.ToHours; } catch { _lastPayH = double.NaN; }
                var d = _cur;
                if (d != null)
                {
                    d.Clan += c; d.ClanN++;
                    d.Pre += __state.Pre; d.WPay += __state.W; d.VMe += __state.VMe; d.VOther += __state.VOther;
                    d.Resid += c - (__state.Pre - __state.VOther);
                    _stamp++;
                }
                // wsie przy wyplacie: licznik i kiesa posiadacza tytulu przed / po (dowod, dokad poszedl podatek)
                var sb = new StringBuilder();
                sb.Append("rozliczenie: zmiana kiesy ").Append(c).Append(", podglad modelu tuz przed ").Append(__state.Pre)
                  .Append(" (warsztaty ").Append(__state.W).Append(", wsie do gracza ").Append(__state.VMe).Append(", do posiadaczy tytulu ").Append(__state.VOther)
                  .Append("), inne roznice ").Append(c - (__state.Pre - __state.VOther));
                var vl = __state.Vils;
                if (vl != null && vl.Count > 0)
                {
                    sb.Append(" | wsie ").Append(vl.Count).Append(": ");
                    int shown = 0;
                    for (int i = 0; i < vl.Count && shown < 12; i++, shown++)
                    {
                        var x = vl[i];
                        if (shown > 0) sb.Append("; ");
                        long c1 = x.V != null ? x.V.TradeTaxAccumulated : 0;
                        sb.Append(Name(x.V != null ? x.V.Settlement : null)).Append(' ').Append(x.Amt).Append(x.ToMe ? " do gracza" : " do posiadacza ")
                          .Append(x.ToMe ? "" : (x.DeJure != null ? Name(x.DeJure) + (x.DeJure.IsAlive ? "" : " (niezywy)") + (x.DeJure.Clan == Clan.PlayerClan ? " (rod gracza)" : " (" + Name(x.DeJure.Clan) + ")") : (x.HasTitle ? "BRAK (nikt)" : "-")))
                          .Append(", licznik ").Append(__state.C0 != null ? __state.C0[i] : 0).Append(" -> ").Append(c1);
                        if (!x.ToMe && x.DeJure != null && __state.H0 != null) sb.Append(", kiesa posiadacza ").Append(x.DeJure.Gold - __state.H0[i] >= 0 ? "+" : "").Append(x.DeJure.Gold - __state.H0[i]);
                    }
                    if (vl.Count > shown) sb.Append("; ...");
                }
                _clanDiag = sb.ToString();
            }
            catch (Exception e) { Stumble("ClanTickPostfix", e); }
            // 361e: podglad warsztatow i wsi gracza tuz PO wyplacie; przyrost tej doby = podglad przed ta wyplata - podglad po poprzedniej (tylko gdy odstep ~ doba:
            // po wczytaniu gra ustala nowa godzine rozliczenia, pierwszy odstep jest dowolny - bez pomiaru). Bez CalculateClanGoldChange - te same odczyty co prefiks.
            try
            {
                var fin = Campaign.Current != null ? Campaign.Current.Models.ClanFinanceModel : null;
                double h = CampaignTime.Now.ToHours;
                long wPost = fin != null ? WorkshopsNow(fin) : 0;
                List<VilNow> vl2; long vPost, oth2;
                VillagesNow(out vl2, out vPost, out oth2);
                // recenzja (08.11): zbior warsztatow glowy rodu i wsi placacych graczowi - po sprzedazy / utracie / kupnie miedzy wyplatami roznica podgladow
                // to nie przyrost, tylko wyplata calego obiektu (ProfitMade po wyplacie nie spada do zera) - wtedy doba bez pomiaru
                var set = new HashSet<object>();
                var pc = Clan.PlayerClan; var ld = pc != null ? pc.Leader : null;
                if (ld != null && ld.OwnedWorkshops != null) foreach (Workshop ws in ld.OwnedWorkshops) if (ws != null) set.Add(ws);
                if (vl2 != null) foreach (var x in vl2) if (x.ToMe && x.V != null) set.Add(x.V);
                var d = _cur;
                if (d != null && !double.IsNaN(_postH))
                {
                    double gap = h - _postH;
                    bool same = _postSet != null && _postSet.SetEquals(set);
                    if (gap >= 20 && gap <= 28 && same) { d.GN++; d.GW += __state.W - _postW; d.GV += __state.VMe - _postV; _stamp++; }
                    else if (gap >= 20 && gap <= 28) Log.Info(P + "przyrost 361e: doba bez pomiaru - zmienil sie zbior warsztatow / wsi gracza od poprzedniej wyplaty");
                }
                _postW = wPost; _postV = vPost; _postH = h; _postSet = set;
            }
            catch (Exception e) { _postH = double.NaN; _postSet = null; Stumble("ClanTickPostfix(361e)", e); }
            // 361a: komunikat "razem dzis" zaraz po komunikacie gry "Daily Gold Change" (ten pokazuje tylko wyplate rodu, bez naszego taktu doby)
            try
            {
                long g = Gold(), c = g - __state.G0;
                var s = Settings.Current;
                if (s != null && s.GoldDailySummary) DailySummary(c, g);
            }
            catch (Exception e) { Stumble("ClanTickPostfix(361a)", e); }
            finally
            {
                // stan "od ostatniej wyplaty" od nowa - takze przy wylaczonym komunikacie (wlaczenie w trakcie gry liczy od nastepnej wyplaty poprawnie)
                try { _purseAtPay = Gold(); } catch { _purseAtPay = long.MinValue; }
                _sumTick = 0; _sumWorks = 0; _sumTickSeen = false; _paySinceTick = true;
                _stamp++;   // 361b (recenzja): kolejka podatku warsztatow i _paySinceTick zmienione - poprawki od nowa (takze bez doby w toku)
            }
        }

        private static string Sg(long v) { return (v >= 0 ? "+" : "") + v.ToString("N0", Inv); }

        /// <summary>361a: wlasny komunikat po wyplacie rodu gracza: wyplata, takt doby i wozy prac od ostatniej wyplaty, zmiana kiesy od ostatniej wyplaty i reszta
        /// (handel, lupy, okupy). Kolejnosc taktu i wyplaty zalezy od godziny wczytania - liczone "od ostatniej wyplaty", nie od polnocy. Rzuca (wolajacy lapie).</summary>
        private static void DailySummary(long pay, long g)
        {
            bool purse = _purseAtPay != long.MinValue;
            var m = new StringBuilder("Today in total: daily payout ").Append(Sg(pay));
            if (purse || _sumTickSeen) m.Append(", daily dues and duties ").Append(Sg(_sumTick));
            else m.Append(", daily dues and duties: not counted yet since loading");
            m.Append(", village works ").Append(Sg(_sumWorks));
            long ch = 0, other = 0;
            if (purse)
            {
                ch = g - _purseAtPay;
                other = ch - pay - _sumTick - _sumWorks;
                m.Append(" - your purse changed by ").Append(Sg(ch)).Append(" since the last payout (trade, loot, ransoms and other: ").Append(Sg(other)).Append(").");
            }
            else m.Append(". The change of your purse is shown from the next payout on.");
            string text = m.ToString();
            TaleWorlds.Library.InformationManager.DisplayMessage(new TaleWorlds.Library.InformationMessage(text, TaleWorlds.Library.Color.FromUint(0xFFE8C547u)));
            Log.Info(P + "komunikat dobowy (361a): wyplata " + pay + ", takt doby od ostatniej wyplaty " + (purse || _sumTickSeen ? _sumTick.ToString(Inv) : "nieznany (po wczytaniu)")
                     + ", wozy prac " + _sumWorks + ", kiesa od ostatniej wyplaty " + (purse ? ch.ToString(Inv) + ", inne " + other.ToString(Inv) : "nieznana (pierwsza wyplata po wczytaniu)")
                     + " | tekst: " + text);
        }

        // ------------------------------------------------------------ odczyty modelu teraz (warsztaty, wsie wedlug regul BK)
        private static long WorkshopsNow(TaleWorlds.CampaignSystem.ComponentInterfaces.ClanFinanceModel fin)
        {
            // gra (DefaultClanFinanceModel.CalculateClanIncomeInternal): warsztaty tylko glowy rodu (CalculateHeroIncomeFromWorkshops(clan.Leader)) - ta sama lista
            long w = 0;
            var pc = Clan.PlayerClan;
            var h = pc != null ? pc.Leader : null;
            if (h == null || fin == null || h.OwnedWorkshops == null) return 0;
            foreach (Workshop ws in h.OwnedWorkshops)
            {
                if (ws == null) continue;
                int x = fin.CalculateOwnerIncomeFromWorkshop(ws);
                if (x > 0) w += x;
            }
            return w;
        }

        private static bool _bkDone;
        private static MethodInfo _mActual, _mVTax, _mPopulated, _mGetTitle;
        private static PropertyInfo _pCfg, _pTax, _pPop, _pTitles;
        private static MemberInfo _deJure;

        private static void ResolveBk()
        {
            if (_bkDone) return;
            _bkDone = true;
            try
            {
                var ext = AccessTools.TypeByName("BannerKings.Extensions.ClanExtensions");
                _mActual = ext != null ? AccessTools.Method(ext, "GetActualVillages", new[] { typeof(Clan) }) : null;
                var cfgT = AccessTools.TypeByName("BannerKings.BannerKingsConfig");
                _pCfg = cfgT != null ? AccessTools.Property(cfgT, "Instance") : null;
                _pTax = cfgT != null ? AccessTools.Property(cfgT, "TaxModel") : null;
                _mVTax = _pTax != null ? AccessTools.Method(_pTax.PropertyType, "CalculateVillageTaxFromIncome", new[] { typeof(Village), typeof(bool), typeof(bool) }) : null;
                _pPop = cfgT != null ? AccessTools.Property(cfgT, "PopulationManager") : null;
                _mPopulated = _pPop != null ? AccessTools.Method(_pPop.PropertyType, "IsSettlementPopulated", new[] { typeof(Settlement) }) : null;
                _pTitles = cfgT != null ? AccessTools.Property(cfgT, "TitleManager") : null;
                _mGetTitle = _pTitles != null ? AccessTools.Method(_pTitles.PropertyType, "GetTitle", new[] { typeof(Settlement) }) : null;
                if (_mGetTitle != null)
                {
                    _deJure = (MemberInfo)AccessTools.Property(_mGetTitle.ReturnType, "deJure") ?? AccessTools.Field(_mGetTitle.ReturnType, "deJure");
                }
            }
            catch (Exception e) { Stumble("ResolveBk", e); }
        }

        private static Hero DeJureOf(object title)
        {
            if (title == null || _deJure == null) return null;
            var p = _deJure as PropertyInfo;
            if (p != null) return p.GetValue(title, null) as Hero;
            var f = _deJure as FieldInfo;
            return f != null ? f.GetValue(title) as Hero : null;
        }

        /// <summary>Wsie rodu gracza wedlug BK (GetActualVillages) i podatek kazdej TERAZ (CalculateVillageTaxFromIncome - to samo, co BK wola w podgladzie), rozdzielone tak,
        /// jak BK przy prawdziwej wyplacie: tytul de iure u glowy rodu albo bez tytulu - graczowi, inaczej posiadaczowi tytulu (deJure null - nikomu).</summary>
        private static void VillagesNow(out List<VilNow> list, out long me, out long other)
        {
            list = null; me = 0; other = 0;
            ResolveBk();
            var pc = Clan.PlayerClan;
            if (pc == null || pc.Leader == null || _mActual == null || _pCfg == null || _mVTax == null) return;
            object cfg = _pCfg.GetValue(null, null);
            if (cfg == null) return;
            object tax = _pTax.GetValue(cfg, null);
            object pop = _pPop != null ? _pPop.GetValue(cfg, null) : null;
            object titles = _pTitles != null ? _pTitles.GetValue(cfg, null) : null;
            if (tax == null || titles == null) return;   // BK bez menedzera tytulow - prefiks BK nie dziala, gra liczy po swojemu
            var vs = _mActual.Invoke(null, new object[] { pc }) as IEnumerable;
            if (vs == null) return;
            list = new List<VilNow>();
            foreach (var o in vs)
            {
                var v = o as Village;
                if (v == null || v.Settlement == null) continue;
                try
                {
                    // osada bez danych BK: GetPopData w podatku BK inicjuje dane (zapis) - nie wolamy (podglad BK i tak zawolal; tu bez kwoty)
                    if (pop != null && _mPopulated != null && !(bool)_mPopulated.Invoke(pop, new object[] { v.Settlement })) continue;
                    var en = (ExplainedNumber)_mVTax.Invoke(tax, new object[] { v, false, false });
                    int amt = (int)en.ResultNumber;
                    object title = _mGetTitle != null ? _mGetTitle.Invoke(titles, new object[] { v.Settlement }) : null;
                    Hero dj = DeJureOf(title);
                    // 354: posiadacz wrogi (inna frakcja niz wies), niezywy albo brak - BK z transpilerem 354 daje podatek panu ziemi (Village Demesnes, jak wlasne wsie)
                    bool toMe = title == null || dj == pc.Leader || !VillageTaxToLand354.HolderGets(dj, v);
                    list.Add(new VilNow { V = v, Amt = amt, DeJure = dj, HasTitle = title != null, ToMe = toMe });
                    if (toMe) me += amt; else other += amt;
                }
                catch (Exception e) { Stumble("VillagesNow", e); }
            }
        }

        // ------------------------------------------------------------ poprawki widoku
        private sealed class Corr { public int Amt; public TextObject Txt; public string Log; public List<KeyValuePair<int, TextObject>> Details; }

        private static List<Corr> _corr = new List<Corr>();
        private static int _cacheHour = int.MinValue, _cacheStamp = -1;
        private static string _cacheKng;   // 352: zmiana krolestwa w tej godzinie - poprawki od nowa

        private static string Avg(int n) { return n == 1 ? "yesterday" : "average of the last " + n + " days"; }

        private static List<Corr> Corrections()
        {
            int hour = int.MinValue;
            try { hour = (int)CampaignTime.Now.ToHours; } catch { }
            string kng = KngNow();
            if (hour == _cacheHour && _cacheStamp == _stamp && string.Equals(kng, _cacheKng, StringComparison.Ordinal)) return _corr;
            _cacheHour = hour; _cacheStamp = _stamp; _cacheKng = kng;
            var res = new List<Corr>();
            try
            {
                var fin = Campaign.Current != null ? Campaign.Current.Models.ClanFinanceModel : null;
                // 1. wsie, ktorych podatek BK przy wyplacie daje posiadaczowi tytulu
                List<VilNow> vl; long me, other;
                VillagesNow(out vl, out me, out other);
                if (other > 0 && vl != null)
                {
                    var c = new Corr { Amt = -(int)Math.Min(int.MaxValue, other), Log = "wsie do posiadaczy tytulu", Details = new List<KeyValuePair<int, TextObject>>() };
                    int n = 0;
                    foreach (var x in vl)
                    {
                        if (x.ToMe || x.Amt <= 0) continue;
                        n++;
                        string who = x.DeJure == null ? "is lost: the title has no holder" : "goes to its title holder " + Name(x.DeJure) + (x.DeJure.IsAlive ? "" : " (dead - nobody gets it)");
                        c.Details.Add(new KeyValuePair<int, TextObject>(-x.Amt, new TextObject("{=!}Village tax of " + Name(x.V.Settlement) + " " + who + ", not to you")));
                    }
                    c.Txt = new TextObject("{=!}Village taxes paid to the title holders, not to you (" + n + (n == 1 ? " village)" : " villages)"));
                    res.Add(c);
                }
                // 2-4. srednie z wyplat rodu (doby z rozliczeniem)
                var diag = new StringBuilder();
                int np = 0; long sw = 0, sv = 0, sr = 0;
                foreach (var d in _hist) if (d.ClanN > 0) { np++; sw += d.WPay / d.ClanN; sv += d.VMe / d.ClanN; sr += d.Resid / d.ClanN; }
                // 361e: zmierzony przyrost zapasu warsztatow i wsi gracza miedzy wyplatami (ostatnie 7 pomiarow: historia i doba w toku)
                int ng = 0; long gw = 0, gv = 0;
                {
                    var recs = new List<DayRec>(_hist);
                    if (_cur != null) recs.Add(_cur);
                    for (int i = recs.Count - 1; i >= 0 && ng < Days; i--)
                    {
                        var d = recs[i];
                        if (d.GN <= 0) continue;
                        ng++; gw += d.GW / d.GN; gv += d.GV / d.GN;
                    }
                }
                bool grow = ng >= GrowMin;
                double f = double.NaN;
                if (!double.IsNaN(_lastPayH)) { try { f = (CampaignTime.Now.ToHours - _lastPayH) / 24.0; } catch { } }
                if (!double.IsNaN(f)) { if (f < 0) f = 0; else if (f > 1) f = 1; }
                int dwRest = 0;   // 361b: reszta warsztatow do wyplaty (do przewidywanej wyplaty warsztatow)
                if (np > 0 || grow)
                {
                    // 347 (test at346: "warsztaty do wyplaty" +1000..+2170 co dobe, a sam model + wsie do posiadaczy trafial w rozliczenie co do ~30 zl): stan "do tej chwili"
                    // rosnie przez dobe, wiec reszta do wyplaty = srednia wyplata x czesc doby, ktora jeszcze zostala do rozliczenia ((1 - f), f = godziny od ostatniej
                    // wyplaty / 24). Tuz przed wyplata f ~ 1 - model juz zawiera calosc, poprawka ~ 0. Bez znanej wyplaty od wczytania - bez poprawki (model jak jest).
                    // 361e (diagnoza 08.11: w dobach, gdy zapas tylko splywa, srednia wyplata zawyzala reszte o 150-650): przy >= 3 pomiarach przyrostu reszta =
                    // (1 - f) x sredni PRZYROST zapasu od wyplaty do wyplaty (po wyplacie zapas nie spada do zera - liczy sie tylko to, co dojdzie); mniej - stara metoda.
                    // Wymog 3, nie 7 pomiarow: wyplata tuz po kazdym wczytaniu nie ma pomiaru (nowa godzina rozliczenia), wiec 7 z 7 oznaczaloby tydzien starej metody po wczytaniu.
                    if (!double.IsNaN(f))
                    {
                        long bw = grow ? gw : sw, bv = grow ? gv : sv; int bn = grow ? ng : np;
                        string how = grow ? "growth since the last payout, " : "payout ";
                        string lg = grow ? " (przyrost " + ng + " dob)" : " (srednia wyplata " + np + " dob)";
                        int dw = (int)Math.Round((1.0 - f) * bw / bn);
                        dwRest = dw;
                        if (dw != 0) res.Add(new Corr { Amt = dw, Log = "warsztaty do wyplaty" + lg, Txt = new TextObject("{=!}Workshops: rest of today's profit (" + how + Avg(bn) + ")") });
                        int dv = (int)Math.Round((1.0 - f) * bv / bn);
                        if (dv != 0) res.Add(new Corr { Amt = dv, Log = "wsie gracza do wyplaty" + lg, Txt = new TextObject("{=!}Village taxes: rest of today's sales (" + how + Avg(bn) + ")") });
                        diag.Append("reszta warsztatow/wsi ").Append(grow ? "z przyrostu" : "ze sredniej wyplaty").Append(" (1-f ").Append((1.0 - f).ToString("0.00", Inv))
                            .Append(", przyrost ").Append(ng).Append(" pomiarow: warsztaty ").Append(ng > 0 ? (gw / ng).ToString(Inv) : "-").Append(", wsie ").Append(ng > 0 ? (gv / ng).ToString(Inv) : "-")
                            .Append("; srednia wyplata ").Append(np).Append(" dob: warsztaty ").Append(np > 0 ? (sw / np).ToString(Inv) : "-").Append(", wsie ").Append(np > 0 ? (sv / np).ToString(Inv) : "-").Append(')');
                    }
                    else diag.Append("reszta warsztatow/wsi: brak (od wczytania nie bylo wyplaty)");
                    if (np > 0)
                    {
                        int dr = (int)Math.Round((double)sr / np);
                        if (dr != 0) res.Add(new Corr { Amt = dr, Log = "inne roznice przy wyplacie", Txt = new TextObject("{=!}Other changes at the daily payout (" + Avg(np) + ")") });
                    }
                }
                // 4b. 361b (diagnoza 08.11: podatek warsztatow to staly udzial ~28,8% ostatniej wyplaty warsztatow, pobierany w nastepnym takcie doby - srednia z 3 dob
                // mylila sie o 600-1100 po skoku wyplaty): po wyplacie - kwota z kolejki Seal31B (naliczona, niepobrana; odczyt bez zmiany stanu); przed wyplata -
                // udzial (podatek / wyplata poprzedniej doby, z historii) x przewidywana wyplata warsztatow (model teraz + reszta do wyplaty). Brak danych - jak dotad (srednia).
                bool otDone = false;
                try
                {
                    int otN; int pend = Seal31B.PendingOwnerTax(Hero.MainHero, out otN);
                    if (pend >= 0)
                    {
                        if (otN > 0)
                        {
                            otDone = true;
                            res.Add(new Corr { Amt = -pend, Log = "podatek warsztatow 361b (kolejka " + otN + ")", Txt = new TextObject("{=!}Workshop owner tax to the towns (on the last workshop payout)") });
                            diag.Append("; podatek warsztatow z kolejki ").Append(-pend).Append(" (").Append(otN).Append(" migawek)");
                        }
                        else if (_paySinceTick)
                        {
                            otDone = true;   // wyplata juz byla, a kolejka pusta: nastepny takt nic nie pobierze
                            diag.Append("; podatek warsztatow 0 (wyplata byla, kolejka pusta)");
                        }
                        else
                        {
                            // udzial: pary (wyplata warsztatow doby D-1, podatek w takcie doby D) - tylko kolejne doby z jedna wyplata (luka po wczytaniu - kolejka ginie)
                            var recs = new List<DayRec>(_hist);
                            if (_cur != null && !_inTick) recs.Add(_cur);
                            long tp = 0, tt = 0; int pairs = 0;
                            for (int i = 1; i < recs.Count; i++)
                            {
                                var a = recs[i - 1]; var b = recs[i];
                                if (b.Day != a.Day + 1 || a.ClanN != 1 || a.WPay <= 0) continue;
                                if (!a.Fresh && b.Fresh) continue;   // luka po wczytaniu (kolejka podatku nie jest zapisywana)
                                long t; b.Laps.TryGetValue(OtKey, out t);
                                tp += a.WPay; tt += -t; pairs++;
                            }
                            if (pairs > 0 && tp > 0)
                            {
                                otDone = true;
                                double share = Math.Max(0.0, (double)tt / tp);
                                long predW;
                                var fin2 = Campaign.Current != null ? Campaign.Current.Models.ClanFinanceModel : null;
                                if (!double.IsNaN(f)) predW = (fin2 != null ? WorkshopsNow(fin2) : 0) + dwRest;
                                else predW = np > 0 ? sw / np : (fin2 != null ? WorkshopsNow(fin2) : 0);
                                if (predW < 0) predW = 0;
                                int a2 = -(int)Math.Round(share * predW);
                                string pct = (100.0 * share).ToString("0.0", Inv);
                                if (a2 != 0) res.Add(new Corr { Amt = a2, Log = "podatek warsztatow 361b (udzial " + pct + "% x " + predW + ")", Txt = new TextObject("{=!}Workshop owner tax to the towns (about " + pct + "% of the coming workshop payout)") });
                                diag.Append("; podatek warsztatow z udzialu ").Append(pct).Append("% (").Append(pairs).Append(" par) x przewidywana wyplata ").Append(predW).Append(" = ").Append(a2);
                            }
                            else diag.Append("; podatek warsztatow: brak par w historii - srednia jak dotad");
                        }
                    }
                    else diag.Append("; podatek warsztatow: D11-3 nieczynne - srednia jak dotad");
                }
                catch (Exception e) { otDone = false; Stumble("Corrections(361b)", e); }
                // 5. nasz takt doby wedlug pozycji (srednia z dob historii; dzien bez wpisu = 0)
                // 347 (test at346: okupy +12081 / -5859, wojna +9040, skarbiec zamku +3051 raz na 9 dob - srednia przenosila je na kazda dobe): do prognozy tylko
                // pozycje regularne - MODUL (klucz taktu) obecny w co najmniej polowie dob historii; rzadsze (okupy, wojna, sprzedaze) - nieregularne, poza prognoza
                // (linia dobowa je wypisuje). 347c (test at347): regula na etykiecie przepuszczala CrownWar314 +9040 jako regularne, bo pod ta sama etykieta
                // "Dues to the crown" jest codzienny KingdomTreasury.Daily - dlatego licznik dob wedlug modulu, kwoty dalej sumowane wedlug etykiety.
                int nd = _hist.Count;
                var keys = new HashSet<string>(StringComparer.Ordinal);
                if (nd > 0)
                {
                    var days = new Dictionary<string, int>(StringComparer.Ordinal);
                    // 352: pozycja korony z doby w innym krolestwie (albo sprzed odejscia) = brak wpisu w tej dobie (jak dzien bez wpisu); rod bez krolestwa - zadnej
                    foreach (var d in _hist)
                    {
                        bool same = SameKng(d, kng);
                        foreach (var kv in d.Laps)
                        {
                            if (!same && IsCrownKey(kv.Key)) continue;
                            int c0; days.TryGetValue(kv.Key, out c0); days[kv.Key] = c0 + 1;   // Laps - jeden wpis na modul w dobie
                        }
                    }
                    // 347c: regularny = w >= polowie dob historii i w co najmniej 2 dobach (przy 1 dobie historii nic nie jest "regularne" - prognoza = model);
                    // kwota = srednia z ostatnich 3 dob (symulacja na at346/at347: 3 doby lapia skoki poziomu, np. TownFolk.Daily 30 -> 900, lepiej niz 7)
                    foreach (var kv in days) if (kv.Value * 2 >= nd && kv.Value >= 2) keys.Add(kv.Key);
                    int na = Math.Min(3, nd);
                    var sum = new Dictionary<string, long>(StringComparer.Ordinal);
                    var order = new List<string>();
                    var spikes = SpikesOf(kng, keys, diag);   // 368: (klucz#indeks doby) -> mediana zamiast wartosci
                    for (int hi = nd - na; hi < nd; hi++)
                    {
                        bool same = SameKng(_hist[hi], kng);
                        foreach (var kv in _hist[hi].Laps)
                        {
                            if (!keys.Contains(kv.Key)) continue;
                            if (!same && IsCrownKey(kv.Key)) continue;   // 352: kwota korony tylko z dob w tym samym krolestwie (inne = 0)
                            if (otDone && kv.Key == OtKey) continue;     // 361b: podatek warsztatow wyzej (kolejka albo udzial)
                            if (kv.Key == HoardKey) continue;            // 361d: danina skarbcow wsi nizej (ostatnia doba)
                            if (kv.Key == RateKey) continue;             // 368: raty firm nizej (tylko w dzien raty)
                            bool shown; string lab = Label(kv.Key, out shown);
                            if (shown) continue;
                            long val = kv.Value, med;
                            if (spikes.TryGetValue(kv.Key + "#" + hi, out med)) val = med;   // 368: skok - w sredniej mediana
                            long o; if (!sum.TryGetValue(lab, out o)) order.Add(lab);
                            sum[lab] = o + val;
                        }
                    }
                    foreach (var lab in order)
                    {
                        int a = (int)Math.Round((double)sum[lab] / na);
                        if (a == 0) continue;
                        res.Add(new Corr { Amt = a, Log = lab, Txt = new TextObject("{=!}" + lab + " (" + Avg(na) + ")") });
                    }
                    // 361d (diagnoza 08.11: danina od skarbca wsi - TownFolk.DaWorksRun, ok. 80% dawnego "Dues from your people" - splywa powoli z doby na dobe, a srednia
                    // z 3 dob zostawala w tyle i zawyzala o 200-400): osobna linia, kwota z OSTATNIEJ doby historii (opoznienie 1 doby zamiast ok. 2; srednia wazona
                    // tez by zostawala w tyle przy stalym spadku). Regula regularnosci 347c jak dla innych pozycji.
                    // Recenzja (08.11): w podpowiedzi po takcie doby najswiezsza danina jest juz w _cur (doba w toku) - bierzemy ja; w TickEnd (_inTickEnd) _cur to
                    // wlasnie mierzony takt (porownanie byloby z gory trafione) - wtedy ostatnia doba historii.
                    if (keys.Contains(HoardKey))
                    {
                        long lastV;
                        if (_cur != null && !_inTick && !_inTickEnd && _cur.Laps.ContainsKey(HoardKey)) lastV = _cur.Laps[HoardKey];
                        else _hist[nd - 1].Laps.TryGetValue(HoardKey, out lastV);
                        int a = (int)Math.Max(int.MinValue, Math.Min(int.MaxValue, lastV));
                        bool sh; string lab = Label(HoardKey, out sh);
                        if (a != 0 && !sh) res.Add(new Corr { Amt = a, Log = "danina skarbcow wsi 361d (ostatni takt)", Txt = new TextObject("{=!}" + lab + " (last daily tick)") });
                        diag.Append("; danina skarbcow wsi z ostatniego taktu ").Append(a);
                    }
                }
                if (otDone) keys.Add(OtKey);   // 361b: podatek warsztatow w prognozie zawsze (deterministyczny) - linia dobowa nie liczy go jako nieregularny
                // 368: raty firm miast i zamkow gracza - w sumie tylko w dzien raty (TickEnd: ta doba, podpowiedz: nastepny takt doby); poza suma harmonogram 7 dob
                try
                {
                    _rateInfo = null;
                    if (DsTowns.FarmsLive)
                    {
                        int ahead = _inTickEnd ? 0 : 1, rn = 0;
                        var names = new List<string>();
                        // TickEnd: szacunek z poczatku taktu (po Pay kasa osady i zaleglosci juz inne); podpowiedz: nastepny takt doby
                        long due = _inTickEnd ? Math.Max(0, _rateTick) : DsTowns.PlayerRateDue(ahead, out rn, names);
                        if (_inTickEnd) rn = _rateTickN;
                        if (due > 0)
                        {
                            int a = (int)Math.Min(int.MaxValue, due);
                            res.Add(new Corr { Amt = a, Log = "raty firm 368 (" + rn + " osad)", Txt = new TextObject("{=!}Town and castle rent installments due " + (ahead == 0 ? "today" : "tomorrow")
                                + " (" + string.Join(", ", names.ToArray()) + (rn > names.Count ? ", ..." : "") + ")") });
                        }
                        diag.Append("; raty firm 368 (doba +").Append(ahead).Append(") ").Append(due).Append(" z ").Append(rn).Append(" osad");
                        keys.Add(RateKey);
                        if (!_inTickEnd)
                        {
                            long wk = 0; int next = -1, nn;
                            for (int i = 1; i <= 7; i++) { long x = DsTowns.PlayerRateDue(i, out nn, null); wk += x; if (x > 0 && next < 0) next = i; }
                            if (wk > 0)
                                _rateInfo = "Town and castle rent installments (paid every 7 days, " + (next == 1 ? "next tomorrow" : "next in " + next + " days") + "): about " + Sg(wk)
                                            + " a week, " + Sg((long)Math.Round(wk / 7.0)) + " a day on average (only an installment due tomorrow is in the total)";
                        }
                    }
                }
                catch (Exception e) { _rateInfo = null; Stumble("Corrections(368)", e); }
                _corrKeys = keys;
                _diag361 = diag.ToString();
            }
            catch (Exception e) { Stumble("Corrections", e); }
            _corr = res;
            return res;
        }

        /// <summary>368: mediana (dla parzystej liczby - srednia dwoch srodkowych, zaokraglona). Pusta - 0.</summary>
        private static long Median(List<long> v)
        {
            if (v == null || v.Count == 0) return 0;
            var a = new List<long>(v); a.Sort();
            int m = a.Count / 2;
            return a.Count % 2 == 1 ? a[m] : (long)Math.Round((a[m - 1] + a[m]) / 2.0);
        }

        /// <summary>368: skoki pozycji regularnych w historii (>= 3 dob): klucz "modul#indeks doby" -> mediana modulu z dob historii (doba bez wpisu = 0; korona z innego
        /// krolestwa = 0, jak w sredniej). Skok = |wartosc - mediana| > max(SpikeMin, SpikeX x |mediana|) i to jedyna taka doba modulu w historii. Skok wczorajszej doby
        /// (ostatniej w historii) - do _spikeYest (linia poza suma, tylko poza TickEnd). Nie rzuca.</summary>
        private static Dictionary<string, long> SpikesOf(string kng, HashSet<string> keys, StringBuilder diag)
        {
            var res = new Dictionary<string, long>(StringComparer.Ordinal);
            var yest = new List<KeyValuePair<string, long>>();
            try
            {
                int nd = _hist.Count;
                if (nd >= 3)
                    foreach (var key in keys)
                    {
                        if (key == RateKey || key == OtKey || key == HoardKey) continue;
                        bool sh; string lab = Label(key, out sh);
                        if (sh) continue;
                        var v = new long[nd];
                        for (int i = 0; i < nd; i++)
                        {
                            if (!SameKng(_hist[i], kng) && IsCrownKey(key)) continue;
                            long x; if (_hist[i].Laps.TryGetValue(key, out x)) v[i] = x;
                        }
                        long med = Median(new List<long>(v));
                        long lim = Math.Max(SpikeMin, SpikeX * Math.Abs(med));
                        int hit = -1, nHit = 0;
                        for (int i = 0; i < nd; i++) if (Math.Abs(v[i] - med) > lim) { hit = i; nHit++; }
                        if (nHit != 1) continue;
                        res[key + "#" + hit] = med;
                        diag.Append("; skok 368 ").Append(key).Append(" doba ").Append(_hist[hit].Day).Append(' ').Append(v[hit]).Append(" (mediana ").Append(med).Append(')');
                        if (hit == nd - 1) yest.Add(new KeyValuePair<string, long>(lab, v[hit] - med));
                    }
            }
            catch (Exception e) { Stumble("SpikesOf(368)", e); }
            if (!_inTickEnd) _spikeYest = yest;
            return res;
        }

        /// <summary>SideVillageFief.GoldTipPostfix (podpowiedz zlota: rod gracza, z opisami, bez pobrania): poprawki 345. Nie rzuca.</summary>
        internal static void AddTip(ref ExplainedNumber r, bool details)
        {
            try
            {
                if (!On || Raw) return;
                foreach (var c in Corrections())
                {
                    if (details && c.Details != null && c.Details.Count > 0) { foreach (var x in c.Details) r.Add(x.Key, x.Value); }
                    else r.Add(c.Amt, c.Txt);
                }
            }
            catch (Exception e) { Stumble("AddTip", e); }
            // 361c: linia kontrolna - ile kiesa NAPRAWDE zmieniala sie na dobe (srednia z ostatnich dob ksiegi, wszystko: takze handel, lupy, okupy). Poza suma:
            // linia objasnienia z liczba 0 (ExplainedNumber.Add pomija 0, wiec wprost do objasnienia - wynik i Total bez zmian; liczba w tekscie). Przy < 3 dobach - brak.
            try
            {
                if (!On || Raw) return;
                // 368: raty firm (harmonogram) i jednorazowe skoki wczoraj - poza suma
                if (_rateInfo != null) InfoLine(ref r, _rateInfo);
                foreach (var sp in _spikeYest) InfoLine(ref r, "One-off yesterday: " + sp.Key + " " + Sg(sp.Value) + " (not in the total)");
                int n = 0; long ps = 0;
                var pv = new List<long>();
                for (int i = _hist.Count - 1; i >= 0 && n < Days; i--) { ps += _hist[i].Purse; pv.Add(_hist[i].Purse); n++; }
                if (n < 3) return;
                long avg = (long)Math.Round((double)ps / n);
                // 368: srednia 7 dob zawiera dokladnie jedna rate firmy kazdej osady (cykl 7 dob = Days) - to prawdziwa srednia, ale "zwykla doba" to mediana
                InfoLine(ref r, "Your purse actually changed: typical day " + Sg(Median(pv)) + " (median of the last " + n + " days), average " + Sg(avg)
                                + " a day incl. weekly installments and one-offs (for comparison, not in the total)");
            }
            catch (Exception e) { Stumble("AddTip(361c)", e); }
        }

        private static bool _explDone;
        private static FieldInfo _fExpl;
        private static MethodInfo _mAddLine;
        private static object _opAdd;

        /// <summary>361c: linia objasnienia z liczba 0 (StatExplainer.AddLine) - nie zmienia BaseNumber ani wyniku. Brak pola / metody (inna wersja gry) - nic. Rzuca (wolajacy lapie).</summary>
        private static void InfoLine(ref ExplainedNumber r, string text)
        {
            if (!_explDone)
            {
                _explDone = true;
                _fExpl = AccessTools.Field(typeof(ExplainedNumber), "_explainer");
                var t = _fExpl != null ? _fExpl.FieldType : null;
                _mAddLine = t != null ? AccessTools.Method(t, "AddLine") : null;
                var ot = t != null ? t.GetNestedType("OperationType", BindingFlags.Public | BindingFlags.NonPublic) : null;
                _opAdd = ot != null ? Enum.Parse(ot, "Add") : null;
            }
            if (_fExpl == null || _mAddLine == null || _opAdd == null) return;
            object ex = _fExpl.GetValue(r);   // kopia struktury, ale objasnienie to obiekt (referencja wspolna z r)
            if (ex == null) return;           // bez opisow - nie ma gdzie dopisac
            _mAddLine.Invoke(ex, new object[] { text, 0f, _opAdd });
        }

        /// <summary>Panel Finance ekranu rodu (tylko FinanceBox.In): dodatnie poprawki do przychodu, ujemne do wydatkow. Nie rzuca.</summary>
        internal static void AddBox(ref ExplainedNumber r, bool income)
        {
            try
            {
                if (!On || Raw || !FinanceBox.In) return;
                foreach (var c in Corrections())
                    if (income ? c.Amt > 0 : c.Amt < 0) r.Add(c.Amt, c.Txt);
            }
            catch (Exception e) { Stumble("AddBox", e); }
        }

        // ------------------------------------------------------------ wydatki rodu (panel Finance)
        private static Type _expDecl;
        private static bool _expWired;

        /// <summary>SideVillageFief.EnsureHooks (model finansow kampanii): postfiks CalculateClanExpenses czynnego modelu - ujemne poprawki w panelu Finance.</summary>
        internal static void EnsureExpenseHook(Harmony h, object fin)
        {
            if (h == null || fin == null) return;
            try
            {
                var args = new[] { typeof(Clan), typeof(bool), typeof(bool), typeof(bool) };
                var any = fin.GetType().GetMethod("CalculateClanExpenses", BindingFlags.Public | BindingFlags.Instance, null, args, null);
                var decl = any != null ? any.DeclaringType : null;
                var m = decl != null ? AccessTools.DeclaredMethod(decl, "CalculateClanExpenses", args) : null;
                if (m != null && !m.IsAbstract && decl != _expDecl)
                {
                    h.Patch(m, postfix: new HarmonyMethod(typeof(GoldForecast), nameof(ExpensePostfix)) { priority = Priority.Last });
                    _expDecl = decl; _expWired = true;
                }
                else if (m != null && decl == _expDecl) _expWired = true;
            }
            catch (Exception e) { _expWired = false; Stumble("EnsureExpenseHook", e); }
        }

        public static void ExpensePostfix(object __instance, Clan __0, bool __2, ref ExplainedNumber __result)
        {
            try
            {
                if (__2 || __0 == null || !ReferenceEquals(__0, Clan.PlayerClan) || !FinanceBox.In) return;
                if (Campaign.Current == null || !ReferenceEquals(__instance, Campaign.Current.Models.ClanFinanceModel)) return;
                AddBox(ref __result, false);
            }
            catch (Exception e) { Stumble("ExpensePostfix", e); }
        }

        // ------------------------------------------------------------ etykiety pozycji taktu doby (napisy w grze po angielsku)
        private static readonly string[][] Labels =
        {
            new[] { "DistrictWorks.", "" }, new[] { "SideVillageFief.", "" },   // maja juz linie widoku (prace okregu, wioska poboczna)
            new[] { "TownFolk.DaWorksRun", "Dues from village hoards (lord's share)" },   // 361d: danina od skarbca wsi (DaVillageDue) osobno
            new[] { "DsTowns.FarmRate", "Town and castle rent installments" },   // 368: rata firm co 7 dob osobno (prognoza w dzien raty)
            new[] { "TownFolk.", "Dues from your people (surplus tax, lord's rights)" },
            new[] { "Seal31B.OwnerTaxDaily", "Workshop owner tax to the towns" },
            new[] { "KingdomTreasury.WageRefund", "Wage refund from the crown" },
            new[] { "KingdomTreasury.ScutagePlayer", "Scutage (shield money) to the crown" },   // 355: pobor W6b z kiesy gracza (NoteSub w ScCollect) - nie "Rent from the crown"
            new[] { "KingdomTreasury.", "Dues to the crown" }, new[] { "CrownIncome.", "Dues to the crown" }, new[] { "CrownWar314.", "Dues to the crown" },
            new[] { "CrownCallToWar.", "Dues to the crown" },
            new[] { "CrownRents.", "Rent from the crown" }, new[] { "CrownGifts.", "Gifts from the crown" }, new[] { "CrownBorrow.", "Crown loans" },
            new[] { "IronBank.", "Iron Bank" }, new[] { "DebtLadder.", "Debts and loans" }, new[] { "TownDebt.", "Debts and loans" },
            new[] { "CouncilWages35.", "Council wages" }, new[] { "GentryService.", "Knights' service" },
            new[] { "MercContract.", "Mercenary contract" }, new[] { "MercTenure.", "Mercenary contract" },
            new[] { "ClanBudget.", "Family purses" }, new[] { "SoldierPay.", "Soldiers' pay" },
            new[] { "CompanionPay.", "Companion pay" }, new[] { "CompanionWage.", "Companion pay" },
            new[] { "PopulationLaw.", "Rents from your land" },
            new[] { "CastlePurse.", "Share of town and castle treasuries" }, new[] { "TownPurse.", "Share of town and castle treasuries" }, new[] { "TownToll.", "Share of town and castle treasuries" },
            new[] { "LandSale.", "Land sales" }, new[] { "EstateIncome187.", "Estates" }, new[] { "MineRegale.", "Mine royalties" },
            new[] { "BuildFunding.", "Building projects" }, new[] { "BuildDiary.", "Building projects" },
            new[] { "Sutlers.", "Sutlers" }, new[] { "PlayerFees8.", "Fees" }, new[] { "QuestPay8.", "Quest rewards" }, new[] { "TourneyPay.", "Tournaments" },
            new[] { "Ransom178.", "Ransoms" }, new[] { "RansomFlows.", "Ransoms" }, new[] { "SiegeRansom.", "Ransoms" },
            new[] { "VetPurse.", "Veterans' purses" }, new[] { "CraftOrders8.", "Smithy orders" }, new[] { "CraftOrdersPurse.", "Smithy orders" }, new[] { "ArmouryZb.", "Smithy orders" },
        };

        /// <summary>Etykieta pozycji taktu doby (po angielsku). shown - pozycja ma juz wlasna linie widoku (prace okregu, wioska poboczna): w poprawkach pominieta.</summary>
        internal static string Label(string lap, out bool shown)
        {
            shown = false;
            if (lap != null)
                foreach (var l in Labels)
                    if (lap.StartsWith(l[0], StringComparison.Ordinal))
                    {
                        if (l[1].Length == 0) { shown = true; return "Village works"; }
                        return l[1];
                    }
            return "Other daily dues and payments";
        }

        private static string Name(Settlement st) { try { return st != null && st.Name != null ? st.Name.ToString() : "?"; } catch { return "?"; } }
        private static string Name(Hero h) { try { return h != null && h.Name != null ? h.Name.ToString() : "?"; } catch { return "?"; } }
        private static string Name(Clan c) { try { return c != null && c.Name != null ? c.Name.ToString() : "?"; } catch { return "?"; } }

        // ------------------------------------------------------------ linia dobowa
        private static void Line(DayRec d)
        {
            try
            {
                long t = d.Clan + d.Tick + d.Works;
                // 347: pozycje taktu dzis spoza prognozy (modul nie wszedl do prognozy jako regularna) - rozjazd liczony bez nich, wypisane osobno
                long irr = 0; var irrSb = new StringBuilder();
                if (_fcHave && _fcKeys != null)
                    foreach (var kv in d.Laps)
                    {
                        bool sh; string lab = Label(kv.Key, out sh);
                        if (sh || _fcKeys.Contains(kv.Key)) continue;
                        irr += kv.Value;
                        if (irrSb.Length > 0) irrSb.Append(", ");
                        irrSb.Append(kv.Key).Append(' ').Append(kv.Value >= 0 ? "+" : "").Append(kv.Value).Append(" [").Append(lab).Append(']');
                    }
                var sb = new StringBuilder(P);
                sb.Append("dzien ").Append(d.Day).Append(" | klucz ").Append(On ? "TAK" : "NIE")
                  .Append(" | rozliczenie rodu ").Append(_clanWired ? "wpiete" : "BRAK (" + _clanWhy + ")").Append(", wydatki panelu ").Append(_expWired ? "wpiete" : "BRAK");
                if (_fcHave)
                {
                    long diff = (t - irr) - _fcTotal;
                    double pct = _fcTotal != 0 ? 100.0 * diff / Math.Abs(_fcTotal) : 0;
                    _cmpN++; _cmpAbs += Math.Abs(diff); _cmpAbsT += Math.Abs(t - irr);
                    sb.Append(" | prognoza wczoraj ").Append(_fcTotal).Append(" (model ").Append(_fcModel).Append(", poprawki 345 ").Append(_fcCorr).Append(": ").Append(_fcParts).Append(')')
                      .Append(" | naprawde dzis ").Append(t).Append(" = rozliczenie ").Append(d.Clan).Append(" (").Append(d.ClanN).Append(") + takt doby ").Append(d.Tick)
                      .Append(" + wozy prac ").Append(d.Works)
                      .Append(" | rozjazd ").Append(diff >= 0 ? "+" : "").Append(diff).Append(" (").Append(pct.ToString("0.0", Inv)).Append("%)");
                }
                else
                    sb.Append(" | prognoza wczoraj brak (pierwsza doba po wczytaniu albo klucz wylaczony)")
                      .Append(" | naprawde dzis ").Append(t).Append(" = rozliczenie ").Append(d.Clan).Append(" (").Append(d.ClanN).Append(") + takt doby ").Append(d.Tick)
                      .Append(" + wozy prac ").Append(d.Works).Append(" | rozjazd -");
                sb.Append(" | kiesa naprawde ").Append(d.Purse).Append(" (poza obiegiem ").Append(d.Purse - t).Append(": handel, lupy, okupy, zakupy gracza)");
                if (_fcHave) sb.Append(" | nieregularne poza prognoza ").Append(irr >= 0 ? "+" : "").Append(irr).Append(" (").Append(irrSb.Length > 0 ? irrSb.ToString() : "-").Append(')');
                sb.Append(" | takt doby wedlug modulow: ");
                if (d.Laps.Count == 0) sb.Append('-');
                else
                {
                    var ks = new List<KeyValuePair<string, long>>(d.Laps);
                    ks.Sort((a, b) => Math.Abs(b.Value).CompareTo(Math.Abs(a.Value)));
                    for (int i = 0; i < ks.Count && i < 20; i++)
                    {
                        if (i > 0) sb.Append(", ");
                        bool sh; string lab = Label(ks[i].Key, out sh);
                        sb.Append(ks[i].Key).Append(' ').Append(ks[i].Value >= 0 ? "+" : "").Append(ks[i].Value).Append(" [").Append(lab).Append(sh ? ", wlasna linia" : "").Append(']');
                    }
                    if (ks.Count > 20) sb.Append(", ... (").Append(ks.Count - 20).Append(" wiecej)");
                }
                sb.Append(" | ").Append(_clanDiag ?? "rozliczenie: brak w tej dobie");
                // 361: przewidywane vs naprawde dla nowych pozycji (podatek warsztatow: prognoza z TickEnd dotyczy wyplaty TEJ doby - porownac z kolejka teraz,
                // ktora pobierze nastepny takt; w takcie tej doby pobrano podatek od wyplaty poprzedniej doby)
                try
                {
                    long otNow, hoard; d.Laps.TryGetValue(OtKey, out otNow); d.Laps.TryGetValue(HoardKey, out hoard);
                    int qn; int q = Seal31B.PendingOwnerTax(Hero.MainHero, out qn);
                    sb.Append(" | 361: prognoza ").Append(_fcHave ? (_fc361 ?? "-") : "brak")
                      .Append(" | naprawde: podatek warsztatow w takcie tej doby ").Append(otNow).Append(", kolejka po wyplacie tej doby (pobierze nastepny takt) ")
                      .Append(q >= 0 ? (-q).ToString(Inv) + " (" + qn + " migawek)" : "D11-3 nieczynne")
                      .Append(", danina skarbcow wsi ").Append(hoard)
                      .Append(", raty firm 368 naprawde ").Append(d.Laps.ContainsKey(RateKey) ? d.Laps[RateKey] : 0).Append(" (szacunek z poczatku taktu ").Append(_rateTick).Append(", osad ").Append(_rateTickN).Append(')')
                      .Append(", przyrost tej doby (361e): ").Append(d.GN > 0 ? "warsztaty " + (d.GW / d.GN) + ", wsie " + (d.GV / d.GN) : "brak pomiaru");
                }
                catch (Exception e) { Stumble("Line(361)", e); }
                if (_fcHave)
                    sb.Append(" | korona prognozy (352): krolestwo ").Append(string.IsNullOrEmpty(_fcKng) ? "brak" : _fcKng).Append(", dob historii w tym krolestwie ").Append(_fcKngDays)
                      .Append(", moduly korony w prognozie ").Append(_fcCrownKeys);
                double avgAbs = _cmpN > 0 ? (double)_cmpAbs / _cmpN : 0, avgT = _cmpN > 0 ? (double)_cmpAbsT / _cmpN : 0;
                sb.Append(" | srednio z ").Append(_cmpN).Append(" dob (sesja): |rozjazd| ").Append(avgAbs.ToString("0", Inv)).Append(", |naprawde| ").Append(avgT.ToString("0", Inv))
                  .Append(" | historia ").Append(_hist.Count).Append(" dob | potkniecia ").Append(_stumbles).Append(" (sesja ").Append(_stumblesSess).Append(").");
                Log.Info(sb.ToString());
                _clanDiag = null; _stumbles = 0;
            }
            catch (Exception e) { Stumble("Line", e); }
        }

        // ------------------------------------------------------------ zapis (arm_prog345)
        /// <summary>"g345|1|doba,rozliczenie,n,podglad,warsztaty,wsie_gracza,wsie_innych,reszta,wozy,kiesa,takt[,krolestwo 352]#modul=kwota;...~..." - 7 dob historii. null - brak historii.</summary>
        internal static string Export()
        {
            try
            {
                if (_hist.Count == 0) return null;
                var sb = new StringBuilder("g345|1|");
                for (int i = 0; i < _hist.Count; i++)
                {
                    var d = _hist[i];
                    if (i > 0) sb.Append('~');
                    sb.Append(d.Day).Append(',').Append(d.Clan).Append(',').Append(d.ClanN).Append(',').Append(d.Pre).Append(',').Append(d.WPay).Append(',').Append(d.VMe)
                      .Append(',').Append(d.VOther).Append(',').Append(d.Resid).Append(',').Append(d.Works).Append(',').Append(d.Purse).Append(',').Append(d.Tick);
                    // 352: 12. pole - krolestwo doby ("" - bez krolestwa); stary odczyt bierze pola 0-10 i to pomija; doba nieznana (null) zostaje nieznana
                    if (d.Kng != null) sb.Append(',').Append(d.Kng);
                    // 361e: pola 13-15 - przyrost zapasu (tylko za polem krolestwa; stary odczyt bierze pola 0-11 i reszte pomija)
                    if (d.Kng != null && d.GN > 0) sb.Append(',').Append(d.GN).Append(',').Append(d.GW).Append(',').Append(d.GV);
                    sb.Append('#');
                    bool first = true;
                    foreach (var kv in d.Laps) { if (!first) sb.Append(';'); first = false; sb.Append(kv.Key).Append('=').Append(kv.Value); }
                }
                return sb.ToString();
            }
            catch (Exception e) { Stumble("Export", e); return null; }
        }

        internal static void Import(string s)
        {
            _hist.Clear(); _stamp++;
            if (string.IsNullOrEmpty(s) || !s.StartsWith("g345|1|", StringComparison.Ordinal)) return;
            try
            {
                foreach (var rec in s.Substring(7).Split('~'))
                {
                    try
                    {
                        int h = rec.IndexOf('#');
                        var f = (h >= 0 ? rec.Substring(0, h) : rec).Split(',');
                        if (f.Length < 11) continue;
                        var d = new DayRec
                        {
                            Day = int.Parse(f[0], Inv), Clan = long.Parse(f[1], Inv), ClanN = int.Parse(f[2], Inv), Pre = long.Parse(f[3], Inv), WPay = long.Parse(f[4], Inv),
                            VMe = long.Parse(f[5], Inv), VOther = long.Parse(f[6], Inv), Resid = long.Parse(f[7], Inv), Works = long.Parse(f[8], Inv), Purse = long.Parse(f[9], Inv),
                            Tick = long.Parse(f[10], Inv),
                            Kng = f.Length > 11 ? f[11] : null   // 352: brak pola - zapis sprzed 352 (nieznane)
                        };
                        if (f.Length > 14)   // 361e: przyrost zapasu (brak - zapis sprzed 361, doba bez pomiaru)
                        {
                            int gn; long gw, gv;
                            if (int.TryParse(f[12], NumberStyles.Integer, Inv, out gn) && long.TryParse(f[13], NumberStyles.Integer, Inv, out gw)
                                && long.TryParse(f[14], NumberStyles.Integer, Inv, out gv) && gn > 0) { d.GN = gn; d.GW = gw; d.GV = gv; }
                        }
                        if (h >= 0 && h + 1 < rec.Length)
                            foreach (var kv in rec.Substring(h + 1).Split(';'))
                            {
                                int e = kv.LastIndexOf('=');
                                long a;
                                if (e > 0 && long.TryParse(kv.Substring(e + 1), NumberStyles.Integer, Inv, out a)) d.Laps[kv.Substring(0, e)] = a;
                            }
                        _hist.Add(d);
                    }
                    catch (Exception e) { Stumble("Import(doba)", e); }
                }
                while (_hist.Count > Days) _hist.RemoveAt(0);
            }
            catch (Exception e) { Stumble("Import", e); }
        }
    }
}
