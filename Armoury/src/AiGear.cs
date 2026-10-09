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
    /// </summary>
    internal static class AiGear
    {
        internal static void Reset() { _lastDay.Clear(); }
        private static Type _dte;
        private static FieldInfo _armories;
        private static MethodInfo _add;
        private static bool _looked;
        private static readonly Dictionary<MobileParty, int> _lastDay = new Dictionary<MobileParty, int>();
        private static int _dayPieces, _dayGold, _dayVisits, _dayLogged, _dayStamp = -1;

        internal static bool On { get { var s = Settings.Current; return s != null && s.AiBuysGear; } }

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
        internal static void OnSettlementEntered(MobileParty mp, Settlement st, Hero hero) { TryBuy(mp, st); }

        internal static void OnDailyTickParty(MobileParty mp)
        {
            try { if (mp != null && mp.CurrentSettlement != null) { AiWear.MendInTown(mp, mp.CurrentSettlement); TryBuy(mp, mp.CurrentSettlement); } } catch { }
        }

        private static readonly ItemObject.ItemTypeEnum[] Order =
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
        internal static bool AddToArmory(MobileParty mp, ItemObject it, int n)
        {
            try { if (!Look() || n <= 0) return false; _add.Invoke(null, new object[] { mp.Id, it, n }); return true; } catch { return false; }
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

        private static int TierOf(ItemObject it)
        {
            try { return Math.Max(1, Math.Min(6, (int)it.Tier + 1)); } catch { return 1; }
        }

        /// <summary>
        /// 171 (wydzielone z TryBuy bez zmian): potrzeby wedle wzorcow (bez koni) minus zbrojownia - koszyki typ*10+tier, po zastepstwie tierow
        /// (wpis 89: zapas w innym tierze tego samego typu pokrywa brak - najpierw wyzsze, potem t-1). needOut - potrzeba przed odjeciem (linia "Pokrycie").
        /// </summary>
        internal static Dictionary<int, int> Deficit(MobileParty mp, Dictionary<ItemObject, int> armory, Dictionary<int, int> needOut = null)
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
                    int k = (int)it.ItemType * 10 + TierOf(it);
                    int n; need.TryGetValue(k, out n); need[k] = n + el.Number;
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

        /// <summary>171: dostawa kupionej partii sztuk (zloto i miejsce) - bucket = koszyk potrzeby, na ktory kupiono.</summary>
        private delegate void Deliver(EquipmentElement el, int n, int bucket, int unitPrice);
        private static bool _lastBudgetStop;   // ostatni BuyLoop: byl kandydat, ale za drogi na reszte budzetu (linia "Zaopatrzenie zamkow")

        /// <summary>
        /// 171 (wydzielone z TryBuy bez zmian): zakup z polki targu wedle brakow. Kolejnosc typow Order, tiery 6..1, kandydaci t i t-1 bez unikatow,
        /// najlepsza skutecznosc do ceny; limit sztuk (pieces - licznik wspolny dla wywolan) i budzetu. Zwraca wydane zloto; need - co zostalo.
        /// </summary>
        private static int BuyLoop(Settlement market, MobileParty buyer, Dictionary<int, int> need, int budget, int maxPieces, ref int pieces, List<string> bought, Deliver deliver)
        {
            int spent = 0;
            _lastBudgetStop = false;
            var shelf = market.ItemRoster;
            foreach (var type in Order)
            {
                for (int t = 6; t >= 1; t--)
                {
                    int k = (int)type * 10 + t;
                    int deficit;
                    if (!need.TryGetValue(k, out deficit) || deficit <= 0) continue;
                    // kandydaci: ten sam typ, tier t albo t-1, bez unikatow; najlepsza skutecznosc do ceny
                    while (deficit > 0 && pieces < maxPieces && spent < budget)
                    {
                        int bestI = -1; float bestScore = 0f; int bestPrice = 0;
                        for (int i = 0; i < shelf.Count; i++)
                        {
                            var el = shelf.GetElementCopyAtIndex(i);
                            var it = el.EquipmentElement.Item;
                            if (el.Amount <= 0 || it == null || it.ItemType != type) continue;
                            int ti = TierOf(it);
                            if (ti != t && ti != t - 1) continue;
                            if (ArmsPricing.IsUnique(it)) continue;
                            int price = market.Town.MarketData.GetPrice(el.EquipmentElement, buyer, false, market.Party);
                            if (price <= 0) continue;
                            if (price > budget - spent) { _lastBudgetStop = true; continue; }
                            float eff = it.Effectiveness > 0f ? it.Effectiveness : 1f;
                            float score = eff / price;
                            if (score > bestScore) { bestScore = score; bestI = i; bestPrice = price; }
                        }
                        if (bestI < 0) break;
                        var pick = shelf.GetElementCopyAtIndex(bestI);
                        int n = Math.Min(deficit, pick.Amount);
                        n = Math.Min(n, maxPieces - pieces);
                        n = Math.Min(n, (budget - spent) / bestPrice);
                        if (n <= 0) break;
                        shelf.AddToCounts(pick.EquipmentElement, -n);
                        deliver(pick.EquipmentElement, n, k, bestPrice);
                        spent += bestPrice * n; pieces += n; deficit -= n;
                        if (bought.Count < 6) bought.Add(pick.EquipmentElement.Item.StringId + " " + bestPrice);
                    }
                    need[k] = deficit;   // wpis 67: co zostalo niezaspokojone
                }
            }
            return spent;
        }

        /// <summary>Czy na polce lezy sztuka typu i tieru (t albo t-1), bez unikatow - "za drogie" to nie brak towaru (wpis 81).</summary>
        private static bool OnShelf(ItemRoster shelf, ItemObject.ItemTypeEnum ty, int tr)
        {
            for (int i = 0; i < shelf.Count; i++)
            {
                var el = shelf.GetElementCopyAtIndex(i); var it = el.EquipmentElement.Item;
                if (el.Amount > 0 && it != null && it.ItemType == ty && !ArmsPricing.IsUnique(it)) { int ti = TierOf(it); if (ti == tr || ti == tr - 1) return true; }
            }
            return false;
        }

        private static void TryBuy(MobileParty mp, Settlement st)
        {
            try
            {
                if (!On || mp == null || st == null || !Look()) return;
                // wpis 57 (Jeff 04.10: "a co z garnizonem, skad oni maja miec bron?"): garnizon kupuje brakujacy sprzet na targu
                // SWOJEJ osady, placi pan osady ze swojej kiesy, zloto idzie do kasy miasta (jak u partii lorda)
                var s0 = Settings.Current;
                bool garrison = mp.IsGarrison && mp.CurrentSettlement == st && s0.GarrisonBuysGear;
                Hero payer = garrison ? (st.OwnerClan != null ? st.OwnerClan.Leader : null) : mp.LeaderHero;
                if (garrison && payer == Hero.MainHero && !s0.GarrisonBuysGearPlayer) return;
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
                int budget = (int)((lord.Gold - reserve) * Math.Max(0f, Math.Min(100f, s.AiGearBudgetPercent)) / 100f);
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
                        double p = Math.Max(0f, Math.Min(100f, s.AiGearBudgetPercent)) / 100.0;
                        budget = (int)((lord.Gold - reserve) * (1.0 - Math.Pow(1.0 - p, D)));
                        maxPieces = maxPieces * D;
                    }
                    else GarrisonCarts.NotePause();
                }
                // wpis 84: ludzie dokupuja braki ze swojej sakiewki (lup), dopiero potem kiesa lorda
                int purse = garrison ? 0 : Math.Max(0, MenPurse.Get(mp) - AiWear.OutstandingCost(mp, st));   // wpis 89: naprawy maja pierwszenstwo (z materialem - szacunek z polki miasta)
                budget = Math.Max(0, budget) + purse;
                if (budget <= 0) return;
                if (orderDay) GarrisonCarts.MarkTried(st);   // jedno zamowienie (i jeden budzet D dob) na D dob, takze gdy proba sie nie uda

                var all = _armories.GetValue(null) as Dictionary<MBGUID, Dictionary<ItemObject, int>>;
                Dictionary<ItemObject, int> armory = null;
                if (all != null) all.TryGetValue(mp.Id, out armory);

                // potrzeby wedle wzorcow (bez koni) i stan zbrojowni - koszyki typ*10+tier
                var need = Deficit(mp, armory);
                if (castleCart) GarrisonCarts.SubtractTransit(st, need);   // 171 C5: towar w drodze - inaczej zamek co dobe zamawialby to samo

                int pieces = 0;
                var bought = new List<string>();
                // polka tej osady (zaloga zamku: wlasnego zamku - to, co tam lezy, oplacila juz kasa zamku i jest na miejscu)
                int spent = BuyLoop(st, mp, need, budget, maxPieces, ref pieces, bought, (el, n, k, unit) =>
                {
                    _add.Invoke(null, new object[] { mp.Id, el.Item, n });
                    AiWear.NoteBought(mp, el, n);   // wpis 89: zuzyta z polki zostaje zuzyta; 171 (recenzja): takze w zalodze - inaczej obita sztuka wychodzila z zalogi jako sprawna
                    int cost = unit * n, fromPurse = garrison ? 0 : MenPurse.Take(mp, cost);
                    lord.ChangeHeroGold(-(cost - fromPurse));
                    st.Town.ChangeGold(unit * n);
                    MoneyLedger.Note(MoneyLedger.NGear, st, unit * n);   // ksiega przeplywow osad (tylko licznik)
                });
                if (castleCart)
                {
                    if (pieces > 0) GarrisonCarts.NoteOwnShelf(pieces, spent);
                    // 171 C2.4: reszte brakow zaloga zamku zamawia w miescie swoich wsi - zloto pana do kasy miasta, towar schodzi z polki miasta
                    // w chwili zakupu i jedzie do zamku (GarrisonCarts); zamowienia dla warsztatow - w tym miescie, wedle JEGO polki (krytyka 15)
                    bool left = false; foreach (var v in need.Values) if (v > 0) { left = true; break; }
                    if (orderDay && left)
                    {
                        float dist; string why;
                        var market = GarrisonCarts.MarketFor(st, out dist, out why);
                        if (market != null)
                        {
                            var lines = new List<GarrisonCarts.Line>();
                            int paid = 0;
                            bool budgetStop = true;
                            // recenzja 171: zamowienie powstaje takze, gdy BuyLoop rzuci wyjatek po kilku zakupach (np. cena z modelu innego moda) -
                            // oplacone sztuki zdjete z polki nie moga zniknac; wydane = suma oplaconych linii (to samo, co zwraca BuyLoop)
                            try
                            {
                                if (budget - spent > 0)
                                {
                                    BuyLoop(market, mp, need, budget - spent, maxPieces, ref pieces, bought, (el, n, k, unit) =>
                                    {
                                        lord.ChangeHeroGold(-unit * n);
                                        market.Town.ChangeGold(unit * n);
                                        MoneyLedger.Note(MoneyLedger.NGear, market, unit * n);
                                        lines.Add(new GarrisonCarts.Line { El = el, N = n, Bucket = k });
                                        paid += unit * n;
                                    });
                                    budgetStop = _lastBudgetStop;
                                }
                            }
                            finally
                            {
                                spent += paid;
                                if (lines.Count > 0) GarrisonCarts.Place(st, market, st.OwnerClan, lines, paid, dist, pieces >= maxPieces, budgetStop);
                            }
                            int unmet = 0;
                            foreach (var kv in need)
                            {
                                if (kv.Value <= 0) continue;
                                var ty = (ItemObject.ItemTypeEnum)(kv.Key / 10); int tr = kv.Key % 10;
                                if (!OnShelf(market.ItemRoster, ty, tr)) { SupplyDemand.NoteUnmetOnce(mp, market, ty, tr, Math.Min(10, kv.Value)); unmet++; }
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
                        if (!OnShelf(shelf, ty, tr)) SupplyDemand.NoteUnmetOnce(mp, st, ty, tr, Math.Min(10, kv.Value));
                    }
                }
                if (pieces <= 0) return;
                _dayPieces += pieces; _dayGold += spent; _dayVisits++; if (garrison) { _dayGarrison++; _dayGarrisonGold += spent; }
                if (_dayLogged < Math.Max(0, s.AiGearLogPerDay))
                {
                    _dayLogged++;
                    Log.Info("ZakupyAI: " + (garrison ? "garnizon " + st.Name + " (placi " + lord.Name + ")" : lord.Name.ToString()) + " (" + mp.MemberRoster.TotalManCount + " ludzi) w " + st.Name + ": " + pieces
                             + " szt. za " + spent + " (budzet " + budget + ", zloto " + (lord.Gold + spent) + " -> " + lord.Gold + "); np. "
                             + string.Join(", ", bought.ToArray()) + ".");
                }
            }
            catch (Exception e) { Log.Error("AiGear.TryBuy", e); }
        }

        private static int _dayGarrison, _dayGarrisonGold;

        private static void FlushDay()
        {
            if (_dayStamp >= 0 && (_dayVisits > 0))
                Log.Info("ZakupyAI: dzien " + _dayStamp + " - " + _dayVisits + " wizyt, " + _dayPieces + " szt. kupionych za " + _dayGold + " zlota; w tym garnizony " + _dayGarrison + " zakupow za " + _dayGarrisonGold + ".");
            _dayPieces = 0; _dayGold = 0; _dayVisits = 0; _dayLogged = 0; _dayGarrison = 0; _dayGarrisonGold = 0;
        }
    }
}
