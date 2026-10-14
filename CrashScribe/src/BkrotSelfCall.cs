using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace CrashScribe
{
    /// <summary>
    /// P2 AUDYTU MODOW 14.10 (docs/audyt-modow-2026-10-14/RAPORT-AUDYT-MODOW.md rozdz. 3: AM-10, AM-11, AM-12, AM-15, AM-18; rozdz. 5 pakiet P2) -
    /// SAMOWYWOLANIA BKROT. Ten sam blad, co przy lennach (CalculateHeroFiefScore - naprawiony osobno przez Armoury FiefGrants, tu NIE ruszany):
    /// metoda modelu BKROTPatch wola wirtualnie SAMA SIEBIE (callvirt na ldarg.0 zamiast base), straznik GlobalRecursionGuard przy drugim wejsciu
    /// zwraca 0, wiec model BK pod spodem nie biegnie nigdy, a dodatki BKROT (blogoslawienstwa ROT) naklejane sa na zero.
    ///
    /// Sprawdzone w IL zainstalowanego BKROTPatch.dll (26.07; ilspycmd -il, te same linie co w raporcie): dokladnie 8 takich wywolan, kazde
    /// "ldarg.0, ldarg.1 .. ldarg.n, callvirt Abstrakt::Metoda" z tymi samymi argumentami, w bloku try po GlobalRecursionGuard.TryEnter:
    ///  - BKROTLegitimacyModel.CalculateKingdomLegitimacy  -> LegitimacyModel::  (AM-10: cel prawowitosci kazdego krolestwa 0)
    ///  - BKROTInfluenceModel.CalculateInfluenceCap         -> InfluenceModel::   (AM-11: limit wplywu 0 w rachunkach BK)
    ///  - BKROTInfluenceModel.CalculateInfluenceChange      -> ClanPoliticsModel:: (AM-11: zmiana wplywu 0 w rachunkach BK - koszt tytulow)
    ///  - BKROTArmyModel.CalculatePartyInfluenceCost        -> ArmyManagementCalculationModel:: (AM-12: Call Banners / laczenie armii za 0)
    ///  - BKROTCultureModel.CalculateAcceptanceGain         -> CultureModel::     (AM-15: akceptacja kultur stoi)
    ///  - BKROTCultureModel.CalculateCultureWeight          -> CultureModel::     (AM-15: wagi kultur 0, 0/0 -> NaN -> 1.0 latka BKROT)
    ///  - BKROTMarriageModel.GetSpouseScore                 -> MarriageModel::    (AM-18: ocena malzonka 0 w kontrakcie BK gracza)
    ///  - BKROTClanFinanceModel.CalculateOwnerIncomeFromCaravan -> ClanFinanceModel:: (dochod z karawan 0 w rachunkach BK; u Jeffa
    ///    RealisticCaravanIncome = true, wiec BK i tak daje 0 - naprawa bez skutku przy tym ustawieniu)
    /// Pozostale modele BKROT ze straznikiem (Stability, Prosperity, Relations, Workshop, Death, Disguise, Persuasion) przy drugim wejsciu oddaja
    /// base.X - dzialaja; Construction oddaje 0, ale gra wola model gry (raport AM-18: bez skutku); Loyalty nie jest podpiety. Poza zakresem P2.
    ///
    /// NAPRAWA: transpiler na kazdej z metod BKROT podmienia JEDYNE samowywolanie "callvirt Abstrakt::M" na niewirtualne "call KlasaBazowaBK::M"
    /// (bezposrednia klasa bazowa typu BKROT - BKLegitimacyModel, BKInfluenceModel, BKArmyManagementModel, BKCultureModel, BKMarriageModel,
    /// BKClanFinanceModel - kazda deklaruje te metode sama; jak "base.M(...)"). Reszta kodu BKROT bez zmian: blogoslawienstwa ROT doklejane sa do
    /// prawdziwego wyniku BK. Kotwica: w oryginalnym IL dokladnie 1 trafienie (callvirt metody o tej nazwie i tych typach parametrow, poprzedzone
    /// ldarg.0..ldarg.n); inna liczba = metoda NIE latana, "BRAK" w linii startu (zachowanie jak dotad). Transpiler, nie prefiks: zostaje kod BKROT
    /// (blogoslawienstwa, pomiar BkrotPerf, straznik), a klucze straznika sa rozne dla Cap i Change, wiec zagniezdzony Cap w Change nie wraca do 0.
    ///
    /// Wpinane raz na proces przy pierwszym OnSessionLaunched (typy BK / BKROT pewnie zaladowane; BKROTClanFinanceModel dziedziczy po
    /// DefaultClanFinanceModel - CLAUDE.md rozdz. 7: latki tej rodziny tylko w kampanii). Do tej chwili w nowej kampanii nie biegnie zadna doba,
    /// a stan BK liczony z tych metod jest dobowy (prawowitosc idzie do celu 0.01 na dobe, asymilacja liczona od nowa co dobe), wiec pierwsza doba
    /// liczy juz dobrze. Klucze (CrashScribe.settings.xml, czytane raz przy starcie gry): BkrotFixLegitimacy, BkrotFixInfluence (obie metody),
    /// BkrotFixArmyCost, BkrotFixCulture (obie metody), BkrotFixSpouseScore, BkrotFixCaravanIncome - false = metoda nielatana (stan b47fecc3).
    /// Linia startu sesji "BKROT self call fix: n/8 metod ..." (stan kazdej metody, GetPatchInfo).
    /// </summary>
    internal static class BkrotSelfCall
    {
        private const string HarmonyId = "com.jeff.crashscribe";
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private sealed class Fix
        {
            public string Key;          // klucz w CrashScribe.settings.xml
            public string Label;        // nazwa do linii
            public string BkrotType;    // typ BKROT (pelna nazwa)
            public string BkType;       // bezposrednia klasa bazowa (model BK)
            public string Name;         // metoda
            public int Argc;            // liczba parametrow (bez this)
            public bool On;             // klucz
            public MethodInfo Target;   // metoda BKROT (latana)
            public MethodInfo Base;     // metoda modelu BK (cel niewirtualnego wywolania)
            public int Pre = -1;        // samowywolania w oryginalnym IL
            public int Hits = -1;       // trafienia transpilera przy wpieciu
            public bool Patched;        // transpiler stoi (GetPatchInfo)
            public string State = "nie wpinane";
        }

        private static readonly Fix[] _fixes =
        {
            new Fix { Key = "BkrotFixLegitimacy", Label = "Legitimacy.CalculateKingdomLegitimacy", BkrotType = "BKROTPatch.Models.BKROTLegitimacyModel",
                      BkType = "BannerKings.Models.BKModels.BKLegitimacyModel", Name = "CalculateKingdomLegitimacy", Argc = 2 },
            new Fix { Key = "BkrotFixInfluence", Label = "Influence.CalculateInfluenceCap", BkrotType = "BKROTPatch.Models.BKROTInfluenceModel",
                      BkType = "BannerKings.Models.Vanilla.BKInfluenceModel", Name = "CalculateInfluenceCap", Argc = 2 },
            new Fix { Key = "BkrotFixInfluence", Label = "Influence.CalculateInfluenceChange", BkrotType = "BKROTPatch.Models.BKROTInfluenceModel",
                      BkType = "BannerKings.Models.Vanilla.BKInfluenceModel", Name = "CalculateInfluenceChange", Argc = 2 },
            new Fix { Key = "BkrotFixArmyCost", Label = "Army.CalculatePartyInfluenceCost", BkrotType = "BKROTPatch.Models.BKROTArmyModel",
                      BkType = "BannerKings.Models.Vanilla.BKArmyManagementModel", Name = "CalculatePartyInfluenceCost", Argc = 2 },
            new Fix { Key = "BkrotFixSpouseScore", Label = "Marriage.GetSpouseScore", BkrotType = "BKROTPatch.Models.BKROTMarriageModel",
                      BkType = "BannerKings.Models.Vanilla.BKMarriageModel", Name = "GetSpouseScore", Argc = 2 },
            new Fix { Key = "BkrotFixCaravanIncome", Label = "ClanFinance.CalculateOwnerIncomeFromCaravan", BkrotType = "BKROTPatch.Models.BKROTClanFinanceModel",
                      BkType = "BannerKings.Models.Vanilla.BKClanFinanceModel", Name = "CalculateOwnerIncomeFromCaravan", Argc = 1 },
        };

        private static Harmony _harmony;
        private static bool _hooksTried;
        private static int _sStumbles;

        private static bool KeyOn(string key)
        {
            switch (key)
            {
                case "BkrotFixLegitimacy": return Config.BkrotFixLegitimacy;
                case "BkrotFixInfluence": return Config.BkrotFixInfluence;
                case "BkrotFixArmyCost": return Config.BkrotFixArmyCost;
                case "BkrotFixSpouseScore": return Config.BkrotFixSpouseScore;
                case "BkrotFixCaravanIncome": return Config.BkrotFixCaravanIncome;
            }
            return false;
        }

        private static void Stumble(string where, Exception e)
        {
            int n = Interlocked.Increment(ref _sStumbles);
            if (n <= 5 || n % 100 == 0) { try { Scribe.Report("CrashScribe", e, "BkrotSelfCall." + where + " (potkniecie " + n + ")", null); } catch { } }
        }

        // ------------------------------------------------------------ wpiecie (raz na proces)

        /// <summary>Start sesji (OnSessionLaunched): wpiecie raz na proces, potem linia stanu przy kazdej sesji. Nie rzuca.</summary>
        internal static void OnSession()
        {
            try { EnsureHooks(); } catch (Exception e) { Stumble("EnsureHooks", e); }
            try { Scribe.Line(StartLine()); } catch (Exception e) { Stumble("StartLine", e); }
        }

        private static void EnsureHooks()
        {
            if (_hooksTried) return;
            _hooksTried = true;
            if (_harmony == null) _harmony = new Harmony(HarmonyId);
            var tr = AccessTools.Method(typeof(BkrotSelfCall), nameof(SelfCallTranspiler));
            foreach (var f in _fixes)
            {
                try { Hook(f, tr); }
                catch (Exception e) { f.State = "wyjatek " + e.GetType().Name + " - NIE wpiete"; Stumble("Hook(" + f.Label + ")", e); }
            }
        }

        private static void Hook(Fix f, MethodInfo tr)
        {
            f.On = KeyOn(f.Key);
            var t = AccessTools.TypeByName(f.BkrotType);
            if (t == null) { f.State = "brak typu " + f.BkrotType + " (BKROTPatch nieobecny?) - nic do naprawy"; return; }
            var decl = t.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly).Where(m => m.Name == f.Name).ToList();
            if (decl.Count != 1) { f.State = "BRAK - metod " + f.Name + " w typie BKROT " + decl.Count + " (oczekiwana 1) - inna wersja BKROT"; return; }
            f.Target = decl[0];
            var ps = f.Target.GetParameters().Select(p => p.ParameterType).ToArray();
            if (ps.Length != f.Argc) { f.State = "BRAK - parametrow " + ps.Length + " (oczekiwane " + f.Argc + ") - inna wersja BKROT"; return; }
            var bt = t.BaseType;
            if (bt == null || bt.FullName != f.BkType) { f.State = "BRAK - klasa bazowa " + (bt != null ? bt.FullName : "-") + ", znana " + f.BkType; return; }
            var b = bt.GetMethod(f.Name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly, null, ps, null);
            if (b == null || b.IsAbstract || b.ReturnType != f.Target.ReturnType) { f.State = "BRAK - " + f.BkType + " nie deklaruje " + f.Name + " o tej sygnaturze"; return; }
            f.Base = b;
            f.Pre = Matches(PatchProcessor.GetOriginalInstructions(f.Target), f).Count;
            if (f.Pre != 1) { f.State = "BRAK - samowywolan w IL " + f.Pre + " (oczekiwane 1) - inna wersja BKROT, nic nie zmienione"; return; }
            if (!f.On) { f.State = "wyl. (" + f.Key + "=false) - samowywolanie BKROT jak dotad (wynik 0 w rachunkach BK)"; return; }
            f.Hits = -1;
            _harmony.Patch(f.Target, transpiler: new HarmonyMethod(tr));
            if (f.Hits != 1)
            {
                try { _harmony.Unpatch(f.Target, tr); } catch (Exception e) { Stumble("Unpatch(" + f.Label + ")", e); }
                f.State = "BRAK - transpiler trafil " + f.Hits + " (oczekiwane 1) - zdjety, jak dotad";
                return;
            }
            f.Patched = HasOur(f.Target, nameof(SelfCallTranspiler), true);
            f.State = f.Patched ? "TAK (1/1 -> call " + bt.Name + ")" : "BRAK - transpiler niewidoczny w GetPatchInfo";
        }

        /// <summary>Czy na metodzie stoi nasza latka (wlasciciel CrashScribe, ta metoda latki).</summary>
        private static bool HasOur(MethodBase m, string patchName, bool transpiler)
        {
            var info = Harmony.GetPatchInfo(m);
            if (info == null) return false;
            var list = transpiler ? info.Transpilers : info.Prefixes;
            return list != null && list.Any(p => p != null && p.owner == HarmonyId && p.PatchMethod != null && p.PatchMethod.Name == patchName);
        }

        private static Fix FixOf(MethodBase original)
        {
            if (original == null) return null;
            foreach (var f in _fixes)
                if (f.Target != null && f.Target.Name == original.Name && f.Target.DeclaringType == original.DeclaringType) return f;
            return null;
        }

        private static bool SameParams(MethodBase a, MethodBase b)
        {
            var pa = a.GetParameters(); var pb = b.GetParameters();
            if (pa.Length != pb.Length) return false;
            for (int i = 0; i < pa.Length; i++) if (pa[i].ParameterType != pb[i].ParameterType) return false;
            return true;
        }

        /// <summary>Indeksy samowywolan: callvirt metody o tej nazwie i tych parametrach, zadeklarowanej w typie, z ktorego dziedziczy BKROT,
        /// poprzedzone ldarg.0 .. ldarg.n (te same argumenty w tej samej kolejnosci).</summary>
        private static List<int> Matches(List<CodeInstruction> list, Fix f)
        {
            var res = new List<int>();
            if (list == null || f.Target == null) return res;
            for (int i = 0; i < list.Count; i++)
            {
                var ci = list[i];
                if (ci.opcode != OpCodes.Callvirt) continue;
                var mi = ci.operand as MethodInfo;
                if (mi == null || mi.Name != f.Name || mi.DeclaringType == null || !mi.DeclaringType.IsAssignableFrom(f.Target.DeclaringType) || !SameParams(mi, f.Target)) continue;
                if (i < f.Argc + 1) continue;
                bool ok = true;
                for (int k = 0; k <= f.Argc && ok; k++) ok = list[i - f.Argc - 1 + k].IsLdarg(k);
                if (ok) res.Add(i);
            }
            return res;
        }

        /// <summary>Transpiler (wspolny dla 8 metod): jedyne samowywolanie -> niewirtualne wywolanie modelu BK. Inna liczba trafien - IL bez zmian
        /// (EnsureHooks zdejmie wtedy latke). Harmony wola go od nowa przy kazdym przelataniu metody - zawsze na oryginalnym IL.</summary>
        internal static IEnumerable<CodeInstruction> SelfCallTranspiler(IEnumerable<CodeInstruction> instructions, MethodBase original)
        {
            var list = new List<CodeInstruction>(instructions);
            var f = FixOf(original);
            if (f == null || f.Base == null) return list;
            var idx = Matches(list, f);
            f.Hits = idx.Count;
            if (idx.Count == 1)
            {
                var ci = list[idx[0]];
                ci.opcode = OpCodes.Call;     // etykiety i bloki wyjatkow zostaja na tej samej instrukcji
                ci.operand = f.Base;
            }
            return list;
        }

        // ------------------------------------------------------------ linia startu sesji

        private static string StartLine()
        {
            int on = 0, ok = 0, miss = 0;
            var sb = new StringBuilder();
            foreach (var f in _fixes)
            {
                if (f.On) on++;
                if (f.Patched) ok++;
                else if (f.On && f.State.StartsWith("BRAK", StringComparison.Ordinal)) miss++;
                sb.Append(sb.Length > 0 ? "; " : "").Append(f.Label).Append(' ').Append(f.State);
            }
            return "BKROT self call fix: " + ok + "/" + _fixes.Length + " metod (kluczem wlaczonych " + on + ", BRAK " + miss + ") | " + sb
                   + " | wylaczona metoda = samowywolanie BKROT jak dotad (wynik 0 w rachunkach BK); pliki BK i BKROT nietkniete.";
        }
    }

    /// <summary>P2 (audyt 14.10): linia startu sesji samowywolan BKROT.</summary>
    internal sealed class BkrotSelfCallBehavior : CampaignBehaviorBase
    {
        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, delegate (CampaignGameStarter s) { BkrotSelfCall.OnSession(); });
        }

        public override void SyncData(IDataStore dataStore) { }
    }
}
