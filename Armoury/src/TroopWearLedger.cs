using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// ZUZYCIE WOJSKA Z PRZEBIEGU WALKI (wpis 96; Jeff 05.10: "4%, ale czy na sztywno? nie wynika z walki?" -> "tak").
    /// W bitwie rozgrywanej: kazde trafienie w zolnierza partii gracza liczy sie czesci, w ktora weszlo (glowa -> helm,
    /// tulow -> zbroja, rece -> rekawice, nogi -> nogawice, barki -> plaszcz, blok -> tarcza; pocisk liczy sie ulamkiem),
    /// celny cios zolnierza - jego bron biala, strzal - luk/kusza. Po wygranej WearTheTroops zuzywa tyle sztuk danego typu,
    /// ile wynika z liczby trafien x szansa na trafienie (TroopWearPerHit...). Bitwa bez misji (symulacja) - wedlug strat.
    /// </summary>
    internal static class TroopWearLedger
    {
        private static object _mission;
        private static TaleWorlds.CampaignSystem.CampaignTime _last = TaleWorlds.CampaignSystem.CampaignTime.Zero;   // trafienia starsze niz kilka godzin - nie z tej bitwy
        private static readonly Dictionary<ItemObject.ItemTypeEnum, float> _hits = new Dictionary<ItemObject.ItemTypeEnum, float>();

        private static void Touch()
        {
            var m = Mission.Current;
            if (!ReferenceEquals(m, _mission)) { _mission = m; _hits.Clear(); }
        }

        internal static void Add(ItemObject.ItemTypeEnum t, float n)
        {
            try { Touch(); float v; _hits.TryGetValue(t, out v); _hits[t] = v + n; _last = TaleWorlds.CampaignSystem.CampaignTime.Now; } catch { }
        }

        /// <summary>Trafienia z ostatniej misji (null, gdy bitwy nie rozgrywano). Czysci pamiec.</summary>
        internal static Dictionary<ItemObject.ItemTypeEnum, float> TakeLast()
        {
            bool stale = (TaleWorlds.CampaignSystem.CampaignTime.Now - _last).ToHours > 6.0;   // przegrana bitwa wczesniej - nie liczy sie do tej
            if (_mission == null || _hits.Count == 0 || stale) { _mission = null; _hits.Clear(); return null; }
            var copy = new Dictionary<ItemObject.ItemTypeEnum, float>(_hits);
            _hits.Clear(); _mission = null;   // bez trzymania calej misji w pamieci
            return copy;
        }

        internal static ItemObject.ItemTypeEnum TypeOfSlot(int slot)
        {
            switch (slot)
            {
                case 5: return ItemObject.ItemTypeEnum.HeadArmor;
                case 7: return ItemObject.ItemTypeEnum.LegArmor;
                case 8: return ItemObject.ItemTypeEnum.HandArmor;
                case 9: return ItemObject.ItemTypeEnum.Cape;
                default: return ItemObject.ItemTypeEnum.BodyArmor;
            }
        }
    }
}
