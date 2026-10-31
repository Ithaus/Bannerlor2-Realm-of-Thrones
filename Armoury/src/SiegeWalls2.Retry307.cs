using System;
using TaleWorlds.CampaignSystem.Siege;

namespace Armoury
{
    /// <summary>
    /// 307c - SZTURMY AI BEZ SAMOBOJSTWA (Jeff 31.10: "AI walczy o zycie"; krytyk 307 'reszta' waga 4: Vaes Dothrak odparty 4 razy w 142b, "s gry" 2.8e18).
    ///
    /// Przyczyna [K gra + P log 142b :43920]: wykladnik murow w decyzji gry (BesiegerCamp.StartingAssaultOnBesiegedSettlementIsLogical :408-417, dekompilacja 1.4.8)
    ///   w = (0.8 - 0.02 x (godziny oblezenia - 96)) x 1.25 bez taranu x 1.25 bez wiezy - po ok. 40 h ponad 4 doby jest juz ujemny, po 50 dobach -21.554;
    ///   przewaga murow (6.688) w ujemnej potedze "pomaga" oblegajacym: s gry = 1288 / (280 x 6.688^-21.554) = 2.8e18. Gra mnozy to przez kare machin
    ///   w budowie (dla trebusza drugiej ekipy TB kara <= 0 - gra nie szturmuje), ale MB (SiegeWalls2.Unblock) liczy szanse 0.1 x s gry x kara samych taranow
    ///   i wiez - przy 2.8e18 kazda dodatnia kara daje 1.0 co godzine. OB1 (Veto) losuje POurs / PGame tylko przy PGame > 0, a tu PGame = 0 (kara gry ujemna),
    ///   wiec "szansa na mysl 0.0%" OB1 w linii O0 nie hamowala niczego. Przerwa OB2 (7 x morale x ludzie x oplacalnosc) wychodzi ok. 3 dob za kazdym razem.
    ///   Liczby z OB1 (s OB1 1.2-1.9, w = 0.8 x HP bez czlonu czasu) mowily, ze mury trzymaja slabo - AI szlo, tracilo ok. 20% i wracalo po 3 dobach.
    /// Zmiana (klucze 307c):
    ///   (1) SiegeSaneAssaultOdds: MB liczy szanse z s = sila / (obroncy x przewaga^max(w gry, SiegeGameTimeTermFloor 0)) - mury nigdy nie licza sie na korzysc
    ///       oblegajacych; s gry (bez zmian) zostaje w warunku "gra: s > 1" i w liniach logu (jak gra). Linia O0 "stosunek gry" to tylko odczyt (decyzje okupu
    ///       licza "mury" bez czlonu czasu - SiegeRansom.Evaluate), wiec nie ma tam czego ograniczac.
    ///   (2) SiegeRepelLearn: przerwa OB2 x (1 + SiegeRepelPauseGrowth x wczesniejsze odparcia w tym oblezeniu), dalej najwyzej 4 x podstawa (OB-2b);
    ///       po SiegeRepelRetryAfter (2) odparciach nastepny szturm (decyzja gry przez Veto i MB) tylko przy wyrwie albo sile >= SiegeRepelRetryMinRatio (6)
    ///       x obroncy w pelni - "zmiana warunkow"; najdluzej 3 x SiegeRepelPauseDays po koncu przerwy (potem proba jak dotad - oblezenie nie zamarza,
    ///       a okup O0, ktory liczy sie dopiero po Veto, wraca). Jedna regula dla kazdego obozu AI (gracz decyduje sam - Veto i Unblock go pomijaja).
    /// OB-2c (1 przypadek 142b, town_K3 O0 w dobie 108936 przy "przerwie do 108937.0"): linia O0 powstaje tylko po Veto = false, a Veto wstrzymuje przy Now < Until,
    ///   wiec Until <= chwila O0 < 108937 - przerwa konczyla sie w ostatniej godzinie doby 108936 (108933.7 + 3.27), a "108937.0" to zaokraglenie do 0.1 w linii.
    ///   Linia OB2 pisze teraz "do dnia" z dwoma miejscami, sprawdz_logi OB-2c liczy z tolerancja zaokraglenia.
    /// Tylko pamiec (liczba odparc i przerwa - w istniejacym zapisie arm_siege_walls). Wyjatek = potkniecie (false - zachowanie bez 307c).
    /// </summary>
    internal static partial class SiegeWalls2
    {
        internal static bool SaneOn { get { var s = Settings.Current; return s != null && s.SiegeSaneAssaultOdds; } }
        private static double TimeFloor { get { var s = Settings.Current; return s == null || float.IsNaN(s.SiegeGameTimeTermFloor) ? 0.0 : Clamp(s.SiegeGameTimeTermFloor, 0.0, 0.8); } }
        internal static bool LearnOn { get { var s = Settings.Current; return s != null && s.SiegeRepelLearn; } }
        internal static double RepelGrowth { get { var s = Settings.Current; return s == null || float.IsNaN(s.SiegeRepelPauseGrowth) ? 0.5 : Clamp(s.SiegeRepelPauseGrowth, 0.0, 2.0); } }
        private static int RetryAfter { get { var s = Settings.Current; return s == null ? 2 : (int)Clamp(s.SiegeRepelRetryAfter, 1, 10); } }
        // recenzja 307c: po odparciach prog nie nizszy niz DR (SiegeLadderMinRatio) - jedna miara miazdzacej przewagi, po odparciach co najmniej tak ostra
        private static double RetryRatio { get { var s = Settings.Current; double r = s == null || float.IsNaN(s.SiegeRepelRetryMinRatio) ? 6.0 : Clamp(s.SiegeRepelRetryMinRatio, 2.0, 20.0); return Math.Max(r, LadderRatio); } }
        // recenzja 307c (1): MB - Veto losuje szanse OB1 / szanse MB (gra ma tu PGame = 0, wiec dotad zostawal sam warunek s OB1 > 1)
        internal static bool MbRollOn { get { var s = Settings.Current; return s != null && s.SiegeMbSameRoll; } }
        // recenzja 307c (2): wstrzymanie po odparciach dotyczy samego szturmu - okup / kapitulacja (O0) liczy sie dalej
        internal static bool RetryRansomOn { get { var s = Settings.Current; return s != null && s.SiegeRepelHoldRansom && LearnOn; } }
        private static BesiegerCamp _mbCamp; private static float _mbAt = -1f; private static double _mbPt, _mbPo;
        private static int _dMbRolls, _dMbVeto, _dRetryRansom, _dRetryRansomDeal;

        /// <summary>Unblock (MB) przepuscil decyzje: szansa MB (pt) i OB1 tej samej drogi (po) - dla Veto w tej samej chwili.</summary>
        private static void NoteMb(BesiegerCamp camp, double pt, double po) { _mbCamp = camp; _mbAt = Now; _mbPt = pt; _mbPo = po; }

        /// <summary>Veto: true = ta decyzja przyszla z MB w tej chwili (klucz SiegeMbSameRoll) - pg = szansa MB, po = szansa OB1 tej drogi.</summary>
        internal static bool MbOdds(BesiegerCamp camp, out double pg, out double po)
        {
            pg = po = 0.0;
            if (!MbRollOn || camp == null || _mbCamp != camp || Math.Abs(Now - _mbAt) >= 0.01f) return false;
            pg = _mbPt; po = _mbPo; _mbCamp = null;
            _dMbRolls++;
            return true;
        }
        internal static void NoteMbVeto() { _dMbVeto++; }
        internal static void NoteRetryRansom(bool deal) { _dRetryRansom++; if (deal) _dRetryRansomDeal++; }

        private static int _dSaneRolls, _dSaneCut, _dRetryHeld, _dRetryGap, _dRetryEdge, _dRetryLate, _dPauseGrown;
        private static double _dSaneMax, _dGameMax, _dGrowSum;

        /// <summary>(1) s do szansy MB: czlon czasu gry nie schodzi ponizej TimeFloor. Klucz wylaczony / pusta warownia - s gry.</summary>
        internal static double SaneS(SiegeWalls.Meas m)
        {
            if (m == null || m.Empty || !SaneOn || m.Def <= 0) return m != null ? m.SGame : 0.0;
            double w = Math.Max(m.WGame, TimeFloor);
            double s = m.Att / (m.Def * Math.Pow(Math.Max(1e-6, m.Adv), w));
            if (double.IsNaN(s) || double.IsInfinity(s)) return m.SGame;
            return Math.Min(m.SGame, s);
        }

        /// <summary>MB: zapis odczytu do linii doby (ile losowan, ile przycietych, najwieksze s przed i po).</summary>
        private static void NoteSane(double sGame, double sUsed)
        {
            _dSaneRolls++;
            if (sUsed < sGame) _dSaneCut++;
            if (sGame > _dGameMax) _dGameMax = sGame;
            if (sUsed > _dSaneMax) _dSaneMax = sUsed;
        }

        /// <summary>(2) true = po RetryAfter odparciach w tym oblezeniu szturm wstrzymany: bez wyrwy i sila &lt; RetryRatio x obroncy w pelni. why - powod do linii OB1.</summary>
        internal static bool RetryHeld(BesiegerCamp camp, SiegeWalls.Meas m, int repels, double holdUntil, bool count, out string why)
        {
            why = null;
            try
            {
                if (!LearnOn || camp == null || m == null || m.Empty || repels < RetryAfter) return false;
                if (Now >= holdUntil) { if (count) _dRetryLate++; return false; }   // po dlugim czekaniu (glod, straty obroncow) wodz probuje znowu - bez zamrozenia oblezenia
                double def = Math.Max(m.Def, m.Def0);
                double edge = def > 0 ? m.Att / def : 999.0;
                if (m.Gap) { if (count) _dRetryGap++; return false; }
                if (edge >= RetryRatio) { if (count) _dRetryEdge++; return false; }
                if (count) _dRetryHeld++;
                why = "po " + repels + " odparciach te same mury (307c): bez wyrwy, sila " + F(m.Att, "0") + " / obroncy " + F(def, "0") + " = " + F(edge, "0.00")
                    + " < " + F(RetryRatio, "0.0") + ", najdluzej do dnia " + F(holdUntil, "0.0");
                return true;
            }
            catch (Exception e) { Stumble("RetryHeld", e); return false; }
        }

        /// <summary>(2) przerwa po odparciu: T x (1 + wzrost x wczesniejsze odparcia), najwyzej cap. prior - odparcia przed tym.</summary>
        internal static double GrowPause(double t, int prior, double cap, out double factor)
        {
            factor = 1.0;
            if (!LearnOn || prior <= 0 || t <= 0) return t;
            double f = 1.0 + RepelGrowth * prior;
            double nt = Math.Min(t * f, Math.Max(t, cap));
            factor = t > 0 ? nt / t : 1.0;
            if (nt > t) { _dPauseGrown++; _dGrowSum += factor; }
            return nt;
        }

        /// <summary>Fragment linii "Mury oblezen (OB0)" (307c) - potem zerowanie licznikow doby.</summary>
        internal static string Day307Text()
        {
            string t = "307c: klucze szansa MB bez ujemnych murow " + (SaneOn ? "tak (w >= " + F(TimeFloor, "0.00") + ")" : "nie")
                     + ", nauka z odparc " + (LearnOn ? "tak (przerwa +" + F(RepelGrowth, "0.00") + " na odparcie, po " + RetryAfter + " odparciach wyrwa albo >= " + F(RetryRatio, "0.0") + ":1)" : "nie")
                     + " | MB losowan " + _dSaneRolls + ", s przyciete " + _dSaneCut + " (s gry max " + (_dGameMax > 1e12 ? _dGameMax.ToString("0.###E+0", Inv) : F(_dGameMax, "0.00"))
                     + ", uzyte max " + F(_dSaneMax, "0.00") + ") | ponowny szturm: wstrzymane " + _dRetryHeld + ", przez wyrwe " + _dRetryGap + ", przy przewadze " + _dRetryEdge + ", po czekaniu " + _dRetryLate
                     + " | przerwy wydluzone " + _dPauseGrown + (_dPauseGrown > 0 ? " (sr. x" + F(_dGrowSum / _dPauseGrown, "0.00") + ")" : "")
                     + " | recenzja: MB losowanie OB1 " + (MbRollOn ? "tak" : "nie") + " (losowan " + _dMbRolls + ", wstrzymane " + _dMbVeto + ")"
                     + ", okup przy wstrzymaniu " + (RetryRansomOn ? "tak" : "nie") + " (ocen " + _dRetryRansom + ", umow " + _dRetryRansomDeal + ")";
            _dSaneRolls = _dSaneCut = _dRetryHeld = _dRetryGap = _dRetryEdge = _dRetryLate = _dPauseGrown = 0; _dSaneMax = _dGameMax = _dGrowSum = 0;
            _dMbRolls = _dMbVeto = _dRetryRansom = _dRetryRansomDeal = 0;
            return t;
        }
    }
}
