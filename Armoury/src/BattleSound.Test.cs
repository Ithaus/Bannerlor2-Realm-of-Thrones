using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using IOPath = System.IO.Path;

namespace Armoury
{
    /// <summary>
    /// noc 25.10 P2 TEST (tylko autotest: LogGate 1/2 + plik Documents/Mount and Blade II Bannerlord/CrashScribe/armoury-dzwiek-bitwy.flag; wzor EventMusic
    /// armoury-muzyka-bitwy.flag): raz na proces, w pierwszej bitwie z dzwiekiem, 2 s po rozstawieniu - po jednym klipie kazdego kodu katalogu (sp, mo, or, hr, dr,
    /// br, bd, ob, jk, ko, pt, wi, rn, fi, sz, cr...) co 1,5 s, 3D 6 m przed sluchaczem (tlo 2D), linia na klip: "probka &lt;id&gt; (kod, dlugosc): grane po 0,5 s
    /// tak/nie, koniec po X s (z dlugosci Y)". Nic nie czeka na gracza - autotest nie stanie. W grze Jeffa (LogGate 0) nic.
    /// </summary>
    internal static partial class BattleSound
    {
        internal const string TestFlagName = "armoury-dzwiek-bitwy.flag";
        private static bool _testDoneProcess, _testOn;
        private static readonly List<Clip> _testQ = new List<Clip>();
        private static float _testNext;
        private static int _cTest, _cTestPlayed;

        private static bool TestWanted()
        {
            int mode = LogGate.Mode;
            if (mode != 1 && mode != 2) return false;
            try
            {
                var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                return File.Exists(IOPath.Combine(IOPath.Combine(IOPath.Combine(docs, "Mount and Blade II Bannerlord"), "CrashScribe"), TestFlagName));
            }
            catch { return false; }
        }

        private static void ResetTest()
        {
            _testOn = false; _testQ.Clear(); _testNext = -1f; _cTest = _cTestPlayed = 0;
        }

        private static void TestBegin(Mission m)
        {
            if (_testDoneProcess || !TestWanted()) return;
            _testDoneProcess = true;
            _testOn = true;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var c in _clips) if (seen.Add(c.Code)) _testQ.Add(c);
            Log.Info(P + "probka autotestu - " + _testQ.Count + " klipow (po jednym z kodu), start 2 s po rozstawieniu.");
        }

        private static void TestTick(Mission m)
        {
            if (!_testOn || _testQ.Count == 0) return;
            if (!m.IsDeploymentFinished) return;
            if (_testNext < 0f) { _testNext = _now + 2f; return; }
            if (_now < _testNext) return;
            _testNext = _now + 1.5f;
            var c = _testQ[0];
            _testQ.RemoveAt(0);
            bool flat = c.Code == "wi" || c.Code == "rn";
            Vec3 pos = _lis + new Vec3(6f, 0f, 0f);
            var s = Play(m, c, GAmb, pos, 5f, 200f, !flat, null, null, "probka");
            _cTest++;
            if (s == null) { Log.Info(P + "probka " + c.Id + " (" + c.Code + ", " + F1(c.Dur) + " s): NIE utworzone (limit / silnik - licznik linii misji)."); return; }
            s.Test = true;
        }

        private static void TestChecked(Snd s, bool playing)
        {
            if (playing) _cTestPlayed++;
            Log.Info(P + "probka " + s.C.Id + " (" + s.C.Code + ", " + (s.Is3d ? "3D" : "2D") + "): grane po 0,5 s " + (playing ? "tak" : "NIE") + ".");
        }

        private static void TestEnded(Snd s, float t)
        {
            Log.Info(P + "probka " + s.C.Id + " - koniec po " + F1(t) + " s (z dlugosci " + F1(s.C.Dur) + " s" + (t > s.C.Dur + 1.5f ? ", silnik nie zglosil konca - limit czasu" : "") + ").");
        }

        private static void AppendTest(StringBuilder sb)
        {
            if (_cTest > 0) sb.Append(" | probka testu ").Append(_cTest).Append(" (grane po 0,5 s ").Append(_cTestPlayed).Append(')');
        }
    }
}
