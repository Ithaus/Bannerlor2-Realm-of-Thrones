using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// BLEDY PERKOW BANNERKINGS (504 = P3 audytu perkow 14.11, docs/audyty-2026-11-14/PERKI.md lista B pkt 4-9; Jeff 14.11 "ok" / "78-105 ok, rob po kolei").
    /// Siedem bledow BK, kazdy potwierdzony w IL zainstalowanego BannerKings.dll (Modules/BannerKings.Redux) i w dekompilacji:
    ///  1 FianRanger (styl Fian = kultura battania = Polnoc w ROT): BKMapTrackTweakPatches.GetTrackLifePostfix mnozy zycie sladu x0.2 (opis: +20%).
    ///    Postfiks ma 67 bajtow IL - moglby byc wkompilowany (inline) w otoczke Harmony celu, wiec transpiler na nim nie jest pewny: latka BK
    ///    zdjeta z DefaultMapTrackModel.GetTrackLife (Unpatch tym samym MethodInfo) i zastapiona nasza (klucz NIE = x0.2 jak BK).
    ///    Recenzja 504: slady zostawiaja tylko partie inne niz gracza (CanPartyLeaveTrack wyklucza IsMainParty) i tylko gracz je czyta - wiec
    ///    jak Scouting.Tracker w GetTrackLife gry: gdy WODZ PARTII GRACZA ma Ranger, kazdy tworzony slad x1.2 (nie slad partii z perkiem).
    ///  2 JawwalDuneRider: BKPartySpeedTweakPatches.CalculateFinalSpeedPostfix - AddFactor(0.8) na pustyni (opis: 8%). Transpiler na postfiksie BK
    ///    (851 bajtow IL - nie do wkompilowania): ldc.r4 0.8 po get_JawwalDuneRider -> DuneFactor() (0.08; klucz NIE = 0.8).
    ///  3 TheologyMatrimony: BKMarriageModel.GetInfluenceCost - Helpers.ApplyPerk(..., primary: true) = +5% (premia na ocene malzonka), a koszt
    ///    wplywu ma druga polowa -15%. Transpiler: call Helpers.ApplyPerk (po ldc.i4.1) -> MatrimonyApply (primary false = SecondaryBonus,
    ///    licznik tylko gdy bohater ma perk; klucz NIE = primary jak BK), dalej oryginalny Helpers.ApplyPerk przez delegat.
    ///  4 LordshipSellswordCareer: BKDiplomacyModel.GetMercenaryDownPayment - to samo (+15% z punktow kariery zamiast +10% zaliczki). Transpiler jw.
    ///  5 ScholarshipTutor: BKEducationBehavior.ApplyScholarshipTutorEffect - Count((Hero _) => hero.GetPerkValue(Tutor)), hero = dorastajace
    ///    DZIECKO (lambda nie patrzy na swoj parametr) - nigdy nie dziala. Transpiler na OnHeroComesOfAge (wolanym przez zdarzenie, nie do
    ///    wkompilowania): call ApplyScholarshipTutorEffect -> TutorEffect(hero): +1 punkt cechy, gdy glowa rodu (zywa, nie samo dziecko) ma perk
    ///    Tutor (504-a, Jeff: tylko glowa rodu); klucz NIE = oryginal BK.
    ///  6 Riding.CavalryTactics: BKVolunteerModel.GetDailyVolunteerProductionProbability liczy po swojemu (GetDraftEfficiency) i gubi +30% gry dla
    ///    konnego ochotnika (DefaultVolunteerModel:104-107). Postfiks: warunki gry (miasto albo wies przy miescie, ochotnik w miejscu jest konny,
    ///    PerkHelper.GetPerkValueForTown) -> wynik x(1 + PrimaryBonus). Levy.ProbabilityPostfix na tej samej metodzie dziala tylko dla PUSTEGO
    ///    miejsca, nasz tylko dla ZAJETEGO (konny ochotnik) - rozlaczne, kolejnosc bez znaczenia.
    ///  7 Riding.WellStraped: BKLoyaltyModel.CalculateLoyaltyChangeInternal (sciezka osad z populacja BK) nie ma perka (gubernator +0.5/dobe).
    ///    Postfiks na BKLoyaltyModel.CalculateLoyaltyChange (klasa deklarujaca; BKROTLoyaltyModel wola base) - tylko gdy osada ma populacje BK
    ///    (inaczej BK wola model gry, ktory perk ma - bez podwojenia): PerkHelper.AddPerkBonusForTown(WellStraped) jak w grze.
    /// Kazda latka sprawdza w IL, czy BK juz naprawil (wtedy nie wpinamy). Latki raz na proces, z OnSessionLaunched (CLAUDE.md 7: BK ma pola
    /// statyczne z GameTexts). Klucz BkPerkBugFixes czytany przy kazdym wywolaniu (NIE = dokladnie jak BK). Bledy liczone, bez wylacznika.
    /// Linie: "Bledy perkow BK (504): start sesji ..." (stan 7 latek) i "Bledy perkow BK (504): dzien N ..." (tylko gdy cos zadzialalo).
    /// </summary>
    internal static class BkPerkFix504
    {
        internal const string P = "Bledy perkow BK (504): ";
        private const string Owner = "com.jeff.armoury.bkperkfix504";
        private const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        private static Harmony _h;
        private static bool _done;
        private static readonly string[] Names = { "FianRanger", "JawwalDuneRider", "TheologyMatrimony", "LordshipSellswordCareer", "ScholarshipTutor", "CavalryTactics", "WellStraped" };
        private static readonly string[] _st = { "nie sprawdzane", "nie sprawdzane", "nie sprawdzane", "nie sprawdzane", "nie sprawdzane", "nie sprawdzane", "nie sprawdzane" };
        private static int _stumbles, _dStumbles;

        // liczniki doby (Interlocked - predkosc partii bywa liczona poza watkiem glownym)
        private static int _dFian, _dDune, _dMatr, _dSell, _dTutorKids, _dTutorPts, _dCav, _dWell;
        private static string _lastTutor = "";

        // odbicie BK (bez twardej zaleznosci od BannerKings.dll)
        private static PropertyInfo _pCfg, _pEdu, _pPop;
        private static MethodInfo _mGetEdu, _mHasPerk, _mPopulated;
        private static Action<Hero> _origTutor;
        private static PerkObject _fian, _tutor;   // perki BK - odswiezane co sesje (obiekty kampanii)

        private static bool On { get { var s = Settings.Current; return s != null && s.BkPerkBugFixes; } }

        private static void Stumble(string where, Exception e)
        {
            int n = Interlocked.Increment(ref _stumbles);
            Interlocked.Increment(ref _dStumbles);
            try { if (n <= 3 || n % 100 == 0) Log.Error("BkPerkFix504." + where + " (potkniecie " + n + ")", e); } catch { }
        }

        // ------------------------------------------------------------ wzorce IL
        private static bool IsCallTo(CodeInstruction c, string name)
        {
            return c != null && (c.opcode == OpCodes.Call || c.opcode == OpCodes.Callvirt) && c.operand is MethodBase m && m.Name == name;
        }

        private static bool IsR4(CodeInstruction c, float v)
        {
            return c != null && c.opcode == OpCodes.Ldc_R4 && c.operand is float f && Math.Abs(f - v) < 1e-5f;
        }

        /// <summary>Pierwsze ldc.r4 (najwyzej 12 instrukcji) po pierwszym wywolaniu gettera perka: indeks, gdy stala == bad; -2 brak gettera,
        /// -3 stala == good (BK naprawil), -1 inny wzorzec.</summary>
        private static int FindConst(List<CodeInstruction> ins, string getter, float bad, float good)
        {
            for (int i = 0; i < ins.Count; i++)
            {
                if (!IsCallTo(ins[i], getter)) continue;
                for (int j = i + 1; j < ins.Count && j <= i + 12; j++)
                {
                    if (ins[j].opcode != OpCodes.Ldc_R4) continue;
                    if (IsR4(ins[j], bad)) return j;
                    if (IsR4(ins[j], good)) return -3;
                    return -1;
                }
                return -1;
            }
            return -2;
        }

        /// <summary>Argument 'primary' wywolania Helpers.ApplyPerk z getterem perka (najwyzej 10 instrukcji wstecz): indeks ldc.i4.1; -2 brak takiego
        /// wywolania, -3 ldc.i4.0 (BK naprawil), -1 inny wzorzec.</summary>
        private static int FindPrimary(List<CodeInstruction> ins, string getter)
        {
            for (int i = 1; i < ins.Count; i++)
            {
                if (!IsCallTo(ins[i], "ApplyPerk")) continue;
                bool perk = false;
                for (int j = i - 1; j >= 0 && j >= i - 10; j--) if (IsCallTo(ins[j], getter)) { perk = true; break; }
                if (!perk) continue;
                if (ins[i - 1].opcode == OpCodes.Ldc_I4_1) return i - 1;
                if (ins[i - 1].opcode == OpCodes.Ldc_I4_0) return -3;
                return -1;
            }
            return -2;
        }

        private static string Why(int r, string what)
        {
            return r == -3 ? "pominiete: BK juz naprawil (" + what + ")" : r == -2 ? "pominiete: brak wzorca w IL (" + what + " - BK zmienil kod?)" : "pominiete: inny wzorzec IL (" + what + ")";
        }

        private static bool Ours(MethodBase target)
        {
            try
            {
                var info = Harmony.GetPatchInfo(target);
                if (info == null) return false;
                foreach (var p in info.Prefixes) if (p.owner == Owner) return true;
                foreach (var p in info.Postfixes) if (p.owner == Owner) return true;
                foreach (var p in info.Transpilers) if (p.owner == Owner) return true;
            }
            catch { }
            return false;
        }

        private static string Wired(MethodBase target, string how)
        {
            return Ours(target) ? "wpieta (" + how + ")" : "BLAD: latki NIE widac w GetPatchInfo (" + how + ")";
        }

        // ------------------------------------------------------------ start
        /// <summary>OnSessionLaunched: latki raz na proces (kazda w osobnym try), perki BK odswiezane co sesje, zerowanie doby, linia startu.</summary>
        internal static void EnsureHooks()
        {
            ResetDay();
            try { _fian = MBObjectManager.Instance != null ? MBObjectManager.Instance.GetObject<PerkObject>("LifestyleFianRanger") : null; } catch { _fian = null; }
            try { _tutor = MBObjectManager.Instance != null ? MBObjectManager.Instance.GetObject<PerkObject>("ScholarshipTutor") : null; } catch { _tutor = null; }
            if (!_done)
            {
                _done = true;
                if (_h == null) _h = new Harmony(Owner);
                ResolveBk();
                Try(0, FixFian);
                Try(1, FixDune);
                Try(2, () => FixPrimary("BannerKings.Models.Vanilla.BKMarriageModel", "GetInfluenceCost", new[] { typeof(Hero), typeof(Hero), typeof(bool) }, "get_TheologyMatrimony", nameof(TrMatrimony)));
                Try(3, () => FixPrimary("BannerKings.Models.Vanilla.BKDiplomacyModel", "GetMercenaryDownPayment", new[] { typeof(Clan), typeof(Kingdom), typeof(bool) }, "get_LordshipSellswordCareer", nameof(TrSellsword)));
                Try(4, FixTutor);
                Try(5, FixCavalry);
                Try(6, FixWell);
            }
            var parts = new List<string>();
            for (int i = 0; i < Names.Length; i++) parts.Add(Names[i] + " " + _st[i]);
            string model = "?";
            try { var vm = Campaign.Current != null ? Campaign.Current.Models.VolunteerModel : null; model = vm != null ? vm.GetType().Name : "brak"; } catch { }
            string track = "?";
            try { var tm = Campaign.Current != null ? Campaign.Current.Models.MapTrackModel : null; track = tm != null ? tm.GetType().Name : "brak"; } catch { }
            Log.Info(P + "start sesji | klucz BkPerkBugFixes " + (On ? "TAK" : "NIE (wszystko jak BK)") + " | " + string.Join(" | ", parts)
                     + " | perki BK: Fian " + (_fian != null ? "TAK" : "BRAK") + ", Tutor " + (_tutor != null ? "TAK" : "BRAK")
                     + " | czynne modele: ochotnicy " + model + ", slad " + track + " | potkniecia " + _stumbles + ".");
        }

        private static void Try(int i, Func<string> fix)
        {
            try { _st[i] = fix(); }
            catch (Exception e) { _st[i] = "wyjatek " + e.GetType().Name; Stumble("Fix" + Names[i], e); }
        }

        private static void ResolveBk()
        {
            try
            {
                var cfg = AccessTools.TypeByName("BannerKings.BannerKingsConfig");
                if (cfg == null) return;
                _pCfg = AccessTools.Property(cfg, "Instance");
                _pEdu = AccessTools.Property(cfg, "EducationManager");
                _pPop = AccessTools.Property(cfg, "PopulationManager");
                var em = AccessTools.TypeByName("BannerKings.Managers.EducationManager");
                _mGetEdu = em != null ? AccessTools.Method(em, "GetHeroEducation", new[] { typeof(Hero) }) : null;
                var ed = AccessTools.TypeByName("BannerKings.Managers.Education.EducationData");
                _mHasPerk = ed != null ? AccessTools.Method(ed, "HasPerk", new[] { typeof(PerkObject) }) : null;
                var pm = AccessTools.TypeByName("BannerKings.Managers.PopulationManager");
                _mPopulated = pm != null ? AccessTools.Method(pm, "IsSettlementPopulated", new[] { typeof(Settlement) }) : null;
            }
            catch (Exception e) { Stumble("ResolveBk", e); }
        }

        // ------------------------------------------------------------ 1 FianRanger
        private static string FixFian()
        {
            var target = AccessTools.DeclaredMethod(typeof(DefaultMapTrackModel), "GetTrackLife", new[] { typeof(MobileParty) });
            if (target == null) return "pominiete: brak DefaultMapTrackModel.GetTrackLife";
            MethodInfo bk = null;
            var info = Harmony.GetPatchInfo(target);
            if (info != null)
                foreach (var p in info.Postfixes)
                    if (p.PatchMethod != null && p.PatchMethod.DeclaringType != null && p.PatchMethod.DeclaringType.Name == "BKMapTrackTweakPatches") { bk = p.PatchMethod; break; }
            if (bk == null) return "pominiete: brak postfiksu BK na DefaultMapTrackModel.GetTrackLife";
            // recenzja 504: bez perka nie zdejmujemy latki BK (inaczej Ranger przepada w obu stanach klucza)
            if (_fian == null) return "pominiete: brak perka LifestyleFianRanger w MBObjectManager (postfiks BK zostaje)";
            int r = FindConst(PatchProcessor.GetOriginalInstructions(bk), "get_FianRanger", 0.2f, 1.2f);
            if (r < 0) return Why(r, "x0.2 po get_FianRanger");
            if (_pCfg == null || _pEdu == null || _mGetEdu == null || _mHasPerk == null) return "pominiete: brak API BK (EducationManager.GetHeroEducation / EducationData.HasPerk)";
            _h.Unpatch(target, bk);
            try { _h.Patch(target, postfix: new HarmonyMethod(typeof(BkPerkFix504), nameof(TrackLifePostfix))); }
            catch (Exception e) { Stumble("FixFian(Patch)", e); }
            if (!Ours(target))
            {
                try { _h.Patch(target, postfix: new HarmonyMethod(bk)); } catch { }   // nie zostawiac gry bez perka - wraca latka BK
                return "BLAD: nasza latka nie weszla - postfiks BK przywrocony";
            }
            return "wpieta (postfiks BK zdjety z DefaultMapTrackModel.GetTrackLife; nasz: wodz partii gracza z perkiem -> kazdy slad x1.2)";
        }

        private static bool HasEdu(Hero hero, PerkObject perk)
        {
            var cfg = _pCfg.GetValue(null, null);
            var mgr = cfg != null ? _pEdu.GetValue(cfg, null) : null;
            var edu = mgr != null ? _mGetEdu.Invoke(mgr, new object[] { hero }) : null;
            return edu != null && (bool)_mHasPerk.Invoke(edu, new object[] { perk });
        }

        /// <summary>Zamiast BKMapTrackTweakPatches.GetTrackLifePostfix. Klucz TAK: wodz partii GRACZA ma Ranger -> kazdy tworzony slad zyje x1.2
        /// ("Increase maximum track life by 20%", wzor Scouting.Tracker w GetTrackLife gry; slady czyta tylko gracz). Klucz NIE = BK: slad partii,
        /// ktorej wodz ma perk, x0.2.</summary>
        public static void TrackLifePostfix(MobileParty __0, ref int __result)
        {
            try
            {
                if (__0 == null || _fian == null) return;
                if (!On)
                {
                    var own = __0.LeaderHero;
                    if (own != null && HasEdu(own, _fian)) __result = (int)((float)__result * 0.2f);
                    return;
                }
                var mp = MobileParty.MainParty;
                var hero = mp != null ? mp.LeaderHero : null;
                if (hero == null || __0.IsCurrentlyAtSea || !HasEdu(hero, _fian)) return;
                __result = (int)((float)__result * 1.2f);
                Interlocked.Increment(ref _dFian);
            }
            catch (Exception e) { Stumble("TrackLifePostfix", e); }
        }

        // ------------------------------------------------------------ 2 JawwalDuneRider
        private static string FixDune()
        {
            var t = AccessTools.TypeByName("BannerKings.Patches.VanillaModelTweakPatches+BKPartySpeedTweakPatches");
            var m = t != null ? AccessTools.DeclaredMethod(t, "CalculateFinalSpeedPostfix") : null;
            if (m == null) return "pominiete: brak BKPartySpeedTweakPatches.CalculateFinalSpeedPostfix";
            int r = FindConst(PatchProcessor.GetOriginalInstructions(m), "get_JawwalDuneRider", 0.8f, 0.08f);
            if (r < 0) return Why(r, "0.8 po get_JawwalDuneRider");
            _h.Patch(m, transpiler: new HarmonyMethod(typeof(BkPerkFix504), nameof(TrDune)));
            return Wired(m, "transpiler na postfiksie BK: 0.8 -> 0.08");
        }

        public static IEnumerable<CodeInstruction> TrDune(IEnumerable<CodeInstruction> instructions)
        {
            var ins = new List<CodeInstruction>(instructions);
            int k = FindConst(ins, "get_JawwalDuneRider", 0.8f, 0.08f);
            if (k >= 0) { ins[k].opcode = OpCodes.Call; ins[k].operand = AccessTools.Method(typeof(BkPerkFix504), nameof(DuneFactor)); }
            return ins;
        }

        /// <summary>Wolane z postfiksu BK tylko na pustyni u wodza z perkiem: czynnik predkosci 0.08 (klucz NIE = 0.8 jak BK).</summary>
        public static float DuneFactor()
        {
            Interlocked.Increment(ref _dDune);
            return On ? 0.08f : 0.8f;
        }

        // ------------------------------------------------------------ 3, 4 Matrimony, SellswordCareer (primary -> secondary)
        private static string FixPrimary(string type, string name, Type[] args, string getter, string tr)
        {
            var t = AccessTools.TypeByName(type);
            var m = t != null ? AccessTools.DeclaredMethod(t, name, args) : null;
            if (m == null) return "pominiete: brak " + type + "." + name;
            var orig = PatchProcessor.GetOriginalInstructions(m);
            int r = FindPrimary(orig, getter);
            if (r < 0) return Why(r, "ApplyPerk(primary: true) z " + getter);
            var ap = orig[r + 1].operand as MethodInfo;
            if (ap == null || !ap.IsStatic) return "pominiete: Helpers.ApplyPerk nie jest metoda statyczna";
            if (_applyPerk == null) _applyPerk = AccessTools.MethodDelegate<ApplyPerkFn>(ap);   // inna sygnatura = wyjatek w Try, bez latki
            _h.Patch(m, transpiler: new HarmonyMethod(typeof(BkPerkFix504), tr));
            return Wired(m, "transpiler " + t.Name + "." + name + ": ApplyPerk primary -> druga polowa perka");
        }

        /// <summary>Sygnatura BK Helpers.ApplyPerk(PerkObject, Hero, ref ExplainedNumber, bool primary).</summary>
        private delegate void ApplyPerkFn(PerkObject perk, Hero hero, ref ExplainedNumber result, bool primary);
        private static ApplyPerkFn _applyPerk;

        // ldc.i4.1 (argument primary BK) zostaje; podmieniamy tylko cel call Helpers.ApplyPerk na nasza otoczke o tej samej sygnaturze
        private static IEnumerable<CodeInstruction> SwapPrimary(IEnumerable<CodeInstruction> instructions, string getter, string helper)
        {
            var ins = new List<CodeInstruction>(instructions);
            int k = FindPrimary(ins, getter);
            if (k >= 0 && _applyPerk != null) ins[k + 1].operand = AccessTools.Method(typeof(BkPerkFix504), helper);
            return ins;
        }

        public static IEnumerable<CodeInstruction> TrMatrimony(IEnumerable<CodeInstruction> instructions)
        {
            return SwapPrimary(instructions, "get_TheologyMatrimony", nameof(MatrimonyApply));
        }

        public static IEnumerable<CodeInstruction> TrSellsword(IEnumerable<CodeInstruction> instructions)
        {
            return SwapPrimary(instructions, "get_LordshipSellswordCareer", nameof(SellswordApply));
        }

        /// <summary>Zamiast Helpers.ApplyPerk w BKMarriageModel.GetInfluenceCost: primary false = -15% kosztu wplywu (klucz NIE = primary BK, +5%).
        /// Licznik doby tylko, gdy bohater perk ma (recenzja 504: dowod dzialania, nie liczba wywolan). Wyjatki BK leca dalej jak dotad.</summary>
        public static void MatrimonyApply(PerkObject perk, Hero hero, ref ExplainedNumber result, bool primary)
        {
            bool on = On;
            if (on && hero != null && perk != null && hero.GetPerkValue(perk)) Interlocked.Increment(ref _dMatr);
            _applyPerk(perk, hero, ref result, on ? false : primary);
        }

        /// <summary>Zamiast Helpers.ApplyPerk w BKDiplomacyModel.GetMercenaryDownPayment: primary false = +10% zaliczki (klucz NIE = primary BK, +15%).</summary>
        public static void SellswordApply(PerkObject perk, Hero hero, ref ExplainedNumber result, bool primary)
        {
            bool on = On;
            if (on && hero != null && perk != null && hero.GetPerkValue(perk)) Interlocked.Increment(ref _dSell);
            _applyPerk(perk, hero, ref result, on ? false : primary);
        }

        // ------------------------------------------------------------ 5 ScholarshipTutor
        private static string FixTutor()
        {
            var t = AccessTools.TypeByName("BannerKings.Behaviours.BKEducationBehavior");
            var apply = t != null ? AccessTools.DeclaredMethod(t, "ApplyScholarshipTutorEffect", new[] { typeof(Hero) }) : null;
            var caller = t != null ? AccessTools.DeclaredMethod(t, "OnHeroComesOfAge", new[] { typeof(Hero) }) : null;
            if (apply == null || caller == null) return "pominiete: brak BKEducationBehavior.ApplyScholarshipTutorEffect / OnHeroComesOfAge";
            // blad = lambda w klasie domkniecia (DisplayClass) z parametrem Hero, ktora go nie czyta (ldarg.1) - patrzy na przechwycone dziecko
            Type dc = null;
            foreach (var ci in PatchProcessor.GetOriginalInstructions(apply))
                if (ci.opcode == OpCodes.Newobj && ci.operand is ConstructorInfo c && c.DeclaringType != null && c.DeclaringType.Name.Contains("DisplayClass")) { dc = c.DeclaringType; break; }
            if (dc == null) return "pominiete: BK juz naprawil albo zmienil kod (lambda bez domkniecia dziecka)";
            bool lambda = false, usesParam = false;
            foreach (var lm in dc.GetMethods(All))
            {
                var ps = lm.GetParameters();
                if (ps.Length != 1 || ps[0].ParameterType != typeof(Hero) || lm.ReturnType != typeof(bool)) continue;
                lambda = true;
                foreach (var ci in PatchProcessor.GetOriginalInstructions(lm))
                    if (ci.opcode == OpCodes.Ldarg_1 || ((ci.opcode == OpCodes.Ldarg_S || ci.opcode == OpCodes.Ldarg) && ci.operand is ParameterInfo pi && pi.Position == 0)) usesParam = true;
            }
            if (!lambda) return "pominiete: brak wzorca w IL (lambda Hero -> bool)";
            if (usesParam) return "pominiete: BK juz naprawil (lambda czyta swoj parametr)";
            int calls = 0;
            foreach (var ci in PatchProcessor.GetOriginalInstructions(caller)) if (ci.opcode == OpCodes.Call && ci.operand is MethodInfo mi && mi == apply) calls++;
            if (calls != 1) return "pominiete: OnHeroComesOfAge wola ApplyScholarshipTutorEffect " + calls + "x (oczekiwane 1)";
            if (_tutor == null) return "pominiete: brak perka ScholarshipTutor w MBObjectManager";
            _origTutor = AccessTools.MethodDelegate<Action<Hero>>(apply);
            _h.Patch(caller, transpiler: new HarmonyMethod(typeof(BkPerkFix504), nameof(TrTutor)));
            return Wired(caller, "transpiler OnHeroComesOfAge: Tutor liczy nauczycieli rodu");
        }

        public static IEnumerable<CodeInstruction> TrTutor(IEnumerable<CodeInstruction> instructions)
        {
            var ins = new List<CodeInstruction>(instructions);
            for (int i = 0; i < ins.Count; i++)
                if (ins[i].opcode == OpCodes.Call && ins[i].operand is MethodInfo mi && mi.Name == "ApplyScholarshipTutorEffect")
                    ins[i].operand = AccessTools.Method(typeof(BkPerkFix504), nameof(TutorEffect));
            return ins;
        }

        /// <summary>Zamiast BK ApplyScholarshipTutorEffect: dorastajace dziecko rodu dostaje +1 punkt cechy za kazdego zywego czlonka rodu
        /// (poza nim samym) z perkiem Tutor. Klucz NIE = oryginal BK.</summary>
        public static void TutorEffect(Hero hero)
        {
            if (!On || _tutor == null)
            {
                if (_origTutor != null) _origTutor(hero);
                return;
            }
            try
            {
                if (hero == null || hero.Clan == null || hero.HeroDeveloper == null) return;
                // 504-a (Jeff 14.11 "tutor tylko glowa rodu"): rola perka w BK = ClanLeader - liczy sie tylko zywa glowa rodu z perkiem (+1 punkt cechy)
                int n = 0;
                string who = "";
                var h = hero.Clan.Leader;
                if (h != null && h != hero && h.IsAlive && h.GetPerkValue(_tutor)) { n = 1; who = h.Name.ToString(); }
                Interlocked.Increment(ref _dTutorKids);
                if (n <= 0) return;
                hero.HeroDeveloper.UnspentAttributePoints += n;
                Interlocked.Add(ref _dTutorPts, n);
                _lastTutor = hero.Name + " +" + n + " (" + who + ")";
            }
            catch (Exception e) { Stumble("TutorEffect", e); }
        }

        // ------------------------------------------------------------ 6 CavalryTactics
        private static bool CallsGetter(MethodBase m, string getter)
        {
            if (m == null) return false;
            foreach (var ci in PatchProcessor.GetOriginalInstructions(m)) if (IsCallTo(ci, getter)) return true;
            return false;
        }

        private static string FixCavalry()
        {
            var t = AccessTools.TypeByName("BannerKings.Models.Vanilla.BKVolunteerModel");
            var m = t != null ? AccessTools.DeclaredMethod(t, "GetDailyVolunteerProductionProbability", new[] { typeof(Hero), typeof(int), typeof(Settlement) }) : null;
            if (m == null) return "pominiete: brak BKVolunteerModel.GetDailyVolunteerProductionProbability";
            var draft = AccessTools.DeclaredMethod(t, "GetDraftEfficiency", new[] { typeof(Hero), typeof(Settlement) });
            if (CallsGetter(m, "get_CavalryTactics") || CallsGetter(draft, "get_CavalryTactics")) return "pominiete: BK juz naprawil (CavalryTactics w modelu BK)";
            _h.Patch(m, postfix: new HarmonyMethod(typeof(BkPerkFix504), nameof(CavalryPostfix)));
            return Wired(m, "postfiks BKVolunteerModel.GetDailyVolunteerProductionProbability: konny ochotnik x1.3");
        }

        /// <summary>Postfiks BKVolunteerModel.GetDailyVolunteerProductionProbability: warunki gry (DefaultVolunteerModel:104-107) -> x(1 + PrimaryBonus).</summary>
        public static void CavalryPostfix(Hero __0, int __1, Settlement __2, ref float __result)
        {
            try
            {
                if (!On || __0 == null || __2 == null || __result <= 0f || !__0.IsAlive) return;
                var vt = __0.VolunteerTypes;
                if (vt == null || __1 < 0 || __1 >= vt.Length || vt[__1] == null || !vt[__1].IsMounted) return;
                Town town = __2.IsTown ? __2.Town : (__2.IsVillage && __2.Village != null && __2.Village.TradeBound != null ? __2.Village.TradeBound.Town : null);
                var perk = DefaultPerks.Riding.CavalryTactics;
                if (town == null || perk == null || !Helpers.PerkHelper.GetPerkValueForTown(perk, town)) return;
                __result *= 1f + perk.PrimaryBonus;
                Interlocked.Increment(ref _dCav);
            }
            catch (Exception e) { Stumble("CavalryPostfix", e); }
        }

        // ------------------------------------------------------------ 7 WellStraped
        private static string FixWell()
        {
            var t = AccessTools.TypeByName("BannerKings.Models.Vanilla.BKLoyaltyModel");
            var m = t != null ? AccessTools.DeclaredMethod(t, "CalculateLoyaltyChange", new[] { typeof(Town), typeof(bool) }) : null;
            if (m == null) return "pominiete: brak BKLoyaltyModel.CalculateLoyaltyChange";
            if (CallsGetter(m, "get_WellStraped") || CallsGetter(AccessTools.DeclaredMethod(t, "CalculateLoyaltyChangeInternal"), "get_WellStraped")
                || CallsGetter(AccessTools.DeclaredMethod(t, "GetSettlementLoyaltyChangeDueToGovernorPerks"), "get_WellStraped"))
                return "pominiete: BK juz naprawil (WellStraped w modelu BK)";
            if (_pCfg == null || _pPop == null || _mPopulated == null) return "pominiete: brak API BK (PopulationManager.IsSettlementPopulated)";
            _h.Patch(m, postfix: new HarmonyMethod(typeof(BkPerkFix504), nameof(WellPostfix)));
            return Wired(m, "postfiks BKLoyaltyModel.CalculateLoyaltyChange: gubernator +0.5 w osadach z populacja BK");
        }

        private static bool Populated(Settlement s)
        {
            var cfg = _pCfg.GetValue(null, null);
            var pm = cfg != null ? _pPop.GetValue(cfg, null) : null;
            return pm != null && (bool)_mPopulated.Invoke(pm, new object[] { s });
        }

        /// <summary>Postfiks BKLoyaltyModel.CalculateLoyaltyChange: tylko sciezka populacji BK (inaczej BK oddal model gry, ktory perk liczy).</summary>
        public static void WellPostfix(Town __0, ref ExplainedNumber __result)
        {
            try
            {
                if (!On || __0 == null || __0.Settlement == null) return;
                var perk = DefaultPerks.Riding.WellStraped;
                var gov = __0.Governor;
                if (perk == null || gov == null || !gov.GetPerkValue(perk)) return;   // tani filtr przed odbiciem BK
                if (!Populated(__0.Settlement)) return;
                float before = __result.ResultNumber;
                Helpers.PerkHelper.AddPerkBonusForTown(perk, __0, ref __result);
                if (__result.ResultNumber != before) Interlocked.Increment(ref _dWell);
            }
            catch (Exception e) { Stumble("WellPostfix", e); }
        }

        // ------------------------------------------------------------ doba
        private static void ResetDay()
        {
            _dFian = 0; _dDune = 0; _dMatr = 0; _dSell = 0; _dTutorKids = 0; _dTutorPts = 0; _dCav = 0; _dWell = 0; _dStumbles = 0; _lastTutor = "";
        }

        /// <summary>ArmouryBehavior.OnDailyTickBodyInner: linia doby (tylko gdy ktoras latka zadzialala albo bylo potkniecie) i zerowanie.</summary>
        internal static void Daily(int day)
        {
            try
            {
                // recenzja 504: linia tylko przy realnym dzialaniu perka (dorastajacy bez nauczyciela w rodzie sie nie liczy)
                if (_dFian + _dDune + _dMatr + _dSell + _dTutorPts + _dCav + _dWell + _dStumbles > 0)
                    Log.Info(P + "dzien " + day + " | klucz " + (On ? "TAK" : "NIE") + " | slady x1.2 (Ranger gracza): " + _dFian + " | Dune Rider na pustyni (+8%): " + _dDune
                             + " | koszt slubu z perkiem (-15%): " + _dMatr + " | zaliczka najemnikow z perkiem (+10%): " + _dSell
                             + " | Tutor: dorastajacych " + _dTutorKids + ", punktow cech " + _dTutorPts + (_lastTutor.Length > 0 ? " (ostatnio " + _lastTutor + ")" : "")
                             + " | konny ochotnik (+30%): " + _dCav + " | Well Strapped (+0.5 lojalnosci): " + _dWell
                             + " | potkniecia doby " + _dStumbles + " (razem " + _stumbles + ").");
            }
            catch (Exception e) { Stumble("Daily", e); }
            ResetDay();
        }
    }
}
