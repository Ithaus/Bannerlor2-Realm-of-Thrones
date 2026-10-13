using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace Armoury
{
    /// <summary>
    /// W5 (decyzja Jeffa 13.10 po biegu rocznym: wojska lordow 79 tys. przy progu 85 tys., "podatki trzeba gdzies zwiekszyc, na czas wojny"; kierunek 1 z 3:
    /// "KORONA PLACI WEDLUG POTRZEBY - pieniadze wojenne najpierw dopelniaja zold rodom, ktore stoja w polu i nie maja z czego placic; dopiero reszta idzie wedlug
    /// wielkosci ziem") - DOPLATA DO ZOLDU WEDLUG POTRZEBY. Wylacznik CrownWarAidByNeed (wylaczony = stan sprzed paczki co do monety), cel CrownWarAidFillTarget
    /// (0.60 limitu druzyn), krok werbunku CrownWarAidRecruitStep (0.10 limitu na dobe).
    ///  - GARNEK krolestwa = danina z oszczednosci ludu P1 tej doby (CrownIncome.KDay.WarAid), najwyzej reszta wplywow dnia po zwrocie zoldu (CrownIncome.LeftFor).
    ///    Bez daniny (P1 wylaczone, pokoj, s = 0, lud bez nadwyzki) garnek = 0 i regula nie robi nic. Nigdy z zapasu skarbca, nigdy z kredytu.
    ///  - KOLEJNOSC DOBY: danina wplywa (TownFolk.Daily) -> ... -> zwrot zoldu (WageRefund) -> DOPLATA (z CrownRents.Daily, po warunkach rodow) -> renty 180
    ///    (reszta wplywow dnia, juz bez doplaty) -> ClanBudget.Daily (pulap rodu z przydzialem tej doby).
    ///  - KOMU: rody AI krolestwa w wojnie z wierszem rent 180 i spelnionym warunkiem rent (ta sama zaloga i sluzba - CrownRents.Daily podaje gotowy wynik),
    ///    z budzetem 166 w wojnie; bez Nocnej Strazy (sluzy bez zoldu - ClanBudget: pulap w ludziach), bez kompanii najemnych (renty ich nie licza), bez rodu
    ///    gracza (nie ma budzetu AI; renty dostaje jak dotad), bez rodow rycerzy 179 (druzyny wstrzymane), bez rodow w zajeciu za dlugi (168 - kiese
    ///    ponad podloge bierze wierzyciel). Rod bez lenna nie ma wiersza rent - bez doplaty.
    ///  - ILE: pulap wlasny druzyn rodu = pulap partii z wczorajszego budzetu BEZ doplaty (ClanBudget.PartyCapOwn). Koszt rodu przy poziomie f (ulamek limitu
    ///    druzyn): zold dzisiejszych ludzi do f x limit (po sredniej stawce rodu) + zold brakujacych do f x limit, minus pulap wlasny. Potrzeba = koszt przy celu.
    ///    Gdy garnek nie starcza na wszystkie potrzeby - POZIOM WYROWNANIA: najwyzszy f (najwyzej cel), przy ktorym suma kosztow miesci sie w garnku; korona
    ///    gwarantuje kazdemu rodowi druzyne do f x limit, wiec pierwsi dostaja ci, ktorych wlasny pulap oplaca najmniejsza czesc limitu (od najslabszych).
    ///  - DOPLATA IDZIE ZA LUDZMI, NIE ZA OBIETNICA: GOTOWKA dnia = zold ludzi, ktorzy dzis SA w druzynach (do poziomu f), ponad pulap wlasny - skarbiec ->
    ///    kiesa glowy rodu. Za ludzi, ktorych jeszcze nie ma, korona nie placi nic: rod dostaje tylko MIEJSCE w pulapie na krok werbunku (najwyzej
    ///    CrownWarAidRecruitStep limitu na dobe, najwyzej do poziomu f). Zwerbuje - od nastepnej doby korona placi ich zold; nie zwerbuje - nie dostaje nic
    ///    wiecej. Bez licznika dob i bez stanu: kazda doba liczona od ludzi, ktorzy sa.
    ///  - ZNACZONA NA DRUZYNY: przydzial (gotowka + miejsce) podnosi pulap partii i pulap rodu w ClanBudget.Daily tej doby (b.Aid) - nie wchodzi do D
    ///    (ClanIncomeBook go nie widzi: nie jest dochodem, z ktorego liczy sie dwor, sprzet, budowy, limit Banku ani splata dlugu z jednorazowych).
    ///    Zwolnienia 166 rodu z doplata licza nadwyzke druzyn ponad pulap partii z doplata (nadwyzka zoldu zalog ponad pulap wlasny nie zwalnia ludzi
    ///    oplaconych przez korone); nowa druzyna i "sluzba na miare srodkow" dalej wedlug pulapu wlasnego.
    ///    Recenzja W5: zold oplacony gotowka korony NIE liczy sie do zoldu, ktoremu ustepuje dwor (ClanBudget.Court) i niewydany przydzial sprzetu
    ///    (CourtFull.Take, E5/A2) - obie reguly pytaja o zold, ktory rod placi SAM (b.AidCash). Bez tego rod z doplata i kiesa ponad rezerwe przestawalby
    ///    wplacac niewydany sprzet do kasy siedziby (zatrzymywalby go w kiesie, a zold placilaby mu korona) - W5 zmienialoby po cichu regule E5/A2.
    ///  - 1 ZL = 1 ZL ZOLDU: zold oplacony doplata nie jest podstawa zwrotu 70% nastepnej doby (NeedAidRefundCut w WageRefund) - korona nie placi za tego
    ///    samego czlowieka 100% i jeszcze 70%.
    /// Platnik -> odbiorca: Kingdom.KingdomBudgetWallet -> kiesa glowy rodu (najpierw skarbiec, potem glowa; wyjatek = zloto wraca). Renty 180 maleja o to,
    /// co poszlo w doplate (reszta garnka idzie w renty tej samej doby). Zapis gry: nic nowego (stan sesji: gotowka wczoraj, pierwsza doba doplaty rodu).
    /// Linia doby "Doplata wedlug potrzeby (W5): dzien N" (z ArmouryBehavior po rentach 180); kolumny budzet-rodow.csv doplata_w5, podniesienie_pulapu_w5.
    /// Wyjatek per krolestwo / per rod - licznik potkniec (pierwszy do pliku), bez globalnych wylacznikow (CLAUDE.md 7).
    /// </summary>
    internal static partial class KingdomTreasury
    {
        private sealed class NaClan { public Clan C; public double Pc0, W, P; public int M, L; }
        private sealed class NaK
        {
            public string Name = ""; public long Pot, Aid, Paid; public double Need, Alloc, Level, Boost;
            public int Cand, NeedN, PaidN, BoostN, NeedMen, NeedLimit, NoBudget;
        }
        private sealed class NaFirst { public int Day, Men, Limit, Last; }

        private static readonly Dictionary<Clan, long> _naCash = new Dictionary<Clan, long>();       // gotowka ostatniego kroku (nastepny WageRefund zdejmuje ja z podstawy zwrotu)
        private static readonly Dictionary<Clan, long> _naPaid = new Dictionary<Clan, long>();       // gotowka tej doby (CSV; nie zmieniana po kroku)
        private static readonly Dictionary<Clan, double> _naBoost = new Dictionary<Clan, double>();  // przydzial tej doby: gotowka + miejsce na werbunek (ClanBudget.Daily)
        private static readonly Dictionary<Clan, NaFirst> _naFirst = new Dictionary<Clan, NaFirst>();   // pomiar: stan druzyn w dniu pierwszej doplaty (pamiec sesji)
        private static readonly List<NaK> _naRows = new List<NaK>();
        private static readonly List<Clan> _naGone = new List<Clan>();
        private static readonly HashSet<string> _naErr = new HashSet<string>();
        private static int _naDay = int.MinValue, _naStumbles, _naCutN, _naNotAtCap;
        private static long _naCut;
        private static int _naAidN, _naMenNow, _naLimNow, _naMen0, _naLim0, _naOldN, _naOldMenNow, _naOldLimNow, _naOldMen0, _naOldLim0;
        /// <summary>"Obieg" (korona) i "Korona: wplywy dnia (165)": doplata do zoldu wedlug potrzeby wyplacona w tej dobie (skarbce -> glowy rodow).</summary>
        internal static long LastNeedAid;

        internal static bool NeedAidOn { get { var s = Settings.Current; return s != null && s.CrownWarAidByNeed && CrownRents.On && ClanBudget.On; } }

        private static void NeedAidReset()
        {
            _naCash.Clear(); _naPaid.Clear(); _naBoost.Clear(); _naFirst.Clear(); _naRows.Clear(); _naGone.Clear(); _naErr.Clear();
            _naDay = int.MinValue; _naStumbles = 0; _naCut = 0; _naCutN = 0; _naNotAtCap = 0; LastNeedAid = 0;
            NaClearMeasure();
        }

        private static void NaClearMeasure()
        {
            _naAidN = _naMenNow = _naLimNow = _naMen0 = _naLim0 = _naOldN = _naOldMenNow = _naOldLimNow = _naOldMen0 = _naOldLim0 = 0;
        }

        private static void NaStumble(string where, Exception e)
        {
            _naStumbles++;
            try { if (_naErr.Add(where)) Log.Error("KingdomTreasury.NeedAid." + where, e); } catch { }
        }

        /// <summary>Nowa doba kroku: przydzialy i gotowka poprzedniej doby znikaja (zwrot zoldu tej doby juz je odliczyl - biegnie przed krokiem).</summary>
        private static void NaRoll(int today)
        {
            if (_naDay == today) return;
            _naDay = today;
            _naCash.Clear(); _naPaid.Clear(); _naBoost.Clear(); _naRows.Clear(); _naNotAtCap = 0;
            NaClearMeasure();
        }

        private static double NaTarget(Settings s) { return WtClamp(s.CrownWarAidFillTarget, 0, 1); }
        private static double NaStep(Settings s) { return WtClamp(s.CrownWarAidRecruitStep, 0, 1); }

        /// <summary>Czynne druzyny lordow rodu (bez druzyn wstrzymanych rodu rycerza 179): suma limitow wielkosci, zwykli ludzie, zold z rycerzami 179 -
        /// te same liczby co ClanBudget (zold partii) i CrownRents (ludzie druzyn).</summary>
        private static bool NaParties(Clan c, out int limit, out int men, out long wage)
        {
            limit = 0; men = 0; wage = 0;
            var wps = c.WarPartyComponents;
            if (wps == null) return false;
            for (int i = 0; i < wps.Count; i++)
            {
                var mp = wps[i] != null ? wps[i].MobileParty : null;
                if (mp == null || !mp.IsActive || !mp.IsLordParty || mp.Party == null || GentryService.IsHeldParty(mp)) continue;
                limit += Math.Max(0, mp.Party.PartySizeLimit);
                men += mp.MemberRoster != null ? mp.MemberRoster.TotalRegulars : 0;
                wage += Math.Max(0, mp.TotalWage) + GentryService.KnightWage(mp);
            }
            return limit > 0;
        }

        /// <summary>Zold, ktory korona uznaje przy poziomie f: ludzie, ktorzy SA, najwyzej do f x limit (po sredniej stawce rodu).</summary>
        private static double NaFunded(NaClan x, double f)
        {
            double cap = f * x.L;
            if (cap <= 0) return 0;
            return x.M > cap ? x.W * cap / x.M : x.W;
        }

        /// <summary>Koszt rodu przy poziomie f: zold druzyn doprowadzonych do f x limit minus pulap wlasny druzyn (0, gdy rod stac samemu).</summary>
        private static double NaCost(NaClan x, double f)
        {
            double cap = f * x.L;
            double goal = x.M < cap ? x.W + (cap - x.M) * x.P : NaFunded(x, f);
            return Math.Max(0, goal - x.Pc0);
        }

        /// <summary>
        /// W5 - krok krolestwa (wola CrownRents.Daily w petli krolestw: po warunkach rent wszystkich rodow z lennem, PRZED odczytem reszty wplywow dnia do rent).
        /// list / ok - rody z wierszem rent i wynik warunku rent (zaloga i sluzba). Zwraca gotowke wyplacona rodom tego krolestwa (0 - nic sie nie zmienilo).
        /// Nie rzuca (wlasny try; kazdy rod we wlasnym try - wyjatek przy wyplacie oddaje zloto skarbcowi).
        /// </summary>
        internal static long NeedAidKingdom(Kingdom k, List<Clan> list, bool[] ok)
        {
            long paidK = 0;
            try
            {
                var s = Settings.Current;
                if (s == null || k == null || list == null || ok == null || Campaign.Current == null) return 0;
                int today = (int)CampaignTime.Now.ToDays;
                NaRoll(today);
                if (!NeedAidOn) return 0;
                var day = CrownIncome.DayOf(k);
                if (day == null || !AtWar(k)) return 0;                  // pokoj = brak doplaty (resztka daniny z doby zawarcia pokoju idzie w renty jak dotad)
                double target = NaTarget(s), step = NaStep(s);
                var row = new NaK { Name = k.Name != null ? k.Name.ToString() : k.StringId, Aid = Math.Max(0, day.WarAid) };
                row.Pot = Math.Max(0, Math.Min(row.Aid, CrownIncome.LeftFor(k)));   // danina tej doby, najwyzej reszta wplywow dnia po zwrocie zoldu
                _naRows.Add(row);
                if (!(target > 0)) return 0;
                float avgWage = Math.Max(1f, Campaign.Current.AverageWage);
                var cand = new List<NaClan>();
                for (int i = 0; i < list.Count && i < ok.Length; i++)
                {
                    var c = list[i];
                    try
                    {
                        if (c == null || !ok[i] || c == Clan.PlayerClan || c.Leader == null || !c.Leader.IsAlive) continue;
                        // rod w zajeciu za dlugi (168): jego kiese ponad podloge bierze wierzyciel, a poczet ma malec - korona nie doplaca (zloto nie pojdzie do Banku)
                        if (DebtLadder.On && DebtLadder.IsSeized(c)) { row.NoBudget++; continue; }
                        double pc0;
                        if (!ClanBudget.PartyCapOwn(c, out pc0)) { row.NoBudget++; continue; }   // Straz bez zoldu, najemnik, rod bez budzetu 166 (pierwsza doba sesji)
                        int limit, men; long wage;
                        if (!NaParties(c, out limit, out men, out wage)) continue;               // bez czynnej druzyny (niewola, brak dowodcy, rod rycerza 179)
                        var x = new NaClan { C = c, Pc0 = pc0, W = wage, M = men, L = limit, P = men >= 20 && wage > 0 ? (double)wage / men : avgWage };
                        row.Cand++;
                        double need = NaCost(x, target);
                        if (!(need > 0)) continue;                                               // rod stac na cel z wlasnego pulapu - bez doplaty
                        row.NeedN++; row.Need += need; row.NeedMen += men; row.NeedLimit += limit;
                        cand.Add(x);
                    }
                    catch (Exception e) { NaStumble("NeedAidKingdom(rod)", e); }
                }
                if (cand.Count == 0 || row.Pot <= 0) return 0;
                // poziom wyrownania: najwyzszy f <= cel, przy ktorym suma kosztow miesci sie w garnku (koszt rosnie z f; przy f = 0 jest 0)
                double level = target;
                if (row.Need > row.Pot)
                {
                    double lo = 0, hi = target;
                    for (int it = 0; it < 32; it++)
                    {
                        double mid = 0.5 * (lo + hi), sum = 0;
                        for (int i = 0; i < cand.Count; i++) sum += NaCost(cand[i], mid);
                        if (sum <= row.Pot) lo = mid; else hi = mid;
                    }
                    level = lo;
                }
                row.Level = level;
                long left = row.Pot;
                for (int i = 0; i < cand.Count; i++)
                {
                    var x = cand[i];
                    try
                    {
                        double alloc = NaCost(x, level);
                        if (!(alloc > 0)) continue;
                        row.Alloc += alloc;
                        double funded = NaFunded(x, level);
                        // miejsce na krok werbunku (bez gotowki): tylko gdy druzyny sa ponizej poziomu; razem z gotowka nigdy ponad koszt rodu przy poziomie
                        double room = x.M < level * x.L ? Math.Min(step * x.L, level * x.L - x.M) * x.P : 0;
                        double boost = Math.Min(alloc, Math.Max(0, funded + room - x.Pc0));
                        long cash = (long)Math.Floor(Math.Max(0, funded - x.Pc0));               // tylko za ludzi, ktorzy dzis sa w druzynach
                        if (cash > left) cash = left;
                        long wallet = Math.Max(0, (long)k.KingdomBudgetWallet);
                        if (cash > wallet) cash = wallet;
                        if (cash > int.MaxValue) cash = int.MaxValue;
                        if (cash > 0)
                        {
                            int ip = (int)cash;
                            var h = x.C.Leader;
                            k.KingdomBudgetWallet -= ip;                  // najpierw skarbiec, potem glowa - dostaje dokladnie tyle, ile zeszlo
                            try { h.ChangeHeroGold(ip); } catch { k.KingdomBudgetWallet += ip; throw; }   // wyjatek - zloto wraca do skarbca (nic w nicosc)
                            left -= ip; paidK += ip; row.Paid += ip; row.PaidN++;
                            _naCash[x.C] = ip; _naPaid[x.C] = ip;
                            if (x.W <= x.Pc0) _naNotAtCap++;              // kontrola: gotowka tylko rodom, ktorych zold druzyn przekracza pulap wlasny (oczekiwane 0)
                            CirculationWindows.NoteHeroGold(h, ip);       // paczka 169b: glowa poza swiatem (Disabled) - zloto wyszlo ze swiata (tylko licznik)
                        }
                        if (boost < cash) boost = cash;
                        if (boost > 0)
                        {
                            _naBoost[x.C] = boost; row.Boost += boost; row.BoostN++;
                            // pomiar: stan druzyn w dniu pierwszej doplaty (przerwa ponad 28 dob = nowy poczatek)
                            NaFirst f0;
                            if (!_naFirst.TryGetValue(x.C, out f0) || today - f0.Last > 28) { f0 = new NaFirst { Day = today, Men = x.M, Limit = x.L }; _naFirst[x.C] = f0; }
                            f0.Last = today;
                            _naAidN++; _naMenNow += x.M; _naLimNow += x.L; _naMen0 += f0.Men; _naLim0 += f0.Limit;
                            if (today - f0.Day >= 28) { _naOldN++; _naOldMenNow += x.M; _naOldLimNow += x.L; _naOldMen0 += f0.Men; _naOldLim0 += f0.Limit; }
                        }
                    }
                    catch (Exception e) { NaStumble("NeedAidKingdom(wyplata)", e); }
                }
            }
            catch (Exception e) { NaStumble("NeedAidKingdom", e); }
            finally
            {
                // recenzja W5: ksiegowanie wyplaty ZAWSZE, takze po wyjatku w polowie petli - zloto, ktore zeszlo ze skarbca, musi zejsc z reszty wplywow
                // dnia (inaczej renty tej doby dzielilyby te same pieniadze drugi raz - z zapasu skarbca) i trafic do linii 165
                if (paidK > 0)
                {
                    try
                    {
                        CrownIncome.Spent(k, paidK);                      // doplata schodzi z reszty wplywow dnia; reszta garnka zostaje do rent tej doby
                        var dk = CrownIncome.DayOf(k);
                        if (dk != null) dk.NeedAid += paidK;
                    }
                    catch (Exception e) { NaStumble("NeedAidKingdom(ksiega)", e); }
                }
            }
            return paidK;
        }

        /// <summary>ClanBudget.Daily: przydzial korony dla rodu z dzisiejszego kroku W5 (gotowka + miejsce na werbunek) - podnosi pulap partii i pulap rodu;
        /// 0 - bez przydzialu, klucz wylaczony albo krok dzis nie biegl.</summary>
        internal static double NeedAidBoostOf(Clan c)
        {
            try
            {
                double b;
                if (c == null || Campaign.Current == null || _naDay != (int)CampaignTime.Now.ToDays || !_naBoost.TryGetValue(c, out b)) return 0;
                return b > 0 ? b : 0;
            }
            catch { return 0; }
        }

        /// <summary>ClanBudget.Daily (recenzja W5): GOTOWKA korony dla rodu z dzisiejszego kroku W5 - zold druzyn, ktory dzis placi korona (bez miejsca na
        /// werbunek). Budzet odejmuje ja od zoldu rodu tam, gdzie regula pyta "ile zoldu rod placi sam" (dwor i niewydany sprzet ustepujace zoldowi, pomiar
        /// rodow z luzem). 0 - bez wyplaty, klucz wylaczony albo krok dzis nie biegl.</summary>
        internal static long NeedAidCashOf(Clan c)
        {
            try
            {
                long p;
                if (c == null || Campaign.Current == null || _naDay != (int)CampaignTime.Now.ToDays || !_naPaid.TryGetValue(c, out p)) return 0;
                return p > 0 ? p : 0;
            }
            catch { return 0; }
        }

        /// <summary>WageRefund (przed krokiem W5 tej doby): ile z zoldu `paid` zaplaconego przez rod od poprzedniego zwrotu oplacila wczorajsza doplata - ta czesc
        /// nie jest podstawa zwrotu. Zdejmuje wykorzystana czesc z pamieci (rod moze miec kilku platnikow). 0 przy wylaczonym kluczu albo bez wczorajszej doplaty.</summary>
        internal static long NeedAidRefundCut(Clan c, long paid)
        {
            try
            {
                long have;
                if (c == null || paid <= 0 || Campaign.Current == null || !NeedAidOn) return 0;
                if (_naDay != (int)CampaignTime.Now.ToDays - 1 || !_naCash.TryGetValue(c, out have) || have <= 0) return 0;
                long cut = Math.Min(paid, have);
                _naCash[c] = have - cut;
                _naCut += cut; _naCutN++;
                return cut;
            }
            catch (Exception e) { NaStumble("NeedAidRefundCut", e); return 0; }
        }

        /// <summary>Kolumny budzet-rodow.csv (ostatnie - czytniki po nazwach): gotowka doplaty tej doby i przydzial w dzisiejszym pulapie rodu.</summary>
        internal const string NeedAidCsvHeader = ";doplata_w5;podniesienie_pulapu_w5";

        internal static string NeedAidCsv(Clan c)
        {
            try
            {
                if (c == null || Campaign.Current == null || _naDay != (int)CampaignTime.Now.ToDays) return ";;";
                long p; double b;
                bool hp = _naPaid.TryGetValue(c, out p), hb = _naBoost.TryGetValue(c, out b);
                if (!hp && !hb) return ";;";
                return ";" + (hp ? p : 0).ToString(WtInv) + ";" + Math.Round(hb ? b : 0).ToString("0", WtInv);
            }
            catch { return ";;"; }
        }

        private static string NaPct(long a, long b) { return b > 0 ? (100.0 * a / b).ToString("0.0", WtInv) + "%" : "-"; }

        // ------------------------------------------------------------ doba: linia "Doplata wedlug potrzeby (W5)" - z ArmouryBehavior, zaraz po rentach 180
        internal static void NeedAidDay()
        {
            LastNeedAid = 0;
            var s = Settings.Current;
            if (s == null || Campaign.Current == null) return;
            int today = (int)CampaignTime.Now.ToDays;
            try
            {
                NaRoll(today);                                           // renty dzis nie biegly (180 wylaczone) - stary stan znika
                long pot = 0, aid = 0, paid = 0; double need = 0, alloc = 0, boost = 0;
                int kWar = 0, kPot = 0, kNeed = 0, needN = 0, paidN = 0, boostN = 0, cand = 0, needMen = 0, needLim = 0, noBudget = 0, shortK = 0;
                var parts = new List<KeyValuePair<double, string>>();
                for (int i = 0; i < _naRows.Count; i++)
                {
                    var r = _naRows[i];
                    kWar++; pot += r.Pot; aid += r.Aid; paid += r.Paid; need += r.Need; alloc += r.Alloc; boost += r.Boost;
                    needN += r.NeedN; paidN += r.PaidN; boostN += r.BoostN; cand += r.Cand; needMen += r.NeedMen; needLim += r.NeedLimit; noBudget += r.NoBudget;
                    if (r.Pot > 0) kPot++;
                    if (r.NeedN > 0) kNeed++;
                    if (r.NeedN > 0 && r.Need > r.Pot) shortK++;
                    if (r.Pot <= 0 && r.NeedN <= 0) continue;
                    parts.Add(new KeyValuePair<double, string>(r.Need, r.Name + ": garnek " + r.Pot + " (danina " + r.Aid + "), potrzeba " + Math.Round(r.Need).ToString("0", WtInv) + " u " + r.NeedN
                              + " rodow, poziom " + (r.Pot > 0 && r.NeedN > 0 ? (100 * r.Level).ToString("0.0", WtInv) + "%" : "-") + ", wyplacono " + r.Paid + " (rodow " + r.PaidN
                              + "), reszta " + (r.Pot - r.Paid)));
                }
                LastNeedAid = paid;
                // rody, ktore odeszly (pomiar pierwszej doplaty)
                _naGone.Clear();
                foreach (var kv in _naFirst) if (kv.Key == null || kv.Key.IsEliminated || today - kv.Value.Last > 56) _naGone.Add(kv.Key);
                for (int i = 0; i < _naGone.Count; i++) _naFirst.Remove(_naGone[i]);
                _naGone.Clear();
                if (s.LogEnabled)
                {
                    parts.Sort((x, y) => y.Key.CompareTo(x.Key));
                    var txt = new List<string>(); foreach (var p in parts) txt.Add(p.Value);
                    var sb = new StringBuilder(2048);
                    sb.Append("Doplata wedlug potrzeby (W5): dzien ").Append(today)
                      .Append(" | wylacznik Crown War Aid By Need ").Append(s.CrownWarAidByNeed ? "tak" : "nie (doplata 0 - jak przed paczka)")
                      .Append(", renty 180 (Crown Rents) ").Append(CrownRents.On ? "tak" : "nie (doplata 0)")
                      .Append(", budzet rodow 166 ").Append(ClanBudget.On ? (ClanBudget.Pending ? "tak (jeszcze nie policzony w tej sesji - doplata od jutra)" : "tak") : "nie (doplata 0)")
                      .Append(", danina z oszczednosci P1 (Folk War Aid) ").Append(s.FolkWarAid ? "tak" : "nie (garnek 0)")
                      .Append(" | cel wypelnienia ").Append((100 * NaTarget(s)).ToString("0", WtInv)).Append("% limitu druzyn, krok werbunku ").Append((100 * NaStep(s)).ToString("0", WtInv))
                      .Append("% limitu na dobe (miejsce w pulapie bez gotowki); garnek = danina P1 tej doby, najwyzej reszta wplywow dnia po zwrocie zoldu")
                      .Append(" | krolestw w wojnie ").Append(kWar).Append(", z garnkiem ").Append(kPot).Append(", z rodami z potrzeba ").Append(kNeed)
                      .Append(", gdzie garnek nie starcza na cel ").Append(shortK)
                      .Append(" | garnek ").Append(pot).Append(" (danina dnia ").Append(aid).Append("), rodow z potrzeba ").Append(needN).Append(" z ").Append(cand)
                      .Append(" ocenianych (z warunkiem rent i budzetem; pominiete - bez budzetu 166, Straz bez zoldu albo zajecie za dlugi: ").Append(noBudget).Append("), potrzeba razem ").Append(Math.Round(need).ToString("0", WtInv))
                      .Append(", przydzial przy poziomie wyrownania ").Append(Math.Round(alloc).ToString("0", WtInv))
                      .Append(", wyplacono ").Append(paid).Append(" (rodow ").Append(paidN).Append("), reszta do rent ").Append(pot - paid)
                      .Append(" | kontrola: gotowka rodom, ktorych zold druzyn miesci sie w pulapie wlasnym ").Append(_naNotAtCap).Append(" (oczekiwane 0)")
                      .Append(" | podniesienie pulapu druzyn ").Append(Math.Round(boost).ToString("0", WtInv)).Append(" u ").Append(boostN)
                      .Append(" rodow (gotowka + miejsce na krok werbunku ").Append(Math.Round(Math.Max(0, boost - paid)).ToString("0", WtInv)).Append(')')
                      .Append(" | druzyny rodow z potrzeba: ").Append(needMen).Append(" ludzi / limit ").Append(needLim).Append(" (").Append(NaPct(needMen, needLim)).Append(')')
                      .Append(" | rody z doplata dzis ").Append(_naAidN).Append(": ").Append(_naMenNow).Append(" ludzi / limit ").Append(_naLimNow).Append(" (").Append(NaPct(_naMenNow, _naLimNow))
                      .Append("), w dniu pierwszej doplaty ").Append(_naMen0).Append(" / ").Append(_naLim0).Append(" (").Append(NaPct(_naMen0, _naLim0)).Append(')')
                      .Append("; z doplata od co najmniej 28 dob ").Append(_naOldN).Append(" rodow: wypelnienie wtedy ").Append(NaPct(_naOldMen0, _naOldLim0))
                      .Append(" -> dzis ").Append(NaPct(_naOldMenNow, _naOldLimNow)).Append(", ludzi ").Append(_naOldMen0).Append(" -> ").Append(_naOldMenNow)
                      .Append(" | zold oplacony wczorajsza doplata poza podstawa zwrotu 70% (dzis) ").Append(_naCut).Append(" (wpisow platnikow ").Append(_naCutN).Append(')')
                      .Append(" | na krolestwo: ").Append(txt.Count > 0 ? string.Join(" ; ", txt.ToArray()) : "-")
                      .Append(" | potkniecia ").Append(_naStumbles).Append('.');
                    Log.Info(sb.ToString());
                }
            }
            catch (Exception e) { NaStumble("NeedAidDay", e); }
            finally { _naStumbles = 0; _naCut = 0; _naCutN = 0; }
        }
    }
}
