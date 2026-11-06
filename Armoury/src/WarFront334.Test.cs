using System;
using System.IO;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace Armoury
{
    /// <summary>
    /// 334 - PROBA AUTOTESTU (wzor WarTruceTest: tylko DLL autotestu CrashScribe - LogGate.Mode 1/2 - i plik Documents\Mount and Blade II Bannerlord\CrashScribe\
    /// armoury-balans334.flag; w zwyklej grze nic nie robi). Tylko czyste funkcje i odczyty - NIE rusza stanu wojny, pokoju, rozejmu ani zapisu. Od 2. godziny sesji:
    ///  T1 tabela teatru (Polnoc przed / po Krwawych Godach, odbicie, Wyspy przed / po), T2 udzial zablokowanego zagrozenia (B3), T3 doby drogi (Days),
    ///  T4 zapis Export -> Import -> Export identyczny, T5 F334 policzone (partii > 0, front Polnoc / korona widoczny w linii).
    /// Linie "Balans wojny (334): test - Tn ... - wynik TAK|NIE|POMINIETY."
    /// </summary>
    internal static class WarFront334Test
    {
        internal const string AutoFlagName = "armoury-balans334.flag";
        private static bool _done, _flag;
        private static int _flagDay = -1, _hours;

        internal static void Reset() { _done = false; _flag = false; _flagDay = -1; _hours = 0; }

        private static string P { get { return WarFront334.Tag + "test - "; } }

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

        private static void Say(string t, bool ok, string detail) { Log.Info(P + t + " " + detail + " - wynik " + (ok ? "TAK" : "NIE") + "."); }

        internal static void Hourly()
        {
            try
            {
                if (_done || Campaign.Current == null || !Wanted()) return;
                if (++_hours < 2) return;
                _done = true;
                // T1
                bool t1 = WarFront334.TheatreVetoKind("battania", "crownlands", false, false) && WarFront334.TheatreVetoKind("battania", "reach", false, false)
                          && WarFront334.TheatreVetoKind("battania", "sturgia", false, false) && !WarFront334.TheatreVetoKind("battania", "river", false, false)
                          && !WarFront334.TheatreVetoKind("battania", "vlandia", false, false) && !WarFront334.TheatreVetoKind("battania", "battania", false, false)
                          && !WarFront334.TheatreVetoKind("battania", "crownlands", true, false) && !WarFront334.TheatreVetoKind("battania", "crownlands", false, true)
                          && WarFront334.TheatreVetoKind("sturgia", "reach", false, false) && !WarFront334.TheatreVetoKind("sturgia", "reach", false, true)
                          && !WarFront334.TheatreVetoKind("sturgia", "battania", false, false) && !WarFront334.TheatreVetoKind("vlandia", "battania", false, false)
                          && !WarFront334.TheatreVetoKind("battania", "freefolk", false, false);
                Say("T1 tabela teatru (Polnoc / Wyspy, odbicie, Krwawe Gody)", t1, "13 przypadkow");
                // T2
                bool t2 = WarFront334.LockedShare(5, 10) == true && WarFront334.LockedShare(4.9, 10) == false && WarFront334.LockedShare(0, 0) == null;
                Say("T2 udzial zablokowanego zagrozenia (B3)", t2, "5/10, 4.9/10, 0/0");
                // T3
                float d1 = WarFront334.Days(MobileParty.NavigationType.Default, 24f * 2f * 8f, 2f, 3f);
                float d2 = WarFront334.Days(MobileParty.NavigationType.None, 10f, 2f, 3f);
                bool t3 = Math.Abs(d1 - 8f) < 0.01f && d2 < 0f;
                Say("T3 doby drogi", t3, "Default " + d1.ToString("0.00") + ", brak drogi " + d2.ToString("0.00"));
                // T4
                string a = WarFront334.Export();
                WarFront334.Import(a);
                string b = WarFront334.Export();
                Say("T4 zapis Export/Import", a == b, "dlugosc " + a.Length);
                // T5
                if (!WarFront334.Canon) Log.Info(P + "T5 F334 policzone - kampania klasyczna - wynik POMINIETY.");
                else
                {
                    bool ok = WarFront334.Ensure();
                    var n = WarFront334.FindKingdom(WarFront334.NorthId); var c = WarFront334.FindKingdom(WarFront334.CrownId);
                    double fn = n != null && c != null ? WarFront334.Front(n, c) : -1, fc = n != null && c != null ? WarFront334.Front(c, n) : -1;
                    Say("T5 F334 policzone", ok && fn >= 0 && fc >= 0, "Polnoc->korona " + fn.ToString("0") + ", korona->Polnoc " + fc.ToString("0"));
                }
            }
            catch (Exception e) { WarFront334.Stumble("Test", e); }
        }
    }
}
