using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// r4-koszyk czesc 2 - PLACE ZA BRAK RAK (Jeff 17.10 "place rosna same tam, gdzie brakuje rak"; 17.10 noc "Poprawka 2 - tak"; klucz Ks Wage By Hands):
    /// w miescie z zastosowana kalibracja startu (mapa koszyka ZC4) wskaznik plac pracy ludu (W i R) idzie raz na 7 dob:
    ///  - W GORE o Ks Wage Hands Step Percent (domyslnie 3%), najwyzej do Ks Wage Hands Max (x1.30), gdy pracodawcom brakuje rak:
    ///    (a) region miasta bez wolnych rak - mezczyzn w sluzbie, poleglych, w niewoli i na Murze co najmniej tyle, ile prog wolnych rak krainy (FreeHands a >= f; ta sama
    ///        definicja, z ktorej Plan KS bierze rece wsi - FreeHands.YieldFactor; w oknie zniw prog nizszy, wiec place zniw rosna) albo
    ///    (b) przetwory ZC5 miasta zajely >= 90% swoich rak przez ostatni tydzien (pary staja na "brak rak");
    ///  - W DOL o ten sam krok, najwyzej do 1.0 (stawka historyczna x poziom plac miasta - TownWage), gdy rak jest duzo: wolne rece regionu >= polowa progu (f - a >= f/2)
    ///    i przetwory zajete < 50%; inaczej bez zmiany.
    /// PLACI PRACODAWCA, nigdy nic z niczego: dzis place ludu miasta to robocizna handlu (W90/R10) i doplata do wartosci wyrobow (W70/R30) z kasy miasta (kupcy
    /// i rzemieslnicy - TownFolk.TownLive krok 2, FromCoffer: zloto schodzi z kasy i trafia do kiesy, najwyzej stan kasy); wskaznik mnozy obie kwoty, wiec wyzsza placa
    /// to mniej w kasie kupcow i wiecej w kiesach W i R (tozsamosc w linii: zaplacone z kas = przyjete w kiesach). Przetwory ZC5 licza w koszcie pracy ta sama placa
    /// (drozsza praca - mniej par sie oplaca). Warsztaty gry, rzemioslo 148 i naprawy - bez zmian (inne sciezki, poza KS).
    /// Zapis: arm_ks_koszyk (miasto:wskaznik:doba decyzji). Linia "Place za brak rak (r4-koszyk): dzien N" co dobe.
    /// </summary>
    internal static class KsWageHands
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private sealed class W
        {
            public float Idx = 1f; public int Last = -1; public byte State;         // 0 bez zmiany, 1 brak rak, 2 duzo wolnych rak, 3 bez decyzji (pierwszy tydzien, po wczytaniu)
            public double Avail, Used;                                                // rece przetworow ZC5 w tygodniu (roboczodni) - dostepne / zajete
            public float A = -1f, F = -1f, Use = -1f; public bool ByFree, ByCraft;   // ostatnia decyzja: obciazenie i prog regionu, zajecie przetworow, przyczyna
        }
        private static readonly Dictionary<string, W> _w = new Dictionary<string, W>(StringComparer.Ordinal);
        // liczniki doby: place z kas (robocizna + doplata) zaplacone / przyjete w kiesach, ponad stawke (za brak rak), miasta z wskaznikiem > 1
        private static long _dPaid, _dRecv; private static double _dExtra; private static int _dTowns, _dUp, _stum;
        private static int _decDay = -1;                                              // najnowsza doba decyzji (linia); liczby decyzji - z ostatniej decyzji kazdego miasta (W.State)
        private static readonly HashSet<string> _err = new HashSet<string>();

        internal static bool On { get { var s = Settings.Current; return s != null && s.KsWageByHands; } }
        private static double Step { get { var s = Settings.Current; return s != null ? Math.Max(0.0, Math.Min(0.10, s.KsWageHandsStepPercent / 100.0)) : 0.03; } }
        private static double Max { get { var s = Settings.Current; return s != null ? Math.Max(1.0, Math.Min(2.0, s.KsWageHandsMax)) : 1.3; } }

        internal static void Reset()
        {
            _w.Clear(); _dPaid = _dRecv = 0; _dExtra = 0; _dTowns = _dUp = 0; _stum = 0; _err.Clear();
            _decDay = -1;
        }

        private static void Stumble(string where, Exception e)
        {
            _stum++;
            try { if (_err.Add(where)) Log.Error("KsWageHands." + where, e); } catch { }
        }

        /// <summary>Czynnik plac miasta (1 = stawka jak dotad): klucz, miasto z KS zastosowanym, wskaznik z ostatniej decyzji. Przetwory ZC5 (koszt pracy).</summary>
        internal static double Factor(Settlement st)
        {
            try
            {
                if (!On || st == null || !st.IsTown || st.StringId == null || !StartCalibration.KsTownApplied(st.Town)) return 1.0;
                W w; return _w.TryGetValue(st.StringId, out w) ? Math.Max(1.0, Math.Min(Max, w.Idx)) : 1.0;
            }
            catch (Exception e) { Stumble("Factor", e); return 1.0; }
        }

        /// <summary>TownFolk.TownLive (raz na dobe na miasto, przed placami z kasy): decyzja tygodnia (co 7 dob od pierwszej doby miasta) i czynnik plac doby.</summary>
        internal static double Day(Settlement st, int day)
        {
            try
            {
                if (!On || st == null || st.Town == null || st.StringId == null || !StartCalibration.KsTownApplied(st.Town)) return 1.0;
                W w;
                if (!_w.TryGetValue(st.StringId, out w)) { w = new W { Last = day, State = 3 }; _w[st.StringId] = w; }
                else if (w.Last < 0 || day < w.Last) w.Last = day;                       // wczytany zapis z innej doby / pierwsza doba po wczytaniu
                else if (day - w.Last >= 7) { Decide(st, w, day); w.Last = day; }
                return Math.Max(1.0, Math.Min(Max, w.Idx));
            }
            catch (Exception e) { Stumble("Day", e); return 1.0; }
        }

        private static void Decide(Settlement st, W w, int day)
        {
            double a, f;
            bool fh = FreeHands.AF(st, out a, out f) && f < 0.999;
            bool craft = w.Avail > 0;
            double use = craft ? w.Used / w.Avail : 0.0;
            bool byFree = fh && a >= f, byCraft = craft && use >= 0.9;
            bool shortH = byFree || byCraft;
            bool slack = !shortH && (!fh || (f - a) >= 0.5 * f) && (!craft || use < 0.5);
            double step = Step, max = Max, idx = Math.Max(1.0, Math.Min(max, w.Idx));
            if (shortH) idx = Math.Min(max, idx * (1.0 + step));
            else if (slack) idx = Math.Max(1.0, idx / (1.0 + step));
            w.Idx = (float)idx; w.State = (byte)(shortH ? 1 : slack ? 2 : 0);
            w.A = fh ? (float)a : -1f; w.F = fh ? (float)f : -1f; w.Use = craft ? (float)use : -1f; w.ByFree = byFree; w.ByCraft = byCraft;
            w.Avail = w.Used = 0;
            if (day > _decDay) _decDay = day;
            // poprawka recenzji (noc 17/18.10): liczby decyzji w linii licza sie z W.State kazdego miasta (DayLine) - miasta maja wlasne 7-dobowe terminy (wejscie do mapy ZC4,
            // zapis), wiec zerowanie licznikow przy kazdej nowej dobie decyzji pokazywalo tylko miasta z tej jednej doby
        }

        /// <summary>Przetwory ZC5 (StartCalibration.TownCraftsDay, co 2 doby): rece dostepne i zajete tej przerobki (roboczodni) - do decyzji tygodnia.</summary>
        internal static void NoteCraft(Settlement st, double avail, double used)
        {
            try
            {
                if (!On || st == null || st.StringId == null || !(avail > 0)) return;
                W w; if (!_w.TryGetValue(st.StringId, out w)) return;                    // miasto bez pierwszej doby plac (TownLive) - jeszcze bez decyzji
                w.Avail += avail; w.Used += Math.Max(0.0, Math.Min(avail, used));
            }
            catch (Exception e) { Stumble("NoteCraft", e); }
        }

        /// <summary>TownFolk.TownLive krok 2: place z kasy miasta tej doby - zeszlo z kasy (paid), przyjete w kiesach ludu (recv), czynnik plac (idx) - linia, tozsamosc.</summary>
        internal static void NotePaid(long paid, long recv, double idx)
        {
            _dPaid += paid; _dRecv += recv; _dTowns++;
            if (idx > 1.0 + 1e-9) { _dExtra += paid * (1.0 - 1.0 / idx); _dUp++; }
        }

        /// <summary>TownFolk.DailyLive (po miastach): linia doby. Bez klucza i bez liczb - nic.</summary>
        internal static void DayLine(int day)
        {
            try
            {
                if (_w.Count == 0 && _dTowns == 0) return;                               // bez KS zastosowanego (gra Jeffa bez KS) - bez linii
                var ix = new List<KeyValuePair<string, float>>();
                int n = 0, nShort = 0, nShortFree = 0, nShortCraft = 0, nSlack = 0, nHold = 0, nNone = 0;
                foreach (var kv in _w)
                {
                    var st = Settlement.Find(kv.Key);
                    if (st == null || st.Town == null || !StartCalibration.KsTownApplied(st.Town)) continue;
                    n++;
                    var w0 = kv.Value;   // ostatnia decyzja miasta (okno 7 dob - kazde miasto decyduje raz na 7 dob)
                    if (w0.State == 1) { nShort++; if (w0.ByFree) nShortFree++; if (w0.ByCraft) nShortCraft++; }
                    else if (w0.State == 2) nSlack++;
                    else if (w0.State == 0) nHold++;
                    else nNone++;
                    ix.Add(new KeyValuePair<string, float>(st.Name != null ? st.Name.ToString().Replace(';', ',').Replace('|', '/') : kv.Key, Math.Max(1f, kv.Value.Idx)));
                }
                ix.Sort((x, y) => y.Value.CompareTo(x.Value));
                var vals = new List<float>(); foreach (var kv in ix) vals.Add(kv.Value);
                vals.Sort();
                float mn = vals.Count > 0 ? vals[0] : 1f, md = vals.Count > 0 ? vals[vals.Count / 2] : 1f, mx = vals.Count > 0 ? vals[vals.Count - 1] : 1f;
                int up = 0; foreach (var v in vals) if (v > 1.0005f) up++;
                var sb = new StringBuilder(900);
                sb.Append("Place za brak rak (r4-koszyk): dzien ").Append(day).Append(" | klucz Ks Wage By Hands ").Append(On ? "TAK" : "NIE")
                  .Append(", krok ").Append((100 * Step).ToString("0.0", Inv)).Append("%/7 dob, sufit x").Append(Max.ToString("0.00", Inv))
                  .Append(" | miasta z KS ").Append(n).Append(", decyzje tygodnia (ostatnia kazdego miasta, najnowsza dnia ").Append(_decDay).Append("): brak rak ").Append(nShort)
                  .Append(" (wolne rece regionu a >= f ").Append(nShortFree).Append(", przetwory ZC5 zajete >= 90% ").Append(nShortCraft).Append("), duzo wolnych rak ").Append(nSlack)
                  .Append(", bez zmiany ").Append(nHold).Append(", bez decyzji jeszcze ").Append(nNone)
                  .Append(" | wskaznik plac min ").Append(mn.ToString("0.000", Inv)).Append(" / mediana ").Append(md.ToString("0.000", Inv)).Append(" / max ").Append(mx.ToString("0.000", Inv))
                  .Append(", miasta > 1: ").Append(up);
                if (up > 0)
                {
                    sb.Append(" (");
                    int k = 0;
                    foreach (var kv in ix) { if (kv.Value <= 1.0005f || k >= 12) break; sb.Append(k == 0 ? "" : ", ").Append(kv.Key).Append(' ').Append(kv.Value.ToString("0.000", Inv)); k++; }
                    sb.Append(')');
                }
                sb.Append(" | doba: kasy miast zaplacily robocizne i doplate ").Append(_dPaid).Append(" zl, kiesy ludu przyjely ").Append(_dRecv).Append(" zl (roznica ").Append(_dPaid - _dRecv)
                  .Append("), z tego ponad stawke (place za brak rak) ").Append(Math.Round(_dExtra).ToString("0", Inv)).Append(" zl w ").Append(_dUp).Append(" miastach (miast z placami ").Append(_dTowns).Append(')')
                  .Append(" | potkniecia ").Append(_stum).Append('.');
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Stumble("DayLine", e); }
            finally { _dPaid = _dRecv = 0; _dExtra = 0; _dTowns = _dUp = 0; _stum = 0; }
        }

        // ------------------------------------------------------------ zapis (klucz arm_ks_koszyk, razem z dokupiona podstawa Planu)
        /// <summary>"miasto:wskaznik:doba decyzji,..." (tylko wskaznik > 1 albo z doba decyzji).</summary>
        internal static string Export()
        {
            var sb = new StringBuilder();
            foreach (var kv in _w)
            {
                if (string.IsNullOrEmpty(kv.Key) || kv.Key.IndexOfAny(new[] { ':', ',', '|' }) >= 0 || kv.Value == null) continue;
                if (sb.Length > 0) sb.Append(',');
                sb.Append(kv.Key).Append(':').Append(Math.Max(1f, Math.Min(2f, kv.Value.Idx)).ToString("0.####", Inv)).Append(':').Append(kv.Value.Last.ToString(Inv));
            }
            return sb.ToString();
        }

        /// <summary>Odczyt; zly wpis - pomijany; straznik rozmiaru (najwyzej 4000 miast, wskaznik 1..2).</summary>
        internal static void Import(string s)
        {
            _w.Clear();
            if (string.IsNullOrEmpty(s) || s.Length > 200000) return;
            foreach (var ent in s.Split(','))
            {
                if (_w.Count >= 4000) break;
                var f = ent.Split(':');
                float v; int d;
                if (f.Length != 3 || f[0].Length == 0 || !float.TryParse(f[1], NumberStyles.Float, Inv, out v) || !(v >= 1f && v <= 2f) || !int.TryParse(f[2], NumberStyles.Integer, Inv, out d)) continue;
                _w[f[0]] = new W { Idx = v, Last = d, State = 3 };   // bez decyzji w tej sesji (linia: "bez decyzji jeszcze" do pierwszej decyzji po wczytaniu)
            }
        }

        /// <summary>Klucz zapisu arm_ks_koszyk: "k1|place|dokupiona podstawa Planu".</summary>
        internal static string ExportAll()
        {
            string a = "", b = "";
            try { a = Export(); } catch (Exception e) { Stumble("Export", e); }
            try { b = StartCalibration.ExportCheap(); } catch (Exception e) { Stumble("ExportCheap", e); }
            return "k1|" + a + "|" + b;
        }

        /// <summary>Odczyt klucza arm_ks_koszyk; brak klucza (stary zapis) albo inny format - wskazniki 1, bez dokupionej podstawy (do pierwszego Planu).</summary>
        internal static void ImportAll(string s)
        {
            if (string.IsNullOrEmpty(s) || !s.StartsWith("k1|", StringComparison.Ordinal) || s.Length > 600000) { _w.Clear(); StartCalibration.ImportCheap(null); return; }
            var p = s.Split('|');
            try { Import(p.Length > 1 ? p[1] : null); } catch (Exception e) { _w.Clear(); Stumble("Import", e); }
            try { StartCalibration.ImportCheap(p.Length > 2 ? p[2] : null); } catch (Exception e) { Stumble("ImportCheap", e); }
        }
    }
}
