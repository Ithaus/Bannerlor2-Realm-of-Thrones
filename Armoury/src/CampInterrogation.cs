using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Core.ImageIdentifiers;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// m109-przesluch (Jeff 22.10 ok. 21:40, pkt 4a: "jak mam jencow, moge ich przesluchac, gdzie jest ich kryjowka - rogery [bandyci] w obozie; wtedy mam opcje
    /// przesluchaj jencow, moge ich przy tym tez zabic, jesli uzyje tortur, aby wydusic info") - PRZESLUCHANIE JENCOW W OBOZIE. Klucz CampInterrogation (TAK).
    /// Opcja "Question the prisoners" w naszym menu obozu arm_camp_wait (ZWYKLE menu, czas stoi - bez SwitchToMenu, okna przez inquiry jak DistrictMenu/MusterBook).
    ///  - JENCY-BANDYCI: zwykly zolnierz Occupation Bandit albo kultura bandytow (jak PrisonerLaw.Criminal); bohater Occupation Bandit albo z klanu bandytow; nigdy
    ///    wight (PeopleBook.IsWight). Grupa = stos jednego rodzaju zolnierza (klucz StringId) albo jeden bohater ("h:" + StringId).
    ///  - PAMIEC BAND (zapis arm_interrogation): po kazdej WYGRANEJ bitwie gracza z banda wpis kultura bandy (klan), jej kryjowka (BanditPartyComponent.Hideout),
    ///    miejsce bitwy, doba; najwyzej MemoryMax (30) wpisow, starsze niz MemoryDays (60) dob odpadaja. Skladu bandy nie zapisujemy (MapEventParty.Troops jest
    ///    przebudowywane z biezacego skladu - w MapEventEnded bywa puste).
    ///  - KTORA KRYJOWKE ZNA JENIEC, po kolei: (a) bohater - BornSettlement, gdy to kryjowka zajeta i niewypatrzona (BK rodzi herszta w kryjowce jego bandy);
    ///    (b) pamiec bandy - najnowszy wpis tej kultury, jego kryjowka zajeta i niewypatrzona, w dowolnej odleglosci; (c) kryjowka jego ludzi - najblizsza zajeta,
    ///    niewypatrzona kryjowka tej kultury do LairRange (160 jedn., jak plotka o kryjowce) od miejsca bitwy z wpisu, bez wpisu od obozu; (d) nic - grupa szara
    ///    z powodem (kultura bez kryjowek / wszystkie znane / zadnej w zasiegu). Kultura kryjowki = Settlement.Culture (gra osadza bandy w kryjowkach swojej kultury).
    ///  - ROZMOWA: clamp(0.10 + P/400 + 0.05 ranny + 0.02 x min(5, doby od bitwy | doby niewoli bohatera) - 0.03 x (tier - 1) - 0.15 bohater, 0.05, 0.85);
    ///    P = Persuasion.Measure (Urok i perki perswazji) wobec kultury jenca; wybrany ranny, jesli jest. XP Uroku +20 za sukces, +5 za porazke.
    ///  - TORTURY: clamp(skladniki rozmowy + 0.30 + Lotrostwo/600 + 0.10 Scarface, 0.05, 0.95); wybrany zdrowy, jesli jest. Smierc (rzut niezalezny): 0.15 zdrowy,
    ///    0.30 ranny, x 0.8 z Forgivable Grievances. Zwykly: zdjety z lochu i "straceni" ksiegi ludzi (PeopleBook FDieExec, jak OthersRise); bohater:
    ///    KillCharacterAction.ApplyByMurder (bez Honoru -1000 egzekucji; weto CanHeroDie - przezywa, log). XP Lotrostwa +20, +10 gdy mowi.
    ///    Kary: Mercy -20 za torture, -40 wiecej za smierc (TraitLevelingHelper.OnIncidentResolved); przy smierci -2 relacji z bohaterami druzyny z Mercy >= 1
    ///    i plotka w karczmach (Rumours.AddCruelty, wiesc NCruel).
    ///  - LIMIT: jedna rozmowa i jedna tortura na grupe na dobe (czas w menu obozu stoi - przesluchanie nie zjada godzin).
    ///  - UJAWNIENIE jak zwiad gry bez zdarzenia gry (bez 100 XP Zwiadu i drugiego meldunku): Hideout.IsSpotted, Settlement.IsVisible, flaga HideoutSpotter.Mark.
    ///  - Ekonomia: zero przeplywow zlota i towarow (zabity jeniec = utracony okup).
    /// Log "Przesluchanie (m109): " - start sesji, pamiec (pierwsze DetailMax na sesje), kazda proba i odmowa, linia doby; tools/sprawdz_logi.py --grupa m109przes.
    /// Bledy liczone per miejsce (pierwsze 3 i co 50. do Log.Error), nic nie gasimy. Autotest (flaga armoury-przesluch.flag) - CampInterrogation.Test.cs.
    /// </summary>
    internal static partial class CampInterrogation
    {
        internal const string P = "Przesluchanie (m109): ";
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        // parametry (Jeff: liczby ustala Claude - stale, nie suwaki)
        internal const float TalkBase = 0.10f, PointsDiv = 400f, WoundedBonus = 0.05f, ChainsPerDay = 0.02f, TierPenalty = 0.03f, HeroPenalty = 0.15f;
        internal const int ChainsDaysMax = 5, TierMax = 6;
        internal const float TalkMin = 0.05f, TalkMax = 0.85f;
        internal const float TortureBonus = 0.30f, RogueryDiv = 600f, ScarfaceBonus = 0.10f, TortureMin = 0.05f, TortureMax = 0.95f;
        internal const float DeathHealthy = 0.15f, DeathWounded = 0.30f, GrievancesFactor = 0.8f;
        internal const int CharmXpWin = 20, CharmXpFail = 5, RogueryXpTorture = 20, RogueryXpTalked = 10;
        internal const int MercyTorture = -20, MercyDeath = -40, RelationDeath = -2;
        internal const float LairRange = 160f;
        internal const int MemoryMax = 30, MemoryDays = 60, DetailMax = 10;

        internal const string WhyNoKind = "Outlaws of this kind keep no lair - they live off the road.";
        internal const string WhyAllKnown = "Every lair of his people that he could know of is already known to you.";
        internal const string WhyNoneNear = "He knows of no lair of his people anywhere near.";

        internal static bool On { get { var s = Settings.Current; return s != null && s.CampInterrogation; } }

        // ------------------------------------------------------------ stan (zapis arm_interrogation)
        internal sealed class Band { public int Day; public string Culture = "", Hideout = "", Party = ""; public float X, Y; }
        private static readonly List<Band> _bands = new List<Band>();
        private static readonly Dictionary<string, int[]> _used = new Dictionary<string, int[]>(StringComparer.Ordinal);   // klucz grupy -> {doba rozmowy, doba tortury}
        // sumy kampanii: rozmowy, mowili, tortury, mowili, zmarli, ujawnione
        private static readonly int[] _sum = new int[6];
        private static string _importNote = "brak klucza (stary zapis albo nowa kampania)";

        // liczniki doby i sesji
        private static int _dTalk, _dTalkYes, _dTort, _dTortYes, _dDeadMen, _dDeadHero, _dBlocked, _dRevHero, _dRevBand, _dRevKin, _dMemNew, _dRefused, _dStumbles;
        private static int _stumbles, _detail, _sessionDays;
        private static readonly Dictionary<string, int> _stumbleAt = new Dictionary<string, int>(StringComparer.Ordinal);

        private static int Today { get { return (int)CampaignTime.Now.ToDays; } }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++; _dStumbles++;
            int n;
            _stumbleAt.TryGetValue(where, out n);
            _stumbleAt[where] = ++n;
            if (n <= 3 || n % 50 == 0) { try { Log.Error("CampInterrogation." + where + " (potkniecie " + n + ")", e); } catch { } }
        }

        internal static void Reset()
        {
            _bands.Clear(); _used.Clear(); Array.Clear(_sum, 0, _sum.Length);
            _importNote = "brak klucza (stary zapis albo nowa kampania)";
            TestReset();
            DayReset();
        }

        private static void DayReset()
        {
            _dTalk = _dTalkYes = _dTort = _dTortYes = _dDeadMen = _dDeadHero = _dBlocked = _dRevHero = _dRevBand = _dRevKin = _dMemNew = _dRefused = _dStumbles = 0;
        }

        // ------------------------------------------------------------ wpiecie (ArmouryBehavior.RegisterEvents - jedna linia)
        internal static void RegisterEvents(CampaignBehaviorBase b)
        {
            CampaignEvents.OnNewGameCreatedEvent.AddNonSerializedListener(b, delegate { try { Reset(); } catch (Exception e) { Stumble("Reset", e); } });
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(b, delegate (CampaignGameStarter s)
            {
                try { _stumbles = 0; _stumbleAt.Clear(); _detail = 0; _sessionDays = 0; DayReset(); if (s != null) AddMenus(s); SessionStart(); }
                catch (Exception e) { Stumble("SessionStart", e); }
            });
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(b, delegate { try { Daily(); } catch (Exception e) { Stumble("Daily", e); } });
            CampaignEvents.MapEventEnded.AddNonSerializedListener(b, DayProfile.MapEnd("CampInterrogation.OnMapEventEnded", OnMapEventEnded));
        }

        // ------------------------------------------------------------ pamiec band (MapEventEnded)
        /// <summary>Strona gracza w bitwie (przy MapEventEnded partia gracza jest jeszcze w bitwie); None - bitwa bez gracza.</summary>
        private static BattleSideEnum PlayerSideOf(MapEvent me)
        {
            var mine = PartyBase.MainParty;
            if (mine == null) return BattleSideEnum.None;
            if (me.IsPlayerMapEvent && mine.MapEventSide != null) return mine.Side;
            foreach (var side in new[] { me.AttackerSide, me.DefenderSide })
            {
                if (side == null || side.Parties == null) continue;
                for (int i = 0; i < side.Parties.Count; i++)
                    if (side.Parties[i] != null && side.Parties[i].Party == mine) return side == me.AttackerSide ? BattleSideEnum.Attacker : BattleSideEnum.Defender;
            }
            return BattleSideEnum.None;
        }

        internal static void OnMapEventEnded(MapEvent me)
        {
            if (!On || me == null) return;
            try
            {
                var ps = PlayerSideOf(me);   // bitwa AI - wyjscie od razu (kilka partii stron, bez przegladu swiata)
                if (ps == BattleSideEnum.None) return;
                var win = me.WinningSide;
                if (win == BattleSideEnum.None || ps != win) return;
                var lose = win == BattleSideEnum.Attacker ? me.DefenderSide : me.AttackerSide;
                if (lose == null || lose.Parties == null) return;
                Vec2 pos = me.Position.ToVec2();
                int today = Today;
                foreach (var mep in lose.Parties)
                {
                    var m = mep != null && mep.Party != null ? mep.Party.MobileParty : null;
                    if (m == null || !m.IsBandit) continue;
                    try { Remember(m, pos, today); }
                    catch (Exception e) { Stumble("Remember", e); }
                }
            }
            catch (Exception e) { Stumble("OnMapEventEnded", e); }
        }

        private static void Remember(MobileParty m, Vec2 pos, int today)
        {
            var clan = m.ActualClan;
            var cul = clan != null && clan.Culture != null ? clan.Culture : (m.Party != null ? m.Party.Culture : null);
            if (cul == null || string.IsNullOrEmpty(cul.StringId)) return;
            var hc = m.BanditPartyComponent;
            var hs = hc != null && hc.Hideout != null ? hc.Hideout.Settlement : null;
            string id = m.StringId ?? "";
            string pid = Clean(id);
            _bands.RemoveAll(x => pid.Length > 0 && x.Party == pid);   // ta sama banda pobita drugi raz - wpis od nowa
            var b = new Band { Day = today, Culture = Clean(cul.StringId), Hideout = hs != null ? Clean(hs.StringId) : "", Party = pid, X = pos.x, Y = pos.y };
            _bands.Add(b);
            while (_bands.Count > MemoryMax) _bands.RemoveAt(0);
            _dMemNew++;
            if (_detail < DetailMax)
            {
                _detail++;
                Log.Info(P + "pamiec - banda " + b.Party + " (" + (clan != null ? clan.Name.ToString() : "-") + ", kultura " + b.Culture + ") | kryjowka " + (b.Hideout.Length > 0 ? b.Hideout : "brak")
                         + " (zajeta " + (hs != null && hs.Hideout != null && hs.Hideout.IsInfested ? "TAK" : "NIE") + ") | miejsce " + pos.x.ToString("0.0", Inv) + "," + pos.y.ToString("0.0", Inv)
                         + " | doba " + today + " | pamiec " + _bands.Count + ".");
            }
        }

        private static string Clean(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new StringBuilder(s.Length);
            foreach (char ch in s) sb.Append(ch == ',' || ch == ';' || ch == '|' || ch == '\n' || ch == '\r' ? '_' : ch);
            return sb.ToString();
        }

        /// <summary>Najnowszy wpis pamieci tej kultury z ostatnich MemoryDays dob (null - brak).</summary>
        internal static Band Memory(CultureObject c)
        {
            if (c == null) return null;
            int today = Today;
            for (int i = _bands.Count - 1; i >= 0; i--)
            {
                var b = _bands[i];
                if (b.Culture == c.StringId && today - b.Day <= MemoryDays) return b;
            }
            return null;
        }

        // ------------------------------------------------------------ jency
        internal sealed class Group
        {
            public string Key, Name;
            public CharacterObject Troop;
            public Hero Hero;
            public int Healthy, Wounded, Tier;
            public CultureObject Culture;         // kultura kryjowek (bohater: kultura klanu bandytow)
            public CultureObject PersonCulture;   // kultura rozmowcy (premia przywodcy w mierze perswazji)
        }

        internal static bool BanditTroop(CharacterObject ch)
        {
            return ch != null && !ch.IsHero && !PeopleBook.IsWight(ch) && (ch.Occupation == Occupation.Bandit || (ch.Culture != null && ch.Culture.IsBandit));
        }

        internal static bool BanditHero(Hero h)
        {
            if (h == null || !h.IsAlive || h == Hero.MainHero || h.IsPlayerCompanion || (h.Clan != null && h.Clan == Clan.PlayerClan)) return false;
            if (PeopleBook.IsWight(h.CharacterObject)) return false;
            return h.Occupation == Occupation.Bandit || (h.Clan != null && h.Clan.IsBanditFaction);
        }

        private static int TierOf(CharacterObject ch)
        {
            int t = 1;
            try { t = ch.Tier; } catch { t = 1; }
            return Math.Max(1, Math.Min(TierMax, t));
        }

        /// <summary>Grupy jencow-bandytow w lochu partii gracza (stos rodzaju albo bohater).</summary>
        internal static List<Group> Groups()
        {
            var res = new List<Group>();
            var mp = MobileParty.MainParty;
            var r = mp != null ? mp.PrisonRoster : null;
            if (r == null) return res;
            for (int i = 0; i < r.Count; i++)
            {
                var el = r.GetElementCopyAtIndex(i);
                var ch = el.Character;
                if (ch == null || el.Number <= 0) continue;
                try
                {
                    if (ch.IsHero)
                    {
                        var h = ch.HeroObject;
                        if (!BanditHero(h)) continue;
                        bool w = h.IsWounded;
                        res.Add(new Group { Key = Clean("h:" + h.StringId), Name = h.Name.ToString(), Troop = ch, Hero = h, Healthy = w ? 0 : 1, Wounded = w ? 1 : 0, Tier = TierOf(ch),
                                            Culture = h.Clan != null && h.Clan.IsBanditFaction && h.Clan.Culture != null ? h.Clan.Culture : h.Culture, PersonCulture = h.Culture });
                    }
                    else
                    {
                        if (!BanditTroop(ch)) continue;
                        int wn = Math.Max(0, Math.Min(el.Number, el.WoundedNumber));
                        res.Add(new Group { Key = Clean(ch.StringId), Name = ch.Name.ToString(), Troop = ch, Healthy = el.Number - wn, Wounded = wn, Tier = TierOf(ch),
                                            Culture = ch.Culture, PersonCulture = ch.Culture });
                    }
                }
                catch (Exception e) { Stumble("Groups", e); }
            }
            return res;
        }

        internal static Group Find(string key)
        {
            foreach (var g in Groups()) if (g.Key == key) return g;
            return null;
        }

        private static void Count(out int groups, out int men, out int heroes)
        {
            groups = men = heroes = 0;
            foreach (var g in Groups()) { groups++; if (g.Hero != null) heroes++; else men += g.Healthy + g.Wounded; }
        }

        // ------------------------------------------------------------ kryjowka jenca
        /// <summary>Kryjowka, ktora zna jeniec: (a) bohater - BornSettlement, (b) pamiec bandy, (c) najblizsza kryjowka kultury do LairRange; null - (d) z powodem
        /// (why - dla gracza, whyLog - do logu). source: "bohater" | "banda" | "kultura".</summary>
        internal static Settlement Target(Group g, out string source, out Band mem, out string why, out string whyLog)
        {
            source = null; why = null; whyLog = null; mem = null;
            if (g.Hero != null)
            {
                var born = g.Hero.BornSettlement;
                if (Open(born)) { source = "bohater"; mem = Memory(g.Culture); return born; }
            }
            var c = g.Culture;
            mem = Memory(c);
            if (mem != null && mem.Hideout.Length > 0)
            {
                var st = Settlement.Find(mem.Hideout);
                if (Open(st)) { source = "banda"; return st; }
            }
            if (c == null) { why = WhyNoKind; whyLog = "brak kultury"; return null; }
            var mp = MobileParty.MainParty;
            Vec2 from = mem != null ? new Vec2(mem.X, mem.Y) : (mp != null ? mp.Position.ToVec2() : Vec2.Zero);
            int total = 0, held = 0, hidden = 0;
            Settlement best = null; float bd = LairRange;
            foreach (var h in Hideout.All)
            {
                var st = h != null ? h.Settlement : null;
                if (st == null || st.Culture != c) continue;
                total++;
                if (!h.IsInfested) continue;
                held++;
                if (h.IsSpotted) continue;
                hidden++;
                float d = st.GetPosition2D.Distance(from);
                if (d <= bd) { bd = d; best = st; }
            }
            if (best != null) { source = "kultura"; return best; }
            if (total == 0) { why = WhyNoKind; whyLog = "kultura bez kryjowek"; }
            else if (held > 0 && hidden == 0) { why = WhyAllKnown; whyLog = "wszystkie znane"; }
            else { why = WhyNoneNear; whyLog = "zadna w zasiegu (zajetych " + held + ", niewypatrzonych " + hidden + ")"; }
            return null;
        }

        private static bool Open(Settlement st)
        {
            return st != null && st.IsHideout && st.Hideout != null && st.Hideout.IsInfested && !st.Hideout.IsSpotted;
        }

        // ------------------------------------------------------------ szanse
        internal struct Odds
        {
            public float Base, Measure, Wounded, Chains, Tier, Hero, Torture, Roguery, Scarface, Sum, Chance;
            public int ChainsDays;
            public bool PickWounded;
        }

        private static int ChainsDays(Group g, Band mem)
        {
            try
            {
                if (g.Hero != null) return Math.Max(0, (int)(CampaignTime.Now - g.Hero.CaptivityStartTime).ToDays);
                return mem != null ? Math.Max(0, Today - mem.Day) : 0;
            }
            catch { return 0; }
        }

        internal static Odds Chance(Group g, Persuasion.Parts p, bool torture, Band mem)
        {
            var o = new Odds();
            o.PickWounded = torture ? g.Healthy <= 0 && g.Wounded > 0 : g.Wounded > 0;
            o.ChainsDays = ChainsDays(g, mem);
            o.Base = TalkBase;
            o.Measure = p.Total / PointsDiv;
            o.Wounded = o.PickWounded ? WoundedBonus : 0f;
            o.Chains = ChainsPerDay * Math.Min(ChainsDaysMax, o.ChainsDays);
            o.Tier = TierPenalty * (Math.Max(1, g.Tier) - 1);
            o.Hero = g.Hero != null ? HeroPenalty : 0f;
            if (torture)
            {
                var me = Hero.MainHero;
                int rog = 0; bool scar = false;
                try { if (me != null) { rog = me.GetSkillValue(DefaultSkills.Roguery); scar = me.GetPerkValue(DefaultPerks.Roguery.Scarface); } } catch { }
                o.Torture = TortureBonus;
                o.Roguery = rog / RogueryDiv;
                o.Scarface = scar ? ScarfaceBonus : 0f;
            }
            o.Sum = o.Base + o.Measure + o.Wounded + o.Chains - o.Tier - o.Hero + o.Torture + o.Roguery + o.Scarface;
            o.Chance = torture ? Math.Max(TortureMin, Math.Min(TortureMax, o.Sum)) : Math.Max(TalkMin, Math.Min(TalkMax, o.Sum));
            return o;
        }

        private static bool Grievances()
        {
            try { var me = Hero.MainHero; return me != null && me.GetPerkValue(DefaultPerks.Charm.ForgivableGrievances); } catch { return false; }
        }

        internal static float DeathChance(bool wounded, out bool grievances)
        {
            grievances = Grievances();
            return (wounded ? DeathWounded : DeathHealthy) * (grievances ? GrievancesFactor : 1f);
        }

        private static string F4(float v) { return v.ToString("0.0000", Inv); }
        private static string Pct(float v) { return ((int)Math.Round(v * 100f)).ToString(Inv) + "%"; }

        private static string OddsLog(Odds o)
        {
            return "szansa " + F4(o.Chance) + " = baza " + F4(o.Base) + " + miara " + F4(o.Measure) + " + ranny " + F4(o.Wounded) + " + niewola " + F4(o.Chains) + " - tier " + F4(o.Tier)
                   + " - bohater " + F4(o.Hero) + " + tortury " + F4(o.Torture) + " + Lotrostwo " + F4(o.Roguery) + " + Scarface " + F4(o.Scarface);
        }

        private static bool Used(string key, int slot, int today)
        {
            int[] u;
            return _used.TryGetValue(key, out u) && u[slot] == today;
        }

        // ------------------------------------------------------------ przesluchanie
        internal sealed class Result
        {
            public bool Refused, Talked, Died, Blocked, Flag, Rumour;
            public string RefuseWhy, Source, Where, Text;
            public float Chance, Roll, DeathChance = -1f, DeathRoll = -1f;
            public Settlement Lair;
            public int MercyBefore, MercyAfter;
        }

        private static int MercyXp()
        {
            try { return Campaign.Current.PlayerTraitDeveloper.GetPropertyValue(DefaultTraits.Mercy); } catch { return 0; }
        }

        /// <summary>Jedno przesluchanie grupy (gra i test - rzuty -1 = MBRandom). Limit doby, rzut, ujawnienie, smierc pod torturami, kary, XP, linia logu.</summary>
        internal static Result Act(Group g0, bool torture, float rollTalk = -1f, float rollDeath = -1f)
        {
            var res = new Result();
            int today = Today;
            string kind = torture ? "tortury" : "rozmowa";
            var g = g0 != null ? Find(g0.Key) : null;   // stan lochu teraz (okno moglo byc otwarte dawniej)
            if (g == null)
            {
                res.Refused = true; res.RefuseWhy = "brak grupy"; _dRefused++;
                Log.Info(P + "odmowa - brak grupy | doba " + today + " | grupa " + (g0 != null ? g0.Key : "-") + " | rodzaj " + kind + ".");
                return res;
            }
            if (Used(g.Key, torture ? 1 : 0, today))
            {
                res.Refused = true; res.RefuseWhy = "limit"; _dRefused++;
                Log.Info(P + "odmowa - limit | doba " + today + " | grupa " + g.Key + " | rodzaj " + kind + ".");
                return res;
            }
            string source, why, whyLog; Band mem;
            var lair = Target(g, out source, out mem, out why, out whyLog);
            if (lair == null)
            {
                res.Refused = true; res.RefuseWhy = why; _dRefused++;
                Log.Info(P + "odmowa - brak kryjowki | doba " + today + " | grupa " + g.Key + " | rodzaj " + kind + " | " + whyLog + ".");
                return res;
            }
            var me = Hero.MainHero;
            var pp = Persuasion.Measure(me, g.PersonCulture);
            var o = Chance(g, pp, torture, mem);
            res.Chance = o.Chance;
            res.Roll = rollTalk >= 0f ? rollTalk : MBRandom.RandomFloat;
            res.Talked = res.Roll < o.Chance;
            int h0 = g.Healthy, w0 = g.Wounded;
            int[] slot;
            if (!_used.TryGetValue(g.Key, out slot)) { slot = new[] { -1, -1 }; _used[g.Key] = slot; }
            slot[torture ? 1 : 0] = today;
            if (torture) _dTort++; else _dTalk++;
            if (torture) _sum[2]++; else _sum[0]++;

            // ujawnienie (jak zwiad gry, bez zdarzenia gry)
            bool spottedBefore = lair.Hideout.IsSpotted;
            string lairLog;
            float dCamp = MobileParty.MainParty != null ? lair.GetPosition2D.Distance(MobileParty.MainParty.Position.ToVec2()) : -1f;
            if (res.Talked)
            {
                try
                {
                    lair.Hideout.IsSpotted = true;
                    lair.IsVisible = true;
                    res.Flag = HideoutSpotter.Mark(lair);
                    res.Where = HideoutSpotter.Where(lair);
                }
                catch (Exception e) { Stumble("Reveal", e); }
                res.Lair = lair; res.Source = source;
                if (torture) { _dTortYes++; _sum[3]++; } else { _dTalkYes++; _sum[1]++; }
                _sum[5]++;
                if (source == "bohater") _dRevHero++; else if (source == "banda") _dRevBand++; else _dRevKin++;
                lairLog = "kryjowka " + lair.StringId + " (kultura " + (lair.Culture != null ? lair.Culture.StringId : "-") + ", zajeta " + (lair.Hideout.IsInfested ? "TAK" : "NIE")
                          + ", wypatrzona przed " + (spottedBefore ? "TAK" : "NIE") + " -> po " + (lair.Hideout.IsSpotted ? "TAK" : "NIE") + ", skad " + source
                          + ", " + dCamp.ToString("0.0", Inv) + " jedn. od obozu, flaga " + (res.Flag ? "TAK" : "NIE") + ")";
            }
            else lairLog = "kryjowka - (cel " + lair.StringId + ", skad " + source + ")";

            // smierc pod torturami (rzut niezalezny)
            string deathLog = "smierc -";
            int heroesHit = 0; var names = new List<string>();
            if (torture)
            {
                bool fg;
                res.DeathChance = DeathChance(o.PickWounded, out fg);
                res.DeathRoll = rollDeath >= 0f ? rollDeath : MBRandom.RandomFloat;
                string how = "NIE";
                if (res.DeathRoll < res.DeathChance) how = Kill(g, o.PickWounded, res);
                deathLog = "smierc szansa " + F4(res.DeathChance) + " (ranny " + (o.PickWounded ? "TAK" : "NIE") + ", Forgivable Grievances " + (fg ? "TAK" : "NIE") + "), rzut " + F4(res.DeathRoll) + " -> " + how;
            }
            var ga = Find(g.Key);
            int h1 = ga != null ? ga.Healthy : 0, w1 = ga != null ? ga.Wounded : 0;

            // kary
            string mercyLog = "Mercy -", relLog = "relacje -", rumourLog = "-";
            if (torture)
            {
                res.MercyBefore = MercyXp();
                try
                {
                    TraitLevelingHelper.OnIncidentResolved(DefaultTraits.Mercy, MercyTorture);
                    if (res.Died) TraitLevelingHelper.OnIncidentResolved(DefaultTraits.Mercy, MercyDeath);
                }
                catch (Exception e) { Stumble("Mercy", e); }
                res.MercyAfter = MercyXp();
                mercyLog = "Mercy xp " + (res.Died ? MercyTorture + MercyDeath : MercyTorture) + " (" + res.MercyBefore + " -> " + res.MercyAfter + ")";
                if (res.Died)
                {
                    try { heroesHit = MercifulFrown(names); } catch (Exception e) { Stumble("Relations", e); }
                    relLog = "relacje " + RelationDeath + " x " + heroesHit + (names.Count > 0 ? " (" + string.Join(", ", names.ToArray()) + ")" : "");
                    try
                    {
                        var mp = MobileParty.MainParty;
                        res.Rumour = mp != null && me != null && Rumours.AddCruelty(mp.Position.ToVec2(), me.Name.ToString());
                    }
                    catch (Exception e) { Stumble("Rumour", e); }
                    rumourLog = res.Rumour ? "TAK" : "NIE";
                }
            }

            // XP
            int xpCharm = 0, xpRog = 0;
            try
            {
                if (!torture) { xpCharm = res.Talked ? CharmXpWin : CharmXpFail; me.AddSkillXp(DefaultSkills.Charm, xpCharm); }
                else { xpRog = RogueryXpTorture + (res.Talked ? RogueryXpTalked : 0); me.AddSkillXp(DefaultSkills.Roguery, xpRog); }
            }
            catch (Exception e) { Stumble("Xp", e); }

            Log.Info(P + "proba " + kind + " | doba " + today + " | grupa " + g.Key + " (" + g.Name + ", kultura " + (g.Culture != null ? g.Culture.StringId : "-") + ", tier " + g.Tier
                     + ", bohater " + (g.Hero != null ? "TAK" : "NIE") + ", wybrany " + (o.PickWounded ? "ranny" : "zdrowy") + ", w niewoli " + o.ChainsDays + " dob, przed " + h0 + "/" + w0 + ")"
                     + " | miara " + pp.Total + " (" + Persuasion.LogText(pp) + ") | " + OddsLog(o) + " | rzut " + F4(res.Roll) + " -> " + (res.Talked ? "MOWI" : "MILCZY")
                     + " | " + lairLog + " | " + deathLog + " | po " + h1 + "/" + w1 + " | " + mercyLog + " | " + relLog + " | plotka " + rumourLog
                     + " | XP Urok +" + xpCharm + ", Lotrostwo +" + xpRog + " | dzis rozmow " + _dTalk + ", tortur " + _dTort + ".");
            res.Text = Story(g, torture, res, mem);
            try
            {
                if (res.Talked) Log.Player("A prisoner talked: an outlaw lair " + res.Where + (res.Flag ? " - flagged on your map." : "."));
                if (res.Died) Log.Player(g.Name + " died under the irons.", true);
            }
            catch { }
            return res;
        }

        /// <summary>Smierc jenca pod torturami. Zwykly: zdjety z lochu (wybrana kolumna) i "straceni" ksiegi ludzi; bohater: ApplyByMurder (weto CanHeroDie - zyje).</summary>
        private static string Kill(Group g, bool wounded, Result res)
        {
            try
            {
                var mp = MobileParty.MainParty;
                if (g.Hero == null)
                {
                    var r = mp.PrisonRoster;
                    r.AddToCounts(g.Troop, -1, false, wounded ? -1 : 0);
                    PeopleBook.DieShares(PeopleBook.Take(r, 1, PeopleBook.FDieExec), PeopleBook.FDieExec);
                    res.Died = true; _dDeadMen++; _sum[4]++;
                    return "TAK (zwykly, ksiega ludzi straceni 1)";
                }
                if (!g.Hero.CanDie(KillCharacterAction.KillCharacterActionDetail.Murdered)) { res.Blocked = true; _dBlocked++; return "zablokowana (CanHeroDie)"; }
                KillCharacterAction.ApplyByMurder(g.Hero, Hero.MainHero, true);
                bool dead = !g.Hero.IsAlive;
                if (dead) { res.Died = true; _dDeadHero++; _sum[4]++; }
                else { res.Blocked = true; _dBlocked++; }
                return "TAK (bohater, zyje " + (dead ? "NIE" : "TAK") + ")";
            }
            catch (Exception e) { Stumble("Kill", e); return "BLAD"; }
        }

        /// <summary>Smierc jenca: -2 relacji z kazdym bohaterem druzyny gracza (towarzysz albo rod) z cecha Mercy >= 1. Zwraca ilu.</summary>
        private static int MercifulFrown(List<string> names)
        {
            int n = 0;
            var mp = MobileParty.MainParty;
            var r = mp != null ? mp.MemberRoster : null;
            if (r == null) return 0;
            var heroes = new List<Hero>();
            for (int i = 0; i < r.Count; i++)
            {
                var ch = r.GetCharacterAtIndex(i);
                var h = ch != null && ch.IsHero ? ch.HeroObject : null;
                if (h == null || h == Hero.MainHero || !h.IsAlive) continue;
                if (h.GetTraitLevel(DefaultTraits.Mercy) >= 1) heroes.Add(h);
            }
            foreach (var h in heroes)
            {
                ChangeRelationAction.ApplyPlayerRelation(h, RelationDeath, false, true);
                n++;
                if (names.Count < 5) names.Add(h.Name.ToString());
            }
            return n;
        }

        /// <summary>Tekst okna wyniku (EN).</summary>
        private static string Story(Group g, bool torture, Result r, Band mem)
        {
            string mark = r.Flag ? " It is marked on your map." : " It now shows on your map.";
            string lair = "";
            if (r.Talked)
            {
                if (r.Source == "bohater") lair = "their lair lies " + r.Where + " - the den his band holds.";
                else if (r.Source == "banda") lair = "their lair lies " + r.Where + " - his own band's den.";
                else if (mem != null && mem.Hideout.Length == 0) lair = "his band had no lair of its own, but he names one of his people's: " + r.Where + ".";
                else lair = "he names a lair of his people: " + r.Where + ".";
            }
            if (!torture) return r.Talked ? "The man talks. " + Cap(lair) + mark : "He spits and holds his tongue. Perhaps tomorrow.";
            if (r.Talked) return (r.Died ? "Under the irons he screams out where their lair is - " + lair + " He dies before the night is out." : "Under the irons he breaks: " + lair) + mark;
            return r.Died ? "He dies under the irons without a word." : "He takes the pain and tells you nothing.";
        }

        private static string Cap(string s) { return string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1); }

        // ------------------------------------------------------------ menu i okna
        internal static bool Can(out string why)
        {
            why = null;
            var mp = MobileParty.MainParty;
            if (Campaign.Current == null || mp == null || Hero.MainHero == null) why = "No campaign is running.";
            else if (mp.MapEvent != null || mp.SiegeEvent != null || PlayerEncounter.Current != null) why = "Not while in a battle or siege.";
            else if (mp.CurrentSettlement != null) why = "Leave the settlement first.";
            else if (mp.IsCurrentlyAtSea || mp.IsTransitionInProgress) why = "Only on land.";
            else if (!Hero.MainHero.IsAlive || Hero.MainHero.IsPrisoner) why = "Not now.";
            return why == null;
        }

        private static void AddMenus(CampaignGameStarter starter)
        {
            try
            {
                starter.AddGameMenuOption("arm_camp_wait", "arm_camp_question", "{=!}Question the prisoners", Cond, delegate (MenuCallbackArgs a) { OpenSafe(); }, false, 4);
            }
            catch (Exception e) { Stumble("AddMenus", e); }
        }

        private static bool Cond(MenuCallbackArgs a)
        {
            try
            {
                if (!On) return false;
                a.optionLeaveType = GameMenuOption.LeaveType.Conversation;
                string why;
                if (!Can(out why)) { a.IsEnabled = false; a.Tooltip = new TextObject("{=!}" + why); return true; }
                if (Groups().Count == 0) { a.IsEnabled = false; a.Tooltip = new TextObject("{=!}You hold no outlaws to question."); return true; }
                var p = Persuasion.Measure(Hero.MainHero, null);
                a.Tooltip = new TextObject("{=!}Have the captive outlaws brought before you and learn where their lair is. Your persuasion: " + p.Total + " (" + Persuasion.Words(p) + ").");
                return true;
            }
            catch (Exception e) { Stumble("Cond", e); return false; }
        }

        private static void OpenSafe() { try { OpenWindow(); } catch (Exception e) { Stumble("Open", e); } }

        private static string Title(Group g)
        {
            if (g.Hero != null) return g.Name + (g.Wounded > 0 ? " (outlaw chief, wounded)" : " (outlaw chief)");
            int n = g.Healthy + g.Wounded;
            return g.Name + " (" + n + (g.Wounded > 0 ? ", " + g.Wounded + " wounded" : "") + ")";
        }

        private static string LairHint(string source, Band mem)
        {
            if (source == "bohater") return "He can lead you to the lair his band holds.";
            if (source == "banda") return "He knows his own band's den.";
            if (mem != null && mem.Hideout.Length == 0) return "His band had no lair of its own, but he knows one of his people's.";
            return "He may know a lair of his people.";
        }

        /// <summary>Okno 1: grupy jencow-bandytow (wiersz szary - brak kryjowki do wskazania albo oba sposoby dzis zuzyte).</summary>
        private static void OpenWindow()
        {
            string why;
            if (!Can(out why)) { Log.Player(why, true); return; }
            var gs = Groups();
            if (gs.Count == 0) { Log.Player("You hold no outlaws to question.", true); return; }
            var me = Hero.MainHero;
            int today = Today;
            var list = new List<InquiryElement>(gs.Count);
            foreach (var g in gs)
            {
                string src, w, wl; Band mem;
                var lair = Target(g, out src, out mem, out w, out wl);
                var p = Persuasion.Measure(me, g.PersonCulture);
                var ot = Chance(g, p, false, mem);
                var oo = Chance(g, p, true, mem);
                bool fg; float dc = DeathChance(oo.PickWounded, out fg);
                bool tu = Used(g.Key, 0, today), ru = Used(g.Key, 1, today);
                var hint = new List<string>(4);
                if (lair == null) hint.Add(w);
                else
                {
                    hint.Add(LairHint(src, mem));
                    hint.Add(tu ? "Questioned today." : "Talk him round: " + Pct(ot.Chance) + ".");
                    hint.Add(ru ? "Put to the question today." : "Put him to the question: " + Pct(oo.Chance) + ", he may die (" + Pct(dc) + ").");
                }
                ImageIdentifier img = null;
                try { img = new CharacterImageIdentifier(CharacterCode.CreateFrom(g.Troop)); } catch { img = null; }
                list.Add(new InquiryElement(g, Title(g), img, lair != null && !(tu && ru), string.Join("\n", hint.ToArray())));
            }
            var mine = Persuasion.Measure(me, null);
            string head = "Your persuasion: " + mine.Total + " (" + Persuasion.Words(mine) + "). Each group once a day to talk, once to put to the question.";
            MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData("Question the prisoners", head, list, true, 1, 1, "Choose", "Leave",
                delegate (List<InquiryElement> picked)
                {
                    try { if (picked != null && picked.Count > 0) Pick(picked[0].Identifier as Group); }
                    catch (Exception e) { Stumble("Pick", e); }
                }, null), false);
        }

        /// <summary>Okno 2: rozmowa albo tortury (szansa, ryzyko smierci, kary); "Back" - okno 1 od nowa.</summary>
        private static void Pick(Group g0)
        {
            var g = g0 != null ? Find(g0.Key) : null;
            if (g == null) { OpenSafe(); return; }
            var me = Hero.MainHero;
            string src, w, wl; Band mem;
            var lair = Target(g, out src, out mem, out w, out wl);
            if (lair == null) { InformationManager.ShowInquiry(new InquiryData(g.Name, w, true, false, "Back", null, delegate { OpenSafe(); }, null), true); return; }
            var p = Persuasion.Measure(me, g.PersonCulture);
            var ot = Chance(g, p, false, mem);
            var oo = Chance(g, p, true, mem);
            bool fg; float dc = DeathChance(oo.PickWounded, out fg);
            int today = Today;
            bool tu = Used(g.Key, 0, today), ru = Used(g.Key, 1, today);
            var parts = new List<string> { "your persuasion " + p.Total + " (" + Persuasion.Words(p) + ")" };
            if (ot.Wounded > 0f) parts.Add("a wounded man breaks sooner");
            if (ot.ChainsDays > 0) parts.Add(Math.Min(ChainsDaysMax, ot.ChainsDays) + (ot.ChainsDays == 1 ? " day" : " days") + " in chains");
            if (ot.Tier > 0f) parts.Add("a hardened tier " + g.Tier + " outlaw holds out");
            if (ot.Hero > 0f) parts.Add("a chief holds out longest");
            string talkHint = "Talk to him, promise and threaten: " + string.Join(", ", parts.ToArray()) + ". Charm grows with it.";
            string tortHint = "Far more likely to talk (your Roguery" + (oo.Scarface > 0f ? " and Scarface" : "") + " help), but he may die under it"
                              + (fg ? " (Forgivable Grievances spares some)" : "") + ". It costs you Mercy, a death more; merciful companions in your party think less of you and the tale gets about the inns.";
            var rows = new List<InquiryElement>
            {
                new InquiryElement("talk", (tu ? "Talk him round (done today)" : "Talk him round (" + Pct(ot.Chance) + ")"), null, !tu, talkHint),
                new InquiryElement("torture", (ru ? "Put him to the question (done today)" : "Put him to the question (" + Pct(oo.Chance) + ", may die " + Pct(dc) + ")"), null, !ru, tortHint)
            };
            MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData("Question the prisoners: " + g.Name, LairHint(src, mem), rows, true, 1, 1, "Proceed", "Back",
                delegate (List<InquiryElement> picked)
                {
                    try
                    {
                        if (picked == null || picked.Count == 0) return;
                        bool torture = (picked[0].Identifier as string) == "torture";
                        var r = Act(g, torture);
                        ShowResult(g, r);
                    }
                    catch (Exception e) { Stumble("Proceed", e); }
                },
                delegate (List<InquiryElement> x) { OpenSafe(); }), false);
        }

        private static void ShowResult(Group g, Result r)
        {
            string text = r.Refused ? (r.RefuseWhy == "limit" ? "He has had enough for today - try again tomorrow." : r.RefuseWhy == "brak grupy" ? "He is no longer in your keeping." : r.RefuseWhy) : r.Text;
            bool more = false;
            try { string why; more = Can(out why) && Groups().Count > 0; } catch { more = false; }
            InformationManager.ShowInquiry(new InquiryData("Question the prisoners", text ?? "", true, more, "Done", "Question others", null, delegate { OpenSafe(); }), true);
        }

        // ------------------------------------------------------------ doba, start sesji
        private static void Daily()
        {
            int today = Today;
            _sessionDays++;
            try
            {
                _bands.RemoveAll(b => today - b.Day > MemoryDays);
                var old = new List<string>();
                foreach (var kv in _used) if (kv.Value[0] < today && kv.Value[1] < today) old.Add(kv.Key);
                foreach (var k in old) _used.Remove(k);
            }
            catch (Exception e) { Stumble("Prune", e); }
            try { TestStep(today); } catch (Exception e) { Stumble("Test", e); }
            bool act = _dTalk + _dTort + _dRefused + _dMemNew + _dStumbles > 0;
            if (On || act)
            {
                int gr, men, he;
                Count(out gr, out men, out he);
                Log.Info(P + "doba " + today + " | rozmowy " + _dTalk + " (mowili " + _dTalkYes + "), tortury " + _dTort + " (mowili " + _dTortYes + ", zmarli " + (_dDeadMen + _dDeadHero)
                         + ": zwykli " + _dDeadMen + ", bohaterowie " + _dDeadHero + ", zablokowane " + _dBlocked + "), odmowy " + _dRefused
                         + " | ujawnione kryjowki " + (_dRevHero + _dRevBand + _dRevKin) + " (bohater " + _dRevHero + ", banda " + _dRevBand + ", kultura " + _dRevKin + ")"
                         + " | pamiec band " + _bands.Count + " (nowe dzis " + _dMemNew + ") | jency-bandyci grup " + gr + ", ludzi " + men + ", bohaterow " + he
                         + " | potkniecia dzis " + _dStumbles + ".");
            }
            DayReset();
        }

        private static void SessionStart()
        {
            var s = Settings.Current;
            var me = Hero.MainHero;
            var own = Persuasion.Measure(me, me != null ? me.Culture : null);
            CultureObject foreign = null;
            foreach (var c in Clan.BanditFactions) if (c != null && c.Culture != null && (me == null || c.Culture != me.Culture)) { foreign = c.Culture; break; }
            var other = Persuasion.Measure(me, foreign);
            int rog = 0; bool scar = false;
            try { if (me != null) { rog = me.GetSkillValue(DefaultSkills.Roguery); scar = me.GetPerkValue(DefaultPerks.Roguery.Scarface); } } catch { }
            int gr, men, he;
            Count(out gr, out men, out he);
            int held = 0, spotted = 0;
            var kinds = new HashSet<CultureObject>();
            foreach (var h in Hideout.All)
            {
                var st = h != null ? h.Settlement : null;
                if (st == null) continue;
                if (st.Culture != null) kinds.Add(st.Culture);
                if (h.IsInfested) { held++; if (h.IsSpotted) spotted++; }
            }
            Log.Info(P + "start sesji | klucze: Camp Interrogation " + (On ? "tak" : "NIE") + ", Gossip Per Drink By Persuasion " + (s != null && s.GossipPerDrinkByPersuasion ? "tak" : "NIE")
                     + " (Gossip Per Drink By Charm " + (s != null && s.GossipPerDrinkByCharm ? "tak" : "NIE") + "), Inn Gossip " + (Rumours.On ? "tak" : "NIE")
                     + " | miara perswazji wobec swoich: " + Persuasion.LogText(own) + " ; wobec obcych: " + Persuasion.LogText(other)
                     + " | grozba: Lotrostwo " + rog + ", Scarface " + (scar ? "TAK" : "NIE") + ", Forgivable Grievances " + (Grievances() ? "TAK" : "NIE")
                     + " | jency-bandyci: grup " + gr + " (ludzi " + men + ", bohaterow " + he + ")"
                     + " | kryjowki: zajete " + held + ", wypatrzone " + spotted + ", kultur z kryjowkami " + kinds.Count
                     + " | pamiec band " + _bands.Count + " | zapis: " + _importNote + " | potkniecia " + _stumbles + ".");
        }

        // ------------------------------------------------------------ zapis "arm_interrogation"
        /// <summary>"1|doba,kultura,kryjowka,x,y,banda;...|klucz,dobaR,dobaT;...|rozmowy,mowili,tortury,mowili,zmarli,ujawnione" (ponizej 4 KB);
        /// klucz wylaczony i stan pusty - "".</summary>
        internal static string Export()
        {
            try
            {
                if (!On && _bands.Count == 0 && _used.Count == 0) return "";
                return Write(_bands, _used, _sum);
            }
            catch (Exception e) { Stumble("Export", e); return ""; }
        }

        private static string Write(List<Band> bands, Dictionary<string, int[]> used, int[] sum)
        {
            var sb = new StringBuilder("1|");
            for (int i = 0; i < bands.Count; i++)
            {
                var b = bands[i];
                if (i > 0) sb.Append(';');
                sb.Append(b.Day.ToString(Inv)).Append(',').Append(b.Culture).Append(',').Append(b.Hideout).Append(',').Append(b.X.ToString("0.0", Inv)).Append(',')
                  .Append(b.Y.ToString("0.0", Inv)).Append(',').Append(b.Party);
            }
            sb.Append('|');
            int n = 0;
            foreach (var kv in used)
            {
                if (n++ > 0) sb.Append(';');
                sb.Append(kv.Key).Append(',').Append(kv.Value[0].ToString(Inv)).Append(',').Append(kv.Value[1].ToString(Inv));
            }
            sb.Append('|');
            for (int i = 0; i < sum.Length; i++) { if (i > 0) sb.Append(','); sb.Append(sum[i].ToString(Inv)); }
            return sb.ToString();
        }

        /// <summary>Odczyt napisu zapisu do podanych kontenerow (Import i proba testu). Zle wpisy pominiete i policzone; false - nieznany format.</summary>
        private static bool Parse(string data, List<Band> bands, Dictionary<string, int[]> used, int[] sum, out int bad)
        {
            bad = 0;
            var parts = data.Split('|');
            if (parts.Length < 4 || parts[0] != "1") return false;
            foreach (var e in parts[1].Split(';'))
            {
                if (e.Length == 0) continue;
                var a = e.Split(',');
                int day; float x, y;
                if (a.Length < 6 || !int.TryParse(a[0], NumberStyles.Integer, Inv, out day) || a[1].Length == 0
                    || !float.TryParse(a[3], NumberStyles.Float, Inv, out x) || !float.TryParse(a[4], NumberStyles.Float, Inv, out y)) { bad++; continue; }
                if (bands.Count < MemoryMax) bands.Add(new Band { Day = day, Culture = a[1], Hideout = a[2], X = x, Y = y, Party = a[5] });
            }
            foreach (var e in parts[2].Split(';'))
            {
                if (e.Length == 0) continue;
                var a = e.Split(',');
                int r, t;
                if (a.Length < 3 || a[0].Length == 0 || !int.TryParse(a[1], NumberStyles.Integer, Inv, out r) || !int.TryParse(a[2], NumberStyles.Integer, Inv, out t)) { bad++; continue; }
                used[a[0]] = new[] { r, t };
            }
            var s = parts[3].Split(',');
            for (int i = 0; i < sum.Length && i < s.Length; i++) { int v; if (int.TryParse(s[i], NumberStyles.Integer, Inv, out v)) sum[i] = Math.Max(0, v); else bad++; }
            return true;
        }

        internal static void Import(string data)
        {
            _bands.Clear(); _used.Clear(); Array.Clear(_sum, 0, _sum.Length);
            TestReset();
            if (string.IsNullOrEmpty(data)) { _importNote = "brak klucza (stary zapis albo nowa kampania)"; return; }
            try
            {
                int bad;
                if (!Parse(data, _bands, _used, _sum, out bad)) { _bands.Clear(); _used.Clear(); Array.Clear(_sum, 0, _sum.Length); _importNote = "nieznany format - pominiety"; return; }
                _importNote = "pamiec " + _bands.Count + ", uzycia " + _used.Count + ", sumy kampanii: rozmowy " + _sum[0] + " (mowili " + _sum[1] + "), tortury " + _sum[2] + " (mowili " + _sum[3]
                              + "), zmarli " + _sum[4] + ", ujawnione " + _sum[5] + (bad > 0 ? ", zlych wpisow " + bad : "");
            }
            catch (Exception e) { _bands.Clear(); _used.Clear(); Array.Clear(_sum, 0, _sum.Length); _importNote = "blad odczytu - pominiety"; Stumble("Import", e); }
        }
    }
}
