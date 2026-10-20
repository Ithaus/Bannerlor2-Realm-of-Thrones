using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// p6-a7 (fala 2 pomyslow, Jeff 20.10: "A7 TAK razem z odplywem pieniedzy z kas miast") - ROBOTY MIAST Z RAK OKREGU (odplyw kas miast do kies wsi).
    /// PRZYCZYNA (kod + test fin103 19.10, WGRANIE 88 + A7, 160 dob x8: NAKUM-7 kasy miast +69 tys./d, NAKUM-12 +65 tys./d wobec fin102 bez A7 +32 tys./d):
    ///  - regula A7 (StartCalibration.Rule.cs Progress -> A7sPay -> VillageCrafts.PayRebuild -> TownFolk.CraftsRebuildPay) placi przebudowe zajecia (28 dob kosztu
    ///    za przenoszone rece) Z KIESY WSI: material z polki kasy miasta targowego (zaplata do kasy) i placa rzemieslnikow (lud miasta; przy kiesach ludu ponad pasmem f5
    ///    czesc wraca do kasy - PayVillageWage, _a7sCof). To nowy, staly przeplyw kiesy wsi -> kasa miasta, ktorego bez A7 nie ma. Zmiany zaczynaja sie po 60 dobach
    ///    nacisku od D (log 160 dob WGRANIA 91: D 109126, trzymane >= 60 dob 7.7% blokow), wiec w biegu 160 dob przyplyw siedzi akurat w oknie NAKUM (ostatnie 28 dob);
    ///  - przy czynnej A7 zajecia wiosek pobocznych tez ida za sygnalem KS (SideVillageCraftKsSignal - ta sama przebudowa z kiesy wsi do kasy);
    ///  - istniejace ujscia nadwyzki kas (M6 do kupcow P/R, przeglad firmy DS wedlug nadwyzki co 14 dob najwyzej +25% i gersuma f7 - do PANOW i KORONY) dzialaja
    ///    z opoznieniem i zadne nie oddaje pieniadza wsiom, z ktorych A7 go zabiera (kiesy wsi bez A7 i tak -0.24%/d - NAKUM-5).
    /// DLACZEGO TAK (model p6-a7-tmp/model4-8.py na kasy-miast.csv biegu 160 dob WGRANIA 91 - stan bez A7, doplyw A7 dodany z rampa 28 dob):
    ///  - pulap kazdego miasta osobno scina zwykle wahania kas (rata firmy co 7 dob, gersuma, handel) - placilby takze bez A7; suma 97 kas jest gladka;
    ///  - pulap z dnia ustalenia firm DS (suma 11 mln) albo sam prog potrzeb (ok. 23 mln) wydaje tez naturalny przyrost kas po przejsciu F3 i KS (11 -> 28 mln bez A7) -
    ///    odplyw 25-50 tys./d bez A7 (gersuma i podwyzki firm panow mniejsze). Pulap z PIERWSZEJ DOBY PRZEBUDOW A7 jest neutralny: bez A7 regula nigdy nie rusza;
    ///  - ale A7 rusza w biegu 160 dob ok. D+60, gdy kasy jeszcze rosna same (25 -> 28.6 mln do D+90): sam pulap wydawalby tez ten przyrost - 58-106 tys./d do kies
    ///    wsi, ktore maja razem tylko ok. 5.5-6.5 mln (NAKUM-5: dryf kies wsi <= +0.2%/d, ok. 12 tys./d - wsie bylyby nowym akumulatorem). Dlatego ZWROT: miasto oddaje
    ///    okregowi najwyzej tyle, ile wsie jego okregu wydaly u niego na przebudowy zajec (srednia 7 dob, VillageCrafts.PayRebuild - A7, takze ZW) - kiesy wsi razem
    ///    bez zmiany wobec biegu bez A7, nic nowego nie gromadzi sie we wsiach. Model (model8.py, zwrot = doplyw A7 do kas): doplyw +35 tys./d -> kasy w oknie ok. 0/d,
    ///    +70 tys./d -> ok. +3 tys./d (prog NAKUM-12 43 tys./d, NAKUM-26 30 tys./d); srednia 28 dob za wolna (+12..+28 tys./d), srednia 7 dob (tydzien rat firmy).
    /// REGULA (sredniowiecze: rada miasta z nadwyzki oplaca mury, bruk, mosty i studnie - murage, pavage, pontage; furmanki z kamieniem i drewnem i robotnicy z wsi
    /// okregu; zloto do kies tych wsi):
    ///  - od dnia, w ktorym firmy DS sa ustalone (DsTowns.FarmsLive - wczesniej kasy oddaja nadwyzke panom zaworem), co dobe: S = srednia wykladnicza 7 dob sumy kas
    ///    wszystkich miast (pomiar, takze przed startem);
    ///  - START: pierwsza doba z przebudowami zajec A7 w toku (StartCalibration.A7Rebuilding) - G0 = wieksze z S i sumy kas tej doby (zapisane; start neutralny). Bez A7 regula tylko mierzy;
    ///  - PULAP = wieksze z: G0 i suma progow wydatkow miast (DsTowns.HireKeep: 1.5 x zapas kupcow, z firma DS ten sam prog co gersuma + reszta gersumy + zaleglosci
    ///    firm; do tego otwarte rachunki zywnosci miasta - KsFoodCredit);
    ///  - S ponad pulapem: miasta wydaja dzis razem najwyzej (S - pulap) / 28 (ok. miesiac - jak M6) i najwyzej tyle, o ile suma kas jest ponad suma progow; kazde miasto
    ///    najwyzej ZWROT (srednia 7 dob wydatkow wsi jego okregu na przebudowy w nim) i najwyzej swoja kase ponad WLASNYM progiem (zadne nie schodzi ponizej swojego
    ///    progu); gdy pulap tnie, miasta placa w stosunku swoich kwot;
    ///  - zloto z kasy miasta do kies wsi, ktorych miastem targowym jest (TradeBound, inaczej Bound - ta sama wies, ktora placi mu przebudowe A7), wedlug ognisk; wies
    ///    spladrowana / najezdzana nie bierze. Bez wyplaty: oblezenie / swiezo zdobyte (DsTowns.RemitOf), "domy najpierw" (KsFolkIncome.HousesFirst - R/P zbiedniali,
    ///    nadwyzka najpierw do ich domow przez M6, jak gersuma), miasto bez wsi do najecia - ich udzial przechodzi na inne miasta tej doby;
    ///  - wszystkie miasta (AI i gracza) i wszystkie ich wsie; zamki - nie (kasy zamkow maja zawor do panow, 110).
    /// PIENIADZ: przelew kasa miasta -> kiesy wsi (oba w "Pieniadz swiata" - suma swiata bez zmiany, bez nowego zrodla ani ujscia); w ksiedze "Przeplywy osad" osobna
    /// pozycja naszego ticku (MoneyLedger.MTownHire, Mark zaraz po tej regule); kontrola linii: zdjete z kas (zmierzone na kasie) = do kies wsi (zmierzone u wsi).
    /// Wyplata to wydatek kasy (TownPurse.NoteTake, jak danina i clo z kasy): doplyw netto kupcow, z ktorego DS ustala i odnawia firmy, bez zmiany; przeglad wedlug
    /// nadwyzki (f5) i gersuma (f7) widza prawdziwa kase - dziela sie przyrostem ponad tym samym progiem.
    /// ZAPIS arm_townhire ("th1|doba startu|G0|S|doba S|id miasta:zwrot,..."), SaveText.Sync; brak klucza (stary zapis, kampania Jeffa) = srednia od pierwszej doby
    /// z firmami DS po wczytaniu, zwrot od zera, start przy pierwszej dobie z przebudowami A7. Reset przy nowej kampanii / przed wczytaniem (ArmouryBehavior, osobna linia _p6a7Reset). Wyjatki per
    /// miasto liczone (potkniecia), bez globalnego wylacznika. Watek glowny (tick dobowy); licznik zwrotu (NoteRebuild) pod blokada.
    /// </summary>
    internal static class TownHire
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private const string Ver = "th1";
        private static readonly char[] Sep = { ':', ',', '|' };
        private const double SpendDays = 28.0;          // nadwyzke sumy kas miasta wydaja w ok. miesiac (1/28 dziennie, jak M6)
        private const double AvgDays = 7.0;             // srednia wykladnicza 7 dob sumy kas (tydzien rat firmy DS - PayDay co 7 dob - nie kolysze wyplaty)
        private const int GapDays = 7;                  // przerwa w pomiarze dluzsza niz tydzien (klucz wylaczony, firmy DS zgasly) - srednia od nowa

        private static int _start = -1, _lastDay = -1, _sDay = -1;
        private static long _g0;                        // srednia 7 dob sumy kas miast w dniu startu reguly (pierwsza doba przebudow A7)
        private static double _s = double.NaN;          // srednia wykladnicza 7 dob sumy kas miast
        private static readonly Dictionary<Settlement, List<Village>> _vil = new Dictionary<Settlement, List<Village>>();
        private static int _vilDay = -1;
        // praca doby (bez alokacji w petli - kolekcje wielokrotnego uzytku)
        private static readonly List<Town> _payT = new List<Town>(128);
        private static readonly List<double> _payW = new List<double>(128);
        private static readonly Dictionary<Settlement, long> _bills = new Dictionary<Settlement, long>();
        private static readonly Dictionary<Settlement, int> _billsOld = new Dictionary<Settlement, int>();
        private static readonly List<double> _payE = new List<double>(128);
        // zwrot: wydatki wsi na przebudowy zajec w miescie targowym (VillageCrafts.PayRebuild) - dzis i srednia wykladnicza 7 dob (zapis)
        private static readonly object _rebLock = new object();
        private static readonly Dictionary<Town, long> _rebToday = new Dictionary<Town, long>();
        private static readonly Dictionary<Town, double> _rebAvg = new Dictionary<Town, double>();
        private static Dictionary<string, double> _rebLoad;              // z zapisu (id osady) - do pierwszej doby pomiaru
        private static long _dReb, _dRet; private static double _dRebAvg;
        private static long _rebL;                                       // wydatki wsi na przebudowy w miastach od ostatniej linii

        // stan ostatniej doby (linia)
        private static long _dG, _dKeep, _dCap, _dPlan; private static double _dS; private static int _dTowns, _dOver; private static bool _dA7;
        // liczniki od ostatniej linii
        private static long _took, _gave, _sessTook;
        private static int _days, _payDays, _paidTD, _paidV, _siege, _houses, _noVil, _stum, _stumSess;
        private static double _msSum, _msMax;
        private static readonly HashSet<string> _err = new HashSet<string>(StringComparer.Ordinal);

        internal static bool Key { get { var s = Settings.Current; return s != null && s.TownWorksHireDistrict; } }

        internal static void Reset()
        {
            _start = -1; _lastDay = -1; _sDay = -1; _g0 = 0; _s = double.NaN; _vil.Clear(); _vilDay = -1; _payT.Clear(); _payW.Clear(); _payE.Clear(); _bills.Clear(); _billsOld.Clear();
            lock (_rebLock) _rebToday.Clear();
            _rebAvg.Clear(); _rebLoad = null;
            _dG = _dKeep = _dCap = _dPlan = 0; _dS = 0; _dTowns = _dOver = 0; _dA7 = false; _dReb = _dRet = 0; _dRebAvg = 0;
            _sessTook = 0; _stumSess = 0; _err.Clear();
            Zero();
        }

        private static void Zero()
        {
            _took = _gave = 0; _days = _payDays = _paidTD = _paidV = _siege = _houses = _noVil = _stum = 0; _msSum = _msMax = 0; _rebL = 0;
        }

        /// <summary>VillageCrafts.PayRebuild (przebudowa zajecia - regula A7 i ZW): `x` zlota zeszlo z kiesy wsi do miasta targowego `mt` (material z polki, rzemieslnicy).
        /// Tylko licznik dnia (zwrot TownHire liczy z niego srednia). Blokada - na wypadek wolania spoza watku glownego (droga dawna); nie rzuca.</summary>
        internal static void NoteRebuild(Town mt, long x)
        {
            try
            {
                if (mt == null || x <= 0) return;
                lock (_rebLock) { long v; _rebToday.TryGetValue(mt, out v); _rebToday[mt] = v + x; }
            }
            catch { }
        }

        private static void Stumble(string where, Exception e)
        {
            _stum++; _stumSess++;
            try { if (_err.Add(where)) Log.Error("TownHire." + where, e); } catch { }
        }

        // ------------------------------------------------------------ zapis
        internal static string Export()
        {
            try
            {
                if (double.IsNaN(_s) || _sDay < 0) return null;            // pomiar jeszcze nie ruszyl (firmy DS nie ustalone) - bez klucza
                var sb = new StringBuilder(64 + 24 * _rebAvg.Count);
                sb.Append(Ver).Append('|').Append(_start.ToString(Inv)).Append('|').Append(_g0.ToString(Inv)).Append('|').Append(_s.ToString("0", Inv)).Append('|').Append(_sDay.ToString(Inv)).Append('|');
                bool first = true;
                foreach (var kv in _rebAvg)
                {
                    var st = kv.Key != null ? kv.Key.Settlement : null;
                    if (st == null || string.IsNullOrEmpty(st.StringId) || st.StringId.IndexOfAny(Sep) >= 0 || !(kv.Value >= 1)) continue;
                    if (!first) sb.Append(',');
                    first = false;
                    sb.Append(st.StringId).Append(':').Append(kv.Value.ToString("0", Inv));
                }
                return sb.ToString();
            }
            catch (Exception e) { Stumble("Export", e); return null; }
        }

        internal static void Import(string t)
        {
            try
            {
                _start = -1; _g0 = 0; _s = double.NaN; _sDay = -1; _rebLoad = null; _rebAvg.Clear();
                if (string.IsNullOrEmpty(t)) return;
                var p = t.Split('|');
                if (p.Length < 5 || p[0] != Ver) return;
                int st, sd; long g0; double s;
                if (!int.TryParse(p[1], NumberStyles.Integer, Inv, out st) || !long.TryParse(p[2], NumberStyles.Integer, Inv, out g0)
                    || !double.TryParse(p[3], NumberStyles.Float, Inv, out s) || !int.TryParse(p[4], NumberStyles.Integer, Inv, out sd)
                    || g0 < 0 || !(s >= 0) || sd < 0 || st < -1) return;
                _start = st; _g0 = st >= 0 ? g0 : 0; _s = s; _sDay = sd;
                if (p.Length >= 6 && p[5].Length > 0)
                {
                    var m = new Dictionary<string, double>(StringComparer.Ordinal);
                    foreach (var it in p[5].Split(','))
                    {
                        int c = it.IndexOf(':');
                        double r;
                        if (c > 0 && double.TryParse(it.Substring(c + 1), NumberStyles.Float, Inv, out r) && r >= 0 && !double.IsInfinity(r)) m[it.Substring(0, c)] = r;
                    }
                    _rebLoad = m;
                }
            }
            catch (Exception e) { Stumble("Import", e); _start = -1; _g0 = 0; _s = double.NaN; _sDay = -1; _rebLoad = null; }
        }

        // ------------------------------------------------------------ wsie okregu (raz na dobe)
        private static void Villages(int today)
        {
            if (_vilDay == today) return;
            _vilDay = today;
            foreach (var l in _vil.Values) l.Clear();
            foreach (var v in Village.All)
            {
                try
                {
                    if (v == null || v.Settlement == null) continue;
                    var mk = v.TradeBound ?? v.Bound;                  // jak miasto targowe przebudowy A7 (StartCalibration.Rule: TradeBound, inaczej Bound)
                    if (mk == null || !mk.IsTown) continue;
                    List<Village> l;
                    if (!_vil.TryGetValue(mk, out l)) { l = new List<Village>(8); _vil[mk] = l; }
                    l.Add(v);
                }
                catch (Exception e) { Stumble("Villages", e); }
            }
        }

        private static bool HasVillages(Settlement st)
        {
            List<Village> l;
            if (!_vil.TryGetValue(st, out l)) return false;
            foreach (var v in l) if (v != null && v.VillageState == Village.VillageStates.Normal) return true;
            return false;
        }

        /// <summary>Prog wydatkow miasta: DsTowns.HireKeep + otwarte rachunki zywnosci miasta (jeden przebieg na dobe - _bills, te same filtry co gersuma). NaN - brak danych.</summary>
        private static double KeepOf(Settlement st, Town t)
        {
            double keep = DsTowns.HireKeep(st, t);
            if (double.IsNaN(keep)) return double.NaN;
            long bills;
            _bills.TryGetValue(st, out bills);
            return keep + Math.Max(0, bills);
        }

        // ------------------------------------------------------------ doba (ArmouryBehavior: po KS i VillageCrafts - przebudowy A7 tej doby juz w kasach; potem Mark MTownHire)
        internal static void Daily()
        {
            int today = (int)CampaignTime.Now.ToDays;
            if (_lastDay == today) return;
            _lastDay = today;
            if (!Key || !DsTowns.FarmsLive) { lock (_rebLock) _rebToday.Clear(); return; }   // kasy oddaja nadwyzke panom zaworem (firmy DS nie ustalone) - pomiar i start pozniej
            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            try
            {
                Villages(today);
                _days++;
                _payT.Clear(); _payW.Clear(); _payE.Clear();
                try { KsFoodCredit.OlderThanByDst(-1, _bills, _billsOld); } catch (Exception e) { _bills.Clear(); Stumble("Bills", e); }
                bool gap = double.IsNaN(_s) || _sDay < 0 || today - _sDay > GapDays;
                long gSum = 0, reb = 0; double keepSum = 0, want = 0, rebAvg = 0; int n = 0, nOver = 0, nSiege = 0, nHouses = 0, nNoVil = 0;
                lock (_rebLock)
                {
                    foreach (var t in Town.AllTowns)
                    {
                        try
                        {
                            var st = t != null ? t.Settlement : null;
                            if (st == null) continue;
                            // zwrot: srednia 7 dob wydatkow wsi okregu na przebudowy w tym miescie (po wczytaniu - z zapisu; po przerwie - od dzis)
                            long o; _rebToday.TryGetValue(t, out o);
                            double r;
                            if (!_rebAvg.TryGetValue(t, out r))
                            {
                                r = 0;
                                double lr;
                                if (_rebLoad != null && st.StringId != null && _rebLoad.TryGetValue(st.StringId, out lr)) r = lr;
                            }
                            r = gap ? o : r + (o - r) / AvgDays;
                            if (r >= 0.5) _rebAvg[t] = r; else _rebAvg.Remove(t);
                            reb += o; rebAvg += r;
                            double keep = KeepOf(st, t);
                            if (double.IsNaN(keep)) continue;
                            long g = t.Gold;
                            n++; gSum += g; keepSum += keep;
                            double ex = g - keep;
                            if (!(ex >= 1)) continue;
                            nOver++;
                            if (!(r >= 1)) continue;                                 // okreg nic u niego nie przebudowuje - nie ma czego zwracac
                            if (DsTowns.RemitOf(st) != 0) { nSiege++; continue; }
                            if (KsFolkIncome.HousesFirst(st)) { nHouses++; continue; }
                            if (!HasVillages(st)) { nNoVil++; continue; }
                            double w = Math.Min(r, ex);
                            _payT.Add(t); _payW.Add(w); _payE.Add(ex); want += w;
                        }
                        catch (Exception e) { Stumble("Town", e); }
                    }
                    _rebToday.Clear();
                }
                _rebLoad = null;
                _rebL += reb;
                if (n == 0) return;
                if (gap) _s = gSum;                                          // pierwsza doba pomiaru albo po przerwie - srednia od sumy dzis
                else _s += (gSum - _s) / AvgDays;
                _sDay = today;
                bool a7 = false;
                try { a7 = StartCalibration.A7Rebuilding; } catch (Exception e) { Stumble("A7", e); }
                if (_start < 0 && a7) { _start = today; _g0 = (long)Math.Round(Math.Max(_s, gSum)); }   // pierwsza doba przebudow A7: pulap = to, co kasy maja (srednia 7 dob albo dzis - wiecej)
                double cap = Math.Max(_start >= 0 ? _g0 : 0, keepSum);
                double plan = _start >= 0 ? (_s - cap) / SpendDays : 0;
                plan = Math.Min(plan, gSum - keepSum);                       // suma kas nie schodzi ponizej sumy progow
                plan = Math.Min(plan, want);                                 // kazde miasto najwyzej zwrot i kasa ponad wlasnym progiem
                _dG = gSum; _dKeep = (long)keepSum; _dCap = (long)cap; _dS = _s; _dTowns = n; _dOver = nOver; _dA7 = a7; _dPlan = plan >= 1 ? (long)Math.Floor(plan) : 0;
                _dReb = reb; _dRebAvg = rebAvg; _dRet = (long)Math.Floor(want);
                if (_start < 0) return;                                      // przed A7 - sam pomiar (bez licznikow "bez wyplaty")
                _siege += nSiege; _houses += nHouses; _noVil += nNoVil;
                if (_dPlan < 1 || !(want > 0)) return;
                _payDays++;
                double k = Math.Min(1.0, _dPlan / want);                     // pulap tnie - miasta w stosunku swoich kwot
                for (int i = 0; i < _payT.Count; i++)
                {
                    try
                    {
                        long x = (long)Math.Floor(Math.Min(_payE[i], _payW[i] * k));
                        if (x >= 1) Pay(_payT[i].Settlement, _payT[i], x);
                    }
                    catch (Exception e) { Stumble("Pay", e); }
                }
            }
            catch (Exception e) { Stumble("Daily", e); }
            finally
            {
                _payT.Clear(); _payW.Clear(); _payE.Clear();
                double ms = (System.Diagnostics.Stopwatch.GetTimestamp() - t0) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
                _msSum += ms; if (ms > _msMax) _msMax = ms;
            }
        }

        private static void Pay(Settlement st, Town t, long want)
        {
            List<Village> l;
            if (st == null || !_vil.TryGetValue(st, out l) || l.Count == 0) { _noVil++; return; }
            double w = 0; Village top = null; double topW = -1;
            foreach (var v in l)
            {
                if (v == null || v.VillageState != Village.VillageStates.Normal) continue;
                double x = Math.Max(1.0, v.Hearth);
                w += x;
                if (x > topW) { topW = x; top = v; }
            }
            if (!(w > 0) || top == null) { _noVil++; return; }
            int ask = (int)Math.Min(int.MaxValue / 2, Math.Min(want, Math.Max(0, t.Gold)));
            if (ask < 1) return;
            int g0 = t.Gold;
            t.ChangeGold(-ask);
            long took = (long)g0 - t.Gold;                                // zmierzone na kasie
            if (took <= 0) return;
            long left = took, gave = 0; int nv = 0;
            foreach (var v in l)
            {
                if (v == null || v.VillageState != Village.VillageStates.Normal || v == top) continue;
                long x = (long)Math.Floor(took * Math.Max(1.0, v.Hearth) / w);
                if (x <= 0) continue;
                int v0 = v.Gold;
                v.ChangeGold((int)x);
                gave += (long)v.Gold - v0; left -= x; nv++;
            }
            if (left > 0)
            {
                int v0 = top.Gold;
                top.ChangeGold((int)left);
                gave += (long)top.Gold - v0; nv++;
            }
            // wydatek kasy, nie spadek zarobku kupcow: doplyw netto kupcow (TownPurse.NetOf -> ustalenie i odnowa firm DS) bez zmiany, jak danina i clo z kasy
            try { TownPurse.NoteTake(st, (int)Math.Min(int.MaxValue, took)); } catch (Exception e) { Stumble("NoteTake", e); }
            _took += took; _gave += gave; _sessTook += took; _paidTD++; _paidV += nv;
        }

        // ------------------------------------------------------------ linia (po Daily, w dobie linii; liczniki od ostatniej linii)
        internal static void Line()
        {
            if (!Key || !LogGate.Lines) return;
            try
            {
                int day = (int)CampaignTime.Now.ToDays;
                var sb = new StringBuilder(1000);
                sb.Append("Roboty miast z rak okregu (p6-a7): dzien ").Append(day).Append(" | klucz Town Works Hire District TAK, ");
                if (!DsTowns.FarmsLive && _start < 0) sb.Append("czeka na firmy DS (FarmsLive NIE)");
                else if (_start < 0) sb.Append("czeka na przebudowy zajec A7 (regula A7 ").Append(SafeA7RuleOn() ? "TAK" : "NIE").Append(", sam pomiar)");
                else sb.Append("od doby ").Append(_start).Append(" (pierwsza doba przebudow A7; pulap = max(kasy razem w dniu startu, suma progow wydatkow); dziennie 1/28 nadwyzki sredniej 7 dob, miasto najwyzej zwrot przebudow okregu)");
                sb.Append(" | doba: miast ").Append(_dTowns).Append(", kasy razem ").Append(_dG).Append(", srednia 7 dob ").Append(_dS.ToString("0", Inv))
                  .Append(", pulap startu ").Append(_g0).Append(", progi razem ").Append(_dKeep).Append(", pulap ").Append(_dCap)
                  .Append(", nadwyzka sredniej ").Append(((long)Math.Round(_dS - _dCap)).ToString(Inv)).Append(", wyplata dzis ").Append(_start >= 0 ? _dPlan : 0)
                  .Append(", miast ponad wlasnym progiem ").Append(_dOver).Append(", przebudowy A7 ").Append(_dA7 ? "TAK" : "NIE")
                  .Append(" | zwrot przebudow: wsie zaplacily miastom dzis ").Append(_dReb).Append(", srednia 7 dob ").Append(_dRebAvg.ToString("0", Inv))
                  .Append(", do zwrotu (najwyzej kasa ponad progiem) ").Append(_dRet).Append(", od linii wsie zaplacily ").Append(_rebL)
                  .Append(" | od linii (dob ").Append(_days).Append(", z wyplata ").Append(_payDays).Append("): zdjete z kas miast ").Append(_took).Append(" = do kies wsi ").Append(_gave)
                  .Append(" (kontrola zdjete - do wsi = ").Append(_took - _gave).Append("; wyplat miasto-doby ").Append(_paidTD).Append(", wsi ").Append(_paidV).Append(")")
                  .Append(", na dobe ").Append(_days > 0 ? (_took / _days).ToString(Inv) : "0")
                  .Append(" | bez wyplaty (miasto-doby ponad progiem): oblezenie / zdobycie ").Append(_siege).Append(", domy najpierw ").Append(_houses).Append(", bez wsi ").Append(_noVil)
                  .Append(" | od startu sesji do kies wsi ").Append(_sessTook)
                  .Append(" | czas ").Append((_days > 0 ? _msSum / _days : 0).ToString("0.00", Inv)).Append(" ms (najwiecej ").Append(_msMax.ToString("0.00", Inv)).Append(")")
                  .Append(" | potkniecia ").Append(_stum).Append(" (sesja ").Append(_stumSess).Append(").");
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Stumble("Line", e); }
            Zero();
        }

        private static bool SafeA7RuleOn() { try { return StartCalibration.A7RuleOn; } catch { return false; } }
    }
}
