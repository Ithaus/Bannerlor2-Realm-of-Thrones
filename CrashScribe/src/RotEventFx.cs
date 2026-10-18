using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;

namespace CrashScribe
{
    /// <summary>
    /// SKUTKI WYDARZEN FABULY ROT (noc 17/18.10, paczka rot-wydarzenia; RAPORT-4-LATA rozdz. 2 pkt 6 "Joffrey zyje po Purpurowych Godach").
    /// Dowod (test 4 lat, logi D:\Backup-Bannerlord\logs\2026-10-15-6lat, economy-2026-10-14_20-19-35.csv, wiersze "krol" krolestwa KL):
    ///  - d1083 FABULA: JoffreyEvent WYSTARTOWALO; d1084 wydarzenie zakonczone (TotalEvents > 0), glowa rodu "Baratheon, Joffrey" = Cersei Lannister
    ///    (109920-109923), ale druzyna "Joffrey Baratheon, King of ... Party" chodzi po mapie do d1087 (linie NocnyMarsz Armoury), a od 109924 krolem
    ///    jest "Meryn Trant, King of House Baratheon of King's Landing" (rycerz Gwardii Krolewskiej!) - Cersei bez tytulu.
    /// Przyczyny [K = sprawdzone w dekompilacji]:
    ///  (1) [K] gra 1.4.8 KillCharacterAction.ApplyInternal: bohater, ktorego druzyna jest w MapEvent albo SiegeEvent, dostaje tylko ZNAK SMIERCI
    ///      (AddDeathMark) i zyje dalej; zabija go dopiero AgingCampaignBehavior.DailyTickHero, gdy druzyna nie ma bitwy ani oblezenia. ROT JoffreyEvent
    ///      wola ApplyByMurder i od razu CompleteEvent - krol "zamordowany na weselu" dowodzi oblezeniem (u nas oblezenia trwaja tygodnie - OB1-OB6).
    ///  (2) [K] BK BKTitleBehavior.OnHeroKilled (BeforeHeroKilledEvent): smierc posiadacza tytulu suwerena -> SuccessionHelper.ApplySovereignSuccession;
    ///      KL w pliku tytulow: Hereditary / Primogeniture / Agnatic -> CalculateSuccessionLine.First() (kobieta wykluczona) -> InheritTitle + ChangeRulingClan
    ///      + Clan.SetLeader(dziedzic) - glowa rodu wyznaczona przez ROT (ChangeClanLeaderAction tuz przed zabojstwem) traci rod i tron. Reszta tytulow:
    ///      InheritanceHelper.ApplyInheritanceAllTitles -> dziedzic BK (nie glowa z ROT). To samo w BlackwaterEvent (Stannis wygral), RedWedding (Bran -
    ///      tu BK trafil w Brana), Karstark (Harold), Ramsay.
    ///  (3) [K] ROT JonEvent: ChangeRulingClanAction(Nights Watch, Tollett) bez sprawdzenia, czy rod jest w krolestwie - w tescie Tollett byl w
    ///      "Confederation of Snow (Nights Watch Rebels)" (bunt Diplomacy) i Nocna Straz zostala z wladca spoza krolestwa (CSV: brak wiersza "krol"
    ///      Nights Watch od 110246 do konca).
    /// Nasze bramki NIE blokuja zabojstw: CanHeroDieEvent ma tylko ROT (Biali Wedrowcy) i autotest (bohater gracza); Armoury nie ma CanHeroDie ani
    /// starzenia; StoryThrone (AddDecision, powrot na tron) nie dotyka KL po smierci Joffreya (wladca ze startu martwy); Fabula przepuscila wydarzenie;
    /// RealisticCaptivity EndCaptivityAction - tylko postfiks gracza. ROT RooseDeathEvent ma ConditionsHold = false, wiec ROT konczy go bez skutkow
    /// (blad ROT - tylko linia "zakonczone ... skutkow 0", fabuly nie ruszamy).
    /// Naprawa (klucze w CrashScribe.settings.xml, domyslnie true; RotEventFx = false -> nic nie wpiete, stan sprzed paczki):
    ///  RotEventFx (glowny): kontekst wydarzen ROT (prefiks + finalizer na metodach ROT.Events, ktore wolaja akcje skutkow) i linie
    ///      "ROT wydarzenie: <id> skutek <rodzaj> | <kto/co> | wykonany / odroczony / zablokowany przez <kto> / naprawiony / bez skutku | dzien N";
    ///      linia "ROT wydarzenie: <id> zakonczone" (CompleteEvent) i linia doby / 7 dob "ROT wydarzenia (RX)".
    ///  RotEventDeathNow: smierc z wydarzenia ROT odroczona znakiem smierci - wykonywana (KillCharacterAction.ApplyByDeathMark, ta sama droga co gra
    ///      po bitwie oblezenia - MapEventSide.HandleMapEventEndForPartyInternal) w pierwszej godzinie, w ktorej druzyna nie jest w bitwie (oblezenie
    ///      nie czeka); nie w oblezeniu fabuly ROT (SiegeLeaderParty), nie przy spotkaniu / rozmowie / oblezeniu gracza.
    ///  RotEventHeirKeepsTitles: glowa rodu wyznaczona przez wydarzenie ROT dziedziczy po zabitym jego tytuly BK - suweren (prefiks
    ///      SuccessionHelper.ApplySovereignSuccession: TitleManager.InheritTitle jak BK ResolveSuccession, bez linii sukcesji i wyborow) i reszta
    ///      (prefiks InheritanceHelper.ApplyInheritanceAllTitles) - gdy ta glowa zyje, jest w rodzie i nim rzadzi.
    ///  RotEventRulerInKingdom: ChangeRulingClanAction z wydarzenia ROT dla rodu spoza krolestwa - rod niezalezny najpierw dolacza
    ///      (ChangeKingdomAction.ApplyByJoinToKingdom), rod w innym krolestwie / wybity / najemny / gracza - przekazanie tronu pominiete.
    ///      Recenzja: ROT wybija rod, od ktorego tron mial odejsc, linie po przekazaniu (JeorMutinyEvent: DestroyClan Mormontow, BastardsEvent: DestroyClan
    ///      Snow) - gra przy DestroyClanAction tronu nie przenosi (Kingdom.RemoveClanInternal nie rusza RulingClan; sukcesja w KillCharacterAction wymaga
    ///      victim.Clan.Kingdom != null). Dlatego po zakonczeniu metody wydarzenia (finalizer kontekstu, glebokosc 0) krolestwo z zablokowanym
    ///      przekazaniem jest sprawdzane: rod panujacy wybity / poza krolestwem / bez zywej glowy -> tron dla pierwszego rodu krolestwa zdolnego do rzadow
    ///      (DiplomacyModel.IsClanEligibleToBecomeRuler, nie gracza, nie najemny - jak galaz else ROT JeorMutinyEvent); gdy takiego nie ma - dla rodu z ROT
    ///      (jak chcial ROT). Rod panujacy wciaz w porzadku (np. wybrany przez gre po smierci wladcy w tym wydarzeniu) - tron zostaje (StoryThrone tez go trzyma).
    /// Zapis: klucz "cs_rotfx_v1" (lista tekstow: czekajacy na smierc i nastepcy z ROT, najwyzej 2 x 64 wpisy); stary zapis bez klucza - pusta lista.
    /// </summary>
    internal static class RotEventFx
    {
        private const string HarmonyId = "com.jeff.crashscribe";
        private const int MaxEntries = 64;
        private const int HeirKeepDays = 60;      // wpis nastepcy po smierci starej glowy (linia "utracony" i stan w linii doby)
        private const int PendingGiveUpHours = 24 * 30;

        // ------------------------------------------------------------ kontekst wydarzenia ROT
        private static int _depth, _actDepth;
        private static string _ev;

        // ------------------------------------------------------------ wpiecie
        private static bool _installed;
        private static string _state = "nie wpiete";
        private static int _ctxMethods, _ctxTypes, _actOk, _actWant, _bkOk, _bkWant, _endHooks;
        private static MethodInfo _miInherit, _piTm;
        private static PropertyInfo _piCfgInst, _piSovereign, _piDeJure, _piContract, _piSucc, _piFullName;
        private static readonly List<KeyValuePair<FieldInfo, FieldInfo>> _rotSieges = new List<KeyValuePair<FieldInfo, FieldInfo>>();   // (static Instance, SiegeLeaderParty)
        private static PropertyInfo _piEvActive;

        // ------------------------------------------------------------ liczniki (V: 0 wykonany, 1 odroczony, 2 zablokowany, 3 naprawiony, 4 bez skutku)
        private static readonly int[] _d = new int[5], _t = new int[5];
        private static readonly Dictionary<string, int[]> _perEvent = new Dictionary<string, int[]>();
        private static int _stumbles, _killedByUs, _diedByGame, _lastLineDay = -1, _lastEndHour = -1;
        private static object _lastEnd;
        private static double _costMs;
        private static readonly string[] VName = { "wykonane", "odroczone", "zablokowane", "naprawione", "bez skutku" };

        // ------------------------------------------------------------ zapis
        internal static List<string> Saved = new List<string>();

        private sealed class Pending
        {
            public string Hero, Event, Detail, Why;
            public double Hour;
            public int Tries;
            public Hero Ref;
        }

        private sealed class Heir
        {
            public string Clan, NewLeader, OldLeader, Event;
            public int Day, DeadDay = -1;
            public bool Titles, Lost;
            public Hero NewRef, OldRef;
        }

        /// <summary>Przekazanie tronu zablokowane w biezacym wywolaniu wydarzenia ROT - sprawdzane po wyjsciu z metody wydarzenia (bez zapisu - zyje w jednym stosie wywolan).</summary>
        private sealed class Blk
        {
            public Kingdom K;
            public Clan Target;
            public string Ev, Why;
        }

        private static readonly List<Pending> _pending = new List<Pending>();
        private static readonly List<Heir> _heirs = new List<Heir>();
        private static readonly List<Blk> _blocked = new List<Blk>();

        public sealed class St
        {
            public object A, B, C;
            public string Pre;
            public bool Flag;
            public KillCharacterAction.KillCharacterActionDetail Mark;
        }

        private static readonly St Nested = new St();

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            if (_stumbles <= 5 || _stumbles % 100 == 0) { try { Scribe.Report("CrashScribe", e, "RotEventFx." + where + " (potkniecie " + _stumbles + ")", null); } catch { } }
        }

        internal static bool On { get { return Config.RotEventFx; } }

        internal static void ResetSession()
        {
            Array.Clear(_d, 0, _d.Length); Array.Clear(_t, 0, _t.Length);
            _perEvent.Clear(); _pending.Clear(); _heirs.Clear(); Saved = new List<string>();
            _killedByUs = _diedByGame = 0; _lastLineDay = -1; _lastEnd = null; _lastEndHour = -1; _costMs = 0;
            _depth = _actDepth = 0; _ev = null;
            _stumbles = 0; _blocked.Clear();   // recenzja: potkniecia liczone na sesje (RX-1 czyta ostatnia linie RX - bez potkniec z poprzedniej kampanii / wczytania)
        }

        private static int Day()
        {
            try { return (int)(CampaignTime.Now - Campaign.Current.Models.CampaignTimeModel.CampaignStartTime).ToDays; }
            catch { return (int)CampaignTime.Now.ToDays; }
        }

        private static string T(object o) { try { return o != null ? o.ToString() : "-"; } catch { return "?"; } }
        private static string Nm(Hero h) { return h == null ? "-" : T(h.Name) + " (" + h.StringId + ")"; }
        private static string Nm(Clan c) { return c == null ? "-" : T(c.Name) + " (" + c.StringId + ")"; }
        private static string Nm(Kingdom k) { return k == null ? "-" : T(k.Name) + " (" + k.StringId + ")"; }

        private static Hero HeroById(string id, ref Hero cache)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (cache != null && cache.StringId == id) return cache;
            try { cache = Hero.FindFirst(x => x != null && x.StringId == id); } catch { cache = null; }
            return cache;
        }

        private static Clan ClanById(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            try { foreach (var c in Clan.All) if (c != null && c.StringId == id) return c; } catch { }
            return null;
        }

        // ------------------------------------------------------------ linia skutku
        private static void Effect(string ev, int v, string kind, string obj, string verdict)
        {
            try
            {
                ev = string.IsNullOrEmpty(ev) ? "?" : ev;
                _d[v]++; _t[v]++;
                int[] pe;
                if (!_perEvent.TryGetValue(ev, out pe)) { pe = new int[5]; _perEvent[ev] = pe; }
                pe[v]++;
                Scribe.Line("ROT wydarzenie: " + ev + " skutek " + kind + " | " + obj + " | " + verdict + " | dzien " + Day() + " (doba " + (int)CampaignTime.Now.ToDays + ")");
            }
            catch (Exception e) { Stumble("Effect", e); }
        }

        // ------------------------------------------------------------ wpiecie (raz na proces) i linia startu sesji
        internal static void OnSession()
        {
            if (!On) return;
            try { Install(); } catch (Exception e) { _state = "wyjatek " + e.GetType().Name; Stumble("Install", e); }
            try
            {
                Scribe.Line("ROT wydarzenia (RX): start sesji | " + _state + " | klucze RotEventFx TAK, RotEventDeathNow " + (Config.RotEventDeathNow ? "TAK" : "NIE")
                            + ", RotEventHeirKeepsTitles " + (Config.RotEventHeirKeepsTitles ? "TAK" : "NIE") + ", RotEventRulerInKingdom " + (Config.RotEventRulerInKingdom ? "TAK" : "NIE")
                            + " | z zapisu: czekaja na smierc " + _pending.Count + ", nastepcy z ROT " + _heirs.Count + ".");
            }
            catch (Exception e) { Stumble("StartLine", e); }
        }

        private static void Install()
        {
            if (_installed) return;
            _installed = true;
            var h = new Harmony(HarmonyId);
            // KOLEJNOSC (WorkshopTrade 1372, SpareHorses K2): najpierw akcje gry (wolane), potem metody ROT (wolajace) - opakowanie metody ROT kompiluje sie
            // przy wpieciu i nie wklei (inline) malej akcji, ktora latki jeszcze nie ma (ChangeRulingClanAction.Apply ma 7 bajtow IL).
            Act(h, typeof(KillCharacterAction), "ApplyInternal", nameof(KillPre), nameof(KillPost), false);
            Act(h, typeof(ChangeClanLeaderAction), "ApplyInternal", nameof(LeaderPre), nameof(LeaderPost), false);
            Act(h, typeof(ChangeRulingClanAction), "Apply", nameof(RulingPre), nameof(RulingPost), true);
            Act(h, typeof(ChangeKingdomAction), "ApplyInternal", nameof(KingdomPre), nameof(KingdomPost), false);
            Act(h, typeof(ChangeOwnerOfSettlementAction), "ApplyInternal", nameof(OwnerPre), nameof(OwnerPost), false);
            Act(h, typeof(DestroyClanAction), "ApplyInternal", nameof(ClanGonePre), nameof(ClanGonePost), false);
            Act(h, typeof(DestroyKingdomAction), "ApplyInternal", nameof(KingdomGonePre), nameof(KingdomGonePost), false);
            Act(h, typeof(EndCaptivityAction), "ApplyInternal", nameof(FreePre), nameof(FreePost), false);
            Act(h, typeof(TeleportHeroAction), "ApplyInternal", nameof(MovePre), nameof(MovePost), false);

            // BK: dziedziczenie tytulow po zabitym (tylko przy kluczu - bez klucza BK bez latki)
            var tSucc = AccessTools.TypeByName("BannerKings.Managers.Helpers.SuccessionHelper");
            var tInh = AccessTools.TypeByName("BannerKings.Managers.Helpers.InheritanceHelper");
            if (Config.RotEventHeirKeepsTitles && (tSucc != null || tInh != null))
            {
                ResolveBk();
                var mSov = tSucc != null ? AccessTools.DeclaredMethod(tSucc, "ApplySovereignSuccession") : null;
                var mInh = tInh != null ? AccessTools.DeclaredMethod(tInh, "ApplyInheritanceAllTitles") : null;
                foreach (var pair in new[] { new KeyValuePair<MethodInfo, string>(mSov, nameof(SovPrefix)), new KeyValuePair<MethodInfo, string>(mInh, nameof(InhPrefix)) })
                {
                    _bkWant++;
                    if (pair.Key == null || _miInherit == null) continue;
                    try { h.Patch(pair.Key, prefix: new HarmonyMethod(typeof(RotEventFx), pair.Value) { priority = Priority.First }); _bkOk++; }
                    catch (Exception e) { Stumble("Patch(" + pair.Value + ")", e); }
                }
            }

            // ROT: kontekst na metodach wydarzen, ktore wolaja akcje skutkow + koniec wydarzenia
            var tBase = AccessTools.TypeByName("ROT.Events.EventBase");
            if (tBase != null)
            {
                _piEvActive = AccessTools.Property(tBase, "IsActive");
                var tracked = new HashSet<Type> { typeof(KillCharacterAction), typeof(ChangeClanLeaderAction), typeof(ChangeRulingClanAction), typeof(ChangeKingdomAction),
                                                  typeof(ChangeOwnerOfSettlementAction), typeof(DestroyClanAction), typeof(DestroyKingdomAction), typeof(EndCaptivityAction),
                                                  typeof(TeleportHeroAction) };
                Type[] all;
                try { all = tBase.Assembly.GetTypes(); }
                catch (ReflectionTypeLoadException rtl) { all = Array.FindAll(rtl.Types ?? new Type[0], x => x != null); }
                var enter = new HarmonyMethod(typeof(RotEventFx), nameof(CtxEnter));
                var exit = new HarmonyMethod(typeof(RotEventFx), nameof(CtxExit));
                foreach (var t in all)
                {
                    if (t == null || t.Namespace != "ROT.Events" || t.ContainsGenericParameters || t.IsInterface) continue;
                    if (t.Name.IndexOf('<') >= 0 || t.IsDefined(typeof(CompilerGeneratedAttribute), false)) continue;
                    bool any = false;
                    foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
                    {
                        try
                        {
                            if (m.IsAbstract || m.ContainsGenericParameters || m.GetMethodBody() == null) continue;
                            bool hit = false;
                            foreach (var kv in PatchProcessor.ReadMethodBody(m))
                            {
                                var mb = kv.Value as MethodBase;
                                if (mb != null && mb.DeclaringType != null && tracked.Contains(mb.DeclaringType)) { hit = true; break; }
                            }
                            if (!hit) continue;
                            h.Patch(m, prefix: enter, finalizer: exit);
                            _ctxMethods++; any = true;
                        }
                        catch (Exception e) { Stumble("Patch(" + t.Name + "." + m.Name + ")", e); }
                    }
                    if (any) _ctxTypes++;
                    if (t == tBase || tBase.IsAssignableFrom(t))
                    {
                        // ROT siege events (Instance + SiegeLeaderParty) - smierc od razu nie w oblezeniu fabuly
                        var fi = AccessTools.Field(t, "Instance");
                        var fl = AccessTools.Field(t, "SiegeLeaderParty");
                        if (fi != null && fi.IsStatic && fl != null && fl.FieldType == typeof(MobileParty)) _rotSieges.Add(new KeyValuePair<FieldInfo, FieldInfo>(fi, fl));
                        var mc = AccessTools.DeclaredMethod(t, "CompleteEvent");
                        if (mc != null && !mc.IsAbstract)
                        {
                            try { h.Patch(mc, postfix: new HarmonyMethod(typeof(RotEventFx), nameof(EndPostfix))); _endHooks++; }
                            catch (Exception e) { Stumble("Patch(" + t.Name + ".CompleteEvent)", e); }
                        }
                    }
                }
            }
            _state = "wpiete: akcje " + _actOk + "/" + _actWant + ", kontekst ROT " + _ctxMethods + " metod w " + _ctxTypes + " typach, koniec wydarzen " + _endHooks
                     + ", oblezenia fabuly " + _rotSieges.Count + ", BK " + _bkOk + "/" + _bkWant
                     + (tBase == null ? " (BRAK ROT.Events.EventBase - kontekst nie wpiety)" : "")
                     + (Config.RotEventHeirKeepsTitles && tSucc == null && tInh == null ? " (BRAK BannerKings - tytuly bez zmian)" : "")
                     + (Config.RotEventHeirKeepsTitles && (tSucc != null || tInh != null) && _miInherit == null ? " (BRAK BK TitleManager.InheritTitle)" : "");
        }

        private static void Act(Harmony h, Type t, string name, string pre, string post, bool isPublic)
        {
            _actWant++;
            try
            {
                var m = AccessTools.DeclaredMethod(t, name);
                if (m == null) { Stumble("Act", new MissingMethodException(t.Name, name)); return; }
                h.Patch(m, prefix: new HarmonyMethod(typeof(RotEventFx), pre) { priority = Priority.First }, postfix: new HarmonyMethod(typeof(RotEventFx), post) { priority = Priority.Last });
                _actOk++;
            }
            catch (Exception e) { Stumble("Act(" + t.Name + "." + name + ")", e); }
        }

        private static void ResolveBk()
        {
            try
            {
                var tCfg = AccessTools.TypeByName("BannerKings.BannerKingsConfig");
                var tTm = AccessTools.TypeByName("BannerKings.Managers.TitleManager");
                var tTitle = AccessTools.TypeByName("BannerKings.Managers.Titles.FeudalTitle");
                if (tCfg == null || tTm == null || tTitle == null) return;
                _piCfgInst = AccessTools.Property(tCfg, "Instance");
                _piTm = AccessTools.PropertyGetter(tCfg, "TitleManager");
                _miInherit = AccessTools.DeclaredMethod(tTm, "InheritTitle", new[] { typeof(Hero), typeof(Hero), tTitle });
                _piSovereign = AccessTools.Property(tTitle, "Sovereign");
                _piDeJure = AccessTools.Property(tTitle, "deJure");
                _piContract = AccessTools.Property(tTitle, "Contract");
                _piFullName = AccessTools.Property(tTitle, "FullName");
                if (_piCfgInst == null || _piTm == null || _piDeJure == null) _miInherit = null;
            }
            catch (Exception e) { _miInherit = null; Stumble("ResolveBk", e); }
        }

        // ------------------------------------------------------------ kontekst
        public static void CtxEnter(MethodBase __originalMethod)
        {
            if (_depth == 0)
            {
                _actDepth = 0;
                var t = __originalMethod != null ? __originalMethod.DeclaringType : null;
                while (t != null && t.DeclaringType != null) t = t.DeclaringType;
                _ev = t != null ? t.Name : "?";
            }
            _depth++;
        }

        public static Exception CtxExit(Exception __exception)
        {
            if (_depth > 0) _depth--;
            if (_depth == 0)
            {
                _actDepth = 0; _ev = null;
                // recenzja: tron zablokowany w tym wywolaniu - sprawdzenie dopiero po calej metodzie wydarzenia (ROT wybija stary rod linie nizej);
                // poza kontekstem (glebokosc 0), wiec nasza ChangeRulingClanAction nie przechodzi przez straz wydarzenia
                if (_blocked.Count > 0) { try { RepairBlocked(); } catch (Exception e) { Stumble("RepairBlocked", e); } }
            }
            return __exception;
        }

        /// <summary>Wejscie akcji: tylko w kontekscie ROT; akcja wolana WPROST przez ROT (nie z wnetrza innej akcji, np. nastepca gry przy zabojstwie) = Top.</summary>
        private static bool Enter(out St st)
        {
            st = null;
            if (_depth <= 0) return false;
            _actDepth++;
            if (_actDepth != 1) { st = Nested; return false; }
            st = new St();
            return true;
        }

        private static bool Leave(St st)
        {
            if (st == null) return false;
            if (_actDepth > 0) _actDepth--;
            return !ReferenceEquals(st, Nested);
        }

        public static void EndPostfix(object __instance)
        {
            try
            {
                if (__instance == null) return;
                int hr = (int)CampaignTime.Now.ToHours;
                if (ReferenceEquals(__instance, _lastEnd) && hr == _lastEndHour) return;   // nadpisane CompleteEvent wola base - jedna linia
                _lastEnd = __instance; _lastEndHour = hr;
                string ev = __instance.GetType().Name;
                int[] pe;
                _perEvent.TryGetValue(ev, out pe);
                int sum = 0;
                var sb = new StringBuilder();
                for (int i = 0; i < 5; i++)
                {
                    int n = pe != null ? pe[i] : 0;
                    sum += n;
                    if (i > 0) sb.Append(", ");
                    sb.Append(VName[i]).Append(' ').Append(n);
                }
                Scribe.Line("ROT wydarzenie: " + ev + " zakonczone | skutkow " + sum + " (" + sb + ")" + (sum == 0 ? " - wydarzenie ROT bez skutkow w swiecie (warunki ROT / ConditionsHold)" : "")
                            + " | dzien " + Day() + " (doba " + (int)CampaignTime.Now.ToDays + ")");
            }
            catch (Exception e) { Stumble("EndPostfix", e); }
        }

        // ------------------------------------------------------------ smierc
        public static void KillPre(out St __state, Hero __0, KillCharacterAction.KillCharacterActionDetail __2)
        {
            if (!Enter(out __state)) return;
            __state.A = __0;
            __state.Flag = __0 != null && __0.IsAlive;
            __state.Mark = __0 != null ? __0.DeathMark : KillCharacterAction.KillCharacterActionDetail.None;
            __state.C = __2;
        }

        public static void KillPost(St __state)
        {
            if (!Leave(__state)) return;
            try
            {
                var h = __state.A as Hero;
                var det = (KillCharacterAction.KillCharacterActionDetail)__state.C;
                if (h == null) { Effect(_ev, 4, "smierc", "-", "bez skutku (brak bohatera)"); return; }
                string obj = Nm(h) + ", " + det;
                if (!__state.Flag) { Effect(_ev, 4, "smierc", obj, "bez skutku (juz nie zyl)"); return; }
                if (!h.IsAlive) { Effect(_ev, 0, "smierc", obj, "wykonany"); return; }
                if (h.DeathMark != KillCharacterAction.KillCharacterActionDetail.None)
                {
                    string why = WhyDeferred(h, det);
                    AddPending(h, _ev, h.DeathMark.ToString(), why);
                    Effect(_ev, 1, "smierc", obj, "odroczony przez gre (" + why + "; znak smierci " + h.DeathMark + ")"
                           + (Config.RotEventDeathNow ? " - CrashScribe wykona, gdy druzyna wyjdzie z bitwy (RotEventDeathNow)" : " - gra zabije po bitwie / oblezeniu"));
                    return;
                }
                Effect(_ev, 2, "smierc", obj, "zablokowany przez " + WhoBlocksDeath(h, det));
            }
            catch (Exception e) { Stumble("KillPost", e); }
        }

        private static string WhyDeferred(Hero h, KillCharacterAction.KillCharacterActionDetail det)
        {
            try
            {
                var p = h.PartyBelongedTo;
                if (p != null && p.MapEvent != null) return "druzyna w bitwie";
                if (p != null && p.SiegeEvent != null)
                    return "druzyna w oblezeniu " + (p.SiegeEvent.BesiegedSettlement != null ? T(p.SiegeEvent.BesiegedSettlement.Name) : "?");
                if (det == KillCharacterAction.KillCharacterActionDetail.ExecutionAfterMapEvent) return "egzekucja po bitwie";
                return "inny powod";
            }
            catch { return "?"; }
        }

        /// <summary>Kto zablokowal smierc: bohater gracza (gra), weto CanHeroDieEvent (wlasciciele sluchaczy, ktorzy daja false) albo nieznane.</summary>
        private static string WhoBlocksDeath(Hero h, KillCharacterAction.KillCharacterActionDetail det)
        {
            try
            {
                if (h.IsHumanPlayerCharacter) return "gre (bohater gracza - OnBeforeMainCharacterDied)";
                bool can = h.CanDie(det);
                if (can) return "nieznane (CanDie = TAK, a bohater zyje bez znaku smierci)";
                var who = new List<string>();
                object ev = CampaignEvents.CanHeroDieEvent;
                var f = ev != null ? AccessTools.Field(ev.GetType(), "_nonSerializedListenerList") : null;
                object rec = f != null ? f.GetValue(ev) : null;
                int guard = 0;
                while (rec != null && guard++ < 64)
                {
                    var rt = rec.GetType();
                    var act = AccessTools.Property(rt, "Action")?.GetValue(rec, null) as Delegate;
                    var own = AccessTools.Property(rt, "Owner")?.GetValue(rec, null);
                    if (act != null)
                    {
                        var args = new object[] { h, det, true };
                        try { act.DynamicInvoke(args); } catch { }
                        if (args[2] is bool && !(bool)args[2]) who.Add(own != null ? own.GetType().FullName : "?");
                    }
                    rec = AccessTools.Field(rt, "Next")?.GetValue(rec);
                }
                if (CampaignOptions.IsLifeDeathCycleDisabled && det == KillCharacterAction.KillCharacterActionDetail.DiedOfOldAge) who.Add("gra (cykl zycia wylaczony)");
                return "CanHeroDie (" + (who.Count > 0 ? string.Join(", ", who.ToArray()) : "nie ustalono sluchacza") + ")";
            }
            catch (Exception e) { Stumble("WhoBlocksDeath", e); return "nieznane (wyjatek)"; }
        }

        private static void AddPending(Hero h, string ev, string detail, string why)
        {
            for (int i = _pending.Count - 1; i >= 0; i--) if (_pending[i].Hero == h.StringId) _pending.RemoveAt(i);
            _pending.Add(new Pending { Hero = h.StringId, Event = ev ?? "?", Detail = detail, Why = why, Hour = CampaignTime.Now.ToHours, Ref = h });
            while (_pending.Count > MaxEntries) _pending.RemoveAt(0);
        }

        private static bool InRotSiege(MobileParty p)
        {
            if (p == null) return false;
            foreach (var kv in _rotSieges)
            {
                try
                {
                    var inst = kv.Key.GetValue(null);
                    if (inst == null) continue;
                    if (_piEvActive != null && !(bool)_piEvActive.GetValue(inst, null)) continue;
                    if (ReferenceEquals(kv.Value.GetValue(inst), p)) return true;
                }
                catch { }
            }
            return false;
        }

        /// <summary>Co godzine (tylko gdy ktos czeka): smierc odroczona przez gre - wykonana przez gre albo przez nas (RotEventDeathNow).</summary>
        internal static void Hourly()
        {
            if (!On || _pending.Count == 0) return;
            var sw = Stopwatch.StartNew();
            try
            {
                double now = CampaignTime.Now.ToHours;
                for (int i = _pending.Count - 1; i >= 0; i--)
                {
                    var p = _pending[i];
                    try
                    {
                        var h = HeroById(p.Hero, ref p.Ref);
                        double waited = now - p.Hour;
                        string obj = (h != null ? Nm(h) : p.Hero) + ", " + p.Detail;
                        if (h == null) { _pending.RemoveAt(i); Effect(p.Event, 4, "smierc", obj, "bez skutku (bohater zniknal ze swiata)"); continue; }
                        if (!h.IsAlive)
                        {
                            _pending.RemoveAt(i); _diedByGame++;
                            Effect(p.Event, 0, "smierc", obj, "wykonany po " + Hrs(waited) + " przez gre (koniec bitwy / oblezenia albo inna droga)");
                            continue;
                        }
                        if (h.DeathMark == KillCharacterAction.KillCharacterActionDetail.None)
                        {
                            _pending.RemoveAt(i);
                            Effect(p.Event, 2, "smierc", obj, "zablokowany przez inna droge (znak smierci zdjety, bohater zyje; po " + Hrs(waited) + ")");
                            continue;
                        }
                        if (!Config.RotEventDeathNow)
                        {
                            if (waited > PendingGiveUpHours) { _pending.RemoveAt(i); Effect(p.Event, 2, "smierc", obj, "zablokowany przez gre (znak smierci od " + Hrs(waited) + ", wciaz zyje - przestajemy sledzic)"); }
                            continue;
                        }
                        if (h == Hero.MainHero || h.IsHumanPlayerCharacter) continue;
                        var party = h.PartyBelongedTo;
                        if (party != null && party.MapEvent != null) continue;                  // bitwa trwa - gra rozstrzyga po bitwie
                        if (party != null && party == MobileParty.MainParty) continue;
                        if (InRotSiege(party)) continue;                                           // oblezenie fabuly ROT - zostawiamy grze
                        if (PlayerInvolved(party)) continue;                                       // gracz w bitwie / spotkaniu z ta druzyna / jej osada - nastepna godzina
                        if (MobileParty.MainParty != null && MobileParty.MainParty.MapEvent != null) continue;
                        if (party != null && party.SiegeEvent != null && PlayerSiege.PlayerSiegeEvent == party.SiegeEvent) continue;
                        if (party != null && party.Army != null && MobileParty.MainParty != null && MobileParty.MainParty.Army == party.Army) continue;
                        try { if (Campaign.Current.ConversationManager != null && Campaign.Current.ConversationManager.IsConversationInProgress) continue; } catch { }
                        string where = WhyDeferred(h, KillCharacterAction.KillCharacterActionDetail.None);
                        KillCharacterAction.ApplyByDeathMark(h, false);
                        if (!h.IsAlive)
                        {
                            _pending.RemoveAt(i); _killedByUs++;
                            Effect(p.Event, 0, "smierc", obj, "wykonany po " + Hrs(waited) + " przez CrashScribe (RotEventDeathNow; wtedy: " + where + "; gra czekalaby do konca oblezenia)");
                        }
                        else if (++p.Tries == 1 || p.Tries % 24 == 0)
                            Effect(p.Event, 2, "smierc", obj, "zablokowany przez " + WhoBlocksDeath(h, h.DeathMark) + " (proba " + p.Tries + " ApplyByDeathMark)");
                        if (p.Tries > 24 * 7) _pending.Remove(p);
                    }
                    catch (Exception e) { Stumble("Hourly." + p.Hero, e); if (++p.Tries > 24 * 7) _pending.Remove(p); }
                }
            }
            finally { _costMs += sw.Elapsed.TotalMilliseconds; }
        }

        /// <summary>Gracz w bitwie albo w spotkaniu z ta druzyna / z osada, w ktorej stoi albo ktora oblega - wtedy czekamy godzine (gra tez nie zabija w trakcie).</summary>
        private static bool PlayerInvolved(MobileParty party)
        {
            try
            {
                if (PlayerEncounter.Current == null) return false;
                if (PlayerEncounter.Battle != null) return true;
                if (party == null) return false;
                var ep = PlayerEncounter.EncounteredParty;
                if (ep != null && ep == party.Party) return true;
                var es = PlayerEncounter.EncounterSettlement ?? (ep != null && ep.IsSettlement ? ep.Settlement : null);
                if (es != null && (party.CurrentSettlement == es || (party.SiegeEvent != null && party.SiegeEvent.BesiegedSettlement == es))) return true;
                return false;
            }
            catch { return true; }
        }

        private static string Hrs(double h) { return h < 48 ? ((int)Math.Round(h)) + " h" : (h / 24.0).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " dob"; }

        // ------------------------------------------------------------ glowa rodu (nastepca z ROT)
        public static void LeaderPre(out St __state, Clan __0, Hero __1)
        {
            if (!Enter(out __state)) return;
            __state.A = __0; __state.B = __1; __state.C = __0 != null ? __0.Leader : null;
        }

        public static void LeaderPost(St __state)
        {
            if (!Leave(__state)) return;
            try
            {
                var c = __state.A as Clan;
                var nl = __state.B as Hero;
                var old = __state.C as Hero;
                string obj = Nm(nl) + " po " + Nm(old) + ", rod " + (c != null ? c.StringId : "-");
                if (c == null) { Effect(_ev, 4, "glowa rodu", obj, "bez skutku (brak rodu)"); return; }
                if (nl == null)
                {
                    if (c.Leader != old) Effect(_ev, 0, "glowa rodu", Nm(c.Leader) + " po " + Nm(old) + ", rod " + c.StringId, "wykonany (nastepca wedlug gry)");
                    else Effect(_ev, 2, "glowa rodu", obj, "zablokowany przez gre (brak nastepcy - GetHeirApparents puste)");
                    return;
                }
                if (c.Leader != nl) { Effect(_ev, 2, "glowa rodu", obj, "zablokowany przez inna latke (glowa teraz " + Nm(c.Leader) + ")"); return; }
                if (old == nl) { Effect(_ev, 4, "glowa rodu", obj, "bez skutku (juz byl glowa)"); return; }
                Effect(_ev, 0, "glowa rodu", obj, "wykonany" + (old != null ? " - nastepca z ROT zapamietany (tytuly BK po smierci " + T(old.Name) + ")" : ""));
                if (old != null) AddHeir(c, nl, old, _ev);
            }
            catch (Exception e) { Stumble("LeaderPost", e); }
        }

        private static void AddHeir(Clan c, Hero nl, Hero old, string ev)
        {
            for (int i = _heirs.Count - 1; i >= 0; i--) if (_heirs[i].Clan == c.StringId) _heirs.RemoveAt(i);
            _heirs.Add(new Heir { Clan = c.StringId, NewLeader = nl.StringId, OldLeader = old.StringId, Event = ev ?? "?", Day = Day(), NewRef = nl, OldRef = old,
                                  DeadDay = old.IsAlive ? -1 : Day() });
            while (_heirs.Count > MaxEntries) _heirs.RemoveAt(0);
        }

        private static Heir HeirFor(Hero victim)
        {
            if (victim == null || victim.Clan == null) return null;
            for (int i = _heirs.Count - 1; i >= 0; i--)
                if (_heirs[i].OldLeader == victim.StringId && _heirs[i].Clan == victim.Clan.StringId) return _heirs[i];
            return null;
        }

        /// <summary>null = nastepca z ROT moze dziedziczyc; inaczej powod.</summary>
        private static string HeirInvalid(Heir r, Hero victim, Hero nl, Kingdom k)
        {
            var c = victim.Clan;
            if (nl == null) return "brak bohatera " + r.NewLeader;
            if (!nl.IsAlive) return "nastepca nie zyje";
            if (c == null || c.IsEliminated) return "rod wybity";
            if (nl.Clan != c) return "nastepca poza rodem";
            if (c.Leader != nl) return "nastepca nie jest glowa rodu (glowa " + Nm(c.Leader) + ")";
            if (k != null && k.RulingClan != c) return "rod nie rzadzi krolestwem";
            return null;
        }

        /// <summary>BK TitleManager biezacej kampanii (BK InitManagers podmienia go przy wczytaniu - bez pamieci; wolane rzadko, tylko przy smierci).</summary>
        private static object TitleManager()
        {
            var cfg = _piCfgInst != null ? _piCfgInst.GetValue(null, null) : null;
            return cfg != null ? _piTm.Invoke(cfg, null) : null;
        }

        private static bool Inherit(Hero victim, Hero heir, object title)
        {
            var tm = TitleManager();
            if (tm == null || title == null) return false;
            _miInherit.Invoke(tm, new[] { victim, heir, title });
            return ReferenceEquals(_piDeJure.GetValue(title, null), heir);
        }

        private static string TitleName(object title)
        {
            try { return T(_piFullName != null ? _piFullName.GetValue(title, null) : title); } catch { return "?"; }
        }

        private static string SuccName(object title)
        {
            try
            {
                var con = _piContract != null ? _piContract.GetValue(title, null) : null;
                if (con == null) return "?";
                if (_piSucc == null) _piSucc = AccessTools.Property(con.GetType(), "Succession");
                var s = _piSucc != null ? _piSucc.GetValue(con, null) as TaleWorlds.ObjectSystem.MBObjectBase : null;
                return s != null ? s.StringId : "?";
            }
            catch { return "?"; }
        }

        /// <summary>Prefiks BK SuccessionHelper.ApplySovereignSuccession(title, victim, kingdom): zabity byl glowa rodu panujacego do chwili, gdy wydarzenie
        /// ROT wyznaczylo nowa glowe - tytul suwerena dla tej glowy (TitleManager.InheritTitle - jak BK ResolveSuccession), bez linii sukcesji i wyborow.</summary>
        public static bool SovPrefix(object __0, Hero __1, Kingdom __2)
        {
            try
            {
                if (!On || !Config.RotEventHeirKeepsTitles || __0 == null || __1 == null || _miInherit == null) return true;
                var r = HeirFor(__1);
                if (r == null) return true;
                if (_piSovereign != null && _piSovereign.GetValue(__0, null) != null) return true;   // BK i tak nic nie robi
                var nl = HeroById(r.NewLeader, ref r.NewRef);
                string bad = HeirInvalid(r, __1, nl, __2);
                string obj = Nm(nl) + " po " + Nm(__1) + ", rod " + r.Clan + ", tytul " + TitleName(__0);
                if (bad != null) { Effect(r.Event, 2, "tron i tytul suwerena", obj, "zablokowany przez sukcesje BK " + SuccName(__0) + " (nastepca z ROT nieaktualny: " + bad + ")"); return true; }
                if (!Inherit(__1, nl, __0)) { Effect(r.Event, 2, "tron i tytul suwerena", obj, "zablokowany przez BK (InheritTitle nie przepisal tytulu) - sukcesja BK " + SuccName(__0)); return true; }
                r.Titles = true;
                Effect(r.Event, 3, "tron i tytul suwerena", obj, "naprawiony przez CrashScribe (RotEventHeirKeepsTitles): tytul dla glowy rodu z ROT, sukcesja BK " + SuccName(__0)
                       + " pominieta (bez niej krolem zostalby dziedzic z linii BK)");
                return false;
            }
            catch (Exception e) { Stumble("SovPrefix", e); return true; }
        }

        /// <summary>Prefiks BK InheritanceHelper.ApplyInheritanceAllTitles(titles, victim): reszta tytulow zabitego dla glowy rodu z ROT (nie dla dziedzica BK).</summary>
        public static bool InhPrefix(object __0, Hero __1)
        {
            try
            {
                if (!On || !Config.RotEventHeirKeepsTitles || __0 == null || __1 == null || _miInherit == null) return true;
                var r = HeirFor(__1);
                if (r == null) return true;
                var list = __0 as IEnumerable;
                var titles = new List<object>();
                if (list != null) foreach (var t in list) if (t != null) titles.Add(t);
                if (titles.Count == 0) return true;
                var nl = HeroById(r.NewLeader, ref r.NewRef);
                string bad = HeirInvalid(r, __1, nl, null);
                string obj = Nm(nl) + " po " + Nm(__1) + ", rod " + r.Clan + ", tytulow " + titles.Count;
                if (bad != null) { Effect(r.Event, 2, "tytuly BK", obj, "zablokowany przez dziedziczenie BK (nastepca z ROT nieaktualny: " + bad + ")"); return true; }
                int ok = 0;
                foreach (var t in titles) { try { if (Inherit(__1, nl, t)) ok++; } catch (Exception e) { Stumble("InhPrefix.Inherit", e); } }
                r.Titles = true;
                Effect(r.Event, 3, "tytuly BK", obj, "naprawiony przez CrashScribe (RotEventHeirKeepsTitles): " + ok + " z " + titles.Count + " dla glowy rodu z ROT, dziedzic BK pominiety");
                return false;
            }
            catch (Exception e) { Stumble("InhPrefix", e); return true; }
        }

        // ------------------------------------------------------------ tron (rod panujacy)
        public static bool RulingPre(out St __state, Kingdom __0, Clan __1)
        {
            if (!Enter(out __state)) return true;
            var st = __state;
            st.A = __0; st.B = __1; st.C = __0 != null ? __0.RulingClan : null;
            try
            {
                if (__0 == null || __1 == null || __1.Kingdom == __0) return true;
                st.Pre = "OUT";
                if (!Config.RotEventRulerInKingdom) return true;
                if (!__1.IsEliminated && __1.Kingdom == null && __1 != Clan.PlayerClan && !__1.IsUnderMercenaryService && !__1.IsBanditFaction)
                {
                    try { ChangeKingdomAction.ApplyByJoinToKingdom(__1, __0, default(CampaignTime), false); } catch (Exception e) { Stumble("RulingPre.Join", e); }
                    if (__1.Kingdom == __0) { st.Pre = "JOINED"; return true; }
                }
                string why = __1.IsEliminated ? "rod wybity" : __1 == Clan.PlayerClan ? "rod gracza poza krolestwem"
                             : __1.IsUnderMercenaryService ? "rod najemny" : __1.Kingdom != null ? "rod w krolestwie " + Nm(__1.Kingdom) : "rod nie moze dolaczyc";
                st.Pre = "BLOCK:" + why;
                for (int i = _blocked.Count - 1; i >= 0; i--) if (_blocked[i].K == __0) _blocked.RemoveAt(i);
                _blocked.Add(new Blk { K = __0, Target = __1, Ev = _ev ?? "?", Why = why });
                return false;
            }
            catch (Exception e) { Stumble("RulingPre", e); return true; }
        }

        public static void RulingPost(St __state)
        {
            if (!Leave(__state)) return;
            try
            {
                var k = __state.A as Kingdom;
                var c = __state.B as Clan;
                var prev = __state.C as Clan;
                string obj = Nm(k) + ": " + Nm(prev) + " -> " + Nm(c);
                string pre = __state.Pre ?? "";
                if (pre.StartsWith("BLOCK:"))
                {
                    Effect(_ev, 2, "tron", obj, "zablokowany przez CrashScribe (RotEventRulerInKingdom): " + pre.Substring(6) + " - tron zostaje przy " + Nm(k != null ? k.RulingClan : null)
                           + " (bez tego krolestwo mialoby wladce spoza siebie; po wydarzeniu sprawdzone, czy ten rod dalej moze rzadzic)");
                    return;
                }
                if (k == null || c == null) { Effect(_ev, 4, "tron", obj, "bez skutku (brak krolestwa / rodu)"); return; }
                if (k.RulingClan != c) { Effect(_ev, 2, "tron", obj, "zablokowany przez inna latke (rzadzi " + Nm(k.RulingClan) + ")"); return; }
                if (pre == "JOINED") { Effect(_ev, 3, "tron", obj, "naprawiony przez CrashScribe (RotEventRulerInKingdom): rod niezalezny dolaczyl do krolestwa i przejal tron"); return; }
                if (pre == "OUT" || c.Kingdom != k)
                {
                    Effect(_ev, 0, "tron", obj, "wykonany - UWAGA wladca spoza krolestwa (rod w " + Nm(c.Kingdom) + "; klucz RotEventRulerInKingdom NIE)");
                    return;
                }
                Effect(_ev, prev == c ? 4 : 0, "tron", obj, prev == c ? "bez skutku (juz rzadzil)" : "wykonany");
            }
            catch (Exception e) { Stumble("RulingPost", e); }
        }

        /// <summary>null = rod panujacy moze dalej rzadzic; inaczej powod (wybity / poza krolestwem / bez zywej glowy). Glowa ze znakiem smierci - w porzadku:
        /// gdy umrze, gra sama wybiera nastepny rod (KillCharacterAction, rod jeszcze w krolestwie).</summary>
        private static string RulerBad(Kingdom k, Clan c)
        {
            if (c == null) return "brak rodu panujacego";
            if (c.IsEliminated) return Nm(c) + " wybity";
            if (c.Kingdom != k) return Nm(c) + " poza krolestwem (w " + Nm(c.Kingdom) + ")";
            if (c.Leader == null || !c.Leader.IsAlive) return Nm(c) + " bez zywej glowy";
            return null;
        }

        /// <summary>Zastepca na tron: pierwszy rod krolestwa (kolejnosc Kingdom.Clans - jak galaz else ROT JeorMutinyEvent) inny niz obecny i niz rod z ROT,
        /// nie gracza, nie najemny, nie wybity, z zywa glowa bez znaku smierci, DiplomacyModel.IsClanEligibleToBecomeRuler.</summary>
        private static Clan SubstituteRuler(Kingdom k, Clan cur, Clan target)
        {
            var dm = Campaign.Current != null && Campaign.Current.Models != null ? Campaign.Current.Models.DiplomacyModel : null;
            foreach (var c in k.Clans)
            {
                if (c == null || c == cur || c == target || c == Clan.PlayerClan || c.IsEliminated || c.IsUnderMercenaryService || c.IsBanditFaction) continue;
                if (c.Leader == null || !c.Leader.IsAlive || c.Leader.DeathMark != KillCharacterAction.KillCharacterActionDetail.None) continue;
                if (dm != null && !dm.IsClanEligibleToBecomeRuler(c)) continue;
                return c;
            }
            return null;
        }

        /// <summary>Po metodzie wydarzenia ROT (finalizer kontekstu, glebokosc 0): krolestwo z zablokowanym przekazaniem tronu, w ktorym rod panujacy
        /// nie moze juz rzadzic (ROT wybil go zaraz po przekazaniu - JeorMutinyEvent, BastardsEvent) - tron dla zastepcy z krolestwa, a bez niego dla
        /// rodu z ROT (zywy wladca spoza krolestwa lepszy niz rod wybity). Rod panujacy w porzadku - bez zmian (stan sprzed recenzji).</summary>
        private static void RepairBlocked()
        {
            var list = new List<Blk>(_blocked);
            _blocked.Clear();   // przed akcjami - sluchacze RulingClanChanged moga wejsc w metody ROT i znow dojsc do glebokosci 0
            foreach (var b in list)
            {
                try
                {
                    var k = b.K;
                    if (k == null || k.IsEliminated) continue;
                    var cur = k.RulingClan;
                    string bad = RulerBad(k, cur);
                    if (bad == null) continue;
                    var sub = SubstituteRuler(k, cur, b.Target);
                    if (sub != null)
                    {
                        ChangeRulingClanAction.Apply(k, sub);
                        string obj = Nm(k) + ": " + Nm(cur) + " -> " + Nm(sub);
                        if (k.RulingClan == sub)
                            Effect(b.Ev, 3, "tron", obj, "naprawiony przez CrashScribe (RotEventRulerInKingdom): po wydarzeniu rod panujacy " + bad + " - tron dla pierwszego rodu krolestwa "
                                   + "zdolnego do rzadow (jak galaz else ROT JeorMutinyEvent), nie dla " + Nm(b.Target) + " (" + b.Why + ")");
                        else
                            Effect(b.Ev, 2, "tron", obj, "zablokowany przez inna latke (zastepca " + Nm(sub) + ", rzadzi " + Nm(k.RulingClan) + "; rod panujacy " + bad + ")");
                        continue;
                    }
                    var t = b.Target;
                    if (t != null && !t.IsEliminated && t != Clan.PlayerClan && t.Leader != null && t.Leader.IsAlive)
                    {
                        ChangeRulingClanAction.Apply(k, t);
                        Effect(b.Ev, k.RulingClan == t ? 0 : 2, "tron", Nm(k) + ": " + Nm(cur) + " -> " + Nm(t), k.RulingClan == t
                               ? "wykonany po wydarzeniu - UWAGA wladca spoza krolestwa (rod panujacy " + bad + ", w krolestwie brak innego rodu zdolnego do rzadow - tron dla rodu z ROT, " + b.Why + ")"
                               : "zablokowany przez inna latke (rzadzi " + Nm(k.RulingClan) + "; rod panujacy " + bad + ")");
                        continue;
                    }
                    Effect(b.Ev, 2, "tron", Nm(k) + ": " + Nm(cur) + " -> " + Nm(t), "zablokowany - krolestwo bez rodu zdolnego do rzadow (rod panujacy " + bad + "; rod z ROT " + b.Why + ")");
                }
                catch (Exception e) { Stumble("RepairBlocked." + (b.K != null ? b.K.StringId : "-"), e); }
            }
        }

        // ------------------------------------------------------------ krolestwo rodu
        public static void KingdomPre(out St __state, Clan __0, Kingdom __1, ChangeKingdomAction.ChangeKingdomActionDetail __2)
        {
            if (!Enter(out __state)) return;
            __state.A = __0; __state.B = __1; __state.C = __2; __state.Pre = __0 != null && __0.Kingdom != null ? __0.Kingdom.StringId : "-";
        }

        public static void KingdomPost(St __state)
        {
            if (!Leave(__state)) return;
            try
            {
                var c = __state.A as Clan;
                var k = __state.B as Kingdom;
                var det = (ChangeKingdomAction.ChangeKingdomActionDetail)__state.C;
                string obj = Nm(c) + ": " + __state.Pre + " -> " + (k != null ? k.StringId : "-") + ", " + det;
                if (c == null) { Effect(_ev, 4, "krolestwo rodu", obj, "bez skutku (brak rodu)"); return; }
                bool join = det == ChangeKingdomAction.ChangeKingdomActionDetail.JoinKingdom || det == ChangeKingdomAction.ChangeKingdomActionDetail.JoinKingdomByDefection
                            || det == ChangeKingdomAction.ChangeKingdomActionDetail.JoinAsMercenary || det == ChangeKingdomAction.ChangeKingdomActionDetail.CreateKingdom;
                bool ok = join ? (c.Kingdom == k && k != null) : c.Kingdom == null;
                Effect(_ev, ok ? 0 : 2, "krolestwo rodu", obj, ok ? "wykonany" : "zablokowany przez inna latke (rod w " + Nm(c.Kingdom) + ")");
            }
            catch (Exception e) { Stumble("KingdomPost", e); }
        }

        // ------------------------------------------------------------ wlasciciel osady
        public static void OwnerPre(out St __state, Settlement __0, Hero __1)
        {
            if (!Enter(out __state)) return;
            __state.A = __0; __state.B = __1; __state.C = __0 != null ? __0.OwnerClan : null;
        }

        public static void OwnerPost(St __state)
        {
            if (!Leave(__state)) return;
            try
            {
                var s = __state.A as Settlement;
                var h = __state.B as Hero;
                var prev = __state.C as Clan;
                string obj = (s != null ? T(s.Name) + " (" + s.StringId + ")" : "-") + ": " + (prev != null ? prev.StringId : "-") + " -> " + Nm(h);
                if (s == null || h == null) { Effect(_ev, 4, "wlasciciel osady", obj, "bez skutku (brak osady / bohatera)"); return; }
                bool ok = h.Clan != null && s.OwnerClan == h.Clan;
                Effect(_ev, ok ? 0 : 2, "wlasciciel osady", obj, ok ? "wykonany" : "zablokowany przez inna latke (wlasciciel " + Nm(s.OwnerClan) + ")");
            }
            catch (Exception e) { Stumble("OwnerPost", e); }
        }

        // ------------------------------------------------------------ upadek rodu / krolestwa
        public static void ClanGonePre(out St __state, Clan __0)
        {
            if (!Enter(out __state)) return;
            __state.A = __0; __state.Flag = __0 != null && __0.IsEliminated;
        }

        public static void ClanGonePost(St __state)
        {
            if (!Leave(__state)) return;
            try
            {
                var c = __state.A as Clan;
                if (c == null || __state.Flag) { Effect(_ev, 4, "upadek rodu", Nm(c), "bez skutku (" + (c == null ? "brak rodu" : "juz wybity") + ")"); return; }
                Effect(_ev, c.IsEliminated ? 0 : 2, "upadek rodu", Nm(c), c.IsEliminated ? "wykonany" : "zablokowany przez inna latke (rod trwa)");
            }
            catch (Exception e) { Stumble("ClanGonePost", e); }
        }

        public static void KingdomGonePre(out St __state, Kingdom __0)
        {
            if (!Enter(out __state)) return;
            __state.A = __0; __state.Flag = __0 != null && __0.IsEliminated;
        }

        public static void KingdomGonePost(St __state)
        {
            if (!Leave(__state)) return;
            try
            {
                var k = __state.A as Kingdom;
                if (k == null || __state.Flag) { Effect(_ev, 4, "upadek krolestwa", Nm(k), "bez skutku (" + (k == null ? "brak krolestwa" : "juz upadlo") + ")"); return; }
                Effect(_ev, k.IsEliminated ? 0 : 2, "upadek krolestwa", Nm(k), k.IsEliminated ? "wykonany" : "zablokowany przez inna latke (krolestwo trwa)");
            }
            catch (Exception e) { Stumble("KingdomGonePost", e); }
        }

        // ------------------------------------------------------------ wolnosc / przeprowadzka
        public static void FreePre(out St __state, Hero __0)
        {
            if (!Enter(out __state)) return;
            __state.A = __0; __state.Flag = __0 != null && __0.IsPrisoner;
        }

        public static void FreePost(St __state)
        {
            if (!Leave(__state)) return;
            try
            {
                var h = __state.A as Hero;
                if (h == null || !__state.Flag) { Effect(_ev, 4, "wolnosc", Nm(h), "bez skutku (nie byl w niewoli)"); return; }
                Effect(_ev, h.IsPrisoner ? 2 : 0, "wolnosc", Nm(h), h.IsPrisoner ? "zablokowany przez inna latke (wciaz w niewoli)" : "wykonany");
            }
            catch (Exception e) { Stumble("FreePost", e); }
        }

        public static void MovePre(out St __state, Hero __0, Settlement __1)
        {
            if (!Enter(out __state)) return;
            __state.A = __0; __state.B = __1;
        }

        public static void MovePost(St __state)
        {
            if (!Leave(__state)) return;
            try
            {
                var h = __state.A as Hero;
                var s = __state.B as Settlement;
                Effect(_ev, h != null ? 0 : 4, "przeprowadzka", Nm(h) + " -> " + (s != null ? T(s.Name) : "druzyna"), h != null ? "wykonany (zlecony grze - podroz)" : "bez skutku (brak bohatera)");
            }
            catch (Exception e) { Stumble("MovePost", e); }
        }

        // ------------------------------------------------------------ doba: nastepcy, wladcy spoza krolestwa, linia
        internal static void Daily()
        {
            if (!On) return;
            var sw = Stopwatch.StartNew();
            try
            {
                int day = Day();
                var heirTxt = new StringBuilder();
                for (int i = _heirs.Count - 1; i >= 0; i--)
                {
                    var r = _heirs[i];
                    try
                    {
                        var c = ClanById(r.Clan);
                        var nl = HeroById(r.NewLeader, ref r.NewRef);
                        var old = HeroById(r.OldLeader, ref r.OldRef);
                        if (c == null || c.IsEliminated || nl == null || !nl.IsAlive) { _heirs.RemoveAt(i); continue; }
                        if (old == null || !old.IsAlive)
                        {
                            if (r.DeadDay < 0) r.DeadDay = day;
                            if (!r.Lost && c.Leader != nl)
                            {
                                r.Lost = true;
                                Effect(r.Event, 2, "glowa rodu", Nm(nl) + ", rod " + r.Clan, "zablokowany przez inna droge po smierci " + (old != null ? T(old.Name) : r.OldLeader)
                                       + " (glowa teraz " + Nm(c.Leader) + (Config.RotEventHeirKeepsTitles ? "" : "; np. sukcesja BK - klucz RotEventHeirKeepsTitles NIE") + ")");
                            }
                            if (day - r.DeadDay > HeirKeepDays) { _heirs.RemoveAt(i); continue; }
                        }
                        if (heirTxt.Length > 0) heirTxt.Append("; ");
                        heirTxt.Append(r.Clan).Append(": ").Append(T(nl.Name)).Append(" (").Append(r.Event).Append(") glowa ").Append(c.Leader == nl ? "TAK" : "NIE")
                               .Append(", stara glowa ").Append(old == null || !old.IsAlive ? "martwa" : "zyje").Append(", tytuly ").Append(r.Titles ? "TAK" : "-");
                    }
                    catch (Exception e) { Stumble("Daily.heir", e); }
                }
                // krolestwa z wladca spoza krolestwa (stan swiata - kazde zrodlo, nie tylko ROT)
                int outside = 0;
                var outTxt = new StringBuilder();
                try
                {
                    foreach (var k in Kingdom.All)
                    {
                        if (k == null || k.IsEliminated || k.RulingClan == null || k.RulingClan.Kingdom == k) continue;
                        outside++;
                        if (outside <= 5) { if (outTxt.Length > 0) outTxt.Append("; "); outTxt.Append(k.StringId).Append(": rod ").Append(k.RulingClan.StringId).Append(" w ").Append(k.RulingClan.Kingdom != null ? k.RulingClan.Kingdom.StringId : "brak"); }
                    }
                }
                catch (Exception e) { Stumble("Daily.outside", e); }
                var pendTxt = new StringBuilder();
                double now = CampaignTime.Now.ToHours;
                foreach (var p in _pending)
                {
                    if (pendTxt.Length > 0) pendTxt.Append("; ");
                    pendTxt.Append(p.Ref != null ? T(p.Ref.Name) : p.Hero).Append(" (").Append(p.Hero).Append(", ").Append(p.Event).Append(", ").Append(Hrs(now - p.Hour)).Append(", ").Append(p.Why).Append(')');
                }
                int today = _d[0] + _d[1] + _d[2] + _d[3] + _d[4];
                bool due = today > 0 || _pending.Count > 0 || outside > 0 || _lastLineDay < 0 || day - _lastLineDay >= 7;
                if (due)
                {
                    _lastLineDay = day;
                    int tot = _t[0] + _t[1] + _t[2] + _t[3] + _t[4];
                    Scribe.Line("ROT wydarzenia (RX): dzien " + day + " (doba " + (int)CampaignTime.Now.ToDays + ") | klucze: smierc od razu " + (Config.RotEventDeathNow ? "TAK" : "NIE")
                                + ", nastepca z tytulami " + (Config.RotEventHeirKeepsTitles ? "TAK" : "NIE") + ", tron w krolestwie " + (Config.RotEventRulerInKingdom ? "TAK" : "NIE")
                                + " | dzis: skutkow " + today + " (" + Counts(_d) + ") | od wczytania: skutkow " + tot + " (" + Counts(_t) + "), smierci po odroczeniu: przez CrashScribe "
                                + _killedByUs + ", przez gre " + _diedByGame + " | czekaja na smierc " + _pending.Count + ": [" + pendTxt + "] | nastepcy z ROT " + _heirs.Count + ": [" + heirTxt
                                + "] | krolestwa z wladca spoza krolestwa " + outside + ": [" + outTxt + "] | koszt " + (_costMs + sw.Elapsed.TotalMilliseconds).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)
                                + " ms | potkniecia " + _stumbles + ".");
                }
            }
            catch (Exception e) { Stumble("Daily", e); }
            Array.Clear(_d, 0, _d.Length);
            _costMs = 0;
        }

        private static string Counts(int[] a)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < 5; i++) { if (i > 0) sb.Append(", "); sb.Append(VName[i]).Append(' ').Append(a[i]); }
            return sb.ToString();
        }

        // ------------------------------------------------------------ zapis (klucz cs_rotfx_v1)
        internal static void Pack()
        {
            var l = new List<string>();
            try
            {
                foreach (var p in _pending)
                    l.Add("D|" + Esc(p.Hero) + "|" + Esc(p.Event) + "|" + Esc(p.Detail) + "|" + p.Hour.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "|" + Esc(p.Why));
                foreach (var r in _heirs)
                    l.Add("H|" + Esc(r.Clan) + "|" + Esc(r.NewLeader) + "|" + Esc(r.OldLeader) + "|" + Esc(r.Event) + "|" + r.Day + "|" + (r.Titles ? 1 : 0) + "|" + (r.Lost ? 1 : 0) + "|" + r.DeadDay);
            }
            catch (Exception e) { Stumble("Pack", e); }
            Saved = l;
        }

        internal static void Unpack()
        {
            _pending.Clear(); _heirs.Clear();
            try
            {
                if (Saved == null) { Saved = new List<string>(); return; }
                int start = Math.Max(0, Saved.Count - 2 * MaxEntries);   // straz rozmiaru
                for (int i = start; i < Saved.Count; i++)
                {
                    var s = Saved[i];
                    if (string.IsNullOrEmpty(s) || s.Length > 600) continue;
                    var f = s.Split('|');
                    try
                    {
                        if (f[0] == "D" && f.Length >= 6 && _pending.Count < MaxEntries)
                        {
                            double hr;
                            if (!double.TryParse(f[4], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out hr)) continue;
                            _pending.Add(new Pending { Hero = f[1], Event = f[2], Detail = f[3], Hour = hr, Why = f[5] });
                        }
                        else if (f[0] == "H" && f.Length >= 9 && _heirs.Count < MaxEntries)
                        {
                            int d0, dd;
                            int.TryParse(f[5], out d0); if (!int.TryParse(f[8], out dd)) dd = -1;
                            _heirs.Add(new Heir { Clan = f[1], NewLeader = f[2], OldLeader = f[3], Event = f[4], Day = d0, Titles = f[6] == "1", Lost = f[7] == "1", DeadDay = dd });
                        }
                    }
                    catch { }
                }
            }
            catch (Exception e) { Stumble("Unpack", e); }
        }

        private static string Esc(string s) { return string.IsNullOrEmpty(s) ? "-" : s.Replace('|', '/').Replace('\n', ' ').Replace('\r', ' '); }
    }

    /// <summary>Skutki wydarzen ROT: wpiecie przy starcie sesji, godzina (smierci odroczone), doba (linia), zapis (cs_rotfx_v1). Rejestrowany tylko przy RotEventFx.</summary>
    internal sealed class RotEventFxBehavior : CampaignBehaviorBase
    {
        public RotEventFxBehavior() { RotEventFx.ResetSession(); }

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, delegate (CampaignGameStarter s) { RotEventFx.OnSession(); });
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, delegate { RotEventFx.Hourly(); });
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, delegate { RotEventFx.Daily(); });
        }

        public override void SyncData(IDataStore dataStore)
        {
            try
            {
                if (dataStore.IsSaving) RotEventFx.Pack();
                dataStore.SyncData("cs_rotfx_v1", ref RotEventFx.Saved);
                if (dataStore.IsLoading) RotEventFx.Unpack();
            }
            catch (Exception e) { try { Scribe.Report("CrashScribe", e, "RotEventFxBehavior.SyncData", null); } catch { } }
        }
    }
}
