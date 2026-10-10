using System;
using System.Collections.Generic;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// 3.6 (etap 3 planu do konca moda, 10.10) WEDROWCY BK BEZ TOWARU Z NICZEGO.
    /// BK PopulationPartyComponent.CreateTravellerParty (jedyne wywolanie: BKPartyBehavior.SendTravellerParty, ok. 10 partii na dobe)
    /// wola GiveItems: partia dostaje z niczego towary handlowe (Items.AllTradeGoods wazone 1/Value; szlachta dodatkowo srebro, klejnoty,
    /// przyprawy, aksamit, futra; rzemieslnicy len, welne, skore, garnki, plotno, narzedzia, bawelne), a rzemieslnicy takze materialy kuzni
    /// (ruda, drewno, sztaby 1-6, wegiel - wazone Value). Po dojsciu do miasta BK (AddPopulationPartyBehavior) niszczy partie RAZEM
    /// z ladunkiem - sciezka "Trading" (miasto placi i dostaje towar na polke) jest martwa, nikt nie podaje trading=true. Towar rodzi sie
    /// wiec z niczego i ginie w nicosc; do swiata trafia tylko jako lup z rozbitych wedrowcow.
    /// 177-1 (CastleSteel, TravellersNoIngots) zdejmuje juz sztaby i wegiel. Ten krok zdejmuje RESZTE tego, co dodal GiveItems (towary
    /// handlowe, ruda, drewno): roznica rosteru przed i po GiveItems, co do sztuki. Zostaje to, co partia miala przed GiveItems - zywnosc
    /// (BK GiveFood) i zwierzeta juczne (BK GivePackAnimals): zapasy drogi, liczone w linii osobno (poza tym krokiem).
    /// Postfiks z Priority.Low biegnie PO CastleSteel.GivePostfix (tamten liczy sztaby do linii "Wedrowcy BK"), przed finalizerem CastleSteel.
    /// Ksiega towarow (GoodsLedger): dodanie i zdjecie w tej samej ramce dobowego ticku osady znosza sie do zera - pozycje
    /// "inne ticki dobowe (osady)" i "przepadlo z rozbitymi partiami (inne partie)" maleja o ten towar; ksiega pieniadza bez zmian (towar, nie zloto).
    /// </summary>
    internal static class TravellerGoods36
    {
        // licznik doby (linia "Wedrowcy BK - towar (3.6)")
        private static int _dParties, _dTrade, _dOre, _dWood, _dBars, _dStumbles, _stumbles;
        private static long _dTradeV, _dOreV, _dWoodV, _dBarsV;
        private static long _leftFood, _leftFoodV, _leftAnimals, _leftAnimalsV, _leftOther, _leftOtherV;
        private static object _camp;   // kampania, ktorej dotycza liczniki (inna kampania = liczniki od zera)

        internal static void ApplyAll(Harmony h)
        {
            try
            {
                var t = AccessTools.TypeByName("BannerKings.Components.PopulationPartyComponent");
                var give = t != null ? AccessTools.Method(t, "GiveItems") : null;
                if (give == null) { Log.Info("TravellerGoods36: BK PopulationPartyComponent.GiveItems nieznaleziona - towar wedrowcow BK bez zmian."); return; }
                h.Patch(give, prefix: new HarmonyMethod(typeof(TravellerGoods36), nameof(Pre)),
                              postfix: new HarmonyMethod(typeof(TravellerGoods36), nameof(Post)) { priority = Priority.Low });
                Log.Info("TravellerGoods36: wedrowcy BK (GiveItems) - towary handlowe, ruda i drewno nadane z niczego "
                         + (Settings.Current.TravellersNoGoods ? "ZDEJMOWANE (zostaja zapasy drogi: zywnosc i zwierzeta juczne)" : "bez zmian (Travellers No Goods wylaczone)") + ".");
            }
            catch (Exception e) { Log.Error("TravellerGoods36.ApplyAll", e); }
        }

        /// <summary>Stan rosteru przed GiveItems (zywnosc z GiveFood, zwierzeta juczne z GivePackAnimals).</summary>
        public static void Pre(ref MobileParty party, out Dictionary<ItemObject, int> __state)
        {
            __state = null;
            try
            {
                if (Settings.Current == null || !Settings.Current.TravellersNoGoods || party == null || party.ItemRoster == null) return;
                __state = Count(party.ItemRoster);
            }
            catch (Exception e) { __state = null; Stumble("Pre", e); }
        }

        /// <summary>Wszystko, co GiveItems dodalo ponad stan sprzed wywolania, schodzi z ladunku (GiveItems dodaje tylko sztuki bez modyfikatora).</summary>
        public static void Post(ref MobileParty party, Dictionary<ItemObject, int> __state)
        {
            if (__state == null) return;
            try
            {
                if (party == null || party.ItemRoster == null) return;
                SameCampaign();
                var r = party.ItemRoster;
                var after = Count(r);
                var ore = Recipes.MaterialItem(CraftingMaterials.IronOre);
                var wood = Recipes.MaterialItem(CraftingMaterials.Wood);
                foreach (var kv in after)
                {
                    int before;
                    __state.TryGetValue(kv.Key, out before);
                    int add = kv.Value - before;
                    if (add <= 0) continue;
                    var it = kv.Key;
                    long v = (long)it.Value * add;
                    r.AddToCounts(it, -add);
                    if (it == ore) { _dOre += add; _dOreV += v; }
                    else if (it == wood) { _dWood += add; _dWoodV += v; }
                    else if (IsBarOrCoal(it)) { _dBars += add; _dBarsV += v; }
                    else { _dTrade += add; _dTradeV += v; }
                }
                _dParties++;
                // co zostaje z niczego: zapasy drogi BK (GiveFood, GivePackAnimals) - tylko licznik
                for (int i = 0; i < r.Count; i++)
                {
                    var el = r.GetElementCopyAtIndex(i);
                    var it = el.EquipmentElement.Item;
                    if (it == null || el.Amount <= 0) continue;
                    long v = (long)it.Value * el.Amount;
                    if (it.IsFood) { _leftFood += el.Amount; _leftFoodV += v; }
                    else if (it.ItemCategory == DefaultItemCategories.PackAnimal || it.HasHorseComponent) { _leftAnimals += el.Amount; _leftAnimalsV += v; }
                    else { _leftOther += el.Amount; _leftOtherV += v; }
                }
            }
            catch (Exception e) { Stumble("Post", e); }
        }

        private static Dictionary<ItemObject, int> Count(ItemRoster r)
        {
            var d = new Dictionary<ItemObject, int>();
            for (int i = 0; i < r.Count; i++)
            {
                var el = r.GetElementCopyAtIndex(i);
                var it = el.EquipmentElement.Item;
                if (it == null || el.Amount <= 0) continue;
                int n;
                d.TryGetValue(it, out n);
                d[it] = n + el.Amount;
            }
            return d;
        }

        private static bool IsBarOrCoal(ItemObject it)
        {
            return it == Recipes.MaterialItem(CraftingMaterials.Charcoal)
                || it == Recipes.MaterialItem(CraftingMaterials.Iron1) || it == Recipes.MaterialItem(CraftingMaterials.Iron2)
                || it == Recipes.MaterialItem(CraftingMaterials.Iron3) || it == Recipes.MaterialItem(CraftingMaterials.Iron4)
                || it == Recipes.MaterialItem(CraftingMaterials.Iron5) || it == Recipes.MaterialItem(CraftingMaterials.Iron6);
        }

        private static void Stumble(string where, Exception e)
        {
            _dStumbles++;
            if (_stumbles++ < 3) Log.Error("TravellerGoods36." + where, e);
        }

        /// <summary>Liczniki z innej kampanii (wczytanie innego zapisu w tej samej sesji gry) nie przechodza do nowej.</summary>
        private static void SameCampaign()
        {
            var c = Campaign.Current;
            if (ReferenceEquals(_camp, c)) return;
            Clear();
            _camp = c;
        }

        private static void Clear()
        {
            _dParties = _dTrade = _dOre = _dWood = _dBars = _dStumbles = 0;
            _dTradeV = _dOreV = _dWoodV = _dBarsV = 0;
            _leftFood = _leftFoodV = _leftAnimals = _leftAnimalsV = _leftOther = _leftOtherV = 0;
        }

        internal static void Daily()
        {
            try
            {
                SameCampaign();
                if (Settings.Current == null || !Settings.Current.TravellersNoGoods) return;
                long pieces = _dTrade + _dOre + _dWood + _dBars, value = _dTradeV + _dOreV + _dWoodV + _dBarsV;
                Log.Info("Wedrowcy BK - towar (3.6): dzien " + (int)CampaignTime.Now.ToDays + " - nowych partii z ladunkiem BK " + _dParties
                         + "; zdjeto towaru nadanego z niczego " + pieces + " szt. za " + value + " zl (wartosc gry): towary handlowe " + _dTrade + " szt. / " + _dTradeV
                         + " zl, ruda " + _dOre + " / " + _dOreV + " zl, drewno " + _dWood + " / " + _dWoodV + " zl"
                         + (_dBars > 0 ? ", sztaby i wegiel " + _dBars + " / " + _dBarsV + " zl" : "")
                         + " - nie rodzi sie i nie ginie z partia u celu; zostaje z niczego (zapasy drogi BK, poza krokiem 3.6): zywnosc " + _leftFood + " szt. / " + _leftFoodV
                         + " zl, zwierzeta juczne " + _leftAnimals + " / " + _leftAnimalsV + " zl"
                         + (_leftOther > 0 ? ", inne " + _leftOther + " / " + _leftOtherV + " zl" : "")
                         + (_dStumbles > 0 ? "; potkniecia " + _dStumbles + " (od wczytania " + _stumbles + ")" : "") + ".");
            }
            catch (Exception e) { Log.Error("TravellerGoods36.Daily", e); }
            Clear();
        }
    }
}
