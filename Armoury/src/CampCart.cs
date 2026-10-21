using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Threading;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Workshops;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// m97-oboz (Jeff 20.10, decyzja 3 po raporcie obozu): WOZ TABOROWY + KON JUCZNY i SIODLA TRANSPORTOWE.
    /// "woz do kupienia + kon juczny do powozenia, daje tyle ile w wozie kg mozna przewiezc, juki nie, juki to siodlo, mamy juz siodla, tylko dajmy siodla
    /// bojowe i siodla transportowe".
    ///
    /// Co juz bylo (sprawdzone 20.10): siodla BOJOWE sa w grze - kazda uprzaz HorseHarness konia (ROT: 16 siodel, RBM/SandBoxCore ok. 10 siodel i pancerze
    /// konskie); SaddleLaw (klucz RiderNeedsHarness) wymaga ich od kazdego jezdzca. Siodla TRANSPORTOWE tez sa jako przedmioty gry - "Mule Harness with
    /// Pack / Baskets / Bags" (mule_load_a/b/c) i obladowane siodla wielblada (camel_saddle_a/b), ale nikt ich nie robi (is_merchandise=false) i nic nie daja.
    /// Wozu jako przedmiotu nie ma w zadnym module (grep po ModuleData), a model 3D wozu jest tylko w prefabach scen (bd_cart_*).
    ///
    /// WOZ (klucz CampCartEnabled): stan druzyny gracza (liczba wozow, zapis arm_campcart "1|wozy"), NIE nowy przedmiot - bez nowego XML, siatki i typu
    /// w zapisie (nowy przedmiot bez sprawdzenia w grze = ryzyko ekranu ekwipunku i zapisu). Robi go stolarz (Wood Workshop) w miescie: z polki miasta
    /// CartWood drewna i CartTools narzedzi (kazda sztuka po swojej cenie - ShelfBuy, zloto do kasy miasta) plus robocizna CartLabor dla wlasciciela
    /// warsztatu (wlasny warsztat - bez robocizny; bez wlasciciela - do kasy miasta). Nic z niczego: drewno i narzedzia znikaja z polki, staja sie wozem.
    /// Woz ciagnie zwierze juczne druzyny (jedno na woz): takie zwierze wiezie CartLoadKg zamiast 100 kg gry (postfiks modelu pojemnosci dopisuje
    /// CartLoadKg - 100), a kolumna z wozem w zaprzegu jedzie o CartSpeedPenaltyPercent wolniej (postfiks CalculateFinalSpeed wszystkich modeli predkosci,
    /// licznik glebokosci jak SightRange - raz na lancuch). Woz bez zwierzecia stoi w taborze bez udzwigu i bez kary. Rozbicie wozu w warsztacie oddaje
    /// CartBackWood drewna do taboru. Niewola gracza: wozy biora zwyciezcy - drewno wozu (CartBackWood na woz) do taboru partii, ktora wziela gracza.
    /// AI wozow nie kupuje (zasada Jeffa: AI nie inwestuje) - stan tylko druzyny gracza.
    ///
    /// SIODLO TRANSPORTOWE (klucz PackSaddleEnabled): prawdziwy przedmiot gry mule_load_a robiony przez garbarza (Tannery): SaddleLeather skory
    /// i SaddleWood drewna z polki plus robocizna SaddleLabor. W taborze partii rodu gracza: kazde siodlo transportowe na wolnym zwierzeciu jucznym
    /// (nie w zaprzegu wozu) daje PackSaddleExtraKg i niesie samo siebie (dopisek wagi siodla - siodlo lezy na zwierzeciu, nie w jukach). Siodlo
    /// transportowe nie jest siodlem bojowym: SaddleLaw.ItemKind daje mu "nie pasuje" (nie zrobi jezdzca) - IsPackSaddle.
    /// Linie logu: start sesji "Woz obozowy (m97): start sesji", zakup i rozbicie, doba "Woz obozowy (m97): dzien N".
    /// </summary>
    internal static class CampCart
    {
        private const string P = "Woz obozowy (m97): ";
        private const string SaveKey = "arm_campcart";
        private const int GamePackKg = 100;     // gra: zwierze juczne x 10 x 10 kg (DefaultInventoryCapacityModel, PackAnimalsFactor x _itemAverageWeight)
        private const int CartWood = 6, CartTools = 1, CartLabor = 120, CartBackWood = 3;
        private const int SaddleLeather = 2, SaddleWood = 1, SaddleLabor = 40;
        private const string CartShop = "wood_WorkshopType", SaddleShop = "tannery";
        private static readonly string[] PackSaddleIds = { "mule_load_a", "mule_load_b", "mule_load_c", "camel_saddle_a", "camel_saddle_b" };

        private static int _carts;                      // wozy druzyny gracza (zapis)
        private static ItemObject[] _saddles;           // siodla transportowe (z MBObjectManager przy starcie sesji)
        private static HashSet<ItemObject> _saddleSet;  // to samo do szybkiego IsPackSaddle (SaddleLaw)
        private static ItemObject _wood, _tools, _leather, _madeSaddle;
        private static string _hooks = "-";
        private static int _stumbles, _dStumbles, _dBuilt, _dBroken, _dSaddles, _dLost;
        private static readonly HashSet<string> _err = new HashSet<string>();

        private static readonly TextObject _txtCart = new TextObject("{=!}Supply carts");
        private static readonly TextObject _txtSaddle = new TextObject("{=!}Pack saddles");
        private static readonly TextObject _txtSlow = new TextObject("{=!}Supply cart");

        internal static bool CartOn { get { var s = Settings.Current; return s != null && s.CampCartEnabled; } }
        internal static bool SaddleOn { get { var s = Settings.Current; return s != null && s.PackSaddleEnabled; } }
        internal static int Carts { get { return _carts; } }

        private static int CartLoad(Settings s) { return Math.Max(GamePackKg, Math.Min(2000, s.CartLoadKg)); }
        private static float SlowPct(Settings s) { return Math.Max(0, Math.Min(50, s.CartSpeedPenaltyPercent)); }
        private static int SaddleKg(Settings s) { return Math.Max(0, Math.Min(300, s.PackSaddleExtraKg)); }

        /// <summary>Siodlo transportowe (juczne) - nie siodlo bojowe; SaddleLaw nie robi na nim jezdzca.</summary>
        internal static bool IsPackSaddle(ItemObject it)
        {
            var set = _saddleSet;
            if (it == null) return false;
            if (set != null) return set.Contains(it);
            var id = it.StringId;
            for (int i = 0; i < PackSaddleIds.Length; i++) if (id == PackSaddleIds[i]) return true;
            return false;
        }

        private static void Stumble(string where, Exception e)
        {
            Interlocked.Increment(ref _stumbles); Interlocked.Increment(ref _dStumbles);
            bool first;
            lock (_err) first = _err.Add(where);
            if (first) { try { Log.Error("CampCart." + where, e); } catch { } }
        }

        // ------------------------------------------------------------ zapis

        internal static string Export() { return "1|" + _carts.ToString(CultureInfo.InvariantCulture); }

        internal static void Import(string s)
        {
            _carts = 0;
            if (string.IsNullOrEmpty(s)) return;
            var p = s.Split('|');
            int n;
            if (p.Length >= 2 && p[0] == "1" && int.TryParse(p[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out n)) _carts = Math.Max(0, Math.Min(50, n));
        }

        private static void Reset() { _carts = 0; _dBuilt = _dBroken = _dSaddles = _dLost = 0; }

        // ------------------------------------------------------------ wpiecie (ArmouryBehavior.RegisterEvents - jedna linia)

        internal static void RegisterEvents(CampaignBehaviorBase b)
        {
            CampaignEvents.OnNewGameCreatedEvent.AddNonSerializedListener(b, delegate { try { Reset(); } catch (Exception e) { Stumble("Reset", e); } });
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(b, delegate (CampaignGameStarter s) { try { SessionStart(s); } catch (Exception e) { Stumble("SessionStart", e); } });
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(b, delegate { try { Daily(); } catch (Exception e) { Stumble("Daily", e); } });
            CampaignEvents.HeroPrisonerTaken.AddNonSerializedListener(b, delegate (PartyBase captor, Hero h) { try { OnTaken(captor, h); } catch (Exception e) { Stumble("OnTaken", e); } });
        }

        private static void SessionStart(CampaignGameStarter starter)
        {
            var mo = MBObjectManager.Instance;
            _wood = mo.GetObject<ItemObject>("hardwood");
            _tools = mo.GetObject<ItemObject>("tools");
            _leather = mo.GetObject<ItemObject>("leather");
            _madeSaddle = mo.GetObject<ItemObject>("mule_load_a");
            var list = new List<ItemObject>();
            foreach (var id in PackSaddleIds) { var it = mo.GetObject<ItemObject>(id); if (it != null && it.ItemType == ItemObject.ItemTypeEnum.HorseHarness) list.Add(it); }
            _saddles = list.ToArray();
            _saddleSet = new HashSet<ItemObject>(list);
            bool menu = false;
            if (starter != null)
            {
                try { AddMenus(starter); menu = true; } catch (Exception e) { Stumble("AddMenus", e); }
            }
            var s = Settings.Current;
            Log.Info(P + "start sesji | woz " + (CartOn ? "TAK" : "NIE") + " (Camp Cart Enabled; ladownosc " + (s != null ? CartLoad(s) : 0) + " kg zamiast " + GamePackKg
                     + " kg zwierzecia w zaprzegu, wolniej o " + (s != null ? SlowPct(s) : 0) + "%) | siodla transportowe " + (SaddleOn ? "TAK" : "NIE")
                     + " (Pack Saddle Enabled; +" + (s != null ? SaddleKg(s) : 0) + " kg, rodzaje " + _saddles.Length + "/" + PackSaddleIds.Length + ")"
                     + " | wozy gracza " + _carts + " | materialy drewno " + (_wood != null ? "tak" : "BRAK") + ", narzedzia " + (_tools != null ? "tak" : "BRAK")
                     + ", skora " + (_leather != null ? "tak" : "BRAK") + ", siodlo do robienia " + (_madeSaddle != null ? "tak" : "BRAK")
                     + " | menu miasta " + (menu ? "tak" : "NIE") + " | wpiecia " + _hooks + ".");
        }

        // ------------------------------------------------------------ udzwig i predkosc

        /// <summary>Postfiks DefaultInventoryCapacityModel.CalculateInventoryCapacity: woz w zaprzegu (CartLoadKg - 100 na zwierze) i siodla transportowe
        /// (PackSaddleExtraKg + waga siodla na wolnym zwierzeciu jucznym). Tylko na ladzie; armia (includeFollowers) - takze doczepione partie. Tylko odczyt.</summary>
        public static void CapPostfix(MobileParty __0, bool __1, bool __6, ref ExplainedNumber __result)
        {
            try
            {
                if (__0 == null || __1) return;
                var s = Settings.Current;
                if (s == null) return;
                bool cart = s.CampCartEnabled && _carts > 0;
                bool sad = s.PackSaddleEnabled && _saddles != null && _saddles.Length > 0;
                if (!cart && !sad) return;
                float cartKg = 0f, sadKg = 0f;
                One(__0, s, cart, sad, ref cartKg, ref sadKg);
                if (__6)
                {
                    var att = __0.AttachedParties;
                    if (att != null) for (int i = 0; i < att.Count; i++) { var ap = att[i]; if (ap != null && ap != __0) One(ap, s, cart, sad, ref cartKg, ref sadKg); }
                }
                if (cartKg > 0f) __result.Add(cartKg, _txtCart);
                if (sadKg > 0f) __result.Add(sadKg, _txtSaddle);
            }
            catch (Exception e) { Stumble("CapPostfix", e); }
        }

        private static void One(MobileParty mp, Settings s, bool cart, bool sad, ref float cartKg, ref float sadKg)
        {
            var main = MobileParty.MainParty;
            bool mine = mp == main;
            if (!mine && !(sad && mp.ActualClan != null && mp.ActualClan == Clan.PlayerClan)) return;
            var pb = mp.Party;
            if (pb == null) return;
            int packs = pb.NumberOfPackAnimals;
            if (packs <= 0) return;
            int pulled = 0;
            if (cart && mine) { pulled = Math.Min(_carts, packs); cartKg += pulled * (CartLoad(s) - GamePackKg); }
            if (!sad) return;
            int free = packs - pulled;
            var roster = mp.ItemRoster;
            if (free <= 0 || roster == null) return;
            int extra = SaddleKg(s);
            for (int i = 0; i < _saddles.Length && free > 0; i++)
            {
                int n = roster.GetItemNumber(_saddles[i]);
                if (n <= 0) continue;
                int use = Math.Min(n, free);
                free -= use;
                sadKg += use * (extra + _saddles[i].Weight);
            }
        }

        /// <summary>Wozy w zaprzegu druzyny gracza (zwierze juczne na woz).</summary>
        internal static int Pulled()
        {
            var main = MobileParty.MainParty;
            if (main == null || main.Party == null || _carts <= 0) return 0;
            return Math.Min(_carts, main.Party.NumberOfPackAnimals);
        }

        [ThreadStatic] private static int _depth;
        public static void DepthPrefix() { _depth++; }
        public static Exception DepthFinalizer(Exception __exception) { if (_depth > 0) _depth--; return __exception; }

        /// <summary>Postfiks CalculateFinalSpeed (kazdy model predkosci; licznik glebokosci - raz na lancuch): druzyna gracza z wozem w zaprzegu i wodz armii,
        /// do ktorej gracz jest doczepiony, jada o CartSpeedPenaltyPercent wolniej. Na morzu bez kary.</summary>
        public static void SpeedPostfix(MobileParty __0, ref ExplainedNumber __result)
        {
            try
            {
                if (_depth > 1 || _carts <= 0 || __0 == null) return;
                var s = Settings.Current;
                if (s == null || !s.CampCartEnabled) return;
                float pct = SlowPct(s);
                if (pct <= 0f) return;
                var main = MobileParty.MainParty;
                if (main == null || (__0 != main && main.AttachedTo != __0)) return;
                if (__0.IsCurrentlyAtSea || Pulled() <= 0) return;
                __result.AddFactor(-pct / 100f, _txtSlow);
            }
            catch (Exception e) { Stumble("SpeedPostfix", e); }
        }

        internal static void ApplyAll(Harmony h)
        {
            var parts = new List<string>();
            try
            {
                var cap = AccessTools.DeclaredMethod(typeof(DefaultInventoryCapacityModel), "CalculateInventoryCapacity");
                if (cap == null) parts.Add("BRAK CalculateInventoryCapacity - bez udzwigu wozu i siodel");
                else { h.Patch(cap, postfix: new HarmonyMethod(typeof(CampCart), nameof(CapPostfix))); parts.Add("udzwig"); }
            }
            catch (Exception e) { parts.Add("blad udzwigu " + e.GetType().Name); Stumble("ApplyAll(udzwig)", e); }
            int done = 0;
            try
            {
                var post = new HarmonyMethod(typeof(CampCart), nameof(SpeedPostfix)) { priority = Priority.Normal };
                var pre = new HarmonyMethod(typeof(CampCart), nameof(DepthPrefix)) { priority = Priority.First };
                var fin = new HarmonyMethod(typeof(CampCart), nameof(DepthFinalizer));
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    Type[] types;
                    try { types = asm.GetTypes(); }
                    catch (ReflectionTypeLoadException rtle) { types = rtle.Types; }
                    catch { continue; }
                    foreach (var t in types)
                    {
                        if (t == null || t.IsAbstract || !typeof(PartySpeedModel).IsAssignableFrom(t)) continue;
                        try
                        {
                            var m = AccessTools.DeclaredMethod(t, "CalculateFinalSpeed", new[] { typeof(MobileParty), typeof(ExplainedNumber) });
                            if (m == null || m.IsAbstract) continue;
                            h.Patch(m, prefix: pre, postfix: post, finalizer: fin);
                            done++;
                        }
                        catch { }
                    }
                }
                parts.Add("predkosc " + done + " modeli");
            }
            catch (Exception e) { parts.Add("blad predkosci " + e.GetType().Name); Stumble("ApplyAll(predkosc)", e); }
            _hooks = string.Join(", ", parts.ToArray());
            Log.Info(P + "wpiecia: " + _hooks + ".");
        }

        // ------------------------------------------------------------ menu miasta: stolarz i garbarz

        private static Workshop ShopOf(Settlement st, string type)
        {
            var town = st != null ? st.Town : null;
            if (town == null || town.Workshops == null) return null;
            foreach (var w in town.Workshops)
                if (w != null && w.WorkshopType != null && w.WorkshopType.StringId == type) return w;
            return null;
        }

        private static int Have(Settlement st, ItemObject it)
        {
            if (st == null || it == null || st.ItemRoster == null) return 0;
            return st.ItemRoster.GetItemNumber(it);
        }

        private static int PriceOf(Settlement st, ItemObject it)
        {
            try { return st.Town.GetItemPrice(new EquipmentElement(it), MobileParty.MainParty, false); } catch { return 0; }
        }

        private static int Labor(Workshop w, int fee) { return w != null && w.Owner == Hero.MainHero ? 0 : fee; }

        /// <summary>Szacunek: cena pierwszej sztuki x ile (kazda nastepna sztuka moze byc odrobine drozsza) + robocizna.</summary>
        private static int Estimate(Settlement st, ItemObject a, int na, ItemObject b, int nb, int labor)
        {
            return PriceOf(st, a) * na + (b != null ? PriceOf(st, b) * nb : 0) + labor;
        }

        private static void AddMenus(CampaignGameStarter starter)
        {
            starter.AddGameMenuOption("town", "arm_cartwright_enter", "{=!}Visit the cartwright and saddler",
                delegate (MenuCallbackArgs a)
                {
                    if (!CartOn && !SaddleOn) return false;
                    a.optionLeaveType = GameMenuOption.LeaveType.Trade;
                    var st = Settlement.CurrentSettlement;
                    if (ShopOf(st, CartShop) == null && ShopOf(st, SaddleShop) == null && _carts <= 0)
                    {
                        a.IsEnabled = false;
                        a.Tooltip = new TextObject("{=!}No carpenter's yard or tannery in this town.");
                    }
                    return true;
                },
                delegate (MenuCallbackArgs a) { GameMenu.SwitchToMenu("arm_cartwright"); }, false, 8);

            starter.AddGameMenu("arm_cartwright", "{=!}{ARM_CARTWRIGHT_TEXT}", delegate (MenuCallbackArgs a)
            {
                try { MBTextManager.SetTextVariable("ARM_CARTWRIGHT_TEXT", MenuText()); } catch (Exception e) { Stumble("MenuText", e); }
            });

            starter.AddGameMenuOption("arm_cartwright", "arm_cart_buy", "{=!}Have a supply cart built (about {ARM_CART_COST}{GOLD_ICON})",
                CartCond, delegate (MenuCallbackArgs a) { try { BuildCart(); } catch (Exception e) { Stumble("BuildCart", e); } GameMenu.SwitchToMenu("arm_cartwright"); }, false, 1);
            starter.AddGameMenuOption("arm_cartwright", "arm_saddle_buy", "{=!}Have a pack saddle made (about {ARM_SADDLE_COST}{GOLD_ICON})",
                SaddleCond, delegate (MenuCallbackArgs a) { try { MakeSaddle(); } catch (Exception e) { Stumble("MakeSaddle", e); } GameMenu.SwitchToMenu("arm_cartwright"); }, false, 2);
            starter.AddGameMenuOption("arm_cartwright", "arm_cart_break", "{=!}Break up a cart for its wood",
                delegate (MenuCallbackArgs a)
                {
                    if (_carts <= 0) return false;
                    a.optionLeaveType = GameMenuOption.LeaveType.Default;
                    return true;
                },
                delegate (MenuCallbackArgs a) { try { BreakCart(); } catch (Exception e) { Stumble("BreakCart", e); } GameMenu.SwitchToMenu("arm_cartwright"); }, false, 3);
            starter.AddGameMenuOption("arm_cartwright", "arm_cartwright_leave", "{=!}Leave",
                delegate (MenuCallbackArgs a) { a.optionLeaveType = GameMenuOption.LeaveType.Leave; return true; },
                delegate (MenuCallbackArgs a) { GameMenu.SwitchToMenu("town"); }, true, 9);
        }

        private static string MenuText()
        {
            var s = Settings.Current;
            var st = Settlement.CurrentSettlement;
            int packs = MobileParty.MainParty != null && MobileParty.MainParty.Party != null ? MobileParty.MainParty.Party.NumberOfPackAnimals : 0;
            int saddles = 0;
            var roster = MobileParty.MainParty != null ? MobileParty.MainParty.ItemRoster : null;
            if (roster != null && _saddles != null) foreach (var it in _saddles) saddles += roster.GetItemNumber(it);
            string t = "Carpenters and tanners work the yards by the gate. ";
            if (s != null && s.CampCartEnabled)
                t += "A supply cart takes " + CartWood + " hardwood and " + CartTools + " tools from the market, plus the carpenter's fee. One pack animal pulls it and hauls "
                     + CartLoad(s) + " kg instead of " + GamePackKg + " kg, but the column moves " + SlowPct(s).ToString("0", CultureInfo.InvariantCulture) + "% slower. ";
            if (s != null && s.PackSaddleEnabled)
                t += "A pack saddle takes " + SaddleLeather + " leather and " + SaddleWood + " hardwood, plus the tanner's fee. On a pack animal it adds " + SaddleKg(s) + " kg and carries its own weight. ";
            t += "\n\nYou have " + _carts + (_carts == 1 ? " cart" : " carts") + ", " + packs + (packs == 1 ? " pack animal" : " pack animals") + " and " + saddles
                 + (saddles == 1 ? " pack saddle" : " pack saddles") + ".";
            if (_carts > packs) t += " Some carts have no animal to pull them.";
            if (ShopOf(st, CartShop) == null) t += " This town has no carpenter's yard.";
            if (ShopOf(st, SaddleShop) == null) t += " This town has no tannery.";
            return t;
        }

        private static bool CartCond(MenuCallbackArgs a)
        {
            if (!CartOn) return false;
            a.optionLeaveType = GameMenuOption.LeaveType.Trade;
            var st = Settlement.CurrentSettlement;
            var w = ShopOf(st, CartShop);
            int cost = st != null && _wood != null && _tools != null ? Estimate(st, _wood, CartWood, _tools, CartTools, Labor(w, CartLabor)) : 0;
            MBTextManager.SetTextVariable("ARM_CART_COST", cost);
            string why = null;
            if (w == null) why = "This town has no carpenter's yard.";
            else if (_wood == null || _tools == null) why = "No hardwood or tools in this world.";
            else if (Have(st, _wood) < CartWood || Have(st, _tools) < CartTools) why = "The market lacks the wood or the tools (" + CartWood + " hardwood, " + CartTools + " tools).";
            else if (Hero.MainHero.Gold < cost) why = "You cannot afford it.";
            if (why != null) { a.IsEnabled = false; a.Tooltip = new TextObject("{=!}" + why); }
            return true;
        }

        private static bool SaddleCond(MenuCallbackArgs a)
        {
            if (!SaddleOn) return false;
            a.optionLeaveType = GameMenuOption.LeaveType.Trade;
            var st = Settlement.CurrentSettlement;
            var w = ShopOf(st, SaddleShop);
            int cost = st != null && _leather != null && _wood != null ? Estimate(st, _leather, SaddleLeather, _wood, SaddleWood, Labor(w, SaddleLabor)) : 0;
            MBTextManager.SetTextVariable("ARM_SADDLE_COST", cost);
            string why = null;
            if (w == null) why = "This town has no tannery.";
            else if (_leather == null || _wood == null || _madeSaddle == null) why = "No leather, hardwood or pack saddle in this world.";
            else if (Have(st, _leather) < SaddleLeather || Have(st, _wood) < SaddleWood) why = "The market lacks the leather or the wood (" + SaddleLeather + " leather, " + SaddleWood + " hardwood).";
            else if (Hero.MainHero.Gold < cost) why = "You cannot afford it.";
            if (why != null) { a.IsEnabled = false; a.Tooltip = new TextObject("{=!}" + why); }
            return true;
        }

        /// <summary>Material z polki miasta (ShelfBuy - kazda sztuka po swojej cenie); obie partie albo nic (zwrot na polke).</summary>
        private static bool TakeTwo(Settlement st, ItemObject a, int na, ItemObject b, int nb, int budget, out int paid)
        {
            paid = 0;
            var shelf = st.ItemRoster;
            var town = st.Town;
            var mp = MobileParty.MainParty;
            var ea = new EquipmentElement(a);
            var eb = new EquipmentElement(b);
            int ca, fa, la, cb = 0, fb = 0, lb;
            int ta = ShelfBuy.Take(shelf, ea, na, budget, () => town.GetItemPrice(ea, mp, false), out ca, out fa, out la, 0, false);
            if (ta < na) { ShelfBuy.PutBack(shelf, ea, ta, ca, fa); return false; }
            int tb = nb > 0 ? ShelfBuy.Take(shelf, eb, nb, budget - ca, () => town.GetItemPrice(eb, mp, false), out cb, out fb, out lb, 0, false) : 0;
            if (tb < nb) { ShelfBuy.PutBack(shelf, ea, ta, ca, fa); ShelfBuy.PutBack(shelf, eb, tb, cb, fb); return false; }
            paid = ca + cb;
            return true;
        }

        private static void PayLabor(Settlement st, Workshop w, int fee)
        {
            if (fee <= 0) return;
            var owner = w != null ? w.Owner : null;
            if (owner != null && owner != Hero.MainHero && owner.IsAlive) GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, owner, fee, true);
            else GiveGoldAction.ApplyForCharacterToSettlement(Hero.MainHero, st, fee, true);
        }

        private static void BuildCart()
        {
            var s = Settings.Current;
            var st = Settlement.CurrentSettlement;
            var w = ShopOf(st, CartShop);
            if (s == null || st == null || w == null || _wood == null || _tools == null) return;
            int labor = Labor(w, CartLabor);
            int paid;
            if (!TakeTwo(st, _wood, CartWood, _tools, CartTools, Hero.MainHero.Gold - labor, out paid))
            {
                Log.Player("The carpenter shakes his head - not enough wood or tools at a price you can pay.", true);
                return;
            }
            if (paid > 0) GiveGoldAction.ApplyForCharacterToSettlement(Hero.MainHero, st, paid, true);
            PayLabor(st, w, labor);
            _carts++;
            _dBuilt++;
            Log.Player("The carpenter builds you a supply cart for " + (paid + labor) + " denars. Hitch a pack animal to it: it hauls " + CartLoad(s) + " kg instead of "
                       + GamePackKg + " kg, and the column moves " + SlowPct(s).ToString("0", CultureInfo.InvariantCulture) + "% slower.");
            Log.Info(P + "woz zbudowany w " + st.Name + " | material " + paid + " zl do kasy miasta, robocizna " + labor + " zl (" + (w.Owner != null ? w.Owner.Name.ToString() : "kasa miasta")
                     + ") | wozy gracza " + _carts + ", w zaprzegu " + Pulled() + ".");
        }

        private static void MakeSaddle()
        {
            var st = Settlement.CurrentSettlement;
            var w = ShopOf(st, SaddleShop);
            if (st == null || w == null || _leather == null || _wood == null || _madeSaddle == null || MobileParty.MainParty == null) return;
            int labor = Labor(w, SaddleLabor);
            int paid;
            if (!TakeTwo(st, _leather, SaddleLeather, _wood, SaddleWood, Hero.MainHero.Gold - labor, out paid))
            {
                Log.Player("The tanner shakes his head - not enough leather or wood at a price you can pay.", true);
                return;
            }
            if (paid > 0) GiveGoldAction.ApplyForCharacterToSettlement(Hero.MainHero, st, paid, true);
            PayLabor(st, w, labor);
            MobileParty.MainParty.ItemRoster.AddToCounts(_madeSaddle, 1);
            _dSaddles++;
            Log.Player("The tanner fits a pack saddle (" + _madeSaddle.Name + ") for " + (paid + labor) + " denars. It is in your baggage.");
            Log.Info(P + "siodlo transportowe zrobione w " + st.Name + " | material " + paid + " zl do kasy miasta, robocizna " + labor + " zl.");
        }

        private static void BreakCart()
        {
            if (_carts <= 0 || MobileParty.MainParty == null) return;
            _carts--;
            _dBroken++;
            if (_wood != null) MobileParty.MainParty.ItemRoster.AddToCounts(_wood, CartBackWood);
            Log.Player("The cart is broken up. " + CartBackWood + " hardwood goes into your baggage.");
            Log.Info(P + "woz rozbity | drewno " + CartBackWood + " do taboru | wozy gracza " + _carts + ".");
        }

        // ------------------------------------------------------------ niewola gracza i linia doby

        private static void OnTaken(PartyBase captor, Hero h)
        {
            if (h != Hero.MainHero || _carts <= 0) return;
            int n = _carts;
            _carts = 0;
            _dLost += n;
            if (captor != null && captor.ItemRoster != null && _wood != null) captor.ItemRoster.AddToCounts(_wood, n * CartBackWood);
            Log.Info(P + "gracz w niewoli - " + n + " wozow wzieli zwyciezcy (" + (captor != null ? captor.Name.ToString() : "?") + ", drewno " + (n * CartBackWood) + " do ich taboru).");
        }

        private static void Daily()
        {
            var s = Settings.Current;
            if (s == null || (_carts == 0 && _dBuilt == 0 && _dBroken == 0 && _dSaddles == 0 && _dLost == 0 && _dStumbles == 0)) return;
            var main = MobileParty.MainParty;
            int packs = main != null && main.Party != null ? main.Party.NumberOfPackAnimals : 0;
            int saddles = 0;
            if (main != null && main.ItemRoster != null && _saddles != null) foreach (var it in _saddles) saddles += main.ItemRoster.GetItemNumber(it);
            int pulled = Pulled();
            int onAnimals = Math.Min(saddles, Math.Max(0, packs - pulled));
            Log.Info(P + "dzien " + (int)CampaignTime.Now.ToDays + " | wozy gracza " + _carts + " (w zaprzegu " + pulled + " z " + packs + " jucznych, +" + (pulled * (CartLoad(s) - GamePackKg))
                     + " kg, wolniej o " + (pulled > 0 ? SlowPct(s) : 0f).ToString("0", CultureInfo.InvariantCulture) + "%) | siodla transportowe " + saddles + " (na jucznych " + onAnimals
                     + ") | dzis zbudowane " + _dBuilt + ", rozbite " + _dBroken + ", siodla " + _dSaddles + ", stracone w niewoli " + _dLost
                     + " | potkniecia dzis " + _dStumbles + " (razem " + _stumbles + ").");
            _dBuilt = _dBroken = _dSaddles = _dLost = 0;
            _dStumbles = 0;
        }
    }
}
