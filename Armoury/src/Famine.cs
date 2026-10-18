using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// SPICHLERZ OKREGU I GLOD (E4/6 = paczka 191, PLAN 4.11; projekt etapu 4 R10 i czlon H w R1, rozdz. 3 "E4/6"; docs/PROJEKT-GLOD-2026-10-07.md (GL) rozdz. 2
    /// z poprawka Jeffa 07.10 - SPICHLERZ napelnia prawdziwa produkcja zywnosci wsi, nie abstrakcyjne racje).
    /// Gra nie karmi naszych ludzi wcale: wies tylko produkuje towar, "Food" maja warownie, a glodu ludzi w grze nie ma (GL 1.1-1.6). Odtad:
    ///  - kazda wies (okreg) ma SPICHLERZ w racjach (1 racja = jedzenie 1 czlowieka na dobe, skala ksiegi ludzi 1:1);
    ///  - PLON dnia = FamineYieldPerPerson (1.2) x L x q: L = ludzie okregu (w domu + jego uchodzcy), q = zywnosc wsi z modelu gry dzis / (poziom + 1)
    ///    (GL 2.2 i 3: model CalculateDailyFoodProductionAmount z latkami - zima E4/1 wedlug krainy, mnoznik rak spustoszenia E4/5, stan wsi (spladrowana:
    ///    zero albo Normal x udzial ludzi w domu - LootedProducesByPeople), zadania "pol produkcji"); dzielnik usuwa skok progow 200/600. Zwykly rok (lato,
    ///    wies Normal, bez uchodzcow) = 1.2 x potrzeby; zima, spustoszenie, brak rak i stan wsi obnizaja plon same - przez model gry; czynnik rak E4/7 w q
    ///    bez czlonow zmarlych cywilow i narodzin (FreeHands.FamineFix - plon i tak idzie za liczba ludzi; przeglad scalenia E4/6 + E4/7);
    ///  - JEDZA: najpierw gospodarze (ludzie w domu) ze swojego spichlerza i plonu, potem uchodzcy obecni w regionie (warownia + jej wsie) z tego,
    ///    co zostalo we wszystkich spichlerzach regionu (GL 2.2 pkt 3, 2.3); przednowek: spichlerz ponizej FamineRationDays dni i plon nie starcza -
    ///    wszyscy jedza 90% (nikt nie umiera, przyrost zwalnia); nadwyzka ponad sufit (FamineBarnYears lat potrzeby) to towar na wozy, jak dotad;
    ///  - GLOD = spichlerz pusty i plon < potrzeba: brak u = 1 - zjedzone / potrzeba; zgony rocznie FamineDeathMaxPercent (70%) x ((u - prog) / (1 - prog))^2
    ///    ponad FamineThresholdPercent (10%) (GL 2.5: 1315-17, 1693-94, 1695-97), osobno gospodarze i uchodzcy; smierc schodzi z DOM przez PeopleBook.Move
    ///    (MFamineDead - ubytek trwaly, R3), z konta UCHODZCY - PeopleBook.RefugeesDie;
    ///  - UCHODZCY Z GLODU (GL 2.6, U15a): tylu, ilu umiera, rusza w droge - PeopleBook.Move (DOM -> UCHODZCY, MFamineFled) na konto wsi pochodzenia, miejsce
    ///    pobytu = najblizszy region tego samego krolestwa (inaczej niewrogiego) w promieniu FamineRefugeRadius ze spichlerzami ponad FamineRefugeMinDays (180)
    ///    dni; brak - zostaja (i gloduja). Jedno miejsce na konto wsi, przeprowadzka najwyzej raz na FamineMoveDays (30) dob. Powrot - regula E4/5
    ///    (Devastation.Daily, liniowo do RefugeeReturnDays), ale nikt nie wraca do glodu (ReturnGate): wies glodna nie przyjmuje, a gdy konto przebywa w innym
    ///    regionie i pole wsi nie karmi czlowieka (plon na gospodarza < 1 racji - zima), spichlerz musi miec FamineReturnMinDays (90) dni dla gospodarzy (konto
    ///    we wlasnym regionie je z tych samych spichlerzy - wstrzymuje je tylko glod gospodarzy); rok liczy sie OSOBNO dla czesci konta z glodu (od dnia, w ktorym
    ///    wies znow przyjmuje; nowy glod wsi zeruje ten dzien) i dla reszty (rabunek, zerowanie - od ostatniego najazdu, decyzja Jeffa 11.10) (odstepstwo od
    ///    "90 dni spichlerza" GL 2.6 - patrz CHANGELOG: spalony spichlerz po ciezkim rabunku dlugo nie wraca do 90 dni calego okregu, a powracajacy w sezonie sami
    ///    sie wyzywia - dosiewaja tyle, ile jedza; doslowna regula lamalaby "rok od najazdu");
    ///  - OBLEZENIE (GL 2.9): uchodzcy regionu oblezonej warowni siedza w murach i jedza z jej Food (postfiks na DefaultSettlementFoodModel - "Refugees
    ///    sheltering": uchodzcy / Q jednostek na dobe, Q = mieszczanie / (dobrobyt / 40), zamek - Q swiata); nikt z nich nie wychodzi;
    ///  - WAROWNIA PRZY FOOD 0 (gra IsStarving): gina mieszczanie (stan miasta E4/4 - PeopleBook.TownDie, w ksiedze MFamineTown, suma na miasto w zapisie)
    ///    i schronieni uchodzcy wedlug u_f = niedobor / spozycie;
    ///    zaloge zabija gra (jak dotad);
    ///  - spustoszenie (Devastation.Strike) pali spichlerzowi ten sam ulamek, ktory trafil ludzi okregu (Burn, GL 2.2 pkt 6);
    ///  - czlon H przyrostu naturalnego (E4/4): brak jedzenia gospodarzy wczoraj (HungerOf) - w miejsce ciecia zimy; w glodzie dodatni przyrost = 0 (GL 2.10).
    /// Stan (spichlerze, miejsce pobytu kont, zegary) w zapisie przez PeopleBook ("arm_people" sekcja "f:", SaveText.Sync). Start i stary zapis: spichlerz
    /// FamineStartShare (1 = pelny) sufitu (GL 2.2 pkt 7). Wymaga People Unit Enabled i Natural Growth Enabled; uchodzcy z glodu - takze Devastation Enabled
    /// (konto UCHODZCY obsluguje E4/5). Wylaczone: spichlerze zamrozone, nikt nie umiera z glodu, uchodzcy z glodu wracaja w rok od dnia wylaczenia.
    /// Wyjatki per wies / konto / warownia liczone (potkniecia, pierwszy blad miejsca do logu) - bez globalnych wylacznikow (CLAUDE.md 7).
    /// </summary>
    internal static class Famine
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private const int NoDraw = 364;                 // U27: warownia bez ubytku = 364 dni (w medianie)
        private const double QMax = 3.0;                // mnoznik plonu z gry najwyzej x3
        private const double QWorldDefault = 906.0;     // GL 2.3: mieszczan na jednostke Food (zamek bez mieszczan w ksiedze), gdy swiata nie da sie policzyc
        private const int Top = 6;
        private const string CsvName = "spichlerze-regiony.csv";
        private const string CsvHeader = "dzien;region_id;region;rodzaj;kultura_start;gracz;wsie;ludzie_dom;uchodzcy_obecni;schronieni;spichlerz;dni_spichlerza;plon;zjedli;"
                                         + "brak_gospodarzy;brak_uchodzcow;zmarli_glod;uciekli_glod;zywnosc;bilans_zywnosci;q_warowni;dni_zapasu_U27;oblezona;glodna_warownia;"
                                         + "konto_e46";     // R2-4: 1 = E4/6 zmienil dzis konto uchodzcow regionu pochodzenia (S10/S11 E4/5 pomijaja ten wiersz)
        private static readonly TextObject _txtShelter = new TextObject("{=!}Refugees sheltering");

        // ------------------------------------------------------------ stan wsi (zapis: PeopleBook "f:")
        private sealed class Barn
        {
            public double Food = double.NaN;    // racje w spichlerzu okregu (NaN = jeszcze nie zalozony)
            public double Starved, Fled;        // zmarli z glodu (gospodarze i uchodzcy tej wsi) i uciekli z glodu - od poczatku kampanii
            public string Stay;                 // miejsce pobytu uchodzcow wsi (id warowni regionu); null = region wsi
            public int MoveDay = int.MinValue;  // doba ostatniej przeprowadzki konta
            public bool FamOut;                 // na koncie sa uchodzcy z glodu (FamN > 0; ich powrot - rok od doby, w ktorej wies znow przyjmuje)
            public int Recovered = -1;          // doba, od ktorej wies znow przyjmuje uchodzcow z glodu (-1 = jeszcze nie; nowy glod wsi - znow -1)
            public float U;                     // brak jedzenia gospodarzy w ostatniej dobie, 0..1 (czlon H przyrostu E4/4)
            // poprawka po recenzji E4/6 (R1-1): CZESC konta z glodu - tylko ona liczy rok od Recovered, reszta (rabunek, zerowanie) od ostatniego najazdu
            // (decyzja Jeffa 11.10). FamN = uchodzcy z glodu przy stanie konta FamAt; ubytek konta (smierc 1.3%/rok, glod, podloga 10) schodzi z obu czesci
            // proporcjonalnie (FamPart), powrot - wedlug czesci (Returned), doplyw rabunku nie rusza FamN; synchronizacja raz na dobe (CleanAccounts)
            public double FamN, FamAt;
            // r4-mur (danina zbozowa Muru, glod Wolnych Ludzi): migawka ostatniego Daily - ludzie w domu, plon doby (po GranaryFloor KS), doba; bez zapisu
            // (pierwsze Daily po wczytaniu wypelnia ja przed WallTribute.Daily i FreeFolkRaids.Daily)
            public double LastHome, LastHarvest, LastCap; public int LastDay = int.MinValue;
        }
        private static readonly object Gate = new object();
        private static readonly Dictionary<string, Barn> _barns = new Dictionary<string, Barn>();   // id osady wsi -> spichlerz
        private static volatile int _hungry;                                                        // wsie z U > 0 (szybka sciezka HungerOf)
        private static readonly Dictionary<Town, double> _shelterUnits = new Dictionary<Town, double>();   // jednostki Food na dobe zjadane przez schronionych
        private static volatile int _shelterN;
        private static bool _shelterPatched;
        // poprawka po recenzji E4/6 (R1-5, R2-3): zmarli z glodu mieszczanie od poczatku kampanii, id miasta -> ludzie (zapis "f:" jako "id=T;suma"; wsie - Barn.Starved)
        private static readonly Dictionary<string, double> _townStarved = new Dictionary<string, double>();

        // ------------------------------------------------------------ liczniki (bez zapisu)
        private static double _dBurned; private static int _dBurnN;    // spalone przy spustoszeniu od ostatniego Daily
        private static double _dEaten;                                  // E7/R1 (K4): zjedzone przez napastnikow rabunku osada po osadzie od ostatniego Daily (racje)
        private static double _sessDead, _sessFled;
        private static int _stumblesAll, _stumblesMark;                 // potkniecia razem; stan przy ostatniej linii (linia pokazuje przyrost - R2-1)
        // poprawka po recenzji E4/6 (R2-4): wsie, ktorym ReturnGate (Devastation.Daily tej doby) zmienil powrot wobec samej reguly E4/5 - do kolumny konto_e46 CSV
        private static readonly HashSet<string> _gateTouched = new HashSet<string>();
        private static readonly HashSet<string> _errSites = new HashSet<string>();
        private static int _winterCount, _firstStarve = -1;          // dni zimy (gdy kalendarz bez stanu) i doba zimy pierwszego glodu warowni AI
        private static readonly HashSet<string> _warned = new HashSet<string>();   // regiony gracza, ktorym juz napisano o glodzie (do konca glodu)
        private static bool _offLogged;
        private static string _csvPath;

        internal static bool On
        {
            get { var s = Settings.Current; return s != null && s.FamineEnabled && PeopleUnit.On && PopulationLaw.GrowthOn; }
        }
        private static bool FleeOn { get { var s = Settings.Current; return On && s.FamineFleeEnabled && Devastation.On; } }
        private static bool ShelterOn { get { var s = Settings.Current; return On && s.FamineSiegeShelter && _shelterPatched; } }

        /// <summary>Prog glodu (brak jedzenia, ponizej ktorego nikt nie umiera), 0..0.9.</summary>
        internal static float ThresholdNow
        {
            get { var s = Settings.Current; return s != null ? Math.Max(0f, Math.Min(0.9f, s.FamineThresholdPercent / 100f)) : 0.1f; }
        }

        private static void Stumble(string where, Exception e)
        {
            System.Threading.Interlocked.Increment(ref _stumblesAll);   // liczymy potkniecia, niczego nie gasimy (moze isc z watkow AI - postfiks, HungerOf)
            bool first;
            lock (_errSites) first = _errSites.Add(where);
            if (first) Log.Error(where, e);
        }

        private static double Clamp01(double x) { return double.IsNaN(x) ? 0.0 : (x < 0.0 ? 0.0 : (x > 1.0 ? 1.0 : x)); }
        private static int YearDays() { return Math.Max(28, CampaignTime.DaysInYear); }
        private static bool BadId(string id) { return string.IsNullOrEmpty(id) || id.IndexOf('|') >= 0 || id.IndexOf('=') >= 0 || id.IndexOf(',') >= 0 || id.IndexOf(';') >= 0; }

        /// <summary>Zgony rocznie (ulamek) przy braku u: Dmax x ((u - T) / (1 - T))^2 ponad prog T (GL 2.5).</summary>
        private static double DeathRate(double u, double t, double dmax)
        {
            if (!(u > t) || !(t < 1.0)) return 0.0;
            double x = (Math.Min(1.0, u) - t) / (1.0 - t);
            return dmax * x * x;
        }

        // ------------------------------------------------------------ cykl zycia stanu (wola PeopleBook)
        /// <summary>PeopleBook.Reset (konstruktor ArmouryBehavior): stan jednej kampanii nie przecieka do drugiej.</summary>
        internal static void Reset()
        {
            Clear();
            _sessDead = _sessFled = 0; _stumblesAll = _stumblesMark = 0;
            lock (_errSites) _errSites.Clear();
            _winterCount = 0; _firstStarve = -1; _warned.Clear(); _offLogged = false;
        }

        /// <summary>PeopleBook.Import (przed odczytem zapisu): spichlerze od zera - zapis bez "f:" (stary albo sprzed E4/6) zaklada je przy pierwszej dobie.</summary>
        internal static void Clear()
        {
            lock (Gate) { _barns.Clear(); _shelterUnits.Clear(); _townStarved.Clear(); _dBurned = 0; _dBurnN = 0; _dEaten = 0; }
            _hungry = 0; _shelterN = 0;
            _gateTouched.Clear();
        }

        // zapis: "|f:<id wsi>=<racje>;<zmarli z glodu>;<uciekli>;<miejsce pobytu>;<doba przeprowadzki>;<uchodzcy z glodu 0/1>;<doba odzyskania>;<brak u>;
        // <uchodzcy z glodu na koncie>;<stan konta przy nich>,...,<id miasta>=T;<zmarli z glodu mieszczanie>,..."
        internal static string Export()
        {
            List<KeyValuePair<string, Barn>> list; List<KeyValuePair<string, double>> towns;
            lock (Gate) { list = _barns.ToList(); towns = _townStarved.ToList(); }
            if (list.Count == 0 && towns.Count == 0) return "";
            var sb = new StringBuilder(16 + list.Count * 64 + towns.Count * 32);
            bool first = true;
            foreach (var kv in list)
            {
                var b = kv.Value;
                if (BadId(kv.Key) || double.IsNaN(b.Food) || double.IsInfinity(b.Food)) continue;
                string stay = b.Stay != null && !BadId(b.Stay) ? b.Stay : "";
                sb.Append(first ? "|f:" : ",").Append(kv.Key).Append('=')
                  .Append(b.Food.ToString("R", Inv)).Append(';').Append(b.Starved.ToString("R", Inv)).Append(';').Append(b.Fled.ToString("R", Inv)).Append(';')
                  .Append(stay).Append(';').Append(b.MoveDay == int.MinValue ? "" : b.MoveDay.ToString(Inv)).Append(';').Append(b.FamOut ? 1 : 0).Append(';')
                  .Append(b.Recovered.ToString(Inv)).Append(';').Append(b.U.ToString("R", Inv)).Append(';')
                  .Append(Fin(b.FamN).ToString("R", Inv)).Append(';').Append(Fin(b.FamAt).ToString("R", Inv));
                first = false;
            }
            foreach (var kv in towns)
            {
                if (BadId(kv.Key) || !(kv.Value > 0.0) || double.IsInfinity(kv.Value)) continue;
                sb.Append(first ? "|f:" : ",").Append(kv.Key).Append("=T;").Append(kv.Value.ToString("R", Inv));
                first = false;
            }
            return sb.ToString();
        }
        private static double Fin(double x) { return double.IsNaN(x) || double.IsInfinity(x) || x < 0.0 ? 0.0 : x; }

        /// <summary>PeopleBook.Import: tresc sekcji "f:" (bez przedrostka). Zepsuty wpis pomijany (spichlerz zalozy sie od nowa przy pierwszej dobie).</summary>
        internal static void Import(string part)
        {
            if (string.IsNullOrEmpty(part)) return;
            foreach (var item in part.Split(','))
            {
                try
                {
                    int eq = item.LastIndexOf('=');
                    if (eq <= 0) continue;
                    var a = item.Substring(eq + 1).Split(';');
                    if (a.Length > 0 && a[0] == "T")
                    {
                        // miasto: zmarli z glodu mieszczanie od poczatku kampanii
                        double t = Num0(a, 1);
                        if (t > 0.0) lock (Gate) _townStarved[item.Substring(0, eq)] = t;
                        continue;
                    }
                    double food = Num(a, 0);
                    if (double.IsNaN(food)) continue;
                    var b = new Barn { Food = food, Starved = Num0(a, 1), Fled = Num0(a, 2) };
                    if (a.Length > 3 && a[3].Length > 0) b.Stay = a[3];
                    int d;
                    if (a.Length > 4 && int.TryParse(a[4], NumberStyles.Integer, Inv, out d)) b.MoveDay = d;
                    b.FamOut = a.Length > 5 && a[5] == "1";
                    if (a.Length > 6 && int.TryParse(a[6], NumberStyles.Integer, Inv, out d)) b.Recovered = d;
                    double u = Num0(a, 7); b.U = (float)Clamp01(u);
                    // czesc konta z glodu (od poprawki R1-1); zapis bez niej - cale konto z glodu (jak dotad), przyciete do konta przy pierwszej dobie
                    if (a.Length > 8) { b.FamN = Num0(a, 8); b.FamAt = Num0(a, 9); }
                    else if (b.FamOut) { b.FamN = 1e12; b.FamAt = 0.0; }
                    if (!(b.FamN > 0.0)) b.FamOut = false;
                    lock (Gate) _barns[item.Substring(0, eq)] = b;
                }
                catch (Exception e) { Stumble("Famine.Import", e); }
            }
            int h = 0;
            lock (Gate) foreach (var b in _barns.Values) if (b.U > 0f) h++;
            _hungry = h;
        }

        private static double Num(string[] a, int i)
        {
            double x;
            if (a == null || i >= a.Length || !double.TryParse(a[i], NumberStyles.Float, Inv, out x) || double.IsInfinity(x) || x < 0.0) return double.NaN;
            return x;
        }
        private static double Num0(string[] a, int i) { double x = Num(a, i); return double.IsNaN(x) ? 0.0 : x; }

        // ------------------------------------------------------------ odczyty dla innych modulow
        /// <summary>Czlon H przyrostu naturalnego (PopulationLaw.GrowthOf, E4/4): brak jedzenia gospodarzy wsi w ostatniej dobie 0..1; 0 bez glodu i przy wylaczonym.
        /// Moze isc z watkow modelu - pod zamkiem, szybka sciezka bez glodnych wsi. Nie rzuca.</summary>
        internal static float HungerOf(Village v)
        {
            if (_hungry == 0 || v == null || v.Settlement == null) return 0f;
            try
            {
                if (!On) return 0f;
                string id = v.Settlement.StringId;
                if (id == null) return 0f;
                lock (Gate) { Barn b; return _barns.TryGetValue(id, out b) ? b.U : 0f; }
            }
            catch (Exception e) { Stumble("Famine.HungerOf", e); return 0f; }
        }

        /// <summary>E4/11 (Z5): czesc konta uchodzcow wsi z glodu przy koncie `away` (ta sama FamPart, z ktorej licza powrot i ReturnGate); 0 bez uchodzcow
        /// z glodu. Moze isc z menu i widoku - pod zamkiem. Nie rzuca.</summary>
        internal static double FamPartOf(Village v, double away)
        {
            if (v == null || v.Settlement == null || v.Settlement.StringId == null || !(away > 0.0)) return 0.0;
            try { lock (Gate) { Barn b; return _barns.TryGetValue(v.Settlement.StringId, out b) && b.FamOut ? FamPart(b, away) : 0.0; } }
            catch (Exception e) { Stumble("Famine.FamPartOf", e); return 0.0; }
        }

        /// <summary>E4/11 (Z5, menu okregu): dni spichlerza okregu dla gospodarzy (racje / ludzie w domu). false - wylaczone, spichlerz nie zalozony albo brak ludzi.
        /// Nie rzuca.</summary>
        internal static bool DaysOf(Village v, out double days)
        {
            days = 0.0;
            if (v == null || v.Settlement == null || v.Settlement.StringId == null) return false;
            try
            {
                if (!On) return false;
                double food;
                lock (Gate) { Barn b; if (!_barns.TryGetValue(v.Settlement.StringId, out b)) return false; food = b.Food; }
                if (double.IsNaN(food) || double.IsInfinity(food)) return false;
                float k = PopulationLaw.KFor(v.Settlement);
                double home = k > 0f ? (double)Math.Max(0f, v.Hearth) * k : 0.0;
                if (!(home > 0.0)) return false;
                days = Math.Max(0.0, food) / home;
                return true;
            }
            catch (Exception e) { Stumble("Famine.DaysOf", e); return false; }
        }

        /// <summary>
        /// Devastation.Strike (rabunek i zerowanie armii), PO ruchu ludzi i kontach: spichlerz okregu traci ten sam ulamek, jaki trafil ludzi (GL 2.2 pkt 6).
        /// Spichlerz jeszcze nie zalozony - nic (zalozy sie od ludzi, ktorzy zostali). Nie rzuca (U24).
        /// </summary>
        internal static void Burn(Village v, double share)
        {
            if (v == null || v.Settlement == null || !(share > 0.0)) return;
            try
            {
                if (!On) return;
                string id = v.Settlement.StringId;
                if (id == null) return;
                double f = Clamp01(share);
                lock (Gate)
                {
                    Barn b;
                    if (!_barns.TryGetValue(id, out b) || double.IsNaN(b.Food) || !(b.Food > 0.0)) return;
                    double lost = b.Food * f;
                    b.Food -= lost; _dBurned += lost; _dBurnN++;
                }
            }
            catch (Exception e) { Stumble("Famine.Burn", e); }
        }

        /// <summary>E7/R1: racje w spichlerzu okregu teraz (niezalezny bilans "zjedzone - spichlerz"). false - wylaczone albo spichlerz nie zalozony. Nie rzuca.</summary>
        internal static bool FoodOf(Village v, out double food)
        {
            food = 0.0;
            if (v == null || v.Settlement == null || v.Settlement.StringId == null) return false;
            try
            {
                if (!On) return false;
                lock (Gate)
                {
                    Barn b;
                    if (!_barns.TryGetValue(v.Settlement.StringId, out b) || double.IsNaN(b.Food) || double.IsInfinity(b.Food)) return false;
                    food = b.Food;
                    return true;
                }
            }
            catch (Exception e) { Stumble("Famine.FoodOf", e); return false; }
        }

        /// <summary>
        /// E7/R1 (K4): napastnicy rabunku osada po osadzie jedza ze spichlerza okregu - zdejmuje calych `pieces` x `perPiece` racji, najwyzej tyle calych porcji, ile
        /// jest w spichlerzu. Zwraca liczbe porcji zdjetych (napastnik dostaje tyle sztuk zboza). Spichlerz nie zalozony albo wylaczony - 0. Nie rzuca.
        /// </summary>
        internal static int Take(Village v, int pieces, double perPiece)
        {
            if (v == null || v.Settlement == null || pieces <= 0 || !(perPiece > 0.0)) return 0;
            try
            {
                if (!On) return 0;
                string id = v.Settlement.StringId;
                if (id == null) return 0;
                lock (Gate)
                {
                    Barn b;
                    if (!_barns.TryGetValue(id, out b) || double.IsNaN(b.Food) || !(b.Food >= perPiece)) return 0;
                    int n = (int)Math.Min(pieces, Math.Floor(b.Food / perPiece));
                    if (n <= 0) return 0;
                    b.Food -= n * perPiece; _dEaten += n * perPiece;
                    return n;
                }
            }
            catch (Exception e) { Stumble("Famine.Take", e); return 0; }
        }

        // ------------------------------------------------------------ r4-mur: danina zbozowa Muru i glod Wolnych Ludzi (WallTribute.cs, FreeFolkRaids.cs)
        /// <summary>r4-mur: migawka spichlerza wsi z ostatniego Daily - ludzie w domu, plon doby (racje, po GranaryFloor KS), racje w spichlerzu teraz, brak jedzenia
        /// gospodarzy (u), sufit spichlerza (racje) i doba migawki. false - wylaczone, spichlerz nie zalozony albo bez migawki. Nie rzuca.</summary>
        internal static bool DayOf(Village v, out double home, out double harvest, out double food, out double cap, out float u, out int day)
        {
            home = harvest = food = cap = 0.0; u = 0f; day = int.MinValue;
            if (v == null || v.Settlement == null || v.Settlement.StringId == null) return false;
            try
            {
                if (!On) return false;
                lock (Gate)
                {
                    Barn b;
                    if (!_barns.TryGetValue(v.Settlement.StringId, out b) || double.IsNaN(b.Food) || double.IsInfinity(b.Food) || b.LastDay == int.MinValue) return false;
                    home = b.LastHome; harvest = b.LastHarvest; food = b.Food; cap = b.LastCap; u = b.U; day = b.LastDay;
                    return true;
                }
            }
            catch (Exception e) { Stumble("Famine.DayOf", e); return false; }
        }

        /// <summary>r4-mur: danina w naturze - zdejmuje ze spichlerza wsi najwyzej `rations` racji (nie wiecej niz lezy). Zwraca, ile zdjeto (0 - wylaczone albo
        /// spichlerz nie zalozony). Miedzy dobami Famine - kontrola spichlerzy liczy od stanu na poczatku Daily, wiec jej nie zaburza. Nie rzuca.</summary>
        internal static double TributeTake(Village v, double rations)
        {
            if (v == null || v.Settlement == null || v.Settlement.StringId == null || !(rations > 0.0) || double.IsInfinity(rations)) return 0.0;
            try
            {
                if (!On) return 0.0;
                lock (Gate)
                {
                    Barn b;
                    if (!_barns.TryGetValue(v.Settlement.StringId, out b) || double.IsNaN(b.Food) || !(b.Food > 0.0)) return 0.0;
                    double t = Math.Min(rations, b.Food);
                    b.Food -= t;
                    return t;
                }
            }
            catch (Exception e) { Stumble("Famine.TributeTake", e); return 0.0; }
        }

        /// <summary>r4-mur: dowieziona danina do spichlerza wsi (racje). false - wylaczone albo spichlerz nie zalozony (wolajacy szuka innej wsi; nic nie ginie).
        /// Ponad sufit spichlerza nadwyzka odejdzie przy nastepnym Daily jak plon (towar na wozy). Nie rzuca.</summary>
        internal static bool TributeGive(Village v, double rations)
        {
            if (v == null || v.Settlement == null || v.Settlement.StringId == null || !(rations > 0.0) || double.IsInfinity(rations)) return false;
            try
            {
                if (!On) return false;
                lock (Gate)
                {
                    Barn b;
                    if (!_barns.TryGetValue(v.Settlement.StringId, out b) || double.IsNaN(b.Food) || double.IsInfinity(b.Food)) return false;
                    b.Food += rations;
                    return true;
                }
            }
            catch (Exception e) { Stumble("Famine.TributeGive", e); return false; }
        }

        /// <summary>
        /// Devastation.Daily (powrot uchodzcow, GL 2.6), `away` = konto wsi teraz: 0 = moga wracac; 1 = wies nie przyjmuje (Accepts: gospodarze gloduja, a dla konta
        /// w innym regionie takze pole nie karmi i spichlerz ponizej FamineReturnMinDays dni dla gospodarzy) - nikt z konta; 2 = siedza w murach oblezonej warowni
        /// regionu pobytu (nikt nie wychodzi). famAway - czesc konta z glodu (reszta: rabunek i zerowanie - rok od ostatniego najazdu, regula E4/5); famDay - doba,
        /// od ktorej wies znow ich przyjmuje (rok czesci z glodu liczony od niej), -1 = czekaja (reszta konta wraca). Wylaczone: czesc z glodu wraca w rok od pierwszej
        /// doby wylaczenia (Daily ja zapisuje), bez bramek. Nie rzuca.
        /// </summary>
        internal static int ReturnGate(Village v, int today, double away, out int famDay, out double famAway)
        {
            famDay = -1; famAway = 0.0;
            try
            {
                if (v == null || v.Settlement == null || v.Settlement.StringId == null) return 0;
                string id = v.Settlement.StringId;
                Barn b; string stay; double food; int rec; float u;
                lock (Gate)
                {
                    if (!_barns.TryGetValue(id, out b)) return 0;
                    stay = b.Stay; food = b.Food; rec = b.Recovered; u = b.U;
                    famAway = b.FamOut ? FamPart(b, away) : 0.0;
                }
                if (!On)
                {
                    if (famAway > 0.0) famDay = rec >= 0 ? rec : today;
                    return 0;
                }
                int gate = 0;
                if (stay != null && ShelterOn)
                {
                    var node = Settlement.Find(stay);
                    if (node != null && node.Town != null && node.IsUnderSiege) gate = 2;
                }
                if (gate == 0 && !double.IsNaN(food))
                {
                    // R1-2: konto we wlasnym regionie je z tych samych spichlerzy regionu - wstrzymuje je tylko glod gospodarzy (pole i spichlerz liczymy
                    // tylko dla konta w innym regionie; FeedNow - wywolanie modelu - tylko wtedy)
                    var s = Settings.Current;
                    double feed = 0.0, home = 0.0;
                    if (stay != null && !(u > ThresholdNow))
                    {
                        float k = PopulationLaw.KFor(v.Settlement);
                        home = k > 0f ? (double)Math.Max(0f, v.Hearth) * k : 0.0;
                        feed = FeedNow(v, s);
                    }
                    if (!Accepts(stay != null, u, feed, food, home, s)) gate = 1;
                }
                if (gate != 0 || famAway > 0.0) _gateTouched.Add(id);      // R2-4: powrot inny niz z samej reguly E4/5 (kolumna konto_e46)
                if (gate != 0) return gate;
                if (famAway > 0.0 && rec >= 0) famDay = rec;
                return 0;
            }
            catch (Exception e) { Stumble("Famine.ReturnGate", e); famAway = 0.0; famDay = -1; return 0; }
        }

        /// <summary>Czesc konta z glodu przy koncie `away` (pod Gate): FamN, a gdy konto zmalalo od synchronizacji - proporcjonalnie mniej (smierc, glod, podloga
        /// 10 schodza z obu czesci po rowno); doplyw rabunku (konto wieksze) FamN nie rusza. Najwyzej cale konto.</summary>
        private static double FamPart(Barn b, double away)
        {
            if (b == null || !(b.FamN > 0.0) || !(away > 0.0)) return 0.0;
            double f = b.FamAt > 0.0 && away < b.FamAt ? b.FamN * (away / b.FamAt) : b.FamN;
            return Math.Min(away, f);
        }

        /// <summary>Devastation.Daily po PeopleBook.RefugeesHome (R1-1): z czesci z glodu famAway wrocilo gotFam - czesc z glodu przy nowym stanie konta.
        /// Pusta czesc - konto bez uchodzcow z glodu (zegar odzyskania od zera). Nie rzuca.</summary>
        internal static void Returned(Village v, double famAway, double gotFam)
        {
            try
            {
                if (v == null || v.Settlement == null || v.Settlement.StringId == null) return;
                double after = PeopleBook.RefugeesOf(v);
                lock (Gate)
                {
                    Barn b;
                    if (!_barns.TryGetValue(v.Settlement.StringId, out b)) return;
                    double f = Math.Min(after, Math.Max(0.0, famAway - Math.Max(0.0, gotFam)));
                    SyncFam(b, f, after);
                }
            }
            catch (Exception e) { Stumble("Famine.Returned", e); }
        }

        /// <summary>Pod Gate: czesc z glodu = f przy koncie `away`; pusta - flaga, zegar odzyskania i czesc od zera.</summary>
        private static void SyncFam(Barn b, double f, double away)
        {
            if (f > 1e-9 && away > 0.0) { b.FamN = f; b.FamAt = away; b.FamOut = true; }
            else { b.FamN = 0.0; b.FamAt = 0.0; b.FamOut = false; b.Recovered = -1; }
        }

        /// <summary>Plon na gospodarza wsi dzis w racjach (pole karmi czlowieka, gdy >= 1): FamineYieldPerPerson x q / mnoznik rak spustoszenia - ile daje pole
        /// kazdemu, kto na nim pracuje (powracajacy dosiewa tyle, ile je). Tylko watek glowny (FoodToday podmienia stan wsi spladrowanej).</summary>
        private static double FeedNow(Village v, Settings s)
        {
            var model = Campaign.Current != null ? Campaign.Current.Models.VillageProductionCalculatorModel : null;
            if (model == null || v == null) return 0.0;
            int level = v.GetHearthLevel();
            double q = level >= 0 ? Devastation.FoodToday(model, v) / (level + 1) * FreeHands.FamineFix(v) : 0.0;     // jak w Daily (przeglad scalenia E4/6 + E4/7)
            if (double.IsNaN(q) || double.IsInfinity(q) || q < 0.0) q = 0.0;
            double yf = Math.Max(1e-3, Devastation.YieldFactor(v));
            return Math.Max(0f, s.FamineYieldPerPerson) * Math.Min(QMax, q) / yf;
        }

        /// <summary>Czy wies przyjmuje swoich uchodzcow (GL 2.6 "nikt nie wraca do glodu"): gospodarze nie gloduja (brak nie ponad prog), a gdy konto przebywa
        /// w INNYM regionie (elsewhere) - takze pole karmi czlowieka (plon na gospodarza >= 1 racji) albo spichlerz ma FamineReturnMinDays dni dla gospodarzy.
        /// Poprawka po recenzji E4/6 (R1-2): konto we wlasnym regionie je z tych samych spichlerzy regionu (po gospodarzach) - powrot doklada rece do plonu
        /// i nie doklada gab regionowi, wiec wstrzymuje go tylko glod gospodarzy.</summary>
        private static bool Accepts(bool elsewhere, float u, double feed, double food, double home, Settings s)
        {
            if (u > ThresholdNow) return false;
            if (!elsewhere) return true;
            return feed >= 1.0 || food / Math.Max(1.0, home) >= Math.Max(0, s.FamineReturnMinDays);
        }

        /// <summary>Postfiks na DefaultSettlementFoodModel.CalculateTownFoodStocksChange: uchodzcy schronieni w murach oblezonej warowni jedza z jej Food
        /// (stan z ostatniej doby Famine.Daily). Moze isc z watkow AI - pod zamkiem, szybka sciezka bez schronionych. Nie rzuca.</summary>
        public static void ShelterPostfix(Town town, ref ExplainedNumber __result)
        {
            if (_shelterN == 0 || town == null) return;
            try
            {
                if (!town.IsUnderSiege) return;
                double u;
                lock (Gate) if (!_shelterUnits.TryGetValue(town, out u)) return;
                if (u > 0.0 && !double.IsInfinity(u)) __result.Add(-(float)u, _txtShelter);
            }
            catch (Exception e) { Stumble("Famine.ShelterPostfix", e); }
        }

        // ------------------------------------------------------------ doba
        private sealed class Vil
        {
            public Village V; public Barn B; public Settlement Node; public string Id;
            public double K, Home, Away, L, Q, Feed, Harvest, Cap, Food0, Eat, Rem, Taken, U;
            public bool Rationed, Winter;
        }
        private sealed class Reg
        {
            public Settlement Node; public readonly List<Vil> Vs = new List<Vil>();
            public double Hosts, Present, Harvest, Food0, Food, EatHosts, EatRef, URef, Dead, Fled, HostsHungry;
            public bool Rationed;
            public double Days { get { double n = Hosts + Present; return n > 1e-9 ? Food / n : (Food > 0.0 ? double.MaxValue : 0.0); } }
        }
        private sealed class Guest { public Vil Acc; public double Away; public Reg R; public Town Shelter; }

        private static int WinterDay()
        {
            try
            {
                if (!WinterBite.WinterNow()) { _winterCount = 0; _firstStarve = -1; return 0; }
                _winterCount++;
                int season, sday, len;
                if (WesterosClimate.SeasonInfo(out season, out sday, out len) && season == (int)CampaignTime.Seasons.Winter && sday > 0) return sday;
                return _winterCount;
            }
            catch (Exception e) { Stumble("Famine.WinterDay", e); return 0; }
        }

        /// <summary>Mieszczan na jednostke Food warowni: miasto - ludzie ksiegi / (dobrobyt / 40); zamek albo brak danych - Q swiata (GL 2.3).</summary>
        private static double QOf(Town t, double qWorld)
        {
            try
            {
                if (t == null || t.Settlement == null || !t.Settlement.IsTown) return qWorld;
                double units = Math.Max(0f, t.Prosperity) / 40.0;
                double people = PopulationLaw.PeopleOf(t.Settlement);
                return units > 0.0 && people > 0.0 ? people / units : qWorld;
            }
            catch (Exception e) { Stumble("Famine.QOf", e); return qWorld; }
        }

        private static double WorldQ()
        {
            double people = 0, units = 0;
            foreach (var t in Town.AllTowns)
            {
                try
                {
                    if (t == null || t.Settlement == null) continue;
                    double p = PopulationLaw.PeopleOf(t.Settlement);
                    if (!(p > 0.0)) continue;
                    people += p; units += Math.Max(0f, t.Prosperity) / 40.0;
                }
                catch (Exception e) { Stumble("Famine.WorldQ", e); }
            }
            return units > 0.0 && people > 0.0 ? people / units : QWorldDefault;
        }

        /// <summary>
        /// Raz na dobe (ArmouryBehavior.OnDailyTick, PO Devastation.Daily - spustoszenie i powrot uchodzcow tej doby - PRZED ksiega ludzi, ktora zamyka dobe konta
        /// UCHODZCY): plon, jedzenie, spichlerze, glod, zgony, uchodzcy z glodu, warownie przy Food 0, linia "Spichlerze (E4/6)" i plik spichlerze-regiony.csv.
        /// </summary>
        internal static void Daily()
        {
            long t0 = Stopwatch.GetTimestamp();
            var s = Settings.Current;
            if (s == null || Campaign.Current == null) return;
            int today = (int)CampaignTime.Now.ToDays, day = today - 1;
            int winterDay = WinterDay();
            if (!On) { OffDay(s, today, day); return; }
            _offLogged = false;
            try { PopulationLaw.EnsureCalibrated(); } catch (Exception e) { Stumble("Famine.Daily(kalibracja)", e); }
            int yd = YearDays();
            double yield = Math.Max(0f, s.FamineYieldPerPerson), years = Math.Max(0.05f, s.FamineBarnYears), start = Clamp01(s.FamineStartShare);
            double rationDays = Math.Max(0, s.FamineRationDays), T = ThresholdNow, dmax = Clamp01(s.FamineDeathMaxPercent / 100.0);
            var model = Campaign.Current.Models.VillageProductionCalculatorModel;

            // 1. wsie: ludzie, plon, sufit, spichlerz (zalozenie przy pierwszej dobie)
            var vils = new List<Vil>(600);
            var byId = new Dictionary<string, Vil>(600);
            var regs = new Dictionary<Settlement, Reg>();
            int noK = 0, seeded = 0, winterV = 0;
            double seededFood = 0, burned, eaten, wq = 0, wl = 0;
            int burnN;
            lock (Gate) { burned = _dBurned; burnN = _dBurnN; eaten = _dEaten; _dBurned = 0; _dBurnN = 0; _dEaten = 0; }
            var seen = new HashSet<Village>();
            foreach (var st in Settlement.All)
            {
                if (st == null || !st.IsVillage || st.Village == null || !seen.Add(st.Village)) continue;   // ROT podaje dwie wsie dwa razy - kazda raz
                try
                {
                    var v = st.Village;
                    float k = PopulationLaw.KFor(st);
                    if (!(k > 0f) || float.IsInfinity(k) || st.StringId == null) { noK++; continue; }
                    var x = new Vil { V = v, Id = st.StringId, K = k };
                    x.Home = (double)Math.Max(0f, v.Hearth) * k;
                    x.Away = PeopleBook.RefugeesOf(v);
                    x.L = x.Home + x.Away;
                    int level = v.GetHearthLevel();
                    double f = model != null ? Devastation.FoodToday(model, v) : 0.0;
                    // przeglad scalenia E4/6 + E4/7: plon idzie za ludzmi (x.L), wiec w q czynnik rak E4/7 bez zmarlych cywilow i narodzin (FreeHands.FamineFix)
                    double q = level >= 0 ? f / (level + 1) * FreeHands.FamineFix(v) : 0.0;
                    x.Q = double.IsNaN(q) || double.IsInfinity(q) || q < 0.0 ? 0.0 : Math.Min(QMax, q);
                    x.Harvest = yield * x.L * x.Q;
                    x.Harvest = StartCalibration.GranaryFloor(v, x.Harvest, x.L, BarnFoodOf(x.Id));   // KS (rozdz. 8): wies zastosowana - max(racje z czesci domowej KS, wzor dzis); r4-bkwies G1: ludzie i zapas spichlerza (podloga awaryjna)
                    x.Feed = yield * x.Q / Math.Max(1e-3, Devastation.YieldFactor(v));    // plon na gospodarza (bramka powrotu)
                    x.Cap = years * yd * x.L;
                    x.Winter = WinterBite.VillageCut(v) > 0f;
                    if (x.Winter) winterV++;
                    wq += x.Q * x.L; wl += x.L;
                    lock (Gate)
                    {
                        Barn b;
                        if (!_barns.TryGetValue(x.Id, out b)) _barns[x.Id] = b = new Barn();
                        if (double.IsNaN(b.Food) || double.IsInfinity(b.Food) || b.Food < 0.0) { b.Food = start * x.Cap; seeded++; seededFood += b.Food; }
                        x.B = b;
                        b.LastHome = x.Home; b.LastHarvest = x.Harvest; b.LastCap = x.Cap; b.LastDay = today;   // r4-mur: migawka doby (tylko odczyt WallTribute / FreeFolkRaids)
                    }
                    x.Node = OutlawLaw.RegionFor(st) ?? st;
                    Reg r;
                    if (!regs.TryGetValue(x.Node, out r)) regs[x.Node] = r = new Reg { Node = x.Node };
                    r.Vs.Add(x); r.Hosts += x.Home; r.Harvest += x.Harvest;
                    vils.Add(x); byId[x.Id] = x;
                }
                catch (Exception e) { Stumble("Famine.Daily(wies)", e); }
            }
            // R2-4: regiony POCHODZENIA, ktorych konto uchodzcow E4/6 dzis zmienil (brama i czesc z glodu w powrocie, zgony i ucieczki z glodu) - kolumna konto_e46
            var e46 = new HashSet<string>();
            foreach (var id in _gateTouched) { Vil gx; if (byId.TryGetValue(id, out gx)) Touch(e46, gx); }
            _gateTouched.Clear();

            // 2. gospodarze jedza pierwsi: swoj spichlerz + plon dnia; przednowek - 90%
            double food0 = 0, harvestAll = 0, eatHosts = 0, hostsAll = 0;
            foreach (var x in vils)
            {
                double home = x.Home;
                x.Food0 = x.B.Food;
                food0 += x.Food0; harvestAll += x.Harvest; hostsAll += home;
                double avail = x.Food0 + x.Harvest;
                x.Rationed = home > 0.0 && x.Food0 < rationDays * home && x.Harvest < home;
                double eat = home * (x.Rationed ? 0.9 : 1.0);
                double got = Math.Min(eat, avail);
                if (got < 0.0) got = 0.0;
                x.Eat = got; x.Rem = Math.Max(0.0, avail - got);
                x.U = home > 0.0 ? Clamp01(1.0 - got / home) : 0.0;
                eatHosts += got;
                var r = regs[x.Node];
                r.Food0 += x.Food0; r.EatHosts += got;
            }

            // 3. uchodzcy obecni w regionach (miejsce pobytu konta; oblezona warownia - w murach) jedza po gospodarzach z tego, co zostalo w regionie
            var accounts = new List<KeyValuePair<string, double>>();
            PeopleBook.RefugeesSnapshot(accounts);
            var nodeById = new Dictionary<string, Settlement>();
            foreach (var n in OutlawLaw.RegionNodes()) if (n != null && n.StringId != null) nodeById[n.StringId] = n;
            bool shelterOn = ShelterOn;
            var guests = new List<Guest>(accounts.Count);
            var shelter = new Dictionary<Town, double>();
            int lostAcc = 0;
            foreach (var kv in accounts)
            {
                try
                {
                    Vil x;
                    if (!byId.TryGetValue(kv.Key, out x)) { lostAcc++; continue; }
                    var stay = StayNode(x, nodeById);
                    var g = new Guest { Acc = x, Away = kv.Value };
                    if (shelterOn && stay.Town != null && stay.IsUnderSiege)
                    {
                        g.Shelter = stay.Town;
                        double v0; shelter.TryGetValue(stay.Town, out v0); shelter[stay.Town] = v0 + kv.Value;
                    }
                    else
                    {
                        Reg r;
                        if (!regs.TryGetValue(stay, out r)) regs[stay] = r = new Reg { Node = stay };
                        r.Present += kv.Value; g.R = r;
                    }
                    guests.Add(g);
                }
                catch (Exception e) { Stumble("Famine.Daily(konto)", e); }
            }
            double eatRef = 0, presentAll = 0;
            foreach (var r in regs.Values)
            {
                if (!(r.Present > 0.0)) continue;
                presentAll += r.Present;
                double pool = 0; foreach (var x in r.Vs) pool += x.Rem;
                r.Rationed = r.Food0 < rationDays * (r.Hosts + r.Present) && r.Harvest < r.Hosts + r.Present;
                double need = r.Present * (r.Rationed ? 0.9 : 1.0);
                double got = Math.Min(need, pool);
                double fr = pool > 0.0 ? got / pool : 0.0;
                foreach (var x in r.Vs) x.Taken = x.Rem * fr;
                r.EatRef = got; eatRef += got;
                r.URef = Clamp01(1.0 - got / r.Present);
            }

            // 4. spichlerze: reszta (najwyzej sufit; nadwyzka to towar na wozy, jak dotad)
            double food1 = 0, overflow = 0;
            foreach (var x in vils)
            {
                double left = Math.Max(0.0, x.Rem - x.Taken);
                double cap = Math.Max(0.0, x.Cap);
                if (left > cap) { overflow += left - cap; left = cap; }
                lock (Gate) x.B.Food = left;
                food1 += left;
                regs[x.Node].Food += left;
            }

            // 5. glod gospodarzy: smierc i uchodzcy z glodu (tylu, ilu umiera)
            bool fleeOn = FleeOn;
            int rationedV = 0, hungryV = 0, hungerSet = 0, fledV = 0, moves = 0, noRefuge = 0, blocked = 0, moveFail = 0;
            double deadHosts = 0, fled = 0, uSum = 0;
            Vil worst = null;
            foreach (var x in vils)
            {
                try
                {
                    float u = x.U > 0.0001 ? (float)x.U : 0f;
                    lock (Gate) x.B.U = u;
                    if (u > 0f) hungerSet++;
                    if (x.Rationed) rationedV++;
                    if (!(x.U > T) || !(x.Home > 0.0)) continue;
                    hungryV++; uSum += x.U;
                    regs[x.Node].HostsHungry++;
                    if (worst == null || x.U > worst.U) worst = x;
                    // R2-2: nowy glod wsi = nowe zdarzenie - rok czesci konta z glodu liczy sie od dnia, w ktorym wies znow przyjmie (takze gdy nikt nie
                    // ucieknie - brak regionu z zapasem); sama zima bez glodu (brama) zegara nie zeruje - wzor dogania jak w E4/5
                    if (x.B.FamOut) lock (Gate) x.B.Recovered = -1;
                    double d = x.Home * DeathRate(x.U, T, dmax) / yd;
                    if (!(d > 0.0)) continue;
                    double h = -PeopleBook.Move(x.V, PeopleBook.ADom, PeopleBook.ADead, d, PeopleBook.MFamineDead, PeopleUnit.GamePerMan);
                    if (!(h > 0.0)) { moveFail++; continue; }
                    deadHosts += d;
                    // scalenie E4/6 + E4/7: zgon z glodu zdjety z DOM przez Move poza modelem hearth (jak zabici MDevRaid) - cywile regionu (K_m, rownanie krain);
                    // czlon H przyrostu (NoteBorn) to inna liczba - bez podwojenia
                    if (x.Node != null && !x.Node.IsVillage) PeopleBook.NoteCivilDead(x.Node, d);
                    lock (Gate) x.B.Starved += d;
                    regs[x.Node].Dead += d;
                    if (fleeOn && TryFlee(x, d, today, regs, nodeById, s, ref moves, ref noRefuge, ref blocked, ref moveFail)) { fled += d; fledV++; regs[x.Node].Fled += d; Touch(e46, x); }
                }
                catch (Exception e) { Stumble("Famine.Daily(gospodarze)", e); }
            }
            _hungry = hungerSet;

            // 6. glod uchodzcow obecnych w regionach: smierc z konta i przeprowadzka konta (najwyzej raz na FamineMoveDays)
            double deadRef = 0; int refHungry = 0;
            foreach (var g in guests)
            {
                try
                {
                    if (g.R == null || !(g.Away > 0.0) || !(g.R.URef > T)) continue;
                    refHungry++;
                    double d = g.Away * DeathRate(g.R.URef, T, dmax) / yd;
                    double got = d > 0.0 ? PeopleBook.RefugeesDie(g.Acc.V, d) : 0.0;
                    if (got > 0.0) { deadRef += got; lock (Gate) g.Acc.B.Starved += got; g.R.Dead += got; Touch(e46, g.Acc); }
                    if (fleeOn) TryRelocate(g, today, regs, nodeById, s, ref moves, ref noRefuge, ref blocked);
                }
                catch (Exception e) { Stumble("Famine.Daily(uchodzcy)", e); }
            }

            // 7. warownie: schronieni w murach (jutro w modelu Food), przy Food 0 gina mieszczanie i schronieni
            double qWorld = WorldQ();
            var shelterUnits = new Dictionary<Town, double>();
            double shelterPeople = 0;
            foreach (var kv in shelter)
            {
                double q = QOf(kv.Key, qWorld);
                if (q > 0.0) shelterUnits[kv.Key] = kv.Value / q;
                shelterPeople += kv.Value;
            }
            lock (Gate) { _shelterUnits.Clear(); foreach (var kv in shelterUnits) _shelterUnits[kv.Key] = kv.Value; _shelterN = _shelterUnits.Count; }
            double deadTown = 0, deadShelter = 0, shelterFood = 0; int starvingAll = 0, starvingAi = 0, townDeathsN = 0;
            foreach (var kv in shelterUnits) shelterFood += kv.Value;
            var starvingNames = new List<string>();
            foreach (var node in nodeById.Values)
            {
                try
                {
                    var t = node.Town;
                    if (t == null || !node.IsStarving) continue;
                    starvingAll++;
                    if (node.OwnerClan != Clan.PlayerClan) starvingAi++;
                    if (starvingNames.Count < Top) starvingNames.Add(NameOf(node));
                    double su; shelterUnits.TryGetValue(t, out su);
                    var gp = t.GarrisonParty;
                    double cons = Math.Max(0f, t.Prosperity) / 40.0 + (gp != null && gp.MemberRoster != null ? gp.MemberRoster.TotalManCount : 0) / 20.0 + su;
                    double deficit = Math.Max(0.0, -(double)t.FoodChange);
                    double uf = cons > 0.0 ? Clamp01(deficit / cons) : 0.0;
                    if (!(uf > T)) continue;
                    double rate = DeathRate(uf, T, dmax) / yd;
                    if (node.IsTown)
                    {
                        float p;
                        if (PeopleBook.TryTown(node, out p) && p > 0f)
                        {
                            double got = PeopleBook.TownDie(node, p * rate);     // R1-5/R2-3: TownDie liczy ich w ksiedze (MFamineTown)
                            if (got > 0.0)
                            {
                                deadTown += got; townDeathsN++;
                                lock (Gate) { double t0s; _townStarved.TryGetValue(node.StringId, out t0s); _townStarved[node.StringId] = t0s + got; }
                            }
                        }
                    }
                    foreach (var g in guests)
                    {
                        if (g.Shelter != t || !(g.Away > 0.0)) continue;
                        double got = PeopleBook.RefugeesDie(g.Acc.V, g.Away * rate);
                        if (got > 0.0) { deadShelter += got; lock (Gate) g.Acc.B.Starved += got; Touch(e46, g.Acc); }
                    }
                }
                catch (Exception e) { Stumble("Famine.Daily(warownia)", e); }
            }
            if (winterDay > 0 && starvingAi > 0 && _firstStarve < 0) _firstStarve = winterDay;

            // 8. konta: czesc z glodu wraca od dnia, w ktorym wies znow przyjmuje; puste konto - miejsce pobytu i zegary od zera
            var stays = new Dictionary<string, double>();
            double famOutPeople = 0; int famOutV = 0, waitFood = 0;
            CleanAccounts(vils, today, s, stays, e46, ref famOutPeople, ref famOutV, ref waitFood);

            _sessDead += deadHosts + deadRef + deadTown + deadShelter;
            _sessFled += fled;

            // 9. komunikat gracza: glod w jego wlasnym regionie (raz na glod)
            try { WarnPlayer(regs, T); } catch (Exception e) { Stumble("Famine.WarnPlayer", e); }

            // 10. linia i plik regionow
            if (!s.LogEnabled) return;
            try
            {
                var sb = new StringBuilder(3200);
                double eaters = hostsAll + presentAll + shelterPeople;
                double control = food1 - (food0 + harvestAll - eatHosts - eatRef - overflow);
                sb.Append("Spichlerze (E4/6): dzien ").Append(day).Append(" | CZYNNE");
                sb.Append(" | wsie ").Append(vils.Count).Append(" (bez przelicznika ").Append(noK).Append("), regiony z wsiami ").Append(regs.Values.Count(r => r.Vs.Count > 0));
                sb.Append(" | spichlerze swiata ").Append(M(food1)).Append(" mln racji = ").Append(F(eaters > 0 ? food1 / eaters : 0, "0")).Append(" dni potrzeby (gospodarze ")
                  .Append(M(hostsAll)).Append(" mln, uchodzcy obecni ").Append(F0(presentAll)).Append(", w murach ").Append(F0(shelterPeople)).Append(')');
                if (seeded > 0) sb.Append(", zalozone dzis ").Append(seeded).Append(" (").Append(M(seededFood)).Append(" mln racji = ").Append(F(100.0 * start, "0")).Append("% sufitu)");
                sb.Append(" | plon dzis ").Append(M(harvestAll)).Append(" mln racji = ").Append(F(hostsAll + presentAll > 0 ? 100.0 * harvestAll / (hostsAll + presentAll) : 0, "0.0"))
                  .Append("% potrzeby (q srednio ").Append(F(wl > 0 ? wq / wl : 0, "0.000")).Append(" x ").Append(F(yield, "0.##")).Append("; zima tnie plon ").Append(winterV).Append(" wsi)");
                sb.Append(" | zjedli: gospodarze ").Append(M(eatHosts)).Append(" mln, uchodzcy ").Append(F0(eatRef)).Append(" | nadwyzka ponad sufit ").Append(M(overflow))
                  .Append(" mln | spalone przy spustoszeniu od wczoraj ").Append(F0(burned)).Append(" racji (").Append(burnN).Append(" razy)").Append(", zjedzone przez napastnikow (E7/R1) ").Append(F0(eaten)).Append(" racji")
                  .Append(" | kontrola spichlerzy ").Append(Sg(control, "0.###"));
                sb.Append(" | przednowek (90%): wsi ").Append(rationedV).Append(", regionow z uchodzcami ").Append(regs.Values.Count(r => r.Present > 0.0 && r.Rationed));
                sb.Append(" | glod: wsi ").Append(hungryV);
                if (hungryV > 0) sb.Append(" (brak srednio ").Append(F(100.0 * uSum / hungryV, "0.#")).Append("%, najgorsza ").Append(NameOf(worst.V.Settlement)).Append(' ').Append(F(100.0 * worst.U, "0.#")).Append("%)");
                sb.Append(", regionow z glodem uchodzcow ").Append(regs.Values.Count(r => r.Present > 0.0 && r.URef > T)).Append(" (kont ").Append(refHungry).Append(')');
                // okregi glodne wedlug przyczyny (bieg zwykly: tylko spustoszone albo oblezone)
                int hDev = 0, hSiege = 0, hWinter = 0, hNone = 0;
                foreach (var x in vils)
                {
                    if (!(x.U > T)) continue;
                    // R1-3: spustoszenie = stan wsi, rabunek w regionie w ciagu roku albo uchodzcy INNI niz z glodu na koncie (wlasni uchodzcy z glodu
                    // spustoszeniem nie sa - inaczej glod bez przyczyny od drugiej doby liczylby sie jako "spustoszone")
                    double acc = PeopleBook.RefugeesOf(x.V), famAcc;
                    lock (Gate) famAcc = x.B.FamOut ? FamPart(x.B, acc) : 0.0;
                    bool dev = acc - famAcc > 0.5 || x.V.VillageState != Village.VillageStates.Normal || StruckWithinYear(x.Node, today);
                    if (x.Node != null && x.Node.IsUnderSiege) hSiege++;
                    else if (dev) hDev++;
                    else if (x.Winter) hWinter++;
                    else hNone++;
                }
                sb.Append(" | okregi glodne ").Append(hungryV).Append(": oblezone ").Append(hSiege).Append(", spustoszone ").Append(hDev).Append(", tylko zima ").Append(hWinter)
                  .Append(", bez przyczyny ").Append(hNone);
                sb.Append(" | zmarli z glodu: gospodarze ").Append(F(deadHosts, "0.##")).Append(", uchodzcy ").Append(F(deadRef, "0.##")).Append(", mieszczanie ").Append(F(deadTown, "0.##"))
                  .Append(" (miast ").Append(townDeathsN).Append("), schronieni w murach ").Append(F(deadShelter, "0.##")).Append("; od wczytania ").Append(F0(_sessDead));
                double allVil = 0, allTown = 0, allFled = 0;
                lock (Gate) { foreach (var b in _barns.Values) { allVil += b.Starved; allFled += b.Fled; } foreach (var t in _townStarved.Values) allTown += t; }
                sb.Append("; od poczatku kampanii (zapis): ze wsi ").Append(F0(allVil)).Append(" (gospodarze i ich uchodzcy), z miast ").Append(F0(allTown))
                  .Append(", uciekli ").Append(F0(allFled));
                sb.Append(" | uciekli z glodu ").Append(F(fled, "0.##")).Append(" ludzi z ").Append(fledV).Append(" wsi").Append(fleeOn ? "" : (s.FamineFleeEnabled ? " [WYLACZONE - wymaga Devastation Enabled]" : " [WYLACZONE w MCM]"))
                  .Append(" (przeprowadzki kont ").Append(moves).Append("; bez regionu z zapasem w zasiegu ").Append(noRefuge).Append(", wstrzymane (przeprowadzka raz na ").Append(Math.Max(0, s.FamineMoveDays))
                  .Append(" dob albo konto w murach oblezonej warowni) ").Append(blocked).Append(", Move nie przyjal ").Append(moveFail).Append("); od wczytania ").Append(F0(_sessFled));
                sb.Append(" | uchodzcy z glodu na kontach ").Append(F0(famOutPeople)).Append(" ludzi w ").Append(famOutV).Append(" wsiach (reszta kont - rok od najazdu), powrot czeka, az wies przyjmie (gospodarze bez glodu; konto w innym regionie - takze pole karmi albo spichlerz >= ")
                  .Append(Math.Max(0, s.FamineReturnMinDays)).Append(" dni dla gospodarzy) ").Append(waitFood).Append(" wsi");
                if (stays.Count > 0)
                {
                    var parts = stays.OrderByDescending(kv => kv.Value).Take(Top).Select(kv => { Settlement n; return (nodeById.TryGetValue(kv.Key, out n) ? NameOf(n) : kv.Key) + " " + F0(kv.Value); });
                    sb.Append("; miejsca pobytu poza swoim regionem (").Append(stays.Count).Append("): ").Append(string.Join(", ", parts.ToArray()));
                }
                sb.Append(" | w murach oblezonych warowni ").Append(F0(shelterPeople)).Append(" uchodzcow w ").Append(shelter.Count).Append(" warowniach (Food -").Append(F(shelterFood, "0.0"))
                  .Append("/d)").Append(shelterOn ? "" : (s.FamineSiegeShelter ? " [latka nie wpieta]" : " [WYLACZONE w MCM]"));
                sb.Append(" | warownie przy Food 0: ").Append(starvingAll).Append(" (AI ").Append(starvingAi).Append(')');
                if (starvingNames.Count > 0) sb.Append(" - ").Append(string.Join(", ", starvingNames.ToArray()));
                StockPart(sb, nodeById, regs, shelter, qWorld, day, e46);
                sb.Append(" | zima: ").Append(winterDay > 0 ? "TAK, doba zimy " + winterDay : "NIE").Append(", pierwsza doba glodu warowni AI w tej zimie: ").Append(_firstStarve >= 0 ? _firstStarve.ToString(Inv) : "-");
                if (lostAcc > 0) sb.Append(" | konta uchodzcow bez wsi z przelicznikiem ").Append(lostAcc);
                // R2-1: potkniecia od poprzedniej linii (takze ReturnGate, Burn, HungerOf, postfiks i Import miedzy dobami), nie tylko z samego Daily
                int all = _stumblesAll, since = all - _stumblesMark;
                _stumblesMark = all;
                sb.Append(" | potkniecia ").Append(since).Append(" (razem ").Append(all).Append(')');
                sb.Append(" | czas ").Append(((Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency).ToString("0.0", Inv)).Append(" ms.");
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Stumble("Famine.Daily(linia)", e); }
        }

        private static Settlement StayNode(Vil x, Dictionary<string, Settlement> nodeById)
        {
            Settlement n;
            if (x.B.Stay != null && nodeById.TryGetValue(x.B.Stay, out n) && n != null) return n;
            return x.Node;
        }

        private static bool StruckWithinYear(Settlement region, int today)
        {
            int last;
            return region != null && PeopleBook.LastStrike(region, out last) && today - last < YearDays();
        }

        /// <summary>
        /// Najblizszy region (warownia z wsiami), ktory przyjmie uchodzcow z glodu: nieoblezony, w promieniu FamineRefugeRadius od `from`, spichlerze ponad
        /// FamineRefugeMinDays dni dla swoich i gosci; pierwszenstwo krolestwa `fac`, potem krolestwa z nim niewalczacego (GL 2.6). null = brak.
        /// </summary>
        private static Settlement FindRefuge(Settlement from, IFaction fac, Dictionary<Settlement, Reg> regs, Settings s, Settlement skip1, Settlement skip2)
        {
            if (from == null) return null;
            double minDays = Math.Max(0, s.FamineRefugeMinDays), rad = Math.Max(0f, s.FamineRefugeRadius);
            double rad2 = rad * rad;
            var p = from.GetPosition2D;
            Settlement best = null; double bd = double.MaxValue; bool bestSame = false;
            foreach (var r in regs.Values)
            {
                var n = r.Node;
                if (n == null || n == skip1 || n == skip2 || r.Vs.Count == 0 || !(n.IsTown || n.IsCastle) || n.IsUnderSiege) continue;
                double d2 = p.DistanceSquared(n.GetPosition2D);
                if (d2 > rad2) continue;
                var nf = n.MapFaction;
                bool same = fac != null && nf == fac;
                if (!same && fac != null && nf != null && fac.IsAtWarWith(nf)) continue;
                if (!(r.Days > minDays)) continue;
                if (best == null || (same && !bestSame) || (same == bestSame && d2 < bd)) { best = n; bd = d2; bestSame = same; }
            }
            return best;
        }

        /// <summary>
        /// Gospodarze wsi x, ktorzy umieraja z glodu, maja tylu towarzyszy w drodze (GL 2.6): n ludzi DOM -> UCHODZCY (PeopleBook.Move MFamineFled, potem konto).
        /// Konto ma juz miejsce poza domem, ktore je karmi - dolaczaja tam; inaczej szukamy regionu (przeprowadzka calego konta - najwyzej raz na FamineMoveDays,
        /// gdy na koncie juz ktos jest). Brak regionu - zostaja. true = ruszyli. Miedzy Move a kontem nic nie rzuca (U24).
        /// </summary>
        private static bool TryFlee(Vil x, double n, int today, Dictionary<Settlement, Reg> regs, Dictionary<string, Settlement> nodeById, Settings s,
                                    ref int moves, ref int noRefuge, ref int blocked, ref int moveFail)
        {
            var b = x.B; var home = x.Node;
            var cur = StayNode(x, nodeById);
            double away = PeopleBook.RefugeesOf(x.V);
            Settlement target;
            Reg cr;
            regs.TryGetValue(cur, out cr);
            bool join = cur != home && cr != null && cr.Vs.Count > 0 && !cur.IsUnderSiege && cr.Days >= Math.Max(0, s.FamineReturnMinDays) && !(cr.URef > ThresholdNow);
            if (join) target = cur;
            else
            {
                if (away > 0.0 && cur.IsUnderSiege) { blocked++; return false; }      // konto w murach oblezonej warowni - nikt nie wychodzi (GL 2.6)
                if (away > 0.0 && b.MoveDay != int.MinValue && today - b.MoveDay < Math.Max(0, s.FamineMoveDays)) { blocked++; return false; }
                target = FindRefuge(home, home != null ? home.MapFaction : null, regs, s, home, cur);
                if (target == null) { noRefuge++; return false; }
            }
            double h = -PeopleBook.Move(x.V, PeopleBook.ADom, PeopleBook.ARefugees, n, PeopleBook.MFamineFled, PeopleUnit.GamePerMan);
            if (!(h > 0.0)) { moveFail++; return false; }
            PeopleBook.FamineFled(x.V, n);
            Reg tr; regs.TryGetValue(target, out tr);
            if (tr != null) tr.Present += n;
            if (target != cur)
            {
                if (away > 0.0) { if (cr != null) cr.Present = Math.Max(0.0, cr.Present - away); if (tr != null) tr.Present += away; }
                moves++;
                lock (Gate) { b.Stay = target == home ? null : target.StringId; b.MoveDay = today; }
            }
            lock (Gate)
            {
                double f = b.FamOut ? FamPart(b, away) : 0.0;     // czesc z glodu przed dzisiejsza ucieczka (konto `away` sprzed Move)
                SyncFam(b, f + n, away + n);
                b.Recovered = -1; b.Fled += n;
            }
            // E4/11 (Z4, krytyka K8): opuszczone osady wiosek pobocznych - na koncu udanej sciezki, po kontach i FamN (U24); wlasny try, nie rzuca
            try { VillageBook.OnFamineFled(x.V, n); } catch (Exception e) { Stumble("Famine.TryFlee(E4/11)", e); }
            return true;
        }

        /// <summary>R2-4: region pochodzenia wsi x do kolumny konto_e46 (E4/6 zmienil dzis konto uchodzcow regionu).</summary>
        private static void Touch(HashSet<string> e46, Vil x)
        {
            if (e46 != null && x != null && x.Node != null && x.Node.StringId != null) e46.Add(x.Node.StringId);
        }

        /// <summary>Uchodzcy gloduja w regionie pobytu: cale konto przenosi sie do regionu z zapasem (najwyzej raz na FamineMoveDays). Brak - zostaja.</summary>
        private static void TryRelocate(Guest g, int today, Dictionary<Settlement, Reg> regs, Dictionary<string, Settlement> nodeById, Settings s,
                                        ref int moves, ref int noRefuge, ref int blocked)
        {
            var b = g.Acc.B;
            if (b.MoveDay != int.MinValue && today - b.MoveDay < Math.Max(0, s.FamineMoveDays)) { blocked++; return; }
            var from = g.R.Node;
            var home = g.Acc.Node;
            var target = FindRefuge(from, home != null ? home.MapFaction : null, regs, s, from, null);
            if (target == null) { noRefuge++; return; }
            g.R.Present = Math.Max(0.0, g.R.Present - g.Away);
            Reg tr; if (regs.TryGetValue(target, out tr)) tr.Present += g.Away;
            lock (Gate) { b.Stay = target == home ? null : target.StringId; b.MoveDay = today; }
            moves++;
        }

        /// <summary>Konta po dobie: puste - miejsce pobytu, przeprowadzka i zegary od zera; czesc z glodu - synchronizacja przy dzisiejszym koncie (FamPart) i doba,
        /// od ktorej ich wies znow przyjmuje (Accepts).</summary>
        private static void CleanAccounts(List<Vil> vils, int today, Settings s, Dictionary<string, double> stays, HashSet<string> e46,
                                          ref double famOutPeople, ref int famOutV, ref int waitFood)
        {
            foreach (var x in vils)
            {
                try
                {
                    var b = x.B;
                    if (!b.FamOut && b.Stay == null && b.Recovered < 0 && b.MoveDay == int.MinValue) continue;
                    double away = PeopleBook.RefugeesOf(x.V);
                    if (!(away > 0.0))
                    {
                        lock (Gate) { b.FamOut = false; b.Stay = null; b.Recovered = -1; b.MoveDay = int.MinValue; b.FamN = 0.0; b.FamAt = 0.0; }
                        continue;
                    }
                    if (b.FamOut)
                    {
                        double f;
                        lock (Gate) { f = FamPart(b, away); SyncFam(b, f, away); }
                    }
                    if (b.FamOut)
                    {
                        famOutPeople += b.FamN; famOutV++;
                        Touch(e46, x);
                        if (b.Recovered < 0)
                        {
                            double home = (double)Math.Max(0f, x.V.Hearth) * x.K;
                            if (Accepts(b.Stay != null, b.U, x.Feed, b.Food, home, s)) { lock (Gate) b.Recovered = today; }
                            else waitFood++;
                        }
                    }
                    if (b.Stay != null) { double v0; stays.TryGetValue(b.Stay, out v0); stays[b.Stay] = v0 + away; }
                }
                catch (Exception e) { Stumble("Famine.CleanAccounts", e); }
            }
        }

        private static void WarnPlayer(Dictionary<Settlement, Reg> regs, double t)
        {
            var pc = Clan.PlayerClan;
            if (pc == null) return;
            foreach (var r in regs.Values)
            {
                var n = r.Node;
                if (n == null || n.StringId == null) continue;
                bool hungry = r.HostsHungry > 0 || (r.Present > 0.0 && r.URef > t);
                if (!hungry) { _warned.Remove(n.StringId); continue; }
                if (n.OwnerClan != pc || !_warned.Add(n.StringId)) continue;
                Log.Player("Famine in the lands of " + NameOf(n) + ": the granaries are empty and people are dying of hunger.", true);
            }
        }

        /// <summary>U27 - jedna definicja zapasu (projekt etapu 4 rozdz. 4, E4/0 T9 i E4/6): wszystkie warownie AI, dni = (Food warowni x Q + spichlerze okregu) /
        /// (ubytek Food x Q + ubytek spichlerzy okregu), bez ubytku = 364 (w medianie), gracz osobno; Q = mieszczan na jednostke Food (GL 2.3). Plus plik regionow.</summary>
        private static void StockPart(StringBuilder sb, Dictionary<string, Settlement> nodeById, Dictionary<Settlement, Reg> regs, Dictionary<Town, double> shelter, double qWorld, int day,
                                      HashSet<string> e46)
        {
            var days = new List<double>();
            double min = double.MaxValue; string minName = "-";
            int ai = 0, below90 = 0, below30 = 0, noDraw = 0, noRead = 0;
            var player = new List<string>();
            var csv = new StringBuilder(nodeById.Count * 160);
            foreach (var node in nodeById.Values)
            {
                int rowStart = csv.Length;
                try
                {
                    var t = node.Town;
                    if (t == null) continue;
                    float fs = t.FoodStocks, fc;
                    try { fc = t.FoodChange; } catch { noRead++; continue; }
                    double q = QOf(t, qWorld);
                    Reg r; regs.TryGetValue(node, out r);
                    double barn = r != null ? r.Food : 0.0;
                    double eat = r != null ? r.EatHosts + r.EatRef : 0.0, harv = r != null ? r.Harvest : 0.0;
                    double drawB = Math.Max(0.0, eat - harv);
                    double drawW = Math.Max(0.0, -(double)fc);
                    double num = Math.Max(0f, fs) * q + barn, den = drawW * q + drawB;
                    double d = den > 1e-9 ? num / den : NoDraw;
                    bool isPlayer = node.OwnerClan == Clan.PlayerClan;
                    if (isPlayer) player.Add(NameOf(node) + " " + F(d, "0") + (node.IsStarving ? " (Food 0)" : ""));
                    else
                    {
                        ai++; days.Add(d);
                        if (den <= 1e-9) noDraw++;
                        if (d < 90.0) below90++;
                        if (d < 30.0) below30++;
                        if (d < min) { min = d; minName = NameOf(node); }
                    }
                    double people = r != null ? r.Hosts + r.Present : 0.0, sh;
                    csv.Append(day).Append(';').Append(node.StringId).Append(';').Append(Clean(NameOf(node))).Append(';').Append(node.IsTown ? "miasto" : "zamek").Append(';')
                       .Append(Measure40.CultureAtStart(node)).Append(';').Append(isPlayer ? 1 : 0).Append(';').Append(r != null ? r.Vs.Count : 0).Append(';')
                       .Append(F0(r != null ? r.Hosts : 0)).Append(';').Append(F0(r != null ? r.Present : 0)).Append(';')
                       .Append(F0(shelter.TryGetValue(t, out sh) ? sh : 0.0)).Append(';').Append(F0(barn)).Append(';').Append(people > 0 ? F(barn / people, "0.#") : "").Append(';')
                       .Append(F0(harv)).Append(';').Append(F0(eat)).Append(';')
                       .Append(r != null && r.Hosts > 0 ? F(HostsShort(r), "0.####") : "").Append(';').Append(r != null && r.Present > 0 ? F(r.URef, "0.####") : "").Append(';')
                       .Append(F(r != null ? r.Dead : 0, "0.##")).Append(';').Append(F(r != null ? r.Fled : 0, "0.##")).Append(';')
                       .Append(F(fs, "0.#")).Append(';').Append(F(fc, "0.#")).Append(';').Append(F(q, "0")).Append(';').Append(F(d, "0.#")).Append(';')
                       .Append(node.IsUnderSiege ? 1 : 0).Append(';').Append(node.IsStarving ? 1 : 0).Append(';')
                       .Append(e46 != null && node.StringId != null && e46.Contains(node.StringId) ? 1 : 0).Append(Environment.NewLine);
                }
                catch (Exception e) { csv.Length = rowStart; Stumble("Famine.StockPart", e); }
            }
            sb.Append(" | zapas warowni AI (U27: (Food x Q + spichlerze okregu) / (ubytek Food x Q + ubytek spichlerzy), bez ubytku = ").Append(NoDraw).Append("): ");
            if (days.Count > 0)
            {
                days.Sort();
                int n = days.Count;
                double med = n % 2 == 1 ? days[n / 2] : 0.5 * (days[n / 2 - 1] + days[n / 2]);
                sb.Append("mediana ").Append(F(med, "0")).Append(" dni, minimum ").Append(F(min, "0")).Append(" (").Append(minName).Append("), ponizej 90 dni ").Append(below90)
                  .Append(", ponizej 30 dni ").Append(below30).Append(", bez ubytku ").Append(noDraw).Append(" z ").Append(ai).Append(" warowni AI");
            }
            else sb.Append("brak warowni AI");
            if (noRead > 0) sb.Append(", bez odczytu Food ").Append(noRead);
            sb.Append("; gracz: ").Append(player.Count > 0 ? string.Join(", ", player.ToArray()) : "brak warowni");
            sb.Append("; Q swiata ").Append(F(qWorld, "0"));
            string path = Log.Csv(CsvName, CsvHeader, csv.ToString());
            if (path != null && _csvPath != path) { _csvPath = path; Log.Info("Spichlerze (E4/6): plik regionow (wiersz na warownie na dobe): " + path); }
        }


        private static double HostsShort(Reg r)
        {
            double eat = 0, need = 0;
            foreach (var x in r.Vs) { eat += x.Eat; need += x.Home; }
            return need > 0.0 ? Clamp01(1.0 - eat / need) : 0.0;
        }

        /// <summary>Wylaczone (MCM albo brak jednostki ludzi / przyrostu): spichlerze zamrozone, H = 0, nikt nie gloduje; uchodzcy z glodu wracaja w rok od dzis.</summary>
        private static void OffDay(Settings s, int today, int day)
        {
            _hungry = 0;
            lock (Gate) { _shelterUnits.Clear(); _shelterN = 0; foreach (var b in _barns.Values) b.U = 0f; }
            _gateTouched.Clear();
            double famOut = 0; int famV = 0;
            try
            {
                var accounts = new List<KeyValuePair<string, double>>();
                PeopleBook.RefugeesSnapshot(accounts);
                var away = new Dictionary<string, double>();
                foreach (var kv in accounts) away[kv.Key] = kv.Value;
                lock (Gate)
                {
                    foreach (var kv in _barns)
                    {
                        var b = kv.Value;
                        double a; away.TryGetValue(kv.Key, out a);
                        if (!(a > 0.0)) { b.FamOut = false; b.Stay = null; b.Recovered = -1; b.MoveDay = int.MinValue; b.FamN = 0.0; b.FamAt = 0.0; continue; }
                        if (b.FamOut) SyncFam(b, FamPart(b, a), a);       // R1-1: tylko czesc z glodu
                        if (b.FamOut) { famOut += b.FamN; famV++; if (b.Recovered < 0) b.Recovered = today; }
                    }
                }
            }
            catch (Exception e) { Stumble("Famine.OffDay", e); }
            if (!s.LogEnabled || (_offLogged && famV == 0)) return;
            _offLogged = true;
            int n; lock (Gate) n = _barns.Count;
            Log.Info("Spichlerze (E4/6): dzien " + day + " | WYLACZONE (" + (!s.FamineEnabled ? "Famine Enabled w MCM" : "wymaga People Unit Enabled i Natural Growth Enabled")
                     + ") - spichlerze zamrozone (" + n + " wsi w zapisie), nikt nie gloduje, glod nie hamuje przyrostu; uchodzcy z glodu na kontach " + F0(famOut)
                     + " ludzi w " + famV + " wsiach - wracaja liniowo w rok od pierwszej doby wylaczenia (regula E4/5).");
        }

        // ------------------------------------------------------------ latka i linia startowa
        internal static void ApplyAll(Harmony h)
        {
            string wired = "-", missing = "-";
            try
            {
                var m = AccessTools.Method(typeof(DefaultSettlementFoodModel), "CalculateTownFoodStocksChange");
                if (m != null && m.ReturnType == typeof(ExplainedNumber))
                {
                    h.Patch(m, postfix: new HarmonyMethod(typeof(Famine), nameof(ShelterPostfix)));
                    _shelterPatched = true; wired = "spichlerz warowni (schronieni w murach oblezonej warowni)";
                }
                else missing = "DefaultSettlementFoodModel.CalculateTownFoodStocksChange";
            }
            catch (Exception e) { missing = "CalculateTownFoodStocksChange (" + e.GetType().Name + ")"; Log.Error("Famine.ApplyAll", e); }
            try
            {
                // stan z ustawien startowych; MCM wchodzi przy starcie kampanii - prawdziwy stan stoi co dobe w linii "Spichlerze (E4/6)"
                var s = Settings.Current;
                var sb = new StringBuilder(900);
                sb.Append("Famine (E4/6): spichlerze okregow i glod ").Append(On ? "CZYNNE" : (s != null && !s.FamineEnabled ? "wylaczone w ustawieniach" : "NIECZYNNE - wymaga People Unit Enabled i Natural Growth Enabled"));
                if (s != null)
                {
                    sb.Append(" - plon ").Append(F(Math.Max(0f, s.FamineYieldPerPerson), "0.##")).Append(" x ludzie okregu x (zywnosc wsi z modelu gry / (poziom + 1)), sufit ")
                      .Append(F(Math.Max(0.05f, s.FamineBarnYears), "0.##")).Append(" lat potrzeby, start ").Append(F(100.0 * Clamp01(s.FamineStartShare), "0")).Append("% sufitu, przednowek ponizej ")
                      .Append(Math.Max(0, s.FamineRationDays)).Append(" dni (90%), prog glodu ").Append(F(100.0 * ThresholdNow, "0")).Append("%, zgony do ")
                      .Append(Math.Max(0, Math.Min(100, s.FamineDeathMaxPercent))).Append("%/rok; uchodzcy z glodu ").Append(s.FamineFleeEnabled ? "TAK" : "NIE")
                      .Append(" (region tego samego albo niewrogiego krolestwa ze spichlerzem ponad ").Append(Math.Max(0, s.FamineRefugeMinDays)).Append(" dni w promieniu ")
                      .Append(F(Math.Max(0f, s.FamineRefugeRadius), "0")).Append(", przeprowadzka raz na ").Append(Math.Max(0, s.FamineMoveDays)).Append(" dob, powrot od ")
                      .Append(Math.Max(0, s.FamineReturnMinDays)).Append(" dni spichlerza wsi), schronienie w oblezonej warowni ").Append(s.FamineSiegeShelter ? "TAK" : "NIE")
                      .Append(", glod w przyroscie (H) ").Append(s.GrowthHungerEnabled ? "TAK" : "NIE");
                }
                sb.Append("; wpiete: ").Append(wired).Append("; BRAK: ").Append(missing).Append('.');
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Log.Error("Famine.ApplyAll(linia)", e); }
        }

        // ------------------------------------------------------------ formaty
        private static string NameOf(Settlement st)
        {
            if (st == null) return "?";
            try { var n = st.Name; if (n != null) return n.ToString(); } catch { }
            return st.StringId ?? "?";
        }
        private static string Clean(string s) { return string.IsNullOrEmpty(s) ? "" : s.Replace(';', ',').Replace('\n', ' ').Replace('\r', ' '); }
        private static string F(double v, string fmt) { return v.ToString(fmt, Inv); }
        private static string F0(double v) { return Math.Round(v).ToString("0", Inv); }
        private static string M(double v) { return (v / 1e6).ToString("0.000", Inv); }
        private static string Sg(double v, string fmt) { return (v >= 0.0 ? "+" : "") + v.ToString(fmt, Inv); }

        /// <summary>r4-bkwies (G1, StartCalibration.GranaryFloor): zapas spichlerza wsi (racje, osobodni) przed plonem doby; -1 - spichlerz jeszcze nie zalozony.</summary>
        internal static double BarnFoodOf(string id)
        {
            if (id == null) return -1;
            lock (Gate)
            {
                Barn b;
                return _barns.TryGetValue(id, out b) && b != null && !double.IsNaN(b.Food) && !double.IsInfinity(b.Food) && b.Food >= 0.0 ? b.Food : -1;
            }
        }
    }
}
