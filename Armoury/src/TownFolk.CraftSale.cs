using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// N4 (noc 16.10, galaz noc-ekonomia cz. 2; raport audytu ekonomii 15.10 rozdz. 3.1, tabela problemow 11) - DOPLATA DO 35% TYLKO OD TOWARU SPRZEDANEGO Z POLKI.
    /// Klucz FolkCraftWageOnSale (wylaczony = jak przed paczka: wartosc wyrobu liczona przy wykonaniu, TownFolk.OnItemProduced).
    ///  - DOWOD: kasy miast placily ludowi 87-103 tys. zl/dobe doplaty do TownFolkCraftWageShare (35%) wartosci KAZDEGO wyrobu (TownFolk.Live.cs TownLive krok 2,
    ///    "doplata do 35%"), takze broni, ktora lezy na zapchanej polce (2 580 sztuk broni na dobe, zlom 135; test 4 lata: polki 68 -> 264 tys., potem warsztaty
    ///    staja "brak zlota"). Lud nie ma czego kupic - zloto z kas miast zostaje w kiesie.
    ///  - REGULA (kolejka FIFO wedlug koszyka w kazdym miescie): wyrob warsztatu miasta (zdarzenie gry OnItemProduced) idzie do puli "wykonane, niesprzedane"
    ///    koszyka (towar - wedlug przedmiotu, uzbrojenie - rodzaj i tier) z wartoscia po cenie targu w chwili wykonania. Raz na dobe (TownFolk.Daily, przed doba ludu)
    ///    liczymy sztuki koszyka na polce: gdy jest ich mniej niz w puli, roznica zeszla z polki (najpierw starszy towar, potem wyroby z puli) - jej wartosc z puli
    ///    (srednia) wchodzi do f.ProdV, od ktorego kasa miasta placi doplate do 35% (ta sama kasa, ten sam wzor, te same place cyklu odliczone). Zejscie z polki
    ///    na zlom z nadmiaru (174, ArmsScrap) liczy sie najpierw i bez doplaty. Pula nie rosnie ponad polke.
    ///  - Zloto: platnik i odbiorca jak dotad (kasa miasta -> kiesa ludu W70/R30); zmienia sie tylko CHWILA i PODSTAWA (sprzedane zamiast wykonane).
    ///  - Zapis arm_craft_sale (pula wedlug miasta i koszyka); brak klucza (stary zapis, wlaczenie w trakcie) - pula od zera: towar na polce sprzed wlaczenia traktowany
    ///    jak starszy (byl juz oplacony przy wykonaniu). Zamki bez zmian (doplata liczona tylko w miastach).
    /// Wyjatek per miasto - licznik potkniec (CLAUDE.md 7), reszta liczy sie dalej.
    /// </summary>
    internal static partial class TownFolk
    {
        private sealed class CsPool { public int U; public double V; public int Gone; }

        private static readonly Dictionary<Settlement, Dictionary<string, CsPool>> _cs = new Dictionary<Settlement, Dictionary<string, CsPool>>();
        private static Dictionary<Settlement, double> _csMade = new Dictionary<Settlement, double>(), _csMadeDay = new Dictionary<Settlement, double>();
        private static readonly CultureInfo CsInv = CultureInfo.InvariantCulture;
        /// <summary>N4 (linia NE, doba): wartosc wyrobow miast wykonanych (stara podstawa) i zeszlych z polki z puli (nowa), sztuki, zlom bez doplaty, pula na koniec,
        /// doplata zaplacona i wedlug starej reguly (po przycieciu do kasy), potkniecia.</summary>
        internal static double N4MadeV, N4SoldV, N4PoolV; internal static long N4SoldN, N4GoneN, N4PoolN, N4TopUp, N4TopUpOld; internal static int N4Towns, N4Stumbles;

        private static bool CraftSaleOn { get { var s = Settings.Current; return s != null && s.FolkCraftWageOnSale; } }

        private static string CsKey(ItemObject it)
        {
            if (it == null) return null;
            if (ArmsLeaks.ArmsPiece(it)) return "a" + ((int)it.ItemType).ToString(CsInv) + "." + Math.Max(1, Math.Min(6, (int)it.Tier + 1)).ToString(CsInv);
            return it.StringId != null ? "i" + it.StringId : null;
        }

        private static void ResetCraftSale()
        {
            _cs.Clear(); _csMade.Clear(); _csMadeDay.Clear();
            CsDayZero(); N4PoolV = 0; N4PoolN = 0; N4Stumbles = 0;
        }

        private static void CsDayZero()
        {
            N4MadeV = N4SoldV = 0; N4SoldN = N4GoneN = N4TopUp = N4TopUpOld = 0; N4Towns = 0;   // N4Stumbles zeruje linia NE (NightEcon)
        }

        /// <summary>OnItemProduced (miasto, klucz wlaczony): wyrob do puli koszyka; wartosc wykonana do porownania ze stara regula. Zwraca false - klucz wylaczony / nie miasto.</summary>
        private static bool CsAdd(Settlement st, ItemObject item, int count, double value)
        {
            if (!CraftSaleOn || st == null || !st.IsTown) return false;
            try
            {
                string k = CsKey(item);
                if (k == null) return false;
                Dictionary<string, CsPool> m;
                if (!_cs.TryGetValue(st, out m)) { m = new Dictionary<string, CsPool>(); _cs[st] = m; }
                CsPool p;
                if (!m.TryGetValue(k, out p)) { p = new CsPool(); m[k] = p; }
                p.U += count; p.V += Math.Max(0, value);
                double d; _csMade.TryGetValue(st, out d); _csMade[st] = d + Math.Max(0, value);
                return true;
            }
            catch (Exception e) { N4Stumbles++; Stumble("CsAdd", e); return false; }
        }

        /// <summary>ArmsScrap (zlom z nadmiaru 174): sztuki zdjete z polki bez sprzedazy - przy liczeniu doby schodza z puli najpierw i bez doplaty. Nie rzuca.</summary>
        internal static void ShelfGone(Settlement st, ItemObject item, int n)
        {
            try
            {
                if (n <= 0 || st == null || _cs.Count == 0) return;
                Dictionary<string, CsPool> m; CsPool p;
                string k = CsKey(item);
                if (k == null || !_cs.TryGetValue(st, out m) || !m.TryGetValue(k, out p)) return;
                p.Gone += n;
            }
            catch (Exception e) { N4Stumbles++; Stumble("ShelfGone", e); }
        }

        /// <summary>
        /// TownFolk.Daily (przed doba ludu i modelem na sucho): sztuki koszykow puli na polce kazdego miasta; zeszle z polki ponad zlom - wartosc z puli do f.ProdV
        /// (podstawa doplaty do 35%). Wartosc wykonana okna do porownania (TownLive). Klucz wylaczony - pula czyszczona (wroci od zera). Nie rzuca.
        /// </summary>
        private static void CraftSaleDay()
        {
            CsDayZero();
            var t = _csMadeDay; _csMadeDay = _csMade; _csMade = t; _csMade.Clear();
            foreach (var kv in _csMadeDay) { N4MadeV += kv.Value; }
            if (!CraftSaleOn) { _cs.Clear(); _csMadeDay.Clear(); N4PoolV = 0; N4PoolN = 0; return; }
            double poolV = 0; long poolN = 0;
            var empty = new List<Settlement>();
            var drop = new List<string>();
            var count = new Dictionary<string, int>();
            foreach (var kv in _cs)
            {
                var st = kv.Key;
                try
                {
                    var roster = st != null ? st.ItemRoster : null;
                    if (roster == null || st.Town == null) { empty.Add(st); continue; }
                    count.Clear();
                    for (int i = 0; i < roster.Count; i++)
                    {
                        var el = roster.GetElementCopyAtIndex(i);
                        if (el.Amount <= 0) continue;
                        string k = CsKey(el.EquipmentElement.Item);
                        if (k == null || !kv.Value.ContainsKey(k)) continue;
                        int c0; count.TryGetValue(k, out c0); count[k] = c0 + el.Amount;
                    }
                    Folk f; _f.TryGetValue(st, out f);
                    double soldV = 0; long soldN = 0;
                    drop.Clear();
                    foreach (var b in kv.Value)
                    {
                        var p = b.Value;
                        int c; count.TryGetValue(b.Key, out c);
                        if (p.U > c)
                        {
                            int left = p.U - c;
                            int gone = Math.Min(left, Math.Max(0, p.Gone));
                            int sold = left - gone;
                            double unit = p.U > 0 ? p.V / p.U : 0;
                            soldV += unit * sold; soldN += sold; N4GoneN += gone;
                            p.V = Math.Max(0, p.V - unit * left); p.U = c;
                        }
                        p.Gone = 0;
                        if (p.U <= 0) drop.Add(b.Key);
                        else { poolV += p.V; poolN += p.U; }
                    }
                    foreach (var k in drop) kv.Value.Remove(k);
                    if (kv.Value.Count == 0) empty.Add(st);
                    if (soldV > 0)
                    {
                        if (f != null && f.Kind == 0) f.ProdV += soldV;
                        N4SoldV += soldV; N4SoldN += soldN; N4Towns++;
                    }
                }
                catch (Exception e) { N4Stumbles++; Stumble("CraftSaleDay", e); }
            }
            foreach (var st in empty) _cs.Remove(st);
            N4PoolV = poolV; N4PoolN = poolN;   // wartosc wykonana okna zostaje w _csMadeDay do TownLive tej doby (porownanie)
        }

        /// <summary>N1(a) (linia "Pompy - sztuczne przelewy (nekon)"): przelewy z kas miast do ludu tej doby kiesy czynnej (-1 - doba ludu dzis nie biegla).</summary>
        internal static long LastLabourTowns { get { var d = _ld; return d != null && _liveDay == (int)CampaignTime.Now.ToDays - 1 ? d.LabT : -1; } }
        internal static long LastTurnTowns { get { var d = _ld; return d != null && _liveDay == (int)CampaignTime.Now.ToDays - 1 ? d.TurnT : -1; } }
        internal static long LastTopUpTowns { get { var d = _ld; return d != null && _liveDay == (int)CampaignTime.Now.ToDays - 1 ? d.TopT : -1; } }
        internal static long LastLabourCastles { get { var d = _ld; return d != null && _liveDay == (int)CampaignTime.Now.ToDays - 1 ? d.LabC : -1; } }

        /// <summary>TownLive: wartosc wyrobow miasta wykonanych w oknie (stara podstawa doplaty) - tylko porownanie.</summary>
        private static double CsMadeOf(Settlement st)
        {
            double v;
            return st != null && _csMadeDay.TryGetValue(st, out v) ? v : 0;
        }

        // ============================================================ zapis arm_craft_sale
        /// <summary>"cs1;S,miasto;B,koszyk,sztuki,wartosc;..." - pusty: nic do zapisania.</summary>
        internal static string ExportCraftSale()
        {
            try
            {
                if (_cs.Count == 0) return "";
                var sb = new StringBuilder(4096);
                sb.Append("cs1");
                foreach (var kv in _cs)
                {
                    if (kv.Key == null || kv.Key.StringId == null || kv.Value.Count == 0) continue;
                    sb.Append(";S,").Append(kv.Key.StringId);
                    foreach (var b in kv.Value)
                    {
                        if (b.Value.U <= 0 || b.Key.IndexOf(',') >= 0 || b.Key.IndexOf(';') >= 0) continue;
                        sb.Append(";B,").Append(b.Key).Append(',').Append(b.Value.U.ToString(CsInv)).Append(',').Append(Math.Round(b.Value.V).ToString("0", CsInv));
                    }
                }
                return sb.ToString();
            }
            catch (Exception e) { Log.Error("TownFolk.ExportCraftSale", e); return ""; }
        }

        internal static void ImportCraftSale(string data)
        {
            try
            {
                _cs.Clear();
                if (string.IsNullOrEmpty(data) || !data.StartsWith("cs1", StringComparison.Ordinal)) return;
                Dictionary<string, CsPool> cur = null;
                foreach (var rec in data.Split(';'))
                {
                    var f = rec.Split(',');
                    if (f.Length >= 2 && f[0] == "S")
                    {
                        cur = null;
                        var st = Settlement.Find(f[1]);
                        if (st == null || !st.IsTown) continue;
                        if (!_cs.TryGetValue(st, out cur)) { cur = new Dictionary<string, CsPool>(); _cs[st] = cur; }
                    }
                    else if (f.Length >= 4 && f[0] == "B" && cur != null)
                    {
                        int u; double v;
                        if (int.TryParse(f[2], NumberStyles.Integer, CsInv, out u) && u > 0 && double.TryParse(f[3], NumberStyles.Float, CsInv, out v) && v >= 0 && !double.IsInfinity(v))
                            cur[f[1]] = new CsPool { U = u, V = v };
                    }
                }
                var empty = new List<Settlement>();
                foreach (var kv in _cs) if (kv.Value.Count == 0) empty.Add(kv.Key);
                foreach (var st in empty) _cs.Remove(st);
            }
            catch (Exception e) { Log.Error("TownFolk.ImportCraftSale", e); }
        }
    }
}
