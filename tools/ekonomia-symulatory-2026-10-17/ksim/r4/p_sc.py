import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from edlib import rep
P = r'C:/Users/GAME/AppData/Local/Temp/claude/C--Users-GAME-Bannerlor2-Realm-of-Thrones/ae222cb7-a7c2-473a-8fc1-1dc1d6f688e3/scratchpad/zb/n-ks/Armoury/src/StartCalibration.cs'
pairs = []
pairs.append(("""        // KS-R3: ks4 = ks3 + pole dodatkowe (gradient krain, czynnik popytu targu, ciecie i polki krain, srednie sterowania, kx KS-H, tabela dob miast, podloga spichlerza wsi,
        // wydatki wsi spoza koszyka, odbior miast, kasa 28 dob) przed cialem okregow; ks3 czytany (gradient 1, kx 1, podloga wlaczona)
        private const string SaveVersion = "ks4", SaveVersion3 = "ks3", SaveVersion2 = "ks2", SaveVersion1 = "ks1";""",
"""        // KS-R3 (recenzja runda 3, wazne): zapis arm_ks dalej w formacie ks3 (starsza DLL czyta go jak dotad), stan rundy 3 (gradient krain, czynnik popytu targu, ciecie i polki
        // krain, srednie sterowania, kx KS-H, tabela dob miast, podloga spichlerza wsi, wydatki wsi spoza koszyka, odbior miast, kasa 28 dob, srednie dowozu KS-H) w osobnym
        // kluczu arm_ks_r3 (starsza DLL go pomija); ks4 (pole dodatkowe w arm_ks - DLL z 17.10 rano) czytany
        private const string SaveVersion = "ks4", SaveVersion3 = "ks3", SaveVersion2 = "ks2", SaveVersion1 = "ks1";
        private static string _r3Pending, _r3Keep;          // arm_ks_r3 z zapisu (do rozwiazania przy starcie sesji) i zachowany bez zmian (zapis zastosowany bez pliku ziemi)"""))
pairs.append(("""                sb.Append(SaveVersion).Append('|').Append(_state).Append('|').Append(Clean(_campaign)).Append('|').Append(_applyDay).Append('|').Append(_plans).Append('|')""",
"""                sb.Append(SaveVersion3).Append('|').Append(_state).Append('|').Append(Clean(_campaign)).Append('|').Append(_applyDay).Append('|').Append(_plans).Append('|')"""))
pairs.append(("""                if (_state == StateApplied) RuleExport(sb);
                sb.Append('|');
                string extra = ""; try { extra = ExportExtra(); } catch (Exception xe) { Stumble("ExportExtra", xe); extra = ""; }
                sb.Append(extra).Append('|');                       // KS-R3 ks4: pole dodatkowe (spakowane; bez '|')
                int rawLive = 0, packed = 0;""", """                if (_state == StateApplied) RuleExport(sb);
                sb.Append('|');
                int rawLive = 0, packed = 0;"""))
pairs.append(("""                    Log.Info(PK + "zapis arm_ks doba " + today + " | " + (res.Length / 1024.0).ToString("0.0", Inv) + " KB (paczek SaveText " + parts + "; okregi " + (rawLive / 1024.0).ToString("0.0", Inv)
                             + " KB tekstu -> " + (packed / 1024.0).ToString("0.0", Inv) + " KB po kompresji) | limit projektu 150 KB " + (res.Length <= 150 * 1024 ? "TAK" : "PRZEKROCZONY") + ".");""",
"""                    int r3 = _r3Size;
                    Log.Info(PK + "zapis arm_ks doba " + today + " | " + (res.Length / 1024.0).ToString("0.0", Inv) + " KB (paczek SaveText " + parts + "; okregi " + (rawLive / 1024.0).ToString("0.0", Inv)
                             + " KB tekstu -> " + (packed / 1024.0).ToString("0.0", Inv) + " KB po kompresji) | limit projektu 150 KB " + (res.Length <= 150 * 1024 ? "TAK" : "PRZEKROCZONY")
                             + " | arm_ks_r3 (stan rundy 3, osobny klucz) " + (r3 >= 0 ? (r3 / 1024.0).ToString("0.0", Inv) + " KB" : "-") + ", razem " + ((res.Length + Math.Max(0, r3)) / 1024.0).ToString("0.0", Inv) + " KB.");"""))
pairs.append(("""        private static int _exportLogged = -1;""", """        private static int _exportLogged = -1, _r3Size = -1;

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

        internal static void ImportR3(string data) { _r3Pending = string.IsNullOrEmpty(data) ? null : data; }"""))
# ResolvePending: extras only after the header is accepted
pairs.append(("""            if (_pending == null) return;
            string data = _pending; _pending = null;
            try
            {""", """            if (_pending == null) { if (_r3Pending != null) { string x0 = _r3Pending; _r3Pending = null; try { ImportExtra(x0); } catch (Exception xe) { Stumble("ImportExtra", xe); } } return; }
            string data = _pending; _pending = null;
            // recenzja runda 3 (drobne): stan rundy 3 dopiero po przyjeciu naglowka (dotad przed sprawdzeniem kampanii i ukladu - z odrzuconego zapisu zostawal gradient, kx ...)
            string extra = _r3Pending; _r3Pending = null;
            int r3 = 0;                                          // 0 - porzucony, 1 - wczytany, 2 - zachowany bez zmian (zapis zastosowany bez pliku ziemi albo ukladu)
            try
            {"""))
pairs.append(("""                { _loadNote = "zapis arm_ks w nieznanym formacie - pominiety (Plan od nowa, produkcja jak dzis)"; return; }""",
"""                { _loadNote = "zapis arm_ks w nieznanym formacie - pominiety (Plan od nowa, produkcja jak dzis)"; r3 = 0; return; }"""))
pairs.append(("""                if (v4) { try { ImportExtra(h[17]); } catch (Exception xe) { Stumble("ImportExtra", xe); } }   // KS-R3: gradient, sterowanie, kx, tabela dob, podloga ... (kazdy stan)""",
"""                if (v4 && extra == null) extra = h[17];               // zapis ks4 (DLL z 17.10 rano): pole dodatkowe w arm_ks"""))
pairs.append(("""                    if (gid.Length > 0 && _campaign.Length > 0 && gid != _campaign) { _loadNote = "zapis UZBROJONY innej kampanii (id " + _campaign + ", gra " + gid + ") - uzbrojenie pominiete, Plan"; _campaign = ""; return; }
                    _state = StateArmed; _armDay = armDay; _armHash = armHash; _armLandCrc = armLand;""",
"""                    if (gid.Length > 0 && _campaign.Length > 0 && gid != _campaign) { _loadNote = "zapis UZBROJONY innej kampanii (id " + _campaign + ", gra " + gid + ") - uzbrojenie pominiete, Plan"; _campaign = ""; r3 = 0; return; }
                    r3 = 1;
                    _state = StateArmed; _armDay = armDay; _armHash = armHash; _armLandCrc = armLand;"""))
pairs.append(("""                if (st != StateApplied) { _loadNote = "zapis: Plan (" + _plans + " planow od doby " + _firstPlanDay + ")"; return; }""",
"""                if (st != StateApplied) { _loadNote = "zapis: Plan (" + _plans + " planow od doby " + _firstPlanDay + ")"; r3 = 1; return; }"""))
pairs.append(("""                if (!LandReady) { _keepRaw = data; _loadNote""", """                if (!LandReady) { _keepRaw = data; r3 = 2; _loadNote"""))
pairs.append(("""                if (!VillageBook.LayoutCrcNow(out known)) { _keepRaw = data; _loadNote""", """                if (!VillageBook.LayoutCrcNow(out known)) { _keepRaw = data; r3 = 2; _loadNote"""))
pairs.append(("""                _state = StateApplied; _applyDay = ad; _saveLandCrc = lc; _saveLayoutCrc = yc; _saveSigma = sg;""", """                _state = StateApplied; _applyDay = ad; _saveLandCrc = lc; _saveLayoutCrc = yc; _saveSigma = sg; r3 = 1;"""))
rep(P, pairs)
# the catch/finally at the end of ResolvePending
s = open(P, encoding='utf-8', newline='').read()
a = """            catch (Exception e) { _loadNote = "blad odczytu arm_ks - stan pominiety"; LiveReset(); _state = StatePlan; Stumble("ResolvePending", e); }
        }"""
assert s.count(a) == 1
b = """            catch (Exception e) { _loadNote = "blad odczytu arm_ks - stan pominiety"; LiveReset(); _state = StatePlan; Stumble("ResolvePending", e); r3 = 0; }
            finally
            {
                if (r3 == 1 && extra != null) { try { ImportExtra(extra); } catch (Exception xe) { Stumble("ImportExtra", xe); } }
                else if (r3 == 2) _r3Keep = extra;
                else if (r3 == 0 && extra != null) _loadNote += " | stan rundy 3 (arm_ks_r3) pominiety z zapisem";
            }
        }"""
s = s.replace(a, b)
open(P, 'w', encoding='utf-8', newline='').write(s)
print('ok finally')
