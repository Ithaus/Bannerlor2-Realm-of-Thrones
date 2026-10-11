using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
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
    ///  - TYLKO w dobowym Tick (flaga wokol PartySupplies.Tick): wejscie do miasta nie kupuje, bo gra (PartiesSellLootCampaignBehavior,
    ///    wejscie lorda AI do miasta) sprzedaje od razu kazdy towar niejadalny - drewno, narzedzia, wino - wiec zakup przy wejsciu
    ///    (BK wola swoj nasluch przed nasluchem gry) wracalby zaraz na polke z utrata na cenie i 1/9 dla korony (Ransom178).
    ///    Zuzycie (ConsumeItems) biegnie w tym samym Tick zaraz po zakupie.
    /// Wylaczone: BK jak dotad (w tym blad zapasu osady i zloto w nicosc).
    /// Kara morale z zaopatrzenia (wzor BK) - nietknieta (osobny krok W1, BkSupplyTemper).
    /// </summary>
    internal static class BkSupplyMarket
    {
        internal static bool On { get { var s = Settings.Current; return s != null && s.BkSupplyBuyByPartyStock; } }
        internal static bool Hooked;                       // prefiks BuyItems(float, List) i flaga Tick wpiete

        private static PropertyInfo _party;                // PartySupplies.Party
        private static int _tickDepth;                     // > 0: trwa PartySupplies.Tick (watek glowny gry)

        private const int GDrink = 0, GWood = 1, GAnimal = 2, GTools = 3, GOther = 4, Groups = 5;
        private static readonly string[] GName = { "napitki", "drewno", "produkty zwierzece", "narzedzia", "inne" };

        // ------------------------------------------------------------ liczniki doby (linia "Szczelnosc BK (3.4)" i czesc linii "Obieg: BK")
        private static int _dCalls, _dBuys, _dFull, _dNoGoods, _dNoGold, _dNoLeader, _dEntry, _dStumbles;
        private static readonly long[] _dPieces = new long[Groups], _dGold = new long[Groups];
        internal static long DayTown, DayCastle, DayVillage;    // zloto rody -> kasy osad dzis
        private static readonly HashSet<string> _errOnce = new HashSet<string>();

        internal static void ClearDay()
        {
            _dCalls = _dBuys = _dFull = _dNoGoods = _dNoGold = _dNoLeader = _dEntry = 0;
            Array.Clear(_dPieces, 0, Groups); Array.Clear(_dGold, 0, Groups);
            DayTown = DayCastle = DayVillage = 0;
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
                if (_tickDepth <= 0) { if (__0 >= 1f) _dEntry++; return false; }   // wejscie do osady - bez zakupu (patrz opis klasy)
                var mp = _party != null ? _party.GetValue(__instance, null) as MobileParty : null;
                if (mp != null) Buy(mp, __0, __1);
            }
            catch (Exception e) { Stumble("BuyPrefix", e); }
            return false;
        }

        private static void Buy(MobileParty mp, float floatCount, List<ItemCategory> cats)
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
            for (int i = 0; i < shelf.Count; i++)
            {
                var e = shelf.GetElementCopyAtIndex(i);
                var it = e.EquipmentElement.Item;
                if (it != null && e.Amount > 0 && cats.Contains(it.ItemCategory)) offer.Add(e.EquipmentElement);
            }
            if (offer.Count == 0) { _dNoGoods++; return; }
            offer.Sort((a, b) => a.ItemValue.CompareTo(b.ItemValue));   // jak BK: najtansze pierwsze
            var comp = st.SettlementComponent;
            int got = 0;
            foreach (var el0 in offer)
            {
                if (need <= 0) break;
                int budget = leader.Gold;
                if (budget <= 0) break;
                var el = el0;
                int cost, first, last;
                int n = ShelfBuy.Take(shelf, el, need, budget, () => comp.GetItemPrice(el, mp, false), out cost, out first, out last, 0, false);
                if (n <= 0) continue;
                bag.AddToCounts(el, n);
                if (cost > 0) GiveGoldAction.ApplyForCharacterToSettlement(leader, st, cost, true);   // przelew lider -> osada (zdarzenie gry - ksiega pieniadza)
                got += n; need -= n;
                _dPieces[g] += n; _dGold[g] += cost;
                if (st.IsVillage) DayVillage += cost; else if (st.IsCastle) DayCastle += cost; else DayTown += cost;
            }
            if (got > 0) _dBuys++; else _dNoGold++;   // towar byl, a nic nie zeszlo - cena ponad kiese lidera
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
              .Append("; przy wejsciu do osady pominiete ").Append(_dEntry).Append(") | sztuk/zl:");
            for (int g = 0; g < Groups; g++) sb.Append(g == 0 ? " " : ", ").Append(GName[g]).Append(' ').Append(_dPieces[g]).Append('/').Append(_dGold[g].ToString(inv));
            sb.Append(" | rody -> kasy osad ").Append((DayTown + DayCastle + DayVillage).ToString(inv)).Append(" (miasta ").Append(DayTown.ToString(inv))
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
                Log.Info("BkSupplyMarket (3.4): zakupy zaopatrzenia BK wedlug zapasu PARTII (sufit Bk Supply Max Pieces na kategorie), z polki osady, zaplata do kasy osady, tylko w dobowym Tick - wpiete"
                         + (On ? "" : " (WYLACZONE w ustawieniach - BK jak dotad)") + ".");
            }
            catch (Exception e) { Log.Error("BkSupplyMarket.ApplyAll", e); }
        }
    }
}
