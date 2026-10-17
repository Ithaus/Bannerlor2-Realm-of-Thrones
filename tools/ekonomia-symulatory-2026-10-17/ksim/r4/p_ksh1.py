import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from edlib import rep
P = r'C:/Users/GAME/AppData/Local/Temp/claude/C--Users-GAME-Bannerlor2-Realm-of-Thrones/ae222cb7-a7c2-473a-8fc1-1dc1d6f688e3/scratchpad/zb/n-ks/Armoury/src/KsFoodCarts.cs'
pairs = []
# constants
pairs.append(("""        private const double TauLand = 0.010, TauSea = 0.005, Margin = 0.03, SrcKeep = 1.25, OfferShare = 0.5, KxUp = 1.10, KxDown = 0.98, KxMax = 2.5, CofferShare = 0.15;
        private const int MaxSrc = 3, NCand = 12, Keep = 5, ReturnDays = 30, CallsPerDay = 200;""",
"""        // recenzja runda 3 (wazne): fracht ladem 2.5% wartosci na dobe, morzem 0.25% (dotad 1.0% / 0.5% - morze tylko 2 x taniej niz woz); te same stale co krok 5 Planu
        private const double TauLand = KsCore.CorrTauLand, TauSea = KsCore.CorrTauSea, Margin = 0.03, SrcKeep = 1.25, OfferShare = 0.5, KxUp = 1.10, KxDown = 0.98, KxMax = 2.5, CofferShare = 0.15;
        private const int MaxSrc = 3, NCand = 12, Keep = 5, ReturnDays = 30, CallsPerDay = 200;
        // recenzja runda 3 (wazne): kasa celu - zapas kupcow zostaje podloga; przy pustej polce zboza i soli najwyzej do polowy zapasu i najwyzej 5% zapasu na dobe ponad zwykly budzet
        private const double ReserveFloorShort = 0.5, ReserveDayShort = 0.05;
        // recenzja runda 3 (drobne): psucie w drodze - psujace sie traca 1.5% sztuk na dobe drogi (ujscie w ksiedze towarow)
        private const double SpoilPerDay = 0.015;
        private const int SampleCap = 256;                                  // probka wyceny: sztuka po sztuce najwyzej pierwsze 256 sztuk lotu (koszt)"""))
# fields: day counters
pairs.append(("""        private static double _dSampleErr, _dSampleMax; private static long _dTicks;""",
"""        private static double _dSampleErr, _dSampleMax; private static long _dTicks, _dSampleTicks;
        // recenzja runda 3: kupcy - wynik lotow przy przyjezdzie (wartosc u celu - zaplata - fracht), loty bez zysku pominiete, kasy miast z brakiem wobec zapasu, psucie
        private static long _dMarVal, _dMarCost, _dNoProfit, _dSpoil, _dRetLost; private static int _dMarLots, _dMarLoss;
        private static readonly List<double> _dMarPct = new List<double>(), _dCofShort = new List<double>();
        private static long _daysTicks; private static int _daysCalls;"""))
pairs.append(("""            Array.Clear(_dByZ, 0, NZ); _dSampleErr = _dSampleMax = 0; _dTicks = 0;
        }""",
"""            Array.Clear(_dByZ, 0, NZ); _dSampleErr = _dSampleMax = 0; _dTicks = 0; _dSampleTicks = 0;
            _dMarVal = _dMarCost = _dNoProfit = _dSpoil = _dRetLost = 0; _dMarLots = _dMarLoss = 0; _dMarPct.Clear(); _dCofShort.Clear();
        }

        /// <summary>Recenzja runda 3 (drobne): czas tabeli dob (MapDistanceModel) od ostatniego odczytu (ms) i liczba wywolan - linia doby KS, NKS-9b; zeruje.</summary>
        internal static double TakeDaysMs(out int calls)
        {
            calls = _daysCalls; double ms = _daysTicks * 1000.0 / Stopwatch.Frequency;
            _daysTicks = 0; _daysCalls = 0;
            return ms;
        }"""))
# DaysTick: ship speed + timer
pairs.append(("""            try
            {
                if (!EnsureTowns() || _pairsDone >= _pairs.Length) return;
                var dm = Campaign.Current != null ? Campaign.Current.Models.MapDistanceModel : null;
                if (dm == null) return;
                double per = Math.Max(1.0, MarketCarts.PerDay(Settings.Current));""",
"""            long t0 = Stopwatch.GetTimestamp();
            try
            {
                if (!EnsureTowns() || _pairsDone >= _pairs.Length) return;
                var dm = Campaign.Current != null ? Campaign.Current.Models.MapDistanceModel : null;
                if (dm == null) return;
                double per = Math.Max(1.0, MarketCarts.PerDay(Settings.Current));"""))
pairs.append(("""                    bool sea = !landOk || (allOk && dl > 1.15f * da);
                    float road = sea ? (allOk ? da : -1f) : dl;
                    float days = road >= 0f ? (float)Math.Max(1.0, Math.Ceiling(road / per)) : -1f;
                    _days[k] = days; _days[b * _nt + a] = days; _sea[k] = sea && days >= 0f; _sea[b * _nt + a] = _sea[k];
                    _pairsDone++;
                    _candDirty = true;
                }
            }
            catch (Exception e) { Stumble("DaysTick", e); }
        }""",
"""                    bool sea = !landOk || (allOk && dl > 1.15f * da);
                    float road = sea ? (allOk ? da : -1f) : dl;
                    // recenzja runda 3 (wazne): para morzem - statek (ShipSpeedK x tempo wozu) + doba portu; ladem - tempo wozu wsi
                    float days = road < 0f ? -1f : (sea ? (float)(Math.Max(1.0, Math.Ceiling(road / (per * KsCore.ShipSpeedK))) + KsCore.PortDays) : (float)Math.Max(1.0, Math.Ceiling(road / per)));
                    _days[k] = days; _days[b * _nt + a] = days; _sea[k] = sea && days >= 0f; _sea[b * _nt + a] = _sea[k];
                    _pairsDone++;
                    _candDirty = true;
                }
                _daysCalls += calls;
            }
            catch (Exception e) { Stumble("DaysTick", e); }
            finally { _daysTicks += Stopwatch.GetTimestamp() - t0; }
        }"""))
# Days table export/import: version prefix (ship days) - old tables recomputed
pairs.append(("""            var sb = new StringBuilder(_pairs.Length * 2 + 32);
            sb.Append(_tHash.ToString("X8")).Append(':').Append(_pairs.Length).Append(':');""",
"""            var sb = new StringBuilder(_pairs.Length * 2 + 32);
            sb.Append(DaysVer).Append(_tHash.ToString("X8")).Append(':').Append(_pairs.Length).Append(':');"""))
pairs.append(("""                var p = s.Split(':');
                uint h; int n;
                if (p.Length < 3 || !uint.TryParse(p[0], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out h) || h != _tHash || !int.TryParse(p[1], out n) || n != _pairs.Length || p[2].Length != 2 * n) return;""",
"""                // recenzja runda 3: tabela z dobami statku (prefiks "S"); stara tabela (doby wozu morzem) - liczona od nowa
                if (!s.StartsWith(DaysVer, StringComparison.Ordinal)) return;
                var p = s.Substring(DaysVer.Length).Split(':');
                uint h; int n;
                if (p.Length < 3 || !uint.TryParse(p[0], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out h) || h != _tHash || !int.TryParse(p[1], out n) || n != _pairs.Length || p[2].Length != 2 * n) return;"""))
pairs.append(("""        private const string B36 = "0123456789abcdefghijklmnopqrstuvwxyz";
        internal static string ExportDays()""", """        private const string B36 = "0123456789abcdefghijklmnopqrstuvwxyz";
        private const string DaysVer = "S";
        internal static string ExportDays()"""))
rep(P, pairs)
