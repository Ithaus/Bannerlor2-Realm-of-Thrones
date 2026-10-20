using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// n20-wyceny - RESZTA AUDYTU WYCEN (docs/AUDYT-WYCENY-2026-10-19.md; PRZEKAZANIE-2026-10-19 p. 4; Jeff 19.10: "masz zgode na wszytko byle to skonczyc").
    /// Ta sama regula co f27 / f28 (badania czesci, ilosci zadan, Lotrostwo): stala w STAREJ monecie porownywana z wartoscia sztuki SPRZED przeliczenia cen
    /// historycznych (HistoricalPrices.OrigUnit - Orig x CountScale: ladunek rudy/drewna = 10 starych sztuk). Ceny, zaplaty i nagrody w zlocie zostaja w nowej
    /// monecie. Kazda regula pod swoim kluczem MCM; latki W KAMPANII (OnSessionLaunched, CLAUDE.md 7), raz na proces, tylko przy wlaczonym kluczu
    /// (bez klucza nic nie wpiete - sciezka WGRANIA 88); cel latki przez AccessTools.DeclaredMethod na klasie deklarujacej (pulapka 15.10).
    ///
    ///  A (pkt 12) HistWarehouseXpOldValue: DefaultWorkshopModel.GetTradeXpPerWarehouseProduction = GetBaseValue x 0.1 (BKWorkshopModel jej nie nadpisuje -
    ///    sprawdzone). Gra 1.4.8 wola ja (SkillLevelingManager.OnProductionProducedToWarehouse) przy kazdej sztuce warsztatu GRACZA oddanej na targ miasta
    ///    (WorkshopsCampaignBehavior.TickOneProductionCycleForPlayerWorkshop). Postfiks: 0.1 x (OrigUnit x mnoznik modyfikatora), nigdy mniej niz gra.
    ///  B (pkt 14) HistAiLootScoreOldCoin: AiVisitSettlementBehavior.CalculateSellItemScore i kopia BK (BKAIVisitSettlementBehavior - BK dodaje ja OBOK gry,
    ///    nie usuwa zachowania gry; obie licza). Czynnik lupu = 1 + min(3, (wartosc lupu bez koni i jedzenia / ((ludzie + 5) x 100))^0.33); postfiks
    ///    przelicza TEN czynnik z wartosci sprzed przeliczenia (czesc za konie - porownanie ze zlotem lorda - bez zmian), w armii pierwiastek jak gra.
    ///    Watki AI partii (PartiesThink rownolegle): tylko odczyt, liczniki przez Interlocked, bez alokacji.
    ///  C (pkt 15) HistBanditHideoutScoreOldCoin: CalculateVisitHideoutScoresForBanditParty (gra i BK): 1 + 4 x min(wartosc lupu, 1000) / 1000 - transpiler
    ///    ItemObject.get_Value -> BanditOld (jedyny odczyt wartosci w metodzie). Watki AI - jak B.
    ///  D (pkt 16) HistHideoutLootOldCoin: HideoutCampaignBehavior.OnCollectLootItems (nocny atak gracza na kryjowke) doklada towar z niczego do celu
    ///    min(lup x populacja x 135, 4750) w starej monecie, liczac kazda sztuke jako GetTheoreticalMaxItemMarketValue + 1 (Value x 10 albo x 1.3) - po
    ///    nowych cenach kilka-kilkadziesiat razy wiecej sztuk. Transpiler: wywolania GetItemValueForHideoutLoot w metodzie i w lambdzie wyboru -> HideoutValue
    ///    (wynik gry x OrigUnit / Value), NIGDY mniej niz wynik gry (recenzja 20.10: sztuka tansza dawniej niz dzis liczylaby sie taniej i gra
    ///    dokladalaby WIECEJ towaru z niczego niz bez klucza). Lista towarow gry (_potentialLootItems) powstaje w OnSessionLaunched gry - bez zmian.
    ///  E (pkt 18) BookGoalTrade: cel BK "Acquire Book" (AcquireBookDecision) bral Value x 30 (ksiazka 1 000 -> 30 000; u ksiegarza po Book Price Fix 1 000),
    ///    zloto w nicosc, ksiazka z niczego. Transpiler ShowInquiry / ApplyGoal: stala 30 -> BookMul() (1 przy kluczu); prefiks ApplyGoal - to samo kupno co
    ///    u ksiegarza (CraftOrders8.BookBuyPrefix): cena z listy, zloto do ksiegarza, ksiazka z jego oferty. Tylko przy Book Price Fix (inaczej Value x 1000
    ///    u ksiegarza i x1 bylby za tani).
    ///  F (pkt 22) HistFenceMinOldCoin i G (pkt 23) HistDonateRenownOldCoin - nasz kod SpoilsSeal (FenceSale, Donate): prog pasera i slawa daru od wartosci
    ///    sprzed przeliczenia, a gdy dzisiejsza wyzsza - od dzisiejszej (pomocniki FenceWorth / OldWorth tutaj). Recenzja 20.10: menu pasera Spoils
    ///    (CalculateFenceOffer - oferta, kategorie, licznik do rabatu ilosciowego; CalculateMerchantEstimate; ExecuteFenceSale Spoils, gdy SpoilsSeal
    ///    go nie przejmuje) liczylo prog dalej od Value - transpiler podmienia TYLKO odczyt wartosci stojacy w porownaniu z progiem
    ///    (get_Value, ldloc prog, blt/bge) na FenceWorth; cena Value x stawka bez zmian. Menu, oferta i sprzedaz licza te same sztuki.
    ///  Stara moneta towarow BK (klucz HistOldCoinFromDefinition, HistoricalPrices.OrigUnit): wartosc z definicji zamiast artefaktu BKROTPatch (/100).
    ///  Pkt 13 (Spoils: niszczenie drogich sztuk w bitwie > 5000) - NIE zmieniane: u Jeffa DestructionChanceMultiplier = 0.0 (RealisticLootSettings.json),
    ///  szansa = min(0.95, 0.3/0.5/0.7 x 0) = 0 - nic nie ginie, prog bez znaczenia.
    /// Log: start sesji "Wyceny (n20, klucz ...)" z "wpiete N/M"; doba "Handel z warsztatu (n20)", "Lup AI (n20)", "Kryjowki band (n20)"; zdarzenia
    /// "Kryjowka nocna (n20)", "Ksiazka z celu BK (n20)", "Paser (n20)", "Dar dla kompanii (n20)". Wyjatek = licznik potkniec (per regula), bez wylacznika.
    /// </summary>
    internal static class Wyceny20
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private static bool Prices { get { return HistoricalPrices.On && HistoricalPrices.Applied; } }
        internal static bool WareOn { get { var s = Settings.Current; return s != null && s.HistWarehouseXpOldValue && Prices; } }
        internal static bool LootOn { get { var s = Settings.Current; return s != null && s.HistAiLootScoreOldCoin && Prices; } }
        internal static bool BanditOn { get { var s = Settings.Current; return s != null && s.HistBanditHideoutScoreOldCoin && Prices; } }
        internal static bool HideoutOn { get { var s = Settings.Current; return s != null && s.HistHideoutLootOldCoin && Prices; } }
        internal static bool BookOn { get { var s = Settings.Current; return s != null && s.BookGoalTrade && s.BookPriceFix; } }
        internal static bool FenceOn { get { var s = Settings.Current; return s != null && s.HistFenceMinOldCoin && Prices; } }
        internal static bool RenownOn { get { var s = Settings.Current; return s != null && s.HistDonateRenownOldCoin && Prices; } }

        private static Harmony _h;
        private static int _stA, _stB, _stC, _stD, _stE, _stFG;

        private static void Stumble(ref int counter, string where, Exception e)
        {
            int n = Interlocked.Increment(ref counter);
            if (n <= 3) { try { Log.Error("Wyceny20." + where, e); } catch { } }
        }

        /// <summary>Wartosc JEDNEJ sztuki w starej monecie (OrigUnit); brak zapisu - Value. Tylko odczyt slownikow cen (watkobezpieczne).</summary>
        private static int OldUnit(ItemObject it)
        {
            int v = it.Value;
            int o = HistoricalPrices.OrigUnit(it);
            return o > 0 ? o : v;
        }

        private static string D(double x) { return x.ToString("0.##", Inv); }
        private static int Day() { try { return (int)CampaignTime.Now.ToDays; } catch { return -1; } }

        private static bool IsGetValue(CodeInstruction ci, MethodInfo getValue)
        {
            return (ci.opcode == OpCodes.Callvirt || ci.opcode == OpCodes.Call) && Equals(ci.operand, getValue);
        }

        // ============================================================ A. XP Handlu z warsztatow gracza (pkt 12)

        private static bool _triedA; private static int _hookedA; private static string _statusA = "";
        private static int _aN, _aUp, _aLines; private static double _aGame, _aAdd;

        public static void WarePost(EquipmentElement __0, ref float __result)
        {
            if (!WareOn) return;
            try
            {
                var it = __0.Item;
                if (it == null) return;
                _aN++; _aGame += __result;
                int v = it.Value, o = HistoricalPrices.OrigUnit(it);
                if (v <= 0 || o <= 0 || o == v) return;
                int baseOld = o;
                if (__0.ItemModifier != null) baseOld = (int)((float)o * __0.ItemModifier.PriceMultiplier);   // ten sam wzor co EquipmentElement.GetBaseValue
                float r = (float)baseOld * 0.1f;
                if (r <= __result) return;                                    // nigdy mniej niz gra (towar drozszy niz dawniej - XP gry)
                _aUp++; _aAdd += r - __result;
                if (_aLines < 10)
                {
                    _aLines++;
                    Log.Info("Handel z warsztatu (n20): " + it.StringId + " - wartosc dzis " + v + " d, sprzed przeliczenia " + o + " - XP Handlu gry " + D(__result) + " -> " + D(r) + ".");
                }
                __result = r;
            }
            catch (Exception e) { Stumble(ref _stA, "WarePost", e); }
        }

        private static void HookA()
        {
            var s = Settings.Current;
            const string K = "Wyceny (n20, klucz Hist Warehouse Xp Old Value): ";
            if (_triedA) { Log.Info(K + (WareOn ? "CZYNNE" : "NIECZYNNE (klucz albo ceny historyczne wylaczone)") + " - " + _statusA + "."); return; }
            if (s == null || !s.HistWarehouseXpOldValue || !HistoricalPrices.On)
            {
                Log.Info(K + (s != null && s.HistWarehouseXpOldValue ? "ceny historyczne wylaczone" : "WYLACZONE (MCM)") + " - nic nie wpiete, XP Handlu z warsztatow jak w grze.");
                return;   // bez _triedA: wlaczony pozniej - wpina przy nastepnym wczytaniu
            }
            _triedA = true;
            string miss = "";
            try
            {
                var m = AccessTools.DeclaredMethod(typeof(DefaultWorkshopModel), "GetTradeXpPerWarehouseProduction", new[] { typeof(EquipmentElement) });
                if (m == null) miss = "BRAK DefaultWorkshopModel.GetTradeXpPerWarehouseProduction";
                else { _h.Patch(m, postfix: new HarmonyMethod(typeof(Wyceny20), nameof(WarePost))); _hookedA++; }
            }
            catch (Exception e) { miss = "BLAD (" + e.GetType().Name + ")"; Stumble(ref _stA, "HookA", e); }
            string model = "?";
            try { var wm = Campaign.Current != null && Campaign.Current.Models != null ? Campaign.Current.Models.WorkshopModel : null; model = wm != null ? wm.GetType().FullName : "-"; } catch { }
            _statusA = "wpiete " + _hookedA + "/1 (DefaultWorkshopModel.GetTradeXpPerWarehouseProduction; czynny model " + model + ")" + (miss.Length > 0 ? "; " + miss : "");
            Log.Info(K + "XP Handlu ze sztuk warsztatow gracza od wartosci sprzed przeliczenia - " + _statusA + ".");
        }

        // ============================================================ B. chec sprzedazy lupu lordow AI (pkt 14)

        private static bool _triedB; private static int _hookedB; private static string _statusB = "";
        private static int _bV, _bK, _bChg; private static long _bGameMil, _bOldMil;

        public static void SellPostV(MobileParty __0, ref float __result) { Sell(__0, ref __result, false); }
        public static void SellPostK(MobileParty __0, ref float __result) { Sell(__0, ref __result, true); }

        private static void Sell(MobileParty mp, ref float result, bool bk)
        {
            if (mp == null || !LootOn) return;
            try
            {
                var ro = mp.ItemRoster;
                if (ro == null) return;
                double sv = 0, so = 0;
                for (int i = 0; i < ro.Count; i++)
                {
                    var el = ro.GetElementCopyAtIndex(i);
                    var it = el.EquipmentElement.Item;
                    if (it == null || it.IsMountable || it.IsFood) continue;   // ta sama czesc co gra (konie osobno, jedzenie poza)
                    int v = it.Value;
                    sv += (double)el.Amount * v;
                    so += (double)el.Amount * OldUnit(it);
                }
                if (bk) Interlocked.Increment(ref _bK); else Interlocked.Increment(ref _bV);
                double men = mp.MemberRoster != null ? mp.MemberRoster.TotalManCount : 0;
                double d = (men + 5.0) * 100.0;
                double fg = 1.0 + Math.Min(3.0, Math.Pow(Math.Max(0.0, sv) / d, 0.33));
                double fo = 1.0 + Math.Min(3.0, Math.Pow(Math.Max(0.0, so) / d, 0.33));
                Interlocked.Add(ref _bGameMil, (long)(fg * 1000.0));
                Interlocked.Add(ref _bOldMil, (long)(fo * 1000.0));
                if (so == sv || fg <= 0.0) return;
                double k = fo / fg;
                if (mp.Army != null) k = Math.Sqrt(k);                            // gra: w armii pierwiastek z iloczynu czynnikow
                result = (float)(result * k);
                Interlocked.Increment(ref _bChg);
            }
            catch (Exception e) { Stumble(ref _stB, "Sell", e); }
        }

        private static void HookB()
        {
            var s = Settings.Current;
            const string K = "Wyceny (n20, klucz Hist Ai Loot Score Old Coin): ";
            if (_triedB) { Log.Info(K + (LootOn ? "CZYNNE" : "NIECZYNNE (klucz albo ceny historyczne wylaczone)") + " - " + _statusB + "."); return; }
            if (s == null || !s.HistAiLootScoreOldCoin || !HistoricalPrices.On)
            {
                Log.Info(K + (s != null && s.HistAiLootScoreOldCoin ? "ceny historyczne wylaczone" : "WYLACZONE (MCM)") + " - nic nie wpiete, chec sprzedazy lupu AI jak w grze.");
                return;
            }
            _triedB = true;
            var miss = new List<string>();
            var plan = new[]
            {
                new KeyValuePair<string, string>("TaleWorlds.CampaignSystem.CampaignBehaviors.AiBehaviors.AiVisitSettlementBehavior", nameof(SellPostV)),
                new KeyValuePair<string, string>("BannerKings.Behaviours.BKAIVisitSettlementBehavior", nameof(SellPostK)),
            };
            foreach (var p in plan)
            {
                try
                {
                    var t = AccessTools.TypeByName(p.Key);
                    var m = t != null ? AccessTools.DeclaredMethod(t, "CalculateSellItemScore", new[] { typeof(MobileParty) }) : null;
                    if (m == null || m.ReturnType != typeof(float)) { miss.Add(p.Key + " BRAK"); continue; }
                    _h.Patch(m, postfix: new HarmonyMethod(typeof(Wyceny20), p.Value));
                    _hookedB++;
                }
                catch (Exception e) { miss.Add(p.Key + " BLAD (" + e.GetType().Name + ")"); Stumble(ref _stB, "HookB", e); }
            }
            _statusB = "wpiete " + _hookedB + "/2 (AiVisitSettlementBehavior / BKAIVisitSettlementBehavior.CalculateSellItemScore)" + (miss.Count > 0 ? "; BRAK: " + string.Join(", ", miss.ToArray()) : "");
            Log.Info(K + "czynnik lupu w checi wizyty w miescie od wartosci sprzed przeliczenia - " + _statusB + ".");
        }

        // ============================================================ C. powrot band z lupem do kryjowki (pkt 15)

        private static bool _triedC; private static int _hookedC, _swapsC; private static string _statusC = "";
        private static int _cN, _cUp; private static long _cGame, _cOld;
        private static MethodInfo _getValue;

        /// <summary>Zamiast ItemObject.get_Value w CalculateVisitHideoutScoresForBanditParty (watki AI). Null - ten sam wyjatek co gra.</summary>
        public static int BanditOld(ItemObject it)
        {
            int v = it.Value;
            if (!BanditOn) return v;
            try
            {
                int o = HistoricalPrices.OrigUnit(it);
                Interlocked.Increment(ref _cN);
                if (o <= 0 || o == v) return v;
                Interlocked.Increment(ref _cUp);
                Interlocked.Add(ref _cGame, v); Interlocked.Add(ref _cOld, o);
                return o;
            }
            catch (Exception e) { Stumble(ref _stC, "BanditOld", e); return v; }
        }

        public static IEnumerable<CodeInstruction> BanditIl(IEnumerable<CodeInstruction> instructions)
        {
            var list = new List<CodeInstruction>(instructions);
            var to = AccessTools.Method(typeof(Wyceny20), nameof(BanditOld));
            int n = 0;
            foreach (var ci in list)
                if (_getValue != null && to != null && IsGetValue(ci, _getValue)) { ci.opcode = OpCodes.Call; ci.operand = to; n++; }
            _swapsC = n;   // przypisanie: Harmony puszcza transpiler od nowa przy kolejnej latce na te metode
            return list;
        }

        private static void HookC()
        {
            var s = Settings.Current;
            const string K = "Wyceny (n20, klucz Hist Bandit Hideout Score Old Coin): ";
            if (_triedC) { Log.Info(K + (BanditOn ? "CZYNNE" : "NIECZYNNE (klucz albo ceny historyczne wylaczone)") + " - " + _statusC + "."); return; }
            if (s == null || !s.HistBanditHideoutScoreOldCoin || !HistoricalPrices.On)
            {
                Log.Info(K + (s != null && s.HistBanditHideoutScoreOldCoin ? "ceny historyczne wylaczone" : "WYLACZONE (MCM)") + " - nic nie wpiete, powrot band do kryjowki jak w grze.");
                return;
            }
            _triedC = true;
            var miss = new List<string>(); int swaps = 0;
            foreach (var tn in new[] { "TaleWorlds.CampaignSystem.CampaignBehaviors.AiBehaviors.AiVisitSettlementBehavior", "BannerKings.Behaviours.BKAIVisitSettlementBehavior" })
            {
                try
                {
                    var t = AccessTools.TypeByName(tn);
                    var m = t != null ? AccessTools.DeclaredMethod(t, "CalculateVisitHideoutScoresForBanditParty") : null;
                    if (m == null) { miss.Add(tn + " BRAK"); continue; }
                    _swapsC = 0;
                    _h.Patch(m, transpiler: new HarmonyMethod(typeof(Wyceny20), nameof(BanditIl)));
                    if (_swapsC == 1) { _hookedC++; swaps += _swapsC; }
                    else { swaps += _swapsC; miss.Add(tn + " (odczytow wartosci " + _swapsC + "/1)"); }
                }
                catch (Exception e) { miss.Add(tn + " BLAD (" + e.GetType().Name + ")"); Stumble(ref _stC, "HookC", e); }
            }
            _statusC = "wpiete " + _hookedC + "/2 (CalculateVisitHideoutScoresForBanditParty gry i BK, podmian " + swaps + ")" + (miss.Count > 0 ? "; BRAK: " + string.Join(", ", miss.ToArray()) : "");
            Log.Info(K + "wartosc lupu bandy w checi powrotu do kryjowki od wartosci sprzed przeliczenia - " + _statusC + ".");
        }

        // ============================================================ D. towar po nocnym ataku na kryjowke (pkt 16)

        private static bool _triedD; private static int _hookedD, _swapsD; private static string _statusD = "";
        private static Func<HideoutCampaignBehavior, ItemObject, int> _hideVal;
        private static MethodInfo _mHideVal;
        private static int _dLines;

        /// <summary>Zamiast HideoutCampaignBehavior.GetItemValueForHideoutLoot w OnCollectLootItems i lambdzie wyboru: wynik gry (metoda gry, z latkami
        /// innych modow) w starej monecie = (wynik - 1) x OrigUnit / Value + 1. Zawsze co najmniej 1 - petla gry konczy sie jak dotad.</summary>
        public static int HideoutValue(HideoutCampaignBehavior beh, ItemObject item)
        {
            int g = _hideVal(beh, item);
            if (!HideoutOn || item == null) return g;
            try
            {
                int v = item.Value, o = HistoricalPrices.OrigUnit(item);
                if (v <= 0 || o <= v) return g;                               // recenzja 20.10: dawniej tansza (albo bez zapisu) - wynik gry, nigdy wiecej towaru niz bez klucza
                long c = (long)Math.Round((g - 1) * (double)o / v) + 1;
                return c > g ? (int)Math.Min(c, int.MaxValue) : g;
            }
            catch (Exception e) { Stumble(ref _stD, "HideoutValue", e); return g; }
        }

        public static IEnumerable<CodeInstruction> HideoutIl(IEnumerable<CodeInstruction> instructions)
        {
            var list = new List<CodeInstruction>(instructions);
            var to = AccessTools.Method(typeof(Wyceny20), nameof(HideoutValue));
            int n = 0;
            foreach (var ci in list)
                if (_mHideVal != null && to != null && (ci.opcode == OpCodes.Call || ci.opcode == OpCodes.Callvirt) && Equals(ci.operand, _mHideVal)) { ci.opcode = OpCodes.Call; ci.operand = to; n++; }
            _swapsD = n;
            return list;
        }

        public struct HideSnap { public bool On; public long N, Now, Old; }

        private static void Sum(ItemRoster r, out long n, out long now, out long old)
        {
            n = now = old = 0;
            for (int i = 0; i < r.Count; i++)
            {
                var el = r.GetElementCopyAtIndex(i);
                var it = el.EquipmentElement.Item;
                if (it == null || el.Amount <= 0) continue;
                n += el.Amount; now += (long)el.Amount * it.Value; old += (long)el.Amount * OldUnit(it);
            }
        }

        public static void HidePre(PartyBase __0, ItemRoster __1, out HideSnap __state)
        {
            __state = default(HideSnap);
            if (!HideoutOn || __1 == null || __0 == null || __0 != PartyBase.MainParty) return;
            try { __state.On = true; Sum(__1, out __state.N, out __state.Now, out __state.Old); }
            catch (Exception e) { __state.On = false; Stumble(ref _stD, "HidePre", e); }
        }

        public static void HidePost(ItemRoster __1, HideSnap __state)
        {
            if (!__state.On || __1 == null) return;
            try
            {
                long n, now, old;
                Sum(__1, out n, out now, out old);
                if (n <= __state.N || _dLines >= 50) return;
                _dLines++;
                Log.Info("Kryjowka nocna (n20): gra dolozyla do lupu " + (n - __state.N) + " szt. towaru - wartosc dzis " + (now - __state.Now) + " d, sprzed przeliczenia "
                         + (old - __state.Old) + " (cel gry najwyzej 4750 w starej monecie; bez klucza liczone po dzisiejszej wartosci).");
            }
            catch (Exception e) { Stumble(ref _stD, "HidePost", e); }
        }

        private static void HookD()
        {
            var s = Settings.Current;
            const string K = "Wyceny (n20, klucz Hist Hideout Loot Old Coin): ";
            if (_triedD) { Log.Info(K + (HideoutOn ? "CZYNNE" : "NIECZYNNE (klucz albo ceny historyczne wylaczone)") + " - " + _statusD + "."); return; }
            if (s == null || !s.HistHideoutLootOldCoin || !HistoricalPrices.On)
            {
                Log.Info(K + (s != null && s.HistHideoutLootOldCoin ? "ceny historyczne wylaczone" : "WYLACZONE (MCM)") + " - nic nie wpiete, towar po nocnym ataku na kryjowke jak w grze.");
                return;
            }
            _triedD = true;
            var miss = new List<string>(); int want = 0, swaps = 0;
            try
            {
                var t = typeof(HideoutCampaignBehavior);
                _mHideVal = AccessTools.DeclaredMethod(t, "GetItemValueForHideoutLoot", new[] { typeof(ItemObject) });
                if (_mHideVal == null || _mHideVal.ReturnType != typeof(int) || _mHideVal.IsStatic) miss.Add("BRAK GetItemValueForHideoutLoot");
                else
                {
                    _hideVal = (Func<HideoutCampaignBehavior, ItemObject, int>)Delegate.CreateDelegate(typeof(Func<HideoutCampaignBehavior, ItemObject, int>), _mHideVal);
                    var main = AccessTools.DeclaredMethod(t, "OnCollectLootItems", new[] { typeof(PartyBase), typeof(ItemRoster) });
                    var targets = new List<MethodBase>();
                    if (main != null) targets.Add(main); else miss.Add("BRAK OnCollectLootItems");
                    // lambda wyboru (klasa zagniezdzona <>c__DisplayClass..): jedyna metoda klas zagniezdzonych wolajaca GetItemValueForHideoutLoot
                    var any = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
                    foreach (var nt in t.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic))
                        foreach (var mm in nt.GetMethods(any))
                        {
                            try
                            {
                                if (mm.IsAbstract || mm.ContainsGenericParameters || mm.GetMethodBody() == null) continue;
                                foreach (var kv in PatchProcessor.ReadMethodBody(mm))
                                    if (Equals(kv.Value, _mHideVal)) { targets.Add(mm); break; }
                            }
                            catch { }
                        }
                    if (targets.Count < 2) miss.Add("lambda wyboru BRAK");
                    foreach (var m in targets)
                    {
                        want++;
                        try
                        {
                            _swapsD = 0;
                            bool isMain = ReferenceEquals(m, main);
                            _h.Patch(m, prefix: isMain ? new HarmonyMethod(typeof(Wyceny20), nameof(HidePre)) : null,
                                     postfix: isMain ? new HarmonyMethod(typeof(Wyceny20), nameof(HidePost)) : null,
                                     transpiler: new HarmonyMethod(typeof(Wyceny20), nameof(HideoutIl)));
                            swaps += _swapsD;
                            if (_swapsD > 0) _hookedD++; else miss.Add(m.DeclaringType.Name + "." + m.Name + " (0 wywolan)");
                        }
                        catch (Exception e) { miss.Add(m.Name + " BLAD (" + e.GetType().Name + ")"); Stumble(ref _stD, "HookD(" + m.Name + ")", e); }
                    }
                }
            }
            catch (Exception e) { miss.Add("wyjatek"); Stumble(ref _stD, "HookD", e); }
            _statusD = "wpiete " + _hookedD + "/" + Math.Max(2, want) + " (HideoutCampaignBehavior.OnCollectLootItems + lambda wyboru, wywolan wartosci " + swaps + ", oczekiwane 3)" + (miss.Count > 0 ? "; BRAK: " + string.Join(", ", miss.ToArray()) : "");
            Log.Info(K + "towar po nocnym ataku na kryjowke liczony w starej monecie - " + _statusD + ".");
        }

        // ============================================================ E. cel BK "Acquire Book" (pkt 18)

        private static bool _triedE; private static int _hookedE, _swapsE; private static string _statusE = "";
        private static FieldInfo _fBook, _fBeh;
        private static MethodInfo _mFulfiller, _mSeller, _mRoster;
        private static PropertyInfo _pItem;
        private static int _eSold, _eOld, _eGone, _ePoor; private static long _eGold;

        /// <summary>Zamiast stalej 30 (cena = Value x 30) w ShowInquiry i ApplyGoal: 1 przy kluczu (cena z listy ksiegarza), inaczej 30 jak BK.</summary>
        public static int BookMul() { return BookOn ? 1 : 30; }

        public static IEnumerable<CodeInstruction> BookIl(IEnumerable<CodeInstruction> instructions)
        {
            var list = new List<CodeInstruction>(instructions);
            var to = AccessTools.Method(typeof(Wyceny20), nameof(BookMul));
            int n = 0;
            for (int i = 1; i + 1 < list.Count; i++)
            {
                var ci = list[i];
                bool k30 = ci.LoadsConstant(30L);   // ldc.i4.s 30 (IL BK 1.4.8: ShowInquiry get_Value, 30, mul; ApplyGoal get_Value, neg, 30, mul)
                if (!k30 || list[i + 1].opcode != OpCodes.Mul) continue;
                bool afterValue = (_getValue != null && IsGetValue(list[i - 1], _getValue)) || (list[i - 1].opcode == OpCodes.Neg && i >= 2 && _getValue != null && IsGetValue(list[i - 2], _getValue));
                if (!afterValue || to == null) continue;
                ci.opcode = OpCodes.Call; ci.operand = to; n++;   // etykiety skokow zostaja na tej samej instrukcji
            }
            _swapsE = n;
            return list;
        }

        /// <summary>Prefiks AcquireBookDecision.ApplyGoal: kupno jak u ksiegarza - cena = Value (z listy), zloto do ksiegarza, ksiazka z jego oferty do sakw.
        /// Brak danych (brak ksiegarza / oferty / druzyny) - stara droga BK z cena x BookMul (1).</summary>
        public static bool BookGoalPrefix(object __instance)
        {
            if (!BookOn || __instance == null) return true;
            Hero buyer = null, seller = null; ItemObject item = null; ItemRoster roster = null; MobileParty party = null;
            try
            {
                buyer = _mFulfiller != null ? _mFulfiller.Invoke(__instance, null) as Hero : null;
                var bt = _fBook != null ? _fBook.GetValue(__instance) : null;
                if (bt != null && (_pItem == null || !_pItem.DeclaringType.IsInstanceOfType(bt))) _pItem = bt.GetType().GetProperty("Item", BindingFlags.Public | BindingFlags.Instance);
                item = bt != null && _pItem != null ? _pItem.GetValue(bt, null) as ItemObject : null;
                var beh = _fBeh != null ? _fBeh.GetValue(__instance) : null;
                var st = buyer != null ? buyer.CurrentSettlement : null;
                if (beh != null && st != null)
                {
                    if (_mSeller != null) seller = _mSeller.Invoke(beh, new object[] { st }) as Hero;
                    if (_mRoster != null) roster = _mRoster.Invoke(beh, new object[] { st }) as ItemRoster;
                }
                party = buyer != null ? buyer.PartyBelongedTo : null;
                if (buyer == null || item == null || seller == null || roster == null || party == null || party.ItemRoster == null) { _eOld++; return true; }
            }
            catch (Exception e) { Stumble(ref _stE, "BookGoalPrefix(odczyt)", e); _eOld++; return true; }
            try
            {
                string sn = seller.Name != null ? seller.Name.ToString() : "The bookseller";
                if (roster.GetItemNumber(item) <= 0) { _eGone++; Log.Player(sn + " no longer has " + item.Name + ".", true); return false; }
                int price = Math.Max(0, item.Value);
                if (buyer.Gold < price) { _ePoor++; Log.Player("You cannot afford " + item.Name + " (" + price + ").", true); return false; }
                roster.AddToCounts(item, -1);
                party.ItemRoster.AddToCounts(item, 1);
                if (price > 0) GiveGoldAction.ApplyBetweenCharacters(buyer, seller, price, false);
                _eSold++; _eGold += price;
                Log.Info("Ksiazka z celu BK (n20): " + buyer.Name + " kupil " + item.StringId + " od " + sn + " (" + (buyer.CurrentSettlement != null ? buyer.CurrentSettlement.StringId : "?")
                         + ") za " + price + " (BK bral " + ((long)item.Value * 30) + " w nicosc, ksiazka z niczego); kiesa ksiegarza " + seller.Gold + ", w ofercie zostalo " + roster.Count + " pozycji"
                         + " | w sesji: kupionych " + _eSold + " za " + _eGold + ", stara droga " + _eOld + ".");
            }
            catch (Exception e) { Stumble(ref _stE, "BookGoalPrefix(kupno)", e); }
            return false;   // kupno (albo odmowa) zrobione u nas - ApplyGoal BK nie biegnie
        }

        private static void HookE()
        {
            var s = Settings.Current;
            const string K = "Wyceny (n20, klucz Book Goal Trade): ";
            if (_triedE) { Log.Info(K + (BookOn ? "CZYNNE" : "NIECZYNNE (klucz albo Book Price Fix wylaczone)") + " - " + _statusE + "."); return; }
            if (s == null || !s.BookGoalTrade || !s.BookPriceFix)
            {
                Log.Info(K + (s != null && s.BookGoalTrade ? "Book Price Fix wylaczone" : "WYLACZONE (MCM)") + " - nic nie wpiete, cel BK Acquire Book jak w BK (Value x 30).");
                return;
            }
            var t = AccessTools.TypeByName("BannerKings.Managers.Goals.Decisions.AcquireBookDecision");
            if (t == null) { Log.Info(K + "BannerKings bez AcquireBookDecision - nic do wpiecia."); return; }
            _triedE = true;
            var miss = new List<string>(); int swaps = 0;
            try
            {
                _fBook = AccessTools.Field(t, "book");
                _fBeh = AccessTools.Field(t, "behavior");
                _mFulfiller = AccessTools.Method(t, "GetFulfiller", Type.EmptyTypes);
                var bt = _fBeh != null ? _fBeh.FieldType : null;
                if (bt != null)
                {
                    _mSeller = AccessTools.Method(bt, "GetBookSeller", new[] { typeof(Settlement) });
                    _mRoster = AccessTools.Method(bt, "GetBookRoster", new[] { typeof(Settlement) });
                    if (_mSeller != null && (_mSeller.IsStatic || !typeof(Hero).IsAssignableFrom(_mSeller.ReturnType))) _mSeller = null;
                    if (_mRoster != null && (_mRoster.IsStatic || !typeof(ItemRoster).IsAssignableFrom(_mRoster.ReturnType))) _mRoster = null;
                }
                if (_fBook == null) miss.Add("pole book");
                if (_fBeh == null) miss.Add("pole behavior");
                if (_mFulfiller == null) miss.Add("GetFulfiller");
                if (_mSeller == null) miss.Add("GetBookSeller");
                if (_mRoster == null) miss.Add("GetBookRoster");
                bool dataOk = miss.Count == 0;   // bez danych do kupna u ksiegarza - tylko cena x1 (stara droga BK)
                foreach (var name in new[] { "ShowInquiry", "ApplyGoal" })
                {
                    var m = AccessTools.DeclaredMethod(t, name, Type.EmptyTypes);
                    if (m == null) { miss.Add(name + " BRAK"); continue; }
                    try
                    {
                        _swapsE = 0;
                        bool apply = name == "ApplyGoal";
                        bool trade = apply && dataOk;
                        _h.Patch(m, prefix: trade ? new HarmonyMethod(typeof(Wyceny20), nameof(BookGoalPrefix)) { priority = Priority.First } : null,
                                 transpiler: new HarmonyMethod(typeof(Wyceny20), nameof(BookIl)));
                        swaps += _swapsE;
                        if (_swapsE == 1) _hookedE++; else miss.Add(name + " (stalych 30: " + _swapsE + "/1)");
                        if (trade) _hookedE++;
                    }
                    catch (Exception e) { miss.Add(name + " BLAD (" + e.GetType().Name + ")"); Stumble(ref _stE, "HookE(" + name + ")", e); }
                }
            }
            catch (Exception e) { miss.Add("wyjatek"); Stumble(ref _stE, "HookE", e); }
            _statusE = "wpiete " + _hookedE + "/3 (ShowInquiry i ApplyGoal: stala 30 -> 1, podmian " + swaps + "; ApplyGoal: kupno u ksiegarza)" + (miss.Count > 0 ? "; BRAK: " + string.Join(", ", miss.ToArray()) : "");
            Log.Info(K + "cel BK Acquire Book za cene ksiegarza, zloto do ksiegarza, ksiazka z jego oferty - " + _statusE + ".");
        }

        // ============================================================ F, G. paser i dar dla kompanii Spoils (pkt 22, 23 - nasz SpoilsSeal)

        private static int _fExtra, _fLines, _gLines;
        private static bool _triedF; private static int _hookedF, _swapsF; private static string _statusF = "";

        /// <summary>Wartosc sztuki do progu Fence Min Item Value (stara moneta Spoils): przy kluczu wieksza z wartosci sprzed przeliczenia i dzisiejszej
        /// (paser nie odrzuca niczego, co bral bez klucza); bez klucza - Value. Wolane z SpoilsSeal.FenceSale i z IL Spoils (menu, oferta, sprzedaz Spoils)
        /// zamiast ItemObject.get_Value przy porownaniu z progiem. Null - ten sam wyjatek co Spoils.</summary>
        public static int FenceWorth(ItemObject it)
        {
            int v = it.Value;
            if (!FenceOn) return v;
            try
            {
                int o = OldUnit(it);
                return o > v ? o : v;
            }
            catch (Exception e) { Stumble(ref _stFG, "FenceWorth", e); return v; }
        }

        /// <summary>SpoilsSeal.FenceSale po filtrze kategorii: sztuka w ofercie, ktora dzisiejsza wartosc by odrzucila (do linii "Paser (n20)").</summary>
        internal static void FenceNote(ItemObject it, int amount, int min)
        {
            try { if (FenceOn && it != null && it.Value < min) _fExtra += Math.Max(0, amount); }
            catch (Exception e) { Stumble(ref _stFG, "FenceNote", e); }
        }

        public static IEnumerable<CodeInstruction> FenceIl(IEnumerable<CodeInstruction> instructions)
        {
            var list = new List<CodeInstruction>(instructions);
            var to = AccessTools.Method(typeof(Wyceny20), nameof(FenceWorth));
            int n = 0;
            for (int i = 0; i + 2 < list.Count; i++)
            {
                if (_getValue == null || to == null || !IsGetValue(list[i], _getValue) || !list[i + 1].IsLdloc()) continue;
                var op = list[i + 2].opcode;   // IL Spoils 1.4.8: get_Value, ldloc prog, blt / bge (porownanie z progiem); cena: get_Value, conv.r4 - nie ruszana
                if (op != OpCodes.Blt && op != OpCodes.Blt_S && op != OpCodes.Bge && op != OpCodes.Bge_S && op != OpCodes.Clt && op != OpCodes.Cgt) continue;
                list[i].opcode = OpCodes.Call; list[i].operand = to; n++;
            }
            _swapsF = n;
            return list;
        }

        private static void HookF()
        {
            var s = Settings.Current;
            const string K = "Wyceny (n20, klucz Hist Fence Min Old Coin, menu Spoils): ";
            if (_triedF) { Log.Info(K + (FenceOn ? "CZYNNE" : "NIECZYNNE (klucz albo ceny historyczne wylaczone)") + " - " + _statusF + "."); return; }
            if (s == null || !s.HistFenceMinOldCoin || !HistoricalPrices.On)
            {
                Log.Info(K + (s != null && s.HistFenceMinOldCoin ? "ceny historyczne wylaczone" : "WYLACZONE (MCM)") + " - nic nie wpiete, prog pasera w menu Spoils od dzisiejszej wartosci.");
                return;
            }
            var t = AccessTools.TypeByName("RealisticLoot.Behaviors.LootCollectionBehavior");
            if (t == null) { Log.Info(K + "Spoils of War nieobecny - nic do wpiecia."); return; }
            _triedF = true;
            var miss = new List<string>(); int swaps = 0;
            var want = new[] { new KeyValuePair<string, int>("CalculateFenceOffer", 2), new KeyValuePair<string, int>("CalculateMerchantEstimate", 1), new KeyValuePair<string, int>("ExecuteFenceSale", 1) };
            foreach (var w in want)
            {
                try
                {
                    MethodInfo m = null;
                    foreach (var mm in t.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                        if (mm.Name == w.Key) { if (m != null) { m = null; miss.Add(w.Key + " (kilka przeciazen)"); break; } m = mm; }
                    if (m == null) { if (!miss.Exists(x => x.StartsWith(w.Key, StringComparison.Ordinal))) miss.Add(w.Key + " BRAK"); continue; }
                    _swapsF = 0;
                    _h.Patch(m, transpiler: new HarmonyMethod(typeof(Wyceny20), nameof(FenceIl)));
                    swaps += _swapsF;
                    if (_swapsF == w.Value) _hookedF++; else miss.Add(w.Key + " (porownan z progiem " + _swapsF + "/" + w.Value + ")");
                }
                catch (Exception e) { miss.Add(w.Key + " BLAD (" + e.GetType().Name + ")"); Stumble(ref _stFG, "HookF(" + w.Key + ")", e); }
            }
            _statusF = "wpiete " + _hookedF + "/3 (LootCollectionBehavior.CalculateFenceOffer / CalculateMerchantEstimate / ExecuteFenceSale: prog od wartosci sprzed przeliczenia, podmian " + swaps + ", oczekiwane 4)"
                       + (miss.Count > 0 ? "; BRAK: " + string.Join(", ", miss.ToArray()) : "");
            Log.Info(K + "menu, oferta i sprzedaz pasera licza te same sztuki - " + _statusF + ".");
        }

        /// <summary>Koniec FenceSale: linia, gdy prog sprzed przeliczenia wpuscil do oferty sztuki, ktore Value by odrzucilo.</summary>
        internal static void FenceDone(int min)
        {
            try
            {
                if (_fExtra > 0 && _fLines < 30)
                {
                    _fLines++;
                    Log.Info("Paser (n20, klucz Hist Fence Min Old Coin): prog " + min + " (stara moneta Spoils) wobec wartosci sprzed przeliczenia - w ofercie " + _fExtra + " szt., ktore dzisiejsza wartosc by odrzucila.");
                }
            }
            catch (Exception e) { Stumble(ref _stFG, "FenceDone", e); }
            _fExtra = 0;
        }

        /// <summary>SpoilsSeal.Donate: wartosc sztuki (z modyfikatorem, jak GetBaseValue) sprzed przeliczenia - do slawy daru.</summary>
        internal static int OldWorth(EquipmentElement el)
        {
            var it = el.Item;
            if (it == null) return 0;
            try
            {
                int o = OldUnit(it), v = it.Value;
                if (el.ItemModifier != null) { o = (int)((float)o * el.ItemModifier.PriceMultiplier); v = (int)((float)v * el.ItemModifier.PriceMultiplier); }
                return o > v ? o : v;   // recenzja 20.10: nigdy mniej niz dzisiejsza wartosc (ten sam wzor co Donate)
            }
            catch (Exception e) { Stumble(ref _stFG, "OldWorth", e); return it.Value; }
        }

        /// <summary>SpoilsSeal.Donate: wartosc do slawy (worth / 10000 jak Spoils) - sprzed przeliczenia przy kluczu; linia logu.</summary>
        internal static int RenownWorth(int sold, int worth, int oldWorth)
        {
            if (!RenownOn || oldWorth <= 0) return worth;
            try
            {
                if (_gLines < 30)
                {
                    _gLines++;
                    Log.Info("Dar dla kompanii (n20, klucz Hist Donate Renown Old Coin): sprzedano " + sold + " szt. wartosci " + worth + " d (sprzed przeliczenia " + oldWorth + ") - slawa "
                             + D(oldWorth / 10000.0) + " zamiast " + D(worth / 10000.0) + " (ponizej 0.1 - zero, jak Spoils).");
                }
            }
            catch (Exception e) { Stumble(ref _stFG, "RenownWorth", e); }
            return oldWorth;
        }

        // ============================================================ start sesji i doba

        /// <summary>Z ArmouryBehavior.OnSessionLaunched (po HistoricalPrices.Apply): latki raz na proces, kazda regula przy swoim kluczu.</summary>
        internal static void SessionStart()
        {
            _aN = _aUp = _aLines = 0; _aGame = _aAdd = 0;
            _bV = _bK = _bChg = 0; _bGameMil = _bOldMil = 0;
            _cN = _cUp = 0; _cGame = _cOld = 0;
            _dLines = 0; _fExtra = 0; _fLines = 0; _gLines = 0;
            _eSold = _eOld = _eGone = _ePoor = 0; _eGold = 0;
            try
            {
                if (_h == null) _h = new Harmony("com.jeff.armoury.n20wyceny");
                if (_getValue == null) _getValue = AccessTools.PropertyGetter(typeof(ItemObject), "Value");
            }
            catch (Exception e) { Log.Error("Wyceny20.SessionStart", e); return; }
            try { HookA(); } catch (Exception e) { Stumble(ref _stA, "HookA", e); }
            try { HookB(); } catch (Exception e) { Stumble(ref _stB, "HookB", e); }
            try { HookC(); } catch (Exception e) { Stumble(ref _stC, "HookC", e); }
            try { HookD(); } catch (Exception e) { Stumble(ref _stD, "HookD", e); }
            try { HookE(); } catch (Exception e) { Stumble(ref _stE, "HookE", e); }
            try { HookF(); } catch (Exception e) { Stumble(ref _stFG, "HookF", e); }
            try
            {
                int nd; string ld = HistoricalPrices.OldCoinFromDefList(out nd);
                Log.Info("Wyceny (n20, klucz Hist Old Coin From Definition): stara moneta towarow BK podzielonych przez BKROTPatch z definicji "
                         + (HistoricalPrices.OldCoinFromDef ? "CZYNNE" : "WYLACZONE (MCM) - wartosc z chwili przeliczenia, jak w WGRANIU 88") + " - przedmiotow " + nd
                         + (nd > 0 ? " [" + ld + "]" : "") + " (dotyczy regul f28 i n20: ilosci zadan, Lotrostwo, A-D, F, G).");
            }
            catch (Exception e) { Stumble(ref _stFG, "SessionStart(def)", e); }
            try
            {
                var s = Settings.Current;
                Log.Info("Wyceny (n20, klucze Hist Fence Min Old Coin / Hist Donate Renown Old Coin): paser Spoils - prog od wartosci sprzed przeliczenia "
                         + (FenceOn ? "CZYNNE" : "NIECZYNNE") + "; slawa daru dla kompanii Spoils od wartosci sprzed przeliczenia " + (RenownOn ? "CZYNNE" : "NIECZYNNE")
                         + " (sprzedaz i dar - nasz kod SpoilsSeal; menu pasera Spoils - linia \"menu Spoils\" wyzej; Spoils No Free Gold " + (s != null && s.SpoilsNoFreeGold ? "wlaczone" : "WYLACZONE - SpoilsSeal nie przejmuje pasera i daru") + ").");
            }
            catch (Exception e) { Stumble(ref _stFG, "SessionStart(FG)", e); }
        }

        /// <summary>Raz na dobe (ArmouryBehavior.OnDailyTick): linie doby regul A-C, zerowanie licznikow. Tylko log.</summary>
        internal static void Daily()
        {
            int day = Day() - 1;
            try
            {
                if (_hookedA > 0 && WareOn)
                    Log.Info("Handel z warsztatu (n20): dzien " + day + " - sztuk z warsztatow gracza " + _aN + " (z dodatkiem " + _aUp + "), XP Handlu gry " + D(_aGame) + ", dodane od wartosci sprzed przeliczenia " + D(_aAdd)
                             + (_stA > 0 ? " | potkniecia " + _stA : "") + ".");
                if (_hookedB > 0 && LootOn)
                {
                    int n = _bV + _bK;
                    Log.Info("Lup AI (n20): dzien " + day + " - ocen checi sprzedazy lupu " + n + " (zachowanie gry " + _bV + ", BK " + _bK + "), zmienionych " + _bChg
                             + ", czynnik lupu srednio: gra " + (n > 0 ? D(_bGameMil / 1000.0 / n) : "-") + " -> od wartosci sprzed przeliczenia " + (n > 0 ? D(_bOldMil / 1000.0 / n) : "-")
                             + (_stB > 0 ? " | potkniecia " + _stB : "") + ".");
                }
                if (_hookedC > 0 && BanditOn)
                    Log.Info("Kryjowki band (n20): dzien " + day + " - pozycji lupu band policzonych " + _cN + " (przeliczonych " + _cUp + "), wartosc sztuki razem: gra " + _cGame + " -> sprzed przeliczenia " + _cOld
                             + (_stC > 0 ? " | potkniecia " + _stC : "") + ".");
            }
            catch (Exception e) { Stumble(ref _stA, "Daily", e); }
            _aN = _aUp = _aLines = 0; _aGame = _aAdd = 0;
            Interlocked.Exchange(ref _bV, 0); Interlocked.Exchange(ref _bK, 0); Interlocked.Exchange(ref _bChg, 0);
            Interlocked.Exchange(ref _bGameMil, 0); Interlocked.Exchange(ref _bOldMil, 0);
            Interlocked.Exchange(ref _cN, 0); Interlocked.Exchange(ref _cUp, 0); Interlocked.Exchange(ref _cGame, 0); Interlocked.Exchange(ref _cOld, 0);
        }
    }
}
