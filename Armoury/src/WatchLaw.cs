using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// E4/9 (192, PLAN 4.15; projekt etapu 4 R14; plan kodu docs/etap4/PLAN-KODU-E49.md rozdz. 4): NOCNA STRAZ Z WESTEROS I DOZYWOTNIO.
    ///  - Levy(n, kanal) - jedyna droga czlowieka do sluzby Strazy: kanaly gry (notable i karczma Daru, A5, szablon nowej partii, przyrost zalog z modelu - Jeff 11.10
    ///    "zalogi Strazy dalej rosna same, ale z prawdziwych ludzi Westeros", milicja i karawany osad Strazy, uzgodnienie doby) decyduja, ILU braci przybywa (pulap 166
    ///    stoi bez zmian), Levy decyduje, SKAD sa: (1) konto MUR (skazancy I1 i bracia z powrotem - "przyjmowani zawsze"), (2) pule wyrzutkow regionow Westeros w budzecie
    ///    WatchPoolSharePercent (3%) puli regionu na dobe (Jeff 3% zamiast RK 5%) - najpierw zolnierze, potem prosci, bez dezerterow Strazy, (3) ludnosc Westeros
    ///    proporcjonalnie do ludzi regionow (kolejka kwot; wies -1/k, podloga 50 hearth), (4) reszta "" nazwana ("bez pokrycia"). Bez hamulca wolnych rak (P R5).
    ///  - Dezerter i rozbity "do lasu" ze Strazy - do puli wyrzutkow najblizszego regionu Polnocy (DeserterRegion; Jeff 11.10 TAK). Bracia "do domu" - na Mur
    ///    (PeopleBook.WatchHome; zwolnien 166 nie ma - WatchNoReleases).
    ///  - H1 (WatchDoesNotRaid): Straz nie pali wsi - postfiks na CampaignEventDispatcher.AiHourlyTick daje opcjom rabunku partii lorda Strazy wynik -1 (SetBehaviorScore,
    ///    bez Reset); petla wyboru ROT/gry bierze tylko wynik wiekszy niz -1.
    /// Stan w zapisie: tylko konto MUR w PeopleBook ("mw:"); tu budzet doby, kolejki kwot i liczniki (bez zapisu - po wczytaniu leniwie od nowa).
    /// Kazde cialo we wlasnym try, potkniecia liczone per miejsce (CLAUDE.md 7 - bez globalnych wylacznikow).
    /// </summary>
    internal static class WatchLaw
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private const double Eps = 1e-6;
        /// <summary>Kraina Polnocy (kultura ROT "battania" = The North - CLAUDE.md 2) - region puli dla dezerterow Strazy (RK WatchDeserterCultures).</summary>
        internal const string NorthRealm = "battania";
        /// <summary>Krainy Westeros - zrodla rekruta Strazy (RK 1.3 WatchSourceCultures; bez Wolnych Ludzi, Strazy i Skagos).</summary>
        internal static readonly HashSet<string> SourceRealms = new HashSet<string> { "battania", "river", "reach", "aserai", "vlandia", "vale", "stormlands", "sturgia", "crownlands", "dragonstone" };
        internal const int KDeduce = 0, KA5 = 1, KSpawn = 2, KGarrison = 3, KMilitia = 4, KCaravan = 5, KReconcile = 6, KKinds = 7;
        private static readonly string[] KName = { "notable i karczma", "A5", "nowe partie", "zalogi z modelu", "milicja", "karawany", "uzgodnienie" };

        private static Settings S { get { return Settings.Current; } }
        internal static bool On { get { var s = S; return s != null && s.WatchLawEnabled; } }
        /// <summary>Prawo czynne: klucz i ksiega pochodzenia po siewie (Levy i Mur potrzebuja udzialow).</summary>
        internal static bool Live { get { return On && PeopleBook.OriginLive; } }

        /// <summary>Wynik Levy: udzialy wedlug zrodla (kazde idzie do rosteru z wlasna przyczyna FWatch*).</summary>
        internal struct Levied { public PeopleBook.Shares Wall, Pool, People, None; }

        // ---------------------------------------------------------------- regiony (raz na dobe; kraina = kultura z dnia kalibracji - U3)
        private static readonly List<Settlement> _src = new List<Settlement>(), _north = new List<Settlement>();
        private static readonly HashSet<string> _srcRealms = new HashSet<string>();
        private static int _listDay = int.MinValue;
        private static readonly Dictionary<string, Settlement> _node = new Dictionary<string, Settlement>();

        // ---------------------------------------------------------------- budzet pul doby i kolejki kwot (bez zapisu)
        private static readonly Dictionary<string, double> _budget = new Dictionary<string, double>();
        private static double _budgetAll, _budgetUsed;
        private static int _budgetDay = int.MinValue, _budgetRegions;
        private static double _lastBudgetAll, _lastBudgetUsed; private static int _lastBudgetRegions;     // budzet doby, ktora sie skonczyla (linia), gdy nowy zalozylo juz uzgodnienie
        private static readonly Dictionary<string, double> _qPool = new Dictionary<string, double>(), _qPeople = new Dictionary<string, double>();

        // ---------------------------------------------------------------- liczniki doby i sesji
        private static readonly double[] _byK = new double[KKinds];
        private static double _dN, _dWall, _dPoolSold, _dPoolCommon, _dPeople, _dNone;
        private static readonly Dictionary<string, double> _dRealm = new Dictionary<string, double>();
        private static double _sWall, _sPool, _sPeople, _sNone;
        private static readonly Dictionary<string, double> _sRealm = new Dictionary<string, double>();
        private static int _dRejected, _dFuse, _dRaids, _sRaids;
        private static long _ticks;
        private static int _stumbles, _stumblesDay;
        private static readonly Dictionary<string, int> _stumbleAt = new Dictionary<string, int>();
        private static readonly HashSet<string> _err = new HashSet<string>();
        private static bool _startLogged;
        // migawka doby dla PeopleLedger ("Straz i Wolni Ludzie (4.15)")
        internal static double LastWall, LastPool, LastPeople, LastNone;
        internal static int LastRaids, LastReleased;

        // ---------------------------------------------------------------- latki (raz na proces)
        private static bool _h1Patched;
        private static MethodBase _h1Target;
        private static string _h1Missing;

        private static void Stumble(string where, Exception e)
        {
            try
            {
                _stumbles++; _stumblesDay++;
                int v; _stumbleAt.TryGetValue(where, out v); _stumbleAt[where] = v + 1;
                if (_err.Add(where)) Log.Error("WatchLaw." + where, e);
            }
            catch { }
        }

        /// <summary>Konstruktor ArmouryBehavior: stan jednej kampanii nie przecieka do drugiej (latki wpiete raz na proces - nie zerowane).</summary>
        internal static void Reset()
        {
            _src.Clear(); _north.Clear(); _srcRealms.Clear(); _listDay = int.MinValue; _node.Clear();
            _budget.Clear(); _budgetAll = _budgetUsed = 0; _budgetDay = int.MinValue; _budgetRegions = 0; _qPool.Clear(); _qPeople.Clear();
            _lastBudgetAll = _lastBudgetUsed = 0; _lastBudgetRegions = 0;
            ZeroDay();
            _sWall = _sPool = _sPeople = _sNone = 0; _sRealm.Clear(); _sRaids = 0;
            _stumbles = 0; _err.Clear(); _startLogged = false;
            LastWall = LastPool = LastPeople = LastNone = 0; LastRaids = LastReleased = 0;
        }

        private static void ZeroDay()
        {
            Array.Clear(_byK, 0, KKinds);
            _dN = _dWall = _dPoolSold = _dPoolCommon = _dPeople = _dNone = 0; _dRealm.Clear();
            _dRejected = _dFuse = _dRaids = 0; _ticks = 0; _stumblesDay = 0; _stumbleAt.Clear();
        }

        private static Settlement Node(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            Settlement s;
            if (_node.TryGetValue(id, out s)) return s;
            try { s = Settlement.Find(id); } catch { s = null; }
            _node[id] = s;
            return s;
        }

        private static int Today() { try { return (int)CampaignTime.Now.ToDays; } catch { return 0; } }

        private static void EnsureLists()
        {
            int day = Today();
            if (_listDay == day && (_src.Count > 0 || _north.Count > 0)) return;
            _listDay = day;
            _src.Clear(); _north.Clear(); _srcRealms.Clear();
            foreach (var node in OutlawLaw.RegionNodes())
            {
                try
                {
                    if (node == null || node.StringId == null) continue;
                    string realm = FreeHands.RealmOf(node);
                    if (SourceRealms.Contains(realm)) { _src.Add(node); _srcRealms.Add(realm); }
                    if (realm == NorthRealm) _north.Add(node);
                }
                catch (Exception e) { Stumble("EnsureLists", e); }
            }
        }

        /// <summary>Budzet pul doby: WatchPoolSharePercent % puli kazdego regionu-zrodla (bez kluczy wightow i postaci Strazy - OutlawLaw.PoolForWatch). Leniwie przy
        /// pierwszym Levy doby (po wczytaniu w srodku doby) albo w Daily na nowa dobe.</summary>
        private static void EnsureBudget()
        {
            int day = Today();
            if (_budgetDay == day) return;
            _lastBudgetAll = _budgetAll; _lastBudgetUsed = _budgetUsed; _lastBudgetRegions = _budgetRegions;
            _budgetDay = day;
            // E4 fixB (K4): _qPeople NIE zerowana co dobe - kolejka kwot ludnosci trwa miedzy dobami (zerowanie tylko w Reset); przy zerowaniu co dobe pierwszy
            // czlowiek doby szedl zawsze do najwiekszego regionu (d120: reach 41.3% wobec 29.1% z tabeli). _qPool zostaje dobowa (budzet pul i tak jest dobowy).
            _budget.Clear(); _budgetAll = 0; _budgetUsed = 0; _budgetRegions = 0; _qPool.Clear();
            var s = S;
            if (s == null || !OutlawLaw.On) return;
            EnsureLists();
            double pct = Math.Max(0.0, Math.Min(100.0, s.WatchPoolSharePercent)) / 100.0;
            if (!(pct > 0.0)) return;
            foreach (var r in _src)
            {
                try
                {
                    double b = pct * OutlawLaw.PoolForWatch(r);
                    if (!(b > Eps)) continue;
                    _budget[r.StringId] = b; _budgetAll += b; _budgetRegions++;
                }
                catch (Exception e) { Stumble("EnsureBudget", e); }
            }
        }

        // ================================================================ Levy (4.2)
        /// <summary>
        /// n ludzi do sluzby Strazy: Mur -> pule wyrzutkow Westeros (budzet doby) -> ludnosc Westeros -> reszta "" (bez pokrycia). Udzialy wedlug zrodla; suma = n.
        /// Wolajacy dopisuje je do rosteru (GiveLevy / ReconcileWatch). Nie rzuca; wyjatek w kroku - reszta idzie dalej (bez pokrycia), potkniecie z nazwa miejsca.
        /// Karawana (KCaravan) bez kroku Mur (recenzja E4/9 R1-2): roster karawany nie ma znacznika sluzby dozywotniej (PeopleBook.WatchServes), wiec brat z Muru po
        /// rozbiciu karawany szedlby do domu z hearth - karawana bierze od pul Westeros.
        /// </summary>
        internal static Levied Levy(double n, int kanal)
        {
            var res = new Levied { Wall = new PeopleBook.Shares(), Pool = new PeopleBook.Shares(), People = new PeopleBook.Shares(), None = new PeopleBook.Shares() };
            if (!(n > Eps) || double.IsInfinity(n) || double.IsNaN(n)) return res;
            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            if (kanal != KCaravan) { try { res.Wall = PeopleBook.WallTake(n); } catch (Exception e) { Stumble("Levy(Mur)", e); res.Wall = new PeopleBook.Shares(); } }
            double rest = n - res.Wall.Total;
            if (rest > Eps && OutlawLaw.On) { try { FromPools(rest, res.Pool); } catch (Exception e) { Stumble("Levy(pule)", e); } }
            rest = n - res.Wall.Total - res.Pool.Total;
            if (rest > Eps && PeopleBook.LevyLive) { try { FromPeople(rest, res.People); } catch (Exception e) { Stumble("Levy(ludnosc)", e); } }
            rest = n - res.Wall.Total - res.Pool.Total - res.People.Total;
            if (rest > Eps) res.None.Add("", rest);
            try
            {
                if (kanal >= 0 && kanal < KKinds) _byK[kanal] += n;
                _dN += n; _dWall += res.Wall.Total; _dPeople += res.People.Total; _dNone += res.None.Total;
                _sWall += res.Wall.Total; _sPool += res.Pool.Total; _sPeople += res.People.Total; _sNone += res.None.Total;
            }
            catch (Exception e) { Stumble("Levy(liczniki)", e); }
            _ticks += System.Diagnostics.Stopwatch.GetTimestamp() - t0;
            return res;
        }

        /// <summary>Krok 2: pule wyrzutkow regionow Westeros w budzecie doby - kolejka kwot (najwieksza reszta) z wagami = pozostaly budzet regionu; kazdy czlowiek
        /// (ulamek przy koncu) z regionu o najwiekszej kwocie; region bez ludzi w puli wypada z tego wolania.</summary>
        private static void FromPools(double want, PeopleBook.Shares into)
        {
            EnsureBudget();
            if (_budget.Count == 0) return;
            double start = into.Total;
            int guard = 0, max = 4 * _budget.Count + 64;
            HashSet<string> dry = null;
            while (want - (into.Total - start) > Eps && guard++ < max)
            {
                double u = Math.Min(1.0, want - (into.Total - start));
                double sum = 0.0;
                foreach (var kv in _budget) if (kv.Value > Eps && (dry == null || !dry.Contains(kv.Key))) sum += kv.Value;
                if (!(sum > Eps)) break;
                string best = null; double bq = double.MinValue;
                foreach (var kv in _budget)
                {
                    if (!(kv.Value > Eps) || (dry != null && dry.Contains(kv.Key))) continue;
                    double q; _qPool.TryGetValue(kv.Key, out q);
                    q += u * kv.Value / sum; _qPool[kv.Key] = q;
                    if (q > bq) { bq = q; best = kv.Key; }
                }
                if (best == null) break;
                double t = Math.Min(u, _budget[best]);
                float troops = 0f;
                var node = Node(best);
                double took = node != null ? OutlawLaw.TakeForWatch(node, (float)t, into, out troops) : 0.0;
                if (took < 0.0) took = 0.0;
                _qPool[best] = _qPool[best] - took;
                _budget[best] = _budget[best] - took; _budgetUsed += took;
                _dPoolSold += troops; _dPoolCommon += Math.Max(0.0, took - troops);
                if (took < t - 1e-6) { if (dry == null) dry = new HashSet<string>(); dry.Add(best); }
            }
        }

        /// <summary>Krok 3: ludnosc Westeros - wagi = DOM regionow-zrodel z widoku doby; kolejka kwot (q_r += w_r / suma w na czlowieka), region o najwiekszej kwocie,
        /// PeopleBook.LevyWatchPeople (wsie wedlug ludzi, podloga 50 hearth); region bez miejsca wypada z tego wolania.
        /// E4 fixB (K4): kolejka trwa miedzy dobami (bez zerowania w EnsureBudget), a kwoty zmieniaja sie tylko o to, co naprawde zdjeto: wybor regionu bez zapisu,
        /// potem wszystkim czynnym q_r += got * w_r / suma, wybranemu q -= got - suma kwot stoi na 0 i region bez miejsca nie zbiera kwoty (bez serii po odblokowaniu).</summary>
        private static void FromPeople(double want, PeopleBook.Shares into)
        {
            EnsureLists();
            var w = new Dictionary<string, double>(_src.Count);
            foreach (var r in _src)
            {
                var v = PeopleBook.ViewOf(r);
                if (v != null && v.Dom > 0.0) w[r.StringId] = v.Dom;
            }
            if (w.Count == 0) return;
            double start = into.Total;
            int guard = 0, max = 4 * w.Count + 64;
            HashSet<string> dry = null;
            while (want - (into.Total - start) > Eps && guard++ < max)
            {
                double u = Math.Min(1.0, want - (into.Total - start));
                double sum = 0.0;
                foreach (var kv in w) if (dry == null || !dry.Contains(kv.Key)) sum += kv.Value;
                if (!(sum > 0.0)) break;
                string best = null; double bq = double.MinValue;
                foreach (var kv in w)
                {
                    if (dry != null && dry.Contains(kv.Key)) continue;
                    double q; _qPeople.TryGetValue(kv.Key, out q);
                    q += u * kv.Value / sum;                                // E4 fixB (K4): tylko wybor - bez zapisu (zapis nizej, o to, co zdjeto)
                    if (q > bq) { bq = q; best = kv.Key; }
                }
                if (best == null) break;
                var node = Node(best);
                double got = node != null ? PeopleBook.LevyWatchPeople(node, u) : 0.0;
                if (!(got > 0.0)) got = 0.0;
                if (got > Eps)
                {
                    foreach (var kv in w)
                    {
                        if (dry != null && dry.Contains(kv.Key)) continue;
                        double q; _qPeople.TryGetValue(kv.Key, out q);
                        _qPeople[kv.Key] = q + got * kv.Value / sum;
                    }
                    _qPeople[best] = _qPeople[best] - got;
                    into.Add(best, got);
                    string realm = FreeHands.RealmOf(node);
                    double v; _dRealm.TryGetValue(realm, out v); _dRealm[realm] = v + got;
                    _sRealm.TryGetValue(realm, out v); _sRealm[realm] = v + got;
                }
                if (got < u - 1e-6)
                {
                    if (dry == null) dry = new HashSet<string>(); dry.Add(best);
                    double qb; if (_qPeople.TryGetValue(best, out qb) && qb > 1.0) _qPeople[best] = 1.0;   // asekuracja: region bez miejsca nie trzyma kwoty ponad 1 czlowieka
                }
            }
        }

        /// <summary>
        /// Jedna linia u kazdego wolajacego (E4/7 DeducePostfix i A5Post, E4/8 Y1-Y4): n rekrutow, ktorych gra wlasnie dopisala do rosteru `r` - przy czynnym prawie z Westeros
        /// (Levy, wejscia FWatchWall/FWatchPool/FWatchLevy/FNoneWatchLeft); przy wylaczonym - koszyk "" z przyczyna offCause (stan E4/7/E4/8; offCause &lt; 0 - nic).
        /// </summary>
        internal static void GiveLevy(TroopRoster r, double n, int kanal, int offCause)
        {
            if (r == null || !(n > Eps) || double.IsInfinity(n)) return;
            if (!Live) { if (offCause >= 0) PeopleBook.GiveNone(r, n, offCause); return; }
            var lv = Levy(n, kanal);
            PeopleBook.Give(r, lv.Wall, PeopleBook.FWatchWall);
            PeopleBook.Give(r, lv.Pool, PeopleBook.FWatchPool);
            PeopleBook.Give(r, lv.People, PeopleBook.FWatchLevy);
            PeopleBook.Give(r, lv.None, PeopleBook.FNoneWatchLeft);
        }

        // ================================================================ dezerter i rozbity "do lasu" (4.5)
        /// <summary>Region puli dla dezerterow i rozbitych "do lasu" partii Strazy: najblizszy pozycji partii region Polnocy. null - nie Straz albo prawo nieczynne
        /// (wolajacy bierze region miejsca jak dotad).</summary>
        internal static Settlement DeserterRegion(MobileParty mp)
        {
            try
            {
                if (mp == null || !Live) return null;
                // E4/9 K9: przy czynnym pochodzeniu ten sam predykat co znacznik W (rodzaj partii z pamieci doby, milicja osady Strazy) - dezerterzy i rozbici
                // ze znacznikiem W ida do puli Polnocy; bez pochodzenia jak dotad (biezacy CrownGifts.IsWatchParty)
                if (!(PeopleBook.OriginLive ? PeopleBook.ServesWatch(mp) : CrownGifts.IsWatchParty(mp))) return null;
                EnsureLists();
                if (_north.Count == 0) return null;
                var p = mp.Position.ToVec2();
                Settlement best = null; float bd = float.MaxValue;
                foreach (var r in _north)
                {
                    float d = p.DistanceSquared(r.GetPosition2D);
                    if (d < bd) { bd = d; best = r; }
                }
                return best;
            }
            catch (Exception e) { Stumble("DeserterRegion", e); return null; }
        }

        /// <summary>N7 (StragglersRally, recenzja K5/O5): region Polnocy najblizszy punktowi - rozbici Strazy rozchodzacy sie z osady zbiorki "do lasu" ida ta sama
        /// droga co rozbici Strazy w bitwie (DeserterRegion, E4/9 4.5); null - prawo nieczynne albo brak regionow Polnocy (wolajacy bierze region punktu).</summary>
        internal static Settlement NorthRegionAt(TaleWorlds.Library.Vec2 p)
        {
            try
            {
                if (!Live) return null;
                EnsureLists();
                Settlement best = null; float bd = float.MaxValue;
                foreach (var r in _north)
                {
                    float d = p.DistanceSquared(r.GetPosition2D);
                    if (d < bd) { bd = d; best = r; }
                }
                return best;
            }
            catch (Exception e) { Stumble("NorthRegionAt", e); return null; }
        }

        // ================================================================ H1 - Straz nie pali wsi (4.6)
        internal static void ApplyAll(Harmony h)
        {
            if (h == null || _h1Patched) return;
            try
            {
                var m = AccessTools.Method(typeof(CampaignEventDispatcher), "AiHourlyTick", new[] { typeof(MobileParty), typeof(PartyThinkParams) });
                _h1Target = m;
                if (m == null) { _h1Missing = "CampaignEventDispatcher.AiHourlyTick"; return; }
                h.Patch(m, postfix: new HarmonyMethod(typeof(WatchLaw), nameof(AiTickPostfix)) { priority = Priority.Low });
                _h1Patched = true;
            }
            catch (Exception e) { _h1Missing = "CampaignEventDispatcher.AiHourlyTick (" + e.GetType().Name + ")"; Log.Error("WatchLaw.ApplyAll", e); }
        }

        /// <summary>Postfiks Low: kazda opcja rabunku partii lorda Strazy (AI) dostaje wynik -1 (SetBehaviorScore - pierwsza rowna pozycja), bez Reset listy. Gracz w Strazy - poza H1.</summary>
        public static void AiTickPostfix(MobileParty party, PartyThinkParams partyThinkParams)
        {
            if (party == null || partyThinkParams == null) return;
            try
            {
                var s = S;
                if (s == null || !s.WatchDoesNotRaid || !party.IsLordParty || party.IsMainParty || !CrownGifts.IsWatch(party.MapFaction)) return;
                var list = partyThinkParams.AIBehaviorScores;
                if (list == null || list.Count == 0) return;
                var copy = new List<(AIBehaviorData, float)>(list.Count);
                foreach (var it in list) copy.Add(it);
                foreach (var it in copy)
                {
                    if (it.Item1.AiBehavior != AiBehavior.RaidSettlement || !(it.Item2 > -1f)) continue;
                    var d = it.Item1;
                    partyThinkParams.SetBehaviorScore(in d, -1f);
                    _dRejected++;
                }
                foreach (var it in partyThinkParams.AIBehaviorScores) if (it.Item1.AiBehavior == AiBehavior.RaidSettlement && it.Item2 > -1f) _dFuse++;
            }
            catch (Exception e) { Stumble("AiTickPostfix", e); }
        }

        /// <summary>Nasluch RaidCompletedEvent (tylko licznik): rabunek z napastnikiem frakcji Strazy (zwyciestwo i porazka).</summary>
        internal static void OnRaidCompleted(BattleSideEnum winnerSide, RaidEventComponent raid)
        {
            try
            {
                var att = raid != null ? raid.AttackerSide : null;
                var lp = att != null ? att.LeaderParty : null;
                if (lp != null && CrownGifts.IsWatch(lp.MapFaction)) { _dRaids++; _sRaids++; }
            }
            catch (Exception e) { Stumble("OnRaidCompleted", e); }
        }

        // ================================================================ linie (4.8, 10.1, 10.2)
        private static string Owners(MethodBase m)
        {
            try
            {
                if (m == null) return "-";
                var info = Harmony.GetPatchInfo(m);
                if (info == null) return "-";
                var o = info.Owners.Distinct().ToList();
                return o.Count > 0 ? string.Join("/", o.ToArray()) : "-";
            }
            catch { return "?"; }
        }

        private static string StartLine()
        {
            var s = S;
            EnsureLists();
            var miss = new List<string>();
            if (!_h1Patched) miss.Add(_h1Missing ?? "CampaignEventDispatcher.AiHourlyTick");
            if (!ThrallLaw.Patched) miss.Add(ThrallLaw.MissingNote ?? "BKRaidCaptureBehavior.ExecuteCapture");
            else if (!ThrallLaw.PreviewPatched) miss.Add(ThrallLaw.PreviewMissing ?? "BKRaidCaptureBehavior.PreviewCondition");
            var sb = new StringBuilder(1400);
            sb.Append("Krainy kanonu (E4/9): wpiete: jency z rabunku BK (BKRaidCaptureBehavior.ExecuteCapture - prefiks bool First) ").Append(ThrallLaw.Patched ? "TAK" : "BRAK")
              .Append(", podglad BK \"Estimated captives\" ukryty (BKRaidCaptureBehavior.PreviewCondition - postfiks Low) ").Append(ThrallLaw.PreviewPatched ? "TAK" : "BRAK")
              .Append(", Straz nie pali wsi (CampaignEventDispatcher.AiHourlyTick - postfiks Low) ").Append(_h1Patched ? "TAK" : "BRAK")
              .Append("; BRAK: ").Append(miss.Count > 0 ? string.Join(", ", miss.ToArray()) : "-")
              .Append("; wlasciciele latek celow: ExecuteCapture: ").Append(Owners(ThrallLaw.Target)).Append("; AiHourlyTick: ").Append(Owners(_h1Target))
              .Append("; ksiega pochodzenia E4/7: ").Append(PeopleBook.OriginLive ? "TAK" : (s != null && s.PeopleOriginEnabled ? "TAK (przed siewem)" : "BRAK"))
              .Append("; spustoszenie E4/5: ").Append(Devastation.On ? "TAK" : "BRAK")
              .Append("; zrodla Strazy: ").Append(_srcRealms.Count).Append(" krain Westeros (").Append(_src.Count).Append(" regionow), region dezercji: Polnoc (").Append(_north.Count).Append(" regionow)")
              .Append("; niewolni: bilans otwarcia ").Append(PeopleBook.ThrallsSeeded ? "TAK (" + PeopleBook.ThrallsOpening.ToString("0", Inv) + ")" : "BRAK (stary zapis - prog Zelaznych z BK 169c)")
              .Append("; kultury wsi bez postaci villager_ (BK: looter) ").Append(ThrallLaw.MissingVillagers())
              .Append("; ustawienia (stan gry): Straz z Westeros ").Append(On ? "TAK" : "NIE").Append(" (pule ").Append(s != null ? s.WatchPoolSharePercent : 0).Append("%/d)")
              .Append(", Straz nie pali wsi ").Append(s != null && s.WatchDoesNotRaid ? "TAK" : "NIE")
              .Append(", niewola w kanonie ").Append(ThrallLaw.KeyOn ? "TAK" : "NIE").Append(" (smierc ").Append(s != null ? s.ThrallYearlyLossPercent : 0).Append("%/rok)")
              .Append(", jency z rabunku z wsi ").Append(s != null && s.RaidCaptivesFromVillage ? "TAK" : "NIE")
              .Append(", dluznik do domu ").Append(s != null && s.DebtorDisbandsHome ? "TAK" : "NIE")
              .Append(", bez zwolnien Strazy (WatchNoReleases) ").Append(s != null && s.WatchNoReleases ? "TAK" : "NIE").Append('.');
            return sb.ToString();
        }

        private static string F0(double v) { return v.ToString("0", Inv); }
        private static string F1(double v) { return v.ToString("0.#", Inv); }

        private static string RealmPct(Dictionary<string, double> d, int top)
        {
            double all = 0; foreach (var v in d.Values) all += v;
            if (!(all > 0.0)) return "-";
            return string.Join(", ", d.OrderByDescending(kv => kv.Value).Take(top).Select(kv => kv.Key + " " + (100.0 * kv.Value / all).ToString("0.#", Inv) + "%").ToArray());
        }

        /// <summary>
        /// ArmouryBehavior.OnDailyTick po ArmsFromPeople.Daily (ksiega zamknela dobe - odcinek Strazy w PeopleBook.LastWatch): linia "Straz z Westeros (E4/9)", migawka
        /// dla PeopleLedger, zerowanie licznikow, budzet pul na nowa dobe. Bez logu - tylko migawka, zerowanie i budzet.
        /// </summary>
        internal static void Daily()
        {
            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            var s = S;
            bool log = s != null && s.LogEnabled;
            int day = Today() - 1;
            var g = PeopleBook.LastWatch;
            // budzet doby, ktora sie skonczyla: uzgodnienie (PeopleBook.Daily, juz nowa doba gry) moglo zalozyc budzet nowej doby - wtedy stary w _last*
            bool newer = _budgetDay == Today();
            double bAll = newer ? _lastBudgetAll : _budgetAll, bUsed = newer ? _lastBudgetUsed : _budgetUsed; int bReg = newer ? _lastBudgetRegions : _budgetRegions;
            try
            {
                LastWall = _dWall; LastPool = _dPoolSold + _dPoolCommon; LastPeople = _dPeople; LastNone = _dNone; LastRaids = _dRaids;
                LastReleased = ClanBudget.LastReleasedWatch;
            }
            catch (Exception e) { Stumble("Daily(migawka)", e); }
            if (log)
            {
                if (!_startLogged) { _startLogged = true; try { Log.Info(StartLine()); } catch (Exception e) { Stumble("StartLine", e); } }
                try { var note = PeopleBook.TakeCanonLoadNote(); if (note != null) Log.Info(note); } catch (Exception e) { Stumble("Daily(wczytanie)", e); }
                try { Log.Info(Line(day, g, bAll, bUsed, bReg, t0)); } catch (Exception e) { Stumble("Line", e); }
            }
            else { try { PeopleBook.TakeCanonLoadNote(); } catch { } }
            ZeroDay();
            try { EnsureBudget(); } catch (Exception e) { Stumble("Daily(budzet)", e); }       // nowa doba: z pul po powrotach i werbunku band (jesli uzgodnienie juz nie zalozylo)
        }

        private static string Line(int day, PeopleBook.WatchDay g, double bAll, double bUsed, int bReg, long t0)
        {
            var s = S;
            double pool = _dPoolSold + _dPoolCommon;
            double back = 0; if (g.Back != null) foreach (var x in g.Back) back += x;
            int cap = ClanBudget.WatchPoolCap;
            var sb = new StringBuilder(2600);
            sb.Append("Straz z Westeros (E4/9): dzien ").Append(day).Append(" | prawo ").Append(Live ? "CZYNNE" : (On ? "NIECZYNNE (ksiega pochodzenia przed siewem albo wylaczona)" : "WYLACZONE (Watch Law Enabled)"))
              .Append(" | stan ").Append(F0(g.S)).Append(" (partie ").Append(F0(g.Par)).Append(", zalogi ").Append(F0(g.Gar)).Append("; z pochodzeniem ")
              .Append(g.S > 0 ? (100.0 * g.Known / g.S).ToString("0.#", Inv) : "-").Append("%, bez pochodzenia ").Append(F0(g.Unknown)).Append("), pulap 166 ").Append(cap >= 0 ? cap.ToString(Inv) : "-")
              .Append(" | przybylo ").Append(F1(_dN)).Append(" = z Muru ").Append(F1(_dWall)).Append(" + z pul wyrzutkow ").Append(F1(pool))
              .Append(" (zolnierze ").Append(F1(_dPoolSold)).Append(", prosci ").Append(F1(_dPoolCommon)).Append("; budzet ").Append(s != null ? s.WatchPoolSharePercent : 0).Append("%: ")
              .Append(F1(bAll)).Append(" w ").Append(bReg).Append(" regionach, wykorzystano ").Append(bAll > Eps ? (100.0 * bUsed / bAll).ToString("0.#", Inv) : "0").Append("%)")
              .Append(" + z ludnosci ").Append(F1(_dPeople)).Append(" (").Append(_dRealm.Count > 0 ? string.Join(", ", _dRealm.OrderByDescending(kv => kv.Value).Select(kv => kv.Key + " " + F1(kv.Value)).ToArray()) : "-").Append(")")
              .Append(" + bez pokrycia ").Append(F1(_dNone))
              .Append(" | kanaly:");
            for (int k = 0; k < KKinds; k++) sb.Append(k > 0 ? ", " : " ").Append(KName[k]).Append(' ').Append(F1(_byK[k]));
            var b = g.Back ?? new double[PeopleBook.WVia];
            sb.Append(" | Mur: stan ").Append(F1(g.WallNow)).Append(" (skazancy dzis ").Append(F1(g.Convicts)).Append(", z Muru do sluzby ").Append(F1(g.WallOut))
              .Append(", bracia z powrotem dzis ").Append(F1(back)).Append(": rozbici ").Append(F1(b[PeopleBook.WvH3])).Append(", przepelnienie ").Append(F1(b[PeopleBook.WvW4c]))
              .Append(", rozwiazani ").Append(F1(b[PeopleBook.WvDisband])).Append(", bez bitwy ").Append(F1(b[PeopleBook.WvR6])).Append(", zwolnieni 166 ").Append(F1(b[PeopleBook.Wv166]))
              .Append(", milicja bez walki ").Append(F1(b[PeopleBook.WvMilitia])).Append(", inne ").Append(F1(b[PeopleBook.WvOther])).Append(')')
              .Append(" | ubylo ze slownikow Strazy ").Append(F1(g.Out)).Append(": polegli ").Append(F1(g.DeadAll)).Append(" (Westeros ").Append(F1(g.DeadKnown)).Append(", bez pochodzenia ").Append(F1(Math.Max(0.0, g.DeadAll - g.DeadKnown)))
              .Append("), zaraza ").Append(F1(g.Plague)).Append(", rozbici ").Append(F1(g.Routed)).Append(", dezercja ").Append(F1(g.Deserted)).Append(", do niewoli ").Append(F1(g.Captured))
              .Append("; do lasu: pula Polnocy ").Append(F1(g.WoodsN)).Append(", inne regiony ").Append(F1(g.WoodsO))
              .Append(" | zwolnieni 166 do domu ").Append(LastReleased).Append(" (WatchNoReleases ").Append(s != null && s.WatchNoReleases ? "TAK" : "NIE").Append("), do domu z hearth ").Append(F1(g.HomeNotWall))
              .Append(" | H1: odrzucone opcje rabunku ").Append(_dRejected).Append(", bezpiecznik ").Append(_dFuse).Append(", rabunki Strazy dzis ").Append(_dRaids).Append(" (od wczytania ").Append(_sRaids).Append(')')
              .Append(s != null && s.WatchDoesNotRaid ? "" : " [Watch Does Not Raid wylaczone]")
              .Append(" | od wczytania: z Muru ").Append(F0(_sWall)).Append(", z pul ").Append(F0(_sPool)).Append(", z ludnosci ").Append(F0(_sPeople)).Append(" (rozklad: ").Append(RealmPct(_sRealm, 10)).Append(")")
              .Append(", bez pokrycia ").Append(F0(_sNone))
              .Append(" | kontrola: S - Swczoraj - (wejscia ").Append(F1(g.In)).Append(" - wyjscia ").Append(F1(g.Out)).Append(" - w nicosc ").Append(F1(g.Q)).Append(" - przy zniknieciu partii ").Append(F1(g.Gone))
              .Append(") - zmiana rodzaju partii (+").Append(F1(g.KindIn)).Append(" / -").Append(F1(g.KindOut)).Append(") = ")
              .Append(g.First ? "pierwsza doba sesji (od jutra)" : g.Check.ToString("0.00", Inv))
              .Append(" | potkniecia ").Append(_stumblesDay);
            if (_stumbleAt.Count > 0) sb.Append(" (").Append(string.Join(", ", _stumbleAt.Select(kv => kv.Key + " " + kv.Value).ToArray())).Append(')');
            double ms = (_ticks + (System.Diagnostics.Stopwatch.GetTimestamp() - t0)) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            sb.Append(" | czas ").Append(ms.ToString("0.0", Inv)).Append(" ms (Levy w dobie i linia).");
            return sb.ToString();
        }
    }
}
