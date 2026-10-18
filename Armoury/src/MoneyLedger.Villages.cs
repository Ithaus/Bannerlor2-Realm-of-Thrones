using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// f14-wsie (recenzja 18.10, "ogon biednych wsi": fin91 59 -> 81 wsi z kiesa ponizej 1000 zl w 109203-109234 przy plaskiej sumie kies 5.02-5.06 mln; ksiegi
    /// zbiorcze NDS-2b / NDS-2d nie wykluczaja stalego przeplywu od biednych wsi do bogatych albo do panow) - PLIK wsie-kiesy.csv co 7 dob, wiersz na wies:
    ///  - kiesa na koncu okna i na jego poczatku (zmiana okna);
    ///  - zmiana kiesy w NASZYM ticku rozbita na pozycje ksiegi przeplywow (te same co w linii "Przeplywy osad (kiesy wsi)" - migawka kiesy kazdej wsi przy BlockOpen,
    ///    przy kazdym Mark i w Daily, jak migawki klas w Snap): renty (czynsz DS W3), budowy (zbytki z nadwyzki - towar), korona, budzet rodow 166 (sakiewki zwolnionych),
    ///    markietani, kiesa ludu (koszyk wsi ZC4 / stary, roboty gminy, dziesiecina w ticku, danina od skarbca), KS, KS-H, ZC6, reszta pozycji;
    ///  - poza tickiem = zmiana okna - suma pozycji ticku (utarg wozow po udziale pana i daninach przy wozie, przelewy gry, sakiewki rozbitych partii itd.);
    ///  - woz: gotowka oddana przez wozy wsi i przyrost kiesy netto po wszystkich krokach okna powrotu (VillageTakings.EnterPostfix: 112, F4b, prace okregu, daniny DS,
    ///    splata drewna f5) - wies, ktorej kiesa urosla (macierzysta albo obca, gdy woz oddal utarg w obcej wsi);
    ///  - stan wsi (spladrowana, najezdzana), miasto targowe (TradeBound, inaczej Bound) i jego oblezenie, typ wsi, ogniska, skrzynia DS tej doby (DsVillage.ChestToday).
    /// Tylko odczyt - gra bez zmian (zadnej zmiany zlota). Zbieranie tylko przy wlaczonym logu (Settings.LogEnabled); plik w dobie podzielnej przez 7 przy LogGate.Lines
    /// (jak ks-miasta.csv). Kazdy krok we wlasnym try, potkniecia liczone - bez globalnych wylacznikow (CLAUDE.md rozdz. 7). Okno od wczytania albo od ostatniego pliku.
    /// </summary>
    internal static partial class MoneyLedger
    {
        private const string VilCsvName = "wsie-kiesy.csv";
        private const string VilCsvHeader = "dzien;dob_okna;wies_id;wies;typ;krolestwo;miasto_targowe_id;stan;targ_oblezony;ogniska;kiesa;kiesa_poczatek_okna;zmiana_okna;skrzynia_ds;"
                                          + "poza_tickiem;wozow;woz_oddane;woz_netto_do_kiesy;tick_renty;tick_budowy;tick_korona;tick_budzet166;tick_markietani;tick_kiesa_ludu;"
                                          + "tick_ks;tick_ksh;tick_zc6;tick_inne";
        private const int VCols = 10;                                  // kolumny pozycji ticku (VCol)
        private static Village[] _vlList;                              // wsie w kolejnosci migawek (Village.All - stale w sesji; inna liczba - lista i okno od nowa)
        private static Dictionary<Village, int> _vlIx;
        private static int[] _vlSnap;                                  // kiesa przy ostatniej migawce bloku (BlockOpen / Mark)
        private static long[] _vlStart;                                // kiesa na poczatku okna pliku
        private static long[] _vlTick;                                 // [wies * VCols + kolumna] - zmiana kiesy w naszym ticku od poczatku okna
        private static long[] _vlCartHanded, _vlCartNet;
        private static int[] _vlCartN;
        private static bool _vlOn, _vlSnapOk;                          // zbieranie tej doby (log wlaczony), migawka bloku wazna
        private static int _vlFrom = -1, _vlCsvDay = -1, _vlStumbles;
        private static bool _vlErrLogged;

        private static void VilReset()
        {
            _vlList = null; _vlIx = null; _vlSnap = null; _vlStart = null; _vlTick = null; _vlCartHanded = null; _vlCartNet = null; _vlCartN = null;
            _vlOn = false; _vlSnapOk = false; _vlFrom = -1; _vlCsvDay = -1; _vlStumbles = 0; _vlErrLogged = false;
        }

        private static void VilStumble(string where, Exception e)
        {
            _vlStumbles++;
            if (!_vlErrLogged) { _vlErrLogged = true; Log.Error("MoneyLedger." + where + " (wsie-kiesy.csv)", e); }
        }

        /// <summary>Pozycja ksiegi (Mark) -> kolumna pliku: renty, budowy, korona, budzet 166, markietani, kiesa ludu, KS, KS-H, ZC6, reszta.</summary>
        private static int VCol(int kind)
        {
            switch (kind)
            {
                case MRent: return 0;
                case MBuild: return 1;
                case MCrown: return 2;
                case MBudget: return 3;
                case MSutlers: return 4;
                case MFolk: return 5;
                case MKs: return 6;
                case MKsH: return 7;
                case MZc6: return 8;
                default: return 9;
            }
        }

        /// <summary>Lista wsi i tablice okna; nowa lista (pierwsza doba albo inna liczba wsi) - okno od nowa od biezacych kies. Zwraca false, gdy brak wsi.</summary>
        private static bool VilEnsure()
        {
            var all = Village.All;
            int n = all != null ? all.Count : 0;
            if (n <= 0) return false;
            if (_vlList != null && _vlList.Length == n) return true;
            var list = new Village[n];
            var ix = new Dictionary<Village, int>(n);
            for (int i = 0; i < n; i++) { list[i] = all[i]; if (all[i] != null) ix[all[i]] = i; }
            _vlList = list; _vlIx = ix;
            _vlSnap = new int[n]; _vlStart = new long[n]; _vlTick = new long[n * VCols];
            _vlCartHanded = new long[n]; _vlCartNet = new long[n]; _vlCartN = new int[n];
            for (int i = 0; i < n; i++) { var v = list[i]; _vlStart[i] = v != null ? v.Gold : 0; }
            _vlFrom = (int)CampaignTime.Now.ToDays;
            _vlSnapOk = false;
            return true;
        }

        /// <summary>BlockOpen: migawka kiesy kazdej wsi przed pierwszym modulem naszego ticku. Log wylaczony - nic (zero kosztu).</summary>
        private static void VilBlockOpen()
        {
            _vlSnapOk = false;
            try
            {
                var s = Settings.Current;
                _vlOn = s != null && s.LogEnabled;
                if (!_vlOn || !VilEnsure()) return;
                var l = _vlList;
                for (int i = 0; i < l.Length; i++) _vlSnap[i] = l[i] != null ? l[i].Gold : 0;
                _vlSnapOk = true;
            }
            catch (Exception e) { _vlSnapOk = false; VilStumble("VilBlockOpen", e); }
        }

        /// <summary>Mark(kind) i Daily (reszta bloku - MRest): zmiana kiesy kazdej wsi od poprzedniej migawki idzie na kolumne pozycji. Tylko liczniki.</summary>
        private static void VilMark(int kind)
        {
            try
            {
                if (!_vlOn || !_vlSnapOk || _vlList == null) return;
                int c = VCol(kind);
                var l = _vlList;
                for (int i = 0; i < l.Length; i++)
                {
                    var v = l[i]; if (v == null) continue;
                    int g = v.Gold;
                    int d = g - _vlSnap[i];
                    if (d != 0) { _vlTick[i * VCols + c] += d; _vlSnap[i] = g; }
                }
            }
            catch (Exception e) { VilStumble("VilMark", e); }
        }

        /// <summary>VillageTakings.EnterPostfix (koniec okna powrotu wozu): `v` - wies, ktorej kiesa urosla, `handed` - gotowka oddana przez woz, `net` - przyrost kiesy
        /// po wszystkich krokach okna. Tylko licznik; nie rzuca.</summary>
        internal static void NoteVillageCart(Village v, int handed, long net)
        {
            try
            {
                if (!_vlOn || v == null || _vlIx == null) return;
                int i;
                if (!_vlIx.TryGetValue(v, out i)) return;
                _vlCartN[i]++; _vlCartHanded[i] += Math.Max(0, handed); _vlCartNet[i] += net;
            }
            catch (Exception e) { VilStumble("NoteVillageCart", e); }
        }

        /// <summary>Daily (po reszcie bloku na MRest): w dobie podzielnej przez 7 przy LogGate.Lines - plik wsie-kiesy.csv i nowe okno. Wlasny try.</summary>
        private static void VilDaily(int day)
        {
            try
            {
                if (!_vlOn || _vlList == null || _vlStart == null) return;
                if (day % 7 != 0 || day == _vlCsvDay || !LogGate.Lines) return;
                _vlCsvDay = day;
                var inv = CultureInfo.InvariantCulture;
                var l = _vlList;
                int now = (int)CampaignTime.Now.ToDays;
                int span = _vlFrom >= 0 ? Math.Max(0, now - _vlFrom) : 0;
                var sb = new StringBuilder(l.Length * 200);
                int rows = 0, bad = 0;
                long sumChange = 0, sumOut = 0;
                for (int i = 0; i < l.Length; i++)
                {
                    try
                    {
                        var v = l[i]; if (v == null || v.Settlement == null) continue;
                        var st = v.Settlement;
                        long g = v.Gold, g0 = _vlStart[i], ch = g - g0, tick = 0;
                        for (int c = 0; c < VCols; c++) tick += _vlTick[i * VCols + c];
                        long outside = ch - tick;
                        sumChange += ch; sumOut += outside;
                        var mk = v.TradeBound ?? v.Bound;
                        string state = v.VillageState == Village.VillageStates.Looted ? "spladrowana" : (v.VillageState == Village.VillageStates.BeingRaided ? "najezdzana" : "normalna");
                        double chest = -1; try { chest = DsVillage.ChestToday(st); } catch { chest = -1; }
                        string name = st.Name != null ? st.Name.ToString().Replace(';', ',') : st.StringId;
                        string fac = st.MapFaction != null && st.MapFaction.Name != null ? st.MapFaction.Name.ToString().Replace(';', ',') : "-";
                        sb.Append(day).Append(';').Append(span).Append(';').Append(st.StringId).Append(';').Append(name).Append(';')
                          .Append(v.VillageType != null ? v.VillageType.StringId : "-").Append(';').Append(fac).Append(';').Append(mk != null ? mk.StringId : "-").Append(';')
                          .Append(state).Append(';').Append(mk != null && mk.IsUnderSiege ? 1 : 0).Append(';').Append(v.Hearth.ToString("0", inv)).Append(';')
                          .Append(g).Append(';').Append(g0).Append(';').Append(ch).Append(';').Append(chest >= 0 ? chest.ToString("0", inv) : "-").Append(';')
                          .Append(outside).Append(';').Append(_vlCartN[i]).Append(';').Append(_vlCartHanded[i]).Append(';').Append(_vlCartNet[i]);
                        for (int c = 0; c < VCols; c++) sb.Append(';').Append(_vlTick[i * VCols + c]);
                        sb.Append(Environment.NewLine);
                        rows++;
                    }
                    catch (Exception e) { bad++; VilStumble("VilDaily(wies)", e); }
                }
                string path = Log.Csv(VilCsvName, VilCsvHeader, sb.ToString());
                Log.Info("Kiesy wsi wies po wsi (f14-wsie): " + VilCsvName + " " + (path != null ? rows + " wierszy" : "NIE ZAPISANY (log wylaczony albo brak katalogu sesji)")
                         + " (doba " + day + ", okno " + span + " dob) | zmiana kies okna " + sumChange.ToString(inv) + ", w tym poza tickiem " + sumOut.ToString(inv)
                         + " | potkniecia wiersza " + bad + ", razem " + _vlStumbles + ".");
                // nowe okno: poczatek = kiesa teraz, liczniki od zera (migawka bloku zostaje - kolejny BlockOpen i tak ja odswiezy)
                for (int i = 0; i < l.Length; i++) _vlStart[i] = l[i] != null ? l[i].Gold : 0;
                Array.Clear(_vlTick, 0, _vlTick.Length); Array.Clear(_vlCartHanded, 0, _vlCartHanded.Length); Array.Clear(_vlCartNet, 0, _vlCartNet.Length);
                Array.Clear(_vlCartN, 0, _vlCartN.Length);
                _vlFrom = now;
            }
            catch (Exception e) { VilStumble("VilDaily", e); }
        }
    }
}
