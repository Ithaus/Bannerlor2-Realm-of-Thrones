using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// n20-wioska (noc 19/20.10) - WIOSKA POBOCZNA GRACZA. Jeff: "czy mozemy naprawic to, ze zaczynam jako lord jakiejs wioski pobocznej! Nie wioski glownej tylko pobocznej,
    /// jest ona moja i daje mi jakis dochod ... moj herb przy niej ... moge wjechac i dostac tylko dla tej wioski opcje, jak nocleg, inwestuj, rozwijaj".
    ///  W1 START (klucz SideVillageFiefStart): start BK "Indebted Lord" (DefaultStartOptions.IndebtedLord) wola TitleManager.GiveLordshipOnKingdomJoin(krolestwo, rod gracza,
    ///     force: true) - JEDYNE wywolanie tej metody w BK (dekompilacja BannerKings.dll: grep). BK daje wtedy tytul Lordship najmniejszej wsi gry krola (deJure i deFacto;
    ///     sama wies gry zostaje przy zamku - ChangeOwnerOfSettlementAction tylko dla miast i zamkow). Prefiks: dla rodu gracza tytul pominiety (return false), gracz
    ///     dostaje WIOSKE POBOCZNA (wiersz ukladu wiosek arm_map_villages, nie przysiolek) w okregu wsi gry z TEGO krolestwa, najblizej partii gracza; wies gry zostaje
    ///     przy panu. Wioska stoi w ksiedze wiosek (stan stoi / uszkodzona). Brak kandydata (ksiega nieczynna) - proba co godzine, po 3 dobach tytul BK jak dotad.
    ///  W2 ZAPIS: arm_side_fief (SaveText): "sf1|uid|okreg|nazwa|doba|wyplacone|srednia". Brak klucza (stary zapis Jeffa) = brak wioski gracza.
    ///  W3 DOCHOD (klucz SideVillageFiefIncome) - r4-wioska (20.10, dotad przy wozie: u Jeffa i w autotescie 5 dob "do gracza 0"): CO DOBE (OnDailyTick, Pay) naleznosc
    ///     wioski = wartosc dnia jej produkcji (VillageCrafts.PartsOf) x stawka pana z utargu (czynny model podatku wsi), z licznika podatku pana wsi gry okregu
    ///     (TradeTaxAccumulated), reszta z kiesy pana - pan ma mniej dokladnie o tyle. Wojna z krolestwem okregu, gracz panem okregu, okreg rabowany, wioska spalona -
    ///     bez przelewu (powod w menu i w zakladce Fiefs). Podpowiedz zlota (CalculateClanGoldChange z opisami, bez pobrania) - linia z naleznoscia dnia (tylko widok).
    ///     Zakladka Fiefs ekranu rodu - SideVillageFiefUi (lista) i NoFiefsPostfix (tekst bez miast i zamkow).
    ///  W4 MAPA: VillageHolders.Of - gospodarzem tej wioski jest gracz (herb rodu gracza na proporczyku i w dymku, linia "This village is yours").
    ///  W5 MENU (klucz SideVillageFiefMenu): klik na wioske na mapie = jazda do niej (MapState.ProcessTravel, wzor zajazdow) i menu po dojechaniu; postoj przy niej - pytanie
    ///     raz na postoj; w menu wsi gry okregu "Visit X (your village)". Menu arm_side_fief (zwykle, nie oczekiwania): nocleg za darmo - pod dachem (recenzja: klucz
    ///     SideVillageFiefLodging, TownInns.HostSleep - stan noclegu jak wspolna izba karczmy: czekanie do switu i premia "wypoczeci"; bez klucza, karczm, w wojnie z krolestwem
    ///     okregu, przy rabunku albo spalonej wiosce - sen jak w obozie, podpowiedz mowi dlaczego), prace okregu tej wioski (DistrictWorks.OpenPartFor; opcja wylaczona z powodem,
    ///     gdy prac nie da sie kupic), przeglad, wyjazd.
    ///  Furtka autotestu: DLL autotestu CrashScribe (LogGate.Mode 1/2) + plik Documents\Mount and Blade II Bannerlord\CrashScribe\armoury-wioska-gracza.flag - w pierwszej
    ///     godzinie z czynna ksiega wiosek proba wola TE SAMA metode BK (odbiciem, force: true) dla krolestwa najblizszej wsi - prefiks, nadanie, herb, zapis, menu - linia "test".
    /// Linie logu: "Wioska gracza (n20): ..." (start sesji, nadanie, doba, test). Bledy liczone (potkniecia), nic nie gasimy globalnie.
    /// </summary>
    internal static class SideVillageFief
    {
        private const string P = "Wioska gracza (n20): ";
        internal const string Menu = "arm_side_fief";
        internal const string OptVillage = "arm_side_fief_visit";
        private const string SaveVer = "sf1", PendVer = "sfp";   // sfp|krolestwo|doba - nadanie startu BK jeszcze czeka
        internal const string AutoFlagName = "armoury-wioska-gracza.flag";
        private const float EnterR = 1.6f, LeaveR = 3.5f, ArriveEps = 0.25f;
        private const int FallbackDays = 3;
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        // ------------------------------------------------------------ stan kampanii (zapis arm_side_fief)
        private static string _uid, _dist, _name;
        private static int _day0 = -1;
        private static long _paidAll;
        private static double _avg;
        // oczekujace nadanie (start BK przed czynna ksiega) - w zapisie jako "sfp|krolestwo|doba" (Export/Import); po wczytaniu proba co godzine (TryPending),
        // po FallbackDays bez kandydata - tytul BK jak dotad
        private static string _pendKingdom; private static int _pendDay = -1; private static string _pendHow;

        // ------------------------------------------------------------ sesja
        private static Harmony _harmony;
        private static bool _bkTried, _bkWired, _finWired, _bypass, _startLogged, _testDone;
        private static MethodInfo _giveLordship;
        private static object _finTriedFor;
        private static Type _finDecl;
        private static int _st, _stDay;
        private static readonly HashSet<string> _err = new HashSet<string>(StringComparer.Ordinal);
        private static VillageHolders.Holder _holder;
        private static Clan _holderLord; private static Banner _holderBanner;
        private static int _shareDay = -1; private static double _share; private static string _shareNote = "-";
        private static float _px, _py; private static bool _posSet;
        private static string _pendingClick, _askedUid;
        private static long _lastCheck;
        private static TextObject _tipTxt; private static string _tipName;
        // liczniki doby
        private static int _dCarts, _dWar, _dSelf, _dNoShare, _dIntercept, _dOpen, _dClick, _dAsk, _dRest, _dRoof, _dInvest, _dTip;
        // recenzja (drobne): czy prace tej wioski da sie dzis kupic - wycena DistrictWorks.QuotesOf raz na godzine gry (warunek opcji menu, nie co klatke)
        private static int _invHour = -1; private static string _invUid; private static bool _invOk; private static string _invWhy;
        private static long _dPaid, _dTax;

        internal static void SetHarmony(Harmony h) { _harmony = h; }

        /// <summary>Konstruktor ArmouryBehavior (kazda kampania): stan od zera (zapis wczyta Import).</summary>
        internal static void Reset()
        {
            _uid = _dist = _name = null; _day0 = -1; _paidAll = 0; _avg = 0;
            _pendKingdom = null; _pendDay = -1; _pendHow = null;
            _startLogged = _testDone = false; _bypass = false;
            _st = _stDay = 0; lock (_err) _err.Clear();
            _holder = null; _holderLord = null; _holderBanner = null;
            _shareDay = -1; _share = 0; _shareNote = "-";
            _posSet = false; _pendingClick = _askedUid = null;
            _invHour = -1; _invUid = null; _invOk = false; _invWhy = null;
            _dueDay = -1; _due = 0; _dueCode = 0; _dueWhy = ""; _dueWhyPl = "-"; _mineVal = _allVal = _rate = 0;   // r4-wioska
            DayZero();
            SideVillageHome.Reset();   // r4-dom: dom, skrzynia, handel, spacer
        }

        private static void DayZero() { _dCarts = _dWar = _dSelf = _dNoShare = _dIntercept = _dOpen = _dClick = _dAsk = _dRest = _dRoof = _dInvest = _dTip = 0; _dPaid = _dTax = 0; _dFromTax = _dFromLord = _dShort = 0; _stDay = 0; }

        private static void Stumble(string where, Exception e)
        {
            _st++; _stDay++;
            bool first; lock (_err) first = _err.Add(where);
            if (first || _st % 200 == 0) Log.Error("SideVillageFief." + where + " (potkniecie " + _st + ")", e);
        }

        private static int Today() { return (int)CampaignTime.Now.ToDays; }

        internal static bool HasFief { get { return _uid != null && _dist != null; } }

        // r4-dom (SideVillageHome): odczyt wioski gracza - uid, wies gry okregu, nazwa
        internal static string Uid { get { return _uid; } }
        internal static string DistId { get { return _dist; } }
        internal static string Name { get { return _name; } }

        /// <summary>Wioska gracza o tym uid (widok mapy, gospodarz, dymek). Sam odczyt.</summary>
        internal static bool IsMine(string uid) { return uid != null && _uid != null && string.Equals(uid, _uid, StringComparison.Ordinal); }

        /// <summary>Widok mapy: wioska gracza z menu (klik, kursor raczki).</summary>
        internal static bool Clickable(string uid) { var s = Settings.Current; return s != null && s.SideVillageFiefMenu && IsMine(uid); }

        // ============================================================ zapis
        internal static string Export()
        {
            if (!HasFief) return _pendKingdom != null ? PendVer + "|" + Clean(_pendKingdom) + "|" + _pendDay.ToString(Inv) : "";   // nadanie czeka (ksiega nieczynna) - przezyje zapis
            return SaveVer + "|" + Clean(_uid) + "|" + Clean(_dist) + "|" + Clean(_name) + "|" + _day0.ToString(Inv) + "|" + _paidAll.ToString(Inv) + "|" + _avg.ToString("0.###", Inv);
        }

        private static string Clean(string s) { return (s ?? "").Replace("|", "/"); }

        internal static void Import(string data)
        {
            _uid = _dist = _name = null; _day0 = -1; _paidAll = 0; _avg = 0;
            if (string.IsNullOrEmpty(data)) return;
            try
            {
                var f = data.Split('|');
                if (f.Length >= 3 && f[0] == PendVer && f[1].Length > 0)
                {
                    _pendKingdom = f[1]; _pendHow = "start BK (Indebted Lord), po wczytaniu";
                    if (!int.TryParse(f[2], NumberStyles.Integer, Inv, out _pendDay)) _pendDay = Today();
                    return;
                }
                if (f.Length < 7 || f[0] != SaveVer || f[1].Length == 0 || f[2].Length == 0) { Log.Info(P + "zapis arm_side_fief nieznany (" + data.Length + " znakow) - pominiety, brak wioski gracza."); return; }
                _uid = f[1]; _dist = f[2]; _name = f[3].Length > 0 ? f[3] : f[1];
                int.TryParse(f[4], NumberStyles.Integer, Inv, out _day0);
                long.TryParse(f[5], NumberStyles.Integer, Inv, out _paidAll);
                double.TryParse(f[6], NumberStyles.Float, Inv, out _avg);
                if (double.IsNaN(_avg) || _avg < 0) _avg = 0;
            }
            catch (Exception e) { _uid = _dist = null; Stumble("Import", e); }
        }

        // ============================================================ latki (OnSessionLaunched - kampania istnieje)
        internal static void EnsureHooks()
        {
            var h = _harmony;
            if (h == null || Campaign.Current == null) return;
            if (!_bkTried)
            {
                _bkTried = true;
                try
                {
                    var t = AccessTools.TypeByName("BannerKings.Managers.TitleManager");
                    _giveLordship = t != null ? AccessTools.DeclaredMethod(t, "GiveLordshipOnKingdomJoin", new[] { typeof(Kingdom), typeof(Clan), typeof(bool) }) : null;
                    if (_giveLordship != null)
                    {
                        h.Patch(_giveLordship, prefix: new HarmonyMethod(typeof(SideVillageFief), nameof(GiveLordshipPrefix)) { priority = Priority.First });
                        _bkWired = true;
                    }
                }
                catch (Exception e) { _bkWired = false; Stumble("EnsureHooks(BK)", e); }
                // przeglad calosci 20.10: okno wyboru startu BK obiecuje przy "Indebted Lord" tytul Lordship ({=CiWCMaQV}), a przy kluczu jest wioska poboczna.
                // BK odtwarza opcje startu w swoim OnSessionLaunched (DefaultStartOptions.Initialize - kolejnosc wobec nas nieznana), wiec nie podmieniamy obiektu,
                // tylko wynik gettera StartOptionVM.ShortDescription (czyta go wiazanie okna przy pokazaniu, po OnSessionLaunched; metoda z wywolaniami, nie 1-bajtowy ret)
                try
                {
                    var vm = AccessTools.TypeByName("BannerKings.UI.CampaignStart.StartOptionVM");
                    var g = vm != null ? AccessTools.DeclaredPropertyGetter(vm, "ShortDescription") : null;
                    _vmOption = vm != null ? AccessTools.DeclaredPropertyGetter(vm, "Option") : null;
                    if (g != null && _vmOption != null) { h.Patch(g, postfix: new HarmonyMethod(typeof(SideVillageFief), nameof(StartTextPostfix))); _startTxtWired = true; }
                }
                catch (Exception e) { _startTxtWired = false; Stumble("EnsureHooks(opis startu BK)", e); }
                // r4-wioska: prawa czesc zakladki Fiefs ("Clan Does Not Own Any Fiefs") - NoFiefsText po RefreshValues ClanFiefsVM (metoda klasy bazowej - takze NavalClanFiefsVM,
                // ktory jej nie nadpisuje); bez UIExtenderEx, wiec dziala takze bez wstawki listy (SideVillageFiefUi)
                try
                {
                    var rv = AccessTools.DeclaredMethod(typeof(TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement.Categories.ClanFiefsVM), "RefreshValues");
                    if (rv != null) { h.Patch(rv, postfix: new HarmonyMethod(typeof(SideVillageFief), nameof(NoFiefsPostfix))); _noFiefsWired = true; }
                }
                catch (Exception e) { _noFiefsWired = false; Stumble("EnsureHooks(Fiefs)", e); }
            }
            var fin = Campaign.Current.Models != null ? Campaign.Current.Models.ClanFinanceModel : null;
            if (fin != null && !ReferenceEquals(fin, _finTriedFor))
            {
                _finTriedFor = fin;
                try
                {
                    var args = new[] { typeof(Clan), typeof(bool), typeof(bool), typeof(bool) };
                    var any = fin.GetType().GetMethod("CalculateClanGoldChange", BindingFlags.Public | BindingFlags.Instance, null, args, null);
                    var decl = any != null ? any.DeclaringType : null;
                    var m = decl != null ? AccessTools.DeclaredMethod(decl, "CalculateClanGoldChange", args) : null;
                    if (m != null && !m.IsAbstract && decl != _finDecl)
                    {
                        h.Patch(m, postfix: new HarmonyMethod(typeof(SideVillageFief), nameof(GoldTipPostfix)) { priority = Priority.Last });
                        _finDecl = decl; _finWired = true;
                    }
                    else if (m != null && decl == _finDecl) _finWired = true;
                }
                catch (Exception e) { _finWired = false; Stumble("EnsureHooks(model finansow)", e); }
                // r4-wioska (prowadzacy): panel Finance ekranu rodu liczy "Total Income" z CalculateClanIncome (ClanManagementVM.RefreshDailyValues) - ta sama
                // naleznosc dnia tylko do widoku, zeby przychod i "Daily Change" zgadzaly sie z podpowiedzia zlota
                try
                {
                    var args = new[] { typeof(Clan), typeof(bool), typeof(bool), typeof(bool) };
                    var any = fin.GetType().GetMethod("CalculateClanIncome", BindingFlags.Public | BindingFlags.Instance, null, args, null);
                    var decl = any != null ? any.DeclaringType : null;
                    var m = decl != null ? AccessTools.DeclaredMethod(decl, "CalculateClanIncome", args) : null;
                    if (m != null && !m.IsAbstract && decl != _incDecl)
                    {
                        h.Patch(m, postfix: new HarmonyMethod(typeof(SideVillageFief), nameof(IncomeViewPostfix)) { priority = Priority.Last });
                        _incDecl = decl; _incWired = true;
                    }
                    else if (m != null && decl == _incDecl) _incWired = true;
                }
                catch (Exception e) { _incWired = false; Stumble("EnsureHooks(przychod rodu)", e); }
            }
        }

        private static Type _incDecl;
        private static bool _incWired;

        /// <summary>r4-wioska (prowadzacy): postfiks CalculateClanIncome czynnego modelu - rod gracza, BEZ pobrania (applyWithdrawals = false): naleznosc dnia wioski
        /// (ta sama co w podpowiedzi zlota). Tylko widok: pieniadze ida z Pay w OnDailyTick; dzienny przelew gry idzie przez CalculateClanGoldChange z pobraniem,
        /// nie przez ta metode, wiec nie ma podwojnej wyplaty.</summary>
        // m105-optym KtKnownPatches (KingdomTreasury.Optim105.cs): przy applyWithdrawals = false tylko odczyt, bez MBRandom; zmiana tresci = przeglad KtSkip
        public static void IncomeViewPostfix(object __instance, Clan __0, bool __1, bool __2, ref ExplainedNumber __result)
        {
            try
            {
                if (__2 || __0 == null || !HasFief || !ReferenceEquals(__0, Clan.PlayerClan)) return;
                var s = Settings.Current;
                if (s == null || !s.SideVillageFiefIncome || Campaign.Current == null || !ReferenceEquals(__instance, Campaign.Current.Models.ClanFinanceModel)) return;
                string why; long due;
                float x = Expect(out why, out due);
                if (x < 1f) return;
                if (_tipTxt == null || !ReferenceEquals(_tipName, _name)) { _tipName = _name; _tipTxt = new TextObject("{=!}" + (_name ?? "?") + " (your side village: its share of the lord's tax)"); }
                __result.Add(x, _tipTxt);
            }
            catch (Exception e) { Stumble("IncomeViewPostfix", e); }
        }

        private static MethodInfo _vmOption;
        private static bool _startTxtWired, _noFiefsWired;

        /// <summary>r4-wioska: postfiks ClanFiefsVM.RefreshValues - przy wioskce gracza tekst prawej czesci zakladki Fiefs (widoczny tylko bez miast i zamkow) mowi o niej
        /// zamiast "Clan Does Not Own Any Fiefs". Klucz SideVillageFiefClanTab. Tylko napis - przy bledzie zostaje tekst gry.</summary>
        public static void NoFiefsPostfix(TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement.Categories.ClanFiefsVM __instance)
        {
            try
            {
                var s = Settings.Current;
                if (__instance == null || s == null || !s.SideVillageFiefClanTab || !HasFief) return;
                string t = NoFiefsText();
                if (t != null) __instance.NoFiefsText = t;
            }
            catch (Exception e) { Stumble("NoFiefsPostfix", e); }
        }
        private const string StartText = "Start as a lord in a kingdom, holding one of its side villages - it flies your banner and pays you a share of the lord's tax. Reduced influence for 5 years. Gain Scholarship skill.";

        /// <summary>Postfiks StartOptionVM.ShortDescription (BK): opcja "start_lord" przy kluczu SideVillageFiefStart - opis wioski pobocznej zamiast tytulu Lordship.</summary>
        public static void StartTextPostfix(object __instance, ref string __result)
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !s.SideVillageFiefStart || __instance == null || _vmOption == null) return;
                var so = _vmOption.Invoke(__instance, null) as TaleWorlds.ObjectSystem.MBObjectBase;
                if (so != null && so.StringId == "start_lord") __result = StartText;
            }
            catch { }   // tylko napis - przy bledzie zostaje tekst BK
        }

        // ============================================================ W1: start BK "Indebted Lord"
        /// <summary>Prefiks BK TitleManager.GiveLordshipOnKingdomJoin(krolestwo, rod, force): dla rodu gracza (przy kluczu) bez tytulu wsi gry - wioska poboczna.</summary>
        public static bool GiveLordshipPrefix(Kingdom __0, Clan __1, bool __2)
        {
            if (_bypass) return true;
            try
            {
                var s = Settings.Current;
                if (s == null || !s.SideVillageFiefStart || __0 == null || __1 == null || !ReferenceEquals(__1, Clan.PlayerClan)) return true;
                if (HasFief) { Log.Info(P + "BK GiveLordshipOnKingdomJoin dla gracza przy wiosce juz nadanej (" + _name + ") - tytul BK pominiety."); return false; }
                _dIntercept++;
                _pendKingdom = __0.StringId; _pendDay = Today(); _pendHow = "start BK (Indebted Lord)";
                Log.Info(P + "BK GiveLordshipOnKingdomJoin(" + __0.StringId + ", gracz, force " + __2 + ") - tytul Lordship wsi gry POMINIETY (wies gry zostaje przy panu), gracz dostanie wioske poboczna krolestwa " + Txt(__0.Name) + ".");
                TryPending();
                // recenzja (drobne): wyjatek w nadaniu (TryPending lapie go i kasuje oczekiwanie) - bez wioski i bez czekania; wtedy tytul BK jak dotad, nie nic
                if (!HasFief && _pendKingdom == null)
                {
                    Log.Info(P + "nadanie wioski pobocznej nie powiodlo sie (blad, linia wyzej) - tytul BK jak dotad.");
                    return true;
                }
                return false;
            }
            catch (Exception e) { Stumble("GiveLordshipPrefix", e); return true; }   // blad - BK jak dotad
        }

        /// <summary>Oczekujace nadanie: kandydat z ksiegi wiosek; po FallbackDays bez kandydata - tytul BK jak dotad (to samo wywolanie z obejsciem prefiksu).</summary>
        private static void TryPending()
        {
            if (_pendKingdom == null) return;
            try
            {
                var k = Kingdom.All != null ? FindKingdom(_pendKingdom) : null;
                string why;
                bool at = LogGate.Mode == 1 || LogGate.Mode == 2;     // autotest CrashScribe: bez okna (nikt go nie kliknie) i bez wioski w wojnie z graczem
                if (k != null && Grant(k, _pendHow ?? "start", at, out why)) { _pendKingdom = null; return; }
                if (k == null) why = "krolestwo " + _pendKingdom + " nie istnieje";
                else why = "brak kandydata";
                if (Today() - _pendDay < FallbackDays) return;
                string kid = _pendKingdom;
                _pendKingdom = null;
                if (k != null && _giveLordship != null)
                {
                    Log.Info(P + "brak wioski pobocznej w krolestwie " + kid + " przez " + FallbackDays + " doby (" + why + ") - tytul BK jak dotad (GiveLordshipOnKingdomJoin z obejsciem).");
                    InvokeBk(k, true);
                }
                else Log.Info(P + "nadanie przepadlo (" + why + ").");
            }
            catch (Exception e) { _pendKingdom = null; Stumble("TryPending", e); }
        }

        private static Kingdom FindKingdom(string id)
        {
            foreach (var k in Kingdom.All) if (k != null && k.StringId == id) return k;
            return null;
        }

        private static bool InvokeBk(Kingdom k, bool bypass)
        {
            try
            {
                var cfgT = AccessTools.TypeByName("BannerKings.BannerKingsConfig");
                var inst = cfgT != null ? AccessTools.Property(cfgT, "Instance") : null;
                object cfg = inst != null ? inst.GetValue(null, null) : null;
                object tm = null;
                if (cfg != null)
                {
                    var f = AccessTools.Field(cfgT, "TitleManager");
                    if (f != null) tm = f.GetValue(cfg);
                    else { var p = AccessTools.Property(cfgT, "TitleManager"); if (p != null) tm = p.GetValue(cfg, null); }
                }
                if (tm == null || _giveLordship == null) return false;
                _bypass = bypass;                                  // obejscie prefiksu tylko dla zapasu "tytul BK jak dotad"; proba autotestu idzie przez prefiks
                _giveLordship.Invoke(tm, new object[] { k, Clan.PlayerClan, true });
                return true;
            }
            catch (Exception e) { Stumble("InvokeBk", e); return false; }
            finally { _bypass = false; }
        }

        /// <summary>Wybor i nadanie: wioska poboczna (nie przysiolek; w drugiej kolejce takze przysiolek i wioska uszkodzona) w okregu wsi gry, ktorej pan (Bound.OwnerClan)
        /// nie jest graczem i nalezy do krolestwa k - najblizej partii gracza. test: bez wojny z graczem.</summary>
        private static bool Grant(Kingdom k, string how, bool test, out string why)
        {
            why = "";
            if (!VillageBook.Active) { why = "ksiega wiosek nieczynna"; return false; }
            var recs = VillageBook.LayoutRecords();
            if (recs == null || recs.Count == 0) { why = "brak ukladu wiosek"; return false; }
            var mp = MobileParty.MainParty;
            if (mp == null) { why = "brak partii gracza"; return false; }
            Vec2 pos = mp.GetPosition2D;
            MapVillageRecord best = null; Settlement bestSt = null; float bestD = float.MaxValue;
            int seen = 0;
            // recenzja (drobne): w nowej kampanii n okregu liczy sie leniwie (EnsureN), a StateOf zwraca false dla okregu bez n - lista kandydatow przy "Finish" bywala
            // niepelna; VillageBook.PartsOf wola EnsureN (raz na okreg, wynik w slowniku tego wyboru)
            var parts = new Dictionary<string, List<VillageBook.PartView>>(StringComparer.Ordinal);
            for (int pass = 0; pass < 2 && best == null; pass++)
            {
                foreach (var r in recs)
                {
                    if (r == null || string.IsNullOrEmpty(r.Uid) || string.IsNullOrEmpty(r.VillageId)) continue;
                    if (pass == 0 && !string.IsNullOrEmpty(r.Hamlet)) continue;     // przysiolek na pustkowiu - dopiero w drugiej kolejce
                    var st = Settlement.Find(r.VillageId);
                    if (st == null || !st.IsVillage || st.Village == null) continue;
                    var bound = st.Village.Bound;
                    var lord = bound != null ? bound.OwnerClan : null;
                    if (lord == null || ReferenceEquals(lord, Clan.PlayerClan) || lord.IsEliminated) continue;
                    if (!ReferenceEquals(lord.Kingdom, k) && !ReferenceEquals(st.MapFaction, k)) continue;
                    if (test && Clan.PlayerClan.MapFaction != null && st.MapFaction != null && FactionManager.IsAtWarAgainstFaction(st.MapFaction, Clan.PlayerClan.MapFaction)) continue;
                    int state;
                    if (!PartState(st.Village, r.VillageId, r.Uid, parts, out state)) continue;
                    if (state != VillageBook.SStanding && (pass == 0 || state != VillageBook.SDamaged)) continue;
                    seen++;
                    float dx = r.X - pos.x, dy = r.Y - pos.y, d = dx * dx + dy * dy;
                    if (d < bestD) { bestD = d; best = r; bestSt = st; }
                }
            }
            if (best == null) { why = "brak wioski stojacej w okregach krolestwa " + k.StringId + " (okregow z wioskami sprawdzonych " + seen + ")"; return false; }
            _uid = best.Uid; _dist = best.VillageId; _name = string.IsNullOrEmpty(best.Name) ? best.Uid : best.Name;
            _day0 = Today(); _paidAll = 0; _avg = 0;
            _px = best.X; _py = best.Y; _posSet = true;
            _holder = null; _shareDay = -1; _dueDay = -1;
            var lordC = bestSt.Village.Bound != null ? bestSt.Village.Bound.OwnerClan : null;
            Log.Info(P + "NADANIE (" + how + ") - " + _name + " (" + _uid + "), okreg " + Txt(bestSt.Name) + " (" + _dist + "), pan " + (lordC != null ? Txt(lordC.Name) : "?")
                     + ", krolestwo " + k.StringId + ", " + Math.Sqrt(bestD).ToString("0.0", Inv) + " jedn. od partii gracza, kandydatow " + seen + "; wies gry zostaje przy panu.");
            if (!test)
            {
                string house = lordC != null ? MapVillagesView.HouseName(lordC) : "your liege";
                string body = "You hold " + _name + ", a side village in the lands of " + Txt(bestSt.Name) + ", sworn to " + house + ". " + Txt(bestSt.Name)
                              + " itself stays with its lord. Your village flies your banner, and every day its share of the lord's tax comes to you. "
                              + "Click it on the map to ride in: rest there, invest in its works or look it over.";
                Log.Player(_name + " is yours now - a side village of " + Txt(bestSt.Name) + ".");
                // okno jak zawiadomienie BK o nadaniu (BK pokazuje je w tym samym miejscu bez sprawdzania innych okien)
                try { InformationManager.ShowInquiry(new InquiryData("Your village", body, true, false, "Done", null, null, null), true); } catch (Exception e) { Stumble("Grant.Okno", e); }
            }
            return true;
        }

        /// <summary>Stan wioski pobocznej uid w okregu wsi gry v (VillageBook.PartsOf - z EnsureN; jeden odczyt na okreg w slowniku cache). false - ksiega jej nie zna.</summary>
        private static bool PartState(Village v, string dist, string uid, Dictionary<string, List<VillageBook.PartView>> cache, out int state)
        {
            state = -1;
            List<VillageBook.PartView> pv;
            if (!cache.TryGetValue(dist, out pv))
            {
                pv = new List<VillageBook.PartView>();
                if (!VillageBook.PartsOf(v, pv)) pv.Clear();
                cache[dist] = pv;
            }
            foreach (var p in pv) if (p != null && !p.Main && string.Equals(p.Uid, uid, StringComparison.Ordinal)) { state = p.State; return true; }
            return false;
        }

        private static string Txt(TextObject t) { try { return t != null ? t.ToString() : "?"; } catch { return "?"; } }

        // ============================================================ W3: wozy okregu (po r4-wioska tylko licznik - dochod idzie co dobe, Pay)
        /// <summary>VillageTakings.EnterPostfix (woz wrocil do wsi macierzystej; po 112, F4b i pracach okregu): r4-wioska - BEZ przelewu (dochod plynie co dobe z Pay,
        /// inaczej podwojnie), tylko licznik wozow okregu i podatku pana z ich okien do linii doby. Zwraca 0 (DsDues: udzial gracza przy wozie = 0).</summary>
        internal static long AtCart(MobileParty cart, Village home, long taxInWindow)
        {
            if (!HasFief || home == null || home.Settlement == null) return 0;
            try
            {
                if (!string.Equals(home.Settlement.StringId, _dist, StringComparison.Ordinal)) return 0;
                _dCarts++;
                if (taxInWindow > 0) _dTax += taxInWindow;
            }
            catch (Exception e) { Stumble("AtCart", e); }
            return 0;
        }

        /// <summary>Udzial wioski gracza w wartosci dnia produkcji okregu (VillageCrafts.PartsOf - te same wartosci co okno prac); raz na dobe. Zapas - udzial ludzi w domu.
        /// r4-wioska: zapamietuje tez wartosc dnia wioski i okregu (_mineVal, _allVal) - podstawa naleznosci dnia (Due).</summary>
        private static double ShareOf(Village v)
        {
            int today = Today();
            if (_shareDay == today) return _share;
            _shareDay = today; _share = 0; _mineVal = 0; _allVal = 0;
            try
            {
                var parts = new List<VillageCrafts.PartCraft>();
                bool ks;
                if (VillageCrafts.PartsOf(v, parts, out ks))
                {
                    double all = 0, mine = 0, hAll = 0, hMine = 0;
                    foreach (var p in parts)
                    {
                        double val = Math.Max(0, p.ValDay), home = p.Book != null ? Math.Max(0, p.Book.Home) : 0;
                        if (double.IsNaN(val) || double.IsInfinity(val)) val = 0;
                        all += val; hAll += home;
                        if (!p.Main && IsMine(p.Uid)) { mine += val; hMine += home; }
                    }
                    _allVal = all; _mineVal = mine;
                    if (all > 0 && mine > 0) { _share = Math.Min(1.0, mine / all); _shareNote = "wartosc dnia " + mine.ToString("0", Inv) + " z " + all.ToString("0", Inv) + (ks ? " (KS)" : ""); }
                    else if (hAll > 0 && hMine > 0) { _share = Math.Min(1.0, hMine / hAll); _shareNote = "ludzie w domu " + hMine.ToString("0", Inv) + " z " + hAll.ToString("0", Inv); }
                    else _shareNote = "wioska bez produkcji i ludzi w ksiedze";
                }
                else _shareNote = "okreg nieznany ksiedze";
            }
            catch (Exception e) { _share = 0; _mineVal = _allVal = 0; _shareNote = "blad"; Stumble("ShareOf", e); }
            return _share;
        }

        // ============================================================ r4-wioska: dochod co dobe
        private static double _mineVal, _allVal, _rate, _hand;
        private static int _dueDay = -1, _dueCode, _dueSig; private static long _due; private static string _dueWhy = "", _dueWhyPl = "-";
        private static long _dFromTax, _dFromLord, _dShort;

        /// <summary>r4-wioska (recenzja): podpis bramek naleznosci (klucz, pan okregu, wojna, rabunek wsi gry) - zmiana w ciagu doby (np. wojna wypowiedziana) liczy Due
        /// od nowa, zamiast pokazywac rano policzone +N. Tanie (bez ksiegi). Nie rzuca.</summary>
        private static int DueSig()
        {
            if (!HasFief) return 0;
            try
            {
                var s = Settings.Current;
                int k = s != null && s.SideVillageFiefIncome ? 1 : 0;
                var st = Settlement.Find(_dist);
                var v = st != null ? st.Village : null;
                if (v == null) return k | 2;
                var lord = v.Bound != null ? v.Bound.OwnerClan : null;
                if (lord == null || ReferenceEquals(lord, Clan.PlayerClan)) k |= 4;
                IFaction mf = Clan.PlayerClan != null ? Clan.PlayerClan.MapFaction : null, df = st.MapFaction;
                if (mf != null && df != null && !ReferenceEquals(mf, df) && FactionManager.IsAtWarAgainstFaction(mf, df)) k |= 8;
                if (v.VillageState == Village.VillageStates.BeingRaided || v.VillageState == Village.VillageStates.Looted) k |= 16;
                if (lord != null) k ^= System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(lord) << 5;
                return k;
            }
            catch { return -1; }
        }

        /// <summary>r4-wioska (recenzja): stawka pana z utargu wsi okregu - czynny model podatku wsi (CalculateVillageTaxFromIncome, ta sama regula co BK, 112 i DS:
        /// 1 - DsVillage.VillageShareOfTakings), liczona tu we wlasnym try: wyjatek modelu = stawka NIEZNANA (false, dochod 0), a nie 100% (DsVillage przy wyjatku daje udzial wsi 0).</summary>
        private static bool LordRate(Village v, out double rate)
        {
            rate = 0;
            try
            {
                const int probe = 10000;
                var m = Campaign.Current != null ? Campaign.Current.Models.SettlementTaxModel : null;
                if (m == null) return false;
                double r = m.CalculateVillageTaxFromIncome(v, probe) / (double)probe;
                if (double.IsNaN(r) || double.IsInfinity(r)) return false;
                rate = r < 0 ? 0 : (r > 1 ? 1 : r);
                return true;
            }
            catch (Exception e) { rate = 0; Stumble("LordRate", e); return false; }
        }

        /// <summary>r4-wioska: naleznosc dnia wioski gracza - to, co z niej nalezy sie panu okregu wedlug naszej ekonomii: wartosc dnia jej produkcji (VillageCrafts.PartsOf,
        /// ta sama co okno prac; zapas - udzial ludzi w domu x wartosc okregu) x H okregu (utarg oddany przy wozach / wartosc produkcji - DistrictWorks.HandedRatio, ta sama
        /// co okno prac: zmierzona albo srednia swiata 0.868; recenzja - podatek pana nalicza sie od utargu, nie od wartosci) x stawka pana z utargu (LordRate: czynny model
        /// podatku wsi - ta sama regula co BK, 112 i "udzial pana" DS przy wozie). Raz na dobe (fresh - od nowa, przy wyplacie; zmiana podpisu bramek DueSig - od nowa).
        /// 0 tylko w uczciwych przypadkach: klucz wylaczony, gracz panem okregu, wojna z krolestwem okregu, wies gry okregu rabowana / spladrowana (gra tez nie daje wtedy
        /// panu podatku), wioska gracza spalona, pan nie bierze podatku, wioska wytwarza dzis za malo, stawka nieznana - powod po angielsku w whyEn (Fiefs, menu), po polsku
        /// w _dueWhyPl (log). Watek glowny.</summary>
        internal static long Due(out string whyEn, bool fresh = false)
        {
            int today = Today(), sig = DueSig();
            if (!fresh && _dueDay == today && _dueSig == sig) { whyEn = _dueWhy; return _due; }
            _dueDay = today; _dueSig = sig; _due = 0; _rate = 0; _hand = 0; _dueCode = 0; _dueWhy = ""; _dueWhyPl = "-";
            try
            {
                var s = Settings.Current;
                if (!HasFief) { _dueWhy = "This village is not yours."; _dueWhyPl = "brak wioski"; }
                else if (s == null || !s.SideVillageFiefIncome) { _dueWhy = "Its income is switched off in mod settings."; _dueWhyPl = "klucz dochodu WYL"; }
                else
                {
                    var st = Settlement.Find(_dist);
                    var v = st != null ? st.Village : null;
                    var lord = v != null && v.Bound != null ? v.Bound.OwnerClan : null;
                    IFaction mf = Clan.PlayerClan != null ? Clan.PlayerClan.MapFaction : null, df = st != null ? st.MapFaction : null;
                    VillageBook.VState vs;
                    if (v == null) { _dueWhy = "Its district is not known."; _dueWhyPl = "okreg nieznany"; }
                    else if (lord == null || ReferenceEquals(lord, Clan.PlayerClan)) { _dueCode = 1; _dueWhy = "You hold the district yourself - its whole tax is yours already."; _dueWhyPl = "gracz panem okregu"; }
                    else if (mf != null && df != null && !ReferenceEquals(mf, df) && FactionManager.IsAtWarAgainstFaction(mf, df)) { _dueCode = 2; _dueWhy = "Nothing while you are at war with its lord's realm."; _dueWhyPl = "wojna z krolestwem okregu"; }
                    else if (v.VillageState == Village.VillageStates.BeingRaided || v.VillageState == Village.VillageStates.Looted) { _dueWhy = "Nothing while " + Txt(st.Name) + " lies raided - the lord takes no tax from it either."; _dueWhyPl = "wies gry okregu rabowana / spladrowana"; }
                    else if (VillageBook.StateOf(_uid, out vs) && (vs.State == VillageBook.SBurned || vs.State == VillageBook.SBurning || vs.State == VillageBook.SAbandoned)) { _dueWhy = "Nothing while your village lies burned."; _dueWhyPl = "wioska gracza spalona"; }
                    else
                    {
                        double share = ShareOf(v);
                        double val = _mineVal > 0 ? _mineVal : share * _allVal;
                        if (double.IsNaN(val) || double.IsInfinity(val) || val < 0) val = 0;
                        _hand = DistrictWorks.HandedRatio(v, _allVal);
                        if (double.IsNaN(_hand) || double.IsInfinity(_hand) || _hand <= 0) _hand = 0.868;
                        bool rateOk = LordRate(v, out _rate);
                        double d = val * _hand * _rate;
                        string calc = "wartosc wioski " + val.ToString("0", Inv) + " x H " + _hand.ToString("0.00", Inv) + " x stawka pana " + (100 * _rate).ToString("0", Inv) + "%";
                        if (!rateOk) { _dueCode = 3; _dueWhy = "Its lord's tax is not known today."; _dueWhyPl = "stawka pana nieznana (model podatku wsi)"; }
                        else if (!(val > 0)) { _dueCode = 3; _dueWhy = "What your village makes is not known yet."; _dueWhyPl = "brak wartosci produkcji (" + _shareNote + ")"; }
                        else if (!(_rate > 0)) { _dueCode = 3; _dueWhy = "The lord takes no tax from this district."; _dueWhyPl = "stawka pana 0"; }
                        else if (!(d >= 0.5) || double.IsInfinity(d)) { _dueCode = 3; _dueWhy = "Your village makes too little today to owe the lord anything."; _dueWhyPl = "za mala wartosc dnia (" + calc + ")"; }
                        else { _due = (long)Math.Round(Math.Min(d, 1e6)); _dueWhyPl = calc; }
                    }
                }
            }
            catch (Exception e) { _due = 0; _dueWhy = "Not known today."; _dueWhyPl = "blad"; Stumble("Due", e); }
            whyEn = _dueWhy;
            return _due;
        }

        /// <summary>r4-wioska (OnDailyTick, SideVillageFief.Daily): wyplata naleznosci dnia graczowi - z licznika podatku pana tej wsi gry okregu (TradeTaxAccumulated: podatek
        /// z utargow wozow okregu, ktorego pan jeszcze nie zebral), a reszta z kiesy glowy rodu pana (tam ida zebrane podatki). Recenzja: z BK pan zbiera licznik wsi
        /// codziennie w 80-100% (EconomyPatches, AddSettlementIncome -> BKTaxModel.CalculateVillageTaxFromIncome i pobranie), wiec zwykle placi glownie KIESA pana.
        /// Zamknieta ekonomia: pan ma mniej dokladnie o tyle, ile dostaje gracz; brak u pana - "brak" (nic z niczego; widok - Expect).
        /// Bez podwojnego liczenia: przy wozie nie ma juz przelewu (AtCart), czynsz i daniny DS zostaja przy panie.</summary>
        private static void Pay()
        {
            if (!HasFief) return;
            try
            {
                string why;
                long due = Due(out why, true);
                if (_dueCode == 1) _dSelf++; else if (_dueCode == 2) _dWar++; else if (_dueCode == 3) _dNoShare++;   // liczniki linii doby - raz na wyplate
                if (due <= 0) return;
                var me = Hero.MainHero;
                var st = Settlement.Find(_dist);
                var v = st != null ? st.Village : null;
                var lord = v != null && v.Bound != null ? v.Bound.OwnerClan : null;
                if (me == null || !me.IsAlive || v == null || lord == null) return;
                long fromTax = Math.Min(due, Math.Max(0, (long)v.TradeTaxAccumulated));
                long fromLord = 0;
                var lh = lord.Leader;
                if (due - fromTax > 0 && lh != null && lh.IsAlive && !ReferenceEquals(lh, me)) fromLord = Math.Min(due - fromTax, Math.Max(0, (long)lh.Gold));
                if (fromTax > 0) v.TradeTaxAccumulated -= (int)fromTax;          // licznik pana -> gracz (jak wyplata prac okregu)
                if (fromLord > 0) lh.ChangeHeroGold(-(int)fromLord);             // kiesa pana -> gracz
                long paid = fromTax + fromLord;
                if (paid > 0) me.ChangeHeroGold((int)paid);
                _dPaid += paid; _paidAll += paid; _dFromTax += fromTax; _dFromLord += fromLord; _dShort += due - paid;
            }
            catch (Exception e) { Stumble("Pay", e); }
        }

        /// <summary>r4-wioska (recenzja): to, co pan moze dac teraz - licznik podatku wsi gry okregu + kiesa glowy rodu pana (te same zrodla co Pay). long.MaxValue - nieznane
        /// (widok bez obciecia). Watek glowny, nie rzuca.</summary>
        private static long LordCanPay()
        {
            try
            {
                var st = Settlement.Find(_dist);
                var v = st != null ? st.Village : null;
                var lord = v != null && v.Bound != null ? v.Bound.OwnerClan : null;
                if (v == null || lord == null) return 0;
                long have = Math.Max(0, (long)v.TradeTaxAccumulated);
                var lh = lord.Leader; var me = Hero.MainHero;
                if (lh != null && lh.IsAlive && !ReferenceEquals(lh, me)) have += Math.Max(0, (long)lh.Gold);
                return have;
            }
            catch (Exception e) { Stumble("LordCanPay", e); return long.MaxValue; }
        }

        /// <summary>r4-wioska (recenzja): widok dochodu dnia (podpowiedz zlota, Fiefs, menu) - naleznosc Due obcieta do tego, co pan ma teraz (LordCanPay), z powodem
        /// po angielsku, gdy obcieta - zeby ekran nie obiecywal +N, gdy pan nie ma z czego zaplacic. Pieniadze plyna tylko z Pay.</summary>
        internal static long Expect(out string whyEn, out long due)
        {
            due = Due(out whyEn);
            if (due <= 0) return 0;
            long have = LordCanPay();
            if (have >= due) return due;
            whyEn = have > 0 ? "Its lord has only " + have.ToString("N0", Inv) + " to give today." : "Its lord has no money to pay it today.";
            return Math.Max(0, have);
        }

        /// <summary>r4-wioska: zakladka Fiefs ekranu rodu (SideVillageFiefUi) - naglowek, linia wioski i dochod dnia. false - brak wioski gracza. Watek glowny, nie rzuca.</summary>
        internal static bool ClanTab(out string header, out string line, out string income)
        {
            header = line = income = "";
            try
            {
                if (!HasFief) return false;
                var st = Settlement.Find(_dist);
                var lord = st != null && st.Village != null && st.Village.Bound != null ? st.Village.Bound.OwnerClan : null;
                header = "Side villages (1)";
                line = _name + " - lands of " + (st != null ? Txt(st.Name) : _dist) + (lord != null && !ReferenceEquals(lord, Clan.PlayerClan) ? ", sworn to " + MapVillagesView.HouseName(lord) : "");
                string why; long due;
                long d = Expect(out why, out due);
                income = d <= 0 ? "Income: none today. " + why
                       : d < due ? "Income: about " + d.ToString("N0", Inv) + " today (its share of the lord's tax is " + due.ToString("N0", Inv) + ", but the lord has no more to give)"
                       : "Income: about " + d.ToString("N0", Inv) + " a day (its share of the lord's tax)";
                return true;
            }
            catch (Exception e) { Stumble("ClanTab", e); return false; }
        }

        /// <summary>r4-wioska: tekst prawej czesci zakladki Fiefs, gdy rod nie ma miast ani zamkow (zamiast "Clan Does Not Own Any Fiefs"). null - bez zmian.</summary>
        internal static string NoFiefsText()
        {
            string h, l, i;
            if (!ClanTab(out h, out l, out i)) return null;
            return "Your clan holds no towns or castles.\n\nYour side village: " + l + ".\n" + i.TrimEnd('.', ' ') + ".";   // recenzja: powody koncza sie kropka - bez dwoch kropek
        }

        internal static void UiStumble(string where, Exception e) { Stumble("Ui." + where, e); }

        // ============================================================ W3: linia w podpowiedzi zlota
        /// <summary>Postfiks CalculateClanGoldChange czynnego modelu: rod gracza, z opisami i BEZ pobrania (podpowiedz zlota) - r4-wioska: naleznosc dnia (Due - wyplata
        /// w najblizszym OnDailyTick), nie srednia 7 dob (na starcie 0). Tylko widok: pieniadze ida z Pay, nie z modelu finansow (bez podwojnej wyplaty).</summary>
        public static void GoldTipPostfix(object __instance, Clan __0, bool __1, bool __2, ref ExplainedNumber __result)
        {
            try
            {
                if (!__1 || __2 || __0 == null || !HasFief || !ReferenceEquals(__0, Clan.PlayerClan)) return;
                var s = Settings.Current;
                if (s == null || !s.SideVillageFiefIncome || Campaign.Current == null || !ReferenceEquals(__instance, Campaign.Current.Models.ClanFinanceModel)) return;
                string why; long due;
                float x = Expect(out why, out due);   // recenzja: obciete do tego, co pan ma teraz (bez obietnicy +N, gdy pan nie ma z czego)
                if (x < 1f) return;
                if (_tipTxt == null || !ReferenceEquals(_tipName, _name)) { _tipName = _name; _tipTxt = new TextObject("{=!}" + (_name ?? "?") + " (your side village: its share of the lord's tax)"); }
                __result.Add(x, _tipTxt);
                _dTip++;
            }
            catch (Exception e) { Stumble("GoldTipPostfix", e); }
        }

        // ============================================================ W4: gospodarz na mapie
        /// <summary>VillageHolders.Of: gracz gospodarzem swojej wioski (herb rodu gracza, pan okregu "sworn to"). null - nie ta wioska albo brak herbu.</summary>
        internal static VillageHolders.Holder HolderOf(Settlement district, string uid)
        {
            if (!IsMine(uid) || district == null || district.Village == null) return null;
            var pc = Clan.PlayerClan;
            if (pc == null || pc.Banner == null || string.IsNullOrEmpty(pc.Banner.BannerCode)) return null;
            var bound = district.Village.Bound;
            var lord = bound != null ? bound.OwnerClan : null;
            if (_holder != null && ReferenceEquals(_holderLord, lord) && ReferenceEquals(_holderBanner, pc.Banner) && ReferenceEquals(_holder.Hero, Hero.MainHero)) return _holder;
            _holder = new VillageHolders.Holder { Rule = VillageHolders.RulePlayer, Hero = Hero.MainHero, Clan = pc, Banner = pc.Banner, Lord = lord };
            _holderLord = lord; _holderBanner = pc.Banner;
            return _holder;
        }

        /// <summary>Linia dymku / okna okregu dla wioski gracza.</summary>
        internal static string TipLine(Clan lord)
        {
            if (lord == null || ReferenceEquals(lord, Clan.PlayerClan)) return "This village is yours.";
            return "This village is yours, sworn to " + MapVillagesView.HouseName(lord) + ".";
        }

        /// <summary>Herb gracza na proporczyku przy wylaczonym VillageBannerPerHolder (kod herbu rodu gracza albo null).</summary>
        internal static string BannerCodeOf(string uid)
        {
            if (!IsMine(uid)) return null;
            var pc = Clan.PlayerClan;
            return pc != null && pc.Banner != null && !string.IsNullOrEmpty(pc.Banner.BannerCode) ? pc.Banner.BannerCode : null;
        }

        // ============================================================ W5: wejscie z mapy
        /// <summary>MapVillagesView: klik na wioske gracza - jazda do niej (jak klik w teren), menu po dojechaniu (Tick). true = klik obsluzony.</summary>
        internal static bool OnClick(string uid, float x, float y)
        {
            try
            {
                if (!Clickable(uid)) return false;
                var mp = MobileParty.MainParty;
                if (mp == null || mp.MapEvent != null || mp.CurrentSettlement != null) return false;
                if (mp.Army != null && mp.Army.LeaderParty != mp) return false;
                var ms = Game.Current != null ? Game.Current.GameStateManager.ActiveState as TaleWorlds.CampaignSystem.GameState.MapState : null;
                if (ms == null) return false;
                _px = x; _py = y; _posSet = true;
                ms.ProcessTravel(new CampaignVec2(new Vec2(x, y), true));
                _pendingClick = uid; _askedUid = null;
                _dClick++;
                return true;
            }
            catch (Exception e) { Stumble("OnClick", e); return false; }
        }

        /// <summary>MapVillagesView: polozenie obrazka wioski gracza po przesunieciu (najechanie) - punkt postoju.</summary>
        internal static void NotePos(string uid, float x, float y) { if (IsMine(uid)) { _px = x; _py = y; _posSet = true; } }

        /// <summary>CampaignEvents.TickEvent: 4 razy na sekunde (zegar rzeczywisty) - partia gracza przy swojej wiosce: po kliku menu, po postoju pytanie (raz na postoj).
        /// Oczekujace nadanie z ksiega wiosek nieczynna - HourlyTick. Koszt: jedno porownanie odleglosci.</summary>
        internal static void Tick()
        {
            if (!HasFief) return;
            long tq = Stopwatch.GetTimestamp();
            if (tq - _lastCheck < Stopwatch.Frequency / 4) return;
            _lastCheck = tq;
            try
            {
                var s = Settings.Current;
                if (s == null || !s.SideVillageFiefMenu) return;
                var c = Campaign.Current;
                if (c == null || c.CurrentMenuContext != null) return;
                if (TaleWorlds.CampaignSystem.Encounters.PlayerEncounter.Current != null) return;
                if (PopupBlocked()) return;
                var mp = MobileParty.MainParty;
                if (mp == null || Hero.MainHero == null || !Hero.MainHero.IsAlive) return;
                if (mp.CurrentSettlement != null || mp.MapEvent != null || mp.IsCurrentlyAtSea || TaleWorlds.CampaignSystem.PlayerCaptivity.IsCaptive
                    || mp.SiegeEvent != null || mp.BesiegerCamp != null) { _pendingClick = null; return; }
                if (!_posSet) PosFromBook();
                if (!_posSet) return;
                var pos = mp.GetPosition2D;
                var me = new Vec2(_px, _py);
                float d = pos.Distance(me);
                if (_askedUid != null && d > LeaveR) _askedUid = null;
                if (_pendingClick != null)
                {
                    if (d <= EnterR) { _pendingClick = null; _askedUid = _uid; Open("klik na wioske"); return; }
                    if (mp.TargetPosition.ToVec2().Distance(me) > EnterR + 0.5f) _pendingClick = null;   // gracz pojechal gdzie indziej
                    return;
                }
                if (d > EnterR || _askedUid == _uid) return;
                if (pos.Distance(mp.TargetPosition.ToVec2()) > ArriveEps) return;    // jeszcze jedzie (mija wioske)
                if (mp.Army != null && mp.Army.LeaderParty != mp) return;
                _askedUid = _uid;
                _dAsk++;
                InformationManager.ShowInquiry(new InquiryData(_name, "You draw rein by " + _name + ", your village. Ride in?", true, true, "Ride in", "Ride on",
                    delegate { try { if (!ConversationOn()) Open("pytanie przy postoju"); } catch (Exception e) { Stumble("Ask.Yes", e); } }, null), true);
            }
            catch (Exception e) { Stumble("Tick", e); }
        }

        /// <summary>TownInns.Host: odleglosc partii gracza od obrazka jego wioski (float.MaxValue - brak wioski albo polozenia). Watek glowny.</summary>
        internal static float PlayerDist()
        {
            var mp = MobileParty.MainParty;
            if (!HasFief || mp == null) return float.MaxValue;
            if (!_posSet) PosFromBook();
            if (!_posSet) return float.MaxValue;
            return mp.GetPosition2D.Distance(new Vec2(_px, _py));
        }

        private static void PosFromBook()
        {
            var recs = VillageBook.LayoutRecords();
            if (recs == null) return;
            foreach (var r in recs) if (r != null && IsMine(r.Uid)) { _px = r.X; _py = r.Y; _posSet = true; return; }
        }

        private static bool PopupBlocked()
        {
            if (LogGate.Mode == 1 || LogGate.Mode == 2) return true;   // autotest CrashScribe - nikt nie kliknie okna (jak zajazdy)
            if (InformationManager.IsAnyInquiryActive()) return true;
            if (ConversationOn()) return true;
            var ms = Game.Current != null && Game.Current.GameStateManager != null ? Game.Current.GameStateManager.ActiveState as TaleWorlds.CampaignSystem.GameState.MapState : null;
            return ms == null;
        }

        private static bool ConversationOn()
        {
            var cm = Campaign.Current != null ? Campaign.Current.ConversationManager : null;
            return cm != null && (cm.IsConversationInProgress || cm.IsConversationFlowActive);
        }

        private static void Open(string how)
        {
            var c = Campaign.Current;
            if (c == null || c.CurrentMenuContext != null) return;
            if (TaleWorlds.CampaignSystem.Encounters.PlayerEncounter.Current != null || ConversationOn()) return;
            var mp = MobileParty.MainParty;
            if (mp == null || mp.CurrentSettlement != null || mp.MapEvent != null) return;
            _dOpen++;
            try { mp.SetMoveModeHold(); } catch { }
            GameMenu.ActivateGameMenu(Menu);
            Log.Info(P + "wjazd do " + _name + " (" + _uid + ") - " + how + ", partia " + mp.GetPosition2D.Distance(new Vec2(_px, _py)).ToString("0.00", Inv) + " jedn. od wioski.");
        }

        // ============================================================ W5: menu
        internal static void AddMenus(CampaignGameStarter starter)
        {
            try
            {
                starter.AddGameMenuOption("village", OptVillage, "{=!}{ARM_SIDE_FIEF_VISIT}", VisitCondition,
                    delegate (MenuCallbackArgs a) { try { _dOpen++; Log.Info(P + "wjazd do " + _name + " z menu wsi gry."); GameMenu.SwitchToMenu(Menu); } catch (Exception e) { Stumble("Visit", e); } }, false, 9);
                starter.AddGameMenu(Menu, "{=!}{ARM_SIDE_FIEF_TEXT}", delegate (MenuCallbackArgs a)
                    {
                        try { MBTextManager.SetTextVariable("ARM_SIDE_FIEF_TEXT", MenuText()); } catch (Exception e) { Stumble("MenuInit", e); }
                        // recenzja (drobne): menu otwarte z mapy (nowy MenuContext) mialo tlo domyslne gry ("wait_guards_stop") - tlo wsi gry okregu jak sen w zajezdzie
                        try
                        {
                            var st = _dist != null ? Settlement.Find(_dist) : null;
                            string mesh = st != null && st.SettlementComponent != null ? st.SettlementComponent.WaitMeshName : null;
                            a.MenuContext.SetBackgroundMeshName(string.IsNullOrEmpty(mesh) ? "wait_fallback" : mesh);
                        }
                        catch (Exception e) { Stumble("MenuInit.Tlo", e); }
                    }, GameMenu.MenuOverlayType.None);
                starter.AddGameMenuOption(Menu, "arm_side_fief_rest", "{=!}Rest the night here",
                    delegate (MenuCallbackArgs a)
                    {
                        a.optionLeaveType = GameMenuOption.LeaveType.Wait;
                        if (!HasFief) return false;
                        var mp = MobileParty.MainParty;
                        if (NightRest.EnlistedNow) { a.IsEnabled = false; a.Tooltip = new TextObject("{=!}Your lord decides when the men sleep."); return true; }
                        if (mp != null && mp.Army != null && mp.Army.LeaderParty != mp) { a.IsEnabled = false; a.Tooltip = new TextObject("{=!}You march in another lord's army - he decides where the host sleeps."); return true; }
                        string why;
                        // recenzja (WAZNE): podpowiedz mowi prawde - pod dachem (stan noclegu karczmy, TownInns.Host) albo jak w obozie z powodem
                        if (RoofOk(out why))
                            a.Tooltip = new TextObject("{=!}Your villagers take the men in for the night - no charge. They sleep on straw under roofs, by night until dawn, and wake rested if they sleep their fill: "
                                                       + TownInns.HostBonusText() + " (like the common room of an inn).");
                        else a.Tooltip = new TextObject("{=!}The men camp by the village for the night, as anywhere on the map: " + why);
                        return true;
                    },
                    delegate (MenuCallbackArgs a)
                    {
                        try
                        {
                            _dRest++;
                            string why;
                            bool roof = RoofOk(out why) && TownInns.HostSleep(_dist, _uid, _name);
                            if (roof) _dRoof++;
                            Log.Info(P + "nocleg w " + _name + (roof ? " - pod dachem (stan noclegu jak wspolna izba karczmy, bez oplaty)" : " - jak w obozie (" + why + ")") + ".");
                            NightRest.BeginInnSleep();
                        }
                        catch (Exception e) { Stumble("Rest", e); }
                    }, false, 0);
                starter.AddGameMenuOption(Menu, "arm_side_fief_invest", "{=!}Invest in its works",
                    delegate (MenuCallbackArgs a)
                    {
                        a.optionLeaveType = GameMenuOption.LeaveType.Trade;
                        if (!HasFief) return false;
                        string why;
                        // recenzja (drobne): opcja martwa, gdy prac tej wioski nie da sie kupic (konie, gift villages Strazy, spalona, zmiana zajecia) - wylaczona z powodem
                        if (!DistrictWorks.On) { a.IsEnabled = false; a.Tooltip = new TextObject("{=!}District works are switched off (mod settings)."); }
                        else if (!InvestOk(out why)) { a.IsEnabled = false; a.Tooltip = new TextObject("{=!}" + why); }
                        else a.Tooltip = new TextObject("{=!}Pay your villagers for boats, herds, ploughs or tools. Your village makes more of its own goods, and half of what the extra sells for is yours.");
                        return true;
                    },
                    delegate (MenuCallbackArgs a)
                    {
                        try { _dInvest++; _invHour = -1; var st = Settlement.Find(_dist); if (st != null) DistrictWorks.OpenPartFor(st, _uid); }   // po oknie prac wycena od nowa
                        catch (Exception e) { Stumble("Invest", e); }
                    }, false, 1);
                starter.AddGameMenuOption(Menu, "arm_side_fief_look", "{=!}Look over your village",
                    delegate (MenuCallbackArgs a) { a.optionLeaveType = GameMenuOption.LeaveType.Manage; return HasFief; },
                    delegate (MenuCallbackArgs a) { try { ShowLook(); } catch (Exception e) { Stumble("Look", e); } }, false, 2);
                starter.AddGameMenuOption(Menu, "arm_side_fief_leave", "{=!}Leave",
                    delegate (MenuCallbackArgs a) { a.optionLeaveType = GameMenuOption.LeaveType.Leave; return true; },
                    delegate (MenuCallbackArgs a)
                    {
                        try
                        {
                            var here = MobileParty.MainParty != null ? MobileParty.MainParty.CurrentSettlement : null;
                            if (here != null && here.IsVillage) GameMenu.SwitchToMenu("village");   // wejscie z menu wsi gry - powrot do niego (zwykle menu)
                            else GameMenu.ExitToLast();
                        }
                        catch (Exception e) { Stumble("Leave", e); }
                    }, true, 9);
                SideVillageHome.AddMenus(starter);   // r4-dom: "Trade", "Your house" (lozko, skrzynia), "Take a walk"
            }
            catch (Exception e) { Stumble("AddMenus", e); }
        }

        private static bool VisitCondition(MenuCallbackArgs a)
        {
            try
            {
                a.optionLeaveType = GameMenuOption.LeaveType.Submenu;
                var s = Settings.Current;
                if (s == null || !s.SideVillageFiefMenu || !HasFief) return false;
                var st = Settlement.CurrentSettlement;
                if (st == null || !string.Equals(st.StringId, _dist, StringComparison.Ordinal)) return false;
                MBTextManager.SetTextVariable("ARM_SIDE_FIEF_VISIT", "Visit " + _name + " (your village)");
                a.Tooltip = new TextObject("{=!}Ride over to your side village: rest there, invest in its works or look it over.");
                return true;
            }
            catch { return false; }
        }

        /// <summary>Recenzja (WAZNE): nocleg pod dachem we wlasnej wiosce - klucz SideVillageFiefLodging, karczmy czynne (TownInns.On), partia u wioski, bez wojny z krolestwem
        /// okregu, wies gry nie rabowana teraz, wioska nie spalona. why - powod po angielsku (podpowiedz "jak w obozie: ..."). Watek glowny (warunek menu).</summary>
        private static bool RoofOk(out string why)
        {
            why = null;
            var s = Settings.Current;
            if (s == null || !s.SideVillageFiefLodging) { why = "lodging in your village is switched off (mod settings)."; return false; }
            if (!TownInns.On) { why = "lodgings need Town Inns and Night Rest (mod settings)."; return false; }
            if (!HasFief) { why = "this village is not yours."; return false; }
            var st = Settlement.Find(_dist);
            if (st == null || st.Village == null) { why = "the village is not known."; return false; }
            if (!TownInns.HostNearNow(_dist, _uid)) { why = "you are not at the village."; return false; }
            IFaction mf = Clan.PlayerClan != null ? Clan.PlayerClan.MapFaction : null, df = st.MapFaction;
            if (mf != null && df != null && !ReferenceEquals(mf, df) && FactionManager.IsAtWarAgainstFaction(mf, df)) { why = "while you are at war with its lord's realm, your villagers dare not take your men in."; return false; }
            if (st.Village.VillageState == Village.VillageStates.BeingRaided) { why = "raiders are on these lands and your villagers have fled."; return false; }
            VillageBook.VState vs;
            if (VillageBook.StateOf(_uid, out vs) && (vs.State == VillageBook.SBurned || vs.State == VillageBook.SBurning || vs.State == VillageBook.SAbandoned))
            { why = "your village is burned - there are no roofs left to sleep under."; return false; }
            return true;
        }

        /// <summary>Recenzja (drobne): prace tej wioski da sie kupic (DistrictWorks.QuotesOf: Allowed i rodzaj prac) - wycena raz na godzine gry i po oknie prac. why - powod gry.</summary>
        private static bool InvestOk(out string why)
        {
            int h = (int)CampaignTime.Now.ToHours;
            if (h != _invHour || !string.Equals(_invUid, _uid, StringComparison.Ordinal))
            {
                _invHour = h; _invUid = _uid; _invOk = false; _invWhy = "No works here.";
                try
                {
                    var st = _dist != null ? Settlement.Find(_dist) : null;
                    var qs = new List<DistrictWorks.Quote>();
                    if (st == null || st.Village == null || !DistrictWorks.QuotesOf(st.Village, qs, Clan.PlayerClan)) _invWhy = "The villages of this district are not known yet.";
                    else
                    {
                        DistrictWorks.Quote q = null;
                        foreach (var x in qs) if (x != null && string.Equals(x.Uid, _uid, StringComparison.Ordinal)) { q = x; break; }
                        if (q != null && q.Allowed && q.WKind >= 0) { _invOk = true; _invWhy = null; }
                        else if (q != null && !string.IsNullOrEmpty(q.Why)) _invWhy = q.Why;
                    }
                }
                catch (Exception e) { _invOk = false; _invWhy = "No works here."; Stumble("InvestOk", e); }
            }
            why = _invWhy;
            return _invOk;
        }

        /// <summary>Tekst menu: wioska, okreg, pan, ludzie, zajecie, udzial w produkcji, dochod.</summary>
        internal static string MenuText()
        {
            if (!HasFief) return "This village is not yours.";
            var st = Settlement.Find(_dist);
            var sb = new StringBuilder(600);
            string dname = st != null ? Txt(st.Name) : _dist;
            var lord = st != null && st.Village != null && st.Village.Bound != null ? st.Village.Bound.OwnerClan : null;
            sb.Append(_name).Append(" is your village in the lands of ").Append(dname);
            if (lord != null && !ReferenceEquals(lord, Clan.PlayerClan)) sb.Append(", sworn to ").Append(MapVillagesView.HouseName(lord));
            sb.Append(".\n");
            VillageBook.VState vs;
            if (VillageBook.StateOf(_uid, out vs))
                sb.Append(Math.Round(vs.Home + vs.Away).ToString("N0", Inv)).Append(" people in ").Append(Math.Max(1.0, Math.Round(vs.Settlements)).ToString("0", Inv)).Append(" homesteads")
                  .Append(vs.State == VillageBook.SStanding ? "." : vs.State == VillageBook.SBurned ? " - burned, people are slowly coming back." : " - raiders have emptied part of it.").Append('\n');
            if (st != null) foreach (string l in DistrictWorks.PartLines(st, _uid, false)) sb.Append(l).Append('\n');
            string why; long due;
            long got = Expect(out why, out due);   // r4-wioska: dochod dnia (ten sam co w podpowiedzi zlota i w zakladce Fiefs; recenzja - obciety do tego, co pan ma)
            if (got > 0) sb.Append("Your share of the lord's tax: about ").Append(got.ToString("N0", Inv)).Append(got < due ? " today (the lord has no more to give)" : " a day");
            else sb.Append("Your share of the lord's tax: none today (").Append(why.TrimEnd('.')).Append(")");
            sb.Append(", ").Append(_paidAll.ToString("N0", Inv)).Append(" in all since day ").Append(_day0).Append('.');
            return sb.ToString();
        }

        private static void ShowLook()
        {
            string body = MenuText();
            var st = Settlement.Find(_dist);
            if (st != null)
            {
                // tylko linie dymku, ktorych nie ma juz w tekscie menu ("Kit in place")
                var shown = new HashSet<string>(DistrictWorks.PartLines(st, _uid, false), StringComparer.Ordinal);
                var more = new StringBuilder();
                foreach (string l in DistrictWorks.PartLines(st, _uid, true)) if (!shown.Contains(l)) more.Append('\n').Append(l);
                if (st.Village != null) ShareOf(st.Village);
                more.Append("\nHow it pays: the lord of ").Append(Txt(st.Name)).Append(" takes his tax from what the district sells at market. The part that comes from your village - its share of what the district makes, now about ")
                    .Append((100 * _share).ToString("0", Inv)).Append("% - is paid to you every day, out of the lord's tax (or his purse). At war with the lord's realm, or while the district lies raided or your village burned, you get nothing.");
                body += "\n" + more.ToString();
            }
            // przeglad calosci 20.10: ramka okna (SingleQueryPopup ROT) nie przewija - ok. 19 linii (Rumours.PageLines z zapasem). Przy wojnie, spaleniu i odbudowie
            // naraz tekst ma ok. 22 linie - wtedy strony po akapitach (jak plotki): "More" i "Done"
            var pages = new List<string>();
            var cur = new StringBuilder();
            int n = 0;
            foreach (string para in body.Split('\n'))
            {
                int need = Math.Max(1, Rumours.EstLines(para));
                if (cur.Length > 0 && n + need > Rumours.PageLines) { pages.Add(cur.ToString().TrimEnd('\n')); cur.Length = 0; n = 0; }
                if (cur.Length == 0 && para.Length == 0) continue;   // pusta linia na poczatku strony - zbedna
                if (cur.Length > 0) cur.Append('\n');
                cur.Append(para); n += need;
            }
            if (cur.Length > 0 || pages.Count == 0) pages.Add(cur.ToString());
            LookPage(pages, 0);
        }

        private static void LookPage(List<string> pages, int i)
        {
            bool more = i + 1 < pages.Count;
            string title = (_name ?? "Your village") + (pages.Count > 1 ? " (" + (i + 1).ToString(Inv) + "/" + pages.Count.ToString(Inv) + ")" : "");
            Action next = null;
            if (more) { int k = i + 1; next = () => { try { LookPage(pages, k); } catch (Exception e) { Stumble("LookPage", e); } }; }
            InformationManager.ShowInquiry(new InquiryData(title, pages[i], true, more, more ? "More" : "Done", more ? "Done" : null, next, null), false);
        }

        // ============================================================ godzina, doba, test
        /// <summary>HourlyTick: oczekujace nadanie (start BK przed czynna ksiega wiosek), furtka autotestu.</summary>
        internal static void Hourly()
        {
            try
            {
                if (_pendKingdom != null) TryPending();
                if (!_testDone && TestWanted()) Test();
                SideVillageHome.Hourly();   // r4-dom: samotest domu, handlu i spaceru (ta sama flaga)
            }
            catch (Exception e) { Stumble("Hourly", e); }
        }

        private static bool TestWanted()
        {
            int m = LogGate.Mode;
            if (m != 1 && m != 2) return false;
            if (!VillageBook.Active) return false;
            try
            {
                var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                string p = System.IO.Path.Combine(System.IO.Path.Combine(System.IO.Path.Combine(docs, "Mount and Blade II Bannerlord"), "CrashScribe"), AutoFlagName);
                return System.IO.File.Exists(p);
            }
            catch { return false; }
        }

        /// <summary>Proba autotestu (raz na sesje): ta sama metoda BK co start (odbiciem), prefiks, nadanie, gospodarz i herb, zapis tam i z powrotem, tekst menu.</summary>
        private static void Test()
        {
            _testDone = true;
            var sb = new StringBuilder(800);
            int pass = 0, fail = 0;
            Action<string, bool, string> add = (n, ok, t) => { sb.Append(" | ").Append(n).Append(' ').Append(ok ? "TAK" : "NIE").Append(" (").Append(t).Append(')'); if (ok) pass++; else fail++; };
            try
            {
                if (HasFief) { Log.Info(P + "test (autotest, plik " + AutoFlagName + ") | wioska juz nadana (" + _name + ") - bez nowego nadania; linie doby pokaza dochod."); return; }
                // krolestwo: gracza albo pana najblizszej wsi gry (bez wojny z graczem)
                Kingdom k = Clan.PlayerClan.Kingdom;
                if (k == null)
                {
                    var pos = MobileParty.MainParty.GetPosition2D; float bd = float.MaxValue;
                    foreach (var st in Settlement.All)
                    {
                        if (st == null || !st.IsVillage || st.Village == null || st.Village.Bound == null) continue;
                        var lc = st.Village.Bound.OwnerClan;
                        if (lc == null || lc.Kingdom == null || ReferenceEquals(lc, Clan.PlayerClan)) continue;
                        if (Clan.PlayerClan.MapFaction != null && FactionManager.IsAtWarAgainstFaction(lc.Kingdom, Clan.PlayerClan.MapFaction)) continue;
                        float d = st.GetPosition2D.Distance(pos);
                        if (d < bd) { bd = d; k = lc.Kingdom; }
                    }
                }
                if (k == null) { Log.Info(P + "test (autotest) | NIE - brak krolestwa bez wojny z graczem."); return; }
                int before = _dIntercept;
                bool called = _bkWired && InvokeBk(k, false);
                add("T1 latka BK GiveLordshipOnKingdomJoin", _bkWired, _bkWired ? "wpieta" : "BRAK");
                add("T2 prefiks przejal wywolanie BK", called && _dIntercept == before + 1, "wywolanie " + (called ? "tak" : "nie") + ", przejec " + (_dIntercept - before));
                if (!HasFief && _pendKingdom != null)
                {
                    string why; _pendKingdom = null;
                    if (!Grant(k, "autotest (bez wojny)", true, out why)) add("T3 nadanie", false, why);
                }
                add("T3 wioska nadana", HasFief, HasFief ? _name + " (" + _uid + "), okreg " + _dist : "brak");
                if (HasFief)
                {
                    var st = Settlement.Find(_dist);
                    var h = st != null ? VillageHolders.Of(st, _uid) : null;
                    string code = VillageHolders.Code(st, _uid);
                    add("T4 gospodarz = gracz, herb gracza", h != null && h.Rule == VillageHolders.RulePlayer && code == BannerCodeOf(_uid), h != null ? "regula " + h.Rule + ", herb " + (code == BannerCodeOf(_uid) ? "gracza" : "inny") : "brak");
                    add("T5 dymek", VillageHolders.Line(h) != null, VillageHolders.Line(h) ?? "brak");
                    string e1 = Export(); string u = _uid, dd = _dist;
                    Import(e1); string e2 = Export();
                    add("T6 zapis tam i z powrotem", e1 == e2 && _uid == u && _dist == dd, e1);
                    string mt = MenuText();
                    add("T7 tekst menu", mt.Length > 40, mt.Replace('\n', ' ').Substring(0, Math.Min(160, mt.Length)));
                    add("T8 model finansow (linia podpowiedzi)", _finWired, _finWired ? "wpieta (" + (_finDecl != null ? _finDecl.Name : "?") + ")" : "BRAK");
                    double sh = st != null && st.Village != null ? ShareOf(st.Village) : 0;
                    add("T9 udzial wioski w okregu", sh > 0 && sh < 1, (100 * sh).ToString("0.0", Inv) + "% (" + _shareNote + ")");
                    // T11 (r4-wioska): naleznosc dnia znana od razu (bez wozu i bez wczorajszej wartosci) i tekst zakladki Fiefs
                    string tw; long td = Due(out tw, true);
                    string th, tl, ti; bool tab = ClanTab(out th, out tl, out ti);
                    // recenzja: takze rejestracja wstawki i mixinu UIExtenderEx (przy kluczu WL) - sam napis nie dowodzi bloku w Fiefs; wyglad bloku tylko recznie w ekranie rodu
                    var ts = Settings.Current;
                    bool ui = (ts != null && !ts.SideVillageFiefClanTab) || (SideVillageFiefUi.State ?? "").StartsWith("wstawka", StringComparison.Ordinal);
                    add("T11 dochod dnia i zakladka Fiefs", td > 0 && tab && ui, "nalezne " + td + " (" + _dueWhyPl + (tw.Length > 0 ? ", " + tw : "") + "), Fiefs: " + (tab ? th + " | " + tl + " | " + ti : "BRAK")
                        + ", lista - " + SideVillageFiefUi.State);
                    // T10 (recenzja, WAZNE): nocleg pod dachem - stan noclegu karczmy z uid wioski i zapis arm_inns tam i z powrotem; stan noclegu gracza przywrocony
                    string i0 = TownInns.Export();
                    bool made = TownInns.HostSleep(_dist, _uid, _name);
                    string i1 = TownInns.Export();
                    TownInns.Import(i1); string i2 = TownInns.Export();
                    TownInns.Import(i0);
                    bool f17 = i1.EndsWith(";" + _uid, StringComparison.Ordinal);
                    add("T10 nocleg pod dachem (stan i zapis)", made ? i1 == i2 && f17 : !TownInns.On,
                        made ? (i1 == i2 ? "zapis tam i z powrotem" : "zapis ROZNY") + ", pole 17 " + (f17 ? "uid wioski" : "BRAK") + ", stan gracza przywrocony (" + (i0.Length > 0 ? "byl" : "brak") + ")"
                             : "bez stanu (karczmy " + (TownInns.On ? "WL" : "WYL") + ")");
                }
            }
            catch (Exception e) { add("wyjatek", false, e.GetType().Name + ": " + e.Message); Stumble("Test", e); }
            Log.Info(P + "test (autotest, plik " + AutoFlagName + ") | wynik TAK " + pass + ", NIE " + fail + sb.ToString() + ".");
        }

        /// <summary>ArmouryBehavior.OnDailyTick: srednia 7 dob udzialu (podpowiedz zlota) i linia doby (start sesji zawsze, potem tylko z wioska albo ruchem).</summary>
        internal static void Daily()
        {
            try
            {
                var s = Settings.Current;
                if (s == null) return;
                int day = Today() - 1;
                Pay();   // r4-wioska: dochod dnia (naleznosc wioski z licznika podatku pana okregu, reszta z kiesy pana) - przed srednia i linia doby
                // srednia: przez pierwsze 7 dob od nadania srednia z dob, potem wykladnicza 7 dob (bez nowego stanu w zapisie - doba nadania jest w zapisie)
                if (HasFief) { int n = Math.Max(1, Math.Min(7, Today() - _day0)); _avg += (_dPaid - _avg) / n; if (_avg < 0) _avg = 0; }
                bool start = !_startLogged;
                _startLogged = true;
                if (!start && !HasFief && _dIntercept == 0 && _st == 0) return;
                var sb = new StringBuilder(500);
                sb.Append(P).Append("dzien ").Append(day).Append(start ? " (start sesji)" : "")
                  .Append(" | klucze: start ").Append(s.SideVillageFiefStart ? "WL" : "WYL").Append(", dochod ").Append(s.SideVillageFiefIncome ? "WL" : "WYL").Append(", menu ").Append(s.SideVillageFiefMenu ? "WL" : "WYL")
                  .Append(", nocleg pod dachem ").Append(s.SideVillageFiefLodging ? (TownInns.On ? "WL" : "WL (karczmy WYL)") : "WYL")
                  .Append(" | latki: BK GiveLordshipOnKingdomJoin ").Append(_bkWired ? "wpieta" : "BRAK").Append(", podpowiedz zlota ").Append(_finWired ? "wpieta (" + (_finDecl != null ? _finDecl.Name : "?") + ")" : "BRAK")
                  .Append(", opis startu BK (StartOptionVM.ShortDescription) ").Append(_startTxtWired ? "wpiety" : "BRAK")
                  .Append(", zakladka Fiefs: tekst ").Append(_noFiefsWired ? "wpiety" : "BRAK").Append(", lista - ").Append(SideVillageFiefUi.State)
                  .Append(", klucz ").Append(s.SideVillageFiefClanTab ? "WL" : "WYL");
                if (HasFief)
                {
                    sb.Append(" | wioska ").Append(_name).Append(" (").Append(_uid).Append(", okreg ").Append(_dist).Append(", od doby ").Append(_day0).Append(')')
                      .Append(" | wozy okregu ").Append(_dCarts).Append(", podatek pana w oknach ").Append(_dTax).Append(", udzial ").Append((100 * _share).ToString("0.0", Inv)).Append("% (").Append(_shareNote).Append(")")
                      .Append(" | dochod dnia (r4): nalezne ").Append(_due).Append(" (").Append(_dueWhyPl).Append("), z licznika podatku pana ").Append(_dFromTax).Append(", z kiesy pana ").Append(_dFromLord)
                      .Append(", brak u pana ").Append(_dShort)
                      .Append(", do gracza ").Append(_dPaid).Append(" (razem ").Append(_paidAll).Append("), srednia 7 dob ").Append(_avg.ToString("0.0", Inv))
                      .Append(", bez przelewu: wojna ").Append(_dWar).Append(", gracz panem ").Append(_dSelf).Append(", bez udzialu ").Append(_dNoShare);
                }
                else sb.Append(" | brak wioski gracza").Append(_pendKingdom != null ? " (nadanie czeka: krolestwo " + _pendKingdom + " od doby " + _pendDay + ")" : "");
                sb.Append(" | przejecia startu BK ").Append(_dIntercept).Append(", wjazdy ").Append(_dOpen).Append(" (klik ").Append(_dClick).Append(", pytania ").Append(_dAsk).Append(")")
                  .Append(", noclegi ").Append(_dRest).Append(" (pod dachem ").Append(_dRoof).Append(")").Append(", okna prac ").Append(_dInvest).Append(", linie podpowiedzi ").Append(_dTip)
                  .Append(" | potkniecia doby ").Append(_stDay).Append(", sesji ").Append(_st).Append('.');
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Stumble("Daily", e); }
            finally
            {
                DayZero();
                SideVillageHome.Daily();   // r4-dom: linia doby domu, handlu i spaceru - w finally, bo "return" w try (bez wioski) pomijal ja (recenzja)
            }
        }
    }
}
