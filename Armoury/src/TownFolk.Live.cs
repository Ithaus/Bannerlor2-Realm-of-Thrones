using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Extensions;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// PACZKA E5/B1 (173, PLAN 5.5) - KIESA LUDU CZYNNA (projekt etapu 5 v2 rozdz. 3 "E5/B1", R1, R5, R8, R9, R11-R14, D1, D4; plan kodu
    /// docs/etap5/PLAN-KODU-E5B1.md; model KL docs/PROJEKT-KIESA-LUDU-2026-10-09.md L1-L9, tabele 1.3-1.5). Model = model E5/0 "na sucho" (TownFolk.cs)
    /// z prawdziwymi przelewami: kiesy miast W/R/P i gmina, kiesa podzamcza (zamek) - nowi posiadacze zlota w pamieci moda (zapis arm_townfolk,
    /// ksiega pieniadza H22-H24); wies - jej Village.Gold jak dotad.
    ///  - Jedyne wejscie do ludu: Earn (arytmetyka, nie rzuca; wolajacy przelewa platnikowi reszte): 25% wydatkow zolnierzy, karawan, band i zalog,
    ///    20% dworu (A2 CourtFolkShare), place budow, kopalnie, place warsztatow; w bloku dobowym z kasy osady: robocizna handlu, obrot patrycjatu,
    ///    doplata do 35% wartosci wyrobow, 5% zaworu miasta / 15% zaworu zamku.
    ///  - Jedyne wyjscie na towar: PayForGoods (z CastlePurse.ConsumePostfix First -> TownPurse.OnConsumed / galaz zamku) zamiast cofniecia "zakupow" A1:
    ///    klasy placa swoja czesc z kiesy, kasa dostaje 98%, gmina akcyze 2%; czego lud nie zaplaci - cofniete jak w A1 ("zjedzone bez zaplaty").
    ///    Zamek: podzamcze placi, reszte pokrywa kredyt dworu (80% wplat dworu do siedziby-zamku) bez ruchu zlota (zloto dworu jest juz w kasie).
    ///  - Kotwica (R1, K1): raz na (osade, kategorie, dobe) - slownik budzetu w UpdateDemandShift x czynnik grupy (m x Afford: jedzenie / reszta) i czlon
    ///    dobrobytu wiersza BK (CalculateBudget, Low) tym samym czynnikiem; budzet nadwyzki na kategorie "lepsze".
    ///  - Doba (TownFolk.Daily w bloku): podatki panu od zarobku okna (1/2/3% W/R/P, podzamcze 3%; cech 1% R -> gmina), przelewy z kas, czynsz/leczenie/sluzba,
    ///    gmina (farma panu, roboty), zapas i nadwyzka, zakupy wsi w miescie targowym, kotwica na jutro.
    ///  - Wies (R13): podatek BK x VillageLordScale + VillageLordExtraShare utargu (decyzja Jeffa 12.10: 35/45/55%; 112 dopisuje wsi reszte), renta 20% -> oplata
    ///    stala (PopulationLaw), danina od kiesy ponad 15 dni zakupow. Dziesiecina (R21/C3, TownFolk.Tithe.cs) i tempo nadwyzki FolkSurplusSpendShare (lud miast i wsie).
    ///  - R14: zadowolenie BK grupy z Afford < 0.98 = migawka + min(0, delta BK) - 0.0015. R5: zapasy startowe z daru (nowa kampania) albo z nadwyzki (stary zapis).
    /// Wylacznik TownFolkLive (i TownFolkMode 1, A1 cofa "zakupy"): wylaczony = stan sprzed B1 - kiesy wracaja do kas w pierwszej dobie (ReturnAll).
    /// Kazda osada we wlasnym try, licznik potkniec, bez globalnych wylacznikow (CLAUDE.md 7). Wszystko na watku glownym (poza nim - zachowanie A1).
    /// </summary>
    internal static partial class TownFolk
    {
        // ------------------------------------------------------------ rodzaje zarobku poza oknem Note (podzial klas KL 1.3)
        internal const int ELabour = 11, ETurn = 12, ETopUp = 13, EValve = 14, EPublic = 15;
        private const int EKinds = 16;
        // kategorie "lepsze" (budzet nadwyzki) i zakupy wsi - lista sprawdzana przy pierwszym uzyciu z ItemCategories.All (linia stanu podaje znalezione)
        private static readonly string[] BetterIds = { "meat", "beer", "wine", "linen", "velvet", "pottery", "cheese", "oil" };
        private static readonly string[] VillIds = { "salt", "tools", "linen", "pottery", "beer" };
        private const int ShelfKeep = 5;                      // wies kupuje najwyzej sztuki ponad 5 na polce miasta targowego
        private const double R14Step = 0.0015;               // stala BK z galezi braku (EconomyPatches :532-544)
        private const double OfferIdxPow = 0.15;             // BK CalculateBudget: popyt x indeks^0.15 (zwykla polityka) - oferta nadwyzki w zlocie

        // ------------------------------------------------------------ przelaczniki
        private static float Unit(float v) { return ValveSplit.Unit(v); }

        /// <summary>Kiesa czynna z ustawien (bez GameStarted): TownFolkLive, TownFolkMode >= 1 i A1 cofa "zakupy" (TownPurse.NoMintNow) - bez A1 lud placilby za towar,
        /// za ktory BK i tak dopisuje zloto (KL 5.1 pkt 3). NIE zalezy od LogEnabled.</summary>
        internal static bool LiveKey { get { var s = Settings.Current; return s != null && s.TownFolkLive && s.TownFolkMode >= 1 && TownPurse.NoMintNow; } }
        /// <summary>E5/B1 czynne: klucz i kampania po GameStarted (tworzenie swiata - stan sprzed B1).</summary>
        internal static bool Live { get { var c = Campaign.Current; return LiveKey && c != null && c.GameStarted; } }
        /// <summary>Zamki: dodatkowo zawor zamku 110 (CastlePurseEnabled - cofa "zakupy" zamkow).</summary>
        internal static bool CastleLive { get { var s = Settings.Current; return Live && s != null && s.CastlePurseEnabled; } }

        // ------------------------------------------------------------ stan kampanii (zapis arm_townfolk) i sesji
        private static int _seedT, _seedC;                   // zapasy startowe: 0 jeszcze nie, 1 z daru (nowa kampania), 2 z nadwyzki (stary zapis), 3 wylaczone
        private static string _pending;                      // zapis wczytany, osady rozwiazywane przy pierwszym uzyciu
        private static long _orphan;                         // kiesy osad nieznanych przy wczytaniu - do kasy miasta w najblizszej dobie (posiadacz "kiesy ludu miast")
        private static string _stateKey;
        private static Town _tickTown; private static int _tickPaid;   // zaplata ludu w ticku konsumpcji (MoneyLedger.ConsumePostfix)
        private static double _brFacFood = 1, _brFacRest = 1; private static bool _brAnchor;
        private static readonly List<ItemCategory> _keys = new List<ItemCategory>();
        private static List<ItemCategory> _better, _villCats; private static string _betterText = "-", _villText = "-";
        private static long _ctlStock0 = long.MinValue, _ctlIn, _ctlOut;   // kontrola zamknieta kies (miasta, zamki, gminy)

        // liczniki okna (zdarzenia miedzy dobami: konsumpcja, Earn, zawor) - DailyLive zamyka
        private sealed class LWin
        {
            public long PaidW, PaidR, PaidP, PaidC, Court, UnpaidT, UnpaidC, GmFood, Excise, MadeT, MadeC, FromTown, ValveT, ValveC, CourtGrant, SlaveW, VFee;
            public int Ticks, TicksC, Fallback, R14Food, R14Rest, VFeeN; public double R14Bk; public long VTaxCut, VTaxBase, VTaxAdd;
            public readonly long[] Earn = new long[EKinds]; public long EarnC;
            public int C2Hit, C2NoFood, C2Wait, C2NoShelf;     // E5/C2: korekty jedzenia R14 i zdjete (brak jedzenia, poza glodem z braku pieniedzy, bez jedzenia na polce)
            // P1 (pomiar, TownFolk.Savings.cs): oferta nadwyzki dopisana do budzetu kategorii "lepszych" (zl), kupione z niej, oferta bez wiersza na polce; bez kategorii w slowniku
            public readonly double[] OfX = new double[OfMax], OfBought = new double[OfMax], OfNoRow = new double[OfMax];
            public readonly int[] OfN = new int[OfMax], OfNoRowN = new int[OfMax];
            public double OfNoCat; public int OfNoCatN, OfPlaces;
        }
        private static LWin _win = new LWin();

        // liczniki doby (DailyLive) i linie
        private sealed class LDay
        {
            public int Towns, Castles, Villages, NFood, NFull, MLow, FacLow, NoTrade, VNoGoods, VNoCal, NoLordN, CNoCourt;
            public double HW, HR, HP, HS, People, B, Wg, Wf, Wp, G0, G0a, MSum, MMin = double.NaN, FfSum, FrSum, FfMin = double.NaN, FrMin = double.NaN; public int MN;
            public long TaxW, TaxR, TaxP, TaxC, Guild, Farm, Held, NoLord, Player;
            public long LabT, TurnT, TopT, LabC, Cut, LabS, Skup; public double BaseFolk, BaseVill, BaseLife, BaseCourt, BaseSale, BaseCar, BaseGm, LabF, LabG, LabA;
            public long Rent, Heal, Serv, GmWage, GmMat, SurpWage; public long OffT, OffC;
            public long KW, KR, KP, KG, KC, CCr; public double NW, NR, NP, AW, AR, AP, NFW, NFR, NFP, AFW, AFR, AFP, CNeed, CAff, Lokata;
            public double CInc, CWg, CHeads;
            public long VBuy, VPieces; public double VNeed, VWant;
            public long Seed, Orphan;
            // dziesiecina (E5/C3, TownFolk.Tithe.cs): wsie (nalezne / zaplacone), lud miast W/R/P, jalmuzna miast / wsi, place i material septow, bez kaplana, bez miasta targowego
            public long TiVDue, TiV, TiW, TiR, TiP, TiAlmsT, TiAlmsV, TiWage, TiMat, TiNoPriestT, TiNoPriestV, TiNoMarket; public int TiPriestN, TiNoPriestN, TiVN;
            // P1 (obieg kiesy ludu): danina z oszczednosci do korony wedlug klasy (W, R, P, podzamcze) i kiesa ponad zapas dni przed / po daninie (klasy z nadwyzka)
            public readonly long[] Aid = new long[4]; public readonly double[] Ov0 = new double[4], Ov1 = new double[4];
        }
        private static LDay _ld;
        private static LWin _lw;
        private static int _liveDay = -1;                              // E5/C4: dzien biezacej doby kiesy czynnej (stempel ADay osad - okno People of this land)
        private static string _lineState, _lineSeed, _lineRet, _lineLab, _lineRights, _lineR14, _lineTithe, _lineC2;
        private static double _lastR = double.NaN; private static long _lastDStock, _lastIn, _lastOut;
        // zapasy startowe (linia jednorazowa)
        private static long _sW, _sR, _sP, _sC, _sV, _sGift; private static int _sTowns, _sCastles, _sVills, _sShort, _sNoBk;

        private static void ResetLive()
        {
            _seedT = _seedC = 0; _pending = null; _orphan = 0; _stateKey = null;
            _tickTown = null; _tickPaid = 0; _brFacFood = _brFacRest = 1; _brAnchor = false; _keys.Clear();
            _better = null; _villCats = null; _betterText = _villText = "-"; _villBy = null;
            _ctlStock0 = long.MinValue; _ctlIn = _ctlOut = 0;
            _win = new LWin(); _ld = null; _lw = null; _liveDay = -1;
            _lineState = _lineSeed = _lineRet = _lineLab = _lineRights = _lineR14 = _lineTithe = _lineC2 = null;
            _lastR = double.NaN; _lastDStock = _lastIn = _lastOut = 0;
            _sW = _sR = _sP = _sC = _sV = _sGift = 0; _sTowns = _sCastles = _sVills = _sShort = _sNoBk = 0;
            ResetTithe();
            ResetP1();   // P1: danina z oszczednosci i pomiary obiegu kiesy (pamiec sesji)
            ResetHorses();   // K1-3a krok 2: liczniki linii koni roboczych (licznik osady jest w Folk - czysci go Reset)
            ResetLadder();   // P3-P5 (zbytki): kategorie listy dostatku, liczniki linii (podzial osady jest w Folk - czysci go Reset)
        }

        // ============================================================ wejscie do ludu (Earn)
        private static void SplitOf(int kind, out double w, out double r, out double p)
        {
            switch (kind)
            {
                case KLife: case KLifeCaravan: case KLifeBand: case KGarrison: w = 0.8; r = 0.1; p = 0.1; return;   // KL 1.3: uslugi
                case KCourt: w = 0.8; r = 0.2; p = 0; return;
                case KBuildLabour: w = 0.6; r = 0.4; p = 0; return;
                case KCraftPaid: case ETopUp: w = 0.7; r = 0.3; p = 0; return;
                case ELabour: w = LabW; r = 1 - LabW; p = 0; return;
                case ETurn: w = 0; r = 0; p = 1; return;
                case EValve: w = 0.2; r = 0.1; p = 0.7; return;                                                      // KL L6
                case EPublic: w = 0.5; r = 0.5; p = 0; return;
                default: w = 1; r = 0; p = 0; return;                                                                // kopalnie, inne - wyrobnicy
            }
        }

        /// <summary>Zapis zarobku do kies osady (czysta arytmetyka - nie rzuca): podzial klas wedlug rodzaju, praca niewolnych do wlascicieli (R40/P60),
        /// zarobek okna (podstawa podatkow). Zamek - cala kwota do podzamcza.</summary>
        private static void Credit(Folk f, long amt, int kind)
        {
            if (amt <= 0) return;
            f.LDayIn += amt;
            if (kind >= 0 && kind < EKinds) f.EarnK[kind] += amt;       // P1 (pomiar): sklad zarobku osady w oknie (tylko licznik)
            if (f.Kind == 1) { f.KW += amt; f.EarnWin[0] += amt; return; }
            double w, r, p;
            SplitOf(kind, out w, out r, out p);
            if (f.Slave && f.HS + f.HW > 0 && w > 0)
            {
                double y = w * f.HS / (f.HS + f.HW);
                w -= y; r += y * OwnR; p += y * OwnP;
                _win.SlaveW += (long)(amt * y);
            }
            long a1 = (long)(amt * r), a2 = (long)(amt * p), a0 = amt - a1 - a2;
            f.KW += a0; f.KR += a1; f.KP += a2;
            f.EarnWin[0] += a0; f.EarnWin[1] += a1; f.EarnWin[2] += a2;
        }

        /// <summary>
        /// Jedyne wejscie do ludu (R11): zarobek `amt` osady `st` rodzaju `kind` - zwraca kwote przyjeta (0 przy wylaczonym B1, wsi, poza watkiem albo
        /// wyjatku - wolajacy przelewa wtedy calosc platnikowi jak dotad). Sam zapis w kiesie (bez zlota z kasy) - wolajacy daje kasie `amt - wynik`.
        /// </summary>
        internal static int Earn(Settlement st, int amt, int kind)
        {
            try
            {
                if (amt <= 0 || st == null || !Live || OffMain()) return 0;
                if (st.IsCastle && !CastleLive) return 0;
                EnsureImported();
                var f = Get(st);
                if (f == null || f.Kind > 1) return 0;
                long t = Stopwatch.GetTimestamp();
                Credit(f, amt, kind);
                if (f.Kind == 1) _win.EarnC += amt; else if (kind >= 0 && kind < EKinds) _win.Earn[kind] += amt;
                _ctlIn += amt;
                _patchTicks += Stopwatch.GetTimestamp() - t;
                return amt;
            }
            catch (Exception e) { Stumble("Earn", e); return 0; }
        }

        /// <summary>Hak 75/25 (R9, KL 1.3): z wydatku zolnierzy, ludzi karawan, band i zalog w osadzie `TownFolkServiceShare` do ludu (uslugi), reszta kupcom.</summary>
        internal static int EarnService(Settlement st, int amt, int kind)
        {
            try
            {
                if (amt <= 0 || !Live) return 0;
                int x = (int)(amt * (double)Unit(Settings.Current.TownFolkServiceShare));
                return x > 0 ? Earn(st, Math.Min(x, amt), kind) : 0;
            }
            catch (Exception e) { Stumble("EarnService", e); return 0; }
        }

        /// <summary>Kasa osady -> lud (przelew w calosci tutaj: ChangeGold, potem zapis). Zwraca to, co naprawde zeszlo z kasy.</summary>
        private static long FromCoffer(Folk f, long x, int kind)
        {
            if (x <= 0 || f == null || f.St == null || f.St.Town == null) return 0;
            var town = f.St.Town;
            int ask = (int)Math.Min(int.MaxValue, Math.Min(x, Math.Max(0, town.Gold)));
            if (ask <= 0) return 0;
            int had = town.Gold;
            town.ChangeGold(-ask);
            long got = had - town.Gold;
            if (got <= 0) return 0;
            Credit(f, got, kind);
            _ctlIn += got;
            return got;
        }

        /// <summary>WorkshopTrade.PayToTown: place warsztatu gracza zaplacone z jego kiesy (przelew gry do kasy miasta) - czesc ludu z kasy do kiesy (ksiega N9).</summary>
        internal static void EarnFromTown(Settlement st, int x, int kind)
        {
            try
            {
                if (x <= 0 || st == null || !Live || OffMain()) return;
                var f = Get(st);
                if (f == null || f.Kind > 1 || (f.Kind == 1 && !CastleLive)) return;
                long got = FromCoffer(f, x, kind);
                if (got <= 0) return;
                _win.FromTown += got;
                if (kind >= 0 && kind < EKinds) _win.Earn[kind] += got;
                MoneyLedger.Note(MoneyLedger.NFolk, st, -(int)got, -1);   // recenzja B1 (1-4): src -1 - przelew przechodni (kiesa gracza -> kasa -> lud), nie zakup kasy; poza "nasze sciezki" (OnTownCoin pomija src < 0)
            }
            catch (Exception e) { Stumble("EarnFromTown", e); }
        }

        /// <summary>Zawor miasta (R4, KL L6): czesc ludu (TownFolkMerchantProfitShare) z kasy do kies - W20/R10/P70. Zwraca to, co zeszlo z kasy.</summary>
        internal static int EarnValve(Settlement st, int x)
        {
            try
            {
                if (x <= 0 || st == null || !st.IsTown || !Live || OffMain()) return 0;
                var f = Get(st);
                if (f == null) return 0;
                long got = FromCoffer(f, x, EValve);
                _win.ValveT += got; _win.Earn[EValve] += got;
                return (int)got;
            }
            catch (Exception e) { Stumble("EarnValve", e); return 0; }
        }

        /// <summary>Zawor zamku (R12, hak CastleDuesSuburbShare): czesc podzamcza z kasy zamku do kiesy podzamcza. Zwraca to, co zeszlo z kasy.</summary>
        internal static int EarnCastleValve(Settlement st, int x)
        {
            try
            {
                if (x <= 0 || st == null || !st.IsCastle || !CastleLive || OffMain()) return 0;
                var f = Get(st);
                if (f == null) return 0;
                long got = FromCoffer(f, x, EValve);
                _win.ValveC += got; _win.EarnC += got;
                return (int)got;
            }
            catch (Exception e) { Stumble("EarnCastleValve", e); return 0; }
        }

        /// <summary>A2 -> B1 (KL 1.2 "podzamcze: czesc wiktu z kuchni pana"): wplata dworu do siedziby-zamku (czesc kupcow) - kredyt na jedzenie podzamcza,
        /// bez ruchu zlota (zloto dworu jest juz w kasie zamku); najwyzej 7 dob przewidywanej konsumpcji zamku.</summary>
        internal static void CourtCredit(Settlement seat, int x)
        {
            try
            {
                var s = Settings.Current;
                if (x <= 0 || seat == null || !seat.IsCastle || !CastleLive || !s.TownFolkCastleCourtFeeds || OffMain()) return;
                var f = Get(seat);
                if (f == null) return;
                double cap = 7.0 * Math.Max(0, f.PredFood + f.PredRest);
                long nw = f.CourtCr + x;
                if (cap > 0 && nw > cap) nw = Math.Max(f.CourtCr, (long)cap);
                if (cap <= 0) nw = Math.Max(f.CourtCr, x);
                _win.CourtGrant += Math.Max(0, nw - f.CourtCr);
                f.CourtCr = nw;
            }
            catch (Exception e) { Stumble("CourtCredit", e); }
        }

        private static long PurseOf(Folk f, int c) { return c == 0 ? f.KW : c == 1 ? f.KR : c == 2 ? f.KP : f.KG; }
        private static void AddPurse(Folk f, int c, long x) { if (c == 0) f.KW += x; else if (c == 1) f.KR += x; else if (c == 2) f.KP += x; else f.KG += x; }

        /// <summary>Przelew wewnatrz ludu osady (kiesa -> kiesa, 3 = gmina): najwyzej stan kiesy platnika; odbiorca-klasa zapisuje zarobek okna.</summary>
        private static long Move(Folk f, int from, int to, long x)
        {
            long y = Math.Min(x, PurseOf(f, from));
            if (y <= 0) return 0;
            AddPurse(f, from, -y); AddPurse(f, to, y);
            if (to < 3) f.EarnWin[to] += y;
            return y;
        }

        /// <summary>E5/B2 (184, scalenie B - plan B1 pkt 6: korona dopisuje czlon w tej samej funkcji co podatki pana): podatek wojenny korony od zarobku okna
        /// klasy `c` (ta sama podstawa co podatek pana) - KingdomTreasury.FolkWarTax (wojna, stawka s, bramka oblezenia KFolkWarTax, przelew do skarbca krolestwa)
        /// najwyzej do stanu kiesy; tu zdjecie z kiesy tej klasy. Kiesa -> skarbiec (posiadacze H22/H23 -> H9), w bloku dobowym przed CrownIncome.Begin. Zwraca zaplacone.</summary>
        private static long PayCrown(Folk f, int c, int cls, long earned)
        {
            if (earned <= 0) return 0;
            int t = KingdomTreasury.FolkWarTax(f.St, cls, earned, Math.Max(0, PurseOf(f, c)));
            if (t <= 0) return 0;
            AddPurse(f, c, -t);
            _ctlOut += t; f.LDayOut += t;
            return t;
        }

        /// <summary>Kiesa `c` osady -> pan osady (podatki od zarobku, farma gminy): ChangeHeroGold w try (wyjatek - nic nie schodzi), RentToday (D "ziemia",
        /// powinnosci 165, budowy). Bez zywego pana - do gminy (odbiorca zapasowy). Oblezona osada - zostaje (BesiegedPay KFolkDues). Zwraca zaplacone panu.</summary>
        private static long PayLord(Folk f, int c, long x, LDay d)
        {
            x = Math.Min(x, PurseOf(f, c));
            if (x <= 0) return 0;
            var st = f.St;
            if (BesiegedPay.Holds(st, BesiegedPay.KFolkDues, x, true)) { d.Held += x; return 0; }
            var clan = st.OwnerClan;
            var lord = clan != null ? clan.Leader : null;
            if (lord == null || !lord.IsAlive)
            {
                if (c != 3 && f.Kind == 0) { Move(f, c, 3, x); d.NoLord += x; d.NoLordN++; }   // miasto: do gminy; zamek - zostaje w kiesie
                return 0;
            }
            int ix = (int)Math.Min(int.MaxValue, x);
            try { lord.ChangeHeroGold(ix); } catch (Exception e) { Stumble("PayLord", e); return 0; }
            AddPurse(f, c, -ix);
            _ctlOut += ix; f.LDayOut += ix;
            int r0; PopulationLaw.RentToday.TryGetValue(clan, out r0); PopulationLaw.RentToday[clan] = r0 + ix;
            CirculationWindows.NoteHeroGold(lord, ix);
            if (lord == Hero.MainHero) d.Player += ix;
            return ix;
        }

        // ============================================================ kotwica (nawias konsumpcji)
        /// <summary>
        /// DemandPostfix (po pomiarze G0/Idx/Sat0): slownik budzetu osady x czynnik grupy (jedzenie: m x Afford_jedzenie, reszta: m x Afford_reszta) - raz na
        /// (osade, kategorie, dobe); budzet nadwyzki ludu na kategorie "lepsze". Czynniki zapamietane dla AnchorBudgetPostfix (czlon dobrobytu wiersza BK).
        /// </summary>
        private static void AnchorDict(Folk f, Town town, Dictionary<ItemCategory, float> dict, double g0)
        {
            if (f == null || f.Kind > 1 || !f.Cal || !Live || (f.Kind == 1 && !CastleLive)) return;
            var s = Settings.Current;
            if (!f.FacOk)
            {
                // pierwsza doba po wczytaniu / nowa osada: przewidywanie z zapisu, a bez niego budzet tej doby (podzial jedzenie / reszta ze slownika)
                if (f.PredFood + f.PredRest > 0) Anchor(f, f.PredFood, f.PredRest);
                else
                {
                    double gf = 0;
                    foreach (var kv in dict) if (kv.Key != null && kv.Value > 0 && GroupOf(kv.Key) == 0) gf += kv.Value;
                    Anchor(f, gf, Math.Max(0, g0 - gf));
                }
            }
            double ff = s.TownFolkAnchorBasket ? Clamp01(f.FacFood) : 1.0, fr = s.TownFolkAnchorBasket ? Clamp01(f.FacRest) : 1.0;
            f.FacFoodUsed = ff; f.FacRestUsed = fr; f.OfferFood = f.OfferRest = 0;
            double after = g0;
            if (ff < 1 || fr < 1)
            {
                _keys.Clear();
                foreach (var kv in dict) _keys.Add(kv.Key);
                after = 0;
                foreach (var k in _keys)
                {
                    float v;
                    if (k == null || !dict.TryGetValue(k, out v) || !(v > 0)) continue;
                    float nv = (float)(v * (GroupOf(k) == 0 ? ff : fr));
                    dict[k] = nv; after += nv;
                }
            }
            long off = f.Offer[0] + f.Offer[1] + f.Offer[2];
            if (off > 0 || LadderOn) after += AddOffer(f, town, dict, off);   // P3: przy liscie dostatku takze oferta 0 - podzial targu na jutro (sygnal P4, zakupy P5)
            f.G0a += after;
            _brFacFood = ff; _brFacRest = fr; _brAnchor = ff < 1 || fr < 1;
        }

        private static double Clamp01(double v) { return double.IsNaN(v) ? 1.0 : (v < 0 ? 0 : (v > 1 ? 1 : v)); }

        private static List<ItemCategory> CatsOf(string[] ids, out string text)
        {
            var l = new List<ItemCategory>(); var found = new List<string>();
            try
            {
                foreach (var c in ItemCategories.All)
                    if (c != null && Array.IndexOf(ids, c.StringId) >= 0 && !l.Contains(c)) { l.Add(c); found.Add(c.StringId); }
            }
            catch (Exception e) { Stumble("CatsOf", e); }
            text = found.Count > 0 ? string.Join(", ", found.ToArray()) : "BRAK";
            return l;
        }

        /// <summary>Budzet nadwyzki ludu (oferta z wczorajszej doby) dopisany do kategorii "lepszych" obecnych w slowniku, proporcjonalnie do ich budzetu (w zlocie:
        /// / indeks^0.15 i / czesc domowa HistoricalPrices). Brak kategorii - pieniadz zostaje w kiesie. Zwraca dopisany popyt (do G0a).</summary>
        private static double AddOffer(Folk f, Town town, Dictionary<ItemCategory, float> dict, long off)
        {
            if (LadderOn) return LadderOffer(f, town, dict, off);       // P3 (zbytki, klucz FolkLuxuryList): lista dostatku z wagami grup (TownFolk.Luxury.cs)
            if (_better == null) _better = CatsOf(BetterIds, out _betterText);
            double sum = 0;
            foreach (var c in _better) { float v; if (dict.TryGetValue(c, out v) && v > 0) sum += v; }
            if (!(sum > 0)) { OfferNoCat(off); return 0; }             // P1 (pomiar): cala oferta bez kategorii w slowniku (pieniadz zostaje w kiesie)
            double added = 0;
            int bi = -1;
            foreach (var c in _better)
            {
                float v;
                bi++;
                if (!dict.TryGetValue(c, out v) || !(v > 0)) continue;
                double x = off * v / sum;
                double idx = Math.Max(0.01, town.GetItemCategoryPriceIndex(c));
                double hs = Math.Max(0.05, HistoricalPrices.HouseShare(c));
                double add = x / (Math.Pow(idx, OfferIdxPow) * hs);
                dict[c] = (float)(v + add); added += add;
                OfferNote(bi, c, x, add, v);                             // P1 (pomiar): oferta w zlocie na kategorie (tylko licznik)
                if (GroupOf(c) == 0) f.OfferFood += x; else f.OfferRest += x;
            }
            return added;
        }

        /// <summary>
        /// Postfiks Priority.Low na BK EconomyPatches.CalculateBudget (po HistoricalPrices.BudgetPostfix, przed pomiarem E5/0): w nawiasie konsumpcji tej osady
        /// czlon dobrobytu wiersza (liczony od nowa przy kazdej pozycji polki) x czynnik grupy - jak slownik (K1: nie caly wynik, bez m^k). Wywolania spoza
        /// nawiasu (RawPrice.HouseBudget) - bez zmian. Wyjatek - wynik bez zmian.
        /// </summary>
        public static void AnchorBudgetPostfix(Town __0, ItemCategory __2, ref float __result)
        {
            if (__2 == null) return;
            bool br = _brAnchor && _brOpen && ReferenceEquals(__0, _brTown);
            // GL-2 (P2 b): czlon dobrobytu wiersza x m_t (zamek x skala podzamcza) - kazde wywolanie (nawias konsumpcji, prognoza targu zamku, RawPrice.HouseBudget),
            // bo to ten sam popyt ludzi osady; 1 = klucz MarketByPeople wylaczony albo brak skali - wtedy dokladnie dawna kotwica (tylko w dol, tylko w nawiasie)
            float sc = PeopleMarket.DemandScale(__0);
            if (!br && sc == 1f) return;
            long t0 = sc != 1f ? Stopwatch.GetTimestamp() : 0;
            try
            {
                double fac = br ? (GroupOf(__2) == 0 ? _brFacFood : _brFacRest) : 1.0;
                double k = sc * fac;                                       // NOWA galaz "w gore" (k > 1): tylko miasta i zamki z m x czynnik kotwicy > 1 (licznik linii GL2)
                if (sc == 1f ? fac >= 1 : k == 1.0) return;
                float part = HistoricalPrices.ProsperityPart(__0, __2);
                if (!(part > 0)) return;
                float v = __result + (float)((k - 1.0) * part);
                __result = v > 0f ? v : 0f;
            }
            catch (Exception e) { Stumble("AnchorBudgetPostfix", e); }
            finally { if (sc != 1f) PeopleMarket.NoteAnchorWork(Stopwatch.GetTimestamp() - t0); }
        }

        /// <summary>R13: postfiks na BK BKTaxModel.CalculateVillageTaxFromIncome(Village, int marketIncome) - przy kiesie czynnej podatek pana x VillageLordScale
        /// + VillageLordExtraShare x utarg (decyzja Jeffa 12.10: 0.5 x 70% + 10% = 45% zwykla, 35% niska, 55% wysoka), najwyzej caly utarg; majatki BK biora
        /// swoj udzial z tej czesci pana jak dotad (BK AccumulateTradeTax). 112 (VillageTakings) dopisuje wsi DOKLADNIE reszte (TaxOf czyta ten sam model).
        /// Wyjatek - wynik bez zmian.</summary>
        public static void VillageTaxPostfix(int __1, ref int __result)
        {
            try
            {
                if (__result <= 0 || !Live) return;
                var s = Settings.Current;
                long inc = Math.Max(0, __1);
                long add = (long)(inc * (double)Unit(s.VillageLordExtraShare));
                long v = (long)(__result * (double)Unit(s.VillageLordScale)) + add;
                if (v > inc) v = inc;
                if (!OffMain()) { _win.VTaxBase += __result; _win.VTaxCut += __result - v; _win.VTaxAdd += add; }
                __result = (int)v;
            }
            catch (Exception e) { Stumble("VillageTaxPostfix", e); }
        }

        // ============================================================ zaplata za towar (R1) - z CastlePurse.ConsumePostfix (First)
        /// <summary>
        /// Zamiast cofniecia "zakupow" A1: `made` = przyrost kasy w konsumpcji (BK ChangeGold(num6) - jedyny zapis zlota w nawiasie). Miasto: klasy placa swoja czesc
        /// (udzialy z kotwicy: towar x Afford + oferta) najwyzej z kiesy, kasa zatrzymuje zaplate bez akcyzy, gmina akcyze; dokup jedzenia BK przy zapasie <= 10%
        /// niezaplacony przez lud - z gminy. Zamek: podzamcze placi, reszte pokrywa kredyt dworu (bez zlota). Reszta przyrostu wraca do nicosci jak w A1 - JEDNO
        /// ChangeGold. Zwraca, ile kasa zatrzymala (>= 0), albo -1 (nie obsluzone - wolajacy cofa calosc jak A1).
        /// </summary>
        internal static int PayForGoods(Town town, int made)
        {
            bool applied = false; long keep = 0;
            try
            {
                if (town == null || made <= 0 || !Live || OffMain()) return -1;
                if (town.IsCastle && !CastleLive) return -1;
                EnsureImported();
                var f = Get(town.Settlement);
                if (f == null || f.Kind > 1) return -1;
                var s = Settings.Current;
                long p0 = 0, p1 = 0, p2 = 0, fromG = 0, excise = 0, court = 0, unpaid;
                if (f.Kind == 0)
                {
                    double s0 = f.Share[0], s1 = f.Share[1], s2 = f.Share[2], ss = s0 + s1 + s2;
                    if (!(ss > 0)) { s0 = f.KW; s1 = f.KR; s2 = f.KP; ss = s0 + s1 + s2; }   // bez kotwicy (przed kalibracja) - wedlug kies
                    if (ss > 0)
                    {
                        long w0 = (long)(made * s0 / ss), w1 = (long)(made * s1 / ss), w2 = made - w0 - w1;
                        p0 = Math.Min(w0, Math.Max(0, f.KW)); p1 = Math.Min(w1, Math.Max(0, f.KR)); p2 = Math.Min(w2, Math.Max(0, f.KP));
                    }
                    long paid = p0 + p1 + p2;
                    excise = (long)Math.Round(paid * (double)Math.Max(0f, Math.Min(0.2f, s.TownFolkExcise)));
                    long rest = made - paid;
                    // dokup jedzenia BK (zapas <= 10%) niezaplacony przez lud - z gminy (plan 4.2, R1); recenzja B1 (1-5): tylko czesc jedzenia niezaplaconej reszty
                    // (reszta rozlozona na towar wedlug zlota grup w nawiasie) - sukno, zbytki i towar ogolny bez zaplaty zostaja "zjedzone bez zaplaty"
                    if (rest > 0 && _brLowFood && ReferenceEquals(_brTown, town))
                    {
                        long fg = Math.Min((long)made, BracketFoodGold(town));
                        long restFood = fg > 0 ? (long)(rest * (double)fg / made) : 0;
                        fromG = Math.Min(restFood, Math.Max(0, f.KG + excise));
                    }
                    keep = paid - excise + fromG;
                    unpaid = made - paid - fromG;
                }
                else
                {
                    p0 = Math.Min((long)made, Math.Max(0, f.KW));
                    if (s.TownFolkCastleCourtFeeds) court = Math.Min(made - p0, Math.Max(0, f.CourtCr));
                    keep = p0;
                    unpaid = made - p0 - court;
                }
                if (made - keep > 0) town.ChangeGold(-(int)(made - keep));   // jedyny zapis zlota (reszta przyrostu wraca do nicosci jak w A1)
                applied = true;
                f.KW -= p0; f.KR -= p1; f.KP -= p2; f.KG += excise - fromG; f.CourtCr -= court;
                f.PaidG[0] += p0; f.PaidG[1] += p1; f.PaidG[2] += p2; f.PaidKeep += keep;
                _tickTown = town; _tickPaid = (int)keep;
                _ctlOut += keep;
                if (f.Kind == 0) { _win.PaidW += p0; _win.PaidR += p1; _win.PaidP += p2; _win.UnpaidT += unpaid; _win.GmFood += fromG; _win.Excise += excise; _win.MadeT += made; _win.Ticks++; }
                else { _win.PaidC += p0; _win.Court += court; _win.UnpaidC += unpaid; _win.MadeC += made; _win.TicksC++; }
                f.LDayPaid += p0 + p1 + p2; f.LDayUnpaid += unpaid; f.LDayCourt += court; f.LDayOut += p0 + p1 + p2;
                try { R14(f); } catch (Exception e) { Stumble("R14", e); }
                return (int)keep;
            }
            catch (Exception e) { Stumble("PayForGoods", e); return applied ? (int)keep : -1; }
        }

        /// <summary>MoneyLedger.ConsumePostfix (po CastlePurse First): ile z przyrostu kasy w tym ticku to zaplata ludu (przelew z kiesy, nie zrodlo) - zeruje.</summary>
        internal static int TakeTickPaid(Town town)
        {
            if (town == null || !ReferenceEquals(town, _tickTown)) return 0;
            int x = _tickPaid; _tickTown = null; _tickPaid = 0;
            return x;
        }

        /// <summary>A1 cofnelo caly przyrost (B1 nie obsluzylo ticku: przed kalibracja, potkniecie) - licznik linii.</summary>
        internal static void NoteFallback() { _win.Fallback++; }

        /// <summary>
        /// R14 (K2): zadowolenie BK grupy z Afford < 0.98 = migawka sprzed konsumpcji + min(0, delta BK) - 0.0015 (obciete 0..1); Afford >= 0.98 - wynik BK.
        /// Jedzenie -> Food, reszta -> Industrial / General / Luxury (BK ConsumptionType: Luxury 0, Industrial 1, General 2, Food 3). Bez latki na UpdateSatisfaction.
        /// E5/C2 (TownFolkHungerHurts): Food tylko przy glodzie z braku pieniedzy i jedzeniu na polce (C2Food); wylaczone - jak B1 (Afford &lt; 0.98).
        /// </summary>
        private static void R14(Folk f)
        {
            var s = Settings.Current;
            if (!s.TownFolkSatisfactionFromBasket || !f.Sat0Ok || !f.FacOk) return;
            bool food = f.AffFood < 0.98, rest = f.AffRest < 0.98;
            if (food && s.TownFolkHungerHurts) food = C2Food(f);      // E5/C2: galaz jedzenia tylko przy glodzie z braku pieniedzy (wylaczone - jak B1)
            if (!food && !rest) return;
            var sat = Sats(f);
            if (sat == null || sat.Length < 4) return;
            for (int i = 0; i < 4; i++)
            {
                bool g = i == 3 ? food : rest;
                if (!g) continue;
                double dl = sat[i] - f.Sat0[i];
                if (dl > 0) _win.R14Bk += dl;
                double v = f.Sat0[i] + Math.Min(0.0, dl) - R14Step;
                sat[i] = (float)(v < 0 ? 0 : (v > 1 ? 1 : v));
                if (i == 3) _win.R14Food++; else _win.R14Rest++;
            }
            if (food && s.TownFolkHungerHurts) { _win.C2Hit++; f.C2Hit = true; }
        }

        // ============================================================ E5/C2 (203, PLAN 5.9, D2) - glod ma skutki: galaz jedzenia R14
        /// <summary>
        /// Galaz jedzenia R14 przy TownFolkHungerHurts [D 09.10 A]: tylko glod z braku pieniedzy (Hunger.Of = NoMoney: Afford jedzenia &lt; 0.95 przez 7 dob z rzedu,
        /// kampania &gt;= 120 dob) i tylko gdy jedzenie jest na polce (nawias tej osady przeszedl wiersz jedzenia). Brak jedzenia (Food 0 warowni, glod okregu E4/6)
        /// karza juz BK "Food Shortage", RBL "Severe Starvation" i E4/6 - bez drugiej kary. Liczniki okna do linii "Glod ludu (E5/C2)".
        /// </summary>
        private static bool C2Food(Folk f)
        {
            var c = Hunger.Of(f.St);
            if (c == HungerCause.NoFood) { _win.C2NoFood++; return false; }
            if (c != HungerCause.NoMoney) { _win.C2Wait++; return false; }
            if (!BracketHadFood(f.St.Town)) { _win.C2NoShelf++; return false; }
            return true;
        }

        /// <summary>E5/C2 "jedzenie na polce": w otwartym nawiasie konsumpcji tej osady BK przeszedl choc jeden wiersz jedzenia (CalculateBudget idzie tylko po towarze
        /// z polki, ktorego kategoria jest w popycie - BK EconomyPatches ItemConsumptionPatch.Prefix). Nawias nie tej osady - false (bez kary).</summary>
        private static bool BracketHadFood(Town town)
        {
            if (!_brOpen || town == null || !ReferenceEquals(town, _brTown)) return false;
            foreach (var kv in _brTrack) if (kv.Key != null && kv.Value != null && kv.Value.Rows > 0 && GroupOf(kv.Key) == 0) return true;
            return false;
        }

        /// <summary>E5/C2 (Hunger.Of): glod z braku pieniedzy - kiesa czynna, miasto albo zamek z kotwica, Afford jedzenia na jutro &lt; 0.95 przez 7 dob z rzedu
        /// (licznik HungerDays z doby), kampania &gt;= 120 dob. Watek glowny (slownik osad); poza nim - false.</summary>
        internal static bool MoneyHungry(Settlement st)
        {
            if (st == null || !Live || OffMain()) return false;
            if (st.IsCastle && !CastleLive) return false;
            Folk f;
            if (!_f.TryGetValue(st, out f) || f == null || f.Kind > 1 || !f.Cal || !f.FacOk) return false;
            return f.HungerDays >= Hunger.MoneyDays && f.AffFood < Hunger.MoneyAfford && Hunger.CampaignAge() >= Hunger.FromDay;
        }

        /// <summary>E5/C2: potkniecie z Hunger (licznik TownFolk, bez wylacznikow).</summary>
        internal static void StumbleOut(string where, Exception e) { Stumble(where, e); }

        /// <summary>E5/C2: licznik dob glodu z braku pieniedzy - po kotwicy doby (Afford na jutro).</summary>
        private static void NoteHungerDay(Folk f)
        {
            f.HungerDays = f.FacOk && f.AffFood < Hunger.MoneyAfford ? Math.Min(f.HungerDays + 1, 100000) : 0;
        }

        /// <summary>E5/C2: linia doby "Glod ludu (E5/C2)" - przyczyny glodu osad (Hunger.Of) po kotwicy doby, korekty okna i kontrola "obie kary tej samej doby"
        /// (korekta jedzenia C2 w oknie, a warownia z Food 0 w chwili doby - warunek BK "Food Shortage" / RBL "Severe Starvation").</summary>
        private static void HungerLine(int day, LWin w)
        {
            var s = Settings.Current;
            int money = 0, moneyT = 0, moneyC = 0, streak = 0, starving = 0, district = 0, both = 0, osad = 0;
            foreach (var f in _f.Values)
            {
                if (f.Kind > 1 || f.St == null) continue;
                try
                {
                    if (f.Kind == 1 && !CastleLive) { f.C2Hit = false; continue; }
                    osad++;
                    bool st0 = Hunger.Starving(f.St);
                    if (f.C2Hit && st0) both++;
                    f.C2Hit = false;
                    if (f.HungerDays > 0) streak++;
                    var c = Hunger.Of(f.St);
                    if (c == HungerCause.NoFood) { if (st0) starving++; else district++; }
                    else if (c == HungerCause.NoMoney) { money++; if (f.Kind == 0) moneyT++; else moneyC++; }
                }
                catch (Exception e) { f.C2Hit = false; Stumble("HungerLine", e); }
            }
            double age = Hunger.CampaignAge();
            _lineC2 = "Glod ludu (E5/C2): dzien " + day + " | TownFolkHungerHurts " + (s.TownFolkHungerHurts ? "tak" : "nie") + ", kampania " + ((int)age).ToString(Inv)
                      + " dob (kara od " + ((int)Hunger.FromDay).ToString(Inv) + ": " + (age >= Hunger.FromDay ? "tak" : "nie") + "), osad " + osad
                      + " | brak pieniedzy (Afford jedzenia < " + Hunger.MoneyAfford.ToString("0.00", Inv) + " przez " + Hunger.MoneyDays + " dob): " + money + " (miasta " + moneyT + " / zamki " + moneyC
                      + "), z licznikiem >= 1 doby " + streak + " | brak jedzenia (bez kary zadowolenia - BK/RBL/E4/6): Food 0 warowni " + starving + ", glod okregu ponad prog " + district
                      + " | korekty jedzenia R14 (C2) " + w.C2Hit + "; zdjete: brak jedzenia " + w.C2NoFood + ", poza glodem z braku pieniedzy (Afford >= 0.95, < 7 dob albo < 120 dob) " + w.C2Wait + ", bez jedzenia na polce " + w.C2NoShelf
                      + " | obie kary tej samej doby " + both + ".";
        }

        // ============================================================ doba (TownFolk.Daily przy kiesie czynnej)
        private static void DailyLive()
        {
            int day = (int)CampaignTime.Now.ToDays - 1;
            _liveDay = day;                                            // E5/C4: stempel doby liczb osad (ADay)
            EnsureImported();
            bool first = _sessionDay < 0;
            if (first) _sessionDay = day;
            StateLine(true);
            if (!_calDone)
            {
                if (!PopulationLaw.Calibrated)
                {
                    _lineFolk = "Kiesa ludu: dzien " + day + " | CZYNNA - czeka na kalibracje ksiegi ludzi (PopulationLaw) - przeplywy okna zostaja w kiesach, kotwica od kalibracji";
                    CloseWin(); ClearWindowsLive(); return;
                }
                Calibrate(day);
            }
            var d = new LDay();
            // stary zapis albo wlaczenie w trakcie: zapasy z nadwyzki kas (najwyzej polowa) - przelew i migawka M20 przed robota doby (M18)
            try { SeedOld(d); } catch (Exception e) { Stumble("SeedOld", e); }
            if (_orphan > 0) { try { OrphanBack(d); } catch (Exception e) { Stumble("OrphanBack", e); } }
            var lw = CloseWin();
            // dziesiecina (E5/C3): kaplani BK raz na dobe, potem wsie (utarg okna) - przed miastami: place i material septow wsi licza sie w miastach targowych tej doby
            try { TitheBegin(); } catch (Exception e) { Stumble("TitheBegin", e); }
            try { VillageTithe(d); } catch (Exception e) { Stumble("VillageTithe", e); }
            AidBegin();                                                // P1: liczniki doby daniny z oszczednosci (czas poboru, nadwyzka po daninie wedlug krolestw)
            LadderBegin();                                             // P3-P5: liczniki doby zakupow podzamczy i wsi z listy dostatku, oferta kupujacych z zewnatrz (P4) od zera
            for (int pass = 0; pass < 2; pass++)
            {
                foreach (var f in _f.Values)
                {
                    if (f.Kind != pass) continue;
                    try { if (pass == 0) TownLive(f, d); else CastleLiveDay(f, d); }
                    catch (Exception e) { Stumble("DailyLive(rodzaj " + f.Kind + ")", e); }
                }
            }
            try { Villages(d); } catch (Exception e) { Stumble("Villages", e); }
            try { LadderDay(day, d); } catch (Exception e) { Stumble("LadderDay", e); }   // P3-P5: linia "Zbytki ludu (P3-P5)"
            try { HungerLine(day, lw); } catch (Exception e) { Stumble("HungerLine", e); }   // E5/C2: po kotwicach doby (licznik glodu)
            // kontrola zamknieta (kiesy miast, podzamczy, gminy): zmiana stanu = wplywy - wydatki
            long stock = TotalTown() + TotalCastle() + TotalGmina();
            if (_ctlStock0 != long.MinValue) { _lastDStock = stock - _ctlStock0; _lastIn = _ctlIn; _lastOut = _ctlOut; _lastR = _lastDStock - (_ctlIn - _ctlOut); }
            else _lastR = double.NaN;
            _ctlStock0 = stock; _ctlIn = _ctlOut = 0;
            _ld = d; _lw = lw;
            _aidDay = day;                                             // P1: liczby daniny tej doby gotowe (linia korony "Danina z oszczednosci (P1)" czyta je w tym samym takcie)
            bool csvDay = !first && ((day - CampaignStart(day) + 1) % 7 == 0 || !_csvFirst);
            if (csvDay) _csvFirst = true;
            try { MeasureP1Day(day, d, lw, first, csvDay); } catch (Exception e) { Stumble("MeasureP1Day", e); }   // P1: pomiary obiegu kiesy (sam log; tygodniowe razem z CSV) - przed BuildLines (zeruje liczby doby osad)
            try { BuildLines(day, d, lw, first, csvDay); } catch (Exception e) { Stumble("BuildLines", e); }
            ClearWindowsLive();
        }

        private static LWin CloseWin() { var w = _win; _win = new LWin(); return w; }

        private static void ClearWindowsLive()
        {
            foreach (var f in _f.Values)
            {
                ClearWindow(f);
                Array.Clear(f.PaidG, 0, 3); f.PaidKeep = 0; f.G0a = 0; f.OfferFood = f.OfferRest = 0;
            }
        }

        /// <summary>Przewidywany wydatek gry na jutro bez kotwicy: wydatek nawiasu tej doby (bez oferty nadwyzki) / czynnik uzyty, najwyzej budzet gry dnia (G0).</summary>
        private static void Predict(Folk f, out double wf, out double wr)
        {
            wf = f.PredFood; wr = f.PredRest;
            if (!(f.G0 > 0)) return;                                   // nawias nie biegl w tym oknie - przewidywanie z wczoraj
            double ofF, ofR; OfferSpent(f, out ofF, out ofR);         // P3 (poprawka Predict, A 9.9): przy liscie dostatku oferta KUPIONA, inaczej oferowana (jak dotad)
            double sf = Math.Max(0, f.SpendFood - ofF), sr = Math.Max(0, (f.SpendGame - f.SpendFood) - ofR);
            wf = sf / Math.Max(0.05, f.FacFoodUsed); wr = sr / Math.Max(0.05, f.FacRestUsed);
            double cap = f.G0;
            if (wf + wr > cap && wf + wr > 0) { double k = cap / (wf + wr); wf *= k; wr *= k; }
        }

        /// <summary>Kotwica i Afford (KL 5.2, S4) na jutro: miasto - koszyk klas D1 x indeks, m = min(1, koszyk / wydatek), Afford wedlug kies wobec PELNEJ potrzeby;
        /// zamek - koszyk podzamcza, kiesa + kredyt dworu.</summary>
        private static void Anchor(Folk f, double wFood, double wRest)
        {
            var s = Settings.Current;
            double idx = f.Idx > 0 && !double.IsNaN(f.Idx) ? f.Idx : 1.0;
            f.PredFood = wFood; f.PredRest = wRest;
            double W = wFood + wRest;
            f.FacOk = true;
            if (f.Kind == 1)
            {
                double B = CastleHeads(f) * BC * idx;                  // GL-3 (P3): podzamcze wedlug ludzi wsi zamku (klucz wylaczony - f.HW)
                double L = Math.Max(0, f.KW) + (s.TownFolkCastleCourtFeeds ? Math.Max(0, f.CourtCr) : 0);
                double need = W > 0 ? Math.Min(W, B) : B, m = W > 0 && B > 0 ? Math.Min(1.0, B / W) : 1.0, F = need * FdC;
                double af = F > 0 ? Math.Min(1.0, L / F) : 1.0, ar = need - F > 0 ? Math.Min(1.0, Math.Max(0, L - F) / (need - F)) : 1.0;
                f.Need[0] = need; f.Need[1] = f.Need[2] = 0;
                f.AffFood = af; f.AffRest = ar; f.MAnc = m; f.AffC[0] = need > 0 ? Math.Min(1.0, L / need) : 1.0; f.AffFC[0] = af;
                f.FacFood = B > 0 ? m * af : 1.0; f.FacRest = B > 0 ? m * ar : 1.0;
                f.Share[0] = 1; f.Share[1] = f.Share[2] = 0;
                return;
            }
            double r = Scale(f);
            double bW = f.HW * r * BW * idx, bR = f.HR * r * BR * idx, bP = f.HP * r * BP * idx;
            if (f.Slave) { double sl = f.HS * r * BS * idx; bR += sl * OwnR; bP += sl * OwnP; }
            double Bt = bW + bR + bP;
            if (!(Bt > 0))
            {
                // bez ludzi BK (i bez mediany) - bez kotwicy; zaplata wedlug kies
                f.FacFood = f.FacRest = f.AffFood = f.AffRest = f.MAnc = 1;
                for (int c = 0; c < 3; c++) { f.Need[c] = 0; f.Share[c] = 0; f.AffC[c] = f.AffFC[c] = 1; }
                return;
            }
            double G = W > 0 ? Math.Min(W, Bt) : Bt, mm = W > 0 ? Math.Min(1.0, Bt / W) : 1.0;
            double sumN = 0, af2 = 0, ar2 = 0;
            for (int c = 0; c < 3; c++)
            {
                double b = c == 0 ? bW : c == 1 ? bR : bP, keep = c == 0 ? KeepW : c == 1 ? KeepR : KeepP;
                f.Need[c] = b / Bt * G / keep; sumN += f.Need[c];
            }
            for (int c = 0; c < 3; c++)
            {
                double fd = c == 0 ? FdW : c == 1 ? FdR : FdP, keep = c == 0 ? KeepW : c == 1 ? KeepR : KeepP;
                double N = f.Need[c], F = N * fd, L = Math.Max(0, PurseOf(f, c));
                double ffc = F > 0 ? Math.Min(1.0, L / F) : 1.0, frc = N - F > 0 ? Math.Min(1.0, Math.Max(0, L - F) / (N - F)) : 1.0;
                double sc = sumN > 0 ? N / sumN : 0;
                af2 += sc * ffc; ar2 += sc * frc;
                f.AffFC[c] = ffc; f.AffC[c] = N > 0 ? Math.Min(1.0, L / N) : 1.0;
                f.Share[c] = N * keep * f.AffC[c] + f.Offer[c] * OfferPayPart(f);   // towar klasy, za ktory jutro zaplaci, + oferta nadwyzki (P3: x czesc kupiona ostatnio)
            }
            if (!(sumN > 0)) { af2 = ar2 = 1; }
            f.AffFood = af2; f.AffRest = ar2; f.MAnc = mm;
            f.FacFood = mm * af2; f.FacRest = mm * ar2;
        }

        private static void TownLive(Folk f, LDay d)
        {
            var s = Settings.Current;
            var st = f.St; var town = st.Town;
            if (town == null) return;
            d.Towns++;
            // ---- 1. podatki od zarobku okna: pan 1/2/3% (W/R/P), cech 1% R -> gmina (raz na dobe); E5/B2 korona 184 na wierzch (scalenie B)
            long eW = f.EarnWin[0], eR = f.EarnWin[1], eP = f.EarnWin[2];
            long tW = (long)Math.Round(eW * TaxW), tR = (long)Math.Round(eR * TaxR), tP = (long)Math.Round(eP * TaxP);
            long guild = (long)Math.Round(eR * GuildR);
            Array.Clear(f.EarnWin, 0, 3);
            d.TaxW += PayLord(f, 0, tW, d); d.TaxR += PayLord(f, 1, tR, d); d.TaxP += PayLord(f, 2, tP, d);
            d.Guild += Move(f, 1, 3, guild);
            PayCrown(f, 0, KingdomTreasury.WtW, eW); PayCrown(f, 1, KingdomTreasury.WtR, eR); PayCrown(f, 2, KingdomTreasury.WtP, eP);
            // ---- 1b. dziesiecina (E5/C3, R21; decyzja Jeffa 12.10) od tego samego zarobku okna: W 1% / R 2% / P 3% -> kaplan osady (material septow - sprzedaz kupcow przed krokiem 2)
            TownTithe(f, town, eW, eR, eP, d);
            // ---- 2. z kasy do ludu (koszt sprzedazy - najwyzej stan kasy): robocizna handlu, obrot P, doplata do 35% wartosci wyrobow
            double svc = Unit(s.TownFolkServiceShare), cs = Unit(s.CourtFolkShare);
            double rf = Unit(s.TownFolkLabourFood), rg = Unit(s.TownFolkLabourGoods), ra = Unit(s.TownFolkLabourArms);
            double life = f.Win[KLife] + f.Win[KLifeCaravan] + f.Win[KLifeBand] + f.Win[KGarrison], court = f.Win[KCourt];
            double lf = (1 - svc) * life, lg = 0, la = 0;                  // podstawa: jedzenie / towar / bron
            lf += (1 - cs) * court * 0.5; lg += (1 - cs) * court * 0.2; la += (1 - cs) * court * 0.3;
            double sale = 0;
            for (int mi = 0; mi < Mixes; mi++) { lf += f.Sale[mi] * MixOf[mi][0]; lg += f.Sale[mi] * MixOf[mi][1]; la += f.Sale[mi] * MixOf[mi][2]; sale += f.Sale[mi]; }
            double fp = f.PaidKeep, gF = 1, gG = 0, gA = 0;
            if (f.SpendGame > 0) { gF = f.Grp[0] / f.SpendGame; gG = f.Grp[1] / f.SpendGame; gA = f.Grp[2] / f.SpendGame; }
            lf += fp * gF; lg += fp * gG; la += fp * gA;
            lf += f.VillWin * 0.2; lg += f.VillWin * 0.8;
            lg += f.GmMatWin;
            double car = f.CarBuy + f.CarSell;
            double lab = lf * rf + lg * rg + la * ra + CaravanLab * car;
            // GL-B / 11c (klucz FolkEarnFromVillageBuys): robocizna od SKUPU plonu wsi - zaplata kasy taborom wsi w oknie x TownFolkLabourVillageBuys (wyladunek, miara,
            // przechowanie, przemial); koszt skupu - ta sama droga i to samo przyciecie do stanu kasy co robocizna od sprzedazy. Klucz wylaczony: okno 0, lab bez zmian
            double skupLab = 0;
            if (f.SkupWin > 0) { skupLab = f.SkupWin * (double)Unit(s.TownFolkLabourVillageBuys); d.Skup += f.SkupWin; lab += skupLab; }
            f.SkupWin = 0;
            double turnBase = (1 - svc) * life + (1 - cs) * court + sale + fp + f.VillWin + f.GmMatWin + f.CarBuy;
            double turn = Math.Max(0f, Math.Min(0.2f, s.TownFolkMerchantDraw)) * turnBase;
            double top = Math.Max(0, Unit(s.TownFolkCraftWageShare) * f.ProdV - f.Win[KCraftPaid]);
            d.BaseFolk += fp; d.BaseVill += f.VillWin; d.BaseLife += (1 - svc) * life; d.BaseCourt += (1 - cs) * court; d.BaseSale += sale; d.BaseCar += car; d.BaseGm += f.GmMatWin;
            d.LabF += lf * rf; d.LabG += lg * rg; d.LabA += la * ra;
            f.VillWin = 0; f.GmMatWin = 0;
            double all = lab + turn + top, k = 1;
            long gold = Math.Max(0, town.Gold);
            if (all > gold && all > 0) { k = gold / all; d.Cut++; }
            d.LabT += FromCoffer(f, (long)(lab * k), ELabour);
            if (skupLab > 0) d.LabS += (long)(skupLab * k);              // 11c: czesc robocizny od skupu (po przycieciu do kasy) - do linii
            d.TurnT += FromCoffer(f, (long)(turn * k), ETurn);
            d.TopT += FromCoffer(f, (long)(top * k), ETopUp);
            // ---- 3. wewnatrz ludu z wczorajszych zakupow: czynsz W 8% / R 13% wydatku (x 1/keep) -> P (plan pkt 0.14), leczenie -> R, sluzba P -> W
            double spW = f.PaidG[0] / KeepW, spR = f.PaidG[1] / KeepR, spP = f.PaidG[2] / KeepP;
            d.Rent += Move(f, 0, 2, (long)Math.Round(RentW * spW)) + Move(f, 1, 2, (long)Math.Round(RentR * spR));
            d.Heal += Move(f, 0, 1, (long)Math.Round(HlW * spW)) + Move(f, 2, 1, (long)Math.Round(HlP * spP));
            d.Serv += Move(f, 2, 0, (long)Math.Round(ServP * spP));
            Array.Clear(f.PaidG, 0, 3);
            // ---- 4. gmina: 7% stanu - 1/3 farma panu, 2/3 roboty: 75% place W/R po polowie, 25% material do kasy (podstawa robocizny jutro)
            long gx = (long)Math.Round(GminaOut * Math.Max(0, f.KG));
            f.GmWorks = 0;                                             // P2: budzet robot gminy tej doby (TownFolk.Works.cs)
            if (gx > 0)
            {
                long farm = (long)Math.Round(gx * GFarm);
                d.Farm += PayLord(f, 3, farm, d);
                long pub = gx - farm, wages = (long)Math.Round(GLab * pub), mat = pub - wages;
                // P2 (lud buduje miasto, klucz FolkBuildsTown): roboty gminy jako prawdziwe punkty budowy i material zdjety z polki - budzet zostaje w kiesie gminy
                // do BuildFunding.FolkWorks (ta sama doba, po budowach pana); czego budowa nie wezmie, idzie tam na place robot publicznych (W/R po polowie, bez
                // materialu bez sztuki). Klucz wylaczony - krok co do monety jak dotad
                if (WorksRoute) { f.GmWorks = pub; wages = 0; mat = 0; }
                long w1 = wages / 2;
                d.GmWage += Move(f, 3, 0, wages - w1) + Move(f, 3, 1, w1);
                mat = Math.Min(mat, Math.Max(0, f.KG));
                if (mat > 0)
                {
                    town.ChangeGold((int)mat);
                    f.KG -= mat; _ctlOut += mat; d.GmMat += mat; f.GmMatWin += mat;
                }
            }
            // ---- 5. zapas i nadwyzka (potrzeba z wczorajszej kotwicy): W ponad TownFolkReserveDays dni - FolkSurplusSpendShare (15%; dotad 7%) na towar jutro;
            //         R ponad 21, P ponad 30 dni - ten sam udzial: 70% place, 30% towar (decyzja Jeffa 12.10 (3))
            double rdW = Math.Max(1, Math.Min(60, s.TownFolkReserveDays)), sr = SpendRate(s);
            f.Offer[0] = f.Offer[1] = f.Offer[2] = 0;
            // P1 (szczebel 4 drabiny nadwyzki): w wojnie i przy potrzebie korony klasa oddaje skarbcowi krolestwa czesc kiesy PONAD zapas dni (PayAid - po robotach gminy,
            // przed oferta na jutro i placami z nadwyzki, ktore licza sie od reszty); wylaczone, pokoj, s = 0 - PayAid zwraca 0 i krok jest co do monety jak przed paczka
            f.DAid = 0; f.DOver = 0; f.DOvC[0] = f.DOvC[1] = f.DOvC[2] = 0;
            if (f.Need[0] > 0) { double ov = f.KW - rdW * f.Need[0]; if (ov > 0) { ov -= PayAid(f, 0, KingdomTreasury.WtW, ov, d); f.Offer[0] = (long)(sr * ov); } d.Lokata += Math.Max(0, ov); }
            if (f.Need[1] > 0)
            {
                double ov = f.KR - RdR * f.Need[1];
                if (ov > 0) { ov -= PayAid(f, 1, KingdomTreasury.WtR, ov, d); long lv = (long)(sr * ov), wp = (long)(SurpWage * lv); long moved = Move(f, 1, 0, wp); d.SurpWage += moved; f.Offer[1] = lv - moved; }
            }
            if (f.Need[2] > 0)
            {
                double ov = f.KP - RdP * f.Need[2];
                if (ov > 0)
                {
                    ov -= PayAid(f, 2, KingdomTreasury.WtP, ov, d);
                    long lv = (long)(sr * ov), wp = (long)(SurpWage * lv), toW = (long)(wp * 0.6);
                    long moved = Move(f, 2, 0, toW) + Move(f, 2, 1, wp - toW);
                    d.SurpWage += moved; f.Offer[2] = lv - moved;
                }
            }
            LadderOffers(f);                                           // P3 (zbytki): oferta na liste dostatku 1/60 nadwyzki po daninie; place R/P wyzej bez zmian
            for (int c = 0; c < 3; c++) d.OffT += f.Offer[c];
            // ---- 6. kotwica na jutro
            double wf, wr; Predict(f, out wf, out wr);
            double r = Scale(f); f.DR = r;
            f.DBook = f.PeopleCal * (PeopleMarket.MarketOn ? BookScale(f) : r);   // GL-2: ludzie ksiegi osady (linia, CSV) - glowy moga isc od ludzi rynku
            Anchor(f, wf, wr);
            NoteHungerDay(f);                                          // E5/C2: doby z rzedu bez chleba z braku pieniedzy
            // ---- liczby swiata
            double hW = f.HW * r, hR = f.HR * r, hP = f.HP * r, hS = f.HS * r, idx = f.Idx > 0 && !double.IsNaN(f.Idx) ? f.Idx : 1.0;
            double B = (hW * BW + hR * BR + hP * BP + (f.Slave ? hS * BS : 0)) * idx;
            d.HW += hW; d.HR += hR; d.HP += hP; d.HS += hS; d.People += f.DBook; d.B += B;
            d.Wg += f.SpendGame; d.Wf += f.SpendFood; d.Wp += wf + wr; d.G0 += f.G0; d.G0a += f.G0a;
            d.MSum += f.MAnc; d.MN++; if (double.IsNaN(d.MMin) || f.MAnc < d.MMin) d.MMin = f.MAnc; if (f.MAnc < 1) d.MLow++;
            double ff = s.TownFolkAnchorBasket ? Clamp01(f.FacFood) : 1, fr = s.TownFolkAnchorBasket ? Clamp01(f.FacRest) : 1;
            d.FfSum += ff; d.FrSum += fr; if (double.IsNaN(d.FfMin) || ff < d.FfMin) d.FfMin = ff; if (double.IsNaN(d.FrMin) || fr < d.FrMin) d.FrMin = fr; if (ff < 1 || fr < 1) d.FacLow++;
            d.KW += f.KW; d.KR += f.KR; d.KP += f.KP; d.KG += f.KG;
            double[] fdc = { FdW, FdR, FdP };
            for (int c = 0; c < 3; c++)
            {
                double N = f.Need[c];
                if (c == 0) { d.NW += N; d.AW += f.AffC[c] * N; d.NFW += N * fdc[c]; d.AFW += f.AffFC[c] * N * fdc[c]; }
                else if (c == 1) { d.NR += N; d.AR += f.AffC[c] * N; d.NFR += N * fdc[c]; d.AFR += f.AffFC[c] * N * fdc[c]; }
                else { d.NP += N; d.AP += f.AffC[c] * N; d.NFP += N * fdc[c]; d.AFP += f.AffFC[c] * N * fdc[c]; }
            }
            if (Math.Min(f.AffFC[0], Math.Min(f.AffFC[1], f.AffFC[2])) < 0.95) d.NFood++;
            if (Math.Min(f.AffC[0], Math.Min(f.AffC[1], f.AffC[2])) < 0.95) d.NFull++;
            f.DB = B; f.DWg = f.SpendGame; f.DM = f.MAnc;
            double needAll = f.Need[0] + f.Need[1] + f.Need[2], nf = f.Need[0] * FdW + f.Need[1] * FdR + f.Need[2] * FdP;
            f.DAff = needAll > 0 ? (f.AffC[0] * f.Need[0] + f.AffC[1] * f.Need[1] + f.AffC[2] * f.Need[2]) / needAll : 1;
            f.DAffF = nf > 0 ? (f.AffFC[0] * f.Need[0] * FdW + f.AffFC[1] * f.Need[1] * FdR + f.AffFC[2] * f.Need[2] * FdP) / nf : 1;
            f.ADay = _liveDay;                                         // E5/C4: liczby tej doby gotowe (okno People of this land)
        }

        private static void CastleLiveDay(Folk f, LDay d)
        {
            var s = Settings.Current;
            var st = f.St; var town = st.Town;
            if (town == null) return;
            d.Castles++;
            if (!CastleLive)
            {
                // zawor zamku 110 wylaczony przy kiesie czynnej: kiesa podzamcza wraca do kasy zamku (stan sprzed B1 dla zamkow), kredyt dworu 0; ponowne wlaczenie - z nadwyzki
                if (f.KW > 0) { town.ChangeGold((int)Math.Min(int.MaxValue, f.KW)); _ctlOut += f.KW; f.KW = 0; _seedC = 0; }
                f.CourtCr = 0; Array.Clear(f.EarnWin, 0, 3); f.Offer[0] = 0; f.FacOk = false; f.HungerDays = 0;   // E5/C2: kiesa nieczynna - licznik glodu od zera
                return;
            }
            // ---- 1. pan 3% od zarobku okna; E5/B2 korona 184 na wierzch (scalenie B)
            long eC = f.EarnWin[0];
            long tax = (long)Math.Round(eC * TaxC);
            d.CInc += eC;
            Array.Clear(f.EarnWin, 0, 3);
            d.TaxC += PayLord(f, 0, tax, d);
            PayCrown(f, 0, KingdomTreasury.WtCastle, eC);
            // ---- 2. robocizna z kasy zamku od sprzedazy zamku: 75% zycia i zalogi (jedzenie), 80% dworu, sprzedaz okna, towar podzamcza, zakupy wsi
            double svc = Unit(s.TownFolkServiceShare), cs = Unit(s.CourtFolkShare);
            double rf = Unit(s.TownFolkLabourFood), rg = Unit(s.TownFolkLabourGoods), ra = Unit(s.TownFolkLabourArms);
            double life = f.Win[KGarrison] + f.Win[KLife] + f.Win[KLifeCaravan] + f.Win[KLifeBand], court = f.Win[KCourt];
            double lab = (1 - svc) * life * rf + (1 - cs) * court * (0.5 * rf + 0.2 * rg + 0.3 * ra);
            for (int mi = 0; mi < Mixes; mi++) lab += f.Sale[mi] * (MixOf[mi][0] * rf + MixOf[mi][1] * rg + MixOf[mi][2] * ra);
            lab += f.PaidKeep * (0.8 * rf + 0.2 * rg) + f.VillWin * (0.2 * rf + 0.8 * rg);
            f.VillWin = 0;
            d.LabC += FromCoffer(f, (long)Math.Min(lab, Math.Max(0, town.Gold)), ELabour);
            // ---- 5. nadwyzka ponad 10 dni potrzeby: 7% na towar jutro (konsumpcja zamku)
            f.Offer[0] = 0;
            f.DAid = 0; f.DOver = 0; f.DOvC[0] = 0;                    // P1: danina podzamcza z kiesy ponad 10 dni potrzeby - przed oferta (jak w miescie)
            if (f.Need[0] > 0) { double ov = f.KW - RdC * f.Need[0]; if (ov > 0) { ov -= PayAid(f, 0, KingdomTreasury.WtCastle, ov, d); f.Offer[0] = (long)(SurplusRate * ov); } }
            CastleLadder(f, d);                                        // P3/P5 (zbytki): podzamcze 1/60 nadwyzki - oferta w konsumpcji zamku albo zakup w miescie najblizszym
            d.OffC += f.Offer[0];
            // ---- 6. kotwica (z kredytem dworu)
            double wf, wr; Predict(f, out wf, out wr);
            Anchor(f, wf, wr);
            NoteHungerDay(f);                                          // E5/C2
            if (f.CourtCr <= 0) d.CNoCourt++;
            double ch = CastleHeads(f);                                // GL-3 (P3)
            d.CHeads += ch; d.KC += f.KW; d.CCr += f.CourtCr; d.CNeed += f.Need[0]; d.CAff += f.AffC[0] * f.Need[0]; d.CWg += f.SpendGame;
            f.DB = ch * BC * (f.Idx > 0 ? f.Idx : 1); f.DWg = f.SpendGame; f.DM = f.MAnc; f.DAff = f.AffC[0]; f.DAffF = f.AffFC[0]; f.DR = 1;
            f.ADay = _liveDay;                                         // E5/C4
            // jedzenie zuzyte w konsumpcji zamku (pierscien 7 dob - linia "Jedzenie z targu", B4)
            f.Food7[f.Food7Head] = f.FoodSold; f.Food7Head = (f.Food7Head + 1) % 7; if (f.Food7Fill < 7) f.Food7Fill++;
            f.FoodSold = 0;
        }

        // ============================================================ wsie (R13): zakupy w miescie targowym, oplata stala, prog daniny
        private static double VillHeads(Folk f, bool scaled)
        {
            double h;
            if (scaled && VillPeopleHeads(f, out h)) return h;            // GL-3 (P3): srednia swiata glow BK wsi x u_v (klucz VillageHeadsByPeople)
            return Math.Max(0, f.HW + f.HR) * (scaled ? Scale(f) : 1.0);
        }

        private static void Villages(LDay d)
        {
            var s = Settings.Current;
            double pph = Math.Max(0f, Math.Min(1f, s.VillagePurchasePerHead));
            if (_villCats == null) _villCats = CatsOf(VillIds, out _villText);
            var shelf = new Dictionary<Settlement, List<EquipmentElement>>();
            foreach (var f in _f.Values)
            {
                if (f.Kind != 2) continue;
                try
                {
                    f.DVNeed = 0; f.DVBuy = 0;                         // E5/C4: zakupy tej doby (okno People of this land)
                    var vil = f.St.Village;
                    if (vil == null) continue;
                    if (!f.Cal) { d.VNoCal++; continue; }
                    d.Villages++;
                    double nv = pph * VillHeads(f, true);
                    int gold = vil.Gold;
                    double buy1 = Math.Min(nv, Math.Max(0, gold));
                    double extra = SpendRate(s) * Math.Max(0, gold - buy1 - RdV * nv);   // decyzja Jeffa 12.10 (3): 15%/d ponad 15 dni zakupow (dotad 7%)
                    double wx = VillWorkOnly() ? extra : 0;            // P5 (zbytki, recenzja WAZNE 2): nadwyzka na koszyk tylko dla soli i narzedzi (len, garnki, piwo - lista)
                    double want = buy1 + extra;
                    d.VNeed += nv; d.VWant += want;
                    f.DVNeed = nv; f.ADay = _liveDay;                  // E5/C4
                    if (want < 1) continue;
                    var mk = vil.TradeBound ?? vil.Bound;
                    if (mk == null || mk.Town == null) { d.NoTrade++; continue; }
                    List<EquipmentElement> list;
                    if (!shelf.TryGetValue(mk, out list)) { list = ShelfOf(mk); shelf[mk] = list; }
                    long spent = BuyGoods(vil, mk, list, want - wx, d, wx);
                    f.DVBuy = Math.Max(0, spent);                      // E5/C4
                    VillageLadder(f, vil, mk, gold, buy1, nv);         // P5 (zbytki): nadwyzka wsi na liste dostatku w miescie targowym (koszyk dnia wyzej bez zmian)
                    if (spent <= 0) { d.VNoGoods++; continue; }
                    Folk mf; if (_f.TryGetValue(mk, out mf)) mf.VillWin += spent;
                }
                catch (Exception e) { f.ADay = -1; Stumble("Villages(wies)", e); }   // E5/C4: liczby wsi z tej doby niepewne - okno bez liczby
            }
        }

        /// <summary>Towar dla wsi na polce miasta targowego (kategorie sol, narzedzia, len, garnki, piwo) - raz na dobe na targ.</summary>
        private static List<EquipmentElement> ShelfOf(Settlement mk)
        {
            var l = new List<EquipmentElement>();
            var r = mk.ItemRoster;
            if (r == null) return l;
            for (int i = 0; i < r.Count; i++)
            {
                var el = r.GetElementCopyAtIndex(i);
                var it = el.EquipmentElement.Item;
                if (it == null || el.Amount <= ShelfKeep || it.ItemCategory == null || !_villCats.Contains(it.ItemCategory)) continue;
                l.Add(el.EquipmentElement);
            }
            return l;
        }

        /// <summary>Zakup wsi w miescie targowym: budzet po rowno na kategorie obecne, cena targu, najwyzej sztuki ponad 5; najpierw zloto (wies -> kasa), potem towar z polki.</summary>
        private static long BuyGoods(Village vil, Settlement mk, List<EquipmentElement> list, double want, LDay d, double workExtra = 0)
        {
            if (list.Count == 0) return 0;
            var town = mk.Town; var roster = mk.ItemRoster;
            var cats = new List<ItemCategory>();
            foreach (var e in list) { var c = e.Item.ItemCategory; if (!cats.Contains(c)) cats.Add(c); }
            double per = want / cats.Count;
            double perW = workExtra > 0 ? workExtra / cats.Count : 0;  // P5 (recenzja WAZNE 2): nadwyzka wsi - sol i narzedzia tyle samo zl na kategorie co bez P5
            long spent = 0;
            foreach (var c in cats)
            {
                double left = per + (perW > 0 && VillWorkCat(c) ? perW : 0);
                foreach (var e in list)
                {
                    if (left < 1) break;
                    if (e.Item.ItemCategory != c) continue;
                    int ix = roster.FindIndexOfElement(e);
                    if (ix < 0) continue;
                    int have = roster.GetElementNumber(ix) - ShelfKeep;
                    if (have <= 0) continue;
                    int price = town.MarketData.GetPrice(e);
                    if (price <= 0) continue;
                    int n = (int)Math.Min(have, Math.Floor(left / price));
                    if (n <= 0) continue;
                    int cost = n * price;
                    int had = vil.Gold;
                    if (cost > had) { n = had / price; cost = n * price; if (n <= 0) return spent; }
                    vil.ChangeGold(-cost);
                    int got = had - vil.Gold;
                    town.ChangeGold(got);
                    roster.AddToCounts(e, -n);
                    spent += got; left -= got; d.VBuy += got; d.VPieces += n;
                }
            }
            return spent;
        }

        /// <summary>R13 (D1): oplata stala dnia wsi dla pana (VillageDuesPerHeadYear rocznie na wolna glowe, YearDays 364) - PopulationLaw zamiast renty 20%.
        /// Ulamek przechodzi na nastepna dobe (najwyzej 1 zl). 0 - wylaczone, nieskalibrowana.</summary>
        internal static int VillageFeeToday(Settlement st)
        {
            try
            {
                if (st == null || !st.IsVillage || !Live) return 0;
                var f = Get(st);
                if (f == null || !f.Cal) return 0;
                double fee = Math.Max(0f, Math.Min(50f, Settings.Current.VillageDuesPerHeadYear)) / YearDays * VillHeads(f, true) + f.VFeeAcc;
                int n = (int)Math.Floor(fee);
                f.VFeeAcc = Math.Max(0, Math.Min(1.0, fee - n));
                return n;
            }
            catch (Exception e) { Stumble("VillageFeeToday", e); return 0; }
        }

        /// <summary>PopulationLaw: oplata stala wsi zaplacona panu (licznik linii "Prawa panskie").</summary>
        internal static void NoteVillageFee(int pay) { if (pay > 0) { _win.VFee += pay; _win.VFeeN++; } }

        /// <summary>KingdomTreasury.Levies (R15a): danina wsi od kiesy ponad 15 dni zakupow (RdV x VillagePurchasePerHead x wolne glowy). 0 - wylaczone.</summary>
        internal static int VillageFloor(Settlement st)
        {
            try
            {
                if (st == null || !st.IsVillage || !Live) return 0;
                var f = Get(st);
                if (f == null || !f.Cal) return 0;
                double x = RdV * Math.Max(0f, Math.Min(1f, Settings.Current.VillagePurchasePerHead)) * VillHeads(f, true);
                return x >= int.MaxValue ? int.MaxValue : (int)x;
            }
            catch (Exception e) { Stumble("VillageFloor", e); return 0; }
        }

        // ============================================================ R5: zapasy startowe
        /// <summary>Nowa kampania (TownPurse.TrimStartGift / CastlePurse.TrimStartGift, mloda kampania): czy zapasy kies z daru.</summary>
        internal static bool SeedTownsDue { get { var s = Settings.Current; return Live && s.TownFolkStartReserves && _seedT == 0; } }
        internal static bool SeedCastlesDue { get { var s = Settings.Current; return CastleLive && s.TownFolkStartReserves && _seedC == 0; } }

        private static bool BkFor(Folk f)
        {
            if (f.Cal || f.BkCal) return !f.NoBk;
            bool ok = ReadBk(f, PopTypes(), null);
            f.BkCal = true;
            if (!ok) _sNoBk++;
            return ok;
        }

        /// <summary>Kiesy W/R/P miasta z daru: dni zapasu x koszyk KL x ludzie BK przy indeksie cen 1.0 (KL 5.3) / keep, najwyzej `gift`; przelew kasa -> kiesy.</summary>
        internal static int SeedTown(Settlement st, int gift)
        {
            try
            {
                if (gift <= 0 || st == null || !st.IsTown || !Live) return 0;
                var f = Get(st);
                if (f == null || !BkFor(f)) return 0;
                double rdW = Math.Max(1, Math.Min(60, Settings.Current.TownFolkReserveDays));
                double bR = f.HR * BR, bP = f.HP * BP;
                if (f.Slave) { double sl = f.HS * BS; bR += sl * OwnR; bP += sl * OwnP; }
                double w = rdW * f.HW * BW / KeepW, r = RdR * bR / KeepR, p = RdP * bP / KeepP, all = w + r + p;
                if (!(all > 0)) return 0;
                double k = Math.Min(1.0, gift / all);
                if (k < 1) _sShort++;
                long sw = (long)(w * k), sr = (long)(r * k), sp = (long)(p * k), tot = sw + sr + sp;
                if (tot <= 0) return 0;
                var town = st.Town;
                int had = town.Gold;
                town.ChangeGold(-(int)tot);
                long got = had - town.Gold;
                if (got < tot) { double q = got / (double)tot; sw = (long)(sw * q); sr = (long)(sr * q); sp = got - sw - sr; }
                f.KW += sw; f.KR += sr; f.KP += sp; _ctlIn += got;
                _sW += sw; _sR += sr; _sP += sp; _sTowns++; _sGift += gift;
                return (int)got;
            }
            catch (Exception e) { Stumble("SeedTown", e); return 0; }
        }

        private static Dictionary<Settlement, List<Folk>> _villBy;

        /// <summary>Wsie, ktorych targiem jest to miasto: do 15 dni zakupow ponad ich kiese, z reszty daru (kasa miasta -> kiesa wsi).</summary>
        internal static int SeedVillages(Settlement st, int left)
        {
            try
            {
                if (left <= 0 || st == null || st.Town == null || !Live) return 0;
                if (_villBy == null)
                {
                    _villBy = new Dictionary<Settlement, List<Folk>>();
                    foreach (var v in Village.All)
                    {
                        if (v == null || v.Settlement == null) continue;
                        var mk = v.TradeBound ?? v.Bound;
                        var vf = Get(v.Settlement);
                        if (mk == null || vf == null) continue;
                        List<Folk> l; if (!_villBy.TryGetValue(mk, out l)) { l = new List<Folk>(); _villBy[mk] = l; }
                        if (!l.Contains(vf)) l.Add(vf);
                    }
                }
                List<Folk> vs;
                if (!_villBy.TryGetValue(st, out vs)) return 0;
                double pph = Math.Max(0f, Math.Min(1f, Settings.Current.VillagePurchasePerHead));
                long used = 0;
                foreach (var vf in vs)
                {
                    if (left - used <= 0) break;
                    var vil = vf.St.Village;
                    if (vil == null || !BkFor(vf)) continue;
                    long want = (long)(RdV * pph * VillHeads(vf, false)) - Math.Max(0, vil.Gold);
                    long give = Math.Min(want, left - used);
                    if (give <= 0) continue;
                    int had = st.Town.Gold;
                    st.Town.ChangeGold(-(int)give);
                    int got = had - st.Town.Gold;
                    if (got <= 0) continue;
                    vil.ChangeGold(got);
                    used += got; _sV += got; _sVills++;
                }
                return (int)used;
            }
            catch (Exception e) { Stumble("SeedVillages", e); return 0; }
        }

        /// <summary>Kiesa podzamcza z daru zamku: 10 dni x 0.18 x ludzie BK podzamcza (indeks 1.0), najwyzej `gift`.</summary>
        internal static int SeedCastle(Settlement st, int gift)
        {
            try
            {
                if (gift <= 0 || st == null || !st.IsCastle || !CastleLive) return 0;
                var f = Get(st);
                if (f == null || !BkFor(f)) return 0;
                long want = (long)(RdC * f.HW * BC);
                long give = Math.Min(want, gift);
                if (give <= 0) return 0;
                if (give < want) _sShort++;
                int had = st.Town.Gold;
                st.Town.ChangeGold(-(int)give);
                long got = had - st.Town.Gold;
                f.KW += got; _ctlIn += got; _sC += got; _sCastles++; _sGift += gift;
                return (int)got;
            }
            catch (Exception e) { Stumble("SeedCastle", e); return 0; }
        }

        /// <summary>Koniec zapasow z daru (wolajacy robi migawke M20): flaga kampanii i linia jednorazowa.</summary>
        internal static void SeedDone(bool towns)
        {
            if (towns) _seedT = 1; else _seedC = 1;
            SeedLine(towns ? "z daru kas miast (nowa kampania)" : "z daru kas zamkow (nowa kampania)");
        }

        private static void SeedLine(string how)
        {
            long all = _sW + _sR + _sP + _sC + _sV;
            string l = "Kiesa ludu (E5/B1): zapasy startowe " + how + " - kiesy W " + _sW + " / R " + _sR + " / P " + _sP + ", podzamcze " + _sC + ", wsie " + _sV
                       + " (" + _sVills + " wsi) = " + all + " (miast " + _sTowns + ", zamkow " + _sCastles + "; dar do podzialu " + _sGift + ", reszta daru przycieta dalej - linie TownPurse/CastlePurse)"
                       + "; osad z darem albo nadwyzka mniejsza niz zapas " + _sShort + ", bez danych BK " + _sNoBk + " | koszyk KL przy indeksie cen 1.0 (W " + (Settings.Current != null ? Settings.Current.TownFolkReserveDays : 10)
                       + " dni, R 21, P 30, podzamcze 10, wsie 15 dni zakupow); migawka ksiegi M20 (kiesa ludu - zapasy startowe).";
            _lineSeed = _lineSeed == null ? l : _lineSeed + " || " + l;
            _sW = _sR = _sP = _sC = _sV = _sGift = 0; _sTowns = _sCastles = _sVills = _sShort = _sNoBk = 0;
        }

        /// <summary>Stary zapis / wlaczenie w trakcie (plan 6.2): kiesy z nadwyzki kas ponad zapas kupcow (D4), najwyzej polowa nadwyzki i najwyzej zapas dni (indeks 1.0);
        /// podzamcze z nadwyzki kasy zamku tak samo; wsie nic. Migawka M20.</summary>
        private static void SeedOld(LDay d)
        {
            var s = Settings.Current;
            if (_seedT != 0 && _seedC != 0) return;
            bool towns = _seedT == 0, castles = _seedC == 0 && CastleLive;
            if (!s.TownFolkStartReserves) { if (towns) _seedT = 3; if (castles) _seedC = 3; return; }
            if (!towns && !castles) return;
            foreach (var f in _f.Values)
            {
                try
                {
                    if (f.Kind == 0 && towns && f.Cal && f.St.Town != null)
                    {
                        long spare = TownPurse.Spare(f.St.Town);
                        int gift = (int)Math.Min(int.MaxValue, spare / 2);
                        if (gift > 0) d.Seed += SeedTown(f.St, gift);
                    }
                    else if (f.Kind == 1 && castles && f.Cal && f.St.Town != null)
                    {
                        long spare = (long)f.St.Town.Gold - CastlePurse.Reserve(f.St.Town);
                        int gift = (int)Math.Min(int.MaxValue, Math.Max(0, spare) / 2);
                        if (gift > 0) d.Seed += SeedCastle(f.St, gift);
                    }
                }
                catch (Exception e) { Stumble("SeedOld(osada)", e); }
            }
            if (towns) _seedT = 2;
            if (castles) _seedC = 2;
            MoneyLedger.Mark(MoneyLedger.MFolkStart);
            SeedLine("z nadwyzki kas ponad zapas kupcow (stary zapis albo kiesa wlaczona w trakcie; najwyzej polowa nadwyzki)");
        }

        private static void OrphanBack(LDay d)
        {
            foreach (var st in Settlement.All)
            {
                if (st == null || !st.IsTown || st.Town == null) continue;
                int x = (int)Math.Min(int.MaxValue, _orphan);
                st.Town.ChangeGold(x);
                _orphan -= x; d.Orphan += x; _ctlOut += x;   // posiadacz H22 -> kasa miasta (kontrola zamknieta)
                break;
            }
        }

        // ============================================================ wylaczenie, posiadacze, zapis
        internal static bool AnyPurse()
        {
            EnsureImported();
            if (_orphan != 0) return true;
            foreach (var f in _f.Values) if (f.KW != 0 || f.KR != 0 || f.KP != 0 || f.KG != 0 || f.CourtCr != 0) return true;
            return false;
        }

        /// <summary>Plan 6.3: kiesa wylaczona (TownFolkLive off, TownFolkMode 0 albo A1 off) - kiesy W/R/P i gmina do kasy miasta, podzamcze do kasy zamku, kredyt dworu 0;
        /// migawka M18 (w bloku). Ponowne wlaczenie - jak stary zapis (zapasy z nadwyzki).</summary>
        internal static void ReturnAll()
        {
            long ret = 0; int n = 0;
            foreach (var f in _f.Values)
            {
                try
                {
                    f.CourtCr = 0; f.FacOk = false; Array.Clear(f.Offer, 0, 3); Array.Clear(f.EarnWin, 0, 3); f.HungerDays = 0;   // E5/C2: licznik glodu od zera
                    long x = f.KW + f.KR + f.KP + f.KG;
                    var t = f.St != null ? f.St.Town : null;
                    if (t == null) { f.KW = f.KR = f.KP = f.KG = 0; continue; }
                    if (x > 0) { t.ChangeGold((int)Math.Min(int.MaxValue, x)); ret += x; n++; }
                    f.KW = f.KR = f.KP = f.KG = 0;
                }
                catch (Exception e) { Stumble("ReturnAll", e); }
            }
            if (_orphan > 0) { foreach (var st in Settlement.All) if (st != null && st.IsTown && st.Town != null) { st.Town.ChangeGold((int)Math.Min(int.MaxValue, _orphan)); ret += _orphan; _orphan = 0; break; } }
            _seedT = _seedC = 0; _ctlStock0 = long.MinValue; _ctlIn = _ctlOut = 0; _win = new LWin();
            MoneyLedger.Mark(MoneyLedger.MFolk);
            if (ret > 0) _lineRet = "Kiesa ludu (E5/B1): WYLACZONA w trakcie - kiesy ludu wrocily do kas osad: " + ret + " zl z " + n + " osad (migawka M18); kredyt dworu wyzerowany; ponowne wlaczenie - zapasy z nadwyzki jak stary zapis.";
        }

        /// <summary>Ksiega pieniadza H22 (kiesy ludu miast: W/R/P + kiesy osad nieznanych przy wczytaniu), H23 (podzamcza), H24 (gminy) - prawdziwe zloto niezaleznie od klucza.</summary>
        internal static long TotalTown()
        {
            if (_pending != null) EnsureImported();
            long t = _orphan;
            for (int i = 0; i < _fk.Count; i++) { var f = _fk[i]; if (f.Kind == 0) t += f.KW + f.KR + f.KP; }
            return t;
        }
        internal static long TotalCastle() { long t = 0; for (int i = 0; i < _fk.Count; i++) { var f = _fk[i]; if (f.Kind == 1) t += f.KW; } return t; }
        internal static long TotalGmina() { long t = 0; for (int i = 0; i < _fk.Count; i++) { var f = _fk[i]; if (f.Kind == 0) t += f.KG; } return t; }

        private static string D(double v) { return double.IsNaN(v) || double.IsInfinity(v) ? "0" : v.ToString("R", Inv); }

        /// <summary>Zapis arm_townfolk: "v1;seed=TC" + na osade "id,rodzaj,cal,HW,HR,HP,HS,PeopleCal,sl,KW,KR,KP,KG,CourtCr,Idx,PredFood,PredRest,HungerDays" (|; pole 17 - E5/C2, starszy zapis bez niego = 0, starsze DLL je pomijaja; pole 18 - K1-3a krok 2: licznik koni roboczych HorseOwed, starszy zapis bez niego = 0). Pusty przy kiesie wylaczonej i pustych kiesach.</summary>
        internal static string Export()
        {
            try
            {
                if (_pending != null) EnsureImported();
                if (!LiveKey && !AnyPurse()) return "";
                var sb = new StringBuilder(64 * 1024);
                sb.Append("v1;seed=").Append(_seedT).Append(_seedC).Append(";orphan=").Append(_orphan);
                foreach (var f in _f.Values)
                {
                    if (f.St == null || f.St.StringId == null) continue;
                    bool money = f.KW != 0 || f.KR != 0 || f.KP != 0 || f.KG != 0 || f.CourtCr != 0;
                    if (!f.Cal && !money) continue;
                    sb.Append('|').Append(f.St.StringId.Replace("|", "").Replace(",", "")).Append(',').Append(f.Kind).Append(',').Append(f.Cal ? 1 : 0).Append(',')
                      .Append(D(f.HW)).Append(',').Append(D(f.HR)).Append(',').Append(D(f.HP)).Append(',').Append(D(f.HS)).Append(',').Append(D(f.PeopleCal)).Append(',').Append(f.Slave ? 1 : 0).Append(',')
                      .Append(f.KW).Append(',').Append(f.KR).Append(',').Append(f.KP).Append(',').Append(f.KG).Append(',').Append(f.CourtCr).Append(',')
                      .Append(D(f.Idx)).Append(',').Append(D(f.PredFood)).Append(',').Append(D(f.PredRest)).Append(',').Append(f.HungerDays)
                      .Append(',').Append(Math.Max(0, Math.Min(WhMax, f.HorseOwed)).ToString("0.####", Inv));   // K1-3a krok 2: pole 18
                }
                return sb.ToString();
            }
            catch (Exception e) { Stumble("Export", e); return ""; }
        }

        internal static void Import(string data)
        {
            _pending = string.IsNullOrEmpty(data) ? null : data;
        }

        private static void EnsureImported()
        {
            var data = _pending;
            if (data == null) return;
            _pending = null;
            try
            {
                var parts = data.Split('|');
                var head = parts[0].Split(';');
                foreach (var h in head)
                {
                    if (h.StartsWith("seed=", StringComparison.Ordinal) && h.Length >= 7) { _seedT = h[5] - '0'; _seedC = h[6] - '0'; if (_seedT < 0 || _seedT > 3) _seedT = 0; if (_seedC < 0 || _seedC > 3) _seedC = 0; }
                    else if (h.StartsWith("orphan=", StringComparison.Ordinal)) { long o; if (long.TryParse(h.Substring(7), NumberStyles.Integer, Inv, out o)) _orphan += Math.Max(0, o); }
                }
                int lost = 0;
                for (int i = 1; i < parts.Length; i++)
                {
                    try
                    {
                        var a = parts[i].Split(',');
                        if (a.Length < 17) continue;
                        var st = Settlement.Find(a[0]);
                        long kw = L(a[9]), kr = L(a[10]), kp = L(a[11]), kg = L(a[12]), cc = L(a[13]);
                        var f = st != null ? Get(st) : null;
                        if (f == null || f.Kind.ToString(Inv) != a[1] || (f.Kind == 2 && kw + kr + kp + kg != 0))
                        {
                            // osada nieznana albo innego rodzaju: kiesy do kasy osady (gdy jest miastem/zamkiem) albo do najblizszej doby - nic w nicosc
                            long x = Math.Max(0, kw) + Math.Max(0, kr) + Math.Max(0, kp) + Math.Max(0, kg);
                            if (x > 0) { _orphan += x; lost++; }
                            continue;
                        }
                        f.Cal = a[2] == "1";
                        f.HW = Dd(a[3]); f.HR = Dd(a[4]); f.HP = Dd(a[5]); f.HS = Dd(a[6]); f.PeopleCal = Dd(a[7]); f.Slave = a[8] == "1";
                        f.KW = kw; f.KR = kr; f.KP = kp; f.KG = kg; f.CourtCr = Math.Max(0, cc);
                        double idx = Dd(a[14]); if (idx > 0) f.Idx = idx;
                        f.PredFood = Math.Max(0, Dd(a[15])); f.PredRest = Math.Max(0, Dd(a[16]));
                        f.HungerDays = a.Length > 17 ? (int)Math.Max(0, Math.Min(100000, L(a[17]))) : 0;   // E5/C2: zapis sprzed C2 - licznik od zera
                        f.HorseOwed = a.Length > 18 ? Math.Max(0, Math.Min(WhMax, Dd(a[18]))) : 0;   // K1-3a krok 2: zapis sprzed paczki - licznik koni roboczych od zera
                        f.FacOk = false;
                    }
                    catch (Exception e) { Stumble("Import(osada)", e); }
                }
                if (lost > 0) Log.Info("Kiesa ludu (E5/B1): zapis - kiesy " + lost + " osad nieznanych w swiecie (" + _orphan + " zl) pojda do kasy miasta w najblizszej dobie (nic w nicosc).");
            }
            catch (Exception e) { Stumble("Import", e); }
        }

        private static long L(string s) { long v; return long.TryParse(s, NumberStyles.Integer, Inv, out v) ? v : 0; }
        private static double Dd(string s) { double v; return double.TryParse(s, NumberStyles.Float, Inv, out v) && !double.IsNaN(v) && !double.IsInfinity(v) ? v : 0; }

        // ============================================================ linie (TownFolk.Lines po ksiedze pieniadza)
        /// <summary>Linia stanu raz na sesje i przy kazdej zmianie (czynna / nieczynna z powodem) - stawki, przelaczniki, klucze B (P K3).</summary>
        private static void StateLine(bool live)
        {
            var s = Settings.Current;
            if (s == null) return;
            string why = live ? "" : !s.TownFolkLive ? "TownFolkLive 0" : s.TownFolkMode < 1 ? "TownFolkMode 0" : !TownPurse.NoMintNow ? "A1 nie cofa \"zakupow\" (Town Purse Regulator 0, latki 110 albo Town Folk Buy Without Minting)" : "kampania przed GameStarted";
            string key = live ? "on|" + (s.CastlePurseEnabled ? 1 : 0) : "off|" + why;
            if (key == _stateKey) return;
            _stateKey = key;
            if (_better == null) _better = CatsOf(BetterIds, out _betterText);
            if (_villCats == null) _villCats = CatsOf(VillIds, out _villText);
            int slaves = 0; foreach (var f in _f.Values) if (f.Slave && f.Kind < 2) slaves++;
            var sb = new StringBuilder(1500);
            sb.Append("Kiesa ludu (E5/B1): ").Append(live ? "CZYNNA" : "NIECZYNNA (" + why + ")").Append(" (TownFolkLive ").Append(s.TownFolkLive ? 1 : 0).Append(", TownFolkMode ").Append(s.TownFolkMode).Append(")")
              .Append(" | A1: ").Append(TownPurse.NoMintNow ? "tak" : "nie").Append(" | zamki (110): ").Append(s.CastlePurseEnabled ? "tak" : "nie")
              .Append(" | stawki: uslugi ").Append(F2(Unit(s.TownFolkServiceShare))).Append(", dwor ").Append(F2(Unit(s.CourtFolkShare))).Append(", robocizna ").Append(F2(Unit(s.TownFolkLabourFood)))
              .Append("/").Append(F2(Unit(s.TownFolkLabourGoods))).Append("/").Append(F2(Unit(s.TownFolkLabourArms))).Append(", place rzemiosla ").Append(F2(Unit(s.TownFolkCraftWageShare)))
              .Append(", obrot P ").Append(s.TownFolkMerchantDraw.ToString("0.000", Inv)).Append(", zawor miast ").Append(F2(Unit(s.TownFolkMerchantProfitShare))).Append(", zawor zamkow ")
              .Append(F2(Unit(s.TownFolkCastleProfitShare))).Append(", akcyza ").Append(s.TownFolkExcise.ToString("0.000", Inv)).Append(", zapas W ").Append(s.TownFolkReserveDays).Append(" dni")
              .Append(", wies: pan x").Append(F2(Unit(s.VillageLordScale))).Append(", zakupy ").Append(s.VillagePurchasePerHead.ToString("0.00", Inv)).Append(" zl/glowe/d, oplaty ")
              .Append(s.VillageDuesPerHeadYear.ToString("0.0", Inv)).Append(" zl/glowe/rok")
              .Append(" | decyzja 12.10: dopisek pana z utargu wsi ").Append(F2(Unit(s.VillageLordExtraShare))).Append(", tempo nadwyzki ").Append(F2(SpendRate(s)))
              .Append("/d (podzamcze ").Append(F2(SurplusRate)).Append("), dziesiecina ").Append(TitheKey ? "tak (wsie " + F2(TitheVillage(s)) + " utargu, lud W/R/P 1/2/3%)" : "nie")
              .Append(", kaplani BK: ").Append(TitheBkState())
              .Append(" | kotwica: ").Append(s.TownFolkAnchorBasket ? "tak" : "nie").Append(", kuchnia pana: ").Append(s.TownFolkCastleCourtFeeds ? "tak" : "nie")
              .Append(", R14: ").Append(s.TownFolkSatisfactionFromBasket ? "tak" : "nie").Append(", zapasy startowe: ").Append(s.TownFolkStartReserves ? "tak" : "nie")
              .Append(" | niewolni: kultury osad z listy I1 (").Append(slaves).Append(" miast i zamkow)")
              .Append(" | kategorie \"lepsze\": ").Append(_betterText).Append(", zakupy wsi: ").Append(_villText)
              // scalenie B (krok E5/B3): wartosci CZYNNE (LordsEvenUp - z wyrownaniem 0.5 i 2/1.5/0.25, bez - stare klucze), nie same klucze
              .Append(" | klucze B: TownRentLordShare ").Append(F2(LordsEvenUp.TownLordShare(s))).Append(" (wyrownanie E5/B3 CastleLordsEvenUp ").Append(LordsEvenUp.Live ? "czynne" : s.CastleLordsEvenUp ? "nieczynne" : "wylaczone")
              .Append("), CrownRentWeight Town/Castle/Village ").Append(LordsEvenUp.WeightTown(s).ToString("0.##", Inv))
              .Append("/").Append(LordsEvenUp.WeightCastle(s).ToString("0.##", Inv)).Append("/").Append(LordsEvenUp.WeightVillage(s).ToString("0.##", Inv))
              .Append(", WarTaxFromFolk (E5/B2) ").Append(s.WarTaxFromFolk ? "ON" : "OFF")
              .Append(", ShipsToPort/ShipTradeThroughPort/ShipPrizesToPort ").Append(s.ShipsToPort ? "ON" : "OFF").Append("/").Append(s.ShipTradeThroughPort ? "ON" : "OFF").Append("/").Append(s.ShipPrizesToPort ? "ON" : "OFF")
              .Append(" | zapasy startowe (flaga kampanii) miasta/zamki ").Append(_seedT).Append("/").Append(_seedC).Append(" (0 jeszcze nie, 1 z daru, 2 z nadwyzki, 3 wylaczone).");
            _lineState = sb.ToString();
        }

        /// <summary>Daily przy kiesie nieczynnej: linia stanu przy zmianie (tylko gdy klucz albo zapis w ogole istnieje).</summary>
        private static void LiveOffLine()
        {
            var s = Settings.Current;
            if (s == null || Campaign.Current == null || !Campaign.Current.GameStarted) return;
            if (_stateKey == null && !s.TownFolkLive) return;
            StateLine(false);
        }

        private static string P1(double part, double whole) { return whole > 0 ? (100.0 * part / whole).ToString("0.0", Inv) + "%" : "-"; }

        private static void BuildLines(int day, LDay d, LWin w, bool partial, bool csvDay)
        {
            long paidT = w.PaidW + w.PaidR + w.PaidP;
            long gameT = w.MadeT, gameC = w.MadeC;
            long unpaid = w.UnpaidT + w.UnpaidC;
            long e = 0; for (int k = 0; k < EKinds; k++) e += w.Earn[k];
            long army = w.Earn[KLife] + w.Earn[KLifeCaravan] + w.Earn[KLifeBand] + w.Earn[KGarrison];
            var sb = new StringBuilder(4096);
            sb.Append("Kiesa ludu: dzien ").Append(day).Append(partial ? " | doba niepelna (pierwsza w sesji)" : "").Append(" | CZYNNA")
              .Append(" | miasta ").Append(d.Towns).Append(": ludzie ksiegi ").Append(N0(d.People)).Append(" (BK z kalibracji x ksiega: W ").Append(N0(d.HW)).Append(" / R ").Append(N0(d.HR))
              .Append(" / P ").Append(N0(d.HP)).Append(d.HS > 0 ? " / niewolni utrzymywani " + N0(d.HS) : "").Append(")")
              .Append(", koszyk ludu ").Append(N0(d.B)).Append(", wydatek gry ").Append(N0(d.Wg)).Append(" (jedzenie ").Append(N0(d.Wf)).Append("; bez kotwicy na jutro ").Append(N0(d.Wp)).Append(")")
              .Append(", budzet gry ").Append(N0(d.G0)).Append(" (po kotwicy i ofercie nadwyzki ").Append(N0(d.G0a)).Append(")")
              .Append(" | kotwica: m srednio ").Append(d.MN > 0 ? F2(d.MSum / d.MN) : "-").Append(" (min ").Append(F2(d.MMin)).Append(", miast z m < 1: ").Append(d.MLow).Append(")")
              .Append(", czynnik jedzenie / reszta srednio ").Append(d.Towns > 0 ? F2(d.FfSum / d.Towns) : "-").Append(" / ").Append(d.Towns > 0 ? F2(d.FrSum / d.Towns) : "-")
              .Append(" (min ").Append(F2(d.FfMin)).Append(" / ").Append(F2(d.FrMin)).Append("), miast z czynnikiem < 1: ").Append(d.FacLow)
              .Append(" | zaplacone przez lud miast ").Append(paidT).Append(" (W ").Append(w.PaidW).Append(" / R ").Append(w.PaidR).Append(" / P ").Append(w.PaidP).Append("; akcyza do gmin ").Append(w.Excise).Append(")")
              .Append(", zamki ").Append(w.PaidC).Append(" (pokryte dworem ").Append(w.Court).Append(")")
              .Append(", zjedzone bez zaplaty ").Append(unpaid).Append(" (").Append((gameT + gameC > 0 ? 100.0 * unpaid / (gameT + gameC) : 0).ToString("0.00", Inv)).Append("% wydatku gry; miasta ").Append(w.UnpaidT)
              .Append(", zamki ").Append(w.UnpaidC).Append("; dokup jedzenia z gminy ").Append(w.GmFood).Append(")")
              .Append(", wydatek gry w konsumpcji: miasta ").Append(gameT).Append(" (").Append(w.Ticks).Append(" tickow), zamki ").Append(gameC).Append(" (").Append(w.TicksC).Append(" tickow), A1 cofnelo calosc (B1 nie obsluzylo) ").Append(w.Fallback)
              .Append(" | dochod ludu miast z zewnatrz i z kas ").Append(e).Append(": wojsko ").Append(army).Append(" (zycie ").Append(w.Earn[KLife]).Append(", karawany ").Append(w.Earn[KLifeCaravan])
              .Append(", bandy ").Append(w.Earn[KLifeBand]).Append(", zalogi ").Append(w.Earn[KGarrison]).Append("), dwor ").Append(w.Earn[KCourt])
              .Append(", budowy ").Append(w.Earn[KBuildLabour]).Append(", kopalnie ").Append(w.Earn[KMine]).Append(", place warsztatow ").Append(w.Earn[KCraftPaid]).Append(" (z kiesy gracza ").Append(w.FromTown).Append(")")
              .Append(", 5% zaworu ").Append(w.ValveT).Append(", w tym praca niewolnych do wlascicieli ").Append(w.SlaveW)
              .Append("; dzis z kas: robocizna ").Append(d.LabT).Append(SkupText(d.LabS, d.Skup)).Append(", obrot P ").Append(d.TurnT).Append(", doplata do 35% ").Append(d.TopT).Append(" (miast przycietych do stanu kasy ").Append(d.Cut).Append(")")
              .Append(" | wewnatrz ludu: czynsz ").Append(d.Rent).Append(", leczenie ").Append(d.Heal).Append(", sluzba ").Append(d.Serv).Append(", place z nadwyzki R/P ").Append(d.SurpWage)
              .Append(", roboty gminy (place) ").Append(d.GmWage).Append(WorksRoute ? " (P2: place robot gminy w linii Roboty ludu)" : "")
              .Append(" | podatki panu: W ").Append(d.TaxW).Append(" / R ").Append(d.TaxR).Append(" / P ").Append(d.TaxP).Append(", cech ").Append(d.Guild).Append(", farma gmin ").Append(d.Farm)
              .Append(", wstrzymane w oblezeniu ").Append(d.Held).Append(", bez zywego pana (do gminy) ").Append(d.NoLord)
              .Append(" | kiesy ").Append(d.KW + d.KR + d.KP).Append(" (W ").Append(d.KW).Append(" / R ").Append(d.KR).Append(" / P ").Append(d.KP).Append("), gminy ").Append(d.KG)
              .Append(", zapas dni W ").Append(F1(d.NW > 0 ? d.KW / d.NW : double.NaN)).Append(" / R ").Append(F1(d.NR > 0 ? d.KR / d.NR : double.NaN)).Append(" / P ").Append(F1(d.NP > 0 ? d.KP / d.NP : double.NaN))
              .Append(", lokata (kiesa W ponad zapas dni) ").Append(N0(d.Lokata)).Append(", oferta nadwyzki na jutro ").Append(d.OffT)
              .Append(" (tempo ").Append(F2(SpendRate(Settings.Current))).Append("/d ponad zapas)").Append(LadderOn ? " (P3: oferta W 1/60, R i P 0.3/60 - linia Zbytki)" : "")
              .Append(" | stac jedzenie W/R/P ").Append(Pct(d.AFW, d.NFW)).Append(" / ").Append(Pct(d.AFR, d.NFR)).Append(" / ").Append(Pct(d.AFP, d.NFP))
              .Append(", pelny W/R/P ").Append(Pct(d.AW, d.NW)).Append(" / ").Append(Pct(d.AR, d.NR)).Append(" / ").Append(Pct(d.AP, d.NP))
              .Append(" (swiat ").Append(Pct(d.AW + d.AR + d.AP, d.NW + d.NR + d.NP)).Append(")")   // recenzja B1 (1-6): prog B1-T8 "swiat >= 98%" (waga potrzeb)
              .Append(", miast ze stac jedzenie < 95%: ").Append(d.NFood).Append(", pelny < 95%: ").Append(d.NFull)
              .Append(" | podzamcze ").Append(d.Castles).Append(": ludzie ").Append(N0(d.CHeads)).Append(", kiesa ").Append(d.KC).Append(", dochod ").Append(w.EarnC)
              .Append(" (15% zaworu ").Append(w.ValveC).Append("), robocizna z kas zamkow ").Append(d.LabC).Append(", zaplacone ").Append(w.PaidC).Append(" (pokryte dworem ").Append(w.Court)
              .Append("), kredyt dworu ").Append(d.CCr).Append(" (przyznany ").Append(w.CourtGrant).Append("; zamkow bez kredytu ").Append(d.CNoCourt).Append("), stac ").Append(P1(d.CAff, d.CNeed))
              .Append(", wydatek gry zamkow ").Append(N0(d.CWg))
              .Append(" | wsie ").Append(d.Villages).Append(": zakupy ").Append(d.VBuy).Append(" z ").Append(N0(d.VNeed)).Append(" (").Append(P1(d.VBuy, d.VNeed)).Append("; chciane z nadwyzka ").Append(N0(d.VWant))
              .Append("; sztuk ").Append(d.VPieces).Append(", wsi bez towaru ").Append(d.VNoGoods).Append(", bez miasta targowego ").Append(d.NoTrade).Append(", nieskalibrowanych ").Append(d.VNoCal).Append(")")
              .Append(", oplaty stale ").Append(w.VFee).Append(" (").Append(w.VFeeN).Append(" wsi)")
              .Append(" | zapasy z nadwyzki dzis ").Append(d.Seed).Append(d.Orphan > 0 ? ", kiesy osad nieznanych do kasy " + d.Orphan : "")
              // P1: nowy rodzaj wydatku kies (w kontroli nizej siedzi w "wydatki") - danina z oszczednosci do skarbcow krolestw; kiesa ponad zapas dni przed / po daninie
              .Append(" | danina z oszczednosci do korony (P1) ").Append(d.Aid[0] + d.Aid[1] + d.Aid[2] + d.Aid[3]).Append(" (W ").Append(d.Aid[0]).Append(" / R ").Append(d.Aid[1])
              .Append(" / P ").Append(d.Aid[2]).Append("; podzamcza ").Append(d.Aid[3]).Append("), kiesa ponad zapas dni przed / po: W ").Append(N0(d.Ov0[0])).Append(" / ").Append(N0(d.Ov1[0]))
              .Append(", R ").Append(N0(d.Ov0[1])).Append(" / ").Append(N0(d.Ov1[1])).Append(", P ").Append(N0(d.Ov0[2])).Append(" / ").Append(N0(d.Ov1[2]))
              .Append(", podzamcza ").Append(N0(d.Ov0[3])).Append(" / ").Append(N0(d.Ov1[3]))
              .Append(" | kontrola: zmiana kies ").Append(_lastDStock).Append(" = wplywy ").Append(_lastIn).Append(" - wydatki ").Append(_lastOut).Append(", r = ").Append(double.IsNaN(_lastR) ? "-" : N0(_lastR));
            _lineFolk = sb.ToString();
            _lineFolkPartial = partial;
            double sale = d.BaseFolk + d.BaseVill + d.BaseLife + d.BaseCourt + d.BaseSale + d.BaseCar + d.BaseGm;
            _lineLab = "Podstawa robocizny (E5/B1): dzien " + day + " | sprzedaz kupcow " + N0(sale) + ": towar ludu " + N0(d.BaseFolk) + ", wsie " + N0(d.BaseVill) + ", zolnierze i zalogi (75%) " + N0(d.BaseLife)
                       + ", dwor (80%) " + N0(d.BaseCourt) + ", sprzet AI, odziez, budowy, zamki (Sale) " + N0(d.BaseSale) + ", karawany i gracz " + N0(d.BaseCar) + ", gmina " + N0(d.BaseGm)
                       + " | robocizna " + d.LabT + " (jedzenie " + N0(d.LabF) + " / towar " + N0(d.LabG) + " / bron " + N0(d.LabA) + " przed przycieciem do kas), obrot P " + d.TurnT
                       + ", doplata do 35% " + d.TopT + " | zamki: robocizna " + d.LabC + ".";
            _lineRights = "Prawa panskie (E5/B1): dzien " + day + " | od zarobku ludu miast " + (d.TaxW + d.TaxR + d.TaxP) + " (W " + d.TaxW + " / R " + d.TaxR + " / P " + d.TaxP + "), podzamcza " + d.TaxC
                          + ", farma gmin " + d.Farm + ", oplaty stale wsi " + w.VFee + " (" + w.VFeeN + " wsi); gracz " + d.Player + "; wstrzymane w oblezeniu " + d.Held
                          + "; bez zywego pana do gminy " + d.NoLord + " (" + d.NoLordN + ") | podatek wsi BK x VillageLordScale + VillageLordExtraShare utargu: z " + w.VTaxBase + " zostalo wsiom " + w.VTaxCut
                          + " (po odjeciu dopisku pana z utargu " + w.VTaxAdd + ").";
            _lineTithe = TitheLine(day, d);
            _lineR14 = "Zadowolenie ludu (R14): dzien " + day + " | korekty " + (w.R14Food + w.R14Rest) + " (Food " + w.R14Food + " / reszta " + w.R14Rest + "), BK sam " + w.R14Bk.ToString("0.000", Inv)
                       + " (dodatnia delta BK zastapiona); osad z Afford < 0.98 na jutro: jedzenie " + CountAff(true) + ", reszta " + CountAff(false) + ".";
            if (csvDay)
            {
                var csv = new StringBuilder(64 * 1024);
                foreach (var f in _f.Values)
                {
                    if (f.Kind > 1 || !f.Cal) continue;
                    try { CsvRowLive(csv, day, f); } catch (Exception x) { Stumble("CsvLive", x); }
                }
                if (csv.Length > 0) { try { Log.Csv("kiesa-ludu.csv", CsvHead, csv.ToString()); } catch (Exception x) { Stumble("Csv", x); } }
            }
            foreach (var f in _f.Values) { f.LDayPaid = f.LDayUnpaid = f.LDayCourt = f.LDayIn = f.LDayOut = 0; }
        }

        private static int CountAff(bool food)
        {
            int n = 0;
            foreach (var f in _f.Values) if (f.Kind < 2 && f.FacOk && (food ? f.AffFood : f.AffRest) < 0.98) n++;
            return n;
        }

        // ============================================================ E5/C4 (205, PLAN 5.13): zdanie "czy ludzi stac na chleb" w oknie People of this land
        /// <summary>E5/C4: zdanie do okna "People of this land" (PeopleMenu) dla osady, w ktorej stoi gracz - liczby kiesy czynnej z ostatniej doby tej osady:
        /// miasto i zamek DAffF / DAff (te same co kolumny stac_jedzenie / stac_pelny kiesa-ludu.csv i skladniki sum "stac jedzenie" / "pelny" / "stac" linii "Kiesa ludu:"),
        /// wies - zakupy tej doby / potrzeba (skladniki "wsie: zakupy X z Y"). Tylko odczyt: bez Get (nie tworzy wpisu), bez zlota i bez stanu.
        /// null - bez zdania (kiesa nieczynna, zawor zamkow wylaczony, osada spoza ksiegi, wies bez miasta targowego). log - linia do logu z fragmentem linii
        /// "Kiesa ludu:" tej samej doby (sprawdz_logi E5/C4-1 porownuje go z linia doby).</summary>
        internal static string BreadLine(Settlement st, out string log)
        {
            log = null;
            if (st == null || !Live) return null;
            Folk f;
            if (!_f.TryGetValue(st, out f) || f == null || f.Kind < 0 || f.Kind > 2) return null;
            if (f.Kind == 1 && !CastleLive) return null;               // podzamcze nie liczone (zawor 110 wylaczony) - nie ma czego pokazac
            // dzien ostatniej doby kiesy (ten sam co w linii "Kiesa ludu: dzien N" tej doby); liczby osady swieze, gdy policzone w tej dobie, a doba nie starsza niz wczoraj
            int now = (int)CampaignTime.Now.ToDays - 1, day = _liveDay >= 0 ? _liveDay : now;
            string head = "PeopleMenu (E5/C4): dzien " + day + " | " + Clean(st.Name != null ? st.Name.ToString() : st.StringId) + " (" + st.StringId + ", "
                          + (f.Kind == 0 ? "miasto" : f.Kind == 1 ? "zamek" : "wies") + ")";
            var d = _ld;
            if (d == null || _liveDay < 0 || f.ADay != _liveDay || now - _liveDay > 1)
            {
                log = head + " | brak liczb tej doby (ostatnie liczenie osady: " + (f.ADay >= 0 ? "dzien " + f.ADay : "brak w tej sesji") + ") - zdanie 'not reckoned yet'";
                return "Bread: the people's purses have not been reckoned yet - look again tomorrow.";
            }
            if (f.Kind == 2)
            {
                var vil = st.Village;
                var mk = vil != null ? (vil.TradeBound ?? vil.Bound) : null;    // to samo miasto targowe co Villages()
                if (mk == null || mk.Town == null || !(f.DVNeed > 0)) { log = head + " | bez zdania (brak miasta targowego albo potrzeby zakupow 0)"; return null; }
                int v = In100(f.DVBuy / f.DVNeed);
                log = head + " | wies: zakupy " + f.DVBuy + " z " + F2(f.DVNeed) + " (w oknie " + v + " na 100, targ " + Clean(mk.Name != null ? mk.Name.ToString() : mk.StringId)
                      + ") | w linii Kiesa ludu: wsie " + d.Villages + ": zakupy " + d.VBuy + " z " + N0(d.VNeed) + " (" + P1(d.VBuy, d.VNeed);
                string market = mk.Name != null ? mk.Name.ToString() : "the market";
                return "Bread: the villagers grow their own; at the market of " + market + " they bought "
                       + (v >= 100 ? "all the salt, tools, cloth, pots and ale they need." : v + " parts in 100 of the salt, tools, cloth, pots and ale they need.");
            }
            int a = In100(f.DAffF), b = In100(f.DAff);
            string who = f.Kind == 0 ? "townsfolk" : "castle folk";
            log = head + " | stac jedzenie " + F2(f.DAffF) + ", pelny " + F2(f.DAff) + " (w oknie " + a + " / " + b + " na 100; kolumny stac_jedzenie / stac_pelny kiesa-ludu.csv) | w linii Kiesa ludu: "
                  + (f.Kind == 0
                     ? "stac jedzenie W/R/P " + Pct(d.AFW, d.NFW) + " / " + Pct(d.AFR, d.NFR) + " / " + Pct(d.AFP, d.NFP)
                       + ", pelny W/R/P " + Pct(d.AW, d.NW) + " / " + Pct(d.AR, d.NR) + " / " + Pct(d.AP, d.NP) + " (swiat " + Pct(d.AW + d.AR + d.AP, d.NW + d.NR + d.NP) + ")"
                     : "zamkow bez kredytu " + d.CNoCourt + "), stac " + P1(d.CAff, d.CNeed));
            return "Bread: " + a + " in 100 " + who + " can afford their daily bread, " + b + " in 100 their full basket.";
        }

        /// <summary>E5/C4: udzial 0..1 jako "ile na 100" - w dol (99.6% to jeszcze nie wszyscy), NaN - 0.</summary>
        private static int In100(double x) { return double.IsNaN(x) ? 0 : (int)Math.Max(0, Math.Min(100, Math.Floor(100.0 * x + 1e-9))); }

        private static void CsvRowLive(StringBuilder sb, int day, Folk f)
        {
            var st = f.St;
            var k = st.OwnerClan != null ? st.OwnerClan.Kingdom : null;
            double people = f.Kind == 1 ? CastleHeads(f) : f.DBook;     // GL-2: ludzie ksiegi (przy glowach wedlug rynku - nie PeopleCal x DR); podzamcze - glowy
            long gold = st.SettlementComponent != null ? st.SettlementComponent.Gold : 0;
            long res = f.Kind == 0 ? Seal31.TownReserve(st.Town) : CastlePurse.Reserve(st.Town);
            sb.Append(day).Append(';').Append(day + 1).Append(';').Append(st.StringId).Append(';').Append(Clean(st.Name != null ? st.Name.ToString() : st.StringId)).Append(';')
              .Append(f.Kind == 0 ? "miasto" : "zamek").Append(';').Append(Clean(k != null ? k.Name.ToString() : "-")).Append(';')
              .Append(st.Culture != null ? st.Culture.StringId : "").Append(';').Append("-").Append(';')
              .Append(N0(people)).Append(';').Append(N0(f.HW + f.HR + f.HP + f.HS)).Append(';').Append(f.DR.ToString("0.000", Inv)).Append(';')
              .Append(N0(f.HW * f.DR)).Append(';').Append(N0(f.HR * f.DR)).Append(';').Append(N0(f.HP * f.DR)).Append(';').Append(N0(f.HS * f.DR)).Append(';')
              .Append(N0(f.DB)).Append(';').Append(N0(f.DWg)).Append(';').Append(N0(f.SpendFood)).Append(';').Append(F2(f.DM)).Append(';')
              .Append(f.LDayIn).Append(';').Append(f.LDayOut).Append(';').Append(f.KW).Append(';').Append(f.KR).Append(';').Append(f.KP).Append(';')
              .Append(F2(f.DAffF)).Append(';').Append(F2(f.DAff)).Append(';').Append(gold).Append(';').Append(res).Append(';').Append(f.LDayUnpaid).Append(Environment.NewLine);
        }

        /// <summary>TownFolk.Lines: linie B1 tej doby (stan przy zmianie, zapasy startowe, zwrot, robocizna, prawa panskie, R14) - zeruje.</summary>
        private static List<string> TakeLiveLines()
        {
            var l = new List<string>();
            foreach (var x in new[] { _lineState, _lineSeed, _lineRet, _lineLab, _lineRights, _lineR14, _lineC2, _lineTithe }) if (x != null) l.Add(x);
            _lineState = _lineSeed = _lineRet = _lineLab = _lineRights = _lineR14 = _lineTithe = _lineC2 = null;
            TakeP1Lines(l);   // P1: linie pomiarow obiegu kiesy tej doby
            return l;
        }
    }
}
