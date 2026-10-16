using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.CraftingSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// NZ (16.10, Jeff "jak ktos nie ma kasy to jak moze ofertowac kucie mieczy??") - ZLECENIE KOWALSKIE TYLKO OD ZLECENIODAWCY Z PIENIEDZMI (klucz CraftOrdersGiverCanPay).
    ///
    /// Od E8/K (CraftOrders8, klucz CraftOrdersHistorical) nagrode zlecenia placi zleceniodawca ze swojej kiesy (GiverPay.PayFromGiver), ale nikt nie pytal,
    /// czy ma pieniadze, gdy zlecenie powstaje albo wisi - stad przy oddaniu "Prudence could pay only 0 of the agreed 11".
    /// Sprawdzone w TaleWorlds.CampaignSystem.dll (dekompilacja CraftingCampaignBehavior, 1.4.8):
    ///   - zlecenia miejskie (notable i lordowie z druzyna w miescie, IsLordOrder = OrderOwner.IsLord) powstaja TYLKO w CreateTownOrder(Hero, int slot):
    ///     DailyTickSettlement (5% na bohatera, gdy jest wolny slot), DailyTick -> ReplaceCraftingOrder (5% na zlecenie: zdejmuje i tworzy nowe od losowego
    ///     bohatera), OnNewGameCreatedPartialFollowUpEnd (start nowej kampanii) i nasza wymiana startowa CraftOrders8.FirstDay (ReplaceCraftingOrder);
    ///     zlecenie trafia do CraftingOrderSlots.Slots[DifficultyLevel] (DifficultyLevel = slot), RemoveTownOrder zeruje Slots[DifficultyLevel];
    ///   - CreateCustomOrderForHero wola tylko zadanie GangLeaderNeedsSpecialWeapons (CustomOrders, zadanie trzyma zlecenie w _currentCraftingOrder
    ///     i zdejmuje je CancelCustomOrder) - zlecen zadan NIE ruszamy (ani przy tworzeniu, ani w przegladzie);
    ///   - smierc: OnHeroKilled -> RemoveOrdersOfHeroWithoutCompletionIfExists zdejmuje zlecenia miejskie zmarlego; przeglad lapie reszte (smierc bez zdarzenia).
    /// Aktywne zlecenie gracza: ekran kuzni (CraftingState) buduje liste z CraftingOrders[Settlement.CurrentSettlement.Town] w CraftingOrderPopupVM.RefreshOrders,
    /// wybrane zlecenie trzyma CraftingOrderPopupVM.SelectedCraftingOrder (sam VM, behavior gry go nie zna); CompleteOrder z VM zdejmuje zlecenie po
    /// DifficultyLevel - usuniecie zlecenia spod otwartego ekranu zostawiloby w VM zlecenie bez slotu. Gdy CraftingState jest na stosie stanow gry
    /// (GameStateManager.LastOrDefault), przeglad nie usuwa zlecen miasta gracza (licznik). Kampania i tak stoi przy otwartej kuzni (GameStateManager.OnTick
    /// tyka tylko stan czynny) - to zabezpieczenie.
    ///
    /// Teraz (klucz true i Craft Orders Historical czynne - bez niego zloto idzie z niczego i nie ma czego sprawdzac):
    ///   (1) postfiks CreateTownOrder (metoda deklarowana): nowe zlecenie (inne niz w slocie przed wywolaniem - prefiks BK moze pominac metode), gdy
    ///       zleceniodawca ma mniej niz BaseGoldReward (juz po przeliczeniu historycznym w postfiksie konstruktora E8/K) - zdjete droga gry
    ///       (CraftingOrderSlots.RemoveTownOrder); slot wolny, gra sprobuje innego bohatera w kolejnych dobach. Nowa kampania przed przeliczeniem cen
    ///       (HistoricalPrices czynne, a jeszcze nie Applied) - bez sprawdzenia, sprawdza start sesji;
    ///   (2) start sesji (po CraftOrders8.SessionStart - nagrody z zapisu juz przeliczone) i kazda doba (ArmouryBehavior.OnDailyTick, po CraftOrders8.Daily):
    ///       zlecenia miejskie zmarlego (albo bez zleceniodawcy) i zleceniodawcy z kiesa ponizej nagrody - zdjete ta sama droga;
    ///   (3) oddanie bez zmian (GiverPay: placi, ile ma - komunikat zostaje na wypadek, gdy kiesa spadla po ostatnim przegladzie albo premia za lepsza bron).
    /// Klucz wylaczony: nic nie zdejmujemy, przeglad liczy "w cieniu", ile by zdjal.
    /// Stan tylko w pamieci (liczniki); zlecenia zapisuje gra (_craftingOrders). Reset z CraftOrders8.Reset (WorldMeasure.Reset - konstruktor ArmouryBehavior).
    /// Linie: "Zlecenia kowalskie (NZ): latki przy starcie gry", "... start sesji", "... dzien N". Wyjatek = licznik potkniec per zlecenie, bez wylacznika.
    /// </summary>
    internal static class CraftOrdersPurse
    {
        private const string Head = "Zlecenia kowalskie (NZ): ";
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        internal static bool KeyOn { get { var s = Settings.Current; return s != null && s.CraftOrdersGiverCanPay; } }
        /// <summary>Czynne: klucz i platnik z kiesy (Craft Orders Historical). Bez platnika gra wyplaca z niczego - zleceniodawca nie musi miec pieniedzy.</summary>
        internal static bool On { get { return KeyOn && CraftOrders8.PayOn; } }

        private static Action<CraftingCampaignBehavior.CraftingOrderSlots, CraftingOrder> _removeTown;   // CraftingOrderSlots.RemoveTownOrder (internal)
        private static bool _wiredCreate;
        private static string _createWhy = "nie zakladana";
        private static CraftingCampaignBehavior _beh;

        private static int _stumbles, _stumblesDay;
        private static readonly HashSet<string> _errSites = new HashSet<string>();
        private static long _ticks;

        private static void Stumble(string where, Exception e)
        {
            _stumbles++; _stumblesDay++;
            try { if (_errSites.Add(where)) Log.Error("CraftOrdersPurse." + where, e); } catch { }
        }

        private static CraftingCampaignBehavior Beh()
        {
            var b = _beh;
            if (b != null) return b;
            var c = Campaign.Current;
            b = c != null ? c.GetCampaignBehavior<CraftingCampaignBehavior>() : null;
            _beh = b;
            return b;
        }

        private static string Ms(long ticks) { return (ticks * 1000.0 / Stopwatch.Frequency).ToString("0.00", Inv); }
        private static string OnOff(bool b) { return b ? "CZYNNE" : "WYLACZONE"; }

        // ------------------------------------------------------------ droga gry: zdjecie zlecenia miejskiego

        private static int _mismatchDay;

        /// <summary>CraftingOrderSlots.RemoveTownOrder zeruje Slots[DifficultyLevel] - zdejmujemy tylko wtedy, gdy w tym slocie stoi wlasnie to zlecenie.
        /// Nie zdjete (niezgodny slot albo brak drogi gry) - licznik _mismatchDay.</summary>
        private static bool Remove(CraftingCampaignBehavior.CraftingOrderSlots slots, CraftingOrder o)
        {
            var d = _removeTown;
            if (slots == null || o == null) return false;
            if (d == null) { _mismatchDay++; return false; }               // brak drogi gry - linia startu gry pokazuje BRAK
            var arr = slots.Slots;
            int i = o.DifficultyLevel;
            if (arr == null || i < 0 || i >= arr.Length || !ReferenceEquals(arr[i], o)) { _mismatchDay++; return false; }
            d(slots, o);
            return true;
        }

        private static Town TownOf(Hero h)
        {
            var st = h != null ? h.CurrentSettlement : null;
            return st != null ? st.Town : null;
        }

        private static CraftingCampaignBehavior.CraftingOrderSlots SlotsOf(CraftingCampaignBehavior beh, Town town)
        {
            if (beh == null || town == null) return null;
            var all = beh.CraftingOrders;
            CraftingCampaignBehavior.CraftingOrderSlots slots;
            return all != null && all.TryGetValue(town, out slots) ? slots : null;
        }

        /// <summary>Ekran kuzni na stosie stanow gry (CraftingState) - wtedy nie zdejmujemy zlecen miasta gracza. Blad odczytu = traktuj jak otwarty.</summary>
        private static bool ForgeOpen(out Town here)
        {
            here = null;
            try
            {
                var g = Game.Current;
                var m = g != null ? g.GameStateManager : null;
                if (m == null || m.LastOrDefault<TaleWorlds.CampaignSystem.GameState.CraftingState>() == null) return false;
                var st = Settlement.CurrentSettlement;
                here = st != null ? st.Town : null;
                return true;
            }
            catch (Exception e) { Stumble("ForgeOpen", e); return true; }
        }

        // ------------------------------------------------------------ (1) nowe zlecenie miejskie

        private static int _made, _rejected, _early, _madeOff, _madeOpen, _madeKept;
        private static long _rejectedDue;

        /// <summary>Prefiks CreateTownOrder(Hero orderOwner, int orderSlot): zlecenie, ktore stoi w slocie przed wywolaniem (zwykle null).</summary>
        public static void CreatePrefix(CraftingCampaignBehavior __instance, Hero __0, int __1, out CraftingOrder __state)
        {
            __state = null;
            try
            {
                var slots = SlotsOf(__instance, TownOf(__0));
                var arr = slots != null ? slots.Slots : null;
                if (arr != null && __1 >= 0 && __1 < arr.Length) __state = arr[__1];
            }
            catch (Exception e) { Stumble("CreatePrefix", e); }
        }

        /// <summary>Postfiks CreateTownOrder: nowe zlecenie od zleceniodawcy bez nagrody w kiesie - zdjete zaraz po utworzeniu (slot zostaje wolny).</summary>
        public static void CreatePostfix(CraftingCampaignBehavior __instance, Hero __0, int __1, CraftingOrder __state)
        {
            long t0 = Stopwatch.GetTimestamp();
            try
            {
                var town = TownOf(__0);
                var slots = SlotsOf(__instance, town);
                var arr = slots != null ? slots.Slots : null;
                if (arr == null || __1 < 0 || __1 >= arr.Length) return;
                var o = arr[__1];
                if (o == null || ReferenceEquals(o, __state)) return;          // zlecenie nie powstalo (prefiks BK pominal metode albo wyjatek gry)
                _made++;
                if (!On) { _madeOff++; return; }
                if (HistoricalPrices.On && !HistoricalPrices.Applied) { _early++; return; }   // start nowej kampanii: nagroda jeszcze w starej monecie - sprawdzi start sesji
                var owner = o.OrderOwner;
                if (owner == null || owner.Gold >= o.BaseGoldReward) return;
                Town here;
                if (ForgeOpen(out here) && (here == null || ReferenceEquals(here, town))) { _madeOpen++; return; }
                if (Remove(slots, o)) { _rejected++; _rejectedDue += o.BaseGoldReward; }
                else _madeKept++;                                                // brak drogi gry albo niezgodny slot - zlecenie zostaje
            }
            catch (Exception e) { Stumble("CreatePostfix", e); }
            finally { _ticks += Stopwatch.GetTimestamp() - t0; }
        }

        // ------------------------------------------------------------ (2) przeglad zlecen wiszacych (start sesji i doba)

        private sealed class Pass
        {
            public int Seen, Poor, Dead, Hang, Uncovered, Open, Mismatch, Custom, ShPoor, ShDead;
            public long PoorDue;
            public readonly List<int> Gold = new List<int>(64);
        }

        private static void Sweep(Pass p, bool act)
        {
            var beh = Beh();
            var all = beh != null ? beh.CraftingOrders : null;
            if (all == null) return;
            Town here = null;
            bool open = act && ForgeOpen(out here);
            int mm0 = _mismatchDay;
            foreach (var kv in all)
            {
                var slots = kv.Value;
                if (slots == null) continue;
                try { var cu = slots.CustomOrders; if (cu != null) p.Custom += cu.Count; } catch (Exception e) { Stumble("Sweep(zadania)", e); }
                var arr = slots.Slots;
                if (arr == null) continue;
                for (int i = 0; i < arr.Length; i++)
                {
                    var o = arr[i];
                    if (o == null) continue;
                    try
                    {
                        p.Seen++;
                        var owner = o.OrderOwner;
                        bool dead = owner == null || !owner.IsAlive;
                        bool poor = !dead && owner.Gold < o.BaseGoldReward;
                        bool bad = dead || poor;
                        if (bad)
                        {
                            if (!act) { if (dead) p.ShDead++; else p.ShPoor++; }
                            else if (open && (here == null || ReferenceEquals(here, kv.Key))) p.Open++;   // ekran kuzni otwarty - zlecenie moze byc wlasnie wykonywane
                            else if (Remove(slots, o))
                            {
                                if (dead) p.Dead++; else { p.Poor++; p.PoorDue += o.BaseGoldReward; }
                                continue;
                            }
                        }
                        p.Hang++;
                        if (bad) p.Uncovered++;                    // = ekran kuzni + niezgodny slot / brak drogi + cien (klucz wylaczony)
                        if (owner != null) p.Gold.Add(owner.Gold);
                    }
                    catch (Exception e) { Stumble("Sweep(zlecenie)", e); }
                }
            }
            p.Mismatch = _mismatchDay - mm0;
        }

        private static string Purse(Pass p)
        {
            if (p.Gold.Count == 0) return "-/-";
            p.Gold.Sort();
            int n = p.Gold.Count;
            long med = n % 2 == 1 ? p.Gold[n / 2] : ((long)p.Gold[n / 2 - 1] + p.Gold[n / 2]) / 2;
            return p.Gold[0].ToString(Inv) + "/" + med.ToString(Inv);
        }

        private static string Rest(Pass p, bool on)
        {
            return ", wiszacych " + p.Hang + " (bez pokrycia " + p.Uncovered + "), kiesa zleceniodawcow min/mediana " + Purse(p)
                   + " | zostawione mimo braku: ekran kuzni otwarty " + p.Open + ", niezgodny slot albo brak drogi gry " + p.Mismatch
                   + " | w cieniu (klucz wylaczony): bez kiesy " + p.ShPoor + ", bez zywego zleceniodawcy " + p.ShDead
                   + " | zlecen zadan (pominiete) " + p.Custom
                   + " | klucz Craft Orders Giver Can Pay " + OnOff(KeyOn) + ", placi zleceniodawca (Craft Orders Historical) " + OnOff(CraftOrders8.PayOn)
                   + ", dziala " + (on ? "TAK" : "NIE");
        }

        /// <summary>Z ArmouryBehavior.OnSessionLaunched, PO CraftOrders8.SessionStart (nagrody z zapisu i zlecenia startowe nowej kampanii juz przeliczone).</summary>
        internal static void SessionStart()
        {
            long t0 = Stopwatch.GetTimestamp();
            bool on = On;
            var p = new Pass();
            int early = _early, made = _made, rej = _rejected;
            try { Sweep(p, on); } catch (Exception e) { Stumble("SessionStart", e); }
            Log.Info(Head + "start sesji | zlecen miejskich z zapisu " + p.Seen + ", usuniete z zapisu " + (p.Poor + p.Dead) + " (kiesa " + p.Poor + ", smierc " + p.Dead
                     + "; nagrod usunietych razem " + p.PoorDue + ")" + Rest(p, on)
                     + " | przed startem sesji (start nowej kampanii): utworzone " + made + ", odrzucone przy tworzeniu " + rej + ", przed przeliczeniem cen (sprawdzone tutaj) " + early
                     + " | latka tworzenia (CreateTownOrder) " + (_wiredCreate ? "TAK" : "NIE") + ", droga gry RemoveTownOrder " + (_removeTown != null ? "TAK" : "NIE")
                     + " | koszt " + Ms(Stopwatch.GetTimestamp() - t0) + " ms | potkniecia " + _stumbles + ".");
            ClearDay();                                       // linia pierwszej doby liczy od startu sesji
        }

        /// <summary>Raz na dobe (ArmouryBehavior.OnDailyTick, po CraftOrders8.Daily - wymiana startowa nowej kampanii juz za nami): przeglad i linia doby.</summary>
        internal static void Daily()
        {
            if (Settings.Current == null || Campaign.Current == null) return;
            long t0 = Stopwatch.GetTimestamp();
            int day = (int)CampaignTime.Now.ToDays - 1;   // ta sama doba co "Kuznia (E8/K): dzien N"
            bool on = On;
            var p = new Pass();
            try { Sweep(p, on); } catch (Exception e) { Stumble("Daily(przeglad)", e); }
            try
            {
                var sb = new StringBuilder(700);
                sb.Append(Head).Append("dzien ").Append(day)
                  .Append(" | utworzone ").Append(_made).Append(", odrzucone przy tworzeniu ").Append(_rejected).Append(" (brak kiesy)")
                  .Append(", usuniete w dobie ").Append(p.Poor + p.Dead).Append(" (kiesa ").Append(p.Poor).Append(", smierc ").Append(p.Dead).Append(')')
                  .Append(Rest(p, on))
                  .Append(" | przy tworzeniu bez sprawdzenia: klucz albo platnik wylaczony ").Append(_madeOff).Append(", przed przeliczeniem cen ").Append(_early)
                  .Append(", ekran kuzni otwarty ").Append(_madeOpen).Append(", zostawione (niezgodny slot albo brak drogi gry) ").Append(_madeKept)
                  .Append(" | nagrody: odrzuconych razem ").Append(_rejectedDue).Append(", usunietych w dobie razem ").Append(p.PoorDue)
                  .Append(" | koszt ").Append(Ms(_ticks + Stopwatch.GetTimestamp() - t0)).Append(" ms/dobe")
                  .Append(" | potkniecia ").Append(_stumblesDay).Append(" (od wczytania ").Append(_stumbles).Append(").");
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Stumble("Daily", e); }
            finally { ClearDay(); }
        }

        // ------------------------------------------------------------ stan

        /// <summary>Nowa kampania i kazde wczytanie (z CraftOrders8.Reset).</summary>
        internal static void Reset()
        {
            _stumbles = 0; _errSites.Clear();
            _beh = null;
            ClearDay();
        }

        private static void ClearDay()
        {
            _stumblesDay = 0; _ticks = 0; _mismatchDay = 0;
            _made = _rejected = _madeOff = _madeOpen = _madeKept = _early = 0; _rejectedDue = 0;
        }

        // ------------------------------------------------------------ start gry (SubModuleMain - po CraftOrders8.ApplyAll)

        private static bool HasOurs(MethodBase target, string owner)
        {
            try
            {
                var info = Harmony.GetPatchInfo(target);
                if (info == null || info.Postfixes == null) return false;
                foreach (var pt in info.Postfixes)
                    if (pt != null && pt.owner == owner && pt.PatchMethod != null && pt.PatchMethod.DeclaringType == typeof(CraftOrdersPurse)) return true;
            }
            catch { }
            return false;
        }

        internal static void ApplyAll(Harmony h)
        {
            var tBeh = typeof(CraftingCampaignBehavior);
            var tSlots = typeof(CraftingCampaignBehavior.CraftingOrderSlots);
            var me = typeof(CraftOrdersPurse);
            string rm = "BRAK";
            try
            {
                var m = AccessTools.DeclaredMethod(tSlots, "RemoveTownOrder", new[] { typeof(CraftingOrder) });
                if (m != null && !m.IsStatic && m.ReturnType == typeof(void))
                    _removeTown = Delegate.CreateDelegate(typeof(Action<CraftingCampaignBehavior.CraftingOrderSlots, CraftingOrder>), m) as Action<CraftingCampaignBehavior.CraftingOrderSlots, CraftingOrder>;
                rm = _removeTown != null ? "TAK" : "BRAK (metoda " + (m != null ? "jest, delegat nie" : "nie znaleziona") + ")";
            }
            catch (Exception e) { Log.Error("CraftOrdersPurse.ApplyAll (RemoveTownOrder)", e); rm = "BRAK (wyjatek)"; }
            try
            {
                var m = PatchTarget.Declared(AccessTools.DeclaredMethod(tBeh, "CreateTownOrder", new[] { typeof(Hero), typeof(int) }));
                if (m == null) _createWhy = "metoda nie znaleziona";
                else if (m.DeclaringType != tBeh) _createWhy = "metoda nie z CraftingCampaignBehavior (" + m.DeclaringType + ")";
                else
                {
                    var r = h.Patch(m, prefix: new HarmonyMethod(me, nameof(CreatePrefix)), postfix: new HarmonyMethod(me, nameof(CreatePostfix)));
                    _wiredCreate = r != null && HasOurs(m, h.Id);
                    _createWhy = _wiredCreate ? "wpieta" : "Patch bez skutku (" + (r == null ? "null" : "brak postfiksu w GetPatchInfo") + ")";
                }
            }
            catch (Exception e) { Log.Error("CraftOrdersPurse.ApplyAll (CreateTownOrder)", e); _createWhy = "wyjatek"; }
            int n = (_wiredCreate ? 1 : 0) + (_removeTown != null ? 1 : 0);
            Log.Info(Head + "latki przy starcie gry - wpiete " + n + "/2: prefiks i postfiks CreateTownOrder (metoda deklarowana) " + _createWhy
                     + ", droga gry CraftingOrderSlots.RemoveTownOrder " + rm
                     + "; BRAK: " + (n == 2 ? "-" : (!_wiredCreate ? "CreateTownOrder" : "") + (!_wiredCreate && _removeTown == null ? ", " : "") + (_removeTown == null ? "RemoveTownOrder" : ""))
                     + " | klucz Craft Orders Giver Can Pay " + OnOff(KeyOn) + " (zlecenie tylko od zleceniodawcy z nagroda w kiesie; zlecenia bez pokrycia zdejmowane co dobe i przy starcie sesji; zlecenia zadan bez zmian).");
        }
    }
}
