using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// E5/0 (195) - POMIAR ETAPU 5: KIESA LUDU "NA SUCHO" (docs/etap5/PROJEKT-ETAP5-KIESA-LUDU-KASY-MIAST-2026-10-11.md rozdz. 3 "E5/0",
    /// plan kodu docs/etap5/PLAN-KODU-E50.md). Tylko log - gra bez zmian: zadne zloto, ludzie, towar, cena ani zadowolenie nie sa ruszane,
    /// zadna latka nie zwraca false i nie pisze wyniku, zadnych pozycji ksiegi pieniadza (MoneyLedger) - wlasne linie logu.
    ///
    /// Kiesa ludu liczona co dobe z PRAWDZIWYCH przeplywow (te same miejsca, ktore B1 zamieni na prawdziwe przelewy): zycie zolnierzy, ludzi karawan
    /// i band w miescie, zold zalog wydany na miejscu, dwor, place budow i kopaln, place rzemiosla (35% wartosci wyrobow), robocizna handlu i obrot
    /// patrycjatu od sprzedazy kupcow, karawany i gracz w handlu z miastem; wsie: 65% utargu taborow, przelewy gry, sakiewki zwolnionych.
    /// Model = rachunek sim2 (sim_kiesa2.py :160-205, :271-283, :430-480) przeniesiony z krolestwa na osade; stawki P0 jako stale ponizej.
    /// Stan tylko w pamieci sesji (bez zapisu): po wczytaniu nowa kalibracja D1 i zapasy startowe (linia sesji).
    ///
    /// Okno doby: przeplywy od poprzedniego TownFolk.Daily do biezacego (tick osad gry jest rozlozony na cala dobe; przeplywy naszego bloku po
    /// Daily wchodza do okna nastepnej doby). Pierwsze okno sesji niepelne - dopisek w linii, narzedzie je pomija.
    /// Wyjatki liczone per miejsce (pierwszy do pliku) - bez globalnych wylacznikow (CLAUDE.md 7).
    /// </summary>
    internal static partial class TownFolk
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        // ------------------------------------------------------------ rodzaje przeplywow do ludu (Note) - miejsca wpiec: plan E5/0 rozdz. 2.2
        internal const int KLife = 0, KLifeCaravan = 1, KLifeBand = 2, KGarrison = 3, KCourt = 4, KBuildLabour = 5, KMine = 6,
                           KCraftPaid = 7, KShopKeep = 8, KCraftFair = 9, KReleased = 10;
        private const int Kinds = 11;
        // sprzedaz kupcow (Sale) - mieszanki towaru: jedzenie / towar / bron-konie-zbytki
        internal const int MixBuild = 0, MixGoods = 1, MixGear = 2, MixCloth = 3, MixFood = 4;
        private const int Mixes = 5;
        private static readonly double[][] MixOf = { new[] { 0.0, 1.0, 0.0 }, new[] { 0.0, 1.0, 0.0 }, new[] { 0.0, 0.0, 1.0 }, new[] { 0.0, 1.0, 0.0 }, new[] { 1.0, 0.0, 0.0 } };
        // zrodla platnosci kas miast z pasa ponizej zapasu kupcow (OnTownCoin / NoteCartPaid / OnGold)
        internal const int BOurs = 0, BCarts = 1, BCaravans = 2, BOther = 3, BBack = 4;   // BBack: nasze zwroty i likwidacje z calej kasy (bez reguly A1 - RecruitKit, tabory zalog, zwrot 171, wedrowcy 36b; scalenie porcji A)
        private const int BSrc = 5;
        // pobory z kas miast (NoteTake) - udzial zysku kupcow zabierany przez pana i korone (Jeff 11.10 16:10, projekt rozdz. 6 pkt 2)
        internal const int TRent = 0, TLevy = 1, TCustoms = 2, TMint = 3, TDuty = 4, TWarTax = 5, TTolls = 6;
        private const int Takes = 7;
        private static readonly string[] TakeName = { "renta (zawor panu)", "danina wojenna", "clo z kasy", "mennica", "Crown Duty", "War Tax", "Road Tolls" };

        // ------------------------------------------------------------ stale KL / sim2 P0 (sim_kiesa2.py :50-80) - klucze stawek dopiero w B1
        private const double BW = 0.27, BR = 0.50, BP = 1.46, BS = 0.20, BC = 0.18, BV = 0.07;      // koszyk towaru na glowe na dobe (indeks cen 1.0)
        private const double FdW = 0.80, FdR = 0.70, FdP = 0.45, FdC = 0.80;                          // jedzenie w koszyku
        private const double RentW = 0.08, RentR = 0.13, RentToP = 0.5;                              // czynsz: polowa notable, polowa patrycjat
        private const double HlW = 0.01, HlR = 0.02, HlP = 0.02, ServP = 0.06;                       // leczenie (do R), sluzba patrycjatu (do W)
        private const double KeepW = 1 - RentW - HlW, KeepR = 1 - RentR - HlR, KeepP = 1 - HlP - ServP;
        private const double RdW = 10, RdR = 21, RdP = 30, RdC = 10, RdV = 15;                       // zapas dni (i zapas startowy R5)
        private const double SurplusRate = 0.07, SurpWage = 0.7;                                     // nadwyzka ponad zapas 7%/d; R, P: 70% place
        private const double TaxW = 0.01, TaxR = 0.02, TaxP = 0.03, TaxC = 0.03, GuildR = 0.01, Excise = 0.02;   // pan, cech R, akcyza (R11 v2)
        private const double WarS = 0.03, WarW = 0.5, WarR = 1.0, WarP = 1.5, WarC = 1.0;            // R15 "na probe": s = 3%, tylko w wojnie
        private const double GminaOut = 0.07, GFarm = 1.0 / 3.0, GLab = 0.75;                        // gmina: 7%/d, 1/3 farma panu, roboty 75% place
        private const double RhFood = 0.20, RhMid = 0.10, RhArms = 0.04, LabW = 0.9, TurnP = 0.03;   // robocizna handlu, podzial W90/R10, obrot P 3%
        private const double OwnR = 0.4, OwnP = 0.6;                                                 // wlasciciele niewolnych (krolestwa niewolnicze)
        private const double VillShare = 0.65, VillFeeYear = 4.0, VillLay = 0.015;                   // R13 wies 65% utargu; oplaty 4 zl/rok; danina 1.5%/d
        private const double LifeShare = 0.25, CourtShare = 0.20, CraftShare = 0.35, CastleValve = 0.15, CaravanLab = 0.10;
        private const double RateNadW = 0.5 * RhFood + 0.5 * RhMid, RateNadRP = 0.7 * RhMid + 0.3 * RhArms, RateNotable = 0.5 * RhMid + 0.5 * RhArms;
        private const double RatePubl = RhMid, RateVill = 0.2 * RhFood + 0.8 * RhMid, RateCastle = 0.8 * RhFood + 0.2 * RhMid, RateCourt = 0.5 * RhFood + 0.2 * RhMid + 0.3 * RhArms;
        private const int YearDays = 364;
        // E5/B1 (KL L8, plan pkt 0.13): niewolni wedlug kultury osady z jednej listy I1/H3 (LosersFlee.SlaveCulture - z Qohorem, decyzja Jeffa 09.10 05:15),
        // zamiast nazw krolestw E5/0 (bez Qohoru); ta sama regula w trybie na sucho i w kiesie czynnej

        // ------------------------------------------------------------ stan osady (pamiec sesji)
        private sealed class Folk
        {
            public Settlement St; public int Kind;                    // 0 miasto, 1 zamek, 2 wies
            public bool Cal, NoBk, Slave;
            public double HW, HR, HP, HS, PeopleCal;                  // ludzie BK z kalibracji (zamek: HW = podzamcze; wies: HW wolni chlopi, HR rzemieslnicy)
            public double LW, LR, LP, Gmina, Notab;                   // kiesy na sucho (zamek: LW = podzamcze; wies: LW = kiesa wsi)
            public readonly double[] Lab = new double[2];             // robocizna na jutro (W, R; zamek: podzamcze w [0])
            public double Turn;                                       // obrot P na jutro
            public readonly double[] Inner = new double[3];           // czynsz, leczenie, sluzba, praca u bogatszych - na jutro (W, R, P)
            public readonly double[] Gm = new double[2];              // roboty gminy na jutro (W, R)
            public readonly double[] Win = new double[Kinds];         // okno doby (Note)
            public readonly double[] Sale = new double[Mixes];        // okno doby (Sale)
            public double ProdV, CarBuy, CarSell, VTrans, VHanded;    // wartosc wyrobow, karawany/gracz, przelewy wsi, utarg wsi
            public int Policy = 4;                                    // polityka podatkowa BK (0 niska, 1 zwykla, 2 wysoka, 3 zwolnienie, 4 nieznana)
            // nawias konsumpcji (okno doby)
            public double Idx = 1, G0, G1, SpendGame, SpendFood, ConsumedBk, ConsGold;
            public readonly double[] Grp = new double[3];
            public int FoodSold, Rows;
            public object Econ; public int EconDay = -1;
            public readonly float[] Sat0 = new float[4];
            public bool Sat0Ok;
            // zamek: jedzenie zuzyte (pierscien 7 dob)
            public readonly int[] Food7 = new int[7]; public int Food7Head, Food7Fill;
            // udzial zysku kupcow (miasta, okno Daily - Daily)
            public long Gold0 = long.MinValue; public double TakeWin, RegIn, RegOut;
            public readonly double[] RTake = new double[28], RIn = new double[28]; public int RHead, RFill;
            public double DTake = double.NaN, DIn = double.NaN;       // ostatnie okno (CSV)
            public int Streak;                                        // zaulki: doby z rzedu ponizej zapasu kupcow
            // liczby ostatniej doby (CSV)
            public double DB, DWg, DM = double.NaN, DInc, DOut, DAff = 1, DAffF = 1, DUnpaid, DR = 1;
            public double DBook;                                      // GL-2: ludzie ksiegi osady ostatniej doby (linia "ludzie ksiegi", CSV ludzie_ksiegi) - glowy moga isc od ludzi rynku
            // ---- E5/B1 (kiesa czynna, TownFolk.Live.cs): prawdziwe zloto w kiesach - long, przelewy co do zlotowki
            public long KW, KR, KP, KG;                               // miasto: kiesy W/R/P i gmina; zamek: KW = kiesa podzamcza (zapis arm_townfolk)
            public long CourtCr;                                      // zamek: kredyt dworu (kuchnia pana) - liczba, bez zlota (zapis)
            public readonly long[] EarnWin = new long[3];             // zarobek okna klas - podstawa podatkow panu (zamek: [0])
            public readonly long[] PaidG = new long[3];               // zaplacone za towar w oknie (czynsz, leczenie, sluzba - w dobie)
            public readonly long[] Offer = new long[3];               // nadwyzka oferowana na towar jutro (nie przelew - budzet "lepszych" kategorii)
            public long PaidKeep, VillWin, GmMatWin;                  // okno: zaplata ludu do kasy, zakupy wsi w tym miescie, material robot gminy (podstawa robocizny)
            public readonly double[] Need = new double[3];            // potrzeba dzienna klas (towar / keep) z kotwicy - zapas dni
            public readonly double[] Share = new double[3];           // udzialy klas w zaplacie za towar (towar x Afford + oferta)
            public double FacFood = 1, FacRest = 1, AffFood = 1, AffRest = 1, MAnc = 1; public bool FacOk;
            public double FacFoodUsed = 1, FacRestUsed = 1, OfferFood, OfferRest;   // nawias dnia: czynniki uzyte i oferta dopisana (przewidywanie bez kotwicy)
            public double PredFood, PredRest;                         // przewidywany wydatek gry bez kotwicy (jedzenie / reszta)
            public readonly double[] AffC = new double[3], AffFC = new double[3];   // stac pelny / jedzenie klasy (linia, CSV)
            public double VFeeAcc;                                    // wies: oplata stala - reszta ulamka (bez zaokraglania w dol co dobe)
            public bool BkCal;                                        // R5: klasy BK odczytane przed kalibracja ksiegi (faza a D1)
            public long LDayPaid, LDayUnpaid, LDayCourt, LDayIn, LDayOut;   // ostatnia doba (CSV)
            public double G0a;                                        // budzet gry po kotwicy i ofercie nadwyzki (okno - linia)
            public int HungerDays;                                    // E5/C2: doby z rzedu z Afford jedzenia na jutro < 0.95 (zapis arm_townfolk, pole 17)
            public bool C2Hit;                                        // E5/C2: korekta jedzenia R14 w oknie doby (kontrola "obie kary tej samej doby")
            public int ADay = -1;                                     // E5/C4: doba ostatniego liczenia kiesy czynnej tej osady (DAff/DAffF albo zakupy wsi) - okno People of this land; bez zapisu
            public double DVNeed; public long DVBuy;                  // E5/C4: wies - potrzeba zakupow i zakupy ostatniej doby (skladniki "wsie: zakupy X z Y" linii Kiesa ludu); bez zapisu
            // ---- P1 (obieg kiesy ludu, TownFolk.Savings.cs): liczby ostatniej doby i okna pomiaru - pamiec sesji, bez zapisu
            public long DAid;                                         // danina z oszczednosci oddana koronie w ostatniej dobie (wszystkie klasy)
            public double DOver;                                      // kiesa ponad zapas dni po daninie (suma klas z nadwyzka) - zdanie "Savings", pomiary
            public readonly double[] DOvC = new double[3];            // to samo na klase (W, R, P; zamek [0])
            public long Aid7; public double Of7X, Of7B;               // okno pomiaru (do 7 dob): danina, oferta nadwyzki dopisana do budzetu, kupione z oferty
            public readonly long[] EarnK = new long[16];              // okno pomiaru: zarobek ludu osady wedlug rodzaju (Credit; K* 0-10, E* 11-15)
            public int BQ, BMat, LuxShops;                            // pomiar tygodnia: czolo kolejki budow (0 brak, 1 cywilna, 2 wojskowa), sztuki materialu na polce targu, warsztaty zbytkow
        }

        private static readonly Dictionary<Settlement, Folk> _f = new Dictionary<Settlement, Folk>();
        private static readonly List<Folk> _fk = new List<Folk>();   // E5/B1: miasta i zamki (posiadacze kies - sumy dla ksiegi pieniadza bez przegladu wsi)
        private static bool _calDone;
        private static int _sessionDay = -1, _calDay = -1, _main = -1;
        private static string _lineSession, _lineFolk;
        private static bool _lineFolkPartial;
        private static int _stumbles, _stumbleSess, _offThread;
        private static readonly HashSet<string> _err = new HashSet<string>();
        private static long _patchTicks, _dayTicks;
        // gotowka wsi wedlug polityki (okno ksiegi pieniadza - zerowane w Lines): [polityka, 0 utarg 1 licznik pana 2 majatki 3 kiesa], przelewy, sakiewki
        private static readonly long[,] _vc = new long[5, 4];
        private static readonly long[] _vcTrans = new long[5], _vcRel = new long[5];

        internal static bool On { get { var s = Settings.Current; return s != null && s.TownFolkMode >= 1 && s.LogEnabled; } }
        /// <summary>Pomiar w logu (dawne Live E5/0): TownFolkMode >= 1, LogEnabled, po GameStarted - nawiasy bitwy i pasma notabli.</summary>
        private static bool Measure { get { var c = Campaign.Current; return On && c != null && c.GameStarted; } }
        /// <summary>E5/B1: okna przeplywow (Note, Sale, wyroby, karawany) i nawias konsumpcji biegna przy pomiarze ALBO kiesie czynnej (kiesa nie wymaga logu).</summary>
        private static bool Run { get { var c = Campaign.Current; return (On || LiveKey) && c != null && c.GameStarted; } }

        /// <summary>Z konstruktora ArmouryBehavior: nowa sesja (stan tylko w pamieci).</summary>
        internal static void Reset()
        {
            try
            {
                _f.Clear(); _fk.Clear(); _calDone = false; _sessionDay = -1; _calDay = -1;
                _lineSession = null; _lineFolk = null; _lineFolkPartial = false;
                _stumbles = _stumbleSess = _offThread = 0; _err.Clear();
                _patchTicks = _dayTicks = 0;
                Array.Clear(_vc, 0, _vc.Length); Array.Clear(_vcTrans, 0, _vcTrans.Length); Array.Clear(_vcRel, 0, _vcRel.Length);
                ResetProbe();
                _hDay = int.MinValue;   // GL-2/GL-3: srednie glow swiata od nowa (TownFolk.Market.cs)
                ResetLive();   // E5/B1: kiesy czynne, flagi zapasow startowych, liczniki (zapis wczytuje Import po konstruktorze)
            }
            catch { }
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++; _stumbleSess++;
            try { if (_err.Add(where)) Log.Error("TownFolk." + where, e); } catch { }
        }

        private static bool OffMain()
        {
            if (Environment.CurrentManagedThreadId == _main) return false;
            _offThread++;
            return true;
        }

        private static Folk Get(Settlement st)
        {
            if (st == null) return null;
            Folk f;
            if (_f.TryGetValue(st, out f)) return f;
            int kind = st.IsTown ? 0 : (st.IsCastle ? 1 : (st.IsVillage ? 2 : -1));
            if (kind < 0 || (kind < 2 && st.Town == null) || (kind == 2 && st.Village == null)) return null;
            f = new Folk { St = st, Kind = kind };
            _f[st] = f;
            if (kind < 2) _fk.Add(f);
            return f;
        }

        private static int Pol(Folk f) { return f != null && f.Policy >= 0 && f.Policy < 4 ? f.Policy : 4; }

        // ============================================================ wpiecia z zywego kodu (kazde w try u wolajacego; tu wlasny try - nie rzucaja)
        /// <summary>Przeplyw do ludu osady (kwota juz przelana przez gre/nasz modul - tylko licznik okna).</summary>
        internal static void Note(Settlement st, int amount, int kind)
        {
            try
            {
                if (amount <= 0 || kind < 0 || kind >= Kinds || st == null || !Run || OffMain()) return;
                long t = Stopwatch.GetTimestamp();
                var f = Get(st);
                if (f != null)
                {
                    f.Win[kind] += amount;
                    if (kind == KReleased && f.Kind == 2) _vcRel[Pol(f)] += amount;
                }
                _patchTicks += Stopwatch.GetTimestamp() - t;
            }
            catch (Exception e) { Stumble("Note", e); }
        }

        /// <summary>Sprzedaz kupcow osady (towar zdjety z polki i zaplacony) - podstawa robocizny handlu i obrotu P.</summary>
        internal static void Sale(Settlement st, int amount, int mix)
        {
            try
            {
                if (amount <= 0 || mix < 0 || mix >= Mixes || st == null || !Run || OffMain()) return;
                long t = Stopwatch.GetTimestamp();
                var f = Get(st);
                if (f != null && f.Kind < 2) f.Sale[mix] += amount;
                _patchTicks += Stopwatch.GetTimestamp() - t;
            }
            catch (Exception e) { Stumble("Sale", e); }
        }

        /// <summary>CampaignEvents.OnItemProducedEvent: wyroby warsztatow miast i zamkow (gra, BK, nasze warsztaty) - wartosc po cenie targu (KL L7).</summary>
        internal static void OnItemProduced(ItemObject item, Settlement st, int count)
        {
            try
            {
                if (item == null || st == null || count <= 0 || !(st.IsTown || st.IsCastle) || st.Town == null || !Run || OffMain()) return;
                long t = Stopwatch.GetTimestamp();
                var f = Get(st);
                if (f != null) f.ProdV += (double)count * Math.Max(0, st.Town.MarketData.GetPrice(item));
                _patchTicks += Stopwatch.GetTimestamp() - t;
            }
            catch (Exception e) { Stumble("OnItemProduced", e); }
        }

        /// <summary>
        /// Z MoneyLedger.OnGoldTraded (po normalizacji kierunku; zdarzenie gra wysyla PO przelewie): (a) karawana albo gracz w handlu z miastem
        /// poza oknem flagowym, (b) przelewy gry do/z kiesy wsi (poza naszym tickiem - definicja ksiegi), (c) czesc platnosci kasy miasta ponizej
        /// zapasu kupcow (poza naszym tickiem - pobory bloku licza sie osobno), (d) w nawiasach bitwy i pasma notabli - NoteBracketGold.
        /// </summary>
        internal static void OnGold(Hero gh, PartyBase gp, Hero rh, PartyBase rp, int a, bool gNone, bool rNone, bool inBlock)
        {
            try
            {
                if (a <= 0 || !Run || OffMain()) return;
                long t = Stopwatch.GetTimestamp();
                Settlement gs = gh == null && gp != null && gp.IsSettlement ? gp.Settlement : null;
                Settlement rs = rh == null && rp != null && rp.IsSettlement ? rp.Settlement : null;
                if (_btOpen || _ntHero != null) NoteBracketGold(gh, rh, gs, rs, a, gNone, rNone);   // nawiasy bitwy i pasma notabli (recenzja E5/0 (1-1), (1-2))
                if (gs != null && gs.IsTown && rh == null && rp != null && rp.MobileParty != null && rp.MobileParty.IsCaravan) CaravanPaidNote(gs, a);   // P1 (pomiar): kasa miasta zaplacila karawanie (tylko licznik)
                if (!inBlock)
                {
                    if (rs != null && rs.IsVillage) VillTrans(rs, a);
                    if (gs != null && gs.IsVillage) VillTrans(gs, -a);
                }
                if (CirculationWindows.CurrentKind == 0)
                {
                    if (rs != null && rs.IsTown && !gNone && CarOrPlayer(gh, gp)) { var f = Get(rs); if (f != null) f.CarBuy += a; }
                    else if (gs != null && gs.IsTown && !rNone && CarOrPlayer(rh, rp)) { var f = Get(gs); if (f != null) f.CarSell += a; }
                }
                if (gs != null && gs.IsTown && gs.Town != null && !inBlock)
                {
                    long below = Below(gs.Town, a);
                    bool car = rh == null && rp != null && rp.MobileParty != null && rp.MobileParty.IsCaravan;
                    if (car) { _bPaid[BCaravans] += a; _bBelow[BCaravans] += below; }
                    else { _bPaid[BOther] += a; _bBelow[BOther] += below; }
                }
                _patchTicks += Stopwatch.GetTimestamp() - t;
            }
            catch (Exception e) { Stumble("OnGold", e); }
        }

        private static bool CarOrPlayer(Hero h, PartyBase p)
        {
            if (h != null) return h == Hero.MainHero;
            var mp = p != null ? p.MobileParty : null;
            return mp != null && (mp.IsCaravan || mp.IsMainParty);
        }

        private static void VillTrans(Settlement st, int x)
        {
            var f = Get(st);
            if (f == null) return;
            f.VTrans += x;
            _vcTrans[Pol(f)] += x;
        }

        /// <summary>Czesc platnosci kasy miasta ponizej zapasu kupcow (D4): kasa juz po przelewie.</summary>
        private static long Below(Town t, long paid)
        {
            if (t == null || paid <= 0) return 0;
            long r = Seal31.TownReserve(t), g = t.Gold;
            return Math.Max(0, Math.Min(paid, r - g));
        }

        /// <summary>Kasa miasta zaplacila (kwota ujemna jak ChangeGold) nasza sciezka - czesc ponizej zapasu kupcow. Dodatnie kwoty i zamki - nic.</summary>
        internal static void OnTownCoin(Settlement st, int amount, int src)
        {
            try
            {
                if (amount >= 0 || st == null || !st.IsTown || st.Town == null || src < 0 || src >= BSrc || !Run || OffMain()) return;
                long t = Stopwatch.GetTimestamp();
                _bPaid[src] += -(long)amount; _bBelow[src] += Below(st.Town, -(long)amount);
                _patchTicks += Stopwatch.GetTimestamp() - t;
            }
            catch (Exception e) { Stumble("OnTownCoin", e); }
        }

        /// <summary>MoneyLedger.OnAfterEntered (tabor wsi w miescie): ile kasa zaplacila, czesc ponizej zapasu, towar niesprzedany (zaulki - Jeff 16:10).</summary>
        internal static void NoteCartPaid(Settlement st, int paid, int unsold)
        {
            try
            {
                if (st == null || !st.IsTown || st.Town == null || !Run || OffMain()) return;
                if (paid > 0) { _bPaid[BCarts] += paid; _bBelow[BCarts] += Below(st.Town, paid); }
                if (unsold > 0) { _unsold += unsold; _unsoldVisits++; }
            }
            catch (Exception e) { Stumble("NoteCartPaid", e); }
        }

        /// <summary>MoneyLedger.OnAfterEntered (powrot taboru do wsi): te same liczby co "Przeplywy osad" - utarg, licznik pana, majatki, kiesa wsi.</summary>
        internal static void NoteVillageReturn(Settlement home, int handed, int kept, int tax, int estates)
        {
            try
            {
                if (home == null || !home.IsVillage || handed == 0 || !Run || OffMain()) return;
                var f = Get(home);
                if (f == null) return;
                f.VHanded += handed;
                int p = Pol(f);
                _vc[p, 0] += handed; _vc[p, 1] += tax; _vc[p, 2] += estates; _vc[p, 3] += kept;
            }
            catch (Exception e) { Stumble("NoteVillageReturn", e); }
        }

        /// <summary>Pobor z kasy miasta (KingdomTreasury: danina, clo, mennica, Crown Duty, War Tax, Road Tolls) - udzial zysku kupcow i pas ponizej zapasu.</summary>
        internal static void NoteTake(Settlement st, int x, int kind)
        {
            try
            {
                if (x <= 0 || st == null || !st.IsTown || st.Town == null || kind < 0 || kind >= Takes || !Run || OffMain()) return;
                var f = Get(st);
                if (f != null) f.TakeWin += x;
                _take[kind] += x; _takeBelow[kind] += Below(st.Town, x);
            }
            catch (Exception e) { Stumble("NoteTake", e); }
        }

        /// <summary>MoneyLedger.RegulatorPostfix (regulator gry tej osady w jej dziennym ticku) - przelew z niczego / w nicosc do udzialu zysku kupcow.</summary>
        internal static void OnRegulator(Town t, int result)
        {
            try
            {
                if (t == null || !t.IsTown || result == 0 || !Run || OffMain()) return;
                var f = Get(t.Settlement);
                if (f == null) return;
                if (result > 0) f.RegIn += result; else f.RegOut -= result;
            }
            catch (Exception e) { Stumble("OnRegulator", e); }
        }

        // ============================================================ doba: model na sucho (po CastlePurse.Daily, przed BuildFunding)
        // liczniki swiata ostatniej doby modelu (do linii)
        private sealed class Day
        {
            public int Towns, Castles, Villages, MLow, NFood, NFull, NoTrade;
            public double HW, HR, HP, HS, People, B, Wg, Wf, G0, G1, MSum; public int MN; public double MMin = double.NaN;
            public double Y, Y1a, Y1b, Y1c, Y1d, Y2, Y3a, Y3k, Y3b, Y3c, Y3d, Y4a, Y4b, Y5, Y6, V, YSlave;
            public double Z1, Z2, Z3, P1, P2, P3, LW, LR, LP, NW, NR, NP, AW, AR, AP, NFW, NFR, NFP, AFW, AFR, AFP;
            public double A, T1, T2, Gm, GEx, GGuild, GFarm, GPub, Notab;
            public double CHeads, CB, CInc, CGar, CCourt, CBuild, CValve, CGoods, CTax, CL, CNeed, CAff, CWg;
            public double VFree, VInc, VHand, VTrans, VRel, VNeed, VBuy, VExt, VFee, VLay; public List<double> VPurse = new List<double>();
            public double WarT; public int WarK;
            public double Paid, CPaid, VillPaid, LabOut, LabTownNext, ExtIn, LabIn, TaxOut, FarmOut, ClampV;
            public double Spend, Consumed;
            public double TakeW, InW, InW2, Cons, Reg; public int TakeN;
        }
        private static Day _d = new Day();

        internal static void Daily()
        {
            long t0 = Stopwatch.GetTimestamp();
            _lineFolk = null; _lineFolkPartial = false;
            try
            {
                if (Campaign.Current == null) return;
                // E5/B1: kiesa czynna - model z prawdziwymi przelewami (TownFolk.Live.cs); wylaczenie w trakcie - zwrot kies do kas (plan rozdz. 6.3)
                if (Live) { DailyLive(); return; }
                if (AnyPurse()) ReturnAll();
                LiveOffLine();
                if (!On) { ClearWindows(); return; }
                int day = (int)CampaignTime.Now.ToDays - 1;
                bool first = _sessionDay < 0;
                if (first) _sessionDay = day;
                if (!_calDone)
                {
                    if (!PopulationLaw.Calibrated)
                    {
                        _lineFolk = "Kiesa ludu (na sucho): dzien " + day + " | czeka na kalibracje ksiegi ludzi (PopulationLaw) - przeplywy okna pominiete";
                        ClearWindows();
                        return;
                    }
                    Calibrate(day);
                }
                Model(day, first);
            }
            catch (Exception e) { Stumble("Daily", e); ClearWindows(); }
            finally { _dayTicks += Stopwatch.GetTimestamp() - t0; }
        }

        private static void ClearWindows()
        {
            foreach (var f in _f.Values) ClearWindow(f);
        }

        private static void ClearWindow(Folk f)
        {
            Array.Clear(f.Win, 0, Kinds); Array.Clear(f.Sale, 0, Mixes);
            f.ProdV = f.CarBuy = f.CarSell = f.VTrans = f.VHanded = 0;
            f.G0 = f.G1 = f.SpendGame = f.SpendFood = f.ConsumedBk = f.ConsGold = 0; Array.Clear(f.Grp, 0, 3); f.Rows = 0;
            f.TakeWin = f.RegIn = f.RegOut = 0;
        }

        /// <summary>Mnoznik glow BK z kalibracji: GL-2 (a) miasto - wedlug ludzi rynku, GL-3 wies - wedlug ludzi okregu (TownFolk.Market.cs PeopleHeads);
        /// inaczej (klucze wylaczone, Straz, brak skali) dzisiejszy wzor BookScale.</summary>
        private static double Scale(Folk f)
        {
            double r;
            if (PeopleHeads(f, out r)) return r;
            return BookScale(f);
        }

        /// <summary>Dzisiejszy wzor (sprzed GL-2): ludzie ksiegi osady dzis / przy kalibracji; zamek 1.</summary>
        private static double BookScale(Folk f)
        {
            if (f.Kind == 1 || !(f.PeopleCal > 0)) return 1.0;
            double p = 0;
            try { p = PopulationLaw.PeopleOf(f.St); } catch (Exception e) { p = 0; Stumble("Scale", e); }   // recenzja E5/0 (1-3): osada wypada z koszyka na te dobe - liczone
            return Math.Max(0.0, p / f.PeopleCal);
        }

        private static void Model(int day, bool partial)
        {
            var d = new Day();
            _d = d;
            var s = Settings.Current;
            var war = new Dictionary<Kingdom, bool>(); var warTax = new Dictionary<Kingdom, bool>();
            RefreshPolicies();
            float valveShare = s != null && !float.IsNaN(s.CastleDuesShare) ? Math.Max(0f, Math.Min(1f, s.CastleDuesShare)) : 0f;
            bool castleOn = s != null && s.CastlePurseEnabled;
            double stock0 = 0, stock1 = 0;
            foreach (var f in _f.Values) if (f.Cal) stock0 += Stock(f);
            var csv = new StringBuilder();
            int startDay = CampaignStart(day);
            int rd = day - startDay + 1;
            bool csvDay = !partial && (rd % 7 == 0 || !_csvFirst);
            if (csvDay) _csvFirst = true;
            // 1. miasta, 2. zamki, 3. wsie (zakupy wsi do miast targowych - robocizna na jutro w miescie targowym)
            for (int pass = 0; pass < 3; pass++)
            {
                foreach (var f in _f.Values)
                {
                    if (f.Kind != pass || !f.Cal) continue;
                    try
                    {
                        Kingdom k = f.St.OwnerClan != null ? f.St.OwnerClan.Kingdom : null;
                        bool w = false, wt = false;
                        if (k != null && !war.TryGetValue(k, out w))
                        {
                            w = KingdomTreasury.AtWar(k); war[k] = w;
                            try { wt = k.ActivePolicies.Contains(DefaultPolicies.WarTax); } catch { wt = false; }
                            warTax[k] = wt;
                        }
                        if (k != null) warTax.TryGetValue(k, out wt);
                        if (pass == 0) TownDay(f, d, w);
                        else if (pass == 1) CastleDay(f, d, w, castleOn, valveShare);
                        else VillageDay(f, d, w, wt);
                        if (csvDay) CsvRow(csv, day, f, k);
                    }
                    catch (Exception e) { Stumble("Model(rodzaj " + f.Kind + ")", e); }   // pierwszy wyjatek rodzaju do pliku, kazdy liczony
                }
            }
            foreach (var f in _f.Values) { if (f.Cal) stock1 += Stock(f); ClearWindow(f); }
            // kontrola zamknieta (cala kiesa ludu swiata, z jutrzejszymi wplywami): zmiana kies = wplywy z zewnatrz + robocizna i obrot od kupcow
            // - podatki (pan, korona, danina wsi) - zaplacone kupcom (miasta, zamki, wsie) - farma gminy i oplaty wsi + obciecie ujemnych kies wsi
            double rr = (stock1 - stock0) - (d.ExtIn + d.LabIn - d.TaxOut - d.FarmOut - d.Paid - d.VillPaid + d.ClampV);
            foreach (var kv in war) if (kv.Value) d.WarK++;
            _lineFolk = FolkLine(day, d, rr, partial);
            _lineFolkPartial = partial;
            if (csvDay && csv.Length > 0) { try { Log.Csv("kiesa-ludu.csv", CsvHead, csv.ToString()); } catch (Exception e) { Stumble("Csv", e); } }
            if (!partial) { try { ShareCsv(day); } catch (Exception e) { Stumble("ShareCsv", e); } }
        }
        private static bool _csvFirst;

        private static double Stock(Folk f)
        {
            return f.LW + f.LR + f.LP + f.Gmina + f.Notab + f.Lab[0] + f.Lab[1] + f.Turn + f.Inner[0] + f.Inner[1] + f.Inner[2] + f.Gm[0] + f.Gm[1];
        }

        /// <summary>Robocizna i obrot P od sprzedazy kupcow (mieszanka towaru) - dochod ludu (dzis z okna albo jutro z wewnatrz).</summary>
        private static void Merch(double value, double food, double goods, double arms, out double lab, out double turn)
        {
            lab = value * (food * RhFood + goods * RhMid + arms * RhArms);
            turn = value * TurnP;
        }

        private static void TownDay(Folk f, Day d, bool war)
        {
            d.Towns++;
            double r = Scale(f); f.DR = r;
            f.DBook = f.PeopleCal * (PeopleMarket.MarketOn ? BookScale(f) : r);   // GL-2: ludzie ksiegi osady (linia, CSV) - przy glowach wedlug rynku osobno
            double idx = f.Idx > 0 && !double.IsNaN(f.Idx) ? f.Idx : 1.0;
            // ---- 1. dochod okna (zewnetrzny) i wczorajszy (wewnetrzny)
            double life = f.Win[KLife], car = f.Win[KLifeCaravan], band = f.Win[KLifeBand], gar = f.Win[KGarrison], court = f.Win[KCourt];
            double y1a = LifeShare * life, y1b = LifeShare * car, y1c = LifeShare * band, y1d = LifeShare * gar, y1 = y1a + y1b + y1c + y1d;
            double y2 = CourtShare * court, y3a = CraftShare * f.ProdV, y3b = f.Win[KBuildLabour], y3c = f.Win[KMine];
            double iW = 0.8 * y1 + 0.8 * y2 + 0.7 * y3a + 0.6 * y3b + y3c, iR = 0.1 * y1 + 0.2 * y2 + 0.3 * y3a + 0.4 * y3b, iP = 0.1 * y1;
            double ext = y1 + y2 + y3a + y3b + y3c;
            // kupcy: 75% zycia i zalog (jedzenie), 80% dworu (kuchnia, sukno, zbytki), sprzedaz Sale - robocizna i obrot P
            double lab = 0, turn = 0, l1, t1;
            Merch((1 - LifeShare) * (life + car + band + gar), 1, 0, 0, out l1, out t1); lab += l1; turn += t1;
            Merch((1 - CourtShare) * court, 0.5, 0.2, 0.3, out l1, out t1); lab += l1; turn += t1;
            for (int mi = 0; mi < Mixes; mi++) { Merch(f.Sale[mi], MixOf[mi][0], MixOf[mi][1], MixOf[mi][2], out l1, out t1); lab += l1; turn += t1; }
            double y5 = CaravanLab * (f.CarBuy + f.CarSell) + TurnP * f.CarBuy;
            iW += LabW * (lab + CaravanLab * (f.CarBuy + f.CarSell)); iR += (1 - LabW) * (lab + CaravanLab * (f.CarBuy + f.CarSell)); iP += turn + TurnP * f.CarBuy;
            double labWin = lab + turn + y5;
            // wczorajsze: robocizna od towaru ludu, obrot P, czynsz/leczenie/sluzba/praca, roboty gminy
            double y4aN = f.Lab[0] + f.Lab[1], y4bN = f.Turn, y6 = f.Inner[0] + f.Inner[1] + f.Inner[2], y3d = f.Gm[0] + f.Gm[1];
            iW += f.Lab[0] + f.Inner[0] + f.Gm[0]; iR += f.Lab[1] + f.Inner[1] + f.Gm[1]; iP += f.Turn + f.Inner[2];
            f.Lab[0] = f.Lab[1] = 0; f.Turn = 0; f.Inner[0] = f.Inner[1] = f.Inner[2] = 0; f.Gm[0] = f.Gm[1] = 0;
            // krolestwo niewolnicze: czesc pracy wyrobnikow robia niewolni - ten dochod bierze wlasciciel (R 40 / P 60)
            if (f.Slave && f.HS + f.HW > 0)
            {
                double y = iW * f.HS / (f.HS + f.HW);
                iW -= y; iR += y * OwnR; iP += y * OwnP; d.YSlave += y;
            }
            double Y = iW + iR + iP;
            d.Y += Y; d.Y1a += y1a; d.Y1b += y1b; d.Y1c += y1c; d.Y1d += y1d; d.Y2 += y2; d.Y3a += y3a; d.V += f.ProdV; d.Y3k += f.Win[KCraftPaid];
            d.Y3b += y3b; d.Y3c += y3c; d.Y3d += y3d; d.Y4a += lab + y4aN; d.Y4b += turn + y4bN; d.Y5 += y5; d.Y6 += y6;
            d.ExtIn += ext; d.LabIn += labWin;
            // ---- 2-3. koszyk i kotwica (R1, KL L3): towar ludu = min(wydatek gry, ludzie x koszyk)
            double hW = f.HW * r, hR = f.HR * r, hP = f.HP * r, hS = f.HS * r;
            double bW = hW * BW * idx, bR = hR * BR * idx, bP = hP * BP * idx;
            if (f.Slave) { double sl = hS * BS * idx; bR += sl * OwnR; bP += sl * OwnP; }
            double B = bW + bR + bP;
            double Wg = f.SpendGame > 0 ? f.SpendGame : Math.Max(0, f.ConsGold);   // bez petli BK - zloto "zakupow" (vanilla)
            double G = Wg > 0 ? Math.Min(Wg, B) : 0;
            double m = Wg > 0 ? G / Wg : double.NaN;
            d.HW += hW; d.HR += hR; d.HP += hP; d.HS += hS; d.People += f.DBook; d.B += B; d.Wg += Wg; d.Wf += f.SpendFood; d.G0 += f.G0; d.G1 += f.G1;
            if (!double.IsNaN(m)) { d.MSum += m; d.MN++; if (double.IsNaN(d.MMin) || m < d.MMin) d.MMin = m; if (m < 1) d.MLow++; }
            d.Spend += f.SpendGame; d.Consumed += f.ConsumedBk;
            double rateGame = f.SpendGame > 0 ? (f.Grp[0] * RhFood + f.Grp[1] * RhMid + f.Grp[2] * RhArms) / f.SpendGame : RhMid;
            double gW = B > 0 ? bW * G / B : 0, gR = B > 0 ? bR * G / B : 0, gP = B > 0 ? bP * G / B : 0;
            double out0 = d.Paid, a0 = d.A;
            double fW, fR, fP, ffW, ffR, ffP;
            ClassDay(f, d, 0, iW, gW, KeepW, FdW, TaxW, WarW, RentW, HlW, 0, RdW, war, rateGame, out fW, out ffW);
            ClassDay(f, d, 1, iR, gR, KeepR, FdR, TaxR, WarR, RentR, HlR, 0, RdR, war, rateGame, out fR, out ffR);
            ClassDay(f, d, 2, iP, gP, KeepP, FdP, TaxP, WarP, 0, HlP, ServP, RdP, war, rateGame, out fP, out ffP);
            // ---- 5. notable (czynsz) i gmina
            double x = SurplusRate * f.Notab; f.Notab -= x; d.Paid += x; d.Notab += x;
            NextLab(f, d, x * RateNotable, TurnP * x);
            x = GminaOut * f.Gmina; f.Gmina -= x;
            if (x > 0)
            {
                double farm = GFarm * x, pub = x - farm, mat = (1 - GLab) * pub;
                d.FarmOut += farm; d.GFarm += farm; d.GPub += pub;
                f.Gm[0] += 0.5 * GLab * pub; f.Gm[1] += 0.5 * GLab * pub;
                d.Paid += mat; NextLab(f, d, mat * RatePubl, TurnP * mat);
            }
            d.Gm += f.Gmina;
            // stac (swiat - wagi potrzeb) i miasta ponizej 95%
            if (Math.Min(ffW, Math.Min(ffR, ffP)) < 0.95) d.NFood++;
            if (Math.Min(fW, Math.Min(fR, fP)) < 0.95) d.NFull++;
            // liczby do CSV
            double needAll = (gW / KeepW) + (gR / KeepR) + (gP / KeepP);
            f.DB = B; f.DWg = Wg; f.DM = m; f.DInc = Y; f.DOut = d.Paid - out0; f.DUnpaid = d.A - a0;
            f.DAff = needAll > 0 ? (fW * gW / KeepW + fR * gR / KeepR + fP * gP / KeepP) / needAll : 1;
            double nf = gW / KeepW * FdW + gR / KeepR * FdR + gP / KeepP * FdP;
            f.DAffF = nf > 0 ? (ffW * gW / KeepW * FdW + ffR * gR / KeepR * FdR + ffP * gP / KeepP * FdP) / nf : 1;
            // kasy miast po B1 (szacunek): + zaplacone przez lud - dochod ludu z kas (robocizna, place, obrot, czesc ludu z zycia/zalog/dworu/budow) - "zakupy" z niczego
            d.LabOut += labWin + y3a + y1 + y2 + y3b + y3c;
            d.Cons += Math.Max(0, f.ConsGold);
            // ---- udzial zysku kupcow zabierany przez pana i korone (Jeff 16:10): okno Daily - Daily, kasa miasta
            ShareDay(f, d);
        }

        /// <summary>Doba klasy miasta (port sim :430-480): podatki przy dochodzie, zakup koszyka, czynsz/leczenie/sluzba, zapas i nadwyzka 7%.</summary>
        private static void ClassDay(Folk f, Day d, int c, double inc, double goods, double keep, double fd, double tax, double warW, double rent, double hl, double serv,
                                     double rdays, bool war, double rateGame, out double fc, out double ffc)
        {
            double tp = inc * tax, tk = war ? inc * WarS * warW : 0, tg = c == 1 ? inc * GuildR : 0;
            d.P1 += tp; d.P3 += tk; d.P2 += tg; d.TaxOut += tp + tk; d.WarT += tk; d.GGuild += tg;
            f.Gmina += tg;
            double L = (c == 0 ? f.LW : c == 1 ? f.LR : f.LP) + inc - tp - tk - tg;
            double need = goods / keep;
            fc = 1; ffc = 1;
            if (need > 0)
            {
                fc = Math.Min(1.0, Math.Max(0, L) / need);
                ffc = Math.Min(1.0, Math.Max(0, L) / (need * fd));
                double sp = fc * need; L -= sp;
                double gd = fc * goods, ex = Excise * gd;
                f.Gmina += ex; d.GEx += ex; d.Paid += gd - ex; d.Z1 += gd;
                NextLab(f, d, (gd - ex) * rateGame, TurnP * (gd - ex));
                double rn = sp * rent;
                f.Notab += rn * (1 - RentToP);
                if (c != 2) f.Inner[2] += rn * RentToP; else f.Notab += rn * RentToP;
                double h = sp * hl;
                if (c == 1) L += h; else f.Inner[1] += h;
                if (serv > 0) f.Inner[0] += sp * serv;
                d.Z2 += rn; d.Z3 += h + sp * serv;
                d.A += (1 - fc) * goods;
                double res = rdays * need, lv = SurplusRate * Math.Max(0, L - res);
                L -= lv;
                if (lv > 0)
                {
                    if (c == 0)
                    {
                        double ex2 = Excise * lv; f.Gmina += ex2; d.GEx += ex2; d.Paid += lv - ex2; d.T1 += lv;
                        NextLab(f, d, (lv - ex2) * RateNadW, TurnP * (lv - ex2));
                    }
                    else
                    {
                        double wp = SurpWage * lv;
                        if (c == 1) f.Inner[0] += wp; else { f.Inner[0] += 0.6 * wp; f.Inner[1] += 0.4 * wp; }
                        double gp = lv - wp; d.Paid += gp; d.T2 += wp; d.T1 += gp;
                        NextLab(f, d, gp * RateNadRP, TurnP * gp);
                    }
                }
            }
            if (c == 0) { f.LW = L; d.LW += L; d.NW += need; d.AW += fc * need; d.NFW += need * fd; d.AFW += ffc * need * fd; }
            else if (c == 1) { f.LR = L; d.LR += L; d.NR += need; d.AR += fc * need; d.NFR += need * fd; d.AFR += ffc * need * fd; }
            else { f.LP = L; d.LP += L; d.NP += need; d.AP += fc * need; d.NFP += need * fd; d.AFP += ffc * need * fd; }
        }

        /// <summary>Robocizna (W90/R10) i obrot P od sprzedazy kupcow dzis - dochod ludu jutro; placa kupcy (wplyw do kiesy ludu).</summary>
        private static void NextLab(Folk f, Day d, double lab, double turn)
        {
            if (f == null) return;
            if (f.Kind == 0)
            {
                f.Lab[0] += LabW * lab; f.Lab[1] += (1 - LabW) * lab; f.Turn += turn;
                d.LabIn += lab + turn; d.LabTownNext += lab + turn;
            }
            else { f.Lab[0] += lab; d.LabIn += lab; }   // zamek: robocizna do podzamcza, bez patrycjatu
        }

        private static void CastleDay(Folk f, Day d, bool war, bool castleOn, float valveShare)
        {
            d.Castles++;
            double idx = f.Idx > 0 && !double.IsNaN(f.Idx) ? f.Idx : 1.0;
            // zold zalogi i zycie zolnierzy, ludzi karawan i band wydane w zamku - 25% do podzamcza, 75% kupcom (jedzenie) jak w miescie
            double gar = f.Win[KGarrison] + f.Win[KLife] + f.Win[KLifeCaravan] + f.Win[KLifeBand], court = f.Win[KCourt], build = f.Win[KBuildLabour];
            // 15% zaworu kasy zamku (KL; hak CastleDuesSuburbShare) - zawor dzisiejszego CastlePurse.Daily z nadwyzki przed zaworem (CastlePurse.DuesOf)
            double valve = 0;
            if (castleOn)
            {
                long lp, sp; CastlePurse.DuesOf(f.St, out lp, out sp);
                if (sp > 0) valve = Math.Min((double)sp, sp * (double)valveShare);
            }
            double ext = LifeShare * gar + CourtShare * court + build + CastleValve * valve;
            double lab = (1 - LifeShare) * gar * RhFood + (1 - CourtShare) * court * RateCourt;
            for (int m = 0; m < Mixes; m++) lab += f.Sale[m] * (MixOf[m][0] * RhFood + MixOf[m][1] * RhMid + MixOf[m][2] * RhArms);
            double inc = ext + lab + f.Lab[0];
            f.Lab[0] = f.Lab[1] = 0;
            d.ExtIn += ext; d.LabIn += lab;
            d.CInc += inc; d.CGar += LifeShare * gar; d.CCourt += CourtShare * court; d.CBuild += build; d.CValve += CastleValve * valve;
            double tp = inc * TaxC, tk = war ? inc * WarS * WarC : 0;
            d.TaxOut += tp + tk; d.CTax += tp + tk; d.WarT += tk;
            double L = f.LW + inc - tp - tk;
            double heads = CastleHeads(f);                                   // GL-3 (P3): podzamcze wedlug ludzi wsi zamku (klucz wylaczony - f.HW)
            double B = heads * BC * idx, Wg = f.SpendGame;
            d.Spend += f.SpendGame; d.Consumed += f.ConsumedBk;
            double need = Wg > 0 ? Math.Min(Wg, B) : 0;
            double fc = need > 0 ? Math.Min(1.0, Math.Max(0, L) / need) : 1, ffc = need > 0 ? Math.Min(1.0, Math.Max(0, L) / (need * FdC)) : 1;
            double sp2 = fc * need; L -= sp2; d.Paid += sp2; d.CPaid += sp2; d.CGoods += sp2;
            NextLab(f, d, sp2 * RateCastle, 0);
            double lv = SurplusRate * Math.Max(0, L - RdC * need); L -= lv; d.Paid += lv; d.CPaid += lv; d.CGoods += lv;
            NextLab(f, d, lv * RateNadW, 0);
            f.LW = L;
            d.CHeads += heads; d.CB += B; d.CL += L; d.CNeed += need; d.CAff += fc * need; d.CWg += Wg;
            d.A += (1 - fc) * need;
            f.DB = B; f.DWg = Wg; f.DM = Wg > 0 ? need / Wg : double.NaN; f.DInc = inc; f.DOut = sp2 + lv; f.DAff = fc; f.DAffF = ffc; f.DR = 1; f.DUnpaid = (1 - fc) * need;
            // zamek: jedzenie zuzyte w konsumpcji (pierscien 7 dob - linia "Jedzenie z targu")
            f.Food7[f.Food7Head] = f.FoodSold; f.Food7Head = (f.Food7Head + 1) % 7; if (f.Food7Fill < 7) f.Food7Fill++;
            f.FoodSold = 0;
        }

        private static void VillageDay(Folk f, Day d, bool war, bool warTax)
        {
            d.Villages++;
            double r = Scale(f); f.DR = r;
            f.DBook = f.PeopleCal * (PeopleMarket.HeadsOn ? BookScale(f) : r);   // GL-3: ludzie ksiegi wsi (CSV) - przy glowach wedlug u_v osobno
            double hand = VillShare * f.VHanded, inc = hand + f.VTrans + f.Win[KReleased];
            d.VInc += inc; d.VHand += hand; d.VTrans += f.VTrans; d.VRel += f.Win[KReleased];
            d.ExtIn += inc;
            double L = f.LW + inc;
            if (L < 0) { d.ClampV += -L; L = 0; }   // przelewy z kiesy wsi wieksze niz kiesa na sucho - obciete (w kontroli osobno)
            double free = (f.HW + f.HR) * r;
            d.VFree += free;
            double fee = Math.Min(Math.Max(0, L), VillFeeYear / YearDays * free); L -= fee; d.VFee += fee; d.FarmOut += fee;
            double need = BV * free, buy = Math.Min(need, Math.Max(0, L)); L -= buy;
            d.VNeed += need; d.VBuy += buy;
            var tf = TradeTown(f);
            if (tf == null && buy > 0) d.NoTrade++;
            d.VillPaid += buy; NextLab(tf, d, buy * RateVill, TurnP * buy);
            double baseX = Math.Max(0, L - RdV * need);
            double lay = war ? VillLay * (warTax ? 2 : 1) * baseX : 0; L -= lay; d.VLay += lay; d.TaxOut += lay;
            double extB = SurplusRate * Math.Max(0, L - RdV * need); L -= extB; d.VExt += extB;
            d.VillPaid += extB; NextLab(tf, d, extB * RateVill, TurnP * extB);
            f.LW = L; d.VPurse.Add(L);
            f.DB = need; f.DWg = 0; f.DM = double.NaN; f.DInc = inc; f.DOut = fee + buy + lay + extB; f.DAff = need > 0 ? buy / need : 1; f.DAffF = f.DAff; f.DUnpaid = 0;
        }

        private static Folk TradeTown(Folk v)
        {
            try
            {
                var vil = v.St.Village;
                var t = vil != null ? (vil.TradeBound ?? vil.Bound) : null;
                if (t == null || !t.IsTown) return null;
                Folk f; return _f.TryGetValue(t, out f) && f.Cal ? f : null;
            }
            catch (Exception e) { Stumble("TradeTown", e); return null; }
        }

        // ------------------------------------------------------------ udzial zysku kupcow (Jeff 11.10 16:10)
        // okno Daily - Daily: pobrane = renta (dzis) + danina, clo, mennica, Crown Duty, War Tax, Road Tolls (NoteTake w oknie); doplyw netto = zmiana kasy
        // + pobrane - przelewy z niczego ("zakupy" mieszczan i dosypka regulatora) + kasowanie regulatora (w nicosc); udzial = pobrane / doplyw (<= 0: "-")
        private static readonly double[] _sTake = new double[28], _sIn = new double[28], _sIn2 = new double[28];
        private static int _sHead, _sFill;
        private static double _sDayTake, _sDayIn, _sDayIn2, _sDayCons, _sDayReg; private static int _sDayN;
        private static bool _sDayOk;

        private static void ShareDay(Folk f, Day d)
        {
            long pay, avail; PopulationLaw.RentOf(f.St, out pay, out avail);
            long gold = f.St.Town.Gold;
            double take = f.TakeWin + Math.Max(0, pay);
            // renta dzis z pasa ponizej zapasu (kasa po rencie = dostepne ponad prog + prog - renta)
            if (pay > 0)
            {
                _rentPaid += pay;
                long floor = (long)Math.Max(0f, Settings.Current.TownRentFloorGold);
                long after = avail + floor - pay;
                _rentBelow += Math.Max(0, Math.Min(pay, Seal31.TownReserve(f.St.Town) - after));
            }
            if (f.Gold0 != long.MinValue)
            {
                double cons = Math.Max(0, f.ConsGold), reg = f.RegIn - f.RegOut;
                double inflow = (gold - f.Gold0) + take - f.ConsGold - f.RegIn + f.RegOut;
                f.DTake = take; f.DIn = inflow;
                f.RTake[f.RHead] = take; f.RIn[f.RHead] = inflow; f.RHead = (f.RHead + 1) % 28; if (f.RFill < 28) f.RFill++;
                d.TakeW += take; d.InW += inflow; d.InW2 += inflow + f.ConsGold; d.Reg += reg; d.TakeN++;
            }
            f.Gold0 = gold;
        }

        private static string Pct(double part, double whole) { return whole > 0 ? (100.0 * part / whole).ToString("0.#", Inv) + "%" : "-"; }
        private static string N0(double v) { return double.IsNaN(v) || double.IsInfinity(v) ? "-" : Math.Round(v).ToString("0", Inv); }
        private static string F2(double v) { return double.IsNaN(v) || double.IsInfinity(v) ? "-" : v.ToString("0.00", Inv); }
        private static string F1(double v) { return double.IsNaN(v) || double.IsInfinity(v) ? "-" : v.ToString("0.0", Inv); }

        private static string FolkLine(int day, Day d, double rr, bool partial)
        {
            // pierscien udzialu zysku kupcow (swiat) - doby pelne
            _sDayOk = d.TakeN > 0 && !partial;
            _sDayTake = d.TakeW; _sDayIn = d.InW; _sDayIn2 = d.InW2; _sDayCons = d.Cons; _sDayReg = d.Reg; _sDayN = d.TakeN;
            if (_sDayOk) { _sTake[_sHead] = d.TakeW; _sIn[_sHead] = d.InW; _sIn2[_sHead] = d.InW2; _sHead = (_sHead + 1) % 28; if (_sFill < 28) _sFill++; }
            double kasy = (d.Paid - d.CPaid) + d.VillPaid - (d.LabOut + d.LabTownNext) - d.Cons;
            double sp = d.Spend, cv = d.Consumed;
            double vMed = double.NaN; int vPoor = 0;
            if (d.VPurse.Count > 0) { d.VPurse.Sort(); vMed = d.VPurse[d.VPurse.Count / 2]; foreach (var v in d.VPurse) if (v < 1000) vPoor++; }
            var sb = new StringBuilder(4096);
            sb.Append("Kiesa ludu (na sucho): dzien ").Append(day).Append(partial ? " | doba niepelna (pierwsza w sesji)" : "")
              .Append(" | miasta ").Append(d.Towns).Append(": ludzie ksiegi ").Append(N0(d.People)).Append(" (BK z kalibracji x ksiega: W ").Append(N0(d.HW)).Append(" / R ").Append(N0(d.HR))
              .Append(" / P ").Append(N0(d.HP)).Append(d.HS > 0 ? " / niewolni utrzymywani " + N0(d.HS) : "").Append(")")
              .Append(", koszyk ludu ").Append(N0(d.B)).Append(", wydatek gry ").Append(N0(d.Wg)).Append(" (jedzenie ").Append(N0(d.Wf)).Append(")")
              .Append(", budzet gry ").Append(N0(d.G0)).Append(" (zmiana w petli BK na wierszach z budzetem ").Append(N0(d.G1)).Append(" - dobrobyt/indeks BK z mnoznikami HistoricalPrices)")
              .Append(", kotwica m srednio ").Append(d.MN > 0 ? F2(d.MSum / d.MN) : "-").Append(" (min ").Append(F2(d.MMin)).Append(", miast z m < 1: ").Append(d.MLow).Append(")")
              .Append(" | dochod ").Append(N0(d.Y)).Append(": wojsko ").Append(N0(d.Y1a + d.Y1b + d.Y1c + d.Y1d)).Append(" (zycie ").Append(N0(d.Y1a)).Append(", karawany ").Append(N0(d.Y1b))
              .Append(", bandy ").Append(N0(d.Y1c)).Append(", zalogi ").Append(N0(d.Y1d)).Append("), dwor ").Append(N0(d.Y2))
              .Append(", rzemioslo i budowy ").Append(N0(d.Y3a + d.Y3b + d.Y3c + d.Y3d)).Append(" (place 35% wyrobow ").Append(N0(d.Y3a)).Append(" z wartosci ").Append(N0(d.V))
              .Append(", juz wyplacone ").Append(N0(d.Y3k)).Append("; budowy ").Append(N0(d.Y3b)).Append("; kopalnie ").Append(N0(d.Y3c)).Append("; roboty gminy ").Append(N0(d.Y3d)).Append(")")
              .Append(", handel ").Append(N0(d.Y4a + d.Y4b)).Append(" (robocizna ").Append(N0(d.Y4a)).Append(", obrot P ").Append(N0(d.Y4b)).Append(")")
              .Append(", karawany ").Append(N0(d.Y5)).Append(", zysk kupcow - (A1), wewnatrz ludu ").Append(N0(d.Y6))
              .Append(d.YSlave > 0 ? ", w tym praca niewolnych do wlascicieli " + N0(d.YSlave) : "")
              .Append(" | wydatki ").Append(N0(d.Z1 + d.Z2 + d.Z3 + d.P1 + d.P2 + d.P3)).Append(": towar ").Append(N0(d.Z1)).Append(", czynsz ").Append(N0(d.Z2))
              .Append(", leczenie i sluzba ").Append(N0(d.Z3)).Append(", podatki ").Append(N0(d.P1 + d.P2 + d.P3)).Append(" (pan ").Append(N0(d.P1)).Append(", cech ").Append(N0(d.P2))
              .Append(", korona ").Append(N0(d.P3)).Append(")")
              .Append(" | kiesy ").Append(N0(d.LW + d.LR + d.LP)).Append(" (W ").Append(N0(d.LW)).Append(" / R ").Append(N0(d.LR)).Append(" / P ").Append(N0(d.LP)).Append(")")
              .Append(", zapas dni W ").Append(F1(d.NW > 0 ? d.LW / d.NW : double.NaN)).Append(" / R ").Append(F1(d.NR > 0 ? d.LR / d.NR : double.NaN))
              .Append(" / P ").Append(F1(d.NP > 0 ? d.LP / d.NP : double.NaN))
              .Append(" | stac jedzenie W/R/P ").Append(Pct(d.AFW, d.NFW)).Append(" / ").Append(Pct(d.AFR, d.NFR)).Append(" / ").Append(Pct(d.AFP, d.NFP))
              .Append(", pelny W/R/P ").Append(Pct(d.AW, d.NW)).Append(" / ").Append(Pct(d.AR, d.NR)).Append(" / ").Append(Pct(d.AP, d.NP))
              .Append(", miast ze stac jedzenie < 95%: ").Append(d.NFood).Append(", pelny < 95%: ").Append(d.NFull)
              .Append(" | zjedzone bez zaplaty ").Append(N0(d.A)).Append(" (").Append(Pct(d.A, d.Wg + d.CWg)).Append(" wydatku gry)")
              .Append(" | nadwyzka 7%: towar ").Append(N0(d.T1)).Append(", place ").Append(N0(d.T2))
              .Append(" | gmina: stan ").Append(N0(d.Gm)).Append(", akcyza ").Append(N0(d.GEx)).Append(", cech ").Append(N0(d.GGuild)).Append(", farma ").Append(N0(d.GFarm))
              .Append(", roboty ").Append(N0(d.GPub)).Append(", notable wydali ").Append(N0(d.Notab))
              .Append(" | podzamcze ").Append(d.Castles).Append(": ludzie ").Append(N0(d.CHeads)).Append(", koszyk ").Append(N0(d.CB)).Append(", dochod ").Append(N0(d.CInc))
              .Append(" (zaloga ").Append(N0(d.CGar)).Append(", dwor ").Append(N0(d.CCourt)).Append(", budowy ").Append(N0(d.CBuild)).Append(", 15% zaworu ").Append(N0(d.CValve)).Append(")")
              .Append(", towar ").Append(N0(d.CGoods)).Append(", podatki ").Append(N0(d.CTax)).Append(", kiesa ").Append(N0(d.CL)).Append(", stac ").Append(Pct(d.CAff, d.CNeed))
              .Append(", wydatek gry zamkow (dzis zjedzone bez zaplaty) ").Append(N0(d.CWg))
              .Append(" | wsie ").Append(d.Villages).Append(": wolne glowy ").Append(N0(d.VFree)).Append(", dochod ").Append(N0(d.VInc)).Append(" (65% utargu ").Append(N0(d.VHand))
              .Append(", przelewy ").Append(N0(d.VTrans)).Append(", sakiewki zwolnionych ").Append(N0(d.VRel)).Append(")")
              .Append(", zakupy ").Append(N0(d.VBuy)).Append(" z ").Append(N0(d.VNeed)).Append(" (").Append(Pct(d.VBuy, d.VNeed)).Append(")")
              .Append(", nadwyzka ").Append(N0(d.VExt)).Append(", oplaty panu ").Append(N0(d.VFee)).Append(", danina ").Append(N0(d.VLay))
              .Append(", kiesy (mediana ").Append(N0(vMed)).Append(", < 1 000 zl: ").Append(vPoor).Append(")").Append(d.NoTrade > 0 ? ", bez miasta targowego " + d.NoTrade : "")
              .Append(" | podatek wojenny przy s = 3%: ").Append(N0(d.WarT)).Append(" (krolestw w wojnie ").Append(d.WarK).Append(")")
              .Append(" | kasy miast po B1 (szacunek): ").Append(N0(kasy)).Append(" (zaplacone kupcom miast przez lud i wsie ").Append(N0(d.Paid - d.CPaid + d.VillPaid))
              .Append(" - dochod ludu z kas miast ").Append(N0(d.LabOut + d.LabTownNext)).Append(" - \"zakupy\" z niczego ").Append(N0(d.Cons)).Append(")")
              .Append(" | kontrola: r=").Append(F1(rr)).Append(" (").Append(d.Y + d.CInc + d.VInc > 0 ? (100.0 * Math.Abs(rr) / (d.Y + d.CInc + d.VInc)).ToString("0.000", Inv) : "-").Append("% dochodu)")
              .Append(" | konsumpcja: wydatek z petli BK ").Append(N0(sp)).Append(" wobec ConsumedValue ").Append(N0(cv))
              .Append(" (roznica ").Append(cv > 0 ? (100.0 * (sp - cv) / cv).ToString("0.00", Inv) + "%" : "-").Append(")")
              .Append(", dokup jedzenia BK (zapas <= 10%): osad ").Append(_lowFoodN).Append(", petla ponad ConsumedValue zdjeta z wydatku jedzenia ").Append(N0(_lowFoodCut))
              .Append(", osad z konsumpcja bez BK ").Append(_noBkCons);
            _noBkCons = 0; _lowFoodN = 0; _lowFoodCut = 0;
            return sb.ToString();
        }

        // ------------------------------------------------------------ CSV
        private const string CsvHead = "dzien;dzien_gry;osada_id;osada;rodzaj;krolestwo;kultura;polityka_bk;ludzie_ksiegi;ludzie_bk_kal;skala_r;W;R;P;niewolni;koszyk;wydatek_gry;wydatek_jedzenie;m;dochod;wydatki;kiesa_W;kiesa_R;kiesa_P;stac_jedzenie;stac_pelny;kasa;zapas_D4;zjedzone_bez_zaplaty";

        private static void CsvRow(StringBuilder sb, int day, Folk f, Kingdom k)
        {
            var st = f.St;
            double people = f.Kind == 1 ? CastleHeads(f) : f.DBook;   // GL-2: ludzie ksiegi (przy glowach wedlug rynku - nie PeopleCal x DR)
            double bk = f.HW + f.HR + f.HP + f.HS;
            long gold = st.SettlementComponent != null ? st.SettlementComponent.Gold : 0;
            long res = f.Kind == 0 ? Seal31.TownReserve(st.Town) : (f.Kind == 1 ? CastlePurse.Reserve(st.Town) : 0);
            double unpaid = f.DUnpaid;
            sb.Append(day).Append(';').Append(day + 1).Append(';').Append(st.StringId).Append(';').Append(Clean(st.Name != null ? st.Name.ToString() : st.StringId)).Append(';')
              .Append(f.Kind == 0 ? "miasto" : f.Kind == 1 ? "zamek" : "wies").Append(';').Append(Clean(k != null ? k.Name.ToString() : "-")).Append(';')
              .Append(st.Culture != null ? st.Culture.StringId : "").Append(';').Append(f.Kind == 2 ? PolName(f.Policy) : "-").Append(';')
              .Append(N0(people)).Append(';').Append(N0(bk)).Append(';').Append(f.DR.ToString("0.000", Inv)).Append(';')
              .Append(N0(f.HW * f.DR)).Append(';').Append(N0(f.HR * f.DR)).Append(';').Append(N0(f.HP * f.DR)).Append(';').Append(N0(f.HS * f.DR)).Append(';')
              .Append(N0(f.DB)).Append(';').Append(N0(f.DWg)).Append(';').Append(N0(f.Kind < 2 ? f.SpendFood : 0)).Append(';').Append(F2(f.DM)).Append(';')
              .Append(N0(f.DInc)).Append(';').Append(N0(f.DOut)).Append(';').Append(N0(f.LW)).Append(';').Append(N0(f.LR)).Append(';').Append(N0(f.LP)).Append(';')
              .Append(F2(f.DAffF)).Append(';').Append(F2(f.DAff)).Append(';').Append(gold).Append(';').Append(res).Append(';').Append(N0(unpaid)).Append(Environment.NewLine);
        }

        private static string Clean(string s) { return s == null ? "" : s.Replace(';', ',').Replace('\n', ' ').Replace('\r', ' '); }
        private static string PolName(int p) { return p == 0 ? "niska" : p == 1 ? "zwykla" : p == 2 ? "wysoka" : p == 3 ? "zwolnienie" : "?"; }

        /// <summary>Udzial zysku kupcow miasto po miescie (Jeff 16:10): plik zysk-kupcow.csv kazdej pelnej doby.</summary>
        private static void ShareCsv(int day)
        {
            var sb = new StringBuilder(8192);
            foreach (var f in _f.Values)
            {
                if (f.Kind != 0 || !f.Cal || double.IsNaN(f.DIn)) continue;
                double t28 = 0, i28 = 0;
                for (int i = 0; i < f.RFill; i++) { t28 += f.RTake[i]; i28 += f.RIn[i]; }
                sb.Append(day).Append(';').Append(f.St.StringId).Append(';').Append(Clean(f.St.Name != null ? f.St.Name.ToString() : "")).Append(';')
                  .Append(N0(f.DTake)).Append(';').Append(N0(f.DIn)).Append(';').Append(f.DIn > 0 ? (100.0 * f.DTake / f.DIn).ToString("0.0", Inv) : "-").Append(';')
                  .Append(N0(t28)).Append(';').Append(N0(i28)).Append(';').Append(i28 > 0 ? (100.0 * t28 / i28).ToString("0.0", Inv) : "-").Append(';')
                  .Append(f.RFill).Append(Environment.NewLine);
                f.DIn = double.NaN;
            }
            if (sb.Length > 0) Log.Csv("zysk-kupcow.csv", "dzien;miasto_id;miasto;pobrane;doplyw_netto;udzial_proc;pobrane_28;doplyw_28;udzial_28_proc;dob_w_pierscieniu", sb.ToString());
        }

        // ------------------------------------------------------------ gotowka wsi wedlug polityki (MoneyLedger.VillageCashLine)
        /// <summary>
        /// Linia "Gotowka wsi (E5/0)": podzial wedlug polityki BK (te same zdarzenia co "Przeplywy osad (kiesy wsi)" - jedna definicja utargu) -
        /// wsi, wplynelo (utarg + przelewy + sakiewki), pan (licznik + renta), majatki, w utargu: pan / majatki / wies. Korona - tylko razem.
        /// </summary>
        internal static string VillagePolicySplit(out long hand, out long trans, out long rel)
        {
            hand = trans = rel = 0;
            var n = new int[5]; var rent = new long[5];
            foreach (var f in _f.Values)
            {
                if (f.Kind != 2) continue;
                int p = Pol(f);
                n[p]++;
                long pay, avail; PopulationLaw.RentOf(f.St, out pay, out avail);
                rent[p] += Math.Max(0, pay);
            }
            var sb = new StringBuilder(512);
            for (int p = 0; p < 5; p++)
            {
                long g = _vc[p, 0] + _vcTrans[p] + _vcRel[p];
                hand += _vc[p, 0]; trans += _vcTrans[p]; rel += _vcRel[p];
                if (n[p] == 0 && g == 0) continue;
                long lord = _vc[p, 1] + rent[p];
                if (sb.Length > 0) sb.Append("; ");
                sb.Append(PolName(p)).Append(": wsi ").Append(n[p]).Append(", G ").Append(g).Append(" (utarg ").Append(_vc[p, 0]).Append(", przelewy ").Append(_vcTrans[p])
                  .Append(", sakiewki ").Append(_vcRel[p]).Append("), pan ").Append(Pct(lord, g)).Append(", pan z majatkami ").Append(Pct(lord + _vc[p, 2], g))
                  .Append(", w utargu: pan ").Append(Pct(_vc[p, 1], _vc[p, 0])).Append(" / majatki ").Append(Pct(_vc[p, 2], _vc[p, 0])).Append(" / wies ").Append(Pct(_vc[p, 3], _vc[p, 0]));
            }
            return sb.Length > 0 ? sb.ToString() : "-";
        }

        /// <summary>Okno ksiegi pieniadza dla podzialu wedlug polityki - zerowane po jej dobie (TownFolk.Lines biegnie zaraz po MoneyLedger.Daily).</summary>
        private static void ClearVillageCash()
        {
            Array.Clear(_vc, 0, _vc.Length); Array.Clear(_vcTrans, 0, _vcTrans.Length); Array.Clear(_vcRel, 0, _vcRel.Length);
        }
    }
}
