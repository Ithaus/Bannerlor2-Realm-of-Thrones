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
    /// KALIBRACJA STARTU (KS, projekt docs/PROJEKT-KALIBRACJA-STARTU-2026-10-16.md; etap 1 - rdzen, etap 2 - uzbrojenie, rampa, popyt koszyka ZC4 i przetwory ZC5):
    /// produkcja okregu = rece x wydajnosc swiata x ziemia z mapy.
    ///  - BLOKI: wies gry w 20 blokach (punkt wsi + mieszanka ziemi okregu z rastra, arm_ks_land.tsv), kazda wioska poboczna osobno (udzial z osad ksiegi wiosek),
    ///    pola mieszczan (20 blokow rdzenia twierdzy; chlopi miasta, polowa pracy niewolnych, piata czesc wyrobnikow);
    ///  - RECE: ludzie okregu (dom + jego uchodzcy) x udzial stanu z ksiegi stanow ZC (chlopi 0.5, niewolni 0.65); ubytek rak robia dalej E4/5 i E4/7 (raz);
    ///  - DOM: straczki, ogrod, mleko, kury, ul (4 kg na pracujacego x ziemia domu), zboze wlasne bez progu ((L - 0.2) / 0.4 potrzeby), reszta rak - zajecia bloku;
    ///  - TABELE SWIATA: P0, koszt K, ha na pracujacego, ziemia (StartCalibration.Tables.cs - port 1:1 modelu v2); regale (zloza, las, oazy, przyprawy) za cena;
    ///  - WYBOR ZAJECIA: rownowaga reguly dochodu rodziny (logit beta 10, premia mapy 1.25, renta ziemi) - Plan na watku roboczym (StartCalibration.Core.cs);
    ///    po wlaczeniu ta sama regula przy 28-dobowym indeksie cen miasta wsi z hamulcem Jeffa (+25% przez 60 dob, koszt przebudowy do kasy miasta, okres przejsciowy,
    ///    najwyzej 1 czesc okregu naraz, najwyzej 5% rak regionu na sezon) - StartCalibration.Rule.cs;
    ///  - PRODUKCJA W GRZE (tylko po wlaczeniu): stawki wsi z KS w MaterialLaw.ProdPostfix, towary spoza typu na liscie BK z waga 0 (MineralOnce), magazyn wsi, zywnosc
    ///    wsi gry (towar z niczego) wylaczona, plon pol mieszczan na polke miasta za zaplata kasy miasta ludowi; na targ idzie to, czego okreg nie zje sam;
    ///  - SPICHLERZ: racje = max(racje KS z czesci domowej, dzisiejszy wzor) - podloga (KS nikogo nie zaglodzi);
    ///  - PLAN co 7 dob (tylko liczy: linie "Kalibracja startu (KS)" i "KS: ...", plik ks-plan.csv wedlug okregu i towaru).
    /// ETAP 2 (rozdz. 6, 11.2, 12): UZBROJENIE W ZAPISIE - klucz MCM "Arm This Save" zapisuje w arm_ks id kampanii, dobe, skrot planu i crc pliku ziemi i sam wraca na Off;
    /// "Confirm" (ten sam zapis, ten sam id, najwczesniej doba po uzbrojeniu, >= 7 dob Planu, ten sam skrot planu i plik ziemi) wlacza nastepnej doby. Obie drogi wymagaja
    /// Planu z DOJRZALA ksiega towarow (>= 28 dob sredniej w tej sesji - popyt 1:1 wojska, zamkow, dworow, budow i warsztatow), Daily Life Measure, twardych bramek ZC1
    /// (rozdz. 10: BkRecipeYieldByWorkshop, WhaleMeatFromWhalersOnly) i nie w zimie. RECENZJA 16.10: Confirm w grze ODRZUCANY do czasu wdrozenia bramki krainy (rozdz. 8)
    /// i reszty bramek rozdz. 10 (LandGateReady) - linia i komunikat; autotest - osobny klucz albo plik-przelacznik, tylko w autotescie CrashScribe, przy pierwszym
    /// dojrzalym Planie (plik zuzyty przy wlaczeniu). RAMPA: doba wlaczenia A = D-14; podaz KS na towar i rynek od A do D (14 dob, liniowo; towar, ktorego polki rynku
    /// maja juz zapas celu, czeka na popyt - zapas tylko z prawdziwej nadwyzki), popyt koszyka ZC4 (StartCalibration.Zc4.cs) lambda 0 do D, potem 0 -> 1 przez 14 dob
    /// (D..D+14, projekt KS 11.2 - dwa trojkaty, 14 dob pelnej nadwyzki na polki); w dobie D wygladzona podaz rynkow dostaje wartosc polki startu (cena startu z Planu
    /// w pasmie 0.7-1.5) - jedyne "ustawienie" ceny, bez towaru i zlota. A7 i regale od D. PRZETWORY ZC5 w miastach (StartCalibration.Crafts.cs). Undo bez terminu:
    /// klucz Start Calibration wylaczony = nastepna doba produkcja i popyt jak dzis (stan zostaje w zapisie).
    /// Wyjatki per osada liczone (potkniecia) - bez globalnych wylacznikow.
    /// </summary>
    internal static partial class StartCalibration
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private const string P = "Kalibracja startu (KS): ";
        private const string PK = "KS: ";
        // KS-R3 (recenzja runda 3, wazne): zapis arm_ks dalej w formacie ks3 (starsza DLL czyta go jak dotad), stan rundy 3 (gradient krain, czynnik popytu targu, ciecie i polki
        // krain, srednie sterowania, kx KS-H, tabela dob miast, podloga spichlerza wsi, wydatki wsi spoza koszyka, odbior miast, kasa 28 dob, srednie dowozu KS-H) w osobnym
        // kluczu arm_ks_r3 (starsza DLL go pomija); ks4 (pole dodatkowe w arm_ks - DLL z 17.10 rano) czytany
        private const string SaveVersion = "ks4", SaveVersion3 = "ks3", SaveVersion2 = "ks2", SaveVersion1 = "ks1";
        private static string _r3Pending, _r3Keep;          // arm_ks_r3 z zapisu (do rozwiazania przy starcie sesji) i zachowany bez zmian (zapis zastosowany bez pliku ziemi)
        internal const int StatePlan = 0, StateApplied = 1, StateArmed = 2;
        internal const int RampSupply = 14, RampDemand = 28;     // rampa: podaz A..A+14 (= D), popyt koszyka D..A+28 (= D+14)
        private const int MinPlanDays = 7;                        // potwierdzenie najwczesniej po 7 dobach Planu tego zapisu
        internal const int MinLedgerDays = 28;                    // recenzja 16.10 (krytyczne): wlaczenie tylko z Planu z >= 28 dobami sredniej ksiegi towarow w tej sesji
        // recenzja 16.10 (wazne): bramka krainy (projekt rozdz. 8: powrot krainy przy glodzie) i reszta twardych bramek rozdz. 10 nie sa jeszcze w kodzie - do tego czasu
        // Confirm w grze Jeffa jest odrzucany (linia + komunikat); autotest dziala (klucz albo plik tylko w autotescie). Wlaczyc dopiero z bramkami i proba zimy.
        // ks-arm (noc 17/18.10): bramka krainy jest w kodzie (StartCalibration.Arm.cs) - LandGateReady z klucza Ks Land Gate zamiast stalej false (wylaczony = jak dotad:
        // Confirm w grze odrzucany, samo wlaczenie czeka)
        private static bool LandGateReady { get { return LandGateKey; } }

        // ------------------------------------------------------------ stan kampanii (zapis arm_ks)
        private static int _state = StatePlan;
        private static int _applyDay = -1, _plans, _firstPlanDay = -1;
        private static string _campaign = "";
        private static uint _saveLandCrc, _saveLayoutCrc;
        private static int _saveSigma;
        private static int _armDay = -1; private static uint _armHash;   // uzbrojenie: doba i skrot planu
        private static uint _armLandCrc;                                  // uzbrojenie: crc pliku ziemi (Confirm - ten sam plik)
        private static uint _applyHash;                                   // skrot planu wlaczonego
        private static bool _autotest;                                    // wlaczone kluczem autotestu (nie Jeffa)
        private static bool _shelvesDone;                                 // polki startu w dobie D ustawione
        private static string _pending;               // zapis do rozwiazania przy starcie sesji
        private static string _keepRaw;               // recenzja 16.10 (drobne): zapis zastosowany bez pliku ziemi / przy nieczynnej ksiedze wiosek - zachowany bez zmian (Export oddaje go jak byl)
        private static string _loadNote = "";

        // ------------------------------------------------------------ pamiec sesji
        private static int _stumbles, _stumblesDay;
        private static readonly HashSet<string> _err = new HashSet<string>();
        private static bool _offLogged;
        private static int _lastPlanDay = int.MinValue;
        private static int _keyLineDay = int.MinValue;

        internal static bool KeyOn { get { var s = Settings.Current; return s != null && s.StartCalibration; } }
        internal static bool Applied { get { return _state == StateApplied && KeyOn; } }
        /// <summary>Doba D (koniec rampy podazy) albo -1.</summary>
        internal static int DDay { get { return _rampDay >= 0 ? _rampDay + RampSupply : -1; } }

        internal static void Reset()
        {
            _state = StatePlan; _applyDay = -1; _plans = 0; _firstPlanDay = -1; _campaign = ""; _saveLandCrc = _saveLayoutCrc = 0; _saveSigma = 0;
            _armDay = -1; _armHash = 0; _armLandCrc = 0; _applyHash = 0; _autotest = false; _shelvesDone = false;
            _pending = null; _keepRaw = null; _loadNote = ""; _exportLogged = -1;
            _stumbles = _stumblesDay = 0; _err.Clear(); _offLogged = false; _lastPlanDay = int.MinValue; _keyLineDay = int.MinValue;
            PlanReset();
            LiveReset();
            RampReset();
            Zc4Reset();
            CraftsReset();
            MoneyReset();
            R3Reset();
            ArmReset();                                              // ks-arm: samo uzbrojenie, bramka krainy, srednia ksiegi w zapisie
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++; _stumblesDay++;
            try { if (_err.Add(where)) { if (e != null) Log.Error("StartCalibration." + where, e); else Log.Info(PK + "potkniecie - " + where); } } catch { }
        }

        private static int Today() { return (int)CampaignTime.Now.ToDays; }

        // ============================================================ zapis
        // ks4|stan|kampania|doba A|plany|pierwszy plan|crc ziemi|crc ukladu|sigma|doba uzbrojenia|skrot uzbrojenia|skrot wlaczenia|flagi|beta|lambda|crc ziemi uzbrojenia
        //    |regula (srednie cen miast, rece regionu w sezonie)|KS-R3 pole dodatkowe (Z + base64 deflate)|okregi (Z + base64 deflate albo tekst)
        // recenzja 16.10 (wazne): rozmiar arm_ks w linii przy zapisie (projekt rozdz. 12: <= 150 KB); stawki wsi nie ida do zapisu (pierwsza doba po wczytaniu -
        // jak Undo, LiveDaily liczy nowe ze stanow ZC doby), lepszy stol w 3 znakach na pozycje, blok "sam start" jako *, cialo okregow skompresowane
        internal static string Export()
        {
            try
            {
                if (_keepRaw != null) return _keepRaw;            // zapis zastosowany bez pliku ziemi / ukladu - oddany bez zmian (nie nadpisany Planem)
                if (_state == StatePlan && _plans == 0) return "";
                var sb = new StringBuilder(96 * 1024);
                sb.Append(SaveVersion3).Append('|').Append(_state).Append('|').Append(Clean(_campaign)).Append('|').Append(_applyDay).Append('|').Append(_plans).Append('|')
                  .Append(_firstPlanDay).Append('|').Append(_saveLandCrc.ToString("X8")).Append('|').Append(_saveLayoutCrc.ToString("X8")).Append('|').Append(_saveSigma).Append('|')
                  .Append(_armDay).Append('|').Append(_armHash.ToString("X8")).Append('|').Append(_applyHash.ToString("X8")).Append('|')
                  .Append((_autotest ? 1 : 0) | (_shelvesDone ? 2 : 0)).Append('|');
                RampExport(sb); sb.Append('|');
                sb.Append(_armLandCrc.ToString("X8")).Append('|');
                if (_state == StateApplied) RuleExport(sb);
                sb.Append('|');
                int rawLive = 0, packed = 0;
                if (_state == StateApplied)
                {
                    var lb = new StringBuilder(256 * 1024);
                    ExportLive(lb);
                    rawLive = lb.Length;
                    string pk = Pack(lb.ToString());
                    packed = pk.Length;
                    sb.Append(pk);
                }
                string res = sb.ToString();
                int today = Today();
                if (_state == StateApplied && today != _exportLogged)
                {
                    _exportLogged = today;
                    int parts = (res.Length + 7999) / 8000;
                    int r3 = _r3Size;
                    Log.Info(PK + "zapis arm_ks doba " + today + " | " + (res.Length / 1024.0).ToString("0.0", Inv) + " KB (paczek SaveText " + parts + "; okregi " + (rawLive / 1024.0).ToString("0.0", Inv)
                             + " KB tekstu -> " + (packed / 1024.0).ToString("0.0", Inv) + " KB po kompresji) | limit projektu 150 KB " + (res.Length <= 150 * 1024 ? "TAK" : "PRZEKROCZONY")
                             + " | arm_ks_r3 (stan rundy 3, osobny klucz) " + (r3 >= 0 ? (r3 / 1024.0).ToString("0.0", Inv) + " KB" : "-") + ", razem " + ((res.Length + Math.Max(0, r3)) / 1024.0).ToString("0.0", Inv) + " KB.");
                }
                return res;
            }
            catch (Exception e) { Stumble("Export", e); return ""; }
        }
        private static int _exportLogged = -1, _r3Size = -1;

        /// <summary>Recenzja runda 3 (wazne): stan rundy 3 w osobnym kluczu arm_ks_r3 (starsza DLL czyta arm_ks ks3 i pomija ten klucz). Zapis zastosowany zachowany bez
        /// zmian (bez pliku ziemi) - oddany jak byl.</summary>
        internal static string ExportR3()
        {
            try
            {
                if (_keepRaw != null) { _r3Size = _r3Keep != null ? _r3Keep.Length : -1; return _r3Keep ?? ""; }
                if (_state == StatePlan && _plans == 0) { _r3Size = -1; return ""; }
                string s = ExportExtra();
                _r3Size = s != null ? s.Length : -1;
                return s ?? "";
            }
            catch (Exception e) { Stumble("ExportR3", e); _r3Size = -1; return ""; }
        }

        internal static void ImportR3(string data) { _r3Pending = string.IsNullOrEmpty(data) ? null : data; }

        /// <summary>Cialo okregow w zapisie: "Z" + base64(deflate(UTF-8)) albo sam tekst (krotki). Base64 bez '|'.</summary>
        private static string Pack(string s)
        {
            if (string.IsNullOrEmpty(s) || s.Length < 2048) return s ?? "";
            var raw = Encoding.UTF8.GetBytes(s);
            using (var ms = new System.IO.MemoryStream(raw.Length / 3 + 64))
            {
                using (var z = new System.IO.Compression.DeflateStream(ms, System.IO.Compression.CompressionLevel.Optimal, true)) z.Write(raw, 0, raw.Length);
                return "Z" + Convert.ToBase64String(ms.ToArray());
            }
        }

        private static string Unpack(string s)
        {
            if (string.IsNullOrEmpty(s) || s[0] != 'Z') return s;
            var data = Convert.FromBase64String(s.Substring(1));
            using (var ms = new System.IO.MemoryStream(data))
            using (var z = new System.IO.Compression.DeflateStream(ms, System.IO.Compression.CompressionMode.Decompress))
            using (var rd = new System.IO.StreamReader(z, Encoding.UTF8)) return rd.ReadToEnd();
        }

        private static string Clean(string s) { return string.IsNullOrEmpty(s) ? "-" : s.Replace('|', '/').Replace(';', ',').Replace('~', '-').Replace('!', '.'); }

        internal static void Import(string data)
        {
            _pending = string.IsNullOrEmpty(data) ? null : data;
        }

        /// <summary>Rozwiazanie zapisu (OnSessionLaunched): naglowek, a przy stanie "zastosowane" - zajecia blokow (wymaga pliku ziemi o tym samym crc i ukladu wiosek).
        /// Formaty ks2 (bez crc ziemi uzbrojenia i pamieci reguly) i ks1 (etap 1: bez uzbrojenia i rampy - zapis zastosowany ks1 = rampa juz za nami) czytane.
        /// Recenzja 16.10 (drobne): brak pliku ziemi albo nieczynna ksiega wiosek (uklad nieznany) - stan zastosowany ZACHOWANY bez zmian (Export oddaje surowy napis),
        /// produkcja jak dzis, dopoki plik / ksiega nie wroci; stan porzucony tylko przy INNYM crc pliku ziemi, innym znanym ukladzie wiosek albo innym id kampanii.</summary>
        private static void ResolvePending()
        {
            if (_pending == null) { if (_r3Pending != null) { string x0 = _r3Pending; _r3Pending = null; try { ImportExtra(x0); } catch (Exception xe) { Stumble("ImportExtra", xe); } } return; }
            string data = _pending; _pending = null;
            // recenzja runda 3 (drobne): stan rundy 3 dopiero po przyjeciu naglowka (dotad przed sprawdzeniem kampanii i ukladu - z odrzuconego zapisu zostawal gradient, kx ...)
            string extra = _r3Pending; _r3Pending = null;
            int r3 = 0;                                          // 0 - porzucony, 1 - wczytany, 2 - zachowany bez zmian (zapis zastosowany bez pliku ziemi albo ukladu)
            try
            {
                bool v4 = data.StartsWith(SaveVersion + "|", StringComparison.Ordinal);
                bool v3 = v4 || data.StartsWith(SaveVersion3 + "|", StringComparison.Ordinal), v2 = data.StartsWith(SaveVersion2 + "|", StringComparison.Ordinal);
                int nf = v4 ? 19 : (v3 ? 18 : (v2 ? 16 : 10));
                var h = data.Split(new[] { '|' }, nf);
                int st, ad, pl, fp, sg; uint lc, yc;
                if ((v3 || v2 ? h.Length < nf : (h.Length < 10 || h[0] != SaveVersion1)) || !int.TryParse(h[1], NumberStyles.Integer, Inv, out st) || !int.TryParse(h[3], NumberStyles.Integer, Inv, out ad)
                    || !int.TryParse(h[4], NumberStyles.Integer, Inv, out pl) || !int.TryParse(h[5], NumberStyles.Integer, Inv, out fp)
                    || !uint.TryParse(h[6], NumberStyles.HexNumber, Inv, out lc) || !uint.TryParse(h[7], NumberStyles.HexNumber, Inv, out yc) || !int.TryParse(h[8], NumberStyles.Integer, Inv, out sg))
                { _loadNote = "zapis arm_ks w nieznanym formacie - pominiety (Plan od nowa, produkcja jak dzis)"; r3 = 0; return; }
                int armDay = -1, flags = 0; uint armHash = 0, applyHash = 0, armLand = 0;
                string body = h[9], rule = null;
                if (v3 || v2)
                {
                    int.TryParse(h[9], NumberStyles.Integer, Inv, out armDay);
                    uint.TryParse(h[10], NumberStyles.HexNumber, Inv, out armHash); uint.TryParse(h[11], NumberStyles.HexNumber, Inv, out applyHash);
                    int.TryParse(h[12], NumberStyles.Integer, Inv, out flags);
                    if (v3) { uint.TryParse(h[15], NumberStyles.HexNumber, Inv, out armLand); rule = h[16]; body = v4 ? h[18] : h[17]; }
                    else body = h[15];
                }
                if (v4 && extra == null) extra = h[17];               // zapis ks4 (DLL z 17.10 rano): pole dodatkowe w arm_ks
                _plans = Math.Max(0, pl); _firstPlanDay = fp; _campaign = h[2] == "-" ? "" : h[2];
                string gid = "";
                try { gid = Campaign.Current != null ? Campaign.Current.UniqueGameId ?? "" : ""; } catch { gid = ""; }
                if (st == StateArmed)
                {
                    if (gid.Length > 0 && _campaign.Length > 0 && gid != _campaign) { _loadNote = "zapis UZBROJONY innej kampanii (id " + _campaign + ", gra " + gid + ") - uzbrojenie pominiete, Plan"; _campaign = ""; r3 = 0; return; }
                    r3 = 1;
                    _state = StateArmed; _armDay = armDay; _armHash = armHash; _armLandCrc = armLand;
                    _loadNote = "zapis UZBROJONY od doby " + armDay + " (skrot planu " + armHash.ToString("X8") + ", plik ziemi " + armLand.ToString("X8") + "; " + _plans + " planow od doby " + _firstPlanDay + ") - czeka na Confirm";
                    return;
                }
                if (st != StateApplied) { _loadNote = "zapis: Plan (" + _plans + " planow od doby " + _firstPlanDay + ")"; r3 = 1; return; }
                if (gid.Length > 0 && _campaign.Length > 0 && gid != _campaign) { _loadNote = "zapis ZASTOSOWANY innej kampanii (id " + _campaign + ", gra " + gid + ") - stan pominiety, produkcja jak dzis"; _plans = 0; return; }
                EnsureLand();
                uint lay;
                if (!LandReady) { _keepRaw = data; r3 = 2; _loadNote = "zapis ZASTOSOWANY, ale BRAK pliku ziemi " + LandFile + " - stan ZACHOWANY w zapisie bez zmian (produkcja i popyt jak dzis, Plan tylko liczy), wroci, gdy plik wroci do ModuleData"; return; }
                if (lc != _landCrc) { _loadNote = "zapis ZASTOSOWANY z innym plikiem ziemi (crc " + lc.ToString("X8") + ", plik " + _landCrc.ToString("X8") + ") - stan pominiety, produkcja jak dzis, Plan od nowa"; _plans = 0; return; }
                uint known;
                if (!VillageBook.LayoutCrcNow(out known)) { _keepRaw = data; r3 = 2; _loadNote = "zapis ZASTOSOWANY, ale ksiega wiosek nieczynna (uklad nieznany) - stan ZACHOWANY w zapisie bez zmian, produkcja jak dzis, wroci przy czynnej ksiedze"; return; }
                if (!LayoutMatches(out lay) || yc != lay) { _loadNote = "zapis ZASTOSOWANY z innym ukladem wiosek (crc " + yc.ToString("X8") + ", gra " + lay.ToString("X8") + ") - stan pominiety, produkcja jak dzis, Plan od nowa"; _plans = 0; return; }
                int nd, nb, badD;
                string live;
                try { live = Unpack(body); } catch (Exception ue) { Stumble("Unpack", ue); _loadNote = "zapis ZASTOSOWANY - blad rozpakowania zajec blokow - stan pominiety, produkcja jak dzis"; LiveReset(); return; }
                if (!ImportLive(live, out nd, out nb, out badD)) { _loadNote = "zapis ZASTOSOWANY - blad odczytu zajec blokow - stan pominiety, produkcja jak dzis"; LiveReset(); return; }
                _state = StateApplied; _applyDay = ad; _saveLandCrc = lc; _saveLayoutCrc = yc; _saveSigma = sg; r3 = 1;
                _armDay = armDay; _armHash = armHash; _armLandCrc = armLand; _applyHash = applyHash; _autotest = (flags & 1) != 0;
                if (v3 || v2) { _shelvesDone = (flags & 2) != 0; RampImport(h[13], h[14]); }
                else { _shelvesDone = true; RampImportFull(); }      // ks1: zapis etapu 1 (wlaczony bez rampy) - rampa i polki za nami
                string rn = rule != null ? RuleImport(rule) : "brak (zapis " + (v2 ? "ks2" : "ks1") + ")";
                _loadNote = "zapis ZASTOSOWANY od doby " + ad + " (D " + DDay + (v3 ? "" : (v2 ? ", format ks2" : ", format ks1 - bez rampy")) + "): okregow " + nd + ", blokow " + nb + (badD > 0 ? " (okregow pominietych " + badD + ")" : "") + ", sigma " + sg
                            + ", " + (_autotest ? "autotest" : "uzbrojenie doba " + armDay) + " | pamiec reguly: " + rn
                            + " | pierwsza doba po wczytaniu - stawki KS i koszyk ZC4 od pierwszej doby ze stanami ZC (do tego czasu produkcja i popyt jak dzis)";
            }
            catch (Exception e) { _loadNote = "blad odczytu arm_ks - stan pominiety"; LiveReset(); _state = StatePlan; Stumble("ResolvePending", e); r3 = 0; }
            finally
            {
                if (r3 == 1 && extra != null) { try { ImportExtra(extra); } catch (Exception xe) { Stumble("ImportExtra", xe); } }
                else if (r3 == 2) _r3Keep = extra;
                else if (r3 == 0 && extra != null) _loadNote += " | stan rundy 3 (arm_ks_r3) pominiety z zapisem";
            }
        }

        // ============================================================ start sesji i doba
        /// <summary>ArmouryBehavior.OnSessionLaunched (po VillageBook.Resolve i McmSettings.Apply): plik ziemi, zapis, linia startu.</summary>
        internal static void SessionStart()
        {
            try
            {
                EnsureLand();
                ResolvePending();
                if (_state == StateApplied) ResolveLive();
                try { ArmSessionStart(); } catch (Exception ae) { Stumble("ArmSessionStart", ae); }   // ks-arm: arm_ks_arm z zapisu (po stanie KS), krainy wrocone, linia
                uint lay; bool lm = LandReady && LayoutMatches(out lay);
                var s = Settings.Current;
                Log.Info(P + "start sesji | klucz Start Calibration " + (KeyOn ? "TAK (Plan co 7 dob - tylko liczy)" : "NIE (nic nie liczy; zastosowany stan - produkcja jak dzis)")
                         + " | autotest: wlaczenie przy pierwszym dojrzalym Planie (ksiega towarow >= " + MinLedgerDays + " dob) " + (s != null && (s.StartCalibrationAutotestApply || ApplyFlag()) ? (AutotestMode() ? "TAK (autotest czynny; " + (s.StartCalibrationAutotestApply ? "klucz" : "plik " + ApplyFlagName) + ")" : "klucz/plik TAK, ale to nie autotest - nic") : "NIE")
                         + " | uzbrojenie w zapisie: klucz Arm This Save " + (s != null && s.StartCalibrationArmThisSave ? "TAK (nastepna doba)" : "nie") + ", Confirm " + (s != null && s.StartCalibrationConfirm ? "TAK (nastepna doba)" : "nie")
                         + (LandGateReady ? "" : " (Confirm w grze ODRZUCANY do bramki krainy i bramek rozdz. 10)")
                         + " | stan: " + StateText() + (_loadNote.Length > 0 ? " (" + _loadNote + ")" : "")
                         + " | " + _landNote + " | uklad wiosek tej sesji " + (lm ? "zgodny z plikiem" : "NIEZGODNY albo ksiega wiosek nieczynna - wioski poboczne poza KS, Apply zablokowane")
                         + " | stany ZC (Daily Life Measure) " + (DailyLife.KeyOn ? "TAK" : "NIE - bez stanow ZC KS w stanie zastosowanym dziala jak Undo, wlaczenie zablokowane")
                         + " | premia mapy " + (s != null ? s.StartCalibrationMapInertia : 1.25f).ToString("0.00", Inv) + ", pola mieszczan " + (s == null || s.StartCalibrationTownFields ? "TAK" : "NIE")
                         + ", regula zmiany zajec po wlaczeniu " + (s == null || s.StartCalibrationSwitchRule ? "TAK" : "NIE (sam pomiar nacisku)") + ", przetwory miast (ZC5) " + (s == null || s.StartCalibrationTownCrafts ? "TAK" : "NIE")
                         + " | rampa: podaz " + RampSupply + " dob (A..D), popyt koszyka ZC4 od D przez " + (RampDemand - RampSupply) + " dob (D..D+14), polki startu w D, A7 i regale od D | potkniecia " + _stumbles + ".");
            }
            catch (Exception e) { Stumble("SessionStart", e); }
        }

        private static string StateText()
        {
            if (_keepRaw != null) return "ZASTOSOWANY w zapisie - zachowany bez zmian (bez pliku ziemi albo ukladu)";
            if (_state == StateApplied) return "ZASTOSOWANY od doby " + _applyDay + " (D " + DDay + ", " + (_autotest ? "autotest" : "uzbrojony doba " + _armDay) + ")";
            if (_state == StateArmed) return "UZBROJONY doba " + _armDay + " (skrot planu " + _armHash.ToString("X8") + ") - czeka na Confirm";
            return "Plan";
        }

        /// <summary>Tryb autotestu CrashScribe (LogGate: 1 pelne, 2 profil) - jedyne miejsce, gdzie klucz Start Calibration Autotest Apply cos robi.</summary>
        private static bool AutotestMode() { int m = LogGate.Mode; return m == 1 || m == 2; }

        /// <summary>Plan gotowy do wlaczenia: bez bledu i z DOJRZALA ksiega towarow (>= 28 dob sredniej w tej sesji - popyt 1:1 wojska, zamkow, dworow, budow i warsztatow).</summary>
        private static bool PlanMature() { return _plan != null && _plan.Error == null && _planIn != null && _planEma >= MinLedgerDays; }

        /// <summary>Plan poza kolejka 7 dob: ksiega towarow wlasnie dojrzala, a ostatni Plan liczony z krotszej - zeby wlaczenie (autotest) i uzbrojenie nie czekaly tygodnia.</summary>
        private static bool EarlyPlan(int day) { return _state != StateApplied && _emaDays >= MinLedgerDays && _planEma < MinLedgerDays && !_wBusy && _lastPlanDay != day; }

        /// <summary>ArmouryBehavior.OnDailyTick, po DailyLife.Daily (stany doby): wynik Planu z watku roboczego, nowy Plan co 7 dob, klucze uzbrojenia i wlaczenie,
        /// a w stanie zastosowanym - rampa, stawki wsi na jutro, popyt koszyka ZC4, regula zmiany zajec, regale, pola mieszczan, linie. Wewnatrz bloku MoneyLedger (MKs).</summary>
        internal static void Daily()
        {
            int today = Today(), day = today - 1;
            _stumblesDay = 0;
            try
            {
                KsRecipes.Ensure(Applied);   // KS runda 4: receptury jedzenia warsztatow wedlug wagi (jak tabela przetworow Planu) przy KS zastosowanym, poza nim - gra (wlasny try)
                if (!KeyOn)
                {
                    if (_state == StateApplied) _wasOff = true;     // Undo: przy ponownym wlaczeniu rampa od nowa
                    PlanCancel();
                    LivePublishOff();
                    if (!_offLogged) { _offLogged = true; Log.Info(P + "dzien " + day + " | WYLACZONY (Start Calibration = off) - bez Planu" + (_state == StateApplied ? "; stan zastosowany zachowany w zapisie, produkcja i popyt jak dzis (Undo)" : (_state == StateArmed ? "; uzbrojenie zachowane w zapisie" : "")) + "."); }
                    KeysIdle(day);
                    return;
                }
                _offLogged = false;
                if (Campaign.Current == null) return;
                EnsureLand();
                if (!LandReady) { KeysIdle(day); return; }
                long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
                try { LedgerEma(); } catch (Exception e) { Stumble("LedgerEma", e); }   // srednia ksiegi towarow - co dobe (popyt 1:1 Planu)
                try { GateCollect(); } catch (Exception e) { Stumble("GateCollect", e); }   // ks-arm: twarde bramki (zrodla bez nazwy, RemoveParty) - 7 pelnych dob
                try { MoneyEma(today); } catch (Exception e) { Stumble("MoneyEma", e); }   // krok 8: udzial kiesy wsi w utargu, zarobek klas miast (srednie 28 dob)
                // KS-R3: odbior miast (P3, KS-H), kasa 28 dob (krok 5), sterowanie rynku (P4), tabela dob miast KS-H (najwyzej 200 wywolan drogi na dobe)
                try { TakeDaily(today); } catch (Exception e) { Stumble("TakeDaily", e); }
                try { GoldDaily(today); } catch (Exception e) { Stumble("GoldDaily", e); }
                try { ControlDaily(today); } catch (Exception e) { Stumble("ControlDaily", e); }
                PricesDaily(today);                                   // indeksy cen miast (stan zastosowany - co 7 dob na zmiane; Plan - tylko w doby linii cen)
                PlanCollect(day);                                     // wynik Planu z watku roboczego (linie i ks-plan.csv)
                if (_keepRaw != null)
                {
                    KeysHeld(day);                                    // zapis zastosowany zachowany bez zmian - zadnych kluczy ani wlaczenia
                    if (day - _lastPlanDay >= 7 || _lastPlanDay == int.MinValue) PlanStart(day);
                }
                else if (_state != StateApplied)
                {
                    bool planOk = _plan != null && _plan.Error == null && _planIn != null;
                    if (ApplyWanted() && PlanMature()) Apply(today, true);
                    else
                    {
                        KeysDaily(today, planOk);
                        try { ArmDaily(today, planOk); } catch (Exception e) { Stumble("ArmDaily", e); }   // ks-arm: samo uzbrojenie i samo wlaczenie po pomiarze (klucz Start Calibration Auto Arm)
                    }
                    if (_state != StateApplied && (day - _lastPlanDay >= 7 || _lastPlanDay == int.MinValue || EarlyPlan(day))) PlanStart(day);
                }
                else
                {
                    KeysApplied(day);                                   // klucze uzbrojenia na zapisie juz zastosowanym - Off (inny zapis nigdy ich nie zobaczy)
                    if (_wasOff)
                    {
                        RampRestart(today);                             // klucz znow wlaczony po przerwie (Undo -> Redo) - rampa od nowa, bez skoku
                        try { LandRetry(today); } catch (Exception e) { Stumble("LandRetry", e); }   // ks-arm (recenzja): druga proba krain wroconych - tylko recznie, razem z rampa
                    }
                    try { LandGateDaily(today); } catch (Exception e) { Stumble("LandGateDaily", e); }   // ks-arm: bramka krainy (rozdz. 8) - PRZED stawkami jutra
                    LiveDaily(today);
                    if (day - _lastPlanDay >= 7 || _lastPlanDay == int.MinValue) PlanStart(day);   // Plan dalej co 7 dob - pomiar nacisku i lepszy stol (gra bez zmian)
                }
                double ms = (System.Diagnostics.Stopwatch.GetTimestamp() - t0) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
                AddMs(ms);
                // KS-R3 G1: tabela dob miast KS-H (najwyzej 200 wywolan drogi na dobe, gotowa w ok. 25 dob stanu Plan) - poza czasem doby KS (osobny czas w linii KS-H)
                try { KsFoodCarts.DaysTick(); } catch (Exception e) { Stumble("DaysTick", e); }
                int dCalls; double dMs = KsFoodCarts.TakeDaysMs(out dCalls);     // recenzja runda 3 (drobne): czas tabeli dob KS-H osobno (NKS-9b)
                if (LogGate.Lines)
                {
                    Log.Info(P + "dzien " + day + " | stan " + (_state == StateApplied ? "ZASTOSOWANY od doby " + _applyDay : (_state == StateArmed ? "Plan (UZBROJONY doba " + _armDay + ")" : "Plan"))
                             + " | Plan: " + (_wBusy ? "w toku (od doby " + _wDay + ")" : "czeka")
                             + ", ostatni gotowy doba " + _planDay + " (planow tego zapisu " + _plans + ", od doby " + _firstPlanDay + "; ksiega towarow " + _planEma + " dob w Planie, " + _emaDays + " w sesji, dojrzaly " + (PlanMature() ? "TAK" : "NIE") + ")"
                             + (_state == StateApplied ? " | " + RampText(today) + " | " + MoneyText() : "")
                             + " | czas doby KS " + ms.ToString("0.00", Inv) + " ms (mediana " + MedianMs().ToString("0.00", Inv) + " ms z " + _msN + " dob; bez watku roboczego)"
                             + " | tabela dob KS-H " + dMs.ToString("0.00", Inv) + " ms (" + dCalls + " wywolan drogi, " + KsFoodCarts.DaysPairsDone + "/" + KsFoodCarts.DaysPairs + " par)"
                             + " | potkniecia dzis " + _stumblesDay + " (sesja " + _stumbles + ").");
                    if (day % 7 == 0) { try { Log.Info(PriceLine(day)); } catch (Exception e) { Stumble("PriceLine", e); } }
                    try { string ml = MarketLine(); if (ml != null) Log.Info(ml); } catch (Exception e) { Stumble("MarketLine", e); }
                }
            }
            catch (Exception e) { Stumble("Daily", e); }
        }

        private const int MsKeep = 120;
        private static readonly double[] _ms = new double[MsKeep], _msTmp = new double[MsKeep];
        private static int _msN, _msPos;
        private static void AddMs(double ms) { _ms[_msPos] = ms; _msPos = (_msPos + 1) % MsKeep; if (_msN < MsKeep) _msN++; }
        private static double MedianMs()
        {
            if (_msN <= 0) return 0;
            Array.Copy(_ms, _msTmp, _msN); Array.Sort(_msTmp, 0, _msN);
            return _msN % 2 == 1 ? _msTmp[_msN / 2] : 0.5 * (_msTmp[_msN / 2 - 1] + _msTmp[_msN / 2]);
        }

        internal const string ApplyFlagName = "armoury-ks-wlacz.flag";

        /// <summary>Plik-przelacznik autotestu (jak proba zimy): Documents\Mount and Blade II Bannerlord\CrashScribe\armoury-ks-wlacz.flag - zamiast klucza MCM
        /// (MCM przepisuje ustawienia co godzine gry). Dziala tylko w autotescie.</summary>
        private static bool ApplyFlag()
        {
            try
            {
                var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                return System.IO.File.Exists(System.IO.Path.Combine(System.IO.Path.Combine(System.IO.Path.Combine(docs, "Mount and Blade II Bannerlord"), "CrashScribe"), ApplyFlagName));
            }
            catch { return false; }
        }

        /// <summary>Wlaczenie autotestu: klucz Start Calibration Autotest Apply albo plik-przelacznik + CrashScribe.Autotest.Active, uklad wiosek zgodny z plikiem ziemi.
        /// Nigdy w grze Jeffa (tam - uzbrojenie w zapisie i Confirm).</summary>
        private static bool ApplyWanted()
        {
            var s = Settings.Current;
            if (s == null || !AutotestMode() || !(s.StartCalibrationAutotestApply || ApplyFlag())) return false;
            if (AutoArmKey && AutoTestOn()) return false;        // ks-arm: autotest z plikiem armoury-ks-auto.flag (albo kluczem Auto Arm Autotest) - droga samego uzbrojenia (jak w grze), nie wlaczenie przy pierwszym Planie
            uint lay;
            return LayoutMatches(out lay) && DailyLife.KeyOn;
        }

        /// <summary>Recenzja 16.10: plik-przelacznik zuzyty przy wlaczeniu w autotescie (stan jest juz w zapisie) - nastepny autotest (np. bieg bazowy) go nie zobaczy.</summary>
        private static void ConsumeApplyFlag()
        {
            try
            {
                var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                string p = System.IO.Path.Combine(System.IO.Path.Combine(System.IO.Path.Combine(docs, "Mount and Blade II Bannerlord"), "CrashScribe"), ApplyFlagName);
                if (System.IO.File.Exists(p)) { System.IO.File.Delete(p); Log.Info(PK + "plik-przelacznik " + ApplyFlagName + " zuzyty (usuniety) przy wlaczeniu w autotescie."); }
            }
            catch (Exception e) { Stumble("ConsumeApplyFlag", e); }
        }

        /// <summary>Bramki wlaczenia w grze (Arm This Save i Confirm; projekt rozdz. 7, 10, 12 i recenzja 16.10): gotowy Plan z dojrzala ksiega towarow, Daily Life Measure,
        /// twarde bramki ZC1, nie w zimie (chyba ze bramka krainy i podloga spichlerza Famine - recenzja ks-arm), uklad wiosek zgodny. false - powod (log) i komunikat po angielsku.</summary>
        private static bool GameGates(bool planOk, out string why, out string en)
        {
            var s = Settings.Current;
            uint lay;
            bool winter = false;
            try { winter = WinterBite.WinterNow(); } catch { winter = false; }
            if (!planOk) { why = "brak gotowego Planu na tym zapisie (planow " + _plans + ")"; en = "no finished plan on this save yet - try again in a few days."; return false; }
            if (!PlanMature()) { why = "za malo dob ksiegi towarow w tej sesji (Plan liczony z " + _planEma + " dob sredniej, potrzeba " + MinLedgerDays + "; w sesji teraz " + _emaDays + ")"; en = "too few days of goods ledger this session (" + Math.Min(_emaDays, MinLedgerDays) + " of " + MinLedgerDays + ") - play on and try again."; return false; }
            if (!DailyLife.KeyOn) { why = "Daily Life Measure wylaczony - bez stanow ZC nie ma koszyka ZC4"; en = "Daily Life Measure is off - the people's basket cannot be reckoned."; return false; }
            if (s != null && (!s.BkRecipeYieldByWorkshop || !s.WhaleMeatFromWhalersOnly)) { why = "twarde bramki ZC1 (rozdz. 10): BkRecipeYieldByWorkshop " + (s.BkRecipeYieldByWorkshop ? "tak" : "NIE") + ", WhaleMeatFromWhalersOnly " + (s.WhaleMeatFromWhalersOnly ? "tak" : "NIE"); en = "switch on Bk Recipe Yield By Workshop and Whale Meat From Whalers Only first."; return false; }
            // recenzja ks-arm: projekt rozdz. 7 - w zimie tylko z podloga Famine I bramka krainy; przy Ks Granary No Floor (domyslnie) podlogi nie ma, a proby zimy z KS nie bylo -
            // wlaczenie czeka do odwilzy (jak przed paczka)
            bool floorOn = s != null && !s.KsGranaryNoFloor;
            if (winter && !(LandGateReady && floorOn)) { why = "zima (WinterBite) - wlaczenie w zimie tylko z bramka krainy i podloga spichlerza Famine (rozdz. 7; Ks Land Gate " + (LandGateReady ? "tak" : "NIE") + ", Ks Granary No Floor " + (floorOn ? "nie" : "TAK - bez podlogi") + ")"; en = "not in winter - try again after the thaw."; return false; }
            if (!LayoutMatches(out lay)) { why = "uklad wiosek niezgodny z plikiem ziemi"; en = "the village layout does not match the land file - nothing done."; return false; }
            why = en = null;
            return true;
        }

        // ============================================================ uzbrojenie w zapisie (rozdz. 12)
        /// <summary>Skrot planu: zajecia i regale wszystkich blokow (udzialy w promilach) - ten sam plan = ten sam skrot.</summary>
        private static uint PlanHash(KsResult r)
        {
            if (r == null || r.X == null) return 0;
            uint h = 2166136261;
            unchecked
            {
                for (int k = 0; k < r.X.Length; k++) { h = (h ^ (uint)(int)Math.Round(r.X[k] * 1000)) * 16777619; h = (h ^ (uint)r.PairCraft[k]) * 16777619; }
                if (r.RegHands != null) for (int i = 0; i < r.RegHands.Length; i++) if (r.RegHands[i] > 0) h = (h ^ (uint)i ^ (uint)(int)Math.Round(r.RegHands[i])) * 16777619;
            }
            return h == 0 ? 1u : h;
        }

        /// <summary>Klucz uzbrojenia albo potwierdzenia wylaczony w MCM i w pamieci (nastepna doba po zobaczeniu) - inny zapis nigdy go nie zobaczy. Plik ustawien MCM zapisany.</summary>
        private static void KeyOff(bool arm)
        {
            var s = Settings.Current;
            if (s != null) { if (arm) s.StartCalibrationArmThisSave = false; else s.StartCalibrationConfirm = false; }
            try
            {
                var i = McmSettings.Instance;
                if (i == null) return;
                if (arm) i.StartCalibrationArmThisSave = false; else i.StartCalibrationConfirm = false;
                try { var pv = MCM.Abstractions.BaseSettingsProvider.Instance; if (pv != null) pv.SaveSettings(i); } catch (Exception e) { Stumble("KeyOff(zapis MCM)", e); }
            }
            catch (Exception e) { Stumble("KeyOff", e); }
        }

        private static void Tell(string en)
        {
            try { TaleWorlds.Library.InformationManager.DisplayMessage(new TaleWorlds.Library.InformationMessage(new TaleWorlds.Localization.TextObject("{=!}" + en).ToString())); } catch { }
        }

        /// <summary>Klucz Start Calibration wylaczony albo bez pliku ziemi: klucze uzbrojenia wracaja na Off z linia (nic nie robia).</summary>
        private static void KeysIdle(int day)
        {
            var s = Settings.Current;
            if (s == null || (!s.StartCalibrationArmThisSave && !s.StartCalibrationConfirm)) return;
            bool a = s.StartCalibrationArmThisSave;
            KeyOff(true); KeyOff(false);
            Log.Info(PK + "dzien " + day + " | klucz " + (a ? "Arm This Save" : "Confirm") + " przy " + (KeyOn ? "braku pliku ziemi" : "wylaczonym Start Calibration") + " - nic nie robie, klucz wraca na Off.");
            Tell("Start calibration: " + (KeyOn ? "the land file is missing" : "Start Calibration is off") + " - nothing done, the switch is off again.");
        }

        /// <summary>Stan zastosowany: klucze Arm This Save i Confirm nic nie robia - wracaja na Off z linia i komunikatem.</summary>
        private static void KeysApplied(int day)
        {
            var s = Settings.Current;
            if (s == null || (!s.StartCalibrationArmThisSave && !s.StartCalibrationConfirm)) return;
            KeyOff(true); KeyOff(false);
            Log.Info(PK + "dzien " + day + " | klucz Arm This Save / Confirm na zapisie juz zastosowanym od doby " + _applyDay + " - nic nie robie, klucze wracaja na Off.");
            Tell("Start calibration is already running on this save - nothing done, the switch is off again.");
        }

        /// <summary>Zapis zastosowany zachowany bez zmian (bez pliku ziemi albo ukladu): klucze uzbrojenia nic nie robia - Off z linia i komunikatem.</summary>
        private static void KeysHeld(int day)
        {
            var s = Settings.Current;
            if (s == null || (!s.StartCalibrationArmThisSave && !s.StartCalibrationConfirm)) return;
            KeyOff(true); KeyOff(false);
            Log.Info(PK + "dzien " + day + " | klucz Arm This Save / Confirm na zapisie zastosowanym zachowanym bez zmian (" + _loadNote + ") - nic nie robie, klucze wracaja na Off.");
            Tell("Start calibration: this save's calibration is kept as it was until its land file is back - nothing done.");
        }

        /// <summary>Doba w stanie Plan albo Uzbrojony: klucz Arm This Save (uzbraja ten zapis), Confirm (wlacza od nastepnej doby). Kazdy klucz po jednej dobie wraca na Off.
        /// Recenzja 16.10: obie drogi przez bramki (GameGates); Confirm najwczesniej doba po uzbrojeniu (ten sam takt - nie), ten sam plik ziemi i skrot planu; w grze
        /// odrzucany, dopoki nie ma bramki krainy (LandGateReady).</summary>
        private static void KeysDaily(int today, bool planOk)
        {
            var s = Settings.Current;
            if (s == null) return;
            int day = today - 1;
            string gid = "";
            try { gid = Campaign.Current != null ? Campaign.Current.UniqueGameId ?? "" : ""; } catch { gid = ""; }
            string why, en;
            if (s.StartCalibrationArmThisSave)
            {
                KeyOff(true);
                if (_state == StateArmed && !(_autoArm && !AutoPathOn())) { Log.Info(PK + "dzien " + day + " | Arm This Save: ten zapis juz uzbrojony (doba " + _armDay + (_autoArm ? ", samo - wlaczy sie sam po pomiarze" : "") + ") - klucz wraca na Off."); Tell(_autoArm ? "Start calibration: this save is being surveyed and starts by itself when the survey is done." : "Start calibration: this save is already armed. Use Confirm after a week of plans."); }
                else if (!GameGates(planOk, out why, out en)) { Log.Info(PK + "dzien " + day + " | Arm This Save odrzucony: " + why + " - nic, klucz wraca na Off."); Tell("Start calibration: " + en); }
                else
                {
                    _state = StateArmed; _armDay = today; _armHash = PlanHash(_plan); _armLandCrc = _landCrc; _campaign = gid; _autoArm = false;   // ks-arm: uzbrojenie reczne (Confirm)
                    int ready = Math.Max(today + 1, _firstPlanDay + MinPlanDays);
                    Log.Info(PK + "uzbrojony zapis doba " + today + " | kampania " + (gid.Length > 0 ? gid : "-") + ", skrot planu " + _armHash.ToString("X8") + " (plan nr " + _plans + " doby " + _planDay + ", ksiega towarow " + _planEma + " dob)"
                             + ", plik ziemi " + _armLandCrc.ToString("X8") + ", Plan na tym zapisie od doby " + _firstPlanDay + " | Confirm najwczesniej doba " + ready + " (ten sam plan i plik ziemi)"
                             + (LandGateReady ? "" : " - w grze Confirm na razie ODRZUCANY (brak bramki krainy)") + " | nic w grze sie nie zmienia | klucz wraca na Off.");
                    Tell("Start calibration: this save is armed. Nothing changes yet. Switch on Confirm from day " + ready + " to start it.");
                }
            }
            if (s.StartCalibrationConfirm)
            {
                KeyOff(false);
                uint ph = PlanHash(_plan);
                if (_state != StateArmed) { Log.Info(PK + "klucz bez uzbrojenia tego zapisu - nic nie robie (dzien " + day + ", Confirm bez Arm This Save; klucz wraca na Off)."); Tell("Start calibration: this save is not armed - nothing done. Use Arm This Save first."); }
                else if (gid.Length > 0 && _campaign.Length > 0 && gid != _campaign) { Log.Info(PK + "klucz bez uzbrojenia tego zapisu - nic nie robie (dzien " + day + ", inny id kampanii " + gid + " / uzbrojony " + _campaign + ")."); Tell("Start calibration: this save is not the one that was armed - nothing done."); }
                else if (_autoArm && AutoPathOn()) { Log.Info(PK + "dzien " + day + " | Confirm: zapis uzbrojony sam (Start Calibration Auto Arm) - wlaczy sie sam po pomiarze i bramkach (linia 'KS: pomiar przed wlaczeniem'); klucz wraca na Off."); Tell("Start calibration: this save starts by itself when the survey is done."); }
                else if (today <= _armDay) { Log.Info(PK + "dzien " + day + " | Confirm odrzucony: ta sama doba co uzbrojenie (" + _armDay + ") - potwierdzenie najwczesniej nastepnej doby; klucz wraca na Off."); Tell("Start calibration: confirm on a later day than arming - nothing done."); }
                else if (_firstPlanDay < 0 || today - _firstPlanDay < MinPlanDays) { Log.Info(PK + "dzien " + day + " | Confirm: za wczesnie - Plan na tym zapisie od doby " + _firstPlanDay + ", potrzeba " + MinPlanDays + " dob (najwczesniej doba " + (_firstPlanDay + MinPlanDays) + ") - nic, klucz wraca na Off."); Tell("Start calibration: too early - wait until day " + (_firstPlanDay + MinPlanDays) + " and confirm again."); }
                else if (!GameGates(planOk, out why, out en)) { Log.Info(PK + "dzien " + day + " | Confirm odrzucony: " + why + " - nic, klucz wraca na Off."); Tell("Start calibration: " + en); }
                else if (_armLandCrc != _landCrc) { Log.Info(PK + "dzien " + day + " | Confirm odrzucony: plik ziemi inny niz przy uzbrojeniu (" + _armLandCrc.ToString("X8") + " / teraz " + _landCrc.ToString("X8") + ") - uzbroj zapis jeszcze raz; klucz wraca na Off."); Tell("Start calibration: the land file changed since arming - use Arm This Save again."); }
                else if (ph != _armHash) { Log.Info(PK + "dzien " + day + " | Confirm odrzucony: skrot planu " + ph.ToString("X8") + " inny niz przy uzbrojeniu " + _armHash.ToString("X8") + " (nowy Plan od doby " + _planDay + ") - uzbroj zapis jeszcze raz; klucz wraca na Off."); Tell("Start calibration: the plan has changed since this save was armed - use Arm This Save again."); }
                else if (!LandGateReady) { Log.Info(PK + "dzien " + day + " | Confirm odrzucony: bramka krainy wylaczona (klucz Ks Land Gate) - kalibracja w kampanii zablokowana; zapis zostaje uzbrojony, klucz wraca na Off."); Tell("Start calibration cannot start in a campaign while Ks Land Gate (the famine gate) is off. Nothing changed; the save stays armed."); }
                else if (!HardGatesConfirm(day)) { }
                else
                {
                    Log.Info(PK + "potwierdzone doba " + today + " | zapis uzbrojony doba " + _armDay + " (skrot " + _armHash.ToString("X8") + "), kampania " + (gid.Length > 0 ? gid : "-") + " | plan do wlaczenia nr " + _plans + " doby " + _planDay
                             + " (skrot " + ph.ToString("X8") + ")");
                    Apply(today, false);
                    Tell("Start calibration has begun: over the next 14 days the villages move to what their hands make on their land; the people's daily basket comes to the market over the 14 days after that.");
                }
            }
            if (_state == StateArmed && !_autoArm && LogGate.Lines && today != _keyLineDay && (today - _armDay) % 7 == 0)
            {
                _keyLineDay = today;
                Log.Info(PK + "dzien " + day + " | zapis uzbrojony doba " + _armDay + " - czeka na Confirm (najwczesniej doba " + Math.Max(_armDay + 1, _firstPlanDay + MinPlanDays) + (LandGateReady ? "" : "; w grze Confirm odrzucany do bramki krainy") + "); gra bez zmian.");
            }
        }
    }
}
