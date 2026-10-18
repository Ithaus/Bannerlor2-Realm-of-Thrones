using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;

namespace Armoury
{
    /// <summary>
    /// M1 (pieniadze roku 2, docs/pieniadze-rok-2/PROJEKT-WYBRANY.md 3.1; klucz NeedAidFromRents) - KORONA W WOJNIE PLACI NAJPIERW ZOLD, POTEM RENTY: czwarty
    /// szczebel garnka W5 (KingdomTreasury.NeedAid.cs) PO danine, skarbcu ponad zapas wojny (W5b regula 2) i tarczowym (W6). Dotad reszta wplywow dnia szla tej samej
    /// doby w renty 180 wedlug lenn (CrownRents.Daily), choc panowie tego krolestwa nie mieli na zold (rok 2 biegu dwuletniego: renty krolestw z brakiem ok. 18%
    /// ich wplywow; Z.1 d754-781: 11.4 tys. zl/d przy braku 37.1 tys.).
    ///  - ILE: pula M1 = mniejsze z (niepokryta potrzeba po W5/W5b/W6, reszta wplywow dnia po zwrocie zoldu i po CALYM garnku daniny, najwyzej skarbiec). Poziom wyrownania ta sama
    ///    regula co W5 (garnek = danina + W5b + W6 + M1, od najslabszych, najwyzej do celu).
    ///  - GOTOWKA: zold ludzi, ktorzy dzis SA w druzynach, przy nowym poziomie, ponad gotowke, ktora rod juz dostal tej doby z nizszych szczebli (_naPaid); reszta
    ///    puli to miejsce w pulapie na krok werbunku (bez gotowki, jak W5). Gotowka schodzi z reszty wplywow dnia (CrownIncome.Spent) - renty dziela to, co zostanie.
    ///  - KSIEGA: gotowka M1 jest gotowka W5 (_naCash - poza podstawa zwrotu 70% nastepnej doby; _naPaid - dwor i sprzet ustepuja zoldowi; _naBoost - pulap 166);
    ///    w 165 osobna pozycja (KDay.NeedAidRent - obok doplaty W5, nie w niej: linia W5 "wyplacono" i 165 "doplata W5" to dalej szczebel daniny).
    ///  - W5b regula 1 (WarTaxDaily) i rezerwa wezwan 168 (M2b) czytaja brak SPRZED M1 (NaK.UncovPre) - danina z ludu i wezwania licza jak bez M1; W7 (LvPlan) dostaje
    ///    poziom PO M1 (pan bierze od ludu tylko to, czego korona nie pokryla).
    /// Platnik -> odbiorca: Kingdom.KingdomBudgetWallet -> kiesa glowy rodu z potrzeba W5 (najpierw skarbiec, potem glowa; wyjatek = zloto wraca). Zapis gry: nic nowego.
    /// Wyjatek per krolestwo / per rod - licznik potkniec W5 (CLAUDE.md 7).
    /// </summary>
    internal static partial class KingdomTreasury
    {
        private static int _raNotAtCap;
        // recenzja: gotowka M1 rodu tej doby (kolumna budzet-rodow.csv doplata_m1 - czesc doplata_w5) i rody z potrzeba W5 tej doby (RentAidSplit); czyszczone z NaRoll
        private static readonly Dictionary<Clan, long> _raPaid = new Dictionary<Clan, long>();
        private static readonly HashSet<Clan> _raNeed = new HashSet<Clan>();
        /// <summary>"Obieg" i linia 165: gotowka M1 wyplacona tej doby (skarbce -> glowy rodow, z reszty wplywow dnia przed rentami).</summary>
        internal static long LastNeedAidRent;

        internal static bool RentAidOn { get { var s = Settings.Current; return s != null && s.NeedAidFromRents && NeedAidOn; } }

        /// <summary>
        /// M1 - szczebel reszty wplywow dnia kroku krolestwa W5 (wola NeedAidKingdom PO szczeblach danina / W5b / W6). aidPaid - gotowka szczebla daniny (jeszcze nie
        /// zdjeta z reszty wplywow - ksiegowana w finally kroku W5), scRoom - pula W6, level0 - poziom po ostatnim nizszym szczeblu. Zwraca poziom po M1 i gotowke
        /// (out paid; zaksiegowana tu: Spent, KDay.NeedAid, KDay.NeedAidRent). Nie rzuca.
        /// f20-d1 (KingdomTreasury.AidNeed.cs): plaster rodu liczony od pelnego uprawnienia tej doby (AnBase - nie dopelnia czesci zatrzymanej na nizszych szczeblach),
        /// gotowka = plaster x f; out kept - czesc zatrzymana (zdjeta z reszty wplywow dnia bez przelewu, KDay.NeedAidKept - nie idzie w renty). Klucz wylaczony - jak dotad.
        /// </summary>
        private static double RentAidStep(Kingdom k, List<NaClan> cand, NaK row, double scRoom, long aidPaid, double level0, double target, double step, int today, out long paid, out long kept)
        {
            paid = 0; kept = 0;
            double level1 = level0;
            try
            {
                if (cand.Count == 0 || !(row.UncovPre >= 1)) return level0;
                // recenzja (rachunek dostepnych pieniedzy): (a) reszta wplywow dnia BEZ CALEGO garnka daniny - niewyplacona reszta garnka jest juz w row.Pot, wiec w pot1
                // liczylaby sie drugi raz (zawyzony poziom dla W7); wyplacona czesc garnka (aidPaid) jest jeszcze w d.Left (Spent w finally kroku W5). (b) skarbiec po wyplacie
                // szczebla daniny bez niewyplaconej reszty garnka - aidPaid zeszlo juz ze skarbca (LeftFor - aidPaid odejmowalo je drugi raz, gdy wiazal skarbiec).
                var dk0 = CrownIncome.DayOf(k);
                long incLeft = dk0 != null ? Math.Max(0, dk0.Left - Math.Max(0, row.Pot)) : 0;
                long walLeft = Math.Max(0, (long)k.KingdomBudgetWallet - Math.Max(0, row.Pot - aidPaid));
                long avail = Math.Min(incLeft, walLeft);                                  // reszta wplywow dnia po zwrocie i po calym garnku daniny
                row.RentAvail = avail;
                long room = (long)Math.Floor(Math.Min((double)avail, row.UncovPre));
                row.RentAll = room >= avail;                                              // cala reszta wplywow poszla do puli - dalszego platnika w krolestwie nie ma
                if (room <= 0) return level0;
                row.Rent = room;
                double pot1 = row.Pot + row.Crown + scRoom + room;
                level1 = target;
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
                row.LevelR = level1;
                // plan: gotowka przy nowym poziomie ponad to, co rod juz dostal tej doby (danina, W5b, W6) - razem najwyzej pula
                var extra = new long[cand.Count]; var boost1 = new double[cand.Count];
                long want = 0;
                for (int i = 0; i < cand.Count; i++)
                {
                    double a1, b1; long c1;
                    NaAt(cand[i], level1, step, out a1, out b1, out c1);
                    long had = AnBase(cand[i].C);   // f20-d1: juz dane = pelne uprawnienie (klucz wylaczony - gotowka _naPaid, jak dotad)
                    long e = Math.Max(0, c1 - had);
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
                        long pay = AnPay(x, e);   // f20-d1: wyplata = uprawnienie x f (klucz wylaczony - e)
                        if (pay > 0)
                        {
                            int ip = (int)pay;
                            var h = x.C.Leader;
                            k.KingdomBudgetWallet -= ip;                  // najpierw skarbiec, potem glowa - dostaje dokladnie tyle, ile zeszlo
                            try { h.ChangeHeroGold(ip); } catch { k.KingdomBudgetWallet += ip; throw; }   // wyjatek - zloto wraca do skarbca (nic w nicosc)
                            paid += ip; row.RentPaid += ip; row.RentN++;
                            long cc; _naCash.TryGetValue(x.C, out cc); _naCash[x.C] = cc + ip;   // gotowka W5: nastepny zwrot 70% zdejmuje ja z podstawy
                            _naPaid[x.C] = had + ip;                                             // gotowka korony tej doby razem (ClanBudget.AidCash, CSV doplata_w5)
                            long rp; _raPaid.TryGetValue(x.C, out rp); _raPaid[x.C] = rp + ip;   // recenzja: czesc M1 (CSV doplata_m1)
                            if (x.W <= x.Pc0) _raNotAtCap++;              // kontrola: gotowka tylko rodom, ktorych zold druzyn przekracza pulap wlasny (oczekiwane 0)
                            CirculationWindows.NoteHeroGold(h, ip);       // paczka 169b: glowa poza swiatem (Disabled) - zloto wyszlo ze swiata (tylko licznik)
                            had += ip;
                        }
                        if (e > 0) { long kp = AnNote(x, e, pay, AnTierM); if (kp > 0) { kept += kp; row.RentKept += kp; } }   // f20-d1: zatrzymane - zdjete z wplywow w finally
                        double nb = Math.Max(boost1[i], AnBase(x.C)), ob;   // f20-d1: podloga - pelne uprawnienie (klucz wylaczony - gotowka, jak dotad)
                        _naBoost.TryGetValue(x.C, out ob);
                        if (nb > ob)
                        {
                            _naBoost[x.C] = nb; row.RentBoost += nb - ob;
                            if (!(ob > 0)) NaMeasure(x, today);           // rod bez przydzialu z nizszych szczebli - do pomiaru "rody z doplata dzis" linii W5
                        }
                    }
                    catch (Exception e) { NaStumble("RentAidStep(wyplata)", e); }
                }
            }
            catch (Exception e) { NaStumble("RentAidStep", e); }
            finally
            {
                if (paid > 0)
                {
                    try
                    {
                        CrownIncome.Spent(k, paid);                       // z reszty wplywow dnia - renty 180 dziela to, co zostanie
                        var dk = CrownIncome.DayOf(k);
                        if (dk != null) dk.NeedAidRent += paid;          // osobna pozycja 165 (NeedAid = szczebel daniny - linia W5 "wyplacono", zamkniecie W5-1)
                    }
                    catch (Exception e) { NaStumble("RentAidStep(ksiega)", e); }
                }
                // f20-d1: czesc uprawnien M1 zatrzymana wedlug potrzeby rodu - zdjeta z reszty wplywow dnia BEZ przelewu (zloto zostaje w skarbcu, renty jej nie dziela)
                if (kept > 0)
                {
                    try
                    {
                        CrownIncome.Spent(k, kept);
                        var dk = CrownIncome.DayOf(k);
                        if (dk != null) dk.NeedAidKept += kept;
                    }
                    catch (Exception e) { NaStumble("RentAidStep(ksiega f20)", e); }
                }
            }
            return level1;
        }

        /// <summary>
        /// Recenzja M1 (CrownRents.Daily, zaraz po kroku W5 krolestwa, przed podzialem rent): skad gotowka M1 tej doby - gdyby nie M1, ta kwota poszlaby w renty 180
        /// wedlug wag lenn: rodom z potrzeba W5 (ta sama kiesa - zmiana etykiety renta -> doplata), rodom bez potrzeby (prawdziwy przelew miedzy rodami), udzialy rodow
        /// bez warunku rent (zostalyby w skarbcu; przy Crown Rent Held To Serving - do rodow z warunkiem). Tylko pomiar linii W5. Nie rzuca.
        /// </summary>
        internal static void RentAidSplit(Kingdom k, List<Clan> list, List<double> wts, bool[] ok, double sum, double sumOk, bool heldToServing)
        {
            try
            {
                if (k == null || list == null || wts == null || ok == null || !RentAidOn) return;
                NaK row = null;
                for (int i = _naRows.Count - 1; i >= 0; i--) if (_naRows[i].K == k) { row = _naRows[i]; break; }
                if (row == null || row.RentPaid <= 0) return;
                bool toServing = heldToServing && sumOk > 0 && sumOk < sum - 1e-9;   // ta sama regula co podzial rent (CrownRents.Daily)
                double div = toServing ? sumOk : sum, wNeed = 0, wNo = 0;
                if (!(div > 0)) { row.RentFromHeld = row.RentPaid; return; }
                for (int i = 0; i < list.Count && i < wts.Count && i < ok.Length; i++)
                {
                    if (!ok[i] || list[i] == null) continue;
                    // f7-renty: przy czynnym kluczu renta rodu = udzial x f (ta sama regula co zwrot) - czesc zatrzymana zostalaby w skarbcu (RentFromHeld); wylaczony - f = 1
                    double w = wts[i] * CrownRents.NeedFactor(list[i]);
                    if (_raNeed.Contains(list[i])) wNeed += w; else wNo += w;
                }
                row.RentFromNeed = (long)Math.Round(row.RentPaid * Math.Min(1.0, wNeed / div));
                row.RentFromNo = (long)Math.Round(row.RentPaid * Math.Min(1.0, wNo / div));
                row.RentFromHeld = Math.Max(0, row.RentPaid - row.RentFromNeed - row.RentFromNo);
            }
            catch (Exception e) { NaStumble("RentAidSplit", e); }
        }

        /// <summary>Kolumna budzet-rodow.csv (ostatnia - czytniki po nazwach): czesc gotowki W5 rodu (kolumna doplata_w5), ktora tej doby przyszla z reszty wplywow
        /// dnia (M1) - suma kolumny = "wyplacono" odcinka M1 linii W5 (recenzja: W6b-1 dolicza ja do sumy doplata_w5).</summary>
        internal const string RentAidCsvHeader = ";doplata_m1";

        internal static string RentAidCsv(Clan c)
        {
            try
            {
                long p;
                if (c == null || Campaign.Current == null || _naDay != (int)CampaignTime.Now.ToDays || !_raPaid.TryGetValue(c, out p)) return ";";
                return ";" + p.ToString(WtInv);
            }
            catch { return ";"; }
        }
    }
}
