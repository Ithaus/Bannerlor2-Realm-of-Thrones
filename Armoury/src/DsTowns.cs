using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// DANINY SREDNIOWIECZNE (DS) - KROK F3: MIASTA I ZAMKI (docs/PROJEKT-DANINY-SREDNIOWIECZNE-2026-10-17.md rozdz. 6.1, 10.3, 11; decyzja Jeffa 17.10: "daniny, oplaty
    /// i logika musi byc taka jak byla w sredniowieczu ... system dynamiczny"). Klucz glowny DsTowns (DOMYSLNIE WYLACZONY - projekt 10.4: do testu). Korona (nadania,
    /// pomoc od sprzedazy, dziesiata od ruchomosci, kredyt) - DsSeason.cs; zaliczki mieszczan - TownFolk.CrownLoans.Ds.cs. "Stare sumy, nowe formy":
    ///  - OKNO POMIARU (DsFarmMeasureDays, 28 dob): stare zasady dzialaja co do monety; liczymy na miasto i zamek, co dzis dostaja pan (zawor po dekrecie + czesc pana
    ///    podatku od skarbca ludu tego miasta + W7 z ludu tego miasta) i korona (czesc korony zaworu). Doba DsFarmMeasureDays + 1: FIRMY USTALONE - M2 firma miejska
    ///    panu F_L = 364 x srednia doby, M3 firma krolewska F_C (to samo z czesci korony), M4 firmy zamku (pan / korona z zaworu zamku). Rata 1/52 co 7 dob z kasy
    ///    (dzien raty wedlug FNV id osady), najwyzej do DsFarmFloorShare zapasu kupcow; brak -> ksiega zaleglosci (splacana pierwsza w nastepnych ratach). Oblezenie
    ///    i zmiana krolestwa wlasciciela w ostatnich 28 dobach (zdobycie / lup) - rata darowana. Martwy pan - rata czeka w ksiedze (jak zawor: "czeka").
    ///    Zaleglosc przez DsFarmDistraintTerms dni kwartalu z rzedu - ZAJECIE: pan i korona biora zaleglosc z kiesy gminy miasta (najwyzej 2/3 jej stanu w dzien raty).
    ///    Zaleglosc ponad DsFarmForgiveWeeks rat - darowana (licznik). ODNOWA co DsFarmRenewDays: firma w strone (doplyw netto kupcow 28 dob x udzial z dnia ustalenia),
    ///    najwyzej +-DsFarmRenewMax (miasto bez udzialu - udzial swiata).
    ///  - Po ustaleniu NIE biegna: zawor miast (TownPurse.Valve) i zamkow (CastlePurse), danina z oszczednosci P1 (KingdomTreasury.WaRate), podatek od skarbca ludu miast
    ///    i zamkow (TownFolk.PayHoard), danina wojenna miast 1%/d (Levies), W7 z ludu (LvSources). Zostaja: czynsz gruntowy M1, akcyza M5, myto, clo (x2 w wojnie - Levies).
    ///  - M6 WYPLATA KUPCOM: co dobe 1/DsMerchantDrawDays kasy ponad 1.5 x zapas kupcow - w miescie P (DsMerchantDrawPShare) i R jako zarobek (placa podatek pana i 184),
    ///    w zamku do kiesy podzamcza (zamiast "5% zaworu dla ludu" i 15% zaworu zamku).
    ///  - M7 POMOC OD SPRZEDAZY: w Levies min(DsAidesMax, g x DsAidesRate) sprzedazy kasy z doby ludu (lud, markietani i zycie partii, dwor, sprzedaz okna, wsie);
    ///    brak -> ksiega (splacana w dzien raty PRZED firma). M9 dziesiata od ruchomosci kasy: kasa ponad 1.5 x zapas x g x DsMovablesRate, 13 rat co 7 dob.
    /// Zamknieta ekonomia: kazdy przelew mierzony u platnika i odbiorcy (kontrola linii "Firmy miast i zamkow (DS)": platnicy - odbiorcy = 0); zaleglosc to roszczenie,
    /// nie pieniadz. Zapis arm_daniny_miasta (SaveText): brak klucza (stary zapis) = start okna pomiaru przy wlaczonym kluczu. Wylaczenie klucza - stare zasady od nastepnej
    /// doby, zaleglosci zostaja w zapisie bez skutku, zaliczki mieszczan przechodza do zwyklego dlugu korona2. Kazde zdarzenie we wlasnym try, potkniecia liczone.
    /// </summary>
    internal static class DsTowns
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        internal const double OverReserve = 1.5;                 // M6 i M9 (kasa): ponad 1.5 x zapas kupcow
        internal const int Quarter = 91, Instalments = 13;       // dni kwartalu (91/182/273/364), raty nadania
        private const int Weeks = 52, NetDays = 28, CaptureDays = 28, YearDays = 364, NetMinDays = 7;

        private sealed class Farm
        {
            public Settlement St;                                  // sesja
            public double ML, MC;                                  // okno pomiaru: pan, korona
            public double FL, FC;                                  // firmy roczne (rata = F / 52)
            public int Fixed = -1;                                 // doba ustalenia albo ostatniej odnowy
            public double Share = double.NaN;                      // firma na dobe / doplyw netto na dobe przy ustaleniu
            public double AL, AC;                                  // zaleglosci firm: pan, korona
            public int ATerms;                                     // dni kwartalu z rzedu z zaleglosciami
            public double AidArr; public string AidK;              // pomoc od sprzedazy niezaplacona (M7) i krolestwo
            public double AidCarry;                                // ulamek pomocy ponizej 1 zl (sesja)
            public double MovLeft, MovPer; public string MovK; public int MovSerial = -1;   // dziesiata od ruchomosci kasy (M9)
            // sesja
            public readonly double[] Net = new double[NetDays]; public int NH, NF;
            public long G0 = long.MinValue, PaidWin;               // zamek: kasa przy poprzedniej probce, pobory od niej
            public string OwnerK; public int CapDay = -1;          // krolestwo wlasciciela widziane ostatnio, doba zmiany
            public double Sales; public int SalesDay = -1;         // podstawa M7 z doby ludu
        }

        // ------------------------------------------------------------ stan (zapis arm_daniny_miasta)
        private static int _dayOn = -1, _fixDay = -1, _mDays;
        private static double _worldShare = double.NaN;
        private static readonly Dictionary<string, Farm> _farm = new Dictionary<string, Farm>();

        // ------------------------------------------------------------ sesja
        /// <summary>DsSeason.Daily: dzis ustalono firmy - sesja przelaczenia (nadania wszystkich krolestw bez kroku 0.5).</summary>
        internal static bool SwitchDue;
        private static int _stumbles, _stumblesSess;
        private static readonly HashSet<string> _err = new HashSet<string>();
        private static readonly List<string> _events = new List<string>();

        // ------------------------------------------------------------ liczniki od ostatniej linii
        private static int _nDue, _nDueT, _remitSiege, _remitCap, _noLord, _distN, _forgN, _renewN, _renewUp, _renewDown, _aidRemit;
        private static double _dueL, _dueC, _remit, _noLordAmt, _arrNew, _arrPaid, _forgiven, _aidArrNew, _aidLost, _movAssess, _movRemit, _movShort, _movLost;
        private static long _paidL, _paidC, _distL, _distC, _drawT, _drawC, _aidPaid, _aidArrPaid, _movPaid;
        private static long _payOut, _payIn;

        private static void ClearCounters()
        {
            _nDue = _nDueT = _remitSiege = _remitCap = _noLord = _distN = _forgN = _renewN = _renewUp = _renewDown = _aidRemit = 0;
            _dueL = _dueC = _remit = _noLordAmt = _arrNew = _arrPaid = _forgiven = _aidArrNew = _aidLost = _movAssess = _movRemit = _movShort = _movLost = 0;
            _paidL = _paidC = _distL = _distC = _drawT = _drawC = _aidPaid = _aidArrPaid = _movPaid = 0;
            _payOut = _payIn = 0;
            _stumbles = 0;
        }

        /// <summary>Konstruktor ArmouryBehavior: nowa sesja (zapis wczytuje Import po konstruktorze).</summary>
        internal static void Reset()
        {
            _dayOn = -1; _fixDay = -1; _mDays = 0; _worldShare = double.NaN; _farm.Clear();
            SwitchDue = false; _stumblesSess = 0; _err.Clear(); _events.Clear();
            ClearCounters();
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++; _stumblesSess++;
            try { if (_err.Add(where)) Log.Error("DsTowns." + where, e); } catch { }
        }

        // ============================================================ przelaczniki
        internal static bool Key { get { var s = Settings.Current; return s != null && s.DsTowns; } }

        /// <summary>F3 czynne: klucz, kiesa ludu (TownFolk.Live), korona z biezacych wplywow (165) i zawor kas miast (A1 - kasy to prawdziwy pieniadz).</summary>
        internal static bool On { get { return Key && TownFolk.Live && CrownIncome.On && TownPurse.ValveLive; } }

        /// <summary>Okno pomiaru: stare zasady, liczymy zawor.</summary>
        internal static bool InWindow { get { return _dayOn >= 0 && _fixDay < 0 && On; } }

        /// <summary>Firmy ustalone: nowe zasady miast, zamkow i korony.</summary>
        internal static bool FarmsLive { get { return _fixDay >= 0 && On; } }

        private static int Today { get { return (int)CampaignTime.Now.ToDays; } }
        private static double Unit(double v) { return double.IsNaN(v) ? 0 : (v < 0 ? 0 : (v > 1 ? 1 : v)); }
        private static int MeasureDays(Settings s) { return Math.Max(7, Math.Min(91, s.DsFarmMeasureDays)); }
        private static int DrawDays(Settings s) { return Math.Max(0, Math.Min(120, s.DsMerchantDrawDays)); }
        private static int ForgiveWeeks(Settings s) { return Math.Max(0, Math.Min(156, s.DsFarmForgiveWeeks)); }

        /// <summary>Rampa zbytkow ludu (DsRampDays od doby ustalenia firm): 0..1; poza F3 - 0.</summary>
        internal static double FolkRamp
        {
            get
            {
                var s = Settings.Current;
                if (s == null || !FarmsLive) return 0;
                int days = Math.Max(0, Math.Min(364, s.DsRampDays));
                if (days <= 0) return 1;
                return Unit((Today - _fixDay) / (double)days);
            }
        }

        private static uint Fnv(string id)
        {
            uint h = 2166136261u;
            if (id != null) for (int i = 0; i < id.Length; i++) { h ^= id[i]; h *= 16777619u; }
            return h;
        }

        /// <summary>Dzien raty osady (co 7 dob, przesuniecie z FNV id - raty rozlozone na tydzien).</summary>
        internal static bool PayDay(Settlement st)
        {
            if (st == null || st.StringId == null) return false;
            return (Today + (int)(Fnv(st.StringId) % 7u)) % 7 == 0;
        }

        private static string OwnerKey(Settlement st)
        {
            var c = st != null ? st.OwnerClan : null;
            if (c == null) return "-";
            var k = c.Kingdom;
            return k != null && !k.IsEliminated && k.StringId != null ? k.StringId : "c:" + (c.StringId ?? "?");
        }

        private static Farm FarmOf(Settlement st, bool create)
        {
            if (st == null || st.StringId == null) return null;
            Farm f;
            if (_farm.TryGetValue(st.StringId, out f)) { if (f.St == null) f.St = st; return f; }
            if (!create || BadId(st.StringId)) return null;
            f = new Farm { St = st };
            _farm[st.StringId] = f;
            return f;
        }

        /// <summary>Zwolnienie z raty: 1 - oblezenie, 2 - zmiana krolestwa wlasciciela w ostatnich 28 dobach (zdobycie, lup); 0 - bez zwolnienia.</summary>
        internal static int RemitOf(Settlement st)
        {
            try
            {
                if (st == null) return 0;
                if (st.IsUnderSiege) return 1;
                Farm f = FarmOf(st, false);
                if (f != null && f.CapDay >= 0 && Today - f.CapDay < CaptureDays) return 2;
            }
            catch (Exception e) { Stumble("RemitOf", e); }
            return 0;
        }

        private static void Push(Farm f, double net)
        {
            if (double.IsNaN(net) || double.IsInfinity(net)) return;
            f.Net[f.NH] = net; f.NH = (f.NH + 1) % NetDays; if (f.NF < NetDays) f.NF++;
        }

        private static double NetAvg(Farm f, int min)
        {
            if (f == null || f.NF < min) return double.NaN;
            double t = 0; for (int i = 0; i < f.NF; i++) t += f.Net[i];
            return t / f.NF;
        }

        // ============================================================ poczatek doby (ArmouryBehavior: po TownPurse.BeforeRents, przed PopulationLaw.Daily)
        /// <summary>Stan F3: wylaczone - stare zasady (i porzadki); start okna pomiaru; ustalenie firm po DsFarmMeasureDays; dni kwartalu (zaleglosci), odnowy, zmiany wlascicieli.</summary>
        internal static void DayStart()
        {
            try
            {
                var s = Settings.Current;
                if (s == null || Campaign.Current == null) return;
                int today = Today;
                if (!On)
                {
                    if (_dayOn >= 0 || _fixDay >= 0)
                    {
                        string why = !Key ? "klucz Ds Towns" : !TownFolk.Live ? "kiesa ludu nieczynna" : !CrownIncome.On ? "165 Crown Current Income nieczynne" : "zawor kas miast nieczynny";
                        _events.Add("WYLACZONE (" + why + ") - stare zasady od tej doby (zawor miast i zamkow, P1, podatek od skarbca ludu, danina miast, W7 z ludu); firmy i zaleglosci "
                                    + "zostaja w zapisie bez skutku (ponowne wlaczenie = nowe okno pomiaru), dziesiata od ruchomosci niezebrana przepada, zaliczki mieszczan -> zwykly dlug korona2");
                        try { TownFolk.AntAllToNormal(); } catch (Exception e) { Stumble("DayStart(zaliczki)", e); }
                        try { DsSeason.Off(); } catch (Exception e) { Stumble("DayStart(nadania)", e); }
                        foreach (var f in _farm.Values) { f.ML = f.MC = 0; f.MovLeft = 0; f.MovPer = 0; f.MovK = null; f.MovSerial = -1; f.AidArr = 0; f.AidK = null; f.AidCarry = 0; }
                    }
                    _dayOn = -1; _fixDay = -1; _mDays = 0; SwitchDue = false;
                    return;
                }
                Owners(today);
                if (_dayOn < 0)
                {
                    _dayOn = today; _mDays = 0; _fixDay = -1;
                    foreach (var f in _farm.Values) { f.ML = f.MC = 0; }
                    _events.Add("start okna pomiaru firm - " + MeasureDays(s) + " dob starych zasad (zawor miast i zamkow, czesc pana podatku od skarbca ludu, W7 z ludu), potem firmy stale i nadania wojenne");
                }
                if (_fixDay < 0)
                {
                    if (_mDays >= MeasureDays(s)) { Fix(today, s); _fixDay = today; SwitchDue = true; }
                    else _mDays++;
                }
                else
                {
                    if (today % Quarter == 0) Terms();
                    Renew(today, s);
                }
            }
            catch (Exception e) { Stumble("DayStart", e); }
        }

        private static void Owners(int today)
        {
            foreach (var st in Settlement.All)
            {
                try
                {
                    if (st == null || !(st.IsTown || st.IsCastle) || st.Town == null) continue;
                    var f = FarmOf(st, true);
                    if (f == null) continue;
                    string o = OwnerKey(st);
                    if (f.OwnerK != null && f.OwnerK != o) f.CapDay = today;
                    f.OwnerK = o;
                }
                catch (Exception e) { Stumble("Owners", e); }
            }
        }

        private static void Fix(int today, Settings s)
        {
            int days = Math.Max(1, _mDays);
            double sF = 0, sN = 0, tL = 0, tC = 0, cL = 0, cC = 0;
            int nT = 0, nC = 0, zT = 0, zC = 0;
            foreach (var st in Settlement.All)
            {
                try
                {
                    if (st == null || !(st.IsTown || st.IsCastle) || st.Town == null) continue;
                    var f = FarmOf(st, true);
                    if (f == null) continue;
                    f.FL = YearDays * Math.Max(0, f.ML) / days; f.FC = YearDays * Math.Max(0, f.MC) / days;
                    f.ML = f.MC = 0; f.Fixed = today; f.ATerms = 0;
                    double net = NetAvg(f, NetMinDays), dayF = (f.FL + f.FC) / YearDays;
                    if (net > 0 && dayF > 0) { f.Share = dayF / net; sF += dayF; sN += net; } else f.Share = double.NaN;
                    if (st.IsTown) { nT++; tL += f.FL; tC += f.FC; if (dayF <= 0) zT++; }
                    else { nC++; cL += f.FL; cC += f.FC; if (dayF <= 0) zC++; }
                }
                catch (Exception e) { Stumble("Fix(osada)", e); }
            }
            _worldShare = sN > 0 ? sF / sN : double.NaN;
            _events.Add("FIRMY USTALONE po " + days + " dobach pomiaru: " + nT + " miast (panom " + N0(tL) + " zl/rok, koronie " + N0(tC) + " zl/rok; bez firmy " + zT + "), " + nC + " zamkow (panom "
                        + N0(cL) + ", koronie " + N0(cC) + "; bez firmy " + zC + "), rata 1/" + Weeks + " co 7 dob; udzial firmy w doplywie netto swiata "
                        + (double.IsNaN(_worldShare) ? "-" : _worldShare.ToString("0.000", Inv)) + "; od tej doby bez zaworu, P1, podatku od skarbca ludu, daniny miast i W7 z ludu; nadania wojenne - sesja dzis");
        }

        private static void Terms()
        {
            foreach (var f in _farm.Values)
            {
                double inst = (f.FL + f.FC) / Weeks;
                if (f.AL + f.AC >= Math.Max(1.0, inst)) f.ATerms++; else f.ATerms = 0;
            }
        }

        private static void Renew(int today, Settings s)
        {
            int rd = Math.Max(0, Math.Min(728, s.DsFarmRenewDays));
            if (rd <= 0) return;
            double m = Math.Max(0, Math.Min(0.5, (double)s.DsFarmRenewMax));
            foreach (var f in _farm.Values)
            {
                try
                {
                    if (f.Fixed < 0 || today - f.Fixed < rd || f.NF < NetDays) continue;   // pamiec 28 dob niepelna (po wczytaniu) - odnowa czeka
                    double F = f.FL + f.FC;
                    f.Fixed = today; _renewN++;
                    if (F <= 0) continue;
                    double share = !double.IsNaN(f.Share) ? f.Share : _worldShare;
                    if (double.IsNaN(share) || share <= 0) continue;
                    double target = Math.Max(0, NetAvg(f, NetDays)) * share * YearDays;
                    double nf = Math.Max(F * (1 - m), Math.Min(F * (1 + m), target));
                    double r = nf / F;
                    f.FL *= r; f.FC *= r;
                    if (r > 1 + 1e-9) _renewUp++; else if (r < 1 - 1e-9) _renewDown++;
                }
                catch (Exception e) { Stumble("Renew", e); }
            }
        }

        // ============================================================ okno pomiaru (stare zasady)
        /// <summary>PopulationLaw.ValveTowns (okno pomiaru, po zaworze miasta): czesc pana (po dekrecie) i korony z zaworu; probka doplywu netto.</summary>
        internal static void TownWindow(Settlement st, int lord, int crown)
        {
            try
            {
                if (!InWindow || st == null || st.Town == null) return;
                var f = FarmOf(st, true);
                if (f == null) return;
                SampleTown(st, st.Town, f, TownPurse.Reserve(st.Town));
                if (lord > 0) f.ML += lord;
                if (crown > 0) f.MC += crown;
            }
            catch (Exception e) { Stumble("TownWindow", e); }
        }

        /// <summary>CastlePurse.Daily (okno pomiaru, po zaworze zamku): czesc pana i korony; pobory do probki doplywu.</summary>
        internal static void CastleWindow(Settlement st, int lord, int crown, int suburb)
        {
            try
            {
                if (!InWindow || st == null) return;
                var f = FarmOf(st, true);
                if (f == null) return;
                if (lord > 0) f.ML += lord;
                if (crown > 0) f.MC += crown;
                f.PaidWin += Math.Max(0, lord) + Math.Max(0, crown) + Math.Max(0, suburb);
            }
            catch (Exception e) { Stumble("CastleWindow", e); }
        }

        /// <summary>TownFolk.PayHoard (okno pomiaru): czesc pana podatku od skarbca ludu tej osady.</summary>
        internal static void MeasureHoard(Settlement st, long lord)
        {
            try { if (lord > 0 && InWindow) { var f = FarmOf(st, true); if (f != null) f.ML += lord; } } catch (Exception e) { Stumble("MeasureHoard", e); }
        }

        /// <summary>KingdomTreasury.LvCollect (okno pomiaru): W7 z ludu tej osady (do pana).</summary>
        internal static void MeasureW7(Settlement st, long took)
        {
            try { if (took > 0 && InWindow && st != null && (st.IsTown || st.IsCastle)) { var f = FarmOf(st, true); if (f != null) f.ML += took; } } catch (Exception e) { Stumble("MeasureW7", e); }
        }

        private static void SampleTown(Settlement st, Town t, Farm f, int reserve)
        {
            long net;
            if (TownPurse.NetOf(t, out net)) Push(f, net);
            DsSeason.NoteMovBase(st, Math.Max(0, t.Gold - OverReserve * reserve));
        }

        /// <summary>CastlePurse.Daily (F3 czynne - okno i firmy), przed zaworem / firma zamku: probka doplywu netto (kasa teraz - kasa przy poprzedniej probce + nasze pobory od niej).</summary>
        internal static void CastleSample(Settlement st, Town t, int reserve)
        {
            try
            {
                if (!On || st == null || t == null) return;
                var f = FarmOf(st, true);
                if (f == null) return;
                long g = t.Gold;
                if (f.G0 != long.MinValue) Push(f, g - f.G0 + f.PaidWin);
                f.G0 = g; f.PaidWin = 0;
                DsSeason.NoteMovBase(st, Math.Max(0, g - OverReserve * reserve));
            }
            catch (Exception e) { Stumble("CastleSample", e); }
        }

        // ============================================================ przelewy (mierzone u platnika i odbiorcy)
        private static long ToLord(Settlement st, Town t, Hero lord, long x)
        {
            if (x <= 0 || lord == null || t == null) return 0;
            int had = t.Gold;
            int ix = (int)Math.Min(int.MaxValue, Math.Min(x, Math.Max(0, had)));
            if (ix <= 0) return 0;
            int h0 = lord.Gold;
            GiveGoldAction.ApplyForSettlementToCharacter(st, lord, ix, true);   // kasa -> pan (ta sama akcja co zawor i renty; kwota nigdy ponad kase)
            long o = Math.Max(0, (long)had - t.Gold), i = (long)lord.Gold - h0;
            _payOut += o; _payIn += i;
            return o;
        }

        private static long ToCrown(Town t, Kingdom k, long x)
        {
            if (x <= 0 || t == null || k == null) return 0;
            int had = t.Gold;
            int ix = (int)Math.Min(int.MaxValue, Math.Min(x, Math.Max(0, had)));
            if (ix <= 0) return 0;
            t.ChangeGold(-ix);
            long o = Math.Max(0, (long)had - t.Gold);
            if (o <= 0) return 0;
            int w0 = k.KingdomBudgetWallet;
            try { k.KingdomBudgetWallet += (int)o; }
            catch (Exception e) { t.ChangeGold((int)o); Stumble("ToCrown(skarbiec)", e); return 0; }   // nic w nicosc: zloto wraca do kasy
            _payOut += o; _payIn += (long)k.KingdomBudgetWallet - w0;
            return o;
        }

        // ============================================================ firmy: miasto (PopulationLaw.ValveTowns) i zamek (CastlePurse.Daily)
        /// <summary>
        /// PopulationLaw.ValveTowns przy ustalonych firmach (zamiast TownPurse.Valve): probka doplywu, w dzien raty zaleglosc pomocy M7, firma M2/M3 (pan / korona),
        /// dziesiata od ruchomosci kasy M9, zajecie i darowanie zaleglosci; co dobe wyplata kupcom M6. lordPay - kasa -> pan (wolajacy: RentToday, DayTownRent),
        /// crownFarm - kasa -> skarbiec w TownPurse.CrownToday (wplyw dnia), crownMov - kasa -> skarbiec dziesiata (wplyw dnia przez DsSeason). Nie rzuca.
        /// </summary>
        internal static bool TownDay(Settlement st, out int lordPay, out int crownFarm, out int crownMov, out long spare)
        {
            lordPay = crownFarm = crownMov = 0; spare = 0;
            try
            {
                var t = st != null ? st.Town : null;
                var s = Settings.Current;
                if (t == null || s == null) return false;
                var f = FarmOf(st, true);
                if (f == null) return false;
                int reserve = TownPurse.Reserve(t);
                SampleTown(st, t, f, reserve);
                spare = (long)t.Gold - reserve;
                Kingdom k;
                Pay(st, t, f, reserve, s, true, out lordPay, out crownFarm, out crownMov, out k);
                if (lordPay > 0 || crownFarm > 0) TownPurse.DsNoteFarm(st, lordPay, crownFarm, k);
                if (crownMov > 0) TownPurse.NoteTake(st, crownMov);
                Draw(st, t, f, reserve, s, true);
                return true;
            }
            catch (Exception e) { Stumble("TownDay", e); return lordPay + crownFarm + crownMov > 0; }
        }

        /// <summary>CastlePurse.Daily przy ustalonych firmach (zamiast zaworu zamku): to samo co miasto (bez zajecia - zamek nie ma gminy); suburb - M6 do kiesy podzamcza.
        /// Wolajacy: RentToday / LordDuesToday (pan), CastlePurse.CrownToday (crownFarm), ksiega MCastleCrown (crownFarm + crownMov) i MFolkValve (suburb).</summary>
        internal static void CastleDay(Settlement st, int reserve, out int lordPay, out int crownFarm, out int crownMov, out int suburb, out Kingdom k)
        {
            lordPay = crownFarm = crownMov = suburb = 0; k = null;
            try
            {
                var t = st != null ? st.Town : null;
                var s = Settings.Current;
                if (t == null || s == null) return;
                var f = FarmOf(st, true);
                if (f == null) return;
                Pay(st, t, f, reserve, s, false, out lordPay, out crownFarm, out crownMov, out k);
                f.PaidWin += lordPay + crownFarm + crownMov;
                suburb = (int)Math.Min(int.MaxValue, Draw(st, t, f, reserve, s, false));
            }
            catch (Exception e) { Stumble("CastleDay", e); }
        }

        private static void Pay(Settlement st, Town t, Farm f, int reserve, Settings s, bool town, out int lordPay, out int crownFarm, out int crownMov, out Kingdom kOut)
        {
            lordPay = crownFarm = crownMov = 0; kOut = null;
            var clan = st.OwnerClan;
            var lord = clan != null ? clan.Leader : null;
            var k = clan != null ? clan.Kingdom : null;
            if (k != null && k.IsEliminated) k = null;
            kOut = k;
            if (!PayDay(st)) return;
            double iL = f.FL / Weeks, iC = f.FC / Weeks;
            if (k == null) { iL += iC; iC = 0; if (f.AC > 0) { f.AL += f.AC; f.AC = 0; } }   // rod bez krolestwa - calosc panu (jak zawor)
            int serial;
            double g = DsSeason.GrantNow(k, out serial);
            bool assess = k != null && g > 0 && serial > 0 && f.MovSerial != serial;
            if (iL + iC <= 0 && f.AL + f.AC < 1 && f.AidArr < 1 && f.MovLeft < 1 && !assess) return;   // osada bez firmy i bez naleznosci
            _nDue++; if (town) _nDueT++;
            _dueL += iL; _dueC += iC;
            int why = RemitOf(st);
            if (why != 0)
            {
                _remit += iL + iC; if (why == 1) _remitSiege++; else _remitCap++;
                if (f.MovLeft >= 1) { double m = Math.Min(f.MovPer, f.MovLeft); f.MovLeft -= m; _movRemit += m; }
                return;
            }
            double before = f.AL + f.AC;
            if (lord == null || !lord.IsAlive)
            {
                f.AL += iL; f.AC += iC; _noLord++; _noLordAmt += iL + iC;   // bez zywego pana - rata czeka (jak zawor)
                _arrNew += iL + iC;
                return;
            }
            double floor = Unit(s.DsFarmFloorShare) * reserve;
            long avail = Math.Max(0L, (long)Math.Floor(t.Gold - floor));
            // 1. zaleglosc pomocy od sprzedazy (M7) - przed firma
            if (f.AidArr >= 1)
            {
                if (k == null || f.AidK != k.StringId) { _aidLost += f.AidArr; f.AidArr = 0; f.AidK = null; }
                else if (avail > 0)
                {
                    long got = ToCrown(t, k, (long)Math.Floor(Math.Min(f.AidArr, avail)));
                    if (got > 0) { f.AidArr -= got; avail -= got; crownFarm += (int)got; _aidArrPaid += got; DsSeason.NoteAides(k, got); }
                    if (f.AidArr < 1) { f.AidArr = 0; f.AidK = null; }
                }
            }
            // 2. firma: zaleglosc i rata (pan i korona w stosunku naleznosci)
            double wantL = f.AL + iL, wantC = f.AC + iC, tot = wantL + wantC;
            if (tot >= 1)
            {
                long pay = (long)Math.Floor(Math.Min(tot, avail));
                long pL = pay > 0 ? (long)Math.Floor(pay * wantL / tot) : 0, pC = pay - pL;
                long gL = pL > 0 ? ToLord(st, t, lord, pL) : 0;
                long gC = pC > 0 && k != null ? ToCrown(t, k, pC) : 0;
                f.AL = Math.Max(0, wantL - gL); f.AC = Math.Max(0, wantC - gC);
                avail -= gL + gC;
                lordPay += (int)gL; crownFarm += (int)gC;
                _paidL += gL; _paidC += gC;
            }
            else { f.AL = wantL; f.AC = wantC; }
            // 3. dziesiata od ruchomosci kasy (M9): wymiar w pierwszy dzien raty po nadaniu wojennym, potem 1/13 co 7 dob
            if (assess)
            {
                double baseM = Math.Max(0, t.Gold - OverReserve * reserve);
                if (f.MovK != null && f.MovK != k.StringId && f.MovLeft > 0) { _movLost += f.MovLeft; f.MovLeft = 0; }
                double a = g * Math.Max(0, Math.Min(0.3, (double)s.DsMovablesRate)) * baseM;
                f.MovLeft += a; f.MovPer = f.MovLeft / Instalments; f.MovK = k.StringId; f.MovSerial = serial; _movAssess += a;
            }
            if (f.MovLeft >= 1)
            {
                if (k == null || f.MovK != k.StringId) { _movLost += f.MovLeft; f.MovLeft = 0; f.MovPer = 0; f.MovK = null; }
                else
                {
                    double inst = Math.Min(f.MovPer, f.MovLeft);
                    long got = ToCrown(t, k, (long)Math.Floor(Math.Min(inst, avail)));
                    if (got > 0) { f.MovLeft -= got; avail -= got; crownMov += (int)got; _movPaid += got; DsSeason.NoteMovables(k, got); }
                    if (got + 0.5 < inst) _movShort += inst - got;
                    if (f.MovLeft < 1) { f.MovLeft = 0; f.MovPer = 0; }
                }
            }
            // 4. zajecie (miasto): zaleglosc przez DsFarmDistraintTerms dni kwartalu - z kiesy gminy (najwyzej 2/3 jej stanu)
            if (town && f.ATerms >= Math.Max(1, Math.Min(8, s.DsFarmDistraintTerms)) && f.AL + f.AC >= 1) Distrain(st, f, lord, k);
            double after = f.AL + f.AC;
            if (after > before) _arrNew += after - before; else _arrPaid += before - after;
            // 5. darowanie zaleglosci ponad DsFarmForgiveWeeks rat (licznik osobno - nie "splacone")
            int fw = ForgiveWeeks(s);
            double instAll = (f.FL + f.FC) / Weeks;
            if (fw > 0 && instAll > 0)
            {
                double cap = fw * instAll, arr = f.AL + f.AC;
                if (arr > cap) { double r = cap / arr; _forgiven += arr - cap; _forgN++; f.AL *= r; f.AC *= r; }
            }
        }

        /// <summary>Zajecie: zaleglosc firmy z kiesy gminy miasta (najwyzej 2/3 jej stanu) - kiesa gminy -> pan (RentToday, D "ziemia") i skarbiec (wplyw dnia przez
        /// TownPurse.CrownToday). Wyjatek przy przelewie oddaje zloto gminie.</summary>
        private static void Distrain(Settlement st, Farm f, Hero lord, Kingdom k)
        {
            try
            {
                double arr = f.AL + f.AC;
                if (arr < 1) return;
                if (k == null) { f.AL += f.AC; f.AC = 0; }
                long took = TownFolk.DistrainCommune(st, (long)Math.Ceiling(f.AL + f.AC));
                if (took <= 0) return;
                long dL = k == null ? took : (long)Math.Floor(took * f.AL / (f.AL + f.AC)), dC = took - dL;
                long inL = 0, inC = 0;
                if (dL > 0)
                {
                    int h0 = lord.Gold;
                    try { lord.ChangeHeroGold((int)dL); }
                    catch (Exception e) { TownFolk.DistrainUndo(st, took); Stumble("Distrain(pan)", e); return; }
                    inL = (long)lord.Gold - h0;
                    var clan = st.OwnerClan;
                    if (clan != null) { int r0; PopulationLaw.RentToday.TryGetValue(clan, out r0); PopulationLaw.RentToday[clan] = (int)Math.Min(int.MaxValue, (long)r0 + dL); }
                    CirculationWindows.NoteHeroGold(lord, (int)dL);
                    f.AL = Math.Max(0, f.AL - dL); _distL += dL;
                }
                if (dC > 0)
                {
                    int w0 = k.KingdomBudgetWallet;
                    try { k.KingdomBudgetWallet += (int)dC; inC = (long)k.KingdomBudgetWallet - w0; }
                    catch (Exception e) { TownFolk.DistrainUndo(st, dC); Stumble("Distrain(korona)", e); dC = 0; }
                    if (dC > 0) { TownPurse.DsCrownFolk(k, dC); f.AC = Math.Max(0, f.AC - dC); _distC += dC; }
                }
                _payOut += dL + dC; _payIn += inL + inC; _distN++;
            }
            catch (Exception e) { Stumble("Distrain", e); }
        }

        /// <summary>M6: 1/DsMerchantDrawDays kasy ponad 1.5 x zapas kupcow - miasto do P i R (zarobek), zamek do kiesy podzamcza. Zwraca wyplacone.</summary>
        private static long Draw(Settlement st, Town t, Farm f, int reserve, Settings s, bool town)
        {
            int days = DrawDays(s);
            if (days <= 0) return 0;
            double over = t.Gold - OverReserve * reserve;
            if (over < days) return 0;
            long x = (long)Math.Floor(over / days);
            if (town)
            {
                long got = TownFolk.EarnDraw(st, x, Unit(s.DsMerchantDrawPShare));
                if (got > 0)
                {
                    _drawT += got; _payOut += got; _payIn += got;
                    MoneyLedger.SplitTownMark(MoneyLedger.MRent, MoneyLedger.MFolkValve, got);   // ksiega: kasa -> kiesy ludu (jak czesc ludu zaworu, M19)
                    TownPurse.NoteTake(st, (int)Math.Min(int.MaxValue, got));                     // wyplata z zysku kupcow - doplyw netto bez zmiany definicji
                }
                return got;
            }
            int gc = TownFolk.EarnCastleValve(st, (int)Math.Min(int.MaxValue, x));
            if (gc > 0) { _drawC += gc; _payOut += gc; _payIn += gc; f.PaidWin += gc; }
            return Math.Max(0, gc);
        }

        // ============================================================ M7 pomoc od sprzedazy (Levies)
        /// <summary>TownLive / CastleLiveDay (krok 2 doby ludu): sprzedaz kasy w oknie (zycie i zolnierze, dwor, sprzedaz, zaplata ludu, wsie) - podstawa M7 i prognozy nadania.</summary>
        internal static void NoteSales(Settlement st, double sales)
        {
            try
            {
                if (!On || st == null || !(sales > 0) || double.IsInfinity(sales)) return;
                var f = FarmOf(st, true);
                if (f == null) return;
                f.Sales = sales; f.SalesDay = Today;
                DsSeason.NoteSales(st, sales);
            }
            catch (Exception e) { Stumble("NoteSales", e); }
        }

        /// <summary>KingdomTreasury.Levies (F3 czynne, po petli osad): pomoc od sprzedazy min(DsAidesMax, g x DsAidesRate) z kasy (ponad DsFarmFloorShare zapasu) do skarbca
        /// - wplyw dnia (LeviesToday) i garnek W5; brak - do ksiegi (dzien raty, przed firma). Oblezone - darowane.</summary>
        internal static void AidesDay(Dictionary<Kingdom, long> levies)
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !FarmsLive || levies == null) return;
                int today = Today;
                double rate1 = Math.Max(0, Math.Min(0.1, (double)s.DsAidesRate)), max = Math.Max(0, Math.Min(0.3, (double)s.DsAidesMax)), fl = Unit(s.DsFarmFloorShare);
                foreach (var f in _farm.Values)
                {
                    try
                    {
                        if (f.SalesDay != today || !(f.Sales > 0)) continue;
                        double sales = f.Sales; f.Sales = 0;
                        var st = f.St;
                        var t = st != null ? st.Town : null;
                        var clan = st != null ? st.OwnerClan : null;
                        var k = clan != null ? clan.Kingdom : null;
                        if (t == null || k == null || k.IsEliminated) continue;
                        double g = DsSeason.GrantOf(k);
                        if (g <= 0) continue;
                        if (st.IsUnderSiege) { _aidRemit++; continue; }
                        double due = sales * Math.Min(max, g * rate1) + f.AidCarry;
                        long want = (long)Math.Floor(due);
                        f.AidCarry = due - want;
                        if (want <= 0) continue;
                        int reserve = st.IsTown ? TownPurse.Reserve(t) : CastlePurse.Reserve(t);
                        long avail = Math.Max(0L, (long)Math.Floor(t.Gold - fl * reserve));
                        long got = ToCrown(t, k, Math.Min(want, avail));
                        if (got > 0)
                        {
                            long lk; levies.TryGetValue(k, out lk); levies[k] = lk + got;   // wplyw dnia 165
                            DsSeason.NoteAides(k, got);
                            _aidPaid += got;
                            if (st.IsTown) { TownPurse.NoteTake(st, (int)got); try { TownFolk.NoteTake(st, (int)got, TownFolk.TLevy); } catch { } }
                            else f.PaidWin += got;
                        }
                        if (got < want)
                        {
                            if (f.AidK != null && f.AidK != k.StringId) { _aidLost += f.AidArr; f.AidArr = 0; }
                            f.AidArr += want - got; f.AidK = k.StringId; _aidArrNew += want - got;
                        }
                    }
                    catch (Exception e) { Stumble("AidesDay(osada)", e); }
                }
            }
            catch (Exception e) { Stumble("AidesDay", e); }
        }

        /// <summary>L1 (limit zaliczki): niezebrana dziesiata od ruchomosci kas osad krolestwa.</summary>
        internal static double MovLeftOf(string kid)
        {
            double t = 0;
            if (kid == null) return 0;
            foreach (var f in _farm.Values) if (f.MovK == kid && f.MovLeft > 0) t += f.MovLeft;
            return t;
        }

        // ============================================================ linia doby
        private static string N0(double v) { return double.IsNaN(v) || double.IsInfinity(v) ? "0" : Math.Round(v).ToString("0", Inv); }

        /// <summary>ArmouryBehavior (koniec bloku doby): linia "Firmy miast i zamkow (DS)" (okno od poprzedniej linii) i "Pozyczki zaliczkowe (DS)"; liczniki od zera.</summary>
        internal static void Daily()
        {
            try
            {
                var s = Settings.Current;
                if (s == null || Campaign.Current == null) return;
                int day = Today - 1;
                foreach (var ev in _events) Log.Info("Firmy miast i zamkow (DS): dzien " + day + " | " + ev + ".");
                _events.Clear();
                try { TownFolk.AntLine(day); } catch (Exception e) { Stumble("Daily(zaliczki)", e); }
                if (!On) return;
                var sb = new StringBuilder(1800);
                sb.Append("Firmy miast i zamkow (DS): dzien ").Append(day);
                if (_fixDay < 0)
                {
                    double mL = 0, mC = 0; foreach (var f in _farm.Values) { mL += f.ML; mC += f.MC; }
                    sb.Append(" | POMIAR dzien ").Append(_mDays).Append('/').Append(MeasureDays(s)).Append(" (od doby ").Append(_dayOn).Append(") - stare zasady; zmierzone: panom ")
                      .Append(N0(mL)).Append(" (zawor po dekrecie, czesc pana podatku od skarbca ludu, W7 z ludu), koronie ").Append(N0(mC))
                      .Append(" (zawor) - srednio ").Append(N0(_mDays > 0 ? (mL + mC) / _mDays : 0)).Append(" zl/d");
                }
                else
                {
                    double fT = 0, fC = 0, aL = 0, aC = 0, aid = 0, mov = 0; int arrN = 0, distReady = 0, nT = 0, nC = 0;
                    foreach (var f in _farm.Values)
                    {
                        bool isT = f.St != null && f.St.IsTown;
                        if (isT) { fT += f.FL + f.FC; nT++; } else { fC += f.FL + f.FC; nC++; }
                        aL += f.AL; aC += f.AC; aid += f.AidArr; mov += f.MovLeft;
                        if (f.AL + f.AC >= 1) arrN++;
                        if (f.ATerms >= Math.Max(1, s.DsFarmDistraintTerms) && f.AL + f.AC >= 1) distReady++;
                    }
                    sb.Append(" | firmy od doby ").Append(_fixDay).Append(": miasta ").Append(nT).Append(" - ").Append(N0(fT)).Append(" zl/rok, zamki ").Append(nC).Append(" - ").Append(N0(fC))
                      .Append(" zl/rok (rata 1/").Append(Weeks).Append(" co 7 dob)")
                      .Append(" | raty dzis: osad ").Append(_nDue).Append(" (miast ").Append(_nDueT).Append("), nalezne panom ").Append(N0(_dueL)).Append(", koronie ").Append(N0(_dueC))
                      .Append("; zaplacone z kas panom ").Append(_paidL).Append(", koronie ").Append(_paidC)
                      .Append("; darowane ").Append(N0(_remit)).Append(" (oblezenie ").Append(_remitSiege).Append(", zdobycie / lup ").Append(_remitCap).Append(")")
                      .Append(", bez zywego pana (czeka) ").Append(N0(_noLordAmt)).Append(" (").Append(_noLord).Append(")")
                      .Append(" | zaleglosci: nowe ").Append(N0(_arrNew)).Append(", splacone ").Append(N0(_arrPaid)).Append(", razem panom ").Append(N0(aL)).Append(" + koronie ").Append(N0(aC))
                      .Append(" u ").Append(arrN).Append(" osad (do zajecia ").Append(distReady).Append("), zajete z kies gmin ").Append(_distL + _distC).Append(" (panom ").Append(_distL).Append(", koronie ").Append(_distC)
                      .Append("; ").Append(_distN).Append("), darowane ponad ").Append(ForgiveWeeks(s)).Append(" rat ").Append(N0(_forgiven)).Append(" (").Append(_forgN).Append(")")
                      .Append(" | odnowy ").Append(_renewN).Append(" (w gore ").Append(_renewUp).Append(", w dol ").Append(_renewDown).Append(")")
                      .Append(" | wyplata kupcom M6: miasta ").Append(_drawT).Append(" (P / R), zamki ").Append(_drawC).Append(" (podzamcza)")
                      .Append(" | pomoc od sprzedazy M7: do skarbcow ").Append(_aidPaid).Append(", nowa zaleglosc ").Append(N0(_aidArrNew)).Append(", zaleglosc splacona ").Append(_aidArrPaid)
                      .Append(", przepadla (zmiana krolestwa) ").Append(N0(_aidLost)).Append(", oblezone bez poboru ").Append(_aidRemit).Append(", zaleglosci razem ").Append(N0(aid))
                      .Append(" | dziesiata od ruchomosci kas M9: wymiar ").Append(N0(_movAssess)).Append(", zaplacone ").Append(_movPaid).Append(", darowane ").Append(N0(_movRemit))
                      .Append(", niezaplacone (na nastepne raty) ").Append(N0(_movShort)).Append(", przepadle (zmiana krolestwa) ").Append(N0(_movLost)).Append(", niezebrane ").Append(N0(mov))
                      .Append(" | rampa zbytkow ludu ").Append(FolkRamp.ToString("0.000", Inv));
                }
                sb.Append(" | kontrola: platnicy - odbiorcy = ").Append(_payOut - _payIn);
                if (_stumbles > 0) sb.Append(" | potkniecia ").Append(_stumbles).Append(" (w sesji ").Append(_stumblesSess).Append(")");
                sb.Append('.');
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Stumble("Daily", e); }
            finally { ClearCounters(); }
        }

        // ============================================================ zapis arm_daniny_miasta
        private static bool BadId(string id) { return string.IsNullOrEmpty(id) || id.IndexOf('|') >= 0 || id.IndexOf('~') >= 0; }
        private static string R(double v) { return double.IsNaN(v) || double.IsInfinity(v) ? "0" : Math.Round(v, 3).ToString("R", Inv); }

        internal static string Export()
        {
            try
            {
                var sb = new StringBuilder(256 + _farm.Count * 80);
                sb.Append("1|w~").Append(_dayOn.ToString(Inv)).Append('~').Append(_fixDay.ToString(Inv)).Append('~').Append(_mDays.ToString(Inv)).Append('~')
                  .Append(double.IsNaN(_worldShare) ? "-" : _worldShare.ToString("R", Inv));
                foreach (var kv in _farm)
                {
                    var f = kv.Value;
                    if (BadId(kv.Key)) continue;
                    bool any = f.FL > 0 || f.FC > 0 || f.ML > 0 || f.MC > 0 || f.AL > 0 || f.AC > 0 || f.AidArr > 0 || f.MovLeft > 0 || f.Fixed >= 0;
                    if (!any) continue;
                    sb.Append("|f~").Append(kv.Key).Append('~').Append(R(f.FL)).Append('~').Append(R(f.FC)).Append('~').Append(f.Fixed.ToString(Inv)).Append('~')
                      .Append(double.IsNaN(f.Share) ? "-" : f.Share.ToString("R", Inv)).Append('~').Append(R(f.AL)).Append('~').Append(R(f.AC)).Append('~').Append(f.ATerms.ToString(Inv))
                      .Append('~').Append(R(f.AidArr)).Append('~').Append(BadId(f.AidK) ? "-" : f.AidK).Append('~').Append(R(f.MovLeft)).Append('~').Append(R(f.MovPer))
                      .Append('~').Append(BadId(f.MovK) ? "-" : f.MovK).Append('~').Append(f.MovSerial.ToString(Inv)).Append('~').Append(R(f.ML)).Append('~').Append(R(f.MC));
                }
                DsSeason.Export(sb);
                return sb.ToString();
            }
            catch (Exception e) { Stumble("Export", e); return ""; }
        }

        private static double D(string x) { double v; return x != null && x != "-" && double.TryParse(x, NumberStyles.Float, Inv, out v) && !double.IsInfinity(v) ? v : double.NaN; }
        private static double D0(string x) { double v = D(x); return double.IsNaN(v) || v < 0 ? 0 : v; }
        private static int I(string x, int def) { int v; return x != null && int.TryParse(x, NumberStyles.Integer, Inv, out v) ? v : def; }

        /// <summary>Wczytanie (po Reset): brak klucza (stary zapis) albo zly format - zera (przy wlaczonym kluczu okno pomiaru od pierwszej doby).</summary>
        internal static void Import(string data)
        {
            try
            {
                if (string.IsNullOrEmpty(data)) return;
                var parts = data.Split('|');
                if (parts.Length == 0 || parts[0] != "1") return;
                for (int i = 1; i < parts.Length; i++)
                {
                    try
                    {
                        var a = parts[i].Split('~');
                        if (a.Length < 2) continue;
                        if (a[0] == "w" && a.Length >= 5)
                        {
                            _dayOn = I(a[1], -1); _fixDay = I(a[2], -1); _mDays = Math.Max(0, I(a[3], 0)); _worldShare = D(a[4]);
                            if (_dayOn < 0) { _dayOn = -1; _fixDay = -1; _mDays = 0; }
                        }
                        else if (a[0] == "f" && a.Length >= 17 && !BadId(a[1]))
                        {
                            var f = new Farm
                            {
                                FL = D0(a[2]), FC = D0(a[3]), Fixed = I(a[4], -1), Share = D(a[5]), AL = D0(a[6]), AC = D0(a[7]), ATerms = Math.Max(0, I(a[8], 0)),
                                AidArr = D0(a[9]), AidK = a[10] == "-" ? null : a[10], MovLeft = D0(a[11]), MovPer = D0(a[12]), MovK = a[13] == "-" ? null : a[13],
                                MovSerial = I(a[14], -1), ML = D0(a[15]), MC = D0(a[16])
                            };
                            if (f.AidArr <= 0) f.AidK = null;
                            if (f.MovLeft <= 0) { f.MovLeft = 0; f.MovPer = 0; }
                            _farm[a[1]] = f;
                        }
                        else DsSeason.ImportPart(a);
                    }
                    catch (Exception e) { Stumble("Import(wpis)", e); }
                }
            }
            catch (Exception e) { Stumble("Import", e); }
        }
    }
}
