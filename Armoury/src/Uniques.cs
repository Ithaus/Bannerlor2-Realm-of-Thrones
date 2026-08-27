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

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, _ => Apply());
        }

        public override void SyncData(IDataStore dataStore) { }

        private static void Apply()
        {
            try
            {
                var c = Settings.Current;
                if (c == null || !c.UniqueCrownsEnabled) return;
                Crowns.Clear();

                var fHead = AccessTools.Field(typeof(ArmorComponent), "<HeadArmor>k__BackingField");
                var fMerch = AccessTools.Field(typeof(ItemObject), "<NotMerchandise>k__BackingField");
                if (fHead == null || fMerch == null)
                { Log.Info("Uniques: pola gry nie pasuja - odpuszczam."); return; }

                int armour = Math.Max(0, c.UniqueCrownHeadArmor);
                foreach (var item in MBObjectManager.Instance.GetObjectTypeList<ItemObject>())
                {
                    if (item == null || item.StringId == null || !item.StringId.EndsWith("_crown")) continue;
                    if (item.ItemType != ItemObject.ItemTypeEnum.HeadArmor || item.ArmorComponent == null) continue;
                    int before = item.ArmorComponent.HeadArmor;
                    fHead.SetValue(item.ArmorComponent, armour);
                    fMerch.SetValue(item, true);
                    Crowns.Add(item);
                    Log.Info("Uniques: " + item.StringId + " - pancerz " + before + " -> " + armour + ", poza handlem.");
                }

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
    }
}
