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
    ///    za przenoszone rece) Z KIESY WSI: material z polki kasy miasta targowego (zaplata do kasy - matIn) i placa rzemieslnikow (lud miasta; przy kiesach ludu ponad
    ///    pasmem f5 czesc do kasy - PayVillageWage, wageCof). To nowy, staly przeplyw kiesy wsi -> kasa miasta, ktorego bez A7 nie ma. Zmiany zaczynaja sie po 60 dobach
    ///    nacisku od D (log 160 dob WGRANIA 91: D 109126, trzymane >= 60 dob 7.7% blokow), wiec w biegu 160 dob przyplyw siedzi akurat w oknie NAKUM (ostatnie 28 dob);
    ///  - przy czynnej A7 zajecia wiosek pobocznych tez ida za sygnalem KS (SideVillageCraftKsSignal - ta sama przebudowa z kiesy wsi do kasy);
    ///  - istniejace ujscia nadwyzki kas (M6 do kupcow P/R, przeglad firmy DS wedlug nadwyzki co 14 dob i kwartalny wedlug doplywu netto, gersuma f7 - do PANOW
    ///    i KORONY) dzialaja z opoznieniem i zadne nie oddaje pieniadza wsiom, z ktorych A7 go zabiera (kiesy wsi bez A7 i tak -0.24%/d - NAKUM-5). Do tego przeglad
    ///    kwartalny podnosil firmy od pieniedzy przebudow (doplyw netto kupcow) - przejsciowy przyplyw zamieniony w stala rente pana i korony (NKA-1 po przegladzie).
    /// REGULA (sredniowiecze: rada miasta z nadwyzki oplaca roboty miejskie - murage, pavage, pontage - najmujac furmanki i rece z wsi okregu; w grze to tylko
    /// przeplyw zlota kasa miasta -> kiesy wsi, bez postepu budowy ani dobrobytu):
    ///  - od dnia, w ktorym firmy DS sa ustalone (DsTowns.FarmsLive - wczesniej kasy oddaja nadwyzke panom zaworem), co dobe: S = srednia wykladnicza 7 dob sumy kas
    ///    wszystkich miast (pomiar, takze przed startem); ZWROT miasta r = srednia 7 dob tego, co przebudowy zajec wsi wplacily DO JEGO KASY (material + placa
    ///    nieprzyjeta przez lud; VillageCrafts.PayRebuild -> NoteRebuild, A7 i ZW) - placa przyjeta przez lud miasta zostaje u rzemieslnikow, nie w kasie;
    ///  - START: pierwsza doba z przebudowami zajec A7 w toku (StartCalibration.A7Rebuilding) - G0 = wieksze z S i sumy kas tej doby (zapisane; start neutralny). Bez A7
    ///    regula tylko mierzy;
    ///  - PULAP = wieksze z: G0 i suma progow wydatkow miast (DsTowns.HireKeep: 1.5 x zapas kupcow, z firma DS ten sam prog co gersuma + reszta gersumy + zaleglosci
    ///    firm; do tego otwarte rachunki zywnosci miasta - KsFoodCredit);
    ///  - S ponad pulapem: miasta wydaja dzis razem najwyzej (S - pulap) / 28 (ok. miesiac - jak M6), najwyzej tyle, o ile suma kas jest ponad suma progow, i najwyzej
    ///    R = suma zwrotow wszystkich miast (to, co przebudowy wplacily do kas swiata). Recenzja p6-a7: ZWROT ZBIORCZY - najpierw kazde miasto ponad wlasnym progiem
    ///    za swoj okreg (najwyzej swoj r i kasa ponad progiem); reszta R (miasta pod progiem, oblezone, z "domami najpierw", bez wsi) - miasta ponad progiem wedlug
    ///    ognisk swojego okregu (ta sama placa na dom w kazdym okregu; w stosunku kasy model9b.py dawal najbogatszemu miastu 8-14% calego zwrotu do kilku wsi),
    ///    kazde najwyzej pozostala kasa ponad progiem (wyrownanie, najwyzej 4 przebiegi). Zadne miasto nie schodzi ponizej swojego progu. Dawniej kazde miasto tylko
    ///    swoj r: pieniadz przebudow w miastach pod progiem zostawal w kasach (model rev_model_pertown.py: +12..+52 tys./d w oknie); zbiorczo kasy razem nie zaleza
    ///    od tego, gdzie A7 wplaca (model9.py / model10.py: doplyw +35 tys./d -> kasy w oknie -0.2 tys./d, +70 tys./d -> +2.7 tys./d; bez A7 -3.2 tys./d jak log);
    ///  - zloto z kasy miasta do kies wsi, ktorych miastem targowym jest (TradeBound, inaczej Bound - te same wsie, ktore placa mu przebudowe A7), wedlug ognisk; wies
    ///    spladrowana / najezdzana nie bierze. Bez wyplaty: oblezenie / swiezo zdobyte (DsTowns.RemitOf), "domy najpierw" (KsFolkIncome.HousesFirst - R/P zbiedniali,
    ///    nadwyzka najpierw do ich domow przez M6, jak gersuma), miasto bez wsi do najecia;
    ///  - wszystkie miasta (AI i gracza) i wszystkie ich wsie; zamki - nie (kasy zamkow maja zawor do panow, 110).
    /// PIENIADZ: przelew kasa miasta -> kiesy wsi (oba w "Pieniadz swiata" - suma swiata bez zmiany, bez nowego zrodla ani ujscia); w ksiedze "Przeplywy osad" osobna
    /// pozycja naszego ticku (MoneyLedger.MTownHire, Mark zaraz po tej regule); kontrola linii: zdjete z kas (zmierzone na kasie) = do kies wsi (zmierzone u wsi).
    /// DOPLYW NETTO KUPCOW (recenzja p6-a7, CLAUDE.md 8.0 - bez liczenia dwa razy): od startu reguly pieniadz przebudow wplacony do kasy i zwrot z kasy sa POZA doplywem
    /// netto (TownPurse.NoteRebuildLoop: + wplata, - zwrot; okno blok-blok -> NetOf -> ustalenie i przeglad firm DS). Przebudowa to jednorazowy wydatek wsi, ktory miasta
    /// oddaja - firma pana i korony nie rosnie od pieniedzy juz oddanych wsiom (ani w miescie, za ktore oddaje inne miasto). Zwrot NIE jest poborem pana i korony
    /// (bez TownPurse.NoteTake: linia udzialu zysku kupcow i kolumna "pobrane" kasy-miast.csv bez zmian). Przeglad wedlug nadwyzki (f5) i gersuma (f7) widza prawdziwa kase.
    /// ZAPIS arm_townhire ("th2|doba startu|G0|S|doba S|id miasta:zwrot,..."), SaveText.Sync; brak klucza albo inna wersja (stary zapis, kampania Jeffa) = srednia od
    /// pierwszej doby z firmami DS po wczytaniu, zwrot od zera, start przy pierwszej dobie z przebudowami A7. Reset przy nowej kampanii / przed wczytaniem
    /// (ArmouryBehavior, osobna linia _p6a7Reset). Wyjatki per miejsce liczone (potkniecia), bez globalnego wylacznika. Watek glowny (tick dobowy); liczniki
    /// przebudow (NoteRebuild) pod blokada.
    /// </summary>
    internal static class TownHire
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private const string Ver = "th2";                // th2: zwrot od wplat przebudow DO KASY (th1 - od calego wydatku wsi; nie wgrane, odrzucane)
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
        private static readonly List<double> _payE = new List<double>(128);
        private static readonly List<double> _payH = new List<double>(128);   // ogniska zdrowych wsi okregu (reszta zwrotu wedlug rak)
        private static readonly List<double> _payA = new List<double>(128);   // reszta zwrotu dzis (za miasta pod progiem)
        private static readonly List<Settlement> _loopT = new List<Settlement>(128);
        private static readonly List<long> _loopX = new List<long>(128);
        private static readonly Dictionary<Settlement, long> _bills = new Dictionary<Settlement, long>();
        private static readonly Dictionary<Settlement, int> _billsOld = new Dictionary<Settlement, int>();
        // zwrot: wplaty przebudow zajec wsi do kasy miasta targowego (VillageCrafts.PayRebuild) - dzis i srednia wykladnicza 7 dob (zapis); wydatek wsi dzis (INFO)
        private static readonly object _rebLock = new object();
        private static readonly Dictionary<Town, long> _rebToday = new Dictionary<Town, long>();
        private static long _vilToday;
        private static readonly Dictionary<Town, double> _rebAvg = new Dictionary<Town, double>();
        private static Dictionary<string, double> _rebLoad;              // z zapisu (id osady) - do pierwszej doby pomiaru
        private static long _dReb, _dVil; private static double _dRebAvg, _dOwn, _dEx;
        private static long _rebL, _vilL, _loopL, _loopOut;              // od ostatniej linii: wplaty przebudow do kas, wydatki wsi, wplaty i zwrot poza doplywem netto kupcow

        // stan ostatniej doby (linia)
        private static long _dG, _dKeep, _dCap, _dPlan; private static double _dS; private static int _dTowns, _dOver; private static bool _dA7;
        // liczniki od ostatniej linii
        private static long _took, _gave, _sessTook, _tookOwn, _tookOther;
        private static int _days, _payDays, _paidTD, _paidV, _siege, _houses, _noVil, _stum, _stumSess;
        private static double _msSum, _msMax;
        private static readonly HashSet<string> _err = new HashSet<string>(StringComparer.Ordinal);

        internal static bool Key { get { var s = Settings.Current; return s != null && s.TownWorksHireDistrict; } }

        internal static void Reset()
        {
            _start = -1; _lastDay = -1; _sDay = -1; _g0 = 0; _s = double.NaN; _vil.Clear(); _vilDay = -1;
            _payT.Clear(); _payW.Clear(); _payE.Clear(); _payH.Clear(); _payA.Clear(); _loopT.Clear(); _loopX.Clear(); _bills.Clear(); _billsOld.Clear();
            lock (_rebLock) { _rebToday.Clear(); _vilToday = 0; }
            _rebAvg.Clear(); _rebLoad = null;
            _dG = _dKeep = _dCap = _dPlan = 0; _dS = 0; _dTowns = _dOver = 0; _dA7 = false; _dReb = _dVil = 0; _dRebAvg = _dOwn = _dEx = 0;
            _sessTook = 0; _stumSess = 0; _err.Clear();
            Zero();
        }

        private static void Zero()
        {
            _took = _gave = _tookOwn = _tookOther = 0; _days = _payDays = _paidTD = _paidV = _siege = _houses = _noVil = _stum = 0; _msSum = _msMax = 0;
            _rebL = _vilL = _loopL = _loopOut = 0;
        }

        /// <summary>VillageCrafts.PayRebuild (przebudowa zajecia - regula A7 i ZW): `vil` zlota zeszlo z kiesy wsi do miasta targowego `mt`, z tego `purse` weszlo do
        /// kasy miasta (material z polki, placa nieprzyjeta przez lud - zmierzone na kasie). Tylko liczniki dnia (zwrot TownHire liczy srednia z `purse`). Blokada -
        /// na wypadek wolania spoza watku glownego; nie rzuca.</summary>
        internal static void NoteRebuild(Town mt, long vil, long purse)
        {
            try
            {
                if (mt == null) return;
                lock (_rebLock)
                {
                    if (vil > 0) _vilToday += vil;
                    if (purse > 0) { long v; _rebToday.TryGetValue(mt, out v); _rebToday[mt] = v + purse; }
                }
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

        /// <summary>Ogniska zdrowych wsi okregu miasta (spladrowana / najezdzana - nie; kazda najmniej 1); 0 - miasto bez wsi do najecia.</summary>
        private static double DistrictHearths(Settlement st)
        {
            List<Village> l;
            if (!_vil.TryGetValue(st, out l)) return 0;
            double h = 0;
            foreach (var v in l) if (v != null && v.VillageState == Village.VillageStates.Normal) h += Math.Max(1.0, v.Hearth);
            return h;
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

        /// <summary>Od startu reguly: wplaty przebudow do kas tej doby poza doplywem netto kupcow (TownPurse.NoteRebuildLoop) - ta sama doba i to samo okno blok-blok co
        /// zloto (PayRebuild biegnie w tym ticku przed TownHire, po TownPurse.BeforeRents).</summary>
        private static void PushLoop()
        {
            if (_start < 0) return;
            for (int i = 0; i < _loopT.Count; i++)
            {
                try { if (TownPurse.NoteRebuildLoop(_loopT[i], _loopX[i])) _loopL += _loopX[i]; }
                catch (Exception e) { Stumble("Loop", e); }
            }
        }

        // ------------------------------------------------------------ doba (ArmouryBehavior: po KS i VillageCrafts - przebudowy A7 tej doby juz w kasach; potem Mark MTownHire)
        internal static void Daily()
        {
            int today = (int)CampaignTime.Now.ToDays;
            if (_lastDay == today) return;
            _lastDay = today;
            if (!Key || !DsTowns.FarmsLive) { lock (_rebLock) { _rebToday.Clear(); _vilToday = 0; } return; }   // kasy oddaja nadwyzke panom zaworem (firmy DS nie ustalone) - pomiar i start pozniej
            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            try
            {
                Villages(today);
                _days++;
                _payT.Clear(); _payW.Clear(); _payE.Clear(); _payH.Clear(); _payA.Clear(); _loopT.Clear(); _loopX.Clear();
                try { KsFoodCredit.OlderThanByDst(-1, _bills, _billsOld); } catch (Exception e) { _bills.Clear(); Stumble("Bills", e); }
                bool gap = double.IsNaN(_s) || _sDay < 0 || today - _sDay > GapDays;
                long gSum = 0, reb = 0, vil = 0; double keepSum = 0, own = 0, exSum = 0, rebAvg = 0; int n = 0, nOver = 0, nSiege = 0, nHouses = 0, nNoVil = 0;
                lock (_rebLock)
                {
                    vil = _vilToday; _vilToday = 0;
                    foreach (var t in Town.AllTowns)
                    {
                        try
                        {
                            var st = t != null ? t.Settlement : null;
                            if (st == null) continue;
                            // zwrot: srednia 7 dob wplat przebudow do kasy tego miasta (po wczytaniu - z zapisu; po przerwie - od dzis)
                            long o; _rebToday.TryGetValue(t, out o);
                            if (o > 0) { _loopT.Add(st); _loopX.Add(o); }
                            double r;
                            if (!_rebAvg.TryGetValue(t, out r))
                            {
                                r = 0;
                                double lr;
                                if (_rebLoad != null && st.StringId != null && _rebLoad.TryGetValue(st.StringId, out lr)) r = lr;
                            }
                            r = gap ? o : r + (o - r) / AvgDays;
                            if (r >= 0.5) _rebAvg[t] = r; else { _rebAvg.Remove(t); r = 0; }
                            reb += o; rebAvg += r;
                            double keep = KeepOf(st, t);
                            if (double.IsNaN(keep)) continue;
                            long g = t.Gold;
                            n++; gSum += g; keepSum += keep;
                            double ex = g - keep;
                            if (!(ex >= 1)) continue;
                            nOver++;
                            if (DsTowns.RemitOf(st) != 0) { nSiege++; continue; }
                            if (KsFolkIncome.HousesFirst(st)) { nHouses++; continue; }
                            double hh = DistrictHearths(st);
                            if (!(hh > 0)) { nNoVil++; continue; }
                            double w = Math.Min(r, ex);                              // najpierw za swoj okreg
                            _payT.Add(t); _payW.Add(w); _payE.Add(ex); _payH.Add(hh); _payA.Add(0); own += w; exSum += ex;
                        }
                        catch (Exception e) { Stumble("Town", e); }
                    }
                    _rebToday.Clear();
                }
                _rebLoad = null;
                _rebL += reb; _vilL += vil;
                if (n == 0) { PushLoop(); return; }
                if (gap) _s = gSum;                                          // pierwsza doba pomiaru albo po przerwie - srednia od sumy dzis
                else _s += (gSum - _s) / AvgDays;
                _sDay = today;
                bool a7 = false;
                try { a7 = StartCalibration.A7Rebuilding; } catch (Exception e) { Stumble("A7", e); }
                if (_start < 0 && a7) { _start = today; _g0 = (long)Math.Round(Math.Max(_s, gSum)); }   // pierwsza doba przebudow A7: pulap = to, co kasy maja (srednia 7 dob albo dzis - wiecej)
                PushLoop();                                                  // od startu: wplaty przebudow poza doplywem netto kupcow (firmy DS)
                double cap = Math.Max(_start >= 0 ? _g0 : 0, keepSum);
                double plan = _start >= 0 ? (_s - cap) / SpendDays : 0;
                plan = Math.Min(plan, gSum - keepSum);                       // suma kas nie schodzi ponizej sumy progow
                plan = Math.Min(plan, rebAvg);                               // razem najwyzej to, co przebudowy wplacily do kas swiata (srednia 7 dob)
                plan = Math.Min(plan, exSum);                                // kazde miasto najwyzej kasa ponad wlasnym progiem
                _dG = gSum; _dKeep = (long)keepSum; _dCap = (long)cap; _dS = _s; _dTowns = n; _dOver = nOver; _dA7 = a7; _dPlan = plan >= 1 ? (long)Math.Floor(plan) : 0;
                _dReb = reb; _dVil = vil; _dRebAvg = rebAvg; _dOwn = own; _dEx = exSum;
                if (_start < 0) return;                                      // przed A7 - sam pomiar (bez licznikow "bez wyplaty")
                _siege += nSiege; _houses += nHouses; _noVil += nNoVil;
                if (_dPlan < 1 || !(exSum > 0)) return;
                _payDays++;
                // najpierw za swoj okreg (w stosunku zwrotow, gdy pulap tnie); reszta (za miasta pod progiem i bez wyplaty) - miasta ponad progiem wedlug ognisk swojego
                // okregu (ta sama placa na dom w kazdym okregu - bez skupienia pieniedzy we wsiach najbogatszego miasta), kazde najwyzej pozostala kasa ponad progiem
                // (wyrownanie: miasto, ktoremu brakuje kasy, placi swoja reszte, a jego czesc dziela inne - najwyzej 4 przebiegi)
                double k1 = _dPlan <= own ? (own > 0 ? _dPlan / own : 0) : 1.0;
                double left = _dPlan > own ? _dPlan - own : 0;
                for (int it = 0; it < 4 && left >= 1; it++)
                {
                    double hs = 0;
                    for (int i = 0; i < _payT.Count; i++) if (_payE[i] - _payW[i] * k1 - _payA[i] >= 1) hs += _payH[i];
                    if (!(hs > 0)) break;
                    double used = 0;
                    for (int i = 0; i < _payT.Count; i++)
                    {
                        double room = _payE[i] - _payW[i] * k1 - _payA[i];
                        if (!(room >= 1)) continue;
                        double add = Math.Min(room, left * _payH[i] / hs);
                        _payA[i] += add; used += add;
                    }
                    left -= used;
                    if (!(used >= 1)) break;
                }
                for (int i = 0; i < _payT.Count; i++)
                {
                    try
                    {
                        double a = _payW[i] * k1, b = _payA[i];
                        long x = (long)Math.Floor(Math.Min(_payE[i], a + b));
                        if (x < 1) continue;
                        long took = Pay(_payT[i].Settlement, _payT[i], x);
                        if (took <= 0) continue;
                        long ownPart = Math.Min(took, (long)Math.Floor(a));
                        _tookOwn += ownPart; _tookOther += took - ownPart;
                    }
                    catch (Exception e) { Stumble("Pay", e); }
                }
            }
            catch (Exception e) { Stumble("Daily", e); }
            finally
            {
                _payT.Clear(); _payW.Clear(); _payE.Clear(); _payH.Clear(); _payA.Clear(); _loopT.Clear(); _loopX.Clear();
                double ms = (System.Diagnostics.Stopwatch.GetTimestamp() - t0) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
                _msSum += ms; if (ms > _msMax) _msMax = ms;
            }
        }

        /// <summary>Kasa miasta -> kiesy wsi okregu wedlug ognisk (reszta z zaokraglen do najwiekszej). Zwraca zloto zdjete z kasy (zmierzone).</summary>
        private static long Pay(Settlement st, Town t, long want)
        {
            List<Village> l;
            if (st == null || !_vil.TryGetValue(st, out l) || l.Count == 0) { _noVil++; return 0; }
            double w = 0; Village top = null; double topW = -1;
            foreach (var v in l)
            {
                if (v == null || v.VillageState != Village.VillageStates.Normal) continue;
                double x = Math.Max(1.0, v.Hearth);
                w += x;
                if (x > topW) { topW = x; top = v; }
            }
            if (!(w > 0) || top == null) { _noVil++; return 0; }
            int ask = (int)Math.Min(int.MaxValue / 2, Math.Min(want, Math.Max(0, t.Gold)));
            if (ask < 1) return 0;
            int g0 = t.Gold;
            t.ChangeGold(-ask);
            long took = (long)g0 - t.Gold;                                // zmierzone na kasie
            if (took <= 0) return 0;
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
            // recenzja p6-a7: zwrot poza doplywem netto kupcow (jak wplata przebudowy - TownPurse.NoteRebuildLoop) i NIE jako pobor pana i korony (bez NoteTake)
            try { if (TownPurse.NoteRebuildLoop(st, -took)) _loopOut += took; } catch (Exception e) { Stumble("Loop", e); }
            _took += took; _gave += gave; _sessTook += took; _paidTD++; _paidV += nv;
            return took;
        }

        // ------------------------------------------------------------ linia (po Daily, w dobie linii; liczniki od ostatniej linii)
        internal static void Line()
        {
            if (!Key || !LogGate.Lines) return;
            try
            {
                int day = (int)CampaignTime.Now.ToDays;
                var sb = new StringBuilder(1200);
                sb.Append("Roboty miast z rak okregu (p6-a7): dzien ").Append(day).Append(" | klucz Town Works Hire District TAK, ");
                if (!DsTowns.FarmsLive && _start < 0) sb.Append("czeka na firmy DS (FarmsLive NIE)");
                else if (_start < 0) sb.Append("czeka na przebudowy zajec A7 (regula A7 ").Append(SafeA7RuleOn() ? "TAK" : "NIE").Append(", sam pomiar)");
                else sb.Append("od doby ").Append(_start).Append(" (pierwsza doba przebudow A7; pulap = max(kasy razem w dniu startu, suma progow wydatkow); dziennie 1/28 nadwyzki sredniej 7 dob, razem najwyzej wplaty przebudow do kas, najpierw miasto za swoj okreg)");
                sb.Append(" | doba: miast ").Append(_dTowns).Append(", kasy razem ").Append(_dG).Append(", srednia 7 dob ").Append(_dS.ToString("0", Inv))
                  .Append(", pulap startu ").Append(_g0).Append(", progi razem ").Append(_dKeep).Append(", pulap ").Append(_dCap)
                  .Append(", nadwyzka sredniej ").Append(((long)Math.Round(_dS - _dCap)).ToString(Inv)).Append(", wyplata dzis ").Append(_start >= 0 ? _dPlan : 0)
                  .Append(", miast ponad wlasnym progiem ").Append(_dOver).Append(", przebudowy A7 ").Append(_dA7 ? "TAK" : "NIE")
                  .Append(" | zwrot przebudow: wsie zaplacily miastom dzis ").Append(_dVil).Append(", do kas ").Append(_dReb).Append(", srednia 7 dob do kas ").Append(_dRebAvg.ToString("0", Inv))
                  .Append(", miasta za swoj okreg (najwyzej kasa ponad progiem) ").Append(((long)Math.Floor(_dOwn)).ToString(Inv))
                  .Append(", kasa ponad progiem razem ").Append(((long)Math.Floor(_dEx)).ToString(Inv))
                  .Append(", od linii wsie zaplacily ").Append(_vilL).Append(", do kas ").Append(_rebL).Append(", poza doplywem netto kupcow wplaty ").Append(_loopL).Append(" i zwrot ").Append(_loopOut)
                  .Append(" | od linii (dob ").Append(_days).Append(", z wyplata ").Append(_payDays).Append("): zdjete z kas miast ").Append(_took).Append(" = do kies wsi ").Append(_gave)
                  .Append(" (kontrola zdjete - do wsi = ").Append(_took - _gave).Append("; wyplat miasto-doby ").Append(_paidTD).Append(", wsi ").Append(_paidV).Append(")")
                  .Append(", za swoj okreg ").Append(_tookOwn).Append(", za okregi miast pod progiem ").Append(_tookOther)
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
