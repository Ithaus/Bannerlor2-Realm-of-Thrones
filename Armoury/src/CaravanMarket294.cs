using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// PACZKA 294 (29.10, Jeff: "Napraw te karawny one traz nie maja sensu bo glownie traca nikt by ich nie trzymal, skoro wiekszosc traci, jedzenie sie nie psuje
    /// zrobmy, albo psuje znacznie wolniej.") - RYNEK KARAWAN.
    /// Dowod (autotest 136a = WGRANE 136, 90 dob 109045-109134 na kopii zapisu Jeffa; m113/wyniki/grupy-136a): mediana wyniku handlu karawany 33 zl/d wobec zoldu
    /// 85 zl/d, zold pokrywa 146 z 386 zmierzonych (0.37); razem wynik handlu swiata 54.8 tys. zl/d wobec zoldu 43.1 tys. - pula zysku z roznicy cen jest, ale dzieli
    /// sie na ok. 400 karawan, ktore sprzedaja miastom razem 1 160 szt./d (3 szt. na karawane na dobe; popyt rynkow 27.6 tys. szt./d bierze dowoz wsi i wozy KS-H).
    /// Gdy karawan ubywalo (452 -> 395 w dobach 109055-109125), pula ROSLA (45.4 -> 54.8 tys. zl/d) - mniej karawan to nie mniej handlu, tylko wiecej na kazda.
    /// Karawany trzymaja towar 30 dob sprzedazy (juki 4.21 mln zl po koszcie, koszt sprzedanego 131 tys. zl/d), kupno BK 'kapital w towarze' odrzucone 519 razy
    /// na dobe - kapital stoi w jukach. Sprzedaz T1 na stracie ostatnie 10 dob: chleb 727 szt., ryby 550, placek 154, zboze 114, maslo 50, winogrona 40, mieso 26
    /// (wszystko regula 'jedzenie > 7 dob', CaravanGoods.LossWhy 4), wino/piwo/ser/oliwki/daktyle prawie nic.
    /// TRZY REGULY (kazda z kluczem; NIE = co do bitu jak 290):
    ///  A. JEDZENIE WEDLUG RODZAJU (klucz CaravanFoodKeepsByKind) - psuje sie tylko jedzenie psujace sie modelu KS (KsCore.Perish: ryby, mieso, mieso wieloryba,
    ///     jaja, maslo, jablka, marchew, pomarancze, winogrona, daktyle - te same, ktore KS-H wiezie najwyzej 7 dob) i pieczywo (chleb, placek - piekarnie miast,
    ///     KS-H ich nie wozi). Psujace sie traci 1.5% na dobe (ten sam ulamek co wozy KS-H w drodze - KsFoodCarts.SpoilPerDay); po 7 dobach (FoodAgeDays) kupiec
    ///     sprzedaje je na stracie, ale nie taniej niz koszt x (1 - 1.5% x doby w jukach) - strata nie wieksza niz to, co juz sie zepsulo; reszta czeka na
    ///     nastepne miasto (T1 i sprzedaz BK / gry - ta sama granica). Reszta jedzenia (zboze, ser, oliwki, piwo, miod, ...) nie psuje sie - jak kazdy towar:
    ///     na stracie dopiero po 30 dobach bez rynku z zyskiem (regula 5, AgeMarket, 286/286b).
    ///  B. STRATNA KARAWANA ZWIJANA (klucz CaravanLosingWoundUp) - kupiec (albo rod) nie trzyma karawany, ktora nie zarabia na swoj zold. Karawana AI sledzona
    ///     co najmniej 28 dob (CaravanFlow.IdleDisbandDays - ten sam czas co m6b), ktorej wynik handlu (utarg - myto - koszt, ksiega 284, okno 14 dob) jest nizszy
    ///     od zoldu przez 14 kolejnych dob pomiaru, ZACZYNA ZWIJANIE (CaravanTrade284.Daily): kupno BK stoi (Why 6), towar schodzi zwyklymi regulami T1 / BK
    ///     w nastepnych miastach (z zyskiem, potem regula 5 po 30 dobach) - bez wyprzedazy ponizej kosztu w jednym miescie (recenzja 294). Rozwiazanie
    ///     (CaravanFlow.WindHourly, co godzine, karawana w miescie bez bitwy i oblezenia), gdy gotowa: towar po koszcie <= tydzien zoldu albo 28 dob zwijania -
    ///     reszta miastu z nadwyzki kasy (SellOut), kiesa do wlasciciela E3/7, ludzie do domu E4/8, niesprzedane na polke E6/K1. Nigdy: rod gracza, bohater
    ///     w skladzie, zadanie, kontrakt, konwoj DTE, karawana morska (statki przepadlyby - DestroyPartyAction ich nie rusza) - CaravanFlow.WindBlocker.
    ///     Ksiega zysku karawan w zapisie (arm_k294) - seria, sledzenie i zwijanie przezywaja wczytanie.
    ///  C. NOWA KARAWANA TYLKO, GDY HANDEL PLACI (klucz CaravanEntryWhenTradePays) - kupiec (CanHeroCreateCaravan) i lord BK (ShouldHaveCaravan) wysylaja nowa
    ///     karawane tylko wtedy, gdy po jej dodaniu placacych zold jest nadal co najmniej PayShare (0.60) wszystkich: miejsce = W / 0.60 - (W + L + T + U)
    ///     z wczorajszego pomiaru (W - wynik >= zold, L - wynik < zold, T - nowe ponizej 7 dob pomiaru, U - bez historii; bez konwojow DTE), kazda dopuszczona
    ///     zmniejsza miejsce o 1; kupiec, ktoremu zwinieto karawane, czeka 28 dob. Brama dziala, gdy zmierzona jest co najmniej polowa karawan (inaczej - jak w grze:
    ///     nowa gra, wczytanie zapisu bez ksiegi 294). Zloto zostaje u kupca / lorda.
    /// Nic z niczego: A - mniej sprzedazy ponizej kosztu (towar w jukach, zloto w kasie miasta); B - istniejace drogi sprzedazy i rozwiazania (kazda moneta i sztuka
    /// z platnikiem); C - zakup karawany nie zachodzi. Linia doby "Rynek karawan (294): dzien N" (CaravanTrade284.Daily, takze przy kluczach NIE - pomiar).
    /// 294b: B i C domyslnie NIE (autotest 296a: zwijanie zniszczylo 2/3 handlu - karawan zmierzonych 423 -> 133, pula 0.43, wyprzedaz zwinietych strata 7 155 zl/d,
    /// najazdy na karawane 0.64% -> 2.49% na dobe; brama blokowala zastepstwa) - przyczyna strat (martwy towar) idzie przez glebokosc rynku (CaravanTrade284.DepthNet).
    /// </summary>
    internal static class CaravanMarket294
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private const int FoodAgeDays = 7;                                     // jak CaravanGoods.FoodAgeDays (polka psujacego sie w modelu KS - 7 dob)
        private static readonly HashSet<string> Bakery = new HashSet<string>(StringComparer.Ordinal) { "bread", "pie" };

        internal static bool FoodKeepsOn { get { var s = Settings.Current; return s != null && s.CaravanFoodKeepsByKind; } }

        private static int _stumbles, _stumbleSess;
        private static readonly HashSet<string> _err = new HashSet<string>();

        private static void Stumble(string where, Exception e)
        {
            _stumbles++; _stumbleSess++;
            try { if (_err.Add(where)) Log.Error("CaravanMarket294." + where, e); } catch { }
        }

        // ------------------------------------------------------------ A. jedzenie wedlug rodzaju

        /// <summary>Jedzenie psujace sie: KsCore.Perish (id przedmiotu) albo pieczywo (id przedmiotu albo kategorii chleb / placek).</summary>
        internal static bool Perishable(ItemObject it)
        {
            if (it == null || !it.IsFood) return false;
            string id = it.StringId ?? "";
            if (KsCore.Perish.Contains(id) || Bakery.Contains(id)) return true;
            var c = it.ItemCategory;
            return c != null && c.StringId != null && Bakery.Contains(c.StringId);
        }

        /// <summary>Czy dla tego towaru dziala regula 'jedzenie psuje sie' (CaravanGoods.LossWhy 4): klucz NIE - kazde jedzenie (jak dotad); TAK - tylko psujace sie.</summary>
        internal static bool Spoils(ItemObject it)
        {
            if (it == null || !it.IsFood) return false;
            return !FoodKeepsOn || Perishable(it);
        }

        /// <summary>Towar jak trwaly (regula 5 - dlugo w jukach, AgeMarket, 286/286b): nie jedzenie albo (klucz TAK) jedzenie, ktore sie nie psuje.</summary>
        internal static bool Durable(ItemObject it)
        {
            if (it == null) return false;
            return !it.IsFood || (FoodKeepsOn && !Perishable(it));
        }

        /// <summary>Najnizsza cena sztuki po mycie przy sprzedazy psujacego sie na stracie (powod 4): koszt x (1 - 1.5% x doby w jukach), nie mniej niz 0.
        /// 0 - bez granicy (klucz NIE albo brak kosztu).</summary>
        internal static double SpoilFloor(double unitCost, double age)
        {
            if (!FoodKeepsOn || !(unitCost > 0.0)) return 0.0;
            return unitCost * Math.Max(0.0, 1.0 - KsFoodCarts.SpoilPerDay * Math.Max(0.0, age));
        }

        // liczniki od ostatniej linii 294 (A)
        private static long _fT1Held, _fBkHeld, _fDurN, _fDurG, _fDurWait;
        internal static void NoteFloorT1() { _fT1Held++; }
        internal static void NoteFloorBk(long n) { if (n > 0) _fBkHeld += n; }
        internal static void NoteDurableLoss(int n, long g) { if (n > 0) { _fDurN += n; _fDurG += g; } }
        internal static void NoteDurableWait() { _fDurWait++; }

        // ------------------------------------------------------------ B / C. pomiar rynku karawan (z CaravanTrade284.Daily)

        internal static int ExitDays { get { return (int)CaravanFlow.IdleDisbandDays; } }
        private const int ReadyWageDays = 7;                                   // jak CaravanTrade284.MeasureDays: towar wart mniej niz tydzien zoldu nie jest wart dalszej drogi

        /// <summary>C: udzial placacych zold, ponizej ktorego nowa karawana nie rusza (cel paczki 294 - wyrazna wiekszosc karawan placi zold; ten sam prog co K294-1
        /// w sprawdz_logi). Miejsce = W / PayShare - wszystkie: nowa karawana dopuszczona tylko, gdy po jej dodaniu placacych jest nadal co najmniej PayShare.</summary>
        internal const double PayShare = 0.60;

        private static bool _have;                                             // pomiar na brame: zmierzone (W + L) co najmniej polowa wszystkich karawan
        private static int _room, _roomStart;                                 // _room - zostalo na dzis, _roomStart - miejsce z pomiaru
        private static double _medNet = double.NaN, _medPd = double.NaN, _medWg = double.NaN;
        // liczniki od ostatniej linii (B, C)
        private static int _xDone, _xFail, _xUnwound, _xNotReady;
        private static readonly Dictionary<string, int> _xWhy = new Dictionary<string, int>();
        private static long _xSoldU, _xSoldG, _xSoldC, _xLeftU, _xLeftC, _xPurse; private static int _xMen;
        private static int _eAsk, _eNoData, _eRoom, _eOwner, _eOk, _eLordAsk, _eLordNo;

        internal static void Reset()
        {
            _have = false; _room = _roomStart = 0;
            _medNet = _medPd = _medWg = double.NaN;
            _stumbleSess = 0; _err.Clear();
            ZeroDay();
        }

        private static void ZeroDay()
        {
            _fT1Held = _fBkHeld = _fDurN = _fDurG = _fDurWait = 0;
            _xDone = _xFail = _xUnwound = _xNotReady = 0; _xWhy.Clear(); _xSoldU = _xSoldG = _xSoldC = _xLeftU = _xLeftC = _xPurse = 0; _xMen = 0;
            _eAsk = _eNoData = _eRoom = _eOwner = _eOk = _eLordAsk = _eLordNo = 0;
            _stumbles = 0;
        }

        /// <summary>B (CaravanFlow.WindHourly, karawana w zwijaniu w miescie): gotowa do rozwiazania - towar handlowy po koszcie nie wiecej niz tydzien jej zoldu
        /// (reszta nie jest warta dalszej drogi) albo zwijana juz ExitDays dob (do tej pory towar mial 30 dob na regule 5 - rynek z zyskiem, potem na stracie w T1).</summary>
        internal static bool WindReady(MobileParty mp)
        {
            try
            {
                int d = CaravanTrade284.WindDays(mp);
                if (d < 0) return false;
                if (d >= ExitDays) return true;
                double cargo = CaravanGoods.CargoCost(mp);
                if (cargo <= ReadyWageDays * Math.Max(0, mp.TotalWage)) return true;
                _xNotReady++;
                return false;
            }
            catch (Exception e) { Stumble("WindReady", e); return false; }
        }

        internal static void NoteExit(int soldU, long soldG, long soldC, int leftU, long leftC, long purse, int men)
        {
            _xDone++; _xSoldU += soldU; _xSoldG += soldG; _xSoldC += soldC; _xLeftU += leftU; _xLeftC += leftC; _xPurse += purse; _xMen += men;
        }

        internal static void NoteExitFail() { _xFail++; }

        internal static void NoteUnwound(string why)
        {
            _xUnwound++;
            if (why != null) { int n; _xWhy.TryGetValue(why, out n); _xWhy[why] = n + 1; }
        }

        /// <summary>C: czy nowa karawana tego bohatera moze ruszyc (true - tak). waited - kupiec / lord, ktoremu zwinieto karawane w ostatnich 28 dobach. lord - zakup BK.</summary>
        internal static bool EntryAllowed(bool waited, bool lord)
        {
            var s = Settings.Current;
            if (s == null || !s.CaravanEntryWhenTradePays) return true;
            if (lord) _eLordAsk++; else _eAsk++;
            if (waited) { _eOwner++; if (lord) _eLordNo++; return false; }
            if (!_have) { _eNoData++; _eOk++; return true; }                  // bez pomiaru (nowa gra, wczytanie bez ksiegi - mniej niz polowa zmierzona) - jak w grze
            if (_room <= 0) { _eRoom++; if (lord) _eLordNo++; return false; }
            _room--; _eOk++;
            return true;
        }

        private static double Med(List<double> l)
        {
            if (l == null || l.Count == 0) return double.NaN;
            l.Sort();
            return l.Count % 2 == 1 ? l[l.Count / 2] : 0.5 * (l[l.Count / 2 - 1] + l[l.Count / 2]);
        }

        private static string F0(double v) { return double.IsNaN(v) ? "-" : v.ToString("0", Inv); }

        private static void Top(StringBuilder sb, Dictionary<string, int> d)
        {
            if (d == null || d.Count == 0) return;
            sb.Append(" [");
            int k = 0;
            foreach (var kv in d) { if (k++ > 0) sb.Append(", "); sb.Append(kv.Key).Append(' ').Append(kv.Value); }
            sb.Append(']');
        }

        /// <summary>CaravanTrade284.Daily (po przegladzie karawan): W/L/T/U, seria stratnych, mediany, zwijanie; linia doby "Rynek karawan (294)". net - wynik handlu/d
        /// minus zold/d zmierzonych (bez konwojow DTE), pd / wg - wynik i zold tych samych; wNew - zaczete dzis zwijanie, winding - w zwijaniu, wWhy - powody, dla
        /// ktorych kandydat nie zaczal zwijania; rWind - kupno BK wstrzymane zwijaniem (kategoria x wizyta); book* - ksiega 294 z zapisu (sesja).</summary>
        internal static string Daily(int day, int w, int l, int t, int u, int streak, List<double> net, List<double> pd, List<double> wg, int wNew, int winding,
                                     Dictionary<string, int> wWhy, int rWind, bool bookHad, int bookOk, int bookGone, int bookBad)
        {
            var s = Settings.Current;
            var sb = new StringBuilder(1800);
            try
            {
                int room0 = _have ? _roomStart : 0; bool had = _have;
                int m = w + l, all = w + l + t + u;
                _have = m > 0 && 2 * m >= all;
                _roomStart = _room = (int)Math.Floor(w / PayShare + 1e-9) - all;
                _medNet = Med(net); _medPd = Med(pd); _medWg = Med(wg);
                sb.Append("Rynek karawan (294): dzien ").Append(day)
                  .Append(" | klucze: jedzenie wedlug rodzaju ").Append(s != null && s.CaravanFoodKeepsByKind ? "TAK" : "NIE")
                  .Append(", stratne karawany zwijane ").Append(s != null && s.CaravanLosingWoundUp ? "TAK" : "NIE")
                  .Append(", nowe karawany gdy handel placi ").Append(s != null && s.CaravanEntryWhenTradePays ? "TAK" : "NIE")
                  .Append(" | jedzenie: psujace sie po ").Append(FoodAgeDays).Append(" dobach na stracie nie taniej niz koszt x (1 - ").Append((100.0 * KsFoodCarts.SpoilPerDay).ToString("0.0", Inv))
                  .Append("% x doby) - wstrzymane T1 ").Append(_fT1Held).Append(" krokow, BK i gra ").Append(_fBkHeld).Append(" szt.; trwale jedzenie jak towar: na stracie (dlugo w jukach) ")
                  .Append(_fDurN).Append(" szt. za ").Append(_fDurG).Append(" zl, czeka na rynek z zyskiem ").Append(_fDurWait)
                  .Append(" | karawany zmierzone ").Append(m).Append(": placace ").Append(w).Append(", tracace ").Append(l).Append(" (seria >= ").Append(CaravanTrade284.RingDays)
                  .Append(" dob ").Append(streak).Append("), nowe ").Append(t).Append(", bez historii ").Append(u).Append(", udzial placacych ").Append(m > 0 ? (w / (double)m).ToString("0.000", Inv) : "-")
                  .Append(", mediana wyniku - zoldu/d ").Append(F0(_medNet)).Append(" zl, mediana wyniku/d ").Append(F0(_medPd)).Append(" zl, mediana zoldu/d ").Append(F0(_medWg)).Append(" zl")
                  .Append(" | ksiega 294 z zapisu ").Append(bookHad ? "TAK" : "NIE").Append(" (wczytane ").Append(bookOk).Append(", karawan juz nie ma ").Append(bookGone).Append(", nieczytelne ").Append(bookBad).Append(')')
                  .Append(" | zwijanie stratnych (seria >= ").Append(CaravanTrade284.RingDays).Append(" dob, sledzona >= ").Append(ExitDays).Append(" dob, bez morskich): zaczete ").Append(wNew);
                Top(sb, wWhy);
                sb.Append(", w zwijaniu ").Append(winding).Append(" (kupno BK wstrzymane ").Append(rWind).Append(" kategorii x wizyta, niegotowe w miescie - godzin ").Append(_xNotReady)
                  .Append("), zwiniete ").Append(_xDone).Append(" (towar miastu ").Append(_xSoldU).Append(" szt. za ").Append(_xSoldG).Append(" zl, koszt ").Append(_xSoldC)
                  .Append(" zl; niesprzedane na polke ").Append(_xLeftU).Append(" szt., koszt ").Append(_xLeftC).Append(" zl; kiesy ").Append(_xPurse).Append(" zl do wlascicieli, ludzie ").Append(_xMen)
                  .Append("), przerwane ").Append(_xUnwound);
                Top(sb, _xWhy);
                sb.Append(", nie zniknely ").Append(_xFail)
                  .Append(" | nowe karawany: prosby kupcow ").Append(_eAsk).Append(", lordow BK ").Append(_eLordAsk).Append(", dopuszczone ").Append(_eOk)
                  .Append(" (bez pomiaru - jak w grze ").Append(_eNoData).Append("), wstrzymane: brak miejsca ").Append(_eRoom).Append(", wlasciciel po zwinieciu ").Append(_eOwner)
                  .Append(" (w tym lordow ").Append(_eLordNo).Append("); pomiar na brame ").Append(_have ? "TAK" : "NIE").Append(" (zmierzone ").Append(m).Append(" z ").Append(all)
                  .Append("), miejsce wczoraj ").Append(had ? room0.ToString(Inv) : "-").Append(", na jutro W / ").Append(PayShare.ToString("0.00", Inv)).Append(" - wszystkie = ").Append(_room)
                  .Append(" | potkniecia 294 ").Append(_stumbles).Append(" (sesja ").Append(_stumbleSess).Append(").");
            }
            catch (Exception e) { Stumble("Daily", e); sb.Clear(); sb.Append("Rynek karawan (294): dzien ").Append(day).Append(" | blad linii | potkniecia 294 ").Append(_stumbles).Append(" (sesja ").Append(_stumbleSess).Append(")."); }
            finally { ZeroDay(); }
            return sb.ToString();
        }
    }
}
