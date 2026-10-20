using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.LogEntries;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>
    /// R3-05c KRUKI: WIESCI Z OPOZNIENIEM (pomysl 10 z docs/POMYSLY-35-2026-10-20.md; Jeff 20.10 "28 - nie, 29 - nie, pozostale tak"; audyt 09.10 L8/Z14/F13).
    /// Klucz RavenNews. Dotad gracz w Pentos wiedzial w tej samej godzinie o bitwie pod Winterfell.
    /// Gdzie gra pokazuje wiesci (dekompilacja 1.4.8): LogEntry.AddLogEntry -> LogEntryHistory.AddActionLog (wpis do historii - encyklopedia, rozmowy, plotki,
    /// decyzje AI) -> CampaignInformationManager.NewLogEntryAdded: wpis IChatNotification z IsVisibleNotification -> InformationManager.DisplayMessage (czat
    /// gracza, kolor z DiplomacyModel.GetNotificationColor). To JEDYNE miejsce, gdzie gra wypisuje te wiesci na czat; ikony po prawej (NewMapNoticeAdded:
    /// wojna/pokoj z krolestwem gracza, oblezenie twierdzy rodu gracza, smierc bliskich, nadanie lenna) dotycza zawsze gracza - bez zmian.
    /// Regula: prefiks na NewLogEntryAdded - wpis wojenny z daleka nie idzie na czat teraz, tylko do kolejki krukow; HISTORIA GRY BEZ ZMIAN (wpis dodany
    /// jak dotad, przed prefiksem), wiec AI, encyklopedia i plotki w karczmach wiedza tyle co dzis. Tylko czat gracza.
    /// Wiesci wojenne (11 rodzajow): oblezenie (BesiegeSettlement), napad/szturm osady (BattleStarted - gra pokazuje tylko z osada), wojna, pokoj, sojusz
    /// (Start/EndAlliance), wezwanie do wojny (Start/EndCallToWar), upadek krolestwa, smierc w wojnie (CharacterKilled: w bitwie, z ran po bitwie, egzekucja
    /// i egzekucja po bitwie - smierc ze starosci/porod/zabojstwo bez zmian), niewola (TakePrisoner), koniec niewoli (EndCaptivity - para do niewoli,
    /// zeby wiesc o uwolnieniu nie wyprzedzila wiesci o pojmaniu), zbiorka armii wlasnego krolestwa (GatherArmy - gra pokazuje tylko swoje).
    /// Od razu (jak dotad): wszystko, co dotyczy gracza - bohater gracza albo jego rodu (ofiara, zabojca, jeniec, pojmujacy, wodz), twierdza rodu gracza,
    /// krolestwo/rod gracza po ktorejkolwiek stronie wojny, pokoju, sojuszu, wezwania (wypowiedzenie wojny Twojemu krolestwu nie moze sie spoznic); wiesc
    /// z bliska (do 40 jedn. mapy - pol dnia jazdy, "close by" plotek: widac dym, mowia okoliczni); wiesc bez znanego miejsca; klucz wylaczony.
    /// Opoznienie: 3 h (maester pisze list) + odleglosc w linii prostej / 6.25 jedn. na godzine (kruk ok. 150 jedn. mapy na dobe = ok. 2.5 x dzien jazdy
    /// z plotek, 3 x list BK - BKTelepathyBehavior: 0.5 h na jedn. drogi), najwyzej 12 dob. Mapa ROT ok. 1570 x 1020 jedn. (wsie z arm_map_villages.tsv):
    /// Pentos - Winterfell ok. 500 jedn. = ok. 3.5 doby, poprzek mapy ok. 10 dob. Miejsce wiesci: osada (oblezenie, szturm, loch), siedziba pana krolestwa
    /// (wojna, pokoj, sojusz - HomeSettlement wodza, jak plotki Rumours.FactionPos; z dwoch stron blizsza graczowi), miejsce bohatera (smierc, niewola).
    /// Gracz: osada, w ktorej jest, druzyna, w ktorej jest jencem, albo druzyna gracza. Odleglosc liczona w chwili zdarzenia.
    /// Kolejnosc: wiesc o tym samym (osada / bohater / para krolestw / wodz armii) nigdy nie przychodzi przed wczesniejsza wiescia o tym samym.
    /// Doreczenie co godzine gry: "A raven from <najblizsza twierdza miejsca>, 3 days on the wing: <tekst gry>" w kolorze gry. Nic nie ginie: kolejka w zapisie
    /// (klucz arm_ravens przez SaveText.Sync; brak klucza = pusta), klucz wylaczony = wszystko z kolejki od razu w nastepnej godzinie, ponad 400 w kolejce =
    /// najstarsza od razu. Zloto i towar: brak (tylko czat). Linie "Kruki (R3-05c)": start sesji, doba, do 30 wyslanych i 30 doreczonych na dobe.
    /// Potkniecia per wiesc (licznik; wiesc idzie wtedy na czat od razu, jak w grze) - nigdy globalny wylacznik.
    /// </summary>
    internal static class Ravens
    {
        private const string P = "Kruki (R3-05c): ";
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static readonly object Gate = new object();

        private const float NearRange = 40f;      // jedn. mapy: pol dnia jazdy (Rumours.Days: "half a day's ride" < 22, "a day's ride" < 60) - widac/slychac od razu
        private const float SpeedPerHour = 6.25f; // kruk: 150 jedn. mapy na dobe
        private const float HandHours = 3f;       // maester pisze i wypuszcza kruka, drugi czyta
        private const float MaxHours = 288f;      // najwyzej 12 dob
        private const int QueueMax = 400, LogMax = 30;

        internal static bool On { get { var s = Settings.Current; return s != null && s.RavenNews; } }

        private const int KSiege = 1, KAssault = 2, KWar = 3, KPeace = 4, KAlly = 5, KCall = 6, KFall = 7, KDeath = 8, KCapture = 9, KFreed = 10, KArmy = 11, KAftermath = 12, KN = 13;
        private static readonly string[] KName = { "-", "oblezenie", "napad/szturm", "wojna", "pokoj", "sojusz", "wezwanie do wojny", "upadek krolestwa",
                                                   "smierc w wojnie", "niewola", "koniec niewoli", "zbiorka armii", "po zdobyciu miasta (BK)" };

        private sealed class Raven { public double Due, Sent; public uint Color; public int Kind, Dist; public long Seq; public string Key = "", From = "", Text = ""; }
        private static readonly List<Raven> _q = new List<Raven>();
        private static long _seq;

        // liczniki doby i sesji
        private static int _dSeen, _dWar, _dDelayed, _dNear, _dMine, _dNoPos, _dOff, _dDelivered, _dForced, _dBumped, _dLogSent, _dLogGot, _dStumbles;
        private static double _dDelaySum, _dDelayMax, _dLateSum;
        private static readonly int[] _dKind = new int[KN];
        private static readonly Dictionary<string, int> _dOther = new Dictionary<string, int>(StringComparer.Ordinal);
        private static int _sDelayed, _sDelivered, _stumbles, _importN = -1, _importBad;
        private static readonly HashSet<string> _err = new HashSet<string>(StringComparer.Ordinal);
        private static List<Settlement> _forts;
        private static double _checkFromH = -1;   // kontrola latki: od tej godziny wpisy historii gry porownane z tym, co przeszlo przez prefiks

        // latka i pola wpisow gry (raz na proces)
        private static bool _hooksTried;
        private static string _wired = "nie zakladana";
        private static int _fieldsOk, _fieldsAll;
        private static AccessTools.FieldRef<CampaignInformationManager, bool> _fLaunched;
        private static AccessTools.FieldRef<BattleStartedLogEntry, Settlement> _fBattleSt;
        private static AccessTools.FieldRef<BattleStartedLogEntry, Hero> _fBattleLord;
        private static AccessTools.FieldRef<StartAllianceLogEntry, Kingdom> _fSa1, _fSa2;
        private static AccessTools.FieldRef<EndAllianceLogEntry, Kingdom> _fEa1, _fEa2;
        private static AccessTools.FieldRef<StartCallToWarAgreementLogEntry, Kingdom> _fSc1, _fSc2, _fSc3;
        private static AccessTools.FieldRef<EndCallToWarAgreementLogEntry, Kingdom> _fEc1, _fEc2, _fEc3;
        private static AccessTools.FieldRef<KingdomDestroyedLogEntry, Kingdom> _fKd;
        private static AccessTools.FieldRef<CharacterKilledLogEntry, KillCharacterAction.KillCharacterActionDetail> _fKill;

        internal static void Reset()
        {
            lock (Gate)
            {
                _q.Clear(); _seq = 0; _forts = null; _checkFromH = -1;
                _sDelayed = _sDelivered = 0; _stumbles = 0; _err.Clear(); _importN = -1; _importBad = 0;
                ZeroDay();
            }
        }

        private static void ZeroDay()
        {
            _dBkSeen = 0;
            _dSeen = _dWar = _dDelayed = _dNear = _dMine = _dNoPos = _dOff = _dDelivered = _dForced = _dBumped = _dLogSent = _dLogGot = _dStumbles = 0;
            _dDelaySum = _dDelayMax = _dLateSum = 0;
            Array.Clear(_dKind, 0, _dKind.Length);
            _dOther.Clear();
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++; _dStumbles++;
            try { if (_err.Add(where)) Log.Error("Ravens." + where, e); } catch { }
        }

        private static bool LogOn { get { var s = Settings.Current; return s != null && s.LogEnabled; } }
        private static double NowH { get { return CampaignTime.Now.ToHours; } }

        // ------------------------------------------------------------ wpiecie (ArmouryBehavior.RegisterEvents - jedna linia)
        internal static void RegisterEvents(CampaignBehaviorBase b)
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(b, delegate (CampaignGameStarter s) { try { SessionStart(); } catch (Exception e) { Log.Error("Ravens.SessionStart", e); } });
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(b, DayProfile.Hr("Ravens.Hourly", delegate { try { Hourly(); } catch (Exception e) { Log.Error("Ravens.Hourly", e); } }));
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(b, delegate { try { Daily(); } catch (Exception e) { Log.Error("Ravens.Daily", e); } });
        }

        private static void SessionStart()
        {
            EnsureHooks();
            int n; lock (Gate) { n = _q.Count; _checkFromH = NowH; ZeroDay(); }
            Log.Info(P + "start sesji | klucz Raven News " + (On ? "tak" : "nie") + " | latka " + _wired + " | pola wpisow gry " + _fieldsOk + "/" + _fieldsAll
                     + " | kruki w drodze " + n + (_importN >= 0 ? " (z zapisu " + _importN + ", bledne " + _importBad + ")" : " (brak klucza arm_ravens - nowa kampania albo stary zapis)")
                     + " | kruk " + (SpeedPerHour * 24f).ToString("0", Inv) + " jedn./dobe + " + HandHours.ToString("0", Inv) + " h, od razu do " + NearRange.ToString("0", Inv)
                     + " jedn., najwyzej " + (MaxHours / 24f).ToString("0", Inv) + " dob.");
        }

        private static void EnsureHooks()
        {
            if (_hooksTried) return;
            _hooksTried = true;
            try
            {
                _fLaunched = Field<CampaignInformationManager, bool>("_isSessionLaunched");
                _fBattleSt = Field<BattleStartedLogEntry, Settlement>("_settlement");
                _fBattleLord = Field<BattleStartedLogEntry, Hero>("_attackerLord");
                _fSa1 = Field<StartAllianceLogEntry, Kingdom>("_kingdom1"); _fSa2 = Field<StartAllianceLogEntry, Kingdom>("_kingdom2");
                _fEa1 = Field<EndAllianceLogEntry, Kingdom>("_kingdom1"); _fEa2 = Field<EndAllianceLogEntry, Kingdom>("_kingdom2");
                _fSc1 = Field<StartCallToWarAgreementLogEntry, Kingdom>("_callingKingdom"); _fSc2 = Field<StartCallToWarAgreementLogEntry, Kingdom>("_calledKingdom");
                _fSc3 = Field<StartCallToWarAgreementLogEntry, Kingdom>("_kingdomToCallToWarAgainst");
                _fEc1 = Field<EndCallToWarAgreementLogEntry, Kingdom>("_callingKingdom"); _fEc2 = Field<EndCallToWarAgreementLogEntry, Kingdom>("_calledKingdom");
                _fEc3 = Field<EndCallToWarAgreementLogEntry, Kingdom>("_kingdomToCallToWarAgainst");
                _fKd = Field<KingdomDestroyedLogEntry, Kingdom>("_kingdom");
                _fKill = Field<CharacterKilledLogEntry, KillCharacterAction.KillCharacterActionDetail>("_actionDetail");
                var m = AccessTools.DeclaredMethod(typeof(CampaignInformationManager), "NewLogEntryAdded", new[] { typeof(LogEntry) });
                if (m == null) _wired = "BRAK (CampaignInformationManager.NewLogEntryAdded nie znaleziona)";
                else if (_fLaunched == null) _wired = "BRAK (pole _isSessionLaunched nie znalezione)";
                else
                {
                    var h = new Harmony("com.jeff.armoury.ravens");
                    h.Patch(m, prefix: new HarmonyMethod(typeof(Ravens), nameof(ChatPrefix)));
                    _wired = "NewLogEntryAdded wpieta";
                    // BK: wiesc o rzezi po zdobyciu miasta (SiegeConsequences) - ta sama regula, wlasny try (blad BK nie rusza latki gry); brak BK = tylko wiesci gry
                    try
                    {
                        var bk = AccessTools.TypeByName("BannerKings.Behaviours.BKSettlementBehavior");
                        var sc = bk != null ? AccessTools.DeclaredMethod(bk, "SiegeConsequences") : null;
                        var sa = bk != null ? AccessTools.DeclaredMethod(bk, "OnSiegeAftermath") : null;
                        if (sc == null || sa == null) _wired += ", BK SiegeConsequences/OnSiegeAftermath BRAK";
                        else
                        {
                            h.Patch(sa, prefix: new HarmonyMethod(typeof(Ravens), nameof(BkSiegePrefix)), finalizer: new HarmonyMethod(typeof(Ravens), nameof(BkSiegeFinalizer)));
                            h.Patch(sc, transpiler: new HarmonyMethod(typeof(Ravens), nameof(BkSiegeTranspiler)));
                            _wired += ", BK po zdobyciu miasta wpieta (komunikatow podmienionych " + _bkSites + ")";
                        }
                    }
                    catch (Exception e) { _wired += ", BK BLAD (" + e.GetType().Name + ")"; Log.Error("Ravens.EnsureHooks(BK)", e); }
                }
            }
            catch (Exception e) { _wired = "BLAD (" + e.GetType().Name + ")"; Log.Error("Ravens.EnsureHooks", e); }
        }

        private static AccessTools.FieldRef<T, F> Field<T, F>(string name) where T : class
        {
            _fieldsAll++;
            try
            {
                if (AccessTools.DeclaredField(typeof(T), name) == null) return null;
                var r = AccessTools.FieldRefAccess<T, F>(name);
                if (r != null) _fieldsOk++;
                return r;
            }
            catch { return null; }
        }

        // ------------------------------------------------------------ latka gry
        /// <summary>Prefiks CampaignInformationManager.NewLogEntryAdded(LogEntry): wiesc wojenna z daleka - do kolejki krukow (false = gra nie pisze jej teraz
        /// na czat); wpis w historii gry juz jest (AddActionLog dodaje go przed ta metoda). Wszystko inne, klucz wylaczony, blad - gra jak dotad.</summary>
        public static bool ChatPrefix(CampaignInformationManager __instance, LogEntry __0)
        {
            try
            {
                var cn = __0 as IChatNotification;
                if (cn == null || __instance == null || !cn.IsVisibleNotification) return true;
                if (_fLaunched == null || !_fLaunched(__instance)) return true;   // gra i tak nic nie pokaze
                lock (Gate)
                {
                    _dSeen++;
                    if (!On) { _dOff++; return true; }
                    if (Campaign.Current == null || Hero.MainHero == null) return true;
                    int kind; Vec2 at; string key, from; bool mine, havePos;
                    if (!Classify(__0, out kind, out at, out key, out from, out mine, out havePos))
                    {
                        string tn = __0.GetType().Name; int c; _dOther.TryGetValue(tn, out c); _dOther[tn] = c + 1;
                        return true;
                    }
                    _dWar++;
                    if (mine) { _dMine++; return true; }
                    if (!havePos) { _dNoPos++; return true; }
                    return !Send(kind, at, key, from, cn.GetNotificationText().ToString(), Campaign.Current.Models.DiplomacyModel.GetNotificationColor(cn.NotificationType));
                }
            }
            catch (Exception e) { lock (Gate) Stumble("ChatPrefix", e); return true; }
        }

        /// <summary>Kruk w droge (pod Gate): false = wiesc idzie na czat od razu (gracz bez miejsca, blisko). Kolejnosc tej samej sprawy zachowana.</summary>
        private static bool Send(int kind, Vec2 at, string key, string from, string text, uint color)
        {
            Vec2 me;
            if (!PlayerPos(out me)) { _dNoPos++; return false; }
            float d = at.Distance(me);
            if (d <= NearRange) { _dNear++; return false; }
            double hours = Math.Min(MaxHours, HandHours + d / SpeedPerHour);
            double now = NowH, due = now + hours;
            bool bumped = false;
            for (int i = 0; i < _q.Count; i++) if (_q[i].Key == key && _q[i].Due > due) { due = _q[i].Due; bumped = true; }   // ta sama sprawa - nie przed wczesniejsza wiescia
            if (bumped) _dBumped++;
            if (string.IsNullOrEmpty(from)) from = NearestFort(at);
            var r = new Raven { Due = due, Sent = now, Color = color, Kind = kind, Dist = (int)Math.Round(d), Seq = ++_seq, Key = key ?? "", From = Clean(from), Text = Clean(text) };
            if (_q.Count >= QueueMax) { _dForced++; DeliverNow(PopEarliest(), "kolejka pelna"); }
            _q.Add(r);
            _dDelayed++; _sDelayed++; _dKind[kind]++;
            _dDelaySum += due - now; if (due - now > _dDelayMax) _dDelayMax = due - now;
            if (LogOn && _dLogSent < LogMax)
            {
                _dLogSent++;
                Log.Info(P + "kruk wyslany | dzien " + (int)CampaignTime.Now.ToDays + " | " + KName[kind] + " | z " + r.From + " | odleglosc " + r.Dist
                         + " jedn. | dotrze za " + (due - now).ToString("0", Inv) + " h" + (bumped ? " (po wczesniejszej wiesci o tym samym)" : "") + " | " + Short(r.Text));
            }
            return true;
        }

        // ------------------------------------------------------------ BK: "N people have been killed in the siege aftermath of X" (BKSettlementBehavior.SiegeConsequences
        // - jedyne wywolanie z BKSettlementBehavior.OnSiegeAftermath, nasluch OnSiegeAftermathAppliedEvent, kazde zdobyte miasto swiata; InformationManager.DisplayMessage
        // wprost, bez wpisu historii). Prefiks OnSiegeAftermath zapamietuje osade, zdobywce i poprzedniego pana (miasto stracone przez rod gracza = od razu), transpiler
        // podmienia to jedno DisplayMessage w SiegeConsequences na BkDisplay (ta sama regula krukow), finalizer czysci. Ludnosc BK liczy sie jak dotad.
        [ThreadStatic] private static Settlement _bkSt;
        [ThreadStatic] private static MobileParty _bkBy;
        [ThreadStatic] private static Clan _bkPrev;
        private static int _bkSites, _dBkSeen;

        public static void BkSiegePrefix(MobileParty __0, Settlement __1, Clan __3) { _bkBy = __0; _bkSt = __1; _bkPrev = __3; }
        public static Exception BkSiegeFinalizer(Exception __exception) { _bkSt = null; _bkBy = null; _bkPrev = null; return __exception; }

        public static IEnumerable<HarmonyLib.CodeInstruction> BkSiegeTranspiler(IEnumerable<HarmonyLib.CodeInstruction> ins)
        {
            var disp = AccessTools.Method(typeof(InformationManager), "DisplayMessage", new[] { typeof(InformationMessage) });
            var ours = AccessTools.Method(typeof(Ravens), nameof(BkDisplay));
            foreach (var c in ins)
            {
                if (disp != null && ours != null && c.Calls(disp)) { c.operand = ours; _bkSites++; }
                yield return c;
            }
        }

        public static void BkDisplay(InformationMessage msg)
        {
            try
            {
                var st = _bkSt;
                if (st != null && msg != null && Campaign.Current != null && Hero.MainHero != null)
                {
                    lock (Gate)
                    {
                        _dBkSeen++;
                        if (!On) _dOff++;
                        else
                        {
                            _dWar++;
                            var by = _bkBy;
                            if (MineS(st) || (by != null && Mine(by.LeaderHero)) || (_bkPrev != null && _bkPrev == Clan.PlayerClan)) _dMine++;
                            else if (Send(KAftermath, st.GetPosition2D, "s:" + st.StringId, Name(st), msg.Information, msg.Color.ToUnsignedInteger())) return;
                        }
                    }
                }
            }
            catch (Exception e) { lock (Gate) Stumble("BkDisplay", e); }
            InformationManager.DisplayMessage(msg);
        }

        // ------------------------------------------------------------ rodzaj wiesci, miejsce, sprawa
        private static bool Mine(Hero h) { return h != null && (h == Hero.MainHero || (h.Clan != null && h.Clan == Clan.PlayerClan)); }
        private static bool MineF(IFaction f) { return f != null && (f == Clan.PlayerClan || f == Hero.MainHero.MapFaction); }
        private static bool MineS(Settlement s) { return s != null && s.OwnerClan != null && s.OwnerClan == Clan.PlayerClan; }

        private static bool Classify(LogEntry log, out int kind, out Vec2 at, out string key, out string from, out bool mine, out bool havePos)
        {
            kind = 0; at = Vec2.Zero; key = ""; from = null; mine = false; havePos = false;
            var bs = log as BesiegeSettlementLogEntry;
            if (bs != null)
            {
                kind = KSiege;
                mine = MineS(bs.Settlement) || (bs.OwnerClanBeforeBesiege != null && bs.OwnerClanBeforeBesiege == Clan.PlayerClan) || Mine(bs.BesiegerHero);
                return Place(bs.Settlement, ref at, ref key, ref from, ref havePos);
            }
            var bt = log as BattleStartedLogEntry;
            if (bt != null)
            {
                kind = KAssault;
                var st = _fBattleSt != null ? _fBattleSt(bt) : null;
                mine = MineS(st) || (_fBattleLord != null && Mine(_fBattleLord(bt)));
                return Place(st, ref at, ref key, ref from, ref havePos);
            }
            var dw = log as DeclareWarLogEntry;
            if (dw != null) { kind = KWar; Factions(dw.Faction1, dw.Faction2, null, ref at, ref key, ref from, ref mine, ref havePos); return true; }
            var mp = log as MakePeaceLogEntry;
            if (mp != null) { kind = KPeace; Factions(mp.Faction1, mp.Faction2, null, ref at, ref key, ref from, ref mine, ref havePos); return true; }
            var sa = log as StartAllianceLogEntry;
            if (sa != null) { kind = KAlly; Factions(Get(_fSa1, sa), Get(_fSa2, sa), null, ref at, ref key, ref from, ref mine, ref havePos); return true; }
            var ea = log as EndAllianceLogEntry;
            if (ea != null) { kind = KAlly; Factions(Get(_fEa1, ea), Get(_fEa2, ea), null, ref at, ref key, ref from, ref mine, ref havePos); return true; }
            var sc = log as StartCallToWarAgreementLogEntry;
            if (sc != null) { kind = KCall; Factions(Get(_fSc1, sc), Get(_fSc2, sc), Get(_fSc3, sc), ref at, ref key, ref from, ref mine, ref havePos); return true; }
            var ec = log as EndCallToWarAgreementLogEntry;
            if (ec != null) { kind = KCall; Factions(Get(_fEc1, ec), Get(_fEc2, ec), Get(_fEc3, ec), ref at, ref key, ref from, ref mine, ref havePos); return true; }
            var kd = log as KingdomDestroyedLogEntry;
            if (kd != null) { kind = KFall; Factions(Get(_fKd, kd), null, null, ref at, ref key, ref from, ref mine, ref havePos); return true; }
            var ck = log as CharacterKilledLogEntry;
            if (ck != null)
            {
                if (_fKill == null) return false;
                var det = _fKill(ck);
                if (det != KillCharacterAction.KillCharacterActionDetail.DiedInBattle && det != KillCharacterAction.KillCharacterActionDetail.WoundedInBattle
                    && det != KillCharacterAction.KillCharacterActionDetail.Executed && det != KillCharacterAction.KillCharacterActionDetail.ExecutionAfterMapEvent) return false;
                kind = KDeath;
                mine = Mine(ck.Victim) || Mine(ck.Killer) || (ck.VictimClan != null && ck.VictimClan == Clan.PlayerClan);
                havePos = HeroPos(ck.Killer, out at) || HeroPos(ck.Victim, out at);
                key = "h:" + Id(ck.Victim);
                return true;
            }
            var tp = log as TakePrisonerLogEntry;
            if (tp != null)
            {
                kind = KCapture;
                mine = Mine(tp.Prisoner) || Mine(tp.CapturerHero) || Mine(tp.CapturerMobilePartyLeader) || MineS(tp.CapturerSettlement);
                if (tp.CapturerSettlement != null) { at = tp.CapturerSettlement.GetPosition2D; havePos = true; from = Name(tp.CapturerSettlement); }
                else havePos = HeroPos(tp.CapturerMobilePartyLeader, out at) || HeroPos(tp.Prisoner, out at);
                key = "h:" + Id(tp.Prisoner);
                return true;
            }
            var fr = log as EndCaptivityLogEntry;
            if (fr != null)
            {
                kind = KFreed;
                mine = Mine(fr.Prisoner);
                havePos = HeroPos(fr.Prisoner, out at);
                key = "h:" + Id(fr.Prisoner);
                return true;
            }
            var ga = log as GatherArmyLogEntry;
            if (ga != null)
            {
                kind = KArmy;
                mine = Mine(ga.ArmyLeader);
                if (ga.GatheringPoint != null) { at = ga.GatheringPoint.Position.ToVec2(); havePos = true; }
                else havePos = HeroPos(ga.ArmyLeader, out at);
                key = "a:" + Id(ga.ArmyLeader);
                return true;
            }
            return false;
        }

        private static K Get<T, K>(AccessTools.FieldRef<T, K> f, T o) where T : class where K : class { return f != null && o != null ? f(o) : null; }

        private static bool Place(Settlement st, ref Vec2 at, ref string key, ref string from, ref bool havePos)
        {
            if (st == null) return true;   // wiesc wojenna bez osady - od razu (bez miejsca)
            at = st.GetPosition2D; havePos = true; key = "s:" + st.StringId; from = Name(st);
            return true;
        }

        /// <summary>Wiesc krolestw: miejsce = siedziba wodza blizsza graczowi (jak plotki: HomeSettlement wodza, inaczej srodek ziem); sprawa = para/trojka id.</summary>
        private static void Factions(IFaction a, IFaction b, IFaction c, ref Vec2 at, ref string key, ref string from, ref bool mine, ref bool havePos)
        {
            mine = MineF(a) || MineF(b) || MineF(c);
            var ids = new List<string>(3);
            Vec2 me; bool haveMe = PlayerPos(out me);
            float best = float.MaxValue;
            foreach (var f in new[] { a, b, c })
            {
                if (f == null) continue;
                ids.Add(f.StringId ?? "");
                var seat = Seat(f);
                if (seat == null) continue;
                float d = haveMe ? seat.GetPosition2D.Distance(me) : 0f;
                if (!havePos || d < best) { best = d; at = seat.GetPosition2D; from = Name(seat); havePos = true; }
            }
            ids.Sort(StringComparer.Ordinal);
            key = "f:" + string.Join("+", ids.ToArray());
        }

        private static Settlement Seat(IFaction f)
        {
            try
            {
                var l = f.Leader;
                if (l != null && l.HomeSettlement != null) return l.HomeSettlement;
                return f.FactionMidSettlement ?? f.InitialHomeSettlement;
            }
            catch { return null; }
        }

        private static bool HeroPos(Hero h, out Vec2 p)
        {
            p = Vec2.Zero;
            if (h == null) return false;
            if (h.CurrentSettlement != null) { p = h.CurrentSettlement.GetPosition2D; return true; }
            if (h.PartyBelongedTo != null) { p = h.PartyBelongedTo.GetPosition2D; return true; }
            if (h.PartyBelongedToAsPrisoner != null) { p = h.PartyBelongedToAsPrisoner.Position.ToVec2(); return true; }
            if (h.LastKnownClosestSettlement != null) { p = h.LastKnownClosestSettlement.GetPosition2D; return true; }
            return false;
        }

        /// <summary>Gdzie jest gracz: osada, druzyna, w ktorej jest jencem, albo druzyna gracza.</summary>
        private static bool PlayerPos(out Vec2 p)
        {
            p = Vec2.Zero;
            var h = Hero.MainHero;
            if (h == null) return false;
            if (h.CurrentSettlement != null) { p = h.CurrentSettlement.GetPosition2D; return true; }
            if (h.IsPrisoner && h.PartyBelongedToAsPrisoner != null) { p = h.PartyBelongedToAsPrisoner.Position.ToVec2(); return true; }
            var mp = MobileParty.MainParty;
            if (mp != null) { p = mp.GetPosition2D; return true; }
            return false;
        }

        private static string NearestFort(Vec2 at)
        {
            if (_forts == null)
            {
                _forts = new List<Settlement>();
                foreach (var s in Settlement.All) if (s != null && s.IsFortification) _forts.Add(s);
            }
            Settlement best = null; float bd = float.MaxValue;
            foreach (var s in _forts)
            {
                float d = s.GetPosition2D.Distance(at);
                if (d < bd) { bd = d; best = s; }
            }
            return best != null ? Name(best) : "afar";
        }

        private static string Name(Settlement s) { try { return s != null && s.Name != null ? s.Name.ToString() : ""; } catch { return ""; } }
        private static string Id(Hero h) { return h != null && h.StringId != null ? h.StringId : "-"; }

        private static string Clean(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new StringBuilder(s.Length);
            foreach (char ch in s) sb.Append(ch == '\t' || ch == '\n' || ch == '\r' ? ' ' : ch);
            return sb.ToString();
        }

        private static string Short(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            // linki encyklopedii (<a ...><b>Nazwa</b></a>) - w logu tylko tekst
            var sb = new StringBuilder(s.Length);
            bool tag = false;
            foreach (char ch in s) { if (ch == '<') tag = true; else if (ch == '>') tag = false; else if (!tag) sb.Append(ch); }
            return sb.Length > 140 ? sb.ToString(0, 140) + "..." : sb.ToString();
        }

        // ------------------------------------------------------------ doreczenie (co godzine gry; klucz wylaczony = wszystko od razu)
        internal static void Hourly()
        {
            List<Raven> due = null;
            lock (Gate)
            {
                if (_q.Count == 0 || Campaign.Current == null) return;
                double now = NowH;
                bool all = !On;
                for (int i = _q.Count - 1; i >= 0; i--)
                {
                    if (!all && _q[i].Due > now) continue;
                    if (due == null) due = new List<Raven>();
                    due.Add(_q[i]); _q.RemoveAt(i);
                }
                if (due == null) return;
                due.Sort(delegate (Raven x, Raven y) { int c = x.Due.CompareTo(y.Due); return c != 0 ? c : x.Seq.CompareTo(y.Seq); });
                foreach (var r in due) DeliverNow(r, all ? "klucz wylaczony" : null);
            }
        }

        private static Raven PopEarliest()
        {
            int bi = 0;
            for (int i = 1; i < _q.Count; i++) if (_q[i].Due < _q[bi].Due || (_q[i].Due == _q[bi].Due && _q[i].Seq < _q[bi].Seq)) bi = i;
            var r = _q[bi]; _q.RemoveAt(bi);
            return r;
        }

        private static void DeliverNow(Raven r, string why)
        {
            if (r == null) return;
            try
            {
                double now = NowH, age = Math.Max(0.0, now - r.Sent);
                string text = "A raven from " + (string.IsNullOrEmpty(r.From) ? "afar" : r.From) + ", " + Age(age) + " on the wing: " + r.Text;
                InformationManager.DisplayMessage(new InformationMessage(text, Color.FromUint(r.Color)));
                _dDelivered++; _sDelivered++;
                if (now > r.Due) _dLateSum += now - r.Due;
                if (LogOn && _dLogGot < LogMax)
                {
                    _dLogGot++;
                    Log.Info(P + "kruk doreczony | dzien " + (int)CampaignTime.Now.ToDays + " | " + KName[Math.Max(0, Math.Min(KN - 1, r.Kind))] + " | z " + r.From + " | odleglosc " + r.Dist
                             + " jedn. | w drodze " + age.ToString("0", Inv) + " h (plan " + Math.Max(0.0, r.Due - r.Sent).ToString("0", Inv) + " h)" + (why != null ? " | " + why : "") + " | " + Short(r.Text));
                }
            }
            catch (Exception e) { Stumble("DeliverNow", e); }
        }

        private static string Age(double h)
        {
            if (h < 1.5) return "an hour";
            if (h < 36) return ((int)Math.Round(h)).ToString(Inv) + " hours";
            int d = (int)Math.Round(h / 24.0);
            return d <= 1 ? "a day" : d.ToString(Inv) + " days";
        }

        // ------------------------------------------------------------ doba
        internal static void Daily()
        {
            lock (Gate)
            {
                try
                {
                    if (LogOn && Campaign.Current != null)
                    {
                        double now = NowH, next = double.MaxValue;
                        foreach (var r in _q) if (r.Due < next) next = r.Due;
                        var kinds = new StringBuilder();
                        for (int k = 1; k < KN; k++) if (_dKind[k] > 0) kinds.Append(kinds.Length > 0 ? ", " : "").Append(KName[k]).Append(' ').Append(_dKind[k]);
                        var other = new List<KeyValuePair<string, int>>(_dOther);
                        other.Sort(delegate (KeyValuePair<string, int> x, KeyValuePair<string, int> y) { return y.Value.CompareTo(x.Value); });
                        var os = new StringBuilder();
                        for (int i = 0; i < other.Count && i < 10; i++) os.Append(i > 0 ? ", " : "").Append(other[i].Key).Append(' ').Append(other[i].Value);
                        // kontrola latki: widoczne wpisy czatu w historii gry od poprzedniej doby (te same, ktore gra pisze na czat) - ile przeszlo przez prefiks
                        int hist = -1;
                        if (_checkFromH >= 0)
                        {
                            hist = 0;
                            var logs = Campaign.Current.LogEntryHistory.GameActionLogs;
                            for (int i = logs.Count - 1; i >= 0; i--)
                            {
                                var le = logs[i];
                                if (le == null) continue;
                                if (le.GameTime.ToHours < _checkFromH) break;
                                try { var c = le as IChatNotification; if (c != null && c.IsVisibleNotification) hist++; } catch { }
                            }
                        }
                        _checkFromH = now;
                        Log.Info(P + "dzien " + (int)CampaignTime.Now.ToDays + " | klucz Raven News " + (On ? "tak" : "nie") + " | latka " + _wired
                                 + " | wiesci gry na czacie " + _dSeen + " (+ BK po zdobyciu miasta " + _dBkSeen + "), wojenne " + _dWar + ": kruki wyslane " + _dDelayed
                                 + (_dDelayed > 0 ? " (sr. " + (_dDelaySum / _dDelayed).ToString("0", Inv) + " h, najdluzej " + _dDelayMax.ToString("0", Inv) + " h; po wczesniejszej wiesci o tym samym " + _dBumped + ")" : "")
                                 + ", od razu: dotyczy gracza " + _dMine + ", blisko (do " + NearRange.ToString("0", Inv) + " jedn.) " + _dNear + ", bez miejsca " + _dNoPos
                                 + (_dOff > 0 ? ", klucz wylaczony " + _dOff : "")
                                 + " | rodzaje wyslanych: " + (kinds.Length > 0 ? kinds.ToString() : "-")
                                 + " | doreczone " + _dDelivered + (_dDelivered > 0 ? " (sr. spoznienie godziny " + (_dLateSum / _dDelivered).ToString("0.0", Inv) + " h)" : "")
                                 + (_dForced > 0 ? ", od razu z pelnej kolejki " + _dForced : "")
                                 + " | w drodze " + _q.Count + (_q.Count > 0 ? " (najblizszy za " + Math.Max(0.0, next - now).ToString("0", Inv) + " h)" : "")
                                 + " | inne wiesci (bez zmian): " + (os.Length > 0 ? os.ToString() : "-")
                                 + (hist >= 0 ? " | kontrola: widocznych wpisow w historii gry " + hist + ", przez latke " + _dSeen + (hist >= 5 && _dSeen == 0 ? " - LATKA NIE WIDZI WPISOW" : "") : "")
                                 + " | sesja: wyslane " + _sDelayed + ", doreczone " + _sDelivered
                                 + (_stumbles > 0 ? " | potkniecia " + _stumbles + " (dzis " + _dStumbles + ")" : "") + ".");
                    }
                }
                catch (Exception e) { Stumble("Daily", e); }
                ZeroDay();
            }
        }

        // ------------------------------------------------------------ zapis (klucz arm_ravens): "v1" + linie "due\tsent\tcolor\tkind\tdist\tkey\tfrom\ttext"
        internal static string Export()
        {
            lock (Gate)
            {
                try
                {
                    if (_q.Count == 0) return "";
                    var l = new List<Raven>(_q);
                    l.Sort(delegate (Raven x, Raven y) { return x.Seq.CompareTo(y.Seq); });
                    var sb = new StringBuilder(l.Count * 160 + 4);
                    sb.Append("v1");
                    foreach (var r in l)
                    {
                        sb.Append('\n').Append(r.Due.ToString("R", Inv)).Append('\t').Append(r.Sent.ToString("R", Inv)).Append('\t').Append(r.Color.ToString(Inv))
                          .Append('\t').Append(r.Kind.ToString(Inv)).Append('\t').Append(r.Dist.ToString(Inv)).Append('\t').Append(Clean(r.Key))
                          .Append('\t').Append(Clean(r.From)).Append('\t').Append(Clean(r.Text));
                    }
                    return sb.ToString();
                }
                catch (Exception e) { Stumble("Export", e); return ""; }
            }
        }

        internal static void Import(string data)
        {
            lock (Gate)
            {
                _q.Clear(); _seq = 0; _importN = 0; _importBad = 0;
                try
                {
                    if (string.IsNullOrEmpty(data)) return;
                    var lines = data.Split('\n');
                    if (lines.Length == 0 || lines[0] != "v1") { _importBad = lines.Length; return; }
                    for (int i = 1; i < lines.Length; i++)
                    {
                        var x = lines[i].Split('\t');
                        double due, sent; uint color; int kind, dist;
                        if (x.Length == 8 && double.TryParse(x[0], NumberStyles.Float, Inv, out due) && double.TryParse(x[1], NumberStyles.Float, Inv, out sent)
                            && uint.TryParse(x[2], NumberStyles.Integer, Inv, out color) && int.TryParse(x[3], NumberStyles.Integer, Inv, out kind)
                            && int.TryParse(x[4], NumberStyles.Integer, Inv, out dist))
                        {
                            _q.Add(new Raven { Due = due, Sent = sent, Color = color, Kind = Math.Max(0, Math.Min(KN - 1, kind)), Dist = dist, Seq = ++_seq, Key = x[5], From = x[6], Text = x[7] });
                            _importN++;
                        }
                        else _importBad++;
                    }
                }
                catch (Exception e) { Stumble("Import", e); }
            }
        }
    }
}
