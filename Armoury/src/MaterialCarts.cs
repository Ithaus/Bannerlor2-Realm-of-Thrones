using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// P3/Z1 (TRIAZ-3 bieg roczny 13.10, O2-1 "Poprawka ostateczna" P1) - WOZ KUPCOW Z SUROWCEM (kontrakt wozem).
    /// Bieg roczny: 41-45 miast z 97 bez zelaza przy 14.5 tys. lup na swiecie; kontrakt 174 wymaga karawany STOJACEJ w zrodle (0.9 kontraktu na dobe),
    /// wyspy bez konwoju w porcie nie dostaja nic. Gdy zadna karawana nie wziela zamowienia (MaterialOrders.Order), miasto kupuje surowiec z polki
    /// najblizszego oplacalnego zrodla z nadwyzka (miasto albo zamek) i towar jedzie wozem kupcow - wzor TradeCarts (E6/R3) i wozow dymarek (F4a):
    /// sztuka schodzi z polki zrodla w chwili zakupu (ShelfBuy.Take, sztuka po sztuce po cenie targu zrodla), placi kasa miasta-celu ponad zapas kupcow
    /// (TownPurse.BuyFloor) do kasy zrodla (Town.ChangeGold - takze zamek), na polke celu wchodzi po czasie drogi (predkosc wozu wsi - TradeCarts.DaysFor;
    /// morzem ta sama predkosc). Bez oplaty frachtu (jak R3 i F4a) - koszt drogi jest tylko warunkiem zysku, jak w kontrakcie karawany.
    /// W drodze: cel oblezony - woz czeka; po 30 dobach od zakupu (albo po planowanej drodze, jesli dluzsza) - zawraca: sztuki na polke zrodla, zwrot
    /// zaplaty z kasy zrodla do kasy celu, nie wiecej niz kasa zrodla (jak TradeCarts.Back). Zmiana wlasciciela celu albo wojna po zakupie - woz dojezdza
    /// (towar jest oplacony z kasy tego miasta). Bandyci wozu nie rozbijaja (uproszczenie jak R3 i F4a - kierunek do potwierdzenia przez Jeffa).
    /// Osobna lista (nie TradeCarts): linia "Przewoz kupcow wozem (215)" i jej progi zostaja bez zmian; "w drodze: cel x surowiec" doliczane do
    /// MaterialOrders.InTransit (miasto nie zamawia drugi raz). Ksiegi: GoodsLedger (posiadacz "wozy surowcow"), OreLedger (zapas + w drodze),
    /// spis stosu regulatora wsi (HarvestValve.Census).
    /// Zapis: rekordy "@w|..." w kluczu arm_matorders (MaterialOrders.Export / ResolvePending) - bez nowego klucza zapisu; stary zapis = brak wozow;
    /// starszy DLL rekordu nie rozumie i pomija go (licznik "pominiete") - sztuki tego wozu przepadaja tylko przy cofnieciu DLL.
    /// Wylaczony klucz TownMaterialOrderByCart - nowych wozow nie ma, oplacone dojezdzaja.
    /// </summary>
    internal static class MaterialCarts
    {
        private const int MaxDays = 30;
        private const string Tag = "@w";

        private sealed class Cart
        {
            internal Settlement Src, Dst; internal ItemObject Item; internal int Mat, N, Paid, DayBought, DayDue; internal bool Naval;
        }

        private static readonly List<Cart> _carts = new List<Cart>();
        private static readonly Dictionary<Settlement, int[]> _incoming = new Dictionary<Settlement, int[]>();   // cel -> surowiec -> szt. w drodze
        private static int _mats = 7;
        private static long _prevTransit = -1;   // w drodze przy poprzedniej linii (-1 = pierwsza linia sesji)

        // liczniki doby (wstawka w linii "Kontrakty surowca (174)")
        private static int[] _dMade = new int[7], _dQty = new int[7];
        private static int _dCarts, _dGold, _dDaysSum, _dCastle, _dCastleQty, _dSea, _dSeaQty, _dArrCarts, _dArr, _dArrDays, _dWait, _dBack, _dBackPcs, _dRefund, _dLost, _dFromSave, _stumbles;
        private static readonly HashSet<string> _errSites = new HashSet<string>();
        private static readonly List<string> _dEx = new List<string>();

        /// <summary>Jest co pisac w linii doby: wozy w drodze albo ruch tej doby (zakupy, dostawy, zawrocenia).</summary>
        internal static bool Busy { get { return _carts.Count > 0 || _dCarts + _dArr + _dBackPcs + _dLost + _dWait > 0; } }

        internal static void Reset(int mats)
        {
            _mats = Math.Max(1, mats);
            _carts.Clear(); _incoming.Clear(); _prevTransit = -1;
            _dMade = new int[_mats]; _dQty = new int[_mats];
            ClearDay(); _errSites.Clear();
        }

        /// <summary>Wczytanie zapisu (MaterialOrders.Import): lista pusta, rekordy dopisze ResolvePending.</summary>
        internal static void Clear() { _carts.Clear(); _incoming.Clear(); _prevTransit = -1; }

        private static void ClearDay()
        {
            Array.Clear(_dMade, 0, _dMade.Length); Array.Clear(_dQty, 0, _dQty.Length);
            _dCarts = _dGold = _dDaysSum = _dCastle = _dCastleQty = _dSea = _dSeaQty = _dArrCarts = _dArr = _dArrDays = _dWait = _dBack = _dBackPcs = _dRefund = _dLost = _dFromSave = _stumbles = 0;
            _dEx.Clear();
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            if (_errSites.Add(where)) Log.Error("MaterialCarts." + where, e);
        }

        private static void AddIncoming(Cart c, int sign)
        {
            if (c == null || c.Dst == null || c.Mat < 0 || c.Mat >= _mats || c.N <= 0) return;
            int[] a;
            if (!_incoming.TryGetValue(c.Dst, out a)) { if (sign < 0) return; a = new int[_mats]; _incoming[c.Dst] = a; }
            a[c.Mat] = Math.Max(0, a[c.Mat] + sign * c.N);
        }

        /// <summary>Sztuki surowca m, ktore jada juz wozem do tej osady.</summary>
        internal static int Incoming(Settlement dst, int m)
        {
            int[] a;
            return dst != null && m >= 0 && m < _mats && _incoming.TryGetValue(dst, out a) ? a[m] : 0;
        }

        internal static long InTransitPieces() { long n = 0; foreach (var c in _carts) n += c.N; return n; }

        internal static long InTransitOf(ItemObject item) { long n = 0; if (item != null) foreach (var c in _carts) if (ReferenceEquals(c.Item, item)) n += c.N; return n; }

        /// <summary>Ksiegi (GoodsLedger, OreLedger, spis regulatora wsi): kazda sztuka w drodze.</summary>
        internal static void ForEachInTransit(Action<ItemObject, int> f)
        {
            foreach (var c in _carts) if (c.Item != null && c.N > 0) f(c.Item, c.N);
        }

        /// <summary>n sztuk zdjetych z polki zrodla i oplaconych przez cel (paid) jedzie wozem; days - planowane doby drogi.</summary>
        internal static void Ship(Settlement src, Settlement dst, ItemObject item, int m, int n, int paid, int days, bool naval, int today, string name)
        {
            if (src == null || dst == null || item == null || n <= 0) return;
            Cart c = null;
            try
            {
                days = Math.Max(1, days);
                c = new Cart { Src = src, Dst = dst, Item = item, Mat = m, N = n, Paid = Math.Max(0, paid), DayBought = today, DayDue = today + days, Naval = naval };
                _carts.Add(c);
                AddIncoming(c, +1);
                _dCarts++; _dGold += c.Paid; _dDaysSum += days;
                if (m >= 0 && m < _mats) { _dMade[m]++; _dQty[m] += n; }
                if (src.IsCastle) { _dCastle++; _dCastleQty += n; }
                if (naval) { _dSea++; _dSeaQty += n; }
                if (_dEx.Count < 3) _dEx.Add(dst.Name + " - " + src.Name + (src.IsCastle ? " (zamek)" : "") + (naval ? " morzem" : "") + " " + name + " " + n + " za " + c.Paid + " zl, " + days + " dob");
            }
            catch (Exception e)
            {
                // woz sie nie zapisal - sztuka nie moze zniknac: na polke celu od razu (woz juz na liscie - potkniecie tylko w licznikach, ladunek jedzie)
                Stumble("Ship", e);
                if (c == null || !_carts.Contains(c)) { try { dst.ItemRoster.AddToCounts(item, n); } catch { } }
            }
        }

        /// <summary>Raz na dobe na poczatku MaterialOrders.Daily (przed zamowieniami): dostawy, czekanie pod oblezeniem, zawrocenia po 30 dobach.
        /// Dziala takze przy wylaczonym kluczu (oplacone wozy dojezdzaja). arrived(cel, surowiec, sztuki, doby od zakupu) - pamiec dni drogi i liczniki rudy.</summary>
        internal static void Arrivals(int today, Action<Settlement, int, int, int> arrived)
        {
            if (_carts.Count == 0) return;
            for (int i = _carts.Count - 1; i >= 0; i--)
            {
                var c = _carts[i];
                try
                {
                    if (today < c.DayDue) continue;
                    bool dstOk = c.Dst != null && c.Dst.ItemRoster != null && c.Dst.Town != null, srcOk = c.Src != null && c.Src.ItemRoster != null;
                    bool late = today - c.DayBought > Math.Max(MaxDays, c.DayDue - c.DayBought);
                    if (c.N <= 0 || c.Item == null) { _carts.RemoveAt(i); continue; }
                    if (!dstOk && !srcOk) { AddIncoming(c, -1); _dLost += c.N; _carts.RemoveAt(i); continue; }   // nie ma dokad ani skad (tylko po zlym zapisie)
                    if (!dstOk || (late && srcOk)) { Back(c); if (c.N <= 0) _carts.RemoveAt(i); continue; }
                    if (c.Dst.IsUnderSiege && !late) { _dWait++; continue; }
                    int n = Put(c, c.Dst.ItemRoster);
                    if (n <= 0) continue;                 // polka nie przyjela ani sztuki (wyjatek zdarzenia polki) - woz czeka do jutra
                    _dArr += n;
                    if (c.N > 0) continue;                // przyjeta tylko czesc - reszta zostaje w wozie (bez dubla, bez zguby)
                    _carts.RemoveAt(i);
                    _dArrCarts++; _dArrDays += today - c.DayBought;
                    try { if (arrived != null) arrived(c.Dst, c.Mat, n, today - c.DayBought); } catch (Exception e) { Stumble("Arrivals(pamiec drogi)", e); }
                }
                catch (Exception e) { Stumble("Arrivals", e); }   // woz zostaje w drodze - sprobuje nazajutrz
            }
        }

        /// <summary>Zawrocenie: sztuki na polke zrodla; zwrot zaplaty z kasy zrodla do kasy celu, nie wiecej niz kasa zrodla (reszta zostaje w kasie zrodla -
        /// to samo zloto, ktore cel zaplacil; jak TradeCarts.Back).</summary>
        private static void Back(Cart c)
        {
            int n = Put(c, c.Src.ItemRoster);
            if (n <= 0) return;
            _dBackPcs += n;
            if (c.N > 0) return;                          // reszta wroci jutro; zwrot zaplaty dopiero z calym ladunkiem
            _dBack++;
            if (c.Src.Town == null || c.Dst == null || c.Dst.Town == null) return;
            int r = Math.Min(c.Paid, Math.Max(0, c.Src.Town.Gold));
            if (r <= 0) return;
            c.Src.Town.ChangeGold(-r);
            c.Dst.Town.ChangeGold(r);
            c.Paid = 0;
            _dRefund += r;
        }

        /// <summary>Ladunek wozu na polke (cel albo zrodlo przy zawroceniu): zwraca sztuki, ktore polka NAPRAWDE przyjela (stan polki po minus przed - wyjatek
        /// w zdarzeniu polki nie robi ani dubla, ani zguby); o tyle maleje ladunek wozu i "w drodze" celu.</summary>
        private static int Put(Cart c, ItemRoster shelf)
        {
            int n = c.N;
            if (n <= 0 || shelf == null || c.Item == null) return 0;
            int before = shelf.GetItemNumber(c.Item), put = n;
            try { shelf.AddToCounts(c.Item, n); }
            catch (Exception e)
            {
                Stumble("Put", e);
                put = Math.Max(0, Math.Min(n, shelf.GetItemNumber(c.Item) - before));
            }
            if (put <= 0) return 0;
            AddIncoming(c, -1);
            c.N = n - put;
            if (c.N > 0) AddIncoming(c, +1);
            return put;
        }

        /// <summary>Wstawka linii "Kontrakty surowca (174)": zakupy wozem, dostawy, w drodze i kontrola "wczoraj + kupione - dowiezione - zawrocone - bez celu
        /// = dzis" (reszta 0). Zeruje liczniki doby.</summary>
        internal static void Segment(StringBuilder sb, bool keyOn, string[] names, ItemObject ore)
        {
            long now = InTransitPieces();
            try
            {
                var inv = CultureInfo.InvariantCulture;
                int bought = 0; foreach (var q in _dQty) bought += q;
                sb.Append("; wozem kupcow (P3/Z1): ").Append(keyOn ? "CZYNNE" : "WYLACZONE (tylko dowoz oplaconych)").Append(" - zawarto ").Append(_dCarts).Append(" [");
                for (int m = 0; m < _mats; m++) { if (m > 0) sb.Append(", "); sb.Append(names != null && m < names.Length ? names[m] : "?").Append(' ').Append(_dQty[m]); }
                sb.Append(" sztuk] za ").Append(_dGold).Append(" zl (kasy miast-celow -> kasy zrodel), srednio ")
                  .Append(_dCarts > 0 ? ((double)_dDaysSum / _dCarts).ToString("0.0", inv) : "-").Append(" dob drogi; w tym ze zrodel-zamkow ").Append(_dCastle).Append(" (sztuk ").Append(_dCastleQty)
                  .Append("), morzem ").Append(_dSea).Append(" (sztuk ").Append(_dSeaQty).Append("); dojechalo wozow ").Append(_dArrCarts).Append(" (sztuk ").Append(_dArr).Append(", srednio ")
                  .Append(_dArrCarts > 0 ? ((double)_dArrDays / _dArrCarts).ToString("0.0", inv) : "-").Append(" dob od zakupu), czeka pod oblezeniem ").Append(_dWait)
                  .Append(", zawrocone ").Append(_dBack).Append(" (sztuk ").Append(_dBackPcs).Append(", zwrot ").Append(_dRefund).Append(" zl), w drodze ").Append(now)
                  .Append(" sztuk w ").Append(_carts.Count).Append(" wozach (rudy ").Append(InTransitOf(ore)).Append(")");
                if (_prevTransit >= 0)
                    sb.Append(", kontrola: w drodze wczoraj ").Append(_prevTransit).Append(" + kupione ").Append(bought).Append(" - dowiezione - zawrocone - bez celu ").Append(_dLost)
                      .Append(" - w drodze dzis = reszta wozow ").Append(_prevTransit + _dFromSave + bought - _dArr - _dBackPcs - _dLost - now);
                else sb.Append(", kontrola od nastepnej doby");
                sb.Append(" [").Append(_dEx.Count > 0 ? string.Join("; ", _dEx.ToArray()) : "-").Append("], potkniecia wozow ").Append(_stumbles);
            }
            catch (Exception e) { Stumble("Segment", e); }
            _prevTransit = now;
            ClearDay();
        }

        // ------------------------------------------------------------ zapis: @w|zrodlo|cel|surowiec|sztuk|zaplacone|dobaZakupu|dobaPrzyjazdu|morzem~
        internal static void Export(StringBuilder sb, string[] ids)
        {
            foreach (var c in _carts)
            {
                try
                {
                    if (c.Src == null || c.Dst == null || c.N <= 0 || c.Mat < 0 || c.Mat >= ids.Length) continue;
                    sb.Append(Tag).Append('|').Append(c.Src.StringId).Append('|').Append(c.Dst.StringId).Append('|').Append(ids[c.Mat]).Append('|').Append(c.N).Append('|')
                      .Append(c.Paid).Append('|').Append(c.DayBought).Append('|').Append(c.DayDue).Append('|').Append(c.Naval ? 1 : 0).Append('~');
                }
                catch (Exception e) { Stumble("Export", e); }
            }
        }

        internal static bool IsRecord(string rec) { return rec != null && rec.StartsWith(Tag + "|", StringComparison.Ordinal); }

        /// <summary>Rekord wozu z zapisu (MaterialOrders.ResolvePending); false = odrzucony (osady albo surowca juz nie ma, rekord nieczytelny).</summary>
        internal static bool ImportRecord(string rec, string[] ids, ItemObject[] items)
        {
            try
            {
                var a = rec.Split('|');
                if (a.Length != 9) return false;
                var om = MBObjectManager.Instance;
                Settlement src = null, dst = null;
                try { src = om.GetObject<Settlement>(a[1]); dst = om.GetObject<Settlement>(a[2]); } catch { }
                int m = Array.IndexOf(ids, a[3]);
                int n, paid, d0, d1;
                if (src == null || dst == null || m < 0 || items == null || m >= items.Length || items[m] == null) return false;
                if (!int.TryParse(a[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out n) || n <= 0) return false;
                int.TryParse(a[5], NumberStyles.Integer, CultureInfo.InvariantCulture, out paid);
                if (!int.TryParse(a[6], NumberStyles.Integer, CultureInfo.InvariantCulture, out d0) || !int.TryParse(a[7], NumberStyles.Integer, CultureInfo.InvariantCulture, out d1)) return false;
                var c = new Cart { Src = src, Dst = dst, Item = items[m], Mat = m, N = n, Paid = Math.Max(0, paid), DayBought = d0, DayDue = Math.Max(d0 + 1, d1), Naval = a[8] == "1" };
                _carts.Add(c);
                AddIncoming(c, +1);
                if (_prevTransit >= 0) _dFromSave += n;   // rozwiazanie zapisu w srodku doby sesji - kontrola linii liczy te sztuki jak przybyle
                return true;
            }
            catch (Exception e) { Stumble("ImportRecord", e); return false; }
        }
    }
}
