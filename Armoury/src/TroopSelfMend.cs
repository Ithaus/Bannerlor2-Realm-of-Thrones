using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// WOJSKO SAMO LATA SWOJ SPRZET (Jeff 29.08: "dostaja zold i czesc lupow,
    /// niech za to naprawiaja; jak im nie starczy, to ja moge"). Kazdego dnia
    /// w MIESCIE (jest kowal) zolnierze oddaja do naprawy najgorsze sztuki
    /// z magazynu kwatermistrza - placa ze swojego zoldu, gracz nie wydaje
    /// ani grosza. Po przerobce 30.08 (Jeff: "10 szt./dzien to bez sensu,
    /// niech naprawiaja PROCENT uszkodzen") dzienna robota to PROCENT calej
    /// puli zuzytych sztuk (min. 3, zeby ogon nie wisial wiecznie) - pelny
    /// remont trwa ~100/procent dni postoju NIEZALEZNIE od wielkosci armii.
    /// Kto sie spieszy, placi kowalowi (Send the men's worn gear...).
    /// </summary>
    internal static class TroopSelfMend
    {
        // ------------------------------------------------------------ wpis 84: godzinowo, z sakiewki ludzi
        // Jeff 05.10: "maja kase, to niech naprawiaja, na ile ich stac; a co jak wyjde z miasta?". Ludzie oddaja kowalom miasta
        // najgorsze sztuki; kowale maja tyle godzin, ile rak (rece rzemieslnikow x udzial platnerzy i miecznikow), w nocy spia.
        // Gotowa sztuka - zaplacona z sakiewki ludzi do kasy miasta. Wyjazd: to, co na warsztacie, ludzie zabieraja nienaprawione
        // i nikt za to nie placi (postep sztuki przepada, nic wiecej). Wraki (<10%) - tylko kowal z materialem albo przetop.
        private static float _bench;
        private static string _benchTown;

        internal static void LeftTown() { _bench = 0f; _benchTown = null; }

        private static int UnitCost(EquipmentElement ee)
        {
            return Math.Max(1, (int)(ee.Item.Value * (1f - ee.ItemModifier.PriceMultiplier) * 0.25f));
        }

        private static bool Mendable(ItemRosterElement el)
        {
            var mod = el.EquipmentElement.ItemModifier;
            return el.Amount > 0 && el.EquipmentElement.Item != null && mod != null && mod.PriceMultiplier < 1f && mod.PriceMultiplier >= 0.1f;
        }

        /// <summary>Ile kosztowalyby wszystkie zalegle naprawy (bez wrakow) - tyle ludzie trzymaja w sakiewce.</summary>
        internal static int OutstandingCost()
        {
            int sum = 0;
            try
            {
                var armory = QuartermasterLaw.DteArmory();
                if (armory == null) return 0;
                for (int i = 0; i < armory.Count; i++)
                {
                    var el = armory.GetElementCopyAtIndex(i);
                    if (Mendable(el)) sum += UnitCost(el.EquipmentElement) * el.Amount;
                }
            }
            catch { }
            return sum;
        }

        internal static void Hourly()
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !s.TroopSelfMendEnabled || !MenPurse.On) return;
                var main = MobileParty.MainParty;
                var st = main != null ? main.CurrentSettlement : null;
                if (st == null || !st.IsTown || st.Town == null) { _bench = 0f; _benchTown = null; return; }
                if (_benchTown != st.StringId) { _bench = 0f; _benchTown = st.StringId; }
                if (s.WorkshopNightRest) { int hh = TaleWorlds.CampaignSystem.CampaignTime.Now.GetHourOfDay; if (hh >= 23 || hh < 5) return; }
                var armory = QuartermasterLaw.DteArmory();
                if (armory == null) return;
                var worn = new List<ItemRosterElement>();
                for (int i = 0; i < armory.Count; i++) { var el = armory.GetElementCopyAtIndex(i); if (Mendable(el)) worn.Add(el); }
                if (worn.Count == 0) { _bench = 0f; return; }
                worn.Sort((a, b) => a.EquipmentElement.ItemModifier.PriceMultiplier.CompareTo(b.EquipmentElement.ItemModifier.PriceMultiplier));
                // godziny kowali na godzine: rece miasta x (platnerze + miecznicy) / wszystkie cechy
                // wpis 91: godziny z WSPOLNEJ puli kowali miasta (dzienny zapas rozlozony na 18 godzin pracy)
                float hourShare = Math.Min(SmithHours.Available(st.Town), SmithHours.Capacity(st.Town) / Math.Max(1f, s.WorkHoursPerManDay));   // wpis 93: dzien pracy kowala rozlozony na jego godziny
                _bench += hourShare;
                float per = Math.Max(0.05f, s.MendLootHoursPerPiece);
                int mended = 0, paid = 0;
                bool broke = false;
                foreach (var el in worn)
                {
                    int left = el.Amount, fixedN = 0;
                    while (left > 0 && _bench >= per)
                    {
                        int unit = UnitCost(el.EquipmentElement);
                        if (MenPurse.Get(main) < unit) { broke = true; break; }
                        MenPurse.Take(main, unit); st.Town.ChangeGold(unit);
                        _bench -= per; left--; fixedN++; paid += unit;
                    }
                    if (fixedN > 0)
                    {
                        armory.AddToCounts(el.EquipmentElement, -fixedN);
                        armory.AddToCounts(new EquipmentElement(el.EquipmentElement.Item), fixedN);
                        mended += fixedN;
                    }
                    if (broke || _bench < per) break;
                }
                if (broke) _bench = Math.Min(_bench, per);   // nie ma czym zaplacic - kowale nie trzymaja godzin na zapas
                SmithHours.Use(st.Town, broke ? mended * per : hourShare);   // godziny kowali tej godziny (bez zaplaty - tylko gotowe)
                _hourMended += mended; _hourPaid += paid;
                if (_hourMended > 0 && (TaleWorlds.CampaignSystem.CampaignTime.Now.GetHourOfDay == 22 || broke))
                {
                    Log.Player("The smiths of " + st.Name + " mended " + _hourMended + " pieces of your men's kit today for " + _hourPaid
                               + " denars from the men's purse." + (broke ? " The men's purse is empty - the rest waits (or pay the smith yourself)." : ""), true);
                    Log.Info("TroopSelfMend: " + st.Name + " - naprawiono " + _hourMended + " szt. za " + _hourPaid + " z sakiewki ludzi" + (broke ? " (sakiewka pusta)" : "") + ".");
                    _hourMended = 0; _hourPaid = 0;
                }
            }
            catch (Exception e) { Log.Error("TroopSelfMend.Hourly", e); }
        }
        private static int _hourMended, _hourPaid;

        internal static void Run(Settlement st)
        {
            if (MenPurse.On) return;   // wpis 84: naprawy godzinowe z sakiewki ludzi (Hourly)
            try
            {
                var s = Settings.Current;
                if (s == null || !s.TroopSelfMendEnabled || s.TroopSelfMendPercentPerDay <= 0) return;
                var main = MobileParty.MainParty;
                if (main == null || st == null || main.CurrentSettlement != st) return;
                if (!st.IsTown) return;   // naprawa wymaga kowala z prawdziwym warsztatem

                var armory = QuartermasterLaw.DteArmory();
                if (armory == null) return;

                // najgorsze sztuki na wierzch - je lataja najpierw
                var worn = new List<ItemRosterElement>();
                for (int i = 0; i < armory.Count; i++)
                {
                    var el = armory.GetElementCopyAtIndex(i);
                    var mod = el.EquipmentElement.ItemModifier;
                    if (el.Amount <= 0 || mod == null || mod.PriceMultiplier >= 1f) continue;
                    if (mod.PriceMultiplier < 0.1f) continue;   // audyt pelny K2: wrak - tylko kowal z materialem albo przetop
                    if (el.EquipmentElement.Item == null) continue;
                    worn.Add(el);
                }
                if (worn.Count == 0) return;
                worn.Sort((a, b) => a.EquipmentElement.ItemModifier.PriceMultiplier
                    .CompareTo(b.EquipmentElement.ItemModifier.PriceMultiplier));

                int wornTotal = 0;
                foreach (var el in worn) wornTotal += el.Amount;
                int budget = Math.Max(3, (int)Math.Round(wornTotal * s.TroopSelfMendPercentPerDay / 100.0));
                if (budget > wornTotal) budget = wornTotal;
                int mended = 0, paidAll = 0;
                foreach (var el in worn)
                {
                    if (budget <= 0) break;
                    // audyt pelny K2: naprawa PLATNA miastu (robocizna jak u kowala: 25% utraconej wartosci) - wczesniej za darmo
                    int unit = Math.Max(1, (int)(el.EquipmentElement.Item.Value * (1f - el.EquipmentElement.ItemModifier.PriceMultiplier) * 0.25f));
                    int afford = Math.Max(0, TaleWorlds.CampaignSystem.Hero.MainHero.Gold - paidAll) / unit;
                    int take = Math.Min(Math.Min(budget, el.Amount), afford);
                    if (take <= 0) break;
                    paidAll += unit * take;
                    armory.AddToCounts(el.EquipmentElement, -take);
                    armory.AddToCounts(new EquipmentElement(el.EquipmentElement.Item), take);   // czysty stan
                    mended += take; budget -= take;
                }
                if (paidAll > 0) Pay.ToSettlement(paidAll);
                if (mended > 0)
                {
                    Log.Info("TroopSelfMend: wojsko naprawilo " + mended + " sztuk w " + st.Name + " za " + paidAll + " zl do kasy miasta.");
                    Log.Player("The men see to their own kit at " + st.Name + " - " + mended
                               + " pieces mended by the town smiths for " + paidAll + " gold.", true);
                }
            }
            catch (Exception e) { Log.Error("TroopSelfMend.Run", e); }
        }
    }
}
