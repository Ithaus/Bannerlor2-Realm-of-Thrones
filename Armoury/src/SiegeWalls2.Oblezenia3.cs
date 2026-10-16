using System;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// OBLEZENIA 3 (noc 15/16.10, galaz w-toku/noc-oblezenia; rozpoznanie kampanii Jeffa 15.10: 110 dob, 20 oblezen skonczonych, zdobyte 3 zamki, 0 miast;
    /// 11 razy odsiecz, 2 okupy, 4 konce bez bitwy, 0 trafien w mury).
    ///
    /// KO - KONIEC OBLEZENIA (tylko log, klucz SiegeWallsLog): linia "Koniec oblezenia (KO)" przy kazdym OnSiegeEventEnded twierdzy (z SiegeWalls.OnSiegeEnded,
    ///   przed czyszczeniem przerw OB2). Przyczyna w kolejnosci: (1) zdobycie - znacznik z MapEventEnded (szturm albo bitwa pod murami wygrana przez
    ///   oblegajacych; gra zwija oboz w KingdomManager.SiegeCompleted PRZED zmiana pana - dlatego OB5 pisal "utrzymane" przy Cape Wrath); (2) oblegajacy
    ///   pokonani - pole gry SiegeEvent._isBesiegerDefeated (OnBeforeSiegeEventEnd); (3) wolajacy z rzutu stosu (pierwsza pasujaca ramka od srodka): okup
    ///   (SiegeRansom), SCA odwrot (StrategicCampaignAI145), pokoj (MakePeaceAction / AiPartyThinkBehavior.OnMakePeace), mysl wodza (PartyHourlyAiTick: ROT
    ///   AIThinkPatch / AiPartyThinkBehavior), odejscie z obozu (SiegeEvent.Tick -> BesiegerCamp.CheckBesiegerPartiesAndMakeThemLeave: wodz zmienil cel poza bitwa -
    ///   w linii cel z jego ostatniej mysli), armia rozwiazana (DisbandArmyAction / Army), frakcja zniknela (FactionDiscontinuation); inaczej "inne" z dwiema
    ///   pierwszymi ramkami spoza zdarzen gry. Ramka metody latanej przez Harmony (DMD) nie ma typu - liczy sie nazwa metody, ktora zawiera typ oryginalu.
    ///   "Szturm przerwany": szturm AI skonczony w tej chwili bez zwyciezcy (SiegeWalls.OnMapEventEnded, galaz "bez rozstrzygniecia") albo bitwa szturmu jeszcze
    ///   trwa (SCA zwijal oboz przed bitwa - SiegeEvent :889 przed :900); "regula SK" - czy ta bitwa byla pod regula F2 (bez Innych, skryptu, gracza, morza, 96 h).
    ///   Jedna linia na oblezenie: FinalizeSiegeEvent wolane wprost przy niepustym obozie wysyla zdarzenie drugi raz (_koDone).
    ///   Gra zwija oboz przed zdarzeniem (RemoveAllSiegeParties) - ludzie i jedzenie obozu wtedy z ostatniej mysli wodza obozu (zapis co jego mysl, LogOn).
    ///
    /// F2 - SZTURM AI DO KONCA (klucz SiegeAssaultRunsToEnd). [K] ROT AIThinkPatch.PartyHourlyAiTick (prefiks zastepujacy AiPartyThinkBehavior, ta sama logika
    ///   co gra :112-124): wodz szturmu mysli co godzine; gdy najlepszy cel nie jest oblezeniem / szturmem / rabunkiem tej osady, gra konczy bitwe bez
    ///   rozstrzygniecia (MapEvent.FinalizeEvent) i oblezenie (FinalizeSiegeEvent). Postfiks Last na CampaignEventDispatcher.AiHourlyTick (po ROT Patch1,
    ///   OB5 Low, WW3 Low, H1 Low): wodz AI we wlasnym szturmie (nie gracz, nie morze, nie skrypt, nie Inni) - wpis BesiegeSettlement tej osady dostaje
    ///   max(1.05 x najlepszy + 0.01, 0.5), wiec gra zostaje przy szturmie (ten sam cel - AIThinkPatch :472 nic nie zmienia). 0.5 ponad prog gry (0.033 dla
    ///   wodza armii) - przy nizszym wyniku gra rozwiazalaby armie w trakcie bitwy (galaz "else" AIThinkPatch :400). Prefiks SCA TryRetreatFromBadSiege: wodz
    ///   armii w bitwie szturmu albo pod murami - bez odwrotu w trakcie bitwy (LiftSiege SCA konczyl tez bitwe - SiegeEvent.FinalizeSiegeEvent :900). Poza
    ///   bitwa SCA i OB5 jak dotad; odsiecz dolacza do bitwy (gra zmienia ja na SiegeOutside - OB3).
    ///
    /// W - TREBUSZ AI BIJE W MUR (klucz SiegeAiTrebuchetsHitWalls). [K] BesiegerCamp.GetAttackTarget (:498-530) losuje cel miedzy machinami obroncow
    ///   (priorytet 7 - (|i-j| + 3)) a murem (5 - (|slot - 0.5 - 2j| + 2), najwyzej 2.5) - mur dostaje mniej niz polowe strzalow, a w 110 dobach Jeffa ani jednego
    ///   trafienia. Postfiks Low: trebusz obozu AI (zakres jak TB: twierdza, bez gracza i jego obozu, bez skryptu i Innych) celuje w NAJSLABSZY stojacy odcinek
    ///   murow, dopoki jakis stoi (takze gdy gra wybrala mur, ale inny odcinek - ogien na jeden odcinek daje wyrwe SZ2 najszybciej); potem wybor gry.
    ///   Inne machiny i oboz gracza bez zmian. Wolane przez interfejs ISiegeEventSide (SiegeEvent :1202) - JIT nie wkleja.
    ///
    /// F1 (SiegeRansom.Evaluate, klucz SiegeRansomRepelNeedsWeakEdge) - tu tylko licznik doby. Wyjatek per obiekt = licznik potkniec (CLAUDE.md 7). Bez zapisu
    /// (po wczytaniu pamiec mysli wodzow od zera).
    /// </summary>
    internal static partial class SiegeWalls2
    {
        // ------------------------------------------------------------ klucze
        internal static bool RunsToEndOn { get { var s = Settings.Current; return s != null && s.SiegeAssaultRunsToEnd; } }
        internal static bool HitWallsOn { get { var s = Settings.Current; return s != null && s.SiegeAiTrebuchetsHitWalls; } }
        internal static bool RepelEdgeOn { get { var s = Settings.Current; return s != null && s.SiegeRansomRepelNeedsWeakEdge; } }
        /// <summary>Najnizszy wynik szturmu w mysli wodza: prog zmiany celu gry 0.1 (0.033 dla wodza armii) przechodzi; ponizej gra rozwiazuje armie (AIThinkPatch :400).</summary>
        private const float HoldFloor = 0.5f;
        /// <summary>Bezpiecznik F2: bitwa szturmu dluzsza niz tyle godzin - mysl gry i SCA jak dotad (najdluzszy szturm AI w logach OB2 55 h; bitwa, ktora nie chce sie
        /// skonczyc, nie trzyma armii pod murami bez konca).</summary>
        private const double HoldMaxHours = 96.0;
        private static double NowD { get { return CampaignTime.Now.ToDays; } }

        // ------------------------------------------------------------ potkniecia (osobno od oblezen 2 - linia "Oblezenia 3 (doba)")
        private static int _stumbles3, _dStumbles3;
        private static readonly HashSet<string> _err3 = new HashSet<string>();
        private static void Stumble3(string where, Exception e)
        {
            _stumbles3++; _dStumbles3++;
            try { if (_err3.Add(where)) Log.Error("SiegeWalls2.O3." + where, e); } catch { }
        }

        // ============================================================ KO: koniec oblezenia (tylko log)
        private static readonly Dictionary<Settlement, double> _capAt = new Dictionary<Settlement, double>();     // zdobycie (MapEventEnded) - chwila
        private static readonly Dictionary<Settlement, double> _abortAt = new Dictionary<Settlement, double>();   // szturm AI bez zwyciezcy - chwila
        private sealed class Snap
        {
            public double Hour = -1; public string Leader = "-", Fac = "-", Beh = "-", Tgt = "-"; public float Score = -1f, Siege = -1f; public bool Held; public int Men = -1, Food = -1;
        }
        private static readonly Dictionary<SiegeEvent, Snap> _snap = new Dictionary<SiegeEvent, Snap>();        // ostatnia mysl wodza obozu (LogOn)
        // poprawka po recenzji: regula F2 dla przerwanego szturmu AI ("tak" albo "nie (powod)") - z MapEventEnded, gdy bitwa jeszcze jest
        private static readonly Dictionary<Settlement, string> _abortRule = new Dictionary<Settlement, string>();
        // poprawka po recenzji: SiegeEvent.FinalizeSiegeEvent wolane wprost przy niepustym obozie (ROT AIThinkPatch :405, FactionDiscontinuation :102/:114) wysyla
        // OnSiegeEventEnded drugi raz (BesiegerCamp.FinalizeSiegeEvent -> RemoveAllSiegeParties -> RemoveSiegePartyInternal :242-245) - jedna linia KO na oblezenie
        private static readonly HashSet<SiegeEvent> _koDone = new HashSet<SiegeEvent>();
        private static readonly string[] EndName = { "zdobycie", "oblegajacy pokonani", "okup", "SCA odwrot", "mysl wodza", "armia rozwiazana", "pokoj", "frakcja zniknela", "odejscie z obozu", "inne" };
        private const int ECapture = 0, EBeaten = 1, ERansom = 2, ESca = 3, EThink = 4, EArmy = 5, EPeace = 6, EGone = 7, ELeave = 8, EOther = 9;
        private static readonly int[] _dEnd = new int[10];
        private static int _dEnds, _dEndAbort, _dEndAbortOurs;
        private static AccessTools.FieldRef<SiegeEvent, bool> _beatRef;
        private static bool _beatFail;

        /// <summary>Z OnMapEventEnded (szturm albo bitwa pod murami wygrana przez oblegajacych): zdobycie tej twierdzy w tej chwili.</summary>
        internal static void NoteCaptured(Settlement st) { try { if (st != null) _capAt[st] = NowD; } catch (Exception e) { Stumble3("NoteCaptured", e); } }

        /// <summary>Z SiegeWalls.OnMapEventEnded (szturm AI bez zwyciezcy i bez odparcia OB2): szturm przerwany w tej chwili i czy byl pod regula F2.</summary>
        internal static void NoteAborted(Settlement st, MapEvent me)
        {
            try { if (st != null) { _abortAt[st] = NowD; _abortRule[st] = RuleSk(me, st); } }
            catch (Exception e) { Stumble3("NoteAborted", e); }
        }

        /// <summary>Czy bitwa szturmu byla pod regula F2 (jak ThinkPost / ScaRetreatPre): "tak" albo "nie (powod)". Liczone w chwili bitwy (MapEventEnded albo bitwa trwa).</summary>
        private static string RuleSk(MapEvent me, Settlement st)
        {
            if (me == null) return "nie (bez bitwy)";
            if (!RunsToEndOn) return "nie (klucz wylaczony)";
            var lpb = me.AttackerSide != null ? me.AttackerSide.LeaderParty : null;
            var lp = lpb != null && lpb.IsMobile ? lpb.MobileParty : null;
            if (lp == null) return "nie (bez wodza)";
            if (lp.IsMainParty || me.IsPlayerMapEvent) return "nie (gracz w bitwie)";
            if (me.IsNavalMapEvent) return "nie (morze)";
            if (Scripted(lp)) return "nie (skrypt)";
            if (Und(lp, st)) return "nie (Inni)";
            if (me.BattleStartTime.ElapsedHoursUntilNow > HoldMaxHours) return "nie (ponad " + F(HoldMaxHours, "0") + " h)";
            return "tak";
        }

        /// <summary>Twierdza zdobyta w ciagu ostatnich 0.02 doby (KO, OB5 Finish - koniec oblezenia przychodzi przed zmiana pana).</summary>
        internal static bool CapturedNow(Settlement st)
        {
            double t;
            return st != null && _capAt.Count > 0 && _capAt.TryGetValue(st, out t) && Math.Abs(NowD - t) < 0.02;
        }

        private static bool BeatenNow(SiegeEvent ev)
        {
            if (_beatFail) return false;
            if (_beatRef == null)
            {
                try { _beatRef = AccessTools.FieldRefAccess<SiegeEvent, bool>("_isBesiegerDefeated"); }
                catch (Exception e) { _beatFail = true; Stumble3("BeatenNow.Field", e); return false; }   // pole gry nie istnieje - przyczyna z rzutu stosu
            }
            try { return _beatRef(ev); }
            catch (Exception e) { Stumble3("BeatenNow", e); return false; }
        }

        /// <summary>Ramki zdarzen gry, Harmony i nasluchu (pomijane przy "inne").</summary>
        private static bool Plumbing(string t)
        {
            return t.StartsWith("System.", StringComparison.Ordinal) || t.StartsWith("TaleWorlds.Library.", StringComparison.Ordinal) || t.StartsWith("HarmonyLib.", StringComparison.Ordinal)
                || t.StartsWith("MonoMod.", StringComparison.Ordinal) || t.StartsWith("TaleWorlds.CampaignSystem.MbEvent", StringComparison.Ordinal)
                || t.StartsWith("TaleWorlds.CampaignSystem.CampaignEvents.", StringComparison.Ordinal) || t.StartsWith("TaleWorlds.CampaignSystem.CampaignEventDispatcher.", StringComparison.Ordinal)
                || t.StartsWith("TaleWorlds.CampaignSystem.Siege.SiegeEvent.FinalizeSiegeEvent", StringComparison.Ordinal) || t.StartsWith("TaleWorlds.CampaignSystem.Siege.BesiegerCamp.", StringComparison.Ordinal)
                || t.StartsWith("TaleWorlds.CampaignSystem.Party.MobileParty.set_BesiegerCamp", StringComparison.Ordinal) || t.StartsWith("Armoury.SiegeWalls.OnSiegeEnded", StringComparison.Ordinal)
                || t.StartsWith("TaleWorlds.CampaignSystem.Party.MobileParty.OnPartyLeftSiegeInternal", StringComparison.Ordinal)
                || t.StartsWith("TaleWorlds.CampaignSystem.Siege.SiegeEvent.Tick", StringComparison.Ordinal)
                || t.StartsWith("Helpers.FactionHelper.FinishAllRelatedHostileActions", StringComparison.Ordinal);   // wolana z pokoju, ChangeKingdomAction i MarriageAction - liczy sie wolajacy
        }

        /// <summary>Rodzaj konca z nazwy ramki albo -1. Ramki sprawdzane od srodka (nasluch) na zewnatrz - pierwsza pasujaca wygrywa.
        /// Poprawka po recenzji [K]: pokoj idzie MakePeaceAction -> AiPartyThinkBehavior.OnMakePeace (AiPartyThinkBehavior.cs:216) -> FactionHelper (BesiegerCamp = null,
        /// FactionHelper.cs:337), wiec mysl wodza tylko z PartyHourlyAiTick (ROT AIThinkPatch :405, gra AiPartyThinkBehavior :123); odejscie po zmianie celu poza bitwa idzie
        /// SiegeEvent.Tick -> BesiegerCamp.CheckBesiegerPartiesAndMakeThemLeave (SiegeEvent.cs:848, BesiegerCamp.cs:200-210) - bez ramki mysli w stosie.</summary>
        private static int Kind(string t)
        {
            if (t.Contains("SiegeRansom")) return ERansom;
            if (t.Contains("StrategicCampaignAI145")) return ESca;
            if (t.Contains("MakePeaceAction") || t.Contains("AiPartyThinkBehavior.OnMakePeace")) return EPeace;
            if (t.Contains("AIThinkPatch.PartyHourlyAiTick") || t.Contains("AiPartyThinkBehavior.PartyHourlyAiTick")) return EThink;
            if (t.Contains("BesiegerCamp.CheckBesiegerPartiesAndMakeThemLeave")) return ELeave;
            if (t.Contains("DisbandArmyAction") || t.StartsWith("TaleWorlds.CampaignSystem.Army.", StringComparison.Ordinal)) return EArmy;
            if (t.Contains("FactionDiscontinuation")) return EGone;
            if (t.Contains("KingdomManager.SiegeCompleted")) return ECapture;
            return -1;
        }

        /// <summary>Rzut stosu od nasluchu w gore: pierwsza ramka znanego wolajacego. Nazwa ramki DMD Harmony "DMD&lt;Typ::Metoda&gt;" sprowadzana do "Typ.Metoda".</summary>
        private static int ByStack(out string other)
        {
            other = null;
            string first = null, second = null;
            var tr = new System.Diagnostics.StackTrace(2, false);
            int n = tr.FrameCount;
            for (int i = 0; i < n; i++)
            {
                var f = tr.GetFrame(i);
                var m = f != null ? f.GetMethod() : null;
                if (m == null) continue;
                string t = m.DeclaringType != null ? m.DeclaringType.FullName + "." + m.Name : m.Name;
                if (string.IsNullOrEmpty(t)) continue;
                if (t.StartsWith("DMD<", StringComparison.Ordinal)) t = t.Substring(4).TrimEnd('>').Replace("::", ".");
                int k = Kind(t);
                if (k >= 0) return k;
                if (second == null && !Plumbing(t))
                {
                    string sh = t.StartsWith("TaleWorlds.CampaignSystem.", StringComparison.Ordinal) ? t.Substring("TaleWorlds.CampaignSystem.".Length) : t;
                    if (sh.Length > 90) sh = sh.Substring(0, 90);
                    if (first == null) first = sh; else second = sh;
                }
            }
            other = first == null ? "?" : second == null ? first : first + " < " + second;
            return EOther;
        }

        /// <summary>Z SiegeWalls.OnSiegeEnded (na poczatku): przyczyna konca oblezenia twierdzy, liczniki doby i linia KO. Tylko odczyt. Nie rzuca.</summary>
        internal static void EndLine(SiegeEvent ev, int repels)
        {
            if (ev == null) return;
            try
            {
                if (!LogOn) return;
                var st = ev.BesiegedSettlement;
                if (st == null || st.Town == null || !st.IsFortification) return;
                if (!_koDone.Add(ev)) return;                                         // drugie OnSiegeEventEnded tego samego oblezenia (opis _koDone)
                string other = null;
                int k = CapturedNow(st) ? ECapture : BeatenNow(ev) ? EBeaten : ByStack(out other);
                double t0;
                bool abort = _abortAt.TryGetValue(st, out t0) && Math.Abs(NowD - t0) < 0.02;
                string rule = "-";
                if (abort) { if (!_abortRule.TryGetValue(st, out rule)) rule = "?"; }
                var sme = st.Party != null ? st.Party.MapEvent : null;
                if (!abort && sme != null && sme.IsSiegeAssault && !sme.IsFinalized) { abort = true; rule = RuleSk(sme, st); }
                _dEnds++; _dEnd[k]++;
                if (abort) { _dEndAbort++; if ((k == EThink || k == ESca) && rule == "tak") _dEndAbortOurs++; }   // przerwanie, ktore regula F2 miala zatrzymac
                var camp = ev.BesiegerCamp;
                var lp = camp != null ? camp.LeaderParty : null;
                Snap sn; _snap.TryGetValue(ev, out sn);
                if (k == ELeave) other = sn != null ? "zmiana celu - ostatnia mysl: " + sn.Beh + " " + sn.Tgt : "zmiana celu - bez mysli wodza w pamieci";
                int men = 0, food = int.MaxValue; bool live = false;
                if (camp != null)
                    foreach (var pb in camp.GetInvolvedPartiesForEventType(MapEvent.BattleTypes.Siege))
                    {
                        if (pb == null) continue;
                        live = true; men += pb.NumberOfHealthyMembers;
                        var mp = pb.MobileParty;
                        if (mp != null) { int d = mp.GetNumDaysForFoodToLast(); if (d < food) food = d; }
                    }
                string campTxt;
                if (live) campTxt = "ludzi " + men + ", jedzenie obozu " + (food == int.MaxValue ? "-" : food.ToString()) + " dob";
                else if (sn != null) campTxt = "ludzi " + sn.Men + ", jedzenie obozu " + (sn.Food < 0 ? "-" : sn.Food.ToString()) + " dob (oboz zwiniety - z mysli wodza " + F(CampaignTime.Now.ToHours - sn.Hour, "0") + " h temu)";
                else campTxt = "ludzi - (oboz zwiniety, bez mysli wodza w pamieci), jedzenie obozu - dob";
                string fac = lp != null && lp.MapFaction != null ? lp.MapFaction.StringId : sn != null ? sn.Fac : "-";
                string lead = lp != null ? Hn(lp) : sn != null ? sn.Leader : "-";
                int h, w;
                int def = MenOf(st, out h, out w);
                bool ram = false, tower = false; int tr = 0, tb = 0;
                try { if (camp != null) Engines(camp, out ram, out tower, out tr, out tb); } catch (Exception e) { Stumble3("EndLine.Engines", e); }
                bool player = (lp != null && lp.IsMainParty) || (MobileParty.MainParty != null && camp != null && MobileParty.MainParty.BesiegerCamp == camp);
                string think = sn == null ? "-" : sn.Beh + " " + sn.Tgt + " (wynik " + F(sn.Score, "0.000") + ", oblezenie " + (sn.Siege < 0f ? "-" : F(sn.Siege, "0.000"))
                                                   + (sn.Held ? ", szturm do konca" : "") + ", " + F(CampaignTime.Now.ToHours - sn.Hour, "0") + " h temu)";
                var sb = new StringBuilder(900);
                sb.Append("Koniec oblezenia (KO): dzien ").Append(Today).Append(" | osada ").Append(st.StringId).Append(" (").Append(Nm(st)).Append(", ").Append(Cls(st)).Append(')')
                  .Append(" | oblegajacy ").Append(fac).Append(", wodz ").Append(lead).Append(", ").Append(campTxt)
                  .Append(" | obroncow ").Append(def).Append(" | oblezenie ").Append(F(ev.SiegeStartTime.ElapsedDaysUntilNow, "0.0")).Append(" dob")
                  .Append(" | przyczyna: ").Append(EndName[k]).Append(k == EOther || k == ELeave ? " (" + (other ?? "?") + ")" : "")
                  .Append(" | szturm przerwany ").Append(abort ? "tak" : "nie").Append(" | regula SK: ").Append(rule).Append(" | odparc ").Append(repels)
                  .Append(" | mury HP ").Append(F(SiegeWalls.WallHp(st) * 100.0, "0")).Append("%, najslabszy ").Append(F(SiegeWalls.WallHpMin(st) * 100.0, "0")).Append('%')
                  .Append(", taran ").Append(ram ? "tak" : "nie").Append(", wieza ").Append(tower ? "tak" : "nie").Append(", trebusze ").Append(tr)
                  .Append(" | odsiecz w drodze ").Append(BesiegedRelief.ReliefOnWay(ev))
                  .Append(" | ostatnia mysl wodza: ").Append(think).Append(" | gracz ").Append(player ? "tak" : "nie").Append('.');
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Stumble3("EndLine", e); }
            finally { _snap.Remove(ev); }
        }

        // ============================================================ F2: szturm AI do konca bitwy
        private static readonly HashSet<MapEvent> _skLogged = new HashSet<MapEvent>();
        private static int _dHoldH, _dHoldSaved, _dHoldLow, _dScaHeld, _dHoldOld;

        private static string Target(AIBehaviorData d)
        {
            var p = d.Party;
            if (p == null) return d.AiBehavior == AiBehavior.None ? "-" : "punkt";
            var s = p as Settlement; if (s != null) return s.StringId;
            var m = p as MobileParty; if (m != null) return m.StringId;
            return p.GetType().Name;
        }

        /// <summary>Postfiks Last CampaignEventDispatcher.AiHourlyTick: (1) wodz AI we wlasnym szturmie - szturm zostaje najlepszym celem (regula w opisie klasy);
        /// (2) przy LogOn - ostatnia mysl wodza obozu do linii KO. Szybkie wyjscie dla druzyn poza obozem oblezenia. Nie rzuca.</summary>
        public static void ThinkPost(MobileParty party, PartyThinkParams partyThinkParams)
        {
            if (party == null || partyThinkParams == null) return;
            var camp = party.BesiegerCamp;
            if (camp == null) return;
            try
            {
                var ev = camp.SiegeEvent;
                var st = ev != null ? ev.BesiegedSettlement : null;
                if (st == null) return;
                bool lead = camp.LeaderParty == party;
                var me = party.MapEvent;
                bool assault = me != null && me.IsSiegeAssault && me.AttackerSide != null && me.AttackerSide.LeaderParty == party.Party && me.MapEventSettlement == st;
                if (!lead && !assault) return;
                var scores = partyThinkParams.AIBehaviorScores;
                if (scores == null) return;
                int bi = -1, si = -1; float best = float.MinValue, sv = -1f;
                for (int i = 0; i < scores.Count; i++)
                {
                    var e = scores[i];
                    if (e.Item2 > best) { best = e.Item2; bi = i; }
                    if (e.Item1.AiBehavior == AiBehavior.BesiegeSettlement && !e.Item1.WillGatherArmy && ReferenceEquals(e.Item1.Party, st) && (si < 0 || e.Item2 > sv)) { si = i; sv = e.Item2; }
                }
                AIBehaviorData bd = bi >= 0 ? scores[bi].Item1 : AIBehaviorData.Invalid;
                float b0 = bi >= 0 ? best : -1f;
                bool held = false;
                bool rule = assault && RunsToEndOn && !party.IsMainParty && !me.IsPlayerMapEvent && !me.IsNavalMapEvent && !partyThinkParams.DoNotChangeBehavior && !Scripted(party) && !Und(party, st);
                if (rule && me.BattleStartTime.ElapsedHoursUntilNow > HoldMaxHours) { rule = false; _dHoldOld++; }   // bezpiecznik: bitwa ponad 96 h - mysl gry
                if (rule)
                {
                    _dHoldH++;
                    // gra (AIThinkPatch :347) konczy szturm, gdy najlepszy cel nie jest oblezeniem / szturmem / rabunkiem tej osady
                    bool keep = bi >= 0 && ReferenceEquals(bd.Party, st)
                                && (bd.AiBehavior == AiBehavior.BesiegeSettlement || bd.AiBehavior == AiBehavior.AssaultSettlement || bd.AiBehavior == AiBehavior.RaidSettlement);
                    if (!keep || b0 < HoldFloor)
                    {
                        float v = Math.Max(b0 * 1.05f + 0.01f, HoldFloor);
                        if (si >= 0) { var d = scores[si].Item1; partyThinkParams.SetBehaviorScore(in d, v); }
                        else partyThinkParams.AddBehaviorScore((new AIBehaviorData(st, AiBehavior.BesiegeSettlement, MobileParty.NavigationType.Default, false, false, false), v));
                        held = true;
                        if (keep) _dHoldLow++;
                        else
                        {
                            _dHoldSaved++;
                            if (LogOn && _skLogged.Add(me)) SkLine(party, st, ev, me, bd, b0, v);
                        }
                    }
                }
                if (lead && LogOn) Remember(ev, camp, party, bi >= 0, bd, b0, sv, held);
            }
            catch (Exception e) { Stumble3("ThinkPost", e); }
        }

        private static void Remember(SiegeEvent ev, BesiegerCamp camp, MobileParty lp, bool any, AIBehaviorData bd, float b0, float sv, bool held)
        {
            Snap sn;
            if (!_snap.TryGetValue(ev, out sn)) { sn = new Snap(); _snap[ev] = sn; }
            sn.Hour = CampaignTime.Now.ToHours; sn.Leader = Hn(lp); sn.Fac = lp.MapFaction != null ? lp.MapFaction.StringId : "-";
            sn.Beh = any ? bd.AiBehavior.ToString() : "-"; sn.Tgt = any ? Target(bd) : "-"; sn.Score = b0; sn.Siege = sv; sn.Held = held;
            int men = 0, food = int.MaxValue;
            foreach (var pb in camp.GetInvolvedPartiesForEventType(MapEvent.BattleTypes.Siege))
            {
                if (pb == null) continue;
                men += pb.NumberOfHealthyMembers;
                var mp = pb.MobileParty;
                if (mp != null) { int d = mp.GetNumDaysForFoodToLast(); if (d < food) food = d; }
            }
            sn.Men = men; sn.Food = food == int.MaxValue ? -1 : food;
        }

        private static void SkLine(MobileParty party, Settlement st, SiegeEvent ev, MapEvent me, AIBehaviorData bd, float b0, float v)
        {
            try
            {
                Log.Info("Szturm do konca (SK): dzien " + Today + " | osada " + st.StringId + " (" + Nm(st) + ", " + Cls(st) + ") | wodz " + Hn(party) + " (frakcja "
                         + (party.MapFaction != null ? party.MapFaction.StringId : "-") + ", armia " + (party.Army != null ? "tak" : "nie") + ") | szturm od "
                         + F(me.BattleStartTime.ElapsedHoursUntilNow, "0") + " h | mysl wybrala: " + bd.AiBehavior + " " + Target(bd) + (bd.WillGatherArmy ? " (zbiorka armii)" : "")
                         + " (wynik " + F(b0, "0.000") + ") - zostaje szturm (wynik " + F(v, "0.000") + ") | odsiecz w drodze " + BesiegedRelief.ReliefOnWay(ev) + ".");
            }
            catch (Exception e) { Stumble3("SkLine", e); }
        }

        /// <summary>Prefiks StrategicCampaignAI145Behavior.TryRetreatFromBadSiege (co 4 h, kazda armia krolestwa): wodz armii w bitwie szturmu albo pod murami -
        /// odwrot SCA czeka do konca bitwy (false). Poza bitwa i przy wylaczonym kluczu - SCA jak dotad. Nie rzuca.</summary>
        public static bool ScaRetreatPre(Army __0)
        {
            try
            {
                if (!RunsToEndOn || __0 == null) return true;
                var lp = __0.LeaderParty;
                if (lp == null || lp.IsMainParty) return true;
                var me = lp.MapEvent;
                if (me == null || me.IsPlayerMapEvent || !(me.IsSiegeAssault || me.IsSiegeOutside)) return true;
                if (me.BattleStartTime.ElapsedHoursUntilNow > HoldMaxHours) { _dHoldOld++; return true; }   // bezpiecznik: bitwa ponad 96 h - SCA jak dotad
                _dScaHeld++;
                return false;
            }
            catch (Exception e) { Stumble3("ScaRetreatPre", e); return true; }
        }

        // ============================================================ W: trebusz AI bije w mur
        private static int _dAimed, _dAimIdx, _dTrebWall, _dTrebBroken, _dTrebEngUp, _dTrebEngDown, _dTrebEngPre, _dRepelWait;
        /// <summary>Poprawka po recenzji: pociski z zapisu (BesiegerCamp._siegeEngineMissiles [SaveableField(1)]) leca najwyzej czas przeladowania trebusza
        /// (1440 / 10 strzalow na dobe = 2.4 h w grze, 4.8 h przy RBM 5 na dobe) - trafienia w machiny w pierwszych 12 h po wczytaniu liczone osobno.</summary>
        private const double PreLoadHours = 12.0;
        private static double _loadH = -1.0;                                     // godzina kampanii przy linii startowej sesji (SessionStart3)

        /// <summary>Oboz AI w zakresie W (jak TB): twierdza, wodz nie gracz, gracz nie w tym obozie, bez skryptu fabuly i Innych.</summary>
        private static bool AiCampOf(BesiegerCamp camp, MobileParty lp, Settlement st)
        {
            if (st == null || st.Town == null || !st.IsFortification || lp == null || lp.IsMainParty) return false;
            if (camp != null && MobileParty.MainParty != null && MobileParty.MainParty.BesiegerCamp == camp) return false;
            return !Scripted(lp) && !Und(lp, st);
        }

        /// <summary>Postfiks Low BesiegerCamp.GetAttackTarget (siegeEngine __1, targetType __3, targetIndex __4): trebusz obozu AI celuje w najslabszy stojacy
        /// odcinek murow, dopoki jakis stoi; brak stojacego odcinka - wybor gry. Szybkie wyjscie dla innych machin. Nie rzuca.</summary>
        public static void AimPost(BesiegerCamp __instance, SiegeEngineType __1, ref SiegeBombardTargets __3, ref int __4)
        {
            if (__1 == null || __instance == null || __1 != DefaultSiegeEngineTypes.Trebuchet || !HitWallsOn) return;
            try
            {
                var ev = __instance.SiegeEvent;
                var st = ev != null ? ev.BesiegedSettlement : null;
                if (!AiCampOf(__instance, __instance.LeaderParty, st)) return;
                var l = st.SettlementWallSectionHitPointsRatioList;
                if (l == null) return;
                int bi = -1; float bv = float.MaxValue;
                for (int i = 0; i < l.Count; i++) if (l[i] > 0f && l[i] < bv) { bv = l[i]; bi = i; }
                if (bi < 0) return;                                                    // mury rozbite - wybor gry (machiny obroncow)
                if (__3 == SiegeBombardTargets.Wall) { if (__4 != bi) { __4 = bi; _dAimIdx++; } return; }
                __3 = SiegeBombardTargets.Wall; __4 = bi;
                _dAimed++;
            }
            catch (Exception e) { Stumble3("AimPost", e); }
        }

        /// <summary>Z SiegeWalls.OnWallHit (zdarzenie gry OnSiegeBombardmentWallHit): trafienia trebuszy obozow AI w mury i rozbite odcinki.</summary>
        internal static void NoteWallHit(MobileParty besieger, Settlement st, BattleSideEnum side, SiegeEngineType t, bool broken)
        {
            try
            {
                if (t != DefaultSiegeEngineTypes.Trebuchet || side != BattleSideEnum.Attacker || st == null) return;
                var camp = st.SiegeEvent != null ? st.SiegeEvent.BesiegerCamp : null;
                if (!AiCampOf(camp, camp != null && camp.LeaderParty != null ? camp.LeaderParty : besieger, st)) return;
                _dTrebWall++;
                if (broken) _dTrebBroken++;
            }
            catch (Exception e) { Stumble3("NoteWallHit", e); }
        }

        /// <summary>Nasluch OnSiegeBombardmentHit (ArmouryBehavior): trebusz obozu AI trafil machine obroncow - przy stojacym murze (z kluczem W ma byc 0) i po rozbiciu murow.</summary>
        internal static void OnBombardHit(MobileParty besieger, Settlement st, BattleSideEnum side, SiegeEngineType weapon, SiegeBombardTargets target)
        {
            try
            {
                if (weapon != DefaultSiegeEngineTypes.Trebuchet || side != BattleSideEnum.Attacker || target != SiegeBombardTargets.RangedEngines || st == null) return;
                var camp = st.SiegeEvent != null ? st.SiegeEvent.BesiegerCamp : null;
                if (!AiCampOf(camp, camp != null && camp.LeaderParty != null ? camp.LeaderParty : besieger, st)) return;
                bool up = false;
                var l = st.SettlementWallSectionHitPointsRatioList;
                if (l != null) for (int i = 0; i < l.Count; i++) if (l[i] > 0f) { up = true; break; }
                if (!up) _dTrebEngDown++;
                else if (_loadH >= 0.0 && CampaignTime.Now.ToHours - _loadH < PreLoadHours) _dTrebEngPre++;   // pocisk mogl wyleciec przed wczytaniem (bez latki)
                else _dTrebEngUp++;
            }
            catch (Exception e) { Stumble3("OnBombardHit", e); }
        }

        /// <summary>F1 (SiegeRansom.AssaultPost): cykl decyzji, w ktorym okup po odpartym szturmie odpadl, bo przewaga dalej >= Siege Ladder Min Ratio.</summary>
        internal static void NoteRepelWait() { _dRepelWait++; }

        // ============================================================ doba, porzadek, start
        private static void PruneAt(Dictionary<Settlement, double> d, double now)
        {
            if (d.Count == 0) return;
            List<Settlement> old = null;
            foreach (var kv in d) if (kv.Key == null || now - kv.Value > 1.0) { if (old == null) old = new List<Settlement>(); old.Add(kv.Key); }
            if (old != null) foreach (var s in old) d.Remove(s);
        }

        /// <summary>Z SiegeWalls2.Daily (finally, po linii "Oblezenia 2 (doba)"): porzadek pamieci i linia "Oblezenia 3 (doba)".</summary>
        private static void Daily3()
        {
            try
            {
                double now = NowD;
                PruneAt(_capAt, now); PruneAt(_abortAt, now);
                if (_abortRule.Count > 0)
                {
                    List<Settlement> gone = null;
                    foreach (var kv in _abortRule) if (kv.Key == null || !_abortAt.ContainsKey(kv.Key)) { if (gone == null) gone = new List<Settlement>(); gone.Add(kv.Key); }
                    if (gone != null) foreach (var s in gone) _abortRule.Remove(s);
                }
                List<SiegeEvent> dead = null;
                foreach (var kv in _snap) if (kv.Key == null || kv.Key.BesiegedSettlement == null || kv.Key.BesiegedSettlement.SiegeEvent != kv.Key) { if (dead == null) dead = new List<SiegeEvent>(); dead.Add(kv.Key); }
                if (dead != null) foreach (var ev in dead) _snap.Remove(ev);
                _koDone.RemoveWhere(e => e == null || e.BesiegedSettlement == null || e.BesiegedSettlement.SiegeEvent != e);   // oblezenia skonczone - drugie zdarzenie przychodzi od razu
                _skLogged.RemoveWhere(m => m == null || m.IsFinalized);
                if (!LogOn) return;
                var sb = new StringBuilder(900);
                sb.Append("Oblezenia 3 (doba): dzien ").Append(Today)
                  .Append(" | klucze: szturm do konca ").Append(RunsToEndOn ? "tak" : "nie").Append(", trebusze AI w mury ").Append(HitWallsOn ? "tak" : "nie")
                  .Append(", okup po odparciu tylko przy przewadze < ").Append(F(LadderRatio, "0.0")).Append(" : 1 ").Append(RepelEdgeOn ? "tak" : "nie")
                  .Append(" | konce oblezen dzis ").Append(_dEnds).Append(" (zdobycie ").Append(_dEnd[0]).Append(", pokonani ").Append(_dEnd[1]).Append(", okup ").Append(_dEnd[2])
                  .Append(", SCA odwrot ").Append(_dEnd[3]).Append(", mysl wodza ").Append(_dEnd[4]).Append(", armia ").Append(_dEnd[5]).Append(", pokoj ").Append(_dEnd[6])
                  .Append(", frakcja zniknela ").Append(_dEnd[7]).Append(", odejscie z obozu ").Append(_dEnd[8]).Append(", inne ").Append(_dEnd[9])
                  .Append("; szturmy przerwane ").Append(_dEndAbort).Append(", w tym mysl wodza / SCA pod regula SK ").Append(_dEndAbortOurs).Append(')')
                  .Append(" | szturm do konca: godzin szturmu pod regula ").Append(_dHoldH).Append(", obca mysl wybrala inny cel ").Append(_dHoldSaved)
                  .Append(", niska ocena szturmu podniesiona ").Append(_dHoldLow).Append(", odwrot SCA wstrzymany ").Append(_dScaHeld)
                  .Append(", bitwa ponad ").Append(F(HoldMaxHours, "0")).Append(" h - mysl gry ").Append(_dHoldOld)
                  .Append(" | trebusze AI: przestawione na mury ").Append(_dAimed).Append(", na najslabszy odcinek ").Append(_dAimIdx)
                  .Append(", trafienia w mury ").Append(_dTrebWall).Append(" (odcinki rozbite ").Append(_dTrebBroken).Append(")")
                  .Append(", w machiny przy stojacym murze ").Append(_dTrebEngUp).Append(" (pociski sprzed wczytania ").Append(_dTrebEngPre)
                  .Append(", po rozbiciu murow ").Append(_dTrebEngDown).Append(')')
                  .Append(" | okup po odparciu odmowiony (przewaga >= ").Append(F(LadderRatio, "0.0")).Append(":1) ").Append(_dRepelWait)
                  .Append(" | w pamieci: mysli wodzow ").Append(_snap.Count).Append(", szturmy z SK ").Append(_skLogged.Count)
                  .Append(" | potkniecia ").Append(_dStumbles3).Append(" (od wczytania ").Append(_stumbles3).Append(").");
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Stumble3("Daily3", e); }
            finally { ZeroDay3(); }
        }

        private static void ZeroDay3()
        {
            for (int i = 0; i < _dEnd.Length; i++) _dEnd[i] = 0;
            _dEnds = _dEndAbort = _dEndAbortOurs = 0;
            _dHoldH = _dHoldSaved = _dHoldLow = _dScaHeld = _dHoldOld = 0;
            _dAimed = _dAimIdx = _dTrebWall = _dTrebBroken = _dTrebEngUp = _dTrebEngDown = _dTrebEngPre = _dRepelWait = 0;
            _dStumbles3 = 0;
        }

        /// <summary>Z SiegeWalls2.Reset (nowa kampania i kazde wczytanie).</summary>
        private static void Reset3()
        {
            _capAt.Clear(); _abortAt.Clear(); _abortRule.Clear(); _koDone.Clear(); _snap.Clear(); _skLogged.Clear();
            _loadH = -1.0;
            ZeroDay3();
            _stumbles3 = 0; _err3.Clear();
        }

        // ------------------------------------------------------------ latki w kampanii (z SiegeWalls2.EnsureHooks, raz na proces)
        private static string _hookThink = "nie zakladana", _hookSca = "nie zakladana", _hookAim = "nie zakladana";

        private static void EnsureHooks3()
        {
            try
            {
                var m = AccessTools.Method(typeof(CampaignEventDispatcher), "AiHourlyTick", new[] { typeof(MobileParty), typeof(PartyThinkParams) });
                if (m == null) _hookThink = "BRAK (CampaignEventDispatcher.AiHourlyTick)";
                else
                {
                    Seal31.Patch("oblezenia 3: szturm do konca - mysl wodza (CampaignEventDispatcher.AiHourlyTick)", m, typeof(SiegeWalls2), null, nameof(ThinkPost), null, Priority.Normal, Priority.Last);
                    _hookThink = Wired(m, nameof(ThinkPost), false);
                }
            }
            catch (Exception e) { _hookThink = "BRAK (blad: " + e.Message + ")"; Stumble3("EnsureHooks.Think", e); }
            try
            {
                var t = AccessTools.TypeByName("StrategicCampaignAI145.StrategicCampaignAI145Behavior");
                var m = t != null ? AccessTools.DeclaredMethod(t, "TryRetreatFromBadSiege", new[] { typeof(Army) }) : null;
                if (t == null) _hookSca = "bez SCA";
                else if (m == null) _hookSca = "BRAK (StrategicCampaignAI145Behavior.TryRetreatFromBadSiege)";
                else
                {
                    Seal31.Patch("oblezenia 3: szturm do konca - odwrot SCA (StrategicCampaignAI145Behavior.TryRetreatFromBadSiege)", m, typeof(SiegeWalls2), nameof(ScaRetreatPre), null, null, Priority.Normal);
                    _hookSca = Wired(m, nameof(ScaRetreatPre), true);
                }
            }
            catch (Exception e) { _hookSca = "BRAK (blad: " + e.Message + ")"; Stumble3("EnsureHooks.Sca", e); }
            try
            {
                var m = AccessTools.DeclaredMethod(typeof(BesiegerCamp), "GetAttackTarget",
                                                   new[] { typeof(ISiegeEventSide), typeof(SiegeEngineType), typeof(int), typeof(SiegeBombardTargets).MakeByRefType(), typeof(int).MakeByRefType() })
                        ?? AccessTools.DeclaredMethod(typeof(BesiegerCamp), "GetAttackTarget");   // jedna metoda o tej nazwie (gra 1.4.8) - gdyby sygnatura z out sie nie dopasowala
                if (m == null) _hookAim = "BRAK (BesiegerCamp.GetAttackTarget)";
                else
                {
                    Seal31.Patch("oblezenia 3: cel trebusza AI (BesiegerCamp.GetAttackTarget)", m, typeof(SiegeWalls2), null, nameof(AimPost), null, Priority.Normal, Priority.Low);
                    _hookAim = Wired(m, nameof(AimPost), false);
                }
            }
            catch (Exception e) { _hookAim = "BRAK (blad: " + e.Message + ")"; Stumble3("EnsureHooks.Aim", e); }
        }

        /// <summary>Linia "Oblezenia 3 (start)" (z SiegeWalls2.SessionStart): latki i klucze.</summary>
        private static void SessionStart3()
        {
            try { _loadH = CampaignTime.Now.ToHours; } catch (Exception e) { Stumble3("SessionStart3.Clock", e); }   // W: okno pociskow sprzed wczytania
            try
            {
                var s = Settings.Current;
                bool banners = s != null && s.GentryBannersNeedVassalParty, k179 = s != null && s.GentryNoParties;
                Log.Info("Oblezenia 3 (start): latki - szturm do konca: mysl wodza (CampaignEventDispatcher.AiHourlyTick, Last) " + _hookThink
                         + ", odwrot SCA (StrategicCampaignAI145Behavior.TryRetreatFromBadSiege) " + _hookSca + ", cel trebusza (BesiegerCamp.GetAttackTarget) " + _hookAim
                         + " | klucze: szturm do konca " + (RunsToEndOn ? "tak" : "nie") + ", okup po odparciu tylko przy przewadze < " + F(LadderRatio, "0.0") + " : 1 "
                         + (RepelEdgeOn ? "tak" : "nie") + ", trebusze AI w mury " + (HitWallsOn ? "tak" : "nie") + ", choragwie BK tylko z druzyna wasala " + (banners ? "tak" : "nie")
                         + " (rycerze 179 " + (k179 ? "tak" : "nie") + "), log " + (LogOn ? "tak" : "nie") + ".");
            }
            catch (Exception e) { Stumble3("SessionStart3", e); }
        }
    }
}
