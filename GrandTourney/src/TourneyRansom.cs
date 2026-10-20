using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.TournamentGames;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;

namespace GrandTourney
{
    /// <summary>
    /// R3-04a (Jeff 20.10: lista 35 pomyslow, pkt 6 "tak") - WYKUP KONIA I ZBROI POKONANEGO. Zrodlo: docs/audyt-2026-10-09/04-wojna-armie-lupy-okupy.md
    /// rozdz. 2 pkt 8 (Dunk i ser Uthor Underleaf) i N6. Klucz TourneyRansom. Wylaczony = turnieje jak przed zmiana.
    ///
    /// PRZEGRANA WALKA: kazda przegrana walka w szrankach (gracz odpada raz - kazdy uczestnik oprocz zwyciezcy turnieju placi jeden raz).
    ///  - Turniej w misji (gracz walczy albo oglada; walki pominiete gra konczy tym samym TournamentMatch.End): postfiks na TournamentMatch.End -
    ///    kazdy uczestnik walki spoza Winners przegral. Zwyciezca nad nim: ten, kto go powalil (postfiks na TournamentFightMissionController.OnAgentRemoved,
    ///    sprawca - jezdziec, gdy uderzyl kon), jesli jest wsrod zwyciezcow walki po drugiej stronie; inaczej najlepszy wynikiem zwyciezca walki z innej
    ///    druzyny (walka pominieta, zwyciezca z punktow); inaczej najlepszy zwyciezca walki. Rozliczenie dopiero po misji (OnApplicationTick: brak misji
    ///    i stan mapy) - zadnych zmian ekwipunku i okien w arenie.
    ///    REZYGNACJA (recenzja 20.10): walka pominieta z graczem na nogach (TournamentBehavior.SkipMatch - "forfeit" albo "Leave") - gra zdejmuje cala
    ///    druzyne gracza bez walki; nikt jej nie pokonal, wiec gracz i jego druzyna (kto nie zostal powalony) nic nie oddaja. Reszta walki - jak zwykle.
    ///  - Turniej gry bez gracza (TournamentManager.ResolveTournament - gra zna tylko zwyciezce): drabinka pojedynkow 16 -> 8 -> 4 -> 2 -> 1
    ///    z listy uczestnikow gry; zwyciezca gry wygrywa kazdy swoj pojedynek, pozostale rozstrzyga wynik symulacji gry
    ///    (TournamentModel.GetTournamentSimulationScore x (0.75 + 0.25 x los) - ten sam wzor co SimulateTournament). Rozliczenie od razu (doba, bez misji).
    ///  - ODWOLANY turniej (recenzja 20.10): GrandTourney odwoluje turniej "wrog u bram" przez ResolveTournament, a gra wtedy go symuluje i wola
    ///    TournamentFinished. Odwolanie idzie przez CallOff - w nim zadnego wykupu (licznik "odwolane").
    /// KTO (recenzja 20.10): tylko rycerze - lord, wedrowiec albo bohater rodu gracza, walczacy (nie IsNoncombatant: damy, ktore NoblesFightPatch wpisuje
    ///  do drabinki, nic nie stawiaja), nie notabl (RBMTournament dopisuje notabli z tierem >= 5). Zolnierz z drabinki walczy w sprzecie miasta lub oddzialu
    ///  i nie ma nic do postawienia: walka z nim nic nie rozlicza (w obie strony). Ten sam rod - nic. Towarzysze i krewni gracza tylko wtedy, gdy gracz SAM
    ///  stal w szrankach (gra wpisuje ich takze, gdy gracz tylko oglada, i w turniejach gry, gdy jego druzyna stoi w miescie - bez jego zgody).
    /// STAWKA: kon i zbroja z ekwipunku bojowego przegranego (glowa, tulow, nogi, rece, plaszcz, kon, ladry), kazda sztuka za wartosc w obecnym stanie =
    ///  EquipmentElement.ItemValue (cena przedmiotu x mnoznik stanu - zuzycie Armoury i lup Spoils of War obnizaja; ceny historyczne Armoury, gdy sa).
    ///  Smok (ROT-Dragon, test jak Armoury DragonFeed.IsDragon) nie jest koniem turniejowym - nie przechodzi, jego ladry tez nie.
    /// WYKUP AI (recenzja 20.10 - jedna zasada wydatkow rodu): sztuka po sztuce od najdrozszej. Placi przegrany z wlasnej kiesy, potem glowa jego rodu,
    ///  lacznie najwyzej NADWYZKA rodu ponad rezerwe budzetu rodu Armoury (ClanBudget.ChestOf: G - R, ta sama rezerwa, ktora tnie pulap zoldu), minus to,
    ///  co rod juz dzis wydal na wykupy. Bez budzetu Armoury (rod poza krolestwem, dluznik, brak Armoury): ten sam ksztalt rezerwy z zoldu calego rodu -
    ///  R = max(20 000; 20 dni zoldu wszystkich partii i zalog) + 5 000 na doroslego. Wedrowiec bez rodu - cala wlasna kiesa. Na co nie stac - oddane.
    /// GRACZ (i towarzysze rodu gracza): okno po turnieju - "Pay N" (najdrozsze sztuki, na ktore starcza zlota, reszta oddana) albo "Yield..." -
    ///  drugie okno z potwierdzeniem "Yield" / "Back" (recenzja 20.10: Esc to akcja negatywna okna - Esc nigdy nie oddaje sprzetu, najwyzej cofa).
    ///  Zloto: GiveGoldAction.ApplyBetweenCharacters(platnik, odbiorca) - prawdziwy przelew; odbiorca = glowa rodu zwyciezcy (jak cena nagrody w grze,
    ///  GivePrizeToWinner), bez rodu - sam zwyciezca.
    /// ODDANE SZTUKI: schodza z ekwipunku przegranego (z tym samym stanem). Zwyciezca AI (recenzja 20.10) ZAKLADA sztuke, gdy moze ja nosic i jest lepsza
    ///  od jego (stara idzie dalej zamiast niej). Dalej - do taboru zwyciezcy: rod gracza - juki gracza (albo jego partii), AI - partia zwyciezcy, inaczej
    ///  partia glowy rodu; zwyciezca bez partii - targ miasta turnieju, miasto placi mu wartosc z nadwyzki ponad rezerwe (TownWageLink.Spare).
    /// ZAMIENNIK (recenzja 20.10 - nikt nie zostaje nagi na stale): przegrany AI uzupelnia pusty tulow, glowe, nogi, rece i konia: najpierw z taboru
    ///  wlasnej partii (za darmo - to jego), potem na targu miasta (turnieju, a pozniej - w kazdym miescie, w ktorym stanie): najlepsza sztuke za nadwyzke
    ///  rodu, a gdy nadwyzki brak - najtansza, na ktora ma zloto (bez konia i zbroi rycerz nie pojdzie na wojne). Zloto -> kasa miasta, sztuka z polki.
    ///  Kogo nie stac - czeka (lista w zapisie, gt_ransomBare) i probuje co dobe; linia doby liczy, ilu takich jest.
    /// Stan: pamiec (walki misji czekajace na rozliczenie, kolejka okien gracza, liczniki doby) + w zapisie tylko lista bohaterow bez zamiennika
    ///  (List string przez dataStore.SyncData - jak gt_events; stary zapis bez klucza = pusta lista). Wyjatek - licznik potkniec per obiekt, nic nie gasimy.
    ///  Linie "Wykup (R3-04a):".
    /// </summary>
    internal static class TourneyRansom
    {
        private const string P = "Wykup (R3-04a): ";
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private static readonly EquipmentIndex[] Slots =
        {
            EquipmentIndex.Body, EquipmentIndex.Horse, EquipmentIndex.Head, EquipmentIndex.Leg,
            EquipmentIndex.Gloves, EquipmentIndex.Cape, EquipmentIndex.HorseHarness
        };

        // zamiennik: sloty, bez ktorych rycerz nie sluzy (plaszcz i ladry - nie)
        private static readonly EquipmentIndex[] RefitSlots =
        {
            EquipmentIndex.Body, EquipmentIndex.Horse, EquipmentIndex.Head, EquipmentIndex.Leg, EquipmentIndex.Gloves
        };

        private sealed class Bout { public CharacterObject Loser, Victor; public Town Town; public bool Mission; }

        private struct Piece { public EquipmentIndex Slot; public EquipmentElement El; public int Worth; }

        private sealed class Offer { public Hero Loser, Victor; public Town Town; public bool Confirm; }

        // misja: kto kogo powalil w biezacej walce; walki czekajace na rozliczenie po misji
        private static readonly Dictionary<CharacterObject, CharacterObject> _ko = new Dictionary<CharacterObject, CharacterObject>();
        private static readonly List<Bout> _missionBouts = new List<Bout>();
        private static readonly List<Offer> _offers = new List<Offer>();
        private static bool _popupOpen;
        private static bool _playerInLists;   // gracz stal w szrankach tej misji (jego rod stawia tylko wtedy)
        private static bool _skipping;        // trwa TournamentBehavior.SkipMatch (pominiecie walki / Leave)
        private static bool _playerDown;      // gracz zostal powalony w biezacej walce (wtedy pominiecie nie jest rezygnacja)
        private static int _callOffDepth;     // trwa odwolanie turnieju przez GrandTourney (CallOff)

        // rod -> zloto wydane dzis na wykupy i zamienniki (nadwyzka rodu liczy sie raz na dobe)
        private static readonly Dictionary<Clan, long> _clanSpent = new Dictionary<Clan, long>();
        // bohater AI -> sloty bez zamiennika (bit 1 << slot); w zapisie jako "id|maska"
        private static readonly Dictionary<string, int> _bare = new Dictionary<string, int>();

        // liczniki doby
        private static int _bouts, _boutsSim, _boutsMis, _tourneys, _commoner, _kin, _noKit, _gone, _beast, _settled;
        private static int _notKnight, _unasked, _forfeit, _calledOff, _fallbackRoom;
        private static int _paidN, _yieldN, _toRoster, _toMarket, _worn, _offersShown, _plPaidN, _plYieldN, _plWonN, _plWonPieces;
        private static int _refitBag, _refitBest, _refitCheap, _refitNone, _refitLater;
        private static long _paid, _yieldWorth, _marketPaid, _plPaid, _plWon, _refitGold;
        private static int _stumbles, _stumblesDay;
        private static bool _patched;

        internal static bool On
        {
            get { var s = Settings.Current; return s != null && s.Enabled && s.TourneyRansom; }
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++; _stumblesDay++;
            try { Log.Error("TourneyRansom." + where, e); } catch { }
        }

        /// <summary>Nowa kampania i kazde wczytanie (konstruktor TourneyRansomBehavior).</summary>
        internal static void Reset()
        {
            _ko.Clear(); _missionBouts.Clear(); _offers.Clear(); _bare.Clear();
            _popupOpen = _playerInLists = _skipping = _playerDown = false; _callOffDepth = 0; _stumbles = 0;
            ClearDay();
        }

        private static void ClearDay()
        {
            _bouts = _boutsSim = _boutsMis = _tourneys = _commoner = _kin = _noKit = _gone = _beast = _settled = 0;
            _notKnight = _unasked = _forfeit = _calledOff = _fallbackRoom = 0;
            _paidN = _yieldN = _toRoster = _toMarket = _worn = _offersShown = _plPaidN = _plYieldN = _plWonN = _plWonPieces = 0;
            _refitBag = _refitBest = _refitCheap = _refitNone = _refitLater = 0;
            _paid = _yieldWorth = _marketPaid = _plPaid = _plWon = _refitGold = 0;
            _clanSpent.Clear();
            _stumblesDay = 0;
        }

        // ------------------------------------------------------------ latki

        internal static void ApplyAll(Harmony h)
        {
            if (_patched || h == null) return;
            _patched = true;
            int n = 0;
            try
            {
                var end = AccessTools.DeclaredMethod(typeof(TournamentMatch), "End");
                if (end != null) { h.Patch(end, postfix: new HarmonyMethod(typeof(TourneyRansom), nameof(EndPostfix))); n++; }
                else Log.Error("TourneyRansom: brak TournamentMatch.End - walki w misji bez wykupu", new MissingMethodException("TournamentMatch.End"));
                var start = AccessTools.DeclaredMethod(typeof(TournamentMatch), "Start");
                if (start != null) { h.Patch(start, postfix: new HarmonyMethod(typeof(TourneyRansom), nameof(StartPostfix))); n++; }
                var tfmc = AccessTools.TypeByName("SandBox.Tournaments.MissionLogics.TournamentFightMissionController");
                var rem = tfmc != null ? AccessTools.DeclaredMethod(tfmc, "OnAgentRemoved") : null;
                if (rem != null) { h.Patch(rem, postfix: new HarmonyMethod(typeof(TourneyRansom), nameof(KoPostfix))); n++; }
                var tb = AccessTools.TypeByName("SandBox.Tournaments.MissionLogics.TournamentBehavior");
                var skip = tb != null ? AccessTools.DeclaredMethod(tb, "SkipMatch") : null;
                if (skip != null)
                {
                    h.Patch(skip, prefix: new HarmonyMethod(typeof(TourneyRansom), nameof(SkipPrefix)),
                            finalizer: new HarmonyMethod(typeof(TourneyRansom), nameof(SkipFinalizer)));
                    n++;
                }
                Log.Info(P + "latki " + n + "/4 (TournamentMatch.End, TournamentMatch.Start, TournamentFightMissionController.OnAgentRemoved, TournamentBehavior.SkipMatch"
                         + (rem == null ? " - BRAK OnAgentRemoved: zwyciezca nad przegranym z punktow walki" : "")
                         + (skip == null ? " - BRAK SkipMatch: rezygnacja gracza liczy sie jak przegrana" : "") + ").");
            }
            catch (Exception e) { Stumble("ApplyAll", e); }
        }

        /// <summary>Nowa walka - kto kogo powalil liczymy od zera.
        /// SkipMatch tez wola Start (na walce juz trwajacej) - wtedy nie czyscimy: powalenia z prawdziwej walki zostaja do End.</summary>
        public static void StartPostfix() { try { if (_skipping) return; _ko.Clear(); _playerDown = false; } catch { } }

        public static void SkipPrefix() { _skipping = true; }

        public static void SkipFinalizer() { _skipping = false; }

        /// <summary>Kto powalil przegranego (tylko bohaterowie jako przegrani - zolnierz nie ma czego oddac); powalenie gracza - zawsze.</summary>
        public static void KoPostfix(Agent affectedAgent, Agent affectorAgent, AgentState agentState)
        {
            try
            {
                // tylko powalenie w walce (sprzatanie areny po walce usuwa agentow innym stanem)
                if (!On || affectedAgent == null || !affectedAgent.IsHuman) return;
                if (agentState != AgentState.Unconscious && agentState != AgentState.Killed) return;
                var lc = affectedAgent.Character as CharacterObject;
                if (lc != null && lc == CharacterObject.PlayerCharacter) _playerDown = true;
                if (affectorAgent == null) return;
                var a = affectorAgent.IsMount && affectorAgent.RiderAgent != null ? affectorAgent.RiderAgent : affectorAgent;
                if (a == affectedAgent) return;
                var vc = a.Character as CharacterObject;
                if (lc == null || vc == null || !lc.IsHero) return;
                _ko[lc] = vc;
            }
            catch (Exception e) { Stumble("Ko", e); }
        }

        /// <summary>Koniec walki w misji: przegrani (spoza Winners) z ich zwyciezcami czekaja na rozliczenie po misji. Rezygnacja gracza - jego druzyna nic.</summary>
        public static void EndPostfix(TournamentMatch __instance)
        {
            try
            {
                if (!On || __instance == null) return;
                var winners = new List<TournamentParticipant>(__instance.Winners);
                if (winners.Count == 0) return;
                TournamentTeam plTeam = null; bool hasPlayer = false;
                foreach (var p in __instance.Participants)
                    if (p != null && p.Character == CharacterObject.PlayerCharacter) { hasPlayer = true; plTeam = p.Team; break; }
                if (hasPlayer) _playerInLists = true;
                bool forfeit = _skipping && hasPlayer && !_playerDown;
                Town town = null;
                try { var st = Settlement.CurrentSettlement; town = st != null ? st.Town : null; } catch { }
                foreach (var p in __instance.Participants)
                {
                    if (p == null || p.Character == null || winners.Contains(p)) continue;
                    if (forfeit && p.Team == plTeam && !_ko.ContainsKey(p.Character)) { if (p.Character.IsHero) _forfeit++; continue; }
                    var v = VictorOf(p, winners);
                    if (v == null || v.Character == null) continue;
                    _missionBouts.Add(new Bout { Loser = p.Character, Victor = v.Character, Town = town, Mission = true });
                }
                _ko.Clear(); _playerDown = false;
            }
            catch (Exception e) { Stumble("End", e); }
        }

        private static TournamentParticipant VictorOf(TournamentParticipant loser, List<TournamentParticipant> winners)
        {
            CharacterObject ko;
            if (_ko.TryGetValue(loser.Character, out ko))
                foreach (var w in winners)
                    if (w != null && w.Character == ko && w.Team != loser.Team) return w;
            TournamentParticipant best = null;
            foreach (var w in winners)
                if (w != null && w.Team != loser.Team && (best == null || w.Score > best.Score)) best = w;
            if (best != null) return best;
            foreach (var w in winners)
                if (w != null && (best == null || w.Score > best.Score)) best = w;
            return best;
        }

        // ------------------------------------------------------------ zdarzenia

        /// <summary>Odwolanie turnieju przez GrandTourney (wrog u bram): gra symuluje go w ResolveTournament i wola TournamentFinished - tu bez wykupu.</summary>
        internal static void CallOff(TournamentGame game, Town town)
        {
            _callOffDepth++;
            try { Campaign.Current.TournamentManager.ResolveTournament(game, town); }
            finally { _callOffDepth--; }
        }

        /// <summary>Koniec turnieju (CampaignEvents.TournamentFinished). W misji - walki juz zebrane, rozliczenie po misji; bez misji - drabinka.</summary>
        internal static void OnFinished(CharacterObject winner, MBReadOnlyList<CharacterObject> participants, Town town)
        {
            try
            {
                if (!On || town == null) return;
                if (_callOffDepth > 0)
                {
                    _calledOff++;
                    Log.Info(P + "turniej w " + town.Name + " odwolany przez GrandTourney (wrog u bram) - bez wykupu.");
                    return;
                }
                if (Mission.Current != null)
                {
                    foreach (var b in _missionBouts) if (b.Town == null) b.Town = town;
                    return;
                }
                if (winner == null || participants == null) return;
                var field = new List<CharacterObject>();
                foreach (var c in participants)
                    if (c != null && c != winner && !field.Contains(c)) field.Add(c);
                for (int i = field.Count - 1; i > 0; i--)
                {
                    int j = MBRandom.RandomInt(i + 1);
                    var t = field[i]; field[i] = field[j]; field[j] = t;
                }
                field.Insert(0, winner);
                var model = Campaign.Current != null ? Campaign.Current.Models.TournamentModel : null;
                var bouts = new List<Bout>();
                while (field.Count > 1)
                {
                    var next = new List<CharacterObject>((field.Count + 1) / 2);
                    for (int i = 0; i < field.Count; i += 2)
                    {
                        if (i + 1 >= field.Count) { next.Add(field[i]); continue; }   // wolny los
                        var a = field[i]; var b = field[i + 1];
                        CharacterObject w, l;
                        if (a == winner) { w = a; l = b; }
                        else if (b == winner) { w = b; l = a; }
                        else if (Score(model, a) >= Score(model, b)) { w = a; l = b; }
                        else { w = b; l = a; }
                        next.Add(w);
                        bouts.Add(new Bout { Loser = l, Victor = w, Town = town, Mission = false });
                    }
                    field = next;
                }
                SettleAll(bouts, town, false);   // turniej gry - gracza w nim nie bylo
            }
            catch (Exception e) { Stumble("OnFinished", e); }
        }

        private static float Score(TaleWorlds.CampaignSystem.ComponentInterfaces.TournamentModel model, CharacterObject c)
        {
            try
            {
                float s = model != null ? model.GetTournamentSimulationScore(c) : c.Level;
                return s * (0.75f + 0.25f * MBRandom.RandomFloat);
            }
            catch (Exception e) { Stumble("Score", e); return MBRandom.RandomFloat; }
        }

        /// <summary>Co klatke (SubModuleMain.OnApplicationTick): po misji rozlicz walki misji, potem okna gracza po kolei. Pusta kolejka - od razu wyjscie.</summary>
        internal static void AppTick()
        {
            if (_missionBouts.Count == 0 && (_offers.Count == 0 || _popupOpen)) return;
            try
            {
                if (Campaign.Current == null || Game.Current == null || Mission.Current != null) return;
                if (!(Game.Current.GameStateManager.ActiveState is MapState)) return;
                var cm = Campaign.Current.ConversationManager;
                if (cm != null && cm.IsConversationInProgress) return;
                if (_missionBouts.Count > 0)
                {
                    var list = new List<Bout>(_missionBouts);
                    _missionBouts.Clear();
                    bool fought = _playerInLists;
                    _playerInLists = false; _skipping = false; _playerDown = false;
                    Town town = null;
                    foreach (var b in list) if (b.Town != null) { town = b.Town; break; }
                    SettleAll(list, town, fought);
                }
                if (_offers.Count > 0 && !_popupOpen) ShowNext();
            }
            catch (Exception e) { _missionBouts.Clear(); Stumble("AppTick", e); }
        }

        // ------------------------------------------------------------ rozliczenie

        private static void SettleAll(List<Bout> bouts, Town town, bool playerFought)
        {
            if (bouts == null || bouts.Count == 0) return;
            _tourneys++;
            int n = 0, pieces = 0, yielded = 0; long gold = 0;
            bool sim = !bouts[0].Mission;
            foreach (var b in bouts)
            {
                try
                {
                    _bouts++;
                    if (b.Mission) _boutsMis++; else _boutsSim++;
                    int pc, yc; long g;
                    if (Settle(b, town ?? b.Town, playerFought, out pc, out yc, out g)) { n++; pieces += pc; yielded += yc; gold += g; }
                }
                catch (Exception e) { Stumble("Settle(" + (b.Loser != null ? b.Loser.StringId : "?") + ")", e); }
            }
            Log.Info(P + "turniej w " + (town != null ? town.Name.ToString() : "?") + " (" + (sim ? "turniej gry - drabinka pojedynkow" : "szranki w misji")
                     + (sim ? "" : playerFought ? ", gracz w szrankach" : ", gracz oglada") + "): walk " + bouts.Count + ", z wykupem " + n
                     + " | wykupione sztuki " + pieces + " za " + gold + " zlota, oddane " + yielded
                     + (_offers.Count > 0 ? " | okna gracza w kolejce " + _offers.Count : "") + ".");
        }

        /// <summary>Rycerz, ktory stawia konia i zbroje: lord, wedrowiec albo bohater rodu gracza; walczacy; nie notabl.</summary>
        private static bool Knight(Hero h)
        {
            if (h == null) return false;
            if (h == Hero.MainHero) return true;
            try
            {
                if (h.IsNotable || h.IsNoncombatant) return false;
                return h.IsLord || h.IsWanderer || h.Clan == Clan.PlayerClan;
            }
            catch (Exception e) { Stumble("Knight(" + h.StringId + ")", e); return false; }
        }

        private static bool PlayerSide(Hero h) { return h != null && (h == Hero.MainHero || h.Clan == Clan.PlayerClan); }

        /// <summary>Jedna walka. true = byla stawka (rozliczona albo okno gracza w kolejce).</summary>
        private static bool Settle(Bout b, Town town, bool playerFought, out int paidPieces, out int yieldPieces, out long gold)
        {
            paidPieces = yieldPieces = 0; gold = 0;
            if (b.Loser == null || b.Victor == null || b.Loser == b.Victor) return false;
            if (!b.Loser.IsHero || !b.Victor.IsHero) { _commoner++; return false; }
            var L = b.Loser.HeroObject; var V = b.Victor.HeroObject;
            if (L == null || V == null || !L.IsAlive || !V.IsAlive) { _gone++; return false; }
            if (!Knight(L) || !Knight(V)) { _notKnight++; return false; }
            // rod gracza (poza samym graczem) - tylko gdy gracz sam stal w szrankach (gra wpisuje towarzyszy takze bez jego zgody)
            if (((L != Hero.MainHero && L.Clan == Clan.PlayerClan) || (V != Hero.MainHero && V.Clan == Clan.PlayerClan)) && !playerFought) { _unasked++; return false; }
            if (L.Clan != null && L.Clan == V.Clan) { _kin++; return false; }
            var stake = Stake(L, true);
            if (stake.Count == 0) { _noKit++; return false; }
            if (PlayerSide(L))
            {
                _offers.Add(new Offer { Loser = L, Victor = V, Town = town });
                return true;
            }
            // AI: wykup od najdrozszej, na ile starcza nadwyzki rodu; reszta oddana, potem zamiennik
            var lead = Lead(L);
            long room = Room(L);
            var recv = Receiver(V);
            bool toPlayer = PlayerSide(V);
            var given = new List<Piece>();
            foreach (var pc in stake)
            {
                int own = Math.Max(0, L.Gold), house = lead != null ? Math.Max(0, lead.Gold) : 0;
                if (pc.Worth <= room && pc.Worth <= (long)own + house)
                {
                    int a = Math.Min(own, pc.Worth), c = pc.Worth - a;
                    if (a > 0) GiveGoldAction.ApplyBetweenCharacters(L, recv, a, true);
                    if (c > 0) GiveGoldAction.ApplyBetweenCharacters(lead, recv, c, true);
                    room -= pc.Worth; Spend(L.Clan, pc.Worth);
                    paidPieces++; gold += pc.Worth; _paidN++; _paid += pc.Worth;
                    if (toPlayer) { _plWon += pc.Worth; }
                }
                else given.Add(pc);
            }
            var moved = new List<Piece>();
            int mask = 0;
            foreach (var pc in given)
            {
                if (Yield(L, V, recv, pc, town)) { moved.Add(pc); mask |= Bit(pc.Slot); yieldPieces++; _yieldN++; _yieldWorth += pc.Worth; if (toPlayer) _plWonPieces++; }
            }
            if (mask != 0) Refit(L, town, mask, false);
            _settled++;
            if (toPlayer && (paidPieces > 0 || yieldPieces > 0))
            {
                _plWonN++;
                var sb = new StringBuilder();
                sb.Append(L.Name).Append(" was bested by ").Append(V == Hero.MainHero ? "you" : V.Name.ToString()).Append(" in the lists.");
                if (gold > 0) sb.Append(" Ransom of horse and armour paid to you: ").Append(gold).Append(" gold.");
                if (yieldPieces > 0) sb.Append(" Yielded to your baggage: ").Append(Names(moved)).Append('.');
                Log.Player(sb.ToString());
            }
            return true;
        }

        private static int Bit(EquipmentIndex i) { return 1 << (int)i; }

        /// <summary>m95-turniej: okup sztuki = wartosc w obecnym stanie x Tourney Ransom Share (0.05-1, domyslnie 0.25), najmniej 1 zloto.</summary>
        private static int ShareOf(int w)
        {
            float s = Settings.Current != null ? Settings.Current.TourneyRansomShare : 1f;
            if (!(s > 0f) || s >= 1f) return w;
            if (s < 0.05f) s = 0.05f;
            return Math.Max(1, (int)Math.Round(w * (double)s));
        }

        /// <summary>m95-turniej: dopisek do okna gracza - okup to czesc wartosci.</summary>
        private static string ShareNote()
        {
            float s = Settings.Current != null ? Settings.Current.TourneyRansomShare : 1f;
            if (!(s > 0f) || s >= 1f) return "";
            return " (the ransom of the lists: " + (s == 0.25f ? "a quarter" : Math.Round(s * 100f) + "%") + " of their worth)";
        }

        /// <summary>Kon i zbroja przegranego z wartoscia w obecnym stanie, od najdrozszej. Smok i jego ladry - nie.</summary>
        private static List<Piece> Stake(Hero h, bool count)
        {
            var list = new List<Piece>();
            var eq = h != null ? h.BattleEquipment : null;
            if (eq == null || (Campaign.Current != null && ReferenceEquals(eq, Campaign.Current.DeadBattleEquipment))) return list;   // wspolny zestaw zmarlych - nie ruszac
            bool dragon = false;
            try { var hs = eq[EquipmentIndex.Horse]; dragon = !hs.IsEmpty && IsDragon(hs.Item); } catch { }
            if (dragon && count) _beast++;
            foreach (var idx in Slots)
            {
                if (dragon && (idx == EquipmentIndex.Horse || idx == EquipmentIndex.HorseHarness)) continue;
                var el = eq[idx];
                if (el.IsEmpty || el.Item == null) continue;
                int w = el.ItemValue;
                if (w <= 0) continue;
                // m95-turniej (Jeff 20.10: "zaplacilem ponad 1k za przegrana ... ale dostalem w tym samym czasie ponad 8k zlota"): okup szranek to czesc wartosci
                // (Tourney Ransom Share, domyslnie 1/4) - turniej na tepa bron, nie wojna; ta sama stawka w obie strony, gracz i AI
                w = ShareOf(w);
                list.Add(new Piece { Slot = idx, El = el, Worth = w });
            }
            list.Sort((a, b) => b.Worth.CompareTo(a.Worth));
            return list;
        }

        /// <summary>Jak Armoury DragonFeed.IsDragon (GrandTourney nie zalezy od Armoury).</summary>
        private static bool IsDragon(ItemObject it)
        {
            try
            {
                if (it == null || it.ItemType != ItemObject.ItemTypeEnum.Horse) return false;
                var id = it.StringId ?? "";
                if (id.StartsWith("dragon_", StringComparison.Ordinal)) return true;
                var mu = it.HorseComponent != null && it.HorseComponent.Monster != null ? (it.HorseComponent.Monster.MonsterUsage ?? "") : "";
                return mu == "dragon" || mu == "dragonfly";
            }
            catch { return false; }
        }

        // ------------------------------------------------------------ pieniadze rodu (jedna zasada z budzetem rodu Armoury)

        private delegate bool ChestFn(Clan c, out double g, out double r, out bool war);
        private static ChestFn _chest;
        private static bool _chestTried;

        /// <summary>Armoury.ClanBudget.ChestOf(rod, out G, out R, out wojna) - szukane raz; null = brak Armoury (zapasowa rezerwa).</summary>
        private static ChestFn Chest()
        {
            if (_chestTried) return _chest;
            _chestTried = true;
            try
            {
                foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (a.GetName().Name != "Armoury") continue;
                    var t = a.GetType("Armoury.ClanBudget", false);
                    var m = t != null ? t.GetMethod("ChestOf", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null,
                        new[] { typeof(Clan), typeof(double).MakeByRefType(), typeof(double).MakeByRefType(), typeof(bool).MakeByRefType() }, null) : null;
                    if (m != null) _chest = (ChestFn)Delegate.CreateDelegate(typeof(ChestFn), m);
                    break;
                }
                Log.Info(P + "nadwyzka rodu z budzetu rodu Armoury (ClanBudget.ChestOf) - " + (_chest != null ? "PODPIETA" : "BRAK (rezerwa zapasowa z zoldu rodu)"));
            }
            catch (Exception e) { _chest = null; Stumble("Chest", e); }
            return _chest;
        }

        /// <summary>Glowa rodu przegranego, ktora doplaca (nie on sam, nie gracz).</summary>
        private static Hero Lead(Hero h)
        {
            var lead = h.Clan != null ? h.Clan.Leader : null;
            if (lead == h || lead == Hero.MainHero || lead == null || !lead.IsAlive) return null;
            return lead;
        }

        /// <summary>Ile rod AI moze dzis jeszcze dac na wykup: G - R budzetu rodu Armoury (bez budzetu - G - R zapasowe), minus wydane dzis. Bez rodu - cala kiesa.</summary>
        private static long Room(Hero h)
        {
            var c = h.Clan;
            if (c == null) return Math.Max(0, h.Gold);
            double G = 0, R = 0; bool war, ok = false;
            var f = Chest();
            if (f != null)
            {
                try { ok = f(c, out G, out R, out war); }
                catch (Exception e) { ok = false; Stumble("ChestOf(" + c.StringId + ")", e); }
            }
            if (!ok)
            {
                _fallbackRoom++;
                int adults; G = FamilyGold(c, out adults);
                R = Math.Max(20000.0, 20.0 * ClanWage(c)) + 5000.0 * adults;
            }
            long spent; _clanSpent.TryGetValue(c, out spent);
            // scalenie z p5-rody (posag, pomoc na okup): budzet z rana minus wykupy dzis albo kiesy dzis - mniejsze (jak VassalRansomAid/AiDowry)
            if (ok) { int a; double live = FamilyGold(c, out a); return Math.Max(0, (long)(Math.Min(G - spent, live) - R)); }
            return Math.Max(0, (long)(G - R) - spent);
        }

        private static void Spend(Clan c, long gold)
        {
            if (c == null || gold <= 0) return;
            long s; _clanSpent.TryGetValue(c, out s);
            _clanSpent[c] = s + gold;
        }

        private static double FamilyGold(Clan c, out int adults)
        {
            adults = 0; double g = 0;
            try
            {
                int age = Campaign.Current.Models.AgeModel.HeroComesOfAge;
                foreach (var h in c.Heroes)
                    if (h != null && h.IsAlive && h.Age >= age) { adults++; g += Math.Max(0, h.Gold); }
            }
            catch (Exception e) { Stumble("FamilyGold(" + c.StringId + ")", e); }
            return g;
        }

        private static double ClanWage(Clan c)
        {
            double w = 0;
            try
            {
                foreach (var wp in c.WarPartyComponents) { var mp = wp != null ? wp.MobileParty : null; if (mp != null && mp.IsActive) w += Math.Max(0, mp.TotalWage); }
                foreach (var t in c.Fiefs) { var gp = t != null ? t.GarrisonParty : null; if (gp != null && gp.IsActive) w += Math.Max(0, gp.TotalWage); }
            }
            catch (Exception e) { Stumble("ClanWage(" + c.StringId + ")", e); }
            return w;
        }

        /// <summary>Odbiorca zlota: glowa rodu zwyciezcy (jak cena nagrody w grze), bez rodu - sam zwyciezca.</summary>
        private static Hero Receiver(Hero v)
        {
            var c = v.Clan;
            if (c != null && c.Leader != null && c.Leader.IsAlive) return c.Leader;
            return v;
        }

        // ------------------------------------------------------------ sztuki

        /// <summary>Tabor zwyciezcy: rod gracza - jego partia albo juki gracza; AI - jego partia, inaczej partia glowy rodu; null = brak (targ miasta).</summary>
        private static ItemRoster RosterOf(Hero v)
        {
            if (PlayerSide(v))
            {
                var mp = v.PartyBelongedTo;
                if (mp != null && mp.IsActive && mp.ItemRoster != null) return mp.ItemRoster;
                return MobileParty.MainParty != null ? MobileParty.MainParty.ItemRoster : null;
            }
            var p = v.PartyBelongedTo;
            if (p != null && p.IsActive && !p.IsMainParty && p.ItemRoster != null) return p.ItemRoster;
            var lead = v.Clan != null ? v.Clan.Leader : null;
            var lp = lead != null && lead != Hero.MainHero ? lead.PartyBelongedTo : null;
            if (lp != null && lp.IsActive && !lp.IsMainParty && lp.ItemRoster != null) return lp.ItemRoster;
            return null;
        }

        /// <summary>Czy bohater moze to nosic w tym slocie: slot, plec, wymog umiejetnosci gry (CharacterHelper.CanUseItem); kon - wierzchowy, nie smok,
        /// zgodny z ladrami; ladry - zgodne z koniem.</summary>
        private static bool Fits(Hero h, EquipmentElement el, EquipmentIndex slot)
        {
            var it = el.Item;
            if (it == null || h == null) return false;
            try
            {
                if (!Equipment.IsItemFitsToSlot(slot, it)) return false;
                if (h.IsFemale && (it.ItemFlags & ItemFlags.NotUsableByFemale) != 0) return false;
                if (!h.IsFemale && (it.ItemFlags & ItemFlags.NotUsableByMale) != 0) return false;
                var eq = h.BattleEquipment;
                if (slot == EquipmentIndex.Horse)
                {
                    if (IsDragon(it) || it.HorseComponent == null || !it.HorseComponent.IsRideable || it.HorseComponent.IsPackAnimal) return false;
                    var hn = eq[EquipmentIndex.HorseHarness];
                    if (!hn.IsEmpty && !Family(it, hn.Item)) return false;
                }
                else if (slot == EquipmentIndex.HorseHarness)
                {
                    var hs = eq[EquipmentIndex.Horse];
                    if (hs.IsEmpty || IsDragon(hs.Item) || !Family(hs.Item, it)) return false;
                }
                TextObject why;
                return global::Helpers.CharacterHelper.CanUseItem(h.CharacterObject, el, out why);
            }
            catch (Exception e) { Stumble("Fits(" + h.StringId + ")", e); return false; }
        }

        private static bool Family(ItemObject horse, ItemObject harness)
        {
            var m = horse != null && horse.HorseComponent != null ? horse.HorseComponent.Monster : null;
            var a = harness != null ? harness.ArmorComponent : null;
            return m != null && a != null && m.FamilyType == a.FamilyType;
        }

        private static bool OwnKit(Hero h)
        {
            return h != null && h.BattleEquipment != null && (Campaign.Current == null || !ReferenceEquals(h.BattleEquipment, Campaign.Current.DeadBattleEquipment));
        }

        /// <summary>Zwyciezca AI zaklada wygrana sztuke, gdy moze ja nosic i jest lepsza od jego (smoka nie zamienia); old = zdjeta sztuka (moze byc pusta).</summary>
        private static bool Wear(Hero v, EquipmentElement el, EquipmentIndex slot, out EquipmentElement old)
        {
            old = EquipmentElement.Invalid;
            if (PlayerSide(v) || !OwnKit(v)) return false;   // gracz i jego rod - decyduje gracz (sztuka w jukach)
            var eq = v.BattleEquipment;
            if (slot == EquipmentIndex.Horse || slot == EquipmentIndex.HorseHarness)
            {
                var hs = eq[EquipmentIndex.Horse];
                if (!hs.IsEmpty && IsDragon(hs.Item)) return false;
            }
            var cur = eq[slot];
            if (!cur.IsEmpty && cur.ItemValue >= el.ItemValue) return false;
            if (!Fits(v, el, slot)) return false;
            old = cur;
            eq.AddEquipmentToSlotWithoutAgent(slot, el);
            _worn++;
            return true;
        }

        /// <summary>Sztuka schodzi z ekwipunku przegranego do zwyciezcy: AI zaklada ja, gdy lepsza (stara idzie dalej zamiast niej); dalej do taboru
        /// zwyciezcy albo na targ miasta, ktore placi zwyciezcy jej wartosc z nadwyzki.</summary>
        private static bool Yield(Hero loser, Hero victor, Hero recv, Piece pc, Town town)
        {
            try
            {
                var cur = loser.BattleEquipment[pc.Slot];
                if (cur.IsEmpty || cur.Item != pc.El.Item) return false;   // sztuka zmienila sie po drodze
                var ro = RosterOf(victor);
                Settlement st = town != null ? town.Settlement : null;
                if (ro == null && (st == null || st.ItemRoster == null)) return false;   // nie ma dokad - sztuka zostaje
                loser.BattleEquipment.AddEquipmentToSlotWithoutAgent(pc.Slot, EquipmentElement.Invalid);
                EquipmentElement old;
                var store = Wear(victor, cur, pc.Slot, out old) ? old : cur;
                if (store.IsEmpty || store.Item == null) return true;   // zalozona w pusty slot - nic do taboru
                if (ro != null) { ro.AddToCounts(store, 1); _toRoster++; }
                else
                {
                    st.ItemRoster.AddToCounts(store, 1); _toMarket++;
                    int pay = Math.Min(Math.Max(0, store.ItemValue), TownWageLink.Spare(st));
                    if (pay > 0) { GiveGoldAction.ApplyForSettlementToCharacter(st, recv, pay, true); _marketPaid += pay; }
                }
                return true;
            }
            catch (Exception e) { Stumble("Yield(" + (loser != null ? loser.StringId : "?") + ")", e); return false; }
        }

        // ------------------------------------------------------------ zamiennik dla przegranego AI

        /// <summary>Puste sloty z maski: najpierw tabor wlasnej partii, potem targ miasta (najlepsza za nadwyzke rodu, inaczej najtansza na ktora ma zloto).
        /// Czego nie ma - zostaje na liscie (probuje co dobe w miescie, w ktorym stoi). later = proba z doby.</summary>
        private static void Refit(Hero h, Town town, int mask, bool later)
        {
            if (h == null || !h.IsAlive || PlayerSide(h) || !OwnKit(h)) { if (h != null) _bare.Remove(h.StringId); return; }
            int left = 0;
            long room = -1;
            var lead = Lead(h);
            foreach (var slot in RefitSlots)
            {
                if ((mask & Bit(slot)) == 0) continue;
                try
                {
                    if (!h.BattleEquipment[slot].IsEmpty) continue;   // juz ma (np. kupil sam)
                    if (FromBag(h, slot)) { _refitBag++; continue; }
                    if (room < 0) room = Room(h);
                    int how = town != null ? Buy(h, lead, town, slot, ref room) : 0;
                    if (how == 1) _refitBest++;
                    else if (how == 2) _refitCheap++;
                    else left |= Bit(slot);
                }
                catch (Exception e) { left |= Bit(slot); Stumble("Refit(" + h.StringId + ")", e); }
            }
            if (left != 0) { _bare[h.StringId] = left; if (!later) _refitNone++; }
            else { _bare.Remove(h.StringId); if (later) _refitLater++; }
        }

        /// <summary>Najlepsza pasujaca sztuka z taboru partii, w ktorej jedzie (partia jego rodu, nie gracza) - za darmo, to jego.</summary>
        private static bool FromBag(Hero h, EquipmentIndex slot)
        {
            var mp = h.PartyBelongedTo;
            if (mp == null || !mp.IsActive || mp.IsMainParty || mp.ItemRoster == null) return false;
            var ld = mp.LeaderHero;
            if (ld == null || ld.Clan != h.Clan) return false;
            var r = mp.ItemRoster;
            int bi = -1, bv = -1; EquipmentElement best = EquipmentElement.Invalid;
            for (int i = 0; i < r.Count; i++)
            {
                var e = r.GetElementCopyAtIndex(i);
                if (e.Amount <= 0) continue;
                var el = e.EquipmentElement;
                if (el.Item == null || !Equipment.IsItemFitsToSlot(slot, el.Item)) continue;
                int v = el.ItemValue;
                if (v <= bv || !Fits(h, el, slot)) continue;
                bi = i; bv = v; best = el;
            }
            if (bi < 0) return false;
            r.AddToCounts(best, -1);
            h.BattleEquipment.AddEquipmentToSlotWithoutAgent(slot, best);
            return true;
        }

        /// <summary>Zakup na targu miasta: 1 = najlepsza za nadwyzke rodu, 2 = najtansza, na ktora ma zloto (kiesa + glowa rodu), 0 = nic. Zloto -> kasa miasta.</summary>
        private static int Buy(Hero h, Hero lead, Town town, EquipmentIndex slot, ref long room)
        {
            var st = town.Settlement;
            if (st == null || st.ItemRoster == null) return 0;
            var r = st.ItemRoster;
            long gold = (long)Math.Max(0, h.Gold) + (lead != null ? Math.Max(0, lead.Gold) : 0);
            if (gold <= 0) return 0;
            MobileParty mp = h.PartyBelongedTo;
            EquipmentElement best = EquipmentElement.Invalid, cheap = EquipmentElement.Invalid;
            int bestP = 0, bestV = -1, cheapP = int.MaxValue;
            for (int i = 0; i < r.Count; i++)
            {
                var e = r.GetElementCopyAtIndex(i);
                if (e.Amount <= 0) continue;
                var el = e.EquipmentElement;
                if (el.Item == null || !Equipment.IsItemFitsToSlot(slot, el.Item)) continue;
                if (!Fits(h, el, slot)) continue;
                int p = town.GetItemPrice(el, mp, false);
                if (p <= 0 || p > gold) continue;
                if (p <= room && el.ItemValue > bestV) { best = el; bestP = p; bestV = el.ItemValue; }
                if (p < cheapP) { cheap = el; cheapP = p; }
            }
            int how = 0, price = 0; EquipmentElement pick = EquipmentElement.Invalid;
            if (bestV >= 0) { how = 1; pick = best; price = bestP; }
            else if (cheapP != int.MaxValue) { how = 2; pick = cheap; price = cheapP; }
            if (how == 0) return 0;
            int own = Math.Min(Math.Max(0, h.Gold), price), rest = price - own;
            if (own > 0) GiveGoldAction.ApplyForCharacterToSettlement(h, st, own, true);
            if (rest > 0) GiveGoldAction.ApplyForCharacterToSettlement(lead, st, rest, true);
            r.AddToCounts(pick, -1);
            h.BattleEquipment.AddEquipmentToSlotWithoutAgent(slot, pick);
            room = Math.Max(0, room - price); Spend(h.Clan, price); _refitGold += price;
            return how;
        }

        private static string Names(List<Piece> list)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < list.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                string nm;
                try { nm = list[i].El.GetModifiedItemName().ToString(); } catch { nm = list[i].El.Item.Name.ToString(); }
                sb.Append(nm);
            }
            return sb.ToString();
        }

        // ------------------------------------------------------------ okno gracza

        /// <summary>Wykup, na ktory stac: najdrozsze sztuki, ktore mieszcza sie w kiesie przegranego i gracza.</summary>
        private static int Affordable(Hero loser, List<Piece> stake, List<Piece> keep, List<Piece> give)
        {
            long money = (long)Math.Max(0, loser.Gold) + (loser != Hero.MainHero ? Math.Max(0, Hero.MainHero.Gold) : 0);
            int sum = 0;
            foreach (var pc in stake)
            {
                if (pc.Worth <= money - sum) { sum += pc.Worth; if (keep != null) keep.Add(pc); }
                else if (give != null) give.Add(pc);
            }
            return sum;
        }

        /// <summary>Okno: "Pay N" / "Yield..." (Esc = "Yield..." = tylko drugie okno). Drugie okno: "Yield" / "Back" (Esc = "Back"). Kazde nastepne okno
        /// dopiero w kolejnej klatce mapy (AppTick) - nic nie otwiera sie z wnetrza zamykanego okna.</summary>
        private static void ShowNext()
        {
            while (_offers.Count > 0)
            {
                var o = _offers[0];
                try
                {
                    if (o.Loser == null || o.Victor == null || !o.Loser.IsAlive || !o.Victor.IsAlive) { _offers.RemoveAt(0); _gone++; continue; }
                    var stake = Stake(o.Loser, false);
                    if (stake.Count == 0) { _offers.RemoveAt(0); _noKit++; continue; }
                    int total = 0; foreach (var pc in stake) total += pc.Worth;
                    var keep = new List<Piece>(); var give = new List<Piece>();
                    int can = Affordable(o.Loser, stake, keep, give);
                    bool me = o.Loser == Hero.MainHero;
                    var offer = o;
                    var sb = new StringBuilder();
                    InquiryData data;
                    if (o.Confirm)
                    {
                        sb.Append("Yield ").Append(me ? "your" : o.Loser.Name + "'s").Append(" horse and armour to ").Append(o.Victor.Name)
                          .Append(" - all ").Append(stake.Count).Append(" pieces, worth ").Append(total).Append(" gold? This cannot be undone.");
                        if (can > 0) sb.Append("\n\nYou could keep ").Append(keep.Count).Append(" of them for ").Append(can).Append(" gold.");
                        data = new InquiryData("Ransom of Arms", sb.ToString(), true, true, "Yield", "Back",
                            () => Answer(offer, false), () => Back(offer));
                    }
                    else
                    {
                        sb.Append(o.Victor.Name).Append(" has bested ").Append(me ? "you" : o.Loser.Name.ToString())
                          .Append(" in the lists").Append(o.Town != null ? " at " + o.Town.Name : "").Append(". By the custom of the tourney the horse and armour of the vanquished are forfeit to the victor:\n\n");
                        foreach (var pc in stake)
                        {
                            string nm; try { nm = pc.El.GetModifiedItemName().ToString(); } catch { nm = pc.El.Item.Name.ToString(); }
                            sb.Append("  ").Append(nm).Append(" - ").Append(pc.Worth).Append(" gold\n");
                        }
                        sb.Append("\nAll together ").Append(total).Append(" gold in their present state").Append(ShareNote()).Append(". ");
                        if (can >= total) sb.Append("Pay to keep them, or yield them.");
                        else if (can > 0) sb.Append("You can pay only ").Append(can).Append(" gold: that keeps ").Append(keep.Count).Append(" of ").Append(stake.Count)
                                            .Append(" pieces, the rest goes to the victor.");
                        else sb.Append("You have not the gold to pay. You must yield them.");
                        _offersShown++;
                        data = new InquiryData("Ransom of Arms", sb.ToString(), can > 0, true, "Pay " + can, "Yield...",
                            () => Answer(offer, true), () => AskYield(offer));
                    }
                    _popupOpen = true;
                    InformationManager.ShowInquiry(data, true);
                    return;
                }
                catch (Exception e) { _offers.Remove(o); _popupOpen = false; Stumble("ShowNext", e); }
            }
        }

        /// <summary>"Yield..." albo Esc w pierwszym oknie - tylko drugie okno z potwierdzeniem (w nastepnej klatce).</summary>
        private static void AskYield(Offer o) { if (o != null) o.Confirm = true; _popupOpen = false; }

        /// <summary>"Back" albo Esc w drugim oknie - wraca pierwsze okno.</summary>
        private static void Back(Offer o) { if (o != null) o.Confirm = false; _popupOpen = false; }

        private static void Answer(Offer o, bool pay)
        {
            try
            {
                _offers.Remove(o);
                if (o.Loser == null || o.Victor == null || !o.Loser.IsAlive || !o.Victor.IsAlive) { _gone++; return; }
                var stake = Stake(o.Loser, false);
                var keep = new List<Piece>(); var give = new List<Piece>();
                if (pay) Affordable(o.Loser, stake, keep, give); else give.AddRange(stake);
                var recv = Receiver(o.Victor);
                int paid = 0;
                foreach (var pc in keep)
                {
                    int own = o.Loser != Hero.MainHero ? Math.Min(Math.Max(0, o.Loser.Gold), pc.Worth) : 0;
                    if (own > 0) GiveGoldAction.ApplyBetweenCharacters(o.Loser, recv, own, true);
                    if (pc.Worth - own > 0) GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, recv, pc.Worth - own, true);
                    paid += pc.Worth; _paidN++; _paid += pc.Worth;
                }
                var gone = new List<Piece>();
                foreach (var pc in give)
                    if (Yield(o.Loser, o.Victor, recv, pc, o.Town)) { gone.Add(pc); _yieldN++; _yieldWorth += pc.Worth; }
                _plPaid += paid; if (paid > 0) _plPaidN++; _plYieldN += gone.Count; _settled++;
                var who = o.Loser == Hero.MainHero ? "your" : o.Loser.Name + "'s";
                if (paid > 0) Log.Player("You pay " + o.Victor.Name + " " + paid + " gold for " + who + " horse and armour.");
                if (gone.Count > 0) Log.Player("You yield to " + o.Victor.Name + ": " + Names(gone) + ".", true);
                Log.Info(P + "okno gracza: " + o.Loser.Name + " pokonany przez " + o.Victor.Name + " - " + (pay ? "wykup" : "oddanie (potwierdzone)")
                         + ", zaplacone " + paid + " (" + keep.Count + " szt.), oddane " + gone.Count + " szt.");
            }
            catch (Exception e) { Stumble("Answer", e); }
            finally { _popupOpen = false; }
        }

        // ------------------------------------------------------------ zapis: bohaterowie bez zamiennika

        internal static List<string> BareOut()
        {
            var list = new List<string>(_bare.Count);
            foreach (var kv in _bare) list.Add(kv.Key + "|" + kv.Value.ToString(Inv));
            return list;
        }

        internal static void BareIn(List<string> list)
        {
            _bare.Clear();
            if (list == null) return;
            foreach (var s in list)
            {
                try
                {
                    if (string.IsNullOrEmpty(s)) continue;
                    int k = s.LastIndexOf('|'); int m;
                    if (k <= 0 || !int.TryParse(s.Substring(k + 1), NumberStyles.Integer, Inv, out m) || m == 0) continue;
                    _bare[s.Substring(0, k)] = m;
                }
                catch (Exception e) { Stumble("BareIn", e); }
            }
        }

        /// <summary>Doba: bohaterowie bez zamiennika probuja w miescie, w ktorym stoja (tabor - wszedzie). Zwraca, ilu nadal czeka.</summary>
        private static int RetryBare()
        {
            if (_bare.Count == 0) return 0;
            var ids = new List<string>(_bare.Keys);
            foreach (var id in ids)
            {
                try
                {
                    int mask; if (!_bare.TryGetValue(id, out mask)) continue;
                    var h = Hero.Find(id);
                    if (h == null || !h.IsAlive || PlayerSide(h) || !OwnKit(h)) { _bare.Remove(id); continue; }
                    if (!On || h.IsPrisoner) continue;   // jeniec nie robi zakupow - czeka
                    var st = h.CurrentSettlement;
                    Refit(h, st != null ? st.Town : null, mask, true);
                }
                catch (Exception e) { Stumble("RetryBare(" + id + ")", e); }
            }
            return _bare.Count;
        }

        /// <summary>Tlo: bohaterowie AI (lordowie i wedrowcy, walczacy, dorosli, spoza rodu gracza) bez zbroi tulowia albo bez konia - z jakiegokolwiek powodu.</summary>
        private static void CountBareWorld(out int noBody, out int noHorse)
        {
            noBody = noHorse = 0;
            try
            {
                int age = Campaign.Current.Models.AgeModel.HeroComesOfAge;
                foreach (var h in Hero.AllAliveHeroes)
                {
                    if (h == null || PlayerSide(h) || h.Age < age || !(h.IsLord || h.IsWanderer) || !OwnKit(h)) continue;
                    if (h.IsNoncombatant) continue;
                    var eq = h.BattleEquipment;
                    if (eq[EquipmentIndex.Body].IsEmpty) noBody++;
                    if (eq[EquipmentIndex.Horse].IsEmpty) noHorse++;
                }
            }
            catch (Exception e) { Stumble("CountBareWorld", e); }
        }

        // ------------------------------------------------------------ linia doby

        internal static void Daily()
        {
            try
            {
                int waiting = RetryBare();
                int noBody, noHorse; CountBareWorld(out noBody, out noHorse);
                long clanMax = 0; string clanMaxName = "-";
                foreach (var kv in _clanSpent) if (kv.Value > clanMax) { clanMax = kv.Value; clanMaxName = kv.Key != null ? kv.Key.StringId : "?"; }
                Log.Info(P + "dzien " + ((int)CampaignTime.Now.ToDays).ToString(Inv) + " | klucz Tourney Ransom " + (On ? "tak" : "nie")
                         + ", nadwyzka rodu: budzet Armoury " + (Chest() != null ? "tak" : "nie") + " (zapasowa rezerwa " + _fallbackRoom + " razy)"
                         + " | turnieje " + _tourneys + ", walki " + _bouts + " (turnieje gry " + _boutsSim + ", szranki w misji " + _boutsMis + "), odwolane bez wykupu " + _calledOff
                         + " | rozliczone " + _settled + ": wykupione sztuki " + _paidN + " za " + _paid + " zlota, oddane " + _yieldN + " (wartosc " + _yieldWorth
                         + "): zalozone przez zwyciezce " + _worn + ", do taboru " + _toRoster + ", na targ " + _toMarket + " (miasto zaplacilo " + _marketPaid + ")"
                         + " | najwiecej jeden rod " + clanMax + " (" + clanMaxName + ")"
                         + " | zamiennik: z taboru " + _refitBag + ", kupione najlepsze " + _refitBest + ", najtansze " + _refitCheap + " (za " + _refitGold + " zlota), bez zamiennika "
                         + _refitNone + ", uzupelnione pozniej " + _refitLater + ", czeka bohaterow " + waiting
                         + " | swiat AI: bez zbroi tulowia " + noBody + ", bez konia " + noHorse
                         + " | pominiete: zolnierz " + _commoner + ", nie rycerz (dama/notabl) " + _notKnight + ", rod gracza bez gracza w szrankach " + _unasked
                         + ", rezygnacja gracza " + _forfeit + ", ten sam rod " + _kin + ", bez sprzetu " + _noKit + ", nie zyje " + _gone + ", smok " + _beast
                         + " | gracz: okna " + _offersShown + " (zaplacil " + _plPaidN + " razy " + _plPaid + " zlota, oddal " + _plYieldN + " szt.), wygral " + _plWonN
                         + " razy " + _plWon + " zlota i " + _plWonPieces + " szt. | w kolejce " + _offers.Count + ", walki misji czekajace " + _missionBouts.Count
                         + " | potkniecia " + _stumblesDay + " (sesja " + _stumbles + ")");
            }
            catch (Exception e) { Stumble("Daily", e); }
            ClearDay();
        }
    }

    /// <summary>R3-04a: sluchacze wykupu (koniec turnieju, linia doby). W zapisie tylko lista bohaterow bez zamiennika (gt_ransomBare).</summary>
    public class TourneyRansomBehavior : CampaignBehaviorBase
    {
        public TourneyRansomBehavior() { TourneyRansom.Reset(); }

        public override void RegisterEvents()
        {
            CampaignEvents.TournamentFinished.AddNonSerializedListener(this, OnFinished);
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, TourneyRansom.Daily);
        }

        public override void SyncData(IDataStore dataStore)
        {
            try
            {
                var bare = dataStore.IsSaving ? TourneyRansom.BareOut() : new List<string>();
                dataStore.SyncData("gt_ransomBare", ref bare);
                if (dataStore.IsLoading) TourneyRansom.BareIn(bare);
            }
            catch (Exception e) { Log.Error("TourneyRansomBehavior.SyncData", e); }
        }

        private void OnFinished(CharacterObject winner, MBReadOnlyList<CharacterObject> participants, Town town, ItemObject prize)
        {
            TourneyRansom.OnFinished(winner, participants, town);
        }
    }
}
