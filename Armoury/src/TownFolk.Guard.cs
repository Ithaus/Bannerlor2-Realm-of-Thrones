using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// obieg2 (decyzja Jeffa 15.10: "napraw tez to, ze kasa sie zbiera u ludu lub mieszczan, bo w dlugim okresie lordowie i krolestwa zostana bez kasy ... ci, co
    /// powinni byc najbogatsi, stana sie najbiedniejsi"; wczesniej "najwyzej zwieksz podatki") - PODATEK OD NADWYZKI KIES WSI i STRAZNIK CALEGO OBIEGU.
    ///  - DOWOD (test 4 lata wersja 54, linie "Pieniadz swiata", "Gotowka wsi (E5/0)", "Kiesa ludu", "Zbytki ludu (P3-P5)", "Towary"): udzial ludu w zlocie swiata
    ///    (kiesy ludu miast, podzamczy, gmin i wsi) 3.7% (d0) -> 25.9% (d720) -> 40.8% (d1458); glowy rodow i lordowie 34.2% -> 25.4%, skarbce koron 26.4% -> 6.8%.
    ///    Kiesy wsi 1.1 -> 16.8 -> 35.6 mln: wies zatrzymuje z utargu 61 tys./d (d1100-1458; d0-719 18 tys./d, gdy korona brala z kies wsi 134 tys./d, po pokoju
    ///    62 tys./d), a wydaje: koszyk dnia 8.3 tys./d z potrzeby 71.8 tys./d (480 z 571 wsi bez towaru - sol, narzedzia, len, garnki i piwo: produkcja swiata ok.
    ///    29 tys./d po cenie zwyklej), zbytki P5 20.8 tys./d z chcianych 531 tys./d (378 z 472 wsi bez towaru). Towaru nie ma - zakupy nie oddadza zlota; jedyna droga
    ///    powrotu kiesy wsi to podatek.
    ///  - (1) WSIE (klucz FolkHoardVillages, wymaga FolkHoardTax): ten sam regulator co kiesa2 - poziom normalny = FolkHoardNormalTimes (3) x 15 dni zakupow wsi
    ///    (RdV x VillagePurchasePerHead x wolne glowy; na starcie kampanii wsie mialy 15 dni), co dobe stawka x kiesa ponad poziom normalny PO koszyku dnia i zakupach
    ///    P5 (wies najpierw kupuje, podatek od reszty). Odbiorcy jak kiesa2: glowa rodu pana wsi (FolkHoardTaxLordShare, RentToday - dochod ziemi D) i kasa osady
    ///    macierzystej (Bound - miasto albo zamek); bez zywego pana - calosc do kasy osady. Wies w rabunku / spladrowana i wies oblezonej osady (BesiegedPay
    ///    KFolkHoard) - nic. Pozniej tej doby W7 i danina wsi korony licza sie od reszty (ta sama zlotowka nie dwa razy).
    ///  - (3) STRAZNIK OBIEGU (klucz FolkCirculationGuard): na poczatku doby ludu P = kiesy ludu z potrzeba (miasta W/R/P, podzamcza, wsie) i N = ich poziom normalny.
    ///    P > FolkGuardBandHigh (1.75) x N - mnoznik stawki x1.03 na dobe (najwyzej FolkGuardMaxTimes 5); P < FolkGuardBandLow (1.25) x N - x0.97 (najmniej 0.25);
    ///    w pasmie bez zmian (sprzezenie zwrotne z sufitem - calkujacy regulator). Pasmo w udziale swiata = 1.25-1.75 x udzial poziomu normalnego (start kampanii: lud
    ///    trzyma ok. poziomu normalnego - 2.6-2.8 x zapas dni). Stawka doby = FolkHoardTaxRate x mnoznik, ale podatek doby najwyzej max(0.15% zlota swiata, 2 x doplyw
    ///    ludu EMA 28 dob) - stary zapis z wielkimi kiesami oddaje nagromadzone w miesiacach, nie w tygodniach (bez wstrzasu pulapow 166); doplyw ludu = kiesy
    ///    na poczatku doby ludu minus na koncu poprzedniej (wszystko, co do ludu wplynelo i nie wyszlo poza naszym podatkiem). Pierwsza doba po wczytaniu - potrzeba
    ///    nieznana, mnoznik czeka (bez ruchu, gdy potrzeba znana w < 80% miast). Czesc korony FolkGuardCrownShare (15%) kazdego podatku od nadwyzki (miasta, zamki,
    ///    wsie) do skarbca krolestwa rodu pana osady (KingdomBudgetWallet - zapas korony, wydawany jej droga 165/W5b); pan i kasa dziela reszte jak dotad.
    ///  - Udzialy w linii: z ksiegi "Pieniadz swiata" poprzedniej doby (MoneyLedger.LastShares) - lud / lordowie (glowy rodow, pozostali lordowie, gracz) / korony.
    ///  - Zapis (arm_obieg2, razem z pozyczkami - TownFolk.Loans.cs): mnoznik i doplyw EMA. Linie: "Obieg ludu (obieg2): start sesji", "Straznik obiegu (obieg2):
    ///    dzien N", "Podatek od nadwyzki wsi (obieg2): dzien N"; "Obieg" - dopiski (MoneyLedger.Obieg.cs). Klucze wylaczone = e2a422ec co do monety.
    /// Wyjatek per wies - licznik potkniec, bez globalnych wylacznikow (CLAUDE.md 7); przelew w kolejnosci kiesa -> pan -> kasa -> skarbiec, wyjatek oddaje zloto wsi.
    /// </summary>
    internal static partial class TownFolk
    {
        // ---- klucze i granice (zakresy jak w tools/gen_mcm.py)
        private static bool GuardOn { get { var s = Settings.Current; return s != null && s.FolkHoardTax && s.FolkCirculationGuard; } }
        private static bool HoardVillOn { get { var s = Settings.Current; return s != null && s.FolkHoardTax && s.FolkHoardVillages; } }
        private static double GdLo(Settings s) { return HdClamp(s.FolkGuardBandLow, 1.0, 3.0); }
        private static double GdHi(Settings s) { return Math.Max(GdLo(s), HdClamp(s.FolkGuardBandHigh, 1.0, 5.0)); }
        private static double GdMax(Settings s) { return HdClamp(s.FolkGuardMaxTimes, 1.0, 10.0); }
        // 15.10 test D (d30-180: korony 24.9 -> 16.9% zlota swiata, lordowie 36.9 -> 40.2%, lud w pasmie): czesc korony zmienna - gdy udzial koron w zlocie
        // swiata (wczorajszy pomiar straznika) spada ponizej FolkGuardCrownTarget, czesc korony = podstawa + 5 x brak, najwyzej 0.8 (reszta jak dotad pan / kasa).
        // Decyzja Jeffa: zwrotu zoldu z korony do rodow NIE ruszamy. korona2 (Jeff 15.10 "korony pozyczaja od miast"): czesc korony idzie
        // do ZAPASU korony (GuardCrownReserve - bez tego 165 oddawal ja tego samego dnia w rentach 180), korony pozyczaja od miast (TownFolk.CrownLoans.cs).
        private static double GuardCrownShare(Settings s)
        {
            if (!GuardOn) return 0;
            double b = HdClamp(s.FolkGuardCrownShare, 0, 0.8);
            if (_gdShares && _gdWorld > 0)
            {
                double now = (double)_gdCrowns / _gdWorld, tgt = HdClamp(s.FolkGuardCrownTarget, 0, 0.5);
                if (now < tgt) b = Math.Min(0.8, b + 5.0 * (tgt - now));
            }
            return b;
        }
        private const double GdStep = 0.03, GdMin = 0.25, GdCapWorld = 0.005,   /* 15.10 test C: 0.0015 nie przegonilo doplywu do ludu (lud 9.9 -> 13% swiata, korony 24.9 -> 18.9% w 160 dob przy mnozniku 5) */ GdCapInflow = 2.0, GdCover = 0.8, GdEmaDays = 28;
        // N7 (noc 16.10, audyt ekonomii 15.10 rozdz. 3.1; klucz FolkGuardNoMemory): mnoznik BEZ PAMIECI = clamp(1 + GdNmSlope x (P/N - GdNmMid), GdMin, GdMax) liczony
        // co dobe z odchylenia. Dowod (log Jeffa d108945 "Straznik obiegu (obieg2)"): mnoznik z pamiecia 5.028 stal, choc kiesy spadly 2.14 -> 1.577 x poziomu - schodzi
        // dopiero ponizej 1.25 x po 3% na dobe (do 1 ok. 54 dob). Przy 1.577 nowa regula daje 1.62 (stawka 1.6%/d zamiast 5.0%/d). Stary mnoznik (_gdM) liczony dalej
        // ta sama regula i zapisywany w arm_obieg2 jak dotad (nieczytany przy kluczu) - zapis zgodny w obie strony, klucz wylaczony wraca do niego bez skoku pamieci.
        private const double GdNmSlope = 8.0, GdNmMid = 1.5;
        // E3-G1 (noc-ekonomia3 16.10, klucze FolkGuardNormalShareMax i FolkGuardShareFuse). Dowod [L] (ekon2 21-25-46, d109093-109114): nowe towary (jaja, len, opal)
        // podniosly poziom normalny ludu N 5.9-6.8% -> 6.3-7.7% swiata, pasmo poszlo w gore i mnoznik zszedl 5.49 -> 2.01 (33 kroki w dol, potem stoi - w pasmie nie
        // rosnie), lud 8.1 -> 10.8% swiata. (a) do DECYZJI o mnozniku poziom normalny liczy sie najwyzej jako FolkGuardNormalShareMax (6.4% - srednia bazy) zlota
        // swiata; podstawa podatku bez zmian (kazda klasa placi od nadwyzki ponad WLASNY poziom). (b) bezpiecznik: wczorajszy udzial ludu z wsiami (LastShares)
        // > FolkGuardShareFuse - mnoznik x1.03 na dobe takze w pasmie, najwyzej GdFuseCeil (uwaga recenzji 1: bez wlasnego sufitu 40 dob dawalo x3.3); w dol nigdy.
        // To w praktyce cel udzialu ludu 8.0-9.0% swiata (dolna krawedz pasma 1.25 x 6.4%) - zapisane wprost w CHANGELOG. Cien "mnoznik bez bezpiecznika" - sesja.
        // Poprawki po recenzjach (uwaga 2 powazna i 1 drobna): sufit bezpiecznika 5.0 -> 3.7 = mnoznik bazy WGRANIA 64 na koniec 120 dob (3.694) - bezpiecznik to
        // podatek AWARYJNY i nie przebija sily bazy; zwrot ponad baze ma isc z obrotu (myto, czynsz gruntowy, danina markietanow), nie z podatku od oszczednosci.
        // Przy kluczu FolkGuardNoMemory mnoznik doby liczy regula bez pamieci - bezpiecznik wtedy nie dziala (opis MCM).
        private const double GdFuseCeil = 3.7;
        private static double _gdNBand, _gdMNoFuse = double.NaN;
        private static bool _gdFuseToday; private static int _gdFuseDays;
        private static double _gdMNew = double.NaN, _gdRateOld;          // N7: mnoznik nowej reguly tej doby (NaN - klucz wylaczony), stawka starej reguly (porownanie)
        private static bool _gdNmToday, _gdNmBase;                        // N7: stawke doby dala regula bez pamieci; potrzeba nieznana - baza x1
        /// <summary>N7 / N8 (linia NE): podatek od nadwyzki tej doby (miasta, zamki, wsie) - zaplanowany, przy starej stawce, bez zaliczenia pozyczek; zaliczone pozyczki.</summary>
        internal static long N7Plan, N7PlanOld, N8PlanNoCredit, N8CreditUsed; internal static int N8CreditN;
        internal static double N7Mult { get { return _gdMNew; } }
        internal static double N7MultOld { get { return _gdM; } }
        internal static double N7Rate { get { return _gdRate; } }
        internal static double N7RateOld { get { return _gdRateOld; } }
        internal static double N7Ratio { get { return _gdN > 0 ? _gdP / _gdN : double.NaN; } }
        internal static bool N7Today { get { return _gdNmToday && !_gdNmBase; } }
        internal static double N7Min { get { return GdMin; } }
        internal static double N7Max { get { var s = Settings.Current; return s != null ? GdMax(s) : 10.0; } }
        /// <summary>Stawka doby starej reguly (mnoznik z pamiecia; z tym samym sufitem doby) - tylko porownanie N7.</summary>
        private static double HoardRateOld(Settings s) { return GuardOn && _gdReady ? _gdRateOld : HoardRate(s); }

        /// <summary>N7 / N8: podatek doby jednej klaso-osady przy innej stawce / bez zaliczenia (te same granice co pobor: nadwyzka ponad poziom normalny, kiesa). Nie rzuca.</summary>
        private static long HoardAlt(double rate, double room, double pre, long purse)
        {
            double due = rate * (room + pre) - pre;
            if (!(due >= 1)) return 0;
            long x = (long)Math.Floor(Math.Min(due, room));
            return Math.Max(0, Math.Min(x, Math.Max(0, purse)));
        }

        /// <summary>N7 / N8 (tylko liczniki linii NE): zaplanowany podatek klaso-osady `x` (stawka doby, z zaliczeniem `credit`), ten sam przy starej stawce (mnoznik
        /// z pamiecia) i bez zaliczenia pozyczki. `aid` - danina P1 / M4 tej doby (wliczona jak dotad). Nie rzuca.</summary>
        private static void HoardNote(Settings s, double room, long aid, long credit, long purse, long x)
        {
            try
            {
                x = Math.Max(0, x);
                N7Plan += x;
                double pre = Math.Max(0, aid) + Math.Max(0, credit);
                N7PlanOld += _gdNmToday ? HoardAlt(HoardRateOld(s), room, pre, purse) : x;
                if (credit > 0) { N8PlanNoCredit += HoardAlt(HoardRateNow(s), room, Math.Max(0, aid), purse); N8CreditUsed += credit; N8CreditN++; }
                else N8PlanNoCredit += x;
            }
            catch (Exception e) { GuardStumble("HoardNote", e); }
        }

        /// <summary>
        /// korona2 (A, klucz FolkGuardCrownToReserve; test D: czesc korony wplywala w dobie ludu przed CrownIncome.Begin, 165 liczyl ja jako "inne wplywy dnia" i tego
        /// samego dnia oddawal w rentach 180 - inne 42 tys./d bez straznika, 185 tys./d przy czesci korony 129.5 tys./d): czesc korony jest ZAPASEM korony - tak, jak
        /// zamierzal obieg2 ("zapas korony, wydawany jej droga 165/W5b"). Zawsze przy czynnym strazniku i kluczu (bez progu udzialu: bez skokow rent rodow z doby na dobe);
        /// udzial koron reguluje dalej czesc zmienna (FolkGuardCrownTarget). Rownowaga przy czesci 35% i podstawie testu D: ok. 23% zlota swiata.
        /// </summary>
        private static bool CrownToReserveNow
        {
            get
            {
                var s = Settings.Current;
                return s != null && s.FolkGuardCrownToReserve && GuardOn;
            }
        }

        /// <summary>korona2 (A): po wplacie czesci korony do skarbca migawka 165 przesunieta o nia (CrownIncome.ShiftSnapshot): nie wplyw
        /// dnia, wiec nie renty 180 tego dnia; zapas korony oddawany jak caly zapas (1/360 na dobe). Nie rzuca.</summary>
        private static void GuardCrownReserve(Kingdom kd, long cp)
        {
            try
            {
                if (kd == null || cp <= 0 || !CrownToReserveNow) return;
                CrownIncome.ShiftSnapshot(kd, cp);
                _gdResT += cp;
            }
            catch (Exception e) { GuardStumble("GuardCrownReserve", e); }
        }

        /// <summary>Skarbiec korony na czesc korony podatku od nadwyzki: krolestwo rodu pana osady (nie zniszczone). null - straznik wylaczony, udzial 0, rod bez krolestwa.</summary>
        private static Kingdom GuardCrownOf(Clan clan)
        {
            if (clan == null || !GuardOn) return null;
            if (!(GuardCrownShare(Settings.Current) > 0)) return null;
            var k = clan.Kingdom;
            return k != null && !k.IsEliminated ? k : null;
        }

        // ---- stan straznika (zapis arm_obieg2) i pamiec sesji
        private static double _gdM = 1.0, _gdInEma = double.NaN;
        private static long _gdPEnd = long.MinValue;                 // kiesy ludu i wsi na koniec doby ludu (pomiar doplywu; po wczytaniu - od drugiej doby)
        private static bool _gdReady, _gdStartDone;
        private static double _gdRate;
        // ---- liczby doby straznika
        private static double _gdN, _gdP, _gdE, _gdCapAbs, _gdIn = double.NaN, _gdNT, _gdNC, _gdNV, _gdPT, _gdPC, _gdPV, _gdWorldUsed;
        private static int _gdCovT, _gdAllT;
        private static string _gdMove = "-";
        private static long _gdWorld, _gdFolk, _gdLords, _gdCrowns, _gdTicks;
        private static bool _gdShares;
        private static long _gdCrownT; private static int _gdCrownTN;  // czesc korony z miast i zamkow (PayHoard) - doba
        private static long _gdResT;                                    // korona2 (A): czesc korony podatku od nadwyzki do zapasu korony (miasta, zamki, wsie) - doba
        // ---- podatek od nadwyzki wsi (doba)
        private static long _vhTake, _vhLord, _vhTown, _vhCastle, _vhCrown, _vhPlayer, _vhHeld, _vhNoLord, _vhTicks;
        private static int _vhN, _vhUnder, _vhRaided, _vhHeldN, _vhNoBound, _vhBelow, _vhVill, _gdStumbles, _gdStumbleSess;
        private static double _vhGold0, _vhGold1, _vhNorm, _vhE0, _vhE1;
        private static readonly HashSet<Clan> _vhClans = new HashSet<Clan>();
        private static string _lineGuard, _lineGuardStart, _lineVill;
        /// <summary>"Obieg" tej doby: podatek od nadwyzki wsi do glow rodow, kas miast i kas zamkow; czesc korony podatku od nadwyzki (miasta, zamki, wsie).</summary>
        internal static long LastVillHoardLord, LastVillHoardTown, LastVillHoardCastle, LastVillHoardTake, LastHoardCrown;
        /// <summary>korona2 (A): "Obieg" - czesc korony podatku od nadwyzki tej doby, ktora weszla do zapasu korony (nie do wplywow dnia 165).</summary>
        internal static long LastHoardCrownReserve;

        private static void ResetGuard()
        {
            _gdM = 1.0; _gdInEma = double.NaN; _gdPEnd = long.MinValue; _gdReady = false; _gdStartDone = false; _gdRate = 0;
            _gdMNoFuse = double.NaN; _gdFuseDays = 0;                  // E3-G1: cien mnoznika bez bezpiecznika i doby bezpiecznika - sesja
            GuardDayZero();
            _gdStumbleSess = 0;
            _lineGuard = _lineGuardStart = _lineVill = null;
        }

        private static void GuardDayZero()
        {
            _gdN = _gdP = _gdE = _gdCapAbs = _gdNT = _gdNC = _gdNV = _gdPT = _gdPC = _gdPV = _gdWorldUsed = 0; _gdIn = double.NaN;
            _gdNBand = 0; _gdFuseToday = false;                        // E3-G1
            _gdCovT = _gdAllT = 0; _gdMove = "-"; _gdWorld = _gdFolk = _gdLords = _gdCrowns = _gdTicks = 0; _gdShares = false;
            _gdCrownT = 0; _gdCrownTN = 0; _gdResT = 0; LastHoardCrownReserve = 0;
            _gdMNew = double.NaN; _gdRateOld = 0; _gdNmToday = false; _gdNmBase = false; N7Plan = N7PlanOld = N8PlanNoCredit = N8CreditUsed = 0; N8CreditN = 0;   // N7 / N8 (noc 16.10)
            _vhTake = _vhLord = _vhTown = _vhCastle = _vhCrown = _vhPlayer = _vhHeld = _vhNoLord = _vhTicks = 0;
            _vhN = _vhUnder = _vhRaided = _vhHeldN = _vhNoBound = _vhBelow = _vhVill = _gdStumbles = 0;
            _vhGold0 = _vhGold1 = _vhNorm = _vhE0 = _vhE1 = 0;
            _vhClans.Clear();
            LastVillHoardLord = LastVillHoardTown = LastVillHoardCastle = LastVillHoardTake = LastHoardCrown = 0;
        }

        private static void GuardStumble(string where, Exception e)
        {
            _gdStumbles++; _gdStumbleSess++;
            Stumble("Guard." + where, e);
        }

        private static long VillGoldAll()
        {
            long t = 0;
            foreach (var v in Village.All) if (v != null) t += Math.Max(0, v.Gold);
            return t;
        }

        /// <summary>Kiesy ludu i wsi razem (miasta W/R/P, gminy, podzamcza, wsie) - pomiar doplywu i linia doby.</summary>
        private static long FolkAll() { return TotalTown() + TotalCastle() + TotalGmina() + VillGoldAll(); }

        /// <summary>
        /// DailyLive, przed miastami (po HoardBegin): liczniki doby, miara ludu wobec poziomu normalnego (P, N - tylko kiesy ze znana potrzeba), nadwyzka ponad poziom
        /// normalny (sufit doby), doplyw ludu od konca poprzedniej doby ludu (EMA 28 dob), ruch mnoznika i stawka doby. Straznik wylaczony - stawka FolkHoardTaxRate.
        /// </summary>
        private static void GuardBegin()
        {
            GuardDayZero();
            _gdReady = false;
            LnSnapBegin();                                             // N8 (noc 16.10): migawka kies zrodel pozyczek PRZED podatkiem tej doby, zaliczenie wczorajszych pozyczek (TownFolk.Loans.cs)
            var s = Settings.Current;
            if (s == null) return;
            _gdRate = HoardRate(s);
            _gdRateOld = _gdRate;
            if (!s.FolkHoardTax || (!s.FolkCirculationGuard && !s.FolkHoardVillages)) return;
            long t0 = Stopwatch.GetTimestamp();
            try
            {
                double times = HoardTimes(s), rdW = Math.Max(1, Math.Min(60, s.TownFolkReserveDays));
                double pph = Math.Max(0f, Math.Min(1f, s.VillagePurchasePerHead));
                bool vill = s.FolkHoardVillages, castle = CastleLive;
                foreach (var f in _f.Values)
                {
                    if (f == null) continue;
                    if (f.Kind == 0)
                    {
                        _gdAllT++;
                        if (f.Need[0] > 0) _gdCovT++;
                        for (int c = 0; c < 3; c++)
                        {
                            double need = f.Need[c];
                            if (!(need > 0)) continue;
                            double rd = c == 0 ? rdW : c == 1 ? RdR : RdP, nl = times * rd * need, pu = PurseOf(f, c);
                            _gdNT += nl; _gdPT += pu; _gdE += Math.Max(0, pu - nl);
                        }
                    }
                    else if (f.Kind == 1)
                    {
                        if (!castle || !(f.Need[0] > 0)) continue;
                        double nl = times * RdC * f.Need[0];
                        _gdNC += nl; _gdPC += f.KW; _gdE += Math.Max(0, f.KW - nl);
                    }
                    else if (f.Kind == 2 && vill)
                    {
                        var v = f.St != null ? f.St.Village : null;
                        if (!f.Cal || v == null) continue;
                        double nv = pph * VillHeads(f, true);
                        if (!(nv > 0)) continue;
                        double nl = times * RdV * nv;
                        _gdNV += nl; _gdPV += v.Gold; _gdE += Math.Max(0, v.Gold - nl);
                    }
                }
                _gdN = _gdNT + _gdNC + _gdNV; _gdP = _gdPT + _gdPC + _gdPV;
                long folk = FolkAll();
                if (_gdPEnd != long.MinValue)
                {
                    _gdIn = folk - _gdPEnd;
                    _gdInEma = double.IsNaN(_gdInEma) ? _gdIn : _gdInEma + (_gdIn - _gdInEma) / GdEmaDays;
                }
                _gdShares = MoneyLedger.LastShares(out _gdWorld, out _gdFolk, out _gdLords, out _gdCrowns);
                if (!s.FolkCirculationGuard) return;                   // same wsie: stawka FolkHoardTaxRate (jak kiesa2)
                double world = _gdShares ? _gdWorld : MoneyLedger.WorldNow();
                _gdWorldUsed = world;
                double lo = GdLo(s), hi = GdHi(s), mx = GdMax(s);
                // E3-G1 (a): poziom normalny do pasma najwyzej FolkGuardNormalShareMax zlota swiata (0 = bez kapu); (b) bezpiecznik udzialu ludu z wsiami
                double capS = HdClamp(s.FolkGuardNormalShareMax, 0, 0.5), fuse = HdClamp(s.FolkGuardShareFuse, 0, 1);
                double nBand = capS > 0 && world > 0 ? Math.Min(_gdN, capS * world) : _gdN;
                _gdNBand = nBand;
                _gdFuseToday = fuse > 0 && _gdShares && _gdWorld > 0 && (double)_gdFolk / _gdWorld > fuse;
                if (_gdFuseToday) _gdFuseDays++;
                if (double.IsNaN(_gdMNoFuse)) _gdMNoFuse = _gdM;              // cien bez bezpiecznika od stanu z zapisu (sesja)
                if (_gdN > 0 && _gdAllT > 0 && _gdCovT >= GdCover * _gdAllT)
                {
                    bool up = _gdP > hi * nBand, down = _gdP < lo * nBand;
                    if (up) _gdMNoFuse *= 1 + GdStep; else if (down) _gdMNoFuse *= 1 - GdStep;
                    double fc = Math.Min(mx, GdFuseCeil);
                    if (up) { _gdM = _gdM * (1 + GdStep); _gdMove = "w gore"; }
                    else if (_gdFuseToday && _gdM < fc) { _gdM = Math.Min(fc, _gdM * (1 + GdStep)); _gdMove = "w gore (bezpiecznik udzialu ludu)"; }
                    else if (_gdFuseToday) _gdMove = "bez zmian (bezpiecznik udzialu ludu - na suficie " + fc.ToString("0.0", Inv) + ")";
                    else if (down) { _gdM = _gdM * (1 - GdStep); _gdMove = "w dol"; }
                    else _gdMove = "bez zmian (w pasmie)";
                }
                else _gdMove = "wstrzymany (potrzeba ludu znana w " + _gdCovT + " z " + _gdAllT + " miast)";
                if (double.IsNaN(_gdM) || double.IsInfinity(_gdM)) _gdM = 1.0;
                _gdM = Math.Max(GdMin, Math.Min(mx, _gdM));
                if (double.IsNaN(_gdMNoFuse) || double.IsInfinity(_gdMNoFuse)) _gdMNoFuse = _gdM;
                _gdMNoFuse = Math.Max(GdMin, Math.Min(mx, _gdMNoFuse));
                // N7 (klucz FolkGuardNoMemory): mnoznik doby z odchylenia tej doby - bez pamieci; potrzeba znana w < 80% miast - baza (x1, jak bez straznika).
                // Stary mnoznik wyzej liczony dalej (zapis, porownanie). Klucz wylaczony - mUse = _gdM, co do monety jak dotad
                double mUse = _gdM;
                if (s.FolkGuardNoMemory)
                {
                    if (_gdN > 0 && _gdAllT > 0 && _gdCovT >= GdCover * _gdAllT)
                    {
                        double q = _gdP / (nBand > 0 ? nBand : _gdN);    // E3-G1: ten sam poziom do decyzji co pasmo
                        mUse = 1 + GdNmSlope * (q - GdNmMid);
                        if (double.IsNaN(mUse) || double.IsInfinity(mUse)) mUse = 1.0;
                        mUse = Math.Max(GdMin, Math.Min(mx, mUse));
                        _gdMove = "bez pamieci: 1 + " + GdNmSlope.ToString("0", Inv) + " x (" + q.ToString("0.000", Inv) + " - " + GdNmMid.ToString("0.00", Inv) + "), stara regula z pamiecia "
                                  + _gdM.ToString("0.000", Inv) + " " + _gdMove + " - w zapisie, nieczytana";
                    }
                    else
                    {
                        mUse = 1.0; _gdNmBase = true;
                        _gdMove = "bez pamieci: baza x1 - potrzeba ludu znana w " + _gdCovT + " z " + _gdAllT + " miast, stara regula z pamiecia " + _gdM.ToString("0.000", Inv) + " - w zapisie, nieczytana";
                    }
                    _gdMNew = mUse; _gdNmToday = true;
                }
                double r = HoardRate(s) * mUse, rOld = HoardRate(s) * _gdM;
                _gdCapAbs = Math.Max(GdCapWorld * Math.Max(0, world), GdCapInflow * (double.IsNaN(_gdInEma) ? 0 : Math.Max(0, _gdInEma)));
                if (_gdE >= 1 && _gdCapAbs > 0) { r = Math.Min(r, _gdCapAbs / _gdE); rOld = Math.Min(rOld, _gdCapAbs / _gdE); }
                _gdRate = HdClamp(r, 0, 0.1);
                _gdRateOld = HdClamp(rOld, 0, 0.1);
                _gdReady = true;
            }
            catch (Exception e) { GuardStumble("GuardBegin", e); _gdRate = HoardRate(s); _gdReady = false; }
            finally { _gdTicks += Stopwatch.GetTimestamp() - t0; }
        }

        /// <summary>
        /// Villages (po koszyku dnia i zakupach P5 tej wsi): podatek od kiesy wsi ponad poziom normalny (FolkHoardNormalTimes x 15 dni zakupow). `nv` - zakupy dnia
        /// wsi (VillagePurchasePerHead x wolne glowy - ta sama liczba co koszyk). Odbiorcy: skarbiec korony (straznik), glowa rodu pana wsi, kasa osady macierzystej.
        /// Nie rzuca.
        /// </summary>
        private static void PayHoardVillage(Folk f, Village vil, double nv)
        {
            if (!HoardVillOn || f == null || vil == null) return;
            // DS (F1): wies DS - podatek od nadwyzki x (1 - rampa), po rampie nic (wies DS placi tylko daniny przy wozie i czynsz)
            double dsOld = DsVillage.OldShare(f.St);
            if (!(dsOld > 0)) return;
            long t0 = Stopwatch.GetTimestamp();
            try
            {
                var s = Settings.Current;
                if (!(nv > 0)) return;
                double norm = HoardTimes(s) * RdV * nv;
                int gold = vil.Gold;
                _vhVill++; _vhGold0 += gold; _vhNorm += norm;
                double room = gold - norm;
                if (!(room >= 1)) { _vhUnder++; _vhGold1 += gold; return; }
                _vhE0 += room;
                if (vil.VillageState != Village.VillageStates.Normal) { _vhRaided++; _vhE1 += room; _vhGold1 += gold; return; }
                var st = f.St; var bound = vil.Bound;
                if (st == null || bound == null || bound.Town == null) { _vhNoBound++; _vhE1 += room; _vhGold1 += gold; return; }
                // N8 (FolkLoanBeforeGuard): pozyczka z tej kiesy w poprzednim oknie pozyczek zalicza sie na podatek (jak danina w kiesa2) - klucz wylaczony: credit 0, jak dotad
                long credit = LnCreditFor(f.St);
                double due = HoardRateNow(s) * (room + credit) - credit;
                long x = due >= 1 ? (long)Math.Floor(Math.Min(due, room)) : 0;
                x = Math.Min(x, (long)gold);
                if (dsOld < 1.0) x = DsVillage.OldHoard(f.St, x);       // DS (F1): rampa
                HoardNote(s, room, 0, credit, gold, x);                // N7 / N8: liczniki porownania (stara stawka, bez zaliczenia)
                if (x <= 0) { _vhE1 += room; _vhGold1 += gold; return; }
                if (BesiegedPay.Holds(bound, BesiegedPay.KFolkHoard, x, true)) { _vhHeld += x; _vhHeldN++; _vhE1 += room; _vhGold1 += gold; return; }
                var clan = st.OwnerClan;
                var lord = clan != null ? clan.Leader : null;
                bool lordOk = lord != null && lord.IsAlive;
                var kd = GuardCrownOf(clan);
                // przelew: kiesa wsi -> glowa rodu -> kasa osady macierzystej -> skarbiec korony; wyjatek oddaje zloto wsi (nic z niczego, nic w nicosc)
                int had = vil.Gold;
                vil.ChangeGold(-(int)x);
                long got = had - vil.Gold;
                if (got <= 0) { _vhE1 += room; _vhGold1 += vil.Gold; return; }
                x = got;
                long cp = kd != null ? (long)Math.Round(x * GuardCrownShare(s), MidpointRounding.AwayFromZero) : 0;
                if (cp > x) cp = x;
                if (cp < 0) cp = 0;
                long rest = x - cp;
                long lp = lordOk ? (long)Math.Round(rest * HoardLordShare(s), MidpointRounding.AwayFromZero) : 0;
                if (lp > rest) lp = rest;
                if (lp < 0) lp = 0;
                long tp = rest - lp;
                if (lp > 0)
                {
                    try { lord.ChangeHeroGold((int)lp); }
                    catch (Exception e) { vil.ChangeGold((int)x); GuardStumble("PayHoardVillage(pan)", e); _vhE1 += room; _vhGold1 += vil.Gold; return; }
                }
                if (tp > 0)
                {
                    try { bound.Town.ChangeGold((int)tp); }
                    catch (Exception e) { vil.ChangeGold((int)tp); x -= tp; tp = 0; GuardStumble("PayHoardVillage(kasa)", e); }
                }
                if (cp > 0)
                {
                    try { kd.KingdomBudgetWallet += (int)cp; }
                    catch (Exception e) { vil.ChangeGold((int)cp); x -= cp; cp = 0; GuardStumble("PayHoardVillage(korona)", e); }
                    if (cp > 0) GuardCrownReserve(kd, cp);   // korona2 (A): zapas korony, nie wplyw dnia (nie renty 180 tego dnia)
                }
                _vhTake += x; _vhE1 += room - x; _vhGold1 += vil.Gold;
                if (x > 0) { _vhN++; LnNoteTaxed(f.St, x); }          // N8: podatek tej doby z kiesy wsi (pozyczka liczy nadwyzke sprzed niego)
                if (dsOld < 1.0) DsVillage.NoteOldHoard(x);            // DS (F1): stary pobor wsi DS w rampie (licznik)
                try
                {
                    if (lp > 0)
                    {
                        int r0; PopulationLaw.RentToday.TryGetValue(clan, out r0);
                        PopulationLaw.RentToday[clan] = (int)Math.Min(int.MaxValue, (long)r0 + lp);   // dochod ziemi D rodu (jak kiesa2 i podatki panu PayLord)
                        CirculationWindows.NoteHeroGold(lord, lp);
                        _vhLord += lp; _vhClans.Add(clan);
                        if (lord == Hero.MainHero) _vhPlayer += lp;
                    }
                    if (tp > 0) { if (bound.IsTown) _vhTown += tp; else _vhCastle += tp; if (!lordOk) _vhNoLord += tp; }
                    if (cp > 0) _vhCrown += cp;
                    if (vil.Gold + 0.5 < norm) _vhBelow++;                // kontrola: nic ponizej poziomu normalnego (oczekiwane 0)
                }
                catch (Exception e) { GuardStumble("PayHoardVillage(liczniki)", e); }
            }
            catch (Exception e) { GuardStumble("PayHoardVillage", e); }
            finally { _vhTicks += Stopwatch.GetTimestamp() - t0; }
        }

        /// <summary>Dopisek linii kiesa2 (przed "czas"): czesc korony z miast i zamkow i stawka straznika. Klucze straznika i wsi wylaczone - pusty (linia jak w kiesa2).</summary>
        private static string GuardHoardNote()
        {
            var s = Settings.Current;
            if (s == null || !(s.FolkCirculationGuard || s.FolkHoardVillages)) return "";
            return " | obieg2: straznik obiegu " + (GuardOn ? "tak" : "nie") + ", do skarbcow koron z miast i zamkow " + _gdCrownT + " (" + _gdCrownTN
                   + " klaso-osad; poza 'pobrano' wyzej), stawka bazowa " + (100 * HoardRate(s)).ToString("0.00", Inv) + "%/d x mnoznik " + (_gdNmToday ? _gdMNew : _gdM).ToString("0.000", Inv)
                   + " (sufit doby " + N0(_gdCapAbs) + ")";
        }

        /// <summary>Koniec doby ludu (DailyLive, po HoardDay): liczby "Obieg", kiesy na koniec (pomiar doplywu jutro), linia startu sesji (raz), linia straznika i linia wsi.</summary>
        private static void GuardDay(int day)
        {
            var s = Settings.Current;
            if (s == null) return;
            long t0 = Stopwatch.GetTimestamp();
            LastVillHoardLord = _vhLord; LastVillHoardTown = _vhTown; LastVillHoardCastle = _vhCastle; LastVillHoardTake = _vhTake; LastHoardCrown = _gdCrownT + _vhCrown;
            LastHoardCrownReserve = _gdResT;   // korona2 (A)
            long folkEnd = 0;
            try { folkEnd = FolkAll(); _gdPEnd = folkEnd; } catch (Exception e) { GuardStumble("GuardDay(kiesy)", e); }
            double times = HoardTimes(s);
            if (!_gdStartDone)
            {
                _gdStartDone = true;
                _lineGuardStart = "Obieg ludu (obieg2): start sesji | klucze: podatek od nadwyzki wsi (Folk Hoard Villages) " + (s.FolkHoardVillages ? "tak" : "nie")
                                  + ", straznik obiegu (Folk Circulation Guard) " + (s.FolkCirculationGuard ? "tak" : "nie") + ", pozyczki od ludu (Folk Loans) " + (s.FolkLoans ? "tak" : "nie")
                                  + ", cena graniczna wedlug zasobnosci (Folk Ladder Ceiling By Wealth) " + (s.FolkLadderCeilingByWealth ? "tak" : "nie")
                                  + "; podatek od nadwyzki (Folk Hoard Tax) " + (s.FolkHoardTax ? "tak" : "nie") + " (wymagany przez wsie i straznika)"
                                  + " | regula wsi: poziom normalny " + times.ToString("0.0#", Inv) + " x " + RdV.ToString("0", Inv) + " dni zakupow, stawka jak miasta, po koszyku dnia i P5"
                                  + " | straznik: pasmo " + F2(GdLo(s)) + "-" + F2(GdHi(s)) + " x poziom normalny ludu, mnoznik x" + (1 + GdStep).ToString("0.00", Inv) + " / x"
                                  + (1 - GdStep).ToString("0.00", Inv) + " na dobe w granicach " + GdMin.ToString("0.00", Inv) + "-" + F2(GdMax(s)) + ", sufit doby max("
                                  + (100 * GdCapWorld).ToString("0.00", Inv) + "% swiata; " + GdCapInflow.ToString("0", Inv) + " x doplyw ludu EMA " + GdEmaDays.ToString("0", Inv)
                                  + " dob), czesc korony " + (100 * HdClamp(s.FolkGuardCrownShare, 0, 0.5)).ToString("0", Inv) + "%"
                                  + " | N7 regula stawki: " + (s.FolkGuardNoMemory ? "bez pamieci (Folk Guard No Memory tak) - mnoznik = clamp(1 + " + GdNmSlope.ToString("0", Inv) + " x (lud / poziom normalny - "
                                     + GdNmMid.ToString("0.00", Inv) + "), " + GdMin.ToString("0.00", Inv) + ", " + F2(GdMax(s)) + ") co dobe, pasmo i mnoznik z pamiecia tylko do zapisu"
                                     : "z pamiecia (Folk Guard No Memory nie) - pasmo jak dotad")
                                  + " | E3-G1: poziom normalny do pasma najwyzej " + (100 * HdClamp(s.FolkGuardNormalShareMax, 0, 0.5)).ToString("0.0", Inv) + "% swiata (Folk Guard Normal Share Max"
                                     + (s.FolkGuardNormalShareMax > 0 ? "" : " - wylaczony") + "), dzis poziom normalny " + Pct(_gdN, _gdShares ? _gdWorld : 0) + " swiata - kap "
                                     + (_gdNBand > 0 && _gdNBand < _gdN ? "WIAZE" : "nie wiaze") + "; bezpiecznik udzialu ludu z wsiami (Folk Guard Share Fuse) "
                                     + (s.FolkGuardShareFuse > 0 ? (100 * HdClamp(s.FolkGuardShareFuse, 0, 1)).ToString("0.0", Inv) + "% swiata - mnoznik x" + (1 + GdStep).ToString("0.00", Inv) + "/d do " + GdFuseCeil.ToString("0.0", Inv) : "wylaczony")
                                  + " | stan z zapisu: mnoznik " + _gdM.ToString("0.000", Inv) + ", doplyw EMA " + N0(_gdInEma) + LoansStateNote()
                                  + " | klucze wylaczone = e2a422ec.";
            }
            if (!s.FolkHoardTax) { _lineGuard = _lineVill = null; return; }
            try
            {
                if (s.FolkCirculationGuard)
                {
                    double w = _gdShares ? _gdWorld : 0;
                    long hoardT = _hdTake[0] + _hdTake[1] + _hdTake[2] + _hdTake[3];
                    var sb = new StringBuilder(1600);
                    sb.Append("Straznik obiegu (obieg2): dzien ").Append(day)
                      .Append(" | swiat wczoraj ").Append(_gdShares ? _gdWorld.ToString(Inv) : "-")
                      .Append(": lud ").Append(_gdShares ? _gdFolk.ToString(Inv) : "-").Append(" (").Append(Pct(_gdFolk, w)).Append("; kiesy ludu miast, podzamczy, gmin i wsi)")
                      .Append(", lordowie ").Append(_gdShares ? _gdLords.ToString(Inv) : "-").Append(" (").Append(Pct(_gdLords, w)).Append("; glowy rodow, pozostali lordowie, gracz)")
                      .Append(", korony ").Append(_gdShares ? _gdCrowns.ToString(Inv) : "-").Append(" (").Append(Pct(_gdCrowns, w)).Append(')')
                      .Append(" | poziom normalny ludu ").Append(N0(_gdN)).Append(" (").Append(Pct(_gdN, w)).Append(" swiata; miasta ").Append(N0(_gdNT)).Append(" / podzamcza ")
                      .Append(N0(_gdNC)).Append(" / wsie ").Append(N0(_gdNV)).Append("), kiesy z potrzeba ").Append(N0(_gdP)).Append(" (miasta ").Append(N0(_gdPT)).Append(" / podzamcza ")
                      .Append(N0(_gdPC)).Append(" / wsie ").Append(N0(_gdPV)).Append(") = ").Append(_gdN > 0 ? (_gdP / _gdN).ToString("0.000", Inv) : "-").Append(" x poziom normalny")
                      .Append("; pasmo ").Append(F2(GdLo(s))).Append("-").Append(F2(GdHi(s))).Append(" x (").Append(Pct(GdLo(s) * _gdN, w)).Append(" - ").Append(Pct(GdHi(s) * _gdN, w)).Append(" swiata)")
                      .Append(" | E3-G1 do pasma ").Append(N0(_gdNBand)).Append(" (").Append(Pct(_gdNBand, w)).Append(" swiata; najwyzej ").Append((100 * HdClamp(s.FolkGuardNormalShareMax, 0, 0.5)).ToString("0.0", Inv))
                      .Append("%, kap ").Append(_gdNBand > 0 && _gdNBand < _gdN ? "wiaze" : "nie wiaze").Append(") = ").Append(_gdNBand > 0 ? (_gdP / _gdNBand).ToString("0.000", Inv) : "-")
                      .Append(" x; pasmo do decyzji ").Append(Pct(GdLo(s) * _gdNBand, w)).Append(" - ").Append(Pct(GdHi(s) * _gdNBand, w)).Append(" swiata")
                      .Append(" | bezpiecznik udzialu ludu ").Append(s.FolkGuardShareFuse > 0 ? (100 * HdClamp(s.FolkGuardShareFuse, 0, 1)).ToString("0.0", Inv) + "%" : "wylaczony")
                      .Append(": dzis ").Append(_gdFuseToday ? "tak" : "nie").Append(", dob w sesji ").Append(_gdFuseDays).Append(", mnoznik bez bezpiecznika ").Append(double.IsNaN(_gdMNoFuse) ? "-" : _gdMNoFuse.ToString("0.000", Inv))
                      .Append(" (sufit bezpiecznika ").Append(GdFuseCeil.ToString("0.0", Inv)).Append(')')
                      .Append(" | czesc korony teraz ").Append((100 * GuardCrownShare(s)).ToString("0.0", Inv)).Append("% (cel udzialu koron ").Append((100 * HdClamp(s.FolkGuardCrownTarget, 0, 0.5)).ToString("0.0", Inv)).Append("%)").Append(" | mnoznik ").Append((_gdNmToday ? _gdMNew : _gdM).ToString("0.000", Inv)).Append(" (").Append(_gdMove).Append("; granice ").Append(GdMin.ToString("0.00", Inv)).Append("-").Append(F2(GdMax(s))).Append(')')
                      .Append(" | stawka doby ").Append((100 * _gdRate).ToString("0.000", Inv)).Append("%/d (baza ").Append((100 * HoardRate(s)).ToString("0.00", Inv)).Append("% x mnoznik")
                      .Append(", sufit doby ").Append(N0(_gdCapAbs)).Append(" = max(").Append((100 * GdCapWorld).ToString("0.00", Inv)).Append("% swiata ").Append(N0(_gdWorldUsed))
                      .Append("; 2 x doplyw EMA), nadwyzka ponad poziom normalny ").Append(N0(_gdE)).Append(')')
                      .Append(" | doplyw ludu od konca poprzedniej doby ").Append(N0(_gdIn)).Append(", EMA ").Append(N0(_gdInEma))
                      .Append(" | podatek od nadwyzki dzis: miasta i zamki ").Append(hoardT + _gdCrownT).Append(" (kiesa2 ").Append(hoardT).Append(" + korony ").Append(_gdCrownT)
                      .Append("), wsie ").Append(_vhTake).Append(", razem ").Append(hoardT + _gdCrownT + _vhTake).Append("; do glow rodow ").Append(_hdLord + _vhLord)
                      .Append(", do kas miast ").Append(_hdTown + _vhTown).Append(", do kas zamkow ").Append(_hdCastle + _vhCastle).Append(", do skarbcow koron ").Append(_gdCrownT + _vhCrown)
                      .Append(" | kiesy ludu i wsi na koniec doby ").Append(folkEnd)
                      .Append(" | czas ").Append(Ms(_gdTicks + Stopwatch.GetTimestamp() - t0)).Append(" ms | potkniecia ").Append(_gdStumbles).Append(" (sesja ").Append(_gdStumbleSess).Append(").");
                    _lineGuard = sb.ToString();
                }
                else _lineGuard = null;
                if (s.FolkHoardVillages)
                {
                    long diff = _vhTake - _vhLord - _vhTown - _vhCastle - _vhCrown;
                    var sb = new StringBuilder(1200);
                    sb.Append("Podatek od nadwyzki wsi (obieg2): dzien ").Append(day)
                      .Append(" | stawka ").Append((100 * HoardRateNow(s)).ToString("0.000", Inv)).Append("%/d kiesy wsi ponad poziom normalny ").Append(times.ToString("0.0#", Inv))
                      .Append(" x ").Append(RdV.ToString("0", Inv)).Append(" dni zakupow (po koszyku dnia i P5); pan ").Append((100 * HoardLordShare(s)).ToString("0", Inv))
                      .Append("% / kasa osady macierzystej reszty").Append(GuardOn ? ", korona " + (100 * GuardCrownShare(s)).ToString("0", Inv) + "% najpierw" : "")
                      .Append(" | wsi z potrzeba ").Append(_vhVill).Append(", ponizej poziomu normalnego ").Append(_vhUnder).Append(", oplacily ").Append(_vhN).Append(" (rodow ").Append(_vhClans.Count).Append(')')
                      .Append(" | pobrano ").Append(_vhTake).Append(": do glow rodow ").Append(_vhLord).Append(" (w tym gracz ").Append(_vhPlayer).Append("), do kas miast ").Append(_vhTown)
                      .Append(", do kas zamkow ").Append(_vhCastle).Append(" (w tym bez zywego pana ").Append(_vhNoLord).Append("), do skarbcow koron ").Append(_vhCrown)
                      .Append(" | bez poboru: wies w rabunku albo spladrowana ").Append(_vhRaided).Append(", osada macierzysta oblezona ").Append(_vhHeld).Append(" (").Append(_vhHeldN)
                      .Append("), bez osady macierzystej ").Append(_vhNoBound)
                      .Append(" | kiesy wsi przed / po ").Append(N0(_vhGold0)).Append(" / ").Append(N0(_vhGold1)).Append(", poziom normalny ").Append(N0(_vhNorm)).Append(" (kiesy / poziom ")
                      .Append(_vhNorm > 0 ? F2(_vhGold1 / _vhNorm) : "-").Append("), ponad poziom normalny przed / po ").Append(N0(_vhE0)).Append(" / ").Append(N0(_vhE1))
                      .Append(" | kontrola: pobrano - do panow - do kas - do koron ").Append(diff).Append(", ponizej poziomu normalnego po poborze ").Append(_vhBelow).Append(" (oczekiwane 0 / 0)")
                      .Append(" | czas ").Append(Ms(_vhTicks)).Append(" ms | potkniecia ").Append(_gdStumbles).Append(" (sesja ").Append(_gdStumbleSess).Append(").");
                    _lineVill = sb.ToString();
                }
                else _lineVill = null;
            }
            catch (Exception e) { GuardStumble("GuardDay", e); }
        }

        /// <summary>TakeLiveLines: linia startu sesji obieg2, straznika, wsi (i pozyczek - TownFolk.Loans.cs) - zeruje.</summary>
        private static void TakeGuardLines(List<string> l)
        {
            if (_lineGuardStart != null) l.Add(_lineGuardStart);
            if (_lineGuard != null) l.Add(_lineGuard);
            if (_lineVill != null) l.Add(_lineVill);
            if (_lineLoans != null) l.Add(_lineLoans);
            _lineGuardStart = _lineGuard = _lineVill = _lineLoans = null;
        }
    }
}
