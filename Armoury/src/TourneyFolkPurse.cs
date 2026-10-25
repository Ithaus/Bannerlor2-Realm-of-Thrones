using System;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>
    /// f8-sakiewka (noc 17/18.10; decyzja Jeffa 18.10 "4 tak" - sakiewka turniejowa od mieszczan; projekt docs/kiesa-ludu/PROJEKT-WYDATKI-2026-10-13.md P5,
    /// rozdz. 5 wiersz "Sakiewka turniejowa (P5)", rozdz. 7-8). Klucz TourneyFolkPurse (+ TourneyFolkPurseDays, TourneyFolkPurseMin).
    ///  - KIEDY: sluchacz CampaignEvents.TournamentFinished - gra wola go po GivePrizeToWinner w obu drogach: turniej AI (TournamentManager.ResolveTournament,
    ///    tick dobowy osady) i turniej z graczem (TournamentBehavior.EndCurrentMatch, w misji). Sprawdzone w dekompilacji: RBMTournament lata tylko
    ///    GivePrizeToWinner (prefiks), Tournaments XPanded nie lata ani ResolveTournament, ani EndCurrentMatch, BK BKTournamentBehavior i GrandTourney sluchaja
    ///    tego samego zdarzenia (BK - renoma, GrandTourney - pula gracza i utarg gospodarza: inni platnicy, nic wspolnego z kiesa ludu). Zdarzenie biegnie
    ///    POZA GivePrizeToWinner - poza kontekstem RealPayer (KTourney) i oknem ksiegi O11, wiec przelew nie jest brany za nagrode "z niczego".
    ///  - ILE i SKAD: TownFolk.TourneyPurseTake - jedna dzienna oferta ludu miasta-gospodarza (oferta nadwyzki W/R/P z ostatniej doby ludu x dni), z kies klas
    ///    ponad zapas dni i ponad oferte na jutro; miasto bez nadwyzki - nic; mniej niz TourneyFolkPurseMin - nic (moneta zostaje u ludu).
    ///  - KOMU: jak cena nagrody w grze (GivePrizeToWinner) - glowa rodu zwyciezcy (winner.Clan.Leader); gracz, gdy wygral on albo ktos z jego druzyny
    ///    (gra daje wtedy przedmiot do jukow gracza), takze bohater rodu gracza (glowa rodu = gracz). Zwyciezca nie bohater albo bez rodu - nic (gra tez nic).
    ///  - PRZELEW: kiesy ludu -> kiesa odbiorcy (ChangeHeroGold - jak PayLord i W7: posiadacze ksiegi pieniadza "kiesy ludu miast" i "bohaterowie",
    ///    zdarzenie gry nie powstaje, wiec ksiega nie liczy go jako zlota z niczego; okno 169b przy glowie poza swiatem - CirculationWindows.NoteHeroGold).
    ///    Poza D rodow (jak W7: jednorazowa wygrana, nie staly dochod). Wyjatek przy przelewie - zloto wraca do kies (TourneyPurseUndo).
    ///  - GDZIE: tylko przy zastosowanym KS (StartCalibration.Applied - regula nocy 17/18.10: paczki ekonomii dzialaja tylko pod KS) i czynnej kiesie ludu.
    ///  - GRACZ: komunikat po angielsku, gdy sakiewka jest jego albo gdy jest w miescie / bral udzial w turnieju.
    /// Tozsamosc (linia doby, prog 0): zdjete z kies ludu (roznica kies) = przelane (roznica kies odbiorcow) + zwrocone po wyjatku.
    /// Stan: tylko liczniki doby i sesji (pamiec; Reset z TourneyPay.Reset - konstruktor ArmouryBehavior). Nic w zapisie. Linie "Sakiewka turniejowa (f8):".
    /// Wyjatek - licznik potkniec, nic nie gasimy (CLAUDE.md 7).
    /// </summary>
    internal static class TourneyFolkPurse
    {
        private const string P = "Sakiewka turniejowa (f8): ";
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private const float DaysMax = 4f;

        internal static bool KeyOn { get { var s = Settings.Current; return s != null && s.TourneyFolkPurse; } }

        // liczniki doby
        private static int _n, _noTown, _noKs, _noFolkLive, _noHero, _noRec, _noFolk, _stale, _noOffer, _noRoom, _belowMin, _paidN, _headsN, _playerN, _undoN, _belowRes;
        private static long _taken, _toHeads, _toPlayer, _undo, _w, _r, _p, _offerSum, _maxPurse, _ticks;
        private static string _maxTown = "-";
        private static int _stumbles, _stumblesDay;

        // recenzja f8: migawka doby dla linii "Obieg" (rody dostaly) - Daily biegnie przed MoneyLedger.Daily i czysci liczniki doby (jak W7 LastLordLevy)
        internal static long LastToHeads, LastToPlayer;
        internal static int LastHeadsN, LastPlayerN;

        private static void Stumble(string where, Exception e)
        {
            _stumbles++; _stumblesDay++;
            try { Log.Error("TourneyFolkPurse." + where, e); } catch { }
        }

        private static void ClearDay()
        {
            _n = _noTown = _noKs = _noFolkLive = _noHero = _noRec = _noFolk = _stale = _noOffer = _noRoom = _belowMin = _paidN = _headsN = _playerN = _undoN = _belowRes = 0;
            _taken = _toHeads = _toPlayer = _undo = _w = _r = _p = _offerSum = _maxPurse = _ticks = 0;
            _maxTown = "-"; _stumblesDay = 0;
        }

        /// <summary>Nowa kampania i kazde wczytanie (TourneyPay.Reset - PlayerFees8.Reset - konstruktor ArmouryBehavior).</summary>
        internal static void Reset() { _stumbles = 0; ClearDay(); LastToHeads = LastToPlayer = 0; LastHeadsN = LastPlayerN = 0; }

        /// <summary>
        /// Wpis linii "Obieg" (rody dostaly [P]) - migawka doby z Daily (MoneyLedger.Daily biegnie po TourneyFolkPurse.Daily tej samej doby).
        /// Klucz wylaczony i zero przelewow - pusty napis (linia "Obieg" jak przed paczka).
        /// </summary>
        internal static string ObiegNote()
        {
            long all = LastToHeads + LastToPlayer;
            if (!KeyOn && all == 0) return "";
            return ", sakiewka turniejowa ludu (f8) kiesy ludu -> glowy rodow i gracz " + all + " (glowy rodow " + LastToHeads + " w " + LastHeadsN
                   + " sakiewkach, gracz " + LastToPlayer + " w " + LastPlayerN + ")";
        }

        private static string NameOf(Hero h) { return h != null && h.Name != null ? h.Name.ToString() : "?"; }

        /// <summary>CampaignEvents.TournamentFinished (ArmouryBehavior.RegisterEvents). Klucz wylaczony - nic.</summary>
        public static void OnTournamentFinished(CharacterObject winner, MBReadOnlyList<CharacterObject> participants, Town town, ItemObject prize)
        {
            if (!KeyOn) return;
            long t0 = Stopwatch.GetTimestamp();
            try { Run(winner, participants, town); }
            catch (Exception e) { Stumble("OnTournamentFinished", e); }
            finally { _ticks += Stopwatch.GetTimestamp() - t0; }
        }

        private static void Run(CharacterObject winner, MBReadOnlyList<CharacterObject> participants, Town town)
        {
            _n++;
            var st = town != null ? town.Settlement : null;
            if (st == null) { _noTown++; return; }
            if (!StartCalibration.Applied) { _noKs++; return; }          // regula nocy: paczki ekonomii tylko pod KS
            if (!TownFolk.Live) { _noFolkLive++; return; }
            var h = winner != null && winner.IsHero ? winner.HeroObject : null;
            if (h == null) { _noHero++; return; }                        // gra: zwyciezca nie bohater - bez nagrody
            var me = Hero.MainHero;
            var mp = MobileParty.MainParty;
            bool mine = me != null && (ReferenceEquals(h, me) || (mp != null && ReferenceEquals(h.PartyBelongedTo, mp)));
            Hero rec = mine ? me : (h.Clan != null ? h.Clan.Leader : null);   // jak cena nagrody w grze (GivePrizeToWinner): glowa rodu zwyciezcy; druzyna gracza - gracz
            if (rec == null || !rec.IsAlive) { _noRec++; return; }
            var s = Settings.Current;
            double days = s != null ? s.TourneyFolkPurseDays : 1.0;
            if (double.IsNaN(days) || days < 0) days = 0; else if (days > DaysMax) days = DaysMax;
            long min = s != null ? Math.Max(0, s.TourneyFolkPurseMin) : 20;
            long cap = Math.Max(0L, (long)int.MaxValue - rec.Gold);
            var cls = new long[3];
            int why, below; long offer;
            long x = TownFolk.TourneyPurseTake(st, days, min, cap, cls, out why, out offer, out below);
            if (x <= 0)
            {
                if (why == TownFolk.TpStale) _stale++;
                else if (why == TownFolk.TpNoOffer) _noOffer++;
                else if (why == TownFolk.TpNoRoom) _noRoom++;
                else if (why == TownFolk.TpBelowMin) _belowMin++;
                else _noFolk++;
                return;
            }
            _taken += x; _w += cls[0]; _r += cls[1]; _p += cls[2]; _offerSum += offer; _belowRes += below;   // zdjete z kies (W + R + P = zdjete, takze gdy przelew wroci po wyjatku)
            long g0 = rec.Gold;
            try { rec.ChangeHeroGold((int)Math.Min(int.MaxValue, x)); }
            catch (Exception e)
            {
                long back = 0;
                try { long got0 = rec.Gold - g0; if (got0 > 0) rec.ChangeHeroGold(-(int)got0); } catch { }
                back = TownFolk.TourneyPurseUndo(st);                       // zloto wraca do kies ludu (nic w nicosc)
                _undo += back; _undoN++;
                Stumble("Run(przelew)", e);
                return;
            }
            long got = rec.Gold - g0;
            try { CirculationWindows.NoteHeroGold(rec, got); } catch (Exception e) { Stumble("Run(NoteHeroGold)", e); }   // paczka 169b: glowa poza swiatem - zloto wyszlo ze swiata (licznik)
            _paidN++;
            if (ReferenceEquals(rec, me)) { _toPlayer += got; _playerN++; } else { _toHeads += got; _headsN++; }
            if (ReferenceEquals(rec, me)) SpoilsReport.NoteTourney(SpoilsReport.LTFolk, got);   // noc P11 (125): okno lupu turnieju - ta sama kwota (tylko odczyt, bez ramki - nic)
            string tn = town.Name != null ? town.Name.ToString() : "?";
            if (x > _maxPurse) { _maxPurse = x; _maxTown = tn; }
            try
            {
                Log.Info(P + tn + ": lud W " + cls[0] + " / R " + cls[1] + " / P " + cls[2] + " = " + x + " -> " + NameOf(rec) + (ReferenceEquals(rec, me) ? " (gracz)" : " (glowa rodu)")
                         + " (zwyciezca " + NameOf(h) + "; oferta dnia ludu " + offer + " x dni " + days.ToString("0.00", Inv) + ", minimum " + min + "); przelane " + got
                         + ", roznica " + (x - got) + (below > 0 ? "; UWAGA klasy ponizej zapasu po sakiewce " + below : "") + ".");
            }
            catch (Exception e) { Stumble("Run(log)", e); }
            Tell(participants, town, tn, h, rec, x);
        }

        /// <summary>Komunikat gracza (po angielsku): sakiewka jego albo gracz w miescie / w turnieju.</summary>
        private static void Tell(MBReadOnlyList<CharacterObject> participants, Town town, string tn, Hero h, Hero rec, long x)
        {
            try
            {
                var me = Hero.MainHero;
                if (me == null) return;
                bool yours = ReferenceEquals(rec, me);
                bool here = yours;
                if (!here)
                {
                    var cur = Settlement.CurrentSettlement;
                    var mp = MobileParty.MainParty;
                    here = (cur != null && ReferenceEquals(cur, town.Settlement)) || (mp != null && ReferenceEquals(mp.CurrentSettlement, town.Settlement));
                }
                if (!here && participants != null)
                {
                    var mc = me.CharacterObject;
                    foreach (var c in participants) if (c != null && ReferenceEquals(c, mc)) { here = true; break; }
                }
                if (!here) return;
                var sb = new StringBuilder(200);
                sb.Append("The townsfolk of ").Append(tn).Append(" put up a purse of ").Append(x.ToString("N0", Inv)).Append(" coins for the champion.");
                if (yours) sb.Append(" The purse is yours.");
                else if (ReferenceEquals(rec, h)) sb.Append(" It goes to ").Append(NameOf(h)).Append('.');
                else sb.Append(" It goes to ").Append(NameOf(rec)).Append(", head of ").Append(NameOf(h)).Append("'s house.");
                Log.Player(sb.ToString());
            }
            catch (Exception e) { Stumble("Tell", e); }
        }

        /// <summary>Raz na dobe (ArmouryBehavior, po TourneyPay.Daily): linia doby. Klucz wylaczony - bez linii (jak przed paczka).</summary>
        internal static void Daily(int day)
        {
            try
            {
                // migawka przed sprawdzeniem klucza: przelewy tej doby sie odbyly, nawet gdy klucz wylaczono w trakcie doby (przy kluczu wylaczonym od rana - 0)
                LastToHeads = _toHeads; LastToPlayer = _toPlayer; LastHeadsN = _headsN; LastPlayerN = _playerN;
                if (!KeyOn) return;
                var s = Settings.Current;
                double days = s != null ? s.TourneyFolkPurseDays : 1.0;
                if (double.IsNaN(days) || days < 0) days = 0; else if (days > DaysMax) days = DaysMax;
                long min = s != null ? Math.Max(0, s.TourneyFolkPurseMin) : 20;
                long diff = _taken - (_toHeads + _toPlayer) - _undo;
                int none = _noTown + _noKs + _noFolkLive + _noHero + _noRec + _noFolk + _stale + _noOffer + _noRoom + _belowMin;
                var sb = new StringBuilder(900);
                sb.Append(P).Append("dzien ").Append(day)
                  .Append(" | klucz Tourney Folk Purse tak, dni oferty ").Append(days.ToString("0.00", Inv)).Append(", minimum ").Append(min)
                  .Append(" | KS ").Append(StartCalibration.Applied ? "zastosowany" : "NIEZASTOSOWANY").Append(", kiesa ludu ").Append(TownFolk.Live ? "czynna" : "NIECZYNNA")
                  .Append(" | turnieje ").Append(_n).Append(": sakiewki ").Append(_paidN).Append(" na sume ").Append(_taken)
                  .Append(" (lud W ").Append(_w).Append(", R ").Append(_r).Append(", P ").Append(_p).Append(") -> glowy rodow ").Append(_toHeads).Append(" (").Append(_headsN)
                  .Append("), gracz ").Append(_toPlayer).Append(" (").Append(_playerN).Append("); oferta dnia ludu tych miast ").Append(_offerSum)
                  .Append("; najwieksza ").Append(_maxPurse).Append(" (").Append(_maxTown).Append(')')
                  .Append(" | bez sakiewki ").Append(none).Append(": bez miasta ").Append(_noTown).Append(", KS niezastosowany ").Append(_noKs).Append(", kiesa nieczynna ").Append(_noFolkLive)
                  .Append(", zwyciezca nie bohater ").Append(_noHero).Append(", bez odbiorcy ").Append(_noRec).Append(", miasto bez kiesy ").Append(_noFolk)
                  .Append(", bez swiezej doby ludu ").Append(_stale).Append(", bez nadwyzki ").Append(_noOffer).Append(", nadwyzka zajeta ").Append(_noRoom)
                  .Append(", ponizej minimum ").Append(_belowMin)
                  .Append(" | tozsamosc (prog 0): zdjete z kies ").Append(_taken).Append(" - przelane ").Append(_toHeads + _toPlayer).Append(" - zwrocone po wyjatku ").Append(_undo)
                  .Append(" (").Append(_undoN).Append(") = ").Append(diff)
                  .Append(" | kontrola (prog 0): klasy ponizej zapasu po sakiewce ").Append(_belowRes)
                  .Append(" | koszt ").Append((_ticks * 1000.0 / Stopwatch.Frequency).ToString("0.000", Inv)).Append(" ms/dobe")
                  .Append(" | potkniecia dzis ").Append(_stumblesDay).Append(" (od wczytania ").Append(_stumbles).Append(").");
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Stumble("Daily", e); }
            finally { ClearDay(); }
        }
    }
}
