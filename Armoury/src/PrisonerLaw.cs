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
    ///
    /// Teraz, w krainie BEZ niewoli (kultura osady / regionu spoza listy LosersFlee.SlaveCulture - jedna lista z H3):
    ///  - Westeros bez Zelaznych Wysp: przestepca (zawod Bandit albo kultura bandycka - bandyci, wyrzutki z band) idzie na Mur:
    ///    ludnosc BK najblizszej warowni Nocnej Strazy w rekach Strazy (LosersFlee.ToWall; Straz werbuje stamtad jak z kazdej osady);
    ///    Muru nie ma - do domu jak reszta;
    ///  - reszta (i wszyscy w innych krainach bez niewoli: Braavos, Pentos, Lorath, Qarth, Wolni Ludzie, Ib, Sarnor, Wyspy Letnie,
    ///    Yi Ti) wraca do domu jedna funkcja pochodzenia H3 (LosersFlee.SendHome, zrodlo SrcLaw): dom = najblizsza miejscu osada
    ///    kultury jenca; zolnierz -> ludnosc BK tej osady, prosty czlowiek -> hearth jej regionu, straz karawan "z szablonu";
    ///    czego dom nie przyjal - do puli wyrzutkow regionu miejsca (prawo wyrzutkow wylaczone - licznik "znikneli").
    /// Gracz wybral w swojej osadzie polityke "Execution" - BK jak dotad (straceni, licznik). W krainie z niewola wszystko bez zmian
    /// (licznik K). NIE ruszamy: polityki karnej BK (lojalnosc, bezpieczenstwo, cena niewolnika), zlota za jencow (164b), rabunku BK
    /// (BKRaidCaptureBehavior), jencow bohaterow. Zapisu nie ma (liczniki dnia + suma sesji).
    /// </summary>
    internal static class PrisonerLaw
    {
        internal static bool On { get { var s = Settings.Current; return s != null && s.PrisonerLawEnabled; } }

        // Westeros bez Zelaznych Wysp (sturgia - thralle, na liscie niewoli H3); kultury osad ROT wedlug ludzie-regiony.csv (08.10)
        private static readonly HashSet<string> WesterosIds = new HashSet<string>
            { "battania", "river", "reach", "aserai", "vlandia", "vale", "stormlands", "crownlands", "dragonstone", "nightswatch", "skagosi" };

        private const int LandNone = -1, LandSlave = 0, LandWesteros = 1, LandFree = 2;
        // zrodla: sprzedaz w warowni (partie AI, loch osady, rozwiazanie, patrol gry), sprzedaz gracza, patrol BK, wypuszczeni przez gracza
        private const int SSale = 0, SPlayerSale = 1, SPatrolBk = 2, SRelease = 3, Ss = 4;
        private static readonly string[] SName = { "sprzedaz w warowni (partie AI, lochy)", "sprzedaz gracza", "patrol BK", "wypuszczeni przez gracza" };

        // liczniki doby
        private static readonly int[] _in = new int[Ss];
        private static int _westeros, _freeOther, _home, _homeBk, _homeVillage, _homeTpl, _wall, _wallMissing, _pool, _vanished, _undead, _executed;
        private static int _slaves, _slavesPatrol, _slaveOther, _slavePlayer, _slaveReleased, _stumbles;
        private static readonly Dictionary<string, int> _wallBy = new Dictionary<string, int>();
        private static readonly Dictionary<string, int> _slavesBy = new Dictionary<string, int>();
        // suma sesji
        private static long _totHome, _totWall, _totSlaves;
        private static long _ticks;                                              // czas Route (doba)
        private static bool _err, _salePatched, _patrolPatched;

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

        private static int Regulars(TroopRoster r)
        {
            int n = 0;
            for (int i = 0; i < r.Count; i++)
            {
                var e = r.GetElementCopyAtIndex(i);
                if (e.Character != null && !e.Character.IsHero && e.Number > 0) n += e.Number;
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
                if (westeros && Criminal(troop))
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
            Settlement st; bool player = false; int land; string pol;
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
                    int n = Regulars(__0);
                    if (player) _slavePlayer += n;                                  // BK bez osady - znikaja jak dotad
                    else
                    {
                        pol = Policy(st);
                        if (pol == null || pol == "Enslavement") { _slaves += n; _totSlaves += n; AddBy(_slavesBy, st.Culture.StringId, n); }
                        else _slaveOther += n;                                      // osada gracza z inna polityka - BK jak dotad
                    }
                    return true;
                }
                if (!player && Policy(st) == "Execution") { _executed += Regulars(__0); return true; }   // wybor gracza w jego osadzie
            }
            catch (Exception e)
            {
                _stumbles++;
                if (!_err) { _err = true; Log.Error("PrisonerLaw.SendOffPrefix", e); }
                return true;                                                        // nic jeszcze nie zrobione - BK jak dotad
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
                var mp = MobileParty.MainParty;
                if (mp == null) return;
                var st = mp.CurrentSettlement;
                Vec2 pos = st != null ? st.GetPosition2D : mp.GetPosition2D;
                var landSt = st ?? OutlawLaw.RegionAt(pos);
                int land = Land(landSt);
                var counts = new Dictionary<CharacterObject, int>();
                foreach (var el in roster)
                {
                    var t = el.Troop;
                    if (t == null || t.IsHero) continue;
                    int v; counts.TryGetValue(t, out v); counts[t] = v + 1;
                }
                if (counts.Count == 0 || land == LandNone) return;
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
                  .Append(", inna polityka BK (osady gracza) ").Append(_slaveOther)
                  .Append(", sprzedaz gracza - BK bez osady (znikaja jak dotad) ").Append(_slavePlayer)
                  .Append(", wypuszczeni przez gracza (znikaja jak dotad) ").Append(_slaveReleased);
                sb.Append(" | sesja: do domu ").Append(_totHome).Append(", na Mur ").Append(_totWall).Append(", niewolnikow ").Append(_totSlaves);
                sb.Append(" | latki: sprzedaz ").Append(_salePatched ? "tak" : "NIE").Append(", patrol BK ").Append(_patrolPatched ? "tak" : "NIE");
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
            Log.Info("Prawo jenca (I1): " + sale + "; " + patrol + "; wypuszczeni przez gracza - nasluch OnPrisonerReleasedEvent. Westeros bez niewoli: "
                     + string.Join(", ", WesterosIds.ToArray()) + "; krainy z niewola - lista H3 (LosersFlee.SlaveCulture); reszta bez niewoli, przestepcy tylko z Westeros na Mur.");
        }
    }
}
