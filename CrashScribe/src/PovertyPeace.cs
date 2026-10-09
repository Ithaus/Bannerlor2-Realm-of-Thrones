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
    ///
    /// E1b (projekt etapu 2, rozdz. "E1b: pokoj z biedy, ktory dziala"; PLAN 2.15): po wgraniu 8 w testach 0 glosowan o pokoj -
    /// wniosek o pokoj nie powstaje. Trzy czesci:
    ///  1. DIAGNOZA (tylko log): postfiks na gre KingdomDecisionProposalBehavior.ConsiderPeace (tam prefiks Diplomacy z
    ///     MakePeaceConditions) - ile wywolan, ile przeszlo, a przy odrzuceniu pierwszy niespelniony warunek w kolejnosci gry:
    ///     zgody Diplomacy (MakePeaceConditions, w tym "wojna za mloda" = MinimumWarDurationInDays), IsPeaceSuitable gry, prog
    ///     wyniku pokoju BK (GetScoreOfDeclaringPeace < GetDecisionMakingThreshold), danina < 0, glos wnioskodawcy <= 0.
    ///     Odtworzenie bez skutkow ubocznych (BK trzyma wynik wojny w pamieci doby; nasz glos bez licznikow - _quiet).
    ///     Zrodlo kazdego wniosku o pokoj wrzuconego do glosowania (KingdomDecisionAdded): gra (ConsiderPeace), BK
    ///     ForceProposePeaceFromLosingSide (prefiks/finalizer ustawia znacznik), z biedy (nasz), inne.
    ///  2. WNIOSEK Z BIEDY: raz na dobe, krolestwo z bieda skarbca T >= 0.5, nie czesciej niz raz na 7 dob, sklada wniosek o pokoj
    ///     z tym wrogiem (krolestwem), z ktorym ma najgorszy wynik wojny (gra GetWarProgressScore: swoj - wroga; remis - starsza
    ///     wojna), sposrod wojen, w ktorych gra dopuszcza wniosek: niefabularna (ROT IsWarForced = nie), zgody gry
    ///     (KingdomDecisionPermissionModel - wojna stala, wezwanie sojusznika, a przy Diplomacy jej MakePeaceConditions) i
    ///     zgody Diplomacy (jak jej prefiks na ConsiderPeace), IsPeaceSuitable gry (inaczej gra daje glosom 0/200 i nasz czlon
    ///     glosu nie dziala). Wnioskodawca: rod AI krolestwa "za pokojem z biedy" (T + P > 1) z wplywem ponad koszt wniosku
    ///     (jak gra), najbiedniejszy; jak w ConsiderPeace - glos wnioskodawcy > 0 (pytamy do 3 rodow). Danina dzienna 0 (jak wniosek
    ///     BK): trybut gry to strumien rody -> nicosc i nic -> odbiorca, glos gry patrzy tylko na danine < 0.
    ///     Wniosek przez Kingdom.AddDecision (wnioskodawca placi wplyw jak w grze); glosuja rody jak dzis (czlon E1 w glosie
    ///     zostaje). Pomijamy TYLKO prog BK (wynik pokoju >= 0) - to on zatrzymywal wnioski. Jeden czlon na jedno zjawisko:
    ///     wniosek z biedy, glos z biedy (wynik pokoju BK, IsPeaceSuitable i nacisk BK bez zmian).
    ///     Dlaczego krok dobowy, a nie postfiks (projekt zostawil miejsce otwarte): ConsiderPeace dostaje losowego wroga z losowego
    ///     rzutu rodu - "raz na 7 dob" i "najgorszy wynik" bylyby niewykonalne; postfiks na wynik pokoju BK wszedlby takze do
    ///     IsPeaceSuitable i do glosu (ta sama funkcja). BK robi to samo w ForceProposePeaceFromLosingSide (AddDecision raz na dobe).
    ///  3. MIARA T Z NIEDOPLATY KORONY (po paczce 165, wylacznik PovertyPeaceArrearsMeasure, domyslnie false): n = 1 - zwrot dany /
    ///     nalezny z 28 dob (Armoury KingdomTreasury._refund, wiersz dobowy Due/Given), T = (n - max(25%; n swiata)) / 15%,
    ///     przyciete do 0-1; krolestwo bez naleznego zwrotu w oknie - T = 0. Brak danych Armoury - miara skarbca (log mowi).
    /// Wylaczniki E1b: PovertyPeaceProposals (wniosek), PovertyPeaceArrearsMeasure (miara); diagnoza z PovertyPeace.
    /// Bez zapisu w grze: przerwa 7 dob liczona od wczytania (po wczytaniu krolestwo w biedzie moze zlozyc wniosek od razu).
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
            public float N = -1f;   // E1b: niedoplata korony z 28 dob (-1 = brak naleznego zwrotu w oknie albo miara skarbca)
            public int W, Enemies, Clans, Poor, ForPeace;
        }

        // ------------------------------------------------------------------ E1b
        private const float ProposeT = 0.5f;          // wniosek z biedy od biedy skarbca 0.5 (zapas <= ok. 60 dob; projekt E1b pkt 2)
        private const int ProposeEveryDays = 7;       // raz na 7 dob na krolestwo
        private const int ProposerTries = 3;          // ilu najbiedniejszych rodow pytamy o glos wnioskodawcy (gra: > 0)
        private const int MineKeepDays = 10;          // nasz wniosek w krolestwie gracza czeka na glos gracza - potem zapominamy
        private const int ArrearsDays = 28;           // okno niedoplaty korony
        private const float ArrearsFloor = 0.25f;     // prog etapu 2: niedoplata 25%
        private const float ArrearsSpan = 0.15f;      // od progu (albo sredniej swiata) do T = 1: +15 pp
        private const string WhyUnsuit = "gra: pokoj nieodpowiedni (IsPeaceSuitable)";
        private const string WhyBk = "prog BK (wynik pokoju < prog decyzji)";

        private static bool _diagOn, _propOn, _bkOn;
        private static MethodInfo _mConsider, _mBkForce;
        private static object _dipConds;        // Diplomacy MakePeaceConditions.Instance (typ wewnetrzny - refleksja)
        private static MethodInfo _mDipExc;     // AbstractConditionEvaluator<MakePeaceConditions>.CanApplyExceptions(Kingdom, Kingdom, bool, bool)
        private static bool _inBk;
        private static MakePeaceKingdomDecision _adding;   // nasz wniosek w trakcie Kingdom.AddDecision (zrodlo "z biedy")
        [ThreadStatic] private static bool _quiet;   // odtworzenie warunkow / pytanie wnioskodawcy: glos bez licznikow E1
        private static MakePeaceKingdomDecision _lastGame;   // ostatni wniosek, ktory przeszedl ConsiderPeace (zrodlo "gra")

        private sealed class Ctr { public int D, T; public void Inc() { D++; T++; } }
        private sealed class Why
        {
            public readonly Dictionary<string, int> D = new Dictionary<string, int>(), T = new Dictionary<string, int>();
            public void Inc(string k) { int v; D.TryGetValue(k, out v); D[k] = v + 1; T.TryGetValue(k, out v); T[k] = v + 1; }
            public string Fmt(bool day)
            {
                var src = day ? D : T;
                return src.Count == 0 ? "-" : string.Join(", ", src.OrderByDescending(p => p.Value).Select(p => p.Key + " " + p.Value).ToArray());
            }
        }
        private static readonly Ctr _cpCall = new Ctr(), _cpPoor = new Ctr(), _cpOk = new Ctr();
        private static readonly Why _cpWhy = new Why();
        private static double _cpScoreSum; private static int _cpScoreN;   // dzis: wynik pokoju BK w odrzuceniach progiem
        private static readonly Ctr _addGame = new Ctr(), _addBk = new Ctr(), _addMine = new Ctr(), _addOther = new Ctr();
        private static readonly Ctr _ppElig = new Ctr(), _ppWait = new Ctr(), _ppMade = new Ctr(), _ppVote = new Ctr(), _ppYes = new Ctr(), _ppCancel = new Ctr(), _ppOffer = new Ctr();
        private static readonly Why _ppWhy = new Why();
        private static readonly Ctr[] _ctrs = { _cpCall, _cpPoor, _cpOk, _addGame, _addBk, _addMine, _addOther, _ppElig, _ppWait, _ppMade, _ppVote, _ppYes, _ppCancel, _ppOffer };
        private static readonly Dictionary<Kingdom, int> _ppByK = new Dictionary<Kingdom, int>();     // wnioski z biedy od wczytania
        private static readonly Dictionary<Kingdom, int> _ppLast = new Dictionary<Kingdom, int>();    // doba (bezwzgledna) ostatniego wniosku z biedy
        private static readonly Dictionary<KingdomDecision, int> _mine = new Dictionary<KingdomDecision, int>();   // nasze wnioski -> doba

        // miara niedoplaty korony (E1b pkt 3)
        private sealed class ArrRow { public int Day; public long Due, Given; }
        private static readonly Dictionary<Kingdom, List<ArrRow>> _arr = new Dictionary<Kingdom, List<ArrRow>>();
        private static float _nWorld = -1f;
        private static int _arrLastDay = -1;
        private static long _arrLastSum = -1;
        private static bool _arrTried;
        private static FieldInfo _fRefund, _fDue, _fGiven;
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
            InstallE1b(harmony);
        }

        /// <summary>E1b: diagnoza wnioskow o pokoj (ConsiderPeace gry, ForceProposePeaceFromLosingSide BK) i odczyt zgod Diplomacy.</summary>
        private static void InstallE1b(Harmony harmony)
        {
            try
            {
                var miss = new List<string>();
                var tProp = typeof(TaleWorlds.CampaignSystem.CampaignBehaviors.KingdomDecisionProposalBehavior);
                var mCons = AccessTools.Method(tProp, "ConsiderPeace", new[] { typeof(Clan), typeof(Clan), typeof(IFaction), typeof(MakePeaceKingdomDecision).MakeByRefType() });
                if (mCons != null && mCons.ReturnType == typeof(bool))
                {
                    // kazda latka osobno: blad jednej nie gasi reszty E1b (wniosek z biedy latek nie potrzebuje)
                    try
                    {
                        harmony.Patch(mCons, postfix: new HarmonyMethod(typeof(PovertyPeace), nameof(ConsiderPeacePostfix)));
                        _diagOn = true; _mConsider = mCons;
                    }
                    catch (Exception e) { Stumble("PovertyPeace.InstallE1b.ConsiderPeace", e); miss.Add("latka ConsiderPeace - blad " + e.GetType().Name); }
                }
                else miss.Add("gra KingdomDecisionProposalBehavior.ConsiderPeace (diagnoza wnioskow gry)");

                var tBkB = AccessTools.TypeByName("BannerKings.Behaviours.Diplomacy.BKDiplomacyBehavior");
                var mForce = tBkB != null ? AccessTools.Method(tBkB, "ForceProposePeaceFromLosingSide") : null;
                if (mForce != null)
                {
                    try
                    {
                        harmony.Patch(mForce, prefix: new HarmonyMethod(typeof(PovertyPeace), nameof(BkForcePrefix)),
                                      finalizer: new HarmonyMethod(typeof(PovertyPeace), nameof(BkForceFinalizer)));
                        _bkOn = true; _mBkForce = mForce;
                    }
                    catch (Exception e) { Stumble("PovertyPeace.InstallE1b.BkForce", e); miss.Add("latka ForceProposePeaceFromLosingSide - blad " + e.GetType().Name); }
                }
                else miss.Add("BK BKDiplomacyBehavior.ForceProposePeaceFromLosingSide (wnioski BK licza sie jako \"inne\")");

                // Diplomacy: MakePeaceConditions - te same warunki, co jej prefiks na ConsiderPeace (typy wewnetrzne)
                try
                {
                    var tMpc = AccessTools.TypeByName("Diplomacy.DiplomaticAction.WarPeace.MakePeaceConditions");
                    var tBase = tMpc != null ? tMpc.BaseType : null;
                    var pInst = tBase != null ? tBase.GetProperty("Instance", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static) : null;
                    var mExc = tBase != null ? tBase.GetMethod("CanApplyExceptions", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null,
                                                               new[] { typeof(Kingdom), typeof(Kingdom), typeof(bool), typeof(bool) }, null) : null;
                    object inst = pInst != null ? pInst.GetValue(null, null) : null;
                    if (inst != null && mExc != null && typeof(System.Collections.IList).IsAssignableFrom(mExc.ReturnType)) { _dipConds = inst; _mDipExc = mExc; }
                }
                catch { _dipConds = null; _mDipExc = null; }
                if (_mDipExc == null) miss.Add("Diplomacy MakePeaceConditions.CanApplyExceptions (zgody Diplomacy tylko przez model zgod gry)");

                _propOn = Config.PovertyPeaceProposals;
                Scribe.Line("Pokoj z biedy (E1b): wniosek z biedy " + (_propOn ? "WLACZONY (bieda skarbca >= " + ProposeT.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)
                                + ", raz na " + ProposeEveryDays + " dob, wrog z najgorszym wynikiem wojny sposrod wojen niefabularnych dopuszczonych przez gre i Diplomacy; pomija tylko prog wyniku pokoju BK)" : "WYLACZONY (PovertyPeaceProposals = false)")
                            + "; miara biedy " + (Config.PovertyPeaceArrearsMeasure ? "NIEDOPLATA KORONY z " + ArrearsDays + " dob (PovertyPeaceArrearsMeasure)" : "zapas skarbca (E1)")
                            + "; diagnoza: ConsiderPeace gry " + (_diagOn ? "wpieta" : "NIE") + ", ForceProposePeaceFromLosingSide BK " + (_bkOn ? "wpieta" : "NIE")
                            + ", zgody Diplomacy " + (_mDipExc != null ? "odczyt MakePeaceConditions" : "NIE")
                            + (miss.Count > 0 ? " | brak: " + string.Join("; ", miss.ToArray()) : "") + ".");
            }
            catch (Exception e) { Stumble("PovertyPeace.InstallE1b", e); }
        }

        private static bool On { get { return (_voteOn || _warOn || _diagOn || _propOn) && Config.PovertyPeace; } }

        /// <summary>Miara niedoplaty korony wlaczona i dane Armoury dostepne.</summary>
        private static bool ArrearsOn { get { return Config.PovertyPeaceArrearsMeasure && ArrearsBind(); } }

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
                    if (!_quiet) lock (_lock) { _dUnsuit++; _tUnsuit++; }
                    return;
                }
                float x = VoteUnit * Math.Min(w, 2f);
                float orig = __result;
                if (o.ShouldPeaceBeDeclared)
                {
                    __result = Yes(orig, x);
                    // glos "przeciw" tego rodu: gra 0/200 i nacisk BK +-240 x p zawsze daja za + przeciw = 200
                    float noOrig = VoteFull - orig;
                    if (!_quiet && !(orig > noOrig) && __result > No(noOrig, x)) Flip(k, e, clan);   // E1b: odtworzenie i pytanie wnioskodawcy bez licznika
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

        /// <summary>Bieda skarbca krolestwa i liczba wrogich krolestw (stan z linii dobowej; przed pierwsza - z samego skarbca,
        /// a przy mierze niedoplaty korony (E1b) - z okna niedoplaty).</summary>
        private static bool Poverty(Kingdom k, out float T, out int enemies)
        {
            lock (_lock)
            {
                KState s;
                if (_k.TryGetValue(k, out s)) { T = s.T; enemies = s.Enemies; return true; }
            }
            if (ArrearsOn) { float n; lock (_lock) { T = ArrearsT(k, out n); } }
            else
            {
                int w = Math.Max(0, k.KingdomBudgetWallet);
                T = Clamp01((RunwayFull - w / DrainFloor) / (RunwayFull - RunwayEmpty));
            }
            enemies = FactionHelper.GetEnemyKingdoms(k).Count();
            return true;
        }

        // ------------------------------------------------------------------ E1b: miara niedoplaty korony

        /// <summary>Pola Armoury KingdomTreasury._refund (Dictionary&lt;Kingdom, RefundRow&gt;, wiersz dobowy zwrotu zoldu: Due, Given).</summary>
        private static bool ArrearsBind()
        {
            if (_arrTried) return _fRefund != null;
            _arrTried = true;
            try
            {
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (asm.GetName().Name != "Armoury") continue;
                    var t = asm.GetType("Armoury.KingdomTreasury");
                    var rt = asm.GetType("Armoury.KingdomTreasury+RefundRow");
                    _fRefund = t != null ? t.GetField("_refund", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static) : null;
                    _fDue = rt != null ? rt.GetField("Due") : null;
                    _fGiven = rt != null ? rt.GetField("Given") : null;
                    break;
                }
            }
            catch { }
            if (_fRefund == null || _fDue == null || _fGiven == null || !typeof(System.Collections.IDictionary).IsAssignableFrom(_fRefund.FieldType))
            {
                _fRefund = null;
                Scribe.Line("Pokoj z biedy (E1b): miara niedoplaty korony NIEDOSTEPNA (brak Armoury KingdomTreasury._refund z polami Due/Given) - zostaje miara zapasu skarbca (E1).");
            }
            return _fRefund != null;
        }

        /// <summary>Raz na dobe: wiersz zwrotu zoldu kazdego krolestwa z Armoury do okna 28 dob; niedoplata swiata. Pod _lock.</summary>
        private static void ReadArrears(int absDay)
        {
            if (absDay == _arrLastDay) return;
            _arrLastDay = absDay;
            var rows = new List<KeyValuePair<Kingdom, ArrRow>>();
            long sum = 0;
            var d = _fRefund.GetValue(null) as System.Collections.IDictionary;
            if (d != null)
                foreach (System.Collections.DictionaryEntry de in d)
                {
                    var k = de.Key as Kingdom;
                    if (k == null || de.Value == null) continue;
                    long due = Convert.ToInt64(_fDue.GetValue(de.Value)), giv = Convert.ToInt64(_fGiven.GetValue(de.Value));
                    if (due <= 0) continue;
                    rows.Add(new KeyValuePair<Kingdom, ArrRow>(k, new ArrRow { Day = absDay, Due = due, Given = Math.Max(0, Math.Min(giv, due)) }));
                    sum += due * 31 + giv;
                }
            // ten sam wiersz drugi raz (rozliczenie Armoury nie przeszlo od wczoraj) - nie liczymy podwojnie
            bool dup = sum != 0 && sum == _arrLastSum;
            _arrLastSum = sum;
            if (!dup)
                foreach (var r in rows)
                {
                    List<ArrRow> l;
                    if (!_arr.TryGetValue(r.Key, out l)) { l = new List<ArrRow>(); _arr[r.Key] = l; }
                    l.Add(r.Value);
                }
            long wDue = 0, wGiv = 0;
            foreach (var kv in _arr)
            {
                kv.Value.RemoveAll(x => x.Day <= absDay - ArrearsDays);
                foreach (var x in kv.Value) { wDue += x.Due; wGiv += x.Given; }
            }
            _nWorld = wDue > 0 ? 1f - (float)wGiv / wDue : -1f;
        }

        /// <summary>T = (n - max(25%; n swiata)) / 15%, przyciete do 0-1; bez naleznego zwrotu w oknie - 0 (n = -1). Pod _lock.</summary>
        private static float ArrearsT(Kingdom k, out float n)
        {
            n = -1f;
            List<ArrRow> l;
            if (!_arr.TryGetValue(k, out l) || l.Count == 0) return 0f;
            long due = 0, giv = 0;
            foreach (var x in l) { due += x.Due; giv += x.Given; }
            if (due <= 0) return 0f;
            n = 1f - (float)giv / due;
            return Clamp01((n - Math.Max(ArrearsFloor, _nWorld)) / ArrearsSpan);
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

        private static void Update(KState s, Kingdom k, int absDay, bool arrears)
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
            s.N = -1f;
            if (arrears) s.T = ArrearsT(k, out s.N);   // E1b pkt 3: po 165 miara z niedoplaty korony (zapas skarbca zostaje w logu)
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
            bool arrears = ArrearsOn;
            lock (_lock)
            {
                _forced.Clear();
                if (arrears)
                {
                    try { ReadArrears(absDay); }
                    catch (Exception e) { Stumble("PovertyPeace.ReadArrears", e); }
                }
                var seen = new HashSet<Kingdom>();
                foreach (var k in Kingdom.All)
                {
                    if (k == null || k.IsEliminated) continue;
                    try
                    {
                        KState s;
                        if (!_k.TryGetValue(k, out s)) { s = new KState(); _k[k] = s; }
                        Update(s, k, absDay, arrears);
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
                            + "; krolestw " + list.Count + ", w biedzie (" + (ArrearsOn ? "z niedoplaty korony od wczytania" : "z samego skarbca, bez ubytku") + ") " + list.Count(p => p.Value.T > 0f) + ".");
                if (_diagOn || _bkOn)
                    Scribe.Line("Pokoj z biedy (E1b): start sesji - latki wniosku gry ConsiderPeace: " + AllPatches(_mConsider) + "; ForceProposePeaceFromLosingSide BK: " + AllPatches(_mBkForce)
                                + "; wniosek z biedy " + (_propOn && Config.PovertyPeaceProposals ? "wlaczony" : "wylaczony")
                                + "; krolestw z bieda skarbca >= " + ProposeT.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + ": " + list.Count(p => p.Value.T >= ProposeT) + ".");
            }
            catch (Exception e) { Stumble("PovertyPeace.OnSessionLaunched", e); }
        }

        /// <summary>Wszystkie latki metody (prefiksy i postfiksy z wlascicielem/waga) - kto jeszcze wchodzi we wniosek o pokoj.</summary>
        private static string AllPatches(MethodInfo m)
        {
            if (m == null) return "-";
            try
            {
                var info = Harmony.GetPatchInfo(m);
                if (info == null) return "brak";
                Func<IEnumerable<Patch>, string> f = ps => ps == null || !ps.Any() ? "-" : string.Join(", ", ps.Select(x => x.owner + "/" + x.priority).ToArray());
                return "prefiksy " + f(info.Prefixes) + ", postfiksy " + f(info.Postfixes) + ", finalizery " + f(info.Finalizers);
            }
            catch { return "?"; }
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
                   + " dob" + (Config.PovertyPeaceArrearsMeasure && _fRefund != null ? ", niedoplata korony " + Pct(s.N) + " (swiat " + Pct(_nWorld) + ")" : "")
                   + ", wrogow " + s.Enemies + "; glow < " + (int)HeadPoor + ": " + s.Poor + "/" + s.Clans + ", rodow za pokojem z biedy " + s.ForPeace + "/" + s.Clans + ")";
        }

        private static string Pct(float n) { return n < 0f ? "-" : ((int)Math.Round(n * 100f)) + "%"; }

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
                    var o = chosen as MakePeaceKingdomDecision.MakePeaceDecisionOutcome;
                    bool yes = o != null && o.ShouldPeaceBeDeclared;
                    bool mine;
                    lock (_lock)
                    {
                        mine = _mine.Remove(mp);   // E1b: wniosek z biedy (zawsze w wojnie niefabularnej)
                        if (mine) { _ppVote.Inc(); if (yes) _ppYes.Inc(); }
                    }
                    var k = mp.Kingdom;
                    var e = mp.FactionToMakePeaceWith as Kingdom;
                    if (k == null || e == null || Forced(k, e)) return;
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
                                + (mine ? " [wniosek z biedy E1b]" : "")
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
                    Propose(list);   // E1b: wnioski z biedy (glosowania krolestw AI rozstrzygaja sie od razu - linie "decyzja o pokoju" przed linia doby)
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
                    Scribe.Line("Pokoj z biedy (E1): dzien " + Day() + " - krolestwa w biedzie ("
                                + (ArrearsOn ? "niedoplata korony z " + ArrearsDays + " dob ponad max(" + (int)(ArrearsFloor * 100f) + "%, swiat " + Pct(_nWorld) + ")" : "zapas skarbca < " + (int)RunwayFull + " dob")
                                + ") " + poor.Count + " z " + list.Count
                                + (parts.Count > 0 ? ": " + string.Join("; ", parts.ToArray()) + (poor.Count > parts.Count ? "; i " + (poor.Count - parts.Count) + " innych" : "") : "")
                                + " | glowy rodow (bez najemnikow): " + heads + ", < " + (int)HeadPoor + ": " + headsPoor + ", za pokojem z biedy: " + forPeace + " | " + sumW + ".");
                    if (_diagOn || _bkOn || _propOn) Scribe.Line(E1bLine());
                }
            }
            catch (Exception e) { Stumble("PovertyPeace.DailyLine", e); }
            lock (_lock)
            {
                _dWar = _dFlip = _dPeaceDec = _dPeacePoor = _dPeaceYes = _dPeaceYesPoor = 0;
                _dWarDec = _dWarPoor = _dWarYes = _dWarYesPoor = 0;
                _dWarSign = _dUnsuit = 0;
                _dWarSum = 0; _stumblesDay = 0;
                foreach (var c in _ctrs) c.D = 0;
                _cpWhy.D.Clear(); _ppWhy.D.Clear();
                _cpScoreSum = 0; _cpScoreN = 0;
                _lastGame = null;
                // nasz wniosek w krolestwie gracza, ktory zniknal bez rozstrzygniecia i anulowania - zapominamy
                int today = (int)CampaignTime.Now.ToDays;
                foreach (var old in _mine.Where(p => today - p.Value > MineKeepDays).Select(p => p.Key).ToList()) _mine.Remove(old);
            }
        }

        /// <summary>E1b: linia doby - wnioski o pokoj gry (ConsiderPeace) z powodami odrzucen, zrodla wnioskow w glosowaniach,
        /// wnioski z biedy i ich glosowania (dzis i od wczytania).</summary>
        private static string E1bLine()
        {
            lock (_lock)
            {
                string byK = _ppByK.Count == 0 ? "-" : string.Join(", ", _ppByK.OrderByDescending(p => p.Value).Select(p => N(p.Key) + " " + p.Value).ToArray());
                return "Pokoj z biedy (E1b): dzien " + Day() + " - WNIOSKI O POKOJ dzis: gra ConsiderPeace wolane " + _cpCall.D + " (z krolestw w biedzie " + _cpPoor.D + "), przeszlo " + _cpOk.D
                       + ", odrzucone " + (_cpCall.D - _cpOk.D) + " - pierwszy niespelniony warunek: " + _cpWhy.Fmt(true)
                       + (_cpScoreN > 0 ? " (sredni wynik pokoju BK przy odrzuceniu progiem " + (int)(_cpScoreSum / _cpScoreN) + ")" : "")
                       + "; do glosowania: gra " + _addGame.D + ", BK ForceProposePeaceFromLosingSide " + _addBk.D + ", z biedy " + _addMine.D + ", inne " + _addOther.D
                       + "; z biedy: krolestw z bieda skarbca >= " + ProposeT.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " w wojnie z krolestwem " + _ppElig.D
                       + " (na przerwie " + ProposeEveryDays + " dob " + _ppWait.D + "), wnioskow z biedy " + _ppMade.D + ", bez wniosku: " + _ppWhy.Fmt(true)
                       + "; glosowan nad wnioskami z biedy " + _ppVote.D + " (POKOJ " + _ppYes.D + ", anulowane " + _ppCancel.D + ")" + (_ppOffer.D > 0 ? ", ofert pokoju dla gracza " + _ppOffer.D : "")
                       + " | od wczytania: ConsiderPeace wolane " + _cpCall.T + " (z krolestw w biedzie " + _cpPoor.T + "), przeszlo " + _cpOk.T + ", odrzucone: " + _cpWhy.Fmt(false)
                       + "; do glosowania: gra " + _addGame.T + ", BK " + _addBk.T + ", z biedy " + _addMine.T + ", inne " + _addOther.T
                       + "; WNIOSKOW Z BIEDY " + _ppMade.T + " (na krolestwo: " + byK + "), bez wniosku: " + _ppWhy.Fmt(false)
                       + "; GLOSOWAN NAD WNIOSKAMI Z BIEDY " + _ppVote.T + ", POKOJ " + _ppYes.T + ", anulowane " + _ppCancel.T + ", ofert pokoju dla gracza " + _ppOffer.T + ".";
            }
        }

        // ------------------------------------------------------------------ E1b: diagnoza wnioskow o pokoj

        /// <summary>Postfiks gry KingdomDecisionProposalBehavior.ConsiderPeace(clan, otherClan, otherFaction, out decision) - tylko log.
        /// Postfiks biegnie takze, gdy prefiks Diplomacy pominal oryginal.</summary>
        public static void ConsiderPeacePostfix(Clan clan, Clan otherClan, IFaction otherFaction, ref MakePeaceKingdomDecision decision, bool __result)
        {
            if (!_diagOn || !Config.PovertyPeace) return;
            try
            {
                var k = clan != null ? clan.Kingdom : null;
                float T; int en;
                bool poor = k != null && !k.IsEliminated && Poverty(k, out T, out en) && T > 0f;
                if (__result)
                {
                    lock (_lock) { _cpCall.Inc(); if (poor) _cpPoor.Inc(); _cpOk.Inc(); _lastGame = decision; }
                    return;
                }
                float score;
                string why = ConsiderWhy(clan, otherClan, otherFaction, out score);
                lock (_lock)
                {
                    _cpCall.Inc(); if (poor) _cpPoor.Inc();
                    _cpWhy.Inc(why);
                    if (why == WhyBk) { _cpScoreSum += score; _cpScoreN++; }
                }
            }
            catch (Exception ex) { Stumble("PovertyPeace.ConsiderPeacePostfix", ex); }
        }

        /// <summary>Pierwszy niespelniony warunek wniosku o pokoj w kolejnosci gry (Diplomacy prefiks -> ConsiderPeace gry 1.4.8).
        /// Bez skutkow: wynik wojny BK z pamieci doby, glos bez licznikow E1 (_quiet).</summary>
        private static string ConsiderWhy(Clan clan, Clan otherClan, IFaction otherFaction, out float score)
        {
            score = 0f;
            if (clan == null || otherFaction == null) return "brak rodu albo wroga";
            var k = clan.Kingdom;
            var e = otherFaction as Kingdom;
            if (k != null && e != null)
            {
                string dip = DipBlock(k, e);
                if (dip != null) return "Diplomacy: " + dip;
            }
            var dm = Campaign.Current.Models.DiplomacyModel;
            _quiet = true;
            try
            {
                if (!dm.IsPeaceSuitable(clan.MapFaction, otherFaction)) return WhyUnsuit;
                score = dm.GetScoreOfDeclaringPeace(clan.MapFaction, otherFaction);
                if (score < dm.GetDecisionMakingThreshold(clan.Kingdom)) return WhyBk;
                if (otherClan == null) return "wrog bez rodu panujacego";
                int dur;
                int trib = dm.GetDailyTributeToPay(clan, otherClan, out dur);
                if (trib < 0) return "danina < 0";
                var dec = new MakePeaceKingdomDecision(clan, otherFaction, trib, dur);
                if (dec.DetermineSupport(clan, YesOf(dec)) <= 0f) return "glos wnioskodawcy <= 0";
                return "nieustalony (inna latka?)";
            }
            finally { _quiet = false; }
        }

        private static DecisionOutcome YesOf(MakePeaceKingdomDecision d)
        {
            return d.DetermineInitialCandidates().First(x => x is MakePeaceKingdomDecision.MakePeaceDecisionOutcome o && o.ShouldPeaceBeDeclared);
        }

        /// <summary>Zgody Diplomacy - te same, co jej prefiks na ConsiderPeace (MakePeaceConditions.CanApply(k, e, false, bypassCosts: true));
        /// null = dopuszczone (albo brak Diplomacy), inaczej krotki opis pierwszego niespelnionego warunku.</summary>
        private static string DipBlock(Kingdom k, Kingdom e)
        {
            if (_dipConds == null || _mDipExc == null) return null;
            var list = _mDipExc.Invoke(_dipConds, new object[] { k, e, false, true }) as System.Collections.IList;
            if (list == null || list.Count == 0) return null;
            return Short(list[0] as TextObject);
        }

        /// <summary>Czy gra i Diplomacy dopuszczaja wniosek o pokoj (model zgod gry: wojna stala, wezwanie sojusznika, przy Diplomacy
        /// jej warunki; potem zgody Diplomacy jak w jej prefiksie). null = dopuszczone.</summary>
        private static string PeaceBlock(Kingdom k, Kingdom e)
        {
            var pm = Campaign.Current.Models.KingdomDecisionPermissionModel;
            TextObject r;
            if (pm != null && !pm.IsPeaceDecisionAllowedBetweenKingdoms(k, e, out r)) return "zgody gry: " + Short(r);
            string dip = DipBlock(k, e);
            return dip != null ? "Diplomacy: " + dip : null;
        }

        /// <summary>Opis warunku do licznika: do pierwszej kropki/wykrzyknika (liczby dni z Diplomacy sa dalej), najwyzej 60 znakow.</summary>
        private static string Short(TextObject t)
        {
            string s;
            try { s = t != null ? t.ToString() : "?"; } catch { s = "?"; }
            if (string.IsNullOrEmpty(s)) return "?";
            int i = s.IndexOfAny(new[] { '.', '!' });
            if (i > 0) s = s.Substring(0, i);
            return s.Length > 60 ? s.Substring(0, 60) : s;
        }

        /// <summary>Prefiks/finalizer BKDiplomacyBehavior.ForceProposePeaceFromLosingSide: wnioski dodane w trakcie = BK
        /// (finalizer oddaje wyjatek BK bez zmian - jak Fabula.WarFinalizer).</summary>
        public static void BkForcePrefix() { _inBk = true; }
        public static Exception BkForceFinalizer(Exception __exception) { _inBk = false; return __exception; }

        /// <summary>Zdarzenie gry KingdomDecisionAdded: zrodlo wniosku o pokoj w glosowaniu.</summary>
        internal static void OnDecisionAdded(KingdomDecision d, bool isPlayerInvolved)
        {
            if (!On || !(d is MakePeaceKingdomDecision)) return;
            try
            {
                lock (_lock)
                {
                    if (_adding != null && ReferenceEquals(d, _adding)) _addMine.Inc();
                    else if (_inBk) _addBk.Inc();
                    else if (_lastGame != null && ReferenceEquals(d, _lastGame)) { _addGame.Inc(); _lastGame = null; }
                    else _addOther.Inc();
                }
            }
            catch (Exception e) { Stumble("PovertyPeace.OnDecisionAdded", e); }
        }

        /// <summary>Zdarzenie gry KingdomDecisionCancelled: nasz wniosek anulowany (np. pokoj zawarty inna droga przed glosem gracza).</summary>
        internal static void OnDecisionCancelled(KingdomDecision d, bool isPlayerInvolved)
        {
            if (!On || d == null) return;
            lock (_lock) { if (_mine.Remove(d)) _ppCancel.Inc(); }
        }

        // ------------------------------------------------------------------ E1b: wniosek z biedy

        /// <summary>Raz na dobe (po odswiezeniu biedy): krolestwa z T >= 0.5 skladaja wniosek o pokoj z biedy (patrz opis klasy).</summary>
        private static void Propose(List<KeyValuePair<Kingdom, KState>> list)
        {
            if (!_propOn || !Config.PovertyPeaceProposals || Campaign.Current == null) return;
            int absDay = (int)CampaignTime.Now.ToDays;
            var dm = Campaign.Current.Models.DiplomacyModel;
            foreach (var p in list)
            {
                var k = p.Key; var s = p.Value;
                try
                {
                    if (k == null || k.IsEliminated || s.T < ProposeT) continue;
                    var enemies = FactionHelper.GetEnemyKingdoms(k).Where(x => x != null && x != k && !x.IsEliminated).ToList();
                    if (enemies.Count == 0) continue;
                    int last; bool wait;
                    lock (_lock) { _ppElig.Inc(); wait = _ppLast.TryGetValue(k, out last) && absDay - last < ProposeEveryDays; if (wait) _ppWait.Inc(); }
                    if (wait) continue;
                    // jak gra (GetRandomPeaceDecision): jeden wniosek o pokoj naraz (lista nierozstrzygnietych ma tylko krolestwo gracza)
                    if (k.UnresolvedDecisions.Any(x => x is MakePeaceKingdomDecision)) { NoProposal("czeka juz wniosek o pokoj"); continue; }

                    int fab = 0, blocked = 0, unsuit = 0, ok = 0; string firstBlock = null;
                    Kingdom best = null; float bestRes = 0f; double bestStart = 0;
                    foreach (var e in enemies)
                    {
                        if (Forced(k, e)) { fab++; continue; }
                        string b = PeaceBlock(k, e);
                        if (b != null) { blocked++; if (firstBlock == null) firstBlock = b; continue; }
                        if (!dm.IsPeaceSuitable(k, e)) { unsuit++; continue; }
                        ok++;
                        float res = dm.GetWarProgressScore(k, e).ResultNumber - dm.GetWarProgressScore(e, k).ResultNumber;
                        double start = k.GetStanceWith(e).WarStartDate.ToDays;
                        if (best == null || res < bestRes || (res == bestRes && start < bestStart)) { best = e; bestRes = res; bestStart = start; }
                    }
                    if (best == null)
                    {
                        NoProposal(fab == enemies.Count ? "tylko wojny fabularne ROT"
                                   : blocked > 0 && unsuit == 0 ? firstBlock
                                   : unsuit > 0 && blocked == 0 ? WhyUnsuit
                                   : "zgody i pokoj nieodpowiedni (rozne wojny)");
                        continue;
                    }
                    var er = best.RulingClan;
                    if (er == null) { NoProposal("wrog bez rodu panujacego"); continue; }

                    // wnioskodawca: rod AI "za pokojem z biedy" (T + P > 1), z wplywem ponad koszt wniosku (jak gra), najbiedniejszy
                    var cands = k.Clans.Where(c => c != null && !c.IsEliminated && c != Clan.PlayerClan && !c.IsUnderMercenaryService && c.Leader != null && c.Leader.IsAlive)
                                       .Select(c => new KeyValuePair<Clan, float>(c, s.T + ClanPoverty(c.Gold, s.Enemies)))
                                       .Where(x => x.Value > 1f && x.Key.Influence > dm.GetInfluenceCostOfProposingPeace(x.Key))
                                       .OrderByDescending(x => x.Value).ThenByDescending(x => x.Key.Influence)
                                       .Take(ProposerTries).ToList();
                    if (cands.Count == 0) { NoProposal("brak rodu za pokojem z biedy z wplywem na wniosek"); continue; }
                    // danina dzienna 0 (jak wniosek BK ForceProposePeaceFromLosingSide): trybut gry placa rody w nicosc, a odbiorca
                    // dostaje go z niczego (DefaultClanFinanceModel), Diplomacy rozciaga go na lata - E1b nie dokleja nowego strumienia;
                    // glos gry patrzy tylko na danine < 0, wiec glosy bez zmian. Reparacje Diplomacy jak przy kazdym pokoju.
                    MakePeaceKingdomDecision dec = null; Clan who = null; float sup = 0f, w = 0f;
                    _quiet = true;
                    try
                    {
                        foreach (var c in cands)
                        {
                            var d = new MakePeaceKingdomDecision(c.Key, best, 0, 0);
                            float v = d.DetermineSupport(c.Key, YesOf(d));
                            if (v > 0f) { dec = d; who = c.Key; sup = v; w = c.Value; break; }
                        }
                    }
                    finally { _quiet = false; }
                    if (dec == null) { NoProposal("glos wnioskodawcy <= 0"); continue; }

                    float bk = dm.GetScoreOfDeclaringPeace(k, best), thr = dm.GetDecisionMakingThreshold(k);
                    int cost = dm.GetInfluenceCostOfProposingPeace(who);
                    // wrog = krolestwo gracza (gracz nie najemnik): gra nie glosuje, tylko wysyla graczowi oferte pokoju
                    // (MakePeaceKingdomDecision.OnShowDecision -> PeaceOfferMapNotification, bez KingdomDecisionConcluded) - jak przy wniosku gry
                    bool toPlayer = best == Clan.PlayerClan.Kingdom && !Clan.PlayerClan.IsUnderMercenaryService;
                    lock (_lock)
                    {
                        if (toPlayer) _ppOffer.Inc(); else _mine[dec] = absDay;
                        _ppLast[k] = absDay; _ppMade.Inc();
                        int n; _ppByK.TryGetValue(k, out n); _ppByK[k] = n + 1;
                    }
                    Scribe.Line("Pokoj z biedy (E1b): dzien " + Day() + " - WNIOSEK Z BIEDY " + N(k) + " z " + N(best) + ": wnioskodawca " + N(who) + " (wplyw " + (int)who.Influence
                                + ", koszt wniosku " + cost + ", bieda skarbca + rodu " + w.ToString("0.00") + ", glos za " + (int)sup + "); " + N(k) + ": " + Desc(s)
                                + "; wynik wojny (gra, swoj - wroga) " + (int)bestRes + " - najgorszy z " + ok + " dopuszczonych (wrogow " + enemies.Count + ", fabularnych " + fab
                                + ", bez zgody " + blocked + ", pokoj nieodpowiedni " + unsuit + "); wynik pokoju BK " + (int)bk + " (prog " + (int)thr + (bk < thr ? " - gra by wniosku nie zlozyla" : "")
                                + "); danina dzienna 0" + (k == Clan.PlayerClan.Kingdom ? "; krolestwo gracza - wniosek czeka na liscie decyzji" : "")
                                + (toPlayer ? "; wrog to krolestwo gracza - gra wysyla graczowi oferte pokoju (bez glosowania)" : "") + ".");
                    _adding = dec;
                    try { k.AddDecision(dec, false); }   // wnioskodawca placi wplyw jak w grze; krolestwo AI glosuje od razu
                    finally { _adding = null; }
                }
                catch (Exception ex) { Stumble("PovertyPeace.Propose", ex); }
            }
        }

        private static void NoProposal(string why) { lock (_lock) { _ppWhy.Inc(why); } }

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
                // E1b (latki zostaja; stan kampanii od zera)
                foreach (var c in _ctrs) { c.D = 0; c.T = 0; }
                _cpWhy.D.Clear(); _cpWhy.T.Clear(); _ppWhy.D.Clear(); _ppWhy.T.Clear();
                _cpScoreSum = 0; _cpScoreN = 0;
                _ppByK.Clear(); _ppLast.Clear(); _mine.Clear();
                _lastGame = null; _inBk = false; _adding = null;
                _arr.Clear(); _nWorld = -1f; _arrLastDay = -1; _arrLastSum = -1;
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

    /// <summary>Start sesji (stan skarbcow), linia dobowa i decyzje krolestw "Pokoj z biedy (E1)", wnioski z biedy i diagnoza
    /// wnioskow "Pokoj z biedy (E1b)" (bez zapisu w grze).</summary>
    internal sealed class PovertyPeaceBehavior : CampaignBehaviorBase
    {
        public PovertyPeaceBehavior() { PovertyPeace.ResetSession(); }

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, s => PovertyPeace.OnSessionLaunched());
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, PovertyPeace.DailyLine);
            CampaignEvents.KingdomDecisionConcluded.AddNonSerializedListener(this, PovertyPeace.OnDecisionConcluded);
            CampaignEvents.KingdomDecisionAdded.AddNonSerializedListener(this, PovertyPeace.OnDecisionAdded);          // E1b: zrodlo wniosku
            CampaignEvents.KingdomDecisionCancelled.AddNonSerializedListener(this, PovertyPeace.OnDecisionCancelled);  // E1b: wniosek z biedy anulowany
        }

        public override void SyncData(IDataStore dataStore) { }
    }
}
