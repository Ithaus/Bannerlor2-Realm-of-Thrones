using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// BROWAR I GARBARNIA MIEJSKA (Jeff 26.08: "nie ma nigdzie alkoholu";
    /// 28.08: "nigdzie nie ma skory - na Polnocy zwierzyny i lasow w brod,
    /// leather powinno byc powszednie"). Banner Kings kaze KAZDEJ partii
    /// kupowac zapasy (alkohol, skory do napraw, tkaniny - 10 dni na glowe),
    /// wiec setki lordow ogolacaja targi z vanillowej podazy szybciej, niz ta
    /// sie odradza. Tu: kazde MIASTO co dzien dowarza piwo i wino oraz
    /// wyprawia skory do progu (ograniczone dzienna praca) - podaz zyje,
    /// mechanika BK dalej dziala, ale rynek przestaje byc pustynia.
    /// </summary>
    internal sealed class AleSupply : CampaignBehaviorBase
    {
        private ItemObject _beer, _wine, _mead, _leather, _hides;
        private bool _resolved;
        private int _sessionAdded;

        public override void RegisterEvents()
        {
            CampaignEvents.DailyTickSettlementEvent.AddNonSerializedListener(this, OnDailySettlement);
        }

        public override void SyncData(IDataStore dataStore) { }

        private void ResolveItems()
        {
            if (_resolved) return;
            _resolved = true;
            try
            {
                _beer = MBObjectManager.Instance.GetObject<ItemObject>("beer");
                _wine = MBObjectManager.Instance.GetObject<ItemObject>("wine");
                _mead = MBObjectManager.Instance.GetObject<ItemObject>("mead");
                _leather = MBObjectManager.Instance.GetObject<ItemObject>("leather");
                _hides = MBObjectManager.Instance.GetObject<ItemObject>("hides");
                Log.Info("AleSupply: beer=" + (_beer != null) + " wine=" + (_wine != null) + " mead=" + (_mead != null)
                         + " leather=" + (_leather != null) + " hides=" + (_hides != null) + ".");
            }
            catch (Exception e) { Log.Error("AleSupply.Resolve", e); }
        }

        private void OnDailySettlement(Settlement settlement)
        {
            try
            {
                var c = Settings.Current;
                if (c == null || !c.AleSupplyEnabled) return;
                if (settlement == null || !settlement.IsTown || settlement.Town == null) return;
                ResolveItems();

                int budget = Math.Max(1, c.AleSupplyPerDay);
                budget -= TopUp(settlement, _beer, Math.Max(0, c.AleSupplyBeerFloor), budget);
                budget -= TopUp(settlement, _wine, Math.Max(0, c.AleSupplyWineFloor), budget);
                // miod pitny (item Banner Kings, jesli istnieje) - skromniej, po polnocnemu
                budget -= TopUp(settlement, _mead, Math.Max(0, c.AleSupplyWineFloor / 2), budget);
                // garbarnia: skory wyprawione do progu (Jeff 28.08: "nigdzie nie
                // ma skory") - osobna dzienna praca, zeby piwo jej nie zjadalo
                int tan = Math.Max(1, c.AleSupplyPerDay);
                tan -= TopUp(settlement, _leather, Math.Max(0, c.TannerySupplyLeatherFloor), tan);
                TopUp(settlement, _hides, Math.Max(0, c.TannerySupplyLeatherFloor / 2), tan);
            }
            catch (Exception e) { Log.Error("AleSupply.Daily", e); }
        }

        /// <summary>Dosypka do progu, w granicach dziennej warki. Zwraca ile weszlo.</summary>
        private int TopUp(Settlement settlement, ItemObject item, int floor, int budget)
        {
            if (item == null || floor <= 0 || budget <= 0) return 0;
            try
            {
                var shelf = settlement.ItemRoster;
                int have = shelf.GetItemNumber(item);
                if (have >= floor) return 0;
                int add = Math.Min(floor - have, budget);
                if (add <= 0) return 0;
                shelf.AddToCounts(item, add);
                _sessionAdded += add;
                if (_sessionAdded <= add || _sessionAdded % 500 < add)
                    Log.Info("AleSupply: " + settlement.Name + " dowarzyl " + add + "x " + item.StringId
                             + " (lacznie w sesji " + _sessionAdded + ").");
                return add;
            }
            catch { return 0; }
        }
    }
}
