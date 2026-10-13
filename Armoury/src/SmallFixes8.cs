using System;
using System.Text;
using TaleWorlds.CampaignSystem;

namespace Armoury
{
    /// <summary>
    /// E8/B (258, PLAN 8.9; LAWA 2, 3, 6, 14; RC8 poz. 20, 22, 30) - KOWADLO I DROBNE: wspolny wlacznik drobnych poprawek etapu 8 (SmallFixes8)
    /// i liczniki calej paczki do jednej linii dnia "Kowadlo i drobne (E8/B)". Sam kod poprawek stoi w swoich plikach:
    ///  - ArmourBotchHalfBack: ArmourQuality.BotchBack (pekniecie zbroi w zakladce CRAFT oddaje czesc wsadu jak luk i kusza);
    ///  - MendTakeByKind: SmithMenu.TakeMendPart (material naprawy wlasnymi rekami schodzi kategoria, jak jest liczony);
    ///  - PlayerMineBulkSkip: WorkshopLaw.PlayerCyclePrefix (kopalnia BK gracza z tym samym dlawikiem ladunku co u notabla);
    ///  - SmallFixes8: MendMaterial.Bench.Pick (liczba sztuk w szacunku nie przekreca int), SmithMenu.SlotQuote + ArmouryBehavior.SlotSmithQuote
    ///    (jedna cena kowala za zalozona sztuke), WorkshopTrade.ClanWagePostfix (ekran rodu: utrzymanie warsztatu z poziomem plac miasta),
    ///    SmithMenu.DoOrderShelf (wpis popytu niezalezny od kiesy), MendMaterial.PlayerLaborToFolk (robocizna napraw gracza do kiesy ludu -
    ///    razem z kluczem N1 MendPayToFolk).
    /// Wylaczony klucz = kod sprzed paczki w swoim miejscu. Tu tylko liczniki (zadnego zlota ani towaru) - czyszczone z ArtisanInputs.Reset
    /// (konstruktor ArmouryBehavior); linia raz na dobe z WorkshopTradeBehavior.
    /// </summary>
    internal static class SmallFixes8
    {
        internal static bool On { get { var s = Settings.Current; return s != null && s.SmallFixes8; } }

        // doba
        private static int _botch, _botchBack, _botchLost, _mendTake, _mendOther, _benchN, _mineHeld, _orderLackN, _orderLack, _onePrice, _clanWage, _estClamp;
        private static long _benchLabor, _benchFolk, _oneBook, _oneMod;
        // od wczytania
        private static int _sBotch, _sBotchBack, _sBotchLost, _sMendTake, _sMendOther, _sBenchN, _sMineHeld, _sOrderLackN, _sOnePrice, _sClanWage, _sEstClamp;
        private static long _sBenchLabor, _sBenchFolk;
        private static int _stumbles;

        internal static void Reset()
        {
            ClearDay();
            _sBotch = _sBotchBack = _sBotchLost = _sMendTake = _sMendOther = _sBenchN = _sMineHeld = _sOrderLackN = _sOnePrice = _sClanWage = _sEstClamp = 0;
            _sBenchLabor = _sBenchFolk = 0;
            _stumbles = 0;
        }

        private static void ClearDay()
        {
            _botch = _botchBack = _botchLost = _mendTake = _mendOther = _benchN = _mineHeld = _orderLackN = _orderLack = _onePrice = _clanWage = _estClamp = 0;
            _benchLabor = _benchFolk = _oneBook = _oneMod = 0;
        }

        /// <summary>Pekniecie zbroi w zakladce CRAFT: back sztuk surowca wrocilo do sakw, lost przepadlo.</summary>
        internal static void NoteBotch(int back, int lost) { _botch++; _botchBack += back; _botchLost += lost; _sBotch++; _sBotchBack += back; _sBotchLost += lost; }

        /// <summary>Material naprawy wlasnymi rekami zdjety kategoria: count sztuk, z tego other nie z dokladnego przedmiotu receptury.</summary>
        internal static void NoteMendTake(int count, int other) { _mendTake += count; _mendOther += other; _sMendTake += count; _sMendOther += other; }

        /// <summary>Zaplata gracza kowalom miasta: robocizna zlecenia i ile z niej przeszlo z kasy do kiesy ludu.</summary>
        internal static void NoteBenchFolk(int labor, int folk) { _benchN++; _benchLabor += labor; _benchFolk += folk; _sBenchN++; _sBenchLabor += labor; _sBenchFolk += folk; }

        /// <summary>Cykl kopalni BK gracza wstrzymany dlawikiem ladunku.</summary>
        internal static void NotePlayerMine() { _mineHeld++; _sMineHeld++; }

        /// <summary>Zamowienie z polki przerwane przez kiese, a polce i tak brakowalo pieces sztuk - wpis popytu poszedl.</summary>
        internal static void NoteOrderLack(int pieces) { _orderLackN++; _orderLack += pieces; _sOrderLackN++; }

        /// <summary>Zalozona sztuka naprawiona u kowala z "Pick a piece" po wycenie z ksiegi zuzycia: robocizna czynna (book) i stara (mod).</summary>
        internal static void NoteOnePrice(int book, int mod) { _onePrice++; _sOnePrice++; _oneBook += book; _oneMod += mod; }

        /// <summary>Ekran rodu: pozycja "Daily Wage" warsztatu podmieniona na utrzymanie z poziomem plac miasta.</summary>
        internal static void NoteClanWage() { _clanWage++; _sClanWage++; }

        /// <summary>Szacunek materialu naprawy: liczba sztuk ponad zakres int przycieta (dotad przekrecala sie na ujemna).</summary>
        internal static void NoteEstimateClamp() { _estClamp++; _sEstClamp++; }

        private static string K(bool on) { return on ? "[WL]" : "[wyl]"; }

        /// <summary>Linia dnia (sam log).</summary>
        internal static void Daily()
        {
            try
            {
                var s = Settings.Current;
                if (s == null || Campaign.Current == null) { ClearDay(); return; }
                int day = (int)CampaignTime.Now.ToDays - 1;
                var sb = new StringBuilder();
                sb.Append("Kowadlo i drobne (E8/B): dzien ").Append(day)
                  .Append(" | ArmourBotchHalfBack ").Append(K(ArmourQuality.BotchBackOn)).Append(", MendTakeByKind ").Append(K(s.MendTakeByKind))
                  .Append(", PlayerMineBulkSkip ").Append(K(s.PlayerMineBulkSkip)).Append(", SmallFixes8 ").Append(K(s.SmallFixes8))
                  .Append(", robocizna gracza do ludu ").Append(K(s.SmallFixes8 && s.MendPayToFolk))
                  .Append(" | pekniecia zbroi w CRAFT ").Append(_botch).Append(" (zwrot do sakw ").Append(_botchBack).Append(" szt., przepadlo ").Append(_botchLost).Append(" szt.)")
                  .Append(" | material napraw wlasnymi rekami zdjety kategoria ").Append(_mendTake).Append(" szt., w tym z odmian (nie dokladny przedmiot) ").Append(_mendOther)
                  .Append(" | zaplaty gracza kowalom miasta ").Append(_benchN).Append(": robocizna ").Append(_benchLabor).Append(" zl, z kasy do kiesy ludu ").Append(_benchFolk).Append(" zl")
                  .Append(" | kopalnia gracza: cykle wstrzymane ").Append(_mineHeld)
                  .Append(" | zamowienia z polki przy pustej kiesie: wpisow popytu ").Append(_orderLackN).Append(" (sztuk ").Append(_orderLack).Append(")")
                  .Append(" | zalozona sztuka u kowala po cenie z ksiegi ").Append(_onePrice).Append(" razy (robocizna ").Append(_oneBook).Append(" zl, z modyfikatora byloby ").Append(_oneMod).Append(" zl)")
                  .Append(" | ekran rodu: utrzymanie warsztatu poprawione ").Append(_clanWage).Append(" razy")
                  .Append(" | szacunek materialu: przyciecia liczby sztuk ").Append(_estClamp)
                  .Append(" | od wczytania: pekniecia ").Append(_sBotch).Append(" (zwrot ").Append(_sBotchBack).Append(", przepadlo ").Append(_sBotchLost).Append(")")
                  .Append(", material kategoria ").Append(_sMendTake).Append(" (z odmian ").Append(_sMendOther).Append(")")
                  .Append(", zaplaty kowalom ").Append(_sBenchN).Append(" (robocizna ").Append(_sBenchLabor).Append(", do ludu ").Append(_sBenchFolk).Append(")")
                  .Append(", kopalnia ").Append(_sMineHeld).Append(", wpisy popytu ").Append(_sOrderLackN).Append(", wyceny z ksiegi ").Append(_sOnePrice)
                  .Append(", ekran rodu ").Append(_sClanWage).Append(", przyciecia ").Append(_sEstClamp)
                  .Append(" | potkniecia ").Append(_stumbles).Append('.');
                Log.Info(sb.ToString());
            }
            catch (Exception e) { _stumbles++; if (_stumbles <= 3) Log.Error("SmallFixes8.Daily", e); }
            ClearDay();
        }
    }
}
