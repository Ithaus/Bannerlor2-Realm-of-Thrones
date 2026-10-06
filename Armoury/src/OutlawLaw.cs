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

        internal static void Reset()
        {
            _pool.Clear(); _seeded = false; _nodes = null; _near.Clear(); _regionOf.Clear(); _commonerOf.Clear(); _empty.Clear(); _refusedHour.Clear();
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
                if (!On || party == null || destroyer != null) return;
                if (!IsOutlawParty(party)) return;
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
        public static void GoldPrefix()
        {
            _gold.Clear();
            if (!On || !Settings.Current.OutlawNoFreeGold) return;
            foreach (var p in MobileParty.AllBanditParties) if (p.IsPartyTradeActive) _gold[p] = p.PartyTradeGold;
        }
        public static void GoldPostfix()
        {
            foreach (var kv in _gold) { try { kv.Key.PartyTradeGold = kv.Value; } catch { } }
            _gold.Clear();
        }
        public static void StartGoldPostfix(MobileParty banditParty)
        {
            try
            {
                if (!On || !Settings.Current.OutlawNoFreeGold || banditParty == null) return;
                banditParty.PartyTradeGold = banditParty.MemberRoster.TotalManCount * Math.Max(0, Settings.Current.OutlawCoinsPerMan);
            }
            catch { }
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
            catch (Exception e) { Log.Error("OutlawLaw.Upgrade", e); return false; }
        }

        private static float BodyOf(CharacterObject c)
        {
            try { var it = c.FirstBattleEquipment[EquipmentIndex.Body].Item; return it != null && it.ArmorComponent != null ? it.ArmorComponent.BodyArmor : 0f; }
            catch { return 0f; }
        }

        /// <summary>Jedna sztuka sprzetu na awans: zbroja (gdy cel nosi ciezsza) i kon (gdy cel konny).</summary>
        private static bool Equip(MobileParty p, CharacterObject from, CharacterObject to)
        {
            float need = BodyOf(to);
            bool armour = need > BodyOf(from) + 2f;
            bool horse = to.IsMounted && !from.IsMounted;
            if (!armour && !horse) return true;
            Func<ItemObject, bool> armourOk = it => it.ItemType == ItemObject.ItemTypeEnum.BodyArmor && it.ArmorComponent != null && it.ArmorComponent.BodyArmor >= need * 0.8f;
            Func<ItemObject, bool> horseOk = it => it.ItemType == ItemObject.ItemTypeEnum.Horse && it.HorseComponent != null && !it.HorseComponent.IsPackAnimal;
            ItemObject a = null, h = null;
            bool aFence = false, hFence = false;
            if (armour) { a = Find(p.ItemRoster, armourOk); if (a == null) { a = Find(Fence(p), armourOk); aFence = a != null; } if (a == null) return false; }
            if (horse) { h = Find(p.ItemRoster, horseOk); if (h == null) { h = Find(Fence(p), horseOk); hFence = h != null; } if (h == null) return false; }
            int price = 0;
            float markup = Settings.Current.OutlawFenceMarkup;
            if (aFence) price += (int)(a.Value * markup);
            if (hFence) price += (int)(h.Value * markup);
            if (price > 0 && p.PartyTradeGold < price) return false;
            var town = Fence(p) != null ? FenceTown(p) : null;
            // wpis 90 (audyt 1-43 F8): zdejmujemy sztuke w jej stanie - dotad zuzyta zostawala na polce, banda awansowala z niczego
            if (a != null) { if (!Helper.RemoveOne(aFence ? town.ItemRoster : p.ItemRoster, a)) return false; }
            if (h != null) { if (!Helper.RemoveOne(hFence ? town.ItemRoster : p.ItemRoster, h)) return false; }
            if (price > 0)
            {
                p.PartyTradeGold -= price;
                try { town.SettlementComponent.ChangeGold(price); } catch { }
                _fenceGold += price; _upFence++;
            }
            else _upLoot++;
            return true;
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

        private static Settlement FenceTown(MobileParty p)
        {
            try
            {
                if (p.CurrentSettlement != null && p.CurrentSettlement.IsTown) return p.CurrentSettlement;
                var pos = p.Position.ToVec2();
                float r = Settings.Current.OutlawFenceRadius;
                Settlement best = null; float bd = r * r;
                foreach (var s in Nodes())
                {
                    if (!s.IsTown) continue;
                    float d = pos.DistanceSquared(s.GetPosition2D);
                    if (d <= bd) { bd = d; best = s; }
                }
                return best;
            }
            catch { return null; }
        }

        private static ItemRoster Fence(MobileParty p) { var t = FenceTown(p); return t != null ? t.ItemRoster : null; }

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
            try
            {
                var t = AccessTools.TypeByName(bsc);
                var dt = t != null ? AccessTools.Method(t, "DailyTick") : null;
                if (dt != null) { h.Patch(dt, prefix: new HarmonyMethod(typeof(OutlawLaw), nameof(GoldPrefix)), postfix: new HarmonyMethod(typeof(OutlawLaw), nameof(GoldPostfix))); done.Add("zloto dzienne"); }
                else miss.Add("zloto dzienne");
                var cpt = t != null ? AccessTools.Method(t, "CreatePartyTrade") : null;
                if (cpt != null) { h.Patch(cpt, postfix: new HarmonyMethod(typeof(OutlawLaw), nameof(StartGoldPostfix))); done.Add("zloto startowe"); }
                else miss.Add("zloto startowe");
            }
            catch (Exception e) { miss.Add("zloto(" + e.Message + ")"); }

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
