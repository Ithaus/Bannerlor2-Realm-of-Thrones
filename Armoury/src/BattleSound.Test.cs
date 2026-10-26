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
    /// 154: na koniec probka odleglosci - ten sam klip (rozkaz "or" na voice_shout, odleglosci rozkazu 4/200 m) raz ok. 5 m i raz ok. 150 m od sluchacza,
    /// z linia na kazdy ("probka &lt;id&gt; ... w 5 m / w 150 m ...") - do odsluchu przez Jeffa (silnik nie podaje glosnosci: w 150 m ma byc wyraznie ciszej).
    /// </summary>
    internal static partial class BattleSound
    {
        internal const string TestFlagName = "armoury-dzwiek-bitwy.flag";
        private static bool _testDoneProcess, _testOn;
        private static readonly List<Clip> _testQ = new List<Clip>();
        private static float _testNext;
        private static int _cTest, _cTestPlayed;
        // 154: probka odleglosci (ten sam klip w 5 m i 150 m)
        private static readonly float[] TestDistM = { 5f, 150f };
        private static Clip _testDistClip;
        private static int _testDistStep;
        private static double _testDistNextR;   // recenzja 154: odstep miedzy probkami odleglosci w czasie rzeczywistym (czas misji w autotescie biegnie ok. 10x szybciej)

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
            _testDistClip = null; _testDistStep = 0; _testDistNextR = 0.0;
        }

        private static void TestBegin(Mission m)
        {
            if (_testDoneProcess || !TestWanted()) return;
            _testDoneProcess = true;
            _testOn = true;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var c in _clips) if (seen.Add(c.Code)) _testQ.Add(c);
            // 154: klip probki odleglosci - pierwszy rozkaz (voice_shout); bez rozkazow - pierwszy klip 3D (nie wiatr / deszcz)
            foreach (var c in _clips) if (c.Code == "or") { _testDistClip = c; break; }
            if (_testDistClip == null) foreach (var c in _clips) if (c.Code != "wi" && c.Code != "rn") { _testDistClip = c; break; }
            Log.Info(P + "probka autotestu - " + _testQ.Count + " klipow (po jednym z kodu), start 2 s po rozstawieniu"
                     + (_testDistClip != null ? "; potem probka odleglosci " + _testDistClip.Id + " (zdarzenie " + EvName[EventFor(_testDistClip, true)] + ") w 5 m i w 150 m od sluchacza." : "; probki odleglosci brak (brak klipu 3D)."));
        }

        private static void TestTick(Mission m)
        {
            if (!_testOn) return;
            if (_testQ.Count == 0) { TestDistTick(m); return; }
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

        /// <summary>154: probka odleglosci - ten sam klip w 5 m, a po jego koncu (dlugosc + 1,5 s) w 150 m od sluchacza; odleglosci rozkazu (4/200 m x glosnosc).
        /// Recenzja 154: odstep i koniec probki (Pool, TestNote) w czasie rzeczywistym - w czasie misji autotestu obie probki trwaly po ok. 0,3 s, jedna tuz po
        /// drugiej, bez linii "w 5.0 m / w 150.0 m"; linia probki juz przy utworzeniu (nie zalezy od kontroli po 0,5 s).</summary>
        private static void TestDistTick(Mission m)
        {
            if (_testDistClip == null || _testDistStep >= TestDistM.Length || !m.IsDeploymentFinished) return;
            if (_testNext < 0f) { _testNext = _now + 2f; return; }
            if (_now < _testNext) return;
            for (int i = 0; i < _live.Count; i++) if (_live[i].Test) return;   // poprzednia probka jeszcze gra - odsluch bez nakladania
            if (Real < _testDistNextR) return;
            var c = _testDistClip;
            float d = TestDistM[_testDistStep++];
            _testDistNextR = Real + c.Dur + ClipTail + 1.5;   // poprzednia konczy sie najwczesniej po dlugosc + 0,5 s czasu rzeczywistego (Pool) - potem 1,5 s ciszy
            string note = " w " + F1(d) + " m od sluchacza (min/max " + F1(OrderMin) + "/" + F1(OrderMax) + " m x glosnosc)";   // zdarzenie naprawde uzyte - w TestChecked
            var s = Play(m, c, GAmb, _lis + new Vec3(d, 0f, 0f), OrderMin, OrderMax, true, null, null, "probka odleglosci");
            _cTest++;
            if (s == null) { Log.Info(P + "probka " + c.Id + note + " (zdarzenie " + EvShort[EventFor(c, true)] + "): NIE utworzone (limit / daleko / silnik - licznik linii misji)."); return; }
            s.Test = true;
            s.TestNote = note;
            Log.Info(P + "probka " + c.Id + " (" + c.Code + ", " + (s.Is3d ? "3D" : "2D") + ", zdarzenie " + EvShort[s.Kind] + ")" + note
                     + ": utworzone - ODSLUCH: w 5 m glosno, w 150 m wyraznie ciszej (tak samo glosno = brak 3D).");
        }

        private static void TestChecked(Snd s, bool playing)
        {
            if (playing) _cTestPlayed++;
            Log.Info(P + "probka " + s.C.Id + " (" + s.C.Code + ", " + (s.Is3d ? "3D" : "2D") + ", zdarzenie " + EvShort[s.Kind] + ")" + (s.TestNote ?? "")
                     + ": grane po 0,5 s " + (playing ? "tak" : "NIE") + ".");
        }

        private static void TestEnded(Snd s, float t)
        {
            if (s.TestNote != null)
            {
                // recenzja 154: probka odleglosci konczy sie w czasie rzeczywistym (Pool) - czas misji przy przyspieszeniu autotestu bylby mylacy
                double r = Real - s.R0;
                Log.Info(P + "probka " + s.C.Id + s.TestNote + " - koniec po " + F1((float)r) + " s czasu rzeczywistego (z dlugosci " + F1(s.C.Dur) + " s"
                         + (r > s.C.Dur + 1.5 ? ", silnik nie zglosil konca - limit czasu" : "") + ").");
                return;
            }
            Log.Info(P + "probka " + s.C.Id + " - koniec po " + F1(t) + " s (z dlugosci " + F1(s.C.Dur) + " s" + (t > s.C.Dur + 1.5f ? ", silnik nie zglosil konca - limit czasu" : "") + ").");
        }

        private static void AppendTest(StringBuilder sb)
        {
            if (_cTest > 0) sb.Append(" | probka testu ").Append(_cTest).Append(" (grane po 0,5 s ").Append(_cTestPlayed).Append(')');
        }
    }
}
