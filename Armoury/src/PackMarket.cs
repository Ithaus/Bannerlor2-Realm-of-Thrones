using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// E6/K5 (213, PLAN 6.16, R9/R13; projekt etapu 6 v2 rozdz. 3 "E6/K5"; krytyka K1-07, KB K5-udzwig-i-kolejnosc) - JUCZNE NOWEJ KARAWANY Z TARGU; UDZWIG LUZAKA
    /// POD JUKAMI. Klucze PackAnimalsFromMarket (a) i SpareHorseCarryKg (c; 20 = jak w grze).
    /// (a) Gra daje nowej karawanie z niczego najtansze zwierze juczne x (int)(0.5 x ludzie) (G CaravanPartyComponent.cs:71-82, InitializationArgs.InitializeCaravanOnCreation,
    ///     tylko gdy CreateCaravanParty nie dostal ladunku). Postfiks InitializeCaravanOnCreation zapamietuje, co gra dodala (partia swieza - roster pusty przed),
    ///     postfiks CreateCaravanParty (Priority.VeryLow - jawnie po E4/8 ArmsFromPeople.CaravanPost Low; finalizery CaravanCapital.CreateFin i O17 CapFin biegna
    ///     PO nim) zdejmuje te sztuki i kupuje juczne na targu miasta startu z kapitalu karawany (PartyTradeGold ustawiony w metodzie przez InitializePartyTrade -
    ///     K5 widzi kapital po CapPostfix/A3; rozliczenie ceny BK z CreateFin przychodzi pozniej): cel = min(dodane przez gre, 0.5 x ludzie po E4/8), najtansze
    ///     juczne z polki najpierw, kazda sztuka po swojej cenie (ShelfBuy), budzet = polowa kapitalu (jak gra w BuyCategory: PartyTradeGold x 0.5), zaplata
    ///     GiveGoldAction partia -> osada. Brak (pusta polka, kapital 0 - karawana gracza dostaje zloto po zaplacie, CaravanCapital.CtxFin; miasto oblezone albo nie
    ///     miasto) - mniej jucznych; dokupi je gra przy handlu (CaravansCampaignBehavior.BuyGoods: juczne do liczby ludzi, przy zlocie > 1000) - takze z targu.
    ///     Konwoj DTE (kontekst CaravanCapital KDte - kapital 0, nie handluje) - juczne gry zostaja (ladunek konwoju DTE z niczego, poza K5). Karawana morska
    ///     (bez drogi ladowej) - juczne zdjete bez zakupu (gra nie kupuje im jucznych, na morzu nie nosza). Karawany startowe (isInitialSpawn, przed GameStarted) -
    ///     jak w grze (stan wyjsciowy ksiegi koni). Juczny przechodzi z polki do taboru - przeniesienie w "Konie (E6/0)" (suma bez zmian), zloto z kasy karawany
    ///     do kasy miasta. Poza K5: BK GiveMounts/GivePackAnimals (3.11 - osobna paczka) i ladunek konwoju DTE (AddReinforcementSupplies: 17 jucznych i zywnosc; juczne gry konwoju zostaja).
    /// (b) Zapasowe konie lordow = zapas Stables.Reserve z E6/K2 (osobnego klucza nie ma - to bylaby trzecia definicja zapasu); jedza jak kazdy kon (pasza K0).
    /// (c) Udzwig: gra daje 20 kg za kazdego luzaka (NumberOfMounts, "Spare Mounts", G DefaultInventoryCapacityModel.cs:78). Konie partii (luzaki + wolne konie
    ///     zbrojowni przy SpareHorsesCarryFootmen) niosa najpierw piechure, potem jencow (kolejnosc MarchPace.Cap), reszta niesie juki; udzwig w modelu gry maja tylko
    ///     luzaki taboru, wiec luzakow pod jukami = min(luzaki, konie - piesi - jency). Postfiks modelu dopisuje (SpareHorseCarryKg - 20) za kazdego takiego
    ///     luzaka (60 kg ZAMIAST 20); luzak pod piechurem zostaje przy 20 kg gry. Tylko na ladzie (jak gra). Lancuch: BK VanillaModelTweakPatches dopisuje 20 kg
    ///     za juczne (perk) - inny skladnik; MarketRoad.CartPostfix - tylko tabory wsi (mnoznik wozu dziala tez na nasz dopisek - dopisek jest w bazie). Postfiks
    ///     tylko czyta (watki tiku partii): rostery, stan koni partii K0 (ConcurrentDictionary). Linia liczy luzaki raz na dobe po wszystkich partiach.
    /// Linia doby "Juczne (213)".
    /// </summary>
    internal static class PackMarket
    {
        private const float BudgetShare = 0.5f;   // jak gra w CaravansCampaignBehavior.BuyCategory: najwyzej polowa PartyTradeGold na kategorie
        private const int GameSpareKg = 20;       // gra: luzak x 2 x 10 kg (DefaultInventoryCapacityModel, SpareMountsFactor x _itemAverageWeight)

        internal static bool On { get { var s = Settings.Current; return s != null && s.PackAnimalsFromMarket; } }
        private static bool Live { get { var c = Campaign.Current; return c != null && c.GameStarted; } }

        private static readonly TextObject _txtPack = new TextObject("{=!}Spare horses under packs");

        // co gra dodala nowej karawanie (InitializeCaravanOnCreation) - zdejmowane w postfiksie CreateCaravanParty tej samej partii
        private static readonly Dictionary<MobileParty, List<KeyValuePair<EquipmentElement, int>>> _pend = new Dictionary<MobileParty, List<KeyValuePair<EquipmentElement, int>>>();
        private static FieldInfo _itemsField;

        // liczniki doby
        private static long _calls, _new, _given, _removed, _target, _bought, _paid, _short, _start, _startN, _off, _offN, _orphan, _orphanN;
        private static long _full, _part, _none, _naval, _navalN, _zeroCap, _noTown, _emptyShelf, _dte, _dteN;
        private static int _stumbles, _stumblesDay;
        private static readonly HashSet<string> _errSites = new HashSet<string>();
        private static string _hooks = "nie wpiete";

        internal static void Reset()
        {
            _pend.Clear();
            DayReset();
            _stumbles = 0;
            _errSites.Clear();
        }

        private static void DayReset()
        {
            _calls = _new = _given = _removed = _target = _bought = _paid = _short = _start = _startN = _off = _offN = _orphan = _orphanN = 0;
            _full = _part = _none = _naval = _navalN = _zeroCap = _noTown = _emptyShelf = _dte = _dteN = 0;
            _stumblesDay = 0;
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++; _stumblesDay++;
            if (_errSites.Add(where)) Log.Error("PackMarket." + where, e);
        }

        private static bool IsPack(ItemObject it) { return it != null && it.ItemCategory == DefaultItemCategories.PackAnimal && it.HorseComponent != null; }

        // ------------------------------------------------------------ (a) juczne nowej karawany

        /// <summary>Postfiks InitializationArgs.InitializeCaravanOnCreation: zwierzeta juczne, ktore gra dodala swiezej karawanie (bez ladunku z CreateCaravanParty).</summary>
        public static void InitPost(object __instance, MobileParty __0)
        {
            try
            {
                if (__0 == null || __0.ItemRoster == null || __instance == null) return;
                if (_itemsField != null && _itemsField.GetValue(__instance) != null) return;   // ladunek z wywolania - gra nie dodaje jucznych
                var r = __0.ItemRoster;
                List<KeyValuePair<EquipmentElement, int>> got = null;
                for (int i = 0; i < r.Count; i++)
                {
                    var el = r.GetElementCopyAtIndex(i);
                    if (el.Amount <= 0 || !IsPack(el.EquipmentElement.Item)) continue;
                    if (got == null) got = new List<KeyValuePair<EquipmentElement, int>>(1);
                    got.Add(new KeyValuePair<EquipmentElement, int>(el.EquipmentElement, el.Amount));
                }
                if (got != null) _pend[__0] = got;
            }
            catch (Exception e) { Stumble("InitPost", e); }
        }

        /// <summary>Postfiks CreateCaravanParty (VeryLow): zdjecie jucznych gry i zakup na targu miasta startu z kapitalu karawany.</summary>
        public static void CreatePost(Settlement __1, bool __3, MobileParty __result)
        {
            if (__result == null) return;
            List<KeyValuePair<EquipmentElement, int>> got;
            if (!_pend.TryGetValue(__result, out got)) return;
            _pend.Remove(__result);
            try
            {
                int added = 0;
                foreach (var kv in got) added += kv.Value;
                _calls++;
                if (!Live || __3) { _start++; _startN += added; return; }   // karawany startowe - jak w grze (stan wyjsciowy)
                if (!On) { _off++; _offN += added; return; }
                // konwoj DTE (CaravanCapital: kapital 0, nie handluje) - juczne gry zostaja: bez nich ladunek DTE (17 z kazdej zywnosci, 17 jucznych) przeciazalby konwoj,
                // a caly ladunek konwoju jest z niczego po stronie DTE (poza K5)
                if (CaravanCapital.InDteConvoy()) { _dte++; _dteN += added; return; }
                var mp = __result;
                var r = mp.ItemRoster;
                _new++; _given += added;
                foreach (var kv in got)
                {
                    int at = r.FindIndexOfElement(kv.Key);
                    int have = at >= 0 ? r.GetElementNumber(at) : 0;
                    int take = Math.Min(kv.Value, have);
                    if (take <= 0) continue;
                    r.AddToCounts(kv.Key, -take);
                    _removed += take;
                }
                if (!mp.HasLandNavigationCapability) { _naval++; _navalN += added; return; }   // karawana morska - bez zakupu
                int men = mp.MemberRoster != null ? mp.MemberRoster.TotalManCount : 0;
                int target = Math.Min(added, (int)(men * 0.5f));                                // regula gry na zalodze po E4/8
                if (target <= 0) return;
                _target += target;
                int bought = 0, paid = 0;
                int gold = mp.PartyTradeGold;
                var st = __1;
                if (gold <= 0) _zeroCap++;
                else if (st == null || !st.IsTown || st.Town == null || st.ItemRoster == null || st.SiegeEvent != null || (st.Party != null && st.Party.MapEvent != null)) _noTown++;
                else
                {
                    bought = Buy(mp, st, target, (int)Math.Min(int.MaxValue, (long)(gold * BudgetShare)), out paid);
                    if (bought <= 0) _emptyShelf++;
                }
                if (bought >= target) _full++; else if (bought > 0) _part++; else _none++;
                _bought += bought; _paid += paid; _short += target - bought;
            }
            catch (Exception e) { Stumble("CreatePost", e); }
        }

        /// <summary>Zakup jucznych z polki miasta: najtansze najpierw, kazda sztuka po swojej cenie (ShelfBuy), zaplata z kasy karawany do kasy miasta.</summary>
        private static int Buy(MobileParty mp, Settlement st, int need, int budget, out int paid)
        {
            paid = 0;
            int bought = 0;
            var shelf = st.ItemRoster;
            var town = st.Town;
            for (int guard = 0; guard < 8 && need > 0 && budget - paid > 0; guard++)
            {
                int best = -1, bestPrice = int.MaxValue;
                for (int i = 0; i < shelf.Count; i++)
                {
                    var el = shelf.GetElementCopyAtIndex(i);
                    if (el.Amount <= 0 || !IsPack(el.EquipmentElement.Item)) continue;
                    int pr = town.GetItemPrice(el.EquipmentElement, mp, false);
                    if (pr > 0 && pr < bestPrice) { bestPrice = pr; best = i; }
                }
                if (best < 0 || bestPrice > budget - paid) break;
                var cel = shelf.GetElementCopyAtIndex(best).EquipmentElement;
                int cost, first, last;
                int took = ShelfBuy.Take(shelf, cel, need, budget - paid, () => town.GetItemPrice(cel, mp, false), out cost, out first, out last, bestPrice, false);
                if (took <= 0) break;
                mp.ItemRoster.AddToCounts(cel, took);
                paid += cost; bought += took; need -= took;
            }
            if (paid > 0) GiveGoldAction.ApplyForPartyToSettlement(mp.Party, st, paid, true);
            return bought;
        }

        // ------------------------------------------------------------ (c) udzwig luzaka pod jukami

        /// <summary>Luzaki taboru pod jukami: konie partii (luzaki + wolne konie zbrojowni przy K0) najpierw pod piechote, potem pod jencow, reszta pod juki;
        /// udzwig gry maja tylko luzaki taboru - wynik najwyzej luzaki. mounts - luzaki taboru (licznik gry, z K0 takze ze stanem).</summary>
        internal static int UnderPacks(MobileParty mp, bool k0, out int mounts)
        {
            mounts = 0;
            var pb = mp.Party;
            if (pb == null) return 0;
            mounts = pb.NumberOfMounts;
            if (mounts <= 0) return 0;
            int foot = pb.NumberOfMenWithoutHorse;
            int pris = mp.PrisonRoster != null ? mp.PrisonRoster.TotalManCount : 0;
            int arm = k0 ? HorseState.FreeOf(mp) : 0;
            int free = mounts + arm - foot - pris;
            return free <= 0 ? 0 : Math.Min(free, mounts);
        }

        /// <summary>Postfiks DefaultInventoryCapacityModel.CalculateInventoryCapacity: (SpareHorseCarryKg - 20) kg za luzaka pod jukami (tylko na ladzie; tylko odczyt).</summary>
        public static void CapPostfix(MobileParty __0, bool __1, bool __6, ref ExplainedNumber __result)
        {
            try
            {
                var s = Settings.Current;
                if (s == null || __0 == null || __1) return;
                int extra = s.SpareHorseCarryKg - GameSpareKg;
                if (extra <= 0) return;
                bool k0 = s.SpareHorsesCarryFootmen;
                int m, n = UnderPacks(__0, k0, out m);
                if (__6)
                {
                    var att = __0.AttachedParties;
                    if (att != null) for (int i = 0; i < att.Count; i++) { var ap = att[i]; if (ap != null) n += UnderPacks(ap, k0, out m); }
                }
                if (n > 0) __result.Add((float)n * extra, _txtPack);
            }
            catch { }
        }

        // ------------------------------------------------------------ wpiecia

        internal static void ApplyAll(Harmony h)
        {
            var parts = new List<string>();
            try
            {
                var inner = AccessTools.Inner(typeof(CaravanPartyComponent), "InitializationArgs");
                var init = inner != null ? AccessTools.Method(inner, "InitializeCaravanOnCreation") : null;
                _itemsField = inner != null ? AccessTools.Field(inner, "CaravanItems") : null;
                var create = AccessTools.Method(typeof(CaravanPartyComponent), "CreateCaravanParty");
                if (init == null || create == null || _itemsField == null) parts.Add("BRAK InitializationArgs.InitializeCaravanOnCreation / CaravanItems / CreateCaravanParty - juczne karawan jak w grze");
                else
                {
                    h.Patch(init, postfix: new HarmonyMethod(typeof(PackMarket), nameof(InitPost)));
                    h.Patch(create, postfix: new HarmonyMethod(typeof(PackMarket), nameof(CreatePost)) { priority = Priority.VeryLow });
                    parts.Add("juczne nowych karawan wpiete (CreateCaravanParty VeryLow)");
                }
            }
            catch (Exception e) { parts.Add("blad wpiecia jucznych " + e.GetType().Name); Log.Error("PackMarket.ApplyAll(juczne)", e); }
            try
            {
                var cap = AccessTools.Method(typeof(DefaultInventoryCapacityModel), "CalculateInventoryCapacity");
                if (cap == null) parts.Add("BRAK DefaultInventoryCapacityModel.CalculateInventoryCapacity - udzwig luzaka jak w grze");
                else { h.Patch(cap, postfix: new HarmonyMethod(typeof(PackMarket), nameof(CapPostfix))); parts.Add("udzwig luzaka wpiety"); }
            }
            catch (Exception e) { parts.Add("blad wpiecia udzwigu " + e.GetType().Name); Log.Error("PackMarket.ApplyAll(udzwig)", e); }
            _hooks = string.Join(", ", parts.ToArray());
            var s = Settings.Current;
            Log.Info("PackMarket (E6/K5, 213): " + _hooks + "; klucze PackAnimalsFromMarket (teraz " + (On ? "TAK" : "NIE") + "), SpareHorseCarryKg (teraz "
                     + (s != null ? s.SpareHorseCarryKg.ToString(CultureInfo.InvariantCulture) : "?") + ").");
        }

        // ------------------------------------------------------------ linia doby

        /// <summary>Linia "Juczne (213)": nowe karawany i ich juczne z doby, luzaki pod jukami i pod piechurem (spis wszystkich partii na ladzie); zeruje liczniki.</summary>
        internal static void Daily()
        {
            var inv = CultureInfo.InvariantCulture;
            var s = Settings.Current;
            if (s == null) return;
            int day = (int)CampaignTime.Now.ToDays;
            // partie, ktorych InitializeCaravanOnCreation nie przeszlo przez CreateCaravanParty (inna droga tworzenia) - juczne gry zostaly (z niczego)
            foreach (var kv in _pend) { _orphan++; foreach (var e in kv.Value) _orphanN += e.Value; }
            _pend.Clear();
            int extra = Math.Max(0, s.SpareHorseCarryKg - GameSpareKg);
            bool k0 = s.SpareHorsesCarryFootmen;
            long under = 0, foot = 0, parties = 0;
            try
            {
                foreach (var mp in MobileParty.All)
                {
                    if (mp == null || !mp.IsActive || mp.IsCurrentlyAtSea) continue;
                    int m, u = UnderPacks(mp, k0, out m);
                    if (m <= 0) continue;
                    under += u; foot += m - u;
                    if (u > 0) parties++;
                }
            }
            catch (Exception e) { Stumble("Daily", e); }
            long left = (_given - _removed) + _orphanN;
            Log.Info("Juczne (213): dzien " + day + " | " + (On ? "CZYNNE" : "WYLACZONE (Pack Animals From Market)")
                     + " | nowe karawany " + _new + " (na sucho z jucznymi gry " + (_new + _off) + " - zakup nie wstrzymuje tworzenia; startowe " + _start + " z " + _startN
                     + " jucznymi gry - stan wyjsciowy; przy wylaczonym kluczu " + _off + " z " + _offN + ")"
                     + " kupily " + _bought + " za " + _paid.ToString(inv) + " (cel " + _target + "; pelne " + _full + ", czesciowo " + _part + ", bez jucznych " + _none + ")"
                     + ", z niczego " + left + " (gra dala " + _given + ", zdjete " + _removed + "; poza CreateCaravanParty " + _orphan + " partii, " + _orphanN + " sztuk)"
                     + ", brak " + _short + " (kapital 0 przy tworzeniu - karawany gracza " + _zeroCap + "; nie miasto albo oblezone " + _noTown + "; polka bez jucznych w budzecie " + _emptyShelf
                     + "; dokupi gra przy handlu z targu) | morskie " + _naval + " (zdjete " + _navalN + " bez zakupu) | konwoje DTE " + _dte + " - juczne gry zostaja " + _dteN + " (poza K5)"
                     + " | udzwig (Spare Horse Carry Kg " + s.SpareHorseCarryKg + "): luzakow pod jukami " + under + " (+" + extra + " kg = " + (under * extra).ToString(inv) + " kg, partii " + parties + ")"
                     + ", pod piechurem albo jencem " + foot + " (20 kg gry)" + (k0 ? "" : " - bez wolnych koni zbrojowni (K0 wylaczone)")
                     + " | poza K5: BK GiveMounts/GivePackAnimals (3.11), ladunek konwojow DTE | wpiecia: " + _hooks
                     + " | potkniecia dzis " + _stumblesDay + " (razem " + _stumbles + ").");
            DayReset();
        }
    }
}
