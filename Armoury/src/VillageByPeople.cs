using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// GL-1 / P1 (projekt docs/gospodarka-od-ludzi/PROJEKT-GOSPODARKA-OD-LUDZI-2026-10-13.md rozdz. 3.2, 3.3 wiersze "plon i wydobycie wsi" i "las wsi", 3.5, 6, 8 P1;
    /// decyzja Jeffa 13.10 "produkuja ludzie; wies to SYMBOL wielu wiosek, ktore lacznie produkuja tyle, ilu jest ludzi") - PLON WSI OD LUDZI.
    ///
    /// Regula (klucz VillageOutputByPeople): plon(wies, towar) = baza gry (stawka typu wsi) x u_v x g(typ) x [poprawki jak dzis: zima, rece po spustoszeniu,
    /// rece w wojnie, budowle warowni, regulator swiata, hodowla]. Gra liczy baze jako stawka x (poziom hearth + 1) x 0.5 (progi 200 / 600 hearth - 520 z 569
    /// wsi ma ten sam mnoznik), wiec w modelu wsi dopisujemy czynnik = u_v x g / ((poziom + 1) x 0.5) - zdjecie progu hearth i skala ludzi naraz; wzorem
    /// "czynnik x suma dotychczasowych" (ExplainedNumber sumuje czynniki), w MaterialLaw.ProdPostfix po mnozniku wydobycia i powtorzen BK, przed rekami
    /// spustoszenia; ryby wiosek pobocznych (W2) i sufit dymarki (R1) zostaja PO nim. Model zywnosci wsi (CalculateDailyFoodProductionAmount) - NIE
    /// (spichlerz mnozy go przez ludzi - liczylby ludzi dwa razy; projekt 3.3 "czego nie skalujemy").
    ///  - u_v z uslugi skal GL-0 (PeopleScale.TryVillage: dom + uchodzcy / srednia wies swiata z kalibracji, gora 6.0, dolnej brak);
    ///  - g(typ) = suma((poziom + 1) / 2) / suma(u_v) po wsiach typu pod regula - liczone RAZ, przy kalibracji (pierwsza doba z kluczem i gotowa usluga
    ///    skal): suma swiata kazdego typu wsi w tym dniu = dzisiejszy wzor gry co do sztuki (przed zima, budowlami i regulatorem). Potem g stoi: swiat,
    ///    ktory sie wyludnia, wytwarza mniej (projekt 3.4). W zapisie (arm_villagepeople) razem ze srednia wsia uslugi skal - inna srednia po wczytaniu
    ///    (usluga skalibrowana od nowa) albo zmiana klucza WatchKeepsFlatOutput = g od nowa (zbior wsi pod regula sie zmienil);
    ///  - las wsi (VillageWoodlot.PiecesPerDay - tick i magazyn): stawka x klimat x u_v x g(las), klimat zostaje; g(las) = suma klimatu / suma (klimat x u_v)
    ///    po wsiach bez drwali pod regula - suma swiata lasu w dniu kalibracji bez zmian;
    ///  - poza regula (wzor gry w calosci): wies bez przelicznika ludzi (k = 0), potkniecie uslugi skal, typ wsi spoza kalibracji, wyjatek Strazy
    ///    (P6, klucz WatchKeepsFlatOutput - wsie Daru przy dzisiejszej stalej produkcji; PeopleScale.TryVillage zwraca wtedy false).
    /// Regulator swiata (HarvestValve) - pomiar od nowa przy kazdej zmianie stanu reguly (wlaczenie, wylaczenie, g od nowa): porownywalby nowa produkcje
    /// ze starym zuzyciem (projekt 3.5, ryzyko 5).
    /// Obieg: zloto nie powstaje - towar w magazynie wsi z pracy ludzi okregu (jak dzis), sprzedaz wozem ta sama droga co dzis; suma swiata typu bez zmian.
    /// Odczyt z watkow AI (postfiks modelu wsi): jedna referencja pamieci (g typow, g lasu) podmieniana na watku glownym, slowniki nigdy nie zmieniane
    /// po publikacji; zadnego liczenia ksiegi w postfiksie. Wyjatek na jednej wsi = potkniecie (licznik), ta wies w tym wywolaniu wedlug wzoru gry.
    /// Gracz: w dymku osady na mapie (wies) linia "People of this land: x{u_v}" pod "Primary Production" (latki na TooltipRefresherCollection i
    /// NavalTooltipRefresherCollection - NavalDLC rejestruje dymek osady od nowa) i jedno zdanie w oknie "People of this land" (PeopleMenu).
    /// Klucz wylaczony: postfiks, las, dymek i okno nic nie robia (stan 4d33042); linia logu raz na sesje.
    /// </summary>
    internal static class VillageByPeople
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private const string Version = "1";
        private const string CsvName = "plon-wsi.csv";
        private const string CsvHeader = "dzien;wies_id;wies;typ;krolestwo;kultura_start;ludzie;u_v;g_typu;poziom_hearth;czynnik;towar_glowny;baza_gry;baza_reguly;plon_glowny_dzien;dni_pomiaru;las_gry;las_reguly;uwagi";
        internal static readonly TextObject Text = new TextObject("{=!}Armoury: goods follow the people of this land");

        // ------------------------------------------------------------ stan kampanii (zapis: arm_villagepeople)
        private static bool _cal;                                   // g typow policzone
        private static int _calDay = -1;
        private static double _calRefV;                             // srednia wies uslugi skal w dniu kalibracji g
        private static bool _calWatch;                              // klucz WatchKeepsFlatOutput w dniu kalibracji g
        private static bool _wasOn;                                 // regula czynna (klucz + g) przy ostatnim sprawdzeniu - przejscia = regulator od nowa
        private static readonly Dictionary<string, double> _g = new Dictionary<string, double>(StringComparer.Ordinal);      // id typu wsi -> g
        private static readonly Dictionary<string, double> _sCal = new Dictionary<string, double>(StringComparer.Ordinal);   // id typu -> suma (poziom + 1) / 2 przy kalibracji
        private static readonly Dictionary<string, int> _nCal = new Dictionary<string, int>(StringComparer.Ordinal);         // id typu -> wsi pod regula przy kalibracji
        private static double _gWood = 1.0, _wCal;                  // las wsi: g i suma klimatu wsi bez drwali przy kalibracji
        private static string _loadNote;                            // null = nowa kampania; inaczej skad stan po wczytaniu

        // ------------------------------------------------------------ pamiec sesji
        private sealed class Memory
        {
            public Dictionary<VillageType, float> G;
            public float GWood;
        }
        private static volatile Memory _mem;
        private static int _stumbles;                               // watek glowny
        private static int _readStumbles;                           // watki AI (Interlocked)
        private static int _tips;                                   // linie dymka od ostatniej linii doby (Interlocked)
        private static readonly HashSet<string> _err = new HashSet<string>();
        private static bool _offLogged, _weekDone;
        private static readonly object _madeLock = new object();
        private static readonly Dictionary<Settlement, double> _made = new Dictionary<Settlement, double>();   // wies -> sztuki towaru glownego od _madeFrom (OnItemProduced)
        private static int _madeFrom = -1;

        internal static void Reset()
        {
            _cal = false; _calDay = -1; _calRefV = 0.0; _calWatch = false; _wasOn = false;
            _g.Clear(); _sCal.Clear(); _nCal.Clear(); _gWood = 1.0; _wCal = 0.0; _loadNote = null;
            _mem = null; _stumbles = 0; _readStumbles = 0; _tips = 0; _err.Clear(); _offLogged = false; _weekDone = false;
            lock (_madeLock) { _made.Clear(); _madeFrom = -1; }
        }

        /// <summary>Klucz VillageOutputByPeople.</summary>
        internal static bool KeyOn { get { var s = Settings.Current; return s != null && s.VillageOutputByPeople; } }

        private static int Today() { return (int)CampaignTime.Now.ToDays; }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;                                                            // liczymy, niczego nie gasimy
            try { if (_err.Add(where)) { if (e != null) Log.Error("VillageByPeople." + where, e); else Log.Info("VillageByPeople: potkniecie - " + where + "."); } } catch { }
        }

        private static void ReadStumble(Exception e)
        {
            if (Interlocked.Increment(ref _readStumbles) == 1) { try { Log.Error("VillageByPeople.Odczyt", e); } catch { } }
        }

        // ============================================================ odczyt (dowolny watek, bez zamka)

        /// <summary>Czynnik reguly dla wsi: u_v x g(typ) / ((poziom + 1) x 0.5). false = wies poza regula (wzor gry): klucz wylaczony, brak g, wies bez skali
        /// (k = 0, potkniecie, wyjatek Strazy), typ spoza kalibracji. Nie rzuca.</summary>
        internal static bool TryFactor(Village v, out float factor, out float u, out float g)
        {
            factor = 1f; u = 1f; g = 1f;
            try
            {
                if (v == null || !KeyOn) return false;
                var m = _mem;
                var t = v.VillageType;
                if (m == null || t == null || !m.G.TryGetValue(t, out g)) { g = 1f; return false; }
                if (!PeopleScale.TryVillage(v, out u)) { u = 1f; return false; }
                float lvl = (v.GetHearthLevel() + 1) * 0.5f;
                if (!(lvl > 0f)) return false;
                factor = u * g / lvl;
                if (float.IsNaN(factor) || float.IsInfinity(factor) || factor < 0f) { factor = 1f; return false; }
                return true;
            }
            catch (Exception e) { ReadStumble(e); factor = 1f; return false; }
        }

        /// <summary>MaterialLaw.ProdPostfix (najbardziej zewnetrzny model wsi, _depth 1): czynnik reguly wzorem "czynnik x suma" - po mnozniku wydobycia
        /// i powtorzen BK, przed rekami spustoszenia. Wlasny try - wyjatek tutaj nie zabiera dalszych krokow postfiksu.</summary>
        internal static void ProdStep(Village village, ref ExplainedNumber r)
        {
            if (!KeyOn) return;                                     // klucz wylaczony: nic nie dopisujemy (stan 4d33042)
            try
            {
                float f, u, g;
                if (!TryFactor(village, out f, out u, out g) || f == 1f) return;
                float sum = 1f + r.SumOfFactors;
                if (sum > 0f) r.AddFactor((f - 1f) * sum, Text);
            }
            catch (Exception e) { ReadStumble(e); }
        }

        /// <summary>Las wsi (VillageWoodlot.PiecesPerDay - tick lasu i magazyn wsi): u_v x g(las); 1 poza regula. Klimat mnozy wolajacy. Nie rzuca.</summary>
        internal static float WoodFactor(Village v)
        {
            if (!KeyOn) return 1f;
            try
            {
                var m = _mem;
                float u;
                if (m == null || v == null || !PeopleScale.TryVillage(v, out u)) return 1f;
                float f = u * m.GWood;
                return float.IsNaN(f) || float.IsInfinity(f) || f < 0f ? 1f : f;
            }
            catch (Exception e) { ReadStumble(e); return 1f; }
        }

        // ============================================================ gracz: dymek i okno osady

        /// <summary>Dymek osady (wies): "People of this land" i "x{u_v}". false = bez linii (klucz, wies poza regula).</summary>
        internal static bool TipLine(Settlement s, out string value)
        {
            value = null;
            try
            {
                if (s == null || !s.IsVillage || s.Village == null || !KeyOn) return false;
                float f, u, g;
                if (!TryFactor(s.Village, out f, out u, out g)) return false;
                value = "x" + u.ToString("0.00", Inv);
                Interlocked.Increment(ref _tips);
                return true;
            }
            catch (Exception e) { ReadStumble(e); return false; }
        }

        /// <summary>Okno "People of this land" we wsi: jedno zdanie, dlaczego wies daje mniej albo wiecej niz dawniej (napis po angielsku). null = bez zdania.</summary>
        internal static string MenuLine(Settlement s)
        {
            try
            {
                if (s == null || !s.IsVillage || s.Village == null || !KeyOn || _mem == null) return null;
                var v = s.Village;
                if (PeopleScale.WatchException(s))
                    return "Goods made here: as before - the Night's Watch lives on the men and gifts of all Westeros, not on the few people of the Gift.";
                float f, u, g, people;
                if (!TryFactor(v, out f, out u, out g)) return null;
                if (!PeopleScale.TryPeople(s, out people)) people = 0f;
                return "Goods made here: x" + f.ToString("0.00", Inv) + " of what the hearths alone would give - this village stands for "
                       + Math.Max(0f, people).ToString("N0", Inv) + " people, " + u.ToString("0.00", Inv) + " times an average village of the world, and goods follow the people"
                       + (u >= PeopleScale.VillageMax - 1e-4f ? " (no village yields more than " + PeopleScale.VillageMax.ToString("0", Inv) + " times the average)" : "") + ".";
            }
            catch (Exception e) { ReadStumble(e); return null; }
        }

        // ============================================================ produkcja faktyczna (pomiar do progow testu)

        /// <summary>Nasluch OnItemProducedEvent: sztuki towaru glownego typu wsi dopisane wsi (gra i BK wolaja raz na towar z liczba sztuk) - tylko licznik.</summary>
        internal static void OnItemProduced(ItemObject item, Settlement st, int n)
        {
            if (n <= 0 || item == null || st == null || !st.IsVillage || !KeyOn || !_cal) return;
            try
            {
                var v = st.Village;
                var t = v != null ? v.VillageType : null;
                if (t == null || t.PrimaryProduction != item) return;
                lock (_madeLock)
                {
                    if (_madeFrom < 0) _madeFrom = Today();
                    double x;
                    _made.TryGetValue(st, out x);
                    _made[st] = x + n;
                }
            }
            catch (Exception e) { ReadStumble(e); }
        }

        // ============================================================ doba i start sesji (watek glowny)

        /// <summary>Z OnSessionLaunched zaraz po PeopleScale.SessionStart: g z zapisu albo kalibracja (stary zapis), przejscia stanu.</summary>
        internal static void SessionStart() { Check("start sesji"); }

        /// <summary>Z OnDailyTickBody zaraz po PeopleScale.Daily: kalibracja (nowa kampania), przejscia stanu, linia doby i co 7 dob tabela krolestw i plon-wsi.csv.</summary>
        internal static void Daily()
        {
            Check("doba");
            try { Report(); } catch (Exception e) { Stumble("Raport", e); }
        }

        private static void Check(string why)
        {
            try
            {
                bool key = KeyOn;
                bool recal = false;
                if (key && PeopleScale.Ready)
                {
                    double refV = PeopleScale.RefVillage;
                    bool watch = PeopleScale.WatchExceptionOn;
                    string reason = null;
                    if (!_cal) reason = _loadNote ?? "nowa kampania";
                    else if (Math.Abs(refV - _calRefV) > 1e-6 * Math.Max(1.0, Math.Abs(refV)))
                        reason = "srednia wies uslugi skal " + refV.ToString("0", Inv) + " zamiast " + _calRefV.ToString("0", Inv) + " z kalibracji g (usluga skal skalibrowana od nowa) - g od nowa";
                    else if (watch != _calWatch) reason = "Watch Keeps Flat Output " + (watch ? "wlaczone" : "wylaczone") + " - zbior wsi pod regula inny, g od nowa";
                    if (reason != null && refV > 0.0) { recal = _cal; Calibrate(reason, refV, watch); }
                }
                if (_cal && _mem == null) Publish();
                bool on = key && _cal;
                if (on != _wasOn)
                {
                    bool vr = HarvestValve.RestartMeasurement(on ? "plon wsi od ludzi (GL1) WLACZONY - produkcja wsi wedlug ludzi" : "plon wsi od ludzi (GL1) WYLACZONY - produkcja wsi wedlug progu hearth");
                    Log.Info("Plon wsi od ludzi (GL1): " + why + ", dzien " + Today() + " - regula " + (on ? "CZYNNA (g typow z kalibracji dnia " + _calDay + ")" : "WYLACZONA (Village Output By People = off) - plon wsi wedlug progu hearth gry, jak dotad")
                             + "; regulator wsi (221): " + (vr ? "pomiar od nowa" : "bez zmian (bez stanu albo pomiar zaczety w tej dobie)") + ".");
                    _wasOn = on;
                    _offLogged = !on;
                }
                else if (recal && on)
                    HarvestValve.RestartMeasurement("plon wsi od ludzi (GL1): g typow policzone od nowa");
                if (!on && !_offLogged)
                {
                    _offLogged = true;
                    Log.Info("Plon wsi od ludzi (GL1): " + why + ", dzien " + Today() + " - " + (key ? "czekam na usluge skal ludzi (GL0) - do tej chwili plon wedlug progu hearth gry"
                                                                                                 : "WYLACZONY (Village Output By People = off) - plon wsi wedlug progu hearth gry, jak dotad") + ".");
                }
            }
            catch (Exception e) { Stumble("Check", e); }
        }

        private static void Publish()
        {
            var map = new Dictionary<VillageType, float>();
            try
            {
                foreach (var t in VillageType.All)
                {
                    double g;
                    if (t != null && t.StringId != null && _g.TryGetValue(t.StringId, out g) && g > 0.0 && !double.IsInfinity(g)) map[t] = (float)g;
                }
            }
            catch (Exception e) { Stumble("Publish", e); }
            _mem = new Memory { G = map, GWood = (float)_gWood };                    // jedna referencja
        }

        private sealed class TAcc { public double W, U; public int N; }

        private static void Calibrate(string reason, double refV, bool watch)
        {
            var acc = new Dictionary<VillageType, TAcc>();
            int under = 0, watchN = 0, noScale = 0;
            double wC = 0.0, wCU = 0.0;
            int wN = 0;
            foreach (var v in Village.All)
            {
                try
                {
                    if (v == null || v.Settlement == null || v.VillageType == null) continue;
                    float u;
                    if (!PeopleScale.TryVillage(v, out u)) { if (PeopleScale.WatchException(v.Settlement)) watchN++; else noScale++; continue; }
                    TAcc a;
                    if (!acc.TryGetValue(v.VillageType, out a)) { a = new TAcc(); acc[v.VillageType] = a; }
                    a.W += (v.GetHearthLevel() + 1) * 0.5; a.U += u; a.N++;
                    under++;
                    if (!VillageWoodlot.IsLumber(v)) { double c = VillageWoodlot.ClimateFactor(v); wC += c; wCU += c * u; wN++; }
                }
                catch (Exception e) { Stumble("Kalibracja(wies)", e); }
            }
            _g.Clear(); _sCal.Clear(); _nCal.Clear();
            var parts = new List<string>();
            var zero = new List<string>();
            var types = new List<KeyValuePair<VillageType, TAcc>>(acc);
            types.Sort((x, y) => y.Value.N.CompareTo(x.Value.N));
            foreach (var kv in types)
            {
                string id = kv.Key.StringId;
                if (string.IsNullOrEmpty(id) || id.IndexOfAny(new[] { '|', ';', '=', ':' }) >= 0) continue;
                double g = kv.Value.U > 0.0 ? kv.Value.W / kv.Value.U : 1.0;    // typ bez ludzi (suma u_v = 0): plon 0 przy kazdym g
                if (!(kv.Value.U > 0.0)) zero.Add(id);
                _g[id] = g; _sCal[id] = kv.Value.W; _nCal[id] = kv.Value.N;
                parts.Add(id + " " + g.ToString("0.000", Inv) + " (" + kv.Value.N + ")");
            }
            _gWood = wCU > 0.0 ? wC / wCU : 1.0;
            _wCal = wC;
            _calDay = Today(); _calRefV = refV; _calWatch = watch; _cal = true;
            Publish();
            Log.Info("Plon wsi od ludzi (GL1): kalibracja (" + reason + ", dzien " + _calDay + ") - g(typ) = suma((poziom hearth + 1) / 2) / suma(u_v) po wsiach typu pod regula"
                     + " (suma swiata kazdego typu w tym dniu = wzor gry co do sztuki, przed zima, budowlami i regulatorem): " + string.Join(", ", parts.ToArray())
                     + (zero.Count > 0 ? "; typy bez ludzi (plon 0): " + string.Join(", ", zero.ToArray()) : "")
                     + "; las wsi g " + _gWood.ToString("0.000", Inv) + " (wsi bez drwali " + wN + ", suma klimatu " + wC.ToString("0.0", Inv) + ", suma klimat x u_v " + wCU.ToString("0.0", Inv) + ")"
                     + "; wsi pod regula " + under + ", Straz przy wzorze gry (Watch Keeps Flat Output) " + watchN + ", bez skali (k = 0 albo potkniecie uslugi) " + noScale
                     + "; srednia wies uslugi skal " + refV.ToString("0", Inv) + "; zapis arm_villagepeople.");
        }

        // ============================================================ linie logu

        private static string F(double x, string fmt) { return double.IsNaN(x) || double.IsInfinity(x) ? "-" : x.ToString(fmt, Inv); }

        private static string Spread(List<float> l, string fmt)
        {
            if (l.Count == 0) return "-";
            l.Sort();
            Func<double, float> q = p => l[(int)Math.Round(p * (l.Count - 1))];
            return F(l[0], fmt) + "/" + F(q(0.1), fmt) + "/" + F(q(0.5), fmt) + "/" + F(q(0.9), fmt) + "/" + F(l[l.Count - 1], fmt);
        }

        private static string Name(Settlement s) { try { return s != null && s.Name != null ? s.Name.ToString() : "?"; } catch { return "?"; } }

        private static string Clean(string s) { return string.IsNullOrEmpty(s) ? "" : s.Replace(';', ',').Replace('\n', ' ').Replace('\r', ' '); }

        // jedna wies w raporcie
        private sealed class VRow
        {
            public Village V;
            public bool Under, Watch, Lumber, Cap;
            public float U, G, F, People;
            public double Lvl;          // (poziom + 1) x 0.5
            public double Clim;         // klimat lasu
        }

        // towary, ktore liczy linia doby (surowce wsi z list typow) i towary tabeli krolestw
        private static readonly string[] KItems = { "grain", "fish", "flax", "wool", "salt", "iron", "hardwood", "clay", "olives", "grape" };
        private static readonly string[] KNames = { "zboze", "ryby", "len", "welna", "sol", "ruda", "drewno drwali", "glina", "oliwki", "winogrona" };

        private static void Report()
        {
            if (!KeyOn || !_cal) return;
            var m = _mem;
            int day = Today();
            int readSt = Interlocked.Exchange(ref _readStumbles, 0);
            int tips = Interlocked.Exchange(ref _tips, 0);
            if (m == null || !PeopleScale.Ready)
            {
                Log.Info("Plon wsi od ludzi (GL1): dzien " + day + " - brak pamieci skal (usluga GL0 niegotowa) - plon wedlug progu hearth gry; potkniecia " + (_stumbles + readSt) + ".");
                _stumbles = 0;
                return;
            }
            var rows = new List<VRow>(600);
            var sToday = new Dictionary<VillageType, double>();
            var sGame = new Dictionary<VillageType, double>();
            int watchN = 0, noScale = 0, noType = 0, capN = 0;
            double wToday = 0.0, wGame = 0.0;
            var fl = new List<float>(600);
            foreach (var v in Village.All)
            {
                try
                {
                    if (v == null || v.Settlement == null || v.VillageType == null) continue;
                    var r = new VRow { V = v, Lvl = (v.GetHearthLevel() + 1) * 0.5, Lumber = VillageWoodlot.IsLumber(v) };
                    r.Clim = r.Lumber ? 0.0 : VillageWoodlot.ClimateFactor(v);
                    float people;
                    r.People = PeopleScale.TryPeople(v.Settlement, out people) ? people : 0f;
                    float f, u, g;
                    r.Under = TryFactor(v, out f, out u, out g);
                    if (r.Under) { r.U = u; r.G = g; r.F = f; r.Cap = u >= PeopleScale.VillageMax - 1e-4f; if (r.Cap) capN++; fl.Add(f); }
                    else
                    {
                        r.F = 1f;
                        if (PeopleScale.WatchException(v.Settlement)) { r.Watch = true; watchN++; }
                        else if (!m.G.ContainsKey(v.VillageType)) noType++;
                        else noScale++;
                    }
                    rows.Add(r);
                    if (r.Under)
                    {
                        double x;
                        sToday.TryGetValue(v.VillageType, out x); sToday[v.VillageType] = x + (double)r.U * r.G;
                        sGame.TryGetValue(v.VillageType, out x); sGame[v.VillageType] = x + r.Lvl;
                        if (!r.Lumber) { wToday += r.Clim * r.U * m.GWood; wGame += r.Clim; }
                    }
                }
                catch (Exception e) { Stumble("Raport(wies)", e); }
            }
            // typy i surowce wobec kalibracji (baza modelu: stawka typu x u_v x g; kalibracja: stawka x suma (poziom + 1) / 2 dnia kalibracji)
            var itemCal = new Dictionary<string, double>(StringComparer.Ordinal);
            var itemNow = new Dictionary<string, double>(StringComparer.Ordinal);
            var typeParts = new List<KeyValuePair<string, double>>();
            var ratioGame = new List<float>();
            foreach (var kv in sToday)
            {
                try
                {
                    string id = kv.Key.StringId;
                    double cal;
                    if (id == null || !_sCal.TryGetValue(id, out cal) || !(cal > 0.0)) continue;
                    typeParts.Add(new KeyValuePair<string, double>(id, kv.Value / cal));
                    double gm;
                    if (sGame.TryGetValue(kv.Key, out gm) && gm > 0.0) ratioGame.Add((float)(kv.Value / gm));
                    if (kv.Key.Productions == null) continue;
                    foreach (var p in kv.Key.Productions)
                    {
                        if (p.Item1 == null || p.Item1.StringId == null || !(p.Item2 > 0f)) continue;
                        string iid = p.Item1.StringId;
                        double a;
                        itemCal.TryGetValue(iid, out a); itemCal[iid] = a + p.Item2 * cal;
                        itemNow.TryGetValue(iid, out a); itemNow[iid] = a + p.Item2 * kv.Value;
                    }
                }
                catch (Exception e) { Stumble("Raport(typ)", e); }
            }
            typeParts.Sort((x, y) => string.CompareOrdinal(x.Key, y.Key));
            var items = new List<string>(itemCal.Keys);
            items.Sort((x, y) => itemCal[y].CompareTo(itemCal[x]));
            var sb = new StringBuilder(2500);
            sb.Append("Plon wsi od ludzi (GL1): dzien ").Append(day).Append(" - suma swiata wedlug typow wobec kalibracji (dzien ").Append(_calDay).Append("):");
            foreach (var kv in typeParts) sb.Append(' ').Append(kv.Key).Append(' ').Append(F(kv.Value, "0.000")).Append(',');
            if (typeParts.Count > 0) sb.Length--;
            sb.Append("; surowce wobec kalibracji (baza modelu: stawka typu x u_v x g, przed zima, budowlami i regulatorem):");
            foreach (var id in items)
            {
                double c = itemCal[id], n;
                itemNow.TryGetValue(id, out n);
                sb.Append(' ').Append(id).Append(' ').Append(F(c > 0.0 ? n / c : double.NaN, "0.000")).Append(',');
            }
            if (items.Count > 0) sb.Length--;
            sb.Append("; las wsi ").Append(F(_wCal > 0.0 ? wToday / _wCal : double.NaN, "0.000"));
            float rMin = 0f, rMax = 0f;
            if (ratioGame.Count > 0) { ratioGame.Sort(); rMin = ratioGame[0]; rMax = ratioGame[ratioGame.Count - 1]; }
            sb.Append("; regula / wzor gry dzis (te same wsie): typy min/max ").Append(F(rMin, "0.000")).Append('/').Append(F(rMax, "0.000"))
              .Append(", las ").Append(F(wGame > 0.0 ? wToday / wGame : double.NaN, "0.000"));
            sb.Append("; wsi pod regula ").Append(fl.Count).Append(", czynnik (u_v x g / ((poziom + 1) x 0.5)) min/p10/med/p90/max ").Append(Spread(fl, "0.000"))
              .Append("; poza regula: Straz przy wzorze gry (Watch Keeps Flat Output) ").Append(watchN).Append(", bez skali (k = 0 albo potkniecie uslugi) ").Append(noScale)
              .Append(", typ spoza kalibracji ").Append(noType)
              .Append("; wsie na suficie ").Append(PeopleScale.VillageMax.ToString("0.0", Inv)).Append(": ").Append(capN)
              .Append("; dymek People of this land: ").Append(tips)
              .Append("; potkniecia ").Append(_stumbles + readSt).Append(" (odczyt ").Append(readSt).Append(").");
            Log.Info(sb.ToString());
            _stumbles = 0;
            bool week = day % 7 == 0 || !_weekDone;
            if (!week) return;
            _weekDone = true;
            Dictionary<Settlement, double> made;
            int from;
            lock (_madeLock) { made = new Dictionary<Settlement, double>(_made); from = _madeFrom; _made.Clear(); _madeFrom = day; }
            int days = from >= 0 ? Math.Max(1, day - from) : 0;
            try { KingdomLine(day, rows, m); } catch (Exception e) { Stumble("Raport(krolestwa)", e); }
            try { CsvRows(day, rows, made, days); } catch (Exception e) { Stumble("Raport(csv)", e); }
        }

        private static float Rate(VillageType t, string itemId)
        {
            float r = 0f;
            if (t == null || t.Productions == null) return 0f;
            foreach (var p in t.Productions) if (p.Item1 != null && p.Item1.StringId == itemId && p.Item2 > 0f) r += p.Item2;
            return r;
        }

        private sealed class KAcc
        {
            public string Name; public int V, Under, WheatN; public double People, FSum, WheatRule, WheatGame, WoodRule, WoodGame, WheatPeople;
            public double[] Rule = new double[10], Game = new double[10];
        }

        private static void KingdomLine(int day, List<VRow> rows, Memory m)
        {
            var acc = new Dictionary<string, KAcc>();
            foreach (var r in rows)
            {
                try
                {
                    var fct = r.V.Settlement.MapFaction;
                    string id = fct != null && fct.StringId != null ? fct.StringId : "-";
                    KAcc k;
                    if (!acc.TryGetValue(id, out k)) { k = new KAcc { Name = fct != null && fct.Name != null ? fct.Name.ToString() : "bez krolestwa" }; acc[id] = k; }
                    k.V++; k.People += r.People;
                    double rule = r.Under ? (double)r.U * r.G : r.Lvl;     // poza regula - wzor gry
                    if (r.Under) { k.Under++; k.FSum += r.F; }
                    for (int i = 0; i < KItems.Length; i++)
                    {
                        float rate = Rate(r.V.VillageType, KItems[i]);
                        if (rate <= 0f) continue;
                        k.Rule[i] += rate * rule; k.Game[i] += rate * r.Lvl;
                    }
                    if (r.V.VillageType.StringId == "wheat_farm") { float g0 = Rate(r.V.VillageType, "grain"); k.WheatN++; k.WheatRule += g0 * rule; k.WheatGame += g0 * r.Lvl; k.WheatPeople += r.People; }
                    if (!r.Lumber) { k.WoodRule += r.Clim * (r.Under ? r.U * m.GWood : 1.0); k.WoodGame += r.Clim; }
                }
                catch (Exception e) { Stumble("Raport(krolestwo)", e); }
            }
            var list = new List<KAcc>(acc.Values);
            list.Sort((a, b) => b.People.CompareTo(a.People));
            var sb = new StringBuilder(6000);
            sb.Append("Plon wsi od ludzi wedlug krolestw (GL1): dzien ").Append(day).Append(" - baza modelu szt./d regula/wzor gry (stawka typu x u_v x g wobec stawka x (poziom + 1) / 2; przed zima, budowlami, regulatorem i mnoznikami wydobycia; las wsi w stawkach lasu x klimat) - ");
            for (int i = 0; i < list.Count; i++)
            {
                var k = list[i];
                if (i > 0) sb.Append(" | ");
                sb.Append(k.Name).Append(": wsi ").Append(k.V).Append(" (pod regula ").Append(k.Under).Append(", czynnik sr. ").Append(k.Under > 0 ? F(k.FSum / k.Under, "0.00") : "-")
                  .Append("), ludzie wsi ").Append((k.People / 1000.0).ToString("0", Inv)).Append(" tys.");
                for (int j = 0; j < KItems.Length; j++)
                    if (k.Game[j] > 0.0 || k.Rule[j] > 0.0) sb.Append(", ").Append(KNames[j]).Append(' ').Append(F(k.Rule[j], "0.0")).Append('/').Append(F(k.Game[j], "0.0"));
                sb.Append(", las wsi ").Append(F(k.WoodRule, "0.0")).Append('/').Append(F(k.WoodGame, "0.0"));
                if (k.WheatN > 0) sb.Append(", wsi zboza ").Append(k.WheatN).Append(" po ").Append(F(k.WheatRule / k.WheatN, "0.0")).Append('/').Append(F(k.WheatGame / k.WheatN, "0.0"))
                                    .Append(" (ludzie sr. ").Append(F(k.WheatPeople / k.WheatN / 1000.0, "0.0")).Append(" tys.)");   // recenzja obiegu GL (D5): prog GL1-2 = stosunek plonu wobec stosunku ludzi tych samych wsi
            }
            sb.Append(" (krolestwo = wlasciciel wsi dzis; plik ").Append(CsvName).Append(").");
            Log.Info(sb.ToString());
        }

        private static void CsvRows(int day, List<VRow> rows, Dictionary<Settlement, double> made, int days)
        {
            var sb = new StringBuilder(rows.Count * 140);
            foreach (var r in rows)
            {
                try
                {
                    var s = r.V.Settlement;
                    var t = r.V.VillageType;
                    var prim = t.PrimaryProduction;
                    float rate = prim != null && prim.StringId != null ? Rate(t, prim.StringId) : 0f;
                    double rule = r.Under ? (double)r.U * r.G : r.Lvl;
                    double mk;
                    made.TryGetValue(s, out mk);
                    string note = r.Watch ? "straz" : (!r.Under ? "poza_regula" : (r.Cap ? "sufit" : ""));
                    if (r.Lumber) note += note.Length > 0 ? ",drwale" : "drwale";
                    var fct = s.MapFaction;
                    sb.Append(day).Append(';').Append(s.StringId).Append(';').Append(Clean(Name(s))).Append(';').Append(t.StringId).Append(';')
                      .Append(Clean(fct != null && fct.Name != null ? fct.Name.ToString() : "")).Append(';').Append(Clean(PeopleBook.CultureAtCalibration(s))).Append(';')
                      .Append(r.People.ToString("0", Inv)).Append(';').Append(r.Under ? r.U.ToString("0.0000", Inv) : "").Append(';').Append(r.Under ? r.G.ToString("0.0000", Inv) : "").Append(';')
                      .Append(((int)Math.Round(r.Lvl * 2.0 - 1.0)).ToString(Inv)).Append(';').Append(r.F.ToString("0.0000", Inv)).Append(';')
                      .Append(prim != null ? prim.StringId : "").Append(';').Append((rate * r.Lvl).ToString("0.000", Inv)).Append(';').Append((rate * rule).ToString("0.000", Inv)).Append(';')
                      .Append(days > 0 ? (mk / days).ToString("0.000", Inv) : "").Append(';').Append(days).Append(';')
                      .Append(r.Lumber ? "" : r.Clim.ToString("0.000", Inv)).Append(';').Append(r.Lumber ? "" : (r.Clim * (r.Under ? r.U * _gWood : 1.0)).ToString("0.000", Inv)).Append(';')
                      .Append(note).Append(Environment.NewLine);
                }
                catch (Exception e) { Stumble("Raport(csv wiersz)", e); }
            }
            Log.Csv(CsvName, CsvHeader, sb.ToString());
        }

        // ============================================================ zapis (arm_villagepeople)

        /// <summary>"1|dzien kalibracji|srednia wies uslugi skal|Straz 0/1|regula czynna 0/1|g lasu|suma klimatu|typ=g:suma:wsi;..." Pusty przed kalibracja.</summary>
        internal static string Export()
        {
            if (!_cal) return "";
            var sb = new StringBuilder(120 + _g.Count * 40);
            sb.Append(Version).Append('|').Append(_calDay).Append('|').Append(_calRefV.ToString("R", Inv)).Append('|').Append(_calWatch ? '1' : '0').Append('|')
              .Append(_wasOn ? '1' : '0').Append('|').Append(_gWood.ToString("R", Inv)).Append('|').Append(_wCal.ToString("R", Inv)).Append('|');
            bool first = true;
            foreach (var kv in _g)
            {
                if (!first) sb.Append(';');
                first = false;
                double s; int n;
                _sCal.TryGetValue(kv.Key, out s); _nCal.TryGetValue(kv.Key, out n);
                sb.Append(kv.Key).Append('=').Append(kv.Value.ToString("R", Inv)).Append(':').Append(s.ToString("R", Inv)).Append(':').Append(n.ToString(Inv));
            }
            return sb.ToString();
        }

        /// <summary>Wczytanie: brak pola (stary zapis albo kampania bez reguly), inny format albo blad - g od nowa przy pierwszej dobie z kluczem (linia w logu);
        /// stan "czynna" z zapisu decyduje, czy wczytanie zmienia regule (regulator od nowa).</summary>
        internal static void Import(string v)
        {
            _cal = false; _calDay = -1; _calRefV = 0.0; _calWatch = false; _wasOn = false; _g.Clear(); _sCal.Clear(); _nCal.Clear(); _gWood = 1.0; _wCal = 0.0; _mem = null;
            if (string.IsNullOrEmpty(v)) { _loadNote = "zapis bez pola arm_villagepeople - kalibracja przy pierwszym wczytaniu z regula"; return; }
            try
            {
                var p = v.Split('|');
                if (p.Length < 8 || p[0] != Version)
                {
                    _loadNote = "nieznany format pola arm_villagepeople (" + (p.Length > 0 ? p[0] : "?") + ") - kalibracja od nowa";
                    Log.Info("Plon wsi od ludzi (GL1): " + _loadNote + ".");
                    return;
                }
                int calDay = int.Parse(p[1], Inv);
                double refV = double.Parse(p[2], NumberStyles.Float, Inv);
                double gw = double.Parse(p[5], NumberStyles.Float, Inv), wc = double.Parse(p[6], NumberStyles.Float, Inv);
                var g = new Dictionary<string, double>(StringComparer.Ordinal);
                var sc = new Dictionary<string, double>(StringComparer.Ordinal);
                var nc = new Dictionary<string, int>(StringComparer.Ordinal);
                if (p[7].Length > 0)
                    foreach (var part in p[7].Split(';'))
                    {
                        int eq = part.IndexOf('=');
                        if (eq <= 0) continue;
                        var q = part.Substring(eq + 1).Split(':');
                        if (q.Length < 3) continue;
                        string id = part.Substring(0, eq);
                        g[id] = double.Parse(q[0], NumberStyles.Float, Inv); sc[id] = double.Parse(q[1], NumberStyles.Float, Inv); nc[id] = int.Parse(q[2], Inv);
                    }
                if (!(refV > 0.0) || double.IsInfinity(refV) || !(gw >= 0.0) || double.IsInfinity(gw) || g.Count == 0)
                {
                    _loadNote = "zle liczby w polu arm_villagepeople - kalibracja od nowa";
                    Log.Info("Plon wsi od ludzi (GL1): " + _loadNote + ".");
                    return;
                }
                foreach (var kv in g) _g[kv.Key] = kv.Value;
                foreach (var kv in sc) _sCal[kv.Key] = kv.Value;
                foreach (var kv in nc) _nCal[kv.Key] = kv.Value;
                _calDay = calDay; _calRefV = refV; _calWatch = p[3] == "1"; _gWood = gw; _wCal = wc;
                _wasOn = p[4] == "1";                               // tylko przy poprawnym polu - g od nowa (pole zle) zawsze zaczyna od "nieczynna" (regulator od nowa)
                _loadNote = "z zapisu";
                _cal = true;
            }
            catch (Exception e)
            {
                _cal = false; _g.Clear(); _sCal.Clear(); _nCal.Clear(); _gWood = 1.0; _wCal = 0.0;
                _loadNote = "blad odczytu pola arm_villagepeople - kalibracja od nowa";
                Stumble("Import", e);
            }
        }

        // ============================================================ dymek osady (latka UI - SubModuleMain)

        private static bool _tipWired;

        /// <summary>Postfiks dymka osady gry (TooltipRefresherCollection.RefreshSettlementTooltip) i NavalDLC (NavalTooltipRefresherCollection - rejestruje
        /// dymek osady od nowa): linia "People of this land" pod "Primary Production". Raz na proces.</summary>
        internal static void ApplyTip(Harmony h)
        {
            if (_tipWired) return;
            _tipWired = true;
            var where = new List<string>();
            foreach (var tn in new[] { "TaleWorlds.CampaignSystem.ViewModelCollection.TooltipRefresherCollection", "NavalDLC.View.NavalTooltipRefresherCollection" })
            {
                try
                {
                    var t = AccessTools.TypeByName(tn);
                    var mi = t != null ? AccessTools.Method(t, "RefreshSettlementTooltip") : null;
                    if (mi == null) continue;
                    h.Patch(mi, postfix: new HarmonyMethod(typeof(VillageByPeople), nameof(TipPostfix)));
                    where.Add(t.Name);
                }
                catch (Exception e) { Log.Error("VillageByPeople.ApplyTip(" + tn + ")", e); }
            }
            Log.Info("Plon wsi od ludzi (GL1): dymek osady - linia People of this land wpieta w: " + (where.Count > 0 ? string.Join(", ", where.ToArray()) : "NIGDZIE (brak metod dymka - zostaje zdanie w oknie People of this land)") + ".");
        }

        public static void TipPostfix(PropertyBasedTooltipVM __0, object[] __1)
        {
            try
            {
                if (__0 == null || __1 == null || __1.Length == 0 || !KeyOn) return;
                var s = __1[0] as Settlement;
                string val;
                if (s == null || !s.IsVillage || !TipLine(s, out val)) return;
                var list = __0.TooltipPropertyList;
                if (list == null) return;
                string prim = null;
                try { prim = GameTexts.FindText("str_primary_production").ToString(); } catch { prim = null; }
                int at = -1;
                for (int i = 0; i < list.Count; i++)
                    if (list[i] != null && prim != null && list[i].DefinitionLabel == prim) { at = i; break; }
                var row = new TooltipProperty("People of this land", val, 0);
                if (at >= 0) list.Insert(at + 1, row); else list.Add(row);
            }
            catch (Exception e) { ReadStumble(e); }
        }
    }
}
