using System;
using System.IO;
using TaleWorlds.CampaignSystem;

namespace Armoury
{
    /// <summary>
    /// 277 - PROBA AUTOTESTU (tylko DLL autotestu CrashScribe - LogGate.Mode 1/2 - i plik Documents\Mount and Blade II Bannerlord\CrashScribe\armoury-rody277.flag;
    /// plik tylko czytany; w zwyklej grze nic nie robi). Na kopii zapisu autotestu, nigdy na zapisach Jeffa. Raz, w pierwszej godzinie sesji od 2. godziny:
    ///  T1 przydzial rycerzy do rodow (AssignCore): siedziba = wies majatku pierwsza, rod z punktem - najblizszy majatek, reszta po kolei, bez dwoch rodow na jednym rycerzu;
    ///  T2 regula zoldu od pana (PayPlan, regula W5): ludzie ponizej celu - gotowka = zold ponad pulap wlasny, miejsce = gotowka + krok; ludzie ponad celem - gotowka do celu;
    ///     rod stac samemu (pulap wlasny >= koszt) - nic; pulap wlasny miedzy zoldem a kosztem - miejsce = ludzie + krok - pulap (recenzja 277, W5); limit 0 - nic;
    ///  T3 zapis: Export -> Import -> Export bez roznic;
    ///  T4 (stan swiata, tylko gdy przeniesienie juz bylo) kazdy rod pana 277 w koronie ma pelne parostwo BK i tier >= 3, a GentryService nie liczy go jako rycerza.
    /// Linie "Rody kanonu (277): test - Tn ... - wynik TAK|NIE|POMINIETY."
    /// </summary>
    internal static class CanonHouses277Test
    {
        internal const string AutoFlagName = "armoury-rody277.flag";
        private static bool _done, _flag;
        private static int _flagDay = -1, _hours;

        internal static void Reset() { _done = false; _flag = false; _flagDay = -1; _hours = 0; }

        private static string FlagPath()
        {
            var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            return Path.Combine(Path.Combine(Path.Combine(docs, "Mount and Blade II Bannerlord"), "CrashScribe"), AutoFlagName);
        }

        private static bool Wanted()
        {
            int m = LogGate.Mode;
            if (m != 1 && m != 2) return false;
            int today = (int)CampaignTime.Now.ToDays;
            if (_flagDay != today)
            {
                _flagDay = today;
                try { _flag = File.Exists(FlagPath()); } catch { _flag = false; }
            }
            return _flag;
        }

        internal static string StartText()
        {
            int m = LogGate.Mode;
            if (m != 1 && m != 2) return "";
            _flagDay = -1;
            return " | proba autotestu: flaga " + AutoFlagName + " " + (Wanted() ? "TAK" : "NIE");
        }

        private static void Line(string text, bool? ok) { Log.Info(CanonHouses277.Tag + "test - " + text + " - wynik " + (ok == null ? "POMINIETY" : ok.Value ? "TAK" : "NIE") + "."); }

        /// <summary>Co godzine (ArmouryBehavior): w zwyklej grze Wanted wraca od razu.</summary>
        internal static void Hourly()
        {
            if (_done || Campaign.Current == null) return;
            if (++_hours < 2) return;
            if (!Wanted()) return;
            _done = true;
            try { T1(); } catch (Exception e) { Line("T1 przydzial: wyjatek " + e.GetType().Name, false); }
            try { T2(); } catch (Exception e) { Line("T2 zold od pana: wyjatek " + e.GetType().Name, false); }
            try { string why; bool ok = CanonHouses277.RoundTrip(out why); Line("T3 zapis Export -> Import -> Export" + (ok ? "" : ": " + why), ok); }
            catch (Exception e) { Line("T3 zapis: wyjatek " + e.GetType().Name, false); }
            try { CanonHouses277.T4(Line); } catch (Exception e) { Line("T4 stan rodow: wyjatek " + e.GetType().Name, false); }
        }

        private static void T1()
        {
            // kandydaci: 0 Feastfires (10,0), 1 Ironbay (100,0), 2 Straits (50,0); cele: Prester (siedziba Feastfires), Spicer (punkt 95,0), Jast (bez punktu), Plumm (bez punktu)
            var est = new[] { "village_V8_3", "v_iron", "v_str" };
            var cx = new double[] { 10, 100, 50 }; var cy = new double[] { 0, 0, 0 }; var cp = new[] { true, true, true };
            var seats = new[] { new[] { "village_V8_3" }, new string[0], new string[0], new string[0] };
            var tx = new double[] { 0, 95, 0, 0 }; var ty = new double[] { 0, 0, 0, 0 }; var tp = new[] { false, true, false, false };
            var p = CanonHouses277.AssignCore(est, cx, cy, cp, seats, tx, ty, tp);
            bool ok = p[0] == 0 && p[1] == 1 && p[2] == 2 && p[3] == -1;
            Line("T1 przydzial: Prester -> Feastfires " + p[0] + ", Spicer (95) -> Ironbay " + p[1] + ", Jast -> Straits " + p[2] + ", Plumm bez rycerza " + p[3], ok);
        }

        private static void T2()
        {
            double c1, r1, c2, r2, c3, r3, c4, r4, c5, r5;
            // limit 200, 50 ludzi, zold 100 (2/czl), pulap wlasny 30: gotowka 70, krok min(20, 120-50) x 2 = 40 -> miejsce 110
            CanonHouses277.PayPlan(200, 50, 100, 2, 30, 0.6, 0.1, out c1, out r1);
            // 150 ludzi ponad cel 120: gotowka = 300 x 120/150 - 30 = 210, krok 0
            CanonHouses277.PayPlan(200, 150, 300, 2, 30, 0.6, 0.1, out c2, out r2);
            // rod stac samemu (pulap 500 >= koszt 240): nic (recenzja 277 - W5 NaCost <= 0; dotad miejsce 40)
            CanonHouses277.PayPlan(200, 50, 100, 2, 500, 0.6, 0.1, out c3, out r3);
            // pulap 120 miedzy zoldem 100 a kosztem 240: gotowka 0, miejsce = min(240 - 120, 100 + 40 - 120) = 20 (W5 boost)
            CanonHouses277.PayPlan(200, 50, 100, 2, 120, 0.6, 0.1, out c5, out r5);
            CanonHouses277.PayPlan(0, 0, 0, 2, 0, 0.6, 0.1, out c4, out r4);
            bool ok = Near(c1, 70) && Near(r1, 110) && Near(c2, 210) && Near(r2, 210) && Near(c3, 0) && Near(r3, 0) && c4 == 0 && r4 == 0 && Near(c5, 0) && Near(r5, 20);
            Line("T2 zold od pana (regula W5): " + c1 + "/" + r1 + ", " + c2 + "/" + r2 + ", " + c3 + "/" + r3 + ", " + c4 + "/" + r4 + ", " + c5 + "/" + r5, ok);
        }

        private static bool Near(double a, double b) { return Math.Abs(a - b) < 0.001; }
    }
}
