using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// E6/K1 (211, PLAN 6.11, R9/R10/R14; projekt etapu 6 v2 rozdz. 3 "E6/K1", krytyka K1-13, K1-14, KB E60-okno-po-DTE) - KON GINIE TYLKO ZABITY;
    /// DOBYTEK ROZBITYCH; KONIE Z NICZEGO ZAMKNIETE.
    /// DTE (dekompilacja v1.4.7, EveryoneCampaignBehavior i DynamicTroopMissionLogic): w misji kon jezdzca wraca do zbrojowni (ItemsToRecover zwyciezcy, zwrot
    /// z agentow) nawet, gdy padl - DTE nie patrzy na agenta konia; w autobitwie konie nie gina wcale; po bitwie GetAllLootItems KOPIUJE zbrojownie pokonanych
    /// do zwyciezcow, nie zdejmujac ich u pokonanych (ocalaly pokonany zostaje z konmi - kon z niczego); zbrojownia rozbitej partii znika z partia.
    ///  (1) HorsesDieOnlyKilled. MISJA: HorseFateMission (MissionLogic) pamieta konia kazdego jezdzca-szeregowego partii przy spawnie (OnAgentBuild - kon jest
    ///      juz posadzony, G Mission.SpawnAgent: SetMountAgentBeforeBuild przed OnAgentBuild jezdzca); agent konia zabity (AgentState.Killed) = zgon; po zwrotach
    ///      DTE (postfiks DynamicTroopMissionLogic.OnEndMission) ten przedmiot schodzi ze zbrojowni wlasciciela (gracz: ArmyArmory, AI: PartyArmories);
    ///      nie ma go tam (lup DTE zabojcy, pokonany bez zwrotu) - licznik "poza zbrojownia wlasciciela". Obserwacja W: misja z zabitymi jezdzcami ->
    ///      W = zgony koni / zgony jezdzcow (suma z zapisu, arm_horsefate). AUTOBITWA (prefiks DTE EveryoneCampaignBehavior.OnMapEventEnded - po poleglych
    ///      MapEvent, przed rozdaniem lupu; bitwa z misja pominieta): za zabitych konnych szeregowych (DiedInBattle, IsMounted) partia AI traci
    ///      zabici x W x udzial jezdzcow z koniem (Stables.Balance sprzed bitwy - zabici dopisani) koni ze zbrojowni (ulamek - losowanie; rycerz: bojowy,
    ///      szlachetny, zwykly; reszta: zwykly, bojowy, szlachetny; w kategorii losowo wedlug liczby). Do HorseDeathRatioMinBattles obserwacji W = 1.0.
    ///      LUP: migawka koni zbrojowni zwyciezcow w prefiksie, w postfiksie konie, ktore DTE dopisalo zwyciezcom, schodza ze zbrojowni pokonanych
    ///      (lup = przeniesienie, nie kopia); droga autorozstrzygniecia gracza (DistributePlayerSimulationLoot - lup z poleglych) bez zmian.
    ///  (2) SpoilsOfBrokenParties (R14). GoodsLedger.OnPartyDestroyed -> Spoils PRZED licznikami "przepadlo" (bez niszczyciela tylko porzucenie znane: rozwiazanie
    ///      gry, druzyna lorda, tabor wsi, karawana, banda - bez wirtualnych dostaw BK PopulationPartyComponent): tabor (towar i konie; bez unikatow - UniqueSpoils
    ///      biegnie po nas; bez sztuk NotMerchandise, zadaniowych i choragwi) oraz konie zbrojowni DTE partii (jesli DTE jeszcze jej nie zdjal) - do
    ///      zwyciezcow wedlug wkladu (ContributionToBattle z migawki MapEventEnded; jak zloto A6 - proporcja, reszta najwiekszym), zaloga/milicja/osada -
    ///      na polke swojej osady, Inni - nic; ponad udzwig zwyciezcy - na polke najblizszej przyjaznej warowni; bez zwyciezcy (rozwiazanie) - polka osady
    ///      rozwiazania albo najblizszej przyjaznej warowni (kasa osady nie placi - porzucony dobytek). BK BKCaravansBehavior.OnMapEventEnded (zwycieska
    ///      karawana wyrzuca zwierzeta ponad ludzi i towar ponad udzwig): roznica rosteru - na polke najblizszej przyjaznej warowni.
    ///  (3) VeteranKeepsHorse. Konny zolnierz, ktory staje w karczmie (PeopleBook.VetCommit, klasa konna), bierze wolnego konia swojej kategorii ze zbrojowni
    ///      partii (Balance bez niego) do puli koni miasta (zapis arm_horsefate); najem konnego najemnika przez AI (RecruitKit) - kon z puli zamiast wzorca;
    ///      karczma pokazuje typ konny tylko do liczby koni w puli, bez koni - typ pieszy (Veterans.Sync); konie ponad konnych weteranow miasta (odeszli do domu,
    ///      najal gracz z kompletem DTE) - na polke miasta raz na dobe; gracz bez zmian (zbrojownia gracza nietknieta).
    ///  (4) NewPartyHorsesFromSeat (K1-14). Prefiks HeroSpawnCampaignBehavior.GiveInitialItemsToParty: ta sama liczba koni i zywnosci co gra (G :280-309),
    ///      ale z polki siedziby rodu (ClanBudget.Seat; siedziba nieoblegana i nie wroga) za zloto wodza (najwyzej przydzial sprzetu 166) - ShelfBuy, zaplata
    ///      GiveGoldAction wodz -> osada; czego nie ma albo za co nie ma zlota - brak. Gra nie biegnie (zero z niczego).
    ///  (5) TroopSwapNeedsHorse. Zamiana ROT pieszego na konnego w kazdej krainie (RotHorseGuard - dotad tylko Dothrakowie): kon z wolnych zbrojowni / taboru,
    ///      inaczej pieszy tego samego tieru z puli rodu.
    /// Linia doby "Konie - smierc, lup, dobytek (E6/K1)"; pozycje K1 i zgony jako ujscie w linii "Konie (E6/0)" (HorseBook). Stan czyszczony w Reset.
    /// </summary>
    internal static class HorseFate
    {
        // ------------------------------------------------------------ klucze
        internal static bool DieOn { get { var s = Settings.Current; return s != null && s.HorsesDieOnlyKilled; } }
        internal static bool SpoilsOn { get { var s = Settings.Current; return s != null && s.SpoilsOfBrokenParties; } }
        internal static bool VetHorseOn { get { var s = Settings.Current; return s != null && s.VeteranKeepsHorse && PeopleBook.VetLive; } }
        internal static bool SeatOn { get { var s = Settings.Current; return s != null && s.NewPartyHorsesFromSeat; } }

        private const int Cats = 5;
        private static readonly int[] OrdHigh = { 1, 2, 0 }, OrdLow = { 0, 1, 2 };

        // ------------------------------------------------------------ zapis: obserwacje W i pula koni weteranow
        private static long _obs, _obsRiders, _obsHorses;
        private static readonly Dictionary<string, Dictionary<ItemObject, int>> _vetPool = new Dictionary<string, Dictionary<ItemObject, int>>();
        private static string _pending;

        // ------------------------------------------------------------ misja
        private sealed class MDeath { public MobileParty Party; public ItemObject Item; }
        private static readonly List<MDeath> _mDeaths = new List<MDeath>();
        private static int _mRiders, _mHorses;
        private static MapEvent _missionEvent;

        // ------------------------------------------------------------ migawki bitwy
        private sealed class Win { public PartyBase Party; public double W; }
        private sealed class Beaten { public List<Win> Wins; public bool SimWin; public int Day; }
        private static readonly Dictionary<MobileParty, Beaten> _beaten = new Dictionary<MobileParty, Beaten>();
        private sealed class LootSnapshot { public List<KeyValuePair<MobileParty, Dictionary<ItemObject, int>>> Win; public List<MobileParty> Lose; }
        private sealed class BkSnap { public List<KeyValuePair<MobileParty, Dictionary<EquipmentElement, int>>> Rows; }

        // ------------------------------------------------------------ wpiecie
        private static string _wMission = "nie wpieta", _wDte = "nie wpiete", _wBk = "nie wpiete", _wGift = "nie wpiete";
        private static bool _endWired;
        private static System.Reflection.FieldInfo _playerArmory;
        [ThreadStatic] private static bool _giftRun;

        // ------------------------------------------------------------ liczniki doby
        private static readonly long[] _dDeadMission = new long[Cats], _dDeadAuto = new long[Cats];
        private static long _dMissionOutside, _dMissionRiders, _dMissionHorses, _dObsNew, _dAutoKilled, _dAutoExp, _dAutoNoHorse, _dAutoParties;
        private static long _dLootMoved, _dLootCopies, _dLootCopiesMissing;
        private static long _dSpWinPcs, _dSpWinHorses, _dSpShelfPcs, _dSpShelfHorses, _dSpArmHorses, _dSpOverflow, _dSpLeft, _dSpParties, _dSpNoWhere, _dSpSkip;
        private static long _dBkPcs, _dBkHorses;
        private static long _dVetIn, _dVetNoHorse, _dVetPlayer, _dVetHire, _dVetHireNone, _dVetOut, _dVetFootShown, _dMercTemplate;
        private static long _dGiftHorses, _dGiftFood, _dGiftNoHorse, _dGiftNoFood, _dGiftGold, _dGiftParties, _dGiftNoSeat;
        private static long _dRotHorse, _dRotFoot, _dRotKeep;
        private static int _stumbles, _dStumbles;
        private static readonly HashSet<string> _errSites = new HashSet<string>();

        internal static void Reset()
        {
            _obs = _obsRiders = _obsHorses = 0; _vetPool.Clear(); _pending = null;
            _mDeaths.Clear(); _mRiders = _mHorses = 0; _missionEvent = null; _beaten.Clear();
            ClearDay(); _stumbles = 0; _errSites.Clear(); _giftRun = false;
        }

        private static void ClearDay()
        {
            Array.Clear(_dDeadMission, 0, Cats); Array.Clear(_dDeadAuto, 0, Cats);
            _dMissionOutside = _dMissionRiders = _dMissionHorses = _dObsNew = _dAutoKilled = _dAutoExp = _dAutoNoHorse = _dAutoParties = 0;
            _dLootMoved = _dLootCopies = _dLootCopiesMissing = 0;
            _dSpWinPcs = _dSpWinHorses = _dSpShelfPcs = _dSpShelfHorses = _dSpArmHorses = _dSpOverflow = _dSpLeft = _dSpParties = _dSpNoWhere = _dSpSkip = 0;
            _dBkPcs = _dBkHorses = 0;
            _dVetIn = _dVetNoHorse = _dVetPlayer = _dVetHire = _dVetHireNone = _dVetOut = _dVetFootShown = _dMercTemplate = 0;
            _dGiftHorses = _dGiftFood = _dGiftNoHorse = _dGiftNoFood = _dGiftGold = _dGiftParties = _dGiftNoSeat = 0;
            _dRotHorse = _dRotFoot = _dRotKeep = 0;
            _dStumbles = 0;
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++; _dStumbles++;
            if (_errSites.Add(where)) Log.Error("HorseFate." + where, e);
        }

        // ------------------------------------------------------------ pomocnicze
        private static int Lv(ItemObject it) { return it != null && Stables.IsPlainMount(it) ? Stables.Level(it.ItemCategory) : -1; }
        private static bool IsHorse(ItemObject it) { return it != null && it.ItemType == ItemObject.ItemTypeEnum.Horse; }

        private static int HighFrom
        {
            get { var c = Settings.Current; return c != null ? Math.Min(c.WarHorseFromTier, c.NobleHorseFromTier) : 4; }
        }

        private static int[] OrderFor(CharacterObject ch) { return ch != null && ch.Tier >= HighFrom ? OrdHigh : OrdLow; }

        private static Dictionary<ItemObject, int> ArmOf(MobileParty mp)
        {
            var all = AiGear.Armories();
            Dictionary<ItemObject, int> a;
            return all != null && mp != null && all.TryGetValue(mp.Id, out a) ? a : null;
        }

        private static ItemRoster PlayerArmory()
        {
            try
            {
                if (_playerArmory == null)
                {
                    var t = AccessTools.TypeByName("DynamicTroopEquipmentReupload.ArmyArmory");
                    _playerArmory = t != null ? AccessTools.Field(t, "Armory") : null;
                }
                return _playerArmory != null ? _playerArmory.GetValue(null) as ItemRoster : null;
            }
            catch { return null; }
        }

        /// <summary>Kon wybranego stopnia (kolejnosc order) ze slownika: w stopniu losowo wedlug liczby sztuk. allow[lv] &gt; 0 - limit na stopien (null = bez).</summary>
        private static ItemObject TakeLevel(Dictionary<ItemObject, int> d, int[] order, int[] allow)
        {
            if (d == null) return null;
            foreach (int lv in order)
            {
                if (allow != null && allow[lv] <= 0) continue;
                int tot = 0;
                foreach (var kv in d) if (kv.Value > 0 && Lv(kv.Key) == lv) tot += kv.Value;
                if (tot <= 0) continue;
                int pick = MBRandom.RandomInt(tot);
                ItemObject hit = null;
                foreach (var kv in d)
                {
                    if (kv.Value <= 0 || Lv(kv.Key) != lv) continue;
                    if (pick < kv.Value) { hit = kv.Key; break; }
                    pick -= kv.Value;
                }
                if (hit == null) continue;
                int c = d[hit];
                if (c <= 1) d.Remove(hit); else d[hit] = c - 1;
                if (allow != null) allow[lv]--;
                return hit;
            }
            return null;
        }

        private static int Rnd(double x)
        {
            if (!(x > 0)) return 0;
            int n = (int)Math.Floor(x);
            if (MBRandom.RandomFloat < x - n) n++;
            return n;
        }

        private static int Cat(ItemObject it) { int c = HorseBook.Cat(it); return c < 0 ? 0 : c; }

        /// <summary>Polka targu osady: wies - jej miasto/zamek; tylko osada z Town (miasto, zamek).</summary>
        private static Settlement MarketOf(Settlement st)
        {
            if (st == null) return null;
            if (st.IsVillage && st.Village != null) st = st.Village.Bound;
            return st != null && st.Town != null && st.ItemRoster != null ? st : null;
        }

        /// <summary>Najblizsza warownia (miasto, zamek) tej samej frakcji, nieoblegana; dalej niewroga; na koniec dowolna.</summary>
        private static Settlement NearestMarket(Vec2 pos, IFaction f)
        {
            Settlement b1 = null, b2 = null, b3 = null; float d1 = float.MaxValue, d2 = float.MaxValue, d3 = float.MaxValue;
            foreach (var t in Town.AllFiefs)
            {
                var st = t != null ? t.Settlement : null;
                if (st == null || st.ItemRoster == null) continue;
                float d = pos.IsValid ? pos.DistanceSquared(st.GetPosition2D) : 0f;
                if (d < d3) { d3 = d; b3 = st; }
                if (st.IsUnderSiege) continue;
                bool same = f != null && st.MapFaction == f;
                bool war = f != null && st.MapFaction != null && f.IsAtWarWith(st.MapFaction);
                if (same && d < d1) { d1 = d; b1 = st; }
                if (!war && d < d2) { d2 = d; b2 = st; }
            }
            return b1 ?? b2 ?? b3;
        }

        private static Vec2 PosOf(MobileParty mp)
        {
            try { return mp.GetPosition2D; } catch { return Vec2.Invalid; }
        }

        // ================================================================ (1) MISJA
        internal static void OnMissionInit(Mission m)
        {
            if (m == null || !DieOn || Campaign.Current == null) return;
            try
            {
                m.AddMissionBehavior(new HorseFateMission());
                var me = MapEvent.PlayerMapEvent;
                if (me != null) _missionEvent = me;
                _wMission = "wpieta";
            }
            catch (Exception e) { Stumble("OnMissionInit", e); }
        }

        internal static void NoteMissionHorse(MobileParty owner, ItemObject it)
        {
            if (owner == null || it == null) return;
            _mHorses++;
            _mDeaths.Add(new MDeath { Party = owner, Item = it });
        }

        internal static void NoteMissionRider() { _mRiders++; }

        /// <summary>Postfiks DTE DynamicTroopMissionLogic.OnEndMission (po zwrotach) albo koniec misji bez DTE: zgony koni schodza ze zbrojowni wlascicieli.</summary>
        public static void EndMissionPostfix() { ApplyMission(); }

        internal static void MissionEnded() { if (!_endWired) ApplyMission(); }

        private static void ApplyMission()
        {
            if (_mDeaths.Count == 0 && _mRiders == 0) return;
            try
            {
                var touched = new HashSet<MobileParty>();
                ItemRoster pa = null;
                foreach (var d in _mDeaths)
                {
                    bool ok = false;
                    try
                    {
                        if (d.Party == MobileParty.MainParty)
                        {
                            if (pa == null) pa = PlayerArmory();
                            if (pa != null)
                                for (int i = 0; i < pa.Count; i++)
                                {
                                    var el = pa.GetElementCopyAtIndex(i);
                                    if (el.Amount <= 0 || el.EquipmentElement.Item != d.Item) continue;
                                    pa.AddToCounts(el.EquipmentElement, -1); ok = true; break;
                                }
                        }
                        else
                        {
                            var arm = ArmOf(d.Party);
                            int c;
                            if (arm != null && arm.TryGetValue(d.Item, out c) && c > 0)
                            {
                                if (c <= 1) arm.Remove(d.Item); else arm[d.Item] = c - 1;
                                ok = true; touched.Add(d.Party);
                            }
                        }
                    }
                    catch (Exception e) { Stumble("ApplyMission(kon)", e); }
                    if (ok) _dDeadMission[Cat(d.Item)]++; else _dMissionOutside++;
                }
                if (_mRiders > 0) { _obs++; _obsRiders += _mRiders; _obsHorses += _mHorses; _dObsNew++; }
                _dMissionRiders += _mRiders; _dMissionHorses += _mHorses;
                foreach (var mp in touched) HorseState.Refresh(mp);
            }
            catch (Exception e) { Stumble("ApplyMission", e); }
            finally { _mDeaths.Clear(); _mRiders = _mHorses = 0; }
        }

        /// <summary>W z misji: do HorseDeathRatioMinBattles obserwacji 1.0.</summary>
        internal static double W
        {
            get
            {
                var s = Settings.Current;
                int min = s != null ? Math.Max(1, s.HorseDeathRatioMinBattles) : 50;
                return _obs >= min && _obsRiders > 0 ? (double)_obsHorses / _obsRiders : 1.0;
            }
        }

        // ================================================================ (1) AUTOBITWA I LUP - okno DTE OnMapEventEnded
        public static void DtePrefix(MapEvent __0, out object __state)
        {
            __state = null;
            try
            {
                if (__0 == null) return;
                bool mission = ReferenceEquals(__0, _missionEvent);
                if (mission) _missionEvent = null;
                if (!DieOn) return;
                if (!mission) AutoDeaths(__0);
                __state = LootSnap(__0);
            }
            catch (Exception e) { Stumble("DtePrefix", e); }
        }

        public static void DtePostfix(MapEvent __0, object __state)
        {
            var sn = __state as LootSnapshot;
            if (sn == null) return;
            try { LootMove(sn); } catch (Exception e) { Stumble("DtePostfix", e); }
        }

        private static void Horsed(int[] horses, int high, int low, out int hh, out int hl)
        {
            var h = (int[])horses.Clone();
            int rh = high, rl = low, t;
            t = Math.Min(rh, h[1]); h[1] -= t; rh -= t;
            t = Math.Min(rh, h[2]); h[2] -= t; rh -= t;
            for (int lv = 0; lv < 3; lv++) { t = Math.Min(rl, h[lv]); h[lv] -= t; rl -= t; }
            t = Math.Min(rh, h[0]); h[0] -= t; rh -= t;
            hh = high - rh; hl = low - rl;
        }

        private static void AutoDeaths(MapEvent me)
        {
            double w = W;
            int hf = HighFrom;
            foreach (var side in new[] { me.AttackerSide, me.DefenderSide })
            {
                if (side == null || side.Parties == null) continue;
                foreach (var mep in side.Parties)
                {
                    try
                    {
                        var mp = mep != null && mep.Party != null ? mep.Party.MobileParty : null;
                        if (mp == null || mp.IsMainParty || Undead.Party(mp)) continue;
                        var died = mep.DiedInBattle;
                        if (died == null || died.TotalManCount <= 0) continue;
                        var arm = ArmOf(mp);
                        if (arm == null) continue;
                        int kh = 0, kl = 0;
                        var adj = new Dictionary<CharacterObject, int>();
                        for (int i = 0; i < died.Count; i++)
                        {
                            var ch = died.GetCharacterAtIndex(i);
                            int n = died.GetElementNumber(i);
                            if (ch == null || ch.IsHero || !ch.IsMounted || n <= 0) continue;
                            if (ch.Tier >= hf) kh += n; else kl += n;
                            int a0; adj.TryGetValue(ch, out a0); adj[ch] = a0 + n;
                        }
                        if (kh + kl <= 0) continue;
                        var b = Stables.Balance(mp, adj);   // sklad sprzed bitwy: zabici dopisani
                        int high = b.RidersHigh, low = b.Riders - b.RidersHigh, hh, hl;
                        Horsed(b.Horses, high, low, out hh, out hl);
                        double eh = high > 0 ? kh * w * hh / high : 0.0, el = low > 0 ? kl * w * hl / low : 0.0;
                        int dh = Rnd(eh), dl = Rnd(el), got = 0;
                        for (int k = 0; k < dh; k++) { var it = TakeLevel(arm, OrdHigh, null); if (it == null) break; _dDeadAuto[Cat(it)]++; got++; }
                        for (int k = 0; k < dl; k++) { var it = TakeLevel(arm, OrdLow, null); if (it == null) break; _dDeadAuto[Cat(it)]++; got++; }
                        _dAutoKilled += kh + kl; _dAutoExp += dh + dl; _dAutoNoHorse += (kh + kl) - (int)Math.Round((high > 0 ? (double)kh * hh / high : 0) + (low > 0 ? (double)kl * hl / low : 0));
                        _dAutoParties++;
                        if (got > 0) HorseState.Refresh(mp);
                    }
                    catch (Exception e) { Stumble("AutoDeaths(partia)", e); }
                }
            }
        }

        private static Dictionary<ItemObject, int> HorsesOfWinner(MobileParty mp)
        {
            var d = new Dictionary<ItemObject, int>();
            if (mp == MobileParty.MainParty)
            {
                var pa = PlayerArmory();
                if (pa != null)
                    for (int i = 0; i < pa.Count; i++)
                    {
                        var el = pa.GetElementCopyAtIndex(i);
                        var it = el.EquipmentElement.Item;
                        if (el.Amount <= 0 || !IsHorse(it)) continue;
                        int n; d.TryGetValue(it, out n); d[it] = n + el.Amount;
                    }
                return d;
            }
            var arm = ArmOf(mp);
            if (arm != null) foreach (var kv in arm) if (kv.Value > 0 && IsHorse(kv.Key)) d[kv.Key] = kv.Value;
            return d;
        }

        private static LootSnapshot LootSnap(MapEvent me)
        {
            if (me.WinningSide == BattleSideEnum.None) return null;
            if (me.IsPlayerSimulation && me.WinningSide == me.PlayerSide) return null;   // DTE DistributePlayerSimulationLoot - lup z poleglych, inna droga
            var win = me.WinningSide == BattleSideEnum.Attacker ? me.AttackerSide : me.DefenderSide;
            var lose = me.WinningSide == BattleSideEnum.Attacker ? me.DefenderSide : me.AttackerSide;
            if (win == null || lose == null) return null;
            var sn = new LootSnapshot { Win = new List<KeyValuePair<MobileParty, Dictionary<ItemObject, int>>>(), Lose = new List<MobileParty>() };
            foreach (var mep in lose.Parties)
            {
                var mp = mep != null && mep.Party != null ? mep.Party.MobileParty : null;
                if (mp != null && !mp.IsMainParty && ArmOf(mp) != null) sn.Lose.Add(mp);
            }
            if (sn.Lose.Count == 0) return null;
            foreach (var mep in win.Parties)
            {
                var mp = mep != null && mep.Party != null ? mep.Party.MobileParty : null;
                if (mp != null) sn.Win.Add(new KeyValuePair<MobileParty, Dictionary<ItemObject, int>>(mp, HorsesOfWinner(mp)));
            }
            return sn;
        }

        private static void LootMove(LootSnapshot sn)
        {
            var gained = new Dictionary<ItemObject, int>();
            foreach (var kv in sn.Win)
            {
                var now = HorsesOfWinner(kv.Key);
                foreach (var x in now)
                {
                    int b; kv.Value.TryGetValue(x.Key, out b);
                    if (x.Value > b) { int g; gained.TryGetValue(x.Key, out g); gained[x.Key] = g + (x.Value - b); }
                }
            }
            if (gained.Count == 0) return;
            var touched = new HashSet<MobileParty>();
            foreach (var g in gained)
            {
                int left = g.Value;
                _dLootMoved += g.Value;
                foreach (var mp in sn.Lose)
                {
                    if (left <= 0) break;
                    var arm = ArmOf(mp);
                    int c;
                    if (arm == null || !arm.TryGetValue(g.Key, out c) || c <= 0) continue;
                    int k = Math.Min(c, left);
                    if (c - k > 0) arm[g.Key] = c - k; else arm.Remove(g.Key);
                    left -= k; _dLootCopies += k; touched.Add(mp);
                }
                if (left > 0) _dLootCopiesMissing += left;
            }
            foreach (var mp in touched) HorseState.Refresh(mp);
        }

        // ================================================================ (2) DOBYTEK ROZBITYCH
        /// <summary>Nasluch MapEventEnded (przed HandleMapEventEnd - partie pokonanych jeszcze zyja): zwyciezcy i ich wklad dla kazdej pokonanej partii.</summary>
        internal static void OnMapEventEnded(MapEvent me)
        {
            if (me == null || !SpoilsOn) return;
            try
            {
                if (me.WinningSide == BattleSideEnum.None) return;
                var win = me.WinningSide == BattleSideEnum.Attacker ? me.AttackerSide : me.DefenderSide;
                var lose = me.WinningSide == BattleSideEnum.Attacker ? me.DefenderSide : me.AttackerSide;
                if (win == null || lose == null) return;
                var wins = new List<Win>();
                foreach (var mep in win.Parties) if (mep != null && mep.Party != null) wins.Add(new Win { Party = mep.Party, W = Math.Max(0, mep.ContributionToBattle) });
                var b = new Beaten { Wins = wins, SimWin = me.IsPlayerSimulation && me.WinningSide == me.PlayerSide, Day = (int)CampaignTime.Now.ToDays };
                foreach (var mep in lose.Parties)
                {
                    var mp = mep != null && mep.Party != null ? mep.Party.MobileParty : null;
                    if (mp != null) _beaten[mp] = b;
                }
            }
            catch (Exception e) { Stumble("OnMapEventEnded", e); }
        }

        private sealed class Dest { public MobileParty Party; public Settlement Shelf; public double W; }

        private static Dest DestOf(PartyBase pb, MobileParty victim, double w)
        {
            if (pb == null) return null;
            if (pb.IsSettlement) { var m = MarketOf(pb.Settlement); return m != null ? new Dest { Shelf = m, W = w } : null; }
            var mp = pb.MobileParty;
            if (mp == null || mp == victim || !mp.IsActive || Undead.Party(mp)) return null;   // Inni dobytku nie biora (jak 3.2)
            if (mp.IsGarrison || mp.IsMilitia) { var m = MarketOf(mp.CurrentSettlement ?? mp.HomeSettlement); return m != null ? new Dest { Shelf = m, W = w } : null; }
            if (mp.ItemRoster == null) return null;
            return new Dest { Party = mp, W = w };
        }

        private static void Put(Dest d, EquipmentElement el, int k, bool horse)
        {
            if (k <= 0) return;
            if (d.Party != null)
            {
                bool over = false;
                if (!horse && !d.Party.IsMainParty)
                {
                    try { over = d.Party.TotalWeightCarried + el.Weight * k > d.Party.InventoryCapacity; } catch { over = false; }
                }
                if (!over)
                {
                    d.Party.ItemRoster.AddToCounts(el, k);
                    _dSpWinPcs += k; if (horse) _dSpWinHorses += k;
                    return;
                }
                var m = NearestMarket(PosOf(d.Party), d.Party.MapFaction);
                if (m == null) { _dSpNoWhere += k; return; }
                m.ItemRoster.AddToCounts(el, k);
                _dSpOverflow += k; _dSpShelfPcs += k;
                return;
            }
            d.Shelf.ItemRoster.AddToCounts(el, k);
            _dSpShelfPcs += k; if (horse) _dSpShelfHorses += k;
        }

        /// <summary>Podzial a sztuk wedlug wag (proporcja, reszta najwiekszym ulamkom) - jak podzial lupu zlota.</summary>
        private static int[] Split(int a, List<Dest> ds)
        {
            var k = new int[ds.Count];
            double tw = 0; foreach (var d in ds) tw += d.W;
            if (tw <= 0) { for (int i = 0; i < ds.Count; i++) ds[i].W = 1; tw = ds.Count; }
            int sum = 0;
            var frac = new double[ds.Count];
            for (int i = 0; i < ds.Count; i++) { double x = a * ds[i].W / tw; k[i] = (int)Math.Floor(x); frac[i] = x - k[i]; sum += k[i]; }
            while (sum < a)
            {
                int best = 0;
                for (int i = 1; i < ds.Count; i++) if (frac[i] > frac[best]) best = i;
                k[best]++; frac[best] = -1; sum++;
            }
            return k;
        }

        /// <summary>GoodsLedger.OnPartyDestroyed (przed licznikami "przepadlo"): tabor i konie zbrojowni rozbitej albo rozwiazanej partii - do zwyciezcow albo osady.</summary>
        internal static void Spoils(MobileParty mp, PartyBase destroyer)
        {
            if (mp == null || !SpoilsOn || mp.IsMainParty) return;
            try
            {
                Beaten b;
                bool had = _beaten.TryGetValue(mp, out b);
                if (had) _beaten.Remove(mp);
                bool fromBattle = had && destroyer != null;   // HandleMapEventEnd niszczy z przywodca strony przeciwnej; bez niszczyciela - rozwiazanie, nie lup tej bitwy
                if (!fromBattle && destroyer == null)
                {
                    // bez niszczyciela tylko porzucenie znane: rozwiazanie gry, druzyna lorda, tabor wsi, karawana, banda. Wykluczone wirtualne dostawy BK
                    // (PopulationPartyComponent: towar dopisany miastu/zapasom bez zdjecia z taboru, potem DestroyPartyAction - towar na polce bylby drugi raz)
                    string tn = "";
                    try { tn = mp.PartyComponent != null ? mp.PartyComponent.GetType().FullName ?? "" : ""; } catch { tn = ""; }
                    bool known = mp.IsDisbanding || mp.IsLordParty || mp.IsVillager || mp.IsCaravan || mp.IsBandit;
                    if (!known || tn.Contains("PopulationPartyComponent")) { _dSpSkip++; return; }
                }
                var ds = new List<Dest>();
                if (fromBattle) foreach (var w in b.Wins) { var d = DestOf(w.Party, mp, w.W); if (d != null) ds.Add(d); }
                else if (destroyer != null) { var d = DestOf(destroyer, mp, 1); if (d != null) ds.Add(d); }
                if (ds.Count == 0)
                {
                    // bez zwyciezcy (rozwiazanie, Inni): polka osady rozwiazania albo najblizszej przyjaznej warowni - porzucony dobytek
                    Settlement at = null;
                    try { if (mp.IsDisbanding && mp.TargetSettlement != null) at = MarketOf(mp.TargetSettlement); } catch { }
                    if (at == null) at = MarketOf(mp.CurrentSettlement);
                    if (at == null) at = NearestMarket(PosOf(mp), mp.MapFaction);
                    if (at != null) ds.Add(new Dest { Shelf = at, W = 1 });
                }
                // tabor
                var r = mp.ItemRoster;
                var moves = new List<KeyValuePair<EquipmentElement, int>>();
                if (r != null)
                    for (int i = 0; i < r.Count; i++)
                    {
                        var el = r.GetElementCopyAtIndex(i);
                        var it = el.EquipmentElement.Item;
                        if (el.Amount <= 0 || it == null) continue;
                        if (it.NotMerchandise || it.IsBannerItem || el.EquipmentElement.IsQuestItem || UniqueSpoils.Is(it)) { _dSpLeft += el.Amount; continue; }
                        moves.Add(new KeyValuePair<EquipmentElement, int>(el.EquipmentElement, el.Amount));
                    }
                // konie zbrojowni DTE (jesli DTE jeszcze jej nie zdjal); bitwa autorozstrzygniecia gracza - lup DTE z poleglych juz byl (bez drugiego)
                // przy bitwie bez okna lupu K1 (HorsesDieOnlyKilled wylaczone) DTE skopiowal zbrojownie zwyciezcom - bez drugiego lupu (jak dotad DTE zdejmie ja z partia)
                var arm = (fromBattle && (b.SimWin || !DieOn)) ? null : ArmOf(mp);
                var armMoves = new List<KeyValuePair<ItemObject, int>>();
                if (arm != null) foreach (var kv in arm) if (kv.Value > 0 && IsHorse(kv.Key)) armMoves.Add(new KeyValuePair<ItemObject, int>(kv.Key, kv.Value));
                if (moves.Count == 0 && armMoves.Count == 0) return;
                if (ds.Count == 0) { foreach (var m in moves) _dSpNoWhere += m.Value; return; }
                foreach (var m in moves)
                {
                    bool horse = IsHorse(m.Key.Item);
                    var k = Split(m.Value, ds);
                    r.AddToCounts(m.Key, -m.Value);
                    for (int i = 0; i < ds.Count; i++) Put(ds[i], m.Key, k[i], horse);
                }
                foreach (var m in armMoves)
                {
                    var k = Split(m.Value, ds);
                    arm.Remove(m.Key);
                    var el = new EquipmentElement(m.Key);
                    for (int i = 0; i < ds.Count; i++) Put(ds[i], el, k[i], true);
                    _dSpArmHorses += m.Value;
                }
                _dSpParties++;
            }
            catch (Exception e) { Stumble("Spoils", e); }
        }

        // BK BKCaravansBehavior.OnMapEventEnded: zwycieska karawana wyrzuca zwierzeta ponad ludzi i towar ponad udzwig - na polke najblizszej przyjaznej warowni
        public static void BkPrefix(MapEvent __0, out object __state)
        {
            __state = null;
            try
            {
                if (__0 == null || !SpoilsOn) return;
                var sn = new BkSnap { Rows = new List<KeyValuePair<MobileParty, Dictionary<EquipmentElement, int>>>() };
                foreach (var pb in __0.InvolvedParties)
                {
                    var mp = pb != null ? pb.MobileParty : null;
                    if (mp == null || !mp.IsCaravan || mp.ItemRoster == null) continue;
                    var d = new Dictionary<EquipmentElement, int>();
                    for (int i = 0; i < mp.ItemRoster.Count; i++)
                    {
                        var el = mp.ItemRoster.GetElementCopyAtIndex(i);
                        if (el.Amount > 0) { int n; d.TryGetValue(el.EquipmentElement, out n); d[el.EquipmentElement] = n + el.Amount; }
                    }
                    sn.Rows.Add(new KeyValuePair<MobileParty, Dictionary<EquipmentElement, int>>(mp, d));
                }
                if (sn.Rows.Count > 0) __state = sn;
            }
            catch (Exception e) { Stumble("BkPrefix", e); }
        }

        public static void BkPostfix(object __state)
        {
            var sn = __state as BkSnap;
            if (sn == null) return;
            try
            {
                foreach (var row in sn.Rows)
                {
                    var mp = row.Key;
                    if (mp == null || mp.ItemRoster == null) continue;
                    Settlement m = null;
                    foreach (var kv in row.Value)
                    {
                        int ix = mp.ItemRoster.FindIndexOfElement(kv.Key);
                        int now = ix >= 0 ? mp.ItemRoster.GetElementNumber(ix) : 0;
                        int gone = kv.Value - now;
                        if (gone <= 0) continue;
                        if (m == null) m = NearestMarket(PosOf(mp), mp.MapFaction);
                        if (m == null) { _dSpNoWhere += gone; continue; }
                        m.ItemRoster.AddToCounts(kv.Key, gone);
                        _dBkPcs += gone; if (IsHorse(kv.Key.Item)) _dBkHorses += gone;
                    }
                }
            }
            catch (Exception e) { Stumble("BkPostfix", e); }
        }

        // ================================================================ (3) KON WETERANA
        private static Dictionary<ItemObject, int> PoolOf(Settlement town, bool make)
        {
            if (town == null || town.StringId == null) return null;
            EnsureLoaded();
            Dictionary<ItemObject, int> p;
            if (!_vetPool.TryGetValue(town.StringId, out p) && make) { p = new Dictionary<ItemObject, int>(); _vetPool[town.StringId] = p; }
            return p;
        }

        /// <summary>Konie w puli karczmy miasta (Veterans.Sync - typ konny tylko do tej liczby).</summary>
        internal static int VetHorsesIn(Settlement town)
        {
            var p = PoolOf(town, false);
            int n = 0;
            if (p != null) foreach (var v in p.Values) n += Math.Max(0, v);
            return n;
        }

        internal static void NoteVetFootShown() { _dVetFootShown++; }
        internal static void NoteMercTemplateHorse(int n) { if (n > 0) _dMercTemplate += n; }

        /// <summary>PeopleBook.VetCommit (klasa konna, t ludzi w karczmie): kazdy bierze wolnego konia swojej kategorii ze zbrojowni partii do puli miasta.</summary>
        internal static void VetTakeHorses(MobileParty party, CharacterObject troop, Settlement town, int n)
        {
            if (n <= 0 || troop == null || town == null || !VetHorseOn) return;
            try
            {
                if (party == null) { _dVetNoHorse += n; return; }
                if (party.IsMainParty) { _dVetPlayer += n; return; }   // zbrojownia gracza nietknieta
                var arm = ArmOf(party);
                if (arm == null) { _dVetNoHorse += n; return; }
                var b = Stables.Balance(party, new Dictionary<CharacterObject, int> { { troop, -n } });   // wolne bez odchodzacych
                var allow = new[] { b.Free[0], b.Free[1], b.Free[2] };
                var pool = PoolOf(town, true);
                int got = 0;
                for (int k = 0; k < n; k++)
                {
                    var it = TakeLevel(arm, OrderFor(troop), allow);
                    if (it == null) break;
                    int c; pool.TryGetValue(it, out c); pool[it] = c + 1;
                    got++;
                }
                _dVetIn += got; _dVetNoHorse += n - got;
                if (got > 0) HorseState.Refresh(party);
            }
            catch (Exception e) { Stumble("VetTakeHorses", e); }
        }

        /// <summary>RecruitKit (najem konnego najemnika przez AI): kon z puli miasta (kategoria wedlug tieru) albo null.</summary>
        internal static ItemObject VetHireHorse(Settlement town, CharacterObject troop)
        {
            try
            {
                var p = PoolOf(town, false);
                var it = TakeLevel(p, OrderFor(troop), null);
                if (it == null && p != null)
                {
                    // egzotyczny albo juczny w puli (spoza IsPlainMount) - pierwszy z brzegu
                    foreach (var kv in p) if (kv.Value > 0) { it = kv.Key; break; }
                    if (it != null) { int c = p[it]; if (c <= 1) p.Remove(it); else p[it] = c - 1; }
                }
                if (it != null) _dVetHire++; else _dVetHireNone++;
                return it;
            }
            catch (Exception e) { Stumble("VetHireHorse", e); return null; }
        }

        /// <summary>Raz na dobe: konie ponad konnych weteranow miasta (odeszli do domu, najal ich gracz z kompletem DTE) - na polke miasta (porzucone).
        /// Wylaczony klucz - cala pula na polki (nic w nicosc).</summary>
        private static void VetPoolDaily()
        {
            EnsureLoaded();
            if (_vetPool.Count == 0) return;
            bool on = VetHorseOn;
            var drop = new List<string>();
            foreach (var kv in _vetPool)
            {
                try
                {
                    var st = Settlement.Find(kv.Key);
                    int total = 0; foreach (var v in kv.Value.Values) total += Math.Max(0, v);
                    if (total <= 0) { drop.Add(kv.Key); continue; }
                    int mounted = 0, old;
                    if (on && st != null) PeopleBook.VetInTown(st, out mounted, out old);
                    int excess = total - (on ? mounted : 0);
                    if (excess <= 0) continue;
                    var m = MarketOf(st);
                    if (m == null) continue;
                    var cheap = new List<ItemObject>(kv.Value.Keys);
                    cheap.Sort((a, b) => a.Value.CompareTo(b.Value));
                    foreach (var it in cheap)
                    {
                        if (excess <= 0) break;
                        int c = kv.Value[it];
                        int k = Math.Min(c, excess);
                        if (k <= 0) continue;
                        if (c - k > 0) kv.Value[it] = c - k; else kv.Value.Remove(it);
                        m.ItemRoster.AddToCounts(new EquipmentElement(it), k);
                        excess -= k; _dVetOut += k;
                    }
                    if (kv.Value.Count == 0) drop.Add(kv.Key);
                }
                catch (Exception e) { Stumble("VetPoolDaily", e); }
            }
            foreach (var k in drop) _vetPool.Remove(k);
        }

        /// <summary>HorseBook (stan koni): konie zbrojowni gracza (DTE ArmyArmory) wedlug kategorii - zgon konia gracza w misji schodzi ze stanu (ujscie), lup DTE
        /// dla gracza i zdjecie kopii u pokonanych AI - przeniesienie w stanie.</summary>
        internal static void PlayerArmoryCats(long[] dst)
        {
            var pa = PlayerArmory();
            if (pa == null) return;
            for (int i = 0; i < pa.Count; i++)
            {
                var el = pa.GetElementCopyAtIndex(i);
                int c = HorseBook.Cat(el.EquipmentElement.Item);
                if (c >= 0 && el.Amount > 0) dst[c] += el.Amount;
            }
        }

        /// <summary>HorseBook (stan koni): konie w pulach karczm wedlug kategorii.</summary>
        internal static void VetPoolCats(long[] dst)
        {
            EnsureLoaded();
            foreach (var p in _vetPool.Values) foreach (var kv in p) { int c = HorseBook.Cat(kv.Key); if (c >= 0 && kv.Value > 0) dst[c] += kv.Value; }
        }

        // ================================================================ (4) DARY STARTOWE Z POLKI SIEDZIBY
        /// <summary>ArmsFromPeople.GiftsFin: true - dary obsluzyl K1 (zakup z polki, nie dar z niczego) - licznik darow E4/8 bez tej sztuki. Odczyt zeruje.</summary>
        internal static bool TakeGiftRun() { bool r = _giftRun; _giftRun = false; return r; }

        public static bool GiftPrefix(MobileParty __0)
        {
            if (__0 == null || !SeatOn) return true;
            if (Campaign.Current == null || !Campaign.Current.GameStarted) return true;   // nowa kampania - dorobek startowy jak w grze (stan wyjsciowy ksiegi), jak SpawnGrain
            _giftRun = true;
            try { SeatGifts(__0); } catch (Exception e) { Stumble("GiftPrefix", e); }   // wyjatek - w strone braku (gra nie daje z niczego)
            return false;
        }

        private static void SeatGifts(MobileParty party)
        {
            _dGiftParties++;
            // te same liczby co gra (G HeroSpawnCampaignBehavior.GiveInitialItemsToParty): wierzchowce x7, zywnosc x0.1 produkcji wsi w zasiegu
            var want = new Dictionary<ItemObject, int>();
            float range = 2f * Campaign.Current.EstimatedAverageLordPartySpeed * (float)CampaignTime.HoursInDay;
            foreach (var st in Campaign.Current.Settlements)
            {
                if (st == null || !st.IsVillage || st.Village == null || st.Village.VillageType == null) continue;
                float ratio;
                float dist = Campaign.Current.Models.MapDistanceModel.GetDistance(party, st, false, party.NavigationCapability, out ratio);
                if (!(dist < range)) continue;
                foreach (var pr in st.Village.VillageType.Productions)
                {
                    var it = pr.Item1;
                    if (it == null) continue;
                    float f = (it.ItemType == ItemObject.ItemTypeEnum.Horse && it.HorseComponent != null && it.HorseComponent.IsRideable && !it.HorseComponent.IsPackAnimal) ? 7f : (it.IsFood ? 0.1f : 0f);
                    if (f <= 0f) continue;
                    float men = ((float)party.MemberRoster.TotalManCount + 2f) / 200f;
                    int n = MBRandom.RoundRandomized(f * pr.Item2 * (1f - dist / range) * men);
                    if (n > 0) { int c; want.TryGetValue(it, out c); want[it] = c + n; }
                }
            }
            if (want.Count == 0) return;
            var lord = party.LeaderHero;
            var clan = party.ActualClan ?? (lord != null ? lord.Clan : null);
            Settlement seat = null;
            try { seat = clan != null ? ClanBudget.Seat(clan) : null; } catch { seat = null; }
            if (seat != null && (seat.IsUnderSiege || seat.Town == null || seat.ItemRoster == null || (party.MapFaction != null && seat.MapFaction != null && party.MapFaction.IsAtWarWith(seat.MapFaction)))) seat = null;
            if (seat == null || lord == null)
            {
                _dGiftNoSeat++;
                foreach (var kv in want) { if (IsHorse(kv.Key)) _dGiftNoHorse += kv.Value; else _dGiftNoFood += kv.Value; }
                return;
            }
            int budget = Math.Max(0, lord.Gold);
            int cap = 0;
            bool capped = lord.Clan != null && ClanBudget.GearCap(lord.Clan, out cap);
            if (capped) budget = Math.Min(budget, cap);
            int paid = 0;
            var shelf = seat.ItemRoster;
            // konie najpierw, potem zywnosc
            var order = new List<KeyValuePair<ItemObject, int>>(want);
            order.Sort((a, b) => (IsHorse(b.Key) ? 1 : 0).CompareTo(IsHorse(a.Key) ? 1 : 0));
            foreach (var kv in order)
            {
                bool horse = IsHorse(kv.Key);
                int need = kv.Value;
                // ta sama sztuka, potem ta sama kategoria (kon pod siodlo tej samej kategorii; zywnosc tej samej kategorii)
                for (int pass = 0; pass < 2 && need > 0; pass++)
                {
                    for (int i = 0; i < shelf.Count && need > 0; i++)
                    {
                        var el = shelf.GetElementCopyAtIndex(i);
                        var it = el.EquipmentElement.Item;
                        if (el.Amount <= 0 || it == null) continue;
                        bool match = pass == 0 ? it == kv.Key
                                               : it != kv.Key && it.ItemCategory == kv.Key.ItemCategory && (!horse || (Stables.IsPlainMount(it) && it.HorseComponent != null && it.HorseComponent.IsRideable));
                        if (!match) continue;
                        int left = budget - paid;
                        if (left <= 0) break;
                        int cost, first, last;
                        var cel = el.EquipmentElement;
                        int took = ShelfBuy.Take(shelf, cel, Math.Min(need, el.Amount), left, () => Stables.ShelfPrice(seat, cel), out cost, out first, out last);
                        if (took <= 0) continue;
                        party.ItemRoster.AddToCounts(cel, took);
                        paid += cost; need -= took;
                        if (horse) _dGiftHorses += took; else _dGiftFood += took;
                        i = -1;   // polka sie zmienila (usuniety wiersz) - od nowa
                    }
                }
                if (need > 0) { if (horse) _dGiftNoHorse += need; else _dGiftNoFood += need; }
            }
            if (paid > 0)
            {
                GiveGoldAction.ApplyForCharacterToSettlement(lord, seat, paid);
                if (capped && lord.Clan != null) ClanBudget.GearSpent(lord.Clan, paid);
                _dGiftGold += paid;
            }
        }

        // ================================================================ (5) ZAMIANY ROT (liczniki - RotHorseGuard)
        internal static void NoteRot(int horse, int foot, int keep) { _dRotHorse += horse; _dRotFoot += foot; _dRotKeep += keep; }

        // ================================================================ ksiega koni (HorseBook) i linia doby
        /// <summary>Zgony koni dnia (zdjete ze zbrojowni - ujscie w ksiedze) wedlug kategorii.</summary>
        internal static void DeathsCats(long[] dst)
        {
            for (int i = 0; i < Cats; i++) dst[i] += _dDeadMission[i] + _dDeadAuto[i];
        }

        private static string Row(long[] a) { var p = new string[Cats]; for (int i = 0; i < Cats; i++) p[i] = a[i].ToString(CultureInfo.InvariantCulture); return string.Join("/", p); }

        /// <summary>Pozycje K1 w linii "Konie (E6/0)" (przeniesienia - informacyjnie, poza mianownikiem).</summary>
        internal static string BookPart()
        {
            long dm = 0, da = 0; for (int i = 0; i < Cats; i++) { dm += _dDeadMission[i]; da += _dDeadAuto[i]; }
            return "polegle (misja " + dm + " / autobitwa " + da + ", W " + W.ToString("0.00", CultureInfo.InvariantCulture) + ") " + Row(Sum(_dDeadMission, _dDeadAuto))
                   + " | przeniesienia: lup zwyciezcow " + _dLootMoved + " (kopie DTE zdjete u pokonanych " + _dLootCopies + "), dobytek rozbitych do zwyciezcow " + _dSpWinHorses
                   + ", porzucone do osad " + (_dSpShelfHorses + _dBkHorses) + ", pula weteranow +" + _dVetIn + "/-" + (_dVetHire + _dVetOut)
                   + ", dary startowe z polki: konie " + _dGiftHorses + ", zywnosc " + _dGiftFood + " / brak " + _dGiftNoHorse + "/" + _dGiftNoFood;
        }

        private static long[] Sum(long[] a, long[] b) { var r = new long[Cats]; for (int i = 0; i < Cats; i++) r[i] = a[i] + b[i]; return r; }

        internal static void Daily()
        {
            try { VetPoolDaily(); } catch (Exception e) { Stumble("Daily(pula)", e); }
            try
            {
                int day = (int)CampaignTime.Now.ToDays;
                // migawki zwyciezcow starsze niz doba - partia nie zostala zniszczona w tej bitwie
                var old = new List<MobileParty>();
                foreach (var kv in _beaten) if (kv.Value.Day < day - 1) old.Add(kv.Key);
                foreach (var k in old) _beaten.Remove(k);
                var s = Settings.Current;
                string yn(bool b) => b ? "TAK" : "NIE";
                bool any = s != null && (s.HorsesDieOnlyKilled || s.SpoilsOfBrokenParties || s.VeteranKeepsHorse || s.NewPartyHorsesFromSeat || s.TroopSwapNeedsHorse);
                long pool = 0; foreach (var p in _vetPool.Values) foreach (var v in p.Values) pool += Math.Max(0, v);
                int min = s != null ? Math.Max(1, s.HorseDeathRatioMinBattles) : 50;
                var sb = new StringBuilder();
                sb.Append("Konie - smierc, lup, dobytek (E6/K1): dzien ").Append(day).Append(any ? " | CZYNNE" : " | WYLACZONE (wszystkie klucze K1)")
                  .Append(": konie gina zabite ").Append(yn(DieOn)).Append(", dobytek rozbitych ").Append(yn(SpoilsOn)).Append(", kon weterana ").Append(yn(VetHorseOn))
                  .Append(", dary z polki siedziby ").Append(yn(SeatOn)).Append(", zamiany ROT ").Append(yn(s != null && s.TroopSwapNeedsHorse && RotHorseGuard.AllOn))
                  .Append(" | polegle w misji (zdjete ze zbrojowni) ").Append(Row(_dDeadMission)).Append(", poza zbrojownia wlasciciela ").Append(_dMissionOutside)
                  .Append(" (zgony koni ").Append(_dMissionHorses).Append(" / jezdzcow ").Append(_dMissionRiders).Append(", nowych obserwacji ").Append(_dObsNew).Append(")")
                  .Append(" | polegle w autobitwie ").Append(Row(_dDeadAuto)).Append(" (zabitych jezdzcow ").Append(_dAutoKilled).Append(", bez konia ok. ").Append(_dAutoNoHorse)
                  .Append(", wylosowanych zgonow ").Append(_dAutoExp).Append(", partii ").Append(_dAutoParties).Append(")")
                  .Append(" | W ").Append(W.ToString("0.000", CultureInfo.InvariantCulture)).Append(" (obserwacji ").Append(_obs).Append("/").Append(min)
                  .Append(", z misji ").Append(_obsRiders > 0 ? ((double)_obsHorses / _obsRiders).ToString("0.000", CultureInfo.InvariantCulture) : "-").Append(")")
                  .Append(" | lup DTE: dopisane zwyciezcom ").Append(_dLootMoved).Append(", kopie zdjete u pokonanych ").Append(_dLootCopies).Append(", brak u pokonanych ").Append(_dLootCopiesMissing)
                  .Append(" | dobytek rozbitych (partii ").Append(_dSpParties).Append("): do zwyciezcow sztuk ").Append(_dSpWinPcs).Append(" (koni ").Append(_dSpWinHorses)
                  .Append("), na polki osad ").Append(_dSpShelfPcs).Append(" (koni ").Append(_dSpShelfHorses).Append(", ponad udzwig ").Append(_dSpOverflow)
                  .Append("), w tym konie zbrojowni rozbitych ").Append(_dSpArmHorses).Append(", BK karawany na polki ").Append(_dBkPcs).Append(" (koni ").Append(_dBkHorses)
                  .Append("), zostalo w taborze (unikaty, nie-towar) ").Append(_dSpLeft).Append(", bez miejsca ").Append(_dSpNoWhere)
                  .Append(", pominiete partie bez niszczyciela (inne mody, dostawy BK) ").Append(_dSpSkip)
                  .Append(" | pula weteranow: +").Append(_dVetIn).Append(" (bez konia ").Append(_dVetNoHorse).Append(", gracz bez zmian ").Append(_dVetPlayer).Append(") / -")
                  .Append(_dVetHire + _dVetOut).Append(" (najem ").Append(_dVetHire).Append(", na polki ").Append(_dVetOut).Append("), stan ").Append(pool)
                  .Append(", karczma z typem pieszym zamiast konnego ").Append(_dVetFootShown).Append(" | najemnik: kon z puli ").Append(_dVetHire)
                  .Append(", bez konia ").Append(_dVetHireNone).Append(", kon z wzorca ").Append(_dMercTemplate)
                  .Append(" | dary startowe (partii ").Append(_dGiftParties).Append("): z polki konie ").Append(_dGiftHorses).Append(", zywnosc ").Append(_dGiftFood)
                  .Append(", brak koni ").Append(_dGiftNoHorse).Append(", brak zywnosci ").Append(_dGiftNoFood).Append(", bez siedziby ").Append(_dGiftNoSeat)
                  .Append(", zloto do siedzib ").Append(_dGiftGold).Append(", z niczego 0")
                  .Append(" | zamiany ROT na konnego: z koniem ").Append(_dRotHorse).Append(", na pieszego ").Append(_dRotFoot).Append(", zostal konny bez konia ").Append(_dRotKeep)
                  .Append(" | wpiecie: misja ").Append(_wMission).Append(", okno DTE ").Append(_wDte).Append(", BK karawany ").Append(_wBk).Append(", dary ").Append(_wGift)
                  .Append(" | potkniecia dzis ").Append(_dStumbles).Append(" (razem ").Append(_stumbles).Append(").");
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Stumble("Daily", e); }
            ClearDay();
        }

        // ================================================================ zapis
        internal static string Export()
        {
            EnsureLoaded();
            var sb = new StringBuilder();
            sb.Append("w:").Append(_obs).Append(':').Append(_obsRiders).Append(':').Append(_obsHorses).Append('\n');
            foreach (var kv in _vetPool)
            {
                if (kv.Value.Count == 0) continue;
                sb.Append("p:").Append(kv.Key).Append(':');
                bool first = true;
                foreach (var x in kv.Value)
                {
                    if (x.Key == null || x.Value <= 0) continue;
                    if (!first) sb.Append(',');
                    sb.Append(x.Key.StringId).Append('=').Append(x.Value);
                    first = false;
                }
                sb.Append('\n');
            }
            return sb.ToString();
        }

        internal static void Import(string data)
        {
            _pending = data;
            _obs = _obsRiders = _obsHorses = 0; _vetPool.Clear();
        }

        /// <summary>Rozwiazanie zapisu przy pierwszym uzyciu (przedmioty po wczytaniu - jak RecruitKit _pending).</summary>
        private static void EnsureLoaded()
        {
            if (_pending == null) return;
            string data = _pending; _pending = null;
            try
            {
                foreach (var line in data.Split('\n'))
                {
                    if (line.Length < 2) continue;
                    if (line.StartsWith("w:"))
                    {
                        var p = line.Split(':');
                        if (p.Length >= 4)
                        {
                            long.TryParse(p[1], out _obs); long.TryParse(p[2], out _obsRiders); long.TryParse(p[3], out _obsHorses);
                        }
                    }
                    else if (line.StartsWith("p:"))
                    {
                        int a = line.IndexOf(':', 2);
                        if (a < 0) continue;
                        string town = line.Substring(2, a - 2);
                        var d = new Dictionary<ItemObject, int>();
                        foreach (var pair in line.Substring(a + 1).Split(','))
                        {
                            int e = pair.IndexOf('=');
                            if (e <= 0) continue;
                            int n;
                            if (!int.TryParse(pair.Substring(e + 1), out n) || n <= 0) continue;
                            var it = MBObjectManager.Instance.GetObject<ItemObject>(pair.Substring(0, e));
                            if (it == null) continue;
                            int c; d.TryGetValue(it, out c); d[it] = c + n;
                        }
                        if (d.Count > 0) _vetPool[town] = d;
                    }
                }
            }
            catch (Exception e) { Stumble("EnsureLoaded", e); }
        }

        // ================================================================ latki
        internal static void ApplyAll(Harmony h)
        {
            try
            {
                var tEvery = AccessTools.TypeByName("DynamicTroopEquipmentReupload.EveryoneCampaignBehavior");
                var m = tEvery != null ? AccessTools.Method(tEvery, "OnMapEventEnded", new[] { typeof(MapEvent) }) : null;
                if (m != null)
                {
                    h.Patch(m, prefix: new HarmonyMethod(typeof(HorseFate), nameof(DtePrefix)) { priority = Priority.First },
                            postfix: new HarmonyMethod(typeof(HorseFate), nameof(DtePostfix)) { priority = Priority.Last });
                    _wDte = "wpiete";
                }
                else _wDte = "BRAK (DTE EveryoneCampaignBehavior.OnMapEventEnded)";
            }
            catch (Exception e) { _wDte = "BLAD"; Log.Error("HorseFate.ApplyAll(DTE okno)", e); }
            try
            {
                var tLogic = AccessTools.TypeByName("DynamicTroopEquipmentReupload.DynamicTroopMissionLogic");
                var m = tLogic != null ? AccessTools.Method(tLogic, "OnEndMission") : null;
                if (m != null) { h.Patch(m, postfix: new HarmonyMethod(typeof(HorseFate), nameof(EndMissionPostfix))); _endWired = true; }
            }
            catch (Exception e) { Log.Error("HorseFate.ApplyAll(DTE koniec misji)", e); }
            try
            {
                var tBk = AccessTools.TypeByName("BannerKings.Behaviours.BKCaravansBehavior");
                var m = tBk != null ? AccessTools.Method(tBk, "OnMapEventEnded", new[] { typeof(MapEvent) }) : null;
                if (m != null)
                {
                    h.Patch(m, prefix: new HarmonyMethod(typeof(HorseFate), nameof(BkPrefix)), postfix: new HarmonyMethod(typeof(HorseFate), nameof(BkPostfix)));
                    _wBk = "wpiete";
                }
                else _wBk = "BRAK (BK BKCaravansBehavior.OnMapEventEnded)";
            }
            catch (Exception e) { _wBk = "BLAD"; Log.Error("HorseFate.ApplyAll(BK karawany)", e); }
            try
            {
                var m = AccessTools.Method(typeof(HeroSpawnCampaignBehavior), "GiveInitialItemsToParty", new[] { typeof(MobileParty) });
                if (m != null)
                {
                    // Last: po prefiksie pomiaru E4/8 (ArmsFromPeople.GiftsPre - migawka taboru); jego finalizer pyta TakeGiftRun
                    h.Patch(m, prefix: new HarmonyMethod(typeof(HorseFate), nameof(GiftPrefix)) { priority = Priority.Last });
                    _wGift = "wpiete";
                }
                else _wGift = "BRAK (GiveInitialItemsToParty)";
            }
            catch (Exception e) { _wGift = "BLAD"; Log.Error("HorseFate.ApplyAll(dary)", e); }
        }
    }

    /// <summary>E6/K1 (211): zgony koni w misji - kon kazdego jezdzca-szeregowego partii pamietany przy spawnie; agent konia zabity = zgon (HorseFate).</summary>
    internal sealed class HorseFateMission : MissionLogic
    {
        private sealed class Rec { public MobileParty Party; public ItemObject Item; }
        private readonly Dictionary<Agent, Rec> _mounts = new Dictionary<Agent, Rec>();
        private readonly HashSet<Agent> _riders = new HashSet<Agent>();

        private static MobileParty PartyOf(Agent a)
        {
            try
            {
                var pb = a.Origin != null ? a.Origin.BattleCombatant as PartyBase : null;
                return pb != null ? pb.MobileParty : null;
            }
            catch { return null; }
        }

        public override void OnAgentBuild(Agent agent, Banner banner)
        {
            try
            {
                if (agent == null || !agent.IsHuman || agent.MountAgent == null) return;
                if (agent.Character == null || agent.Character.IsHero) return;   // konie bohaterow - ich rynsztunek, poza zbrojownia
                var mp = PartyOf(agent);
                if (mp == null) return;
                var it = agent.SpawnEquipment[EquipmentIndex.Horse].Item;
                if (it == null) return;
                _mounts[agent.MountAgent] = new Rec { Party = mp, Item = it };
                _riders.Add(agent);
            }
            catch { }
        }

        public override void OnAgentRemoved(Agent affectedAgent, Agent affectorAgent, AgentState agentState, KillingBlow blow)
        {
            try
            {
                if (affectedAgent == null) return;
                if (affectedAgent.IsMount)
                {
                    Rec r;
                    if (_mounts.TryGetValue(affectedAgent, out r))
                    {
                        if (agentState == AgentState.Killed) HorseFate.NoteMissionHorse(r.Party, r.Item);
                        _mounts.Remove(affectedAgent);
                    }
                    return;
                }
                if (_riders.Remove(affectedAgent) && agentState == AgentState.Killed) HorseFate.NoteMissionRider();
            }
            catch { }
        }

        protected override void OnEndMission()
        {
            try { HorseFate.MissionEnded(); } catch { }
            _mounts.Clear(); _riders.Clear();
            base.OnEndMission();
        }
    }
}
