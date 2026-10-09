using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Election;
using TaleWorlds.Localization;

namespace CrashScribe
{
    /// <summary>
    /// POKOJ Z BIEDY (E1 noc 09/10.10; Jeff 09.10 lista A-L, punkt E "pokoj z biedy TAK"; audyt 09.10 05-D3 / 04-W6, E13).
    /// Krolestwo z pustym skarbcem i biednymi wasalami chetniej zawiera pokoj i rzadziej wypowiada wojne.
    /// Wojny fabularne ROT (ROTStorylineWars.IsWarForced) bez zmian - ROT i tak je wymusza.
    ///
    /// Gdzie AI decyduje (dekompilacja gry 1.4.8, BK, BKROTPatch, Diplomacy 1.4.7, ROT 8.1.8; StrategicCampaignAI145 nie ma
    /// ani jednego odwolania do wojny/pokoju):
    ///  - BK ModCompat.DiplomacyMod szuka modulu o Id "Diplomacy" albo DLL "DiplomacyFixes"; u Jeffa Id to "Bannerlord.Diplomacy",
    ///    a DLL "Bannerlord.Diplomacy.1.4.7" - dla BK Diplomacy NIE ma. Skutek: BK Main dodaje BKDiplomacyModel jako model gry
    ///    (Main.cs:222 tylko przy !DiplomacyMod) i jego postfiks glosu o pokoj (DiplomacyPatches: +-240 x nacisk pokoju) dziala.
    ///    Linia startu sesji wypisuje typ modelu gry i wlascicieli postfiksow glosu - autotest to potwierdza.
    ///  - Wojna: KingdomDecisionProposalBehavior.GetRandomWarDecision (prog DeclareWarBarterable >= 0) -> ConsiderWar
    ///    (Diplomacy: tylko warunki; DeclareWarDecision.CalculateSupport > 50) -> BK podmienia na BKDeclareWarDecision -> glos rodow.
    ///    Wszystkie te liczby biora sie z jednej funkcji: BKDiplomacyModel.GetScoreOfDeclaringWar (6 arg., ExplainedNumber) - model
    ///    gry i BannerKingsConfig.DiplomacyModel (BKROTDiplomacyModel jej nie nadpisuje). Ta sama funkcja daje poparcie wojny
    ///    w nacisku pokoju BK (BKDiplomacyBehavior.GetWarSupport -> CalculatePeacePressure), ale NIEPEWNIE: w trwajacej wojnie
    ///    BK liczy tam BKDeclareWarDecision dla pary juz w wojnie; gdy model zgod gry (KingdomDecisionPermissionModel) to
    ///    Diplomacy z warunkiem AtPeace, BK daje -50000 i nasz czlon tam nie dziala (pomijamy -50000); gdy model zgod BK -
    ///    dziala, i wtedy bieda skarbca wchodzi do glosu o pokoj takze przez nacisk BK (obok naszego czlonu glosu, ktory
    ///    i tak nie wychodzi ponad 200). Linia startu sesji wypisuje model zgod - autotest rozstrzyga, ktory przypadek.
    ///  - Pokoj: wniosek (ConsiderPeace: prog BK GetScoreOfDeclaringPeace >= 0, potem MakePeaceKingdomDecision.DetermineSupport
    ///    wnioskodawcy > 0; albo wniosek BK ForceProposePeaceFromLosingSide przy nacisku >= 0.5) -> glos rodow
    ///    MakePeaceKingdomDecision.DetermineSupport (vanilla 0 albo 200, za + przeciw = 200; BK dodaje +-240 x nacisk, suma dalej 200)
    ///    -> Diplomacy ApplyChosenOutcome -> KingdomPeaceAction.ApplyPeace. Osobno: zmeczenie Diplomacy (100) wymusza pokoj bez glosu.
    ///  - Dlaczego nie jeden czlon w wyniku wojny BK: wynik pokoju BK (= -wynik wojny + zmeczenie) glos vanilla porownuje
    ///    z wynikiem rodu w skali gry ("rod za pokojem, gdy jego wynik > wynik krolestwa") - podniesienie wyniku pokoju BK
    ///    otwiera prog, ale czesci rodow odbiera glos za pokojem. Kierunek niepewny, wiec pokoj dostaje czlon w samym glosie,
    ///    a wynik pokoju BK zostaje bez zmian (postfiks wojny pomija wywolanie z GetScoreOfDeclaringPeace: oceniajacy == krolestwo).
    ///    Kazde zjawisko ma jeden czlon: wojna - w ocenie wypowiedzenia, pokoj - w glosie. Zmeczenie Diplomacy, nacisk BK, ROT - bez zmian.
    ///
    /// Miara biedy (raz na dobe, krolestwa nie zniszczone):
    ///  - bieda skarbca T: zapas = skarbiec / max(sredni ubytek z 7 dob, 1000 zl/d); zapas <= 28 dob -> 1, >= 91 dob -> 0, liniowo.
    ///    (miesiac zoldu / sezon kampanii w roku Armoury 364 = 4 x 91; 1000 zl/d - prawie pusty skarbiec bez ruchu tez jest pusty).
    ///  - bieda rodu P: kiesa glowy na kazda wojne krolestwa (jak BK "cannot afford a war": zloto / (1 + wrogie krolestwa));
    ///    < 5000 -> 1 (prog zapomogi korony w grze), >= 50000 -> 0 (prog BK), liniowo.
    ///  - log: udzial glow < 5000 i rodow "za pokojem z biedy" (T + P > 1).
    /// Czlony:
    ///  - wojna: wynik BK += -0.75 x T x max(|wynik|, 600) (skala BK: 600 = jej minimum, 0.75 = waga zmeczenia w wyniku pokoju BK).
    ///    Bez czlonu P - BK ma juz "cannot afford a war" dla glowy oceniajacego rodu (nie liczymy drugi raz).
    ///    Znak zmienia tylko slabym wojnom (wynik < 450 x T); przy wyniku >= 600 zostaje s x (1 - 0.75T) > 0 - mniej wojen
    ///    na granicy oplacalnosci i slabsze glosy, nie mniej wojen w ogole. Linia dobowa liczy oceny, ktorym czlon zmienil znak.
    ///  - glos o pokoju (ai rod, nie gracz, nie najemnik): gdy T + P > 1 - za pokojem +100 x (T + P), przeciw -100 x (T + P),
    ///    ale bez wyjscia poza wiekszy z (wynik, 200) dla "za" i mniejszy z (wynik, 0) dla "przeciw": rod przekonany juz wczesniej
    ///    (przez gre albo nacisk BK) nie dostaje wiecej, a mocno wojowniczy (nacisk BK < 0) moze zostac przy wojnie. Postfiks
    ///    z Priority.Last: BK zaklada swoje latki dopiero w Main.OnGameStart, a my w menu glownym - przy rownej wadze (400)
    ///    Harmony puscilby nasz postfiks PRZED BK; z Priority.Last idzie po nim i widzi jego nacisk. Bogaty rod w biednym
    ///    krolestwie i biedny w bogatym - bez zmian. Przy T + P > 1 zawsze x > 100, wiec rod przy grze 0/200 zawsze zmienia
    ///    strone; "im biedniej, tym mocniej" dotyczy wagi glosu (wplyw), nie strony. T > 0 wystarczy rodowi z P bliskim 1.
    ///    Wniosek o pokoj (ConsiderPeace: glos wnioskodawcy > 0) nasz czlon dodaje tylko przy T + P > 1.
    ///    Gdy gra uznaje pokoj za nieodpowiedni (IsPeaceSuitable == false: wojna < 150 dob i wrog daleko w przodzie) i wniosek
    ///    nie jest od wroga - czlonu nie ma (gra daje wtedy 0/200; nie otwieramy pokoju, ktorego gra by nie dopuscila).
    /// Bledy: try/catch per wywolanie (wynik gry zostaje), potkniecia liczone (pierwsze w raporcie), latka dziala dalej.
    /// Wylacznik: PovertyPeace w ModuleData/CrashScribe.settings.xml. Bez zapisu w grze (historia skarbca od wczytania).
    /// </summary>
    internal static class PovertyPeace
    {
        private const float RunwayEmpty = 28f;
        private const float RunwayFull = 91f;
        private const float DrainFloor = 1000f;
        private const int DrainDays = 7;
        private const float HeadPoor = 5000f;
        private const float HeadRich = 50000f;
        private const float WarWeight = 0.75f;
        private const float WarFloor = 600f;
        private const float VoteUnit = 100f;
        private const float VoteFull = 200f;
        private const float BkForbidden = -49999f;   // BK: -50000 = wojna niedozwolona / ta sama frakcja
        private const double FlipKeepDays = 6.0;     // decyzja w krolestwie gracza zyje do rozstrzygniecia; AI glosuje od razu (Kingdom.AddDecision -> StartElection)
        // Odwrocenia licza sie tez z ConsiderPeace (glos wnioskodawcy) i obliczen prawdopodobienstwa - licznik przyblizony (tylko log).
        // Filtr "decyzja w k.UnresolvedDecisions" nie zadziala: decyzje krolestw AI nigdy tam nie trafiaja.

        private static bool _voteOn, _warOn, _rotOn;
        private static FieldInfo _fWars;     // ROT SubModule.StorylineWars
        private static MethodInfo _mForced;  // ROT ROTStorylineWars.IsWarForced(IFaction, IFaction)
        private static TextObject _warText;
        private static MethodInfo _mVote, _mWar;   // cele latek - do linii startu sesji (kto jeszcze je latka)
        private static AccessTools.FieldRef<MakePeaceKingdomDecision, bool> _fOpp;   // gra: _isProposedByOpponent (brak = traktujemy jak false)

        private sealed class KState
        {
            public readonly List<KeyValuePair<int, int>> Hist = new List<KeyValuePair<int, int>>();   // (doba, skarbiec >= 0)
            public float T, Drain, Runway;
            public int W, Enemies, Clans, Poor, ForPeace;
        }
        private sealed class Flips { public readonly HashSet<Clan> Clans = new HashSet<Clan>(); public CampaignTime Last; }

        private static readonly object _lock = new object();
        private static readonly Dictionary<Kingdom, KState> _k = new Dictionary<Kingdom, KState>();
        private static readonly Dictionary<(Kingdom, Kingdom), bool> _forced = new Dictionary<(Kingdom, Kingdom), bool>();
        private static readonly Dictionary<(Kingdom, Kingdom), Flips> _flips = new Dictionary<(Kingdom, Kingdom), Flips>();

        // dzis / od wczytania
        private static int _dWar, _tWar, _dFlip, _tFlip, _dPeaceDec, _tPeaceDec, _dPeacePoor, _tPeacePoor, _dPeaceYes, _tPeaceYes, _dPeaceYesPoor, _tPeaceYesPoor;
        private static int _dWarDec, _tWarDec, _dWarPoor, _tWarPoor, _dWarYes, _tWarYes, _dWarYesPoor, _tWarYesPoor;
        private static int _dWarSign, _tWarSign, _dUnsuit, _tUnsuit;   // oceny wojny ze zmienionym znakiem; glosy bez czlonu (gra: pokoj nieodpowiedni)
        private static double _dWarSum, _tWarSum;
        private static int _stumbles, _stumblesDay;

        private static void Stumble(string where, Exception e)
        {
            lock (_lock) { _stumbles++; _stumblesDay++; }
            if (_stumbles == 1) { try { Scribe.Report("CrashScribe", e, where, null); } catch { } }
        }

        internal static void Install(Harmony harmony)
        {
            try
            {
                if (!Config.PovertyPeace)
                {
                    Scribe.Line("Pokoj z biedy (E1): wylaczone (PovertyPeace = false) - decyzje o wojnie i pokoju jak w grze i modach.");
                    return;
                }
                var miss = new List<string>();
                var mVote = AccessTools.Method(typeof(MakePeaceKingdomDecision), "DetermineSupport", new[] { typeof(Clan), typeof(DecisionOutcome) });
                if (mVote != null)
                {
                    // Priority.Last: BK zaklada latki dopiero w OnGameStart (po nas) - przy rownej wadze bylibysmy przed jego naciskiem
                    harmony.Patch(mVote, postfix: new HarmonyMethod(typeof(PovertyPeace), nameof(PeaceVotePostfix)) { priority = Priority.Last });
                    _voteOn = true; _mVote = mVote;
                    try { if (AccessTools.Field(typeof(MakePeaceKingdomDecision), "_isProposedByOpponent") != null) _fOpp = AccessTools.FieldRefAccess<MakePeaceKingdomDecision, bool>("_isProposedByOpponent"); } catch { _fOpp = null; }
                    if (_fOpp == null) miss.Add("gra MakePeaceKingdomDecision._isProposedByOpponent (wniosek wroga traktowany jak wlasny)");
                }
                else miss.Add("gra MakePeaceKingdomDecision.DetermineSupport");

                var tBk = AccessTools.TypeByName("BannerKings.Models.Vanilla.BKDiplomacyModel");
                MethodInfo mWar = null;
                if (tBk != null)
                    foreach (var m in AccessTools.GetDeclaredMethods(tBk))
                    {
                        if (m.Name != "GetScoreOfDeclaringWar" || m.ReturnType != typeof(ExplainedNumber)) continue;
                        var ps = m.GetParameters();
                        if (ps.Length == 6 && ps[0].ParameterType == typeof(IFaction) && ps[1].ParameterType == typeof(IFaction) && ps[2].ParameterType == typeof(IFaction)) { mWar = m; break; }
                    }
                if (mWar != null)
                {
                    harmony.Patch(mWar, postfix: new HarmonyMethod(typeof(PovertyPeace), nameof(WarScorePostfix)));
                    _warOn = true; _mWar = mWar;
                }
                else miss.Add(tBk == null ? "BK nieobecny (brak BKDiplomacyModel) - czlon wojny niewpiety" : "BK BKDiplomacyModel.GetScoreOfDeclaringWar(IFaction, IFaction, IFaction, ...)");

                var tWars = AccessTools.TypeByName("ROT.CampaignBehaviors.ROTStorylineWars");
                if (tWars != null)
                {
                    var tSub = tWars.Assembly.GetType("ROT.SubModule");
                    _fWars = tSub != null ? AccessTools.Field(tSub, "StorylineWars") : null;
                    _mForced = AccessTools.Method(tWars, "IsWarForced", new[] { typeof(IFaction), typeof(IFaction) });
                    _rotOn = _fWars != null && _mForced != null && _mForced.ReturnType == typeof(bool);
                }
                if (!_rotOn) miss.Add("ROT IsWarForced (wojny fabularne nierozpoznawane - czlon dziala na wszystkie wojny)");

                int ok = (_voteOn ? 1 : 0) + (_warOn ? 1 : 0);
                Scribe.Line("Pokoj z biedy (E1): wpiete " + ok + "/2 - " + (_voteOn ? "glos rodu o pokoj (MakePeaceKingdomDecision.DetermineSupport, Priority.Last - po nacisku BK: gdy bieda skarbca + bieda rodu > 1, za pokojem 100 x suma; bez czlonu, gdy gra uznaje pokoj za nieodpowiedni)" : "glos o pokoj NIE")
                            + "; " + (_warOn ? "ocena wypowiedzenia wojny BK (GetScoreOfDeclaringWar: -0.75 x bieda skarbca x max(|wynik|, 600); wynik pokoju BK bez zmian)" : "ocena wojny NIE")
                            + "; bieda skarbca: zapas <= " + RunwayEmpty + " dob = 1, >= " + RunwayFull + " dob = 0; bieda rodu: kiesa na wojne < " + HeadPoor + " = 1, >= " + HeadRich + " = 0"
                            + "; wojny fabularne ROT " + (_rotOn ? "bez zmian" : "NIEROZPOZNAWANE") + (miss.Count > 0 ? " | brak: " + string.Join("; ", miss.ToArray()) : "") + ".");
            }
            catch (Exception e) { Stumble("PovertyPeace.Install", e); }
        }

        private static bool On { get { return (_voteOn || _warOn) && Config.PovertyPeace; } }

        /// <summary>Postfiks BKDiplomacyModel.GetScoreOfDeclaringWar(declarer, target, evaluator, out reason, casusBelli, explanations).</summary>
        public static void WarScorePostfix(IFaction __0, IFaction __1, IFaction __2, ref ExplainedNumber __result)
        {
            if (!_warOn || !Config.PovertyPeace) return;
            try
            {
                if (__2 != null && ReferenceEquals(__2, __0)) return;   // wywolanie z GetScoreOfDeclaringPeace BK - pokoj ma czlon w glosie
                var k = __0 as Kingdom;
                var t = __1 as Kingdom;
                if (k == null || t == null || k == t || k.IsEliminated) return;
                float s = __result.ResultNumber;
                if (s <= BkForbidden) return;
                float T; int en;
                if (!Poverty(k, out T, out en) || T <= 0f) return;
                if (Forced(k, t)) return;
                float term = -WarWeight * T * Math.Max(Math.Abs(s), WarFloor);
                if (_warText == null) _warText = new TextObject("{=!}The royal treasury cannot pay for a war");
                __result.Add(term, _warText);
                bool sign = s > 0f && s + term <= 0f;   // czlon zmienil znak oceny (tylko slabe wojny: s < 450 x T)
                lock (_lock) { _dWar++; _tWar++; _dWarSum += term; _tWarSum += term; if (sign) { _dWarSign++; _tWarSign++; } }
            }
            catch (Exception e) { Stumble("PovertyPeace.WarScorePostfix", e); }
        }

        /// <summary>Postfiks MakePeaceKingdomDecision.DetermineSupport(clan, outcome).</summary>
        public static void PeaceVotePostfix(MakePeaceKingdomDecision __instance, Clan __0, DecisionOutcome __1, ref float __result)
        {
            if (!_voteOn || !Config.PovertyPeace) return;
            try
            {
                var clan = __0;
                var o = __1 as MakePeaceKingdomDecision.MakePeaceDecisionOutcome;
                if (__instance == null || clan == null || o == null || clan == Clan.PlayerClan || clan.IsUnderMercenaryService || clan.Leader == null) return;
                var k = __instance.Kingdom;
                var e = __instance.FactionToMakePeaceWith as Kingdom;
                if (k == null || e == null || k == e) return;
                float T; int en;
                if (!Poverty(k, out T, out en) || T <= 0f) return;
                float w = T + ClanPoverty(clan.Gold, en);
                if (w <= 1f) return;
                if (Forced(k, e)) return;
                // jak gra: pokoj nieodpowiedni (mloda wojna, wrog daleko w przodzie) i wniosek nie od wroga - bez czlonu
                bool opp = _fOpp != null && _fOpp(__instance);
                if (!opp && !Campaign.Current.Models.DiplomacyModel.IsPeaceSuitable(k, e))
                {
                    lock (_lock) { _dUnsuit++; _tUnsuit++; }
                    return;
                }
                float x = VoteUnit * Math.Min(w, 2f);
                float orig = __result;
                if (o.ShouldPeaceBeDeclared)
                {
                    __result = Yes(orig, x);
                    // glos "przeciw" tego rodu: gra 0/200 i nacisk BK +-240 x p zawsze daja za + przeciw = 200
                    float noOrig = VoteFull - orig;
                    if (!(orig > noOrig) && __result > No(noOrig, x)) Flip(k, e, clan);
                }
                else __result = No(orig, x);
            }
            catch (Exception ex) { Stumble("PovertyPeace.PeaceVotePostfix", ex); }
        }

        private static float Yes(float orig, float x) { return Math.Min(orig + x, Math.Max(orig, VoteFull)); }
        private static float No(float orig, float x) { return Math.Max(orig - x, Math.Min(orig, 0f)); }

        private static float ClanPoverty(int gold, int enemies)
        {
            float perWar = Math.Max(0, gold) / (1f + Math.Max(0, enemies));
            return Clamp01((HeadRich - perWar) / (HeadRich - HeadPoor));
        }

        private static float Clamp01(float v) { return v < 0f ? 0f : (v > 1f ? 1f : v); }

        /// <summary>Bieda skarbca krolestwa i liczba wrogich krolestw (stan z linii dobowej; przed pierwsza - z samego skarbca).</summary>
        private static bool Poverty(Kingdom k, out float T, out int enemies)
        {
            lock (_lock)
            {
                KState s;
                if (_k.TryGetValue(k, out s)) { T = s.T; enemies = s.Enemies; return true; }
            }
            int w = Math.Max(0, k.KingdomBudgetWallet);
            T = Clamp01((RunwayFull - w / DrainFloor) / (RunwayFull - RunwayEmpty));
            enemies = FactionHelper.GetEnemyKingdoms(k).Count();
            return true;
        }

        private static bool Forced(Kingdom a, Kingdom b)
        {
            if (!_rotOn) return false;
            lock (_lock)
            {
                bool r;
                if (_forced.TryGetValue((a, b), out r)) return r;
                r = false;
                object wars = _fWars.GetValue(null);
                if (wars != null) { object x = _mForced.Invoke(wars, new object[] { a, b }); r = x is bool && (bool)x; }
                _forced[(a, b)] = r;
                return r;
            }
        }

        private static void Flip(Kingdom k, Kingdom e, Clan c)
        {
            lock (_lock)
            {
                Flips f;
                if (!_flips.TryGetValue((k, e), out f)) { f = new Flips(); _flips[(k, e)] = f; }
                f.Last = CampaignTime.Now;
                if (f.Clans.Add(c)) { _dFlip++; _tFlip++; }
            }
        }

        private static void Update(KState s, Kingdom k, int absDay)
        {
            int w = k.KingdomBudgetWallet;
            int wp = Math.Max(0, w);
            s.W = w;
            if (s.Hist.Count > 0 && s.Hist[s.Hist.Count - 1].Key == absDay) s.Hist.RemoveAt(s.Hist.Count - 1);
            s.Hist.Add(new KeyValuePair<int, int>(absDay, wp));
            while (s.Hist.Count > 1 && s.Hist[0].Key < absDay - DrainDays) s.Hist.RemoveAt(0);
            int span = absDay - s.Hist[0].Key;
            float drain = span > 0 ? (s.Hist[0].Value - wp) / (float)span : 0f;
            s.Drain = drain > 0f ? drain : 0f;
            s.Runway = wp / Math.Max(s.Drain, DrainFloor);
            s.T = Clamp01((RunwayFull - s.Runway) / (RunwayFull - RunwayEmpty));
            s.Enemies = FactionHelper.GetEnemyKingdoms(k).Count();
            int clans = 0, poor = 0, forPeace = 0;
            foreach (var c in k.Clans)
            {
                if (c == null || c.IsEliminated || c.Leader == null || c.IsUnderMercenaryService) continue;
                clans++;
                if (c.Gold < HeadPoor) poor++;
                if (c != Clan.PlayerClan && s.T + ClanPoverty(c.Gold, s.Enemies) > 1f) forPeace++;
            }
            s.Clans = clans; s.Poor = poor; s.ForPeace = forPeace;
        }

        /// <summary>Stan wszystkich krolestw (start sesji i linia dobowa).</summary>
        private static List<KeyValuePair<Kingdom, KState>> Refresh()
        {
            var list = new List<KeyValuePair<Kingdom, KState>>();
            int absDay = (int)CampaignTime.Now.ToDays;
            lock (_lock)
            {
                _forced.Clear();
                var seen = new HashSet<Kingdom>();
                foreach (var k in Kingdom.All)
                {
                    if (k == null || k.IsEliminated) continue;
                    try
                    {
                        KState s;
                        if (!_k.TryGetValue(k, out s)) { s = new KState(); _k[k] = s; }
                        Update(s, k, absDay);
                        seen.Add(k);
                        list.Add(new KeyValuePair<Kingdom, KState>(k, s));
                    }
                    catch (Exception e) { _k.Remove(k); Stumble("PovertyPeace.Update", e); }
                }
                foreach (var dead in _k.Keys.Where(x => !seen.Contains(x)).ToList()) _k.Remove(dead);
                var now = CampaignTime.Now;
                foreach (var key in _flips.Where(p => (now - p.Value.Last).ToDays > FlipKeepDays).Select(p => p.Key).ToList()) _flips.Remove(key);
            }
            return list;
        }

        internal static void OnSessionLaunched()
        {
            try
            {
                if (!On) return;
                var list = Refresh();
                string model = "?", perm = "?";
                bool bkModel = true;
                try
                {
                    var dm = Campaign.Current.Models.DiplomacyModel;
                    model = dm != null ? dm.GetType().FullName : "brak";
                    // podklasa BK z innej przestrzeni nazw tez liczy przez latana metode bazowa
                    bkModel = _mWar != null && dm != null && _mWar.DeclaringType.IsInstanceOfType(dm);
                }
                catch { }
                try { var pm = Campaign.Current.Models.KingdomDecisionPermissionModel; perm = pm != null ? pm.GetType().FullName : "brak"; } catch { }
                Scribe.Line("Pokoj z biedy (E1): start sesji - model dyplomacji gry " + model
                            + (_warOn && !bkModel ? " (NIE BK - wniosek o wojne vanilla liczy bez czlonu bieda, glosy BK z czlonem)" : "")
                            + "; model zgod gry " + perm + " (Diplomacy = czlon wojny nie dziala w nacisku pokoju BK w trwajacej wojnie; BK = dziala)"
                            + "; postfiksy glosu o pokoj (wlasciciel/waga, kolejnosc Harmony: wyzsza waga pierwsza): " + Owners(_mVote) + "; postfiksy oceny wojny BK: " + Owners(_mWar)
                            + "; krolestw " + list.Count + ", w biedzie (z samego skarbca, bez ubytku) " + list.Count(p => p.Value.T > 0f) + ".");
            }
            catch (Exception e) { Stumble("PovertyPeace.OnSessionLaunched", e); }
        }

        private static string Owners(MethodInfo m)
        {
            if (m == null) return "-";
            try
            {
                var info = Harmony.GetPatchInfo(m);
                if (info == null || info.Postfixes == null || info.Postfixes.Count == 0) return "brak";
                return string.Join(", ", info.Postfixes.Select(x => x.owner + "/" + x.priority).ToArray());
            }
            catch { return "?"; }
        }

        private static string Desc(KState s)
        {
            return "bieda skarbca " + s.T.ToString("0.00") + " (skarbiec " + s.W + ", ubytek " + (int)s.Drain + "/d, zapas " + (s.Runway > 9999f ? ">9999" : ((int)s.Runway).ToString())
                   + " dob, wrogow " + s.Enemies + "; glow < " + (int)HeadPoor + ": " + s.Poor + "/" + s.Clans + ", rodow za pokojem z biedy " + s.ForPeace + "/" + s.Clans + ")";
        }

        private static KState Snap(Kingdom k)
        {
            lock (_lock) { KState s; return _k.TryGetValue(k, out s) ? s : null; }
        }

        /// <summary>Zdarzenie gry KingdomDecisionConcluded: decyzja o pokoju albo wojnie miedzy krolestwami (bez wojen fabularnych ROT).</summary>
        internal static void OnDecisionConcluded(KingdomDecision d, DecisionOutcome chosen, bool isPlayerInvolved)
        {
            if (!On || d == null) return;
            try
            {
                var mp = d as MakePeaceKingdomDecision;
                if (mp != null)
                {
                    var k = mp.Kingdom;
                    var e = mp.FactionToMakePeaceWith as Kingdom;
                    if (k == null || e == null || Forced(k, e)) return;
                    var o = chosen as MakePeaceKingdomDecision.MakePeaceDecisionOutcome;
                    bool yes = o != null && o.ShouldPeaceBeDeclared;
                    var s = Snap(k);
                    bool poor = s != null && s.T > 0f;
                    List<string> names = null;
                    int fl = 0;
                    lock (_lock)
                    {
                        Flips f;
                        if (_flips.TryGetValue((k, e), out f)) { fl = f.Clans.Count; names = f.Clans.Take(8).Select(c => N(c)).ToList(); _flips.Remove((k, e)); }
                        _dPeaceDec++; _tPeaceDec++;
                        if (yes) { _dPeaceYes++; _tPeaceYes++; }
                        if (poor) { _dPeacePoor++; _tPeacePoor++; if (yes) { _dPeaceYesPoor++; _tPeaceYesPoor++; } }
                    }
                    Scribe.Line("Pokoj z biedy (E1): dzien " + Day() + " - decyzja o pokoju " + N(k) + " z " + N(e) + ": " + (yes ? "POKOJ" : "BEZ POKOJU")
                                + " (popierajacych wybrana opcje " + (chosen != null && chosen.SupporterList != null ? chosen.SupporterList.Count : 0) + (isPlayerInvolved ? ", gracz bral udzial" : "") + "); "
                                + N(k) + ": " + (s != null ? Desc(s) : "stan nieznany") + "; rodow odwroconych na pokoj przez biede (przyblizone) " + fl
                                + (fl > 0 ? " (" + string.Join(", ", names.ToArray()) + (fl > names.Count ? ", ..." : "") + ")" : "") + ".");
                    return;
                }
                var dw = d as DeclareWarDecision;
                if (dw != null)
                {
                    var k = dw.Kingdom;
                    var t = dw.FactionToDeclareWarOn as Kingdom;
                    if (k == null || t == null || Forced(k, t)) return;
                    var o = chosen as DeclareWarDecision.DeclareWarDecisionOutcome;
                    bool yes = o != null && o.ShouldWarBeDeclared;
                    var s = Snap(k);
                    bool poor = s != null && s.T > 0f;
                    lock (_lock)
                    {
                        _dWarDec++; _tWarDec++;
                        if (yes) { _dWarYes++; _tWarYes++; }
                        if (poor) { _dWarPoor++; _tWarPoor++; if (yes) { _dWarYesPoor++; _tWarYesPoor++; } }
                    }
                    Scribe.Line("Pokoj z biedy (E1): dzien " + Day() + " - decyzja o wojnie " + N(k) + " na " + N(t) + ": " + (yes ? "WOJNA" : "BEZ WOJNY")
                                + " (" + d.GetType().Name + ", popierajacych wybrana opcje " + (chosen != null && chosen.SupporterList != null ? chosen.SupporterList.Count : 0) + (isPlayerInvolved ? ", gracz bral udzial" : "") + "); "
                                + N(k) + ": " + (s != null ? Desc(s) : "stan nieznany")
                                + (poor && _warOn ? " - w ocenach tej wojny czlon bieda " + (-WarWeight * s.T).ToString("0.00") + " x max(|wynik|, 600)" : " - bez czlonu bieda") + ".");
                }
            }
            catch (Exception e) { Stumble("PovertyPeace.OnDecisionConcluded", e); }
        }

        /// <summary>Raz na dobe: stan krolestw (bieda skarbca, udzial biednych glow) i liczniki decyzji.</summary>
        internal static void DailyLine()
        {
            try
            {
                if (On)
                {
                    var list = Refresh();
                    var poor = list.Where(p => p.Value.T > 0f).OrderByDescending(p => p.Value.T).ThenBy(p => p.Value.Runway).ToList();
                    var parts = poor.Take(12).Select(p => N(p.Key) + " " + Desc(p.Value)).ToList();
                    int heads = list.Sum(p => p.Value.Clans), headsPoor = list.Sum(p => p.Value.Poor), forPeace = list.Sum(p => p.Value.ForPeace);
                    string sumW;
                    lock (_lock)
                    {
                        sumW = "dzis: ocen wojny z czlonem bieda " + _dWar + (_dWar > 0 ? " (sredni czlon " + ((int)(_dWarSum / _dWar)) + ")" : "") + ", w tym zmienionych na ujemne " + _dWarSign
                               + ", rodow odwroconych na pokoj (przyblizone) " + _dFlip + ", glosow bez czlonu (gra: pokoj nieodpowiedni) " + _dUnsuit
                               + ", decyzji o pokoju " + _dPeaceDec + " (pokoj " + _dPeaceYes + "; w krolestwach w biedzie " + _dPeacePoor + ", pokoj " + _dPeaceYesPoor + ")"
                               + ", decyzji o wojnie " + _dWarDec + " (wojna " + _dWarYes + "; w krolestwach w biedzie " + _dWarPoor + ", wojna " + _dWarYesPoor + ")"
                               + "; od wczytania: ocen wojny z czlonem " + _tWar + (_tWar > 0 ? " (sredni " + ((int)(_tWarSum / _tWar)) + ")" : "") + ", zmienionych na ujemne " + _tWarSign
                               + ", rodow odwroconych " + _tFlip + ", glosow bez czlonu (pokoj nieodpowiedni) " + _tUnsuit
                               + ", decyzji o pokoju " + _tPeaceDec + " (pokoj " + _tPeaceYes + "; w biedzie " + _tPeacePoor + ", pokoj " + _tPeaceYesPoor + ")"
                               + ", decyzji o wojnie " + _tWarDec + " (wojna " + _tWarYes + "; w biedzie " + _tWarPoor + ", wojna " + _tWarYesPoor + ")"
                               + (_stumblesDay > 0 ? "; potkniecia dzis " + _stumblesDay + " (razem " + _stumbles + ", pierwsze w raporcie; przy potknieciu wynik gry bez zmian)" : "");
                    }
                    Scribe.Line("Pokoj z biedy (E1): dzien " + Day() + " - krolestwa w biedzie (zapas skarbca < " + (int)RunwayFull + " dob) " + poor.Count + " z " + list.Count
                                + (parts.Count > 0 ? ": " + string.Join("; ", parts.ToArray()) + (poor.Count > parts.Count ? "; i " + (poor.Count - parts.Count) + " innych" : "") : "")
                                + " | glowy rodow (bez najemnikow): " + heads + ", < " + (int)HeadPoor + ": " + headsPoor + ", za pokojem z biedy: " + forPeace + " | " + sumW + ".");
                }
            }
            catch (Exception e) { Stumble("PovertyPeace.DailyLine", e); }
            lock (_lock)
            {
                _dWar = _dFlip = _dPeaceDec = _dPeacePoor = _dPeaceYes = _dPeaceYesPoor = 0;
                _dWarDec = _dWarPoor = _dWarYes = _dWarYesPoor = 0;
                _dWarSign = _dUnsuit = 0;
                _dWarSum = 0; _stumblesDay = 0;
            }
        }

        /// <summary>Nowa gra / wczytanie: stan i liczniki od zera (latka zakladana raz przy starcie gry).</summary>
        internal static void ResetSession()
        {
            lock (_lock)
            {
                _k.Clear(); _forced.Clear(); _flips.Clear();
                _dWar = _tWar = _dFlip = _tFlip = _dPeaceDec = _tPeaceDec = _dPeacePoor = _tPeacePoor = _dPeaceYes = _tPeaceYes = _dPeaceYesPoor = _tPeaceYesPoor = 0;
                _dWarDec = _tWarDec = _dWarPoor = _tWarPoor = _dWarYes = _tWarYes = _dWarYesPoor = _tWarYesPoor = 0;
                _dWarSign = _tWarSign = _dUnsuit = _tUnsuit = 0;
                _dWarSum = _tWarSum = 0; _stumblesDay = 0;
            }
        }

        private static int Day()
        {
            try { return (int)(CampaignTime.Now - Campaign.Current.Models.CampaignTimeModel.CampaignStartTime).ToDays; } catch { return 0; }
        }

        private static string N(Kingdom k)
        {
            try { return k == null ? "?" : (k.Name != null ? k.Name.ToString() : k.StringId); } catch { return "?"; }
        }

        private static string N(Clan c)
        {
            try { return c == null ? "?" : (c.Name != null ? c.Name.ToString() : c.StringId) + " " + c.Gold; } catch { return "?"; }
        }
    }

    /// <summary>Start sesji (stan skarbcow), linia dobowa i decyzje krolestw "Pokoj z biedy (E1)" (bez zapisu w grze).</summary>
    internal sealed class PovertyPeaceBehavior : CampaignBehaviorBase
    {
        public PovertyPeaceBehavior() { PovertyPeace.ResetSession(); }

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, s => PovertyPeace.OnSessionLaunched());
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, PovertyPeace.DailyLine);
            CampaignEvents.KingdomDecisionConcluded.AddNonSerializedListener(this, PovertyPeace.OnDecisionConcluded);
        }

        public override void SyncData(IDataStore dataStore) { }
    }
}
