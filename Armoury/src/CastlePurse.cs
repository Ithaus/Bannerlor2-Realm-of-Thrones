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
    /// KASA ZAMKU TO PRAWDZIWY PIENIADZ (krok K5 fundamentu: docs/EKONOMIA-FUNDAMENT-2026-10-05.md rozdz. 1.3 P2, 6.3 K5 z uwagami
    /// krytyka, rozdz. 7 pytanie 5). Dotad kasa zamku byla atrapa: gra co dobe sciagala ja do celu 10 000 + 12 x dobrobyt
    /// (cwierc roznicy dziennie - w gore z niczego, w dol w nicosc), a "zakupy" ludnosci zamku dopisywaly do niej cene zjedzonego
    /// towaru z niczego. Po ogniwie 107 do tej kasy wplywa prawdziwy zold zalogi (Jeff: "zaloga wydaje zold na miejscu"), po wpisie
    /// 100 wsie zamkowe woza plon na targ miasta - wiec regulator kasowal ok. 99% tego, co wplynelo.
    ///
    /// Co robimy (wszystko pod jednym wlacznikiem CastlePurseEnabled; wylaczony = gra jak dotad):
    ///  1. Regulator: postfiks (First) na GetTownGoldChange kazdego modelu kasy osad - dla ZAMKU wynik 0. Nic z niczego, nic w nicosc.
    ///  2. "Zakupy" ludnosci zamku: dwa postfiksy na ItemConsumptionBehavior (DeleteOverproducedItems = stan kasy przed,
    ///     MakeConsumption = po) - przyrost kasy z konsumpcji jest cofany. Towar schodzi z polki jak dotad: kasa zamku to jedna
    ///     kiesa kupcow podzamcza i ich klientow, wiec zakup wewnatrz niej ma saldo 0. Prefiksow nie zakladamy - latka BK na
    ///     MakeConsumption zwraca false, a Harmony pomija po niej cudze prefiksy z parametrem-obiektem; postfiksy biegna zawsze.
    ///  3. Zapas i zawor: kasa trzyma zapas kupcow (CastlePurseFloorGold + CastlePurseFloorPerProsperity x dobrobyt = dzisiejszy
    ///     cel regulatora, ok. 22 000 w typowym zamku); z nadwyzki ponad zapas pan zamku pobiera co dobe CastleDuesShare (7%, jak
    ///     zawor renty miasta) - zaloga wydaje zold u jego ludzi: karczma, mlyn, kramy pod murami. Przelew kasa -> glowa rodu
    ///     (GiveGoldAction), liczony do dziennych rent rodu (powinnosci wobec korony, budzet budow). Oblezony zamek nie placi.
    ///     Dzieki temu kasa nie puchnie bez konca (stan ustalony: zapas + doplyw / 7%) ani nie wysycha (zapas jest poza zaworem).
    ///  4. Tabory: wies, ktorej targ lezy za MarketMaxDistance, dalej wozi do zamku pana - ale tylko wtedy, gdy zamek ma ponad
    ///     zapasem dosc na caly ladunek; inaczej tabor jedzie na daleki targ (MarketRoad pyta CanPayCart). Pusty zamek nie kupuje.
    ///  5. Start nowej kampanii: dar startowy w kasach zamkow (gra 20 000 + BK 40 x dobrobyt = 8.0 mln w 130 zamkach) jest raz,
    ///     w pierwszej dobie, przycinany do zapasu. Dzis regulator kasuje go w ok. 12 dob; zostawiony splynalby zaworem do panow
    ///     zamkow (5.1 mln z niczego). Przycinamy najwyzej tyle, ile wynosil sam dar - to, co wplynelo od startu, zostaje.
    ///     Flaga w sejwie (arm_castlepurse). Zapis starszy niz dwie doby, wczytany pierwszy raz z ta zmiana: przycinamy najwyzej
    ///     to, co z daru zostawil regulator gry (nadwyzka daru x 0.75 do potegi wieku w dobach - po 12 dobach 3%, po 40 nic).
    ///
    /// Czego NIE robimy (fundament K5 pkt c): zaplaty za budowy i sprzet w zamku zostaja w kasie zamku, nie ida do najblizszego
    /// miasta - kasa zamku ma juz odplyw (zawor, tabory, handel sprzetem miedzy osadami), a "najblizsze miasto" bywa cudze.
    /// Poza zakresem zostaja dwa przecieki BK: -1% dziennie od kasy ponad 50 000 + 12 x dobrobyt (zawor trzyma kase ponizej -
    /// linia dobowa liczy zamki ponad limitem) i cotygodniowy skup zywnosci przy pelnym spichlerzu (zloto w nicosc); oba widac
    /// jako "reszta" w linii "Przeplywy osad (kasy zamkow)".
    ///
    /// Krok K6 (TownPurse) uzywa tych samych trzech latek dla MIAST: kazdy postfiks oddaje miasto modulowi TownPurse i wraca
    /// (nowych zalatanych metod nie przybywa). K6 zamyka tez pierwszy z przeciekow BK - takze dla zamkow.
    /// </summary>
    internal static class CastlePurse
    {
        private const int VanillaStartGold = 20000;          // Town: InitialTownGold, ChangeGold(20000) przy zakladaniu osady
        private const float BkStartGoldPerProsperity = 40f;  // BK BKCampaignStartBehavior.GiveTownsResources: ChangeGold((int)(Prosperity * 40f)) po kreatorze postaci
        private const int BkLimitBase = 50000;               // BK BKEconomyModel.GetSettlementMarketGoldLimit: zamek 50 000 + 12 x dobrobyt; ponad tym HandleMarketGold zdejmuje 1% dziennie
        private const float BkLimitPerProsperity = 12f;
        private const double FreshAgeDays = 2.0;             // pierwszy tick dobowy nowej kampanii przypada w wieku 1.0 doby, zapisu z pierwszej doby - przed 2.0: regulator gry daru jeszcze nie ruszyl
        private const double RegulatorKeeps = 0.75;          // regulator gry zdejmuje cwierc nadwyzki dziennie (DefaultSettlementEconomyModel.GetTownGoldChange)

        // ------------------------------------------------------------ stan kampanii
        private static bool _trimDone;                       // dar startowy rozliczony (zapis: arm_castlepurse)
        private static bool _trimSkip;                       // wieku kampanii nie da sie odczytac - w tej sesji nie probujemy dalej (jedna linia logu)
        private static bool _offLogged;                      // linia "wylaczone" raz na sesje

        // ------------------------------------------------------------ nawias dziennego ticku zamku (konsumpcja -> regulator)
        private static Town _consTown, _regDue;
        private static int _consGold;

        // ------------------------------------------------------------ liczniki doby (linia "Kasy zamkow:")
        private static long _dRegUp, _dRegDown, _dConsBack, _dCartValue;
        private static int _dRegUpN, _dRegDownN, _dConsN, _dConsHit, _dCartPaid, _dCartSent, _stumbles;
        private static bool _errLogged;

        // ------------------------------------------------------------ wpiecie (raz na proces - to nie jest stan kampanii); czyta je TownPurse (krok K6)
        internal static int HookedModels;                    // w ilu modelach kasy osad siedzi nasz postfiks regulatora
        internal static bool HookedConsumption;              // para postfiksow na konsumpcji osady zalozona

        internal static void Reset()
        {
            _trimDone = false; _trimSkip = false; _offLogged = false; _errLogged = false;
            _consTown = null; _regDue = null;
            ClearDay();
        }

        private static void ClearDay()
        {
            _dRegUp = _dRegDown = _dConsBack = _dCartValue = 0;
            _dRegUpN = _dRegDownN = _dConsN = _dConsHit = _dCartPaid = _dCartSent = _stumbles = 0;
        }

        internal static string Export() { return _trimDone ? "done" : ""; }
        internal static void Import(string s) { _trimDone = s == "done"; }

        private static bool On { get { var s = Settings.Current; return s != null && s.CastlePurseEnabled; } }

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

        /// <summary>Zapas kupcow podzamcza: tej czesci kasy pan nie pobiera i tabory na nia nie licza (domyslnie = cel regulatora gry).</summary>
        internal static int Reserve(Town town)
        {
            var s = Settings.Current;
            double r = Math.Max(0, s.CastlePurseFloorGold) + (double)Math.Max(0f, s.CastlePurseFloorPerProsperity) * Prosperity(town);
            return r >= int.MaxValue ? int.MaxValue : (int)r;
        }

        /// <summary>Danina podzamcza: czesc nadwyzki kasy ponad zapas, nigdy wiecej niz nadwyzka (w dol).</summary>
        internal static int Dues(int gold, int reserve, float share)
        {
            long spare = (long)gold - reserve;
            if (spare <= 0 || float.IsNaN(share) || share <= 0f) return 0;
            if (share >= 1f) return (int)Math.Min(spare, int.MaxValue);
            return (int)Math.Min(spare, (long)(spare * (double)share));
        }

        /// <summary>
        /// Ile daru startowego lezy w kasie ponad zapasem: najwyzej tyle, ile dar (gra 20 000 + BK 40 x dobrobyt) przekracza zapas,
        /// pomniejszone o to, co regulator gry zdazyl skasowac przez regulatorDays dob (cwierc nadwyzki dziennie; 0 = nic).
        /// Nigdy wiecej niz nadwyzka kasy ponad zapas - doplyw od startu kampanii zostaje.
        /// </summary>
        internal static int StartGiftCut(int gold, int reserve, float prosperity, double regulatorDays)
        {
            long gift = VanillaStartGold + (long)(Math.Max(0f, prosperity) * BkStartGoldPerProsperity);
            double left = regulatorDays > 0.0 ? Math.Pow(RegulatorKeeps, regulatorDays) : 1.0;
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
                if (__0 != null && __0.IsTown) { TownPurse.OnShelf(__0); return; }   // krok K6: miasto ma wlasny nawias (wlasny try w srodku)
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
                if (__0 != null && __0.IsTown) { TownPurse.OnConsumed(__0); return; }   // krok K6
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
        /// GetTownGoldChange dowolnego modelu kasy osad: dla zamku 0 (gra robi ChangeGold wynikiem). First - przed licznikiem ksiegi
        /// pieniadza; tarcza zoldu (SoldierPay, tez First) dotyczy tylko miast. Do logu liczymy to, czego gra chciala, raz na tick zamku.
        /// </summary>
        public static void RegulatorPostfix(Town __0, ref int __result)
        {
            if (__0 == null) return;
            if (__0.IsTown) { TownPurse.OnRegulator(__0, ref __result); return; }   // krok K6: regulator kasy MIASTA (wlasny try w srodku)
            if (__result == 0) { if (ReferenceEquals(__0, _regDue)) _regDue = null; return; }   // gra niczego nie chciala - tick zamku rozliczony
            try
            {
                if (!__0.IsCastle || !On || Campaign.Current == null) return;
                long wanted = __result;
                __result = 0;
                if (!ReferenceEquals(__0, _regDue)) return;      // pytanie spoza dziennego ticku (ekran) albo drugi poziom modelu - zerujemy, nie liczymy
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
                // te same dwie metody, ktore od wpisu 102 mierzy ksiega pieniadza - nowych zalatanych metod nie przybywa
                var shelf = AccessTools.Method(typeof(ItemConsumptionBehavior), "DeleteOverproducedItems");
                var cons = AccessTools.Method(typeof(ItemConsumptionBehavior), "MakeConsumption");
                if (shelf != null && cons != null)
                {
                    h.Patch(shelf, postfix: new HarmonyMethod(typeof(CastlePurse), nameof(ShelfPostfix)));
                    h.Patch(cons, postfix: new HarmonyMethod(typeof(CastlePurse), nameof(ConsumePostfix)) { priority = Priority.First });
                    HookedConsumption = true;
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
            HookedModels = reg;
            if (reg > 0) done.Add("regulator kasy zamku = 0 w " + reg + " modelach"); else miss.Add("regulator kasy");
            var s = Settings.Current;
            Log.Info("CastlePurse: kasa zamku jako prawdziwy pieniadz " + (s != null && s.CastlePurseEnabled ? "CZYNNA" : "WYLACZONA w ustawieniach (latki tylko wracaja)")
                     + " - wpiete: " + (done.Count > 0 ? string.Join(", ", done.ToArray()) : "nic")
                     + (miss.Count > 0 ? "; BRAK: " + string.Join(", ", miss.ToArray()) : "")
                     + "; zapas kupcow " + (s != null ? s.CastlePurseFloorGold.ToString(CultureInfo.InvariantCulture) + " + " + s.CastlePurseFloorPerProsperity.ToString("0.##", CultureInfo.InvariantCulture) + " x dobrobyt" : "?")
                     + ", danina podzamcza " + (s != null ? (s.CastleDuesShare * 100f).ToString("0.#", CultureInfo.InvariantCulture) : "?") + "% nadwyzki dziennie dla pana zamku"
                     + ", dar startowy przycinany w pierwszej dobie nowej kampanii: " + (s != null && s.CastlePurseTrimAtStart ? "tak" : "nie")
                     + ", tabor do zamku tylko gdy zamek ma czym zaplacic: " + (s != null && s.CastleCartsNeedCoin ? "tak" : "nie") + ".");
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
            if (_trimDone || _trimSkip || !s.CastlePurseTrimAtStart) return;   // wylaczone: flagi nie zapisujemy (wlaczenie w pierwszej dobie jeszcze zadziala)
            double age = double.NaN;
            try { age = (CampaignTime.Now - Campaign.Current.Models.CampaignTimeModel.CampaignStartTime).ToDays; }
            catch (Exception e) { Log.Error("CastlePurse.Age", e); }
            if (double.IsNaN(age))
            {
                _trimSkip = true;
                Log.Info("CastlePurse: wieku kampanii nie da sie odczytac - dar startowy w kasach zamkow bez przyciecia (flaga nie zapisana).");
                return;
            }
            _trimDone = true;                                    // flaga PRZED robota: przerwanego przyciecia nie powtarzamy (drugie zdjeloby prawdziwy doplyw)
            if (age < -0.01)
            {
                Log.Info("CastlePurse: kampania ma wiek ujemny (" + age.ToString("0.0", CultureInfo.InvariantCulture)
                         + " dni - data startu z innego kalendarza) - dar startowy w kasach zamkow BEZ przyciecia (flaga zapisana). Nadwyzke ponad zapas pobiora panowie zaworem.");
                return;
            }
            // mloda kampania: regulator gry daru jeszcze nie ruszyl (albo byl zablokowany od startu) - przycinamy caly; starszy zapis
            // wczytany pierwszy raz z ta zmiana: tylko to, co z daru zostawil regulator (cwierc nadwyzki dziennie)
            double regDays = age <= FreshAgeDays ? 0.0 : age;   // kazdy zamek ma swoja pore dziennego ticku - srednio tyle tickow regulatora, ile dob
            double left = regDays > 0.0 ? Math.Pow(RegulatorKeeps, regDays) : 1.0;
            long before = 0, after = 0, cutSum = 0, reserveSum = 0; int castles = 0, cutN = 0, maxCut = 0; string maxName = null;
            foreach (var st in Settlement.All)
            {
                if (st == null || !st.IsCastle || st.Town == null) continue;
                try
                {
                    var town = st.Town;
                    int gold = town.Gold, reserve = Reserve(town);
                    castles++; before += gold; reserveSum += reserve;
                    int cut = StartGiftCut(gold, reserve, Prosperity(town), regDays);
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
            // ksiega pieniadza: ta zmiana kas to zloto w nicosc, nie danina - osobna migawka (poprzednia stoi tuz przed CastlePurse.Daily)
            MoneyLedger.Mark(MoneyLedger.MTrim);                 // tylko licznik; wlasny try w srodku
            Log.Info("CastlePurse: " + (regDays > 0.0 ? "zapis z " + age.ToString("0.0", CultureInfo.InvariantCulture) + ". doby kampanii wczytany pierwszy raz z ta zmiana (regulator gry zostawil ok. "
                                                        + (left * 100.0).ToString("0.#", CultureInfo.InvariantCulture) + "% daru)"
                                                      : "poczatek kampanii (doba " + age.ToString("0.00", CultureInfo.InvariantCulture) + ")")
                     + " - dar startowy w kasach zamkow (gra " + VanillaStartGold + " + BK "
                     + BkStartGoldPerProsperity.ToString("0", CultureInfo.InvariantCulture) + " x dobrobyt) przyciety do zapasu kupcow: zamkow " + castles + ", kasy " + before + " -> " + after
                     + " (zdjeto " + cutSum + " w " + cutN + " zamkach, najwiecej " + maxCut + (maxName != null ? " - " + maxName : "") + "; zapas razem " + reserveSum
                     + "). To samo zloto regulator gry kasowal dotad w ok. 12 dob; doplyw od startu kampanii (zold zalog, zakupy) zostal w kasach. Flaga zapisze sie w sejwie (arm_castlepurse).");
        }

        // ------------------------------------------------------------ raz na dobe: danina podzamcza i linia "Kasy zamkow:"
        internal static void Daily()
        {
            var s = Settings.Current;
            if (s == null || Campaign.Current == null) return;
            int day = (int)CampaignTime.Now.ToDays;
            bool on = s.CastlePurseEnabled;
            if (on) { try { TrimStartGift(s); } catch (Exception e) { Stumble("CastlePurse.TrimStartGift", e); } }
            float share = float.IsNaN(s.CastleDuesShare) ? 0f : Math.Max(0f, Math.Min(1f, s.CastleDuesShare));
            long gold = 0, reserveSum = 0, spareSum = 0, shortSum = 0, paid = 0, playerPaid = 0;
            int castles = 0, payers = 0, below = 0, siege = 0, noLord = 0, overBk = 0, maxSpare = 0; string maxName = null;
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
                                // przelew kasa zamku -> pan (ta sama akcja gry co renty PopulationLaw; kwota nigdy nie przekracza kasy)
                                GiveGoldAction.ApplyForSettlementToCharacter(st, lord, pay, true);
                                int r0; PopulationLaw.RentToday.TryGetValue(clan, out r0); PopulationLaw.RentToday[clan] = r0 + pay;
                                paid += pay; payers++;
                                if (lord == Hero.MainHero) playerPaid += pay;
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
            try
            {
                if (!on)
                {
                    if (!_offLogged)
                    {
                        _offLogged = true;
                        Log.Info("Kasy zamkow: dzien " + day + " | WYLACZONE w ustawieniach (MCM Castle Purse Enabled) - regulator gry i \"zakupy\" z niczego jak dotad, bez daniny podzamcza; stan "
                                 + gold + " w " + castles + " zamkach. (Linia raz na sesje.)");
                    }
                }
                else
                    Log.Info("Kasy zamkow: dzien " + day + " | stan " + gold + " w " + castles + " zamkach: zapas kupcow " + reserveSum + ", ponad zapasem " + spareSum
                             + ", do zapasu brakuje " + shortSum + "; najwieksza nadwyzka " + maxSpare + (maxName != null ? " (" + maxName + ")" : "")
                             + "; zamki ponad limitem kasy BK (" + BkLimitBase + " + " + BkLimitPerProsperity.ToString("0", CultureInfo.InvariantCulture) + " x dobrobyt - BK kasuje tam 1% dziennie): " + overBk
                             + " | regulator gry zablokowany: z dzisiejszych kas chcial dosypac z niczego " + _dRegUp + " (" + _dRegUpN + " tickow zamkow) i skasowac " + _dRegDown + " (" + _dRegDownN + " tickow)"
                             + " | \"zakupy\" ludnosci zamkow: zloto z niczego cofniete " + _dConsBack + " (w " + _dConsHit + " z " + _dConsN + " tickow; towar zjedzony jak dotad)"
                             + " | danina podzamcza: " + paid + " do panow z " + payers + " zamkow (" + (share * 100f).ToString("0.#", CultureInfo.InvariantCulture) + "% nadwyzki ponad zapas"
                             + (playerPaid > 0 ? "; w tym rod gracza " + playerPaid : "") + "); bez poboru: kasa nie ponad zapasem " + below + ", oblezone " + siege + ", bez pana " + noLord
                             + " | tabory wsi z targiem za daleko: do zamku (ma czym zaplacic) " + _dCartPaid + ", na daleki targ (zamek nie mial na caly ladunek) " + _dCartSent
                             + (_dCartSent > 0 ? " - ladunki warte " + _dCartValue : "")
                             + (_stumbles > 0 ? " | potkniecia (wyjatki, pierwszy w logu): " + _stumbles : "") + ".");
            }
            catch (Exception e) { Log.Error("CastlePurse.Daily(log)", e); }
            ClearDay();
        }
    }
}
