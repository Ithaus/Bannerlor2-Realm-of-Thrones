using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// 177-1 STAL ZAMKOWA (Jeff 09.10, decyzja 24a - TAK; docs/PROJEKT-177-STAL-VALYRIANSKA-2026-10-09.md rozdz. 2-3).
    /// Kanon: sztuka wytapiania stali valyrianskiej zginela z Valyria. Najwyzszy metal kuzni (wanilia "Thamaskene Steel",
    /// CraftingMaterials.Iron6; nazwe "Valyrian Steel" nadawal mu DOTAD nasz ValyrianSteel.Rename) to stal z kuzni zamkowych:
    ///  - nazwa "Castle-forged Steel" (termin AWOIAF), opis perku Steel Maker 3 (oba pola: podpowiedz i okno wyboru perku),
    ///    "Thamaskene" w nazwach przedmiotow i czesci kuzni -> "Castle-forged" (slowo nic nie znaczy w Westeros);
    ///  - zwykly stopien lancucha: bez dawnego podwojnego wsadu (DearRefine), przetop pancerzy bez dodatkowego "/2"
    ///    (Recipes.SmeltYield), cena HistCastleSteelPerKg 12 d/kg (sztaba 6 d) / CastleSteelValue 375;
    ///  - jedna regula tieru 6 (ArmsPricing.GradeFor t6 -> Iron6, wylacznik Tier6CastleSteel);
    ///  - wedrowcy BK (PopulationPartyComponent.GiveItems) nie przynosza sztab Iron1-6 ani wegla z niczego (TravellersNoIngots).
    ///    Budzet ladunku wedrowca BK to suma WARTOSCI (BK GiveItems: num2 = ludzie x 100/30/300, losowanie materialu wazone Value);
    ///    przy sztabie 6 d zamiast 100 d petla nie konczylaby sie budzetem i wedrowcy niesliby wiecej rudy, drewna i towarow
    ///    z niczego (krytyka 177, pkt 8). Dlatego NA CZAS GiveItems sztaba 6 ma swoja dawna wartosc (pole, bez settera - blokada
    ///    cen HistoricalPrices i TotalValue rosteru nie widza zmiany, bo zdjecie sztab idzie tez przy dawnej wartosci), a potem
    ///    wszystkie sztaby i wegiel schodza z ladunku. Losowanie i ilosc pozostalego towaru - dokladnie jak przed 177.
    /// </summary>
    internal static class CastleSteel
    {
        internal const string BarName = "Castle-forged Steel";
        // dawna cena sztaby 6 (przed 177): HistValyrianPerKg 200 d/kg x 0.5 kg = 100 d (ceny historyczne), ValyrianSteelValue 1000 (bez nich).
        // Tylko kotwica: budzet ladunku wedrowcow BK (wyzej) i przelicznik popytu kategorii "iron" (HistoricalPrices) - 177 nie przesuwa ani
        // ilosci towaru wedrowcow, ani popytu miast na rude i sztaby (krytyka 177 pkt 8).
        private const float LegacyPerKg = 200f;
        private const int LegacyNoHist = 1000;

        private static readonly FieldInfo FValue = AccessTools.Field(typeof(ItemObject), "<Value>k__BackingField");

        // licznik doby (linia "Wedrowcy BK")
        private static int _dParties, _dBars, _dBars6, _dCoal, _stumbles;
        private static ItemObject _i6;
        private static int _swapWas = -1;   // wartosc sztaby 6 przed podmiana na czas GiveItems (-1 = brak podmiany)

        private static bool IsIngot6(ItemObject it)
        {
            if (it == null) return false;
            if (_i6 == null) _i6 = Recipes.MaterialItem(CraftingMaterials.Iron6);
            return it == _i6;
        }

        /// <summary>Dawna (sprzed 177) wartosc sztaby 6 - kotwica budzetu wedrowcow i przelicznika kategorii; dla innych przedmiotow - Value.</summary>
        internal static int LegacyValue(ItemObject it)
        {
            if (it == null) return 0;
            if (!IsIngot6(it)) return it.Value;
            if (HistoricalPrices.On) return Math.Max(1, (int)Math.Round(Math.Max(0.5f, it.Weight) * LegacyPerKg));
            if (MaterialLaw.On) return LegacyNoHist;
            return MaterialLaw.Orig(it);
        }

        /// <summary>Wartosc do przelicznika popytu kategorii (HistoricalPrices krok 3 i kategorie mieszane): sztaba 6 po dawnej cenie.</summary>
        internal static int RatioValue(ItemObject it)
        {
            return it != null && IsIngot6(it) ? LegacyValue(it) : (it != null ? it.Value : 0);
        }

        // ------------------------------------------------------------ nazwy (start sesji)
        internal static void Rename()
        {
            try
            {
                var it = Recipes.MaterialItem(CraftingMaterials.Iron6);
                if (it == null) { Log.Info("CastleSteel: brak itemu Iron6 - nazwa zostaje."); return; }
                var cur = it.Name != null ? it.Name.ToString() : "";
                var setter = AccessTools.PropertySetter(typeof(ItemObject), "Name");
                if (setter == null) { Log.Info("CastleSteel: ItemObject.Name bez settera - nazwa zostaje."); return; }
                if (cur != BarName)
                    setter.Invoke(it, new object[] { new TextObject("{=!}" + BarName + "{@Plural}bars of castle-forged steel{\\@}", null) });
                int perk = PerkText();
                int items = 0, pieces = 0;
                foreach (var x in MBObjectManager.Instance.GetObjectTypeList<ItemObject>())
                {
                    if (x == null || x.Name == null || x.IsCraftedByPlayer) continue;   // wyroby gracza maja nazwe z zapisu - nie ruszamy
                    string n = x.Name.ToString(), m = Westeros(n);
                    if (m == n) continue;
                    setter.Invoke(x, new object[] { new TextObject("{=!}" + m, null) });
                    items++;
                }
                var pSet = AccessTools.PropertySetter(typeof(CraftingPiece), "Name");
                if (pSet != null)
                    foreach (var p in MBObjectManager.Instance.GetObjectTypeList<CraftingPiece>())
                    {
                        if (p == null || p.Name == null) continue;
                        string n = p.Name.ToString(), m = Westeros(n);
                        if (m == n) continue;
                        pSet.Invoke(p, new object[] { new TextObject("{=!}" + m, null) });
                        pieces++;
                    }
                Log.Info("CastleSteel: '" + cur + "' przemianowana na '" + BarName + "'; opis Steel Maker 3 " + (perk == 2 ? "(podpowiedz i okno wyboru perku)" : perk == 1 ? "TYLKO podpowiedz (brak pola opisu)" : "BEZ ZMIAN (brak perku/pol)")
                         + "; 'Thamaskene' w nazwach: " + items + " przedmiotow, " + pieces + " czesci kuzni.");
            }
            catch (Exception e) { Log.Error("CastleSteel.Rename", e); }
        }

        /// <summary>"Thamaskene Steel X" -> "Castle-forged Steel X", "Thamaskene X" -> "Castle-forged X" (wielkosc liter jak w zrodle).</summary>
        private static string Westeros(string n)
        {
            if (string.IsNullOrEmpty(n) || n.IndexOf("hamaskene", StringComparison.OrdinalIgnoreCase) < 0) return n;
            return n.Replace("Thamaskene", "Castle-forged").Replace("thamaskene", "castle-forged");
        }

        /// <summary>Perk Steel Maker 3: wanilia pisze o "Thamaskene steel" (i o wsadzie, ktorego u nas nie ma). PrimaryDescription to
        /// podpowiedz (CampaignUIHelper), a okno wyboru perku czyta Description zlozone przy Initialize (PropertyObject._description) -
        /// ustawiamy oba (krytyka 177 pkt 9). Zwraca 2 = oba pola, 1 = tylko podpowiedz, 0 = nic.</summary>
        private static int PerkText()
        {
            try
            {
                var perk = DefaultPerks.Crafting.SteelMaker3;
                if (perk == null) return 0;
                var text = "You can refine fine steel into castle-forged steel, the finest steel the castle forges make.";
                int done = 0;
                var setPrim = AccessTools.PropertySetter(typeof(PerkObject), "PrimaryDescription");
                if (setPrim != null) { setPrim.Invoke(perk, new object[] { new TextObject("{=!}" + text, null) }); done = 1; }
                var fDesc = AccessTools.Field(typeof(PropertyObject), "_description");
                if (fDesc != null) { fDesc.SetValue(perk, new TextObject("{=!}" + text, null)); done = done == 1 ? 2 : 1; }
                return done;
            }
            catch (Exception e) { Log.Error("CastleSteel.PerkText", e); return 0; }
        }

        // ------------------------------------------------------------ wedrowcy BK bez sztab i wegla (177-1, pkt 4 decyzji 24a)
        internal static void ApplyAll(Harmony h)
        {
            try
            {
                var t = AccessTools.TypeByName("BannerKings.Components.PopulationPartyComponent");
                var give = t != null ? AccessTools.Method(t, "GiveItems") : null;
                var create = t != null ? AccessTools.Method(t, "CreateTravellerParty") : null;
                if (give == null) { Log.Info("CastleSteel: BK PopulationPartyComponent.GiveItems nieznaleziona - wedrowcy BK bez zmian."); return; }
                h.Patch(give, prefix: new HarmonyMethod(typeof(CastleSteel), nameof(GivePrefix)),
                              postfix: new HarmonyMethod(typeof(CastleSteel), nameof(GivePostfix)),
                              finalizer: new HarmonyMethod(typeof(CastleSteel), nameof(GiveFinalizer)));
                if (create != null) h.Patch(create, postfix: new HarmonyMethod(typeof(CastleSteel), nameof(CreatePostfix)));
                Log.Info("CastleSteel: wedrowcy BK (GiveItems" + (create != null ? " + CreateTravellerParty" : "") + ") - sztaby i wegiel z niczego "
                         + (Settings.Current.TravellersNoIngots ? "ZDEJMOWANE (budzet ladunku liczony po dawnej cenie sztaby 6)" : "bez zmian (TravellersNoIngots wylaczone)") + ".");
            }
            catch (Exception e) { Log.Error("CastleSteel.ApplyAll", e); }
        }

        public static void GivePrefix()
        {
            try
            {
                if (!Settings.Current.TravellersNoIngots || FValue == null) return;
                var it = Recipes.MaterialItem(CraftingMaterials.Iron6);
                if (it == null) return;
                int legacy = LegacyValue(it);
                if (legacy == it.Value) return;
                _swapWas = it.Value;
                FValue.SetValue(it, legacy);   // pole, nie setter: blokada cen nie loguje "inny mod zmienil cene"
            }
            catch { _swapWas = -1; }
        }

        /// <summary>Sztaby i wegiel z ladunku precz - JESZCZE przy dawnej wartosci sztaby 6 (TotalValue rosteru wraca dokladnie do stanu bez nich).</summary>
        public static void GivePostfix(ref MobileParty party)
        {
            try
            {
                if (!Settings.Current.TravellersNoIngots || party == null || party.ItemRoster == null) return;
                Strip(party.ItemRoster);
            }
            catch (Exception e) { if (_stumbles++ < 1) Log.Error("CastleSteel.GivePostfix", e); }
        }

        public static void GiveFinalizer(Exception __exception)
        {
            try
            {
                if (_swapWas >= 0 && FValue != null)
                {
                    var it = Recipes.MaterialItem(CraftingMaterials.Iron6);
                    if (it != null) FValue.SetValue(it, _swapWas);
                }
            }
            catch { }
            _swapWas = -1;
        }

        public static void CreatePostfix(MobileParty __result)
        {
            if (__result != null && Settings.Current.TravellersNoIngots) _dParties++;
        }

        private static void Strip(TaleWorlds.CampaignSystem.Roster.ItemRoster roster)
        {
            for (int i = roster.Count - 1; i >= 0; i--)
            {
                var el = roster.GetElementCopyAtIndex(i);
                var it = el.EquipmentElement.Item;
                if (it == null || el.Amount <= 0) continue;
                bool coal = it == Recipes.MaterialItem(CraftingMaterials.Charcoal);
                bool bar = !coal && IsBar(it);
                if (!coal && !bar) continue;
                roster.AddToCounts(el.EquipmentElement, -el.Amount);
                if (coal) _dCoal += el.Amount;
                else { _dBars += el.Amount; if (IsIngot6(it)) _dBars6 += el.Amount; }
            }
        }

        private static bool IsBar(ItemObject it)
        {
            return it == Recipes.MaterialItem(CraftingMaterials.Iron1) || it == Recipes.MaterialItem(CraftingMaterials.Iron2)
                || it == Recipes.MaterialItem(CraftingMaterials.Iron3) || it == Recipes.MaterialItem(CraftingMaterials.Iron4)
                || it == Recipes.MaterialItem(CraftingMaterials.Iron5) || IsIngot6(it);
        }

        internal static void Daily()
        {
            try
            {
                if (!Settings.Current.TravellersNoIngots) return;
                var it = Recipes.MaterialItem(CraftingMaterials.Iron6);
                Log.Info("Wedrowcy BK: dzien " + (int)CampaignTime.Now.ToDays + " - nowych partii " + _dParties + ", zdjeto z ladunku sztab " + _dBars
                         + " (w tym " + BarName + " " + _dBars6 + ") i wegla " + _dCoal + " (sztaby tylko z wytopu; budzet ladunku liczony po dawnej cenie sztaby 6 = "
                         + LegacyValue(it) + " d, dzis " + (it != null ? it.Value : 0) + " d)" + (_stumbles > 0 ? "; potkniecia " + _stumbles : "") + ".");
            }
            catch (Exception e) { Log.Error("CastleSteel.Daily", e); }
            _dParties = _dBars = _dBars6 = _dCoal = 0;
        }

        internal static void Reset() { _dParties = _dBars = _dBars6 = _dCoal = 0; _stumbles = 0; _i6 = null; _swapWas = -1; }
    }
}
