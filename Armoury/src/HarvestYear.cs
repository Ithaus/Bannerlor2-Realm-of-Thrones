using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// R3-05e (Jeff 20.10 "pozostale tak"; audyt 10-pomysly Z6, F10) - ROK URODZAJU I ZLY ROK. Klucz MCM Harvest Year Enabled (domyslnie TAK).
    ///
    /// Przyczyna -> skutek: pogoda roku w okolicy -> plon pol x0.8..x1.2 (rzadko x0.6 - rok glodu) -> spichlerze okregow (Famine), magazyn wsi i targ
    /// -> cena zboza rosnie tam, gdzie brak, wozy i karawany wioza zboze z urodzaju do glodu (juz wedlug zysku). Zadnego skryptu glodu, zadnego zlota.
    ///
    /// LOSOWANIE: raz na rok kalendarza, w pierwszej dobie okna zniw (FreeHands.HarvestWindow - dni 196-252 roku 364 i pora Westeros lato albo jesien;
    /// w latach zimy okna nie ma, wiec i losowania). POGODA JAKO POLE NA MAPIE: siatka wezlow co 1/8 dluzszego boku mapy (prostokat wszystkich osad);
    /// kazdy wezel losuje swoj rok - zwykly x(1 + 0.2 x (u1 + u2 - 1)) (rozklad trojkatny 0.8..1.2, srednia 1), z szansa 1/20 rok glodu x0.6 (1315-17:
    /// plon 2:1 wobec do 7:1 w dobre lata; szansa - szacunek audytu "ok. raz na 20 lat"). Region (okreg warowni wsi - Village.Bound) bierze wartosc pola
    /// w polozeniu swojej warowni (interpolacja dwuliniowa z wygladzeniem) - caly okreg ten sam rok, sasiednie okregi podobny, dalekie niezalezne.
    /// Pola mieszczan (KS) - wartosc pola w polozeniu miasta.
    ///
    /// GDZIE MNOZY (jeden czynnik, wszystkie drogi plonu pol):
    ///  - lancuch produkcji wsi gry (MaterialLaw.ProdPostfix, po rekach E4/5 i E4/7, tym samym wzorem "czynnik x suma") i jego odczyt ChainFactor - tylko
    ///    plony pol i sadow: zboze, len, winogrona, oliwki, daktyle, bawelna, papirus, jablka, pomarancze, marchew (zwierzeta, kopaliny, ryby, las - bez zmian);
    ///  - zywnosc wsi gry (postfiks DefaultVillageProductionCalculatorModel.CalculateDailyFoodProductionAmount - mnozy sie z zima, rekami E4/5 i E4/7) -
    ///    to plon spichlerza okregu (Famine); bez wsi rybackich, wielorybniczych i lowcow morsow (ich zywnosc to morze);
    ///  - rachunek KS wsi i pol mieszczan po wlaczeniu kalibracji (StartCalibration.ComputeDistrict z czynnikami gry) - te same towary.
    /// ZIMA: w zimie Westeros (WesterosClimate.Now) czynnik = 1 - plon tnie zima (WinterBite), dwie reguly na jedno zjawisko nie nakladaja sie
    /// (ryzyko audytu: zly rok na wieloletniej zimie). Rok wazny 364 doby od losowania (rok gry); potem 1 do nastepnego okna.
    /// Start neutralny: do pierwszego losowania (nowa kampania albo pierwsze wczytanie z kluczem) wszedzie x1.
    /// Zapis: arm_harvest_year (SaveText.Sync) - doba i rok losowania, siatka i wartosci wezlow; brak klucza = stary zapis (x1 do nastepnego okna).
    /// Watki: odczyt z watkow AI (model wsi w PartiesThink) - jedna referencja migawki (slownik osada -> czynnik), budowana pod blokada, po publikacji
    /// tylko czytana; flaga czynnosci doby ustawiana na watku glownym. Wyjatek na obiekcie = potkniecie (licznik), czynnik 1, bez wylacznikow.
    /// Linia logu "Rok urodzaju (R3-05e):" przy losowaniu, przy zmianie stanu i co 28 dob.
    /// </summary>
    internal static class HarvestYear
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static readonly TextObject _txt = new TextObject("{=!}Harvest year");
        private const float Famine = 0.6f, Spread = 0.2f, FamineChance = 1f / 20f;
        private const int Cells = 8;                         // wezly co 1/8 dluzszego boku mapy

        private static readonly HashSet<string> Crops = new HashSet<string>(StringComparer.Ordinal)
            { "grain", "flax", "grape", "olives", "date_fruit", "cotton", "Papyrus", "apple", "orange", "carrot" };
        private static readonly HashSet<string> SeaTypes = new HashSet<string>(StringComparer.Ordinal) { "fisherman", "whaler", "walrus_hunter" };
        private static bool[] _ksCrop;                       // maska towarow KS (KsDeliver.ItemIds) - plony pol

        // stan (watek glowny; zapis)
        private static Campaign _camp;
        private static int _drawDay = -1, _drawYear = -1;
        private static float _minX, _minY, _cell;
        private static int _nx, _ny;
        private static float[] _nodes;                       // wartosci wezlow [iy * nx + ix]

        // migawka dla watkow AI
        private sealed class Snap { public Dictionary<Settlement, float> F; }
        private static volatile Snap _snap;
        private static volatile bool _active;
        private static readonly object Gate = new object();

        private static int _stumbles, _lastLogDay = -1;
        private static bool _lastActive, _firstLine;
        private static readonly HashSet<string> _errOnce = new HashSet<string>();

        internal static bool KeyOn { get { var s = Settings.Current; return s != null && s.HarvestYearEnabled; } }

        private static void Stumble(string where, Exception e)
        {
            System.Threading.Interlocked.Increment(ref _stumbles);
            try { lock (_errOnce) { if (!_errOnce.Add(where)) return; } Log.Error("HarvestYear." + where, e); } catch { }
        }

        private static void ResetIfNewCampaign()
        {
            var cur = Campaign.Current;
            if (ReferenceEquals(cur, _camp)) return;
            _camp = cur;
            _drawDay = -1; _drawYear = -1; _nodes = null; _nx = _ny = 0; _cell = 0f;
            _snap = null; _active = false; _lastLogDay = -1; _lastActive = false; _firstLine = false; _stumbles = 0;
            lock (_errOnce) _errOnce.Clear();
        }

        private static int YearDays { get { return Math.Max(28, CampaignTime.DaysInYear); } }

        // ------------------------------------------------------------ odczyt (watki AI)
        private static bool IsCrop(ItemObject item) { var id = item != null ? item.StringId : null; return id != null && Crops.Contains(id); }

        private static float Of(Settlement st)
        {
            if (st == null) return 1f;
            var sn = _snap;
            if (sn == null) { EnsureSnap(); sn = _snap; if (sn == null) return 1f; }
            float f;
            return sn.F.TryGetValue(st, out f) ? f : 1f;
        }

        /// <summary>Czynnik roku dla towaru wsi (plon pol i sadow), 1 poza tym albo bez czynnego roku. Nie rzuca.</summary>
        internal static float Factor(Village village, ItemObject item)
        {
            try
            {
                if (!_active || village == null || village.Settlement == null || !IsCrop(item)) return 1f;
                return Of(village.Settlement);
            }
            catch (Exception e) { Stumble("Factor", e); return 1f; }
        }

        /// <summary>Krok lancucha MaterialLaw.ProdPostfix: wynik x czynnik roku (czynnik x suma dotychczasowych - jak E4/5, E4/7).</summary>
        internal static void ProdStep(Village village, ItemObject item, ref ExplainedNumber r)
        {
            float f = Factor(village, item);
            if (f == 1f) return;
            float sum = 1f + r.SumOfFactors;
            if (sum > 0f) r.AddFactor((f - 1f) * sum, _txt);
        }

        /// <summary>Postfiks DefaultVillageProductionCalculatorModel.CalculateDailyFoodProductionAmount: zywnosc wsi (plon spichlerza) x czynnik roku.</summary>
        public static void FoodPostfix(Village village, ref float __result)
        {
            try
            {
                if (!_active || village == null || village.Settlement == null || __result <= 0f) return;
                var vt = village.VillageType;
                if (vt != null && vt.StringId != null && SeaTypes.Contains(vt.StringId)) return;
                __result *= Of(village.Settlement);
            }
            catch (Exception e) { Stumble("FoodPostfix", e); }
        }

        /// <summary>KS (StartCalibration.ComputeDistrict z czynnikami gry): plony pol okregu (wies albo pola mieszczan miasta) x czynnik roku.</summary>
        internal static void KsApply(Settlement st, double[] prod)
        {
            try
            {
                if (!_active || st == null || prod == null) return;
                float f = Of(st);
                if (f == 1f) return;
                var mask = _ksCrop;
                if (mask == null)
                {
                    var ids = KsDeliver.ItemIds; mask = new bool[ids.Length];
                    for (int i = 0; i < ids.Length; i++) mask[i] = Crops.Contains(ids[i]);
                    _ksCrop = mask;
                }
                int n = Math.Min(mask.Length, prod.Length);
                for (int i = 0; i < n; i++) if (mask[i]) prod[i] *= f;
            }
            catch (Exception e) { Stumble("KsApply", e); }
        }

        // ------------------------------------------------------------ pole pogody
        private static float Smooth(float t) { t = MBMath.ClampFloat(t, 0f, 1f); return t * t * (3f - 2f * t); }

        private static float FieldAt(float x, float y)
        {
            var nodes = _nodes;
            if (nodes == null || _cell <= 0f || _nx < 2 || _ny < 2) return 1f;
            float gx = (x - _minX) / _cell, gy = (y - _minY) / _cell;
            int ix = (int)Math.Floor(gx), iy = (int)Math.Floor(gy);
            ix = Math.Max(0, Math.Min(_nx - 2, ix)); iy = Math.Max(0, Math.Min(_ny - 2, iy));
            float tx = Smooth(gx - ix), ty = Smooth(gy - iy);
            float a = nodes[iy * _nx + ix], b = nodes[iy * _nx + ix + 1], c = nodes[(iy + 1) * _nx + ix], d = nodes[(iy + 1) * _nx + ix + 1];
            float v = (a * (1f - tx) + b * tx) * (1f - ty) + (c * (1f - tx) + d * tx) * ty;
            return MBMath.ClampFloat(v, Famine, 1f + Spread);
        }

        /// <summary>Migawka osada -> czynnik z pola (wsie - polozenie warowni okregu, miasta - wlasne). Pod blokada; publikacja jednej referencji.</summary>
        private static void EnsureSnap()
        {
            if (_snap != null || _nodes == null) return;
            lock (Gate)
            {
                if (_snap != null || _nodes == null) return;
                var map = new Dictionary<Settlement, float>();
                foreach (var st in Settlement.All)
                {
                    try
                    {
                        if (st == null) continue;
                        Settlement at = null;
                        if (st.IsVillage && st.Village != null) at = st.Village.Bound ?? st;
                        else if (st.IsTown) at = st;
                        if (at == null) continue;
                        var p = at.GetPosition2D;
                        map[st] = FieldAt(p.X, p.Y);
                    }
                    catch (Exception e) { Stumble("EnsureSnap", e); }
                }
                _snap = new Snap { F = map };
            }
        }

        private static uint Fnv(string s)
        {
            uint h = 2166136261;
            if (s != null) foreach (char ch in s) { h ^= ch; h *= 16777619; }
            return h;
        }

        private static void Draw(int today, int year)
        {
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            foreach (var st in Settlement.All)
            {
                if (st == null || !(st.IsVillage || st.IsTown || st.IsCastle)) continue;
                var p = st.GetPosition2D;
                if (p.X < minX) minX = p.X; if (p.Y < minY) minY = p.Y; if (p.X > maxX) maxX = p.X; if (p.Y > maxY) maxY = p.Y;
            }
            if (!(maxX > minX) || !(maxY > minY)) return;
            float cell = Math.Max(maxX - minX, maxY - minY) / Cells;
            int nx = (int)Math.Ceiling((maxX - minX) / cell) + 1, ny = (int)Math.Ceiling((maxY - minY) / cell) + 1;
            nx = Math.Max(2, nx); ny = Math.Max(2, ny);
            string camp = ""; try { camp = Campaign.Current.UniqueGameId ?? ""; } catch { }
            var rnd = new Random((int)(Fnv(camp + "|urodzaj|" + year) & 0x7FFFFFFF));
            var nodes = new float[nx * ny];
            for (int i = 0; i < nodes.Length; i++)
            {
                if (rnd.NextDouble() < FamineChance) nodes[i] = Famine;
                else nodes[i] = 1f + Spread * (float)(rnd.NextDouble() + rnd.NextDouble() - 1.0);
            }
            lock (Gate)
            {
                _minX = minX; _minY = minY; _cell = cell; _nx = nx; _ny = ny; _nodes = nodes;
                _drawDay = today; _drawYear = year; _snap = null;
            }
            EnsureSnap();
        }

        // ------------------------------------------------------------ doba
        /// <summary>Z ArmouryBehavior.OnDailyTickBodyInner (po WinterBite.OnDaily). Losowanie w oknie zniw, flaga czynnosci, linia logu. Nie rzuca.</summary>
        internal static void Daily()
        {
            try
            {
                ResetIfNewCampaign();
                int today = (int)CampaignTime.Now.ToDays;
                if (!KeyOn)
                {
                    if (_active || !_firstLine) { _active = false; _firstLine = true; Log.Info("Rok urodzaju (R3-05e): WYLACZONY (Harvest Year Enabled = off) - plony pol bez roku urodzaju, jak dotad."); }
                    _lastActive = false;
                    return;
                }
                int yd = YearDays, year = today / yd;
                bool window = FreeHands.HarvestWindow();
                bool drew = false;
                if (window && year != _drawYear) { Draw(today, year); drew = _drawYear == year; }
                bool winter = WesterosClimate.Now() == CampaignTime.Seasons.Winter;
                _active = _nodes != null && _drawDay >= 0 && today - _drawDay < yd && !winter;
                if (drew || _active != _lastActive || !_firstLine || today - _lastLogDay >= 28)
                {
                    _lastLogDay = today; _firstLine = true;
                    Log.Info(Line(today, window, winter, drew));
                    if (drew) Tell();
                }
                _lastActive = _active;
            }
            catch (Exception e) { Stumble("Daily", e); }
        }

        private static string Line(int today, bool window, bool winter, bool drew)
        {
            var sb = new StringBuilder(512);
            sb.Append("Rok urodzaju (R3-05e): dzien ").Append(today).Append(" | ").Append(drew ? "LOSOWANIE roku " + _drawYear : "rok losowany doba " + (_drawDay >= 0 ? _drawDay.ToString(Inv) : "-"))
              .Append(" | czynny ").Append(_active ? "TAK" : "NIE").Append(winter ? " (zima Westeros - plon tnie zima, rok x1)" : "")
              .Append(" | okno zniw ").Append(window ? "TAK" : "NIE");
            var sn = _snap;
            if (_nodes != null && sn != null)
            {
                int famN = 0; foreach (var v in _nodes) if (v <= Famine + 1e-4f) famN++;
                sb.Append(" | siatka ").Append(_nx).Append('x').Append(_ny).Append(" co ").Append(_cell.ToString("0", Inv)).Append(" jedn., wezly glodu ").Append(famN);
                int nv = 0, good = 0, bad = 0, fam = 0; double sum = 0; float mn = 9f, mx = 0f;
                var byK = new Dictionary<string, double[]>(StringComparer.Ordinal);
                foreach (var kv in sn.F)
                {
                    if (kv.Key == null || !kv.Key.IsVillage) continue;
                    float f = kv.Value; nv++; sum += f; if (f < mn) mn = f; if (f > mx) mx = f;
                    if (f >= 1.1f) good++; else if (f <= 0.75f) fam++; else if (f <= 0.9f) bad++;
                    string k = "?"; try { var mf = kv.Key.MapFaction; if (mf != null && mf.Name != null) k = mf.Name.ToString(); } catch { }
                    double[] a; if (!byK.TryGetValue(k, out a)) byK[k] = a = new double[2];
                    a[0] += f; a[1]++;
                }
                sb.Append(" | wsie ").Append(nv).Append(": srednio x").Append(nv > 0 ? (sum / nv).ToString("0.000", Inv) : "-").Append(", min x").Append(mn.ToString("0.00", Inv))
                  .Append(", max x").Append(mx.ToString("0.00", Inv)).Append(", urodzaj (>= 1.10) ").Append(good).Append(", zly rok (0.76-0.90) ").Append(bad).Append(", glod (<= 0.75) ").Append(fam);
                var list = new List<KeyValuePair<string, double>>();
                foreach (var kv in byK) if (kv.Value[1] > 0) list.Add(new KeyValuePair<string, double>(kv.Key, kv.Value[0] / kv.Value[1]));
                list.Sort((p, q) => p.Value.CompareTo(q.Value));
                sb.Append(" | krolestwa (srednia wsi): ");
                for (int i = 0; i < list.Count; i++) { if (i > 0) sb.Append(", "); sb.Append(list[i].Key).Append(" x").Append(list[i].Value.ToString("0.00", Inv)); }
            }
            sb.Append(" | plony: zboze, len, winogrona, oliwki, daktyle, bawelna, papirus, jablka, pomarancze, marchew + zywnosc wsi (bez wsi morskich) | potkniecia ").Append(_stumbles).Append('.');
            return sb.ToString();
        }

        /// <summary>Wiesc dla gracza przy losowaniu: najlepszy i najgorszy kraj (srednia wsi), glod - osobno.</summary>
        private static void Tell()
        {
            try
            {
                var sn = _snap; if (sn == null) return;
                var byK = new Dictionary<string, double[]>(StringComparer.Ordinal);
                foreach (var kv in sn.F)
                {
                    if (kv.Key == null || !kv.Key.IsVillage) continue;
                    string k = null; try { var mf = kv.Key.MapFaction; if (mf != null && mf.Name != null) k = mf.Name.ToString(); } catch { }
                    if (string.IsNullOrEmpty(k)) continue;
                    double[] a; if (!byK.TryGetValue(k, out a)) byK[k] = a = new double[3];
                    a[0] += kv.Value; a[1]++; if (kv.Value <= 0.75f) a[2]++;
                }
                string best = null, worst = null; double bv = 0, wv = 9; var famine = new List<string>();
                foreach (var kv in byK)
                {
                    if (kv.Value[1] < 3) continue;
                    double m = kv.Value[0] / kv.Value[1];
                    if (m > bv) { bv = m; best = kv.Key; }
                    if (m < wv) { wv = m; worst = kv.Key; }
                    if (kv.Value[2] >= Math.Max(2, kv.Value[1] / 4)) famine.Add(kv.Key);
                }
                if (best != null && worst != null && best != worst)
                    Log.Player("Harvest is in: a rich year in " + best + ", a poor one in " + worst + ".", false);
                if (famine.Count > 0)
                    Log.Player("The harvest failed in " + string.Join(", ", famine.ToArray()) + " - famine will follow.", true);
            }
            catch (Exception e) { Stumble("Tell", e); }
        }

        // ------------------------------------------------------------ latka i zapis
        /// <summary>SubModuleMain (obok WinterBite.ApplyAll): postfiks zywnosci wsi na klasie deklarujacej. Klucz czytany na zywo (FoodPostfix).</summary>
        internal static void ApplyAll(Harmony h)
        {
            try
            {
                var m = AccessTools.DeclaredMethod(typeof(DefaultVillageProductionCalculatorModel), "CalculateDailyFoodProductionAmount", new[] { typeof(Village) });
                if (m != null) h.Patch(m, postfix: new HarmonyMethod(typeof(HarvestYear), nameof(FoodPostfix)));
                Log.Info("Rok urodzaju (R3-05e): zywnosc wsi (CalculateDailyFoodProductionAmount) " + (m != null ? "wpieta" : "BRAK") + "; plony pol w lancuchu MaterialLaw i w KS; klucz Harvest Year Enabled (na zywo).");
            }
            catch (Exception e) { Log.Error("HarvestYear.ApplyAll", e); }
        }

        /// <summary>Zapis: "1|dobaLosowania|rok|minX|minY|krok|nx|ny|w1,w2,...". Pusty - brak losowania.</summary>
        internal static string Export()
        {
            try
            {
                ResetIfNewCampaign();
                var nodes = _nodes;
                if (nodes == null || _drawDay < 0) return "";
                var sb = new StringBuilder(64 + nodes.Length * 6);
                sb.Append("1|").Append(_drawDay).Append('|').Append(_drawYear).Append('|').Append(_minX.ToString("R", Inv)).Append('|').Append(_minY.ToString("R", Inv))
                  .Append('|').Append(_cell.ToString("R", Inv)).Append('|').Append(_nx).Append('|').Append(_ny).Append('|');
                for (int i = 0; i < nodes.Length; i++) { if (i > 0) sb.Append(','); sb.Append(nodes[i].ToString("0.####", Inv)); }
                return sb.ToString();
            }
            catch (Exception e) { Stumble("Export", e); return ""; }
        }

        internal static void Import(string data)
        {
            try
            {
                _camp = Campaign.Current;
                _drawDay = -1; _drawYear = -1; _nodes = null; _snap = null; _active = false; _firstLine = false; _lastLogDay = -1;
                if (string.IsNullOrEmpty(data)) return;
                var f = data.Split('|');
                if (f.Length < 9 || f[0] != "1") return;
                int nx = int.Parse(f[6], Inv), ny = int.Parse(f[7], Inv);
                var vals = f[8].Split(',');
                if (nx < 2 || ny < 2 || vals.Length != nx * ny) return;
                var nodes = new float[nx * ny];
                for (int i = 0; i < nodes.Length; i++) nodes[i] = MBMath.ClampFloat(float.Parse(vals[i], Inv), Famine, 1f + Spread);
                lock (Gate)
                {
                    _drawDay = int.Parse(f[1], Inv); _drawYear = int.Parse(f[2], Inv);
                    _minX = float.Parse(f[3], Inv); _minY = float.Parse(f[4], Inv); _cell = float.Parse(f[5], Inv);
                    _nx = nx; _ny = ny; _nodes = nodes; _snap = null;
                }
                // czynnosc ustala pierwsza Daily po wczytaniu (w SyncData bez odczytow czasu i klimatu - zegar i pory moga nie byc jeszcze wczytane);
                // do niej plony x1 (najwyzej czesc jednej doby ticku osad)
            }
            catch (Exception e) { _nodes = null; _active = false; Stumble("Import", e); }
        }
    }
}
