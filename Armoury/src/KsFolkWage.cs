using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// f4-place-ludu (noc 17/18.10; przeglad koncowy ekonomii "Wyrobnicy W w miastach to glowny akumulator", werdykt: najpierw dochod W wedlug koszyka KS, potem place w dol,
    /// na koncu nizsza danina; Jeff 17.10 noc "Poprawka 2 - tak") - PLACE WYROBNIKOW (KLASA W) MIAST Z KS:
    ///  (a) klucz Ks Wage Hands Down (KsWageHands.cs): wskaznik plac schodzi ponizej 1 (do Ks Wage Hands Floor) tym samym krokiem, ktorym rosnie przy braku rak, tam gdzie
    ///      wolnych rak duzo - druga strona tej samej reguly podazy i popytu rak. Dotyczy tylko czesci W robocizny handlu i doplaty do wyrobow (wolne rece regionu to
    ///      wyrobnicy; place rzemieslnikow R bez ciecia - R ma dzis 14.5 dnia zapasu przy normie 21). Recenzja 18.10 (werdykt przegladu "(a) tylko razem z poprawka firm"):
    ///      czynne tylko przy przegladzie firm DS co kwartal (Ds Farm Review Days > 0, paczka P5) i tylko przy dowodzie wolnych rak; ciecie (a) wchodzi liniowo w pasmie
    ///      Lo-Hi dni koszyka (jak kappa), bez skoku na progu Lo.
    ///  (b) klucz Ks Labour To Basket: stawka placy wyrobnikow wedlug ich koszyka (jak sredniowieczne taksy plac "wedlug drozyzny chleba"): miasto mierzy kiese W w dniach
    ///      koszyka KS (Need[0] kotwicy - koszyk klasy W po cenach targu, ZC4); do Ks Labour Basket Low Days - pelna stawka (wyrobnicy odkladaja), ponad - stawka schodzi
    ///      liniowo do Ks Labour Basket Min przy Ks Labour Basket High Days; zmiana najwyzej Ks Labour Basket Step Percent na dobe (bez skoku przy wlaczeniu KS, gdy
    ///      koszyk tanieje, a kiesa w dniach skacze). Dotyczy wszystkich plac W od pracodawcow z doby ludu: robocizna handlu i doplata do wyrobow (kasa miasta) oraz place
    ///      z nadwyzki R i P dla W (zamozne domy).
    ///  PODLOGA PRZEZYCIA (a i b): wyrobnicy z kiesa ponizej Ks Labour Basket Low Days koszyka (albo bez kotwicy - brak miary) dostaja pelna stawke - nikt nie glodnieje
    ///  przez te regule.
    ///  (c) w miescie z regula (b) danina pana od skarbca klasy W (Ds Hoard Due) schodzi z Ds Hoard Due Rate do Ks Labour Basket Hoard Rate RAZEM Z KAPPA miasta (kappa 1 -
    ///      stawka jak dotad, kappa najnizej - rzadki tallage; recenzja 18.10: bez skoku dochodu panow pierwszej doby) - place juz nie napelniaja kies W (TownFolk.F4.cs,
    ///      TownFolk.Hoard.cs). R, P, podzamcza i wsie - stawka jak dotad.
    /// ZAMKNIETY PIENIADZ: czesc placy, ktorej pracodawca nie placi, nigdy nie schodzi z jego kiesy - zostaje w kasie miasta (robocizna, doplata) albo w kiesie R/P (place
    /// z nadwyzki - przy liscie dostatku P3 oferta R/P liczy sie od nadwyzki, nie od reszty po placach, wiec ta czesc ZOSTAJE w kiesie R/P: jej dalsze place, zbytki, P1
    /// i danina od skarbca R/P - recenzja 18.10). Nic z niczego, nic w nicosc: tozsamosc linii pelne = zaplacone + zostalo u placacych.
    /// Dalsza droga pieniadza w kasach: ponad 1.5 x zapas kupcow - wyplata kupcom M6 (P 70% i R 30%, 1/28 dziennie) i dziesiata M9; ponizej - kasa splaca firmy DS
    /// (zaleglosci biednych miast) i skupuje towar. Recenzja 18.10 (model zamkniety tools/ekonomia-symulatory-2026-10-17/f4/simw3.py): obieg miasta (kasy -> P/R -> W)
    /// opuszcza pieniadz tylko danina od skarbca, firmy DS, P1 i M9 - bez przegladu firm (P5) nadwyzka zatrzymana u W konczy w kasach (ponad 1.5 x zapas) i u P/R,
    /// a danina od skarbca P/R przejmuje dawna danine W; z P5 przejmuje ja firma (panom i koronie) w 1-2 lata. Linia pokazuje kasy miast z KS i nadwyzke ponad
    /// 1.5 x zapas kupcow (zapas, z ktorego idzie M6); sprawdz_logi nakum NAKUM-12/14/15/16.
    /// Linia doby "Place W wedlug koszyka (f4-place-ludu): dzien N" (TownFolk.DailyLive po "Place za brak rak"). Zapis arm_ks_place: "f4|miasto:wskaznik&lt;1,...|miasto:kappa,...".
    /// Wyjatek per miasto - licznik potkniec, bez globalnego wylacznika (CLAUDE.md 7).
    /// </summary>
    internal static class KsFolkWage
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static readonly Dictionary<string, float> _k = new Dictionary<string, float>(StringComparer.Ordinal);   // kappa miasta (brak wpisu = 1)
        private static readonly HashSet<string> _err = new HashSet<string>();
        // liczniki doby (DayLine zeruje)
        private static double _dFull, _dPaid, _dCutA, _dCutB, _dSurpFull, _dSurpPaid, _dRule, _dHr;
        private static int _dTowns, _dB, _dBelow, _dIn, _dAbove, _dNoNeed, _dProt, _dDownA, _stum;
        private static long _dKW, _dKR, _dKP; private static double _dNW, _dNR, _dNP;
        private static readonly List<float> _dDays = new List<float>(), _dKap = new List<float>();
        private static long _ticks;

        internal static bool On { get { var s = Settings.Current; return s != null && s.KsLabourToBasket; } }
        /// <summary>(b) takze place z nadwyzki R i P dla W (zamozne domy) - klucz Ks Labour Basket Households.</summary>
        internal static bool HouseholdsOn { get { var s = Settings.Current; return s != null && s.KsLabourToBasket && s.KsLabourBasketHouseholds; } }
        internal static double Lo { get { var s = Settings.Current; return s != null ? Math.Max(1.0, Math.Min(240.0, s.KsLabourBasketLowDays)) : 15.0; } }
        internal static double Hi { get { var s = Settings.Current; return s != null ? Math.Max(Lo + 1.0, Math.Min(480.0, s.KsLabourBasketHighDays)) : 30.0; } }
        internal static double Min { get { var s = Settings.Current; return s != null ? Math.Max(0.0, Math.Min(1.0, s.KsLabourBasketMin)) : 0.25; } }
        private static double Step { get { var s = Settings.Current; return s != null ? Math.Max(0.001, Math.Min(0.5, s.KsLabourBasketStepPercent / 100.0)) : 0.03; } }

        internal static void Reset()
        {
            _k.Clear(); _err.Clear(); Zero(); _stum = 0;
        }

        private static void Zero()
        {
            _dFull = _dPaid = _dCutA = _dCutB = _dSurpFull = _dSurpPaid = _dRule = _dHr = 0;
            _dTowns = _dB = _dBelow = _dIn = _dAbove = _dNoNeed = _dProt = _dDownA = 0;
            _dKW = _dKR = _dKP = 0; _dNW = _dNR = _dNP = 0;
            _dDays.Clear(); _dKap.Clear(); _ticks = 0;
        }

        private static void Stumble(string where, Exception e)
        {
            _stum++;
            try { if (_err.Add(where)) Log.Error("KsFolkWage." + where, e); } catch { }
        }

        /// <summary>(b) czynna w tym miescie: klucz i KS zastosowany w miescie (mapa koszyka ZC4).</summary>
        internal static bool Active(Settlement st)
        {
            try { return On && st != null && st.IsTown && st.Town != null && StartCalibration.KsTownApplied(st.Town); }
            catch (Exception e) { Stumble("Active", e); return false; }
        }

        /// <summary>(c) stawka danin od skarbca klasy W w miescie z regula (b): od Ds Hoard Due Rate (kappa 1) do min(Ds Hoard Due Rate, Ks Labour Basket Hoard Rate) (kappa
        /// najnizej), liniowo z kappa miasta (recenzja 18.10: danina schodzi razem z placa, ktora juz nie napelnia kies - bez skoku dochodu panow); -1 - bez zmiany (miasto bez reguly).</summary>
        internal static double HoardRate(Settlement st)
        {
            var s = Settings.Current;
            if (s == null || !Active(st)) return -1.0;
            return HoardRateAt(s, KappaOf(st));
        }

        private static double HoardRateAt(Settings s, double k)
        {
            double a = s.DsHoardDueRate, b = s.KsLabourBasketHoardRate;
            if (double.IsNaN(a)) a = 0; if (double.IsNaN(b)) b = 0;
            double hiR = Math.Max(0.0, Math.Min(0.1, a)), loR = Math.Max(0.0, Math.Min(0.1, Math.Min(a, b)));
            double mn = Min, sh = mn >= 1.0 - 1e-9 ? 1.0 : (k - mn) / (1.0 - mn);
            if (double.IsNaN(sh)) sh = 1.0;
            return loR + (hiR - loR) * Math.Max(0.0, Math.Min(1.0, sh));
        }

        /// <summary>Kappa miasta (brak wpisu - 1).</summary>
        private static double KappaOf(Settlement st)
        {
            float k0;
            return st != null && st.StringId != null && _k.TryGetValue(st.StringId, out k0) ? k0 : 1.0;
        }

        /// <summary>
        /// TownFolk.TownLive krok 2 (raz na dobe na miasto, przed placami z kasy): czynnik plac W tej doby = fA x fB. `hwIdx` - wskaznik plac za brak rak (KsWageHands.Day;
        /// &lt; 1 tylko przy kluczu Ks Wage Hands Down), `kw` / `needW` - kiesa W i potrzeba W na dobe (kotwica wczoraj), kr/kp i needR/needP - tylko do linii.
        /// fA - czesc (a) (wskaznik ponizej 1), fB - kappa (b); podloga przezycia: kiesa W ponizej Lo dni koszyka - 1 i 1 (kappa od razu 1); bez miary (brak kotwicy, pierwsza
        /// doba po wczytaniu) - (a) bez ciecia, kappa z zapisu bez zmiany. Raz na dobe na miasto (kappa zmienia sie tylko tutaj). Nie rzuca.
        /// </summary>
        internal static double WFactor(Settlement st, double hwIdx, long kw, double needW, long kr, long kp, double needR, double needP, out double fA, out double fB)
        {
            fA = 1.0; fB = 1.0;
            long t0 = Stopwatch.GetTimestamp();
            try
            {
                bool downA = hwIdx < 1.0 - 1e-9;
                bool onB = Active(st);
                if (!downA && !onB) return 1.0;
                _dTowns++;
                _dKW += kw; _dKR += kr; _dKP += kp;
                if (needW > 0) _dNW += needW; if (needR > 0) _dNR += needR; if (needP > 0) _dNP += needP;
                double lo = Lo, hi = Hi;
                bool info = needW > 0 && !double.IsNaN(needW) && !double.IsInfinity(needW);
                double d = info ? Math.Max(0L, kw) / needW : double.NaN;
                if (info) _dDays.Add((float)Math.Min(d, 99999.0)); else _dNoNeed++;
                bool poor = info && d < lo;
                if (downA)
                {
                    _dDownA++;
                    if (poor || !info) _dProt++;                                // podloga przezycia / bez miary (brak kotwicy) - bez ciecia
                    else
                    {
                        // recenzja 18.10: ciecie (a) wchodzi liniowo w pasmie Lo-Hi (jak kappa: kt(Lo) = 1) - bez skoku placy W na progu Lo (wczesniej: ponizej Lo x1,
                        // od Lo od razu x wskaznik, wiec kiesa W skakala wokol Lo z dnia na dzien o 40% placy)
                        double rmp = Math.Max(0.0, Math.Min(1.0, (d - lo) / (hi - lo)));
                        fA = 1.0 - (1.0 - Math.Max(0.0, Math.Min(1.0, hwIdx))) * rmp;
                    }
                }
                if (onB)
                {
                    _dB++;
                    string id = st.StringId ?? "";
                    float k0;
                    double k = _k.TryGetValue(id, out k0) ? k0 : 1.0;
                    double mn = Min;
                    if (!info) { }                                               // bez miary (pierwsza doba po wczytaniu - kotwica jeszcze nie liczona, miasto bez kotwicy):
                                                                                 // kappa z zapisu bez zmiany (miasto, ktore nigdy nie mialo miary - 1)
                    else if (poor) k = 1.0;                                      // podloga przezycia: pelna stawka od razu
                    else
                    {
                        double kt = d >= hi ? mn : 1.0 - (d - lo) / (hi - lo) * (1.0 - mn);
                        double stp = Step;
                        k += Math.Max(-stp, Math.Min(stp, kt - k));
                    }
                    k = Math.Max(mn, Math.Min(1.0, k));
                    if (id.Length > 0) { if (k >= 0.99995) _k.Remove(id); else _k[id] = (float)k; }
                    fB = k;
                    _dKap.Add((float)k);
                    var s0 = Settings.Current; if (s0 != null) _dHr += HoardRateAt(s0, k);   // (c) stawka danin W tej doby (linia: srednia miast z regula)
                    if (!info) { } else if (d < lo) _dBelow++; else if (d < hi) _dIn++; else _dAbove++;
                }
                return fA * fB;
            }
            catch (Exception e) { Stumble("WFactor", e); fA = fB = 1.0; return 1.0; }
            finally { _ticks += Stopwatch.GetTimestamp() - t0; }
        }

        /// <summary>TownLive krok 2: place W z kasy tej doby (po przycieciu do stanu kasy, zaokraglone w dol jak przelew) - `fullW` przy czynniku 1, `ruleW` zlecone kasie
        /// wedlug reguly (a i b), `paidW` - czesc W, ktora NAPRAWDE zeszla z kasy (FromCofferWR; recenzja 18.10 - wczesniej zapisywane bylo zlecenie, wiec kontrola linii
        /// nie mogla sie nie zgodzic). Zostalo w kasie = pelne - zlecone; fA/fB - podzial tego, co zostalo.</summary>
        internal static void NoteCoffer(long fullW, long ruleW, long paidW, double fA, double fB)
        {
            if (fullW <= 0) return;
            _dFull += fullW; _dRule += ruleW; _dPaid += paidW;
            double cut = Math.Max(0, fullW - ruleW), a = fullW * (1.0 - fA);
            if (a > cut) a = cut;
            if (a < 0) a = 0;
            _dCutA += a; _dCutB += cut - a;
        }

        /// <summary>TownLive krok 5: place z nadwyzki R/P dla W - pelne i zaplacone (reszta zostaje w kiesie R/P).</summary>
        internal static void NoteSurplus(long full, long paid)
        {
            if (full <= 0) return;
            _dSurpFull += full; _dSurpPaid += Math.Max(0, paid);
        }

        private static float Med(List<float> v) { if (v.Count == 0) return float.NaN; v.Sort(); return v[v.Count / 2]; }
        private static string F(double v, string fmt) { return double.IsNaN(v) ? "-" : v.ToString(fmt, Inv); }

        /// <summary>TownFolk.DailyLive (po miastach, po linii "Place za brak rak"): linia doby. Bez kluczy i bez miast z regula - nic.</summary>
        internal static void DayLine(int day)
        {
            try
            {
                var s = Settings.Current;
                if (s == null) return;
                if (_dTowns == 0) return;                                    // klucze wylaczone albo KS niezastosowany (zadne miasto z regula tej doby) - bez linii (log jak dotad)
                long t0 = Stopwatch.GetTimestamp();
                // kasy miast z KS (dalsza droga pieniadza, ktory zostal u pracodawcow): razem i ponad 1.5 x zapas kupcow (zrodlo wyplaty kupcom M6) - liczba miast i NADWYZKA
                // (zapas zl ponad 1.5 x zapas; recenzja 18.10: NAKUM-16 pilnuje, zeby M6 ja oproznial, a nie zeby rosla)
                long gold = 0, over = 0; int nT = 0, nOver = 0;
                foreach (var t in TaleWorlds.CampaignSystem.Settlements.Town.AllTowns)
                {
                    try
                    {
                        if (t == null || t.Settlement == null || !StartCalibration.KsTownApplied(t)) continue;
                        nT++; gold += Math.Max(0, t.Gold);
                        double lim = DsTowns.OverReserve * TownPurse.Reserve(t);
                        if (t.Gold > lim) { nOver++; over += (long)Math.Floor(t.Gold - lim); }
                    }
                    catch (Exception e) { Stumble("DayLine.kasa", e); }
                }
                float mdD = Med(_dDays), mdK = Med(_dKap);
                float mnK = _dKap.Count > 0 ? _dKap[0] : float.NaN, mxK = _dKap.Count > 0 ? _dKap[_dKap.Count - 1] : float.NaN;
                int kLow = 0; foreach (var v in _dKap) if (v < 0.99995f) kLow++;
                long full = (long)Math.Round(_dFull), paid = (long)Math.Round(_dPaid), cut = (long)Math.Round(_dCutA + _dCutB), cutA = (long)Math.Round(_dCutA);
                long cutB = cut - cutA;                                          // kontrola full - paid - cut = zlecone - zeszlo z kas (0, gdy kasy zaplacily cale zlecenie)
                long sf = (long)Math.Round(_dSurpFull), sp = (long)Math.Round(_dSurpPaid);
                double hr = Math.Max(0.0, Math.Min(0.1, Math.Min(s.DsHoardDueRate, s.KsLabourBasketHoardRate)));
                double hrNow = _dB > 0 ? _dHr / _dB : double.NaN;            // (c) srednia stawka danin W miast z regula tej doby (schodzi razem z kappa)
                int frd = KsWageHands.FarmReviewDays(s);
                var sb = new StringBuilder(1200);
                sb.Append("Place W wedlug koszyka (f4-place-ludu): dzien ").Append(day)
                  .Append(" | klucze: Ks Labour To Basket ").Append(On ? "TAK" : "NIE").Append(", Ks Wage Hands Down ").Append(KsWageHands.DownOn ? "TAK" : "NIE")
                  .Append(" (podloga x").Append(KsWageHands.FloorNow.ToString("0.00", Inv)).Append("; klucz ").Append(s.KsWageHandsDown ? "TAK" : "NIE")
                  .Append(", przeglad firm DS co kwartal - Ds Farm Review Days ").Append(frd >= 0 ? frd.ToString(Inv) : "brak klucza (bez P5)").Append(")")
                  .Append(" | pasmo ").Append(Lo.ToString("0", Inv)).Append('-').Append(Hi.ToString("0", Inv)).Append(" dni koszyka KS, najnizej x").Append(Min.ToString("0.00", Inv))
                  .Append(", krok ").Append((100 * Step).ToString("0.0", Inv)).Append("%/d, danina od skarbca W w tych miastach ").Append((100 * hr).ToString("0.00", Inv))
                  .Append("%/d (dzis srednio ").Append(F(100 * hrNow, "0.00")).Append("%, schodzi razem z kappa; Ds Hoard Due Rate ")
                  .Append((100 * Math.Max(0.0, Math.Min(0.1, s.DsHoardDueRate))).ToString("0.00", Inv)).Append("%)")
                  .Append(" | miasta z regula ").Append(_dTowns).Append(" (b: ").Append(_dB).Append("): ponizej pasma (pelna placa) ").Append(_dBelow).Append(", w pasmie ").Append(_dIn)
                  .Append(", ponad pasmem ").Append(_dAbove).Append(", bez potrzeby W (pelna placa) ").Append(_dNoNeed)
                  .Append(" | (a) wskaznik < 1: miast ").Append(_dDownA).Append(", podloga przezycia (bez ciecia) ").Append(_dProt)
                  .Append(" | kappa min ").Append(F(mnK, "0.000")).Append(" / mediana ").Append(F(mdK, "0.000")).Append(" / max ").Append(F(mxK, "0.000")).Append(", miast z kappa < 1: ").Append(kLow)
                  .Append(" | zapas dni W (koszyk KS) mediana ").Append(F(mdD, "0.0")).Append(", kiesy W/R/P miast z regula ").Append(_dKW).Append('/').Append(_dKR).Append('/').Append(_dKP)
                  .Append(" (dni W/R/P ").Append(F(_dNW > 0 ? _dKW / _dNW : double.NaN, "0.0")).Append('/').Append(F(_dNR > 0 ? _dKR / _dNR : double.NaN, "0.0")).Append('/')
                  .Append(F(_dNP > 0 ? _dKP / _dNP : double.NaN, "0.0")).Append(')')
                  .Append(" | doba: place W z kas pelne ").Append(full).Append(" = zaplacone ").Append(paid).Append(" + zostalo w kasach miast ").Append(cut)
                  .Append(" (kontrola pelne - zaplacone - zostalo = ").Append(full - paid - cut).Append("), z tego (a) wskaznik ponizej 1: ").Append(cutA).Append(", (b) stawka wedlug koszyka: ").Append(cutB)
                  .Append("; place z nadwyzki R/P dla W (Ks Labour Basket Households ").Append(HouseholdsOn ? "TAK" : "NIE").Append(") pelne ").Append(sf).Append(" = zaplacone ").Append(sp).Append(" + zostalo w kiesach R/P ").Append(sf - sp)
                  .Append(" | kasy miast z KS razem ").Append(gold).Append(" (miast ").Append(nT).Append(", ponad 1.5 x zapas kupcow ").Append(nOver)
                  .Append(", nadwyzka ponad 1.5 x zapas ").Append(over).Append(')')
                  .Append(" | czas ").Append(((_ticks + (Stopwatch.GetTimestamp() - t0)) * 1000.0 / Stopwatch.Frequency).ToString("0.00", Inv)).Append(" ms | potkniecia ").Append(_stum).Append('.');
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Stumble("DayLine", e); }
            finally { Zero(); _stum = 0; }
        }

        // ------------------------------------------------------------ zapis (klucz arm_ks_place)
        private static string ExportK()
        {
            var sb = new StringBuilder();
            foreach (var kv in _k)
            {
                if (string.IsNullOrEmpty(kv.Key) || kv.Key.IndexOfAny(new[] { ':', ',', '|' }) >= 0) continue;
                if (sb.Length > 0) sb.Append(',');
                sb.Append(kv.Key).Append(':').Append(Math.Max(0f, Math.Min(1f, kv.Value)).ToString("0.####", Inv));
            }
            return sb.ToString();
        }

        private static void ImportK(string s)
        {
            _k.Clear();
            if (string.IsNullOrEmpty(s) || s.Length > 200000) return;
            foreach (var ent in s.Split(','))
            {
                if (_k.Count >= 4000) break;
                var f = ent.Split(':');
                float v;
                if (f.Length != 2 || f[0].Length == 0 || !float.TryParse(f[1], NumberStyles.Float, Inv, out v) || !(v >= 0f && v < 1f)) continue;
                _k[f[0]] = v;
            }
        }

        /// <summary>Klucz zapisu arm_ks_place: "f4|wskazniki ponizej 1 (KsWageHands)|kappa (b)"; null - nic do zapisania (klucze wylaczone, wszystko 1: klucza nie piszemy, zapis jak
        /// dotad). Starsza DLL klucza nie czyta (wskazniki z arm_ks_koszyk >= 1, kappa 1).</summary>
        internal static string ExportAll()
        {
            string a = "", b = "";
            try { a = KsWageHands.ExportDown(); } catch (Exception e) { Stumble("ExportDown", e); }
            try { b = ExportK(); } catch (Exception e) { Stumble("ExportK", e); }
            if (a.Length == 0 && b.Length == 0) return null;
            return "f4|" + a + "|" + b;
        }

        /// <summary>Odczyt klucza arm_ks_place PO arm_ks_koszyk; brak klucza (stary zapis, Ironman Jeffa) albo inny format - wskazniki jak z arm_ks_koszyk (>= 1), kappa 1.</summary>
        internal static void ImportAll(string s)
        {
            if (string.IsNullOrEmpty(s) || !s.StartsWith("f4|", StringComparison.Ordinal) || s.Length > 400000) { _k.Clear(); return; }
            var p = s.Split('|');
            try { KsWageHands.ImportDown(p.Length > 1 ? p[1] : null); } catch (Exception e) { Stumble("ImportDown", e); }
            try { ImportK(p.Length > 2 ? p[2] : null); } catch (Exception e) { _k.Clear(); Stumble("ImportK", e); }
        }
    }
}
