using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>
    /// 274 - ROWNOWAGA WOJNY KANONU DO KRWAWYCH GODOW (Jeff 27.10: "lannisterowie i krolewska przystan byli minimum porownywalnie tak samo silni jak polnoc, chodzi
    /// o to zeby zadna ze stron nie uzyskala wyraznej przewagi az do krwawych godow aby walczyli i aby sily byly porownywalne dorzecze + polnoc i lannisterowie
    /// i krolewska przystan"). Klucze CanonWarBalance (glowny), CanonWarBalanceRestraint, CanonWarBalanceMuster. Projekt: docs/AUDYT-2026-10-27.md + projekt 274.
    ///
    /// DLACZEGO B (korona z Zachodem) JEST MNIEJSZA (pomiar dnia 109000, log Jeffa 26.10): to ustawienie ROT, nie ekonomia - B to jedno krolestwo z 17 rodami
    /// z lennami i 19 twierdzami, A (Polnoc + Dorzecze) 34 rody i 38 twierdz; rody rycerskie nie wystawiaja druzyn (179), druzyn na rod z lennem tyle samo
    /// (2.8-3.6), wiec pojemnosc B 13.1 tys. miejsc wobec 29.3 tys. A (0.45). Pieniadze, ludzie, straty, zalogi i Bank - wykluczone (projekt rozdz. 1).
    ///
    /// STRONY (co dobe z danych): A = battania i riverlands, ktore istnieja (po unii 272 - sama battania; dziala z 272 i bez), B = vlandia; plus kazde krolestwo
    /// w SOJUSZU gry (Kingdom.IsAllyWith) z czlonkiem strony, ktore jest w wojnie z druga strona (Reach przy koronie po smierci Renly'ego).
    /// Regula CZYNNA: klucz, kampania kanonu (Prologue.CanonHere), obie strony istnieja, kanon nie skonczony (CanonWarClock - ta sama regula co 264).
    /// MIARA: S = suma PartyBase.EstimatedStrength druzyn lordow rodow strony (z druzyna gracza) - ta sama liczba, ktora AI decyduje (p9 ArmedStrength
    /// juz w niej siedzi). Zalogi, ludzie, druzyny, armie, wypelnienie i wolni dowodcy - tylko w linii. r = S_B / S_A.
    /// PASMO (liczby z istniejacych modeli): porownywalne = 1/1.5 .. 1.5 (1.5 - "bitwa wyrownana" miary KingdomBalance 175); dzwignie wlaczaja sie po 3 dobach
    /// z rzedu poza 1.5, gasna po 3 dobach z rzedu w 1.2 (prog odsieczy gry 1.2 - OB5); 3 doby - reguly 166 i 159. Slabszy = strona z mniejszym S (co dobe).
    /// DZWIGNIE (zamknieta ekonomia - prawdziwi ludzie, prawdziwe zloto, drogi gry; pliki CanonWarBalance.Levers.cs i .Restraint.cs):
    ///  W1 werbunek do celu wypelnienia (a: podbicie wizyty we wsi / miescie strony z ochotnikami, b: najem weteranow z karczmy przez prywatna metode gry),
    ///  W2 nowa druzyna rodu na limicie (prywatne SpawnLordParty + GiveInitialItemsToParty gry - dzialaja 166, 159, E4/8), W3 kredyt - tylko raport,
    ///  W4 kompanie najemne - tylko raport. Siedziby (obie strony, zawsze do Krwawych Godow): bez nowych oblezen Winterfell, Riverrun, Blizniakow,
    ///  Casterly Rock, Krolewskiej Przystani. Powsciagliwosc silniejszego (r poza 1.5): nowe oblezenia tylko celow frontu (SCA 55). Obie strony walcza:
    ///  strona bez bitwy ani oblezenia z druga przez 7 dob - wladca zbiera armie na cel frontu, lordowie dalej niz zasieg obrony gry (8.02 doby) ida na front.
    /// ODCHYLENIA OD PROJEKTU (raport 274): (1) latki SCA (FindBestAggressiveTarget, TryKeepCommittedObjective, IsTargetOnCooldown) NIE wpiete - w zainstalowanym
    /// StrategicCampaignAI145.dll (1.4.7) AssignArmyRoles i TryRunAssignedArmyRole nie sa nigdzie wolane (martwy kod, dekompilacja 27.10); SCA dziala tylko przez
    /// model ocen celow (StrategicTargetScoreModel) - ten idzie do mysli AI, a tam stoi nasze weto (-1). Zamiast tego weto na BK BKArmyBehavior.FindArmyObjective
    /// (BK zaklada armie z celem oblezenia najblizszej twierdzy wroga w 280 jedn.). (2) Koniec kanonu: RedWeddingEvent >= 2 (nie >= 1) - jak 264 po recenzji.
    /// (3) Lord daleko od frontu dostaje PatrolAroundPoint wokol twierdzy frontu (nie DefendSettlement - obrona nieoblezonej twierdzy nie jest celem gry).
    /// (4) W2 tylko gdy slabszy ma mniej niz silniejszy / 1.2 (bez nadmiaru druzyn, ktorych nikt potem nie rozwiaze).
    /// Linie: "Rownowaga wojny (274): start sesji | ...", "Rownowaga wojny (274): dzien N | ..." (doba), zdarzenia "weto", "nowa druzyna", "najem z karczmy",
    /// "zbiorka" (raz na partie / cel i dobe), "KONIEC - Krwawe Gody ..." raz. Zapis "arm_cwb274" (ponizej 1 KB). Potkniecia liczone, bez wylacznika.
    /// </summary>
    internal static partial class CanonWarBalance
    {
        internal const string Tag = "Rownowaga wojny (274): ";
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        internal const string RiverId = "riverlands";

        // ---- liczby z istniejacych modeli (zrodlo w komentarzu)
        internal const float Band = 1.5f;              // KingdomBalance 175: bitwa wyrownana = stosunek mocy < 1.5
        internal const float CalmBand = 1.2f;          // gra / OB5: odsiecz rusza na oboz przy 1.2 x jego sile
        internal const int SwitchDays = 3;             // 166 (zwolnienia po 3 dobach ponad pulapem), 159
        internal const float MinFill = 0.6f;           // W5: cel 60% wypelnienia
        internal const float Edge = 1.05f;             // OB5 KingArmyEdge ("wladca zbiera armie")
        internal const float FrontRadius = 55f;        // SCA IsFrontline / IsEnemyFrontlineTarget
        internal const float DefendRangeDays = 8.02f;  // gra AiMilitaryBehavior.GetDistanceScoreForDefending: 2 x 4.01 doby - dalej ocena 0
        internal const float FrontPull = 1.1f;         // OB5 Prio zamku
        internal const float FloorBase = 0.5f;         // OB5 FloorBase (prog zmiany celu gry)
        internal const int IdleDays = 7;               // projekt 274: "bezczynnosc" = 7 dob bez bitwy lordow ani oblezenia z druga strona
        internal const int SpawnEveryDays = 7;         // W2: najwyzej 1 nowa druzyna rodu na 7 dob
        internal const int FamilyFloor = 5000;         // 166: kiesa rodziny - dorosly nie schodzi ponizej 5 000
        internal const int Window = 28;                // okno walki w linii (sprawdz_logi R274-3)

        // TABELA siedzib (przelacznik danych): bez nowych oblezen do Krwawych Godow, obie strony.
        internal static readonly string[] SeatIds = { "town_B2", "town_V2", CanonWarClock.TwinsId, "castle_V5", "town_EW4" };
        private static readonly string[] SeatNames = { "Winterfell", "Riverrun", "Blizniaki", "Casterly Rock", "Krolewska Przystan" };
        // recenzja 274 (1): strona domowa siedziby (wlasciciel z ROT-Map settlements.xml: Stark, Tully, Frey - A; Lannister, Krolewska Przystan - B). Weto tylko,
        // gdy siedziba jest w rekach swojej strony domowej - strona domowa zawsze moze odbic swoja siedzibe.
        private static readonly int[] SeatHome = { 1, 1, 1, 2, 2 };

        internal static bool KeyOn { get { var s = Settings.Current; return s != null && s.CanonWarBalance; } }
        private static bool RestraintOn { get { var s = Settings.Current; return s != null && s.CanonWarBalance && s.CanonWarBalanceRestraint; } }
        private static bool MusterOn { get { var s = Settings.Current; return s != null && s.CanonWarBalance && s.CanonWarBalanceMuster; } }

        // ================================================================ stan zapisany (arm_cwb274)
        private static int _lastFightA = -1, _lastFightB = -1;   // doba ostatniej bitwy lordow albo oblezenia z druga strona
        private static int _weak;                                 // 0 brak, 1 A, 2 B (dzwignie)
        private static int _streakOn, _streakOff, _leverSince = -1, _spawned, _hired, _fortsA0 = -1, _fortsB0 = -1, _startDay = -1, _endDay = -1;
        private static bool _levers;
        private static long _goldHired;
        private static readonly Dictionary<string, int> _clanSpawnDay = new Dictionary<string, int>(StringComparer.Ordinal);
        // recenzja 274 (3): doba ostatniej walki z druga strona NA KROLESTWO (druzyny lordow AI tego krolestwa) - zbiorka liczy bezczynnosc krolestwa, nie koalicji
        private static readonly Dictionary<string, int> _lastFightK = new Dictionary<string, int>(StringComparer.Ordinal);
        // okno walki 28 dob: [dzien % 28] -> bitwy A, oblezenia A, bitwy B, oblezenia B (z zapisem)
        private static readonly int[,] _ring = new int[Window, 4];
        private static readonly int[] _ringDay = new int[Window];

        // ================================================================ stan dnia (bez zapisu)
        private static bool _active;                              // regula czynna (doba; mysl AI pyta jeszcze CanonWarClock.OverNow)
        private static int _restrain;                             // strona powsciagana (silniejsza poza 1.5) albo 0
        private static float _target = 1f;                        // W1: cel wypelnienia slabszego
        private static double _ratio = 1.0;
        private static readonly Dictionary<Kingdom, int> _side = new Dictionary<Kingdom, int>();
        private static readonly HashSet<Settlement> _seats = new HashSet<Settlement>();
        private static readonly Dictionary<Settlement, int> _seatHome = new Dictionary<Settlement, int>();
        private static readonly List<string> _coreNoWar = new List<string>();      // recenzja 274 (M2): krolestwa rdzenia bez wojny z druga strona (poza stronami)
        private static readonly Dictionary<Kingdom, HashSet<Settlement>> _frontTargets = new Dictionary<Kingdom, HashSet<Settlement>>();
        private static readonly List<Settlement>[] _frontOwn = { new List<Settlement>(), new List<Settlement>(), new List<Settlement>() };
        private static readonly HashSet<Clan> _homeGuard = new HashSet<Clan>();
        private static int _stumbles, _dStumbles;
        // liczniki doby (linia)
        private static int _dBoost, _dHireN, _dHireMen, _dVetoSeat, _dVetoFront, _dVetoBk, _dMusterKing, _dMusterLords, _dSpawnBlock;
        private static long _dHireGold;
        private static readonly int[] _dBattles = new int[3], _dSieges = new int[3];
        private static readonly List<string> _dSpawnNames = new List<string>();
        private static readonly HashSet<string> _dSaid = new HashSet<string>(StringComparer.Ordinal);
        private static string _musterKingNote = "-";

        private static Settings S { get { return Settings.Current; } }
        private static int Today { get { return (int)CampaignTime.Now.ToDays; } }

        internal static void Reset()
        {
            _lastFightA = _lastFightB = -1; _weak = 0; _streakOn = _streakOff = 0; _leverSince = -1; _spawned = _hired = 0; _fortsA0 = _fortsB0 = -1; _startDay = -1; _endDay = -1;
            _levers = false; _goldHired = 0; _clanSpawnDay.Clear(); _lastFightK.Clear();
            Array.Clear(_ring, 0, _ring.Length); for (int i = 0; i < Window; i++) _ringDay[i] = -1;
            _active = false; _restrain = 0; _target = 1f; _ratio = 1.0;
            _side.Clear(); _seats.Clear(); _seatHome.Clear(); _coreNoWar.Clear(); _frontTargets.Clear(); for (int i = 0; i < 3; i++) _frontOwn[i].Clear(); _homeGuard.Clear();
            _stumbles = _dStumbles = 0;
            ZeroDay();
            _farCache.Clear();
            CanonWarClock.Reset();
            CanonWarBalanceTest.Reset();
        }

        private static void ZeroDay()
        {
            _dBoost = _dHireN = _dHireMen = _dVetoSeat = _dVetoFront = _dVetoBk = _dMusterKing = _dMusterLords = _dSpawnBlock = 0; _dHireGold = 0;
            for (int i = 0; i < 3; i++) { _dBattles[i] = 0; _dSieges[i] = 0; }
            _dSpawnNames.Clear(); _dSaid.Clear(); _musterKingNote = "-"; _dStumbles = 0;
        }

        internal static void Stumble(string where, Exception e)
        {
            _stumbles++; _dStumbles++;
            if (_stumbles <= 5) { try { Log.Error("CanonWarBalance." + where + " (potkniecie " + _stumbles + ")", e); } catch { } }
        }

        private static string Nm(IFaction f) { try { return f != null && f.Name != null ? f.Name.ToString() : "-"; } catch { return "?"; } }
        private static string Nm(Settlement st) { try { return st != null && st.Name != null ? st.Name.ToString() : "-"; } catch { return "?"; } }
        private static string Nm(Hero h) { try { return h != null && h.Name != null ? h.Name.ToString() : "-"; } catch { return "?"; } }
        private static string Nm(MobileParty mp) { try { return mp != null && mp.Name != null ? mp.Name.ToString() : "-"; } catch { return "?"; } }
        private static string Nm(Clan c) { try { return c != null && c.Name != null ? c.Name.ToString() : "-"; } catch { return "?"; } }
        private static string F2(double v) { return v.ToString("0.00", Inv); }
        private static string F0(double v) { return v.ToString("0", Inv); }
        private static string SideName(int side) { return side == 1 ? "A" : side == 2 ? "B" : "-"; }

        // ================================================================ czyste funkcje (autotest T1-T3, T5)

        /// <summary>Strona krolestwa z tabeli rdzenia: battania / riverlands - A (1), vlandia - B (2), reszta 0. Zniszczone - 0.</summary>
        internal static int CoreSide(string id, bool eliminated)
        {
            if (eliminated || id == null) return 0;
            if (id == Prologue.NorthId || id == RiverId) return 1;
            if (id == Prologue.Crown) return 2;
            return 0;
        }

        /// <summary>Sojusznik strony: w sojuszu gry z czlonkiem jednej strony (i nie drugiej) i w wojnie z druga. 0 - poza wojna kanonu.</summary>
        internal static int AllySide(bool allyOfA, bool allyOfB, bool warWithA, bool warWithB)
        {
            if (allyOfA && !allyOfB && warWithB) return 1;
            if (allyOfB && !allyOfA && warWithA) return 2;
            return 0;
        }

        /// <summary>Regula czynna (T5): klucz, kampania kanonu, obie strony, kanon nie skonczony.</summary>
        internal static bool Active(bool key, bool canon, bool sidesExist, bool canonOver) { return key && canon && sidesExist && !canonOver; }

        /// <summary>Krok przelacznika dzwigni (T1): r = S_B / S_A. Poza 1.5 przez 3 doby - wlacz; w 1.2 przez 3 doby - wylacz. Slabszy co dobe = mniejsze S.</summary>
        internal static void Step(double r, ref bool on, ref int weak, ref int sOn, ref int sOff)
        {
            bool outside = r < 1.0 / Band || r > Band;
            bool calm = r >= 1.0 / CalmBand && r <= CalmBand;
            sOn = outside ? sOn + 1 : 0;
            sOff = calm ? sOff + 1 : 0;
            if (!on && sOn >= SwitchDays) on = true;
            else if (on && sOff >= SwitchDays) on = false;
            weak = on ? (r < 1.0 ? 2 : r > 1.0 ? 1 : 0) : 0;
        }

        /// <summary>W1: cel wypelnienia slabszego = min(1, wypelnienie x (S_silny / 1.2) / S_slaby), nie mniej niz 60% (W5).</summary>
        internal static float TargetFill(double fillWeak, double sStrong, double sWeak)
        {
            if (sWeak <= 0.0) return 1f;
            double t = fillWeak * (sStrong / CalmBand) / sWeak;
            if (double.IsNaN(t)) t = 1.0;
            return (float)Math.Max(MinFill, Math.Min(1.0, t));
        }

        /// <summary>Weto oblezenia (T3): 0 - bez weta, 1 - siedziba, 2 - za frontem (silniejszy powsciagany). Tylko cel drugiej strony; trwajace oblezenie - bez weta.
        /// seat - siedziba w rekach swojej strony domowej; retake - oblega strona domowa siedziby (odbicie, recenzja 274 (1)) - bez weta, takze przy powsciagliwosci.</summary>
        internal static int VetoKind(bool otherSide, bool running, bool seat, bool restrained, bool frontTarget, bool retake = false)
        {
            if (!otherSide || running || retake) return 0;
            if (seat) return 1;
            if (restrained && !frontTarget) return 2;
            return 0;
        }

        // ================================================================ strony i mapa (raz na dobe)

        /// <summary>Strona krolestwa dzis (0 - poza wojna kanonu). Mysl AI - jedno wyszukanie w slowniku.</summary>
        internal static int SideOf(IFaction f)
        {
            var k = f as Kingdom;
            int s;
            return k != null && _side.TryGetValue(k, out s) ? s : 0;
        }

        private static void BuildSides()
        {
            _side.Clear(); _coreNoWar.Clear();
            var cand = new List<Kingdom>(); var candSide = new List<int>();
            foreach (var k in Kingdom.All)
            {
                if (k == null) continue;
                int s = CoreSide(k.StringId, k.IsEliminated);
                if (s != 0) { cand.Add(k); candSide.Add(s); }
            }
            // recenzja 274 (M2): krolestwo rdzenia liczy sie do strony tylko w wojnie z co najmniej jednym krolestwem rdzenia drugiej strony (jak sojusznik w AllySide) -
            // Polnoc w pokoju z korona nie zawyza S_A, a dzwignie nie ida dla B przeciw krolestwu, ktore samo walczy
            var core = new List<Kingdom>();
            for (int i = 0; i < cand.Count; i++)
            {
                bool war = false;
                for (int j = 0; j < cand.Count && !war; j++)
                {
                    if (candSide[j] == candSide[i]) continue;
                    try { war = cand[i].IsAtWarWith(cand[j]); } catch { war = false; }
                }
                if (war) { _side[cand[i]] = candSide[i]; core.Add(cand[i]); }
                else _coreNoWar.Add(Nm(cand[i]) + " (" + SideName(candSide[i]) + ")");
            }
            foreach (var k in Kingdom.All)
            {
                if (k == null || k.IsEliminated || _side.ContainsKey(k)) continue;
                bool allyA = false, allyB = false, warA = false, warB = false;
                for (int i = 0; i < core.Count; i++)
                {
                    var c = core[i]; int s = _side[c];
                    bool ally = false, war = false;
                    try { ally = k.IsAllyWith(c); } catch { }
                    try { war = k.IsAtWarWith(c); } catch { }
                    if (s == 1) { allyA |= ally; warA |= war; } else { allyB |= ally; warB |= war; }
                }
                int side = AllySide(allyA, allyB, warA, warB);
                if (side != 0) _side[k] = side;
            }
        }

        private static string SideList(int side)
        {
            var l = new List<string>();
            foreach (var kv in _side) if (kv.Value == side) l.Add(Nm(kv.Key));
            l.Sort(StringComparer.Ordinal);
            return "[" + string.Join(", ", l.ToArray()) + "]";
        }

        private static bool HasSide(int side) { foreach (var kv in _side) if (kv.Value == side) return true; return false; }

        /// <summary>Siedziby, cele frontu (twierdze drugiej strony w 55 od twierdzy przyjaznej danemu krolestwu - SCA IsEnemyFrontlineTarget), twierdze frontu
        /// stron (wlasne w 55 od twierdzy drugiej strony) i rody "stoja w domu" (twierdza w BorderGarrisonRadius od twierdzy trzeciego wroga - R3-08g).</summary>
        private static void BuildMap()
        {
            _seats.Clear(); _seatHome.Clear(); _frontTargets.Clear(); for (int i = 0; i < 3; i++) _frontOwn[i].Clear(); _homeGuard.Clear();
            for (int i = 0; i < SeatIds.Length; i++) { try { var st = Settlement.Find(SeatIds[i]); if (st != null) { _seats.Add(st); _seatHome[st] = SeatHome[i]; } } catch { } }
            var forts = new List<Settlement>(256);
            foreach (var t in Town.AllFiefs) { var st = t != null ? t.Settlement : null; if (st != null && st.MapFaction != null) forts.Add(st); }
            float r2 = FrontRadius * FrontRadius;
            int n = forts.Count;
            var side = new int[n];
            var pos = new Vec2[n];
            for (int i = 0; i < n; i++) { side[i] = SideOf(forts[i].MapFaction); pos[i] = forts[i].GetPosition2D; }
            // twierdze frontu stron
            for (int i = 0; i < n; i++)
            {
                if (side[i] == 0) continue;
                for (int j = 0; j < n; j++)
                {
                    if (side[j] == 0 || side[j] == side[i]) continue;
                    if (pos[i].DistanceSquared(pos[j]) > r2) continue;
                    _frontOwn[side[i]].Add(forts[i]);
                    break;
                }
            }
            // cele frontu na krolestwo strony: twierdza drugiej strony z przyjazna (nie w wojnie z k) twierdza w 55 - jak SCA
            foreach (var kv in _side)
            {
                var k = kv.Key; int s = kv.Value;
                var set = new HashSet<Settlement>();
                for (int i = 0; i < n; i++)
                {
                    if (side[i] == 0 || side[i] == s) continue;
                    for (int j = 0; j < n; j++)
                    {
                        if (j == i || pos[i].DistanceSquared(pos[j]) > r2) continue;   // odleglosc najpierw (tanio), potem stosunek frakcji
                        var f = forts[j].MapFaction;
                        bool friendly;
                        try { friendly = f == k || !k.IsAtWarWith(f); } catch { friendly = false; }
                        if (!friendly) continue;
                        set.Add(forts[i]); break;
                    }
                }
                _frontTargets[k] = set;
            }
            // rody przy trzecim wrogu (Zelazne Wyspy, Wolni Ludzie...) - zostaja w domu przy zbiorce
            var sset = S;
            float br = sset != null ? Math.Max(0f, sset.BorderGarrisonRadius) : 60f;
            float b2 = br * br;
            for (int i = 0; i < n; i++)
            {
                if (side[i] == 0) continue;
                var own = forts[i].MapFaction;
                var clan = forts[i].OwnerClan;
                if (clan == null || _homeGuard.Contains(clan)) continue;
                for (int j = 0; j < n; j++)
                {
                    if (j == i || side[j] != 0 || pos[i].DistanceSquared(pos[j]) > b2) continue;
                    var f = forts[j].MapFaction;
                    bool war;
                    try { war = f != null && own != null && own.IsAtWarWith(f); } catch { war = false; }
                    if (!war) continue;
                    _homeGuard.Add(clan); break;
                }
            }
        }

        internal static bool IsSeat(Settlement st) { return st != null && _seats.Contains(st); }

        /// <summary>Strona domowa siedziby (1 A, 2 B) albo 0 - nie siedziba.</summary>
        internal static int SeatHomeOf(Settlement st) { int h; return st != null && _seatHome.TryGetValue(st, out h) ? h : 0; }

        /// <summary>Recenzja 274 (1): siedziba w rekach swojej strony domowej - tylko taka ma weto (zdobyta przez druga strone moze byc odbita).</summary>
        internal static bool SeatHeld(Settlement st) { int h = SeatHomeOf(st); return h != 0 && SideOf(st.MapFaction) == h; }

        /// <summary>Regula czynna teraz (po Daily albo przeliczeniu po wczytaniu) - T4 autotestu.</summary>
        internal static bool IsActiveNow { get { return _active; } }
        /// <summary>277 (linia doby, tylko odczyt): stosunek S_B / S_A z ostatniego liczenia 274 (1.0 - jeszcze nie liczony).</summary>
        internal static double LastRatio { get { return _ratio; } }

        internal static bool IsFrontTarget(Kingdom k, Settlement st)
        {
            HashSet<Settlement> set;
            return k != null && st != null && _frontTargets.TryGetValue(k, out set) && set.Contains(st);
        }

        // ================================================================ miara
        private sealed class M
        {
            public double S; public int Men, Parties, Armies, Limit, Gar, Idle, Forts, LandedClans;
            public double Fill { get { return Limit > 0 ? (double)Men / Limit : 0.0; } }
            public double Bank; public int BankClans, Debtors; public long Gold;
        }
        private static readonly M[] _m = { new M(), new M(), new M() };

        private static bool Landed(Clan c) { return c != null && !c.IsEliminated && c.Fiefs != null && c.Fiefs.Count > 0; }

        private static void Measure()
        {
            for (int i = 0; i < 3; i++) _m[i] = new M();
            foreach (var mp in MobileParty.AllLordParties)
            {
                if (mp == null || !mp.IsActive || mp.Party == null) continue;
                int s = SideOf(mp.MapFaction);
                if (s == 0) continue;
                var m = _m[s];
                m.S += mp.Party.EstimatedStrength;
                m.Men += mp.Party.NumberOfAllMembers;
                m.Limit += mp.Party.PartySizeLimit;
                m.Parties++;
            }
            var main = MobileParty.MainParty;
            if (main != null && main.IsActive && main.Party != null && !main.IsLordParty)
            {
                int s = SideOf(main.MapFaction);
                if (s != 0) { var m = _m[s]; m.S += main.Party.EstimatedStrength; m.Men += main.Party.NumberOfAllMembers; m.Limit += main.Party.PartySizeLimit; m.Parties++; }
            }
            int age = 18;
            try { age = Campaign.Current.Models.AgeModel.HeroComesOfAge; } catch { }
            foreach (var kv in _side)
            {
                var k = kv.Key; var m = _m[kv.Value];
                try { m.Armies += k.Armies.Count; } catch { }
                foreach (var st in k.Settlements)
                {
                    if (st == null || !st.IsFortification || st.Town == null) continue;
                    m.Forts++;
                    var g = st.Town.GarrisonParty;
                    if (g != null && g.MemberRoster != null) m.Gar += g.MemberRoster.TotalManCount;
                }
                foreach (var c in k.Clans)
                {
                    if (c == null || c.IsEliminated) continue;
                    try { m.Gold += c.Gold; } catch { }
                    try { double p; int missed; bool def; int due; if (IronBank.TryGetDebt(c, out p, out missed, out def, out due) && p > 0) { m.Bank += p; m.BankClans++; } } catch { }
                    try { if (DebtLadder.IsDebtor(c)) m.Debtors++; } catch { }
                    if (!Landed(c)) continue;
                    m.LandedClans++;
                    foreach (var h in c.Heroes)
                    {
                        if (h == null || !h.IsActive || !h.IsAlive || h.PartyBelongedTo != null || h.PartyBelongedToAsPrisoner != null || h.IsPrisoner) continue;
                        if (h.Age <= age || h.CharacterObject == null || h.CharacterObject.Occupation != Occupation.Lord) continue;
                        bool lead; try { lead = h.CanLeadParty(); } catch { lead = false; }
                        if (lead) m.Idle++;
                    }
                }
            }
        }

        // ================================================================ zdarzenia (bitwy i oblezenia stron)

        private static int Slot(int day) { int i = day % Window; if (i < 0) i += Window; if (_ringDay[i] != day) { _ringDay[i] = day; for (int c = 0; c < 4; c++) _ring[i, c] = 0; } return i; }

        private static void NoteFight(int side, bool siege)
        {
            int d = Today;
            if (side == 1) _lastFightA = d; else if (side == 2) _lastFightB = d; else return;
            int i = Slot(d);
            _ring[i, (side - 1) * 2 + (siege ? 1 : 0)]++;
            if (siege) _dSieges[side]++; else _dBattles[side]++;
        }

        /// <summary>Recenzja 274 (3): doba walki krolestwa druzyny lorda AI strony (druzyna gracza nie liczy sie - zbiorka dotyczy lordow AI).</summary>
        private static void NoteKingdom(MobileParty mp)
        {
            if (mp == null || mp.IsMainParty || !mp.IsLordParty) return;
            var k = mp.MapFaction as Kingdom;
            if (k == null || k.StringId == null || SideOf(k) == 0) return;
            _lastFightK[k.StringId] = Today;
        }

        private static void NoteKingdoms(MapEventSide sd)
        {
            try { foreach (var p in sd.Parties) if (p != null && p.Party != null && p.Party.IsMobile) NoteKingdom(p.Party.MobileParty); }
            catch { }
        }

        private static bool HasLord(MapEventSide sd)
        {
            try { foreach (var p in sd.Parties) if (p != null && p.Party != null && p.Party.IsMobile && p.Party.MobileParty != null && p.Party.MobileParty.IsLordParty) return true; }
            catch { }
            return false;
        }

        /// <summary>CampaignEvents.MapEventEnded: bitwa lordow strony A ze strona B (bez najazdow) - doba walki obu stron.</summary>
        internal static void OnMapEventEnded(MapEvent me)
        {
            try
            {
                if (!_active || me == null || me.IsRaid) return;
                var a = me.AttackerSide; var d = me.DefenderSide;
                if (a == null || d == null || a.LeaderParty == null || d.LeaderParty == null) return;
                int sa = SideOf(a.LeaderParty.MapFaction), sd = SideOf(d.LeaderParty.MapFaction);
                if (sa == 0 || sd == 0 || sa == sd) return;
                if (!HasLord(a) || !HasLord(d)) return;
                NoteFight(1, false); NoteFight(2, false);
                NoteKingdoms(a); NoteKingdoms(d);
            }
            catch (Exception e) { Stumble("OnMapEventEnded", e); }
        }

        /// <summary>CampaignEvents.OnSiegeEventStartedEvent: oblezenie twierdzy drugiej strony - doba walki strony oblegajacej; siedziba - linia (R274-4).</summary>
        internal static void OnSiegeStarted(SiegeEvent ev)
        {
            try
            {
                if (!_active || ev == null || ev.BesiegedSettlement == null || ev.BesiegerCamp == null || ev.BesiegerCamp.LeaderParty == null) return;
                var st = ev.BesiegedSettlement;
                int sb = SideOf(ev.BesiegerCamp.LeaderParty.MapFaction), ss = SideOf(st.MapFaction);
                if (sb == 0 || ss == 0 || sb == ss) return;
                NoteFight(sb, true);
                var lead = ev.BesiegerCamp.LeaderParty;
                NoteKingdom(lead);
                if (lead != null && lead.Army != null) { try { foreach (var mp in lead.Army.Parties) if (mp != lead) NoteKingdom(mp); } catch { } }
                if (IsSeat(st))
                {
                    string head = Nm(st) + " (" + st.StringId + ", " + Nm(st.MapFaction) + ") oblega " + Nm(ev.BesiegerCamp.LeaderParty) + " (" + Nm(ev.BesiegerCamp.LeaderParty.MapFaction);
                    // recenzja 274 (M3): druzyna gracza nie ma weta (jego wybor); recenzja 274 (1): odbicie wlasnej siedziby przez strone domowa - bez weta
                    if (lead != null && lead.IsMainParty)
                        Log.Info(Tag + "siedziba bez weta | dzien " + Today + " | " + head + ", strona " + SideName(sb) + ") - druzyna gracza, weto tylko dla AI (wybor gracza).");
                    else if (SeatHomeOf(st) == sb)
                        Log.Info(Tag + "siedziba bez weta | dzien " + Today + " | " + head + ", strona domowa " + SideName(sb) + ") - odbicie wlasnej siedziby, bez weta.");
                    else
                        Log.Info(Tag + "OBLEZENIE SIEDZIBY | dzien " + Today + " | " + head + ") - weto nie zadzialalo (droga spoza mysli AI i BK).");
                }
            }
            catch (Exception e) { Stumble("OnSiegeStarted", e); }
        }

        private static int Count28(int side, bool siege)
        {
            int n = 0, d = Today;
            for (int i = 0; i < Window; i++) if (_ringDay[i] > d - Window && _ringDay[i] <= d) n += _ring[i, (side - 1) * 2 + (siege ? 1 : 0)];
            return n;
        }

        private static int IdleOf(int side, int today)
        {
            int last = side == 1 ? _lastFightA : _lastFightB;
            if (last < 0) last = _startDay >= 0 ? _startDay : today;
            return Math.Max(0, today - last);
        }

        internal static bool Idle(int side) { return IdleOf(side, Today) >= IdleDays; }

        /// <summary>Recenzja 274 (3): doby bez walki krolestwa (jego lordow AI) z druga strona; brak wpisu - od startu reguly.</summary>
        private static int IdleOfK(Kingdom k, int today)
        {
            int last;
            if (k == null || k.StringId == null || !_lastFightK.TryGetValue(k.StringId, out last) || last < 0) last = _startDay >= 0 ? _startDay : today;
            return Math.Max(0, today - last);
        }

        /// <summary>Zbiorka: krolestwo bez walki 7 dob - takze gdy sojusznik z koalicji walczy (Polnoc stoi, Dorzecze walczy).</summary>
        internal static bool IdleK(Kingdom k) { return IdleOfK(k, Today) >= IdleDays; }

        private static string KingdomIdleText(int today)
        {
            var l = new List<string>();
            foreach (var kv in _side) l.Add(Nm(kv.Key) + " " + IdleOfK(kv.Key, today));
            l.Sort(StringComparer.Ordinal);
            return "[" + string.Join(", ", l.ToArray()) + "]";
        }

        // ================================================================ doba

        /// <summary>Raz na dobe (ArmouryBehavior, po KingdomBalance.Daily - miara 175 i p9 ArmedStrength opublikowane): strony, miara, przelacznik, W2, linia.</summary>
        internal static void Daily()
        {
            int today = Today;
            try
            {
                _farCache.Clear();   // pamiec zbiorki (6 h) - bez martwych partii przez kampanie
                var s = S;
                if (s == null) return;
                string off = null;
                if (!s.CanonWarBalance) off = "klucz Canon War Balance NIE";
                else if (!Prologue.CanonHere) off = "kampania klasyczna - nie dotyczy";
                BuildSides();
                bool sides = HasSide(1) && HasSide(2);
                int rw = CanonWarClock.RedWeddingState; bool robbDead = CanonWarClock.RobbDead;
                bool over = CanonWarClock.Over(rw, robbDead);
                if (off == null && !sides) off = "strona nie istnieje (A " + SideList(1) + ", B " + SideList(2) + NoWarText() + ")";
                if (off == null && over)
                {
                    if (_endDay < 0)
                    {
                        _endDay = today;
                        Log.Info(Tag + "KONIEC - Krwawe Gody | dzien " + today + " | RedWeddingEvent " + rw + ", Robb " + (robbDead ? "nie zyje" : "zyje")
                                 + " - dzwignie, weta siedzib, powsciagliwosc i zbiorka wylaczone na zawsze (druzyny W2 zostaja; " + _spawned + " nowych, najetych z karczmy " + _hired + ").");
                    }
                    off = "Krwawe Gody od dnia " + _endDay + " (RedWeddingEvent " + rw + ", Robb " + (robbDead ? "nie zyje" : "zyje") + ")";
                }
                _active = Active(s.CanonWarBalance, Prologue.CanonHere, sides, over);
                if (!_active)
                {
                    _levers = false; _weak = 0; _restrain = 0;
                    if (s.LogEnabled) Log.Info(Tag + "dzien " + today + " | wylaczone (" + off + ") | potkniecia " + _dStumbles + " (sesja " + _stumbles + ").");
                    ZeroDay();
                    return;
                }
                if (_startDay < 0) _startDay = today;
                foreach (var c in _coreNoWar)
                    if (Say("rdzen|" + c)) Log.Info(Tag + "rdzen bez wojny | dzien " + today + " | " + c + " nie jest w wojnie z zadnym krolestwem rdzenia drugiej strony - poza miara, dzwigniami, wetami i zbiorka do wznowienia wojny.");
                BuildMap();
                Measure();
                var A = _m[1]; var B = _m[2];
                if (_fortsA0 < 0) _fortsA0 = A.Forts;
                if (_fortsB0 < 0) _fortsB0 = B.Forts;
                _ratio = A.S > 0 ? B.S / A.S : (B.S > 0 ? 99.0 : 1.0);
                bool was = _levers;
                Step(_ratio, ref _levers, ref _weak, ref _streakOn, ref _streakOff);
                if (_levers && !was) _leverSince = today;
                if (!_levers) _leverSince = -1;
                bool outside = _ratio < 1.0 / Band || _ratio > Band;
                _restrain = _levers && outside && s.CanonWarBalanceRestraint ? (_weak == 1 ? 2 : _weak == 2 ? 1 : 0) : 0;
                var W = _weak == 1 ? A : _weak == 2 ? B : null;
                var X = _weak == 1 ? B : _weak == 2 ? A : null;
                _target = W != null ? TargetFill(W.Fill, X.S, W.S) : 1f;
                if (_levers && W != null && W.S < X.S / CalmBand) SpawnDaily(_weak, today);
                DayLine(today, rw, robbDead, outside);
            }
            catch (Exception e) { Stumble("Daily", e); }
            finally { ZeroDay(); }
        }

        private static string NoWarText() { return _coreNoWar.Count > 0 ? "; rdzen bez wojny z druga strona: " + string.Join(", ", _coreNoWar.ToArray()) : ""; }

        /// <summary>Recenzja 274 (2): po wczytaniu (EnsureHooks, po Import i BuildSides) - stan doby bez czekania na polnoc: czynnosc reguly, siedziby, front, miara,
        /// powsciagliwosc z zapisanych dzwigni (_levers, _weak). Bez Step, SpawnDaily i linii KONIEC (serie dob nie rosna drugi raz). Linia "po wczytaniu".</summary>
        private static void RefreshAfterLoad()
        {
            var s = S;
            if (s == null) return;
            int today = Today;
            bool sides = HasSide(1) && HasSide(2);
            int rw = CanonWarClock.RedWeddingState; bool robbDead = CanonWarClock.RobbDead;
            bool over = CanonWarClock.Over(rw, robbDead);
            _active = Active(s.CanonWarBalance, Prologue.CanonHere, sides, over);
            if (!_active)
            {
                _restrain = 0;
                Log.Info(Tag + "po wczytaniu | dzien " + today + " | regula NIE (" + (!s.CanonWarBalance ? "klucz NIE" : !Prologue.CanonHere ? "kampania klasyczna - nie dotyczy"
                         : !sides ? "strona nie istnieje" + NoWarText() : "Krwawe Gody") + ") - weta, dzwignie i zbiorka nieczynne.");
                return;
            }
            BuildMap();
            Measure();
            var A = _m[1]; var B = _m[2];
            _ratio = A.S > 0 ? B.S / A.S : (B.S > 0 ? 99.0 : 1.0);
            bool outside = _ratio < 1.0 / Band || _ratio > Band;
            _restrain = _levers && outside && s.CanonWarBalanceRestraint ? (_weak == 1 ? 2 : _weak == 2 ? 1 : 0) : 0;
            var W = _weak == 1 ? A : _weak == 2 ? B : null;
            var X = _weak == 1 ? B : _weak == 2 ? A : null;
            _target = W != null ? TargetFill(W.Fill, X.S, W.S) : 1f;
            int fronts = 0; foreach (var kv in _frontTargets) fronts += kv.Value.Count;
            Log.Info(Tag + "po wczytaniu | dzien " + today + " | regula TAK | siedziby " + _seats.Count + ", cele frontu " + fronts + ", stosunek B/A " + _ratio.ToString("0.000", Inv)
                     + ", dzwignie z zapisu " + (_levers ? "TAK, slabszy " + SideName(_weak) : "NIE") + ", powsciagany " + (_restrain == 0 ? "-" : SideName(_restrain))
                     + " | bezczynnosc krolestw " + KingdomIdleText(today) + NoWarText() + " - weta i zbiorka czynne od teraz, nie od polnocy.");
        }

        private static string SideText(M m)
        {
            return "sila " + F0(m.S) + " (ludzie " + m.Men + ", partii " + m.Parties + ", armii " + m.Armies + ", wypelnienie " + F2(m.Fill) + ", limit " + m.Limit
                   + ", zalogi " + m.Gar + ", wolni dowodcy " + m.Idle + ")";
        }

        private static void DayLine(int today, int rw, bool robbDead, bool outside)
        {
            var s = S;
            if (s == null || !s.LogEnabled) return;
            var A = _m[1]; var B = _m[2];
            var sb = new StringBuilder(1024);
            sb.Append(Tag).Append("dzien ").Append(today)
              .Append(" | A ").Append(SideText(A)).Append(" | B ").Append(SideText(B))
              .Append(" | stosunek B/A ").Append(_ratio.ToString("0.000", Inv)).Append(" (pasmo ").Append(F2(1.0 / Band)).Append('-').Append(F2(Band))
              .Append("; poza pasmem ").Append(_streakOn).Append(" dob, w 1.2 ").Append(_streakOff).Append(" dob; dzwignie ")
              .Append(_levers ? "TAK od dnia " + _leverSince + ", slabszy " + SideName(_weak) : "NIE")
              .Append("; powsciagany ").Append(_restrain == 0 ? "-" : SideName(_restrain)).Append(')');
            sb.Append(" | walka dzis: A bitwy ").Append(_dBattles[1]).Append(" oblezenia ").Append(_dSieges[1]).Append(", B bitwy ").Append(_dBattles[2]).Append(" oblezenia ").Append(_dSieges[2])
              .Append("; 28 dob: A bitwy ").Append(Count28(1, false)).Append(" oblezenia ").Append(Count28(1, true)).Append(", B bitwy ").Append(Count28(2, false)).Append(" oblezenia ").Append(Count28(2, true))
              .Append("; bezczynnosc A ").Append(IdleOf(1, today)).Append(" dob, B ").Append(IdleOf(2, today)).Append(" dob")
              .Append("; krolestwa ").Append(KingdomIdleText(today));
            sb.Append(" | twierdze A ").Append(A.Forts).Append(" (start ").Append(_fortsA0).Append(") B ").Append(B.Forts).Append(" (start ").Append(_fortsB0).Append(')')
              .Append(", front A ").Append(_frontOwn[1].Count).Append(" B ").Append(_frontOwn[2].Count)
              .Append(", rody w domu (trzeci wrog) A ").Append(HomeGuard(1)).Append(" B ").Append(HomeGuard(2));
            sb.Append(" | dzwignie: werbunek ").Append(_levers && _weak != 0 ? SideName(_weak) + " cel " + F2(_target) : "-")
              .Append(" (podbic wizyt ").Append(_dBoost).Append(", karczma najmow ").Append(_dHireN).Append(" ludzi ").Append(_dHireMen).Append(" za ").Append(_dHireGold).Append(" zl)")
              .Append(", nowe druzyny ").Append(_dSpawnNames.Count).Append(_dSpawnNames.Count > 0 ? " [" + string.Join(", ", _dSpawnNames.ToArray()) + "]" : "")
              .Append(" (blokada 166 ").Append(_dSpawnBlock).Append(')')
              .Append(", weta oblezen: siedziby ").Append(_dVetoSeat).Append(" / za frontem ").Append(_dVetoFront).Append(", BK cel zdjety ").Append(_dVetoBk)
              .Append(", zbiorka: wladca ").Append(_dMusterKing).Append(' ').Append(_musterKingNote).Append(", lordow do frontu ").Append(_dMusterLords)
              .Append("; razem najetych ").Append(_hired).Append(" za ").Append(_goldHired).Append(" zl, nowych druzyn ").Append(_spawned);
            sb.Append(" | kredyt: A Bank ").Append(F0(A.Bank)).Append(" (rodow ").Append(A.BankClans).Append("), dluznicy 168 ").Append(A.Debtors).Append(", zloto rodow ").Append(A.Gold)
              .Append("; B Bank ").Append(F0(B.Bank)).Append(" (rodow ").Append(B.BankClans).Append("), dluznicy 168 ").Append(B.Debtors).Append(", zloto rodow ").Append(B.Gold)
              .Append(" | kompanie najemne wolne ").Append(FreeMercs());
            sb.Append(" | warunki Krwawych Godow ROT: ").Append(CanonWarClock.Conditions()).Append(" (RedWeddingEvent ").Append(rw).Append(')')
              .Append(" | koalicje A=").Append(SideList(1)).Append(" B=").Append(SideList(2))
              .Append(" | potkniecia ").Append(_dStumbles).Append(" (sesja ").Append(_stumbles).Append(").");
            Log.Info(sb.ToString());
        }

        private static int HomeGuard(int side)
        {
            int n = 0;
            foreach (var c in _homeGuard) if (c != null && SideOf(c.MapFaction) == side) n++;
            return n;
        }

        /// <summary>W4 (tylko raport): rody najemne bez sluzby (nie w krolestwie), ktore nie sa zniszczone.</summary>
        private static int FreeMercs()
        {
            int n = 0;
            try { foreach (var c in Clan.All) if (c != null && !c.IsEliminated && c.IsMinorFaction && c.IsClanTypeMercenary && c.Kingdom == null && !c.IsBanditFaction) n++; }
            catch { }
            return n;
        }

        // ================================================================ latki i start sesji
        private static Harmony _harmony;
        private static bool _hooksTried;
        private static string _wBoost = "BRAK", _wVeto = "BRAK", _wBk = "BRAK", _wHire = "BRAK", _wSpawn = "BRAK";

        internal static void SetHarmony(Harmony h) { _harmony = h; }

        /// <summary>Raz na proces w kampanii (ArmouryBehavior): mysl AI (podbicia 250, weta 50), BK cel armii; odbicia metod gry (karczma, nowa druzyna). Linia startu przy kazdej sesji.</summary>
        internal static void EnsureHooks()
        {
            if (Campaign.Current == null) return;
            if (!_hooksTried && _harmony != null)
            {
                _hooksTried = true;
                try
                {
                    var m = AccessTools.Method(typeof(CampaignEventDispatcher), "AiHourlyTick", new[] { typeof(MobileParty), typeof(PartyThinkParams) });
                    if (m == null) { _wBoost = _wVeto = "BRAK (CampaignEventDispatcher.AiHourlyTick)"; }
                    else
                    {
                        // 250: po ROT AIThinkPatch (Normal 400), przed OB5 (Low 200) - obrona oblezonej twierdzy dalej wygrywa
                        _harmony.Patch(m, postfix: new HarmonyMethod(typeof(CanonWarBalance), nameof(BoostPostfix)) { priority = 250 });
                        _wBoost = "TAK";
                        // 50: po ArmsFirst p9 (VeryLow 100), przed LordEscort i CanonWarWatch (Last 0)
                        _harmony.Patch(m, postfix: new HarmonyMethod(typeof(CanonWarBalance), nameof(VetoPostfix)) { priority = 50 });
                        _wVeto = "TAK";
                    }
                }
                catch (Exception e) { Stumble("EnsureHooks.think", e); _wBoost = _wVeto = "BLAD (" + e.GetType().Name + ")"; }
                _wBk = HookBk(_harmony);
                _wHire = ReflectHire();
                _wSpawn = ReflectSpawn();
            }
            try
            {
                BuildSides();
                int rw = CanonWarClock.RedWeddingState;
                Log.Info(Tag + "start sesji | klucz " + (KeyOn ? "TAK" : "NIE") + ", powsciagliwosc " + (RestraintOn ? "TAK" : "NIE") + ", zbiorka " + (MusterOn ? "TAK" : "NIE")
                         + " | latki: mysl AI podbicia " + _wBoost + ", weta " + _wVeto + ", BK cel armii " + _wBk + "; gra: najem z karczmy " + _wHire + ", nowa druzyna " + _wSpawn
                         + "; SCA rozkazy armii martwe w tej wersji - cele przez model ocen w mysli AI"
                         + " | zakres: " + (Prologue.CanonHere ? "kampania kanonu" : "kampania klasyczna - nie dotyczy")
                         + " | koalicje A=" + SideList(1) + " B=" + SideList(2)
                         + " | Krwawe Gody: " + (CanonWarClock.OverNow() ? "TAK" : "nie") + " (RedWeddingEvent " + rw + ", Robb " + (CanonWarClock.RobbDead ? "nie zyje" : "zyje") + ")"
                         + " | stan z zapisu: dzwignie " + (_levers ? "TAK" : "NIE") + ", slabszy " + SideName(_weak) + ", od dnia startu " + _startDay
                         + (_endDay >= 0 ? ", koniec dnia " + _endDay : "") + "." + CanonWarBalanceTest.StartText());
            }
            catch (Exception e) { Stumble("StartLine", e); }
            try { RefreshAfterLoad(); }
            catch (Exception e) { Stumble("RefreshAfterLoad", e); }
        }

        // ================================================================ zapis
        /// <summary>v1|lastFightA|lastFightB|weak|streakOn|streakOff|levers|leverSince|spawned|hired|goldHired|fortsA0|fortsB0|startDay|endDay|rody|okno|krolestwa</summary>
        internal static string Export()
        {
            var sb = new StringBuilder(512);
            sb.Append("v1|").Append(_lastFightA).Append('|').Append(_lastFightB).Append('|').Append(_weak).Append('|').Append(_streakOn).Append('|').Append(_streakOff).Append('|')
              .Append(_levers ? 1 : 0).Append('|').Append(_leverSince).Append('|').Append(_spawned).Append('|').Append(_hired).Append('|').Append(_goldHired).Append('|')
              .Append(_fortsA0).Append('|').Append(_fortsB0).Append('|').Append(_startDay).Append('|').Append(_endDay).Append('|');
            int today = Campaign.Current != null ? Today : int.MaxValue;
            bool first = true;
            foreach (var kv in _clanSpawnDay)
            {
                if (today != int.MaxValue && kv.Value < today - SpawnEveryDays) continue;
                if (!first) sb.Append(','); first = false;
                sb.Append(kv.Key).Append(':').Append(kv.Value);
            }
            sb.Append('|');
            first = true;
            for (int i = 0; i < Window; i++)
            {
                if (_ringDay[i] < 0) continue;
                if (!first) sb.Append(','); first = false;
                sb.Append(_ringDay[i]).Append(':').Append(_ring[i, 0]).Append('.').Append(_ring[i, 1]).Append('.').Append(_ring[i, 2]).Append('.').Append(_ring[i, 3]);
            }
            // recenzja 274 (3): doba ostatniej walki krolestw (pole 17; stary zapis bez pola - od startu reguly)
            sb.Append('|');
            first = true;
            foreach (var kv in _lastFightK)
            {
                if (!first) sb.Append(','); first = false;
                sb.Append(kv.Key).Append(':').Append(kv.Value);
            }
            return sb.ToString();
        }

        private static int I(string[] p, int i, int def) { int v; return i < p.Length && int.TryParse(p[i], NumberStyles.Integer, Inv, out v) ? v : def; }

        /// <summary>Stary zapis bez klucza (null / "") - start od zera (seria 0). Nie rzuca.</summary>
        internal static void Import(string v)
        {
            try
            {
                if (string.IsNullOrEmpty(v)) return;
                var p = v.Split('|');
                if (p.Length < 15 || p[0] != "v1") return;
                _lastFightA = I(p, 1, -1); _lastFightB = I(p, 2, -1); _weak = I(p, 3, 0); _streakOn = I(p, 4, 0); _streakOff = I(p, 5, 0);
                _levers = I(p, 6, 0) == 1; _leverSince = I(p, 7, -1); _spawned = I(p, 8, 0); _hired = I(p, 9, 0);
                long g; _goldHired = long.TryParse(p[10], NumberStyles.Integer, Inv, out g) ? g : 0;
                _fortsA0 = I(p, 11, -1); _fortsB0 = I(p, 12, -1); _startDay = I(p, 13, -1); _endDay = I(p, 14, -1);
                _clanSpawnDay.Clear();
                if (p.Length > 15 && p[15].Length > 0)
                    foreach (var e in p[15].Split(','))
                    {
                        int c = e.LastIndexOf(':'); int d;
                        if (c > 0 && int.TryParse(e.Substring(c + 1), NumberStyles.Integer, Inv, out d)) _clanSpawnDay[e.Substring(0, c)] = d;
                    }
                Array.Clear(_ring, 0, _ring.Length); for (int i = 0; i < Window; i++) _ringDay[i] = -1;
                if (p.Length > 16 && p[16].Length > 0)
                    foreach (var e in p[16].Split(','))
                    {
                        int c = e.IndexOf(':'); int d;
                        if (c <= 0 || !int.TryParse(e.Substring(0, c), NumberStyles.Integer, Inv, out d)) continue;
                        var q = e.Substring(c + 1).Split('.');
                        if (q.Length != 4) continue;
                        int i = d % Window; if (i < 0) i += Window;
                        _ringDay[i] = d;
                        for (int k = 0; k < 4; k++) { int x; _ring[i, k] = int.TryParse(q[k], NumberStyles.Integer, Inv, out x) ? x : 0; }
                    }
                _lastFightK.Clear();
                if (p.Length > 17 && p[17].Length > 0)
                    foreach (var e in p[17].Split(','))
                    {
                        int c = e.LastIndexOf(':'); int d;
                        if (c > 0 && int.TryParse(e.Substring(c + 1), NumberStyles.Integer, Inv, out d)) _lastFightK[e.Substring(0, c)] = d;
                    }
            }
            catch (Exception e) { Stumble("Import", e); }
        }
    }
}
