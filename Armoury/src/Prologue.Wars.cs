using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;

namespace Armoury
{
    /// <summary>
    /// m104-wojny - WOJNY WEDLUG KSIAZKI (Jeff 21.10: "Kod scen zmieniajacych swiat i wojen wedlug ksiazki - rob to teraz"; decyzje "tak, rob to"). Trzy czesci,
    /// kazda pod wlasnym kluczem MCM; linie logu "Wojny kanonu (m104): ...", progi tools/sprawdz_logi.py --grupa m104wojny.
    ///
    /// (1) ESSOS ZYJE SWOIM ZYCIEM (klucz PrologueEssosWarsCalendar, tylko kampania z prologiem 298). ROT ROTStorylineWars.OnNewGameCreated zaklada 8 wojen fabularnych
    ///     z obiema stronami w Essos (StartDay 5, EndDay 100 - po rozciagnieciu osi Fabuly CrashScribe x4.33: dni 22-433): Braavos-Lorath (bravos-nord), Tyrosh-Lys,
    ///     Pentos-Myr, Norvos-Qohor, Volantis-Aegon (volantis-valyrian), Qarth-Daenerys (qarth-empire_s), Dothrakowie-Daenerys (khuzait-empire_s), Sarnor-Ibben (sarnor-ibb).
    ///     Latka startu opowiesci (Prologue.Story) przestawia oba odczyty CampaignStartTime w ROTStorylineWar.Enforced i bramke "ElapsedDaysUntilNow &lt; 1" w
    ///     ROTStorylineWars.EnforceWars na start OPOWIESCI - w prologu ROT nie wymusza zadnej wojny, takze essoskiej.
    ///     DECYZJA (kanon i mapa): kalendarz dostaja wojny, w ktorych OBIE strony sa w zbiorze EssosIds - Wolne Miasta, Lorath, Dothrakowie, Qarth, Sarnor, Ibben, Yi Ti,
    ///     Wyspy Letnie i Aegon (valyrian: na mapie Mantarys w Essos; w kanonie Zlota Kompania i Aegon ukryci w Essos az do inwazji - wojna Volantis-Aegon to zycie Essos).
    ///     Daenerys (empire_s, Meereen) - NIE: w kanonie jej wojny w Essos (Qarth, khalasary) zaczynaja sie po smierci khala Drogo, czyli na starcie opowiesci ROT; w prologu
    ///     jest khaleesi khalasaru Drogo (sceny prologu: slub dnia 12, korona Viserysa dnia 145, zamach dnia 172) - wojna Dothrakow z Daenerys w prologu bylaby
    ///     sprzeczna z ksiazka. Jej wojny (Qarth, Dothrakowie) zostaja na zegarze opowiesci, jak wszystkie wojny z udzialem Westeros.
    ///     JAK: prefiks + finalizer na ROTStorylineWar.Enforced (wpinane leniwie razem z latkami prologu, raz na proces): wojna essoska w kampanii z prologiem i z kluczem ->
    ///     licznik [ThreadStatic] Prologue._calendarDepth na czas wywolania, a Prologue.StoryStartOf (podmieniony odczyt w ROT) zwraca wtedy start KALENDARZA. Ta sama
    ///     metoda ROT decyduje dalej (StartDay/EndDay rozciagniete przez Fabula.WarPrefix CrashScribe, IsEnabled, wyzwalacz, zniszczenie stron) - takze dla
    ///     IsWarForced (blokada pokoju ROT, PovertyPeace, StorylineReparations, PeaceHold). Prefiks nie pomija oryginalu (zadnego return false).
    ///     W prologu bramka EnforceWars ROT stoi (dzien opowiesci &lt; 1), wiec doba Armoury (CanonWarsBehavior.OnDaily -> EssosDaily) robi dla wojen essoskich to samo,
    ///     co petla ROT: Enforced (przez nasze i cudze latki) == true i strony nie w wojnie -> CallingDeclareWar ROT = true, DeclareWarAction.ApplyByDefault, false.
    ///     Od dnia opowiesci 1 robi to juz ROT (z kalendarzem z prefiksu). Pokoj krola (Prologue.Peace) Essos nie dotyczy (Realms - tylko Westeros).
    ///     Klasyczna kampania: nic (Active = false; zreszta oba zegary sa rowne). Klucz wylaczony - wszystkie wojny czekaja na opowiesc, jak w 103.
    /// (2) REACH I DORNE Z KORONA (klucze CanonReachJoinsCrown, CanonDorneJoinsCrown) i (3) MLODE SMOKI DAENERYS (klucz DaenerysDragonsGrow) - Prologue.Crown /
    ///     Prologue.Dragons w tym samym pliku i w Prologue.Dragons.cs.
    ///     ZAKRES (2) i (3) (recenzja R1, do decyzji Jeffa nr 5 w CHANGELOG): kampania z prologiem 298 (Active) albo nowa kampania utworzona z ta DLL (znacznik nw=1
    ///     w arm_m104, CampaignEvents.OnNewGameCreatedEvent). Zapis bez znacznika (biezaca kampania klasyczna Jeffa z 20.10) - jak klucze wylaczone: nic nie zakladamy
    ///     i nic nie zdejmujemy; linia startu i doby mowia "nie dotyczy". Wlaczenie w starych zapisach = CanonHere zwraca true (jedna linia), tylko na slowo Jeffa.
    /// Bledy: per wywolanie, liczone (WStumble, wlasny licznik - linia "potkniecia"), bez globalnego wylacznika. Stan (2) i (3) w zapisie: klucz "arm_m104" (CanonWarsBehavior).
    /// </summary>
    internal static partial class Prologue
    {
        internal const string WTag = "Wojny kanonu (m104): ";

        // krolestwa Essos z wlasnym kalendarzem (id ROT.Misc.ROTKingdoms; bez empire_s - Daenerys, patrz summary)
        private static readonly HashSet<string> EssosIds = new HashSet<string>(StringComparer.Ordinal)
        {
            "bravos", "nord", "pentos", "norvos", "qohor", "myr", "lys", "tyrosh", "volantis", "sarnor", "ibb", "qarth", "khuzait", "yiti", "summer", "valyrian"
        };

        private static bool EssosKey { get { var s = S; return s != null && s.PrologueEssosWarsCalendar; } }

        // ---------------------------------------------------------------- zakres (2) i (3) (recenzja R1)
        private static bool _wNew;   // nowa kampania utworzona z m104 (zapis arm_m104 "nw")

        /// <summary>CanonWarsBehavior: CampaignEvents.OnNewGameCreatedEvent (po OnGameStart - ResetWars juz wyzerowal stan) - znacznik nowej kampanii.</summary>
        internal static void WarsOnNewGame() { _wNew = true; }

        /// <summary>(2) i (3) dzialaja: kampania z prologiem albo nowa kampania z m104 (znacznik w zapisie). Stary zapis (biezaca kampania klasyczna Jeffa) - nie.</summary>
        internal static bool CanonHere { get { return Active || _wNew; } }

        private const string ScopeNo = "nie dotyczy (zapis sprzed m104 bez prologu - decyzja Jeffa nr 5)";

        private static string ScopeText() { return Active ? "kampania z prologiem" : _wNew ? "nowa kampania (znacznik)" : ScopeNo; }

        // ---------------------------------------------------------------- bledy m104 (osobny licznik - linie m101 bez zmian)
        private static int _wStumbles, _wStumblesDay;

        internal static void WStumble(string where, Exception e)
        {
            _wStumbles++; _wStumblesDay++;
            if (_wStumbles <= 5) Log.Error("CanonWars." + where + " (potkniecie " + _wStumbles + ")", e);
        }

        // ---------------------------------------------------------------- ROT: wojny fabularne (odbiciem)
        private static bool _rotWarsTried;
        private static FieldInfo _fSubWars, _fWarList, _fCalling, _fReachAllied;
        private static PropertyInfo _pWF1, _pWF2, _pWEnabled, _pWCustom, _pWId, _pWTriggered, _pWFlavor;
        private static MethodInfo _mWEnforced, _mWForced, _mWTrigger;

        private static bool RotWarsReady()
        {
            if (!_rotWarsTried)
            {
                _rotWarsTried = true;
                try
                {
                    var tWars = AccessTools.TypeByName("ROT.CampaignBehaviors.ROTStorylineWars");
                    var tWar = AccessTools.TypeByName("ROT.CampaignBehaviors.ROTStorylineWar");
                    if (tWars != null && tWar != null)
                    {
                        var tSub = tWars.Assembly.GetType("ROT.SubModule");
                        _fSubWars = tSub != null ? AccessTools.Field(tSub, "StorylineWars") : null;
                        _fWarList = AccessTools.Field(tWars, "_storyLineWars");
                        _fCalling = AccessTools.Field(tWars, "CallingDeclareWar");
                        _mWForced = AccessTools.Method(tWars, "IsWarForced", new[] { typeof(IFaction), typeof(IFaction) });
                        _pWF1 = AccessTools.Property(tWar, "Faction1");
                        _pWF2 = AccessTools.Property(tWar, "Faction2");
                        _pWEnabled = AccessTools.Property(tWar, "IsEnabled");
                        _pWCustom = AccessTools.Property(tWar, "IsCustom");
                        _mWEnforced = AccessTools.DeclaredMethod(tWar, "Enforced", new[] { typeof(string).MakeByRefType() });
                        // R3 (Prologue.Crown UntriggerWestReach): wyzwalacz wojny jak ROT OnRenlysDeath - opcjonalne (brak = linia doby mowi BRAK)
                        _pWId = AccessTools.Property(tWar, "Id");
                        _pWTriggered = AccessTools.Property(tWar, "IsTriggered");
                        _pWFlavor = AccessTools.Property(tWar, "FlavorText");
                        _mWTrigger = AccessTools.DeclaredMethod(tWar, "Trigger", new[] { typeof(bool) });
                        _fReachAllied = AccessTools.Field(tWars, "_reachAlliedRenly");
                    }
                }
                catch (Exception e) { WStumble("RotWarsReady", e); }
            }
            return _fSubWars != null && _fWarList != null && _pWF1 != null && _pWF2 != null && _mWEnforced != null;
        }

        private static object RotWarsBehavior() { return _fSubWars != null ? _fSubWars.GetValue(null) : null; }

        /// <summary>Kopia listy wojen fabularnych ROT (ROTStorylineWars._storyLineWars) - pusta, gdy ROT nieobecny.</summary>
        private static List<object> RotWars()
        {
            var r = new List<object>();
            if (!RotWarsReady()) return r;
            var beh = RotWarsBehavior();
            var list = beh != null ? _fWarList.GetValue(beh) as System.Collections.IEnumerable : null;
            if (list != null) foreach (var w in list) if (w != null) r.Add(w);
            return r;
        }

        private static IFaction WF1(object war) { return _pWF1.GetValue(war, null) as IFaction; }
        private static IFaction WF2(object war) { return _pWF2.GetValue(war, null) as IFaction; }

        /// <summary>ROT ROTStorylineWars.IsWarForced(a, b) - wojna fabularna tej pary wymuszona (te same latki Enforced co ROT); brak ROT - false.</summary>
        private static bool WarForced(IFaction a, IFaction b)
        {
            try
            {
                if (!RotWarsReady() || _mWForced == null) return false;
                var beh = RotWarsBehavior();
                return beh != null && (bool)_mWForced.Invoke(beh, new object[] { a, b });
            }
            catch (Exception e) { WStumble("WarForced", e); return false; }
        }

        // wojna czysto essoska (obie strony to krolestwa z EssosIds) - strony wojny ROT sie nie zmieniaja (prywatny setter), wynik w pamieci na obiekt
        private static readonly ConditionalWeakTable<object, object> _essosOf = new ConditionalWeakTable<object, object>();
        private static readonly ConditionalWeakTable<object, object>.CreateValueCallback _essosCalc = EssosCalc;
        private static readonly object Yes = true, No = false;

        private static object EssosCalc(object war)
        {
            try
            {
                var a = WF1(war) as Kingdom;
                var b = WF2(war) as Kingdom;
                return a != null && b != null && a.StringId != null && b.StringId != null && EssosIds.Contains(a.StringId) && EssosIds.Contains(b.StringId) ? Yes : No;
            }
            catch { return No; }
        }

        private static bool IsEssosWar(object war) { return war != null && RotWarsReady() && (bool)_essosOf.GetValue(war, _essosCalc); }

        // ---------------------------------------------------------------- (1) latka Enforced
        private static bool _essosTried;
        private static string _essosWire = "nie wpieta (brak kampanii z prologiem w tym procesie)";

        /// <summary>Raz na proces, przy pierwszej kampanii z prologiem (OnPrologueCampaign): prefiks + finalizer ROTStorylineWar.Enforced.</summary>
        private static void InstallEssos(string why)
        {
            if (_essosTried) return;
            _essosTried = true;
            try
            {
                if (!RotWarsReady()) { _essosWire = "BRAK (ROT ROTStorylineWar.Enforced / StorylineWars)"; }
                else
                {
                    var h = new Harmony("com.jeff.armoury.prologue");
                    var r = h.Patch(_mWEnforced, prefix: new HarmonyMethod(typeof(Prologue), nameof(EssosEnforcedPrefix)),
                                    finalizer: new HarmonyMethod(typeof(Prologue), nameof(EssosEnforcedFinalizer)));
                    _essosWire = r != null ? "TAK" : "BRAK";
                }
            }
            catch (Exception e) { WStumble("InstallEssos", e); _essosWire = "BLAD (" + e.GetType().Name + ")"; }
            Log.Info(WTag + "latka wojen essoskich (" + why + "): ROTStorylineWar.Enforced " + _essosWire + ".");
        }

        /// <summary>Prefiks ROTStorylineWar.Enforced: wojna essoska w kampanii z prologiem i z kluczem - na czas wywolania odczyt startu w ROT = start kalendarza.</summary>
        public static void EssosEnforcedPrefix(object __instance, out bool __state)
        {
            __state = false;
            try
            {
                if (!Active || !EssosKey || !IsEssosWar(__instance)) return;
                _calendarDepth++;
                __state = true;
            }
            catch (Exception e) { WStumble("EssosEnforcedPrefix", e); }
        }

        /// <summary>Finalizer ROTStorylineWar.Enforced: zdejmuje licznik kalendarza (zawsze, takze po wyjatku); wyjatek oryginalu idzie dalej bez zmian.</summary>
        public static Exception EssosEnforcedFinalizer(bool __state, Exception __exception)
        {
            if (__state && _calendarDepth > 0) _calendarDepth--;
            return __exception;
        }

        /// <summary>Dni od startu opowiesci (ujemne w prologu).</summary>
        internal static double StoryDays()
        {
            try { return (CampaignTime.Now - StoryStart()).ToDays; } catch { return 0.0; }
        }

        private static int _essosDeclaredDay, _essosDeclared;
        private static readonly List<string> _essosToday = new List<string>();

        /// <summary>Doba kampanii z prologiem: dopoki bramka ROT EnforceWars stoi (dzien opowiesci &lt; 1, ten sam warunek co w ROT po latce opowiesci), wojny essoskie
        /// wymuszone przez ROT Enforced (kalendarz z prefiksu) i nie w wojnie - wypowiedziane jak w petli ROT. Linia doby Wojny kanonu.</summary>
        internal static void EssosDaily()
        {
            if (!Active || !EssosKey || _essosWire != "TAK") return;
            if (StoryDays() >= 1.0) return;   // od dnia opowiesci 1 - ROT EnforceWars (z kalendarzem z prefiksu)
            var beh = RotWarsBehavior();
            if (beh == null) return;
            foreach (var war in RotWars())
            {
                try
                {
                    if (!IsEssosWar(war)) continue;
                    var f1 = WF1(war); var f2 = WF2(war);
                    if (f1 == null || f2 == null || f1.IsEliminated || f2.IsEliminated) continue;
                    var args = new object[] { null };
                    if (!(bool)_mWEnforced.Invoke(war, args)) continue;
                    if (FactionManager.IsAtWarAgainstFaction(f1, f2)) continue;
                    bool prev = false;
                    try
                    {
                        if (_fCalling != null) { prev = (bool)_fCalling.GetValue(beh); _fCalling.SetValue(beh, true); }
                        DeclareWarAction.ApplyByDefault(f1, f2);
                    }
                    finally { if (_fCalling != null) _fCalling.SetValue(beh, prev); }
                    bool now = FactionManager.IsAtWarAgainstFaction(f1, f2);
                    if (now) { _essosDeclaredDay++; _essosDeclared++; }
                    string pair = f1.StringId + "-" + f2.StringId;
                    if (_essosToday.Count < 8) _essosToday.Add(pair);
                    Log.Info(WTag + "ESSOS - wojna " + pair + (now ? " WYPOWIEDZIANA" : " NIE weszla (DeclareWarAction bez skutku)") + " w prologu (dzien kalendarza "
                             + ((int)PrologueDay()).ToString(Inv) + ", dzien opowiesci " + StoryDays().ToString("0.0", Inv) + ") - wojna fabularna ROT na kalendarzu.");
                }
                catch (Exception e) { WStumble("EssosDaily", e); }
            }
        }

        /// <summary>Opis wojen essoskich do linii: na liscie N (pary), w wojnie W; wojny ze strona zniszczona osobno ("zniszczone Z (pary)") - poza N i W, jak w EssosDaily
        /// i w ROT (EnforceWars usuwa je z listy od dnia opowiesci 1; w prologu bramka stoi, wiec zostaja na liscie) - recenzja R7.</summary>
        private static string EssosText()
        {
            if (!RotWarsReady()) return "ROT nieobecny";
            int n = 0, w = 0;
            var l = new List<string>();
            var gone = new List<string>();
            foreach (var war in RotWars())
            {
                if (!IsEssosWar(war)) continue;
                var f1 = WF1(war); var f2 = WF2(war);
                string pair = (f1 != null ? f1.StringId : "?") + "-" + (f2 != null ? f2.StringId : "?");
                if (f1 == null || f2 == null || f1.IsEliminated || f2.IsEliminated) { gone.Add(pair); continue; }
                n++;
                bool at = FactionManager.IsAtWarAgainstFaction(f1, f2);
                if (at) w++;
                l.Add(pair + (at ? " (wojna)" : ""));
            }
            return "na kalendarzu " + n + " (" + string.Join(", ", l.ToArray()) + "), w wojnie " + w + "/" + n
                   + (gone.Count > 0 ? ", zniszczone " + gone.Count + " (" + string.Join(", ", gone.ToArray()) + ")" : "");
        }

        // ---------------------------------------------------------------- zapis, reset, linie (wspolne dla m104)

        /// <summary>Prologue.ResetCampaign (OnGameStart kazdej kampanii): stan m104 poprzedniej kampanii w procesie zerowany.</summary>
        private static void ResetWars()
        {
            _wStumblesDay = 0; _essosDeclaredDay = 0; _essosDeclared = 0; _essosToday.Clear();
            _warsEventDay = false;
            _wNew = false;
            ResetCrown();
            ResetDragons();
        }

        internal static string PackWars()
        {
            return "v=1;nw=" + (_wNew ? "1" : "0") + ";" + PackCrown() + PackDragons();
        }

        internal static void UnpackWars(string v)
        {
            ResetWars();
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            if (!string.IsNullOrEmpty(v))
                foreach (var part in v.Split(';'))
                {
                    int i = part.IndexOf('=');
                    if (i > 0) map[part.Substring(0, i)] = part.Substring(i + 1);
                }
            string nw;
            _wNew = map.TryGetValue("nw", out nw) && nw == "1";
            UnpackCrown(map);
            UnpackDragons(map);
        }

        private static bool _warsEventDay;   // cos sie dzis wydarzylo (linia doby mimo rytmu 7 dob)

        /// <summary>CanonWarsBehavior.OnSessionLaunched: linia startu sesji m104 (kazda kampania).</summary>
        internal static void WarsStartLine()
        {
            string essos;
            if (!Active) essos = "kampania klasyczna - nie dotyczy";
            else essos = "klucz " + (EssosKey ? "TAK" : "NIE") + ", latka Enforced " + _essosWire + ", " + EssosText()
                         + (StoryDays() < 1.0 ? ", bramka ROT EnforceWars zamknieta - wypowiada doba Armoury" : ", wymusza ROT EnforceWars");
            Log.Info(WTag + "start sesji | prolog " + (Active ? "TAK" : "NIE") + " | dzien opowiesci " + StoryDays().ToString("0.0", Inv)
                     + " | (1) Essos na kalendarzu: " + essos
                     + " | zakres (2) i (3): " + ScopeText()
                     + " | (2) Reach i Dorne z korona: " + CrownStartText()
                     + " | (3) smoki Daenerys: " + DragonsText()
                     + " | potkniecia " + _wStumbles + ".");
        }

        /// <summary>CanonWarsBehavior.OnDaily: linia doby m104. W prologu (dzien opowiesci &lt; 1, Essos) co dobe, potem przy zdarzeniu albo co 7 dob opowiesci.</summary>
        internal static void WarsDaily()
        {
            try { EssosDaily(); } catch (Exception e) { WStumble("EssosDaily", e); }
            try { CrownDaily(); } catch (Exception e) { WStumble("CrownDaily", e); }
            double sd = StoryDays();
            bool essosWindow = Active && EssosKey && sd < 1.0;
            int every = (int)Math.Floor(sd);
            if (essosWindow || _warsEventDay || _essosDeclaredDay > 0 || _wStumblesDay > 0 || (every % 7 + 7) % 7 == 0)
            {
                Log.Info(WTag + "doba | dzien opowiesci " + sd.ToString("0.0", Inv) + (Active ? " (dzien kalendarza " + ((int)PrologueDay()).ToString(Inv) + ")" : "")
                         + " | (1) Essos: " + (Active ? (EssosKey ? EssosText() + ", wypowiedziane dzis przez Armoury " + _essosDeclaredDay
                                                                    + (_essosToday.Count > 0 ? " [" + string.Join(", ", _essosToday.ToArray()) + "]" : "") + " (razem " + _essosDeclared + ")"
                                                         : "klucz NIE") : "nie dotyczy")
                         + " | (2) Reach: " + _reachState + " | Dorne: " + _dorneState
                         + " | (3) smoki Daenerys: " + DragonsText()
                         + " | potkniecia dzis " + _wStumblesDay + " (razem " + _wStumbles + ").");
            }
            _essosDeclaredDay = 0; _essosToday.Clear(); _wStumblesDay = 0; _warsEventDay = false; _dChangesDay = 0;
        }
    }

    /// <summary>m104-wojny: zachowanie kampanii - linia startu sesji, doba (Reach i Dorne z korona, smoki Daenerys, linia doby), zapis stanu (arm_m104).
    /// Doba: najpierw Essos (1), potem linia; smoki (3) z ArmouryBehavior PRZED DragonFeed (karmienie widzi biezacy wierzchowiec).</summary>
    internal sealed class CanonWarsBehavior : CampaignBehaviorBase
    {
        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDaily);
            CampaignEvents.HeroPrisonerReleased.AddNonSerializedListener(this, OnHeroReleased);   // (3) Daenerys z niewoli - etap smokow od razu
            CampaignEvents.OnNewGameCreatedEvent.AddNonSerializedListener(this, _ => Prologue.WarsOnNewGame());   // R1: zakres (2) i (3) - znacznik nowej kampanii
        }

        public override void SyncData(IDataStore dataStore)
        {
            string v = "";
            try { if (dataStore.IsSaving) v = Prologue.PackWars(); } catch (Exception e) { Prologue.WStumble("PackWars", e); v = ""; }
            SaveText.Sync(dataStore, "arm_m104", ref v);
            if (dataStore.IsLoading) { try { Prologue.UnpackWars(v); } catch (Exception e) { Prologue.WStumble("UnpackWars", e); } }
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            Prologue.DragonsApply("start sesji");   // (3) przed linia startu - linia mowi stan po zmianie
            try { Prologue.WarsStartLine(); } catch (Exception e) { Prologue.WStumble("WarsStartLine", e); }
        }

        private void OnHeroReleased(Hero prisoner, PartyBase party, IFaction capturer, EndCaptivityDetail detail, bool notify)
        {
            Prologue.DragonsOnReleased(prisoner);
        }

        private void OnDaily()
        {
            try { Prologue.WarsDaily(); } catch (Exception e) { Prologue.WStumble("WarsDaily", e); }
        }
    }
}
