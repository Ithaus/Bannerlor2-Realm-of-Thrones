using System;
using System.Collections.Generic;
using System.IO;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// noc 25.10 P4 - PROBA AUTOTESTU przegladu armii (tylko DLL autotestu CrashScribe - LogGate.Mode 1/2 - i plik Documents\Mount and Blade II Bannerlord\
    /// CrashScribe\armoury-przeglad.flag; plik tylko czytany; w zwyklej grze nic nie robi). Bez okien (okna otwiera tylko gracz z menu obozu - autotest ich
    /// nie zobaczy i nie stanie). Raz na sesje, od 2. doby sesji (najpozniej do 10. - inaczej "bez proby (powod)"), poza bitwa i spotkaniem:
    ///  T1 raport: te same funkcje co okno (CountMen, SaddleLaw.Pairs, MarchPace.Explain, strony) - linia "raport" i strony najwyzej Rumours.PageLines linii;
    ///     tozsamosci par: jezdzcy + wyjatki = z para + kon bez uprzezy + wyjatki z koniem + bez konia; sufit kolumny wedlug wzoru Cap.
    ///  T2 zapis choragwi testowych przez Write (ta sama droga co "Save" w oknie): 2 x piechota (60 ludzi / reszta, pierwsza z tarczami), lucznicy, konni
    ///     z kapitanem-towarzyszem, konni lucznicy - tylko rodzaje obecne; odczyt 8 x pole i armia zgodny, wagi kazdej klasy = 100, kazdy obecny rodzaj pokryty.
    ///  T3 nazwy: NameOf zgodne, Export -> Import -> Export bez zmian.
    ///  T4 odmowa: 8 choragwi piechoty przy obecnych innych rodzajach - odmowa z powodem, zapis gry bez zmian (pominieta, gdy jest tylko piechota).
    ///  T5 powrot do porzadku gry: null x 16, NameOf null; T6 ponowny zapis T2 (bitwa autotestu z nim - linia "bitwa" pokaze zgodnosc).
    /// Linia "Przeglad armii (P4): test - ... - wynik TAK|NIE."
    /// </summary>
    internal static partial class ArmyReview
    {
        internal const string AutoFlagName = "armoury-przeglad.flag";
        private const int TestFirstDay = 2, TestLastDay = 10;
        private static bool _testDone, _testFlag;
        private static int _testFlagDay = -1;

        private static void TestReset() { _testDone = false; _testFlag = false; _testFlagDay = -1; }

        private static bool TestWanted(int today)
        {
            int m = LogGate.Mode;
            if (m != 1 && m != 2) return false;   // tylko DLL autotestu CrashScribe
            if (_testFlagDay != today)
            {
                _testFlagDay = today;
                try
                {
                    var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                    _testFlag = File.Exists(Path.Combine(Path.Combine(Path.Combine(docs, "Mount and Blade II Bannerlord"), "CrashScribe"), AutoFlagName));
                }
                catch { _testFlag = false; }
            }
            return _testFlag;
        }

        private static void TestStep()
        {
            int today = (int)CampaignTime.Now.ToDays;
            if (_testDone || !TestWanted(today) || _sessionDays < TestFirstDay) return;
            string why = null;
            var mp = MobileParty.MainParty;
            if (!On) why = "klucz Army Review Enabled NIE";
            else if (mp == null || Hero.MainHero == null) why = "brak partii gracza";
            else if (mp.MapEvent != null || mp.SiegeEvent != null || (PlayerEncounter.Current != null && PlayerEncounter.EncounterSettlement == null)) why = "bitwa albo spotkanie";
            else if (Behavior() == null) why = "brak OrderOfBattleCampaignBehavior";
            if (why != null)
            {
                if (!On || Behavior() == null || _sessionDays >= TestLastDay) { _testDone = true; Log.Info(P + "test - bez proby (" + why + ")."); }
                return;
            }
            _testDone = true;
            TestRun();
        }

        private static string T(bool b) { return b ? "TAK" : "NIE"; }

        private static void TestRun()
        {
            var mp = MobileParty.MainParty;
            var pts = new List<string>();
            bool ok = true;
            Log.Info(P + "test - start (zapis choragwi testowych do Order of Battle gry; stan koncowy: choragwie testowe T6).");
            // ---------------- T1 raport
            var fit = new int[5]; var wnd = new int[5];
            CountMen(mp, fit, wnd);
            var p = SaddleLaw.Pairs(mp);
            var col = MarchPace.Explain(mp);
            var pm = Paginate(MenText(mp, fit, wnd, p));
            var ph = Paginate(HorseText(mp, p));
            var pq = Paginate(MarchText(mp, col, p));
            Log.Info(ReportLine(mp, fit, wnd, p, col, pm, ph, pq));
            int longest = Math.Max(LongestPage(pm), Math.Max(LongestPage(ph), LongestPage(pq)));
            bool t1p = longest <= Rumours.PageLines;
            int exM = ExemptMounted(p);
            bool t1a = p.Riders + p.Exempt == p.Paired + p.NoSaddle + exM + p.NoHorse && p.Mounted == p.Paired + p.NoSaddle;
            bool t1c = true;
            if (col != null)
            {
                int ride = Math.Min(col.Foot, col.Horses);
                int walk = (col.Foot - ride) + Math.Max(0, col.Prisoners - (col.Horses - ride));
                t1c = walk == col.Walkers && (col.Kind == 0) == (walk > 0) && (col.Kind != 0 || col.N == walk);
            }
            pts.Add("T1 raport: strony <= " + Rumours.PageLines + " linii " + T(t1p) + " (najdluzsza " + longest + "), pary " + T(t1a) + ", kolumna " + (col == null ? "wylaczona" : T(t1c)));
            ok &= t1p && t1a && t1c;
            if (!Writes)
            {
                // klucz zapisu wylaczony = porzadek gry nietkniety (tak obiecuje opis klucza) - test tez go nie rusza
                pts.Add("T2-T6 pominiete (klucz Army Review Writes Order Of Battle NIE)");
                Log.Info(P + "test - " + string.Join(" | ", pts.ToArray()) + " - wynik " + T(ok) + ".");
                return;
            }
            // ---------------- T2 zapis choragwi testowych
            var cnt = CommandedFit();
            Hero comp = null;
            foreach (var h in PartyHeroes()) if (h != Hero.MainHero && HeroOk(h, false) && !h.IsWounded) { comp = h; break; }
            var test = TestSlots(cnt, comp);
            var r = Write(test);
            Log.Info(WriteLine("test", r));
            bool t2 = r.Ok && !r.AsReset && r.ReadField && r.ReadArmy;
            bool sums = true, covered = true;
            if (r.Ok && !r.AsReset)
            {
                for (int k = 1; k <= 4; k++)
                {
                    int sum = 0, nb = 0;
                    bool cov = false;
                    for (int i = 0; i < Slots; i++)
                    {
                        if (r.Banners[i] == null) continue;
                        if (r.Banners[i].Cls == k) { sum += r.W1[i]; nb++; }
                        if (Covers(r.Banners[i], k)) cov = true;
                    }
                    if (nb > 0 && sum != 100) sums = false;
                    if (cnt[k - 1] > 0 && !cov) covered = false;
                }
            }
            pts.Add("T2 zapis/odczyt pole " + T(r.ReadField) + ", armia " + T(r.ReadArmy) + ", suma wag klasy 100 " + T(sums) + ", rodzaje pokryte " + T(covered)
                    + ", dopisane " + r.Auto.Count + (comp != null ? ", kapitan " + comp.Name : ", bez towarzysza"));
            ok &= t2 && sums && covered;
            // ---------------- T3 nazwy
            bool t3 = true;
            if (r.Ok && r.Banners != null)
                for (int i = 0; i < Slots; i++)
                {
                    string want = r.Banners[i] != null ? r.Banners[i].Name : null;
                    if (NameOf(i) != want) t3 = false;
                }
            string e1 = Export();
            Import(e1);
            string e2 = Export();
            bool t3b = e1 == e2;
            pts.Add("T3 nazwy NameOf " + T(t3) + ", zapis nazw tam i z powrotem " + T(t3b));
            ok &= t3 && t3b;
            // ---------------- T4 odmowa przy 8 choragwiach bez miejsca na brakujacy rodzaj
            bool other = cnt[1] + cnt[2] + cnt[3] > 0;
            if (other)
            {
                var full = new Slot[Slots];
                for (int i = 0; i < Slots; i++) full[i] = new Slot { Cls = 1, Men = i == 0 ? TheRest : 10 };
                var before = SnapshotClasses();
                var rr = Write(full);
                bool t4 = !rr.Ok && rr.Error != null && SnapshotClasses() == before;
                Log.Info(WriteLine("test T4", rr));
                pts.Add("T4 odmowa przy 8 bez miejsca " + T(t4) + " (zapis gry bez zmian)");
                ok &= t4;
            }
            else pts.Add("T4 odmowa - pominieta (sama piechota)");
            // ---------------- T5 powrot do porzadku gry
            bool t5 = ResetOrder();
            bool t5b = true;
            for (int i = 0; i < Slots; i++) if (NameOf(i) != null) t5b = false;
            bool saved; int entries;
            ReadSlots(out saved, out entries);
            pts.Add("T5 powrot: null x 16 " + T(t5) + ", nazwy null " + T(t5b) + ", wpisow " + entries);
            ok &= t5 && t5b && entries == 0;
            // ---------------- T6 ponowny zapis (bitwa autotestu z choragwiami testowymi)
            var r2 = Write(TestSlots(cnt, comp));
            bool t6 = r2.Ok && !r2.AsReset && r2.ReadField && r2.ReadArmy;
            pts.Add("T6 ponowny zapis " + T(t6) + " (" + OobState() + ")");
            ok &= t6;
            Log.Info(P + "test - " + string.Join(" | ", pts.ToArray()) + " - wynik " + T(ok) + ".");
        }

        /// <summary>Choragwie testowe wedlug obecnych rodzajow: 2 x piechota (60 ludzi z tarczami / reszta), lucznicy, konni (kapitan-towarzysz), konni lucznicy.</summary>
        private static Slot[] TestSlots(int[] cnt, Hero comp)
        {
            var s = new Slot[Slots];
            int i = 0;
            if (cnt[0] > 1)
            {
                var a = new Slot { Cls = 1, Men = Math.Max(1, Math.Min(60, cnt[0] / 2)), Name = "Test Shields" };
                a.Filt[(int)FormationFilterType.Shield] = true;
                s[i++] = a;
                s[i++] = new Slot { Cls = 1, Men = TheRest, Name = "Test Foot" };
            }
            else if (cnt[0] > 0) s[i++] = new Slot { Cls = 1, Men = TheRest };
            if (cnt[1] > 0) s[i++] = new Slot { Cls = 2, Men = TheRest, Name = "Test Bows" };
            if (cnt[2] > 0) s[i++] = new Slot { Cls = 3, Men = TheRest, Captain = comp, Name = "Test Horse" };
            if (cnt[3] > 0) s[i++] = new Slot { Cls = 4, Men = TheRest };
            if (i == 0) s[0] = new Slot { Cls = 1, Men = TheRest, Captain = comp };
            return s;
        }

        /// <summary>Klasy i wagi zapisu pola i armii jako napis (porownanie przed / po odmowie).</summary>
        private static string SnapshotClasses()
        {
            var b = Behavior();
            if (b == null) return "";
            var sb = new System.Text.StringBuilder();
            for (int a = 0; a < 2; a++)
                for (int i = 0; i <= Slots; i++)
                {
                    var d = b.GetFormationDataAtIndex(i, false, a == 1);
                    sb.Append(d == null ? "-" : (int)d.FormationClass + ":" + d.PrimaryClassWeight + ":" + d.SecondaryClassWeight).Append(',');
                }
            return sb.ToString();
        }
    }
}
