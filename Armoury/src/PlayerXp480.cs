using System;
using System.Collections.Generic;
using System.Globalization;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// XP UMIEJETNOSCI GRACZA WOLNIEJ (480; Jeff 13.11: "zmniejsz przyrost doswiadczenia o 25% dla gracza czyli dla mnie, bo w 3 lata prawie zdobylem
    /// wszystko na ponad 300, za szybko sie rozwija bohater"). Klucz PlayerSkillXpMult (0.75 = 25% wolniej, 1.00 = jak dotad).
    /// JEDNO MIEJSCE: prefiks Priority.LowerThanNormal na HeroDeveloper.AddSkillXp(SkillObject, float, bool, bool) (klasa deklarujaca, DeclaredMethod) -
    /// ta sama metoda co SmithXp (m109). Kazda droga przyrostu XP umiejetnosci bohatera konczy sie tutaj (Hero.AddSkillXp tylko do niej przekazuje):
    /// walka gry i RBM, SkillLevelingManager (handel, zwiad, Roguery, Steward...), BK (ksiazki, edukacja, rada, rytualy), ROT (pojedynki), Spoils,
    /// nasze mody (kuznia, niewola RC), turnieje i arena.
    /// Tylko Hero.MainHero (porownanie na biezaco - po smierci gracza dziedzic), tylko isAffectedByFocusFactor = true i rawXp > 0:
    /// ChangeSkillLevel (poziom ustawiany wprost - tworzenie postaci, edukacja, zdarzenia) wola z false i zostaje dokladne. Towarzysze, rod, lordowie,
    /// wojsko (TroopRoster) bez zmian.
    /// TYLKO UMIEJETNOSC (jak SmithXp): oryginal daje GainRawXp juz uciete rawXp, wiec prefiks oddaje poziomowi postaci roznice
    /// GainRawXp(Round(przed) - Round(po)) przez delegat na prywatna metode gry - poziom postaci (skupienia, cechy) rosnie jak dotad.
    /// Kolejnosc na celu: ROT patch30 (Normal, x GlobalXPBonus na sluzbie) -> nasz 480 (LowerThanNormal) -> SmithXp (Low, Crafting x0.70 - u gracza
    /// razem 0.525) -> ksiega E8/P1 (SkillLedger, Last - liczy po cieciach). Kazdy prefiks oddaje poziomowi tylko swoja roznice, suma = Round(przed).
    /// Goracy punkt (ok. 20 tys. wywolan na dobe): najpierw porownanie referencji z Hero.MainHero, bez alokacji poza 3 pierwszymi liniami sesji.
    /// Bledy liczone (potkniecia), bez wylacznika. Linie "XP gracza (480): ..." - start sesji, 3 pierwsze wywolania, doba.
    /// </summary>
    internal static class PlayerXp480
    {
        internal const string P = "XP gracza (480): ";
        private const string Owner = "com.jeff.armoury.playerxp480";
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static Harmony _h;
        private static readonly HashSet<System.Reflection.MethodBase> _patched = new HashSet<System.Reflection.MethodBase>();
        private static string _hook = "nie wpiete";
        private static int _stumbles, _lines;
        private static int _dN, _dPlain;
        private static double _dBefore, _dAfter;
        private static Action<HeroDeveloper, float, bool> _gainRaw;
        private static bool _gainLooked;
        private static string _gainHook = "nie szukany";

        internal static float Factor
        {
            get
            {
                var s = Settings.Current;
                if (s == null || float.IsNaN(s.PlayerSkillXpMult)) return 1f;
                return Math.Max(0f, Math.Min(4f, s.PlayerSkillXpMult));
            }
        }

        private static bool Neutral(float f) { return f > 0.999f && f < 1.001f; }
        private static string F2(double v) { return v.ToString("0.00", Inv); }
        private static string F1(double v) { return v.ToString("0.#", Inv); }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            if (_stumbles <= 3 || _stumbles % 100 == 0) Log.Error("PlayerXp480." + where + " (potkniecie " + _stumbles + ")", e);
        }

        /// <summary>OnSessionLaunched: wpiecie prefiksu (raz na proces), zerowanie licznikow, linia startu.</summary>
        internal static void EnsureHooks()
        {
            _lines = 0;
            _dN = 0; _dPlain = 0; _dBefore = 0; _dAfter = 0;
            int others = -1;
            try
            {
                if (_h == null) _h = new Harmony(Owner);
                var m = AccessTools.DeclaredMethod(typeof(HeroDeveloper), "AddSkillXp", new[] { typeof(SkillObject), typeof(float), typeof(bool), typeof(bool) });
                if (m == null) _hook = "BRAK celu";
                else
                {
                    if (!_patched.Contains(m))
                    {
                        _h.Patch(m, prefix: new HarmonyMethod(typeof(PlayerXp480), nameof(XpPrefix)) { priority = Priority.LowerThanNormal });   // po ROT patch30 (Normal), przed SmithXp (Low) i ksiega (Last)
                        _patched.Add(m);
                    }
                    _hook = "wpieta";
                    try
                    {
                        var info = Harmony.GetPatchInfo(m);
                        if (info != null)
                        {
                            bool ours = false;
                            others = 0;
                            foreach (var p in info.Prefixes) { if (p.owner == Owner) ours = true; else others++; }
                            foreach (var p in info.Postfixes) if (p.owner != Owner) others++;
                            foreach (var p in info.Transpilers) if (p.owner != Owner) others++;
                            foreach (var p in info.Finalizers) if (p.owner != Owner) others++;
                            if (!ours) _hook = "NIE widac w GetPatchInfo";
                        }
                    }
                    catch { others = -1; }
                }
            }
            catch (Exception e) { _hook = "wyjatek " + e.GetType().Name; Stumble("EnsureHooks", e); }
            if (!_gainLooked)
            {
                _gainLooked = true;
                try
                {
                    var g = AccessTools.DeclaredMethod(typeof(HeroDeveloper), "GainRawXp", new[] { typeof(float), typeof(bool) });
                    _gainRaw = g != null ? AccessTools.MethodDelegate<Action<HeroDeveloper, float, bool>>(g) : null;
                    _gainHook = _gainRaw != null ? "TAK (GainRawXp)" : "NIE (brak GainRawXp - poziom postaci tez wolniej)";
                }
                catch (Exception e) { _gainRaw = null; _gainHook = "NIE (wyjatek " + e.GetType().Name + " - poziom postaci tez wolniej)"; Stumble("EnsureHooks(GainRawXp)", e); }
            }
            float f = Factor;
            Log.Info(P + "start sesji | mnoznik (klucz Player Skill XP Multiplier) " + F2(f) + (Neutral(f) ? " (1.00 - XP jak dotad, latka nic nie zmienia)" : "")
                     + " | latka HeroDeveloper.AddSkillXp " + _hook + " (Priority.LowerThanNormal - po ROT patch30, przed SmithXp i ksiega E8/P1)"
                     + " | poziom postaci bez ciecia " + _gainHook + " | inne latki na celu " + (others >= 0 ? others.ToString(Inv) : "?") + " | potkniecia " + _stumbles + ".");
        }

        /// <summary>Prefiks HeroDeveloper.AddSkillXp: XP umiejetnosci bohatera gracza (z czynnikiem nauki) x PlayerSkillXpMult; poziom postaci dostaje
        /// roznice od razu (GainRawXp). Nic innego nie zmienia.</summary>
        public static void XpPrefix(HeroDeveloper __instance, SkillObject skill, ref float rawXp, bool isAffectedByFocusFactor, bool shouldNotify)
        {
            if (__instance == null) return;
            Hero main = Hero.MainHero;
            if (main == null || (object)__instance.Hero != (object)main) return;
            if (!(rawXp > 0f)) return;
            try
            {
                if (!isAffectedByFocusFactor) { _dPlain++; return; }
                float f = Factor;
                if (Neutral(f)) { _dN++; _dBefore += rawXp; _dAfter += rawXp; return; }
                float before = rawXp;
                rawXp = before * f;
                // poziom postaci bez ciecia: oryginal da GainRawXp(Round(po)), tu reszta do Round(przed) (przy kluczu > 1 ujemna - poziom tez jak bez mnoznika)
                if (_gainRaw != null)
                {
                    int give = TaleWorlds.Library.MathF.Round(before) - TaleWorlds.Library.MathF.Round(rawXp);
                    if (give != 0) _gainRaw(__instance, give, shouldNotify);
                }
                _dN++; _dBefore += before; _dAfter += rawXp;
                if (_lines < 3)
                {
                    _lines++;
                    string sk = "?";
                    try { sk = skill != null ? skill.StringId : "?"; } catch { }
                    Log.Info(P + "XP " + sk + " " + F1(before) + " -> " + F1(rawXp) + " (mnoznik " + F2(f) + ").");
                }
            }
            catch (Exception e) { Stumble("XpPrefix", e); }
        }

        /// <summary>ArmouryBehavior.OnDailyTickBodyInner: linia doby (gdy cokolwiek sie dzialo albo sa potkniecia) i zerowanie.</summary>
        internal static void Daily(int day)
        {
            try
            {
                if (_dN > 0 || _dPlain > 0 || _stumbles > 0)
                    Log.Info(P + "mnoznik " + F2(Factor) + ", dzis XP przed/po: " + F1(_dBefore) + " / " + F1(_dAfter) + " w " + _dN + " wywolaniach (dzien " + day
                             + "), bez mnoznika (ustawianie poziomu) " + _dPlain + ", latka " + _hook + ", potkniecia " + _stumbles + ".");
            }
            catch (Exception e) { Stumble("Daily", e); }
            _dN = 0; _dPlain = 0; _dBefore = 0; _dAfter = 0;
        }
    }
}
