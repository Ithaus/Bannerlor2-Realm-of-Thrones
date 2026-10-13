using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;

namespace Armoury
{
    /// <summary>
    /// W6 (decyzja Jeffa 13.10 po biegu rocznym: wojska lordow 79 tys. przy progu 85 tys., "pieniadz na wojsko ma przyjsc z podatkow na czas wojny"; kierunek 2 z 3:
    /// "TARCZOWE - rod, ktory w wojnie trzyma mniej wojska, niz udzwigna jego ziemie, placi koronie czesc zaoszczedzonego zoldu, a korona tego samego dnia
    /// daje to rodom walczacym") - DRUGIE ZRODLO GARNKA "WEDLUG POTRZEBY" (W5, KingdomTreasury.NeedAid.cs). Wylacznik Scutage (wylaczony = stan sprzed paczki co
    /// do monety), stawka ScutageRate (0.25 podstawy). Czynne tylko z W5 (CrownWarAidByNeed, renty 180, budzet 166); danina P1 NIE jest potrzebna - krolestwo
    /// bez daniny (s = 0, P1 wylaczone) ma garnek z samego tarczowego.
    ///  - KTO PLACI: rod AI krolestwa w wojnie z lennem (wiersz rent 180 - warunek rent NIE jest wymagany: rod, ktory nie sluzy, placi tym bardziej), z budzetem
    ///    166 w wojnie i czynna druzyna, z kiesa rodziny ponad rezerwe wojenna. Bez rodu gracza (gracz nie placi i nie dostaje - osobna decyzja), bez Nocnej
    ///    Strazy (sluzy bez zoldu), bez kompanii najemnych, bez rodow rycerzy 179 (sluza osobiscie w druzynie pana), bez dluznikow i rodow w zajeciu (168).
    ///  - PODSTAWA (lustro potrzeby W5): pulap WLASNY druzyn z wczorajszego budzetu - bez udzialu dworu (rod z luzem placi dwor w calosci, to nie jest
    ///    zaoszczedzony zold), bez kredytu wojennego i bez przydzialu korony (ClanBudget.ScutageCap) - PONAD zold druzyn doprowadzonych do celu W5
    ///    (CrownWarAidFillTarget, 60% limitu): zold ludzi, ktorzy dzis sa, + zold brakujacych do celu po stawce rodu. Dla rodu na celu i wyzej to dokladnie
    ///    niewykorzystany pulap druzyn; rod ponizej celu (np. po przegranej bitwie) nie placi za dziure, ktora wlasnie odbudowuje - swieze straty bez osobnej
    ///    miary i bez stanu. Podstawa > 0 oznacza, ze rod NIE ma potrzeby W5 (jego pulap z dworem tym bardziej starcza na cel) - platnik nigdy nie jest odbiorca.
    ///    Rod, ktory zwerbuje ponad cel, placi mniej (podstawa maleje o zold nowych ludzi) - jedyna zacheta.
    ///  - ILE: nalezne = ScutageRate x podstawa, ale (i) najwyzej 1 / WarChestDays (45) nadwyzki kiesy rodziny ponad rezerwe wojenna na dobe (ten sam rytm,
    ///    ktorym budzet sam uwalnia nadwyzke), wiec nigdy ponizej rezerwy; (ii) kiesa glowy nie schodzi ponizej FamilyPurseFloor (jak dwor); (iii) pulap
    ///    platnika po poborze nie schodzi ponizej zoldu druzyn przy celu (WarChestToWages x pobor / WarChestDays najwyzej podstawa - przy stawce do 1 nigdy nie
    ///    wiaze; zostaje jako bezpiecznik) - platnik nie zwalnia przez tarczowe wlasnych ludzi i nie staje sie odbiorca W5.
    ///  - KORONA BIERZE TYLE, ILE POTRZEBA: krolestwo po krolestwie. Niepokryta potrzeba = potrzeba W5 rodow z warunkiem rent minus garnek daniny tej doby.
    ///    Garnek z tarczowym = danina + min(nalezne razem, niepokryta potrzeba); z niego nowy poziom wyrownania W5 (ta sama regula: od najslabszych, do celu).
    ///    POBRANE = gotowka, ktora W5 wyplaca przy nowym poziomie PONAD szczebel daniny (tylko za ludzi, ktorzy dzis sa w druzynach) - co do zlotowki, dzielone
    ///    miedzy platnikow proporcjonalnie do naleznego. Reszta przydzialu to MIEJSCE w pulapie na krok werbunku (bez gotowki - jak w W5): za ludzi, ktorych
    ///    jeszcze nie ma, nikt nie placi i nic nie jest pobierane. Nikt nie potrzebuje - tarczowe 0. Nic nie zostaje w skarbcu, renty 180 bez zmian.
    ///  - KOLEJNOSC (w kroku krolestwa W5, wolanym z CrownRents.Daily po warunkach rent, przed podzialem rent): potrzeba W5 -> platnicy i nalezne (sam rachunek)
    ///    -> szczebel daniny W5 (bez zmian) -> pobor tarczowego (glowa platnika -> skarbiec) -> wyplata szczebla tarczowego (skarbiec -> glowa odbiorcy) ->
    ///    renty 180 z reszty wplywow dnia (tarczowe nie wchodzi do wplywow dnia: wplynelo i wyszlo) -> ClanBudget.Daily (pulap odbiorcy z przydzialem, kiesa
    ///    platnika juz po poborze).
    /// Platnik -> odbiorca: kiesa glowy rodu platnika -> Kingdom.KingdomBudgetWallet -> kiesa glowy rodu z doplata W5 (ta sama doba, ta sama kwota). Gotowka z
    /// tarczowego jest gotowka W5: znaczona na druzyny (przydzial w pulapie, nie w D), poza podstawa zwrotu 70% nastepnej doby, poza zoldem, ktoremu ustepuje
    /// dwor. Zapis gry: nic nowego (stan doby w pamieci sesji). Linia doby "Tarczowe (W6): dzien N" (z ArmouryBehavior po linii W5); kolumny budzet-rodow.csv
    /// tarczowe_w6, podstawa_tarczowego_w6, doplata_z_tarczowego_w6, nadwyzka_ponad_rezerwe_w6 (recenzja: nadwyzka kiesy platnika ponad rezerwe BUDZETU przed
    /// poborem - kolumna R tego pliku to rezerwa od dochodu z 28 dob ksiegi, zwykle wyzsza niz rezerwa budzetu od dochodu stalego, wiec nie jest miara reguly).
    /// Wyjatek per krolestwo / per rod - licznik potkniec (pierwszy do pliku), bez globalnych wylacznikow (CLAUDE.md 7).
    /// </summary>
    internal static partial class KingdomTreasury
    {
        private sealed class ScP { public Clan C; public double Base, Sur, Reserve; public long Due, Paid; }
        private sealed class ScK
        {
            public string Name = ""; public readonly List<ScP> P = new List<ScP>();
            public long Due, Room, Got, Paid; public double Base, Sur, Uncovered, Level0 = -1, Level1 = -1, Boost;
            public int PayN, PaidN, RecN, NewN, NeedN;   // platnicy z naleznym / z poborem; odbiorcy z gotowka tarczowego / w tym bez gotowki z daniny; rody z potrzeba W5
        }

        private static readonly Dictionary<Clan, long> _scPaid = new Dictionary<Clan, long>();       // platnik -> tarczowe tej doby (CSV)
        private static readonly Dictionary<Clan, double> _scBase = new Dictionary<Clan, double>();   // platnik -> podstawa tej doby (CSV)
        private static readonly Dictionary<Clan, double> _scSur = new Dictionary<Clan, double>();    // platnik -> nadwyzka kiesy rodziny ponad rezerwe wojenna budzetu przed poborem (CSV)
        private static readonly Dictionary<Clan, long> _scCash = new Dictionary<Clan, long>();       // odbiorca -> gotowka W5 ze szczebla tarczowego tej doby (CSV)
        private static readonly List<ScK> _scRows = new List<ScK>();
        private static readonly HashSet<string> _scErr = new HashSet<string>();
        private static int _scStumbles;
        // liczniki doby (linia): oceniane rody z lennem i powody pominiecia, ograniczenia naleznego, kontrole (oczekiwane 0)
        private static int _scSeen, _scNoBudget, _scNoParty, _scNoBase, _scNeed, _scNoSurplus, _scNoDue, _scCapSur, _scCapHead, _scCapCeil;
        private static int _scPayerAided, _scBelowReserve, _scNotAtCap;
        /// <summary>"Obieg" (rody i korona): tarczowe pobrane w tej dobie (glowy platnikow -> skarbce) i wyplacone przez W5 ponad danine (skarbce -> glowy rodow).</summary>
        internal static long LastScutageIn, LastScutageOut;

        internal static bool ScutageOn { get { var s = Settings.Current; return s != null && s.Scutage && NeedAidOn; } }

        private static double ScRate(Settings s) { return WtClamp(s.ScutageRate, 0, 1); }

        private static void ScutageReset()
        {
            ScClearDay(); _scErr.Clear(); _scStumbles = 0; LastScutageIn = LastScutageOut = 0;
        }

        /// <summary>Nowa doba kroku W5 (NaRoll): stan doby tarczowego znika razem z przydzialami W5.</summary>
        private static void ScClearDay()
        {
            _scPaid.Clear(); _scBase.Clear(); _scSur.Clear(); _scCash.Clear(); _scRows.Clear();
            _scSeen = _scNoBudget = _scNoParty = _scNoBase = _scNeed = _scNoSurplus = _scNoDue = _scCapSur = _scCapHead = _scCapCeil = 0;
            _scPayerAided = _scBelowReserve = _scNotAtCap = 0;
        }

        private static void ScStumble(string where, Exception e)
        {
            _scStumbles++;
            try { if (_scErr.Add(where)) Log.Error("KingdomTreasury.Scutage." + where, e); } catch { }
        }

        /// <summary>Przydzial, podniesienie pulapu i gotowka rodu przy poziomie f - ten sam rachunek co petla wyplaty W5 (NeedAidKingdom), bez zlota.</summary>
        private static void NaAt(NaClan x, double f, double step, out double alloc, out double boost, out long cash)
        {
            alloc = NaCost(x, f); boost = 0; cash = 0;
            if (!(alloc > 0)) return;
            double funded = NaFunded(x, f);
            double room = x.M < f * x.L ? Math.Min(step * x.L, f * x.L - x.M) * x.P : 0;
            boost = Math.Min(alloc, Math.Max(0, funded + room - x.Pc0));
            cash = (long)Math.Floor(Math.Max(0, funded - x.Pc0));
        }

        /// <summary>
        /// W6 - platnicy krolestwa i nalezne tej doby (SAM RACHUNEK, bez zlota). Wola NeedAidKingdom po policzeniu potrzeby W5, przed jakakolwiek wyplata.
        /// list - rody z wierszem rent 180 (z lennem). null - klucz wylaczony. Nie rzuca (kazdy rod we wlasnym try).
        /// </summary>
        private static ScK ScutagePlan(Kingdom k, List<Clan> list, Settings s, double target, float avgWage)
        {
            if (!ScutageOn) return null;
            var sc = new ScK { Name = k.Name != null ? k.Name.ToString() : k.StringId };
            _scRows.Add(sc);
            try
            {
                double rate = ScRate(s);
                if (!(rate > 0)) return sc;
                double chestDays = Math.Max(1f, s.WarChestDays), toWages = Math.Max(0f, s.WarChestToWages);
                int floor = Math.Max(0, s.FamilyPurseFloor);
                for (int i = 0; i < list.Count; i++)
                {
                    var c = list[i];
                    try
                    {
                        if (c == null || c == Clan.PlayerClan || c.Leader == null || !c.Leader.IsAlive) continue;
                        _scSeen++;
                        double pc, reserve;
                        if (!ClanBudget.ScutageCap(c, out pc, out reserve)) { _scNoBudget++; continue; }   // bez budzetu 166 w wojnie, Straz bez zoldu, najemnik, dluznik, zajecie
                        int limit, men; long wage;
                        if (!NaParties(c, out limit, out men, out wage)) { _scNoParty++; continue; }       // bez czynnej druzyny (niewola, brak dowodcy, rod rycerza 179)
                        double perMan = men >= 20 && wage > 0 ? (double)wage / men : avgWage;
                        // zold druzyn doprowadzonych do celu W5: ludzie, ktorzy dzis sa, + brakujacy do celu po stawce rodu (ponad cel - sam zold dzisiejszy)
                        double goal = wage + Math.Max(0, target * limit - men) * perMan;
                        double bas = pc - goal;
                        if (!(bas > 0))
                        {
                            _scNoBase++;
                            double pc0;   // pomiar: ile z nich ma potrzebe W5 (ten sam rachunek co kandydat W5, bez warunku rent)
                            if (ClanBudget.PartyCapOwn(c, out pc0) && NaCost(new NaClan { C = c, Pc0 = pc0, W = wage, M = men, L = limit, P = perMan }, target) > 0) _scNeed++;
                            continue;
                        }
                        double sur = ClanIncomeBook.FamilyGoldOf(c) - reserve;
                        if (!(sur > 0)) { _scNoSurplus++; continue; }                                        // kiesa rodziny nie ponad rezerwe wojenna - nie placi
                        double due = rate * bas;
                        double capSur = sur / chestDays;                                                     // rytm, ktorym budzet sam uwalnia nadwyzke - nigdy ponizej rezerwy
                        if (due > capSur) { due = capSur; _scCapSur++; }
                        if (toWages > 0)
                        {
                            double capCeil = bas * chestDays / toWages;                                      // pulap po poborze nie ponizej zoldu druzyn przy celu
                            if (due > capCeil) { due = capCeil; _scCapCeil++; }
                        }
                        long head = Math.Max(0, (long)c.Leader.Gold - floor);                                // kiesa glowy nie ponizej podlogi rodziny (jak dwor)
                        if (due > head) { due = head; _scCapHead++; }
                        long d = (long)Math.Floor(due);
                        if (d <= 0) { _scNoDue++; continue; }
                        sc.P.Add(new ScP { C = c, Base = bas, Sur = sur, Reserve = reserve, Due = d });
                        sc.Due += d; sc.Base += bas; sc.Sur += sur; sc.PayN++;
                        _scBase[c] = bas; _scSur[c] = sur;
                    }
                    catch (Exception e) { ScStumble("ScutagePlan(rod)", e); }
                }
            }
            catch (Exception e) { ScStumble("ScutagePlan", e); }
            return sc;
        }

        /// <summary>Pobor: dokladnie `want` zl (najwyzej nalezne razem) od platnikow krolestwa proporcjonalnie do naleznego - podloga udzialu, reszta po 1 zl
        /// platnikom, ktorym zostalo miejsce w naleznym. Glowa platnika -> skarbiec (najpierw glowa, potem skarbiec; wyjatek = zloto wraca glowie). Zwraca
        /// faktycznie pobrane. Nie rzuca.</summary>
        private static long ScCollect(Kingdom k, ScK sc, long want)
        {
            long got = 0;
            try
            {
                if (want > sc.Due) want = sc.Due;
                if (want <= 0 || sc.Due <= 0) return 0;
                int n = sc.P.Count;
                var share = new long[n];
                long sum = 0;
                for (int j = 0; j < n; j++) { share[j] = want * sc.P[j].Due / sc.Due; sum += share[j]; }
                for (int j = 0; j < n && sum < want; j++) if (share[j] < sc.P[j].Due) { share[j]++; sum++; }
                for (int j = 0; j < n; j++)
                {
                    var p = sc.P[j];
                    try
                    {
                        if (share[j] <= 0) continue;
                        var h = p.C.Leader;
                        if (h == null || !h.IsAlive) continue;
                        int ip = (int)Math.Min(int.MaxValue, share[j]);
                        h.ChangeHeroGold(-ip);                            // najpierw platnik, potem skarbiec - skarbiec dostaje dokladnie tyle, ile zeszlo
                        try { k.KingdomBudgetWallet += ip; } catch { h.ChangeHeroGold(ip); throw; }   // wyjatek - zloto wraca platnikowi (nic w nicosc)
                        got += ip; p.Paid = ip; sc.PaidN++;
                        _scPaid[p.C] = ip;
                        CirculationWindows.NoteHeroGold(h, -ip);          // paczka 169b: glowa poza swiatem (Disabled) - zloto weszlo do swiata (tylko licznik)
                        if (ClanIncomeBook.FamilyGoldOf(p.C) < p.Reserve) _scBelowReserve++;   // kontrola: po poborze kiesa rodziny nie ponizej rezerwy (oczekiwane 0)
                    }
                    catch (Exception e) { ScStumble("ScCollect(platnik)", e); }
                }
            }
            catch (Exception e) { ScStumble("ScCollect", e); }
            return got;
        }

        /// <summary>
        /// W6 - szczebel tarczowego kroku krolestwa (wola NeedAidKingdom PO petli wyplaty szczebla daniny W5): niepokryta potrzeba -> poziom wyrownania z
        /// garnkiem danina + tarczowe -> pobor dokladnie tej gotowki, ktora W5 wyplaca przy nowym poziomie ponad szczebel daniny -> wyplata skarbiec -> glowa
        /// (ta sama para co W5) i przydzial w pulapie (gotowka + miejsce na krok werbunku). cand - rody z potrzeba W5 (te same obiekty co w kroku W5),
        /// level0 - poziom szczebla daniny. Nie rzuca; ksiegowanie w finally (pobrane i wyplacone do linii 165 takze po wyjatku).
        /// </summary>
        private static void ScutageStep(Kingdom k, List<NaClan> cand, NaK row, ScK sc, double level0, double target, double step, int today)
        {
            long got = 0, paid = 0;
            try
            {
                sc.NeedN = cand.Count;
                sc.Uncovered = Math.Max(0, row.Need - row.Pot);
                sc.Level0 = level0; sc.Level1 = level0;
                long room = (long)Math.Floor(Math.Min((double)sc.Due, sc.Uncovered));   // KORONA BIERZE TYLE, ILE POTRZEBA: najwyzej niepokryta potrzeba po daninie
                sc.Room = room;
                if (room <= 0 || cand.Count == 0) return;
                // poziom wyrownania z garnkiem danina + tarczowe: ta sama regula co W5 (najwyzszy f <= cel, przy ktorym suma kosztow miesci sie w garnku)
                double pot1 = row.Pot + room, level1 = target;
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
                sc.Level1 = level1;
                // plan: gotowka rodu przy nowym poziomie ponad gotowke szczebla daniny (tylko za ludzi, ktorzy dzis sa) - razem najwyzej `room`
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
                // pobor: dokladnie tyle, ile W5 wyplaci ponad danine (za ludzi, ktorych jeszcze nie ma, nic nie jest pobierane - rod dostaje samo miejsce w pulapie)
                if (want > 0) got = ScCollect(k, sc, want);
                long left = got;
                for (int i = 0; i < cand.Count; i++)
                {
                    var x = cand[i];
                    try
                    {
                        long had; _naPaid.TryGetValue(x.C, out had);
                        long e = Math.Min(extra[i], left);
                        if (e > 0)
                        {
                            int ip = (int)Math.Min(int.MaxValue, e);
                            var h = x.C.Leader;
                            k.KingdomBudgetWallet -= ip;                  // najpierw skarbiec, potem glowa - dostaje dokladnie tyle, ile zeszlo
                            try { h.ChangeHeroGold(ip); } catch { k.KingdomBudgetWallet += ip; throw; }   // wyjatek - zloto zostaje w skarbcu (licznik "zostalo w skarbcach")
                            left -= ip; paid += ip; sc.RecN++; if (had <= 0) sc.NewN++;
                            long cc; _naCash.TryGetValue(x.C, out cc); _naCash[x.C] = cc + ip;   // gotowka W5: nastepny zwrot 70% zdejmuje ja z podstawy
                            _naPaid[x.C] = had + ip;                                             // gotowka korony tej doby razem (ClanBudget.AidCash, CSV doplata_w5)
                            long sp; _scCash.TryGetValue(x.C, out sp); _scCash[x.C] = sp + ip;
                            if (x.W <= x.Pc0) _scNotAtCap++;              // kontrola: gotowka tylko rodom, ktorych zold druzyn przekracza pulap wlasny (oczekiwane 0)
                            CirculationWindows.NoteHeroGold(h, ip);       // paczka 169b: glowa poza swiatem (Disabled) - zloto wyszlo ze swiata (tylko licznik)
                            had += ip;
                        }
                        // przydzial w pulapie przy nowym poziomie (gotowka + miejsce na krok werbunku), nigdy ponizej gotowki; tylko gdy rosnie
                        double nb = Math.Max(boost1[i], had), ob;
                        _naBoost.TryGetValue(x.C, out ob);
                        if (nb > ob)
                        {
                            _naBoost[x.C] = nb; sc.Boost += nb - ob;
                            if (!(ob > 0)) NaMeasure(x, today);           // rod bez przydzialu ze szczebla daniny - do pomiaru "rody z doplata dzis" linii W5
                        }
                    }
                    catch (Exception e) { ScStumble("ScutageStep(wyplata)", e); }
                }
                // kontrola: platnik nie jest odbiorca (oczekiwane 0 - podstawa > 0 wyklucza potrzebe W5)
                for (int j = 0; j < sc.P.Count; j++) if (sc.P[j].Paid > 0 && (_naBoost.ContainsKey(sc.P[j].C) || _naPaid.ContainsKey(sc.P[j].C))) _scPayerAided++;
            }
            catch (Exception e) { ScStumble("ScutageStep", e); }
            finally
            {
                sc.Got = got; sc.Paid = paid;
                if (got > 0 || paid > 0)
                {
                    try { var dk = CrownIncome.DayOf(k); if (dk != null) { dk.ScutIn += got; dk.ScutOut += paid; } }
                    catch (Exception e) { ScStumble("ScutageStep(ksiega)", e); }
                }
            }
        }

        /// <summary>Kolumny budzet-rodow.csv (ostatnie - czytniki po nazwach): tarczowe zaplacone przez rod tej doby, jego podstawa, u odbiorcy czesc
        /// gotowki W5 (kolumna doplata_w5), ktora przyszla z tarczowego, i (recenzja) nadwyzka kiesy rodziny platnika ponad rezerwe wojenna budzetu PRZED
        /// poborem (w dol do zlotowki) - prog "nikt nie placi z rezerwy i nie wiecej niz 1 / WarChestDays nadwyzki" liczony z pliku niezaleznie od linii.</summary>
        internal const string ScutageCsvHeader = ";tarczowe_w6;podstawa_tarczowego_w6;doplata_z_tarczowego_w6;nadwyzka_ponad_rezerwe_w6";

        internal static string ScutageCsv(Clan c)
        {
            try
            {
                if (c == null || Campaign.Current == null || _naDay != (int)CampaignTime.Now.ToDays) return ";;;;";
                long p, r; double b, su;
                bool hp = _scPaid.TryGetValue(c, out p), hb = _scBase.TryGetValue(c, out b), hr = _scCash.TryGetValue(c, out r), hs = _scSur.TryGetValue(c, out su);
                if (!hp && !hb && !hr) return ";;;;";
                return ";" + (hb ? (hp ? p : 0).ToString(WtInv) : "") + ";" + (hb ? Math.Round(b).ToString("0", WtInv) : "") + ";" + (hr ? r.ToString(WtInv) : "")
                       + ";" + (hb && hs ? Math.Floor(su).ToString("0", WtInv) : "");
            }
            catch { return ";;;;"; }
        }

        private static string ScPct(double level) { return level >= 0 ? (100 * level).ToString("0.0", WtInv) + "%" : "-"; }

        // ------------------------------------------------------------ doba: linia "Tarczowe (W6)" - z ArmouryBehavior, zaraz po linii W5
        internal static void ScutageDay()
        {
            LastScutageIn = LastScutageOut = 0;
            var s = Settings.Current;
            if (s == null || Campaign.Current == null) return;
            int today = (int)CampaignTime.Now.ToDays;
            try
            {
                if (_naDay != today) ScClearDay();                       // krok W5 dzis nie biegl (linia W5 juz przewinela dobe) - bez starych liczb
                long due = 0, room = 0, got = 0, paid = 0; double bas = 0, sur = 0, unc = 0, boost = 0;
                int kPay = 0, kUnc = 0, kGot = 0, payN = 0, paidN = 0, recN = 0, newN = 0;
                var parts = new List<KeyValuePair<double, string>>();
                for (int i = 0; i < _scRows.Count; i++)
                {
                    var r = _scRows[i];
                    due += r.Due; room += r.Room; got += r.Got; paid += r.Paid; bas += r.Base; sur += r.Sur; unc += Math.Floor(r.Uncovered); boost += r.Boost;
                    payN += r.PayN; paidN += r.PaidN; recN += r.RecN; newN += r.NewN;
                    if (r.PayN > 0) kPay++;
                    if (r.Uncovered >= 1) kUnc++;
                    if (r.Got > 0) kGot++;
                    if (r.PayN <= 0 && r.Uncovered < 1) continue;
                    parts.Add(new KeyValuePair<double, string>(r.Uncovered, r.Name + ": platnikow " + r.PayN + ", nalezne " + r.Due + ", niepokryta potrzeba "
                              + Math.Floor(r.Uncovered).ToString("0", WtInv) + " u " + r.NeedN + " rodow, pobrane " + r.Got + ", wyplacone " + r.Paid + " (rodow " + r.RecN
                              + "), poziom " + ScPct(r.Level0) + " -> " + ScPct(r.Level1)));
                }
                LastScutageIn = got; LastScutageOut = paid;
                if (s.LogEnabled)
                {
                    parts.Sort((x, y) => y.Key.CompareTo(x.Key));
                    var txt = new List<string>(); foreach (var p in parts) txt.Add(p.Value);
                    var sb = new StringBuilder(2048);
                    sb.Append("Tarczowe (W6): dzien ").Append(today)
                      .Append(" | wylacznik Scutage ").Append(s.Scutage ? "tak" : "nie (tarczowe 0 - jak przed paczka)")
                      .Append(", doplata wedlug potrzeby W5 (Crown War Aid By Need, renty 180, budzet 166) ").Append(NeedAidOn ? (ClanBudget.Pending ? "tak (budzet jeszcze nie policzony w tej sesji - tarczowe od jutra)" : "tak") : "nie (tarczowe 0)")
                      .Append(" | stawka ").Append((100 * ScRate(s)).ToString("0", WtInv)).Append("% podstawy (pulap wlasny druzyn bez dworu, kredytu i przydzialu korony ponad zold druzyn doprowadzonych do celu W5 ")
                      .Append((100 * NaTarget(s)).ToString("0", WtInv)).Append("% limitu), najwyzej 1/").Append(Math.Max(1f, s.WarChestDays).ToString("0", WtInv))
                      .Append(" nadwyzki kiesy rodziny ponad rezerwe wojenna na dobe, glowa nie ponizej ").Append(Math.Max(0, s.FamilyPurseFloor))
                      .Append(" | krolestw w wojnie ").Append(_scRows.Count).Append(", z platnikami ").Append(kPay).Append(", z niepokryta potrzeba W5 po daninie ").Append(kUnc)
                      .Append(", z poborem ").Append(kGot)
                      .Append(" | platnikow ").Append(payN).Append(" z ").Append(_scSeen).Append(" ocenianych rodow z lennem (pominiete - bez budzetu 166 w wojnie, Straz, dluznik albo zajecie: ")
                      .Append(_scNoBudget).Append("; bez czynnej druzyny: ").Append(_scNoParty).Append("; pulap nie ponad zold przy celu: ").Append(_scNoBase).Append(", w tym z potrzeba W5 ").Append(_scNeed)
                      .Append("; kiesa nie ponad rezerwe: ").Append(_scNoSurplus).Append("; nalezne ponizej 1 zl: ").Append(_scNoDue).Append(')')
                      .Append(", podstawa (wolny pulap ponad cel) ").Append(Math.Round(bas).ToString("0", WtInv))
                      .Append(", nadwyzka kies platnikow ponad rezerwe ").Append(Math.Round(sur).ToString("0", WtInv))
                      .Append(", nalezne ").Append(due).Append(" (ograniczone: nadwyzka 1/").Append(Math.Max(1f, s.WarChestDays).ToString("0", WtInv)).Append(" u ").Append(_scCapSur)
                      .Append(", kiesa glowy u ").Append(_scCapHead).Append(", pulap po poborze u ").Append(_scCapCeil).Append(')')
                      .Append(" | niepokryta potrzeba W5 po daninie ").Append(Math.Round(unc).ToString("0", WtInv))
                      .Append(", do pobrania (mniejsze z naleznego i niepokrytej potrzeby, krolestwo po krolestwie) ").Append(room)
                      .Append(" | pobrane ").Append(got).Append(" od ").Append(paidN).Append(" rodow = dopisane do garnka W5 ").Append(got)
                      .Append(", wyplacone przez W5 ponad danine ").Append(paid).Append(" (rodow ").Append(recN).Append(", w tym bez gotowki z daniny ").Append(newN)
                      .Append("), zostalo w skarbcach ").Append(got - paid).Append(" (oczekiwane 0)")
                      .Append(" | podniesienie pulapu druzyn ponad szczebel daniny ").Append(Math.Round(boost).ToString("0", WtInv))
                      .Append(" (w tym miejsce na krok werbunku bez gotowki ").Append(Math.Round(Math.Max(0, boost - paid)).ToString("0", WtInv)).Append(')')
                      .Append(" | kontrola: platnik z doplata W5 ").Append(_scPayerAided).Append(", platnik ponizej rezerwy po poborze ").Append(_scBelowReserve)
                      .Append(", gotowka rodom z zoldem druzyn w pulapie wlasnym ").Append(_scNotAtCap).Append(" (oczekiwane 0)")
                      .Append(" | na krolestwo: ").Append(txt.Count > 0 ? string.Join(" ; ", txt.ToArray()) : "-")
                      .Append(" | potkniecia ").Append(_scStumbles).Append('.');
                    Log.Info(sb.ToString());
                }
            }
            catch (Exception e) { ScStumble("ScutageDay", e); }
            finally { _scStumbles = 0; }
        }
    }
}
