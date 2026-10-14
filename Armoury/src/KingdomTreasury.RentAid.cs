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
    ///  - ILE: pula M1 = mniejsze z (niepokryta potrzeba po W5/W5b/W6, reszta wplywow dnia po zwrocie zoldu i po wyplacie szczebla daniny). Poziom wyrownania ta sama
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
        /// <summary>"Obieg" i linia 165: gotowka M1 wyplacona tej doby (skarbce -> glowy rodow, z reszty wplywow dnia przed rentami).</summary>
        internal static long LastNeedAidRent;

        internal static bool RentAidOn { get { var s = Settings.Current; return s != null && s.NeedAidFromRents && NeedAidOn; } }

        /// <summary>
        /// M1 - szczebel reszty wplywow dnia kroku krolestwa W5 (wola NeedAidKingdom PO szczeblach danina / W5b / W6). aidPaid - gotowka szczebla daniny (jeszcze nie
        /// zdjeta z reszty wplywow - ksiegowana w finally kroku W5), scRoom - pula W6, level0 - poziom po ostatnim nizszym szczeblu. Zwraca poziom po M1 i gotowke
        /// (out paid; zaksiegowana tu: Spent, KDay.NeedAid, KDay.NeedAidRent). Nie rzuca.
        /// </summary>
        private static double RentAidStep(Kingdom k, List<NaClan> cand, NaK row, double scRoom, long aidPaid, double level0, double target, double step, int today, out long paid)
        {
            paid = 0;
            double level1 = level0;
            try
            {
                if (cand.Count == 0 || !(row.UncovPre >= 1)) return level0;
                long avail = Math.Max(0, CrownIncome.LeftFor(k) - aidPaid);              // reszta wplywow dnia po zwrocie i po szczeblu daniny
                long wallet0 = Math.Max(0, (long)k.KingdomBudgetWallet);
                if (avail > wallet0) avail = wallet0;
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
                    double a1, b1; long c1, had;
                    NaAt(cand[i], level1, step, out a1, out b1, out c1);
                    _naPaid.TryGetValue(cand[i].C, out had);
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
                        if (e > 0)
                        {
                            int ip = (int)Math.Min(int.MaxValue, e);
                            var h = x.C.Leader;
                            k.KingdomBudgetWallet -= ip;                  // najpierw skarbiec, potem glowa - dostaje dokladnie tyle, ile zeszlo
                            try { h.ChangeHeroGold(ip); } catch { k.KingdomBudgetWallet += ip; throw; }   // wyjatek - zloto wraca do skarbca (nic w nicosc)
                            paid += ip; row.RentPaid += ip; row.RentN++;
                            long cc; _naCash.TryGetValue(x.C, out cc); _naCash[x.C] = cc + ip;   // gotowka W5: nastepny zwrot 70% zdejmuje ja z podstawy
                            _naPaid[x.C] = had + ip;                                             // gotowka korony tej doby razem (ClanBudget.AidCash, CSV doplata_w5)
                            if (x.W <= x.Pc0) _raNotAtCap++;              // kontrola: gotowka tylko rodom, ktorych zold druzyn przekracza pulap wlasny (oczekiwane 0)
                            CirculationWindows.NoteHeroGold(h, ip);       // paczka 169b: glowa poza swiatem (Disabled) - zloto wyszlo ze swiata (tylko licznik)
                            had += ip;
                        }
                        double nb = Math.Max(boost1[i], had), ob;
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
            }
            return level1;
        }
    }
}
