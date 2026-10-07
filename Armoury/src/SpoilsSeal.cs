using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// SPOILS OF WAR BEZ AUTOMATYCZNEJ SPRZEDAZY I BEZ ZLOTA Z NICZEGO (paczka 128; Jeff 07.10: "zadnej automatycznej sprzedazy,
    /// system ma byc szczelny i wszystko z czego wynika, zero darmowej kasy").
    /// Dekompilacja RealisticLoot.dll 1.8.4 (Spoils of War) - kazde miejsce, gdzie mod daje zloto albo towar bez platnika:
    ///  1. QuartermasterBehavior.OnDailyTick - kwatermistrz co dobe "sprzedaje" do 5 tanich sztuk z magazynu wojennego kazdego
    ///     miasta gracza: sztuki znikaja (nie trafiaja na zaden targ), a kasa miasta dostaje Town.ChangeGold(+40% wartosci) od
    ///     nikogo. -> BLOKADA calkowita (SpoilsNoAutoSale), niezaleznie od "Auto-sell from stockpile" w MCM Spoils (kazdy preset
    ///     Spoils ma tam "tak"); towar zostaje w magazynie.
    ///  2. RealisticLootModel.GenerateLootFromUnitList - "monety z cial" 0-150 zl na trupa (LastGeneratedGold), wyplacane graczowi
    ///     przy wyjsciu z pola. Prawdziwe pieniadze pokonanych juz plyna: gra zabiera kiese pokonanych (MapEvent.
    ///     CalculatePlunderedAndLostGoldAmounts -> zwyciezcy wedle wkladu), a sakiewka ludzi rozbitej partii idzie do zwyciezcy
    ///     (MenPurse.OnPartyDestroyed) - monety z cial to trzecia wyplata, z niczego. -> BLOKADA (zero).
    ///  3. BaggageTrainModel.GenerateBaggageLoot - "zloto handlowe" taboru: czesc PartyTradeGold pokonanych ze zdjecia z poczatku
    ///     bitwy, nikomu nie zabrana (kiese pokonanych i tak bierze gra). -> BLOKADA (zero). Tabor Spoils i tak wycina
    ///     BattlefieldLaw - to zabezpieczenie na wypadek wylaczenia prawa pola bitwy.
    ///  4. LootCollectionBehavior.TryTriggerRandomEvent - "ukryta skrzynia" 50-200 zl, "okup od rannego" 30-120 zl i "dodatkowy
    ///     sprzet" (+1 sztuka z niczego). -> te trzy BLOKADA; "bandyci ukradli" (sztuka mniej) zostaje jak u Spoils.
    ///  5. LootCollectionBehavior.ExecuteFenceSale - paser w zaulku placi graczowi z niczego, lup znika. -> PLATNIK: kasa miasta
    ///     ponad rezerwe na renty (TownRentFloorGold), za sztuke najwyzej cena skupu targu (OutlawLaw.FencePrice - ta sama co dla
    ///     band), sztuka na polke miasta; czego miasto nie kupi - zostaje w sakwach. Limit pasera (CalculateGoldLimit) przyciety
    ///     do kasy miasta ponad rezerwe.
    ///  6. QuartermasterBehavior.OnSalvageScreenClosed - przetop za 15% wartosci z niczego, lup znika. -> PLATNIK jak w 5.
    ///  7. SubClanBehavior.OnDonateGearScreenClosed - "przedmioty sprzedane, zloto do skarbca klanu": skarbiec dostaje 80/60/40/20%
    ///     wartosci z niczego, przedmioty znikaja. -> PLATNIK jak w 5, zaplata do przywodcy klanu.
    ///  8. SubClanBehavior.OnSubClanMapEventEnded - "udzial gracza" 20% z (polegli x 200) po kazdej wygranej bitwie klanu
    ///     najemnikow, z niczego. -> PLATNIK: skarbiec klanu (przywodca) ponad 5000 - ten sam prog, co dzienny dochod Spoils;
    ///     czego klan nie ma, tego gracz nie dostaje.
    ///  9. QuartermasterBehavior.OnStockpileScreenClosed - magazyn wojenny pamieta tylko id i znacznik "lup": sztuka zuzyta (rusty,
    ///     chipped... - zuzycie Armoury) wraca NOWA, "Mangled" (10%) wraca jako "Plundered" (55%) - wartosc z niczego ("masterwork"
    ///     wraca zwykla - strata). -> do magazynu wchodzi tylko to, co magazyn oddaje co do sztuki (czyste i "Plundered"), reszta
    ///     wraca do sakw.
    /// Martwe w 1.8.4: zloto pozostalosci pola (BattlefieldRemnantsTemporarilyDisabled = true; i tak bralo z monet z cial).
    /// Bez zmian (to nie zloto z niczego): naprawa i najem kwatermistrza, zalozenie / odnowienie / nowe druzyny klanu (zloto gracza
    /// czesciowo do nikad - ujscie), dary dla zalogi / milicji / zywnosc dla miasta (towar na wskazniki miasta), dzienny dochod
    /// klanu (przelew od przywodcy), wyslanie zlota (przelew), wyposazenie przywodcy (towar do jego partii), lup i modyfikatory.
    /// Wszystko przez refleksje - bez Spoils nic sie nie wpina. Stan: tylko liczniki do linii dnia (bez zapisu); wyjatek - licznik
    /// potkniec, latka dziala dalej, a towar w obrocie wraca do sakw (nic nie znika).
    /// </summary>
    internal static class SpoilsSeal
    {
        private const int SubClanFloor = 5000;       // Spoils SubClanBehavior.OnDailyTick: dochod tylko ze skarbca ponad 5000 - ten sam prog dla dzialki z bitwy
        private const int Handled = int.MinValue;    // __state: latka sama obsluzyla wywolanie (oryginal pominiety) - postfiks nic nie liczy
        private const int FenceAll = 4;              // LootCollectionBehavior.FenceCategory.All

        // Spoils of War (RealisticLoot) - typy i skladowe przez refleksje
        private static Type _tQm, _tLoot, _tSub, _tModel, _tBag, _tMcm, _tHelper, _tDamage, _tJournal, _tStock, _tMgr;
        private static PropertyInfo _pMcm, _pIds, _pCounts, _pJournal;
        private static readonly Dictionary<string, PropertyInfo> _mcmProp = new Dictionary<string, PropertyInfo>();
        private static FieldInfo _fHired, _fStockMgr, _fSalvage, _fStockScreen;
        private static FieldInfo _fEvTriggered, _fEvProgress, _fEvLoot, _fEvMessage;
        private static FieldInfo _fFenceCount, _fFenceLimit, _fFenceRate, _fFenceBargain, _fFenceBonus, _fJournal;
        private static FieldInfo _fScLeader, _fScData, _fScClan, _fScDonate;
        private static MethodInfo _mStockGet, _mIsWeapon, _mIsArmor, _mGetGold, _mSetGold, _mGetTrade, _mSetTrade;
        private static MethodInfo _mTypeCoef, _mFenceCat, _mBulk, _mCrime, _mCooldown, _mRecordFence, _mRecordSalvage;
        private static MethodInfo _mIsLooted, _mLootedMod, _mLogMessage, _mDonationGold;
        private static readonly List<string> _wired = new List<string>(), _missing = new List<string>();
        private static bool _present, _saleWired;

        internal static bool NoAutoSale { get { var s = Settings.Current; return s != null && s.SpoilsNoAutoSale; } }
        internal static bool NoFreeGold { get { var s = Settings.Current; return s != null && s.SpoilsNoFreeGold; } }
        private static int Reserve { get { var s = Settings.Current; return s != null ? (int)Math.Max(0f, s.TownRentFloorGold) : 0; } }

        // liczniki od poprzedniej linii dnia; [0] zablokowane, [1] przepuszczone (wylacznik wylaczony - Spoils dal z niczego)
        private static readonly int[] _saleItems = new int[2], _saleGold = new int[2], _coins = new int[2], _trade = new int[2];
        private static readonly StringBuilder[] _saleWhere = { new StringBuilder(), new StringBuilder() };
        private static int _saleQms, _saleStocks, _saleStockItems, _coinsBattles, _coinsN;
        private static bool _saleSpoilsOff, _saleNoMcm;
        private static int _evChest, _evChestGold, _evRansom, _evRansomGold, _evExtra, _evExtraValue, _evBandits, _evLetGold;
        private static readonly Tally _fence = new Tally(), _salv = new Tally(), _don = new Tally();
        private static int _fenceLet, _salvLet, _donLet, _scN, _scPaid, _scCut, _scLet, _stockBack, _stockUp, _stockDown;
        private static int _stumbles, _stumblesDay;

        /// <summary>Rozliczenie sprzedazy miastu: transakcje, sztuki sprzedane / zostawione, zaplata miasta, oferta Spoils (z niczego).</summary>
        private sealed class Tally
        {
            public int Deals, Sold, Kept, Paid, Offer, Worth, Capped, Poor;
            public void Clear() { Deals = Sold = Kept = Paid = Offer = Worth = Capped = Poor = 0; }
        }

        /// <summary>Stos do sprzedazy: element, liczba sztuk, oferta Spoils za sztuke, wartosc sztuki wedle Spoils (statystyki klanu).</summary>
        private struct Lot { public EquipmentElement El; public int Amount, Offer, Worth; }

        /// <summary>Nowa gra albo wczytanie (konstruktor ArmouryBehavior): liczniki od zera.</summary>
        internal static void Reset()
        {
            ClearDay();
            _stumbles = 0;
        }

        private static void ClearDay()
        {
            Array.Clear(_saleItems, 0, 2); Array.Clear(_saleGold, 0, 2); Array.Clear(_coins, 0, 2); Array.Clear(_trade, 0, 2);
            _saleWhere[0].Length = 0; _saleWhere[1].Length = 0;
            _saleQms = _saleStocks = _saleStockItems = _coinsBattles = _coinsN = 0;
            _saleSpoilsOff = _saleNoMcm = false;
            _evChest = _evChestGold = _evRansom = _evRansomGold = _evExtra = _evExtraValue = _evBandits = _evLetGold = 0;
            _fence.Clear(); _salv.Clear(); _don.Clear();
            _fenceLet = _salvLet = _donLet = _scN = _scPaid = _scCut = _scLet = _stockBack = _stockUp = _stockDown = 0;
            _stumblesDay = 0;
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++; _stumblesDay++;
            if (_stumbles <= 3) Log.Error(where, e);
        }

        private static int MainGold() { var h = Hero.MainHero; return h != null ? h.Gold : 0; }

        // ------------------------------------------------------------ ustawienia Spoils (MCM) - jak w Spoils: brak = wartosc domyslna

        private static object Mcm() { try { return _pMcm != null ? _pMcm.GetValue(null, null) : null; } catch { return null; } }

        private static object McmVal(object inst, string name)
        {
            if (inst == null || _tMcm == null) return null;
            PropertyInfo p;
            if (!_mcmProp.TryGetValue(name, out p)) { p = AccessTools.Property(_tMcm, name); _mcmProp[name] = p; }
            try { return p != null ? p.GetValue(inst, null) : null; } catch { return null; }
        }

        private static bool McmBool(object i, string n, bool d) { var v = McmVal(i, n); return v is bool ? (bool)v : d; }
        private static int McmInt(object i, string n, int d) { var v = McmVal(i, n); return v is int ? (int)v : d; }
        private static float McmFloat(object i, string n, float d) { var v = McmVal(i, n); return v is float ? (float)v : d; }

        private static bool IsLooted(ItemModifier m) { return m != null && _mIsLooted != null && (bool)_mIsLooted.Invoke(null, new object[] { m }); }

        /// <summary>Komunikat dla gracza tym samym kanalem co Spoils (LootHelper.LogMessage - czat i log Spoils), inaczej nasz.</summary>
        private static void Say(string chat, string file)
        {
            try { if (_mLogMessage != null) { _mLogMessage.Invoke(null, new object[] { chat, file }); return; } } catch { }
            Log.Player(chat);
        }

        private static string Nm(Settlement st)
        {
            try { return st != null && st.Name != null ? st.Name.ToString() : (st != null ? st.StringId : "?"); } catch { return st != null ? st.StringId : "?"; }
        }

        // ------------------------------------------------------------ 1. sprzedaz automatyczna magazynu wojennego

        /// <summary>Prefiks QuartermasterBehavior.OnDailyTick: liczy dokladnie to, co Spoils sprzedalby dzis (ten sam algorytm: zapasy
        /// broni / pancerza / zywnosci / koni, prog wartosci, najtansze pierwsze, limit na dobe), i przy SpoilsNoAutoSale zwraca false -
        /// Spoils nie sprzedaje niczego. Przy wylaczonym przepuszcza Spoils, a liczniki mowia, ile sprzedal z niczego.</summary>
        public static bool AutoSalePrefix(object __instance)
        {
            bool block = NoAutoSale;
            try { MeasureAutoSale(__instance, block ? 0 : 1); }
            catch (Exception e) { Stumble("SpoilsSeal.AutoSale", e); }
            return !block;
        }

        private static void MeasureAutoSale(object qm, int b)
        {
            var inst = Mcm();
            if (inst == null) { _saleNoMcm = true; return; }   // Spoils: bez ustawien OnDailyTick nic nie robi
            if (!McmBool(inst, "EnableQuartermaster", true) || !McmBool(inst, "QMAutoSellEnabled", true)) { _saleSpoilsOff = true; return; }
            int maxPerDay = McmInt(inst, "QMAutoSellMaxPerDay", 5), maxValue = McmInt(inst, "QMAutoSellMaxValue", 1000);
            float rate = McmFloat(inst, "QMAutoSellRate", 40f) / 100f;
            int rW = McmInt(inst, "ReserveWeapons", 10), rA = McmInt(inst, "ReserveArmor", 10), rF = McmInt(inst, "ReserveFood", 20), rH = McmInt(inst, "ReserveHorses", 5);
            int expensive = McmInt(inst, "ReserveExpensiveAbove", 1500);
            var hired = _fHired.GetValue(qm) as IDictionary;
            var mgr = _fStockMgr.GetValue(qm);
            if (hired == null || mgr == null) return;
            var om = MBObjectManager.Instance;
            foreach (var k in hired.Keys)
            {
                var key = k as string;
                _saleQms++;
                var sp = _mStockGet.Invoke(mgr, new object[] { key });
                if (sp == null) continue;
                var ids = _pIds.GetValue(sp, null) as List<string>;
                var counts = _pCounts.GetValue(sp, null) as List<int>;
                if (ids == null || counts == null) continue;
                int total = 0;
                foreach (var c in counts) total += c;
                if (total == 0) continue;
                Settlement st = null;
                foreach (var s in Settlement.All) if (s.StringId == key) { st = s; break; }
                if (st == null || st.Town == null) continue;
                _saleStocks++; _saleStockItems += total;
                var list = new List<KeyValuePair<int, ItemObject>>();
                for (int i = 0; i < ids.Count; i++)
                {
                    var it = om != null ? om.GetObject<ItemObject>(ids[i]) : null;
                    if (it == null) continue;
                    if ((i < counts.Count ? counts[i] : 0) > 0) list.Add(new KeyValuePair<int, ItemObject>(i, it));
                }
                list.Sort((x, y) => y.Value.Value.CompareTo(x.Value.Value));
                int w = 0, a = 0, f = 0, h = 0;
                var sellable = new List<int[]>();   // { wartosc, do sprzedania }
                foreach (var kv in list)
                {
                    var it = kv.Value;
                    int n = counts[kv.Key];
                    if (it.Value >= expensive || it.Value > maxValue) continue;
                    int s = n;
                    if ((bool)_mIsWeapon.Invoke(null, new object[] { it })) { int keep = Math.Min(n, Math.Max(0, rW - w)); s = n - keep; w += keep; }
                    else if ((bool)_mIsArmor.Invoke(null, new object[] { it })) { int keep = Math.Min(n, Math.Max(0, rA - a)); s = n - keep; a += keep; }
                    else if (it.IsFood) { int keep = Math.Min(n, Math.Max(0, rF - f)); s = n - keep; f += keep; }
                    else if (it.IsMountable) { int keep = Math.Min(n, Math.Max(0, rH - h)); s = n - keep; h += keep; }
                    if (s > 0) sellable.Add(new[] { it.Value, s });
                }
                sellable.Sort((x, y) => x[0].CompareTo(y[0]));
                int sold = 0, gold = 0;
                foreach (var e in sellable)
                {
                    if (sold >= maxPerDay) break;
                    int n = Math.Min(e[1], maxPerDay - sold);
                    gold += (int)((float)e[0] * rate) * n;
                    sold += n;
                }
                if (sold <= 0) continue;
                _saleItems[b] += sold; _saleGold[b] += gold;
                if (_saleWhere[b].Length < 600)
                    _saleWhere[b].Append(_saleWhere[b].Length > 0 ? ", " : "").Append(Nm(st)).Append(' ').Append(sold).Append(" szt./").Append(gold).Append(" zl");
            }
        }

        // ------------------------------------------------------------ 2-3. monety z cial i zloto taboru

        /// <summary>Postfiks RealisticLootModel.GenerateLootFromUnitList: "monety z cial" (LastGeneratedGold) do zera.</summary>
        public static void CoinsPostfix()
        {
            try
            {
                _coinsBattles++;
                int g = (int)_mGetGold.Invoke(null, null);
                if (g <= 0) return;
                _coinsN++;
                if (NoFreeGold) { _mSetGold.Invoke(null, new object[] { 0 }); _coins[0] += g; }
                else _coins[1] += g;
            }
            catch (Exception e) { Stumble("SpoilsSeal.Coins", e); }
        }

        /// <summary>Postfiks BaggageTrainModel.GenerateBaggageLoot: "zloto handlowe" taboru (LastRecoveredTradeGold) do zera.</summary>
        public static void TradePostfix()
        {
            try
            {
                int g = (int)_mGetTrade.Invoke(null, null);
                if (g <= 0) return;
                if (NoFreeGold) { _mSetTrade.Invoke(null, new object[] { 0 }); _trade[0] += g; }
                else _trade[1] += g;
            }
            catch (Exception e) { Stumble("SpoilsSeal.Trade", e); }
        }

        // ------------------------------------------------------------ 4. zdarzenia podczas zbierania lupu

        /// <summary>Prefiks LootCollectionBehavior.TryTriggerRandomEvent - te same bramki i losowania co Spoils (40-65% postepu, raz
        /// na zbieranie, 15% szansy; 35% skrzynia, 25% okup, 20% bandyci, 20% dodatkowy sprzet). Skrzynia, okup i dodatkowy sprzet
        /// nie zachodza (liczone); bandyci - jak u Spoils (sztuka mniej, ten sam komunikat).</summary>
        public static bool EventPrefix(object __instance, out int __state)
        {
            __state = Handled;
            if (!NoFreeGold) { __state = MainGold(); return true; }
            try
            {
                if ((bool)_fEvTriggered.GetValue(__instance)) return false;
                float p = (float)_fEvProgress.GetValue(__instance);
                if (p < 0.4f || p > 0.65f) return false;
                _fEvTriggered.SetValue(__instance, true);
                if (MBRandom.RandomFloat > 0.15f) return false;
                float r = MBRandom.RandomFloat;
                var loot = _fEvLoot.GetValue(__instance) as ItemRoster;
                if (r < 0.35f) { _evChest++; _evChestGold += MBRandom.RandomInt(50, 200); }
                else if (r < 0.6f) { _evRansom++; _evRansomGold += MBRandom.RandomInt(30, 120); }
                else if (r < 0.8f) { if (loot != null && loot.Count > 1) Bandits(__instance, loot); }
                else if (loot != null && loot.Count > 0)
                {
                    var el = loot.GetElementCopyAtIndex(MBRandom.RandomInt(loot.Count));
                    _evExtra++; _evExtraValue += el.EquipmentElement.GetBaseValue();
                }
            }
            catch (Exception e) { Stumble("SpoilsSeal.Event", e); }
            return false;
        }

        private static void Bandits(object beh, ItemRoster loot)
        {
            var el = loot.GetElementCopyAtIndex(MBRandom.RandomInt(loot.Count));
            var item = el.EquipmentElement.Item;
            string name = item != null && item.Name != null ? item.Name.ToString() : "item";
            loot.AddToCounts(el.EquipmentElement, -1);
            string msg = new TextObject("{=RL_Event_Bandits}Bandits raided the camp and stole: {ITEM}!").SetTextVariable("ITEM", name).ToString();
            _fEvMessage.SetValue(beh, msg);
            _evBandits++;
            Say(msg, "[Event] Bandits stole item: " + (item != null ? item.StringId : "item:null"));
        }

        public static void EventPostfix(int __state)
        {
            if (__state == Handled) return;
            try { int g = MainGold() - __state; if (g > 0) _evLetGold += g; } catch { }
        }

        // ------------------------------------------------------------ sprzedaz miastu (paser, przetop, dar dla klanu)

        /// <summary>
        /// Miasto kupuje sztuki z `source` - kazda osobno: cena = min(oferta Spoils, cena skupu targu za te sztuke TERAZ - polka
        /// zmienia sie po kazdej), z kasy miasta ponad rezerwe na renty, razem najwyzej `limit` (ujemny = bez limitu). Sztuka
        /// schodzi z `source` i trafia na polke miasta (nieudane dolozenie cofa zdjecie); zaplata w finally - takze po wyjatku -
        /// z kasy miasta do `payee`. Legend i egzotycznych wierzchowcow nie u swoich miasto nie kupuje (LegendaryLaw zdjalby je z
        /// polki). Czego miasto nie kupi, zostaje w `source`.
        /// </summary>
        private static int SellToTown(Settlement st, ItemRoster source, List<Lot> lots, Hero payee, int limit, Tally t)
        {
            var town = st != null ? st.Town : null;
            int reserve = Reserve, due = 0;
            bool poor = false;
            try
            {
                foreach (var lot in lots)
                {
                    var item = lot.El.Item;
                    bool banned = item == null || LegendaryLaw.IsLegend(item) || (MountLaw.IsExotic(item) && !MountLaw.AllowedForSettlement(st, item));
                    for (int k = 0; k < lot.Amount; k++)
                    {
                        if (town == null || banned || payee == null) { t.Kept++; continue; }
                        if (source.FindIndexOfElement(lot.El) < 0) break;                 // tej sztuki juz nie ma
                        int market = OutlawLaw.FencePrice(st, lot.El);
                        int price = Math.Min(lot.Offer, market);
                        if (price <= 0 || (limit >= 0 && due + price > limit)) { t.Kept++; continue; }
                        if (town.Gold - reserve < due + price) { t.Kept++; poor = true; continue; }
                        source.AddToCounts(lot.El, -1);
                        bool placed = false;
                        try { st.ItemRoster.AddToCounts(lot.El, 1); placed = true; }
                        finally { if (!placed) source.AddToCounts(lot.El, 1); }
                        if (market < lot.Offer) t.Capped++;
                        due += price; t.Sold++; t.Worth += lot.Worth;
                    }
                }
            }
            finally
            {
                if (due > 0) { town.ChangeGold(-due); payee.ChangeHeroGold(due); t.Paid += due; }
                if (poor) t.Poor++;
            }
            return due;
        }

        /// <summary>To, co zostalo na ekranie Spoils (niesprzedane), wraca do sakw gracza - nic nie znika.</summary>
        private static void ReturnRest(ItemRoster screen)
        {
            try
            {
                var bag = MobileParty.MainParty != null ? MobileParty.MainParty.ItemRoster : null;
                if (bag == null || screen == null) return;
                for (int i = screen.Count - 1; i >= 0; i--)
                {
                    if (i >= screen.Count) continue;
                    var el = screen.GetElementCopyAtIndex(i);
                    if (el.Amount <= 0) continue;
                    bag.AddToCounts(el.EquipmentElement, el.Amount);
                    screen.AddToCounts(el.EquipmentElement, -el.Amount);
                }
            }
            catch (Exception e) { Stumble("SpoilsSeal.ReturnRest", e); }
        }

        // ------------------------------------------------------------ 5. paser

        /// <summary>Postfiks LootCollectionBehavior.CalculateGoldLimit: paser ma najwyzej tyle, ile kasa miasta ma ponad rezerwe -
        /// oferta w menu ("Gold available") mowi prawde, zanim gracz sprzeda.</summary>
        public static void FenceLimitPostfix(ref int __result)
        {
            if (!NoFreeGold) return;
            try
            {
                var st = Settlement.CurrentSettlement;
                int spare = st != null && st.Town != null ? Math.Max(0, st.Town.Gold - Reserve) : 0;
                if (spare < __result) __result = spare;
            }
            catch (Exception e) { Stumble("SpoilsSeal.FenceLimit", e); }
        }

        /// <summary>Prefiks LootCollectionBehavior.ExecuteFenceSale(FenceCategory): ta sama wycena i te same sztuki co Spoils, ale
        /// placi kasa miasta (SellToTown); potem jak u Spoils: XP lotrostwa od zaplaty, dziennik, ryzyko przestepstwa, odstep dni.</summary>
        public static bool FencePrefix(object __instance, object[] __args, out int __state)
        {
            __state = Handled;
            if (!NoFreeGold) { __state = MainGold(); return true; }
            try { FenceSale(__instance, __args != null && __args.Length > 0 ? Convert.ToInt32(__args[0]) : FenceAll); }
            catch (Exception e) { Stumble("SpoilsSeal.Fence", e); try { GameMenu.SwitchToMenu("town_backstreet"); } catch { } }
            return false;
        }

        public static void FencePostfix(int __state)
        {
            if (__state == Handled) return;
            try { int g = MainGold() - __state; if (g > 0) _fenceLet += g; } catch { }
        }

        private static void FenceSale(object beh, int cat)
        {
            var bag = MobileParty.MainParty != null ? MobileParty.MainParty.ItemRoster : null;
            if (bag == null) return;                                       // jak Spoils: bez sakw nic sie nie dzieje
            var inst = Mcm();
            int min = McmInt(inst, "FenceMinItemValue", 100);
            float bulk = (float)_mBulk.Invoke(null, new object[] { (int)_fFenceCount.GetValue(beh), true });
            float rate = (float)_fFenceRate.GetValue(beh), bonus = (float)_fFenceBonus.GetValue(beh);
            bool bargain = (bool)_fFenceBargain.GetValue(beh);
            int limit = Math.Max(0, (int)_fFenceLimit.GetValue(beh));
            var lots = new List<Lot>();
            long offer = 0;
            for (int i = 0; i < bag.Count; i++)
            {
                var el = bag.GetElementCopyAtIndex(i);
                var it = el.EquipmentElement.Item;
                if (it == null || el.Amount <= 0 || !IsLooted(el.EquipmentElement.ItemModifier) || it.Value < min) continue;
                if (cat != FenceAll && Convert.ToInt32(_mFenceCat.Invoke(null, new object[] { it })) != cat) continue;
                float r = rate * (float)_mTypeCoef.Invoke(null, new object[] { it }) * bulk;
                if (bargain) r += bonus;
                r = MathF.Clamp(r * 0.22f, 0.05f, 0.95f);
                int unit = Math.Max(1, (int)((float)it.Value * r));
                lots.Add(new Lot { El = el.EquipmentElement, Amount = el.Amount, Offer = unit, Worth = it.Value });
                offer += (long)unit * el.Amount;
            }
            var st = Settlement.CurrentSettlement;
            int s0 = _fence.Sold, k0 = _fence.Kept;
            int paid = SellToTown(st, bag, lots, Hero.MainHero, limit, _fence);
            int sold = _fence.Sold - s0, kept = _fence.Kept - k0;
            _fence.Deals++; _fence.Offer += (int)Math.Min(offer, (long)limit);   // Spoils placi do swojego limitu
            if (sold > 0)
            {
                Hero.MainHero.AddSkillXp(DefaultSkills.Roguery, (float)paid * 0.05f);
                var j = _fJournal.GetValue(beh);
                if (j != null) _mRecordFence.Invoke(j, new object[] { paid });
                _mCrime.Invoke(beh, new object[] { inst });
                _mCooldown.Invoke(beh, null);
                Say("Sold " + sold + " plundered items to the fence for " + paid + " denars, paid out of the coffers of " + Nm(st) + "."
                    + (kept > 0 ? " " + kept + " items stay with you - the town has no more coin for them." : ""),
                    "[Fence] Armoury 128: sold " + sold + " items for " + paid + " den. from the town treasury, " + kept + " kept (Spoils offer " + offer + " den. out of nothing)");
            }
            else Say("The fence has no coin for you: the coffers of " + Nm(st) + " cannot pay. Your goods stay with you.",
                     "[Fence] Armoury 128: nothing sold - the town treasury cannot pay (Spoils offer " + offer + " den. out of nothing)");
            GameMenu.SwitchToMenu("town_backstreet");
        }

        // ------------------------------------------------------------ 6. przetop u kwatermistrza

        /// <summary>Prefiks QuartermasterBehavior.OnSalvageScreenClosed: to, co gracz zostawil na ekranie przetopu, kupuje miasto
        /// (SellToTown, oferta Spoils = stawka przetopu od wartosci); reszta wraca do sakw; potem jak u Spoils: dziennik, menu.</summary>
        public static bool SalvagePrefix(object __instance, out int __state)
        {
            __state = Handled;
            ItemRoster screen = null;
            try { screen = _fSalvage.GetValue(__instance) as ItemRoster; } catch { }
            if (!NoFreeGold || screen == null) { __state = MainGold(); return true; }   // pusty ekran: Spoils i tak nic nie placi
            try { Salvage(screen); }
            catch (Exception e) { Stumble("SpoilsSeal.Salvage", e); }
            finally
            {
                ReturnRest(screen);
                try { _fSalvage.SetValue(__instance, null); } catch { }
                try { GameMenu.ActivateGameMenu("realistic_loot_quartermaster"); } catch { }
            }
            return false;
        }

        public static void SalvagePostfix(int __state)
        {
            if (__state == Handled) return;
            try { int g = MainGold() - __state; if (g > 0) _salvLet += g; } catch { }
        }

        private static void Salvage(ItemRoster screen)
        {
            float rate = McmFloat(Mcm(), "SalvageRate", 15f) / 100f;
            var lots = new List<Lot>();
            long offer = 0;
            for (int i = 0; i < screen.Count; i++)
            {
                var el = screen.GetElementCopyAtIndex(i);
                var it = el.EquipmentElement.Item;
                if (el.Amount <= 0 || it == null) continue;
                int unit = (int)((float)it.Value * rate);
                lots.Add(new Lot { El = el.EquipmentElement, Amount = el.Amount, Offer = unit, Worth = it.Value });
                offer += (long)unit * el.Amount;
            }
            if (lots.Count == 0) return;
            var st = Settlement.CurrentSettlement;
            int s0 = _salv.Sold, k0 = _salv.Kept;
            int paid = SellToTown(st, screen, lots, Hero.MainHero, -1, _salv);
            int sold = _salv.Sold - s0, kept = _salv.Kept - k0;
            _salv.Deals++; _salv.Offer += (int)Math.Min(offer, int.MaxValue);
            if (sold > 0)
            {
                var j = _pJournal != null ? _pJournal.GetValue(null, null) : null;
                if (j != null) _mRecordSalvage.Invoke(j, new object[] { sold, paid });
                Say("Salvaged " + sold + " items for " + paid + " denars - the smiths of " + Nm(st) + " paid out of the town's coffers."
                    + (kept > 0 ? " " + kept + " items go back to your baggage - the town has no more coin for them." : ""),
                    "[QM] Armoury 128: salvaged " + sold + " items for " + paid + " den. from the town treasury, " + kept + " back to baggage (Spoils offer " + offer + " den. out of nothing)");
            }
            else Say("The smiths of " + Nm(st) + " cannot pay for scrap now - the town's coffers are bare. Your gear goes back to your baggage.",
                     "[QM] Armoury 128: nothing salvaged - the town treasury cannot pay (Spoils offer " + offer + " den. out of nothing)");
        }

        // ------------------------------------------------------------ 9. magazyn wojenny

        /// <summary>Prefiks QuartermasterBehavior.OnStockpileScreenClosed: sztuki, ktorych magazyn nie umie oddac co do sztuki (stan
        /// inny niz czysty albo "Plundered" - Spoils zapisuje tylko id i znacznik lupu), wracaja do sakw z dokladnym stanem.</summary>
        public static void StockpilePrefix(object __instance)
        {
            if (!NoFreeGold) return;
            try
            {
                var screen = _fStockScreen.GetValue(__instance) as ItemRoster;
                var bag = MobileParty.MainParty != null ? MobileParty.MainParty.ItemRoster : null;
                if (screen == null || bag == null || Settlement.CurrentSettlement == null) return;   // Spoils i tak nic nie zapisze
                var looted = _mLootedMod.Invoke(null, null) as ItemModifier;
                int n = 0;
                for (int i = screen.Count - 1; i >= 0; i--)
                {
                    if (i >= screen.Count) continue;
                    var el = screen.GetElementCopyAtIndex(i);
                    var it = el.EquipmentElement.Item;
                    var m = el.EquipmentElement.ItemModifier;
                    if (el.Amount <= 0 || it == null || m == null || (looted != null && m == looted)) continue;
                    // magazyn oddalby te sztuke jako: lup -> "Plundered", kazda inna -> czysta
                    var back = IsLooted(m) && looted != null ? new EquipmentElement(it, looted) : new EquipmentElement(it);
                    int diff = (back.ItemValue - el.EquipmentElement.ItemValue) * el.Amount;
                    if (diff > 0) _stockUp += diff; else _stockDown -= diff;
                    screen.AddToCounts(el.EquipmentElement, -el.Amount);
                    bag.AddToCounts(el.EquipmentElement, el.Amount);
                    n += el.Amount;
                }
                if (n <= 0) return;
                _stockBack += n;
                Say("The war stockpile keeps gear only as it is clean or plundered: " + n + " pieces in another condition stay in your baggage.",
                    "[Stockpile] Armoury 128: " + n + " pieces in another condition returned to baggage");
            }
            catch (Exception e) { Stumble("SpoilsSeal.Stockpile", e); }
        }

        // ------------------------------------------------------------ 7-8. klan najemnikow

        /// <summary>Prefiks SubClanBehavior.OnDonateGearScreenClosed: dar sprzetu = sprzedaz miastu (SellToTown, oferta Spoils =
        /// jego stawka 80/60/40/20% od wartosci), zaplata do przywodcy klanu; reszta wraca do sakw; potem jak u Spoils: statystyki
        /// klanu, slawa od sprzedanej wartosci, menu.</summary>
        public static bool DonatePrefix(object __instance, out int __state)
        {
            __state = Handled;
            ItemRoster screen = null;
            try { screen = _fScDonate.GetValue(__instance) as ItemRoster; } catch { }
            if (!NoFreeGold || screen == null) { __state = LeaderGold(__instance); return true; }
            try { Donate(__instance, screen); }
            catch (Exception e) { Stumble("SpoilsSeal.Donate", e); }
            finally
            {
                ReturnRest(screen);
                try { _fScDonate.SetValue(__instance, null); } catch { }
                try { GameMenu.ActivateGameMenu("realistic_loot_subclan"); } catch { }
            }
            return false;
        }

        public static void DonatePostfix(object __instance, int __state)
        {
            if (__state == Handled) return;
            try { int g = LeaderGold(__instance) - __state; if (g > 0) _donLet += g; } catch { }
        }

        private static int LeaderGold(object sc)
        {
            try { var l = _fScLeader.GetValue(sc) as Hero; return l != null ? l.Gold : 0; } catch { return 0; }
        }

        private static void Donate(object sc, ItemRoster screen)
        {
            var leader = _fScLeader.GetValue(sc) as Hero;
            var data = _fScData.GetValue(sc);
            var clan = _fScClan.GetValue(sc) as Clan;
            var units = new List<Lot>();
            int count = 0, value = 0;
            for (int i = 0; i < screen.Count; i++)
            {
                var el = screen.GetElementCopyAtIndex(i);
                if (el.Amount <= 0) continue;
                int v = el.EquipmentElement.Item != null ? el.EquipmentElement.Item.Value : 0;
                if (el.EquipmentElement.ItemModifier != null) v = (int)((float)v * el.EquipmentElement.ItemModifier.PriceMultiplier);
                count += el.Amount; value += v * el.Amount;
                units.Add(new Lot { El = el.EquipmentElement, Amount = el.Amount, Worth = v });
            }
            if (count == 0) return;                                        // Spoils: nic nie oddano
            if (leader == null)                                            // Spoils: przedmioty przepadaly bez zaplaty - tu wracaja do sakw
            {
                Say("The company has no captain to take your gear - it stays with you.", "[SubClan] Armoury 128: no leader - donation returned");
                return;
            }
            int offer = (int)_mDonationGold.Invoke(null, new object[] { value });
            float rate = value > 0 ? (float)offer / value : 0f;
            for (int i = 0; i < units.Count; i++) { var u = units[i]; u.Offer = (int)((float)u.Worth * rate); units[i] = u; }
            var st = Settlement.CurrentSettlement;
            int s0 = _don.Sold, k0 = _don.Kept, w0 = _don.Worth;
            int paid = SellToTown(st, screen, units, leader, -1, _don);
            int sold = _don.Sold - s0, kept = _don.Kept - k0, worth = _don.Worth - w0;
            _don.Deals++; _don.Offer += offer;
            string name = clan != null && clan.Name != null ? clan.Name.ToString() : "the company";
            if (sold <= 0)
            {
                Say("No buyer in " + Nm(st) + " - the town's coffers are bare. Your gear goes back to your baggage.",
                    "[SubClan] Armoury 128: nothing sold - the town treasury cannot pay (Spoils offer " + offer + " den. out of nothing)");
                return;
            }
            if (data != null)
            {
                var t = data.GetType();
                var fCount = t.GetField("DonatedGearCount"); var fValue = t.GetField("DonatedGearValue"); var fDay = t.GetField("LastDonationDay");
                if (fCount != null) fCount.SetValue(data, (int)fCount.GetValue(data) + sold);
                if (fValue != null) fValue.SetValue(data, (int)fValue.GetValue(data) + worth);
                if (fDay != null) fDay.SetValue(data, CampaignTime.Now.ElapsedDaysUntilNow);   // jak Spoils (ten sam zapis)
            }
            float renown = (float)worth / 10000f;                          // jak Spoils: slawa od wartosci - tu od wartosci SPRZEDANEJ
            if (renown >= 0.1f && clan != null) { try { clan.AddRenown(renown); } catch (Exception e) { Stumble("SpoilsSeal.Donate.Renown", e); } }
            Say("Sold " + sold + " pieces of your gear to " + Nm(st) + " for " + paid + " denars, paid into the treasury of \"" + name + "\"."
                + (kept > 0 ? " " + kept + " pieces go back to your baggage - the town has no more coin for them." : ""),
                "[SubClan] Armoury 128: " + sold + " items sold to the town for " + paid + " den. -> clan treasury, " + kept + " back (Spoils offer " + offer + " den. out of nothing)");
        }

        /// <summary>Prefiks SubClanBehavior.OnSubClanMapEventEnded: zloto gracza przed metoda.</summary>
        public static void IncomePrefix(out int __state) { __state = MainGold(); }

        /// <summary>Postfiks: to, co Spoils dopisal graczowi jako "udzial z bitwy klanu", placi skarbiec klanu (przywodca) ponad 5000;
        /// reszte odbieramy - z niczego nie powstaje nic. Wylacznik wylaczony: tylko licznik.</summary>
        public static void IncomePostfix(object __instance, int __state)
        {
            try
            {
                var me = Hero.MainHero;
                if (me == null) return;
                int got = me.Gold - __state;
                if (got <= 0) return;
                _scN++;
                if (!NoFreeGold) { _scLet += got; return; }
                var leader = _fScLeader.GetValue(__instance) as Hero;
                int can = leader != null && leader.IsAlive ? Math.Max(0, leader.Gold - SubClanFloor) : 0;
                int pay = Math.Min(got, can);
                if (pay > 0) leader.ChangeHeroGold(-pay);
                int cut = got - pay;
                if (cut > 0)
                {
                    me.ChangeHeroGold(-cut);
                    var data = _fScData.GetValue(__instance);
                    var f = data != null ? data.GetType().GetField("TotalIncomeEarned") : null;
                    if (f != null) f.SetValue(data, (int)f.GetValue(data) - cut);
                }
                _scPaid += pay; _scCut += cut;
            }
            catch (Exception e) { Stumble("SpoilsSeal.Income", e); }
        }

        // ------------------------------------------------------------ linia dnia

        private static string Deal(Tally t)
        {
            return "transakcji " + t.Deals + ", miasto kupilo " + t.Sold + " szt. za " + t.Paid + " zl z kasy (Spoils dalby z niczego " + t.Offer
                   + " zl; cena targu nizsza od oferty Spoils: " + t.Capped + " szt.), zostalo u sprzedajacego " + t.Kept + " szt. (kasa ponad rezerwe za mala: "
                   + t.Poor + " razy)";
        }

        /// <summary>Raz na dobe (ArmouryBehavior.OnDailyTick): co zablokowano, co zaplacilo miasto albo klan, co Spoils dal z niczego
        /// przy wylaczonym wylaczniku - od poprzedniej linii. Tylko log.</summary>
        internal static void Daily()
        {
            if (!_present) return;
            try
            {
                var sb = new StringBuilder();
                sb.Append("Spoils of War (128): dzien ").Append((int)CampaignTime.Now.ToDays - 1).Append(" | sprzedaz automatyczna magazynu wojennego: ")
                  .Append(!_saleWired ? "LATKA NIEWPIETA - Spoils sprzedaje jak dotad" : (NoAutoSale ? "ZABLOKOWANA (128)" : "CZYNNA - wylacznik Spoils No Auto Sale wylaczony (tu tylko pomiar)"));
                if (_saleNoMcm) sb.Append(" (ustawien MCM Spoils brak - Spoils i tak nic nie sprzedaje)");
                else if (_saleSpoilsOff) sb.Append(" (w MCM Spoils 'Auto-sell from stockpile' albo kwatermistrz wylaczony - Spoils i tak nic nie sprzedaje)");
                sb.Append("; kwatermistrzow ").Append(_saleQms).Append(", magazynow z towarem ").Append(_saleStocks).Append(" (").Append(_saleStockItems).Append(" szt.)")
                  .Append("; zablokowano (tyle Spoils sprzedalby z niczego od poprzedniej linii - towar zostaje w magazynie, przy blokadzie liczone co dobe od nowa): ")
                  .Append(_saleItems[0]).Append(" szt., zloto z niczego, ktore nie powstalo: ").Append(_saleGold[0]).Append(" zl");
                if (_saleWhere[0].Length > 0) sb.Append(" [").Append(_saleWhere[0]).Append(']');
                sb.Append("; Spoils sprzedal z niczego: ").Append(_saleItems[1]).Append(" szt. (zniknely), kasy miast +").Append(_saleGold[1]).Append(" zl od nikogo");
                if (_saleWhere[1].Length > 0) sb.Append(" [").Append(_saleWhere[1]).Append(']');
                sb.Append(" | reszta zlota z niczego (wylacznik Spoils No Free Gold ").Append(NoFreeGold ? "wlaczony" : "WYLACZONY - tylko pomiar").Append("): ")
                  .Append("monety z cial - bitew ").Append(_coinsBattles).Append(" (z monetami ").Append(_coinsN).Append("), zablokowano ").Append(_coins[0])
                  .Append(" zl, wyplacono z niczego ").Append(_coins[1]).Append(" zl; zloto taboru - zablokowano ").Append(_trade[0]).Append(" zl, wyplacono z niczego ").Append(_trade[1]).Append(" zl")
                  .Append("; zdarzenia przy zbieraniu - zablokowano skrzynie ").Append(_evChest).Append(" (").Append(_evChestGold).Append(" zl), okupy ").Append(_evRansom)
                  .Append(" (").Append(_evRansomGold).Append(" zl), dodatkowy sprzet ").Append(_evExtra).Append(" szt. (").Append(_evExtraValue).Append(" zl), bandyci ukradli ")
                  .Append(_evBandits).Append(" szt. (jak u Spoils), wyplacono z niczego ").Append(_evLetGold).Append(" zl")
                  .Append("; paser: ").Append(Deal(_fence)).Append(", wyplacono z niczego ").Append(_fenceLet).Append(" zl")
                  .Append("; przetop u kwatermistrza: ").Append(Deal(_salv)).Append(", wyplacono z niczego ").Append(_salvLet).Append(" zl")
                  .Append("; dar sprzetu dla klanu najemnikow (zaplata do skarbca klanu): ").Append(Deal(_don)).Append(", wyplacono z niczego ").Append(_donLet).Append(" zl")
                  .Append("; udzial z bitew klanu: wyplat ").Append(_scN).Append(", ze skarbca klanu ").Append(_scPaid).Append(" zl, odciete (klanu nie stac ponad ")
                  .Append(SubClanFloor).Append(") ").Append(_scCut).Append(" zl, wyplacono z niczego ").Append(_scLet).Append(" zl")
                  .Append("; magazyn wojenny: wrocilo do sakw ").Append(_stockBack).Append(" szt. w innym stanie (wartosc, ktora magazyn dodalby z niczego ")
                  .Append(_stockUp).Append(" zl, odebralby ").Append(_stockDown).Append(" zl)")
                  .Append("; potkniecia dzis ").Append(_stumblesDay).Append(" (od wczytania ").Append(_stumbles).Append(").");
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Log.Error("SpoilsSeal.Daily", e); }
            finally { ClearDay(); }
        }

        // ------------------------------------------------------------ wpiecie

        private static Type Find(string name) { return QuartermasterLaw.FindType(name); }

        private static void Wire(Harmony h, Type t, string method, string prefix, string postfix, string label, bool ready)
        {
            var m = t != null ? AccessTools.Method(t, method) : null;
            if (m == null) { _missing.Add(label + " (brak metody " + (t != null ? t.Name : "?") + "." + method + ")"); return; }
            if (!ready) { _missing.Add(label + " (brak pol " + t.Name + ")"); return; }
            h.Patch(m, prefix: prefix != null ? new HarmonyMethod(typeof(SpoilsSeal), prefix) : null,
                       postfix: postfix != null ? new HarmonyMethod(typeof(SpoilsSeal), postfix) : null);
            _wired.Add(label);
            if (label == "sprzedaz automatyczna") _saleWired = true;
        }

        internal static void ApplyAll(Harmony h)
        {
            try
            {
                _wired.Clear(); _missing.Clear();
                _tQm = Find("RealisticLoot.Behaviors.QuartermasterBehavior");
                _tLoot = Find("RealisticLoot.Behaviors.LootCollectionBehavior");
                if (_tQm == null && _tLoot == null) { Log.Info("SpoilsSeal: Spoils of War (RealisticLoot) nieobecny - nie ma czego uszczelniac."); return; }
                _present = true;
                _tSub = Find("RealisticLoot.Behaviors.SubClanBehavior");
                _tModel = Find("RealisticLoot.Models.RealisticLootModel");
                _tBag = Find("RealisticLoot.Models.BaggageTrainModel");
                _tMcm = Find("RealisticLoot.Settings.MCMSettings");
                _tHelper = Find("RealisticLoot.Utils.LootHelper");
                _tDamage = Find("RealisticLoot.Models.EquipmentDamageModel");
                _tJournal = Find("RealisticLoot.Utils.TrophyJournal");
                _tStock = Find("RealisticLoot.Models.WarStockpile");
                _tMgr = Find("RealisticLoot.Models.WarStockpileManager");
                _pMcm = _tMcm != null ? AccessTools.Property(_tMcm, "Instance") : null;     // GlobalSettings<MCMSettings>.Instance (statyczna)
                _mIsLooted = _tDamage != null ? AccessTools.Method(_tDamage, "IsLootedModifier") : null;
                _mLootedMod = _tDamage != null ? AccessTools.PropertyGetter(_tDamage, "LootedModifier") : null;
                _mLogMessage = _tHelper != null ? AccessTools.Method(_tHelper, "LogMessage", new[] { typeof(string), typeof(string) }) : null;
                _pJournal = _tJournal != null ? AccessTools.Property(_tJournal, "Instance") : null;
                _mRecordFence = _tJournal != null ? AccessTools.Method(_tJournal, "RecordFenceSale") : null;
                _mRecordSalvage = _tJournal != null ? AccessTools.Method(_tJournal, "RecordSalvage") : null;

                // 1. sprzedaz automatyczna
                if (_tQm != null)
                {
                    _fHired = AccessTools.Field(_tQm, "_hiredSettlements"); _fStockMgr = AccessTools.Field(_tQm, "_stockpileManager");
                    _mIsWeapon = AccessTools.Method(_tQm, "IsWeapon"); _mIsArmor = AccessTools.Method(_tQm, "IsArmor");
                    _fSalvage = AccessTools.Field(_tQm, "_salvageScreenRoster"); _fStockScreen = AccessTools.Field(_tQm, "_stockpileScreenRoster");
                }
                _mStockGet = _tMgr != null ? AccessTools.Method(_tMgr, "Get") : null;
                _pIds = _tStock != null ? AccessTools.Property(_tStock, "ItemIds") : null;
                _pCounts = _tStock != null ? AccessTools.Property(_tStock, "ItemCounts") : null;
                Wire(h, _tQm, "OnDailyTick", "AutoSalePrefix", null, "sprzedaz automatyczna",
                     _fHired != null && _fStockMgr != null && _mStockGet != null && _pIds != null && _pCounts != null && _mIsWeapon != null && _mIsArmor != null && _pMcm != null);

                // 2-3. monety z cial, zloto taboru
                _mGetGold = _tModel != null ? AccessTools.PropertyGetter(_tModel, "LastGeneratedGold") : null;
                _mSetGold = _tModel != null ? AccessTools.PropertySetter(_tModel, "LastGeneratedGold") : null;
                Wire(h, _tModel, "GenerateLootFromUnitList", null, "CoinsPostfix", "monety z cial", _mGetGold != null && _mSetGold != null);
                _mGetTrade = _tBag != null ? AccessTools.PropertyGetter(_tBag, "LastRecoveredTradeGold") : null;
                _mSetTrade = _tBag != null ? AccessTools.PropertySetter(_tBag, "LastRecoveredTradeGold") : null;
                Wire(h, _tBag, "GenerateBaggageLoot", null, "TradePostfix", "zloto taboru", _mGetTrade != null && _mSetTrade != null);

                // 4. zdarzenia
                if (_tLoot != null)
                {
                    _fEvTriggered = AccessTools.Field(_tLoot, "_eventTriggered"); _fEvProgress = AccessTools.Field(_tLoot, "_currentProgress");
                    _fEvLoot = AccessTools.Field(_tLoot, "_generatedLoot"); _fEvMessage = AccessTools.Field(_tLoot, "_eventMessage");
                }
                Wire(h, _tLoot, "TryTriggerRandomEvent", "EventPrefix", "EventPostfix", "zdarzenia przy zbieraniu",
                     _fEvTriggered != null && _fEvProgress != null && _fEvLoot != null && _fEvMessage != null);

                // 5. paser
                if (_tLoot != null)
                {
                    _fFenceCount = AccessTools.Field(_tLoot, "_fenceItemCount"); _fFenceLimit = AccessTools.Field(_tLoot, "_fenceGoldLimit");
                    _fFenceRate = AccessTools.Field(_tLoot, "_fenceRate"); _fFenceBargain = AccessTools.Field(_tLoot, "_fenceBargainSuccess");
                    _fFenceBonus = AccessTools.Field(_tLoot, "_fenceBargainBonus"); _fJournal = AccessTools.Field(_tLoot, "_journal");
                    _mTypeCoef = AccessTools.Method(_tLoot, "GetItemTypeCoefficient"); _mFenceCat = AccessTools.Method(_tLoot, "GetFenceCategory");
                    _mBulk = AccessTools.Method(_tLoot, "CalculateBulkDiscount"); _mCrime = AccessTools.Method(_tLoot, "ApplyFenceCrimeCheck");
                    _mCooldown = AccessTools.Method(_tLoot, "SetFenceCooldown");
                }
                Wire(h, _tLoot, "ExecuteFenceSale", "FencePrefix", "FencePostfix", "paser",
                     _fFenceCount != null && _fFenceLimit != null && _fFenceRate != null && _fFenceBargain != null && _fFenceBonus != null && _fJournal != null
                     && _mTypeCoef != null && _mFenceCat != null && _mBulk != null && _mCrime != null && _mCooldown != null && _mRecordFence != null && _mIsLooted != null);
                Wire(h, _tLoot, "CalculateGoldLimit", null, "FenceLimitPostfix", "limit pasera", true);

                // 6. przetop, 9. magazyn
                Wire(h, _tQm, "OnSalvageScreenClosed", "SalvagePrefix", "SalvagePostfix", "przetop", _fSalvage != null && _pJournal != null && _mRecordSalvage != null);
                Wire(h, _tQm, "OnStockpileScreenClosed", "StockpilePrefix", null, "magazyn wojenny", _fStockScreen != null && _mLootedMod != null && _mIsLooted != null);

                // 7-8. klan najemnikow
                if (_tSub != null)
                {
                    _fScLeader = AccessTools.Field(_tSub, "_leader"); _fScData = AccessTools.Field(_tSub, "_data");
                    _fScClan = AccessTools.Field(_tSub, "_subClan"); _fScDonate = AccessTools.Field(_tSub, "_donateScreenRoster");
                    _mDonationGold = AccessTools.Method(_tSub, "CalculateDonationGold");
                }
                Wire(h, _tSub, "OnDonateGearScreenClosed", "DonatePrefix", "DonatePostfix", "dar dla klanu",
                     _fScLeader != null && _fScData != null && _fScClan != null && _fScDonate != null && _mDonationGold != null);
                Wire(h, _tSub, "OnSubClanMapEventEnded", "IncomePrefix", "IncomePostfix", "udzial z bitew klanu", _fScLeader != null && _fScData != null);

                string ver = "?";
                try { var sm = Find("RealisticLoot.RealisticLootSubModule"); var f = sm != null ? sm.GetField("Version") : null; if (f != null) ver = f.GetRawConstantValue() as string; } catch { }
                Log.Info("SpoilsSeal: Spoils of War (RealisticLoot " + ver + ") - wpiete: " + string.Join(", ", _wired.ToArray())
                         + (_missing.Count > 0 ? " | BRAK (te sciezki Spoils BEZ ZMIAN - sprawdzic dekompilacje): " + string.Join(", ", _missing.ToArray()) : " | BRAK: nic")
                         + " - sprzedaz automatyczna magazynu wojennego blokowana wedle wlacznika Spoils No Auto Sale, reszta zlota z niczego wedle Spoils No Free Gold"
                         + " (oba domyslnie wlaczone; reszta Spoils bez zmian); liczby - linia dnia \"Spoils of War (128)\".");
            }
            catch (Exception e) { Log.Error("SpoilsSeal.ApplyAll", e); }
        }
    }
}
