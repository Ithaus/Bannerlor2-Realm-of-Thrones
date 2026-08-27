using System;
using System.Collections.Generic;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// KLEJNOTY KORONNE (Jeff 26.08: "kupilem korone Sansy - takie rzeczy musza
    /// byc unikatowe, i czemu ona daje tyle pancerza, to przeciez tylko korona!").
    /// ROT daje KAZDEJ koronie head_armor=75 (jak pelny helm) i 7 z 8 koron
    /// zostawia jako zwykly towar (tylko cersei_crown ma is_merchandise=false),
    /// wiec targi sprzedaja korony krolowych jak kapelusze. Nazwane miecze
    /// (Longclaw, Ice, Oathkeeper...) ROT juz trzyma poza handlem - korony nie.
    /// Tu, raz na start sesji:
    ///   1. kazda korona (id konczace sie "_crown") dostaje pancerz glowy
    ///      UniqueCrownHeadArmor (domyslnie 10 - ozdoba, nie helm),
    ///   2. kazda korona przestaje byc towarem (NotMerchandise) - znika
    ///      z przyszlego zaopatrzenia sklepow, a przez regule Teachable
    ///      (pancerz spoza kramow to legenda) NIE DA sie jej wykuc ani
    ///      nauczyc jej wzoru w kuzni,
    ///   3. korony zalegajace na targach miast sa zdejmowane z polek -
    ///      egzemplarze w rekach bohaterow (kupiona korona Sansy Jeffa)
    ///      zostaja JEDYNYMI na swiecie.
    /// </summary>
    internal sealed class Uniques : CampaignBehaviorBase
    {
        private static readonly List<ItemObject> Crowns = new List<ItemObject>();
        // jednorazowy zwrot za korone Sansy - flaga jedzie w save, zeby przy
        // ponownym zdobyciu korony (lup z lorda) nikt jej po cichu nie sprzedal
        private static bool _sansaRefunded;

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, _ => Apply());
        }

        public override void SyncData(IDataStore dataStore)
        {
            bool v = _sansaRefunded;
            dataStore.SyncData("armouryUniquesSansaRefunded", ref v);
            _sansaRefunded = v;
        }

        private static void Apply()
        {
            try
            {
                var c = Settings.Current;
                if (c == null || !c.UniqueCrownsEnabled) return;
                Crowns.Clear();

                var fHead = AccessTools.Field(typeof(ArmorComponent), "<HeadArmor>k__BackingField");
                var fBody = AccessTools.Field(typeof(ArmorComponent), "<BodyArmor>k__BackingField");
                var fLeg = AccessTools.Field(typeof(ArmorComponent), "<LegArmor>k__BackingField");
                var fArm = AccessTools.Field(typeof(ArmorComponent), "<ArmArmor>k__BackingField");
                var fMerch = AccessTools.Field(typeof(ItemObject), "<NotMerchandise>k__BackingField");
                if (fHead == null || fMerch == null)
                { Log.Info("Uniques: pola gry nie pasuja - odpuszczam."); return; }

                int armour = Math.Max(0, c.UniqueCrownHeadArmor);
                int sansaOldValue = 0;
                ItemObject sansa = null;
                foreach (var item in MBObjectManager.Instance.GetObjectTypeList<ItemObject>())
                {
                    if (item == null || item.StringId == null || !item.StringId.EndsWith("_crown")) continue;
                    if (item.ItemType != ItemObject.ItemTypeEnum.HeadArmor || item.ArmorComponent == null) continue;
                    // wartosc SPRZED nerfa - do zwrotu za korone Sansy (Jeff kupil
                    // helm-75 za pelna cene; odczyt tutaj utrwala stara wycene)
                    if (item.StringId == "sansa_crown") { sansa = item; sansaOldValue = item.Value; }
                    int before = item.ArmorComponent.HeadArmor;
                    fHead.SetValue(item.ArmorComponent, armour);
                    // korona nie chroni niczego poza glowa - zero na korpus,
                    // nogi i rece (ROT liczy je jak "plate")
                    if (fBody != null) fBody.SetValue(item.ArmorComponent, 0);
                    if (fLeg != null) fLeg.SetValue(item.ArmorComponent, 0);
                    if (fArm != null) fArm.SetValue(item.ArmorComponent, 0);
                    fMerch.SetValue(item, true);
                    Crowns.Add(item);
                    Log.Info("Uniques: " + item.StringId + " - pancerz " + before + " -> " + armour + ", poza handlem.");
                }

                RefundSansa(sansa, sansaOldValue);

                // zdejmij zalegajace korony z targow miast i zamkow - egzemplarze
                // w rekach bohaterow zostaja jedynymi
                int pulled = 0;
                foreach (var settlement in Settlement.All)
                {
                    if (settlement == null || (!settlement.IsTown && !settlement.IsCastle)) continue;
                    var shelf = settlement.ItemRoster;
                    if (shelf == null) continue;
                    foreach (var crown in Crowns)
                    {
                        int n = shelf.GetItemNumber(crown);
                        if (n > 0) { shelf.AddToCounts(crown, -n); pulled += n; }
                    }
                }
                Log.Info("Uniques: koron " + Crowns.Count + ", zdjeto z polek " + pulled + " szt.");
            }
            catch (Exception e) { Log.Error("Uniques.Apply", e); }
        }

        /// <summary>
        /// JEDNORAZOWY ZWROT (Jeff 26.08: "usun korone Sansy z mojego ekwipunku
        /// i oddaj mi za to kase"). Kupil ja z polki, gdy byla helmem-75
        /// i zwyklym towarem - nie jego blad. Zdejmujemy z sakw I z zalozonych
        /// slotow (bojowych i cywilnych), zwracamy pelna wartosc sprzed nerfa.
        /// Flaga w save: wykonuje sie raz na kampanie.
        /// </summary>
        private static void RefundSansa(ItemObject sansa, int oldValue)
        {
            try
            {
                if (_sansaRefunded || sansa == null || Hero.MainHero == null) return;
                int removed = 0;

                var roster = TaleWorlds.CampaignSystem.Party.MobileParty.MainParty != null
                    ? TaleWorlds.CampaignSystem.Party.MobileParty.MainParty.ItemRoster : null;
                if (roster != null)
                {
                    int n = roster.GetItemNumber(sansa);
                    if (n > 0) { roster.AddToCounts(sansa, -n); removed += n; }
                }

                foreach (var eq in new[] { Hero.MainHero.BattleEquipment, Hero.MainHero.CivilianEquipment })
                {
                    if (eq == null) continue;
                    for (var slot = EquipmentIndex.WeaponItemBeginSlot; slot < EquipmentIndex.NumEquipmentSetSlots; slot++)
                    {
                        try
                        {
                            if (eq[slot].Item == sansa) { eq[slot] = default(EquipmentElement); removed++; }
                        }
                        catch { }
                    }
                }

                _sansaRefunded = true;   // takze gdy korony nie bylo - nie szukamy jej wiecznie
                if (removed > 0)
                {
                    int gold = Math.Max(1, oldValue) * removed;
                    Hero.MainHero.ChangeHeroGold(gold);
                    TaleWorlds.Library.InformationManager.DisplayMessage(new TaleWorlds.Library.InformationMessage(
                        "Sansa's Crown returns to the realm - " + gold + " denars refunded.",
                        TaleWorlds.Library.Colors.Green));
                    Log.Info("Uniques: korona Sansy zdjeta z ekwipunku (" + removed + " szt.), zwrot " + gold + " zlota (stara wycena " + oldValue + ").");
                }
                else Log.Info("Uniques: korony Sansy nie ma w ekwipunku gracza - zwrot nieaktywny.");
            }
            catch (Exception e) { Log.Error("Uniques.RefundSansa", e); }
        }
    }
}
