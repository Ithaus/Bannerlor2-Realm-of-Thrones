using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// E7/R1 (235, PLAN 7.11 a, R15; projekt etapu 7 v2 rozdz. 3 "E7/R1"; RB = docs/PROJEKT-RABUNEK-OSADA-PO-OSADZIE-2026-10-07.md po poprawkach K1-K12) -
    /// RABUNEK OSADA PO OSADZIE DLA AI. Wies na mapie to okreg (n osad po ok. DistrictSettlementPeople ludzi - ksiega wiosek E4/11). Dotad gra konczyla rabunek
    /// po ok. 2.4 doby bez wzgledu na sile napastnika i dawala zloto za hearth oraz towar "z produkcji" i "zwykly lup" z niczego. Odtad (tylko rabunki AI):
    ///  - PRZEJECIE LUPIENIA: prefiks Priority.First na RaidEventComponent.Update (G :160) zwraca false w fazie lupienia (DefenderSide.TroopCount == 0, G :162).
    ///    Decyzja "rabunek prowadzi R1" zapada RAZ, w pierwszym wywolaniu prefiksu (przed Devastation.RaidPrefix i oknem W6): klucz RaidBySettlementEnabled,
    ///    wies objeta spustoszeniem (Devastation.Covers), zdarzenie bez gracza, wodz nie jest gracza. Prefiksy void po naszym false biegna dalej (Harmony
    ///    AffectsOriginal) - stad straznik Owns na gorze Devastation.RaidPrefix i SmallGold.RaidPre.
    ///  - KROK = m osad: m = min(Z, max(1, N / D)), Z = floor(Room / S_v) osad w zasiegu (Room E4/5 - pulap i dno 11 hearth), S_v = ludzie okregu / n,
    ///    D = RaidDetachmentMen. Postep w ludziach przeszukanych: += R x N x f x u x dt (R = DevastationPerRaiderDay, N = napastnicy na biezaco, f = mnoznik
    ///    modelu rabunku (perki BK, BKROT, NavalDLC - iloraz ResultNumber / BaseNumber), u = Room / zasieg na poczatku kroku, nie mniej niz UFloor - straznik
    ///    przeciw rabunkowi "na wieki" w okregu prawie wypalonym, licznik w linii). Punkty osady = punkty startu x (1 - 0.99 x postep) - w kroku nigdy 0 (K1).
    ///  - LUDZIE: co 5% kroku Devastation.StrikePeople - ten sam rdzen co Strike (PeopleBook.Move, zabici / las / uchodzcy, Famine.Burn, VillageBook.OnStrike
    ///    z kluczem sesji rs.Comp) i TEN SAM stan rabunku E4/5 (Devastation.StateOf): jency E4/9 przy koncu rabunku, nekromancja Innych (RaidDead), ksiega wiosek.
    ///  - JEDZENIE (K4): przy kazdym uderzeniu napastnicy dostaja ze spichlerza okregu (Famine.Take) zboze rowne swemu zuzyciu za ten czas (20 racji za sztuke).
    ///  - AI: jeden krok na sesje, potem koniec tak jak gra (G :307-313): punkty 0, AttackerVictory (setter internal - delegat), finish, SetLevelMaskIsDirty ->
    ///    OnBeforeFinalize: Looted -> RaidCompleted (jeden na sesje). RaidDamage = postep kroku w kazdym ticku (kara relacji gry, K3; 1 po pelnym kroku).
    ///    CZUJNOSC: po pelnym kroku wies zostaje Looted bez leczenia ceil(1 + 100 x d) dob (d = trafieni sesji / ludzie okregu), najwyzej RaidAlertMaxDays -
    ///    prefiks VillageHealCampaignBehavior.DailyTickSettlement tylko odklada leczenie (milicji nie rusza: +20 przy odbudowie zamyka E4/8).
    ///  - LUP tylko z zasobow wsi, raz na sesje (koniec kroku albo przerwanie - za zrobiona czesc): udzial s = trafieni sesji / ludzie w domu na starcie; zloto
    ///    s x kiesa wsi TA SAMA funkcja platnika co E5/A7 (SmallGold.PayRaidLoot -> LootPayer); towar s z magazynu wsi (Settlement.ItemRoster): konie i zywy
    ///    inwentarz ida z napastnikiem, reszta wedlug wolnego udzwigu taboru (najcenniejsze na kilogram najpierw), czego nie uniesie - "spalone w rabunku"
    ///    (ksiega towarow 146 FRaid, ArmsBook KRaid). Zadnego zlota za hearth, towaru "z produkcji" ani "zwyklego lupu" (G :176-188, :272-301 - z niczego).
    ///    OnItemsLooted jak oryginal (sluchacze tylko w grze - ROT, BKROT, BEE, DTE, NavalDLC nie sluchaja).
    ///  - NIEZALEZNY BILANS (v2, R1-test): wokol kazdej operacji pomiar kiesy wodza i wsi, magazynu wsi, rosterow napastnikow i spichlerza - linia podaje roznice.
    ///  - GRACZ: R1 go nie prowadzi (okienko po osadzie - E7/R2); gracz w zdarzeniu w trakcie sesji = sesja oddana grze (czesc kroku rozliczona, dalej E4/5).
    /// Zapis: sekcje r: (sesje) i c: (czujnosc) klucza arm_district przez VillageBook.Export/Import (brak = sesja od nowa z punktow gry). Wyjatek per sesja liczony;
    /// po MaxStumbles w jednej sesji - sesja oddana grze (bez globalnego wylacznika, CLAUDE.md 7). Reset w konstruktorze ArmouryBehavior.
    /// </summary>
    internal static class DistrictRaid
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private const double PointsKeep = 0.99;          // punkty osady w kroku: start x (1 - 0.99 x postep) - zero tylko na koniec sesji (K1)
        private const int Chunks = 20;                   // uderzenie StrikePeople co 5% kroku
        private const double RationsPerGrain = 20.0;     // skala zolnierza: 1 sztuka jedzenia = 20 czlowieko-dni (gra: zuzycie partii = ludzie / 20 na dobe; PROJEKT-GLOD 1.6)
        private const double UFloor = 0.25;              // najnizsze u kroku (okreg prawie wypalony: krok najwyzej 4x dluzszy niz w swiezym okregu zamiast setek dob)
        private const int MaxStumbles = 3;               // potkniec jednej sesji, po ktorych oddajemy ja grze
        private const int MaxDaysList = 40;              // dni na osade w linii doby (lista do mediany okna w sprawdz_logi)

        internal static bool On { get { var s = Settings.Current; return s != null && s.RaidBySettlementEnabled && _ready; } }

        internal sealed class Sess
        {
            public RaidEventComponent Comp; public Village V; public Devastation.RaidState Rs;
            public bool Started, Done, Restored, BurnedOut, UClamped, Lord, LootGiven;
            public float Hp0 = 1f;
            public double Prog, Work, M, U, L0, Home0, Sv;   // postep i praca kroku w ludziach przeszukanych
            public double Struck;                            // ludzie kroku juz oddani do StrikePeople
            public double Hit, Dead, Fled, Ref;              // wynik uderzen tej sesji
            public double PdPend, EatDays, LootDays;         // osobodni od uderzenia, doby od posilku, doby lupienia razem
            public double F = 1.0;                           // ostatni znany mnoznik tempa z modelu rabunku
            public int Stumbles;
        }

        internal sealed class EndCtx { public Sess Ss; public long Cap0; public double RaidDead; }

        private static readonly Dictionary<RaidEventComponent, Sess> _sess = new Dictionary<RaidEventComponent, Sess>();
        private static readonly HashSet<RaidEventComponent> _notMine = new HashSet<RaidEventComponent>();
        private static readonly Dictionary<string, string> _pendingR = new Dictionary<string, string>(StringComparer.Ordinal);
        private static readonly Dictionary<string, int> _alert = new Dictionary<string, int>(StringComparer.Ordinal);   // id wsi -> doba konca czujnosci
        private static int _pendingDays;
        private static bool _ready;
        private static Action<Settlement, float> _setHp;
        private static Action<RaidEventComponent, float> _setDmg;
        private static Action<MapEvent, BattleState> _setBs;

        // liczniki doby
        private static int _dEnded, _dSteps, _dInterrupted, _dReleased, _dBurnedOut, _dUClamped, _dStarted, _dRestored, _dStrikes, _dAlertNew, _dHealHeld, _dAbandoned, _dStumbles;
        private static double _dSettl, _dHit, _dDead, _dFled, _dRef, _dRaidDead, _dCheckDead;
        private static long _dCap, _dGold, _dTake, _dTakeValue, _dBurn, _dBurnValue, _dEat;
        private static double _dBal1, _dBal2, _dBal3;
        private static long _dTicks;
        private static readonly List<double> _dDaysPer = new List<double>();
        private static readonly HashSet<string> _errSites = new HashSet<string>();
        private static readonly List<string> _wired = new List<string>(), _missing = new List<string>();
        private static bool _offLogged;

        internal static void Reset()
        {
            _sess.Clear(); _notMine.Clear(); _pendingR.Clear(); _alert.Clear(); _pendingDays = 0; _offLogged = false;
            lock (_errSites) _errSites.Clear();
            NewDay();
        }

        private static void NewDay()
        {
            _dEnded = _dSteps = _dInterrupted = _dReleased = _dBurnedOut = _dUClamped = _dStarted = _dRestored = _dStrikes = _dAlertNew = _dHealHeld = _dAbandoned = _dStumbles = 0;
            _dSettl = _dHit = _dDead = _dFled = _dRef = _dRaidDead = _dCheckDead = 0.0;
            _dCap = _dGold = _dTake = _dTakeValue = _dBurn = _dBurnValue = _dEat = 0;
            _dBal1 = _dBal2 = _dBal3 = 0.0;
            _dTicks = 0;
            _dDaysPer.Clear();
        }

        private static void Stumble(string where, Exception e)
        {
            _dStumbles++;
            bool first;
            lock (_errSites) first = _errSites.Add(where);
            if (first) Log.Error("DistrictRaid." + where, e);
        }

        private static int Today() { return (int)CampaignTime.Now.ToDays; }
        private static double Clamp01(double x) { return double.IsNaN(x) ? 0.0 : (x < 0.0 ? 0.0 : (x > 1.0 ? 1.0 : x)); }
        private static string F(double x, string f) { return x.ToString(f, Inv); }

        private static double TickDays()
        {
            // czas tego tykniecia mapy - ten sam, z ktorego gra liczy postep rabunku (DefaultRaidModel.CalculateHitDamage)
            try { double d = CampaignTime.DeltaTime.ToDays; return d > 0.0 && d < 1.0 ? d : 0.0; }
            catch (Exception e) { Stumble("TickDays", e); return 0.0; }
        }

        /// <summary>Czy rabunek prowadzi R1 (Devastation.RaidPrefix/EndPrefix, SmallGold.RaidPre - straznik przeciw podwojnemu liczeniu).</summary>
        internal static bool Owns(RaidEventComponent r)
        {
            return r != null && _sess.Count > 0 && _sess.ContainsKey(r);
        }

        // ------------------------------------------------------------ decyzja (raz na rabunek)
        private static Sess Decide(RaidEventComponent comp)
        {
            Sess ss;
            if (_sess.TryGetValue(comp, out ss)) return ss;
            if (_notMine.Contains(comp)) return null;
            bool mine = false;
            Village v = null;
            try
            {
                var c = Campaign.Current;
                var me = comp.MapEvent;
                var st = comp.MapEventSettlement;
                v = st != null ? st.Village : null;
                float per;
                var s = Settings.Current;
                // R = 0 dalby krok bez konca (postep stoi) - wtedy rabunek gry pod E4/5
                if (On && s.DevastationPerRaiderDay > 0f && c != null && c.GameStarted && me != null && v != null && !me.IsPlayerMapEvent && Devastation.Covers(v, out per))
                {
                    var att = comp.AttackerSide;
                    var lp = att != null ? att.LeaderParty : null;
                    var mp = lp != null ? lp.MobileParty : null;
                    mine = mp != null && mp != MobileParty.MainParty;
                }
            }
            catch (Exception e) { mine = false; Stumble("Decide", e); }
            if (!mine) { _notMine.Add(comp); return null; }
            ss = new Sess { Comp = comp, V = v };
            ss.Rs = Devastation.StateOf(comp, v, true);           // TEN SAM stan rabunku E4/5 (v2): jency E4/9, Inni, klucz sesji ksiegi wiosek
            _sess[comp] = ss;
            _dStarted++;
            try { Restore(ss); } catch (Exception e) { Stumble("Restore", e); }
            return ss;
        }

        /// <summary>Sesja oddana grze (gracz w zdarzeniu, potkniecia): czesc kroku rozliczona, lup za zrobiona czesc; dalej rabunek gry pod E4/5 (ten sam stan rabunku).</summary>
        private static void Release(Sess ss, RaidEventComponent comp)
        {
            try
            {
                if (ss.Started && !ss.Done)
                {
                    double rest = Math.Min(ss.Prog, ss.Work) - ss.Struck;
                    if (rest > 1e-9) StrikeChunk(ss, rest, comp);
                    Loot(ss, comp);
                }
            }
            catch (Exception e) { Stumble("Release", e); }
            _sess.Remove(comp);
            _notMine.Add(comp);
            _dReleased++;
        }

        // ------------------------------------------------------------ prefiks RaidEventComponent.Update(ref bool finish) - Priority.First
        public static bool UpdatePrefix(RaidEventComponent __instance, ref bool finish)
        {
            if (__instance == null) return true;
            Sess ss;
            try { ss = Decide(__instance); }
            catch (Exception e) { Stumble("UpdatePrefix(decyzja)", e); return true; }
            if (ss == null) return true;
            long t0 = Stopwatch.GetTimestamp();
            try
            {
                var me = __instance.MapEvent;
                if (me == null) return true;
                if (me.IsPlayerMapEvent) { Release(ss, __instance); return true; }      // gracz w zdarzeniu - droga gry (E7/R2 poprowadzi gracza)
                var def = __instance.DefenderSide; var att = __instance.AttackerSide;
                if (def == null || att == null || def.TroopCount > 0) return true;       // walka (milicja, odsiecz) - gra i MapEvent
                var v = ss.V; var st = v.Settlement;
                me.WasEverInLootingPhase = true;                                          // jak G :164 (odsiecz -> bitwa polowa, rabunek z morza)
                if (ss.Done) { finish = true; return false; }
                int men = att.TroopCount;
                if (!ss.Started) Start(ss, __instance, men);
                double dt = TickDays();
                if (men > 0 && dt > 0.0)
                {
                    ss.F = Factor(att, st, ss.F);
                    double r = Math.Max(0f, Settings.Current.DevastationPerRaiderDay);
                    ss.Prog += r * men * ss.F * ss.U * dt;
                    ss.PdPend += men * dt; ss.EatDays += dt; ss.LootDays += dt;
                    double chunk = ss.Work / Chunks;
                    int guard = 0;
                    while (chunk > 0.0 && ss.Struck < ss.Work - 1e-9 && ss.Prog >= ss.Struck + chunk - 1e-9 && guard++ < Chunks + 1)
                        StrikeChunk(ss, Math.Min(chunk, ss.Work - ss.Struck), __instance);
                }
                double q = ss.Work > 0.0 ? Math.Min(1.0, ss.Prog / ss.Work) : 1.0;
                if (q >= 1.0 - 1e-9)
                {
                    // koniec kroku AI - dokladnie zakonczenie gry (G :307-313); oryginal nie biegnie (inaczej zloto i towar z niczego - G :179-188, :272-301)
                    if (ss.Work - ss.Struck > 1e-9) StrikeChunk(ss, ss.Work - ss.Struck, __instance);
                    Loot(ss, __instance);
                    ss.Done = true;
                    _setHp(st, 0f);
                    _setDmg(__instance, 1f);
                    if (me.BattleState != BattleState.AttackerVictory) _setBs(me, BattleState.AttackerVictory);
                    finish = true;
                    st.Party.SetLevelMaskIsDirty();
                    return false;
                }
                _setHp(st, ss.Hp0 * (float)(1.0 - PointsKeep * q));
                _setDmg(__instance, (float)q);
                return false;
            }
            catch (Exception e)
            {
                ss.Stumbles++;
                Stumble("UpdatePrefix", e);
                if (ss.Stumbles >= MaxStumbles) { Release(ss, __instance); return true; }
                return false;
            }
            finally { _dTicks += Stopwatch.GetTimestamp() - t0; }
        }

        /// <summary>Poczatek kroku: osady okregu, Z w zasiegu, m, praca w ludziach, u, ludzie w domu (udzial lupu), punkty startu.</summary>
        private static void Start(Sess ss, RaidEventComponent comp, int men)
        {
            var s = Settings.Current;
            var v = ss.V;
            ss.Started = true;
            ss.Hp0 = Math.Max(0.01f, Math.Min(1f, v.Settlement.SettlementHitPoints));
            double home, away, open, reach;
            if (!Devastation.DistrictPeople(v, out home, out away, out open, out reach)) { home = away = open = reach = 0.0; }
            double l = home + away;
            double n;
            if (!VillageBook.DistrictN(v, out n)) n = Math.Max(1.0, Math.Round(l / Math.Max(50, Math.Min(5000, s.DistrictSettlementPeople))));
            double sv = n > 0.0 ? l / n : 0.0;
            int d = Math.Max(20, Math.Min(2000, s.RaidDetachmentMen));
            double z = sv > 0.0 ? Math.Floor(open / sv) : 0.0;
            if (z >= 1.0) ss.M = Math.Min(z, Math.Max(1.0, Math.Floor(men / (double)d)));
            else { ss.M = 0.0; ss.BurnedOut = true; _dBurnedOut++; }           // okreg wypalony (Z = 0): nic do spalenia - koniec od razu (Looted, czujnosc 1 doba)
            ss.Work = ss.BurnedOut ? 0.0 : Math.Max(0.0, Math.Min(ss.M * sv, open));
            ss.U = reach > 0.0 ? open / reach : 0.0;
            if (ss.U < UFloor) { ss.U = UFloor; ss.UClamped = true; _dUClamped++; }
            ss.L0 = l; ss.Home0 = home; ss.Sv = sv;
            try { var lp = comp.AttackerSide.LeaderParty; var h = lp != null ? lp.LeaderHero : null; ss.Lord = h != null && h.IsLord; }
            catch { ss.Lord = false; }
        }

        /// <summary>Mnoznik tempa z czynnego modelu rabunku (perki gry, BK, BKROT, NavalDLC - wszystko AddFactor): ResultNumber / BaseNumber; przy bazie 0 - poprzedni.</summary>
        private static double Factor(MapEventSide att, Settlement st, double prev)
        {
            try
            {
                var en = Campaign.Current.Models.RaidModel.CalculateHitDamage(att, st.SettlementHitPoints);
                double b = en.BaseNumber;
                if (b > 1e-12) { double f = en.ResultNumber / b; if (f > 0.0 && !double.IsInfinity(f)) return f; }
            }
            catch (Exception e) { Stumble("Factor", e); }
            return prev;
        }

        /// <summary>Uderzenie: `people` ludzi przeszukanej czesci kroku przez Devastation.StrikePeople (wspolny rdzen, ten sam stan rabunku), potem posilek ze spichlerza.</summary>
        private static void StrikeChunk(Sess ss, double people, RaidEventComponent comp)
        {
            if (!(people > 0.0)) return;
            double pd = ss.PdPend > 0.0 ? ss.PdPend : 1e-9;
            double d, f, r;
            double h = Devastation.StrikePeople(ss.V, people, pd, ss.Rs, null, out d, out f, out r);
            ss.Struck += people; ss.PdPend = 0.0;
            ss.Hit += h; ss.Dead += d; ss.Fled += f; ss.Ref += r;
            _dHit += h; _dDead += d; _dFled += f; _dRef += r; _dStrikes++;
            Eat(ss, comp);
        }

        /// <summary>K4: kazda partia napastnika dostaje ze spichlerza okregu zboze rowne swemu zuzyciu od ostatniego posilku (Famine.Take, 20 racji za sztuke).</summary>
        private static void Eat(Sess ss, RaidEventComponent comp)
        {
            double days = ss.EatDays;
            ss.EatDays = 0.0;
            if (!(days > 0.0)) return;
            var grain = DefaultItems.Grain;
            var att = comp.AttackerSide;
            if (grain == null || att == null) return;
            foreach (var mep in att.Parties)
            {
                try
                {
                    var mp = mep != null && mep.Party != null ? mep.Party.MobileParty : null;
                    if (mp == null || mp.ItemRoster == null) continue;
                    double need = Math.Max(0f, -mp.FoodChange) * days;
                    int pieces = MBRandom.RoundRandomized((float)need);
                    if (pieces <= 0) continue;
                    double f0, f1;
                    bool hasF = Famine.FoodOf(ss.V, out f0);
                    int g0 = mp.ItemRoster.GetItemNumber(grain);
                    int got = Famine.Take(ss.V, pieces, RationsPerGrain);
                    if (got <= 0) continue;
                    var gf = GoodsLedger.Begin(GoodsLedger.FRaid, mp);
                    try { mp.ItemRoster.AddToCounts(grain, got); }
                    finally { GoodsLedger.End(gf); }
                    int g1 = mp.ItemRoster.GetItemNumber(grain);
                    double drop = hasF && Famine.FoodOf(ss.V, out f1) ? f0 - f1 : got * RationsPerGrain;
                    _dEat += got;
                    _dBal3 += (g1 - g0) * RationsPerGrain - drop;            // niezalezny bilans: zjedzone (zboze w taborze) - ubytek spichlerza
                }
                catch (Exception e) { Stumble("Eat", e); }
            }
        }

        private static long Pieces(ItemRoster r)
        {
            long n = 0;
            if (r == null) return 0;
            for (int i = 0; i < r.Count; i++) { var el = r.GetElementCopyAtIndex(i); if (el.Amount > 0) n += el.Amount; }
            return n;
        }

        /// <summary>
        /// Lup sesji (raz): udzial s = trafieni sesji / ludzie w domu na starcie. Zloto s x kiesa wsi przez SmallGold.PayRaidLoot (platnik E5/A7); towar s z magazynu
        /// wsi - konie i zywy inwentarz z napastnikiem, reszta wedlug wolnego udzwigu (najcenniejsze na kilogram najpierw), nadwyzka "spalone w rabunku".
        /// </summary>
        private static void Loot(Sess ss, RaidEventComponent comp)
        {
            if (ss.LootGiven) return;
            ss.LootGiven = true;
            double s = ss.Home0 > 0.0 ? Clamp01(ss.Hit / ss.Home0) : 0.0;
            if (!(s > 0.0)) return;
            var v = ss.V; var st = v.Settlement;
            var att = comp.AttackerSide;
            var lp = att != null ? att.LeaderParty : null;
            // zloto - jeden platnik (kiesa wsi), wodz napastnika; bez wodza nic (jak gra G :177)
            try
            {
                var leader = lp != null ? lp.LeaderHero : null;
                if (leader != null)
                {
                    long g0 = leader.Gold, v0 = v.Gold;
                    long due = (long)Math.Round(s * Math.Max(0, v.Gold));
                    long paid = SmallGold.PayRaidLoot(v, leader, due);
                    long g1 = leader.Gold, v1 = v.Gold;
                    _dGold += paid;
                    _dBal1 += (g1 - g0) - (v0 - v1);                          // niezalezny bilans: przyrost kiesy wodza - ubytek kiesy wsi
                }
            }
            catch (Exception e) { Stumble("Loot(zloto)", e); }
            // towar z magazynu wsi
            try
            {
                var roster = st.ItemRoster;
                if (roster == null || roster.Count == 0) return;
                var plan = new List<KeyValuePair<EquipmentElement, int>>();
                for (int i = 0; i < roster.Count; i++)
                {
                    var el = roster.GetElementCopyAtIndex(i);
                    if (el.Amount <= 0 || el.EquipmentElement.Item == null) continue;
                    int want = MBRandom.RoundRandomized(el.Amount * (float)s);
                    if (want > el.Amount) want = el.Amount;
                    if (want > 0) plan.Add(new KeyValuePair<EquipmentElement, int>(el.EquipmentElement, want));
                }
                if (plan.Count == 0) return;
                plan.Sort((a, b) =>
                {
                    bool aa = Walks(a.Key.Item), bb = Walks(b.Key.Item);
                    if (aa != bb) return aa ? -1 : 1;
                    return PerKg(b.Key.Item).CompareTo(PerKg(a.Key.Item));
                });
                // partie napastnika: wodz najpierw, wolny udzwig
                var parties = new List<MobileParty>();
                var free = new List<double>();
                var got = new List<ItemRoster>();
                if (lp != null && lp.MobileParty != null) parties.Add(lp.MobileParty);
                if (att != null) foreach (var mep in att.Parties) { var mp = mep != null && mep.Party != null ? mep.Party.MobileParty : null; if (mp != null && !parties.Contains(mp)) parties.Add(mp); }
                if (parties.Count == 0) return;
                long p0 = 0;
                foreach (var mp in parties)
                {
                    double cap = 0.0;
                    try { cap = Math.Max(0.0, mp.InventoryCapacity - mp.TotalWeightCarried); } catch { cap = 0.0; }
                    free.Add(cap); got.Add(new ItemRoster());
                    p0 += Pieces(mp.ItemRoster);
                }
                long v0 = Pieces(roster);
                long take = 0, burn = 0;
                var gf = GoodsLedger.Begin(GoodsLedger.FRaid, st);
                try
                {
                    foreach (var kv in plan)
                    {
                        var el = kv.Key; var it = el.Item; int n = kv.Value;
                        roster.AddToCounts(el, -n);
                        int left = n;
                        if (Walks(it))
                        {
                            parties[0].ItemRoster.AddToCounts(el, left); got[0].AddToCounts(el, left);
                            take += left; _dTakeValue += (long)it.Value * left; left = 0;
                        }
                        else
                        {
                            double w = Math.Max(0f, it.Weight);
                            for (int p = 0; p < parties.Count && left > 0; p++)
                            {
                                int fit = w > 1e-6 ? (int)Math.Min(left, Math.Floor(free[p] / w)) : left;
                                if (fit <= 0) continue;
                                parties[p].ItemRoster.AddToCounts(el, fit); got[p].AddToCounts(el, fit);
                                free[p] -= fit * w; left -= fit;
                                take += fit; _dTakeValue += (long)it.Value * fit;
                            }
                        }
                        if (left > 0)
                        {
                            burn += left; _dBurnValue += (long)it.Value * left;
                            ArmsBook.Sink(ArmsBook.KRaid, it, left);                   // uzbrojenie - ujscie z przyczyna (E6/0); towar handlowy - ramka FRaid ksiegi 146
                        }
                    }
                }
                finally { GoodsLedger.End(gf); }
                long v1 = Pieces(roster), p1 = 0;
                foreach (var mp in parties) p1 += Pieces(mp.ItemRoster);
                _dTake += take; _dBurn += burn;
                _dBal2 += (p1 - p0) + burn - (v0 - v1);                          // niezalezny bilans: towar napastnika + spalone - ubytek magazynu
                for (int p = 0; p < parties.Count; p++)
                {
                    if (got[p].Count == 0) continue;
                    try { CampaignEventDispatcher.Instance.OnItemsLooted(parties[p], got[p]); }   // jak oryginal (G :302-305): znaczniki nowych przedmiotow gry
                    catch (Exception e) { Stumble("Loot(OnItemsLooted)", e); }
                }
            }
            catch (Exception e) { Stumble("Loot(towar)", e); }
        }

        private static bool Walks(ItemObject it) { return it != null && (it.HasHorseComponent || it.IsAnimal); }
        private static double PerKg(ItemObject it) { return it == null ? 0.0 : it.Value / Math.Max(0.1, (double)it.Weight); }

        // ------------------------------------------------------------ koniec rabunku: RaidEventComponent.OnBeforeFinalize - prefiks i finalizer Priority.First
        public static void EndPre(RaidEventComponent __instance, out EndCtx __state)
        {
            __state = null;
            Sess ss;
            if (__instance == null || !_sess.TryGetValue(__instance, out ss)) return;
            try
            {
                if (ss.Started && !ss.Done)
                {
                    // przerwany krok (AI, odsiecz, pokoj): czesc kroku w ksiedze, lup za zrobiona czesc - gdy napastnik nie przegral; bez czujnosci (punkty > 0 -> Normal)
                    double rest = Math.Min(ss.Prog, ss.Work) - ss.Struck;
                    if (rest > 1e-9) StrikeChunk(ss, rest, __instance);
                    var att = __instance.AttackerSide;
                    bool lost = __instance.BattleState == BattleState.DefenderVictory || att == null || att.TroopCount <= 0;
                    if (!lost) Loot(ss, __instance);
                    _setDmg(__instance, ss.Work > 0.0 ? (float)Math.Min(1.0, ss.Prog / ss.Work) : 0f);   // kara relacji gry za zrobiona czesc (K3)
                }
                __state = new EndCtx { Ss = ss, Cap0 = Devastation.CapTakenAll, RaidDead = Devastation.RaidDead(__instance) };
            }
            catch (Exception e) { Stumble("EndPre", e); }
        }

        public static Exception EndFin(RaidEventComponent __instance, EndCtx __state, Exception __exception)
        {
            try
            {
                if (__state != null && __state.Ss != null && __state.Ss.Started)
                {
                    var ss = __state.Ss;
                    _dEnded++;
                    _dRaidDead += __state.RaidDead; _dCheckDead += ss.Dead;
                    _dCap += Devastation.CapTakenAll - __state.Cap0;              // jency E4/9 z tej sesji (BK ExecuteCapture na RaidCompleted -> ThrallLaw)
                    if (ss.Done)
                    {
                        _dSteps++; _dSettl += ss.M;
                        if (ss.Lord && ss.M >= 1.0 && _dDaysPer.Count < 4096) _dDaysPer.Add(ss.LootDays / ss.M);
                        var v = ss.V;
                        if (v != null && v.VillageState == Village.VillageStates.Looted && v.Settlement != null && v.Settlement.StringId != null)
                        {
                            var s = Settings.Current;
                            double d = ss.L0 > 0.0 ? ss.Hit / ss.L0 : 0.0;
                            int days = (int)Math.Ceiling(1.0 + 100.0 * d);
                            days = Math.Max(1, Math.Min(Math.Max(1, Math.Min(60, s != null ? s.RaidAlertMaxDays : 17)), days));
                            _alert[v.Settlement.StringId] = Today() + days;
                            _dAlertNew++;
                        }
                    }
                    else _dInterrupted++;
                }
            }
            catch (Exception e) { Stumble("EndFin", e); }
            finally
            {
                if (__instance != null) { _sess.Remove(__instance); _notMine.Remove(__instance); }
            }
            return __exception;
        }

        // ------------------------------------------------------------ czujnosc: VillageHealCampaignBehavior.DailyTickSettlement - prefiks (tylko odklada leczenie)
        public static bool HealPrefix(Settlement settlement)
        {
            try
            {
                // recenzja E7/R1: wylacznik zdjety (MCM) - wies leczy sie jak w grze takze z czujnoscia z zapisu albo sprzed zdjecia (off = stan sprzed R1)
                if (_alert.Count == 0 || !On || settlement == null || !settlement.IsVillage || settlement.StringId == null) return true;
                int end;
                if (!_alert.TryGetValue(settlement.StringId, out end)) return true;
                var v = settlement.Village;
                if (Today() < end && v != null && v.VillageState == Village.VillageStates.Looted) { _dHealHeld++; return false; }
                return true;
            }
            catch (Exception e) { Stumble("HealPrefix", e); return true; }
        }

        // ------------------------------------------------------------ zapis (VillageBook.Export / ApplyPending)
        private static bool BadId(string id) { return string.IsNullOrEmpty(id) || id.IndexOf('|') >= 0 || id.IndexOf('=') >= 0 || id.IndexOf(';') >= 0 || id.IndexOf('/') >= 0; }

        /// <summary>"|r:id=hp0/prog/work/m/u/l0/home0/sv/struck/pd/hit/dead/fled/ref/capw/eat/loot/f/flagi;...|c:id=doba;..." - sesje w toku (niezakonczone) i czujnosc.</summary>
        internal static void Export(StringBuilder sb)
        {
            int nr = 0, nc = 0;
            sb.Append("|r:");
            bool first = true;
            foreach (var ss in _sess.Values)
            {
                if (ss == null || !ss.Started || ss.Done || ss.V == null || ss.V.Settlement == null || BadId(ss.V.Settlement.StringId)) continue;
                var rs = ss.Rs;
                if (!first) sb.Append(';');
                first = false;
                int flags = (ss.BurnedOut ? 1 : 0) | (ss.UClamped ? 2 : 0) | (ss.Lord ? 4 : 0) | (ss.LootGiven ? 8 : 0);
                sb.Append(ss.V.Settlement.StringId).Append('=')
                  .Append(F(ss.Hp0, "R")).Append('/').Append(F(ss.Prog, "R")).Append('/').Append(F(ss.Work, "R")).Append('/').Append(F(ss.M, "R")).Append('/')
                  .Append(F(ss.U, "R")).Append('/').Append(F(ss.L0, "R")).Append('/').Append(F(ss.Home0, "R")).Append('/').Append(F(ss.Sv, "R")).Append('/')
                  .Append(F(ss.Struck, "R")).Append('/').Append(F(ss.PdPend, "R")).Append('/').Append(F(ss.Hit, "R")).Append('/').Append(F(ss.Dead, "R")).Append('/')
                  .Append(F(ss.Fled, "R")).Append('/').Append(F(ss.Ref, "R")).Append('/').Append(F(rs != null ? rs.CapWould : 0.0, "R")).Append('/')
                  .Append(F(ss.EatDays, "R")).Append('/').Append(F(ss.LootDays, "R")).Append('/').Append(F(ss.F, "R")).Append('/').Append(flags.ToString(Inv));
                nr++;
            }
            foreach (var kv in _pendingR)                                       // wczytane, a jeszcze nieodebrane (zapis zaraz po wczytaniu) - nie gubimy
            {
                if (BadId(kv.Key)) continue;
                if (!first) sb.Append(';');
                first = false;
                sb.Append(kv.Key).Append('=').Append(kv.Value);
                nr++;
            }
            sb.Append("|c:"); first = true;
            int today = Today();
            foreach (var kv in _alert)
            {
                if (BadId(kv.Key) || kv.Value <= today) continue;
                if (!first) sb.Append(';');
                first = false;
                sb.Append(kv.Key).Append('=').Append(kv.Value.ToString(Inv));
                nc++;
            }
            try { var s = Settings.Current; if (s != null && s.LogEnabled) Log.Info("Rabunek osada po osadzie (E7/R1): zapis - sesji " + nr + ", czujnosc " + nc + " wsi."); }
            catch { }
        }

        /// <summary>Wczytanie (VillageBook.ApplyPending - po Reset konstruktora): sesje czekaja na swoj rabunek (pierwsze wywolanie prefiksu przy tej wsi), czujnosc od razu.</summary>
        internal static void Import(string sr, string sc)
        {
            _pendingR.Clear(); _alert.Clear(); _pendingDays = 0;
            int bad = 0;
            if (!string.IsNullOrEmpty(sr))
                foreach (var item in sr.Split(';'))
                {
                    int eq = item.IndexOf('=');
                    if (eq <= 0) { if (item.Length > 0) bad++; continue; }
                    string id = item.Substring(0, eq), body = item.Substring(eq + 1);
                    if (body.Split('/').Length != 19) { bad++; continue; }
                    _pendingR[id] = body;
                }
            if (!string.IsNullOrEmpty(sc))
                foreach (var item in sc.Split(';'))
                {
                    int eq = item.IndexOf('='), end;
                    if (eq <= 0 || !int.TryParse(item.Substring(eq + 1), NumberStyles.Integer, Inv, out end)) { if (item.Length > 0) bad++; continue; }
                    _alert[item.Substring(0, eq)] = end;
                }
            try
            {
                var s = Settings.Current;
                if (s != null && s.LogEnabled)
                    Log.Info("Rabunek osada po osadzie (E7/R1): wczytanie - sesji " + _pendingR.Count + ", czujnosc " + _alert.Count + " wsi" + (bad > 0 ? ", zepsute wpisy " + bad : "")
                             + (sr == null && sc == null ? " (zapis bez sekcji r:/c: - stary zapis albo bez rabunkow)" : "") + ".");
            }
            catch { }
        }

        private static void Restore(Sess ss)
        {
            var st = ss.V != null ? ss.V.Settlement : null;
            string body;
            if (st == null || st.StringId == null || !_pendingR.TryGetValue(st.StringId, out body)) return;
            _pendingR.Remove(st.StringId);
            var a = body.Split('/');
            var x = new double[18];
            for (int i = 0; i < 18; i++)
                if (!double.TryParse(a[i], NumberStyles.Float, Inv, out x[i]) || double.IsNaN(x[i]) || double.IsInfinity(x[i])) return;   // zepsuty wpis - sesja od nowa z punktow gry
            int flags;
            if (!int.TryParse(a[18], NumberStyles.Integer, Inv, out flags)) return;
            ss.Hp0 = (float)Math.Max(0.01, Math.Min(1.0, x[0])); ss.Prog = Math.Max(0.0, x[1]); ss.Work = Math.Max(0.0, x[2]); ss.M = Math.Max(0.0, x[3]);
            ss.U = Math.Max(UFloor, Math.Min(1.0, x[4])); ss.L0 = x[5]; ss.Home0 = x[6]; ss.Sv = x[7]; ss.Struck = Math.Max(0.0, x[8]); ss.PdPend = Math.Max(0.0, x[9]);
            ss.Hit = x[10]; ss.Dead = x[11]; ss.Fled = x[12]; ss.Ref = x[13];
            ss.EatDays = Math.Max(0.0, x[15]); ss.LootDays = Math.Max(0.0, x[16]); ss.F = x[17] > 0.0 ? x[17] : 1.0;
            ss.BurnedOut = (flags & 1) != 0; ss.UClamped = (flags & 2) != 0; ss.Lord = (flags & 4) != 0; ss.LootGiven = (flags & 8) != 0;
            ss.Started = true; ss.Restored = true;
            if (ss.Rs != null) { ss.Rs.Hit = ss.Hit; ss.Rs.Dead = ss.Dead; ss.Rs.CapWould = Math.Max(0.0, x[14]); }   // stan rabunku bez zapisu - odtworzony z sesji (jency E4/9, Inni)
            _dRestored++;
        }

        // ------------------------------------------------------------ doba (ArmouryBehavior - po VillageNews.Daily)
        internal static void Daily()
        {
            try
            {
                int today = Today();
                // czujnosc: wpisy po terminie albo wsie juz nie Looted
                if (_alert.Count > 0)
                {
                    var drop = new List<string>();
                    foreach (var kv in _alert) if (kv.Value <= today) drop.Add(kv.Key);
                    foreach (var k in drop) _alert.Remove(k);
                }
                // rabunki zakonczone bez OnBeforeFinalize (nie powinno sie zdarzyc) - bez wiszacych wpisow
                if (_notMine.Count > 0)
                {
                    var gone = new List<RaidEventComponent>();
                    foreach (var c in _notMine) { var me = c != null ? c.MapEvent : null; if (me == null || me.IsFinalized) gone.Add(c); }
                    foreach (var c in gone) _notMine.Remove(c);
                }
                if (_sess.Count > 0)
                {
                    var gone = new List<RaidEventComponent>();
                    foreach (var kv in _sess) { var me = kv.Key != null ? kv.Key.MapEvent : null; if (me == null || me.IsFinalized) gone.Add(kv.Key); }
                    foreach (var c in gone) { _sess.Remove(c); _dAbandoned++; }
                }
                int unclaimed = 0;
                if (_pendingR.Count > 0 && ++_pendingDays >= 2) { unclaimed = _pendingR.Count; _pendingR.Clear(); }   // sesje z zapisu bez rabunku po wczytaniu
                Line(today, unclaimed);
            }
            catch (Exception e) { Stumble("Daily", e); }
            finally { NewDay(); }
        }

        private static double Median(List<double> xs)
        {
            if (xs.Count == 0) return double.NaN;
            var a = new List<double>(xs); a.Sort();
            int n = a.Count;
            return (n % 2) == 1 ? a[n / 2] : 0.5 * (a[n / 2 - 1] + a[n / 2]);
        }

        private static void Line(int today, int unclaimed)
        {
            var s = Settings.Current;
            if (s == null || !s.LogEnabled) return;
            int day = today - 1;
            if (!On)
            {
                if (!_offLogged)
                {
                    _offLogged = true;
                    Log.Info("Rabunek osada po osadzie (E7/R1): dzien " + day + " | WYLACZONE (" + (!s.RaidBySettlementEnabled ? "Raid By Settlement Enabled w MCM" : "latki niewpiete: " + string.Join(", ", _missing.ToArray()))
                             + ") - rabunek AI jak w E4/5 (spustoszenie od osobodni, lup z kiesy wsi E5/A7).");
                }
                if (_sess.Count == 0 && _alert.Count == 0 && _dEnded == 0) return;
            }
            double check = _dHit - _dDead - _dFled - _dRef;
            double med = Median(_dDaysPer);
            int act = 0, actR = 0;
            foreach (var ss in _sess.Values) if (ss != null && ss.Started && !ss.Done) { act++; if (ss.Restored) actR++; }
            var sb = new StringBuilder(1600);
            sb.Append("Rabunek osada po osadzie (E7/R1): dzien ").Append(day)
              .Append(" | sesji ").Append(_dEnded).Append(" (AI ").Append(_dEnded).Append(", gracz 0)")
              .Append(", krokow ").Append(_dSteps).Append(" (osad ").Append(F(_dSettl, "0")).Append(')')
              .Append(", dni na osade mediana ").Append(double.IsNaN(med) ? "-" : F(med, "0.00")).Append(" (lordowie ").Append(_dDaysPer.Count);
            if (_dDaysPer.Count > 0)
            {
                sb.Append(": ");
                for (int i = 0; i < _dDaysPer.Count && i < MaxDaysList; i++) sb.Append(i > 0 ? "/" : "").Append(F(_dDaysPer[i], "0.00"));
            }
            sb.Append(')')
              .Append(", przerwanych ").Append(_dInterrupted).Append(" (").Append(_dEnded > 0 ? F(100.0 * _dInterrupted / _dEnded, "0") : "0").Append("%)");
            sb.Append(" | trafieni ").Append(F(_dHit, "0.##")).Append(" = zabici ").Append(F(_dDead, "0.##")).Append(" + las ").Append(F(_dFled, "0.##"))
              .Append(" + jency 0 + uchodzcy ").Append(F(_dRef, "0.##")).Append(" (kontrola ").Append(F(check, "0.000")).Append("; uderzen ").Append(_dStrikes).Append(')');
            sb.Append(" | lup: zloto z kiesy wsi ").Append(_dGold).Append(" zl, towar z magazynu ").Append(_dTake).Append(" szt. (").Append(_dTakeValue).Append(" zl), spalone w rabunku ")
              .Append(_dBurn).Append(" szt. (").Append(_dBurnValue).Append(" zl), jedzenie ze spichlerza ").Append(_dEat).Append(" szt. (").Append(F(_dEat * RationsPerGrain, "0")).Append(" racji)");
            sb.Append(" | bilans (v2): zloto wodza - kiesa wsi ").Append(F(_dBal1, "0")).Append(", towar + spalone - magazyn ").Append(F(_dBal2, "0"))
              .Append(", zjedzone - spichlerz ").Append(F(_dBal3, "0.###"));
            sb.Append(" | jency z rabunkow R1 ").Append(_dCap).Append(", zabici w stanie rabunku (RaidDead) ").Append(F(_dRaidDead, "0.#")).Append(" (zabici R1 ").Append(F(_dCheckDead, "0.#")).Append(')');
            sb.Append(" | czujnosc: wsi ").Append(_alert.Count).Append(" (nowe ").Append(_dAlertNew).Append(", leczenie wstrzymane ").Append(_dHealHeld).Append(')');
            sb.Append(" | w toku: sesji ").Append(act).Append(" (z zapisu ").Append(actR).Append("), nowych ").Append(_dStarted).Append(", podjetych z zapisu ").Append(_dRestored);
            if (unclaimed > 0) sb.Append(", z zapisu bez rabunku ").Append(unclaimed);
            sb.Append(" | okreg wypalony ").Append(_dBurnedOut).Append(", u przyciete do ").Append(F(UFloor, "0.00")).Append(' ').Append(_dUClamped)
              .Append(", oddane grze (gracz w zdarzeniu, potkniecia) ").Append(_dReleased);
            if (_dAbandoned > 0) sb.Append(", porzucone bez konca rabunku ").Append(_dAbandoned);
            sb.Append(" | czas ").Append(F(_dTicks * 1000.0 / Stopwatch.Frequency, "0.0")).Append(" ms");
            sb.Append(" | potkniecia ").Append(_dStumbles).Append('.');
            Log.Info(sb.ToString());
        }

        // ------------------------------------------------------------ wpiecia (SubModuleMain - po Devastation)
        internal static void ApplyAll(Harmony h)
        {
            _wired.Clear(); _missing.Clear(); _ready = false;
            if (h == null) { _missing.Add("Harmony"); StartLine(); return; }
            try
            {
                var hp = AccessTools.PropertySetter(typeof(Settlement), "SettlementHitPoints");
                var dmg = AccessTools.PropertySetter(typeof(RaidEventComponent), "RaidDamage");
                var bs = AccessTools.PropertySetter(typeof(MapEvent), "BattleState");
                if (hp != null) _setHp = (Action<Settlement, float>)Delegate.CreateDelegate(typeof(Action<Settlement, float>), hp); else _missing.Add("Settlement.SettlementHitPoints (setter)");
                if (dmg != null) _setDmg = (Action<RaidEventComponent, float>)Delegate.CreateDelegate(typeof(Action<RaidEventComponent, float>), dmg); else _missing.Add("RaidEventComponent.RaidDamage (setter)");
                if (bs != null) _setBs = (Action<MapEvent, BattleState>)Delegate.CreateDelegate(typeof(Action<MapEvent, BattleState>), bs); else _missing.Add("MapEvent.BattleState (setter)");
            }
            catch (Exception e) { _missing.Add("settery (" + e.GetType().Name + ")"); Log.Error("DistrictRaid.ApplyAll(settery)", e); }
            if (_setHp == null || _setDmg == null || _setBs == null) { StartLine(); return; }
            bool upd = false, end = false;
            try
            {
                var m = AccessTools.Method(typeof(RaidEventComponent), "Update", new[] { typeof(bool).MakeByRefType() });
                if (m != null && m.DeclaringType == typeof(RaidEventComponent))
                {
                    h.Patch(m, prefix: new HarmonyMethod(typeof(DistrictRaid), nameof(UpdatePrefix)) { priority = Priority.First });
                    upd = true; _wired.Add("krok rabunku (Update, First)");
                }
                else _missing.Add("RaidEventComponent.Update");
            }
            catch (Exception e) { _missing.Add("RaidEventComponent.Update (" + e.GetType().Name + ")"); Log.Error("DistrictRaid.ApplyAll(Update)", e); }
            if (upd)
            {
                try
                {
                    var m = AccessTools.Method(typeof(RaidEventComponent), "OnBeforeFinalize");
                    if (m != null && m.DeclaringType == typeof(RaidEventComponent) && m.GetParameters().Length == 0)
                    {
                        h.Patch(m, prefix: new HarmonyMethod(typeof(DistrictRaid), nameof(EndPre)) { priority = Priority.First },
                                   finalizer: new HarmonyMethod(typeof(DistrictRaid), nameof(EndFin)) { priority = Priority.First });
                        end = true; _wired.Add("koniec rabunku (OnBeforeFinalize)");
                    }
                    else _missing.Add("RaidEventComponent.OnBeforeFinalize");
                }
                catch (Exception e) { _missing.Add("RaidEventComponent.OnBeforeFinalize (" + e.GetType().Name + ")"); Log.Error("DistrictRaid.ApplyAll(OnBeforeFinalize)", e); }
            }
            try
            {
                var m = AccessTools.Method(typeof(VillageHealCampaignBehavior), "DailyTickSettlement", new[] { typeof(Settlement) });
                if (m != null)
                {
                    h.Patch(m, prefix: new HarmonyMethod(typeof(DistrictRaid), nameof(HealPrefix)));
                    _wired.Add("czujnosc (VillageHealCampaignBehavior.DailyTickSettlement)");
                }
                else _missing.Add("VillageHealCampaignBehavior.DailyTickSettlement (czujnosc nieczynna - wies leczy sie jak w grze)");
            }
            catch (Exception e) { _missing.Add("DailyTickSettlement (" + e.GetType().Name + ")"); Log.Error("DistrictRaid.ApplyAll(czujnosc)", e); }
            // prefiks Update bez konca rabunku liczylby sesje bez rozliczenia - R1 czynne tylko z obiema latkami
            _ready = upd && end;
            StartLine();
        }

        private static void StartLine()
        {
            var s = Settings.Current;
            Log.Info("Rabunek osada po osadzie (E7/R1): latki przy starcie gry - " + (_ready ? "GOTOWE" : "NIECZYNNE") + "; wpiete: " + (_wired.Count > 0 ? string.Join(", ", _wired.ToArray()) : "nic")
                     + "; BRAK: " + (_missing.Count > 0 ? string.Join(", ", _missing.ToArray()) : "-")
                     + (s != null ? "; ustawienia startowe przed MCM (stan - linia doby): klucz " + (s.RaidBySettlementEnabled ? "TAK" : "NIE") + ", oddzial " + s.RaidDetachmentMen
                                    + " ludzi, czujnosc najwyzej " + s.RaidAlertMaxDays + " dob, R " + F(Math.Max(0f, s.DevastationPerRaiderDay), "0.##") + " czlowieka na zbrojnego na dobe" : "") + ".");
        }
    }
}
