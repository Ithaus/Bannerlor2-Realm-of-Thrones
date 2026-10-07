using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// KASA MIASTA TO PRAWDZIWY PIENIADZ (krok K6 fundamentu + "decyzja o pulapie zaworu": docs/EKONOMIA-FUNDAMENT-2026-10-05.md
    /// rozdz. 6.3 K6, K9, K10 i 6.4 z uwagami krytyka; decyzja Jeffa 06.10 pkt 5). Dotad gra co dobe sciagala kase miasta do celu
    /// 10 000 + 12 x dobrobyt: cwierc kazdej nadwyzki dziennie w nicosc, cwierc kazdego braku z niczego. Od ogniwa 107 do kas miast
    /// plynie prawdziwy zold (zalogi, wydatki zolnierzy "na zycie") - regulator kasowal go w ok. 81%, a tarcza zoldu byla obejsciem.
    ///
    /// Co robimy (wszystko pod jednym ustawieniem TownPurseRegulator; 0 = gra jak przed ta zmiana, co do znaku):
    ///  1. Regulator (latka z kroku K5 na GetTownGoldChange kazdego modelu - CastlePurse.RegulatorPostfix oddaje nam miasta):
    ///     tryb 1 - wynik ujemny = 0 (nic w nicosc); dosypka ponizej celu zostaje jako bezpiecznik liczony w ksiedze, ale nigdy
    ///     ponad zapas kupcow; tryb 2 - regulator nie robi nic.
    ///  2. Zapas kupcow miasta = TownPurseFloorGold + TownPurseFloorPerProsperity x dobrobyt (domyslnie cel regulatora gry).
    ///     To jedna granica dla wszystkiego, co kase opodatkowuje (renta pana, udzial korony, danina wojenna, clo, mennica, podatki
    ///     polityk): ponizej zapasu nikt nie pobiera, wiec dosypka regulatora nigdy nie zasila renty - koniec pompy
    ///     "regulator -> kasa -> pan". Zakupy miasta (karawany, paser, tabory) moga siegac po zapas jak dotad.
    ///  3. "Zakupy" mieszczan (TownFolkBuyWithoutMinting): towar schodzi z polki jak dotad, ale kasa nie dostaje za niego zlota
    ///     z niczego - kasa miasta to jedna kiesa kupcow i mieszczan, zakup wewnatrz niej ma saldo 0 (to samo, co K5 robi w zamku).
    ///     Bez tego zdjecie kasowania zamieniloby zloto z "zakupow" (dzis kasowane, gdy kasa stoi ponad celem) w rente panow.
    ///  4. Zawor bez pulapu naleznej: z nadwyzki ponad zapas schodzi co dobe TownRentShare (7%). Z tego pan miasta dostaje
    ///     TownRentLordShare (2/3 - farma miasta, myta, sady; dekret podatkowy BK skaluje tylko jego czesc), reszte bierze skarbiec
    ///     krolestwa (tallage miast korony) i oddaje ja rodom zwrotem zoldu w wojnie. Miasto bez krolestwa: calosc dla pana.
    ///     Pulap "naleznej" (ludnosc x 40 d rocznie) zostaje tylko liczba w logu: wiazal w 44 z 97 miast i zloto w nich zalegalo,
    ///     a 8 miast krain bez udzialu miejskiego nie mialo zaworu wcale. Stan ustalony: nadwyzka = doplyw netto / 7%.
    ///  5. Gorna galaz BK HandleMarketGold (kasa ponad 50 000 + 25 x dobrobyt: -1% w nicosc, potem przez blad znaku +0.5% do kasy
    ///     i -0.5% losowemu notablowi - razem 1% kasy dziennie znika ze swiata): pomijana dla miast, a przy czynnym K5 takze
    ///     dla zamkow (TownPurseNoBkSkim). Dolna galaz (notabl doplaca 1000 biednej kasie - przelew) zostaje.
    ///  6. Start nowej kampanii: dar startowy w kasach miast (gra 20 000 + BK 40 x dobrobyt = 20.5 mln) raz, w pierwszej dobie,
    ///     przed pierwszym zaworem, przycinany do zapasu (6.5 mln) - to samo zloto regulator kasowal w ok. 12 dob; zostawione
    ///     splyneloby zaworem do panow i korony (14 mln z niczego). Flaga w sejwie (arm_townpurse). Starszy zapis: jak w K5.
    ///  7. Tarcza zoldu (SoldierPay, ogniwo 107) przy czynnym K6 nie robi nic: znacznikow nie dopisuje, stare porzuca.
    ///  8. Zwrot zoldu z korony (ogniwo 107) nie liczy zoldu zalogi, ktory wraca panu z kasy jej osady - miasta (ten zawor) albo
    ///     zamku (danina podzamcza K5): inaczej zaloga dawalaby panu w wojnie wiecej, niz kosztuje (CrownRefundSkipsHomeGarrisons).
    ///     Wraca tylko to, co wplynelo PONAD zapas kupcow (HomePart) - zold dopelniajacy kase do zapasu jest podstawa zwrotu jak dotad.
    /// </summary>
    internal static class TownPurse
    {
        internal const int ModeGame = 0, ModeKeep = 1, ModeOff = 2;

        private const int BkLimitBase = 50000;               // BK BKEconomyModel.GetSettlementMarketGoldLimit: lenno 50 000 + dobrobyt x 25 (miasto) / x 12 (zamek)
        private const float BkLimitTown = 25f, BkLimitCastle = 12f;
        private const double FreshAgeDays = 2.0;             // pierwszy tick dobowy nowej kampanii przypada w wieku 1.0 doby (jak w CastlePurse)
        private const double RegulatorKeeps = 0.75;

        // ------------------------------------------------------------ stan kampanii
        private static bool _trimDone;                       // dar startowy rozliczony (zapis: arm_townpurse)
        private static bool _trimSkip;                       // wieku kampanii nie da sie odczytac - w tej sesji nie probujemy dalej
        private static bool _offLogged, _errLogged, _noHooksLogged;

        // ------------------------------------------------------------ nawias dziennego ticku miasta (konsumpcja -> regulator)
        private static Town _consTown, _regDue;
        private static int _consGold;

        // ------------------------------------------------------------ liczniki doby (linia "Kasy miast:")
        private static long _dRegUp, _dRegUpCut, _dRegDown, _dConsBack;
        private static int _dRegUpN, _dRegUpCutN, _dRegDownN, _dConsN, _dConsHit, _stumbles;
        private static long _vDraw, _vLord, _vCrown, _vPlayer, _vDue;
        private static int _vTowns, _vPayers, _vBelow, _vTiny, _vExempt, _vNoCrown;
        private static readonly HashSet<Kingdom> _vKingdoms = new HashSet<Kingdom>();
        private static long _bkSkipGold;
        private static int _bkSkipTowns, _bkSkipCastles;

        // ------------------------------------------------------------ latka na BK (raz na proces - to nie jest stan kampanii)
        private static bool _bkHooked;
        private static MethodInfo _bkLimit; private static PropertyInfo _bkInstance, _bkModel; private static bool _bkResolved;

        internal static void Reset()
        {
            _trimDone = false; _trimSkip = false; _offLogged = false; _errLogged = false; _noHooksLogged = false;
            _consTown = null; _regDue = null;
            _bkResolved = false; _bkLimit = null; _bkInstance = null; _bkModel = null;   // BK tworzy swoja konfiguracje od nowa przy kazdej grze
            ClearDay();
        }

        private static void ClearDay()
        {
            _dRegUp = _dRegUpCut = _dRegDown = _dConsBack = 0;
            _dRegUpN = _dRegUpCutN = _dRegDownN = _dConsN = _dConsHit = _stumbles = 0;
            _vDraw = _vLord = _vCrown = _vPlayer = _vDue = 0;
            _vTowns = _vPayers = _vBelow = _vTiny = _vExempt = _vNoCrown = 0;
            _vKingdoms.Clear();
            _bkSkipGold = 0; _bkSkipTowns = _bkSkipCastles = 0;
        }

        internal static string Export() { return _trimDone ? "done" : ""; }
        internal static void Import(string s) { _trimDone = s == "done"; }

        /// <summary>Tryb regulatora kas miast z ustawien: 0 = jak w grze (cala zmiana wylaczona), 1 = bez kasowania, 2 = wylaczony.</summary>
        internal static int Mode
        {
            get
            {
                var s = Settings.Current;
                if (s == null) return ModeGame;
                int m = s.TownPurseRegulator;
                return m <= 0 ? ModeGame : (m >= 2 ? ModeOff : ModeKeep);
            }
        }

        /// <summary>Czy kasa miasta jest prowadzona jako prawdziwy pieniadz (tryb 1 albo 2 i latki K5 na regulatorze sa wpiete).</summary>
        internal static bool On { get { return Mode != ModeGame && CastlePurse.HookedModels > 0; } }

        private static bool NoMint { get { var s = Settings.Current; return s != null && s.TownFolkBuyWithoutMinting && CastlePurse.HookedConsumption; } }

        /// <summary>Wyjatek przy jednym miescie: pierwszy do pliku, kolejne liczone w linii dobowej (mechanizmu nie gasimy).</summary>
        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            if (_errLogged) return;
            _errLogged = true;
            Log.Error(where, e);
        }

        // ------------------------------------------------------------ rachunek (czyste funkcje)
        private static float Prosperity(Town town)
        {
            float p = town.Prosperity;
            return float.IsNaN(p) || float.IsInfinity(p) || p < 0f ? 0f : p;
        }

        private static float Unit(float v) { return float.IsNaN(v) || v <= 0f ? 0f : (v >= 1f ? 1f : v); }

        /// <summary>Zapas kupcow miasta: tej czesci kasy nikt nie opodatkowuje (domyslnie = cel regulatora gry, 10 000 + 12 x dobrobyt).</summary>
        internal static int Reserve(Town town)
        {
            var s = Settings.Current;
            double r = Math.Max(0, s.TownPurseFloorGold) + (double)Math.Max(0f, s.TownPurseFloorPerProsperity) * Prosperity(town);
            return r >= int.MaxValue ? int.MaxValue : (int)r;
        }

        /// <summary>
        /// Granica kasy, ponizej ktorej nie siega zaden podatek (renta, danina wojenna, clo, mennica, podatki polityk): przy czynnym
        /// K6 zapas kupcow miasta, inaczej - i zawsze dla zamku - dotychczasowe TownRentFloorGold.
        /// </summary>
        internal static int TaxFloor(Town town)
        {
            var s = Settings.Current;
            int flat = s != null ? (int)Math.Max(0f, s.TownRentFloorGold) : 0;
            try { if (town != null && town.IsTown && On) return Reserve(town); }
            catch (Exception e) { Stumble("TownPurse.TaxFloor", e); }
            return flat;
        }

        /// <summary>Czesc nadwyzki zdejmowana zaworem jednej doby: w dol, nigdy wiecej niz nadwyzka.</summary>
        internal static int Portion(long spare, float share)
        {
            if (spare <= 0) return 0;
            float sh = Unit(share);
            if (sh <= 0f) return 0;
            long x = sh >= 1f ? spare : (long)(spare * (double)sh);
            if (x > spare) x = spare;
            return x >= int.MaxValue ? int.MaxValue : (int)x;
        }

        /// <summary>Podzial zdjetej kwoty: korona dostaje (1 - udzial pana) w dol, pan reszte; suma zawsze rowna `draw`.</summary>
        internal static void Split(int draw, float lordShare, out int lord, out int crown)
        {
            if (draw <= 0) { lord = 0; crown = 0; return; }
            double c = draw * (1.0 - Unit(lordShare));
            crown = c <= 0.0 ? 0 : (c >= draw ? draw : (int)c);
            lord = draw - crown;
        }

        /// <summary>Limit kasy lenna wedle BK (ponad nim BK kasuje 1% dziennie): z modelu BK, a gdy go nie ma - ze stalych z jego kodu.</summary>
        internal static int BkLimit(Settlement st)
        {
            try
            {
                if (!_bkResolved)
                {
                    _bkResolved = true;
                    var cfgT = AccessTools.TypeByName("BannerKings.BannerKingsConfig");
                    _bkInstance = cfgT != null ? AccessTools.Property(cfgT, "Instance") : null;
                    _bkModel = cfgT != null ? AccessTools.Property(cfgT, "EconomyModel") : null;
                }
                if (_bkInstance != null && _bkModel != null)
                {
                    var cfg = _bkInstance.GetValue(null, null);
                    var model = cfg != null ? _bkModel.GetValue(cfg, null) : null;
                    if (model != null)
                    {
                        if (_bkLimit == null || _bkLimit.DeclaringType == null || !_bkLimit.DeclaringType.IsInstanceOfType(model))
                            _bkLimit = AccessTools.Method(model.GetType(), "GetSettlementMarketGoldLimit", new[] { typeof(Settlement) });
                        if (_bkLimit != null) return Convert.ToInt32(_bkLimit.Invoke(model, new object[] { st }));
                    }
                }
            }
            catch { _bkLimit = null; }      // model BK niedostepny (inna wersja, kampania w budowie) - liczymy ze stalych
            var town = st != null ? st.Town : null;
            if (town == null) return int.MaxValue;
            double lim = BkLimitBase + (double)Prosperity(town) * (st.IsCastle ? BkLimitCastle : BkLimitTown);
            return lim >= int.MaxValue ? int.MaxValue : (int)lim;
        }

        // ------------------------------------------------------------ nawias dziennego ticku miasta (wolane z latek CastlePurse - kroku K5)
        /// <summary>ItemConsumptionBehavior.DeleteOverproducedItems - pierwszy krok dziennego ticku osady: kasa miasta PRZED "zakupami".</summary>
        internal static void OnShelf(Town town)
        {
            try
            {
                _consTown = null;
                if (town == null || Campaign.Current == null || Mode == ModeGame) return;
                _consTown = town; _consGold = town.Gold;
            }
            catch (Exception e) { _consTown = null; Stumble("TownPurse.OnShelf", e); }
        }

        /// <summary>
        /// ItemConsumptionBehavior.MakeConsumption - po "zakupach" mieszczan: to, co konsumpcja dopisala do kasy, wraca do nicosci,
        /// z ktorej przyszlo (towar zszedl z polki jak dotad). Biegnie przed licznikiem ksiegi pieniadza - ksiega ma zobaczyc 0.
        /// </summary>
        internal static void OnConsumed(Town town)
        {
            try
            {
                var t = _consTown; _consTown = null;
                if (t == null || !ReferenceEquals(t, town)) return;
                _regDue = town;                                  // zaraz potem gra pyta model o regulator tej samej osady
                var camp = Campaign.Current;
                bool live = camp != null && camp.GameStarted;    // ticki z tworzenia nowej kampanii cofamy tak samo, ale do linii dobowej ich nie liczymy
                if (live) _dConsN++;
                if (!NoMint) return;
                int made = town.Gold - _consGold;
                if (made <= 0) return;
                town.ChangeGold(-made);
                if (live) { _dConsBack += made; _dConsHit++; }
            }
            catch (Exception e) { Stumble("TownPurse.OnConsumed", e); }
        }

        /// <summary>
        /// GetTownGoldChange dowolnego modelu kasy osad, dla miasta: kasowania nie ma nigdy; dosypka w trybie 1 zostaje, ale najwyzej
        /// do zapasu kupcow (bezpiecznik nie zasila zaworu), w trybie 2 jej nie ma. Liczymy raz na dzienny tick miasta - pytania
        /// z ekranow i drugi poziom modelu (BetterEconomy deleguje do gry) dostaja ten sam wynik, ale do licznikow nie wchodza.
        /// </summary>
        internal static void OnRegulator(Town town, ref int result)
        {
            int mode = Mode;
            var camp = Campaign.Current;
            if (mode == ModeGame || town == null || camp == null) return;
            try
            {
                bool due = ReferenceEquals(town, _regDue);
                if (result == 0) { if (due) _regDue = null; return; }
                // tworzenie nowej kampanii: gra robi dwa ticki wszystkich miast, zanim kampania ruszy (ItemConsumptionBehavior.
                // OnNewGameCreatedFollowUp). Dosypka z tych tickow bylaby czescia daru startowego, ktorej przyciecie nie zna -
                // wtedy regulator nie robi nic (nikt jeszcze niczego nie kupil, bezpiecznik nie ma czego chronic) i nic nie liczymy.
                bool creating = !camp.GameStarted;
                long wanted = result;
                long cut = 0;
                if (wanted < 0) result = 0;
                else if (mode == ModeOff || creating) { cut = wanted; result = 0; }
                else
                {
                    long room = (long)Reserve(town) - town.Gold;
                    if (room < 0) room = 0;
                    if (wanted > room) { cut = wanted - room; result = (int)room; }
                }
                if (!due) return;
                _regDue = null;
                if (creating) return;
                if (wanted < 0) { _dRegDown -= wanted; _dRegDownN++; }
                else
                {
                    if (result > 0) { _dRegUp += result; _dRegUpN++; }
                    if (cut > 0) { _dRegUpCut += cut; _dRegUpCutN++; }
                }
            }
            catch (Exception e) { Stumble("TownPurse.OnRegulator", e); }
        }

        // ------------------------------------------------------------ zawor: nadwyzka ponad zapas -> pan i korona (wolane z PopulationLaw.Daily)
        /// <summary>
        /// Zawor jednego miasta. Zwraca kwote przelana panu (do dziennych rent rodu); udzial korony idzie od razu do skarbca
        /// krolestwa. `due` - renta nalezna od ludnosci miasta (juz tylko liczba do logu). Wyjatek: to jedno miasto, licznik potkniec.
        /// </summary>
        internal static int CollectRent(Settlement st, Hero lord, float decree, float due)
        {
            try
            {
                var s = Settings.Current;
                var town = st.Town;
                _vTowns++;
                if (due > 0f && !float.IsNaN(due) && !float.IsInfinity(due)) _vDue += (long)due;
                int reserve = Reserve(town);
                long spare = (long)town.Gold - reserve;
                if (spare <= 0) { _vBelow++; return 0; }
                int draw = Portion(spare, s.TownRentShare);
                if (draw <= 0) { _vTiny++; return 0; }
                var clan = st.OwnerClan;
                var k = clan != null ? clan.Kingdom : null;
                bool crownTakes = k != null && !k.IsEliminated;
                int lordBase, crown;
                Split(draw, crownTakes ? s.TownRentLordShare : 1f, out lordBase, out crown);
                if (!crownTakes) _vNoCrown++;
                // dekret podatkowy lenna BK (Low / Standard / High / Exemption) skaluje tylko czesc pana; razem nigdy ponad nadwyzke
                double d = float.IsNaN(decree) || decree < 0f ? 0.0 : decree;
                long lordPay = (long)(lordBase * d);
                if (lordPay > spare - crown) lordPay = spare - crown;
                if (lordPay < 0) lordPay = 0;
                if (lordBase > 0 && lordPay == 0) _vExempt++;
                if (lordPay > 0)
                {
                    // przelew kasa miasta -> pan (ta sama akcja gry co dotychczasowe renty; kwota nigdy nie przekracza kasy)
                    GiveGoldAction.ApplyForSettlementToCharacter(st, lord, (int)lordPay, true);
                    _vLord += lordPay; _vPayers++;
                    if (lord == Hero.MainHero) _vPlayer += lordPay;
                }
                long gone = lordPay;
                if (crown > 0)
                {
                    // najpierw kasa, potem skarbiec: skarbiec dostaje dokladnie tyle, ile zeszlo z kasy
                    int before = town.Gold;
                    town.ChangeGold(-crown);
                    int taken = before - town.Gold;
                    if (taken > 0) { k.KingdomBudgetWallet += taken; _vCrown += taken; _vKingdoms.Add(k); gone += taken; }
                }
                _vDraw += gone;
                return (int)lordPay;
            }
            catch (Exception e) { Stumble("TownPurse.CollectRent(" + (st != null ? st.StringId : "?") + ")", e); return 0; }
        }

        // ------------------------------------------------------------ zwrot zoldu z korony a zaloga "u siebie"
        /// <summary>
        /// Czy zold tej zalogi, wplacony do kasy jej osady, wraca panu zaworem (miasto: ten modul; zamek: danina podzamcza K5) -
        /// wtedy korona go nie zwraca (zaloga to koszt pana, pokrywany z dochodow jego ziemi; korona placi za wojsko w polu).
        /// Jedna regula dla miasta i zamku (wpis 114): osada musi oddawac nadwyzke (zawor / danina czynne, stawka > 0) i pan musi
        /// miec w niej udzial > 0 - przy udziale 0 calosc bierze korona, do pana nic nie wraca, wiec zwrot korony liczy zaloge
        /// jak w ogniwie 107. Przy wylaczonym podziale daniny podzamcza udzial pana w zamku to 1 (stan sprzed wpisu 114).
        /// </summary>
        internal static bool PayComesHome(Settlement st)
        {
            try
            {
                var s = Settings.Current;
                if (s == null || st == null || st.Town == null || !s.CrownRefundSkipsHomeGarrisons || !On) return false;
                if (st.IsTown) return s.PopulationRentEnabled && Unit(s.TownRentShare) > 0f && Unit(s.TownRentLordShare) > 0f;
                if (st.IsCastle) return s.CastlePurseEnabled && Unit(s.CastleDuesShare) > 0f && CastlePurse.LordShare(s) > 0f;
            }
            catch (Exception e) { Stumble("TownPurse.PayComesHome", e); }
            return false;
        }

        /// <summary>
        /// Ile z zoldu zalogi, ktory ZARAZ wplynie do kasy jej osady, wyladuje ponad zapasem kupcow. Tylko ta czesc wraca panu
        /// zaworem (miasto) albo danina podzamcza (zamek) - i tylko ona nie jest podstawa zwrotu z korony. Czesc, ktora dopelnia
        /// kase do zapasu, do pana nie wraca nigdy (w miescie zastepuje dosypke regulatora, w zamku zostaje kupcom podzamcza):
        /// za nia zwrot korony nalezy sie jak w ogniwie 107 - inaczej pan miasta-zaulka placilby zaloge w calosci, bez zaworu i bez
        /// zwrotu. Wolac PRZED dopisaniem zoldu do kasy. Wyjatek albo wylacznik = 0 (caly zold jest podstawa zwrotu, jak dotad).
        /// </summary>
        internal static int HomePart(Settlement st, int amount)
        {
            try
            {
                if (amount <= 0 || !PayComesHome(st)) return 0;
                var town = st.Town;
                long reserve = st.IsTown ? Reserve(town) : CastlePurse.Reserve(town);
                long above = (long)town.Gold + amount - reserve;
                return above <= 0 ? 0 : (above >= amount ? amount : (int)above);
            }
            catch (Exception e) { Stumble("TownPurse.HomePart", e); return 0; }
        }

        // ------------------------------------------------------------ gorna galaz BK HandleMarketGold
        /// <summary>
        /// Prefiks na BK BKSettlementBehavior.HandleMarketGold(Settlement): gdy kasa miasta (albo zamku przy czynnym K5) stoi ponad
        /// limitem BK, pomijamy oryginal - jego gorna galaz kasuje 1% kasy dziennie (pol z kasy, pol z kiesy losowego notabla).
        /// W kazdym innym przypadku oryginal biegnie jak dotad (dolna galaz: notabl doplaca 1000 - przelew).
        /// </summary>
        public static bool BkSkimPrefix(Settlement __0)
        {
            try
            {
                if (__0 == null || Campaign.Current == null || !On) return true;
                var s = Settings.Current;
                if (!s.TownPurseNoBkSkim) return true;
                var comp = __0.SettlementComponent;
                if (comp == null || __0.Town == null) return true;
                bool castle = __0.IsCastle;
                if (!(__0.IsTown || (castle && s.CastlePurseEnabled))) return true;
                int gold = comp.Gold, limit = BkLimit(__0);
                if (gold <= limit) return true;
                if (castle) _bkSkipCastles++; else _bkSkipTowns++;
                _bkSkipGold += (long)(gold * 0.01f);
                return false;
            }
            catch (Exception e) { Stumble("TownPurse.BkSkimPrefix", e); return true; }
        }

        internal static void ApplyAll(Harmony h)
        {
            string bk = "BRAK BK BKSettlementBehavior.HandleMarketGold (bez BannerKings nie ma czego pomijac)";
            try
            {
                var t = AccessTools.TypeByName("BannerKings.Behaviours.BKSettlementBehavior");
                var m = t != null ? AccessTools.Method(t, "HandleMarketGold", new[] { typeof(Settlement) }) : null;
                if (m != null && !_bkHooked)
                {
                    h.Patch(m, prefix: new HarmonyMethod(typeof(TownPurse), nameof(BkSkimPrefix)) { priority = Priority.First });
                    _bkHooked = true;
                }
                if (_bkHooked) bk = "wpiete (prefiks na BK HandleMarketGold)";
            }
            catch (Exception e) { bk = "BRAK (" + e.Message + ")"; Log.Error("TownPurse.ApplyAll(BK)", e); }
            var s = Settings.Current;
            int mode = Mode;
            Log.Info("TownPurse: kasa miasta jako prawdziwy pieniadz " + (mode == ModeGame ? "WYLACZONA w ustawieniach (Town Purse Regulator 0 - regulator gry, pulap renty i tarcza zoldu jak dotad)"
                                                                         : "CZYNNA, tryb regulatora " + mode + (mode == ModeKeep ? " (bez kasowania; dosypka ponizej zapasu jako bezpiecznik)" : " (regulator wylaczony)"))
                     + " - regulator kas miast przez latki CastlePurse w " + CastlePurse.HookedModels + " modelach" + (CastlePurse.HookedModels > 0 ? "" : " (BRAK - bez nich cala zmiana stoi)")
                     + ", \"zakupy\" mieszczan bez zlota z niczego: " + (s != null && s.TownFolkBuyWithoutMinting ? (CastlePurse.HookedConsumption ? "tak" : "BRAK latek konsumpcji") : "nie (ustawienie)")
                     + ", gorna galaz BK (kasa ponad " + BkLimitBase + " + " + BkLimitTown.ToString("0", CultureInfo.InvariantCulture) + " x dobrobyt miasta / " + BkLimitCastle.ToString("0", CultureInfo.InvariantCulture)
                     + " x zamku: 1% dziennie w nicosc): " + (s != null && s.TownPurseNoBkSkim ? bk : "zostaje (ustawienie)")
                     + "; zapas kupcow " + (s != null ? s.TownPurseFloorGold.ToString(CultureInfo.InvariantCulture) + " + " + s.TownPurseFloorPerProsperity.ToString("0.##", CultureInfo.InvariantCulture) + " x dobrobyt" : "?")
                     + ", zawor " + (s != null ? (Unit(s.TownRentShare) * 100f).ToString("0.#", CultureInfo.InvariantCulture) : "?") + "% nadwyzki dziennie, z tego panu "
                     + (s != null ? (Unit(s.TownRentLordShare) * 100f).ToString("0.#", CultureInfo.InvariantCulture) : "?") + "% i reszta do skarbca krolestwa (bez pulapu renty naleznej)"
                     + ", dar startowy przycinany w pierwszej dobie nowej kampanii: " + (s != null && s.TownPurseTrimAtStart ? "tak" : "nie")
                     + ", zwrot zoldu z korony bez zalog, ktorych zold wraca panu z kasy osady: " + (s != null && s.CrownRefundSkipsHomeGarrisons ? "tak" : "nie") + ".");
        }

        // ------------------------------------------------------------ raz na kampanie: dar startowy (PRZED pierwszym zaworem)
        private static void TrimStartGift(Settings s)
        {
            if (_trimDone || _trimSkip || !s.TownPurseTrimAtStart) return;     // wylaczone: flagi nie zapisujemy (wlaczenie w pierwszej dobie jeszcze zadziala)
            double age = double.NaN;
            try { age = (CampaignTime.Now - Campaign.Current.Models.CampaignTimeModel.CampaignStartTime).ToDays; }
            catch (Exception e) { Log.Error("TownPurse.Age", e); }
            if (double.IsNaN(age))
            {
                _trimSkip = true;
                Log.Info("TownPurse: wieku kampanii nie da sie odczytac - dar startowy w kasach miast bez przyciecia (flaga nie zapisana).");
                return;
            }
            _trimDone = true;                                    // flaga PRZED robota: przerwanego przyciecia nie powtarzamy (drugie zdjeloby prawdziwy doplyw)
            if (age < -0.01)
            {
                Log.Info("TownPurse: kampania ma wiek ujemny (" + age.ToString("0.0", CultureInfo.InvariantCulture)
                         + " dni - data startu z innego kalendarza) - dar startowy w kasach miast BEZ przyciecia (flaga zapisana). Nadwyzke ponad zapas pobiora panowie i korona zaworem.");
                return;
            }
            // mloda kampania: regulator gry daru jeszcze nie ruszyl (albo kasowanie bylo zablokowane od startu) - przycinamy caly;
            // starszy zapis wczytany pierwszy raz z ta zmiana: tylko to, co z daru zostawil regulator (cwierc nadwyzki dziennie)
            double regDays = age <= FreshAgeDays ? 0.0 : age;
            double left = regDays > 0.0 ? Math.Pow(RegulatorKeeps, regDays) : 1.0;
            long before = 0, after = 0, cutSum = 0, reserveSum = 0; int towns = 0, cutN = 0, maxCut = 0; string maxName = null;
            foreach (var st in Settlement.All)
            {
                if (st == null || !st.IsTown || st.Town == null) continue;
                try
                {
                    var town = st.Town;
                    int gold = town.Gold, reserve = Reserve(town);
                    towns++; before += gold; reserveSum += reserve;
                    int cut = CastlePurse.StartGiftCut(gold, reserve, Prosperity(town), regDays);   // ten sam dar co w zamku: gra 20 000 + BK 40 x dobrobyt
                    if (cut > 0)
                    {
                        town.ChangeGold(-cut);
                        cutSum += cut; cutN++;
                        if (cut > maxCut) { maxCut = cut; maxName = st.Name != null ? st.Name.ToString() : st.StringId; }
                    }
                    after += town.Gold;
                }
                catch (Exception e) { Stumble("TownPurse.TrimStartGift(" + st.StringId + ")", e); }
            }
            MoneyLedger.Mark(MoneyLedger.MTownTrim);             // ksiega pieniadza: zloto w nicosc, osobna migawka (tylko licznik; wlasny try w srodku)
            Log.Info("TownPurse: " + (regDays > 0.0 ? "zapis z " + age.ToString("0.0", CultureInfo.InvariantCulture) + ". doby kampanii wczytany pierwszy raz z ta zmiana (regulator gry zostawil ok. "
                                                      + (left * 100.0).ToString("0.#", CultureInfo.InvariantCulture) + "% daru)"
                                                    : "poczatek kampanii (doba " + age.ToString("0.00", CultureInfo.InvariantCulture) + ")")
                     + " - dar startowy w kasach miast (gra 20000 + BK 40 x dobrobyt) przyciety do zapasu kupcow: miast " + towns + ", kasy " + before + " -> " + after
                     + " (zdjeto " + cutSum + " w " + cutN + " miastach, najwiecej " + maxCut + (maxName != null ? " - " + maxName : "") + "; zapas razem " + reserveSum
                     + "). To samo zloto regulator gry kasowal dotad w ok. 12 dob; doplyw od startu kampanii zostal w kasach. Flaga zapisze sie w sejwie (arm_townpurse).");
        }

        /// <summary>Raz na dobe, PRZED rentami (PopulationLaw.Daily): jednorazowe przyciecie daru startowego, zanim zawor go rozda.</summary>
        internal static void BeforeRents()
        {
            try
            {
                var s = Settings.Current;
                if (s == null || Campaign.Current == null || !On) return;
                TrimStartGift(s);
            }
            catch (Exception e) { Stumble("TownPurse.BeforeRents", e); }
        }

        // ------------------------------------------------------------ raz na dobe, PO rentach: linia "Kasy miast:"
        internal static void Daily()
        {
            var s = Settings.Current;
            if (s == null || Campaign.Current == null) return;
            int day = (int)CampaignTime.Now.ToDays;
            int mode = Mode;
            try
            {
                if (mode != ModeGame && CastlePurse.HookedModels <= 0)
                {
                    if (!_noHooksLogged)
                    {
                        _noHooksLogged = true;
                        Log.Info("Kasy miast: dzien " + day + " | Town Purse Regulator " + mode + ", ale latki regulatora nie sa wpiete (CastlePurse: BRAK) - kasy miast jak przed zmiana. (Linia raz na sesje.)");
                    }
                    return;
                }
                // ksiega pieniadza: udzial korony zszedl z kas razem z rentami - przenosimy go z pozycji "renty" do wlasnej
                if (_vCrown > 0) MoneyLedger.SplitTownMark(MoneyLedger.MRent, MoneyLedger.MTownCrown, _vCrown);
                long gold = 0, reserveSum = 0, spareSum = 0, shortSum = 0;
                int towns = 0, poor = 0, overBk = 0, maxSpare = 0; string maxName = null;
                foreach (var st in Settlement.All)
                {
                    if (st == null || !st.IsTown || st.Town == null) continue;
                    try
                    {
                        var town = st.Town;
                        int g = town.Gold, reserve = Reserve(town), spare = g - reserve;
                        towns++; gold += g; reserveSum += reserve;
                        if (spare >= 0) spareSum += spare; else shortSum -= spare;
                        if (spare > maxSpare) { maxSpare = spare; maxName = st.Name != null ? st.Name.ToString() : st.StringId; }
                        if (g < 1000) poor++;
                        if (g > BkLimitBase + (long)(Prosperity(town) * BkLimitTown)) overBk++;
                    }
                    catch (Exception e) { Stumble("TownPurse.Daily(" + st.StringId + ")", e); }
                }
                if (mode == ModeGame)
                {
                    if (!_offLogged)
                    {
                        _offLogged = true;
                        Log.Info("Kasy miast: dzien " + day + " | WYLACZONE w ustawieniach (MCM Town Purse Regulator 0) - regulator gry kasuje nadwyzki i dosypuje braki, \"zakupy\" mieszczan z niczego, renta z pulapem naleznej, tarcza zoldu jak dotad; stan "
                                 + gold + " w " + towns + " miastach. (Linia raz na sesje.)");
                    }
                    return;
                }
                string pct = (Unit(s.TownRentShare) * 100f).ToString("0.#", CultureInfo.InvariantCulture);
                Log.Info("Kasy miast: dzien " + day + " | tryb regulatora " + mode + (mode == ModeKeep ? " (bez kasowania)" : " (wylaczony)")
                         + " | stan " + gold + " w " + towns + " miastach: zapas kupcow " + reserveSum + ", ponad zapasem " + spareSum + ", do zapasu brakuje " + shortSum
                         + "; najwieksza nadwyzka " + maxSpare + (maxName != null ? " (" + maxName + ")" : "") + "; miast z kasa ponizej 1000: " + poor
                         + "; miast ponad limitem kasy BK (" + BkLimitBase + " + " + BkLimitTown.ToString("0", CultureInfo.InvariantCulture) + " x dobrobyt): " + overBk
                         + " | regulator gry: dosypal z niczego " + _dRegUp + " (" + _dRegUpN + " tickow miast - bezpiecznik ponizej zapasu), kasowanie zablokowane " + _dRegDown + " (" + _dRegDownN
                         + " tickow - tyle chcial dzis skasowac), dosypka zablokowana " + _dRegUpCut + " (" + _dRegUpCutN + " tickow)"
                         + " | \"zakupy\" mieszczan: " + (NoMint ? "zloto z niczego cofniete " + _dConsBack + " (w " + _dConsHit + " z " + _dConsN + " tickow; towar zjedzony jak dotad)"
                                                               : "zloto z niczego ZOSTAJE w kasach (Town Folk Buy Without Minting wylaczone; " + _dConsN + " tickow)")
                         + " | zawor (" + pct + "% nadwyzki ponad zapas, bez pulapu naleznej): " + (s.PopulationRentEnabled
                                ? "zeszlo " + _vDraw + " = panom " + _vLord + " z " + _vPayers + " miast" + (_vPlayer > 0 ? " (w tym rod gracza " + _vPlayer + ")" : "")
                                  + " + skarbcom krolestw " + _vCrown + " (" + _vKingdoms.Count + " krolestw); renta nalezna od ludnosci tych miast (dawny pulap, juz tylko dla porownania) " + _vDue
                                  + "; bez poboru: kasa nie ponad zapasem " + _vBelow + ", nadwyzka ponizej 1 d dziennie " + _vTiny + ", pan zwolnil miasto dekretem (zostaje sam udzial korony) " + _vExempt
                                  + ", miast bez krolestwa (calosc dla pana) " + _vNoCrown + "; miast z zywym panem " + _vTowns
                                : "NIE DZIALA - renty od ludnosci wylaczone (Population Rent Enabled); kasy miast tylko rosna")
                         + " | gorna galaz BK (1% kasy dziennie w nicosc): " + (s.TownPurseNoBkSkim ? (_bkHooked ? "pominieta w " + _bkSkipTowns + " miastach i " + _bkSkipCastles + " zamkach (BK skasowalby ok. " + _bkSkipGold + ")"
                                                                                                             : "BRAK latki")
                                                                             : "zostaje (ustawienie)")
                         + (_stumbles > 0 ? " | potkniecia (wyjatki, pierwszy w logu): " + _stumbles : "") + ".");
            }
            catch (Exception e) { Log.Error("TownPurse.Daily(log)", e); }
            finally { ClearDay(); }
        }
    }
}
