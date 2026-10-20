using System;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace Armoury
{
    /// <summary>
    /// m96-koszyk (20.10; PLAN 5.5 "kazdego dnia w kazdym miescie co najmniej 95% ludzi stac na jedzenie, 98% na pelny koszyk, zjedzone bez zaplaty ponizej 1%";
    /// RAPORT-NOCNY-2026-10-20 rozdz. 5: koszyk jedzenia ludu 88,5% przy progu 95%) - PLACA DLA KLAS W BIEDZIE.
    /// Dowod (WGRANIE 95, 160 dob z zapisu d240, Armoury-2026-10-20_12-25-19.log; to samo w 09-14-49, 04-01-39 i 00-44-29): do doby 109144 "stac jedzenie W/R/P"
    /// 100/100/100%. Kotwica KS (Koszyk stanow ZC4) wchodzi lambda 0 -> 1 w dobach 109126-109141, a od 109145 R spada do 85-95%, P do 94-99%, pelny koszyk swiata
    /// sr28 91.4-92.4%, 6.7-9.2 miast (najwiecej 10-15) ze stac jedzenie &lt; 95%, zjedzone bez zaplaty sr28 0.70-0.84%, najwiecej 2.4-3.0%. Wydatek R 38 -> 100-130 tys./d,
    /// P 8 -> 75-114 tys./d (kotwica KS przelozyla potrzebe z W na R i P), a podzial zarobku miedzy klasy (SplitOf) zostal na klasach BK. f5-lud tnie dochod W
    /// (doba 109160: kappa W 0.10 w 96 z 97 miast): placa W, ktorej W nie przyjal, zostaje w kasach miast - 188 tys./d w dobie 109160, 119 tys./d w 109234 (maleje
    /// razem z kappa W2; linia f5 "z zewnatrz ... W pelne/przyjete" i "z kas ... W"); razem z M6 przycieta kappa R/P w bogatych miastach "niezaplacone ludowi w kasach"
    /// 384-536 tys./d - a w tych samych miastach R i P nie maja na jutro (kiesa-ludu.csv 19.10 07-51-11: Qarth kiesy W 90700 / R 233 / P 2064, Selhorys 73814 / 930 / 767,
    /// Valysar 79703 / 805 / 872; tez Braavos, Rhyos, Pentos, Norvos, Aquos Dhaen, Meereen, Oldtown). M6 placi R/P ponizej pasma pelna stawka, ale wolno
    /// (1/Ds Merchant Draw Days = 1/28 nadwyzki ponad 1.5 x zapasu kupcow na dobe), a w miastach z kasa ponizej 1.5 x zapasu wcale (CSV: 37 ze 105 miasto-dni ze stac
    /// jedzenie &lt; 95% ma kase ponad 1.5 x, mediana 1.26 x). Chleb biedoty M3 (korona) placi sr28 17.9-23.4 tys./d, a i tak zostaje 14.7-22.0 tys./d "nie stac
    /// nikogo". Polki nie sa przyczyna: linia ZC4 "jedzenie niekupione z braku pieniedzy 4.9% budzetu jedzenia" - brakuje pieniedzy, nie towaru.
    /// REGULA (klucz Ks Folk Wage To Short Classes): raz na dobe w kazdym miescie (TownLive, po placach z kas, przed czynszem, nadwyzka i kotwica jutra) placa
    /// zatrzymana w kasie tej doby - czego klasy z pelna kiesa nie przyjely regula f5 / f4 (glownie W: kappa W 0.10): (a) f5 przy zarobku z zewnatrz (uslugi wojska,
    /// karawan i band, dwor, budowy, kopalnie, warsztaty) i przelewach gracza przez kase (CreditF5 / FromCofferF5 - reszta poszla do kasy miasta), (b) wyplata
    /// kupcom M6 przycieta kappa R/P (EarnDraw - zostaje w kasie), (c) robocizna, doplata i obrot P z kasy przy czynnikach f4 / f5 ponizej 1 (TownLive krok 2) -
    /// idzie do kies R i P tego samego miasta, ktorym brakuje: najpierw jedzenie na 2 dni (udzial jedzenia klasy x potrzeba dzienna z kotwicy), potem pelny
    /// koszyk na Ks Folk Short Basket Days dni (domyslnie 7, przyciete do 2-20: ponizej zapasu R 21 / P 30 dni, wiec nic nie wraca do W placami z nadwyzki
    /// w kroku 5, a klasa w biedzie ma kappa 1 - podloga przezycia f5 - wiec nigdy nie jest jednoczesnie dawca i biorca). W kazdym etapie klasy dziela wedlug
    /// swoich luk. Prace, ktorej ludzie z pelna kiesa juz nie biora, biora rzemieslnicy i kupcy tego miasta, ktorym brakuje na chleb.
    /// Placa przebudowy zajecia wsi (A7, CraftsRebuildPay) zatrzymana w kasie NIE wchodzi do okna (M96Uncut) - te monete kasa oddaje wsiom robotami miasta (TownHire,
    /// p6-a7); bez tego ta sama kwota wychodzilaby z kasy dwa razy (recenzja m96, CLAUDE.md 8.0).
    /// ZAMKNIETY PIENIADZ: z kasy schodzi najwyzej placa zatrzymana tej doby i najwyzej stan kasy - kasa nie placi wiecej, niz placila przed paczka f5 (cala placa
    /// wszystkich klas). Ksiega kies: _ctlIn i LDayIn (kontrola linii "Kiesa ludu" r = 0). Miasto bez placy zatrzymanej - nic (zostaja chleb M3 i dawne drogi;
    /// licznik "bez pokrycia").
    /// ZAROBEK OKNA, NIE SAM PRZELEW (recenzja techniczna m96 - decyzja swiadoma): przelew to placa za prace, ktorej W nie wzial - jak kazdy wplyw do kies z kasy (M6
    /// EarnDraw, Move) jest zarobkiem okna R/P (EarnWin; w skladzie zarobku C8 / kiesa-obieg.csv rodzaj "inne" - EPublic, nie "robocizna handlu"): podatek pana, cech,
    /// 184 i dziesiecina nastepnej doby (ok. 5-8% przelewu), ZC0 (Dostatek) i budzet Planu KS (krok 8: srednia 28 dob zarobku netto x 0.90). Sprzezenie: budzet R/P
    /// biednych miast rosnie -> Plan placi wieksza czesc koszyka R/P (baza ostatniej linii: Essos R 74.8-82.8%, P 78.6-87.9%; Westeros R 92.1-96.5%, P 90.2-96.8%)
    /// -> czesc zaplacona miasta (TownPaid - jedna na miasto, dla wszystkich klas w Zc4) w gore -> potrzeba W/R/P tych miast w gore, ku pelnemu koszykowi -> luka R/P
    /// i przelew w gore, dni koszyka W w dol (kappa W w gore, placa zatrzymana w dol). Ograniczone z obu stron: czesc zaplacona &lt;= 1, przelew &lt;= placa zatrzymana
    /// tej doby i stan kasy; Plan co 7 dob, budzet ze sredniej 28 dob - pelny skutek po ok. 4 tygodniach. Wariant "przelew bez zarobku okna" zostawilby Plan przy R/P
    /// Essos placacych 75-83% koszyka, a "stac jedzenie / pelny" mierzylby kiesy wobec koszyka przycietego bieda - to ukrywa glod zamiast go usunac. Sprzezenie
    /// mierza progi MK-11 (Plan: czesc zaplacona R/P) i MK-12 (f5: dni koszyka, kappa W2, placa W nieprzyjeta).
    /// Wylaczony klucz - licznik okna zerowany, nic sie nie rusza (kod co do monety jak dotad). Bez pola zapisu (okno jednej doby) - stary zapis wczytuje sie
    /// bez zmian. Linie "Placa dla klas w biedzie (m96-koszyk): start sesji" i "...: dzien N" (tylko gdy jest miasto z kotwica KS albo z placa zatrzymana - w nowej
    /// kampanii przed KS sama linia startu). Watek glowny (blok doby ludu; zapis okna tylko z Earn / EarnFromTown / EarnDraw - te same bramki watku). Wyjatek per
    /// miasto - licznik potkniec, bez globalnego wylacznika (CLAUDE.md 7).
    /// </summary>
    internal static partial class TownFolk
    {
        private const double M96FoodDays = 2.0;
        private const int M96NameMax = 6;
        private static readonly CultureInfo M96Inv = CultureInfo.InvariantCulture;
        private static readonly StringBuilder _m96Names = new StringBuilder(256);
        private static bool _m96Start;
        // liczniki doby (M96DayLine zeruje)
        private static int _m96Towns, _m96CutN, _m96GapN, _m96PaidN, _m96NoCutN, _m96EmptyN, _m96Stum, _m96StumS, _m96HungryN, _m96NamesN;
        private static long _m96Cut, _m96Ask, _m96Got, _m96R, _m96P, _m96Food, _m96Rest, _m96Over, _m96Uncut;
        private static double _m96GapFood, _m96GapAll, _m96LeftNoCut, _m96LeftAfter;
        private static long _m96Ticks;

        private static bool M96On { get { var s = Settings.Current; return s != null && s.KsFolkWageToShortClasses; } }

        private static double M96Days
        {
            get
            {
                var s = Settings.Current;
                double v = s != null ? s.KsFolkShortBasketDays : 7.0;
                if (double.IsNaN(v) || double.IsInfinity(v)) v = 7.0;
                // ponizej dolnych granic pasm f5 R / P (podloga przezycia - kappa 1): klasa w biedzie nigdy nie jest jednoczesnie dawca placy zatrzymanej
                if (s != null) v = Math.Min(v, Math.Min(s.KsFolkIncomeRLowDays, s.KsFolkIncomePLowDays) - 1.0);
                return Math.Max(M96FoodDays, Math.Min(20.0, v));
            }
        }

        private static void M96Reset()
        {
            _m96Start = false; M96Zero(); _m96StumS = 0;
        }

        private static void M96Zero()
        {
            _m96Towns = _m96CutN = _m96GapN = _m96PaidN = _m96NoCutN = _m96EmptyN = _m96Stum = _m96HungryN = _m96NamesN = 0;
            _m96Cut = _m96Ask = _m96Got = _m96R = _m96P = _m96Food = _m96Rest = _m96Over = _m96Uncut = 0;
            _m96GapFood = _m96GapAll = _m96LeftNoCut = _m96LeftAfter = 0; _m96Ticks = 0;
            _m96Names.Length = 0;
        }

        /// <summary>CreditF5 / FromCofferF5 / EarnDraw (miasto z regula f5): kwota pelna wobec przyjetej przez klasy - roznica zostala w kasie miasta (okno doby).
        /// Czysta arytmetyka (bez alokacji; te same bramki watku co wolajacy).</summary>
        private static void M96NoteCut(Folk f, long full, long acc)
        {
            if (f != null && full > acc) f.M96Cut += full - acc;
        }

        /// <summary>Recenzja m96 (CLAUDE.md 8.0): CraftsRebuildPay - placa przebudowy zajecia wsi (A7) zatrzymana w kasie regula f5 (PayVillageWage `cof`) wraca do wsi
        /// robotami miasta (TownHire.NoteRebuild: material + placa nieprzyjeta) - zdjeta z okna, zeby kasa nie oddawala tej samej monety drugi raz klasom R/P.
        /// Czysta arytmetyka, watek wolajacego (glowny). Klucz wylaczony - okno i tak zerowane w M96Short.</summary>
        private static void M96Uncut(Folk f, long x)
        {
            if (f == null || x <= 0 || f.M96Cut <= 0) return;
            long y = Math.Min(x, f.M96Cut);
            f.M96Cut -= y; _m96Uncut += y;
        }

        /// <summary>Po przelewie: R albo P bez jedzenia na jutro (ta sama miara co "stac jedzenie" w kotwicy: kiesa wobec 0.95 x udzial jedzenia x potrzeba dzienna).
        /// Liczy miasto i dopisuje nazwe (najwyzej M96NameMax; oblezone oznaczone) - wyjasnienie progu MK-5 (miasto, ktoremu regula nie pomogla).</summary>
        private static void M96NoteHungry(Folk f, double kR, double kP, double nR, double nP)
        {
            if (!(kR < 0.95 * FdR * nR) && !(kP < 0.95 * FdP * nP)) return;
            _m96HungryN++;
            if (_m96NamesN >= M96NameMax || f == null || f.St == null) return;
            if (_m96NamesN > 0) _m96Names.Append("; ");
            string nm = f.St.Name != null ? f.St.Name.ToString() : f.St.StringId;
            _m96Names.Append(Clean(nm).Replace('|', '/'));
            if (f.St.IsUnderSiege) _m96Names.Append(" [oblezone]");
            _m96NamesN++;
        }

        /// <summary>
        /// TownLive krok 2b (po placach z kas, przed krokami 3-6): placa zatrzymana w kasie tej doby (okno f5 / M6 + `cutCoffer` - robocizna, doplata i obrot
        /// P z kasy, ktorych kasa nie wyplacila przy czynnikach f4 / f5) do kies R i P w biedzie - jedzenie na 2 dni, potem koszyk na Ks Folk Short Basket Days dni. Okno zawsze zerowane. Nie rzuca.
        /// </summary>
        private static void M96Short(Folk f, long cutCoffer)
        {
            if (f == null) return;
            long cut = Math.Max(0, f.M96Cut) + Math.Max(0, cutCoffer);
            f.M96Cut = 0;
            if (!M96On || f.Kind != 0 || f.St == null || f.St.Town == null) return;
            long t0 = Stopwatch.GetTimestamp();
            try
            {
                if (!f.FacOk) return;                                  // kotwica jeszcze nie liczona - potrzeby klas nieznane
                // recenzja m96: liczone tylko miasta z kotwica KS albo z placa zatrzymana (bez KS f5 / f4 nic nie zatrzymuja - nowa kampania: sama linia startu);
                // przy cut > 0 droga jak dotad (zachowanie bez zmian)
                if (cut <= 0 && !StartCalibration.KsTownApplied(f.St.Town)) return;
                _m96Towns++;
                if (cut > 0) { _m96CutN++; _m96Cut += cut; }
                double nR = f.Need[1], nP = f.Need[2];
                if (!(nR > 0) || double.IsInfinity(nR)) nR = 0;
                if (!(nP > 0) || double.IsInfinity(nP)) nP = 0;
                double kR = Math.Max(0L, f.KR), kP = Math.Max(0L, f.KP), T = M96Days;
                // etap 1 - jedzenie na 2 dni; etap 2 - reszta pelnego koszyka do T dni (ta sama potrzeba dzienna co kotwica i "stac jedzenie / pelny")
                double gR1 = Math.Max(0, M96FoodDays * FdR * nR - kR), gP1 = Math.Max(0, M96FoodDays * FdP * nP - kP);
                double gR2 = Math.Max(0, T * nR - kR - gR1), gP2 = Math.Max(0, T * nP - kP - gP1);
                double g1 = gR1 + gP1, g2 = gR2 + gP2, gap = g1 + g2;
                if (!(gap >= 1)) return;
                _m96GapN++; _m96GapFood += g1; _m96GapAll += gap;
                if (cut <= 0) { _m96NoCutN++; _m96LeftNoCut += gap; _m96LeftAfter += gap; M96NoteHungry(f, kR, kP, nR, nP); return; }
                var town = f.St.Town;
                long avail = Math.Min(cut, Math.Max(0L, (long)town.Gold));
                if (avail <= 0) { _m96EmptyN++; _m96LeftAfter += gap; M96NoteHungry(f, kR, kP, nR, nP); return; }
                double x1 = Math.Min(avail, g1), x2 = Math.Min(avail - x1, g2);
                long xR = (long)Math.Floor((g1 > 0 ? x1 * gR1 / g1 : 0) + (g2 > 0 ? x2 * gR2 / g2 : 0));
                long xP = (long)Math.Floor((g1 > 0 ? x1 * gP1 / g1 : 0) + (g2 > 0 ? x2 * gP2 / g2 : 0));
                xR = Math.Max(0, xR); xP = Math.Max(0, xP);
                long ask = xR + xP;
                if (ask > avail) { xP = Math.Max(0, avail - xR); ask = xR + xP; }   // zaokraglenia - nigdy ponad placa zatrzymana i kase
                if (ask <= 0) { _m96LeftAfter += gap; M96NoteHungry(f, kR, kP, nR, nP); return; }
                int had = town.Gold;
                town.ChangeGold(-(int)Math.Min(int.MaxValue, ask));
                long got = Math.Max(0L, (long)had - town.Gold);
                _m96Ask += ask;
                if (got <= 0) { _m96EmptyN++; _m96LeftAfter += gap; M96NoteHungry(f, kR, kP, nR, nP); return; }
                if (got < ask) { xR = (long)Math.Floor(got * (double)xR / ask); xP = got - xR; }
                // zarobek okna R/P (LDayIn, EarnWin - podatki nastepnej doby i budzet Planu KS; sklad zarobku C8 - "inne", nie robocizna handlu)
                CreditParts(f, 0, xR, xP, EPublic, 0);
                _ctlIn += got;                                          // ksiega kies: kasa -> kiesy (kontrola zamknieta)
                _m96Got += got; _m96R += xR; _m96P += xP; _m96PaidN++;
                long food = (long)Math.Min(got, Math.Floor(x1));
                _m96Food += food; _m96Rest += got - food;
                if (got > cut) _m96Over += got - cut;                   // kontrola (oczekiwane 0): przelew ponad placa zatrzymana
                _m96LeftAfter += Math.Max(0, gap - got);
                M96NoteHungry(f, kR + xR, kP + xP, nR, nP);
            }
            catch (Exception e) { _m96Stum++; _m96StumS++; Stumble("M96Short", e); }
            finally { _m96Ticks += Stopwatch.GetTimestamp() - t0; }
        }

        private static string M96F(double v, string fmt) { return double.IsNaN(v) || double.IsInfinity(v) ? "-" : v.ToString(fmt, M96Inv); }

        /// <summary>DailyLive (po linii f5): linia startu sesji (raz) i linia doby (gdy klucz wlaczony i bylo choc jedno miasto z kotwica KS albo z placa zatrzymana).
        /// Nie rzuca.</summary>
        private static void M96DayLine(int day)
        {
            try
            {
                var s = Settings.Current;
                if (s == null) return;
                bool on = s.KsFolkWageToShortClasses;
                if (!_m96Start)
                {
                    _m96Start = true;
                    Log.Info("Placa dla klas w biedzie (m96-koszyk): start sesji | klucz Ks Folk Wage To Short Classes " + (on ? "TAK" : "NIE")
                        + ", cel: jedzenie na " + M96F(M96FoodDays, "0") + " dni, potem pelny koszyk R/P na " + M96F(M96Days, "0.#") + " dni (Ks Folk Short Basket Days "
                        + M96F(s.KsFolkShortBasketDays, "0.#") + ", przyciete 2-20) | zrodlo: placa zatrzymana w kasie tej doby (f5 / f4 / M6 - klasy z pelna kiesa; bez placy"
                        + " przebudowy wsi A7 - wraca do wsi robotami miasta), najwyzej stan kasy | przelew = zarobek okna R/P (podatki nastepnej doby, budzet Planu KS).");
                }
                if (!on || _m96Towns == 0) { M96Zero(); return; }
                double ms = _m96Ticks * 1000.0 / Stopwatch.Frequency;
                var sb = new StringBuilder(900);
                sb.Append("Placa dla klas w biedzie (m96-koszyk): dzien ").Append(day)
                  .Append(" | miasta z kotwica ").Append(_m96Towns).Append(" (z placa zatrzymana w kasie ").Append(_m96CutN).Append(": ").Append(_m96Cut).Append(" zl)")
                  .Append(", zdjete z okna - placa przebudowy wsi A7 (zwrot TownHire) ").Append(_m96Uncut).Append(" zl")
                  .Append(" | z luka R/P ").Append(_m96GapN).Append(" (jedzenie na 2 dni ").Append((long)Math.Round(_m96GapFood)).Append(" zl, razem z koszykiem ")
                  .Append((long)Math.Round(_m96GapAll)).Append(" zl)")
                  .Append(" | przelane z kas do kies ").Append(_m96Got).Append(" zl (R ").Append(_m96R).Append(" / P ").Append(_m96P).Append("; jedzenie ").Append(_m96Food)
                  .Append(", reszta koszyka ").Append(_m96Rest).Append(") w ").Append(_m96PaidN).Append(" miastach")
                  .Append(" | bez pokrycia: bez placy zatrzymanej ").Append(_m96NoCutN).Append(" miast (").Append((long)Math.Round(_m96LeftNoCut)).Append(" zl), kasa pusta ")
                  .Append(_m96EmptyN).Append(" miast, luka po przelewie ").Append((long)Math.Round(_m96LeftAfter)).Append(" zl")
                  .Append(" | R/P bez jedzenia na jutro po przelewie ").Append(_m96HungryN).Append(" miast");
                if (_m96NamesN > 0) sb.Append(": ").Append(_m96Names.ToString()).Append(_m96HungryN > _m96NamesN ? "; ..." : "");
                sb.Append(" | kontrola: zlecone ").Append(_m96Ask).Append(", zeszlo z kas ").Append(_m96Got).Append(", przelane = R + P: ").Append(_m96R + _m96P == _m96Got ? "tak" : "NIE")
                  .Append(", ponad placa zatrzymana ").Append(_m96Over).Append(" (oczekiwane 0)")
                  .Append(" | czas ").Append(M96F(ms, "0.00")).Append(" ms | potkniecia ").Append(_m96Stum).Append(" (sesja ").Append(_m96StumS).Append(").");
                Log.Info(sb.ToString());
                M96Zero();
            }
            catch (Exception e) { _m96StumS++; Stumble("M96DayLine", e); M96Zero(); }
        }
    }
}
