using System;
using System.IO;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// 274 - PROBA AUTOTESTU (tylko DLL autotestu CrashScribe - LogGate.Mode 1/2 - i plik Documents\Mount and Blade II Bannerlord\CrashScribe\armoury-rownowaga274.flag;
    /// plik tylko czytany; w zwyklej grze nic nie robi). Na kopii zapisu autotestu, nigdy na zapisach Jeffa. Raz, w pierwszej godzinie sesji od 2. godziny poza bitwa:
    ///  T1 tabela przelacznika (3 doby poza 1.5 - wlacz, 3 doby w 1.2 - wylacz, 1.2-1.5 trzyma, slabszy co dobe) i cel wypelnienia W1 (min 0.6, max 1);
    ///  T2 strony z danych: Dorzecze obecne (A = battania + riverlands), po unii 272 (riverlands zniszczone -> A = battania), sojusznik korony w wojnie z Polnoca -> B;
    ///  T3 tabela weta (siedziba, za frontem przy powsciagliwosci, cel frontu, trwajace oblezenie, cel nie drugiej strony);
    ///  T4 (zamiast SCA - martwy kod) postfiks BK FindArmyObjective: Krolewska Przystan jako cel oblezenia dla Polnocy -> brak celu (tylko przy czynnej regule);
    ///  T5 znacznik Krwawych Godow: RedWeddingEvent 2 albo smierc Robba wylacza regule; 0, 1 i -1 nie;
    ///  T6 zapis: Export -> Import -> Export bez roznic.
    /// Linie "Rownowaga wojny (274): test - Tn ... - wynik TAK|NIE|POMINIETY."
    /// </summary>
    internal static class CanonWarBalanceTest
    {
        internal const string AutoFlagName = "armoury-rownowaga274.flag";
        private static bool _done, _flag;
        private static int _flagDay = -1, _hours;

        internal static void Reset() { _done = false; _flag = false; _flagDay = -1; _hours = 0; }

        private static string P { get { return CanonWarBalance.Tag + "test - "; } }

        private static string FlagPath()
        {
            var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            return Path.Combine(Path.Combine(Path.Combine(docs, "Mount and Blade II Bannerlord"), "CrashScribe"), AutoFlagName);
        }

        private static bool Wanted()
        {
            int m = LogGate.Mode;
            if (m != 1 && m != 2) return false;   // tylko DLL autotestu CrashScribe
            int today = (int)CampaignTime.Now.ToDays;
            if (_flagDay != today)
            {
                _flagDay = today;
                try { _flag = File.Exists(FlagPath()); } catch { _flag = false; }
            }
            return _flag;
        }

        /// <summary>Dopisek linii startu 274 (tylko DLL autotestu; w grze pusty).</summary>
        internal static string StartText()
        {
            int m = LogGate.Mode;
            if (m != 1 && m != 2) return "";
            _flagDay = -1;
            return " | proba autotestu: flaga " + AutoFlagName + " " + (Wanted() ? "TAK" : "NIE");
        }

        private static void Line(string text, bool? ok) { Log.Info(P + text + " - wynik " + (ok == null ? "POMINIETY" : ok.Value ? "TAK" : "NIE") + "."); }

        /// <summary>Co godzine (ArmouryBehavior): w zwyklej grze Wanted wraca od razu.</summary>
        internal static void Hourly()
        {
            if (_done) return;
            _hours++;
            if (_hours < 2 || !Wanted()) return;
            var mp = TaleWorlds.CampaignSystem.Party.MobileParty.MainParty;
            if (mp != null && (mp.MapEvent != null || mp.SiegeEvent != null)) return;
            _done = true;
            try { Run(); } catch (Exception e) { CanonWarBalance.Stumble("Test.Run", e); Line("T0 bieg przerwany (" + e.GetType().Name + ")", false); }
        }

        private struct St { public bool On; public int Weak, SOn, SOff; }

        private static St Days(St s, params double[] rs)
        {
            foreach (var r in rs) CanonWarBalance.Step(r, ref s.On, ref s.Weak, ref s.SOn, ref s.SOff);
            return s;
        }

        private static void Run()
        {
            // T1
            {
                var a = Days(new St(), 0.47, 0.47);                  // 2 doby poza - jeszcze nie
                var b = Days(a, 0.47);                                // 3. doba - wlacz, slabszy B
                var c = Days(b, 1.3, 1.3, 1.3, 1.3);                 // 1.2-1.5 - trzyma; slabszy A (r > 1)
                var d = Days(c, 1.1, 1.1);                            // 2 doby w 1.2 - trzyma
                var e = Days(d, 0.95);                                // 3. doba w 1.2 - wylacz
                var f = Days(new St(), 0.47, 0.47, 1.0, 0.47, 0.47);  // przerwana seria - nie
                var g = Days(new St(), 1.6, 1.7, 1.8);                // przewaga B - slabszy A
                float t1 = CanonWarBalance.TargetFill(0.66, 19500, 9200), t2 = CanonWarBalance.TargetFill(0.9, 10000, 10000), t3 = CanonWarBalance.TargetFill(0.2, 10000, 10000);
                bool ok = !a.On && b.On && b.Weak == 2 && c.On && c.Weak == 1 && d.On && !e.On && e.Weak == 0 && !f.On && g.On && g.Weak == 1
                          && t1 >= 0.99f && Math.Abs(t2 - 0.75f) < 0.01f && Math.Abs(t3 - CanonWarBalance.MinFill) < 0.001f;
                Line("T1 przelacznik: 2 doby " + T(a.On) + ", 3 doby " + T(b.On) + " slabszy " + b.Weak + ", 1.3 trzyma " + T(c.On) + " slabszy " + c.Weak + ", 1.1 x2 " + T(d.On)
                     + ", 1.1 x3 " + T(e.On) + ", przerwana " + T(f.On) + ", przewaga B " + T(g.On) + " slabszy " + g.Weak + "; cel W1 " + t1.ToString("0.00") + " / " + t2.ToString("0.00")
                     + " / " + t3.ToString("0.00"), ok);
            }
            // T2
            {
                int n1 = CanonWarBalance.CoreSide(Prologue.NorthId, false), r1 = CanonWarBalance.CoreSide(CanonWarBalance.RiverId, false), c1 = CanonWarBalance.CoreSide(Prologue.Crown, false);
                int r2 = CanonWarBalance.CoreSide(CanonWarBalance.RiverId, true), x = CanonWarBalance.CoreSide("empire", false);
                int reach = CanonWarBalance.AllySide(false, true, true, false), neutral = CanonWarBalance.AllySide(false, true, false, false), both = CanonWarBalance.AllySide(true, true, true, true);
                bool ok = n1 == 1 && r1 == 1 && c1 == 2 && r2 == 0 && x == 0 && reach == 2 && neutral == 0 && both == 0;
                Line("T2 strony: Polnoc " + n1 + ", Dorzecze " + r1 + " (po unii 272 " + r2 + "), korona " + c1 + ", obce " + x + "; sojusznik korony w wojnie z A " + reach
                     + ", sojusznik bez wojny " + neutral + ", sojusznik obu " + both + " | dzis A=" + Sides(1) + " B=" + Sides(2), ok);
            }
            // T3
            {
                int seat = CanonWarBalance.VetoKind(true, false, true, false, true), back = CanonWarBalance.VetoKind(true, false, false, true, false);
                int front = CanonWarBalance.VetoKind(true, false, false, true, true), free = CanonWarBalance.VetoKind(true, false, false, false, false);
                int run = CanonWarBalance.VetoKind(true, true, true, true, false), own = CanonWarBalance.VetoKind(false, false, true, true, false);
                bool ok = seat == 1 && back == 2 && front == 0 && free == 0 && run == 0 && own == 0;
                Line("T3 weto: siedziba " + seat + ", za frontem (powsciagany) " + back + ", cel frontu " + front + ", bez powsciagliwosci " + free + ", trwajace " + run + ", nie druga strona " + own, ok);
            }
            // T4
            {
                Kingdom north = null; foreach (var k in Kingdom.All) if (k != null && k.StringId == Prologue.NorthId) north = k;
                Settlement kl = null; try { kl = Settlement.Find("town_EW4"); } catch { }
                if (north == null || kl == null || CanonWarBalance.SideOf(north) != 1 || CanonWarBalance.SideOf(kl.MapFaction) != 2)
                    Line("T4 BK cel armii (regula nieczynna albo brak Polnocy / Krolewskiej Przystani po stronach)", null);
                else
                {
                    ValueTuple<Settlement, Army.ArmyTypes>? res = (kl, Army.ArmyTypes.Besieger);
                    CanonWarBalance.BkObjectivePostfix(null, north, ref res);
                    ValueTuple<Settlement, Army.ArmyTypes>? def = (kl, Army.ArmyTypes.Defender);
                    CanonWarBalance.BkObjectivePostfix(null, north, ref def);
                    Line("T4 BK cel armii Polnocy: oblezenie Krolewskiej Przystani zdjete " + T(!res.HasValue) + ", cel obrony zostaje " + T(def.HasValue), !res.HasValue && def.HasValue);
                }
            }
            // T5
            {
                bool a = CanonWarBalance.Active(true, true, true, CanonWarClock.Over(0, false)), b = CanonWarBalance.Active(true, true, true, CanonWarClock.Over(1, false));
                bool c = CanonWarBalance.Active(true, true, true, CanonWarClock.Over(-1, false)), d = CanonWarBalance.Active(true, true, true, CanonWarClock.Over(2, false));
                bool e = CanonWarBalance.Active(true, true, true, CanonWarClock.Over(0, true)), f = CanonWarBalance.Active(false, true, true, false), g = CanonWarBalance.Active(true, false, true, false);
                bool h = CanonWarBalance.Active(true, true, false, false);
                bool ok = a && b && c && !d && !e && !f && !g && !h;
                Line("T5 Krwawe Gody: stan 0 " + T(a) + ", 1 " + T(b) + ", -1 " + T(c) + ", 2 " + T(d) + ", Robb martwy " + T(e) + ", klucz NIE " + T(f) + ", klasyczna " + T(g) + ", bez strony " + T(h)
                     + " | teraz RedWeddingEvent " + CanonWarClock.RedWeddingState + ", warunki ROT: " + CanonWarClock.Conditions(), ok);
            }
            // T6
            {
                string x1 = CanonWarBalance.Export();
                CanonWarBalance.Import(x1);
                string x2 = CanonWarBalance.Export();
                Line("T6 zapis: dlugosc " + x1.Length + " znakow, round-trip " + T(x1 == x2), x1 == x2 && x1.Length < 1024);
            }
        }

        private static string T(bool b) { return b ? "TAK" : "NIE"; }

        private static string Sides(int side)
        {
            var l = new System.Collections.Generic.List<string>();
            foreach (var k in Kingdom.All) if (k != null && CanonWarBalance.SideOf(k) == side) l.Add(k.StringId);
            return "[" + string.Join(",", l.ToArray()) + "]";
        }
    }
}
