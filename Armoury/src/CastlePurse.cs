using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// PACZKA 110 - ZAWOR KASY ZAMKU (projekt etapu 2 krok B, rozdz. "110 + 112 + klucz 114"; D-1 audytu 12, Z15-1 audytu 15; galaz
    /// `paczki/110-k5-kasa-zamku` 39f5bdf przeniesiona na noc/sklad10 ze zmiana S14: regulator tylko w dol).
    /// Dotad kasa zamku byla atrapa: regulator gry (DefaultSettlementEconomyModel.GetTownGoldChange) co dobe sciagal ja do celu
    /// 10 000 + 12 x dobrobyt (cwierc roznicy dziennie - w gore z niczego, w dol w nicosc), a "zakupy" ludnosci zamku dopisywaly do niej
    /// cene zjedzonego towaru z niczego. Do kasy zamku wplywa prawdziwy zold zalogi (SoldierPay), sprzet AI i zaplaty za towar
    /// - regulator kasowal ok. 105 tys. dziennie w 130 zamkach (projekt, starsze logi). 110-p: bieg bazowy kroku A (kopia-baza120, doby 31-120)
    /// ma mniej: kasowanie 34 tys., dosypka 26 tys., "zakupy" +55 tys. dziennie, a prawdziwe przeplywy kas zamkow netto -40 tys. (nasze moduly
    /// wydaja z nich wiecej, niz wplywa) - po 110 dosypka trybu 1 moze byc wielokrotnie wieksza niz 3.7 tys. z projektu (prog w sprawdz_logi).
    ///
    /// Co robimy (wszystko pod jednym wlacznikiem CastlePurseEnabled; wylaczony = gra jak dotad):
    ///  1. Regulator: postfiks (First) na GetTownGoldChange kazdego modelu kasy osad - dla ZAMKU wynik ujemny (kasowanie nadwyzki) = 0;
    ///     dodatni (dosypka do zapasu, "tryb 1" - Z9) zostaje i jest liczony w linii "Zawor zamkow (110)" - zamyka go dopiero etap 3.
    ///  2. "Zakupy" ludnosci zamku: dwa postfiksy na ItemConsumptionBehavior (DeleteOverproducedItems = stan kasy przed,
    ///     MakeConsumption = po) - przyrost kasy z konsumpcji jest cofany. Towar schodzi z polki jak dotad: kasa zamku to jedna
    ///     kiesa kupcow podzamcza i ich klientow, wiec zakup wewnatrz niej ma saldo 0. Prefiksow nie zakladamy - latka BK na
    ///     MakeConsumption zwraca false, a Harmony pomija po niej cudze prefiksy z parametrem-obiektem; postfiksy biegna zawsze.
    ///  3. Zapas i zawor: kasa trzyma zapas kupcow (CastlePurseFloorGold + CastlePurseFloorPerProsperity x dobrobyt = dzisiejszy
    ///     cel regulatora, ok. 22 000 w typowym zamku); z nadwyzki ponad zapas schodzi co dobe CastleDuesShare (7%, jak zawor renty
    ///     miasta) - zaloga wydaje zold u ludzi pana: karczma, mlyn, kramy pod murami. Przelew kasa -> glowa rodu (GiveGoldAction),
    ///     liczony do dziennych rent rodu (powinnosci wobec korony, budzet budow, D169) i do czesci "ziemia" D stalego 169c (bez
    ///     "wlasnych" - pieniedzy rodu, ktore wrocily). Oblezony zamek nie placi. Kasa nie puchnie bez konca (stan ustalony:
    ///     zapas + doplyw / 7%) ani nie wysycha (zapas jest poza zaworem). Hak kiesy ludu (KL, etap 5): CastleDuesSuburbShare -
    ///     czesc zaworu dla podzamcza, dzis 0 (podzamcze nie ma jeszcze wlasnej kiesy - ta czesc zostaje w kasie zamku).
    ///     KLUCZ 114 (projekt etapu 2, Z15-1; galaz `paczki/114-porzadki` a14efe8 - tylko klucz i poprawki ksiegi, podzial przez wspolny
    ///     pomocnik ValveSplit.Split zamiast TownPurse.Split - S15): zawor dzielony z korona - pan CastleDuesLordShare (2/3), reszta do
    ///     skarbca jego krolestwa (bez zdarzenia gry, jak danina wojenna KingdomTreasury; korona oddaje to rodom zwrotem zoldu w wojnie);
    ///     zamek rodu bez krolestwa i wylaczony podzial (CastleDuesSplitWithCrown) - calosc dla pana. Bez podzialu pan odzyskiwal caly zold
    ///     zalogi wlasnego zamku; z podzialem zaloga "u siebie" kosztuje go co najmniej trzecia czesc zoldu. 114-p: renta i zawor MIASTA ida
    ///     dzis w 100% do pana (PopulationLaw, TownRentShare) - ten sam podzial 2/3 : 1/3 dla miast przyjdzie dopiero z 111' (etap 5, K6 OB);
    ///     do tego czasu zamek i miasto maja rozne udzialy korony (przejsciowa niespojnosc, wpisana w CHANGELOG 114-p).
    ///     114-p (Z8, 2.0b): w wojnie korona zwraca 50% zoldu zalog (CrownWageRefundGarrisons); zold zalogi zamku, ktory laduje w kasie
    ///     PONAD zapasem, wraca panu zaworem - ta czesc nie jest podstawa zwrotu (HomePart, CastleGarrisonPayComesHome), inaczej z 1 zl
    ///     zoldu wracalo ok. 1.16 zl (gracz2.py: 0.5 zwrotu + ok. 0.66 zaworu). Do 165 (zwrot bez zalog) - tylko zamki.
    ///  4. Tabory: wies, ktorej targ lezy za MarketMaxDistance, dalej wozi do zamku pana - ale tylko wtedy, gdy zamek ma ponad
    ///     zapasem dosc na caly ladunek; inaczej tabor jedzie na daleki targ (MarketRoad pyta CanPayCart). Pusty zamek nie kupuje.
    ///  5. Start nowej kampanii: dar startowy w kasach zamkow (gra 20 000 + BK 40 x dobrobyt) jest raz, w pierwszej dobie, przycinany
    ///     do zapasu. Dotad regulator kasowal go w ok. 12 dob; zostawiony splynalby zaworem do panow zamkow (ok. 5 mln z niczego).
    ///     Przycinamy najwyzej tyle, ile wynosil sam dar - to, co wplynelo od startu, zostaje. Flaga w zapisie (arm_castlepurse,
    ///     SaveText.Sync). Zapis wczytany pierwszy raz z ta zmiana (pierwsza doba po wczytaniu ma wiek >= 2): przycinamy najwyzej to, co z daru
    ///     zostawil regulator gry (nadwyzka daru x 0.75 do potegi wieku w dobach - po 12 dobach 3%, po 40 nic). 110-p: zawor czynny bez
    ///     przyciecia (CastlePurseTrimAtStart wylaczone) zapisuje dobe startu ("on:<wiek>") - przyciecie wlaczone pozniej liczy spadek daru
    ///     od regulatora tylko do tej doby, a dalej od zaworu (CastleDuesShare dziennie). Zapas kupcow nigdy ponizej celu regulatora gry.
    ///
    /// Czego NIE robimy: zaplaty za budowy i sprzet w zamku zostaja w kasie zamku (zawor je oddaje). Poza zakresem zostaja dwa przecieki
    /// BK: -1% dziennie od kasy ponad 50 000 + 12 x dobrobyt (zawor trzyma kase ponizej - linia liczy zamki ponad limitem) i cotygodniowy
    /// skup zywnosci przy pelnym spichlerzu (zloto w nicosc); oba widac jako "reszta" w linii "Przeplywy osad (kasy zamkow)".
    /// Dosypka regulatora do zapasu (tryb 1) zostaje do etapu 3 (Z9) - liczona.
    /// </summary>
    internal static class CastlePurse
    {
        private const int VanillaStartGold = 20000;          // Town: InitialTownGold, ChangeGold(20000) przy zakladaniu osady
        private const float BkStartGoldPerProsperity = 40f;  // BK BKCampaignStartBehavior.GiveTownsResources: ChangeGold((int)(Prosperity * 40f)) po kreatorze postaci
        private const int BkLimitBase = 50000;               // BK BKEconomyModel.GetSettlementMarketGoldLimit: zamek 50 000 + 12 x dobrobyt; ponad tym HandleMarketGold zdejmuje 1% dziennie
        private const float BkLimitPerProsperity = 12f;
        // 110-p: pierwszy tick dobowy nowej kampanii przypada w wieku ok. 1.0 doby (dar nieruszony - zalozenie galezi 110); zapis wczytany pierwszy
        // raz ma pierwszy tick w wieku >= 2.0 - regulator gry mial juz co najmniej dobe, wiec liczymy spadek (dotad prog 2.0 dawal takiemu zapisowi pelne przyciecie)
        private const double FreshAgeDays = 1.5;
        private const double RegulatorKeeps = 0.75;          // regulator gry zdejmuje cwierc nadwyzki dziennie (DefaultSettlementEconomyModel.GetTownGoldChange)
        // 110-p: cel regulatora gry dla kasy osady (DefaultSettlementEconomyModel.GetTownGoldChange: 10 000 + 12 x dobrobyt). Zapas kupcow nie schodzi
        // ponizej - inaczej regulator dosypywalby z niczego do swojego celu, a zawor oddawal te dosypke panom i koronom
        private const int GameTargetGold = 10000;
        private const float GameTargetPerProsperity = 12f;

        /// <summary>Hak kiesy ludu (KL, etap 5; projekt etapu 2 rozdz. 2.0b): czesc zaworu zamku dla kiesy podzamcza (KL: 15%). Do etapu 5 = 0 -
        /// podzamcze nie ma wlasnej kiesy; czesc podzamcza zostaje w kasie zamku.</summary>
        internal static readonly float CastleDuesSuburbShare = 0f;

        // ------------------------------------------------------------ stan kampanii
        private static bool _trimDone;                       // dar startowy rozliczony (zapis: arm_castlepurse "done")
        // 110-p: wiek kampanii, w ktorym zawor ruszyl BEZ przyciecia daru (CastlePurseTrimAtStart wylaczone) - od tej doby regulator nie kasowal
        // daru, bral go zawor (zapis: arm_castlepurse "on:<wiek>"); NaN = nie bylo takiej doby
        private static double _liveAge = double.NaN;
        private static bool _trimSkip;                    // wieku kampanii nie da sie odczytac - w tej sesji nie probujemy dalej (jedna linia logu)
        private static bool _offLogged;                      // linia "wylaczone" raz na sesje

        // ------------------------------------------------------------ nawias dziennego ticku zamku (konsumpcja -> regulator)
        private static Town _consTown, _regDue;
        private static int _consGold;

        // ------------------------------------------------------------ liczniki doby (linia "Zawor zamkow (110)")
        private static long _dRegUp, _dRegDown, _dConsBack, _dCartValue;
        private static int _dRegUpN, _dRegDownN, _dConsN, _dConsHit, _dCartPaid, _dCartSent, _stumbles;
        private static bool _errLogged;

        // ------------------------------------------------------------ odczyty dnia dla innych modulow (169c D staly, ksiega pieniadza)
        /// <summary>Czesc pana z zaworu dzis, na rod (to samo, co dopisane do PopulationLaw.RentToday) - 169c: "zawor zamkow" w ziemi D stalego.</summary>
        internal static readonly Dictionary<Clan, int> LordDuesToday = new Dictionary<Clan, int>();
        // na zamek: [czesc pana, nadwyzka kasy ponad zapas przed zaworem] - 169c: czesc "wlasne" (pieniadze rodu, ktore wrocily zaworem)
        private static readonly Dictionary<Settlement, long[]> _duesBy = new Dictionary<Settlement, long[]>();
        /// <summary>Liczby ostatniego Daily (ksiega pieniadza drukuje je po nas w tym samym ticku); zeruje MoneyLedger.ClearLast169 na poczatku bloku.</summary>
        internal static long LastLordPaid, LastCrownPaid, LastRegDown, LastRegUp, LastConsBack;
        /// <summary>114: udzial korony z zaworu zamkow dzis, na krolestwo (wplyw skarbca - dla 165 "wplywy dnia").</summary>
        internal static readonly Dictionary<Kingdom, long> CrownToday = new Dictionary<Kingdom, long>();
        internal static int LastRegDownN, LastRegUpN;

        internal static void Reset()
        {
            _trimDone = false; _liveAge = double.NaN; _trimSkip = false; _offLogged = false; _errLogged = false;
            _consTown = null; _regDue = null;
            LordDuesToday.Clear(); _duesBy.Clear(); CrownToday.Clear();
            ZeroLast();
            ClearDay();
        }

        internal static void ZeroLast()
        {
            LastLordPaid = LastCrownPaid = LastRegDown = LastRegUp = LastConsBack = 0;
            LastRegDownN = LastRegUpN = 0;
        }

        private static void ClearDay()
        {
            _dRegUp = _dRegDown = _dConsBack = _dCartValue = 0;
            _dRegUpN = _dRegDownN = _dConsN = _dConsHit = _dCartPaid = _dCartSent = _stumbles = 0;
        }

        internal static string Export()
        {
            if (_trimDone) return "done";
            return double.IsNaN(_liveAge) ? "" : "on:" + _liveAge.ToString("0.###", CultureInfo.InvariantCulture);
        }

        internal static void Import(string s)
        {
            _trimDone = s == "done";
            _liveAge = double.NaN;
            double a;
            if (!_trimDone && s != null && s.StartsWith("on:", StringComparison.Ordinal)
                && double.TryParse(s.Substring(3), NumberStyles.Float, CultureInfo.InvariantCulture, out a) && !double.IsNaN(a) && !double.IsInfinity(a))
                _liveAge = Math.Max(0.0, a);
        }

        private static bool On { get { var s = Settings.Current; return s != null && s.CastlePurseEnabled; } }

        /// <summary>169c (tylko odczyt): czesc pana z zaworu tego zamku dzis i nadwyzka kasy ponad zapas, z ktorej ja wzieto.</summary>
        internal static void DuesOf(Settlement st, out long lordPay, out long spare)
        {
            long[] v;
            if (st != null && _duesBy.TryGetValue(st, out v)) { lordPay = v[0]; spare = v[1]; } else { lordPay = 0; spare = 0; }
        }

        /// <summary>Wyjatek przy jednym zamku albo taborze: pierwszy do pliku, kolejne liczone w linii dobowej (mechanizmu nie gasimy).</summary>
        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            if (_errLogged) return;
            _errLogged = true;
            Log.Error(where, e);
        }

        // ------------------------------------------------------------ rachunek
        private static float Prosperity(Town town)
        {
            float p = town.Prosperity;
            return float.IsNaN(p) || float.IsInfinity(p) || p < 0f ? 0f : p;
        }

        /// <summary>
        /// Zapas kupcow podzamcza: tej czesci kasy zawor nie bierze i tabory na nia nie licza (domyslnie = cel regulatora gry).
        /// 110-p: nigdy ponizej celu regulatora gry (10 000 + 12 x dobrobyt) - ponizej niego regulator dosypuje z niczego (tryb 1), a zawor
        /// liczony od nizszego zapasu oddawalby te dosypke panom i koronom. Ustawienia moga zapas tylko podniesc.
        /// </summary>
        internal static int Reserve(Town town)
        {
            var s = Settings.Current;
            float p = Prosperity(town);
            double r = Math.Max(0, s.CastlePurseFloorGold) + (double)Math.Max(0f, s.CastlePurseFloorPerProsperity) * p;
            double game = GameTargetGold + (double)GameTargetPerProsperity * p;
            if (r < game) r = game;
            return r >= int.MaxValue ? int.MaxValue : (int)r;
        }

        /// <summary>Zawor zamku: czesc nadwyzki kasy ponad zapas, nigdy wiecej niz nadwyzka (w dol).</summary>
        internal static int Dues(int gold, int reserve, float share)
        {
            long spare = (long)gold - reserve;
            if (spare <= 0 || float.IsNaN(share) || share <= 0f) return 0;
            if (share >= 1f) return (int)Math.Min(spare, int.MaxValue);
            return (int)Math.Min(spare, (long)(spare * (double)share));
        }

        /// <summary>
        /// 114: udzial pana w zaworze zamku nalezacego do rodu w krolestwie: CastleDuesLordShare (obciete do 0..1, NaN = 0), a przy
        /// wylaczonym podziale z korona 1 (pan bierze calosc - stan sprzed 114).
        /// </summary>
        internal static float LordShare(Settings s)
        {
            if (s == null || !s.CastleDuesSplitWithCrown) return 1f;
            return ValveSplit.Unit(s.CastleDuesLordShare);
        }

        /// <summary>
        /// Ile daru startowego lezy w kasie ponad zapasem: najwyzej tyle, ile dar (gra 20 000 + BK 40 x dobrobyt) przekracza zapas,
        /// razy `left` - czesc daru, ktorej nie zdjal jeszcze regulator gry (cwierc dziennie) ani zawor (110-p: CastleDuesShare dziennie, gdy
        /// zawor biegl bez przyciecia); 1 = caly dar. Nigdy wiecej niz nadwyzka kasy ponad zapas - doplyw od startu kampanii zostaje.
        /// </summary>
        internal static int StartGiftCut(int gold, int reserve, float prosperity, double left)
        {
            long gift = VanillaStartGold + (long)(Math.Max(0f, prosperity) * BkStartGoldPerProsperity);
            if (double.IsNaN(left) || left < 0.0) left = 0.0; else if (left > 1.0) left = 1.0;
            long giftOver = (long)(Math.Max(0L, gift - reserve) * left);
            long cut = Math.Min((long)gold - reserve, giftOver);
            return cut > 0 ? (int)Math.Min(cut, int.MaxValue) : 0;
        }

        // ------------------------------------------------------------ latki
        /// <summary>ItemConsumptionBehavior.DeleteOverproducedItems - pierwszy krok dziennego ticku osady: kasa zamku PRZED "zakupami" ludnosci.</summary>
        public static void ShelfPostfix(Town __0)
        {
            try
            {
                _consTown = null;
                if (__0 == null || !__0.IsCastle || !On || Campaign.Current == null) return;
                _consTown = __0; _consGold = __0.Gold;
            }
            catch (Exception e) { _consTown = null; Stumble("CastlePurse.ShelfPostfix", e); }
        }

        /// <summary>
        /// ItemConsumptionBehavior.MakeConsumption - po "zakupach" ludnosci zamku: to, co konsumpcja dopisala do kasy, wraca do
        /// nicosci, z ktorej przyszlo (towar zszedl z polki jak dotad). First - przed licznikiem ksiegi pieniadza, ktora ma zobaczyc 0.
        /// </summary>
        public static void ConsumePostfix(Town __0)
        {
            try
            {
                var t = _consTown; _consTown = null;
                if (t == null || !ReferenceEquals(t, __0)) return;
                _regDue = __0;                                   // zaraz potem gra pyta model o regulator tej samej osady
                _dConsN++;
                int made = __0.Gold - _consGold;
                if (made <= 0) return;
                __0.ChangeGold(-made);
                _dConsBack += made; _dConsHit++;
            }
            catch (Exception e) { Stumble("CastlePurse.ConsumePostfix", e); }
        }

        /// <summary>
        /// GetTownGoldChange dowolnego modelu kasy osad (gra robi ChangeGold wynikiem). S14: dla zamku TYLKO W DOL - wynik ujemny (kasowanie
        /// nadwyzki ponad cel) = 0, dodatni (dosypka do zapasu - tryb 1, Z9) zostaje. First - przed licznikiem ksiegi pieniadza; tarcza zoldu
        /// (SoldierPay, tez First) dotyczy tylko miast. Do logu liczymy raz na dzienny tick zamku (pytania z ekranow i drugi poziom modelu - nie).
        /// </summary>
        public static void RegulatorPostfix(Town __0, ref int __result)
        {
            if (__0 == null) return;
            if (__result == 0) { if (ReferenceEquals(__0, _regDue)) _regDue = null; return; }   // gra niczego nie chciala - tick zamku rozliczony
            try
            {
                if (!__0.IsCastle || !On || Campaign.Current == null) return;
                long wanted = __result;
                if (wanted < 0) __result = 0;                    // kasowanie nadwyzki zablokowane; dosypka do zapasu przechodzi bez zmian
                if (!ReferenceEquals(__0, _regDue)) return;      // pytanie spoza dziennego ticku (ekran) albo drugi poziom modelu - nie liczymy
                _regDue = null;
                if (wanted > 0) { _dRegUp += wanted; _dRegUpN++; } else { _dRegDown -= wanted; _dRegDownN++; }
            }
            catch (Exception e) { Stumble("CastlePurse.RegulatorPostfix", e); }
        }

        internal static void ApplyAll(Harmony h)
        {
            var done = new List<string>(); var miss = new List<string>();
            try
            {
                // te same dwie metody, ktore mierzy ksiega pieniadza - nowych zalatanych metod nie przybywa
                var shelf = AccessTools.Method(typeof(ItemConsumptionBehavior), "DeleteOverproducedItems");
                var cons = AccessTools.Method(typeof(ItemConsumptionBehavior), "MakeConsumption");
                if (shelf != null && cons != null)
                {
                    h.Patch(shelf, postfix: new HarmonyMethod(typeof(CastlePurse), nameof(ShelfPostfix)));
                    h.Patch(cons, postfix: new HarmonyMethod(typeof(CastlePurse), nameof(ConsumePostfix)) { priority = Priority.First });
                    done.Add("\"zakupy\" ludnosci zamkow bez zlota z niczego");
                }
                else miss.Add("\"zakupy\" ludnosci zamkow");
            }
            catch (Exception e) { miss.Add("\"zakupy\" ludnosci zamkow (" + e.Message + ")"); }
            int reg = 0;
            try
            {
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    Type[] types;
                    try { types = asm.GetTypes(); } catch (ReflectionTypeLoadException e) { types = e.Types; } catch { continue; }
                    foreach (var t in types)
                    {
                        try
                        {
                            if (t == null || t.IsAbstract || !typeof(SettlementEconomyModel).IsAssignableFrom(t)) continue;
                            var m = t.GetMethod("GetTownGoldChange", BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly, null, new[] { typeof(Town) }, null);
                            if (m == null || m.IsAbstract) continue;
                            h.Patch(m, postfix: new HarmonyMethod(typeof(CastlePurse), nameof(RegulatorPostfix)) { priority = Priority.First });
                            reg++;
                        }
                        catch (Exception e) { miss.Add("regulator kasy w " + (t != null ? t.Name : "?") + " (" + e.Message + ")"); }
                    }
                }
            }
            catch (Exception e) { Log.Error("CastlePurse.ApplyAll(regulator)", e); }
            if (reg > 0) done.Add("regulator kasy zamku tylko w dol (kasowanie 0, dosypka do zapasu zostaje) w " + reg + " modelach"); else miss.Add("regulator kasy");
            var s = Settings.Current;
            Log.Info("CastlePurse (110): zawor kasy zamku " + (s != null && s.CastlePurseEnabled ? "CZYNNY" : "WYLACZONY w ustawieniach (latki tylko wracaja)")
                     + " - wpiete: " + (done.Count > 0 ? string.Join(", ", done.ToArray()) : "nic")
                     + (miss.Count > 0 ? "; BRAK: " + string.Join(", ", miss.ToArray()) : "")
                     + "; zapas kupcow " + (s != null ? s.CastlePurseFloorGold.ToString(CultureInfo.InvariantCulture) + " + " + s.CastlePurseFloorPerProsperity.ToString("0.##", CultureInfo.InvariantCulture) + " x dobrobyt" : "?")
                     + " (nie mniej niz cel regulatora gry " + GameTargetGold + " + " + GameTargetPerProsperity.ToString("0", CultureInfo.InvariantCulture) + " x dobrobyt)"
                     + ", zawor " + (s != null ? (s.CastleDuesShare * 100f).ToString("0.#", CultureInfo.InvariantCulture) : "?") + "% nadwyzki dziennie"
                     + (s != null && s.CastleDuesSplitWithCrown ? ", z tego panu zamku " + (LordShare(s) * 100f).ToString("0.#", CultureInfo.InvariantCulture) + "% i reszta do skarbca krolestwa (114; zamek rodu bez krolestwa: calosc dla pana)"
                                                                : " dla pana zamku (podzial z korona 114 wylaczony)")
                     + ", dar startowy przycinany w pierwszej dobie nowej kampanii: " + (s != null && s.CastlePurseTrimAtStart ? "tak" : "nie")
                     + ", tabor do zamku tylko gdy zamek ma czym zaplacic: " + (s != null && s.CastleCartsNeedCoin ? "tak" : "nie")
                     + "; hak kiesy ludu (etap 5) CastleDuesSuburbShare " + CastleDuesSuburbShare.ToString("0.##", CultureInfo.InvariantCulture) + ".");
        }

        // ------------------------------------------------------------ zwrot zoldu z korony a zaloga "u siebie" (114-p, Z8)
        /// <summary>
        /// 114-p (Z8 z 2.0b; z galezi 114 TownPurse.HomePart/PayComesHome, tylko dla zamkow): ile z zoldu zalogi, ktory ZARAZ wplynie do kasy
        /// jej zamku, wyladuje ponad zapasem kupcow. Tylko ta czesc wraca panu zaworem - i tylko ona nie jest podstawa zwrotu zoldu z korony
        /// (inaczej zaloga dawalaby panu w wojnie wiecej, niz kosztuje: 0.5 zwrotu + do 2/3 zaworu). Czesc dopelniajaca kase do zapasu nie wraca
        /// nigdy (zastepuje dosypke regulatora) - za nia zwrot zostaje jak dotad. Warunki: zawor czynny (CastlePurseEnabled, CastleDuesShare > 0),
        /// pan ma w nim udzial > 0 (bez krolestwa - calosc; w krolestwie CastleDuesLordShare przy podziale), CastleGarrisonPayComesHome.
        /// Miasta - nie (renta miasta: TownWageShield; zwrot bez zalog wprowadza 165). Wolac PRZED wplata zoldu. Wyjatek albo wylacznik = 0.
        /// </summary>
        internal static int HomePart(Settlement st, int amount)
        {
            try
            {
                var s = Settings.Current;
                if (amount <= 0 || s == null || st == null || !st.IsCastle || st.Town == null) return 0;
                if (!s.CastlePurseEnabled || !s.CastleGarrisonPayComesHome) return 0;
                if (float.IsNaN(s.CastleDuesShare) || s.CastleDuesShare <= 0f) return 0;
                var clan = st.OwnerClan;
                if (clan == null) return 0;
                var k = clan.Kingdom;
                float lordShare = s.CastleDuesSplitWithCrown && k != null && !k.IsEliminated ? LordShare(s) : 1f;
                if (lordShare <= 0f) return 0;                   // calosc zaworu bierze korona - do pana nic nie wraca, zwrot jak dotad
                long above = (long)st.Town.Gold + amount - Reserve(st.Town);
                return above <= 0 ? 0 : (above >= amount ? amount : (int)above);
            }
            catch (Exception e) { Stumble("CastlePurse.HomePart", e); return 0; }
        }

        // ------------------------------------------------------------ tabory wsi bez bliskiego targu
        /// <summary>
        /// Czy zamek ma z czego zaplacic za CALY ladunek taboru: nadwyzka kasy ponad zapas kupcow >= wartosc ladunku po cenach skupu
        /// zamku. Wolane przez MarketRoad tylko dla wsi, ktorej targ lezy za MarketMaxDistance. Wylaczone albo blad = jak dotad (true).
        /// Niczego nie zmienia - sam odczyt i licznik.
        /// </summary>
        internal static bool CanPayCart(Settlement castle, MobileParty cart)
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !s.CastlePurseEnabled || !s.CastleCartsNeedCoin) return true;
                var town = castle != null ? castle.Town : null;
                if (town == null || cart == null) return true;
                long value = LoadValue(town, cart);
                long spare = (long)town.Gold - Reserve(town);
                if (value <= 0 || spare >= value) { _dCartPaid++; return true; }
                _dCartSent++; _dCartValue += value;
                return false;
            }
            catch (Exception e) { Stumble("CastlePurse.CanPayCart", e); return true; }
        }

        /// <summary>
        /// Wartosc tego, co tabor sprzedalby w zamku, po cenach skupu zamku - ten sam wybor co w grze (SellGoodsForTradeAction):
        /// wszystko poza polowa "glow" najtanszych zwierzat jucznych, cena stosu liczona raz.
        /// </summary>
        private static long LoadValue(Town town, MobileParty cart)
        {
            var r = cart.ItemRoster;
            if (r == null) return 0;
            ItemObject pack = null; int packValue = 10000;
            for (int i = 0; i < r.Count; i++)
            {
                var it = r.GetElementCopyAtIndex(i).EquipmentElement.Item;
                if (it != null && it.ItemCategory == DefaultItemCategories.PackAnimal && it.Value < packValue) { packValue = it.Value; pack = it; }
            }
            long v = 0;
            for (int i = 0; i < r.Count; i++)
            {
                var el = r.GetElementCopyAtIndex(i);
                var it = el.EquipmentElement.Item;
                if (it == null) continue;
                int n = el.Amount;
                if (ReferenceEquals(it, pack)) n -= (int)(0.5f * cart.MemberRoster.TotalManCount);
                if (n <= 0) continue;
                int price = town.GetItemPrice(el.EquipmentElement, cart, true);
                if (price > 0) v += (long)n * price;
            }
            return v;
        }

        // ------------------------------------------------------------ raz na kampanie: dar startowy
        private static void TrimStartGift(Settings s)
        {
            if (_trimDone || _trimSkip) return;
            bool trimOn = s.CastlePurseTrimAtStart;
            if (!trimOn && !double.IsNaN(_liveAge)) return;     // przyciecie wylaczone, doba startu zaworu bez przyciecia juz zapisana
            double age = double.NaN;
            try { age = (CampaignTime.Now - Campaign.Current.Models.CampaignTimeModel.CampaignStartTime).ToDays; }
            catch (Exception e) { Log.Error("CastlePurse.Age", e); }
            if (double.IsNaN(age))
            {
                _trimSkip = true;
                Log.Info("CastlePurse (110): wieku kampanii nie da sie odczytac - dar startowy w kasach zamkow bez przyciecia (flaga nie zapisana).");
                return;
            }
            if (!trimOn)
            {
                // 110-p: zawor rusza bez przyciecia - od tej doby regulator nie kasuje daru, bierze go zawor. Zapisujemy wiek (flaga "on:<wiek>"),
                // zeby przyciecie wlaczone pozniej zdjelo tylko to, co z daru naprawde zostalo (regulator do tej doby, potem zawor)
                _liveAge = Math.Max(0.0, age);
                Log.Info("CastlePurse (110): przyciecie daru startowego WYLACZONE (MCM Castle Purse Trim At Start) - zawor czynny od "
                         + age.ToString("0.0", CultureInfo.InvariantCulture) + ". doby kampanii bez przyciecia: dar ponad zapas kupcow pobierze zawor ("
                         + (s.CastleDuesSplitWithCrown ? "panom i skarbcom krolestw" : "panom") + "). Wlaczone pozniej, przyciecie zdejmie tylko to, czego z daru nie wzial jeszcze regulator ani zawor (flaga on:wiek w sejwie).");
                return;
            }
            _trimDone = true;                                    // flaga PRZED robota: przerwanego przyciecia nie powtarzamy (drugie zdjeloby prawdziwy doplyw)
            if (age < -0.01)
            {
                Log.Info("CastlePurse (110): kampania ma wiek ujemny (" + age.ToString("0.0", CultureInfo.InvariantCulture)
                         + " dni - data startu z innego kalendarza) - dar startowy w kasach zamkow BEZ przyciecia (flaga zapisana). Nadwyzke ponad zapas pobierze zawor"
                         + (s.CastleDuesSplitWithCrown ? " - panom i skarbcom ich krolestw (114)." : "."));
                return;
            }
            // mloda kampania: regulator gry daru jeszcze nie ruszyl - przycinamy caly; starszy zapis wczytany pierwszy raz z ta zmiana:
            // tylko to, co z daru zostawil regulator (cwierc nadwyzki dziennie). 110-p: gdy zawor biegl wczesniej bez przyciecia (flaga on:<wiek>),
            // regulator kasowal dar tylko do tamtej doby, a potem dar bral zawor (CastleDuesShare nadwyzki dziennie)
            double regAge = double.IsNaN(_liveAge) ? age : Math.Min(age, _liveAge);
            double regDays = regAge <= FreshAgeDays ? 0.0 : regAge;   // kazdy zamek ma swoja pore dziennego ticku - srednio tyle tickow regulatora, ile dob
            double valveDays = double.IsNaN(_liveAge) ? 0.0 : Math.Max(0.0, age - _liveAge);
            float valveShare = float.IsNaN(s.CastleDuesShare) ? 0f : Math.Max(0f, Math.Min(1f, s.CastleDuesShare));
            double left = (regDays > 0.0 ? Math.Pow(RegulatorKeeps, regDays) : 1.0) * (valveDays > 0.0 ? Math.Pow(1.0 - valveShare, valveDays) : 1.0);
            long before = 0, after = 0, cutSum = 0, reserveSum = 0; int castles = 0, cutN = 0, maxCut = 0; string maxName = null;
            foreach (var st in Settlement.All)
            {
                if (st == null || !st.IsCastle || st.Town == null) continue;
                try
                {
                    var town = st.Town;
                    int gold = town.Gold, reserve = Reserve(town);
                    castles++; before += gold; reserveSum += reserve;
                    int cut = StartGiftCut(gold, reserve, Prosperity(town), left);
                    if (cut > 0)
                    {
                        town.ChangeGold(-cut);
                        cutSum += cut; cutN++;
                        if (cut > maxCut) { maxCut = cut; maxName = st.Name != null ? st.Name.ToString() : st.StringId; }
                    }
                    after += town.Gold;
                }
                catch (Exception e) { Stumble("CastlePurse.TrimStartGift(" + st.StringId + ")", e); }
            }
            // ksiega pieniadza: ta zmiana kas to zloto w nicosc, nie zawor - osobna migawka (poprzednia stoi tuz przed CastlePurse.Daily)
            MoneyLedger.Mark(MoneyLedger.MTrim);                 // tylko licznik; wlasny try w srodku
            Log.Info("CastlePurse (110): " + (valveDays > 0.0 ? "przyciecie wlaczone w " + age.ToString("0.0", CultureInfo.InvariantCulture) + ". dobie kampanii, zawor biegl bez przyciecia od "
                                                          + _liveAge.ToString("0.0", CultureInfo.InvariantCulture) + ". doby (regulator i zawor zostawili ok. " + (left * 100.0).ToString("0.#", CultureInfo.InvariantCulture) + "% daru)"
                                            : regDays > 0.0 ? "zapis z " + age.ToString("0.0", CultureInfo.InvariantCulture) + ". doby kampanii wczytany pierwszy raz z ta zmiana (regulator gry zostawil ok. "
                                                        + (left * 100.0).ToString("0.#", CultureInfo.InvariantCulture) + "% daru)"
                                                      : "poczatek kampanii (doba " + age.ToString("0.00", CultureInfo.InvariantCulture) + ")")
                     + " - dar startowy w kasach zamkow (gra " + VanillaStartGold + " + BK "
                     + BkStartGoldPerProsperity.ToString("0", CultureInfo.InvariantCulture) + " x dobrobyt) przyciety do zapasu kupcow: zamkow " + castles + ", kasy " + before + " -> " + after
                     + " (zdjeto " + cutSum + " w " + cutN + " zamkach, najwiecej " + maxCut + (maxName != null ? " - " + maxName : "") + "; zapas razem " + reserveSum
                     + "). To samo zloto regulator gry kasowal dotad w ok. 12 dob; doplyw od startu kampanii (zold zalog, zakupy) zostal w kasach. Flaga zapisze sie w sejwie (arm_castlepurse).");
        }

        // ------------------------------------------------------------ raz na dobe: zawor i linia "Zawor zamkow (110)"
        internal static void Daily()
        {
            LordDuesToday.Clear(); _duesBy.Clear(); CrownToday.Clear();
            var s = Settings.Current;
            if (s == null || Campaign.Current == null) return;
            int day = (int)CampaignTime.Now.ToDays - 1;          // 110-p: numer doby jak w ksiedze pieniadza ("Przeplywy osad") i w linii "Utarg wsi (112)" - doba zakonczona
            bool on = s.CastlePurseEnabled;
            if (on) { try { TrimStartGift(s); } catch (Exception e) { Stumble("CastlePurse.TrimStartGift", e); } }
            float share = float.IsNaN(s.CastleDuesShare) ? 0f : Math.Max(0f, Math.Min(1f, s.CastleDuesShare));
            float suburbShare = ValveSplit.Unit(CastleDuesSuburbShare);
            bool split = s.CastleDuesSplitWithCrown;             // 114: zawor dzielony z korona (miasta - dopiero z 111', etap 5)
            float lordShare = LordShare(s);
            long gold = 0, reserveSum = 0, spareSum = 0, shortSum = 0, paid = 0, playerPaid = 0, suburbKept = 0, crownPaid = 0, playerCrown = 0;
            int castles = 0, payers = 0, below = 0, siege = 0, noLord = 0, overBk = 0, maxSpare = 0, crownPayers = 0, noCrown = 0; string maxName = null;
            var perClan = new Dictionary<Clan, long>();          // czesc pana na rod - srednia na pana samych zamkow (prog testu B: 200-350 zl/dobe)
            foreach (var st in Settlement.All)
            {
                if (st == null || !st.IsCastle || st.Town == null) continue;
                try
                {
                    var town = st.Town;
                    castles++;
                    int reserve = Reserve(town);
                    if (on)
                    {
                        try
                        {
                            int before = town.Gold;
                            int pay = Dues(before, reserve, share);
                            var clan = st.OwnerClan;
                            var lord = clan != null ? clan.Leader : null;
                            if (before <= reserve) below++;
                            else if (lord == null || !lord.IsAlive) noLord++;
                            else if (st.IsUnderSiege) siege++;
                            else if (pay > 0)
                            {
                                // hak KL: czesc podzamcza zostaje w kasie zamku (dzis 0)
                                int suburb = suburbShare > 0f ? (int)Math.Min(pay, (long)(pay * (double)suburbShare)) : 0;
                                suburbKept += suburb;
                                // 114: podzial ValveSplit - korona (1 - udzial pana) w dol, pan reszte; suma = pay - suburb.
                                // Zamek rodu bez krolestwa i wylaczony podzial: calosc dla pana
                                var k = clan.Kingdom;
                                bool crownTakes = split && k != null && !k.IsEliminated;
                                int lordPay, crown;
                                ValveSplit.Split(pay - suburb, crownTakes ? lordShare : 1f, out lordPay, out crown);
                                if (split && !crownTakes) noCrown++;
                                if (lordPay > 0)
                                {
                                    // przelew kasa zamku -> pan (ta sama akcja gry co renty PopulationLaw; kwota nigdy nie przekracza kasy)
                                    GiveGoldAction.ApplyForSettlementToCharacter(st, lord, lordPay, true);
                                    int r0; PopulationLaw.RentToday.TryGetValue(clan, out r0); PopulationLaw.RentToday[clan] = r0 + lordPay;
                                    int l0; LordDuesToday.TryGetValue(clan, out l0); LordDuesToday[clan] = l0 + lordPay;
                                    long c0; perClan.TryGetValue(clan, out c0); perClan[clan] = c0 + lordPay;
                                    _duesBy[st] = new long[] { lordPay, (long)before - reserve };
                                    paid += lordPay; payers++;
                                    if (lord == Hero.MainHero) playerPaid += lordPay;
                                }
                                if (crown > 0)
                                {
                                    // najpierw kasa, potem skarbiec: skarbiec dostaje dokladnie tyle, ile zeszlo z kasy (bez zdarzenia gry, jak
                                    // danina wojenna KingdomTreasury); po wyjatku przy przelewie pana tu nie dochodzimy
                                    int had = town.Gold;
                                    town.ChangeGold(-crown);
                                    int taken = had - town.Gold;
                                    if (taken > 0)
                                    {
                                        k.KingdomBudgetWallet += taken; crownPaid += taken; crownPayers++;
                                        long k0; CrownToday.TryGetValue(k, out k0); CrownToday[k] = k0 + taken;
                                        if (lord == Hero.MainHero) playerCrown += taken;
                                    }
                                }
                            }
                        }
                        catch (Exception e) { Stumble("CastlePurse.Dues(" + st.StringId + ")", e); }   // ten jeden zamek; stan kasy i tak wchodzi do linii
                    }
                    int g = town.Gold, spare = g - reserve;
                    gold += g; reserveSum += reserve;
                    if (spare >= 0) spareSum += spare; else shortSum -= spare;
                    if (spare > maxSpare) { maxSpare = spare; maxName = st.Name != null ? st.Name.ToString() : st.StringId; }
                    if (g > BkLimitBase + (long)(Prosperity(town) * BkLimitPerProsperity)) overBk++;
                }
                catch (Exception e) { Stumble("CastlePurse.Daily(" + st.StringId + ")", e); }
            }
            // ksiega pieniadza: udzial korony zszedl z kas zamkow razem z czescia panow (jedna migawka MCastle zaraz po tej metodzie) - przenosimy
            // go do wlasnej pozycji (tylko licznik; wlasny try w srodku)
            if (crownPaid > 0) MoneyLedger.SplitCastleMark(MoneyLedger.MCastle, MoneyLedger.MCastleCrown, crownPaid);
            LastLordPaid = paid; LastCrownPaid = crownPaid; LastRegDown = _dRegDown; LastRegUp = _dRegUp; LastConsBack = _dConsBack; LastRegDownN = _dRegDownN; LastRegUpN = _dRegUpN;
            try
            {
                if (!on)
                {
                    if (!_offLogged)
                    {
                        _offLogged = true;
                        Log.Info("Zawor zamkow (110): dzien " + day + " | WYLACZONE w ustawieniach (MCM Castle Purse Enabled) - regulator gry i \"zakupy\" z niczego jak dotad, bez zaworu; stan "
                                 + gold + " w " + castles + " zamkach. (Linia raz na sesje.)");
                    }
                }
                else
                {
                    // pan samych zamkow: rod AI z zamkiem i bez miasta, nie najemnik, bez Innych - ta sama definicja co linia "D staly (169c)";
                    // liczony kazdy taki rod, takze bez wplywu z zaworu dzis (kasa ponizej zapasu, oblezenie)
                    long only = 0; int onlyN = 0; var onlyList = new List<long>();
                    foreach (var c in Clan.All)
                    {
                        try
                        {
                            if (c == null || c.IsEliminated || c.IsBanditFaction || c.Leader == null || c == Clan.PlayerClan || c.IsUnderMercenaryService) continue;
                            if (ClanIncomeBook.IsUndeadClan(c)) continue;
                            int towns = 0, cs = 0;
                            var fiefs = c.Fiefs;
                            if (fiefs != null) for (int i = 0; i < fiefs.Count; i++) { var f = fiefs[i]; if (f == null) continue; if (f.IsTown) towns++; else if (f.IsCastle) cs++; }
                            if (cs == 0 || towns > 0) continue;
                            long v; perClan.TryGetValue(c, out v);
                            only += v; onlyN++; onlyList.Add(v);
                        }
                        catch (Exception e) { Stumble("CastlePurse.Daily(pan zamku)", e); }
                    }
                    onlyList.Sort();
                    Log.Info("Zawor zamkow (110): dzien " + day + " | stan " + gold + " w " + castles + " zamkach: zapas kupcow " + reserveSum + ", ponad zapasem " + spareSum
                             + ", do zapasu brakuje " + shortSum + "; najwieksza nadwyzka " + maxSpare + (maxName != null ? " (" + maxName + ")" : "")
                             + "; zamki ponad limitem kasy BK (" + BkLimitBase + " + " + BkLimitPerProsperity.ToString("0", CultureInfo.InvariantCulture) + " x dobrobyt - BK kasuje tam 1% dziennie): " + overBk
                             + " | regulator gry: kasowanie nadwyzki zablokowane " + _dRegDown + " (" + _dRegDownN + " tickow zamkow), dosypka do zapasu (tryb 1, zostaje do etapu 3) " + _dRegUp + " (" + _dRegUpN + " tickow)"
                             + " | \"zakupy\" ludnosci zamkow: zloto z niczego cofniete " + _dConsBack + " (w " + _dConsHit + " z " + _dConsN + " tickow; towar zjedzony jak dotad)"
                             + " | zawor: " + paid + " do panow z " + payers + " zamkow (" + (share * 100f).ToString("0.#", CultureInfo.InvariantCulture) + "% nadwyzki ponad zapas"
                             + (playerPaid > 0 ? "; w tym rod gracza " + playerPaid : "") + (suburbKept > 0 ? "; czesc podzamcza zostala w kasach " + suburbKept : "")
                             + (split ? "; skarbcom krolestw " + crownPaid + " z " + crownPayers + " zamkow w " + CrownToday.Count + " krolestwach (114) - podzial z korona: panu "
                                        + (lordShare * 100f).ToString("0.#", CultureInfo.InvariantCulture) + "%, reszta koronie" + (playerCrown > 0 ? ", z zamkow gracza " + playerCrown : "")
                                        + ", zamkow rodow bez krolestwa z caloscia dla pana " + noCrown
                                      : "; skarbcom krolestw 0 - podzial z korona WYLACZONY w MCM (Castle Dues Split With Crown): pan bierze calosc")
                             + "); bez poboru: kasa nie ponad zapasem " + below + ", oblezone " + siege + ", bez pana " + noLord
                             + " | pan samych zamkow (" + onlyN + " rodow AI): z zaworu srednio " + (onlyN > 0 ? (only / onlyN).ToString(CultureInfo.InvariantCulture) : "-")
                             + ", mediana " + (onlyN > 0 ? onlyList[onlyN / 2].ToString(CultureInfo.InvariantCulture) : "-") + " zl dzis"
                             + " | tabory wsi z targiem za daleko: do zamku (ma czym zaplacic) " + _dCartPaid + ", na daleki targ (zamek nie mial na caly ladunek) " + _dCartSent
                             + (_dCartSent > 0 ? " - ladunki warte " + _dCartValue : "")
                             + (_stumbles > 0 ? " | potkniecia (wyjatki, pierwszy w logu): " + _stumbles : "") + ".");
                }
            }
            catch (Exception e) { Log.Error("CastlePurse.Daily(log)", e); }
            ClearDay();
        }
    }
}
