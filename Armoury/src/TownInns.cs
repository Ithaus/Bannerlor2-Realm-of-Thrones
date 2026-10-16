using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Inventory;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// KA - KARCZMY W MIESCIE (16.10, Jeff: "w miescie moge spac tylko w karczmie, daj do wyboru trzy jakosci karczmy i daj pomysl jakie sa bonusy za kazda;
    /// ludzie sami placa za siebie i za swoje konie, dostaja zold, ja place za siebie i za konie, ktore nie sa na ludziach"; wczesniej: "nocleg w miescie czy
    /// tawernie kosztuje, ale daje wyspanie w lozku +10% do predkosci poruszania sie + 5 do morale"; projekt zatwierdzony, klucze TownInns, TownInnPriceBonus).
    ///
    /// T1 Menu miasta "Take lodgings at an inn" (tylko miasta) -> trzy jakosci; linia opcji = caly rachunek i kto placi, podpowiedz = szczegoly:
    ///   0 wspolna izba (sloma)                 czlowiek 1, kon 1, pokoj gracza 3  | do zmierzchu +5% predkosci na mapie, +2 morale
    ///   1 porzadna karczma (lozka, cieple, ale)  2 / 2 / 8                         | +10%, +5 morale, sen x1.25 (dlug snu splaca sie szybciej)
    ///   2 dobry dom (pokoje, mieso, wino, laznia) 4 / 3 / 20                       | +12%, +8 morale, sen x1.25, gojenie rannych +50% tej nocy, ceny gracza
    ///                                                                                w TYM miescie do zmierzchu: kupno -3%, sprzedaz +3% (klucz TownInnPriceBonus)
    ///   Wybor = zaplata, potem ten sam sen w menu co w obozie (NightRest, arm_sleep_wait): pobudka nie przed switem (gdy swit najwyzej 12 h przed nami - nocleg
    ///   wziety w dzien to zwykly sen "do wyspania"), potem stan "wypoczety" od pobudki do najblizszego zmierzchu (18:00). Premia tylko bez dlugu snu (NightRest:
    ///   kara dlugu i premia nigdy naraz). Pobudka przed przespaniem bazy (SleepHoursNeeded) i bez pelnego paska - bez premii (zaplacone przepada u karczmarza).
    /// T2 Kto placi: kazdy szeregowy z sakiewki ludzi (MenPurse - wolna czesc po rezerwie napraw, odziezy i oszczednosciach K1, jak markietani) za siebie i swojego
    ///   konia (IsMounted - ta sama regula co zold konnych 160); sakiewka jest wspolna dla druzyny, wiec rozdzial "kogo stac": najpierw kazdy dostaje wspolna izbe
    ///   (piesi przed konnymi - taniej), potem z reszty kolejni wyzej, do wybranej jakosci; kogo nie stac na nic - stajnia za darmo (jak oboz). Towarzysze (wszyscy
    ///   bohaterowie druzyny poza graczem) z wlasnej kiesy (Hero.Gold) za siebie i swojego konia, kazdy osobno: wybrana albo najlepsza tansza, na ktora go stac,
    ///   albo stajnia. Gracz za siebie (pokoj), swojego konia i konie bez jezdzca (luzaki i juczne z taboru gracza - ItemRoster, bez zywego inwentarza) w wybranej
    ///   jakosci; gracza nie stac - opcja nieczynna. Premia druzyny wazona liczba osob w kazdej jakosci (stajnia = 0). W oknie lupu (CitySack L3 - wojsko krolestwa
    ///   zdobywcy nie placi) szeregowi spia w wybranej jakosci bez zaplaty, zloto zostaje w sakiewce.
    ///   Zloto do kiesy ludu miasta (karczmarze, uslugi KLife): szeregowi MenPurse.InnPay (TownFolk.Earn calosci), bohaterowie GiveGoldAction do kasy miasta
    ///   i TownFolk.EarnFromTown (droga oplat gracza PlayerFees8); kiesa ludu nieczynna - zostaje w kasie miasta. Kontrola obiegu przy kazdym noclegu: suma
    ///   posiadaczy (sakiewki ludzi, kiesy gracza i towarzyszy, kasa miasta, kiesy ludu miast i gminy) przed i po - roznica 0.
    /// T3 Sen w miescie bez noclegu - bez zmian (NightRest: postoj pod dachem jak oboz, baza bez premii). Dom gracza: POMINIETY - dom jest w RealisticCaptivity
    ///   (Homes.Vault, pole internal innego DLL), Armoury nie ma do niego API; refleksja w cudze pole to nie "czyste API" (decyzja projektu).
    /// T4 Zapis "arm_inns" (SaveText.Sync, wlasny try): tylko stan noclegu - faza (1 oplacony sen, 2 wypoczety), jakosc, miasto, godziny, premie. Brak klucza = brak.
    /// T5 Linie "Karczmy (KA):" - latki, start sesji, kazdy nocleg, sen, pobudka, doba; tools/sprawdz_logi.py --grupa nkarczmy.
    /// Ceny (T1, dobry dom): tylko ekran handlu gracza z TYM miastem - prefiks / finalizer na InventoryLogic.GetItemPrice ustawiaja flage watku, postfiks na
    /// TownMarketData.GetPrice zmienia cene tylko pod ta flaga (sciezka rynku; cena z historii transakcji ekranu - odkup tej samej sztuki - bez zmiany, wiec bez
    /// kola zysku). Sprzedaz nigdy nie drozej niz kupno tej samej sztuki z premia minus 1. Nasze zakupy i sprzedaze ludzi (MenPurse, MarketData wprost), AI
    /// i model cen nietknieci - rdzen ekonomii bez zmian, zloto dalej gracz <-> kasa miasta jak w kazdym handlu.
    /// Gojenie (dobry dom): postfiks na DefaultPartyHealingModel (te same metody co SlowHealing) - partia gracza, tylko w czasie snu oplaconego noclegu,
    /// wynik x (1 + 0.5 x udzial osob w dobrym domu); kary (wynik <= 0) bez zmian.
    /// Predkosc i morale: NightRest.SpeedPostfix / MoralePostfix (te same latki co kara snu, raz na lancuch modeli - SpeedDepth) wolaja SpeedBonus / MoraleBonus,
    /// gdy dlug gracza 0. Predkosc liczona rownolegle - stan noclegu to obiekt podmieniany w calosci (_rest), czytany bez zmian.
    /// Wyjatek w naszym kodzie - licznik potkniec (pierwsze do Log.Error), nic nie gasimy.
    /// </summary>
    internal static class TownInns
    {
        private const string P = "Karczmy (KA): ";
        internal const string Menu = "arm_inn";
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        // jakosci 0..2 (-1 = stajnia, darmo jak oboz)
        private static readonly string[] QName = { "Common room", "Decent inn", "Good house" };
        private static readonly string[] QWhat = { "straw and a roof", "beds, a hot meal and ale", "rooms, meat, wine and a bath" };
        private static readonly string[] QLog = { "wspolna izba", "porzadna karczma", "dobry dom" };
        private static readonly int[] PMan = { 1, 2, 4 }, PHorse = { 1, 2, 3 }, PRoom = { 3, 8, 20 };
        private static readonly float[] QSpeed = { 5f, 10f, 12f }, QMorale = { 2f, 5f, 8f }, QSleep = { 1f, 1.25f, 1.25f }, QHeal = { 0f, 0f, 0.5f };
        private const int DuskHour = 18;             // zmierzch (NightRest.DayPart: 18-21)
        private const float PriceStep = 0.03f;       // dobry dom: kupno -3%, sprzedaz +3%
        private const long HoldMaxMin = 12 * 60;     // czekanie na swit tylko, gdy swit najwyzej 12 h przed nami
        private const long DropAwakeMin = 120;       // oplacony nocleg bez snu w menu dluzej niz 2 h - stan zdjety (log)

        internal static bool On { get { var s = Settings.Current; return s != null && s.TownInns && s.NightRestEnabled; } }
        private static bool PriceKey { get { var s = Settings.Current; return s != null && s.TownInnPriceBonus; } }

        // ------------------------------------------------------------ stan noclegu (zapis arm_inns)
        /// <summary>Faza 1 - nocleg oplacony, sen w menu; faza 2 - wypoczety do UntilMin. Pola premii stale; zmiana fazy = nowy obiekt (predkosc czyta rownolegle).</summary>
        private sealed class Rest
        {
            internal int Phase, Q, PlayerQ;
            internal string TownId, TownName;
            internal long CommitMin, StartMin, WakeMin, UntilMin;   // minuty kampanii: zaplata, poczatek snu, pobudka nie przed (0 = bez czekania), koniec premii
            internal float Speed, Morale, Sleep = 1f, Heal;
            internal bool Price;                                    // gracz spal w dobrym domu - ceny w tym miescie (klucz TownInnPriceBonus czytany na zywo)

            /// <summary>Nazwa miasta do logu i komunikatow (po wczytaniu szukana przy pierwszym uzyciu, na watku glownym).</summary>
            internal string Town
            {
                get
                {
                    if (TownName == null)
                    {
                        try { var st = Settlement.Find(TownId); TownName = st != null ? st.Name.ToString() : TownId; }
                        catch { TownName = TownId; }
                    }
                    return TownName;
                }
            }

            internal Rest Copy(int phase)
            {
                return new Rest { Phase = phase, Q = Q, PlayerQ = PlayerQ, TownId = TownId, TownName = TownName, CommitMin = CommitMin, StartMin = StartMin,
                                  WakeMin = WakeMin, UntilMin = UntilMin, Speed = Speed, Morale = Morale, Sleep = Sleep, Heal = Heal, Price = Price };
            }
        }
        private static volatile Rest _rest;

        // liczniki doby (linia "dzien") i sesji
        private static int _dStays, _dHorses, _dWoke, _dNoRest, _dDropped, _dHealH, _dBuyN, _dSellN, _stumbles, _stumblesDay, _priceHooks, _healHooks;
        private static readonly int[] _dPeople = new int[4];
        private static long _dMen, _dComp, _dPlayer, _dFolk, _dCoffer, _dDiff, _dBuy, _dSell;
        private static bool _errLogged;
        // wolna sakiewka ludzi do podgladu w menu (TroopSelfMend.OutstandingCost nie jest tani) - liczona raz na minute gry; zaplata liczy od nowa
        private static long _freeAt = -1; private static int _freeCache, _purseCache;

        private static void Stumble(string where, Exception e)
        {
            _stumbles++; _stumblesDay++;
            if (!_errLogged) { _errLogged = true; Log.Error("TownInns." + where, e); }
        }

        /// <summary>Nowa kampania albo wczytanie (konstruktor ArmouryBehavior, PRZED SyncData): stan noclegu i liczniki od zera.</summary>
        internal static void Reset()
        {
            _rest = null;
            DayReset();
            _stumbles = 0; _errLogged = false; _freeAt = -1;
        }

        private static void DayReset()
        {
            _dStays = _dHorses = _dWoke = _dNoRest = _dDropped = _dHealH = _dBuyN = _dSellN = _stumblesDay = 0;
            Array.Clear(_dPeople, 0, 4);
            _dMen = _dComp = _dPlayer = _dFolk = _dCoffer = _dDiff = _dBuy = _dSell = 0;
        }

        // ------------------------------------------------------------ czas
        private static long NowMin() { return (long)CampaignTime.Now.ToMinutes; }
        private static long Mod(long a, long m) { long r = a % m; return r < 0 ? r + m : r; }
        private static long NextAt(long now, int hour) { long d = now - Mod(now, 1440) + hour * 60L; return d <= now ? d + 1440 : d; }
        private static string When(long min) { return (min / 1440).ToString(Inv) + " " + (Mod(min, 1440) / 60).ToString("00", Inv) + ":" + Mod(min, 60).ToString("00", Inv); }
        private static string Clock(long min) { return (Mod(min, 1440) / 60).ToString("00", Inv) + ":" + Mod(min, 60).ToString("00", Inv); }
        private static string F1(float v) { return v.ToString("0.0", Inv); }

        // ------------------------------------------------------------ rachunek
        private sealed class Bill
        {
            internal int Q, Foot, Mounted, MenCost, MenFree, MenPurse, CompCostSum, Spare, PlayerCost, Persons;
            internal readonly int[] FootAt = new int[3], MountAt = new int[3];
            internal readonly int[] People = new int[4];          // 0 stajnia, 1..3 = jakosc 0..2 (szeregowi, towarzysze, gracz)
            internal bool MenNoPurse, OwnHorse, PlayerCan, Sacked;
            internal readonly List<Hero> Comp = new List<Hero>();
            internal readonly List<int> CompQ = new List<int>(), CompCost = new List<int>();
            internal int Total { get { return MenCost + CompCostSum + PlayerCost; } }
            internal int MenStable { get { int n = Foot + Mounted; for (int l = 0; l < 3; l++) n -= FootAt[l] + MountAt[l]; return n; } }
        }

        private static int ManCost(int q, bool horse) { return q < 0 ? 0 : PMan[q] + (horse ? PHorse[q] : 0); }

        private static bool HasMount(Hero h)
        {
            try { var eq = h != null ? h.BattleEquipment : null; return eq != null && !eq[EquipmentIndex.Horse].IsEmpty; }
            catch { return false; }
        }

        /// <summary>Konie bez jezdzca: wierzchowce i juczne w taborze gracza (ItemRoster), bez zywego inwentarza.</summary>
        private static int SpareHorses(MobileParty mp)
        {
            int n = 0;
            var r = mp != null ? mp.ItemRoster : null;
            if (r == null) return 0;
            for (int i = 0; i < r.Count; i++)
            {
                var el = r.GetElementCopyAtIndex(i);
                var it = el.EquipmentElement.Item;
                if (it == null || el.Amount <= 0 || it.ItemType != ItemObject.ItemTypeEnum.Horse || it.HorseComponent == null || it.HorseComponent.IsLiveStock) continue;
                n += el.Amount;
            }
            return n;
        }

        /// <summary>Wolna sakiewka ludzi gracza: po rezerwie napraw (z szacunkiem materialu z polki tego miasta, jak OnLeft), odziezy i oszczednosciach K1.</summary>
        private static int MenFree(MobileParty mp, Settlement st, bool fresh, out int purse)
        {
            long now = NowMin();
            if (!fresh && _freeAt == now) { purse = _purseCache; return _freeCache; }
            purse = MenPurse.Get(mp);
            int res, kept;
            long mend = Math.Max(0, TroopSelfMend.OutstandingCost(st));
            int free = MenPurse.CampFree(mp, mend, 1f, out res, out kept);
            _freeAt = now; _freeCache = free; _purseCache = purse;
            return free;
        }

        /// <summary>Rozdzial szeregowych: kazdy poziom od wspolnej izby do wybranego - najpierw piesi (taniej), potem konni; kto nie wszedl - zostaje nizej.</summary>
        private static void Allocate(Bill b, int q, long budget)
        {
            int fPrev = b.Foot, mPrev = b.Mounted;
            long left = Math.Max(0, budget);
            for (int l = 0; l <= q; l++)
            {
                int dF = ManCost(l, false) - ManCost(l - 1, false), dM = ManCost(l, true) - ManCost(l - 1, true);
                int nF = dF > 0 ? (int)Math.Min(fPrev, left / dF) : fPrev; left -= (long)nF * dF;
                int nM = dM > 0 ? (int)Math.Min(mPrev, left / dM) : mPrev; left -= (long)nM * dM;
                b.FootAt[l] = nF; b.MountAt[l] = nM;
                if (l > 0) { b.FootAt[l - 1] -= nF; b.MountAt[l - 1] -= nM; }
                fPrev = nF; mPrev = nM;
            }
            b.MenCost = 0;
            for (int l = 0; l <= q; l++) b.MenCost += b.FootAt[l] * ManCost(l, false) + b.MountAt[l] * ManCost(l, true);
        }

        private static Bill Plan(Settlement st, int q, bool fresh)
        {
            var b = new Bill { Q = q };
            var mp = MobileParty.MainParty;
            var me = Hero.MainHero;
            var roster = mp.MemberRoster;
            for (int i = 0; i < roster.Count; i++)
            {
                var el = roster.GetElementCopyAtIndex(i);
                var c = el.Character;
                if (c == null || el.Number <= 0) continue;
                if (c.IsHero)
                {
                    var h = c.HeroObject;
                    if (h != null && h != me && h.IsAlive) b.Comp.Add(h);
                    continue;
                }
                if (c.IsMounted) b.Mounted += el.Number; else b.Foot += el.Number;
            }
            if (MenPurse.On) { int purse; b.MenFree = MenFree(mp, st, fresh, out purse); b.MenPurse = purse; }
            else b.MenNoPurse = true;
            Allocate(b, q, b.MenFree);
            for (int l = 0; l < 3; l++) b.People[l + 1] += b.FootAt[l] + b.MountAt[l];
            b.People[0] += b.MenStable;
            foreach (var h in b.Comp)
            {
                bool horse = HasMount(h);
                int hq = q;
                while (hq >= 0 && ManCost(hq, horse) > Math.Max(0, h.Gold)) hq--;
                int cost = ManCost(hq, horse);
                b.CompQ.Add(hq); b.CompCost.Add(cost); b.CompCostSum += cost;
                b.People[hq + 1]++;
            }
            b.OwnHorse = HasMount(me);
            b.Spare = SpareHorses(mp);
            b.PlayerCost = PRoom[q] + ((b.OwnHorse ? 1 : 0) + b.Spare) * PHorse[q];
            b.PlayerCan = me.Gold >= b.PlayerCost;
            b.People[q + 1]++;
            b.Persons = b.Foot + b.Mounted + b.Comp.Count + 1;
            return b;
        }

        /// <summary>Okno lupu (CitySack L3): szeregowi w wybranej jakosci bez zaplaty.</summary>
        private static void QuarterFree(Bill b, int q)
        {
            b.People[0] -= b.MenStable;   // przed zerowaniem rozdzialu (MenStable liczy sie z FootAt / MountAt)
            for (int l = 0; l < 3; l++) { b.People[l + 1] -= b.FootAt[l] + b.MountAt[l]; b.FootAt[l] = 0; b.MountAt[l] = 0; }
            b.FootAt[q] = b.Foot; b.MountAt[q] = b.Mounted;
            b.People[q + 1] += b.Foot + b.Mounted;
            b.MenCost = 0; b.Sacked = true;
        }

        private static void Bonus(Bill b, out float speed, out float morale, out float sleep, out float heal)
        {
            speed = morale = heal = 0f; sleep = 1f;
            if (b.Persons <= 0) return;
            for (int l = 0; l < 3; l++)
            {
                float sh = b.People[l + 1] / (float)b.Persons;
                speed += sh * QSpeed[l]; morale += sh * QMorale[l]; sleep += sh * (QSleep[l] - 1f); heal += sh * QHeal[l];
            }
        }

        // ------------------------------------------------------------ menu
        private static bool Allowed(Settlement st, out string why)
        {
            why = null;
            var mp = MobileParty.MainParty;
            if (st == null || !st.IsTown || st.Town == null || mp == null || Hero.MainHero == null) { why = "There is no inn to be had here."; return false; }
            if (st.IsUnderSiege) { why = "The town is under siege - no inn takes guests now."; return false; }
            if (mp.MapEvent != null) { why = "Not in the middle of a fight."; return false; }
            if (Campaign.Current != null && Campaign.Current.IsMainHeroDisguised) { why = "In disguise you cannot bring your company to an inn."; return false; }
            if (NightRest.EnlistedNow) { why = "In service your lord decides where the men sleep."; return false; }
            if (mp.Army != null && mp.Army.LeaderParty != mp) { why = "You march in another lord's army - he decides where the host sleeps."; return false; }
            if (NightRest.SleepingNow) { why = "The men are already asleep."; return false; }
            return true;
        }

        internal static void AddMenus(CampaignGameStarter starter)
        {
            try
            {
                starter.AddGameMenuOption("town", "arm_inn_enter", "{=!}Take lodgings at an inn", EnterCondition,
                    delegate (MenuCallbackArgs a) { GameMenu.SwitchToMenu(Menu); }, false, 8);
                starter.AddGameMenu(Menu, "{=!}{ARM_INN_TEXT}", MenuInit, GameMenu.MenuOverlayType.SettlementWithBoth);
                for (int i = 0; i < 3; i++)
                {
                    int q = i;
                    starter.AddGameMenuOption(Menu, "arm_inn_q" + q, "{=!}{ARM_INN_Q" + q + "}",
                        delegate (MenuCallbackArgs a) { return QCondition(a, q); },
                        delegate (MenuCallbackArgs a) { Commit(q); }, false, q);
                }
                starter.AddGameMenuOption(Menu, "arm_inn_back", "{=!}Back to the town",
                    delegate (MenuCallbackArgs a) { a.optionLeaveType = GameMenuOption.LeaveType.Leave; return true; },
                    delegate (MenuCallbackArgs a) { GameMenu.SwitchToMenu("town"); }, true, 9);
            }
            catch (Exception e) { Stumble("AddMenus", e); }
        }

        private static bool EnterCondition(MenuCallbackArgs args)
        {
            try
            {
                args.optionLeaveType = GameMenuOption.LeaveType.Wait;
                var st = Settlement.CurrentSettlement;
                if (!On || st == null || !st.IsTown) return false;
                string why;
                if (!Allowed(st, out why)) { args.IsEnabled = false; args.Tooltip = new TextObject("{=!}" + why); }
                else args.Tooltip = new TextObject("{=!}A common room, a decent inn or a good house - the men pay for themselves, you sleep until dawn and wake rested until dusk.");
                return true;
            }
            catch (Exception e) { Stumble("EnterCondition", e); return false; }
        }

        private static void MenuInit(MenuCallbackArgs args)
        {
            try
            {
                var st = Settlement.CurrentSettlement;
                try { if (st != null && st.SettlementComponent != null && !string.IsNullOrEmpty(st.SettlementComponent.WaitMeshName)) args.MenuContext.SetBackgroundMeshName(st.SettlementComponent.WaitMeshName); } catch { }
                var sb = new StringBuilder();
                sb.Append("The inns of ").Append(st != null ? st.Name.ToString() : "the town").Append(" offer three kinds of night (a man / a horse / your own room):\n");
                sb.Append("\nCommon room - straw and a roof: 1 / 1 / 3. Rested until dusk: +5% speed on the map, +2 morale.");
                sb.Append("\nDecent inn - beds, a hot meal and ale: 2 / 2 / 8. +10% speed, +5 morale, and sleep comes a quarter faster (sleep debt is paid off sooner).");
                sb.Append("\nGood house - rooms, meat, wine and a bath: 4 / 3 / 20. +12% speed, +8 morale, sleep a quarter faster, the wounded heal half again as fast that night");
                sb.Append(PriceKey ? ", and you trade 3% better in this town until dusk." : ".");
                sb.Append("\n\nEvery soldier pays for himself and his horse out of his own purse - they draw their pay for it. A man who cannot afford your choice takes the best cheaper bed he can, or sleeps in the stable for free, as in camp. Companions pay from their own purses. You pay for yourself, your horse and every horse that carries nobody. The party's rest follows how many slept in each kind of bed.");
                sb.Append("\nYou sleep until dawn and wake rested until the next dusk - unless the men still owe sleep.");
                var mp = MobileParty.MainParty;
                if (mp != null && st != null)
                {
                    if (MenPurse.On)
                    {
                        int purse; int free = MenFree(mp, st, true, out purse);
                        sb.Append("\n\nYour men have ").Append(free).Append(" denars to spare (purse ").Append(purse).Append(", the rest is kept for mending and better kit).");
                    }
                    else sb.Append("\n\nYour men keep no purses (Men Purse is off) - they sleep in the stable.");
                    int spare = SpareHorses(mp);
                    if (spare > 0) sb.Append(" Horses that carry nobody: ").Append(spare).Append(".");
                }
                MBTextManager.SetTextVariable("ARM_INN_TEXT", sb.ToString());
            }
            catch (Exception e) { Stumble("MenuInit", e); }
        }

        private static string QualityOf(int q) { return q < 0 ? "the stable" : QName[q].ToLowerInvariant(); }

        private static bool QCondition(MenuCallbackArgs args, int q)
        {
            try
            {
                args.optionLeaveType = GameMenuOption.LeaveType.Wait;
                var st = Settlement.CurrentSettlement;
                string why;
                if (!On || !Allowed(st, out why)) return false;
                var b = Plan(st, q, false);
                MBTextManager.SetTextVariable("ARM_INN_Q" + q, QName[q] + " (" + QWhat[q] + ") - " + b.Total + " d: you " + b.PlayerCost + ", the men " + b.MenCost
                                                             + (b.Comp.Count > 0 ? ", companions " + b.CompCostSum : ""));
                var t = new StringBuilder();
                t.Append(PMan[q]).Append(" d a man, ").Append(PHorse[q]).Append(" d a horse, ").Append(PRoom[q]).Append(" d your room.");
                int men = b.Foot + b.Mounted;
                if (men > 0)
                {
                    t.Append("\nThe men (").Append(men).Append(", ").Append(b.Mounted).Append(" mounted): ");
                    var parts = new List<string>();
                    for (int l = 2; l >= 0; l--) { int n = b.FootAt[l] + b.MountAt[l]; if (n > 0) parts.Add(n + " in a " + QualityOf(l)); }
                    if (b.MenStable > 0) parts.Add(b.MenStable + " in the stable");
                    t.Append(string.Join(", ", parts.ToArray())).Append(b.MenNoPurse ? " - they keep no purses." : " - they pay " + b.MenCost + " of the " + b.MenFree + " d they can spare.");
                }
                if (b.Comp.Count > 0)
                {
                    t.Append("\nCompanions: ");
                    var parts = new List<string>();
                    for (int i = 0; i < b.Comp.Count; i++)
                        parts.Add(b.Comp[i].Name + " " + (b.CompQ[i] < 0 ? "the stable" : QualityOf(b.CompQ[i]) + " " + b.CompCost[i] + " d"));
                    t.Append(string.Join(", ", parts.ToArray())).Append(".");
                }
                t.Append("\nYou: room ").Append(PRoom[q]);
                if (b.OwnHorse) t.Append(" + your horse ").Append(PHorse[q]);
                if (b.Spare > 0) t.Append(" + ").Append(b.Spare).Append(b.Spare == 1 ? " horse" : " horses").Append(" that carry nobody x ").Append(PHorse[q]);
                t.Append(" = ").Append(b.PlayerCost).Append(" d (you have ").Append(Hero.MainHero.Gold).Append(").");
                float sp, mo, sl, he; Bonus(b, out sp, out mo, out sl, out he);
                t.Append("\nRested until dusk: +").Append(F1(sp)).Append("% speed, +").Append(F1(mo)).Append(" morale");
                if (sl > 1.001f) t.Append(", sleep x").Append(sl.ToString("0.00", Inv));
                if (he > 0.001f) t.Append(", healing tonight +").Append((he * 100f).ToString("0", Inv)).Append("%");
                if (q == 2 && PriceKey) t.Append(", you trade 3% better here");
                t.Append(".");
                if (!b.PlayerCan) { args.IsEnabled = false; t.Append("\nYou cannot pay your own part (").Append(b.PlayerCost).Append(" d)."); }
                args.Tooltip = new TextObject("{=!}" + t.ToString());
                return true;
            }
            catch (Exception e) { Stumble("QCondition", e); return false; }
        }

        // ------------------------------------------------------------ zaplata
        /// <summary>Suma posiadaczy dotknietych noclegiem (kontrola obiegu): sakiewki ludzi, kiesy gracza i towarzyszy, kasa miasta, kiesy ludu miast i gminy.</summary>
        private static long World(Settlement st, List<Hero> comp)
        {
            long w = MenPurse.TotalNow() + Hero.MainHero.Gold + st.Town.Gold + TownFolk.TotalTown() + TownFolk.TotalGmina();
            foreach (var h in comp) w += h.Gold;
            return w;
        }

        /// <summary>Bohater -> karczmarze: przelew gry do kasy miasta, potem kasa -> kiesa ludu (TownFolk.EarnFromTown, uslugi - droga oplat gracza PlayerFees8).</summary>
        private static int HeroPays(Hero h, Settlement st, int cost, bool quiet, out int folk)
        {
            folk = 0;
            if (h == null || cost <= 0) return 0;
            int had = h.Gold;
            GiveGoldAction.ApplyForCharacterToSettlement(h, st, cost, quiet);
            int paid = Math.Max(0, had - h.Gold);
            if (paid > 0)
            {
                int t0 = st.Town.Gold;
                TownFolk.EarnFromTown(st, paid, TownFolk.KLife);
                folk = Math.Max(0, Math.Min(paid, t0 - st.Town.Gold));
            }
            return paid;
        }

        private static void Commit(int q)
        {
            try
            {
                var st = Settlement.CurrentSettlement;
                var mp = MobileParty.MainParty;
                var me = Hero.MainHero;
                string why = null;
                if (!On || !Allowed(st, out why)) { if (On) Log.Player(why ?? "No inn takes you in now.", true); return; }
                var b = Plan(st, q, true);
                if (!b.PlayerCan) { Log.Player("You cannot pay your own part of the lodging (" + b.PlayerCost + " d).", true); return; }
                if (b.Foot + b.Mounted > 0 && MenPurse.On && CitySack.NoSpendStay(st, mp)) QuarterFree(b, q);
                long w0 = World(st, b.Comp);
                int menFolk = 0, menPaid = 0;
                if (b.MenCost > 0) menPaid = MenPurse.InnPay(mp, st, b.MenCost, out menFolk);
                int compPaid = 0, compFolk = 0;
                var compLog = new StringBuilder();
                for (int i = 0; i < b.Comp.Count; i++)
                {
                    int f, paid = HeroPays(b.Comp[i], st, b.CompCost[i], true, out f);
                    compPaid += paid; compFolk += f;
                    if (compLog.Length > 0) compLog.Append(", ");
                    compLog.Append(b.Comp[i].StringId).Append(' ').Append(b.CompQ[i] < 0 ? "stajnia" : QLog[b.CompQ[i]]).Append(' ').Append(paid);
                }
                int playerFolk, playerPaid = HeroPays(me, st, b.PlayerCost, false, out playerFolk);
                long diff = World(st, b.Comp) - w0;
                int paidAll = menPaid + compPaid + playerPaid, folkAll = menFolk + compFolk + playerFolk;
                float sp, mo, sl, he; Bonus(b, out sp, out mo, out sl, out he);
                long now = NowMin();
                _rest = new Rest { Phase = 1, Q = q, PlayerQ = q, TownId = st.StringId, TownName = st.Name.ToString(), CommitMin = now,
                                   Speed = sp, Morale = mo, Sleep = sl, Heal = he, Price = q == 2 };
                _dStays++; _dHorses += b.Spare; for (int l = 0; l < 4; l++) _dPeople[l] += b.People[l];
                _dMen += menPaid; _dComp += compPaid; _dPlayer += playerPaid; _dFolk += folkAll; _dCoffer += paidAll - folkAll; _dDiff += diff;
                Log.Info(P + "nocleg w " + st.Name + " (" + st.StringId + ") " + When(now) + " - wybor " + q + " (" + QLog[q] + ")"
                         + " | szeregowi " + (b.Foot + b.Mounted) + " (pieszych " + b.Foot + ", konnych " + b.Mounted + "): wspolna izba " + (b.FootAt[0] + b.MountAt[0])
                         + ", porzadna karczma " + (b.FootAt[1] + b.MountAt[1]) + ", dobry dom " + (b.FootAt[2] + b.MountAt[2]) + ", stajnia " + b.MenStable
                         + "; sakiewka ludzi " + b.MenPurse + ", wolne " + b.MenFree + ", zaplacili " + menPaid + " z " + b.MenCost
                         + (b.MenNoPurse ? " (sakiewki wylaczone)" : "") + (b.Sacked ? " (okno lupu - bez zaplaty)" : "")
                         + " | towarzysze " + b.Comp.Count + (compLog.Length > 0 ? ": " + compLog : "") + " | gracz: pokoj " + PRoom[q] + ", swoj kon " + (b.OwnHorse ? 1 : 0)
                         + ", konie bez jezdzca " + b.Spare + " x " + PHorse[q] + " = " + b.PlayerCost + ", zaplacil " + playerPaid
                         + " | razem " + paidAll + " (ludzie " + menPaid + ", towarzysze " + compPaid + ", gracz " + playerPaid + ") -> kiesa ludu " + folkAll + ", kasa miasta " + (paidAll - folkAll)
                         + " | kontrola obiegu: roznica " + diff
                         + " | osoby " + b.Persons + ": stajnia " + b.People[0] + ", wspolna izba " + b.People[1] + ", porzadna karczma " + b.People[2] + ", dobry dom " + b.People[3]
                         + " | premia po snie: predkosc +" + F1(sp) + "%, morale +" + F1(mo) + ", sen x" + sl.ToString("0.00", Inv) + ", gojenie +" + (he * 100f).ToString("0", Inv) + "%"
                         + ", cena " + (q == 2 && PriceKey ? "tak" : "nie") + ".");
                string men = b.Foot + b.Mounted == 0 ? "" : b.Sacked ? " Your men take their beds by right of the sack and pay nothing." : " Your men paid " + menPaid + " from their purses" + (b.MenStable > 0 ? ", " + b.MenStable + " of them sleep in the stable." : ".");
                Log.Player("You take lodgings at a " + QualityOf(q) + " in " + st.Name + " - " + paidAll + " denars in all, " + playerPaid + " of them yours." + men
                           + (compPaid > 0 ? " Your companions paid " + compPaid + "." : ""));
                NightRest.BeginInnSleep();
            }
            catch (Exception e) { Stumble("Commit", e); }
        }

        // ------------------------------------------------------------ sen (wolane z NightRest)
        /// <summary>NightRest.SleepInit w osadzie: oplacony nocleg w tym miescie - poczatek snu, pobudka nie przed switem (gdy swit w 12 h), tlo miasta, tekst.
        /// null = sen bez noclegu (tekst NightRest jak dotad). Po wczytaniu w trakcie snu (faza 1 z zapisu) godziny zostaja z zapisu.</summary>
        internal static string OnSleepInit(MenuCallbackArgs args)
        {
            try
            {
                var r = _rest;
                var mp = MobileParty.MainParty;
                var st = mp != null ? mp.CurrentSettlement : null;
                if (r == null || r.Phase != 1 || st == null || st.StringId != r.TownId) return null;
                long now = NowMin();
                if (r.StartMin <= 0)
                {
                    r.StartMin = now;
                    long dawn = NextAt(now, NightRest.PlayerDawn);
                    r.WakeMin = dawn - now <= HoldMaxMin ? dawn : 0;
                    Log.Info(P + "sen w karczmie (" + QLog[Math.Max(0, r.Q)] + ") w " + r.Town + " od " + When(now) + ", "
                             + (r.WakeMin > 0 ? "pobudka nie przed " + When(r.WakeMin) + " (swit)" : "bez czekania do switu (swit dalej niz 12 h) - sen do wyspania")
                             + ", sen x" + r.Sleep.ToString("0.00", Inv) + ", gojenie +" + (r.Heal * 100f).ToString("0", Inv) + "%.");
                }
                try { if (st.SettlementComponent != null && !string.IsNullOrEmpty(st.SettlementComponent.WaitMeshName)) args.MenuContext.SetBackgroundMeshName(st.SettlementComponent.WaitMeshName); } catch { }
                string bed = r.Q == 2 ? "The house gives you rooms, a bath, meat and wine; the men eat their fill and sleep in proper beds"
                           : r.Q == 1 ? "The men eat a hot meal, drink their ale and sleep in beds"
                           : "The men bed down on the straw of the common room";
                return bed + (r.WakeMin > 0 ? " - nobody stirs before dawn." : ".");
            }
            catch (Exception e) { Stumble("OnSleepInit", e); return null; }
        }

        /// <summary>NightRest.SleepTick: mnoznik godzin snu w lozku (porzadna karczma / dobry dom - x1.25 wazone liczba osob); poza noclegiem 1.</summary>
        internal static float SleepFactor()
        {
            var r = InnSleep();
            return r != null && r.Sleep > 1f ? r.Sleep : 1f;
        }

        /// <summary>Oplacony nocleg, ktorego sen zaczal sie w karczmie (OnSleepInit), a partia jest w tym miescie - inaczej null (sen w obozie po przerwanym
        /// noclegu przy wylaczonym PlayerSleepGuard nie dostaje mnoznika, czekania do switu ani premii). Watek glowny (SleepTick, LeaveSleep).</summary>
        private static Rest InnSleep()
        {
            var r = _rest;
            if (r == null || r.Phase != 1 || r.StartMin <= 0 || !On) return null;
            try
            {
                var mp = MobileParty.MainParty;
                var st = mp != null ? mp.CurrentSettlement : null;
                return st != null && st.StringId == r.TownId ? r : null;
            }
            catch { return null; }
        }

        /// <summary>NightRest.SleepTick: oplacony nocleg - pasek pelny, ale do switu nikt nie wstaje.</summary>
        internal static bool HoldAbed()
        {
            var r = InnSleep();
            return r != null && r.WakeMin > 0 && NowMin() < r.WakeMin;
        }

        /// <summary>NightRest.SleepTick: pasek snu przy czekaniu do switu - mniejsze z paska snu i zegara do switu.</summary>
        internal static float Progress(float p)
        {
            try
            {
                var r = InnSleep();
                if (r == null || r.WakeMin <= 0 || r.WakeMin <= r.StartMin) return p;
                float c = (NowMin() - r.StartMin) / (float)(r.WakeMin - r.StartMin);
                return Math.Max(0f, Math.Min(p, Math.Min(1f, c)));
            }
            catch { return p; }
        }

        /// <summary>NightRest.LeaveSleep: pobudka po noclegu - wypoczeci do zmierzchu, gdy pasek pelny albo przespana baza (SleepHoursNeeded); inaczej bez premii.</summary>
        internal static void OnWake(float menuRest, bool full) { Wake(menuRest, full, "pobudka"); }

        /// <summary>NightRest.EndSleepQuiet: sen w karczmie przerwany (atak, inne menu) - premia tylko po przespanej bazie.</summary>
        internal static void OnInterrupted(float menuRest, string why) { Wake(menuRest, false, "sen przerwany (menu '" + why + "')"); }

        private static void Wake(float menuRest, bool full, string how)
        {
            try
            {
                var r0 = _rest;
                if (r0 == null || r0.Phase != 1 || r0.StartMin <= 0) return;   // sen nie zaczal sie w karczmie (OnSleepInit) - ten sen to nie nocleg
                var r = InnSleep();
                if (r == null)
                {
                    // nocleg oplacony, ale ten sen nie jest w miescie noclegu (albo klucz wylaczony w trakcie) - stan zdjety, bez premii
                    _rest = null; _dNoRest++;
                    Log.Info(P + how + " poza miastem noclegu " + r0.Town + " (albo Town Inns wylaczone) " + When(NowMin()) + " - stan noclegu zdjety, bez premii.");
                    return;
                }
                var s = Settings.Current;
                float baseH = s != null ? Math.Max(1f, s.SleepHoursNeeded) : 6f;
                bool ok = full || menuRest + 0.01f >= baseH;
                long now = NowMin();
                if (!ok)
                {
                    _rest = null; _dNoRest++;
                    Log.Player("The men were up before they had slept in their beds - the lodging is paid, but nobody wakes rested.", true);
                    Log.Info(P + how + " w " + r.Town + " " + When(now) + " - w menu " + F1(menuRest) + " h z bazy " + F1(baseH) + " h, pelny pasek nie -> bez premii.");
                    return;
                }
                var n = r.Copy(2);
                n.UntilMin = NextAt(now, DuskHour);
                _rest = n; _dWoke++;
                var msg = new StringBuilder("Rested in a bed: until dusk (" + Clock(n.UntilMin) + ") the column marches " + F1(n.Speed) + "% faster and morale is +" + F1(n.Morale) + ".");
                if (n.Price && PriceKey) msg.Append(" The good house's name opens doors - you trade 3% better in ").Append(n.Town).Append(" until dusk.");
                if (NightRest.Debt > 0) msg.Append(" Old weariness still lingers - the bed's comfort shows once the sleep debt is paid off.");
                Log.Player(msg.ToString());
                Log.Info(P + how + " w " + n.Town + " " + When(now) + " - w menu " + F1(menuRest) + " h, pelny pasek " + (full ? "tak" : "nie") + " -> wypoczeci do "
                         + When(n.UntilMin) + " (predkosc +" + F1(n.Speed) + "%, morale +" + F1(n.Morale) + ", cena " + (n.Price && PriceKey ? "tak" : "nie")
                         + ", dlug snu " + NightRest.Debt + (NightRest.Debt > 0 ? " - premia czeka na splate dlugu" : "") + ").");
            }
            catch (Exception e) { Stumble("Wake", e); }
        }

        // ------------------------------------------------------------ premie (predkosc i morale z NightRest, gojenie, ceny)
        private static readonly TextObject _txtBed = new TextObject("{=!}Slept in a bed");
        private static readonly TextObject _txtBath = new TextObject("{=!}A good bed and a bath");

        /// <summary>Wypoczety teraz (faza 2, przed zmierzchem, klucz). Bez wyjatku - czytane rownolegle (predkosc).</summary>
        private static Rest Live()
        {
            var r = _rest;
            if (r == null || r.Phase != 2 || !On) return null;
            try { return NowMin() < r.UntilMin ? r : null; } catch { return null; }
        }

        /// <summary>NightRest.SpeedPostfix (gracz bez dlugu snu, zewnetrzny poziom lancucha): +x% WYNIKU - dzielone przez (1 + suma wspolczynnikow), jak kara snu.</summary>
        internal static void SpeedBonus(ref ExplainedNumber r)
        {
            try
            {
                var x = Live();
                if (x == null || x.Speed <= 0f) return;
                float add = r.ResultNumber * x.Speed / 100f;
                float f = 1f + r.SumOfFactors;
                if (add > 0f) r.Add(f > 0.01f ? add / f : add, _txtBed);
            }
            catch { }
        }

        /// <summary>NightRest.MoralePostfix (gracz bez dlugu snu, zewnetrzny poziom lancucha): +punkty morale.</summary>
        internal static void MoraleBonus(ref ExplainedNumber r)
        {
            try
            {
                var x = Live();
                if (x != null && x.Morale > 0f) r.Add(x.Morale, _txtBed);
            }
            catch { }
        }

        /// <summary>Gojenie w dobrym domu: partia gracza (czlonkowie, nie jency), tylko w czasie snu oplaconego noclegu; wynik x (1 + premia) - po SlowHealing (Low) i BK.</summary>
        public static void HealPostfix(PartyBase __0, bool __1, ref ExplainedNumber __result)
        {
            try
            {
                if (__1 || __0 == null || __0 != PartyBase.MainParty) return;
                var r = _rest;
                if (r == null || r.Phase != 1 || r.Heal <= 0f || r.StartMin <= 0 || !On || !InSleepMenu()) return;
                if (__result.ResultNumber <= 0f) return;   // kara (glod) - bez zmian
                float f = 1f + __result.SumOfFactors;
                __result.AddFactor(r.Heal * (f > 0.01f ? f : 1f), _txtBath);
            }
            catch { }
        }

        /// <summary>Gracz spi teraz w menu snu (flaga NightRest i menu arm_sleep_wait - przy wylaczonym PlayerSleepGuard flaga snu bywa nieaktualna). Watek glowny.</summary>
        private static bool InSleepMenu()
        {
            if (!NightRest.SleepingNow) return false;
            try
            {
                var c = Campaign.Current;
                var mc = c != null ? c.CurrentMenuContext : null;
                var gm = mc != null ? mc.GameMenu : null;
                return gm != null && gm.StringId == "arm_sleep_wait";
            }
            catch { return false; }
        }

        [ThreadStatic] private static bool _tradeHere;

        private static Rest PriceLive()
        {
            if (!PriceKey) return null;
            var r = Live();
            return r != null && r.Price ? r : null;
        }

        /// <summary>Ekran handlu gracza z miastem noclegu w dobrym domu - flaga watku na czas InventoryLogic.GetItemPrice.</summary>
        public static void InvPrefix(InventoryLogic __instance)
        {
            _tradeHere = false;
            try
            {
                if (__instance == null || !__instance.IsTrading) return;
                var r = PriceLive();
                if (r == null) return;
                var op = __instance.OtherParty;
                _tradeHere = op != null && op.IsSettlement && op.Settlement != null && op.Settlement.StringId == r.TownId;
            }
            catch { _tradeHere = false; }
        }

        public static void InvFinalizer() { _tradeHere = false; }

        /// <summary>Cena rynku miasta pod flaga ekranu handlu: kupno -3%, sprzedaz +3% (nie drozej niz kupno tej sztuki z premia - 1).</summary>
        public static void MarketPostfix(TownMarketData __instance, EquipmentElement __0, MobileParty __1, bool __2, PartyBase __3, ref int __result)
        {
            if (!_tradeHere) return;
            try
            {
                if (__1 == null || __1 != MobileParty.MainParty || __result <= 0) return;
                if (!__2) { __result = Math.Max(1, (int)Math.Round(__result * (1f - PriceStep))); return; }
                int up = (int)Math.Round(__result * (1f + PriceStep));
                int buy;
                _tradeHere = false;
                try { buy = __instance.GetPrice(__0, __1, false, __3); }
                finally { _tradeHere = true; }
                int buyAdj = Math.Max(1, (int)Math.Round(buy * (1f - PriceStep)));
                if (up >= buyAdj) up = Math.Max(__result, buyAdj - 1);
                __result = up;
            }
            catch { }
        }

        /// <summary>PlayerInventoryExchangeEvent: handel w miescie noclegu przy czynnej premii cen - do linii doby (oszczednosc szacowana z 3%).</summary>
        internal static void OnPlayerExchange(List<(ItemRosterElement, int)> bought, List<(ItemRosterElement, int)> sold, bool isTrading)
        {
            try
            {
                if (!isTrading) return;
                var r = PriceLive();
                var st = Settlement.CurrentSettlement;
                if (r == null || st == null || st.StringId != r.TownId) return;
                if (bought != null) foreach (var x in bought) { if (x.Item2 > 0) { _dBuyN += Math.Max(1, x.Item1.Amount); _dBuy += x.Item2; } }
                if (sold != null) foreach (var x in sold) { if (x.Item2 > 0) { _dSellN += Math.Max(1, x.Item1.Amount); _dSell += x.Item2; } }
            }
            catch (Exception e) { Stumble("OnPlayerExchange", e); }
        }

        // ------------------------------------------------------------ zegar, doba, sesja
        /// <summary>Godzina gry: koniec premii o zmierzchu (komunikat), oplacony nocleg bez snu w menu dluzej niz 2 h - zdjety; godzina gojenia w dobrym domu.</summary>
        internal static void Hourly()
        {
            try
            {
                var r = _rest;
                if (r == null) return;
                long now = NowMin();
                if (r.Phase == 2 && now >= r.UntilMin)
                {
                    _rest = null;
                    if (On) Log.Player("Dusk - the comfort of the inn wears off.");
                    Log.Info(P + "koniec premii o " + When(now) + " (wypoczeci od noclegu w " + r.Town + ").");
                }
                else if (r.Phase == 1 && !InSleepMenu() && now - Math.Max(r.CommitMin, r.StartMin) > DropAwakeMin)
                {
                    _rest = null; _dDropped++;
                    Log.Info(P + "oplacony nocleg w " + r.Town + " bez snu w menu od " + When(Math.Max(r.CommitMin, r.StartMin)) + " - stan zdjety, bez premii.");
                }
                else if (r.Phase == 1 && r.Heal > 0f && r.StartMin > 0 && InSleepMenu() && On) _dHealH++;
            }
            catch (Exception e) { Stumble("Hourly", e); }
        }

        private static string Keys()
        {
            var s = Settings.Current;
            return "klucze: Town Inns " + (s != null && s.TownInns ? "tak" : "WYLACZONE") + ", Town Inn Price Bonus " + (s != null && s.TownInnPriceBonus ? "tak" : "WYLACZONE")
                   + ", Night Rest " + (s != null && s.NightRestEnabled ? "tak" : "WYLACZONE");
        }

        private static string StateText()
        {
            var r = _rest;
            if (r == null) return "brak";
            if (r.Phase == 1) return "sen oplacony (" + QLog[Math.Max(0, r.Q)] + ", " + r.Town + ", od " + When(Math.Max(r.CommitMin, r.StartMin)) + ")";
            return "wypoczeci do " + When(r.UntilMin) + " (predkosc +" + F1(r.Speed) + "%, morale +" + F1(r.Morale) + ", cena " + (r.Price && PriceKey ? "tak w " + r.Town : "nie") + ")";
        }

        internal static void SessionStart()
        {
            try { Log.Info(P + "start sesji | " + Keys() + " | latki: cena (ekran handlu) " + _priceHooks + "/2, gojenie " + _healHooks + "/2 | stan noclegu: " + StateText() + " | potkniecia " + _stumbles + "."); }
            catch (Exception e) { Stumble("SessionStart", e); }
        }

        /// <summary>Linia doby (z OnDailyTick, za dobe od poprzedniej linii) - zawsze przy wlaczonym kluczu TownInns.</summary>
        internal static void Daily(int day)
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !s.TownInns) { DayReset(); return; }
                long est(long sum, float up) { return (long)Math.Round(sum * PriceStep / (double)up); }
                Log.Info(P + "dzien " + day + " | " + Keys()
                         + " | noclegi " + _dStays + ": osoby " + (_dPeople[0] + _dPeople[1] + _dPeople[2] + _dPeople[3]) + " (wspolna izba " + _dPeople[1] + ", porzadna karczma " + _dPeople[2]
                         + ", dobry dom " + _dPeople[3] + ", stajnia " + _dPeople[0] + "), konie bez jezdzca " + _dHorses
                         + " | zaplacone " + (_dMen + _dComp + _dPlayer) + " (ludzie " + _dMen + ", towarzysze " + _dComp + ", gracz " + _dPlayer + ") -> kiesa ludu " + _dFolk + ", kasa miasta " + _dCoffer
                         + " | kontrola obiegu (prog 0): roznica " + _dDiff
                         + " | pobudki: wypoczeci " + _dWoke + ", bez premii " + _dNoRest + ", noclegi bez snu " + _dDropped
                         + " | stan noclegu: " + StateText()
                         + " | gojenie dobrego domu: godzin " + _dHealH
                         + " | cena dobrego domu: kupione " + _dBuyN + " szt. za " + _dBuy + " (taniej ok. " + est(_dBuy, 1f - PriceStep) + "), sprzedane " + _dSellN + " szt. za " + _dSell
                         + " (drozej ok. " + est(_dSell, 1f + PriceStep) + ")"
                         + " | potkniecia dzis " + _stumblesDay + " (od wczytania " + _stumbles + ").");
            }
            catch (Exception e) { Stumble("Daily", e); }
            finally { DayReset(); }
        }

        // ------------------------------------------------------------ zapis
        /// <summary>"v1;faza;q;qGracza;miasto;zaplata;start;pobudka;koniec;predkosc;morale;sen;gojenie;cena" (minuty kampanii, kultura niezmienna). Brak stanu = "".</summary>
        internal static string Export()
        {
            var r = _rest;
            if (r == null || r.Phase < 1) return "";
            return string.Join(";", new[] { "v1", r.Phase.ToString(Inv), r.Q.ToString(Inv), r.PlayerQ.ToString(Inv), r.TownId ?? "", r.CommitMin.ToString(Inv), r.StartMin.ToString(Inv),
                                            r.WakeMin.ToString(Inv), r.UntilMin.ToString(Inv), r.Speed.ToString("R", Inv), r.Morale.ToString("R", Inv), r.Sleep.ToString("R", Inv),
                                            r.Heal.ToString("R", Inv), r.Price ? "1" : "0" });
        }

        internal static void Import(string data)
        {
            _rest = null;
            if (string.IsNullOrEmpty(data)) return;
            try
            {
                var a = data.Split(';');
                if (a.Length < 14 || a[0] != "v1") { Log.Info(P + "zapis arm_inns w nieznanym formacie - pominiety."); return; }
                var r = new Rest();
                r.Phase = int.Parse(a[1], Inv); r.Q = int.Parse(a[2], Inv); r.PlayerQ = int.Parse(a[3], Inv); r.TownId = a[4];
                r.CommitMin = long.Parse(a[5], Inv); r.StartMin = long.Parse(a[6], Inv); r.WakeMin = long.Parse(a[7], Inv); r.UntilMin = long.Parse(a[8], Inv);
                r.Speed = float.Parse(a[9], Inv); r.Morale = float.Parse(a[10], Inv); r.Sleep = float.Parse(a[11], Inv); r.Heal = float.Parse(a[12], Inv); r.Price = a[13] == "1";
                if (r.Phase < 1 || r.Phase > 2 || r.Q < 0 || r.Q > 2) return;
                // premie przyciete do projektu (zly zapis nie robi z noclegu dopalacza)
                r.Speed = Math.Max(0f, Math.Min(QSpeed[2], r.Speed)); r.Morale = Math.Max(0f, Math.Min(QMorale[2], r.Morale));
                r.Sleep = Math.Max(1f, Math.Min(QSleep[2], r.Sleep)); r.Heal = Math.Max(0f, Math.Min(QHeal[2], r.Heal));
                _rest = r;   // nazwa miasta przy pierwszym uzyciu (Rest.Town) - bez szukania osady w trakcie wczytywania
            }
            catch (Exception e) { _rest = null; Stumble("Import", e); }
        }

        // ------------------------------------------------------------ latki (raz na proces, przy starcie gry - SubModuleMain)
        internal static void ApplyAll(Harmony h)
        {
            try
            {
                var inv = AccessTools.Method(typeof(InventoryLogic), "GetItemPrice", new[] { typeof(EquipmentElement), typeof(bool) });
                var mkt = AccessTools.Method(typeof(TownMarketData), "GetPrice", new[] { typeof(EquipmentElement), typeof(MobileParty), typeof(bool), typeof(PartyBase) });
                if (inv != null && mkt != null)
                {
                    if (h.Patch(inv, prefix: new HarmonyMethod(typeof(TownInns), nameof(InvPrefix)), finalizer: new HarmonyMethod(typeof(TownInns), nameof(InvFinalizer))) != null) _priceHooks++;
                    if (h.Patch(mkt, postfix: new HarmonyMethod(typeof(TownInns), nameof(MarketPostfix)) { priority = Priority.Last }) != null) _priceHooks++;
                }
            }
            catch (Exception e) { Stumble("ApplyAll(cena)", e); }
            try
            {
                var t = typeof(DefaultPartyHealingModel);
                var post = new HarmonyMethod(typeof(TownInns), nameof(HealPostfix)) { priority = Priority.VeryLow };   // po SlowHealing (Low) i BK; przed StarvePostfix (Last)
                var m1 = AccessTools.Method(t, "GetDailyHealingForRegulars");
                var m2 = AccessTools.Method(t, "GetDailyHealingHpForHeroes");
                if (m1 != null && h.Patch(m1, postfix: post) != null) _healHooks++;
                if (m2 != null && h.Patch(m2, postfix: post) != null) _healHooks++;
            }
            catch (Exception e) { Stumble("ApplyAll(gojenie)", e); }
            Log.Info(P + "latki przy starcie gry - cena (ekran handlu: InventoryLogic.GetItemPrice + TownMarketData.GetPrice) " + _priceHooks + "/2, gojenie (DefaultPartyHealingModel) "
                     + _healHooks + "/2.");
        }
    }
}
