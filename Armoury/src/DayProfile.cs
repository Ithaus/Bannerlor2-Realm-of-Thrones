using System;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// E8/0 (242, PLAN 8.17 pomiar; docs/etap8/PROJEKT-ETAP8-... rozdz. 3 "E8/0", rozpoznanie D rozdz. 5) - PROFIL DOBY W LOGU. Tylko log, gra bez zmian.
    /// Linia "Profil doby (E8/0): dzien N | ..." raz na dobe, na poczatku ArmouryBehavior.OnDailyTick - za okres od poprzedniego taktu doby (te same granice
    /// co "Koszt 171-174 (doba)", czytana PRZED Cost174.Daily, ktore zeruje swoje liczniki). Skladniki:
    ///  - takt doby: stoper calego OnDailyTick (jedna klatka) i odcinki miedzy kolejnymi Lap (kazde wywolanie X.Daily() - jeden odcinek, top 15 w linii);
    ///    inne sluchacze DailyTickEvent Armoury (LegendaryLaw, UniqueLaw, WorkshopTrade, ArtisanInputs) - Dx, pelny stoper;
    ///  - godzina: kazdy sluchacz HourlyTickEvent Armoury (Hr) - pelny stoper, suma, maks. pojedynczego wywolania i maks. jednej godziny (wszystkie sluchacze);
    ///  - zdarzenia: sluchacze SettlementEntered / MobilePartyDestroyed / MapEventEnded / DailyTickPartyEvent / DailyTickSettlementEvent z RegisterEvents
    ///    (Settle/Destroyed/MapEnd/PartyDay/TownDay) - probka 1/16 na sluchacza (jak Cost174: czas x wywolan/probek), liczba wywolan pelna;
    ///    rozbicie MenPurse.OnEntered lorda (SellArmorySurplus / AiWear.MendInTown / AiGear.TryBuy) - probka 1/16 wejsc lordow;
    ///  - podzialy: WorkshopLaw.CyclePrefix z planem doby warsztatu (BuildPlan/Candidates) i kolejne (na probkach Cost174), musztra wedlug wejscia (Drill),
    ///    ceny sprzetu wedlug wolajacego ([ThreadStatic] Caller: AiGear, wywoz, VolunteerKit, warsztaty; bez flagi - "nasze inne" w naszym sluchaczu
    ///    albo "gra i cudze") - na probkach Cost174 SPrice, przebudowy pamieci polki po wlasnym Take / reszta, czas zapisu logu, GC i pamiec,
    ///    najdluzsza pojedyncza probka (pauza sprzatania pamieci x16 wobec prawdziwego kosztu - RD8 1).
    /// "Nasze razem (dolna granica)" - tylko pozycje rozlaczne: takt + inne sluchacze doby + godzina + zdarzenia + to, co gra wola w naszym kodzie poza naszymi
    /// sluchaczami (CyclePrefix, VolunteerKit, TownFletchers z Cost174, trening musztry, ceny "gra i cudze"). Pokrycie wobec Koszt 171-174 (nasze 171-174b + ceny)
    /// - prog E80-2 >= 80%. Koszt samego profilu: linia (stoper) + stopery i opakowania (kalibracja raz na sesje) - prog E80-3 <= 1 ms/dobe.
    /// Opakowania zdarzen i godzin zakladane przy RegisterEvents tylko przy wlaczonym Stage8ProfileLog (wylaczony = te same delegaty co dotad);
    /// wylaczenie w trakcie gry gasi linie i stopery taktu od razu, opakowania - przy nastepnym wczytaniu. Wolane z watku gry (liczniki bez blokad - to log).
    /// </summary>
    internal static class DayProfile
    {
        internal static bool On { get { var s = Settings.Current; return s != null && s.Stage8ProfileLog; } }

        // ---- wolajacy cen (ThreadStatic - zgodnie z projektem; czytany tylko przy probce Cost174 SPrice)
        internal const int CNone = 0, CAiGear = 1, CExport = 2, CVolunteer = 3, CWorkshop = 4;
        [ThreadStatic] internal static int Caller;
        /// <summary>Glebokosc naszych sluchaczy (takt, godzina, zdarzenia) - cena bez flagi w naszym sluchaczu to "nasze inne", poza nim "gra i cudze".</summary>
        internal static int Depth;
        private static readonly string[] PriceNames = { "AiGear", "wywoz", "VolunteerKit", "warsztaty", "nasze inne", "gra i cudze" };
        private static readonly long[] _pTicks = new long[6], _pSampled = new long[6];

        private static double Ms(long ticks) { return ticks * 1000.0 / Stopwatch.Frequency; }

        // ---- takt doby
        private const int LMax = 200, XMax = 8;
        private static readonly string[] _lName = new string[LMax];
        private static readonly long[] _lTicks = new long[LMax];
        private static int _lN, _lapCount;
        private static bool _inTick, _hadTick;
        private static long _tickT0, _lapT, _tickTicks;
        private static int _lastDay = -1;
        private static readonly string[] _xName = new string[XMax];
        private static readonly long[] _xTicks = new long[XMax], _xCalls = new long[XMax];
        private static int _xN;

        // ---- godzina
        private const int HMax = 24;
        private static readonly string[] _hName = new string[HMax];
        private static readonly long[] _hTicks = new long[HMax], _hCalls = new long[HMax], _hMax = new long[HMax];
        private static int _hN, _hourKey = int.MinValue;
        private static long _hourAcc, _hourMaxAcc;

        // ---- zdarzenia (probka 1/16)
        internal const int KSettle = 0, KDestroyed = 1, KMapEnd = 2, KPartyDay = 3, KTownDay = 4, Kinds = 5;
        private static readonly string[] KindNames = { "wejscia do osad", "partie zniszczone", "koniec bitew", "doba partii", "doba osady" };
        private const int EMax = 48;
        private static readonly string[] _eName = new string[EMax];
        private static readonly int[] _eKind = new int[EMax];
        private static readonly long[] _eCalls = new long[EMax], _eTicks = new long[EMax], _eSampled = new long[EMax], _eMax = new long[EMax];
        private static int _eN;

        // ---- MenPurse.OnEntered lorda: SellArmorySurplus / AiWear.MendInTown / AiGear.TryBuy (probka 1/16 wejsc lordow)
        private static long _mpCalls, _mpSampled;
        private static readonly long[] _mp = new long[3];

        // ---- WorkshopLaw.CyclePrefix: 0 z planem doby (BuildPlan), 1 kolejne (czas na probkach Cost174, wywolan pelna liczba)
        private static readonly long[] _cCalls = new long[2], _cTicks = new long[2], _cSampled = new long[2];

        // ---- GC, koszt profilu, kalibracja opakowan
        private static readonly int[] _gc = new int[3];
        private static bool _gcSet;
        private static long _selfTicks, _tsCount;
        private static double _nsWrap = -1.0, _nsTs = -1.0;
        private static long _calCalls, _calSink;
        private static readonly long[] _drill = new long[8];
        private static readonly int[] _ord = new int[LMax + XMax + EMax];

        /// <summary>Nowa gra / wczytanie (konstruktor ArmouryBehavior): liczniki i stan okresu od zera; nazwy slotow zostaja (opakowania z RegisterEvents).</summary>
        internal static void Reset()
        {
            ClearPeriod();
            _inTick = false; _hadTick = false; _lastDay = -1; Depth = 0; _gcSet = false; _selfTicks = 0; _nsWrap = -1.0; _nsTs = -1.0;
            _hourKey = int.MinValue; _hourAcc = 0;
        }

        private static void ClearPeriod()
        {
            Array.Clear(_lTicks, 0, LMax); _lN = 0; _lapCount = 0; _tickTicks = 0;
            Array.Clear(_xTicks, 0, XMax); Array.Clear(_xCalls, 0, XMax);
            Array.Clear(_hTicks, 0, HMax); Array.Clear(_hCalls, 0, HMax); Array.Clear(_hMax, 0, HMax); _hourMaxAcc = 0;
            Array.Clear(_eCalls, 0, EMax); Array.Clear(_eTicks, 0, EMax); Array.Clear(_eSampled, 0, EMax); Array.Clear(_eMax, 0, EMax);
            _mpCalls = _mpSampled = 0; Array.Clear(_mp, 0, 3);
            Array.Clear(_cCalls, 0, 2); Array.Clear(_cTicks, 0, 2); Array.Clear(_cSampled, 0, 2);
            Array.Clear(_pTicks, 0, 6); Array.Clear(_pSampled, 0, 6);
            _tsCount = 0;
        }

        // ------------------------------------------------------------ takt doby
        /// <summary>Poczatek ArmouryBehavior.OnDailyTick: linia za poprzedni okres (przed Cost174.Daily), zerowanie, stoper taktu.</summary>
        internal static void TickBegin()
        {
            long t0 = Stopwatch.GetTimestamp();
            _inTick = false;
            if (!On) { _hadTick = false; return; }
            try
            {
                if (_hadTick) Line();
                else
                {
                    // pierwszy takt sesji (albo po wlaczeniu): okres niepelny - bez linii; liczniki musztry, zapisu logu i GC od teraz
                    Drill.ProfileTake(_drill); long wt, wl; Log.TakeWrite(out wt, out wl);
                    _gc[0] = GC.CollectionCount(0); _gc[1] = GC.CollectionCount(1); _gc[2] = GC.CollectionCount(2); _gcSet = true;
                }
            }
            catch (Exception e) { Log.Error("DayProfile.Line", e); }
            ClearPeriod();
            _hadTick = true;
            try { _lastDay = (int)CampaignTime.Now.ToDays; } catch { }
            long now = Stopwatch.GetTimestamp();
            _selfTicks = now - t0;           // koszt linii i zerowania - do nastepnej linii (ta jest juz wypisana)
            _inTick = true; Depth++;
            _tickT0 = _lapT = now;
        }

        /// <summary>Koniec odcinka taktu doby: czas od poprzedniego Lap (albo od poczatku taktu) pod ta nazwa.</summary>
        internal static void Lap(string name)
        {
            if (!_inTick) return;
            long now = Stopwatch.GetTimestamp();
            _lapCount++;
            if (_lN < LMax) { _lName[_lN] = name; _lTicks[_lN] = now - _lapT; _lN++; }
            _lapT = now;
        }

        internal static void TickEnd()
        {
            if (!_inTick) return;
            Lap("reszta taktu");
            _tickTicks = Stopwatch.GetTimestamp() - _tickT0;
            _inTick = false;
            if (Depth > 0) Depth--;
        }

        private static int Slot(string[] names, ref int n, int max, string name)
        {
            for (int i = 0; i < n; i++) if (names[i] == name) return i;
            if (n >= max) return -1;
            names[n] = name;
            return n++;
        }

        /// <summary>Inny sluchacz DailyTickEvent Armoury (poza OnDailyTick) - pelny stoper; wylaczony przy rejestracji = ten sam delegat.</summary>
        internal static Action Dx(string name, Action a)
        {
            if (a == null || !On) return a;
            int s = Slot(_xName, ref _xN, XMax, name);
            if (s < 0) return a;
            return () =>
            {
                long t = Stopwatch.GetTimestamp(); Depth++;
                try { a(); }
                finally { Depth--; _xTicks[s] += Stopwatch.GetTimestamp() - t; _xCalls[s]++; _tsCount += 2; }
            };
        }

        // ------------------------------------------------------------ godzina
        internal static Action Hr(string name, Action a)
        {
            if (a == null || !On) return a;
            int s = Slot(_hName, ref _hN, HMax, name);
            if (s < 0) return a;
            return () =>
            {
                long t = Stopwatch.GetTimestamp(); Depth++;
                try { a(); }
                finally
                {
                    Depth--;
                    long d = Stopwatch.GetTimestamp() - t;
                    _hTicks[s] += d; _hCalls[s]++; if (d > _hMax[s]) _hMax[s] = d; _tsCount += 2;
                    int hk = _hourKey;
                    try { hk = (int)CampaignTime.Now.ToHours; } catch { }
                    if (hk != _hourKey) { _hourKey = hk; _hourAcc = 0; }
                    _hourAcc += d;
                    if (_hourAcc > _hourMaxAcc) _hourMaxAcc = _hourAcc;
                }
            };
        }

        // ------------------------------------------------------------ zdarzenia (probka 1/16 na sluchacza)
        private static int ESlot(int kind, string name)
        {
            for (int i = 0; i < _eN; i++) if (_eKind[i] == kind && _eName[i] == name) return i;
            if (_eN >= EMax) return -1;
            _eName[_eN] = name; _eKind[_eN] = kind;
            return _eN++;
        }

        private static long EBegin(int s) { return (++_eCalls[s] & 15) == 1 ? Stopwatch.GetTimestamp() : 0L; }

        private static void EEnd(int s, long t)
        {
            if (t == 0) return;
            long d = Stopwatch.GetTimestamp() - t;
            _eTicks[s] += d; _eSampled[s]++; if (d > _eMax[s]) _eMax[s] = d;   // odczyty zegara probki - w koszcie opakowania (kalibracja)
        }

        internal static Action<MobileParty, Settlement, Hero> Settle(string name, Action<MobileParty, Settlement, Hero> a)
        {
            if (a == null || !On) return a;
            int s = ESlot(KSettle, name);
            if (s < 0) return a;
            return (mp, st, h) => { long t = EBegin(s); Depth++; try { a(mp, st, h); } finally { Depth--; EEnd(s, t); } };
        }

        internal static Action<MobileParty, PartyBase> Destroyed(string name, Action<MobileParty, PartyBase> a)
        {
            if (a == null || !On) return a;
            int s = ESlot(KDestroyed, name);
            if (s < 0) return a;
            return (mp, pb) => { long t = EBegin(s); Depth++; try { a(mp, pb); } finally { Depth--; EEnd(s, t); } };
        }

        internal static Action<MapEvent> MapEnd(string name, Action<MapEvent> a)
        {
            if (a == null || !On) return a;
            int s = ESlot(KMapEnd, name);
            if (s < 0) return a;
            return me => { long t = EBegin(s); Depth++; try { a(me); } finally { Depth--; EEnd(s, t); } };
        }

        internal static Action<MobileParty> PartyDay(string name, Action<MobileParty> a)
        {
            if (a == null || !On) return a;
            int s = ESlot(KPartyDay, name);
            if (s < 0) return a;
            return mp => { long t = EBegin(s); Depth++; try { a(mp); } finally { Depth--; EEnd(s, t); } };
        }

        internal static Action<Settlement> TownDay(string name, Action<Settlement> a)
        {
            if (a == null || !On) return a;
            int s = ESlot(KTownDay, name);
            if (s < 0) return a;
            return st => { long t = EBegin(s); Depth++; try { a(st); } finally { Depth--; EEnd(s, t); } };
        }

        // ------------------------------------------------------------ podzialy
        /// <summary>MenPurse.OnEntered lorda: 0 = bez probki, inaczej znacznik czasu poczatku (SellArmorySurplus).</summary>
        internal static long MpBegin() { return (++_mpCalls & 15) == 1 ? Stopwatch.GetTimestamp() : 0L; }

        internal static long Now(long t0) { return t0 != 0 ? Stopwatch.GetTimestamp() : 0L; }

        /// <summary>t0 - przed SellArmorySurplus, t1 - przed AiWear.MendInTown, t2 - przed AiGear.TryBuy; koniec teraz.</summary>
        internal static void MpEnd(long t0, long t1, long t2)
        {
            if (t0 == 0 || t1 == 0 || t2 == 0) return;
            long t3 = Stopwatch.GetTimestamp();
            _mp[0] += t1 - t0; _mp[1] += t2 - t1; _mp[2] += t3 - t2; _mpSampled++; _tsCount += 4;
        }

        /// <summary>WorkshopLaw.CyclePrefix (finally): plan - w tym wywolaniu zbudowano plan doby warsztatu; dt - probka Cost174 (0 = bez probki).</summary>
        internal static void Cycle(bool plan, long dt)
        {
            int k = plan ? 0 : 1;
            _cCalls[k]++;
            if (dt > 0) { _cTicks[k] += dt; _cSampled[k]++; }
        }

        /// <summary>SupplyDemand.PricePostfix (finally) przy probce Cost174 SPrice: czas probki do wolajacego.</summary>
        internal static void PriceSample(long dt)
        {
            if (dt <= 0) return;
            int c = Caller;
            int k = c >= CAiGear && c <= CWorkshop ? c - 1 : (Depth > 0 ? 4 : 5);
            _pTicks[k] += dt; _pSampled[k]++;
        }

        // ------------------------------------------------------------ linia
        private static double EMs(int s) { return _eSampled[s] > 0 ? Ms(_eTicks[s]) * ((double)_eCalls[s] / _eSampled[s]) : 0.0; }

        private static string F1(double v) { return v.ToString("0.0", CultureInfo.InvariantCulture); }
        private static string F2(double v) { return v.ToString("0.00", CultureInfo.InvariantCulture); }

        private static void Calibrate()
        {
            // koszt jednego opakowania zdarzenia (licznik, maska, glebokosc, try/finally, probka 1/16) i jednego odczytu zegara - raz na sesje
            const int N = 4096;
            Action noop = () => { };
            long t0 = Stopwatch.GetTimestamp();
            for (int i = 0; i < N; i++)
            {
                long t = (++_calCalls & 15) == 1 ? Stopwatch.GetTimestamp() : 0L; Depth++;
                try { noop(); }
                finally { Depth--; if (t != 0) _calSink += Stopwatch.GetTimestamp() - t; }
            }
            long t1 = Stopwatch.GetTimestamp();
            long sink = 0;
            for (int i = 0; i < N; i++) sink += Stopwatch.GetTimestamp();
            long t2 = Stopwatch.GetTimestamp();
            _nsWrap = (t1 - t0) * 1e9 / Stopwatch.Frequency / N;
            _nsTs = (t2 - t1) * 1e9 / Stopwatch.Frequency / N + (sink == 1 ? 1e-9 : 0.0);
        }

        private static void Line()
        {
            long t0 = Stopwatch.GetTimestamp();
            if (_nsWrap < 0) Calibrate();
            var inv = CultureInfo.InvariantCulture;
            var sb = new StringBuilder(2048);
            sb.Append("Profil doby (E8/0): dzien ").Append(_lastDay);

            // takt doby (jedna klatka) i inne sluchacze doby
            double tick = Ms(_tickTicks), extra = 0;
            for (int i = 0; i < _xN; i++) extra += Ms(_xTicks[i]);
            sb.Append(" | takt doby ").Append(F1(tick)).Append(" ms (wywolan ").Append(_lapCount).Append("; top: ");
            int n = 0;
            for (int i = 0; i < _lN; i++) _ord[n++] = i;
            Array.Sort(_ord, 0, n, new ByTicks(_lTicks));
            for (int i = 0; i < n && i < 15; i++) { if (i > 0) sb.Append(", "); sb.Append(_lName[_ord[i]]).Append(' ').Append(F1(Ms(_lTicks[_ord[i]]))); }
            sb.Append("); inne sluchacze doby ").Append(F1(extra)).Append(" ms (");
            for (int i = 0; i < _xN; i++) { if (i > 0) sb.Append(", "); sb.Append(_xName[i]).Append(' ').Append(F1(Ms(_xTicks[i]))); }
            sb.Append(')');

            // godzina
            double hour = 0;
            for (int i = 0; i < _hN; i++) hour += Ms(_hTicks[i]);
            sb.Append(" | godzina: suma ").Append(F1(hour)).Append(" ms, maks. godziny ").Append(F1(Ms(_hourMaxAcc))).Append(" ms (top suma/maks.: ");
            n = 0;
            for (int i = 0; i < _hN; i++) _ord[n++] = i;
            Array.Sort(_ord, 0, n, new ByTicks(_hTicks));
            for (int i = 0; i < n && i < 6; i++) { if (i > 0) sb.Append(", "); int s = _ord[i]; sb.Append(_hName[s]).Append(' ').Append(F1(Ms(_hTicks[s]))).Append('/').Append(F1(Ms(_hMax[s]))); }
            sb.Append(')');

            // zdarzenia
            double ev = 0;
            sb.Append(" | zdarzenia:");
            var kindMs = new double[Kinds];
            var kindN = new long[Kinds];
            for (int i = 0; i < _eN; i++) { double m = EMs(i); kindMs[_eKind[i]] += m; ev += m; if (_eCalls[i] > kindN[_eKind[i]]) kindN[_eKind[i]] = _eCalls[i]; }
            var est = new long[EMax];
            for (int i = 0; i < _eN; i++) est[i] = (long)(EMs(i) * 1000.0);
            for (int k = 0; k < Kinds; k++)
            {
                sb.Append(k == 0 ? " " : "; ").Append(KindNames[k]).Append(' ').Append(F1(kindMs[k])).Append(" ms/").Append(kindN[k]).Append(" (");
                n = 0;
                for (int i = 0; i < _eN; i++) if (_eKind[i] == k) _ord[n++] = i;
                Array.Sort(_ord, 0, n, new ByTicks(est));
                for (int i = 0; i < n && i < 4; i++)
                {
                    if (i > 0) sb.Append(", ");
                    int s = _ord[i];
                    sb.Append(_eName[s]).Append(' ').Append(F1(EMs(s)));
                    if (k == KSettle && _eName[s] == "MenPurse.OnEntered" && _mpSampled > 0)
                    {
                        double sc = (double)_mpCalls / _mpSampled;
                        sb.Append(" [lordowie ").Append(_mpCalls).Append(": SellArmorySurplus ").Append(F1(Ms(_mp[0]) * sc)).Append(" / AiWear.MendInTown ").Append(F1(Ms(_mp[1]) * sc))
                          .Append(" / AiGear.TryBuy ").Append(F1(Ms(_mp[2]) * sc)).Append(']');
                    }
                }
                sb.Append(')');
            }

            // CyclePrefix
            double cyc0 = _cSampled[0] > 0 ? Ms(_cTicks[0]) * ((double)_cCalls[0] / _cSampled[0]) : 0.0;
            double cyc1 = _cSampled[1] > 0 ? Ms(_cTicks[1]) * ((double)_cCalls[1] / _cSampled[1]) : 0.0;
            sb.Append(" | CyclePrefix z planem doby ").Append(F1(cyc0)).Append(" ms/").Append(_cCalls[0]).Append(" (probek ").Append(_cSampled[0])
              .Append("), kolejne ").Append(F1(cyc1)).Append(" ms/").Append(_cCalls[1]).Append(" (probek ").Append(_cSampled[1]).Append(')');

            // musztra (Drill): 0 godziny, 1 tik treningu (z zuzyciem i kowalami w tiku), 2 kowale przy wejsciu, 3 linie, 4 zniszczone, 5 ekrany; 6 zuzycie, 7 kowale w tiku
            Drill.ProfileTake(_drill);
            double dTrain = Ms(_drill[1]), dWear = Ms(_drill[6]), dScrap = Ms(_drill[7]);
            sb.Append(" | musztra: godziny ").Append(F1(Ms(_drill[0]))).Append(", trening ").Append(F1(dTrain)).Append(" (w tym zuzycie ").Append(F1(dWear))
              .Append(", kowale w tiku ").Append(F1(dScrap)).Append("), kowale przy wejsciu ").Append(F1(Ms(_drill[2]))).Append(", linie ").Append(F1(Ms(_drill[3])))
              .Append(", inne ").Append(F1(Ms(_drill[4]) + Ms(_drill[5]))).Append(" ms");

            // ceny wedlug wolajacego (probki Cost174 SPrice x wywolan/probek tej pozycji)
            double pr = Cost174.Ratio(Cost174.SPrice);
            long pS = 0; for (int k = 0; k < 6; k++) pS += _pSampled[k];
            sb.Append(" | ceny wedlug wolajacego: ");
            for (int k = 0; k < 6; k++) { if (k > 0) sb.Append(", "); sb.Append(PriceNames[k]).Append(' ').Append(F1(Ms(_pTicks[k]) * pr)); }
            sb.Append(" ms (probek ").Append(pS).Append(')');
            double priceGame = Ms(_pTicks[5]) * pr;

            // polka, log, GC
            sb.Append(" | polka przebudowy wlasne ").Append(ShelfIndex.BuildsOwn).Append(" / reszta ").Append(ShelfIndex.BuildsRest);
            long wt, wl; Log.TakeWrite(out wt, out wl);
            sb.Append(" | log zapis ").Append(F1(Ms(wt))).Append(" ms / ").Append(wl).Append(" linii");
            int g0 = GC.CollectionCount(0), g1 = GC.CollectionCount(1), g2 = GC.CollectionCount(2);
            if (!_gcSet) { _gc[0] = g0; _gc[1] = g1; _gc[2] = g2; _gcSet = true; }
            sb.Append(" | GC 0/1/2 +").Append(g0 - _gc[0]).Append("/+").Append(g1 - _gc[1]).Append("/+").Append(g2 - _gc[2])
              .Append(", pamiec ").Append((GC.GetTotalMemory(false) / 1048576L).ToString(inv)).Append(" MB");
            _gc[0] = g0; _gc[1] = g1; _gc[2] = g2;

            // najdluzsza pojedyncza probka (Cost174, zdarzenia, godzina)
            sb.Append(" | najdluzsza probka: ").Append(Cost174.MaxText());
            int be = -1; for (int i = 0; i < _eN; i++) if (be < 0 || _eMax[i] > _eMax[be]) be = i;
            if (be >= 0 && _eMax[be] > 0) sb.Append("; zdarzenie ").Append(_eName[be]).Append(' ').Append(F2(Ms(_eMax[be]))).Append(" ms");
            int bh = -1; for (int i = 0; i < _hN; i++) if (bh < 0 || _hMax[i] > _hMax[bh]) bh = i;
            if (bh >= 0 && _hMax[bh] > 0) sb.Append("; godzina ").Append(_hName[bh]).Append(' ').Append(F2(Ms(_hMax[bh]))).Append(" ms");

            // nasze razem (rozlaczne) i pokrycie wobec Koszt 171-174
            double outside = Cost174.MsOf(Cost174.SCycle) + Cost174.MsOf(Cost174.SVolunteer) + Cost174.MsOf(Cost174.SFletch) + dTrain + priceGame;   // ekrany musztry (trofea - czesc w koncu bitwy) poza suma
            double ours = tick + extra + hour + ev + outside;
            double refMs = Cost174.SumMs() + Cost174.MsOf(Cost174.SPrice);
            sb.Append(" | nasze razem (dolna granica) ").Append(F1(ours)).Append(" ms = takt ").Append(F1(tick)).Append(" + inne sluchacze doby ").Append(F1(extra))
              .Append(" + godzina ").Append(F1(hour)).Append(" + zdarzenia ").Append(F1(ev)).Append(" + gra wola nasze (CyclePrefix, VolunteerKit, TownFletchers, trening musztry, ceny gra i cudze) ")
              .Append(F1(outside)).Append("; pokrycie wobec Koszt 171-174 (nasze 171-174b + ceny ").Append(F1(refMs)).Append(" ms) ")
              .Append(refMs > 0 ? (100.0 * ours / refMs).ToString("0", inv) + "%" : "-");

            // koszt profilu: poprzednia linia (stoper) + ta linia + stopery i opakowania (kalibracja)
            long wraps = 0; for (int i = 0; i < _eN; i++) wraps += _eCalls[i];
            long reads = _tsCount + _lapCount + 2;   // godzina, inne sluchacze doby, MenPurse (licznik w opakowaniach) + odcinki i poczatek taktu
            double stoppers = (wraps * _nsWrap + reads * _nsTs) / 1e6;
            double lineMs = Ms(_selfTicks + (Stopwatch.GetTimestamp() - t0));
            sb.Append(" | koszt profilu ").Append(F2(lineMs + stoppers)).Append(" ms (linia ").Append(F2(lineMs)).Append(", stopery i opakowania ~").Append(F2(stoppers))
              .Append(": opakowan ").Append(wraps).Append(" x ").Append(_nsWrap.ToString("0.0", inv)).Append(" ns, odczytow zegara ").Append(reads)
              .Append(" x ").Append(_nsTs.ToString("0.0", inv)).Append(" ns)");
            Log.Info(sb.ToString());
        }

        private sealed class ByTicks : System.Collections.Generic.IComparer<int>
        {
            private readonly long[] _t;
            public ByTicks(long[] t) { _t = t; }
            public int Compare(int a, int b) { int c = _t[b].CompareTo(_t[a]); return c != 0 ? c : a.CompareTo(b); }
        }
    }
}
