using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// m102-tablica E2 (projekt pkt 2.2, 3, 4, 6.3) - LISTY GONCZNE KAPITANA STRAZY. Kapitana strazy nie ma w grze, ROT ani BK - bez nowych postaci: twarz z zalogi
    /// miasta (piechur z najwyzszym poziomem, na start EliteBasicTroop kultury pana - wzor gry "Talk to the garrison commander"), w Krolewskiej Przystani (town_EW4)
    /// "Gold Cloak Captain" z ROT. Stan w zapisie tekstowym arm_board (bez QuestBase i bez typow w zapisie - decyzja D6).
    /// REGULA (nic z niczego):
    ///  - Doba: jeden przebieg po bandach (MobileParty.AllBanditParties): bez Innych (Undead), bez band w osadach (kryjowki - to zadania soltysa z gry), bez band na morzu,
    ///    od 5 zdrowych ludzi, najblizsze miasto w promieniu MaxBandDist. W miescie najwyzej WatchBountiesPerTown listow; kolejnosc: najwieksza doplata za szkody, potem
    ///    najwiecej ludzi.
    ///  - Kwota = N x D x S x 1.25 x (1 + doplata): N = zdrowi ludzie bandy 10..60, D = 3 dni (4 przy wiekszosci konnych), S = pelny zold zalogi miasta na glowe
    ///    (bez zalogi 5), 1.25 jak QuestPay8; doplata +25% za kazda wies miasta zlupiona przez bande w 60 dniach, +15% za kazda rozbita karawane / woz wiejski okregu,
    ///    +50% za herszta-bohatera, razem najwyzej +100%. Kwota ustalona przy wywieszeniu.
    ///  - Wolna kasa = kasa miasta - max(zapas kupcow TownPurse.Reserve, DsTowns.TdKeep: podloga firm i najblizsze raty); list wolno wywiesic, gdy kwota <= 1/4 wolnej kasy
    ///    pomniejszonej o kwoty czynnych listow miasta, albo (WatchBountyLordTopUp) gdy pan miasta (AI) ma ponad rezerwe AiGearGoldReserve co najmniej kwote.
    ///    Rada z dlugiem TownDebt nie wywiesza. Oblezone miasto nie wywiesza nowych (stare stoja).
    ///  - Dowod: banda po stronie przegranej bitwy z mniej niz 25% ludzi z chwili listu (albo zniszczona) - MapEventEnded (przed zniszczeniem partii w HandleMapEventEnd).
    ///    Placi sie temu, kto rozbil: gracz (po stronie zwyciezcy), lord AI prowadzacy strone zwyciezcy (WatchBountyAiLords; nie wrog miasta). Straz, milicja, patrol,
    ///    karawana, bandyci, wrog miasta - list zdjety bez wyplaty. Wyplata od razu: kasa miasta min(kwota, wolna kasa) (GiveGoldAction.ApplyForSettlementToCharacter),
    ///    reszta od pana miasta z jego zlota ponad rezerwe (ApplyBetweenCharacters, nie gdy pan to gracz albo sam odbiorca), brak - niedoplata z komunikatem.
    ///    Bezpieczenstwo miasta +2, slawa +1. Lup bandy i tak idzie do zwyciezcy (nasze zasady) - nagroda jest osobno.
    ///  - Zdjecie: banda znikla / nieaktywna, termin (WatchBountyDays), zmiana pana miasta.
    /// Linie "Listy goncze (m102)": wpiete, list, wyplata, doba. Watek glowny (doba, zdarzenia); wyjatek per list / banda - licznik potkniec (CLAUDE.md 7).
    /// </summary>
    internal static partial class NoticeBoard
    {
        internal const string PW = "Listy goncze (m102): ";
        private const float Premium = 1.25f;       // jak QuestPay8 (narzut za ryzyko i rannych)
        private const float FallbackRate = 5f;     // zold na glowe bez zalogi
        private const float MaxBandDist = 45f;     // jedn. mapy (ok. 214 km) - dalej banda nie jest "z okolicy" zadnego miasta
        private const int HarmDays = 60;
        private const float BrokenShare = 0.25f;   // banda z mniej niz 25% ludzi z chwili listu - rozbita

        private sealed class Notice
        {
            public string Town, Band, Name, Owner;     // StringId miasta, bandy, nazwa bandy, StringId rodu pana przy wywieszeniu
            public int Men, Days, BonusPct, Sum, Posted, Due, Kind;   // Kind 0 - kapitan strazy, 1 - soltys (E2b)
            public float S;
            public bool Lord, Taken;
            public string Giver = "";                  // E2b: soltys (StringId), wies w Village
            public string Village = "";
            public Settlement St;                      // sesja
            public MobileParty Mp;
        }

        private struct Harm { public int Day; public byte Kind; public string Town, Vil; }   // Kind 0 - wies zlupiona (Vil - ta wies, E2b), 1 - karawana / woz wiejski rozbity

        private static readonly List<Notice> _notices = new List<Notice>();
        private static readonly Dictionary<string, List<Harm>> _harm = new Dictionary<string, List<Harm>>(StringComparer.Ordinal);
        private static int _wEvents, _wDialog;

        // rozmowa z kapitanem
        private static bool _captainTalk;
        private static CharacterObject _captainChar;
        private static Notice _talkNotice;

        // liczniki doby
        private static int _wNew, _wPaidN, _wPaidPlayerN, _wPaidLordN, _wDropGuard, _wDropEnemy, _wDropGone, _wDropDue, _wDropOwner, _wBelowKeep, _wFromNothing, _wTakenToday;
        private static long _wPaid, _wPaidPlayer, _wPaidLord, _wFromTown, _wFromLordPurse, _wShort;
        private static int _wStumbles;

        internal static bool WatchOn { get { var s = Settings.Current; return s != null && s.WatchBounties; } }

        private static void ResetWatch()
        {
            _notices.Clear(); _harm.Clear();
            _captainTalk = false; _captainChar = null; _talkNotice = null;
            ZeroWatchDay();
        }

        private static void ZeroWatchDay()
        {
            _wNew = _wPaidN = _wPaidPlayerN = _wPaidLordN = _wDropGuard = _wDropEnemy = _wDropGone = _wDropDue = _wDropOwner = _wBelowKeep = _wFromNothing = _wTakenToday = 0;
            _wPaid = _wPaidPlayer = _wPaidLord = _wFromTown = _wFromLordPurse = _wShort = 0;
            _wStumbles = 0;
            ZeroHeadmanDay();
        }

        private static void WStumble(string where, Exception e) { _wStumbles++; Stumble("Watch." + where, e); }

        /// <summary>ArmouryBehavior.RegisterEvents: ile nasluchow listow wpietych (do linii "wpiete").</summary>
        internal static void NoteEventsWired(int n) { _wEvents = n; }

        private static string WatchKeysText()
        {
            var s = Settings.Current;
            if (s == null) return "";
            return ", Watch Bounties " + YN(s.WatchBounties) + " (na miasto " + PerTown + ", dni " + DaysUp + "), AI Lords " + YN(s.WatchBountyAiLords) + ", Lord Top Up " + YN(s.WatchBountyLordTopUp)
                   + HeadmanKeysText();
        }

        /// <summary>Linia "Listy goncze (m102): wpiete" (po rejestracji menu i rozmow).</summary>
        private static void WatchStartLine()
        {
            Log.Info(PW + "wpiete - zdarzenia " + _wEvents + "/3 (MobilePartyDestroyed, MapEventEnded, RaidCompleted), dialog kapitana " + _wDialog + "/7, dialog soltysa " + _hDialog + "/8, opcja karty 1/1, zapis arm_board " + Ver
                     + "; klucz Watch Bounties " + YN(WatchOn) + ", na miasto " + PerTown + ", dni " + DaysUp + ", promien " + MaxBandDist.ToString("0", Inv) + " jedn.");
        }

        private static int PerTown { get { var s = Settings.Current; return s != null ? Math.Max(0, Math.Min(5, s.WatchBountiesPerTown)) : 2; } }
        private static int DaysUp { get { var s = Settings.Current; return s != null ? Math.Max(1, Math.Min(60, s.WatchBountyDays)) : 10; } }
        private static int LordReserve { get { var s = Settings.Current; return s != null ? Math.Max(0, s.AiGearGoldReserve) : 2000; } }

        // ------------------------------------------------------------ nazwy strazy

        private static readonly HashSet<string> Westeros = new HashSet<string>(StringComparer.Ordinal)
        { "battania", "vlandia", "reach", "river", "vale", "stormlands", "aserai", "sturgia", "crownlands", "dragonstone", "skagosi", "nightswatch", "freefolk", "wildlings" };

        /// <summary>"the watch" (Westeros), "the city guard" (Essos), "the Gold Cloaks" (Krolewska Przystan).</summary>
        private static string GuardName(Settlement st)
        {
            if (st == null) return "the watch";
            if (st.StringId == "town_EW4") return "the Gold Cloaks";
            string c = st.Culture != null ? st.Culture.StringId : "";
            return Westeros.Contains(c) ? "the watch" : "the city guard";
        }

        private static string CaptainTitle(Settlement st)
        {
            if (st != null && st.StringId == "town_EW4") return "the captain of the Gold Cloaks";
            return "the captain of " + GuardName(st);
        }

        /// <summary>Twarz kapitana: town_EW4 - goldcloak_captain (ROT); inaczej piechur z najwyzszym poziomem z zalogi, na start EliteBasicTroop kultury pana (jak gra).</summary>
        private static CharacterObject CaptainOf(Settlement st)
        {
            if (st == null || st.Town == null) return null;
            try
            {
                if (st.StringId == "town_EW4")
                {
                    var gc = MBObjectManager.Instance.GetObject<CharacterObject>("goldcloak_captain");
                    if (gc != null) return gc;
                }
                CharacterObject best = null;
                try { if (st.OwnerClan != null && st.OwnerClan.Culture != null) best = st.OwnerClan.Culture.EliteBasicTroop; } catch { best = null; }
                var g = st.Town.GarrisonParty;
                if (g != null && g.MemberRoster != null)
                    foreach (var el in g.MemberRoster.GetTroopRoster())
                    {
                        var c = el.Character;
                        if (c == null || c.IsHero || !c.IsInfantry || el.Number <= 0) continue;
                        if (best == null || c.Level > best.Level) best = c;
                    }
                return best;
            }
            catch (Exception e) { WStumble("CaptainOf", e); return null; }
        }

        // ------------------------------------------------------------ odczyty dla tablicy (E1)

        private static Notice NoticeOf(Entry e) { return e != null ? e.Tag as Notice : null; }

        private static bool Live(Notice n)
        {
            if (n == null || !_notices.Contains(n)) return false;
            if (n.Mp == null) n.Mp = FindParty(n.Band);
            if (n.Mp != null && string.IsNullOrEmpty(n.Name)) n.Name = BandNameOf(n.Mp);
            return n.Mp != null && n.Mp.IsActive;
        }

        /// <summary>Nazwa bandy (plotki: "Jon Rivers and his bandits"; bez kluczy plotek - nazwa gry). Nie w zapisie - liczona po wczytaniu (deterministyczna z id bandy).</summary>
        private static string BandNameOf(MobileParty mp)
        {
            try { return Safe(Rumours.BandName(mp) ?? (mp.Name != null ? mp.Name.ToString() : "a band")); } catch { return "a band"; }
        }

        private static bool AnyWatchFree(Settlement st)
        {
            if (!WatchOn || st == null) return false;
            foreach (var n in _notices) if (n.Kind == 0 && n.Town == st.StringId && !n.Taken) return true;
            return false;
        }

        private static void WatchInto(Settlement st, List<Entry> list, bool probe)
        {
            if (st == null || _notices.Count == 0) return;
            foreach (var n in _notices)
            {
                try
                {
                    if (n.Town != st.StringId || !Live(n)) continue;
                    if (n.Kind == 0 && !WatchOn) continue;
                    var e = new Entry { K = n.Kind == 0 ? Kind.Watch : Kind.Headman, Tag = n, Reward = n.Sum, Group = n.Kind == 0 ? 1 : 2 };
                    if (n.Kind == 1) { e.Village = Settlement.Find(n.Village); e.Hero = Hero.Find(n.Giver); }
                    string near = n.Mp != null ? NearName(n.Mp.GetPosition2D) : null;
                    string who = n.Kind == 0 ? Cap(CaptainTitle(st)) : NameOf(e.Hero) + ", headman of " + (e.Village != null ? e.Village.Name.ToString() : "a village");
                    e.Where = near != null ? "near " + near : "";
                    e.Line = who + " - WANTED: " + n.Name + " (" + n.Men + ") - " + n.Sum.ToString("N0", Inv) + " - " + DayLeft(n) + (e.Where.Length > 0 ? " - " + e.Where : "")
                             + (n.Taken ? " - you took it up" : "");
                    list.Add(e);
                }
                catch (Exception ex) { WStumble("WatchInto", ex); }
            }
        }

        private static string Cap(string s) { return string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1); }

        private static string DayLeft(Notice n)
        {
            int d = n.Due - Today;
            return d <= 0 ? "last day" : d + (d == 1 ? " day" : " days");
        }

        private static string NearName(Vec2 p)
        {
            var s = NearestPlace(p);
            return s != null ? s.Name.ToString() : null;
        }

        private static bool WatchValid(Entry e) { return Live(NoticeOf(e)); }

        private static ITrackableCampaignObject WatchTarget(Entry e) { var n = NoticeOf(e); return n != null && Live(n) ? n.Mp : null; }

        private static string BandLabel(MobileParty mp)
        {
            foreach (var n in _notices) if (n.Mp == mp && !string.IsNullOrEmpty(n.Name)) return n.Name;
            return mp != null && mp.Name != null ? mp.Name.ToString() : "the band";
        }

        private static bool WatchAny() { return _notices.Count > 0 || _harm.Count > 0; }

        private static int HarmCount(string band, string town, byte kind)
        {
            List<Harm> l;
            if (band == null || !_harm.TryGetValue(band, out l)) return 0;
            int n = 0, from = Today - HarmDays;
            foreach (var h in l) if (h.Kind == kind && h.Day >= from && h.Town == town) n++;
            return n;
        }

        private static string HarmText(Notice n)
        {
            int v = HarmCount(n.Band, n.Town, 0), c = HarmCount(n.Band, n.Town, 1);
            var parts = new List<string>();
            if (v > 0) parts.Add("plundered " + (v == 1 ? "one of our villages" : Words(v).ToLowerInvariant() + " of our villages"));
            if (c > 0) parts.Add("robbed " + (c == 1 ? "one of our caravans" : Words(c).ToLowerInvariant() + " caravans and carts"));
            if (parts.Count == 0) return "";
            return " They have " + string.Join(" and ", parts.ToArray()) + " this season.";
        }

        private static string PayerText(Notice n, Settlement st)
        {
            var owner = st != null && st.OwnerClan != null ? st.OwnerClan.Leader : null;
            var s = Settings.Current;
            bool topUp = s != null && s.WatchBountyLordTopUp && owner != null && owner != Hero.MainHero;
            if (n.Lord && topUp) return "The council's coffers are thin - " + NameOf(owner) + " pays for this one from " + His(owner) + " own purse.";
            return "The town's coffers." + (topUp ? " If those run dry, " + NameOf(owner) + " has promised the rest." : "");
        }

        private static string WatchCard(Entry e, Settlement st)
        {
            var n = NoticeOf(e);
            if (n == null) return "This notice has been taken down.";
            if (n.Kind == 1) return HeadmanCard(n, st);
            var sb = new StringBuilder();
            sb.Append("WANTED: ").Append(n.Name.ToUpperInvariant()).Append('\n');
            sb.Append("Posted by ").Append(CaptainTitle(st)).Append(" of ").Append(st != null ? st.Name.ToString() : "the town").Append('.');
            string near = n.Mp != null ? NearName(n.Mp.GetPosition2D) : null;
            sb.Append("\n\n").Append(n.Men.ToString(Inv)).Append(" men")
              .Append(near != null ? ", last seen near " + near : "").Append(n.Mp != null ? Away(st, n.Mp.GetPosition2D) : "").Append('.').Append(HarmText(n));
            sb.Append("\n\nPay: ").Append(n.Sum.ToString("N0", Inv)).Append(" denars, paid at once to whoever breaks them - on top of whatever the band carries. ").Append(PayerText(n, st));
            sb.Append("\nTime: the notice stays up ").Append(Math.Max(0, n.Due - Today)).Append(" more days. It is public: if a lord breaks them first, he is paid.");
            if (n.Taken) sb.Append("\n\nYou have told the captain you will hunt them.");
            return sb.ToString();
        }

        // ------------------------------------------------------------ opcja karty i rozmowa z kapitanem

        private static void AddWatchOptions(CampaignGameStarter starter)
        {
            starter.AddGameMenuOption(MenuNote, "arm_note_captain", "{=!}{ARM_NOTE_CAPTAIN}", CaptainOptCond, CaptainOptCons);
        }

        /// <summary>Wpis listu na tablicy: wykrzyknik, gdy gracz jeszcze go nie przyjal.</summary>
        private static void WatchQuestFlag(MenuCallbackArgs args, Entry e)
        {
            var n = NoticeOf(e);
            if (n != null && !n.Taken) args.OptionQuestData |= GameMenuOption.IssueQuestFlags.AvailableIssue;
        }

        private static bool CaptainOptCond(MenuCallbackArgs args)
        {
            args.optionLeaveType = GameMenuOption.LeaveType.Conversation;
            var e = _sel;
            if (e == null || e.K != Kind.Watch || !Live(NoticeOf(e))) return false;
            var st = Settlement.CurrentSettlement;
            if (st == null || st.Town == null || NoticeOf(e).Town != st.StringId) return false;
            try
            {
                string label = "Speak to " + CaptainTitle(st);
                MBTextManager.SetTextVariable("ARM_NOTE_CAPTAIN", label);
                args.Text.SetTextVariable("ARM_NOTE_CAPTAIN", label);
                if (st.Town.GarrisonParty == null || CaptainOf(st) == null)
                {
                    args.IsEnabled = false;
                    args.Tooltip = new TextObject("{=!}The watch has no one to speak for it right now.");
                }
            }
            catch (Exception ex) { WStumble("CaptainOptCond", ex); return false; }
            return true;
        }

        private static void CaptainOptCons(MenuCallbackArgs args)
        {
            var e = _sel;
            var n = NoticeOf(e);
            var st = Settlement.CurrentSettlement;
            if (!Live(n) || st == null || st.Town == null) { Gone(); return; }
            try
            {
                var g = st.Town.GarrisonParty;
                var face = CaptainOf(st);
                if (g == null || face == null) return;
                _captainTalk = true; _captainChar = face; _talkNotice = n;
                Log.Info(PW + "rozmowa z kapitanem - " + st.StringId + " " + n.Band + " (twarz " + face.StringId + ")");
                // wzor gry: LordNeedsGarrisonTroopsIssueQuestBehavior.talk_to_garrison_commander_on_consequence (partia rozmowcy - zaloga miasta)
                CampaignMapConversation.OpenConversation(new ConversationCharacterData(CharacterObject.PlayerCharacter, PartyBase.MainParty),
                                                         new ConversationCharacterData(face, g.Party, true));
            }
            catch (Exception ex) { _captainTalk = false; _captainChar = null; _talkNotice = null; WStumble("CaptainOptCons", ex); }
        }

        private static int AddWatchDialogs(CampaignGameStarter starter)
        {
            int n = 0;
            try
            {
                // priorytet 310 - nad 300 rozmowy z dowodca zalogi z gry (na wypadek starego wyboru w tamtym zadaniu); warunek: nasza flaga rozmowy i ta sama postac
                starter.AddDialogLine("arm_watch_start", "start", "arm_watch_opts", "{=!}{ARM_WATCH_OPEN}", CaptainStartCond, null, 310); n++;
                starter.AddPlayerLine("arm_watch_take", "arm_watch_opts", "arm_watch_taken", "{=!}I'll hunt them down.", null, CaptainTakeCons, 100); n++;
                starter.AddDialogLine("arm_watch_taken_r", "arm_watch_taken", "close_window", "{=!}{ARM_WATCH_TAKEN}", null, null, 100); n++;
                starter.AddPlayerLine("arm_watch_who", "arm_watch_opts", "arm_watch_who_r", "{=!}Who pays, exactly?", null, null, 99); n++;
                starter.AddDialogLine("arm_watch_who_r", "arm_watch_who_r", "arm_watch_opts", "{=!}{ARM_WATCH_PAYER}", null, null, 100); n++;
                starter.AddPlayerLine("arm_watch_no", "arm_watch_opts", "arm_watch_no_r", "{=!}Not now.", null, null, 98); n++;
                starter.AddDialogLine("arm_watch_no_r", "arm_watch_no_r", "close_window", "{=!}The notice stays up. First to bring them down gets paid.", null, null, 100); n++;
            }
            catch (Exception e) { WStumble("AddWatchDialogs", e); }
            _wDialog = n;
            return n;
        }

        private static bool CaptainStartCond()
        {
            try
            {
                if (!_captainTalk || _captainChar == null || CharacterObject.OneToOneConversationCharacter != _captainChar || Hero.OneToOneConversationHero != null) return false;
                var n = _talkNotice;
                var st = Settlement.CurrentSettlement;
                if (!Live(n) || st == null) return false;
                string near = n.Mp != null ? NearName(n.Mp.GetPosition2D) : null;
                string open = "You've read the notice of " + GuardName(st) + "? " + n.Name + " - " + n.Men + " " + (n.Men >= 20 ? "cutthroats" : "of them") + " - have been working the roads"
                              + (near != null ? " by " + near : "") + "." + HarmText(n) + " The council pays " + n.Sum.ToString("N0", Inv) + " for them, coin on the table once they're broken. Whatever they carry is yours.";
                MBTextManager.SetTextVariable("ARM_WATCH_OPEN", open);
                MBTextManager.SetTextVariable("ARM_WATCH_TAKEN", (near != null ? "They were last seen near " + near + ". " : "") + "I've marked them for you. Good hunting.");
                string payer = PayerText(n, st);
                MBTextManager.SetTextVariable("ARM_WATCH_PAYER", payer.StartsWith("The town's coffers.", StringComparison.Ordinal) ? "The town's coffers." + payer.Substring("The town's coffers.".Length) : payer);
                return true;
            }
            catch (Exception e) { WStumble("CaptainStartCond", e); return false; }
        }

        private static void CaptainTakeCons()
        {
            try
            {
                var n = _talkNotice;
                if (!Live(n)) return;
                if (!n.Taken) { n.Taken = true; _wTakenToday++; }
                var vt = Campaign.Current != null ? Campaign.Current.VisualTrackerManager : null;
                if (vt != null && n.Mp != null && FlagOf(n.Mp) == null)
                {
                    vt.RegisterObject(n.Mp);
                    _flags.Add(new Flag { Kind = "p", Id = n.Band, Until = Math.Max(Today + 1, n.Due), Obj = n.Mp });
                    _dFlag++;
                }
                Log.Info(PW + "przyjety przez gracza - " + n.Town + " " + n.Band + " (" + n.Men + " ludzi) = " + n.Sum);
            }
            catch (Exception e) { WStumble("CaptainTakeCons", e); }
        }

        private static void WatchConversationEnded() { _captainTalk = false; _captainChar = null; _talkNotice = null; }

        // ------------------------------------------------------------ szkody band (zdarzenia)

        private static void AddHarm(MobileParty band, byte kind, Settlement town, Settlement village = null)
        {
            if (band == null || town == null || string.IsNullOrEmpty(band.StringId)) return;
            List<Harm> l;
            if (!_harm.TryGetValue(band.StringId, out l)) { l = new List<Harm>(); _harm[band.StringId] = l; }
            if (l.Count >= 12) l.RemoveAt(0);
            l.Add(new Harm { Day = Today, Kind = kind, Town = town.StringId, Vil = village != null ? village.StringId : "" });
        }

        /// <summary>CampaignEvents.RaidCompletedEvent: banda zlupila wies - szkoda dla miasta tej wsi.</summary>
        internal static void OnRaidCompleted(BattleSideEnum winner, RaidEventComponent raid)
        {
            try
            {
                if (raid == null || winner != BattleSideEnum.Attacker) return;
                var st = raid.MapEventSettlement;
                var lp = raid.AttackerSide != null ? raid.AttackerSide.LeaderParty : null;
                var mp = lp != null ? lp.MobileParty : null;
                if (st == null || st.Village == null || mp == null || !mp.IsBandit) return;
                AddHarm(mp, 0, st.Village.Bound, st);
            }
            catch (Exception e) { WStumble("OnRaidCompleted", e); }
        }

        // ------------------------------------------------------------ dowod i wyplata

        /// <summary>CampaignEvents.MapEventEnded (przed zniszczeniem partii): szkody band (karawana / woz wiejski rozbity) i wyplata za rozbita bande z listem.</summary>
        internal static void OnMapEventEnded(MapEvent me)
        {
            if (me == null) return;
            try
            {
                var win = me.WinningSide;
                if (win == BattleSideEnum.None) return;
                var winSide = win == BattleSideEnum.Attacker ? me.AttackerSide : me.DefenderSide;
                var lose = win == BattleSideEnum.Attacker ? me.DefenderSide : me.AttackerSide;
                if (winSide == null || lose == null) return;
                // szkody: bandyci wygrali z karawana albo wozem wiejskim
                bool loserTrade = false;
                foreach (var mep in lose.Parties) { var m = mep != null && mep.Party != null ? mep.Party.MobileParty : null; if (m != null && (m.IsCaravan || m.IsVillager)) { loserTrade = true; break; } }
                if (loserTrade)
                {
                    Settlement town = NearestTown(me.Position.ToVec2());
                    foreach (var mep in winSide.Parties) { var m = mep != null && mep.Party != null ? mep.Party.MobileParty : null; if (m != null && m.IsBandit) AddHarm(m, 1, town); }
                }
                if (_notices.Count == 0) return;
                foreach (var mep in lose.Parties)
                {
                    var m = mep != null && mep.Party != null ? mep.Party.MobileParty : null;
                    if (m == null || !m.IsBandit) continue;
                    for (int i = _notices.Count - 1; i >= 0; i--)
                    {
                        var n = _notices[i];
                        if (n.Band != m.StringId) continue;
                        try { Settle(n, m, me, winSide, win); }
                        catch (Exception e) { WStumble("Settle", e); }
                    }
                }
            }
            catch (Exception e) { WStumble("OnMapEventEnded", e); }
        }

        private static void Settle(Notice n, MobileParty band, MapEvent me, MapEventSide winSide, BattleSideEnum win)
        {
            if (string.IsNullOrEmpty(n.Name)) n.Name = BandNameOf(band);
            int left = band.Party != null ? band.Party.NumberOfHealthyMembers : 0;
            if (band.IsActive && left >= Math.Max(1, (int)Math.Ceiling(n.Men * BrokenShare))) return;   // uszla z sila - list stoi
            var st = n.St ?? Settlement.Find(n.Town);
            _notices.Remove(n);
            if (st == null || st.Town == null) { _wDropGone++; return; }
            Hero payee = null;
            string who;
            var s = Settings.Current;
            if (me.IsPlayerMapEvent && me.PlayerSide == win) { payee = Hero.MainHero; who = "gracz"; }
            else
            {
                var lp = winSide.LeaderParty;
                var lm = lp != null ? lp.MobileParty : null;
                var lh = lm != null ? lm.LeaderHero : null;
                if (lm != null && lm.IsLordParty && lh != null && lh != Hero.MainHero && s != null && s.WatchBountyAiLords) { payee = lh; who = "lord " + lh.StringId; }
                else
                {
                    _wDropGuard++;
                    Log.Info(PW + "zdjety bez wyplaty - " + n.Town + " " + n.Band + " rozbita przez " + (lm != null ? (lm.IsGarrison ? "zaloge" : lm.IsMilitia ? "milicje" : lm.IsCaravan ? "karawane" : lm.IsVillager ? "wiesniakow" : lm.IsBandit ? "bandytow" : lm.IsLordParty ? "lorda (klucz AI Lords NIE)" : "inna partie") : "nieznanego"));
                    return;
                }
            }
            try
            {
                var pf = payee.MapFaction;
                if (pf != null && st.MapFaction != null && pf != st.MapFaction && pf.IsAtWarWith(st.MapFaction))
                {
                    _wDropEnemy++;
                    Log.Info(PW + "zdjety bez wyplaty - " + n.Town + " " + n.Band + " rozbita przez wroga miasta (" + who + ")");
                    if (payee == Hero.MainHero) Log.Player("The " + GuardName(st).Replace("the ", "") + " of " + st.Name + " will pay no bounty to an enemy of their town.");
                    return;
                }
            }
            catch (Exception e) { WStumble("Settle(wrog)", e); }
            Pay(n, st, payee, who);
        }

        /// <summary>Wyplata: kasa miasta (wolna kasa) -> odbiorca; reszta od pana miasta (ponad rezerwe); brak - niedoplata. Zawsze z platnikiem (nic z niczego).</summary>
        private static void Pay(Notice n, Settlement st, Hero payee, string who)
        {
            var town = st.Town;
            int g0 = town.Gold;
            double keep = Keep(st);
            int free = (int)Math.Max(0, Math.Min(int.MaxValue, g0 - keep));
            int fromTown = Math.Max(0, Math.Min(n.Sum, free));
            if (n.Kind == 1) fromTown = 0;   // E2b: list soltysa placi kiesa wsi (PayHeadman)
            if (fromTown > 0) GiveGoldAction.ApplyForSettlementToCharacter(st, payee, fromTown, true);
            int g1 = town.Gold;
            int fromLord = 0;
            var s = Settings.Current;
            var owner = st.OwnerClan != null ? st.OwnerClan.Leader : null;
            if (n.Kind == 1) fromLord = PayHeadman(n, payee);   // E2b
            else if (n.Sum > fromTown && s != null && s.WatchBountyLordTopUp && owner != null && owner != payee && owner != Hero.MainHero && owner.IsAlive)
            {
                fromLord = Math.Max(0, Math.Min(n.Sum - fromTown, owner.Gold - LordReserve));
                if (fromLord > 0) GiveGoldAction.ApplyBetweenCharacters(owner, payee, fromLord, true);
            }
            int shortfall = Math.Max(0, n.Sum - fromTown - fromLord);
            if (g1 < keep - 0.5 && fromTown > 0) _wBelowKeep++;
            if (n.Kind == 0) { try { town.Security += 2f; } catch { } }
            try { GainRenownAction.Apply(payee, 1f, payee != Hero.MainHero); } catch (Exception e) { WStumble("Pay(slawa)", e); }
            int paid = fromTown + fromLord;
            _wPaidN++; _wPaid += paid; _wFromTown += fromTown; _wFromLordPurse += fromLord; _wShort += shortfall;
            if (payee == Hero.MainHero) { _wPaidPlayerN++; _wPaidPlayer += paid; } else { _wPaidLordN++; _wPaidLord += paid; }
            Log.Info(PW + "wyplata - " + n.Town + " " + n.Band + " obiecane " + n.Sum + ", kasa " + fromTown + " + " + (n.Kind == 1 ? "wies " : "pan ") + fromLord + ", niedoplata " + shortfall
                     + ", dla " + who + "; kasa przed " + g0 + " po " + g1 + " (Keep " + ((long)Math.Round(keep)).ToString(Inv) + ")");
            if (payee == Hero.MainHero)
            {
                string by = n.Kind == 1 ? "The headman of " + n.Village : Cap(GuardName(st)) + " of " + st.Name;
                if (n.Kind == 1) { var v = Settlement.Find(n.Village); if (v != null) by = "The headman of " + v.Name; }
                if (paid > 0) Log.Player(by + " pays you " + paid.ToString("N0", Inv) + " for breaking " + n.Name + ".");
                if (shortfall > 0) Log.Player((n.Kind == 1 ? "The village" : "The council") + " could pay only " + paid.ToString("N0", Inv) + " of the promised " + n.Sum.ToString("N0", Inv) + ".", true);
            }
        }

        private static double Keep(Settlement st)
        {
            double k = TownPurse.Reserve(st.Town);
            try { k = Math.Max(k, DsTowns.TdKeep(st)); } catch { }
            return k;
        }

        /// <summary>CampaignEvents.MobilePartyDestroyed: banda z listem zniknela bez rozbicia w bitwie (rozwiazana, wcielona) - list zdjety bez wyplaty.</summary>
        internal static void OnPartyDestroyed(MobileParty mp, PartyBase by)
        {
            if (mp == null || _notices.Count == 0) return;
            try
            {
                for (int i = _notices.Count - 1; i >= 0; i--)
                    if (_notices[i].Band == mp.StringId) { _notices.RemoveAt(i); _wDropGone++; }
                _harm.Remove(mp.StringId ?? "");
            }
            catch (Exception e) { WStumble("OnPartyDestroyed", e); }
        }

        // ------------------------------------------------------------ doba: zdjecie, rozklad band, wywieszenie

        private static Settlement NearestTown(Vec2 p)
        {
            Settlement best = null; float bd = float.MaxValue;
            foreach (var t in Town.AllTowns)
            {
                var s = t != null ? t.Settlement : null;
                if (s == null) continue;
                float d = s.GetPosition2D.DistanceSquared(p);
                if (d < bd) { bd = d; best = s; }
            }
            return best;
        }

        private sealed class Cand { public MobileParty Mp; public int Men, Bonus; public bool Mounted; }

        private static void WatchDaily(int day)
        {
            long t0 = Stopwatch.GetTimestamp();
            var s = Settings.Current;
            // 0) szkody starsze niz 60 dni
            if (_harm.Count > 0)
            {
                var drop = new List<string>();
                foreach (var kv in _harm) { kv.Value.RemoveAll(h => h.Day < day - HarmDays); if (kv.Value.Count == 0) drop.Add(kv.Key); }
                foreach (var k in drop) _harm.Remove(k);
            }
            // 1) zdjecie listow (po wczytaniu bandy po id jednym przebiegiem - bez szukania w calej liscie partii dla kazdego listu)
            Dictionary<string, MobileParty> byId = null;
            foreach (var n in _notices) if (n.Mp == null) { byId = new Dictionary<string, MobileParty>(StringComparer.Ordinal); break; }
            if (byId != null)
                foreach (var mp in MobileParty.All) if (mp != null && !string.IsNullOrEmpty(mp.StringId)) byId[mp.StringId] = mp;
            for (int i = _notices.Count - 1; i >= 0; i--)
            {
                var n = _notices[i];
                try
                {
                    if (n.St == null) n.St = Settlement.Find(n.Town);
                    if (n.Mp == null && byId != null) { MobileParty f; if (byId.TryGetValue(n.Band ?? "", out f)) n.Mp = f; }
                    if (n.Mp != null && string.IsNullOrEmpty(n.Name)) n.Name = BandNameOf(n.Mp);
                    if (n.St == null || n.St.Town == null || n.Mp == null || !n.Mp.IsActive) { _notices.RemoveAt(i); _wDropGone++; continue; }
                    if (day > n.Due) { _notices.RemoveAt(i); _wDropDue++; continue; }
                    string owner = n.St.OwnerClan != null ? n.St.OwnerClan.StringId : "";
                    if (owner != n.Owner) { _notices.RemoveAt(i); _wDropOwner++; continue; }
                }
                catch (Exception e) { WStumble("WatchDaily(zdjecie)", e); }
            }
            // 2) bandy wedlug miast (jeden przebieg)
            var byTown = new Dictionary<Settlement, List<Cand>>();
            int bands = 0;
            try
            {
                foreach (var mp in MobileParty.AllBanditParties)
                {
                    try
                    {
                        if (mp == null || !mp.IsActive || mp.MapEvent != null || mp.CurrentSettlement != null) continue;
                        if (Undead.Party(mp) || mp.IsCurrentlyAtSea) continue;
                        int men = mp.Party != null ? mp.Party.NumberOfHealthyMembers : 0;
                        if (men < 5) continue;
                        Vec2 p = mp.GetPosition2D;
                        var town = NearestTown(p);
                        if (town == null || town.GetPosition2D.Distance(p) > MaxBandDist) continue;
                        bands++;
                        List<Cand> l;
                        if (!byTown.TryGetValue(town, out l)) { l = new List<Cand>(); byTown[town] = l; }
                        l.Add(new Cand { Mp = mp, Men = men });
                    }
                    catch (Exception e) { WStumble("WatchDaily(banda)", e); }
                }
            }
            catch (Exception e) { WStumble("WatchDaily(bandy)", e); }
            // 3) wywieszenie
            int withBand = byTown.Count, noCash = 0, atLimit = 0, allPosted = 0, besieged = 0, inDebt = 0, noLord = 0, keyOff = 0, other = 0;
            var posted = new HashSet<string>(StringComparer.Ordinal);
            foreach (var n in _notices) posted.Add(n.Band);
            int per = PerTown;
            foreach (var kv in byTown)
            {
                var st = kv.Key;
                try
                {
                    if (s == null || !s.WatchBounties || per <= 0) { keyOff++; continue; }
                    int have = 0; long committed = 0;
                    foreach (var n in _notices) if (n.Kind == 0 && n.Town == st.StringId) { have++; committed += n.Sum; }
                    if (have >= per) { atLimit++; continue; }
                    if (st.IsUnderSiege) { besieged++; continue; }
                    if (TownDebt.ArrearsLike(st) > 0) { inDebt++; continue; }
                    if (st.OwnerClan == null) { noLord++; continue; }
                    var cands = new List<Cand>();
                    foreach (var c in kv.Value) if (!posted.Contains(c.Mp.StringId)) cands.Add(c);
                    if (cands.Count == 0) { allPosted++; continue; }
                    foreach (var c in cands) Score(c, st);
                    cands.Sort((a, b) => a.Bonus != b.Bonus ? b.Bonus.CompareTo(a.Bonus) : b.Men.CompareTo(a.Men));
                    float rate = GarrisonRate(st);
                    double free = Math.Max(0, st.Town.Gold - Keep(st)) - committed;
                    var owner = st.OwnerClan.Leader;
                    bool any = false, cash = false;
                    foreach (var c in cands)
                    {
                        if (have >= per) break;
                        int N = Math.Max(10, Math.Min(60, c.Men));
                        int D = c.Mounted ? 4 : 3;
                        int sum = Math.Max(1, (int)Math.Round(N * D * rate * Premium * (1 + c.Bonus / 100.0)));
                        bool byLord = false;
                        if (sum <= free / 4) { }
                        else if (s.WatchBountyLordTopUp && owner != null && owner != Hero.MainHero && owner.IsAlive && owner.Gold - LordReserve >= sum) byLord = true;
                        else continue;
                        var n = new Notice
                        {
                            Town = st.StringId, Band = c.Mp.StringId, Name = BandNameOf(c.Mp),
                            Owner = st.OwnerClan.StringId, Men = c.Men, Days = D, BonusPct = c.Bonus, S = rate, Sum = sum, Lord = byLord, Posted = day, Due = day + DaysUp,
                            St = st, Mp = c.Mp
                        };
                        _notices.Add(n);
                        posted.Add(n.Band);
                        if (!byLord) free -= sum;
                        have++; _wNew++; any = true; cash = true;
                        Log.Info(PW + "list - " + st.StringId + " " + n.Band + " (" + n.Men + " ludzi, " + D + " d, S " + rate.ToString("0.00", Inv) + ", szkody +" + c.Bonus + "%) = " + sum
                                 + ", platnik " + (byLord ? "pan " + (owner != null ? owner.StringId : "?") : "kasa") + ", wolna kasa " + ((long)Math.Max(0, free + (byLord ? 0 : sum))).ToString(Inv) + " - " + n.Name);
                    }
                    if (!any && !cash) noCash++;
                }
                catch (Exception e) { other++; WStumble("WatchDaily(miasto " + st.StringId + ")", e); }
            }
            // 3b) E2b: listy soltysow wsi zlupionych w 30 dniach (kiesa wsi)
            try { HeadmanPost(day, posted); } catch (Exception e) { WStumble("WatchDaily(soltysi)", e); }
            // 4) linia doby
            try
            {
                int withNotice = 0, takenAll = 0;
                var towns = new HashSet<string>(StringComparer.Ordinal);
                foreach (var n in _notices) { if (n.Kind == 0) towns.Add(n.Town); if (n.Taken) takenAll++; }
                withNotice = towns.Count;
                string exp = Export();
                int bytes = exp != null ? exp.Length : 0;
                double ms = (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency;
                Log.Info(PW + "dzien " + day + ", klucz " + YN(s != null && s.WatchBounties) + ", band poza osadami " + bands + ", miasta z banda " + withBand + "/" + Town.AllTowns.Count + ", z listem " + withNotice
                         + ", bez nowego listu: brak kasy " + noCash + ", limit " + atLimit + ", kazda banda z listem " + allPosted + ", oblezone " + besieged + ", dlug rady " + inDebt + ", bez pana " + noLord
                         + ", klucz " + keyOff + ", inne " + other
                         + "; czynne " + _notices.Count + " (nowe " + _wNew + ", przyjete przez gracza " + takenAll + ", dzis " + _wTakenToday + ")"
                         + "; wyplacone " + _wPaidN + " za " + _wPaid + " zl (gracz " + _wPaidPlayerN + "/" + _wPaidPlayer + " zl, lordowie " + _wPaidLordN + "/" + _wPaidLord + " zl; z kas miast " + _wFromTown
                         + ", od panow " + _wFromLordPurse + "), niedoplata " + _wShort + ", z niczego " + _wFromNothing
                         + "; zdjete " + (_wDropGuard + _wDropEnemy + _wDropGone + _wDropDue + _wDropOwner) + " (straz/karawana " + _wDropGuard + ", wrog miasta " + _wDropEnemy + ", banda znikla " + _wDropGone
                         + ", termin " + _wDropDue + ", zmiana pana " + _wDropOwner + "); kasa ponizej Keep po wyplacie " + _wBelowKeep
                         + HeadmanDayText() + "; zapis arm_board " + bytes + " B; szkody band " + _harm.Count + "; koszt " + ms.ToString("0.00", Inv) + " ms; potkniecia " + _wStumbles);
            }
            catch (Exception e) { WStumble("WatchDaily(linia)", e); }
            ZeroWatchDay();
        }

        /// <summary>Doplata za szkody i herszta; konni (wiekszosc zdrowych na koniu - D = 4).</summary>
        private static void Score(Cand c, Settlement st)
        {
            int v = HarmCount(c.Mp.StringId, st.StringId, 0), k = HarmCount(c.Mp.StringId, st.StringId, 1);
            int b = 25 * v + 15 * k + (c.Mp.LeaderHero != null ? 50 : 0);
            c.Bonus = Math.Min(100, b);
            int mounted = 0, all = 0;
            try
            {
                foreach (var el in c.Mp.MemberRoster.GetTroopRoster())
                {
                    if (el.Character == null) continue;
                    int healthy = Math.Max(0, el.Number - el.WoundedNumber);
                    all += healthy;
                    if (el.Character.IsMounted) mounted += healthy;
                }
            }
            catch { }
            c.Mounted = all > 0 && mounted * 2 > all;
        }

        /// <summary>S: pelny zold zalogi miasta na glowe (MountedWage.FullTotalWage); bez zalogi - 5.</summary>
        private static float GarrisonRate(Settlement st)
        {
            try
            {
                var g = st.Town != null ? st.Town.GarrisonParty : null;
                if (g == null || g.MemberRoster == null || g.MemberRoster.TotalManCount <= 0) return FallbackRate;
                float r = MountedWage.FullTotalWage(g) / (float)g.MemberRoster.TotalManCount;
                return r > 0.01f ? r : FallbackRate;
            }
            catch (Exception e) { WStumble("GarrisonRate", e); return FallbackRate; }
        }

        // E2b: list soltysa (kiesa wsi) - NoticeBoard.Headman.cs
        private static int PayHeadman(Notice n, Hero payee) { return HeadmanPay(n, payee); }

        private static string WatchProbeText(List<Settlement> towns)
        {
            int faces = 0, garrisons = 0;
            string ew4 = "-";
            foreach (var st in towns)
            {
                var f = CaptainOf(st);
                if (f != null) faces++;
                if (st.Town != null && st.Town.GarrisonParty != null) garrisons++;
                if (st.StringId == "town_EW4") ew4 = f != null ? f.StringId : "BRAK";
            }
            return ", twarze kapitana " + faces + "/" + towns.Count + " (zaloga " + garrisons + ", town_EW4 " + ew4 + "), listow czynnych " + _notices.Count;
        }

        // ------------------------------------------------------------ zapis ("W=miasto~banda~(nazwa - pusta, liczona po wczytaniu)~ludzie~dni~doplata~kwota~od~do~flagi~S*100~rodzaj~ktos~wies~pan;...|H=banda~doba,rodzaj,miasto/...;...")

        private static void WatchExport(StringBuilder sb)
        {
            sb.Append("|W=");
            bool first = true;
            foreach (var n in _notices)
            {
                if (!first) sb.Append(';');
                first = false;
                sb.Append(Safe(n.Town)).Append('~').Append(Safe(n.Band)).Append('~').Append('~').Append(n.Men.ToString(Inv)).Append('~').Append(n.Days.ToString(Inv))
                  .Append('~').Append(n.BonusPct.ToString(Inv)).Append('~').Append(n.Sum.ToString(Inv)).Append('~').Append(n.Posted.ToString(Inv)).Append('~').Append(n.Due.ToString(Inv))
                  .Append('~').Append(n.Taken ? "T" : "").Append(n.Lord ? "L" : "").Append('~').Append(((int)Math.Round(n.S * 100)).ToString(Inv)).Append('~').Append(n.Kind.ToString(Inv))
                  .Append('~').Append(Safe(n.Giver)).Append('~').Append(Safe(n.Village)).Append('~').Append(Safe(n.Owner));
            }
            sb.Append("|H=");
            first = true;
            foreach (var kv in _harm)
            {
                if (kv.Value.Count == 0) continue;
                if (!first) sb.Append(';');
                first = false;
                sb.Append(Safe(kv.Key)).Append('~');
                for (int i = 0; i < kv.Value.Count; i++)
                {
                    var h = kv.Value[i];
                    if (i > 0) sb.Append('/');
                    sb.Append(h.Day.ToString(Inv)).Append(',').Append(h.Kind.ToString(Inv)).Append(',').Append(Safe(h.Town).Replace(",", ".")).Append(',').Append(Safe(h.Vil).Replace(",", "."));
                }
            }
        }

        private static void WatchImport(string p)
        {
            try
            {
                if (p.StartsWith("W=", StringComparison.Ordinal))
                {
                    _notices.Clear();
                    foreach (var r in p.Substring(2).Split(';'))
                    {
                        var x = r.Split('~');
                        if (x.Length < 15) continue;
                        int men, days, bonus, sum, posted, due, s100, kind;
                        if (!int.TryParse(x[3], NumberStyles.Integer, Inv, out men) || !int.TryParse(x[4], NumberStyles.Integer, Inv, out days) || !int.TryParse(x[5], NumberStyles.Integer, Inv, out bonus)
                            || !int.TryParse(x[6], NumberStyles.Integer, Inv, out sum) || !int.TryParse(x[7], NumberStyles.Integer, Inv, out posted) || !int.TryParse(x[8], NumberStyles.Integer, Inv, out due)
                            || !int.TryParse(x[10], NumberStyles.Integer, Inv, out s100) || !int.TryParse(x[11], NumberStyles.Integer, Inv, out kind)) continue;
                        _notices.Add(new Notice
                        {
                            Town = x[0], Band = x[1], Name = x[2], Men = men, Days = days, BonusPct = bonus, Sum = sum, Posted = posted, Due = due,
                            Taken = x[9].IndexOf('T') >= 0, Lord = x[9].IndexOf('L') >= 0, S = s100 / 100f, Kind = kind, Giver = x[12], Village = x[13], Owner = x[14]
                        });
                    }
                }
                else if (p.StartsWith("H=", StringComparison.Ordinal))
                {
                    _harm.Clear();
                    foreach (var r in p.Substring(2).Split(';'))
                    {
                        int k = r.IndexOf('~');
                        if (k <= 0) continue;
                        var l = new List<Harm>();
                        foreach (var h in r.Substring(k + 1).Split('/'))
                        {
                            var y = h.Split(',');
                            int d, kind;
                            if (y.Length < 3 || !int.TryParse(y[0], NumberStyles.Integer, Inv, out d) || !int.TryParse(y[1], NumberStyles.Integer, Inv, out kind)) continue;
                            l.Add(new Harm { Day = d, Kind = (byte)kind, Town = y[2], Vil = y.Length > 3 ? y[3] : "" });
                        }
                        if (l.Count > 0) _harm[r.Substring(0, k)] = l;
                    }
                }
            }
            catch (Exception e) { WStumble("WatchImport", e); }
        }
    }
}
