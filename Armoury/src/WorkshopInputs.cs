using System;
using System.Collections.Generic;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Workshops;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// E6/M2 (208, PLAN 6.9; projekt etapu 6 v2 rozdz. 3 "E6/M2" (b), krytyka KB "M2-bramka-losowy-zapas") - WARSZTAT TOWAROWY TAM, GDZIE ROSNIE JEGO WSAD.
    /// Gra wybiera typ warsztatu (DecideBestWorkshopType: start nowej gry G WorkshopsCampaignBehavior.cs:1258, bankructwo :1097 - takze nasze
    /// WorkshopTrade.Fail, warsztat odebrany graczowi :1086) losowo z wag FindTotalInputDensityScore (:1116), w ktorych wsad z okregu jest tylko
    /// jednym skladnikiem - stad tlocznie bez winogron i tkalnie bez lnu. Bramka (postfiks wagi): typ, ktorego linie towarowe potrzebuja wsadu, a zaden
    /// z jego wsadow nie rosnie we wsiach TradeBound miasta (slownik gry z tego samego wywolania: krowy -> skory, owce -> welna) ani nie przychodzi
    /// kontraktem surowca 174.2 (MaterialOrders: ruda, drewno, skory, len, welna i ich wyroby), dostaje x0.01 wagi, NIE 0 - przy samych zerach gra
    /// losuje typ calkiem na slepo (:1319-1336, WorkshopType.All[RandomInt(1, ...)]), a tak wybierze typ z wsadem, jesli jest, i najmniej zly, jesli
    /// nie ma zadnego. Typy bez wsadu (linie bez Inputs albo bez wyrobu towarowego) bez zmian. Istniejacych warsztatow nie przestawia (jednorazowe
    /// przestawienie przy wczytaniu - nastepna sesja etapu 6); warsztatow gracza nie dotyka. Liczniki: wybory, z wsadem, bez, miasta bez typu z wsadem.
    /// </summary>
    internal static class WorkshopInputs
    {
        private static readonly string[] ContractIds = { "iron", "hardwood", "leather", "linen", "flax", "hides", "wool" };   // MaterialOrders.Ids
        private static HashSet<ItemCategory> _contract;
        private static int _picks, _picksStart, _withInput, _withoutInput, _noTypeTowns, _cut, _stumbles;
        private static readonly List<string> _noTypeNames = new List<string>();
        [ThreadStatic] private static bool _anyWithInput;
        [ThreadStatic] private static IDictionary<ItemCategory, float> _lastDict;
        internal static bool Wired;

        internal static bool On { get { var s = Settings.Current; return s != null && s.WorkshopsFollowInputs; } }

        internal static void Reset()
        {
            _contract = null; _picks = _picksStart = _withInput = _withoutInput = _noTypeTowns = _cut = _stumbles = 0; _noTypeNames.Clear();
        }

        private static bool Contracted(ItemCategory c)
        {
            if (c == null || !MaterialOrders.On) return false;
            if (_contract == null)
            {
                var set = new HashSet<ItemCategory>();
                try
                {
                    var om = MBObjectManager.Instance;
                    if (om != null) foreach (var id in ContractIds) { var it = om.GetObject<ItemObject>(id); if (it != null && it.ItemCategory != null) set.Add(it.ItemCategory); }
                }
                catch { }
                _contract = set;
            }
            return _contract.Contains(c);
        }

        private static bool TradeLine(WorkshopType.Production p)
        {
            if (p.Inputs == null || p.Inputs.Count == 0 || p.Outputs == null) return false;
            foreach (var o in p.Outputs) if (o.Item1 != null && o.Item1.IsTradeGood) return true;
            return false;
        }

        /// <summary>Typ potrzebuje wsadu: ma linie z wyrobem towarowym i niepustym wsadem (te same linie, ktore liczy waga gry).</summary>
        private static bool NeedsInput(WorkshopType t)
        {
            if (t == null || t.Productions == null) return false;
            foreach (var p in t.Productions) if (TradeLine(p)) return true;
            return false;
        }

        /// <summary>Choc jedna linia towarowa ma caly wsad z okregu (slownik gry) albo z kontraktu surowca.</summary>
        private static bool HasInput(WorkshopType t, IDictionary<ItemCategory, float> dict)
        {
            foreach (var p in t.Productions)
            {
                if (!TradeLine(p)) continue;
                bool all = true;
                foreach (var i in p.Inputs)
                {
                    float v;
                    if (i.Item1 != null && ((dict != null && dict.TryGetValue(i.Item1, out v) && v > 0f) || Contracted(i.Item1))) continue;
                    all = false; break;
                }
                if (all) return true;
            }
            return false;
        }

        public static void DecidePrefix() { _anyWithInput = false; _lastDict = null; }

        public static void ScorePostfix(WorkshopType workshopType, IDictionary<ItemCategory, float> productionDict, ref float __result)
        {
            if (workshopType == null || !On) return;
            try
            {
                _lastDict = productionDict;
                if (!NeedsInput(workshopType)) return;
                if (HasInput(workshopType, productionDict)) { _anyWithInput = true; return; }
                __result *= 0.01f;
                _cut++;
            }
            catch (Exception e) { _stumbles++; if (_stumbles <= 1) Log.Error("WorkshopInputs.ScorePostfix", e); }
        }

        /// <summary>Postfiks Last (po BK, ktory przy dublu w miescie podmienia typ na kopalnie albo kuznie): tylko liczniki wyboru.</summary>
        public static void DecidePostfix(Settlement currentSettlement, bool atGameStart, WorkshopType __result)
        {
            if (!On) return;
            try
            {
                _picks++;
                if (atGameStart) _picksStart++;
                if (__result != null && NeedsInput(__result)) { if (HasInput(__result, _lastDict)) _withInput++; else _withoutInput++; }
                if (!_anyWithInput)
                {
                    _noTypeTowns++;
                    if (_noTypeNames.Count < 8 && currentSettlement != null) _noTypeNames.Add(currentSettlement.Name != null ? currentSettlement.Name.ToString() : currentSettlement.StringId);
                }
            }
            catch (Exception e) { _stumbles++; if (_stumbles <= 1) Log.Error("WorkshopInputs.DecidePostfix", e); }
            finally { _lastDict = null; }
        }

        internal static void ApplyAll(Harmony h)
        {
            try
            {
                var score = AccessTools.Method(typeof(WorkshopsCampaignBehavior), "FindTotalInputDensityScore");
                var decide = AccessTools.Method(typeof(WorkshopsCampaignBehavior), "DecideBestWorkshopType");
                if (score != null && decide != null)
                {
                    h.Patch(decide, prefix: new HarmonyMethod(typeof(WorkshopInputs), nameof(DecidePrefix)),
                                    postfix: new HarmonyMethod(typeof(WorkshopInputs), nameof(DecidePostfix)) { priority = Priority.Last });
                    h.Patch(score, postfix: new HarmonyMethod(typeof(WorkshopInputs), nameof(ScorePostfix)));
                    Wired = true;
                }
                Log.Info("WorkshopInputs (E6/M2): bramka wsadu warsztatow towarowych " + (Wired ? "wpieta (FindTotalInputDensityScore x0.01 bez wsadu z okregu i kontraktu)" : "BRAK FindTotalInputDensityScore/DecideBestWorkshopType - wybor typu jak w grze")
                         + "; klucz Workshops Follow Inputs czytany na zywo.");
            }
            catch (Exception e) { Log.Error("WorkshopInputs.ApplyAll", e); }
        }

        /// <summary>Z ArmouryBehavior.OnDailyTick: linia "Warsztaty wedlug surowca (208)" przy wyborach typu od ostatniej linii; zerowanie licznikow.</summary>
        internal static void Daily()
        {
            if (_picks > 0 && On)
                Log.Info("Warsztaty wedlug surowca (208): wybrano typ " + _picks + " razy (przy starcie gry " + _picksStart + ") - typ towarowy z wsadem z okregu albo kontraktu " + _withInput
                         + ", bez wsadu " + _withoutInput + "; miasta bez zadnego typu z wsadem " + _noTypeTowns + (_noTypeNames.Count > 0 ? " [" + string.Join(", ", _noTypeNames.ToArray()) + "]" : "")
                         + "; wag przycietych x0.01 " + _cut + "; istniejace warsztaty i warsztaty gracza nietkniete" + (_stumbles > 0 ? "; potkniecia " + _stumbles : "") + ".");
            _picks = _picksStart = _withInput = _withoutInput = _noTypeTowns = _cut = 0; _noTypeNames.Clear();
        }
    }
}
