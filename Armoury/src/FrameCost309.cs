using System;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// 309 (soczewka wydajnosci, Jeff 31.10 "gra spojna" - bitwa 1500+ nie moze sie zacinac): wspolny pomiar kosztu klatki naszych zachowan w bitwie polowej.
    /// Dotad mierzyly sie tylko 4 moduly (AiBreath, RoutKeeper, 305c, pass 204/283) - reszta bez ms i bez budzetu, a takty co 0.2 / 0.25 / 0.5 / 1 s moga trafic
    /// w te sama klatke. Kazde OnMissionTick naszego zachowania (FieldCraft, RoutKeeper, BrokenMen, HorseWind, BannerCompanies, OwnFootAhead, BattleStall307e)
    /// i kazdy modul BattleSense (okrzyki, marsz, doktryna, dzwiek, tarcze, szyk, poscig 148/204/283, bohaterowie, kaluze, pojedynki) dodaje swoj czas (Stopwatch,
    /// raz na wywolanie - nie na agenta). Klatka = numer klatki silnika (TaleWorlds.Engine.Utilities.EngineFrameNo; wyjatek - czas misji). Na koniec misji jedna
    /// linia "Koszt klatki Armoury (309)": suma naszych na klatke (p50 / p99 z koszykow, max, klatki ponad 2 i 5 ms), realny odstep klatek (haczenia ponad 33 / 50 /
    /// 100 ms - cala klatka gry, nie tylko nasza), moduly (razem, najwiecej na wywolanie, wywolan) i zapisy zamachu FieldCraft (309). Tylko pomiar - zachowanie bez zmian.
    /// Glowny watek (OnMissionTick); poza bitwa polowa nic nie liczy (T() = 0).
    /// </summary>
    internal static class FrameCost309
    {
        internal const int MFieldCraft = 0, MCries = 1, MMarch = 2, MDoctrine = 3, MSound = 4, MShields = 5, MRanks = 6, MPursuit = 7, MHeroes = 8, MPools = 9,
                           MDuel = 10, MRout = 11, MBroken = 12, MHorseWind = 13, MBanner = 14, MOwnFoot = 15, MStall = 16, MClean = 17, MKinds = 18;
        private static readonly string[] Names = { "FieldCraft", "okrzyki", "marsz", "doktryna", "dzwiek", "tarcze 140", "szyk 146", "poscig 148/204/283",
                                                   "bohaterowie 149", "kaluze", "zrywy", "RoutKeeper", "BrokenMen", "HorseWind", "choragwie", "305c OwnFootAhead", "zastoj 307e",
                                                   "czysty strzal 310 (w tarcze 140)" };
        // koszyki sumy naszych na klatke (ms, gorna granica)
        private static readonly double[] Edges = { 0.05, 0.1, 0.25, 0.5, 1, 2, 3, 5, 8, 12, 20, double.MaxValue };
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private static bool _on, _said;
        private static string _kind = "-";
        private static readonly long[] Sum = new long[MKinds], Max = new long[MKinds];
        private static readonly int[] N = new int[MKinds];
        private static readonly int[] Hist = new int[12];
        private static int _frame = int.MinValue, _frames, _over2, _over5, _dt33, _dt50, _dt100, _stumbles;
        private static long _frameSum, _frameMax, _lastFrameTs, _dtSum, _dtMax, _startTs;
        private static float _lastMissionT = -1f, _startMissionT;
        private static int _dtN;

        /// <summary>Poczatek misji (BattleSense.AfterStart): liczniki od zera; pomiar tylko w bitwie polowej.</summary>
        internal static void Begin(Mission m, bool field, string kind)
        {
            _on = field && m != null;
            _said = false;
            _kind = kind ?? "-";
            Array.Clear(Sum, 0, Sum.Length); Array.Clear(Max, 0, Max.Length); Array.Clear(N, 0, N.Length); Array.Clear(Hist, 0, Hist.Length);
            _frame = int.MinValue; _frames = _over2 = _over5 = _dt33 = _dt50 = _dt100 = _stumbles = _dtN = 0;
            _frameSum = _frameMax = _lastFrameTs = _dtSum = _dtMax = 0;
            _lastMissionT = -1f;
            _startTs = Stopwatch.GetTimestamp();
            _startMissionT = 0f;
            try { if (m != null) _startMissionT = m.CurrentTime; } catch { }
        }

        /// <summary>Znacznik czasu do Add (0 = pomiar nieczynny).</summary>
        internal static long T() { return _on ? Stopwatch.GetTimestamp() : 0L; }

        internal static void Add(int mod, long t0)
        {
            if (!_on || t0 == 0L) return;
            try
            {
                long now = Stopwatch.GetTimestamp();
                long d = now - t0;
                if (d < 0) d = 0;
                Sum[mod] += d; N[mod]++;
                if (d > Max[mod]) Max[mod] = d;
                int fr = FrameNo();
                if (fr != _frame)
                {
                    if (_frame != int.MinValue) CloseFrame();
                    if (_lastFrameTs != 0L)
                    {
                        long dt = t0 - _lastFrameTs;
                        if (dt > 0)
                        {
                            _dtSum += dt; _dtN++;
                            if (dt > _dtMax) _dtMax = dt;
                            double ms = Ms(dt);
                            if (ms > 33.0) _dt33++;
                            if (ms > 50.0) _dt50++;
                            if (ms > 100.0) _dt100++;
                        }
                    }
                    _lastFrameTs = t0;
                    _frame = fr;
                    _frameSum = 0;
                }
                _frameSum += d;
            }
            catch { _stumbles++; }
        }

        /// <summary>310: czas modulu liczonego WEWNATRZ innego (czysty strzal w ShieldsUp.Tick) - tylko razem / najwiecej / wywolan, bez sumy klatki
        /// (ta juz jest w module zewnetrznym).</summary>
        internal static void AddTicks(int mod, long ticks)
        {
            if (!_on || ticks <= 0 || mod < 0 || mod >= MKinds) return;
            Sum[mod] += ticks; N[mod]++;
            if (ticks > Max[mod]) Max[mod] = ticks;
        }

        private static int FrameNo()
        {
            try { return TaleWorlds.Engine.Utilities.EngineFrameNo; }
            catch
            {
                // zapas: czas misji (wszystkie zachowania klatki widza ten sam CurrentTime)
                var m = Mission.Current;
                float t = m != null ? m.CurrentTime : 0f;
                if (t != _lastMissionT) { _lastMissionT = t; return _frame == int.MinValue ? 0 : _frame + 1; }
                return _frame;
            }
        }

        private static void CloseFrame()
        {
            _frames++;
            double ms = Ms(_frameSum);
            if (_frameSum > _frameMax) _frameMax = _frameSum;
            if (ms > 2.0) _over2++;
            if (ms > 5.0) _over5++;
            for (int i = 0; i < Edges.Length; i++)
                if (ms <= Edges[i]) { Hist[i]++; break; }
        }

        private static double Ms(long ticks) { return ticks * 1000.0 / Stopwatch.Frequency; }

        private static string Pct(double q)
        {
            if (_frames <= 0) return "-";
            int need = (int)Math.Ceiling(q * _frames), acc = 0;
            for (int i = 0; i < Hist.Length; i++)
            {
                acc += Hist[i];
                if (acc >= need) return i == Hist.Length - 1 ? "> 20 ms" : "<= " + Edges[i].ToString("0.##", Inv) + " ms";
            }
            return "-";
        }

        /// <summary>Koniec misji (BattleSense.Clean): linia raz na bitwe polowa.</summary>
        internal static void End(Mission m)
        {
            if (!_on || _said) { _on = false; return; }
            _said = true;
            _on = false;
            try
            {
                if (_frame != int.MinValue && _frameSum > 0) CloseFrame();
                float mt = 0f;
                try { if (m != null) mt = m.CurrentTime - _startMissionT; } catch { }
                double real = Ms(Stopwatch.GetTimestamp() - _startTs) / 1000.0;
                var c = Settings.Current;
                var sb = new StringBuilder(1600);
                sb.Append("Koszt klatki Armoury (309): misja ").Append(_kind).Append(", klatek z nasza praca ").Append(_frames)
                  .Append(" (czas misji ").Append(mt.ToString("0", Inv)).Append(" s, realny ").Append(real.ToString("0", Inv)).Append(" s)")
                  .Append(" | suma naszych na klatke: p50 ").Append(Pct(0.5)).Append(", p99 ").Append(Pct(0.99)).Append(", najwiecej ").Append(Ms(_frameMax).ToString("0.000", Inv))
                  .Append(" ms; klatek ponad 2 ms ").Append(_over2).Append(", ponad 5 ms ").Append(_over5)
                  .Append(" | odstep klatek realny (cala klatka gry): sr. ").Append(_dtN > 0 ? Ms(_dtSum / _dtN).ToString("0.0", Inv) : "-").Append(" ms, ponad 33 ms ").Append(_dt33)
                  .Append(", ponad 50 ms ").Append(_dt50).Append(", ponad 100 ms ").Append(_dt100).Append(", najdluzszy ").Append(Ms(_dtMax).ToString("0", Inv)).Append(" ms")
                  .Append(" | moduly (ms razem / najwiecej na wywolanie / wywolan):");
                for (int i = 0; i < MKinds; i++)
                {
                    sb.Append(i == 0 ? " " : ", ").Append(Names[i]).Append(' ').Append(Ms(Sum[i]).ToString("0.0", Inv)).Append(" / ").Append(Ms(Max[i]).ToString("0.000", Inv))
                      .Append(" / ").Append(N[i]);
                }
                sb.Append(" | zamach FieldCraft (309, klucz ").Append(c != null && c.FieldCraftSwingOnChange ? "TAK" : "NIE").Append("): zapisy do silnika ")
                  .Append(FieldCraft.SwingWrites).Append(", pominiete bez zmiany ").Append(FieldCraft.SwingSkips).Append(", ponowione po nadpisaniu ").Append(FieldCraft.SwingRewrites)
                  .Append(" | potkniecia pomiaru ").Append(_stumbles).Append('.');
                Log.Info(sb.ToString());
            }
            catch (Exception e) { RoutHold.Stumble(RoutHold.SiteVoice, "FrameCost309.End", e); }
        }
    }
}
