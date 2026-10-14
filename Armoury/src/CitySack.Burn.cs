using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// LB - BUDOWLE PLONA, ZGLISZCZA SIE ODBUDOWUJE (kierunek Jeffa 13.10: "lupienie miasta to straszna rzecz ... budynki tez maja byc niszczone - przy spustoszeniu
    /// masa budynkow splonie i trzeba budowac od nowa"; "miasto musi potrafic sie odbudowac - nie za szybko, rozsadnie; ciagle lupione miasto nigdy sie nie odbuduje").
    /// Klucz SackBurnsBuildings, parametr SackRuinRebuildShare (0.25 - po recenzji; bylo 0.125).
    /// GRA DZIS (G SiegeAftermathCampaignBehavior.cs:133-139, :670-689): k = floor(log2(0.02 n + 2)) razy LevelDown losowej budowli bez projektow dziennych (takze murow) -
    ///    grabiez k, spustoszenie ceil(1.5 k), laska 0; losowanie trafia tez budowle na poziomie startowym (LevelDown nic nie robi). Typowe miasto ROT (97 miast:
    ///    17.5 budowli z BK, 14.3 poziomu palnego) traci srednio 2.2-3.6 poziomu przy grabiezy i 3.2-5.3 przy spustoszeniu (armia 1-10 tys.). BK budowli nie rusza.
    ///    Odbudowa dzis: budowy panow (BuildFunding) - miasto srednio 5.0 pkt/dobe (mediana 3.7), zamek 2.7 (bieg kodu 43) - spalone przez gre poziomy wracalyby 4-6 lat.
    /// ODTAD:
    ///  - plonie ta sama czesc co moneta (ShareOf: grabiez 1/3, spustoszenie 2/3, laska 0) poziomow palnych: budowle bez projektow dziennych i bez murow, ponad poziom
    ///    startowy; n = floor(czesc x poziomy palne), kazdy poziom losowany z waga = poziomy budowli ponad start (LevelDown gry - zdarzenie OnBuildingLevelChanged:
    ///    podsumowanie gracza "Town Projects Razed", pojemnosc zalogi i lochu). Liczba gry w czasie nastepstw = 0 (prefiks GetSiegeAftermathProjectsLoss), w podpowiedzi
    ///    menu gracza - nasze n dla tej osady ("Building Levels Reduced").
    ///  - MURY STOJA: kamien nie plonie; zdobywca zostaje panem i bedzie tych murow bronil; oblezenie juz je obilo (punkty budowli gry - HitPointChanged); poziom murow
    ///    miasta to 6 000-12 000 pkt - jeden spalony poziom murow kosztowalby wiecej niz odbudowa wszystkich zgliszcz typowego miasta i otwieralby odbicie miasta od reki.
    ///  - ZGLISZCZA: spalony poziom zostawia fundamenty, kamien i plan - jego odbudowa kosztuje SackRuinRebuildShare (1/4) kosztu poziomu. Budowla ze zgliszczami dostaje
    ///    postep (1 - 1/4) x koszt biezacego poziomu (gra przy LevelDown zeruje postep); po kazdym odbudowanym poziomie - nastepny ze zgliszcz (raz na poziom). Zloto:
    ///    nic z niczego - punkty budowy dalej kupuje pan (BuildFunding) i lud (P2), tylko mniej ich trzeba; postep to pamiec tego, co juz raz zbudowano.
    ///    Postep budowy W TOKU na spalonym poziomie plonie razem z nim (LevelDown gry zeruje postep; zloto za te punkty juz poszlo do robotnikow i na targ -
    ///    BuildFunding) - jawnie w linii LB i w linii doby ("postep w toku spalony").
    ///  - KOLEJKA AI: osada AI ze zgliszczami ma na czele kolejki budowle ze zgliszczami (najmniejsza reszta kosztu); gdy krolestwo pana jest w wojnie - cywilna tylko
    ///    tam, gdzie BuildFunding ja dopuszcza (WarFront.CivilAllowedPeek - spokoj wokol warowni i luz zoldu pana; odczyt bez licznikow), inaczej wojskowa; przy froncie
    ///    cywilne zgliszcze z czola ustepuje pierwszej budowli wojskowej z kolejki (BuildFunding cywilnej przy froncie nie placi - nie blokujemy murow).
    ///    Gracz uklada kolejke sam - widzi postep zgliszcz w oknie budow. Klucz wylaczony - zgliszcza nie dostaja postepu ani kolejki (czekaja w zapisie).
    /// Zapis: SaveText.Sync "arm_ruins" (osada|budowla|zgliszcza|poziom z postepem|doba). Wyjatek per osada - licznik potkniec, bez globalnego wylacznika.
    /// </summary>
    internal static partial class CitySack
    {
        internal static bool BurnOn { get { var s = Settings.Current; return s != null && s.SackBurnsBuildings; } }

        /// <summary>Czesc kosztu poziomu, ktora kosztuje odbudowa ze zgliszcz (0..1; domyslnie 1/4).</summary>
        internal static double RuinShare
        {
            get
            {
                var s = Settings.Current;
                float v = s != null ? s.SackRuinRebuildShare : 0.25f;
                if (float.IsNaN(v)) return 0.25;
                return v < 0f ? 0.0 : v > 1f ? 1.0 : v;
            }
        }

        private static bool Wall(Building b)
        {
            var t = b != null ? b.BuildingType : null;
            return t != null && t.StringId != null && t.StringId.IndexOf("fortification", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool Burnable(Building b) { return b != null && b.BuildingType != null && !b.BuildingType.IsDailyProject && !Wall(b); }
        private static int Above(Building b) { return b == null || b.BuildingType == null ? 0 : Math.Max(0, b.CurrentLevel - b.BuildingType.StartLevel); }

        internal static int BurnableLevels(Town t)
        {
            int n = 0;
            if (t == null || t.Buildings == null) return 0;
            foreach (var b in t.Buildings) if (Burnable(b)) n += Above(b);
            return n;
        }

        private static int WallLevels(Town t)
        {
            int n = 0;
            if (t == null || t.Buildings == null) return 0;
            foreach (var b in t.Buildings) if (b != null && Wall(b)) n += b.CurrentLevel;
            return n;
        }

        /// <summary>Ile poziomow splonie przy wyborze kind: floor(czesc x poziomy palne); laska 0.</summary>
        internal static int BurnPlan(Town t, int kind) { return (int)Math.Floor(ShareOf(kind) * BurnableLevels(t)); }

        // ------------------------------------------------------------ zgliszcza
        private sealed class Ruin { public Settlement St; public Building B; public int R; public int Lvl = -1; public float Day; }
        private static readonly Dictionary<Building, Ruin> _ruins = new Dictionary<Building, Ruin>();
        private static string _ruinPending;

        // liczniki doby
        private static int _dbSacks, _dbLevels, _dbRebuilt, _dbQueued, _dbWarWait, _dbFront;
        private static long _dbPts, _dbGrant, _dbProgLost;

        private static void Grant(Ruin r)
        {
            var b = r.B;
            if (r.R <= 0 || b.CurrentLevel >= 3 || r.Lvl == b.CurrentLevel) return;
            float head = (float)((1.0 - RuinShare) * b.GetConstructionCost());
            if (head > 0f) { b.BuildingProgress += head; _dbGrant += (long)head; }
            r.Lvl = b.CurrentLevel;
        }

        // ------------------------------------------------------------ nastepstwa: prefiks (osada w czasie nastepstw), prefiks liczby gry, postfiks (pozar)
        private static Settlement _burnSt;

        public static void BurnPre(Settlement __1) { _burnSt = __1; }

        public static void BurnFin() { _burnSt = null; }

        /// <summary>Prefiks GetSiegeAftermathProjectsLoss: w czasie nastepstw 0 (palimy sami w postfiksie), w podpowiedzi menu gracza - nasze n dla osady menu.</summary>
        public static bool ProjectsLossPre(SiegeAftermathAction.SiegeAftermath __1, ref int __result)
        {
            if (!BurnOn) return true;
            try
            {
                if (_burnSt != null) { __result = 0; return false; }
                var st = Settlement.CurrentSettlement;
                if (st == null || st.Town == null) return true;
                __result = BurnPlan(st.Town, KindOf(__1));
                return false;
            }
            catch (Exception e) { Stumble("ProjectsLossPre", e); return true; }
        }

        public static void BurnPost(Settlement __1, SiegeAftermathAction.SiegeAftermath __2)
        {
            var st = _burnSt;
            try
            {
                if (!Live || !BurnOn || st == null || !ReferenceEquals(st, __1) || st.Town == null) return;
                EnsureRuins();
                int kind = KindOf(__2);
                var t = st.Town;
                double share = ShareOf(kind);
                int a0 = BurnableLevels(t), w0 = WallLevels(t);
                int plan = (int)Math.Floor(share * a0);
                int done = 0; long pts = 0; double progLost = 0;
                var lost = new Dictionary<Building, int>();
                for (int i = 0; i < plan; i++)
                {
                    int tot = 0;
                    foreach (var b in t.Buildings) if (Burnable(b)) tot += Above(b);
                    if (tot <= 0) break;
                    int x = MBRandom.RandomInt(tot);
                    Building pick = null;
                    foreach (var b in t.Buildings)
                    {
                        if (!Burnable(b)) continue;
                        int w = Above(b);
                        if (x < w) { pick = b; break; }
                        x -= w;
                    }
                    if (pick == null) break;
                    int lv = pick.CurrentLevel;
                    float prog = pick.BuildingProgress;
                    pick.LevelDown();
                    if (pick.CurrentLevel >= lv) break;                     // nie zeszlo (poziom startowy) - koniec
                    done++;
                    if (prog > 0f) progLost += prog;                         // postep w toku plonie z poziomem (LevelDown gry zeruje) - recenzja: jawnie w linii
                    pts += pick.GetConstructionCost();                       // koszt odbudowy tego poziomu od nowa
                    int c; lost.TryGetValue(pick, out c); lost[pick] = c + 1;
                    Ruin r;
                    if (!_ruins.TryGetValue(pick, out r)) { r = new Ruin { St = st, B = pick }; _ruins[pick] = r; }
                    r.R++; r.Lvl = -1; r.Day = Now;
                }
                foreach (var kv in lost) Grant(_ruins[kv.Key]);
                if (kind != KMercy) _dbSacks++;
                _dbLevels += done; _dbPts += pts; _dbProgLost += (long)progLost;
                if (MeasureOn)
                {
                    int rb = 0, rl = 0;
                    foreach (var kv in _ruins) if (kv.Value.St == st && kv.Value.R > 0) { rb++; rl += kv.Value.R; }
                    var sb = new StringBuilder(500);
                    sb.Append("Lup miast (LB budowle): dzien ").Append((int)CampaignTime.Now.ToDays).Append(" | osada ").Append(st.StringId).Append(" (").Append(st.IsTown ? "miasto" : "zamek")
                      .Append(") | wybor ").Append(KName[kind]).Append(" | czesc ").Append(share.ToString("0.000", Inv))
                      .Append(" | poziomy palne (bez murow i projektow dziennych) ").Append(a0).Append(" -> ").Append(BurnableLevels(t))
                      .Append(" (splonelo ").Append(done).Append(" z planu ").Append(plan).Append(" = floor(czesc x ").Append(a0).Append(")) | mury ").Append(w0).Append(" -> ").Append(WallLevels(t))
                      .Append(" | koszt spalonych poziomow ").Append(pts).Append(" pkt, odbudowa ze zgliszcz ").Append((long)Math.Ceiling(pts * RuinShare)).Append(" pkt (x").Append(RuinShare.ToString("0.000", Inv)).Append(')')
                      .Append(" | zgliszcza osady: budowli ").Append(rb).Append(", poziomow ").Append(rl)
                      .Append(" | postep w toku spalony ").Append((long)progLost).Append(" pkt");
                    if (lost.Count > 0)
                    {
                        sb.Append(" | spalone:");
                        bool first = true;
                        foreach (var kv in lost) { sb.Append(first ? " " : ", ").Append(kv.Key.BuildingType.StringId).Append(" -").Append(kv.Value); first = false; }
                    }
                    Log.Info(sb.Append('.').ToString());
                }
            }
            catch (Exception e) { Stumble("BurnPost", e); }
        }

        // ------------------------------------------------------------ raz na dobe: odbudowane poziomy, postep ze zgliszcz, kolejka AI
        private static void RuinsDaily()
        {
            if (!BurnOn) return;                                             // recenzja (kod W3, obieg 10): klucz off = stan 2c0a225 - zgliszcza czekaja w zapisie
            EnsureRuins();
            if (_ruins.Count == 0) return;
            var drop = new List<Building>();
            var bySt = new Dictionary<Settlement, List<Ruin>>();
            foreach (var kv in _ruins)
            {
                var r = kv.Value; var b = kv.Key;
                try
                {
                    if (r.St == null || r.St.Town == null || b.BuildingType == null) { drop.Add(b); continue; }
                    if (r.Lvl >= 0 && b.CurrentLevel > r.Lvl) { int up = Math.Min(r.R, b.CurrentLevel - r.Lvl); r.R -= up; _dbRebuilt += up; r.Lvl = -1; }
                    else if (r.Lvl >= 0 && b.CurrentLevel < r.Lvl) r.Lvl = -1;           // poziom zszedl z innej przyczyny (ostrzal) - postep zgliszcz od nowa na tym poziomie
                    r.R = Math.Min(r.R, Math.Max(0, 3 - b.CurrentLevel));
                    if (r.R <= 0) { drop.Add(b); continue; }
                    Grant(r);
                    List<Ruin> l;
                    if (!bySt.TryGetValue(r.St, out l)) { l = new List<Ruin>(); bySt[r.St] = l; }
                    l.Add(r);
                }
                catch (Exception e) { Stumble("RuinsDaily", e); }
            }
            foreach (var b in drop) _ruins.Remove(b);
            foreach (var kv in bySt)
            {
                try { Prioritize(kv.Key, kv.Value); }
                catch (Exception e) { Stumble("RuinsDaily.Queue", e); }
            }
        }

        private static bool AtWar(Clan c)
        {
            var k = c != null ? c.Kingdom : null;
            if (k == null) return false;
            foreach (var o in Kingdom.All) if (o != null && o != k && !o.IsEliminated && k.IsAtWarWith(o)) return true;
            return false;
        }

        /// <summary>Osada AI: na czele kolejki budowla ze zgliszczami (najmniejsza reszta kosztu); w wojnie cywilna tylko z dala od frontu (bramka BuildFunding). Gracz - bez zmian.</summary>
        private static void Prioritize(Settlement st, List<Ruin> rs)
        {
            if (st.OwnerClan == null || st.OwnerClan == Clan.PlayerClan || st.IsUnderSiege) return;
            var t = st.Town;
            var q = t.BuildingsInProgress;
            if (q == null) return;
            // recenzja (obieg 3, kod W2): w wojnie cywilne wedlug tej samej bramki co BuildFunding (WarFront - z dala od frontu i z luzem zoldu pana), nie samej flagi wojny
            bool war = AtWar(st.OwnerClan);
            bool civilOk = !war || WarFront.CivilAllowedPeek(st);
            if (q.Count > 0)
            {
                Ruin hr; var head = q.Peek();
                if (head != null && _ruins.TryGetValue(head, out hr) && hr.R > 0 && (civilOk || head.BuildingType.IsMilitaryProject)) return;   // czolo juz ze zgliszcz (przy froncie - wojskowe)
            }
            Ruin best = null; float bestRest = float.MaxValue; bool civilWaits = false;
            foreach (var r in rs)
            {
                if (r.R <= 0 || r.B.CurrentLevel >= 3) continue;
                if (!civilOk && !r.B.BuildingType.IsMilitaryProject) { civilWaits = true; continue; }
                float rest = r.B.GetConstructionCost() - r.B.BuildingProgress;
                if (rest < bestRest) { bestRest = rest; best = r; }
            }
            if (best == null)
            {
                if (civilWaits) _dbWarWait++;
                // przy froncie cywilne zgliszcze na czele (postawione w spokoju) blokowaloby budowy: BuildFunding cywilnej przy froncie nie placi - pierwsza wojskowa z kolejki
                if (!civilOk && q.Count > 0)
                {
                    Ruin hr; var head = q.Peek();
                    if (head != null && head.BuildingType != null && !head.BuildingType.IsMilitaryProject && _ruins.TryGetValue(head, out hr))
                    {
                        Building mil = null;
                        foreach (var b in q) if (b != null && b.BuildingType != null && b.BuildingType.IsMilitaryProject && !b.BuildingType.IsDailyProject) { mil = b; break; }
                        if (mil != null) { ToHead(q, mil); _dbFront++; }
                    }
                }
                return;
            }
            ToHead(q, best.B);
            _dbQueued++;
        }

        private static void ToHead(Queue<Building> q, Building first)
        {
            var keep = new List<Building>();
            foreach (var b in q) if (b != null && b != first && !keep.Contains(b)) keep.Add(b);
            q.Clear();
            q.Enqueue(first);
            foreach (var b in keep) q.Enqueue(b);
        }

        private static void LBPart(StringBuilder sb)
        {
            int rs = 0, rb = 0, rl = 0; var seen = new HashSet<Settlement>();
            foreach (var kv in _ruins) if (kv.Value.R > 0) { rb++; rl += kv.Value.R; if (seen.Add(kv.Value.St)) rs++; }
            sb.Append(" | LB: ");
            if (!BurnOn) sb.Append("WYLACZONE (Sack Burns Buildings) - budowle zdejmuje gra; ");
            sb.Append("lupow z pozarem ").Append(_dbSacks).Append(", spalone poziomy ").Append(_dbLevels).Append(" (koszt ").Append(_dbPts).Append(" pkt, postep w toku spalony ").Append(_dbProgLost).Append(" pkt)")
              .Append("; zgliszcza: osad ").Append(rs).Append(", budowli ").Append(rb).Append(", poziomow ").Append(rl)
              .Append("; odbudowane ze zgliszcz dzis ").Append(_dbRebuilt).Append(", postep ze zgliszcz dany ").Append(_dbGrant).Append(" pkt")
              .Append("; kolejka AI: zgliszcza na czele ").Append(_dbQueued).Append(", cywilne czekaja (front albo pan bez luzu zoldu) ").Append(_dbWarWait)
              .Append(", przy froncie wojskowa przed cywilnym zgliszczem ").Append(_dbFront);
        }

        private static void LBClear() { _dbSacks = _dbLevels = _dbRebuilt = _dbQueued = _dbWarWait = _dbFront = 0; _dbPts = _dbGrant = _dbProgLost = 0; }

        private static bool LBAny { get { return _dbSacks > 0 || _dbLevels > 0 || _dbRebuilt > 0 || _dbQueued > 0 || _ruins.Count > 0; } }

        private static void RuinsReset() { _ruins.Clear(); _ruinPending = null; _burnSt = null; LBClear(); }

        // ------------------------------------------------------------ zapis (arm_ruins) - wersja 1
        private static void EnsureRuins()
        {
            if (_ruinPending == null) return;
            string s = _ruinPending;
            _ruinPending = null;
            try { ParseRuins(s); } catch (Exception e) { Stumble("EnsureRuins", e); }
        }

        internal static string ExportRuins()
        {
            if (_ruinPending != null) return _ruinPending;
            var sb = new StringBuilder(16 + 60 * _ruins.Count);
            sb.Append('1');
            foreach (var kv in _ruins)
            {
                var r = kv.Value;
                if (r.R <= 0 || r.St == null || r.St.StringId == null || kv.Key.BuildingType == null) continue;
                sb.Append(';').Append(r.St.StringId).Append('|').Append(kv.Key.BuildingType.StringId).Append('|').Append(r.R).Append('|').Append(r.Lvl).Append('|').Append(r.Day.ToString("R", Inv));
            }
            return sb.ToString();
        }

        internal static void ImportRuins(string s)
        {
            _ruins.Clear();
            _ruinPending = string.IsNullOrEmpty(s) ? null : s;
        }

        private static void ParseRuins(string s)
        {
            int bad = 0;
            var parts = s.Split(';');
            if (parts.Length == 0 || parts[0] != "1") { Log.Info("Lup miast: zapis arm_ruins w nieznanej wersji - pominiety."); return; }
            for (int i = 1; i < parts.Length; i++)
            {
                try
                {
                    var a = parts[i].Split('|');
                    if (a.Length < 5) { bad++; continue; }
                    var st = Settlement.Find(a[0]);
                    if (st == null || st.Town == null) { bad++; continue; }
                    Building b = null;
                    foreach (var x in st.Town.Buildings) if (x != null && x.BuildingType != null && x.BuildingType.StringId == a[1]) { b = x; break; }
                    if (b == null) { bad++; continue; }
                    int r = int.Parse(a[2], Inv), lvl = int.Parse(a[3], Inv);
                    if (r <= 0) continue;
                    _ruins[b] = new Ruin { St = st, B = b, R = r, Lvl = lvl, Day = float.Parse(a[4], NumberStyles.Float, Inv) };
                }
                catch { bad++; }
            }
            if (bad > 0) Log.Info("Lup miast: zapis arm_ruins - pominieto " + bad + " wpisow (osada albo budowla nieznana, uszkodzony wpis).");
        }

        private static void LBPatch()
        {
            var t = typeof(SiegeAftermathCampaignBehavior);
            Seal31.Patch("lup miast LB: liczba gry poziomow budowli (GetSiegeAftermathProjectsLoss)", AccessTools.Method(t, "GetSiegeAftermathProjectsLoss"), typeof(CitySack),
                         nameof(ProjectsLossPre), null, null, Priority.Low);
            Seal31.Patch("lup miast LB: budowle plona (OnSiegeAftermathApplied)", AccessTools.Method(t, "OnSiegeAftermathApplied"), typeof(CitySack),
                         nameof(BurnPre), nameof(BurnPost), nameof(BurnFin), Priority.High, Priority.Normal, Priority.Normal);
        }
    }
}
