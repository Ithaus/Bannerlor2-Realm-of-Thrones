using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>
    /// I1 - PRAWO JENCA WEDLUG KRAINY (decyzja Jeffa 09.10 pkt I: "jency w Westeros: do domu, przestepcy na Mur"; audyt 2026-10-09
    /// E6, S2, raport 10 Z2 / L2, raport 04 N5 - Yoren zabiera ludzi z lochow).
    ///
    /// Dzis (BK, sprawdzone w dekompilacji):
    ///  - BK prefiks na obu wersjach SellPrisonersAction -> SettlementPatches.ApplyAllPrisionersPatch.SendOffPrisoners: polityka karna
    ///    osady "Enslavement" (domyslna w KAZDEJ osadzie, PolicyManager.GeneratePolicy; AI jej nie zmienia) = wszyscy szeregowi do
    ///    ludnosci BK osady jako niewolnicy - takze w Westeros. Obejmuje: partie AI wchodzace do przyjaznej warowni, codzienna
    ///    sprzedaz 10% lochu warowni, rozwiazanie partii, powrot patrolu gry. Sprzedaz gracza (posrednik, ekran) - BK nie zna osady
    ///    (sprzedajacy MainParty, kupujacy null), wiec ludzie znikaja.
    ///  - BKPartyBehavior.AddPatrolBehavior: patrol BK z garnizonu ("Patrol from X") wraca do domu - jency zawsze niewolnikami osady.
    ///  - Wypuszczenie przez gracza (ekran oddzialu, EndCaptivityAction.ApplyByReleasedByChoice) - szeregowi znikaja.
    ///  - Ekran Aftermath po bitwie gracza: jency i uwolnieni, ktorych gracz nie wzial, znikaja (PlayerEncounter czysci listy).
    ///  - BK przy Enslavement dopisuje Number + WoundedNumber - ranni (juz w Number) drugi raz, z niczego.
    ///
    /// Teraz, w krainie BEZ niewoli (kultura osady / regionu spoza listy LosersFlee.SlaveCulture - jedna lista z H3):
    ///  - Westeros bez Zelaznych Wysp: przestepca (zawod Bandit albo kultura bandycka - bandyci, wyrzutki z band) idzie na Mur:
    ///    ludnosc BK najblizszej warowni Nocnej Strazy w rekach Strazy (LosersFlee.ToWall; Straz werbuje stamtad jak z kazdej osady);
    ///    Muru nie ma - do domu jak reszta;
    ///  - reszta (i wszyscy w innych krainach bez niewoli: Braavos, Pentos, Lorath, Qarth, Wolni Ludzie, Ib, Sarnor, Wyspy Letnie,
    ///    Yi Ti) wraca do domu jedna funkcja pochodzenia H3 (LosersFlee.SendHome, zrodlo SrcLaw): dom = najblizsza miejscu osada
    ///    kultury jenca; zolnierz -> ludnosc BK tej osady, prosty czlowiek -> hearth jej regionu, straz karawan "z szablonu";
    ///    czego dom nie przyjal - do puli wyrzutkow regionu miejsca (prawo wyrzutkow wylaczone - licznik "znikneli").
    /// Ekran Aftermath (prefiks na PlayerEncounter.OnPlayerLootMembersAndPrisonerEnd): zostawieni jency jak wypuszczeni przez gracza,
    /// zostawieni ludzie (uwolnieni jency pokonanych) - wolni, do domu.
    /// Gracz wybral w swojej osadzie polityke "Execution" - BK jak dotad (straceni, licznik). W krainie z niewola bez zmian (licznik K),
    /// tylko niewolnikow ze sprzedazy (Enslavement) dopisujemy sami, kazdego raz (LosersFlee.AddSlaves). NIE ruszamy: polityki karnej BK (lojalnosc, bezpieczenstwo, cena niewolnika), zlota za jencow (164b), rabunku BK
    /// (BKRaidCaptureBehavior), jencow bohaterow. Zapisu nie ma (liczniki dnia + suma sesji).
    /// </summary>
    internal static class PrisonerLaw
    {
        internal static bool On { get { var s = Settings.Current; return s != null && s.PrisonerLawEnabled; } }

        // Westeros bez Zelaznych Wysp (sturgia - thralle, na liscie niewoli H3); kultury osad ROT wedlug ludzie-regiony.csv (08.10)
        private static readonly HashSet<string> WesterosIds = new HashSet<string>
            { "battania", "river", "reach", "aserai", "vlandia", "vale", "stormlands", "crownlands", "dragonstone", "nightswatch", "skagosi" };

        private const int LandNone = -1, LandSlave = 0, LandWesteros = 1, LandFree = 2;
        // zrodla: sprzedaz w warowni (partie AI, loch osady, rozwiazanie, patrol gry), sprzedaz gracza, patrol BK, wypuszczeni przez gracza,
        // jency zostawieni po bitwie gracza (ekran Aftermath: nie wzieci i wypuszczeni tam), uwolnieni jency pokonanych, ktorych gracz nie wzial
        private const int SSale = 0, SPlayerSale = 1, SPatrolBk = 2, SRelease = 3, SLoot = 4, SLootFreed = 5, Ss = 6;
        private static readonly string[] SName = { "sprzedaz w warowni (partie AI, lochy)", "sprzedaz gracza", "patrol BK", "wypuszczeni przez gracza",
                                                   "jency zostawieni po bitwie gracza", "uwolnieni po bitwie gracza (nie wzieci)" };

        // liczniki doby
        private static readonly int[] _in = new int[Ss];
        private static int _westeros, _freeOther, _home, _homeBk, _homeVillage, _homeTpl, _wall, _wallMissing, _pool, _vanished, _undead, _executed;
        private static int _slaves, _slavesPatrol, _slaveOther, _slavePlayer, _slaveReleased, _stumbles;
        // uwaga przegladu: ranni sprzedani w niewole policzeni raz (BK dopisywal ich drugi raz z niczego); BK po staremu (nie dalo sie dopisac samemu)
        private static int _woundedOnce, _slavesBkOld, _slavesBkOldWounded;
        // bitwa gracza: wypuszczeni na ekranie Aftermath (OnPrisonerReleasedEvent pominiety - ci sami ludzie sa w lewej liscie, liczy LootEndPrefix)
        private static int _lootRelSkipped;
        private static readonly Dictionary<string, int> _wallBy = new Dictionary<string, int>();
        private static readonly Dictionary<string, int> _slavesBy = new Dictionary<string, int>();
        // suma sesji
        private static long _totHome, _totWall, _totSlaves;
        private static long _ticks;                                              // czas Route (doba)
        private static bool _err, _salePatched, _patrolPatched, _lootPatched;
        // PartyScreenLogic.PartyScreenClosedEvent (pole zdarzenia) - rozpoznanie ekranu Aftermath bitwy gracza
        private static FieldInfo _closedEvt;
        private const string LootEndName = "OnPlayerLootMembersAndPrisonerEnd";

        // polityka karna BK (refleksja; BK tworzy nowy PolicyManager przy kazdej grze - Reset)
        private static bool _polTried;
        private static object _polMgr;
        private static MethodInfo _getPol;
        private static PropertyInfo _polProp;

        internal static void Reset()
        {
            ClearDay();
            _totHome = _totWall = _totSlaves = 0;
            _err = false;
            _polTried = false; _polMgr = null; _getPol = null; _polProp = null;
        }

        private static void ClearDay()
        {
            Array.Clear(_in, 0, Ss);
            _westeros = _freeOther = _home = _homeBk = _homeVillage = _homeTpl = _wall = _wallMissing = _pool = _vanished = _undead = _executed = 0;
            _slaves = _slavesPatrol = _slaveOther = _slavePlayer = _slaveReleased = _stumbles = 0;
            _woundedOnce = _slavesBkOld = _slavesBkOldWounded = _lootRelSkipped = 0;
            _wallBy.Clear(); _slavesBy.Clear();
            _ticks = 0;
        }

        // ------------------------------------------------------------ kraina
        private static int Land(Settlement st)
        {
            var c = st != null ? st.Culture : null;
            if (c == null || c.StringId == null) return LandNone;
            if (LosersFlee.SlaveCulture(c)) return LandSlave;
            return WesterosIds.Contains(c.StringId) ? LandWesteros : LandFree;
        }

        private static bool Criminal(CharacterObject t)
        {
            return t.Occupation == Occupation.Bandit || (t.Culture != null && t.Culture.IsBandit);
        }

        /// <summary>Nazwa polityki karnej BK osady ("Enslavement" / "Forgiveness" / "Execution"); null - nie da sie odczytac.</summary>
        private static string Policy(Settlement st)
        {
            try
            {
                if (!_polTried)
                {
                    _polTried = true;
                    var cfgT = AccessTools.TypeByName("BannerKings.BannerKingsConfig");
                    var cfg = cfgT != null ? AccessTools.Property(cfgT, "Instance")?.GetValue(null, null) : null;
                    _polMgr = cfg != null ? AccessTools.Property(cfgT, "PolicyManager")?.GetValue(cfg, null) : null;
                    _getPol = _polMgr != null ? AccessTools.Method(_polMgr.GetType(), "GetPolicy", new[] { typeof(Settlement), typeof(string) }) : null;
                }
                if (_getPol == null || st == null) return null;
                var pol = _getPol.Invoke(_polMgr, new object[] { st, "criminal" });
                if (pol == null) return null;
                if (_polProp == null || _polProp.DeclaringType != pol.GetType()) _polProp = AccessTools.Property(pol.GetType(), "Policy");
                var v = _polProp != null ? _polProp.GetValue(pol, null) : null;
                return v != null ? v.ToString() : null;
            }
            catch { return null; }
        }

        private static int Regulars(TroopRoster r) { int w; return Regulars(r, out w); }

        /// <summary>Szeregowi listy (Number - juz z rannymi); wounded - ilu z nich rannych (BK GetRosterCount dodaje ich drugi raz).</summary>
        private static int Regulars(TroopRoster r, out int wounded)
        {
            int n = 0; wounded = 0;
            for (int i = 0; i < r.Count; i++)
            {
                var e = r.GetElementCopyAtIndex(i);
                if (e.Character != null && !e.Character.IsHero && e.Number > 0) { n += e.Number; wounded += e.WoundedNumber; }
            }
            return n;
        }

        private static void AddBy(Dictionary<string, int> d, string key, int n)
        {
            if (n <= 0) return;
            if (string.IsNullOrEmpty(key)) key = "?";
            int v; d.TryGetValue(key, out v); d[key] = v + n;
        }

        // ------------------------------------------------------------ los jednego typu jencow w krainie bez niewoli
        private static void Route(CharacterObject troop, int n, Vec2 pos, bool westeros, int src)
        {
            if (troop == null || troop.IsHero || n <= 0) return;
            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            try
            {
                _in[src] += n;
                if (westeros) _westeros += n; else _freeOther += n;
                if (Undead.Character(troop)) { _undead += n; return; }          // wight - z niczego, do niczego (jak H3)
                // bandyta przy WYLACZONYM prawie wyrzutkow jest "z szablonu" gry (H3 SendHome pkt 2a) - nie dopisujemy go na Murze z niczego
                if (westeros && Criminal(troop) && (OutlawLaw.On || troop.Occupation != Occupation.Bandit))
                {
                    Settlement wall;
                    int w = LosersFlee.ToWall(troop, n, pos, out wall);
                    if (w > 0)
                    {
                        _wall += w; _totWall += w;
                        AddBy(_wallBy, wall != null && wall.Name != null ? wall.Name.ToString() : "?", w);
                    }
                    n -= w;
                    if (n <= 0) return;
                    _wallMissing += n;                                           // Muru nie ma / BK nie przyjal - do domu jak reszta
                }
                int cat;
                int got = LosersFlee.SendHome(null, troop, n, pos, null, LosersFlee.SrcLaw, out cat);
                if (got > 0)
                {
                    _home += got; _totHome += got;
                    if (cat == LosersFlee.CatBk) _homeBk += got; else if (cat == LosersFlee.CatTemplate) _homeTpl += got; else _homeVillage += got;
                }
                int left = n - got;
                if (left > 0)
                {
                    var region = OutlawLaw.PoolRegionAt(pos);
                    if (OutlawLaw.On && region != null) { OutlawLaw.PoolAdd(region, troop, left); _pool += left; }
                    else _vanished += left;
                }
            }
            catch (Exception e)
            {
                _stumbles++;
                if (!_err) { _err = true; Log.Error("PrisonerLaw.Route", e); }
            }
            finally { _ticks += System.Diagnostics.Stopwatch.GetTimestamp() - t0; }
        }

        // ------------------------------------------------------------ latki
        /// <summary>
        /// Prefiks na BK SendOffPrisoners(prisoners, currentSettlement) - wolanym z prefiksow BK na SellPrisonersAction, PRZED zdjeciem
        /// jencow z listy sprzedajacego. Kraina bez niewoli: ludzie do domu / na Mur i BK nie dopisuje niewolnikow (false).
        /// </summary>
        public static bool SendOffPrefix(TroopRoster __0, Settlement __1)
        {
            if (!On || __0 == null) return true;
            Settlement st; bool player = false, done = false; int land; string pol;
            try
            {
                st = __1;
                if (st == null)
                {
                    // BK nie zna osady tylko przy sprzedazy gracza (posrednik w karczmie, ekran sprzedazy: MainParty, kupujacy null)
                    var mp = MobileParty.MainParty;
                    st = mp != null ? mp.CurrentSettlement : null;
                    if (st == null) return true;
                    player = true;
                }
                land = Land(st);
                if (land == LandNone) return true;
                if (land == LandSlave)
                {
                    int w;
                    int n = Regulars(__0, out w);
                    if (player) { _slavePlayer += n; return true; }                 // BK bez osady - znikaja jak dotad
                    pol = Policy(st);
                    if (pol != "Enslavement") { _slaveOther += n; return true; }    // osada gracza z inna polityka / nieodczytana - BK jak dotad
                    // niewolnicy dopisani przez nas, kazdy czlowiek raz (BK: Number + WoundedNumber - ranni drugi raz z niczego)
                    int got = LosersFlee.AddSlaves(st, n);
                    if (got < 0)
                    {
                        _slaves += n + w; _totSlaves += n + w; _slavesBkOld += n + w; _slavesBkOldWounded += w;   // BK po staremu - liczymy jak BK
                        AddBy(_slavesBy, st.Culture.StringId, n + w);
                        return true;
                    }
                    done = true;                                                    // dopisani - BK nie moze drugi raz, nawet po bledzie nizej
                    _slaves += got; _totSlaves += got; _woundedOnce += w;
                    AddBy(_slavesBy, st.Culture.StringId, got);
                    return false;
                }
                if (!player && Policy(st) == "Execution") { _executed += Regulars(__0); return true; }   // wybor gracza w jego osadzie
            }
            catch (Exception e)
            {
                _stumbles++;
                if (!_err) { _err = true; Log.Error("PrisonerLaw.SendOffPrefix", e); }
                return !done;                                                       // nic jeszcze nie zrobione - BK jak dotad
            }
            // od tej chwili ludzie juz ida do domu - BK nie moze ich dopisac drugi raz (Route ma wlasny try na kazdy typ)
            var pos = st.GetPosition2D;
            bool wes = land == LandWesteros;
            int src = player ? SPlayerSale : SSale;
            for (int i = 0; i < __0.Count; i++)
            {
                TroopRosterElement e;
                try { e = __0.GetElementCopyAtIndex(i); } catch { _stumbles++; continue; }
                if (e.Character == null || e.Character.IsHero || e.Number <= 0) continue;
                Route(e.Character, e.Number, pos, wes, src);
            }
            return false;
        }

        /// <summary>
        /// Prefiks na BK BKPartyBehavior.AddPatrolBehavior(party, target, data): patrol BK wracajacy do swojej warowni oddaje jencow
        /// jako niewolnikow. Kraina bez niewoli: szeregowi zdjeci z listy jencow patrolu i do domu / na Mur PRZED petla BK (BK widzi
        /// juz tylko bohaterow). Te same warunki co BK - inaczej BK nic nie robi i jency zostaja w patrolu.
        /// </summary>
        public static void PatrolPrefix(MobileParty __0, Settlement __1)
        {
            if (!On || __0 == null || __1 == null) return;
            try
            {
                var party = __0; var target = __1;
                var comp = party.PartyComponent;
                if (comp == null || comp.GetType().FullName != "BannerKings.Components.GarrisonPartyComponent") return;   // nie garnizon gry (ta sama nazwa klasy)
                if (target.Town == null || target != party.HomeSettlement || target.Town.GarrisonParty == null) return;
                var pr = party.PrisonRoster;
                if (pr == null || pr.TotalRegulars <= 0) return;
                int land = Land(target);
                if (land == LandNone) return;
                if (land == LandSlave)
                {
                    int n = Regulars(pr);
                    _slaves += n; _slavesPatrol += n; _totSlaves += n; AddBy(_slavesBy, target.Culture.StringId, n);
                    return;
                }
                var pos = target.GetPosition2D;
                bool wes = land == LandWesteros;
                for (int i = pr.Count - 1; i >= 0; i--)
                {
                    var e = pr.GetElementCopyAtIndex(i);
                    if (e.Character == null || e.Character.IsHero || e.Number <= 0) continue;
                    pr.AddToCounts(e.Character, -e.Number, false, -e.WoundedNumber);   // najpierw z listy - blad tu = BK jak dotad, bez dubla
                    Route(e.Character, e.Number, pos, wes, SPatrolBk);
                }
            }
            catch (Exception e)
            {
                _stumbles++;
                if (!_err) { _err = true; Log.Error("PrisonerLaw.PatrolPrefix", e); }
            }
        }

        /// <summary>CampaignEvents.OnPrisonerReleasedEvent - gracz wypuscil jencow (ekran oddzialu). Kraina = osada, w ktorej stoi,
        /// albo region jego miejsca na mapie.</summary>
        internal static void OnReleased(FlattenedTroopRoster roster)
        {
            if (!On || roster == null) return;
            try
            {
                Vec2 pos;
                int land = PlayerLand(out pos);
                var counts = new Dictionary<CharacterObject, int>();
                foreach (var el in roster)
                {
                    var t = el.Troop;
                    if (t == null || t.IsHero) continue;
                    int v; counts.TryGetValue(t, out v); counts[t] = v + 1;
                }
                if (counts.Count == 0) return;
                // ekran Aftermath bitwy gracza: wypuszczeni tam laduja w lewej liscie jencow, ktora LootEndPrefix oddaje w calosci - tu nie (bez dubla)
                if (EncounterLootOpen()) { _lootRelSkipped += counts.Values.Sum(); return; }
                if (land == LandNone) return;
                if (land == LandSlave) { _slaveReleased += counts.Values.Sum(); return; }   // jak dotad - znikaja
                bool wes = land == LandWesteros;
                foreach (var kv in counts) Route(kv.Key, kv.Value, pos, wes, SRelease);
            }
            catch (Exception e)
            {
                _stumbles++;
                if (!_err) { _err = true; Log.Error("PrisonerLaw.OnReleased", e); }
            }
        }

        /// <summary>Kraina gracza: osada, w ktorej stoi, albo region jego miejsca na mapie; pos - punkt.</summary>
        private static int PlayerLand(out Vec2 pos)
        {
            pos = Vec2.Zero;
            var mp = MobileParty.MainParty;
            if (mp == null) return LandNone;
            var st = mp.CurrentSettlement;
            pos = st != null ? st.GetPosition2D : mp.GetPosition2D;
            return Land(st ?? OutlawLaw.RegionAt(pos));
        }

        /// <summary>Otwarty ekran Aftermath bitwy gracza (tryb Loot, zamkniecie -> PlayerEncounter.OnPlayerLootMembersAndPrisonerEnd).
        /// Inne ekrany Loot (karawany, wsie, BK) - nie: ich wypuszczonych liczy OnReleased jak dotad.</summary>
        private static bool EncounterLootOpen()
        {
            if (!_lootPatched || _closedEvt == null) return false;
            try
            {
                var gsm = Game.Current != null ? Game.Current.GameStateManager : null;
                var ps = gsm != null ? gsm.ActiveState as TaleWorlds.CampaignSystem.GameState.PartyState : null;
                if (ps == null || ps.PartyScreenMode != Helpers.PartyScreenHelper.PartyScreenMode.Loot || ps.PartyScreenLogic == null) return false;
                var d = _closedEvt.GetValue(ps.PartyScreenLogic) as Delegate;
                if (d == null) return false;
                foreach (var x in d.GetInvocationList()) if (x.Method != null && x.Method.Name == LootEndName) return true;
                return false;
            }
            catch { _stumbles++; return false; }
        }

        /// <summary>
        /// Prefiks na PlayerEncounter.OnPlayerLootMembersAndPrisonerEnd(leftOwnerParty, leftMemberRoster __1, leftPrisonRoster __2, ...)
        /// - zamkniecie ekranu Aftermath po bitwie gracza; zaraz potem gra czysci obie lewe listy (ludzie w nicosc). Lewa lista jencow
        /// (nie wzieci + wypuszczeni na tym ekranie; po Anuluj - stan poczatkowy) - jak wypuszczeni przez gracza (Route, kraina miejsca);
        /// lewa lista ludzi (uwolnieni jency pokonanych i odprawieni tam wlasni) - wolni, do domu wedlug kultury (SendHome SrcLaw: dom
        /// liczony od punktu, pamiec H3 bitwy _battleHome nalezy do innej bitwy), bez Muru i bez niewoli. Ludzie H3 (rozbici, uwolnieni
        /// bez odbiorcy) zeszli z list przed tym ekranem - zbiory rozlaczne, takze z paczka F (przegrani uchodza przy bitwie gracza).
        /// </summary>
        public static void LootEndPrefix(TroopRoster __1, TroopRoster __2)
        {
            if (!On) return;
            try
            {
                Vec2 pos;
                int land = PlayerLand(out pos);
                if (__2 != null && land != LandNone)
                {
                    bool wes = land == LandWesteros;
                    for (int i = 0; i < __2.Count; i++)
                    {
                        var e = __2.GetElementCopyAtIndex(i);
                        if (e.Character == null || e.Character.IsHero || e.Number <= 0) continue;
                        if (land == LandSlave) { _in[SLoot] += e.Number; _slaveReleased += e.Number; continue; }   // jak wypuszczeni - znikaja jak dotad
                        Route(e.Character, e.Number, pos, wes, SLoot);
                    }
                }
                if (__1 != null)
                {
                    for (int i = 0; i < __1.Count; i++)
                    {
                        var e = __1.GetElementCopyAtIndex(i);
                        if (e.Character == null || e.Character.IsHero || e.Number <= 0) continue;
                        Free(e.Character, e.Number, pos);
                    }
                }
            }
            catch (Exception e)
            {
                _stumbles++;
                if (!_err) { _err = true; Log.Error("PrisonerLaw.LootEndPrefix", e); }
            }
        }

        /// <summary>Wolny czlowiek zostawiony po bitwie gracza: do domu (H3 SendHome), bez domu - do puli wyrzutkow miejsca.</summary>
        private static void Free(CharacterObject troop, int n, Vec2 pos)
        {
            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            try
            {
                _in[SLootFreed] += n;
                if (Undead.Character(troop)) { _undead += n; return; }
                int cat;
                int got = LosersFlee.SendHome(null, troop, n, pos, null, LosersFlee.SrcLaw, out cat);
                if (got > 0)
                {
                    _home += got; _totHome += got;
                    if (cat == LosersFlee.CatBk) _homeBk += got; else if (cat == LosersFlee.CatTemplate) _homeTpl += got; else _homeVillage += got;
                }
                int left = n - got;
                if (left > 0)
                {
                    var region = OutlawLaw.PoolRegionAt(pos);
                    if (OutlawLaw.On && region != null) { OutlawLaw.PoolAdd(region, troop, left); _pool += left; }
                    else _vanished += left;
                }
            }
            catch (Exception e)
            {
                _stumbles++;
                if (!_err) { _err = true; Log.Error("PrisonerLaw.Free", e); }
            }
            finally { _ticks += System.Diagnostics.Stopwatch.GetTimestamp() - t0; }
        }

        // ------------------------------------------------------------ log
        private static string Join(Dictionary<string, int> d)
        {
            if (d.Count == 0) return "-";
            return string.Join(", ", d.OrderByDescending(kv => kv.Value).Select(kv => kv.Key + " " + kv.Value).ToArray());
        }

        internal static void Daily()
        {
            try
            {
                if (Campaign.Current == null) return;
                int day = (int)CampaignTime.Now.ToDays;
                var sb = new StringBuilder();
                sb.Append("Prawo jenca (I1): dzien ").Append(day);
                if (!On)
                {
                    sb.Append(" - WYLACZONE (BK jak dotad: sprzedani jency to niewolnicy osady w kazdej krainie).");
                    Log.Info(sb.ToString());
                    return;
                }
                sb.Append(" - krainy bez niewoli: do domu ").Append(_home).Append(" (ludnosc BK ").Append(_homeBk).Append(", wsie ").Append(_homeVillage)
                  .Append(", z szablonu ").Append(_homeTpl).Append("), na Mur ").Append(_wall).Append(" (").Append(Join(_wallBy)).Append(')')
                  .Append(", przestepcy bez Muru (do domu) ").Append(_wallMissing)
                  .Append(", bez domu: do puli wyrzutkow ").Append(_pool).Append(", znikneli ").Append(_vanished)
                  .Append(", Inni (wighty - do niczego) ").Append(_undead).Append(", straceni (polityka Execution gracza) ").Append(_executed);
                sb.Append(" | wedlug zrodla:");
                for (int i = 0; i < Ss; i++) sb.Append(i == 0 ? " " : ", ").Append(SName[i]).Append(' ').Append(_in[i]);
                sb.Append(" | Westeros ").Append(_westeros).Append(", inne krainy bez niewoli ").Append(_freeOther);
                sb.Append(" | krainy z niewola: niewolnikow ").Append(_slaves).Append(" (z patroli BK ").Append(_slavesPatrol).Append("; ").Append(Join(_slavesBy)).Append(')')
                  .Append(", w tym ranni policzeni raz ").Append(_woundedOnce).Append(" (BK dopisywal ich drugi raz z niczego)")
                  .Append(", BK po staremu (nie dalo sie dopisac samemu) ").Append(_slavesBkOld).Append(" (ranni dwa razy ").Append(_slavesBkOldWounded).Append(')')
                  .Append(", inna albo nieodczytana polityka BK (osady gracza) ").Append(_slaveOther)
                  .Append(", sprzedaz gracza - BK bez osady (znikaja jak dotad) ").Append(_slavePlayer)
                  .Append(", wypuszczeni przez gracza i zostawieni po jego bitwie (znikaja jak dotad) ").Append(_slaveReleased);
                sb.Append(" | Aftermath: wypuszczeni tam (liczeni w \"jency zostawieni po bitwie gracza\", nie w \"wypuszczeni przez gracza\") ").Append(_lootRelSkipped);
                sb.Append(" | sesja: do domu ").Append(_totHome).Append(", na Mur ").Append(_totWall).Append(", niewolnikow ").Append(_totSlaves);
                sb.Append(" | latki: sprzedaz ").Append(_salePatched ? "tak" : "NIE").Append(", patrol BK ").Append(_patrolPatched ? "tak" : "NIE")
                  .Append(", Aftermath bitwy gracza ").Append(_lootPatched ? "tak" : "NIE");
                sb.Append(" | potkniecia ").Append(_stumbles).Append(" | czas ").Append((_ticks * 1000.0 / System.Diagnostics.Stopwatch.Frequency).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)).Append(" ms.");
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Log.Error("PrisonerLaw.Daily", e); }
            finally { ClearDay(); }
        }

        // ------------------------------------------------------------ wpiecie
        internal static void ApplyAll(Harmony h)
        {
            string sale, patrol;
            try
            {
                var outer = AccessTools.TypeByName("BannerKings.Patches.SettlementPatches");
                var inner = outer != null ? AccessTools.Inner(outer, "ApplyAllPrisionersPatch") : null;
                var m = inner != null ? AccessTools.Method(inner, "SendOffPrisoners", new[] { typeof(TroopRoster), typeof(Settlement) }) : null;
                if (m != null && m.IsStatic && m.ReturnType == typeof(void))
                {
                    h.Patch(m, prefix: new HarmonyMethod(typeof(PrisonerLaw), nameof(SendOffPrefix)));
                    _salePatched = true;
                    sale = "sprzedaz jencow (BK SendOffPrisoners) wpieta";
                }
                else sale = "sprzedaz jencow: BRAK METODY BK SendOffPrisoners - BK robi niewolnikow jak dotad";
            }
            catch (Exception e) { sale = "sprzedaz jencow: blad (" + e.Message + ") - BK jak dotad"; }
            try
            {
                var t = AccessTools.TypeByName("BannerKings.Behaviours.BKPartyBehavior");
                var m = t != null ? AccessTools.Method(t, "AddPatrolBehavior") : null;
                var ps = m != null ? m.GetParameters() : null;
                if (m != null && ps.Length == 3 && ps[0].ParameterType == typeof(MobileParty) && ps[1].ParameterType == typeof(Settlement))
                {
                    h.Patch(m, prefix: new HarmonyMethod(typeof(PrisonerLaw), nameof(PatrolPrefix)));
                    _patrolPatched = true;
                    patrol = "patrol BK (AddPatrolBehavior) wpiety";
                }
                else patrol = "patrol BK: BRAK METODY AddPatrolBehavior - jency patroli BK niewolnikami jak dotad";
            }
            catch (Exception e) { patrol = "patrol BK: blad (" + e.Message + ")"; }
            string loot;
            try
            {
                // obie rzeczy albo nic: bez rozpoznania ekranu Aftermath OnReleased i ten prefiks liczylyby wypuszczonych tam dwa razy
                _closedEvt = AccessTools.Field(typeof(TaleWorlds.CampaignSystem.Party.PartyScreenLogic), "PartyScreenClosedEvent");
                var t = AccessTools.TypeByName("TaleWorlds.CampaignSystem.Encounters.PlayerEncounter");
                var m = t != null ? AccessTools.Method(t, LootEndName) : null;
                var ps = m != null ? m.GetParameters() : null;
                if (_closedEvt != null && typeof(Delegate).IsAssignableFrom(_closedEvt.FieldType) && m != null && !m.IsStatic && ps.Length == 7
                    && ps[1].ParameterType == typeof(TroopRoster) && ps[2].ParameterType == typeof(TroopRoster))
                {
                    h.Patch(m, prefix: new HarmonyMethod(typeof(PrisonerLaw), nameof(LootEndPrefix)));
                    _lootPatched = true;
                    loot = "Aftermath bitwy gracza (PlayerEncounter." + LootEndName + ") wpiety";
                }
                else loot = "Aftermath bitwy gracza: BRAK METODY albo pola zdarzenia - zostawieni tam znikaja jak dotad";
            }
            catch (Exception e) { loot = "Aftermath bitwy gracza: blad (" + e.Message + ")"; }
            Log.Info("Prawo jenca (I1): " + sale + "; " + patrol + "; " + loot + "; wypuszczeni przez gracza - nasluch OnPrisonerReleasedEvent. Westeros bez niewoli: "
                     + string.Join(", ", WesterosIds.ToArray()) + "; krainy z niewola - lista H3 (LosersFlee.SlaveCulture); reszta bez niewoli, przestepcy tylko z Westeros na Mur.");
        }
    }
}
