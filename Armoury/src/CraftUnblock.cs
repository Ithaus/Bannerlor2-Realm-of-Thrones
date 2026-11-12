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
        internal const int MinStallDays = 28;         // 435-L2-p: prog ponizej 28 bralby zwykly rytm kupna na sztuki za postoj (rata A7 co 7 dob, material na sztuki)
        internal const int RefDays = 60;              // 435-L2-p: zaleglosc liczona w skali przejscia 60 dob (przebudowa drzew 364 doby: ta sama czesc planu = ta sama zaleglosc)
        internal const int MaxStalled = 1;            // 435-L2-p: najwyzej tyle stojacych przebudow okregu zwalnia miejsce (1 stojaca + 1 czynna)
        private static readonly int[] Edges = { 14, 28, 56, 91 };

        /// <summary>Prog postoju w dobach; 0 - regula nieczynna (stan sprzed 435-L2). Wartosci 1-27 licza sie jak 28 (435-L2-p).</summary>
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
            _refSeason[0].Clear(); _refSeason[1].Clear(); _minOwe.Clear(); _minOweDay = int.MinValue;
        }

        private static void Zero()
        {
            for (int i = 0; i < 2; i++)
            {
                _seen[i] = _rel[i] = _full[i] = 0; _maxLag[i] = 0;
                _abandon[i] = _startPast[i] = _startRefund[i] = 0; _refundHands[i] = _refundUsed[i] = 0;
                _gWinter[i] = _gHunger[i] = _gNeed[i] = _gNoData[i] = _gPass[i] = 0; _gHands[i] = 0;
                _capHeld[i] = _shareSmall[i] = _refBlocked[i] = _gFood[i] = 0;
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
        /// <summary>Zaleglosc przebudowy w dobach SKALI PRZEJSCIA 60 dob: (czesc czasu - czesc zaplacona) x min(doby przejscia, 60). 0 - koszt 0 (nie ma czego placic),
        /// zle dane, zaplacona w calosci (rece dochodza juz tylko wedlug czasu - to nie postoj) albo - przed terminem - rata nalezna mniejsza od `minOwe` (najmniejsza
        /// kwota, za ktora da sie kupic sztuke materialu: zwykly rytm kupna na sztuki, nie postoj). 435-L2-p (recenzje 1, S3, N1).</summary>
        internal static double Lag(int today, int tStart, int tEnd, double cost, double paid, double minOwe = 0.0)
        {
            if (!(cost > 0) || tStart < 0 || tEnd < tStart) return 0.0;
            if (paid >= cost - 0.5) return 0.0;
            int T = Math.Max(1, tEnd - tStart);
            double fPay = Math.Max(0.0, paid / cost);
            double fTime = (today - tStart) / (double)T;
            if (fTime < 1.0 && fTime * cost - paid < Math.Max(0.5, minOwe)) return 0.0;
            double lag = (fTime - fPay) * Math.Min(T, RefDays);
            return lag > 0 && !double.IsNaN(lag) ? lag : 0.0;
        }

        private static readonly Dictionary<Town, double> _minOwe = new Dictionary<Town, double>();
        private static int _minOweDay = int.MinValue;
        /// <summary>435-L2-p: najmniejsza rata, ktora moze cos kupic w miescie targowym `mt` - (1 + rzemieslnicy na zlotowke materialu) x cena najtanszej sztuki materialu
        /// robot dzis (pamiec doby). 0 - klucz postoju 0 (nic nie liczy), brak miasta albo blad. Watek glowny, nie rzuca.</summary>
        internal static double MinOwe(Town mt)
        {
            if (mt == null || StallDays <= 0) return 0.0;
            try
            {
                int d = (int)CampaignTime.Now.ToDays;
                if (d != _minOweDay) { _minOwe.Clear(); _minOweDay = d; }
                double x;
                if (_minOwe.TryGetValue(mt, out x)) return x;
                int min = int.MaxValue;
                var items = BuildFunding.DsWorksItems();
                if (items != null) foreach (var it in items) { if (it == null) continue; int p = Math.Max(1, mt.GetItemPrice(it, null, false)); if (p < min) min = p; }
                var s = Settings.Current;
                double k = s != null ? Math.Max(0.0, Math.Min(3.0, (double)s.DsWorksWagePerMaterial)) : 0.5;
                x = min == int.MaxValue ? 0.0 : (1.0 + k) * min;
                _minOwe[mt] = x;
                return x;
            }
            catch (Exception e) { Stumble("MinOwe", e); return 0.0; }
        }

        /// <summary>Przebudowa stoi (zaleglosc >= prog) - nie zajmuje miejsca w okregu i oddaje regionowi nieuzyta czesc udzialu. false przy kluczu 0.</summary>
        internal static bool Released(double lag) { int n = StallDays; return n > 0 && lag >= n; }

        /// <summary>Przebudowa porzucona nowa regula: klucz czynny, zaplacona w MNIEJ NIZ POLOWIE (435-L2-p: przebudowa, ktora placi, tylko wolno, nie jest porzucana -
        /// konczy ja dopiero dotychczasowa regula "rok po terminie"), zaleglosc >= max(91, prog) w skali przejscia.</summary>
        internal static bool Abandon(double lag, double cost, double paid)
        {
            int n = StallDays;
            return n > 0 && cost > 0 && paid < 0.5 * cost && lag >= Math.Max(AbandonDays, n);
        }

        // 435-L2-p (recenzje 2, N2): hamulec petli - stojace przebudowy okregu ponad MaxStalled znow trzymaja miejsce; zwrot udzialu regionu najwyzej raz na sezon
        private static readonly int[] _capHeld = new int[2], _shareSmall = new int[2], _refBlocked = new int[2], _gFood = new int[2];
        private static readonly Dictionary<string, int>[] _refSeason = { new Dictionary<string, int>(StringComparer.Ordinal), new Dictionary<string, int>(StringComparer.Ordinal) };
        /// <summary>Okreg z wiecej niz MaxStalled stojacymi przebudowami: miejsce "jedna czesc naraz" zajete (sam licznik).</summary>
        internal static void NoteCap(int sys) { if (sys >= 0 && sys <= 1) _capHeld[sys]++; }
        /// <summary>Zmiana wstrzymana udzialem regionu, choc region nic jeszcze nie przesunal: rece tej czesci sa wieksze niz caly udzial regionu na sezon (pomiar, recenzja 3).</summary>
        internal static void NoteShareSmall(int sys) { if (sys >= 0 && sys <= 1) _shareSmall[sys]++; }
        /// <summary>Zwrot udzialu regionu wolno wziac najwyzej raz na sezon (pamiec sesji).</summary>
        internal static bool RefundFree(int sys, string region, int season)
        {
            if (sys < 0 || sys > 1) return false;
            int s;
            if (_refSeason[sys].TryGetValue(region ?? "-", out s) && s == season) { _refBlocked[sys]++; return false; }
            return true;
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
        internal static void NoteStartRefund(int sys, double hands, string region, int season)
        {
            if (sys < 0 || sys > 1) return;
            _startRefund[sys]++; _refundUsed[sys] += hands; _refSeason[sys][region ?? "-"] = season;
        }
        /// <summary>Rece oddane regionom przez stojace przebudowy w dobie kroku reguly (stan kroku).</summary>
        internal static void NoteRefund(int sys, double hands) { if (sys >= 0 && sys <= 1) _refundHands[sys] += hands; }

        // ============================================================ 2. bezpiecznik zboza
        /// <summary>
        /// Zmiana Z zboza zablokowana? 0 - nie (albo klucz wylaczony), 1 - zima krainy przy zapasie okregu ponizej progu zimy, 2 - gospodarze gloduja,
        /// 3 - zapas okregu ponizej progu przednowka. 435-L2-p (recenzje 4, 5, S1, S2): miara to ZAPAS spichlerza okregu w dniach potrzeby ludzi w domu (migawka
        /// Famine: racje w spichlerzu / ludzie w domu) - plon spichlerza nie zalezy od rak zboza wiosek, wiec dawny rachunek "plon po odejsciu" liczyl zaleznosc, ktorej
        /// w ksiedze nie ma. Progi: Famine Ration Days (przednowek), w zimie krainy 2 x tyle. Przejscie na INNE JEDZENIE (`toFood`) zawsze wolne.
        /// `v` - wies gry okregu (null - pola mieszczan: bez spichlerza wsi, sama zima miasta `t`), `leave` - rece odchodzace od zboza. `force` - rachunek takze przy
        /// wylaczonym kluczu (435-L6-p: nacisk tylko z niedoboru). Watek glowny, nie rzuca.
        /// </summary>
        internal static int GrainLock(int sys, Village v, Town t, double leave, bool toFood, int today, bool force = false)
        {
            if (sys < 0 || sys > 1 || (!GrainKey && !force)) return 0;
            try
            {
                if (toFood) { _gFood[sys]++; return 0; }
                bool winter = false;
                if (WinterBite.WinterNow())
                {
                    float f = v != null ? WinterRealm.OfVillage(v) : (t != null ? WinterRealm.OfTown(t) : 0f);
                    winter = f > 0f;
                }
                if (v == null) { if (winter) { _gWinter[sys]++; _gHands[sys] += leave; return 1; } _gPass[sys]++; return 0; }
                double home, harvest, food, cap; float u; int day;
                if (!Famine.DayOf(v, out home, out harvest, out food, out cap, out u, out day) || day < today - 2 || !(home > 0))
                {
                    _gNoData[sys]++;
                    if (winter) { _gWinter[sys]++; _gHands[sys] += leave; return 1; }   // bez migawki spichlerza - sama zima (jak 435-L2)
                    return 0;
                }
                if (u > 0f) { _gHunger[sys]++; _gHands[sys] += leave; return 2; }
                double days = food / home;
                int ration = RationDays;
                if (winter && days < 2.0 * ration) { _gWinter[sys]++; _gHands[sys] += leave; return 1; }
                if (days < ration) { _gNeed[sys]++; _gHands[sys] += leave; return 3; }
                _gPass[sys]++;
                return 0;
            }
            catch (Exception e) { Stumble("GrainLock", e); return 0; }
        }

        /// <summary>435-L6-p (recenzja 4): okreg moze oddac rece pola na zamowienie materialu - gospodarze nie gloduja i spichlerz trzyma co najmniej prog przednowka
        /// (w zimie krainy 2 x tyle) - ta sama miara co bezpiecznik zboza. Bez migawki spichlerza: poza zima tak, w zimie nie. Sam odczyt, nie rzuca.</summary>
        internal static bool FieldHandsFree(Village v, int today)
        {
            try
            {
                if (v == null) return false;
                bool winter = WinterBite.WinterNow() && WinterRealm.OfVillage(v) > 0f;
                double home, harvest, food, cap; float u; int day;
                if (!Famine.DayOf(v, out home, out harvest, out food, out cap, out u, out day) || day < today - 2 || !(home > 0)) return !winter;
                if (u > 0f) return false;
                return food / home >= (winter ? 2.0 : 1.0) * RationDays;
            }
            catch (Exception e) { Stumble("FieldHandsFree", e); return false; }
        }

        /// <summary>Prog zapasu przednowka w dniach potrzeby (Famine Ration Days, 30-364).</summary>
        internal static int RationDays { get { var s = Settings.Current; return Math.Max(30, Math.Min(364, s != null ? s.FamineRationDays : 90)); } }

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
                    Log.Info(P + "start | postoj przebudowy (Rebuild Stall Release Days): " + (n > 0 ? n + " dob zaleglosci raty (skala przejscia " + RefDays + " dob; rata mniejsza od sztuki materialu przed terminem nie liczy sie) - zwalnia miejsce w okregu (najwyzej " + MaxStalled + " stojaca + 1 czynna) i nieuzyty udzial regionu (raz na sezon); porzucenie po " + Math.Max(AbandonDays, n) + " dobach zaleglosci tylko przy zaplacie ponizej polowy (dotad rok po terminie)" : "0 - WYLACZONE (jak dotad: rok po terminie)")
                             + (s != null && s.RebuildStallReleaseDays > 0 && s.RebuildStallReleaseDays < MinStallDays ? " [ustawione " + s.RebuildStallReleaseDays + " - liczone jak " + MinStallDays + "]" : "")
                             + " | bezpiecznik zboza (Grain Hands Winter Lock): " + (g ? "TAK - glod gospodarzy, zapas okregu ponizej " + RationDays + " dni potrzeby (w zimie krainy ponizej " + 2 * RationDays + "); przejscie na inne jedzenie wolne" : "WYLACZONY (jak dotad)")
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
                      .Append(", zaleglosc raty (doby w skali przejscia ").Append(RefDays).Append(") <14 / 14-27 / 28-55 / 56-90 / >=91: ").Append(_hist[o]).Append(" / ").Append(_hist[o + 1]).Append(" / ").Append(_hist[o + 2]).Append(" / ").Append(_hist[o + 3]).Append(" / ").Append(_hist[o + 4])
                      .Append(", najwieksza ").Append(_maxLag[sys].ToString("0", Inv))
                      .Append(", stojace wedlug progu (zwolnione postojem) ").Append(_rel[sys]).Append(", zajmujace miejsce ").Append(_seen[sys] - _rel[sys])
                      .Append(" | od ostatniej linii: zaczete obok stojacej ").Append(_startPast[sys])
                      .Append(", zaczete dzieki zwrotowi udzialu regionu ").Append(_startRefund[sys]).Append(" (").Append(_refundUsed[sys].ToString("0", Inv)).Append(" rak)")
                      .Append(", rece oddane regionom w krokach reguly ").Append(_refundHands[sys].ToString("0", Inv))
                      .Append(", porzucone po ").Append(Math.Max(AbandonDays, n)).Append(" dobach zaleglosci (zaplata ponizej polowy) ").Append(_abandon[sys])
                      .Append(", okregow z miejscem zajetym przez nadmiar stojacych (ponad ").Append(MaxStalled).Append(") ").Append(_capHeld[sys])
                      .Append(", zwrot udzialu regionu juz wziety w tym sezonie ").Append(_refBlocked[sys])
                      .Append(", wstrzymane udzialem regionu, choc region nic nie przesunal (rece czesci > caly udzial regionu) ").Append(_shareSmall[sys])
                      .Append(", bezpiecznik zboza ").Append(_gWinter[sys] + _gHunger[sys] + _gNeed[sys]).Append(" (zima przy zapasie < ").Append(2 * RationDays).Append(" dni ").Append(_gWinter[sys]).Append(", glod ").Append(_gHunger[sys])
                      .Append(", zapas < ").Append(RationDays).Append(" dni ").Append(_gNeed[sys]).Append("; rak ").Append(_gHands[sys].ToString("0", Inv))
                      .Append("; przepuszczone ").Append(_gPass[sys]).Append(", na inne jedzenie (wolne) ").Append(_gFood[sys]).Append(", bez migawki spichlerza ").Append(_gNoData[sys]).Append(")");
                }
                sb.Append(" | tozsamosc licznikow (nie kontrola ksiag): przebudowy - rozklad zaleglosci = ").Append(ctl).Append("; zloto, ludzie, towar: regula niczego nie przenosi")
                  .Append(" | potkniecia ").Append(_st).Append(" (od wczytania ").Append(_stAll).Append(").");
                Log.Info(sb.ToString());
                Zero();
            }
            catch (Exception e) { Stumble("Daily", e); }
        }
    }
}
