using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// ZAKUPY ARMII AI (Jeff 04.10: "nie dostaja nic za darmo"; "popyt wzrasta, bo armia bedzie
    /// potrzebowala kupic, aby wyposazyc wojsko - lordowie AI, gracz - realnie, nie sztucznie";
    /// "lord mowi: piekna zbroja, ale biore te tansza, chroni minimalnie gorzej, a jest o polowe
    /// tansza - nie potrzebuje zbroi dla krola"). docs/MODEL-MATERIALOW.md, krok 6.
    ///
    /// Dynamic Troop Equipment (dekompilacja 1.4.7.2, EveryoneCampaignBehavior) dawal AI za darmo:
    ///  - codziennie AllocateRandomEquipmentToPartyArmory (sztuki ze wzorcow, z lenn, od rodu) -> WYLACZONE;
    ///  - co tydzien MoveRosterToArmory: bron/zbroje z taboru do zbrojowni I PELNA Value w zlocie
    ///    dla lorda (drukarka pieniedzy) -> WYLACZONE; sztuki zostaja w taborze i lord sprzedaje je
    ///    w miescie zwyklym PartiesSellLoot;
    ///  - komplet przy werbunku (OnTroopRecruited) -> ZOSTAJE na przelaczniku AiRecruitsBringKit
    ///    (pobor przychodzi z wlasnym sprzetem).
    /// W zamian lord AI w miescie/zamku (wejscie + raz dziennie, gdy stoi) liczy braki zbrojowni
    /// wzgledem wzorcow swoich ludzi (koszyki typ x tier, bez koni - te ma Stajnia) i KUPUJE z polki
    /// po cenie rynku (prawo podazy i popytu + wycena), placac miastu ze swojego zlota, najwyzej
    /// AiGearBudgetPercent zlota na wizyte, zostawiajac AiGearGoldReserve. Wybor: tier ten sam albo
    /// o jeden nizszy, najlepszy stosunek skutecznosci (Effectiveness) do ceny; unikaty pomija
    /// (prestiz to nie ochrona). Kolejnosc: korpus, bron, tarcza, helm, amunicja, nogi, rece, plaszcz.
    ///
    /// sklad7 (scalenie 171 + K1) - JEDNA regula zakupow brakow (TryBuyCore) dla lordow AI i zalog:
    ///  - placi najpierw sakiewka ludzi (lord AI: lup; zaloga: jej zold - K1 A6), potem kiesa pana (zaloga gracza - tylko z GarrisonBuysGearPlayer);
    ///  - brak liczony do sufitu jednostki (K1c, SwapMath.CeilingTier) i tylko sztuka, ktora ktos z koszyka udzwignie (K1, ItemReq);
    ///  - zaloga zamku: polka wlasnego zamku codziennie, reszta zamowieniem w miescie handlowym raz na GarrisonOrderDays dob, towar jedzie wozem (171 C2,
    ///    GarrisonCarts) - K1 A4 (zakup w miescie od razu, "woz pana bez kosztu") usuniete: dwie drogi tego samego zakupu, druga bez drogi towaru;
    ///  - DTE odrzucil sztuke (czarna lista) - wraca na polke, nikt nie placi (K1); 174: sztuki zastepcze tylko z polki tej osady.
    /// </summary>
    internal static class AiGear
    {
        internal static void Reset() { _lastDay.Clear(); _dteRefused.Clear(); _dteOk.Clear(); }
        private static Type _dte;
        private static FieldInfo _armories;
        private static MethodInfo _add;
        private static bool _looked;
        private static readonly Dictionary<MobileParty, int> _lastDay = new Dictionary<MobileParty, int>();
        private static int _dayPieces, _dayGold, _dayVisits, _dayLogged, _dayStamp = -1;

        internal static bool On { get { var s = Settings.Current; return s != null && s.AiBuysGear; } }

        // ------------------------------------------------------------ 174, pytanie 2: gorszy sprzet zamiast zadnego
        // (a) AiAnyMeleeWhenShort (rekomendacja "tak"): zolnierz bez broni bialej swojego szczebla, gdy partia nie ma dosc broni bialej W OGOLE (suma typow
        //     1H, 2H, drzewce w dowolnym tierze mniejsza niz sloty wzorcow), kupuje dowolna bron biala tieru <= swojego; (b) AiWorseBodyArmourWhenShort
        //     (domyslnie WYLACZONE - wlaczyc po tescie, gdy "cokolwiek na tulow" < 75%): zbroja na tulow o 2 tiery nizej albo przeszywanica tieru <= swojego.
        // Kupujemy tylko tyle, ile brakuje "dowolnego szczebla" - te sztuki nie sa nadwyzka dla MenPurse (bron biala liczona tam grupa), wiec nie ma
        // petli kup-sprzedaj. Do cwiczen i awansu liczy sie dalej tylko bron szczebla (ArmsDrill, AiGear.Deficit bez zmian).
        internal static bool SubstituteMeleeOn { get { var s = Settings.Current; return s != null && s.AiAnyMeleeWhenShort; } }
        private static bool SubstituteBodyOn { get { var s = Settings.Current; return s != null && s.AiWorseBodyArmourWhenShort; } }
        private static int _daySubMelee, _daySubBody;

        internal static bool Melee(int type)
        {
            return type == (int)ItemObject.ItemTypeEnum.OneHandedWeapon || type == (int)ItemObject.ItemTypeEnum.TwoHandedWeapon || type == (int)ItemObject.ItemTypeEnum.Polearm;
        }

        /// <summary>Sprzedaz nadwyzek wedlug typu (MenPurse.SellArmorySurplus - lordowie i zalogi): przy AiAnyMeleeWhenShort bron biala liczona razem - ile wolno sprzedac z calej grupy.</summary>
        internal static int MeleeGroupExtra(Dictionary<int, int> have, Dictionary<int, int> needByType, float keepPercent)
        {
            int h = 0, k = 0;
            foreach (var kv in have) if (Melee(kv.Key)) h += kv.Value;
            if (needByType != null) foreach (var kv in needByType) if (Melee(kv.Key)) k += (int)Math.Ceiling(kv.Value * (1f + Math.Max(0f, keepPercent) / 100f));
            return Math.Max(0, h - k);
        }

        private static int BuySubstitutes(Settlement market, MobileParty buyer, Dictionary<ItemObject, int> armory, Dictionary<int, int> needOut, Dictionary<int, int> need,
                                          Dictionary<int, List<CharacterObject>> lifters, int budget, int maxPieces, ref int pieces, List<string> bought, Deliver deliver, int who)
        {
            int spent = 0;
            long tc = Cost174.Begin(Cost174.SBuySub);   // 174b.5 F6 (probka 1/16, tylko log)
            try
            {
                bool melee = SubstituteMeleeOn, body = SubstituteBodyOn;
                if ((!melee && !body) || market == null || market.ItemRoster == null || market.Town == null) return 0;
                if (pieces >= maxPieces || budget <= 0) return 0;   // 174b.5 F2: druga petla i tak nic nie kupi (ten sam warunek petli)
                // "dowolny szczebel": sloty wzorcow wedlug typu wobec sztuk zbrojowni w dowolnym tierze
                var slots = new Dictionary<int, int>(); var have = new Dictionary<int, int>();
                foreach (var kv in needOut) { int ty = kv.Key / 10; int v; slots.TryGetValue(ty, out v); slots[ty] = v + kv.Value; }
                if (armory != null) foreach (var kv in armory) { if (kv.Key == null || kv.Value <= 0) continue; int ty = (int)kv.Key.ItemType; int v; have.TryGetValue(ty, out v); have[ty] = v + kv.Value; }
                int gapMelee = 0, gapBody = 0;
                foreach (var kv in slots) if (Melee(kv.Key)) gapMelee += kv.Value;
                foreach (var kv in have) if (Melee(kv.Key)) gapMelee -= kv.Value;
                { int sb, hb; slots.TryGetValue((int)ItemObject.ItemTypeEnum.BodyArmor, out sb); have.TryGetValue((int)ItemObject.ItemTypeEnum.BodyArmor, out hb); gapBody = sb - hb; }
                if (gapMelee <= 0 && gapBody <= 0) return 0;
                // 174b.5 F2(a): bez koszyka z brakiem, ktory moglby kupic zastepcza (ten sam warunek co w drugiej petli) nic sie nie kupi - bez wyceny polki
                bool anyNeed = false;
                foreach (var kv in need)
                {
                    if (kv.Value <= 0) continue;
                    int ty0 = kv.Key / 10;
                    if ((melee && Melee(ty0) && gapMelee > 0) || (body && ty0 == (int)ItemObject.ItemTypeEnum.BodyArmor && gapBody > 0)) { anyNeed = true; break; }
                }
                if (!anyNeed) return 0;
                var shelf = market.ItemRoster;
                // recenzja 174 (koszt): jedno przejscie polki na wizyte - kandydaci z cena liczona raz na stos, malejaco wedlug skutecznosci do ceny;
                // przy zakupie cena wybranego stosu liczona na nowo przed kazda sztuka (ShelfBuy - polka zmienia sie po kazdej sztuce)
                var cands = new List<SubCand>();
                bool subHeld = false;
                for (int i = 0; i < shelf.Count; i++)
                {
                    var el = shelf.GetElementCopyAtIndex(i);
                    var it = el.EquipmentElement.Item;
                    if (el.Amount <= 0 || it == null || ArmsPricing.IsUnique(it) || !DteTakes(it)) continue;
                    bool isM = melee && gapMelee > 0 && Melee((int)it.ItemType), isB = body && gapBody > 0 && it.ItemType == ItemObject.ItemTypeEnum.BodyArmor;
                    if (!isM && !isB) continue;
                    if (isB && ShopReserve.Free(market, it) <= 0) { subHeld = true; continue; }   // 174b.4: ostatnia sztuka pasma zostaje na straganie
                    int price = market.Town.MarketData.GetPrice(el.EquipmentElement, buyer, false, market.Party);
                    if (price <= 0) continue;
                    bool cloth = isB && it.ArmorComponent != null && it.ArmorComponent.MaterialType == ArmorComponent.ArmorMaterialTypes.Cloth;
                    cands.Add(new SubCand { El = el.EquipmentElement, Left = el.Amount, Tier = TierOf(it), Price = price, Gen = 1, Melee = isM, Cloth = cloth,
                                            Score = (it.Effectiveness > 0f ? it.Effectiveness : 1f) / price });
                }
                if (cands.Count == 0) { if (subHeld) Measure174b.NoteHeld(who, 1); return 0; }
                cands.Sort((x, y) => y.Score.CompareTo(x.Score));
                var keys = new List<int>(need.Keys); keys.Sort((a, b) => (b % 10).CompareTo(a % 10));   // najwyzsze szczeble najpierw
                int gen = 1;   // 174b.5 F2(b): cena z pierwszego przejscia wazna do pierwszego zakupu (polka, kiesa i ksiega te same), potem liczona od nowa jak dotad
                foreach (var k in keys)
                {
                    // recenzja 174: need tylko czytany - sztuka zastepcza nie liczy sie do szczebla, wiec nie tlumi zamowien warsztatom (NoteUnmetOnce)
                    // ani zamowienia zalogi zamku w miescie; pokrywa "ochrone" (luka dowolnego szczebla), nie sygnal szczebla
                    int ty = k / 10, t = k % 10, deficit = need[k];
                    bool isMelee = melee && Melee(ty) && gapMelee > 0, isBody = body && ty == (int)ItemObject.ItemTypeEnum.BodyArmor && gapBody > 0;
                    if (deficit <= 0 || (!isMelee && !isBody)) continue;
                    List<CharacterObject> lift = null; if (lifters != null) lifters.TryGetValue(k, out lift);
                    for (int ci = 0; ci < cands.Count && deficit > 0 && pieces < maxPieces && spent < budget && (isMelee ? gapMelee : gapBody) > 0; ci++)
                    {
                        var c = cands[ci];
                        if (c.Left <= 0 || c.Melee != isMelee || c.Tier > t) continue;
                        if (!isMelee && !c.Cloth && c.Tier > Math.Max(1, t - 2)) continue;   // przeszywanica tieru <= swojego albo zbroja 2 tiery nizej
                        if (!Lift(lift, c.El.Item)) continue;   // sklad7 (K1): tylko sztuka, ktora ktos z koszyka udzwignie - inaczej nadwyzki sprzedaja ja nazajutrz
                        // 174b.5 F2(b): cena z pamieci wazna do pierwszego zakupu (gen), potem od nowa - i jako cena PIERWSZEJ sztuki partii w ShelfBuy (known)
                        int price;
                        if (c.Gen == gen) price = c.Price;
                        else { price = market.Town.MarketData.GetPrice(c.El, buyer, false, market.Party); c.Price = price; c.Gen = gen; cands[ci] = c; }
                        if (price <= 0 || price > budget - spent) continue;
                        int maxN = Math.Min(Math.Min(deficit, c.Left), Math.Min(maxPieces - pieces, isMelee ? gapMelee : gapBody));
                        // 174b.4: rezerwa kramu ogranicza partie; licznik "zatrzymane" jak w 174b - szacunek po cenie pierwszej sztuki (tylko licznik)
                        if (!isMelee && maxN > 0) { int fr = ShopReserve.Free(market, c.El.Item); if (fr < maxN) { Measure174b.NoteHeld(who, Math.Min(maxN, (budget - spent) / price) - Math.Max(0, fr)); maxN = Math.Max(0, fr); } }
                        if (maxN <= 0) continue;
                        // ceny hurtu (Jeff 09.10 08:00): kazda sztuka po swojej cenie - wycena od nowa po zdjeciu poprzedniej (ShelfBuy); dotad cena pierwszej x n
                        var cel = c.El;
                        int cost, first, last;
                        int n = ShelfBuy.Take(shelf, cel, maxN, budget - spent, () => market.Town.MarketData.GetPrice(cel, buyer, false, market.Party), out cost, out first, out last, price);
                        if (n <= 0) continue;
                        gen++;   // 174b.5 F2(b): polka sie zmienila - ceny kandydatow od nowa
                        if (!deliver(cel, n, k, cost)) { ShelfBuy.PutBack(shelf, cel, n, cost, first); c.Left = 0; cands[ci] = c; continue; }   // K1: DTE odrzucil - wraca na polke
                        c.Left -= n; cands[ci] = c;
                        ArmsScrap.NoteBuy(market, c.El.Item, n);
                        spent += cost; pieces += n; deficit -= n;
                        if (isMelee) { gapMelee -= n; _daySubMelee += n; } else { gapBody -= n; _daySubBody += n; }
                        if (bought.Count < 6) bought.Add(c.El.Item.StringId + " " + ShelfBuy.Prices(n, first, last) + " (zastepcza)");
                    }
                }
            }
            catch (Exception e) { _subStumbles++; if (_subErrLogged.Add("BuySubstitutes")) Log.Error("AiGear.BuySubstitutes", e); }
            finally { Cost174.End(Cost174.SBuySub, tc); }
            return spent;
        }

        private struct SubCand { public EquipmentElement El; public int Left, Tier, Price, Gen; public float Score; public bool Melee, Cloth; }
        private static int _subStumbles;
        private static readonly HashSet<string> _subErrLogged = new HashSet<string>();   // recenzja 174: Log.Error raz na miejsce, reszta w liczniku linii dnia

        private static bool Look()
        {
            if (_looked) return _dte != null && _armories != null && _add != null;
            _looked = true;
            try
            {
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    var t = asm.GetType("DynamicTroopEquipmentReupload.EveryoneCampaignBehavior");
                    if (t != null) { _dte = t; break; }
                }
                if (_dte != null)
                {
                    _armories = _dte.GetField("PartyArmories", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                    _add = _dte.GetMethod("AddItemToPartyArmory", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                    // poprawki sklad7: ten sam test, ktory DTE robi w AddItemToPartyArmory (rozpoznanie sztuki + blacklist.json) - przed zakupem
                    var bl = _dte.Assembly.GetType("DynamicTroopEquipmentReupload.ItemBlackList");
                    _blTest = bl != null ? bl.GetMethod("Test", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(ItemObject) }, null) : null;
                }
            }
            catch { }
            return _dte != null && _armories != null && _add != null;
        }

        // ------------------------------------------------------------ latki DTE
        public static bool SkipWhenBuying() { return !On; }

        /// <summary>
        /// Prefiks DTE GarbageCollectParties (co dobe na partie): zbrojownia zostaje dla zalogi (wpis 89) i - 171, poprawka po recenzji - dla kazdej aktywnej
        /// partii lorda (poza gracza): DTE kasowal zbrojownie partii bez wodza albo rozwiazywanej w pierwszej dobie czekania (DisbandPartyCampaignBehavior:
        /// doba czekania, potem droga do twierdzy; TeleportHeroAction z opoznieniem), zanim gra scalila ja z zaloga (A7) albo dala jej nowego wodza -
        /// ludzie wchodzili do zalogi nadzy, a nowy wodz kupowal wszystko od nowa. Zbrojownie i tak kasuje DTE OnMobilePartyDestroyed (zniszczenie partii).
        /// </summary>
        public static bool KeepGarrisonArmory(MobileParty mobileParty, ref bool __result)
        {
            if (!On || mobileParty == null || !mobileParty.IsActive) return true;
            if (mobileParty.IsGarrison) { __result = false; return false; }
            if (mobileParty.IsLordParty && !mobileParty.IsMainParty)
            {
                try { if (mobileParty.LeaderHero == null || mobileParty.IsDisbanding) GarrisonArmory.NoteKeptLeaderless(); } catch { }
                __result = false; return false;
            }
            return true;
        }

        public static bool SkipWeeklyTrim() { return !(On && MenPurse.On); }

        internal static void Forget(MobileParty mp) { try { if (mp != null) _lastDay.Remove(mp); } catch { } }

        // nazwy parametrow jak w DTE OnTroopRecruited(Hero recruiterHero, Settlement recruitmentSettlement, Hero recruitmentSource, CharacterObject troop, int amount)
        public static bool RecruitKitPrefix(Hero recruiterHero, Settlement recruitmentSettlement, Hero recruitmentSource, CharacterObject troop, int amount)
        {
            var s = Settings.Current;
            if (s == null) return true;
            if (recruiterHero == null) return true;
            if (recruiterHero == Hero.MainHero) { try { if (RecruitKit.On) RecruitKit.OnRecruited(recruiterHero, recruitmentSettlement, recruitmentSource, troop, amount); } catch { } return true; }
            // 171 A1: echo werbunku ROT - ten sam czlowiek (zamiana oznaki X -> Y), komplet dostal w zdarzeniu pierwszym; DTE nic nie doklada
            try { if (RecruitSources.IsRotEcho(recruitmentSettlement, recruitmentSource)) { RecruitKit.NoteEcho(RecruitSources.EchoFrom, troop, amount); return false; } }
            catch (Exception e) { RecruitSources.Stumble("echo", e); }
            if (!s.AiRecruitsBringKit) return false;
            // wpis 92: AI - tylko to, co notabl naprawde kupil (tier 1: wlasny dobytek); 171 B2: bez zapisu - "z tym, co ma"
            try { if (RecruitKit.On) return RecruitKit.OnRecruited(recruiterHero, recruitmentSettlement, recruitmentSource, troop, amount); } catch (Exception e) { Log.Error("RecruitKit", e); }
            return true;
        }

        internal static void ApplyAll(Harmony h)
        {
            try
            {
                if (!Look()) { Log.Info("AiGear: Dynamic Troop Equipment nie znaleziony - zakupy AI wylaczone."); return; }
                var skip = new HarmonyMethod(typeof(AiGear), nameof(SkipWhenBuying));
                var a = AccessTools.Method(_dte, "AllocateRandomEquipmentToPartyArmory");
                var m = AccessTools.Method(_dte, "MoveRosterToArmory");
                var r = AccessTools.Method(_dte, "OnTroopRecruited");
                if (a != null) h.Patch(a, prefix: skip);
                if (m != null) h.Patch(m, prefix: skip);
                if (r != null) h.Patch(r, prefix: new HarmonyMethod(typeof(AiGear), nameof(RecruitKitPrefix)));
                // wpis 89 (audyt, kod DTE): GarbageCollectParties kasuje zbrojownie partii bez dowodcy - garnizon tracil co dobe wszystko,
                // co kupil (log: 87-115 zakupow garnizonow dziennie bez spadku); GarbageCollectEquipments co tydzien kasowal nadwyzke
                // ponad liczbe ludzi (zapas, druga bron) - nadwyzki sprzedaje teraz MenPurse
                var gcp = AccessTools.Method(_dte, "GarbageCollectParties");
                if (gcp != null) h.Patch(gcp, prefix: new HarmonyMethod(typeof(AiGear), nameof(KeepGarrisonArmory)));
                var gce = AccessTools.Method(_dte, "GarbageCollectEquipments");
                if (gce != null) h.Patch(gce, prefix: new HarmonyMethod(typeof(AiGear), nameof(SkipWeeklyTrim)));
                Log.Info("AiGear: latki DTE - darmowy przydzial " + (a != null ? "wpiety" : "BRAK") + ", doplata z taboru "
                         + (m != null ? "wpieta" : "BRAK") + ", komplet przy werbunku " + (r != null ? "wpiety" : "BRAK")
                         + "; zakupy AI " + (On ? "CZYNNE" : "wylaczone w MCM") + ".");
            }
            catch (Exception e) { Log.Error("AiGear.ApplyAll", e); }
        }

        // ------------------------------------------------------------ zakupy
        internal static void OnSettlementEntered(MobileParty mp, Settlement st, Hero hero)
        {
            // K1 (A5): lord AI w MIESCIE - nadwyzki, naprawy, braki i lepsze w JEDNYM zdarzeniu i w tej kolejnosci (MenPurse.OnEntered
            // wola TryBuy po sprzedazy nadwyzek i naprawach). Gra wola sluchaczy od ostatnio dopisanego (MbEvent), wiec ten sluchacz
            // biegl dotad PRZED MenPurse.OnEntered - braki kupowane przed sprzedaza nadwyzek, wbrew opisowi "nadwyzki PRZED zakupami".
            if (MenPurse.On && mp != null && st != null && st.IsTown && mp.IsLordParty && !mp.IsMainParty) return;
            TryBuy(mp, st);
        }

        internal static void OnDailyTickParty(MobileParty mp)
        {
            try
            {
                if (mp == null || mp.CurrentSettlement == null) return;
                var st = mp.CurrentSettlement;
                if (mp.IsGarrison) MenPurse.GarrisonDay(mp, st);   // K1 (A11): zaloga bez ludzi - sakiewka do kasy osady (nadwyzki: GarrisonArmory.SellWeek)
                // K1 (przeglad): zaloga zamku naprawia u kowali najblizszego miasta handlowego (jak zakupy) - od K1 jej sprzet obija sie
                // w bitwach (AiWear), a bez kowali rezerwa sakiewki na naprawy wisialaby wiecznie i blokowala zakupy
                // Poprawki sklad7: naprawy to usluga (obite sztuki jada do kowala miasta i wracaja), nie zakup towaru - swiadome odstepstwo od reguly
                // "z miasta do zamku tylko wozem", ale tylko gdy droga wozu jest otwarta: te same warunki co zamowienie (GarrisonCarts.MarketFor - miasto
                // handlowe, droga, odleglosc MarketMaxDistance, bez oblezenia i wojny); inaczej zamek bez napraw (nie ma kowali)
                Settlement mendAt = st;
                if (mp.IsGarrison && st.IsCastle && AiWear.BookOn) { float d; string why; mendAt = GarrisonCarts.MarketFor(st, out d, out why, false) ?? st; }
                AiWear.MendInTown(mp, mendAt);
                TryBuy(mp, st);
            }
            catch { }
        }

        internal static readonly ItemObject.ItemTypeEnum[] Order =
        {
            ItemObject.ItemTypeEnum.BodyArmor, ItemObject.ItemTypeEnum.OneHandedWeapon, ItemObject.ItemTypeEnum.TwoHandedWeapon,
            ItemObject.ItemTypeEnum.Polearm, ItemObject.ItemTypeEnum.Bow, ItemObject.ItemTypeEnum.Crossbow,
            ItemObject.ItemTypeEnum.Shield, ItemObject.ItemTypeEnum.HeadArmor, ItemObject.ItemTypeEnum.Arrows,
            ItemObject.ItemTypeEnum.Bolts, ItemObject.ItemTypeEnum.Thrown, ItemObject.ItemTypeEnum.LegArmor,
            ItemObject.ItemTypeEnum.HandArmor, ItemObject.ItemTypeEnum.Cape
        };

        // wpis 79: dla ColdStart (dorobek stuleci w zbrojowniach)
        internal static Dictionary<MBGUID, Dictionary<ItemObject, int>> Armories() { return Look() ? _armories.GetValue(null) as Dictionary<MBGUID, Dictionary<ItemObject, int>> : null; }
        internal static int Bucket(ItemObject it) { return (int)it.ItemType * 10 + TierOf(it); }
        /// <summary>K1 (przeglad): true tylko, gdy zbrojownia naprawde urosla o n - DTE AddItemToPartyArmory po cichu odrzuca sztuke z czarnej
        /// listy (blacklist.json: korony, suknie, dp_*) albo nierozpoznana; dotad wynik byl true, a sztuka (zaplacona albo zdjeta z ekranu) znikala.</summary>
        internal static bool AddToArmory(MobileParty mp, ItemObject it, int n)
        {
            try
            {
                if (!Look() || mp == null || it == null || n <= 0) return false;
                var all = _armories.GetValue(null) as Dictionary<MBGUID, Dictionary<ItemObject, int>>;
                long before = ArmTotal(all, mp.Id);
                _add.Invoke(null, new object[] { mp.Id, it, n });
                bool ok = ArmTotal(all, mp.Id) - before == n;
                if (!ok) { _dteRefused.Add(it); _dteOk.Remove(it); }   // poprawki sklad7: zapamietane na sesje - zakupy (BuyLoop, BuySubstitutes, MenUpgrade) juz jej nie biora
                return ok;
            }
            catch { return false; }
        }

        // poprawki sklad7 (petla "DTE nie przyjal"): zamek kupowal w miescie sztuke z czarnej listy DTE (suknia, korona, dp_*) do zamowienia wozem; przy dostawie
        // DTE jej nie przyjmowal (na polke zamku), brak zostawal otwarty i co GarrisonOrderDays dob ta sama sztuka byla kupowana znowu - sakiewka i pan
        // placili miastu, a suknie wedrowaly z polek miast na polki zamkow. Teraz przed zakupem: test DTE ItemBlackList.Test (refleksja) i zbior odmow sesji.
        private static MethodInfo _blTest;
        private static readonly HashSet<ItemObject> _dteRefused = new HashSet<ItemObject>(), _dteOk = new HashSet<ItemObject>();
        /// <summary>Ile roznych przedmiotow jest w zbiorze "DTE nie przyjmie" tej sesji (linia "Zaopatrzenie zamkow").</summary>
        internal static int DteRefusedIds { get { return _dteRefused.Count; } }

        /// <summary>Poprawki sklad7: czy zbrojownia DTE przyjmie te sztuke (do zbrojowni partii AI i zalog). Wynik testu raz na przedmiot i sesje
        /// (czarna lista DTE sie nie zmienia); blad testu - tak (nie blokujemy zakupow).</summary>
        internal static bool DteTakes(ItemObject it)
        {
            if (it == null) return false;
            if (_dteRefused.Contains(it)) return false;
            if (_dteOk.Contains(it)) return true;
            try
            {
                if (_blTest == null && !Look()) return true;
                if (_blTest != null && !(bool)_blTest.Invoke(null, new object[] { it })) { _dteRefused.Add(it); return false; }
                if (_blTest != null) _dteOk.Add(it);
            }
            catch { }
            return true;
        }

        private static long ArmTotal(Dictionary<MBGUID, Dictionary<ItemObject, int>> all, MBGUID id)
        {
            Dictionary<ItemObject, int> a; long t = 0;
            if (all == null || !all.TryGetValue(id, out a) || a == null) return 0;
            foreach (var v in a.Values) t += v;
            return t;
        }

        /// <summary>wpis 84: potrzeby partii w koszykach typ x tier (komplety ludzi, bez koni).</summary>
        internal static Dictionary<int, int> NeedBuckets(MobileParty mp)
        {
            var need = new Dictionary<int, int>();
            var roster = mp.MemberRoster;
            for (int i = 0; i < roster.Count; i++)
            {
                var el = roster.GetElementCopyAtIndex(i);
                var ch = el.Character;
                if (ch == null || ch.IsHero || el.Number <= 0) continue;
                Equipment eq = null;
                try { eq = ch.Equipment; } catch { }
                if (eq == null) continue;
                for (int sl = 0; sl < 10; sl++)
                {
                    var it = eq[(EquipmentIndex)sl].Item;
                    if (it == null || !SupplyDemand.Equipmentish(it)) continue;
                    int k = Bucket(it); int n; need.TryGetValue(k, out n); need[k] = n + el.Number;
                }
            }
            return need;
        }

        internal static int TierOf(ItemObject it)
        {
            try { return Math.Max(1, Math.Min(6, (int)it.Tier + 1)); } catch { return 1; }
        }

        /// <summary>
        /// sklad7 (scalenie 171 + K1c): JEDNA miara braku - koszyk potrzeby = typ x tier sztuki wzorca, ale najwyzej SUFIT JEDNOSTKI (K1c, Jeff 09.10 P1
        /// "ten sam sufit dla ludzi gracza i AI": SwapMath.CeilingTier - tier jednostki, z MenUpgradeOneTierUp o jeden wyzej). Ta sama miara w zakupach brakow
        /// (TryBuyCore), w cwiczeniach i linii "Pokrycie" (ArmsDrill) - brak, ktory zakupy uznaja za pokryty, cwiczenia tez.
        /// </summary>
        internal static int NeedKey(CharacterObject ch, ItemObject it, bool oneUp)
        {
            return (int)it.ItemType * 10 + Math.Min(TierOf(it), SwapMath.CeilingTier(MenUpgrade.TroopTier(ch), oneUp));
        }

        /// <summary>
        /// 171 (wydzielone z TryBuy): potrzeby wedle wzorcow (bez koni) minus zbrojownia - koszyki NeedKey (typ*10+tier do sufitu jednostki), po zastepstwie
        /// tierow (wpis 89: zapas w innym tierze tego samego typu pokrywa brak - najpierw wyzsze, potem t-1). needOut - potrzeba przed odjeciem (linia "Pokrycie");
        /// liftersOut - oddzialy koszyka (K1: kupiona sztuka musi pasowac komus z nich).
        /// </summary>
        internal static Dictionary<int, int> Deficit(MobileParty mp, Dictionary<ItemObject, int> armory, Dictionary<int, int> needOut = null, Dictionary<int, List<CharacterObject>> liftersOut = null)
        {
            var need = new Dictionary<int, int>();
            var s = Settings.Current;
            bool oneUp = s != null && s.MenUpgradeOneTierUp;
            var roster = mp.MemberRoster;
            for (int i = 0; i < roster.Count; i++)
            {
                var el = roster.GetElementCopyAtIndex(i);
                var ch = el.Character;
                if (ch == null || ch.IsHero || el.Number <= 0) continue;
                Equipment eq = null;
                try { eq = ch.Equipment; } catch { }
                if (eq == null) continue;
                // K1c (przeglad K1b, Jeff 09.10 P1): brak kupowany najwyzej do sufitu jednostki - koszyk wzorca ponad sufit liczy sie jako koszyk sufitu.
                // Dotad t4 z tarcza t6 we wzorcu (przed paczka 175) kupowal na brak t6. Ten sam tier w potrzebie i w pokryciu zapasem, wiec bez petli
                // "tier nizej" z wpisu 89 (sztuka sufitu pokrywa koszyk sufitu nazajutrz).
                for (int sl = 0; sl < 10; sl++)
                {
                    var it = eq[(EquipmentIndex)sl].Item;
                    if (it == null || !SupplyDemand.Equipmentish(it)) continue;
                    int k = NeedKey(ch, it, oneUp);
                    int n; need.TryGetValue(k, out n); need[k] = n + el.Number;
                    if (liftersOut != null)
                    {
                        List<CharacterObject> lt;
                        if (!liftersOut.TryGetValue(k, out lt)) liftersOut[k] = lt = new List<CharacterObject>();
                        if (!lt.Contains(ch)) lt.Add(ch);
                    }
                }
            }
            if (needOut != null) foreach (var kv in need) { int v; needOut.TryGetValue(kv.Key, out v); needOut[kv.Key] = v + kv.Value; }
            // wpis 89 (audyt): zapas w innym tierze tego samego typu pokrywa brak - najpierw wyzsze, potem t-1 (AiGear sam
            // kupuje t-1 na brak t); dotad brak t zostawal caly, lord co dzien dokupowal t-1, a MenPurse sprzedawal je jako nadwyzke
            var spare = new Dictionary<int, int>();
            if (armory != null)
                foreach (var kv in armory)
                {
                    if (kv.Key == null || kv.Value <= 0) continue;
                    int k = (int)kv.Key.ItemType * 10 + TierOf(kv.Key);
                    int n;
                    if (need.TryGetValue(k, out n)) { need[k] = n - kv.Value; if (need[k] < 0) { int sp0; spare.TryGetValue(k, out sp0); spare[k] = sp0 - need[k]; need[k] = 0; } }
                    else { int sp0; spare.TryGetValue(k, out sp0); spare[k] = sp0 + kv.Value; }
                }
            foreach (var k in new List<int>(need.Keys))
            {
                int d = need[k]; if (d <= 0) continue;
                int type = k / 10, t = k % 10;
                var order = new List<int>(); for (int tt = t + 1; tt <= 6; tt++) order.Add(tt); if (t > 1) order.Add(t - 1);
                foreach (var tt in order)
                {
                    int sk = type * 10 + tt, sp; if (!spare.TryGetValue(sk, out sp) || sp <= 0) continue;
                    int c = Math.Min(d, sp); d -= c; spare[sk] = sp - c; if (d <= 0) break;
                }
                need[k] = d;
            }
            return need;
        }

        /// <summary>171: dostawa kupionej partii sztuk (zloto i miejsce) - bucket = koszyk potrzeby, na ktory kupiono. sklad7 (K1): false - dostawa
        /// sie nie udala (DTE odrzucil sztuke z czarnej listy), BuyLoop oddaje sztuki na polke, nikt nie placi. Ceny hurtu (09.10): cost = suma cen
        /// n sztuk, kazda po swojej cenie (ShelfBuy.Take) - dotad cena jednej sztuki, mnozona przez n.</summary>
        private delegate bool Deliver(EquipmentElement el, int n, int bucket, int cost);
        private static bool _lastBudgetStop;   // ostatni BuyLoop: byl kandydat, ale za drogi na reszte budzetu (linia "Zaopatrzenie zamkow")

        /// <summary>
        /// 171 (wydzielone z TryBuy): zakup z polki targu wedle brakow. Kolejnosc typow Order, tiery 6..1, kandydaci t i t-1 bez unikatow,
        /// najlepsza skutecznosc do ceny; limit sztuk (pieces - licznik wspolny dla wywolan) i budzetu. Zwraca wydane zloto; need - co zostalo.
        /// K1 (przeglad): tylko sztuka, ktora udzwignie ktos z oddzialow koszyka (ItemReq - jak braki gracza i MenUpgrade); nadwyzki (MenPurse) oddaja
        /// najpierw sztuki, ktorych nikt nie udzwignie - bez tego warunku zaloga kupowala ciezka zbroje ponad swoja atletyke, a nazajutrz ja sprzedawala.
        /// </summary>
        private static int BuyLoop(Settlement market, MobileParty buyer, Dictionary<int, int> need, Dictionary<int, List<CharacterObject>> lifters, int budget, int maxPieces,
                                   ref int pieces, List<string> bought, Deliver deliver, int who)
        {
            int spent = 0;
            _lastBudgetStop = false;
            var shelf = market.ItemRoster;
            HashSet<ItemObject> refused = null;   // DTE nie przyjal (czarna lista) - nie kupujemy drugi raz
            // 174b.5 F2: cena stosu zapamietana do najblizszego zakupu (miedzy dwiema wycenami tego samego stosu bez zakupu polka, kiesa i ksiega sa te same;
            // zakup zmienia polke i kolejnosc stosow - wtedy cala pamiec od nowa); kolejnosc i wybor kandydatow bez zmian. Scalenie sklad8: kazda zmiana
            // polki (ShelfBuy.Take, takze cofnieta PutBack) - gen++
            int[] memo = new int[shelf.Count], memoGen = new int[shelf.Count];
            int gen = 1;
            foreach (var type in Order)
            {
                for (int t = 6; t >= 1; t--)
                {
                    int k = (int)type * 10 + t;
                    int deficit;
                    if (!need.TryGetValue(k, out deficit) || deficit <= 0) continue;
                    List<CharacterObject> lift = null; if (lifters != null) lifters.TryGetValue(k, out lift);
                    // kandydaci: ten sam typ, tier t albo t-1, bez unikatow; najlepsza skutecznosc do ceny
                    while (deficit > 0 && pieces < maxPieces && spent < budget)
                    {
                        int bestI = -1; float bestScore = 0f; int bestPrice = 0;
                        bool resHit = false;   // 174b.4: kandydat pominiety, bo zostala sama rezerwa kramu
                        for (int i = 0; i < shelf.Count; i++)
                        {
                            var el = shelf.GetElementCopyAtIndex(i);
                            var it = el.EquipmentElement.Item;
                            if (el.Amount <= 0 || it == null || it.ItemType != type) continue;
                            int ti = TierOf(it);
                            if (ti != t && ti != t - 1) continue;
                            if (ArmsPricing.IsUnique(it)) continue;
                            if (refused != null && refused.Contains(it)) continue;
                            if (!DteTakes(it)) continue;   // poprawki sklad7: takze zamowienie wozem (tam sztuka idzie do DTE dopiero przy dostawie)
                            if (!Lift(lift, it)) continue;
                            if (ShopReserve.Free(market, it) <= 0) { resHit = true; continue; }   // 174b.4: ostatnia sztuka pasma zostaje na straganie
                            int price;
                            if (i < memo.Length && memoGen[i] == gen) price = memo[i];
                            else { price = market.Town.MarketData.GetPrice(el.EquipmentElement, buyer, false, market.Party); if (i < memo.Length) { memo[i] = price; memoGen[i] = gen; } }
                            if (price <= 0) continue;
                            if (price > budget - spent) { _lastBudgetStop = true; continue; }
                            float eff = it.Effectiveness > 0f ? it.Effectiveness : 1f;
                            float score = eff / price;
                            if (score > bestScore) { bestScore = score; bestI = i; bestPrice = price; }
                        }
                        if (bestI < 0) { if (resHit) Measure174b.NoteHeld(who, 1); break; }
                        var pick = shelf.GetElementCopyAtIndex(bestI);
                        var pel = pick.EquipmentElement;
                        int maxN = Math.Min(Math.Min(deficit, pick.Amount), maxPieces - pieces);
                        // 174b.4: rezerwa kramu ogranicza partie; licznik "zatrzymane" jak w 174b - szacunek po cenie pierwszej sztuki (tylko licznik)
                        { int fr = ShopReserve.Free(market, pel.Item); if (fr < maxN) { Measure174b.NoteHeld(who, Math.Min(maxN, (budget - spent) / bestPrice) - Math.Max(0, fr)); maxN = Math.Max(0, fr); } }
                        // ceny hurtu (Jeff 09.10 08:00): kazda sztuka po swojej cenie - po zdjeciu sztuki z polki nastepna wyceniana od nowa (ShelfBuy, jak
                        // gra w SellItemsAction); dotad cena pierwszej sztuki x n. Pierwsza - cena z przegladu polki (ta sama polka, bez drugiej wyceny).
                        int cost, first, last;
                        int n = ShelfBuy.Take(shelf, pel, maxN, budget - spent, () => market.Town.MarketData.GetPrice(pel, buyer, false, market.Party), out cost, out first, out last, bestPrice);
                        if (n <= 0) break;
                        gen++;   // 174b.5 F2: polka sie zmienila - pamiec cen od nowa
                        if (!deliver(pel, n, k, cost))
                        {
                            ShelfBuy.PutBack(shelf, pel, n, cost, first);   // K1 (przeglad): DTE odrzucil - sztuka wraca na polke, nikt nie placi
                            if (refused == null) refused = new HashSet<ItemObject>();
                            refused.Add(pel.Item);
                            continue;
                        }
                        ArmsScrap.NoteBuy(market, pel.Item, n);   // 174 pytanie 4: popyt koszyka w miescie (tylko licznik)
                        if (type == ItemObject.ItemTypeEnum.Arrows || type == ItemObject.ItemTypeEnum.Bolts) TownFletchers.NoteBought(type, n);   // 172: linia strzelarzy (tylko licznik)
                        spent += cost; pieces += n; deficit -= n;
                        if (bought.Count < 6) bought.Add(pel.Item.StringId + " " + ShelfBuy.Prices(n, first, last));
                    }
                    need[k] = deficit;   // wpis 67: co zostalo niezaspokojone
                }
            }
            return spent;
        }

        /// <summary>K1 (przeglad): czy ktos z oddzialow koszyka udzwignie sztuke (brak listy - bez warunku, jak dotad).</summary>
        private static bool Lift(List<CharacterObject> who, ItemObject it)
        {
            if (who == null || who.Count == 0) return true;
            foreach (var c in who) if (ItemReq.Meets(c, it)) return true;
            return false;
        }

        /// <summary>Czy na polce lezy sztuka typu i tieru (t albo t-1), bez unikatow - "za drogie" to nie brak towaru (wpis 81). 174b.4: tylko sztuki PONAD
        /// rezerwe kramu - gdy zostala sama rezerwa, zamowienie dla kowali idzie jak przy pustej polce.</summary>
        private static bool OnShelf(Settlement market, ItemObject.ItemTypeEnum ty, int tr)
        {
            var shelf = market.ItemRoster;
            for (int i = 0; i < shelf.Count; i++)
            {
                var el = shelf.GetElementCopyAtIndex(i); var it = el.EquipmentElement.Item;
                if (el.Amount > 0 && it != null && it.ItemType == ty && !ArmsPricing.IsUnique(it)) { int ti = TierOf(it); if ((ti == tr || ti == tr - 1) && ShopReserve.Free(market, it) > 0) return true; }
            }
            return false;
        }

        /// <summary>
        /// K1 (A5): najpierw braki (A6, TryBuyCore), potem lepsze za swoje (A7, MenUpgrade - wlasna bramka raz na dobe). 174.0: oba w ramce ksiegi towarow
        /// "zakupy uzbrojenia Armoury" - w ticku dobowym partii nie licza sie jako "inne ticki" (tylko licznik).
        /// </summary>
        internal static void TryBuy(MobileParty mp, Settlement st)
        {
            // 174b.5 F5: tanie filtry przed ramka ksiegi (ramka bez AddToCounts rozlicza sie na zero - ksiega ta sama). Scalenie sklad8: WouldBuy
            // filtruje tylko braki (TryBuyCore); lepsze za swoje (MenUpgrade.ForAi) maja wlasne warunki i bramke doby - wolane jak w sklad7
            bool core = WouldBuy(mp, st);
            if (!core && !MenUpgrade.On) return;
            var gf = GoodsLedger.Begin(GoodsLedger.FArmsBuy, mp);
            try
            {
                if (core)
                {
                    long tc = Cost174.Begin(Cost174.STryBuy);
                    try { TryBuyCore(mp, st); }
                    finally { Cost174.End(Cost174.STryBuy, tc); }
                }
                MenUpgrade.ForAi(mp, st);
            }
            finally { GoodsLedger.End(gf); }
        }

        /// <summary>174b.5 F5: te same wczesne wyjscia co TryBuyCore az do stempla doby (bez stempla i bez zmian stanu) - false = TryBuyCore i tak nic nie zrobi.
        /// Scalenie sklad8: zaloga gracza jak w TryBuyCore sklad7 - w systemie takze przez sama sakiewke zalogi (GarrisonPurseEnabled), bez GarrisonBuysGearPlayer.</summary>
        private static bool WouldBuy(MobileParty mp, Settlement st)
        {
            try
            {
                if (!On || mp == null || st == null) return false;
                var s0 = Settings.Current;
                bool garrison = mp.IsGarrison && mp.CurrentSettlement == st && s0.GarrisonBuysGear;
                Hero payer = garrison ? (st.OwnerClan != null ? st.OwnerClan.Leader : null) : mp.LeaderHero;
                bool gPurse = garrison && MenUpgrade.GarrisonPurseOn;
                bool lordPays = !(garrison && payer == Hero.MainHero && !s0.GarrisonBuysGearPlayer);
                if (garrison && !lordPays && !gPurse) return false;
                if (mp.IsMainParty || (!mp.IsLordParty && !garrison) || payer == null || !payer.IsAlive || !mp.IsActive || mp.MapEvent != null) return false;
                if ((!st.IsTown && !st.IsCastle) || st.ItemRoster == null || st.Town == null) return false;
                if (!garrison && st.IsCastle && GarrisonCarts.On) return false;
                int last;
                if (_lastDay.TryGetValue(mp, out last) && last == (int)CampaignTime.Now.ToDays) return false;
                return true;
            }
            catch { return true; }   // w razie watpliwosci - jak dotad (TryBuyCore ma swoj try)
        }

        private static void TryBuyCore(MobileParty mp, Settlement st)
        {
            try
            {
                if (!On || mp == null || st == null || !Look()) return;
                // wpis 57 (Jeff 04.10: "a co z garnizonem, skad oni maja miec bron?"): garnizon kupuje brakujacy sprzet na targu
                // SWOJEJ osady, zloto idzie do kasy miasta (jak u partii lorda)
                // K1 (A6): placi NAJPIERW sakiewka zalogi (jej zold), potem kiesa pana osady; Twoja kiesa doplaca do Twoich zalog tylko
                // przy GarrisonBuysGearPlayer (dotad ten wylacznik zatrzymywal zakupy Twoich zalog w ogole)
                var s0 = Settings.Current;
                bool garrison = mp.IsGarrison && mp.CurrentSettlement == st && s0.GarrisonBuysGear;
                Hero payer = garrison ? (st.OwnerClan != null ? st.OwnerClan.Leader : null) : mp.LeaderHero;
                bool gPurse = garrison && MenUpgrade.GarrisonPurseOn;
                bool lordPays = !(garrison && payer == Hero.MainHero && !s0.GarrisonBuysGearPlayer);
                if (garrison && !lordPays && !gPurse) return;
                if (mp.IsMainParty || (!mp.IsLordParty && !garrison) || payer == null || !payer.IsAlive || !mp.IsActive || mp.MapEvent != null) return;
                if ((!st.IsTown && !st.IsCastle) || st.ItemRoster == null || st.Town == null) return;
                if (FactionManager.IsAtWarAgainstFaction(mp.MapFaction, st.MapFaction)) return;
                // 171 C2.3 (Z6): zamek nie jest targiem broni - lord kupuje tylko w miastach; PRZED stemplem doby, wiec wizyta w zamku
                // nie zabiera mu zakupu tego dnia w miescie
                bool castleCart = garrison && st.IsCastle && GarrisonCarts.On;
                if (!garrison && st.IsCastle && GarrisonCarts.On) return;
                int day = (int)CampaignTime.Now.ToDays;
                int last;
                if (_lastDay.TryGetValue(mp, out last) && last == day) return;
                _lastDay[mp] = day;
                if (_dayStamp != day) { FlushDay(); _dayStamp = day; }

                var s = Settings.Current;
                var lord = payer;
                int reserve = Math.Max(0, s.AiGearGoldReserve);
                double p = Math.Max(0f, Math.Min(100f, s.AiGearBudgetPercent)) / 100.0;
                int budget = lordPays ? (int)((lord.Gold - reserve) * p) : 0;
                int maxPieces = Math.Max(1, s.AiGearMaxPiecesPerVisit);
                // 171 C6 (krytyka 4): zaloga zamku zamawia w miescie raz na D dob - zamowienie obejmuje wszystkie te doby: budzet 1-(1-p)^D kiesy
                // ponad rezerwe (tyle, ile wydalyby D dziennych zakupow, nie wiecej) i limit sztuk x D; polka wlasnego zamku - codziennie jak dotad
                bool orderDay = false;
                if (castleCart)
                {
                    orderDay = GarrisonCarts.CanOrderToday(st);
                    if (orderDay)
                    {
                        int D = Math.Max(1, s.GarrisonOrderDays);
                        if (lordPays) budget = (int)((lord.Gold - reserve) * (1.0 - Math.Pow(1.0 - p, D)));
                        maxPieces = maxPieces * D;
                    }
                    else GarrisonCarts.NotePause();
                }
                // wpis 84: ludzie dokupuja braki ze swojej sakiewki (lup), dopiero potem kiesa lorda; K1: zaloga - ze swojej sakiewki (zold)
                int purse = (!garrison || gPurse) ? Math.Max(0, MenPurse.Get(mp) - AiWear.OutstandingCost(mp, st)) : 0;   // wpis 89: naprawy maja pierwszenstwo (z materialem - szacunek z polki miasta)
                budget = Math.Max(0, budget) + purse;
                if (budget <= 0) return;
                if (orderDay) GarrisonCarts.MarkTried(st);   // jedno zamowienie (i jeden budzet D dob) na D dob, takze gdy proba sie nie uda

                var all = _armories.GetValue(null) as Dictionary<MBGUID, Dictionary<ItemObject, int>>;
                Dictionary<ItemObject, int> armory = null;
                if (all != null) all.TryGetValue(mp.Id, out armory);

                // potrzeby wedle wzorcow (bez koni) i stan zbrojowni - koszyki typ*10+tier (do sufitu jednostki), z oddzialami koszyka (udzwig)
                var needOut = new Dictionary<int, int>();
                var lifters = new Dictionary<int, List<CharacterObject>>();
                var need = Deficit(mp, armory, needOut, lifters);
                if (castleCart) GarrisonCarts.SubtractTransit(st, need);   // 171 C5: towar w drodze - inaczej zamek co dobe zamawialby to samo

                int pieces = 0;
                var bought = new List<string>();
                int lordPart = 0;   // poprawki sklad7: ile dolozyl pan (Twoja zaloga - Twoja kiesa: komunikat w grze)
                // zaplata: najpierw sakiewka ludzi (lord AI zawsze, zaloga przy GarrisonPurseEnabled), reszta kiesa pana; zwraca czesc z sakiewki
                Func<int, int> pay = cost =>
                {
                    int fromPurse = (!garrison || gPurse) ? MenPurse.Take(mp, cost) : 0;
                    lord.ChangeHeroGold(-(cost - fromPurse));
                    lordPart += cost - fromPurse;
                    if (garrison) MenUpgrade.NoteGarrisonGap(fromPurse);
                    return fromPurse;
                };
                int who = garrison ? (st.IsTown ? Measure174b.BTownGarrison : Measure174b.BCastleOwn) : Measure174b.BLord;   // 174b.0 M2: kupujacy (tylko licznik)
                // polka tej osady (zaloga zamku: wlasnego zamku - to, co tam lezy, oplacila juz kasa zamku i jest na miejscu)
                Deliver here = (el, n, k, cost) =>
                {
                    if (!AddToArmory(mp, el.Item, n)) return false;   // K1 (przeglad): DTE odrzucil - nikt nie placi
                    Measure174b.NoteBuySum(who, el.Item, n, cost);   // 174b.0 M2 (tylko licznik); ceny hurtu: suma cen sztuk
                    AiWear.NoteBought(mp, el, n);   // wpis 89: zuzyta z polki zostaje zuzyta; 171/K1 (recenzje): takze w zalodze
                    MenUpgrade.NoteChurn(mp, el.Item, true);
                    pay(cost);   // ceny hurtu (09.10): cost - suma cen sztuk (ShelfBuy), nie cena pierwszej x n
                    st.Town.ChangeGold(cost);
                    MoneyLedger.Note(MoneyLedger.NGear, st, cost);   // ksiega przeplywow osad (tylko licznik)
                    return true;
                };
                long tl = Cost174.Begin(Cost174.SBuyLoop);
                int spent = BuyLoop(st, mp, need, lifters, budget, maxPieces, ref pieces, bought, here, who);
                Cost174.End(Cost174.SBuyLoop, tl);
                // 174 pytanie 2: gorszy sprzet zamiast zadnego - tylko z polki tej osady, tylko do pokrycia "dowolnego szczebla"
                if (SubstituteMeleeOn || SubstituteBodyOn)
                {
                    Dictionary<ItemObject, int> armNow = null;
                    if (all != null) all.TryGetValue(mp.Id, out armNow);
                    spent += BuySubstitutes(st, mp, armNow, needOut, need, lifters, budget - spent, maxPieces, ref pieces, bought, here, who);
                }
                string where = st.Name.ToString();
                if (castleCart)
                {
                    if (pieces > 0) GarrisonCarts.NoteOwnShelf(pieces, spent);
                    // 171 C2.4: reszte brakow zaloga zamku zamawia w miescie swoich wsi - zloto (sakiewka zalogi, potem pan) do kasy miasta, towar schodzi
                    // z polki miasta w chwili zakupu i jedzie do zamku (GarrisonCarts); zamowienia dla warsztatow - w tym miescie, wedle JEGO polki (krytyka 15)
                    bool left = false; foreach (var v in need.Values) if (v > 0) { left = true; break; }
                    if (orderDay && left)
                    {
                        float dist; string why;
                        var market = GarrisonCarts.MarketFor(st, out dist, out why);
                        if (market != null)
                        {
                            var lines = new List<GarrisonCarts.Line>();
                            int paid = 0, pursePaid = 0;
                            bool budgetStop = true;
                            long to = Cost174.Begin(Cost174.SCastleOrder);   // 174b.5 F6 (probka 1/16, tylko log)
                            // recenzja 171: zamowienie powstaje takze, gdy BuyLoop rzuci wyjatek po kilku zakupach (np. cena z modelu innego moda) -
                            // oplacone sztuki zdjete z polki nie moga zniknac; wydane = suma oplaconych linii (to samo, co zwraca BuyLoop)
                            try
                            {
                                if (budget - spent > 0)
                                {
                                    BuyLoop(market, mp, need, lifters, budget - spent, maxPieces, ref pieces, bought, (el, n, k, cost) =>
                                    {
                                        int fromPurse = pay(cost);   // ceny hurtu (09.10): cost - suma cen sztuk (ShelfBuy)
                                        Measure174b.NoteBuySum(Measure174b.BCastleOrder, el.Item, n, cost);   // 174b.0 M2 (tylko licznik)
                                        market.Town.ChangeGold(cost);
                                        MoneyLedger.Note(MoneyLedger.NGear, market, cost);
                                        lines.Add(new GarrisonCarts.Line { El = el, N = n, Bucket = k });
                                        paid += cost; pursePaid += fromPurse;
                                        return true;
                                    }, Measure174b.BCastleOrder);
                                    budgetStop = _lastBudgetStop;
                                }
                            }
                            finally
                            {
                                Cost174.End(Cost174.SCastleOrder, to);
                                spent += paid;
                                if (lines.Count > 0) { GarrisonCarts.Place(st, market, st.OwnerClan, lines, paid, dist, pieces >= maxPieces, budgetStop, pursePaid); where += " i zamowienie z " + market.Name; }
                            }
                            int unmet = 0;
                            foreach (var kv in need)
                            {
                                if (kv.Value <= 0) continue;
                                var ty = (ItemObject.ItemTypeEnum)(kv.Key / 10); int tr = kv.Key % 10;
                                if (!OnShelf(market, ty, tr)) { SupplyDemand.NoteUnmetOnce(mp, market, ty, tr, Math.Min(10, kv.Value)); unmet++; }
                            }
                            if (unmet > 0) GarrisonCarts.NoteUnmet(unmet);
                        }
                    }
                }
                else
                {
                    // wpis 67: czego nie bylo na polce - zamowienie w tym miescie (najwyzej po 10 na rodzaj z jednej wizyty)
                    // wpis 81: tylko gdy na polce NIE MA zadnej sztuki tego typu i tieru (t albo t-1) - "za drogie" to nie brak
                    // towaru, tylko brak zlota kupca; i jeden kupiec raz na SupplyDemandOrderRepeatDays (garnizon liczy co dzien)
                    var shelf = st.ItemRoster;
                    foreach (var kv in need)
                    {
                        if (kv.Value <= 0) continue;
                        var ty = (ItemObject.ItemTypeEnum)(kv.Key / 10); int tr = kv.Key % 10;
                        if (!OnShelf(st, ty, tr)) SupplyDemand.NoteUnmetOnce(mp, st, ty, tr, Math.Min(10, kv.Value));
                    }
                }
                if (pieces <= 0) return;
                // poprawki sklad7: Twoja kiesa nie zmienia sie bez slowa - tylko gdy Twoja kiesa doplacila (GarrisonBuysGearPlayer); raz na dobe na zaloge (stempel _lastDay)
                if (garrison && lord == Hero.MainHero && lordPart > 0)
                    Log.Player("Your garrison of " + st.Name + " bought " + pieces + " pieces of kit for " + spent + " denars (their purse " + (spent - lordPart) + ", your coin " + lordPart + ").");
                _dayPieces += pieces; _dayGold += spent; _dayVisits++; if (garrison) { _dayGarrison++; _dayGarrisonGold += spent; }
                if (_dayLogged < Math.Max(0, s.AiGearLogPerDay))
                {
                    _dayLogged++;
                    Log.Info("ZakupyAI: " + (garrison ? "garnizon " + st.Name + " (placi " + lord.Name + (gPurse ? " i sakiewka zalogi" : "") + ")" : lord.Name.ToString()) + " (" + mp.MemberRoster.TotalManCount + " ludzi) w " + where + ": " + pieces
                             + " szt. za " + spent + " (budzet " + budget + ", zloto pana po zakupach " + lord.Gold + ", sakiewka " + MenPurse.Get(mp) + "); np. "
                             + string.Join(", ", bought.ToArray()) + ".");
                }
            }
            catch (Exception e) { Log.Error("AiGear.TryBuy", e); }
        }

        private static int _dayGarrison, _dayGarrisonGold;

        private static void FlushDay()
        {
            if (_dayStamp >= 0 && (_dayVisits > 0))
                Log.Info("ZakupyAI: dzien " + _dayStamp + " - " + _dayVisits + " wizyt, " + _dayPieces + " szt. kupionych za " + _dayGold + " zlota; w tym garnizony " + _dayGarrison + " zakupow za " + _dayGarrisonGold
                         + "; zastepcze (174, pytanie 2): bron biala " + _daySubMelee + (SubstituteMeleeOn ? "" : " (wylaczone)") + ", zbroja na tulow " + _daySubBody + (SubstituteBodyOn ? "" : " (wylaczone)") + (_subStumbles > 0 ? "; potkniecia zastepczych " + _subStumbles : "") + ".");
            _daySubMelee = 0; _daySubBody = 0; _subStumbles = 0;
            _dayPieces = 0; _dayGold = 0; _dayVisits = 0; _dayLogged = 0; _dayGarrison = 0; _dayGarrisonGold = 0;
        }
    }
}
