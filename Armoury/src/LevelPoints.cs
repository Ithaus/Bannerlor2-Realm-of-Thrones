using System;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.GameComponents;

namespace Armoury
{
    /// <summary>
    /// RP (decyzja Jeffa 14.10): punkty z poziomow postaci - "co 3. poziom jeden punkt cechy" (zamiast co 4.) i "co 4. poziom dodatkowy punkt
    /// skupienia" (oprocz +1 skupienia z gry na kazdy poziom i +1 BK co 5. poziom). Jedna regula dla gracza i AI.
    ///
    /// Gra 1.4.8 (dekompilacja dec32): punkty daje tylko HeroDeveloper w dwoch miejscach, oba czytaja model rozwoju:
    ///  - awans: CheckLevel robi Hero.Level++ i wola OnGainLevel (HeroDeveloper.cs:252-269, 336-344) - +FocusPointsPerLevel skupienia,
    ///    +1 cechy gdy Level % LevelsPerAttributePoint == 0, potem zdarzenie HeroLevelledUp (tu BK BKEducationBehavior dodaje +1 skupienia co 5);
    ///    wiele poziomow naraz = petla, OnGainLevel raz na kazdy poziom; wczytanie zapisu niczego nie powtarza (pula jest w zapisie);
    ///  - pula NPC przy tworzeniu: InitializeHeroDeveloper -> CheckLevel(false) -> SetupDefaultPoints (HD:282-286) NADPISUJE pule:
    ///    skupienie (Level - 1) x FocusPointsPerLevel + FocusPointsAtStart, cechy (Level - 1) / LevelsPerAttributePoint + AttributePointsAtStart
    ///    (to, co dal awans w CheckLevel(false), przepada - takze +1 BK; tak jest w grze). Wolane z HeroCreator, Hero (XML), AgingCampaignBehavior
    ///    (dzieci spoza rodu gracza), InitialChildGeneration, ResetCharacterStats.
    /// Czynny model to BKLearningModel (BK dodaje go w OnGameStart, wygrywa z NavalDLC) - dziedziczy po DefaultCharacterDevelopmentModel i
    /// NIE nadpisuje LevelsPerAttributePoint; NavalCharacterDevelopmentModel nadpisuje, ale wola BaseModel (= model gry). Getter czyta tez
    /// okno postaci (CharacterDeveloperVM: tekst ATTRIBUTE_EVERY_LEVEL) - pokaze nowa liczbe.
    ///
    /// Latki (wpinane w menu glownym, przed pierwsza kampania - metody prywatne HeroDeveloper nie moga byc wklejone przez JIT w wolajacych):
    ///  1) postfiks gettera LevelsPerAttributePoint w modelu gry i kazdym modelu, ktory go sam deklaruje: wynik = AttributePointEveryLevels
    ///     (0 = bez zmian); PRZYPISANIE, nie dodawanie - lancuch modeli (NavalDLC -> BaseModel) daje ten sam wynik;
    ///  2) prefiks HeroDeveloper.OnGainLevel: +1 skupienia, gdy nowy poziom dzieli sie przez ExtraFocusEveryLevels (raz na wywolanie = raz na poziom);
    ///     postfiks tylko liczy i pisze linie przy awansie gracza;
    ///  3) postfiks HeroDeveloper.SetupDefaultPoints: pula NPC + poziom / ExtraFocusEveryLevels (tyle, ile gracz dostalby z awansow do tego poziomu).
    /// </summary>
    internal static class LevelPoints
    {
        private static bool _applied;
        private static int _getters;               // ile getterow LevelsPerAttributePoint wpietych
        private static bool _gainHooked, _setupHooked;
        private static int _errors;
        // liczniki od poprzedniej linii startu sesji (tylko log)
        private static int _setups, _setupFocus;   // pule przy tworzeniu bohatera (SetupDefaultPoints) i dodane w nich skupienie
        private static int _aiLevels, _aiFocus, _aiAttr;   // awanse innych bohaterow w trakcie gry: liczba, dodatkowe skupienie, punkty cech

        /// <summary>Co ile poziomow (0 = jak gra / wylaczone); przyciete do 0..62 (wyzej nie ma poziomow).</summary>
        internal static int Every(int v) { return v < 0 ? 0 : (v > 62 ? 62 : v); }
        internal static int AttrEvery { get { var s = Settings.Current; return s == null ? 0 : Every(s.AttributePointEveryLevels); } }
        internal static int FocusEvery { get { var s = Settings.Current; return s == null ? 0 : Every(s.ExtraFocusEveryLevels); } }

        public struct GainState { public int Focus; public int Attr; public bool Extra; }

        /// <summary>(1) Postfiks gettera: co ile poziomow punkt cechy.</summary>
        public static void AttrGetterPostfix(ref int __result)
        {
            int n = AttrEvery;
            if (n > 0) __result = n;
        }

        /// <summary>(2) Prefiks awansu: Hero.Level jest juz nowym poziomem (CheckLevel: Level++ przed OnGainLevel). Dodatkowe skupienie przed
        /// zdarzeniem HeroLevelledUp (sluchacze widza pelna pule), stan sprzed awansu do linii gracza.</summary>
        public static void GainPrefix(HeroDeveloper __instance, out GainState __state)
        {
            __state = default(GainState);
            try
            {
                __state.Focus = __instance.UnspentFocusPoints;
                __state.Attr = __instance.UnspentAttributePoints;
                var h = __instance.Hero;
                int f = FocusEvery;
                if (h == null || f <= 0 || h.Level <= 0 || h.Level % f != 0) return;
                __instance.UnspentFocusPoints = __instance.UnspentFocusPoints + 1;
                __state.Extra = true;
            }
            catch (Exception e) { if (_errors++ < 5) Log.Error("LevelPoints.GainPrefix", e); }
        }

        /// <summary>(2) Postfiks awansu: liczniki AI i linia przy awansie gracza (tylko w trakcie gry - nie przy tworzeniu kampanii).
        /// Recenzja RP: liczniki AI tylko przy shouldNotify - InitializeHeroDeveloper (tworzenie bohatera w trakcie gry: wedrowcy, notable, gentry BK,
        /// dzieci) wola CheckLevel(false) i te poziomy nadpisuje potem SetupDefaultPoints; bez tego warunku zawyzaly "AI od startu sesji".</summary>
        public static void GainPostfix(HeroDeveloper __instance, GainState __state, bool shouldNotify)
        {
            try
            {
                var c = Campaign.Current;
                var h = __instance.Hero;
                if (c == null || !c.GameStarted || h == null) return;
                int df = __instance.UnspentFocusPoints - __state.Focus;
                int da = __instance.UnspentAttributePoints - __state.Attr;
                if (h != Hero.MainHero)
                {
                    if (!shouldNotify) return;
                    _aiLevels++;
                    if (__state.Extra) _aiFocus++;
                    if (da > 0) _aiAttr += da;
                    return;
                }
                var m = c.Models.CharacterDevelopmentModel;
                int fpl = m != null ? m.FocusPointsPerLevel : 1;
                int lpa = m != null ? m.LevelsPerAttributePoint : 4;
                int ex = __state.Extra ? 1 : 0;
                Log.Info("Rozwoj postaci (RP): awans gracza na L" + h.Level
                         + " | skupienie +" + df + " (gra " + fpl + ", Armoury co " + FocusEvery + " +" + ex + ", inne " + (df - fpl - ex)
                         + (h.Level % 5 == 0 ? " - BK co 5" : "") + ")"
                         + " | cechy +" + da + " (co " + lpa + (lpa > 0 && h.Level % lpa == 0 ? " - ten poziom" : "") + ")"
                         + " | wolne teraz: skupienie " + __instance.UnspentFocusPoints + ", cechy " + __instance.UnspentAttributePoints
                         + " | AI od startu sesji: awansow " + _aiLevels + ", +" + _aiFocus + " skupienia (Armoury), +" + _aiAttr + " cech");
            }
            catch (Exception e) { if (_errors++ < 5) Log.Error("LevelPoints.GainPostfix", e); }
        }

        /// <summary>(3) Postfiks puli przy tworzeniu bohatera: + poziom / ExtraFocusEveryLevels skupienia (cechy liczy juz getter (1)).</summary>
        public static void SetupPostfix(HeroDeveloper __instance)
        {
            try
            {
                var h = __instance.Hero;
                int f = FocusEvery;
                if (h == null || f <= 0 || h.Level <= 0) return;
                int add = h.Level / f;
                _setups++;
                if (add <= 0) return;
                __instance.UnspentFocusPoints = __instance.UnspentFocusPoints + add;
                _setupFocus += add;
            }
            catch (Exception e) { if (_errors++ < 5) Log.Error("LevelPoints.SetupPostfix", e); }
        }

        /// <summary>Wpiecie w menu glownym (SubModuleMain.OnBeforeInitialModuleScreenSetAsRoot), raz na proces. Latki zawsze; klucze czytane na zywo
        /// (0 = gra bez zmian).</summary>
        internal static void ApplyAll(Harmony harmony)
        {
            if (_applied) return;
            _applied = true;
            var post = new HarmonyMethod(typeof(LevelPoints), nameof(AttrGetterPostfix)) { priority = Priority.Last };
            try
            {
                var g = AccessTools.PropertyGetter(typeof(DefaultCharacterDevelopmentModel), "LevelsPerAttributePoint");
                if (g != null) { harmony.Patch(g, postfix: post); _getters++; }
            }
            catch (Exception e) { Log.Error("LevelPoints.Patch(DefaultCharacterDevelopmentModel)", e); }
            // modele z modow, ktore same deklaruja getter (NavalDLC wola BaseModel - przypisanie daje ten sam wynik)
            // recenzja RP: caly skan we wlasnym try, filtr typu w try per typ - wywrotka skanu nie moze zabrac latek awansu i puli NPC ponizej;
            // klasa abstrakcyjna tez (moze deklarowac gotowy getter, ktory dziedziczy model czynny) - pomijany tylko getter abstrakcyjny
            try
            {
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (!RefersCampaign(asm)) continue;
                    Type[] types;
                    try { types = asm.GetTypes(); }
                    catch (ReflectionTypeLoadException rtle) { types = rtle.Types; }
                    catch { continue; }
                    if (types == null) continue;
                    foreach (var t in types)
                    {
                        try
                        {
                            if (t == null || t == typeof(DefaultCharacterDevelopmentModel) || !typeof(CharacterDevelopmentModel).IsAssignableFrom(t)) continue;
                            var p = t.GetProperty("LevelsPerAttributePoint", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                            var g = p != null ? p.GetGetMethod(true) : null;
                            if (g == null || g.IsAbstract) continue;
                            harmony.Patch(g, postfix: post);
                            _getters++;
                        }
                        catch (Exception e) { Log.Error("LevelPoints.Patch(" + (t != null ? t.FullName : "?") + ")", e); }
                    }
                }
            }
            catch (Exception e) { Log.Error("LevelPoints.Patch(skan modeli)", e); }
            try
            {
                var gain = AccessTools.Method(typeof(HeroDeveloper), "OnGainLevel", new[] { typeof(bool) });
                if (gain != null)
                {
                    harmony.Patch(gain, prefix: new HarmonyMethod(typeof(LevelPoints), nameof(GainPrefix)) { priority = Priority.First },
                                  postfix: new HarmonyMethod(typeof(LevelPoints), nameof(GainPostfix)) { priority = Priority.Last });
                    _gainHooked = true;
                }
            }
            catch (Exception e) { Log.Error("LevelPoints.Patch(OnGainLevel)", e); }
            try
            {
                var setup = AccessTools.Method(typeof(HeroDeveloper), "SetupDefaultPoints", Type.EmptyTypes);
                if (setup != null)
                {
                    harmony.Patch(setup, postfix: new HarmonyMethod(typeof(LevelPoints), nameof(SetupPostfix)) { priority = Priority.Last });
                    _setupHooked = true;
                }
            }
            catch (Exception e) { Log.Error("LevelPoints.Patch(SetupDefaultPoints)", e); }
            Log.Info("Rozwoj postaci (RP): wpiete - getter LevelsPerAttributePoint " + _getters + ", awans " + (_gainHooked ? "tak" : "NIE")
                     + ", pula NPC " + (_setupHooked ? "tak" : "NIE"));
        }

        private static bool RefersCampaign(Assembly asm)
        {
            try
            {
                var n = asm.GetName().Name;
                if (n == "TaleWorlds.CampaignSystem") return true;
                foreach (var r in asm.GetReferencedAssemblies())
                    if (r.Name == "TaleWorlds.CampaignSystem") return true;
            }
            catch { }
            return false;
        }

        /// <summary>Linia startu sesji (ArmouryBehavior.OnSessionLaunched, PO McmSettings.Apply): klucze, czynny model, przyklad punktow z poziomow
        /// wedlug starej i nowej reguly, pule bohaterow od poprzedniej linii (nowa kampania: lordowie tworzeni przed ta linia).</summary>
        internal static void SessionStart()
        {
            var c = Campaign.Current;
            var m = c != null ? c.Models.CharacterDevelopmentModel : null;
            int a = AttrEvery, f = FocusEvery;
            int fpl = m != null ? m.FocusPointsPerLevel : 1;
            int lpa = m != null ? m.LevelsPerAttributePoint : 4;
            int fas = m != null ? m.FocusPointsAtStart : 5;
            int aas = m != null ? m.AttributePointsAtStart : 15;
            bool bk = AccessTools.TypeByName("BannerKings.Behaviours.BKEducationBehavior") != null;
            var sb = new System.Text.StringBuilder();
            sb.Append("Rozwoj postaci (RP): start sesji | AttributePointEveryLevels ").Append(a).Append(a == 0 ? " (gra)" : "")
              .Append(", ExtraFocusEveryLevels ").Append(f).Append(f == 0 ? " (wylaczone)" : "")
              .Append(" | czynny model ").Append(m != null ? m.GetType().Name : "-").Append(": LevelsPerAttributePoint ").Append(lpa)
              .Append(", FocusPointsPerLevel ").Append(fpl).Append(", start NPC skupienie ").Append(fas).Append(" cechy ").Append(aas)
              .Append(" | latki: getter ").Append(_getters).Append(", awans ").Append(_gainHooked ? "tak" : "NIE").Append(", pula NPC ").Append(_setupHooked ? "tak" : "NIE")
              .Append(" | z poziomow (gracz").Append(bk ? ", BK +1 skupienia co 5" : "").Append("), nowa / gra:");
            foreach (int lv in new[] { 10, 30, 62 })
            {
                int attrNew = lpa > 0 ? lv / lpa : 0, attrOld = lv / 4;
                int focOld = lv * fpl + (bk ? lv / 5 : 0), focNew = focOld + (f > 0 ? lv / f : 0);
                sb.Append(" L").Append(lv).Append(" cechy ").Append(attrNew).Append("/").Append(attrOld)
                  .Append(", skupienie ").Append(focNew).Append("/").Append(focOld).Append(";");
            }
            int l20 = 20;
            sb.Append(" NPC tworzony na L20: pula cech ").Append((lpa > 0 ? (l20 - 1) / lpa : 0) + aas).Append("/").Append((l20 - 1) / 4 + aas)
              .Append(", skupienia ").Append((l20 - 1) * fpl + fas + (f > 0 ? l20 / f : 0)).Append("/").Append((l20 - 1) * fpl + fas)
              .Append(" | pule bohaterow od poprzedniej linii: ").Append(_setups).Append(", +").Append(_setupFocus).Append(" skupienia")
              .Append(" | potkniecia ").Append(_errors);
            Log.Info(sb.ToString());
            _setups = _setupFocus = 0;
            _aiLevels = _aiFocus = _aiAttr = 0;
        }
    }
}
