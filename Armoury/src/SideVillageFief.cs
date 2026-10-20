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
    ///  W3 DOCHOD (klucz SideVillageFiefIncome): przy powrocie wozu do wsi MACIERZYSTEJ okregu (VillageTakings.EnterPostfix, po 112, F4b i pracach okregu, przed danina DS)
    ///     udzial wioski w produkcji okregu (VillageCrafts.PartsOf: ValDay czesci / suma) x przyrost licznika podatku pana w tym oknie - z licznika pana (TradeTaxAccumulated)
    ///     do kiesy gracza, w ksiedze pieniadza jak wyplata prac okregu (MoneyLedger.CartWorksInWindow). Pan dostaje mniej dokladnie o tyle. Wojna gracza z krolestwem
    ///     okregu albo gracz panem okregu - bez przelewu. Podpowiedz zlota (CalculateClanGoldChange z opisami, bez pobrania) - linia ze srednia 7 dob (tylko widok:
    ///     pieniadze ida przy wozie, nie z modelu finansow - bez podwojnej wyplaty).
    ///  W4 MAPA: VillageHolders.Of - gospodarzem tej wioski jest gracz (herb rodu gracza na proporczyku i w dymku, linia "This village is yours").
    ///  W5 MENU (klucz SideVillageFiefMenu): klik na wioske na mapie = jazda do niej (MapState.ProcessTravel, wzor zajazdow) i menu po dojechaniu; postoj przy niej - pytanie
    ///     raz na postoj; w menu wsi gry okregu "Visit X (your village)". Menu arm_side_fief (zwykle, nie oczekiwania): nocleg za darmo (NightRest.BeginInnSleep - sen
    ///     w menu jak w karczmie), prace okregu tej wioski (DistrictWorks.OpenPartFor), przeglad, wyjazd.
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
        // oczekujace nadanie (start BK przed czynna ksiega) - tylko sesja; zapis w toku czekania = brak wioski, BK nie wola drugi raz (wtedy linia doby mowi "brak")
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
        private static int _dCarts, _dWar, _dSelf, _dNoShare, _dIntercept, _dOpen, _dClick, _dAsk, _dRest, _dInvest, _dTip;
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
            DayZero();
        }

        private static void DayZero() { _dCarts = _dWar = _dSelf = _dNoShare = _dIntercept = _dOpen = _dClick = _dAsk = _dRest = _dInvest = _dTip = 0; _dPaid = _dTax = 0; _stDay = 0; }

        private static void Stumble(string where, Exception e)
        {
            _st++; _stDay++;
            bool first; lock (_err) first = _err.Add(where);
            if (first || _st % 200 == 0) Log.Error("SideVillageFief." + where + " (potkniecie " + _st + ")", e);
        }

        private static int Today() { return (int)CampaignTime.Now.ToDays; }

        internal static bool HasFief { get { return _uid != null && _dist != null; } }

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
            }
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
                    VillageBook.VState vs;
                    if (!VillageBook.StateOf(r.Uid, out vs)) continue;
                    if (vs.State != VillageBook.SStanding && (pass == 0 || vs.State != VillageBook.SDamaged)) continue;
                    seen++;
                    float dx = r.X - pos.x, dy = r.Y - pos.y, d = dx * dx + dy * dy;
                    if (d < bestD) { bestD = d; best = r; bestSt = st; }
                }
            }
            if (best == null) { why = "brak wioski stojacej w okregach krolestwa " + k.StringId + " (okregow z wioskami sprawdzonych " + seen + ")"; return false; }
            _uid = best.Uid; _dist = best.VillageId; _name = string.IsNullOrEmpty(best.Name) ? best.Uid : best.Name;
            _day0 = Today(); _paidAll = 0; _avg = 0;
            _px = best.X; _py = best.Y; _posSet = true;
            _holder = null; _shareDay = -1;
            var lordC = bestSt.Village.Bound != null ? bestSt.Village.Bound.OwnerClan : null;
            Log.Info(P + "NADANIE (" + how + ") - " + _name + " (" + _uid + "), okreg " + Txt(bestSt.Name) + " (" + _dist + "), pan " + (lordC != null ? Txt(lordC.Name) : "?")
                     + ", krolestwo " + k.StringId + ", " + Math.Sqrt(bestD).ToString("0.0", Inv) + " jedn. od partii gracza, kandydatow " + seen + "; wies gry zostaje przy panu.");
            if (!test)
            {
                string house = lordC != null ? MapVillagesView.HouseName(lordC) : "your liege";
                string body = "You hold " + _name + ", a side village in the lands of " + Txt(bestSt.Name) + ", sworn to " + house + ". " + Txt(bestSt.Name)
                              + " itself stays with its lord. Your village flies your banner, and its share of the lord's tax comes to you whenever the district's carts come back from market. "
                              + "Click it on the map to ride in: rest there, invest in its works or look it over.";
                Log.Player(_name + " is yours now - a side village of " + Txt(bestSt.Name) + ".");
                // okno jak zawiadomienie BK o nadaniu (BK pokazuje je w tym samym miejscu bez sprawdzania innych okien)
                try { InformationManager.ShowInquiry(new InquiryData("Your village", body, true, false, "Done", null, null, null), true); } catch (Exception e) { Stumble("Grant.Okno", e); }
            }
            return true;
        }

        private static string Txt(TextObject t) { try { return t != null ? t.ToString() : "?"; } catch { return "?"; } }

        // ============================================================ W3: dochod przy wozie
        /// <summary>VillageTakings.EnterPostfix (woz wrocil do wsi macierzystej; po 112, F4b i pracach okregu): udzial wioski gracza w podatku pana z tego okna.</summary>
        internal static void AtCart(MobileParty cart, Village home, long taxInWindow)
        {
            if (!HasFief || home == null || home.Settlement == null || taxInWindow <= 0) return;
            try
            {
                var s = Settings.Current;
                if (s == null || !s.SideVillageFiefIncome) return;
                if (!string.Equals(home.Settlement.StringId, _dist, StringComparison.Ordinal)) return;
                _dCarts++;
                var lord = home.Bound != null ? home.Bound.OwnerClan : null;
                if (lord == null || ReferenceEquals(lord, Clan.PlayerClan)) { _dSelf++; return; }   // gracz panem okregu - podatek i tak jest jego
                var me = Hero.MainHero;
                if (me == null || !me.IsAlive) return;
                IFaction mf = Clan.PlayerClan.MapFaction, df = home.Settlement.MapFaction;
                if (mf != null && df != null && !ReferenceEquals(mf, df) && FactionManager.IsAtWarAgainstFaction(mf, df)) { _dWar++; return; }
                double share = ShareOf(home);
                if (!(share > 0)) { _dNoShare++; return; }
                long want = (long)Math.Floor(share * taxInWindow);
                want = Math.Min(want, Math.Max(0, home.TradeTaxAccumulated));
                _dTax += taxInWindow;
                if (want < 1) return;
                home.TradeTaxAccumulated -= (int)want;                       // licznik pana -> kiesa gracza (jak wyplata prac okregu)
                me.ChangeHeroGold((int)want);
                MoneyLedger.CartWorksInWindow(cart, 0, (int)want);            // okno powrotu wozu w ksiedze pieniadza: to nie "zniklo"
                _dPaid += want; _paidAll += want;
            }
            catch (Exception e) { Stumble("AtCart", e); }
        }

        /// <summary>Udzial wioski gracza w wartosci dnia produkcji okregu (VillageCrafts.PartsOf - te same wartosci co okno prac); raz na dobe. Zapas - udzial ludzi w domu.</summary>
        private static double ShareOf(Village v)
        {
            int today = Today();
            if (_shareDay == today) return _share;
            _shareDay = today; _share = 0;
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
                        all += val; hAll += home;
                        if (!p.Main && IsMine(p.Uid)) { mine += val; hMine += home; }
                    }
                    if (all > 0 && mine > 0) { _share = Math.Min(1.0, mine / all); _shareNote = "wartosc dnia " + mine.ToString("0", Inv) + " z " + all.ToString("0", Inv) + (ks ? " (KS)" : ""); }
                    else if (hAll > 0 && hMine > 0) { _share = Math.Min(1.0, hMine / hAll); _shareNote = "ludzie w domu " + hMine.ToString("0", Inv) + " z " + hAll.ToString("0", Inv); }
                    else _shareNote = "wioska bez produkcji i ludzi w ksiedze";
                }
                else _shareNote = "okreg nieznany ksiedze";
            }
            catch (Exception e) { _share = 0; _shareNote = "blad"; Stumble("ShareOf", e); }
            return _share;
        }

        // ============================================================ W3: linia w podpowiedzi zlota
        /// <summary>Postfiks CalculateClanGoldChange czynnego modelu: rod gracza, z opisami i BEZ pobrania (podpowiedz zlota) - srednia 7 dob udzialu wioski. Tylko widok.</summary>
        public static void GoldTipPostfix(object __instance, Clan __0, bool __1, bool __2, ref ExplainedNumber __result)
        {
            try
            {
                if (!__1 || __2 || __0 == null || !HasFief || !ReferenceEquals(__0, Clan.PlayerClan)) return;
                var s = Settings.Current;
                if (s == null || !s.SideVillageFiefIncome || Campaign.Current == null || !ReferenceEquals(__instance, Campaign.Current.Models.ClanFinanceModel)) return;
                float x = (float)Math.Round(_avg);
                if (x < 1f) return;
                if (_tipTxt == null || !ReferenceEquals(_tipName, _name)) { _tipName = _name; _tipTxt = new TextObject("{=!}" + (_name ?? "?") + " (your village's share of the lord's tax)"); }
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
                starter.AddGameMenu(Menu, "{=!}{ARM_SIDE_FIEF_TEXT}", delegate (MenuCallbackArgs a) { try { MBTextManager.SetTextVariable("ARM_SIDE_FIEF_TEXT", MenuText()); } catch (Exception e) { Stumble("MenuInit", e); } }, GameMenu.MenuOverlayType.None);
                starter.AddGameMenuOption(Menu, "arm_side_fief_rest", "{=!}Rest the night here",
                    delegate (MenuCallbackArgs a)
                    {
                        a.optionLeaveType = GameMenuOption.LeaveType.Wait;
                        if (NightRest.EnlistedNow) { a.IsEnabled = false; a.Tooltip = new TextObject("{=!}Your lord decides when the men sleep."); }
                        else a.Tooltip = new TextObject("{=!}Your own people put you up for the night - no charge. The men sleep under roofs and wake rested.");
                        return HasFief;
                    },
                    delegate (MenuCallbackArgs a) { try { _dRest++; Log.Info(P + "nocleg w " + _name + "."); NightRest.BeginInnSleep(); } catch (Exception e) { Stumble("Rest", e); } }, false, 0);
                starter.AddGameMenuOption(Menu, "arm_side_fief_invest", "{=!}Invest in its works",
                    delegate (MenuCallbackArgs a)
                    {
                        a.optionLeaveType = GameMenuOption.LeaveType.Trade;
                        if (!DistrictWorks.On) { a.IsEnabled = false; a.Tooltip = new TextObject("{=!}District works are switched off (mod settings)."); }
                        else a.Tooltip = new TextObject("{=!}Pay your villagers for boats, herds, ploughs or tools. Your village makes more of its own goods, and half of what the extra sells for is yours.");
                        return HasFief;
                    },
                    delegate (MenuCallbackArgs a)
                    {
                        try { _dInvest++; var st = Settlement.Find(_dist); if (st != null) DistrictWorks.OpenPartFor(st, _uid); }
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
            sb.Append("Your share of the lord's tax: about ").Append(Math.Round(_avg).ToString("N0", Inv)).Append(" a day, ").Append(_paidAll.ToString("N0", Inv)).Append(" in all since day ").Append(_day0).Append('.');
            if (!Settings.Current.SideVillageFiefIncome) sb.Append(" (switched off in mod settings)");
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
                more.Append("\nHow it pays: when the carts of ").Append(Txt(st.Name)).Append(" come back from market, the lord takes his tax. Your village's part of it - its share of what the district makes, now about ")
                    .Append((100 * _share).ToString("0", Inv)).Append("% - is yours. At war with the lord's realm you get nothing.");
                body += "\n" + more.ToString();
            }
            InformationManager.ShowInquiry(new InquiryData(_name ?? "Your village", body, true, false, "Done", null, null, null), false);
        }

        // ============================================================ godzina, doba, test
        /// <summary>HourlyTick: oczekujace nadanie (start BK przed czynna ksiega wiosek), furtka autotestu.</summary>
        internal static void Hourly()
        {
            try
            {
                if (_pendKingdom != null) TryPending();
                if (!_testDone && TestWanted()) Test();
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
                // srednia: przez pierwsze 7 dob od nadania srednia z dob, potem wykladnicza 7 dob (bez nowego stanu w zapisie - doba nadania jest w zapisie)
                if (HasFief) { int n = Math.Max(1, Math.Min(7, Today() - _day0)); _avg += (_dPaid - _avg) / n; if (_avg < 0) _avg = 0; }
                bool start = !_startLogged;
                _startLogged = true;
                if (!start && !HasFief && _dIntercept == 0 && _st == 0) return;
                var sb = new StringBuilder(500);
                sb.Append(P).Append("dzien ").Append(day).Append(start ? " (start sesji)" : "")
                  .Append(" | klucze: start ").Append(s.SideVillageFiefStart ? "WL" : "WYL").Append(", dochod ").Append(s.SideVillageFiefIncome ? "WL" : "WYL").Append(", menu ").Append(s.SideVillageFiefMenu ? "WL" : "WYL")
                  .Append(" | latki: BK GiveLordshipOnKingdomJoin ").Append(_bkWired ? "wpieta" : "BRAK").Append(", podpowiedz zlota ").Append(_finWired ? "wpieta (" + (_finDecl != null ? _finDecl.Name : "?") + ")" : "BRAK");
                if (HasFief)
                {
                    sb.Append(" | wioska ").Append(_name).Append(" (").Append(_uid).Append(", okreg ").Append(_dist).Append(", od doby ").Append(_day0).Append(')')
                      .Append(" | wozy okregu ").Append(_dCarts).Append(", podatek pana w oknach ").Append(_dTax).Append(", udzial ").Append((100 * _share).ToString("0.0", Inv)).Append("% (").Append(_shareNote).Append(")")
                      .Append(", do gracza ").Append(_dPaid).Append(" (razem ").Append(_paidAll).Append("), srednia 7 dob ").Append(_avg.ToString("0.0", Inv))
                      .Append(", bez przelewu: wojna ").Append(_dWar).Append(", gracz panem ").Append(_dSelf).Append(", bez udzialu ").Append(_dNoShare);
                }
                else sb.Append(" | brak wioski gracza").Append(_pendKingdom != null ? " (nadanie czeka: krolestwo " + _pendKingdom + " od doby " + _pendDay + ")" : "");
                sb.Append(" | przejecia startu BK ").Append(_dIntercept).Append(", wjazdy ").Append(_dOpen).Append(" (klik ").Append(_dClick).Append(", pytania ").Append(_dAsk).Append(")")
                  .Append(", noclegi ").Append(_dRest).Append(", okna prac ").Append(_dInvest).Append(", linie podpowiedzi ").Append(_dTip)
                  .Append(" | potkniecia doby ").Append(_stDay).Append(", sesji ").Append(_st).Append('.');
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Stumble("Daily", e); }
            finally { DayZero(); }
        }
    }
}
