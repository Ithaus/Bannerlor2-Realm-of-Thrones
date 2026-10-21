using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// m104-e5 - SCENY ZMIENIAJACE SWIAT PROLOGU 298 (projekt PROJEKT-PROLOG-298 pkt 2, rodzaj "S"; Jeff 21.10 "rob to teraz"). Tylko kampania z prologiem (Active),
    /// kazda scena pod swoim kluczem MCM (czytany co dobe), wszystko konczy sie najpozniej przy koncu prologu (dzien 186-188); kampania klasyczna - latki sie nawet nie
    /// wpinaja (InstallRest leniwie przy pierwszej kampanii z prologiem), a zapis arm_prologue jest pusty.
    ///
    /// STRAZ NIEWOLI (pkt 3 i 4): jency kanonu z tabeli arm_canon_moves.tsv (CanonCompanions.HeldRow - ostatni wykonany wiersz "prisoner", pozniejszy "free" jeszcze
    /// nie zapadl): #p01 Tyrion w lochu Orlego Gniazda (dzien 110 -> #p02 w dniu 132, klucz PrologueTyrionHeld) i #p10 Jaime w Riverrun (wojna Polnocy +5 dni ->
    /// #m02 po Czarnym Nurcie, klucz PrologueJaimeAtRiverrun, najwyzej JaimeHoldMaxDays dni). Rozpoznanie m104 (dekompilacja 1.4.8): RealisticCaptivity dotyka tylko
    /// niewoli gracza; jeniec AI w lochu wychodzi przez (a) ucieczke 4%/dobe (PrisonerReleaseCampaignBehavior.DailyHeroTick), (b) zwolnienie przy pokoju / zmianie
    /// krolestwa rodu (ReleasePrisonersInternal - Tyrion i Dolina, Jaime i Dorzecze nie sa w wojnie z jego krolestwem), (c) okup AI-AI (RansomOfferCampaignBehavior.
    /// DailyTickHero -> ConsiderRansomPrisoner, u nas Ransom178 - zloto plynie PRZED EndCaptivityAction.ApplyByRansom), (d) wypuszczenie przez porywacza i wykup.
    /// Dwie latki: prefiks EndCaptivityAction.ApplyInternal (ucieczka, pokoj, okup, wypuszczenie, wykup - pomijane; smierc i "po bitwie" (upadek osady, zmiana pana)
    /// zawsze przechodza; gracz jako sprawca (rozbicie lochu, jego okup) przechodzi - nic nie placi za darmo) i prefiks RansomOfferCampaignBehavior.DailyTickHero
    /// (okup nie zaczyna sie wcale - zloto nie plynie za jenca, ktory zostanie w lochu). Straz tylko w lochu OSADY (jeniec w druzynie - zwykle zasady gry: rozwiazanie
    /// druzyny i tratwa uwalniaja go, inaczej zostalby w rozwiazanej partii) i tylko przy kluczu CanonCompanionsFollowStory (bez niego wiersz "free" nie przyjdzie).
    /// Nasze uwolnienie (akcja "free") przechodzi (CanonCompanions.Freeing).
    ///
    /// Zapis: arm_prologue (klucze "w*"). Linie logu: "Prolog E5 (m104): ..." - start sesji, doba (kazda scena: stan / pozycja / postep), zdarzenia scen.
    /// Bledy: Stumble (licznik prologu), bez globalnego wylacznika.
    /// </summary>
    internal static partial class Prologue
    {
        internal const string WTag = "Prolog E5 (m104): ";
        private const string TyrionTpl = "ROTwanderer8", JaimeTpl = "ROTwanderer1";
        private const string TyrionHoldRow = "p01", JaimeHoldRow = "p10";
        private const int TyrionHoldLastDay = 140;    // dzien prologu - zapas po #p02 (132); straz Tyriona gasnie najpozniej wtedy
        private const int JaimeHoldMaxDays = 364;     // dni od #p10 - Jaime w Riverrun najwyzej rok (dlugi rok Armoury), gdyby #m02 nie przyszedl
        private const int WorldLogAfterStory = 20;    // linia doby E5 jeszcze tyle dni po starcie opowiesci (jak linia doby prologu), potem co 10 dni przy strazy

        // ---------------------------------------------------------------- stan (zapis)
        // straz niewoli: od poczatku - ucieczki, okupy (dni bez okupu), pokoj, wypuszczenie przez porywacza, wykup; przepuszczone (gracz, smierc, po bitwie)
        private static int _hbEsc, _hbRan, _hbPeace, _hbChoice, _hbComp, _hbPass;
        // sesja / doba
        private static int _hbDay, _hbPassDay, _wStumbles;
        private static readonly HashSet<string> _hbSaid = new HashSet<string>(StringComparer.Ordinal);
        private static string _worldWire = "nie wpiete";
        private static bool _worldDirty;      // po wczytaniu: partie scen odbudowane w pierwszej godzinie (WorldHourly)
        private static bool _wLegacy;         // zapis kampanii z prologiem sprzed E5 (bez "whb=") - sceny, ktorych czas juz minal, pominiete (bez nadrabiania)
        private static int _ordersDay;        // rozkazy wydane partiom scen od ostatniej linii doby

        private static void ResetWorld()
        {
            _hbEsc = _hbRan = _hbPeace = _hbChoice = _hbComp = _hbPass = 0;
            _hbDay = _hbPassDay = _wStumbles = 0; _hbSaid.Clear();
            _worldDirty = false; _wLegacy = false; _ordersDay = 0;
            _ctl.Clear(); _thawed = 0; _heroCacheDay = -1; _heroCache.Clear();
            ResetProgress();
            ResetTourney();
        }

        private static void WStumble(string where, Exception e) { _wStumbles++; Stumble("World." + where, e); }

        private static string PackWorld()
        {
            return "whb=" + string.Join(",", new[] { _hbEsc, _hbRan, _hbPeace, _hbChoice, _hbComp, _hbPass }.Select(x => x.ToString(Inv))) + ";"
                   + PackProgress() + PackTourney();
        }

        private static string Ids(IEnumerable<string> ids) { return string.Join(".", ids.Where(x => !string.IsNullOrEmpty(x)).ToArray()); }

        private static List<string> IdList(Dictionary<string, string> map, string key)
        {
            string v;
            var l = new List<string>();
            if (map.TryGetValue(key, out v)) foreach (var x in v.Split('.')) if (x.Length > 0) l.Add(x);
            return l;
        }

        private static void UnpackWorld(Dictionary<string, string> map)
        {
            ResetWorld();
            string v;
            if (map.TryGetValue("whb", out v))
            {
                var a = v.Split(',');
                int[] r = new int[6];
                for (int i = 0; i < 6 && i < a.Length; i++) int.TryParse(a[i], System.Globalization.NumberStyles.Integer, Inv, out r[i]);
                _hbEsc = r[0]; _hbRan = r[1]; _hbPeace = r[2]; _hbChoice = r[3]; _hbComp = r[4]; _hbPass = r[5];
            }
            _wLegacy = Active && !map.ContainsKey("whb");
            UnpackProgress(map);
            UnpackTourney(map);
            _worldDirty = true;
        }

        // ---------------------------------------------------------------- latki (raz na proces, przy pierwszej kampanii z prologiem - InstallRest)

        private static string ApplyWorld(Harmony h)
        {
            string g = "BRAK", r = "BRAK";
            try
            {
                var m = AccessTools.DeclaredMethod(typeof(EndCaptivityAction), "ApplyInternal");
                if (m != null && h.Patch(m, prefix: new HarmonyMethod(typeof(Prologue), nameof(CaptivityPrefix))) != null) g = "TAK";
            }
            catch (Exception e) { WStumble("ApplyWorld.Captivity", e); }
            try
            {
                var m = AccessTools.DeclaredMethod(typeof(RansomOfferCampaignBehavior), "DailyTickHero", new[] { typeof(Hero) });
                if (m != null && h.Patch(m, prefix: new HarmonyMethod(typeof(Prologue), nameof(RansomDailyPrefix))) != null) r = "TAK";
            }
            catch (Exception e) { WStumble("ApplyWorld.Ransom", e); }
            string f = "BRAK (BK)", gt = "BRAK (GrandTourney)";
            try
            {
                // e5:A: uczta BK (BKFeastBehavior.HourlyTickPartyImpl) co godzine wylacza AI goscia i wysyla go na uczte - partia sceny (orszak, turniej, Beric) jej nie slucha
                var t = AccessTools.TypeByName("BannerKings.Behaviours.Feasts.BKFeastBehavior");
                var m = t != null ? AccessTools.DeclaredMethod(t, "HourlyTickPartyImpl", new[] { typeof(MobileParty) }) : null;
                if (m != null && h.Patch(m, prefix: new HarmonyMethod(typeof(Prologue), nameof(SkipScriptedPrefix))) != null) f = "TAK";
            }
            catch (Exception e) { WStumble("ApplyWorld.BkFeast", e); }
            try
            {
                // GrandTourney: herold nie wzywa partii sceny (SummonLords); null w GT = jak dotad
                var t = AccessTools.TypeByName("GrandTourney.CanonApi");
                var fi = t != null ? t.GetField("SkipSummon", BindingFlags.Public | BindingFlags.Static) : null;
                if (fi != null && fi.FieldType == typeof(Func<MobileParty, bool>)) { fi.SetValue(null, new Func<MobileParty, bool>(Scripted)); gt = "TAK"; }
            }
            catch (Exception e) { WStumble("ApplyWorld.GrandTourney", e); }
            _worldWire = "straz niewoli " + g + ", okup " + r + ", uczta BK " + f + ", herold GT " + gt;
            return _worldWire;
        }

        // ---------------------------------------------------------------- straz niewoli (pkt 3 i 4)

        private static bool HoldKey(string row)
        {
            var s = S;
            if (s == null || !s.CanonCompanionsFollowStory) return false;
            if (row == TyrionHoldRow) return s.PrologueTyrionHeld;
            if (row == JaimeHoldRow) return s.PrologueJaimeAtRiverrun;
            return false;
        }

        private static bool HoldInTime(string row, Hero h)
        {
            if (row == TyrionHoldRow) return PrologueDay() < TyrionHoldLastDay;
            if (row == JaimeHoldRow)
            {
                int since = CanonCompanions.HeldDay(h);
                return since < 0 || (int)CampaignTime.Now.ToDays - since <= JaimeHoldMaxDays;
            }
            return false;
        }

        /// <summary>Wiersz niewoli, ktorej pilnuje scena (null - straz nie dziala dla tego bohatera): kampania z prologiem, klucz, czas, loch osady.</summary>
        internal static string HeldByScene(Hero h)
        {
            if (!Active || h == null) return null;
            var row = CanonCompanions.HeldRow(h);
            if (row == null || !HoldKey(row) || !HoldInTime(row, h)) return null;
            var pp = h.PartyBelongedToAsPrisoner;
            if (pp == null || !pp.IsSettlement) return null;
            return row;
        }

        private static string WhoS(Hero h) { try { return h != null && h.Name != null ? h.Name.ToString() : "-"; } catch { return "?"; } }

        private static string WhereS(Hero h)
        {
            try
            {
                if (h == null) return "-";
                if (!h.IsAlive) return "nie zyje";
                var pp = h.PartyBelongedToAsPrisoner;
                if (h.IsPrisoner && pp != null) return "jeniec " + (pp.IsSettlement ? pp.Settlement.StringId : pp.Name.ToString());
                var p = h.PartyBelongedTo;
                if (p != null) return (p.LeaderHero == h ? "prowadzi druzyne" : "w druzynie " + p.Name) + (p.CurrentSettlement != null ? " w " + p.CurrentSettlement.StringId : "");
                return h.CurrentSettlement != null ? "w " + h.CurrentSettlement.StringId : "bez miejsca";
            }
            catch { return "?"; }
        }

        /// <summary>Prefiks EndCaptivityAction.ApplyInternal(prisoner, detail, facilitator, showNotification): jeniec trzymany przez scene nie wychodzi przez ucieczke,
        /// pokoj, okup, wypuszczenie ani wykup. Smierc i "po bitwie" (upadek osady, zmiana pana), gracz jako sprawca i nasze uwolnienie - przechodza.</summary>
        public static bool CaptivityPrefix(Hero __0, EndCaptivityDetail __1, Hero __2)
        {
            try
            {
                if (!Active || __0 == null || __0 == Hero.MainHero) return true;
                string row = HeldByScene(__0);
                if (row == null) return true;
                string what;
                bool pass = __1 == EndCaptivityDetail.Death || __1 == EndCaptivityDetail.ReleasedAfterBattle || CanonCompanions.Freeing(__0) || (__2 != null && __2 == Hero.MainHero);
                if (pass)
                {
                    if (CanonCompanions.Freeing(__0)) return true;   // nasze uwolnienie (#p02 / #m02) - linie pisze CanonCompanions
                    _hbPass++; _hbPassDay++;
                    Log.Info(WTag + "straz niewoli: PRZEPUSZCZONE - " + WhoS(__0) + " (#" + row + ", " + WhereS(__0) + ") - " + __1 + (__2 == Hero.MainHero ? " (sprawca gracz)" : "")
                             + " (dzien prologu " + PrologueDay().ToString("0.0", Inv) + ").");
                    return true;
                }
                switch (__1)
                {
                    case EndCaptivityDetail.ReleasedAfterEscape: _hbEsc++; what = "ucieczka"; break;
                    case EndCaptivityDetail.ReleasedAfterPeace: _hbPeace++; what = "zwolnienie przy pokoju"; break;
                    case EndCaptivityDetail.Ransom: _hbRan++; what = "okup"; break;
                    case EndCaptivityDetail.ReleasedByCompensation: _hbComp++; what = "wykup"; break;
                    default: _hbChoice++; what = "wypuszczenie przez porywacza"; break;
                }
                _hbDay++;
                string key = __0.StringId + "|" + what;
                if (_hbSaid.Add(key))
                    Log.Info(WTag + "straz niewoli: ZABLOKOWANE - " + WhoS(__0) + " (#" + row + ", " + WhereS(__0) + ") - " + what + (__2 != null ? " (sprawca " + WhoS(__2) + ")" : "")
                             + " (dzien prologu " + PrologueDay().ToString("0.0", Inv) + "; linia raz na dobe na rodzaj).");
                return false;
            }
            catch (Exception e) { WStumble("CaptivityPrefix", e); return true; }
        }

        /// <summary>Prefiks RansomOfferCampaignBehavior.DailyTickHero(hero): jeniec trzymany przez scene - okup nie zaczyna sie wcale (zloto nie plynie przed zwolnieniem,
        /// ktore straz i tak by zatrzymala). Liczone jako dni bez okupu.</summary>
        public static bool RansomDailyPrefix(Hero __0)
        {
            try
            {
                if (!Active || __0 == null || !__0.IsPrisoner) return true;
                if (HeldByScene(__0) == null) return true;
                _hbRan++; _hbDay++;
                return false;
            }
            catch (Exception e) { WStumble("RansomDailyPrefix", e); return true; }
        }

        private static string HoldText(string tpl, string row, string freeRow)
        {
            var h = CanonCompanions.TemplateHero(tpl);
            if (h == null) return "brak postaci";
            string held = HeldByScene(h);
            string tab = CanonCompanions.HeldRow(h);
            return WhoS(h) + " " + WhereS(h) + (held != null ? " (#" + held + ", straz TAK)"
                   : tab != null ? " (#" + tab + ", straz NIE: " + (!HoldKey(tab) ? "klucz" : !HoldInTime(tab, h) ? "po czasie" : "nie w lochu osady") + ")"
                   : " (poza niewola #" + row + "/#" + freeRow + ")");
        }

        // ---------------------------------------------------------------- partie pod scena (wspolne dla scen)
        // Partia sceny: flaga gry "bez nowych decyzji" (MobilePartyAi.DoNotMakeNewDecisions - mysl gry, ROT AIThinkPatch, wezwania do armii ja pomijaja), rozkaz sceny
        // (idz do osady / eskortuj / scigaj / rabuj) przez wejscie metody (bramka snu Armoury SleepGate, straznik drog BK - jak GrandTourney i MaterialOrders), opcjonalnie
        // ukrycie przed AI (MobileParty.IgnoreForHours odnawiane co godzine: AI nie wybiera jej za cel i nie zaczyna z nia spotkania; gracz moze). Spiaca partia (NightRest
        // oboz / osada / sen dlugu) - rozkaz nie ruszany do switu (NightRest oddaje swoj, godzine pozniej poprawiamy). Rejestr bez zapisu: sceny odbudowuja go z wlasnego
        // stanu co godzine (po wczytaniu - pierwsza godzina). Flaga gry idzie do zapisu ([SaveableProperty]), wiec WorldBeforeSave ja zdejmuje, WorldSaveOver przywraca
        // (wzor FreeFolkRaids - partia bez wpisu nie stoi zamrozona na zawsze po cofnieciu DLL).
        private sealed class Ctl
        {
            public string Scene;
            public MobileParty P;
            public int Kind;
            public Settlement To;
            public MobileParty Follow;
            public bool Hide;
            public bool Seen;    // odswiezona w tym przebiegu scen (bez odswiezenia - puszczona)
        }

        private const int KGo = 1, KEscort = 2, KEngage = 3, KRaid = 4;
        private const float HideHours = 2f;
        private static readonly Dictionary<MobileParty, Ctl> _ctl = new Dictionary<MobileParty, Ctl>();
        private static int _thawed;
        private static MethodInfo _mGo, _mEsc, _mEng, _mRaid;

        /// <summary>Partia pod scena E5 (uczta BK, herold GrandTourney - pomijaja ja).</summary>
        internal static bool Scripted(MobileParty mp) { return mp != null && _ctl.Count > 0 && _ctl.ContainsKey(mp); }

        /// <summary>Prefiks BKFeastBehavior.HourlyTickPartyImpl(party): partia sceny nie jedzie na uczte BK.</summary>
        public static bool SkipScriptedPrefix(MobileParty __0) { return !Scripted(__0); }

        private static int CountScene(string scene) { int n = 0; foreach (var c in _ctl.Values) if (c.Scene == scene) n++; return n; }

        /// <summary>Partia pod scena (albo nowy rozkaz sceny). Wolane z kroku sceny - co godzine.</summary>
        private static void Control(string scene, MobileParty mp, int kind, Settlement to, MobileParty follow, bool hide)
        {
            if (mp == null || !mp.IsActive || mp == MobileParty.MainParty) return;
            Ctl c;
            if (!_ctl.TryGetValue(mp, out c)) { c = new Ctl { P = mp }; _ctl[mp] = c; }
            c.Scene = scene; c.Kind = kind; c.To = to; c.Follow = follow; c.Hide = hide; c.Seen = true;
            try { ApplyCtl(c); } catch (Exception e) { WStumble("Control(" + scene + ")", e); }
        }

        /// <summary>Partia wraca do zwyklego AI (AI mysli od razu - jak FreeFolkRaids.Release).</summary>
        private static void Uncontrol(MobileParty mp)
        {
            if (mp == null || !_ctl.Remove(mp)) return;
            Thaw(mp);
        }

        private static void UncontrolScene(string scene)
        {
            foreach (var c in _ctl.Values.ToList()) if (c.Scene == scene) Uncontrol(c.P);
        }

        private static void Thaw(MobileParty mp)
        {
            try { if (mp.IsActive && mp.Ai != null && mp.Ai.DoNotMakeNewDecisions) { mp.Ai.SetDoNotMakeNewDecisions(false); mp.Ai.RethinkAtNextHourlyTick = true; } }
            catch (Exception e) { WStumble("Thaw", e); }
        }

        private static void ApplyCtl(Ctl c)
        {
            var mp = c.P;
            if (mp == null || !mp.IsActive || mp.Ai == null) return;
            if (!mp.Ai.DoNotMakeNewDecisions) mp.Ai.SetDoNotMakeNewDecisions(true);
            if (c.Hide) mp.IgnoreForHours(HideHours);
            if (mp.MapEvent != null || mp.SiegeEvent != null || mp.IsDisbanding) return;
            if (NightRest.SleepKind(mp) != NightRest.SkNone) return;
            if (mp.Army != null)
            {
                // armia (np. Ned wezwany przez swoich przed wyjazdem) - partia sceny wychodzi; wodz armii - armia rozwiazana (jak areszt Neda)
                if (mp.Army.LeaderParty == mp) DisbandArmyAction.ApplyByUnknownReason(mp.Army);
                else mp.Army = null;
            }
            if (OrderOk(c)) return;
            var nav = MobileParty.NavigationType.Default;
            switch (c.Kind)
            {
                case KGo: Call(ref _mGo, "SetMoveGoToSettlement", new[] { typeof(Settlement), typeof(MobileParty.NavigationType), typeof(bool) }, mp, new object[] { c.To, nav, false }); break;
                case KEscort: Call(ref _mEsc, "SetMoveEscortParty", new[] { typeof(MobileParty), typeof(MobileParty.NavigationType), typeof(bool) }, mp, new object[] { c.Follow, nav, false }); break;
                case KEngage: Call(ref _mEng, "SetMoveEngageParty", new[] { typeof(MobileParty), typeof(MobileParty.NavigationType) }, mp, new object[] { c.Follow, nav }); break;
                case KRaid: Call(ref _mRaid, "SetMoveRaidSettlement", new[] { typeof(Settlement), typeof(MobileParty.NavigationType), typeof(bool) }, mp, new object[] { c.To, nav, false }); break;
                default: return;
            }
            try { mp.RecalculateShortTermBehavior(); } catch { }
            _ordersDay++;
        }

        private static bool OrderOk(Ctl c)
        {
            var mp = c.P;
            switch (c.Kind)
            {
                case KGo: return c.To == null || mp.CurrentSettlement == c.To || (mp.DefaultBehavior == AiBehavior.GoToSettlement && mp.TargetSettlement == c.To);
                case KEscort:
                    return c.Follow == null || !c.Follow.IsActive || (mp.DefaultBehavior == AiBehavior.EscortParty && mp.TargetParty == c.Follow)
                           || (c.Follow.CurrentSettlement != null && mp.CurrentSettlement == c.Follow.CurrentSettlement);
                case KEngage: return c.Follow == null || !c.Follow.IsActive || (mp.DefaultBehavior == AiBehavior.EngageParty && mp.TargetParty == c.Follow);
                case KRaid: return c.To == null || (mp.DefaultBehavior == AiBehavior.RaidSettlement && mp.TargetSettlement == c.To);
            }
            return true;
        }

        private static void Call(ref MethodInfo mi, string name, Type[] sig, MobileParty mp, object[] args)
        {
            if (mi == null) mi = typeof(MobileParty).GetMethod(name, sig);
            if (mi == null) throw new MissingMethodException("MobileParty." + name);
            try { mi.Invoke(mp, args); }
            catch (TargetInvocationException e) { throw e.InnerException ?? e; }
        }

        private static float Dist(MobileParty mp, Settlement st)
        {
            try { return mp != null && st != null ? mp.GetPosition2D.Distance(st.GatePosition.ToVec2()) : -1f; } catch { return -1f; }
        }

        private static float Dist(MobileParty a, MobileParty b)
        {
            try { return a != null && b != null ? a.GetPosition2D.Distance(b.GetPosition2D) : -1f; } catch { return -1f; }
        }

        private static bool At(MobileParty mp, Settlement st)
        {
            if (mp == null || st == null) return false;
            if (mp.CurrentSettlement == st) return true;
            float d = Dist(mp, st);
            return d >= 0f && d <= 3f;
        }

        private static string Pos(MobileParty mp, Settlement goal)
        {
            if (mp == null) return "-";
            try
            {
                if (mp.MapEvent != null) return "bitwa";
                if (mp.CurrentSettlement != null) return "w " + mp.CurrentSettlement.StringId;
                return goal != null ? Dist(mp, goal).ToString("0", Inv) + " jedn. do " + goal.StringId : "w polu";
            }
            catch { return "?"; }
        }

        /// <summary>Druzyna, ktora bohater prowadzi (null - nie prowadzi albo jeniec; druzyna gracza i rodu gracza - nigdy).</summary>
        private static MobileParty Led(Hero h)
        {
            if (h == null || !h.IsAlive || h.IsPrisoner || h.Clan == Clan.PlayerClan) return null;
            var p = h.PartyBelongedTo;
            return p != null && p.IsActive && p.LeaderHero == h && p != MobileParty.MainParty ? p : null;
        }

        // postacie z wzoru (skan wszystkich bohaterow) - pamiec na dobe
        private static readonly Dictionary<string, Hero> _heroCache = new Dictionary<string, Hero>(StringComparer.Ordinal);
        private static int _heroCacheDay = -1;

        private static Hero Tpl(string tpl)
        {
            int d = (int)CampaignTime.Now.ToDays;
            if (d != _heroCacheDay) { _heroCache.Clear(); _heroCacheDay = d; }
            Hero h;
            if (_heroCache.TryGetValue(tpl, out h)) return h;
            h = CanonCompanions.TemplateHero(tpl);
            if (h == null)
                foreach (var x in Hero.AllAliveHeroes) if (x != null && x.Template != null && x.Template.StringId == tpl) { h = x; break; }
            _heroCache[tpl] = h;
            return h;
        }

        // ---------------------------------------------------------------- kroki scen

        /// <summary>Jeden przebieg scen (co godzine; w dobie - takze zdarzenia doby). Partie nieodswiezone w przebiegu - puszczone.</summary>
        private static void WorldStep(bool daily)
        {
            foreach (var c in _ctl.Values) c.Seen = false;
            double day = PrologueDay();
            try { ProgressStep(day, daily); } catch (Exception e) { WStumble("ProgressStep", e); }
            try { TourneyStep(day, daily); } catch (Exception e) { WStumble("TourneyStep", e); }
            foreach (var c in _ctl.Values.ToList())
                if (!c.Seen || c.P == null || !c.P.IsActive) Uncontrol(c.P);
        }

        // ---------------------------------------------------------------- doba, godzina, zapis gry

        /// <summary>Prologue.Daily (kazda doba kampanii z prologiem, takze po starcie opowiesci - straz Jaimego trwa w wojnie): sceny po kolei, linia doby.</summary>
        private static void DailyWorld()
        {
            if (!Active) return;
            double day = PrologueDay();
            int d = (int)day;
            bool holdOn = CanonCompanions.HeldCount > 0;
            if (day > PrologueDays + WorldLogAfterStory && !(holdOn && d % 10 == 0))
            {
                try { if (_ctl.Count > 0) WorldStep(true); } catch (Exception e) { WStumble("WorldStep(po prologu)", e); }   // wszystko oddane najpozniej w dniu 188
                _hbDay = 0; _hbPassDay = 0; _hbSaid.Clear(); _ordersDay = 0; return;
            }
            try { WorldStep(true); } catch (Exception e) { WStumble("WorldStep(doba)", e); }
            var sb = new StringBuilder(1200);
            sb.Append(WTag).Append("dzien ").Append(d.ToString(Inv));
            try { sb.Append(" | ").Append(ProgressLine()); } catch (Exception e) { WStumble("ProgressLine", e); }
            try { sb.Append(" | ").Append(TourneyLine()); } catch (Exception e) { WStumble("TourneyLine", e); }
            sb.Append(" | Tyrion: ").Append(HoldText(TyrionTpl, TyrionHoldRow, "p02"));
            sb.Append(" | Jaime: ").Append(HoldText(JaimeTpl, JaimeHoldRow, "m02"));
            sb.Append(" | straz niewoli: zablokowane dzis ").Append(_hbDay).Append(", od poczatku ucieczki ").Append(_hbEsc).Append(", dni bez okupu i okupy ").Append(_hbRan)
              .Append(", pokoj ").Append(_hbPeace).Append(", wypuszczenie ").Append(_hbChoice).Append(", wykup ").Append(_hbComp).Append(" | przepuszczone (gracz, smierc, po bitwie) dzis ")
              .Append(_hbPassDay).Append(", od poczatku ").Append(_hbPass);
            sb.Append(" | partie pod scena ").Append(_ctl.Count).Append(", rozkazy od wczoraj ").Append(_ordersDay);
            sb.Append(" | potkniecia E5 ").Append(_wStumbles).Append('.');
            Log.Info(sb.ToString());
            _hbDay = 0; _hbPassDay = 0; _hbSaid.Clear(); _ordersDay = 0;
        }

        /// <summary>OnSessionLaunched: linia startu E5 (tylko kampania z prologiem).</summary>
        internal static void WorldStartLine()
        {
            if (!Active) return;
            var s = S;
            Func<bool, string> yn = b => b ? "TAK" : "NIE";
            Log.Info(WTag + "start sesji | dzien prologu " + PrologueDay().ToString("0.0", Inv)
                     + " | klucze: Tyrion " + yn(s != null && s.PrologueTyrionHeld) + ", Jaime " + yn(s != null && s.PrologueJaimeAtRiverrun)
                     + " (zmiany stron CanonCompanionsFollowStory " + yn(s != null && s.CanonCompanionsFollowStory) + ")"
                     + " | orszak " + yn(s != null && s.PrologueRoyalProgress)
                     + ", turniej " + yn(s != null && s.PrologueHandTourney)
                     + " | latki: " + _worldWire + " | zapis sprzed E5 " + yn(_wLegacy) + " | " + ProgressStart() + " | " + TourneyLine()
                     + " | straz niewoli od poczatku: ucieczki " + _hbEsc + ", okup " + _hbRan + ", pokoj " + _hbPeace + ", wypuszczenie " + _hbChoice + ", wykup " + _hbComp
                     + ", przepuszczone " + _hbPass + " | potkniecia " + _stumbles + ".");
        }

        /// <summary>HourlyTickEvent: po wczytaniu - partie scen odbudowane i zamrozone od nowa (flaga gry nie idzie do zapisu - WorldBeforeSave).</summary>
        internal static void WorldHourly()
        {
            if (!Active) return;
            if (_worldDirty) _worldDirty = false;
            if (PrologueDay() > PrologueDays + 2 && _ctl.Count == 0) return;   // po prologu nic nie biegnie (straz niewoli to latki)
            try { WorldStep(false); } catch (Exception e) { WStumble("WorldStep(godzina)", e); }
        }

        /// <summary>OnBeforeSaveEvent: flaga "bez nowych decyzji" zdjeta z partii scen (gra zapisuje ja w partii) - po cofnieciu DLL nikt nie stoi zamrozony.</summary>
        internal static void WorldBeforeSave()
        {
            _thawed = 0;
            foreach (var c in _ctl.Values)
            {
                try
                {
                    var mp = c.P;
                    if (mp == null || !mp.IsActive || mp.Ai == null || !mp.Ai.DoNotMakeNewDecisions) continue;
                    mp.Ai.SetDoNotMakeNewDecisions(false);
                    _thawed++;
                }
                catch (Exception e) { WStumble("WorldBeforeSave", e); }
            }
        }

        /// <summary>OnSaveOverEvent (takze po nieudanym zapisie): flaga wraca partiom scen.</summary>
        internal static void WorldSaveOver()
        {
            if (_thawed == 0) return;
            _thawed = 0;
            foreach (var c in _ctl.Values)
            {
                try { var mp = c.P; if (mp != null && mp.IsActive && mp.Ai != null && !mp.Ai.DoNotMakeNewDecisions) mp.Ai.SetDoNotMakeNewDecisions(true); }
                catch (Exception e) { WStumble("WorldSaveOver", e); }
            }
        }

    }
}
