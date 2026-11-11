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
    /// kazdy wezel losuje swoj rok - zwykly x(c + 0.2 x (u1 + u2 - 1)) (rozklad trojkatny c-0.2..c+0.2), z szansa 1/20 rok glodu x0.6 (1315-17:
    /// plon 2:1 wobec do 7:1 w dobre lata; szansa - szacunek audytu "ok. raz na 20 lat"). Srodek zwyklego roku c = (1 - 0.05 x 0.6) / 0.95 = 1.021 - srednia
    /// swiata dokladnie 1 (start neutralny; recenzja: z c = 1 swiat tracil 2% plonu). Okreg (warownia wsi - Village.Bound) bierze wartosc pola w polozeniu
    /// swojej warowni: zwykly rok - interpolacja dwuliniowa z wygladzeniem (caly okreg ten sam rok, sasiednie okregi podobny, dalekie niezalezne);
    /// ROK GLODU - CALY REGION najblizszego wezla (kwadrat o boku kroku siatki wokol wezla) x0.6, bez rozmycia (recenzja: rozmyty glod trafial okreg
    /// raz na ok. 50 lat i prawie nigdy w pelni) - kazde miejsce ma rok glodu z szansa 1/20. W interpolacji zwyklego roku wezel glodu liczy sie jako c.
    /// Pola mieszczan (KS) - wartosc pola w polozeniu miasta.
    ///
    /// GDZIE MNOZY (jeden czynnik, wszystkie drogi plonu pol):
    ///  - lancuch produkcji wsi gry (MaterialLaw.ProdPostfix, po rekach E4/5 i E4/7, tym samym wzorem "czynnik x suma") i jego odczyt ChainFactor - tylko
    ///    plony pol i sadow: zboze, len, winogrona, oliwki, daktyle, bawelna, papirus, jablka, pomarancze, marchew (zywe zwierzeta, kopaliny, ryby wsi, las -
    ///    bez zmian). Swiadomie: ryby przysiolkow W2 (HamletKinds.Step przerabia u x zboze PO tym kroku na ryby) ida za rokiem - to praca z ziemi przysiolka;
    ///  - zywnosc wsi gry (postfiks DefaultVillageProductionCalculatorModel.CalculateDailyFoodProductionAmount - mnozy sie z zima, rekami E4/5 i E4/7) -
    ///    to plon spichlerza okregu (Famine) i koszyk zywnosci wsi gry (TickFoodProduction: zboze, ser, maslo) - swiadomie ser i maslo tez: zly rok = gorsze
    ///    pastwiska i siano, mniej mleka; bez wsi rybackich, wielorybniczych i lowcow morsow (ich zywnosc to morze);
    ///  - rachunek KS wsi i pol mieszczan po wlaczeniu kalibracji (StartCalibration.ComputeDistrict z czynnikami gry) - te same towary.
    /// ZIMA: w zimie Westeros (WesterosClimate.Now) czynnik = 1 - plon tnie zima (WinterBite), dwie reguly na jedno zjawisko nie nakladaja sie
    /// (ryzyko audytu: zly rok na wieloletniej zimie). Rok wazny 364 doby od losowania (rok gry); potem 1 do nastepnego okna.
    /// Start neutralny: do pierwszego losowania (nowa kampania albo pierwsze wczytanie z kluczem) wszedzie x1.
    /// Zapis: arm_harvest_year (SaveText.Sync) - doba i rok losowania, siatka i wartosci wezlow; brak klucza = stary zapis (x1 do nastepnego okna).
    /// Watki: odczyt z watkow AI (model wsi w PartiesThink) - jedna referencja migawki (slownik osada -> czynnik), budowana pod blokada, po publikacji
    /// tylko czytana; flaga czynnosci doby ustawiana na watku glownym. Wyjatek na obiekcie = potkniecie (licznik), czynnik 1, bez wylacznikow.
    /// Linia logu "Rok urodzaju (R3-05e):" przy losowaniu, przy zmianie stanu i co 28 dob.
    ///
    /// 432 (audyt ekonomii 11.11 C3, decyzja Jeffa 5 "tak modyfikuj zime"; klucz Winter Year Enabled, domyslnie TAK; NIE = wszystko powyzej jak dotad) - LEPSZE
    /// I GORSZE LATA ZIMY. TEN SAM mechanizm (to samo pole, te same trzy drogi plonu, ten sam zapis) - nie druga regula: w zimie Westeros okna zniw nie ma, wiec
    /// rok losuje sie w dobie, w ktorej zniwa by sie zaczely (dzien 196 roku 364; pierwszy raz od razu, gdy zima trwa i ten rok rolny nie ma losowania).
    /// TRYB ZIMY pola: wezel = 1 +- Winter Year Spread Percent (15%, rozklad rowny; interpolacja czterech wezlow sciaga wsie do srodka), BEZ roku glodu x0.6,
    /// po losowaniu wezly dzielone przez srednia wsi - srednia swiata = 1 (plan zimy i prog racjonowania bez zmian: swiat ma tyle plonu co dotad, inaczej
    /// rozlozone). Rok trybu zimy mnozy plon TAKZE w zimie (na cieciu WinterBite - to nie druga regula zimy, tylko rok: zima tnie wszystkim, rok rozni krainy);
    /// rok ze zniw (tryb zniw, z x0.6) w zimie dalej = 1, jak dotad. BEZPIECZNIK (audyt: zapas zimy ma zerowy margines ponad racjonowanie): ujemna czesc roku
    /// wsi gasnie liniowo, gdy spichlerz okregu (Famine.DaysOf) schodzi z 2T do T dni (T = Winter Year Relief Days, 45; 2T = prog przednowka 90) - zly rok
    /// wpycha wies w przednowek (racje 90%, drozsze zboze), ale sam nie oproznia spichlerza; HAMULEC SWIATA: gdy wiecej niz Winter Year World Brake Percent
    /// (10%) wsi jest na przednowku, ujemna czesc gasnie wszedzie (wraca ponizej polowy progu). Dobry rok bez zmian. Bezpiecznik liczy watek glowny raz na dobe
    /// (druga migawka _eff - watki AI tylko czytaja). Zapis: dziesiate pole "w" / "s" (starsza DLL czyta 9 pol - rok trybu zimy widzi jako zwykly: w zimie x1).
    /// Linia doby "Lata zimy (432):" z kontrola pola (srednia wsi - 1) i pomiarem (wsie na przednowku wedlug roku, cena zboza w miastach wedlug roku).
    /// </summary>
    internal static class HarvestYear
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static readonly TextObject _txt = new TextObject("{=!}Harvest year");
        private const float Famine = 0.6f, Spread = 0.2f, FamineChance = 1f / 20f;
        private const float Center = (1f - FamineChance * Famine) / (1f - FamineChance);   // srodek zwyklego roku 1.021 - srednia swiata 1
        private const float Max = 1.25f;                     // gorna granica pola (zwykly rok najwyzej c + 0.2 = 1.221)
        private const int Cells = 8;                         // wezly co 1/8 dluzszego boku mapy
        private const float WinterMin = 0.75f;               // 432: dolna granica wezla trybu zimy (ponad Famine - rok zimy nigdy nie jest rokiem glodu)
        private const float WGood = 1.05f, WBad = 0.95f;     // 432: progi "dobry rok" / "zly rok" w liniach i wiesci

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
        private static bool _winterMode;                     // 432: wezly losowane w trybie zimy (zapis: pole 10 "w")

        // migawka dla watkow AI
        private sealed class Snap { public Dictionary<Settlement, float> F; }
        private static volatile Snap _snap;
        private static volatile bool _active;
        private static volatile Snap _eff;                   // 432: migawka po bezpieczniku (tryb zimy; budowana raz na dobe na watku glownym), null = czytaj _snap
        private static bool _brake;                          // 432: hamulec swiata (w pamieci; pierwsza doba po wczytaniu liczy go od nowa)
        private sealed class WStat
        {
            public int Nv, Good, Bad, Mid, Part, Full, WithBarn, Rationed, RatBad, RatGood, RatMid, DnBad, DnGood, DnMid, BrakePct;
            public double SumRaw, SumEff, DaysBad, DaysGood, DaysMid, SharePct, T, Ration;
            public float Min = 9f, Max;
        }
        private static WStat _ws;
        private static readonly object Gate = new object();

        private static int _stumbles, _lastLogDay = -1;
        private static bool _lastActive, _firstLine;
        private static readonly HashSet<string> _errOnce = new HashSet<string>();

        internal static bool KeyOn { get { var s = Settings.Current; return s != null && s.HarvestYearEnabled; } }
        /// <summary>432: lata zimy - tylko z Harvest Year Enabled (ten sam mechanizm).</summary>
        internal static bool WinterKeyOn { get { var s = Settings.Current; return s != null && s.HarvestYearEnabled && s.WinterYearEnabled; } }
        private static float WinterSpread { get { var s = Settings.Current; return s == null ? 0f : MBMath.ClampFloat(s.WinterYearSpreadPercent, 0f, 25f) / 100f; } }

        private static void Stumble(string where, Exception e)
        {
            System.Threading.Interlocked.Increment(ref _stumbles);
            try { lock (_errOnce) { if (!_errOnce.Add(where)) return; } Log.Error("HarvestYear." + where, e); } catch { }
        }

        /// <summary>Konstruktor ArmouryBehavior (nowa kampania, przed wczytaniem): stan od zera.</summary>
        internal static void Reset()
        {
            _camp = null;
            ResetIfNewCampaign();
        }

        private static void ResetIfNewCampaign()
        {
            var cur = Campaign.Current;
            if (_camp != null && ReferenceEquals(cur, _camp)) return;
            _camp = cur;
            _drawDay = -1; _drawYear = -1; _nodes = null; _nx = _ny = 0; _cell = 0f;
            _snap = null; _active = false; _lastLogDay = -1; _lastActive = false; _firstLine = false; _stumbles = 0;
            _winterMode = false; _eff = null; _brake = false; _ws = null;
            lock (_errOnce) _errOnce.Clear();
        }

        private static int YearDays { get { return Math.Max(28, CampaignTime.DaysInYear); } }

        // ------------------------------------------------------------ odczyt (watki AI)
        private static bool IsCrop(ItemObject item) { var id = item != null ? item.StringId : null; return id != null && Crops.Contains(id); }

        private static float Of(Settlement st)
        {
            if (st == null) return 1f;
            var ef = _eff;                                   // 432: tryb zimy - czynnik po bezpieczniku spichlerza
            if (ef != null) { float e; return ef.F.TryGetValue(st, out e) ? e : 1f; }
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

        private static bool IsFamine(float v) { return v <= Famine + 1e-4f; }

        /// <summary>Wezel w interpolacji zwyklego roku: wezel glodu liczy sie jako srodek zwyklego roku (glod nie rozlewa sie na sasiadow).</summary>
        private static float Normal(float v) { return IsFamine(v) ? Center : v; }

        private static float FieldAt(float x, float y)
        {
            var nodes = _nodes;
            if (nodes == null || _cell <= 0f || _nx < 2 || _ny < 2) return 1f;
            float gx = (x - _minX) / _cell, gy = (y - _minY) / _cell;
            // rok glodu: caly region najblizszego wezla
            int nx0 = Math.Max(0, Math.Min(_nx - 1, (int)Math.Round(gx))), ny0 = Math.Max(0, Math.Min(_ny - 1, (int)Math.Round(gy)));
            if (IsFamine(nodes[ny0 * _nx + nx0])) return Famine;
            int ix = (int)Math.Floor(gx), iy = (int)Math.Floor(gy);
            ix = Math.Max(0, Math.Min(_nx - 2, ix)); iy = Math.Max(0, Math.Min(_ny - 2, iy));
            float tx = Smooth(gx - ix), ty = Smooth(gy - iy);
            float a = Normal(nodes[iy * _nx + ix]), b = Normal(nodes[iy * _nx + ix + 1]), c = Normal(nodes[(iy + 1) * _nx + ix]), d = Normal(nodes[(iy + 1) * _nx + ix + 1]);
            float v = (a * (1f - tx) + b * tx) * (1f - ty) + (c * (1f - tx) + d * tx) * ty;
            return MBMath.ClampFloat(v, Famine, Max);
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

        private static void Draw(int today, int year, bool winterMode)
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
            var rnd = new Random((int)(Fnv(camp + (winterMode ? "|zima432|" : "|urodzaj|") + year) & 0x7FFFFFFF));
            var nodes = new float[nx * ny];
            float ws = WinterSpread;
            for (int i = 0; i < nodes.Length; i++)
            {
                if (winterMode) nodes[i] = 1f + ws * (float)(2.0 * rnd.NextDouble() - 1.0);   // 432: maly rozrzut, bez roku glodu
                else if (rnd.NextDouble() < FamineChance) nodes[i] = Famine;
                else nodes[i] = Center + Spread * (float)(rnd.NextDouble() + rnd.NextDouble() - 1.0);
            }
            // pod jedna blokada: pole, normalizacja trybu zimy i dopiero potem migawka (EnsureSnap z watku AI czeka na Gate - nie zobaczy pola przed normalizacja)
            lock (Gate)
            {
                _minX = minX; _minY = minY; _cell = cell; _nx = nx; _ny = ny; _nodes = nodes;
                _drawDay = today; _drawYear = year; _winterMode = winterMode; _snap = null; _eff = null;
                if (winterMode) NormalizeLocked();
            }
            EnsureSnap();
        }

        /// <summary>432 (pod Gate): wezly trybu zimy / srednia pola po wsiach - srednia swiata = 1 (interpolacja jest liniowa w wezlach; zaciski WinterMin..Max
        /// moga zostawic odchylke rzedu 0.001 - pokazuje ja kontrola pola w linii doby). Bez wsi albo ze zla srednia - bez zmian.</summary>
        private static void NormalizeLocked()
        {
            double sum = 0; int n = 0;
            foreach (var st in Settlement.All)
            {
                try
                {
                    if (st == null || !st.IsVillage || st.Village == null) continue;
                    var p = (st.Village.Bound ?? st).GetPosition2D;
                    sum += FieldAt(p.X, p.Y); n++;
                }
                catch (Exception e) { Stumble("Normalize", e); }
            }
            if (n == 0) return;
            double mean = sum / n;
            if (!(mean > 0.5) || double.IsInfinity(mean)) return;
            var src = _nodes; var nn = new float[src.Length];
            for (int i = 0; i < nn.Length; i++) nn[i] = MBMath.ClampFloat((float)(src[i] / mean), WinterMin, Max);
            _nodes = nn;
        }

        /// <summary>432 (watek glowny, raz na dobe, tryb zimy czynny): migawka po bezpieczniku. Ujemna czesc roku wsi x fade, fade = (dni spichlerza - T) / T w 0..1
        /// (T = Winter Year Relief Days; bez spichlerza / Famine wylaczone - bez ulgi); hamulec swiata (wsie na przednowku ponad prog) - ujemna czesc 0 wszedzie.
        /// Dobry rok bez zmian. Pola mieszczan (miasta) - tylko hamulec swiata. Liczniki do linii doby.</summary>
        private static void BuildEff()
        {
            var sn = _snap; var s = Settings.Current;
            if (sn == null || s == null) { _eff = null; _ws = null; return; }
            var w = new WStat { T = Math.Max(0, s.WinterYearReliefDays), Ration = Math.Max(0, s.FamineRationDays), BrakePct = Math.Max(0, Math.Min(100, s.WinterYearWorldBrakePercent)) };
            var days = new Dictionary<Settlement, double>();
            foreach (var kv in sn.F)
            {
                try
                {
                    var st = kv.Key; double d;
                    if (st == null || !st.IsVillage || st.Village == null || !Armoury.Famine.DaysOf(st.Village, out d)) continue;
                    days[st] = d; w.WithBarn++; if (d < w.Ration) w.Rationed++;
                }
                catch (Exception e) { Stumble("BuildEff.days", e); }
            }
            w.SharePct = w.WithBarn > 0 ? 100.0 * w.Rationed / w.WithBarn : 0.0;
            if (w.BrakePct <= 0) _brake = false;
            else if (w.SharePct > w.BrakePct) _brake = true;
            else if (w.SharePct < 0.5 * w.BrakePct) _brake = false;
            var map = new Dictionary<Settlement, float>(sn.F.Count);
            foreach (var kv in sn.F)
            {
                float f = kv.Value, e = f;
                try
                {
                    var st = kv.Key; bool vil = st != null && st.IsVillage;
                    double d = 0; bool has = vil && days.TryGetValue(st, out d);
                    if (f < 1f)
                    {
                        if (_brake) e = 1f;
                        else if (has && w.T > 0)
                        {
                            double fade = (d - w.T) / w.T; if (fade < 0) fade = 0; if (fade > 1) fade = 1;
                            e = 1f - (1f - f) * (float)fade;
                            if (vil) { if (fade <= 0) w.Full++; else if (fade < 1) w.Part++; }
                        }
                    }
                    if (vil)
                    {
                        w.Nv++; w.SumRaw += f; w.SumEff += e; if (f < w.Min) w.Min = f; if (f > w.Max) w.Max = f;
                        bool rat = has && d < w.Ration;
                        if (f >= WGood) { w.Good++; if (has) { w.DnGood++; w.DaysGood += d; } if (rat) w.RatGood++; }
                        else if (f <= WBad) { w.Bad++; if (has) { w.DnBad++; w.DaysBad += d; } if (rat) w.RatBad++; }
                        else { w.Mid++; if (has) { w.DnMid++; w.DaysMid += d; } if (rat) w.RatMid++; }
                    }
                }
                catch (Exception ex) { Stumble("BuildEff", ex); e = f; }
                if (kv.Key != null) map[kv.Key] = e;
            }
            _ws = w;
            _eff = new Snap { F = map };
        }

        private static string Avg(double sum, int n, string fmt) { return n > 0 ? (sum / n).ToString(fmt, Inv) : "-"; }

        /// <summary>432: linia doby trybu zimy - pole, KONTROLA (srednia wsi - 1; wsie wedlug roku - razem), bezpiecznik, przednowek i cena zboza wedlug roku.</summary>
        private static string WinterLine(int today, bool drew)
        {
            var sb = new StringBuilder(768);
            var w = _ws; var sn = _snap;
            sb.Append("Lata zimy (432): dzien ").Append(today).Append(" | rok rolny ").Append(_drawYear).Append(drew ? " LOSOWANIE dzis" : " losowany doba " + _drawDay.ToString(Inv))
              .Append(" | rozrzut wezla +-").Append((100f * WinterSpread).ToString("0", Inv)).Append("% (bez roku glodu)");
            if (w == null || sn == null) { sb.Append(" | brak migawki | potkniecia ").Append(_stumbles).Append('.'); return sb.ToString(); }
            double mean = w.Nv > 0 ? w.SumRaw / w.Nv : 1.0;
            sb.Append(" | wsie ").Append(w.Nv).Append(": rok srednio x").Append(mean.ToString("0.0000", Inv)).Append(", min x").Append(w.Min.ToString("0.000", Inv)).Append(", max x").Append(w.Max.ToString("0.000", Inv))
              .Append("; dobry rok (>= ").Append(WGood.ToString("0.00", Inv)).Append(") ").Append(w.Good).Append(", zly rok (<= ").Append(WBad.ToString("0.00", Inv)).Append(") ").Append(w.Bad).Append(", zwykly ").Append(w.Mid)
              .Append(" | KONTROLA pola: srednia wsi - 1 = ").Append((mean - 1.0).ToString("+0.0000;-0.0000", Inv)).Append(Math.Abs(mean - 1.0) <= 0.005 ? " (OK)" : " (ROZJAZD)")
              .Append(", wsie wedlug roku - razem = ").Append(w.Good + w.Bad + w.Mid - w.Nv)
              .Append(" | bezpiecznik: po nim srednio x").Append(Avg(w.SumEff, w.Nv, "0.0000")).Append(", ulga czesciowa ").Append(w.Part).Append(" wsi, pelna ").Append(w.Full)
              .Append(" wsi (spichlerz ").Append(w.T.ToString("0", Inv)).Append("..").Append((2 * w.T).ToString("0", Inv)).Append(" dni), hamulec swiata ").Append(_brake ? "TAK" : "NIE")
              .Append(" (na przednowku ").Append(w.Rationed).Append(" z ").Append(w.WithBarn).Append(" wsi = ").Append(w.SharePct.ToString("0.0", Inv)).Append("% wobec progu ").Append(w.BrakePct).Append("%)")
              .Append(" | wsie na przednowku (< ").Append(w.Ration.ToString("0", Inv)).Append(" dni): zly rok ").Append(w.RatBad).Append(" z ").Append(w.Bad).Append(", zwykly ").Append(w.RatMid).Append(" z ").Append(w.Mid)
              .Append(", dobry ").Append(w.RatGood).Append(" z ").Append(w.Good)
              .Append(" | spichlerz srednio dni: zly rok ").Append(Avg(w.DaysBad, w.DnBad, "0")).Append(", zwykly ").Append(Avg(w.DaysMid, w.DnMid, "0")).Append(", dobry ").Append(Avg(w.DaysGood, w.DnGood, "0"));
            // indeks zboza: cena zboza w miastach wedlug roku ich okolicy (czynnik pola w polozeniu miasta)
            double pb = 0, pm = 0, pg = 0; int nb = 0, nm = 0, ng = 0;
            var grain = DefaultItems.Grain;
            if (grain != null)
                foreach (var kv in sn.F)
                {
                    try
                    {
                        var st = kv.Key;
                        if (st == null || !st.IsTown || st.Town == null) continue;
                        double pr = st.Town.GetItemPrice(grain, null, false);
                        if (!(pr > 0)) continue;
                        if (kv.Value >= WGood) { pg += pr; ng++; } else if (kv.Value <= WBad) { pb += pr; nb++; } else { pm += pr; nm++; }
                    }
                    catch (Exception e) { Stumble("WinterLine.price", e); }
                }
            int na = nb + nm + ng; double pa = pb + pm + pg;
            sb.Append(" | cena zboza w miastach: zly rok ").Append(Avg(pb, nb, "0.0")).Append(" (").Append(nb).Append("), zwykly ").Append(Avg(pm, nm, "0.0")).Append(" (").Append(nm).Append("), dobry ")
              .Append(Avg(pg, ng, "0.0")).Append(" (").Append(ng).Append("), swiat ").Append(Avg(pa, na, "0.0")).Append("; indeks zly rok / swiat ")
              .Append(nb > 0 && na > 0 && pa > 0 ? ((pb / nb) / (pa / na)).ToString("0.00", Inv) : "-")
              .Append(" | potkniecia ").Append(_stumbles).Append('.');
            return sb.ToString();
        }

        /// <summary>432: wiesc dla gracza przy losowaniu roku zimy - rok w JEGO ziemiach (wsie jego rodu; bez nich - wsie jego krolestwa), potem najlepsze i najgorsze krolestwo.</summary>
        private static void TellWinter()
        {
            try
            {
                var sn = _snap; if (sn == null) return;
                Clan pc = null; Kingdom pk = null;
                try { pc = Clan.PlayerClan; pk = pc != null ? pc.Kingdom : null; } catch { }
                double own = 0, kg = 0; int ownN = 0, kgN = 0;
                var byK = new Dictionary<string, double[]>(StringComparer.Ordinal);
                foreach (var kv in sn.F)
                {
                    try
                    {
                        var st = kv.Key;
                        if (st == null || !st.IsVillage) continue;
                        if (pc != null && st.OwnerClan == pc) { own += kv.Value; ownN++; }
                        var mf = st.MapFaction;
                        if (pk != null && mf == pk) { kg += kv.Value; kgN++; }
                        string k = mf != null && mf.Name != null ? mf.Name.ToString() : null;
                        if (string.IsNullOrEmpty(k)) continue;
                        double[] a; if (!byK.TryGetValue(k, out a)) byK[k] = a = new double[2];
                        a[0] += kv.Value; a[1]++;
                    }
                    catch (Exception e) { Stumble("TellWinter.village", e); }
                }
                string where = ownN > 0 ? "your lands" : (kgN > 0 ? "your realm" : null);
                double m = ownN > 0 ? own / ownN : (kgN > 0 ? kg / kgN : 1.0);
                if (where != null)
                {
                    int pct = (int)Math.Round(Math.Abs(m - 1.0) * 100.0);
                    if (m >= 1.03) Log.Player("Word from the villages: a good year in " + where + " - fields and stores yield about " + pct + "% more than usual.", false);
                    else if (m <= 0.97) Log.Player("Word from the villages: a lean year in " + where + " - fields and stores yield about " + pct + "% less than usual; grain will be short and dear.", true);
                    else Log.Player("Word from the villages: an ordinary year in " + where + ".", false);
                }
                string best = null, worst = null; double bv = 0, wv = 9;
                foreach (var kv in byK)
                {
                    if (kv.Value[1] < 3) continue;
                    double a = kv.Value[0] / kv.Value[1];
                    if (a > bv) { bv = a; best = kv.Key; }
                    if (a < wv) { wv = a; worst = kv.Key; }
                }
                if (best != null && worst != null && best != worst && bv - wv >= 0.02)
                    Log.Player("Across the realms this year is kindest to " + best + " and hardest on " + worst + ".", false);
                Log.Info("Lata zimy (432): wiesc - " + (where == null ? "gracz bez wsi i krolestwa (bez wiesci o jego ziemiach)" : where + " x" + m.ToString("0.000", Inv) + " (" + (ownN > 0 ? ownN : kgN) + " wsi)")
                    + "; najlepszy rok " + (best ?? "-") + " x" + (best != null ? bv.ToString("0.000", Inv) : "-") + ", najgorszy " + (worst ?? "-") + " x" + (worst != null ? wv.ToString("0.000", Inv) : "-") + ".");
            }
            catch (Exception e) { Stumble("TellWinter", e); }
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
                    _lastActive = false; _eff = null; _ws = null;
                    return;
                }
                int yd = YearDays, year = today / yd;
                bool window = FreeHands.HarvestWindow();
                bool drew = false;
                bool winter = WesterosClimate.Now() == CampaignTime.Seasons.Winter;
                bool wKey = WinterKeyOn;
                if (window && year != _drawYear) { Draw(today, year, false); drew = _drawYear == year; }
                else if (wKey && winter)
                {
                    // 432: w zimie okna zniw nie ma - rok rolny liczy sie od doby, w ktorej zniwa by sie zaczely (numer = rok kalendarza tej doby, jak w trybie zniw);
                    // rok rolny bez losowania (takze pierwsza doba z kluczem) - losowanie trybu zimy od razu
                    int from = (int)Math.Round(FreeHands.HarvestFrom * (double)yd / 364.0);
                    int agr = (int)Math.Floor((today - from) / (double)yd);
                    if (agr != _drawYear) { Draw(today, agr, true); drew = _drawYear == agr && _drawDay == today; }
                }
                // rok trybu zniw: poza zima (jak dotad); rok trybu zimy (432): przez caly rok, ale tylko z kluczem (klucz wylaczony = takiego roku nie ma, jak dotad)
                bool act = _nodes != null && _drawDay >= 0 && today - _drawDay < yd && (_winterMode ? wKey : !winter);
                if (act && _snap == null) EnsureSnap();   // recenzja: po wczytaniu migawke buduje watek glowny (nie pierwszy odczyt z watku AI)
                if (act && _winterMode) BuildEff(); else { _eff = null; _ws = null; }   // 432: bezpiecznik spichlerza - przed wlaczeniem czynnika
                _active = act;
                if (drew || _active != _lastActive || !_firstLine || today - _lastLogDay >= 28)
                {
                    _lastLogDay = today; _firstLine = true;
                    Log.Info(Line(today, window, winter, drew));
                    if (drew) { if (_winterMode) TellWinter(); else Tell(); }
                }
                if (_active && _winterMode) Log.Info(WinterLine(today, drew));
                _lastActive = _active;
            }
            catch (Exception e) { Stumble("Daily", e); }
        }

        private static string Line(int today, bool window, bool winter, bool drew)
        {
            var sb = new StringBuilder(512);
            sb.Append("Rok urodzaju (R3-05e): dzien ").Append(today).Append(" | ").Append(drew ? "LOSOWANIE roku " + _drawYear : "rok losowany doba " + (_drawDay >= 0 ? _drawDay.ToString(Inv) : "-"))
              .Append(" | tryb ").Append(_nodes == null ? "-" : (_winterMode ? "ZIMY (432)" : "zniw")).Append(", lata zimy (432) ").Append(WinterKeyOn ? "TAK" : "NIE")
              .Append(" | czynny ").Append(_active ? "TAK" : "NIE").Append(winter && !_active ? " (zima Westeros - plon tnie zima, rok x1)" : "")
              .Append(" | okno zniw ").Append(window ? "TAK" : "NIE");
            var sn = _snap;
            if (_nodes != null && sn != null)
            {
                int famN = 0; foreach (var v in _nodes) if (IsFamine(v)) famN++;
                sb.Append(" | siatka ").Append(_nx).Append('x').Append(_ny).Append(" co ").Append(_cell.ToString("0", Inv)).Append(" jedn., wezly glodu ").Append(famN);
                int nv = 0, good = 0, bad = 0, fam = 0; double sum = 0; float mn = 9f, mx = 0f;
                var byK = new Dictionary<string, double[]>(StringComparer.Ordinal);
                foreach (var kv in sn.F)
                {
                    if (kv.Key == null || !kv.Key.IsVillage) continue;
                    float f = kv.Value; nv++; sum += f; if (f < mn) mn = f; if (f > mx) mx = f;
                    if (f >= 1.1f) good++; else if (IsFamine(f)) fam++; else if (f <= 0.9f) bad++;
                    string k = "?"; try { var mf = kv.Key.MapFaction; if (mf != null && mf.Name != null) k = mf.Name.ToString(); } catch { }
                    double[] a; if (!byK.TryGetValue(k, out a)) byK[k] = a = new double[2];
                    a[0] += f; a[1]++;
                }
                sb.Append(" | wsie ").Append(nv).Append(": srednio x").Append(nv > 0 ? (sum / nv).ToString("0.000", Inv) : "-").Append(", min x").Append(mn.ToString("0.00", Inv))
                  .Append(", max x").Append(mx.ToString("0.00", Inv)).Append(", urodzaj (>= 1.10) ").Append(good).Append(", zly rok (<= 0.90) ").Append(bad).Append(", nieurodzaj (x0.6) ").Append(fam);
                var list = new List<KeyValuePair<string, double>>();
                foreach (var kv in byK) if (kv.Value[1] > 0) list.Add(new KeyValuePair<string, double>(kv.Key, kv.Value[0] / kv.Value[1]));
                list.Sort((p, q) => p.Value.CompareTo(q.Value));
                sb.Append(" | krolestwa (srednia wsi): ");
                for (int i = 0; i < list.Count; i++) { if (i > 0) sb.Append(", "); sb.Append(list[i].Key).Append(" x").Append(list[i].Value.ToString("0.00", Inv)); }
            }
            sb.Append(" | plony: zboze, len, winogrona, oliwki, daktyle, bawelna, papirus, jablka, pomarancze, marchew (+ ryby przysiolkow W2 ze zboza) + zywnosc wsi (zboze, ser, maslo; bez wsi morskich) | potkniecia ").Append(_stumbles).Append('.');
            return sb.ToString();
        }

        /// <summary>Wiesc dla gracza przy losowaniu: najlepszy i najgorszy kraj (srednia wsi), glod - osobno.</summary>
        private static void Tell()
        {
            try
            {
                var sn = _snap; if (sn == null) return;
                var byK = new Dictionary<string, double[]>(StringComparer.Ordinal);
                var famine = new List<string>(); var famSeen = new HashSet<Settlement>(); int famVil = 0;
                foreach (var kv in sn.F)
                {
                    if (kv.Key == null || !kv.Key.IsVillage) continue;
                    if (IsFamine(kv.Value))
                    {
                        famVil++;
                        // region nieurodzaju po warowniach okregow (do 5 nazw)
                        try { var b = kv.Key.Village != null ? kv.Key.Village.Bound : null; if (b != null && b.Name != null && famSeen.Add(b) && famine.Count < 5) famine.Add(b.Name.ToString()); } catch { }
                    }
                    string k = null; try { var mf = kv.Key.MapFaction; if (mf != null && mf.Name != null) k = mf.Name.ToString(); } catch { }
                    if (string.IsNullOrEmpty(k)) continue;
                    double[] a; if (!byK.TryGetValue(k, out a)) byK[k] = a = new double[2];
                    a[0] += kv.Value; a[1]++;
                }
                string best = null, worst = null; double bv = 0, wv = 9;
                foreach (var kv in byK)
                {
                    if (kv.Value[1] < 3) continue;
                    double m = kv.Value[0] / kv.Value[1];
                    if (m > bv) { bv = m; best = kv.Key; }
                    if (m < wv) { wv = m; worst = kv.Key; }
                }
                if (best != null && worst != null && best != worst)
                    Log.Player("Harvest is in: a rich year in " + best + ", a poor one in " + worst + ".", false);
                // bez obietnicy glodu: spichlerze okregow (do 2 lat) zwykle przetrwaja jeden zly rok - glod przychodzi, gdy dolozy sie wojna, rabunek albo drugi zly rok
                if (famine.Count > 0)
                    Log.Player("The harvest failed around " + string.Join(", ", famine.ToArray()) + (famSeen.Count > famine.Count ? " and beyond" : "") + " - grain will be short and dear.", true);
                Log.Info("Rok urodzaju (R3-05e): wiesc - urodzaj " + (best ?? "-") + ", zly rok " + (worst ?? "-") + "; nieurodzaj x" + Famine.ToString("0.0", Inv) + " w " + famVil + " wsiach wokol " + famSeen.Count + " warowni.");
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
                sb.Append('|').Append(_winterMode ? 'w' : 's');   // 432: tryb losowania (starsza DLL czyta 9 pol i to pomija)
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
                _winterMode = false; _eff = null; _brake = false; _ws = null;
                if (string.IsNullOrEmpty(data)) return;
                var f = data.Split('|');
                if (f.Length < 9 || f[0] != "1") return;
                int nx = int.Parse(f[6], Inv), ny = int.Parse(f[7], Inv);
                var vals = f[8].Split(',');
                if (nx < 2 || ny < 2 || vals.Length != nx * ny) return;
                var nodes = new float[nx * ny];
                for (int i = 0; i < nodes.Length; i++) nodes[i] = MBMath.ClampFloat(float.Parse(vals[i], Inv), Famine, Max);
                lock (Gate)
                {
                    _drawDay = int.Parse(f[1], Inv); _drawYear = int.Parse(f[2], Inv);
                    _minX = float.Parse(f[3], Inv); _minY = float.Parse(f[4], Inv); _cell = float.Parse(f[5], Inv);
                    _nx = nx; _ny = ny; _nodes = nodes; _snap = null;
                    _winterMode = f.Length > 9 && f[9] == "w";   // 432: brak pola = stary zapis = tryb zniw
                }
                // czynnosc ustala pierwsza Daily po wczytaniu (w SyncData bez odczytow czasu i klimatu - zegar i pory moga nie byc jeszcze wczytane);
                // do niej plony x1 (najwyzej czesc jednej doby ticku osad)
            }
            catch (Exception e) { _nodes = null; _active = false; _winterMode = false; _eff = null; Stumble("Import", e); }
        }
    }
}
