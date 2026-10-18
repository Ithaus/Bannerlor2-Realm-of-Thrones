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
            var r = _plan;
            return KsCore.StartIndex(ItemIds[i], r != null ? r.P : null, m);   // ta sama regula co krok 8 Planu (KsCore.StartIndex)
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
            // KS-R3 P5 (klucz Ks Market Control): ta sama podstawa co polki startu - popyt zaplacony miast przy lambda 1 x dni polki startu; zapas = polki miast + towar w drodze
            // KS-H, cel = polki + rurociag KS-H (wyslane 7 dob x srednie doby); towar spoza koszyka ZC4 - jak dotad (przeplyw na targ przy pelnej stawce KS)
            double[] dpM = null; var pipe = new double[2 * ItemCount];
            if (SwMarketControl && _zt.Count > 0)
            {
                dpM = new double[2 * ItemCount];
                foreach (var zt in _zt.Values)
                {
                    if (zt == null) continue;
                    int mo = (zt.Market == 0 ? 0 : 1) * ItemCount;
                    for (int i = 0; i < ItemCount; i++) { int z = RampZ(i); if (z >= 0) dpM[mo + i] += zt.Q[z] + zt.QV[z] + zt.Der[z] + zt.One[z]; }
                }
                try
                {
                    KsFoodCarts.ForEachInTransit((it, n, mk) =>
                    {
                        int ii; if (it == null || mk < 0 || mk > 1 || ix == null || !ix.TryGetValue(it, out ii)) return;
                        _rampStock[mk * ItemCount + ii] += n;
                    });
                }
                catch (Exception e) { Stumble("RampDaily(w drodze)", e); }
                for (int m = 0; m < 2; m++) for (int i = 0; i < ItemCount; i++) { int z = RampZ(i); if (z >= 0) pipe[m * ItemCount + i] = KsFoodCarts.Pipeline(m, z); }
            }
            for (int m = 0; m < 2; m++)
                for (int i = 0; i < ItemCount; i++)
                {
                    int k = m * ItemCount + i;
                    double baseD = dpM != null && RampZ(i) >= 0 ? dpM[k] : Math.Max(0.0, full != null && k < full.Length ? full[k] : 0.0);
                    double target = ShelfDays(StartPrice(m, i)) * baseD + pipe[k];
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

        /// <summary>KS-R3 P5: pozycja koszyka ZC4 towaru KS i (ten sam przedmiot) albo -1.</summary>
        private static int RampZ(int i) { return i >= 0 && i < ItemCount ? Array.IndexOf(ZIds, ItemIds[i]) : -1; }

        /// <summary>Polki startu w dobie D (rozdz. 6): ItemData.Supply miast = dni polki startu x wygladzony popyt kategorii, dla kategorii towarow KS i koszyka ZC4.</summary>
        private static void StartShelves(int today)
        {
            EnsureItems(); Zc4EnsureMap();
            var cats = new Dictionary<ItemCategory, int>();                      // kategoria -> towar KS (cena startu) albo -1 (1.0)
            for (int i = 0; i < ItemCount; i++) { var it = _items[i]; if (it != null && it.ItemCategory != null && !cats.ContainsKey(it.ItemCategory)) cats[it.ItemCategory] = i; }
            foreach (var c in _zCats.Keys) if (!cats.ContainsKey(c)) cats[c] = -1;
            int towns = 0, cells = 0, stum = 0, lift = 0, noShelf = 0; double sumIdx0 = 0, sumIdx1 = 0; int nIdx = 0;
            foreach (var town in Town.AllTowns)
            {
                try
                {
                    if (town == null || !town.IsTown || town.MarketData == null) continue;
                    if (LandBackOf(town.Settlement)) continue;           // ks-arm: kraina wrocona przez bramke krainy - bez polek startu
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
                        // F2 (poprawka po autotescie 16.10): bez sztucznych polek - wygladzona podaz tylko tam, gdzie prawdziwa polka ma co najmniej polowe celu
                        // (dotad 3295 z 3977 pozycji mialo polke ponizej celu, indeks 3.87 -> 0.98 w D i wracal do 2-5); reszta - cena wzoru gry z prawdziwej polki
                        // KS-R3 P5: cel polki z tej samej podstawy co rampa - popyt zaplacony miasta przy lambda 1 (wartosc kategorii), gdy miasto jest w mapie ZC4
                        double baseV = dem;
                        if (SwMarketControl) { double dv = DpValue(town, kv.Key); if (dv > 0.01) baseV = dv; }
                        if (sv < 0.5 * days * baseV) { noShelf++; continue; }
                        // recenzja runda 3 (drobne): ta sama podstawa w calym celu polki startu (dotad bramka z popytu zaplaconego, a want/sup z wygladzonego popytu gry)
                        double want = (0.14 * days * baseV - 0.04 * sv) / 0.1;
                        float sup = (float)Math.Max(0.1, Math.Min(1.4 * days * baseV, Math.Max(days * baseV * 0.5, want)));
                        if (sup > days * baseV * 1.001) lift++;
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
                         + " -> " + (nIdx > 0 ? (sumIdx1 / nIdx).ToString("0.00", Inv) : "-") + ", pozycji z podaza ponad dni x popyt (polka realna ponizej celu) " + lift
                         + ", bez ustawienia (prawdziwa polka ponizej polowy celu - cena z polki) " + noShelf + ", potkniecia " + stum;
            var ps = new StringBuilder(1200);
            for (int m = 0; m < 2; m++)
            {
                ps.Append(m == 0 ? " | ceny startu Westeros:" : " | ceny startu Essos:");
                for (int i = 0; i < ItemCount; i++) ps.Append(i == 0 ? " " : ", ").Append(ItemIds[i]).Append(' ').Append(StartPrice(m, i).ToString("0.00", Inv));
            }
            Log.Info(PK + "polki startu " + _shelfNote + ps + " | wygladzona podaz = (0.14 x dni polki startu (7 x cena^(-1/0.6)) x popyt (zaplacony miasta przy lambda 1, inaczej wygladzony popyt gry) - 0.04 x wartosc polki) / 0.1, w pasmie 0.5-1.4 x dni x popyt;"
                     + " to dzwignia CENY (ItemData.Supply - wygladzona podaz wzoru ceny gry), nie towar: na polki towaru i zlota nie przybywa, gra wygladza podaz 15% dziennie do prawdziwej polki (wygladzenie startu) | popyt koszyka ZC4 od tej doby przez " + (RampDemand - RampSupply) + " dob; srednie cen A7 i regale od zera.");
        }

        /// <summary>KS-R3 P5: popyt zaplacony miasta przy lambda 1 w wartosci kategorii (sztuki x wartosc) - podstawa celu polki startu; 0 - miasto poza mapa ZC4.</summary>
        private static double DpValue(Town t, ItemCategory c)
        {
            ZTown zt; int[] zs;
            if (t == null || t.Settlement == null || c == null || !_zt.TryGetValue(t.Settlement, out zt) || zt == null || !_zCats.TryGetValue(c, out zs)) return 0.0;
            double v = 0;
            foreach (int z in zs) { var it = _zItems[z]; if (it == null) continue; v += (zt.Q[z] + zt.QV[z] + zt.Der[z] + zt.One[z]) * Math.Max(1, it.Value); }
            return v;
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

        // ============================================================ kontrola wczesna D+7 (recenzja 17.10, wazne)
        // ceny na rynek jak NKS-7 (mediana indeksu miast od D wobec ceny startu +-0.3) i sufit wzoru ceny (> 20% miast) dla towarow rak; wiecej niz polowa par rynek x towar
        // poza pasmem - wynik biegu 120 dob znany z gory: w autotescie plik-przelacznik armoury-ks-przerwij.flag (lancuch przerywa bieg), w grze - tylko linia
        internal const int EarlyDays = 7;
        internal const string AbortFlagName = "armoury-ks-przerwij.flag";
        private static bool _earlyDone;
        private static readonly int[] EarlyItems = { IGrain, KsDeliver.IFish, IMeat, KsDeliver.ICheese, KsDeliver.IButter, KsDeliver.IGrape, KsDeliver.IOlives, KsDeliver.IOrange, KsDeliver.IDate,
                                                     KsDeliver.ISalt, KsDeliver.IWool, KsDeliver.IFlax };

        private static void EarlyCheck(int day)
        {
            if (_earlyDone) return;
            _earlyDone = true;
            var vals = new List<double>(64);
            int n = 0, far = 0, ceilBad = 0;
            var bad = new List<string>(); var cb = new List<string>();
            for (int m = 0; m < 2; m++)
                foreach (int i in EarlyItems)
                {
                    vals.Clear(); int ceil = 0, cnt = 0;
                    foreach (var kv in _px)
                    {
                        if (kv.Key == null || kv.Value == null || i >= kv.Value.Length || TownMarket(kv.Key) != m) continue;
                        vals.Add(kv.Value[i]);
                        double[] now;
                        if (_pxNow.TryGetValue(kv.Key, out now) && now != null && i < now.Length) { cnt++; if (now[i] >= CeilIx) ceil++; }
                    }
                    if (vals.Count < 3) continue;
                    double med = Med(vals), sp = StartPrice(m, i);
                    n++;
                    string nm = (m == 0 ? "Westeros " : "Essos ") + ItemIds[i];
                    if (med < sp - 0.3 || med > sp + 0.3) { far++; bad.Add(nm + " " + med.ToString("0.00", Inv) + " (start " + sp.ToString("0.00", Inv) + ")"); }
                    if (cnt > 0 && 100.0 * ceil / cnt > 20.0) { ceilBad++; cb.Add(nm + " " + (100 * ceil / cnt).ToString(Inv) + "%"); }
                }
            bool stop = n > 0 && 2 * far > n;
            bool flag = false;
            if (stop && AutotestMode())
            {
                try
                {
                    var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                    string dir = System.IO.Path.Combine(System.IO.Path.Combine(docs, "Mount and Blade II Bannerlord"), "CrashScribe");
                    if (System.IO.Directory.Exists(dir))
                    {
                        System.IO.File.WriteAllText(System.IO.Path.Combine(dir, AbortFlagName), "KS kontrola wczesna D+7 (dzien " + day + "): ceny poza pasmem " + far + " z " + n + "\r\n");
                        flag = true;
                    }
                }
                catch (Exception e) { Stumble("EarlyCheck(plik)", e); }
            }
            Log.Info(PK + "kontrola wczesna D+" + EarlyDays + " dzien " + day + " (D " + DDay + ") | ceny na rynek (mediana indeksu miast od D wobec ceny startu +-0.3, towary rak): w pasmie " + (n - far) + " z " + n
                     + (bad.Count > 0 ? " (poza: " + string.Join(", ", bad.ToArray()) + ")" : "") + " | sufit wzoru ceny w > 20% miast: " + ceilBad + (cb.Count > 0 ? " (" + string.Join(", ", cb.ToArray()) + ")" : "")
                     + " | WYNIK: " + (stop ? "PRZERWAC - ponad polowa poza pasmem, bieg 120 dob nie przejdzie NKS-7" + (flag ? " (autotest: plik " + AbortFlagName + " zapisany)" : (AutotestMode() ? " (pliku nie zapisano)" : " (gra - tylko linia)")) : "dalej") + ".");
        }
    }
}
