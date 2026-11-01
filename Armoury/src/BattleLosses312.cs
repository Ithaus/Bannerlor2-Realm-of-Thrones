using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>
    /// 312f (Jeff 01.11 "tak mozesz" - rozbic straty przegranych na zabitych / jencow / zbieglych; rozp312 straty-raport.md + straty-krytyk.md "Ostateczna
    /// propozycja" pkt 2). SAM POMIAR - zadnej zmiany zachowania, zadnej nowej latki: jedna linia "Straty bitwy (312f)" na bitwe z wynikiem (zwyciezca),
    /// z graczem albo AI-AI od MinSide (100) ludzi na kazda strone (HealthyManCountAtStart), klucz BattleLossesLog.
    ///
    /// Skad liczby (tylko istniejace haki - kolejnosc w grze 1.4.8, MapEvent.CalculateAndCommitMapEventResults, dekompilacja rozp312/dec MapEvent.cs:1411-1431):
    ///  1. MIGAWKA PO WALCE, PRZED H3: KingdomBalance.ResultsPrefix (prefiks CalculateAndCommitMapEventResults, Priority 801 - przed LosersFlee 800) wola Snap.
    ///     Szeregowi (bez bohaterow) kazdej partii stron: zabici = DiedInBattle, nieprzytomni = WoundedInBattle, zbiegli z pola = RoutedInBattle,
    ///     zdrowi na polu = zdrowi w MemberRoster; wystawieni = suma tych czterech. Ranni sprzed bitwy = ranni w MemberRoster ponad WoundedInBattle
    ///     (MapEventParty.OnTroopWounded rani tez w MemberRoster - MapEventParty.cs:309-314) - nie walczyli, ale ida z partia do niewoli.
    ///     Tryb: misja / symulacja (LosersFlee.PlayerAuto - BattleSimulation jest jeszcze w PlayerEncounter) / AI. Wodz pobitej strony (LeaderParty.LeaderHero
    ///     - CaptureDefeatedPartyMembers zdejmuje go potem RemovePartyLeader).
    ///  2. OKNO POJMANIA: Measure40.CapturePre / CaptureFin (prefiks First + finalizer na MapEvent.CaptureDefeatedPartyMembers, E4/0 W10) wolaja
    ///     CaptureOpen / CaptureClose. Gra (MapEvent.cs:1955-2025) kazdego szeregowego pobitej partii daje do RosterToReceiveLootPrisoners zwyciezcy (ranny
    ///     z woundedCount 1) i zeruje sklad - takze gdy odbiorcy nie ma albo CanTroopBeTakenPrisoner = false (ci znikaja: "bez odbiorcy"). Odbiorcy:
    ///     partia AI - jej PrisonRoster (milicja / zaloga - loch osady; MapEventParty.cs:87-101), gracz - PlayerEncounter._alternativeRosterToReceiveLootPrisoners
    ///     czytany POLEM (getter tworzy roster leniwie - Measure40 go tez nie dotyka). Przyrost TotalRegulars / TotalWoundedRegulars = jency ranni / zdrowi
    ///     u gracza i u AI (bez bohaterow). Notatka H3 tej bitwy (LosersFlee.A17NoteOf - zdejmowana przy MapEventEnded) czytana tu.
    ///  3. MapEventEnded: koniec OutlawLaw.OnMapEventEnded (po RoutedReturn307e - tylko zwyciezcy, DoomedPartyMen.Before i podziale rozbitych RoutedH3Body):
    ///     A17 (DoomedPartyMen) - jency z partii kasowanych (NoteDoomed) i znacznik "gra skasuje" (DoomedNow); rozbici przegranych wedlug RoutedH3Body
    ///     (NoteRouted na partie: pod choragiew N7 / do domu / do lasu / do karczmy / w nicosc / Inni); zabici i zbiegli koncowi (po H3 i A17), zostali
    ///     w skladzie, los wodza (niewola / u gracza / zginal / zbiegl / wolny), zwyciezcy zabici / nieprzytomni.
    /// Kontrola: wystawieni + ranni sprzed bitwy - zabici - zbiegli - jency (gracz + AI + A17) - bez odbiorcy - zostali w skladzie = 0 (H3 i A17 tylko
    /// przesuwaja ludzi). Rozjazd != 0 = ktos poza tymi drogami zmienil sklad pobitych (do sprawdzenia w kodzie).
    /// Koszt: migawka tylko bitew gracza i AI od 100 na strone (filtr z HealthyManCountAtStart, bez petli po skladzie); reszta - slownik slaby (bitwa bez
    /// MapEventEnded znika z GC). Wyjatek per wywolanie liczony (potkniecia w linii, pierwszy blad miejsca do logu) - bez globalnego wylacznika.
    /// </summary>
    internal static class BattleLosses312
    {
        internal const int MinSide = 100;
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private sealed class Recv { public TroopRoster R; public bool Player; public int Regs, Wnd; }

        private sealed class Rec
        {
            public int Id, Day; public string Mode, Type, Where, LoseName, LeaderName; public Hero Leader;
            public BattleSideEnum LoseSide;
            public readonly List<MapEventParty> Lose = new List<MapEventParty>(), Win = new List<MapEventParty>();
            // po walce (przed H3)
            public int Died0, Wnd0, Routed0, Healthy0, WoundedBefore, WinMen;
            // okno pojmania
            public bool CapSeen; public string H3 = "-";
            public int CapStart, CapEnd, PlG, PlGW, AiG, AiGW, Cap;
            public List<Recv> Rx;
            // MapEventEnded
            public int DoomCapt, DoomCaptW;
            public int RRouted, RRally, RHome, RWoods, RTavern, RVanished, RUndead, RParties;
        }

        private static readonly ConditionalWeakTable<MapEvent, Rec> _recs = new ConditionalWeakTable<MapEvent, Rec>();
        private static Rec _open;                                       // biezace okno pojmania (gra jednowatkowa, CaptureDefeatedPartyMembers bez wnikania)
        private static int _seq, _lines, _stumbles;
        private static readonly HashSet<string> _errSites = new HashSet<string>();
        private static AccessTools.FieldRef<PlayerEncounter, TroopRoster> _plRef;
        private static bool _plRefTried;

        internal static bool On { get { var s = Settings.Current; return s != null && s.BattleLossesLog; } }

        internal static void Reset() { _open = null; _seq = 0; _lines = 0; _stumbles = 0; _errSites.Clear(); }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            try { if (_errSites.Add(where)) Log.Error("BattleLosses312." + where, e); } catch { }
        }

        // ------------------------------------------------------------ liczenie szeregowych
        private static void Regs(TroopRoster r, out int men, out int wounded)
        {
            men = wounded = 0;
            if (r == null) return;
            for (int i = 0; i < r.Count; i++)
            {
                var e = r.GetElementCopyAtIndex(i);
                if (e.Character == null || e.Character.IsHero || e.Number <= 0) continue;
                men += e.Number; wounded += Math.Min(e.WoundedNumber, e.Number);
            }
        }

        private static int RegsOf(TroopRoster r) { int m, w; Regs(r, out m, out w); return m; }

        private static int StartOf(MapEventSide side)
        {
            int n = 0;
            if (side == null) return 0;
            for (int i = 0; i < side.Parties.Count; i++) { var p = side.Parties[i]; if (p != null) n += p.HealthyManCountAtStart; }
            return n;
        }

        private static TroopRoster PlayerRoster()
        {
            if (!_plRefTried)
            {
                _plRefTried = true;
                try { _plRef = AccessTools.FieldRefAccess<PlayerEncounter, TroopRoster>("_alternativeRosterToReceiveLootPrisoners"); } catch { _plRef = null; }
            }
            var pe = PlayerEncounter.Current;
            if (pe == null || _plRef == null) return null;
            return _plRef(pe);                                          // pole - bez leniwego tworzenia rostera w getterze
        }

        // ------------------------------------------------------------ 1. migawka po walce (KingdomBalance.ResultsPrefix, Priority 801 - przed H3)
        internal static void Snap(MapEvent me)
        {
            try
            {
                if (!On || me == null) return;
                var bs = me.BattleState;
                if (bs != BattleState.AttackerVictory && bs != BattleState.DefenderVictory) return;
                Rec old;
                if (_recs.TryGetValue(me, out old)) return;
                var lose = me.GetMapEventSide(me.DefeatedSide); var win = me.GetMapEventSide(me.WinningSide);
                if (lose == null || win == null) return;
                bool player = me.IsPlayerMapEvent;
                if (!player && (StartOf(lose) < MinSide || StartOf(win) < MinSide)) return;
                var r = new Rec { Id = ++_seq, Day = (int)CampaignTime.Now.ToDays, LoseSide = me.DefeatedSide };
                r.Mode = !player ? "AI" : LosersFlee.PlayerAuto(me) ? "symulacja" : "misja";
                try { r.Type = me.EventType.ToString(); } catch { r.Type = "?"; }
                try { r.Where = me.MapEventSettlement != null && me.MapEventSettlement.Name != null ? me.MapEventSettlement.Name.ToString() : "-"; } catch { r.Where = "-"; }
                try
                {
                    var lp = lose.LeaderParty;
                    r.Leader = lp != null ? lp.LeaderHero : null;
                    r.LeaderName = r.Leader != null && r.Leader.Name != null ? r.Leader.Name.ToString() : "-";
                    r.LoseName = lp != null && lp.Name != null ? lp.Name.ToString() : "?";
                }
                catch { r.LeaderName = "-"; r.LoseName = "?"; }
                for (int i = 0; i < lose.Parties.Count; i++)
                {
                    var mep = lose.Parties[i];
                    if (mep == null || mep.Party == null) continue;
                    r.Lose.Add(mep);
                    int d, dw, wb, wbw, ro, row, m, mw;
                    Regs(mep.DiedInBattle, out d, out dw);
                    Regs(mep.WoundedInBattle, out wb, out wbw);
                    Regs(mep.RoutedInBattle, out ro, out row);
                    Regs(mep.Party.MemberRoster, out m, out mw);
                    r.Died0 += d; r.Wnd0 += wb; r.Routed0 += ro; r.Healthy0 += m - mw;
                    r.WoundedBefore += Math.Max(0, mw - wb);
                }
                for (int i = 0; i < win.Parties.Count; i++)
                {
                    var mep = win.Parties[i];
                    if (mep == null || mep.Party == null) continue;
                    r.Win.Add(mep);
                    int m, mw;
                    Regs(mep.Party.MemberRoster, out m, out mw);
                    r.WinMen += RegsOf(mep.DiedInBattle) + RegsOf(mep.RoutedInBattle) + m;   // ranni w MemberRoster (z WoundedInBattle) juz w m
                    r.WinMen -= Math.Max(0, mw - RegsOf(mep.WoundedInBattle));             // bez rannych sprzed bitwy
                }
                _recs.Add(me, r);
            }
            catch (Exception e) { Stumble("Snap", e); }
        }

        // ------------------------------------------------------------ 2. okno pojmania (Measure40.CapturePre / CaptureFin na CaptureDefeatedPartyMembers)
        internal static void CaptureOpen(MapEvent me, MBReadOnlyList<MapEventParty> winners)
        {
            _open = null;
            try
            {
                Rec r;
                if (me == null || !_recs.TryGetValue(me, out r) || r.CapSeen) return;
                r.CapSeen = true;
                try { r.H3 = LosersFlee.A17NoteOf(me); } catch { r.H3 = "?"; }
                int start = 0;
                foreach (var mep in r.Lose) { try { start += RegsOf(mep.Party.MemberRoster); } catch (Exception e) { Stumble("CaptureOpen(sklad)", e); } }
                r.CapStart = start;
                r.Rx = new List<Recv>(4);
                bool pl = false;
                if (winners != null)
                {
                    for (int i = 0; i < winners.Count; i++)
                    {
                        try
                        {
                            var mep = winners[i];
                            if (mep == null || mep.Party == null) continue;
                            if (!mep.IsNpcParty)
                            {
                                if (pl) continue;
                                pl = true;
                                var pr = PlayerRoster();
                                var rv = new Recv { R = pr, Player = true };
                                if (pr != null) Regs(pr, out rv.Regs, out rv.Wnd);
                                r.Rx.Add(rv);
                                continue;
                            }
                            var ro = mep.RosterToReceiveLootPrisoners;
                            if (ro == null) continue;
                            bool dup = false;
                            foreach (var x in r.Rx) if (ReferenceEquals(x.R, ro)) { dup = true; break; }
                            if (dup) continue;
                            var nv = new Recv { R = ro };
                            Regs(ro, out nv.Regs, out nv.Wnd);
                            r.Rx.Add(nv);
                        }
                        catch (Exception e) { Stumble("CaptureOpen(odbiorca)", e); }
                    }
                }
                _open = r;
            }
            catch (Exception e) { _open = null; Stumble("CaptureOpen", e); }
        }

        internal static void CaptureClose(MapEvent me)
        {
            var r = _open;
            _open = null;
            try
            {
                Rec k;
                if (r == null || me == null || !_recs.TryGetValue(me, out k) || !ReferenceEquals(k, r)) return;
                int end = 0;
                foreach (var mep in r.Lose) { try { end += RegsOf(mep.Party.MemberRoster); } catch (Exception e) { Stumble("CaptureClose(sklad)", e); } }
                r.CapEnd = end;
                foreach (var x in r.Rx)
                {
                    try
                    {
                        var ro = x.Player ? PlayerRoster() : x.R;               // gracz: roster mogl powstac dopiero w pojmaniu
                        if (ro == null) continue;
                        int m, w;
                        Regs(ro, out m, out w);
                        int dm = Math.Max(0, m - (x.Player && x.R == null ? 0 : x.Regs)), dw = Math.Max(0, w - (x.Player && x.R == null ? 0 : x.Wnd));
                        dw = Math.Min(dw, dm);
                        if (x.Player) { r.PlG += dm; r.PlGW += dw; } else { r.AiG += dm; r.AiGW += dw; }
                    }
                    catch (Exception e) { Stumble("CaptureClose(odbiorca)", e); }
                }
                r.Cap = r.PlG + r.AiG;
            }
            catch (Exception e) { Stumble("CaptureClose", e); }
        }

        // ------------------------------------------------------------ 3. MapEventEnded (OutlawLaw.OnMapEventEnded)
        /// <summary>DoomedPartyMen.One: jency z partii pobitej strony, ktora gra skasuje (A17) - przy MapEventEnded, po oknie pojmania.</summary>
        internal static void NoteDoomed(MapEvent me, int capt, int captWounded)
        {
            try
            {
                Rec r;
                if (capt <= 0 || me == null || !_recs.TryGetValue(me, out r)) return;
                r.DoomCapt += capt; r.DoomCaptW += Math.Min(capt, Math.Max(0, captWounded));
            }
            catch (Exception e) { Stumble("NoteDoomed", e); }
        }

        /// <summary>OutlawLaw.RoutedH3Body: podzial rozbitych jednej partii (przyrost licznikow bitwy). Tylko pobita strona.</summary>
        internal static void NoteRouted(MapEvent me, BattleSideEnum side, int routed, int rally, int home, int woods, int tavern, int vanished, int undead)
        {
            try
            {
                Rec r;
                if (routed <= 0 || me == null || !_recs.TryGetValue(me, out r) || side != r.LoseSide) return;
                r.RParties++; r.RRouted += routed; r.RRally += rally; r.RHome += home; r.RWoods += woods; r.RTavern += tavern; r.RVanished += vanished; r.RUndead += undead;
            }
            catch (Exception e) { Stumble("NoteRouted", e); }
        }

        private static string Pct(int x, int of) { return of > 0 ? (100.0 * x / of).ToString("0.0", Inv) + "%" : "-"; }

        private static string Fate(Rec r)
        {
            var h = r.Leader;
            if (h == null) return "brak wodza";
            try
            {
                if (h.IsPrisoner) return "niewola";
                var pr = PlayerRoster();
                if (pr != null && h.CharacterObject != null && pr.FindIndexOfTroop(h.CharacterObject) >= 0) return "niewola u gracza";
                if (h.IsDead || h.DeathMark == KillCharacterAction.KillCharacterActionDetail.DiedInBattle) return "zginal";
                if (h.HeroState == Hero.CharacterStates.Fugitive) return "zbiegl";
                return "wolny";
            }
            catch { return "?"; }
        }

        /// <summary>Koniec OutlawLaw.OnMapEventEnded (po DoomedPartyMen.Before i RoutedH3Body): linia "Straty bitwy (312f)". Nie rzuca.</summary>
        internal static void OnEnded(MapEvent me)
        {
            try
            {
                Rec r;
                if (me == null || !_recs.TryGetValue(me, out r)) return;
                _recs.Remove(me);
                if (!On) return;
                int died = 0, routed = 0, left = 0, doomed = 0;
                foreach (var mep in r.Lose)
                {
                    try
                    {
                        died += RegsOf(mep.DiedInBattle);
                        routed += RegsOf(mep.RoutedInBattle);
                        left += RegsOf(mep.Party.MemberRoster);
                        if (DoomedPartyMen.DoomedNow(mep.Party)) doomed++;
                    }
                    catch (Exception e) { Stumble("OnEnded(partia)", e); }
                }
                int wDied = 0, wWnd = 0;
                foreach (var mep in r.Win)
                {
                    try { wDied += RegsOf(mep.DiedInBattle); wWnd += RegsOf(mep.WoundedInBattle); }
                    catch (Exception e) { Stumble("OnEnded(zwyciezca)", e); }
                }
                int fought = r.Died0 + r.Wnd0 + r.Routed0 + r.Healthy0;
                int noRecv = r.CapSeen ? Math.Max(0, r.CapStart - r.CapEnd - r.Cap) : 0;
                int capt = r.Cap + r.DoomCapt;
                int ctl = fought + r.WoundedBefore - died - routed - capt - noRecv - left;
                var sb = new StringBuilder(900);
                sb.Append("Straty bitwy (312f): bitwa ").Append(r.Id).Append(" dzien ").Append(r.Day).Append(" [").Append(r.Mode).Append("] typ ").Append(r.Type)
                  .Append(" pod ").Append(r.Where)
                  .Append(" | przegrani ").Append(r.LoseSide == BattleSideEnum.Attacker ? "atakujacy" : "obroncy").Append(" (").Append(r.LoseName).Append(", wodz ")
                  .Append(r.LeaderName).Append(", partii ").Append(r.Lose.Count).Append(", skasowanych ").Append(doomed).Append("): wystawieni ").Append(fought)
                  .Append(", ranni sprzed bitwy ").Append(r.WoundedBefore)
                  .Append(" | po walce (przed H3): zabici ").Append(r.Died0).Append(" (").Append(Pct(r.Died0, fought)).Append(") / nieprzytomni ").Append(r.Wnd0)
                  .Append(" (").Append(Pct(r.Wnd0, fought)).Append(") / zbiegli z pola ").Append(r.Routed0).Append(" (").Append(Pct(r.Routed0, fought))
                  .Append(") / zdrowi na polu ").Append(r.Healthy0).Append(" (").Append(Pct(r.Healthy0, fought)).Append(')')
                  .Append(" | pojmanie ").Append(r.CapSeen ? "TAK" : "NIE").Append(": u gracza ").Append(r.PlG).Append(" (ranni ").Append(r.PlGW).Append(", zdrowi ").Append(r.PlG - r.PlGW)
                  .Append("), u AI ").Append(r.AiG).Append(" (ranni ").Append(r.AiGW).Append(", zdrowi ").Append(r.AiG - r.AiGW).Append("), A17 ").Append(r.DoomCapt)
                  .Append(" (ranni ").Append(r.DoomCaptW).Append("), bez odbiorcy ").Append(noRecv)
                  .Append(" | wynik: zabici ").Append(died).Append(" (").Append(Pct(died, fought)).Append(") / jency ").Append(capt).Append(" (").Append(Pct(capt, fought))
                  .Append(") / zbiegli ").Append(routed).Append(" (").Append(Pct(routed, fought)).Append(") / bez odbiorcy ").Append(noRecv).Append(" / zostali w skladzie ").Append(left)
                  .Append(" | zbiegli wedlug H3 (").Append(r.RParties).Append(" partii): rozbici ").Append(r.RRouted).Append(", pod choragiew ").Append(r.RRally).Append(", do domu ")
                  .Append(r.RHome).Append(", do lasu ").Append(r.RWoods).Append(", do karczmy ").Append(r.RTavern).Append(", w nicosc ").Append(r.RVanished).Append(", Inni ").Append(r.RUndead)
                  .Append(" | wodz: ").Append(Fate(r))
                  .Append(" | zwyciezcy (wystawieni ").Append(r.WinMen).Append("): zabici ").Append(wDied).Append(" (").Append(Pct(wDied, r.WinMen)).Append(") / nieprzytomni ")
                  .Append(wWnd).Append(" (").Append(Pct(wWnd, r.WinMen)).Append(')')
                  .Append(" | H3: ").Append(r.H3)
                  .Append(" | kontrola: wystawieni + ranni sprzed bitwy - zabici - zbiegli - jency - bez odbiorcy - zostali = ").Append(ctl)
                  .Append(" | potkniecia ").Append(_stumbles).Append('.');
                _lines++;
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Stumble("OnEnded", e); }
        }
    }
}
