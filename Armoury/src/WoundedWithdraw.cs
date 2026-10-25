using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// 143 (25.10, fala "AI walczy o zycie", karta "141 Ranny ponizej 20% HP: odwrot z bronia" z syntezy ai-przetrwanie; Jeff 13:30: "przeciwnik zachowuje sie
    /// jak prawdziwy zywy czlowiek, nie chce dac sie zabic ... dopiero ta zasada peka gdy jest panika"; decyzja Jeffa D2 ok. 15:00: ranny ponizej 20% HP =
    /// "Odwrot z bronia", panika z bolu dopiero ponizej 10%). Klucz WoundedWithdrawWithArms (dziala przy BrokenMenNeverRally i WoundedFleeEnabled - maszyna RoutHold
    /// i BrokenMen). ZMIANA REGULY NB2 z 16.10 (wtedy: ranny ponizej WoundedFleePercent -> RoutHold.Break -> Panic: okrzyk Debacle, zrzut broni Artem, zarazanie,
    /// ucieczka do granicy pola): teraz w bitwie polowej ranny cofa sie ROZSADNIE - natywny Agent.Retreat za tyl wlasnego szyku albo do sztandaru, z bronia i tarcza,
    /// potem postoj za linia (ScriptedPosition, bez NoAttack - broni sie, gdy ktos do niego dojdzie). Mechanika w RoutKeeper (RoutHold.Withdraw.cs); ta klasa: stale,
    /// liczniki misji i linia "ODWROT RANNYCH (143)" zaraz po "PANIKA BEZ POWROTU (142)" (RoutHold.Write, pod Gate). Liczniki zeruje RoutHold.Ensure (zmiana misji).
    /// Potkniecia: miejsce "odwrot" linii NB2 (RoutHold.SiteWithdraw) - funkcji nie gasimy; czlowiek z 5 potknieciami wypada z ticku (postoj zdjety).
    /// Nazwa klasy nie moze byc "Log", "Settings" ani "McmSettings" (CrashScribe Net.cs:155).
    /// </summary>
    internal static class WoundedWithdraw
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        // ---- stale (dowody w RoutHold.Withdraw.cs)
        internal const float ArriveRadius = 8f;   // blizej celu - postoj; dalej niz promien zanikania gry przy natywnym odwrocie (sprint ok. 5.3 m)
        internal const float RearExtra = 5f;      // cel: srodek szyku - kierunek x (glebokosc/2 + 5 m) (karta)
        internal const float RearNear = 2f;       // druga proba: 2 m za tylnym szeregiem
        internal const float EdgeMargin = 15f;    // cel co najmniej tyle od granicy pola (zanikanie przy granicy - CommonAIComponent.OnTickParallel)
        internal const float SafeRadius = 10f;    // wrog (nie uciekajacy) blizej celu - cel odrzucony
        internal const float BannerRange = 80f;   // najblizszy sztandar strony w tym promieniu
        internal const float BannerBack = 3f;     // cel: 3 m za chorazym (wzdluz kierunku jego szyku)
        internal const float WalkMax = 30f;       // dluzej w drodze (zablokowany) - postoj tam, gdzie stoi
        internal const float MoveMin = 5f;        // cel przesuniety o wiecej - nowy rozkaz (odwrot albo postoj); mniej - bez wywolan natywnych
        internal const float MoraleFloor = 0.02f; // pod 0.01 gra sama wolalaby Panic (CommonAIComponent.OnTickParallel) - jak RoutHold po zgaszeniu
        internal const float BrokenShare = 0.5f;  // szyk rozbity: co najmniej polowa reszty szyku w PANICE (przelacznik 139)
        internal const int PathPerFrame = 10;     // zapytan o droge na klatke (osobny budzet od RoutHold)

        // powody konca odwrotu
        internal const int WhyNoFormation = 1, WhyAlone = 2, WhyHalf = 3, WhyPain = 4, WhyCompany = 5, WhyGame = 6, WhyComp = 7, WhyOther = 8;
        // rodzaj celu
        internal const int KindRear = 0, KindBanner = 1, KindInPlace = 2;

        private static bool _begun, _here, _key, _rally, _flee;
        // ---- wejscie
        private static readonly HashSet<int> People = new HashSet<int>();
        private static int _mine, _foe, _armIn, _shieldIn;
        internal static readonly HashSet<int> PanickedSkip = new HashSet<int>();   // juz w panice gry (142 przejmie) - bez odwrotu
        internal static readonly HashSet<int> WaitComp = new HashSet<int>();       // czekal na koniec odwrotu komponentu (szarza RBM, rozkaz odwrotu szyku)
        internal static readonly HashSet<int> WaitOrder = new HashSet<int>();      // czekal na koniec natywnego odwrotu z rozkazu szyku
        // ---- droga i postoj
        private static readonly int[] Arrived = new int[3];
        private static float _walkSum;
        private static int _walkN;
        private static int _endWalk, _endHold;
        private static int _diedWalkK, _diedWalkU, _diedHold, _escaped;
        internal static readonly HashSet<int> ReturnedPeople = new HashSet<int>();   // na postoju znow w przedniej polowie szyku (rozni ludzie)
        internal static readonly HashSet<int> FoughtPeople = new HashSet<int>();     // cios wrecz zadany po wejsciu (rozni ludzie)
        private static readonly HashSet<int> ArrivedPeople = new HashSet<int>();
        private static int _restarts;            // ponowne wejscie po cudzym odwrocie (szarza RBM, rozkaz odwrotu szyku)
        // ---- koniec odwrotu
        private static readonly int[] Why = new int[9];
        internal static int Player, BreakRefused, Dropped;
        // ---- bron przy losie albo na koniec
        private static int _armDen, _armed, _shield;
        // ---- rozkazy
        internal static int Sends, Resends, Turns, Moves, Restored, NoSpot, FoeAtSpot, PathQueries, PathLimit, Pins;
        private static int _bannerSeen = -1;     // -1 nie szukano, 0 brak BannerBearerLogic, 1 jest

        /// <summary>Zmiana misji (RoutHold.Ensure, wolajacy trzyma Gate): wszystko od zera.</summary>
        internal static void ResetCounters()
        {
            _begun = _here = _key = _rally = _flee = false;
            People.Clear();
            _mine = _foe = _armIn = _shieldIn = 0;
            PanickedSkip.Clear();
            WaitComp.Clear();
            WaitOrder.Clear();
            Array.Clear(Arrived, 0, Arrived.Length);
            _walkSum = 0f;
            _walkN = 0;
            _endWalk = _endHold = 0;
            _diedWalkK = _diedWalkU = _diedHold = _escaped = 0;
            ReturnedPeople.Clear();
            FoughtPeople.Clear();
            ArrivedPeople.Clear();
            _restarts = 0;
            Array.Clear(Why, 0, Why.Length);
            Player = BreakRefused = Dropped = 0;
            _armDen = _armed = _shield = 0;
            Sends = Resends = Turns = Moves = Restored = NoSpot = FoeAtSpot = PathQueries = PathLimit = Pins = 0;
            _bannerSeen = -1;
        }

        /// <summary>RoutKeeper.AfterStart: bitwa polowa (ten sam zakres co 142) i stan kluczy na te misje.</summary>
        internal static void Begin(Mission m, bool here)
        {
            var c = Settings.Current;
            lock (RoutHold.Gate)
            {
                RoutHold.EnsureLocked(m);
                _begun = true;
                _here = here;
                _key = c != null && c.WoundedWithdrawWithArms;
                _rally = c != null && c.BrokenMenNeverRally;
                _flee = c != null && c.WoundedFleeEnabled;
            }
        }

        /// <summary>Wejscie w odwrot (RoutKeeper.Withdraw): strona i bron przy wejsciu.</summary>
        internal static void Started(Agent a, bool mine, bool weapon, bool shield)
        {
            if (!People.Add(a.Index)) { _restarts++; return; }   // ponowne wejscie po cudzym odwrocie - strona i bron juz liczone
            if (mine) _mine++; else _foe++;
            if (weapon) _armIn++;
            if (shield) _shieldIn++;
        }

        /// <summary>Postoj za linia (pierwszy raz na czlowieka w bitwie): rodzaj celu i czas drogi.</summary>
        internal static void Arrive(int index, int kind, float walk)
        {
            if (!ArrivedPeople.Add(index)) return;
            if (kind < 0 || kind >= Arrived.Length) kind = KindInPlace;
            Arrived[kind]++;
            if (walk < 0f) walk = 0f;
            _walkSum += walk;
            _walkN++;
        }

        /// <summary>Koniec odwrotu inna droga (panika, cudzy odwrot) - powod.</summary>
        internal static void Ended(int why)
        {
            if (why < 0 || why >= Why.Length) why = WhyOther;
            Why[why]++;
        }

        /// <summary>Los rannego w odwrocie (OnAgentRemoved): w drodze / na postoju, stan, bron przy losie.</summary>
        internal static void Fate(bool holding, AgentState state, bool weapon, bool shield)
        {
            if (state == AgentState.Routed) _escaped++;
            else if (state == AgentState.Killed || state == AgentState.Unconscious)
            {
                if (holding) _diedHold++;
                else if (state == AgentState.Killed) _diedWalkK++;
                else _diedWalkU++;
            }
            else return;
            Arms(weapon, shield);
        }

        /// <summary>Koniec misji: kto wciaz w odwrocie (w drodze / na postoju) i jego bron.</summary>
        internal static void AtEnd(bool holding, bool weapon, bool shield)
        {
            if (holding) _endHold++; else _endWalk++;
            Arms(weapon, shield);
        }

        private static void Arms(bool weapon, bool shield)
        {
            _armDen++;
            if (weapon) _armed++;
            if (shield) _shield++;
        }

        internal static void BannerLogic(bool present)
        {
            if (_bannerSeen < 0) _bannerSeen = present ? 1 : 0;
        }

        private static string Pct(int a, int n)
        {
            return n > 0 ? (100.0 * a / n).ToString("0", Inv) + "%" : "-";
        }

        /// <summary>Linia "ODWROT RANNYCH (143)" po linii 142 (RoutHold.Write, wolajacy trzyma Gate). Tylko bitwa polowa; przy kluczu NIE tylko, gdy BrokenMen
        /// zlamal kogos po staremu (porownanie przed / po).</summary>
        internal static void Write(Settings c)
        {
            if (!_begun || !_here) return;
            bool on = _key && _rally && _flee;
            if (!on && RoutHold.Broken == 0) return;
            var sb = new StringBuilder(1500);
            sb.Append("ODWROT RANNYCH (143): misja bitwa, klucz ").Append(_key ? "TAK" : "NIE");
            if (on) sb.Append(" (ranny ponizej ").Append(c != null ? c.WoundedFleePercent.ToString("0", Inv) : "?")
                      .Append("% HP bez Panic: natywny odwrot za tyl szyku albo do sztandaru, bez okrzyku, zrzutu broni i zarazania; postoj za linia; panika dopiero z bolu ponizej ")
                      .Append(c != null ? c.AiFleeBelowPercent.ToString(Inv) : "?").Append("% HP, przy zalamaniu kompanii, panice gry albo gdy szyk rozbity)");
            else if (_key) sb.Append(" (nieczynne: ").Append(!_rally ? "Broken Men Never Rally NIE" : "Wounded Flee NIE").Append(" - stara regula NB2)");
            else sb.Append(" (stara regula NB2 z 16.10: ranny ponizej progu panikuje - Panic, okrzyk, zrzut broni)");
            sb.Append(" | odwrot rannych ").Append(People.Count).Append(" (strona gracza ").Append(_mine).Append(" / wroga ").Append(_foe)
              .Append("; przy wejsciu z bronia ").Append(_armIn).Append(", z tarcza ").Append(_shieldIn)
              .Append("; pominieci: juz w panice gry ").Append(PanickedSkip.Count).Append(", czekali na koniec cudzego odwrotu ").Append(WaitComp.Count)
              .Append(" / rozkazu szyku ").Append(WaitOrder.Count).Append("; ponowne wejscia po cudzym odwrocie ").Append(_restarts).Append(')');
            int arrived = Arrived[0] + Arrived[1] + Arrived[2];
            sb.Append(" | za linia ").Append(arrived).Append(" (cel: tyl szyku ").Append(Arrived[KindRear]).Append(", sztandar ").Append(Arrived[KindBanner])
              .Append(", w miejscu po ").Append(WalkMax.ToString("0", Inv)).Append(" s drogi ").Append(Arrived[KindInPlace])
              .Append("; sr. droga ").Append(_walkN > 0 ? (_walkSum / _walkN).ToString("0.0", Inv) : "-").Append(" s), na koniec: w drodze ").Append(_endWalk)
              .Append(", na postoju ").Append(_endHold)
              .Append(" | zginal w drodze ").Append(_diedWalkK + _diedWalkU).Append(" (zabici ").Append(_diedWalkK).Append(" / nieprzytomni ").Append(_diedWalkU)
              .Append("), padl na postoju ").Append(_diedHold).Append(", uszedl z pola ").Append(_escaped).Append(" (oczekiwane 0)")
              .Append(" | wrocil do walki ").Append(ReturnedPeople.Count).Append(" (na postoju znow w przedniej polowie szyku), bronil sie (cios wrecz po wejsciu) ")
              .Append(FoughtPeople.Count)
              .Append(" | panika: z bolu (<").Append(c != null ? c.AiFleeBelowPercent.ToString(Inv) : "?").Append("%) ").Append(Why[WhyPain])
              .Append(", szyk rozbity ").Append(Why[WhyNoFormation] + Why[WhyAlone] + Why[WhyHalf]).Append(" (bez szyku ").Append(Why[WhyNoFormation])
              .Append(" / sam ").Append(Why[WhyAlone]).Append(" / polowa w panice ").Append(Why[WhyHalf]).Append("), zalamanie kompanii ").Append(Why[WhyCompany])
              .Append(", panika gry ").Append(Why[WhyGame]).Append(", inna ").Append(Why[WhyOther]).Append(", odmowy Break ").Append(BreakRefused)
              .Append("; bez paniki: odwrot komponentu (rozkaz odwrotu szyku / szarza RBM) ").Append(Why[WhyComp]).Append(", przejeci przez gracza ").Append(Player)
              .Append(" | z bronia przy losie albo na koniec ").Append(_armed).Append(" z ").Append(_armDen).Append(" (").Append(Pct(_armed, _armDen))
              .Append("), z tarcza ").Append(_shield)
              .Append(" | rozkazy: odwrot ").Append(Sends).Append(", ponowien (zgasl) ").Append(Resends).Append(", zmian celu ").Append(Turns)
              .Append(", przesuniec postoju ").Append(Moves).Append(", postoj przywrocony ").Append(Restored)
              .Append(", ocen bez bezpiecznego miejsca ").Append(NoSpot).Append(" (wrog przy celu ").Append(FoeAtSpot).Append("), sztandary ")
              .Append(_bannerSeen < 0 ? "nie szukano" : (_bannerSeen > 0 ? "TAK" : "brak BannerBearerLogic"))
              .Append(", zapytan o droge ").Append(PathQueries).Append(" (limit ").Append(PathLimit).Append(')')
              .Append(" | morale do ").Append(MoraleFloor.ToString("0.00", Inv)).Append(' ').Append(Pins).Append(" razy")
              .Append(" | rozbici BrokenMen w NB2 ").Append(RoutHold.Broken).Append(on && c != null && c.RoutedMenNeverRally ? " (oczekiwane 0)" : "")
              .Append(" | potkniecia odwrot ").Append(RoutHold.StumblesLocked(RoutHold.SiteWithdraw)).Append(" (wypadli ").Append(Dropped).Append(").");
            Log.Info(sb.ToString());
        }
    }
}
