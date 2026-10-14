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
    /// Linia startu sesji "BKROT self call fix: n/8 metod ..." (stan kazdej metody, GetPatchInfo). Linia doby "BKROT self call (P2): dzien N"
    /// (tylko odczyt): prawowitosc 3 najwiekszych krolestw (L, cel modelu, cel BK), wplyw ich rodow panujacych (limit i zmiana modelu / BK, koszt
    /// wniosku pokoju i wojny), pakty handlowe BK, koszt Call Banners krol <- wasal, kultura 3 miast (asymilacja i akceptacja kultury pana, zysk
    /// akceptacji model / BK, liczba kultur i suma asymilacji) i calego swiata (twierdze z >= 2 kulturami, wszystkie 1.0 = znak bledu; srednia
    /// asymilacja pana; lojalnosc srednia i < 25; zmiany kultury osad), ocena malzonka gracza, dochod z karawany, wyniki "poza kultura pana".
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

        // ------------------------------------------------------------ probka doby (tylko odczyt; linia "BKROT self call (P2): dzien N")
        // Wartosc modelu = metoda wolana wirtualnie na obiekcie z BannerKingsConfig (BKROT - z naprawa albo bez); "BK" = ta sama metoda modelu BK
        // wolana niewirtualnie na tym samym obiekcie (to, co BKROT powinien dostac od base). Bez naprawy: model 0, BK > 0.
        private static bool _smpTried, _smpOk;
        private static string _smpWhy = "-";
        private static PropertyInfo _pCfg, _pLegitM, _pInflM, _pArmyM, _pCultM, _pMarM, _pFinM, _pPopMgr, _pPopCulture, _pCultures, _pKdLegit;
        private static PropertyInfo _pCdcAssim, _pCdcAccept, _pCdcCult;
        private static MethodInfo _mGetPop, _mGetKd, _mPacts;
        private static Type _tDipBeh;
        private static Func<object, object, bool, float> _legitBase;
        private static Func<object, Clan, bool, ExplainedNumber> _capBase, _changeBase;
        private static Func<object, MobileParty, MobileParty, int> _armyBase;
        private static Func<object, object, ExplainedNumber> _acceptBaseS;
        private static Func<object, Hero, bool, ExplainedNumber> _spouseBase;
        private static Func<object, MobileParty, int> _caravanBase;
        // stan sesji (ResetSession z konstruktora zachowania - kazda nowa gra i wczytanie)
        private static object _dipBeh; private static bool _dipTried;
        private static Dictionary<Settlement, CultureObject> _cultSnap = new Dictionary<Settlement, CultureObject>();
        private static int _sCultChanges, _dSmpErr, _sSmpErr;

        internal static void ResetSession()
        {
            _dipBeh = null; _dipTried = false;
            _cultSnap = new Dictionary<Settlement, CultureObject>();
            _sCultChanges = 0; _dSmpErr = 0; _sSmpErr = 0;
            Interlocked.Exchange(ref _dOwnerOnly, 0); _sOwnerOnly = 0;
        }

        private static bool ResolveSample()
        {
            if (_smpTried) return _smpOk;
            _smpTried = true;
            var miss = new List<string>();
            var tCfg = AccessTools.TypeByName("BannerKings.BannerKingsConfig");
            if (tCfg == null) { _smpWhy = "BK nieobecny"; return false; }
            _pCfg = AccessTools.Property(tCfg, "Instance");
            _pLegitM = AccessTools.Property(tCfg, "LegitimacyModel");
            _pInflM = AccessTools.Property(tCfg, "InfluenceModel");
            _pArmyM = AccessTools.Property(tCfg, "ArmyManagementModel");
            _pCultM = AccessTools.Property(tCfg, "CultureModel");
            _pMarM = AccessTools.Property(tCfg, "MarriageModel");
            _pFinM = AccessTools.Property(tCfg, "ClanFinanceModel");
            _pPopMgr = AccessTools.Property(tCfg, "PopulationManager");
            _mGetPop = _pPopMgr != null ? AccessTools.Method(_pPopMgr.PropertyType, "GetPopData", new[] { typeof(Settlement) }) : null;
            _pPopCulture = _mGetPop != null ? AccessTools.Property(_mGetPop.ReturnType, "CultureData") : null;
            _pCultures = _pPopCulture != null ? AccessTools.Property(_pPopCulture.PropertyType, "Cultures") : null;
            var fa = FixByName("CalculateAcceptanceGain");
            var tCdc = fa != null && fa.Target != null ? fa.Target.GetParameters()[0].ParameterType : AccessTools.TypeByName("BannerKings.Managers.Populations.CultureDataClass");
            _pCdcAssim = tCdc != null ? AccessTools.Property(tCdc, "Assimilation") : null;
            _pCdcAccept = tCdc != null ? AccessTools.Property(tCdc, "Acceptance") : null;
            _pCdcCult = tCdc != null ? AccessTools.Property(tCdc, "Culture") : null;
            _tDipBeh = AccessTools.TypeByName("BannerKings.Behaviours.Diplomacy.BKDiplomacyBehavior");
            _mGetKd = _tDipBeh != null ? AccessTools.Method(_tDipBeh, "GetKingdomDiplomacy", new[] { typeof(Kingdom) }) : null;
            _pKdLegit = _mGetKd != null ? AccessTools.Property(_mGetKd.ReturnType, "Legitimacy") : null;
            _mPacts = _mGetKd != null ? AccessTools.Method(_mGetKd.ReturnType, "GetTradePactsSnapshot") : null;
            _legitBase = Caller(Base("CalculateKingdomLegitimacy"), typeof(Func<object, object, bool, float>)) as Func<object, object, bool, float>;
            _capBase = Caller(Base("CalculateInfluenceCap"), typeof(Func<object, Clan, bool, ExplainedNumber>)) as Func<object, Clan, bool, ExplainedNumber>;
            _changeBase = Caller(Base("CalculateInfluenceChange"), typeof(Func<object, Clan, bool, ExplainedNumber>)) as Func<object, Clan, bool, ExplainedNumber>;
            _armyBase = Caller(Base("CalculatePartyInfluenceCost"), typeof(Func<object, MobileParty, MobileParty, int>)) as Func<object, MobileParty, MobileParty, int>;
            _acceptBaseS = Caller(Base("CalculateAcceptanceGain"), typeof(Func<object, object, ExplainedNumber>)) as Func<object, object, ExplainedNumber>;
            _spouseBase = Caller(Base("GetSpouseScore"), typeof(Func<object, Hero, bool, ExplainedNumber>)) as Func<object, Hero, bool, ExplainedNumber>;
            _caravanBase = Caller(Base("CalculateOwnerIncomeFromCaravan"), typeof(Func<object, MobileParty, int>)) as Func<object, MobileParty, int>;
            if (_pCfg == null) miss.Add("BannerKingsConfig.Instance");
            if (_mGetPop == null || _pCultures == null || _pCdcAssim == null || _pCdcAccept == null || _pCdcCult == null) miss.Add("dane kultury BK");
            if (_mGetKd == null || _pKdLegit == null) miss.Add("KingdomDiplomacy.Legitimacy");
            _smpWhy = miss.Count > 0 ? string.Join(", ", miss.ToArray()) : "-";
            _smpOk = _pCfg != null;   // reszta - "-" w swoim kawalku linii
            return _smpOk;
        }

        private static MethodInfo Base(string name) { var f = FixByName(name); return f != null ? f.Base : null; }

        private static object KdOf(Kingdom k)
        {
            if (_mGetKd == null || k == null) return null;
            if (!_dipTried)
            {
                _dipTried = true;
                var get = typeof(Campaign).GetMethod("GetCampaignBehavior", Type.EmptyTypes);
                _dipBeh = get != null && Campaign.Current != null ? get.MakeGenericMethod(_tDipBeh).Invoke(Campaign.Current, null) : null;
            }
            return _dipBeh != null ? _mGetKd.Invoke(_dipBeh, new object[] { k }) : null;
        }

        private static void SmpErr(string where, Exception e)
        {
            var ie = e is TargetInvocationException && e.InnerException != null ? e.InnerException : e;
            _dSmpErr++;
            if (++_sSmpErr <= 3) { try { Scribe.Report("CrashScribe", ie, "BkrotSelfCall.probka(" + where + ")", null); } catch { } }
        }

        /// <summary>Wynik metody modelu wolanej WIRTUALNIE (jak BK: BannerKingsConfig.Instance.XModel.M) - NaN, gdy sie nie da.</summary>
        private static float ModelF(string name, object model, params object[] args)
        {
            var m = Base(name);
            if (m == null || model == null || !m.DeclaringType.IsInstanceOfType(model)) return float.NaN;
            try { return ToF(m.Invoke(model, args)); }
            catch (Exception e) { SmpErr(name, e); return float.NaN; }
        }

        private static float ToF(object o)
        {
            if (o is ExplainedNumber) return ((ExplainedNumber)o).ResultNumber;
            if (o is int) return (int)o;
            if (o is float) return (float)o;
            var p = o != null ? o.GetType().GetProperty("ResultNumber") : null;
            return p != null ? Convert.ToSingle(p.GetValue(o, null), Inv) : float.NaN;
        }

        private static float BkF(string name, Func<float> f)
        {
            if (f == null) return float.NaN;
            try { return f(); }
            catch (Exception e) { SmpErr(name + "(BK)", e); return float.NaN; }
        }

        private static string N(float v, string fmt) { return float.IsNaN(v) ? "-" : v.ToString(fmt, Inv); }
        private static string Nm(object o) { return o == null ? "-" : o.ToString().Replace("|", "/").Replace(";", ","); }

        /// <summary>Linia doby: prawowitosc 3 krolestw, wplyw 3 rodow panujacych (limit, zmiana, koszt wnioskow), Call Banners, kultura 3 miast
        /// i calego swiata, malzonek gracza, karawana, licznik blogoslawienstw poza kultura pana. Tylko odczyt. Nie rzuca.</summary>
        internal static void Daily()
        {
            try
            {
                if (Campaign.Current == null || !ResolveSample()) return;
                var cfg = _pCfg.GetValue(null, null);
                if (cfg == null) return;
                var sb = new StringBuilder("BKROT self call (P2): dzien ").Append((int)CampaignTime.Now.ToDays);
                var ks = Kingdom.All.Where(k => k != null && !k.IsEliminated && k.Leader != null && k.RulingClan != null)
                                    .OrderByDescending(k => k.Fiefs.Count).ThenBy(k => k.StringId, StringComparer.Ordinal).Take(3).ToList();
                // AM-10 prawowitosc
                sb.Append(" | prawowitosc: ");
                var lm = _pLegitM != null ? _pLegitM.GetValue(cfg, null) : null;
                for (int i = 0; i < ks.Count; i++)
                {
                    var k = ks[i];
                    object kd = null;
                    try { kd = KdOf(k); } catch (Exception e) { SmpErr("KingdomDiplomacy", e); }
                    if (i > 0) sb.Append("; ");
                    if (kd == null) { sb.Append(Nm(k.Name)).Append(" brak KD"); continue; }
                    float l = float.NaN;
                    try { l = Convert.ToSingle(_pKdLegit.GetValue(kd, null), Inv); } catch (Exception e) { SmpErr("Legitimacy", e); }
                    float m = ModelF("CalculateKingdomLegitimacy", lm, kd, false);
                    float b = BkF("CalculateKingdomLegitimacy", _legitBase == null || lm == null ? (Func<float>)null : () => _legitBase(lm, kd, false));
                    sb.Append(Nm(k.Name)).Append(" L ").Append(N(l, "0.000")).Append(" cel ").Append(N(m, "0.000")).Append(" (BK ").Append(N(b, "0.000")).Append(')');
                }
                // AM-11 wplyw rodow panujacych + koszt wnioskow (model dyplomacji gry - u Jeffa BKDiplomacyModel, czyta limit z BannerKingsConfig)
                sb.Append(" | wplyw: ");
                var im = _pInflM != null ? _pInflM.GetValue(cfg, null) : null;
                var dm = Campaign.Current.Models != null ? Campaign.Current.Models.DiplomacyModel : null;
                for (int i = 0; i < ks.Count; i++)
                {
                    var c = ks[i].RulingClan;
                    if (i > 0) sb.Append("; ");
                    float cap = ModelF("CalculateInfluenceCap", im, c, false);
                    float capB = BkF("CalculateInfluenceCap", _capBase == null || im == null ? (Func<float>)null : () => _capBase(im, c, false).ResultNumber);
                    float ch = ModelF("CalculateInfluenceChange", im, c, false);
                    float chB = BkF("CalculateInfluenceChange", _changeBase == null || im == null ? (Func<float>)null : () => _changeBase(im, c, false).ResultNumber);
                    float peace = BkF("GetInfluenceCostOfProposingPeace", dm == null ? (Func<float>)null : () => dm.GetInfluenceCostOfProposingPeace(c));
                    float war = BkF("GetInfluenceCostOfProposingWar", dm == null ? (Func<float>)null : () => dm.GetInfluenceCostOfProposingWar(c));
                    sb.Append(Nm(c.Name)).Append(" limit ").Append(N(cap, "0")).Append(" (BK ").Append(N(capB, "0")).Append(") zmiana ").Append(N(ch, "0.00"))
                      .Append(" (BK ").Append(N(chB, "0.00")).Append(") wplyw ").Append(N(c.Influence, "0")).Append(" koszt pokoju ").Append(N(peace, "0"))
                      .Append(" wojny ").Append(N(war, "0"));
                }
                int pacts = 0;
                if (_mPacts != null)
                    foreach (var k in Kingdom.All)
                    {
                        if (k == null || k.IsEliminated) continue;
                        try { var kd = KdOf(k); var en = kd != null ? _mPacts.Invoke(kd, null) as System.Collections.IEnumerable : null; if (en != null) foreach (var x in en) pacts++; }
                        catch (Exception e) { SmpErr("GetTradePactsSnapshot", e); break; }
                    }
                sb.Append("; pakty handlowe (wpisy krolestw) ").Append(_mPacts != null ? pacts.ToString(Inv) : "-");
                // AM-12 Call Banners: krol (ze swoja druzyna) <- glowa innego rodu krolestwa z druzyna
                sb.Append(" | call banners: ");
                var am = _pArmyM != null ? _pArmyM.GetValue(cfg, null) : null;
                MobileParty lp = null, vp = null;
                foreach (var k in ks)
                {
                    var kp = k.Leader.PartyBelongedTo;
                    if (kp == null || kp.LeaderHero != k.Leader) continue;
                    var vc = k.Clans.Where(c => c != null && c != k.RulingClan && !c.IsEliminated && !c.IsUnderMercenaryService && c.Leader != null
                                                && c.Leader.PartyBelongedTo != null && c.Leader.PartyBelongedTo.LeaderHero == c.Leader)
                                    .OrderBy(c => c.StringId, StringComparer.Ordinal).FirstOrDefault();
                    if (vc == null) continue;
                    lp = kp; vp = vc.Leader.PartyBelongedTo;
                    break;
                }
                if (lp == null) sb.Append("brak pary krol - wasal z druzynami");
                else
                {
                    float m = ModelF("CalculatePartyInfluenceCost", am, lp, vp);
                    float b = BkF("CalculatePartyInfluenceCost", _armyBase == null || am == null ? (Func<float>)null : () => _armyBase(am, lp, vp));
                    sb.Append(Nm(lp.LeaderHero.Name)).Append(" <- ").Append(Nm(vp.LeaderHero.Name)).Append(" koszt ").Append(N(m, "0")).Append(" (BK ").Append(N(b, "0")).Append(')');
                }
                // AM-15 kultura: 3 miasta (2 z obca kultura pana, 1 swoja) i caly swiat
                var cm = _pCultM != null ? _pCultM.GetValue(cfg, null) : null;
                var pm = _pPopMgr != null ? _pPopMgr.GetValue(cfg, null) : null;
                sb.Append(" | osady: ");
                var towns = Town.AllTowns.Where(t => t != null && t.Settlement != null && t.OwnerClan != null && t.OwnerClan.Leader != null)
                                         .OrderBy(t => t.Settlement.StringId, StringComparer.Ordinal).ToList();
                var pick = towns.Where(t => t.OwnerClan.Leader.Culture != t.Settlement.Culture).Take(2).ToList();
                pick.AddRange(towns.Where(t => t.OwnerClan.Leader.Culture == t.Settlement.Culture).Take(3 - pick.Count));
                for (int i = 0; i < pick.Count; i++)
                {
                    var st = pick[i].Settlement;
                    var oc = st.OwnerClan.Leader.Culture;
                    if (i > 0) sb.Append("; ");
                    sb.Append(Nm(st.Name)).Append(" [").Append(st.Culture != null ? st.Culture.StringId : "-").Append('/').Append(oc != null ? oc.StringId : "-").Append(']');
                    var cl = Cultures(pm, st);
                    if (cl == null) { sb.Append(" brak danych BK"); continue; }
                    float sum = 0f, oa = 0f, oac = 0f; object own = null;
                    foreach (var cdc in cl)
                    {
                        float a = Convert.ToSingle(_pCdcAssim.GetValue(cdc, null), Inv);
                        sum += a;
                        if (_pCdcCult.GetValue(cdc, null) as CultureObject == oc) { own = cdc; oa = a; oac = Convert.ToSingle(_pCdcAccept.GetValue(cdc, null), Inv); }
                    }
                    float g = own != null ? ModelF("CalculateAcceptanceGain", cm, own) : float.NaN;
                    float gb = own != null ? BkF("CalculateAcceptanceGain", _acceptBaseS == null || cm == null ? (Func<float>)null : () => _acceptBaseS(cm, own).ResultNumber) : float.NaN;
                    sb.Append(" asym. pana ").Append(N(oa, "0.00")).Append(" akcept. pana ").Append(N(oac, "0.00")).Append(" zysk akcept. ").Append(N(g, "+0.000;-0.000;0.000"))
                      .Append(" (BK ").Append(N(gb, "+0.000;-0.000;0.000")).Append(") kultur ").Append(cl.Count).Append(" suma ").Append(N(sum, "0.00"));
                }
                int fiefs = 0, allOne = 0, multi = 0, below = 0, riot = 0, changes = 0;
                double oaSum = 0, loySum = 0;
                foreach (var t in Town.AllFiefs)
                {
                    if (t == null || t.Settlement == null) continue;
                    var cl = Cultures(pm, t.Settlement);
                    if (cl == null) continue;
                    fiefs++;
                    loySum += t.Loyalty;
                    if (t.Loyalty < 25f) riot++;
                    var oc = t.OwnerClan != null && t.OwnerClan.Leader != null ? t.OwnerClan.Leader.Culture : null;
                    int n = 0, one = 0; float oa = 0f;
                    foreach (var cdc in cl)
                    {
                        float a = Convert.ToSingle(_pCdcAssim.GetValue(cdc, null), Inv);
                        n++;
                        if (a >= 0.99f) one++;
                        if (oc != null && _pCdcCult.GetValue(cdc, null) as CultureObject == oc) oa = a;
                    }
                    if (n >= 2) { multi++; if (one == n) allOne++; }
                    oaSum += oa;
                    if (oa < 0.5f) below++;
                }
                bool first = _cultSnap.Count == 0;
                foreach (var st in Settlement.All)
                {
                    if (st == null || !(st.IsTown || st.IsCastle || st.IsVillage)) continue;
                    CultureObject was;
                    if (_cultSnap.TryGetValue(st, out was) && was != st.Culture) changes++;
                    _cultSnap[st] = st.Culture;
                }
                _sCultChanges += changes;
                sb.Append(" | swiat: twierdz ").Append(fiefs).Append(" wszystkie kultury 1.0 ").Append(allOne).Append(" z ").Append(multi)
                  .Append(" asym. pana sr. ").Append(fiefs > 0 ? (oaSum / fiefs).ToString("0.00", Inv) : "-").Append(" <0.5 ").Append(below)
                  .Append(" lojalnosc sr. ").Append(fiefs > 0 ? (loySum / fiefs).ToString("0.0", Inv) : "-").Append(" <25 ").Append(riot)
                  .Append(" zmiany kultury osad ").Append(first ? "-" : changes.ToString(Inv)).Append(" (sesja ").Append(_sCultChanges).Append(')');
                // AM-18 malzonek gracza, karawana
                var mm = _pMarM != null ? _pMarM.GetValue(cfg, null) : null;
                var me = Hero.MainHero;
                float sp = me != null && me.IsAlive ? ModelF("GetSpouseScore", mm, me, false) : float.NaN;
                float spB = me != null && me.IsAlive ? BkF("GetSpouseScore", _spouseBase == null || mm == null ? (Func<float>)null : () => _spouseBase(mm, me, false).ResultNumber) : float.NaN;
                sb.Append(" | malzonek gracza ").Append(N(sp, "0")).Append(" (BK ").Append(N(spB, "0")).Append(')');
                var fm = _pFinM != null ? _pFinM.GetValue(cfg, null) : null;
                var car = MobileParty.AllCaravanParties.FirstOrDefault(x => x != null && x.IsActive && x.Owner != null);
                float cv = car != null ? ModelF("CalculateOwnerIncomeFromCaravan", fm, car) : float.NaN;
                float cvB = car != null ? BkF("CalculateOwnerIncomeFromCaravan", _caravanBase == null || fm == null ? (Func<float>)null : () => _caravanBase(fm, car)) : float.NaN;
                sb.Append(" | karawana ").Append(N(cv, "0")).Append(" (BK ").Append(N(cvB, "0")).Append(')');
                int oo = Interlocked.Exchange(ref _dOwnerOnly, 0);
                _sOwnerOnly += oo;
                sb.Append(" | poza kultura pana ").Append(oo).Append(" (sesja ").Append(_sOwnerOnly).Append(')');
                sb.Append(" | bledy probki ").Append(_dSmpErr).Append(" (sesja ").Append(_sSmpErr).Append(") | potkniecia od startu gry ").Append(_sStumbles).Append('.');
                _dSmpErr = 0;
                Scribe.Line(sb.ToString());
            }
            catch (Exception e) { Stumble("Daily", e); }
        }

        /// <summary>Lista CultureDataClass osady z BK (PopulationManager.GetPopData -> CultureData.Cultures) albo null.</summary>
        private static List<object> Cultures(object pm, Settlement st)
        {
            if (pm == null || _mGetPop == null || _pPopCulture == null || _pCultures == null || _pCdcAssim == null || _pCdcCult == null || _pCdcAccept == null) return null;
            var pop = _mGetPop.Invoke(pm, new object[] { st });
            var cd = pop != null ? _pPopCulture.GetValue(pop, null) : null;
            var en = cd != null ? _pCultures.GetValue(cd, null) as System.Collections.IEnumerable : null;
            if (en == null) return null;
            var res = new List<object>();
            foreach (var x in en) if (x != null) res.Add(x);
            return res;
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

    /// <summary>P2 (audyt 14.10): wpiecie i linia startu sesji samowywolan BKROT, probka doby. Bez zapisu; stan sesji od zera przy kazdej
    /// nowej grze i wczytaniu (zachowanie tworzone w OnGameStart).</summary>
    internal sealed class BkrotSelfCallBehavior : CampaignBehaviorBase
    {
        public BkrotSelfCallBehavior() { BkrotSelfCall.ResetSession(); }

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, delegate (CampaignGameStarter s) { BkrotSelfCall.OnSession(); });
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, delegate { BkrotSelfCall.Daily(); });
        }

        public override void SyncData(IDataStore dataStore) { }
    }
}
