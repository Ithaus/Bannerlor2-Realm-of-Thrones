using System;
using System.Collections.Generic;
using System.Globalization;
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
using TaleWorlds.MountAndBlade;

namespace GrandTourney
{
    /// <summary>
    /// R3-04a (Jeff 20.10: lista 35 pomyslow, pkt 6 "tak") - WYKUP KONIA I ZBROI POKONANEGO. Zrodlo: docs/audyt-2026-10-09/04-wojna-armie-lupy-okupy.md
    /// rozdz. 2 pkt 8 (Dunk i ser Uthor Underleaf) i N6. Klucz TourneyRansom (+ RansomAiKeepWageDays). Wylaczony = turnieje jak przed zmiana.
    ///
    /// PRZEGRANA WALKA: kazda przegrana walka w szrankach (gracz odpada raz - kazdy uczestnik oprocz zwyciezcy turnieju placi jeden raz).
    ///  - Turniej w misji (gracz walczy albo oglada; takze walki pominiete i "Leave" - gra konczy je przez TournamentMatch.End): postfiks na
    ///    TournamentMatch.End - kazdy uczestnik walki spoza Winners przegral. Zwyciezca nad nim: ten, kto go powalil (postfiks na
    ///    TournamentFightMissionController.OnAgentRemoved, sprawca - jezdziec, gdy uderzyl kon), jesli jest wsrod zwyciezcow walki po drugiej stronie;
    ///    inaczej najlepszy wynikiem zwyciezca walki z innej druzyny (walka pominieta, zwyciezca z punktow); inaczej najlepszy zwyciezca walki.
    ///    Rozliczenie dopiero po misji (OnApplicationTick: brak misji i stan mapy) - zadnych zmian ekwipunku i okien w arenie.
    ///  - Turniej gry bez gracza (TournamentManager.ResolveTournament - gra zna tylko zwyciezce): drabinka pojedynkow 16 -> 8 -> 4 -> 2 -> 1
    ///    z listy uczestnikow gry; zwyciezca gry wygrywa kazdy swoj pojedynek, pozostale rozstrzyga wynik symulacji gry
    ///    (TournamentModel.GetTournamentSimulationScore x (0.75 + 0.25 x los) - ten sam wzor co SimulateTournament). Rozliczenie od razu (doba, bez misji).
    /// KTO: tylko bohaterowie (lordowie, towarzysze, wedrowcy, gracz) - maja wlasny kon i zbroje. Zolnierz z drabinki walczy w sprzecie miasta lub oddzialu
    ///  i nie ma nic do postawienia: walka z nim nic nie rozlicza (w obie strony). Ten sam rod - nic (krewni i towarzysze gracza nie wykupuja sie nawzajem).
    /// STAWKA: kon i zbroja z ekwipunku bojowego przegranego (glowa, tulow, nogi, rece, plaszcz, kon, ladry), kazda sztuka za wartosc w obecnym stanie =
    ///  EquipmentElement.ItemValue (cena przedmiotu x mnoznik stanu - zuzycie Armoury i lup Spoils of War obnizaja; ceny historyczne Armoury, gdy sa).
    ///  Smok (ROT-Dragon, test jak Armoury DragonFeed.IsDragon) nie jest koniem turniejowym - nie przechodzi, jego ladry tez nie.
    /// WYKUP: sztuka po sztuce od najdrozszej. Placi przegrany z wlasnej kiesy, potem glowa jego rodu (rod wykupuje swojego). AI wydaje tylko ponad
    ///  RansomAiKeepWageDays dni zoldu partii, ktora prowadzi (zold zostaje dla ludzi); sztuka, na ktora nie stac - oddana. Gracz (i towarzysze rodu gracza):
    ///  okno po turnieju - "Pay N" (najdrozsze sztuki, na ktore starcza zlota, reszta oddana) albo "Yield" (wszystko oddane); bez progu.
    ///  Zloto: GiveGoldAction.ApplyBetweenCharacters(platnik, odbiorca) - prawdziwy przelew; odbiorca = glowa rodu zwyciezcy (jak cena nagrody w grze,
    ///  GivePrizeToWinner), bez rodu - sam zwyciezca.
    /// ODDANE SZTUKI: schodza z ekwipunku przegranego (z tym samym stanem) do taboru zwyciezcy: rod gracza - juki gracza (albo jego partii), AI - partia
    ///  zwyciezcy, inaczej partia glowy rodu (lord sprzeda je w miescie jak kazdy lup); zwyciezca bez partii - targ miasta turnieju, miasto placi mu
    ///  wartosc z nadwyzki ponad rezerwe (TownWageLink.Spare), najwyzej tyle, ile ma. Nic nie powstaje z niczego i nic nie znika.
    /// Stan: tylko pamiec (walki misji czekajace na rozliczenie, kolejka okien gracza, liczniki doby). Nic w zapisie - wczytanie czysci kolejke
    ///  (sprzet zostaje u przegranego). Wyjatek - licznik potkniec per obiekt, nic nie gasimy. Linie "Wykup (R3-04a):".
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

        private sealed class Bout { public CharacterObject Loser, Victor; public Town Town; public bool Mission; }

        private struct Piece { public EquipmentIndex Slot; public EquipmentElement El; public int Worth; }

        private sealed class Offer { public Hero Loser, Victor; public Town Town; }

        // misja: kto kogo powalil w biezacej walce; walki czekajace na rozliczenie po misji
        private static readonly Dictionary<CharacterObject, CharacterObject> _ko = new Dictionary<CharacterObject, CharacterObject>();
        private static readonly List<Bout> _missionBouts = new List<Bout>();
        private static readonly List<Offer> _offers = new List<Offer>();
        private static bool _popupOpen;

        // liczniki doby
        private static int _bouts, _boutsSim, _boutsMis, _tourneys, _commoner, _kin, _noKit, _gone, _beast, _settled;
        private static int _paidN, _yieldN, _toRoster, _toMarket, _offersShown, _plPaidN, _plYieldN, _plWonN, _plWonPieces;
        private static long _paid, _yieldWorth, _marketPaid, _plPaid, _plWon;
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
            _ko.Clear(); _missionBouts.Clear(); _offers.Clear(); _popupOpen = false; _stumbles = 0;
            ClearDay();
        }

        private static void ClearDay()
        {
            _bouts = _boutsSim = _boutsMis = _tourneys = _commoner = _kin = _noKit = _gone = _beast = _settled = 0;
            _paidN = _yieldN = _toRoster = _toMarket = _offersShown = _plPaidN = _plYieldN = _plWonN = _plWonPieces = 0;
            _paid = _yieldWorth = _marketPaid = _plPaid = _plWon = 0;
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
                Log.Info(P + "latki " + n + "/3 (TournamentMatch.End, TournamentMatch.Start, TournamentFightMissionController.OnAgentRemoved"
                         + (rem == null ? " - BRAK: zwyciezca nad przegranym z punktow walki" : "") + ").");
            }
            catch (Exception e) { Stumble("ApplyAll", e); }
        }

        /// <summary>Nowa walka - kto kogo powalil liczymy od zera.</summary>
        public static void StartPostfix() { try { _ko.Clear(); } catch { } }

        /// <summary>Kto powalil przegranego (tylko bohaterowie jako przegrani - zolnierz nie ma czego oddac).</summary>
        public static void KoPostfix(Agent affectedAgent, Agent affectorAgent)
        {
            try
            {
                if (!On || affectedAgent == null || affectorAgent == null || !affectedAgent.IsHuman) return;
                var a = affectorAgent.IsMount && affectorAgent.RiderAgent != null ? affectorAgent.RiderAgent : affectorAgent;
                if (a == affectedAgent) return;
                var lc = affectedAgent.Character as CharacterObject;
                var vc = a.Character as CharacterObject;
                if (lc == null || vc == null || !lc.IsHero) return;
                _ko[lc] = vc;
            }
            catch (Exception e) { Stumble("Ko", e); }
        }

        /// <summary>Koniec walki w misji: przegrani (spoza Winners) z ich zwyciezcami czekaja na rozliczenie po misji.</summary>
        public static void EndPostfix(TournamentMatch __instance)
        {
            try
            {
                if (!On || __instance == null) return;
                var winners = new List<TournamentParticipant>(__instance.Winners);
                if (winners.Count == 0) return;
                Town town = null;
                try { var st = Settlement.CurrentSettlement; town = st != null ? st.Town : null; } catch { }
                foreach (var p in __instance.Participants)
                {
                    if (p == null || p.Character == null || winners.Contains(p)) continue;
                    var v = VictorOf(p, winners);
                    if (v == null || v.Character == null) continue;
                    _missionBouts.Add(new Bout { Loser = p.Character, Victor = v.Character, Town = town, Mission = true });
                }
                _ko.Clear();
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

        /// <summary>Koniec turnieju (CampaignEvents.TournamentFinished). W misji - walki juz zebrane, rozliczenie po misji; bez misji - drabinka.</summary>
        internal static void OnFinished(CharacterObject winner, MBReadOnlyList<CharacterObject> participants, Town town)
        {
            try
            {
                if (!On || town == null) return;
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
                SettleAll(bouts, town);
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
                    Town town = null;
                    foreach (var b in list) if (b.Town != null) { town = b.Town; break; }
                    SettleAll(list, town);
                }
                if (_offers.Count > 0 && !_popupOpen) ShowNext();
            }
            catch (Exception e) { _missionBouts.Clear(); Stumble("AppTick", e); }
        }

        // ------------------------------------------------------------ rozliczenie

        private static void SettleAll(List<Bout> bouts, Town town)
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
                    if (Settle(b, town ?? b.Town, out pc, out yc, out g)) { n++; pieces += pc; yielded += yc; gold += g; }
                }
                catch (Exception e) { Stumble("Settle(" + (b.Loser != null ? b.Loser.StringId : "?") + ")", e); }
            }
            Log.Info(P + "turniej w " + (town != null ? town.Name.ToString() : "?") + " (" + (sim ? "turniej gry - drabinka pojedynkow" : "szranki w misji")
                     + "): walk " + bouts.Count + ", z wykupem " + n + " | wykupione sztuki " + pieces + " za " + gold + " zlota, oddane " + yielded
                     + (_offers.Count > 0 ? " | okna gracza w kolejce " + _offers.Count : "") + ".");
        }

        /// <summary>Jedna walka. true = byla stawka (rozliczona albo okno gracza w kolejce).</summary>
        private static bool Settle(Bout b, Town town, out int paidPieces, out int yieldPieces, out long gold)
        {
            paidPieces = yieldPieces = 0; gold = 0;
            if (b.Loser == null || b.Victor == null || b.Loser == b.Victor) return false;
            if (!b.Loser.IsHero || !b.Victor.IsHero) { _commoner++; return false; }
            var L = b.Loser.HeroObject; var V = b.Victor.HeroObject;
            if (L == null || V == null || !L.IsAlive || !V.IsAlive) { _gone++; return false; }
            if (L.Clan != null && L.Clan == V.Clan) { _kin++; return false; }
            var stake = Stake(L, true);
            if (stake.Count == 0) { _noKit++; return false; }
            if (L.Clan == Clan.PlayerClan || L == Hero.MainHero)
            {
                _offers.Add(new Offer { Loser = L, Victor = V, Town = town });
                return true;
            }
            // AI: wykup od najdrozszej, na ile starcza ponad zold; reszta oddana
            var lead = L.Clan != null ? L.Clan.Leader : null;
            if (lead == L || lead == Hero.MainHero || lead == null || !lead.IsAlive) lead = null;
            int own = Spare(L), house = lead != null ? Spare(lead) : 0;
            var recv = Receiver(V);
            bool toPlayer = V.Clan == Clan.PlayerClan || V == Hero.MainHero;
            var given = new List<Piece>();
            foreach (var pc in stake)
            {
                if (pc.Worth <= own + house)
                {
                    int a = Math.Min(own, pc.Worth), c = pc.Worth - a;
                    if (a > 0) { GiveGoldAction.ApplyBetweenCharacters(L, recv, a, true); own -= a; }
                    if (c > 0) { GiveGoldAction.ApplyBetweenCharacters(lead, recv, c, true); house -= c; }
                    paidPieces++; gold += pc.Worth; _paidN++; _paid += pc.Worth;
                    if (toPlayer) { _plWon += pc.Worth; }
                }
                else given.Add(pc);
            }
            var moved = new List<Piece>();
            foreach (var pc in given)
            {
                if (Yield(L, V, recv, pc, town)) { moved.Add(pc); yieldPieces++; _yieldN++; _yieldWorth += pc.Worth; if (toPlayer) _plWonPieces++; }
            }
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

        /// <summary>Ile bohater moze dac na wykup: gracz i rod gracza - cala kiesa (decyduje gracz); AI - ponad RansomAiKeepWageDays dni zoldu partii, ktora prowadzi.</summary>
        private static int Spare(Hero h)
        {
            if (h == null || !h.IsAlive) return 0;
            int gold = Math.Max(0, h.Gold);
            if (h == Hero.MainHero || h.Clan == Clan.PlayerClan) return gold;
            long keep = 0;
            try
            {
                var mp = h.PartyBelongedTo;
                if (mp != null && mp.LeaderHero == h) keep = (long)Math.Max(0, mp.TotalWage) * Math.Max(0, Settings.Current.RansomAiKeepWageDays);
            }
            catch (Exception e) { Stumble("Spare(" + h.StringId + ")", e); }
            return (int)Math.Max(0, gold - keep);
        }

        /// <summary>Odbiorca zlota: glowa rodu zwyciezcy (jak cena nagrody w grze), bez rodu - sam zwyciezca.</summary>
        private static Hero Receiver(Hero v)
        {
            var c = v.Clan;
            if (c != null && c.Leader != null && c.Leader.IsAlive) return c.Leader;
            return v;
        }

        /// <summary>Tabor zwyciezcy: rod gracza - jego partia albo juki gracza; AI - jego partia, inaczej partia glowy rodu; null = brak (targ miasta).</summary>
        private static ItemRoster RosterOf(Hero v)
        {
            if (v.Clan == Clan.PlayerClan || v == Hero.MainHero)
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

        /// <summary>Sztuka schodzi z ekwipunku przegranego do taboru zwyciezcy (albo na targ miasta, ktore placi zwyciezcy jej wartosc z nadwyzki).</summary>
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
                if (ro != null) { ro.AddToCounts(cur, 1); _toRoster++; }
                else
                {
                    st.ItemRoster.AddToCounts(cur, 1); _toMarket++;
                    int pay = Math.Min(pc.Worth, TownWageLink.Spare(st));
                    if (pay > 0) { GiveGoldAction.ApplyForSettlementToCharacter(st, recv, pay, true); _marketPaid += pay; }
                }
                return true;
            }
            catch (Exception e) { Stumble("Yield(" + (loser != null ? loser.StringId : "?") + ")", e); return false; }
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
            int money = Spare(loser) + (loser != Hero.MainHero ? Spare(Hero.MainHero) : 0), sum = 0;
            foreach (var pc in stake)
            {
                if (pc.Worth <= money - sum) { sum += pc.Worth; if (keep != null) keep.Add(pc); }
                else if (give != null) give.Add(pc);
            }
            return sum;
        }

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
                    var sb = new StringBuilder();
                    sb.Append(o.Victor.Name).Append(" has bested ").Append(me ? "you" : o.Loser.Name.ToString())
                      .Append(" in the lists").Append(o.Town != null ? " at " + o.Town.Name : "").Append(". By the custom of the tourney the horse and armour of the vanquished are forfeit to the victor:\n\n");
                    foreach (var pc in stake)
                    {
                        string nm; try { nm = pc.El.GetModifiedItemName().ToString(); } catch { nm = pc.El.Item.Name.ToString(); }
                        sb.Append("  ").Append(nm).Append(" - ").Append(pc.Worth).Append(" gold\n");
                    }
                    sb.Append("\nAll together ").Append(total).Append(" gold in their present state. ");
                    if (can >= total) sb.Append("Pay to keep them, or yield them.");
                    else if (can > 0) sb.Append("You can pay only ").Append(can).Append(" gold: that keeps ").Append(keep.Count).Append(" of ").Append(stake.Count)
                                        .Append(" pieces, the rest goes to the victor.");
                    else sb.Append("You have not the gold to pay. You must yield them.");
                    var offer = o;
                    _popupOpen = true;
                    _offersShown++;
                    InformationManager.ShowInquiry(new InquiryData("Ransom of Arms", sb.ToString(), can > 0, true,
                        "Pay " + can, "Yield", () => Answer(offer, true), () => Answer(offer, false)), true);
                    return;
                }
                catch (Exception e) { _offers.Remove(o); _popupOpen = false; Stumble("ShowNext", e); }
            }
        }

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
                Log.Info(P + "okno gracza: " + o.Loser.Name + " pokonany przez " + o.Victor.Name + " - " + (pay ? "wykup" : "oddanie")
                         + ", zaplacone " + paid + " (" + keep.Count + " szt.), oddane " + gone.Count + " szt.");
            }
            catch (Exception e) { Stumble("Answer", e); }
            finally { _popupOpen = false; }
        }

        // ------------------------------------------------------------ linia doby

        internal static void Daily()
        {
            try
            {
                var s = Settings.Current;
                Log.Info(P + "dzien " + ((int)CampaignTime.Now.ToDays).ToString(Inv) + " | klucz Tourney Ransom " + (On ? "tak" : "nie")
                         + ", zold zostawiany AI " + (s != null ? s.RansomAiKeepWageDays : 0) + " dni"
                         + " | turnieje " + _tourneys + ", walki " + _bouts + " (turnieje gry " + _boutsSim + ", szranki w misji " + _boutsMis + ")"
                         + " | rozliczone " + _settled + ": wykupione sztuki " + _paidN + " za " + _paid + " zlota, oddane " + _yieldN + " (wartosc " + _yieldWorth
                         + "): do taboru " + _toRoster + ", na targ " + _toMarket + " (miasto zaplacilo " + _marketPaid + ")"
                         + " | pominiete: zolnierz " + _commoner + ", ten sam rod " + _kin + ", bez sprzetu " + _noKit + ", nie zyje " + _gone + ", smok " + _beast
                         + " | gracz: okna " + _offersShown + " (zaplacil " + _plPaidN + " razy " + _plPaid + " zlota, oddal " + _plYieldN + " szt.), wygral " + _plWonN
                         + " razy " + _plWon + " zlota i " + _plWonPieces + " szt. | w kolejce " + _offers.Count + ", walki misji czekajace " + _missionBouts.Count
                         + " | potkniecia " + _stumblesDay + " (sesja " + _stumbles + ")");
            }
            catch (Exception e) { Stumble("Daily", e); }
            ClearDay();
        }
    }

    /// <summary>R3-04a: sluchacze wykupu (koniec turnieju, linia doby). Nic w zapisie.</summary>
    public class TourneyRansomBehavior : CampaignBehaviorBase
    {
        public TourneyRansomBehavior() { TourneyRansom.Reset(); }

        public override void RegisterEvents()
        {
            CampaignEvents.TournamentFinished.AddNonSerializedListener(this, OnFinished);
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, TourneyRansom.Daily);
        }

        public override void SyncData(IDataStore dataStore) { }

        private void OnFinished(CharacterObject winner, MBReadOnlyList<CharacterObject> participants, Town town, ItemObject prize)
        {
            TourneyRansom.OnFinished(winner, participants, town);
        }
    }
}
