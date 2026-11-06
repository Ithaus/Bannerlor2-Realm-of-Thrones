using System;
using System.Collections.Generic;
using System.Globalization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>
    /// 334e - ZASADA SILY LOKALNEJ (Jeff 06.11: "liczy sie, gdzie armia jest w danej chwili, a nie ile ogolnie jest"). Projekt docs/PROJEKT-334E-SILA-LOKALNA.md.
    /// O ataku decyduje sila wroga w polu, ktora ZDAZY dojsc do celu, zanim skonczymy (Z), wobec naszej sily (O): q = Z / O. Zalogi innych osad i armie daleko sie
    /// nie licza. Ten plik - miara: jednostki w polu (armia = wodz z doczepionymi, samotna druzyna lorda; bez zalog, milicji, karawan, band, Innych), czas dojscia
    /// z drogi gry (MapDistanceModel miedzy osadami - kotwica jednostki to najblizsza osada; czesc ladowa predkoscia ladowa jednostki, czesc morska morska - Zelazni
    /// z przeprawa), wiedza: PLOTKI - polozenia z migawki dobowej (budowana w petli F334 WarFront334.Compute), ZWIAD - wrogowie w zasiegu wzroku decydujacej druzyny
    /// (MapVisibilityModel.GetPartySpottingRange) i wrogowie z widocznym zamiarem na ten cel (DefendSettlement / BesiegeSettlement / cel armii) na zywo.
    /// Reszta: .Score.cs (fantom, ocena celu, cofanie SCA, armie lupia), .Siege.cs (oblezenie w toku, rabunek przerwany, BK stick), .Defence.cs (obrona pustych ziem),
    /// .Log.cs (linie, zdarzenia), .Test.cs (testy regul). Tylko kampania kanonu (jak cala rodzina 334). Nic nie tworzy - tylko oceny celow w mysli AI.
    /// Watki: mysl AI moze isc rownolegle (WarFront334.Iron) - caly stan za jednym zamkiem _lk (Monitor - wejscie powtorne dozwolone). Migawke buduje tylko watek
    /// glowny (Hourly / Daily); bez migawki z dzis miara milczy (mnoznik 1 = stara gra), zamiast liczyc z pustej listy.
    /// Po recenzjach 334e (334f): WIESC - wrog z migawki (nie widziany zwiadem, bez widocznego zamiaru na cel) musi sie dowiedziec i zawrocic: liczy sie 2 x doby drogi
    /// (wiesc idzie tempem lorda, potem marsz); TRZECIA STRONA - wrog naszego wroga, ktory sam jest w wojnie z panem celu, nie broni celu (nie wchodzi do Z).
    /// </summary>
    internal static partial class LocalFront334e
    {
        internal const string Tag = "Sila lokalna (334e): ";
        internal const float SiegeDays = 8.02f;      // AiMilitaryBehavior.AverageSiegeDurationAsDays
        internal const float HorizonDays = 12.03f;   // 8.02 + 4.01 (prog obrony gry GetDistanceScoreForDefending) - dalej nie zdazy nigdy
        internal const double OpenQ = 0.8, SiegeBanQ = 1.0, AbandonQ = 1.25;
        internal const double DefNeed = 1.2, DefDrop = 1.0;
        internal const float FoodMinDays = 3f;       // SCA: jedzenie < 3 doby x0.25
        internal const float HoldFloor = 0.5f;       // jak F2 / m116 (prog zmiany celu gry)
        private const int StumbleDayCap = 25;        // ponad tyle potkniec doby - miara nieczynna do konca doby (stara gra), licznik w linii
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static readonly object _lk = new object();

        private static Settings S { get { return Settings.Current; } }
        private static int Today { get { return (int)CampaignTime.Now.ToDays; } }
        private static int NowH { get { return (int)CampaignTime.Now.ToHours; } }

        // ---------------------------------------------------------------- klucze (kanon sprawdzany raz na godzine gry - wolane bardzo czesto)
        private static int _canonH = int.MinValue;
        private static bool _canon;
        private static bool CanonC
        {
            get
            {
                int h = NowH;
                if (h != _canonH) { _canon = WarFront334.Canon; _canonH = h; }
                return _canon;
            }
        }
        private static bool Live { get { var c = Campaign.Current; return c != null && c.GameStarted && CanonC && _dStum < StumbleDayCap; } }
        internal static bool PowerOn { get { var s = S; return s != null && s.LocalPower334e && Live; } }
        internal static bool PhantomOn { get { var s = S; return s != null && s.LocalPhantomOff334e && Live; } }
        internal static bool StanceOn { get { var s = S; return s != null && s.LocalStance334e && Live; } }
        internal static bool ScaLocalOn { get { var s = S; return s != null && s.ScaLocal334e && Live; } }
        internal static bool RestraintOff { get { var s = S; return s != null && s.LocalRestraint334e && Live; } }
        internal static bool ArmyRaidOn { get { var s = S; return s != null && s.ArmyRaid334e && s.LocalPower334e && Live; } }
        internal static bool JudgeOn { get { var s = S; return s != null && s.LocalSiegeJudge334e && Live; } }
        internal static bool StickYieldOn { get { var s = S; return s != null && s.BkSiegeStickYields334e && Live; } }
        internal static bool BreakOn { get { var s = S; return s != null && s.LocalRaidBreakOff334e && Live; } }
        internal static bool DefenceOn { get { var s = S; return s != null && s.LocalDefence334e && s.LocalPower334e && Live; } }

        // ---------------------------------------------------------------- potkniecia (per obiekt; powyzej progu doby - stara gra do konca doby)
        private static int _dStum, _sStum;
        private static readonly HashSet<string> _errSites = new HashSet<string>(StringComparer.Ordinal);
        internal static void Stumble(string where, Exception e)
        {
            bool first; int n;
            lock (_lk) { _dStum++; _sStum++; first = _errSites.Add(where); n = _errSites.Count; }
            if (!first) return;
            try { WarFront334.Stumble("334e " + where, e); } catch { }
            try { if (S != null && S.LogEnabled && n <= 40) Log.Info(Tag + "potkniecie " + where + " " + (e != null ? e.GetType().Name + ": " + e.Message : "-")); } catch { }
        }

        private static string Nm(IFaction f) { try { return f != null && f.Name != null ? f.Name.ToString() : "-"; } catch { return "?"; } }
        private static string Nm(Settlement st) { try { return st != null && st.Name != null ? st.Name.ToString() : "-"; } catch { return "?"; } }
        private static string Nm(MobileParty mp) { try { return mp != null && mp.Name != null ? mp.Name.ToString() : "-"; } catch { return "?"; } }
        private static string Id(IFaction f) { return f != null ? f.StringId ?? "-" : "-"; }
        private static string F0(double v) { return v.ToString("0", Inv); }
        private static string F2(double v) { return v.ToString("0.00", Inv); }

        // ================================================================ jednostka w polu
        internal sealed class Unit
        {
            public MobileParty Lead; public IFaction F; public Vec2 Pos; public float Str; public int Men;
            public float Land, Sea; public bool Ships, AtSea, Player;
            public Settlement Anchor; public bool AnchorPort; public float Off;
            public LV V;   // 334f: widok godziny tej druzyny (pamiec odwolania)
        }

        // ================================================================ 334f: widok godziny (stan druzyn i wojen czytany raz na godzine - jedna miara dla wszystkich ocen godziny)
        /// <summary>Stan druzyny w tej godzinie (zamiar, cel marszu, osada, armia, frakcja). Odswiezany przy pierwszym odczycie w godzinie.</summary>
        internal sealed class LV
        {
            public int H = int.MinValue; public bool Active; public IFaction F; public int Fi = -1;
            public Settlement Cur, Tgt, Besieged; public AiBehavior Beh; public Army Army; public bool ArmyLead; public object ArmyObj;
        }
        private static readonly Dictionary<MobileParty, LV> _lv = new Dictionary<MobileParty, LV>();
        private static int _hvH = int.MinValue;
        private const int FCap = 384;
        private static readonly Dictionary<IFaction, int> _fIdx = new Dictionary<IFaction, int>();
        private static readonly List<IFaction> _fList = new List<IFaction>();
        private static readonly sbyte[] _war = new sbyte[FCap * FCap];   // 0 nie wiadomo, 1 wojna, 2 bez wojny (godzina)
        private static readonly int[] _side = new int[FCap];

        /// <summary>Nowa godzina: frakcje, macierz wojen, sumy (cel, krolestwo) od nowa. Wolajacy trzyma _lk.</summary>
        private static void HourView()
        {
            int h = NowH;
            if (h == _hvH) return;
            _hvH = h;
            if (_fList.Count > 0) Array.Clear(_war, 0, _war.Length);
            _fIdx.Clear(); _fList.Clear(); _agg.Clear();
            if (_lv.Count > 6000) _lv.Clear();
        }

        private static int FI(IFaction f)
        {
            if (f == null) return -1;
            int i;
            if (_fIdx.TryGetValue(f, out i)) return i;
            if (_fList.Count >= FCap) return -1;
            i = _fList.Count; _fList.Add(f); _fIdx[f] = i;
            int sd = 0;
            try { sd = CanonWarBalance.SideOf(f); } catch { }
            _side[i] = sd;
            return i;
        }

        private static bool War(IFaction a, int ai, IFaction b, int bi)
        {
            if (a == null || b == null) return false;
            if (ai < 0 || bi < 0) return FactionManager.IsAtWarAgainstFaction(a, b);
            int x = ai * FCap + bi;
            sbyte v = _war[x];
            if (v == 0) { v = FactionManager.IsAtWarAgainstFaction(a, b) ? (sbyte)1 : (sbyte)2; _war[x] = v; _war[bi * FCap + ai] = v; }
            return v == 1;
        }

        private static bool Same(IFaction a, int ai, IFaction b, int bi)
        {
            if (a == null || b == null) return false;
            if (a == b) return true;
            int sa = ai >= 0 ? _side[ai] : CanonWarBalance.SideOf(a), sb = bi >= 0 ? _side[bi] : CanonWarBalance.SideOf(b);
            return sa != 0 && sa == sb;
        }

        /// <summary>Foe z macierzy godziny (ta sama regula: wojna z a, trzecia strona nie broni celu).</summary>
        private static bool FoeI(IFaction a, int ai, IFaction lf, int li, IFaction tf, int ti)
        {
            if (!War(a, ai, lf, li)) return false;
            if (tf == null || Same(a, ai, tf, ti) || Same(lf, li, tf, ti)) return true;
            return !War(lf, li, tf, ti);
        }

        private static LV LVOf(MobileParty mp)
        {
            HourView();
            LV v;
            if (!_lv.TryGetValue(mp, out v)) { v = new LV(); _lv[mp] = v; }
            if (v.H != _hvH)
            {
                v.H = _hvH;
                try
                {
                    v.Active = mp.IsActive; v.F = mp.MapFaction; v.Fi = FI(v.F); v.Cur = mp.CurrentSettlement; v.Tgt = mp.TargetSettlement; v.Beh = mp.DefaultBehavior;
                    var a = mp.Army; v.Army = a; v.ArmyLead = a != null && a.LeaderParty == mp; v.ArmyObj = v.ArmyLead ? a.AiBehaviorObject : null;
                    var bc = mp.BesiegerCamp; v.Besieged = bc != null && bc.SiegeEvent != null ? bc.SiegeEvent.BesiegedSettlement : null;
                }
                catch { v.Active = false; v.F = null; v.Fi = -1; }
            }
            return v;
        }

        private static LV LVOf(Unit u)
        {
            HourView();
            var v = u.V;
            if (v == null || v.H != _hvH) { v = LVOf(u.Lead); u.V = v; }
            return v;
        }

        /// <summary>Widoczny zamiar na te osade (obrona, oblezenie, rabunek, cel armii).</summary>
        private static bool TargetsV(LV v, Settlement t)
        {
            return (v.Tgt == t && (v.Beh == AiBehavior.DefendSettlement || v.Beh == AiBehavior.BesiegeSettlement || v.Beh == AiBehavior.RaidSettlement)) || (v.ArmyLead && ReferenceEquals(v.ArmyObj, t));
        }

        /// <summary>Odsiecz: zamiar OBRONY tej osady (DefendSettlement albo armia z tym celem, ktora jej nie oblega ani nie rabuje).</summary>
        private static bool RelievesV(LV v, Settlement t)
        {
            return (v.Tgt == t && v.Beh == AiBehavior.DefendSettlement)
                   || (v.ArmyLead && ReferenceEquals(v.ArmyObj, t) && v.Beh != AiBehavior.BesiegeSettlement && v.Beh != AiBehavior.RaidSettlement);
        }

        private static bool GoingToV(LV v, Settlement t) { return (v.Beh == AiBehavior.BesiegeSettlement && v.Tgt == t) || v.Besieged == t; }

        private static bool SameGroupV(MobileParty lead, LV v, MobileParty p)
        {
            if (p == null || lead == null) return false;
            if (lead == p) return true;
            return v.Army != null && v.Army == p.Army;
        }

        // ================================================================ 334f: pamiec drog miedzy osadami (droga gry osada-osada sie nie zmienia)
        private struct RK : IEquatable<RK>
        {
            public readonly Settlement A, T; public readonly int F;
            public RK(Settlement a, Settlement t, int f) { A = a; T = t; F = f; }
            public bool Equals(RK o) { return ReferenceEquals(A, o.A) && ReferenceEquals(T, o.T) && F == o.F; }
            public override bool Equals(object o) { return o is RK && Equals((RK)o); }
            public override int GetHashCode() { unchecked { return (((A != null ? A.GetHashCode() : 0) * 397) ^ (T != null ? T.GetHashCode() : 0)) * 31 + F; } }
        }
        private static readonly Dictionary<RK, KeyValuePair<float, float>> _road = new Dictionary<RK, KeyValuePair<float, float>>();
        private static long _dRoadHit;

        private static float Road(Settlement a, Settlement t, bool port, MobileParty.NavigationType nav, out float lr)
        {
            var key = new RK(a, t, (port ? 1 : 0) | ((int)nav << 1));
            KeyValuePair<float, float> kv;
            if (_road.TryGetValue(key, out kv)) { _dRoadHit++; lr = kv.Value; return kv.Key; }
            _dDist++;
            float d;
            try { d = Campaign.Current.Models.MapDistanceModel.GetDistance(a, t, port, false, nav, out lr); } catch { d = -1f; lr = 1f; }
            if (_road.Count > 400000) _road.Clear();
            _road[key] = new KeyValuePair<float, float>(d, lr);
            return d;
        }

        // ================================================================ 334f: koszt - liczniki czasu (Stopwatch.GetTimestamp, bez obiektow)
        private static long Ts() { return System.Diagnostics.Stopwatch.GetTimestamp(); }
        private static double TMs(long ticks) { return ticks * 1000.0 / System.Diagnostics.Stopwatch.Frequency; }
        private static long _dWaitT, _dListT, _dAggT, _dScoutT, _dModelT, _dDailyT, _dAggN, _dAggHit, _dChk, _dDiv;
        private static string _dDivNote;
        [ThreadStatic] private static long _modelT;   // czas modelu gry w mysli poza obozem (ThinkField odejmuje)

        private struct Entry { public Unit U; public float D; }

        private sealed class TList { public readonly List<Entry> E = new List<Entry>(); public readonly HashSet<MobileParty> Leads = new HashSet<MobileParty>(); }

        private sealed class Snap
        {
            public int Day = -1;
            public readonly List<Unit> Units = new List<Unit>(400);
            public readonly Dictionary<MobileParty, Unit> ByLead = new Dictionary<MobileParty, Unit>();
            public readonly Dictionary<string, double> OwnField = new Dictionary<string, double>(StringComparer.Ordinal);
            public int Garrisons;   // zawsze 0 (L10) - jednostki tylko z druzyn lordow
        }

        private static Snap _snap = new Snap();
        private static Settlement[] _anch = new Settlement[0], _ports = new Settlement[0];
        private static readonly Dictionary<Settlement, TList> _tl = new Dictionary<Settlement, TList>();
        private static readonly Dictionary<MobileParty, KeyValuePair<int, Unit>> _live = new Dictionary<MobileParty, KeyValuePair<int, Unit>>();
        private static readonly Dictionary<MobileParty, KeyValuePair<int, HashSet<MobileParty>>> _scout = new Dictionary<MobileParty, KeyValuePair<int, HashSet<MobileParty>>>();
        private static float _avgLand = 1f, _avgSea = 1f;

        // koszt
        private static long _dDist, _dQ, _dLiveUsed;
        private static double _hMs, _dMsSum, _dMsMax;
        private static int _hHour = -1, _dHours;

        internal static void Reset()
        {
            lock (_lk)
            {
                _snap = new Snap(); _anch = new Settlement[0]; _ports = new Settlement[0];
                _tl.Clear(); _live.Clear(); _scout.Clear(); _canonH = int.MinValue;
                _lv.Clear(); _road.Clear(); _agg.Clear(); _fIdx.Clear(); _fList.Clear(); Array.Clear(_war, 0, _war.Length); _hvH = int.MinValue;
                _dStum = _sStum = 0; _errSites.Clear();
                ZeroCost();
                ResetScore(); ResetSiege(); ResetDefence(); ResetLog();
            }
        }

        private static void ZeroCost()
        {
            _dDist = _dQ = _dLiveUsed = 0; _hMs = _dMsSum = _dMsMax = 0; _hHour = -1; _dHours = 0;
            _dRoadHit = _dWaitT = _dListT = _dAggT = _dScoutT = _dModelT = _dDailyT = _dAggN = _dAggHit = _dChk = _dDiv = 0; _dDivNote = null;
        }

        /// <summary>Czas zapytania miary do kosztu godziny (wolajacy trzyma _lk).</summary>
        private static void Cost(double ms)
        {
            int h = NowH;
            if (h != _hHour)
            {
                if (_hHour >= 0) { _dMsSum += _hMs; if (_hMs > _dMsMax) _dMsMax = _hMs; _dHours++; }
                _hHour = h; _hMs = 0;
            }
            _hMs += ms;
        }

        private static float Spd(MobileParty mp)
        {
            try { return mp != null && mp.IsActive ? mp.Speed : 0f; } catch { return 0f; }
        }

        /// <summary>Sila jednostki: druzyna (PartyBase.EstimatedStrength - z p9) albo wodz armii z doczepionymi. Czlonkowie armii w drodze do niej - osobno.</summary>
        private static float StrOf(MobileParty mp, out int men)
        {
            men = 0;
            float s = 0f;
            var a = mp.Army;
            if (a != null && a.LeaderParty == mp)
            {
                foreach (var p in a.Parties)
                {
                    if (p == null || !p.IsActive || p.Party == null) continue;
                    if (p != mp && p.AttachedTo != mp) continue;
                    s += p.Party.EstimatedStrength; men += p.Party.NumberOfAllMembers;
                }
                return s;
            }
            men = mp.Party.NumberOfAllMembers;
            return mp.Party.EstimatedStrength;
        }

        /// <summary>Jednostka z druzyny teraz (null - nie jednostka w polu: doczepiony czlonek armii, bez wodza, wodz w niewoli, rozwiazywana, banda / Inni / wyjeci).</summary>
        internal static Unit MakeUnit(MobileParty mp)
        {
            if (mp == null || !mp.IsActive || mp.Party == null) return null;
            if (mp.Army != null && mp.Army.LeaderParty != mp && mp.AttachedTo != null) return null;
            if (!mp.IsMainParty)
            {
                var h = mp.LeaderHero;
                if (h == null || h.IsPrisoner || !h.IsAlive || mp.IsDisbanding) return null;
            }
            var F = mp.MapFaction;
            if (F == null || ScaNoBandits320.Skip(F) != 0) return null;
            var u = new Unit { Lead = mp, F = F, Player = mp.IsMainParty };
            u.Str = StrOf(mp, out u.Men);
            u.Pos = mp.GetPosition2D;
            try { u.AtSea = mp.IsCurrentlyAtSea; } catch { }
            try { u.Ships = mp.HasNavalNavigationCapability; } catch { }
            float sp = Spd(mp);
            u.Land = !u.AtSea && sp > 0.05f ? sp : _avgLand;
            u.Sea = u.AtSea && sp > 0.05f ? sp : _avgSea;
            if (u.Land <= 0.05f) u.Land = 1f;
            if (u.Sea <= 0.05f) u.Sea = u.Land;
            var cs = mp.CurrentSettlement;
            if (cs != null) { u.Anchor = cs; u.Off = 0f; }
            else
            {
                var arr = u.AtSea && _ports.Length > 0 ? _ports : _anch;
                Settlement best = null; float bd = float.MaxValue;
                for (int i = 0; i < arr.Length; i++)
                {
                    float d = u.Pos.DistanceSquared(arr[i].GetPosition2D);
                    if (d < bd) { bd = d; best = arr[i]; }
                }
                u.Anchor = best; u.Off = best != null ? (float)Math.Sqrt(bd) : 0f; u.AnchorPort = u.AtSea && best != null && best.HasPort;
            }
            return u;
        }

        /// <summary>Doby drogi jednostki do osady (kotwica + droga gry miedzy osadami; czesc ladowa / morska wlasnymi predkosciami). -1 - brak drogi.</summary>
        internal static float DaysTo(Unit u, Settlement t)
        {
            if (u == null || u.Anchor == null || t == null) return -1f;
            float h0 = u.Off / (u.AtSea ? u.Sea : u.Land);
            if (u.Anchor == t) return h0 / CampaignTime.HoursInDay;
            var nav = (u.Ships || u.AtSea) ? MobileParty.NavigationType.All : MobileParty.NavigationType.Default;
            float lr = 1f, d;
            d = Road(u.Anchor, t, u.AnchorPort, nav, out lr);   // 334f: pamiec drog osada-osada (Z3)
            float lim = Campaign.MapDiagonal * 5f;
            if (!(d >= 0f) || d >= lim)
            {
                if (nav == MobileParty.NavigationType.Default) return -1f;
                nav = MobileParty.NavigationType.Default;
                d = Road(u.Anchor, t, false, nav, out lr);
                if (!(d >= 0f) || d >= lim) return -1f;
            }
            if (nav == MobileParty.NavigationType.Default || !(lr >= 0f) || lr > 1f) lr = 1f;
            float land = d * lr, sea = d - land;
            float h = h0 + land / u.Land + sea / u.Sea;
            return h / CampaignTime.HoursInDay;
        }

        // ================================================================ migawka doby (plotki) - watek glowny
        /// <summary>Migawka z dzis. Bez niej miara nic nie zmienia (mnoznik 1).</summary>
        private static bool SnapFresh { get { return _snap.Day == Today; } }

        /// <summary>HourlyTick (WarFront334Behavior, watek glowny): migawka po wczytaniu albo gdy Daily jej nie zbudowal (od 1. godziny doby - o polnocy buduje Daily
        /// po liniach doby), przeglad postawy nowych druzyn (Neutral). Nie rzuca.</summary>
        internal static void Hourly()
        {
            try
            {
                var c = Campaign.Current;
                if (c == null || !c.GameStarted || !Live) return;
                var s = S;
                if (s != null && (s.LocalPower334e || s.LocalSiegeJudge334e || s.LocalRaidBreakOff334e || s.ScaLocal334e))
                {
                    int d;
                    lock (_lk) d = _snap.Day;
                    if (d != Today && (d < 0 || CampaignTime.Now.CurrentHourInDay >= 1)) BuildSnap();
                }
                if (StanceOn) StanceSweep();
            }
            catch (Exception e) { Stumble("Hourly", e); }
        }

        /// <summary>Druzyny wojenne krolestw AI (bez krolestwa gracza i krolestw z jedna twierdza - tam zostaje BK) z postawa inna niz Neutral -> Neutral (nowe druzyny
        /// z doby; BK ustawia postawe raz na dobe).</summary>
        private static void StanceSweep()
        {
            IFaction player = null;
            try { player = Clan.PlayerClan != null ? Clan.PlayerClan.MapFaction : null; } catch { }
            int n = 0;
            foreach (var k in Kingdom.All)
            {
                if (k == null || k.IsEliminated || k == player || k.WarPartyComponents == null) continue;
                if (k.Fiefs == null || k.Fiefs.Count <= 1) continue;
                foreach (var wp in k.WarPartyComponents)
                {
                    var mp = wp != null ? wp.MobileParty : null;
                    if (mp == null || !mp.IsActive || mp.IsMainParty) continue;
                    if (mp.Objective != MobileParty.PartyObjective.Neutral) { mp.SetPartyObjective(MobileParty.PartyObjective.Neutral); n++; }
                }
            }
            if (n > 0) lock (_lk) _dStanceSweep += n;
        }
        private static int _dStanceSweep;

        /// <summary>Migawka (watek glowny: Hourly / Daily): polozenia wszystkich jednostek w polu, sila w polu krolestw dla SCA (3.3). Nie rzuca.</summary>
        internal static void BuildSnap()
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                var c = Campaign.Current;
                if (c == null) return;
                var anch = new List<Settlement>(1024); var ports = new List<Settlement>(256);
                foreach (var st in Settlement.All)
                {
                    if (st == null || !(st.IsFortification || st.IsVillage)) continue;
                    anch.Add(st);
                    try { if (st.HasPort) ports.Add(st); } catch { }
                }
                var sn = new Snap { Day = Today };
                lock (_lk)
                {
                    _avgLand = c.EstimatedAverageLordPartySpeed; _avgSea = c.EstimatedAverageLordPartyNavalSpeed;
                    if (_avgLand <= 0.05f) _avgLand = 1f;
                    if (_avgSea <= 0.05f) _avgSea = _avgLand;
                    _anch = anch.ToArray(); _ports = ports.ToArray();
                    foreach (var mp in MobileParty.AllLordParties)
                    {
                        try { var u = MakeUnit(mp); if (u != null) { sn.Units.Add(u); sn.ByLead[mp] = u; } }
                        catch (Exception e) { Stumble("BuildSnap.unit", e); }
                    }
                    var main = MobileParty.MainParty;
                    if (main != null && !sn.ByLead.ContainsKey(main))
                    {
                        try { var u = MakeUnit(main); if (u != null) { sn.Units.Add(u); sn.ByLead[main] = u; } } catch (Exception e) { Stumble("BuildSnap.main", e); }
                    }
                    _snap = sn; _tl.Clear(); _live.Clear(); _scout.Clear(); _agg.Clear();
                    // 3.3: sila w polu krolestw (swoi + koalicja 274) w zasiegu 8.02 doby od wlasnych twierdz - lustro F334 dla SCA
                    if (ScaLocalOn)
                    foreach (var k in Kingdom.All)
                    {
                        if (k == null || k.IsEliminated) continue;
                        try { sn.OwnField[k.StringId] = OwnFieldOf(k, sn); } catch (Exception e) { Stumble("BuildSnap.own", e); }
                    }
                }
            }
            catch (Exception e) { Stumble("BuildSnap", e); }
            lock (_lk) { _snapMs = sw.Elapsed.TotalMilliseconds; }
        }
        private static double _snapMs;

        private static bool SameSide(IFaction a, IFaction b)
        {
            if (a == null || b == null) return false;
            if (a == b) return true;
            int sa = CanonWarBalance.SideOf(a);
            return sa != 0 && sa == CanonWarBalance.SideOf(b);
        }

        private static double OwnFieldOf(Kingdom k, Snap sn)
        {
            var forts = new List<Settlement>();
            foreach (var st in k.Settlements) if (st != null && st.IsFortification) forts.Add(st);
            if (forts.Count == 0) return 0;
            double s = 0;
            float reach = _avgLand * CampaignTime.HoursInDay * CanonWarBalance.DefendRangeDays;
            foreach (var u in sn.Units)
            {
                if (u.Player || !SameSide(k, u.F)) continue;
                Settlement nb = null; float bd = float.MaxValue;
                for (int i = 0; i < forts.Count; i++) { float d = u.Pos.DistanceSquared(forts[i].GetPosition2D); if (d < bd) { bd = d; nb = forts[i]; } }
                if (nb == null) continue;
                float lim = reach / (u.Ships ? 0.35f : 1f);
                if (bd > lim * lim) continue;
                float dd = DaysTo(u, nb);
                if (dd >= 0f && dd <= CanonWarBalance.DefendRangeDays) s += u.Str;
            }
            return s;
        }

        /// <summary>SCA (3.3): sila w polu krolestwa i koalicji w zasiegu 8.02 doby od jego twierdz. -1 - migawka nieczynna.</summary>
        internal static double OwnField(Kingdom k)
        {
            if (k == null || !WarFront334.Ensure()) return -1;
            lock (_lk)
            {
                double v;
                return _snap.Day == Today && _snap.OwnField.TryGetValue(k.StringId, out v) ? v : -1;
            }
        }

        /// <summary>Lista jednostek w horyzoncie celu z migawki (doba), posortowana po dobach. Wolajacy trzyma _lk.</summary>
        private static TList ListOf(Settlement t)
        {
            TList l;
            if (_tl.TryGetValue(t, out l)) return l;
            l = new TList();
            if (!SnapFresh) return l;   // bez migawki z dzis - pusta lista (wolajacy i tak milczy: SnapFresh); nie zapamietujemy
            var tp = t.GetPosition2D;
            var bound = t.IsVillage && t.Village != null ? t.Village.Bound : null;
            if (bound != null && bound != t)
            {
                // wies dziedziczy doby swojej twierdzy (droga gry) + odcinek twierdza - wies ladem (bez nowych zapytan o droge)
                var lb = ListOf(bound);
                float off = bound.GetPosition2D.Distance(tp);
                foreach (var e in lb.E)
                {
                    float d = e.D + off / Math.Max(0.05f, e.U.Land) / CampaignTime.HoursInDay;
                    if (d > HorizonDays) continue;
                    l.E.Add(new Entry { U = e.U, D = d });
                    l.Leads.Add(e.U.Lead);
                }
                l.E.Sort((a, b) => a.D.CompareTo(b.D));
                _tl[t] = l;
                return l;
            }
            long lt0 = Ts();
            foreach (var u in _snap.Units)
            {
                // filtr linii prostej (droga nigdy krotsza): ladem predkosc ladowa; statki - z zapasem 0.35 na objazdy morzem
                float maxD = (u.Ships || u.AtSea ? Math.Max(u.Land, u.Sea) / 0.35f : u.Land) * CampaignTime.HoursInDay * HorizonDays;
                if (u.Pos.DistanceSquared(tp) > maxD * maxD) continue;
                float d = DaysTo(u, t);
                if (d < 0f || d > HorizonDays) continue;
                l.E.Add(new Entry { U = u, D = d });
                l.Leads.Add(u.Lead);
            }
            l.E.Sort((a, b) => a.D.CompareTo(b.D));
            _tl[t] = l;
            _dListT += Ts() - lt0;
            return l;
        }

        /// <summary>Jednostka na zywo (zwiad / zamiar), pamiec na godzine. Wolajacy trzyma _lk.</summary>
        private static Unit LiveUnit(MobileParty mp)
        {
            if (mp == null) return null;
            int h = NowH;
            KeyValuePair<int, Unit> kv;
            if (_live.TryGetValue(mp, out kv) && kv.Key == h) return kv.Value;
            Unit u = null;
            try { u = MakeUnit(mp); } catch (Exception e) { Stumble("LiveUnit", e); }
            _live[mp] = new KeyValuePair<int, Unit>(h, u);
            return u;
        }

        /// <summary>Zwiad: druzyny lordow (i gracz) w zasiegu wzroku decydujacej druzyny (umiejetnosc Zwiadu przez model gry), pamiec na godzine. Wolajacy trzyma _lk.</summary>
        private static HashSet<MobileParty> ScoutOf(MobileParty p)
        {
            int h = NowH;
            KeyValuePair<int, HashSet<MobileParty>> kv;
            if (_scout.TryGetValue(p, out kv) && kv.Key == h) return kv.Value;
            var set = new HashSet<MobileParty>();
            long st0 = Ts();
            try
            {
                float r = Campaign.Current.Models.MapVisibilityModel.GetPartySpottingRange(p, false).ResultNumber;
                if (r > 0f)
                {
                    var data = MobileParty.StartFindingLocatablesAroundPosition(p.GetPosition2D, r);
                    for (var m = MobileParty.FindNextLocatable(ref data); m != null; m = MobileParty.FindNextLocatable(ref data))
                    {
                        if (m == p || !m.IsActive) continue;
                        if (!m.IsLordParty && !m.IsMainParty) continue;
                        if (m.Army != null && m.Army.LeaderParty != m && m.AttachedTo != null) m = m.Army.LeaderParty;
                        if (m != null && m != p) set.Add(m);
                    }
                }
            }
            catch (Exception e) { Stumble("ScoutOf", e); }
            _dScoutT += Ts() - st0;
            _scout[p] = new KeyValuePair<int, HashSet<MobileParty>>(h, set);
            return set;
        }

        /// <summary>Wrog strony a przy celu t: w wojnie z a i nie trzecia strona (wrog naszego wroga, sam w wojnie z panem celu i spoza jego strony, nie broni celu).</summary>
        private static bool Foe(IFaction a, IFaction lf, Settlement t)
        {
            HourView();
            var tf = t != null ? t.MapFaction : null;
            return a != null && lf != null && FoeI(a, FI(a), lf, FI(lf), tf, FI(tf));
        }

        private static bool SameGroup(MobileParty a, MobileParty b)
        {
            if (a == null || b == null) return false;
            if (a == b) return true;
            return a.Army != null && a.Army == b.Army;
        }

        // ================================================================ 334f (Z2): sumy wrogow na godzine dla pary (cel, krolestwo)
        private struct AE { public float D, S; public MobileParty L; public bool Live; }
        private sealed class Agg
        {
            public float[] ED; public float[] ES; public double[] ECum;          // wrogowie po dobach (zamiar - zywa droga, plotka - 2 x doby), sumy narastajace
            public readonly Dictionary<MobileParty, int> Rumor = new Dictionary<MobileParty, int>();   // wrog z plotki -> indeks (poprawka zwiadu)
            public readonly List<float> AD = new List<float>(), AS = new List<float>(); public readonly List<MobileParty> AL = new List<MobileParty>();   // nasi idacy na cel
            public HashSet<MobileParty> Leads; public IFaction Tf; public int Ti;
        }
        private static readonly Dictionary<SK, Agg> _agg = new Dictionary<SK, Agg>();
        private static readonly List<AE> _aeTmp = new List<AE>(512);

        /// <summary>Sumy (cel, krolestwo) z widoku godziny; pamiec do konca godziny. Wolajacy trzyma _lk.</summary>
        private static Agg AggOf(Settlement t, IFaction a)
        {
            HourView();
            var key = new SK(t, a);
            Agg g;
            if (_agg.TryGetValue(key, out g)) { _dAggHit++; return g; }
            var l = ListOf(t);
            long t0 = Ts();
            g = new Agg { Leads = l.Leads };
            bool keep = SnapFresh;   // bez migawki z dzis - lista pusta, sumy nie zapamietane (jak ListOf)
            int ai = FI(a);
            var tf = t.MapFaction; int ti = FI(tf);
            g.Tf = tf; g.Ti = ti;
            var en = _aeTmp; en.Clear();
            for (int i = 0; i < l.E.Count; i++)
            {
                var e = l.E[i];
                var lead = e.U.Lead;
                if (lead == null) continue;
                var v = LVOf(e.U);
                if (!v.Active || v.F == null) continue;
                bool enemy = FoeI(a, ai, v.F, v.Fi, tf, ti);
                bool ours = !enemy && !War(a, ai, v.F, v.Fi) && Same(a, ai, v.F, v.Fi);
                if (!enemy && !ours) continue;
                if (enemy)
                {
                    if (TargetsV(v, t))
                    {
                        var u = LiveUnit(lead);
                        if (u == null || v.Cur == t) continue;
                        float d = DaysTo(u, t);
                        if (d < 0f) continue;
                        en.Add(new AE { D = d, S = u.Str, L = lead, Live = true });
                    }
                    else
                    {
                        if (v.Cur == t) continue;
                        en.Add(new AE { D = e.D * 2f, S = e.U.Str, L = lead, Live = false });   // wiesc + marsz
                    }
                }
                else
                {
                    if (!GoingToV(v, t)) continue;
                    var u = LiveUnit(lead);
                    if (u == null || v.Cur == t) continue;
                    float d = DaysTo(u, t);
                    if (d < 0f) continue;
                    g.AD.Add(d); g.AS.Add(u.Str); g.AL.Add(lead);
                }
            }
            en.Sort((x, y) => x.D.CompareTo(y.D));
            int n = en.Count;
            g.ED = new float[n]; g.ES = new float[n]; g.ECum = new double[n];
            double c = 0;
            for (int i = 0; i < n; i++)
            {
                var x = en[i];
                g.ED[i] = x.D; g.ES[i] = x.S; c += x.S; g.ECum[i] = c;
                if (!x.Live) g.Rumor[x.L] = i;
            }
            en.Clear();
            if (keep) _agg[key] = g;
            _dAggN++;
            _dAggT += Ts() - t0;
            return g;
        }

        private static int Upper(float[] a, float w)
        {
            int lo = 0, hi = a.Length;
            while (lo < hi) { int m = (lo + hi) >> 1; if (a[m] <= w) lo = m + 1; else hi = m; }
            return lo;
        }

        /// <summary>Z: sila wrogow strony a (jej wojny), ktorzy dojda do t w W dobach (plotki z migawki, zwiad i zamiar na zywo). first - pierwszy przyjazd wroga,
        /// ally - sojusznicy juz idacy na t, ktorzy dojda przed pierwszym wrogiem (i w W). 334f: z sum godziny (cel, krolestwo) + poprawka zwiadu druzyny p; co 128.
        /// ocena - kontrola starym przegladem (ZForRef, ten sam widok godziny): rozbieznosci w linii koszt (ma byc 0). Wolajacy trzyma _lk.</summary>
        private static double ZFor(Settlement t, IFaction a, MobileParty p, float w, out float first, out double ally)
        {
            var g = AggOf(t, a);
            HashSet<MobileParty> sc = p != null ? ScoutOf(p) : null;
            first = float.MaxValue; ally = 0;
            int k = Upper(g.ED, w);
            double z = k > 0 ? g.ECum[k - 1] : 0;
            List<int> rem = null;
            if (sc != null && sc.Count > 0)
            {
                foreach (var m in sc)
                {
                    int idx;
                    if (g.Rumor.TryGetValue(m, out idx))
                    {
                        // wrog z plotki widziany zwiadem: zamiast 2 x doby z migawki - zywa droga i sila
                        if (rem == null) rem = new List<int>(4);
                        rem.Add(idx);
                        if (idx < k) z -= g.ES[idx];
                        var u = LiveUnit(m);
                        if (u == null) continue;
                        float d = DaysTo(u, t);
                        if (d < 0f || d > w) continue;
                        z += u.Str; _dLiveUsed++;
                        if (d < first) first = d;
                    }
                    else if (!g.Leads.Contains(m))
                    {
                        var v = LVOf(m);
                        if (SameGroupV(m, v, p) || v.F == null) continue;
                        if (!FoeI(a, FI(a), v.F, v.Fi, g.Tf, g.Ti)) continue;
                        var u = LiveUnit(m);
                        if (u == null || v.Cur == t) continue;
                        float d = DaysTo(u, t);
                        if (d < 0f || d > w) continue;
                        z += u.Str; _dLiveUsed++;
                        if (d < first) first = d;
                    }
                }
            }
            for (int i = 0; i < k; i++)
            {
                if (rem != null && rem.Contains(i)) continue;
                if (g.ED[i] < first) first = g.ED[i];
                break;
            }
            for (int i = 0; i < g.AD.Count; i++)
            {
                float d = g.AD[i];
                if (d > w || d > first) continue;
                var al = g.AL[i];
                if (p != null && SameGroupV(al, LVOf(al), p)) continue;
                ally += g.AS[i];
            }
            if (((++_dChk) & 127) == 0)
            {
                float f2; double a2;
                double z2 = ZForRef(t, a, p, w, out f2, out a2);
                bool fOk = (f2 == float.MaxValue && first == float.MaxValue) || Math.Abs(f2 - first) < 1e-3f;
                if (Math.Abs(z2 - z) > 1e-3 * Math.Max(1.0, Math.Abs(z2)) || Math.Abs(a2 - ally) > 1e-3 * Math.Max(1.0, Math.Abs(a2)) || !fOk)
                {
                    _dDiv++;
                    if (_dDivNote == null) _dDivNote = t.StringId + " z " + F0(z) + "/" + F0(z2) + " sojusznicy " + F0(ally) + "/" + F0(a2);
                }
            }
            return z;
        }

        /// <summary>Wzorzec (stary przeglad listy, ten sam widok godziny) - do kontroli rozbieznosci ZFor. Wolajacy trzyma _lk.</summary>
        private static double ZForRef(Settlement t, IFaction a, MobileParty p, float w, out float first, out double ally)
        {
            first = float.MaxValue; ally = 0;
            var l = ListOf(t);
            HashSet<MobileParty> sc = p != null ? ScoutOf(p) : null;
            int ai = FI(a); var tf = t.MapFaction; int ti = FI(tf);
            double z = 0;
            List<KeyValuePair<float, float>> al = null;
            for (int i = 0; i < l.E.Count; i++)
            {
                var e = l.E[i];
                var lead = e.U.Lead;
                if (lead == null) continue;
                var v = LVOf(e.U);
                if (!v.Active || SameGroupV(lead, v, p) || v.F == null) continue;
                bool enemy = FoeI(a, ai, v.F, v.Fi, tf, ti);
                bool ours = !enemy && !War(a, ai, v.F, v.Fi) && Same(a, ai, v.F, v.Fi);
                if (!enemy && !ours) continue;
                bool live = enemy && ((sc != null && sc.Contains(lead)) || TargetsV(v, t));
                Unit u = e.U; float d = e.D;
                if (enemy && !live) d *= 2f;
                if (live || ours)
                {
                    if (ours && !GoingToV(v, t)) continue;
                    u = LiveUnit(lead);
                    if (u == null) continue;
                    d = DaysTo(u, t);
                    if (d < 0f) continue;
                }
                if (v.Cur == t) continue;
                if (d > w) continue;
                if (enemy) { z += u.Str; if (d < first) first = d; }
                else { if (al == null) al = new List<KeyValuePair<float, float>>(); al.Add(new KeyValuePair<float, float>(d, u.Str)); }
            }
            if (sc != null)
                foreach (var m in sc)
                {
                    if (l.Leads.Contains(m)) continue;
                    var v = LVOf(m);
                    if (SameGroupV(m, v, p) || v.F == null) continue;
                    if (!FoeI(a, ai, v.F, v.Fi, tf, ti)) continue;
                    var u = LiveUnit(m);
                    if (u == null || v.Cur == t) continue;
                    float d = DaysTo(u, t);
                    if (d < 0f || d > w) continue;
                    z += u.Str;
                    if (d < first) first = d;
                }
            if (al != null) foreach (var kv in al) if (kv.Key <= first) ally += kv.Value;
            return z;
        }

        /// <summary>Doby naszej drogi (jednostka na zywo). -1 - brak drogi. Wolajacy trzyma _lk.</summary>
        private static float OurDays(MobileParty p, Settlement t)
        {
            var u = LiveUnit(p);
            return u != null ? DaysTo(u, t) : -1f;
        }

        // ================================================================ czyste reguly (autotest .Test.cs)
        /// <summary>q = Z / O; O = 0 - 99 przy wrogu, 0 bez wroga.</summary>
        internal static double Ratio(double z, double o) { return o > 0 ? z / o : (z > 0 ? 99.0 : 0.0); }

        /// <summary>Mnoznik oceny oblezenia: okno x1.2, srodek x0.8, zakaz 0 (nowy cel ponad 1.0, biezacy cel marszu ponad 1.25 - histereza).</summary>
        internal static float SiegeMult(double q, bool current)
        {
            if (q <= OpenQ) return 1.2f;
            if (q <= (current ? AbandonQ : SiegeBanQ)) return 0.8f;
            return 0f;
        }

        /// <summary>Mnoznik oceny rabunku: okno x1.2, srodek x0.8, zakaz ponad 1.25.</summary>
        internal static float RaidMult(double q)
        {
            if (q <= OpenQ) return 1.2f;
            if (q <= AbandonQ) return 0.8f;
            return 0f;
        }

        /// <summary>Doby niszczenia wsi przez N ludzi - wzor gry DefaultRaidModel.CalculateHitDamage ((sqrt N + 5) / 900 na godzine) od hp (0-1).</summary>
        internal static float RaidDays(float hp, int men)
        {
            if (men < 1) men = 1;
            float perH = ((float)Math.Sqrt(men) + 5f) / 900f;
            if (!(hp > 0f)) return 0f;
            return hp / perH / CampaignTime.HoursInDay;
        }

        /// <summary>Stany oblezenia w toku: 0 brak (szturm), 1 KONTYNUUJ, 2 SRODEK, 3 KONTYNUUJ (odsiecz nie zdazy), 4 ODSTAP (kandydat), 5 GLOD (bez ingerencji).
        /// q (linia, KONTYNUUJ) liczy cala zaloge; o ODSTAP decyduje q z zaloga najwyzej 0.8 x oboz (tyle gra wycenila przy starcie - num6/num7): sama zaloga nie
        /// wywoluje odwrotu, przewage musi dac odsiecz (inaczej: odwrot, doba blokady, q bez zalogi niskie, znow oblezenie - petla). arrival - przyjazd przewagi
        /// (Arrival z ta sama baza).</summary>
        internal static int JudgeRule(double camp, double relief, double garrison, float arrival, float tLeft, float food, out double q)
        {
            q = Ratio(relief + garrison, camp);
            if (relief <= 0) return q <= OpenQ ? (food >= FoodMinDays ? 1 : 5) : 2;
            if (q <= OpenQ) return food >= FoodMinDays ? 1 : 5;
            double qr = Ratio(relief + GarCap(garrison, camp), camp);
            if (qr <= AbandonQ) return 2;
            if (arrival > tLeft) return food >= FoodMinDays ? 3 : 5;
            return 4;
        }

        /// <summary>Zaloga do oceny odwrotu: najwyzej 0.8 x oboz.</summary>
        internal static double GarCap(double garrison, double camp) { return Math.Min(garrison, OpenQ * Math.Max(0.0, camp)); }

        /// <summary>Przyjazd przewagi: najwczesniejsze doby (lista posortowana: doby, sila odsieczy), od ktorych baza + odsiecz &gt; 1.25 x oboz; float.MaxValue - nigdy.</summary>
        internal static float Arrival(List<KeyValuePair<float, float>> sorted, double baseStr, double camp)
        {
            double cum = baseStr;
            for (int i = 0; i < sorted.Count; i++)
            {
                cum += sorted[i].Value;
                if (Ratio(cum, camp) > AbandonQ) return sorted[i].Key;
            }
            return float.MaxValue;
        }

        internal static readonly string[] StateName = { "SZTURM", "KONTYNUUJ", "SRODEK", "KONTYNUUJ-NIE-ZDAZY", "ODSTAP", "GLOD" };

        /// <summary>Obrona pustych ziem: kandydaci (doby, sila) posortowani po dobach; ilu najblizszych trzeba, zeby razem >= 1.2 x napastnik w czasie t. 0 - nikt nie wraca.</summary>
        internal static int PickDefenders(List<KeyValuePair<float, float>> sorted, double attacker, float t, out double sum)
        {
            sum = 0;
            int n = 0;
            for (int i = 0; i < sorted.Count; i++)
            {
                if (sorted[i].Key > t) break;
                sum += sorted[i].Value; n++;
                if (sum >= DefNeed * attacker) return n;
            }
            return 0;
        }
    }
}
