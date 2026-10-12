using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Extensions;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// E8/P1 (243, PLAN 8.1a; docs/etap8/PROJEKT-ETAP8-... rozdz. 3 "E8/P1", rozpoznanie A 1.1) - KSIEGA UMIEJETNOSCI. Tylko log, gra bez zmian:
    /// zadna latka nie zmienia argumentow, wyniku ani kolejnosci - same liczniki. Stan "przed" i "po" dla E8/X (R10).
    ///  - bohaterowie: prefiks-licznik (Priority.Last - po cudzych prefiksach, wiec z ostateczna kwota; pominiety, gdy cudzy prefiks zdejmie metode gry)
    ///    na HeroDeveloper.AddSkillXp(SkillObject, float, bool, bool) - Hero.AddSkillXp tylko do niej przekazuje (G Hero.cs:1386). Surowe XP (przed
    ///    mnoznikiem GenericXpModel i wspolczynnikiem nauki) w tablicy [grupa, umiejetnosc, zrodlo]; grupy: gracz / rod gracza (towarzysze i rodzina) /
    ///    lordowie AI / inni. XP z isAffectedByFocusFactor = false (HeroDeveloper.ChangeSkillLevel - poziom ustawiany wprost, G HeroDeveloper.cs:176-188)
    ///    liczone osobno, POZA suma (to nie nauka, tylko przeliczenie poziomu na XP - jedna zmiana to dziesiatki tysiecy).
    ///  - zrodlo: znacznik [ThreadStatic] ustawiany w krotkich prefiksach znanych wolajacych i zdejmowany w postfiksie (wewnetrzny wygrywa):
    ///    kazda metoda ISkillLevelingManager czynnego menedzera i jego lancucha (DefaultSkillLevelingManager; NavalDLC NavalSkillLevellingManager
    ///    przekazuje do niego przez pole), strzal RBM (RBMCombat.CampaignChanges+OverrideOnAgentShootMissile.Postfix), musztra
    ///    (MobilePartyTrainingBehavior.OnDailyTickParty - tam gra dodaje wojsku dobowe XP z modelu, ktory zmienia Drill/ArmsDrill), rozliczenie bitwy
    ///    (MapEventParty.CommitXpGain), Spoils of War (6 metod RealisticLoot - nasze SpoilsSeal.FencePrefix i GiftSkillXp biegna w ich srodku).
    ///    Kuznia = cala nauka Crafting bez znacznika (Forge / SmithMenu / SmeltTab / Helper, CraftingCampaignBehavior gry, BK - jedyne zrodla tej
    ///    umiejetnosci), bez dotykania 17 miejsc w kodzie kuzni. Reszta = "inne"; dla lordow AI i gracza probka stosu (najwyzej ok. 24 na dobe i grupe)
    ///    mowi, kto wolal - pokrycie znacznikow (prog P1-3: inne <= 20% XP lordow) widac z jednego biegu.
    ///  - wojsko: prefiks i postfiks na TroopRoster.AddXpToTroopAtIndex (wspolne ujscie: AddXpToTroop do niej przekazuje, MobilePartyHelper.PartyAddSharedXp
    ///    i BattleCampaignBehavior wolaja ja wprost) - zadane i PRZYJETE (po przycieciu gry w PartyBase.OnXpChanged) wedlug wlasciciela spisu i zrodla;
    ///    jency (tam "XP" to gotowosc do werbunku) i spisy bez wlasciciela (ekran partii) poza ksiega. Prefiks-licznik na AddXpToTroop (17 bajtow IL)
    ///    wymusza osobna kopie tej metody skompilowana PO oznaczeniu AddXpToTroopAtIndex "nie wklejaj" (latka wpinana przed nia).
    ///    XP niesione z ludzmi przy AddToCountsAtIndex(xpChange) i zdejmowane przy awansie (SetElementXp wprost) - poza ksiega (to nie nauka).
    /// Goracy punkt (kazdy cios, strzal, godzina marszu kazdej partii z wodzem): w prefiksach tylko tablice i liczniki - bez napisow i alokacji; napisy
    /// w liniach raz na dobe. Wyjatek = licznik potkniec (bez wylacznika). Znacznik zerwany wyjatkiem w metodzie gry (postfiks nie pobiegl) zdejmuje
    /// takt godziny i doby (licznik "zerwane"). Latki raz na proces, w kampanii (OnSessionLaunched), tylko przy wlaczonym SkillLedgerLog; wylaczony w
    /// trakcie gry = prefiksy wracaja od razu. Stan sesji czyszczony w konstruktorze ArmouryBehavior (Reset); bez zapisu.
    /// Linie: "Ksiega umiejetnosci (E8/P1): start sesji" (latki wpiete N/M), co dobe "Umiejetnosci (E8/P1): dzien N" i "XP wojska (E8/P1): dzien N"
    /// (za dobe od poprzedniego taktu, etykieta jak "Profil doby (E8/0)"), co 7 dob i przy pierwszym takcie sesji "Poziomy lordow (E8/P1)" (mediana i p90).
    /// Wspolne z E8/X: Enter / Leave (znacznik z naszego kodu), stale zrodel S*.
    /// </summary>
    internal static class SkillLedger
    {
        internal static bool On { get { var s = Settings.Current; return s != null && s.SkillLedgerLog; } }

        // ---- zrodla (znacznik); SOther = bez znacznika
        internal const int SOther = 0, SHit = 1, SShot = 2, SSim = 3, SBattle = 4, SFood = 5, SMarch = 6, SMarchAi = 7, STravel = 8, SDrill = 9, SForge = 10, SSpoils = 11,
                           STactics = 12, SArmy = 13, STrade = 14, SGovern = 15, SCharm = 16, SRecruit = 17, SHeal = 18, SRogue = 19, STrack = 20, SShip = 21, SManager = 22, Sources = 23;
        private static readonly string[] SrcNames =
        {
            "inne", "cios w misji", "strzal RBM", "bitwa symulowana", "rozliczenie bitwy", "jedzenie", "marsz", "marsz premia AI", "podroz", "musztra", "kuznia", "Spoils",
            "taktyka", "armia i oblezenie", "handel", "zarzad", "relacje", "werbunek i awanse", "leczenie", "lup i lotrostwo", "tropy i kryjowki", "statki", "menedzer gry reszta"
        };

        // ---- grupy bohaterow i wlasciciele spisow wojska
        private const int GPlayer = 0, GClan = 1, GLord = 2, GRest = 3, Groups = 4;
        private static readonly string[] GroupNames = { "gracz", "rod gracza", "lordowie AI", "inni" };
        private const int OPlayer = 0, OLord = 1, OGarrison = 2, ORest = 3, Owners = 4;
        private static readonly string[] OwnerNames = { "gracz i rod", "lordowie AI", "zalogi", "inne partie" };

        private static bool _on;                       // odswiezany przy starcie sesji, co godzine i co dobe - prefiksy czytaja pole, nie ustawienia
        [ThreadStatic] private static int _src;        // znacznik zrodla (projekt: ThreadStatic)
        private static Hero _main;
        private static Clan _pClan;

        // ---- bohaterowie: [grupa, umiejetnosc, zrodlo]
        private const int KMax = 48;                   // ostatni slot = "pozostale" (przepelnienie)
        private static readonly SkillObject[] _kObj = new SkillObject[KMax];
        private static readonly int[] _kDef = new int[KMax];
        private static int _kN;
        private static readonly double[] _xp = new double[Groups * KMax * Sources];
        private static readonly long[] _n = new long[Groups * Sources];
        private static readonly double[] _dXp = new double[Groups];
        private static readonly long[] _dN = new long[Groups];
        private static long _hCalls, _hSampled, _hTicks;

        // ---- wojsko
        private static readonly long[] _tReq = new long[Owners], _tGot = new long[Owners];
        private static readonly long[] _tSrcGot = new long[Sources], _tSrcN = new long[Sources];
        private static long _tNeg, _tNegN, _tAdd, _tCalls, _tSampled, _tTicks, _tOut;
        private static Func<TroopRoster, PartyBase> _owner;

        // ---- "inne" wedlug wolajacego (probka stosu): 0 lordowie AI, 1 gracz
        private const int CMax = 40, OCapDay = 24;
        private static readonly Type[] _cType = new Type[CMax];
        private static readonly double[] _cW = new double[2 * CMax];
        private static int _cN;
        private static readonly long[] _oSeen = new long[2], _oStride = { 32, 32 }, _oDay = new long[2], _oAll = new long[2];
        private static long _oTicks, _oTaken;

        // ---- znaczniki, koszt, stan linii
        private static long _mCalls;
        private static int _stale, _stumbles, _lastDay = -1;
        private static bool _had;
        private static long _selfTicks;
        private static double _nsMark = -1.0;

        // ---- latki (raz na proces)
        private static Harmony _h;
        private static bool _tried;
        private static int _want, _wired;
        private static readonly List<string> _missing = new List<string>();
        private static string _managers = "-";

        /// <summary>Nowa gra / wczytanie (konstruktor ArmouryBehavior): liczniki, tablica umiejetnosci (obiekty SkillObject sa nowe w kazdej kampanii) i probki od zera.</summary>
        internal static void Reset()
        {
            _on = false; _src = 0; _main = null; _pClan = null;
            ClearPeriod();
            Array.Clear(_kObj, 0, KMax); Array.Clear(_kDef, 0, KMax); _kN = 0;
            Array.Clear(_cType, 0, CMax); Array.Clear(_cW, 0, 2 * CMax); _cN = 0;
            for (int i = 0; i < 2; i++) { _oSeen[i] = 0; _oStride[i] = 32; _oDay[i] = 0; _oAll[i] = 0; }
            _stale = 0; _stumbles = 0; _lastDay = -1; _had = false; _selfTicks = 0; _nsMark = -1.0;
            XpFair.Reset();   // E8/X (248): liczniki linii "Doswiadczenie (E8/X)" - ten sam stan sesji (bez dopisku w konstruktorze ArmouryBehavior)
        }

        private static void ClearPeriod()
        {
            Array.Clear(_xp, 0, _xp.Length); Array.Clear(_n, 0, _n.Length); Array.Clear(_dXp, 0, Groups); Array.Clear(_dN, 0, Groups);
            _hCalls = _hSampled = _hTicks = 0;
            Array.Clear(_tReq, 0, Owners); Array.Clear(_tGot, 0, Owners); Array.Clear(_tSrcGot, 0, Sources); Array.Clear(_tSrcN, 0, Sources);
            _tNeg = _tNegN = _tAdd = _tCalls = _tSampled = _tTicks = _tOut = 0;
            _oTicks = _oTaken = 0; _mCalls = 0;
        }

        // ------------------------------------------------------------ znacznik zrodla
        /// <summary>Znacznik z naszego kodu (wspolne z E8/X): zwraca poprzedni stan do Leave. Wylaczona ksiega = bez zmiany.</summary>
        internal static int Enter(int src) { int p = _src; if (_on) { _src = src; _mCalls++; } return p; }
        internal static void Leave(int prev) { _src = prev; }

        // Prefiksy znacznikow: bez parametrow metody gry (nie zaleza od sygnatury, biegna takze przy zdjetej metodzie). __state = poprzedni znacznik + 1
        // (0 = prefiks nie pobiegl - postfiks nic nie rusza). Jedna metoda na zrodlo - stala w kodzie zamiast odczytu __originalMethod (wolny, alokuje).
        public static void M1(out int __state) { __state = _src + 1; if (_on) { _src = 1; _mCalls++; } }
        public static void M2(out int __state) { __state = _src + 1; if (_on) { _src = 2; _mCalls++; } }
        public static void M3(out int __state) { __state = _src + 1; if (_on) { _src = 3; _mCalls++; } }
        public static void M4(out int __state) { __state = _src + 1; if (_on) { _src = 4; _mCalls++; } }
        public static void M5(out int __state) { __state = _src + 1; if (_on) { _src = 5; _mCalls++; } }
        public static void M6(out int __state) { __state = _src + 1; if (_on) { _src = 6; _mCalls++; } }
        public static void M7(out int __state) { __state = _src + 1; if (_on) { _src = 7; _mCalls++; } }
        public static void M8(out int __state) { __state = _src + 1; if (_on) { _src = 8; _mCalls++; } }
        public static void M9(out int __state) { __state = _src + 1; if (_on) { _src = 9; _mCalls++; } }
        public static void M11(out int __state) { __state = _src + 1; if (_on) { _src = 11; _mCalls++; } }
        public static void M12(out int __state) { __state = _src + 1; if (_on) { _src = 12; _mCalls++; } }
        public static void M13(out int __state) { __state = _src + 1; if (_on) { _src = 13; _mCalls++; } }
        public static void M14(out int __state) { __state = _src + 1; if (_on) { _src = 14; _mCalls++; } }
        public static void M15(out int __state) { __state = _src + 1; if (_on) { _src = 15; _mCalls++; } }
        public static void M16(out int __state) { __state = _src + 1; if (_on) { _src = 16; _mCalls++; } }
        public static void M17(out int __state) { __state = _src + 1; if (_on) { _src = 17; _mCalls++; } }
        public static void M18(out int __state) { __state = _src + 1; if (_on) { _src = 18; _mCalls++; } }
        public static void M19(out int __state) { __state = _src + 1; if (_on) { _src = 19; _mCalls++; } }
        public static void M20(out int __state) { __state = _src + 1; if (_on) { _src = 20; _mCalls++; } }
        public static void M21(out int __state) { __state = _src + 1; if (_on) { _src = 21; _mCalls++; } }
        public static void M22(out int __state) { __state = _src + 1; if (_on) { _src = 22; _mCalls++; } }
        public static void MEnd(int __state) { if (__state > 0) _src = __state - 1; }

        /// <summary>Metoda ISkillLevelingManager -> zrodlo (nazwy z G ISkillLevelingManager.cs, 49 metod); nieznana (nowa wersja gry) = "menedzer gry reszta".</summary>
        private static int SourceOf(string name)
        {
            switch (name)
            {
                case "OnCombatHit": return SHit;
                case "OnSimulationCombatKill": return SSim;
                case "OnBattleEnded": return SBattle;
                case "OnFoodConsumed": return SFood;
                case "OnTraverseTerrain": return SMarch;
                case "OnAIPartiesTravel": return SMarchAi;
                case "OnTravelOnFoot": case "OnTravelOnHorse": case "OnTravelOnWater": return STravel;
                case "OnTacticsUsed": return STactics;
                case "OnLeadingArmy": case "OnSieging": case "OnSiegeEngineBuilt": case "OnSiegeEngineDestroyed": case "OnWallBreached": return SArmy;
                case "OnTradeProfitMade": case "OnWarehouseProduction": return STrade;
                case "OnSettlementGoverned": case "OnSettlementProjectFinished": case "OnInfluenceSpent": return SGovern;
                case "OnGainRelation": case "OnPersuasionSucceeded": case "OnBoardGameWonAgainstLord": return SCharm;
                case "OnTroopRecruited": case "OnUpgradeTroops": case "OnBanditsRecruited": return SRecruit;
                case "OnHeroHealedWhileWaiting": case "OnRegularTroopHealedWhileWaiting": case "OnSurgeryApplied": return SHeal;
                case "OnRaid": case "OnLoot": case "OnPrisonerSell": case "OnAIPartyLootCasualties": case "OnForceVolunteers": case "OnForceSupplies": case "OnBribeGiven":
                case "OnMainHeroReleasedFromCaptivity": case "OnMainHeroTortured": case "OnMainHeroDisguised": case "OnPrisonBreakEnd": case "OnAlleyCleared": case "OnDailyAlleyTick":
                case "OnHideoutClearedAsGhost": case "OnHideoutMissionEnd": return SRogue;
                case "OnHideoutSpotted": case "OnTrackDetected": return STrack;
                case "OnShipDamaged": case "OnShipRepaired": return SShip;
                default: return SManager;
            }
        }

        // ------------------------------------------------------------ bohaterowie
        private static int SkillIndex(SkillObject skill)
        {
            var a = _kObj; int n = _kN;
            for (int i = 0; i < n; i++) if ((object)a[i] == (object)skill) return i;
            if (n >= KMax - 1) return KMax - 1;
            a[n] = skill;
            _kDef[n] = (object)skill == (object)DefaultSkills.Crafting ? SForge : SOther;   // kuznia: cala nauka Crafting bez znacznika
            _kN = n + 1;
            return n;
        }

        private static int GroupOf(Hero h)
        {
            if (h == null) return GRest;
            if ((object)h == (object)_main) return GPlayer;
            Clan c = h.Clan;
            if (c != null && (object)c == (object)_pClan) return GClan;
            return h.IsLord ? GLord : GRest;
        }

        /// <summary>Prefiks-licznik HeroDeveloper.AddSkillXp (Priority.Last). Nic nie zmienia; bez alokacji poza probka stosu (ograniczona na dobe).</summary>
        public static void XpPrefix(HeroDeveloper __instance, SkillObject skill, float rawXp, bool isAffectedByFocusFactor)
        {
            if (!_on || !(rawXp > 0f) || float.IsInfinity(rawXp) || skill == null || __instance == null) return;
            try
            {
                long t = (++_hCalls & 63) == 1 ? Stopwatch.GetTimestamp() : 0L;
                int g = GroupOf(__instance.Hero);
                int stack = -1;
                if (!isAffectedByFocusFactor) { _dXp[g] += rawXp; _dN[g]++; }
                else
                {
                    int k = SkillIndex(skill);
                    int s = _src;
                    if (s <= SOther || s >= Sources) s = _kDef[k];
                    _xp[(g * KMax + k) * Sources + s] += rawXp;
                    _n[g * Sources + s]++;
                    if (s == SOther && (g == GLord || g == GPlayer))
                    {
                        int i = g == GLord ? 0 : 1;
                        if (++_oSeen[i] % _oStride[i] == 0 && _oDay[i] < 2 * OCapDay) stack = i;
                    }
                }
                if (t != 0) { _hTicks += Stopwatch.GetTimestamp() - t; _hSampled++; }
                if (stack >= 0) SampleCaller(stack, rawXp);
            }
            catch { _stumbles++; }
        }

        /// <summary>Kto wolal nauke bez znacznika: pierwsza ramka z typem spoza HeroDeveloper / Hero / tej klasy (kopie zalatanych metod Harmony nie maja
        /// typu - pomijane, liczy sie wolajacy wyzej). Waga = XP x krok probki. Typy w tablicy, nazwy dopiero w linii doby.</summary>
        private static void SampleCaller(int i, float rawXp)
        {
            long t = Stopwatch.GetTimestamp();
            try
            {
                _oDay[i]++; _oAll[i]++; _oTaken++;
                var st = new StackTrace(2, false);
                Type who = null;
                int n = Math.Min(st.FrameCount, 16);
                for (int f = 0; f < n; f++)
                {
                    var fr = st.GetFrame(f);
                    var m = fr != null ? fr.GetMethod() : null;
                    var dt = m != null ? m.DeclaringType : null;
                    if (dt == null || dt == typeof(HeroDeveloper) || dt == typeof(Hero) || dt == typeof(SkillLedger)) continue;
                    var asm = dt.Assembly;
                    if (asm.IsDynamic || asm.FullName.StartsWith("DMDASM", StringComparison.Ordinal)) continue;   // kopia zalatanej metody w bibliotece tworzonej w locie
                    who = dt; break;
                }
                int slot = -1;
                for (int c = 0; c < _cN; c++) if (_cType[c] == who) { slot = c; break; }
                if (slot < 0)
                {
                    if (_cN < CMax - 1) { slot = _cN++; _cType[slot] = who; }
                    else { slot = CMax - 1; _cType[slot] = typeof(SkillLedger); if (_cN < CMax) _cN = CMax; }   // przepelnienie: "pozostale"
                }
                _cW[i * CMax + slot] += (double)rawXp * _oStride[i];
            }
            catch { _stumbles++; }
            _oTicks += Stopwatch.GetTimestamp() - t;
        }

        // ------------------------------------------------------------ wojsko
        /// <summary>Prefiks TroopRoster.AddXpToTroop: tylko licznik (latka wymusza kopie metody z niewklejonym AddXpToTroopAtIndex).</summary>
        public static void TroopAddPrefix() { if (_on) _tAdd++; }

        /// <summary>Prefiks TroopRoster.AddXpToTroopAtIndex: stan sprzed i zadana kwota w __state (gra nadpisuje swoj argument - starg; 0 = nie liczone).</summary>
        public static void TroopPrefix(TroopRoster __instance, int index, int xpAmount, out long __state)
        {
            __state = 0L;
            if (!_on || xpAmount == 0 || __instance == null || index < 0 || index >= __instance.Count) return;
            __state = ((long)__instance.GetElementXp(index) << 32) | (uint)xpAmount;
        }

        public static void TroopPostfix(TroopRoster __instance, int index, long __state)
        {
            if (__state == 0L) return;
            try
            {
                long t = (++_tCalls & 63) == 1 ? Stopwatch.GetTimestamp() : 0L;
                int req = unchecked((int)(uint)(__state & 0xFFFFFFFFL)), before = (int)(__state >> 32);
                int o = OwnerOf(__instance);
                if (o < 0) _tOut++;
                else if (req < 0) { _tNeg += req; _tNegN++; }
                else
                {
                    long got = (long)__instance.GetElementXp(index) - before;
                    int s = _src;
                    if (s < 0 || s >= Sources) s = SOther;
                    _tReq[o] += req; _tGot[o] += got; _tSrcGot[s] += got; _tSrcN[s]++;
                }
                if (t != 0) { _tTicks += Stopwatch.GetTimestamp() - t; _tSampled++; }
            }
            catch { _stumbles++; }
        }

        /// <summary>Wlasciciel spisu: -1 = poza ksiega (jency - "XP" to gotowosc do werbunku; spis bez wlasciciela - ekran partii, kopie).</summary>
        private static int OwnerOf(TroopRoster r)
        {
            var get = _owner;
            if (get == null) return ORest;
            PartyBase p = get(r);
            if (p == null || (object)p.PrisonRoster == (object)r) return -1;
            MobileParty m = p.MobileParty;
            if (m == null) return ORest;
            if (m.IsGarrison) return OGarrison;
            if (m.IsMainParty) return OPlayer;
            if (!m.IsLordParty) return ORest;
            Clan c = m.ActualClan;
            return c != null && (object)c == (object)_pClan ? OPlayer : OLord;
        }

        // ------------------------------------------------------------ takt godziny i doby
        private static void Refresh()
        {
            _on = _tried && On;
            if (_src != 0) { _stale++; _src = 0; }   // znacznik zerwany wyjatkiem w metodzie gry (postfiks nie pobiegl) - takt biegnie poza kazda z nich
            try { _main = Hero.MainHero; _pClan = Clan.PlayerClan; } catch { }
        }

        internal static void Hourly() { Refresh(); }

        /// <summary>Z ArmouryBehavior.OnDailyTick: linie za dobe od poprzedniego taktu; pierwszy takt sesji bez linii doby (okres niepelny), z linia poziomow.</summary>
        internal static void Daily()
        {
            long t0 = Stopwatch.GetTimestamp();
            Refresh();
            if (!_on) { _had = false; return; }
            int day = (int)CampaignTime.Now.ToDays;
            try
            {
                if (_had)
                {
                    if (_nsMark < 0) Calibrate();
                    HeroLine();
                    TroopLine();
                }
                if (!_had || day % 7 == 0) LevelsLine(day);
            }
            catch (Exception e) { Log.Error("SkillLedger.Daily", e); }
            for (int i = 0; i < 2; i++) { _oStride[i] = Math.Max(1L, _oSeen[i] / OCapDay); _oSeen[i] = 0; _oDay[i] = 0; }
            ClearPeriod();
            _had = true; _lastDay = day;
            _selfTicks = Stopwatch.GetTimestamp() - t0;   // koszt linii - pokaze nastepna linia
        }

        private static void Calibrate()
        {
            // koszt pary prefiks + postfiks znacznika (cialo metod; skok latki Harmony poza pomiarem) - raz na sesje
            const int N = 4096;
            bool on = _on; int keep = _src; long mc = _mCalls;
            _on = true;
            int st;
            for (int i = 0; i < 64; i++) { M6(out st); MEnd(st); }
            long t0 = Stopwatch.GetTimestamp();
            for (int i = 0; i < N; i++) { M6(out st); MEnd(st); }
            long t1 = Stopwatch.GetTimestamp();
            _on = on; _src = keep; _mCalls = mc;
            _nsMark = (t1 - t0) * 1e9 / Stopwatch.Frequency / N;
        }

        // ------------------------------------------------------------ linie
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static double Ms(long ticks) { return ticks * 1000.0 / Stopwatch.Frequency; }
        private static string L(double v) { return ((long)Math.Round(v)).ToString(Inv); }
        private static string F1(double v) { return v.ToString("0.0", Inv); }
        private static string Pct(double part, double all) { return all > 0 ? (100.0 * part / all).ToString("0", Inv) + "%" : "0%"; }

        private sealed class Desc : IComparer<int>
        {
            private readonly double[] _v; private readonly int _off;
            public Desc(double[] v, int off) { _v = v; _off = off; }
            public int Compare(int a, int b) { int c = _v[_off + b].CompareTo(_v[_off + a]); return c != 0 ? c : a.CompareTo(b); }
        }

        /// <summary>" | etykieta: zrodlo XP/wywolan (udzial), ..., inne XP/wywolan (udzial)" - "inne" zawsze, na koncu (prog P1-3 czyta je z linii).</summary>
        private static void SrcPart(StringBuilder sb, string label, int g, double[] bySrc, double total)
        {
            sb.Append(" | ").Append(label).Append(": ");
            var ord = new int[Sources]; int n = 0;
            for (int s = 1; s < Sources; s++) if (bySrc[g * Sources + s] > 0) ord[n++] = s;
            Array.Sort(ord, 0, n, new Desc(bySrc, g * Sources));
            for (int i = 0; i < n; i++)
            {
                int s = ord[i];
                sb.Append(SrcNames[s]).Append(' ').Append(L(bySrc[g * Sources + s])).Append('/').Append(_n[g * Sources + s]).Append(" (").Append(Pct(bySrc[g * Sources + s], total)).Append("), ");
            }
            sb.Append(SrcNames[SOther]).Append(' ').Append(L(bySrc[g * Sources])).Append('/').Append(_n[g * Sources]).Append(" (").Append(Pct(bySrc[g * Sources], total)).Append(')');
        }

        private static void SkillPart(StringBuilder sb, string label, int g, double[] bySkill)
        {
            sb.Append(" | ").Append(label).Append(": ");
            var ord = new int[KMax]; int n = 0;
            for (int k = 0; k < KMax; k++) if (bySkill[g * KMax + k] > 0) ord[n++] = k;
            if (n == 0) { sb.Append('-'); return; }
            Array.Sort(ord, 0, n, new Desc(bySkill, g * KMax));
            for (int i = 0; i < n && i < 8; i++)
            {
                int k = ord[i];
                if (i > 0) sb.Append(", ");
                sb.Append(k == KMax - 1 || _kObj[k] == null ? "pozostale" : _kObj[k].StringId).Append(' ').Append(L(bySkill[g * KMax + k]));
            }
        }

        private static void CallerPart(StringBuilder sb, int i, string label)
        {
            sb.Append(label).Append(' ').Append(_oAll[i]).Append(" probek [");
            double all = 0;
            for (int c = 0; c < _cN; c++) all += _cW[i * CMax + c];
            var ord = new int[CMax]; int n = 0;
            for (int c = 0; c < _cN; c++) if (_cW[i * CMax + c] > 0) ord[n++] = c;
            Array.Sort(ord, 0, n, new Desc(_cW, i * CMax));
            for (int k = 0; k < n && k < 6; k++)
            {
                int c = ord[k];
                if (k > 0) sb.Append(", ");
                var t = _cType[c];
                sb.Append(t == null ? "bez typu" : (t == typeof(SkillLedger) ? "pozostale" : (t.FullName ?? t.Name))).Append(' ').Append(Pct(_cW[i * CMax + c], all));
            }
            if (n == 0) sb.Append('-');
            sb.Append(']');
        }

        private static void HeroLine()
        {
            var tot = new double[Groups];
            var cnt = new long[Groups];
            var bySrc = new double[Groups * Sources];
            var bySkill = new double[Groups * KMax];
            for (int g = 0; g < Groups; g++)
            {
                for (int k = 0; k < KMax; k++)
                    for (int s = 0; s < Sources; s++)
                    {
                        double v = _xp[(g * KMax + k) * Sources + s];
                        if (v == 0) continue;
                        tot[g] += v; bySrc[g * Sources + s] += v; bySkill[g * KMax + k] += v;
                    }
                for (int s = 0; s < Sources; s++) cnt[g] += _n[g * Sources + s];
            }
            var sb = new StringBuilder(2048);
            sb.Append("Umiejetnosci (E8/P1): dzien ").Append(_lastDay).Append(" | surowe XP/wywolan:");
            for (int g = 0; g < Groups; g++) sb.Append(g == 0 ? " " : ", ").Append(GroupNames[g]).Append(' ').Append(L(tot[g])).Append('/').Append(cnt[g]);
            SrcPart(sb, "zrodla lordow AI", GLord, bySrc, tot[GLord]);
            SrcPart(sb, "zrodla gracza", GPlayer, bySrc, tot[GPlayer]);
            SkillPart(sb, "umiejetnosci lordow AI", GLord, bySkill);
            SkillPart(sb, "umiejetnosci gracza", GPlayer, bySkill);
            sb.Append(" | XP wprost poza suma (ChangeSkillLevel i inne bez wspolczynnika nauki):");
            for (int g = 0; g < Groups; g++) sb.Append(g == 0 ? " " : ", ").Append(GroupNames[g]).Append(' ').Append(L(_dXp[g])).Append('/').Append(_dN[g]);
            sb.Append(" | inne wedlug wolajacego (sesja, probka stosu): ");
            CallerPart(sb, 0, "lordowie AI");
            sb.Append("; ");
            CallerPart(sb, 1, "gracz");
            double hMs = _hSampled > 0 ? Ms(_hTicks) * ((double)_hCalls / _hSampled) : 0.0;
            double tMs = _tSampled > 0 ? Ms(_tTicks) * ((double)_tCalls / _tSampled) : 0.0;
            double mMs = _mCalls * Math.Max(0.0, _nsMark) / 1e6;
            double oMs = Ms(_oTicks), lMs = Ms(_selfTicks);
            sb.Append(" | znaczniki ").Append(_mCalls).Append(", zerwane ").Append(_stale).Append(", potkniecia ").Append(_stumbles);
            sb.Append(" | koszt ksiegi ").Append(F1(hMs + tMs + mMs + oMs + lMs)).Append(" ms (bohaterowie ").Append(F1(hMs)).Append(" ms/").Append(_hCalls)
              .Append(", wojsko ").Append(F1(tMs)).Append(" ms/").Append(_tCalls).Append(", znaczniki ~").Append(F1(mMs)).Append(" ms x ").Append(Math.Max(0.0, _nsMark).ToString("0.0", Inv))
              .Append(" ns, probki stosu ").Append(F1(oMs)).Append(" ms/").Append(_oTaken).Append(", linie poprzedniej doby ").Append(F1(lMs)).Append(" ms; bez skoku latek Harmony)");
            Log.Info(sb.ToString());
        }

        private static void TroopLine()
        {
            long req = 0, got = 0, calls = 0;
            for (int o = 0; o < Owners; o++) { req += _tReq[o]; got += _tGot[o]; }
            for (int s = 0; s < Sources; s++) calls += _tSrcN[s];
            var sb = new StringBuilder(700);
            sb.Append("XP wojska (E8/P1): dzien ").Append(_lastDay).Append(" | dodane ").Append(req).Append(" (przyjete ").Append(got).Append(") / ").Append(calls).Append(" wywolan |");
            for (int o = 0; o < Owners; o++) sb.Append(o == 0 ? " " : ", ").Append(OwnerNames[o]).Append(' ').Append(_tReq[o]).Append(" (przyjete ").Append(_tGot[o]).Append(')');
            sb.Append(" | zrodla (przyjete/wywolan): ");
            var v = new double[Sources];
            for (int s = 0; s < Sources; s++) v[s] = _tSrcGot[s];
            var ord = new int[Sources]; int n = 0;
            for (int s = 1; s < Sources; s++) if (_tSrcN[s] > 0) ord[n++] = s;
            Array.Sort(ord, 0, n, new Desc(v, 0));
            for (int i = 0; i < n; i++)
            {
                int s = ord[i];
                sb.Append(SrcNames[s]).Append(' ').Append(_tSrcGot[s]).Append('/').Append(_tSrcN[s]).Append(" (").Append(Pct(_tSrcGot[s], got)).Append("), ");
            }
            sb.Append(SrcNames[SOther]).Append(' ').Append(_tSrcGot[SOther]).Append('/').Append(_tSrcN[SOther]).Append(" (").Append(Pct(_tSrcGot[SOther], got)).Append(')');
            sb.Append(" | zdjete ").Append(_tNeg).Append(" / ").Append(_tNegN).Append(" | poza ksiega (jency, spisy bez wlasciciela) ").Append(_tOut)
              .Append(" | AddXpToTroop ").Append(_tAdd).Append(_owner == null ? " | wlasciciel spisu: BRAK odczytu (wszystko w \"inne partie\", jency w sumie)" : "");
            Log.Info(sb.ToString());
        }

        /// <summary>Co 7 dob (i przy pierwszym takcie sesji): mediana i p90 poziomu kazdej umiejetnosci doroslych, zywych lordow AI (bez rodu gracza).</summary>
        private static void LevelsLine(int day)
        {
            var skills = Skills.All;
            int ns = skills != null ? skills.Count : 0;
            if (ns == 0) return;
            var lists = new List<int>[ns];
            for (int i = 0; i < ns; i++) lists[i] = new List<int>(2048);
            var sums = new List<int>(2048);
            Clan pc = Clan.PlayerClan;
            foreach (var h in Hero.AllAliveHeroes)
            {
                if (h == null || !h.IsLord || h.IsChild) continue;
                Clan c = h.Clan;
                if (c == null || c == pc) continue;
                int sum = 0;
                for (int i = 0; i < ns; i++) { int lv = h.GetSkillValue(skills[i]); lists[i].Add(lv); sum += lv; }
                sums.Add(sum);
            }
            int n = sums.Count;
            var sb = new StringBuilder(900);
            sb.Append("Poziomy lordow (E8/P1): dzien ").Append(day).Append(" | lordowie AI ").Append(n).Append(" (dorosli, zywi, bez rodu gracza)");
            if (n > 0)
            {
                sums.Sort();
                sb.Append(" | suma poziomow mediana ").Append(sums[n / 2]).Append(", p90 ").Append(sums[Math.Min(n - 1, n * 9 / 10)]).Append(" | mediana/p90:");
                for (int i = 0; i < ns; i++)
                {
                    lists[i].Sort();
                    sb.Append(i == 0 ? " " : ", ").Append(skills[i].StringId).Append(' ').Append(lists[i][n / 2]).Append('/').Append(lists[i][Math.Min(n - 1, n * 9 / 10)]);
                }
            }
            Log.Info(sb.ToString());
        }

        // ------------------------------------------------------------ latki (raz na proces, w kampanii)
        /// <summary>Z OnSessionLaunched: przy wlaczonym kluczu wpina latki (raz na proces) i pisze linie startu sesji; wylaczony = nic nie wpina.</summary>
        internal static void EnsureHooks()
        {
            try
            {
                _on = false;
                if (Campaign.Current == null) return;
                if (!On)
                {
                    Log.Info("Ksiega umiejetnosci (E8/P1): WYLACZONA (SkillLedgerLog off) - " + (_tried ? "latki z wczesniejszej sesji zostaja, liczniki stoja." : "latki niewpiete."));
                    return;
                }
                if (!_tried) { _tried = true; Hook(); }
                Refresh();
                _stale = 0;
                Log.Info("Ksiega umiejetnosci (E8/P1): start sesji - latki wpiete " + _wired + "/" + _want + " (AddSkillXp, wojsko 2, menedzer umiejetnosci: " + _managers
                         + ", strzal RBM, musztra, rozliczenie bitwy, Spoils 6); wlasciciel spisu wojska: " + (_owner != null ? "odczyt jest" : "BRAK odczytu")
                         + (_missing.Count > 0 ? " | nie znaleziono: " + string.Join(", ", _missing.ToArray()) : "") + ".");
            }
            catch (Exception e) { Log.Error("SkillLedger.EnsureHooks", e); }
        }

        private static void Mark(string label, MethodBase m, int src)
        {
            _want++;
            try
            {
                if (m == null || m.IsAbstract) { _missing.Add(label); return; }
                _h.Patch(m, prefix: new HarmonyMethod(typeof(SkillLedger), "M" + src) { priority = Priority.First },
                            postfix: new HarmonyMethod(typeof(SkillLedger), nameof(MEnd)) { priority = Priority.Last });
                _wired++;
            }
            catch (Exception e) { _missing.Add(label + " (blad: " + e.Message + ")"); }
        }

        private static void Count(string label, MethodBase m, string prefix, string postfix, int priority)
        {
            _want++;
            try
            {
                if (m == null) { _missing.Add(label); return; }
                _h.Patch(m, prefix: prefix != null ? new HarmonyMethod(typeof(SkillLedger), prefix) { priority = priority } : null,
                            postfix: postfix != null ? new HarmonyMethod(typeof(SkillLedger), postfix) { priority = priority } : null);
                _wired++;
            }
            catch (Exception e) { _missing.Add(label + " (blad: " + e.Message + ")"); }
        }

        private static MethodBase Declared(Type t, string name)
        {
            if (t == null) return null;
            try
            {
                foreach (var m in t.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                    if (m.Name == name && !m.IsAbstract && !m.IsGenericMethodDefinition) return m;
            }
            catch { }
            return null;
        }

        private static void Hook()
        {
            try { if (_h == null) _h = new Harmony("com.jeff.armoury.skillledger"); }
            catch (Exception e) { _want++; _missing.Add("Harmony (blad: " + e.Message + ")"); return; }

            // 1. bohaterowie
            MethodBase add = null;
            try { add = AccessTools.Method(typeof(HeroDeveloper), "AddSkillXp", new[] { typeof(SkillObject), typeof(float), typeof(bool), typeof(bool) }); } catch { }
            Count("HeroDeveloper.AddSkillXp", add, nameof(XpPrefix), null, Priority.Last);

            // 2. wojsko: najpierw AddXpToTroopAtIndex (dostaje znacznik "nie wklejaj"), potem AddXpToTroop (jej kopia kompiluje sie juz z wywolaniem)
            try
            {
                var g = AccessTools.PropertyGetter(typeof(TroopRoster), "OwnerParty");
                if (g != null) _owner = (Func<TroopRoster, PartyBase>)Delegate.CreateDelegate(typeof(Func<TroopRoster, PartyBase>), g);
            }
            catch { _owner = null; }
            MethodBase atIndex = null, addTroop = null;
            try { atIndex = AccessTools.Method(typeof(TroopRoster), "AddXpToTroopAtIndex", new[] { typeof(int), typeof(int) }); } catch { }
            try { addTroop = AccessTools.Method(typeof(TroopRoster), "AddXpToTroop", new[] { typeof(CharacterObject), typeof(int) }); } catch { }
            Count("TroopRoster.AddXpToTroopAtIndex", atIndex, nameof(TroopPrefix), nameof(TroopPostfix), Priority.Normal);
            Count("TroopRoster.AddXpToTroop", addTroop, nameof(TroopAddPrefix), null, Priority.Normal);

            // 3. menedzer umiejetnosci: DefaultSkillLevelingManager + czynny menedzer i jego lancuch (pole typu ISkillLevelingManager - NavalDLC)
            try
            {
                var types = new List<Type> { typeof(DefaultSkillLevelingManager) };
                object cur = Campaign.Current.SkillLevelingManager;
                for (int depth = 0; cur != null && depth < 4; depth++)
                {
                    var t = cur.GetType();
                    if (!types.Contains(t)) types.Add(t);
                    object next = null;
                    foreach (var f in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                        if (typeof(ISkillLevelingManager).IsAssignableFrom(f.FieldType)) { next = f.GetValue(cur); break; }
                    cur = next;
                }
                var names = new List<string>();
                foreach (var t in types)
                {
                    int before = _wired;
                    var map = t.GetInterfaceMap(typeof(ISkillLevelingManager));
                    for (int i = 0; i < map.InterfaceMethods.Length; i++)
                    {
                        var target = map.TargetMethods[i];
                        if (target == null || target.DeclaringType != t) continue;   // odziedziczona - zalatana w typie bazowym
                        Mark(t.Name + "." + map.InterfaceMethods[i].Name, target, SourceOf(map.InterfaceMethods[i].Name));
                    }
                    names.Add(t.Name + " " + (_wired - before));
                }
                _managers = string.Join(" + ", names.ToArray());
            }
            catch (Exception e) { _want++; _missing.Add("menedzer umiejetnosci (blad: " + e.Message + ")"); }

            // 4. znani wolajacy poza menedzerem
            Type rbm = null;
            try { rbm = AccessTools.TypeByName("RBMCombat.CampaignChanges+OverrideOnAgentShootMissile"); } catch { }
            Mark("strzal RBM (RBMCombat.CampaignChanges+OverrideOnAgentShootMissile.Postfix)", Declared(rbm, "Postfix"), SShot);
            Mark("MobilePartyTrainingBehavior.OnDailyTickParty", Declared(typeof(MobilePartyTrainingBehavior), "OnDailyTickParty"), SDrill);
            Mark("MapEventParty.CommitXpGain", Declared(typeof(MapEventParty), "CommitXpGain"), SBattle);
            string[][] spoils =
            {
                new[] { "RealisticLoot.Behaviors.DonateEquipmentBehavior", "OnDonateScreenClosed" },
                new[] { "RealisticLoot.Behaviors.LootCollectionBehavior", "ExecuteFenceSale" },
                new[] { "RealisticLoot.Behaviors.LootCollectionBehavior", "GiveLeftoverXpToTroops" },
                new[] { "RealisticLoot.Behaviors.LootCollectionBehavior", "AwardRogueryXp" },
                new[] { "RealisticLoot.Behaviors.QuartermasterBehavior", "OnFoodScreenClosed" },
                new[] { "RealisticLoot.Behaviors.SubClanBehavior", "OnEquipLeaderScreenClosed" }
            };
            foreach (var sp in spoils)
            {
                Type t = null;
                try { t = AccessTools.TypeByName(sp[0]); } catch { }
                Mark("Spoils " + sp[0].Substring(sp[0].LastIndexOf('.') + 1) + "." + sp[1], Declared(t, sp[1]), SSpoils);
            }
        }
    }
}
