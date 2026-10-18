using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>
    /// A17 (poz. 11; A12 valyrian / yiti, 14.10) - LUDZIE PARTII KASOWANEJ PO BITWIE NIE IDA W NICOSC. Klucz DestroyedPartyMenRouted (wylaczony = 79a8d89).
    ///
    /// GRA (dekompilacja 1.4.8): MapEvent.FinalizeEventAux wola nasluchy MapEventEnded (:2079), potem MapEventSide.HandleMapEventEnd (:2158) ->
    /// HandleMapEventEndForPartyInternal (MapEventSide.cs:431-446): partia ruchoma (nie gracza, czynna) z NumberOfAllMembers == 0 albo (strona nie wygrala,
    /// bitwa nie skonczona odwrotem bez poscigu (EndedByRetreat), zero zdrowych albo milicja przy BattleState != None, bez armii albo wodz armii bez zdrowych)
    /// i (nie rozwiazywana albo pusty sklad) - DestroyPartyAction; RemoveParty czysci MemberRoster i PrisonRoster (MobileParty.cs:3524-3527). Zwykle pokonana
    /// partia jest juz pusta (CaptureDefeatedPartyMembers zdjal szeregowych), ale pelna zostaje, gdy pojmanie sie nie odbylo:
    ///  (A) dogoniona ucieczka - CaptureDefeatedPartyMembers wychodzi przy RetreatingSide != None (MapEvent.cs:1957), H3 tez (odwrot), EndedByRetreat = false
    ///      po poscigu (PursuitRoundNumber > 0) - dowod: Aegon Targaryen 443, Benjamin Buckler 108 (rok 2 + 60 dob, dni z "odwrot 1" w linii H3);
    ///  (B) bez zwyciezcy - przywodca AI wola FinalizeEvent (AiPartyThinkBehavior.cs:110-121) albo pokoj w trakcie: BattleState None, nikt nie pojmuje,
    ///      partia z samymi rannymi skasowana (fin28: Hullen 29, Denis bar Emmon 93);
    ///  (C) pobity oblegajacy N3 - H3 zostawil j rannych do pojmania, gra ich nie wziela (Daeron Vaith 27 w fin29; warunek nieustalony - linia diagnozy D4).
    ///
    /// REGULA (jedna dla AI wszystkich rodzajow partii; gracza gra nie kasuje): w pierwszym nasluchu MapEventEnded Armoury (OutlawLaw.OnMapEventEnded, PRZED
    /// podzialem rozbitych H3) liczymy ten sam warunek gry dla kazdej partii obu stron. Partia, ktora gra skasuje, a ma szeregowych:
    ///  - przy zwyciestwie drugiej strony (A, C, inne): jency do lochu zwyciezcow (szanse gry GetCaptureMemberChancesForWinnerParties, losowanie odbiorcy
    ///    jak w grze, tylko partie AI): (C) wszyscy pozostawieni przez H3; inaczej stawka H3 - LoserCaptiveVeteranPercent od tieru 4, LoserCaptiveCommonPercent
    ///    reszta, tylko CanTroopBeTakenPrisoner (tabory wsi tylko przez ludy z niewola - jak H3); udzialy pochodzenia PeopleBook.Take/Give (FCaptured);
    ///  - reszta (przy B - wszyscy) do RoutedInBattle tej partii; druzyna lorda dostaje znacznik N7 (StragglersRally.MarkBeaten, gdy H3 go nie dal) -
    ///    OutlawLaw.RoutedH3Body robi z nimi to samo co z kazdym rozbitym: zbiorka pod choragiew / karczma / dom / las (udzialy FRouted ze slownika);
    ///  - jency szeregowi tej partii (lochu gra nie lupila - B) - wolni jak "uwolnieni bez odbiorcy" H3 (LosersFlee.FreedHome: karczma, dom, las).
    /// Ranni szeregowi zdjeci ze skladu nie zmieniaja liczby zdrowych, a milicja i tak jest kasowana przy BattleState != None - warunek gry po regule
    /// ten sam, zniszczenie partii i jej wodz bez zmian. Inni (partia Innych albo wight) - bez zmian (z niczego, do niczego). Linia doby
    /// "Ludzie kasowanych partii (A17)" i do 20 linii diagnozy D4 na sesje (takze przy kluczu wylaczonym - tylko pomiar). Zapisu nie ma.
    /// </summary>
    internal static class DoomedPartyMen
    {
        internal static bool On { get { var s = Settings.Current; return s != null && s.DestroyedPartyMenRouted; } }

        private const int VeteranTier = 4;          // ten sam prog co H3 (LosersFlee.VeteranTier)
        private const int DiagMax = 20;
        private const int PA = 0, PB = 1, PC = 2, POther = 3, Paths = 4;
        private static readonly string[] PathName = { "A dogoniona ucieczka", "B bez zwyciezcy", "C H3 zostawil rannych", "inna" };

        // liczniki doby
        private static int _parties, _men, _routed, _routedNoWin, _noRecv, _freed, _freedTavern, _freedHome, _freedLeft, _undead, _offMen, _offParties, _stumbles, _ctl;
        private static readonly int[] _pathParties = new int[Paths], _pathMen = new int[Paths], _capt = new int[Paths];
        private static int _diag, _stumblesAll;
        // partie, ktore gra skasuje po biezacej bitwie (warunek gry - takze bez szeregowych); resztka ich slownika pochodzenia przy zniszczeniu (PeopleBook.OriginPartyGone)
        // = ludzie, ktorzy mimo reguly poszli w nicosc z ta droga (przy kluczu wylaczonym - caly udzial bitew w "przy zniknieciu partii")
        private static readonly HashSet<PartyBase> _doomedNow = new HashSet<PartyBase>();
        private static int _goneN; private static double _goneMen;
        private static bool _err;

        internal static void Reset()
        {
            ClearDay();
            _diag = 0; _stumblesAll = 0; _err = false; _doomedNow.Clear();
        }

        private static void ClearDay()
        {
            _parties = _men = _routed = _routedNoWin = _noRecv = _freed = _freedTavern = _freedHome = _freedLeft = _undead = _offMen = _offParties = _stumbles = _ctl = 0;
            _goneN = 0; _goneMen = 0.0;
            Array.Clear(_pathParties, 0, Paths); Array.Clear(_pathMen, 0, Paths); Array.Clear(_capt, 0, Paths);
        }

        /// <summary>OutlawLaw.OnMapEventEnded (pierwszy nasluch MapEventEnded Armoury), PRZED RoutedH3. h3 = LosersFlee.On (bez H3 nikt nie dzieli rozbitych -
        /// regula nieczynna, sam pomiar). Nie rzuca.</summary>
        internal static void Before(MapEvent me, bool h3)
        {
            if (me == null) return;
            _doomedNow.Clear();
            try
            {
                bool act = On && h3;
                // recenzja A17 (1): pamiec osad domowych BK (LosersFlee._battleHome, klucz bez miejsca) jest na JEDNA bitwe - bez czyszczenia uwolnieni jency
                // skasowanej partii (FreedHome, SrcFreed) szli do osady BK wybranej dla poprzedniej bitwy (ten sam blad co recenzja fixA (1) przy ucieczkach);
                // RoutedH3Body i tak czysci ja na swoim starcie (po nas)
                if (act) LosersFlee.BeginBattle();
                var bs = me.BattleState;
                bool victory = bs == BattleState.AttackerVictory || bs == BattleState.DefenderVictory;
                foreach (var side in new[] { me.AttackerSide, me.DefenderSide })
                {
                    if (side == null) continue;
                    bool winner = victory && side.MissionSide == me.WinningSide;
                    var parties = new List<MapEventParty>(side.Parties);
                    foreach (var mep in parties)
                    {
                        try { One(me, side, mep, winner, victory, act); }
                        catch (Exception e) { _stumbles++; _stumblesAll++; if (!_err) { _err = true; Log.Error("DoomedPartyMen", e); } }
                    }
                }
            }
            catch (Exception e) { _stumbles++; _stumblesAll++; if (!_err) { _err = true; Log.Error("DoomedPartyMen.Before", e); } }
            finally { try { LosersFlee.A17Forget(me); } catch { } try { A17Fix.Forget(me); } catch { } }
        }

        /// <summary>Warunek gry z MapEventSide.HandleMapEventEndForPartyInternal (1.4.8, :439) - te same pola, w tej samej chwili bitwy (po wyniku, przed
        /// HandleMapEventEnd). IsWinnerSide gry jest internal - ta sama definicja przez BattleState.</summary>
        private static bool GameDestroys(MapEvent me, PartyBase pb, bool winner)
        {
            if (pb == null || !pb.IsMobile || pb == PartyBase.MainParty || !pb.IsActive) return false;
            var mp = pb.MobileParty;
            if (mp == null) return false;
            bool hit = pb.NumberOfAllMembers == 0
                       || (!winner && !me.EndedByRetreat && (pb.NumberOfHealthyMembers == 0 || (me.BattleState != BattleState.None && mp.IsMilitia))
                           && (mp.Army == null || (mp.Army.LeaderParty != null && mp.Army.LeaderParty.Party.NumberOfHealthyMembers == 0)));
            return hit && (!mp.IsDisbanding || pb.MemberRoster.Count == 0);
        }

        private static void Regulars(TroopRoster r, out int men, out int wounded, out int undead)
        {
            men = wounded = undead = 0;
            if (r == null) return;
            for (int i = 0; i < r.Count; i++)
            {
                var e = r.GetElementCopyAtIndex(i);
                if (e.Character == null || e.Character.IsHero || e.Number <= 0) continue;
                if (Undead.Character(e.Character)) { undead += e.Number; continue; }
                men += e.Number; wounded += e.WoundedNumber;
            }
        }

        private static void One(MapEvent me, MapEventSide side, MapEventParty mep, bool winner, bool victory, bool act)
        {
            var pb = mep != null ? mep.Party : null;
            if (!GameDestroys(me, pb, winner)) return;
            _doomedNow.Add(pb);
            var mp = pb.MobileParty;
            int men, wnd, und, pmen, pw, pund;
            Regulars(pb.MemberRoster, out men, out wnd, out und);
            Regulars(pb.PrisonRoster, out pmen, out pw, out pund);
            if (men + pmen + und <= 0) return;
            bool undeadParty = Undead.Party(mp);
            bool left = LosersFlee.A17LeftFor(pb, me);
            int path = !victory ? PB : winner ? POther : me.RetreatingSide != BattleSideEnum.None ? PA : left ? PC : POther;
            int capt = 0, routed = 0, freed = 0; string recvName = "-";
            if (undeadParty) { _undead += men + und; Diag(me, side, pb, path, men, wnd, pmen, winner, act, "Inni - bez zmian (z niczego, do niczego)"); return; }
            _undead += und;
            if (!act)
            {
                _offMen += men; _offParties++;
                Diag(me, side, pb, path, men, wnd, pmen, winner, act, On ? "nieczynna (H3 wylaczone - Losers Flee Enabled)" : "wylaczona (Destroyed Party Men Routed) - w nicosc jak dotad");
                return;
            }
            var pos = me.Position.ToVec2();
            if (men > 0)
            {
                // 1. jency (zwyciestwo drugiej strony)
                MBList<KeyValuePair<MapEventParty, float>> ch = null;
                bool slaveWin = false;
                float vet = 0f, com = 0f;
                var model = Campaign.Current.Models.BattleRewardModel;
                if (victory && !winner)
                {
                    try
                    {
                        var win = me.GetMapEventSide(me.WinningSide);
                        if (win != null)
                        {
                            MBList<KeyValuePair<MapEventParty, float>> wch, hch;
                            model.GetCaptureMemberChancesForWinnerParties(me, win.Parties, out wch, out hch);
                            ch = new MBList<KeyValuePair<MapEventParty, float>>();
                            if (wch != null)
                                foreach (var kv in wch)
                                    if (kv.Key != null && kv.Key.IsNpcParty && kv.Value > 0f && kv.Key.Party != null && kv.Key.Party.IsActive && kv.Key.RosterToReceiveLootPrisoners != null) ch.Add(kv);
                            try { var wf = win.MapFaction; slaveWin = wf != null && LosersFlee.SlaveCulture(wf.Culture); } catch { }
                        }
                    }
                    catch (Exception e) { ch = null; _stumbles++; _stumblesAll++; if (!_err) { _err = true; Log.Error("DoomedPartyMen.Chances", e); } }
                    var s = Settings.Current;
                    vet = MBMath.ClampFloat(s.LoserCaptiveVeteranPercent / 100f, 0f, 1f);
                    com = MBMath.ClampFloat(s.LoserCaptiveCommonPercent / 100f, 0f, 1f);
                    if (ch == null || ch.Count == 0) _noRecv += men;
                }
                bool villager = LosersFlee.KindOf(mp) == LosersFlee.KVillager;
                var roster = pb.MemberRoster;
                var els = new List<TroopRosterElement>();
                for (int i = 0; i < roster.Count; i++) els.Add(roster.GetElementCopyAtIndex(i));
                foreach (var e in els)
                {
                    var c = e.Character;
                    if (c == null || c.IsHero || e.Number <= 0 || Undead.Character(c)) continue;
                    int n = e.Number, w = Math.Min(e.WoundedNumber, n);
                    int k = 0;
                    if (ch != null && ch.Count > 0 && model.CanTroopBeTakenPrisoner(c) && (!villager || slaveWin))
                        k = left ? n : Math.Max(0, Math.Min(n, MBRandom.RoundRandomized(n * (c.Tier >= VeteranTier ? vet : com))));
                    if (k > 0)
                    {
                        var to = Pick(ch);
                        var recv = to != null ? to.RosterToReceiveLootPrisoners : null;
                        if (recv != null)
                        {
                            int kw = Math.Min(k, w);
                            PeopleBook.Shares org = null;
                            try { if (PeopleBook.OriginLive) org = PeopleBook.Take(roster, k, PeopleBook.FCaptured); } catch { org = null; }
                            roster.AddToCounts(c, -k, false, -kw);
                            recv.AddToCounts(c, k, false, kw);
                            if (org != null) { try { PeopleBook.Give(recv, org, PeopleBook.FCaptured); } catch { } }
                            capt += k; n -= k; w -= kw;
                            try { recvName = to.Party != null && to.Party.Name != null ? to.Party.Name.ToString() : "?"; } catch { }
                        }
                    }
                    // 2. reszta - rozbici tej bitwy (podzial H3: N7 / karczma / dom / las; udzialy FRouted bierze RoutedH3Body ze slownika skladu)
                    if (n > 0)
                    {
                        roster.AddToCounts(c, -n, false, -w);
                        mep.RoutedInBattle.AddToCounts(c, n);
                        routed += n;
                    }
                }
                if (routed > 0 && LosersFlee.KindOf(mp) == LosersFlee.KArmy && !StragglersRally.Marked(mp, me)) StragglersRally.MarkBeaten(mp, me, pos);
            }
            // 3. jency szeregowi skasowanej partii (lochu nikt nie lupil) - wolni, droga "uwolnieni bez odbiorcy" H3
            if (pmen > 0)
            {
                var region = OutlawLaw.PoolRegionAt(pos);
                bool law = OutlawLaw.On && region != null;
                var pr = pb.PrisonRoster;
                var els = new List<TroopRosterElement>();
                for (int i = 0; i < pr.Count; i++) els.Add(pr.GetElementCopyAtIndex(i));
                foreach (var e in els)
                {
                    var c = e.Character;
                    if (c == null || c.IsHero || e.Number <= 0 || Undead.Character(c)) continue;
                    int n = e.Number;
                    PeopleBook.Shares org = null;
                    try { if (PeopleBook.OriginLive) org = PeopleBook.Take(pr, n, PeopleBook.FFreed); } catch { org = null; }
                    pr.AddToCounts(c, -n);
                    int vt, got, lf;
                    LosersFlee.FreedHome(c, n, pos, region, law, org, out vt, out got, out lf);
                    freed += n; _freedTavern += vt; _freedHome += got; _freedLeft += lf;
                }
            }
            _parties++; _men += men; _routed += routed; if (!victory) _routedNoWin += routed; _freed += freed;
            _pathParties[path]++; _pathMen[path] += men; _capt[path] += capt;
            _ctl += men - capt - routed;
            Diag(me, side, pb, path, men, wnd, pmen, winner, act, "CZYNNA: do niewoli " + capt + (capt > 0 ? " (" + recvName + ")" : "") + ", rozbici " + routed + ", jency uwolnieni " + freed);
        }

        /// <summary>PeopleBook.OriginPartyGone: resztka slownika (sklad albo loch) partii przy zniknieciu - liczona, gdy to partia skasowana po biezacej bitwie. Nie rzuca.</summary>
        internal static void NoteGone(PartyBase pb, double sum)
        {
            if (pb == null || !(sum > 0.0) || _doomedNow.Count == 0 || !_doomedNow.Contains(pb)) return;
            _goneN++; _goneMen += sum;
        }

        private static MapEventParty Pick(MBList<KeyValuePair<MapEventParty, float>> ch)
        {
            if (ch == null || ch.Count == 0) return null;
            float sum = 0f;
            foreach (var kv in ch) sum += kv.Value;
            float r = MBRandom.RandomFloat * sum;
            foreach (var kv in ch) { r -= kv.Value; if (r <= 0f) return kv.Key; }
            return ch[ch.Count - 1].Key;
        }

        /// <summary>D4 (rozpoznanie A17 rozdz. 3): pierwsze 20 kasowanych partii z ludzmi na sesje - stan bitwy i co zrobila regula. Tylko log.</summary>
        private static void Diag(MapEvent me, MapEventSide side, PartyBase pb, int path, int men, int wnd, int pmen, bool winner, bool act, string rule)
        {
            if (_diag >= DiagMax) return;
            _diag++;
            try
            {
                var mp = pb.MobileParty;
                string nm = "?", id = "?", clan = "-", kind = "?";
                try { nm = pb.Name != null ? pb.Name.ToString() : "-"; } catch { }
                try { id = mp != null ? mp.StringId : "-"; } catch { }
                try { var c = mp != null ? mp.ActualClan : null; clan = c != null && c.Name != null ? c.Name.ToString() : "-"; } catch { }
                try { int k = LosersFlee.KindOf(mp); kind = k == LosersFlee.KArmy ? "wojsko" : k == LosersFlee.KBand ? "banda" : k == LosersFlee.KVillager ? "tabor wsi" : k == LosersFlee.KCaravan ? "karawana" : "zaloga/milicja/patrol"; } catch { }
                int healthy = 0;
                try { healthy = pb.NumberOfHealthyMembers; } catch { }
                var sb = new StringBuilder(600);
                sb.Append("Partia kasowana z ludzmi (A17 diagnoza D4): ").Append(nm).Append(" (").Append(id).Append(", ").Append(kind).Append(", rod ").Append(clan)
                  .Append(") | droga ").Append(PathName[path])
                  .Append(" | BattleState ").Append(me.BattleState).Append(", RetreatingSide ").Append(me.RetreatingSide).Append(", PursuitRoundNumber ").Append(me.PursuitRoundNumber)
                  .Append(", EndedByRetreat ").Append(me.EndedByRetreat ? "tak" : "nie").Append(", DiplomaticallyFinished ").Append(me.DiplomaticallyFinished ? "tak" : "nie")
                  .Append(", typ ").Append(me.EventType).Append(", strona ").Append(side.MissionSide).Append(winner ? " (zwycieska)" : " (nie wygrala)")
                  .Append(", armia ").Append(mp != null && mp.Army != null ? "tak" : "nie")
                  .Append(" | szeregowi ").Append(men).Append(" (ranni ").Append(wnd).Append("), zdrowi w skladzie (z bohaterami) ").Append(healthy).Append(", jency szeregowi ").Append(pmen)
                  .Append(" | H3: ").Append(LosersFlee.A17NoteOf(me))
                  .Append(" | wynik gry (A17 F4): ").Append(A17Fix.CommitNote(me))
                  .Append(" | regula: ").Append(rule).Append(" | linia ").Append(_diag).Append('/').Append(DiagMax).Append('.');
                Log.Info(sb.ToString());
            }
            catch (Exception e) { _stumbles++; _stumblesAll++; if (!_err) { _err = true; Log.Error("DoomedPartyMen.Diag", e); } }
        }

        /// <summary>Linia doby "Ludzie kasowanych partii (A17)" (ArmouryBehavior, po linii H3). Kontrola: szeregowi = do niewoli + rozbici (0).</summary>
        internal static void Daily()
        {
            try
            {
                int day = (int)CampaignTime.Now.ToDays;
                var sb = new StringBuilder(700);
                int capt = _capt[PA] + _capt[PB] + _capt[PC] + _capt[POther];
                sb.Append("Ludzie kasowanych partii (A17): dzien ").Append(day).Append(" | ")
                  .Append(On ? (LosersFlee.On ? "CZYNNE" : "NIECZYNNE (Losers Flee Enabled wylaczone - tylko pomiar)") : "WYLACZONE (Destroyed Party Men Routed - tylko pomiar)")
                  .Append(" | partii ").Append(_parties).Append(", szeregowych ").Append(_men).Append(": do niewoli ").Append(capt)
                  .Append(" (A ").Append(_capt[PA]).Append(", C ").Append(_capt[PC]).Append(", inna ").Append(_capt[POther]).Append(")")
                  .Append(", rozbici ").Append(_routed).Append(" (bez zwyciezcy ").Append(_routedNoWin).Append("; zwyciezca bez lochu ").Append(_noRecv).Append(") - dalej droga rozbitych H3 (linie Przegrani / Wyrzutki / N7)")
                  .Append(" | wedlug drogi: ");
                for (int i = 0; i < Paths; i++) sb.Append(i > 0 ? ", " : "").Append(PathName[i]).Append(' ').Append(_pathParties[i]).Append(" partii / ").Append(_pathMen[i]).Append(" ludzi");
                sb.Append(" | jency skasowanych partii uwolnieni ").Append(_freed).Append(" (karczma ").Append(_freedTavern).Append(", dom ").Append(_freedHome).Append(", bez domu ").Append(_freedLeft).Append(")")
                  .Append(" | Inni bez zmian ").Append(_undead).Append(" | przy regule nieczynnej w nicosc ").Append(_offMen).Append(" ludzi w ").Append(_offParties).Append(" partiach")
                  .Append(" | w nicosc przy zniszczeniu tych partii (resztka slownika pochodzenia, linia Pochodzenie: przy zniknieciu partii) ").Append(_goneMen.ToString("0.#", CultureInfo.InvariantCulture))
                  .Append(" w ").Append(_goneN).Append(" rosterach")
                  .Append(" | kontrola: szeregowi - do niewoli - rozbici = ").Append(_ctl)
                  .Append(" | diagnoza D4 ").Append(_diag).Append('/').Append(DiagMax)
                  .Append(_stumbles > 0 ? " | potkniecia dzis " + _stumbles + " (razem " + _stumblesAll + ")" : "").Append('.');
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Log.Error("DoomedPartyMen.Daily", e); }
            ClearDay();
        }
    }
}
