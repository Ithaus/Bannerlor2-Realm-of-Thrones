using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// E4/6a (z E4/6 "spichlerz i glod"; Jeff 11.10 "tak naprawic") - JEDEN LANCUCH MODELU JEDZENIA PARTII.
    ///
    /// LANCUCH U JEFFA (kolejnosc modulow z rgl "Command Args": ... BannerKings.Redux, BetterEconomy, NavalDLC, ..., ROT-Core, ...,
    /// RealisticBannerlord, ..., Armoury, ..., BKROTPatch). Gra bierze OSTATNI dodany model (GameModelsManager.GetGameModel - od konca listy),
    /// a CampaignGameStarter.AddModel&lt;T&gt; wpisuje nowemu modelowi poprzedni jako BaseModel. Kolejne OnGameStart dokladaja:
    ///  1. DefaultMobilePartyFoodConsumptionModel (gra): baza -(ludzie + jency/2) / NumberOfMenOnMapToEatOneFood (20), perki vanilli,
    ///     LimitMax(-0.01), DoesPartyConsumeFood wedlug flag partii (lord / gracz / bez wodza; bez zalog, karawan, band, milicji, patroli, wiesniakow);
    ///  2. BK BKPartyConsumptionModel (po Default, NIE wola base w CalculateDailyFoodConsumptionf): wlasna kopia perkow vanilli (bez sprawdzania
    ///     morza), LimitMax(0), perki BK (Kheshig Raider, Siege Overseer, Jawwal), pasza zwierzat na pustyni (pelna, bez wielbladow) i w sniegu /
    ///     zamieci (pol) - CalculateAnimalFoodNeed; DoesPartyConsumeFood = false dla partii z komponentem BK (wedrowcy, majatki, wolne kompanie,
    ///     zalogi i milicje BK, orszaki), reszta przez Default; ludzi na 1 jedzenie 20 + SlowerParties x 20 (u Jeffa SlowerParties 0 = 20);
    ///  3. BEE BEE_MobilePartyFoodConsumptionModel(_baseModel = BK): tylko oslona wyjatkow (wlasnej reguly brak);
    ///  4. NavalDLC NavalDLCMobilePartyFoodConsumptionModel (BaseModel = BEE): -20% na morzu + perk Smooth Operator;
    ///  5. ROT ROTMobilePartyFoodConsumptionModel(_previousModel = NavalDLC): Inni (klan kultury whitewalker) - baza 0, zuzycie 0, nie jedza;
    ///     zaciagniety gracz (ROT enlistment) - baza 0;
    ///  6. RBL RealisticFoodConsumptionModel (po Default, BaseModel = ROT, ale go NIE uzywa): base.CalculateDailyFoodConsumptionf (perki Default)
    ///     + pora roku (lato SummerFoodBonus, zima WinterFoodPenalty - u Jeffa -0.1 i 0, zima jest nasza: WinterSource) + pogoda RBL
    ///     (zamiec +40%, upal +20%); baze, DoesPartyConsumeFood i NumberOfMenOnMapToEatOneFood bierze z Default. AKTYWNY.
    ///     U Jeffa pory roku RBL sa wylaczone na twardo (CrashScribe Mends.SeasonsOff: getter SeasonsEnabled = false; log CrashScribe
    ///     "pory roku RB wylaczone na twardo") - RBL zwraca wtedy sam wynik base, bez pory roku i pogody: jego wlasny wklad to dzis zero,
    ///     a jedzenie partii liczyl w praktyce sam Default (baza 1/20 + perki vanilli) plus nasze WinterBite i Rations.
    /// Log 11.10 05:18 (linia E4/1): "lancuch: RealisticFoodConsumptionModel (konczy lancuch - nie wola modelu wewnetrznego)" - ROT, NavalDLC,
    /// BEE i BK nie biegly wcale: Inni jedli (i glodowali), -20% na morzu nie dzialalo, pasza BK i perki BK nie dzialaly, partie BK jadly.
    ///
    /// NAPRAWA (wylacznik FoodModelChain, domyslnie wlaczony; stan ustalany raz na kampanie): RBL liczy swoje, a pod nim biegnie caly lancuch.
    ///  - CalculateDailyFoodConsumptionf: transpiler w RBL podmienia JEDYNE wywolanie base (call Default::CalculateDailyFoodConsumptionf)
    ///    na InnerDaily - model wewnetrzny RBL (BaseModel = ROT -> NavalDLC -> BEE -> BK). Perki vanilli licza sie raz (kopia BK; Default nie
    ///    biegnie), na wynik lancucha RBL naklada pore roku i pogode. Transpiler, nie prefiks na Default: wywolanie base jest niewirtualne,
    ///    a mala metoda Default moglaby byc wklejona przez JIT w RBL (prefiks by wtedy nie zadzialal).
    ///  - CalculateDailyBaseFoodConsumptionf i DoesPartyConsumeFood: RBL ich nie deklaruje (dziedziczy z Default) - prefiks na metodach
    ///    Default, ktory dla obiektu typu RBL oddaje wynik modelu wewnetrznego (BK woluje base.DoesPartyConsumeFood na SWOIM obiekcie - prefiks
    ///    go przepuszcza). Wolane wirtualnie przez abstrakcyjny model (MobileParty.FoodChange, FoodConsumptionBehavior) - bez wklejania.
    ///  - ZIMA ARMOURY RAZ: WinterBite.FoodPostfix (zima +50% i pasza koni E4/1) siedzi na Default.CalculateDailyBaseFoodConsumptionf -
    ///    biegnie w najglebszym modelu (BK dziedziczy te metode), a na zewnetrznym poziomie RBL (wynik z lancucha) pomija sie (BaseFromChain;
    ///    znacznik watku ustawiany tylko, gdy prefiks naprawde oddal wynik lancucha).
    ///  - PASZA KONI E4/1: WinterRealm.FeedInfo idzie przez RBL w glab, gdy lancuch jest naprawiony (Chains) - widzi BK, wiec regula paszy BK
    ///    "BIEGNIE"; na pustyni i w sniegu (tam karmi BK) E4/1 doplaca w zimie tylko roznice ponad pasze BK (po recenzji: pasza = wieksza
    ///    z E4/1 i BK) - bez podwojnej paszy i bez dziury, gdzie zima najostrzejsza.
    ///  - RATIONS -40%: licznik zagniezdzenia (Rations._depth) tnie raz, na zewnatrz (RBL), choc latka siedzi na kazdym modelu lancucha.
    ///  - NumberOfMenOnMapToEatOneFood NIE przepiety (getter jednej instrukcji - latka bez potrzeby: u Jeffa BK SlowerParties 0, wiec 20 = 20);
    ///    linia kampanii pokazuje obie liczby i ostrzega, gdyby sie rozjechaly.
    /// Wszystko albo nic: gdy ktores wpiecie nie stanie, lancuch nie biegnie wcale (RBL jak dotad). Wyjatki per wywolanie liczone (potkniecia)
    /// i wtedy to wywolanie liczy po staremu (Default) - bez globalnego wylacznika. Zapora glebokosci (MaxDepth) przeciw petli modeli.
    /// </summary>
    internal static class FoodChain
    {
        internal const string RblModel = "RealisticBannerlord.Systems.Seasons.RealisticFoodConsumptionModel";
        private const string BkComponent = "BannerKings.Components.BannerKingsComponent";
        private const int MaxDepth = 4;
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private static Type _rbl;
        private static bool _patched;                 // wszystkie trzy wpiecia stoja (inaczej lancuch nie biegnie - bez polowicznego stanu)
        private static int _swapped;                  // ile wywolan base podmienil transpiler w RBL
        private static string _start = "nie wpiety";  // stan wpiecia do linii kampanii
        private static MethodInfo _baseModelGetter;   // MBGameModel<MobilePartyFoodConsumptionModel>.BaseModel (protected)
        private static Func<DefaultMobilePartyFoodConsumptionModel, MobileParty, ExplainedNumber, ExplainedNumber> _defaultDaily;   // Default bez wywolania wirtualnego

        private sealed class State
        {
            public Campaign Camp;
            public object Model;                      // obiekt RBL, dla ktorego liczony
            public bool On;
            public MobilePartyFoodConsumptionModel Inner;
            public string Why = "";
        }
        private static volatile State _st;
        private static readonly object Gate = new object();

        [ThreadStatic] private static int _depth;
        [ThreadStatic] private static object _baseDone;   // obiekt RBL, ktoremu prefiks bazy oddal wynik lancucha (WinterBite nie dolicza zimy drugi raz)

        private static int _stumbles;
        private static readonly Dictionary<string, int> _stumbleAt = new Dictionary<string, int>();
        private static Campaign _loggedFor;

        private static void Stumble(string where, Exception e)
        {
            int n = Interlocked.Increment(ref _stumbles);
            lock (Gate) { int v; _stumbleAt.TryGetValue(where, out v); _stumbleAt[where] = v + 1; }
            if (n <= 3) Log.Error("FoodChain." + where, e);
        }

        // ------------------------------------------------------------ stan (raz na kampanie, na obiekt RBL)

        private static State For(object model)
        {
            var cur = Campaign.Current;
            var s = _st;
            if (s != null && ReferenceEquals(s.Model, model) && ReferenceEquals(s.Camp, cur)) return s;
            lock (Gate)
            {
                s = _st;
                if (s != null && ReferenceEquals(s.Model, model) && ReferenceEquals(s.Camp, cur)) return s;
                s = Build(model, cur);
                _st = s;
                return s;
            }
        }

        private static State Build(object model, Campaign cur)   // pod Gate
        {
            var s = new State { Camp = cur, Model = model };
            try
            {
                if (!_patched) { s.Why = "wpiecia nie stoja: " + _start; return s; }
                if (!Settings.Current.FoodModelChain) { s.Why = "wylacznik FoodModelChain (zmiana dziala od wczytania kampanii)"; return s; }
                var inner = BaseModelOf(model);
                if (inner == null) { s.Why = "RBL bez modelu wewnetrznego (BaseModel pusty)"; return s; }
                object m = inner;
                for (int i = 0; m != null && i < 16; i++)
                {
                    if (ReferenceEquals(m, model)) { s.Why = "petla - model wewnetrzny prowadzi z powrotem do RBL"; return s; }
                    m = Next(m, false);
                }
                s.Inner = inner;
                s.On = true;
            }
            catch (Exception e) { Stumble("stan", e); s.On = false; s.Inner = null; s.Why = "wyjatek " + e.GetType().Name; }
            return s;
        }

        private static MobilePartyFoodConsumptionModel BaseModelOf(object model)
        {
            if (model == null || _baseModelGetter == null) return null;
            var inner = _baseModelGetter.Invoke(model, null) as MobilePartyFoodConsumptionModel;
            return ReferenceEquals(inner, model) ? null : inner;
        }

        /// <summary>Model wewnetrzny w lancuchu: RBL (tylko przy follow i naprawionym lancuchu) - jego BaseModel; owijka (typ wprost po
        /// abstrakcyjnym MobilePartyFoodConsumptionModel) - pierwsze pole typu modelu jedzenia (ROT _previousModel, BEE _baseModel, NavalDLC
        /// BaseModel gry); model po klasie konkretnej (BK, Default, RBL bez naprawy) - koniec lancucha.</summary>
        private static object Next(object m, bool follow)
        {
            if (m == null) return null;
            var t = m.GetType();
            if (_rbl != null && t == _rbl) return follow && Chains(m) ? For(m).Inner : null;
            if (t.BaseType != typeof(MobilePartyFoodConsumptionModel)) return null;
            for (var k = t; k != null && k != typeof(object); k = k.BaseType)
                foreach (var fi in k.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (!typeof(MobilePartyFoodConsumptionModel).IsAssignableFrom(fi.FieldType)) continue;
                    var v = fi.GetValue(m);
                    if (v != null && !ReferenceEquals(v, m)) return v;
                }
            return null;
        }

        /// <summary>Czy ten model (RBL) oddaje swoje metody lancuchowi w tej kampanii - ten sam warunek co wpiecia (WinterRealm.FeedInfo).</summary>
        internal static bool Chains(object model)
        {
            if (model == null || _rbl == null || model.GetType() != _rbl) return false;
            try { return For(model).On; }
            catch (Exception e) { Stumble("stan", e); return false; }
        }

        private static MobilePartyFoodConsumptionModel InnerFor(object model)
        {
            try { var s = For(model); return s.On ? s.Inner : null; }
            catch (Exception e) { Stumble("stan", e); return null; }
        }

        /// <summary>WinterBite.FoodPostfix: baza tego obiektu przyszla z lancucha - zima i pasza juz doliczone w najglebszym modelu.</summary>
        internal static bool BaseFromChain(object model)
        {
            var d = _baseDone;
            return model != null && d != null && ReferenceEquals(d, model);
        }

        // ------------------------------------------------------------ wpiecia

        /// <summary>RBL.CalculateDailyFoodConsumptionf: call Default::CalculateDailyFoodConsumptionf (base) -> InnerDaily.</summary>
        public static IEnumerable<CodeInstruction> DailyTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            var helper = AccessTools.Method(typeof(FoodChain), nameof(InnerDaily));
            foreach (var ci in instructions)
            {
                var m = ci.operand as MethodInfo;
                if (ci.opcode == OpCodes.Call && m != null && m.DeclaringType == typeof(DefaultMobilePartyFoodConsumptionModel)
                    && m.Name == "CalculateDailyFoodConsumptionf" && m.GetParameters().Length == 2)
                {
                    ci.operand = helper;
                    _swapped++;
                }
                yield return ci;
            }
        }

        /// <summary>Zamiast base w RBL: model wewnetrzny RBL (ROT -> NavalDLC -> BEE -> BK); bez lancucha / przy wyjatku - Default jak dotad.</summary>
        public static ExplainedNumber InnerDaily(DefaultMobilePartyFoodConsumptionModel self, MobileParty party, ExplainedNumber baseConsumption)
        {
            var inner = InnerFor(self);
            if (inner != null && _depth < MaxDepth)
            {
                _depth++;
                try { return inner.CalculateDailyFoodConsumptionf(party, baseConsumption); }
                catch (Exception e) { Stumble("zuzycie", e); }
                finally { _depth--; }
            }
            return _defaultDaily(self, party, baseConsumption);
        }

        /// <summary>Default.CalculateDailyBaseFoodConsumptionf dla obiektu RBL: baza z modelu wewnetrznego (ROT: Inni i zaciagniety gracz 0).</summary>
        public static bool BasePrefix(DefaultMobilePartyFoodConsumptionModel __instance, MobileParty __0, bool __1, ref ExplainedNumber __result)
        {
            if (__instance == null || _rbl == null || __instance.GetType() != _rbl) return true;
            _baseDone = null;
            var inner = InnerFor(__instance);
            if (inner == null || _depth >= MaxDepth) return true;
            _depth++;
            try
            {
                __result = inner.CalculateDailyBaseFoodConsumptionf(__0, __1);
                _baseDone = __instance;
                return false;
            }
            catch (Exception e) { Stumble("baza", e); return true; }
            finally { _depth--; }
        }

        /// <summary>Priority.Last - po WinterBite.FoodPostfix: znacznik "baza z lancucha" zdjety.</summary>
        public static void BasePostfix(DefaultMobilePartyFoodConsumptionModel __instance)
        {
            if (_baseDone != null && ReferenceEquals(_baseDone, __instance)) _baseDone = null;
        }

        /// <summary>Default.DoesPartyConsumeFood dla obiektu RBL: wynik modelu wewnetrznego (ROT: Inni nie jedza; BK: partie BK nie jedza).</summary>
        public static bool EatsPrefix(DefaultMobilePartyFoodConsumptionModel __instance, MobileParty __0, ref bool __result)
        {
            if (__instance == null || _rbl == null || __instance.GetType() != _rbl) return true;
            var inner = InnerFor(__instance);
            if (inner == null || _depth >= MaxDepth) return true;
            _depth++;
            try { __result = inner.DoesPartyConsumeFood(__0); return false; }
            catch (Exception e) { Stumble("czy je", e); return true; }
            finally { _depth--; }
        }

        /// <summary>Default::CalculateDailyFoodConsumptionf wolane niewirtualnie (jak base w RBL) - wlasna metoda dynamiczna z OpCodes.Call
        /// (AccessTools.MethodDelegate z virtualCall=false nie wiaze sie z ta sygnatura - test poza gra 11.10: ArgumentException).</summary>
        private static Func<DefaultMobilePartyFoodConsumptionModel, MobileParty, ExplainedNumber, ExplainedNumber> NonVirtual(MethodInfo mDaily)
        {
            var dm = new DynamicMethod("FoodChain_DefaultDaily", typeof(ExplainedNumber),
                new[] { typeof(DefaultMobilePartyFoodConsumptionModel), typeof(MobileParty), typeof(ExplainedNumber) }, typeof(FoodChain).Module, true);
            var il = dm.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Ldarg_2);
            il.Emit(OpCodes.Call, mDaily);
            il.Emit(OpCodes.Ret);
            return (Func<DefaultMobilePartyFoodConsumptionModel, MobileParty, ExplainedNumber, ExplainedNumber>)dm.CreateDelegate(
                typeof(Func<DefaultMobilePartyFoodConsumptionModel, MobileParty, ExplainedNumber, ExplainedNumber>));
        }

        internal static void ApplyAll(Harmony h)
        {
            try
            {
                // typ RBL i getter BaseModel takze przy wylaczniku - tylko do linii kampanii (RBL rozpoznany, lancuch pod nim widoczny); bez _patched nic nie biegnie
                _rbl = AccessTools.TypeByName(RblModel);
                _baseModelGetter = AccessTools.PropertyGetter(typeof(MBGameModel<MobilePartyFoodConsumptionModel>), "BaseModel");
                if (!Settings.Current.FoodModelChain) { _start = "wylaczony (FoodModelChain)"; Log.Info("FoodChain (E4/6a): wylaczony (FoodModelChain) - model jedzenia partii jak dotad."); return; }
                if (_rbl == null) { _start = "brak RealisticBannerlord"; Log.Info("FoodChain (E4/6a): brak RealisticBannerlord - nic do naprawy."); return; }
                var def = typeof(DefaultMobilePartyFoodConsumptionModel);
                var mDaily = AccessTools.Method(def, "CalculateDailyFoodConsumptionf", new[] { typeof(MobileParty), typeof(ExplainedNumber) });
                var mBase = AccessTools.Method(def, "CalculateDailyBaseFoodConsumptionf", new[] { typeof(MobileParty), typeof(bool) });
                var mEats = AccessTools.Method(def, "DoesPartyConsumeFood", new[] { typeof(MobileParty) });
                var mRbl = AccessTools.DeclaredMethod(_rbl, "CalculateDailyFoodConsumptionf", new[] { typeof(MobileParty), typeof(ExplainedNumber) });
                var miss = new List<string>();
                if (mDaily == null) miss.Add("Default.CalculateDailyFoodConsumptionf");
                if (mBase == null) miss.Add("Default.CalculateDailyBaseFoodConsumptionf");
                if (mEats == null) miss.Add("Default.DoesPartyConsumeFood");
                if (mRbl == null) miss.Add("RBL.CalculateDailyFoodConsumptionf");
                if (_baseModelGetter == null) miss.Add("MBGameModel.BaseModel");
                if (miss.Count > 0) { _start = "brak metod: " + string.Join(", ", miss.ToArray()); Log.Info("FoodChain (E4/6a): NIE wpiety - " + _start + "; model jedzenia jak dotad."); return; }
                _defaultDaily = NonVirtual(mDaily);
                if (_defaultDaily == null) { _start = "brak wywolania Default"; Log.Info("FoodChain (E4/6a): NIE wpiety - " + _start + "."); return; }

                h.Patch(mRbl, transpiler: new HarmonyMethod(typeof(FoodChain), nameof(DailyTranspiler)));
                if (_swapped <= 0) { _start = "w RBL nie ma wywolania base"; Log.Info("FoodChain (E4/6a): NIE wpiety - " + _start + " (inna wersja RBL?); model jedzenia jak dotad."); return; }
                h.Patch(mBase, prefix: new HarmonyMethod(typeof(FoodChain), nameof(BasePrefix)) { priority = Priority.First },
                               postfix: new HarmonyMethod(typeof(FoodChain), nameof(BasePostfix)) { priority = Priority.Last });
                h.Patch(mEats, prefix: new HarmonyMethod(typeof(FoodChain), nameof(EatsPrefix)) { priority = Priority.First });
                _patched = true;
                _start = "wpiety";
                Log.Info("FoodChain (E4/6a): wpiety - RBL liczy pory roku i pogode na wyniku swojego modelu wewnetrznego (ROT -> NavalDLC -> BEE -> BK);"
                         + " baza i DoesPartyConsumeFood RBL z lancucha; zima Armoury raz (w bazie najglebszego modelu). Linia lancucha w pierwszej dobie kampanii.");
            }
            catch (Exception e) { _patched = false; _start = "wyjatek przy wpinaniu " + e.GetType().Name; Log.Error("FoodChain.ApplyAll", e); }
        }

        // ------------------------------------------------------------ linia raz na kampanie

        /// <summary>Raz na kampanie (pierwsza doba, ArmouryBehavior przed WinterBite.OnDaily): lancuch modelu jedzenia i co biegnie.</summary>
        internal static void Daily()
        {
            var cur = Campaign.Current;
            if (cur == null || ReferenceEquals(_loggedFor, cur)) return;
            _loggedFor = cur;
            try { Log.Info(Line(cur)); }
            catch (Exception e) { Stumble("linia", e); }
        }

        private static string Walk(object m, bool follow)
        {
            var sb = new StringBuilder();
            var seen = new List<object>();
            for (int i = 0; m != null && i < 16; i++)
            {
                bool again = false;
                foreach (var o in seen) if (ReferenceEquals(o, m)) { again = true; break; }
                if (again) { sb.Append(" (petla)"); break; }
                seen.Add(m);
                sb.Append(i > 0 ? " -> " : "").Append(m.GetType().Name);
                m = Next(m, follow);
            }
            return sb.Length > 0 ? sb.ToString() : "-";
        }

        private static List<Type> Types(object m, bool follow)
        {
            var list = new List<Type>();
            for (int i = 0; m != null && i < 16; i++)
            {
                if (list.Contains(m.GetType())) break;
                list.Add(m.GetType());
                m = Next(m, follow);
            }
            return list;
        }

        private static string RulesOf(Type t)
        {
            switch (t.FullName)
            {
                case RblModel:
                    {
                        bool on;
                        string txt = RblSeasons(out on);
                        return on ? "RBL - pora roku i pogoda RBL (" + txt + "; zamiec +40%, upal +20%)"
                                  : "RBL - bez wlasnego wkladu (pory roku RBL wylaczone - SeasonsEnabled = false, CrashScribe Mends gasi je na twardo; " + txt + ")";
                    }
                case "ROT.Models.ROTMobilePartyFoodConsumptionModel": return "ROT - Inni nie jedza (baza 0, zuzycie 0), zaciagniety gracz baza 0";
                case "NavalDLC.GameComponents.NavalDLCMobilePartyFoodConsumptionModel": return "NavalDLC - -20% na morzu (+ perk Smooth Operator)";
                case "BetterEconomy.Models.BEE_MobilePartyFoodConsumptionModel": return "BEE - oslona wyjatkow (bez wlasnej reguly)";
                case "BannerKings.Models.Vanilla.BKPartyConsumptionModel": return "BK - perki vanilli (kopia BK) i BK (Kheshig Raider, Siege Overseer, Jawwal), pasza zwierzat na pustyni pelna i w sniegu/zamieci pol, partie BK nie jedza";
                case "TaleWorlds.CampaignSystem.GameComponents.DefaultMobilePartyFoodConsumptionModel": return "Default - perki vanilli";
                default: return t.Name + " - nieznany model";
            }
        }

        private static string RblSeasons(out bool seasonsOn)
        {
            seasonsOn = false;
            try
            {
                var t = AccessTools.TypeByName("RealisticBannerlord.Settings.RealisticSettings");
                var p = t != null ? AccessTools.Property(t, "Instance") : null;
                var o = p != null ? p.GetValue(null, null) : null;
                if (o == null) return "ustawienia RBL nieczytelne";
                object on = AccessTools.Property(t, "SeasonsEnabled")?.GetValue(o, null);
                object sum = AccessTools.Property(t, "SummerFoodBonus")?.GetValue(o, null);
                object win = AccessTools.Property(t, "WinterFoodPenalty")?.GetValue(o, null);
                seasonsOn = on is bool b && b;
                return "pory roku " + (seasonsOn ? "TAK" : "NIE") + ", lato " + Num(sum) + ", zima " + Num(win) + " w ustawieniach RBL";
            }
            catch (Exception e) { Stumble("linia(RBL)", e); return "ustawienia RBL nieczytelne"; }
        }

        private static string Num(object v) { return v is float f ? f.ToString("+0.00;-0.00;0", Inv) : "?"; }

        private static bool IsBkParty(MobileParty mp)
        {
            var pc = mp.PartyComponent;
            for (var t = pc != null ? pc.GetType() : null; t != null && t != typeof(object); t = t.BaseType)
                if (t.FullName == BkComponent) return true;
            return false;
        }

        private static string Line(Campaign cur)
        {
            var sb = new StringBuilder(1500);
            sb.Append("Lancuch modelu jedzenia (E4/6a): ");
            var fm = cur.Models != null ? cur.Models.MobilePartyFoodConsumptionModel : null;
            if (fm == null) return sb.Append("brak modelu jedzenia partii.").ToString();
            var t = fm.GetType();
            bool isRbl = _rbl != null && t == _rbl;
            bool chained = isRbl && Chains(fm);
            sb.Append(chained ? "NAPRAWIONY" : isRbl ? "NIE NAPRAWIONY (" + For(fm).Why + ")" : "nie dotyczy (aktywny model to nie RBL; wpiecie: " + _start + ")");
            sb.Append(" | aktywny model: ").Append(t.FullName);
            if (isRbl)
            {
                MobilePartyFoodConsumptionModel below = null;
                try { below = BaseModelOf(fm); } catch (Exception e) { Stumble("linia(BaseModel)", e); }
                sb.Append(" | bez naprawy: RealisticFoodConsumptionModel -> DefaultMobilePartyFoodConsumptionModel (base RBL, koniec); pod RBL nie biegnie: ").Append(Walk(below, false));
            }
            var types = Types(fm, true);
            sb.Append(" | ").Append(chained ? "teraz" : "lancuch").Append(": ").Append(Walk(fm, true));
            sb.Append(" | biegnie: ");
            for (int i = 0; i < types.Count; i++) sb.Append(i > 0 ? "; " : "").Append(RulesOf(types[i]));
            if (isRbl && !chained) sb.Append("; Default - perki vanilli (base RBL)");
            if (chained) sb.Append(" | przepiete w RBL: CalculateDailyFoodConsumptionf (base -> model wewnetrzny), CalculateDailyBaseFoodConsumptionf, DoesPartyConsumeFood");

            // ludzi na 1 jedzenie: aktywny (RBL -> Default) i lancuch (bez przepiecia)
            try
            {
                int a = fm.NumberOfMenOnMapToEatOneFood;
                var inner = chained ? For(fm).Inner : null;
                sb.Append(" | ludzi na 1 jedzenie: aktywny ").Append(a);
                if (inner != null)
                {
                    int c = inner.NumberOfMenOnMapToEatOneFood;
                    sb.Append(", lancuch ").Append(c).Append(a == c ? " (zgodne)" : " (ROZNE - zuzycie liczy lancuch, zakupy AI aktywny model; BK SlowerParties)");
                }
            }
            catch (Exception e) { Stumble("linia(ludzi)", e); }

            bool bk = types.Exists(x => x.FullName == "BannerKings.Models.Vanilla.BKPartyConsumptionModel");
            var s = Settings.Current;
            sb.Append(" | zima Armoury (WinterBite +").Append(s.WinterPartyFoodBonusPercent).Append("%, pasza koni E4/1): raz, w bazie najglebszego modelu");
            sb.Append("; regula paszy BK ").Append(bk ? "w lancuchu - na pustyni i w sniegu (tam karmi BK) E4/1 doplaca w zimie tylko roznice ponad pasze BK" : "poza lancuchem - E4/1 doplaca pelna pasze takze na pustyni i w sniegu");
            sb.Append(" | Rations -").Append(Math.Max(0, Math.Min(90, s.FoodConsumptionCutPercent))).Append("% raz, na zewnatrz (licznik zagniezdzenia)");

            // partie: kto je wedlug aktywnego modelu
            try
            {
                int act = 0, eat = 0, ww = 0, bkp = 0, other = 0;
                foreach (var mp in MobileParty.All)
                {
                    try
                    {
                        if (mp == null || !mp.IsActive) continue;
                        act++;
                        if (fm.DoesPartyConsumeFood(mp)) { eat++; continue; }
                        if (OthersRise.IsWhiteWalkerClan(mp.ActualClan)) ww++;
                        else if (IsBkParty(mp)) bkp++;
                        else other++;
                    }
                    catch (Exception e) { Stumble("linia(partia)", e); }
                }
                sb.Append(" | partie aktywne ").Append(act).Append(": jedza ").Append(eat).Append(", nie jedza ").Append(act - eat)
                  .Append(" (Inni ").Append(ww).Append(", partie BK ").Append(bkp).Append(", reszta - zalogi, karawany, bandy, milicje, wiesniacy itd. ").Append(other).Append(')');
            }
            catch (Exception e) { Stumble("linia(partie)", e); }

            // rozpiska partii gracza - kazda regula raz
            try
            {
                var mp = MobileParty.MainParty;
                if (mp != null)
                {
                    var b = fm.CalculateDailyBaseFoodConsumptionf(mp, true);
                    var r = fm.CalculateDailyFoodConsumptionf(mp, b);
                    sb.Append(" | partia gracza: je ").Append(fm.DoesPartyConsumeFood(mp) ? "TAK" : "NIE").Append(", ").Append(r.ResultNumber.ToString("0.00", Inv)).Append("/d = ");
                    bool first = true;
                    foreach (var ln in r.GetLines())
                    {
                        sb.Append(first ? "" : ", ").Append(ln.name).Append(' ').Append(ln.number.ToString("+0.00;-0.00;0", Inv));
                        first = false;
                    }
                }
            }
            catch (Exception e) { Stumble("linia(gracz)", e); }

            sb.Append(" | potkniecia od startu sesji ").Append(Volatile.Read(ref _stumbles));
            lock (Gate)
            {
                if (_stumbleAt.Count > 0)
                {
                    sb.Append(" (");
                    bool first = true;
                    foreach (var kv in _stumbleAt) { sb.Append(first ? "" : ", ").Append(kv.Key).Append(' ').Append(kv.Value); first = false; }
                    sb.Append(')');
                }
            }
            return sb.Append('.').ToString();
        }
    }
}
