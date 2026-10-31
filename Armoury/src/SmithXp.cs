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
    /// JEDNO MIEJSCE: prefiks Priority.Low na HeroDeveloper.AddSkillXp(SkillObject, float, bool, bool) (klasa deklarujaca). Kazda droga XP kuzni konczy sie
    /// tutaj (Hero.AddSkillXp tylko do niej przekazuje): kucie broni gry (CraftingCampaignBehavior - wolne kucie, zlecenia, przetop, rafinacja), CRAFT BK
    /// (ExecuteMainActionBK), nasze Forge.Smith / Begin / Finish / Smelt / SelfRepair, ForgeJob i zlecenia, SmithMenu, kowal polowy, pomocnik, SmeltTab, RBL.
    /// Nikt nie wola jej drugi raz za ta sama robote, wiec ciecie jest raz. Mnozymy rawXp tylko przy Crafting, isAffectedByFocusFactor = true i rawXp > 0:
    /// HeroDeveloper.ChangeSkillLevel (poziom ustawiany wprost - zdarzenia, edukacja, Kronika) wola ja z false i zostaje dokladne.
    /// TYLKO UMIEJETNOSC (recenzja K3 - Jeff mowil o kowalstwie, nie o poziomie postaci): oryginal daje GainRawXp juz uciete rawXp, wiec prefiks oddaje
    /// poziomowi postaci roznice sam - GainRawXp(Round(przed) - Round(po)) przez delegat na prywatna metode gry; gra zaokragla kazde wywolanie GainRawXp,
    /// wiec suma = Round(przed), jak bez ciecia. Bez delegata (inna wersja gry) poziom postaci z kowalstwa tez wolniej - linia startu to mowi.
    /// Kolejnosc na celu: ROT patch30 (Normal - x GlobalXPBonus na sluzbie u lorda) -> nasz (Low - tnie i oddaje poziomowi juz z premia ROT, wiec poziom
    /// dokladnie jak bez ciecia) -> ksiega E8/P1 (SkillLedger, Last - liczy juz po cieciu). Odrzucone: postfiks GetFocusFactor (jednolinijkowa metoda - JIT
    /// moze ja wkleic w AddSkillXp i latka po cichu nie dziala, pulapka 19.10), ok. 12 miejsc w kodzie kuzni (latwo pominac albo przyciac dwa razy).
    /// Sufity (WeaponXpCapPerTier, XpCapPerTier, RefineXpCap) licza sie przed cieciem.
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
        // recenzja K3: poziom postaci bez ciecia - delegat na prywatne HeroDeveloper.GainRawXp(float, bool) (raz na proces)
        private static Action<HeroDeveloper, float, bool> _gainRaw;
        private static bool _gainLooked;
        private static string _gainHook = "nie szukany";
        // recenzja K2: test A mierzy ciecie w samej latce (przed -> po), niezaleznie od czynnika nauki
        private static bool _probe;
        private static float _pBefore = -1f, _pAfter = -1f;

        // 305d: rozbicie XP na zrodla - droga XP zostawia slowo zrodla tuz przed AddSkillXp (Tag), prefiks je zuzywa; bez slowa (albo starsze niz 1 s) = "inne"
        private static readonly string[] SrcNames = { "luk", "kusza", "amunicja", "pancerz", "miecz", "zlecenie", "przetop", "rafinacja", "inne" };
        private static readonly int[] _srcN = new int[9];
        private static readonly double[] _srcXp = new double[9];
        private static string _tag;
        private static int _tagAt;

        /// <summary>305d: slowo zrodla nastepnego XP Smithing (luk / kusza / amunicja / pancerz / miecz / zlecenie / przetop / rafinacja).</summary>
        internal static void Tag(string src) { _tag = src; _tagAt = Environment.TickCount; }

        /// <summary>306b (sonda autotestu kowal305): zdjecie slowa zrodla zostawionego przez podglad XP modelu - nie opisze nastepnego prawdziwego XP.</summary>
        internal static void ClearTag() { _tag = null; }

        private static int TakeTag()
        {
            string t = _tag;
            _tag = null;
            if (t == null || unchecked(Environment.TickCount - _tagAt) > 1000) return 8;
            for (int i = 0; i < 8; i++) if (SrcNames[i] == t) return i;
            return 8;
        }

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
                        _h.Patch(m, prefix: new HarmonyMethod(typeof(SmithXp), nameof(XpPrefix)) { priority = Priority.Low });   // po ROT patch30 (Normal), przed ksiega (Last)
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
            if (!_gainLooked)
            {
                _gainLooked = true;
                try
                {
                    var g = AccessTools.DeclaredMethod(typeof(HeroDeveloper), "GainRawXp", new[] { typeof(float), typeof(bool) });
                    _gainRaw = g != null ? AccessTools.MethodDelegate<Action<HeroDeveloper, float, bool>>(g) : null;
                    _gainHook = _gainRaw != null ? "TAK (GainRawXp)" : "NIE (brak GainRawXp - poziom postaci z kowalstwa tez wolniej)";
                }
                catch (Exception e) { _gainRaw = null; _gainHook = "NIE (wyjatek " + e.GetType().Name + " - poziom postaci z kowalstwa tez wolniej)"; Stumble("EnsureHooks(GainRawXp)", e); }
            }
            Log.Info(P + "start sesji | klucz SmithingXpFactor " + F2(Factor) + (Neutral(Factor) ? " (1.00 - XP jak dotad, latka nic nie zmienia)" : "")
                     + " | latka HeroDeveloper.AddSkillXp " + _hook + " (Priority.Low - po ROT patch30, przed ksiega E8/P1) | poziom postaci bez ciecia " + _gainHook
                     + " | inne latki na celu " + (others >= 0 ? others.ToString(Inv) : "?") + " | test autotestu " + (TestWanted() ? "TAK" : "NIE") + " | potkniecia " + _stumbles + ".");
        }

        private static int GroupOf(Hero h)
        {
            if (h == null) return 2;
            if ((object)h == (object)Hero.MainHero) return 0;
            var c = h.Clan;
            return c != null && (object)c == (object)Clan.PlayerClan ? 1 : 2;
        }

        private static readonly string[] GroupNames = { "gracz", "rod", "inni" };

        /// <summary>Prefiks Priority.Low HeroDeveloper.AddSkillXp: XP Smithing z czynnikiem nauki x SmithingXpFactor; poziom postaci dostaje roznice od razu
        /// (GainRawXp), wiec rosnie jak bez ciecia. Nic innego nie zmienia.</summary>
        public static void XpPrefix(HeroDeveloper __instance, SkillObject skill, ref float rawXp, bool isAffectedByFocusFactor, bool shouldNotify)
        {
            if (skill == null || (object)skill != (object)_crafting) return;
            if (!(rawXp > 0f)) { _tag = null; return; }   // recenzja 305d: slowo zrodla nie przechodzi na nastepne XP
            try
            {
                if (!isAffectedByFocusFactor) { _tag = null; _dPlain++; return; }
                int si = TakeTag();   // 305d: zrodlo XP (surowe, przed cieciem)
                _srcN[si]++; _srcXp[si] += rawXp;
                float f = Factor;
                if (Neutral(f)) return;
                float before = rawXp;
                rawXp = before * f;
                if (_probe) { _pBefore = before; _pAfter = rawXp; }
                // poziom postaci bez ciecia: oryginal da GainRawXp(Round(po)), tu reszta do Round(przed) (przy kluczu > 1 ujemna - poziom tez jak bez mnoznika)
                if (_gainRaw != null && __instance != null)
                {
                    int give = TaleWorlds.Library.MathF.Round(before) - TaleWorlds.Library.MathF.Round(rawXp);
                    if (give != 0) _gainRaw(__instance, give, shouldNotify);
                }
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
            for (int i = 0; i < _srcN.Length; i++) { _srcN[i] = 0; _srcXp[i] = 0; }
            SmithXpRule.ResetDay();
            HistoricalPrices.RoundFixed = 0;
        }

        /// <summary>305d: linia doby "Kowalstwo - zrodla XP (305d)" - surowe XP Smithing (przed cieciem SmithingXpFactor) wedlug zrodla, podstawienia zasady pracy
        /// i liczba wynikow liczonych od dawnej wartosci (SmithXpRoundFix). Tylko w dobie, w ktorej cos sie dzialo.</summary>
        private static void DailySources(int day)
        {
            int n = 0;
            for (int i = 0; i < _srcN.Length; i++) n += _srcN[i];
            if (n == 0 && SmithXpRule.DayFree + SmithXpRule.DayArmour + SmithXpRule.DayOrder + SmithXpRule.DayOrderBonus == 0 && HistoricalPrices.RoundFixed == 0) return;
            var sb = new System.Text.StringBuilder();
            double sum = 0;
            for (int i = 0; i < _srcN.Length; i++)
            {
                sum += _srcXp[i];
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(SrcNames[i]).Append(" ").Append(_srcN[i].ToString(Inv)).Append(" x / ").Append(F1(_srcXp[i]));
            }
            var s = Settings.Current;
            Log.Info("Kowalstwo - zrodla XP (305d): dzien " + day + " | surowe XP " + F1(sum) + " w " + n + " wywolaniach: " + sb
                     + " | zasada pracy " + (SmithXpRule.On ? "TAK (" + (s != null ? s.SmithXpPerStaminaTier : 0) + " XP na stamine x tier)" : "NIE")
                     + ": wolne kucie broni " + SmithXpRule.DayFree + " / " + F1(SmithXpRule.DayFreeXp) + ", pancerz CRAFT " + SmithXpRule.DayArmour + " / " + F1(SmithXpRule.DayArmourXp)
                     + ", zlecenie " + SmithXpRule.DayOrder + " / " + F1(SmithXpRule.DayOrderXp) + " + premia " + SmithXpRule.DayOrderBonus + " / " + F1(SmithXpRule.DayOrderBonusXp)
                     + " | od dawnej wartosci (SmithXpRoundFix " + (s != null && s.SmithXpRoundFix ? "TAK" : "NIE") + ") " + HistoricalPrices.RoundFixed
                     + " | potkniecia zasady " + SmithXpRule.Stumbles + ".");
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
            try { DailySources(day); } catch (Exception e) { Stumble("DailySources", e); }
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

        private const float TestXp = 100f;   // recenzja K2: 100 XP - przyrost mierzalny takze przy duzym XP umiejetnosci (float)

        /// <summary>Przyrost mierzalny: blad float przy odejmowaniu XP to ok. 2 ulp (2 x 2.4e-7 x XP) - musi byc maly wobec tolerancji proporcji.</summary>
        private static bool Measurable(float x0, float expected, double tol) { return expected > 0f && Math.Abs(x0) * 4.8e-7 < expected * tol; }

        private static string Why(float expected) { return expected > 0f ? "przyrost za maly do pomiaru" : "czynnik nauki 0 - ponad limit nauki"; }

        /// <summary>Test na Hero.MainHero (recenzja K2): 100 XP Smithing - (1) ciecie zmierzone w samej latce (przed -> po = klucz, niezaleznie od czynnika
        /// nauki), (2) przyrost XP umiejetnosci wobec 100 x mnoznik gry x czynnik nauki (proporcja = klucz; pominiety z powodem, gdy czynnik nauki 0 albo przyrost
        /// za maly do pomiaru), (3) poziom postaci +100 (recenzja K3 - bez ciecia; pominiety przy suficie XP); kontrola Athletics (1.00), droga ustawiania poziomu
        /// (isAffectedByFocusFactor false - 1.00). Skutek dla bohatera pomijalny. Na sluzbie u lorda ROT pominiety (ROT patch30 mnozy wtedy XP przez GlobalXPBonus).</summary>
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
                bool neutral = Neutral(f);
                float want = neutral ? 1f : f;
                // Smithing z czynnikiem nauki: ciecie w latce, przyrost umiejetnosci, poziom postaci
                float e1 = TestXp * mult * dev.GetFocusFactor(craft);
                float x0 = dev.GetSkillXp(craft);
                int t0 = dev.TotalXp;
                int el = TaleWorlds.Library.MathF.Round(TestXp);
                bool lvlCap = (long)t0 + el >= Campaign.Current.Models.CharacterDevelopmentModel.GetMaxSkillPoint();
                _pBefore = _pAfter = -1f;
                _probe = true;
                try { dev.AddSkillXp(craft, TestXp, true, false); }
                finally { _probe = false; }
                float d1 = dev.GetSkillXp(craft) - x0;
                int dl = dev.TotalXp - t0;
                double cut = _pBefore > 0f ? _pAfter / (double)_pBefore : -1;
                bool cutOk = neutral ? _pBefore < 0f : Math.Abs(cut - f) <= 0.001;
                bool pMeas = Measurable(x0, e1 * want, 0.01);
                double p = e1 > 0f ? d1 / e1 : 0;
                bool pOk = !pMeas || Math.Abs(p - want) <= 0.01;
                bool lvlOk = lvlCap || dl == el;
                // kontrola: inna umiejetnosc
                float e2 = TestXp * mult * dev.GetFocusFactor(ath);
                float y0 = dev.GetSkillXp(ath);
                dev.AddSkillXp(ath, TestXp, true, false);
                float d2 = dev.GetSkillXp(ath) - y0;
                bool cMeas = Measurable(y0, e2, 0.005);
                double c = e2 > 0f ? d2 / e2 : 0;
                bool cOk = !cMeas || Math.Abs(c - 1) <= 0.005;
                // ustawianie poziomu (droga ChangeSkillLevel)
                float e3 = TestXp * mult;
                float z0 = dev.GetSkillXp(craft);
                dev.AddSkillXp(craft, TestXp, false, false);
                float d3 = dev.GetSkillXp(craft) - z0;
                bool zMeas = Measurable(z0, e3, 0.005);
                double z = e3 > 0f ? d3 / e3 : 0;
                bool zOk = !zMeas || Math.Abs(z - 1) <= 0.005;
                bool ok = cutOk && pOk && lvlOk && cOk && zOk;
                Log.Info(P + "test - Smithing " + F1(TestXp) + " XP: ciecie w latce " + (_pBefore > 0f ? F1(_pBefore) + " -> " + F1(_pAfter) : "- -> -")
                         + " (proporcja " + (cut >= 0 ? cut.ToString("0.000", Inv) : "-") + ", klucz " + F2(f) + ")"
                         + " | umiejetnosc: " + (pMeas ? "przyrost " + d1.ToString("0.###", Inv) + " wobec " + e1.ToString("0.###", Inv) + " bez mnoznika, proporcja " + p.ToString("0.000", Inv)
                                                        : "pominieta (" + Why(e1) + ")")
                         + " | poziom postaci " + (lvlCap ? "pominiety (sufit XP)" : "+" + dl.ToString(Inv) + " wobec " + el.ToString(Inv))
                         + " | kontrola Athletics " + (cMeas ? "proporcja " + c.ToString("0.000", Inv) : "pominieta (" + Why(e2) + ")")
                         + " | ustawianie poziomu " + (zMeas ? "proporcja " + z.ToString("0.000", Inv) : "pominiete (" + Why(e3) + ")")
                         + " - " + (ok ? "TAK" : "NIE") + ".");
            }
            catch (Exception e) { Stumble("SelfTest", e); Log.Info(P + "test - pominiety (wyjatek " + e.GetType().Name + ")."); }
        }
    }
}
