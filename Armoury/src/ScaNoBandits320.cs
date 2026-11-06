using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// 320b (Jeff 02.11 "i niech korona przestanie liczyc bandytow i Innych jako wroga"). Klucz StrategicAiIgnoresBandits.
    ///
    /// Przyczyna (StrategicCampaignAI145.dll, dekompilacja sca320/a): StrategicCampaignAI145Behavior.CalculateFactionStatus(Kingdom) (private static,
    /// zadeklarowana w tej klasie; wolana z OnDailyTick i co 4 h z OnHourlyTick przed SetFactionStatus/UpdateWarGoal) sumuje EnemyStrength jako
    /// CurrentTotalStrength WSZYSTKICH kingdom.FactionsAtWarWith - takze klanow band (IsBanditFaction, stala wojna z DefaultDiplomacyModel), Innych
    /// (ROTclan_126, kultura whitewalker, is_outlaw) i mniejszych frakcji wyjetych spod prawa (Bractwo bez Choragwi itp.). StrategicFactionStatus.IsExhausted:
    /// wrog > swoi x 1.35 -> ForcePeace / WantsPeace, a StrategicTargetScoreModel tlumi cele oblezen x0.19 i podbija obrone x1.7.
    ///
    /// Zmiana: postfiks na CalculateFactionStatus (DeclaredMethod na klasie deklarujacej, sprawdzony Harmony.GetPatchInfo po wlascicielu) liczy EnemyStrength
    /// od nowa bez band, Innych i frakcji wyjetych spod prawa (krolestwa, klany zbuntowane i inne klany z wojna zostaja). ActiveWars i RaidedVillages bez zmian.
    /// WantsPeace i IsExhausted SCA przelicza sam (SetFactionStatus po postfiksie).
    ///
    /// 320e (Jeff 02.11 "1. tak 2. tak" - korona przy zablokowanym pokoju ma normalnie walczyc i oblegac zamiast "chciec pokoju"):
    ///  (b) klucz StrategicAiIgnoresBandits: ten sam postfiks liczy od nowa ThreatenedFortifications - twierdza zagrozona, gdy oblezona przez wroga, ktorego Skip
    ///      nie pomija, albo gdy sila wrogich partii lordow w promieniu 80 (jak SCA) bez partii band, Innych i wyjetych spod prawa przewyzsza EstimatedStrength
    ///      twierdzy. Wspolny pomocnik SCA NearbyEnemyLordStrength bez latki (uzywa go tez wybor celow i obrona).
    ///  (a) klucz StrategicAiFightsOnWhenPeaceBlocked: krolestwo, ktorego WSZYSTKIE wojny z krolestwami (Skip 0) maja pokoj zablokowany przez CanonPeaceLock (264,
    ///      Blocks - ta sama regula, ktora zatrzymuje glosowania i zmeczenie wojna), nie dostaje celu ForcePeace ani WantsPeace - i tak nie moze zawrzec pokoju.
    ///      Postfiks UpdateWarGoal: cel ForcePeace (nowy albo stary z zapisu) zastapiony nastepnym, ktory SCA wybiera bez wyczerpania (DefendCapitalRegion /
    ///      ReclaimLostFief / WeakenEconomy / BorderWar, pomocnikami SCA). Postfiksy OnDailyTick i OnHourlyTick: WantsPeace = false (SetFactionStatus liczy go
    ///      od nowa po UpdateWarGoal, a sama SetFactionStatus jest mala - ryzyko wkompilowania, wiec bez latki na niej). IsExhausted zostaje (cel oblezen x0.55,
    ///      obrona x1.7 - prawdziwa przewaga wroga; decyzja dla Jeffa).
    ///      Zapas: postfiks ticku zamienia tez zapisany ForcePeace (gdyby UpdateWarGoal zostal wkompilowany w petle ticku) - licznik "w ticku" w linii doby. Wojny z klanami (nie krolestwami) nie licza sie do reguly - pokoj z nimi to nie glosowanie.
    /// Klucze na zywo: wylaczony = postfiks nic nie zmienia. Potkniecia liczone, bez globalnego wylacznika. Tylko odczyt cudzych struktur + zapis wlasciwosci.
    /// Linie: "SCA bez band (320b): start sesji ..." (latki, cel) i co dobe "SCA bez band (320b): dzien N ..." (na krolestwo wrog przed/po, zagrozone twierdze
    /// przed/po, cel przed/po z powodem, chce pokoju przed/po).
    /// </summary>
    internal static class ScaNoBandits320
    {
        private const string HarmonyId = "com.jeff.armoury.scanobandits320";
        internal const string P = "SCA bez band (320b): ";
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private sealed class Rec
        {
            public string Name;
            public float Before, After, Bandits, Others, Outlaws;
            public int NBandits, NOthers, NOutlaws, NKept;
            public int ThreatSca = -1, ThreatOurs = -1;              // 320e (b)
            public bool Locked;                                       // 320e (a): wszystkie wojny z krolestwami z blokada pokoju
            public string Why = "-";
            public string GoalSca, GoalOurs;                          // ostatni UpdateWarGoal: cel SCA i po naszej zmianie
            public bool PeaceSca, PeaceSeen;                          // ostatnie WantsPeace SCA przed wyczyszczeniem
        }

        private static bool _hookTried, _wired, _wiredGoal, _wiredTicks;
        private static string _hookNote = "-", _hookNoteE = "-";
        private static PropertyInfo _pEnemy;                        // StrategicFactionStatus.EnemyStrength (internal klasa, publiczny setter)
        private static PropertyInfo _pOwn, _pForts, _pThreat, _pWars, _pExh, _pPeace, _pGoal, _pRaided;
        private static MethodInfo _mGetStatus;                      // StrategicAiState.GetFactionStatus(IFaction)
        private static MethodInfo _mGetGoal, _mSetGoal, _mLostClaim, _mNearby;   // 320e: StrategicAiState.GetWarGoal/SetWarGoal/GetLostClaim, StrategicAiHelpers.NearbyEnemyLordStrength
        private static Type _goalType;
        private static object _gForce, _gDefend, _gReclaim, _gWeaken, _gBorder;
        private static readonly Dictionary<string, Rec> _last = new Dictionary<string, Rec>();   // StringId krolestwa -> ostatnia ocena
        private static readonly List<Kingdom> _pending = new List<Kingdom>();                    // 320e: krolestwa z blokada z biezacej rundy (WantsPeace do wyczyszczenia)
        private static int _dEvals, _dChanged, _stumbles, _dStumbles, _dGoalSwaps, _dGoalLate, _dPeaceCleared, _dThreatChanged;
        private static readonly HashSet<string> _errSites = new HashSet<string>();

        internal static bool On { get { var s = Settings.Current; return s != null && s.StrategicAiIgnoresBandits; } }
        internal static bool LockOn { get { var s = Settings.Current; return s != null && s.StrategicAiFightsOnWhenPeaceBlocked; } }

        internal static void Reset() { _last.Clear(); _pending.Clear(); _dEvals = _dChanged = _stumbles = _dStumbles = _dGoalSwaps = _dGoalLate = _dPeaceCleared = _dThreatChanged = 0; _errSites.Clear(); }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++; _dStumbles++;
            try { if (_errSites.Add(where)) Log.Error("ScaNoBandits320." + where + " (potkniecie " + _stumbles + ")", e); } catch { }
        }

        /// <summary>Latka + sprawdzenie GetPatchInfo po wlascicielu; true = nasza latka siedzi. others - cudze latki na tym celu.</summary>
        private static bool PatchChecked(Harmony h, MethodInfo m, string patchName, ref int others)
        {
            h.Patch(m, postfix: new HarmonyMethod(typeof(ScaNoBandits320), patchName) { priority = Priority.Normal });
            bool ok = false;
            var info = Harmony.GetPatchInfo(m);
            if (info == null) return false;
            foreach (var p in info.Postfixes) { if (p == null) continue; if (p.owner == HarmonyId && p.PatchMethod != null && p.PatchMethod.Name == patchName) ok = true; else if (p.owner != HarmonyId) others++; }
            foreach (var p in info.Prefixes) if (p != null && p.owner != HarmonyId) others++;
            foreach (var p in info.Transpilers) if (p != null && p.owner != HarmonyId) others++;
            foreach (var p in info.Finalizers) if (p != null && p.owner != HarmonyId) others++;
            return ok;
        }

        // ------------------------------------------------------------ latka (raz na proces, w kampanii)
        internal static void EnsureHooks()
        {
            Wire();
            StartLog();
        }

        /// <summary>334c (B334-1): samo wpiecie latek (raz na proces), bez linii startu - WarFront334.SessionStart wola je przed swoja linia startu, zeby stan latek
        /// SCA w linii 334 nie zalezal od kolejnosci sluchaczy OnSessionLaunched. Ponowne wywolanie nic nie robi (_hookTried).</summary>
        internal static void Wire()
        {
            if (!_hookTried)
            {
                _hookTried = true;
                Type t = null;
                try
                {
                    t = AccessTools.TypeByName("StrategicCampaignAI145.StrategicCampaignAI145Behavior");
                    var m = t != null ? AccessTools.DeclaredMethod(t, "CalculateFactionStatus", new[] { typeof(Kingdom) }) : null;
                    if (t == null) _hookNote = "bez SCA (brak StrategicCampaignAI145.StrategicCampaignAI145Behavior)";
                    else if (m == null) _hookNote = "BRAK (StrategicCampaignAI145Behavior.CalculateFactionStatus(Kingdom))";
                    else
                    {
                        var st = m.ReturnType;
                        _pEnemy = AccessTools.Property(st, "EnemyStrength");
                        _pOwn = AccessTools.Property(st, "OwnedStrength");
                        _pForts = AccessTools.Property(st, "OwnedFortifications");
                        _pThreat = AccessTools.Property(st, "ThreatenedFortifications");
                        _pWars = AccessTools.Property(st, "ActiveWars");
                        _pExh = AccessTools.Property(st, "IsExhausted");
                        _pPeace = AccessTools.Property(st, "WantsPeace");
                        _pGoal = AccessTools.Property(st, "WarGoal");
                        _pRaided = AccessTools.Property(st, "RaidedVillages");
                        var stt = AccessTools.TypeByName("StrategicCampaignAI145.StrategicAiState");
                        _mGetStatus = stt != null ? AccessTools.DeclaredMethod(stt, "GetFactionStatus", new[] { typeof(IFaction) }) : null;
                        if (_pEnemy == null || _pEnemy.GetSetMethod(true) == null) _hookNote = "BRAK (StrategicFactionStatus.EnemyStrength bez settera)";
                        else
                        {
                            var h = new Harmony(HarmonyId);
                            int others = 0;
                            _wired = PatchChecked(h, m, nameof(CalcPostfix), ref others);
                            _hookNote = (_wired ? "wpieta" : "BRAK (latka nie weszla - slad w BKROTLog.txt)") + " (" + m.DeclaringType.FullName + "." + m.Name + "(Kingdom)"
                                        + (others > 0 ? ", cudzych latek " + others : "") + ")";
                        }
                    }
                }
                catch (Exception e) { _hookNote = "BRAK (" + e.GetType().Name + ": " + e.Message + ")"; Stumble("EnsureHooks", e); }
                // 320e (a): cel wojny i WantsPeace przy zablokowanym pokoju
                try
                {
                    if (t == null || !_wired) _hookNoteE = "BRAK (bez latki CalculateFactionStatus)";
                    else
                    {
                        var stt = AccessTools.TypeByName("StrategicCampaignAI145.StrategicAiState");
                        var hlp = AccessTools.TypeByName("StrategicCampaignAI145.StrategicAiHelpers");
                        _goalType = AccessTools.TypeByName("StrategicCampaignAI145.StrategicWarGoal");
                        _mGetGoal = stt != null ? AccessTools.DeclaredMethod(stt, "GetWarGoal", new[] { typeof(Kingdom) }) : null;
                        _mSetGoal = stt != null && _goalType != null ? AccessTools.DeclaredMethod(stt, "SetWarGoal", new[] { typeof(Kingdom), _goalType }) : null;
                        _mLostClaim = stt != null ? AccessTools.DeclaredMethod(stt, "GetLostClaim", new[] { typeof(Kingdom) }) : null;
                        _mNearby = hlp != null ? AccessTools.DeclaredMethod(hlp, "NearbyEnemyLordStrength", new[] { typeof(Settlement), typeof(IFaction), typeof(float) }) : null;
                        var mGoal = AccessTools.DeclaredMethod(t, "UpdateWarGoal", new[] { typeof(Kingdom) });
                        var mDay = AccessTools.DeclaredMethod(t, "OnDailyTick", Type.EmptyTypes);
                        var mHour = AccessTools.DeclaredMethod(t, "OnHourlyTick", Type.EmptyTypes);
                        if (_goalType != null)
                        {
                            _gForce = Enum.Parse(_goalType, "ForcePeace"); _gDefend = Enum.Parse(_goalType, "DefendCapitalRegion"); _gReclaim = Enum.Parse(_goalType, "ReclaimLostFief");
                            _gWeaken = Enum.Parse(_goalType, "WeakenEconomy"); _gBorder = Enum.Parse(_goalType, "BorderWar");
                        }
                        if (_goalType == null || _mGetGoal == null || _mSetGoal == null || _mLostClaim == null || _mNearby == null || mGoal == null || mDay == null || mHour == null
                            || _pPeace == null || _pGoal == null || _pRaided == null || _mGetStatus == null)
                            _hookNoteE = "BRAK (brak celu SCA: typ celu " + (_goalType != null) + ", Get/SetWarGoal " + (_mGetGoal != null && _mSetGoal != null) + ", GetLostClaim " + (_mLostClaim != null)
                                         + ", NearbyEnemyLordStrength " + (_mNearby != null) + ", UpdateWarGoal " + (mGoal != null) + ", OnDailyTick/OnHourlyTick " + (mDay != null && mHour != null) + ")";
                        else
                        {
                            var h = new Harmony(HarmonyId);
                            int others = 0;
                            _wiredGoal = PatchChecked(h, mGoal, nameof(GoalPostfix), ref others);
                            bool d = PatchChecked(h, mDay, nameof(TickPostfix), ref others);
                            bool hr = PatchChecked(h, mHour, nameof(TickPostfix), ref others);
                            _wiredTicks = d && hr;
                            _hookNoteE = "UpdateWarGoal " + (_wiredGoal ? "wpieta" : "BRAK") + ", OnDailyTick " + (d ? "wpieta" : "BRAK") + ", OnHourlyTick " + (hr ? "wpieta" : "BRAK")
                                         + (others > 0 ? ", cudzych latek " + others : "") + ((_wiredGoal && _wiredTicks) ? "" : " - slad w BKROTLog.txt");
                        }
                    }
                }
                catch (Exception e) { _hookNoteE = "BRAK (" + e.GetType().Name + ": " + e.Message + ")"; Stumble("EnsureHooks.E", e); }
            }
        }

        private static void StartLog()
        {
            try
            {
                Log.Info(P + "start sesji - latka " + _hookNote + ", klucz Strategic AI Ignores Bandits " + (On ? "wlaczony" : "WYLACZONY")
                         + " | wrog SCA i zagrozone twierdze bez band (IsBanditFaction), Innych (kultura whitewalker) i frakcji wyjetych spod prawa (IsOutlaw); liczba wojen i wioski bez zmian"
                         + " | 320e pokoj zablokowany: latki " + _hookNoteE + ", klucz Strategic AI Fights On When Peace Blocked " + (LockOn ? "wlaczony" : "WYLACZONY")
                         + " - bez ForcePeace i WantsPeace, gdy wszystkie wojny z krolestwami maja pokoj zablokowany (264).");
            }
            catch (Exception e) { Stumble("StartLine", e); }
        }

        /// <summary>334 linia startu: latka CalculateFactionStatus.</summary>
        internal static string WiredStatus { get { return _wired ? "ok" : "BRAK"; } }
        /// <summary>334 linia startu: latki celu (UpdateWarGoal + ticki).</summary>
        internal static string WiredGoal { get { return _wiredGoal && _wiredTicks ? "ok" : "BRAK"; } }

        /// <summary>334 linia 3: swoi, wyczerpane, cel (SCA -> nasz, powod 320e/334), chce pokoju - dla krolestwa (tylko odczyt).</summary>
        internal static string Brief(Kingdom k)
        {
            try
            {
                if (k == null || !_wired) return "SCA brak";
                object st = _mGetStatus != null ? _mGetStatus.Invoke(null, new object[] { k }) : null;
                if (st == null) return "SCA brak oceny";
                Rec r; _last.TryGetValue(k.StringId, out r);
                bool exh = Get(_pExh, st) is bool b1 && b1, peace = Get(_pPeace, st) is bool b2 && b2;
                return "swoi " + F0(Get(_pOwn, st)) + ", wyczerpane " + (exh ? "tak" : "nie") + ", cel " + (r != null ? (r.GoalSca ?? "?") : "?") + "->" + (Get(_pGoal, st) ?? "?")
                       + " (" + (r != null ? r.Why : "-") + "), chce pokoju " + (peace ? "tak" : "nie") + ", " + Gates(st);
            }
            catch (Exception e) { Stumble("Brief", e); return "SCA blad"; }
        }

        /// <summary>334c (recenzja W3): ktora furtka StrategicFactionStatus.IsExhausted dziala (kod SCA145: wojny > 0 i (wrog > swoi x 1.35 albo zagrozone >= twierdze / 3
        /// (dzielenie calkowite) albo wsie spladrowane >= 3)). Tekst bez nawiasow i '|' (regex B334_SCA_K).</summary>
        private static string Gates(object st)
        {
            try
            {
                double en = Convert.ToDouble(Get(_pEnemy, st) ?? 0f, Inv), own = Convert.ToDouble(Get(_pOwn, st) ?? 0f, Inv);
                int thr = Convert.ToInt32(Get(_pThreat, st) ?? 0, Inv), forts = Convert.ToInt32(Get(_pForts, st) ?? 0, Inv), raid = Convert.ToInt32(Get(_pRaided, st) ?? 0, Inv);
                int wars = Convert.ToInt32(Get(_pWars, st) ?? 0, Inv);
                bool gS = (float)en > (float)own * 1.35f, gT = thr >= forts / 3, gR = raid >= 3;
                return "furtki wyczerpania: sila " + (gS ? "TAK" : "nie") + ", twierdze " + thr + "/" + forts + " " + (gT ? "TAK" : "nie") + ", wsie " + raid + " " + (gR ? "TAK" : "nie")
                       + (wars > 0 ? "" : " - bez wojen");
            }
            catch { return "furtki wyczerpania: ?"; }
        }

        /// <summary>Kto NIE jest wrogiem dla SCA: 1 = banda, 2 = Inni, 3 = wyjeci spod prawa, 0 = wrog.</summary>
        internal static int Skip(IFaction f)
        {
            if (f == null) return 3;
            if (f.IsBanditFaction) return 1;
            var c = f as Clan;
            if (c != null && c.Culture != null && c.Culture.StringId == "whitewalker") return 2;
            if (f.IsOutlaw) return 3;
            return 0;
        }

        /// <summary>320e (b): ThreatenedFortifications jak SCA (twierdza = miasto albo zamek; oblezona albo wrogie partie lordow w promieniu 80 silniejsze od
        /// EstimatedStrength twierdzy), ale bez partii i oblegajacych z frakcji, ktore Skip pomija.</summary>
        private static int Threatened(Kingdom kingdom)
        {
            var foes = new List<MobileParty>();
            foreach (var mp in MobileParty.AllLordParties)
            {
                if (mp == null || !mp.IsActive) continue;
                var mf = mp.MapFaction;
                if (mf == null || Skip(mf) != 0 || !FactionManager.IsAtWarAgainstFaction(kingdom, mf)) continue;
                foes.Add(mp);
            }
            int n = 0;
            foreach (var s in kingdom.Settlements)
            {
                if (s == null || !(s.IsTown || s.IsCastle)) continue;
                if (s.IsUnderSiege)
                {
                    IFaction bf = null;
                    try { var se = s.SiegeEvent; bf = se != null && se.BesiegerCamp != null && se.BesiegerCamp.LeaderParty != null ? se.BesiegerCamp.LeaderParty.MapFaction : null; } catch { bf = null; }
                    if (bf == null || Skip(bf) == 0) { n++; continue; }   // nie widac oblegajacego = jak SCA
                }
                float sum = 0f;
                var pos = s.GetPosition2D;
                foreach (var mp in foes)
                {
                    if (mp.GetPosition2D.Distance(pos) <= 80f) sum += mp.GetTotalLandStrengthWithFollowers(false);
                }
                float own = s.Party != null ? s.Party.EstimatedStrength : 0f;
                if (sum > own) n++;
            }
            return n;
        }

        /// <summary>320e (a): wszystkie wojny krolestwa z krolestwami (Skip 0) maja pokoj zablokowany (CanonPeaceLock.Blocks). why - powod do linii doby (bez nawiasow).</summary>
        private static bool PeaceLocked(Kingdom kingdom, out string why)
        {
            if (!LockOn) { why = "klucz NIE"; return false; }
            int all = 0, locked = 0;
            foreach (var f in kingdom.FactionsAtWarWith)
            {
                if (f == null || !f.IsKingdomFaction || Skip(f) != 0) continue;
                all++;
                if (CanonPeaceLock.Blocks(kingdom, f)) locked++;
            }
            if (all == 0) { why = "bez wojen z krolestwami"; return false; }
            if (locked == all) { why = "pokoj zablokowany 264 - krolestwa " + locked + " z " + all; return true; }
            // 334 B3 (klucz ScaFightsOnMainWarLocked334): wojny bez mozliwego pokoju (264 / 330 / wymuszone ROT - Wyspy-Polnoc) daja >= polowe zagrozenia F334 - bez
            // ForcePeace, takze gdy obok trwa mala wojna z pokojem mozliwym (korona - Smocza Skala; Wyspy, Reach - Polnoc i inne). Nigdy nie zdejmuje blokady stara regula.
            string w334 = null;
            bool? m334 = null;
            try { m334 = WarFront334.MainWarLocked(kingdom, out w334); } catch (Exception e) { Stumble("PeaceLocked.334", e); }
            if (m334 == true) { why = w334; return true; }
            why = "pokoj mozliwy - zablokowane " + locked + " z " + all + " krolestw" + (w334 != null ? ", " + w334 : "");
            return false;
        }

        /// <summary>Postfiks StrategicCampaignAI145Behavior.CalculateFactionStatus: EnemyStrength i ThreatenedFortifications bez band, Innych i wyjetych spod prawa;
        /// stan blokady pokoju (320e). Nie rzuca.</summary>
        public static void CalcPostfix(Kingdom kingdom, object __result)
        {
            if (kingdom == null || __result == null || _pEnemy == null) return;
            bool on = On, lockOn = LockOn;
            if (!on && !lockOn && !WarFront334.ScaFrontOn) { _last.Remove(kingdom.StringId); return; }
            try
            {
                var r = new Rec { Name = kingdom.Name != null ? kingdom.Name.ToString() : kingdom.StringId };
                try { r.Before = Convert.ToSingle(_pEnemy.GetValue(__result, null), Inv); } catch { r.Before = -1f; }
                r.After = r.Before;
                if (on)
                {
                    float e = 0f;
                    foreach (var f in kingdom.FactionsAtWarWith)
                    {
                        float v = 0f;
                        try { v = f != null ? f.CurrentTotalStrength : 0f; } catch { v = 0f; }
                        switch (Skip(f))
                        {
                            case 0: e += v; r.NKept++; break;
                            case 1: r.Bandits += v; r.NBandits++; break;
                            case 2: r.Others += v; r.NOthers++; break;
                            default: r.Outlaws += v; r.NOutlaws++; break;
                        }
                    }
                    r.After = e;
                    _pEnemy.SetValue(__result, e, null);
                    _dEvals++;
                    // 320e (b)
                    if (_pThreat != null)
                    {
                        try
                        {
                            r.ThreatSca = Convert.ToInt32(_pThreat.GetValue(__result, null), Inv);
                            r.ThreatOurs = Threatened(kingdom);
                            _pThreat.SetValue(__result, r.ThreatOurs, null);
                            if (r.ThreatOurs != r.ThreatSca) _dThreatChanged++;
                        }
                        catch (Exception ex) { r.ThreatOurs = -1; Stumble("CalcPostfix.Threat", ex); }
                    }
                }
                // 334 B2 (klucz ScaFrontThreat334): wrog = suma F334 wrogow (druzyny w 8.02 doby od naszych twierdz), nie cala sila swiata; swoi bez zmian.
                // Po recenzji 334: r.After zostaje wartoscia 320b (linia 320b i S320-5: po = przed - pominiete), F334 tylko w ocenie SCA i w linii 334; zmiana liczona raz.
                float fin = r.After;
                try
                {
                    float fe = WarFront334.ScaEnemy(kingdom, r.After);
                    if (fe >= 0f) { _pEnemy.SetValue(__result, fe, null); fin = fe; }
                }
                catch (Exception ex) { Stumble("CalcPostfix.Front334", ex); }
                // 334c (klucz ScaCoalitionOwn334c): strona wojny kanonu (274) - "swoi" = suma tej samej miary SCA (CurrentTotalStrength) krolestw tej strony; wrog
                // zostaje F334 do tego krolestwa (bez sumowania frontu koalicji - kazde krolestwo liczy swoich wrogow raz). Inne krolestwa - bez zmian.
                if (_pOwn != null)
                {
                    try
                    {
                        float ownRaw = Convert.ToSingle(_pOwn.GetValue(__result, null), Inv);
                        float fo = WarFront334.ScaOwn(kingdom, ownRaw);
                        if (fo >= 0f) _pOwn.SetValue(__result, fo, null);
                    }
                    catch (Exception ex) { Stumble("CalcPostfix.Own334c", ex); }
                }
                if (Math.Abs(r.Before - fin) > 0.5f) _dChanged++;
                // 320e (a)
                try { r.Locked = PeaceLocked(kingdom, out r.Why); } catch (Exception ex) { r.Locked = false; r.Why = "blad"; Stumble("CalcPostfix.Lock", ex); }
                if (r.Locked && _wiredTicks && !_pending.Contains(kingdom)) _pending.Add(kingdom);
                _last[kingdom.StringId] = r;
            }
            catch (Exception ex) { Stumble("CalcPostfix", ex); }
        }

        /// <summary>320e (a): cel, ktory SCA wybiera bez wyczerpania (ta sama kolejnosc co UpdateWarGoal, pomocniki SCA).</summary>
        private static object NextGoal(Kingdom k, object status)
        {
            var home = k.InitialHomeSettlement;
            if (home != null)
            {
                float near = Convert.ToSingle(_mNearby.Invoke(null, new object[] { home, k, 80f }), Inv);
                if (near > 500f) return _gDefend;
            }
            if (_mLostClaim.Invoke(null, new object[] { k }) != null) return _gReclaim;
            int raided = 0;
            try { raided = Convert.ToInt32(_pRaided.GetValue(status, null), Inv); } catch { raided = 0; }
            return raided > 0 ? _gWeaken : _gBorder;
        }

        /// <summary>Postfiks UpdateWarGoal: krolestwo z blokada pokoju - ForcePeace (nowy albo stary) zastapiony nastepnym celem. Nie rzuca.</summary>
        public static void GoalPostfix(Kingdom __0)
        {
            var k = __0;
            if (k == null || _gForce == null) return;
            try
            {
                Rec r;
                if (!_last.TryGetValue(k.StringId, out r)) return;
                object g = _mGetGoal.Invoke(null, new object[] { k });
                r.GoalSca = g != null ? g.ToString() : "?";
                r.GoalOurs = r.GoalSca;
                if (!r.Locked || !LockOn || g == null || !g.Equals(_gForce)) return;
                object st = _mGetStatus.Invoke(null, new object[] { k });
                object ng = NextGoal(k, st);
                _mSetGoal.Invoke(null, new object[] { k, ng });
                r.GoalOurs = ng.ToString();
                _dGoalSwaps++;
            }
            catch (Exception e) { Stumble("GoalPostfix", e); }
        }

        /// <summary>Postfiks OnDailyTick / OnHourlyTick: WantsPeace = false dla krolestw z blokada z tej rundy (SetFactionStatus liczy je po UpdateWarGoal). Nie rzuca.</summary>
        public static void TickPostfix()
        {
            if (_pending.Count == 0) return;
            try
            {
                bool lockOn = LockOn;
                foreach (var k in _pending)
                {
                    if (k == null) continue;
                    Rec r;
                    if (!_last.TryGetValue(k.StringId, out r) || !r.Locked || !lockOn) continue;
                    object st = _mGetStatus.Invoke(null, new object[] { k });
                    if (st == null) continue;
                    bool p = _pPeace.GetValue(st, null) is bool b && b;
                    r.PeaceSca = p; r.PeaceSeen = true;
                    if (p) { _pPeace.SetValue(st, false, null); _dPeaceCleared++; }
                    // zapas na wypadek wkompilowania UpdateWarGoal w petle ticku (postfiks celu by nie zadzialal): ForcePeace zapisany -> nastepny cel, status wyrownany
                    object g = _mGetGoal.Invoke(null, new object[] { k });
                    if (g != null && g.Equals(_gForce))
                    {
                        object ng = NextGoal(k, st);
                        _mSetGoal.Invoke(null, new object[] { k, ng });
                        _pGoal.SetValue(st, ng, null);
                        if (r.GoalSca == null) r.GoalSca = g.ToString();
                        r.GoalOurs = ng.ToString();
                        _dGoalSwaps++;
                        _dGoalLate++;
                    }
                }
            }
            catch (Exception e) { Stumble("TickPostfix", e); }
            _pending.Clear();
        }

        // ------------------------------------------------------------ doba (tylko log)

        private static string F0(object o) { try { return o == null ? "?" : Convert.ToDouble(o, Inv).ToString("0", Inv); } catch { return "?"; } }
        private static object Get(PropertyInfo p, object o) { try { return p != null && o != null ? p.GetValue(o, null) : null; } catch { return null; } }

        internal static void Daily()
        {
            if (Campaign.Current == null) return;
            var s = Settings.Current;
            try
            {
                if (s != null && s.LogEnabled && (_wired || _hookTried))
                {
                    var sb = new StringBuilder(1600);
                    sb.Append(P).Append("dzien ").Append((int)CampaignTime.Now.ToDays).Append(" | klucz ").Append(On ? "tak" : "nie")
                      .Append(", latka ").Append(_wired ? "wpieta" : "BRAK").Append(", ocen doby ").Append(_dEvals).Append(" (zmienionych ").Append(_dChanged)
                      .Append("), potkniecia doby ").Append(_dStumbles).Append(" (sesja ").Append(_stumbles).Append(")")
                      .Append(" | 320e klucz ").Append(LockOn ? "tak" : "nie").Append(", latki ").Append(_wiredGoal && _wiredTicks ? "wpiete" : "BRAK")
                      .Append(", zamian celu ").Append(_dGoalSwaps).Append(" (w ticku ").Append(_dGoalLate).Append(")").Append(", pokoj wyczyszczony ").Append(_dPeaceCleared).Append(", zagrozone zmienione ").Append(_dThreatChanged);
                    if (_wired && (On || LockOn))
                    {
                        var parts = new List<string>();
                        foreach (var k in Kingdom.All)
                        {
                            if (k == null || k.IsEliminated) continue;
                            Rec r;
                            if (!_last.TryGetValue(k.StringId, out r)) continue;
                            object st = null;
                            try { st = _mGetStatus != null ? _mGetStatus.Invoke(null, new object[] { k }) : null; } catch (Exception e) { Stumble("Daily.Status", e); }
                            bool exh = Get(_pExh, st) is bool b1 && b1, peace = Get(_pPeace, st) is bool b2 && b2;
                            string goal = (Get(_pGoal, st) ?? "?").ToString();
                            parts.Add(r.Name + ": wrog " + r.Before.ToString("0", Inv) + " -> " + r.After.ToString("0", Inv)
                                      + " (pominiete: bandy " + r.Bandits.ToString("0", Inv) + " z " + r.NBandits + ", Inni " + r.Others.ToString("0", Inv) + " z " + r.NOthers
                                      + ", wyjeci spod prawa " + r.Outlaws.ToString("0", Inv) + " z " + r.NOutlaws + "; liczeni " + r.NKept + ")"
                                      + ", swoi " + F0(Get(_pOwn, st)) + ", twierdze " + F0(Get(_pForts, st)) + " zagrozone " + F0(Get(_pThreat, st))
                                      + " (SCA " + (r.ThreatSca >= 0 ? r.ThreatSca.ToString(Inv) : "?") + ")"
                                      + ", wojen " + F0(Get(_pWars, st)) + ", cel " + goal + " (SCA " + (r.GoalSca ?? "?") + ", " + r.Why + ")"
                                      + ", wyczerpane " + (exh ? "tak" : "nie") + ", chce pokoju " + (peace ? "tak" : "nie")
                                      + " (SCA " + (r.PeaceSeen ? (r.PeaceSca ? "tak" : "nie") : (peace ? "tak" : "nie")) + ")");
                        }
                        sb.Append(" | ").Append(parts.Count > 0 ? string.Join("; ", parts.ToArray()) : "brak ocen SCA");
                    }
                    sb.Append('.');
                    Log.Info(sb.ToString());
                }
            }
            catch (Exception e) { Stumble("Daily", e); }
            _dEvals = _dChanged = _dStumbles = _dGoalSwaps = _dGoalLate = _dPeaceCleared = _dThreatChanged = 0;
        }
    }
}
