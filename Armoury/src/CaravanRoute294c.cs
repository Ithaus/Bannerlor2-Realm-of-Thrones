using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Extensions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// PACZKA 294c (29.10, Jeff: "Napraw te karwany one traz nie maja sensu bo glownie traca nikt by ich nie trzymal, skoro wiekszosc traci, jedzenie sie nie psuje
    /// zrobmy, albo psuje znacznie wolniej." i "i napraw pozostale bledy i pusc test 90, rob tak dlugo porawi az bedziesz zadowlony z wyniku.") - CEL KARAWANY
    /// WEDLUG TEGO, CO NAPRAWDE SPRZEDA I KUPI.
    /// Dowod (autotest 296c = 136 + 290 + 291..297 + 294b, 90 dob na kopii zapisu Jeffa): zold pokrywa 0.503 karawan (cel 0.60), mediana (wynik - zold) -1 zl/d,
    /// sprzedaz w 7 dobach 0.71 (cel 0.80); linia 'Karawany (kierunek)' - ok. 88 wjazdow na dobe do 35-40 roznych miast (rozrzut losowy na 97 miast dalby ok. 58);
    /// karawana Jeffa 12 dob bez sprzedazy, kiesa stoi na 9 925, towar po koszcie 4 878 wobec 'rynek' 16 254, ok. 844 zl zakupu na wizyte.
    /// Przyczyna (dekompilacja BK BKCaravansBehavior): cel = max po miastach GetTradeScoreForTown = (sprzedaz + kupno) / droga. Sprzedaz
    /// CalculateTownSellScoreForCategory = (indeks miasta - srednia x limit) x min(4000, wartosc x sztuki) - bez kosztu sztuki, bez towaru, ktory inne karawany juz wioza
    /// do tego miasta, bez kasy miasta i bez spadku ceny po pierwszej sztuce; kupno CalculateTownBuyScoreForCategory - bez naszych regul zakupu 284/294b (bramka,
    /// kapital, glebokosc), wiec karawana jedzie tam, gdzie regula zablokuje kazdy zakup. Wzor latki: CaravanAmmo zeruje juz ocene kupna amunicji w tej metodzie.
    /// Regula (jedna regula na jedno zjawisko - wybor miasta widzi te same zasady sprzedazy i kupna co samo miasto; nic z niczego - zmienia sie tylko decyzja):
    ///  D1 (klucz CaravanRouteBySale) - ocena sprzedazy towaru w miescie x f = k* / maxSell: maxSell = polowa sztuk handlowych (bez spizarni eskorty; T1 sprzedaje
    ///     w miescie najwyzej polowe stosu), k* - najwiecej sztuk, z ktorych KAZDA po mycie miasta idzie nie taniej niz prog karawany (CaravanGoods.SaleFloor - koszt,
    ///     granice psucia i starego towaru) przy polce miasta powiekszonej o towar innych karawan jadacych do tego miasta (Inbound, jednostki polki) minus to, co miasto
    ///     zje do przyjazdu (CaravanTrade284.MktTownValue x dni drogi CaravanEscort.RoadDaysTo); najwyzej kasa miasta ponad rezerwe / 20 / cena pierwszej sztuki (limit
    ///     T1), surowce masowe najwyzej brak miasta (CaravanBulk.SellableAt) minus towar innych karawan w drodze;
    ///  D2 (klucz CaravanRouteByBuyRules) - ocena kupna kategorii = 0, gdy nasza regula zakupu zablokowalaby ja w tym miescie (CaravanTrade284.WhyPeek: bramka,
    ///     kapital, glebokosc 294b / zysk osiagalny, jedzenie > 7 dob, zwijanie; surowce masowe - CaravanBulk.BuyOpen);
    ///  awaryjnie: gdy po D1/D2 przebieg BK z limitem odleglosci (FindNextDestinationForCaravan distanceCut true) nie znajdzie miasta, a BK sam by je znalazl - wybor
    ///     BK z limitem bez zmian (powtorka przebiegu z obejsciem; recenzja 294c - bez tego BK szedl do drugiego przebiegu po wszystkich miastach i wybieral daleki
    ///     cel, czyli dluzszy odcinek, zold i glod w drodze); gdy po D1/D2 nie ma celu w ogole (null) - wybor BK bez zmian (drugie wywolanie z obejsciem). Liczniki obu.
    ///  Surowiec masowy (recenzja 294c): kasa miasta jak CaravanBulk.Sell (cala nadwyzka ponad rezerwe / cena pierwszej sztuki, bez 1/20 T1), prog jak CaravanBulk.Sell.
    /// Bez zmian: karawany nie handlujace (konwoje DTE), z kontraktem 174, z rozkazem BK gracza (CaravanOrdersBehavior.HasOrder - kotwica rozkazu), z zadania.
    /// Pamiec 'w drodze do miasta' (Inbound) tylko w sesji: przebudowa z celow karawan w dobie, na biezaco przy decyzji (stara wycofana, nowa dopisana) i przy wjezdzie
    /// do miasta (wycofana). Linia doby 'Cel karawan (294c): dzien N' (po linii 294b). Wyjatek - licznik potkniec (wybor BK jak dotad).
    /// </summary>
    internal static class CaravanRoute294c
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private const int TownDayShare = 20;     // limit T1 (CaravanGoods.TownDayShare): miasto wydaje na towar karawan najwyzej 1/20 kasy ponad rezerwe na dobe

        private static bool D1On(Settings s) { return s != null && s.CaravanRouteBySale; }
        private static bool D2On(Settings s) { return s != null && s.CaravanRouteByBuyRules; }
        // 139 (klucz CaravanRouteRoadCost, CaravanWait139.Route): stan decyzji i kolejka 'w drodze do miast' (Inbound) takze bez D1/D2 - cel z kosztem drogi liczy
        // zysk sprzedazy za towarem innych karawan jadacych do tego miasta (bez kolejki wszystkie karawany wybralyby to samo najblizsze miasto)
        private static bool RouteOn(Settings s) { return D1On(s) || D2On(s) || CaravanWait139.COn(s); }

        /// <summary>139: karawana w biezacej decyzji (ThinkNextDestination, nie zwolniona); null - poza decyzja.</summary>
        internal static MobileParty Deciding { get { return _bypass ? null : _mp; } }

        /// <summary>139 (CaravanWait139.SaleValue): towar innych karawan w drodze do miasta t w kategorii c - [0] sztuki, [1] jednostki polki; bez wpisu karawany self
        /// (pomiar doby - jej wlasny towar nie stoi w kolejce przed nia). null - brak.</summary>
        internal static double[] InboundFor(Town t, ItemCategory c, MobileParty self)
        {
            var x = InboundOf(t, c);
            if (x == null) return null;
            Rec own; double[] v;
            if (self != null && _in.TryGetValue(self, out own) && own != null && own.T == t && own.V.TryGetValue(c, out v))
                return new[] { Math.Max(0.0, x[0] - v[0]), Math.Max(0.0, x[1] - v[1]) };
            return x;
        }

        // ------------------------------------------------------------ stan decyzji (watek glowny)
        private static MobileParty _mp;                   // karawana w ThinkNextDestination (null - poza decyzja albo zwolniona)
        private static bool _bypass, _peek;               // drugie wywolanie awaryjne; stan regul zakupu (PeekStart)
        private static int _zeroCall;                     // oceny wyzerowane w tym wywolaniu (D1 f = 0, D2)
        private static Dictionary<ItemObject, int> _pantry;   // spizarnia eskorty karawany (kopia - PantryOf nadpisuje wspolna pamiec)
        private static Town _town;                        // miasto w GetTradeScoreForTown
        private static double _days = -1;                 // dni drogi do _town (leniwie)
        // prog sprzedazy towaru - na decyzje (recenzja 294c: SaleFloor zalezy od miasta tylko przez myto - klucz (towar, 1 - myto); czyszczony w ThinkPrefix / Postfix)
        private static readonly Dictionary<(ItemObject, double), double> _floor = new Dictionary<(ItemObject, double), double>();
        private static Rec _old;                          // wpis Inbound karawany sprzed decyzji (przywracany, gdy BK nie wybierze celu)
        private static int _passZero;                     // _zeroCall na poczatku przebiegu FindNextDestinationForCaravan

        // ------------------------------------------------------------ w drodze do miast (Inbound)
        private sealed class Rec { public Town T; public readonly Dictionary<ItemCategory, double[]> V = new Dictionary<ItemCategory, double[]>(); }   // [0] sztuki, [1] jednostki polki
        private static readonly Dictionary<MobileParty, Rec> _in = new Dictionary<MobileParty, Rec>();
        private static readonly Dictionary<Town, Dictionary<ItemCategory, double[]>> _agg = new Dictionary<Town, Dictionary<ItemCategory, double[]>>();

        // ------------------------------------------------------------ wjazdy z towarem handlowym (wjazd -> wyjazd)
        private static readonly Dictionary<MobileParty, Dictionary<ItemObject, int>> _snap = new Dictionary<MobileParty, Dictionary<ItemObject, int>>();

        // ------------------------------------------------------------ liczniki doby
        private static int _cThink, _cScore, _cSell, _cBuy;                 // wywolania latek (JIT - metoda wkompilowana w wolajacego nie wola latki)
        private static int _dec, _exempt, _decTrade, _fallback, _fallNull;
        private static int _cutFall, _cutFallNull, _farPass, _farZero;     // przebieg z limitem pusty przez nasze zera - wybor BK z limitem; drugi przebieg (bez limitu)
        private static int _cFind;
        private static int _fEval, _fZero, _fFloor, _fQueue, _fPurse, _fBulk, _fPantry;
        private static double _fSum;
        private static int _bZero, _bGate, _bCap, _bDepth, _bFood, _bWind, _bBulk;
        private static int _chosen; private static double _chosenDays;
        private static int _eTrade, _eSold;
        private static long _ticks;
        private static int _stumbles, _stumbleSess;
        private static readonly HashSet<string> _err = new HashSet<string>();
        private static bool _wThink, _wScore, _wSell, _wBuy;
        private static MethodInfo _thinkMi, _findMi;
        private static bool _wFind;
        private static MethodInfo _hasOrderMi; private static PropertyInfo _ordersInst;

        internal static void Reset()
        {
            _mp = null; _bypass = false; _peek = false; _zeroCall = 0; _passZero = 0; _pantry = null; _town = null; _days = -1; _floor.Clear(); _old = null;
            _in.Clear(); _agg.Clear(); _snap.Clear();
            ZeroDay();
            _stumbleSess = 0; _err.Clear();
        }

        private static void ZeroDay()
        {
            _cThink = _cScore = _cSell = _cBuy = 0;
            _dec = _exempt = _decTrade = _fallback = _fallNull = 0;
            _cutFall = _cutFallNull = _farPass = _farZero = 0; _cFind = 0;
            _fEval = _fZero = _fFloor = _fQueue = _fPurse = _fBulk = _fPantry = 0; _fSum = 0;
            _bZero = _bGate = _bCap = _bDepth = _bFood = _bWind = _bBulk = 0;
            _chosen = 0; _chosenDays = 0;
            _eTrade = _eSold = 0;
            _ticks = 0; _stumbles = 0;
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++; _stumbleSess++;
            try { if (_err.Add(where)) Log.Error("CaravanRoute294c." + where, e); } catch { }
        }

        // ------------------------------------------------------------ pomocnicze
        /// <summary>Towar handlowy pod ocena (jak T1 i BK: nie konie, nie juczne, nie zwierzeta).</summary>
        private static bool Goods(ItemObject it)
        {
            return it != null && it.IsTradeGood && !it.HasHorseComponent && it.ItemCategory != null && it.ItemCategory != DefaultItemCategories.PackAnimal && !it.ItemCategory.IsAnimal;
        }

        /// <summary>Karawana poza regula 294c (ocena BK bez zmian): nie handluje (konwoje DTE), kontrakt 174, rozkaz BK gracza (filtr zakupu albo kotwica), zadanie.</summary>
        private static bool Exempt(MobileParty mp)
        {
            if (mp == null || !mp.IsCaravan || !CaravanBulk.TradesForT1(mp) || MaterialOrders.HasContract(mp) || CaravanBulk.HasOrders(mp) || mp.IsCurrentlyUsedByAQuest) return true;
            if (_hasOrderMi != null && _ordersInst != null)
            {
                try
                {
                    var beh = _ordersInst.GetValue(null, null);
                    if (beh != null && _hasOrderMi.Invoke(beh, new object[] { mp }) is bool b && b) return true;
                }
                catch (Exception e) { Stumble("Exempt(HasOrder)", e); }
            }
            return false;
        }

        /// <summary>Sztuki handlowe towaru it w jukach (bez spizarni eskorty - przydzial PantryOf, najtansze najpierw).</summary>
        private static int TradeOf(ItemRoster r, ItemObject it, Dictionary<ItemObject, int> pantry)
        {
            int have = r.GetItemNumber(it), keep = 0;
            if (pantry != null && it.IsFood) pantry.TryGetValue(it, out keep);
            return Math.Max(0, have - Math.Max(0, keep));
        }

        /// <summary>Towar handlowy karawany wedlug kategorii: [0] sztuki, [1] jednostki polki (ShelfWorth x sztuki) - wpis Inbound.</summary>
        private static Rec RecOf(MobileParty mp, Town to)
        {
            var r = mp.ItemRoster;
            if (r == null || to == null) return null;
            var pantry = CaravanTrade284.PantryCopy(mp);
            Rec rec = null;
            HashSet<ItemObject> seen = null;
            for (int i = 0; i < r.Count; i++)
            {
                var el = r.GetElementCopyAtIndex(i);
                var it = el.EquipmentElement.Item;
                if (el.Amount <= 0 || !Goods(it)) continue;
                if (seen == null) seen = new HashSet<ItemObject>();
                if (!seen.Add(it)) continue;
                int n = TradeOf(r, it, pantry);
                if (n <= 0) continue;
                if (rec == null) rec = new Rec { T = to };
                double[] v;
                if (!rec.V.TryGetValue(it.ItemCategory, out v)) { v = new double[2]; rec.V[it.ItemCategory] = v; }
                v[0] += n; v[1] += n * (double)Math.Max(1, HistoricalPrices.ShelfWorth(it));
            }
            return rec;
        }

        private static void AggAdd(Rec rec, double sign)
        {
            if (rec == null || rec.T == null) return;
            Dictionary<ItemCategory, double[]> a;
            if (!_agg.TryGetValue(rec.T, out a)) { a = new Dictionary<ItemCategory, double[]>(); _agg[rec.T] = a; }
            foreach (var kv in rec.V)
            {
                double[] x;
                if (!a.TryGetValue(kv.Key, out x)) { x = new double[2]; a[kv.Key] = x; }
                x[0] = Math.Max(0.0, x[0] + sign * kv.Value[0]); x[1] = Math.Max(0.0, x[1] + sign * kv.Value[1]);
            }
        }

        private static Rec Take(MobileParty mp)
        {
            Rec rec;
            if (mp == null || !_in.TryGetValue(mp, out rec)) return null;
            _in.Remove(mp);
            AggAdd(rec, -1.0);
            return rec;
        }

        private static void Put(MobileParty mp, Rec rec)
        {
            if (mp == null || rec == null) return;
            Take(mp);
            _in[mp] = rec;
            AggAdd(rec, 1.0);
        }

        private static double[] InboundOf(Town t, ItemCategory c)
        {
            Dictionary<ItemCategory, double[]> a; double[] x;
            return t != null && c != null && _agg.TryGetValue(t, out a) && a.TryGetValue(c, out x) ? x : null;
        }

        // ------------------------------------------------------------ latki BK
        /// <summary>Prefiks BKCaravansBehavior.ThinkNextDestination: stan decyzji - zwolnienie, spizarnia, reguly zakupu, wpis Inbound karawany wycofany.</summary>
        public static void ThinkPrefix(MobileParty __0)
        {
            if (_bypass) return;
            _mp = null; _town = null; _zeroCall = 0; _passZero = 0; _old = null; _pantry = null; _peek = false; _floor.Clear();
            _cThink++;
            var s = Settings.Current;
            if (__0 == null || !RouteOn(s)) return;
            long t0 = Stopwatch.GetTimestamp();
            try
            {
                _dec++;
                _old = Take(__0);                                              // decyzja na nowo: moj towar nie stoi w kolejce do starego celu
                if (Exempt(__0)) { _exempt++; return; }
                _pantry = CaravanTrade284.PantryCopy(__0);
                _peek = D2On(s) && CaravanTrade284.PeekStart(__0);
                _mp = __0;
                var r = __0.ItemRoster;
                if (r != null)
                    for (int i = 0; i < r.Count; i++)
                    {
                        var it = r.GetElementCopyAtIndex(i).EquipmentElement.Item;
                        if (Goods(it) && TradeOf(r, it, _pantry) > 0) { _decTrade++; break; }
                    }
            }
            catch (Exception e) { _mp = null; Stumble("ThinkPrefix", e); }
            finally { _ticks += Stopwatch.GetTimestamp() - t0; }
        }

        /// <summary>Postfiks ThinkNextDestination: wynik null po wyzerowaniu ocen - wybor BK bez zmian (drugie wywolanie z obejsciem); wpis Inbound nowego celu
        /// (albo stary, gdy BK celu nie wybral); dni drogi wybranego miasta.</summary>
        public static void ThinkPostfix(object __instance, MobileParty __0, ref Town __result)
        {
            if (_bypass) return;
            var s = Settings.Current;
            if (__0 == null || !RouteOn(s)) { EndDecision(); return; }
            long t0 = Stopwatch.GetTimestamp();
            try
            {
                if (__result == null && _zeroCall > 0 && ReferenceEquals(_mp, __0) && _thinkMi != null && __instance != null)
                {
                    _bypass = true;
                    try { __result = _thinkMi.Invoke(__instance, new object[] { __0 }) as Town; }
                    catch (TargetInvocationException e) { Stumble("ThinkPostfix(BK)", e.InnerException ?? e); }
                    finally { _bypass = false; }
                    _fallback++;
                    if (__result == null) _fallNull++;
                }
                // 308a: karawana ze statkami - rejs dalszy niz Caravan Sea Near Days tylko z lepszym zyskiem na dobe niz bliski port; pomiar dni drogi morskiej
                if (ReferenceEquals(_mp, __0)) CaravanSea308.Decide(__0, ref __result);
                if (__result != null && __result.Settlement != null && CaravanBulk.TradesForT1(__0))
                {
                    var rec = RecOf(__0, __result);
                    if (rec != null) Put(__0, rec);
                    if (ReferenceEquals(_mp, __0)) { _chosen++; _chosenDays += CaravanEscort.RoadDaysTo(__0, __result.Settlement); }
                }
                else if (_old != null && _old.T != null) Put(__0, _old);    // BK nie wybral celu - karawana jedzie dalej do starego
            }
            catch (Exception e) { Stumble("ThinkPostfix", e); }
            finally
            {
                EndDecision();
                _ticks += Stopwatch.GetTimestamp() - t0;
            }
        }

        private static void EndDecision()
        {
            _mp = null; _town = null; _old = null; _pantry = null; _floor.Clear(); _zeroCall = 0; _passZero = 0;
            if (_peek) CaravanTrade284.PeekEnd();
            _peek = false;
        }

        /// <summary>Finalizer ThinkNextDestination (recenzja 294c): wyjatek w BK (wolajacy BK go polyka) - postfiks nie biegnie; stary wpis Inbound wraca, stan regul zakupu
        /// zamkniety, decyzja wyczyszczona. Wyjatek idzie dalej bez zmian (finalizer void).</summary>
        public static void ThinkFinalizer(MobileParty __0, Exception __exception)
        {
            if (_bypass || __exception == null) return;
            try { if (_old != null && _old.T != null && __0 != null) Put(__0, _old); }
            catch (Exception e) { Stumble("ThinkFinalizer", e); }
            finally { EndDecision(); }
        }

        /// <summary>Prefiks FindNextDestinationForCaravan(caravan, distanceCut): poczatek przebiegu - zera D1/D2 liczone od tego miejsca.</summary>
        public static void FindPrefix(MobileParty __0)
        {
            if (_bypass) return;
            _cFind++;
            if (_mp != null && ReferenceEquals(__0, _mp)) { _passZero = _zeroCall; CaravanWait139.PassStart(); }   // 139: oceny miast BK tego przebiegu od nowa
        }

        /// <summary>Postfiks FindNextDestinationForCaravan: przebieg z limitem odleglosci pusty, a w nim zerowalismy oceny - wybor BK z limitem bez zmian (powtorka przebiegu
        /// z obejsciem), zeby nasze zera nie wypychaly karawany do drugiego przebiegu BK po wszystkich miastach (daleki cel). Drugi przebieg - licznik.</summary>
        public static void FindPostfix(object __instance, MobileParty __0, bool __1, ref Town __result)
        {
            if (_bypass || _mp == null || !ReferenceEquals(__0, _mp)) return;
            long t0 = Stopwatch.GetTimestamp();
            try
            {
                bool zeroed = _zeroCall > _passZero;
                if (!__1) { _farPass++; if (zeroed) _farZero++; CaravanLeg140.NoteSecond(__0); CaravanSea308.NoteFar(__0); return; }   // 140: drugi przebieg karawany utknietej - licznik
                if (__result == null && zeroed && _findMi != null && __instance != null)
                {
                    _bypass = true;
                    try { __result = _findMi.Invoke(__instance, new object[] { __0, true }) as Town; }
                    catch (TargetInvocationException e) { Stumble("FindPostfix(BK)", e.InnerException ?? e); }
                    finally { _bypass = false; }
                    _cutFall++;
                    if (__result == null) _cutFallNull++;
                }
                // 139 (klucz CaravanRouteRoadCost): wybor BK z limitem odleglosci b* - karawana z ladunkiem albo utknieta moze wziac miasto nie dalsze, w ktorym zysk
                // sprzedazy po koszcie dni drogi na dobe jest wiekszy (CaravanWait139.Route); ocen BK nie zmienia, drugi przebieg BK bez zmian
                if (__result != null) CaravanWait139.Route(__0, ref __result, __instance);
                // 140 (klucz CaravanStuckSeekPayingTown): pierwszy przebieg pusty, karawana utknieta - miasto placace w zasiegu odcinka zamiast drugiego przebiegu BK
                else CaravanWait139.RouteEmpty(__0, ref __result, __instance);
            }
            catch (Exception e) { Stumble("FindPostfix", e); }
            finally { _ticks += Stopwatch.GetTimestamp() - t0; }
        }

        /// <summary>Prefiks GetTradeScoreForTown(caravan, town, ...): miasto oceny (dni drogi i progi leniwie).</summary>
        public static void ScorePrefix(MobileParty __0, Town __1)
        {
            if (_bypass) return;
            _cScore++;
            if (_mp == null || !ReferenceEquals(__0, _mp)) { _town = null; return; }
            _town = __1; _days = -1;
        }

        public static void ScorePostfix() { if (!_bypass) _town = null; }

        /// <summary>Postfiks CalculateTownSellScoreForCategory(party, marketData, i, limit): D1 - ocena sprzedazy x f = k* / maxSell.</summary>
        public static void SellScorePostfix(MobileParty __0, TownMarketData __1, int __2, ref float __result)
        {
            if (_bypass) return;
            _cSell++;
            if (!(__result > 0f) || _mp == null || _town == null || !ReferenceEquals(__0, _mp)) return;
            var s = Settings.Current;
            if (!D1On(s)) return;
            long t0 = Stopwatch.GetTimestamp();
            try
            {
                var town = _town;
                if (!ReferenceEquals(__1, town.MarketData)) return;
                var r = __0.ItemRoster;
                if (r == null || __2 < 0 || __2 >= r.Count) return;
                var el = r.GetElementCopyAtIndex(__2);
                var it = el.EquipmentElement.Item;
                if (!Goods(it)) return;                                        // konie, juczne, zwierzeta, lup - ocena BK bez zmian
                _fEval++;
                double f = Factor(__0, town, el, it, s);
                _fSum += f;
                if (f <= 0.0) { _fZero++; _zeroCall++; }
                __result = (float)(__result * f);
            }
            catch (Exception e) { Stumble("SellScorePostfix", e); }
            finally { _ticks += Stopwatch.GetTimestamp() - t0; }
        }

        /// <summary>D1: f = k* / maxSell (0..1) dla elementu jukow el w miescie town; przyczyna zera do licznikow.</summary>
        private static double Factor(MobileParty mp, Town town, ItemRosterElement el, ItemObject it, Settings s)
        {
            var r = mp.ItemRoster;
            int keep = 0;
            if (_pantry != null && it.IsFood) _pantry.TryGetValue(it, out keep);
            // spizarnia przydzielona na przedmiot - element (modyfikator) dostaje swoja czesc: sztuki elementu ponad to, co ze spizarni nie miesci sie w innych elementach
            int have = r.GetItemNumber(it);
            int trade = Math.Min(el.Amount, Math.Max(0, have - Math.Max(0, keep)));
            if (trade <= 0) { _fPantry++; return 0.0; }
            bool bulk = CaravanBulk.Owns(it);
            int maxSell = bulk ? trade : (trade + 1) / 2;                 // T1: najwyzej polowa stosu w miescie; surowiec masowy (CaravanBulk.Sell) - caly brak miasta
            var cg = Campaign.Current;
            var model = cg != null && cg.Models != null ? cg.Models.TradeItemPriceFactorModel : null;
            var st = town.Settlement;
            if (model == null || st == null || town.MarketData == null) return 1.0;
            double keepT = 1.0 - TownToll.CaravanRateFor(mp, st);
            double floor;
            if (!_floor.TryGetValue((it, keepT), out floor)) { floor = CaravanGoods.SaleFloor(mp, it, st); _floor[(it, keepT)] = floor; }
            var cat = it.ItemCategory;
            if (_days < 0) _days = Math.Max(0.0, CaravanEscort.RoadDaysTo(mp, st));
            var inb = InboundOf(town, cat);
            double eat = CaravanTrade284.MktTownValue(town, cat) * _days;
            double q = inb != null ? Math.Max(0.0, inb[1] - eat) : 0.0;           // jednostki polki innych karawan, ktore beda tam przede mna
            double qn = inb != null && inb[1] > 0 ? Math.Max(0.0, inb[0] * (q / inb[1])) : 0.0;   // to samo w sztukach (surowce masowe)
            var d = town.MarketData.GetCategoryData(cat);
            int wv = Math.Max(1, HistoricalPrices.ShelfWorth(it));
            var ee = el.EquipmentElement;
            int k = floor > 0.0 ? MaxK(model, ee, mp, d, q, wv, keepT, floor, maxSell) : maxSell;
            if (k <= 0)
            {
                if (q > 0.0 && MaxK(model, ee, mp, d, 0.0, wv, keepT, floor, maxSell) > 0) _fQueue++; else _fFloor++;
                return 0.0;
            }
            // kasa miasta: towar T1 - limit T1 (1/20 kasy ponad rezerwe kupcow na dobe) / cena pierwszej sztuki; surowiec masowy - regula CaravanBulk.Sell (cala kasa ponad
            // rezerwe / cena pierwszej sztuki, bez 1/20 - recenzja 294c)
            int reserve = (int)Math.Max(0f, s.TownRentFloorGold);
            int spare = town.Gold - TownPurse.BuyFloorPeek(town, reserve);
            int p1 = Math.Max(1, CaravanGoods.PriceAtExtra(model, ee, mp, d, q, 1, wv));
            int kc = spare > 0 ? (bulk ? spare / p1 : spare / TownDayShare / p1) : 0;
            if (kc <= 0) { _fPurse++; return 0.0; }
            k = Math.Min(k, kc);
            // surowiec masowy: najwyzej brak miasta (do zapasu docelowego; przy zamknietym kupnie do progu nadwyzki) minus towar w drodze (174 i inne karawany)
            if (bulk)
            {
                int lack = CaravanBulk.SellableAt(town, it);
                if (lack >= 0)
                {
                    int left = Math.Max(0, lack - (int)Math.Ceiling(qn));
                    if (left <= 0) { _fBulk++; return 0.0; }
                    k = Math.Min(k, left);
                }
            }
            return Math.Max(0.0, Math.Min(1.0, k / (double)maxSell));
        }

        /// <summary>Najwiecej sztuk k (0..n), z ktorych k-ta po mycie przy polce + q kosztuje co najmniej floor (cena maleje z k - wyszukiwanie polowkowe).</summary>
        private static int MaxK(TradeItemPriceFactorModel model, EquipmentElement ee, MobileParty mp, ItemData d, double q, int wv, double keepT, double floor, int n)
        {
            int lo = 0, hi = n;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) / 2;
                if (CaravanGoods.PriceAtExtra(model, ee, mp, d, q, mid, wv) * keepT >= floor) lo = mid; else hi = mid - 1;
            }
            return lo;
        }

        /// <summary>Postfiks CalculateTownBuyScoreForCategory(marketData, index): D2 - kategoria, ktorej nasza regula tu nie kupi, nie przyciaga karawany.</summary>
        public static void BuyScorePostfix(TownMarketData __0, int __1, ref float __result)
        {
            if (_bypass) return;
            _cBuy++;
            if (!(__result > 0f) || _mp == null || _town == null) return;
            var s = Settings.Current;
            if (!D2On(s)) return;
            long t0 = Stopwatch.GetTimestamp();
            try
            {
                var town = _town;
                if (!ReferenceEquals(__0, town.MarketData)) return;
                var all = ItemCategories.All;
                if (all == null || __1 < 0 || __1 >= all.Count) return;
                var cat = all[__1];
                if (CaravanTrade284.Tracked(cat))
                {
                    if (!_peek) return;
                    int why = CaravanTrade284.WhyPeek(_mp, cat, town);
                    if (why == 0) return;
                    switch (why) { case 1: _bGate++; break; case 2: _bCap++; break; case 4: _bDepth++; break; case 5: _bFood++; break; default: _bWind++; break; }
                }
                else if (CaravanBulk.OwnsCategory(cat))
                {
                    if (CaravanBulk.BuyOpen(_mp, town, cat)) return;
                    _bBulk++;
                }
                else return;
                __result = 0f; _bZero++; _zeroCall++;
            }
            catch (Exception e) { Stumble("BuyScorePostfix", e); }
            finally { _ticks += Stopwatch.GetTimestamp() - t0; }
        }

        // ------------------------------------------------------------ zdarzenia (wjazd / wyjazd) - ArmouryBehavior
        /// <summary>SettlementEntered (sluchacz dopisany po sprzedazach przy wjezdzie - biegnie przed nimi): karawana dojechala - wpis Inbound zdjety; migawka towaru
        /// handlowego (czy wjazd z towarem skonczy sie sprzedaza).</summary>
        internal static void OnEntered(MobileParty mp, Settlement st, Hero hero)
        {
            if (mp == null || st == null || !mp.IsCaravan || !st.IsTown) return;
            try
            {
                if (_in.Count > 0) Take(mp);
                var s = Settings.Current;
                if (!RouteOn(s) || !CaravanBulk.TradesForT1(mp) || mp.ItemRoster == null) return;
                var r = mp.ItemRoster;
                var pantry = CaravanTrade284.PantryCopy(mp);
                Dictionary<ItemObject, int> snap = null;
                for (int i = 0; i < r.Count; i++)
                {
                    var it = r.GetElementCopyAtIndex(i).EquipmentElement.Item;
                    if (!Goods(it)) continue;
                    if (snap != null && snap.ContainsKey(it)) continue;
                    int n = TradeOf(r, it, pantry);
                    if (n <= 0) continue;
                    if (snap == null) snap = new Dictionary<ItemObject, int>();
                    snap[it] = r.GetItemNumber(it);
                }
                if (snap != null) _snap[mp] = snap; else _snap.Remove(mp);
            }
            catch (Exception e) { Stumble("OnEntered", e); }
        }

        /// <summary>OnSettlementLeft: wjazd z towarem handlowym skonczony sprzedaza (ktorys towar w jukach mniej niz przy wjezdzie - kazda droga: T1, BK, gra, surowce).</summary>
        internal static void OnLeft(MobileParty mp, Settlement st)
        {
            if (mp == null || _snap.Count == 0) return;
            try
            {
                Dictionary<ItemObject, int> snap;
                if (!_snap.TryGetValue(mp, out snap)) return;
                _snap.Remove(mp);
                if (st == null || !st.IsTown || mp.ItemRoster == null) return;
                _eTrade++;
                foreach (var kv in snap) if (mp.ItemRoster.GetItemNumber(kv.Key) < kv.Value) { _eSold++; break; }
            }
            catch (Exception e) { Stumble("OnLeft", e); }
        }

        // ------------------------------------------------------------ doba
        /// <summary>Linia doby 'Cel karawan (294c): dzien N' (po linii 294b, CaravanTrade284.Daily) i przebudowa Inbound z celow karawan w drodze.</summary>
        internal static void Daily()
        {
            var s = Settings.Current;
            if (s == null || Campaign.Current == null) return;
            long t0 = Stopwatch.GetTimestamp();
            int today = (int)CampaignTime.Now.ToDays, day = today - 1;
            int cars = 0, withTrade = 0, withSold = 0, ore = 0, oreShort = 0, inCars = 0;
            double inUnits = 0, inValue = 0;
            bool on = RouteOn(s);
            try
            {
                _in.Clear(); _agg.Clear();
                var oreIt = CaravanBulk.OreItem();
                var gone = new List<MobileParty>();
                foreach (var kv in _snap) if (kv.Key == null || !kv.Key.IsActive || kv.Key.CurrentSettlement == null) gone.Add(kv.Key);
                foreach (var k in gone) _snap.Remove(k);
                foreach (var mp in MobileParty.AllCaravanParties)
                {
                    if (mp == null || !mp.IsActive || mp.ItemRoster == null || !CaravanBulk.TradesForT1(mp)) continue;
                    cars++;
                    var r = mp.ItemRoster;
                    var pantry = CaravanTrade284.PantryCopy(mp);
                    bool has = false;
                    for (int i = 0; i < r.Count && !has; i++)
                    {
                        var it = r.GetElementCopyAtIndex(i).EquipmentElement.Item;
                        if (Goods(it) && TradeOf(r, it, pantry) > 0) has = true;
                    }
                    if (has) { withTrade++; if (CaravanTrade284.SoldIn7(mp, day)) withSold++; }
                    var tg = mp.TargetSettlement;
                    var tt = tg != null && tg.IsTown ? tg.Town : null;
                    if (oreIt != null && r.GetItemNumber(oreIt) > 0) { ore++; if (tt != null && mp.CurrentSettlement == null && CaravanBulk.OreShort(tt)) oreShort++; }
                    // w miescie bez nowego celu (cel = to miasto) - cel wybierze przy wyjezdzie; w miescie z nowym celem (decyzja zapadla, wyjazd w nastepnym ticku) - wpis zostaje
                    if (!on || tt == null || (mp.CurrentSettlement != null && mp.CurrentSettlement == tg)) continue;
                    var rec = RecOf(mp, tt);
                    if (rec == null) continue;
                    Put(mp, rec);
                    inCars++;
                    foreach (var v in rec.V.Values) { inUnits += v[0]; inValue += v[1]; }
                }
            }
            catch (Exception e) { Stumble("Daily", e); }
            double ms = (Stopwatch.GetTimestamp() - t0 + _ticks) * 1000.0 / Stopwatch.Frequency;
            var sb = new StringBuilder(1400);
            sb.Append("Cel karawan (294c): dzien ").Append(day)
              .Append(" | klucze: cel wedlug sprzedazy ").Append(D1On(s) ? "TAK" : "NIE").Append(", ocena kupna wedlug regul ").Append(D2On(s) ? "TAK" : "NIE")
              .Append(" | latki: mysl ").Append(_wThink ? "TAK" : "NIE").Append(", ocena miasta ").Append(_wScore ? "TAK" : "NIE").Append(", sprzedaz ").Append(_wSell ? "TAK" : "NIE")
              .Append(", kupno ").Append(_wBuy ? "TAK" : "NIE")
              .Append(" | wywolania: mysl ").Append(_cThink).Append(", ocena miast ").Append(_cScore).Append(", sprzedaz ").Append(_cSell).Append(", kupno ").Append(_cBuy)
              .Append(" | decyzje ").Append(_dec).Append(" (zwolnione ").Append(_exempt).Append(", z towarem handlowym ").Append(_decTrade).Append(')')
              .Append(" | sprzedaz: ocen ").Append(_fEval).Append(", f = 0 ").Append(_fZero).Append(" (pod progiem ").Append(_fFloor).Append(", kolejka ").Append(_fQueue)
              .Append(", kasa miasta ").Append(_fPurse).Append(", brak surowca ").Append(_fBulk).Append(", spizarnia ").Append(_fPantry).Append("), f srednio ")
              .Append(_fEval > 0 ? (_fSum / _fEval).ToString("0.000", Inv) : "-")
              .Append(" | kupno wyzerowane ").Append(_bZero).Append(" (bramka ").Append(_bGate).Append(", kapital ").Append(_bCap).Append(", glebokosc ").Append(_bDepth)
              .Append(", jedzenie ").Append(_bFood).Append(", zwijanie ").Append(_bWind).Append(", surowce ").Append(_bBulk).Append(')')
              .Append(" | awaryjnie wybor BK ").Append(_fallback).Append(" (bez celu i tak ").Append(_fallNull).Append(')')
              .Append(" | limit odleglosci: wpieta ").Append(_wFind ? "TAK" : "NIE").Append(", przebiegow ").Append(_cFind).Append(", pusty po zerowaniu - wybor BK z limitem ")
              .Append(_cutFall).Append(" (bez celu i tak ").Append(_cutFallNull).Append("), drugi przebieg bez limitu ").Append(_farPass).Append(" (z zerowaniem ").Append(_farZero).Append(')')
              .Append(" | w drodze do miast: karawan ").Append(inCars).Append(", szt. ").Append(inUnits.ToString("0", Inv)).Append(", jednostek polki ").Append(inValue.ToString("0", Inv))
              .Append(" | dni drogi wybranych miast srednio ").Append(_chosen > 0 ? (_chosenDays / _chosen).ToString("0.00", Inv) : "-").Append(" (").Append(_chosen).Append(" decyzji)")
              .Append(" | wjazdy z towarem handlowym (przy wyjezdzie) ").Append(_eTrade).Append(", ze sprzedaza ").Append(_eSold)
              .Append(" | karawany handlujace ").Append(cars).Append(", z towarem handlowym ").Append(withTrade).Append(", z nich sprzedaly w 7 dobach ").Append(withSold)
              .Append(" | ruda: karawan z ruda ").Append(ore).Append(", w drodze do miast z brakiem ").Append(oreShort)
              .Append(" | czas ").Append(ms.ToString("0.00", Inv)).Append(" ms (na decyzje ").Append(_dec > 0 ? (ms / _dec).ToString("0.000", Inv) : "-").Append(" ms) | potkniecia ").Append(_stumbles).Append(" (sesja ").Append(_stumbleSess).Append(").");
            Log.Info(sb.ToString());
            ZeroDay();
        }

        // ------------------------------------------------------------ wpiecie
        private static bool Fits(MethodInfo m, Type ret, params Type[] args)
        {
            if (m == null || m.ReturnType != ret) return false;
            var ps = m.GetParameters();
            if (ps.Length < args.Length) return false;
            for (int i = 0; i < args.Length; i++) if (ps[i].ParameterType != args[i]) return false;
            return true;
        }

        internal static void ApplyAll(Harmony h)
        {
            var miss = new List<string>();
            try
            {
                var bkc = AccessTools.TypeByName("BannerKings.Behaviours.BKCaravansBehavior");
                if (bkc == null) { Log.Info("CaravanRoute294c (294c): BRAK BannerKings.Behaviours.BKCaravansBehavior - cel karawan BK bez zmian."); return; }
                var think = AccessTools.Method(bkc, "ThinkNextDestination", new[] { typeof(MobileParty) });
                var score = AccessTools.Method(bkc, "GetTradeScoreForTown");
                var sell = AccessTools.Method(bkc, "CalculateTownSellScoreForCategory", new[] { typeof(MobileParty), typeof(TownMarketData), typeof(int), typeof(float) });
                var buy = AccessTools.Method(bkc, "CalculateTownBuyScoreForCategory", new[] { typeof(TownMarketData), typeof(int) });
                var find = AccessTools.Method(bkc, "FindNextDestinationForCaravan", new[] { typeof(MobileParty), typeof(bool) });
                try
                {
                    var tco = AccessTools.TypeByName("BannerKings.Behaviours.Caravans.CaravanOrdersBehavior");
                    _ordersInst = tco != null ? AccessTools.Property(tco, "Instance") : null;
                    _hasOrderMi = tco != null ? AccessTools.Method(tco, "HasOrder", new[] { typeof(MobileParty) }) : null;
                    if (_ordersInst == null || _hasOrderMi == null || _hasOrderMi.ReturnType != typeof(bool)) { _ordersInst = null; _hasOrderMi = null; miss.Add("CaravanOrdersBehavior.HasOrder"); }
                }
                catch (Exception e) { _ordersInst = null; _hasOrderMi = null; miss.Add("CaravanOrdersBehavior.HasOrder"); Log.Error("CaravanRoute294c.ApplyAll(rozkazy)", e); }
                // kolejnosc: najpierw metody wewnetrzne, na koncu mysl (bez myslenia nic sie nie liczy)
                if (Fits(sell, typeof(float), typeof(MobileParty), typeof(TownMarketData), typeof(int), typeof(float)))
                { h.Patch(sell, postfix: new HarmonyMethod(typeof(CaravanRoute294c), nameof(SellScorePostfix))); _wSell = true; }
                else miss.Add("CalculateTownSellScoreForCategory(MobileParty, TownMarketData, int, float)");
                if (Fits(buy, typeof(float), typeof(TownMarketData), typeof(int)))
                { h.Patch(buy, postfix: new HarmonyMethod(typeof(CaravanRoute294c), nameof(BuyScorePostfix))); _wBuy = true; }
                else miss.Add("CalculateTownBuyScoreForCategory(TownMarketData, int)");
                if (Fits(score, typeof(float), typeof(MobileParty), typeof(Town)))
                { h.Patch(score, prefix: new HarmonyMethod(typeof(CaravanRoute294c), nameof(ScorePrefix)) { priority = Priority.First }, postfix: new HarmonyMethod(typeof(CaravanRoute294c), nameof(ScorePostfix))); _wScore = true; }
                else miss.Add("GetTradeScoreForTown(MobileParty, Town, ..)");
                if (_wScore && Fits(find, typeof(Town), typeof(MobileParty), typeof(bool)))
                {
                    h.Patch(find, prefix: new HarmonyMethod(typeof(CaravanRoute294c), nameof(FindPrefix)), postfix: new HarmonyMethod(typeof(CaravanRoute294c), nameof(FindPostfix)));
                    _findMi = find; _wFind = true;
                }
                else miss.Add("FindNextDestinationForCaravan(MobileParty, bool)");
                if (_wScore && Fits(think, typeof(Town), typeof(MobileParty)))
                {
                    h.Patch(think, prefix: new HarmonyMethod(typeof(CaravanRoute294c), nameof(ThinkPrefix)), postfix: new HarmonyMethod(typeof(CaravanRoute294c), nameof(ThinkPostfix)),
                            finalizer: new HarmonyMethod(typeof(CaravanRoute294c), nameof(ThinkFinalizer)));
                    _thinkMi = think; _wThink = true;
                }
                else miss.Add("ThinkNextDestination(MobileParty)");
            }
            catch (Exception e) { Log.Error("CaravanRoute294c.ApplyAll", e); }
            Log.Info("CaravanRoute294c (294c): cel karawan BK - mysl " + (_wThink ? "wpieta" : "BRAK") + ", przebieg z limitem " + (_wFind ? "wpiety" : "BRAK") + ", ocena miasta " + (_wScore ? "wpieta" : "BRAK")
                     + ", ocena sprzedazy " + (_wSell ? "wpieta" : "BRAK") + ", ocena kupna " + (_wBuy ? "wpieta" : "BRAK")
                     + (miss.Count > 0 ? "; BRAK w kodzie BK: " + string.Join(", ", miss.ToArray()) + " - ta czesc nie dziala" : "") + ".");
        }
    }
}
