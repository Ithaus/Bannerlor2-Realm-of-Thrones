using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// 435-L2 (lancuch podazy, krok 2 = poprawka A4 audytu ekonomii 11.11): ODBLOKOWANIE ZMIANY ZAJEC. Jedna regula dla obu drog (ZW bez KS - VillageCrafts,
    /// A7 pod KS - StartCalibration.Rule.cs); ta klasa trzyma tylko wzory, klucze, liczniki i linie - stan przebudow zostaje tam, gdzie byl.
    ///
    /// Problem z logu (Armoury-2026-11-11_13-19-13.log, ZW d109809): nacisk 547, zaczete 0, w przebudowie 70, wstrzymane 266 (inna wioska okregu) + 230 (udzial
    /// regionu). Hamulec Jeffa pozwala na JEDNA czesc okregu w przebudowie i 5% rak regionu na sezon - a przebudowa bez materialu na polce stoi do roku po terminie
    /// i przez ten czas trzyma oba miejsca.
    ///
    /// 1. POSTOJ (klucz Rebuild Stall Release Days, 0 = jak dotad). Zaleglosc przebudowy = doby od poczatku - czesc zaplacona x doby przejscia (ile dob raty
    ///    przebudowa jest za planem; 0 przy koszcie 0 - pola mieszczan). Liczona z pol, ktore juz sa w zapisie (poczatek, koniec, koszt, zaplacone) - ZADNEGO nowego
    ///    stanu, wczytanie niczego nie gubi, stara DLL czyta ten sam zapis.
    ///    - zaleglosc >= N: przebudowa STOI - nie zajmuje miejsca "jedna czesc okregu naraz" i oddaje regionowi nieuzyta czesc udzialu (rece, ktore jeszcze nie
    ///      przeszly; tylko przebudowy zaczete w biezacym sezonie - licznik regionu i tak zeruje sie co sezon). Sama przebudowa trwa dalej: gdy zaplaci i nadgoni
    ///      ponizej N, znow zajmuje miejsce.
    ///    - zaleglosc >= max(91, N) i nie zaplacona w calosci: PORZUCONA (dotad rok po terminie); rece zostaja, gdzie doszly - ta sama galaz kodu co dotad.
    ///    Hamulec Jeffa zostaje caly: x1.25 przez 60 dob, jedna NIEstojaca czesc okregu naraz, 5% rak regionu na sezon, ziemia, snieg, prog wyzywienia.
    /// 2. BEZPIECZNIK ZBOZA (klucz Grain Hands Winter Lock, NIE = jak dotad). Zmiana zajecia Z wheat_farm nie zaczyna sie (nacisk zostaje, do zboza wolno), gdy:
    ///    - kraina wsi ma zime (WinterBite.WinterNow i czynnik krainy WinterRealm > 0),
    ///    - gospodarze okregu gloduja (Famine - brak jedzenia w ostatniej dobie),
    ///    - plon doby okregu po odejsciu tych rak spadlby ponizej 1.2 x potrzeby ludzi w domu (migawka spichlerza Famine: plon x (1 - rece odchodzace / rece zboza
    ///      okregu) &lt; 1.2 x ludzie w domu).
    ///    Bez migawki spichlerza (Famine wylaczone, wies bez spichlerza) - tylko regula zimy; licznik "bez danych".
    /// Zloto, ludzie, towar: regula niczego nie przenosi (zmienia tylko to, KIEDY zaczyna sie i konczy przebudowa placona dotychczasowa droga).
    /// Linia doby "LP zajecia (435-L2)": przy czynnym kluczu albo przy pomiarze Supply Chain Log (wtedy sam rozklad zaleglosci - do wyboru N).
    /// Stan tylko sesyjny (liczniki linii). Wyjatki liczone (potkniecia), bez globalnego wylacznika.
    /// </summary>
    internal static class CraftUnblock
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        internal const string P = "LP zajecia (435-L2): ";
        internal const int SysZw = 0, SysKs = 1;
        internal const int AbandonDays = 91;          // projekt II.6: po 91 dobach postoju - porzucona
        internal const int MinStallDays = 14;         // rata A7 przy sygnale KS idzie co 7 dob - prog ponizej 14 bralby zwykly rytm raty za postoj
        internal const double GrainNeed = 1.2;        // projekt II.6: plon okregu nie schodzi ponizej 1.2 x wlasnej potrzeby
        private static readonly int[] Edges = { 14, 28, 56, 91 };

        /// <summary>Prog postoju w dobach; 0 - regula nieczynna (stan sprzed 435-L2). Wartosci 1-13 licza sie jak 14.</summary>
        internal static int StallDays
        {
            get
            {
                var s = Settings.Current;
                int n = s != null ? s.RebuildStallReleaseDays : 0;
                return n <= 0 ? 0 : Math.Max(MinStallDays, Math.Min(364, n));
            }
        }

        internal static bool GrainKey { get { var s = Settings.Current; return s != null && s.GrainHandsWinterLock; } }
        private static bool Measure { get { var s = Settings.Current; return s != null && s.SupplyChainLog; } }
        private static bool Counting { get { return StallDays > 0 || GrainKey || Measure; } }

        // liczniki: [sys] - stan ostatniej doby (zerowane linia) i zdarzenia od ostatniej linii
        private static readonly int[] _seen = new int[2], _rel = new int[2], _full = new int[2];
        private static readonly int[] _hist = new int[2 * 5];
        private static readonly double[] _maxLag = new double[2];
        private static readonly int[] _abandon = new int[2], _startPast = new int[2], _startRefund = new int[2];
        private static readonly double[] _refundHands = new double[2], _refundUsed = new double[2];
        private static readonly int[] _gWinter = new int[2], _gHunger = new int[2], _gNeed = new int[2], _gNoData = new int[2], _gPass = new int[2];
        private static readonly double[] _gHands = new double[2];
        private static int _st, _stAll;
        private static readonly HashSet<string> _err = new HashSet<string>();
        private static bool _startLogged;

        internal static void Reset()
        {
            Zero();
            _stAll = 0; _err.Clear(); _startLogged = false;
        }

        private static void Zero()
        {
            for (int i = 0; i < 2; i++)
            {
                _seen[i] = _rel[i] = _full[i] = 0; _maxLag[i] = 0;
                _abandon[i] = _startPast[i] = _startRefund[i] = 0; _refundHands[i] = _refundUsed[i] = 0;
                _gWinter[i] = _gHunger[i] = _gNeed[i] = _gNoData[i] = _gPass[i] = 0; _gHands[i] = 0;
            }
            Array.Clear(_hist, 0, _hist.Length);
            _st = 0;
        }

        private static void Stumble(string where, Exception e)
        {
            _st++; _stAll++;
            try { if (_err.Add(where)) Log.Error("CraftUnblock." + where, e); } catch { }
        }

        // ============================================================ 1. postoj
        /// <summary>Zaleglosc przebudowy w dobach: doby od poczatku minus czesc zaplacona x doby przejscia. 0 - koszt 0 (nie ma czego placic) albo zle dane.</summary>
        internal static double Lag(int today, int tStart, int tEnd, double cost, double paid)
        {
            if (!(cost > 0) || tStart < 0 || tEnd < tStart) return 0.0;
            int T = Math.Max(1, tEnd - tStart);
            double fPay = paid >= cost - 0.5 ? 1.0 : Math.Max(0.0, paid / cost);
            double lag = (today - tStart) - fPay * T;
            return lag > 0 && !double.IsNaN(lag) ? lag : 0.0;
        }

        /// <summary>Przebudowa stoi (zaleglosc >= prog) - nie zajmuje miejsca w okregu i oddaje regionowi nieuzyta czesc udzialu. false przy kluczu 0.</summary>
        internal static bool Released(double lag) { int n = StallDays; return n > 0 && lag >= n; }

        /// <summary>Przebudowa porzucona nowa regula: klucz czynny, nie zaplacona w calosci, zaleglosc >= max(91, prog).</summary>
        internal static bool Abandon(double lag, double cost, double paid)
        {
            int n = StallDays;
            return n > 0 && cost > 0 && paid < cost - 0.5 && lag >= Math.Max(AbandonDays, n);
        }

        /// <summary>Poczatek przegladu przebudow drogi `sys` w tej dobie: stan doby od zera (linia pokazuje ostatnia dobe, zdarzenia - od ostatniej linii).</summary>
        internal static void BeginDay(int sys)
        {
            if (sys < 0 || sys > 1) return;
            _seen[sys] = _rel[sys] = _full[sys] = 0; _maxLag[sys] = 0;
            for (int i = 0; i < 5; i++) _hist[sys * 5 + i] = 0;
        }

        /// <summary>Przebudowa w toku tej doby (stan do linii): rozklad zaleglosci, stojace wedlug progu, zaplacone w calosci.</summary>
        internal static void Seen(int sys, double lag, bool paidFull)
        {
            if (sys < 0 || sys > 1 || !Counting) return;
            _seen[sys]++;
            int b = 0; while (b < Edges.Length && lag >= Edges[b]) b++;
            _hist[sys * 5 + b]++;
            if (lag > _maxLag[sys]) _maxLag[sys] = lag;
            if (paidFull) _full[sys]++;
            if (Released(lag)) _rel[sys]++;
        }

        internal static void NoteAbandon(int sys) { if (sys >= 0 && sys <= 1) _abandon[sys]++; }
        /// <summary>Zmiana zaczeta w okregu, w ktorym inna czesc stoi w przebudowie (bez reguly bylaby "wstrzymana - inna czesc okregu").</summary>
        internal static void NoteStartPast(int sys) { if (sys >= 0 && sys <= 1) _startPast[sys]++; }
        /// <summary>Zmiana zaczeta dzieki udzialowi regionu oddanemu przez stojace przebudowy (bez zwrotu bylaby "wstrzymana - udzial regionu").</summary>
        internal static void NoteStartRefund(int sys, double hands) { if (sys >= 0 && sys <= 1) { _startRefund[sys]++; _refundUsed[sys] += hands; } }
        /// <summary>Rece oddane regionom przez stojace przebudowy w dobie kroku reguly (stan kroku).</summary>
        internal static void NoteRefund(int sys, double hands) { if (sys >= 0 && sys <= 1) _refundHands[sys] += hands; }

        // ============================================================ 2. bezpiecznik zboza
        /// <summary>
        /// Zmiana Z zboza zablokowana? 0 - nie (albo klucz wylaczony), 1 - zima krainy, 2 - gospodarze gloduja, 3 - plon po odejsciu ponizej 1.2 x potrzeby.
        /// `v` - wies gry okregu (null - pola mieszczan: tylko zima miasta `t`), `leave` - rece odchodzace od zboza, `wheat` - rece zboza okregu. Watek glowny, nie rzuca.
        /// </summary>
        internal static int GrainLock(int sys, Village v, Town t, double leave, double wheat, int today)
        {
            if (sys < 0 || sys > 1 || !GrainKey) return 0;
            try
            {
                if (WinterBite.WinterNow())
                {
                    float f = v != null ? WinterRealm.OfVillage(v) : (t != null ? WinterRealm.OfTown(t) : 0f);
                    if (f > 0f) { _gWinter[sys]++; _gHands[sys] += leave; return 1; }
                }
                if (v == null) { _gPass[sys]++; return 0; }
                double home, harvest, food, cap; float u; int day;
                if (!Famine.DayOf(v, out home, out harvest, out food, out cap, out u, out day) || day < today - 2 || !(home > 0)) { _gNoData[sys]++; return 0; }
                if (u > 0f) { _gHunger[sys]++; _gHands[sys] += leave; return 2; }
                double x = wheat > 0 ? Math.Max(0.0, Math.Min(1.0, leave / wheat)) : 1.0;
                if (harvest * (1.0 - x) < GrainNeed * home) { _gNeed[sys]++; _gHands[sys] += leave; return 3; }
                _gPass[sys]++;
                return 0;
            }
            catch (Exception e) { Stumble("GrainLock", e); return 0; }
        }

        // ============================================================ linia
        /// <summary>ArmouryBehavior.OnDailyTick, po StartCalibration.Daily i VillageCrafts.Daily (stan tej doby): linia startu raz na sesje, linia doby w takcie logu.</summary>
        internal static void Daily()
        {
            try
            {
                int n = StallDays; bool g = GrainKey, m = Measure;
                if (!_startLogged)
                {
                    _startLogged = true;
                    var s = Settings.Current;
                    Log.Info(P + "start | postoj przebudowy (Rebuild Stall Release Days): " + (n > 0 ? n + " dob zaleglosci raty - zwalnia miejsce w okregu i nieuzyty udzial regionu; porzucenie po " + Math.Max(AbandonDays, n) + " dobach zaleglosci (dotad rok po terminie)" : "0 - WYLACZONE (jak dotad: rok po terminie)")
                             + (s != null && s.RebuildStallReleaseDays > 0 && s.RebuildStallReleaseDays < MinStallDays ? " [ustawione " + s.RebuildStallReleaseDays + " - liczone jak " + MinStallDays + "]" : "")
                             + " | bezpiecznik zboza (Grain Hands Winter Lock): " + (g ? "TAK - zima krainy, glod gospodarzy, plon po odejsciu < " + GrainNeed.ToString("0.0", Inv) + " x potrzeby" : "WYLACZONY (jak dotad)")
                             + " | drogi: ZW bez KS " + (VillageCrafts.KeyOn ? "czynna" : "klucz wylaczony") + ", A7 pod KS " + (StartCalibration.A7RuleOn ? "czynna" : "sam pomiar (zmian nie ma)")
                             + " | stan: bez nowych pol w zapisie (zaleglosc liczona z poczatku, konca, kosztu i zaplaconego).");
                }
                if (!LogGate.Lines) return;
                if (n <= 0 && !g && !m) { Zero(); return; }
                var sb = new StringBuilder(900);
                sb.Append(P).Append("dzien ").Append((int)CampaignTime.Now.ToDays - 1)
                  .Append(" | prog postoju ").Append(n > 0 ? n.ToString(Inv) + " dob" : "0 (wylaczony - sam pomiar)").Append(", bezpiecznik zboza ").Append(g ? "TAK" : "NIE");
                int ctl = 0;
                for (int sys = 0; sys < 2; sys++)
                {
                    int o = sys * 5, sum = _hist[o] + _hist[o + 1] + _hist[o + 2] + _hist[o + 3] + _hist[o + 4];
                    ctl += _seen[sys] - sum;
                    sb.Append(sys == SysZw ? " | ZW (bez KS): " : " | A7 (pod KS): ")
                      .Append("w przebudowie ").Append(_seen[sys]).Append(" (zaplacone w calosci ").Append(_full[sys]).Append(")")
                      .Append(", zaleglosc raty <14 / 14-27 / 28-55 / 56-90 / >=91 dob: ").Append(_hist[o]).Append(" / ").Append(_hist[o + 1]).Append(" / ").Append(_hist[o + 2]).Append(" / ").Append(_hist[o + 3]).Append(" / ").Append(_hist[o + 4])
                      .Append(", najwieksza ").Append(_maxLag[sys].ToString("0", Inv))
                      .Append(", stojace wedlug progu (zwolnione postojem) ").Append(_rel[sys]).Append(", zajmujace miejsce ").Append(_seen[sys] - _rel[sys])
                      .Append(" | od ostatniej linii: zaczete obok stojacej ").Append(_startPast[sys])
                      .Append(", zaczete dzieki zwrotowi udzialu regionu ").Append(_startRefund[sys]).Append(" (").Append(_refundUsed[sys].ToString("0", Inv)).Append(" rak)")
                      .Append(", rece oddane regionom w krokach reguly ").Append(_refundHands[sys].ToString("0", Inv))
                      .Append(", porzucone po ").Append(Math.Max(AbandonDays, n)).Append(" dobach zaleglosci ").Append(_abandon[sys])
                      .Append(", bezpiecznik zboza ").Append(_gWinter[sys] + _gHunger[sys] + _gNeed[sys]).Append(" (zima ").Append(_gWinter[sys]).Append(", glod ").Append(_gHunger[sys])
                      .Append(", plon < ").Append(GrainNeed.ToString("0.0", Inv)).Append(" x potrzeby ").Append(_gNeed[sys]).Append("; rak ").Append(_gHands[sys].ToString("0", Inv))
                      .Append("; przepuszczone ").Append(_gPass[sys]).Append(", bez migawki spichlerza ").Append(_gNoData[sys]).Append(")");
                }
                sb.Append(" | kontrola: przebudowy - rozklad zaleglosci = ").Append(ctl).Append("; zloto, ludzie, towar: regula niczego nie przenosi")
                  .Append(" | potkniecia ").Append(_st).Append(" (od wczytania ").Append(_stAll).Append(").");
                Log.Info(sb.ToString());
                Zero();
            }
            catch (Exception e) { Stumble("Daily", e); }
        }
    }
}
