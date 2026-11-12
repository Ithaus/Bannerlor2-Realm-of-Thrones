using System;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// 428 (audyt ekonomii 11.11: A3 + A8, decyzja Jeffa 1 "wies"; docs/audyty-2026-11-11/AUDYT-EKONOMIA.md). Cztery klucze, kazdy NIE = kod jak przed 428.
    /// DOWOD (Armoury-2026-11-11_13-19-13.log, linia "Firmy miast i zamkow (DS)" doby 109814): zaleglosci firm miast 237 381 zl u 17 miast, z tego 225 032 to czesc
    /// "z oszczednosci ludu" (Rhyos 52 raty, Pentos 52, Bolozo 32 - lud bez nadwyzki, firma bez drogi w dol); "m105 szybka obnizka ... z firma ponad doplyw 0" przez
    /// cala sesje (pamiec doplywu miast tylko w sesji - pelne 28 dob nigdy nie mija); "osad z firma ponad doplyw 36 (o 19.0 mln zl/rok)"; rachunki wsi 6.87-7.4 mln
    /// u 28-31 miast, splacane 12-20 tys./d.
    ///  A3 (Ds Town Inflow Saved): pamiec doplywu netto kupcow miasta (liczba dob, srednia) i doba ostatniej szybkiej obnizki ida do zapisu arm_daniny_miasta (pola 24-26
    ///     wpisu miasta - te same pola 24-25 co zamki od m107 K3) i wracaja przy pierwszej probce sesji (SampleTown) jako tyle probek sredniej. Szybka obnizka (pelne
    ///     28 dob), obnizka z dochodu, przeglad kwartalny i obnizka przy dlugu rady licza od pierwszych dob sesji, nie od 8. / 29. doby po wczytaniu.
    ///  A8a (Ds Farm Brakes All Towns): miasta BEZ kalibracji KS (StartCalibration.KsTownApplied NIE - ok. 50 z 97) dostaja te same hamulce co miasta z KS: szybka
    ///     obnizka (kasa po naleznosciach ponizej zapasu kupcow albo zaleglosc >= raty - firma z kasy do doplywu netto 28 dob x 364) i obnizka z dochodu (kasa do
    ///     1.5 x zapas albo zaleglosc przez caly cykl - najwyzej Ks Farm Surplus Review Max na cykl), cel przegladu kwartalnego najwyzej caly doplyw (udzial > 1 jak 1).
    ///     Jak zamki: przy doplywie &lt;= 0 bez szybkiej obnizki (firma miasta bez KS nie ma drogi od zera - FromZero to regula miast z KS). Podwyzek wedlug nadwyzki
    ///     i gersumy BRAK (reguly kupcow miast z KS) - w gore prowadzi przeglad kwartalny (+Ds Farm Renew Max, bez zaleglosci).
    ///  A8b (Ds Farm Folk Part Cut): czesc firmy miasta placona z oszczednosci ludu (FLf). W dzien raty lud, ktory nie zaplacil calej naleznosci (brak >= 1 zl - kiesy
    ///     przy zapasie dni), ma te czesc obnizona do 52 x tego, co zaplacil z biezacej raty, najwyzej o Ks Farm Surplus Review Max na rate; zaleglosc tej czesci
    ///     w tym samym stosunku (roszczenie, nie zloto). W gore: przeglad kwartalny (ten sam stosunek co firma z kasy); gdy lud znow ma nadwyzke ponad poziom normalny,
    ///     bierze ja danina od skarbca ludu (Ds Hoard Due liczy FLf jako zaplacone - mniejsza FLf = wieksza danina, nic dwa razy). Zamki: m107 K2 (bez zmian).
    ///  A8c (Town Pays Villages First): KOLEJNOSC WIERZYCIELI kasy miasta - wies przed panem i korona. Dotad w dzien raty (PopulationLaw.Daily) pan i korona brali
    ///     wszystko ponad podloge, a splata rachunkow (KsFoodCredit.PayDaily w SupplyDemand.DailyTrade, pozniej tej samej doby) zastawala kase na podlodze. Teraz z kasy
    ///     ponad podloge najpierw zostaje to, co miasto jest winne wsiom (KsFoodCredit.HoldForVillages), a PayDaily wyplaca z tej kwoty najpierw rachunki wsi. Czego pan
    ///     i korona przez to nie dostali z BIEZACEJ raty firmy, jest DAROWANE (pan bierze z tego, co zostaje po wsi - mniejszy dochod, nie zaleglosc): zaleglosc
    ///     uruchamialaby pozyczke rady miasta na splate pana (TownDebt krok 1 - dlug wobec wsi zamienialby sie w oprocentowany dlug wobec mieszczan i Banku) i trwale
    ///     obnizki firmy (szybka obnizka przy zaleglosci), choc miasto tylko splaca stary rachunek. Stare zaleglosci firmy i pomoc M7 czekaja w ksiedze jak dotad.
    /// NIC Z NICZEGO: A3, A8a, A8b zmieniaja tylko nalezna rate i roszczenia (zloto sie nie rusza). A8c: kasa miasta -> kiesa wsi przez KsFoodCredit.TransferTo (pomiar
    /// obu stron, kontrola zlota i rachunkow linii KS-H jak dotad); kontrola tej linii: wstrzymane - splacone wsiom - niewykorzystane = 0.
    /// B5 (podloga rat 0.5 -> 1.0): klucz juz istnieje (Ds Farm Floor Share) - bez zmiany domyslnej.
    /// 428-p (poprawki po recenzjach 12.11 - nadpisuja opis wyzej tam, gdzie sie roznia):
    ///  A8a: szybka obnizka miast bez KS tylko z kluczem Ds Farm Brakes Fast Cut (domyslnie NIE); miasto bez KS ma droge W GORE - podwyzka do doplywu przy kasie
    ///       ponad 1.5 x zapas (A428Raise, ta sama regula i krok co podwyzka (a) miast z KS, bez gersumy).
    ///  A8b: obnizka dopiero przy trwalym braku (zaleglosc czesci ludu >= 4 raty) i tylko, gdy kiesy byly policzone; sufit sprzed obnizek w zapisie (pole "T")
    ///       i powrot w gore tym samym krokiem, gdy lud placi w calosci i zostaje mu nadwyzka na jeszcze jedna rate.
    ///  A8c: darowanie raty pamietane per miasto - czego wies tej doby z wstrzymania nie dostala, wraca do zaleglosci firmy (kontrola darowania, oczekiwane 0);
    ///       rada miasta nie splaca dlugu wstrzymanym zlotem (TdKeep) i nie liczy go jako pokrycia zaleglosci (TdArrears.cover); VillageOpenOf bez rachunkow,
    ///       ktore splata doby umorzy (wojna, brak wierzyciela).
    /// Linia doby "Hamulce firm i wierzyciele miast (428)". Kazde miasto we wlasnym try wolajacego, potkniecia liczone (Stumble); nic co klatke.
    /// </summary>
    internal static partial class DsTowns
    {
        // A3 (sesja)
        private static int _a3Seed, _a3SeedFull;
        // A8a: obnizki miast bez KS - doba i sesja (szybkie osobno)
        private static int _a8N, _a8FastN, _a8NS, _a8FastNS; private static double _a8Amt, _a8AmtS;
        // A8b: obnizki czesci z oszczednosci ludu - doba i sesja (zl/rok), zaleglosci zmniejszone z firma (zl)
        private static int _a8fN, _a8fNS; private static double _a8fAmt, _a8fAmtS, _a8fArr, _a8fArrS;
        // A8c: czego pan i korona nie dostali z kasy w dzien raty przez pierwszenstwo wsi (zl; w tym pan) - doba i sesja; wstrzymane i splacone w sesji
        private static double _vfLost, _vfLostL, _vfLostS, _vfLostLS, _vfRemit, _vfRemitS; private static long _vfSetS, _vfPaidS;   // _vfRemit: z tego darowane z biezacej raty
        // 428-p (poprawki po recenzji):
        //  A8a - podwyzki miast bez KS do doplywu (doba / sesja), szybkie obnizki wstrzymane kluczem Ds Farm Brakes Fast Cut (doba)
        private static int _a8Up, _a8UpS, _a8FastHeld; private static double _a8UpAmt, _a8UpAmtS;
        //  A8b - raty z brakiem ponizej progu trwalosci, raty bez pomiaru kies ludu, powroty w gore (doba / sesja)
        internal const int FolkCutTerms = 4;                       // obnizka dopiero, gdy zaleglosc czesci ludu po racie >= 4 raty (trwaly brak, nie jeden zly tydzien)
        private static int _a8fWait, _a8fNoMeas, _a8fUp, _a8fUpS; private static double _a8fUpAmt, _a8fUpAmtS;
        private static bool _lfMeasured; private static double _lfRoom;   // FolkToLord: ostatni zwrot byl pomiarem kies; nadwyzka, ktora ludowi zostala
        //  A8c - darowane raty per miasto tej doby [pan, korona] i darowanie cofniete, bo wies nie dostala (doba / sesja)
        private static readonly System.Collections.Generic.Dictionary<Settlement, double[]> _vfTown = new System.Collections.Generic.Dictionary<Settlement, double[]>();
        private static int _vfTownDay = int.MinValue, _vfBackN, _vfBackNS; private static double _vfBack, _vfBackS;

        private static void A428ClearDay()
        {
            _a8N = _a8FastN = 0; _a8Amt = 0; _a8fN = 0; _a8fAmt = 0; _a8fArr = 0; _vfLost = 0; _vfLostL = 0; _vfRemit = 0;
            _a8Up = _a8FastHeld = 0; _a8UpAmt = 0; _a8fWait = _a8fNoMeas = _a8fUp = 0; _a8fUpAmt = 0; _vfBackN = 0; _vfBack = 0;   // 428-p
        }

        private static void A428Reset()
        {
            _a3Seed = _a3SeedFull = 0; _a8NS = _a8FastNS = 0; _a8AmtS = 0; _a8fNS = 0; _a8fAmtS = 0; _a8fArrS = 0; _vfLostS = 0; _vfLostLS = 0; _vfRemitS = 0; _vfSetS = 0; _vfPaidS = 0;
            _a8UpS = 0; _a8UpAmtS = 0; _a8fUpS = 0; _a8fUpAmtS = 0; _vfBackNS = 0; _vfBackS = 0; _vfTown.Clear(); _vfTownDay = int.MinValue; _lfMeasured = false; _lfRoom = 0;   // 428-p
            A428ClearDay();
        }

        /// <summary>A3 (SampleTown, pierwsza probka sesji miasta): pamiec doplywu z zapisu jako NImpN probek sredniej i doba ostatniej szybkiej obnizki. Klucz NIE albo
        /// pamiec sesji juz niepusta - pamiec z zapisu porzucona (jak zamki przy m107 K3). Wolajacy w try (TownDay).</summary>
        private static void A428Seed(Farm f)
        {
            var s = Settings.Current;
            if (s != null && s.DsTownInflowSaved && f.NF == 0 && !double.IsNaN(f.NImpAvg) && !double.IsInfinity(f.NImpAvg))
            {
                int n = Math.Min(NetDays, f.NImpN);
                for (int i = 0; i < n; i++) f.Net[i] = f.NImpAvg;
                f.NF = n; f.NH = n % NetDays;
                if (f.FastDay < 0 && f.NImpFast >= 0 && f.NImpFast <= Today) f.FastDay = f.NImpFast;
                _a3Seed++; if (n >= NetDays) _a3SeedFull++;
            }
            f.NImpN = 0; f.NImpFast = int.MinValue;
        }

        /// <summary>A8a (SurplusReview, miasto bez KS po rampie DS): te same warunki i te same reguly co miasto z KS w galezi "kasa do 1.5 x zapas / zaleglosc"
        /// (FastCut, IncomeCut) - liczone osobno (segment linii 428, nie linia f5-lud). Kasa ponad progiem - cykl obnizki od nowa. Wolajacy w try.</summary>
        private static void A428Brakes(Farm f, Settlement st, Town t, int today, int days, double mx, Settings s, bool fastOn)
        {
            double inst = (f.FL + f.FC + f.FLf) / Weeks;
            bool arrears = f.AL + f.AC + f.ALf + TownDebt.ArrearsLike(st) >= Math.Max(1.0, inst);
            double gq = t.Gold - CofferOwes(f, s);
            int remit = RemitOf(st);
            int reserve = TownPurse.Reserve(t);
            if (!(gq <= OverReserve * reserve || remit != 0 || arrears)) { f.SLow = -1; A428Raise(f, t, today, days, mx); return; }
            f.SG0 = long.MinValue; f.SGDay = -1;                                 // kasa nie ponad progiem - pomiar przyrostu (podwyzka) od nowa, jak miasto z KS
            double F0 = f.FL + f.FC;
            // jak zamek (castle = true): doplyw <= 0 - bez szybkiej obnizki do zera; bez licznikow linii f5-lud.
            // 428-p (recenzja S2): szybka obnizka miast bez KS tylko z kluczem Ds Farm Brakes Fast Cut (domyslnie NIE) - po wgraniu A3 pelne 28 dob pamieci jest od
            // 2. doby sesji i okolo 50 miast zeszloby naraz (CLAUDE.md 8.4); bez klucza zostaje obnizka z dochodu (najwyzej Ks Farm Surplus Review Max na cykl)
            bool fastWant = fastOn && remit == 0 && (gq < reserve || arrears);
            if (fastWant && !s.DsFarmBrakesFastCut) { double nt = NetAvg(f, NetDays); if (!double.IsNaN(nt) && nt > 0 && F0 > nt * YearDays + 1e-6) _a8FastHeld++; }
            bool fast = fastWant && s.DsFarmBrakesFastCut && FastCut(f, today, true);
            double cut = fast ? F0 - (f.FL + f.FC) : IncomeCut(f, today, days, mx, remit != 0, true);
            if (cut > 0 && !double.IsInfinity(cut))
            {
                _a8N++; _a8NS++; _a8Amt += cut; _a8AmtS += cut;
                if (fast) { _a8FastN++; _a8FastNS++; }
            }
        }

        /// <summary>428-p (recenzja S2): PODWYZKA miasta bez KS pod hamulcami - druga strona obnizek, ta sama regula co podwyzka (a) miast z KS (SurplusReview): kasa po
        /// naleznosciach ponad 1.5 x zapas kupcow, bez oblezenia i zaleglosci, przez caly cykl Ks Farm Surplus Review Days - firma pana i korony rosnie o przyrost kasy
        /// na dobe x 364, najwyzej Ks Farm Surplus Review Max x max(firma, doplyw x 364) i najwyzej DO biezacego doplywu netto x 364; udzial firmy w doplywie (Share)
        /// idzie za podwyzka (przeglad kwartalny jej nie cofa). Bez gersumy, bez reguly "domy najpierw" i bez drogi od zera (reguly miast z KS). Pamiec pomiaru
        /// (SG0 / SGDay) sesyjna jak w miastach z KS; przeplywy pozyczek rady koryguja ja ta sama droga (SgShift). Zloto sie nie rusza. Wolajacy w try.</summary>
        private static void A428Raise(Farm f, Town t, int today, int days, double mx)
        {
            long g = t.Gold;
            if (f.SG0 == long.MinValue || f.SGDay < 0) { f.SG0 = g; f.SGDay = today; return; }
            int span = today - f.SGDay;
            if (span < days) return;
            double grow = (g - f.SG0) / (double)span;
            f.SG0 = g; f.SGDay = today;                                          // przyrost od tej doby (bez liczenia drugi raz)
            double F = f.FL + f.FC;
            if (!(F > 0) || !(grow > 0) || !(mx > 0)) return;
            double netA = NetAvg(f, NetMinDays);
            if (double.IsNaN(netA) || !(netA > 0)) return;
            double year = netA * YearDays;
            double add = Math.Min(grow * YearDays, mx * Math.Max(F, year));
            double room = Math.Max(0.0, year - F);
            if (room < add) add = room;
            if (!(add > 0) || double.IsInfinity(add)) return;
            double r = (F + add) / F;
            f.FL *= r; f.FC *= r;
            _a8Up++; _a8UpS++; _a8UpAmt += add; _a8UpAmtS += add;
            double sh = Math.Min(1.0, (f.FL + f.FC) / YearDays / netA);
            double old = !double.IsNaN(f.Share) ? f.Share : _worldShare;
            if (double.IsNaN(old)) old = 0;
            if (old > 1.0) old = 1.0;
            if (sh > old + 1e-9) f.Share = sh;
        }

        /// <summary>A8b (Pay krok 2b, miasto po rampie DS, lud placi z oszczednosci). 428-p (recenzja S1 - zapadka w dol bez drogi w gore):
        ///  W DOL tylko przy TRWALYM braku: lud nie zaplacil calej naleznosci (brak >= 1 zl), kiesy byly POLICZONE (measured - zero techniczne DsFolkRate / wyjatek
        ///  przelewu to nie brak ludu) i zaleglosc tej czesci po racie >= FolkCutTerms (4) raty - wtedy FLf do 52 x zaplaconego z biezacej raty, najwyzej o Ks Farm
        ///  Surplus Review Max na rate; ALf w tym samym stosunku. FLf sprzed pierwszej obnizki zostaje jako sufit (LfTop, w zapisie).
        ///  W GORE: lud zaplacil wszystko (z zalegloscia), po zaplacie zostala mu nadwyzka ponad zapas dni co najmniej na jeszcze jedna rate (room) - FLf rosnie tym
        ///  samym krokiem (Ks Farm Surplus Review Max na rate), najwyzej do sufitu; przy suficie pamiec znika.
        /// Zwraca zmniejszenie zaleglosci (zl - wolajacy nie liczy go jako "splacone").</summary>
        private static double A428FolkCut(Farm f, double iLf, double wantLf, long gF, Settings s, bool measured, double room)
        {
            if (!(f.FLf > 0) || !(iLf > 0)) return 0;
            double mx = Math.Max(0.0, Math.Min(1.0, (double)s.KsFarmSurplusReviewMax));
            if (!(mx > 0)) return 0;
            if (Math.Floor(wantLf) - gF < 1)                                     // zaplacone w calosci (ulamek ponizej 1 zl to nie brak)
            {
                if (!(f.LfTop > f.FLf + 1e-6)) { f.LfTop = 0; return 0; }
                if (!measured || !(room >= iLf)) return 0;
                double up = Math.Min(f.LfTop, f.FLf * (1 + mx)), du = up - f.FLf;
                if (!(du > 0) || double.IsInfinity(du)) return 0;
                f.FLf = up; if (up >= f.LfTop - 1e-6) f.LfTop = 0;
                _a8fUp++; _a8fUpS++; _a8fUpAmt += du; _a8fUpAmtS += du;
                return 0;
            }
            if (!measured) { _a8fNoMeas++; return 0; }
            if (f.ALf < FolkCutTerms * iLf) { _a8fWait++; return 0; }
            double cap = Math.Min(iLf, Math.Max(0L, gF)) * Weeks;                // tyle lud placi na rok w tym tempie
            double nf = Math.Max(cap, (1 - mx) * f.FLf);
            if (!(nf >= 0) || !(nf < f.FLf - 1e-6)) return 0;
            if (!(f.LfTop > f.FLf)) f.LfTop = f.FLf;                             // sufit powrotu = firma sprzed pierwszej obnizki
            double r = nf / f.FLf, d = f.FLf - nf, a0 = f.ALf;
            f.FLf = nf; f.ALf = a0 * r;
            double freed = Math.Max(0, a0 - f.ALf);
            _a8fN++; _a8fNS++; _a8fAmt += d; _a8fAmtS += d; _a8fArr += freed; _a8fArrS += freed;
            return freed;
        }

        /// <summary>A8c (Pay, dzien raty miasta, przed pomoca M7 i firma): wstrzymanie dla wsi z kasy ponad podloge; pomiar tego, czego pan i korona przez to dzis nie
        /// dostali (pomoc M7 + zaleglosc i rata firmy, ktore zmiescilyby sie w kasie bez wstrzymania). remitL / remitC - czesc BIEZACEJ raty pana i korony darowana
        /// (najwyzej to, czego firma przez wstrzymanie nie dostala, i najwyzej cala biezaca rata; w stosunku rat). Zwraca wstrzymane. Wolajacy w try (TownDay).</summary>
        private static long A428Hold(Settlement st, Farm f, Kingdom k, long avail, double iL, double iC, out double remitL, out double remitC)
        {
            remitL = 0; remitC = 0;
            long h = KsFoodCredit.HoldForVillages(st, avail);
            if (h <= 0) return 0;
            if (h > avail) h = avail;
            double aid = f.AidArr >= 1 && k != null && f.AidK == k.StringId ? f.AidArr : 0;
            double wl = f.AL + iL, wc = f.AC + iC, firm = wl + wc;
            double lost = Math.Min(aid + firm, avail) - Math.Min(aid + firm, avail - h);
            double lostFirm = Math.Min(firm, Math.Max(0, avail - aid)) - Math.Min(firm, Math.Max(0, avail - h - aid));
            double lostL = firm > 0 ? Math.Max(0, lostFirm) * wl / firm : 0;
            if (lost > 0 && !double.IsInfinity(lost)) { _vfLost += lost; _vfLostS += lost; _vfLostL += lostL; _vfLostLS += lostL; }
            double inst = Math.Max(0, iL) + Math.Max(0, iC);
            double rem = Math.Min(Math.Max(0, lostFirm), inst);
            if (rem > 0 && inst > 0 && !double.IsInfinity(rem))
            {
                remitL = rem * Math.Max(0, iL) / inst; remitC = rem - remitL;
                _vfRemit += rem; _vfRemitS += rem;
                // 428-p (recenzja 3 / N3): darowanie zapamietane per miasto - czego wies tej doby nie dostanie, wraca do zaleglosci (A428Line)
                if (_vfTownDay != Today) { _vfTown.Clear(); _vfTownDay = Today; }
                double[] v;
                if (!_vfTown.TryGetValue(st, out v)) { v = new double[2]; _vfTown[st] = v; }
                v[0] += remitL; v[1] += remitC;
            }
            return h;
        }

        /// <summary>Linia doby "Hamulce firm i wierzyciele miast (428)" (DsTowns.Daily - po SupplyDemand.DailyTrade tej doby; wszystkie cztery klucze NIE - bez linii).
        /// Tylko odczyt poza odebraniem licznikow wstrzymania (KsFoodCredit.TakeHold); petla po firmach raz na dobe.</summary>
        private static void A428Line(int day, Settings s)
        {
            // 428-p (recenzja 3 / N3): KONTROLA DAROWANIA per miasto - rata pana i korony byla darowana w Pay, zanim wies cokolwiek dostala. Czego splata rachunkow
            // tej doby wsiom z wstrzymania nie wyplacila (kasa zeszla miedzy rata a splata, rachunek przepadl, wyjatek), wraca do zaleglosci firmy w stosunku pan /
            // korona - roszczenie, nie zloto (zloto zostalo w kasie miasta). Oczekiwane 0; liczba > 0 = cos zabiera wstrzymane zloto przed wsia.
            if (_vfTown.Count > 0)
            {
                bool sameDay = _vfTownDay == Today;
                foreach (var kv in _vfTown)
                {
                    try
                    {
                        double rl = Math.Max(0, kv.Value[0]), rc = Math.Max(0, kv.Value[1]), rem = rl + rc;
                        if (!sameDay || !(rem >= 1)) continue;
                        double back = rem - KsFoodCredit.HoldUsedOf(kv.Key, _vfTownDay);
                        if (!(back >= 1) || double.IsInfinity(back)) continue;
                        var f = FarmOf(kv.Key, false);
                        if (f == null) continue;
                        f.AL += back * rl / rem; f.AC += back * rc / rem;
                        _vfBack += back; _vfBackS += back; _vfBackN++; _vfBackNS++;
                    }
                    catch (Exception e) { Stumble("A428Line(darowanie)", e); }
                }
                _vfTown.Clear();
            }
            long hSet, hPaid, hUnused; int hTowns;
            KsFoodCredit.TakeHold(out hSet, out hPaid, out hUnused, out hTowns);
            _vfSetS += hSet; _vfPaidS += hPaid;
            if (!s.DsTownInflowSaved && !s.DsFarmBrakesAllTowns && !s.DsFarmFolkPartCut && !s.TownPaysVillagesFirst) return;
            int nT = 0, full = 0, part = 0, none = 0, noKs = 0, noKsBelow = 0, noKsOver = 0, noKsNoFull = 0, lfArrN = 0;
            double fLf = 0, aLf = 0, noKsF = 0, noKsOverAmt = 0;
            bool capNet = s.KsFarmSurplusCapNet;
            foreach (var f in _farm.Values)
            {
                try
                {
                    var st = f.St;
                    if (st == null || !st.IsTown || st.Town == null || f.Fixed < 0) continue;
                    nT++;
                    if (f.NF >= NetDays) full++; else if (f.NF >= NetMinDays) part++; else none++;
                    fLf += f.FLf; aLf += f.ALf; if (f.ALf >= Math.Max(1.0, f.FLf / Weeks)) lfArrN++;
                    if (StartCalibration.KsTownApplied(st.Town)) continue;
                    noKs++; noKsF += f.FL + f.FC;
                    double gq = st.Town.Gold - (capNet ? CofferOwes(f, s) : 0.0);
                    if (gq < TownPurse.Reserve(st.Town)) noKsBelow++;
                    double net = NetAvg(f, NetDays);
                    if (double.IsNaN(net)) noKsNoFull++;
                    else if (f.FL + f.FC > Math.Max(0.0, net) * YearDays + 1.0) { noKsOver++; noKsOverAmt += f.FL + f.FC - Math.Max(0.0, net) * YearDays; }
                }
                catch (Exception e) { Stumble("A428Line(osada)", e); }
            }
            long vilOpen, vilPaidLast; int vilTowns, vilPaidDay;
            KsFoodCredit.VillageStats(out vilOpen, out vilTowns, out vilPaidLast, out vilPaidDay);
            bool a8On = s.DsFarmBrakesAllTowns && s.KsFarmFollowsSurplus && capNet;
            int lfTopN = 0; double lfTopGap = 0;
            foreach (var f in _farm.Values) if (f.St != null && f.St.IsTown && f.LfTop > f.FLf + 1e-6) { lfTopN++; lfTopGap += f.LfTop - f.FLf; }
            var sb = new StringBuilder(1400);
            sb.Append("Hamulce firm i wierzyciele miast (428): dzien ").Append(day)
              .Append(" | A3 pamiec doplywu miast w zapisie (Ds Town Inflow Saved ").Append(s.DsTownInflowSaved ? "TAK" : "NIE").Append("): odtworzone z zapisu w tej sesji ")
              .Append(_a3Seed).Append(" miast (z pelnymi 28 dobami ").Append(_a3SeedFull).Append("); miast z firma ").Append(nT).Append(": pomiar pelny (28 dob) ").Append(full)
              .Append(", czesciowy (7-27) ").Append(part).Append(", bez pomiaru (< 7) ").Append(none)
              .Append(" | A8 miasta bez KS (Ds Farm Brakes All Towns ").Append(a8On ? "TAK" : s.DsFarmBrakesAllTowns ? "TAK - bez Ks Farm Follows Surplus albo Ks Farm Surplus Cap Net" : "NIE")
              .Append("): miast bez KS ").Append(noKs).Append(" (firmy z kas ").Append(N0(noKsF)).Append(" zl/rok), kasa po naleznosciach ponizej zapasu ").Append(noKsBelow)
              .Append(", z firma ponad doplyw ").Append(noKsOver).Append(" (o ").Append(N0(noKsOverAmt)).Append(" zl/rok), bez pelnego pomiaru ").Append(noKsNoFull)
              .Append("; obnizki dzis ").Append(_a8N).Append(" (szybkie ").Append(_a8FastN).Append("; -").Append(N0(_a8Amt)).Append(" zl/rok), w sesji ").Append(_a8NS)
              .Append(" (szybkie ").Append(_a8FastNS).Append("; -").Append(N0(_a8AmtS)).Append(" zl/rok)")
              .Append("; szybka obnizka miast bez KS (Ds Farm Brakes Fast Cut ").Append(s.DsFarmBrakesFastCut ? "TAK" : "NIE").Append("): wstrzymane kluczem dzis ").Append(_a8FastHeld)
              .Append("; podwyzki do doplywu przy kasie ponad 1.5 x zapas dzis ").Append(_a8Up).Append(" (+").Append(N0(_a8UpAmt)).Append(" zl/rok), w sesji ").Append(_a8UpS)
              .Append(" (+").Append(N0(_a8UpAmtS)).Append(" zl/rok)")
              .Append(" | A8 czesc z oszczednosci ludu (Ds Farm Folk Part Cut ").Append(s.DsFarmFolkPartCut ? (s.DsFarmFolkFromSavings ? "TAK" : "TAK - bez Ds Farm Folk From Savings") : "NIE")
              .Append("): firmy miast z oszczednosci ludu ").Append(N0(fLf)).Append(" zl/rok, zaleglosci ").Append(N0(aLf)).Append(" zl (miast z zalegloscia >= raty ").Append(lfArrN)
              .Append("); obnizki dzis ").Append(_a8fN).Append(" (-").Append(N0(_a8fAmt)).Append(" zl/rok, zaleglosci zmniejszone z firma ").Append(N0(_a8fArr)).Append(" zl), w sesji ")
              .Append(_a8fNS).Append(" (-").Append(N0(_a8fAmtS)).Append(" zl/rok, zaleglosci ").Append(N0(_a8fArrS)).Append(" zl)")
              .Append("; raty z brakiem bez obnizki dzis: zaleglosc ponizej ").Append(FolkCutTerms).Append(" rat (brak nietrwaly) ").Append(_a8fWait).Append(", bez pomiaru kies ludu ").Append(_a8fNoMeas)
              .Append("; powroty w gore dzis ").Append(_a8fUp).Append(" (+").Append(N0(_a8fUpAmt)).Append(" zl/rok), w sesji ").Append(_a8fUpS).Append(" (+").Append(N0(_a8fUpAmtS))
              .Append(" zl/rok); miast ponizej sufitu sprzed obnizek ").Append(lfTopN).Append(" (do odrobienia ").Append(N0(lfTopGap)).Append(" zl/rok)")
              .Append(" | A8 wies przed panem (Town Pays Villages First ").Append(s.TownPaysVillagesFirst ? "TAK" : "NIE").Append("): wstrzymane w dzien raty dla wsi ").Append(hSet)
              .Append(" zl w ").Append(hTowns).Append(" miastach, splacone z tego wsiom ").Append(hPaid).Append(" zl, niewykorzystane ").Append(hUnused)
              .Append(" zl (rachunek licznikow: wstrzymane - splacone - niewykorzystane = ").Append(hSet - hPaid - hUnused)
              .Append("); KONTROLA darowania: raty darowane, ktorych wies nie dostala - cofniete do zaleglosci firm ").Append(N0(_vfBack)).Append(" zl w ").Append(_vfBackN)
              .Append(" miastach (oczekiwane 0; w sesji ").Append(N0(_vfBackS)).Append(" zl, ").Append(_vfBackNS).Append("); pan i korona dostali dzis przez to mniej o ")
              .Append(N0(_vfLost)).Append(" zl (firma i pomoc M7, bez dziesiatej M9 i doplaty urzedu podatkowego; w tym panowie ").Append(N0(_vfLostL)).Append(" zl; z biezacych rat firm darowane ").Append(N0(_vfRemit))
              .Append(" zl, reszta - stare zaleglosci i pomoc M7 - czeka w ksiedze), w sesji ").Append(N0(_vfLostS)).Append(" zl (panowie ")
              .Append(N0(_vfLostLS)).Append(" zl, darowane ").Append(N0(_vfRemitS)).Append(" zl; wstrzymane ").Append(_vfSetS).Append(", splacone wsiom ").Append(_vfPaidS).Append("); rachunki wsi otwarte ").Append(vilOpen)
              .Append(" zl u ").Append(vilTowns).Append(" miast, splacone wsiom w splacie doby ").Append(vilPaidDay == int.MinValue ? "-" : vilPaidDay.ToString(Inv)).Append(": ").Append(vilPaidLast)
              .Append(" zl | skutek dla panow: porownaj \"zaplacone z kas panom\" linii DS, D rodow i zwolnienia w linii budzetu rodow (166)")
              .Append(" | podloga rat (B5, Ds Farm Floor Share) ").Append((s.TownPurseRegulator == 1 ? 1.0 : Unit(s.DsFarmFloorShare)).ToString("0.00", Inv)).Append('.');
            Log.Info(sb.ToString());
        }
    }
}
