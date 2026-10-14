using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// W7 (decyzja Jeffa 13.10 "tak, stawka wojenna podatku pana"; rachunek 85 tys. z krytyka, rozdz. 0/5/7: w 2.-3. roku i po regule "gospodarka od ludzi"
    /// danina z oszczednosci ludu przesuwa sie do ludnych krolestw, a puste - Polnoc, Zelazne Wyspy, Dorne, Wolni Ludzie, Ibben - traca wojsko; 120 biednych
    /// rodow traci kiesy 15.5 -> 5.8 mln i wojsko 26.1 -> 21.5 tys.) - STAWKA WOJENNA PANA. Klucz LordWarLevy (wylaczony = stan 79664cf co do monety),
    /// gracz - PlayerWarLevy (domyslnie wylaczony: "u gracza to jego decyzja").
    ///  - KTO: rod z planu doby W5 (KingdomTreasury.NeedAid.cs - ta sama miara potrzeby, NIE liczona drugi raz): krolestwo w wojnie, rod z warunkiem rent
    ///    i budzetem 166, ktorego WLASNY pulap druzyn nie starcza na druzyny do celu W5 (CrownWarAidFillTarget, 60% limitu) - i ktoremu wszystkie zrodla W5
    ///    (danina P1, skarbiec ponad zapas wojny W5b, tarczowe W6) nie pokryly potrzeby: niepokryte rodu = koszt przy celu - koszt przy poziomie wyrownania
    ///    W5 po ostatnim szczeblu (NaCost - ten sam wzor). Rod z luzem w pulapie (zold druzyn ponizej 95% pulapu wlasnego druzyn - ta sama miara "na pulapie"
    ///    co ClanBudget.SlackGold) jest ponizej celu nie z powodu pulapu - bez poboru i bez miejsca W7 (recenzja W7). Gracz tylko z kluczem PlayerWarLevy i tylko
    ///    wtedy, gdy W5 go mierzy (W6b: wasal z miastem albo zamkiem) - bez klucza nic nie placi i nic nie dostaje.
    ///  - SKAD (prawdziwe zloto): z WLASNYCH lenn rodu (miasta i zamki wlasciciela) i ich wsi: kiesa ludu (W/R/P, podzamcze) ponad zapas dni PO daninie P1 tej
    ///    doby i ponad oferte nadwyzki na jutro (TownFolk.LevyRoom - ta sama miara "ponad zapas" co P1), kiesa wsi ponad 15 dni zakupow (TownFolk.VillageFloor -
    ///    ta sama co danina wojenna wsi Levies, ktora biegla wczesniej tej doby). Najwyzej LordWarLevyRate (3%) tej nadwyzki na dobe. Kolejnosc doby: P1 (doba
    ///    ludu) i danina wsi (Levies) najpierw, W7 po bloku korony - z tego, co zostalo; zadna zlotowka nie jest brana dwa razy (kontrola w linii).
    ///  - ILE: poziom W7 = najwyzszy f miedzy poziomem W5 a celem, przy ktorym koszt ponad poziom W5 miesci sie w stawce nadwyzki lenn; GOTOWKA = zold ludzi,
    ///    ktorzy dzis SA w druzynach, do tego poziomu, ponad gotowke W5 tej doby (NaAt - ten sam rachunek co szczeble W5); za ludzi, ktorych jeszcze nie ma -
    ///    tylko MIEJSCE w pulapie na krok werbunku (bez poboru). Pobierane jest dokladnie tyle, ile idzie do glowy rodu - nigdy ponad niepokryta potrzebe.
    ///  - DOKAD: kiesa glowy rodu (ChangeHeroGold - poza D: ClanIncomeBook go nie widzi, nie jest dochodem dworu, sprzetu, budow ani limitu Banku) i ZNACZONE
    ///    na druzyny: przydzial (gotowka + miejsce) podnosi pulap partii i rodu w ClanBudget.Daily tej doby razem z przydzialem W5 (b.Aid; gotowka w b.AidCash -
    ///    dwor i niewydany sprzet nie ustepuja zoldowi oplaconemu poborem). Pulap wlasny nastepnej doby (PartyCapOwn, ScutageCap, AtMeans) jest BEZ W7 - W5, W5b
    ///    i W6 nie licza W7 jako swojego garnka. Zold oplacony poborem nie jest podstawa zwrotu 70% (LordLevyRefundCut w WageRefund, jak W5).
    ///    Dlaczego nie skrzynia (0.8 x nadwyzka / 45): biedny rod ma G ponizej R - skrzynia rusza dopiero ponad rezerwa, wiec zloto lezaloby w kiesie albo
    ///    lataloby deficyt rodu, nie dajac miejsca w pulapie; podniesienie pulapu tej samej doby zamienia zloto na zold ludzi od razu (bez zwolnien 166 i z
    ///    miejscem na werbunek), a gotowka idzie tylko za ludzmi, ktorzy sa.
    ///  - KOSZT: osada, z ktorej pan bierze, traci lojalnosc LordWarLevyLoyalty (0.5) i dobrobyt LordWarLevyProsperity (1.0 - tyle co polityka War Tax gry)
    ///    na dobe PRZY PELNEJ STAWCE, proporcjonalnie do czesci nadwyzki, ktora pan wzial (miasto albo zamek liczy tez swoje wsie). Bez poboru w osadzie z
    ///    lojalnoscia ponizej LordWarLevyMinLoyalty (40), w buncie, oblezonej (razem z jej wsiami) i we wsi spladrowanej albo grabionej.
    ///  - POKOJ: plan W5 nie powstaje (W5 tylko w wojnie) - W7 = 0.
    /// Platnik -> odbiorca: kiesa ludu / kiesa wsi -> kiesa glowy rodu (to samo zloto; wyjatek przy przelewie - zloto wraca tam, skad wyszlo).
    /// Linie: "Stawka wojenna pana (W7): dzien D" (doba) i co 7 dob "Stawka wojenna pana (W7) - krolestwa: dzien D"; kolumny budzet-rodow.csv
    /// pobor_wojenny_w7, podniesienie_pulapu_w7; "Obieg" (rody dostaly). Zapis gry: nic nowego (stan sesji: plan doby, gotowka wczoraj, sumy tygodnia).
    /// Wyjatek per rod / per osada - licznik potkniec (pierwszy do pliku), bez globalnych wylacznikow (CLAUDE.md 7).
    /// </summary>
    internal static partial class KingdomTreasury
    {
        private sealed class LvK { public Kingdom K; public string Name = ""; public List<NaClan> Cand; public double Level; }
        private sealed class LvSrc { public Settlement St; public Village V; public int Cls; public double Room; public long Want, Took; public LvFief F; }
        private sealed class LvFief { public Settlement St; public Town T; public double Room; public long Took; }
        private sealed class LvWk { public long Folk, Vill; public double Unc, Loy, Pros; public readonly HashSet<Clan> Clans = new HashSet<Clan>(); public readonly HashSet<Settlement> Places = new HashSet<Settlement>(); }

        private static readonly Dictionary<Kingdom, LvK> _lvPlan = new Dictionary<Kingdom, LvK>();   // plan doby z kroku W5 (CrownRents.Daily): rody z potrzeba i poziom
        private static int _lvPlanDay = int.MinValue;
        private static readonly Dictionary<Clan, long> _lvCash = new Dictionary<Clan, long>();      // gotowka ostatniego kroku (nastepny WageRefund zdejmuje ja z podstawy zwrotu)
        private static readonly Dictionary<Clan, long> _lvPaid = new Dictionary<Clan, long>();      // gotowka tej doby (CSV, ClanBudget.AidCash)
        private static readonly Dictionary<Clan, double> _lvBoost = new Dictionary<Clan, double>(); // podniesienie pulapu tej doby ponad przydzial W5 (ClanBudget.Daily)
        private static int _lvDay = int.MinValue;
        private static readonly HashSet<string> _lvErr = new HashSet<string>();
        private static int _lvStumbles;
        // licznik doby (linia)
        private static long _lvFolk, _lvVill, _lvHeads, _lvCut; private static double _lvUnc, _lvBoostSum, _lvLoy, _lvPros, _lvLoySum, _lvLoyMin;
        private static int _lvCutN, _lvNeedN, _lvPaidN, _lvRoomN, _lvNoSrcN, _lvPlOff, _lvPlanK, _lvPeaceK, _lvLowLoy, _lvRebel, _lvSiege, _lvLooted, _lvLoyN;
        private static int _lvOverAid, _lvBelowRes, _lvAtLowLoy, _lvSlack, _lvOverNeed, _lvInPeace, _lvMen, _lvLim, _lvSlackSkip, _lvShortN;
        private const double LvAtCap = 0.95;   // recenzja W7: rod "na pulapie" - zold druzyn co najmniej 95% pulapu wlasnego druzyn (ta sama granica co ClanBudget.SlackGold)
        private static readonly HashSet<Settlement> _lvPlaces = new HashSet<Settlement>();
        private static readonly Dictionary<Settlement, double> _lvLoyAt = new Dictionary<Settlement, double>();   // P1b: koszt lojalnosci W7 tej doby na miasto / zamek (odczyt FolkAidLoyalty)
        // tydzien na krolestwo (linia co 7 dob) i gracz (komunikat)
        private static readonly Dictionary<string, LvWk> _lvWeek = new Dictionary<string, LvWk>();
        private static int _lvWeekStart = -1, _lvPlWeekStart = -1;
        private static long _lvPlWeek;
        private static bool _lvPlTold;
        /// <summary>"Obieg" (rody dostaly): stawka wojenna pana tej doby - kiesy ludu i wsi -> glowy rodow (razem, z ludu, ze wsi).</summary>
        internal static long LastLordLevy, LastLordLevyFolk, LastLordLevyVill;

        internal static bool LordLevyOn { get { var s = Settings.Current; return s != null && s.LordWarLevy && NeedAidOn && TownFolk.LevyOn; } }

        private static double LvRate(Settings s) { return WtClamp(s.LordWarLevyRate, 0, 0.2); }
        private static double LvLoyCost(Settings s) { return WtClamp(s.LordWarLevyLoyalty, 0, 10); }
        private static double LvProsCost(Settings s) { return WtClamp(s.LordWarLevyProsperity, 0, 50); }
        private static double LvMinLoy(Settings s) { return WtClamp(s.LordWarLevyMinLoyalty, 0, 100); }

        private static void LordLevyReset()
        {
            _lvPlan.Clear(); _lvPlanDay = int.MinValue; _lvCash.Clear(); _lvPaid.Clear(); _lvBoost.Clear(); _lvDay = int.MinValue;
            _lvErr.Clear(); _lvStumbles = 0; LvClearDay(); _lvWeek.Clear(); _lvWeekStart = -1; _lvPlWeekStart = -1; _lvPlWeek = 0; _lvPlTold = false;
            LastLordLevy = LastLordLevyFolk = LastLordLevyVill = 0;
        }

        private static void LvClearDay()
        {
            _lvFolk = _lvVill = _lvHeads = 0; _lvUnc = _lvBoostSum = _lvLoy = _lvPros = _lvLoySum = 0; _lvLoyMin = double.NaN;
            _lvNeedN = _lvPaidN = _lvRoomN = _lvNoSrcN = _lvPlOff = _lvPlanK = _lvPeaceK = _lvLowLoy = _lvRebel = _lvSiege = _lvLooted = _lvLoyN = 0;
            _lvOverAid = _lvBelowRes = _lvAtLowLoy = _lvSlack = _lvOverNeed = _lvInPeace = _lvMen = _lvLim = _lvSlackSkip = _lvShortN = 0;
            _lvPlaces.Clear(); _lvLoyAt.Clear();
        }

        private static void LvStumble(string where, Exception e)
        {
            _lvStumbles++;
            try { if (_lvErr.Add(where)) Log.Error("KingdomTreasury.LordLevy." + where, e); } catch { }
        }

        /// <summary>W5 (NeedAidKingdom) -> W7: plan doby krolestwa - rody z potrzeba W5 (te same obiekty, ta sama miara) i poziom wyrownania po ostatnim
        /// szczeblu W5 (0 - bez garnka albo przed szczeblami). Bez zlota. Klucz wylaczony - nic.</summary>
        private static void LvPlan(Kingdom k, string name, List<NaClan> cand, double level)
        {
            try
            {
                if (!LordLevyOn || k == null || cand == null) return;
                int today = (int)CampaignTime.Now.ToDays;
                if (_lvPlanDay != today) { _lvPlan.Clear(); _lvPlanDay = today; }
                LvK p;
                if (!_lvPlan.TryGetValue(k, out p)) { p = new LvK { K = k, Name = name ?? "" }; _lvPlan[k] = p; }
                p.Cand = cand; p.Level = level > 0 ? level : 0;
            }
            catch (Exception e) { LvStumble("LvPlan", e); }
        }

        /// <summary>Nowa doba kroku W7: gotowka i przydzialy poprzedniej doby znikaja (zwrot zoldu tej doby juz je odliczyl - biegnie przed krokiem).</summary>
        private static void LvRoll(int today)
        {
            if (_lvDay == today) return;
            _lvDay = today;
            _lvCash.Clear(); _lvPaid.Clear(); _lvBoost.Clear();
        }

        // ------------------------------------------------------------ doba: krok W7 i linia - z ArmouryBehavior po bloku korony, przed budzetem rodow 166
        internal static void LordLevyDay()
        {
            LastLordLevy = LastLordLevyFolk = LastLordLevyVill = 0;
            var s = Settings.Current;
            if (s == null || Campaign.Current == null) return;
            int today = (int)CampaignTime.Now.ToDays;
            LvClearDay();
            bool on = false;
            try
            {
                LvRoll(today);
                on = LordLevyOn;
                if (on && _lvPlanDay == today)
                {
                    foreach (var p in _lvPlan.Values)
                    {
                        try { LvKingdom(p, s, today); }
                        catch (Exception e) { LvStumble("LvKingdom", e); }
                    }
                }
                // kontrola niezalezna od planu: pobor tylko u rodow krolestw w wojnie (oczekiwane 0)
                foreach (var kv in _lvPaid) { var c = kv.Key; if (kv.Value > 0 && (c == null || c.Kingdom == null || !AtWar(c.Kingdom))) _lvInPeace++; }
                LastLordLevy = _lvHeads; LastLordLevyFolk = _lvFolk; LastLordLevyVill = _lvVill;
                LvPlayer(today, s, on);
                if (s.LogEnabled) { LvLine(today, s, on); LvWeekLine(today, on); }
            }
            catch (Exception e) { LvStumble("LordLevyDay", e); }
            finally { _lvPlan.Clear(); _lvStumbles = 0; _lvCut = 0; _lvCutN = 0; }
        }

        private static void LvKingdom(LvK p, Settings s, int today)
        {
            var k = p.K;
            if (k == null || k.IsEliminated || p.Cand == null) return;
            _lvPlanK++;
            if (!AtWar(k)) { _lvPeaceK++; return; }                     // pokoj = bez poboru (plan W5 powstaje tylko w wojnie - kontrola)
            double target = NaTarget(s), step = NaStep(s), rate = LvRate(s);
            if (!(target > 0) || !(rate > 0)) return;
            double f0 = Math.Min(target, p.Level);
            LvWk wk = null;
            for (int i = 0; i < p.Cand.Count; i++)
            {
                var x = p.Cand[i];
                try
                {
                    if (x == null || x.C == null || x.C.IsEliminated || x.C.Leader == null || !x.C.Leader.IsAlive || x.C.Kingdom != k) continue;
                    if (x.Pl && !s.PlayerWarLevy) { _lvPlOff++; continue; }   // gracz bez klucza - nic nie placi i nic nie dostaje
                    double c0 = NaCost(x, f0), ct = NaCost(x, target), unc = ct - c0;
                    if (!(unc >= 1)) continue;                                // W5 pokryl potrzebe rodu (rod stac na cel z wlasnego pulapu - nie ma go w planie)
                    // recenzja W7: rod z luzem w pulapie (zold druzyn ponizej 95% pulapu wlasnego) jest ponizej celu nie z powodu pulapu zoldu - werbuje z wlasnego
                    // miejsca; W7 nie bierze dla niego i nie daje mu miejsca (W5 daje swoje jak dotad). Dotad dostawal samo miejsce W7, gdy poziom W7 byl wyzszy od W5
                    if (x.W < LvAtCap * x.Pc0) { _lvSlackSkip++; continue; }
                    _lvNeedN++; _lvUnc += unc;
                    if (wk == null) wk = LvWeekOf(p.Name);
                    wk.Unc += unc;
                    // zrodla: wlasne lenna rodu (miasta, zamki) i ich wsie - nadwyzka ponad zapas po daninach tej doby
                    var fiefs = new List<LvFief>(); var src = new List<LvSrc>();
                    double E = LvSources(x.C, s, fiefs, src);
                    double cap = rate * E;
                    if (!(cap >= 1)) { _lvNoSrcN++; continue; }
                    // poziom W7: najwyzszy f w [poziom W5, cel], przy ktorym koszt ponad poziom W5 miesci sie w stawce nadwyzki lenn
                    double f7 = target;
                    if (unc > cap)
                    {
                        double lo = f0, hi = target;
                        for (int it = 0; it < 32; it++) { double mid = 0.5 * (lo + hi); if (NaCost(x, mid) - c0 <= cap) lo = mid; else hi = mid; }
                        f7 = lo;
                    }
                    double a7, b7; long c7;
                    NaAt(x, f7, step, out a7, out b7, out c7);
                    long paid5 = 0; double boost5 = 0;
                    if (_naDay == today) { _naPaid.TryGetValue(x.C, out paid5); _naBoost.TryGetValue(x.C, out boost5); }
                    long want = Math.Max(0, c7 - paid5);                      // gotowka tylko za ludzi, ktorzy dzis sa w druzynach, ponad gotowke W5
                    if (want > (long)Math.Floor(cap)) want = (long)Math.Floor(cap);
                    if (want > (long)Math.Floor(unc)) want = (long)Math.Floor(unc);
                    long got = want > 0 ? LvCollect(x.C, src, fiefs, E, want, rate, s, wk) : 0;
                    if (got > 0)
                    {
                        _lvPaid[x.C] = got; _lvCash[x.C] = got; _lvPaidN++;
                        if (got > unc + 1) _lvOverNeed++;                     // kontrola: nigdy ponad niepokryta potrzebe (oczekiwane 0)
                        wk.Clans.Add(x.C);
                        if (x.Pl) _lvPlWeek += got;
                    }
                    // przydzial w pulapie: gotowka + miejsce na krok werbunku przy poziomie W7, ponad przydzial W5 tej doby (gracz - bez pulapu 166, sama gotowka)
                    // recenzja W7: przydzial liczony przy poziomie W7 zaklada gotowke `want`; gdy przelew wzial mniej (potkniecie zrodla), niezaplacona czesc
                    // nie podnosi pulapu - inaczej rod placilby ten zold z wlasnej kiesy ponad pulap wlasny (pulap bez zlota)
                    if (got < want) _lvShortN++;
                    double nb = Math.Max(b7 - Math.Max(0, want - got), paid5 + got), add = x.Pl ? 0 : Math.Max(0, nb - boost5);
                    if ((got > 0 || add > 0) && x.W < LvAtCap * x.Pc0) _lvSlack++;   // kontrola: przydzial (gotowka albo miejsce) rodowi z luzem w pulapie (oczekiwane 0)
                    if (add > 0) { _lvBoost[x.C] = add; _lvBoostSum += add; if (got <= 0) _lvRoomN++; }
                    if (got > 0 || add > 0) { _lvMen += x.M; _lvLim += x.L; }
                }
                catch (Exception e) { LvStumble("LvKingdom(rod)", e); }
            }
        }

        private static LvWk LvWeekOf(string name)
        {
            LvWk w;
            if (!_lvWeek.TryGetValue(name ?? "", out w)) { w = new LvWk(); _lvWeek[name ?? ""] = w; }
            return w;
        }

        /// <summary>Zrodla poboru rodu: jego miasta i zamki (bez oblezonych, zbuntowanych i z lojalnoscia ponizej progu - razem z ich wsiami) - kiesa ludu ponad
        /// zapas po daninie P1 (TownFolk.LevyRoom) i wsie (stan Normal, wlasciciel = rod) ponad 15 dni zakupow. Zwraca sume nadwyzki. Tylko odczyt.</summary>
        private static double LvSources(Clan c, Settings s, List<LvFief> fiefs, List<LvSrc> src)
        {
            double E = 0, minLoy = LvMinLoy(s);
            var fl = c.Fiefs;
            if (fl == null) return 0;
            var room = new double[3];
            for (int i = 0; i < fl.Count; i++)
            {
                var t = fl[i];
                var st = t != null ? t.Settlement : null;
                if (st == null || st.OwnerClan != c || !(st.IsTown || st.IsCastle)) continue;
                try
                {
                    if (st.IsUnderSiege) { _lvSiege++; continue; }
                    if (t.InRebelliousState) { _lvRebel++; continue; }
                    if (t.Loyalty < minLoy) { _lvLowLoy++; continue; }
                    var fief = new LvFief { St = st, T = t };
                    if (TownFolk.LevyRoom(st, room))
                        for (int k = 0; k < 3; k++) if (room[k] >= 1) { src.Add(new LvSrc { St = st, Cls = k, Room = room[k], F = fief }); fief.Room += room[k]; }
                    var bv = st.BoundVillages;
                    if (bv != null)
                        for (int j = 0; j < bv.Count; j++)
                        {
                            var v = bv[j];
                            var vs = v != null ? v.Settlement : null;
                            if (vs == null || vs.OwnerClan != c) continue;
                            if (v.VillageState != Village.VillageStates.Normal) { _lvLooted++; continue; }
                            int floor = TownFolk.VillageFloor(vs);
                            if (floor <= 0) continue;                         // wies bez miary zapasu (nieskalibrowana) - bez poboru
                            double ex = Math.Floor((double)v.Gold - floor);
                            if (ex >= 1) { src.Add(new LvSrc { St = vs, V = v, Cls = -1, Room = ex, F = fief }); fief.Room += ex; }
                        }
                    if (fief.Room > 0) { fiefs.Add(fief); E += fief.Room; }
                }
                catch (Exception e) { LvStumble("LvSources(osada)", e); }
            }
            return E;
        }

        /// <summary>Pobor `want` ze zrodel rodu proporcjonalnie do nadwyzki (kazde najwyzej swoja nadwyzke) i przelew DOKLADNIE zdjetej kwoty do glowy rodu;
        /// potem koszt osad (lojalnosc, dobrobyt) wedlug czesci nadwyzki wzietej w osadzie i jej wsiach. Wyjatek przy przelewie - zloto wraca do zrodel.</summary>
        private static long LvCollect(Clan c, List<LvSrc> src, List<LvFief> fiefs, double E, long want, double rate, Settings s, LvWk wk)
        {
            if (src.Count == 0 || !(E > 0) || want <= 0) return 0;
            long plan = 0;
            for (int i = 0; i < src.Count; i++)
            {
                var x = src[i];
                x.Want = Math.Min((long)Math.Floor(want * x.Room / E), (long)Math.Floor(x.Room));
                plan += x.Want;
            }
            if (plan < want)
            {
                src.Sort((a, b) => b.Room.CompareTo(a.Room));
                for (int i = 0; i < src.Count && plan < want; i++)
                {
                    long add = Math.Min(want - plan, (long)Math.Floor(src[i].Room) - src[i].Want);
                    if (add > 0) { src[i].Want += add; plan += add; }
                }
            }
            double minLoy = LvMinLoy(s);
            long folk = 0, vill = 0;
            for (int i = 0; i < src.Count; i++)
            {
                var x = src[i];
                if (x.Want <= 0) continue;
                try
                {
                    if (x.F != null && x.F.T != null && x.F.T.Loyalty < minLoy) _lvAtLowLoy++;   // kontrola: pobor w osadzie z lojalnoscia ponizej progu (oczekiwane 0)
                    if (x.V != null)
                    {
                        int had = x.V.Gold;
                        x.V.ChangeGold(-(int)Math.Min(int.MaxValue, x.Want));
                        x.Took = Math.Max(0, had - x.V.Gold);
                        if (x.Took > 0 && x.V.Gold + 0.5 < TownFolk.VillageFloor(x.St)) _lvBelowRes++;   // kontrola: wies ponizej zapasu po poborze (oczekiwane 0)
                        vill += x.Took;
                    }
                    else
                    {
                        double aid = TownFolk.LevyAfterAid(x.St, x.Cls);
                        x.Took = TownFolk.LevyTake(x.St, x.Cls, x.Want);
                        if (x.Took > 0 && (aid < 0 || x.Took > aid + 0.5)) _lvOverAid++;               // kontrola: P1 i W7 bez podwojnego poboru (oczekiwane 0)
                        if (x.Took > 0 && TownFolk.LevyBelowReserve(x.St, x.Cls)) _lvBelowRes++;       // kontrola: kiesa ponizej zapasu dni po poborze (oczekiwane 0)
                        folk += x.Took;
                    }
                }
                catch (Exception e) { LvStumble("LvCollect(zrodlo)", e); }
            }
            long sum = folk + vill;
            if (sum <= 0) return 0;
            var h = c.Leader;
            try { h.ChangeHeroGold((int)Math.Min(int.MaxValue, sum)); }
            catch
            {
                // wyjatek przy przelewie: zloto wraca tam, skad wyszlo (nic w nicosc), rod nie dostaje nic
                for (int i = 0; i < src.Count; i++)
                {
                    var x = src[i];
                    if (x.Took <= 0) continue;
                    try { if (x.V != null) x.V.ChangeGold((int)x.Took); else TownFolk.LevyBack(x.St, x.Cls, x.Took); } catch { }
                    x.Took = 0;
                }
                throw;
            }
            CirculationWindows.NoteHeroGold(h, (int)Math.Min(int.MaxValue, sum));   // paczka 169b: glowa poza swiatem (Disabled) - zloto wyszlo ze swiata (tylko licznik)
            _lvFolk += folk; _lvVill += vill; _lvHeads += sum;
            if (wk != null) { wk.Folk += folk; wk.Vill += vill; }
            // koszt osad: czesc nadwyzki wzietej w osadzie i jej wsiach wobec pelnej stawki
            for (int i = 0; i < src.Count; i++) { var x = src[i]; if (x.Took > 0 && x.F != null) { x.F.Took += x.Took; _lvPlaces.Add(x.St); if (wk != null) wk.Places.Add(x.St); } }
            double lc = LvLoyCost(s), pc = LvProsCost(s);
            for (int i = 0; i < fiefs.Count; i++)
            {
                var f = fiefs[i];
                if (f.Took <= 0 || f.T == null || !(f.Room > 0)) continue;
                try
                {
                    double q = Math.Min(1.0, f.Took / (rate * f.Room));
                    float l0 = f.T.Loyalty, p0 = f.T.Prosperity;
                    if (lc > 0) f.T.Loyalty = Math.Max(0f, l0 - (float)(lc * q));
                    if (pc > 0) f.T.Prosperity = Math.Max(0f, p0 - (float)(pc * q));
                    double dl = l0 - f.T.Loyalty, dp = p0 - f.T.Prosperity;
                    _lvLoy += dl; _lvPros += dp;
                    if (dl > 0 && f.St != null) { double o; _lvLoyAt.TryGetValue(f.St, out o); _lvLoyAt[f.St] = o + dl; }   // P1b: wspolna podloga lojalnosci z danina
                    if (wk != null) { wk.Loy += dl; wk.Pros += dp; }
                    _lvLoySum += f.T.Loyalty; _lvLoyN++;
                    if (double.IsNaN(_lvLoyMin) || f.T.Loyalty < _lvLoyMin) _lvLoyMin = f.T.Loyalty;
                }
                catch (Exception e) { LvStumble("LvCollect(koszt)", e); }
            }
            return sum;
        }

        /// <summary>P1b (FolkAidLoyalty): lojalnosc, ktora W7 zdjal tej osadzie w ostatnim kroku doby (0 - bez poboru albo krok nie biegl). Tylko odczyt.</summary>
        internal static double LordLevyLoyaltyAt(Settlement st)
        {
            double x;
            return st != null && _lvLoyAt.TryGetValue(st, out x) ? x : 0;
        }

        /// <summary>ClanBudget.Daily: podniesienie pulapu z dzisiejszego kroku W7 (gotowka + miejsce na werbunek ponad przydzial W5); 0 - bez poboru, klucz
        /// wylaczony albo krok dzis nie biegl.</summary>
        internal static double LordLevyBoostOf(Clan c)
        {
            try
            {
                double b;
                if (c == null || Campaign.Current == null || _lvDay != (int)CampaignTime.Now.ToDays || !_lvBoost.TryGetValue(c, out b)) return 0;
                return b > 0 ? b : 0;
            }
            catch { return 0; }
        }

        /// <summary>ClanBudget.Daily: GOTOWKA poboru W7 tej doby (zold druzyn oplacony z lenn - jak gotowka korony W5: dwor i niewydany sprzet mu nie ustepuja).</summary>
        internal static long LordLevyCashOf(Clan c)
        {
            try
            {
                long p;
                if (c == null || Campaign.Current == null || _lvDay != (int)CampaignTime.Now.ToDays || !_lvPaid.TryGetValue(c, out p)) return 0;
                return p > 0 ? p : 0;
            }
            catch { return 0; }
        }

        /// <summary>WageRefund (przed krokiem W5 i W7 tej doby, po odjeciu doplaty W5): ile z zoldu `paid` oplacil wczorajszy pobor W7 - ta czesc nie jest
        /// podstawa zwrotu 70% (korona nie zwraca zoldu oplaconego podatkiem pana). Zdejmuje wykorzystana czesc z pamieci. 0 bez wczorajszego poboru.</summary>
        internal static long LordLevyRefundCut(Clan c, long paid)
        {
            try
            {
                long have;
                if (c == null || paid <= 0 || Campaign.Current == null) return 0;
                if (_lvDay != (int)CampaignTime.Now.ToDays - 1 || !_lvCash.TryGetValue(c, out have) || have <= 0) return 0;
                long cut = Math.Min(paid, have);
                _lvCash[c] = have - cut;
                _lvCut += cut; _lvCutN++;
                return cut;
            }
            catch (Exception e) { LvStumble("LordLevyRefundCut", e); return 0; }
        }

        /// <summary>Kolumny budzet-rodow.csv (ostatnie - czytniki po nazwach): pobor W7 tej doby (gotowka do glowy rodu) i podniesienie pulapu ponad W5.</summary>
        internal const string LordLevyCsvHeader = ";pobor_wojenny_w7;podniesienie_pulapu_w7";

        internal static string LordLevyCsv(Clan c)
        {
            try
            {
                if (c == null || Campaign.Current == null || _lvDay != (int)CampaignTime.Now.ToDays) return ";;";
                long p; double b;
                bool hp = _lvPaid.TryGetValue(c, out p), hb = _lvBoost.TryGetValue(c, out b);
                if (!hp && !hb) return ";;";
                return ";" + (hp ? p : 0).ToString(WtInv) + ";" + Math.Round(hb ? b : 0).ToString("0", WtInv);
            }
            catch { return ";;"; }
        }

        /// <summary>Komunikaty gracza (napisy po angielsku; tylko z kluczem PlayerWarLevy): wyjasnienie przy pierwszym poborze w sesji, suma raz na 7 dob.</summary>
        private static void LvPlayer(int today, Settings s, bool on)
        {
            try
            {
                var c = Clan.PlayerClan;
                if (c == null || !on || !s.PlayerWarLevy) { _lvPlWeek = 0; _lvPlWeekStart = -1; return; }
                long got;
                _lvPaid.TryGetValue(c, out got);
                if (got > 0 && !_lvPlTold)
                {
                    _lvPlTold = true;
                    Log.Player("War levy: while your lands cannot pay for your companies and the crown's war aid falls short, your towns, castles and villages give you up to "
                               + (100 * LvRate(s)).ToString("0.#", WtInv) + "% a day of the coin they hold above their reserve, toward the wages of the men in your companies. "
                               + "Their loyalty and prosperity suffer in proportion. A fief with loyalty under " + LvMinLoy(s).ToString("0", WtInv)
                               + ", in revolt or under siege pays nothing, nor does a looted village. You can turn it off (Player War Levy).");
                }
                if (_lvPlWeekStart < 0 || today < _lvPlWeekStart) _lvPlWeekStart = today;
                if (today - _lvPlWeekStart >= 7)
                {
                    long w = _lvPlWeek - got;
                    if (w > 0) Log.Player("War levy: your fiefs gave you " + w.ToString("N0", WtInv) + " coins this week toward your soldiers' wages.");
                    _lvPlWeek = got; _lvPlWeekStart = today;
                }
            }
            catch (Exception e) { LvStumble("LvPlayer", e); }
        }

        private static string LvF(double x, string fmt) { return x.ToString(fmt, WtInv); }

        private static void LvLine(int today, Settings s, bool on)
        {
            try
            {
                long all = _lvFolk + _lvVill;
                var sb = new StringBuilder(1536);
                sb.Append("Stawka wojenna pana (W7): dzien ").Append(today)
                  .Append(" - rodow ").Append(_lvPaidN).Append(", osad ").Append(_lvPlaces.Count)
                  .Append(", pobrano ").Append(all).Append(" (z ludu ").Append(_lvFolk).Append(" / ze wsi ").Append(_lvVill).Append(')')
                  .Append(", lojalnosc -").Append(LvF(_lvLoy, "0.00")).Append(", dobrobyt -").Append(LvF(_lvPros, "0.0"))
                  .Append(", potkniecia ").Append(_lvStumbles)
                  .Append(" | wylacznik Lord War Levy ").Append(s.LordWarLevy ? (on ? "tak" : "tak, nieczynny (bez doplaty W5 albo kiesy ludu)") : "nie (pobor 0 - jak przed paczka)")
                  .Append(", gracz (Player War Levy) ").Append(s.PlayerWarLevy ? "tak" : "nie (rod gracza nie placi i nie dostaje)")
                  .Append(", doplata W5 (Crown War Aid By Need) ").Append(NeedAidOn ? "tak" : "nie")
                  .Append(", kiesa ludu ").Append(TownFolk.LevyOn ? "czynna" : "nieczynna")
                  .Append(" | stawka ").Append(LvF(100 * LvRate(s), "0.0")).Append("% nadwyzki ponad zapas na dobe (lud po daninie P1 i ofercie na jutro, wsie ponad 15 dni zakupow po daninie wsi)")
                  .Append(", cel W5 ").Append(LvF(100 * NaTarget(s), "0")).Append("% limitu druzyn, krok werbunku ").Append(LvF(100 * NaStep(s), "0")).Append("%")
                  .Append("; koszt przy pelnej stawce: lojalnosc ").Append(LvF(LvLoyCost(s), "0.00")).Append(" / dobrobyt ").Append(LvF(LvProsCost(s), "0.00"))
                  .Append(" na dobe; bez poboru przy lojalnosci < ").Append(LvF(LvMinLoy(s), "0")).Append(", w buncie, oblezeniu i we wsi spladrowanej")
                  .Append(" | krolestw z planem W5 ").Append(_lvPlanK).Append(" (w pokoju ").Append(_lvPeaceK).Append(")")
                  .Append(", rody z niepokryta potrzeba po W5 ").Append(_lvNeedN).Append(" (niepokryte ").Append(LvF(Math.Floor(_lvUnc), "0")).Append(")")
                  .Append(", z poborem ").Append(_lvPaidN).Append(", z samym miejscem w pulapie ").Append(_lvRoomN)
                  .Append(", bez nadwyzki w lennach ").Append(_lvNoSrcN).Append(", gracz bez klucza ").Append(_lvPlOff)
                  .Append(", z luzem w pulapie (bez W7) ").Append(_lvSlackSkip)
                  .Append(" | do glow rodow ").Append(_lvHeads).Append(" (roznica obiegu ").Append(_lvHeads - all).Append(")")
                  .Append(", podniesienie pulapu ").Append(LvF(Math.Round(_lvBoostSum), "0")).Append(" (gotowka ").Append(_lvHeads)
                  .Append(" + miejsce na krok werbunku ").Append(LvF(Math.Round(Math.Max(0, _lvBoostSum - _lvHeads)), "0")).Append(")")
                  .Append(" | osady pominiete: lojalnosc < prog ").Append(_lvLowLoy).Append(", bunt ").Append(_lvRebel).Append(", oblezenie ").Append(_lvSiege)
                  .Append(", wsie spladrowane ").Append(_lvLooted)
                  .Append(" | kontrola: lud ponad nadwyzke po P1 ").Append(_lvOverAid).Append(", ponizej zapasu po poborze ").Append(_lvBelowRes)
                  .Append(", pobor w osadzie z lojalnoscia < prog ").Append(_lvAtLowLoy).Append(", przydzial rodom z luzem ").Append(_lvSlack)
                  .Append(", gotowka ponad niepokryta potrzebe ").Append(_lvOverNeed).Append(", pobor w pokoju ").Append(_lvInPeace).Append(", przelew ponizej planu (potkniecie zrodla) ").Append(_lvShortN).Append(" (oczekiwane 0)")
                  .Append(" | wojsko rodow z W7 dzis: ").Append(_lvMen).Append(" ludzi / limit ").Append(_lvLim).Append(" (").Append(NaPct(_lvMen, _lvLim)).Append(')')
                  .Append(" | lojalnosc osad z poborem po koszcie: srednio ").Append(_lvLoyN > 0 ? LvF(_lvLoySum / _lvLoyN, "0.0") : "-")
                  .Append(", najnizsza ").Append(double.IsNaN(_lvLoyMin) ? "-" : LvF(_lvLoyMin, "0.0"))
                  .Append(" | zold oplacony wczorajszym poborem poza podstawa zwrotu 70% (dzis) ").Append(_lvCut).Append(" (wpisow ").Append(_lvCutN).Append(")")
                  .Append('.');
                Log.Info(sb.ToString());
            }
            catch (Exception e) { LvStumble("LvLine", e); }
        }

        /// <summary>Co 7 dob: wiersz na krolestwo (sumy tygodnia) - "Stawka wojenna pana (W7) - krolestwa: dzien D". Tylko przy czynnej paczce.</summary>
        private static void LvWeekLine(int today, bool on)
        {
            try
            {
                if (!on) { _lvWeek.Clear(); _lvWeekStart = -1; return; }
                if (_lvWeekStart < 0 || today < _lvWeekStart) _lvWeekStart = today;
                int days = today - _lvWeekStart + 1;
                if (days < 7) return;
                var parts = new List<KeyValuePair<double, string>>();
                foreach (var kv in _lvWeek)
                {
                    var w = kv.Value;
                    long all = w.Folk + w.Vill;
                    if (all <= 0 && w.Unc < 1) continue;
                    parts.Add(new KeyValuePair<double, string>(all + w.Unc * 1e-6, kv.Key + ": rodow " + w.Clans.Count + ", osad " + w.Places.Count + ", pobrano " + all
                              + " (lud " + w.Folk + " / wsie " + w.Vill + ") = " + LvF((double)all / days, "0") + "/d, niepokryte po W5 " + LvF(w.Unc / days, "0") + "/d"
                              + ", pokrycie " + (w.Unc >= 1 ? LvF(100.0 * all / w.Unc, "0") + "%" : "-") + ", lojalnosc -" + LvF(w.Loy, "0.0") + ", dobrobyt -" + LvF(w.Pros, "0")));
                }
                parts.Sort((a, b) => b.Key.CompareTo(a.Key));
                var txt = new List<string>(); foreach (var p in parts) txt.Add(p.Value);
                Log.Info("Stawka wojenna pana (W7) - krolestwa: dzien " + today + " (doby " + _lvWeekStart + "-" + today + ", " + days + " dob) | "
                         + (txt.Count > 0 ? string.Join(" ; ", txt.ToArray()) : "-") + ".");
                _lvWeek.Clear(); _lvWeekStart = today + 1;
            }
            catch (Exception e) { LvStumble("LvWeekLine", e); }
        }
    }
}
