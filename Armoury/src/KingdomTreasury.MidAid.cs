using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;

namespace Armoury
{
    /// <summary>
    /// f15-skarbiec / W5c (decyzja Jeffa 18.10, doslownie "takze srednim" - na pytanie: "Skarbce rosna, a armie sa troche mniejsze. Czy skarbiec krolestwa ma w czasie
    /// wojny doplacac do zoldu takze srednim rodom, a nie tylko biednym? Powiedziales, ze te pieniadze sa 'na wojny i trudne czasy'.") - SKARBIEC DLA SREDNICH RODOW.
    /// Klucz CrownWarAidMiddle (domyslnie TAK; wylaczony = stan sprzed paczki co do monety). Czynny tylko z W5b (CrownSurplusFeedsNeedAid - z nim W5, renty 180, budzet 166).
    ///
    /// Dowod (fin92, D:/Backup-Bannerlord/logi-testow/Armoury-2026-10-18_09-33-39.log + budzet-rodow.csv, ostatnie 28 dob): po f6/f7 renty 180 spadly 283.6k -> 112.9k/d,
    /// 143k/d zostaje w skarbcach (skarbce +17k/d, 57.2 mln wobec 40.2 mln w fin91), wojsko w polu 108k wobec 117k (-7%), pulap zoldu -18%. Doplata W5 (60% limitu
    /// druzyn) jest pokryta w ok. 96% (potrzeba 166k/d, niepokryte 5.9k/d) - wiaze CEL W5, nie pieniadze; nadwyzka skarbcow ponad zapas wojny ok. 35 mln.
    /// Uwaga: W5 juz dzis placi rodom ze wszystkich pasm (bez miary bogactwa - wedlug pulapu): R-2R ok. 79k/d, ponad 2R ok. 37k/d gotowki (CSV) - decyzja "takze
    /// srednim" dotyczy pieniedzy PONAD gwarancje W5.
    ///
    /// REGULA (jedno zdanie): ponad gwarancje W5 (druzyny do CrownWarAidFillTarget limitu, bez zmian) skarbiec ponad zapas wojny placi czesc f braku zoldu druzyn
    /// rodu do PELNEGO limitu; f = ta sama miara potrzeby co zwrot f6 i renty f7 (KingdomTreasury.NeedOfClan - ta sama pamiec doby, te same G i R z wczorajszego
    /// budzetu 166, R miary bez ciec - Need Reserve Uncut): ponizej R f = 1, R-2R f = (2R - G) / R, ponad 2R f = 0 (nic); "bez miary" (w krokach W5 to dluznik 168 bez
    /// zajecia) f = 1 - ta sama regula co f6/f7 ("bez miary - pelny").
    ///  - KTO: rody z kroku W5 krolestwa w wojnie (warunek rent, budzet 166, czynna druzyna, bez zajecia 168, bez Strazy i najemnikow; gracz z W6b) - TAKZE te, ktorym
    ///    W5 nic nie dal (pulap wlasny starcza na 60%, nie na pelne druzyny). Bez: pasma ponad 2R, rodu z luzem w pulapie (zold druzyn ponizej 95% pulapu wlasnego -
    ///    ta sama granica co W7 i SlackGold: rod nie jest ponizej limitu z braku pieniedzy) i platnika tarczowego tej doby (platnik i odbiorca nigdy naraz - jak W6b).
    ///  - ILE: koszt rodu przy poziomie phi (ulamek limitu druzyn, NaCost - ten sam wzor co W5) ponad koszt przy poziomie, do ktorego doszlo W5 (wszystkie szczeble) =
    ///    brak ponad W5; W5c = f x ten brak. Przy phi = 1 - pelny limit. Pula krolestwa = mniejsze z (f x brak przy phi = 1, budzet); gdy budzet nie starcza -
    ///    POZIOM WYROWNANIA phi (najwyzszy, przy ktorym suma f x brak miesci sie w puli; od najslabszych - jak W5).
    ///  - Z CZEGO: BUDZET = 1/CrownSurplusAidDays NADWYZKI skarbca (ta sama liczba co W5b, policzona przed jakimkolwiek zlotem kroku W5: skarbiec - reszta wplywow dnia -
    ///    zapas wojny - dlug w Banku 186 - dlug wobec miast korona2) MINUS pula W5b tej doby. W5b + W5c razem najwyzej 1/30 nadwyzki na dobe - jedno tempo
    ///    skarbca; zapasu wojny nigdy (kontrola w linii). Nigdy z wplywow dnia (renty 180 dziela to samo co bez paczki), nigdy z kredytu. W5c rusza tylko, gdy W5b ma
    ///    nadwyzke ponad swoja pule - czyli gdy W5 juz doszlo do celu albo rod nie mial potrzeby W5 (szczeble W6, M1 i W7 wtedy nic nie robia - brak kolizji).
    ///  - JAK (zloto idzie w zolnierzy, nie w oszczednosci): GOTOWKA = f x (zold ludzi, ktorzy dzis SA w druzynach, do poziomu phi ponad pulap wlasny - NaAt) minus
    ///    gotowka korony, ktora rod juz dostal tej doby (W5, W5b, W6, M1); MIEJSCE w pulapie = przydzial W5 + f x (przydzial przy phi - przydzial W5) - krok werbunku
    ///    CrownWarAidRecruitStep limitu na dobe (jak W5; gracz - bez miejsca, sama gotowka). Przydzial idzie przez _naBoost / _naPaid / _naCash: podnosi pulap partii
    ///    i rodu w ClanBudget.Daily tej doby (b.Aid - nie wchodzi do D, nie otwiera nowych druzyn), dwor i niewydany sprzet mu nie ustepuja (b.AidCash), zold oplacony
    ///    gotowka W5c nie jest podstawa zwrotu 70% nastepnej doby (NeedAidRefundCut). Pozyczki wojenne tej doby (KW Banku, od ludu obieg2, miejska f8) licza brak zoldu
    ///    BEZ gotowki W5c (ClanBudget - MidAidCashOf): ta sama zlotowka nie jest pozyczana drugi raz.
    ///  - POKOJ: krok W5 nie biegnie - W5c = 0; zatrzymane renty f7 zbieraja sie wtedy w skarbcu (zapas na nastepna wojne).
    /// Platnik -> odbiorca: Kingdom.KingdomBudgetWallet -> kiesa glowy rodu (najpierw skarbiec, potem glowa; wyjatek - zloto wraca). KDay.MidAid (linia 165, poza
    /// wplywami dnia - z zapasu), LastMidAid ("Obieg", "Pompy"). Zapis gry: nic nowego (stan doby w pamieci sesji). Linia "Skarbiec dla srednich (W5c): dzien N" i
    /// segment linii W5; kolumny budzet-rodow.csv doplata_srednim_w5c (czesc doplata_w5, jak W5b i M1), podniesienie_pulapu_w5c, mnoznik_w5c.
    /// Wyjatek per krolestwo / per rod - licznik potkniec (pierwszy do pliku), bez globalnych wylacznikow (CLAUDE.md 7).
    /// </summary>
    internal static partial class KingdomTreasury
    {
        private sealed class McK
        {
            public string Name = ""; public long Budget, Sur, Res, Pool, Paid, W0, W1; public double Want, Level = -1, Boost; public int Elig, PaidN, BoostN;
        }
        private const int McBands = 4;   // pasma jak f6/f7: 0 ponizej R, 1 R-2R, 2 ponad 2R (pominiete), 3 bez miary (dluznik - f = 1)

        private static readonly Dictionary<Clan, long> _mcPaid = new Dictionary<Clan, long>();      // rod -> gotowka W5c tej doby (CSV, kontrola pozyczek ClanBudget)
        private static readonly Dictionary<Clan, double> _mcBoost = new Dictionary<Clan, double>(); // rod -> podniesienie pulapu W5c ponad przydzial W5 tej doby (CSV)
        private static readonly Dictionary<Clan, double> _mcF = new Dictionary<Clan, double>();     // rod -> mnoznik potrzeby f tej doby (CSV; rody ocenione przez W5c)
        private static readonly List<McK> _mcRows = new List<McK>();
        private static readonly HashSet<string> _mcErr = new HashSet<string>();
        private static readonly long[] _mcCashB = new long[McBands];
        private static readonly double[] _mcBoostB = new double[McBands];
        private static readonly int[] _mcEligB = new int[McBands], _mcPaidB = new int[McBands];
        private static int _mcStumbles, _mcRichSkip, _mcSlackSkip, _mcScutSkip, _mcBelow, _mcNotAtCap, _mcOverShort, _mcRichPaid;
        private static long _mcPlGot; private static double _mcPlF = -1; private static int _mcPlBand = -1;
        private static int _plToldMidDay = int.MinValue;   // wyjasnienie gracza - pamiec procesu jak W6b (PlTellDue), bez pola zapisu
        /// <summary>"Obieg", "Pompy" i linia 165: gotowka W5c wyplacona tej doby (skarbce ponad zapas wojny -> glowy rodow, poza wplywami dnia).</summary>
        internal static long LastMidAid;

        internal static bool MidAidOn { get { var s = Settings.Current; return s != null && s.CrownWarAidMiddle && CrownSurplusOn; } }

        private static void MidAidReset()
        {
            McClearDay(); _mcErr.Clear(); LastMidAid = 0;
        }

        /// <summary>Nowa doba kroku W5 (NaRoll): stan doby W5c znika razem z przydzialami W5.</summary>
        private static void McClearDay()
        {
            _mcPaid.Clear(); _mcBoost.Clear(); _mcF.Clear(); _mcRows.Clear();
            for (int i = 0; i < McBands; i++) { _mcCashB[i] = 0; _mcBoostB[i] = 0; _mcEligB[i] = 0; _mcPaidB[i] = 0; }
            _mcStumbles = _mcRichSkip = _mcSlackSkip = _mcScutSkip = _mcBelow = _mcNotAtCap = _mcOverShort = _mcRichPaid = 0;
            _mcPlGot = 0; _mcPlF = -1; _mcPlBand = -1;
        }

        private static void McStumble(string where, Exception e)
        {
            _mcStumbles++;
            try { if (_mcErr.Add(where)) Log.Error("KingdomTreasury.MidAid." + where, e); } catch { }
        }

        private sealed class McC { public NaClan X; public double F, A; public int Band; }

        /// <summary>
        /// W5c - krok krolestwa (wola NeedAidKingdom PO wszystkich szczeblach W5 - danina, W5b, W6, M1 - i przed planem W7; takze gdy W5 nie mial kandydatow).
        /// all - wszystkie rody ocenione przez W5 tej doby (z warunkiem rent, budzetem i czynna druzyna), cand - rody z potrzeba W5, lvl - poziom W5 po ostatnim szczeblu,
        /// aidPaid - gotowka szczebla daniny (jeszcze nie zdjeta z reszty wplywow dnia - do kontroli zapasu), w5bPool - pula W5b z planu (CrownSurplusPlan). Zwraca gotowke
        /// W5c. Nie rzuca; ksiegowanie w finally.
        /// </summary>
        private static long MidAidStep(Kingdom k, List<NaClan> all, List<NaClan> cand, NaK row, double lvl, double step, long aidPaid, long w5bPool, int today)
        {
            long paid = 0;
            McK mk = null;
            try
            {
                var s = Settings.Current;
                if (!MidAidOn || s == null || k == null || row == null || all == null || all.Count == 0) return 0;
                if (!AtWar(k)) return 0;                                                   // pokoj - nic (krok W5 i tak biegnie tylko w wojnie)
                mk = new McK { Name = row.Name, Sur = row.CrownSur, Res = row.CrownRes };
                _mcRows.Add(mk);
                // budzet: 1/30 nadwyzki sprzed zlota kroku W5 (W5b - CrownSurplusPlan) minus pula W5b tej doby - jedno tempo skarbca na W5b i W5c
                long budget = row.CrownSur > 0 ? (long)Math.Floor(row.CrownSur / CsDays(s)) - Math.Max(0, Math.Max(row.Crown, w5bPool)) : 0;
                mk.Budget = Math.Max(0, budget);
                var inCand = new HashSet<NaClan>(cand ?? new List<NaClan>());
                var el = new List<McC>();
                for (int i = 0; i < all.Count; i++)
                {
                    var x = all[i];
                    try
                    {
                        if (x == null || x.C == null || x.C.Leader == null || !x.C.Leader.IsAlive) continue;
                        double f, G, R;
                        int band = NeedOfClan(x.C, out f, out G, out R);              // ta sama miara i pamiec doby co zwrot f6 i renty f7
                        if (band == NbAbove) { _mcRichSkip++; continue; }             // ponad 2R - nic (decyzja: bogaci nie)
                        if (band == NbNone) f = 1.0;                                   // bez miary (dluznik 168) - jak f6/f7: pelny
                        if (!(f > 0)) continue;
                        if (x.W < LvAtCap * x.Pc0) { _mcSlackSkip++; continue; }      // luz w pulapie wlasnym - rod nie stoi ponizej limitu z braku pieniedzy
                        long sp; if (_scPaid.TryGetValue(x.C, out sp) && sp > 0) { _mcScutSkip++; continue; }   // platnik tarczowego tej doby - nigdy naraz
                        var m = new McC { X = x, F = Math.Min(1.0, f), Band = band, A = inCand.Contains(x) ? NaCost(x, Math.Max(0, lvl)) : 0 };
                        el.Add(m);
                        _mcF[x.C] = m.F;
                        _mcEligB[band]++; mk.Elig++;
                        if (x.Pl) { _mcPlF = m.F; _mcPlBand = band; }
                    }
                    catch (Exception e) { McStumble("MidAidStep(rod)", e); }
                }
                if (el.Count == 0) return 0;
                // chec: f x brak ponad W5 przy pelnym limicie (phi = 1)
                double want = McCost(el, 1.0);
                mk.Want = want;
                if (mk.Budget <= 0 || !(want >= 1)) return 0;
                long pool = (long)Math.Floor(Math.Min((double)mk.Budget, want));
                if (pool <= 0) return 0;
                mk.Pool = pool;
                double phi = 1.0;
                if (want > pool)
                {
                    double lo = 0, hi = 1.0;
                    for (int it = 0; it < 32; it++) { double mid = 0.5 * (lo + hi); if (McCost(el, mid) <= pool) lo = mid; else hi = mid; }
                    phi = lo;
                }
                mk.Level = phi;
                mk.W0 = (long)k.KingdomBudgetWallet;
                // plan gotowki (razem najwyzej pula), potem wyplata
                var extra = new long[el.Count]; var nbs = new double[el.Count];
                long planned = 0;
                for (int i = 0; i < el.Count; i++)
                {
                    var m = el[i]; var x = m.X;
                    double a1, b1; long c1;
                    NaAt(x, phi, step, out a1, out b1, out c1);
                    long had; _naPaid.TryGetValue(x.C, out had);
                    double ob; _naBoost.TryGetValue(x.C, out ob);
                    long e = (long)Math.Floor(m.F * Math.Max(0, c1 - had));             // tylko za ludzi, ktorzy dzis sa - czesc f ponad gotowke korony tej doby
                    if (e > pool - planned) e = pool - planned;
                    if (e < 0) e = 0;
                    extra[i] = e; planned += e;
                    nbs[i] = ob + m.F * Math.Max(0, b1 - ob);                            // przydzial W5 + czesc f przydzialu ponad W5 (gotowka + miejsce na krok werbunku)
                }
                for (int i = 0; i < el.Count; i++)
                {
                    var m = el[i]; var x = m.X;
                    try
                    {
                        long had; _naPaid.TryGetValue(x.C, out had);
                        long e = extra[i];
                        long wallet = Math.Max(0, (long)k.KingdomBudgetWallet);
                        if (e > wallet) e = wallet;
                        if (e > int.MaxValue) e = int.MaxValue;
                        if (e > 0)
                        {
                            int ip = (int)e;
                            var h = x.C.Leader;
                            k.KingdomBudgetWallet -= ip;                                     // najpierw skarbiec, potem glowa - dostaje dokladnie tyle, ile zeszlo
                            try { h.ChangeHeroGold(ip); } catch { k.KingdomBudgetWallet += ip; throw; }   // wyjatek - zloto wraca do skarbca (nic w nicosc)
                            paid += ip; mk.Paid += ip; mk.PaidN++;
                            long cc; _naCash.TryGetValue(x.C, out cc); _naCash[x.C] = cc + ip;   // nastepny zwrot 70% zdejmuje ja z podstawy (1 zl = 1 zl zoldu)
                            _naPaid[x.C] = had + ip;                                             // gotowka korony tej doby razem (ClanBudget.AidCash, CSV doplata_w5)
                            long mp; _mcPaid.TryGetValue(x.C, out mp); _mcPaid[x.C] = mp + ip;
                            _mcCashB[m.Band] += ip; _mcPaidB[m.Band]++;
                            if (x.W <= x.Pc0) _mcNotAtCap++;                                    // kontrola: gotowka tylko przy zoldzie druzyn ponad pulap wlasny (oczekiwane 0)
                            if (ip > m.F * Math.Max(0, x.W - x.Pc0) + 1) _mcOverShort++;         // kontrola: nigdy ponad f x brak zoldu ludzi, ktorzy sa (oczekiwane 0)
                            if (m.Band == NbAbove) _mcRichPaid++;                               // kontrola: ponad 2R nic (oczekiwane 0)
                            if (x.Pl) _mcPlGot += ip;
                            CirculationWindows.NoteHeroGold(h, ip);                             // paczka 169b: glowa poza swiatem - zloto wyszlo ze swiata (tylko licznik)
                            had += ip;
                        }
                        // przydzial w pulapie: nigdy ponizej gotowki; tylko gdy rosnie (rod gracza - bez pulapu 166, przydzial bez znaczenia dla budzetu)
                        double nb = Math.Max(nbs[i], had), ob;
                        _naBoost.TryGetValue(x.C, out ob);
                        if (nb > ob)
                        {
                            _naBoost[x.C] = nb; _mcBoost[x.C] = nb - ob;
                            mk.Boost += nb - ob; mk.BoostN++; _mcBoostB[m.Band] += nb - ob;
                        }
                    }
                    catch (Exception e) { McStumble("MidAidStep(wyplata)", e); }
                }
            }
            catch (Exception e) { McStumble("MidAidStep", e); }
            finally
            {
                if (mk != null)
                {
                    try
                    {
                        mk.W1 = (long)k.KingdomBudgetWallet;
                        if (mk.W0 == 0 && paid == 0) mk.W0 = mk.W1;
                        if (paid > 0)
                        {
                            var dk = CrownIncome.DayOf(k);
                            if (dk != null) dk.MidAid += paid;                               // poza wplywami dnia (Spend / Left) - z zapasu skarbca, jak W5b
                            // kontrola niezalezna od rachunku puli: skarbiec bez reszty wplywow dnia (pojdzie jeszcze w renty 180; doplata z daniny jeszcze w niej siedzi)
                            // nie ponizej zapasu wojny i dlugow (ta sama miara co nadwyzka W5b)
                            long leftNow = Math.Max(0, CrownIncome.LeftFor(k) - Math.Max(0, aidPaid));
                            double free = (double)k.KingdomBudgetWallet - leftNow - row.CrownRes - CrownBorrow.DebtOf(k) - TownFolk.CrownTownDebt(k);
                            if (free < 0) _mcBelow++;
                        }
                    }
                    catch (Exception e) { McStumble("MidAidStep(ksiega)", e); }
                }
            }
            return paid;
        }

        /// <summary>Suma f x brak ponad W5 przy poziomie phi (NaCost - ten sam wzor co W5; A - koszt przy poziomie W5). Rosnie z phi.</summary>
        private static double McCost(List<McC> el, double phi)
        {
            double sum = 0;
            for (int i = 0; i < el.Count; i++) { var m = el[i]; sum += m.F * Math.Max(0, NaCost(m.X, phi) - m.A); }
            return sum;
        }

        /// <summary>ClanBudget.Daily (pozyczki wojenne tej doby - KW Banku, od ludu, miejska): gotowka W5c rodu tej doby - zold, ktory juz oplacil skarbiec; brak do
        /// pozyczki liczy sie bez niej. 0 - bez wyplaty, klucz wylaczony albo krok dzis nie biegl.</summary>
        internal static long MidAidCashOf(Clan c)
        {
            try
            {
                long p;
                if (c == null || Campaign.Current == null || _naDay != (int)CampaignTime.Now.ToDays || !_mcPaid.TryGetValue(c, out p)) return 0;
                return p > 0 ? p : 0;
            }
            catch { return 0; }
        }

        /// <summary>Kolumny budzet-rodow.csv (ostatnie - czytniki po nazwach): gotowka W5c tej doby (czesc doplata_w5), podniesienie pulapu ponad W5 i mnoznik potrzeby f
        /// (rody ocenione przez W5c).</summary>
        internal const string MidAidCsvHeader = ";doplata_srednim_w5c;podniesienie_pulapu_w5c;mnoznik_w5c";

        internal static string MidAidCsv(Clan c)
        {
            try
            {
                if (c == null || Campaign.Current == null || _naDay != (int)CampaignTime.Now.ToDays) return ";;;";
                long p; double b, f;
                bool hp = _mcPaid.TryGetValue(c, out p), hb = _mcBoost.TryGetValue(c, out b), hf = _mcF.TryGetValue(c, out f);
                if (!hp && !hb && !hf) return ";;;";
                return ";" + (hp ? p : 0).ToString(WtInv) + ";" + Math.Round(hb ? b : 0).ToString("0", WtInv) + ";" + (hf ? f.ToString("0.0000", WtInv) : "");
            }
            catch { return ";;;"; }
        }

        /// <summary>Segment linii W5 (przed " | na krolestwo: "): gotowka W5c tej doby - suma kolumny doplata_w5 = W5 + W5b + W6 + M1 + W5c (checker W6b-1). Klucz
        /// wylaczony i nic nie wyplacone - pusty (linia W5 jak przed paczka).</summary>
        private static string MidAidSeg()
        {
            try
            {
                var s = Settings.Current;
                long cash = 0; int n = 0; double boost = 0;
                foreach (var r in _mcRows) { cash += r.Paid; n += r.PaidN; boost += r.Boost; }
                if ((s == null || !s.CrownWarAidMiddle) && cash <= 0) return "";   // klucz wylaczony - linia W5 co do znaku jak przed paczka
                return " | dla srednich (W5c, Crown War Aid Middle " + (s != null && s.CrownWarAidMiddle ? (MidAidOn ? "tak" : "tak, ale W5b nieczynne - 0") : "nie") + "): wyplacono " + cash
                       + " (rodow " + n + "), podniesienie pulapu ponad W5 " + Math.Round(boost).ToString("0", WtInv);
            }
            catch (Exception e) { McStumble("MidAidSeg", e); return " | dla srednich (W5c): blad odczytu"; }
        }

        // ------------------------------------------------------------ doba: linia "Skarbiec dla srednich (W5c)" - z NeedAidDay, zaraz po linii W5
        private static void MidAidDay(Settings s, int today)
        {
            LastMidAid = 0;
            try
            {
                long cash = 0, budget = 0, pool = 0, w0 = 0, w1 = 0, sur = 0, res = 0; double want = 0, boost = 0; int kRow = 0, kBud = 0, kPool = 0, kPaid = 0, elig = 0, paidN = 0, boostN = 0;
                var parts = new List<KeyValuePair<long, string>>();
                for (int i = 0; i < _mcRows.Count; i++)
                {
                    var r = _mcRows[i];
                    kRow++; cash += r.Paid; budget += r.Budget; pool += r.Pool; want += r.Want; boost += r.Boost; elig += r.Elig; paidN += r.PaidN; boostN += r.BoostN;
                    w0 += r.W0; w1 += r.W1; sur += Math.Max(0, r.Sur); res += Math.Max(0, r.Res);
                    if (r.Budget > 0) kBud++;
                    if (r.Pool > 0) kPool++;
                    if (r.Paid > 0) kPaid++;
                    if (r.Elig <= 0 && r.Pool <= 0) continue;
                    parts.Add(new KeyValuePair<long, string>(r.Paid, r.Name + ": budzet " + r.Budget + " (nadwyzka " + r.Sur + ", zapas wojny " + r.Res + "), rodow " + r.Elig
                              + ", chec " + Math.Round(r.Want).ToString("0", WtInv) + ", pula " + r.Pool + ", poziom " + (r.Level >= 0 ? (100 * r.Level).ToString("0.0", WtInv) + "%" : "-")
                              + ", wyplacono " + r.Paid + " (rodow " + r.PaidN + "), podniesienie " + Math.Round(r.Boost).ToString("0", WtInv) + ", skarbiec " + r.W0 + " -> " + r.W1));
                }
                LastMidAid = cash;
                // kontrola niezalezna od wierszy: gotowka W5c tylko rodom krolestw w wojnie (oczekiwane 0)
                int inPeace = 0;
                foreach (var kv in _mcPaid) { var c = kv.Key; if (kv.Value > 0 && (c == null || c.Kingdom == null || !AtWar(c.Kingdom))) inPeace++; }
                // gracz: wyjasnienie (po angielsku) przy pierwszej dobie z gotowka W5c, potem najwyzej raz na 30 dob gry (PlTellDue - pamiec procesu jak W6b); suma tygodnia
                // idzie w komunikacie W5 gracza (PlWeek: got = _naPaid, w tym W5c)
                if (_mcPlGot > 0 && PlTellDue(ref _plToldMidDay, today))
                    Log.Player("War aid for houses of modest means: in war the crown's treasury also pays a share of the wages of your companies above "
                               + (100 * NaTarget(s)).ToString("0", WtInv) + "% of their size, from the coin it holds above its war reserve - the whole lack while your house's gold is below its war reserve, "
                               + "a smaller share the richer your house, and nothing above twice the reserve. It pays only for men already in your companies.");
                if (!s.LogEnabled) return;
                parts.Sort((x, y) => y.Key.CompareTo(x.Key));
                var txt = new List<string>(); foreach (var p in parts) txt.Add(p.Value);
                string[] bn = { "ponizej R (f = 1)", "R-2R (f = (2R - G) / R)", "ponad 2R (nic)", "bez miary - dluznik 168 (f = 1, jak f6/f7)" };
                var sb = new StringBuilder(2048);
                sb.Append("Skarbiec dla srednich (W5c): dzien ").Append(today)
                  .Append(" | klucz Crown War Aid Middle ").Append(s.CrownWarAidMiddle ? (MidAidOn ? "tak" : "tak, ale W5b (Crown Surplus Feeds Need Aid) albo W5 nieczynne - W5c 0") : "nie (W5c 0 - jak przed paczka)")
                  .Append(" | regula: ponad W5 (druzyny do ").Append((100 * NaTarget(s)).ToString("0", WtInv)).Append("% limitu) skarbiec ponad zapas wojny placi f x brak zoldu druzyn do pelnego limitu")
                  .Append(", f jak zwrot f6 i renty f7; budzet = 1/").Append(CsDays(s).ToString("0", WtInv)).Append(" nadwyzki skarbca minus pula W5b; gotowka tylko za ludzi, ktorzy sa, reszta - miejsce na krok werbunku ")
                  .Append((100 * NaStep(s)).ToString("0", WtInv)).Append("% limitu x f")
                  .Append(" | krolestw w wojnie z krokiem ").Append(kRow).Append(", z budzetem ").Append(kBud).Append(", z pula ").Append(kPool).Append(", z wyplata ").Append(kPaid)
                  .Append(" | nadwyzka skarbcow razem ").Append(sur).Append(", zapas wojny razem ").Append(res)
                  .Append(" | budzet ").Append(budget).Append(", chec (f x brak ponad W5 przy pelnym limicie) ").Append(Math.Round(want).ToString("0", WtInv)).Append(", pula ").Append(pool)
                  .Append(" | wyplacono ").Append(cash).Append(" (rodow ").Append(paidN).Append(" z ").Append(elig).Append(" ocenionych), podniesienie pulapu ponad W5 ").Append(Math.Round(boost).ToString("0", WtInv))
                  .Append(" u ").Append(boostN).Append(" rodow");
                for (int b = 0; b < McBands; b++)
                {
                    if (b == NbAbove) { sb.Append(" | ").Append(bn[b]).Append(": pominiete ").Append(_mcRichSkip).Append(", gotowka ").Append(_mcCashB[b]); continue; }
                    sb.Append(" | ").Append(bn[b]).Append(": rodow ").Append(_mcEligB[b]).Append(", z gotowka ").Append(_mcPaidB[b]).Append(", gotowka ").Append(_mcCashB[b])
                      .Append(", podniesienie ").Append(Math.Round(_mcBoostB[b]).ToString("0", WtInv));
                }
                sb.Append(" | pominiete: luz w pulapie wlasnym ").Append(_mcSlackSkip).Append(", platnicy tarczowego tej doby ").Append(_mcScutSkip)
                  .Append(" | skarbce w kroku W5c: przed ").Append(w0).Append(", po ").Append(w1).Append(", ubylo ").Append(w0 - w1).Append(" (= wyplacono)")
                  .Append(" | kontrola: ponizej zapasu wojny po wyplacie ").Append(_mcBelow).Append(", gotowka przy zoldzie druzyn w pulapie wlasnym ").Append(_mcNotAtCap)
                  .Append(", ponad f x brak ").Append(_mcOverShort).Append(", ponad 2R ").Append(_mcRichPaid).Append(", w pokoju ").Append(inPeace).Append(" (oczekiwane 0)")
                  .Append(" | gracz: ").Append(_mcPlBand >= 0 ? "pasmo " + bn[_mcPlBand] + ", f " + _mcPlF.ToString("0.00", WtInv) + ", dostal " + _mcPlGot : "nie oceniony (bez kroku W5, poza pasmem albo z luzem)")
                  .Append(" | na krolestwo: ").Append(txt.Count > 0 ? string.Join(" ; ", txt.ToArray()) : "-")
                  .Append(" | potkniecia ").Append(_mcStumbles).Append('.');
                Log.Info(sb.ToString());
            }
            catch (Exception e) { McStumble("MidAidDay", e); }
            finally { _mcStumbles = 0; }
        }
    }
}
