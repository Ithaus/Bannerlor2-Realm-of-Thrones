using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// sw-uchodzcy (noc 17/18.10; decyzja Jeffa "4. tak" do propozycji STAN-PRAC 57: "uchodzcy ida do najblizszego miasta (ludnosc/biedota), potem wracaja";
    /// klucz RaidRefugeesToTowns) - UCHODZCY Z NAJAZDU W MIESCIE.
    /// Dotad ludzie spustoszonych osad (Devastation, konto UCHODZCY ksiegi ludzi "u:") siedzieli tylko w ksiedze "poza domem" i jedli ze spichlerzy swojego regionu (Famine).
    /// Odtad CZESC Z NAJAZDU konta wsi (konto minus czesc z glodu Famine.FamPartOf - jedna ksiega, jedna regula na zjawisko) idzie do najblizszego MIASTA:
    ///  - miasto: najblizsze nieoblezone miasto krolestwa wsi w promieniu RaidRefugeesTownRadius, inaczej miasta krolestwa z nim niewalczacego; brak - zostaja w regionie
    ///    jak dotad (jedza ze spichlerzy). Przydzial raz (Daily) i trwaly, dopoki konto ma ludzi z najazdu w miescie (bez przeprowadzek za polityka);
    ///  - LICZBA W MIESCIE jest pochodna (bez drugiego licznika ludzi): InTown = konto wsi - czesc z glodu, gdy konto ma miasto. Ubytek konta (smierc 1.3%/rok, powrot,
    ///    podloga 10, pojmani) zmienia ja sam; Track po kazdej zmianie rozlicza roznice wedlug przyczyny (przybyli, wrocili, zmarli, glod miasta, pojmani, podloga),
    ///    Daily sprawdza reszte (kontrola - prog 0);
    ///  - w miescie sa BIEDOTA (stan "biedota" ZC / klasa W kiesy ludu): DailyLife dopisuje ich do mianownika Dostatku biedoty miasta (Rec.Guests, AwayE) i do stanow
    ///    KS (KsSnapshot: biedota i ludzie miasta - potrzeba targu ZC4, koszyk klas, rece pol mieszczan Planu); okreg wsi traci ich w potrzebie jedzenia KS (AwayInRegion),
    ///    rece okregu bez zmian (mnoznik rak spustoszenia juz ich zdejmuje); kiesa ludu liczy ich glowy w koszyku klasy W (TownFolk.RefugeeHeadsOf - kotwica jutra) - jedza
    ///    z targu jak biedota (popyt prawdziwy, drabina najtanszego jedzenia ZC4) i pracuja za place klasy W (zarobek klasy W dzieli sie na wiecej glow - nikt nie placi z niczego);
    ///  - KIESA: przybyli zabieraja z kiesy wsi (Village.Gold) swoja czesc na glowe x RaidRefugeesPurseShare -> kiesa W miasta (TownFolk.RefugeePurseIn; posiadacz H22, ksiega
    ///    pieniadza MFolk, kontrola zamknieta kies). W kiesie W ich czesc to "saldo" (Bal): co dobe udzial glow uchodzcow w zmianie kiesy W (zarobek - zakupy - podatki
    ///    i czynsz), nie ponizej 0 (reszte dokladaja mieszczanie - "doplata ludu miasta") i nie ponad kiese W. Powracajacy zabieraja swoja czesc salda do kiesy wsi
    ///    (TownFolk.RefugeePurseOut) - zamkniety obieg: wies -> saldo -> wydane w miescie, reszta wraca;
    ///  - POWROT bez zmian (Devastation.Daily: 30 dob spokoju, wszyscy w rok, oblezenie warowni regionu wstrzymuje) + oblezenie MIASTA POBYTU trzyma czesc z najazdu
    ///    w murach (HostBesieged); przy Food 0 oblezonego miasta gloduja razem z mieszczanami (Famine - ta sama stopa co TownDie);
    ///  - Famine karmi ze spichlerzy tylko czesc konta poza miastem (goscie regionu) - bez podwojnego jedzenia; smierc gosci i glod miasta nie ruszaja drugiej czesci.
    /// Stan w zapisie (klucz "arm_raidref", SaveText.Sync): miasto konta, ludzie przy ostatnim rozliczeniu, saldo, przybyli bez kiesy, nalezne powracajacym, doba,
    /// migawki kies W miast. Stary zapis bez klucza - pusto (przydzial w pierwszej dobie); starsza DLL klucz pomija (saldo zostaje w kiesie W - zloto nie ginie).
    /// Klucz wylaczony (albo spustoszenie nieczynne): saldo wraca do kies wsi, przydzialy znikaja, wszystko jak przed paczka (InTown = 0).
    /// RECENZJA (wazne 1): miasto przyjmuje uchodzcow TYLKO przy kiesie ludu czynnej i skalibrowanej ORAZ pelnym koszyku stanow ZC4 (StartCalibration.Zc4Host - lud kupuje
    /// z targu wedlug potrzeby ludzi stanow, w ktorej sa uchodzcy; poza ZC4 zakupy ludu siegaja najwyzej wydatku gry, wiec uchodzcy w miescie nie jedliby nic - towar z niczego
    /// w nicosc). Inaczej zostaja w regionie i jedza ze spichlerzy jak dotad (gra Jeffa bez KS - zachowanie sprzed paczki). Miasto, ktore przestalo moc przyjac (albo weszlo
    /// w wojne z ich krolestwem), odsyla ich do regionu (CLeave, saldo do kiesy wsi); ZC4 przelicza potrzebe miasta po zmianie uchodzcow (Zc4Redo - bez 7 dob opoznienia).
    /// Oblezone miasto pobytu: jedza z jego Food jak schronieni (Famine, ta sama stopa ludzie / Q) i gloduja z mieszczanami. Nalezne powracajacym, ktorych kiesa W nie oddala,
    /// czekaja (OwedTown) - sufit kiesy W obejmuje saldo i nalezne, wiec zwrot ma pokrycie.
    /// Linia doby "Uchodzcy do miast (RT): dzien N" (sprawdz_logi --grupa nuchodzcy). Wyjatki per konto liczone (potkniecia), bez globalnych wylacznikow.
    /// </summary>
    internal static class RaidRefugees
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private const string P = "Uchodzcy do miast (RT): ";

        // przyczyny ruchu ludzi w miastach (Track)
        internal const int CArrive = 0, CAssign = 1, CHome = 2, CDied = 3, CStarve = 4, CCapt = 5, CFloor = 6, CLeave = 7, COther = 8;
        private const int CKinds = 9;

        private sealed class Acc
        {
            public string Town;          // id miasta pobytu (null - region, jak dotad)
            public double N;             // ludzie konta w miescie przy ostatnim rozliczeniu (Track / Daily)
            public long Bal;             // saldo: czesc kiesy W miasta nalezna uchodzcom tej wsi (zl)
            public double ArrPend;       // przybyli od ostatniej doby bez kiesy (ludzie)
            public long Owed;            // zl nalezne powracajacym (do kiesy wsi w Daily)
            public int Since = -1;       // doba przydzialu miasta
            public double NSnap;         // ludzie w miescie przy migawce kiesy (udzial glow w zmianie kiesy W)
            public Village V;            // wies (bez zapisu - leniwie)
            public string OwedTown;      // recenzja: konto zamkniete (nikt w miescie), a nalezne jeszcze nie oddane - miasto, ktorego kiesa W je trzyma (Town = null)
        }

        /// <summary>Miasto, z ktorego kiesy W idzie zwrot konta (pobyt albo konto zamkniete z naleznymi).</summary>
        private static string PayTown(Acc a) { return a.Town ?? a.OwedTown; }

        private static readonly object Gate = new object();
        private static readonly Dictionary<string, Acc> _acc = new Dictionary<string, Acc>(StringComparer.Ordinal);       // id wsi -> konto w miescie
        private static readonly Dictionary<string, long> _kwSnap = new Dictionary<string, long>(StringComparer.Ordinal);  // id miasta -> kiesa W po przelewach ostatniej doby
        private static volatile int _n;                                                                                    // konta z miastem (szybka sciezka)

        // liczniki doby (od ostatniej linii; bez zapisu)
        private static readonly double[] _in = new double[CKinds], _out = new double[CKinds];
        private static readonly Dictionary<string, double> _arrTown = new Dictionary<string, double>(StringComparer.Ordinal);    // id miasta -> przybyli od ostatniej doby
        private static readonly Dictionary<string, string> _arrFrom = new Dictionary<string, string>(StringComparer.Ordinal);    // id miasta -> wies najwiekszej fali
        private static readonly Dictionary<string, double> _arrFromN = new Dictionary<string, double>(StringComparer.Ordinal);
        private static double _depHit, _depAssign;    // niezalezne liczniki odejsc z regionu: uchodzcy dopisani przez uderzenie kontu z miastem, czesc z najazdu konta przy przydziale
        private static long _dOwedMoved;               // saldo, ktore odeszlo z powracajacymi / do regionu / po pustym koncie (Track i Daily) - do zaplaty
        private static int _dHold;                     // konta: powrot wstrzymany oblezeniem miasta pobytu (Devastation.Daily)
        private static double _dHoldPeople;
        private static int _stumbles, _stumblesAll;
        private static readonly HashSet<string> _err = new HashSet<string>();
        // migawka spichlerzy (Famine.Daily) i ZC (DailyLife) - kontrola "nikt dwa razy"
        private static bool _famHas; private static int _famDay = -1; private static double _famGuests, _famShelter, _famLost, _famTown, _famLedger;
        private static bool _zcHas; private static int _zcDay = -1, _zcTowns; private static double _zcLabour, _zcGuests, _zcPresent;
        // sesja
        private static bool _sessionLogged, _offLogged, _firstShare = true;
        private static long _sIn, _sOut, _sShort, _sNet, _sStart = long.MinValue;
        private static double _tYesterday = double.NaN;
        // recenzja (wazne 1): ZC4 - uchodzcy liczeni w potrzebie targu miasta przy ostatnim przeliczeniu (id miasta -> ludzie) i migawka ostatniego Zc4Build (id miasta ->
        // dopisani, liczeni, prog); przeliczenia wymuszone zmiana uchodzcow od ostatniej linii
        private static readonly Dictionary<string, double> _zcNoted = new Dictionary<string, double>(StringComparer.Ordinal);
        private static readonly Dictionary<string, double[]> _zcBuild = new Dictionary<string, double[]>(StringComparer.Ordinal);
        private static int _zcBuildDay = -1, _zcRedoRT, _zcUnknownRun;
        // recenzja (wazne 2, drobne 3): oblezone miasta pobytu - uchodzcy z najazdu w murach jedza z Food (Famine), zmarli z glodu miasta (doba)
        private static bool _sgHas; private static int _sgDay = -1, _sgTowns; private static double _sgPeople, _sgDead;
        // recenzja (drobne 5): nalezne powracajacym, ktorych kiesa W nie oddala w dobie - czekaja na nastepna (zostaja w koncie, nie przepadaja)
        private static long _owedCarry;

        private static void Stumble(string where, Exception e)
        {
            _stumbles++; _stumblesAll++;
            try { lock (_err) { if (!_err.Add(where)) return; } Log.Error("RaidRefugees." + where, e); } catch { }
        }

        /// <summary>Czynne: klucz RaidRefugeesToTowns i spustoszenie (konto UCHODZCY obsluguje Devastation).</summary>
        internal static bool On
        {
            get { var s = Settings.Current; return s != null && s.RaidRefugeesToTowns && Devastation.On; }
        }

        /// <summary>PeopleBook.Reset (konstruktor ArmouryBehavior): stan jednej kampanii nie przecieka do drugiej (zapis wczytuje Import po konstruktorze).</summary>
        internal static void Reset()
        {
            lock (Gate) { _acc.Clear(); _kwSnap.Clear(); _n = 0; _zcNoted.Clear(); _zcBuild.Clear(); _zcBuildDay = -1; _zcRedoRT = 0; _zcUnknownRun = 0; _owedCarry = 0; }
            NewDay();
            _stumbles = _stumblesAll = 0; lock (_err) _err.Clear();
            _famHas = false; _famDay = -1; _zcHas = false; _zcDay = -1; _sgHas = false; _sgDay = -1;
            _sessionLogged = _offLogged = false; _firstShare = true;
            _sIn = _sOut = _sShort = _sNet = 0; _sStart = long.MinValue; _tYesterday = double.NaN;
        }

        private static void NewDay()
        {
            lock (Gate)
            {
                Array.Clear(_in, 0, CKinds); Array.Clear(_out, 0, CKinds);
                _arrTown.Clear(); _arrFrom.Clear(); _arrFromN.Clear();
                _dOwedMoved = 0; _dHold = 0; _dHoldPeople = 0; _depHit = _depAssign = 0;
            }
        }

        private static bool BadId(string id) { return string.IsNullOrEmpty(id) || id.IndexOf('|') >= 0 || id.IndexOf('=') >= 0 || id.IndexOf(',') >= 0 || id.IndexOf(';') >= 0; }

        private static string IdOf(Village v) { return v != null && v.Settlement != null ? v.Settlement.StringId : null; }

        private static Village VillageOf(Acc a, string id)
        {
            if (a.V != null) return a.V;
            try { var st = Settlement.Find(id); a.V = st != null ? st.Village : null; } catch { a.V = null; }
            return a.V;
        }

        private static Settlement TownOf(string id)
        {
            if (id == null) return null;
            try { var st = Settlement.Find(id); return st != null && st.IsTown && st.Town != null ? st : null; } catch { return null; }
        }

        // ============================================================ odczyty (watek glowny)
        /// <summary>Ludzie konta wsi v w miescie pobytu teraz (pochodna: konto - czesc z glodu, gdy konto ma miasto); 0 - wylaczone, bez miasta. Nie rzuca.</summary>
        internal static double InTown(Village v)
        {
            if (_n == 0 || v == null) return 0.0;
            try
            {
                if (!On) return 0.0;
                string id = IdOf(v);
                if (id == null) return 0.0;
                lock (Gate) { Acc a; if (!_acc.TryGetValue(id, out a) || a.Town == null) return 0.0; }
                return Derived(v);
            }
            catch (Exception e) { Stumble("InTown", e); return 0.0; }
        }

        private static double Derived(Village v)
        {
            double away = PeopleBook.RefugeesOf(v);
            if (!(away > 0.0)) return 0.0;
            double fam = Famine.FamPartOf(v, away);
            double r = away - Math.Max(0.0, fam);
            return r > 1e-9 && !double.IsInfinity(r) ? r : 0.0;
        }

        /// <summary>Uchodzcy wsi v w jej regionie (konto - czesc w miescie) - potrzeba jedzenia okregu KS. Wylaczone - cale konto (jak dotad).</summary>
        internal static double AwayInRegion(Village v)
        {
            double away = PeopleBook.RefugeesOf(v);
            if (_n == 0) return away;
            return Math.Max(0.0, away - InTown(v));
        }

        /// <summary>Konto wsi v ma ludzi w miescie (Famine: smierc gosci regionu i glod miasta nie ruszaja drugiej czesci konta).</summary>
        internal static bool Owns(Village v) { return InTown(v) > 0.0; }

        /// <summary>Uchodzcy z najazdu mieszkajacy teraz w miescie town (suma kont). 0 - wylaczone albo nikt. Nie rzuca.</summary>
        internal static double PresentIn(Settlement town)
        {
            if (_n == 0 || town == null || town.StringId == null) return 0.0;
            try
            {
                if (!On) return 0.0;
                var vs = VillagesIn(town);
                double t = 0.0;
                foreach (var v in vs) t += Derived(v);
                return t;
            }
            catch (Exception e) { Stumble("PresentIn", e); return 0.0; }
        }

        /// <summary>Wsie, ktorych uchodzcy mieszkaja w miescie town (lista kopiowana - wolajacy moze ruszac konta).</summary>
        internal static List<Village> VillagesIn(Settlement town)
        {
            var res = new List<Village>();
            if (_n == 0 || town == null || town.StringId == null) return res;
            List<KeyValuePair<string, Acc>> l;
            lock (Gate) l = _acc.Where(kv => kv.Value.Town == town.StringId).ToList();
            foreach (var kv in l) { var v = VillageOf(kv.Value, kv.Key); if (v != null) res.Add(v); }
            return res;
        }

        /// <summary>Devastation.Daily: miasto pobytu konta wsi v oblezone - czesc z najazdu siedzi w murach (powrot wstrzymany). Licznik linii.</summary>
        internal static bool HostBesieged(Village v)
        {
            if (_n == 0 || v == null) return false;
            try
            {
                if (!On) return false;
                string id = IdOf(v), town;
                if (id == null) return false;
                lock (Gate) { Acc a; if (!_acc.TryGetValue(id, out a) || a.Town == null) return false; town = a.Town; }
                var st = TownOf(town);
                if (st == null || !st.IsUnderSiege) return false;
                double n = Derived(v);
                if (!(n > 0.0)) return false;
                lock (Gate) { _dHold++; _dHoldPeople += n; }
                return true;
            }
            catch (Exception e) { Stumble("HostBesieged", e); return false; }
        }

        // ============================================================ rozliczenie ruchu (po kazdej zmianie konta)
        /// <summary>
        /// Po zmianie konta UCHODZCY wsi v (wolajacy: Devastation.Hit - przybyli, Devastation.Daily - zmarli i powrot, Famine - glod, ThrallLaw - pojmani, PeopleBook.FloorFromPool -
        /// podloga 10): roznica ludzi w miescie od ostatniego rozliczenia idzie na przyczyne; saldo powracajacych do zaplaty w Daily. Konto bez miasta - nic. Nie rzuca.
        /// </summary>
        internal static void Track(Village v, int cause, double expected = double.NaN)
        {
            if (_n == 0 || v == null || cause < 0 || cause >= CKinds) return;
            try
            {
                if (!On) return;
                string id = IdOf(v);
                if (id == null) return;
                lock (Gate) { Acc a0; if (!_acc.TryGetValue(id, out a0) || a0.Town == null) return; }
                double now = Derived(v);
                lock (Gate)
                {
                    Acc a;
                    if (!_acc.TryGetValue(id, out a) || a.Town == null) return;
                    if (cause == CArrive && expected > 0.0 && !double.IsInfinity(expected)) _depHit += expected;   // niezalezny licznik: uchodzcy dopisani kontu z miastem (Devastated)
                    Apply(a, id, now, cause);
                }
            }
            catch (Exception e) { Stumble("Track", e); }
        }

        /// <summary>Pod Gate: ludzie w miescie a.N -> now z przyczyny cause (liczniki, przybyli bez kiesy, saldo odchodzacych).</summary>
        private static void Apply(Acc a, string id, double now, int cause)
        {
            double d = now - a.N;
            if (Math.Abs(d) < 1e-9) { a.N = now; return; }
            double before = a.N;
            if (d > 0.0)
            {
                _in[cause] += d;
                if (cause == CArrive || cause == CAssign)
                {
                    a.ArrPend += d;
                    double t; _arrTown.TryGetValue(a.Town, out t); _arrTown[a.Town] = t + d;
                    double f; string key = a.Town + "|" + id; _arrFromN.TryGetValue(key, out f); _arrFromN[key] = f + d;
                    double best; string bk; if (!_arrFrom.TryGetValue(a.Town, out bk) || !_arrFromN.TryGetValue(a.Town + "|" + bk, out best) || f + d > best) _arrFrom[a.Town] = id;
                }
            }
            else
            {
                double x = -d;
                _out[cause] += x;
                // przybyli bez kiesy odchodza pierwsi (kiesy jeszcze nie przyniesli)
                double pend = Math.Min(a.ArrPend, x);
                a.ArrPend -= pend;
                double rest = x - pend, base0 = Math.Max(0.0, before - pend - 0.0);
                if (rest > 0.0 && a.Bal > 0 && (cause == CHome || cause == CFloor || cause == CLeave))
                {
                    // powracajacy (i podloga 10 - tez wracaja do wsi) zabieraja swoja czesc salda
                    long owe = base0 > 1e-9 ? (long)Math.Round(a.Bal * Math.Min(1.0, rest / base0)) : a.Bal;
                    owe = Math.Max(0, Math.Min(a.Bal, owe));
                    a.Bal -= owe; a.Owed += owe; _dOwedMoved += owe;
                }
            }
            a.N = now;
            if (a.ArrPend > now) a.ArrPend = now;
        }

        // ============================================================ hooki kontroli (Famine, DailyLife)
        /// <summary>Famine.Daily (po gosciach regionow): jedzacy ze spichlerzy (goscie regionow i schronieni w murach) wobec konta i czesci w miastach - kontrola "nikt dwa razy".</summary>
        internal static void NoteFamine(double guests, double shelter, double lost)
        {
            try
            {
                if (!On) { _famHas = false; return; }
                double town = 0.0;
                List<KeyValuePair<string, Acc>> l;
                lock (Gate) l = _acc.Where(kv => kv.Value.Town != null).ToList();
                foreach (var kv in l) { var v = VillageOf(kv.Value, kv.Key); if (v != null) town += Derived(v); }
                _famGuests = guests; _famShelter = shelter; _famLost = lost; _famTown = town; _famLedger = PeopleBook.RefugeesWorld();
                _famDay = (int)CampaignTime.Now.ToDays - 1; _famHas = true;
            }
            catch (Exception e) { _famHas = false; Stumble("NoteFamine", e); }
        }

        /// <summary>DailyLife (po stanach doby): biedota miast ksiegi, uchodzcy dopisani do mianownika biedoty (Rec.Guests) i obecni w miastach w tej chwili.</summary>
        internal static void NoteZc(int day, double labourTowns, double guests, int towns)
        {
            try
            {
                double present = 0.0;
                if (On && _n > 0)
                {
                    List<KeyValuePair<string, Acc>> l;
                    lock (Gate) l = _acc.Where(kv => kv.Value.Town != null).ToList();
                    foreach (var kv in l) { var v = VillageOf(kv.Value, kv.Key); if (v != null && TownOf(kv.Value.Town) != null) present += Derived(v); }
                }
                _zcLabour = labourTowns; _zcGuests = guests; _zcPresent = present; _zcTowns = towns; _zcDay = day; _zcHas = true;
            }
            catch (Exception e) { _zcHas = false; Stumble("NoteZc", e); }
        }

        /// <summary>
        /// Recenzja (wazne 1) - StartCalibration.Zc4Build (miasto st, po regule "co 7 dob na zmiane"): uchodzcy dopisani do biedoty miasta (KsRec.Guests) zmienili sie od
        /// ostatniego przeliczenia potrzeby o co najmniej max(20, 1% ludzi miasta) - potrzeba miasta od nowa (uchodzcy jedza z targu od jutra, odeszli - nikt za nich nie
        /// kupuje). Kazde przeliczenie zapisuje liczbe uchodzcow w potrzebie (linia RT: liczeni w potrzebie = dopisani +- prog). Nikt i nic zapisanego (klucz wylaczony) -
        /// zwraca redo bez zmian (co do bitu jak dotad). Watek glowny; nie rzuca na zewnatrz (wolajacy i tak lapie).
        /// </summary>
        internal static bool Zc4Redo(Settlement st, double guests, double people, bool redo)
        {
            if (st == null || st.StringId == null) return redo;
            double g = guests > 0.0 && !double.IsInfinity(guests) && !double.IsNaN(guests) ? guests : 0.0;
            lock (Gate)
            {
                if (g <= 0.0 && _zcNoted.Count == 0) return redo;
                string id = st.StringId;
                double noted; _zcNoted.TryGetValue(id, out noted);
                double thr = Math.Max(20.0, 0.01 * (people > 0.0 && !double.IsInfinity(people) ? people : 0.0));
                if (!redo && Math.Abs(g - noted) >= thr) { redo = true; _zcRedoRT++; }
                if (redo) { if (g > 0.0) _zcNoted[id] = g; else _zcNoted.Remove(id); noted = g; }
                int day = Campaign.Current != null ? (int)CampaignTime.Now.ToDays : -1;
                if (day != _zcBuildDay) { _zcBuild.Clear(); _zcBuildDay = day; }
                if (g > 0.0 || noted > 0.0) _zcBuild[id] = new[] { g, noted, thr };
                else _zcBuild.Remove(id);
                return redo;
            }
        }

        /// <summary>Recenzja (wazne 2) - Famine.Daily: miasta pobytu i uchodzcy z najazdu mieszkajacy w nich teraz (oblezone miasto - siedza w murach i jedza z jego Food).
        /// Wylaczone / nikt - pusto. Nie rzuca.</summary>
        internal static void HostsPresent(Dictionary<Settlement, double> into)
        {
            into.Clear();
            if (_n == 0) return;
            try
            {
                if (!On) return;
                List<KeyValuePair<string, Acc>> l;
                lock (Gate) l = _acc.Where(kv => kv.Value.Town != null).ToList();
                foreach (var kv in l)
                {
                    var v = VillageOf(kv.Value, kv.Key); var t = TownOf(kv.Value.Town);
                    if (v == null || t == null) continue;
                    double x = Derived(v);
                    if (!(x > 0.0)) continue;
                    double p; into.TryGetValue(t, out p); into[t] = p + x;
                }
            }
            catch (Exception e) { into.Clear(); Stumble("HostsPresent", e); }
        }

        /// <summary>Recenzja (wazne 2, drobne 3) - Famine.Daily po warowniach: uchodzcy z najazdu w murach oblezonych miast pobytu (jedza z Food) i zmarli z glodu miasta.</summary>
        internal static void NoteSiege(double people, int towns, double dead)
        {
            if (!On) { _sgHas = false; return; }
            _sgPeople = people; _sgTowns = towns; _sgDead = dead; _sgHas = true;
            try { _sgDay = (int)CampaignTime.Now.ToDays - 1; } catch { _sgDay = -1; }
        }

        /// <summary>Recenzja (drobne 4) - widok okregu (DailyLife): nazwa miasta pobytu uchodzcow wsi v; null - brak.</summary>
        internal static string HostName(Village v)
        {
            if (_n == 0 || v == null) return null;
            try
            {
                string id = IdOf(v), town;
                if (id == null) return null;
                lock (Gate) { Acc a; if (!_acc.TryGetValue(id, out a) || a.Town == null) return null; town = a.Town; }
                var st = TownOf(town);
                return st != null && st.Name != null ? st.Name.ToString() : null;
            }
            catch (Exception e) { Stumble("HostName", e); return null; }
        }

        // ============================================================ doba (ArmouryBehavior: w bloku pieniadza, przed TownFolk.Daily - pozycja MFolk)
        internal static void Daily()
        {
            long t0 = Stopwatch.GetTimestamp();
            var s = Settings.Current;
            if (s == null || Campaign.Current == null) return;
            int today = (int)CampaignTime.Now.ToDays, day = today - 1;
            if (!On) { Off(s, day); return; }
            _offLogged = false;
            if (_sStart == long.MinValue) { long b = 0; lock (Gate) foreach (var a in _acc.Values) b += a.Bal + a.Owed; _sStart = b; }
            if (!_sessionLogged) SessionLine(s);

            // 0. recenzja (wazne 1): miasta, ktore moga przyjac uchodzcow - kiesa ludu czynna i miasto skalibrowane (kiesa W placi, glowy klasy W) ORAZ pelny koszyk stanow ZC4
            //    (lud kupuje z targu wedlug potrzeby ludzi stanow, w ktorej sa uchodzcy - KsSnapshot; poza ZC4 zakupy ludu siegaja najwyzej wydatku gry, wiec uchodzcy w miescie
            //    nie jedliby nic). Inne miasto - uchodzcy zostaja w regionie i jedza ze spichlerzy jak dotad. ZC4 "nieznany" (stan zastosowany, mapa koszyka jeszcze nie
            //    zbudowana w tej sesji - pierwsza doba po wczytaniu, doba jak Undo): obecni zostaja, nowych przydzialow nie ma
            var hostOk = new HashSet<string>(StringComparer.Ordinal);
            var hostUnknown = new HashSet<string>(StringComparer.Ordinal);
            int townsAll = 0, townsPurse = 0, townsZc4 = 0, townsZc4Unknown = 0;
            foreach (var t in Town.AllTowns)
            {
                try
                {
                    var ts = t != null ? t.Settlement : null;
                    if (ts == null || ts.StringId == null || !ts.IsTown) continue;
                    townsAll++;
                    double hpp0, nat0; long kw0;
                    bool purse = TownFolk.RefugeeW(ts, out hpp0, out nat0, out kw0);
                    int zc = StartCalibration.Zc4Host(ts);
                    if (purse) townsPurse++;
                    if (zc > 0) townsZc4++; else if (zc < 0) townsZc4Unknown++;
                    if (purse && zc > 0) hostOk.Add(ts.StringId);
                    else if (purse && zc < 0) hostUnknown.Add(ts.StringId);
                }
                catch (Exception e) { Stumble("Daily(miasta)", e); }
            }
            // ZC4 nieznany dluzej niz 2 doby z rzedu (KS w dobie jak Undo - popyt jak dzis, nikt by ich w miescie nie karmil) - obecni tez wracaja do regionu
            if (townsZc4Unknown > 0) _zcUnknownRun++; else _zcUnknownRun = 0;
            if (_zcUnknownRun > 2) hostUnknown.Clear();

            double t0People = 0.0;
            long bal0 = 0, bIn = 0, bOut = 0, bShort = 0, bShare = 0, bSub = 0, bTrim = 0, bTrimOwed = 0;
            int nAssign = 0, nNoTown = 0, nSame = 0, nFriend = 0, nCleared = 0, nOver = 0, nLeftHost = 0, nLeftWar = 0, nNoHost = 0, nWaitOwed = 0;
            double ruPeople = 0.0, leftPeople = 0.0, noHostPeople = 0.0;
            var shortTowns = new HashSet<string>();
            long owedMovedTrack, owedPrev;
            lock (Gate) { owedMovedTrack = _dOwedMoved; owedPrev = _owedCarry; foreach (var a in _acc.Values) { bal0 += a.Bal; } }
            // saldo "wczoraj" = stan teraz + to, co odeszlo w ciagu doby z powracajacymi (Track)
            bal0 += owedMovedTrack;

            // 1. udzial glow uchodzcow w zmianie kiesy W miasta od ostatniej migawki (zarobek - zakupy - podatki i czynsz; nie ponizej 0, nie ponad kiese) - glowy z migawki
            //    wczoraj (NSnap), PRZED zamknieciem kont: kto wrocil dzis, mial swoj udzial w dobie, w ktorej byl w miescie. Grupy wedlug miasta zaplaty (pobyt albo konto
            //    zamkniete z naleznymi) - sufit kiesy W obejmuje saldo i nalezne (recenzja, drobne 5: zwrot w kroku 3 ma zawsze pokrycie)
            var byTown = new Dictionary<string, List<Acc>>(StringComparer.Ordinal);
            lock (Gate) foreach (var kv in _acc) { var pt = PayTown(kv.Value); if (pt == null) continue; List<Acc> l; if (!byTown.TryGetValue(pt, out l)) byTown[pt] = l = new List<Acc>(); l.Add(kv.Value); }
            bool shareDay = !_firstShare;
            foreach (var kv in byTown)
            {
                try
                {
                    var town = TownOf(kv.Key);
                    if (town == null) continue;                            // miasto zniklo z mapy - krok 3 odpisze nalezne (bez pokrycia)
                    long kw;
                    if (!TownFolk.RefugeePurse(town, out kw))
                    {
                        // kiesa ludu wylaczona (ReturnAll oddal kiesy W do kas miast) - saldo i nalezne nie maja juz pokrycia w kiesie W: przyciete do 0 (zloto jest w kasie miasta)
                        if (!TownFolk.Live) lock (Gate) foreach (var a in kv.Value) { bTrim += a.Bal; a.Bal = 0; bTrimOwed += a.Owed; a.Owed = 0; }
                        continue;
                    }
                    double nsum = 0; lock (Gate) foreach (var a in kv.Value) if (a.Town == kv.Key) nsum += Math.Max(0.0, a.NSnap);
                    long snap;
                    bool has; lock (Gate) has = _kwSnap.TryGetValue(kv.Key, out snap);
                    double hpp, native; long kwW;
                    if (shareDay && has && nsum > 0 && TownFolk.RefugeeW(town, out hpp, out native, out kwW) && hpp > 0)
                    {
                        double dK = kw - snap, all = native + nsum * hpp;
                        if (all > 0)
                        {
                            lock (Gate)
                            {
                                foreach (var a in kv.Value)
                                {
                                    if (a.Town != kv.Key) continue;
                                    long x = (long)Math.Round(dK * Math.Max(0.0, a.NSnap) * hpp / all);
                                    bShare += x;
                                    long nb = a.Bal + x;
                                    if (nb < 0) { bSub += -nb; nb = 0; }
                                    a.Bal = nb;
                                }
                            }
                        }
                    }
                    long sum = 0; lock (Gate) foreach (var a in kv.Value) sum += a.Bal + a.Owed;
                    long cap = Math.Max(0, kw);
                    if (sum > cap)
                    {
                        // saldo i nalezne uchodzcow nie moga byc wieksze niz kiesa W miasta (wydatek klasy W ponad udzial, daniny z kiesy)
                        double q = sum > 0 ? (double)cap / sum : 0.0;
                        lock (Gate)
                            foreach (var a in kv.Value)
                            {
                                long nb = (long)Math.Floor(a.Bal * q); bTrim += a.Bal - nb; a.Bal = nb;
                                long no = (long)Math.Floor(a.Owed * q); bTrimOwed += a.Owed - no; a.Owed = no;
                            }
                    }
                }
                catch (Exception e) { Stumble("Daily(udzial)", e); }
            }

            // 2. uzgodnienie: ludzie w miescie teraz wobec ostatniego rozliczenia (reszta = sciezka bez Track - kontrola); konta puste, bez miasta albo w miescie, ktore nie
            //    moze ich juz przyjac (recenzja: bez kiesy ludu / koszyka ZC4 - jedliby z niczego; w wojnie z ich krolestwem - nie mieszkaja u wroga) - do zamkniecia
            var accs = new List<KeyValuePair<string, Acc>>();
            lock (Gate) accs.AddRange(_acc);
            var done = new List<string>();
            foreach (var kv in accs)
            {
                try
                {
                    var a = kv.Value;
                    string townId; lock (Gate) townId = a.Town;
                    var v = townId != null ? VillageOf(a, kv.Key) : null;
                    var town = TownOf(townId);
                    double now = v != null && town != null ? Derived(v) : 0.0;
                    int why = 0;
                    if (v != null && town != null)
                    {
                        if (!hostOk.Contains(townId) && !hostUnknown.Contains(townId)) why = 1;
                        else if (AtWar(v, town)) why = 2;
                    }
                    lock (Gate)
                    {
                        if (a.Town == null)
                        {
                            // konto zamkniete czeka na zwrot naleznych (OwedTown); reszta salda (gdy jest) tez do zwrotu
                            if (a.Bal > 0) { a.Owed += a.Bal; _dOwedMoved += a.Bal; a.Bal = 0; }
                            done.Add(kv.Key); continue;
                        }
                        t0People += a.N;
                        if (v == null || town == null)
                        {
                            // wies albo miasto zniknely z mapy - ludzie wracaja do regionu (konto zostaje w ksiedze), saldo do kiesy wsi (gdy jest)
                            Apply(a, kv.Key, 0.0, CLeave);
                            Close(a); nCleared++;
                            done.Add(kv.Key);
                            continue;
                        }
                        double d = now - a.N;
                        if (Math.Abs(d) > 1e-9) { if (d > 0) _in[COther] += d; else _out[COther] += -d; a.N = now; if (a.ArrPend > now) a.ArrPend = now; }
                        if (v.Settlement != null && now > PeopleBook.RefugeesOf(v) + 1e-6) nOver++;
                        if (why != 0)
                        {
                            // miasto pobytu nie moze ich juz przyjac - wracaja do swojego regionu (jedza ze spichlerzy jak dotad), saldo do kiesy wsi; jutro przydzial od nowa
                            if (why == 1) nLeftHost++; else nLeftWar++;
                            leftPeople += now;
                            Apply(a, kv.Key, 0.0, CLeave);
                            Close(a); nCleared++;
                            done.Add(kv.Key);
                            continue;
                        }
                        if (!(now > 1e-9) && a.ArrPend <= 1e-9)
                        {
                            // konto bez ludzi w miescie (wrocili, zmarli): reszta salda wraca do kiesy wsi
                            Close(a); nCleared++;
                            done.Add(kv.Key);
                        }
                    }
                }
                catch (Exception e) { Stumble("Daily(uzgodnienie)", e); }
            }
            lock (Gate) Recount();

            // 3. nalezne powracajacym: kiesa W miasta -> kiesa wsi (TownFolk.RefugeePurseOut). Recenzja (drobne 5): czego kiesa W dzis nie oddala, czeka na jutro (konto
            //    zostaje); bez pokrycia (wies albo miasto zniknely, kiesa ludu nieczynna - zlota nie ma juz w kiesie W) - odpisane
            lock (Gate) accs = _acc.ToList();
            foreach (var kv in accs)
            {
                try
                {
                    var a = kv.Value;
                    long owe; string payId;
                    lock (Gate) { owe = a.Owed; payId = PayTown(a); }
                    if (owe <= 0) continue;
                    var v = VillageOf(a, kv.Key);
                    var town = TownOf(payId);
                    long kwP = 0;
                    bool purse = v != null && town != null && TownFolk.RefugeePurse(town, out kwP);
                    long got = purse ? TownFolk.RefugeePurseOut(town, v, owe) : 0;
                    got = Math.Max(0, Math.Min(owe, got));
                    bOut += got;
                    lock (Gate)
                    {
                        if (!purse) { bShort += owe - got; a.Owed = 0; if (payId != null) shortTowns.Add(payId); }
                        else a.Owed = owe - got;
                    }
                }
                catch (Exception e) { Stumble("Daily(zwrot)", e); }
            }
            long owedNow = 0; int waitAcc = 0; var waitTowns = new HashSet<string>(StringComparer.Ordinal);
            lock (Gate)
            {
                foreach (var id in done)
                {
                    Acc a;
                    if (!_acc.TryGetValue(id, out a)) continue;
                    if (a.Town != null || a.Owed > 0 || a.Bal > 0) continue;       // nalezne czekaja (konto zostaje do zwrotu), saldo nie ginie
                    _acc.Remove(id);
                }
                Recount();
                foreach (var a in _acc.Values) if (a.Owed > 0) { owedNow += a.Owed; waitAcc++; var pt = PayTown(a); if (pt != null) waitTowns.Add(pt); }
                _owedCarry = owedNow;
            }

            // 4. przydzial miasta kontom z ludzmi z najazdu bez miasta (tylko miasta, ktore moga przyjac - krok 0)
            var snapList = new List<KeyValuePair<string, double>>();
            PeopleBook.RefugeesSnapshot(snapList);
            float rad = Math.Max(0f, s.RaidRefugeesTownRadius);
            foreach (var kv in snapList)
            {
                try
                {
                    Acc a; bool has, wait;
                    lock (Gate) { has = _acc.TryGetValue(kv.Key, out a); wait = has && a.Town == null && a.Owed > 0; has = has && a.Town != null; }
                    if (has) continue;
                    var st = Settlement.Find(kv.Key);
                    var v = st != null ? st.Village : null;
                    if (v == null) continue;
                    double fam = Famine.FamPartOf(v, kv.Value);
                    double raid = kv.Value - Math.Max(0.0, fam);
                    if (!(raid >= 1.0)) continue;
                    if (wait) { nWaitOwed++; continue; }                     // konto czeka na zwrot naleznych z poprzedniego miasta - przydzial po zwrocie
                    bool same; int seen;
                    var town = FindTown(v, rad, hostOk, out same, out seen);
                    if (town == null)
                    {
                        if (seen > 0) { nNoHost++; noHostPeople += raid; } else { nNoTown++; ruPeople += raid; }
                        continue;
                    }
                    lock (Gate)
                    {
                        if (!_acc.TryGetValue(kv.Key, out a)) _acc[kv.Key] = a = new Acc();
                        a.Town = town.StringId; a.OwedTown = null; a.V = v; a.N = 0.0; a.ArrPend = 0.0; a.NSnap = 0.0; a.Since = today;
                        Recount();
                    }
                    nAssign++; if (same) nSame++; else nFriend++;
                    lock (Gate) _depAssign += raid;
                    Track(v, CAssign);
                }
                catch (Exception e) { Stumble("Daily(przydzial)", e); }
            }

            // 5. przybyli od ostatniej doby przynosza swoja czesc kiesy wsi (kiesa wsi -> kiesa W miasta)
            float share = Math.Max(0f, Math.Min(1f, s.RaidRefugeesPurseShare));
            lock (Gate) accs = _acc.ToList();
            foreach (var kv in accs)
            {
                try
                {
                    var a = kv.Value;
                    double pend; string townId;
                    lock (Gate) { pend = a.ArrPend; townId = a.Town; a.ArrPend = 0.0; }
                    if (!(pend > 1e-9) || townId == null || !(share > 0f)) continue;
                    var v = VillageOf(a, kv.Key);
                    var town = TownOf(townId);
                    if (v == null || town == null || v.Settlement == null) continue;
                    float k = PopulationLaw.KFor(v.Settlement);
                    double home = k > 0f ? (double)Math.Max(0f, v.Hearth) * k : 0.0;
                    double region = Math.Max(0.0, PeopleBook.RefugeesOf(v) - Derived(v));
                    double denom = home + region + pend;
                    long gold = Math.Max(0, v.Gold);
                    if (!(denom > 0.0) || gold <= 0) continue;
                    long want = (long)Math.Floor(gold * Math.Min(1.0, pend / denom) * share);
                    if (want <= 0) continue;
                    long got = TownFolk.RefugeePurseIn(town, v, want);
                    if (got > 0) { lock (Gate) a.Bal += got; bIn += got; }
                }
                catch (Exception e) { Stumble("Daily(kiesa)", e); }
            }

            // 6. migawki: ludzie w miastach i kiesy W po przelewach (udzial jutra)
            var present = new Dictionary<string, double>(StringComparer.Ordinal);
            var vilCount = new Dictionary<string, int>(StringComparer.Ordinal);
            double tNow = 0.0; long balNow = 0; int accN = 0;
            lock (Gate) accs = _acc.ToList();
            foreach (var kv in accs)
            {
                var a = kv.Value;
                lock (Gate) balNow += a.Bal;
                if (a.Town == null) continue;
                lock (Gate) a.NSnap = a.N;
                accN++; tNow += a.N;
                double p0; present.TryGetValue(a.Town, out p0); present[a.Town] = p0 + a.N;
                int c0; vilCount.TryGetValue(a.Town, out c0); vilCount[a.Town] = c0 + 1;
            }
            lock (Gate)
            {
                _kwSnap.Clear();
                foreach (var tid in present.Keys)
                {
                    try { var town = TownOf(tid); double hpp, native; long kw; if (town != null && TownFolk.RefugeeW(town, out hpp, out native, out kw)) _kwSnap[tid] = kw; }
                    catch (Exception e) { Stumble("Daily(migawka)", e); }
                }
            }
            _firstShare = false;
            long owedMoved; lock (Gate) owedMoved = _dOwedMoved;
            _sIn += bIn; _sOut += bOut; _sShort += bShort; _sNet += bShare + bSub - bTrim - bTrimOwed;

            // 7. komunikat gracza: duza fala w miescie gracza albo w miescie, w ktorym stoi
            try { Messages(s); } catch (Exception e) { Stumble("Messages", e); }

            // 8. linia doby
            if (s.LogEnabled)
            {
                try
                {
                    double ledger = PeopleBook.RefugeesWorld();
                    double region = ledger - tNow;
                    double tPrev = double.IsNaN(_tYesterday) ? t0People : _tYesterday;
                    double[] inn = new double[CKinds], outn = new double[CKinds];
                    int hold; double holdP, depHit, depAssign;
                    lock (Gate) { Array.Copy(_in, inn, CKinds); Array.Copy(_out, outn, CKinds); hold = _dHold; holdP = _dHoldPeople; depHit = _depHit; depAssign = _depAssign; }
                    double arr = inn[CArrive] + inn[CAssign];
                    double dead = outn[CDied] + outn[CStarve];
                    double check = tNow - (tPrev + arr - outn[CHome] - dead - outn[CCapt] + inn[CCapt] - outn[CFloor] - outn[CLeave] + inn[COther] - outn[COther]);
                    long balCheck = balNow - (bal0 + bIn + bShare + bSub - bTrim - owedMoved);
                    long owedCheck = owedNow - (owedPrev + owedMoved - bTrimOwed - bOut - bShort);
                    var sb = new StringBuilder(2200);
                    sb.Append(P).Append("dzien ").Append(day).Append(" | CZYNNE");
                    sb.Append(" | konto UCHODZCY ").Append(F1(ledger)).Append(" = w miastach ").Append(F1(tNow)).Append(" + w regionach ").Append(F1(region))
                      .Append(" (kont z miastem ").Append(accN).Append(", miast ").Append(present.Count).Append("; konta z miastem ponad konto ").Append(nOver).Append(')');
                    sb.Append(" | miasta zdolne przyjac ").Append(hostOk.Count).Append(" z ").Append(townsAll).Append(" (kiesa ludu czynna i skalibrowana ").Append(townsPurse)
                      .Append(", koszyk stanow ZC4 pelny ").Append(townsZc4).Append(", ZC4 nieznany ").Append(townsZc4Unknown).Append(" - dob z rzedu ").Append(_zcUnknownRun).Append(')');
                    sb.Append(" | w miastach: wczoraj ").Append(F1(tPrev)).Append(" + przybyli ").Append(F1(arr)).Append(" (z uderzen ").Append(F1(inn[CArrive])).Append(", z regionu ").Append(F1(inn[CAssign]))
                      .Append(") - wrocili ").Append(F1(outn[CHome])).Append(" - zmarli ").Append(F3(dead)).Append(" (w drodze ").Append(F3(outn[CDied])).Append(", glod miasta ").Append(F3(outn[CStarve]))
                      .Append(") - pojmani ").Append(F1(outn[CCapt] - inn[CCapt])).Append(" - podloga 10 ").Append(F1(outn[CFloor])).Append(" - do regionu ").Append(F1(outn[CLeave]))
                      .Append(" + inne ").Append(F3(inn[COther] - outn[COther])).Append(" = ").Append(F1(tNow)).Append(" (kontrola ").Append(Sg(check)).Append(')');
                    sb.Append(" | przybyli ").Append(F1(arr)).Append(" = odeszli z regionu do miast ").Append(F1(depHit + depAssign)).Append(" (dopisani przez uderzenia ").Append(F1(depHit))
                      .Append(", czesc z najazdu przy przydziale ").Append(F1(depAssign)).Append("; roznica ").Append(Sg(arr - depHit - depAssign)).Append(')');
                    sb.Append(" | powrot wstrzymany oblezeniem miasta pobytu ").Append(hold).Append(" kont (").Append(F1(holdP)).Append(" ludzi)");
                    sb.Append(" | do regionu z miasta, ktore nie moze ich juz przyjac: bez kiesy ludu lub koszyka ZC4 ").Append(nLeftHost).Append(" kont, w wojnie z ich krolestwem ")
                      .Append(nLeftWar).Append(" kont (").Append(F1(leftPeople)).Append(" ludzi)");
                    sb.Append(" | przydzialy: nowe ").Append(nAssign).Append(" (wlasne krolestwo ").Append(nSame).Append(", niewrogie ").Append(nFriend).Append("), bez miasta w promieniu ")
                      .Append(F0(rad)).Append(' ').Append(nNoTown).Append(" kont (zostaja w regionie ").Append(F1(ruPeople)).Append(" ludzi), zamkniete ").Append(nCleared)
                      .Append(", miasta w zasiegu nie moga przyjac ").Append(nNoHost).Append(" kont (").Append(F1(noHostPeople)).Append(" ludzi), czekaja na zwrot naleznych ").Append(nWaitOwed).Append(" kont");
                    sb.Append(" | kiesa W miast (saldo uchodzcow): wczoraj ").Append(bal0).Append(" + przyniesione z kies wsi ").Append(bIn).Append(" + udzial glow w zmianie kiesy W ").Append(bShare)
                      .Append(" + doplata ludu miasta ").Append(bSub).Append(" - przyciete do kiesy W ").Append(bTrim).Append(" - z powracajacymi ").Append(owedMoved).Append(" = ").Append(balNow)
                      .Append(" (kontrola ").Append(balCheck).Append(')');
                    sb.Append(" | nalezne powracajacym: czekalo ").Append(owedPrev).Append(" + z powracajacymi ").Append(owedMoved).Append(" - przyciete do kiesy W ").Append(bTrimOwed)
                      .Append(" = odniesione do kies wsi ").Append(bOut).Append(" + bez pokrycia ").Append(bShort).Append(" (miast ").Append(shortTowns.Count).Append(") + czeka ").Append(owedNow)
                      .Append(" (kont ").Append(waitAcc).Append(", miast ").Append(waitTowns.Count).Append("; kontrola ").Append(owedCheck).Append(')');
                    sb.Append(" | od wczytania: saldo i nalezne na poczatku ").Append(_sStart == long.MinValue ? 0 : _sStart).Append(", przyniesione ").Append(_sIn).Append(", odniesione ").Append(_sOut)
                      .Append(", bez pokrycia ").Append(_sShort).Append(", netto w miescie ").Append(_sNet).Append(", saldo i nalezne ").Append(balNow + owedNow)
                      .Append(" (wydane netto w miastach ").Append(-_sNet).Append(')');
                    sb.Append(" | kiesa ludu ").Append(TownFolk.Live ? "czynna" : "NIECZYNNA - kiesa zostaje we wsi");
                    if (_famHas && _famDay == day)
                        sb.Append(" | spichlerze (E4/6): w regionach ").Append(F1(_famGuests)).Append(" + w murach ").Append(F1(_famShelter)).Append(" + w miastach ").Append(F1(_famTown))
                          .Append(" + konta bez wsi ").Append(F1(_famLost)).Append(" = ").Append(F1(_famGuests + _famShelter + _famTown + _famLost)).Append(" wobec konta ").Append(F1(_famLedger))
                          .Append(" (roznica ").Append(Sg(_famGuests + _famShelter + _famTown + _famLost - _famLedger)).Append(')');
                    else sb.Append(" | spichlerze (E4/6): brak migawki tej doby");
                    if (_sgHas && _sgDay == day)
                        sb.Append(" | oblezone miasta pobytu: w murach jedza z Food ").Append(F1(_sgPeople)).Append(" ludzi w ").Append(_sgTowns).Append(" miastach, zmarli z glodu miasta ").Append(F3(_sgDead));
                    else sb.Append(" | oblezone miasta pobytu: brak migawki tej doby");
                    if (_zcHas)
                        sb.Append(" | ZC (doba ").Append(_zcDay).Append("): biedota miast ").Append(F0(_zcLabour)).Append(" + uchodzcy ").Append(F1(_zcGuests)).Append(" = ").Append(F0(_zcLabour + _zcGuests))
                          .Append(" (obecni w miastach wtedy ").Append(F1(_zcPresent)).Append(", miast ").Append(_zcTowns).Append(", roznica ").Append(Sg(_zcGuests - _zcPresent)).Append(')');
                    else sb.Append(" | ZC: brak migawki");
                    // recenzja (wazne 1): uchodzcy w miescie jedza z targu - liczeni w potrzebie koszyka stanow ZC4 miasta (popyt prawdziwy; placi kiesa W)
                    int zcN, zcRedo, zcBDay; double zcG = 0, zcNoted = 0, zcThr = 0;
                    lock (Gate)
                    {
                        zcN = _zcBuild.Count; zcRedo = _zcRedoRT; _zcRedoRT = 0; zcBDay = _zcBuildDay - 1;
                        foreach (var x in _zcBuild.Values) { zcG += x[0]; zcNoted += x[1]; zcThr += x[2]; }
                    }
                    if (_zcBuildDay >= 0)
                        sb.Append(" | potrzeba targu ZC4 (doba ").Append(zcBDay).Append("): uchodzcy liczeni w potrzebie miast ").Append(F1(zcNoted)).Append(" wobec dopisanych do biedoty ").Append(F1(zcG))
                          .Append(" (miast ").Append(zcN).Append("; roznica ").Append(Sg(zcNoted - zcG)).Append(", prog ").Append(F1(zcThr)).Append("; przeliczenia od zmiany uchodzcow ").Append(zcRedo)
                          .Append(") - jedza z targu, placi kiesa W");
                    else sb.Append(" | potrzeba targu ZC4: brak przeliczenia");
                    if (present.Count > 0)
                    {
                        var top = present.OrderByDescending(x => x.Value).Take(5).Select(x =>
                        {
                            var t = TownOf(x.Key); int c; vilCount.TryGetValue(x.Key, out c);
                            return Clean(t != null && t.Name != null ? t.Name.ToString() : x.Key) + " " + F0(x.Value) + " (z " + c + " wsi)";
                        });
                        sb.Append(" | najwiecej w: ").Append(string.Join(", ", top.ToArray()));
                    }
                    sb.Append(" | potkniecia ").Append(_stumbles).Append(" (razem ").Append(_stumblesAll).Append(')');
                    sb.Append(" | czas ").Append(((Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency).ToString("0.00", Inv)).Append(" ms.");
                    Log.Info(sb.ToString());
                }
                catch (Exception e) { Stumble("Daily(linia)", e); }
            }
            _tYesterday = tNow;
            _stumbles = 0; _famHas = false; _sgHas = false;
            NewDay();
        }

        /// <summary>Pod Gate: konto bez ludzi w miescie - reszta salda do naleznych (zwrot do kiesy wsi w kroku 3), miasto pobytu zostaje miastem zaplaty (OwedTown).</summary>
        private static void Close(Acc a)
        {
            if (a.Bal > 0) { a.Owed += a.Bal; _dOwedMoved += a.Bal; a.Bal = 0; }
            if (a.Town != null) a.OwedTown = a.Town;
            a.Town = null; a.N = 0.0; a.NSnap = 0.0; a.ArrPend = 0.0;
        }

        /// <summary>Recenzja: krolestwo wsi w wojnie z krolestwem miasta pobytu (uchodzcy nie mieszkaja u wroga). Nie rzuca.</summary>
        private static bool AtWar(Village v, Settlement town)
        {
            try
            {
                var fv = v != null && v.Settlement != null ? v.Settlement.MapFaction : null;
                var ft = town != null ? town.MapFaction : null;
                return fv != null && ft != null && fv != ft && fv.IsAtWarWith(ft);
            }
            catch (Exception e) { Stumble("AtWar", e); return false; }
        }

        /// <summary>Najblizsze nieoblezone miasto krolestwa wsi w promieniu, inaczej krolestwa z nim niewalczacego - sposrod miast, ktore moga przyjac (ok); null - brak
        /// (zostaja w regionie). seen - miasta w zasiegu niezaleznie od tego, czy moga przyjac (linia: "miasta w zasiegu nie moga przyjac").</summary>
        private static Settlement FindTown(Village v, float rad, HashSet<string> ok, out bool same, out int seen)
        {
            same = false; seen = 0;
            var from = v != null ? v.Settlement : null;
            if (from == null || !(rad > 0f)) return null;
            var fac = from.MapFaction;
            var p = from.GetPosition2D;
            double rad2 = (double)rad * rad;
            Settlement best = null; double bd = double.MaxValue; bool bestSame = false;
            foreach (var t in Town.AllTowns)
            {
                try
                {
                    if (t == null || !t.IsTown) continue;
                    var ts = t.Settlement;
                    if (ts == null || ts.StringId == null || ts.IsUnderSiege) continue;
                    var tf = ts.MapFaction;
                    bool sm = fac != null && tf == fac;
                    if (!sm && (fac == null || tf == null || fac.IsAtWarWith(tf))) continue;
                    double d2 = p.DistanceSquared(ts.GetPosition2D);
                    if (d2 > rad2) continue;
                    seen++;
                    if (ok == null || !ok.Contains(ts.StringId)) continue;
                    if (best == null || (sm && !bestSame) || (sm == bestSame && d2 < bd)) { best = ts; bd = d2; bestSame = sm; }
                }
                catch (Exception e) { Stumble("FindTown", e); }
            }
            same = bestSame;
            return best;
        }

        private static void Recount() { int n = 0; foreach (var a in _acc.Values) if (a.Town != null) n++; _n = n; }

        private static void Messages(Settings s)
        {
            int min = s.RaidRefugeesMessageMin;
            if (min <= 0) return;
            List<KeyValuePair<string, double>> l;
            lock (Gate) l = _arrTown.ToList();
            var pc = Clan.PlayerClan;
            var mp = MobileParty.MainParty;
            var here = mp != null ? mp.CurrentSettlement : null;
            foreach (var kv in l)
            {
                if (kv.Value < min) continue;
                var town = TownOf(kv.Key);
                if (town == null) continue;
                bool mine = pc != null && town.OwnerClan == pc, inside = here != null && here == town;
                if (!mine && !inside) continue;
                string from; lock (Gate) _arrFrom.TryGetValue(kv.Key, out from);
                var fs = from != null ? Settlement.Find(from) : null;
                string fn = fs != null && fs.Name != null ? fs.Name.ToString() : "the raided villages";
                Log.Player(Math.Round(kv.Value).ToString("N0", Inv) + " refugees from the burnt lands around " + fn + " have reached " + town.Name
                           + " and found shelter among its labourers. They will go home when their land is quiet again.");
                Log.Info(P + "komunikat gracza - fala " + F0(kv.Value) + " ludzi do " + Clean(town.Name.ToString()) + " (najwiecej z " + Clean(fn) + "; " + (mine ? "miasto gracza" : "gracz w miescie") + ").");
            }
        }

        /// <summary>Klucz wylaczony (albo spustoszenie nieczynne): saldo do kies wsi, przydzialy od zera; linia raz.</summary>
        private static void Off(Settings s, int day)
        {
            int n; lock (Gate) n = _acc.Count;
            if (n == 0)
            {
                if (!_offLogged && s.LogEnabled) Log.Info(P + "dzien " + day + " | WYLACZONE (" + (!s.RaidRefugeesToTowns ? "Raid Refugees To Towns w MCM" : "spustoszenie nieczynne - Devastation Enabled") + ") - uchodzcy z najazdu zostaja w swoim regionie jak dotad.");
                _offLogged = true; NewDay(); return;
            }
            long back = 0, shortG = 0; int accs = 0;
            List<KeyValuePair<string, Acc>> l;
            lock (Gate) l = _acc.ToList();
            foreach (var kv in l)
            {
                try
                {
                    var a = kv.Value;
                    long owe = a.Bal + a.Owed;
                    if (owe <= 0) continue;
                    var v = VillageOf(a, kv.Key); var town = TownOf(PayTown(a));
                    long got = v != null && town != null ? TownFolk.RefugeePurseOut(town, v, owe) : 0;
                    back += got; shortG += owe - got; accs++;
                }
                catch (Exception e) { Stumble("Off", e); }
            }
            lock (Gate) { _acc.Clear(); _kwSnap.Clear(); _n = 0; _owedCarry = 0; }
            _sStart = long.MinValue; _sIn = _sOut = _sShort = _sNet = 0;   // rachunek sesji od nowa przy ponownym wlaczeniu (saldo 0)
            if (s.LogEnabled) Log.Info(P + "dzien " + day + " | WYLACZONE w trakcie (" + (!s.RaidRefugeesToTowns ? "Raid Refugees To Towns w MCM" : "spustoszenie nieczynne") + ") - konta z miastem " + n
                                       + ": saldo do kies wsi " + back + " zl z " + accs + " kont, brak w kiesach W " + shortG + "; uchodzcy wracaja do swoich regionow (jedza ze spichlerzy jak dotad).");
            _offLogged = true; _firstShare = true; _tYesterday = double.NaN;
            NewDay();
        }

        private static void SessionLine(Settings s)
        {
            _sessionLogged = true;
            if (!s.LogEnabled) return;
            int n; long bal = 0, owed = 0; double ppl = 0;
            List<KeyValuePair<string, Acc>> l;
            lock (Gate) { n = _acc.Count; l = _acc.ToList(); }
            var towns = new HashSet<string>();
            foreach (var kv in l) { bal += kv.Value.Bal; owed += kv.Value.Owed; ppl += kv.Value.N; if (kv.Value.Town != null) towns.Add(kv.Value.Town); }
            Log.Info(P + "start sesji | klucze: Raid Refugees To Towns tak, promien " + F0(s.RaidRefugeesTownRadius) + ", kiesa z wsi x" + Math.Max(0f, Math.Min(1f, s.RaidRefugeesPurseShare)).ToString("0.00", Inv)
                     + ", komunikat od " + s.RaidRefugeesMessageMin + " | spustoszenie " + (Devastation.On ? "CZYNNE" : "nieczynne") + ", spichlerze " + (Famine.On ? "CZYNNE" : "nieczynne")
                     + ", kiesa ludu " + (TownFolk.LiveKey ? "czynna" : "NIECZYNNA") + ", KS " + (StartCalibration.Applied ? "zastosowany" : "NIEZASTOSOWANY")
                     + " (miasto przyjmuje tylko przy kiesie ludu i pelnym koszyku stanow ZC4 - inaczej uchodzcy zostaja w regionie jak dotad)"
                     + " | z zapisu: kont " + n + " (miast " + towns.Count + ", ludzi przy ostatnim rozliczeniu " + F1(ppl) + ", saldo " + bal + " zl, nalezne " + owed + " zl).");
        }

        // ============================================================ widok (People of this land, po angielsku)
        /// <summary>Linia okna "People of this land" miasta: uchodzcy z najazdu mieszkajacy w tym miescie. null - nikt albo wylaczone.</summary>
        internal static string MenuLine(Settlement town)
        {
            try
            {
                if (town == null || !town.IsTown || !On || _n == 0) return null;
                var vs = VillagesIn(town);
                double t = 0.0; int c = 0;
                foreach (var v in vs) { double x = Derived(v); if (x >= 0.5) { t += x; c++; } }
                if (!(t >= 1.0)) return null;
                return "Refugees from raided lands living in this town: " + Math.Round(t).ToString("N0", Inv) + " from " + c + (c == 1 ? " village" : " villages")
                       + " - they work and buy their bread here among the labourers and will go home when their land is quiet again.";
            }
            catch (Exception e) { Stumble("MenuLine", e); return null; }
        }

        // ============================================================ zapis ("arm_raidref": "v1|a:<wies>=<miasto>;<N>;<saldo>;<przybyli bez kiesy>;<nalezne>;<doba>[;<miasto zaplaty>],...|k:<miasto>=<kiesa W>,...")
        // recenzja (drobne 5): 7. pole - miasto, ktorego kiesa W trzyma nalezne konta zamknietego (Town puste); zapis bez niego (6 pol) - brak (nalezne bez miasta odpisane w dobie)
        private const int MaxAcc = 5000;

        internal static string Export()
        {
            try
            {
                List<KeyValuePair<string, Acc>> l; List<KeyValuePair<string, long>> k;
                lock (Gate) { l = _acc.ToList(); k = _kwSnap.ToList(); }
                if (l.Count == 0) return "";
                var sb = new StringBuilder(32 + l.Count * 64 + k.Count * 24);
                sb.Append("v1");
                bool first = true; int n = 0;
                foreach (var kv in l)
                {
                    var a = kv.Value;
                    if (BadId(kv.Key) || (a.Town != null && BadId(a.Town))) continue;
                    if (a.Town == null && a.Bal <= 0 && a.Owed <= 0) continue;
                    if (++n > MaxAcc) break;
                    string ot = a.Town == null && a.OwedTown != null && !BadId(a.OwedTown) ? a.OwedTown : "";
                    sb.Append(first ? "|a:" : ",").Append(kv.Key).Append('=').Append(a.Town ?? "").Append(';').Append(Fin(a.N).ToString("R", Inv)).Append(';').Append(Math.Max(0, a.Bal).ToString(Inv))
                      .Append(';').Append(Fin(a.ArrPend).ToString("R", Inv)).Append(';').Append(Math.Max(0, a.Owed).ToString(Inv)).Append(';').Append(a.Since.ToString(Inv)).Append(';').Append(ot);
                    first = false;
                }
                first = true;
                foreach (var kv in k)
                {
                    if (BadId(kv.Key)) continue;
                    sb.Append(first ? "|k:" : ",").Append(kv.Key).Append('=').Append(kv.Value.ToString(Inv));
                    first = false;
                }
                return sb.ToString();
            }
            catch (Exception e) { Stumble("Export", e); return ""; }
        }

        private static double Fin(double x) { return double.IsNaN(x) || double.IsInfinity(x) || x < 0.0 ? 0.0 : x; }

        internal static void Import(string data)
        {
            lock (Gate) { _acc.Clear(); _kwSnap.Clear(); _n = 0; _owedCarry = 0; }
            _sStart = long.MinValue; _firstShare = true; _tYesterday = double.NaN;
            if (string.IsNullOrEmpty(data)) return;
            try
            {
                var parts = data.Split('|');
                if (parts.Length == 0 || parts[0] != "v1") { Log.Info(P + "zapis w nieznanym formacie - konta od zera (saldo zostaje w kiesach W miast)."); return; }
                foreach (var part in parts)
                {
                    if (part.StartsWith("a:", StringComparison.Ordinal))
                    {
                        int n = 0;
                        foreach (var item in part.Substring(2).Split(','))
                        {
                            if (++n > MaxAcc) break;
                            int eq = item.IndexOf('=');
                            if (eq <= 0) continue;
                            var f = item.Substring(eq + 1).Split(';');
                            if (f.Length < 6) continue;
                            double nn, pend; long bal, owed; int since;
                            if (!double.TryParse(f[1], NumberStyles.Float, Inv, out nn)) nn = 0;
                            if (!long.TryParse(f[2], NumberStyles.Integer, Inv, out bal)) bal = 0;
                            if (!double.TryParse(f[3], NumberStyles.Float, Inv, out pend)) pend = 0;
                            if (!long.TryParse(f[4], NumberStyles.Integer, Inv, out owed)) owed = 0;
                            if (!int.TryParse(f[5], NumberStyles.Integer, Inv, out since)) since = -1;
                            var a = new Acc { Town = f[0].Length > 0 ? f[0] : null, N = Fin(nn), Bal = Math.Max(0, bal), ArrPend = Fin(pend), Owed = Math.Max(0, owed), Since = since };
                            if (a.Town == null && f.Length >= 7 && f[6].Length > 0) a.OwedTown = f[6];
                            a.NSnap = a.N;
                            lock (Gate) _acc[item.Substring(0, eq)] = a;
                        }
                    }
                    else if (part.StartsWith("k:", StringComparison.Ordinal))
                    {
                        foreach (var item in part.Substring(2).Split(','))
                        {
                            int eq = item.LastIndexOf('=');
                            if (eq <= 0) continue;
                            long kw; if (!long.TryParse(item.Substring(eq + 1), NumberStyles.Integer, Inv, out kw)) continue;
                            lock (Gate) _kwSnap[item.Substring(0, eq)] = kw;
                        }
                    }
                }
                lock (Gate)
                {
                    Recount();
                    // stan "wczoraj" linii doby i sesji z zapisu (Track miedzy wczytaniem a pierwsza doba juz rusza konta): ludzie w miastach, saldo, nalezne powracajacym
                    // (recenzja, drobne 5: nalezne z zapisu "czekaja" - pozycja "czekalo" linii, nie "z powracajacymi")
                    double t = 0; long b = 0, o = 0;
                    foreach (var a in _acc.Values) { if (a.Town != null) t += a.N; b += a.Bal; o += a.Owed; }
                    _tYesterday = t; _sStart = b + o; _owedCarry = o; _dOwedMoved = 0;
                }
                _firstShare = false;      // migawki kies z zapisu - udzial liczy sie od pierwszej doby
            }
            catch (Exception e) { lock (Gate) { _acc.Clear(); _kwSnap.Clear(); _n = 0; _owedCarry = 0; } _sStart = long.MinValue; Stumble("Import", e); }
        }

        // ============================================================ formaty
        private static string F0(double v) { return Math.Round(v).ToString("0", Inv); }
        private static string F1(double v) { return v.ToString("0.#", Inv); }
        private static string F3(double v) { return v.ToString("0.###", Inv); }
        private static string Sg(double v) { return (v >= 0 ? "+" : "") + v.ToString("0.###", Inv); }
        private static string Clean(string s) { return string.IsNullOrEmpty(s) ? "?" : s.Replace('|', '/').Replace(';', ',').Replace('\n', ' ').Replace('\r', ' '); }
    }
}
