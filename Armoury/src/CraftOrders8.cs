using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.CraftingSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>
    /// E8/K (255, PLAN 8.3; rozpoznanie B rozdz. 1; projekt etapu 8 v2 rozdz. 3 "E8/K", krytyka W3) - KUZNIA: ZLECENIA KOWALSKIE GRY I KSIEGARZ BK.
    ///
    /// 1. ZLECENIA KOWALSKIE (CraftingOrder gry - zlecenia notabli i lordow w miescie oraz zlecenia zadania "gang leader needs special weapons").
    ///    Sprawdzone w TaleWorlds.CampaignSystem.dll i TaleWorlds.CampaignSystem.ViewModelCollection.dll z gry (ilspycmd, 12.10):
    ///      - konstruktor CraftingOrder: projekt broni z Crafting.GenerateItem (wartosc wzorem gry), BaseGoldReward = GetTheoreticalMaxItemMarketValue
    ///        (wartosc x 1.3) x (0.9-1.1) - nagroda w starej monecie (tysiace za bron, ktora po przeliczeniu jest warta dziesiatki albo setki);
    ///      - CompleteOrder: GiveGoldAction.ApplyBetweenCharacters(null, gracz, kwota) - zloto z niczego; wykuta bron nie trafia nigdzie (w trybie zlecenia
    ///        gra nie wklada jej nawet do sakw gracza - CreateCraftedWeaponInternal dodaje sztuke tylko w trybie swobodnym; WeaponDesignVM po oddaniu zeruje
    ///        CraftedItemObject) - chyba ze lord wezmie ja do reki (ChangeCraftedOrderWithTheNoblesWeaponIfItIsBetter; warunek praktycznie niespelnialny);
    ///        lord z druzyna dostaje zolnierza z niczego (GiveTroopToNobleAtWeaponTier);
    ///      - CreateTownOrder / CreateCustomOrderForHero: szablon CraftingTemplate.All.GetRandomElement(), czesci GetWeaponPieces(szablon, trudnosc / 50
    ///        albo / 40) - bez pytania, czy gracz ma w tej klasie otwarte czesci (brama kucia CrashScribe zamyka klasy z pula od T4).
    ///    Teraz (klucz CraftOrdersHistorical, true):
    ///      - postfiks konstruktora: projekt wyceniony jak kazda bron swiata (HistoricalPrices.PriceDesign), nagroda przeliczona tym samym stosunkiem
    ///        (zostaje rzut 0.9-1.1 gry); zlecenia sprzed sesji (zapis albo start nowej kampanii - powstaja przed przeliczeniem cen) - SessionStart:
    ///        nagroda przeliczona, gdy jest wieksza niz 3 x nowa (bez podwojnego przeliczenia);
    ///      - doswiadczenie za zlecenie (CraftingOrder.GetOrderExperience liczy z wartosci PROJEKTU) dalej od wartosci sprzed przeliczenia - postfiks;
    ///      - CompleteOrder: wyplata przez GiverPay.PayFromGiver - placi zleceniodawca ze swojej kiesy, przy pustej kiesie mniej i komunikat;
    ///        r4-zlecenia (klucz CraftOrderClanPurse, CraftOrderClanPurse.cs): brak lorda, ktory nie jest glowa rodu, doplaca glowa rodu (kiesa rodu);
    ///        wykuta bron: lord - jak gra, a gdy nie wzial do reki - do taboru jego druzyny; zleceniodawca bez druzyny (notabl, lord w miescie, zadanie) -
    ///        na polke miasta zlecenia; zolnierz z niczego wyciety (licznik).
    ///    Klucz CraftOrdersGiverCanPay (NZ, 16.10, CraftOrdersPurse.cs): zlecenie miejskie tylko od zleceniodawcy z kiesa na sume swoich zlecen (po przeliczeniu, +20% na premie); zlecenia
    ///    bez pokrycia albo zmarlego zleceniodawcy zdejmowane przy starcie sesji i co dobe (zlecenia zadan bez zmian).
    ///    Klucz CraftOrdersOpenedOnly (true): szablon losowany tylko z klas, w ktorych KAZDY slot szablonu ma czesc otwarta u gracza (IsOpened), tier czesci
    ///    najwyzej najwyzszy otwarty tier szablonu + 1. Brak takiej klasy = los gry (licznik). W nowej kampanii zlecenia startowe powstaja PRZED brama
    ///    kucia (OnNewGameCreatedPartialFollowUpEnd przed OnSessionLaunched) - raz, w pierwszym takcie doby, wymieniamy je metoda gry ReplaceCraftingOrder.
    ///
    /// 2. KSIEGARZ BK (klucz BookPriceFix, true; wartosci ksiazek - HistoricalPrices.Apply).
    ///    Sprawdzone w BannerKings.dll z gry (ilspycmd -il): BKEducationBehavior.OnBuyBookConsequence pokazuje cene Value x 1000 (nasz stary BookTranspiler
    ///    zamienia te stala na 1), a lambda kupna robi Hero.MainHero.ChangeHeroGold(-Value * 1000) - w IL "get_Value, neg, ldc.i4 1000, mul", wiec stary
    ///    BookTranspiler (stala zaraz po get_Value) jej NIE siegal (log: "w 1 metodach (stalych 1)"): lista pokazywala 7-10 / 1 000-1 500, a kupno zabieralo
    ///    tysiac razy wiecej. Zloto szlo w nicosc, ksiazka powstawala z niczego (PartyBelongedTo.ItemRoster.AddToCounts), z oferty ksiegarza nie schodzila.
    ///    Teraz: prefiks na lambdzie (szukana po tresci IL: jedyna metoda klas zagniezdzonych z ChangeHeroGold i AddToCounts) robi kupno sam i wylacza
    ///    lambde BK: cena = Value (ta z listy), GiveGoldAction gracz -> ksiegarz, ksiazka schodzi z jego oferty (BK jej nie uzupelnia - sprzedane, to
    ///    sprzedane) i trafia do sakw gracza. Pusta oferta: komunikat zamiast pustego okna wyboru.
    ///
    /// Stan tylko w pamieci (liczniki doby, pamiec "wartosc projektu sprzed przeliczenia" - slaba tablica, nie trzyma zlecen przy zyciu). Nic w zapisie:
    /// nagroda zlecenia to pole gry (BaseGoldReward) - zapisuje je gra. Reset z WorldMeasure.Reset (konstruktor ArmouryBehavior).
    /// Linie: start gry "Kuznia (E8/K): latki przy starcie gry", sesja "Kuznia (E8/K): sesja", doba "Kuznia (E8/K): dzien N", kupno "Kuznia (E8/K): ksiegarz".
    /// Wyjatek = licznik potkniec, bez globalnego wylacznika. Zegar realny tylko do pomiaru kosztu (Stopwatch).
    /// </summary>
    internal static class CraftOrders8
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        internal static bool PayOn { get { var s = Settings.Current; return s != null && s.CraftOrdersHistorical; } }
        internal static bool OpenOn { get { var s = Settings.Current; return s != null && s.CraftOrdersOpenedOnly; } }
        internal static bool BookOn { get { var s = Settings.Current; return s != null && s.BookPriceFix; } }

        private static int _stumbles, _stumblesDay;
        private static readonly HashSet<string> _errSites = new HashSet<string>();
        private static readonly List<string> _wired = new List<string>(), _missing = new List<string>();
        private static int _wiredN, _wantedN;
        private static long _ticks;

        private static void Stumble(string where, Exception e)
        {
            _stumbles++; _stumblesDay++;
            try { if (_errSites.Add(where)) Log.Error("CraftOrders8." + where, e); } catch { }
        }

        // ------------------------------------------------------------ dostep do gry (delegaty prywatnych metod, pole nagrody)

        private static Func<CraftingCampaignBehavior, CraftingTemplate, int, WeaponDesignElement[]> _getPieces;   // GetWeaponPieces(szablon, tier)
        private static Action<CraftingCampaignBehavior, int, Hero> _giveTroop;                                    // GiveTroopToNobleAtWeaponTier(tier, lord)
        private static Action<CraftingCampaignBehavior, Town, CraftingOrder> _replace;                            // ReplaceCraftingOrder(miasto, zlecenie)
        private static FieldInfo _fReward;                                                                        // CraftingOrder.BaseGoldReward (readonly)
        private static CraftingCampaignBehavior _beh;

        private static CraftingCampaignBehavior Beh()
        {
            var b = _beh;
            if (b != null) return b;
            var c = Campaign.Current;
            b = c != null ? c.GetCampaignBehavior<CraftingCampaignBehavior>() : null;
            _beh = b;
            return b;
        }

        private sealed class Box { public int Orig; }
        // zlecenie -> wartosc projektu sprzed przeliczenia (do doswiadczenia zlecenia); slaba tablica - zlecen przybywa kilkanascie na dobe
        private static ConditionalWeakTable<CraftingOrder, Box> _origOf = new ConditionalWeakTable<CraftingOrder, Box>();

        private static void Remember(CraftingOrder o, int orig)
        {
            Box b;
            if (_origOf.TryGetValue(o, out b)) b.Orig = orig; else _origOf.Add(o, new Box { Orig = orig });
        }

        private static bool SetReward(CraftingOrder o, int reward)
        {
            var f = _fReward;
            if (f == null) return false;
            f.SetValue(o, reward);
            return true;
        }

        private static int Scale(int reward, int now, int was)
        {
            if (reward <= 0 || was <= 0 || now <= 0) return reward;
            return (int)Math.Max(1L, (long)Math.Round((double)reward * now / was));
        }

        // ------------------------------------------------------------ 1a. nowe zlecenie: projekt i nagroda w nowej monecie

        private static int _nNew, _nNewPriced, _nNewEarly;
        private static long _sumGame, _sumHist;

        /// <summary>Postfiks konstruktora CraftingOrder(Hero, float, WeaponDesign, CraftingTemplate, int, string).</summary>
        public static void CtorPostfix(CraftingOrder __instance)
        {
            long t0 = Stopwatch.GetTimestamp();
            try
            {
                if (__instance == null) return;
                _nNew++;
                var it = __instance.PreCraftedWeaponDesignItem;
                if (it == null || ReferenceEquals(it, DefaultItems.Trash)) return;
                int g = __instance.BaseGoldReward;
                if (!PayOn || !HistoricalPrices.On) return;                    // klucz albo ceny historyczne wylaczone - zlecenie jak w grze
                if (!HistoricalPrices.Applied) { _nNewEarly++; return; }      // start nowej kampanii: ceny przeliczy sesja, zlecenie - SessionStart
                int was = HistoricalPrices.PriceDesign(it);
                if (was <= 0 || it.Value <= 0 || it.Value == was) return;      // nie umiemy wycenic - zlecenie jak w grze
                Remember(__instance, was);
                int h = Scale(g, it.Value, was);
                if (h != g && SetReward(__instance, h)) { _nNewPriced++; _sumGame += g; _sumHist += h; }
            }
            catch (Exception e) { Stumble("CtorPostfix", e); }
            finally { _ticks += Stopwatch.GetTimestamp() - t0; }
        }

        // ------------------------------------------------------------ 1b. doswiadczenie zlecenia od wartosci sprzed przeliczenia

        private static int _xpCalls, _xpScaled;

        /// <summary>Postfiks CraftingOrder.GetOrderExperience: gra liczy 0.25 x 1.3 x wartosc PROJEKTU - po przeliczeniu projektu od dawnej wartosci
        /// (ta sama regula co HistoricalPrices.XpPostfix dla wykutej sztuki).</summary>
        public static void OrderXpPostfix(CraftingOrder __instance, ref float __result)
        {
            try
            {
                _xpCalls++;
                Box b;
                var it = __instance != null ? __instance.PreCraftedWeaponDesignItem : null;
                if (it == null || it.Value <= 0 || !_origOf.TryGetValue(__instance, out b) || b.Orig <= 0 || b.Orig == it.Value) return;
                __result = __result * b.Orig / it.Value;
                _xpScaled++;
            }
            catch (Exception e) { Stumble("OrderXpPostfix", e); }
        }

        // ------------------------------------------------------------ 1c. oddanie zlecenia: platnik, bron, zolnierz

        public sealed class Done { public GiverPay.Ctx Ctx; public bool Custom; }

        private static int _cDone, _cCustom, _cShortN, _cTroopCut, _cTroopGiven, _cLordTook, _cTabor, _cShelf, _cLost;
        private static long _cDue, _cPaid, _cNothing;
        private static long _cHead; private static int _cHeadN, _cHeadFull;   // r4-zlecenia (Craft Order Clan Purse): doplata glowy rodu - kwota, zlecenia, w tym do pelna

        /// <summary>Prefiks CompleteOrder(Town, CraftingOrder, ItemObject, Hero): kontekst wyplaty (zamykany w finalizerze - takze po wyjatku).</summary>
        public static void CompletePrefix(CraftingCampaignBehavior __instance, Town __0, CraftingOrder __1, out Done __state)
        {
            __state = null;
            try
            {
                var d = new Done();
                try
                {
                    CraftingCampaignBehavior.CraftingOrderSlots slots;
                    var all = __instance != null ? __instance.CraftingOrders : null;
                    d.Custom = all != null && __0 != null && __1 != null && all.TryGetValue(__0, out slots) && slots != null && slots.CustomOrders != null && slots.CustomOrders.Contains(__1);
                }
                catch (Exception e) { Stumble("CompletePrefix(zadanie)", e); }
                d.Ctx = GiverPay.Open("zlecenia kowalskie", __1 != null ? __1.OrderOwner : null, PayOn);
                d.Ctx.ClanPurse = CraftOrderClanPurse.KeyOn;   // r4-zlecenia: brak lorda, ktory nie jest glowa rodu, doplaca glowa (tylko przy PayOn - kontekst z On)
                __state = d;
            }
            catch (Exception e) { Stumble("CompletePrefix", e); }
        }

        /// <summary>Postfiks CompleteOrder: wykuta bron ma odbiorce. Gra juz zdjela zlecenie i (dla lorda) sprobowala dac mu bron do reki.</summary>
        public static void CompletePostfix(CraftingCampaignBehavior __instance, Town __0, CraftingOrder __1, ItemObject __2, Done __state)
        {
            try
            {
                _cDone++;
                if (__state != null && __state.Custom) _cCustom++;
                var owner = __1 != null ? __1.OrderOwner : null;
                if (owner == null || __2 == null) { _cLost++; return; }
                var eq = owner.BattleEquipment;
                if (eq != null)
                    for (int i = 0; i < 12; i++)
                        if (!eq[i].IsEmpty && ReferenceEquals(eq[i].Item, __2)) { _cLordTook++; return; }   // gra: lord wzial bron do reki
                if (!PayOn) { _cLost++; return; }                                                           // cien: sztuka znika jak w grze
                var el = new EquipmentElement(__2, __instance != null ? __instance.GetCurrentItemModifier() : null);
                var mp = owner.IsLord ? owner.PartyBelongedTo : null;
                var tabor = mp != null ? mp.ItemRoster : null;
                if (tabor != null) { tabor.AddToCounts(el, 1); _cTabor++; return; }
                var shelf = __0 != null && __0.Owner != null ? __0.Owner.ItemRoster : null;
                if (shelf != null) { shelf.AddToCounts(el, 1); _cShelf++; return; }
                _cLost++;
            }
            catch (Exception e) { Stumble("CompletePostfix", e); }
        }

        public static void CompleteFinalizer(Done __state)
        {
            try
            {
                var c = __state != null ? __state.Ctx : null;
                if (c == null) return;
                GiverPay.Close(c);
                _cDue += c.Due; _cPaid += c.Paid; _cShortN += c.Short; _cNothing += c.FromNothing;
                _cHead += c.FromHead; _cHeadN += c.HeadN; _cHeadFull += c.HeadFull;
            }
            catch (Exception e) { Stumble("CompleteFinalizer", e); }
        }

        /// <summary>Zamiast GiveTroopToNobleAtWeaponTier(tier, lord) w CompleteOrder: gra dopisywala druzynie lorda zolnierza z niczego.</summary>
        public static void Troop(CraftingCampaignBehavior beh, int tier, Hero noble)
        {
            if (PayOn) { _cTroopCut++; return; }
            _cTroopGiven++;
            var d = _giveTroop;
            if (d != null) d(beh, tier, noble);   // klucz wylaczony - gra jak dotad
        }

        // ------------------------------------------------------------ 1d. szablon i czesci: tylko klasy, ktore gracz umie kuc

        private static readonly List<CraftingTemplate> _elig = new List<CraftingTemplate>(48);
        private static int _pickCalls, _pickFallback, _pickShadow, _pickShadowClosed, _tierCut;

        /// <summary>Kazdy slot szablonu ma czesc otwarta u gracza (dana z gory albo odkryta) i widoczna w projektancie - ta sama miara co spis
        /// "klasy zamkniete" bramy kucia (CrashScribe Mends.LoreForgeGate), tylko z IsOpened zamiast IsGivenByDefault.</summary>
        private static bool Eligible(CraftingCampaignBehavior beh, CraftingTemplate tpl)
        {
            if (tpl == null) return false;
            var bo = tpl.BuildOrders; var ps = tpl.Pieces;
            if (bo == null || ps == null || bo.Length == 0) return false;
            for (int i = 0; i < bo.Length; i++)
            {
                var slot = bo[i].PieceType;
                bool any = false;
                for (int j = 0; j < ps.Count && !any; j++)
                {
                    var p = ps[j];
                    any = p != null && p.PieceType == slot && !p.IsHiddenOnDesigner && beh.IsOpened(p, tpl);
                }
                if (!any) return false;
            }
            return true;
        }

        /// <summary>Najwyzszy tier czesci otwartej u gracza w tym szablonie (0 = zadnej).</summary>
        private static int TopOpen(CraftingCampaignBehavior beh, CraftingTemplate tpl)
        {
            int top = 0;
            var ps = tpl.Pieces;
            if (ps == null) return 0;
            for (int j = 0; j < ps.Count; j++)
            {
                var p = ps[j];
                if (p == null || p.IsHiddenOnDesigner || p.PieceTier <= top) continue;
                if (beh.IsOpened(p, tpl)) top = p.PieceTier;
            }
            return top;
        }

        private static void Fill(CraftingCampaignBehavior beh, MBReadOnlyList<CraftingTemplate> all)
        {
            _elig.Clear();
            for (int i = 0; i < all.Count; i++)
            {
                var t = all[i];
                try { if (Eligible(beh, t)) _elig.Add(t); }
                catch (Exception e) { Stumble("Eligible", e); }   // szablon bez wpisu w otwartych czesciach - nie jest dozwolony
            }
        }

        /// <summary>Zamiast CraftingTemplate.All.GetRandomElement() w CreateTownOrder i CreateCustomOrderForHero.</summary>
        public static CraftingTemplate PickTemplate(MBReadOnlyList<CraftingTemplate> all)
        {
            long t0 = Stopwatch.GetTimestamp();
            try
            {
                var beh = Beh();
                if (all != null && all.Count > 0 && beh != null)
                {
                    Fill(beh, all);
                    if (OpenOn)
                    {
                        _pickCalls++;
                        if (_elig.Count > 0) return _elig[MBRandom.RandomInt(_elig.Count)];
                        _pickFallback++;                                   // zadna klasa nie jest otwarta - los gry
                    }
                    else
                    {
                        var r = all.GetRandomElement();
                        _pickShadow++;
                        if (r != null && !_elig.Contains(r)) _pickShadowClosed++;
                        return r;
                    }
                }
            }
            catch (Exception e) { Stumble("PickTemplate", e); }
            finally { _ticks += Stopwatch.GetTimestamp() - t0; }
            return all.GetRandomElement();
        }

        /// <summary>Zamiast GetWeaponPieces(szablon, tier) w obu metodach: tier czesci najwyzej najwyzszy otwarty tier szablonu + 1.</summary>
        public static WeaponDesignElement[] Pieces(CraftingCampaignBehavior beh, CraftingTemplate tpl, int tier)
        {
            int t = tier;
            try
            {
                if (OpenOn && beh != null && tpl != null)
                {
                    int top = TopOpen(beh, tpl);
                    if (top > 0 && t > top + 1) { t = top + 1; _tierCut++; }
                }
            }
            catch (Exception e) { t = tier; Stumble("Pieces", e); }
            return _getPieces(beh, tpl, t);
        }

        // ------------------------------------------------------------ podmiany wywolan w kodzie gry (transpilery)

        private const int WComplete = 0, WTown = 1, WCustom = 2;             // metoda
        private const int KPay = 0, KTroop = 1, KTpl = 2, KPieces = 3;       // rodzaj podmiany
        private static readonly int[,] _hits = new int[3, 4];
        private static MethodInfo _mGive, _mPay, _mTroop, _mPick, _mPieces;

        private static int KindOf(CodeInstruction ci)
        {
            if (ci == null || ci.opcode != OpCodes.Call) return -1;
            var mi = ci.operand as MethodInfo;
            if (mi == null) return -1;
            if (_mGive != null && mi == _mGive) return KPay;
            if (mi.DeclaringType == typeof(CraftingCampaignBehavior))
            {
                if (mi.Name == "GiveTroopToNobleAtWeaponTier") return KTroop;
                if (mi.Name == "GetWeaponPieces") return KPieces;
                return -1;
            }
            if (mi.Name == "GetRandomElement" && mi.IsGenericMethod)
            {
                var ga = mi.GetGenericArguments(); var pa = mi.GetParameters();
                if (ga.Length == 1 && ga[0] == typeof(CraftingTemplate) && pa.Length == 1 && pa[0].ParameterType == typeof(MBReadOnlyList<CraftingTemplate>)) return KTpl;
            }
            return -1;
        }

        /// <summary>Podmienia wywolanie tylko wtedy, gdy w metodzie jest DOKLADNIE jedno takie (inaczej gra zmienila tresc - zostaje jak jest, linia startowa
        /// pokaze BRAK). Etykiety instrukcji zostaja (zmieniamy tylko operand).</summary>
        private static IEnumerable<CodeInstruction> Swap(IEnumerable<CodeInstruction> instructions, int where, bool pay, bool troop, bool tpl, bool pieces)
        {
            var list = new List<CodeInstruction>(instructions);
            try
            {
                var cnt = new int[4];
                foreach (var ci in list) { int k = KindOf(ci); if (k >= 0) cnt[k]++; }
                for (int k = 0; k < 4; k++) _hits[where, k] = cnt[k];
                var use = new[] { pay && _mPay != null, troop && _mTroop != null && _giveTroop != null, tpl && _mPick != null, pieces && _mPieces != null && _getPieces != null };
                var to = new[] { _mPay, _mTroop, _mPick, _mPieces };
                foreach (var ci in list)
                {
                    int k = KindOf(ci);
                    if (k < 0 || !use[k] || cnt[k] != 1) continue;
                    ci.operand = to[k];
                }
            }
            catch (Exception e) { Stumble("Swap", e); }
            return list;
        }

        public static IEnumerable<CodeInstruction> CompleteIl(IEnumerable<CodeInstruction> instructions) { return Swap(instructions, WComplete, true, true, false, false); }
        public static IEnumerable<CodeInstruction> TownIl(IEnumerable<CodeInstruction> instructions) { return Swap(instructions, WTown, false, false, true, true); }
        public static IEnumerable<CodeInstruction> CustomIl(IEnumerable<CodeInstruction> instructions) { return Swap(instructions, WCustom, false, false, true, true); }

        // ------------------------------------------------------------ 2. ksiegarz BK

        private static object _bkEdu;                                        // BKEducationBehavior tej kampanii (z prefiksu rozmowy)
        private static MethodInfo _mSeller, _mRoster;                        // GetBookSeller(Settlement), GetBookRoster(Settlement)
        private static PropertyInfo _pBookItem;                              // BookType.Item
        private static int _bOpen, _bEmpty, _bSold, _bPoor, _bGone, _bOld, _bFree;
        private static long _bGold, _bVoid;

        private static Hero SellerAt(Settlement st)
        {
            var b = _bkEdu; var m = _mSeller;
            return b != null && m != null && st != null ? m.Invoke(b, new object[] { st }) as Hero : null;
        }

        private static ItemRoster RosterAt(Settlement st)
        {
            var b = _bkEdu; var m = _mRoster;
            return b != null && m != null && st != null ? m.Invoke(b, new object[] { st }) as ItemRoster : null;
        }

        private static ItemObject BookItem(object bookType)
        {
            if (bookType == null) return null;
            var p = _pBookItem;
            if (p == null || !p.DeclaringType.IsInstanceOfType(bookType)) { p = bookType.GetType().GetProperty("Item", BindingFlags.Public | BindingFlags.Instance); _pBookItem = p; }
            return p != null ? p.GetValue(bookType, null) as ItemObject : null;
        }

        private static string NameOf(Hero h, string fallback) { return h != null && h.Name != null ? h.Name.ToString() : fallback; }

        /// <summary>Prefiks BKEducationBehavior.OnBuyBookConsequence ("I would like to buy a book"): pamieta zachowanie BK; pusta oferta - komunikat
        /// zamiast pustego okna wyboru (oferta moze byc pusta dopiero od tej paczki - ksiazka schodzi z oferty).</summary>
        public static bool BookOpenPrefix(object __instance)
        {
            try
            {
                _bkEdu = __instance; _bOpen++;
                if (!BookOn) return true;
                var st = Hero.MainHero != null ? Hero.MainHero.CurrentSettlement : null;
                var roster = RosterAt(st);
                if (roster != null && roster.Count == 0)
                {
                    _bEmpty++;
                    Log.Player(NameOf(SellerAt(st), "The bookseller") + " has no books left to sell.");
                    return false;
                }
            }
            catch (Exception e) { Stumble("BookOpenPrefix", e); }
            return true;
        }

        /// <summary>Prefiks lambdy kupna (wybor z listy): kupno robimy sami - cena z listy, zloto do ksiegarza, ksiazka z jego oferty. __state = zloto gracza
        /// przed lambda BK, gdy biegnie stara droga (klucz wylaczony albo brak danych) - finalizer liczy wtedy zloto w nicosc i ksiazke z niczego.</summary>
        public static bool BookBuyPrefix(List<InquiryElement> __0, out int __state)
        {
            __state = int.MinValue;
            Hero buyer = null, seller = null; ItemObject item = null; ItemRoster roster = null; MobileParty party = null;
            try
            {
                buyer = Hero.MainHero;
                if (buyer == null) return true;
                if (BookOn)
                {
                    var el = __0 != null && __0.Count > 0 ? __0[0] : null;
                    item = BookItem(el != null ? el.Identifier : null);
                    party = buyer.PartyBelongedTo;
                    var st = buyer.CurrentSettlement;
                    seller = SellerAt(st); roster = RosterAt(st);
                }
                if (item == null || party == null || party.ItemRoster == null || seller == null || roster == null)
                {
                    if (BookOn) _bOld++;                                   // brak danych - stara droga BK (prog 0)
                    __state = buyer.Gold;
                    return true;
                }
            }
            catch (Exception e) { Stumble("BookBuyPrefix(odczyt)", e); if (buyer != null) __state = buyer.Gold; return true; }
            try
            {
                if (roster.GetItemNumber(item) <= 0) { _bGone++; Log.Player(NameOf(seller, "The bookseller") + " no longer has " + item.Name + ".", true); return false; }
                int price = Math.Max(0, item.Value);
                if (buyer.Gold < price) { _bPoor++; Log.Player("You cannot afford " + item.Name + " (" + price + ").", true); return false; }
                roster.AddToCounts(item, -1);
                party.ItemRoster.AddToCounts(item, 1);
                bool pn = buyer == Hero.MainHero && PayNotes.On;   // m96-wies: graczowi jeden komunikat z powodem zamiast "You paid N" gry
                if (price > 0) GiveGoldAction.ApplyBetweenCharacters(buyer, seller, price, pn);
                if (pn) PayNotes.Paid("book", price, " for " + item.Name + " (" + NameOf(seller, "the bookseller") + ").");
                _bSold++; _bGold += price;
                Log.Info("Kuznia (E8/K): ksiegarz - " + NameOf(seller, "?") + " (" + (buyer.CurrentSettlement != null ? buyer.CurrentSettlement.StringId : "?") + ") sprzedal " + item.StringId
                         + " za " + price + "; kiesa ksiegarza " + seller.Gold + ", zostalo w ofercie " + roster.Count + " ksiazek.");
            }
            catch (Exception e) { Stumble("BookBuyPrefix(kupno)", e); }
            return false;                                                  // kupno (albo odmowa) zrobione u nas - lambda BK nie biegnie
        }

        public static void BookBuyFinalizer(int __state)
        {
            if (__state == int.MinValue) return;
            try
            {
                var b = Hero.MainHero;
                if (b == null) return;
                long lost = (long)__state - b.Gold;
                if (lost > 0) _bVoid += lost;
                _bFree++;
            }
            catch (Exception e) { Stumble("BookBuyFinalizer", e); }
        }

        // ------------------------------------------------------------ start sesji: zlecenia sprzed sesji (zapis albo start nowej kampanii)

        private static int _sOrders, _sCustom, _sPriced, _sNoPrice, _sConv, _sKept, _sHigh;
        private static long _sBefore, _sAfter;
        private static bool _firstDone;
        private static int _nkReplaced = -1;                                 // -1 = wymiana startowa nie biegla w tej sesji

        private static void Sweep(CraftingOrder o, bool custom, bool on)
        {
            if (o == null) return;
            _sOrders++; if (custom) _sCustom++;
            var it = o.PreCraftedWeaponDesignItem;
            if (it == null || ReferenceEquals(it, DefaultItems.Trash) || !on) return;
            int was = HistoricalPrices.PriceDesign(it);
            if (was <= 0 || it.Value <= 0 || it.Value == was) { _sNoPrice++; return; }
            Remember(o, was);
            _sPriced++;
            int max = Campaign.Current.Models.TradeItemPriceFactorModel.GetTheoreticalMaxItemMarketValue(it);
            int g = o.BaseGoldReward;
            if ((long)g > 3L * Math.Max(1, max))
            {
                int h = Scale(g, it.Value, was);
                if (h != g && SetReward(o, h)) { _sConv++; _sBefore += g; _sAfter += h; }
            }
            else _sKept++;
            if ((long)o.BaseGoldReward > 3L * Math.Max(1, max)) _sHigh++;
        }

        /// <summary>Z ArmouryBehavior.OnSessionLaunched, PO HistoricalPrices.Apply: zlecenia z zapisu i zlecenia startowe nowej kampanii (powstaly przed
        /// przeliczeniem cen). Projekt wyceniony, nagroda przeliczona tylko wtedy, gdy jest wieksza niz 3 x nowa - drugie wczytanie niczego nie zmienia.</summary>
        internal static void SessionStart()
        {
            long t0 = Stopwatch.GetTimestamp();
            _sOrders = _sCustom = _sPriced = _sNoPrice = _sConv = _sKept = _sHigh = 0; _sBefore = _sAfter = 0;
            bool on = PayOn;
            try
            {
                var beh = Beh();
                var all = beh != null ? beh.CraftingOrders : null;
                if (all != null)
                    foreach (var kv in all)
                    {
                        var slots = kv.Value;
                        if (slots == null) continue;
                        if (slots.Slots != null)
                            foreach (var o in slots.Slots)
                                try { Sweep(o, false, on); } catch (Exception e) { Stumble("SessionStart(zlecenie)", e); }
                        if (slots.CustomOrders != null)
                            foreach (var o in slots.CustomOrders)
                                try { Sweep(o, true, on); } catch (Exception e) { Stumble("SessionStart(zadanie)", e); }
                    }
            }
            catch (Exception e) { Stumble("SessionStart", e); }
            Log.Info("Kuznia (E8/K): sesja - zlecen kowalskich w miastach " + _sOrders + " (w tym zlecen zadan " + _sCustom + ")"
                     + " | Craft Orders Historical " + (on ? "CZYNNE" : "WYLACZONE") + ": projekt w cenach historycznych " + _sPriced + ", bez wyceny " + _sNoPrice
                     + ", nagroda z zapisu przeliczona " + _sConv + " (suma " + _sBefore + " -> " + _sAfter + ")"
                     + ", bez zmiany (juz w nowej monecie albo najwyzej 3 x nowa) " + _sKept + ", po przeliczeniu ponad 3 x nowa " + _sHigh
                     + " | zlecenia zalozone w tej sesji przed przeliczeniem cen (start nowej kampanii) " + _nNewEarly
                     + " | Craft Orders Opened Only " + (OpenOn ? "CZYNNE" : "WYLACZONE") + ", Book Price Fix " + (BookOn ? "CZYNNE" : "WYLACZONE")
                     + " | koszt " + ((Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency).ToString("0.00", Inv) + " ms, potkniecia " + _stumbles + ".");
        }

        /// <summary>Nowa kampania, pierwszy takt doby sesji (wiek kampanii najwyzej 1.5 doby): zlecenia startowe powstaly przed brama kucia (darmowe
        /// czesci jeszcze wedlug plikow gry) - wymieniamy je raz metoda gry ReplaceCraftingOrder; nowe powstaja juz z dozwolonych klas.</summary>
        private static void FirstDay()
        {
            if (_firstDone) return;
            _firstDone = true;
            if (!OpenOn || _replace == null) return;
            double age;
            try { age = (CampaignTime.Now - Campaign.Current.Models.CampaignTimeModel.CampaignStartTime).ToDays; } catch { return; }
            if (age < 0.0 || age > 1.5) return;
            var beh = Beh();
            var all = beh != null ? beh.CraftingOrders : null;
            if (all == null) return;
            var work = new List<KeyValuePair<Town, CraftingOrder>>();
            foreach (var kv in all)
            {
                if (kv.Key == null || kv.Value == null || kv.Value.Slots == null) continue;
                foreach (var o in kv.Value.Slots) if (o != null) work.Add(new KeyValuePair<Town, CraftingOrder>(kv.Key, o));
            }
            _nkReplaced = 0;
            foreach (var w in work)
                try { _replace(beh, w.Key, w.Value); _nkReplaced++; }
                catch (Exception e) { Stumble("FirstDay(wymiana)", e); }
        }

        // ------------------------------------------------------------ stan

        /// <summary>Nowa kampania i kazde wczytanie (WorldMeasure.Reset - konstruktor ArmouryBehavior).</summary>
        internal static void Reset()
        {
            _stumbles = 0; _errSites.Clear();
            _beh = null; _bkEdu = null; _elig.Clear();
            _origOf = new ConditionalWeakTable<CraftingOrder, Box>();
            _firstDone = false; _nkReplaced = -1; _nNewEarly = 0;
            _sOrders = _sCustom = _sPriced = _sNoPrice = _sConv = _sKept = _sHigh = 0; _sBefore = _sAfter = 0;
            GiverPay.Reset();
            CraftOrdersPurse.Reset();   // NZ (16.10): zlecenia od zleceniodawcy z pieniedzmi - liczniki (ta sama chwila)
            CraftOrderClanPurse.Reset();   // r4-zlecenia: potkniecia doplaty glowy rodu
            ClearDay();
        }

        private static void ClearDay()
        {
            _stumblesDay = 0; _ticks = 0;
            _nNew = _nNewPriced = 0; _sumGame = _sumHist = 0;
            _xpCalls = _xpScaled = 0;
            _cDone = _cCustom = _cShortN = _cTroopCut = _cTroopGiven = _cLordTook = _cTabor = _cShelf = _cLost = 0;
            _cDue = _cPaid = _cNothing = 0;
            _cHead = 0; _cHeadN = _cHeadFull = 0;
            _pickCalls = _pickFallback = _pickShadow = _pickShadowClosed = _tierCut = 0;
            _bOpen = _bEmpty = _bSold = _bPoor = _bGone = _bOld = _bFree = 0; _bGold = _bVoid = 0;
        }

        // ------------------------------------------------------------ takt doby

        /// <summary>Raz na dobe (ArmouryBehavior.OnDailyTick, po MercFeast.Daily): wymiana zlecen startowych nowej kampanii (raz) i linia "Kuznia (E8/K)".</summary>
        internal static void Daily()
        {
            var s = Settings.Current;
            if (s == null || Campaign.Current == null) return;
            long t0 = Stopwatch.GetTimestamp();
            int day = (int)CampaignTime.Now.ToDays - 1;
            int newBefore = _nNew;
            try { FirstDay(); } catch (Exception e) { Stumble("FirstDay", e); }
            try
            {
                int total = 0, open = 0; string closed = "";
                try
                {
                    var beh = Beh(); var all = CraftingTemplate.All;
                    if (beh != null && all != null)
                    {
                        Fill(beh, all);
                        total = all.Count; open = _elig.Count;
                        if (open < total)
                        {
                            var names = new List<string>();
                            for (int i = 0; i < all.Count; i++) if (all[i] != null && !_elig.Contains(all[i])) names.Add(all[i].StringId ?? "?");
                            names.Sort(string.CompareOrdinal);
                            closed = " [zamkniete: " + string.Join(", ", names.GetRange(0, Math.Min(12, names.Count)).ToArray()) + (names.Count > 12 ? ", ..." : "") + "]";
                        }
                    }
                }
                catch (Exception e) { Stumble("Daily(klasy)", e); }
                var sb = new StringBuilder(1400);
                sb.Append("Kuznia (E8/K): dzien ").Append(day)
                  .Append(" | zlecenia kowalskie (Craft Orders Historical ").Append(s.CraftOrdersHistorical ? "CZYNNE" : "WYLACZONE")
                  .Append(", Craft Orders Opened Only ").Append(s.CraftOrdersOpenedOnly ? "CZYNNE" : "WYLACZONE").Append("): nowych ").Append(_nNew)
                  .Append(" (nagroda przeliczona w ").Append(_nNewPriced).Append(": gra ").Append(_sumGame).Append(" -> historyczna ").Append(_sumHist).Append(')')
                  .Append(", klasy broni dozwolone ").Append(open).Append(" z ").Append(total).Append(closed)
                  .Append(", szablon z dozwolonych klas ").Append(_pickCalls).Append(" razy, brak dozwolonej klasy - los gry ").Append(_pickFallback)
                  .Append(", tier czesci przyciety ").Append(_tierCut).Append(" razy")
                  .Append(", w cieniu (klucz wylaczony): los gry ").Append(_pickShadow).Append(" razy, z klasy zamknietej ").Append(_pickShadowClosed)
                  .Append(", zlecenia startowe nowej kampanii wymienione ").Append(_nkReplaced < 0 ? "-" : _nkReplaced.ToString(Inv))
                  .Append(_nkReplaced >= 0 ? " (nowych w wymianie " + (_nNew - newBefore) + ")" : "");
                if (_nkReplaced >= 0) _nkReplaced = -1;   // raz w linii
                sb.Append(" | okno \"zlecenia kowalskie\": oddanych ").Append(_cDone).Append(" (w tym zlecen zadan ").Append(_cCustom).Append(')')
                  .Append(", obiecane ").Append(_cDue).Append(", zaplacone przez zleceniodawce ").Append(_cPaid)
                  .Append(", doplata glowy rodu (Craft Order Clan Purse ").Append(s.CraftOrderClanPurse ? "CZYNNE" : "WYLACZONE").Append(") ").Append(_cHead)
                  .Append(" w ").Append(_cHeadN).Append(" zleceniach (do pelna ").Append(_cHeadFull).Append(", potkniecia ").Append(CraftOrderClanPurse.Stumbles).Append(')')
                  .Append(", niedoplata przy pustej kiesie ").Append(_cDue - _cPaid - _cHead).Append(" w ").Append(_cShortN).Append(" zleceniach")
                  .Append(", zloto z niczego ").Append(_cNothing)
                  .Append(", zolnierz z niczego: wyciety ").Append(_cTroopCut).Append(", dany ").Append(_cTroopGiven)
                  .Append(", bron: lord wzial do reki ").Append(_cLordTook).Append(", do taboru lorda ").Append(_cTabor).Append(", na polke miasta ").Append(_cShelf)
                  .Append(", bron w nicosc ").Append(_cLost)
                  .Append(" | doswiadczenie zlecen: odczytow ").Append(_xpCalls).Append(", od wartosci sprzed przeliczenia ").Append(_xpScaled)
                  .Append(" | okno \"ksiegarz\" (Book Price Fix ").Append(s.BookPriceFix ? "CZYNNE" : "WYLACZONE").Append("): rozmow o kupnie ").Append(_bOpen)
                  .Append(", pusta oferta ").Append(_bEmpty).Append(", sprzedanych ").Append(_bSold).Append(", zaplacone ksiegarzowi ").Append(_bGold)
                  .Append(", odmowy: brak zlota ").Append(_bPoor).Append(", brak ksiazki ").Append(_bGone)
                  .Append(", stara droga BK mimo klucza ").Append(_bOld).Append(", zloto w nicosc ").Append(_bVoid).Append(", ksiazka z niczego ").Append(_bFree)
                  .Append(" | latki wpiete ").Append(_wiredN).Append('/').Append(_wantedN)
                  .Append(_missing.Count > 0 ? " (BRAK: " + string.Join(", ", _missing.ToArray()) + ")" : "")
                  .Append(" | koszt ").Append(((_ticks + Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency).ToString("0.00", Inv)).Append(" ms/dobe")
                  .Append(" | potkniecia dzis ").Append(_stumblesDay).Append(" (od wczytania ").Append(_stumbles).Append(").");
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Stumble("Daily", e); }
            finally { ClearDay(); }
        }

        // ------------------------------------------------------------ start gry (SubModuleMain - po MercFeast)

        private static void Want(string label, bool ok)
        {
            _wantedN++;
            if (ok) { _wired.Add(label); _wiredN++; } else _missing.Add(label);
        }

        private static T Bind<T>(MethodInfo m) where T : class
        {
            try { return m != null ? Delegate.CreateDelegate(typeof(T), m) as T : null; } catch { return null; }
        }

        internal static void ApplyAll(Harmony h)
        {
            _wired.Clear(); _missing.Clear(); _wiredN = 0; _wantedN = 0;
            Array.Clear(_hits, 0, _hits.Length);
            string book = "bez Banner Kings - nic do wpiecia";
            try
            {
                var tBeh = typeof(CraftingCampaignBehavior); var me = typeof(CraftOrders8);
                _fReward = AccessTools.Field(typeof(CraftingOrder), "BaseGoldReward");
                _getPieces = Bind<Func<CraftingCampaignBehavior, CraftingTemplate, int, WeaponDesignElement[]>>(AccessTools.Method(tBeh, "GetWeaponPieces", new[] { typeof(CraftingTemplate), typeof(int) }));
                _giveTroop = Bind<Action<CraftingCampaignBehavior, int, Hero>>(AccessTools.Method(tBeh, "GiveTroopToNobleAtWeaponTier", new[] { typeof(int), typeof(Hero) }));
                _replace = Bind<Action<CraftingCampaignBehavior, Town, CraftingOrder>>(AccessTools.Method(tBeh, "ReplaceCraftingOrder", new[] { typeof(Town), typeof(CraftingOrder) }));
                _mGive = AccessTools.Method(typeof(GiveGoldAction), "ApplyBetweenCharacters", new[] { typeof(Hero), typeof(Hero), typeof(int), typeof(bool) });
                _mPay = AccessTools.Method(typeof(GiverPay), nameof(GiverPay.PayFromGiver));
                _mTroop = AccessTools.Method(me, nameof(Troop));
                _mPick = AccessTools.Method(me, nameof(PickTemplate));
                _mPieces = AccessTools.Method(me, nameof(Pieces));

                // 1a. projekt i nagroda
                bool ok = false;
                try
                {
                    var ctor = AccessTools.Constructor(typeof(CraftingOrder), new[] { typeof(Hero), typeof(float), typeof(WeaponDesign), typeof(CraftingTemplate), typeof(int), typeof(string) });
                    if (ctor != null && _fReward != null) { h.Patch(ctor, postfix: new HarmonyMethod(me, nameof(CtorPostfix))); ok = true; }
                }
                catch (Exception e) { Log.Error("CraftOrders8.ApplyAll (konstruktor CraftingOrder)", e); }
                Want("projekt i nagroda zlecenia w nowej monecie (postfiks konstruktora CraftingOrder, pole BaseGoldReward)", ok);
                // 1b. doswiadczenie zlecenia
                ok = false;
                try
                {
                    var xp = AccessTools.Method(typeof(CraftingOrder), "GetOrderExperience", new[] { typeof(ItemObject), typeof(ItemModifier) });
                    if (xp != null && xp.ReturnType == typeof(float)) { h.Patch(xp, postfix: new HarmonyMethod(me, nameof(OrderXpPostfix))); ok = true; }
                }
                catch (Exception e) { Log.Error("CraftOrders8.ApplyAll (GetOrderExperience)", e); }
                Want("doswiadczenie zlecenia od wartosci sprzed przeliczenia (postfiks CraftingOrder.GetOrderExperience)", ok);
                // 1c. oddanie zlecenia
                ok = false;
                try
                {
                    var done = AccessTools.Method(tBeh, "CompleteOrder", new[] { typeof(Town), typeof(CraftingOrder), typeof(ItemObject), typeof(Hero) });
                    if (done != null)
                    {
                        h.Patch(done, prefix: new HarmonyMethod(me, nameof(CompletePrefix)), postfix: new HarmonyMethod(me, nameof(CompletePostfix)),
                                transpiler: new HarmonyMethod(me, nameof(CompleteIl)), finalizer: new HarmonyMethod(me, nameof(CompleteFinalizer)));
                        ok = true;
                    }
                }
                catch (Exception e) { Log.Error("CraftOrders8.ApplyAll (CompleteOrder)", e); }
                Want("oddanie zlecenia - odbiorca broni (prefiks, postfiks i finalizer CompleteOrder)", ok);
                Want("wyplata od zleceniodawcy (GiveGoldAction -> GiverPay.PayFromGiver w CompleteOrder)", ok && _mPay != null && _hits[WComplete, KPay] == 1);
                Want("zolnierz z niczego (GiveTroopToNobleAtWeaponTier w CompleteOrder)", ok && _giveTroop != null && _hits[WComplete, KTroop] == 1);
                // 1d. szablon i czesci
                ok = false;
                try
                {
                    var town = AccessTools.Method(tBeh, "CreateTownOrder", new[] { typeof(Hero), typeof(int) });
                    if (town != null) { h.Patch(town, transpiler: new HarmonyMethod(me, nameof(TownIl))); ok = true; }
                }
                catch (Exception e) { Log.Error("CraftOrders8.ApplyAll (CreateTownOrder)", e); }
                Want("szablon z dozwolonych klas (CreateTownOrder)", ok && _hits[WTown, KTpl] == 1);
                Want("tier czesci do progu (CreateTownOrder)", ok && _getPieces != null && _hits[WTown, KPieces] == 1);
                ok = false;
                try
                {
                    var cust = AccessTools.Method(tBeh, "CreateCustomOrderForHero", new[] { typeof(Hero), typeof(float), typeof(WeaponDesign), typeof(CraftingTemplate) });
                    if (cust != null) { h.Patch(cust, transpiler: new HarmonyMethod(me, nameof(CustomIl))); ok = true; }
                }
                catch (Exception e) { Log.Error("CraftOrders8.ApplyAll (CreateCustomOrderForHero)", e); }
                Want("szablon z dozwolonych klas (CreateCustomOrderForHero)", ok && _hits[WCustom, KTpl] == 1);
                Want("tier czesci do progu (CreateCustomOrderForHero)", ok && _getPieces != null && _hits[WCustom, KPieces] == 1);
                Want("wymiana zlecen startowych nowej kampanii (ReplaceCraftingOrder)", _replace != null);

                // 2. ksiegarz BK
                var bt = QuartermasterLaw.FindType("BannerKings.Behaviours.BKEducationBehavior");
                if (bt != null)
                {
                    const BindingFlags any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
                    _mSeller = AccessTools.Method(bt, "GetBookSeller", new[] { typeof(Settlement) });
                    _mRoster = AccessTools.Method(bt, "GetBookRoster", new[] { typeof(Settlement) });
                    if (_mSeller != null && (_mSeller.IsStatic || !typeof(Hero).IsAssignableFrom(_mSeller.ReturnType))) _mSeller = null;
                    if (_mRoster != null && (_mRoster.IsStatic || !typeof(ItemRoster).IsAssignableFrom(_mRoster.ReturnType))) _mRoster = null;
                    ok = false;
                    try
                    {
                        var open = AccessTools.Method(bt, "OnBuyBookConsequence", Type.EmptyTypes);
                        if (open != null && !open.IsStatic && _mSeller != null && _mRoster != null) { h.Patch(open, prefix: new HarmonyMethod(me, nameof(BookOpenPrefix))); ok = true; }
                    }
                    catch (Exception e) { Log.Error("CraftOrders8.ApplyAll (OnBuyBookConsequence)", e); }
                    Want("ksiegarz BK: rozmowa o kupnie (prefiks BKEducationBehavior.OnBuyBookConsequence, GetBookSeller, GetBookRoster)", ok);
                    // lambda kupna: jedyna metoda klas zagniezdzonych z jednym parametrem List<InquiryElement>, ktora wola Hero.ChangeHeroGold i ItemRoster.AddToCounts
                    bool opened = ok; ok = false;
                    int found = 0; MethodInfo lam = null;
                    try
                    {
                        var mGold = AccessTools.Method(typeof(Hero), "ChangeHeroGold", new[] { typeof(int) });
                        var mAdd = AccessTools.Method(typeof(ItemRoster), "AddToCounts", new[] { typeof(ItemObject), typeof(int) });
                        foreach (var ty in bt.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic))
                            foreach (var mm in ty.GetMethods(any))
                            {
                                try
                                {
                                    if (mm.IsAbstract || mm.ContainsGenericParameters || mm.GetMethodBody() == null || mm.ReturnType != typeof(void)) continue;
                                    var pa = mm.GetParameters();
                                    if (pa.Length != 1 || pa[0].ParameterType != typeof(List<InquiryElement>)) continue;
                                    bool gold = false, add = false;
                                    foreach (var kv in PatchProcessor.ReadMethodBody(mm))
                                    {
                                        var mi = kv.Value as MethodInfo;
                                        if (mi == null) continue;
                                        if (mGold != null && mi == mGold) gold = true; else if (mAdd != null && mi == mAdd) add = true;
                                    }
                                    if (gold && add) { found++; lam = mm; }
                                }
                                catch { }
                            }
                        if (found == 1 && lam != null && opened)
                        {
                            h.Patch(lam, prefix: new HarmonyMethod(me, nameof(BookBuyPrefix)) { priority = Priority.First }, finalizer: new HarmonyMethod(me, nameof(BookBuyFinalizer)));
                            ok = true;
                        }
                    }
                    catch (Exception e) { Log.Error("CraftOrders8.ApplyAll (lambda kupna ksiazki)", e); }
                    Want("ksiegarz BK: kupno (prefiks i finalizer lambdy OnBuyBookConsequence - szukana po tresci IL)", ok);
                    book = "lambda kupna " + (lam != null ? lam.DeclaringType.Name + "." + lam.Name : "-") + " (pasujacych " + found + ")";
                }
            }
            catch (Exception e) { Log.Error("CraftOrders8.ApplyAll", e); }
            Log.Info("Kuznia (E8/K): latki przy starcie gry - wpiete " + _wiredN + "/" + _wantedN + ": " + (_wired.Count > 0 ? string.Join(", ", _wired.ToArray()) : "-")
                     + "; BRAK: " + (_missing.Count > 0 ? string.Join(", ", _missing.ToArray()) : "-")
                     + " | wywolania w kodzie gry (znalezione, oczekiwane 1): CompleteOrder wyplata " + _hits[WComplete, KPay] + ", zolnierz " + _hits[WComplete, KTroop]
                     + "; CreateTownOrder szablon " + _hits[WTown, KTpl] + ", czesci " + _hits[WTown, KPieces]
                     + "; CreateCustomOrderForHero szablon " + _hits[WCustom, KTpl] + ", czesci " + _hits[WCustom, KPieces]
                     + " | ksiegarz BK: " + book
                     + " | Craft Orders Historical " + (PayOn ? "CZYNNE" : "wylaczone") + " (nagroda w nowej monecie, placi zleceniodawca, bron ma odbiorce, lord bez zolnierza z niczego)"
                     + ", Craft Orders Opened Only " + (OpenOn ? "CZYNNE" : "wylaczone") + " (zlecenia tylko z klas z otwarta czescia w kazdym slocie)"
                     + ", Book Price Fix " + (BookOn ? "CZYNNE" : "wylaczone") + " (ksiazka BK za cene z listy, zloto do ksiegarza, ksiazka z jego oferty).");
        }
    }
}
