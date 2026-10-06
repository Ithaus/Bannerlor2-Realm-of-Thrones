using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// PRAWO WYRZUTKOW (Jeff 04.10, docs/AUDYT-BANDYCI.md P3+P4+P5): "bandy rekrutuja sie z ludzi,
    /// nie z kosmosu; maja sprzet, jaki maja, nic za darmo; im wiekszy dobrobyt i pokoj, tym mniej band;
    /// spalone wioski, glod i wojna - wiecej; powstaja z dezerterow, nieoplaconych najemnikow, przestepcow;
    /// moga werbowac jencow i chlopow".
    ///
    /// PULA WYRZUTKOW na region (miasto/zamek + jego wsie). Ludzie w puli to prawdziwi ludzie:
    ///  - dezerterzy (CampaignEvents.OnTroopsDeserted: brak zoldu, zbyt duza partia, niezaplacone zalogi)
    ///    - zostaja w swoim typie zolnierza, czyli w swoim sprzecie;
    ///  - rozbitkowie z bitew (RoutedInBattle - vanilla ich kasuje): czesc `OutlawRoutedShare` idzie w las;
    ///  - wygnani przez rabunek (VillageLooted) i bieda (codziennie: niski dobrobyt, glod, wojna,
    ///    spalone wsie, niskie bezpieczenstwo) - prosci ludzie ZABIERANI z hearth wsi regionu;
    ///  - jency wypuszczeni przez rozbita bande i ludzie rozwiazanych band (MobilePartyDestroyed bez sprawcy).
    /// Odplyw: powrot do wsi (hearth z powrotem) - szybszy w pokoju i dobrobycie; werbunek do band.
    ///
    /// BANDY: kazde narodziny bandy z szablonu (vanilla noc, NavalDLC piraci, BK bohaterowie i kryjowki)
    /// przechodza przez FindAppropriateInitialRosterForMobileParty - podmieniamy sklad na ludzi z puli
    /// regionu kryjowki/osady bandy (i sasiednich). Bramki na spawnerach: nie ma ludzi = nie ma bandy.
    /// Vanillowi "dezerterzy" ze zmarlych (DiedInBattle) wylaczeni - rozbitkowie ida do puli.
    /// Bandy codziennie werbuja: jencow (zostaja w swoim sprzecie) i ludzi z puli regionu.
    ///
    /// SPRZET: awans bandyty tylko ze zdobytym sprzetem - zbroja/kon z lupu bandy (ItemRoster) albo
    /// kupione u pasera w miescie za zloto bandy (zloto idzie do miasta, towar schodzi z targu).
    /// Zloto band: koniec darmowego dosypywania (vanilla 0.95g + 2.5/czlowiek dziennie) i startowych
    /// 10/czlowieka - bandy maja to, co zrabuja.
    ///
    /// PASER W OBIE STRONY (Jeff 05.10: "bandy maja sprzedawac zrabowany ladunek u pasera w miescie"; Jeff 06.10: "nie moze
    /// byc tak, ze sa bandyci, ktorzy nie moga opchnac towaru", "po co czesc band siedzi na skarbach jak smok w jaskini",
    /// "to ma byc to, co sie oplaca w danej chwili"): KAZDA banda ma pasera - nieoblezone miasta w promieniu
    /// OutlawFenceRadius, a gdy w promieniu nie ma zadnego, najblizsze otwarte miasto na warunkach z granicy promienia.
    /// Raz na dobe banda sprzedaje z jukow towary handlowe, zwierzeta hodowlane i zywnosc, sztuka po sztuce temu miastu,
    /// ktore za NASTEPNA sztuke da jej najwiecej na reke: cena skupu miasta x udzial bandy, a udzial maleje z odlegloscia
    /// (droga i ryzyko posrednika; marza zostaje w kasie miasta jako nizsza zaplata). Miasto placi z kasy ponad rezerwe na
    /// renty; gdy jednego nie stac, kupuje nastepne. Zwierzeta ponad potrzebe (juczne ponad to, co trzeba uniesc, konie
    /// ponad jednego na pieszego) tez ida na sprzedaz. Kupno sprzetu: najtansza sztuka z dostawa posrod miast w zasiegu
    /// (wartosc x marza rosnaca z odlegloscia). Zbroje, bron, helmy i tarcze zostaja w jukach - z pomiarem.
    /// ZYWNOSC: bandy w tej grze nie jedza (DoesPartyConsumeFood = false dla IsBandit), a gra dawala kazdej nowej bandzie
    /// zywnosc z niczego (GiveFoodToBanditParty) - zamkniete, wiec zywnosc w jukach to juz tylko lup i paser ja skupuje.
    /// KRYJOWKI: vanilla przy kazdym wejsciu bandy do kryjowki dopisywala bandzie i kryjowce po 25% wartosci jukow
    /// jako zloto z niczego (towar zostawal w jukach) - zamkniete. Kasa kryjowki rosnie tylko z tego, co banda odlozy
    /// z WLASNEJ kiesy, i nie tylko rosnie: to, co lezy ponad skarbiec, jest w obiegu - nowa banda dostaje z tego kiese
    /// startowa (zamiast z niczego), bandy kryjowki siegaja po to, gdy brakuje im na sprzet, a reszta idzie co dobe na
    /// zycie w miescie. Skarbiec (HideoutGoldBase + HideoutGoldPerBand x bandy kryjowki) zostaje - HideoutPurge wyplaca
    /// kase temu, kto kryjowke przetrzebi i przeszuka. Bandy tez zyja: co dobe wydaja czesc kiesy w najblizszym otwartym miescie.
    /// </summary>
    internal static class OutlawLaw
    {
        internal static bool On { get { var s = Settings.Current; return s != null && s.OutlawLawEnabled; } }

        private const string Commoner = "~";

        // region -> (troopId|"~") -> ludzie
        private static readonly Dictionary<string, Dictionary<string, float>> _pool = new Dictionary<string, Dictionary<string, float>>();
        private static bool _seeded;

        private static List<Settlement> _nodes;
        private static readonly Dictionary<string, List<Settlement>> _near = new Dictionary<string, List<Settlement>>();
        private static readonly Dictionary<string, Settlement> _regionOf = new Dictionary<string, Settlement>();
        private static readonly Dictionary<Clan, CharacterObject> _commonerOf = new Dictionary<Clan, CharacterObject>();
        private static readonly HashSet<MobileParty> _empty = new HashSet<MobileParty>();

        // licznik dnia
        private static float _inDesert, _inRouted, _inRaid, _inMisery, _inDisband, _outReturn;
        private static int _bornBands, _bornMen, _refused, _emptyRemoved, _bandRecruit, _prisonerJoin, _upLoot, _upFence, _upBlocked, _fenceGold;

        // paser - skup lupu band (licznik dnia)
        private static int _sellBands, _sellFenced, _sellBeyond, _sellSellers, _sellSpareBands, _sellUnits, _sellIron, _sellWood, _sellFood, _sellAnimals, _sellStumbles;
        private static int _leftBands, _leftUnits, _leftPoor, _leftNoTown, _leftBattle, _leftNoPurse, _leftQuest, _leftUndead;
        private static long _sellPaid, _sellWorth;
        // sprzedaz wedlug odleglosci bandy od miasta, ktore kupilo: do 50, 50-100, dalej (Jeff 06.10: "promien 50 ... to moze i 100, sprawdz")
        private const float ZoneNear = 50f, ZoneMid = 100f;
        private static readonly int[] _zoneUnits = new int[3];
        private static readonly long[] _zonePaid = new long[3];
        private static readonly HashSet<Settlement> _sellTowns = new HashSet<Settlement>();
        // kurek kryjowek (licznik dnia)
        private static int _hideEntries, _hideStumbles;
        private static long _hideBand, _hideHoard, _hideStash;
        // kryjowka -> suma NASZYCH zmian jej kasy (wplaty band +, wyplaty -) od poczatku sesji. Wejscie bywa zagniezdzone
        // (gra tworzy herszta w srodku wejscia bandy): po roznicy tej sumy wejscie zewnetrzne odroznia nasze przelewy od dosypki gry.
        private static readonly Dictionary<Settlement, long> _hoardSeen = new Dictionary<Settlement, long>();
        // obieg kas kryjowek i zycie band (licznik dnia)
        private static long _hoardStart, _hoardStartWant, _startNothing, _hoardGear, _lifeBands, _lifeHoards, _goneFought, _goneDisbanded;
        private static int _lifeStumbles, _upStumbles;
        // kryjowka -> skarbiec, ktory w niej zostaje (HideoutGoldBase + HideoutGoldPerBand x bandy tej kryjowki); liczony raz na dobe
        private static readonly Dictionary<Settlement, int> _hoardKeep = new Dictionary<Settlement, int>();
        // zywnosc z niczego (licznik dnia): ilu nowym bandom gra chciala ja dac; ile sztuk dosypanych bandom w bitwie zdjelismy
        private static int _foodBands, _foodTaken;
        private static bool _errSell, _errHide, _errFenceLog, _errLife, _errStart, _errFood, _errUpgrade;

        internal static void Reset()
        {
            _pool.Clear(); _seeded = false; _nodes = null; _near.Clear(); _regionOf.Clear(); _commonerOf.Clear(); _empty.Clear(); _refusedHour.Clear();
            FenceNewDay(); _hoardSeen.Clear(); _hoardKeep.Clear(); _errSell = false; _errHide = false; _errFenceLog = false; _errLife = false; _errStart = false; _errFood = false; _errUpgrade = false;
            _gold.Clear(); _goldTick = false; _foodSnap.Clear();      // migawki dziennego ticku gry nie przechodza do nastepnej kampanii
            _upLoot = _upFence = _upBlocked = _fenceGold = 0;         // liczniki zakupow u pasera ida do linii "Paser:" - nie moga przejsc z poprzedniej kampanii
        }

        private static void FenceNewDay()
        {
            _sellBands = _sellFenced = _sellBeyond = _sellSellers = _sellSpareBands = _sellUnits = _sellIron = _sellWood = _sellFood = _sellAnimals = _sellStumbles = 0;
            _leftBands = _leftUnits = _leftPoor = _leftNoTown = _leftBattle = _leftNoPurse = _leftQuest = _leftUndead = 0;
            _sellPaid = _sellWorth = 0; _sellTowns.Clear();
            Array.Clear(_zoneUnits, 0, _zoneUnits.Length); Array.Clear(_zonePaid, 0, _zonePaid.Length);
            _hideEntries = _hideStumbles = 0; _hideBand = _hideHoard = _hideStash = 0;
            _hoardStart = _hoardStartWant = _startNothing = _hoardGear = _lifeBands = _lifeHoards = _goneFought = _goneDisbanded = 0; _lifeStumbles = _upStumbles = 0;
            _foodBands = _foodTaken = 0;
        }

        // ------------------------------------------------------------ geografia
        private static List<Settlement> Nodes()
        {
            if (_nodes == null)
                _nodes = Settlement.All.Where(s => s != null && (s.IsTown || s.IsCastle)).ToList();
            return _nodes;
        }

        private static Settlement NearestNode(Vec2 p)
        {
            Settlement best = null; float bd = float.MaxValue;
            foreach (var s in Nodes())
            {
                float d = p.DistanceSquared(s.GetPosition2D);
                if (d < bd) { bd = d; best = s; }
            }
            return best;
        }

        private static Settlement RegionOf(Settlement s)
        {
            if (s == null) return null;
            if (s.IsTown || s.IsCastle) return s;
            if (s.IsVillage && s.Village != null && s.Village.Bound != null) return s.Village.Bound;
            Settlement r;
            if (_regionOf.TryGetValue(s.StringId, out r)) return r;
            r = NearestNode(s.GetPosition2D);
            _regionOf[s.StringId] = r;
            return r;
        }

        private static List<Settlement> Near(Settlement region)
        {
            List<Settlement> l;
            if (_near.TryGetValue(region.StringId, out l)) return l;
            int k = Math.Max(0, Settings.Current.OutlawNeighbourRegions);
            var p = region.GetPosition2D;
            l = new List<Settlement> { region };
            l.AddRange(Nodes().Where(s => s != region).OrderBy(s => p.DistanceSquared(s.GetPosition2D)).Take(k));
            _near[region.StringId] = l;
            return l;
        }

        // ------------------------------------------------------------ pula
        private static Dictionary<string, float> PoolOf(Settlement region)
        {
            Dictionary<string, float> d;
            if (!_pool.TryGetValue(region.StringId, out d)) { d = new Dictionary<string, float>(); _pool[region.StringId] = d; }
            return d;
        }

        private static float Count(Settlement region)
        {
            Dictionary<string, float> d;
            return _pool.TryGetValue(region.StringId, out d) ? d.Values.Sum() : 0f;
        }

        private static float Avail(Settlement region)
        {
            if (region == null) return 0f;
            float n = 0f;
            foreach (var r in Near(region)) n += Count(r);
            return n;
        }

        private static float Total() { return _pool.Values.Sum(d => d.Values.Sum()); }

        // odczyt dla ksiegi "Ludzie:" (PeopleLedger, tylko log) - ta sama geografia regionow i ta sama pula, niczego nie zmienia
        internal static List<Settlement> RegionNodes() { return Nodes(); }
        internal static Settlement RegionFor(Settlement s) { return RegionOf(s); }
        internal static Settlement RegionAt(Vec2 p) { return NearestNode(p); }
        internal static float PoolIn(Settlement region) { return region != null ? Count(region) : 0f; }

        private static void Add(Settlement region, string key, float n)
        {
            if (region == null || n <= 0f || string.IsNullOrEmpty(key)) return;
            var d = PoolOf(region);
            float v; d.TryGetValue(key, out v); d[key] = v + n;
        }

        private static void AddRoster(Settlement region, TroopRoster roster, float share, ref float counter)
        {
            if (region == null || roster == null || share <= 0f) return;
            for (int i = 0; i < roster.Count; i++)
            {
                var e = roster.GetElementCopyAtIndex(i);
                if (e.Character == null || e.Character.IsHero || e.Number <= 0) continue;
                float n = e.Number * share;
                Add(region, e.Character.StringId, n);
                counter += n;
            }
        }

        /// <summary>Ludzie z wsi regionu: zabrani z hearth (nie z kosmosu).</summary>
        private static float TakeCommoners(Settlement region, float men)
        {
            if (region == null || men <= 0f) return 0f;
            float per = Math.Max(0.01f, Settings.Current.OutlawHearthPerMan);
            var vs = region.BoundVillages;
            if (vs == null || vs.Count == 0) return 0f;
            float taken = 0f;
            foreach (var v in vs.OrderByDescending(x => x.Hearth))
            {
                if (taken >= men) break;
                float can = Math.Max(0f, (v.Hearth - 50f) / per);      // wies nie znika do zera
                float t = Math.Min(can, men - taken);
                if (t <= 0f) continue;
                v.Hearth -= t * per;
                taken += t;
            }
            Add(region, Commoner, taken);
            return taken;
        }

        private static void ReturnHome(Settlement region, float men)
        {
            if (region == null || men <= 0f) return;
            var vs = region.BoundVillages;
            if (vs == null || vs.Count == 0) return;
            float per = Settings.Current.OutlawHearthPerMan;
            var v = vs.OrderBy(x => x.Hearth).First();
            v.Hearth += men * per;
        }

        private static void Seed()
        {
            if (_seeded) return;
            _seeded = true;
            try
            {
                float per = Settings.Current.OutlawSeedPerHearth;
                float sum = 0f;
                foreach (var r in Nodes())
                {
                    float h = 0f;
                    if (r.BoundVillages != null) foreach (var v in r.BoundVillages) h += v.Hearth;
                    sum += TakeCommoners(r, h * per);
                }
                Log.Info("Wyrzutki: pula poczatkowa " + (int)sum + " ludzi w " + Nodes().Count + " regionach (" + per.ToString("0.000", CultureInfo.InvariantCulture) + " na hearth, zabrani z wsi).");
            }
            catch (Exception e) { Log.Error("OutlawLaw.Seed", e); }
        }

        private static CharacterObject CommonerFor(Clan clan)
        {
            CharacterObject c;
            if (clan != null && _commonerOf.TryGetValue(clan, out c)) return c;
            CharacterObject looter = null;
            try { looter = MBObjectManager.Instance.GetObject<CharacterObject>("looter"); } catch { }
            c = null;
            try
            {
                var bb = clan != null && clan.Culture != null ? clan.Culture.BanditBandit : null;
                if (bb != null)
                {
                    var body = bb.FirstBattleEquipment[EquipmentIndex.Body].Item;
                    float w = body != null ? body.Weight : 0f;
                    if (w <= Settings.Current.OutlawCommonerMaxArmorKg) c = bb;      // lekki bandyta klanu - ok
                }
            }
            catch { }
            if (c == null) c = looter;
            if (clan != null) _commonerOf[clan] = c;
            return c;
        }

        /// <summary>Zabiera do `want` ludzi z regionu i sasiadow: najpierw zolnierze (dezerterzy), potem prosci.</summary>
        private static TroopRoster Draw(Settlement home, int want, Clan clan)
        {
            var roster = TroopRoster.CreateDummyTroopRoster();
            if (home == null || want <= 0) return roster;
            int got = 0;
            var commoner = CommonerFor(clan);
            foreach (var r in Near(home))
            {
                if (got >= want) break;
                Dictionary<string, float> d;
                if (!_pool.TryGetValue(r.StringId, out d)) continue;
                foreach (var key in d.Keys.Where(k => k != Commoner).OrderByDescending(k => d[k]).ToList())
                {
                    if (got >= want) break;
                    int n = Math.Min(want - got, (int)Math.Floor(d[key]));
                    if (n <= 0) continue;
                    var ch = MBObjectManager.Instance.GetObject<CharacterObject>(key);
                    if (ch == null) { d.Remove(key); continue; }
                    roster.AddToCounts(ch, n);
                    d[key] -= n; got += n;
                }
                float cm; d.TryGetValue(Commoner, out cm);
                int nc = Math.Min(want - got, (int)Math.Floor(cm));
                if (nc > 0 && commoner != null)
                {
                    roster.AddToCounts(commoner, nc);
                    d[Commoner] = cm - nc; got += nc;
                }
                foreach (var key in d.Keys.Where(k => d[k] < 0.01f).ToList()) d.Remove(key);
            }
            return roster;
        }

        // ------------------------------------------------------------ zdarzenia
        internal static void OnTroopsDeserted(MobileParty party, TroopRoster roster)
        {
            try
            {
                if (!On || party == null || roster == null) return;
                var region = NearestNode(party.Position.ToVec2());
                AddRoster(region, roster, 1f, ref _inDesert);
            }
            catch (Exception e) { Log.Error("OutlawLaw.Deserted", e); }
        }

        internal static void OnMapEventEnded(MapEvent me)
        {
            try
            {
                if (!On || me == null) return;
                float share = Settings.Current.OutlawRoutedShare;
                if (share <= 0f) return;
                var region = NearestNode(me.Position.ToVec2());
                foreach (var side in new[] { me.AttackerSide, me.DefenderSide })
                {
                    if (side == null) continue;
                    foreach (var mep in side.Parties)
                    {
                        if (mep == null) continue;
                        AddRoster(region, mep.RoutedInBattle, share, ref _inRouted);
                    }
                }
            }
            catch (Exception e) { Log.Error("OutlawLaw.MapEventEnded", e); }
        }

        internal static void OnVillageLooted(Village v)
        {
            try
            {
                if (!On || v == null || v.Settlement == null) return;
                var region = RegionOf(v.Settlement);
                float men = v.Hearth * Settings.Current.OutlawRaidFleePercent / 100f;
                float per = Math.Max(0.01f, Settings.Current.OutlawHearthPerMan);
                men = Math.Min(men, Math.Max(0f, (v.Hearth - 50f) / per));
                if (men <= 0f) return;
                v.Hearth -= men * per;
                Add(region, Commoner, men);
                _inRaid += men;
            }
            catch (Exception e) { Log.Error("OutlawLaw.VillageLooted", e); }
        }

        internal static void OnPartyDestroyed(MobileParty party, PartyBase destroyer)
        {
            try
            {
                _empty.Remove(party);
                if (!On || party == null) return;
                if (!IsOutlawParty(party)) return;
                // pomiar zastanej dziury (tylko licznik do linii "Paser:"): kiesa bandy znika razem z partia. Rozbitej w bitwie gra
                // zdjela juz polowe dla zwyciezcy (CalculatePlunderedGoldAmountFromDefeatedParty -> MapEventParty.CommitGoldChanges
                // biegnie PRZED zniszczeniem partii), wiec tu widzimy sama reszte, ktora przepada; rozwiazanej nikt nie dostaje nic
                if (party.IsPartyTradeActive && party.PartyTradeGold > 0)
                {
                    if (destroyer != null) _goneFought += party.PartyTradeGold; else _goneDisbanded += party.PartyTradeGold;
                }
                if (destroyer != null) return;
                var region = NearestNode(party.Position.ToVec2());
                AddRoster(region, party.MemberRoster, 1f, ref _inDisband);
                AddRoster(region, party.PrisonRoster, 1f, ref _inDisband);      // jency rozwiazanej bandy tez zostaja w lesie
            }
            catch (Exception e) { Log.Error("OutlawLaw.PartyDestroyed", e); }
        }

        internal static void Hourly()
        {
            if (_empty.Count == 0) return;
            foreach (var p in _empty.ToList())
            {
                try
                {
                    if (p == null || !p.IsActive) { _empty.Remove(p); continue; }
                    if (p.MapEvent != null || p.SiegeEvent != null) continue;
                    if (p.MemberRoster.TotalManCount > 0) { _empty.Remove(p); continue; }
                    _empty.Remove(p);
                    DestroyPartyAction.Apply(null, p);
                    _emptyRemoved++;
                }
                catch (Exception e) { _empty.Remove(p); Log.Error("OutlawLaw.Hourly", e); }
            }
        }

        private static bool IsOutlawParty(MobileParty p)
        {
            if (p == null) return false;
            if (p.IsBandit) return true;
            try
            {
                var c = p.PartyComponent;
                return c != null && c.GetType().Name.IndexOf("Bandit", StringComparison.OrdinalIgnoreCase) >= 0;
            }
            catch { return false; }
        }

        // ------------------------------------------------------------ dzien
        private static readonly Dictionary<IFaction, bool> _war = new Dictionary<IFaction, bool>();
        private static bool AtWar(IFaction f)
        {
            if (f == null) return false;
            bool w;
            if (_war.TryGetValue(f, out w)) return w;
            w = false;
            try { foreach (var k in Kingdom.All) if (k != f && !k.IsEliminated && f.IsAtWarWith(k)) { w = true; break; } } catch { }
            _war[f] = w;
            return w;
        }

        /// <summary>Nedza regionu (miasto/zamek z wsiami): bieda, bezprawie, wojna, spalone wsie, glod.
        /// Ta sama miara dla wyrzutkow i dla ochotnikow (Levy) - jedna prawda o regionie.</summary>
        internal static float MiseryOf(Settlement r)
        {
            try
            {
                var s = Settings.Current;
                if (r == null || s == null) return 0f;
                if (r.IsVillage && r.Village != null && r.Village.Bound != null) r = r.Village.Bound;
                float looted = 0f; int nv = 0;
                if (r.BoundVillages != null)
                    foreach (var v in r.BoundVillages)
                    {
                        nv++;
                        if (v.VillageState == Village.VillageStates.Looted || v.VillageState == Village.VillageStates.BeingRaided) looted++;
                    }
                float prosp = 0.5f, sec = 0.5f;
                if (r.Town != null)
                {
                    prosp = MBMath.ClampFloat(r.Town.Prosperity / Math.Max(1f, s.OutlawProsperityGood), 0f, 1f);
                    sec = MBMath.ClampFloat(r.Town.Security / 100f, 0f, 1f);
                }
                return (1f - prosp) + (1f - sec) * 0.5f
                       + (AtWar(r.MapFaction) ? s.OutlawWarMisery : 0f)
                       + (nv > 0 ? looted / nv * s.OutlawLootedMisery : 0f)
                       + (r.IsStarving ? s.OutlawStarvingMisery : 0f);
            }
            catch { return 0f; }
        }

        internal static void Daily()
        {
            if (!On) return;
            try
            {
                var s = Settings.Current;
                Seed();
                _war.Clear();
                // 1. bieda i chaos -> ludzie w las; pokoj i dobrobyt -> do domu
                foreach (var r in Nodes())
                {
                    try
                    {
                        float hearth = 0f, looted = 0f; int nv = 0;
                        if (r.BoundVillages != null)
                            foreach (var v in r.BoundVillages)
                            {
                                hearth += v.Hearth; nv++;
                                if (v.VillageState == Village.VillageStates.Looted || v.VillageState == Village.VillageStates.BeingRaided) looted++;
                            }
                        float prosp = 0.5f, sec = 0.5f;
                        if (r.Town != null)
                        {
                            prosp = MBMath.ClampFloat(r.Town.Prosperity / Math.Max(1f, s.OutlawProsperityGood), 0f, 1f);
                            sec = MBMath.ClampFloat(r.Town.Security / 100f, 0f, 1f);
                        }
                        bool war = AtWar(r.MapFaction);
                        float misery = (1f - prosp) + (1f - sec) * 0.5f
                                       + (war ? s.OutlawWarMisery : 0f)
                                       + (nv > 0 ? looted / nv * s.OutlawLootedMisery : 0f)
                                       + (r.IsStarving ? s.OutlawStarvingMisery : 0f);
                        float inflow = hearth / 1000f * s.OutlawDailyPerThousandHearth * misery;
                        if (inflow > 0f) _inMisery += TakeCommoners(r, inflow);

                        Dictionary<string, float> d;
                        if (_pool.TryGetValue(r.StringId, out d) && d.Count > 0)
                        {
                            float rate = (s.OutlawReturnBasePercent + (war ? 0f : s.OutlawReturnPeacePercent * prosp)) / 100f;
                            if (r.IsStarving) rate *= 0.25f;
                            float back = 0f;
                            foreach (var key in d.Keys.ToList())
                            {
                                float m = d[key] * rate;
                                d[key] -= m; back += m;
                                if (d[key] < 0.01f) d.Remove(key);
                            }
                            ReturnHome(r, back);
                            _outReturn += back;
                        }
                    }
                    catch { }
                }

                // 2. bandy werbuja: jency i ludzie z puli regionu
                float join = s.OutlawPrisonerJoinPercent / 100f;
                int daily = Math.Max(0, s.OutlawDailyRecruit);
                foreach (var p in MobileParty.AllBanditParties.ToList())
                {
                    try
                    {
                        if (p == null || !p.IsActive || p.MapEvent != null) continue;
                        // jency przechodza do bandy w swoim sprzecie (gorsi chetniej)
                        if (join > 0f && p.PrisonRoster != null && p.PrisonRoster.TotalRegulars > 0)
                        {
                            for (int i = p.PrisonRoster.Count - 1; i >= 0; i--)
                            {
                                var e = p.PrisonRoster.GetElementCopyAtIndex(i);
                                if (e.Character == null || e.Character.IsHero) continue;
                                int healthy = e.Number - e.WoundedNumber;
                                if (healthy <= 0) continue;
                                float chance = join * Math.Max(0.1f, 1f - e.Character.Tier / 7f);
                                int n = MBRandom.RoundRandomized(healthy * chance);
                                if (n <= 0) continue;
                                p.PrisonRoster.AddToCounts(e.Character, -n);
                                p.MemberRoster.AddToCounts(e.Character, n);
                                _prisonerJoin += n;
                            }
                        }
                        if (daily > 0)
                        {
                            int room = p.Party.PartySizeLimit - p.MemberRoster.TotalManCount;
                            int want = Math.Min(room, daily);
                            if (want > 0)
                            {
                                var region = NearestNode(p.Position.ToVec2());
                                if (Count(region) >= 1f)
                                {
                                    var got = DrawOnly(region, want, p.ActualClan);
                                    if (got.TotalManCount > 0) { p.MemberRoster.Add(got); _bandRecruit += got.TotalManCount; }
                                }
                            }
                        }
                    }
                    catch { }
                }

                // 3. zycie band i kryjowek w miastach (z kiesy, ktora banda miala przez cala dobe - na sprzet byl czas), potem
                // 4. paser: kazda banda sprzedaje zrabowany ladunek (wyjatek jednej bandy nie zatrzymuje pozostalych).
                // Bez kiesy banda nie ma dokad wziac zaplaty; partii zajetej przez zadanie nie ruszamy; trup nie handluje.
                MoneyLedger.Mark(MoneyLedger.MRest);          // ksiega przeplywow osad (tylko log): to, co zmienilo kasy dotad, to nie bandy
                LifeDay(s);
                MoneyLedger.Mark(MoneyLedger.MLife);          // ... zmiana kas od poprzedniej migawki to wydatki band i kryjowek na zycie
                float fenceShare = MBMath.ClampFloat(s.OutlawFenceLootShare, 0f, 1f);
                if (s.OutlawFenceBuysLoot && fenceShare > 0f)
                {
                    int reserve = (int)Math.Max(0f, s.TownRentFloorGold);
                    bool food = s.OutlawNoFreeFood;
                    foreach (var p in MobileParty.AllBanditParties.ToList())
                    {
                        try
                        {
                            if (p == null || !p.IsActive) continue;
                            int why = p.MapEvent != null ? 1 : (!p.IsPartyTradeActive ? 2 : (p.IsCurrentlyUsedByAQuest ? 3 : (Undead.Party(p) ? 4 : 0)));
                            if (why != 0)
                            {
                                // banda poza handlem: liczymy, ile ladunku przez to lezy (pomiar do linii "Paser:")
                                int left = CargoUnits(p.ItemRoster, food);
                                if (left > 0)
                                {
                                    _leftUnits += left;
                                    if (why == 1) _leftBattle++; else if (why == 2) _leftNoPurse++; else if (why == 3) _leftQuest++; else _leftUndead++;
                                }
                                continue;
                            }
                            FenceSell(p, fenceShare, reserve);
                        }
                        catch (Exception e)
                        {
                            _sellStumbles++;
                            if (!_errSell) { _errSell = true; Log.Error("OutlawLaw.FenceSell", e); }
                        }
                    }
                }
                MoneyLedger.Mark(MoneyLedger.MFence);         // ... a zmiana kas od poprzedniej migawki to zaplata miast za lup

                int bands = 0, men = 0;
                foreach (var p in MobileParty.AllBanditParties) { bands++; men += p.MemberRoster.TotalManCount; }
                float tot = Total(); float sold = _pool.Values.Sum(d => d.Where(kv => kv.Key != Commoner).Sum(kv => kv.Value));
                Log.Info("Wyrzutki: dzien " + (int)CampaignTime.Now.ToDays + " | pula " + (int)tot + " ludzi (zolnierzy " + (int)sold + ", prostych " + (int)(tot - sold) + ")"
                         + " | naplyw: dezercja " + (int)_inDesert + ", rozbitkowie " + (int)_inRouted + ", rabunki " + (int)_inRaid
                         + ", bieda/wojna " + _inMisery.ToString("0.0", CultureInfo.InvariantCulture) + ", rozwiazane bandy " + (int)_inDisband
                         + " | powrot do wsi " + _outReturn.ToString("0.0", CultureInfo.InvariantCulture)
                         + " | bandy nowe " + _bornBands + " (" + _bornMen + " ludzi), odmowione " + _refused + ", puste usuniete " + _emptyRemoved
                         + " | werbunek: z puli " + _bandRecruit + ", jency " + _prisonerJoin
                         + " | awanse: z lupu " + _upLoot + ", od pasera " + _upFence + " (" + _fenceGold + " zl), bez sprzetu " + _upBlocked
                         + " | band " + bands + ", ludzi " + men + ".");
                FenceLog(s, _upFence, _fenceGold);
                _inDesert = _inRouted = _inRaid = _inMisery = _inDisband = _outReturn = 0f;
                _bornBands = _bornMen = _refused = _emptyRemoved = _bandRecruit = _prisonerJoin = _upLoot = _upFence = _upBlocked = _fenceGold = 0;
            }
            catch (Exception e) { Log.Error("OutlawLaw.Daily", e); }
        }

        /// <summary>Werbunek z jednego regionu (bez sasiadow).</summary>
        private static TroopRoster DrawOnly(Settlement region, int want, Clan clan)
        {
            var roster = TroopRoster.CreateDummyTroopRoster();
            Dictionary<string, float> d;
            if (!_pool.TryGetValue(region.StringId, out d)) return roster;
            int got = 0;
            foreach (var key in d.Keys.ToList())
            {
                if (got >= want) break;
                int n = Math.Min(want - got, (int)Math.Floor(d[key]));
                if (n <= 0) continue;
                var ch = key == Commoner ? CommonerFor(clan) : MBObjectManager.Instance.GetObject<CharacterObject>(key);
                if (ch == null) { d.Remove(key); continue; }
                roster.AddToCounts(ch, n);
                d[key] -= n; got += n;
                if (d[key] < 0.01f) d.Remove(key);
            }
            return roster;
        }

        // ------------------------------------------------------------ narodziny band
        [ThreadStatic] private static int _depth;
        public static void RosterPrefix() { _depth++; }
        public static Exception RosterFinalizer(Exception __exception) { if (_depth > 0) _depth--; return __exception; }
        public static void RosterPostfix(MobileParty __0, ref TroopRoster __result)
        {
            if (_depth > 1) return;                       // ROT -> BEE -> Naval -> vanilla: podmieniamy raz, na zewnatrz
                var party = __0;
            try
            {
                if (!On || party == null || __result == null || !IsOutlawParty(party)) return;
                Seed();
                Settlement home = null;
                try { home = party.PartyComponent != null ? party.PartyComponent.HomeSettlement : null; } catch { }
                Settlement region = RegionOf(home);
                if (region == null) region = Nodes().OrderByDescending(Avail).FirstOrDefault();
                int want = Math.Max(1, (int)Math.Round(__result.TotalManCount * Settings.Current.OutlawBandSizeScale));
                Clan clan = null;
                try { clan = party.ActualClan; } catch { }
                var got = Draw(region, want, clan);
                __result = got;
                if (got.TotalManCount == 0) _empty.Add(party);
                else { _bornBands++; _bornMen += got.TotalManCount; }
            }
            catch (Exception e) { Log.Error("OutlawLaw.Roster", e); }
        }

        // bramki: nie ma ludzi w okolicy = nie ma bandy
        private static int MinBand { get { return Math.Max(1, Settings.Current.OutlawMinBand); } }

        private static readonly Dictionary<Clan, double> _refusedHour = new Dictionary<Clan, double>();

        public static bool GateClan(Clan selectedFaction)
        {
            try
            {
                if (!On) return true;
                double hour = Math.Floor(CampaignTime.Now.ToHours);
                double rh;
                if (selectedFaction != null && _refusedHour.TryGetValue(selectedFaction, out rh) && rh == hour) { _refused++; return false; }   // ta godzina juz odmowiona - bez ponownego szukania
                Seed();
                foreach (var h in Hideout.All)
                {
                    if (h == null || !h.IsInfested || h.Settlement == null) continue;
                    bool mine = false;
                    foreach (var p in h.Settlement.Parties) if (p.IsBandit && p.ActualClan == selectedFaction) { mine = true; break; }
                    if (mine && Avail(RegionOf(h.Settlement)) >= MinBand) return true;
                }
                if (selectedFaction != null) _refusedHour[selectedFaction] = hour;
                _refused++;
                return false;
            }
            catch { return true; }
        }

        public static bool GateGlobal()
        {
            try
            {
                if (!On) return true;
                Seed();
                if (Total() >= MinBand * 3) return true;
                _refused++;
                return false;
            }
            catch { return true; }
        }

        public static bool GateHideout(Hideout hideout)
        {
            try
            {
                if (!On) return true;
                Seed();
                if (hideout != null && hideout.Settlement != null && Avail(RegionOf(hideout.Settlement)) >= MinBand * 2) return true;
                _refused++;
                return false;
            }
            catch { return true; }
        }

        public static bool GateLooter(Clan selectedFaction, bool uniformDistribution) { return GateGlobal(); }
        public static bool GatePirate(Clan clan) { return GateGlobal(); }
        public static bool GateFill(Clan faction) { return GateGlobal(); }
        public static bool GateBkInfest(Hideout hideout) { return GateHideout(hideout); }
        public static bool GateBkHero(Clan clan) { return GateGlobal(); }
        public static bool SkipVanillaDeserters() { return !On; }
        public static bool SkipBkUpgrade() { return !On; }      // BK dosypywal 2-6 ludzi dziennie - teraz werbunek z puli

        // ------------------------------------------------------------ zloto band
        private static readonly Dictionary<MobileParty, int> _gold = new Dictionary<MobileParty, int>();
        private static bool _goldTick;      // trwa dzienny tick gry (miedzy GoldPrefix a GoldPostfix) i migawka kies jest czynna
        public static void GoldPrefix()
        {
            _gold.Clear(); _goldTick = false; _foodSnap.Clear();
            if (!On) return;
            FoodSnapshot();
            if (!Settings.Current.OutlawNoFreeGold) return;
            foreach (var p in MobileParty.AllBanditParties) if (p.IsPartyTradeActive) _gold[p] = p.PartyTradeGold;
            _goldTick = true;
        }
        public static void GoldPostfix()
        {
            _goldTick = false;
            foreach (var kv in _gold) { try { kv.Key.PartyTradeGold = kv.Value; } catch { } }
            _gold.Clear();
            FoodRestore();
        }

        /// <summary>
        /// Kiesa startowa nowej bandy (postfiks na CreatePartyTrade gry i NavalDLC; pierwszy parametr po pozycji).
        /// Gra losowala 5-15 zl na czlowieka z niczego. Przy obiegu kas kryjowek (OutlawHoardCirculates) banda dostaje
        /// OutlawCoinsPerMan na czlowieka Z KASY swojej kryjowki - z tego, co lezy w niej ponad skarbiec, i tylko tyle, ile
        /// tam jest (przelew, nie dosypka); banda bez kryjowki (lupiezcy, piraci) i banda z kryjowki bez nadwyzki zaczyna
        /// bez monety. Przy wylaczonym obiegu - jak dotad, z niczego (z licznikiem).
        /// </summary>
        public static void StartGoldPostfix(MobileParty __0)
        {
            var banditParty = __0;
            try
            {
                var s = Settings.Current;
                if (!On || !s.OutlawNoFreeGold || banditParty == null) return;
                int want = banditParty.MemberRoster.TotalManCount * Math.Max(0, s.OutlawCoinsPerMan);
                int got = want;
                if (s.OutlawHoardCirculates)
                {
                    got = 0;
                    var home = HomeHideout(banditParty);
                    // trup monety nie bierze (nie kupuje i nie wydaje)
                    if (home != null && want > 0 && !Undead.Party(banditParty)) got = Math.Min(want, HoardSurplus(home));
                    if (got > 0) HoardChange(home, -got);
                    _hoardStart += got; _hoardStartWant += want;
                }
                else _startNothing += got;
                banditParty.PartyTradeGold = got;
                // Banda urodzona W SRODKU dziennego ticku gry (nowa kryjowka: gra wola AddNewHideouts PRZED petla dziennej dosypki)
                // nie byla w migawce GoldPrefix - gra dopisalaby jej 2.5 zl na czlowieka z niczego, a GoldPostfix by tego nie cofnal.
                // Wchodzi do migawki z kiesa startowa; to, co zaraz odlozy w kryjowce, odejmuje od migawki HideoutGoldPostfix.
                if (_goldTick && banditParty.IsPartyTradeActive) _gold[banditParty] = banditParty.PartyTradeGold;
            }
            catch (Exception e) { if (!_errStart) { _errStart = true; Log.Error("OutlawLaw.StartGold", e); } }
        }

        // ------------------------------------------------------------ zywnosc z niczego
        // Bandy nie jedza: DefaultMobilePartyFoodConsumptionModel.DoesPartyConsumeFood zwraca false dla IsBandit, a modele BK
        // (BKPartyConsumptionModel), BEE, ROT i NavalDLC tylko je opakowuja; glodu, kary morale za glod ani dezercji band w kodzie
        // gry nie ma (IsStarving ustawia tylko PartyConsumeFood, ktorego bandy nie wolaja). Mimo to gra daje zywnosc z niczego
        // kazdej nowej bandzie (GiveFoodToBanditParty; NavalDLC ma wlasna kopie dla piratow) i z szansa 3% na dobe bandzie
        // w bitwie (w srodku BanditSpawnCampaignBehavior.DailyTick). Odkad paser skupuje zywnosc, ta dosypka zamienialaby sie
        // w zloto miast - zamykamy ja jednym wlacznikiem (OutlawNoFreeFood), tym samym, ktory pozwala paserowi brac zywnosc.

        /// <summary>Prefiks na GiveFoodToBanditParty (gra i NavalDLC): przy zamknietym kurku oryginal nie biegnie.</summary>
        public static bool FoodPrefix()
        {
            try
            {
                var s = Settings.Current;
                if (!On || s == null || !s.OutlawNoFreeFood) return true;
                _foodBands++;
                return false;
            }
            catch { return true; }
        }

        // banda w bitwie w chwili dziennego ticku gry -> zywnosc w jukach przed tickiem (przedmiot -> sztuki)
        private static readonly Dictionary<MobileParty, Dictionary<ItemObject, int>> _foodSnap = new Dictionary<MobileParty, Dictionary<ItemObject, int>>();

        private static Dictionary<ItemObject, int> FoodOf(ItemRoster r)
        {
            var d = new Dictionary<ItemObject, int>();
            if (r == null) return d;
            for (int i = 0; i < r.Count; i++)
            {
                var el = r.GetElementCopyAtIndex(i);
                var it = el.EquipmentElement.Item;
                if (it == null || el.Amount <= 0 || !it.IsFood) continue;
                int n; d.TryGetValue(it, out n); d[it] = n + el.Amount;
            }
            return d;
        }

        /// <summary>Przed dziennym tickiem gry: stan zywnosci band, ktorym gra moze cos dosypac (tylko bandy w bitwie, z kiesa).</summary>
        private static void FoodSnapshot()
        {
            try
            {
                if (!Settings.Current.OutlawNoFreeFood) return;
                foreach (var p in MobileParty.AllBanditParties)
                    if (p != null && p.MapEvent != null && p.IsPartyTradeActive) _foodSnap[p] = FoodOf(p.ItemRoster);
            }
            catch (Exception e) { _foodSnap.Clear(); if (!_errFood) { _errFood = true; Log.Error("OutlawLaw.FoodSnapshot", e); } }
        }

        /// <summary>Po dziennym ticku gry: zdejmujemy to, co gra dosypala (w srodku ticku banda niczego nie rabuje ani nie kupuje).</summary>
        private static void FoodRestore()
        {
            if (_foodSnap.Count == 0) return;
            foreach (var kv in _foodSnap)
            {
                try
                {
                    var r = kv.Key != null ? kv.Key.ItemRoster : null;
                    if (r == null) continue;
                    foreach (var now in FoodOf(r))
                    {
                        int had; kv.Value.TryGetValue(now.Key, out had);
                        int extra = now.Value - had;
                        if (extra <= 0) continue;
                        // gra dosypuje sztuki bez znacznika stanu - te same zdejmujemy, nigdy wiecej niz lezy w tym stosie
                        var plain = new EquipmentElement(now.Key);
                        int idx = r.FindIndexOfElement(plain);
                        int take = idx >= 0 ? Math.Min(extra, r.GetElementNumber(idx)) : 0;
                        if (take <= 0) continue;
                        r.AddToCounts(plain, -take);
                        _foodTaken += take;
                    }
                }
                catch (Exception e) { if (!_errFood) { _errFood = true; Log.Error("OutlawLaw.FoodRestore", e); } }
            }
            _foodSnap.Clear();
        }

        // ------------------------------------------------------------ awanse tylko ze sprzetem
        public static bool UpgradePrefix(PartyBase party)
        {
            try
            {
                if (!On || !Settings.Current.OutlawGearUpgrades || party == null || party.MobileParty == null || !party.MobileParty.IsBandit) return true;
                if (party == PartyBase.MainParty || !party.IsActive) return false;
                var mp = party.MobileParty;
                var model = Campaign.Current.Models.PartyTroopUpgradeModel;
                var roster = party.MemberRoster;
                var chars = new List<CharacterObject>();
                for (int i = 0; i < roster.Count; i++) { var c = roster.GetCharacterAtIndex(i); if (c != null && !c.IsHero) chars.Add(c); }
                foreach (var c in chars)
                {
                    var targets = c.UpgradeTargets;
                    if (targets == null || targets.Length == 0) continue;
                    int idx = roster.FindIndexOfTroop(c);
                    if (idx < 0) continue;
                    var target = targets[MBRandom.RandomInt(targets.Length)];
                    if (target == null) continue;
                    int cost = model.GetXpCostForUpgrade(party, c, target);
                    if (cost <= 0) continue;
                    int number = roster.GetElementNumber(idx);
                    int byXp = Math.Min(number, roster.GetElementXp(idx) / cost);
                    if (byXp <= 0) continue;
                    int n = 0;
                    for (int k = 0; k < byXp; k++) { if (Equip(mp, c, target)) n++; else break; }
                    if (n <= 0) { _upBlocked += byXp; continue; }
                    roster.SetElementXp(idx, roster.GetElementXp(idx) - cost * n);
                    roster.AddToCounts(c, -n);
                    roster.AddToCounts(target, n);
                }
                return false;
            }
            catch (Exception e)
            {
                // odkad kazda banda ma pasera, zakupy biegna u wszystkich band - blad raz do logu, potem tylko licznik (linia "Paser:")
                _upStumbles++;
                if (!_errUpgrade) { _errUpgrade = true; Log.Error("OutlawLaw.Upgrade", e); }
                return false;
            }
        }

        private static float BodyOf(CharacterObject c)
        {
            try { var it = c.FirstBattleEquipment[EquipmentIndex.Body].Item; return it != null && it.ArmorComponent != null ? it.ArmorComponent.BodyArmor : 0f; }
            catch { return 0f; }
        }

        /// <summary>
        /// Jedna sztuka sprzetu na awans: zbroja (gdy cel nosi ciezsza) i kon (gdy cel konny). Najpierw z jukow bandy; czego
        /// w jukach nie ma, banda kupuje u pasera - w tym miescie w zasiegu, w ktorym wychodzi najtaniej Z DOSTAWA (wartosc
        /// sztuki x marza rosnaca z odlegloscia). Gdy w kiesie brakuje, reszte doklada nadwyzka kasy kryjowki bandy.
        /// </summary>
        private static bool Equip(MobileParty p, CharacterObject from, CharacterObject to)
        {
            float need = BodyOf(to);
            bool armour = need > BodyOf(from) + 2f;
            bool horse = to.IsMounted && !from.IsMounted;
            if (!armour && !horse) return true;
            Func<ItemObject, bool> armourOk = it => it.ItemType == ItemObject.ItemTypeEnum.BodyArmor && it.ArmorComponent != null && it.ArmorComponent.BodyArmor >= need * 0.8f;
            Func<ItemObject, bool> horseOk = it => it.ItemType == ItemObject.ItemTypeEnum.Horse && it.HorseComponent != null && !it.HorseComponent.IsPackAnimal;
            ItemObject a = null, h = null;
            Settlement aTown = null, hTown = null;
            int aPrice = 0, hPrice = 0;
            List<Bid> reach = null;
            if (armour) { a = Find(p.ItemRoster, armourOk); if (a == null) a = FindAtFence(p, ref reach, armourOk, out aTown, out aPrice); if (a == null) return false; }
            if (horse) { h = Find(p.ItemRoster, horseOk); if (h == null) h = FindAtFence(p, ref reach, horseOk, out hTown, out hPrice); if (h == null) return false; }
            int price = aPrice + hPrice, lent = 0;
            Settlement home = null;
            if (price > 0 && p.PartyTradeGold < price)
            {
                lent = price - p.PartyTradeGold;
                home = HoardSpare(p, lent);
                if (home == null) return false;       // nie stac ani bandy, ani nadwyzki kasy jej kryjowki
            }
            // wpis 90 (audyt 1-43 F8): zdejmujemy sztuke w jej stanie - dotad zuzyta zostawala na polce, banda awansowala z niczego
            if (a != null) { if (!Helper.RemoveOne(aTown != null ? aTown.ItemRoster : p.ItemRoster, a)) return false; }
            if (h != null) { if (!Helper.RemoveOne(hTown != null ? hTown.ItemRoster : p.ItemRoster, h)) return false; }
            if (price > 0)
            {
                if (lent > 0) { HoardChange(home, -lent); p.PartyTradeGold += lent; _hoardGear += lent; }   // przelew kryjowka -> banda
                p.PartyTradeGold -= price;
                FencePaid(aTown, aPrice); FencePaid(hTown, hPrice);
                _fenceGold += price; _upFence++;
            }
            else _upLoot++;
            return true;
        }

        /// <summary>Zaplata bandy za sprzet trafia do kasy miasta pasera; Note = ksiega przeplywow osad (tylko licznik).</summary>
        private static void FencePaid(Settlement town, int price)
        {
            if (town == null || price <= 0) return;
            try { town.SettlementComponent.ChangeGold(price); MoneyLedger.Note(MoneyLedger.NFence, town, price); } catch { }
        }

        /// <summary>
        /// Najtansza z dostawa sztuka, ktora wystarczy, na polkach miast w zasiegu pasera: wartosc sztuki x marza, a marza rosnie
        /// rowno z odlegloscia od OutlawFenceMarkup przy miescie do OutlawFenceMarkupFar na granicy promienia (zbroja i kon sa
        /// lekkie wobec swojej wartosci, wiec droga podnosi cene slabiej, niz obniza cene skupu lupu). Trup nie handluje.
        /// </summary>
        private static ItemObject FindAtFence(MobileParty p, ref List<Bid> reach, Func<ItemObject, bool> ok, out Settlement town, out int price)
        {
            town = null; price = 0;
            if (Undead.Party(p)) return null;
            var s = Settings.Current;
            float radius = Math.Max(1f, s.OutlawFenceRadius);
            if (reach == null) { bool beyond; reach = Reach(p, radius, out beyond); }
            float near = Math.Max(0f, s.OutlawFenceMarkup), far = Math.Max(near, s.OutlawFenceMarkupFar);
            ItemObject best = null;
            foreach (var b in reach)
            {
                var it = Find(b.St.ItemRoster, ok);
                if (it == null) continue;
                int cost = (int)(it.Value * (double)Slide(near, far, b.Dist, radius));
                if (best == null || cost < price) { best = it; price = cost; town = b.St; }
            }
            return best;
        }

        /// <summary>
        /// Nadwyzka kasy kryjowki: to, co lezy w niej ponad skarbiec (liczony raz na dobe w LifeDay). Skarbca bandy nie
        /// ruszaja - to lup dla tego, kto kryjowke oczysci; kasy czekajacej na przeszukanie przez gracza nie rusza nikt.
        /// Dopoki skarbiec nie jest policzony (pierwsza doba po wczytaniu), nadwyzki nie ma.
        /// </summary>
        private static int HoardSurplus(Settlement hideout)
        {
            var comp = hideout != null ? hideout.SettlementComponent : null;
            int keep;
            if (comp == null || !_hoardKeep.TryGetValue(hideout, out keep) || HideoutPurge.Holds(hideout)) return 0;
            return Math.Max(0, comp.Gold - keep);
        }

        /// <summary>Czy nadwyzka kasy kryjowki bandy pokryje brakujaca kwote. Zwraca kryjowke albo null.</summary>
        private static Settlement HoardSpare(MobileParty p, int lack)
        {
            if (!Settings.Current.OutlawHoardCirculates || lack <= 0) return null;
            var home = HomeHideout(p);
            return home != null && HoardSurplus(home) >= lack ? home : null;
        }

        /// <summary>Kryjowka, z ktorej jest banda (lupiezcy i piraci jej nie maja).</summary>
        private static Settlement HomeHideout(MobileParty p)
        {
            var c = p != null ? p.BanditPartyComponent : null;
            var h = c != null ? c.Hideout : null;
            return h != null ? h.Settlement : null;
        }

        /// <summary>Kazda NASZA zmiana kasy kryjowki idzie tedy - suma _hoardSeen pozwala wejsciu bandy (HideoutGoldPostfix)
        /// odroznic nasze przelewy od dosypki gry, takze gdy kiesa startowa herszta wychodzi z kasy w srodku tego wejscia.</summary>
        private static void HoardChange(Settlement hideout, int delta)
        {
            hideout.SettlementComponent.ChangeGold(delta);
            long seen; _hoardSeen.TryGetValue(hideout, out seen);
            _hoardSeen[hideout] = seen + delta;
        }

        private static ItemObject Find(ItemRoster r, Func<ItemObject, bool> ok)
        {
            if (r == null) return null;
            ItemObject best = null;
            for (int i = 0; i < r.Count; i++)
            {
                var el = r.GetElementCopyAtIndex(i);
                var it = el.EquipmentElement.Item;
                if (it == null || el.Amount <= 0 || el.EquipmentElement.ItemModifier != null || !ok(it)) continue;
                if (best == null || it.Value < best.Value) best = it;     // najtansze, co wystarczy
            }
            return best;
        }

        // ------------------------------------------------------------ paser: zasieg i warunki
        /// <summary>Miasto w zasiegu pasera bandy i jego oferta na biezacy stos (pola robocze SellStack).</summary>
        private sealed class Bid
        {
            public Settlement St; public Town Town;
            public float Dist;          // odleglosc do warunkow handlu (banda poza promieniem: sam promien, czyli warunki z jego granicy)
            public float Real;          // prawdziwa odleglosc bandy od miasta (do podzialu na strefy w logu)
            public float Share;         // udzial bandy w cenie skupu tego miasta
            public bool Skip;           // na ten stos to miasto odpada (nie stac go, zerowy udzial, zwierze nie z tej krainy)
            public int Price; public double Net, Due; public long Worth; public int Moved;
        }

        /// <summary>Warunki pasera zmieniaja sie rowno z odlegloscia: `near` przy miescie, `far` na granicy promienia (i dalej).</summary>
        private static float Slide(float near, float far, float dist, float radius)
        {
            return near + (far - near) * MBMath.ClampFloat(dist / Math.Max(1f, radius), 0f, 1f);
        }

        /// <summary>
        /// Paserzy bandy: wszystkie nieoblezone miasta w promieniu (do oblezonego paser nie wjedzie). Gdy w promieniu nie ma
        /// zadnego, banda i tak ma pasera - najblizsze otwarte miasto swiata, na warunkach z granicy promienia (`beyond`).
        /// Pusta lista tylko wtedy, gdy na mapie nie ma ani jednego nieoblezonego miasta.
        /// </summary>
        private static List<Bid> Reach(MobileParty p, float radius, out bool beyond)
        {
            beyond = false;
            var list = new List<Bid>();
            var pos = p.Position.ToVec2();
            var cur = p.CurrentSettlement;
            float r2 = radius * radius, nd = float.MaxValue;
            Settlement nearest = null;
            foreach (var s in Nodes())
            {
                if (!s.IsTown || s.Town == null || s.ItemRoster == null || s.IsUnderSiege) continue;
                float d2 = ReferenceEquals(s, cur) ? 0f : pos.DistanceSquared(s.GetPosition2D);
                if (d2 <= r2) { float d = (float)Math.Sqrt(d2); list.Add(new Bid { St = s, Town = s.Town, Dist = d, Real = d }); }
                else if (d2 < nd) { nd = d2; nearest = s; }
            }
            if (list.Count == 0 && nearest != null)
            {
                beyond = true;
                list.Add(new Bid { St = nearest, Town = nearest.Town, Dist = radius, Real = (float)Math.Sqrt(nd) });
            }
            return list;
        }

        /// <summary>Najblizsze nieoblezone miasto - tam banda i kryjowka wydaja na zycie (bez promienia: do karczmy trafi kazdy).</summary>
        private static Settlement NearestOpenTown(Vec2 pos)
        {
            Settlement best = null; float bd = float.MaxValue;
            foreach (var s in Nodes())
            {
                if (!s.IsTown || s.Town == null || s.IsUnderSiege) continue;
                float d = pos.DistanceSquared(s.GetPosition2D);
                if (d < bd) { bd = d; best = s; }
            }
            return best;
        }

        // ------------------------------------------------------------ zycie band i obieg kas kryjowek
        /// <summary>
        /// Raz na dobe, przed skupem. (1) Skarbiec kazdej kryjowki = HideoutGoldBase + HideoutGoldPerBand x bandy, ktore maja
        /// w niej dom; kryjowka bez band skarbca nie trzyma. (2) Banda wydaje na zycie (jedzenie, picie, towarzystwo) czesc
        /// kiesy - OutlawLifeSpendShare - w najblizszym otwartym miescie: przelew kiesa -> kasa miasta, jak "zycie w miastach"
        /// sakiewek ludzi (MenPurse). (3) Kryjowka wydaje tak samo czesc tego, co lezy w jej kasie PONAD skarbiec (tylko przy
        /// OutlawHoardCirculates) - kasa kryjowki nie moze tylko rosnac. Lupu, ktory czeka na przeszukanie przez gracza, nie rusza.
        /// </summary>
        private static void LifeDay(Settings s)
        {
            try
            {
                _hoardKeep.Clear();
                var bandsOf = new Dictionary<Settlement, int>();
                foreach (var p in MobileParty.AllBanditParties)
                {
                    if (p == null || !p.IsActive) continue;
                    var home = HomeHideout(p);
                    if (home == null) continue;
                    int n; bandsOf.TryGetValue(home, out n); bandsOf[home] = n + 1;
                }
                int kb = Math.Max(0, s.HideoutGoldBase), kp = Math.Max(0, s.HideoutGoldPerBand);
                foreach (var h in Hideout.All)
                {
                    if (h == null || h.Settlement == null) continue;
                    int n; bandsOf.TryGetValue(h.Settlement, out n);
                    _hoardKeep[h.Settlement] = n > 0 ? kb + kp * n : 0;
                }
            }
            catch (Exception e) { _lifeStumbles++; if (!_errLife) { _errLife = true; Log.Error("OutlawLaw.LifeDay(skarbce)", e); } }

            double share = MBMath.ClampFloat(s.OutlawLifeSpendShare, 0f, 1f);
            if (share <= 0.0) return;
            foreach (var p in MobileParty.AllBanditParties.ToList())
            {
                try
                {
                    if (p == null || !p.IsActive || p.MapEvent != null || !p.IsPartyTradeActive) continue;
                    if (p.IsCurrentlyUsedByAQuest || Undead.Party(p)) continue;
                    int purse = p.PartyTradeGold;
                    if (purse <= 0) continue;
                    int spend = Math.Min(purse, Math.Max(1, (int)(purse * share)));      // co najmniej moneta - drobna kiesa tez sie rozchodzi
                    var town = ReferenceEquals(p.CurrentSettlement, null) || !p.CurrentSettlement.IsTown ? NearestOpenTown(p.Position.ToVec2()) : p.CurrentSettlement;
                    if (town == null || town.Town == null || spend <= 0) continue;
                    p.PartyTradeGold = purse - spend;
                    town.Town.ChangeGold(spend);
                    _lifeBands += spend;
                }
                catch (Exception e) { _lifeStumbles++; if (!_errLife) { _errLife = true; Log.Error("OutlawLaw.LifeDay(banda)", e); } }
            }
            if (!s.OutlawHoardCirculates) return;
            foreach (var h in Hideout.All)
            {
                try
                {
                    var hs = h != null ? h.Settlement : null;
                    if (hs == null || h.Gold <= 0) continue;
                    int keep;
                    if (!_hoardKeep.TryGetValue(hs, out keep)) continue;      // skarbiec niepoliczony (potkniecie wyzej): jak w HoardSurplus - nadwyzki nie ma, calej kasy nie ruszamy
                    int over = h.Gold - keep;
                    if (over <= 0 || HideoutPurge.Holds(hs)) continue;
                    int spend = Math.Min(over, Math.Max(1, (int)(over * share)));
                    var town = NearestOpenTown(hs.GetPosition2D);
                    if (town == null || town.Town == null || spend <= 0) continue;
                    HoardChange(hs, -spend);
                    town.Town.ChangeGold(spend);
                    _lifeHoards += spend;
                }
                catch (Exception e) { _lifeStumbles++; if (!_errLife) { _errLife = true; Log.Error("OutlawLaw.LifeDay(kryjowka)", e); } }
            }
        }

        // ------------------------------------------------------------ paser: skup lupu band
        private const int LootCargo = 0, LootFood = 1, LootArmour = 2, LootMount = 3, LootPack = 4, LootArms = 5, LootOther = 6, LootKinds = 7;

        /// <summary>
        /// Co lezy w jukach bandy.
        ///  LootCargo  - ladunek na sprzedaz: towary handlowe (typ Goods), zwierzeta hodowlane (typ Animal) i zywnosc, gdy `foodSells`;
        ///  LootFood   - zywnosc, ktorej paser nie bierze (tylko przy otwartym kurku zywnosci z niczego - skup zamienialby ja w zloto miast);
        ///  LootArmour - zbroja na korpus bez znacznika stanu: tego szuka Equip na awanse;
        ///  LootMount  - kon pod siodlo (awanse na konnych; piesi jada na zapasowych), LootPack - zwierze juczne (niesie lup);
        ///  LootArms   - bron, helmy, tarcze, reszta sprzetu i zbroje ze znacznikiem stanu: zostaja w jukach (z pomiarem);
        ///  LootOther  - przedmioty zadan i rzeczy niehandlowe.
        /// </summary>
        private static int LootKind(ItemRosterElement el, bool foodSells)
        {
            var it = el.EquipmentElement.Item;
            if (it == null || el.Amount <= 0) return -1;
            if (el.EquipmentElement.IsQuestItem || it.NotMerchandise) return LootOther;
            if (it.ItemType == ItemObject.ItemTypeEnum.Horse) return it.HorseComponent != null && it.HorseComponent.IsPackAnimal ? LootPack : LootMount;
            if (it.ItemType == ItemObject.ItemTypeEnum.BodyArmor) return el.EquipmentElement.ItemModifier == null ? LootArmour : LootArms;
            if (it.IsFood) return foodSells ? LootCargo : LootFood;
            if (it.ItemType == ItemObject.ItemTypeEnum.Goods || it.ItemType == ItemObject.ItemTypeEnum.Animal) return LootCargo;   // ladunek taborow i karawan, stada
            return LootArms;
        }

        private static int CargoUnits(ItemRoster pack, bool foodSells)
        {
            int n = 0;
            if (pack == null) return 0;
            for (int i = 0; i < pack.Count; i++) { var el = pack.GetElementCopyAtIndex(i); if (LootKind(el, foodSells) == LootCargo) n += el.Amount; }
            return n;
        }

        /// <summary>
        /// Cena skupu sztuki w miescie. Towary, zywnosc i stada: Town.GetItemPrice ze sprzedaza, bez partii - jak przy taborze wsi
        /// (na targ wnosi paser, czlowiek z miasta). Konie i zwierzeta juczne naleza do prawa podazy i popytu uzbrojenia
        /// (SupplyDemand), a ono liczy cene tylko wtedy, gdy zna kupca - dlatego tu wycena z polka miasta jako kupcem, jak przy
        /// nadwyzkach sprzedawanych przez ludzi lordow (MenPurse.SellPrice): zawalona polka placi mniej, podloga zlomu obowiazuje.
        /// </summary>
        private static int FencePrice(Settlement st, EquipmentElement what)
        {
            if (SupplyDemand.Equipmentish(what.Item)) return Math.Max(1, st.Town.MarketData.GetPrice(what, null, true, st.Party));
            return Math.Max(1, st.Town.GetItemPrice(what, null, true));
        }

        /// <summary>
        /// Banda sprzedaje paserom ladunek z jukow - kazda sztuke temu miastu w zasiegu, ktore za NASTEPNA sztuke da jej najwiecej
        /// na reke (cena skupu miasta x udzial bandy malejacy z odlegloscia). Cena liczona od nowa po kazdej sztuce, ktora trafila
        /// na polke (towar tanieje - jedna cena na stos przeplacalaby); reszta ceny to dzialka pasera, ktora zostaje w kasie
        /// miasta. Miasto placi tylko z tego, co ma ponad rezerwe na renty (TownRentFloorGold); gdy go nie stac, kupuje nastepne.
        /// Potem zwierzeta ponad potrzebe (SellSpare). Nic nie znika i nic nie powstaje: sztuka schodzi z jukow i trafia na polke
        /// (nieudane dolozenie cofa zdjecie), zaplata za to, co przeszlo, idzie w finally - takze po wyjatku przy kolejnej sztuce.
        /// </summary>
        private static void FenceSell(MobileParty p, float share, int reserve)
        {
            var s = Settings.Current;
            var pack = p.ItemRoster;
            if (pack == null || pack.Count == 0) return;
            bool food = s.OutlawNoFreeFood, spare = s.OutlawFenceBuysSpareAnimals;
            bool cargo = false, beasts = false;
            for (int i = 0; i < pack.Count; i++)
            {
                int k = LootKind(pack.GetElementCopyAtIndex(i), food);
                if (k == LootCargo) cargo = true; else if (k == LootMount || k == LootPack) beasts = true;
            }
            if (!cargo && !(spare && beasts)) return;
            if (cargo) _sellBands++;
            float radius = Math.Max(1f, s.OutlawFenceRadius);
            bool beyond;
            var bids = Reach(p, radius, out beyond);
            if (bids.Count == 0)
            {
                if (cargo) { _leftBands++; _leftNoTown++; _leftUnits += CargoUnits(pack, food); }
                return;
            }
            float far = MBMath.ClampFloat(s.OutlawFenceLootShareFar, 0f, share);      // daleko nigdy lepiej niz przy miescie
            foreach (var b in bids) b.Share = Slide(share, far, b.Dist, radius);
            if (cargo) { _sellFenced++; if (beyond) _sellBeyond++; }
            int units = 0;
            bool poor = false;
            try
            {
                // od konca: sprzedany do zera stos znika, a na jego miejsce wskakuje ostatni (juz obejrzany) - zaden nie zostaje pominiety
                for (int i = pack.Count - 1; i >= 0; i--)
                {
                    if (i >= pack.Count) continue;
                    var el = pack.GetElementCopyAtIndex(i);
                    if (LootKind(el, food) != LootCargo) continue;
                    units += SellStack(p, pack, el, bids, reserve, false, 0f, ref poor);
                }
                if (spare && beasts) units += SellSpare(p, pack, bids, reserve, ref poor);
            }
            finally
            {
                // "sprzedalo" w linii "Paser:" to bandy z ladunkiem (nie moze przekroczyc "band z ladunkiem"); banda, ktora miala do
                // sprzedania same zwierzeta ponad potrzebe, idzie do osobnego licznika
                if (units > 0) { if (cargo) _sellSellers++; else _sellSpareBands++; }
                if (cargo)
                {
                    int left = CargoUnits(pack, food);
                    if (left > 0) { _leftBands++; _leftUnits += left; if (poor) _leftPoor++; }
                }
            }
        }

        /// <summary>
        /// Sprzedaje do `el.Amount` sztuk jednego stosu. Dla zwierzat (`beast`) kazda sztuka przechodzi probe: po zdjeciu jej
        /// z jukow banda musi dalej uniesc to, co niesie (`weight`) - inaczej zwierze wraca do jukow i stos jest skonczony.
        /// Zaplata za sztuki, ktore przeszly, jest w finally: po miescie, z ulamkiem monety na korzysc pasera.
        /// </summary>
        private static int SellStack(MobileParty p, ItemRoster pack, ItemRosterElement el, List<Bid> bids, int reserve, bool beast, float weight, ref bool poor)
        {
            var what = el.EquipmentElement;
            var item = what.Item;
            bool exotic = beast && MountLaw.IsExotic(item);      // wielblad, slon, rydwan: tylko tam, gdzie targ ma prawo je trzymac (LegendaryLaw zdjalby je z polki)
            foreach (var b in bids)
            {
                b.Skip = b.Share <= 0f || (exotic && !MountLaw.AllowedForSettlement(b.St, item));
                b.Price = 0; b.Net = 0.0; b.Due = 0.0; b.Worth = 0; b.Moved = 0;
            }
            int sold = 0;
            try
            {
                for (int k = 0; k < el.Amount; k++)
                {
                    if (pack.FindIndexOfElement(what) < 0) break;           // stosu juz nie ma - nic nie moze trafic na polke bez zdjecia z jukow
                    pack.AddToCounts(what, -1);
                    bool placed = false;
                    try
                    {
                        if (beast && !Carries(p, weight)) break;            // bez tego zwierzecia banda nie uniesie jukow - zostaje
                        Bid best = null;
                        foreach (var b in bids)
                        {
                            if (b.Skip) continue;
                            if (b.Price <= 0) { b.Price = FencePrice(b.St, what); b.Net = b.Price * (double)b.Share; }
                            if (b.Town.Gold - reserve < (int)Math.Ceiling(b.Due + b.Net)) { b.Skip = true; poor = true; continue; }   // tego miasta na te sztuke nie stac
                            if (best == null || b.Net > best.Net) best = b;
                        }
                        if (best == null) break;                            // nikt w zasiegu nie kupi - reszta stosu zostaje na pozniej
                        best.St.ItemRoster.AddToCounts(what, 1);
                        placed = true;
                        best.Moved++; best.Due += best.Net; best.Worth += best.Price;
                        best.Price = 0;                                     // polka tego miasta sie zmienila - nastepna sztuke wyceni od nowa
                        sold++;
                    }
                    finally { if (!placed) pack.AddToCounts(what, 1); }     // sztuka, ktora nie trafila na polke, wraca do jukow
                }
            }
            finally
            {
                string id = item.StringId;                                  // ruda i drewno osobno - do zestawienia z liniami "Ruda:" i "Drewno:"
                foreach (var b in bids)
                {
                    if (b.Moved <= 0) continue;
                    int pay = (int)b.Due;                                   // ulamek monety zostaje u pasera
                    if (pay > 0) { b.Town.ChangeGold(-pay); p.PartyTradeGold += pay; }
                    _sellUnits += b.Moved; _sellPaid += pay; _sellWorth += b.Worth;
                    int zone = b.Real <= ZoneNear ? 0 : (b.Real <= ZoneMid ? 1 : 2);
                    _zoneUnits[zone] += b.Moved; _zonePaid[zone] += pay;
                    if (id == "iron") _sellIron += b.Moved; else if (id == "hardwood") _sellWood += b.Moved;
                    if (beast) _sellAnimals += b.Moved; else if (item.IsFood) _sellFood += b.Moved;
                    _sellTowns.Add(b.St);
                    b.Moved = 0; b.Due = 0.0; b.Worth = 0;
                }
            }
            return sold;
        }

        /// <summary>Czy banda uniesie to, co niesie (`weight`), z tym, co ma teraz w jukach (model nosnosci gry z cudzymi latkami).</summary>
        private static bool Carries(MobileParty p, float weight)
        {
            return Campaign.Current.Models.InventoryCapacityModel.CalculateInventoryCapacity(p, p.IsCurrentlyAtSea).ResultNumber >= weight;
        }

        /// <summary>Piesi bandy - kazdy moze jechac na zapasowym koniu, wiec tyle koni banda trzyma.</summary>
        private static int Footmen(MobileParty p)
        {
            var r = p.MemberRoster;
            int mounted = 0;
            for (int i = 0; i < r.Count; i++)
            {
                var e = r.GetElementCopyAtIndex(i);
                if (e.Character != null && e.Character.IsMounted) mounted += e.Number;
            }
            return Math.Max(0, r.TotalManCount - mounted);
        }

        /// <summary>
        /// Zwierzeta ponad potrzebe (po sprzedazy ladunku). Kon pod siodlo jest potrzebny, dopoki banda ma pieszego, ktory moze
        /// na nim jechac (i awansowac na konnego) - nadwyzka to konie ponad liczbe pieszych, najdrozsze ida pierwsze. Zwierze
        /// juczne jest potrzebne, dopoki bez niego banda nie uniesie reszty jukow - kazda sztuka przechodzi probe nosnosci.
        /// </summary>
        private static int SellSpare(MobileParty p, ItemRoster pack, List<Bid> bids, int reserve, ref bool poor)
        {
            float weight = Campaign.Current.Models.InventoryCapacityModel.CalculateTotalWeightCarried(p, p.IsCurrentlyAtSea).ResultNumber;
            var beasts = new List<ItemRosterElement>();
            int mounts = 0;
            for (int i = 0; i < pack.Count; i++)
            {
                var el = pack.GetElementCopyAtIndex(i);
                int kind = LootKind(el, false);
                if (kind == LootMount) { mounts += el.Amount; beasts.Add(el); }
                else if (kind == LootPack) beasts.Add(el);
            }
            int spareMounts = Math.Max(0, mounts - Footmen(p));
            beasts.Sort((x, y) => y.EquipmentElement.ItemValue.CompareTo(x.EquipmentElement.ItemValue));
            int sold = 0;
            foreach (var el in beasts)
            {
                bool mount = LootKind(el, false) == LootMount;
                int n = mount ? Math.Min(el.Amount, spareMounts) : el.Amount;
                if (n <= 0) continue;
                int got = SellStack(p, pack, new ItemRosterElement(el.EquipmentElement, n), bids, reserve, true, weight, ref poor);
                if (mount) spareMounts -= got;
                sold += got;
            }
            return sold;
        }

        /// <summary>Linia "Paser:" - raz na dobe, zaraz po linii "Wyrzutki:". Zeruje liczniki pasera, kryjowek i zywnosci.</summary>
        private static void FenceLog(Settings s, int boughtUnits, int boughtGold)
        {
            try
            {
                bool food = s.OutlawNoFreeFood;
                long purses = 0, hoards = 0, hoardMax = 0, keepSum = 0;
                int hoardN = 0;
                var units = new int[LootKinds]; var worth = new long[LootKinds];
                foreach (var p in MobileParty.AllBanditParties)
                {
                    if (p == null || !p.IsActive) continue;
                    purses += p.PartyTradeGold;
                    var r = p.ItemRoster;
                    if (r == null) continue;
                    for (int i = 0; i < r.Count; i++)
                    {
                        var el = r.GetElementCopyAtIndex(i);
                        int k = LootKind(el, food);
                        if (k < 0) continue;
                        units[k] += el.Amount; worth[k] += (long)el.Amount * el.EquipmentElement.ItemValue;
                    }
                }
                foreach (var h in Hideout.All)
                {
                    if (h == null) continue;
                    hoards += h.Gold;
                    if (h.Gold > 0) hoardN++;
                    if (h.Gold > hoardMax) hoardMax = h.Gold;
                }
                foreach (var kv in _hoardKeep) keepSum += kv.Value;
                float share = MBMath.ClampFloat(s.OutlawFenceLootShare, 0f, 1f);
                float far = MBMath.ClampFloat(s.OutlawFenceLootShareFar, 0f, share);
                bool sells = s.OutlawFenceBuysLoot && share > 0f;
                var inv = CultureInfo.InvariantCulture;
                var sb = new StringBuilder("Paser: dzien " + (int)CampaignTime.Now.ToDays);
                if (sells)
                {
                    sb.Append(" | skup lupu (udzial bandy ").Append(share.ToString("0.00", inv)).Append(" przy miescie -> ").Append(far.ToString("0.00", inv))
                      .Append(" na granicy promienia ").Append(((int)Math.Max(1f, s.OutlawFenceRadius)).ToString(inv))
                      .Append("): band z ladunkiem ").Append(_sellBands).Append(", z paserem ").Append(_sellFenced).Append(" (ma byc 100%; w tym poza promieniem ").Append(_sellBeyond)
                      .Append("), sprzedalo ").Append(_sellSellers).Append(" w ").Append(_sellTowns.Count).Append(" miastach - ")
                      .Append(_sellUnits).Append(" szt. (w tym ruda ").Append(_sellIron).Append(", drewno ").Append(_sellWood)
                      .Append(" ladunkow, zywnosc ").Append(_sellFood).Append(", zwierzeta ponad potrzebe ").Append(_sellAnimals)
                      .Append(") za ").Append(_sellPaid).Append(" zl; po cenach skupu miast ").Append(_sellWorth)
                      .Append(" zl, dzialka pasera ").Append(_sellWorth - _sellPaid).Append(" zl zostala w kasach miast")
                      .Append("; same zwierzeta ponad potrzebe (bez innego ladunku) sprzedalo ").Append(_sellSpareBands).Append(" band")
                      .Append(" | wg odleglosci od miasta, ktore kupilo: do 50 - ").Append(_zoneUnits[0]).Append(" szt. za ").Append(_zonePaid[0])
                      .Append(" zl, 50-100 - ").Append(_zoneUnits[1]).Append(" szt. za ").Append(_zonePaid[1])
                      .Append(" zl, dalej - ").Append(_zoneUnits[2]).Append(" szt. za ").Append(_zonePaid[2]).Append(" zl")
                      .Append(" | z ladunkiem zostalo ").Append(_leftBands).Append(" band (").Append(_leftUnits).Append(" szt. razem z bandami poza handlem): kasy miast w zasiegu przy rezerwie ")
                      .Append(_leftPoor).Append(", brak otwartego miasta ").Append(_leftNoTown)
                      .Append("; poza handlem z ladunkiem: w bitwie ").Append(_leftBattle).Append(", bez kiesy ").Append(_leftNoPurse)
                      .Append(", zajete przez zadanie ").Append(_leftQuest).Append(", nieumarli ").Append(_leftUndead);
                    if (!food) sb.Append("; zywnosci paser nie bierze (kurek zywnosci z niczego otwarty)");
                    if (!s.OutlawFenceBuysSpareAnimals) sb.Append("; zwierzat ponad potrzebe nie bierze (wylaczone)");
                }
                else
                    sb.Append(" | skup lupu WYLACZONY w ustawieniach (bandy niczego nie sprzedaja)");
                sb.Append(" | kupno sprzetu u pasera: ").Append(boughtUnits).Append(" awansow za ").Append(boughtGold).Append(" zl (w tym z kas kryjowek ").Append(_hoardGear).Append(" zl)")
                  .Append(" | zycie w miastach: bandy wydaly ").Append(_lifeBands).Append(" zl, kryjowki z nadwyzki ").Append(_lifeHoards).Append(" zl")
                  .Append(" | kryjowki: wejsc band ").Append(_hideEntries);
                if (s.OutlawNoHideoutGold)
                    sb.Append(", zloto z niczego zablokowane - bandom ").Append(_hideBand).Append(" zl, kryjowkom ").Append(_hideHoard).Append(" zl");
                else
                    sb.Append(", KUREK OTWARTY w ustawieniach - gra dopisala z niczego bandom ").Append(_hideBand).Append(" zl, kryjowkom ").Append(_hideHoard).Append(" zl");
                sb.Append("; kasy kryjowek: doplyw ").Append(_hideStash).Append(" zl (bandy odlozyly z wlasnych kies), odplyw ").Append(_hoardStart + _hoardGear + _lifeHoards)
                  .Append(" zl (kiesy startowe nowych band ").Append(_hoardStart).Append(" z ").Append(_hoardStartWant).Append(" zl, na ktore liczyly; sprzet ").Append(_hoardGear)
                  .Append("; zycie w miastach ").Append(_lifeHoards).Append("); stan ").Append(hoards).Append(" zl w ").Append(hoardN).Append(" kryjowkach, najwieksza ").Append(hoardMax)
                  .Append(" zl, skarbce (to, co zostaje dla zdobywcy) razem do ").Append(keepSum).Append(" zl");
                if (!s.OutlawHoardCirculates) sb.Append("; OBIEG KAS WYLACZONY - kiesy startowe z niczego: ").Append(_startNothing).Append(" zl");
                sb.Append(" | zywnosc z niczego: ");
                if (food) sb.Append("zablokowana ").Append(_foodBands).Append(" nowym bandom, w bitwie zdjete ").Append(_foodTaken).Append(" szt.");
                else sb.Append("KUREK OTWARTY w ustawieniach (gra daje zywnosc kazdej nowej bandzie)");
                sb.Append(" | kiesy band, ktore dzis zniknely z mapy: rozbitych ").Append(_goneFought).Append(" zl (reszta po dzialce zwyciezcy - przepada), rozwiazanych ")
                  .Append(_goneDisbanded).Append(" zl (przepada)")
                  .Append(" | stan: kiesy band ").Append(purses).Append(" zl; w jukach band: ladunek na sprzedaz ").Append(units[LootCargo]).Append(" szt. (").Append(worth[LootCargo])
                  .Append(" zl), zywnosc poza skupem ").Append(units[LootFood]).Append(" (").Append(worth[LootFood])
                  .Append(" zl), zbroje na awanse ").Append(units[LootArmour]).Append(" (").Append(worth[LootArmour])
                  .Append(" zl), konie pod siodlo ").Append(units[LootMount]).Append(" (").Append(worth[LootMount])
                  .Append(" zl), juczne ").Append(units[LootPack]).Append(" (").Append(worth[LootPack])
                  .Append(" zl), bron i inny sprzet - nie na sprzedaz ").Append(units[LootArms]).Append(" (").Append(worth[LootArms])
                  .Append(" zl), niehandlowe ").Append(units[LootOther]).Append(" (wartosci wg cen bazowych)");
                if (_sellStumbles + _hideStumbles + _lifeStumbles + _upStumbles > 0)
                    sb.Append(" | potkniecia: skup ").Append(_sellStumbles).Append(", kryjowki ").Append(_hideStumbles).Append(", zycie ").Append(_lifeStumbles)
                      .Append(", awanse ").Append(_upStumbles);
                sb.Append('.');
                Log.Info(sb.ToString());
            }
            catch (Exception e) { if (!_errFenceLog) { _errFenceLog = true; Log.Error("OutlawLaw.FenceLog", e); } }
            finally { FenceNewDay(); }
        }

        // ------------------------------------------------------------ kurek kryjowek
        // BanditSpawnCampaignBehavior.OnSettlementEntered: gdy banda wchodzi do kryjowki, gra liczy wartosc jej jukow (bez zapasu
        // zywnosci) i dopisuje bandzie oraz kryjowce po 25% tej wartosci jako zloto - towaru nie zdejmuje, wiec przy kazdym wejsciu
        // od nowa. Prefiks zapamietuje kiese bandy i kase kryjowki, postfiks zdejmuje to, co doszlo: oryginal biegnie caly (herszt,
        // wykrycie kryjowki, losowania gry w tej samej kolejnosci). W zamian banda odklada w kryjowce czesc WLASNEJ kiesy -
        // przelew, nie dosypka. Metoda bywa zagniezdzona (gra tworzy partie herszta i wprowadza ja do tej samej kryjowki w srodku
        // wywolania, przed swoja dosypka), stad stan w __state i suma _hoardSeen: wplata herszta nie jest dosypka gry.

        /// <summary>Stan przed: [kiesa bandy, kasa kryjowki, _hoardSeen kryjowki]. Null = to nie banda w kryjowce albo modul wylaczony.</summary>
        public static void HideoutGoldPrefix(MobileParty __0, Settlement __1, out long[] __state)
        {
            __state = null;
            try
            {
                if (!On || __0 == null || __1 == null || !__0.IsBandit || !__1.IsHideout) return;
                var c = Campaign.Current;
                if (c == null || !c.GameStarted) return;              // przed startem kampanii gra niczego tu nie dopisuje
                var comp = __1.SettlementComponent;
                if (comp == null) return;
                long seen; _hoardSeen.TryGetValue(__1, out seen);
                __state = new long[] { __0.PartyTradeGold, comp.Gold, seen };
            }
            catch { __state = null; }
        }

        public static void HideoutGoldPostfix(MobileParty __0, Settlement __1, long[] __state)
        {
            if (__state == null) return;
            try
            {
                var s = Settings.Current;
                var comp = __1.SettlementComponent;
                long seen; _hoardSeen.TryGetValue(__1, out seen);
                long gotBand = __0.PartyTradeGold - __state[0];
                long gotHoard = comp.Gold - __state[1] - (seen - __state[2]);      // bez tego, co zostawily wejscia zagniezdzone
                _hideEntries++;
                if (gotBand > 0) _hideBand += gotBand;
                if (gotHoard > 0) _hideHoard += gotHoard;
                int snap;
                if (!s.OutlawNoHideoutGold)
                {
                    if (gotHoard > 0) _hoardSeen[__1] = seen + gotHoard;           // kurek otwarty - tylko pomiar; dosypka zostaje w kasie
                    // ... i w kiesie: banda z migawki dziennego ticku gry (urodzona w jego srodku) nie moze jej stracic przy przywracaniu kies
                    if (gotBand > 0 && _gold.TryGetValue(__0, out snap)) _gold[__0] = snap + (int)gotBand;
                    return;
                }
                if (gotBand > 0) __0.PartyTradeGold -= (int)gotBand;
                if (gotHoard > 0) comp.ChangeGold(-(int)gotHoard);
                float share = MBMath.ClampFloat(s.OutlawHideoutStashShare, 0f, 1f);
                if (share <= 0f || !__0.IsPartyTradeActive) return;
                int put = (int)(__0.PartyTradeGold * share);
                if (put <= 0) return;
                __0.PartyTradeGold -= put;
                comp.ChangeGold(put);
                _hoardSeen[__1] = seen + put;
                _hideStash += put;
                // wejscie w srodku dziennego ticku gry (w praktyce: banda urodzona w tym ticku, dopisana do migawki przez StartGoldPostfix):
                // GoldPostfix przywraca kiesy z migawki - wplata nie moze wrocic do bandy
                if (_gold.TryGetValue(__0, out snap)) _gold[__0] = Math.Max(0, snap - put);
            }
            catch (Exception e)
            {
                _hideStumbles++;
                if (!_errHide) { _errHide = true; Log.Error("OutlawLaw.HideoutGold", e); }
            }
        }

        // ------------------------------------------------------------ zapis
        internal static string Export()
        {
            if (_pool.Count == 0 && !_seeded) return "";
            var sb = new StringBuilder("v1;" + (_seeded ? "1" : "0"));
            foreach (var kv in _pool)
            {
                if (kv.Value.Count == 0) continue;
                sb.Append('|').Append(kv.Key).Append('=');
                bool first = true;
                foreach (var t in kv.Value)
                {
                    if (t.Value < 0.01f) continue;
                    if (!first) sb.Append(',');
                    sb.Append(t.Key).Append(':').Append(t.Value.ToString("0.###", CultureInfo.InvariantCulture));
                    first = false;
                }
            }
            return sb.ToString();
        }

        internal static void Import(string data)
        {
            try
            {
                _pool.Clear(); _seeded = false;
                if (string.IsNullOrEmpty(data)) return;
                var parts = data.Split('|');
                var head = parts[0].Split(';');
                _seeded = head.Length > 1 && head[1] == "1";
                for (int i = 1; i < parts.Length; i++)
                {
                    int eq = parts[i].IndexOf('=');
                    if (eq <= 0) continue;
                    var d = new Dictionary<string, float>();
                    foreach (var t in parts[i].Substring(eq + 1).Split(','))
                    {
                        int c = t.LastIndexOf(':');
                        if (c <= 0) continue;
                        float v;
                        if (float.TryParse(t.Substring(c + 1), NumberStyles.Float, CultureInfo.InvariantCulture, out v) && v > 0f) d[t.Substring(0, c)] = v;
                    }
                    if (d.Count > 0) _pool[parts[i].Substring(0, eq)] = d;
                }
                Log.Info("Wyrzutki: wczytano pule " + (int)Total() + " ludzi w " + _pool.Count + " regionach.");
            }
            catch (Exception e) { Log.Error("OutlawLaw.Import", e); }
        }

        // ------------------------------------------------------------ latki
        /// <summary>Postfiks kiesy startowej bierze pierwszy parametr po pozycji (__0) - sprawdzamy jego typ, nie nazwe.</summary>
        private static bool StartGoldFits(System.Reflection.MethodInfo m)
        {
            var ps = m.GetParameters();
            return ps.Length >= 1 && ps[0].ParameterType == typeof(MobileParty);
        }

        internal static void ApplyAll(Harmony h)
        {
            var done = new List<string>();
            var miss = new List<string>();
            Action<string, string, string, string> pre = (type, method, hook, label) =>
            {
                try
                {
                    var t = AccessTools.TypeByName(type);
                    var m = t != null ? AccessTools.Method(t, method) : null;
                    if (m == null) { miss.Add(label); return; }
                    h.Patch(m, prefix: new HarmonyMethod(typeof(OutlawLaw), hook));
                    done.Add(label);
                }
                catch (Exception e) { miss.Add(label + "(" + e.Message + ")"); }
            };
            const string bsc = "TaleWorlds.CampaignSystem.CampaignBehaviors.BanditSpawnCampaignBehavior";
            pre(bsc, "SpawnBanditParty", nameof(GateClan), "vanilla banda");
            pre(bsc, "SpawnLooterParty", nameof(GateLooter), "vanilla lupiezcy");
            pre(bsc, "FillANewHideoutWithBandits", nameof(GateFill), "vanilla nowa kryjowka");
            pre("NavalDLC.CampaignBehaviors.PiratesCampaignBehavior", "SpawnPirateParty", nameof(GatePirate), "NavalDLC piraci");
            pre("BannerKings.Behaviours.BKBanditBehavior", "InfestHieout", nameof(GateBkInfest), "BK kryjowka");
            pre("BannerKings.Behaviours.BKBanditBehavior", "CreateBanditHero", nameof(GateBkHero), "BK bohater");
            pre("BannerKings.Behaviours.BKBanditBehavior", "UpgradeParty", nameof(SkipBkUpgrade), "BK dosypka");
            pre("TaleWorlds.CampaignSystem.CampaignBehaviors.DesertersCampaignBehavior", "TrySpawnDeserters", nameof(SkipVanillaDeserters), "vanilla dezerterzy");
            pre("TaleWorlds.CampaignSystem.CampaignBehaviors.PartyUpgraderCampaignBehavior", "UpgradeReadyTroops", nameof(UpgradePrefix), "awanse band");
            // zywnosc z niczego dla nowych band: gra i jej kopia w NavalDLC (piraci) - ten sam prefiks, wlacznik OutlawNoFreeFood
            pre(bsc, "GiveFoodToBanditParty", nameof(FoodPrefix), "zywnosc band");
            pre("NavalDLC.CampaignBehaviors.PiratesCampaignBehavior", "GiveFoodToBanditParty", nameof(FoodPrefix), "NavalDLC zywnosc piratow");
            try
            {
                var t = AccessTools.TypeByName(bsc);
                var dt = t != null ? AccessTools.Method(t, "DailyTick") : null;
                if (dt != null) { h.Patch(dt, prefix: new HarmonyMethod(typeof(OutlawLaw), nameof(GoldPrefix)), postfix: new HarmonyMethod(typeof(OutlawLaw), nameof(GoldPostfix))); done.Add("zloto dzienne"); }
                else miss.Add("zloto dzienne");
                var cpt = t != null ? AccessTools.Method(t, "CreatePartyTrade") : null;
                if (cpt != null && StartGoldFits(cpt)) { h.Patch(cpt, postfix: new HarmonyMethod(typeof(OutlawLaw), nameof(StartGoldPostfix))); done.Add("zloto startowe"); }
                else miss.Add("zloto startowe");
            }
            catch (Exception e) { miss.Add("zloto(" + e.Message + ")"); }
            try
            {
                // piraci NavalDLC maja wlasna kopie CreatePartyTrade (5-15 zl na czlowieka z niczego) - ta sama zasada co dla band z ladu
                var nt = AccessTools.TypeByName("NavalDLC.CampaignBehaviors.PiratesCampaignBehavior");
                var ncpt = nt != null ? AccessTools.Method(nt, "CreatePartyTrade") : null;
                if (ncpt != null && StartGoldFits(ncpt)) { h.Patch(ncpt, postfix: new HarmonyMethod(typeof(OutlawLaw), nameof(StartGoldPostfix))); done.Add("NavalDLC zloto startowe"); }
                else miss.Add("NavalDLC zloto startowe");
            }
            catch (Exception e) { miss.Add("NavalDLC zloto startowe(" + e.Message + ")"); }
            try
            {
                // kurek kryjowek: parametry bierzemy po pozycji (__0, __1), wiec sprawdzamy typy, nie nazwy
                var t = AccessTools.TypeByName(bsc);
                var ose = t != null ? AccessTools.Method(t, "OnSettlementEntered") : null;
                var ps = ose != null ? ose.GetParameters() : null;
                if (ose != null && !ose.IsStatic && ose.ReturnType == typeof(void) && ps.Length == 3 && ps[0].ParameterType == typeof(MobileParty) && ps[1].ParameterType == typeof(Settlement))
                {
                    h.Patch(ose, prefix: new HarmonyMethod(typeof(OutlawLaw), nameof(HideoutGoldPrefix)), postfix: new HarmonyMethod(typeof(OutlawLaw), nameof(HideoutGoldPostfix)));
                    done.Add("zloto kryjowek");
                }
                else miss.Add("zloto kryjowek");
            }
            catch (Exception e) { miss.Add("zloto kryjowek(" + e.Message + ")"); }

            int rosters = 0;
            var seen = new HashSet<Type>();
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = asm.GetTypes(); } catch { continue; }
                foreach (var t in types)
                {
                    try
                    {
                        if (t == null || t.IsAbstract || !typeof(TaleWorlds.CampaignSystem.ComponentInterfaces.PartySizeLimitModel).IsAssignableFrom(t)) continue;
                        var m = t.GetMethod("FindAppropriateInitialRosterForMobileParty", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                        if (m == null || m.DeclaringType != t || seen.Contains(t)) continue;
                        seen.Add(t);
                        h.Patch(m, prefix: new HarmonyMethod(typeof(OutlawLaw), nameof(RosterPrefix)) { priority = Priority.First },
                                   postfix: new HarmonyMethod(typeof(OutlawLaw), nameof(RosterPostfix)) { priority = Priority.Last },
                                   finalizer: new HarmonyMethod(typeof(OutlawLaw), nameof(RosterFinalizer)));
                        rosters++;
                    }
                    catch { }
                }
            }
            Log.Info("OutlawLaw: sklad band z puli w " + rosters + " modelach; wpiete: " + string.Join(", ", done.ToArray())
                     + (miss.Count > 0 ? "; BRAK: " + string.Join(", ", miss.ToArray()) : "") + ".");
        }
    }
}
