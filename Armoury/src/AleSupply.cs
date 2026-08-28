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
    /// <summary>
    /// MNIEJSZE SAKWY AI (Jeff 28.08: "moze za duzo kupuja?"). Banner Kings
    /// kaze kazdej partii AI trzymac 10 DNI zapasu kazdego dobra (alkohol,
    /// skory, tkaniny, narzedzia...) - setki partii x 10 dni = wykupione targi.
    /// Postfix na PartySupplies.PostInitialize sciaga AI do BkSupplyDaysCap
    /// dni (gracz kupuje recznie - jego nie ruszamy). Popyt maleje u zrodla,
    /// browar i garbarnia lataja reszte od strony podazy.
    /// </summary>
    internal static class BkSupplyTemper
    {
        private static int _tempered;

        public static void PostInitPostfix(object __instance)
        {
            try
            {
                var c = Settings.Current;
                if (c == null || c.BkSupplyDaysCap <= 0) return;
                var tr = HarmonyLib.Traverse.Create(__instance);
                bool auto = false;
                try { auto = tr.Property("AutoBuying").GetValue<bool>(); } catch { }
                if (!auto) return;   // gracz zaopatruje sie sam
                int days = tr.Property("DaysOfProvision").GetValue<int>();
                if (days <= c.BkSupplyDaysCap) return;
                tr.Property("DaysOfProvision").SetValue(c.BkSupplyDaysCap);
                if (++_tempered == 1)
                    Log.Info("BkSupplyTemper: zapasy AI sciete do " + c.BkSupplyDaysCap + " dni (BK chcial " + days + ").");
            }
            catch { }
        }

        /// <summary>
        /// SUFIT NA SZTUKI (Jeff 28.08: "NIE na glowe - po co im SETKI tego?
        /// tyle, ile potrzebuja do napraw"). BK liczy potrzeby per zolnierz
        /// (300 ludzi = 300 stawek dziennie) - czapka na WYNIK kazdego modelu
        /// potrzeb: dzienna potrzeba partii AI nie przekroczy
        /// BkSupplyMaxPieces / BkSupplyDaysCap, wiec CALY zapas nigdy nie
        /// przekroczy BkSupplyMaxPieces sztuk danego dobra. Gracz nietkniety.
        /// </summary>
        public static void NeedCapPostfix(object __0, ref TaleWorlds.CampaignSystem.ExplainedNumber __result)
        {
            try
            {
                var c = Settings.Current;
                if (c == null || c.BkSupplyMaxPieces <= 0) return;
                bool auto = false;
                try { auto = HarmonyLib.Traverse.Create(__0).Property("AutoBuying").GetValue<bool>(); } catch { }
                if (!auto) return;   // gracz kupuje recznie - bez czapki
                float perDay = (float)c.BkSupplyMaxPieces / Math.Max(1, c.BkSupplyDaysCap > 0 ? c.BkSupplyDaysCap : 4);
                __result.LimitMax(perDay);
            }
            catch { }
        }

        internal static void ApplyAll(HarmonyLib.Harmony h)
        {
            try
            {
                var t = QuartermasterLaw.FindType("BannerKings.Behaviours.PartyNeeds.PartySupplies");
                var m = t != null ? HarmonyLib.AccessTools.Method(t, "PostInitialize") : null;
                if (m == null) { Log.Info("BkSupplyTemper: BK PartySupplies nieobecne."); return; }
                h.Patch(m, postfix: new HarmonyLib.HarmonyMethod(typeof(BkSupplyTemper), "PostInitPostfix"));

                // czapka na kazdy model potrzeb BK (Calculate*Need)
                int capped = 0;
                var tModel = QuartermasterLaw.FindType("BannerKings.Models.BKModels.BKPartyNeedsModel");
                if (tModel != null)
                {
                    var capPost = new HarmonyLib.HarmonyMethod(typeof(BkSupplyTemper), "NeedCapPostfix");
                    foreach (var mm in tModel.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly))
                    {
                        if (!mm.Name.StartsWith("Calculate") || !mm.Name.EndsWith("Need")) continue;
                        if (mm.ReturnType != typeof(TaleWorlds.CampaignSystem.ExplainedNumber)) continue;
                        try { h.Patch(mm, postfix: capPost); capped++; } catch { }
                    }
                }
                Log.Info("BkSupplyTemper: sakwy AI ograniczone (dni=" + (Settings.Current != null ? Settings.Current.BkSupplyDaysCap : 4)
                         + ", sufit sztuk=" + (Settings.Current != null ? Settings.Current.BkSupplyMaxPieces : 15)
                         + ", czapka w " + capped + " modelach potrzeb).");
            }
            catch (Exception e) { Log.Error("BkSupplyTemper.ApplyAll", e); }
        }
    }

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
