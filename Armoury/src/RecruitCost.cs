using System;
using System.Collections.Generic;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// CENA WERBUNKU HISTORYCZNIE (Jeff 04.10: "przelicz te pensje i to wszystko tak, aby mialo sens historyczny").
    /// Zold juz jest historyczny (t1 2 d ... t6 21 d dziennie wobec piechura 2 d, rycerza 24 d), ale cena werbunku
    /// byla w skali gry: wedle poziomu jednostki 10/20/50/100/200/400/600/1000/1500 (DefaultPartyWageModel:216) -
    /// tier 6 za 600 = prawie miesiac zoldu, przy broni i zbroi juz w pensach.
    /// Historycznie przy zaciagu placono zaliczke/"prest" - kilka do kilkunastu dni zoldu.
    /// Teraz: cena = dzienny zold jednostki (z modelu, z mnoznikiem BK) x `RecruitCostDays` (10), najemnicy x2;
    /// doplata za konia jak w grze (150/500, poza "bez sprzetu"); doplaty z praw BK zostaja proporcjonalnie.
    /// Postfix na kazdym modelu PartyWageModel (licznik zagniezdzenia - liczymy raz, na zewnatrz).
    ///
    /// KON PO CENIE TARGU (paczka 143, Jeff 07.10; HorsesAtMarketPrice): doplata za konia nie jest juz stala gry 150/500, tylko
    /// cena konia tej jednostki (miejsce Horse jej sprzetu) na targu osady, w ktorej sie werbuje (Stables.MarketPrice - ten sam
    /// targ, na ktorym notabl kupil konia ochotnikowi, VolunteerKit). Miejsce werbunku: AI - osada z latki CheckRecruiting
    /// (takze przy wjezdzie do osady, zanim druzyna w niej stanie) i TickAutoRecruitmentGarrisonChange (garnizon - jego miasto
    /// albo zamek); poza nimi gracz - Settlement.CurrentSettlement, AI - osada, w ktorej stoi kupujacy. Brak osady albo konia
    /// (okup, koszt awansu "bez sprzetu") - stala gry jak dotad. Zloto: AI placi (gra), LevyGold oddaje te sama kwote notablowi
    /// (liczy ja w tej samej chwili i w tym samym miejscu), gracz placi notablowi przez BK - nic sie nie liczy dwa razy.
    ///
    /// KON NAJEMNIKA Z KARCZMY (poprawka 157, wylacznik MercHorseFromShelf; autotest stosu lawy 07.10 14:18: zloto AI za najemnikow
    /// do kas miast +55-64%, 479 zamiast ok. 290 na najemnika): kon najemnika bierze sie z wzorca (DTE / komplet rekruta - z niczego),
    /// a miasto dostawalo zaplate po cenie targu i konia nie oddawalo. Teraz doplata za konia najemnika (Mercenary, Gangster,
    /// CaravanGuard) jest tylko wtedy, gdy na polce targu tego miasta stoi kon tej samej rasy (ten sam przedmiot, a gdy go nie ma -
    /// ta sama kategoria, tier i rodzaj zwierzecia): przy werbunku ten kon schodzi z polki (to on jest koniem najemnika), a miasto
    /// dostaje to, co kupujacy za niego zaplacil (cena polki x mnoznik kupujacego z modelu gry - perki, kultura, prawa i ranga klanu BK;
    /// poprawka po recenzji 157, HorseShareOfCost). Gdy polka takiego konia nie ma - najemnik przychodzi z wlasnym koniem i placi sie tylko dni zoldu.
    /// "Wlasny kon" dalej bierze sie z niczego (jak caly komplet najemnika) - do kroku "weterani" (skad najemnik ma sprzet i konia:
    /// z puli zolnierzy rozpuszczonych z armii, z lupu, z wlasnego kupna). Gdy najemnikow jest wiecej niz koni na polce, nadplata za
    /// brakujace konie wraca do kiesy (lord, karawana, gracz). Linia dnia "Konie rekrutow (157)" zamiast 8 probek wyceny.
    /// </summary>
    internal static class RecruitCost
    {
        [ThreadStatic] private static int _depth;
        [ThreadStatic] private static Settlement _where;     // paczka 143: osada werbunku AI (latki miejsca), null = poza nimi
        private static readonly TextObject _txt = new TextObject("{=!}Prest money (days of pay)");

        // poprawka 157: liczniki doby (prawdziwe werbunki, nie wyceny) - linia "Konie rekrutow (157)"
        private static int _dVolN, _dVolMin = int.MaxValue, _dVolMax, _dMercShelf, _dMercOwn, _dMercFlat, _dPlShelf, _dPlOwn, _dWired;
        private static long _dVolSum, _dMercShelfGold, _dMercRefund, _dPlShelfGold, _dPlRefund;
        private static readonly Dictionary<string, int> _dShelfTowns = new Dictionary<string, int>(), _dOwnTowns = new Dictionary<string, int>();

        internal static void Reset() { ClearDay(); }

        private static void ClearDay()
        {
            _dVolN = _dVolMax = _dMercShelf = _dMercOwn = _dMercFlat = _dPlShelf = _dPlOwn = 0; _dVolMin = int.MaxValue;
            _dVolSum = _dMercShelfGold = _dMercRefund = _dPlShelfGold = _dPlRefund = 0;
            _dShelfTowns.Clear(); _dOwnTowns.Clear();
        }

        internal static bool IsMerc(CharacterObject c) { return c != null && (c.Occupation == Occupation.Mercenary || c.Occupation == Occupation.Gangster || c.Occupation == Occupation.CaravanGuard); }

        /// <summary>Poprawka 157 czynna: kon najemnika tylko z polki (wymaga ceny werbunku z dni zoldu i konia po cenie targu).</summary>
        internal static bool MercShelfOn { get { var s = Settings.Current; return s != null && s.HistoricalRecruitCost && s.HorsesAtMarketPrice && s.MercHorseFromShelf; } }

        public static void Prefix() { _depth++; }
        public static Exception Finalizer(Exception __exception) { if (_depth > 0) _depth--; return __exception; }

        public static void Postfix(CharacterObject __0, Hero __1, bool __2, ref ExplainedNumber __result)
        {
            if (_depth > 1) return;
            try
            {
                var s = Settings.Current;
                if (s == null || !s.HistoricalRecruitCost || __0 == null || __0.IsHero) return;
                int wage = Campaign.Current.Models.PartyWageModel.GetCharacterWage(__0);
                float days = Math.Max(0f, s.RecruitCostDays);
                bool merc = __0.Occupation == Occupation.Mercenary || __0.Occupation == Occupation.Gangster || __0.Occupation == Occupation.CaravanGuard;
                if (merc) days *= 2f;
                float target = Math.Max(1f, wage * days);
                if (!__2 && __0.IsMounted) target += HorseCost(__0, __1, merc);
                float mult = 1f + __result.SumOfFactors;
                if (mult <= 0.01f) return;
                float baseNow = __result.ResultNumber / mult;
                __result.Add(target - baseNow, _txt);
            }
            catch { }
        }

        /// <summary>Paczka 143: doplata za konia rekruta - cena jego konia na targu osady werbunku (HorsesAtMarketPrice), inaczej
        /// stala gry (DefaultPartyWageModel: 150 ponizej poziomu 26, wyzej 500). Poprawka 157: najemnik (merc) - cena konia, ktory
        /// przy werbunku zejdzie z polki tego targu, a gdy takiego konia na polce nie ma - 0 (wlasny kon); bez targu - stala jak dotad.</summary>
        internal static float HorseCost(CharacterObject troop, Hero buyer, bool merc = false)
        {
            float flat = troop.Level < 26 ? 150f : 500f;
            var s = Settings.Current;
            if (s == null || !s.HorsesAtMarketPrice) return flat;
            int p = 0;
            try
            {
                var item = TemplateHorse(troop);
                if (item == null) return flat;
                var where = WhereRecruited(buyer);
                if (merc && s.MercHorseFromShelf)
                {
                    var market = VolunteerKit.MarketOf(where);
                    if (market == null || market.Town == null) return flat;   // brak osady albo targu (okup: kupujacy null) - stala gry jak dotad
                    EquipmentElement el;
                    return FindShelfHorse(market, item, out el) ? Stables.ShelfPrice(market, el) : 0f;   // polka bez takiego konia - wlasny kon, bez doplaty
                }
                p = Stables.MarketPrice(where, new EquipmentElement(item));
            }
            catch { return flat; }
            if (p <= 0) return flat;                          // brak osady albo targu - stala gry
            return p;
        }

        /// <summary>Kon z wzorca jednostki (miejsce Horse: Equipment, inaczej FirstBattleEquipment) albo null.</summary>
        internal static ItemObject TemplateHorse(CharacterObject troop)
        {
            if (troop == null) return null;
            var eq = troop.Equipment;
            var horse = eq != null ? eq[EquipmentIndex.Horse] : EquipmentElement.Invalid;
            if (horse.Item == null) { var fb = troop.FirstBattleEquipment; if (fb != null) horse = fb[EquipmentIndex.Horse]; }
            return horse.Item;
        }

        /// <summary>Poprawka 157: kon na polce targu dla najemnika z danym koniem wzorca - najpierw ten sam przedmiot bez modyfikatora,
        /// potem ten sam z modyfikatorem, potem ta sama kategoria, tier i rodzaj zwierzecia (Monster); w kazdej grupie najtanszy po cenie
        /// polki (Stables.ShelfPrice), przy rownej cenie pierwszy na polce. Ta sama regula przy wycenie i przy zdjeciu z polki.</summary>
        internal static bool FindShelfHorse(Settlement market, ItemObject template, out EquipmentElement el)
        {
            el = EquipmentElement.Invalid;
            if (market == null || market.Town == null || template == null) return false;
            var r = market.ItemRoster;
            if (r == null) return false;
            int tier = (int)template.Tier;
            var cat = template.ItemCategory;
            var mon = template.HorseComponent != null ? template.HorseComponent.Monster : null;
            int bestScore = int.MaxValue, bestPrice = int.MaxValue;
            for (int i = 0; i < r.Count; i++)
            {
                var e = r[i];
                var it = e.EquipmentElement.Item;
                if (it == null || e.Amount <= 0 || it.ItemType != ItemObject.ItemTypeEnum.Horse) continue;
                int score;
                if (it == template) score = e.EquipmentElement.ItemModifier == null ? 0 : 1;
                else if (cat != null && it.ItemCategory == cat && (int)it.Tier == tier && (it.HorseComponent != null ? it.HorseComponent.Monster : null) == mon) score = 2;
                else continue;
                if (score > bestScore) continue;
                int price = Stables.ShelfPrice(market, e.EquipmentElement);
                if (score < bestScore || price < bestPrice) { bestScore = score; bestPrice = price; el = e.EquipmentElement; }
            }
            return bestScore != int.MaxValue;
        }

        /// <summary>Poprawka 157: zdejmuje z polki targu osady werbunku do n koni dla najemnikow (regula FindShelfHorse); zwraca, ile zeszlo.
        /// Kon zdjety z polki to kon najemnika (w zbrojowni / sprzecie jednostki jest juz kon wzorca) - nie idzie nigdzie indziej.</summary>
        internal static int TakeShelfHorses(Settlement where, CharacterObject troop, int n)
        {
            var market = VolunteerKit.MarketOf(where);
            var item = TemplateHorse(troop);
            if (market == null || item == null || n <= 0) return 0;
            int k = 0;
            for (; k < n; k++)
            {
                EquipmentElement el;
                if (!FindShelfHorse(market, item, out el)) break;
                market.ItemRoster.AddToCounts(el, -1);
            }
            return k;
        }

        /// <summary>Poprawka po recenzji 157: ile z kosztu werbunku (cost - koszt jednej sztuki z gry) kupujacy naprawde placi za konia - koszt ze
        /// sprzetem minus koszt "bez sprzetu" (withoutItemCost) tego samego modelu i tego samego kupujacego. Gra mnozy (dni zoldu + kon) przez
        /// 1 + SumOfFactors (perki gry: Slick Negotiator, Sword for Barter, Frugal, kultura; BK: ranga klanu, obca kultura w krolestwie +25%,
        /// Drafting Hidage / Free Contracts +50 / +100%) - kon w koszcie to cena polki RAZY ten mnoznik (po zaokragleniu i dolnej granicy
        /// modelu), nie sama cena polki. Ta kwota idzie do kasy miasta za konia zdjetego z polki i ta sama wraca za konia, ktorego nie bylo.
        /// fallback - gdy model rzuci wyjatkiem (cena polki, najwyzej cost).</summary>
        internal static int HorseShareOfCost(CharacterObject troop, Hero buyer, int cost, int fallback)
        {
            if (troop == null || cost <= 0) return 0;
            try
            {
                int bare = Campaign.Current.Models.PartyWageModel.GetTroopRecruitmentCost(troop, buyer, true).RoundedResultNumber;
                return Math.Max(0, Math.Min(cost, cost - bare));
            }
            catch { return Math.Max(0, Math.Min(cost, fallback)); }
        }

        /// <summary>Poprawka 157: cena konia najemnika, ktora siedzi w jego koszcie werbunku (0 = wlasny kon albo poza regula), i targ.</summary>
        internal static int MercHorseQuote(CharacterObject troop, Hero buyer, out Settlement market)
        {
            market = null;
            if (!MercShelfOn || troop == null || !troop.IsMounted || !IsMerc(troop)) return 0;
            try
            {
                var item = TemplateHorse(troop);
                market = VolunteerKit.MarketOf(WhereRecruited(buyer));
                EquipmentElement el;
                if (item == null || market == null || market.Town == null || !FindShelfHorse(market, item, out el)) return 0;
                return Stables.ShelfPrice(market, el);
            }
            catch { return 0; }
        }

        private static void Bump(Dictionary<string, int> d, Settlement st, int n)
        {
            if (st == null || n <= 0) return;
            string k = st.Name != null ? st.Name.ToString() : st.StringId;
            int v; d.TryGetValue(k, out v); d[k] = v + n;
        }

        /// <summary>Poprawka 157: liczniki prawdziwych werbunkow (LevyGold i zakup gracza).</summary>
        internal static void NoteVolunteerHorse(int price) { if (price <= 0) return; _dVolN++; _dVolSum += price; if (price < _dVolMin) _dVolMin = price; if (price > _dVolMax) _dVolMax = price; }
        internal static void NoteMercHorses(Settlement town, int shelf, int own, int horsePrice, int refund, bool flat, bool player)
        {
            if (flat) { _dMercFlat += shelf + own; if (!player) _dMercRefund += refund; return; }   // recenzja 157: doplata stalej gry za konia spoza polki wraca kupujacemu
            if (player) { _dPlShelf += shelf; _dPlOwn += own; _dPlShelfGold += (long)shelf * horsePrice; _dPlRefund += refund; }
            else { _dMercShelf += shelf; _dMercOwn += own; _dMercShelfGold += (long)shelf * horsePrice; _dMercRefund += refund; Bump(_dShelfTowns, town, shelf); Bump(_dOwnTowns, town, own); }   // miasta - tylko AI
        }

        private static string Top(Dictionary<string, int> d)
        {
            if (d.Count == 0) return "";
            var l = new List<KeyValuePair<string, int>>(d); l.Sort((a, b) => b.Value.CompareTo(a.Value));
            var p = new List<string>(); for (int i = 0; i < l.Count && i < 4; i++) p.Add(l[i].Key + " " + l[i].Value);
            return " [" + string.Join(", ", p.ToArray()) + "]";
        }

        /// <summary>Linia dnia "Konie rekrutow (157)" - zamiast 8 probek wyceny z 143 (wolane z ticku dobowego ArmouryBehavior).</summary>
        internal static void Daily()
        {
            var s = Settings.Current;
            if (s == null) return;
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            Log.Info("Konie rekrutow (157" + (MercShelfOn ? ", kon najemnika z polki CZYNNY" : ", kon najemnika z polki WYLACZONY (" + (!s.MercHorseFromShelf ? "MCM" : "wymaga Historical Recruit Cost i Horses At Market Price") + ")")
                     + "): dzien " + ((int)CampaignTime.Now.ToDays - 1) + " - ochotnicy konni od notabli " + _dVolN
                     + (_dVolN > 0 ? " (kon po cenie targu srednio " + (_dVolSum / _dVolN).ToString(inv) + ", od " + _dVolMin + " do " + _dVolMax + ", razem " + _dVolSum.ToString(inv) + " do notabli)" : "")
                     + "; najemnicy konni AI: kon z polki " + _dMercShelf + (_dMercShelf > 0 ? " (miastom za konie " + _dMercShelfGold.ToString(inv) + ", srednio " + (_dMercShelfGold / _dMercShelf).ToString(inv) + ")" : "") + Top(_dShelfTowns)
                     + ", z wlasnym koniem bez doplaty " + _dMercOwn + Top(_dOwnTowns) + ", nadplata za brakujace konie zwrocona " + _dMercRefund.ToString(inv)
                     + ", bez targu (stala gry) " + _dMercFlat
                     + "; gracz: kon z polki " + _dPlShelf + (_dPlShelf > 0 ? " (miastu " + _dPlShelfGold.ToString(inv) + ")" : "") + ", wlasny kon " + _dPlOwn + ", zwrot " + _dPlRefund.ToString(inv)
                     + "; latki zakupu gracza wpiete " + _dWired + "/2.");
            ClearDay();
        }

        // ------------------------------------------------------------ poprawka 157: najemnicy gracza (menu zaulka i rozmowa w karczmie)
        internal sealed class PlayerBuy { public Settlement Town; public CharacterObject Troop; public int Before, Horse, Per; }   // Per: poprawka po recenzji 157 - kon w koszcie gracza (z jego mnoznikiem)

        /// <summary>Prefiks RecruitmentCampaignBehavior.buy_mercenaries_on_consequence / BuyMercenaries: co i po ile kupuje gracz.</summary>
        public static void PlayerBuyPrefix(RecruitmentCampaignBehavior __instance, out PlayerBuy __state)
        {
            __state = null;
            try
            {
                if (!MercShelfOn || __instance == null) return;
                var st = Settlement.CurrentSettlement ?? TaleWorlds.CampaignSystem.Encounters.PlayerEncounter.EncounterSettlement;
                if (st == null || st.Town == null) return;
                var md = __instance.GetMercenaryData(st.Town);
                if (md == null || md.TroopType == null || !md.TroopType.IsMounted || !IsMerc(md.TroopType)) return;
                Settlement market;
                int horse = MercHorseQuote(md.TroopType, Hero.MainHero, out market);
                // poprawka po recenzji 157: kon w koszcie gracza = koszt ze sprzetem - bez sprzetu (ten sam model, perki gracza, prawa i ranga
                // jego klanu) - dotad miasto dostawalo pelna cene polki, a gracz zwrot pelnej ceny polki: przy mnozniku 0.75 zloto z niczego
                int cost = Campaign.Current.Models.PartyWageModel.GetTroopRecruitmentCost(md.TroopType, Hero.MainHero).RoundedResultNumber;
                int per = HorseShareOfCost(md.TroopType, Hero.MainHero, cost, horse);
                __state = new PlayerBuy { Town = st, Troop = md.TroopType, Before = md.Number, Horse = horse, Per = per };
            }
            catch { __state = null; }
        }

        /// <summary>Postfiks tych metod: gra skasowala zaplate gracza (GiveGoldAction do nikogo); za kazdego konia, ktory zszedl z polki,
        /// miasto dostaje to, co gracz za niego zaplacil (cena polki x mnoznik gracza - poprawka po recenzji 157), a doplata za konie, ktorych
        /// na polce zabraklo, wraca graczowi w tej samej kwocie.</summary>
        public static void PlayerBuyPostfix(RecruitmentCampaignBehavior __instance, PlayerBuy __state)
        {
            if (__state == null || __instance == null) return;
            try
            {
                var md = __instance.GetMercenaryData(__state.Town.Town);
                int n = __state.Before - (md != null && md.TroopType == __state.Troop ? md.Number : 0);
                if (n <= 0) return;
                int per = Math.Max(0, __state.Per);
                if (__state.Horse <= 0 && per <= 0) { NoteMercHorses(__state.Town, 0, n, 0, 0, false, true); return; }
                int k = __state.Horse > 0 && per > 0 ? TakeShelfHorses(__state.Town, __state.Troop, n) : 0;   // kon "bez targu" (stala gry) - zaden z polki, cala doplata wraca
                int toTown = k * per, refund = (n - k) * per;
                if (toTown > 0) { __state.Town.Town.ChangeGold(toTown); MoneyLedger.Note(MoneyLedger.NMerc, __state.Town, toTown); }
                if (refund > 0) Hero.MainHero.ChangeHeroGold(refund);
                MoneyLedger.NoteLevyBack(toTown + refund);   // ksiega pieniadza: gra skasowala te zaplate gracza przez GiveGoldAction
                NoteMercHorses(__state.Town, k, n - k, per, refund, false, true);
            }
            catch (Exception e) { Log.Error("RecruitCost.PlayerBuyPostfix", e); }
        }

        /// <summary>Osada werbunku: z latki miejsca (AI), gracz - ta, w ktorej jest; AI - ta, w ktorej stoi jego druzyna.</summary>
        internal static Settlement WhereRecruited(Hero buyer)
        {
            if (_where != null) return _where;
            if (buyer == null) return null;
            if (buyer == Hero.MainHero) return Settlement.CurrentSettlement ?? buyer.CurrentSettlement;
            return buyer.CurrentSettlement;
        }

        /// <summary>RecruitmentCampaignBehavior.CheckRecruiting(MobileParty, Settlement): werbunek AI w tej osadzie (co godzine i przy
        /// wjezdzie - wtedy druzyna jeszcze w niej nie stoi). Poprzednia osada w __state, przywracana w finalizerze.</summary>
        public static void WherePrefix(Settlement __1, out Settlement __state) { __state = _where; _where = __1; }

        /// <summary>GarrisonRecruitmentCampaignBehavior.TickAutoRecruitmentGarrisonChange(Town): garnizon werbuje w swoim miescie
        /// albo zamku (gra liczy cene z wodzem klanu wlasciciela, ktory moze byc gdziekolwiek).</summary>
        public static void GarrisonWherePrefix(Town __0, out Settlement __state) { __state = _where; _where = __0 != null ? __0.Settlement : null; }

        public static Exception WhereFinalizer(Exception __exception, Settlement __state) { _where = __state; return __exception; }

        internal static void ApplyAll(Harmony h)
        {
            int n = 0;
            var seen = new HashSet<Type>();
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = asm.GetTypes(); } catch { continue; }
                foreach (var t in types)
                {
                    try
                    {
                        if (t == null || t.IsAbstract || !typeof(TaleWorlds.CampaignSystem.ComponentInterfaces.PartyWageModel).IsAssignableFrom(t)) continue;
                        var m = t.GetMethod("GetTroopRecruitmentCost", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                        if (m == null || m.DeclaringType != t || seen.Contains(t)) continue;
                        seen.Add(t);
                        h.Patch(m, prefix: new HarmonyMethod(typeof(RecruitCost), nameof(Prefix)) { priority = Priority.First },
                                   postfix: new HarmonyMethod(typeof(RecruitCost), nameof(Postfix)) { priority = Priority.Last },
                                   finalizer: new HarmonyMethod(typeof(RecruitCost), nameof(Finalizer)));
                        n++;
                    }
                    catch { }
                }
            }
            Log.Info("RecruitCost: cena werbunku = dni zoldu w " + n + " modelach.");
            int w = 0;
            try
            {
                var cr = AccessTools.Method(typeof(RecruitmentCampaignBehavior), "CheckRecruiting", new[] { typeof(MobileParty), typeof(Settlement) });
                if (cr != null)
                {
                    h.Patch(cr, prefix: new HarmonyMethod(typeof(RecruitCost), nameof(WherePrefix)), finalizer: new HarmonyMethod(typeof(RecruitCost), nameof(WhereFinalizer)));
                    w++;
                }
                var gr = AccessTools.Method(typeof(GarrisonRecruitmentCampaignBehavior), "TickAutoRecruitmentGarrisonChange", new[] { typeof(Town) });
                if (gr != null)
                {
                    h.Patch(gr, prefix: new HarmonyMethod(typeof(RecruitCost), nameof(GarrisonWherePrefix)), finalizer: new HarmonyMethod(typeof(RecruitCost), nameof(WhereFinalizer)));
                    w++;
                }
            }
            catch (Exception e) { Log.Error("RecruitCost.Where", e); }
            // poprawka 157: zakup najemnikow przez gracza (menu zaulka, rozmowa w karczmie) - kon z polki, zaplata za konia do kasy miasta
            int pw = 0;
            foreach (var name in new[] { "buy_mercenaries_on_consequence", "BuyMercenaries" })
            {
                try
                {
                    var m = AccessTools.Method(typeof(RecruitmentCampaignBehavior), name, Type.EmptyTypes);
                    if (m == null) continue;
                    h.Patch(m, prefix: new HarmonyMethod(typeof(RecruitCost), nameof(PlayerBuyPrefix)), postfix: new HarmonyMethod(typeof(RecruitCost), nameof(PlayerBuyPostfix)));
                    pw++;
                }
                catch (Exception e) { Log.Error("RecruitCost.PlayerBuy " + name, e); }
            }
            _dWired = pw;
            Log.Info("RecruitCost: kon rekruta po cenie targu (HorsesAtMarketPrice) - miejsce werbunku AI wpiete w " + w + "/2 metodach (CheckRecruiting, TickAutoRecruitmentGarrisonChange); kon najemnika tylko z polki targu (MercHorseFromShelf, poprawka 157) - zakup gracza wpiety w " + pw + "/2 metodach (buy_mercenaries_on_consequence, BuyMercenaries).");
        }
    }
}
