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
    /// AM-15 drugi klucz BkrotCultureBlessingsOwnerOnly (tylko przy naprawionej kulturze): BKROT dokleja blogoslawienstwa pana (Trios, GodsBlood,
    /// Harpy: wagi +25 / +25 / +50, akceptacja +0.04 / +0.04 / +0.08) do KAZDEJ kultury osady, co rozmywa premie kultury pana; z kluczem prefiks
    /// oddaje dla kultury innej niz kultura pana sam wynik modelu BK (to samo, co BKROT liczy przed doklejeniem), a dla kultury pana kod BKROT
    /// biegnie bez zmian. Blogoslawienstwa ma w praktyce tylko gracz (AddBlessing w rozmowie z kaplanem - raport AM-14).
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
            new Fix { Key = "BkrotFixCulture", Label = "Culture.CalculateAcceptanceGain", BkrotType = "BKROTPatch.Models.BKROTCultureModel",
                      BkType = "BannerKings.Models.BKModels.BKCultureModel", Name = "CalculateAcceptanceGain", Argc = 1 },
            new Fix { Key = "BkrotFixCulture", Label = "Culture.CalculateCultureWeight", BkrotType = "BKROTPatch.Models.BKROTCultureModel",
                      BkType = "BannerKings.Models.BKModels.BKCultureModel", Name = "CalculateCultureWeight", Argc = 3 },
            new Fix { Key = "BkrotFixSpouseScore", Label = "Marriage.GetSpouseScore", BkrotType = "BKROTPatch.Models.BKROTMarriageModel",
                      BkType = "BannerKings.Models.Vanilla.BKMarriageModel", Name = "GetSpouseScore", Argc = 2 },
            new Fix { Key = "BkrotFixCaravanIncome", Label = "ClanFinance.CalculateOwnerIncomeFromCaravan", BkrotType = "BKROTPatch.Models.BKROTClanFinanceModel",
                      BkType = "BannerKings.Models.Vanilla.BKClanFinanceModel", Name = "CalculateOwnerIncomeFromCaravan", Argc = 1 },
        };

        private static Harmony _harmony;
        private static bool _hooksTried;
        private static int _sStumbles;

        // AM-15 klucz 2 (BkrotCultureBlessingsOwnerOnly): blogoslawienstwa ROT tylko dla kultury pana osady
        private static string _ownerOnlyState = "nie wpinane";
        private static bool _ownerOnly;
        private static PropertyInfo _pCdcSettlement, _pCdcCulture;                          // CultureDataClass.Settlement / .Culture
        private static Func<object, object, ExplainedNumber> _acceptBase;                   // BKCultureModel.CalculateAcceptanceGain niewirtualnie
        private static Func<object, Settlement, object, float, ExplainedNumber> _weightBase; // BKCultureModel.CalculateCultureWeight niewirtualnie
        private static int _dOwnerOnly, _sOwnerOnly;                                        // wyniki BK bez blogoslawienstw (kultura inna niz pana)

        private static bool KeyOn(string key)
        {
            switch (key)
            {
                case "BkrotFixLegitimacy": return Config.BkrotFixLegitimacy;
                case "BkrotFixInfluence": return Config.BkrotFixInfluence;
                case "BkrotFixArmyCost": return Config.BkrotFixArmyCost;
                case "BkrotFixCulture": return Config.BkrotFixCulture;
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
            try { HookOwnerOnly(); }
            catch (Exception e) { _ownerOnlyState = "wyjatek " + e.GetType().Name + " - NIE wpiete"; Stumble("HookOwnerOnly", e); }
        }

        private static Fix FixByName(string name)
        {
            foreach (var f in _fixes) if (f.Name == name) return f;
            return null;
        }

        /// <summary>AM-15 klucz 2: prefiksy "blogoslawienstwa tylko dla kultury pana" - tylko gdy obie metody kultury sa naprawione (bez naprawy
        /// wynik BK i tak jest 0, wiec reguly nie ma do czego stosowac).</summary>
        private static void HookOwnerOnly()
        {
            var fa = FixByName("CalculateAcceptanceGain");
            var fw = FixByName("CalculateCultureWeight");
            if (!Config.BkrotCultureBlessingsOwnerOnly) { _ownerOnlyState = "wyl. (BkrotCultureBlessingsOwnerOnly=false) - blogoslawienstwa ROT dla kazdej kultury osady (jak BKROT)"; return; }
            if (fa == null || fw == null || !fa.Patched || !fw.Patched) { _ownerOnlyState = "nieczynne - naprawa kultury (BkrotFixCulture) nie stoi"; return; }
            var tData = fa.Target.GetParameters()[0].ParameterType;
            _pCdcSettlement = AccessTools.Property(tData, "Settlement");
            _pCdcCulture = AccessTools.Property(tData, "Culture");
            _acceptBase = Caller(fa.Base, typeof(Func<object, object, ExplainedNumber>)) as Func<object, object, ExplainedNumber>;
            _weightBase = Caller(fw.Base, typeof(Func<object, Settlement, object, float, ExplainedNumber>)) as Func<object, Settlement, object, float, ExplainedNumber>;
            if (_pCdcSettlement == null || _pCdcCulture == null || _acceptBase == null || _weightBase == null)
            { _ownerOnlyState = "BRAK - skladowe BK (CultureDataClass.Settlement / Culture albo wywolanie modelu BK) - blogoslawienstwa jak BKROT"; return; }
            _harmony.Patch(fa.Target, prefix: new HarmonyMethod(typeof(BkrotSelfCall), nameof(AcceptOwnerOnly)));
            _harmony.Patch(fw.Target, prefix: new HarmonyMethod(typeof(BkrotSelfCall), nameof(WeightOwnerOnly)));
            int n = (HasOur(fa.Target, nameof(AcceptOwnerOnly), false) ? 1 : 0) + (HasOur(fw.Target, nameof(WeightOwnerOnly), false) ? 1 : 0);
            _ownerOnly = n == 2;
            _ownerOnlyState = _ownerOnly ? "TAK (2/2)" : "BRAK - prefiksow w GetPatchInfo " + n + " z 2";
        }

        /// <summary>Kultura inna niz kultura pana osady (wodza rodu wlasciciela - tego samego bohatera, ktorego blogoslawienstwa sprawdza BKROT).</summary>
        private static bool NotOwnerCulture(Settlement st, CultureObject cul)
        {
            var leader = st != null && st.OwnerClan != null ? st.OwnerClan.Leader : null;
            return leader != null && cul != null && cul != leader.Culture;
        }

        /// <summary>AM-15 klucz 2: BKROTCultureModel.CalculateAcceptanceGain dla kultury innej niz kultura pana - sam wynik modelu BK (to, co BKROT
        /// liczy przed doklejeniem blogoslawienstw); kultura pana - kod BKROT bez zmian (BK + blogoslawienstwa). Wyjatek BK leci jak z BKROT.</summary>
        internal static bool AcceptOwnerOnly(object __instance, object __0, ref ExplainedNumber __result)
        {
            if (!_ownerOnly || __0 == null) return true;
            try
            {
                if (!NotOwnerCulture(_pCdcSettlement.GetValue(__0, null) as Settlement, _pCdcCulture.GetValue(__0, null) as CultureObject)) return true;
            }
            catch (Exception e) { Stumble("AcceptOwnerOnly", e); return true; }
            Interlocked.Increment(ref _dOwnerOnly);
            __result = _acceptBase(__instance, __0);
            return false;
        }

        /// <summary>AM-15 klucz 2: to samo dla BKROTCultureModel.CalculateCultureWeight (+25 / +25 / +50 blogoslawienstw tylko do wagi kultury pana).</summary>
        internal static bool WeightOwnerOnly(object __instance, Settlement __0, object __1, float __2, ref ExplainedNumber __result)
        {
            if (!_ownerOnly || __1 == null) return true;
            try
            {
                if (!NotOwnerCulture(__0, _pCdcCulture.GetValue(__1, null) as CultureObject)) return true;
            }
            catch (Exception e) { Stumble("WeightOwnerOnly", e); return true; }
            Interlocked.Increment(ref _dOwnerOnly);
            __result = _weightBase(__instance, __0, __1, __2);
            return false;
        }

        /// <summary>Delegat wolajacy metode modelu BK NIEWIRTUALNIE (jak base.M) na podanym obiekcie: pierwszy parametr delegata to obiekt modelu
        /// (object), dalej argumenty (object dla typow BK - rzutowane); wynik jak metoda albo float z ResultNumber (BKExplainedNumber).
        /// Null, gdy sygnatury sie nie zgadzaja.</summary>
        private static Delegate Caller(MethodInfo m, Type del)
        {
            if (m == null) return null;
            var inv = del.GetMethod("Invoke");
            var dps = inv.GetParameters().Select(p => p.ParameterType).ToArray();
            var mps = m.GetParameters();
            if (dps.Length != mps.Length + 1 || dps[0] != typeof(object)) return null;
            MethodInfo result = null;
            if (inv.ReturnType != m.ReturnType)
            {
                result = inv.ReturnType == typeof(float) ? AccessTools.PropertyGetter(m.ReturnType, "ResultNumber") : null;
                if (result == null || result.ReturnType != typeof(float)) return null;
            }
            for (int i = 0; i < mps.Length; i++)
            {
                var want = mps[i].ParameterType;
                var have = dps[i + 1];
                if (have != want && (have != typeof(object) || want.IsValueType)) return null;
            }
            var dm = new DynamicMethod("cs_bkrot_base_" + m.DeclaringType.Name + "_" + m.Name, inv.ReturnType, dps, typeof(BkrotSelfCall).Module, true);
            var il = dm.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Castclass, m.DeclaringType);
            for (int i = 0; i < mps.Length; i++)
            {
                il.Emit(OpCodes.Ldarg, (short)(i + 1));
                if (dps[i + 1] != mps[i].ParameterType) il.Emit(OpCodes.Castclass, mps[i].ParameterType);
            }
            il.Emit(OpCodes.Call, m);
            if (result != null)
            {
                var loc = il.DeclareLocal(m.ReturnType);
                il.Emit(OpCodes.Stloc, loc);
                il.Emit(OpCodes.Ldloca, loc);
                il.Emit(OpCodes.Call, result);
            }
            il.Emit(OpCodes.Ret);
            return dm.CreateDelegate(del);
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
                   + " | blogoslawienstwa kultury tylko dla kultury pana (AM-15): " + _ownerOnlyState
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
