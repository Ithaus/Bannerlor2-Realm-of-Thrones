using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Election;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// 330 - WOJNA DO KAPITULACJI (Jeff 05.11: "pokoj moze byc tymczasowy, ale nie staly"; "korona bedzie prowadzila wojne az do zwyciestwa"). Klucz CanonWarUntilSurrender.
    /// Projekt docs/PROJEKT-330-WOJNA-DO-KAPITULACJI.md (sekcje B-E, poprawka G: korona NIE kapituluje z samego stosunku sil - K3 tylko Polnoc).
    /// PARA: Polnoc (battania) - korona (vlandia), wiersz CanonPeaceLock z UntilSurrender. Fazy: F0 przed Krwawymi Godami, F2 320a-KG (Krwawe Gody - Bekarci),
    /// F3 reszta po kanonie (takze wczesne wyjscie 320a), Z po kapitulacji (zatrzask w zapisie). Gracz wladca strony - bez akcji, tylko linia.
    /// ROZEJM (F0 i F3; w F2 nie - oblezenie Winterfell idzie jak w ROT): jedyne wejscie H1 (CanonPeaceLock.DipApplyPeacePrefix -> TryFromExhaustion, zmeczenie Diplomacy)
    ///  oraz zima przy zmeczeniu obu stron >= WarTruceWinterExhaustion (doba - H1 Diplomacy rusza dopiero przy 100). Warunki: wojna >= WarTruceMinWarDays od
    ///  StanceLink.WarStartDate, karencja WarTruceLoadGraceDays od pierwszego wczytania 330, zadna strona nie ma progu kapitulacji (wtedy kapitulacja).
    ///  Wykonanie: MakePeaceAction.ApplyByKingdomDecision(platnik, odbiorca, danina z DiplomacyModel.GetDailyTributeToPay, doby rozejmu) - bez zwrotu twierdz,
    ///  kosztow i SoftlyDestroy Diplomacy (H1 zwraca false). Dlugosc WarTruceDays; konczy sie zima - przedluzenie do wiosny, najwyzej WarTruceWinterMaxExtra.
    ///  W czasie rozejmu: H4 postfiks ROTStorylineWar.Enforced (Priority.Last) = false dla tej pary; H5 prefiks Kingdom.AddDecision(DeclareWarDecision) = false
    ///  i CanonThroneWars.WarScorePostfix -1e6 (BlocksWar). Koniec: kapitulacja, jesli prog; inaczej Prologue.RotDeclareWar272 (odbiorca daniny albo korona) -
    ///  gra zeruje danine (StanceLink.ResetStats), Diplomacy liczy zmeczenie od 0. Nieudane wznowienie - ponowienie co dobe (potkniecie).
    ///  Wojna wypowiedziana wczesniej (ROT, inny mod) - rozejm konczy sie od razu (linia).
    /// H6 (CampaignEvents.MakePeace, CanonWarsBehavior): kazdy pokoj tej pary bez naszej flagi - rozejm WarTruceDays (danina przycieta do tylu rat); w F2 rozejm 1 doby.
    ///  Wyjatki: nasze pokoje (flaga), gracz wladca strony, pokoj ROT z Krwawych Godow przy kluczu NorthFightsOnAfterRedWedding NIE.
    /// KAPITULACJA (WarTruce.Surrender.cs): liczniki K1-K3 w zapisie, co dobe wojny; F2 - odlozona. Zatrzask Z do nastepnego wydarzenia kanonu pary (Krwawe Gody,
    ///  JonEvent, doba Revenge x skala) - wtedy wojna wraca; po Bitwie Bekartow (BastardsSiegeEvent 2) koniec 330 dla pary (staly pokoj).
    /// Zapis "arm_k330" (SaveText.Sync; stara gra bez wpisu - stan pusty, karencja od pierwszej doby). Bledy: licznik potkniec, bez globalnego wylacznika, latki nie rzucaja.
    /// Linie "Wojna do kapitulacji (330): ...", progi tools/sprawdz_logi.py --grupa wojna330.
    /// </summary>
    internal static partial class WarTruce
    {
        internal const string Tag = "Wojna do kapitulacji (330): ";
        internal const string SaveKey = "arm_k330";
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        internal const string NorthId = "battania", CrownId = "vlandia", RiverId = "riverlands";
        internal const string WinterfellId = "town_B2", KingsLandingId = "town_EW4", RedKeepId = "ROT_castle61";
        internal const int RevengeRotDay = 350;   // ROTStorylineWars.OnNewGameCreated: Polnoc-Westerlands StartDay 350, EndDay 0 ("Revenge of the King in the North")

        // ---------------------------------------------------------------- ustawienia (przyciete jak w gen_mcm RANGES)
        private static Settings S { get { return Settings.Current; } }
        internal static bool KeyOn { get { var s = S; return s != null && s.CanonWarUntilSurrender; } }
        /// <summary>Regula 330 czynna: klucz TAK i para nie po ostatecznej kapitulacji (Bekarci).</summary>
        internal static bool On { get { return KeyOn && !_final; } }
        /// <summary>Tlumienie wojny pary (H4, H5, H5b call-to-war, ocena BK) mozliwe: klucz 330 i te same warunki co Live (klucz 264, kampania kanonu). Recenzja 330:
        /// bez tego zatrzask Z przy kluczu 264 NIE trzymal wiecznie (ZDaily nie biegnie przy !Live).</summary>
        private static bool GuardOn { get { return KeyOn && CanonPeaceLock.KeyOn && Prologue.CanonHere; } }
        /// <summary>Pokoj pary trzyma: rozejm, zatrzask Z albo KONIEC 330 (pokoj staly po Bekartach - recenzja 330: bez tego ROT Revenge wypowiadal wojne nastepnej doby).</summary>
        private static bool PeaceHeld { get { return _final || _z || TruceHolds; } }
        /// <summary>Wlasnie trwa nasze MakePeaceAction (rozejm / kapitulacja 330) - WorldClock7 dopisuje "rozejm/kapitulacja 330" zamiast "MIMO BLOKADY".</summary>
        internal static bool OurPeaceNow { get { return _ourPeace; } }
        internal static int TruceDays { get { var s = S; int v = s != null ? s.WarTruceDays : 42; return v < 1 ? 1 : v > 364 ? 364 : v; } }
        internal static int WinterMaxExtra { get { var s = S; int v = s != null ? s.WarTruceWinterMaxExtra : 21; return v < 0 ? 0 : v > 91 ? 91 : v; } }
        internal static int MinWarDays { get { var s = S; int v = s != null ? s.WarTruceMinWarDays : 84; return v < 0 ? 0 : v > 364 ? 364 : v; } }
        internal static float WinterExh { get { var s = S; float v = s != null ? s.WarTruceWinterExhaustion : 75f; return v < 1f ? 1f : v > 100f ? 100f : v; } }
        internal static int GraceDays { get { var s = S; int v = s != null ? s.WarTruceLoadGraceDays : 7; return v < 0 ? 0 : v > 91 ? 91 : v; } }
        internal static float LoseRatio { get { var s = S; float v = s != null ? s.WarSurrenderLoseRatio : 0.67f; return v < 0.05f ? 0.05f : v > 1f ? 1f : v; } }
        internal static float LowRatio { get { var s = S; float v = s != null ? s.WarSurrenderRatio : 0.33f; return v < 0.05f ? 0.05f : v > 1f ? 1f : v; } }
        internal static int RatioDays { get { var s = S; int v = s != null ? s.WarSurrenderRatioDays : 30; return v < 1 ? 1 : v > 364 ? 364 : v; } }
        internal static int HoldDays { get { var s = S; int v = s != null ? s.WarSurrenderHoldDays : 42; return v < 1 ? 1 : v > 364 ? 364 : v; } }
        internal static int CaptiveDays { get { var s = S; int v = s != null ? s.WarSurrenderCaptiveDays : 21; return v < 1 ? 1 : v > 364 ? 364 : v; } }
        internal static int Strongholds { get { var s = S; int v = s != null ? s.WarSurrenderStrongholds : 3; return v < 0 ? 0 : v > 20 ? 20 : v; } }
        internal static int TributeDays { get { var s = S; int v = s != null ? s.WarSurrenderTributeDays : 84; return v < 0 ? 0 : v > 364 ? 364 : v; } }
        internal static bool ClansKey { get { var s = S; return s != null && s.WarSurrenderClansToCrown; } }
        private static bool LogOn { get { var s = S; return s != null && s.LogEnabled; } }

        // ---------------------------------------------------------------- stan (zapis arm_k330)
        private static int _firstSeen = -1;                     // pierwsza doba kampanii z 330 (karencja)
        private static int _tFrom = -1, _tTo = -1;              // rozejm od / do (doba kampanii; -1 brak)
        private static string _tWhy = "";                       // powod rozejmu
        private static int _tCount;                             // rozejmow w kampanii
        private static bool _tExt;                              // przedluzenie zimowe juz policzone
        private static string _tPayer = "", _tRecv = "";        // platnik i odbiorca daniny (id krolestw; pusty - bez daniny)
        private static int _tDaily;                             // danina / doba
        // liczniki kapitulacji: 0..2 Polnoc K1 K2 K3, 3..5 korona K1 K2 (K3 korony zawsze 0 - poprawka G)
        private static readonly int[] _k = new int[6];
        private static int _fortsN0 = -1, _fortsC0 = -1;        // twierdze w dniu startu licznikow
        private static string _leadN = "", _leadC = "";        // wladcy (zmiana - K2 od zera)
        private static bool _z;                                 // zatrzask Z (po kapitulacji)
        private static string _zWhy = "";
        private static int _zDay = -1, _zRw = -9, _zJon = -9;  // stan RedWeddingEvent / JonEvent w chwili zatrzasku
        private static bool _zRwDone, _zRev;                    // Krwawe Gody zaszly / doba Revenge minela w chwili zatrzasku
        private static bool _final;                             // KONIEC 330 (Bekarci + kapitulacja) - para poza regula
        private static bool _resumePending;                     // wznowienie wojny nieudane - ponowienie co dobe
        private static string _resumeWhy = "";
        private static readonly List<string> _cession = new List<string>();   // "stId>clanId>dzien" - cesja czekajaca (gracz w osadzie, oblezenie)
        private static int _importBad;

        // ---------------------------------------------------------------- sesja
        [ThreadStatic] private static bool _ourPeace;           // nasze MakePeaceAction (rozejm, kapitulacja) - H6 pomija
        [ThreadStatic] private static bool _ourWar;             // nasze wznowienie - zrodlo w linii
        [ThreadStatic] private static bool _bypass;             // odczyt ROT bez H4 (linia doby "wymusza tak/nie")
        private static int _stumbles, _dStumbles;
        private static int _dBlockAi, _sBlockAi, _dBlockBk, _sBlockBk, _dH4, _sH4;
        private static readonly HashSet<string> _dSaid = new HashSet<string>(StringComparer.Ordinal);
        private static bool _playerToldWar, _sessionLine;
        private static double _lastSN = -1, _lastSC = -1, _lastRN = -1, _lastRC = -1;
        private static int _lastFN = -1, _lastFC = -1;
        private static float _lastExhN = -1f, _lastExhC = -1f;

        /// <summary>Nowa kampania (WarTruceBehavior): stan zapisu i sesji.</summary>
        internal static void Reset()
        {
            ResetSaved(); _importBad = 0;
            _stumbles = _dStumbles = 0; _dBlockAi = _sBlockAi = _dBlockBk = _sBlockBk = _dH4 = _sH4 = 0;
            _dSaid.Clear(); _playerToldWar = false; _sessionLine = false;
            _lastSN = _lastSC = _lastRN = _lastRC = -1; _lastFN = _lastFC = -1; _lastExhN = _lastExhC = -1f;
            WarTruceTest.Reset();
        }

        private static void ResetSaved()
        {
            _firstSeen = -1; ClearTruceState(); _tCount = 0;
            for (int i = 0; i < _k.Length; i++) _k[i] = 0;
            _fortsN0 = _fortsC0 = -1; _leadN = _leadC = "";
            _z = false; _zWhy = ""; _zDay = -1; _zRw = _zJon = -9; _zRwDone = _zRev = false;
            _final = false; _resumePending = false; _resumeWhy = ""; _cession.Clear();
        }

        /// <summary>Autotest: koniec rozejmu na dzis (bez przedluzenia zimowego) - doba wznowi wojne.</summary>
        internal static void TestEndTruceToday() { if (_tFrom >= 0) { _tTo = Today; _tExt = true; } }

        private static void ClearTruceState() { _tFrom = _tTo = -1; _tWhy = ""; _tExt = false; _tPayer = _tRecv = ""; _tDaily = 0; }

        internal static void Stumble(string where, Exception e)
        {
            _stumbles++; _dStumbles++;
            if (_stumbles <= 5) { try { Log.Error("WarTruce." + where + " (potkniecie " + _stumbles + ")", e); } catch { } }
        }

        private static void Info(string text) { try { if (LogOn) Log.Info(Tag + "dzien " + Today + " | " + text); } catch { } }

        /// <summary>Linia raz na dobe na klucz.</summary>
        private static void InfoOnce(string key, string text) { if (_dSaid.Add(key)) Info(text); }

        internal static int Today { get { return (int)CampaignTime.Now.ToDays; } }

        private static string Name(IFaction f) { try { return f != null && f.Name != null ? f.Name.ToString() : "-"; } catch { return "?"; } }

        private static string Clean(string v) { return string.IsNullOrEmpty(v) ? "" : v.Replace('|', '/').Replace(';', ',').Replace('\n', ' ').Replace('\r', ' '); }

        internal static Kingdom North { get { return Prologue.KingdomById(NorthId); } }
        internal static Kingdom Crown { get { return Prologue.KingdomById(CrownId); } }

        internal static bool IsPair(IFaction a, IFaction b)
        {
            if (a == null || b == null) return false;
            string x = a.StringId, y = b.StringId;
            return (x == NorthId && y == CrownId) || (x == CrownId && y == NorthId);
        }

        private static bool PlayerRules(Kingdom n, Kingdom c) { return Prologue.RulesRealm(n) || Prologue.RulesRealm(c); }

        /// <summary>Rozejm trwa (doba &lt; konca). W dobie konca H4/H5 juz nie blokuja - ROT moze wypowiedziec wojne pierwszy.</summary>
        internal static bool TruceHolds { get { return _tFrom >= 0 && Today < _tTo; } }
        internal static bool TruceActive { get { return _tFrom >= 0; } }
        internal static bool ZLatched { get { return _z; } }
        internal static int TruceEnd { get { return _tTo; } }
        internal static int[] Counters { get { return (int[])_k.Clone(); } }

        /// <summary>Para pod regula teraz (klucz, kampania kanonu, klucz 264, oba krolestwa w grze).</summary>
        private static bool Live(out Kingdom n, out Kingdom c)
        {
            n = null; c = null;
            if (!On || Campaign.Current == null || !Prologue.CanonHere || !CanonPeaceLock.KeyOn) return false;
            n = North; c = Crown;
            return n != null && c != null && !n.IsEliminated && !c.IsEliminated;
        }

        // ---------------------------------------------------------------- odczyty
        private static bool _wemTried;
        private static PropertyInfo _wemInst;
        private static MethodInfo _wemGet;

        /// <summary>Diplomacy WarExhaustionManager.Instance.GetWarExhaustion(k1, k2) - zmeczenie k1 w wojnie z k2 (0-100); -1 brak Diplomacy.</summary>
        internal static float Exh(Kingdom k1, Kingdom k2)
        {
            try
            {
                if (!_wemTried)
                {
                    _wemTried = true;
                    var t = AccessTools.TypeByName("Diplomacy.WarExhaustion.WarExhaustionManager");
                    if (t != null) { _wemInst = AccessTools.Property(t, "Instance"); _wemGet = AccessTools.Method(t, "GetWarExhaustion", new[] { typeof(Kingdom), typeof(Kingdom) }); }
                }
                if (_wemInst == null || _wemGet == null) return -1f;
                var inst = _wemInst.GetValue(null, null);
                if (inst == null) return -1f;
                return Convert.ToSingle(_wemGet.Invoke(inst, new object[] { k1, k2 }), Inv);
            }
            catch (Exception e) { Stumble("Exh", e); return -1f; }
        }

        /// <summary>Zima teraz (klimat Westeros, gdy czynny; inaczej pora kalendarza gry). Nie rzuca.</summary>
        internal static bool IsWinter()
        {
            try { return WesterosClimate.Now() == CampaignTime.Seasons.Winter; }
            catch { try { return CampaignTime.Now.GetSeasonOfYear == CampaignTime.Seasons.Winter; } catch { return false; } }
        }

        /// <summary>Doby zimy do wiosny (klimat Westeros); -1 nie wiadomo.</summary>
        private static int WinterLeft()
        {
            try
            {
                int season, sday, len;
                if (WesterosClimate.SeasonInfo(out season, out sday, out len) && season == (int)CampaignTime.Seasons.Winter && len > 0) return Math.Max(0, len - sday + 1);
            }
            catch { }
            return -1;
        }

        /// <summary>Doby wojny od StanceLink.WarStartDate; int.MaxValue - stance bez daty (stary zapis: liczone jak dluga wojna); -1 nie w wojnie.</summary>
        internal static int WarDays(Kingdom n, Kingdom c, out int startDay)
        {
            startDay = -1;
            if (n == null || c == null || !n.IsAtWarWith(c)) return -1;
            var link = n.GetStanceWith(c);
            if (link == null) return -1;
            var ws = link.WarStartDate;
            if (ws == CampaignTime.Zero) return int.MaxValue;
            startDay = (int)ws.ToDays;
            return (int)Math.Floor((CampaignTime.Now - ws).ToDays);
        }

        /// <summary>Danina / doba z modelu gry (DiplomacyModel.GetDailyTributeToPay; aktywny model gry / modow); 0 - nikt nie placi. Nie rzuca.</summary>
        internal static int ModelTribute(Kingdom payer, Kingdom recv)
        {
            try
            {
                var m = Campaign.Current != null && Campaign.Current.Models != null ? Campaign.Current.Models.DiplomacyModel : null;
                if (m == null || payer == null || recv == null || payer.RulingClan == null || recv.RulingClan == null) return 0;
                int dur;
                int v = m.GetDailyTributeToPay(payer.RulingClan, recv.RulingClan, out dur);
                return v > 0 ? v : 0;
            }
            catch (Exception e) { Stumble("ModelTribute", e); return 0; }
        }

        /// <summary>Doba Revenge na osi opowiesci (StartDay 350 x skala Fabuli) minela.</summary>
        internal static bool RevengeReached()
        {
            try { return Prologue.StoryDays() >= RevengeRotDay * Prologue.StoryScale(); } catch { return false; }
        }

        // ---------------------------------------------------------------- czyste reguly (autotest - tabele prawdy)

        internal const int GateNone = 0, GateTruce = 1, GateSurrender = 2;

        /// <summary>Brama rozejmu (pkt B.1): faza F0/F3, wojna, gracz nie rzadzi, prog kapitulacji -> kapitulacja, wojna >= minDays, karencja, zmeczenie (exhOk).
        /// why - powod bloku do linii ("faza 2", "wojna &lt; 84", "karencja" ...). Czysta funkcja.</summary>
        internal static int Gate(int phase, bool war, bool player, bool surrenderDue, int warDays, int minDays, int today, int graceEnd, bool exhOk, out string why)
        {
            if (!war) { why = "nie w wojnie"; return GateNone; }
            if (player) { why = "gracz rzadzi strona"; return GateNone; }
            if (phase == 2 || phase == 1) { why = "faza 2"; return GateNone; }
            if (phase != 0 && phase != 3) { why = "poza regula"; return GateNone; }
            if (surrenderDue) { why = "prog kapitulacji"; return GateSurrender; }
            if (warDays < minDays) { why = "wojna < " + minDays; return GateNone; }
            if (today < graceEnd) { why = "karencja"; return GateNone; }
            if (!exhOk) { why = "zmeczenie ponizej progu"; return GateNone; }
            why = "-";
            return GateTruce;
        }

        /// <summary>Przedluzenie zimowe: koniec rozejmu w zimie - do wiosny (winterLeft dob; -1 nie wiadomo = pelne), najwyzej maxExtra. Czysta funkcja.</summary>
        internal static int WinterExtra(bool winterAtEnd, int winterLeft, int maxExtra)
        {
            if (!winterAtEnd || maxExtra <= 0) return 0;
            if (winterLeft < 0) return maxExtra;
            return Math.Max(0, Math.Min(maxExtra, winterLeft));
        }

        /// <summary>Pierwsza doba, od ktorej rozejm jest mozliwy (wojna >= min i koniec karencji).</summary>
        private static int NextTruceDay(int warStart, int graceEnd)
        {
            int a = warStart >= 0 ? warStart + MinWarDays : Today;
            return Math.Max(a, graceEnd);
        }

        private static int GraceEnd { get { return _firstSeen < 0 ? Today + GraceDays : _firstSeen + GraceDays; } }

        // ---------------------------------------------------------------- H1: zmeczenie Diplomacy (CanonPeaceLock.DipApplyPeacePrefix)

        /// <summary>H1 wiersza UntilSurrender (para zablokowana przez 264/330): rozejm albo kapitulacja zamiast pokoju Diplomacy. true - zalatwione (rozejm / kapitulacja
        /// weszly), false - blok jak dotad (linia "blok: rozejm odrzucony"). Nie rzuca.</summary>
        internal static bool TryFromExhaustion(Kingdom k1, Kingdom k2, string path)
        {
            try
            {
                Kingdom n, c;
                if (!IsPair(k1, k2) || !Live(out n, out c)) return false;
                if (_firstSeen < 0) _firstSeen = Today;
                string pw;
                int ph = CanonPeaceLock.Phase330(n, c, out pw);
                bool war = n.IsAtWarWith(c);
                bool player = PlayerRules(n, c);
                int ws; int wd = WarDays(n, c, out ws);
                float eN = Exh(n, c), eC = Exh(c, n);
                bool exhOk = (eN < 0f && eC < 0f) ? path != null && path.StartsWith(CanonPeaceLock.PathWeariness, StringComparison.Ordinal) : (eN >= 100f || eC >= 100f);
                Kingdom loser; string kind;
                bool due = SurrenderDue(n, c, out loser, out kind);
                string why;
                int g = Gate(ph, war, player, due, wd, MinWarDays, Today, GraceEnd, exhOk, out why);
                if (g == GateSurrender) return Surrender(loser, loser == n ? c : n, kind, n, c, ph);
                if (g == GateTruce) return StartTruce(n, c, "zmeczenie", TruceDays);
                if (player) { InfoOnce("h1gracz", "zmeczenie (" + path + "): gracz rzadzi strona - bez akcji 330, blokada 264 jak dotad."); return false; }
                string next = why.StartsWith("wojna <", StringComparison.Ordinal) || why == "karencja" ? " (nastepny od d." + NextTruceDay(ws, GraceEnd) + ")" : "";
                InfoOnce("h1blok", "blok: rozejm odrzucony (" + why + ")" + next + " | droga " + path + " | zmeczenie " + F0(eN) + "/" + F0(eC) + ", faza " + PhaseName(ph) + ".");
                return false;
            }
            catch (Exception e) { Stumble("TryFromExhaustion", e); return false; }
        }

        private static string F0(float v) { return v < 0f ? "?" : v.ToString("0", Inv); }

        internal static string PhaseName(int ph)
        {
            if (_z) return "Z";
            return ph == 0 ? "F0" : ph == 1 ? "F2 (wesele ROT w toku)" : ph == 2 ? "F2" : ph == 3 ? "F3" : "-";
        }

        // ---------------------------------------------------------------- rozejm

        /// <summary>Rozejm: pokoj gry z danina z modelu na czas rozejmu (strona slabsza w postepie wojny placi; 0 - bez daniny). true - pokoj wszedl.</summary>
        internal static bool StartTruce(Kingdom n, Kingdom c, string why, int days)
        {
            int tN = ModelTribute(n, c), tC = ModelTribute(c, n);
            Kingdom payer = null, recv = null; int daily = 0;
            if (tN > 0 && tN >= tC) { payer = n; recv = c; daily = tN; }
            else if (tC > 0) { payer = c; recv = n; daily = tC; }
            _ourPeace = true;
            // recenzja 330: MakePeaceAction robi SetNeutral przed zdarzeniem MakePeace - wyjatek obcego sluchacza nie moze zostawic pokoju bez stanu rozejmu
            try { MakePeaceAction.ApplyByKingdomDecision(payer ?? c, recv ?? n, daily, daily > 0 ? days : 0); }
            catch (Exception e) { Stumble("StartTruce.MakePeace", e); }
            finally { _ourPeace = false; }
            if (n.IsAtWarWith(c)) { _dStumbles++; _stumbles++; Info("ROZEJM nie wszedl (" + why + ") - pokoj gry odrzucony przez inny mod; wojna dalej."); return false; }
            int d = Today;
            ClearTruceState();
            ZeroCounters("rozejm");
            _tFrom = d; _tTo = d + days; _tWhy = why; _tCount++;
            _tPayer = payer != null ? payer.StringId : ""; _tRecv = recv != null ? recv.StringId : ""; _tDaily = daily;
            Info("ROZEJM start (" + why + ") d." + _tFrom + "-d." + _tTo + ", danina " + TributeText(payer, recv, daily, days) + " | rozejm nr " + _tCount + ".");
            try { Log.Player("A truce is made between " + Name(n) + " and " + Name(c) + ". It holds for " + days + " days; then the war goes on.", false); } catch { }
            return true;
        }

        private static string TributeText(IFaction payer, IFaction recv, int daily, int days)
        {
            if (payer == null || daily <= 0) return "brak (model gry 0)";
            return payer.StringId + "->" + (recv != null ? recv.StringId : "?") + " " + daily + "/doba x " + days;
        }

        /// <summary>Koniec rozejmu albo zatrzasku: wojna wraca (odbiorca daniny albo korona wypowiada; DeclareWar z flaga ROT CallingDeclareWar). true - w wojnie.</summary>
        private static bool Resume(Kingdom n, Kingdom c, string why)
        {
            if (PlayerRules(n, c))
            {
                Info(why + ": gracz rzadzi strona - bez wznowienia (linia).");
                ClearTruceState(); _resumePending = false;
                return false;
            }
            Kingdom a = ResumeByNorth(_tFrom >= 0, _tRecv, _zWhy) ? n : c;
            Kingdom b = a == n ? c : n;
            _ourWar = true;
            try { Prologue.RotDeclareWar272(a, b); }
            finally { _ourWar = false; }
            bool war = n.IsAtWarWith(c);
            if (!war)
            {
                _resumePending = true; _resumeWhy = why; _stumbles++; _dStumbles++;
                Info(why + ": DeclareWar " + a.StringId + "->" + b.StringId + " NIE weszla - ponowienie nastepnej doby (potkniecie).");
            }
            else { _resumePending = false; _resumeWhy = ""; }
            return war;
        }

        /// <summary>Kto wypowiada przy wznowieniu: koniec rozejmu - odbiorca daniny (brak - korona); koniec zatrzasku Z - zwyciezca kapitulacji (recenzja 330:
        /// po kapitulacji korony wypowiada Polnoc). true - Polnoc. Czysta funkcja.</summary>
        internal static bool ResumeByNorth(bool truce, string recv, string zWhy)
        {
            if (truce) return recv == NorthId;
            return zWhy != null && zWhy.StartsWith("KAPITULACJA korony", StringComparison.Ordinal);
        }

        /// <summary>Liczniki K1-K3 od zera (seria "z rzedu": rozejm, faza F2). Linia, gdy cos bylo.</summary>
        private static void ZeroCounters(string why)
        {
            bool any = false;
            for (int i = 0; i < _k.Length; i++) if (_k[i] != 0) any = true;
            if (any) Info("liczniki kapitulacji od zera (" + why + "): Polnoc " + _k[0] + "/" + _k[1] + "/" + _k[2] + ", korona " + _k[3] + "/" + _k[4] + ".");
            for (int i = 0; i < _k.Length; i++) _k[i] = 0;
        }

        // ---------------------------------------------------------------- H6 i wojna (CanonWarsBehavior.RegisterEvents)

        private static string StackSource()
        {
            try
            {
                var st = new StackTrace(2, false);
                string first = null;
                for (int i = 0; i < st.FrameCount && i < 40; i++)
                {
                    var m = st.GetFrame(i)?.GetMethod();
                    if (m == null || m.DeclaringType == null) continue;
                    string t = m.DeclaringType.FullName ?? "", nm = m.Name ?? "";
                    if (t.IndexOf("RedWedding", StringComparison.Ordinal) >= 0 || nm.IndexOf("ExecuteRobb", StringComparison.Ordinal) >= 0) return "ROT Krwawe Gody";
                    if (t.IndexOf("ROTStorylineWars", StringComparison.Ordinal) >= 0) return "ROT";
                    if (t.IndexOf("Chronicle", StringComparison.Ordinal) >= 0) return "Kronika";
                    if (t.IndexOf("Barter", StringComparison.Ordinal) >= 0) return "barter";
                    if (t.IndexOf("Diplomacy", StringComparison.Ordinal) >= 0) return "Diplomacy";
                    if (t.IndexOf("BannerKings", StringComparison.Ordinal) >= 0) return "BK";
                    if (t.IndexOf("CheatsCampaignBehavior", StringComparison.Ordinal) >= 0 || t.IndexOf("Console", StringComparison.Ordinal) >= 0) return "kody";
                    if (first == null && t.StartsWith("TaleWorlds.CampaignSystem.Actions", StringComparison.Ordinal) == false && t.StartsWith("TaleWorlds.CampaignSystem.CampaignEvent", StringComparison.Ordinal) == false
                        && m.DeclaringType != typeof(WarTruce)) first = m.DeclaringType.Name + "." + nm;
                }
                return first ?? "?";
            }
            catch { return "?"; }
        }

        /// <summary>H6: pokoj pary bez naszej flagi - rozejm (F0/F3 WarTruceDays, F2 1 doba), danina przycieta do tylu rat. Wyjatki w opisie klasy. Nie rzuca.</summary>
        internal static void OnPeace(IFaction f1, IFaction f2, MakePeaceAction.MakePeaceDetail detail)
        {
            try
            {
                if (_ourPeace || !IsPair(f1, f2)) return;
                Kingdom n, c;
                if (!Live(out n, out c)) return;
                if (_firstSeen < 0) _firstSeen = Today;
                string src = StackSource();
                if (PlayerRules(n, c)) { Info("pokoj z zewnatrz (" + src + "): gracz rzadzi strona - bez rozejmu, pokoj zostaje."); return; }
                if (src == "ROT Krwawe Gody" && !CanonPeaceLock.FightsOnKey) { Info("pokoj ROT Krwawych Godow przy kluczu North Fights On After Red Wedding NIE - bez rozejmu (wyjatek), pokoj zostaje."); return; }
                string pw;
                int ph = CanonPeaceLock.Phase330(n, c, out pw);
                int days = ph == 2 || ph == 1 ? 1 : TruceDays;
                var link = n.GetStanceWith(c);
                Kingdom payer = null, recv = null; int daily = 0;
                if (link != null)
                {
                    int dn = link.GetDailyTributeToPay(n), dc = link.GetDailyTributeToPay(c);
                    if (dn > 0) { payer = n; recv = c; daily = dn; } else if (dc > 0) { payer = c; recv = n; daily = dc; }
                    int left = link.GetRemainingTributePaymentCount();
                    if (payer != null && left > days) link.SetDailyTributePaid(payer, daily, days);
                }
                int d = Today;
                ClearTruceState();
                _tFrom = d; _tTo = d + days; _tWhy = "z zewnatrz:" + src; _tCount++;
                _tPayer = payer != null ? payer.StringId : ""; _tRecv = recv != null ? recv.StringId : ""; _tDaily = daily;
                _z = false;
                ZeroCounters("rozejm z zewnatrz");
                Info("ROZEJM start (z zewnatrz:" + src + ") d." + _tFrom + "-d." + _tTo + ", danina " + TributeText(payer, recv, daily, days) + " | faza " + PhaseName(ph)
                     + (days == 1 ? " - rozejm 1 doby, wojna wraca jutro" : "") + " | rozejm nr " + _tCount + ".");
            }
            catch (Exception e) { Stumble("OnPeace", e); }
        }

        /// <summary>Wojna pary: rozejm konczy sie (koniec albo przerwanie), zatrzask Z przerwany. Nie rzuca.</summary>
        internal static void OnWar(IFaction f1, IFaction f2, DeclareWarAction.DeclareWarDetail detail)
        {
            try
            {
                if (!IsPair(f1, f2)) return;   // recenzja 330: czyszczenie takze przy kluczu NIE (inaczej nieaktualny zatrzask Z po wlaczeniu)
                string src = _ourWar ? "nasza DeclareWar" : StackSource();
                int d = Today;
                if (_tFrom >= 0)
                {
                    if (d >= _tTo) Info("ROZEJM koniec: wojna wraca (" + src + ") | rozejm d." + _tFrom + "-d." + _tTo + " (" + _tWhy + ").");
                    else Info("ROZEJM przerwany (" + src + ") | rozejm d." + _tFrom + "-d." + _tTo + " (" + _tWhy + "), zostalo " + (_tTo - d) + " dob.");
                    ClearTruceState();
                }
                if (_z && !_final) { Info("zatrzask Z przerwany (" + src + ") - wojna | " + _zWhy + "."); _z = false; }
                if (_cession.Count > 0) { Info("cesja czekajaca porzucona (" + _cession.Count + ") - wojna wrocila (" + src + ")."); _cession.Clear(); }
                _resumePending = false; _resumeWhy = "";
            }
            catch (Exception e) { Stumble("OnWar", e); }
        }

        // ---------------------------------------------------------------- H4 / H5 (latki)

        /// <summary>H5 i CanonThroneWars: wojna pary zablokowana (rozejm trwa albo zatrzask Z; gracz wladca - jego wybor). Tylko odczyt, bez logu.</summary>
        internal static bool BlocksWar(IFaction a, IFaction b)
        {
            try
            {
                if (!IsPair(a, b) || !GuardOn || !PeaceHeld) return false;
                return !(Prologue.RulesRealm(a) || Prologue.RulesRealm(b));
            }
            catch { return false; }
        }

        /// <summary>CanonThroneWars.WarScorePostfix: licznik ocen BK zbitych do -1e6 (watek dowolny).</summary>
        internal static void NoteBkBlock() { Interlocked.Increment(ref _dBlockBk); Interlocked.Increment(ref _sBlockBk); }

        private static bool _hooksTried;
        private static string _wH4 = "nie wpieta", _wH5 = "nie wpieta", _wH5b = "nie wpieta";

        /// <summary>Raz na proces (WarTruceBehavior.OnSessionLaunched, po CanonWarsBehavior - ROT wojny gotowe): H4 postfiks Last na ROTStorylineWar.Enforced, H5 prefiks
        /// First na Kingdom.AddDecision. Wpinane zawsze (klucz czytany w latkach - NIE = przepuszczaja).</summary>
        internal static void EnsureHooks()
        {
            if (_hooksTried) return;
            _hooksTried = true;
            var h = new Harmony("com.jeff.armoury.war330");
            try
            {
                var m = Prologue.EnforcedMethod330();
                if (m == null) _wH4 = "BRAK (ROT ROTStorylineWar.Enforced)";
                else { h.Patch(m, postfix: new HarmonyMethod(typeof(WarTruce), nameof(EnforcedPostfix)) { priority = Priority.Last }); _wH4 = "TAK"; }
            }
            catch (Exception e) { _wH4 = "BLAD (" + e.GetType().Name + ")"; Stumble("EnsureHooks.H4", e); }
            try
            {
                var m = AccessTools.DeclaredMethod(typeof(Kingdom), "AddDecision", new[] { typeof(KingdomDecision), typeof(bool) });
                if (m == null) _wH5 = "BRAK";
                else { h.Patch(m, prefix: new HarmonyMethod(typeof(WarTruce), nameof(AddDecisionPrefix)) { priority = Priority.First }); _wH5 = "TAK"; }
            }
            catch (Exception e) { _wH5 = "BLAD (" + e.GetType().Name + ")"; Stumble("EnsureHooks.H5", e); }
            try
            {
                // recenzja 330: sojusz gry wzywa do wojny (ProposeCallToWarAgreementDecision / okno gracza) -> StartCallToWarAgreement -> DeclareWarAction.ApplyByCallToWarAgreement;
                // ocena idzie przez AllianceModel, nie przez -1e6 BK - prefiks na samym starcie umowy (przed kosztem i premia)
                var t = AccessTools.TypeByName("TaleWorlds.CampaignSystem.CampaignBehaviors.AllianceCampaignBehavior");
                var m = t != null ? AccessTools.DeclaredMethod(t, "StartCallToWarAgreement") : null;
                if (m == null || m.GetParameters().Length < 3) _wH5b = "BRAK";
                else { h.Patch(m, prefix: new HarmonyMethod(typeof(WarTruce), nameof(CallToWarPrefix)) { priority = Priority.First }); _wH5b = "TAK"; }
            }
            catch (Exception e) { _wH5b = "BLAD (" + e.GetType().Name + ")"; Stumble("EnsureHooks.H5b", e); }
        }

        /// <summary>H5b: prefiks AllianceCampaignBehavior.StartCallToWarAgreement(wzywajacy, wezwany, przeciw, ...) - wezwanie do wojny w parze w rozejmie / zatrzasku / KONCU 330:
        /// false (bez kosztu, premii i DeclareWar). Nie rzuca.</summary>
        public static bool CallToWarPrefix(object __1, object __2)
        {
            try
            {
                var k = __1 as IFaction; var t = __2 as IFaction;
                if (!BlocksWar(k, t)) return true;
                _dBlockAi++; _sBlockAi++;
                InfoOnce("h5b|" + (k != null ? k.StringId : "?"), "blok: AI wojna w " + BlockName() + " (call-to-war sojuszu, wezwany " + Name(k) + " przeciw " + Name(t) + ").");
                return false;
            }
            catch (Exception e) { Stumble("CallToWarPrefix", e); return true; }
        }

        private static string BlockName() { return _final ? "KONCU 330" : _z ? "zatrzasku Z" : "rozejmie do d." + _tTo; }

        /// <summary>H4: postfiks ROTStorylineWar.Enforced (Priority.Last) - para w rozejmie albo w zatrzasku Z: false z naszym powodem (po angielsku - okno ROT). Nie rzuca.</summary>
        public static void EnforcedPostfix(object __instance, ref bool __result, ref string disabledReason)
        {
            try
            {
                if (!__result || _bypass || __instance == null || !GuardOn || !PeaceHeld) return;
                if (!Prologue.War330Pair(__instance)) return;
                var n = North; var c = Crown;
                if (n == null || c == null || PlayerRules(n, c)) return;
                __result = false;
                disabledReason = _final ? "The war ended with a surrender after the Battle of the Bastards; the peace holds."
                               : _z ? "The war ended with a surrender; it flares up again with the next turn of the story." : "A truce holds until day " + _tTo + ".";
                _dH4++; _sH4++;
            }
            catch (Exception e) { Stumble("EnforcedPostfix", e); }
        }

        /// <summary>H5: prefiks Kingdom.AddDecision - DeclareWarDecision pary w rozejmie / zatrzasku: false przed kosztem wplywow i wyborami. Nie rzuca.</summary>
        public static bool AddDecisionPrefix(Kingdom __instance, KingdomDecision __0)
        {
            try
            {
                if (__0 == null) return true;
                IFaction k, t; string kind;
                var d = __0 as DeclareWarDecision;
                var pc = __0 as ProposeCallToWarAgreementDecision;
                var ac = __0 as AcceptCallToWarAgreementDecision;
                if (d != null) { k = d.Kingdom ?? __instance; t = d.FactionToDeclareWarOn; kind = "AddDecision"; }
                else if (pc != null) { k = pc.CalledKingdom; t = pc.KingdomToCallToWarAgainst; kind = "AddDecision call-to-war"; }   // recenzja 330: wezwany do wojny z para
                else if (ac != null) { k = ac.Kingdom ?? __instance; t = ac.KingdomToCallToWarAgainst; kind = "AddDecision przyjecie call-to-war"; }
                else return true;
                if (!BlocksWar(k, t)) return true;
                _dBlockAi++; _sBlockAi++;
                if (WarTruceTest.Armed) WarTruceTest.Hits++;
                var who = __0.ProposerClan;
                InfoOnce("h5|" + kind + "|" + (k != null ? k.StringId : "?"), "blok: AI wojna w " + BlockName() + " (" + kind + ", wnioskodawca "
                                                                + (who != null && who.Name != null ? who.Name.ToString() : "-") + ").");
                if (who != null && who == Clan.PlayerClan && !_playerToldWar)
                {
                    _playerToldWar = true;
                    Log.Player(_z || _final ? "The surrender still holds: the council will not hear of a new war yet." : "The truce holds until day " + _tTo + ": the council will not hear of breaking it.", true);
                }
                return false;
            }
            catch (Exception e) { Stumble("AddDecisionPrefix", e); return true; }
        }

        /// <summary>Kronika (Chronicle.Canon truce): 0 - nie dotyczy (stara droga), 1 - rozejm 330 wszedl (Done), 2 - pominiete (Skip). Tylko z kluczem 330.</summary>
        internal static int ChronicleTruce(Kingdom k1, Kingdom k2, out string detail)
        {
            detail = null;
            try
            {
                Kingdom n, c;
                if (!IsPair(k1, k2) || !Live(out n, out c)) return 0;
                if (_firstSeen < 0) _firstSeen = Today;
                string pw;
                int ph = CanonPeaceLock.Phase330(n, c, out pw);
                int ws; int wd = WarDays(n, c, out ws);
                float eN = Exh(n, c), eC = Exh(c, n);
                bool exhOk = (eN < 0f && eC < 0f) || eN >= 100f || eC >= 100f || (IsWinter() && eN >= WinterExh && eC >= WinterExh);
                Kingdom loser; string kind;
                bool due = SurrenderDue(n, c, out loser, out kind);
                string why;
                int g = Gate(ph, n.IsAtWarWith(c), PlayerRules(n, c), due, wd, MinWarDays, Today, GraceEnd, exhOk, out why);
                if (g == GateTruce && StartTruce(n, c, "kronika", TruceDays)) { detail = "rozejm 330 d." + _tFrom + "-d." + _tTo + " (wojna do kapitulacji)"; return 1; }
                detail = "wojna do kapitulacji (330): rozejm Kroniki odrzucony (" + (g == GateSurrender ? "prog kapitulacji - kapitulacja w dobie 330" : why) + ")";
                Info("blok: rozejm Kroniki odrzucony (" + why + ").");
                return 2;
            }
            catch (Exception e) { Stumble("ChronicleTruce", e); detail = "wojna do kapitulacji (330): blad"; return 2; }
        }

        // ---------------------------------------------------------------- doba

        /// <summary>Doba (WarTruceBehavior): zatrzask Z, rozejm (koniec / zima), wojna (liczniki, kapitulacja, rozejm zimowy), cesja czekajaca, linia doby. Nie rzuca.</summary>
        internal static void Daily()
        {
            try
            {
                if (!KeyOn || Campaign.Current == null) { _dSaid.Clear(); return; }
                int day = Today;
                if (_firstSeen < 0) { _firstSeen = day; Info("pierwsza doba 330 w tej kampanii - karencja rozejmu do d." + (day + GraceDays) + "."); }
                if (_final)
                {
                    // recenzja 330: cesja z kapitulacji ostatecznej dalej przetwarzana (kolejka konczy sie sama - twierdza oddana, juz nie Polnocy albo 30 dob)
                    var nf = North; var cf = Crown;
                    if (_cession.Count > 0 && nf != null && cf != null && !nf.IsEliminated && !cf.IsEliminated) CessionDaily(nf, cf);
                    if (day % 7 == 0) Info("KONIEC 330 - para Polnoc-korona poza regula (kapitulacja po Bitwie Bekartow), pokoj staly | tlumienie wojny "
                                           + (GuardOn ? "TAK" : "NIE (klucz 264 NIE / kampania klasyczna)") + ", ROT Enforced " + _dH4 + " (sesja " + _sH4 + "), blok AI wojna " + _dBlockAi + " (sesja " + _sBlockAi + ")"
                                           + (_cession.Count > 0 ? ", cesja czeka " + _cession.Count : "") + ".");
                    EndDay(); return;
                }
                Kingdom n, c;
                if (!Live(out n, out c))
                {
                    if (day % 7 == 0)
                    {
                        var nn = North; var cc = Crown;
                        Info("nie dotyczy (" + (!Prologue.CanonHere ? "kampania klasyczna" : !CanonPeaceLock.KeyOn ? "klucz 264 NIE" : nn == null || cc == null ? "brak krolestwa"
                             : "krolestwo zniszczone: " + (nn.IsEliminated ? NorthId : CrownId) + " (K4 - koniec wojny przez gre)") + ").");
                    }
                    EndDay(); return;
                }
                string pw;
                int ph = CanonPeaceLock.Phase330(n, c, out pw);
                bool player = PlayerRules(n, c);
                if (_z) ZDaily(n, c, ph, player);
                if (_final) { CessionDaily(n, c); EndDay(); return; }
                if (_tFrom >= 0) TruceDaily(n, c, ph, player);
                if (_resumePending && !n.IsAtWarWith(c) && _tFrom < 0 && !_z) Resume(n, c, "ponowienie (" + _resumeWhy + ")");
                Measure(n, c);
                if (n.IsAtWarWith(c) && _tFrom < 0 && !_z) WarDaily(n, c, ph, player);
                CessionDaily(n, c);
                if (LogOn) Log.Info(DayLine(n, c, ph, pw, player));
            }
            catch (Exception e) { Stumble("Daily", e); }
            EndDay();
        }

        private static void EndDay() { _dSaid.Clear(); _dStumbles = 0; _dBlockAi = 0; _dBlockBk = 0; _dH4 = 0; }

        private static void ZDaily(Kingdom n, Kingdom c, int ph, bool player)
        {
            int bs = CanonPeaceLock.BastardsState330;
            if (bs >= 2)
            {
                _final = true; _z = false;
                Info("KONIEC 330 (Bekarci + kapitulacja) - " + _zWhy + "; pokoj staly, para wypada z regul 330.");
                return;
            }
            int rw = Prologue.RotEventState("RedWeddingEvent"), jon = Prologue.RotEventState("JonEvent");
            bool rwDone = KingInTheNorth.RwHappened || rw >= 2, rev = RevengeReached();
            string ev = null;
            if (rwDone && !_zRwDone) ev = "RedWedding";
            else if (jon >= 1 && _zJon < 1) ev = "Jon";
            else if (rev && !_zRev) ev = "Revenge";
            if (ev == null) return;
            _z = false;
            Info("zatrzask Z skasowany (" + ev + ") -> wojna | " + _zWhy + " | faza " + PhaseName(ph) + ".");
            if (n.IsAtWarWith(c)) return;
            Resume(n, c, "zatrzask Z skasowany (" + ev + ")");
        }

        private static void TruceDaily(Kingdom n, Kingdom c, int ph, bool player)
        {
            int day = Today;
            if (n.IsAtWarWith(c)) { Info("ROZEJM koniec: wojna juz trwa (wypowiedziana poza zdarzeniem) | rozejm d." + _tFrom + "-d." + _tTo + "."); ClearTruceState(); return; }
            if (!_tExt && day >= _tTo - 1 && day < _tTo + 1)
            {
                _tExt = true;
                bool w = IsWinter();
                int add = WinterExtra(w, WinterLeft(), WinterMaxExtra);
                if (_tWhy.StartsWith("z zewnatrz", StringComparison.Ordinal) && _tTo - _tFrom <= 1) add = 0;   // rozejm 1 doby F2 bez przedluzenia
                if (add > 0)
                {
                    _tTo += add;
                    Info("ROZEJM przedluzony do wiosny: d." + _tTo + " (+" + add + ", zima" + (WinterLeft() >= 0 ? " jeszcze " + WinterLeft() + " dob" : "") + ").");
                    return;
                }
            }
            if (day < _tTo) return;
            Kingdom loser; string kind;
            if (!player && (ph == 0 || ph == 3) && SurrenderDue(n, c, out loser, out kind)) { Surrender(loser, loser == n ? c : n, kind, n, c, ph); return; }
            Resume(n, c, "ROZEJM koniec");
            if (n.IsAtWarWith(c) || player) ClearTruceState();
        }

        private static void WarDaily(Kingdom n, Kingdom c, int ph, bool player)
        {
            if (ph < 0) return;
            if (ph == 1 || ph == 2)
            {
                // recenzja 330: w F2 liczniki nie rosna - seria z F2 dawala kapitulacje ostateczna zaraz po Bekartach (kanon: zwyciestwo Polnocy); F3 liczy od swiezej serii.
                // W F2 nie ma tez rozejmu (Gate "faza 2").
                ZeroCounters("faza F2");
                _fortsN0 = _fortsC0 = -1;
                return;
            }
            StepCounters(n, c);
            Kingdom loser; string kind;
            bool due = SurrenderDue(n, c, out loser, out kind);
            if (due)
            {
                if (player) { InfoOnce("kapgracz", "prog kapitulacji " + (loser == n ? "Polnocy" : "korony") + " (" + kind + ") - gracz rzadzi strona, bez akcji."); }
                else { Surrender(loser, loser == n ? c : n, kind, n, c, ph); return; }
            }
            // rozejm zimowy (H1 Diplomacy rusza dopiero przy 100 - doba sprawdza 75/75 zima)
            if (!IsWinter()) return;
            float eN = _lastExhN, eC = _lastExhC;
            if (eN < WinterExh || eC < WinterExh) return;
            int ws; int wd = WarDays(n, c, out ws);
            string why;
            int g = Gate(ph, true, player, false, wd, MinWarDays, Today, GraceEnd, true, out why);
            if (g == GateTruce) StartTruce(n, c, "zima", TruceDays);
            else InfoOnce("zimablok", "blok: rozejm zimowy odrzucony (" + why + ") | zmeczenie " + F0(eN) + "/" + F0(eC) + ".");
        }

        // ---------------------------------------------------------------- linia doby i start sesji

        private static string DayLine(Kingdom n, Kingdom c, int ph, string pw, bool player)
        {
            var sb = new StringBuilder(500);
            sb.Append(Tag).Append("dzien ").Append(Today).Append(" | faza ").Append(PhaseName(ph)).Append(" (").Append(pw).Append(')');
            int ws; int wd = WarDays(n, c, out ws);
            if (wd < 0) sb.Append(" | wojna NIE");
            else if (wd == int.MaxValue) sb.Append(" | wojna TAK (bez daty)");
            else sb.Append(" | wojna od d.").Append(ws).Append(" (").Append(wd).Append(" dob)");
            sb.Append(" | zmeczenie ").Append(F0(_lastExhN)).Append('/').Append(F0(_lastExhC)).Append(IsWinter() ? " (zima)" : "");
            if (_tFrom >= 0) sb.Append(" | rozejm do d.").Append(_tTo).Append(" nr ").Append(_tCount).Append(" (").Append(_tWhy).Append(", danina ").Append(_tPayer.Length > 0 ? _tPayer + "->" + _tRecv + " " + _tDaily + "/doba" : "brak").Append(')');
            else sb.Append(" | rozejm NIE (nastepny od d.").Append(NextTruceDay(ws, GraceEnd)).Append(')');
            if (_z) sb.Append(" | zatrzask Z od d.").Append(_zDay).Append(" (").Append(_zWhy).Append(')');
            sb.Append(" | Polnoc K1 ").Append(_k[0]).Append('/').Append(HoldDays).Append(" K2 ").Append(_k[1]).Append('/').Append(CaptiveDays).Append(" K3 ").Append(_k[2]).Append('/').Append(RatioDays)
              .Append(", tw. ").Append(_lastFN).Append(" (start ").Append(_fortsN0).Append(')');
            sb.Append(" | korona K1 ").Append(_k[3]).Append('/').Append(HoldDays).Append(" K2 ").Append(_k[4]).Append('/').Append(CaptiveDays).Append(" K3 -")
              .Append(", tw. ").Append(_lastFC).Append(" (start ").Append(_fortsC0).Append(')');
            sb.Append(" | sila Polnoc ").Append(_lastSN.ToString("0", Inv)).Append(", korona ").Append(_lastSC.ToString("0", Inv)).Append(", Polnoc/korona ").Append(_lastRN.ToString("0.00", Inv));
            bool has, forced;
            RevengeInfo(out has, out forced);
            sb.Append(" | ROT Revenge: ").Append(has ? "jest" : "BRAK").Append(", wymusza ").Append(forced ? "tak" : "nie");
            int ge = GraceEnd;
            sb.Append(" | karencja ").Append(Today < ge ? "do d." + ge : "minela");
            if (_cession.Count > 0) sb.Append(" | cesja czeka ").Append(_cession.Count);
            if (_resumePending) sb.Append(" | wznowienie czeka (").Append(_resumeWhy).Append(')');
            sb.Append(" | blok AI wojna ").Append(_dBlockAi).Append(" (sesja ").Append(_sBlockAi).Append("), ocena BK ").Append(_dBlockBk).Append(" (sesja ").Append(_sBlockBk).Append("), ROT Enforced ").Append(_dH4).Append(" (sesja ").Append(_sH4).Append(')');
            sb.Append(" | gracz rzadzi: ").Append(player ? "tak" : "nie").Append(" | potkniecia ").Append(_dStumbles).Append(" (sesja ").Append(_stumbles).Append(").");
            return sb.ToString();
        }

        /// <summary>Wpis ROT Revenge (Polnoc-Westerlands, bez Id, StartDay > 0) w liscie wojen i czy ROT wymusza teraz te pare (bez H4).</summary>
        private static void RevengeInfo(out bool has, out bool forced)
        {
            has = false; forced = false;
            try
            {
                foreach (var w in Prologue.RotWars330())
                {
                    string id; int sd, ed; bool trig, isTrig, en;
                    if (!Prologue.War330Pair(w) || !Prologue.WarProps330(w, out id, out sd, out ed, out trig, out isTrig, out en)) continue;
                    if (id == "NONE" && sd > 0 && !trig) has = true;
                }
                var n = North; var c = Crown;
                _bypass = true;
                try { forced = n != null && c != null && Prologue.RotWarForced(n, c); }
                finally { _bypass = false; }
            }
            catch (Exception e) { Stumble("RevengeInfo", e); }
        }

        /// <summary>Start sesji: latki, klucz, jednorazowo wpisy ROT tej pary (Revenge i l.71) i stan z zapisu.</summary>
        internal static void SessionStart()
        {
            try { EnsureHooks(); } catch (Exception e) { Stumble("EnsureHooks", e); }
            try
            {
                var sb = new StringBuilder(400);
                sb.Append(Tag).Append("start sesji | latki: ROT Enforced (H4) ").Append(_wH4).Append(", AddDecision wojna (H5) ").Append(_wH5).Append(", call-to-war (H5b) ").Append(_wH5b)
                  .Append(", ocena BK (CanonThroneWars) ").Append(CanonThroneWars.WireText)
                  .Append(" | klucz Canon War Until Surrender ").Append(KeyOn ? "tak" : "nie").Append(_final ? " (KONIEC 330 - para poza regula)" : "")
                  .Append(" | zakres: ").Append(Prologue.CanonHere ? "kampania kanonu" : "kampania klasyczna - nie dotyczy")
                  .Append(" | rozejm ").Append(TruceDays).Append(" dob (+").Append(WinterMaxExtra).Append(" zima), wojna >= ").Append(MinWarDays).Append(", karencja ").Append(GraceDays)
                  .Append(", zima ").Append(WinterExh.ToString("0", Inv)).Append(" | kapitulacja K1 ").Append(HoldDays).Append(" K2 ").Append(CaptiveDays).Append(" K3 ").Append(RatioDays)
                  .Append(" (tylko Polnoc), sila < ").Append(LoseRatio.ToString("0.00", Inv)).Append(" / ").Append(LowRatio.ToString("0.00", Inv))
                  .Append(" | zapis: pierwsza doba ").Append(_firstSeen).Append(", rozejm ").Append(_tFrom >= 0 ? "d." + _tFrom + "-d." + _tTo + " (" + _tWhy + ")" : "NIE")
                  .Append(", zatrzask Z ").Append(_z ? "TAK (" + _zWhy + ")" : "NIE").Append(", zle wpisy ").Append(_importBad)
                  .Append(" | wpisy ROT pary: ").Append(RotEntriesText()).Append('.').Append(WarTruceTest.StartText());
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Stumble("SessionStart", e); }
        }

        private static string RotEntriesText()
        {
            var l = new List<string>();
            bool revenge = false;
            foreach (var w in Prologue.RotWars330())
            {
                string id; int sd, ed; bool trig, isTrig, en;
                if (!Prologue.War330Pair(w) || !Prologue.WarProps330(w, out id, out sd, out ed, out trig, out isTrig, out en)) continue;
                bool rev = id == "NONE" && sd > 0 && !trig;
                if (rev) revenge = true;
                l.Add((rev ? "Revenge" : id == "FIVE_KINGS_NORTH_WESTERLANDS" ? "l.71 " + id : id) + " (start " + sd + ", koniec " + ed + ", wyzwalacz " + (trig ? (isTrig ? "TAK-wyzwolony" : "TAK-czeka") : "nie")
                      + ", wlaczona " + (en ? "tak" : "nie") + ")");
            }
            if (l.Count == 0) return "BRAK (ROT bez listy albo bez wpisow Polnoc-Westerlands)";
            return (revenge ? "Revenge jest" : "Revenge BRAK") + " [" + string.Join("; ", l.ToArray()) + "]";
        }

        // ---------------------------------------------------------------- zapis

        internal static string Export()
        {
            var sb = new StringBuilder(300);
            sb.Append("v1|").Append(_firstSeen).Append('|').Append(_tFrom).Append('|').Append(_tTo).Append('|').Append(Clean(_tWhy)).Append('|').Append(_tCount).Append('|').Append(_tExt ? 1 : 0)
              .Append('|').Append(Clean(_tPayer)).Append('|').Append(Clean(_tRecv)).Append('|').Append(_tDaily).Append('|');
            for (int i = 0; i < _k.Length; i++) { if (i > 0) sb.Append(','); sb.Append(_k[i]); }
            sb.Append('|').Append(_fortsN0).Append('|').Append(_fortsC0).Append('|').Append(Clean(_leadN)).Append('|').Append(Clean(_leadC))
              .Append('|').Append(_z ? 1 : 0).Append('|').Append(Clean(_zWhy)).Append('|').Append(_zDay).Append('|').Append(_zRw).Append('|').Append(_zJon).Append('|').Append(_zRwDone ? 1 : 0).Append('|').Append(_zRev ? 1 : 0)
              .Append('|').Append(_final ? 1 : 0).Append('|').Append(_resumePending ? 1 : 0).Append('|').Append(Clean(_resumeWhy)).Append('|').Append(string.Join(";", _cession.ToArray()));
            return sb.ToString();
        }

        private static int I(string s, int def) { int v; return int.TryParse(s, NumberStyles.Integer, Inv, out v) ? v : def; }

        /// <summary>Wczytanie (stara gra bez wpisu - pusty napis - stan pusty; zly wpis - stan pusty i licznik).</summary>
        internal static void Import(string v)
        {
            ResetSaved();
            if (string.IsNullOrEmpty(v)) return;
            var f = v.Split('|');
            if (f.Length < 26 || f[0] != "v1") { _importBad++; return; }
            _firstSeen = I(f[1], -1); _tFrom = I(f[2], -1); _tTo = I(f[3], -1); _tWhy = f[4]; _tCount = I(f[5], 0); _tExt = f[6] == "1";
            _tPayer = f[7]; _tRecv = f[8]; _tDaily = I(f[9], 0);
            var ks = f[10].Split(',');
            for (int i = 0; i < _k.Length && i < ks.Length; i++) _k[i] = I(ks[i], 0);
            _fortsN0 = I(f[11], -1); _fortsC0 = I(f[12], -1); _leadN = f[13]; _leadC = f[14];
            _z = f[15] == "1"; _zWhy = f[16]; _zDay = I(f[17], -1); _zRw = I(f[18], -9); _zJon = I(f[19], -9); _zRwDone = f[20] == "1"; _zRev = f[21] == "1";
            _final = f[22] == "1"; _resumePending = f[23] == "1"; _resumeWhy = f[24];
            if (f[25].Length > 0) foreach (var x in f[25].Split(';')) if (x.Length > 0) _cession.Add(x);
            if (_tFrom >= 0 && _tTo < _tFrom) { ClearTruceState(); _importBad++; }
        }
    }

    /// <summary>330: odczyty listy wojen ROT (pola prywatne Prologue.Wars) bez zmiany ich widocznosci.</summary>
    internal static partial class Prologue
    {
        private static readonly ConditionalWeakTable<object, object> _p330 = new ConditionalWeakTable<object, object>();
        private static readonly ConditionalWeakTable<object, object>.CreateValueCallback _p330Calc = P330Calc;
        private static PropertyInfo _p330Start, _p330End, _p330Trig, _p330IsTrig, _p330En;

        private static object P330Calc(object war)
        {
            try { return WarTruce.IsPair(WF1(war), WF2(war)) ? Yes : No; } catch { return No; }
        }

        /// <summary>330 H4: ROTStorylineWar.Enforced (to samo MethodInfo co postfiksy Feuds / Dorzecza); null - ROT nieobecny.</summary>
        internal static MethodInfo EnforcedMethod330() { return RotWarsReady() ? _mWEnforced : null; }

        /// <summary>Wpis wojny ROT miedzy Polnoca a korona (Westerlands); strony wpisu sie nie zmieniaja - wynik w pamieci na obiekt.</summary>
        internal static bool War330Pair(object war) { return war != null && RotWarsReady() && (bool)_p330.GetValue(war, _p330Calc); }

        internal static List<object> RotWars330() { return RotWars(); }

        /// <summary>Pola wpisu ROT (Id, StartDay, EndDay, HasTrigger, IsTriggered, IsEnabled) - surowe wartosci z zapisu (poza wywolaniem Enforced Fabula ich nie skaluje).</summary>
        internal static bool WarProps330(object war, out string id, out int start, out int end, out bool trig, out bool isTrig, out bool en)
        {
            id = "?"; start = end = 0; trig = isTrig = en = false;
            try
            {
                var t = war.GetType();
                if (_p330Start == null)
                {
                    _p330Start = AccessTools.Property(t, "StartDay"); _p330End = AccessTools.Property(t, "EndDay"); _p330Trig = AccessTools.Property(t, "HasTrigger");
                    _p330IsTrig = AccessTools.Property(t, "IsTriggered"); _p330En = AccessTools.Property(t, "IsEnabled");
                }
                if (_p330Start == null || _p330End == null || _p330Trig == null) return false;
                start = Convert.ToInt32(_p330Start.GetValue(war, null), CultureInfo.InvariantCulture);
                end = Convert.ToInt32(_p330End.GetValue(war, null), CultureInfo.InvariantCulture);
                trig = (bool)_p330Trig.GetValue(war, null);
                isTrig = _p330IsTrig != null && (bool)_p330IsTrig.GetValue(war, null);
                en = _p330En != null && (bool)_p330En.GetValue(war, null);
                var idv = _pWId != null ? _pWId.GetValue(war, null) : null;
                id = idv != null ? idv.ToString() : "?";
                return true;
            }
            catch { return false; }
        }
    }

    /// <summary>330: zachowanie kampanii - start sesji (latki, linia), doba, godzina (autotest), zapis arm_k330. Rejestrowane po CanonWarsBehavior i KingInTheNorthBehavior.</summary>
    internal sealed class WarTruceBehavior : CampaignBehaviorBase
    {
        public WarTruceBehavior() { WarTruce.Reset(); }

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, delegate (CampaignGameStarter s) { try { WarTruce.SessionStart(); } catch (Exception e) { WarTruce.Stumble("SessionStart", e); } });
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, delegate { try { WarTruce.Daily(); } catch (Exception e) { WarTruce.Stumble("Daily", e); } });
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, delegate { try { WarTruceTest.Hourly(); } catch (Exception e) { WarTruce.Stumble("Test.Hourly", e); } });
        }

        public override void SyncData(IDataStore dataStore)
        {
            string v = "";
            try { if (dataStore.IsSaving) v = WarTruce.Export(); } catch (Exception e) { WarTruce.Stumble("Export", e); v = ""; }
            SaveText.Sync(dataStore, WarTruce.SaveKey, ref v);
            if (dataStore.IsLoading) { try { WarTruce.Import(v); } catch (Exception e) { WarTruce.Stumble("Import", e); } }
        }
    }
}
