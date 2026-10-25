using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using HarmonyLib;
using SandBox.Tournaments.MissionLogics;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.TournamentGames;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>
    /// noc 25.10 P11 (125) "okno lupu po turnieju" (Jeff 25.10: "po bitwie i po oblezeniu i po turnieju, powinien wyskakiwac popup z dokladna iloscia lootu";
    /// 06:35 "no ale miales to zrobic"). Klucz SpoilsWindowAfterTourney. Projekt noc/projekty/okno-lupu.json, ramka T.
    /// RAMKA T - ta sama zasada co ramki bitwy (SpoilsReport.cs): roznica stanu prawdziwych posiadaczy (kiesa gracza, sakwy, slawa, wplywy) przed i po turnieju,
    /// TYLKO ODCZYT - zadna latka nie zmienia wyniku. Otwarcie: CampaignEvents.OnPlayerJoinedTournamentEvent (gracz w szrankach albo widz; menu miasta, tuz przed
    /// misja). Ramka liczaca jest jedyna (_frame, _depth) - wewnetrzne ramki (F sakw) w czasie turnieju nic nie licza, wiec nic nie liczy sie dwa razy.
    /// Zamkniecie (TWatch z SpoilsWindow.AppTick, 4 razy na sekunde): po misji szranek, na mapie (menu miasta), GrandTourney bez kolejki (TourneyRansom
    /// _missionBouts/_offers/_popupOpen - wykup konia i zbroi rozliczany PO misji i okna "Pay N / Yield"), bez zapytania, dwa sprawdzenia z rzedu (0,5 s).
    /// Bitwa przy otwartej ramce (MapEvent gracza, nowa bitwa w ApplyResultsPrefix) albo nowy turniej - zamkniecie z tego, co bylo. Turniej, ktory nie ruszyl
    /// (bez misji i bez TournamentFinished) - zamkniecie po godzinie kampanii albo gdy gracz jest poza miastem; po szrankach straznik 12 h kampanii.
    /// ETYKIETY ZLOTA (suma czesci = roznica kiesy; czego zaden zakres nie tlumaczy - "Other"): ZAKRESY roznicy kiesy gracza (TBegin/TEnd, stos - zakres
    /// wewnetrzny liczy sie tylko w sobie):
    ///  - PlayerFees8 (wrappery wywolan GiveGoldAction w grze i w Tournaments XPanded): TxpGold "Prize money", TxpBonus "Match honours", BetStake "Bets placed",
    ///    BetWon "Bets won", PrizeAfter "The prize, paid in coin" (nagrody nie bylo na polce miasta - cena zamiast przedmiotu);
    ///  - TourneyFolkPurse: "The townsfolk's purse" (przelana kwota, gdy sakiewka jest gracza);
    ///  - GrandTourney (osobny DLL - latki Harmony z Armoury, wpinane w kampanii, prefiks First / finalizer Last, tylko odczyt kiesy): TourneyBehavior.PayPurse
    ///    "The champion's purse", TourneyBehavior.OnTournamentFinished "Takings of your lists" (gospodarz, bez sakiewki mistrza), TourneyRansom.SettleAll
    ///    "Ransoms paid to you", TourneyRansom.Answer "Ransom of your horse and armour". Bez GT (albo latka nie weszla) - te kwoty w "Other".
    /// PRZEDMIOTY: przyrost sakw w ramce (nagroda turnieju - przedmiot TournamentGame.Prize z TournamentFinished; reszta - kon i zbroja oddane graczowi w GT).
    /// ODDANY RYNSZTUNEK: sloty zbroi i konia bohaterow druzyny gracza pelne przy otwarciu, puste przy zamknieciu (GT Yield zdejmuje sztuke z ekwipunku bojowego).
    /// SZRANKI: TournamentBehavior misji przy TournamentFinished (rundy, walki gracza, final), PlayerEliminatedFromTournament (runda odpadniecia).
    /// PROBA AUTOTESTU (LogGate.Mode 1/2 + plik CrashScribe\armoury-lup-turniej.flag): ramka T z kiesa PROBNA (_testGold - prawdziwa kiesa i swiat nietkniete),
    /// zakresy zagniezdzone i reszta, tresc okna, potem okno przez zwykla bramke (w autotescie zamyka sie samo po 1.5 s) - linia "test turnieju - ... wynik".
    /// Linie: "Okno lupu (117): turniej N (bitew 0) ..." (raport - ten sam format co bitwa, tozsamosc zlota sprawdza grupa p5lup), "Okno lupu (125): latki
    /// GrandTourney ...", "Okno lupu (125): test ...". Potkniecia liczone (Stumble), bez wylacznika.
    /// </summary>
    internal static partial class SpoilsReport
    {
        internal const string TagT = "Okno lupu (125): ";

        internal const string LTPrize = "Prize money";
        internal const string LTHonours = "Match honours";
        internal const string LTBetStake = "Bets placed";
        internal const string LTBetWon = "Bets won";
        internal const string LTPrizeCoin = "The prize, paid in coin";
        internal const string LTFolk = "The townsfolk's purse";
        internal const string LTChampion = "The champion's purse";
        internal const string LTTakings = "Takings of your lists";
        internal const string LTRansomIn = "Ransoms paid to you";
        internal const string LTRansomOut = "Ransom of your horse and armour";

        internal static bool TourneyOn { get { var s = Settings.Current; return s != null && s.SpoilsWindowAfterTourney; } }

        /// <summary>Turniej raportu: szranki, mistrz, nagroda, oddany rynsztunek (tylko odczyt - wypelnia ramka T).</summary>
        internal sealed class TInfo
        {
            public bool Rode;                 // gracz w szrankach (isParticipant), inaczej widz
            public bool Test;
            public bool SawMission, Finished; // misja szranek widziana; TournamentFinished dla miasta ramki
            public string Champion = "";
            public int ChampionWho;           // 0 obcy, 1 gracz, 2 z druzyny gracza
            public ItemObject Prize;          // TournamentGame.Prize przy TournamentFinished
            public int ElimRound = -1;        // runda odpadniecia gracza (0..3)
            public int Rounds, Bouts, BoutsWon;
            public string FellTo = "", Final = "";
            public int LostN; public long LostW;
            public readonly List<string> LostNames = new List<string>();
            public int Scopes, ScopeLost;     // zakresy kiesy zamkniete / zdjete bez zamkniecia
            public string WaitedFor = "";     // na co ramka czekala po misji (GrandTourney, zapytanie)
        }

        private static TInfo _t;                  // turniej otwartej ramki T
        private static Rep _tLast;                // ostatni raport turnieju (proba autotestu)
        private static int _tClear;
        private static long _testGold = -1;       // proba autotestu: kiesa probna zamiast prawdziwej (tylko w jednym synchronicznym wywolaniu TestRun)
        private static Harmony _harmony;

        private sealed class Worn { public Hero H; public EquipmentIndex Slot; public EquipmentElement E; }
        private static List<Worn> _tWorn;
        private static readonly EquipmentIndex[] WornSlots =
        {
            EquipmentIndex.Head, EquipmentIndex.Body, EquipmentIndex.Leg, EquipmentIndex.Gloves, EquipmentIndex.Cape, EquipmentIndex.Horse, EquipmentIndex.HorseHarness
        };

        /// <summary>Kiesa gracza (ramki i zakresy); w probie autotestu - kiesa probna.</summary>
        private static long CurGold()
        {
            if (_testGold >= 0) return _testGold;
            var me = Hero.MainHero;
            return me != null ? me.Gold : 0;
        }

        private static void TReset()
        {
            _t = null; _tTop = null; _tWorn = null; _tClear = 0; _tLast = null; _testGold = -1;
        }

        // ------------------------------------------------------------ zdarzenia turnieju (ArmouryBehavior.RegisterEvents)

        /// <summary>CampaignEvents.OnPlayerJoinedTournamentEvent - otwarcie ramki T (gracz w szrankach albo widz).</summary>
        internal static void OnTourneyJoined(Town town, bool isParticipant)
        {
            try
            {
                if (!TourneyOn || town == null || town.Settlement == null) return;
                if (_frame != null) Abandon("turniej (OnPlayerJoinedTournament) przy otwartej ramce " + _frame.Name);
                OpenT(town, isParticipant, false);
            }
            catch (Exception e) { Stumble("OnTourneyJoined", e); }
        }

        private static void OpenT(Town town, bool rode, bool test)
        {
            if (_frame == null) _depth = 0;   // licznik bez ramki (wyjatek miedzy Open i Close) - jak straznik Watch
            var s = Open("T");
            if (s == null) return;
            s.Town = town.Settlement;
            s.Place = Nm(town.Settlement);
            s.Date = CampaignTime.Now.ToString();
            _t = new TInfo { Rode = rode, Test = test };
            _tTop = null; _tClear = 0;
            _tWorn = WornOf(MobileParty.MainParty);
        }

        /// <summary>CampaignEvents.TournamentFinished (wszystkie turnieje; tylko miasto otwartej ramki): mistrz, nagroda, szranki z TournamentBehavior misji.</summary>
        internal static void OnTourneyFinished(CharacterObject winner, MBReadOnlyList<CharacterObject> participants, Town town, ItemObject prize)
        {
            try
            {
                var f = _frame; var t = _t;
                if (f == null || f.Name != "T" || t == null || town == null || !ReferenceEquals(f.Town, town.Settlement)) return;
                t.Finished = true;
                t.Prize = prize;
                if (winner != null)
                {
                    t.Champion = NameOf(winner);
                    if (ReferenceEquals(winner, CharacterObject.PlayerCharacter)) t.ChampionWho = 1;
                    else if (winner.IsHero && winner.HeroObject != null && ReferenceEquals(winner.HeroObject.PartyBelongedTo, MobileParty.MainParty)) t.ChampionWho = 2;
                }
                ReadLists(t);
            }
            catch (Exception e) { Stumble("OnTourneyFinished", e); }
        }

        /// <summary>CampaignEvents.PlayerEliminatedFromTournament - runda odpadniecia (indeks rundy gry 0..3).</summary>
        internal static void OnTourneyEliminated(int round, Town town)
        {
            try
            {
                var f = _frame; var t = _t;
                if (f == null || f.Name != "T" || t == null || town == null || !ReferenceEquals(f.Town, town.Settlement)) return;
                if (t.ElimRound < 0) t.ElimRound = round;
            }
            catch (Exception e) { Stumble("OnTourneyEliminated", e); }
        }

        private static string NameOf(CharacterObject c)
        {
            try
            {
                if (c == null) return "";
                if (ReferenceEquals(c, CharacterObject.PlayerCharacter)) return "you";
                return c.Name != null ? c.Name.ToString() : "";
            }
            catch { return ""; }
        }

        private static string Names(IEnumerable<TournamentParticipant> ps, int max, string sep)
        {
            var list = new List<string>();
            int more = 0;
            if (ps != null)
                foreach (var p in ps)
                {
                    if (p == null || p.Character == null) continue;
                    if (list.Count < max) list.Add(NameOf(p.Character)); else more++;
                }
            string s = string.Join(sep, list.ToArray());
            return more > 0 ? s + " and " + more + " more" : s;
        }

        /// <summary>Szranki z TournamentBehavior misji (TournamentFinished biegnie w misji, po ostatniej walce): walki gracza, wygrane, kto go pokonal, final.</summary>
        private static void ReadLists(TInfo t)
        {
            try
            {
                var m = TaleWorlds.MountAndBlade.Mission.Current;
                var tb = m != null ? m.GetMissionBehavior<TournamentBehavior>() : null;
                if (tb == null || tb.Rounds == null) return;
                t.Rounds = tb.Rounds.Length;
                var pc = CharacterObject.PlayerCharacter;
                for (int i = 0; i < tb.Rounds.Length; i++)
                {
                    var rd = tb.Rounds[i];
                    if (rd == null || rd.Matches == null) continue;
                    foreach (var mt in rd.Matches)
                    {
                        if (mt == null) continue;
                        bool mine = false;
                        foreach (var p in mt.Participants) if (p != null && ReferenceEquals(p.Character, pc)) { mine = true; break; }
                        if (!mine) continue;
                        t.Bouts++;
                        bool won = false;
                        foreach (var w in mt.Winners) if (w != null && ReferenceEquals(w.Character, pc)) { won = true; break; }
                        if (won) t.BoutsWon++;
                        else if (t.FellTo.Length == 0) t.FellTo = Names(mt.Winners, 2, ", ");
                    }
                }
                var last = tb.Rounds[tb.Rounds.Length - 1];
                if (last != null && last.Matches != null && last.Matches.Length > 0 && last.Matches[0] != null)
                    t.Final = Names(last.Matches[0].Participants, 4, " against ");
            }
            catch (Exception e) { Stumble("ReadLists", e); }
        }

        // ------------------------------------------------------------ zakresy kiesy (etykiety ramki T)

        internal sealed class TScope { internal string Label; internal long Gold0, Inner; internal TScope Parent; }
        private static TScope _tTop;

        /// <summary>Poczatek zakresu roznicy kiesy gracza (para z TEnd w finally). Bez otwartej ramki T - null (nic).</summary>
        internal static TScope TBegin(string label)
        {
            try
            {
                var f = _frame;
                if (f == null || f.Name != "T" || Hero.MainHero == null) return null;
                var s = new TScope { Label = label, Gold0 = CurGold(), Parent = _tTop };
                _tTop = s;
                return s;
            }
            catch (Exception e) { Stumble("TBegin", e); return null; }
        }

        /// <summary>Koniec zakresu: roznica kiesy minus zakresy wewnetrzne = etykieta tego zakresu. Nigdy nie rzuca.</summary>
        internal static void TEnd(TScope s)
        {
            if (s == null) return;
            try
            {
                var top = _tTop;
                while (top != null && !ReferenceEquals(top, s)) { if (_t != null) _t.ScopeLost++; top = top.Parent; }   // wewnetrzny bez TEnd - liczy sie w tym
                if (top == null) return;   // ramka T zamknieta w srodku zakresu (Abandon) - nic
                _tTop = s.Parent;
                var f = _frame;
                if (f == null || f.Name != "T") return;
                long d = CurGold() - s.Gold0;
                if (s.Parent != null) s.Parent.Inner += d;
                if (_t != null) _t.Scopes++;
                long own = d - s.Inner;
                if (own != 0) NoteCoin(s.Label, own);
            }
            catch (Exception e) { Stumble("TEnd", e); }
        }

        /// <summary>Gotowa kwota do kiesy gracza (TourneyFolkPurse - przelew policzony przez sam przelew) jako etykieta otwartej ramki T. Bez ramki - nic.</summary>
        internal static void NoteTourney(string label, long amount)
        {
            try
            {
                var f = _frame;
                if (f == null || f.Name != "T" || amount == 0) return;
                NoteCoin(label, amount);
                if (_tTop != null) _tTop.Inner += amount;   // w srodku zakresu - nie liczy sie drugi raz w zakresie
            }
            catch (Exception e) { Stumble("NoteTourney", e); }
        }

        // ------------------------------------------------------------ zamkniecie ramki T

        private static List<Worn> WornOf(MobileParty mp)
        {
            var list = new List<Worn>();
            try
            {
                var mr = mp != null ? mp.MemberRoster : null;
                if (mr == null) return list;
                for (int i = 0; i < mr.Count; i++)
                {
                    var ch = mr.GetCharacterAtIndex(i);
                    var h = ch != null && ch.IsHero ? ch.HeroObject : null;
                    var eq = h != null ? h.BattleEquipment : null;
                    if (eq == null) continue;
                    foreach (var sl in WornSlots)
                    {
                        var el = eq[sl];
                        if (el.IsEmpty || el.Item == null) continue;
                        list.Add(new Worn { H = h, Slot = sl, E = el });
                    }
                }
            }
            catch (Exception e) { Stumble("WornOf", e); }
            return list;
        }

        /// <summary>SpoilsReport.Watch (4 razy na sekunde, takze w misji): kiedy zamknac ramke T.</summary>
        private static void TWatch()
        {
            var f = _frame;
            if (f == null || f.Name != "T") return;
            var t = _t;
            var mp = MobileParty.MainParty;
            if (mp != null && mp.MapEvent != null) { TClose("bitwa przy otwartej ramce turnieju"); return; }
            if (TaleWorlds.MountAndBlade.Mission.Current != null) { if (t != null) t.SawMission = true; _tClear = 0; return; }
            double day = CampaignTime.Now.ToDays;
            bool fought = t != null && (t.SawMission || t.Finished);
            if (!fought)
            {
                // szranki jeszcze nie ruszyly (scena w drodze - czas kampanii stoi) albo nie rusza wcale
                bool away = mp != null && f.Town != null && !ReferenceEquals(mp.CurrentSettlement, f.Town) && PlayerEncounter.Current == null;
                if (day - f.Day > 1.0 / 24.0 || away) TClose("turniej sie nie odbyl (bez misji szranek" + (away ? ", gracz poza miastem" : ", godzina kampanii") + ")");
                return;
            }
            var gsm = GameStateManager.Current;
            if (gsm == null || !(gsm.ActiveState is MapState)) { _tClear = 0; return; }
            string wait = null;
            if (SpoilsWindow.GtBusyNow()) wait = "GrandTourney (wykup konia i zbroi)";
            else if (InformationManager.IsAnyInquiryActive()) wait = "zapytanie";
            if (wait != null)
            {
                _tClear = 0;
                if (t != null && t.WaitedFor.Length == 0) t.WaitedFor = wait;
                if (day - f.Day > 0.5) TClose("straznik - 12 h kampanii po szrankach (" + wait + ")");
                return;
            }
            if (++_tClear < 2) return;
            TClose(t != null && t.Finished ? "po szrankach" : "po misji bez TournamentFinished");
        }

        private static void TClose(string how)
        {
            var f = _frame;
            if (f == null || f.Name != "T") { _tTop = null; return; }
            var t = _t ?? new TInfo();
            var worn = _tWorn;
            _t = null; _tTop = null; _tWorn = null; _tClear = 0;
            var d = Close(f);
            _depth = 0;
            if (d == null) return;
            try
            {
                // oddany rynsztunek (GrandTourney Yield zdejmuje sztuke z ekwipunku bojowego przegranego) - slot pelny przy otwarciu, pusty teraz
                if (worn != null)
                    foreach (var w in worn)
                    {
                        var eq = w.H != null ? w.H.BattleEquipment : null;
                        if (eq == null) continue;
                        var now = eq[w.Slot];
                        if (!now.IsEmpty && now.Item != null) continue;
                        t.LostN++; t.LostW += Math.Max(0, w.E.ItemValue);
                        if (t.LostNames.Count < 3)
                        {
                            string nm;
                            try { var tx = w.E.GetModifiedItemName(); nm = tx != null ? tx.ToString() : w.E.Item.StringId; } catch { nm = w.E.Item.StringId; }
                            t.LostNames.Add(nm);
                        }
                    }
                var r = new Rep { Id = ++_nextId, Kind = KTourney, Town = f.Town, Place = f.Place, Date = f.Date, Battles = 0, T = t, Test = t.Test };
                string a = Apply(r, d, LOther);
                bool prizeIn = false;
                if (t.Prize != null) foreach (var g in r.Gear.Values) if (ReferenceEquals(g.E.Item, t.Prize)) { prizeIn = true; break; }
                r.Lines.Append(" | T ").Append(f.Place).Append(" (").Append(how).Append("): ").Append(a)
                 .Append(" | szranki: gracz ").Append(t.Rode ? "w szrankach" : "widz").Append(", TournamentFinished ").Append(t.Finished ? "TAK" : "NIE")
                 .Append(", misja ").Append(t.SawMission ? "TAK" : "NIE")
                 .Append(", mistrz ").Append(t.Champion.Length > 0 ? t.Champion : "-").Append(t.ChampionWho == 1 ? " (gracz)" : t.ChampionWho == 2 ? " (druzyna gracza)" : "")
                 .Append(", walki gracza ").Append(t.BoutsWon).Append('/').Append(t.Bouts).Append(" (rund ").Append(t.Rounds).Append(')')
                 .Append(t.ElimRound >= 0 ? ", odpadl w rundzie " + (t.ElimRound + 1).ToString(Inv) : "")
                 .Append(", nagroda ").Append(t.Prize != null ? t.Prize.StringId + (prizeIn ? " w sakwach" : " poza sakwami") : "-")
                 .Append(", oddany rynsztunek ").Append(t.LostN).Append(" szt. za ").Append(t.LostW.ToString(Inv))
                 .Append(", zakresy kiesy ").Append(t.Scopes).Append(t.ScopeLost > 0 ? " (bez zamkniecia " + t.ScopeLost + ")" : "")
                 .Append(t.WaitedFor.Length > 0 ? ", czekala na " + t.WaitedFor : "");
                r.WaitFor = "";
                _reps.Add(r);
                _tLast = r;
                TryReady(r, "po turnieju");
            }
            catch (Exception e) { Stumble("TClose", e); }
        }

        // ------------------------------------------------------------ GrandTourney (osobny DLL - latki z Armoury, tylko odczyt kiesy)

        private static bool _gtWireTried;

        public static void GtPurseBegin(out object __state) { __state = TBegin(LTChampion); }
        public static void GtTakingsBegin(out object __state) { __state = TBegin(LTTakings); }
        public static void GtSettleBegin(out object __state) { __state = TBegin(LTRansomIn); }
        public static void GtAnswerBegin(out object __state) { __state = TBegin(LTRansomOut); }

        /// <summary>Finalizer Last metod GrandTourney - koniec zakresu kiesy; wyjatek GT przechodzi bez zmian.</summary>
        public static Exception GtScopeEnd(Exception __exception, object __state)
        {
            TEnd(__state as TScope);
            return __exception;
        }

        /// <summary>Raz na proces, w kampanii (SpoilsWindow.Step): zestaw GrandTourney jest juz zaladowany. Brak GT - linia i kwoty GT w "Other".</summary>
        internal static void EnsureGtWired()
        {
            if (_gtWireTried) return;
            var c = Campaign.Current;
            if (c == null || !c.GameStarted) return;
            _gtWireTried = true;
            try
            {
                var h = _harmony;
                if (h == null) { Log.Info(TagT + "latki GrandTourney - bez proby (brak Harmony: SpoilsReport.Apply nie wolany) - kwoty GrandTourney w 'Other'."); return; }
                var tr = Type.GetType("GrandTourney.TourneyRansom, GrandTourney");
                var tb = Type.GetType("GrandTourney.TourneyBehavior, GrandTourney");
                if (tr == null && tb == null)
                {
                    Log.Info(TagT + "latki GrandTourney 0/0 - GrandTourney nieobecny (kwoty turnieju bez GT) | klucz Tourney " + YN(TourneyOn) + ".");
                    return;
                }
                int w0 = _wired, n0 = _wanted, m0 = _missing.Count;
                Wire(h, tb, "PayPurse", nameof(GtPurseBegin), null, nameof(GtScopeEnd), Priority.First, "GT PayPurse");
                Wire(h, tb, "OnTournamentFinished", nameof(GtTakingsBegin), null, nameof(GtScopeEnd), Priority.First, "GT OnTournamentFinished");
                Wire(h, tr, "SettleAll", nameof(GtSettleBegin), null, nameof(GtScopeEnd), Priority.First, "GT SettleAll");
                Wire(h, tr, "Answer", nameof(GtAnswerBegin), null, nameof(GtScopeEnd), Priority.First, "GT Answer");
                var miss = _missing.GetRange(m0, _missing.Count - m0);
                Log.Info(TagT + "latki GrandTourney " + (_wired - w0) + "/" + (_wanted - n0) + " (zakresy kiesy: sakiewka mistrza, utarg gospodarza, wykup od pokonanych, wykup gracza)"
                         + (miss.Count > 0 ? " | BRAK: " + string.Join(", ", miss.ToArray()) : "") + " | GT Busy " + (SpoilsWindow.GtKnown() ? "TAK" : "BRAK")
                         + " | klucz Tourney " + YN(TourneyOn) + " | proba autotestu: plik " + TestFlagName + ".");
            }
            catch (Exception e) { Stumble("EnsureGtWired", e); }
        }

        // ------------------------------------------------------------ proba autotestu (LogGate.Mode 1/2 + plik flagi)

        internal const string TestFlagName = "armoury-lup-turniej.flag";
        private const double TestMinSec = 15.0, TestCalmSec = 60.0, TestGiveUpSec = 1800.0, TestMinDays = 0.25;
        private static bool _testDone, _testFlag, _testOk;
        private static long _testFlagAt, _testStart;
        private static double _testDay0;
        private static string _testNote = "";

        /// <summary>SpoilsWindow.Step (4 razy na sekunde w kampanii): proba raz na sesje gry. Bez autotestu albo bez pliku - od razu wyjscie.
        /// Bez czekania na dobe ani licznik sesji (P4 z tego powodu nie odpalil) - start po 15 s kampanii z plynacym czasem (albo 60 s), na spokojnej mapie.</summary>
        internal static void TestStep()
        {
            if (_testDone) return;
            try
            {
                int m = LogGate.Mode;
                if (m != 1 && m != 2) return;
                long now = Stopwatch.GetTimestamp();
                if (!_testFlag)
                {
                    if (_testFlagAt != 0 && (now - _testFlagAt) < Stopwatch.Frequency * 20) return;
                    _testFlagAt = now;
                    var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                    _testFlag = File.Exists(Path.Combine(Path.Combine(Path.Combine(docs, "Mount and Blade II Bannerlord"), "CrashScribe"), TestFlagName));
                    if (!_testFlag) return;
                    Log.Info(TagT + "test - plik " + TestFlagName + " jest: proba po " + TestMinSec.ToString("0", Inv) + " s kampanii na spokojnej mapie.");
                }
                var c = Campaign.Current;
                if (c == null || !c.GameStarted) return;
                if (_testStart == 0) { _testStart = now; _testDay0 = CampaignTime.Now.ToDays; return; }
                double sec = (now - _testStart) / (double)Stopwatch.Frequency;
                double days = CampaignTime.Now.ToDays - _testDay0;
                if (sec < TestMinSec || (days < TestMinDays && sec < TestCalmSec)) return;
                string no = null;
                if (!TourneyOn) no = "klucz Spoils Window After Tourney NIE";
                else if (MobileParty.MainParty == null || Hero.MainHero == null) no = "brak gracza";
                if (no != null) { _testDone = true; Log.Info(TagT + "test - bez proby (" + no + ")."); return; }
                var mp = MobileParty.MainParty;
                string busy = null;
                if (mp.MapEvent != null) busy = "bitwa";
                else if (TaleWorlds.MountAndBlade.Mission.Current != null) busy = "misja";
                else if (PlayerEncounter.Current != null && PlayerEncounter.EncounterSettlement == null) busy = "spotkanie";
                else if (_frame != null || _depth > 0) busy = "otwarta ramka " + (_frame != null ? _frame.Name : "?");
                else if (NextReady() != null || SpoilsWindow.Busy) busy = "inne okno lupu";
                if (busy != null)
                {
                    if (sec > TestGiveUpSec) { _testDone = true; Log.Info(TagT + "test - bez proby (30 min bez spokojnej mapy, ostatnio: " + busy + ")."); }
                    return;
                }
                _testDone = true;
                TestRun();
            }
            catch (Exception e) { _testDone = true; Stumble("TestStep", e); }
        }

        private static void TestRun()
        {
            var me = Hero.MainHero;
            Town town = null;
            try { var cs = Settlement.CurrentSettlement; if (cs != null) town = cs.Town; } catch { }
            if (town == null) { var ks = me.LastKnownClosestSettlement; if (ks != null) town = ks.Town; }
            if (town == null) foreach (var tw in Town.AllTowns) if (tw != null && tw.Settlement != null && tw.Settlement.IsTown) { town = tw; break; }
            if (town == null) { Log.Info(TagT + "test - bez proby (brak miasta)."); return; }
            long real0 = me.Gold;
            var pts = new List<string>();
            bool ok = true;
            Rep r = null;
            _testGold = 10000;
            try
            {
                OpenT(town, true, true);
                bool opened = _frame != null && _frame.Name == "T";
                if (opened)
                {
                    // zakresy jak w turnieju: zaklad i wyplata zakladu, utarg gospodarza z sakiewka mistrza w srodku, sakiewka mieszczan, nagroda TXP, kwota bez zakresu (reszta)
                    var a = TBegin(LTBetStake); _testGold -= 150; TEnd(a);
                    var bw = TBegin(LTBetWon); _testGold += 345; TEnd(bw);
                    var outer = TBegin(LTTakings); _testGold += 200;
                    var inner = TBegin(LTChampion); _testGold += 500; TEnd(inner);
                    TEnd(outer);
                    _testGold += 60; NoteTourney(LTFolk, 60);
                    var pz = TBegin(LTPrize); _testGold += 300; TEnd(pz);
                    _testGold += 40;
                    var t = _t;
                    if (t != null)
                    {
                        t.Finished = true; t.SawMission = true; t.Champion = "you"; t.ChampionWho = 1; t.Rounds = 4; t.Bouts = 4; t.BoutsWon = 4;
                        t.Final = "you against Ser Test of the Lists";
                    }
                    TClose("proba autotestu");
                    r = _tLast;
                }
                pts.Add("T1 ramka T otwarta " + YN(opened));
                ok &= opened;
            }
            finally { _testGold = -1; }
            if (r == null || !r.Test)
            {
                Log.Info(TagT + "test turnieju - " + string.Join(" | ", pts.ToArray()) + ", raport NIE - wynik NIE.");
                return;
            }
            // T1: etykiety (stos zakresow) i reszta; prawdziwa kiesa bez zmian; ramka zamknieta
            var want = new Dictionary<string, long> { { LTBetStake, -150 }, { LTBetWon, 345 }, { LTTakings, 200 }, { LTChampion, 500 }, { LTFolk, 60 }, { LTPrize, 300 }, { LOther, 40 } };
            bool labels = r.CoinLabels.Count == want.Count;
            for (int i = 0; i < r.CoinLabels.Count; i++) { long v; if (!want.TryGetValue(r.CoinLabels[i], out v) || v != r.CoinAmounts[i]) labels = false; }
            bool total = r.CoinTotal == 1295;
            bool real = me.Gold == real0;
            bool closed = _frame == null && _depth == 0 && _tTop == null && _t == null;
            pts.Add("T1 etykiety " + YN(labels) + " (" + r.CoinLabels.Count + "), suma 1295 " + YN(total) + " (" + r.CoinTotal + "), kiesa gracza bez zmian " + YN(real)
                    + ", ramka zamknieta " + YN(closed) + ", oddany rynsztunek " + (r.T != null ? r.T.LostN : -1));
            ok &= labels && total && real && closed && (r.T == null || r.T.LostN == 0);
            // nagroda do tresci okna (tylko raport probny - zadna sztuka nie trafia do sakw)
            ItemObject prize = null;
            try
            {
                var eq = me.BattleEquipment;
                if (eq != null) { var b = eq[EquipmentIndex.Body]; if (!b.IsEmpty) prize = b.Item; }
                if (prize == null) foreach (var it in TaleWorlds.ObjectSystem.MBObjectManager.Instance.GetObjectTypeList<ItemObject>()) if (it != null && it.Value > 0) { prize = it; break; }
            }
            catch (Exception e) { Stumble("TestRun.prize", e); }
            if (prize != null)
            {
                var el = new EquipmentElement(prize);
                r.Gear[prize.StringId] = new Gain { E = el, N = 1 };
                if (r.T != null) r.T.Prize = prize;
            }
            // T2: tresc okna (te same funkcje co okno)
            bool content = false; string titles = "";
            try
            {
                var c = SpoilsWindow.Build(r);
                var names = new List<string>();
                foreach (var s in c.Sections) names.Add(s.Title);
                titles = string.Join("/", names.ToArray());
                bool coinRows = false;
                foreach (var s in c.Sections) if (s.Title == "Coin") coinRows = s.Rows.Count == want.Count + 1;
                content = c.Title.StartsWith("The Tourney at ", StringComparison.Ordinal) && names.Contains("The lists") && coinRows
                          && (prize == null || names.Contains("The prize and the arms")) && c.Total.Length > 0;
            }
            catch (Exception e) { Stumble("TestRun.Build", e); }
            pts.Add("T2 tresc okna " + YN(content) + " (" + titles + ")");
            ok &= content;
            _testOk = ok;
            _testNote = string.Join(" | ", pts.ToArray());
            Log.Info(TagT + "test turnieju - start | " + _testNote + " | raport " + r.Id + " gotowy " + YN(r.Ready) + " - okno przez zwykla bramke (T3, w autotescie zamyka sie samo).");
        }

        /// <summary>Finish raportu probnego (pokazane / pominiete): T3 i wynik calej proby.</summary>
        private static void TestVerdict(Rep r, string outcome, string why, string window)
        {
            try
            {
                bool shown = outcome == "pokazane" && window != null && window.IndexOf("zamkni", StringComparison.Ordinal) >= 0;
                bool ok = _testOk && shown;
                Log.Info(TagT + "test turnieju - " + _testNote + " | T3 okno " + outcome + (why != null ? " (" + why + ")" : "") + (window != null ? " | " + window : "")
                         + " - " + YN(shown) + " - wynik " + YN(ok) + ".");
            }
            catch (Exception e) { Stumble("TestVerdict", e); }
        }
    }
}
