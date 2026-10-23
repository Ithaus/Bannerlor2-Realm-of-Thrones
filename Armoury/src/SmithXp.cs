using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// KOWALSTWO WOLNIEJ (m109-kuznia; Jeff 23.10: "smithing - za duzo teraz i za szybko rosnie, jak sie tworzy luki itd., trzeba to wszystko zmniejszyc o 30%,
    /// spowolnic"). Klucz SmithingXpFactor (0.70 = 30% wolniej, 1.00 = jak 44c03a05).
    /// DOWOD (log Jeffa 22.10 22:28-23:59, Armoury-2026-10-22_22-28-22): Smithing 63 -> 122 w jednej sesji, surowe XP kuzni 31 744, z tego ok. 58% luki
    /// (Forge.Smith: klik luku t1 60, t2 240, t3 540, t4 960 XP; pekniecie 30% tej kwoty; zlecenie kowalskie ok. 343 XP).
    /// JEDNO MIEJSCE: prefiks Priority.First na HeroDeveloper.AddSkillXp(SkillObject, float, bool, bool) (klasa deklarujaca). Kazda droga XP kuzni konczy sie
    /// tutaj (Hero.AddSkillXp tylko do niej przekazuje): kucie broni gry (CraftingCampaignBehavior - wolne kucie, zlecenia, przetop, rafinacja), CRAFT BK
    /// (ExecuteMainActionBK), nasze Forge.Smith / Begin / Finish / Smelt / SelfRepair, ForgeJob i zlecenia, SmithMenu, kowal polowy, pomocnik, SmeltTab, RBL.
    /// Nikt nie wola jej drugi raz za ta sama robote, wiec ciecie jest raz. Mnozymy rawXp tylko przy Crafting, isAffectedByFocusFactor = true i rawXp > 0:
    /// HeroDeveloper.ChangeSkillLevel (poziom ustawiany wprost - zdarzenia, edukacja, Kronika) wola ja z false i zostaje dokladne.
    /// Skutek uboczny (zgodny z "to wszystko zmniejszyc o 30%"): GainRawXp dostaje to samo rawXp, wiec poziom postaci z kowalstwa tez -30%.
    /// Kolejnosc na celu: nasz (First) -> ROT patch30 (x GlobalXPBonus na sluzbie u lorda - mnozenie przemienne) -> ksiega E8/P1 (SkillLedger, Last - liczy
    /// juz po cieciu). Odrzucone: postfiks GetFocusFactor (jednolinijkowa metoda - JIT moze ja wkleic w AddSkillXp i latka po cichu nie dziala, pulapka 19.10),
    /// ok. 12 miejsc w kodzie kuzni (latwo pominac albo przyciac dwa razy). Sufity (WeaponXpCapPerTier, XpCapPerTier, RefineXpCap) licza sie przed cieciem.
    /// Goracy punkt (ok. 20 tys. wywolan na dobe): dwa porownania przed wszystkim, bez alokacji poza 5 pierwszymi liniami sesji. Bledy liczone, bez wylacznika.
    /// Linie "Kowalstwo wolniej (m109): ..." - start sesji, pierwsze 5 wywolan, doba, test autotestu (plik armoury-kuznia.flag). Bez zapisu.
    /// </summary>
    internal static class SmithXp
    {
        internal const string P = "Kowalstwo wolniej (m109): ";
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static Harmony _h;
        private static readonly HashSet<System.Reflection.MethodBase> _patched = new HashSet<System.Reflection.MethodBase>();
        private static string _hook = "nie wpiete";
        private static SkillObject _crafting;
        private static int _stumbles, _sessionCalls, _lines;
        // doba: [0] gracz, [1] rod gracza, [2] inni
        private static readonly int[] _dN = new int[3];
        private static readonly double[] _dBefore = new double[3], _dAfter = new double[3];
        private static int _dPlain;   // Crafting z isAffectedByFocusFactor = false (ustawianie poziomu) - bez mnoznika
        private static bool _testDone;

        internal static float Factor
        {
            get
            {
                var s = Settings.Current;
                if (s == null || float.IsNaN(s.SmithingXpFactor)) return 1f;
                return Math.Max(0f, Math.Min(4f, s.SmithingXpFactor));
            }
        }

        private static bool Neutral(float f) { return f > 0.999f && f < 1.001f; }

        private static string F2(double v) { return v.ToString("0.00", Inv); }
        private static string F1(double v) { return v.ToString("0.#", Inv); }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            if (_stumbles <= 3 || _stumbles % 100 == 0) Log.Error("SmithXp." + where + " (potkniecie " + _stumbles + ")", e);
        }

        // ------------------------------------------------------------ latka (raz na proces, w kampanii)
        /// <summary>OnSessionLaunched: wpiecie prefiksu (raz na proces), zerowanie licznikow sesji, linia startu.</summary>
        internal static void EnsureHooks()
        {
            _sessionCalls = 0; _lines = 0; _testDone = false;
            ResetDay();
            try { _crafting = DefaultSkills.Crafting; } catch (Exception e) { _crafting = null; Stumble("EnsureHooks(Crafting)", e); }
            int others = -1;
            try
            {
                if (_h == null) _h = new Harmony("com.jeff.armoury.smithxp");
                var m = AccessTools.DeclaredMethod(typeof(HeroDeveloper), "AddSkillXp", new[] { typeof(SkillObject), typeof(float), typeof(bool), typeof(bool) });
                if (m == null) _hook = "BRAK celu";
                else
                {
                    if (!_patched.Contains(m))
                    {
                        _h.Patch(m, prefix: new HarmonyMethod(typeof(SmithXp), nameof(XpPrefix)) { priority = Priority.First });
                        _patched.Add(m);
                    }
                    _hook = "wpieta";
                    try
                    {
                        var info = Harmony.GetPatchInfo(m);
                        if (info != null)
                        {
                            others = 0;
                            foreach (var p in info.Prefixes) if (p.owner != "com.jeff.armoury.smithxp") others++;
                            foreach (var p in info.Postfixes) if (p.owner != "com.jeff.armoury.smithxp") others++;
                            foreach (var p in info.Transpilers) if (p.owner != "com.jeff.armoury.smithxp") others++;
                            foreach (var p in info.Finalizers) if (p.owner != "com.jeff.armoury.smithxp") others++;
                        }
                    }
                    catch { others = -1; }
                }
            }
            catch (Exception e) { _hook = "wyjatek " + e.GetType().Name; Stumble("EnsureHooks", e); }
            Log.Info(P + "start sesji | klucz SmithingXpFactor " + F2(Factor) + (Neutral(Factor) ? " (1.00 - XP jak dotad, latka nic nie zmienia)" : "")
                     + " | latka HeroDeveloper.AddSkillXp " + _hook + " (Priority.First) | inne latki na celu " + (others >= 0 ? others.ToString(Inv) : "?")
                     + " | test autotestu " + (TestWanted() ? "TAK" : "NIE") + " | potkniecia " + _stumbles + ".");
        }

        private static int GroupOf(Hero h)
        {
            if (h == null) return 2;
            if ((object)h == (object)Hero.MainHero) return 0;
            var c = h.Clan;
            return c != null && (object)c == (object)Clan.PlayerClan ? 1 : 2;
        }

        private static readonly string[] GroupNames = { "gracz", "rod", "inni" };

        /// <summary>Prefiks Priority.First HeroDeveloper.AddSkillXp: XP Smithing z czynnikiem nauki x SmithingXpFactor. Nic innego nie zmienia.</summary>
        public static void XpPrefix(HeroDeveloper __instance, SkillObject skill, ref float rawXp, bool isAffectedByFocusFactor)
        {
            if (!(rawXp > 0f) || skill == null || (object)skill != (object)_crafting) return;
            try
            {
                if (!isAffectedByFocusFactor) { _dPlain++; return; }
                float f = Factor;
                if (Neutral(f)) return;
                float before = rawXp;
                rawXp = before * f;
                int g = GroupOf(__instance != null ? __instance.Hero : null);
                _dN[g]++; _dBefore[g] += before; _dAfter[g] += rawXp;
                _sessionCalls++;
                if (_lines < 5)
                {
                    _lines++;
                    string who = "?";
                    try { who = __instance != null && __instance.Hero != null && __instance.Hero.Name != null ? __instance.Hero.Name.ToString() : "?"; } catch { }
                    Log.Info(P + "XP " + who + " (" + GroupNames[g] + ") " + F1(before) + " -> " + F1(rawXp) + " | wywolanie " + _sessionCalls + " w sesji.");
                }
            }
            catch (Exception e) { Stumble("XpPrefix", e); }
        }

        private static void ResetDay()
        {
            for (int i = 0; i < 3; i++) { _dN[i] = 0; _dBefore[i] = 0; _dAfter[i] = 0; }
            _dPlain = 0;
        }

        /// <summary>ArmouryBehavior.OnDailyTickBodyInner: linia doby (gdy cokolwiek sie dzialo albo sa potkniecia) i zerowanie; test autotestu raz na sesje.</summary>
        internal static void Daily(int day)
        {
            try
            {
                int n = _dN[0] + _dN[1] + _dN[2];
                if (n > 0 || _dPlain > 0 || _stumbles > 0)
                {
                    double pb = _dBefore[0] + _dBefore[1] + _dBefore[2], pa = _dAfter[0] + _dAfter[1] + _dAfter[2];
                    Log.Info(P + "dzien " + day + " | wywolan " + n + " (gracz " + _dN[0] + ", rod " + _dN[1] + ", inni " + _dN[2] + "), XP przed " + F1(pb) + " -> po " + F1(pa)
                             + " (gracz " + F1(_dBefore[0]) + " -> " + F1(_dAfter[0]) + "), bez mnoznika (ustawianie poziomu) " + _dPlain + ", potkniecia " + _stumbles + ".");
                }
            }
            catch (Exception e) { Stumble("Daily", e); }
            ResetDay();
            if (!_testDone && TestWanted())
            {
                _testDone = true;
                SelfTest();
            }
        }

        // ------------------------------------------------------------ autotest (plik armoury-kuznia.flag)
        internal const string AutoFlagName = "armoury-kuznia.flag";

        /// <summary>DLL autotestu CrashScribe (LogGate.Mode 1/2) i plik CrashScribe\armoury-kuznia.flag - jak MapChatLog.TestWanted. W zwyklej grze false.</summary>
        internal static bool TestWanted()
        {
            int m = LogGate.Mode;
            if (m != 1 && m != 2) return false;
            try
            {
                var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                return File.Exists(Path.Combine(Path.Combine(Path.Combine(docs, "Mount and Blade II Bannerlord"), "CrashScribe"), AutoFlagName));
            }
            catch { return false; }
        }

        /// <summary>Test na Hero.MainHero: 5 XP Smithing (przyrost wobec 5 x mnoznik gry x czynnik nauki - proporcja = klucz), kontrola Athletics (1.00),
        /// droga ustawiania poziomu (isAffectedByFocusFactor false - 1.00). Skutek dla bohatera pomijalny (kilka XP). Na sluzbie u lorda ROT pominiety
        /// (ROT patch30 mnozy wtedy XP przez GlobalXPBonus).</summary>
        private static void SelfTest()
        {
            try
            {
                var h = Hero.MainHero;
                var dev = h != null ? h.HeroDeveloper : null;
                if (dev == null || Campaign.Current == null || Campaign.Current.Models == null) { Log.Info(P + "test - pominiety (brak bohatera gracza)."); return; }
                if (_hook != "wpieta") { Log.Info(P + "test - pominiety (latka " + _hook + ")."); return; }
                if (NightRest.EnlistedNow) { Log.Info(P + "test - pominiety (gracz na sluzbie u lorda ROT - patch30 mnozy XP przez GlobalXPBonus)."); return; }
                float mult = Campaign.Current.Models.GenericXpModel.GetXpMultiplier(h);
                if (!(mult > 0f)) { Log.Info(P + "test - pominiety (mnoznik XP gry " + F2(mult) + ")."); return; }
                var craft = DefaultSkills.Crafting;
                var ath = DefaultSkills.Athletics;
                float f = Factor;
                // Smithing z czynnikiem nauki
                float e1 = 5f * mult * dev.GetFocusFactor(craft);
                float x0 = dev.GetSkillXp(craft);
                dev.AddSkillXp(craft, 5f, true, false);
                float d1 = dev.GetSkillXp(craft) - x0;
                // kontrola: inna umiejetnosc
                float e2 = 5f * mult * dev.GetFocusFactor(ath);
                float y0 = dev.GetSkillXp(ath);
                dev.AddSkillXp(ath, 5f, true, false);
                float d2 = dev.GetSkillXp(ath) - y0;
                // ustawianie poziomu (droga ChangeSkillLevel)
                float e3 = 5f * mult;
                float z0 = dev.GetSkillXp(craft);
                dev.AddSkillXp(craft, 5f, false, false);
                float d3 = dev.GetSkillXp(craft) - z0;
                double p = e1 > 0f ? d1 / e1 : 0, c = e2 > 0f ? d2 / e2 : 0, z = e3 > 0f ? d3 / e3 : 0;
                bool ok = Math.Abs(p - (Neutral(f) ? 1f : f)) <= 0.01 && Math.Abs(c - 1) <= 0.005 && Math.Abs(z - 1) <= 0.005;
                Log.Info(P + "test - Smithing 5 XP: przyrost " + d1.ToString("0.###", Inv) + " wobec " + e1.ToString("0.###", Inv) + " bez mnoznika, proporcja " + p.ToString("0.000", Inv)
                         + " (klucz " + F2(f) + ") | kontrola Athletics proporcja " + c.ToString("0.000", Inv) + " | ustawianie poziomu proporcja " + z.ToString("0.000", Inv)
                         + " - " + (ok ? "TAK" : "NIE") + ".");
            }
            catch (Exception e) { Stumble("SelfTest", e); Log.Info(P + "test - pominiety (wyjatek " + e.GetType().Name + ")."); }
        }
    }
}
