using System;
using System.Collections;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>
    /// KON DLA RYCERZA, wedle slow Jeffa: "jak awansuje na konnice, to musi
    /// byc kon - im wyzszy poziom, tym lepszy rumak - i AI tez, warunki
    /// globalne". DTE wycina vanilla wymog wierzchowca (kategoria = null),
    /// wiec konnica rodzila sie z powietrza. Przywracamy go i skalujemy:
    /// awans na jezdnego = kon w TABORZE druzyny, jeden na czlowieka,
    /// znika przy awansie. Tier niski - zwykly kon; wyzej - kon bojowy;
    /// najwyzej - rumak szlachetny. Gracza pilnuje ekran druzyny (vanilla
    /// sam sprawdza i zabiera konie), AI - nasze latki na awans w polu:
    /// lord bez koni w taborze nie wystawi jazdy (konie bierze z lupow).
    /// </summary>
    internal static class Stables
    {
        /// <summary>
        /// Getter CharacterObject.UpgradeRequiresItemFromCategory: DTE zeruje go
        /// prefixem, my dopisujemy postfixem (postfix biegnie PO prefiksach),
        /// wiec ostatnie slowo nalezy do stajni.
        /// </summary>
        public static void RanksNeedHorses(CharacterObject __instance, ref ItemCategory __result)
        {
            try
            {
                var c = Settings.Current;
                if (c == null || !c.CavalryNeedsMounts) return;
                if (__instance == null || __instance.IsHero || !__instance.IsMounted) return;
                int tier = (int)__instance.Tier;
                if (tier >= c.NobleHorseFromTier) __result = DefaultItemCategories.NobleHorse;
                else if (tier >= c.WarHorseFromTier) __result = DefaultItemCategories.WarHorse;
                else __result = DefaultItemCategories.Horse;
            }
            catch { }
        }

        private static int CountInRoster(PartyBase party, ItemCategory cat)
        {
            int n = 0;
            try
            {
                var r = party.ItemRoster;
                for (int i = 0; i < r.Count; i++)
                {
                    var el = r[i];
                    var it = el.EquipmentElement.Item;
                    if (it != null && it.ItemCategory == cat) n += el.Amount;
                }
            }
            catch { }
            return n;
        }

        /// <summary>Najtansze konie ida pod siodlo pierwsze - lepsze rumaki czekaja na wyzsze awanse.</summary>
        private static void Consume(PartyBase party, ItemCategory cat, int count)
        {
            try
            {
                var r = party.ItemRoster;
                while (count > 0)
                {
                    int best = -1; int bestVal = int.MaxValue;
                    for (int i = 0; i < r.Count; i++)
                    {
                        var el = r[i];
                        var it = el.EquipmentElement.Item;
                        if (it == null || it.ItemCategory != cat || el.Amount <= 0) continue;
                        if (it.Value < bestVal) { bestVal = it.Value; best = i; }
                    }
                    if (best < 0) return;
                    var elBest = r[best];
                    int take = Math.Min(count, elBest.Amount);
                    r.AddToCounts(elBest.EquipmentElement, -take);
                    count -= take;
                }
            }
            catch { }
        }

        /// <summary>
        /// AI, krok 1 (lista celow awansu): cel na jezdnego bez ani jednego
        /// wlasciwego konia w taborze ODPADA (oddzial z rozwidleniem drzewka
        /// naturalnie skreca w piechote); przy garstce koni liczba awansow
        /// zostaje przycieta do stanu stajni.
        /// </summary>
        public static void FilterTargets(PartyBase party, object __result)
        {
            try
            {
                var c = Settings.Current;
                if (c == null || !c.CavalryNeedsMounts) return;
                var list = __result as IList;
                if (list == null || list.Count == 0 || party == null) return;
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    var boxed = list[i];
                    if (boxed == null) continue;
                    var tr = Traverse.Create(boxed);
                    var target = tr.Field("UpgradeTarget").GetValue<CharacterObject>();
                    if (target == null) continue;
                    ItemCategory cat = null;
                    try { cat = target.UpgradeRequiresItemFromCategory; } catch { }
                    if (cat == null) continue;
                    int have = CountInRoster(party, cat);
                    if (have <= 0) { list.RemoveAt(i); continue; }
                    int need = tr.Field("PossibleUpgradeCount").GetValue<int>();
                    if (need <= have) continue;
                    // stajnia na czesc awansow: przytnij liczbe do stanu koni
                    var ctor = boxed.GetType().GetConstructor(new[]
                    {
                        typeof(CharacterObject), typeof(CharacterObject),
                        typeof(int), typeof(int), typeof(int), typeof(float)
                    });
                    if (ctor == null) continue;
                    list[i] = ctor.Invoke(new object[]
                    {
                        tr.Field("Target").GetValue<CharacterObject>(), target, have,
                        tr.Field("UpgradeGoldCost").GetValue<int>(),
                        tr.Field("UpgradeXpCost").GetValue<int>(),
                        tr.Field("UpgradeChance").GetValue<float>()
                    });
                }
            }
            catch (Exception e) { Log.Error("Stables.Filter", e); }
        }

        /// <summary>
        /// AI, krok 2 (sam awans): konie znikaja z taboru, sztuka za czlowieka.
        /// Gdyby w miedzyczasie stajnia oprozniala (inny oddzial tej samej
        /// druzyny zdazyl wybrac konie) - awans w ogole nie zachodzi.
        /// </summary>
        public static bool PayInHorses(PartyBase party, object upgradeArgs)
        {
            try
            {
                var c = Settings.Current;
                if (c == null || !c.CavalryNeedsMounts) return true;
                if (party == null || upgradeArgs == null) return true;
                var tr = Traverse.Create(upgradeArgs);
                var target = tr.Field("UpgradeTarget").GetValue<CharacterObject>();
                if (target == null) return true;
                ItemCategory cat = null;
                try { cat = target.UpgradeRequiresItemFromCategory; } catch { }
                if (cat == null) return true;
                int need = tr.Field("PossibleUpgradeCount").GetValue<int>();
                if (need <= 0) return true;
                if (CountInRoster(party, cat) < need) return false;   // konie wybrane - czekaja (XP zostaje)
                Consume(party, cat, need);
                return true;
            }
            catch (Exception e) { Log.Error("Stables.Pay", e); return true; }
        }

        // ------------------------------------------------------------ stajnia AI
        /// <summary>
        /// LORD KUPUJE KONIE, wedle slow Jeffa: "nie no maja miec konie, czemu
        /// mieliby nie miec, i tez moga kupowac". Sama zasada "awans na jezdnego
        /// wymaga rumaka" powoli rozbroilaby konnice AI - bo lord bral konie
        /// wylacznie z lupow. Odtad kazdy lord, ktory wjezdza do osady, uzupelnia
        /// stajnie: najpierw z tamtejszego targu (uczciwy handel - konie schodza
        /// z rynku, zloto idzie do miasta), a gdy na targu pusto, zamawia je
        /// u miejscowego hodowcy z narzutem. Placi z wlasnej sakiewki i nigdy
        /// nie wydaje na to wiecej niz czesc majatku.
        /// </summary>
        // kiedy ktora druzyna ostatnio uzupelniala stajnie (dzien kampanii)
        private static readonly System.Collections.Generic.Dictionary<string, double> _lastBuy =
            new System.Collections.Generic.Dictionary<string, double>();

        internal static void OnSettlementEntered(MobileParty party, TaleWorlds.CampaignSystem.Settlements.Settlement settlement)
        {
            try
            {
                var c = Settings.Current;
                if (c == null || !c.CavalryNeedsMounts || !c.AiBuysMounts) return;
                if (party == null || settlement == null || party.IsMainParty) return;
                if (!party.IsLordParty || party.LeaderHero == null) return;
                if (!settlement.IsTown && !settlement.IsVillage) return;

                // KONIOKRADZTWO GOSPODARCZE: bez przerwy miedzy zakupami powstawala
                // pompa - lord kupowal konie, AI sprzedawalo je jako zwykly towar
                // przy nastepnym postoju, lord kupowal znowu. W jeden dzien gry
                // przez ten obieg przeplynely miliony. Stajnie uzupelnia sie
                // najwyzej raz na kilka dni i najwyzej o garstke sztuk naraz.
                double today = CampaignTime.Now.ToDays;
                double last;
                string pid = party.StringId ?? "";
                if (_lastBuy.TryGetValue(pid, out last)
                    && today - last < Math.Max(0f, c.AiMountBuyCooldownDays)) return;

                var lord = party.LeaderHero;

                // PO CO TE KONIE? Wylacznie po to, zeby awansowac ludzi, ktorzy
                // WLASNIE czekaja na awans na jezdnego - z wysluzonym
                // doswiadczeniem i bez rumaka w taborze. Pierwsza wersja kupowala
                // procent STANU OSOBOWEGO, wiec lord z pieciuset piechurami brał
                // sto koni "na zapas", odsprzedawal je jako zwykly towar
                // i kupowal znowu. Teraz liczymy glowy, nie procenty: nikt nie
                // czeka na awans - nikt nie kupuje ani jednego konia.
                int want = NeedForUpgrades(party.Party);
                if (want <= 0) return;
                want += Math.Max(0, c.AiMountSpareBuffer);      // kilka luzem na straty
                int have = CountAnyMounts(party.Party);
                int need = want - have;
                if (need <= 0) return;
                int cap = Math.Max(1, c.AiMountMaxPerVisit);
                if (need > cap) need = cap;              // jeden postoj to kilka koni, nie stado

                int purse = lord.Gold;
                int budget = (int)(purse * MBMath.ClampFloat(c.AiMountPurseShare, 0f, 1f));
                if (budget < 50) return;

                int bought = 0, paid = 0;
                // 1. z targu osady - najtansze najpierw
                var shelf = settlement.ItemRoster;
                while (need > 0 && shelf != null)
                {
                    int best = -1, bestPrice = int.MaxValue;
                    for (int i = 0; i < shelf.Count; i++)
                    {
                        var el = shelf[i];
                        var it = el.EquipmentElement.Item;
                        if (it == null || el.Amount <= 0 || !IsPlainMount(it)) continue;
                        int price = PriceOf(settlement, el.EquipmentElement);
                        if (price < bestPrice) { bestPrice = price; best = i; }
                    }
                    if (best < 0 || bestPrice > budget - paid) break;
                    var chosen = shelf[best];
                    int take = Math.Min(need, chosen.Amount);
                    if (take * bestPrice > budget - paid) take = Math.Max(1, (budget - paid) / Math.Max(1, bestPrice));
                    if (take <= 0) break;
                    shelf.AddToCounts(chosen.EquipmentElement, -take);
                    party.ItemRoster.AddToCounts(chosen.EquipmentElement, take);
                    paid += take * bestPrice; bought += take; need -= take;
                }

                // 2. targ pusty, a lord potrzebuje - zamawia u hodowcy (narzut za fatyge)
                if (need > 0 && c.AiMountBreederFallback)
                {
                    var nag = CheapestMount(settlement);
                    if (nag != null)
                    {
                        int price = (int)(nag.Value * Math.Max(1f, c.AiMountBreederMarkup));
                        while (need > 0 && paid + price <= budget)
                        {
                            party.ItemRoster.AddToCounts(nag, 1);
                            paid += price; bought++; need--;
                        }
                    }
                }

                if (bought <= 0) return;
                _lastBuy[pid] = today;
                TaleWorlds.CampaignSystem.Actions.GiveGoldAction.ApplyForCharacterToSettlement(lord, settlement, paid);
                Log.Info("Stajnia AI: " + lord.Name + " kupil " + bought + " koni w " + settlement.Name
                         + " za " + paid + " (czekalo na awans " + (want - Math.Max(0, c.AiMountSpareBuffer))
                         + ", mial " + have + ").");
            }
            catch (Exception e) { Log.Error("Stables.AiBuy", e); }
        }

        /// <summary>
        /// Ilu ludzi w tej druzynie STOI GOTOWYCH do awansu na jezdnego i czeka
        /// tylko na rumaka. Liczymy tak samo, jak liczy sama gra: zdrowi z tego
        /// szczebla, przyciecie przez zebrane doswiadczenie (Xp / koszt awansu),
        /// i tylko te sciezki awansu, ktore wymagaja konia.
        /// </summary>
        private static int NeedForUpgrades(PartyBase party)
        {
            int need = 0;
            try
            {
                var r = party.MemberRoster;
                if (r == null) return 0;
                for (int i = 0; i < r.Count; i++)
                {
                    var el = r.GetElementCopyAtIndex(i);
                    var ch = el.Character;
                    if (ch == null || ch.IsHero) continue;
                    int healthy = el.Number - el.WoundedNumber;
                    if (healthy <= 0) continue;
                    var targets = ch.UpgradeTargets;
                    if (targets == null || targets.Length == 0) continue;

                    int bestForThisRank = 0;
                    for (int t = 0; t < targets.Length; t++)
                    {
                        var tg = targets[t];
                        if (tg == null) continue;
                        ItemCategory cat = null;
                        try { cat = tg.UpgradeRequiresItemFromCategory; } catch { }
                        if (cat == null) continue;                 // ta sciezka nie potrzebuje konia
                        int ready = healthy;
                        try
                        {
                            int xpCost = ch.GetUpgradeXpCost(party, t);
                            if (xpCost > 0) ready = Math.Min(healthy, el.Xp / xpCost);
                        }
                        catch { }
                        if (ready > bestForThisRank) bestForThisRank = ready;
                    }
                    need += bestForThisRank;   // jeden czlowiek = jeden kon, nie po jednym na sciezke
                }
            }
            catch { }
            return need;
        }

        /// <summary>Wierzchowiec pod siodlo - nie juczny mul i nie bydlo.</summary>
        private static bool IsPlainMount(ItemObject it)
        {
            try
            {
                if (it == null || it.ItemType != ItemObject.ItemTypeEnum.Horse) return false;
                // ROT daje sloniowi item_category="horse" - stajnia kupowala
                // lordom SLONIE jako zwykle konie i tak trafialy na Polnoc
                // (Jeff 29.08: "skad slonie na polnocy?!"). Smoki tak samo.
                var id = it.StringId ?? "";
                if (id == "elephant" || id.StartsWith("rot_elephant") || id.StartsWith("dragon_")) return false;
                var hc = it.HorseComponent;
                if (hc == null || !hc.IsMount) return false;
                var cat = it.ItemCategory;
                return cat == DefaultItemCategories.Horse || cat == DefaultItemCategories.WarHorse
                    || cat == DefaultItemCategories.NobleHorse;
            }
            catch { return false; }
        }

        private static int CountAnyMounts(PartyBase party)
        {
            int n = 0;
            try
            {
                var r = party.ItemRoster;
                for (int i = 0; i < r.Count; i++)
                    if (IsPlainMount(r[i].EquipmentElement.Item)) n += r[i].Amount;
            }
            catch { }
            return n;
        }

        private static int PriceOf(TaleWorlds.CampaignSystem.Settlements.Settlement st, EquipmentElement el)
        {
            try
            {
                if (st.Town != null) return Math.Max(1, st.Town.GetItemPrice(el, null, false));
            }
            catch { }
            return Math.Max(1, el.Item != null ? el.Item.Value : 1);
        }

        /// <summary>Najtanszy koń pod siodlo, jakiego zna swiat - z kultury osady, jak sie da.</summary>
        private static ItemObject _cheapAny;

        private static ItemObject CheapestMount(TaleWorlds.CampaignSystem.Settlements.Settlement st)
        {
            try
            {
                ItemObject local = null, any = null;
                foreach (var it in TaleWorlds.ObjectSystem.MBObjectManager.Instance.GetObjectTypeList<ItemObject>())
                {
                    if (!IsPlainMount(it) || it.NotMerchandise || it.Value <= 0) continue;
                    if (it.ItemCategory != DefaultItemCategories.Horse) continue;      // hodowca ma zwykle konie
                    if (any == null || it.Value < any.Value) any = it;
                    if (st.Culture != null && it.Culture == st.Culture
                        && (local == null || it.Value < local.Value)) local = it;
                }
                _cheapAny = any;
                // miejscowa rasa TYLKO, jesli nie zdziera - inaczej hodowca w Braavos
                // liczyl 1500 za sztuke, bo najtanszy kon jego kultury tyle wart
                if (local != null && any != null && local.Value > any.Value * 1.4f) return any;
                return local ?? any;
            }
            catch { return _cheapAny; }
        }

        internal static void ApplyAll(Harmony h)
        {
            try
            {
                var c = Settings.Current;
                if (c == null || !c.CavalryNeedsMounts) { Log.Info("Stables: wylaczone."); return; }

                var g = AccessTools.PropertyGetter(typeof(CharacterObject), "UpgradeRequiresItemFromCategory");
                if (g == null) { Log.Info("Stables: brak gettera UpgradeRequiresItemFromCategory."); return; }
                h.Patch(g, postfix: new HarmonyMethod(typeof(Stables), "RanksNeedHorses") { priority = Priority.Last });

                var tU = typeof(TaleWorlds.CampaignSystem.CampaignBehaviors.PartyUpgraderCampaignBehavior);
                var mList = AccessTools.Method(tU, "GetPossibleUpgradeTargets");
                var mUp = AccessTools.Method(tU, "UpgradeTroop");
                if (mList != null) h.Patch(mList, postfix: new HarmonyMethod(typeof(Stables), "FilterTargets"));
                if (mUp != null) h.Patch(mUp, prefix: new HarmonyMethod(typeof(Stables), "PayInHorses"));

                Log.Info("Stables: awans na jezdnego wymaga rumaka - gracz przez ekran druzyny (vanilla sam zabiera konie), "
                         + "AI przez tabor (lista=" + (mList != null) + ", zaplata=" + (mUp != null) + "). "
                         + "Kon bojowy od tieru " + c.WarHorseFromTier + ", szlachetny od " + c.NobleHorseFromTier + ".");
            }
            catch (Exception e) { Log.Error("Stables.ApplyAll", e); }
        }
    }
}
