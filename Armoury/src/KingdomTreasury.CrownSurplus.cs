using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;

namespace Armoury
{
    /// <summary>
    /// W5b regula 2 (decyzja Jeffa 13.10 "zrob poprawke daniny" po pytaniu, czemu Zelazne Wyspy nie maja daniny) - SKARBIEC PONAD ZAPAS WOJNY IDZIE NA DOPLATE:
    /// drugie zrodlo garnka "wedlug potrzeby" (W5, KingdomTreasury.NeedAid.cs), PO daninie P1 i PRZED tarczowym (W6, KingdomTreasury.Scutage.cs) - rody placa
    /// dopiero, gdy korona wylozyla swoje. Wylacznik CrownSurplusFeedsNeedAid (wylaczony = stan sprzed paczki co do monety), tempo CrownSurplusAidDays (30).
    /// Czynne tylko z W5 (CrownWarAidByNeed, renty 180, budzet 166) i 165 (dzien korony); danina P1 NIE jest potrzebna.
    ///  - KIEDY: krolestwo w wojnie ma niepokryta potrzebe W5 po garnku daniny tej doby (potrzeba rodow z warunkiem rent minus garnek).
    ///  - Z CZEGO: NADWYZKA = skarbiec minus reszta wplywow dnia (pojdzie dzis w doplate z daniny i renty 180 - to nie zapas) minus ZAPAS WOJNY (ta sama liczba
    ///    co regula 184: wieksze z CrownReserveGold 500 000 i WarTaxReserveDays 60 dob sredniego naleznego zwrotu zoldu z 28 dob) minus dlug korony w Banku
    ///    (186 - zloto winne Bankowi nie jest nadwyzka korony: NIGDY Z KREDYTU). Liczona raz, przed jakimkolwiek zlotem kroku W5 tej doby.
    ///  - ILE: PULA = mniejsze z (nadwyzka / CrownSurplusAidDays, niepokryta potrzeba po daninie). Skarbiec po wyplacie zostaje wiec co najmniej o
    ///    (1 - 1/30) nadwyzki ponad zapasem wojny i dlugiem - NIGDY PONIZEJ ZAPASU WOJNY (kontrola w linii, oczekiwane 0).
    ///  - JAK: garnek z pula = danina + pula -> nowy poziom wyrownania W5 (ta sama regula: od najslabszych, najwyzej do celu). GOTOWKA = to, co W5 wyplaca
    ///    przy nowym poziomie PONAD szczebel daniny (tylko za ludzi, ktorzy dzis sa w druzynach) - skarbiec -> kiesa glowy rodu; reszta puli to MIEJSCE w
    ///    pulapie na krok werbunku (bez gotowki, jak w W5). Niewyplacona czesc puli zostaje w skarbcu.
    ///  - KSIEGA: gotowka jest gotowka W5 (znaczona na druzyny - przydzial w pulapie, nie w D; poza podstawa zwrotu 70% nastepnej doby; poza zoldem, ktoremu
    ///    ustepuje dwor), ale idzie z ZAPASU, nie z wplywow dnia: nie schodzi z reszty wplywow (CrownIncome.Spent NIE jest wolane), wiec renty 180 dziela to
    ///    samo co bez paczki. Osobna pozycja w "Korona: wplywy dnia (165)" (KDay.CrownAid) i w "Obieg".
    ///  - PO NIM: tarczowe liczy niepokryta potrzebe po daninie I po puli skarbca (bierze od rodow mniej); regula 1 (brak wojska u panow, KingdomTreasury.WarTax.cs)
    ///    liczy to, czego nie pokrylo zadne z trzech zrodel.
    /// Platnik -> odbiorca: Kingdom.KingdomBudgetWallet -> kiesa glowy rodu z potrzeba W5 (najpierw skarbiec, potem glowa; wyjatek = zloto wraca). Zapis gry: nic
    /// nowego (stan doby w pamieci sesji). Linia: dopiski w "Doplata wedlug potrzeby (W5)"; kolumna budzet-rodow.csv doplata_ze_skarbca_w5b.
    /// Wyjatek per krolestwo / per rod - licznik potkniec (pierwszy do pliku), bez globalnych wylacznikow (CLAUDE.md 7).
    /// f20-d1 (KingdomTreasury.AidNeed.cs): gotowka tego szczebla = uprawnienie x f rodu; zatrzymane nie wychodzi ze skarbca (pula i budzet W5c bez zmian).
    /// </summary>
    internal static partial class KingdomTreasury
    {
        private static readonly Dictionary<Clan, long> _csCash = new Dictionary<Clan, long>();   // odbiorca -> gotowka W5 ze skarbca ponad zapas wojny tej doby (CSV)
        private static readonly HashSet<string> _csErr = new HashSet<string>();
        private static int _csStumbles, _csBelow, _csNotAtCap, _csDebtK;
        /// <summary>"Obieg" (korona i rody): gotowka doplaty W5 wyplacona w tej dobie ze skarbcow ponad zapas wojny (skarbce -> glowy rodow, poza wplywami dnia).</summary>
        internal static long LastCrownSurplusAid;

        internal static bool CrownSurplusOn { get { var s = Settings.Current; return s != null && s.CrownSurplusFeedsNeedAid && NeedAidOn; } }

        private static double CsDays(Settings s) { return Math.Max(1, s.CrownSurplusAidDays); }

        private static void CrownSurplusReset()
        {
            CsClearDay(); _csErr.Clear(); _csStumbles = 0; LastCrownSurplusAid = 0;
        }

        /// <summary>Nowa doba kroku W5 (NaRoll): stan doby znika razem z przydzialami W5.</summary>
        private static void CsClearDay()
        {
            _csCash.Clear(); _csBelow = _csNotAtCap = _csDebtK = 0;
        }

        private static void CsStumble(string where, Exception e)
        {
            _csStumbles++;
            try { if (_csErr.Add(where)) Log.Error("KingdomTreasury.CrownSurplus." + where, e); } catch { }
        }

        /// <summary>Zapas wojny krolestwa - ta sama liczba co w regule 184 (WarTaxDaily): wieksze z CrownReserveGold i WarTaxReserveDays x sredni nalezny zwrot
        /// zoldu z 28 dob (pierscien 184; tu stan do wczoraj - krok 184 dopisuje dzisiejsza dobe pozniej). Krolestwo bez pierscienia - sama podloga.
        /// f6-zwrot (recenzja): pierscien niesie PELNY zwrot (KDay.RefundFull, przed mnoznikiem potrzeby rodu) - zapas wojny i jego czytelnicy jak przed f6.
        /// f23-korona (klucz War Reserve By Need): sredni pelny zwrot x udzial naleznego po f6 w pelnym z dob pierscienia DueN (WtNeedShare) - przy 28 dobach to
        /// dokladnie sredni nalezny zwrot; DueN i FN sa w zapisie (recenzja), wiec po wczytaniu udzial jak przed zapisem (zapis sprzed recenzji: pierwsza doba udzial 1 -
        /// zapas jak z pelnego). Jedna liczba dla wszystkich czytelnikow (184, W5b, W5c, K5, DS,
        /// L5b-1, korona2). full = true - zapas z pelnego zwrotu (tylko do linii f23: o ile zapas wedlug potrzeby jest mniejszy).</summary>
        private static long WarReserveOf(Kingdom k, Settings s, bool full = false)
        {
            long floor = Math.Max(0, s.CrownReserveGold);
            var w = WtOf(k, false);
            if (w == null || w.F28 <= 0) return floor;
            long dsum = 0;
            for (int i = 0; i < WtRing28; i++) dsum += w.Due[i];
            double share = !full && WarReserveByNeedOn ? WtNeedShare(w) : 1.0;
            return Math.Max(floor, (long)(WtClamp(s.WarTaxReserveDays, 0, 728) * ((double)dsum / w.F28) * share));
        }

        /// <summary>f23-korona: klucz War Reserve By Need (zapas wojny z naleznego zwrotu po mierze potrzeby rodu). Bez zwrotu wedlug potrzeby (Crown Refund By Need
        /// wylaczony) nalezny = pelny - udzial 1, zapas co do zlotowki jak dotad.</summary>
        internal static bool WarReserveByNeedOn { get { var s = Settings.Current; return s != null && s.WarReserveByNeed; } }

        /// <summary>f23-korona: udzial naleznego zwrotu (KDay.RefundDue - po mierze potrzeby rodu f6) w pelnym (KDay.RefundFull) z dob pierscienia DueN (ostatnie FN miejsc
        /// przed glowa H28 - te same doby w obu pierscieniach; DueN i FN w zapisie od recenzji). 1 - brak dob DueN (zapis sprzed recenzji, pierwsza doba) albo bez zwrotu
        /// w tych dobach (zapas jak z pelnego). 0..1.</summary>
        private static double WtNeedShare(WarTaxK w)
        {
            if (w == null || w.FN <= 0) return 1.0;
            long sn = 0, sf = 0;
            int n = Math.Min(w.FN, WtRing28);
            for (int j = 1; j <= n; j++) { int i = (w.H28 - j + WtRing28 * 2) % WtRing28; sn += w.DueN[i]; sf += w.Due[i]; }
            if (sf <= 0) return 1.0;
            return Math.Max(0.0, Math.Min(1.0, (double)sn / sf));
        }

        /// <summary>f23-korona (linia "Korona: najpierw skarbiec (f23)"): zapas wojny z PELNEGO zwrotu - jak przed paczka (porownanie). -1 = nieczytelny.</summary>
        internal static long WarReserveFullFor(Kingdom k)
        {
            try { var s = Settings.Current; return s != null && k != null ? WarReserveOf(k, s, true) : -1; }
            catch (Exception e) { CsStumble("WarReserveFullFor", e); return -1; }
        }

        /// <summary>f23-korona (DsSeason - "najpierw skarbiec", klucz Ds War Grant Treasury First): nadwyzka skarbca krolestwa ponad zapas wojny - TA SAMA miara co pula W5b
        /// (CrownSurplusPlan): skarbiec - reszta wplywow dnia (pojdzie dzis na wydatki dnia) - zapas wojny - dlug w Banku (186) - dlug u miast (korona2); nigdy z kredytu.
        /// reserve - zapas wojny (WarReserveOf). Zwraca nadwyzke >= 0 (0 - brak albo nieczytelne). Tylko odczyt.</summary>
        internal static double TreasurySurplusFor(Kingdom k, out long reserve)
        {
            reserve = -1;
            try
            {
                var s = Settings.Current;
                if (s == null || k == null) return 0;
                reserve = WarReserveOf(k, s);
                double sur = Math.Floor((double)k.KingdomBudgetWallet - CrownIncome.LeftFor(k) - reserve - CrownBorrow.DebtOf(k) - TownFolk.CrownTownDebt(k));
                return sur >= 1 ? sur : 0;
            }
            catch (Exception e) { CsStumble("TreasurySurplusFor", e); return 0; }
        }

        /// <summary>L5b-1 (skarbiec stolicy, CapitalTreasury): zapas wojny krolestwa - ta sama liczba co W5b i 184 (tylko odczyt). -1 = nieczytelny (wolajacy nic nie bierze).</summary>
        internal static long WarReserveFor(Kingdom k)
        {
            try { var s = Settings.Current; return s != null && k != null ? WarReserveOf(k, s) : -1; }
            catch (Exception e) { CsStumble("WarReserveFor", e); return -1; }
        }

        /// <summary>
        /// Pula ze skarbca ponad zapas wojny dla krolestwa tej doby - SAM RACHUNEK, bez zlota. Wola NeedAidKingdom po policzeniu potrzeby i garnka daniny, przed
        /// jakakolwiek wyplata (skarbiec i reszta wplywow dnia sa wtedy jeszcze nieruszone przez krok W5). Zapisuje w wierszu zapas wojny i nadwyzke.
        /// 0: klucz wylaczony, bez rodow z potrzeba, bez niepokrytej potrzeby po daninie, bez nadwyzki. Nie rzuca.
        /// </summary>
        private static long CrownSurplusPlan(Kingdom k, Settings s, NaK row, int needClans)
        {
            try
            {
                if (!CrownSurplusOn || k == null || row == null) return 0;
                long reserve = WarReserveOf(k, s);
                row.CrownRes = reserve;
                long wallet = k.KingdomBudgetWallet;
                long left = CrownIncome.LeftFor(k);                       // reszta wplywow dnia: pojdzie dzis w doplate z daniny i renty 180 - to nie zapas
                double free = (double)wallet - left - reserve;
                double sur = Math.Floor(free - CrownBorrow.DebtOf(k) - TownFolk.CrownTownDebt(k));    // nigdy z kredytu: zloto winne Bankowi (186) i miastom (korona2) nie jest nadwyzka korony
                if (!(sur >= 1)) { if (free >= 1) _csDebtK++; return 0; }
                row.CrownSur = (long)sur;
                double unc = row.Need - row.Pot;
                if (needClans <= 0 || !(unc >= 1)) return 0;
                long room = (long)Math.Floor(Math.Min(sur / CsDays(s), unc));
                return room > 0 ? room : 0;
            }
            catch (Exception e) { CsStumble("CrownSurplusPlan", e); return 0; }
        }

        /// <summary>
        /// Szczebel skarbca kroku krolestwa W5 (wola NeedAidKingdom PO petli wyplaty szczebla daniny, PRZED tarczowym): garnek danina + pula -> poziom wyrownania
        /// -> wyplata gotowki, ktora W5 daje przy nowym poziomie ponad szczebel daniny (skarbiec -> glowa, ta sama para co W5) i przydzial w pulapie
        /// (gotowka + miejsce na krok werbunku). cand - rody z potrzeba W5 (te same obiekty co w kroku W5), level0 - poziom szczebla daniny, aidPaid - gotowka
        /// szczebla daniny (do kontroli zapasu wojny). Zwraca poziom po tym szczeblu (level0, gdy nic nie zrobil). Nie rzuca; ksiegowanie w finally.
        /// </summary>
        private static double CrownSurplusStep(Kingdom k, List<NaClan> cand, NaK row, long room, long aidPaid, double level0, double target, double step, int today)
        {
            long paid = 0, wallet0 = 0, left0 = 0;
            double level1 = level0;
            try
            {
                if (room <= 0 || cand.Count == 0) return level0;
                wallet0 = (long)k.KingdomBudgetWallet + aidPaid;          // skarbiec sprzed kroku W5 (szczebel daniny zdjal juz aidPaid)
                left0 = Math.Max(0, CrownIncome.LeftFor(k));              // reszta wplywow dnia jeszcze bez odjecia doplaty z daniny (ksiegowana w finally kroku W5)
                row.Crown = room; row.LevelC = level0;
                // poziom wyrownania z garnkiem danina + pula skarbca: ta sama regula co W5 (najwyzszy f <= cel, przy ktorym suma kosztow miesci sie w garnku)
                double pot1 = row.Pot + room; level1 = target;
                if (row.Need > pot1)
                {
                    double lo = 0, hi = target;
                    for (int it = 0; it < 32; it++)
                    {
                        double mid = 0.5 * (lo + hi), sum = 0;
                        for (int i = 0; i < cand.Count; i++) sum += NaCost(cand[i], mid);
                        if (sum <= pot1) lo = mid; else hi = mid;
                    }
                    level1 = lo;
                }
                if (level1 < level0) level1 = level0;
                row.LevelC = level1;
                // plan: gotowka rodu przy nowym poziomie ponad gotowke szczebla daniny (tylko za ludzi, ktorzy dzis sa) - razem najwyzej pula
                var extra = new long[cand.Count]; var boost1 = new double[cand.Count];
                long want = 0;
                for (int i = 0; i < cand.Count; i++)
                {
                    double a0, b0, a1, b1; long c0, c1;
                    NaAt(cand[i], level0, step, out a0, out b0, out c0);
                    NaAt(cand[i], level1, step, out a1, out b1, out c1);
                    long e = Math.Max(0, c1 - c0);
                    if (e > room - want) e = room - want;
                    extra[i] = e; want += e; boost1[i] = b1;
                }
                for (int i = 0; i < cand.Count; i++)
                {
                    var x = cand[i];
                    try
                    {
                        long had; _naPaid.TryGetValue(x.C, out had);
                        long e = extra[i];
                        long wallet = Math.Max(0, (long)k.KingdomBudgetWallet);
                        if (e > wallet) e = wallet;
                        if (e > int.MaxValue) e = int.MaxValue;
                        // f20-d1: e = uprawnienie rodu na tym szczeblu (rachunek jak dotad); wyplata = e x f (klucz wylaczony - e). Zatrzymane po prostu nie wychodzi ze
                        // skarbca (szczebel poza wplywami dnia - bez Spent); pula i budzet W5c bez zmian
                        long pay = AnPay(x, e);
                        if (pay > 0)
                        {
                            int ip = (int)pay;
                            var h = x.C.Leader;
                            k.KingdomBudgetWallet -= ip;                  // najpierw skarbiec, potem glowa - dostaje dokladnie tyle, ile zeszlo
                            try { h.ChangeHeroGold(ip); } catch { k.KingdomBudgetWallet += ip; throw; }   // wyjatek - zloto wraca do skarbca (nic w nicosc)
                            paid += ip; row.CrownPaid += ip; row.CrownN++;
                            long cc; _naCash.TryGetValue(x.C, out cc); _naCash[x.C] = cc + ip;   // gotowka W5: nastepny zwrot 70% zdejmuje ja z podstawy
                            _naPaid[x.C] = had + ip;                                             // gotowka korony tej doby razem (ClanBudget.AidCash, CSV doplata_w5)
                            long sp; _csCash.TryGetValue(x.C, out sp); _csCash[x.C] = sp + ip;
                            if (x.W <= x.Pc0) _csNotAtCap++;              // kontrola: gotowka tylko rodom, ktorych zold druzyn przekracza pulap wlasny (oczekiwane 0)
                            CirculationWindows.NoteHeroGold(h, ip);       // paczka 169b: glowa poza swiatem (Disabled) - zloto wyszlo ze swiata (tylko licznik)
                            had += ip;
                        }
                        if (e > 0) { long kept = AnNote(x, e, pay, AnTierB); if (kept > 0) row.CrownKept += kept; }   // f20-d1: uprawnienie i gotowka do pamieci doby
                        // przydzial w pulapie przy nowym poziomie (gotowka + miejsce na krok werbunku), nigdy ponizej gotowki; tylko gdy rosnie
                        // f20-d1: podloga - pelne uprawnienie (AnBase; klucz wylaczony - gotowka, jak dotad): przydzial co do zlotowki jak bez paczki
                        double nb = Math.Max(boost1[i], AnBase(x.C)), ob;
                        _naBoost.TryGetValue(x.C, out ob);
                        if (nb > ob)
                        {
                            _naBoost[x.C] = nb; row.CrownBoost += nb - ob;
                            if (!(ob > 0)) NaMeasure(x, today);           // rod bez przydzialu ze szczebla daniny - do pomiaru "rody z doplata dzis" linii W5
                        }
                    }
                    catch (Exception e) { CsStumble("CrownSurplusStep(wyplata)", e); }
                }
            }
            catch (Exception e) { CsStumble("CrownSurplusStep", e); }
            finally
            {
                if (paid > 0)
                {
                    try
                    {
                        var dk = CrownIncome.DayOf(k);
                        if (dk != null) dk.CrownAid += paid;              // poza wplywami dnia (Spend / Left) - z zapasu skarbca
                        // kontrola niezalezna od rachunku puli: skarbiec bez reszty wplywow dnia (ta pojdzie jeszcze w renty 180) nie ponizej zapasu wojny
                        long stock = (long)k.KingdomBudgetWallet - Math.Max(0, Math.Min(left0, wallet0) - aidPaid);
                        if (stock < row.CrownRes) _csBelow++;
                    }
                    catch (Exception e) { CsStumble("CrownSurplusStep(ksiega)", e); }
                }
            }
            return level1;
        }

        /// <summary>Kolumna budzet-rodow.csv (ostatnia - czytniki po nazwach): czesc gotowki W5 rodu (kolumna doplata_w5), ktora tej doby przyszla ze
        /// skarbca ponad zapas wojny - suma kolumny = "wyplacono ze skarbca" z linii W5, niezaleznie od linii.</summary>
        internal const string CrownSurplusCsvHeader = ";doplata_ze_skarbca_w5b";

        internal static string CrownSurplusCsv(Clan c)
        {
            try
            {
                long p;
                if (c == null || Campaign.Current == null || _naDay != (int)CampaignTime.Now.ToDays || !_csCash.TryGetValue(c, out p)) return ";";
                return ";" + p.ToString(WtInv);
            }
            catch { return ";"; }
        }
    }
}
