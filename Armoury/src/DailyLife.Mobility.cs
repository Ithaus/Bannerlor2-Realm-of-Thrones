using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// ZC6 - DOSTATEK CZYNNY: AWANS I SPADEK MIEDZY STANAMI (projekt docs/PROJEKT-ZYCIE-CODZIENNE-2026-10-16.md rozdz. 10 i 14 "ZC6"; klucze DailyLifeMobility
    /// i DailyLifeFreedom, oba z DailyLifeEstates; dobrobyt - DailyLife.Prosperity.cs). Jeff 16.10: "dobrobyt naprawde zwieksza zamoznosc obszaru, wobec czego coraz
    /// wiecej ludzi moze awansowac spolecznie; wojny niszcza dobrobyt". Udzialy stanow ksiegi stanow (ZC2, milionowe, suma 1 000 000) przesuwane RAZ NA TYDZIEN na
    /// osade - co dobe 1/7 miast i wsi (koszyk wedlug skrotu id) - w grupach (ludzie = udzial x ludzie ksiegi osady); ulamek milionowej rozstrzygany deterministycznie
    /// (skrot id + doba + przejscie: wartosc oczekiwana zachowana, bez licznikow ulamkow w zapisie - zapis wczytany ponownie daje te same ruchy). Suma ludzi bez zmian
    /// (przesuniecie etykiet); D stanu = Dostatek tej doby (srednia 28 dob dochodu, ZC0); osada z mniej niz 7 dobami sredniej - bez przegladu (rozgrzewka).
    ///  W GORE (e -> e+1): na tydzien = 3%/rok x 7/364 x ludzie stanu x clamp((D - U) / U, 0, 1) x min(1, zapas / zapas potrzebny) x miejsce:
    ///   - chlopi -> rzemieslnicy (wies): U 2.5, kiesa wsi >= 60 dni koszyka, miejsce rzemieslnicy wsi <= 6%;
    ///   - biedota -> rzemieslnicy (miasto): U 2.5, kiesa W >= 30 dni, miejsce <= 1.5 x pasma; z W do R (cech = kiesa R) oplata cechowa 30 dni koszyka rzemieslnika,
    ///     a gdy oszczednosci przechodzacych (ich czesc kiesy W) sa wieksze - one (pieniadze ida z ludzmi);
    ///   - rzemieslnicy -> kupcy: U 4, kiesa R (miasto) / wsi >= 60 dni, miejsce <= 1.5 x pasma; miasto - oszczednosci z R do P;
    ///   - kupcy (i zamozni chlopi wsi) -> szlachta: U 10, kiesa P / wsi >= 180 dni, miejsce: szlachta krainy (kultura) <= 3%; KUPNO ZIEMI: rok koszyka szlachty na
    ///     glowe (364 x zwykly koszyk / sigma zl gry) do glowy rodu pana osady (miasto - z kiesy P, TownFolk.ZcPayLand; wies - z kiesy wsi, GiveGoldAction); bez
    ///     zywego pana, w oblezeniu, we wsi najezdzanej - bez kupna; brak pieniedzy - mniej ludzi (ilu stac).
    ///   "miejsce": pelne tempo do 3/4 sufitu, dalej liniowo do 0 na suficie.
    ///  W DOL (e -> e-1), gdy D stanu < L przez 4 kolejne przeglady tygodniowe (28 dob; licznik w zapisie) i zapas < 14 dni: 6%/rok x 7/364 x ludzie x
    ///   clamp((L - D) / L, 0, 1): rzemieslnicy L 1.3 (-> biedota / chlopi wsi), kupcy L 2.5 (-> rzemieslnicy), szlachta L 4 (-> kupcy w miescie / chlopi na wsi -
    ///   "sprzedane majatki"; szlachta wsi zyje z majatkow BK bez kiesy - spadek tylko od Dostatku). Miasto: oszczednosci z ludzmi (R -> W, P -> R).
    ///  NIEWOLNI: wyzwolenia 0.5%/rok (do biedoty w miescie, do chlopow na wsi); DailyLifeFreedom - wyzwolenie przez zdobywce: region warowni pod wladca, ktorego
    ///   kultura nie zna niewoli (LosersFlee.SlaveCulture; wladca krolestwa, bez krolestwa - glowa rodu), 10% niewolnych na 28 dob (2.6% na przeglad), bez granic pasm
    ///   i limitu 0.2%. Konto NIEWOLNI ksiegi (jency, PeopleBook) nietkniete - to ludzie ksiegi, nie etykieta.
    ///  GRANICE: udzial stanu w [0.5 x pasmo, 1.5 x pasmo] (pasma rozdz. 5.2 kultury z kalibracji), w osadzie najwyzej 0.2% ludzi na tydzien (awans, spadek, wyzwolenia).
    ///  MIGRACJA (codziennie, kazde miasto): przybysze ze wsi regionu (TownGrowth E4/8 R2, ta sama doba) to biedota - etykieta: ich udzial w innych stanach miasta
    ///   (stale udzialy rozlozylyby ich po wszystkich) przechodzi do biedoty. Wies traci migrantow wedlug swoich udzialow (95% chlopi) - bez poprawki.
    /// PIENIADZ: tylko przelewy (kiesa -> kiesa ludu, kiesa -> pan); kontrola obiegu w linii (kiesy przed - po = zaplacone panom, panowie + to samo). Nic z niczego.
    /// Linia "Awans (ZC6): dzien N" w dobie linii (liczniki od ostatniej linii); przeglad biegnie kazdej doby (takze w cichych logach i bez logu).
    /// </summary>
    internal static partial class DailyLife
    {
        private const string PM = "Awans (ZC6): ";
        private const double RUp = 0.03, RDown = 0.06, RManum = 0.005, WeekYear = 7.0 / 364.0, FreeMonth = 0.10;
        private const double UCraft = 2.5, UMerch = 4.0, UGentry = 10.0;                 // progi awansu: Dostatek stanu zrodlowego
        private const double LCraft = 1.3, LMerch = 2.5, LGentry = 4.0;                  // progi spadku
        private const double SavPeasCraft = 60, SavLabCraft = 30, SavCraftMerch = 60, SavGentry = 180, SavDown = 14;   // zapas kiesy w dniach zwyklego koszyka stanu
        private const double GuildDays = 30, LandDays = 364;
        private const double VilCraftCap = 0.06, LandGentryCap = 0.03, MoveCapWeek = 0.002, SlotSoft = 0.25;
        private const int BelowNeed = 4;
        private const int MvSlots = 4;

        // przejscia
        private const int TPC = 0, TLC = 1, TCM = 2, TMG = 3, TPG = 4, TCD = 5, TMD = 6, TGD = 7, TMan = 8, TFree = 9, TMig = 10, TN = 11;
        private static readonly string[] TPl =
        {
            "chlopi->rzemieslnicy (wies)", "biedota->rzemieslnicy", "rzemieslnicy->kupcy", "kupcy->szlachta", "zamozni chlopi->szlachta (wies)",
            "rzemieslnicy->biedota/chlopi", "kupcy->rzemieslnicy", "szlachta->kupcy/chlopi", "wyzwoleni (0.5%/rok)", "wyzwoleni przez zdobywce", "przybysze ze wsi->biedota"
        };
        private static readonly int[] TFrom = { EPeasant, ELabour, ECraft, EMerchant, EPeasant, ECraft, EMerchant, EGentry, ESlave, ESlave };
        private static bool IsUp(int t) { return t <= TPG; }
        private static bool IsDown(int t) { return t >= TCD && t <= TGD; }

        private static int ToOf(int t, bool town)
        {
            switch (t)
            {
                case TPC: case TLC: case TMD: return ECraft;
                case TCM: return EMerchant;
                case TMG: case TPG: return EGentry;
                case TCD: return town ? ELabour : EPeasant;
                case TGD: return town ? EMerchant : EPeasant;
                default: return town ? ELabour : EPeasant;      // wyzwoleni
            }
        }

        // ------------------------------------------------------------ pamiec sesji (liczniki od ostatniej linii; S - od wczytania)
        private static readonly double[] _mvT = new double[TN], _mvTS = new double[TN];
        private static int _mvDays, _mvDaysS, _mvSeen, _mvWarm, _mvCapHit, _mvBand, _mvNoPurse, _mvNoLord, _mvLandShort, _mvLandWar, _mvFreeSt, _mvOut, _mvOutFree, _mvStum;
        private static double _mvMax; private static string _mvMaxName = "-";
        private static long _gW2R, _gR2P, _gR2W, _gP2R, _gFee, _gLandT, _gLandV, _gCtl, _gPurseOut, _gLordIn, _gVilOut;
        private static int _gLandTN, _gLandVN;
        private static readonly Dictionary<string, double[]> _landK = new Dictionary<string, double[]>(StringComparer.Ordinal);   // kultura -> [szlachta, ludzie]
        private static readonly double[] _mS = new double[6], _mN = new double[6], _mBand = new double[6], _mFlow = new double[TN];
        private static readonly long[] _mPurse = new long[3], _mPurse2 = new long[3];

        internal static bool MobOn { get { var s = Settings.Current; return EstatesOn && s.DailyLifeMobility; } }
        internal static bool FreeOn { get { var s = Settings.Current; return EstatesOn && s.DailyLifeFreedom; } }

        /// <summary>Z ResetEstates (konstruktor ArmouryBehavior): liczniki ZC6 i dobrobytu od nowa (stan kampanii - pole "bbb" w arm_estates, wczytuje ImportEstates).</summary>
        private static void ResetMobility()
        {
            Array.Clear(_mvT, 0, TN); Array.Clear(_mvTS, 0, TN);
            _mvDays = _mvDaysS = 0; ZeroMobLine();
            _landK.Clear();
            ResetProsperity();
        }

        private static void ZeroMobLine()
        {
            Array.Clear(_mvT, 0, TN);
            _mvDays = _mvSeen = _mvWarm = _mvCapHit = _mvBand = _mvNoPurse = _mvNoLord = _mvLandShort = _mvLandWar = _mvFreeSt = _mvOut = _mvOutFree = _mvStum = 0;
            _mvMax = 0; _mvMaxName = "-";
            _gW2R = _gR2P = _gR2W = _gP2R = _gFee = _gLandT = _gLandV = _gCtl = _gPurseOut = _gLordIn = _gVilOut = 0; _gLandTN = _gLandVN = 0;
        }

        // ------------------------------------------------------------ skroty deterministyczne (koszyk tygodnia, ulamek milionowej)
        private static uint Fnv(string id)
        {
            uint h = 2166136261u;
            for (int i = 0; i < id.Length; i++) { h ^= id[i]; h *= 16777619u; }
            return h;
        }

        private static int BucketOf(string id) { return (int)(Fnv(id) % 7u); }

        private static double FracOf(string id, int day, int t)
        {
            uint h = Fnv(id);
            h ^= (uint)day; h *= 16777619u;
            h ^= (uint)(t * 7919 + 13); h *= 16777619u;
            h ^= h >> 15; h *= 0x2c1b3c6du; h ^= h >> 12; h *= 0x297a2d39u; h ^= h >> 15;
            return (h & 0xFFFFFFu) / 16777216.0;
        }

        /// <summary>Milionowe do przesuniecia: czesc calkowita + 1 z prawdopodobienstwem ulamka (skrot id, doby, przejscia) - wartosc oczekiwana = ppm.</summary>
        private static int RoundPpm(double ppm, string id, int day, int t)
        {
            if (!(ppm > 0) || double.IsInfinity(ppm)) return 0;
            if (ppm > PpmAll) ppm = PpmAll;
            double f = Math.Floor(ppm);
            int q = (int)f;
            if (ppm - f > FracOf(id, day, t)) q++;
            return q;
        }

        private static double Clamp01(double v) { return !(v > 0) ? 0.0 : (v > 1 ? 1.0 : v); }

        /// <summary>Miejsce: pelne tempo do 3/4 sufitu, dalej liniowo do 0 na suficie.</summary>
        private static double Slot(double share, double cap) { return cap > 0 ? Clamp01((cap - share) / (SlotSoft * cap)) : 0.0; }

        // ------------------------------------------------------------ doba
        /// <summary>Compute (doba z ksiega stanow, po LivelihoodDay i ViewDay): etykieta migracji miast (codziennie), przeglad tygodniowy osad koszyka doby,
        /// potem dobrobyt na jutro (DailyLife.Prosperity.cs). Kazda osada we wlasnym try (potkniecie, reszta liczy dalej).</summary>
        private static void ActiveDay(int day)
        {
            _mvDays++; _mvDaysS++;
            bool mob = MobOn, free = FreeOn;
            if (mob || free)
            {
                if (mob) LandShares();
                int bucket = ((day % 7) + 7) % 7;
                foreach (var r in _recs)
                {
                    if (r.Kind == KCastle || !SafeId(r.Id)) continue;
                    EBook eb;
                    if (!_eb.TryGetValue(r.Id, out eb) || eb.P == null) continue;
                    try
                    {
                        if (mob && r.Kind == KTown) MigrantsLabel(r, eb, day);
                        if (BucketOf(r.Id) != bucket) continue;
                        if (r.YDays < WarmDays) { _mvWarm++; continue; }
                        WeekOne(r, eb, day, mob, free);
                    }
                    catch (Exception e) { _mvStum++; Stumble("Awans(osada)", e); }
                }
            }
            try { ProsperityDay(day); } catch (Exception e) { Stumble("ProsperityDay", e); }
        }

        /// <summary>Szlachta krainy (kultura z kalibracji) - miejsce kupna ziemi (szlachta <= 3% krainy). Ludzie doby (przed ruchami).</summary>
        private static void LandShares()
        {
            _landK.Clear();
            foreach (var r in _recs)
            {
                if (r.Kind == KCastle || !(r.L > 0)) continue;
                string k = r.Cul ?? "?";
                double[] a;
                if (!_landK.TryGetValue(k, out a)) { a = new double[2]; _landK[k] = a; }
                a[0] += r.L * r.Share[EGentry]; a[1] += r.L;
            }
        }

        /// <summary>Przybysze ze wsi do miasta tej doby (TownGrowth.MigratedToday) to biedota: ich czesc w innych stanach (udzial x przybysze) przechodzi do biedoty.</summary>
        private static void MigrantsLabel(Rec r, EBook eb, int day)
        {
            double mig = TownGrowth.MigratedToday(r.St);
            if (!(mig > 0) || !(r.L > 0)) return;
            var p = eb.P;
            int moved = 0;
            for (int e = 0; e < 6; e++)
            {
                if (e == ELabour || p[e] <= 0) continue;
                int q = Math.Min(p[e], RoundPpm(mig * p[e] / r.L, r.Id, day, 100 + e));   // milionowe: przybysze x udzial / ludzie x 1e6
                if (q <= 0) continue;
                p[e] -= q; p[ELabour] += q; moved += q;
            }
            if (moved <= 0) return;
            double ppl = moved * r.L / PpmAll;
            eb.MigAcc += (float)ppl; _mvT[TMig] += ppl; _mvTS[TMig] += ppl;
        }

        private static void ShiftRing(EBook eb, int day)
        {
            if (eb.Mv == null) { eb.Mv = new float[MvSlots * TN]; eb.MvDay = new[] { -1, -1, -1, -1 }; }
            for (int i = MvSlots - 1; i > 0; i--)
            {
                eb.MvDay[i] = eb.MvDay[i - 1];
                Array.Copy(eb.Mv, (i - 1) * TN, eb.Mv, i * TN, TN);
            }
            eb.MvDay[0] = day;
            Array.Clear(eb.Mv, 0, TN);
            eb.Mv[TMig] = eb.MigAcc; eb.MigAcc = 0;
        }

        /// <summary>Wladca regionu osady nie zna niewoli: kultura wladcy krolestwa wlasciciela warowni (bez krolestwa - glowy rodu, a bez niej - rodu) spoza kultur z niewola.</summary>
        private static bool FreeRuler(Rec r)
        {
            var reg = (r.Region ?? r).St;
            var oc = reg != null ? reg.OwnerClan : null;
            if (oc == null) return false;
            var k = oc.Kingdom;
            var ruler = k != null ? k.Leader : oc.Leader;
            var c = ruler != null && ruler.Culture != null ? ruler.Culture : (k != null ? k.Culture : oc.Culture);
            return c != null && !LosersFlee.SlaveCulture(c);
        }

        /// <summary>Przeglad tygodniowy osady: przeplywy w gore, w dol, wyzwolenia; limit 0.2%, granice pasm, ulamek milionowej, pieniadze z ludzmi i kupno ziemi.</summary>
        private static void WeekOne(Rec r, EBook eb, int day, bool mob, bool free)
        {
            _mvSeen++;
            ShiftRing(eb, day);
            double L = r.L;
            if (!(L > 0)) return;
            bool town = r.Kind == KTown;
            var p = eb.P;
            var s = _mS; var n = _mN;
            for (int e = 0; e < 6; e++) { s[e] = p[e] / (double)PpmAll; n[e] = L * s[e]; }
            Bands(r.Cul, town, _mBand);
            double sig = Math.Max(1, _sigma);
            var u = _usual;
            // zapas kiesy w dniach zwyklego koszyka stanu: miasto - kiesy W/R/P (kiesa czynna), wies - kiesa wsi (wspolna: chlopi, rzemieslnicy, kupcy wedlug ludzi x koszyk)
            double dW = double.NaN, dR = double.NaN, dP = double.NaN, dV = double.NaN, hW = 0, hR = 0, hM = 0;
            bool purses = false;
            if (town)
            {
                purses = TownFolk.ZcPurses(r.St, _mPurse);
                if (purses)
                {
                    double nW = n[ELabour] + n[EPeasant], wsP = n[EMerchant] * u[EMerchant] + n[EGentry] * u[EGentry];
                    if (nW > 0) { hW = _mPurse[0] / nW; dW = hW * sig / u[ELabour]; }
                    if (n[ECraft] > 0) { hR = _mPurse[1] / n[ECraft]; dR = hR * sig / u[ECraft]; }
                    if (wsP > 0) { hM = _mPurse[2] * u[EMerchant] / wsP; dP = _mPurse[2] * sig / wsP; }
                }
            }
            else
            {
                int g = 0;
                try { g = r.St.Village != null ? Math.Max(0, r.St.Village.Gold) : 0; } catch { g = 0; }
                double ws = n[EPeasant] * u[EPeasant] + n[ECraft] * u[ECraft] + n[EMerchant] * u[EMerchant];
                if (ws > 0) dV = g * sig / ws;
            }
            var fl = _mFlow;
            Array.Clear(fl, 0, TN);
            if (mob)
            {
                double land = 1.0;
                double[] lk;
                if (_landK.TryGetValue(r.Cul ?? "?", out lk) && lk[1] > 0) land = Slot(lk[0] / lk[1], LandGentryCap);
                if (town) fl[TLC] = Up(n[ELabour], r.DE[ELabour], UCraft, dW, SavLabCraft) * Slot(s[ECraft], 1.5 * _mBand[ECraft]);
                else
                {
                    fl[TPC] = Up(n[EPeasant], r.DE[EPeasant], UCraft, dV, SavPeasCraft) * Slot(s[ECraft], VilCraftCap);
                    fl[TPG] = Up(n[EPeasant], r.DE[EPeasant], UGentry, dV, SavGentry) * land;
                }
                fl[TCM] = Up(n[ECraft], r.DE[ECraft], UMerch, town ? dR : dV, SavCraftMerch) * Slot(s[EMerchant], 1.5 * _mBand[EMerchant]);
                fl[TMG] = Up(n[EMerchant], r.DE[EMerchant], UGentry, town ? dP : dV, SavGentry) * land;
                fl[TCD] = Down(eb, 0, n[ECraft], r.DE[ECraft], LCraft, town ? dR : dV, true);
                fl[TMD] = Down(eb, 1, n[EMerchant], r.DE[EMerchant], LMerch, town ? dP : dV, true);
                fl[TGD] = Down(eb, 2, n[EGentry], r.DE[EGentry], LGentry, town ? dP : double.NaN, town);   // szlachta wsi - majatki BK bez kiesy: spadek tylko od D
                if (n[ESlave] > 0) fl[TMan] = RManum * WeekYear * n[ESlave];
                // w osadzie najwyzej 0.2% ludzi na tydzien (awans, spadek, wyzwolenia)
                double reg = 0;
                for (int t = 0; t <= TMan; t++) reg += fl[t];
                if (reg > MoveCapWeek * L)
                {
                    double k = MoveCapWeek * L / reg;
                    for (int t = 0; t <= TMan; t++) fl[t] *= k;
                    _mvCapHit++;
                }
            }
            bool freeHere = false;
            if (free && n[ESlave] > 0 && FreeRuler(r))
            {
                freeHere = true; _mvFreeSt++;
                fl[TFree] = (1.0 - Math.Pow(1.0 - FreeMonth, 7.0 / 28.0)) * Math.Max(0.0, n[ESlave] - fl[TMan]);
            }
            // zastosowanie: wyzwolenia, spadki, awanse (kolejnosc stala - granice pasm na biezacych udzialach)
            double regPpl = 0;
            foreach (int t in Order)
            {
                double x = fl[t];
                if (!(x > 0)) continue;
                int f = TFrom[t], d = ToOf(t, town);
                double ppm = x / L * PpmAll;
                if (t != TFree)
                {
                    double room = Math.Min(p[f] - 0.5 * _mBand[f] * PpmAll, 1.5 * _mBand[d] * PpmAll - p[d]);
                    if (!(room > 0)) { _mvBand++; continue; }
                    if (ppm > room) { ppm = room; _mvBand++; }
                }
                int q = Math.Min(p[f], RoundPpm(ppm, r.Id, day, t));
                if (q <= 0) continue;
                double ppl = q * L / PpmAll;
                // kupno ziemi (do szlachty) - ilu stac; pieniadze z ludzmi (miasto)
                if (t == TMG || t == TPG)
                {
                    q = town ? LandTown(r, q, L) : LandVillage(r, q, L);
                    if (q <= 0) continue;
                    ppl = q * L / PpmAll;
                }
                else if (town && purses)
                {
                    if (t == TLC)
                    {
                        // oplata cechowa 30 dni koszyka rzemieslnika z W do R (cech = kiesa R) - w calosci, a gdy oszczednosci przechodzacych sa wieksze, ida one
                        double fee = ppl * GuildDays * u[ECraft] / sig;
                        Carry(r, 0, 1, Math.Max(hW * ppl, fee), ref _gW2R, fee);
                    }
                    else if (t == TCM) Carry(r, 1, 2, hR * ppl, ref _gR2P, 0);
                    else if (t == TCD) Carry(r, 1, 0, hR * ppl, ref _gR2W, 0);
                    else if (t == TMD) Carry(r, 2, 1, hM * ppl, ref _gP2R, 0);
                }
                p[f] -= q; p[d] += q;
                eb.Mv[t] += (float)ppl;
                _mvT[t] += ppl; _mvTS[t] += ppl;
                if (t != TFree) regPpl += ppl;
            }
            double sh = regPpl / L;
            if (sh > _mvMax) { _mvMax = sh; _mvMaxName = r.Name; }
            // udzialy w pasmach po przegladzie (wyzwolenie przez zdobywce wolno przekroczyc - liczone osobno)
            bool out1 = false;
            for (int e = 0; e < 6 && !out1; e++)
            {
                double v = p[e] / (double)PpmAll, lo = 0.5 * _mBand[e], hi = 1.5 * _mBand[e];
                if (v < lo - 2e-6 || v > hi + 2e-6) out1 = true;
            }
            // po wyzwoleniu przez zdobywce (dzis albo kiedys - niewolnych ponizej dolu pasma, czego zwykle wyzwolenia nie robia) pasma osady sa nieaktualne
            bool freed = freeHere || (_mBand[ESlave] > 0 && p[ESlave] < 0.5 * _mBand[ESlave] * PpmAll - 2);
            if (out1) { if (freed) _mvOutFree++; else _mvOut++; }
        }

        private static readonly int[] Order = { TFree, TMan, TCD, TMD, TGD, TPC, TLC, TCM, TMG, TPG };

        /// <summary>Przeplyw w gore na tydzien (ludzie). Zapas nieznany (kiesa nieczynna) - 0 (licznik).</summary>
        private static double Up(double nFrom, double d, double uTh, double days, double need)
        {
            if (!(nFrom > 0) || !Fin(d)) return 0;
            double gap = Clamp01((d - uTh) / uTh);
            if (!(gap > 0)) return 0;
            if (!Fin(days)) { _mvNoPurse++; return 0; }
            return RUp * WeekYear * nFrom * gap * Math.Min(1.0, Math.Max(0.0, days) / need);
        }

        /// <summary>Przeplyw w dol na tydzien (ludzie): licznik przegladow z D ponizej progu (zapis), od 4 z rzedu i zapasu < 14 dni (needSav; nieznany - bez spadku).</summary>
        private static double Down(EBook eb, int k, double nFrom, double d, double lTh, double days, bool needSav)
        {
            if (!Fin(d)) return 0;                                   // brak liczb dochodu tej doby - licznik stoi
            if (d < lTh) { if (eb.Below[k] < 9) eb.Below[k]++; } else eb.Below[k] = 0;
            if (eb.Below[k] < BelowNeed || !(nFrom > 0)) return 0;
            if (needSav)
            {
                if (!Fin(days)) { _mvNoPurse++; return 0; }
                if (days >= SavDown) return 0;
            }
            return RDown * WeekYear * nFrom * Clamp01((lTh - d) / lTh);
        }

        private static long PurseSum(Settlement st)
        {
            return TownFolk.ZcPurses(st, _mPurse2) ? _mPurse2[0] + _mPurse2[1] + _mPurse2[2] : 0;
        }

        /// <summary>Oszczednosci ludzi z kiesy `from` do kiesy `to` (miasto). fee - oplata cechowa (czesc przeniesionych, do R). Kontrola: suma kies bez zmian.</summary>
        private static void Carry(Rec r, int from, int to, double x, ref long sum, double fee)
        {
            long want = (long)Math.Round(x);
            if (want <= 0) return;
            long before = PurseSum(r.St);
            long got = TownFolk.ZcCarry(r.St, from, to, want);
            long after = PurseSum(r.St);
            if (after != before) _gCtl += Math.Abs(after - before);
            sum += got;
            if (fee > 0) _gFee += Math.Min(got, (long)fee);
        }

        /// <summary>Kupno ziemi w miescie: kiesa P -> pan miasta, rok koszyka szlachty na glowe. Zwraca milionowe, ktore naprawde przeszly (ilu stac).</summary>
        private static int LandTown(Rec r, int q, double L)
        {
            if (!TownFolk.ZcPurses(r.St, _mPurse2)) { _mvNoPurse++; return 0; }
            if (r.St.IsUnderSiege) { _mvLandWar++; return 0; }
            var oc = r.St.OwnerClan; var lord = oc != null ? oc.Leader : null;
            if (lord == null || !lord.IsAlive) { _mvNoLord++; return 0; }
            long kp = _mPurse2[2];
            double head = LandDays * _usual[EGentry] / Math.Max(1, _sigma);
            double cost = q * L / PpmAll * head;
            if (cost > kp) { _mvLandShort++; q = (int)Math.Floor(q * kp / cost); if (q <= 0) return 0; cost = q * L / PpmAll * head; }
            long want = Math.Min(kp, Math.Max(1L, (long)Math.Ceiling(cost)));
            long before = _mPurse2[0] + _mPurse2[1] + _mPurse2[2];
            int g0 = lord.Gold;
            Hero got2;
            long got = TownFolk.ZcPayLand(r.St, want, out got2);
            if (got <= 0) { _mvNoLord++; return 0; }
            long after = PurseSum(r.St);
            long gave = ReferenceEquals(got2, lord) ? (long)lord.Gold - g0 : got;   // inny odbiorca (zmiana pana w przelewie - nie zdarza sie) - bez pomiaru
            if (before - after != got) _gCtl += Math.Abs(before - after - got);
            if (gave != got) _gCtl += Math.Abs(gave - got);
            _gLandT += got; _gLandTN++; _gPurseOut += before - after; _gLordIn += gave;
            if (got < want) q = (int)Math.Floor(q * (double)got / want);
            return q;
        }

        /// <summary>Kupno ziemi we wsi: kiesa wsi -> pan wsi (GiveGoldAction, najwyzej stan kiesy - gra obcina kiese do 0 i wyplaca calosc). Zwraca milionowe.</summary>
        private static int LandVillage(Rec r, int q, double L)
        {
            var st = r.St; var v = st.Village;
            if (v == null) return 0;
            if (v.VillageState != Village.VillageStates.Normal) { _mvLandWar++; return 0; }
            var oc = st.OwnerClan; var lord = oc != null ? oc.Leader : null;
            if (lord == null || !lord.IsAlive) { _mvNoLord++; return 0; }
            int gold = Math.Max(0, v.Gold);
            double head = LandDays * _usual[EGentry] / Math.Max(1, _sigma);
            double cost = q * L / PpmAll * head;
            if (cost > gold) { _mvLandShort++; q = (int)Math.Floor(q * gold / cost); if (q <= 0) return 0; cost = q * L / PpmAll * head; }
            int want = (int)Math.Min(gold, Math.Max(1.0, Math.Ceiling(cost)));
            if (want <= 0) return 0;
            int g0 = v.Gold, l0 = lord.Gold;
            GiveGoldAction.ApplyForSettlementToCharacter(st, lord, want, true);
            long took = g0 - v.Gold, gave = (long)lord.Gold - l0;
            if (took != gave) _gCtl += Math.Abs(took - gave);
            if (took <= 0) return 0;
            _gLandV += took; _gLandVN++; _gVilOut += took; _gLordIn += gave;
            if (took < want) q = (int)Math.Floor(q * (double)took / want);
            return q;
        }

        // ------------------------------------------------------------ linie
        /// <summary>OnSessionLaunched (po EstatesSessionStart): postfiks dobrobytu (raz na metode czynnego modelu) i linia startu ZC6.</summary>
        internal static void MobilitySessionStart()
        {
            try { WireProsperity(); } catch (Exception e) { Log.Error("DailyLife.WireProsperity", e); }
            try
            {
                var s = Settings.Current;
                Log.Info(PM + "start sesji | klucze: Daily Life Mobility " + (s != null && s.DailyLifeMobility ? "tak" : "WYLACZONE") + ", Daily Life Freedom "
                         + (s != null && s.DailyLifeFreedom ? "tak" : "WYLACZONE") + ", Daily Life Prosperity Link " + (s != null && s.DailyLifeProsperityLink ? "tak" : "WYLACZONE")
                         + " (Daily Life Estates " + (s != null && s.DailyLifeEstates ? "tak" : "WYLACZONE") + ", Daily Life Measure " + (KeyOn ? "tak" : "WYLACZONE") + ")"
                         + (EstatesOn ? "" : " - bez ksiegi stanow nic nie rusza")
                         + " | awans 3%/rok x (D-U)/U x zapas x miejsce: chlopi->rzemieslnicy wsi U 2.5 / 60 dni / <= 6%, biedota->rzemieslnicy U 2.5 / 30 dni / <= 1.5 x pasma"
                         + " (oplata cechowa 30 dni do R), rzemieslnicy->kupcy U 4 / 60 dni / <= 1.5 x pasma, kupcy i zamozni chlopi->szlachta U 10 / 180 dni / szlachta krainy <= 3%"
                         + " (kupno ziemi: 364 dni koszyka szlachty do pana) | spadek 6%/rok x (L-D)/L po 4 przegladach ponizej L i zapasie < 14 dni: rzemieslnicy 1.3, kupcy 2.5, szlachta 4"
                         + " | wyzwolenia 0.5%/rok; przez zdobywce 10% na 28 dob | granice 0.5-1.5 x pasma, 0.2% ludzi osady na tydzien | przeglad co 7 dob na osade (1/7 dziennie)"
                         + " | dobrobyt: " + ProsperityWhere + " | potkniecia " + _stumbles + ".");
            }
            catch (Exception e) { Stumble("MobilitySessionStart", e); }
        }

        /// <summary>Linia doby ZC6 (doba linii): przejscia od ostatniej linii, pieniadze, blokady, granice, mediany Dostatku stanow, dobrobyt. Zeruje liczniki linii.</summary>
        private static string MobilityLine(int day)
        {
            var sb = new StringBuilder(2400);
            double up = 0, down = 0;
            for (int t = 0; t < TN; t++) { if (IsUp(t)) up += _mvT[t]; else if (IsDown(t)) down += _mvT[t]; }
            double upS = 0, downS = 0;
            for (int t = 0; t < TN; t++) { if (IsUp(t)) upS += _mvTS[t]; else if (IsDown(t)) downS += _mvTS[t]; }
            double world = _nWorld;
            double yr = _mvDaysS > 0 && world > 0 ? (upS - downS) / world * 364.0 / _mvDaysS * 100.0 : double.NaN;
            sb.Append(PM).Append("dzien ").Append(day).Append(" | klucze: ruch ").Append(MobOn ? "tak" : "nie").Append(", wyzwolenie ").Append(FreeOn ? "tak" : "nie")
              .Append(", dobrobyt ").Append(ProsOn ? "tak" : "nie")
              .Append(" | od ostatniej linii ").Append(_mvDays).Append(" dob: przegladow osad ").Append(_mvSeen).Append(" (rozgrzewka - bez przegladu ").Append(_mvWarm).Append(')')
              .Append(" | ludzie (ksiega):");
            for (int t = 0; t < TN; t++) sb.Append(t == 0 ? " " : ", ").Append(TPl[t]).Append(' ').Append(N0(_mvT[t]));
            sb.Append(" | w gore ").Append(N0(up)).Append(", w dol ").Append(N0(down)).Append(", netto ").Append(N0(up - down))
              .Append(" | od wczytania (").Append(_mvDaysS).Append(" dob): w gore ").Append(N0(upS)).Append(", w dol ").Append(N0(downS))
              .Append(", wyzwoleni ").Append(N0(_mvTS[TMan] + _mvTS[TFree])).Append(", przybysze ").Append(N0(_mvTS[TMig]))
              .Append(", netto w gore ").Append(F3(yr)).Append("% swiata na rok")
              .Append(" | pieniadze (zl gry): z ludzmi W->R ").Append(_gW2R).Append(" (w tym oplaty cechowe ").Append(_gFee).Append("), R->P ").Append(_gR2P)
              .Append(", R->W ").Append(_gR2W).Append(", P->R ").Append(_gP2R)
              .Append("; ziemia do panow: miasta ").Append(_gLandT).Append(" (").Append(_gLandTN).Append(" kupna), wsie ").Append(_gLandV).Append(" (").Append(_gLandVN).Append(" kupna)")
              .Append(" | kontrola obiegu: kiesy ludu -").Append(_gPurseOut).Append(", kiesy wsi -").Append(_gVilOut).Append(", panowie +").Append(_gLordIn)
              .Append(", roznica ").Append(_gCtl)
              .Append(" | blokady: zapas nieznany (kiesa nieczynna) ").Append(_mvNoPurse).Append(", bez zywego pana ").Append(_mvNoLord).Append(", ziemia nie dla wszystkich (brak pieniedzy) ")
              .Append(_mvLandShort).Append(", oblezenie / najazd ").Append(_mvLandWar)
              .Append(" | limit 0.2%/tydz.: osad z cieciem ").Append(_mvCapHit).Append(", najwiekszy ruch ").Append(F3(100 * _mvMax)).Append("% (").Append(_mvMaxName.Replace('|', '/'))
              .Append(") | granice pasm: ciecia ").Append(_mvBand).Append(", osad poza pasmem ").Append(_mvOut).Append(" (po wyzwoleniu przez zdobywce ").Append(_mvOutFree)
              .Append("), osad pod wladca bez niewoli ").Append(_mvFreeSt);
            MedianDLine(sb);
            sb.Append(" | ").Append(ProsperityText());
            sb.Append(" | potkniecia przegladu ").Append(_mvStum).Append('.');
            ZeroMobLine();
            return sb.ToString();
        }

        private static readonly List<KeyValuePair<double, double>> _wmed = new List<KeyValuePair<double, double>>(900);

        /// <summary>Mediany Dostatku stanow wazone ludzmi (osady z wlasnym D) - progi projektu ZC6: chlopi 1.3-2.5, biedota 1.4-2.5, rzemieslnicy 2.2-4.5, kupcy 4-10, szlachta 6-15.</summary>
        private static void MedianDLine(StringBuilder sb)
        {
            sb.Append(" | mediany D stanow (wazone ludzmi, osady z wlasnym D):");
            for (int e = EPeasant; e <= EGentry; e++)
            {
                _wmed.Clear();
                foreach (var r in _recs)
                {
                    if (r.DSrc != 0 || !(r.N[e] > 0) || !Fin(r.DE[e])) continue;
                    _wmed.Add(new KeyValuePair<double, double>(r.DE[e], r.N[e]));
                }
                sb.Append(e == EPeasant ? " " : ", ").Append(EstateName[e]).Append(' ').Append(F2(WeightedMedian(_wmed)));
            }
        }

        // ------------------------------------------------------------ widok (People of this land): ruchy osady w ostatnich 28 dobach
        private static readonly string[] TEn =
        {
            "peasants took up a craft", "labourers joined a guild", "craftsmen became merchants", "merchants bought land and joined the gentry",
            "wealthy peasants bought land and joined the gentry", "craftsmen lost their trade", "merchants fell back to a craft", "gentry sold their land",
            "slaves were freed by their owners", "slaves were set free under the new rule", "newcomers from the villages joined the labourers"
        };

        /// <summary>Zdanie "Rose and fell in the last 28 days: ..." - ludzie wedlug przejsc z pamieci przegladow (sesja). null - brak ruchow albo pamieci.</summary>
        private static string MobilityText(Rec r)
        {
            try
            {
                if (r == null || r.Kind == KCastle || !SafeId(r.Id) || !(MobOn || FreeOn)) return null;
                EBook eb;
                if (!_eb.TryGetValue(r.Id, out eb) || eb.P == null) return null;
                var sum = new double[TN];
                int oldest = -1;
                if (eb.Mv != null)
                    for (int i = 0; i < MvSlots; i++)
                    {
                        if (eb.MvDay[i] < 0 || _viewDay - eb.MvDay[i] >= 28) continue;
                        for (int t = 0; t < TN; t++) sum[t] += eb.Mv[i * TN + t];
                        oldest = eb.MvDay[i];
                    }
                sum[TMig] += eb.MigAcc;
                var parts = new List<string>(6);
                for (int t = 0; t < TN; t++) if (sum[t] >= 0.5) parts.Add(Num(sum[t]) + " " + TEn[t]);
                if (parts.Count == 0)
                    return oldest >= 0 ? "No one rose or fell between the estates here in the last weeks." : null;
                int days = oldest >= 0 ? Math.Min(28, Math.Max(7, _viewDay - oldest + 7)) : 7;
                return "Rose and fell in the last " + days + " days: " + string.Join(", ", parts.ToArray()) + ".";
            }
            catch (Exception e) { ViewStumble("MobilityText", e); return null; }
        }
    }
}
