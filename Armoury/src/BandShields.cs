using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.AgentOrigins;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// n20-herby (Jeff 19.10: "jak walka jest to wywalmy tarcze bandytow ... oni powinni miec herby z armii lub takie jakie zdezerterowali lub nakradli
    /// tarcze zbroje itp, a nie herb bandyty"): TARCZE BAND W BITWIE.
    ///
    /// Skad herb na tarczy (dekompilacja 1.4.8): Mission.SpawnTroop -> new AgentBuildData(troop).Banner(troopOrigin.Banner); PartyGroupAgentOrigin.Banner
    /// i PartyAgentOrigin.Banner = herb rodu wodza bandy (herszt BK) albo Party.MapFaction.Banner - dla bandy klan bandycki. Mission.SpawnAgent ->
    /// Agent.InitializeMissionEquipment(AgentOverridenSpawnMissionEquipment, AgentBanner) -> new MissionEquipment(SpawnEquipment, banner): kazda bron
    /// (tarcza) agenta niesie ten herb. Nasze mody herbu agenta dotad nie ruszaly; BK, BKROTPatch, RBM, DTE i DynamicReinforcements tez nie
    /// (BannerColorPersistence z ROT-Core zmienia przy spawnie tylko kolory ubran partii z bohaterem-wodzem; ROT patch24 i NavalDLC buduja gotowa bron
    /// - AgentOverridenSpawnMissionEquipment - tylko zaciagowi gracza i w misjach fabularnych; parametr herbu w OnAgentBuild nikt w bitwie nie czyta).
    ///
    /// Ten sam herb gra czyta tez z POCHODZENIA agenta (agent.Origin.Banner): zbroje i tuniki z herbem (MissionScreen.OnEquipItemsFromSpawnEquipment,
    /// IsUsingTableau - w ROT m.in. chain_armor_short_tabbard, velaryon_soldier_armor, northern_knight_armor), Agent.UpdateSpawnEquipmentAndRefreshVisuals,
    /// znacznik oddzialu z kapitanem (FormationIndicatorMissionView), pociski machin i strzaly plonace NavalDLC (recenzja 19.10).
    ///
    /// TERAZ: (a) postfiks gettera Banner w PartyGroupAgentOrigin i PartyAgentOrigin - dla czlowieka bandy zwraca herb wybrany dla niego (raz na pochodzenie
    /// w misji, pamiec _chosen), wiec Mission.SpawnTroop sam poda go do AgentBuildData, a tunika, znacznik i pociski maja ten sam herb co tarcza;
    /// (b) prefiks Mission.SpawnAgent (Priority.High - przed prefiksami, ktore buduja bron z AgentBanner) ustawia ten sam herb w AgentBuildData
    /// (agenci budowani bez SpawnTroop). Czlowiek partii frakcji bandyckiej (bez Innych) dostaje herb prawdziwego rodu:
    ///  (1) DEZERTER albo rozbitek (jednostka kultury krolestwa, nie bandycka - OutlawLaw zostawia dezerterow i rozbitkow w ich typie zolnierza) - herb
    ///      pana ziemi, z ktorej wzial go pobor: regiony pochodzenia skladu bandy z ksiegi ludzi (PeopleBook E4/7, udzialy region -> ludzie), tylko regiony
    ///      jego kultury (kultura osady, rodu pana albo krolestwa pana); losowanie wagi ludzi;
    ///  (2) bez pochodzenia jego kultury - 3 najblizsze rody jego kultury (panowie najblizszych warowni; wagi 3/2/1);
    ///  (3) bez takiego rodu - herb krolestwa jego kultury;
    ///  (4) BANDYTA z urodzenia (kultura bandycka albo kultura klanu bandy) i kazdy bez (1)-(3) - NAKRADZIONE: gospodarze 3 najblizszych wsi gry
    ///      (VillageHolders - ten sam herb co proporczyki wiosek na mapie; bez klucza Village Banner Per Holder - pan wsi) i panowie 3 najblizszych warowni,
    ///      rowno; pusto (same rody gracza) - kolejne warownie od najblizszej.
    /// Nigdy herb strony gracza w tej bitwie (Twoj rod, rody partii po Twojej stronie i ich krolestwa - zebys w bitwie nie bral bandyty za swojego
    /// ani za sojusznika), herb bandycki ani Innych. Wybor deterministyczny na czlowieka (UniqueSeed rosteru + id bandy); listy herbow liczone raz na
    /// bande w misji (bez kosztu co klatke; liczy tylko watek glowny, inne watki czytaja gotowy wybor pod zamkiem). Kolory ubran (kolory druzyny) bez zmian.
    /// Klucz BandShieldsStolenArms (off - herb bandy, jak dotad); linia "Tarcze band (herby):" po rozstawieniu i na koniec misji, gdy doszli ludzie.
    /// </summary>
    internal static class BandShields
    {
        internal static bool On { get { var s = Settings.Current; return s != null && s.BandShieldsStolenArms; } }

        private const int KOrigin = 0, KNear = 1, KRealm = 2, KStolen = 3, KKinds = 4;
        private const int NearVillages = 3, NearForts = 3, NearHouses = 3, MaxFortScan = 60;
        private const int MaxStolen = 12, MaxOrigin = 6;            // najwiecej roznych herbow: nakradzionych na bande, z pochodzenia na kulture

        /// <summary>Lista herbow do losowania: herb, waga, nazwa rodu (do linii logu).</summary>
        private sealed class Pick
        {
            public readonly List<Banner> B = new List<Banner>(8);
            public readonly List<float> W = new List<float>(8);
            public readonly List<string> L = new List<string>(8);
            public float Sum;

            public void Add(Banner b, float w, string label)
            {
                if (b == null || !(w > 0f)) return;
                string code = b.BannerCode;
                for (int i = 0; i < B.Count; i++)
                    if (string.Equals(B[i].BannerCode, code, StringComparison.Ordinal)) { W[i] += w; Sum += w; return; }   // ten sam herb - jedna pozycja
                B.Add(b); W.Add(w); L.Add(label ?? "?"); Sum += w;
            }

            /// <summary>Zostaw `max` pozycji o najwiekszej wadze (kolejnosc wedlug wagi; rowne - wczesniejsza pierwsza).</summary>
            public void Trim(int max)
            {
                if (B.Count <= max) return;
                var idx = new List<int>(B.Count);
                for (int i = 0; i < B.Count; i++) idx.Add(i);
                idx.Sort((x, y) => W[y] != W[x] ? W[y].CompareTo(W[x]) : x.CompareTo(y));
                var nb = new List<Banner>(max); var nw = new List<float>(max); var nl = new List<string>(max);
                for (int i = 0; i < max; i++) { nb.Add(B[idx[i]]); nw.Add(W[idx[i]]); nl.Add(L[idx[i]]); }
                B.Clear(); W.Clear(); L.Clear(); Sum = 0f;
                for (int i = 0; i < max; i++) { B.Add(nb[i]); W.Add(nw[i]); L.Add(nl[i]); Sum += nw[i]; }
            }

            public int Draw(uint h)
            {
                if (B.Count == 0 || !(Sum > 0f)) return -1;
                float r = (float)(h / 4294967296.0) * Sum;
                for (int i = 0; i < B.Count; i++) { r -= W[i]; if (r < 0f) return i; }
                return B.Count - 1;
            }
        }

        /// <summary>Pamiec jednej bandy w misji (raz na bande): polozenie, warownie od najblizszej, listy herbow wedlug kultury.</summary>
        private sealed class PartyArms
        {
            public PartyBase Party;
            public Vec2 Pos;
            public uint Salt;
            public string OwnBanner;                                   // kod herbu bandy (nigdy na tarczy)
            public List<Settlement> Forts;                             // warownie od najblizszej (leniwie)
            public List<KeyValuePair<Settlement, double>> Regions;     // pochodzenie skladu z ksiegi ludzi (leniwie)
            public Pick Stolen;                                        // nakradzione (leniwie)
            public readonly Dictionary<string, Pick> Origin = new Dictionary<string, Pick>(StringComparer.Ordinal);
            public readonly Dictionary<string, Pick> Near = new Dictionary<string, Pick>(StringComparer.Ordinal);
        }

        // ---------------------------------------------------------------- stan misji (tylko watek misji; czyszczony przy nowej misji i na jej koncu)
        private static WeakReference _mission;
        private static readonly Dictionary<PartyBase, PartyArms> _arms = new Dictionary<PartyBase, PartyArms>();
        private static readonly Dictionary<string, Pick> _realm = new Dictionary<string, Pick>(StringComparer.Ordinal);
        private static readonly HashSet<string> _codes = new HashSet<string>(StringComparer.Ordinal);
        private static readonly Dictionary<string, int> _byHouse = new Dictionary<string, int>(StringComparer.Ordinal);
        private static readonly int[] _kind = new int[KKinds];
        private static int _men, _heroes, _none, _ready, _skipUndead, _stMission, _loggedMen = -1, _seedless, _friendParties, _originHits;
        // herby strony gracza w tej bitwie (Twoj rod, rody partii po Twojej stronie i ich krolestwa) - nigdy na tarczy bandyty
        private static readonly HashSet<string> _friendCodes = new HashSet<string>(StringComparer.Ordinal);

        // wybor na czlowieka: pochodzenie agenta -> herb (null = bez zmiany). Pisze tylko watek glowny; czyta kazdy watek - zawsze pod zamkiem.
        private static readonly object _gate = new object();
        private static readonly Dictionary<IAgentOriginBase, Banner> _chosen = new Dictionary<IAgentOriginBase, Banner>(new RefEq());
        private static int _main = -1;                                  // watek glowny (ApplyAll w OnBeforeInitialModuleScreenSetAsRoot)

        private sealed class RefEq : IEqualityComparer<IAgentOriginBase>
        {
            public bool Equals(IAgentOriginBase x, IAgentOriginBase y) { return ReferenceEquals(x, y); }
            public int GetHashCode(IAgentOriginBase o) { return RuntimeHelpers.GetHashCode(o); }
        }

        // ---------------------------------------------------------------- sesja
        private static int _stSession;
        private static readonly HashSet<string> _errSites = new HashSet<string>(StringComparer.Ordinal);

        private static void Stumble(string where, Exception e)
        {
            _stSession++; _stMission++;
            bool first;
            lock (_errSites) first = _errSites.Add(where);
            if (first || _stSession % 500 == 0) Log.Error("BandShields." + where + " (potkniecie " + _stSession + ")", e);
        }

        internal static void ApplyAll(Harmony h)
        {
            _main = Environment.CurrentManagedThreadId;
            try
            {
                var m = AccessTools.DeclaredMethod(typeof(Mission), "SpawnAgent");
                if (m == null) Log.Info("Tarcze band (herby): brak Mission.SpawnAgent - latka nie weszla.");
                else
                {
                    h.Patch(m, prefix: new HarmonyMethod(typeof(BandShields), nameof(Prefix)) { priority = Priority.High });
                    Log.Info("Tarcze band (herby): latka Mission.SpawnAgent wpieta (prefiks High) - ludzie band dostaja na tarcze herby prawdziwych rodow (dezerter - pan ziemi pochodzenia albo rod/krolestwo jego kultury, reszta - nakradzione z najblizszych wsi i warowni); klucz Band Shields Stolen Arms " + (On ? "TAK" : "NIE") + ".");
                }
            }
            catch (Exception e) { Log.Error("BandShields.ApplyAll", e); }
            // herb pochodzenia agenta (zbroje i tuniki z herbem, znaczniki oddzialow, pociski): postfiks gettera Banner na klasach deklarujacych.
            // Oba gettery to metody z wywolaniami (nie 1-bajtowy ret - bez ryzyka UNWIND_INFO z 16:09); wolane tylko przez interfejs IAgentOriginBase
            // (sprawdzone w metadanych wszystkich DLL gry i modow - zadnego wywolania wprost, wiec JIT ich nie wkleja).
            int ok = PatchGetter(h, typeof(PartyGroupAgentOrigin)) + PatchGetter(h, typeof(PartyAgentOrigin));
            Log.Info("Tarcze band (herby): herb pochodzenia ludzi band (zbroje i tuniki z herbem, znaczniki oddzialow, pociski) - postfiks Banner wpiety " + ok + "/2 (PartyGroupAgentOrigin, PartyAgentOrigin).");
        }

        private static int PatchGetter(Harmony h, Type t)
        {
            try
            {
                var g = AccessTools.DeclaredPropertyGetter(t, "Banner");
                if (g == null) { Log.Info("Tarcze band (herby): brak " + t.Name + ".Banner - herb pochodzenia bez zmian."); return 0; }
                h.Patch(g, postfix: new HarmonyMethod(typeof(BandShields), nameof(OriginBannerPostfix)));
                return 1;
            }
            catch (Exception e) { Log.Error("BandShields.PatchGetter(" + t.Name + ")", e); return 0; }
        }

        // ---------------------------------------------------------------- postfiks herbu pochodzenia (kazdy watek; liczy tylko watek glowny)
        private static void OriginBannerPostfix(object __instance, ref Banner __result)
        {
            if (__result == null) return;
            try
            {
                if (!On) return;
                var o = __instance as IAgentOriginBase;
                if (o == null) return;
                var b = Decide(o, null, 0);
                if (b != null && !ReferenceEquals(b, __result)) { __result = b; Interlocked.Increment(ref _originHits); }
            }
            catch (Exception e) { Stumble("OriginBanner", e); }
        }

        // ---------------------------------------------------------------- prefiks spawnu
        private static void Prefix(Mission __instance, AgentBuildData agentBuildData)
        {
            if (agentBuildData == null) return;
            try
            {
                if (!On || Campaign.Current == null) return;
                var o = agentBuildData.AgentOrigin;
                if (o == null) return;
                // SpawnTroop podal juz herb z gettera pochodzenia (ten sam wybor z pamieci); tu dla agentow budowanych bez SpawnTroop
                var b = Decide(o, __instance, agentBuildData.AgentEquipmentSeed);
                if (b == null) return;
                agentBuildData.Banner(b);
                // gotowa bron innego moda: tarcze w niej maja herb, z jakim ja zbudowano (NavalDLC/ROT biora go z pochodzenia - czyli nasz); licznik
                if (agentBuildData.AgentOverridenSpawnMissionEquipment != null) _ready++;
            }
            catch (Exception e) { Stumble("Prefix", e); }
        }

        /// <summary>Herb dla czlowieka bandy - raz na pochodzenie agenta w misji, potem z pamieci (kazdy watek); null - bez zmiany.
        /// Nowy wybor liczy tylko watek glowny (spawn, widoki misji); inny watek dostaje wylacznie to, co juz wybrane.</summary>
        private static Banner Decide(IAgentOriginBase o, Mission m, int seedHint)
        {
            var pb = o.BattleCombatant as PartyBase;
            if (pb == null) return null;
            var f = pb.MapFaction;
            if (f == null || !f.IsBanditFaction) return null;
            Banner b;
            lock (_gate) { if (_chosen.TryGetValue(o, out b)) return b; }
            if (Environment.CurrentManagedThreadId != _main || Campaign.Current == null) return null;
            if (m == null) m = Mission.Current;
            if (m == null || m.MissionEnded) return null;     // koniec misji (pamiec juz wyczyszczona) - nic nowego, nic nie trzymamy do nastepnej
            var ch = o.Troop as CharacterObject;
            if (ch == null || ch.IsPlayerCharacter) return null;
            Begin(m);
            b = Choose(o, pb, f, ch, seedHint);
            lock (_gate) _chosen[o] = b;
            return b;
        }

        private static Banner Choose(IAgentOriginBase o, PartyBase pb, IFaction f, CharacterObject ch, int seedHint)
        {
            if (Undead.Character(ch) || (pb.MobileParty != null && Undead.Party(pb.MobileParty))) { _skipUndead++; return null; }
            var pa = ArmsOf(pb, f);
            if (pa == null) return null;
            _men++;
            if (ch.IsHero) _heroes++;
            int seed = o.UniqueSeed;
            if (seed == 0) seed = seedHint;
            if (seed == 0) { _seedless++; seed = unchecked(_seedless * 7919 + (int)Fnv(ch.StringId ?? "")); }   // posilki DR, kryjowka: bez ziarna rosteru
            uint hsh = Mix(unchecked((uint)seed) ^ pa.Salt);
            int kind;
            Pick p = ChooseList(pa, ch, f, out kind);
            int i = p != null ? p.Draw(hsh) : -1;
            if (i < 0) { _none++; return null; }
            var b = p.B[i];
            _kind[kind]++;
            _codes.Add(b.BannerCode);
            int n; _byHouse.TryGetValue(p.L[i], out n); _byHouse[p.L[i]] = n + 1;
            return b;
        }

        /// <summary>Nowa misja - pamiec od nowa (obiekty innej misji nie zostaja) i herby strony gracza w tej bitwie.</summary>
        private static void Begin(Mission m)
        {
            var cur = _mission != null ? _mission.Target as Mission : null;
            if (ReferenceEquals(cur, m)) return;
            Clear();
            _mission = new WeakReference(m);
            try { var pc = Clan.PlayerClan; if (pc != null) AddFriend(pc.Banner); } catch { }
            try
            {
                var me = MapEvent.PlayerMapEvent;
                if (me != null && me.PlayerSide != BattleSideEnum.None)
                {
                    var side = me.PlayerSide == BattleSideEnum.Attacker ? me.AttackerSide : me.DefenderSide;
                    if (side != null && side.Parties != null)
                        foreach (var mep in side.Parties)
                        {
                            var p = mep != null ? mep.Party : null;
                            if (p == null) continue;
                            _friendParties++;
                            var lh = p.LeaderHero;
                            if (lh != null) { AddFriend(lh.ClanBanner); if (lh.Clan != null) AddFriend(lh.Clan.Banner); }
                            var ow = p.Owner;
                            if (ow != null && ow.Clan != null) AddFriend(ow.Clan.Banner);
                            var mf = p.MapFaction;
                            if (mf != null && !mf.IsBanditFaction) AddFriend(mf.Banner);
                        }
                }
            }
            catch (Exception e) { Stumble("Begin(strona gracza)", e); }
        }

        private static void AddFriend(Banner b)
        {
            if (b == null) return;
            string code = b.BannerCode;
            if (!string.IsNullOrEmpty(code)) _friendCodes.Add(code);
        }

        private static void Clear()
        {
            _mission = null;
            _arms.Clear(); _realm.Clear(); _codes.Clear(); _byHouse.Clear(); _friendCodes.Clear();
            lock (_gate) _chosen.Clear();
            Array.Clear(_kind, 0, _kind.Length);
            _men = _heroes = _none = _ready = _skipUndead = _stMission = _seedless = _friendParties = 0;
            Interlocked.Exchange(ref _originHits, 0);
            _loggedMen = -1;
        }

        private static PartyArms ArmsOf(PartyBase pb, IFaction f)
        {
            PartyArms pa;
            if (_arms.TryGetValue(pb, out pa)) return pa;
            pa = new PartyArms { Party = pb };
            Vec2 pos = Vec2.Zero; bool has = false;
            try
            {
                if (pb.MobileParty != null) { pos = pb.MobileParty.Position.ToVec2(); has = true; }
                else if (pb.Settlement != null) { pos = pb.Settlement.GetPosition2D; has = true; }
                if (!has && MobileParty.MainParty != null) { pos = MobileParty.MainParty.Position.ToVec2(); has = true; }
            }
            catch (Exception e) { Stumble("ArmsOf(polozenie)", e); }
            pa.Pos = pos;
            string id = null;
            try { id = pb.MobileParty != null ? pb.MobileParty.StringId : (pb.Settlement != null ? pb.Settlement.StringId : null); } catch { }
            pa.Salt = Fnv(id ?? "");
            try { pa.OwnBanner = f.Banner != null ? f.Banner.BannerCode : null; } catch { pa.OwnBanner = null; }
            _arms[pb] = pa;
            return pa;
        }

        // ---------------------------------------------------------------- wybor listy wedlug czlowieka
        private static Pick ChooseList(PartyArms pa, CharacterObject ch, IFaction band, out int kind)
        {
            var cu = ch.Culture;
            bool outlaw = cu == null || cu.IsBandit || (band.Culture != null && ReferenceEquals(cu, band.Culture));
            if (!outlaw)
            {
                string ck = cu.StringId ?? "";
                var p = OriginList(pa, cu, ck);
                if (p != null && p.B.Count > 0) { kind = KOrigin; return p; }
                p = NearList(pa, cu, ck);
                if (p != null && p.B.Count > 0) { kind = KNear; return p; }
                p = RealmList(cu, ck);
                if (p != null && p.B.Count > 0) { kind = KRealm; return p; }
            }
            kind = KStolen;
            return StolenList(pa);
        }

        /// <summary>Rod gotowy na tarcze bandyty: zyje, nie bandycki, nie Inni, nie Twoj, ma herb inny niz herb bandy i herby strony gracza w tej bitwie.</summary>
        private static bool ClanOk(Clan c, PartyArms pa)
        {
            if (c == null || c.IsEliminated || c.IsBanditFaction || ReferenceEquals(c, Clan.PlayerClan)) return false;
            if (OthersRise.IsWhiteWalkerClan(c) || c.StringId == "ROTclan_126") return false;
            return BannerOk(c.Banner, pa);
        }

        private static bool BannerOk(Banner b, PartyArms pa)
        {
            if (b == null) return false;
            string code = b.BannerCode;
            if (string.IsNullOrEmpty(code)) return false;
            if (_friendCodes.Contains(code)) return false;     // Twoj rod, sojusznicy w tej bitwie i ich krolestwa
            if (pa != null && pa.OwnBanner != null && string.Equals(code, pa.OwnBanner, StringComparison.Ordinal)) return false;
            return true;
        }

        private static bool CultureOf(Settlement s, Clan owner, CultureObject cu)
        {
            if (cu == null) return false;
            if (s != null && ReferenceEquals(s.Culture, cu)) return true;
            if (owner != null && ReferenceEquals(owner.Culture, cu)) return true;
            var k = owner != null ? owner.Kingdom : null;
            return k != null && ReferenceEquals(k.Culture, cu);
        }

        private static string NameOf(Clan c) { try { return c != null && c.Name != null ? c.Name.ToString() : "?"; } catch { return "?"; } }

        /// <summary>(1) Pan ziemi pochodzenia: regiony skladu bandy z ksiegi ludzi, tylko jego kultury, wagi ludzi.</summary>
        private static Pick OriginList(PartyArms pa, CultureObject cu, string ck)
        {
            Pick p;
            if (pa.Origin.TryGetValue(ck, out p)) return p;
            p = new Pick();
            try
            {
                if (pa.Regions == null)
                {
                    pa.Regions = new List<KeyValuePair<Settlement, double>>();
                    var raw = new List<KeyValuePair<string, double>>();
                    if (PeopleBook.BandOrigin(pa.Party, raw))
                        foreach (var kv in raw)
                        {
                            var s = Settlement.Find(kv.Key);
                            if (s != null) pa.Regions.Add(new KeyValuePair<Settlement, double>(s, kv.Value));
                        }
                }
                foreach (var kv in pa.Regions)
                {
                    var owner = kv.Key.OwnerClan;
                    if (!ClanOk(owner, pa) || !CultureOf(kv.Key, owner, cu)) continue;
                    p.Add(owner.Banner, (float)kv.Value, NameOf(owner));
                }
                p.Trim(MaxOrigin);
            }
            catch (Exception e) { Stumble("OriginList", e); }
            pa.Origin[ck] = p;
            return p;
        }

        /// <summary>Warownie (miasta i zamki) od najblizszej bandy - raz na bande.</summary>
        private static List<Settlement> FortsOf(PartyArms pa)
        {
            if (pa.Forts != null) return pa.Forts;
            var tmp = new List<KeyValuePair<float, Settlement>>(256);
            foreach (var s in Settlement.All)
            {
                if (s == null || !(s.IsTown || s.IsCastle)) continue;
                tmp.Add(new KeyValuePair<float, Settlement>(pa.Pos.DistanceSquared(s.GetPosition2D), s));
            }
            tmp.Sort((a, b) => a.Key.CompareTo(b.Key));
            pa.Forts = new List<Settlement>(tmp.Count);
            foreach (var kv in tmp) pa.Forts.Add(kv.Value);
            return pa.Forts;
        }

        /// <summary>(2) 3 najblizsze rody jego kultury (panowie warowni od najblizszej), wagi 3/2/1.</summary>
        private static Pick NearList(PartyArms pa, CultureObject cu, string ck)
        {
            Pick p;
            if (pa.Near.TryGetValue(ck, out p)) return p;
            p = new Pick();
            try
            {
                var forts = FortsOf(pa);
                int scanned = 0;
                for (int i = 0; i < forts.Count && p.B.Count < NearHouses && scanned < MaxFortScan; i++, scanned++)
                {
                    var s = forts[i];
                    var owner = s.OwnerClan;
                    if (!ClanOk(owner, pa) || !CultureOf(s, owner, cu)) continue;
                    p.Add(owner.Banner, NearHouses - p.B.Count, NameOf(owner));
                }
            }
            catch (Exception e) { Stumble("NearList", e); }
            pa.Near[ck] = p;
            return p;
        }

        /// <summary>(3) Krolestwo jego kultury (herb krolestwa = rod panujacy), rowno; bez krolestwa gracza-krola.</summary>
        private static Pick RealmList(CultureObject cu, string ck)
        {
            Pick p;
            if (_realm.TryGetValue(ck, out p)) return p;
            p = new Pick();
            try
            {
                foreach (var k in Kingdom.All)
                {
                    if (k == null || k.IsEliminated || !ReferenceEquals(k.Culture, cu)) continue;
                    if (k.RulingClan != null && ReferenceEquals(k.RulingClan, Clan.PlayerClan)) continue;
                    if (!BannerOk(k.Banner, null)) continue;
                    string name; try { name = k.Name != null ? k.Name.ToString() : "?"; } catch { name = "?"; }
                    p.Add(k.Banner, 1f, name);
                }
            }
            catch (Exception e) { Stumble("RealmList", e); }
            _realm[ck] = p;
            return p;
        }

        /// <summary>(4) Nakradzione: gospodarze 3 najblizszych wsi gry (z wioskami ukladu) i panowie 3 najblizszych warowni, rowno. Kolejnosc po kolei
        /// (gospodarz kazdej z 3 wsi gry, panowie warowni, potem wioski ukladu na zmiane miedzy okregami), najwyzej MaxStolen roznych herbow na bande -
        /// kazdy herb to tekstura tarczy w misji (duza bitwa lordow ma ich podobnie wiele).</summary>
        private static Pick StolenList(PartyArms pa)
        {
            if (pa.Stolen != null) return pa.Stolen;
            var p = new Pick();
            try
            {
                // wsie gry od najblizszej (czesciowy wybor - tylko 3)
                var near = new List<KeyValuePair<float, Settlement>>(NearVillages + 1);
                foreach (var s in Settlement.All)
                {
                    if (s == null || !s.IsVillage || s.Village == null) continue;
                    float d = pa.Pos.DistanceSquared(s.GetPosition2D);
                    if (near.Count == NearVillages && d >= near[near.Count - 1].Key) continue;
                    int at = near.Count;
                    while (at > 0 && near[at - 1].Key > d) at--;
                    near.Insert(at, new KeyValuePair<float, Settlement>(d, s));
                    if (near.Count > NearVillages) near.RemoveAt(near.Count - 1);
                }
                bool holders = VillageHolders.On;
                var uids = new List<string>();
                var sides = new List<List<VillageHolders.Holder>>(near.Count);
                // runda 1: gospodarz kazdej wsi gry (klucz Village Banner Per Holder off - pan wsi)
                foreach (var kv in near)
                {
                    var v = kv.Value;
                    var side = new List<VillageHolders.Holder>();
                    sides.Add(side);
                    if (holders)
                    {
                        AddHolder(p, VillageHolders.Of(v, null), pa);
                        uids.Clear();
                        if (VillageBook.DistrictUids(v.StringId, uids))
                            foreach (var u in uids) { var h = VillageHolders.Of(v, u); if (h != null) side.Add(h); }
                    }
                    else
                    {
                        var lord = v.Village.Bound != null ? v.Village.Bound.OwnerClan : v.OwnerClan;
                        if (ClanOk(lord, pa)) p.Add(lord.Banner, 1f, NameOf(lord));
                    }
                }
                // runda 2: panowie warowni od najblizszej - 3 warownie, a gdy wciaz malo (same Twoje ziemie) - dalej, az do 3 herbow
                var forts = FortsOf(pa);
                for (int i = 0; i < forts.Count && i < MaxFortScan && p.B.Count < MaxStolen; i++)
                {
                    if (i >= NearForts && p.B.Count >= NearHouses) break;
                    var owner = forts[i].OwnerClan;
                    if (!ClanOk(owner, pa)) continue;
                    p.Add(owner.Banner, 1f, NameOf(owner));
                }
                // runda 3: wioski ukladu (rycerze i rody kanonu) na zmiane miedzy okregami, do MaxStolen herbow
                for (int k = 0; p.B.Count < MaxStolen; k++)
                {
                    bool more = false;
                    foreach (var side in sides)
                    {
                        if (k >= side.Count) continue;
                        more = true;
                        if (p.B.Count >= MaxStolen) break;
                        AddHolder(p, side[k], pa);
                    }
                    if (!more) break;
                }
            }
            catch (Exception e) { Stumble("StolenList", e); }
            pa.Stolen = p;
            return p;
        }

        private static void AddHolder(Pick p, VillageHolders.Holder h, PartyArms pa)
        {
            if (h == null || h.Banner == null) return;
            if (h.Clan != null && !ClanOk(h.Clan, pa)) return;
            if (h.Clan == null && !BannerOk(h.Banner, pa)) return;
            string label = h.Clan != null ? NameOf(h.Clan) : (h.Canon != null && !string.IsNullOrEmpty(h.Canon.Name) ? h.Canon.Name : "knight of the lore");
            p.Add(h.Banner, 1f, label);
        }

        // ---------------------------------------------------------------- liczby
        private static uint Mix(uint x)
        {
            unchecked
            {
                x ^= x >> 16; x *= 0x7feb352dU; x ^= x >> 15; x *= 0x846ca68bU; x ^= x >> 16;
                return x;
            }
        }

        private static uint Fnv(string s)
        {
            unchecked
            {
                uint h = 2166136261U;
                for (int i = 0; i < s.Length; i++) { h ^= s[i]; h *= 16777619U; }
                return h;
            }
        }

        // ---------------------------------------------------------------- linia logu
        /// <summary>Linia po rozstawieniu (end = false) i na koniec misji (end = true; tylko gdy doszli ludzie albo nie bylo linii rozstawienia).</summary>
        internal static void Flush(Mission m, bool end)
        {
            try
            {
                var cur = _mission != null ? _mission.Target as Mission : null;
                if (cur == null || !ReferenceEquals(cur, m))
                {
                    if (end && cur == null) Clear();
                    return;
                }
                bool any = _men > 0 || _skipUndead > 0 || _ready > 0 || _stMission > 0;
                if (any && (!end || _loggedMen != _men))
                {
                    var sb = new StringBuilder(400);
                    sb.Append("Tarcze band (herby): ").Append(end ? "koniec misji" : "rozstawienie");
                    string scene = null; try { scene = m.SceneName; } catch { }
                    bool field = false; try { field = m.IsFieldBattle; } catch { }
                    sb.Append(" | misja ").Append(scene ?? "?").Append(" (bitwa w polu ").Append(field ? "TAK" : "NIE").Append(')');
                    sb.Append(" | ludzi band ").Append(_men).Append(" (bohaterow ").Append(_heroes).Append(") z ").Append(_arms.Count).Append(" band");
                    sb.Append(" | roznych herbow ").Append(_codes.Count);
                    sb.Append(" | dezerterzy i rozbitkowie: pan ziemi pochodzenia (ksiega ludzi) ").Append(_kind[KOrigin])
                      .Append(", najblizszy rod ich kultury ").Append(_kind[KNear]).Append(", krolestwo ich kultury ").Append(_kind[KRealm]);
                    sb.Append(" | nakradzione (gospodarze 3 najblizszych wsi i panowie 3 najblizszych warowni) ").Append(_kind[KStolen]);
                    sb.Append(" | bez herbu do wyboru ").Append(_none).Append(" (herb bandy zostal)");
                    sb.Append(" | herb pochodzenia (tuniki z herbem, znaczniki, pociski) podmieniony ").Append(Volatile.Read(ref _originHits)).Append(" razy");
                    sb.Append(" | wykluczone herby strony gracza ").Append(_friendCodes.Count).Append(" (partii ").Append(_friendParties).Append(')');
                    sb.Append(" | z gotowa bronia innego moda ").Append(_ready).Append(" | pominieci Inni ").Append(_skipUndead);
                    if (_byHouse.Count > 0)
                    {
                        var top = new List<KeyValuePair<string, int>>(_byHouse);
                        top.Sort((a, b) => b.Value != a.Value ? b.Value.CompareTo(a.Value) : string.CompareOrdinal(a.Key, b.Key));
                        sb.Append(" | najczestsze: ");
                        for (int i = 0; i < top.Count && i < 6; i++) { if (i > 0) sb.Append(", "); sb.Append(top[i].Key).Append(' ').Append(top[i].Value); }
                    }
                    sb.Append(" | potkniecia ").Append(_stMission).Append(" (sesja ").Append(_stSession).Append(").");
                    Log.Info(sb.ToString());
                    _loggedMen = _men;
                }
                if (end) Clear();
            }
            catch (Exception e) { Stumble("Flush", e); if (end) Clear(); }
        }
    }

    /// <summary>n20-herby: zachowanie misji kampanii przy kluczu BandShieldsStolenArms - tylko linia logu (rozstawienie i koniec misji) i czyszczenie pamieci.</summary>
    internal sealed class BandShieldsWatch : MissionBehavior
    {
        public override MissionBehaviorType BehaviorType { get { return MissionBehaviorType.Other; } }

        public override void OnDeploymentFinished()
        {
            try { BandShields.Flush(Mission, false); } catch { }
            base.OnDeploymentFinished();
        }

        protected override void OnEndMission()
        {
            try { BandShields.Flush(Mission, true); } catch { }
            base.OnEndMission();
        }
    }

    internal static partial class PeopleBook
    {
        /// <summary>n20-herby (BandShields): regiony pochodzenia skladu partii (konto SLUZBA, E4/7) - tylko odczyt, bez "" (bez pochodzenia).
        /// false - brak slownika, ksiega nieczynna albo blad. Wolane z watku misji (kampania stoi).</summary>
        internal static bool BandOrigin(PartyBase pb, List<KeyValuePair<string, double>> into)
        {
            if (pb == null || into == null) return false;
            try
            {
                if (!OriginLive) return false;
                Org o;
                if (!_mem.TryGetValue(pb, out o) || o == null) return false;
                foreach (var kv in o.M)
                    if (!string.IsNullOrEmpty(kv.Key) && kv.Value > Eps) into.Add(new KeyValuePair<string, double>(kv.Key, kv.Value));
                return into.Count > 0;
            }
            catch { return false; }
        }
    }
}
