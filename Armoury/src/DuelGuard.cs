using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text;
using HarmonyLib;
using SandBox.Tournaments.MissionLogics;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// m111-walka faza 1 (24.10, Jeff: "jak atakuje caly czas, przeciwnik sie tylko broni; z drzewcem, ktorego sie dopiero ucze, bije duzo wyzsze
    /// poziomy; nie czulem, ze rycerz Gwardii Krolewskiej jest mocny"): ZASADY POSTURY W POJEDYNKU - turniej, arena, pojedynek ROT (BattleSense.InDuel).
    ///
    /// Dowod [K]: RBM liczy obrazenia postury z umiejetnosci obu stron tylko przez (1 + atl/500 + bron/500) / (1 + ...) (StanceLogic.cs:823-960) -
    /// roznica 156 punktow broni to ok. 20%; RBM podaje modyfikatory parowania (0.5), parowania tarcza (0.8), chamber (0.25 / 2.0), ale w srodku
    /// calculate*PostureDamage nadpisuje je na 1 (:843, :912); zlamanie postury przez gracza wlacza zwolnione tempo tylko dla gracza
    /// (HitStopLogic.TriggerHitStop priorytet 3, StanceLogic.cs:316, :378); Artem przy HP ponizej progu (Jeff: 20) wali AI na ziemie i tnie mu
    /// predkosc ciosu do x0.45 (ArtemsLivelyAnimationsMissionLogic.cs:186-276, model :134-167).
    ///
    /// Haki (kazdy cel szukany refleksja, sprawdzona sygnatura, linia "wpieta" / "NIEWPIETA" w logu; zadnych malych metod - lekcja GC 19.10:
    /// NIE latamy TriggerParryHitStop ani TriggerPostureBreakHitStop, jednolinijkowych):
    ///  A) postfiksy na prywatnych statycznych RBMAI.StanceLogic+CreateMeleeBlowPatch.calculateDefenderPostureDamage / calculateAttackerPostureDamage
    ///     (8 parametrow, [6] = MeleeHitType): liczba zmienia sie PRZED decyzja RBM o zlamaniu, wiec ogluszenie, reset i przebicie zostaja RBM;
    ///     roznica umiejetnosci g = (s obroncy (+20 z tarcza, jak RBM) - s atakujacego) / 250, obronca x clamp(1 - W g, 0.6, 1.4), atakujacy
    ///     x clamp(1 + W g, 0.6, 1.4) (W = DuelSkillGapWeight); DuelPerfectDefence: parowanie bronia obronca x0.5, tarcza x0.8, chamber obronca x0.25
    ///     i atakujacy x2.0; zwykly blok i strona atakujacego przy parowaniu x1;
    ///  B) prefiks + postfiks na publicznych statycznych handleDefender (18 parametrow, [15] MeleeHitType) i handleAttacker (16, [14]): postura przed
    ///     i po (pola Stance.posture / maxPosture); zlamanie = (po > przed) albo (po <= 0 i przed > 0); zlamanie atakujacego na obronie AI -> zryw
    ///     OTWARCIE (DuelCraft); obrona AI -> pierscien obron / NACISK; licznik zlaman stron; komunikat gracza, gdy jego garda peka na obroncy
    ///     o 60+ punktow lepszym (najwyzej raz na 6 s);
    ///  C) prefiks na prywatnej HitStopLogic.TriggerHitStop(float, float, int): w pojedynku przy DuelNoSlowMotion priorytet 3 (zlamanie postury)
    ///     odrzucony; zabojstwo (2) i bitwy bez zmian;
    ///  D) prefiks na ArtemsLivelyAnimationsMissionLogic.OnAgentHit: w pojedynku przy DuelNoInjuredCollapse AI pomijane (bez zestawu "injured"
    ///     model Artema nie daje kary x0.45); gracz i bitwy bez zmian.
    /// Rodzaj ciosu w hakach A i B z danych kolizji wedlug tej samej tablicy co RBM (StanceLogic.cs:507-590) - bez __args (pudelkowanie 8-18
    /// argumentow, w tym AttackCollisionData i MissionWeapon, przy kazdym ciosie takze w bitwach). Haki poza pojedynkiem wychodza po dwoch odczytach pol.
    /// Wyjatek liczony (RoutHold.Stumble, miejsce "pojedynek"), nigdy nie wychodzi z haka. Po recenzji: kazdy hak wpinany we wlasnym try (cele
    /// szukane bez AmbiguousMatchException - pierwsza metoda o nazwie i pasujacej sygnaturze), linia stanu hakow zawsze; pola postury przez
    /// typowany FieldRef (bez pudelkowania); po misji End() zwalnia agentow i mecz.
    /// Ten sam plik: probkowanie akcji AI co 0.1 s (najwyzej 32 agentow, tylko pojedynek), liczniki kontaktow (OnMeleeHit), lista uczestnikow meczu
    /// turnieju i linia misji "Pojedynek (DU)".
    /// </summary>
    internal static class DuelGuard
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        // ---- haki
        private static MethodInfo _mCalcDef, _mCalcAtt, _mHDef, _mHAtt, _mHitStop, _mArtem;
        private static bool _okA1, _okA2, _okB1, _okB2, _okC, _okD;
        private static string _whyA = "-", _whyB = "-", _whyC = "-", _whyD = "-";
        private static FieldInfo _fPosture, _fMax, _fStances;
        private static AccessTools.FieldRef<object, float> _rPosture, _rMax;
        private static FieldInfo _fArtSettings;
        private static PropertyInfo _pArtHp;
        private static float _artemHp = -1f;          // prog Artema na te misje (-1 = jeszcze nie czytany)
        private static bool _artemHpGuess;            // nieczytelny - licze od 33 (domyslny Artema)
        private static string _order = "?";

        internal static float Weight(Settings c) { return Clamp(c != null ? c.DuelSkillGapWeight : 0.5f, 0f, 1f); }
        private static float Clamp(float v, float lo, float hi) { return v < lo ? lo : (v > hi ? hi : v); }

        /// <summary>Czy ktorykolwiek klucz pojedynku (BattleSense rejestrowany, haki wpinane).</summary>
        internal static bool AnyKey(Settings c)
        {
            return c != null && (c.DuelAiEnabled || c.DuelPressureBursts || c.DuelFairGuard || c.DuelSkillGapWeight > 0f || c.DuelPerfectDefence
                                 || c.DuelNoSlowMotion || c.DuelNoInjuredCollapse);
        }

        internal static string HookState()
        {
            return "A obronca " + (_okA1 ? "wpieta" : "NIEWPIETA") + ", A atakujacy " + (_okA2 ? "wpieta" : "NIEWPIETA") + " (" + _whyA + "), B obronca "
                   + (_okB1 ? "wpieta" : "NIEWPIETA") + ", B atakujacy " + (_okB2 ? "wpieta" : "NIEWPIETA") + " (" + _whyB + "), C "
                   + (_okC ? "wpieta" : "NIEWPIETA") + " (" + _whyC + "), D " + (_okD ? "wpieta" : "odpuszczam") + " (" + _whyD + ")";
        }

        // ------------------------------------------------------------ wpiecie (menu glowne, SubModuleMain)

        private static bool IsHitType(Type t) { return t != null && t.IsEnum && t.Name == "MeleeHitType"; }

        private static bool IsCollisionRef(Type t) { return t != null && t.IsByRef && t.GetElementType() == typeof(AttackCollisionData); }

        private static bool Owned(MethodBase target, string id)
        {
            try
            {
                var info = Harmony.GetPatchInfo(target);
                if (info == null) return false;
                foreach (var p in info.Prefixes) if (p != null && p.owner == id) return true;
                foreach (var p in info.Postfixes) if (p != null && p.owner == id) return true;
            }
            catch { }
            return false;
        }

        /// <summary>Po recenzji: kandydat bez AmbiguousMatchException - pierwsza metoda deklarowana o tej nazwie, ktora przechodzi sprawdzenie
        /// sygnatury (aktualizacja RBM / Artema moze dodac przeciazenie).</summary>
        private static MethodInfo FindDeclared(Type t, string name, Func<MethodInfo, bool> sig)
        {
            if (t == null) return null;
            MethodInfo first = null;
            foreach (var mi in t.GetMethods(AccessTools.allDeclared))
            {
                if (mi == null || mi.Name != name) continue;
                if (sig(mi)) return mi;
                if (first == null) first = mi;
            }
            return first;   // zadna nie pasuje - pierwsza (sprawdzenie sygnatury ja odrzuci i napisze powod)
        }

        private static bool ArtemSig(MethodInfo mi)
        {
            var ps = mi != null ? mi.GetParameters() : null;
            return ps != null && ps.Length == 5 && ps[0].ParameterType == typeof(Agent) && ps[1].ParameterType == typeof(Agent);
        }

        internal static void ApplyAll(Harmony h)
        {
            var c = Settings.Current;
            if (!AnyKey(c)) { Log.Info("DuelGuard (m111-walka): wszystkie klucze pojedynku wylaczone przy starcie - haki niewpiete."); return; }
            // po recenzji: kazdy hak we wlasnym try - wyjatek jednego (przeciazenie po aktualizacji, h.Patch) nie wylacza pozostalych,
            // a linia stanu hakow pisze sie zawsze (finally)
            try
            {
                Type tStance = null, tLogic = null, tStances = null, inner = null;
                try
                {
                    tStance = QuartermasterLaw.FindType("RBMAI.Stance");
                    tLogic = QuartermasterLaw.FindType("RBMAI.StanceLogic");
                    tStances = QuartermasterLaw.FindType("RBMAI.AgentStances");
                    if (tStance == null || tLogic == null) _whyA = _whyB = _whyC = "RBM nieobecny";
                    else
                    {
                        _fPosture = AccessTools.Field(tStance, "posture");
                        _fMax = AccessTools.Field(tStance, "maxPosture");
                        if (tStances != null) _fStances = AccessTools.Field(tStances, "values");
                        inner = AccessTools.Inner(tLogic, "CreateMeleeBlowPatch");
                        if (inner == null) _whyA = _whyB = "brak StanceLogic+CreateMeleeBlowPatch";
                        if (_fPosture != null && _fMax != null && _fPosture.FieldType == typeof(float) && _fMax.FieldType == typeof(float))
                        {
                            // po recenzji: typowany dostep do pol (bez FieldInfo.GetValue - pudelkowanie float przy kazdym kontakcie)
                            _rPosture = AccessTools.FieldRefAccess<float>(tStance, "posture");
                            _rMax = AccessTools.FieldRefAccess<float>(tStance, "maxPosture");
                        }
                    }
                }
                catch (Exception e) { tStance = null; inner = null; _whyA = _whyB = _whyC = "wyjatek " + e.GetType().Name; Log.Error("DuelGuard.ApplyAll typy RBM", e); }

                // A) calculateDefenderPostureDamage / calculateAttackerPostureDamage
                if (inner != null)
                {
                    try
                    {
                        _mCalcDef = FindDeclared(inner, "calculateDefenderPostureDamage", CalcSig);
                        _mCalcAtt = FindDeclared(inner, "calculateAttackerPostureDamage", CalcSig);
                        if (!CalcSig(_mCalcDef) || !CalcSig(_mCalcAtt)) _whyA = "sygnatura calculate*PostureDamage nie pasuje (8 parametrow, [6] MeleeHitType)";
                        else
                        {
                            h.Patch(_mCalcDef, postfix: new HarmonyMethod(typeof(DuelGuard), nameof(DefDamagePostfix)));
                            _okA1 = Owned(_mCalcDef, h.Id);
                            h.Patch(_mCalcAtt, postfix: new HarmonyMethod(typeof(DuelGuard), nameof(AttDamagePostfix)));
                            _okA2 = Owned(_mCalcAtt, h.Id);
                            _whyA = "sygnatura OK";
                        }
                    }
                    catch (Exception e) { _whyA = "wyjatek " + e.GetType().Name; Log.Error("DuelGuard.ApplyAll hak A", e); }

                    // B) handleDefender / handleAttacker
                    try
                    {
                        var ts = tStance;
                        _mHDef = FindDeclared(inner, "handleDefender", mi => HandleSig(mi, ts, 18, 15));
                        _mHAtt = FindDeclared(inner, "handleAttacker", mi => HandleSig(mi, ts, 16, 14));
                        if (_rPosture == null || _rMax == null)
                            _whyB = "pola Stance.posture / maxPosture nie pasuja";
                        else if (!HandleSig(_mHDef, tStance, 18, 15) || !HandleSig(_mHAtt, tStance, 16, 14))
                            _whyB = "sygnatura handleDefender (18, [15]) / handleAttacker (16, [14]) nie pasuje";
                        else
                        {
                            h.Patch(_mHDef, prefix: new HarmonyMethod(typeof(DuelGuard), nameof(HandlePrefix)),
                                    postfix: new HarmonyMethod(typeof(DuelGuard), nameof(DefenderPostfix)));
                            _okB1 = Owned(_mHDef, h.Id);
                            h.Patch(_mHAtt, prefix: new HarmonyMethod(typeof(DuelGuard), nameof(HandlePrefix)),
                                    postfix: new HarmonyMethod(typeof(DuelGuard), nameof(AttackerPostfix)));
                            _okB2 = Owned(_mHAtt, h.Id);
                            _whyB = "sygnatura OK";
                        }
                    }
                    catch (Exception e) { _whyB = "wyjatek " + e.GetType().Name; Log.Error("DuelGuard.ApplyAll hak B", e); }
                }

                // C) HitStopLogic.TriggerHitStop(float, float, int) - prywatna, instancyjna (NIE TriggerPostureBreakHitStop - jednolinijkowa)
                if (tStance != null)
                {
                    try
                    {
                        var tHit = QuartermasterLaw.FindType("RBMAI.HitStopLogic");
                        if (tHit == null) _whyC = "brak RBMAI.HitStopLogic";
                        else
                        {
                            _mHitStop = AccessTools.DeclaredMethod(tHit, "TriggerHitStop", new[] { typeof(float), typeof(float), typeof(int) });
                            if (_mHitStop == null || _mHitStop.IsStatic || _mHitStop.ReturnType != typeof(void)) _whyC = "sygnatura TriggerHitStop(float, float, int) nie pasuje";
                            else
                            {
                                h.Patch(_mHitStop, prefix: new HarmonyMethod(typeof(DuelGuard), nameof(HitStopPrefix)));
                                _okC = Owned(_mHitStop, h.Id);
                                _whyC = "sygnatura OK";
                            }
                        }
                    }
                    catch (Exception e) { _whyC = "wyjatek " + e.GetType().Name; Log.Error("DuelGuard.ApplyAll hak C", e); }
                }

                // D) Artem - OnAgentHit (bez RBM tez)
                try
                {
                    var tArt = QuartermasterLaw.FindType("ArtemsLivelyAnimations.ArtemsLivelyAnimationsMissionLogic");
                    if (tArt == null) _whyD = "Artem nieobecny";
                    else
                    {
                        _mArtem = FindDeclared(tArt, "OnAgentHit", ArtemSig);
                        if (!ArtemSig(_mArtem)) _whyD = "sygnatura OnAgentHit nie pasuje";
                        else
                        {
                            // po recenzji: prog Artema (settings.HPThreshold, ArtemsLivelyAnimationsMissionLogic.cs:201; Jeff 20) do licznika
                            // "upadek Artema pominiety" - czytany raz na misje z instancji w prefiksie
                            try
                            {
                                _fArtSettings = AccessTools.Field(tArt, "settings");
                                _pArtHp = _fArtSettings != null ? AccessTools.Property(_fArtSettings.FieldType, "HPThreshold") : null;
                            }
                            catch { _fArtSettings = null; _pArtHp = null; }
                            h.Patch(_mArtem, prefix: new HarmonyMethod(typeof(DuelGuard), nameof(ArtemHitPrefix)));
                            _okD = Owned(_mArtem, h.Id);
                            _whyD = "sygnatura OK" + (_pArtHp != null ? "" : ", prog HP nieczytelny - licze od 33");
                        }
                    }
                }
                catch (Exception e) { _whyD = "wyjatek " + e.GetType().Name; Log.Error("DuelGuard.ApplyAll hak D", e); }
            }
            catch (Exception e) { Log.Error("DuelGuard.ApplyAll", e); }
            finally
            {
                Log.Info("DuelGuard (m111-walka): haki postury w pojedynku - " + HookState() + "; klucze czytane na zywo (DuelSkillGapWeight, DuelPerfectDefence, "
                         + "DuelPressureBursts, DuelNoSlowMotion, DuelNoInjuredCollapse), poza turniejem / arena / pojedynkiem bez zmian.");
            }
        }

        private static bool CalcSig(MethodInfo mi)
        {
            if (mi == null || !mi.IsStatic || mi.ReturnType != typeof(float)) return false;
            var p = mi.GetParameters();
            return p.Length == 8 && p[0].ParameterType == typeof(Agent) && p[1].ParameterType == typeof(Agent) && p[2].ParameterType == typeof(float)
                   && IsCollisionRef(p[3].ParameterType) && p[4].ParameterType == typeof(MissionWeapon) && p[5].ParameterType == typeof(float)
                   && IsHitType(p[6].ParameterType) && p[7].ParameterType == typeof(bool);
        }

        private static bool HandleSig(MethodInfo mi, Type tStance, int count, int hitIdx)
        {
            if (mi == null || !mi.IsStatic || mi.ReturnType != typeof(void)) return false;
            var p = mi.GetParameters();
            return p.Length == count && p[0].ParameterType == tStance && p[1].ParameterType == typeof(Agent) && p[2].ParameterType == typeof(Agent)
                   && IsCollisionRef(p[3].ParameterType) && IsHitType(p[hitIdx].ParameterType);
        }

        /// <summary>AfterStart (BattleSense) w pojedynku: czy nasze haki wciaz stoja (do linii misji).</summary>
        internal static void CheckOrder(Mission m)
        {
            string s;
            try
            {
                var id = VeteranAi.HarmonyId;
                s = "A " + (_mCalcDef != null && Owned(_mCalcDef, id) ? "TAK" : "NIE") + "/" + (_mCalcAtt != null && Owned(_mCalcAtt, id) ? "TAK" : "NIE")
                    + ", B " + (_mHDef != null && Owned(_mHDef, id) ? "TAK" : "NIE") + "/" + (_mHAtt != null && Owned(_mHAtt, id) ? "TAK" : "NIE")
                    + ", C " + (_mHitStop != null && Owned(_mHitStop, id) ? "TAK" : "NIE") + ", D " + (_mArtem != null && Owned(_mArtem, id) ? "TAK" : "NIE");
            }
            catch { s = "?"; }
            lock (RoutHold.Gate) { _order = s; }
        }

        // ------------------------------------------------------------ wspolne

        /// <summary>Rodzaj ciosu jak RBM (StanceLogic.cs:507-590): 1 trafienie, 2 blok bronia, 3 parowanie bronia, 4 tarcza zla strona, 5 blok tarcza,
        /// 6 parowanie tarcza, 7 chamber; 0 inne.</summary>
        private static int HitType(ref AttackCollisionData cd)
        {
            int cr = (int)cd.CollisionResult;
            if (cr == 5) return 7;
            if (!cd.AttackBlockedWithShield)
            {
                if (cr == 3) return 2;
                if (cr == 4) return 3;
                return cr == 1 ? 1 : 0;
            }
            bool correct = cd.CorrectSideShieldBlock;
            if (cr == 3) return correct ? 5 : 4;
            if (cr == 4) return correct ? 6 : 5;
            return 0;
        }

        private static bool HasShield(Agent a)
        {
            try { return VeteranAi.HasShield(a); } catch { return false; }
        }

        // pamiec ostatniego kontaktu: oba haki A i oba B tego samego ciosu licza roznice raz
        private static Agent _gDef, _gAtt;
        private static float _gTime = -1f, _gVal;

        /// <summary>Roznica umiejetnosci (s obroncy +20 z tarcza - s atakujacego) / 250.</summary>
        private static float Gap(Agent def, Agent att, Settings c, float now)
        {
            if (ReferenceEquals(def, _gDef) && ReferenceEquals(att, _gAtt) && now == _gTime) return _gVal;
            int h1, b1, h2, b2;
            float d = DuelAi.DuelSkill(def, c, out h1, out b1) + (HasShield(def) ? 20f : 0f);
            float a = DuelAi.DuelSkill(att, c, out h2, out b2);
            float g = (d - a) / 250f;
            _gDef = def; _gAtt = att; _gTime = now; _gVal = g;
            return g;
        }

        private static long _hookTicks;

        // ------------------------------------------------------------ hak A

        public static void DefDamagePostfix(Agent __0, Agent __1, ref AttackCollisionData __3, ref float __result)
        {
            var m = Mission.Current;
            if (!BattleSense.InDuel(m)) return;
            long t0 = Stopwatch.GetTimestamp();
            try
            {
                var c = Settings.Current;
                if (c == null || __0 == null || __1 == null) return;
                int ht = HitType(ref __3);
                if (ht < 2) return;
                float f = 1f, w = Weight(c);
                if (w > 0f) f *= Clamp(1f - w * Gap(__0, __1, c, m.CurrentTime), 0.6f, 1.4f);
                if (c.DuelPerfectDefence)
                {
                    if (ht == 3) f *= 0.5f;          // parowanie bronia (RBM przekazuje 0.5, nadpisuje 1)
                    else if (ht == 6) f *= 0.8f;     // parowanie tarcza (0.8)
                    else if (ht == 7) f *= 0.25f;    // chamber obronca (0.25)
                }
                __result *= f;
            }
            catch (Exception e) { RoutHold.Stumble(RoutHold.SiteDuel, "DuelGuard.DefDamage", e); }
            finally { System.Threading.Interlocked.Add(ref _hookTicks, Stopwatch.GetTimestamp() - t0); }
        }

        public static void AttDamagePostfix(Agent __0, Agent __1, ref AttackCollisionData __3, ref float __result)
        {
            var m = Mission.Current;
            if (!BattleSense.InDuel(m)) return;
            long t0 = Stopwatch.GetTimestamp();
            try
            {
                var c = Settings.Current;
                if (c == null || __0 == null || __1 == null) return;
                int ht = HitType(ref __3);
                if (ht < 2) return;
                float f = 1f, w = Weight(c);
                if (w > 0f) f *= Clamp(1f + w * Gap(__0, __1, c, m.CurrentTime), 0.6f, 1.4f);
                if (c.DuelPerfectDefence && ht == 7) f *= 2f;      // chamber atakujacy (2.0)
                __result *= f;
            }
            catch (Exception e) { RoutHold.Stumble(RoutHold.SiteDuel, "DuelGuard.AttDamage", e); }
            finally { System.Threading.Interlocked.Add(ref _hookTicks, Stopwatch.GetTimestamp() - t0); }
        }

        // ------------------------------------------------------------ hak B

        private static float PostureOf(object stance)
        {
            return _rPosture(stance);
        }

        public static void HandlePrefix(object __0, out float __state)
        {
            __state = -1f;
            if (__0 == null || !BattleSense.InDuel(Mission.Current)) return;
            try { __state = PostureOf(__0); }
            catch (Exception e) { __state = -1f; RoutHold.Stumble(RoutHold.SiteDuel, "DuelGuard.HandlePrefix", e); }
        }

        // ---- liczniki misji (pod RoutHold.Gate)
        private static int _brAttAi, _brAttPl, _brDefAi, _brDefPl, _slowCut, _artemSkip, _guardMsg, _defContacts;
        private static float _lastMsg = -100f;

        /// <summary>handleDefender: __1 = ofiara (obronca), stan = jego postura.</summary>
        public static void DefenderPostfix(object __0, Agent __1, Agent __2, ref AttackCollisionData __3, float __state)
        {
            if (__state < 0f || __0 == null) return;
            long t0 = Stopwatch.GetTimestamp();
            try
            {
                var m = Mission.Current;
                var c = Settings.Current;
                if (m == null || c == null || __1 == null) return;
                float after = PostureOf(__0);
                bool broke = after > __state || (after <= 0f && __state > 0f);
                int ht = HitType(ref __3);
                if (broke)
                    lock (RoutHold.Gate) { if (__1.IsMainAgent) _brDefPl++; else if (__1.IsAIControlled) _brDefAi++; }
                if (ht < 2 || __1.IsMainAgent || !__1.IsAIControlled) return;
                lock (RoutHold.Gate) { _defContacts++; }
                float now = m.CurrentTime;
                bool three = DuelCraft.NoteDefence(__1.Index, now);
                if (!c.DuelPressureBursts || !c.DuelAiEnabled) return;
                float max = _rMax(__0);
                bool low = max > 1f && after / max < DuelCraft.PressedPostureFrac;
                if ((three || low) && DuelAi.LevelOf(__1, c) >= DuelAi.PressedFrom) DuelCraft.Request(__1, DuelCraft.Pressed);
            }
            catch (Exception e) { RoutHold.Stumble(RoutHold.SiteDuel, "DuelGuard.DefenderPostfix", e); }
            finally { System.Threading.Interlocked.Add(ref _hookTicks, Stopwatch.GetTimestamp() - t0); }
        }

        /// <summary>handleAttacker: __2 = atakujacy (stan = jego postura), __1 = obronca.</summary>
        public static void AttackerPostfix(object __0, Agent __1, Agent __2, ref AttackCollisionData __3, float __state)
        {
            if (__state < 0f || __0 == null) return;
            long t0 = Stopwatch.GetTimestamp();
            try
            {
                var m = Mission.Current;
                var c = Settings.Current;
                if (m == null || c == null || __1 == null || __2 == null) return;
                float after = PostureOf(__0);
                bool broke = after > __state || (after <= 0f && __state > 0f);
                if (!broke) return;
                lock (RoutHold.Gate) { if (__2.IsMainAgent) _brAttPl++; else if (__2.IsAIControlled) _brAttAi++; }
                int ht = HitType(ref __3);
                if (ht < 2) return;
                // otwarcie: garda atakujacego pekla na obronie AI
                if (c.DuelPressureBursts && c.DuelAiEnabled && __1.IsAIControlled && !__1.IsMainAgent && DuelAi.LevelOf(__1, c) >= DuelAi.Floor)
                    DuelCraft.Request(__1, DuelCraft.Opening);
                // komunikat gracza: jego garda pekla na obroncy o 60+ punktow lepszym (najwyzej raz na 6 s)
                if (__2.IsMainAgent)
                {
                    float now = m.CurrentTime;
                    if (now - _lastMsg >= 6f && Gap(__1, __2, c, now) >= 0.24f)
                    {
                        _lastMsg = now;
                        lock (RoutHold.Gate) { _guardMsg++; }
                        string foe = "Your opponent";
                        try { if (!string.IsNullOrEmpty(__1.Name)) foe = __1.Name; } catch { }
                        InformationManager.DisplayMessage(new InformationMessage(foe + " turns your blow aside - your guard is broken!", Color.FromUint(4294940979u)));
                    }
                }
            }
            catch (Exception e) { RoutHold.Stumble(RoutHold.SiteDuel, "DuelGuard.AttackerPostfix", e); }
            finally { System.Threading.Interlocked.Add(ref _hookTicks, Stopwatch.GetTimestamp() - t0); }
        }

        // ------------------------------------------------------------ hak C i D

        /// <summary>HitStopLogic.TriggerHitStop(duration, slowFactor, priority): priorytet 3 = zlamanie postury.</summary>
        public static bool HitStopPrefix(int __2)
        {
            try
            {
                if (__2 != 3) return true;
                var c = Settings.Current;
                if (c == null || !c.DuelNoSlowMotion || !BattleSense.InDuel(Mission.Current)) return true;
                lock (RoutHold.Gate) { _slowCut++; }
                return false;
            }
            catch (Exception e) { RoutHold.Stumble(RoutHold.SiteDuel, "DuelGuard.HitStopPrefix", e); return true; }
        }

        /// <summary>Artem OnAgentHit(affectedAgent, ...): AI w pojedynku bez zestawu "injured" i bez powalenia.</summary>
        public static bool ArtemHitPrefix(object __instance, Agent __0)
        {
            try
            {
                // po recenzji: najpierw flaga pojedynku (w bitwach w polu wyjscie po odczycie statykow)
                if (!BattleSense.InDuel(Mission.Current)) return true;
                var c = Settings.Current;
                if (c == null || !c.DuelNoInjuredCollapse) return true;
                if (__0 == null || __0.IsMainAgent || !__0.IsAIControlled) return true;
                // liczymy tylko trafienia, ktore moglyby zwalic - ponizej progu Artema (Health < HPThreshold, ArtemsLivelyAnimationsMissionLogic.cs:201)
                if (__0.IsHuman && __0.Health > 0f)
                {
                    if (_artemHp < 0f) ReadArtemHp(__instance);
                    if (__0.Health < _artemHp) lock (RoutHold.Gate) { _artemSkip++; }
                }
                return false;
            }
            catch (Exception e) { RoutHold.Stumble(RoutHold.SiteDuel, "DuelGuard.ArtemHitPrefix", e); return true; }
        }

        /// <summary>Prog HP Artema raz na misje (z instancji logiki misji); nieczytelny - 33 (domyslny Artema) i "(prog ?)" w linii misji.</summary>
        private static void ReadArtemHp(object inst)
        {
            float v = -1f;
            try
            {
                var st = _fArtSettings != null && inst != null ? _fArtSettings.GetValue(inst) : null;
                var o = st != null && _pArtHp != null ? _pArtHp.GetValue(st, null) : null;
                if (o is int) v = (int)o;
                else if (o is float) v = (float)o;
            }
            catch { v = -1f; }
            _artemHpGuess = v <= 0f;
            _artemHp = v > 0f ? v : 33f;
        }

        // ------------------------------------------------------------ postura (BattleWind, glowny watek)

        private static float[] _pMul = new float[0];
        private static int _capped, _guarded;
        private static readonly HashSet<int> _cappedIds = new HashSet<int>(), _guardIds = new HashSet<int>();

        internal static void NotePosture(Agent a, float mul, bool capped)
        {
            int idx = a.Index;
            if (idx < 0) return;
            // po recenzji: wzrost i zapis tablicy pod blokada (InitializePosture idzie z wywolania zwrotnego zmiany broni - watek niepewny)
            lock (RoutHold.Gate)
            {
                if (idx >= _pMul.Length)
                {
                    var n = new float[Math.Max(idx + 1, _pMul.Length * 2 + 32)];
                    Array.Copy(_pMul, n, _pMul.Length);
                    _pMul = n;
                }
                _pMul[idx] = mul;
                if (_guardIds.Add(idx)) _guarded++;
                if (capped && _cappedIds.Add(idx)) _capped++;
            }
        }

        /// <summary>Linia DU: postura z RBM (AgentStances.values) i nasz mnoznik.</summary>
        internal static void AppendPosture(StringBuilder sb, Agent a)
        {
            try
            {
                var dict = _fStances != null ? _fStances.GetValue(null) as IDictionary : null;
                object st = dict != null && a != null && dict.Contains(a) ? dict[a] : null;
                if (st == null || _rPosture == null || _rMax == null) { sb.Append('-'); return; }
                sb.Append(_rPosture(st).ToString("0", Inv)).Append('/').Append(_rMax(st).ToString("0", Inv));
                int idx = a.Index;
                var pm = _pMul;
                if (idx >= 0 && idx < pm.Length && pm[idx] > 0f) sb.Append(" (x").Append(pm[idx].ToString("0.00", Inv)).Append(')');
            }
            catch { sb.Append('?'); }
        }

        // ------------------------------------------------------------ probkowanie akcji (glowny watek, tylko pojedynek)

        internal const int SampleAgents = 32;
        internal const float SampleEvery = 0.1f;
        internal const float RiposteWindow = 1.0f;
        private static float _acc;
        private static int[] _lastType = new int[0];
        private static bool[] _readyOpen = new bool[0];
        private static float[] _lastDef = new float[0];
        private static float[] _swingFor = new float[0];
        private static int _samples, _defSamples, _attacks, _ripostes, _feints, _kicks, _sampled;
        private static readonly HashSet<int> _sampledIds = new HashSet<int>();
        // kontakty (OnMeleeHit): [0] AI jako ofiara, [1] gracz jako ofiara; trafienie, blok, parowanie, chamber
        private static readonly int[,] _contacts = new int[2, 4];

        private static void GrowSample(int idx)
        {
            if (idx < _lastType.Length) return;
            int n = Math.Max(idx + 1, _lastType.Length * 2 + 32);
            var t = new int[n]; Array.Copy(_lastType, t, _lastType.Length);
            var r = new bool[n]; Array.Copy(_readyOpen, r, _readyOpen.Length);
            var d = new float[n]; for (int i = _lastDef.Length; i < n; i++) d[i] = -100f; Array.Copy(_lastDef, d, _lastDef.Length);
            var s = new float[n]; for (int i = _swingFor.Length; i < n; i++) s[i] = -100f; Array.Copy(_swingFor, s, _swingFor.Length);
            _lastType = t; _readyOpen = r; _lastDef = d; _swingFor = s;
        }

        /// <summary>Walczacy: czlowiek w druzynie z bronia w reku (widzowie trybun turnieju i areny bez broni - poza probka i lista meczu).</summary>
        private static bool IsFighter(Agent a)
        {
            return a != null && a.IsHuman && a.IsActive() && a.Team != null && a.GetPrimaryWieldedItemIndex() != EquipmentIndex.None;
        }

        /// <summary>BattleSense.OnMissionTick: co 0.1 s typ akcji gornej czesci ciala (kanal 1) najwyzej 32 AI.</summary>
        internal static void Sample(Mission m, float dt)
        {
            _acc += dt;
            if (_acc < SampleEvery) return;
            _acc = 0f;
            float now = m.CurrentTime;
            var list = m.Agents;
            int done = 0;
            for (int i = 0; i < list.Count && done < SampleAgents; i++)
            {
                var a = list[i];
                if (a == null || a.IsMainAgent || !a.IsAIControlled || !IsFighter(a)) continue;
                done++;
                int idx = a.Index;
                if (idx < 0) continue;
                GrowSample(idx);
                int ty = (int)a.GetCurrentActionType(1);
                int last = _lastType[idx];
                bool defend = ty >= 1 && ty <= 14;
                _samples++;
                if (_sampledIds.Add(idx)) _sampled++;
                if (defend) _defSamples++;
                if (ty == 19 && last != 19 && last != 20)
                {
                    _attacks++;
                    _readyOpen[idx] = true;
                    if (now - _lastDef[idx] <= RiposteWindow) _ripostes++;
                    float st = DuelCraft.StartedAt(idx);
                    if (st >= 0f && now - st <= DuelCraft.SwingAfter && _swingFor[idx] != st) { _swingFor[idx] = st; DuelCraft.WithSwing++; }
                }
                else if (ty == 20) _readyOpen[idx] = false;
                else if (defend && _readyOpen[idx]) { _feints++; _readyOpen[idx] = false; }
                if (ty == 28 && last != 28) _kicks++;
                _lastType[idx] = ty;
            }
        }

        /// <summary>BattleSense.OnMeleeHit (glowny watek): kontakty wedlug ofiary; obrona AI - czas do ripost.</summary>
        internal static void OnMeleeHit(Mission m, Agent attacker, Agent victim, ref AttackCollisionData cd)
        {
            if (victim == null || !victim.IsHuman || attacker == null) return;
            int side = victim.IsMainAgent ? 1 : (victim.IsAIControlled ? 0 : -1);
            if (side < 0) return;
            int cr = (int)cd.CollisionResult;
            int k = cr == 1 ? 0 : (cr == 3 ? 1 : (cr == 4 ? 2 : (cr == 5 ? 3 : -1)));
            if (k < 0) return;
            _contacts[side, k]++;
            if (side == 0 && (k == 1 || k == 2 || k == 3))
            {
                int idx = victim.Index;
                if (idx >= 0) { GrowSample(idx); _lastDef[idx] = m.CurrentTime; }
            }
        }

        // ------------------------------------------------------------ lista meczu turnieju

        private static object _lastMatch;
        private static float _rosterAt = -1f;
        private static int _rosters, _rosterTries;

        /// <summary>BattleSense.OnMissionTick w turnieju: 2 s po starcie meczu jedna linia - bron wydana, wymagana umiejetnosc, umiejetnosc w rece, s, L.</summary>
        internal static void Roster(Mission m, TournamentBehavior tb)
        {
            if (tb == null) return;
            // po recenzji: CurrentMatch zmienia sie w EndCurrentMatch (gracz jeszcze na drabince, nikt nie stoi) - zegar startuje dopiero, gdy
            // mecz ruszyl (State == 1 Started, jak sam TournamentBehavior.OnMissionTick); mecze pominiete (SkipMatch) nigdy nie stoja w Started
            object match = null;
            try
            {
                int ri = tb.CurrentRoundIndex;
                var cm = ri >= 0 && ri <= 3 ? tb.CurrentMatch : null;
                match = cm != null && (int)cm.State == 1 ? cm : null;
            }
            catch { match = null; }
            float now = m.CurrentTime;
            if (match != null && !ReferenceEquals(match, _lastMatch)) { _lastMatch = match; _rosterAt = now + 2f; _rosterTries = 0; return; }
            if (_rosterAt < 0f || now < _rosterAt) return;
            _rosterAt = -1f;
            var c = Settings.Current;
            var sb = new StringBuilder(800);
            sb.Append("Turniej (DU): runda ").Append(BattleSense.Round).Append(", mecz ").Append(++_rosters).Append(" |");
            var list = m.Agents;
            int n = 0;
            float dm = VeteranAi.DiffMulNow();
            for (int i = 0; i < list.Count; i++)
            {
                var a = list[i];
                if (!IsFighter(a)) continue;
                n++;
                int held, best;
                int s = DuelAi.DuelSkill(a, c, out held, out best);
                float lvl = DuelAi.Level(s, dm, BattleSense.Round, c);
                var w = a.WieldedWeapon;
                var it = !w.IsEmpty ? w.Item : null;
                string who = "?";
                try { who = a.Character != null ? a.Character.StringId : "?"; } catch { }
                sb.Append(' ').Append(a.IsMainAgent ? "GRACZ " : "").Append(who).Append(": ").Append(it != null ? it.StringId : "bez broni");
                if (it != null && it.RelevantSkill != null) sb.Append(" (wymaga ").Append(it.RelevantSkill.StringId).Append(' ').Append(it.Difficulty).Append(')');
                sb.Append(" trzyma ").Append(held).Append(", najlepsza ").Append(best).Append(", s ").Append(s).Append(", L ").Append(lvl.ToString("0.00", Inv))
                  .Append(a.IsMainAgent ? "" : (lvl < DuelAi.Floor ? " RBM" : " profil")).Append(';');
            }
            if (n > 0) Log.Info(sb.ToString());
            else if (++_rosterTries < 5) _rosterAt = now + 1f;   // jeszcze nikt nie wyciagnal broni - sprobuj za 1 s (najwyzej 5 razy)
        }

        // ------------------------------------------------------------ misja

        private static int _maxRound;

        internal static void NoteRound(int r) { if (r > _maxRound) _maxRound = r; }

        /// <summary>Poczatek misji pojedynku (BattleSense.EarlyStart): liczniki od zera.</summary>
        internal static void Begin(Mission m)
        {
            lock (RoutHold.Gate)
            {
                RoutHold.EnsureLocked(m);
                _brAttAi = _brAttPl = _brDefAi = _brDefPl = _slowCut = _artemSkip = _guardMsg = _defContacts = 0;
                _capped = _guarded = 0;
                _cappedIds.Clear(); _guardIds.Clear();
                DuelAi.ResetLocked();
                Array.Clear(_pMul, 0, _pMul.Length);
                _order = "?";
            }
            _artemHp = -1f; _artemHpGuess = false;
            _lastMsg = -100f;
            _gDef = _gAtt = null; _gTime = -1f;
            System.Threading.Interlocked.Exchange(ref _hookTicks, 0L);
            _acc = 0f;
            Array.Clear(_lastType, 0, _lastType.Length);
            Array.Clear(_readyOpen, 0, _readyOpen.Length);
            for (int i = 0; i < _lastDef.Length; i++) _lastDef[i] = -100f;
            for (int i = 0; i < _swingFor.Length; i++) _swingFor[i] = -100f;
            _samples = _defSamples = _attacks = _ripostes = _feints = _kicks = _sampled = 0;
            _sampledIds.Clear();
            Array.Clear(_contacts, 0, _contacts.Length);
            _lastMatch = null; _rosterAt = -1f; _rosters = 0; _rosterTries = 0;
            _maxRound = 0;
            DuelCraft.Reset();
        }

        /// <summary>Po recenzji: koniec misji pojedynku (BattleSense.Clean, po Write) - zadnych silnych odwolan do agentow ani meczu w statykach
        /// (Agent trzyma Mission - skonczony turniej wisialby w pamieci do nastepnego pojedynku).</summary>
        internal static void End()
        {
            lock (RoutHold.Gate) { DuelAi.ResetLocked(); }
            DuelCraft.Reset();
            _gDef = _gAtt = null; _gTime = -1f;
            _lastMatch = null; _rosterAt = -1f;
        }

        /// <summary>Koniec misji pojedynku (BattleSense.Clean): linia "Pojedynek (DU)".</summary>
        internal static void Write(Mission m, string kind)
        {
            var c = Settings.Current;
            var sb = new StringBuilder(1600);
            float minutes = _samples * SampleEvery / 60f;
            float ms = (float)(System.Threading.Interlocked.Read(ref _hookTicks) * 1000.0 / Stopwatch.Frequency);
            float mins = m != null ? m.CurrentTime / 60f : 0f;
            sb.Append("Pojedynek (DU): misja ").Append(kind).Append(" runda max ").Append(_maxRound)
              .Append(" (Duel AI ").Append(c != null && c.DuelAiEnabled ? "TAK" : "NIE").Append(", zrywy ").Append(c != null && c.DuelPressureBursts ? "TAK" : "NIE")
              .Append(", Fair Guard ").Append(c != null && c.DuelFairGuard ? "TAK" : "NIE").Append(", waga roznicy ").Append(Weight(c).ToString("0.00", Inv))
              .Append(", Perfect Defence ").Append(c != null && c.DuelPerfectDefence ? "TAK" : "NIE").Append(", bez zwolnienia ").Append(c != null && c.DuelNoSlowMotion ? "TAK" : "NIE")
              .Append(", bez upadku Artema ").Append(c != null && c.DuelNoInjuredCollapse ? "TAK" : "NIE").Append(", misja ").Append(mins.ToString("0.0", Inv)).Append(" min) | ");
            lock (RoutHold.Gate)
            {
                RoutHold.EnsureLocked(m);
                DuelAi.BandsLocked(sb);
                sb.Append(" | probkowanie co ").Append(SampleEvery.ToString("0.0", Inv)).Append(" s (najwyzej ").Append(SampleAgents).Append(" AI): agentow ").Append(_sampled)
                  .Append(", minut agenta ").Append(minutes.ToString("0.0", Inv)).Append(", zamachy ").Append(_attacks)
                  .Append(" (na minute agenta ").Append(minutes > 0.01f ? (_attacks / minutes).ToString("0.0", Inv) : "-").Append("), obrona ")
                  .Append(_samples > 0 ? (100f * _defSamples / _samples).ToString("0", Inv) : "-").Append("% probek, riposty ").Append(_ripostes)
                  .Append(" (do ").Append(RiposteWindow.ToString("0.0", Inv)).Append(" s po bloku / parowaniu; na obrone ")
                  .Append(_contacts[0, 1] + _contacts[0, 2] + _contacts[0, 3] > 0 ? ((float)_ripostes / (_contacts[0, 1] + _contacts[0, 2] + _contacts[0, 3])).ToString("0.00", Inv) : "-")
                  .Append("), zwody ").Append(_feints).Append(", kopniecia ").Append(_kicks)
                  .Append(" | kontakty: AI trafiony ").Append(_contacts[0, 0]).Append(", blok ").Append(_contacts[0, 1]).Append(", parowanie ").Append(_contacts[0, 2])
                  .Append(", chamber ").Append(_contacts[0, 3]).Append("; gracz trafiony ").Append(_contacts[1, 0]).Append(", blok ").Append(_contacts[1, 1])
                  .Append(", parowanie ").Append(_contacts[1, 2]).Append(", chamber ").Append(_contacts[1, 3])
                  .Append(" | postura peka: atakujacy AI ").Append(_brAttAi).Append(", gracz ").Append(_brAttPl).Append("; obronca AI ").Append(_brDefAi)
                  .Append(", gracz ").Append(_brDefPl).Append(" (obron AI w hakach ").Append(_defContacts).Append(')')
                  .Append(" | zrywy: otwarcie ").Append(DuelCraft.StartedOpening).Append(", nacisk ").Append(DuelCraft.StartedPressed).Append(" (z zamachem do ")
                  .Append(DuelCraft.SwingAfter.ToString("0.0", Inv)).Append(" s ").Append(DuelCraft.WithSwing).Append("; w przerwie ").Append(DuelCraft.Cooled)
                  .Append(", kolejka pelna ").Append(DuelCraft.Dropped).Append(", odswiezen ").Append(DuelCraft.Refreshed).Append(')')
                  .Append(" | zwolnienie tempa odrzucone ").Append(_slowCut).Append(", upadek Artema pominiety ").Append(_artemSkip)
                  .Append(" (prog HP ").Append(_artemHp > 0f ? _artemHp.ToString("0", Inv) : "-").Append(_artemHpGuess ? " ?" : "").Append(')')
                  .Append(", postura bohaterow w pojedynku ").Append(_guarded).Append(" (przycieta ").Append(_capped).Append("), komunikat gardy ").Append(_guardMsg)
                  .Append(", list meczu ").Append(_rosters)
                  .Append(" | haki ").Append(_order).Append(", czas hakow ").Append(ms.ToString("0.0", Inv)).Append(" ms")
                  .Append(" | potkniecia pojedynek ").Append(RoutHold.StumblesLocked(RoutHold.SiteDuel)).Append('.');
            }
            Log.Info(sb.ToString());
        }
    }
}
