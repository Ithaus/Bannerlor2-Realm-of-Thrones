using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// KS ETAP 2 - RAMPA I POLKI STARTU (projekt KS rozdz. 6 i 11.2):
    ///  - PODAZ: od doby wlaczenia A do D = A + 14 stawka wsi i plon pol mieszczan przechodza liniowo z lancucha gry na stawke KS - beta na towar i rynek (Westeros, Essos);
    ///    ZAPAS TYLKO Z PRAWDZIWEJ NADWYZKI, LICZONY TOWAR PO TOWARZE: gdy polki miast rynku maja juz zapas celu (dni polki startu x dzienny przeplyw towaru na targ przy pelnej
    ///    stawce KS), beta tego towaru stoi (nie rosnie) i czeka, az dojdzie do niej popyt koszyka - nadwyzka nie zalewa polek; beta nigdy ponizej lambdy popytu
    ///    i od A + 28 zawsze 1;
    ///  - POPYT KOSZYKA ZC4: lambda 0 do D, potem 0 -> 1 przez 14 dob (D..D + 14; StartCalibration.Zc4.cs) - projekt KS 11.2 i rozdz. 6: dwa trojkaty (podaz D-14..D,
    ///    popyt D..D+14) daja 14 dob pelnej nadwyzki na polki startu (recenzja 16.10: dotad lambda od A dawala 7 dob - miod, oliwki i mieso zaczynaly ponizej celu);
    ///  - POLKI STARTU w dobie D: wygladzona podaz rynku miasta (ItemData.Supply) dostaje wartosc polki startu = 7 x cena startu^(-1/0.6) dni wygladzonego popytu
    ///    (wzor ceny gry: (popyt / (0.1 x podaz + 0.04 x wartosc polki + 2))^0.6 - polka 7 dni zakupow przy podazy = wartosci polki = indeks 1; przy polce realnej
    ///    ponizej celu podaz dostaje brakujacy skladnik 0.04 x polka / 0.1 - najwyzej x1.4 - zeby indeks w D byl cena startu, recenzja 16.10);
    ///    cena startu = cena cienia Planu w pasmie 0.7-1.5, towar psujacy sie (ryby, mieso, jaja, maslo, owoce, winogrona)
    ///    co najmniej 1.0 (najwyzej 7 dni polki), domowe i uboczne (jaja, miod, marchew, jablka, skory), zwierzeta i regale - 1.0. Jedyne "ustawienie" ceny: nie tworzy
    ///    towaru ani zlota i wygasa samo (gra wygladza podaz 15% dziennie do prawdziwej polki).
    /// Rampa w zapisie (arm_ks): beta 2 x 33 i lambda 2; zapis etapu 1 (ks1) - rampa za nami (wszystko 1).
    /// </summary>
    internal static partial class StartCalibration
    {
        private static float[] _beta = new float[2 * ItemCount];          // opublikowane (haki produkcji moga isc z watkow AI)
        private static readonly double[] _lam = new double[2];
        private static int _rampHeld, _rampStocked;                          // licznik doby: towarow z beta wstrzymana (zapas celu) i z zapasem celu
        private static readonly double[] _rampStock = new double[2 * ItemCount], _rampTarget = new double[2 * ItemCount];
        private static Dictionary<Town, int> _townMk;
        private static readonly Dictionary<Settlement, int> _setMk = new Dictionary<Settlement, int>();   // zamki i inne osady: rynek z kultury (GoodsLedger - podzial na rynki)
        private static string _shelfNote;
        private static readonly HashSet<string> Perish = new HashSet<string>(StringComparer.Ordinal) { "fish", "meat", "WhaleMeat", "Egg", "butter", "apple", "carrot", "orange", "grape", "date_fruit" };
        private static readonly HashSet<string> PriceOne = new HashSet<string>(StringComparer.Ordinal) { "Egg", "honey", "carrot", "apple", "hides", "cow", "hog", "sheep", "spice", "whale_oil", "walrus_tusk" };

        private static void RampReset()
        {
            _beta = new float[2 * ItemCount]; _lam[0] = _lam[1] = 0; _rampHeld = _rampStocked = 0; _townMk = null; _setMk.Clear(); _shelfNote = null; _rampDay = -1; _wasOff = false;
            Array.Clear(_rampStock, 0, _rampStock.Length); Array.Clear(_rampTarget, 0, _rampTarget.Length);
        }

        private static int _rampDay = -1;                                     // poczatek rampy (A): wlaczenie albo ponowne wlaczenie klucza po przerwie (Undo -> Redo)
        private static bool _wasOff;                                         // stan zastosowany przy wylaczonym kluczu (sesja albo zapis) - przy ponownym wlaczeniu rampa od nowa

        private static void RampStart(int today) { _beta = new float[2 * ItemCount]; _lam[0] = _lam[1] = 0; _rampDay = today; _wasOff = false; }

        /// <summary>Beta podazy towaru KS i na rynku m (0..1); poza stanem zastosowanym - 0.</summary>
        internal static float Beta(int m, int i)
        {
            var b = _beta;
            int k = (m == 0 ? 0 : ItemCount) + i;
            return k >= 0 && k < b.Length ? b[k] : 0f;
        }

        /// <summary>Wszystkie towary rynku m z pelna stawka KS (po rampie).</summary>
        private static bool RampFull(int m)
        {
            var b = _beta; int o = m == 0 ? 0 : ItemCount;
            for (int i = 0; i < ItemCount; i++) if (b[o + i] < 0.9995f) return false;
            return true;
        }

        /// <summary>Lambda popytu koszyka ZC4 rynku m (0..1); poza stanem zastosowanym - 0.</summary>
        internal static double Lam(int m) { return Applied ? _lam[m == 0 ? 0 : 1] : 0.0; }

        /// <summary>Rynek miasta: z pliku ziemi (pola mieszczan), inaczej z kultury (Westeros / Essos).</summary>
        private static int TownMarket(Town t)
        {
            if (t == null) return 0;
            if (_townMk == null)
            {
                var m = new Dictionary<Town, int>();
                foreach (var tt in Town.AllTowns)
                {
                    if (tt == null || tt.Settlement == null) continue;
                    LandDist ld;
                    if (_landTown != null && tt.Settlement.StringId != null && _landTown.TryGetValue(tt.Settlement.StringId, out ld)) m[tt] = ld.Market;
                    else
                    {
                        string cul = null; try { cul = PeopleBook.CultureAtCalibration(tt.Settlement); } catch { cul = null; }
                        if (string.IsNullOrEmpty(cul)) try { cul = tt.Settlement.Culture != null ? tt.Settlement.Culture.StringId : null; } catch { cul = null; }
                        m[tt] = cul != null && WestLands.Contains(cul) ? 0 : 1;
                    }
                }
                _townMk = m;
            }
            int r; return _townMk.TryGetValue(t, out r) ? r : 0;
        }

        /// <summary>GoodsLedger (KS, odbior na rynek): rynek osady - wies przez miasto targowe, miasto z pliku ziemi albo kultury, zamek z kultury z kalibracji; -1 - nie wiadomo.
        /// Tylko watek glowny.</summary>
        internal static int MarketOfSettlement(Settlement st)
        {
            if (st == null) return -1;
            if (st.IsVillage && st.Village != null) { st = st.Village.TradeBound ?? st.Village.Bound; if (st == null) return -1; }
            var t = st.Town;
            if (t != null && t.IsTown) return TownMarket(t);
            int r;
            if (_setMk.TryGetValue(st, out r)) return r;
            string cul = null;
            try { cul = PeopleBook.CultureAtCalibration(st); } catch { cul = null; }
            if (string.IsNullOrEmpty(cul)) try { cul = st.Culture != null ? st.Culture.StringId : null; } catch { cul = null; }
            r = cul == null ? -1 : (WestLands.Contains(cul) ? 0 : 1);
            _setMk[st] = r;
            return r;
        }

        /// <summary>Cena startu towaru KS i na rynku m (rozdz. 6): cena cienia Planu w 0.7-1.5; psujace sie >= 1.0; domowe, uboczne, zwierzeta, regale - 1.0.</summary>
        private static double StartPrice(int m, int i)
        {
            string id = ItemIds[i];
            if (PriceOne.Contains(id)) return 1.0;
            int g;
            if (!KsTab.GoodIndex.TryGetValue(id == "WhaleMeat" ? "meat" : id, out g) || KsTab.RegGoodMask[g]) return 1.0;
            var r = _plan;
            double p = r != null && r.P != null && r.P.Length == 2 * KsTab.NG ? r.P[(m == 0 ? 0 : 1) * KsTab.NG + g] : 1.0;
            if (!(p > 0) || double.IsNaN(p)) p = 1.0;
            p = Math.Max(0.7, Math.Min(1.5, p));
            if (Perish.Contains(id)) p = Math.Max(1.0, p);
            return p;
        }

        /// <summary>Dni polki startu: 7 x cena^(-1/0.6) (wzor ceny gry z wykladnikiem 0.6).</summary>
        private static double ShelfDays(double p) { return 7.0 * Math.Pow(Math.Max(0.3, p), -1.0 / 0.6); }

        /// <summary>Doba stanu zastosowanego (po stawkach jutra): beta i lambda na jutro. full - przeplyw na targ przy pelnej stawce KS [rynek x towar] (sztuki/d).</summary>
        private static void RampDaily(int today, double[] full)
        {
            int t = today - _rampDay;
            double r = Math.Max(0.0, Math.Min(1.0, t / (double)RampSupply));
            double lam = Math.Max(0.0, Math.Min(1.0, (t - RampSupply) / (double)(RampDemand - RampSupply)));   // popyt koszyka od D do D+14 (recenzja 16.10)
            _lam[0] = _lam[1] = lam;
            Array.Clear(_rampStock, 0, _rampStock.Length);
            EnsureItems();
            var ix = _itemIx;
            if (t < RampDemand && ix != null)
            {
                // zapas miast rynku wedlug towaru KS (jedno przejscie po polkach)
                foreach (var town in Town.AllTowns)
                {
                    try
                    {
                        if (town == null || !town.IsTown || town.Settlement == null) continue;
                        var ro = town.Settlement.ItemRoster;
                        if (ro == null) continue;
                        int m = TownMarket(town) * ItemCount;
                        for (int k = 0; k < ro.Count; k++)
                        {
                            var el = ro.GetElementCopyAtIndex(k);
                            int i;
                            if (el.Amount > 0 && el.EquipmentElement.Item != null && ix.TryGetValue(el.EquipmentElement.Item, out i)) _rampStock[m + i] += el.Amount;
                        }
                    }
                    catch (Exception e) { Stumble("RampDaily(miasto)", e); }
                }
            }
            var nb = new float[2 * ItemCount];
            var ob = _beta;
            int held = 0, stocked = 0;
            for (int m = 0; m < 2; m++)
                for (int i = 0; i < ItemCount; i++)
                {
                    int k = m * ItemCount + i;
                    double target = ShelfDays(StartPrice(m, i)) * Math.Max(0.0, full != null && k < full.Length ? full[k] : 0.0);
                    _rampTarget[k] = target;
                    double b;
                    if (t >= RampDemand) b = 1.0;
                    else if (target > 0 && _rampStock[k] >= target) { b = Math.Max(lam, Math.Min(r, ob[k])); stocked++; if (b < r - 1e-6) held++; }
                    else b = Math.Max(lam, r);
                    nb[k] = (float)Math.Max(0.0, Math.Min(1.0, b));
                }
            _beta = nb;
            _rampHeld = held; _rampStocked = stocked;
        }

        /// <summary>Polki startu w dobie D (rozdz. 6): ItemData.Supply miast = dni polki startu x wygladzony popyt kategorii, dla kategorii towarow KS i koszyka ZC4.</summary>
        private static void StartShelves(int today)
        {
            EnsureItems(); Zc4EnsureMap();
            var cats = new Dictionary<ItemCategory, int>();                      // kategoria -> towar KS (cena startu) albo -1 (1.0)
            for (int i = 0; i < ItemCount; i++) { var it = _items[i]; if (it != null && it.ItemCategory != null && !cats.ContainsKey(it.ItemCategory)) cats[it.ItemCategory] = i; }
            foreach (var c in _zCats.Keys) if (!cats.ContainsKey(c)) cats[c] = -1;
            int towns = 0, cells = 0, stum = 0, lift = 0; double sumIdx0 = 0, sumIdx1 = 0; int nIdx = 0;
            foreach (var town in Town.AllTowns)
            {
                try
                {
                    if (town == null || !town.IsTown || town.MarketData == null) continue;
                    int m = TownMarket(town);
                    var md = town.MarketData;
                    foreach (var kv in cats)
                    {
                        var d = md.GetCategoryData(kv.Key);
                        float dem = d.Demand;
                        if (!(dem > 0.01f)) continue;
                        double p = kv.Value >= 0 ? StartPrice(m, kv.Value) : 1.0;
                        // wzor ceny: 0.1 x podaz + 0.04 x wartosc polki; cel 0.14 x dni x popyt - podaz dopelnia to, czego brakuje realnej polce (najwyzej x1.4 dni x popyt)
                        double days = ShelfDays(p), sv = Math.Max(0.0, d.InStoreValue);
                        double want = (0.14 * days * dem - 0.04 * sv) / 0.1;
                        float sup = (float)Math.Max(0.1, Math.Min(1.4 * days * dem, Math.Max(days * dem * 0.5, want)));
                        if (sup > days * dem * 1.001) lift++;
                        float i0 = md.GetPriceFactor(kv.Key);
                        md.SetSupplyDemand(kv.Key, sup, dem);
                        float i1 = md.GetPriceFactor(kv.Key);
                        sumIdx0 += i0; sumIdx1 += i1; nIdx++;
                        cells++;
                    }
                    towns++;
                }
                catch (Exception e) { if (stum++ < 1) Stumble("StartShelves(miasto)", e); }
            }
            _shelfNote = "doba " + today + ": miast " + towns + ", kategorii " + cats.Count + " (" + cells + " pozycji), srednia indeksu " + (nIdx > 0 ? (sumIdx0 / nIdx).ToString("0.00", Inv) : "-")
                         + " -> " + (nIdx > 0 ? (sumIdx1 / nIdx).ToString("0.00", Inv) : "-") + ", pozycji z podaza ponad dni x popyt (polka realna ponizej celu) " + lift + ", potkniecia " + stum;
            var ps = new StringBuilder(1200);
            for (int m = 0; m < 2; m++)
            {
                ps.Append(m == 0 ? " | ceny startu Westeros:" : " | ceny startu Essos:");
                for (int i = 0; i < ItemCount; i++) ps.Append(i == 0 ? " " : ", ").Append(ItemIds[i]).Append(' ').Append(StartPrice(m, i).ToString("0.00", Inv));
            }
            Log.Info(PK + "polki startu " + _shelfNote + ps + " | wygladzona podaz = (0.14 x dni polki startu (7 x cena^(-1/0.6)) x wygladzony popyt - 0.04 x wartosc polki) / 0.1, w pasmie 0.5-1.4 x dni x popyt;"
                     + " towaru i zlota nie przybywa, gra wygladza podaz 15% dziennie do prawdziwej polki | popyt koszyka ZC4 od tej doby przez " + (RampDemand - RampSupply) + " dob; srednie cen A7 i regale od zera.");
        }

        private static string RampText(int today)
        {
            int t = today - _rampDay;
            if (t >= RampDemand) return "rampa skonczona (doba " + (_rampDay + RampDemand) + "), polki startu " + (_shelvesDone ? "ustawione" : "czekaja");
            return "rampa doba " + t + "/" + RampDemand + ": podaz r " + Math.Min(1.0, t / (double)RampSupply).ToString("0.00", Inv) + ", lambda popytu (od D) " + _lam[0].ToString("0.00", Inv)
                   + ", towary z zapasem celu " + _rampStocked + " (beta wstrzymana " + _rampHeld + ")";
        }

        /// <summary>Linia "KS: rampa dzien N": beta i zapas wobec celu wedlug towaru i rynku (w rampie), lambda.</summary>
        private static string RampLine(int day)
        {
            var sb = new StringBuilder(2500);
            int t = day + 1 - _rampDay;
            sb.Append(PK).Append("rampa dzien ").Append(day).Append(" | doba rampy ").Append(t).Append(" (A ").Append(_rampDay).Append(", D ").Append(DDay).Append(", koniec popytu ").Append(_rampDay + RampDemand)
              .Append(") | lambda popytu koszyka ").Append(_lam[0].ToString("0.00", Inv));
            if (t > RampDemand) { sb.Append(" | rampa skonczona - beta 1 wszystkich towarow | polki startu: ").Append(_shelfNote ?? (_shelvesDone ? "ustawione" : "-")).Append('.'); return sb.ToString(); }
            for (int m = 0; m < 2; m++)
            {
                sb.Append(" | ").Append(m == 0 ? "Westeros" : "Essos").Append(" beta (zapas/cel szt.):");
                bool first = true;
                for (int i = 0; i < ItemCount; i++)
                {
                    int k = m * ItemCount + i;
                    if (_rampTarget[k] < 1 && _rampStock[k] < 1) continue;
                    sb.Append(first ? " " : ", ").Append(ItemIds[i]).Append(' ').Append(_beta[k].ToString("0.00", Inv)).Append(" (").Append(F0(_rampStock[k])).Append('/').Append(F0(_rampTarget[k])).Append(')');
                    first = false;
                }
            }
            sb.Append(" | polki startu: ").Append(_shelfNote ?? (_shelvesDone ? "ustawione" : "w dobie D " + DDay)).Append('.');
            return sb.ToString();
        }

        private static void RampExport(StringBuilder sb)
        {
            var b = _beta;
            for (int k = 0; k < b.Length; k++) { if (k > 0) sb.Append(','); sb.Append(b[k].ToString("0.####", Inv)); }
            sb.Append('|').Append(_lam[0].ToString("0.####", Inv)).Append(',').Append(_lam[1].ToString("0.####", Inv)).Append(',').Append(_rampDay).Append(',').Append(_wasOff || !KeyOn ? 1 : 0);
        }

        private static void RampImport(string betas, string lams)
        {
            var nb = new float[2 * ItemCount];
            if (!string.IsNullOrEmpty(betas))
            {
                var a = betas.Split(',');
                for (int k = 0; k < a.Length && k < nb.Length; k++) { float v; if (float.TryParse(a[k], System.Globalization.NumberStyles.Float, Inv, out v) && v >= 0 && v <= 1) nb[k] = v; }
            }
            _beta = nb;
            if (!string.IsNullOrEmpty(lams))
            {
                var a = lams.Split(',');
                for (int m = 0; m < 2 && m < a.Length; m++) { double v; if (double.TryParse(a[m], System.Globalization.NumberStyles.Float, Inv, out v) && v >= 0 && v <= 1) _lam[m] = v; }
                int rd, off;
                if (a.Length >= 3 && int.TryParse(a[2], System.Globalization.NumberStyles.Integer, Inv, out rd)) _rampDay = rd;
                if (a.Length >= 4 && int.TryParse(a[3], System.Globalization.NumberStyles.Integer, Inv, out off)) _wasOff = off != 0;
            }
            if (_rampDay < 0) _rampDay = _applyDay;
        }

        /// <summary>Ponowne wlaczenie klucza Start Calibration na zapisie zastosowanym (po Undo): podaz i popyt koszyka znow przez rampe od dzisiaj (bez skoku), polki startu - nie.</summary>
        private static void RampRestart(int today)
        {
            RampStart(today);
            _liveFull = true;
            Log.Info(PK + "klucz Start Calibration znow wlaczony doba " + today + " na zapisie zastosowanym od doby " + _applyDay + " - rampa od nowa: podaz KS doby " + today + ".." + (today + RampSupply)
                     + ", popyt koszyka ZC4 doby " + today + ".." + (today + RampDemand) + " (bez skoku produkcji i popytu); polki startu nie sa ustawiane drugi raz.");
        }

        private static void RampImportFull()
        {
            var nb = new float[2 * ItemCount];
            for (int k = 0; k < nb.Length; k++) nb[k] = 1f;
            _beta = nb; _lam[0] = _lam[1] = 1.0; _rampDay = _applyDay - RampDemand;
        }
    }
}
