using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>405 (PLAN-396 commit 405, Jeff 11.11 "rob 5" - pogrzeby i sukcesja) - POGRZEBY, RDZEN: wykrycie smierci glowy waznego rodu, wiersz pogrzebu,
    /// tryby, zapis, wiesc i dwie opcje gracza w siedzibie. BEZ wplywu na AI (pokoj pogrzebu = 406, zjazd rodu = 407, spor o dziedzictwo = 410). Zero latek.
    ///
    /// WYKRYCIE (KillCharacterAction.ApplyInternal, cs_full.cs:227128): gra wola BeforeHeroKilled, potem wybiera nowa glowe (ChangeClanLeaderAction ->
    /// OnClanLeaderChanged), ewentualnie niszczy rod (DestroyClanAction.ApplyByClanLeaderDeath), na koncu HeroKilled. Glowa = ofiara spelnia ktorykolwiek warunek:
    ///  (a) "przed": w BeforeHeroKilledEvent clan.Leader == victim (wtedy tez migawka linii dziedziczenia BK - TitleModel.CalculateInheritanceLine(clan, victim, 4),
    ///      odbiciem, sam odczyt: bk.cs:50315);
    ///  (b) "zmiana glowy": OnClanLeaderChangedEvent z ostatnich 24 h ze stara glowa = ofiara (droga ROT: wydarzenie ustawia glowe PRZED zabojstwem);
    ///  (c) "migawka": migawka doby rod -> glowa (niezalezna od kolejnosci sluchaczy; BK przy tytule suwerena zmienia glowe golym Clan.SetLeader bez zdarzenia).
    /// MbEvent dopisuje sluchacza NA POCZATEK listy (cs_full.cs:22885) - kto rejestruje pozniej, biegnie pierwszy; Armoury laduje sie po BK, wiec (a) dziala.
    /// Linia startu pisze "sluchacz przed BK TAK/NIE" z faktycznej kolejnosci listy.
    /// Pogrzeb powstaje w HeroKilledEvent (smierc odroczona znakiem smierci liczy sie od faktycznego zgonu). Dziedzic = clan.Leader PO smierci.
    ///
    /// POMIJAMY: detail Lost, rod gracza, bandyci, rody mniejsze / buntownicze / najemne, kultury nightswatch, freefolk, wildlings, Inni (whitewalker).
    /// Wazny rod: IsNoble i (Tier >= 4 albo panujacy) - regula Kroniki (Chronicle.Recurring). Siedziba: HomeSettlement, jesli to twierdza rodu, inaczej najlepsze lenno.
    /// TRYBY: PELNY | CICHY (siedziba oblezona; limit swiata / krolestwa; kanon - klucz Funeral Canon Quiet) | TYLKO WIESC (rod bez twierdzy albo zniszczony - bez wiersza).
    /// Limity: Funeral Max Active pelnych na swiecie, 1 pelny na krolestwo; wladca wypiera pelny pogrzeb zwyklego rodu zaczety tej samej doby. Druga smierc w rodzie
    /// w oknie = wspolny pogrzeb (okno bez przedluzenia). Kalendarz: D..D+4 zjazd, obrzed D+4, koniec D+5 (Funeral Days 5); wiersz wygasa po dacie - bez softlocka.
    ///
    /// GRACZ (Funeral Player Attend): wiesc, gdy jest w tym krolestwie / ma relacje >= 10 ze zmarlym lub dziedzicem / jest w zasiegu 120 od siedziby. Opcje w menu
    /// "town" i "castle" siedziby (przed "Leave"): "Attend the funeral of {NAME}" (+3 dziedzic, +1 kazdy lord rodu na miejscu; od razu, bez menu oczekiwania i bez
    /// przelaczania menu) i "Give {GOLD} denars for the rites" (GiveGoldAction gracz -> dziedzic, +2, raz). AI nic nie placi i nic nie dostaje - jedyny przeplyw zlota.
    ///
    /// Nikogo nie zabijamy i nie okaleczamy (StoryGuard pytany tylko do linii: "dziedzic pod straza fabuly"). Wyjatki per zdarzenie liczone (potkniecia), nic sie nie
    /// gasi. Zapis arm_funeral "v1" (stary zapis = pusto). Reset: konstruktor ArmouryBehavior i OnGameEnd.
    /// 406: pokoj pogrzebu (FuneralRites.Ai.cs) - migawka z wierszy PELNYCH, liczniki rozejmu w wierszu (tylko sesja, nie w zapisie).
    /// 407: zjazd rodu (FuneralRites.Ai.cs) - jedyna latka: postfiks 130 CampaignEventDispatcher.AiHourlyTick (w kampanii, raz na proces).
    /// 410: sporna sukcesja i zaloba krolestwa dla 312c (FuneralRites.Succession.cs) - ocena sporu w Head po utworzeniu wiersza, pytanie do gracza po
    /// "Attend the funeral", wiersze "S" / "K" w tym samym zapisie; Pre.Rot / Chg.Rot = wydarzenie ROT na stosie sluchacza.</summary>
    internal static partial class FuneralRites
    {
        internal const string Tag = "Pogrzeby (P5)";
        internal const int Full = 0, Quiet = 1;
        private const float NewsRange = 120f;   // zasieg wiesci (jednostki mapy)
        private const int NewsRelation = 10;    // relacja gracza ze zmarlym / dziedzicem, od ktorej wiesc dociera
        private const string OptAttendTown = "arm405_funeral_attend_town", OptAttendCastle = "arm405_funeral_attend_castle";
        private const string OptGiftTown = "arm405_funeral_gift_town", OptGiftCastle = "arm405_funeral_gift_castle";
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static readonly HashSet<string> SkipCultures = new HashSet<string>(StringComparer.Ordinal) { "nightswatch", "freefolk", "wildlings", "whitewalker" };

        internal sealed class Row
        {
            public string Clan, Dead, Name, Heir, Seat, Kingdom, Why, Line, Others;   // rod, zmarly (id, imie), dziedzic, siedziba, krolestwo w chwili smierci, powod trybu, linia BK, inni zmarli
            public int Day, Rite, End, Mode, Gift;                                    // doba smierci, obrzedu, konca; tryb; dar gracza
            public bool King, Attended;                                               // glowa rodu panujacego; gracz byl
            public int TZero, TBk, TPlayer, TPlayerDay;                               // 406 (tylko sesja): wyzerowane cele AI (Interlocked), weta BK, doby z zablokowana opcja gracza
            public bool TSaid, TBroken;                                               // 406 (tylko sesja): linia pokoju napisana; siedziba oblegana mimo pokoju
            public int GOrders;                                                       // 407 (tylko sesja): rozkazy zjazdu (ocena GoToSettlement siedziby podniesiona / dodana)
        }

        private sealed class Pre
        {
            public string Clan, Kingdom, Line;
            public bool Leader, Ruling, Important, Rot;   // 410: Rot = zgon glowy z wydarzenia ROT (ROT.Events na stosie)
        }

        private sealed class Chg { public string Clan; public double Hour; public bool Rot; }   // 410: Rot = glowe zmienilo wydarzenie ROT

        private static readonly Dictionary<string, Row> _active = new Dictionary<string, Row>(StringComparer.Ordinal);       // rod -> pogrzeb (pelny albo cichy)
        private static readonly Dictionary<string, Pre> _pre = new Dictionary<string, Pre>(StringComparer.Ordinal);          // ofiara -> stan sprzed zmiany glowy (tylko sesja)
        private static readonly Dictionary<string, Chg> _changed = new Dictionary<string, Chg>(StringComparer.Ordinal);      // stara glowa -> rod i godzina zmiany (tylko sesja)
        private static readonly Dictionary<string, string> _leaderOf = new Dictionary<string, string>(StringComparer.Ordinal); // migawka doby: rod -> glowa (tylko sesja)
        private static readonly List<string> _tmp = new List<string>();
        private static readonly HashSet<string> _err = new HashSet<string>(StringComparer.Ordinal);
        private static int _stumbles, _nPre, _nChg, _nSnap, _nFull, _nQuiet, _nWord, _nJoint;
        private static string _hookMenu = "-";
        // BK (odbicie, raz na proces): BannerKingsConfig.Instance.TitleModel.CalculateInheritanceLine(Clan, Hero, int)
        private static bool _bkTried, _bkOk;
        private static PropertyInfo _bkInstance, _bkTitleModel;
        private static MethodInfo _bkLine;

        // ---------------------------------------------------------------- cykl zycia

        internal static void Reset()
        {
            _active.Clear(); _pre.Clear(); _changed.Clear(); _leaderOf.Clear(); _tmp.Clear(); _err.Clear();
            _stumbles = _nPre = _nChg = _nSnap = _nFull = _nQuiet = _nWord = _nJoint = 0;
            TruceReset();   // 406
            SuccReset();    // 410
        }

        private static void Stumble(string where, Exception e)
        {
            System.Threading.Interlocked.Increment(ref _stumbles);   // 406: takze z watkow AI (ocena celu)
            try { bool first; lock (_err) first = _err.Add(where); if (first) Log.Error("FuneralRites." + where, e); } catch { }
        }

        private static bool KeyOn { get { var s = Settings.Current; return s != null && s.Funerals; } }
        private static bool LogOn { get { var s = Settings.Current; return s != null && s.FuneralLog; } }
        private static int Days { get { var s = Settings.Current; int d = s != null ? s.FuneralDays : 5; return d < 2 ? 2 : (d > 14 ? 14 : d); } }
        private static int MaxActive { get { var s = Settings.Current; int m = s != null ? s.FuneralMaxActive : 3; return m < 1 ? 1 : m; } }
        private static string YN(bool b) { return b ? "TAK" : "NIE"; }
        private static int Today() { return (int)CampaignTime.Now.ToDays; }
        private static string Nm(Hero h) { try { return h != null && h.Name != null ? h.Name.ToString() : "?"; } catch { return "?"; } }
        private static string Nm(Clan c) { try { return c != null && c.Name != null ? c.Name.ToString() : "?"; } catch { return "?"; } }
        private static string Nm(Settlement s) { try { return s != null && s.Name != null ? s.Name.ToString() : "?"; } catch { return "?"; } }
        private static string Clean(string t) { return string.IsNullOrEmpty(t) ? "" : t.Replace('|', '/').Replace('\n', ' ').Replace('\r', ' '); }
        private static void Info(string line) { if (LogOn) Log.Info(Tag + ": " + line); }

        private static Clan FindClan(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            try { foreach (var c in Clan.All) if (c != null && c.StringId == id) return c; } catch { }
            return null;
        }

        // ---------------------------------------------------------------- kto i gdzie

        /// <summary>Rod, ktorego smierc glowy w ogole nas obchodzi (bez progu waznosci).</summary>
        private static bool Eligible(Clan cl)
        {
            if (cl == null || cl == Clan.PlayerClan || cl.IsBanditFaction || cl.IsMinorFaction || cl.IsRebelClan || cl.IsClanTypeMercenary || !cl.IsNoble) return false;
            if (cl.Culture != null && SkipCultures.Contains(cl.Culture.StringId)) return false;
            var k = cl.Kingdom;
            if (k != null && k.Culture != null && SkipCultures.Contains(k.Culture.StringId)) return false;
            return true;
        }

        private static bool Ruling(Clan cl) { return cl != null && cl.Kingdom != null && cl.Kingdom.RulingClan == cl; }

        /// <summary>Siedziba: HomeSettlement, jesli to twierdza tego rodu; inaczej najlepsze lenno (miasto przed zamkiem, wyzsza zamoznosc). null = rod bez twierdzy.</summary>
        private static Settlement SeatOf(Clan cl)
        {
            var home = cl.HomeSettlement;
            if (home != null && home.IsFortification && home.OwnerClan == cl) return home;
            Settlement best = null; float bestScore = float.MinValue;
            foreach (var t in cl.Fiefs)
            {
                var st = t != null ? t.Settlement : null;
                if (st == null || !st.IsFortification || st.OwnerClan != cl) continue;
                float score = (st.IsTown ? 1000000f : 0f) + t.Prosperity;
                if (score > bestScore) { bestScore = score; best = st; }
            }
            return best;
        }

        private static int FullCount(string kingdom, out Row inKingdom)
        {
            int n = 0; inKingdom = null;
            foreach (var r in _active.Values)
            {
                if (r.Mode != Full) continue;
                n++;
                if (!string.IsNullOrEmpty(kingdom) && r.Kingdom == kingdom && inKingdom == null) inKingdom = r;
            }
            return n;
        }

        /// <summary>Tryb cichy z kanonu (klucz Funeral Canon Quiet): cale Westeros do konca kanonu (Krwawe Gody), Polnoc i jej sojusznicy do Bitwy Bekartow
        /// (albo wyjscia fazy 2 - ta sama bramka co 312c CanonThroneWars).</summary>
        private static string CanonQuiet(Kingdom k)
        {
            var s = Settings.Current;
            if (s == null || !s.FuneralCanonQuiet || !Prologue.CanonHere) return null;
            if (!CanonWarClock.OverNow()) return "kanon (przed koncem Krwawych Godow)";
            if (k != null && CanonThroneWars.NorthSide(k) && Prologue.RotEventState("BastardsSiegeEvent") != 2 && KingInTheNorth.FightsOnEnd == null)
                return "kanon (Polnoc przed Bitwa Bekartow)";
            return null;
        }

        // ---------------------------------------------------------------- sluchacze

        /// <summary>CampaignEvents.BeforeHeroKilledEvent: stan sprzed wyboru dziedzica. Tani: rod poza regula = wyjscie po kilku porownaniach.</summary>
        internal static void OnBeforeKilled(Hero victim, Hero killer, KillCharacterAction.KillCharacterActionDetail detail, bool notify)
        {
            try
            {
                if (victim == null || detail == KillCharacterAction.KillCharacterActionDetail.Lost || !KeyOn || victim == Hero.MainHero || !victim.IsLord) return;
                var cl = victim.Clan;
                if (!Eligible(cl) || victim.StringId == null) return;
                var p = new Pre { Clan = cl.StringId, Kingdom = cl.Kingdom != null ? cl.Kingdom.StringId : "", Leader = cl.Leader == victim, Ruling = Ruling(cl) };
                p.Important = cl.Tier >= 4 || p.Ruling;
                if (p.Leader && p.Important) { p.Line = BkLine(cl, victim); p.Rot = RotOnStack(); }   // 410: stos raz na zgon glowy waznego rodu
                _pre[victim.StringId] = p;
            }
            catch (Exception e) { Stumble("OnBeforeKilled", e); }
        }

        /// <summary>CampaignEvents.OnClanLeaderChangedEvent: stara glowa zapamietana na 24 h (droga ROT - glowa zmieniona przed zabojstwem).</summary>
        internal static void OnLeaderChanged(Hero oldLeader, Hero newLeader)
        {
            try
            {
                if (oldLeader == null || oldLeader.StringId == null || !KeyOn) return;
                var cl = oldLeader.Clan ?? (newLeader != null ? newLeader.Clan : null);
                if (cl == null || cl == Clan.PlayerClan) return;
                _changed[oldLeader.StringId] = new Chg { Clan = cl.StringId, Hour = CampaignTime.Now.ToHours, Rot = RotOnStack() };   // 410: stos raz na zmiane glowy
            }
            catch (Exception e) { Stumble("OnLeaderChanged", e); }
        }

        /// <summary>CampaignEvents.HeroKilledEvent: faktyczny zgon - pogrzeb glowy albo dopisanie do trwajacego pogrzebu rodu.</summary>
        internal static void OnKilled(Hero victim, Hero killer, KillCharacterAction.KillCharacterActionDetail detail, bool notify)
        {
            try
            {
                if (victim == null || victim.StringId == null) return;
                Pre pre; Chg chg;
                _pre.TryGetValue(victim.StringId, out pre); _pre.Remove(victim.StringId);
                _changed.TryGetValue(victim.StringId, out chg); _changed.Remove(victim.StringId);
                if (!KeyOn || detail == KillCharacterAction.KillCharacterActionDetail.Lost || victim == Hero.MainHero || !victim.IsLord) return;
                var cl = victim.Clan;
                if (cl == null || cl.StringId == null) return;
                string snap;
                bool a = pre != null && pre.Leader && pre.Clan == cl.StringId;
                bool b = chg != null && chg.Clan == cl.StringId && CampaignTime.Now.ToHours - chg.Hour <= 24.0;
                bool c = _leaderOf.TryGetValue(cl.StringId, out snap) && snap == victim.StringId;
                _leaderOf[cl.StringId] = cl.Leader != null && cl.Leader.IsAlive ? cl.Leader.StringId : "";   // migawka po kazdym zgonie w rodzie
                if (!Eligible(cl)) return;
                if (!(a || b || c)) { Joint(cl, victim, false); return; }
                if (a) _nPre++; else if (b) _nChg++; else _nSnap++;
                bool ruling = pre != null ? pre.Ruling : Ruling(cl);
                bool important = pre != null ? pre.Important : (cl.Tier >= 4 || ruling);
                if (!important) return;
                bool rot = (pre != null && pre.Rot) || (chg != null && chg.Clan == cl.StringId && chg.Rot);   // 410: glowa / zgon z wydarzenia ROT - bez sporu
                Head(cl, victim, detail, a ? "przed" : (b ? "zmiana glowy" : "migawka"), ruling, pre != null ? pre.Kingdom : (cl.Kingdom != null ? cl.Kingdom.StringId : ""), pre != null ? pre.Line : null, rot);
            }
            catch (Exception e) { Stumble("OnKilled", e); }
        }

        /// <summary>Druga smierc w rodzie w oknie pogrzebu = wspolny pogrzeb (okno bez przedluzenia). true = dopisano.</summary>
        private static bool Joint(Clan cl, Hero victim, bool head)
        {
            Row r;
            if (!_active.TryGetValue(cl.StringId, out r) || Today() > r.End) return false;
            string nm = Clean(Nm(victim)).Replace(';', ',');
            r.Others = string.IsNullOrEmpty(r.Others) ? nm : r.Others + ";" + nm;
            if (head && !cl.IsEliminated && cl.Leader != null && cl.Leader.IsAlive) r.Heir = cl.Leader.StringId;
            _nJoint++;
            Info("WSPOLNY POGRZEB rodu " + cl.StringId + ": " + Nm(victim) + " (" + victim.StringId + ")" + (head ? " - kolejna glowa rodu, dziedzic teraz " + (r.Heir ?? "-") : "") + " dopisany do pogrzebu " + r.Name + " (obrzed dnia " + r.Rite + ", bez przedluzenia).");
            return true;
        }

        private static void Head(Clan cl, Hero victim, KillCharacterAction.KillCharacterActionDetail detail, string how, bool ruling, string kingdomId, string line, bool rot)
        {
            int today = Today();
            Hero heir = !cl.IsEliminated && cl.Leader != null && cl.Leader != victim && cl.Leader.IsAlive ? cl.Leader : null;
            if (ruling) KingDied(kingdomId, victim);   // 410: zaloba krolestwa (312c) - kazdy tryb pogrzebu, takze sama wiesc i wspolny pogrzeb
            if (Joint(cl, victim, true)) return;
            Kingdom kingdom = cl.Kingdom;
            Settlement seat = cl.IsEliminated || heir == null ? null : SeatOf(cl);
            string head = "SMIERC GLOWY " + Nm(victim) + " (" + victim.StringId + ") rodu " + cl.StringId + (ruling ? " [panujacy]" : "") + " | powod " + detail + " | wykrycie: " + how
                          + " | dziedzic " + (heir != null ? Nm(heir) + " (" + heir.StringId + ")" + (GuardedSafe(heir) ? " [pod straza fabuly]" : "") : "-")
                          + (string.IsNullOrEmpty(line) ? "" : " | linia BK: " + line);
            if (seat == null)
            {
                _nWord++;
                Info(head + " | siedziba - | tryb TYLKO WIESC (" + (cl.IsEliminated ? "rod zniszczony" : (heir == null ? "brak dziedzica" : "rod bez twierdzy")) + ").");
                Widow(victim);
                if (TellPlayer(cl, victim, heir, null)) Log.Player(Nm(victim) + " of " + Nm(cl) + " is dead.");
                return;
            }
            var row = new Row
            {
                Clan = cl.StringId, Dead = victim.StringId, Name = Clean(Nm(victim)), Heir = heir.StringId, Seat = seat.StringId, Kingdom = kingdomId ?? "",
                Line = Clean(line), Day = today, Rite = today + Days - 1, End = today + Days, King = ruling, Mode = Full
            };
            string why = null;
            bool siege = false;
            if (seat.IsUnderSiege) { why = "siedziba oblezona"; siege = true; }
            if (why == null) why = CanonQuiet(kingdom);
            if (why == null)
            {
                Row same;
                int full = FullCount(row.Kingdom, out same);
                if (same != null)
                {
                    if (ruling && !same.King && same.Day == today)
                    {
                        same.Mode = Quiet; same.Why = "wyparty przez pogrzeb wladcy";
                        full--;
                        Info("pogrzeb rodu " + same.Clan + " (" + same.Name + ") schodzi na CICHY - wladca " + row.Name + " zmarl tej samej doby (1 pelny na krolestwo).");
                    }
                    else why = "limit krolestwa (trwa pogrzeb rodu " + same.Clan + ")";
                }
                if (why == null && full >= MaxActive) why = "limit swiata (" + full + "/" + MaxActive + ")";
            }
            if (why != null) { row.Mode = Quiet; row.Why = why; _nQuiet++; } else _nFull++;
            _active[cl.StringId] = row;
            Info(head + " | siedziba " + seat.StringId + " | tryb " + (row.Mode == Full ? "PELNY" : "CICHY (" + why + ")") + " | obrzed dnia " + row.Rite + ", koniec " + row.End + ".");
            TruceRebuild();   // 406: pokoj pogrzebu - nowy wiersz pelny (albo wyparty na cichy)
            Widow(victim);
            bool told = TellPlayer(cl, victim, heir, seat);
            if (told)
            {
                if (row.Mode == Full)
                    Log.Player("Word reaches you: " + Nm(victim) + " of " + Nm(cl) + " is dead. The house gathers at " + Nm(seat) + " to bury " + (victim.IsFemale ? "her" : "him")
                               + "; the rites are held in " + (row.Rite - today) + " days.");
                else if (siege) Log.Player(Nm(cl) + " buries its lord in silence - " + Nm(seat) + " is not theirs to gather in.");
                else Log.Player(Nm(victim) + " of " + Nm(cl) + " is dead.");
            }
            SuccessionCheck(cl, victim, heir, row, line, rot, told);   // 410: spor o dziedzictwo - jedna ocena na zgon glowy (po wiesci o smierci)
        }

        private static bool GuardedSafe(Hero h) { try { return StoryGuard.Needed(h); } catch { return false; } }

        /// <summary>Tylko log: malzonek zmarlej glowy (w HeroKilledEvent victim.Spouse jest jeszcze ustawione - cs_full.cs:227241).</summary>
        private static void Widow(Hero victim)
        {
            try
            {
                var sp = victim.Spouse;
                if (sp != null && sp.IsAlive) Info((sp.IsFemale ? "wdowa " : "wdowiec ") + Nm(sp) + " (" + sp.StringId + ") " + (sp.IsFemale ? "wolna" : "wolny") + " do slubu.");
            }
            catch (Exception e) { Stumble("Widow", e); }
        }

        /// <summary>Wiesc dociera do gracza: to samo krolestwo, relacja >= 10 ze zmarlym lub dziedzicem, albo siedziba w zasiegu 120.</summary>
        private static bool TellPlayer(Clan cl, Hero victim, Hero heir, Settlement seat)
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !s.FuneralPlayerAttend || Hero.MainHero == null) return false;
                var pk = Clan.PlayerClan != null ? Clan.PlayerClan.Kingdom : null;
                if (pk != null && cl.Kingdom == pk) return true;
                if (CharacterRelationManager.GetHeroRelation(Hero.MainHero, victim) >= NewsRelation) return true;
                if (heir != null && CharacterRelationManager.GetHeroRelation(Hero.MainHero, heir) >= NewsRelation) return true;
                var mp = MobileParty.MainParty;
                return seat != null && mp != null && mp.GetPosition2D.Distance(seat.GetPosition2D) <= NewsRange;
            }
            catch (Exception e) { Stumble("TellPlayer", e); return false; }
        }

        // ---------------------------------------------------------------- BK: linia dziedziczenia (sam odczyt)

        private static bool BkReady()
        {
            if (_bkTried) return _bkOk;
            _bkTried = true;
            try
            {
                var cfg = AccessTools.TypeByName("BannerKings.BannerKingsConfig");
                _bkInstance = cfg != null ? AccessTools.Property(cfg, "Instance") : null;
                _bkTitleModel = cfg != null ? AccessTools.Property(cfg, "TitleModel") : null;
                var tm = _bkTitleModel != null ? _bkTitleModel.PropertyType : null;   // abstrakcyjna TitleModel - klasa DEKLARUJACA metode (bk.cs:50256, :50315)
                _bkLine = tm != null ? AccessTools.DeclaredMethod(tm, "CalculateInheritanceLine", new[] { typeof(Clan), typeof(Hero), typeof(int) }) : null;
                _bkOk = _bkInstance != null && _bkTitleModel != null && _bkLine != null;
            }
            catch (Exception e) { _bkOk = false; Stumble("BkReady", e); }
            return _bkOk;
        }

        /// <summary>"id,id,.." - do 4 pierwszych z linii dziedziczenia BK dla umierajacej glowy; null = brak BK / blad (liczony).</summary>
        private static string BkLine(Clan cl, Hero victim)
        {
            if (!BkReady()) return null;
            try
            {
                var cfg = _bkInstance.GetValue(null, null);
                var model = cfg != null ? _bkTitleModel.GetValue(cfg, null) : null;
                var seq = model != null ? _bkLine.Invoke(model, new object[] { cl, victim, 4 }) as System.Collections.IEnumerable : null;
                if (seq == null) return null;
                var sb = new StringBuilder(48);
                PropertyInfo key = null;
                foreach (var kv in seq)
                {
                    if (kv == null) continue;
                    if (key == null) key = kv.GetType().GetProperty("Key");
                    var h = key != null ? key.GetValue(kv, null) as Hero : null;
                    if (h == null || h.StringId == null) continue;
                    if (sb.Length > 0) sb.Append(',');
                    sb.Append(h.StringId);
                }
                return sb.ToString();
            }
            catch (Exception e) { Stumble("BkLine", e); return null; }
        }

        /// <summary>Czy nasz sluchacz BeforeHeroKilledEvent stoi na liscie PRZED sluchaczem BKTitleBehavior (lista od poczatku = kolejnosc wywolan).</summary>
        private static string BeforeBk()
        {
            try
            {
                object ev = CampaignEvents.BeforeHeroKilledEvent;
                var f = ev != null ? AccessTools.Field(ev.GetType(), "_nonSerializedListenerList") : null;
                object node = f != null ? f.GetValue(ev) : null;
                if (f == null) return "? (lista niewidoczna)";
                int i = 0, ours = -1, bk = -1;
                while (node != null && i < 4096)
                {
                    var t = node.GetType();
                    var owner = AccessTools.Property(t, "Owner").GetValue(node, null);
                    var act = AccessTools.Property(t, "Action").GetValue(node, null) as Delegate;
                    if (ours < 0 && act != null && act.Method != null && act.Method.DeclaringType == typeof(FuneralRites)) ours = i;
                    if (bk < 0 && owner != null && owner.GetType().Name == "BKTitleBehavior") bk = i;
                    node = AccessTools.Field(t, "Next").GetValue(node);
                    i++;
                }
                if (ours < 0) return "? (naszego sluchacza nie ma na liscie)";
                if (bk < 0) return "TAK (BK nie slucha)";
                return ours < bk ? "TAK" : "NIE (wykrycie przez migawke doby)";
            }
            catch (Exception e) { Stumble("BeforeBk", e); return "? (blad)"; }
        }

        // ---------------------------------------------------------------- doba

        /// <summary>DailyTick (ostatni w dobie, po lochu): migawka glow, wygasle pogrzeby (linia KONIEC), porzadek pamieci zmian glowy.</summary>
        internal static void Daily()
        {
            if (Campaign.Current == null) return;
            try { Snapshot(); } catch (Exception e) { Stumble("Snapshot", e); }
            try
            {
                int today = Today();
                _tmp.Clear();
                foreach (var kv in _active) if (today > kv.Value.End) _tmp.Add(kv.Key);
                foreach (var id in _tmp)
                {
                    var r = _active[id];
                    _active.Remove(id);
                    Info("KONIEC " + id + " (" + r.Name + (string.IsNullOrEmpty(r.Others) ? "" : " + " + r.Others) + ", siedziba " + r.Seat + ", tryb " + (r.Mode == Full ? "pelny" : "cichy") + ") | gracz obecny "
                         + (r.Attended ? "T" : "N") + ", dar " + r.Gift + (r.Mode == Full ? TruceEndText(r) : "") + GatherEnd(r) + " | sesja: glowy wykryte przed " + _nPre + " / zmiana glowy " + _nChg + " / migawka " + _nSnap + ", pelnych " + _nFull + ", cichych " + _nQuiet
                         + ", tylko wiesc " + _nWord + ", wspolnych " + _nJoint + SuccEndText() + ", potkniecia " + _stumbles + ".");
                }
                if (_changed.Count > 0)
                {
                    double now = CampaignTime.Now.ToHours;
                    _tmp.Clear();
                    foreach (var kv in _changed) if (now - kv.Value.Hour > 24.0) _tmp.Add(kv.Key);
                    foreach (var id in _tmp) _changed.Remove(id);
                }
                if (_pre.Count > 64) _pre.Clear();   // zgon wstrzymany po BeforeHeroKilled nie zdarza sie w grze; bezpiecznik pamieci
                _tmp.Clear();
            }
            catch (Exception e) { Stumble("Daily", e); }
            try { SuccDaily(); } catch (Exception e) { Stumble("SuccDaily", e); }     // 410: spory po koncu pogrzebu (rozstrzygniecie), urazy, zaloby; migawka 312c
            try { TruceDaily(); } catch (Exception e) { Stumble("TruceDaily", e); }   // 406: migawka pokoju po wygaszeniu wierszy (stan fabuly i pan siedziby na dzis)
        }

        private static void Snapshot()
        {
            _leaderOf.Clear();
            foreach (var c in Clan.All)
            {
                if (c == null || c.StringId == null || c.IsEliminated || c.Leader == null) continue;
                _leaderOf[c.StringId] = c.Leader.StringId;
            }
        }

        // ---------------------------------------------------------------- gracz: opcje w siedzibie

        /// <summary>CampaignEvents.OnAfterSessionLaunchedEvent: dwie opcje w menu "town" i "castle" (przed "Leave"), migawka glow, linia startu. Nie rzuca.</summary>
        internal static void AddMenus(CampaignGameStarter starter)
        {
            int added = 0;
            try
            {
                added += AddOpt(starter, "town", "town_leave", OptAttendTown, "{=!}Attend the funeral of {ARM_FUN_NAME}", AttendCondition, AttendConsequence) ? 1 : 0;
                added += AddOpt(starter, "town", "town_leave", OptGiftTown, "{=!}Give {ARM_FUN_GOLD} denars for the rites", GiftCondition, GiftConsequence) ? 1 : 0;
                added += AddOpt(starter, "castle", "leave", OptAttendCastle, "{=!}Attend the funeral of {ARM_FUN_NAME}", AttendCondition, AttendConsequence) ? 1 : 0;
                added += AddOpt(starter, "castle", "leave", OptGiftCastle, "{=!}Give {ARM_FUN_GOLD} denars for the rites", GiftCondition, GiftConsequence) ? 1 : 0;
                _hookMenu = added + "/4";
            }
            catch (Exception e) { _hookMenu = "BRAK (blad: " + e.Message + ")"; Stumble("AddMenus", e); }
            try { Snapshot(); } catch (Exception e) { Stumble("Snapshot(start)", e); }
            TruceRebuild();   // 406: po wczytaniu (Import zostawia migawke pusta - osady sa pewne dopiero tutaj)
            try
            {
                var s = Settings.Current;
                int full = 0;
                foreach (var r in _active.Values) if (r.Mode == Full) full++;
                Log.Info(Tag + " (405-407): start | klucze: pogrzeby " + YN(s != null && s.Funerals) + ", dni " + Days + ", naraz " + MaxActive + " (1 na krolestwo), gracz " + YN(s != null && s.FuneralPlayerAttend)
                         + ", dar " + (s != null ? s.FuneralGiftGold : 0) + ", cichy w kanonie " + YN(s != null && s.FuneralCanonQuiet) + ", log " + YN(s != null && s.FuneralLog) + ", pokoj pogrzebu " + YN(s != null && s.FuneralTruce)
                         + " | sluchacz przed BK " + BeforeBk() + " | BK linia dziedziczenia " + YN(BkReady()) + " | kanon tutaj " + YN(SafeCanon())
                         + " | menu siedziby " + _hookMenu + " | pokoj: ocena celu " + SiegeRansom.HookScoreText + ", weto BK FindArmyObjective " + WarFront334.BkHookText
                         + ", opcja oblezenia gracza " + SiegeRansom.HookBesiegeText + ", osad w pokoju " + (_truce != null ? _truce.By.Count : 0) + " | zjazd rodu: latka AiHourlyTick 130 " + _gHook + ", zasieg " + (s != null ? s.FuneralTravelRange : 0) + ", eskorta w wojnie " + (s != null ? s.FuneralMinEscort : 0) + ", rodow w zjezdzie " + (_gather != null ? _gather.By.Count : 0)
                         + " | latki wlasne " + (_gHooked ? 1 : 0) + " | migawka glow " + _leaderOf.Count + " rodow | w zapisie " + _active.Count + " (pelnych " + full + ").");
            }
            catch (Exception e) { Stumble("StartLine", e); }
            SuccStart();   // 410: migawka 312c (zaloba, rody panujace w sporze) i linia startu sukcesji
        }

        private static bool SafeCanon() { try { return Prologue.CanonHere; } catch { return false; } }

        private static bool AddOpt(CampaignGameStarter starter, string menuId, string leaveId, string optId, string text, GameMenuOption.OnConditionDelegate cond, GameMenuOption.OnConsequenceDelegate cons)
        {
            var mgr = Campaign.Current != null ? Campaign.Current.GameMenuManager : null;
            var menu = mgr != null ? mgr.GetGameMenu(menuId) : null;
            if (starter == null || menu == null) return false;
            int idx = -1, i = 0;
            foreach (var o in menu.MenuOptions)
            {
                if (o != null && o.IdString == optId) return true;   // juz jest
                if (o != null && o.IdString == leaveId && idx < 0) idx = i;
                i++;
            }
            starter.AddGameMenuOption(menuId, optId, text, cond, cons, false, idx);
            return true;
        }

        /// <summary>Pogrzeb trwajacy w osadzie, w ktorej stoi gracz (siedziba dalej rodu, rod zyje, dziedzic zyje, bez wojny z rodem). null = brak.</summary>
        private static Row Here(out Clan clan, out Hero heir)
        {
            clan = null; heir = null;
            var s = Settings.Current;
            if (_active.Count == 0 || s == null || !s.Funerals || !s.FuneralPlayerAttend) return null;
            var st = Settlement.CurrentSettlement;
            if (st == null || st.StringId == null || Hero.MainHero == null) return null;
            int today = Today();
            foreach (var r in _active.Values)
            {
                if (r.Seat != st.StringId || today > r.End) continue;
                var cl = FindClan(r.Clan);
                if (cl == null || cl.IsEliminated || st.OwnerClan != cl) return null;
                var h = cl.Leader;
                if (h == null || !h.IsAlive || h == Hero.MainHero) return null;
                var pf = Clan.PlayerClan != null ? Clan.PlayerClan.MapFaction : null;
                if (pf != null && cl.MapFaction != null && pf.IsAtWarWith(cl.MapFaction)) return null;
                clan = cl; heir = h;
                return r;
            }
            return null;
        }

        private static bool AttendCondition(MenuCallbackArgs args)
        {
            try
            {
                args.optionLeaveType = GameMenuOption.LeaveType.Default;
                Clan cl; Hero heir;
                var r = Here(out cl, out heir);
                if (r == null || r.Attended) return false;
                MBTextManager.SetTextVariable("ARM_FUN_NAME", r.Name, false);
                args.Tooltip = new TextObject("{=!}Stand with " + Nm(cl) + " at the grave. " + Nm(heir) + " and the lords of the house who are here will think better of you.");
                return true;
            }
            catch (Exception e) { Stumble("AttendCondition", e); return false; }
        }

        /// <summary>Skutek od razu: +3 z dziedzicem, +1 z kazdym innym lordem rodu stojacym w siedzibie. Bez menu oczekiwania i bez przelaczania menu.</summary>
        private static void AttendConsequence(MenuCallbackArgs args)
        {
            try
            {
                Clan cl; Hero heir;
                var r = Here(out cl, out heir);
                if (r == null || r.Attended) return;
                r.Attended = true;
                var st = Settlement.CurrentSettlement;
                int lords = 0;
                _lordsHere.Clear();
                foreach (var h in cl.Heroes)
                    if (h != null && h != heir && h.IsAlive && h.IsLord && !h.IsChild && !h.IsPrisoner && h.CurrentSettlement == st) _lordsHere.Add(h);
                ChangeRelationAction.ApplyPlayerRelation(heir, 3);
                foreach (var h in _lordsHere) { try { ChangeRelationAction.ApplyPlayerRelation(h, 1, true, false); lords++; } catch (Exception e) { Stumble("Attend.lord", e); } }
                _lordsHere.Clear();
                Log.Player("You stood with " + Nm(cl) + " at the grave of " + r.Name + ". " + Nm(heir) + " will remember it.");
                Info("gracz na pogrzebie " + r.Name + " (rod " + r.Clan + ", siedziba " + r.Seat + "): dziedzic " + Nm(heir) + " +3, lordowie rodu na miejscu " + lords + " po +1.");
                SuccAsk(cl, heir);   // 410: otwarty spor o dziedzictwo - pytanie o strone (raz na spor; okno gry, menu bez zmian)
            }
            catch (Exception e) { Stumble("AttendConsequence", e); }
        }

        private static readonly List<Hero> _lordsHere = new List<Hero>();

        private static bool GiftCondition(MenuCallbackArgs args)
        {
            try
            {
                args.optionLeaveType = GameMenuOption.LeaveType.Default;
                var s = Settings.Current;
                Clan cl; Hero heir;
                var r = Here(out cl, out heir);
                if (r == null || r.Gift > 0 || s.FuneralGiftGold <= 0) return false;
                int gold = s.FuneralGiftGold;
                MBTextManager.SetTextVariable("ARM_FUN_GOLD", gold);
                if (Hero.MainHero.Gold < gold)
                {
                    args.IsEnabled = false;
                    args.Tooltip = new TextObject("{=!}You do not have " + gold + " denars.");
                }
                else args.Tooltip = new TextObject("{=!}The denars go to " + Nm(heir) + " for the rites of " + r.Name + ". " + (heir.IsFemale ? "She" : "He") + " will think better of you.");
                return true;
            }
            catch (Exception e) { Stumble("GiftCondition", e); return false; }
        }

        /// <summary>Dar: zloto gracz -> dziedzic akcja gry (jedyny przeplyw; nic nie powstaje i nie znika), +2 z dziedzicem, raz na pogrzeb.</summary>
        private static void GiftConsequence(MenuCallbackArgs args)
        {
            try
            {
                var s = Settings.Current;
                Clan cl; Hero heir;
                var r = Here(out cl, out heir);
                if (r == null || r.Gift > 0 || s.FuneralGiftGold <= 0) return;
                int gold = s.FuneralGiftGold;
                if (Hero.MainHero.Gold < gold) return;
                r.Gift = gold;
                GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, heir, gold);
                ChangeRelationAction.ApplyPlayerRelation(heir, 2);
                Info("dar gracza na obrzedy " + r.Name + " (rod " + r.Clan + "): " + gold + " zlota do dziedzica " + Nm(heir) + " (" + heir.StringId + "), relacja +2.");
            }
            catch (Exception e) { Stumble("GiftConsequence", e); }
        }

        // ---------------------------------------------------------------- zapis

        private static bool Ok(string id) { return !string.IsNullOrEmpty(id) && id.IndexOf('|') < 0 && id.IndexOf('\n') < 0 && id.IndexOf('\r') < 0; }

        /// <summary>"v1" + wiersze "F|rod|zmarly|imie|dziedzic|siedziba|krolestwo|doba|obrzed|koniec|tryb|wladca|byl|dar|powod|linia|inni"
        /// + 410: wiersze "S|..." (spor) i "K|..." (zaloba krolestwa) - SuccExport.</summary>
        internal static string Export()
        {
            try
            {
                if (_active.Count == 0 && !SuccAny) return "";
                var sb = new StringBuilder(32 + 128 * (_active.Count + _disputes.Count));
                sb.Append("v1");
                foreach (var r in _active.Values)
                {
                    if (!Ok(r.Clan) || !Ok(r.Dead) || !Ok(r.Seat)) continue;
                    sb.Append('\n').Append("F|").Append(r.Clan).Append('|').Append(r.Dead).Append('|').Append(Clean(r.Name)).Append('|').Append(Clean(r.Heir)).Append('|').Append(r.Seat)
                      .Append('|').Append(Clean(r.Kingdom)).Append('|').Append(r.Day.ToString(Inv)).Append('|').Append(r.Rite.ToString(Inv)).Append('|').Append(r.End.ToString(Inv))
                      .Append('|').Append(r.Mode.ToString(Inv)).Append('|').Append(r.King ? '1' : '0').Append('|').Append(r.Attended ? '1' : '0').Append('|').Append(r.Gift.ToString(Inv))
                      .Append('|').Append(Clean(r.Why)).Append('|').Append(Clean(r.Line)).Append('|').Append(Clean(r.Others));
                }
                SuccExport(sb);   // 410
                return sb.ToString();
            }
            catch (Exception e) { Stumble("Export", e); return ""; }
        }

        /// <summary>Wczytanie (brak klucza w starym zapisie = pusto). Zly wiersz pominiety; wiersz po terminie zamknie pierwsza doba.</summary>
        internal static void Import(string data)
        {
            _active.Clear();
            TruceReset();   // 406: migawka pokoju od nowa w AddMenus (po starcie sesji)
            SuccReset();    // 410: spory i zaloby z zapisu; migawka 312c w AddMenus (SuccStart)
            int bad = 0;
            try
            {
                if (string.IsNullOrEmpty(data)) return;
                var lines = data.Split('\n');
                if (lines.Length == 0 || lines[0].Trim() != "v1") { Log.Info(Tag + ": zapis nieczytelny - od zera."); return; }
                for (int i = 1; i < lines.Length; i++)
                {
                    var p = lines[i].TrimEnd('\r').Split('|');
                    if (p.Length == 0 || p[0].Length == 0) continue;
                    try
                    {
                        if (p[0] == "S" || p[0] == "K") { if (!SuccImportLine(p)) bad++; continue; }   // 410
                        if (p[0] != "F" || p.Length < 14 || p[1].Length == 0 || p[2].Length == 0 || p[5].Length == 0) { bad++; continue; }
                        var r = new Row
                        {
                            Clan = p[1], Dead = p[2], Name = p[3], Heir = p[4], Seat = p[5], Kingdom = p[6], Day = int.Parse(p[7], Inv), Rite = int.Parse(p[8], Inv), End = int.Parse(p[9], Inv),
                            Mode = p[10] == "0" ? Full : Quiet, King = p[11] == "1", Attended = p[12] == "1", Gift = Math.Max(0, int.Parse(p[13], Inv)),
                            Why = p.Length > 14 ? p[14] : "", Line = p.Length > 15 ? p[15] : "", Others = p.Length > 16 ? p[16] : ""
                        };
                        _active[r.Clan] = r;
                    }
                    catch { bad++; }
                }
                if (bad > 0) Log.Info(Tag + ": wczytano z zapisu - bledne wiersze pominiete: " + bad + ".");
            }
            catch (Exception e) { Stumble("Import", e); }
        }
    }
}
