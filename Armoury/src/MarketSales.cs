using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Extensions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Workshops;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// PAKA 241 (Jeff 27.10: "czemu moj warsztat prawie nic nie zarabia"; audyt 27.10 pkt 1, "tak zacznij od tych pieciu"): PRAWDZIWA SPRZEDAZ MIAST Z 28 DOB.
    /// Kupiec miasta placi warsztatowi za sztuke tyle, za ile ja odsprzedaje: srednia z prawdziwych sprzedazy tego towaru w tym miescie z ostatnich 28 dob
    /// (WorkshopTrade.RefUnit). Ten modul tylko MIERZY sprzedaz - zadnego zlota ani sztuk nie rusza.
    ///
    /// Co jest sprzedaza (tylko platne zdjecia z polki miasta - town.Owner.ItemRoster - i tylko w watku glownym; reszta liczona jako "poza watkiem"):
    ///  H1 konsumpcja mieszczan: ItemConsumptionBehavior.MakeConsumption (prefiks Priority.First - przed prefiksem BK, ktory zwraca false; postfiks Priority.Last - po
    ///     CastlePurse/TownPurse (cofniecie przyrostu kasy) i TownFolk.PayForGoods (zaplata ludu)). Sztuki = spadek polki, zloto = PRAWDZIWY przyrost kasy miasta w nawiasie
    ///     rozlozony na towary wedlug ceny polki (lud placi czesc ceny, gdy kiesy puste - wtedy kupiec dostaje mniej i tyle liczy srednia);
    ///  H2 partie kupujace na targu (karawany, AI): SellItemsAction.ApplyInternal, gdy sprzedajacy to miasto - sztuki = number, zloto = przyrost kasy miasta;
    ///  H3 zakupy Armoury przez ShelfBuy.Take (i zwrot PutBack) - zloto = suma cen sztuk (wolajacy placi te cene);
    ///  H4 zakupy ludu wprost z polki (P5 wsie i podzamcza, potrzeby wsi, koszyk KS) - zloto = to, co naprawde weszlo do kasy miasta.
    /// Handel gracza na ekranie (InventoryLogic) nie jest liczony (male ilosci).
    ///
    /// Komorka (miasto, kategoria): 28 przegrodek dobowych (sztuki, wartosc sztuk = ItemValue, zloto). Indeks sprzedazy = zloto / wartosc z okna 28 dob (wartosc,
    /// nie sztuki - kategoria z kilkoma przedmiotami i modyfikatory licza sie dobrze). Zapis arm_mktsales (sumy okna; przy wczytaniu rozlozone rowno na 28 dob).
    /// Liczone tylko kategorie, ktore sa wyrobem linii towarowej jakiegos typu warsztatu (WorkshopTrade.IsTradeLine).
    /// </summary>
    internal static class MarketSales
    {
        internal const int Window = 28;   // doby sredniej (audyt 27.10, punkt zatwierdzony przez Jeffa: "srednia z 28 dob")

        private sealed class Cell
        {
            public readonly int[] Day = new int[Window];
            public readonly int[] U = new int[Window];
            public readonly long[] V = new long[Window];
            public readonly long[] G = new long[Window];
            public Cell() { for (int i = 0; i < Window; i++) Day[i] = int.MinValue; }
        }

        private static readonly Dictionary<Settlement, Dictionary<ItemCategory, Cell>> _cells = new Dictionary<Settlement, Dictionary<ItemCategory, Cell>>();
        private static HashSet<ItemCategory> _tracked;
        private static Dictionary<ItemRoster, Town> _shelfTown;
        private static List<string[]> _pending;            // wiersze z zapisu - rozlozone w SessionStart (czas kampanii pewny)
        private static int _loadedRows, _loadedTowns;
        private static int _main = -1;
        private static readonly HashSet<string> _err = new HashSet<string>();
        private static string _wired = "-", _missing = "-";

        // licznik doby (tylko log): drogi 0 konsumpcja, 1 partie, 2 Armoury (ShelfBuy), 3 lud
        private const int PCons = 0, PParty = 1, PArm = 2, PFolk = 3, Paths = 4;
        private static readonly string[] PathName = { "konsumpcja", "partie", "Armoury", "lud" };
        private static readonly long[] _dU = new long[Paths], _dG = new long[Paths];
        private static long _dConsMade, _dConsGot; private static int _dConsTicks;
        private static int _dOff, _dStumbles;

        internal static void Reset()
        {
            _cells.Clear(); _tracked = null; _shelfTown = null; _pending = null; _loadedRows = _loadedTowns = 0;
            _main = Environment.CurrentManagedThreadId;
            _err.Clear();
            ClearDay();
        }

        private static void ClearDay()
        {
            Array.Clear(_dU, 0, Paths); Array.Clear(_dG, 0, Paths);
            _dConsMade = _dConsGot = 0; _dConsTicks = 0; _dOff = 0; _dStumbles = 0;
        }

        private static void Stumble(string where, Exception e)
        {
            _dStumbles++;
            try { if (_err.Add(where)) Log.Error("MarketSales." + where, e); } catch { }
        }

        private static bool OffMain()
        {
            if (_main < 0 || Environment.CurrentManagedThreadId == _main) return false;
            _dOff++;
            return true;
        }

        private static int Today { get { return (int)CampaignTime.Now.ToDays; } }
        private static bool Live { get { var c = Campaign.Current; return c != null && c.GameStarted; } }

        // ------------------------------------------------------------ kategorie
        /// <summary>Kategorie wyrobow linii towarowych wszystkich typow warsztatow (raz na sesje).</summary>
        internal static bool Tracked(ItemCategory c)
        {
            if (c == null) return false;
            var t = _tracked;
            if (t == null) t = BuildTracked();
            return t != null && t.Contains(c);
        }

        private static HashSet<ItemCategory> BuildTracked()
        {
            try
            {
                if (Campaign.Current == null) return null;
                var set = new HashSet<ItemCategory>();
                foreach (var wt in WorkshopType.All)
                {
                    if (wt == null || wt.Productions == null) continue;
                    foreach (var p in wt.Productions)
                    {
                        if (!WorkshopTrade.IsTradeLine(p)) continue;
                        foreach (var o in p.Outputs) if (o.Item1 != null) set.Add(o.Item1);
                    }
                }
                if (set.Count == 0) return null;   // przed wczytaniem typow - nastepne wywolanie sprobuje znowu
                _tracked = set;
                return set;
            }
            catch (Exception e) { Stumble("BuildTracked", e); return null; }
        }

        // ------------------------------------------------------------ zapis sprzedazy
        private static Cell CellOf(Settlement st, ItemCategory c, bool make)
        {
            Dictionary<ItemCategory, Cell> m;
            if (!_cells.TryGetValue(st, out m))
            {
                if (!make) return null;
                m = new Dictionary<ItemCategory, Cell>();
                _cells[st] = m;
            }
            Cell cell;
            if (!m.TryGetValue(c, out cell) && make) { cell = new Cell(); m[c] = cell; }
            return cell;
        }

        private static void Add(Settlement st, ItemCategory c, int units, long value, long gold, int path)
        {
            if (st == null || c == null || units == 0) return;
            var cell = CellOf(st, c, true);
            int d = Today, k = ((d % Window) + Window) % Window;
            if (cell.Day[k] != d) { cell.Day[k] = d; cell.U[k] = 0; cell.V[k] = 0; cell.G[k] = 0; }
            cell.U[k] += units; cell.V[k] += value; cell.G[k] += gold;
            if (cell.U[k] < 0) cell.U[k] = 0; if (cell.V[k] < 0) cell.V[k] = 0; if (cell.G[k] < 0) cell.G[k] = 0;   // zwrot PutBack ponad zapis tej doby
            _dU[path] += units; _dG[path] += gold;
        }

        /// <summary>H4 (TownFolk) i inne platne zakupy z polki miasta: n sztuk el, do kasy miasta weszlo `gold`. Nie rzuca.</summary>
        internal static void Note(Town town, EquipmentElement el, int n, long gold)
        {
            try
            {
                if (town == null || el.Item == null || n <= 0 || !Live || !Tracked(el.Item.ItemCategory) || OffMain()) return;
                Add(town.Settlement, el.Item.ItemCategory, n, (long)n * Math.Max(0, el.ItemValue), Math.Max(0, gold), PFolk);
            }
            catch (Exception e) { Stumble("Note", e); }
        }

        /// <summary>H3: ShelfBuy.Take zdjal n sztuk el z polki `shelf` za `cost` (sign = -1: PutBack - sztuki wrocily, zakup cofniety). Nie rzuca.</summary>
        internal static void NoteShelf(ItemRoster shelf, EquipmentElement el, int n, long cost, int sign = 1)
        {
            try
            {
                if (shelf == null || el.Item == null || n <= 0 || !Live || !Tracked(el.Item.ItemCategory) || OffMain()) return;
                var town = TownOf(shelf);
                if (town == null) return;   // zamek, partia, magazyn
                Add(town.Settlement, el.Item.ItemCategory, sign * n, sign * (long)n * Math.Max(0, el.ItemValue), sign * Math.Max(0, cost), PArm);
            }
            catch (Exception e) { Stumble("NoteShelf", e); }
        }

        private static Town TownOf(ItemRoster shelf)
        {
            var map = _shelfTown;
            Town t;
            if (map != null && map.TryGetValue(shelf, out t)) return t;
            map = new Dictionary<ItemRoster, Town>();
            foreach (var tw in Town.AllTowns)
            {
                var r = tw != null && tw.Owner != null ? tw.Owner.ItemRoster : null;
                if (r != null) map[r] = tw;
            }
            if (!map.TryGetValue(shelf, out t)) { t = null; map[shelf] = null; }   // nie miasto (zamek, partia) - zapamietane jako null, bez przebudowy przy nastepnym
            _shelfTown = map;
            return t;
        }

        // ------------------------------------------------------------ odczyt
        /// <summary>Indeks sprzedazy (zloto / wartosc) z 28 dob tego miasta dla kategorii; false - brak historii (zadna sztuka). Sam odczyt (bez zapisu stanu).</summary>
        internal static bool Index(Settlement st, ItemCategory c, out double idx, out int units)
        {
            idx = 0; units = 0;
            try
            {
                if (st == null || c == null) return false;
                var cell = CellOf(st, c, false);
                if (cell == null) return false;
                int d = Today;
                long u = 0, v = 0, g = 0;
                for (int k = 0; k < Window; k++)
                {
                    int dd = cell.Day[k];
                    if (dd > d || dd <= d - Window) continue;
                    u += cell.U[k]; v += cell.V[k]; g += cell.G[k];
                }
                if (u < 1 || v <= 0) return false;
                units = (int)Math.Min(int.MaxValue, u);
                idx = (double)g / v;
                return !double.IsNaN(idx) && !double.IsInfinity(idx);
            }
            catch { return false; }
        }

        // ------------------------------------------------------------ H1: konsumpcja mieszczan
        private struct Snap { public EquipmentElement El; public int N; public int Price; }
        private sealed class ConsState { public Town T; public int Gold0; public List<Snap> Rows; }

        [HarmonyPriority(Priority.First)]
        public static void ConsPre(Town __0, out object __state)
        {
            __state = null;
            try
            {
                if (__0 == null || !__0.IsTown || !Live || __0.Owner == null || OffMain()) return;
                var ro = __0.Owner.ItemRoster;
                if (ro == null || ro.Count == 0) return;
                var md = __0.MarketData;
                var rows = new List<Snap>(ro.Count);
                for (int i = 0; i < ro.Count; i++)
                {
                    var re = ro.GetElementCopyAtIndex(i);
                    var el = re.EquipmentElement;
                    if (el.Item == null || re.Amount <= 0) continue;
                    // ta sama cena, ktora BK bierze za kazda pozycje (TownMarketData.GetPrice(sztuka, null, false, null))
                    int p = 0;
                    try { p = md.GetPrice(el.Item, null, false, null); } catch { p = 0; }
                    rows.Add(new Snap { El = el, N = re.Amount, Price = Math.Max(0, p) });
                }
                __state = new ConsState { T = __0, Gold0 = __0.Gold, Rows = rows };
            }
            catch (Exception e) { __state = null; Stumble("ConsPre", e); }
        }

        [HarmonyPriority(Priority.Last)]
        public static void ConsPost(Town __0, object __state)
        {
            var cs = __state as ConsState;
            if (cs == null || !ReferenceEquals(cs.T, __0)) return;
            try
            {
                var ro = __0.Owner.ItemRoster;
                var now = new Dictionary<EquipmentElement, int>(ro.Count);
                for (int i = 0; i < ro.Count; i++)
                {
                    var re = ro.GetElementCopyAtIndex(i);
                    int n0; now.TryGetValue(re.EquipmentElement, out n0);
                    now[re.EquipmentElement] = n0 + re.Amount;
                }
                long made = 0;
                var drop = new int[cs.Rows.Count];
                for (int i = 0; i < cs.Rows.Count; i++)
                {
                    var r = cs.Rows[i];
                    int n1; now.TryGetValue(r.El, out n1);
                    int d = r.N - n1;
                    if (d <= 0) continue;
                    drop[i] = d;
                    made += (long)d * r.Price;
                }
                if (made <= 0) return;
                long got = (long)__0.Gold - cs.Gold0;   // przyrost kasy po cofnieciu z niczego i zaplacie ludu - tyle kupiec naprawde dostal
                double ratio = got <= 0 ? 0.0 : Math.Min(1.0, (double)got / made);
                _dConsMade += made; _dConsGot += Math.Max(0, got); _dConsTicks++;
                var st = __0.Settlement;
                for (int i = 0; i < cs.Rows.Count; i++)
                {
                    int d = drop[i];
                    if (d <= 0) continue;
                    var r = cs.Rows[i];
                    var c = r.El.Item.ItemCategory;
                    if (!Tracked(c)) continue;
                    long g = (long)Math.Round((double)d * r.Price * ratio);
                    Add(st, c, d, (long)d * Math.Max(0, r.El.ItemValue), g, PCons);
                }
            }
            catch (Exception e) { Stumble("ConsPost", e); }
        }

        // ------------------------------------------------------------ H2: partie kupujace na targu miasta
        public static void SellPre(PartyBase __0, ItemRosterElement __2, out int __state)
        {
            __state = int.MinValue;
            try
            {
                if (__0 == null || !__0.IsSettlement || __0.Settlement == null || !__0.Settlement.IsTown || __0.Settlement.Town == null || !Live) return;
                var it = __2.EquipmentElement.Item;
                if (it == null || !Tracked(it.ItemCategory) || OffMain()) return;
                __state = __0.Settlement.Town.Gold;
            }
            catch (Exception e) { __state = int.MinValue; Stumble("SellPre", e); }
        }

        public static void SellPost(PartyBase __0, ItemRosterElement __2, int __3, int __state)
        {
            if (__state == int.MinValue) return;
            try
            {
                var town = __0.Settlement.Town;
                long got = (long)town.Gold - __state;
                if (__3 <= 0 || got <= 0) return;
                var el = __2.EquipmentElement;
                Add(town.Settlement, el.Item.ItemCategory, __3, (long)__3 * Math.Max(0, el.ItemValue), got, PParty);
            }
            catch (Exception e) { Stumble("SellPost", e); }
        }

        // ------------------------------------------------------------ wpiecie (WorkshopTrade.ApplyAll)
        internal static void ApplyAll(Harmony h)
        {
            var done = new List<string>(); var miss = new List<string>();
            try
            {
                var mc = AccessTools.Method(typeof(ItemConsumptionBehavior), "MakeConsumption", new[] { typeof(Town), typeof(Dictionary<ItemCategory, float>), typeof(Dictionary<ItemCategory, int>) });
                if (mc == null) miss.Add("konsumpcja (MakeConsumption)");
                else
                {
                    h.Patch(mc, prefix: new HarmonyMethod(typeof(MarketSales), nameof(ConsPre)) { priority = Priority.First },
                                postfix: new HarmonyMethod(typeof(MarketSales), nameof(ConsPost)) { priority = Priority.Last });
                    done.Add("konsumpcja");
                }
            }
            catch (Exception e) { miss.Add("konsumpcja (" + e.Message + ")"); Log.Error("MarketSales.ApplyAll(konsumpcja)", e); }
            try
            {
                var m = AccessTools.DeclaredMethod(typeof(SellItemsAction), "ApplyInternal");
                var ps = m != null ? m.GetParameters() : null;
                if (m != null && m.IsStatic && ps.Length >= 4 && ps[0].ParameterType == typeof(PartyBase) && ps[2].ParameterType == typeof(ItemRosterElement) && ps[3].ParameterType == typeof(int))
                {
                    h.Patch(m, prefix: new HarmonyMethod(typeof(MarketSales), nameof(SellPre)), postfix: new HarmonyMethod(typeof(MarketSales), nameof(SellPost)));
                    done.Add("partie");
                }
                else miss.Add("partie (SellItemsAction.ApplyInternal - inna sygnatura)");
            }
            catch (Exception e) { miss.Add("partie (" + e.Message + ")"); Log.Error("MarketSales.ApplyAll(partie)", e); }
            done.Add("ShelfBuy"); done.Add("lud (P5/potrzeby/KS)");
            _wired = string.Join(", ", done.ToArray());
            _missing = miss.Count > 0 ? string.Join(", ", miss.ToArray()) : "-";
        }

        /// <summary>Czy pomiar konsumpcji (glowne zrodlo sprzedazy) jest wpiety - bez niego historia bylaby niepelna (log startu).</summary>
        internal static bool ConsWired { get { return _wired.Contains("konsumpcja"); } }

        // ------------------------------------------------------------ start sesji i linia doby
        internal static void SessionStart()
        {
            _main = Environment.CurrentManagedThreadId;
            _shelfTown = null;
            try { Materialize(); } catch (Exception e) { Stumble("Materialize", e); }
            var t = BuildTracked();
            var names = new List<string>();
            if (t != null) foreach (var c in t) names.Add(c.StringId);
            names.Sort(StringComparer.Ordinal);
            Log.Info("Sprzedaz miast (241): start sesji | kategorii " + names.Count + " [" + string.Join(", ", names.ToArray()) + "] | wpiete: " + _wired + "; BRAK: " + _missing
                     + " | z zapisu wierszy " + _loadedRows + " (miast " + _loadedTowns + ") | okno " + Window + " dob, indeks = zloto / wartosc sztuk (ItemValue).");
        }

        internal static void Daily()
        {
            try
            {
                if (Campaign.Current == null) return;
                int day = Today - 1, d = Today;
                long u = 0, g = 0; int cells = 0;
                foreach (var m in _cells.Values)
                    foreach (var cell in m.Values)
                    {
                        long cu = 0, cg = 0;
                        for (int k = 0; k < Window; k++)
                        {
                            int dd = cell.Day[k];
                            if (dd > d || dd <= d - Window) continue;
                            cu += cell.U[k]; cg += cell.G[k];
                        }
                        if (cu > 0) { cells++; u += cu; g += cg; }
                    }
                var inv = CultureInfo.InvariantCulture;
                var sb = new StringBuilder(512);
                sb.Append("Sprzedaz miast (241): dzien ").Append(day).Append(" | 28 dob: sztuk ").Append(u).Append(", zloto ").Append(g).Append(", komorek z historia ").Append(cells)
                  .Append(" | dzis wedlug drogi: ");
                for (int i = 0; i < Paths; i++) sb.Append(i > 0 ? ", " : "").Append(PathName[i]).Append(' ').Append(_dU[i]).Append('/').Append(_dG[i]);
                sb.Append("; poza watkiem glownym ").Append(_dOff)
                  .Append(" | konsumpcja: kupiec dostal ").Append(_dConsGot).Append(" z ").Append(_dConsMade).Append(" wedle ceny polki (")
                  .Append(_dConsMade > 0 ? (100.0 * _dConsGot / _dConsMade).ToString("0.0", inv) + "%" : "-").Append(", tickow ").Append(_dConsTicks).Append(')')
                  .Append(" | potkniecia ").Append(_dStumbles).Append('.');
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Log.Error("MarketSales.Daily", e); }
            finally { ClearDay(); }
        }

        // ------------------------------------------------------------ zapis (klucz arm_mktsales)
        /// <summary>"osada|kategoria|sztuki|wartosc|zloto|doby;..." - sumy okna 28 dob, tylko komorki ze sprzedaza.</summary>
        internal static string Export()
        {
            var sb = new StringBuilder();
            if (_pending != null)
            {
                foreach (var f in _pending) { if (sb.Length > 0) sb.Append(';'); sb.Append(string.Join("|", f)); }
                return sb.ToString();
            }
            int d = Today;
            foreach (var kv in _cells)
            {
                var st = kv.Key;
                if (st == null || st.StringId == null) continue;
                foreach (var kc in kv.Value)
                {
                    if (kc.Key == null || kc.Key.StringId == null) continue;
                    long u = 0, v = 0, g = 0; int days = 0;
                    var cell = kc.Value;
                    for (int k = 0; k < Window; k++)
                    {
                        int dd = cell.Day[k];
                        if (dd > d || dd <= d - Window) continue;
                        u += cell.U[k]; v += cell.V[k]; g += cell.G[k];
                        if (cell.U[k] > 0) days++;
                    }
                    if (u <= 0) continue;
                    if (sb.Length > 0) sb.Append(';');
                    sb.Append(st.StringId).Append('|').Append(kc.Key.StringId).Append('|').Append(u).Append('|').Append(v).Append('|').Append(g).Append('|').Append(days);
                }
            }
            return sb.ToString();
        }

        internal static void Import(string data)
        {
            _cells.Clear(); _pending = null; _loadedRows = _loadedTowns = 0;
            if (string.IsNullOrEmpty(data)) return;
            var list = new List<string[]>();
            foreach (var row in data.Split(';'))
            {
                var f = row.Split('|');
                if (f.Length >= 6) list.Add(f);
            }
            _pending = list;
        }

        /// <summary>Wiersze z zapisu do przegrodek: sumy rozlozone rowno na 28 ostatnich dob (srednia okna ta sama; stara sprzedaz wygasa po dobie, jak zwykle).</summary>
        private static void Materialize()
        {
            var rows = _pending;
            _pending = null;
            if (rows == null || rows.Count == 0) return;
            var sts = new Dictionary<string, Settlement>();
            foreach (var st in Settlement.All) if (st != null && st.StringId != null) sts[st.StringId] = st;
            var cats = new Dictionary<string, ItemCategory>();
            foreach (var c in ItemCategories.All) if (c != null && c.StringId != null) cats[c.StringId] = c;
            int d = Today;
            var towns = new HashSet<Settlement>();
            foreach (var f in rows)
            {
                try
                {
                    Settlement st; ItemCategory c; long u, v, g;
                    if (!sts.TryGetValue(f[0], out st) || !cats.TryGetValue(f[1], out c)) continue;
                    if (!long.TryParse(f[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out u) || !long.TryParse(f[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out v)
                        || !long.TryParse(f[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out g) || u <= 0) continue;
                    var cell = CellOf(st, c, true);
                    for (int i = 0; i < Window; i++)
                    {
                        // i = 0 to dzis; reszta z dzielenia trafia do najnowszych dob (pojedyncza sprzedaz nie wygasa nazajutrz)
                        int day = d - i, k = ((day % Window) + Window) % Window, a = Window - i, b = Window - i - 1;
                        cell.Day[k] = day;
                        cell.U[k] = (int)Math.Min(int.MaxValue, u * a / Window - u * b / Window);
                        cell.V[k] = v * a / Window - v * b / Window;
                        cell.G[k] = g * a / Window - g * b / Window;
                    }
                    _loadedRows++; towns.Add(st);
                }
                catch (Exception e) { Stumble("Materialize(wiersz)", e); }
            }
            _loadedTowns = towns.Count;
        }
    }
}
