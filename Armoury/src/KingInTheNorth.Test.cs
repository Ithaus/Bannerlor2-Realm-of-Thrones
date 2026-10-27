using System;
using System.IO;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// 272 - PROBA AUTOTESTU (tylko DLL autotestu CrashScribe - LogGate.Mode 1/2 - i plik Documents\Mount and Blade II Bannerlord\CrashScribe\armoury-krol-polnocy.flag;
    /// plik tylko czytany; w zwyklej grze nic nie robi). Flaga wymusza tylko ZNACZNIK wyzwalacza od 2. doby sesji (kanon i bezpieczna chwila - jak w grze). Po unii
    /// (wymuszonej albo prawdziwej, np. kopia zapisu Jeffa - znacznik dawno minal) linie "Krol Polnocy (272): test - Tn ... - wynik TAK|NIE|POMINIETY":
    ///  T1 migawka (rodow &gt; 0); T2 rody / lenna / partie / ludzie przed = po, zniszczonych 0; T3 kazda armia z tymi samymi czlonkami i spojnoscia (bez armii -
    ///  POMINIETY); T4 Dorzecze zniszczone, sojuszu nie ma, korona w wojnie z kazdym przeniesionym rodem; T5 kontrola pieniedzy 0; T6 rola i mnoznik gracza, jency gracza
    ///  ci sami (Tywin); T7 zapis tam i z powrotem (Export -&gt; Import -&gt; Export) identyczny; T8 Kronika: war:riverlands:vlandia = war:battania:vlandia,
    ///  own:town_V2:riverlands = Riverrun w Polnocy na ziemi Tridentu; T9 drugie wyzwolenie nic nie robi. Bez unii do 10. doby sesji - T0 POMINIETY z powodem.
    /// </summary>
    internal static partial class KingInTheNorth
    {
        internal const string AutoFlagName = "armoury-krol-polnocy.flag";
        private const int TestFirstDay = 2, TestLastDay = 10;
        private static bool _testDone, _testFlag;
        private static int _testFlagDay = -1;

        private static void TestReset() { _testDone = false; _testFlag = false; _testFlagDay = -1; }

        private static void TLine(string t, string what, string res) { Log.Info(P + "test - " + t + " " + what + " - wynik " + res + "."); }

        private static bool TestWanted()
        {
            int m = LogGate.Mode;
            if (m != 1 && m != 2) return false;
            if (_testFlagDay != _sessionDays)
            {
                _testFlagDay = _sessionDays;
                try
                {
                    var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                    _testFlag = File.Exists(Path.Combine(Path.Combine(Path.Combine(docs, "Mount and Blade II Bannerlord"), "CrashScribe"), AutoFlagName));
                }
                catch { _testFlag = false; }
            }
            return _testFlag;
        }

        /// <summary>Flaga autotestu wymusza znacznik wyzwalacza od TestFirstDay doby sesji.</summary>
        private static bool TestForce() { return !_testDone && TestWanted() && _sessionDays >= TestFirstDay; }

        /// <summary>Co godzine: z flaga, bez unii do TestLastDay doby sesji - T0 POMINIETY (raz).</summary>
        private static void TestAfter()
        {
            if (_testDone || _sessionDays < TestLastDay || !TestWanted()) return;
            _testDone = true;
            TLine("T0", "bez unii do " + TestLastDay + ". doby sesji (stan " + (_st == 1 ? "zrobione wczesniej" : _st == 2 ? "nigdy: " + _never : "czeka: " + _lastWait) + ")", "POMINIETY");
        }

        private static void TestRun(Kingdom river, Kingdom north, Kingdom crown)
        {
            if (_testDone || !TestWanted()) return;
            _testDone = true;
            try
            {
                TLine("T1", "migawka: rodow " + _tClans + ", twierdz " + _tFiefs0 + ", partii " + _tParties0 + ", ludzi " + _tMen0, _tClans > 0 ? "TAK" : "NIE");
                bool t2 = _tMoved == _tClans && _tDestroyed == 0 && _tFiefs1 == _tFiefs0 && _tParties1 == _tParties0 && _tMen1 == _tMen0;
                TLine("T2", "rody " + _tMoved + "/" + _tClans + " (zniszczonych " + _tDestroyed + "), lenna " + _tFiefs1 + "/" + _tFiefs0 + ", partie " + _tParties1 + "/" + _tParties0 + ", ludzie " + _tMen1 + "/" + _tMen0, t2 ? "TAK" : "NIE");
                TLine("T3", "armie: " + _tArmies + ", z tymi samymi czlonkami " + _tArmiesOk + ", z ta sama spojnoscia " + _tCohOk + " (latka " + _wireArmy + ")",
                      _tArmies == 0 ? "POMINIETY" : (_tArmiesOk == _tArmies && _tCohOk == _tArmies ? "TAK" : "NIE"));
                TLine("T4", "Dorzecze zniszczone " + (_tRiverGone ? "TAK" : "NIE") + ", sojusz Polnoc+Dorzecze " + (_tAllyGone ? "brak" : "JEST") + ", korona w wojnie z rodami unii " + (_tCrownWar ? "TAK" : "NIE"),
                      _tRiverGone && _tAllyGone && _tCrownWar ? "TAK" : "NIE");
                TLine("T5", "kontrola pieniedzy (zdjete - dopisane) " + (_tControl == long.MinValue ? "brak" : _tControl.ToString()), _tControl == 0 ? "TAK" : "NIE");
                TLine("T6", "gracz (" + _player + ") rola i mnoznik " + (_tPlayerOk ? "zachowane" : "ZMIENIONE") + ", jency " + (_tPrisOk ? "ci sami" : "INNI"), _tPlayerOk && _tPrisOk ? "TAK" : "NIE");
                string e1 = Export(); Import(e1); string e2 = Export();
                TLine("T7", "zapis tam i z powrotem (" + e1.Length + " znakow)", e1 == e2 ? "TAK" : "NIE");
                bool w1 = Chronicle.Terms.Eval("war:riverlands:vlandia"), w2 = Chronicle.Terms.Eval("war:battania:vlandia");
                var rr = Settlement.Find("town_V2");
                bool o1 = Chronicle.Terms.Eval("own:town_V2:riverlands"), o2 = HeldAsRiverlands(rr);
                TLine("T8", "Kronika: war:riverlands:vlandia " + (w1 ? "TAK" : "NIE") + " = war:battania:vlandia " + (w2 ? "TAK" : "NIE") + ", own:town_V2:riverlands " + (o1 ? "TAK" : "NIE")
                      + " = Riverrun w Polnocy na ziemi Tridentu " + (o2 ? "TAK" : "NIE"), w1 == w2 && o1 == o2 ? "TAK" : "NIE");
                int c0 = _mergeCount, n0 = north.Clans.Count;
                Hourly();
                bool t9 = _mergeCount == c0 && north.Clans.Count == n0 && _st == 1 && river.IsEliminated;
                TLine("T9", "drugie wyzwolenie: unii " + _mergeCount + " (przed " + c0 + "), rodow Polnocy " + north.Clans.Count + " (przed " + n0 + ")", t9 ? "TAK" : "NIE");
            }
            catch (Exception e) { Stumble("TestRun", e); TLine("T?", "wyjatek " + e.GetType().Name, "NIE"); }
        }
    }
}
