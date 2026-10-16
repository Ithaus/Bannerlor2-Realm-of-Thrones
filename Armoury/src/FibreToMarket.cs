using System;
using System.Collections.Generic;
using System.Globalization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// N2-E3 (noc 16.10, galaz noc-ekonomia2; raport audytu ekonomii 15.10 rozdz. 3.2 P2, wariant prosty bez zapisu) - LEN, WELNA I SKORY ZE WSI DO MIASTA TARGOWEGO
    /// (klucz VillageFibreToMarket).
    /// Dowod (log Jeffa 2026-10-15_10-56-30, 12:10:56 "Towary: dzien 108945"): len - produkcja 687 (flax_plant), posiadacze: wsie 2632, WOZY 5121, miasta 1555
    /// (82% we wsiach i na wozach; 7.5 doby produkcji w jukach - zboze 1.8, ryby 2.2, welna 1.7), "Ceny surowcow": len bez towaru w 69 miastach, z nadwyzka w 1;
    /// welna - wsie 1062, wozy 578, bez towaru w 50 miastach. Skory surowe dzis robia tylko rzeznicy miast (wsie ich nie maja) - regula je obejmuje, gdyby wies je miala.
    /// Przyczyna [K]: woz wsi wybiera miasto najlepiej placace za CALY ladunek na dobe kursu (MarketCarts.Choose, poprawka 119, w zasiegu MarketMaxDistance 250);
    /// len w miescie targowym wsi lnianej stoi ponizej ceny bazowej (indeks min 0.15 - tam juz lezy), wiec woz wiezie go do dalekiego miasta bez lnu, a kurs
    /// jest dluzszy niz 5 dob, na ktore starcza magazynu (Village.GetWarehouseCapacity = 5 dob produkcji): towar lezy w jukach i w magazynie, a od 1.5 magazynu
    /// gra wstrzymuje produkcje wsi (VillageGoodProductionCampaignBehavior). Kontrakty surowca 174 (MaterialOrders) biora zrodlo tylko z miast z nadwyzka -
    /// "brak zrodla z nadwyzka w zasiegu": len 83, skory 80, welna 68 miast (audyt rozdz. 3.2 P2).
    /// Regula (DECYZJA CLAUDE noc 16.10, N = FibreDays 6 dob - miedzy 5 dobami magazynu a 7.5 doby, przy ktorych wies staje): gdy woz wiezie len, welne albo skory
    /// surowe, a zapas tego towaru wsi (juki tego wozu + magazyn wsi) przekracza N dob jego produkcji we wsi (model produkcji wsi, ten sam co tick), woz jedzie
    /// do MIASTA TARGOWEGO wsi (wies miejska - jej miasto, wies zamkowa - TradeBound; to samo "wlasne miasto", ktore Choose porownuje w linii "Dowoz") i sprzedaje
    /// tam caly ladunek takze ponizej ceny bazowej - krotki kurs zamiast najlepszej ceny. Miasto targowe musi byc kandydatem Choose (nie oblezone, nie wrogie,
    /// wies zamkowa - w zasiegu) i zaplacic za ladunek wiecej niz 0. Nastepny wyjazd z mniejszym zapasem znowu wybiera najlepsza cene (regula sama sie wylacza).
    /// Dalej towar z miasta targowego rozwoza kontrakty 174 i wozy kupcow (MaterialCarts) - miasto ma teraz nadwyzke, ktora moze byc ich zrodlem.
    /// Platnik i odbiorca bez zmian: kasa miasta targowego -> sakwa wozu (sprzedaz gry / BK, cena sztuka po sztuce 119) -> kiesa wsi i pan (utarg wsi 112).
    /// Ksiega towarow: sprzedaz wozu jak kazda (ramka sprzedazy wozow) - nic nie liczymy drugi raz. Liczniki: wozy skierowane, sztuki wlokna w ich jukach, wycena
    /// w miescie targowym wobec najlepszego miasta; przy sprzedazy w miescie celu - sprzedane sztuki wlokna i utarg wozu (po zwrocie nadplaty 119).
    /// Nic w zapisie (pamiec wozow skierowanych - sesja; po wczytaniu woz w drodze sprzedaje, tylko bez licznika). Wyjatek - potkniecie, wybor jak dotad.
    /// </summary>
    internal static class FibreToMarket
    {
        internal const double FibreDays = 6.0;
        private static readonly string[] FibreIds = { "flax", "wool", "hides" };
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private sealed class Trip { public Settlement To; public int Units; public double Day; }
        private static readonly Dictionary<MobileParty, Trip> _trips = new Dictionary<MobileParty, Trip>();
        private static readonly Dictionary<ItemObject, int> _pre = new Dictionary<ItemObject, int>();
        private static MobileParty _preCart; private static int _preGold;

        // liczniki od poprzedniej linii doby
        private static int _checked, _over, _sent, _sentCastle, _noOwn, _ownZero, _soldCarts, _stumbles, _stDay, _again;
        private static long _sentUnits, _valOwn, _valBest, _soldUnits, _soldGold;
        private static double _maxDays;
        private static readonly Dictionary<string, long> _soldBy = new Dictionary<string, long>(StringComparer.Ordinal);
        private static bool _errLogged;

        internal static bool On { get { var s = Settings.Current; return s != null && s.VillageFibreToMarket; } }

        private static bool Fibre(ItemObject it)
        {
            if (it == null || it.StringId == null) return false;
            return Array.IndexOf(FibreIds, it.StringId) >= 0;
        }

        private static void Stumble(Exception e)
        {
            _stumbles++; _stDay++;
            if (!_errLogged) { _errLogged = true; Log.Error("FibreToMarket", e); }
        }

        internal static void Reset()
        {
            _trips.Clear(); _pre.Clear(); _preCart = null; _preGold = 0;
            _stumbles = 0; _errLogged = false;
            ClearDay();
        }

        private static void ClearDay()
        {
            _checked = _over = _sent = _sentCastle = _noOwn = _ownZero = _soldCarts = _stDay = _again = 0;
            _sentUnits = _valOwn = _valBest = _soldUnits = _soldGold = 0;
            _maxDays = 0;
            _soldBy.Clear();
        }

        /// <summary>
        /// MarketCarts.Choose, po wyborze najlepszego miasta: czy ten woz ma jechac do miasta targowego (zapas wlokna wsi ponad FibreDays dob produkcji).
        /// units - sztuki wlokna w ladunku (juki), days - najwiekszy zapas w dobach. Liczy rowniez wozy bez miasta targowego w kandydatach (noOwn). Nie rzuca.
        /// </summary>
        internal static bool Wants(Village v, Settlement home, List<MarketCarts.Line> load, bool ownIsCandidate, out int units, out double days)
        {
            units = 0; days = 0;
            try
            {
                if (!On || v == null || load == null) return false;
                // wlokno w jukach (jednostki do licznika) i zapas wsi: juki + magazyn
                Dictionary<ItemObject, int> have = null;
                foreach (var l in load)
                {
                    var it = l.El.Item;
                    if (!Fibre(it) || l.N <= 0) continue;
                    if (have == null) have = new Dictionary<ItemObject, int>();
                    int n; have.TryGetValue(it, out n); have[it] = n + l.N;
                    units += l.N;
                }
                if (have == null) return false;
                _checked++;
                var store = home != null ? home.ItemRoster : null;
                var model = Campaign.Current.Models.VillageProductionCalculatorModel;
                foreach (var kv in have)
                {
                    int stock = kv.Value + (store != null ? store.GetItemNumber(kv.Key) : 0);
                    float prod = 0f;
                    try { prod = model.CalculateDailyProductionAmount(v, kv.Key).ResultNumber; } catch (Exception e) { Stumble(e); prod = 0f; }
                    if (!(prod > 0.01f)) continue;                              // wies tego nie wytwarza - nie jej zapas
                    double d = stock / (double)prod;
                    if (d > days) days = d;
                }
                if (days > _maxDays) _maxDays = days;
                if (!(days > FibreDays)) return false;
                _over++;
                if (!ownIsCandidate) { _noOwn++; return false; }
                return true;
            }
            catch (Exception e) { Stumble(e); return false; }
        }

        /// <summary>Sztuki wlokna w ladunku (poprawka po recenzji 16.10: licznik z ladunku wybranego kandydata - z doladowaniem TopUp z magazynu, gdy pelny woz).</summary>
        internal static int Units(List<MarketCarts.Line> load)
        {
            int u = 0;
            if (load == null) return 0;
            foreach (var l in load) if (Fibre(l.El.Item) && l.N > 0) u += l.N;
            return u;
        }

        /// <summary>MarketCarts.Choose: wycena w miescie targowym 0 - zostaje najlepsze miasto (licznik).</summary>
        internal static void NoteOwnZero() { _ownZero++; }

        /// <summary>MarketCarts.Choose: woz skierowany do miasta targowego - zapamietany do sprzedazy (licznik sztuk i zlota przy wjezdzie). Poprawka po recenzji 16.10:
        /// drugi wybor tego samego kursu (woz wsi zamkowej po przystanku w zamku, CastleMarket.FirstStop -> SellPrefix -> PickTown) z tym samym celem nie liczy sie
        /// drugi raz - tylko odswieza pamiec wozu.</summary>
        internal static void NoteSent(MobileParty cart, Settlement to, bool castle, int units, long valOwn, long valBest)
        {
            try
            {
                Trip old;
                if (cart != null && to != null && _trips.TryGetValue(cart, out old) && old != null && old.To == to) { old.Units = units; _again++; return; }
                _sent++; if (castle) _sentCastle++;
                _sentUnits += units; _valOwn += valOwn; _valBest += valBest;
                if (cart != null && to != null) _trips[cart] = new Trip { To = to, Units = units, Day = CampaignTime.Now.ToDays };
            }
            catch (Exception e) { Stumble(e); }
        }

        /// <summary>MarketCarts.Choose dla wozu bez skierowania: poprzednie skierowanie tego wozu wygasa (nowy wybor) - chyba ze nowy wybor to ten sam cel (drugi
        /// odcinek kursu wozu wsi zamkowej, gdy miasto targowe wygralo samo: sprzedaz w nim dalej liczy sie do skierowania).</summary>
        internal static void Forget(MobileParty cart, Settlement chosen)
        {
            if (cart == null || _trips.Count == 0) return;
            Trip t;
            if (_trips.TryGetValue(cart, out t) && t != null && chosen != null && t.To == chosen) return;
            _trips.Remove(cart);
        }

        /// <summary>MarketCarts.SellPrefix: woz skierowany wjezdza do miasta celu - migawka wlokna w jukach i sakwy (przed sprzedaza gry / BK).</summary>
        internal static void SellBegin(Settlement at, MobileParty cart)
        {
            _preCart = null;
            try
            {
                Trip t;
                if (cart == null || at == null || _trips.Count == 0 || !_trips.TryGetValue(cart, out t) || t.To != at) return;
                _pre.Clear();
                var r = cart.ItemRoster;
                for (int i = 0; i < r.Count; i++)
                {
                    var el = r.GetElementCopyAtIndex(i);
                    var it = el.EquipmentElement.Item;
                    if (!Fibre(it) || el.Amount <= 0) continue;
                    int n; _pre.TryGetValue(it, out n); _pre[it] = n + el.Amount;
                }
                _preCart = cart; _preGold = cart.PartyTradeGold;
            }
            catch (Exception e) { _preCart = null; Stumble(e); }
        }

        /// <summary>MarketCarts.SellPostfix (po zwrocie nadplaty 119): sprzedane sztuki wlokna i utarg wozu w miescie celu; skierowanie wygasa.</summary>
        internal static void SellEnd(Settlement at, MobileParty cart)
        {
            try
            {
                if (cart == null || !ReferenceEquals(cart, _preCart)) return;
                _preCart = null;
                _trips.Remove(cart);
                _soldCarts++;
                _soldGold += Math.Max(0, cart.PartyTradeGold - _preGold);
                foreach (var kv in _pre)
                {
                    int left = cart.ItemRoster.GetItemNumber(kv.Key);
                    int sold = kv.Value - left;
                    if (sold <= 0) continue;
                    _soldUnits += sold;
                    string id = kv.Key.StringId ?? "?";
                    long v; _soldBy.TryGetValue(id, out v); _soldBy[id] = v + sold;
                }
                _pre.Clear();
            }
            catch (Exception e) { Stumble(e); }
        }

        /// <summary>Z ArmouryBehavior.OnSessionLaunched: linia startu sesji.</summary>
        internal static void SessionStart()
        {
            Reset();
            var s = Settings.Current;
            bool best = s != null && s.VillageCartsBestMarket;
            Log.Info("Wlokno do miasta targowego (N2-E3): start sesji | klucz VillageFibreToMarket " + (On ? "TAK | " + (best ? "CZYNNE" : "NIECZYNNE - wybor miasta wozu (Village Carts Best Market) wylaczony") : "NIE | WYLACZONE (MCM) - woz wsi wybiera miasto najlepszej ceny, jak dotad")
                     + " | towary: len, welna, skory surowe | prog zapasu wsi (juki + magazyn) " + FibreDays.ToString("0.0", Inv) + " dob produkcji wsi (magazyn wsi 5 dob, wies staje przy 7.5)"
                     + " | cel: miasto targowe wsi (wies miejska - jej miasto, zamkowa - TradeBound), sprzedaz calego ladunku takze ponizej ceny bazowej | potkniecia 0.");
        }

        /// <summary>Z ArmouryBehavior.OnDailyTick (po linii "Dowoz (wozy)"): linia doby i porzadki pamieci wozow skierowanych (woz nieczynny albo ponad 30 dob).</summary>
        internal static void Daily()
        {
            List<MobileParty> stale = null;
            double now = CampaignTime.Now.ToDays;
            foreach (var kv in _trips)
                if (kv.Key == null || !kv.Key.IsActive || now - kv.Value.Day > 30.0) { if (stale == null) stale = new List<MobileParty>(); stale.Add(kv.Key); }
            if (stale != null) foreach (var mp in stale) _trips.Remove(mp);
            if (!On) { ClearDay(); return; }
            var parts = new List<string>();
            foreach (var kv in _soldBy) parts.Add(kv.Key + " " + kv.Value);
            parts.Sort(StringComparer.Ordinal);
            Log.Info("Wlokno do miasta targowego (N2-E3): dzien " + ((int)now - 1) + " | wozow z wloknem (najlepsze miasto inne niz targowe) sprawdzonych " + _checked + ", zapas ponad " + FibreDays.ToString("0", Inv) + " dob " + _over
                     + " (najwiekszy " + _maxDays.ToString("0.0", Inv) + " dob) | skierowane do miasta targowego " + _sent + " (wsi zamkowych " + _sentCastle + "), wlokna w jukach " + _sentUnits + " szt."
                     + ", wycena ladunku w miescie targowym " + _valOwn + " d wobec najlepszego miasta " + _valBest + " d (wies traci " + Math.Max(0, _valBest - _valOwn) + " d)"
                     + " | bez miasta targowego w zasiegu " + _noOwn + ", miasto targowe nic nie zaplaci " + _ownZero + ", ten sam kurs wybrany drugi raz (po zamku, bez licznika) " + _again
                     + " | sprzedaz w miescie celu: wozow " + _soldCarts + ", wlokna sprzedanego " + _soldUnits + " szt." + (parts.Count > 0 ? " (" + string.Join(", ", parts.ToArray()) + ")" : "")
                     + ", utarg tych wozow (kasa miasta -> sakwa wozu, po zwrocie nadplaty 119) " + _soldGold + " d"
                     + " | w drodze " + _trips.Count + ", wygasle przy porzadkach " + (stale != null ? stale.Count : 0)
                     + " | potkniecia " + _stDay + " (sesja " + _stumbles + ").");
            ClearDay();
        }
    }
}
