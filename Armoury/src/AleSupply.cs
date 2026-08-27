using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// BROWAR MIEJSKI (Jeff 26.08: "nie ma nigdzie alkoholu, zjechalem mase
    /// miast, a daje ogromny minus do morale"). Banner Kings kaze KAZDEJ partii
    /// w Westeros kupowac alkohol na zapas (PartySupplies.BuyItems: piwo, wino,
    /// miod - 10 dni zapasu na glowe), wiec setki lordow ogolacaja targi
    /// z vanillowej podazy szybciej, niz ta sie odradza. Kara morale zostaje,
    /// kupic nie ma czego. Tu: kazde MIASTO co dzien dowarza piwo i wino
    /// do progu (ograniczone dzienna warka) - podaz zyje, mechanika BK dalej
    /// dziala, ale rynek przestaje byc pustynia.
    /// </summary>
    internal sealed class AleSupply : CampaignBehaviorBase
    {
        private ItemObject _beer, _wine, _mead;
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
                Log.Info("AleSupply: beer=" + (_beer != null) + " wine=" + (_wine != null) + " mead=" + (_mead != null) + ".");
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
                TopUp(settlement, _mead, Math.Max(0, c.AleSupplyWineFloor / 2), budget);
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
