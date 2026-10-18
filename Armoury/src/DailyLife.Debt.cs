using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Armoury
{
    /// <summary>
    /// f8-niewola - NIEWOLA ZA DLUGI (decyzja Jeffa 18.10 "6 tak"; projekt docs/PROJEKT-ZYCIE-CODZIENNE-2026-10-16.md rozdz. 10: "niewola za dlugi w kulturach
    /// z niewola, 1% na rok przy D < 0.8 bez zapasu"; klucz DebtSlavery, wymaga DailyLifeMeasure i DailyLifeEstates). Czesc przegladu tygodniowego ZC6
    /// (DailyLife.Mobility.cs WeekOne - co dobe 1/7 miast i wsi): przejscie TDebtSl "biedni wolni -> niewolni" tej samej osady.
    ///  KTO: osada (miasto, wies) kultury z niewola, ktora zna pieniadz i dlug - pasma niewolnych SlaveBand / lista LosersFlee bez Zelaznych Wysp (sturgia: Westeros,
    ///   thralle z lupu, nie z dlugu) i bez Dothrakow (khuzait: lud bez pieniadza - dary zamiast handlu i dlugu; ta sama para wyjatkow co pasma miast Wolnych Miast w Bands);
    ///   kultura osady z kalibracji (r.Cul - jak pasma), osada spoza Westeros (r.West). Westeros i kultury bez niewoli - nigdy.
    ///  WARUNKI (wszystkie): KS zastosowany w tej osadzie (wies - StartCalibration.AppliedFor, miasto - Applied i nie kraina wrocona LandBackOf; paczki ekonomii tylko pod
    ///   KS); wladca regionu z kultury z niewola (FreeRuler - ta sama regula co wyzwolenie przez zdobywce: pod wladca bez niewoli nikt nie wpada w niewole, wiec obie
    ///   reguly nie kreca sie w kolko); Dostatek stanu zrodlowego D < 0.8 (srednia 28 dob ZC0; przeglad po rozgrzewce 7 dob jak caly ZC6); zapas kiesy < 7 dni
    ///   zwyklego koszyka stanu (miasto - czesc kiesy W na glowe biedoty i chlopow miasta, wies - kiesa wsi; kiesa nieczynna - bez ruchu, licznik).
    ///  STAN ZRODLOWY: wies - chlopi; miasto - biedota (chlopi miasta 2% - pola mieszczan - bez zmian: maja te sama kiese W i ten sam Dostatek co biedota).
    ///  TEMPO: 1% na rok ludzi stanu zrodlowego = 0.01 x 7/364 na przeglad; w limicie ZC6 0.2% ludzi osady na tydzien (razem z awansem, spadkiem, wyzwoleniami)
    ///   i w granicach pasm (zrodlo >= 0.5 x pasmo, niewolni <= 1.5 x pasmo); ulamek milionowej deterministycznie (RoundPpm - wartosc oczekiwana zachowana).
    ///  LUDZIE: tylko przesuniecie udzialow ksiegi stanow (milionowe, suma 1 000 000) - nikt nie powstaje i nie ginie; ksiega ludzi bez zmian.
    ///  PIENIADZE: miasto - udzial przechodzacych w kiesie W (kiesa W / (biedota + chlopi miasta) x ludzie) do wierzycieli = wlascicieli niewolnych tej osady:
    ///   R 40% / P 60% (ta sama proporcja co racja i praca niewolnych - TownFolk OwnR/OwnP, DailyLife SlaveR), TownFolk.ZcCarry - suma kies ludu bez zmian;
    ///   wies - kiesa wsi jest wspolna (wierzyciele wsi siedza w tej samej kiesie) - bez przelewu. Zadnego nowego zlota.
    ///  DALEJ (istniejace reguly niewolnych, bez zmian): racja niewolnych z dochodu wlascicieli (Dostatek), w KS koszyk niewolnych miasta placa R 40% / P 60% (ZC4),
    ///   rece w polu wsi niewolni x 0.65 (chlopi x 0.5), pola mieszczan niewolni x 0.5; wyzwolenia 0.5%/rok i wyzwolenie przez zdobywce dzialaja na nich jak na
    ///   kazdego niewolnego.
    /// Zapis: bez nowych danych (udzialy w arm_estates jak dotad; liczniki - pamiec sesji). Linia "Niewola za dlugi (f8): dzien N" w dobie linii (przy kluczu),
    /// start sesji w MobilitySessionStart. Wyjatki per osada liczone (bez wylacznika). Progi: sprawdz_logi --grupa nniewola.
    /// </summary>
    internal static partial class DailyLife
    {
        private const string PDS = "Niewola za dlugi (f8): ";
        private const double DslRate = 0.01, DslD = 0.8, DslSavDays = 7.0;
        private static readonly HashSet<string> DslCultures = new HashSet<string>(StringComparer.Ordinal)
            { "ghiscari", "volantine", "lyseni", "myrish", "tyroshi", "qohorik", "norvos", "valyrian", "qartheen" };

        // ------------------------------------------------------------ pamiec sesji (linia - od ostatniej linii; S - od wczytania)
        private static int _dslDays, _dslDaysS, _dslSeen, _dslPoor, _dslNoKs, _dslFreeRuler, _dslRich, _dslSaved, _dslNoPurse, _dslNoD, _dslEmpty, _dslBand, _dslStum;
        private static int _dslMoves, _dslSumBad, _dslBadCul, _dslMovesS, _dslSumBadS, _dslBadCulS, _dslStumS;
        private static double _dslV, _dslT, _dslOut, _dslIn, _dslPoorN, _dslFreeN;
        private static double _dslVS, _dslTS, _dslOutS, _dslInS, _dslPoorNS, _dslFreeNS;
        private static long _dslW, _dslR, _dslP, _dslCtl, _dslWS, _dslRS, _dslPS, _dslCtlS, _dslTicks;
        private static double _dslMax; private static string _dslMaxName = "-";
        private static readonly Dictionary<string, double> _dslCul = new Dictionary<string, double>(StringComparer.Ordinal);
        private static readonly Dictionary<string, double> _dslCulS = new Dictionary<string, double>(StringComparer.Ordinal);

        internal static bool DebtSlOn { get { var s = Settings.Current; return EstatesOn && s.DebtSlavery; } }

        /// <summary>Z ResetMobility (nowa sesja): liczniki niewoli za dlugi od nowa (stan kampanii - udzialy w arm_estates).</summary>
        private static void ResetDebtSl()
        {
            ZeroDebtSlLine();
            _dslDaysS = _dslMovesS = _dslSumBadS = _dslBadCulS = _dslStumS = 0;
            _dslVS = _dslTS = _dslOutS = _dslInS = _dslPoorNS = _dslFreeNS = 0;
            _dslWS = _dslRS = _dslPS = _dslCtlS = 0;
            _dslCulS.Clear();
        }

        private static void ZeroDebtSlLine()
        {
            _dslDays = _dslSeen = _dslPoor = _dslNoKs = _dslFreeRuler = _dslRich = _dslSaved = _dslNoPurse = _dslNoD = _dslEmpty = _dslBand = _dslStum = 0;
            _dslMoves = _dslSumBad = _dslBadCul = 0;
            _dslV = _dslT = _dslOut = _dslIn = _dslPoorN = _dslFreeN = 0;
            _dslW = _dslR = _dslP = _dslCtl = _dslTicks = 0;
            _dslMax = 0; _dslMaxName = "-";
            _dslCul.Clear();
        }

        /// <summary>Osada kultury z niewola znajacej pieniadz i dlug (kultura z kalibracji, poza Westeros).</summary>
        private static bool DslCulture(Rec r) { return r != null && !r.West && r.Cul != null && DslCultures.Contains(r.Cul); }

        /// <summary>KS zastosowany w osadzie: wies - stawki KS (AppliedFor), miasto - KS zastosowany i nie kraina wrocona przez bramke krainy.</summary>
        private static bool DslKs(Rec r)
        {
            if (r.Kind == KVillage) return r.St.Village != null && StartCalibration.AppliedFor(r.St.Village);
            return StartCalibration.Applied && !StartCalibration.LandBackOf(r.St);
        }

        /// <summary>WeekOne (przeglad tygodniowy osady): przeplyw niewoli za dlugi w ludziach na ten przeglad (0 - osada poza regula albo warunek niespelniony;
        /// liczniki powodow). n - ludzie stanow osady przed ruchami; dW / dV - zapas kiesy W miasta / kiesy wsi w dniach zwyklego koszyka (NaN - kiesa nieczynna).</summary>
        private static double DebtSlFlow(Rec r, bool town, double[] n, double dW, double dV)
        {
            if (!DslCulture(r)) return 0;
            long t0 = Stopwatch.GetTimestamp();
            try
            {
                _dslSeen++;
                int f = town ? ELabour : EPeasant;
                double nf = n[f];
                if (!(nf > 0)) { _dslEmpty++; return 0; }
                _dslFreeN += nf; _dslFreeNS += nf;
                if (!DslKs(r)) { _dslNoKs++; return 0; }
                if (FreeRuler(r)) { _dslFreeRuler++; return 0; }
                double d = r.DE[f];
                if (!Fin(d)) { _dslNoD++; return 0; }
                if (d >= DslD) { _dslRich++; return 0; }
                double days = town ? dW : dV;
                if (!Fin(days)) { _dslNoPurse++; return 0; }
                if (days >= DslSavDays) { _dslSaved++; return 0; }
                _dslPoor++; _dslPoorN += nf; _dslPoorNS += nf;
                return DslRate * WeekYear * nf;
            }
            catch (Exception e) { _dslStum++; _dslStumS++; Stumble("NiewolaZaDlugi(przeplyw)", e); return 0; }
            finally { _dslTicks += Stopwatch.GetTimestamp() - t0; }
        }

        /// <summary>Miasto: udzial przechodzacych w kiesie W (x zl gry) do wierzycieli - wlascicieli niewolnych R 40% / P 60% (TownFolk.ZcCarry, najwyzej stan kiesy W).
        /// Kontrola: suma kies ludu miasta przed = po.</summary>
        private static void DebtSlCarry(Rec r, double x)
        {
            long t0 = Stopwatch.GetTimestamp();
            try
            {
                long want = (long)Math.Round(x);
                if (want <= 0) return;
                long wR = (long)Math.Round(want * SlaveR), wP = want - wR;
                long before = PurseSum(r.St);
                long gR = wR > 0 ? TownFolk.ZcCarry(r.St, 0, 1, wR) : 0;
                long gP = wP > 0 ? TownFolk.ZcCarry(r.St, 0, 2, wP) : 0;
                long after = PurseSum(r.St);
                long ctl = Math.Abs(after - before);
                _dslW += gR + gP; _dslR += gR; _dslP += gP; _dslCtl += ctl;
                _dslWS += gR + gP; _dslRS += gR; _dslPS += gP; _dslCtlS += ctl;
            }
            catch (Exception e) { _dslStum++; _dslStumS++; Stumble("NiewolaZaDlugi(kiesa)", e); }
            finally { _dslTicks += Stopwatch.GetTimestamp() - t0; }
        }

        /// <summary>Po przesunieciu udzialow (WeekOne): liczniki ludzi (z wolnych / do niewolnych z roznicy udzialow), tozsamosc sumy udzialow osady, kultura.</summary>
        private static void DebtSlDone(Rec r, bool town, double L, int[] p, int outPpm, int inPpm)
        {
            try
            {
                double o = outPpm * L / PpmAll, i = inPpm * L / PpmAll;
                long sum = 0;
                for (int e = 0; e < 6; e++) sum += p[e];
                if (sum != PpmAll) { _dslSumBad++; _dslSumBadS++; }
                if (!DslCulture(r)) { _dslBadCul++; _dslBadCulS++; }
                _dslMoves++; _dslMovesS++;
                _dslOut += o; _dslOutS += o; _dslIn += i; _dslInS += i;
                if (town) { _dslT += i; _dslTS += i; } else { _dslV += i; _dslVS += i; }
                string k = r.Cul ?? "?";
                double c;
                _dslCul.TryGetValue(k, out c); _dslCul[k] = c + i;
                _dslCulS.TryGetValue(k, out c); _dslCulS[k] = c + i;
                if (L > 0 && i / L > _dslMax) { _dslMax = i / L; _dslMaxName = r.Name ?? r.Id; }
            }
            catch (Exception e) { _dslStum++; _dslStumS++; Stumble("NiewolaZaDlugi(liczniki)", e); }
        }

        // ------------------------------------------------------------ linie
        private static string DslCultureList()
        {
            var l = new List<string>(DslCultures);
            l.Sort(StringComparer.Ordinal);
            return string.Join(", ", l.ToArray());
        }

        private static string DslByCul(Dictionary<string, double> d)
        {
            if (d.Count == 0) return "-";
            var l = new List<KeyValuePair<string, double>>(d);
            l.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
            var sb = new StringBuilder(160);
            for (int i = 0; i < l.Count; i++) { if (i > 0) sb.Append(", "); sb.Append(l[i].Key).Append(' ').Append(N0(l[i].Value)); }
            return sb.ToString();
        }

        /// <summary>MobilitySessionStart: linia startu niewoli za dlugi (klucz, kultury, zasady).</summary>
        private static void DebtSlSessionStart()
        {
            var s = Settings.Current;
            Log.Info(PDS + "start sesji | klucz Debt Slavery " + (s != null && s.DebtSlavery ? "tak" : "WYLACZONE") + " (Daily Life Estates " + (s != null && s.DailyLifeEstates ? "tak" : "WYLACZONE")
                     + ", Daily Life Measure " + (KeyOn ? "tak" : "WYLACZONE") + ")" + (DebtSlOn ? "" : " - nikt nie wpada w niewole za dlugi")
                     + " | kultury: " + DslCultureList() + " (lista z niewola bez sturgia - Zelazne Wyspy, Westeros - i khuzait - Dothrakowie bez pieniadza)"
                     + " | zasada: " + F2(100 * DslRate) + "%/rok stanu zrodlowego (wies chlopi, miasto biedota) przy Dostatku < " + F2(DslD) + " i zapasie kiesy < " + F1(DslSavDays)
                     + " dni zwyklego koszyka -> niewolni tej samej osady; tylko pod KS i pod wladca z niewola; w limicie ZC6 0.2%/tydz. i granicach pasm"
                     + " | pieniadze: miasto - udzial kiesy W do wierzycieli R " + F2(100 * SlaveR) + "% / P " + F2(100 * (1 - SlaveR)) + "%, wies - kiesa wspolna bez przelewu"
                     + " | przeglad co 7 dob na osade (1/7 dziennie, razem z ZC6) | zapis: bez nowych danych (udzialy w arm_estates).");
        }

        /// <summary>Linia doby (doba linii, przy kluczu): przeplyw od ostatniej linii i od wczytania, kultury, tozsamosc ludzi, pieniadze, powody, czas. Zeruje linie.</summary>
        private static string DebtSlLine(int day)
        {
            double moved = _dslV + _dslT, movedS = _dslVS + _dslTS;
            double rateS = _dslPoorNS > 0 ? 100.0 * movedS / _dslPoorNS * (364.0 / 7.0) : double.NaN;    // tempo: przeniesieni / biedni w przegladach x 52 (przeglad = tydzien)
            double freeS = _dslFreeNS > 0 ? 100.0 * movedS / _dslFreeNS * (364.0 / 7.0) : double.NaN;     // na rok jako % wolnych stanu zrodlowego w kulturach z niewola
            double ms = _dslTicks * 1000.0 / Stopwatch.Frequency;
            var sb = new StringBuilder(1600);
            sb.Append(PDS).Append("dzien ").Append(day).Append(" | klucz ").Append(DebtSlOn ? "tak" : "nie")
              .Append(" | od ostatniej linii ").Append(_dslDays).Append(" dob: przegladow osad kultur z niewola ").Append(_dslSeen)
              .Append(" (biednych ").Append(_dslPoor).Append("; pominiete: bez KS ").Append(_dslNoKs).Append(", wladca bez niewoli ").Append(_dslFreeRuler)
              .Append(", D >= ").Append(F2(DslD)).Append(' ').Append(_dslRich).Append(", zapas >= ").Append(F1(DslSavDays)).Append(" dni ").Append(_dslSaved)
              .Append(", zapas nieznany ").Append(_dslNoPurse).Append(", D nieznane ").Append(_dslNoD).Append(", bez ludzi stanu ").Append(_dslEmpty).Append(')')
              .Append(" | do niewoli ").Append(N0(moved)).Append(" ludzi (wsie chlopi ").Append(N0(_dslV)).Append(", miasta biedota ").Append(N0(_dslT)).Append(") w ").Append(_dslMoves)
              .Append(" przegladach, granice pasm / limit ciecia ").Append(_dslBand).Append(", najwiekszy ruch ").Append(F3(100 * _dslMax)).Append("% (").Append(_dslMaxName.Replace('|', '/')).Append(')')
              .Append(" | wedlug kultur: ").Append(DslByCul(_dslCul))
              .Append(" | od wczytania (").Append(_dslDaysS).Append(" dob): do niewoli ").Append(N0(movedS)).Append(" (wsie ").Append(N0(_dslVS)).Append(", miasta ").Append(N0(_dslTS))
              .Append("), biedni w przegladach ").Append(N0(_dslPoorNS)).Append(", tempo ").Append(F3(rateS)).Append("% na rok (cel ").Append(F3(100 * DslRate))
              .Append("%), wolni stanu zrodlowego w przegladach ").Append(N0(_dslFreeNS)).Append(" (na rok ").Append(F3(freeS)).Append("% wolnych)")
              .Append(" | wedlug kultur od wczytania: ").Append(DslByCul(_dslCulS))
              .Append(" | ludzie (ksiega stanow): z wolnych -").Append(F1(_dslOut)).Append(", do niewolnych +").Append(F1(_dslIn)).Append(", roznica ").Append(F1(_dslIn - _dslOut))
              .Append("; od wczytania z wolnych -").Append(F1(_dslOutS)).Append(", do niewolnych +").Append(F1(_dslInS)).Append(", roznica ").Append(F1(_dslInS - _dslOutS))
              .Append("; osad z suma udzialow != 1000000 ").Append(_dslSumBad).Append(" (od wczytania ").Append(_dslSumBadS).Append("), ruchy poza kulturami z niewola ").Append(_dslBadCul)
              .Append(" (od wczytania ").Append(_dslBadCulS).Append(')')
              .Append(" | pieniadze (zl gry): kiesa W -").Append(_dslW).Append(", wierzyciele R +").Append(_dslR).Append(", P +").Append(_dslP).Append(", roznica ").Append(_dslW - _dslR - _dslP)
              .Append(", kontrola kies ludu ").Append(_dslCtl).Append("; od wczytania W -").Append(_dslWS).Append(", R +").Append(_dslRS).Append(", P +").Append(_dslPS)
              .Append(", kontrola ").Append(_dslCtlS).Append(" (wsie: kiesa wspolna - bez przelewu)")
              .Append(" | czas ").Append(F3(ms)).Append(" ms (").Append(F3(_dslDays > 0 ? ms / _dslDays : double.NaN)).Append(" ms/dobe)")
              .Append(" | potkniecia ").Append(_dslStum).Append(" (sesja ").Append(_dslStumS).Append(").");
            ZeroDebtSlLine();
            return sb.ToString();
        }
    }
}
