using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// KROK 3.4 (plan do konca moda, etap 3 - szczelnosc; rozpoznanie punktu 1, wariant W2): ZAKUPY ZAOPATRZENIA BK WEDLUG ZAPASU PARTII,
    /// ZAPLATA DO KASY OSADY.
    ///
    /// Dzis (dekompilacja BannerKings.dll, PartySupplies.cs:318-422):
    ///  - BuyItems() (dobowy Tick partii lorda w kazdej osadzie i wejscie partii lorda do miasta albo zamku) wola dla kazdej kategorii
    ///    BuyItems(Need x DaysOfProvision, kategorie);
    ///  - BuyItems(float, List) odejmuje od chcianej liczby zapas OSADY (CurrentSettlement.ItemRoster), nie partii: gdy osada ma tyle
    ///    sztuk kategorii, ile partia chce, nie kupuje nic (drewno: miasta trzymaja 55-72 tys. - BK nie kupuje go prawie nigdy), gdy ma
    ///    mniej - kupuje roznice (najtansze pierwsze, do zlota lidera / ItemValue);
    ///  - placi LeaderHero.ChangeHeroGold(-cena x n) - osada nic nie dostaje: zloto w nicosc, bez zdarzenia gry (w ksiedze pieniadza
    ///    siedzialo w reszcie "poza tickiem"), towar z polki do jukow.
    ///
    /// Regula (BkSupplyBuyByPartyStock, domyslnie wlaczona; jedna regula dla kazdej partii z PartySupplies.AutoBuying - AI i gracz,
    /// jesli wlaczy autozakupy kwatermistrza):
    ///  - cel = floor(Need x DaysOfProvision) jak w BK, najwyzej BkSupplyMaxPieces sztuk na kategorie (sufit, ktory BkSupplyTemper
    ///    obiecywal, a ktorego BK nie trzymal); kupuje sie cel minus to, co partia JUZ WIEZIE w tej kategorii;
    ///  - z polki osady, w ktorej partia stoi (miasto, zamek, wies), najtansze pierwsze (jak BK), kazda sztuka po swojej cenie targu
    ///    dla tej partii (ShelfBuy.Take - ceny hurtu, decyzja Jeffa 09.10), do zlota lidera (jak BK);
    ///  - zaplata GiveGoldAction lider -> osada (kasa miasta lub zamku, kiesa wsi): przelew w ksiedze pieniadza, nic w nicosc;
    ///  - w dobowym Tick (flaga wokol PartySupplies.Tick) i przy wejsciu do miasta albo zamku (BK: OnSettlementEntered, Town != null) ta sama
    ///    regula, z jednym wyjatkiem przy WEJSCIU: sztuki, ktore gra zaraz odsprzeda, sie pomija. Gra (PartiesSellLootCampaignBehavior) przy
    ///    wejsciu lorda AI (nie gracza, nie rozwiazywanej partii, nie do wroga) do MIASTA (nie zamku) sprzedaje kazdy towar niejadalny
    ///    (IsFood false: drewno, narzedzia, wino) i kazdego konia poza wierzchowcem bez modyfikatora; nasluch BK biegnie przed nasluchem gry,
    ///    wiec taki zakup wracalby od razu na polke ze strata na cenie i 1/9 dla korony (Ransom178). Zywnosc (piwo, mieso, ser, ryby),
    ///    zakupy gracza i zakupy w zamku przy wejsciu ida normalnie (poprawka po recenzji 3.4: dotad wejscie nie kupowalo nic).
    ///    Zuzycie (ConsumeItems) biegnie w dobowym Tick zaraz po zakupie.
    /// D11-4 (decyzja Jeffa 11.10 "napitki i drewno wojska placi WOJSKO", wylacznik BkSupplyFromMenPurse, przy wlaczonej sakiewce ludzi MenPurseEnabled):
    ///  platnikiem jest sakiewka ludzi tej partii (MenPurse - jak odziez wojska 150), nie lider: budzet = sakiewka minus rezerwa na zalegle naprawy
    ///  (robocizna - AiWear / TroopSelfMend.OutstandingCost bez szacunku materialu; naprawy maja pierwszenstwo jak od wpisu 84), zaplata MenPurse.Take ->
    ///  kasa osady (miasto, zamek, kiesa wsi) bez zdarzenia gry, licznik ksiegi przeplywow osad (MoneyLedger.NBkSupply) i tarcza zoldu w miescie
    ///  (SoldierPay.Hold - jak odziez i "zycie": to zloto z zoldu). Sakiewki nie starcza - kupuja tyle, na ile ich stac; lider nie doplaca. Gracz ta sama
    ///  regula (gdy wlaczy autozakupy kwatermistrza BK). Wylaczone albo sakiewki wylaczone - placi lider jak w 3.4.
    /// Wylaczone: BK jak dotad (w tym blad zapasu osady i zloto w nicosc).
    /// Kara morale z zaopatrzenia (wzor BK) - nietknieta (osobny krok W1, BkSupplyTemper).
    /// </summary>
    internal static class BkSupplyMarket
    {
        internal static bool On { get { var s = Settings.Current; return s != null && s.BkSupplyBuyByPartyStock; } }
        /// <summary>D11-4: placi sakiewka ludzi partii (wylacznik i sakiewki ludzi wlaczone).</summary>
        internal static bool PurseOn { get { var s = Settings.Current; return s != null && s.BkSupplyFromMenPurse && MenPurse.On; } }
        internal static bool Hooked;                       // prefiks BuyItems(float, List) i flaga Tick wpiete

        private static PropertyInfo _party;                // PartySupplies.Party
        private static int _tickDepth;                     // > 0: trwa PartySupplies.Tick (watek glowny gry)

        private const int GDrink = 0, GWood = 1, GAnimal = 2, GTools = 3, GOther = 4, Groups = 5;
        private static readonly string[] GName = { "napitki", "drewno", "produkty zwierzece", "narzedzia", "inne" };

        // ------------------------------------------------------------ liczniki doby (linia "Szczelnosc BK (3.4)" i czesc linii "Obieg: BK")
        private static int _dCalls, _dBuys, _dFull, _dNoGoods, _dNoGold, _dNoLeader, _dEntry, _dEntryBuys, _dStumbles;
        private static readonly long[] _dPieces = new long[Groups], _dGold = new long[Groups];
        internal static long DayTown, DayCastle, DayVillage;    // zloto platnika (lider albo od D11-4 sakiewka ludzi) -> kasy osad dzis
        // D11-4: zakupy z sakiewek ludzi - zakupow, zloto, wywolan bez budzetu (sakiewka <= rezerwa na naprawy), zloto lidera (D11-4 wylaczone)
        private static int _dPurseBuys, _dPurseBroke;
        private static long _dPurseGold, _dLeaderGold;
        private static readonly HashSet<string> _errOnce = new HashSet<string>();

        internal static void ClearDay()
        {
            _dCalls = _dBuys = _dFull = _dNoGoods = _dNoGold = _dNoLeader = _dEntry = _dEntryBuys = _dStumbles = 0;   // potkniecia tez z doby (poprawka po recenzji)
            Array.Clear(_dPieces, 0, Groups); Array.Clear(_dGold, 0, Groups);
            DayTown = DayCastle = DayVillage = 0;
            _dPurseBuys = _dPurseBroke = 0; _dPurseGold = _dLeaderGold = 0;
        }

        private static void Stumble(string where, Exception e)
        {
            _dStumbles++;
            if (_errOnce.Add(where)) Log.Error("BkSupplyMarket." + where, e);
        }

        // ------------------------------------------------------------ flaga dobowego Tick
        public static void TickPre(out bool __state)
        {
            __state = true;
            _tickDepth++;
        }

        public static void TickFin(bool __state)
        {
            if (__state && _tickDepth > 0) _tickDepth--;
        }

        // ------------------------------------------------------------ zakup
        /// <summary>Prefiks PartySupplies.BuyItems(float floatCount, List&lt;ItemCategory&gt; categories) - przy wlaczonej regule zastepuje BK
        /// w calosci (false). Ramka ksiegi towarow (GoodsLedger, Priority.First) otwiera sie przed nami i widzi sztuki polka -> juki.</summary>
        public static bool BuyPrefix(object __instance, float __0, List<ItemCategory> __1)
        {
            if (!On) return true;
            try
            {
                var mp = _party != null ? _party.GetValue(__instance, null) as MobileParty : null;
                if (mp != null) Buy(mp, __0, __1, _tickDepth <= 0);   // poza Tick = wejscie do miasta/zamku (BKPartyNeedsBehavior.OnSettlementEntered)
            }
            catch (Exception e) { Stumble("BuyPrefix", e); }
            return false;
        }

        private static void Buy(MobileParty mp, float floatCount, List<ItemCategory> cats, bool entry)
        {
            if (cats == null || cats.Count == 0) return;
            int want = (int)Math.Floor(floatCount);   // jak BK: potrzeba x dni, w dol
            if (want < 1) return;
            var st = mp.CurrentSettlement;
            if (st == null || st.SettlementComponent == null || st.ItemRoster == null) return;
            _dCalls++;
            var leader = mp.LeaderHero;
            if (leader == null) { _dNoLeader++; return; }   // BK: NullReference przy LeaderHero.Gold
            int g = GroupOf(cats);
            var bag = mp.ItemRoster;
            int have = 0;
            for (int i = 0; i < bag.Count; i++)
            {
                var e = bag.GetElementCopyAtIndex(i);
                var it = e.EquipmentElement.Item;
                if (it != null && e.Amount > 0 && cats.Contains(it.ItemCategory)) have += e.Amount;
            }
            var s = Settings.Current;
            int cap = s != null ? s.BkSupplyMaxPieces : 0;
            int target = cap > 0 ? Math.Min(want, cap) : want;
            int need = target - have;
            if (need <= 0) { _dFull++; return; }
            var shelf = st.ItemRoster;
            var offer = new List<EquipmentElement>();
            bool resell = entry && ResoldOnEntry(mp, st);   // zaraz po nasluchu BK gra sprzeda lordowi AI w miescie towar niejadalny
            int withheld = 0;
            for (int i = 0; i < shelf.Count; i++)
            {
                var e = shelf.GetElementCopyAtIndex(i);
                var it = e.EquipmentElement.Item;
                if (it == null || e.Amount <= 0 || !cats.Contains(it.ItemCategory)) continue;
                if (resell && GameSellsOnEntry(e.EquipmentElement)) { withheld++; continue; }
                offer.Add(e.EquipmentElement);
            }
            if (withheld > 0) _dEntry++;
            if (offer.Count == 0) { if (withheld == 0) _dNoGoods++; return; }
            offer.Sort((a, b) => a.ItemValue.CompareTo(b.ItemValue));   // jak BK: najtansze pierwsze
            var comp = st.SettlementComponent;
            bool purse = PurseOn;                                       // D11-4: placi sakiewka ludzi
            int reserve = purse ? Reserve(mp) : 0;
            int got = 0;
            foreach (var el0 in offer)
            {
                if (need <= 0) break;
                int budget = purse ? MenPurse.Get(mp) - reserve : leader.Gold;
                if (budget <= 0) break;
                var el = el0;
                int cost, first, last;
                int n = ShelfBuy.Take(shelf, el, need, budget, () => comp.GetItemPrice(el, mp, false), out cost, out first, out last, 0, false);
                if (n <= 0) continue;
                bag.AddToCounts(el, n);
                if (cost > 0)
                {
                    if (purse)
                    {
                        int paid = MenPurse.Take(mp, cost);                    // cost <= budget <= sakiewka - paid == cost
                        comp.ChangeGold(paid);                                  // sakiewka ludzi -> kasa osady (bez zdarzenia gry; obie strony w "Pieniadz swiata")
                        MoneyLedger.Note(MoneyLedger.NBkSupply, st, paid);      // ksiega przeplywow osad (tylko licznik)
                        try { TownFolk.Sale(st, paid, TownFolk.MixFood); } catch { }   // E5/0 (195): zaopatrzenie BK z sakiewek (tylko licznik)
                        SoldierPay.Hold(st, paid);                              // tarcza zoldu w miescie - jak odziez wojska i "zycie"
                        _dPurseGold += paid;
                        cost = paid;
                    }
                    else { GiveGoldAction.ApplyForCharacterToSettlement(leader, st, cost, true); _dLeaderGold += cost; }   // przelew lider -> osada (zdarzenie gry - ksiega pieniadza)
                }
                got += n; need -= n;
                _dPieces[g] += n; _dGold[g] += cost;
                if (st.IsVillage) DayVillage += cost; else if (st.IsCastle) DayCastle += cost; else DayTown += cost;
            }
            if (got > 0) { _dBuys++; if (entry) _dEntryBuys++; if (purse) _dPurseBuys++; }
            else { _dNoGold++; if (purse && MenPurse.Get(mp) - reserve <= 0) _dPurseBroke++; }   // towar byl, a nic nie zeszlo - cena ponad kiese platnika
        }

        /// <summary>D11-4: rezerwa sakiewki na zalegle naprawy (robocizna; bez szacunku materialu - zakup BK biegnie tez w zamkach i wsiach, a co dobe).</summary>
        private static int Reserve(MobileParty mp)
        {
            try { return Math.Max(0, mp.IsMainParty ? TroopSelfMend.OutstandingCost(null) : AiWear.OutstandingCost(mp, null)); }
            catch (Exception e) { Stumble("Reserve", e); return 0; }
        }

        /// <summary>D11-4: kto placi za zaopatrzenie BK (do linii "Obieg: BK" i "Szczelnosc BK (3.4)").</summary>
        internal static string PayerText { get { return PurseOn ? "sakiewki ludzi (D11-4)" : "rody (lider; D11-4 wylaczone)"; } }

        /// <summary>Warunek partii i osady z PartiesSellLootCampaignBehavior.OnSettlementEntered (dekompilacja gry): gra sprzedaje przy wejsciu
        /// tylko za lorda AI (nie gracz, partia lorda, nie rozwiazywana), tylko w miescie (nie zamek) i tylko w osadzie niewrogiej.</summary>
        private static bool ResoldOnEntry(MobileParty mp, Settlement st)
        {
            if (mp.IsMainParty || !mp.IsLordParty || mp.IsDisbanding || !st.IsTown) return false;
            return !FactionManager.IsAtWarAgainstFaction(mp.MapFaction, st.MapFaction);
        }

        /// <summary>Warunek sztuki z PartiesSellLoot: sprzedaje kazda sztuke niejadalna, a z koni - wszystko poza wierzchowcem bez modyfikatora.</summary>
        private static bool GameSellsOnEntry(EquipmentElement el)
        {
            var it = el.Item;
            if (it == null || it.IsFood) return false;
            if (it.ItemType != ItemObject.ItemTypeEnum.Horse) return true;
            var hc = it.HorseComponent;
            return hc == null || !hc.IsRideable || el.ItemModifier != null || hc.IsPackAnimal;
        }

        private static int GroupOf(List<ItemCategory> cats)
        {
            if (cats.Contains(DefaultItemCategories.Beer) || cats.Contains(DefaultItemCategories.Wine)) return GDrink;
            if (cats.Contains(DefaultItemCategories.Wood)) return GWood;
            if (cats.Contains(DefaultItemCategories.Meat) || cats.Contains(DefaultItemCategories.Fish)) return GAnimal;
            if (cats.Contains(DefaultItemCategories.Tools)) return GTools;
            return GOther;
        }

        // ------------------------------------------------------------ log
        /// <summary>Czesc linii "Szczelnosc BK (3.4)" (BkSeal34.Daily) - bez zerowania.</summary>
        internal static string Part()
        {
            if (!Hooked) return "zaopatrzenie BK: latka nie jest wpieta (BK jak dotad)";
            if (!On) return "zaopatrzenie BK: WYLACZONE w ustawieniach (Bk Supply Buy By Party Stock) - BK jak dotad";
            var inv = CultureInfo.InvariantCulture;
            var sb = new System.Text.StringBuilder(512);
            sb.Append("zaopatrzenie BK (wedlug zapasu partii, sufit ").Append(Settings.Current != null ? Settings.Current.BkSupplyMaxPieces : 0)
              .Append(" szt.): zakupow ").Append(_dBuys).Append(" z ").Append(_dCalls).Append(" wywolan z potrzeba >= 1 szt. (zapas pelny ").Append(_dFull)
              .Append(", bez towaru ").Append(_dNoGoods).Append(", bez zlota ").Append(_dNoGold).Append(", bez wodza ").Append(_dNoLeader)
              .Append("; przy wejsciu do miasta/zamku: zakupow ").Append(_dEntryBuys).Append(", wywolan z pominietym towarem, ktory gra zaraz odsprzedaje (lord AI w miescie, niejadalne) ").Append(_dEntry).Append(")")
              .Append(" | platnik: ").Append(PayerText).Append(" - z sakiewek ludzi ").Append(_dPurseGold.ToString(inv)).Append(" (").Append(_dPurseBuys).Append(" zakupow; wywolan bez budzetu - sakiewka <= rezerwa na naprawy, lider nie doplaca: ")
              .Append(_dPurseBroke).Append("), z kies liderow ").Append(_dLeaderGold.ToString(inv)).Append(" | sztuk/zl:");
            for (int g = 0; g < Groups; g++) sb.Append(g == 0 ? " " : ", ").Append(GName[g]).Append(' ').Append(_dPieces[g]).Append('/').Append(_dGold[g].ToString(inv));
            sb.Append(" | ").Append(PayerText).Append(" -> kasy osad ").Append((DayTown + DayCastle + DayVillage).ToString(inv)).Append(" (miasta ").Append(DayTown.ToString(inv))
              .Append(", zamki ").Append(DayCastle.ToString(inv)).Append(", wsie ").Append(DayVillage.ToString(inv)).Append(')');
            if (_dStumbles > 0) sb.Append(" | potkniecia ").Append(_dStumbles);
            return sb.ToString();
        }

        // ------------------------------------------------------------ wpiecie
        internal static void ApplyAll(Harmony h)
        {
            try
            {
                var t = QuartermasterLaw.FindType("BannerKings.Behaviours.PartyNeeds.PartySupplies");
                if (t == null) { Log.Info("BkSupplyMarket (3.4): BK PartySupplies nieobecne - nic nie wpinam."); return; }
                _party = t.GetProperty("Party", BindingFlags.Public | BindingFlags.Instance);
                var buy = t.GetMethod("BuyItems", BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { typeof(float), typeof(List<ItemCategory>) }, null);
                var tick = t.GetMethod("Tick", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
                if (_party == null || buy == null || tick == null)
                {
                    Log.Info("BkSupplyMarket (3.4): BRAK - nie znaleziono " + (_party == null ? "PartySupplies.Party " : "") + (buy == null ? "BuyItems(float, List) " : "")
                             + (tick == null ? "Tick() " : "") + "- zaopatrzenie BK jak dotad.");
                    return;
                }
                h.Patch(tick, prefix: new HarmonyMethod(typeof(BkSupplyMarket), nameof(TickPre)), finalizer: new HarmonyMethod(typeof(BkSupplyMarket), nameof(TickFin)));
                h.Patch(buy, prefix: new HarmonyMethod(typeof(BkSupplyMarket), nameof(BuyPrefix)));
                Hooked = true;
                Log.Info("BkSupplyMarket (3.4): zakupy zaopatrzenia BK wedlug zapasu PARTII (sufit Bk Supply Max Pieces na kategorie), z polki osady, zaplata do kasy osady, w dobowym Tick i przy wejsciu (bez towaru, ktory gra zaraz odsprzedaje) - wpiete"
                         + (On ? "" : " (WYLACZONE w ustawieniach - BK jak dotad)") + ".");
            }
            catch (Exception e) { Log.Error("BkSupplyMarket.ApplyAll", e); }
        }
    }
}
