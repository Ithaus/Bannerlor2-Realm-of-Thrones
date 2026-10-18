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
    /// f4-place-ludu (klucz Ks Wage Hands Down, KsFolkWage.cs): przy duzej liczbie wolnych rak wskaznik schodzi dalej PONIZEJ 1 tym samym krokiem, najwyzej do Ks Wage Hands
    /// Floor - druga strona tej samej reguly. Wskaznik ponizej 1 tnie tylko czesc W robocizny i doplaty (TownFolk.TownLive krok 2; podloga przezycia - KsFolkWage.WFactor);
    /// Factor (koszt pracy przetworow ZC5 - rece rzemieslnikow) zostaje >= 1. Wskazniki ponizej 1 w osobnym kluczu zapisu arm_ks_place (arm_ks_koszyk jak dotad >= 1).
    /// Klucz wylaczony - podloga 1, co do liczby jak dotad. Recenzja 18.10: (1) w dol ponizej 1 tylko RAZEM z przegladem firm DS co kwartal (paczka P5, klucz Ds Farm
    /// Review Days > 0 - werdykt przegladu ekonomii: inaczej zloto przejdzie do kupcow i zostanie w kasach); po scaleniu fali 4 pole czytane wprost (przed scaleniem - refleksja)
    /// (wtedy (a) nieczynne, podloga 1); (2) ponizej 1 tylko przy DOWODZIE wolnych rak (dane regionu sa i f - a >= f/2; przetwory, jesli miasto je ma, zajete < 50%);
    /// brak danych (FreeHands nieczynny, region bez komorki, prog ~1, miasto bez przetworow) - wskaznik wraca do 1 z obu stron, nigdy nie schodzi ponizej 1.
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
        /// <summary>f4-place-ludu (a): wskaznik moze zejsc ponizej 1 (klucze Ks Wage By Hands i Ks Wage Hands Down, przeglad firm DS co kwartal czynny).</summary>
        internal static bool DownOn { get { var s = Settings.Current; return s != null && s.KsWageByHands && DownKey(s); } }
        /// <summary>f4-place-ludu (a): podloga wskaznika - Ks Wage Hands Floor (0.3..1) przy kluczu Ks Wage Hands Down i przegladzie firm DS co kwartal, inaczej 1 (jak dotad).</summary>
        private static double Floor { get { var s = Settings.Current; return s != null && DownKey(s) ? Math.Max(0.3, Math.Min(1.0, (double)s.KsWageHandsFloor)) : 1.0; } }
        private static bool DownKey(Settings s) { return s.KsWageHandsDown && FarmReviewDays(s) > 0; }
        // recenzja 18.10: pole Ds Farm Review Days paczki P5 (f4-kasy-miast-i-kupcy-z). Scalenie fali 4 (noc 17/18.10): P5 jest w tej samej DLL - odczyt wprost z pola
        // zamiast refleksji (zmiana nazwy albo typu pola psuje build, a nie wylacza (a) po cichu - CLAUDE.md 7); wartosc ta sama co przez refleksje (surowe pole)
        /// <summary>Ds Farm Review Days (przeglad firm DS co kwartal, paczka P5); -1 - brak ustawien.</summary>
        internal static int FarmReviewDays(Settings s)
        {
            return s != null ? s.DsFarmReviewDays : -1;
        }
        internal static double FloorNow { get { return Floor; } }

        internal static void Reset()
        {
            _w.Clear(); _dPaid = _dRecv = 0; _dExtra = 0; _dTowns = _dUp = 0; _stum = 0; _err.Clear();
            _decDay = -1;
            KsFolkWage.Reset();   // f4-place-ludu: kappa plac W (zapis arm_ks_place)
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
                return Math.Max(Floor, Math.Min(Max, w.Idx));                            // f4-place-ludu (a): ponizej 1 tylko przy Ks Wage Hands Down (inaczej podloga 1)
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
            // f4-place-ludu (recenzja 18.10): PONIZEJ 1 tylko przy dowodzie wolnych rak - dane regionu sa (fh) i f - a >= f/2, przetwory (jesli miasto je ma) zajete < 50%;
            // stary warunek slack (brak danych regionu liczy sie jak duzo rak) zostaje tylko do powrotu wskaznika do 1 - z gory (jak dotad) i z dolu (bez dowodu wolnych rak)
            bool slackDown = !shortH && fh && (f - a) >= 0.5 * f && (!craft || use < 0.5);
            double step = Step, max = Max, fl = Floor, idx = Math.Max(fl, Math.Min(max, w.Idx));   // f4-place-ludu (a): podloga Ks Wage Hands Floor albo 1 (jak dotad)
            if (shortH) idx = Math.Min(max, idx * (1.0 + step));
            else if (slackDown) idx = Math.Max(fl, idx / (1.0 + step));                   // bez klucza fl = 1 - co do liczby jak dotad
            else if (slack) idx = idx > 1.0 ? Math.Max(1.0, idx / (1.0 + step)) : Math.Min(1.0, idx * (1.0 + step));   // brak danych: do 1 (bez klucza idx >= 1 - jak dotad)
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
                float flF = (float)Floor;                                                   // f4-place-ludu (a): podloga wskaznika (bez klucza Ks Wage Hands Down - 1, jak dotad)
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
                    ix.Add(new KeyValuePair<string, float>(st.Name != null ? st.Name.ToString().Replace(';', ',').Replace('|', '/') : kv.Key, Math.Max(flF, kv.Value.Idx)));
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
                  .Append(" | potkniecia ").Append(_stum);
                // f4-place-ludu (a): wskazniki ponizej 1 (tylko przy kluczu Ks Wage Hands Down - inaczej linia jak dotad); 12 najnizszych miast - krok miedzy dobami (NKO-11);
                // recenzja 18.10: czynne tylko z przegladem firm DS co kwartal (P5) - bez niego podloga x1.00 (dopisek mowi, czemu)
                var sNow = Settings.Current;
                if (sNow != null && sNow.KsWageHandsDown)
                {
                    int dn = 0; foreach (var v in vals) if (v < 0.9995f) dn++;
                    int frd = FarmReviewDays(sNow);
                    sb.Append(" | w dol (f4-place-ludu): klucz Ks Wage Hands Down TAK, czynne ").Append(DownKey(sNow) ? "TAK" : "NIE")
                      .Append(" (przeglad firm DS co kwartal - Ds Farm Review Days ").Append(frd >= 0 ? frd.ToString(Inv) : "brak klucza (bez P5)").Append(")")
                      .Append(", podloga x").Append(flF.ToString("0.00", Inv)).Append(", tylko place W, miasta < 1: ").Append(dn);
                    if (dn > 0)
                    {
                        sb.Append(" (");
                        int k = 0;
                        for (int i = ix.Count - 1; i >= 0 && k < 12; i--) { var kv = ix[i]; if (kv.Value >= 0.9995f) break; sb.Append(k == 0 ? "" : ", ").Append(kv.Key).Append(' ').Append(kv.Value.ToString("0.000", Inv)); k++; }
                        sb.Append(')');
                    }
                }
                sb.Append('.');
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

        // ------------------------------------------------------------ f4-place-ludu (a): wskazniki ponizej 1 w osobnym kluczu arm_ks_place (KsFolkWage.ExportAll / ImportAll)
        /// <summary>"miasto:wskaznik,..." - tylko wskaznik ponizej 1 (arm_ks_koszyk trzyma te miasta z wartoscia 1 - starsza DLL wczyta 1).</summary>
        internal static string ExportDown()
        {
            var sb = new StringBuilder();
            foreach (var kv in _w)
            {
                if (string.IsNullOrEmpty(kv.Key) || kv.Key.IndexOfAny(new[] { ':', ',', '|' }) >= 0 || kv.Value == null || !(kv.Value.Idx < 0.99995f)) continue;
                if (sb.Length > 0) sb.Append(',');
                sb.Append(kv.Key).Append(':').Append(Math.Max(0.3f, kv.Value.Idx).ToString("0.####", Inv));
            }
            return sb.ToString();
        }

        /// <summary>Odczyt PO arm_ks_koszyk (Import): wskaznik ponizej 1 nadpisuje 1 z arm_ks_koszyk; zly wpis - pomijany; straznik rozmiaru (4000 miast, wskaznik 0.3..1).</summary>
        internal static void ImportDown(string s)
        {
            if (string.IsNullOrEmpty(s) || s.Length > 200000) return;
            int n = 0;
            foreach (var ent in s.Split(','))
            {
                if (n >= 4000) break;
                var f = ent.Split(':');
                float v;
                if (f.Length != 2 || f[0].Length == 0 || !float.TryParse(f[1], NumberStyles.Float, Inv, out v) || !(v >= 0.3f && v < 1f)) continue;
                W w;
                if (_w.TryGetValue(f[0], out w) && w != null) w.Idx = v;
                else _w[f[0]] = new W { Idx = v, Last = -1, State = 3 };   // bez doby decyzji - pierwsza doba po wczytaniu ustawi termin (Day)
                n++;
            }
        }
    }
}
