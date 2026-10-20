using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// m96-bandy (Jeff 20.10: "jak pokonuje bande czy ogolnie armie, to oni powinni miec tez zloto i jedzenie, bandyci chodza bez jedzenia czy zlota?").
    /// Wylacznik BandsEatFood (wylaczony = bandy nie jedza jak dotad, paser bierze cala zywnosc, dola bierze udzial calej kiesy).
    ///
    /// GRA (dekompilacja 1.4.8): DefaultMobilePartyFoodConsumptionModel.DoesPartyConsumeFood = false dla IsBandit (modele BK, BEE, ROT i NavalDLC tylko je
    /// opakowuja) - bandy nie jedza, nie gloduja i nie dezerteruja (DesertionCampaignBehavior tylko lordowie, karawany, zalogi). Nie zmieniamy modelu gry
    /// (opakowania cudzych modeli, UI, AI zakupow zywnosci) - wlasne zuzycie z jukow raz na dobe, ta sama miara co gra: 1 sztuka zywnosci na 20 ludzi
    /// (jency po pol), ulamek przechodzi na nastepna dobe.
    ///
    /// DOBA BANDY (OutlawLaw.Daily, po paserze - banda ma juz zaplate za lup; bandy w bitwie, zadaniu, nieumarli i najazdy Wolnych Ludzi poza regula):
    ///  1. ZAPAS: banda chce miec w jukach jedzenie na dzis i BandFoodKeepDays dni. Paser (OutlawLaw.FenceSell) skupuje tylko zywnosc ponad ten zapas.
    ///  2. ZAKUP: brak dokupuje przez pasera najblizszego otwartego miasta za WLASNA kiese (zamknieta ekonomia: towar schodzi z polki - ShelfBuy.Take,
    ///     sztuka po sztuce po cenie targu x marza pasera rosnaca z droga jak przy sprzecie; cala zaplata do kasy miasta, licznik sprzedazy kupcow TownFolk).
    ///     Takze z morza (recenzja: paser skupuje lup od band na morzu - FenceSell/Reach bez warunku morza - wiec i sprzedaje; piraci NavalDLC nie schodza na lad).
    ///  3. RABUNEK (tylko na ladzie): czego nie kupila, zabiera z magazynow wsi w zasiegu dnia drogi (RobRadius; wies nie spladrowana) - do pelnego zapasu,
    ///     najblizsza wies najpierw, z jednej wsi na dobe najwyzej RobVillageShare jej zywnosci (kilka band jednej kryjowki nie oprozni jednej wsi), do
    ///     MaxRobVillages wsi. Towar przechodzi z wsi do jukow (nic z niczego, nic w nicosc), wies nic nie dostaje; wies rodu gracza - komunikat raz na dobe.
    ///  4. JEDZENIE: zuzycie doby z jukow (najtansze najpierw) w ramce ksiegi towarow FFood ("jedzenie partii") z osada = najblizsze miasto bandy (rynek KS
    ///     tej krainy - recenzja: banda z kryjowka liczyla sie na rynek kultury kryjowki, piraci "bez rynku").
    ///  5. GLOD: brak jedzenia obniza morale (RecentEventsMorale, gra sama wygasza 10% na dobe) proporcjonalnie do braku; po HungerGraceDays dobach
    ///     glodu z rzedu czesc ludzi odchodzi (BandHungerLeaveShare x brak) - do puli wyrzutkow regionu tymi samymi drogami co dezercja z zaleglego
    ///     zoldu (WarLedger: OutlawLaw.OnTroopsDeserted i PeopleLedger, bez budzenia cudzych sluchaczy); banda bez ludzi rozwiazana jak przy dezercji
    ///     gry (DestroyPartyAction bez sprawcy - reszte robia reguly rozwiazanej bandy: kiesa, juki, jency). Na morzu i z bohaterem w niewoli bez odejsc
    ///     (gra tez nie zdejmuje dezerterow na morzu). Glodna banda na ladzie nie werbuje z puli (OutlawLaw.Daily); na morzu werbunek jak dotad (banda nie
    ///     ma jak zejsc po jedzenie - piraci nie moga tylko wymierac).
    ///  6. KIESA: zapas kiesy = BandPurseKeepDays dni zycia bandy: zold rekruta (1 zl na czlowieka na dzien - DefaultPartyWageModel, tier 0) i jedzenie po
    ///     cenie pasera. Zycie w miastach (OutlawLaw.LifeDay) i dola do kryjowki (OutlawLaw.StashDay) biora tylko z kiesy ponad zapas; banda z kryjowka
    ///     ponizej zapasu dostaje brak z nadwyzki kasy kryjowki ponad skarbiec (OutlawHoardCirculates - ta sama regula co kiesy startowe; w liniach
    ///     nskarbiec i Paser w "kiesy startowe"). Skarbca kryjowki nie rusza.
    /// LUP: zwyciezca bandy dostaje jej jedzenie i kiese juz dzis - juki: MapEvent.LootDefeatedPartyItems (ok. polowa sztuk, szansa 0.5 x udzial) +
    /// reszta przy zniszczeniu (HorseFate.Spoils, R14 SpoilsOfBrokenParties); kiesa: BeatenPurse (3.2, cala kiesa bandy bez wodza). Ta klasa tylko mierzy
    /// (OnLoot - zdarzenie gry OnLootDistributedToParty; NoteBandGold z BeatenPurse) - gracz osobno.
    /// Stan (dlug ulamka jedzenia, doby glodu) tylko w sesji - nic nie idzie do zapisu; po wczytaniu glod liczy sie od nowa (morale zostaje w zapisie gry).
    /// Linia doby "Bandy - jedzenie i kiesy (m96-bandy)".
    /// </summary>
    internal static class BandFood
    {
        internal static bool On { get { var s = Settings.Current; return s != null && s.BandsEatFood && OutlawLaw.On; } }

        private const float MenPerFood = 20f;       // jak gra (DefaultMobilePartyFoodConsumptionModel.NumberOfMenOnMapToEatOneFood)
        private const int HungerGraceDays = 2;      // doby glodu z rzedu, zanim ludzie zaczna odchodzic
        private const float HungerMorale = 5f;      // morale z glodu na dobe przy calkowitym braku (gra wygasza 10% na dobe - ustalony stan ok. -50)
        private const float RobRadius = 100f;       // dzien drogi bandy (do linii pasera "50-100")
        private const float RobVillageShare = 0.5f; // z jednej wsi na dobe najwyzej polowa jej zywnosci (wszystkie bandy razem)
        private const int MaxRobVillages = 3;       // banda obchodzi na dobe najwyzej tyle wsi
        private const int LifeCoinPerMan = 1;       // dzien zycia czlowieka bandy w kiesie = zold rekruta tier 0 (DefaultPartyWageModel.GetCharacterWage)

        private sealed class St { public float Owed; public int Hungry; }
        private static readonly Dictionary<MobileParty, St> _st = new Dictionary<MobileParty, St>(PeopleBook.RefEq.I);
        private static readonly List<MobileParty> _gone = new List<MobileParty>();
        private static readonly List<int> _purses = new List<int>();
        private static readonly HashSet<Settlement> _robbed = new HashSet<Settlement>();
        private static readonly HashSet<Settlement> _plRobbed = new HashSet<Settlement>();
        private static readonly HashSet<Settlement> _buyTowns = new HashSet<Settlement>();
        private static readonly Dictionary<Settlement, int> _villLeft = new Dictionary<Settlement, int>();   // ile zywnosci wies moze jeszcze dzis oddac bandom
        private static ItemObject _grain;
        private static bool _grainTried;

        // liczniki doby
        private static int _bands, _skipBattle, _skipQuest, _skipUndead, _skipRaid, _skipEmpty;
        private static double _need;
        private static long _eaten, _bought, _boughtGold, _robbedUnits, _robbedWorth, _plRobUnits;
        private static int _buyBands, _seaBuyBands, _robBands, _fed, _hungry, _starving, _longHungry, _leftMen, _leftBands, _broken, _atSea, _seaHungry, _heroHeld;
        private static double _morale;
        private static long _stock;
        private static int _stockOk;
        // kiesa: dola (StashDay), zycie (LifeDay), kieszonkowe z nadwyzki kryjowki (StashDay)
        private static int _stashKept, _stashWhole; private static long _stashKeptGold;
        private static int _lifeKept, _lifeWhole; private static long _lifeKeptGold;
        private static int _pocketBands, _pocketShort; private static long _pocketGold, _pocketWant;
        // lup (zdarzenie gry + BeatenPurse)
        private static long _lootFoodAi, _lootFoodPlayer, _lootFoodLords, _lootFoodLordsPlayer, _bandGold, _bandGoldPlayer;
        private static int _lootBands, _lootBandsPlayer;
        private static int _stumbles, _sessStumbles;
        private static bool _err;

        internal static void Reset()
        {
            _st.Clear(); _gone.Clear(); _purses.Clear(); _robbed.Clear(); _plRobbed.Clear(); _buyTowns.Clear(); _villLeft.Clear();
            _grain = null; _grainTried = false; _sessStumbles = 0; _err = false;
            NewDay();
            KiesaNewDay();
            _lootFoodAi = _lootFoodPlayer = _lootFoodLords = _lootFoodLordsPlayer = _bandGold = _bandGoldPlayer = 0; _lootBands = _lootBandsPlayer = 0;
        }

        private static void NewDay()
        {
            _bands = _skipBattle = _skipQuest = _skipUndead = _skipRaid = _skipEmpty = 0;
            _need = 0; _eaten = _bought = _boughtGold = _robbedUnits = _robbedWorth = _plRobUnits = 0;
            _buyBands = _seaBuyBands = _robBands = _fed = _hungry = _starving = _longHungry = _leftMen = _leftBands = _broken = _atSea = _seaHungry = _heroHeld = 0;
            _morale = 0; _stock = 0; _stockOk = 0; _stumbles = 0;
            _robbed.Clear(); _plRobbed.Clear(); _buyTowns.Clear(); _villLeft.Clear();
        }

        private static void KiesaNewDay()
        {
            _stashKept = _stashWhole = 0; _stashKeptGold = 0;
            _lifeKept = _lifeWhole = 0; _lifeKeptGold = 0;
            _pocketBands = _pocketShort = 0; _pocketGold = _pocketWant = 0;
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++; _sessStumbles++;
            if (!_err) { _err = true; Log.Error("BandFood." + where, e); }
        }

        // ------------------------------------------------------------ miary bandy
        /// <summary>Zywnosc doby bandy (sztuki, ulamkowo): ludzie i jency po pol na 20 - jak gra.</summary>
        private static float Need(MobileParty p)
        {
            int men = p.MemberRoster != null ? p.MemberRoster.TotalManCount : 0;
            int pris = p.PrisonRoster != null ? p.PrisonRoster.TotalManCount : 0;
            return Math.Max(1, men + pris / 2) / MenPerFood;
        }

        internal static int FoodUnits(ItemRoster r)
        {
            int n = 0;
            if (r == null) return 0;
            for (int i = 0; i < r.Count; i++)
            {
                var el = r.GetElementCopyAtIndex(i);
                var it = el.EquipmentElement.Item;
                if (it != null && el.Amount > 0 && it.IsFood && !it.NotMerchandise && !el.EquipmentElement.IsQuestItem) n += el.Amount;
            }
            return n;
        }

        /// <summary>Banda, ktora je wedlug tej reguly (nieumarli i najazdy Wolnych Ludzi - nie).</summary>
        private static bool Eats(MobileParty p)
        {
            return p != null && p.IsActive && !Undead.Party(p) && !FreeFolkRaids.Owns(p);
        }

        /// <summary>OutlawLaw.FenceSell: ile sztuk zywnosci banda zostawia sobie w jukach (dzis + BandFoodKeepDays dni). 0 = paser bierze cala (klucz off).</summary>
        internal static int FoodKeep(MobileParty p)
        {
            try
            {
                var s = Settings.Current;
                if (!On || !Eats(p)) return 0;
                return (int)Math.Ceiling(Need(p) * (1 + Math.Max(0, s.BandFoodKeepDays)));
            }
            catch (Exception e) { Stumble("FoodKeep", e); return 0; }
        }

        /// <summary>OutlawLaw.Daily: banda na ladzie glodowala wczoraj - nie werbuje z puli (nikt nie przystaje do glodnych). Na morzu werbunek jak dotad
        /// (banda nie ma jak zejsc na lad po jedzenie; recenzja: piraci NavalDLC bez werbunku tylko by wymierali).</summary>
        internal static bool Starving(MobileParty p)
        {
            St st;
            return On && p != null && !p.IsCurrentlyAtSea && _st.TryGetValue(p, out st) && st.Hungry > 0;
        }

        private static EquipmentElement GrainEl()
        {
            if (!_grainTried)
            {
                _grainTried = true;
                try { _grain = MBObjectManager.Instance.GetObject<ItemObject>("grain"); } catch { _grain = null; }
                if (_grain == null) { try { _grain = DefaultItems.Grain; } catch { _grain = null; } }
            }
            return _grain != null ? new EquipmentElement(_grain) : EquipmentElement.Invalid;
        }

        private static float Markup(Settings s, float dist)
        {
            float radius = Math.Max(1f, s.OutlawFenceRadius);
            float near = Math.Max(0f, s.OutlawFenceMarkup), far = Math.Max(near, s.OutlawFenceMarkupFar);
            return near + (far - near) * MBMath.ClampFloat(dist / radius, 0f, 1f);
        }

        /// <summary>OutlawLaw.LifeDay i StashDay: kiesa, ktora banda zostawia sobie - BandPurseKeepDays dni zycia: zold rekruta (LifeCoinPerMan na czlowieka)
        /// i jedzenie po cenie zboza u pasera najblizszego miasta. 0 = klucz off (cala kiesa jak dotad).</summary>
        internal static int PurseKeep(MobileParty p)
        {
            try
            {
                var s = Settings.Current;
                if (!On || s.BandPurseKeepDays <= 0 || !Eats(p) || p.MemberRoster == null) return 0;
                double day = (double)p.MemberRoster.TotalManCount * LifeCoinPerMan;
                var g = GrainEl();
                if (g.Item != null)
                {
                    var pos = p.Position.ToVec2();
                    var town = OutlawLaw.NearestTownOf(pos);
                    float price = town != null && town.Town != null
                        ? Math.Max(1, town.Town.GetItemPrice(g, null, false)) * Markup(s, pos.Distance(town.GetPosition2D))
                        : g.Item.Value * Math.Max(0f, s.OutlawFenceMarkupFar);
                    day += Need(p) * price;
                }
                return (int)Math.Min(int.MaxValue, Math.Ceiling(day * s.BandPurseKeepDays));
            }
            catch (Exception e) { Stumble("PurseKeep", e); return 0; }
        }

        /// <summary>OutlawLaw.StashDay: dola pominieta albo mniejsza, bo kiesa trzyma zapas (purse - kwota w kiesie przed dola, keep - zapas).</summary>
        internal static void NoteStashKept(int purse, int keep)
        {
            if (purse <= 0 || keep <= 0) return;
            _stashKept++; _stashKeptGold += Math.Min(purse, keep);
            if (keep >= purse) _stashWhole++;
        }

        /// <summary>OutlawLaw.LifeDay: zapas kiesy, ktorego zycie w miastach nie rusza (wydatek tylko z reszty). 0 = klucz off (jak dotad).</summary>
        internal static int LifeKeep(MobileParty p)
        {
            int keep = PurseKeep(p);
            if (keep <= 0) return 0;
            int purse = p.PartyTradeGold;
            if (purse > 0)
            {
                _lifeKept++; _lifeKeptGold += Math.Min(purse, keep);
                if (keep >= purse) _lifeWhole++;
            }
            return keep;
        }

        /// <summary>OutlawLaw.StashDay: kieszonkowe - banda z kryjowka ponizej zapasu kiesy (want - brak) dostala got z nadwyzki kasy kryjowki.</summary>
        internal static void NotePocket(int want, int got)
        {
            if (want <= 0) return;
            _pocketWant += want;
            if (got > 0) { _pocketBands++; _pocketGold += got; }
            if (got < want) _pocketShort++;
        }

        // ------------------------------------------------------------ lup (tylko pomiar)
        /// <summary>CampaignEvents.OnLootDistributedToPartyEvent (MapEvent.LootDefeatedPartyItems): zywnosc z jukow pokonanej bandy i druzyny lorda u zwyciezcy.</summary>
        internal static void OnLoot(PartyBase winner, PartyBase defeated, ItemRoster loot)
        {
            try
            {
                var dm = defeated != null ? defeated.MobileParty : null;
                if (dm == null || loot == null) return;
                bool band = OutlawLaw.IsOutlawParty(dm), lord = !band && dm.IsLordParty;
                if (!band && !lord) return;
                int n = FoodUnits(loot);
                if (n <= 0) return;                    // przydzial bez jedzenia (np. lup poleglych - LootDefeatedPartyCasualties) - nie liczymy
                bool player = winner != null && winner == PartyBase.MainParty;
                if (band)
                {
                    if (player) { _lootFoodPlayer += n; _lootBandsPlayer++; } else { _lootFoodAi += n; _lootBands++; }
                }
                else if (player) _lootFoodLordsPlayer += n; else _lootFoodLords += n;
            }
            catch (Exception e) { Stumble("OnLoot", e); }
        }

        /// <summary>BeatenPurse.SplitPostfix: zloto z kies band wyplacone zwyciezcom tej bitwy (all) i z tego graczowi (player).</summary>
        internal static void NoteBandGold(long all, long player)
        {
            if (all > 0) _bandGold += all;
            if (player > 0) _bandGoldPlayer += player;
        }

        // ------------------------------------------------------------ doba
        /// <summary>Z OutlawLaw.Daily po paserze (MoneyLedger: zakupy jedzenia w odcinku "bandy - zycie").</summary>
        internal static void Daily(Settings s)
        {
            if (s == null) return;
            if (!On) { _st.Clear(); Line(s); NewDay(); return; }   // wylaczony: stan glodu od nowa (po ponownym wlaczeniu znow 2 doby karencji)
            List<MobileParty> list;
            try { list = MobileParty.AllBanditParties.ToList(); }
            catch (Exception e) { Stumble("Daily(lista)", e); Line(s); NewDay(); return; }
            try
            {
                // stan partii, ktorych juz nie ma
                _gone.Clear();
                foreach (var kv in _st) if (kv.Key == null || !kv.Key.IsActive) _gone.Add(kv.Key);
                foreach (var g in _gone) _st.Remove(g);
                _gone.Clear();
            }
            catch (Exception e) { Stumble("Daily(stan)", e); }
            int keepDays = Math.Max(0, s.BandFoodKeepDays);
            float leave = MBMath.ClampFloat(s.BandHungerLeaveShare, 0f, 1f);
            foreach (var p in list)
            {
                try { Band(p, s, keepDays, leave); }
                catch (Exception e) { Stumble("Band", e); }
            }
            Line(s);
            NewDay();
        }

        private static void Band(MobileParty p, Settings s, int keepDays, float leave)
        {
            if (p == null || !p.IsActive || p.IsDisbanding) return;
            if (p.MemberRoster == null || p.MemberRoster.TotalManCount <= 0 || p.ItemRoster == null) { _skipEmpty++; return; }
            if (Undead.Party(p)) { _skipUndead++; return; }
            if (FreeFolkRaids.Owns(p)) { _skipRaid++; return; }
            if (p.MapEvent != null) { _skipBattle++; return; }
            if (p.IsCurrentlyUsedByAQuest) { _skipQuest++; return; }
            _bands++;
            St st;
            if (!_st.TryGetValue(p, out st)) { st = new St(); _st[p] = st; }
            var pack = p.ItemRoster;
            float need = Need(p);
            _need += need;
            bool sea = p.IsCurrentlyAtSea;
            if (sea) _atSea++;
            var pos = p.Position.ToVec2();
            var town = OutlawLaw.NearestTownOf(pos);
            int have = FoodUnits(pack);
            // 2. dokup do zapasu (dzis + keepDays) przez pasera - takze z morza (paser skupuje lup z morza, wiec i sprzedaje)
            int want = (int)Math.Ceiling(need * (1 + keepDays)) - have;
            if (want > 0 && town != null && p.PartyTradeGold > 0)
            {
                int got = Buy(p, s, want, town, pos);
                if (got > 0) { have += got; want -= got; _buyBands++; if (sea) _seaBuyBands++; }
            }
            // 3. czego nie kupila - rabunek wsi do pelnego zapasu (tylko na ladzie)
            if (want > 0 && !sea)
            {
                int got = Rob(p, want, pos);
                if (got > 0) { have += got; _robBands++; }
            }
            // 4. jedzenie doby (ulamek przechodzi na jutro)
            float owed = st.Owed + need;
            int units = (int)owed;
            int eat = Math.Min(units, have);
            if (eat > 0) eat = Eat(p, eat, town);
            st.Owed = Math.Max(0f, owed - units);
            have -= eat;
            _eaten += eat;
            _stock += Math.Max(0, have);
            if (have >= (int)Math.Ceiling(need * keepDays)) _stockOk++;
            // 5. glod: najedzona = zjadla dzis cala potrzebe albo ma jej zapas (dzien bez pelnej sztuki do zjedzenia nie jest glodem)
            float fed = need <= 0f ? 1f : MBMath.ClampFloat((eat + have) / need, 0f, 1f);
            if (fed >= 0.999f) { _fed++; st.Hungry = 0; return; }
            st.Hungry++;
            _hungry++;
            if (sea) _seaHungry++;
            if (eat + have <= 0) _starving++;
            float lack = 1f - fed;
            try
            {
                float dm = HungerMorale * lack;
                p.RecentEventsMorale -= dm;
                _morale += dm;
            }
            catch (Exception e) { Stumble("Morale", e); }
            if (st.Hungry <= HungerGraceDays) return;
            _longHungry++;
            if (sea) return;                                                   // gra tez nie zdejmuje dezerterow na morzu
            if (p.PrisonRoster != null && p.PrisonRoster.TotalHeroes > 0) { _heroHeld++; return; }   // jeniec-bohater (gracz, lord) - banda nie rozchodzi sie z nim
            if (leave > 0f) Leave(p, leave * lack);
        }

        /// <summary>Zakup przez pasera najblizszego otwartego miasta: najtansza zywnosc z polki (wartosc), sztuka po sztuce, za kiese bandy.</summary>
        private static int Buy(MobileParty p, Settings s, int want, Settlement town, Vec2 pos)
        {
            if (town == null || town.Town == null || town.ItemRoster == null) return 0;
            var shelf = town.ItemRoster;
            float mk = Markup(s, pos.Distance(town.GetPosition2D));
            int got = 0;
            for (int round = 0; round < 3 && got < want; round++)
            {
                int budget = p.PartyTradeGold;
                if (budget <= 0) break;
                int best = -1, bv = int.MaxValue;
                for (int i = 0; i < shelf.Count; i++)
                {
                    var el = shelf.GetElementCopyAtIndex(i);
                    var it = el.EquipmentElement.Item;
                    if (it == null || el.Amount <= 0 || !it.IsFood || it.NotMerchandise || el.EquipmentElement.IsQuestItem) continue;
                    if (it.Value < bv) { bv = it.Value; best = i; }
                }
                if (best < 0) break;
                var cel = shelf.GetElementCopyAtIndex(best).EquipmentElement;
                var tw = town.Town;
                int cost, first, last;
                int n = ShelfBuy.Take(shelf, cel, want - got, budget, () => (int)Math.Ceiling(Math.Max(1, tw.GetItemPrice(cel, null, false)) * mk), out cost, out first, out last, 0, false);
                if (n <= 0) break;
                p.ItemRoster.AddToCounts(cel, n);
                if (cost > 0)
                {
                    p.PartyTradeGold -= cost;
                    town.SettlementComponent.ChangeGold(cost);                        // kiesa bandy -> kasa miasta pasera (w bloku doby - odcinek MoneyLedger "bandy - zycie")
                    try { TownFolk.Sale(town, cost, TownFolk.MixFood); } catch { }    // sprzedaz kupcow miasta (podstawa robocizny handlu) - jak zaopatrzenie BK
                }
                got += n; _bought += n; _boughtGold += cost;
                _buyTowns.Add(town);
            }
            return got;
        }

        /// <summary>Ile zywnosci wies moze jeszcze dzis oddac bandom: przy pierwszym rabunku doby RobVillageShare jej zywnosci (co najmniej 1, gdy ma cokolwiek).</summary>
        private static int Allowance(Settlement st)
        {
            int a;
            if (_villLeft.TryGetValue(st, out a)) return a;
            int f = FoodUnits(st.ItemRoster);
            a = f > 0 ? Math.Max(1, (int)Math.Ceiling(f * RobVillageShare)) : 0;
            _villLeft[st] = a;
            return a;
        }

        /// <summary>Rabunek: zywnosc z magazynow wsi w zasiegu dnia drogi (wies Normal) - najblizsza wies z czyms do wziecia najpierw, do MaxRobVillages wsi,
        /// z jednej wsi na dobe najwyzej Allowance. Zwraca sztuki dodane do jukow.</summary>
        private static int Rob(MobileParty p, int want, Vec2 pos)
        {
            float r2 = RobRadius * RobRadius;
            int got = 0;
            for (int k = 0; k < MaxRobVillages && got < want; k++)
            {
                float bd = float.MaxValue;
                Village bestV = null;
                foreach (var v in Village.All)
                {
                    if (v == null || v.Settlement == null || v.VillageState != Village.VillageStates.Normal) continue;
                    float d2 = pos.DistanceSquared(v.Settlement.GetPosition2D);
                    if (d2 > r2 || d2 >= bd) continue;
                    if (Allowance(v.Settlement) <= 0) continue;
                    bd = d2; bestV = v;
                }
                if (bestV == null) break;
                var vs = bestV.Settlement;
                int left = Allowance(vs);
                int n = RobFrom(p, vs, Math.Min(want - got, left));
                _villLeft[vs] = n > 0 ? left - n : 0;          // nic nie wziela (stosy niehandlowe) - wies dzis odpada
                got += n;
            }
            return got;
        }

        /// <summary>Rabunek z jednej wsi: najtansza zywnosc najpierw, najwyzej want sztuk. Zwraca sztuki dodane do jukow.</summary>
        private static int RobFrom(MobileParty p, Settlement vs, int want)
        {
            var shelf = vs.ItemRoster;
            int got = 0;
            if (shelf == null || want <= 0) return 0;
            for (int round = 0; round < 4 && got < want; round++)
            {
                int best = -1, bv = int.MaxValue;
                for (int i = 0; i < shelf.Count; i++)
                {
                    var el = shelf.GetElementCopyAtIndex(i);
                    var it = el.EquipmentElement.Item;
                    if (it == null || el.Amount <= 0 || !it.IsFood || it.NotMerchandise || el.EquipmentElement.IsQuestItem) continue;
                    if (it.Value < bv) { bv = it.Value; best = i; }
                }
                if (best < 0) break;
                var e0 = shelf.GetElementCopyAtIndex(best);
                int n = Math.Min(want - got, e0.Amount);
                if (n <= 0) break;
                shelf.AddToCounts(e0.EquipmentElement, -n);
                p.ItemRoster.AddToCounts(e0.EquipmentElement, n);
                got += n; _robbedUnits += n; _robbedWorth += (long)n * e0.EquipmentElement.ItemValue;
            }
            if (got > 0)
            {
                _robbed.Add(vs);
                if (vs.OwnerClan != null && vs.OwnerClan == Clan.PlayerClan) { _plRobUnits += got; _plRobbed.Add(vs); }   // komunikat dla gracza raz na dobe (Line)
            }
            return got;
        }

        /// <summary>Zjada n sztuk z jukow, najtansze najpierw, w ramce ksiegi towarow "jedzenie partii" z osada = najblizsze miasto bandy (rynek KS krainy,
        /// w ktorej banda je; bez miasta - partia jak w ramce gry). Zwraca zjedzone.</summary>
        private static int Eat(MobileParty p, int n, Settlement town)
        {
            var pack = p.ItemRoster;
            int done = 0;
            var gf = town != null ? GoodsLedger.Begin(GoodsLedger.FFood, town, p) : GoodsLedger.Begin(GoodsLedger.FFood, p);
            try
            {
                for (int round = 0; round < 8 && done < n; round++)
                {
                    int best = -1, bv = int.MaxValue;
                    for (int i = 0; i < pack.Count; i++)
                    {
                        var el = pack.GetElementCopyAtIndex(i);
                        var it = el.EquipmentElement.Item;
                        if (it == null || el.Amount <= 0 || !it.IsFood || it.NotMerchandise || el.EquipmentElement.IsQuestItem) continue;
                        if (it.Value < bv) { bv = it.Value; best = i; }
                    }
                    if (best < 0) break;
                    var e0 = pack.GetElementCopyAtIndex(best);
                    int k = Math.Min(n - done, e0.Amount);
                    if (k <= 0) break;
                    pack.AddToCounts(e0.EquipmentElement, -k);
                    done += k;
                }
            }
            finally { GoodsLedger.End(gf); }
            return done;
        }

        /// <summary>Odejscie z glodu: z kazdego stosu zolnierzy (bez bohaterow) udzial `rate` (losowo zaokraglony), zdrowi i ranni po rowno; ludzie do puli
        /// wyrzutkow regionu i ksiegi ludzi (jak WarLedger - bez zdarzenia gry). Banda bez ludzi - rozwiazana jak przy dezercji gry.</summary>
        private static void Leave(MobileParty p, float rate)
        {
            var mr = p.MemberRoster;
            if (mr == null || rate <= 0f) return;
            TroopRoster left = null;
            for (int i = mr.Count - 1; i >= 0; i--)
            {
                var e = mr.GetElementCopyAtIndex(i);
                if (e.Character == null || e.Character.IsHero || e.Number <= 0) continue;
                int n = Math.Min(e.Number, MBRandom.RoundRandomized(e.Number * rate));
                if (n <= 0) continue;
                int w = Math.Min(e.WoundedNumber, MBRandom.RoundRandomized(n * (float)e.WoundedNumber / e.Number));
                if (left == null) left = TroopRoster.CreateDummyTroopRoster();
                mr.AddToCounts(e.Character, -n, false, -w);
                left.AddToCounts(e.Character, n, false, w);
            }
            if (left == null || left.TotalManCount <= 0) return;
            _leftMen += left.TotalManCount; _leftBands++;
            try { OutlawLaw.OnTroopsDeserted(p, left); } catch (Exception e) { Stumble("Leave(pula)", e); }
            try { PeopleLedger.OnTroopsDeserted(p, left); } catch (Exception e) { Stumble("Leave(ksiega)", e); }
            if (mr.TotalManCount <= 0)
            {
                try { DestroyPartyAction.Apply(null, p); _broken++; } catch (Exception e) { Stumble("Leave(rozwiazanie)", e); }
            }
        }

        // ------------------------------------------------------------ linia doby
        private static void Line(Settings s)
        {
            try
            {
                var ci = CultureInfo.InvariantCulture;
                long purse = 0, homePurse = 0; int withPurse = 0, field = 0, home = 0, homeWith = 0;
                _purses.Clear();
                foreach (var p in MobileParty.AllBanditParties)
                {
                    if (p == null || !p.IsActive) continue;
                    field++;
                    int g = p.PartyTradeGold;
                    purse += Math.Max(0, g);
                    if (g > 0) { withPurse++; _purses.Add(g); }
                    var bc = p.BanditPartyComponent;
                    if (bc != null && bc.Hideout != null) { home++; homePurse += Math.Max(0, g); if (g > 0) homeWith++; }
                }
                _purses.Sort();
                int med = _purses.Count > 0 ? _purses[_purses.Count / 2] : 0;
                var sb = new StringBuilder(1800);
                sb.Append("Bandy - jedzenie i kiesy (m96-bandy): dzien ").Append((int)CampaignTime.Now.ToDays)
                  .Append(On ? " | CZYNNE" : " | WYLACZONE (Bands Eat Food albo prawo wyrzutkow) - bandy nie jedza")
                  .Append(" (zapas ").Append(Math.Max(0, s.BandFoodKeepDays)).Append(" dni, kiesa ").Append(Math.Max(0, s.BandPurseKeepDays))
                  .Append(" dni zycia, odchodza ").Append(MBMath.ClampFloat(s.BandHungerLeaveShare, 0f, 1f).ToString("0.00", ci)).Append(" po ").Append(HungerGraceDays).Append(" dobach glodu)")
                  .Append(" | bandy jedzace ").Append(_bands).Append(" (pominiete: bitwa ").Append(_skipBattle).Append(", zadanie ").Append(_skipQuest)
                  .Append(", nieumarli ").Append(_skipUndead).Append(", najazdy Wolnych Ludzi ").Append(_skipRaid).Append(", puste ").Append(_skipEmpty).Append(')')
                  .Append(" | potrzeba doby ").Append(_need.ToString("0.0", ci)).Append(" szt., zjedzone ").Append(_eaten)
                  .Append(", kupione u pasera ").Append(_bought).Append(" szt. za ").Append(_boughtGold).Append(" zl (band ").Append(_buyBands).Append(", z morza ").Append(_seaBuyBands)
                  .Append(", miast ").Append(_buyTowns.Count).Append(')')
                  .Append(", zabrane wsiom ").Append(_robbedUnits).Append(" szt. warte ").Append(_robbedWorth).Append(" zl (band ").Append(_robBands).Append(", wsi ").Append(_robbed.Count)
                  .Append(", wsi gracza ").Append(_plRobbed.Count).Append(": ").Append(_plRobUnits).Append(" szt.)")
                  .Append(" | najedzone ").Append(_fed).Append(", glodne ").Append(_hungry).Append(" (bez jedzenia ").Append(_starving).Append(", ponad ").Append(HungerGraceDays)
                  .Append(" doby ").Append(_longHungry).Append(", na morzu ").Append(_seaHungry).Append(" z ").Append(_atSea).Append(", z jencem-bohaterem bez odejsc ").Append(_heroHeld).Append(')')
                  .Append(", morale z glodu -").Append(_morale.ToString("0", ci))
                  .Append(" | odeszli z glodu ").Append(_leftMen).Append(" ludzi z ").Append(_leftBands).Append(" band (do puli wyrzutkow), rozpady z glodu ").Append(_broken)
                  .Append(" | zapas w jukach ").Append(_stock).Append(" szt. (").Append(_need > 0 ? (_stock / _need).ToString("0.0", ci) : "-").Append(" dob potrzeby; band z pelnym zapasem ").Append(_stockOk).Append(')')
                  .Append(" | kiesy band w polu ").Append(purse).Append(" zl (band ").Append(field).Append(", z kiesa ").Append(withPurse).Append(", mediana ").Append(med)
                  .Append(" zl; z kryjowka ").Append(home).Append(", z kiesa ").Append(homeWith).Append(", razem ").Append(homePurse).Append(" zl)")
                  .Append("; dola: zapas zostal w ").Append(_stashKept).Append(" kiesach (").Append(_stashKeptGold).Append(" zl; w calosci zapasem ").Append(_stashWhole).Append(')')
                  .Append("; zycie w miastach: zapas nietkniety w ").Append(_lifeKept).Append(" kiesach (").Append(_lifeKeptGold).Append(" zl; w calosci zapasem ").Append(_lifeWhole).Append(')')
                  .Append("; kieszonkowe z nadwyzki kryjowek ").Append(_pocketGold).Append(" zl dla ").Append(_pocketBands).Append(" band (brakowalo ").Append(_pocketWant)
                  .Append(" zl, nie starczylo ").Append(_pocketShort).Append(" bandom; w liniach nskarbiec i Paser w \"kiesy startowe\")")
                  .Append(" | lup u zwyciezcow (od wczoraj): jedzenie z band AI ").Append(_lootFoodAi).Append(" szt. (").Append(_lootBands).Append(" przydzialow), gracz ").Append(_lootFoodPlayer)
                  .Append(" szt. (").Append(_lootBandsPlayer).Append("); zloto z kies band ").Append(_bandGold).Append(" zl, gracz ").Append(_bandGoldPlayer)
                  .Append(" zl; jedzenie z druzyn lordow AI ").Append(_lootFoodLords).Append(" szt., gracz ").Append(_lootFoodLordsPlayer)
                  .Append(" szt. (reszta jukow rozbitych - linia K1 \"dobytek rozbitych\")")
                  .Append(" | potkniecia ").Append(_stumbles).Append(" (sesja ").Append(_sessStumbles).Append(").");
                Log.Info(sb.ToString());
                if (_plRobUnits > 0 && _plRobbed.Count > 0)
                {
                    // komunikat dla gracza raz na dobe: wsie jego rodu, z ktorych bandy zabraly jedzenie (gra ma wtedy mniej do sprzedania w miescie)
                    var names = new List<string>(3);
                    foreach (var v in _plRobbed) { if (names.Count >= 3) break; names.Add(v.Name != null ? v.Name.ToString() : v.StringId); }
                    string more = _plRobbed.Count > names.Count ? " and " + (_plRobbed.Count - names.Count) + " more" : "";
                    Log.Player("Outlaws took " + _plRobUnits + (_plRobUnits == 1 ? " load" : " loads") + " of food from your village" + (_plRobbed.Count > 1 ? "s " : " ")
                               + string.Join(", ", names) + more + ".", true);
                }
            }
            catch (Exception e) { Stumble("Line", e); }
            finally
            {
                KiesaNewDay();
                _lootFoodAi = _lootFoodPlayer = _lootFoodLords = _lootFoodLordsPlayer = _bandGold = _bandGoldPlayer = 0; _lootBands = _lootBandsPlayer = 0;
            }
        }
    }
}
