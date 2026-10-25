using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.AgentOrigins;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// noc 25.10 P4 - PROBA AUTOTESTU przegladu armii (tylko DLL autotestu CrashScribe - LogGate.Mode 1/2 - i plik Documents\Mount and Blade II Bannerlord\
    /// CrashScribe\armoury-przeglad.flag; plik tylko czytany; w zwyklej grze nic nie robi). Bez okien (okna otwiera tylko gracz z menu obozu - autotest ich
    /// nie zobaczy i nie stanie). Raz na sesje, w pierwszym takcie godziny albo doby sesji poza bitwa i spotkaniem (P12 - 124c: proba od 2. doby czekala
    /// tylko na takt doby i nie odpalila; czekanie ma teraz linie "test - czeka (powod)" raz na dobe, najwyzej 5; po 10. dobie "bez proby (powod)");
    /// linia startu sesji mowi, czy flaga jest widziana ("proba autotestu: flaga ... TAK/NIE (sciezka)"):
    ///  T1 raport: te same funkcje co okno (CountMen, SaddleLaw.Pairs, MarchPace.Explain, strony) - linia "raport" i strony najwyzej Rumours.PageLines linii;
    ///     tozsamosci par: jezdzcy + wyjatki = z para + kon bez uprzezy + wyjatki z koniem + bez konia; sufit kolumny wedlug wzoru Cap.
    ///  T2 zapis choragwi testowych przez Write (ta sama droga co "Save" w oknie): 2 x piechota (60 ludzi / reszta, pierwsza z tarczami), lucznicy, konni
    ///     z kapitanem-towarzyszem, konni lucznicy - tylko rodzaje obecne; odczyt 8 x pole i armia zgodny, wagi kazdej klasy = 100, kazdy obecny rodzaj pokryty.
    ///  T3 nazwy: NameOf zgodne, Export -> Import -> Export bez zmian.
    ///  T4 odmowa: 8 choragwi piechoty przy obecnych innych rodzajach - odmowa z powodem, zapis gry bez zmian (pominieta, gdy jest tylko piechota).
    ///  T5 powrot do porzadku gry: null x 16, NameOf null, przypiecia zdjete; T6 ponowny zapis T2 (bitwa autotestu z nim - linia "bitwa" pokaze zgodnosc).
    ///  T7 (P12) przypiecia: najliczniejszy typ druzyny -> choragiew innego rodzaju (Write z przypieciami - droga "Save"), wagi 100, udzial bez przypietych,
    ///     zapis arm_army_review tam i z powrotem, podzial spawnu na prawdziwych PartyAgentOrigin (Split / Merge - przypieci z druzyny gracza do choragwi,
    ///     bohater i obcy tego typu bez zmian, kazdy raz; zamiana bez dubla), odpiecie, zwiniecie choragwi, ponowne przypiecie (stan koncowy - bitwa
    ///     autotestu z przypieciem: linia "bitwa" ma segment "przypiecia").
    ///  T7g (recenzja P12, przed stanem koncowym T7): jedyny typ rodzaju przypiety do choragwi swojego rodzaju - zapis, liczba klasy ekranu (CountAfterPins)
    ///     i naprawa wag po bitwie (RepairWeights) na zapisie popsutym jak przez ekran Order of Battle (int.MinValue z 0/0, zero); pominiety, gdy kazdy
    ///     rodzaj ma kilka typow. Flaga biegu: tools/wgranie/bieg-autotest.ps1 -Flags armoury-przeglad (galaz noc/p12-flagi) - bez pliku proby nie ma.
    /// Linia "Przeglad armii (P4): test - ... - wynik TAK|NIE."
    /// </summary>
    internal static partial class ArmyReview
    {
        internal const string AutoFlagName = "armoury-przeglad.flag";
        private const int TestLastDay = 10, TestWaitLines = 5;
        private static bool _testDone, _testFlag;
        private static int _testFlagDay = -1, _testWaitDay = -1, _testWaits, _sessionHours;

        private static void TestReset() { _testDone = false; _testFlag = false; _testFlagDay = -1; _testWaitDay = -1; _testWaits = 0; _sessionHours = 0; }

        private static string FlagPath()
        {
            var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            return Path.Combine(Path.Combine(Path.Combine(docs, "Mount and Blade II Bannerlord"), "CrashScribe"), AutoFlagName);
        }

        /// <summary>Stan proby do linii startu sesji (tylko DLL autotestu; w grze "-").</summary>
        internal static string TestState()
        {
            int m = LogGate.Mode;
            if (m != 1 && m != 2) return "-";
            string path;
            try { path = FlagPath(); } catch { path = "?"; }
            _testFlagDay = -1;
            bool f = TestWanted((int)CampaignTime.Now.ToDays);
            return "flaga " + AutoFlagName + " " + (f ? "TAK - proba w pierwszym takcie godziny poza bitwa i spotkaniem" : "NIE (" + path + ")");
        }

        /// <summary>Takt godziny (AddMenus): sama proba autotestu - w zwyklej grze TestWanted wraca od razu (tryb gry).</summary>
        private static void Hourly()
        {
            _sessionHours++;
            if (_testDone) return;
            try { TestStep(); } catch (Exception e) { _testDone = true; Stumble("Test", e); }
        }

        private static bool TestWanted(int today)
        {
            int m = LogGate.Mode;
            if (m != 1 && m != 2) return false;   // tylko DLL autotestu CrashScribe
            if (_testFlagDay != today)
            {
                _testFlagDay = today;
                try { _testFlag = File.Exists(FlagPath()); }
                catch { _testFlag = false; }
            }
            return _testFlag;
        }

        private static void TestStep()
        {
            int today = (int)CampaignTime.Now.ToDays;
            if (_testDone || !TestWanted(today) || (_sessionHours < 1 && _sessionDays < 1)) return;
            string why = null;
            var mp = MobileParty.MainParty;
            if (!On) why = "klucz Army Review Enabled NIE";
            else if (mp == null || Hero.MainHero == null) why = "brak partii gracza";
            else if (mp.MapEvent != null || mp.SiegeEvent != null || (PlayerEncounter.Current != null && PlayerEncounter.EncounterSettlement == null)) why = "bitwa albo spotkanie";
            else if (Behavior() == null) why = "brak OrderOfBattleCampaignBehavior";
            if (why != null)
            {
                if (!On || Behavior() == null || _sessionDays >= TestLastDay) { _testDone = true; Log.Info(P + "test - bez proby (" + why + ")."); }
                else if (_testWaitDay != today && _testWaits < TestWaitLines)
                {
                    _testWaitDay = today; _testWaits++;
                    Log.Info(P + "test - czeka (" + why + ") | doba sesji " + _sessionDays + ", godzin " + _sessionHours + ", najpozniej do " + TestLastDay + ". doby.");
                }
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
            Log.Info(P + "test - start (doba sesji " + _sessionDays + ", godzin " + _sessionHours + "; zapis choragwi testowych do Order of Battle gry; stan koncowy: choragwie testowe T6 z przypieciem T7).");
            // ---------------- T1 raport
            var fit = new int[5]; var wnd = new int[5];
            CountMen(mp, fit, wnd);
            var p = SaddleLaw.Pairs(mp);
            var col = MarchPace.Explain(mp);
            var pm = Paginate(MenText(mp, fit, wnd, p));
            var ph = Paginate(HorseText(mp, p));
            var pq = Paginate(MarchText(mp, col, p));
            var pl = Paginate(LastBattleText());   // 133b: strony "Last battle" (zapis ostatniej bitwy albo zdanie "nic jeszcze")
            Log.Info(ReportLine(mp, fit, wnd, p, col, pm, ph, pq, pl));
            int longest = Math.Max(Math.Max(LongestPage(pm), LongestPage(pl)), Math.Max(LongestPage(ph), LongestPage(pq)));
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
            bool t5c = _pins.Count == 0;
            pts.Add("T5 powrot: null x 16 " + T(t5) + ", nazwy null " + T(t5b) + ", przypiecia zdjete " + T(t5c) + ", wpisow " + entries);
            ok &= t5 && t5b && t5c && entries == 0;
            // ---------------- T6 ponowny zapis (bitwa autotestu z choragwiami testowymi)
            var r2 = Write(TestSlots(cnt, comp));
            bool t6 = r2.Ok && !r2.AsReset && r2.ReadField && r2.ReadArmy;
            pts.Add("T6 ponowny zapis " + T(t6) + " (" + OobState() + ")");
            ok &= t6;
            // ---------------- T7 przypiecia typow (P12)
            bool t7;
            try { t7 = TestPins(cnt, comp, pts); }
            catch (Exception e) { t7 = false; pts.Add("T7 BLAD " + e.GetType().Name); Stumble("Test.T7", e); }
            ok &= t7;
            Log.Info(P + "test - " + string.Join(" | ", pts.ToArray()) + " - wynik " + T(ok) + ".");
        }

        /// <summary>T7: przypiecie najliczniejszego typu do choragwi innego rodzaju - zapis, wagi, zapis gry, podzial spawnu, odpiecie, zwiniecie, stan koncowy.</summary>
        private static bool TestPins(int[] cnt, Hero comp, List<string> pts)
        {
            if (!PinOn) { pts.Add("T7 przypiecia pominiete (klucz Army Review Pin Troop Types NIE)"); return true; }
            TypeRow pick = null;
            foreach (var t in PartyTypes()) if (t.Fit > 0 && (pick == null || t.Fit > pick.Fit)) pick = t;
            if (pick == null) { pts.Add("T7 przypiecia pominiete (brak zdrowych szeregowych w druzynie)"); return true; }
            string id = pick.Ch.StringId;
            int kind = KindOf(pick.Ch) + 1;
            var slots = TestSlots(cnt, comp);
            int target = -1;
            for (int i = 0; i < Slots && target < 0; i++) if (slots[i] != null && !Covers(slots[i], kind)) target = i;   // inny rodzaj - widac w bitwie
            for (int i = Slots - 1; i >= 0 && target < 0; i--) if (slots[i] != null) target = i;
            if (target < 0) { pts.Add("T7 przypiecia pominiete (brak choragwi testowych)"); return true; }
            var pins = new Dictionary<string, int>(StringComparer.Ordinal) { [id] = target };
            // a) zapis ta sama droga co "Save" w oknie
            var r = Write(slots, pins);
            Log.Info(WriteLine("test T7", r));
            int at;
            bool a = r.Ok && !r.AsReset && r.PinCommitted && r.PinTypes == 1 && _pins.TryGetValue(id, out at) && at == target && r.PinMen == pick.Fit && r.ReadField && r.ReadArmy;
            bool sums = true;
            if (r.Ok && !r.AsReset)
                for (int k = 1; k <= 4; k++)
                {
                    int sum = 0, nb = 0;
                    for (int i = 0; i < Slots; i++) if (r.Banners[i] != null && r.Banners[i].Cls == k) { sum += r.W1[i]; nb++; }
                    if (nb > 0 && sum != 100) sums = false;
                }
            var sf = ShareFit(r.Banners, _pins);
            var all = CommandedFit();
            bool share = sf[kind - 1] == Math.Max(0, all[kind - 1] - pick.Fit);
            // b) zapis arm_army_review tam i z powrotem
            string e1 = Export();
            Import(e1);
            string e2 = Export();
            bool b = e1 != null && e1 == e2 && e1.Contains("|p~" + id + "~" + target) && _pins.Count == 1 && _pins.TryGetValue(id, out at) && at == target;
            // c) podzial spawnu na danych testowych
            string cInfo;
            bool c = TestSplit(pick.Ch, target, out cInfo);
            // d) odpiecie (wpis zdjety w kopii, Save)
            var r2 = Write(slots, new Dictionary<string, int>(StringComparer.Ordinal));
            bool d = r2.Ok && r2.PinCommitted && _pins.Count == 0;
            // e) zwiniecie choragwi przypiecia: przypiecie odpada (chyba ze w to miejsce weszla choragiew dopisana dla brakujacego rodzaju)
            var slots2 = TestSlots(cnt, comp);
            slots2[target] = null;
            var r3 = Write(slots2, pins);
            bool e = r3.Ok && (r3.AsReset ? _pins.Count == 0 : (r3.Banners[target] == null ? _pins.Count == 0 && r3.PinDropped == 1 : _pins.Count == 1));
            pts.Add("T7 przypiecia: typ " + id + " (" + KindName(kind) + ", zdrowych " + pick.Fit + ") -> choragiew " + Roman[target] + " (" + KindName(slots[target].Cls)
                    + ") | zapis " + T(a) + ", wagi klasy 100 " + T(sums) + ", udzial bez przypietych " + T(share) + " (" + sf[kind - 1] + " z " + all[kind - 1] + ")"
                    + " | arm_army_review tam i z powrotem " + T(b) + " | podzial spawnu " + T(c) + " (" + cInfo + ") | odpiecie " + T(d) + ", zwiniecie choragwi " + T(e));
            // g) recenzja P12: jedyny typ rodzaju przypiety do choragwi swojego rodzaju, liczba klasy ekranu i naprawa wag po bitwie (przed stanem koncowym)
            bool g;
            try { g = TestPinsOwnKind(cnt, comp, pts); }
            catch (Exception ex) { g = false; pts.Add("T7g BLAD " + ex.GetType().Name); Stumble("Test.T7g", ex); }
            // f) stan koncowy: choragwie testowe z przypieciem (bitwa autotestu)
            var r4 = Write(TestSlots(cnt, comp), pins);
            bool f = r4.Ok && !r4.AsReset && _pins.Count == 1 && r4.ReadField && r4.ReadArmy;
            pts.Add("T7 stan koncowy z przypieciem " + T(f) + " (" + id + " -> " + Roman[target] + ")");
            return a && sums && share && b && c && d && e && g && f;
        }

        /// <summary>
        /// T7g (recenzja P12): jedyny typ swojego rodzaju (cala liczba rodzaju partii, ktorymi gracz dowodzi) przypiety do choragwi SWOJEGO rodzaju -
        /// zapis (waga choragwi 1..100 mimo udzialu 0), liczba klasy ekranu przy samych przypietych zostaje (CountAfterPins - bez 0/0), naprawa wag po bitwie
        /// (RepairWeights - ta sama funkcja co koniec misji) na zapisie popsutym jak przez ekran: smiec (int.MinValue) i zero na choragwi przypiecia wracaja,
        /// zero zwyklej choragwi zostaje; bez naprawy okno (ReadSlots) pomija choragiew - z naprawa ja widzi.
        /// </summary>
        private static bool TestPinsOwnKind(int[] cnt, Hero comp, List<string> pts)
        {
            var all = CommandedFit();
            var types = PartyTypes();
            TypeRow pick = null;
            int kind = 0;
            for (int k = 0; k < 4 && pick == null; k++)
            {
                TypeRow only = null;
                int n = 0;
                foreach (var t in types) if (KindOf(t.Ch) == k && t.Fit > 0) { only = t; n++; }
                if (n == 1 && all[k] == only.Fit) { pick = only; kind = k + 1; }
            }
            if (pick == null) { pts.Add("T7g jedyny typ rodzaju - pominiety (kazdy rodzaj ma kilka typow albo ludzi innych partii)"); return true; }
            var slots = TestSlots(cnt, comp);
            int target = -1;
            for (int i = 0; i < Slots && target < 0; i++) if (slots[i] != null && slots[i].Cls == kind) target = i;
            if (target < 0) { pts.Add("T7g jedyny typ rodzaju - pominiety (brak choragwi rodzaju " + KindName(kind) + ")"); return true; }
            string id = pick.Ch.StringId;
            var r = Write(slots, new Dictionary<string, int>(StringComparer.Ordinal) { [id] = target });
            Log.Info(WriteLine("test T7g", r));
            int at;
            bool a = r.Ok && !r.AsReset && r.PinTypes == 1 && _pins.TryGetValue(id, out at) && at == target && WOk(r.W1[target])
                     && ShareFit(r.Banners, _pins)[kind - 1] == 0 && r.ReadField && r.ReadArmy;
            // ekran: sami przypieci tej klasy w formacji - liczba zostaje (stara zasada dawala 0, a przy jednej choragwi rodzaju 0/0); obok wolnych - odjeci
            bool scr = CountAfterPins(pick.Fit, pick.Fit) == pick.Fit && CountAfterPins(10, 4) == 6 && CountAfterPins(5, 0) == 5 && CountAfterPins(0, 0) == 0;
            // naprawa wag: kopia "sprzed bitwy" z listy, ktorej uzylaby bitwa (pole albo armia), zapis popsuty jak przez SaveConfiguration
            bool army = MobileParty.MainParty.Army != null;
            var beh = Behavior();
            var pc = new int[Slots]; var p1 = new int[Slots]; var p2 = new int[Slots];
            int pe = 0;
            for (int i = 0; i < Slots; i++)
            {
                var d = beh.GetFormationDataAtIndex(i, false, army);
                if (d == null) continue;
                pe++; pc[i] = (int)d.FormationClass; p1[i] = d.PrimaryClassWeight; p2[i] = d.SecondaryClassWeight;
            }
            int other = -1;
            for (int i = 0; i < Slots && other < 0; i++) if (i != target && r.Banners != null && r.Banners[i] != null) other = i;
            var targets = new HashSet<int> { target };
            var note = new StringBuilder();
            bool saved; int entries;
            // 1) smiec na choragwi przypiecia, zero na zwyklej: bez naprawy okno pomija choragiew przypiecia; naprawa - wraca tylko ona
            Spoil(beh, army, target, int.MinValue, other, 0);
            var cur = ReadSlots(out saved, out entries);
            bool skipped = cur != null && cur[target] == null;
            int n1 = RepairWeights(army, pe, pc, p1, p2, targets, note);
            cur = ReadSlots(out saved, out entries);
            bool back = n1 == 1 && cur != null && cur[target] != null && cur[target].W1 == p1[target] && (other < 0 || cur[other] == null);
            // 2) zero na choragwi przypiecia - wraca (przypieci stoja w niej ponad udzialem)
            Spoil(beh, army, target, 0, -1, 0);
            int n2 = RepairWeights(army, pe, pc, p1, p2, targets, note);
            cur = ReadSlots(out saved, out entries);
            bool zero = n2 == 1 && cur != null && cur[target] != null && cur[target].W1 == p1[target];
            // 3) zapis bez zmian - nic do naprawy
            int n3 = RepairWeights(army, pe, pc, p1, p2, targets, null);
            bool none = n3 == 0;
            pts.Add("T7g jedyny typ rodzaju: " + id + " (" + KindName(kind) + ", zdrowych " + pick.Fit + ") -> choragiew " + Roman[target] + " swojego rodzaju (waga " + r.W1[target]
                    + ") | zapis " + T(a) + " | ekran: liczba klasy przy samych przypietych zostaje " + T(scr)
                    + " | naprawa wag zapisu " + (army ? "armii" : "pola") + ": bez niej okno pomija choragiew " + T(skipped) + ", smiec wraca, zero zwyklej" + (other >= 0 ? " (" + Roman[other] + ")" : "")
                    + " zostaje " + T(back) + ", zero choragwi przypiecia wraca " + T(zero) + ", zapis zdrowy bez zmian " + T(none) + " (" + note + ")");
            return a && scr && skipped && back && zero && none;
        }

        /// <summary>Test: lista zapisu (pole albo armia) z podmienionymi wagami glownymi w dwoch indeksach (-1 - bez) - jak zapis ekranu z 0/0.</summary>
        private static void Spoil(OrderOfBattleCampaignBehavior beh, bool army, int i1, int w1, int i2, int w2)
        {
            var l = new List<OrderOfBattleCampaignBehavior.OrderOfBattleFormationData>();
            for (int i = 0; i < 64; i++)
            {
                var d = beh.GetFormationDataAtIndex(i, false, army);
                if (d == null) break;
                int w = i == i1 ? w1 : i == i2 ? w2 : d.PrimaryClassWeight;
                l.Add(new OrderOfBattleCampaignBehavior.OrderOfBattleFormationData(d.Captain, d.HeroTroops, d.FormationClass, w, d.SecondaryClassWeight,
                                                                                   d.Filters ?? new Dictionary<FormationFilterType, bool>()));
            }
            beh.SetFormationInfos(l, false, army);
        }

        /// <summary>Podzial spawnu (Split / Merge - te same funkcje co latka modelu) na prawdziwych PartyAgentOrigin bez misji: przypieci typu ch z druzyny gracza
        /// -> target, inny typ, bohater (gracz) i obcy tego typu (partia null) bez zmian; kazdy dokladnie raz; zamiana zamiast dubla.</summary>
        private static bool TestSplit(CharacterObject ch, int target, out string info)
        {
            var main = MobileParty.MainParty.Party;
            var plan = TestPlan(new Dictionary<string, int>(StringComparer.Ordinal) { [ch.StringId] = target });
            CharacterObject other = null;
            foreach (var t in PartyTypes()) if (t.Ch != ch) { other = t.Ch; break; }
            int seed = 970001;
            var origins = new List<IAgentOriginBase>();
            for (int i = 0; i < 4; i++)
            {
                if (other != null) origins.Add(new PartyAgentOrigin(main, other, 0, new UniqueTroopDescriptor(seed++)));
                origins.Add(new PartyAgentOrigin(main, ch, 0, new UniqueTroopDescriptor(seed++)));
            }
            var hero = new PartyAgentOrigin(main, Hero.MainHero.CharacterObject, 0, new UniqueTroopDescriptor(seed++));
            var stranger = new PartyAgentOrigin(null, ch, 0, new UniqueTroopDescriptor(seed++));
            origins.Add(hero);
            origins.Add(stranger);
            List<IAgentOriginBase> rest;
            List<(IAgentOriginBase origin, int formationIndex)> pinned;
            bool split = Split(plan, origins, out rest, out pinned);
            // model gry "na niby": reszta wedlug rodzaju (indeks = KindOf)
            var res = new List<(IAgentOriginBase origin, int formationIndex)>();
            if (rest != null) foreach (var o in rest) res.Add((o, KindOf((CharacterObject)o.Troop)));
            int rep1 = split ? Merge(res, pinned) : -1;
            bool ok = split && rep1 == 0 && res.Count == origins.Count && pinned.Count == 4;
            var seen = new HashSet<IAgentOriginBase>();
            foreach (var x in res)
            {
                if (!seen.Add(x.origin)) ok = false;
                var t = (CharacterObject)x.origin.Troop;
                bool shouldPin = t == ch && ReferenceEquals(x.origin.BattleCombatant, main) && !t.IsHero;
                if (shouldPin ? x.formationIndex != target : x.formationIndex != KindOf(t)) ok = false;
            }
            foreach (var o in origins) if (!seen.Contains(o)) ok = false;
            // zamiana: wynik modelu juz zawiera przypietych (podstawienie listy nie weszlo) - bez dubla
            var res2 = new List<(IAgentOriginBase origin, int formationIndex)>();
            foreach (var o in origins) res2.Add((o, 0));
            int rep2 = split ? Merge(res2, pinned) : -1;
            bool swap = rep2 == 4 && res2.Count == origins.Count;
            foreach (var x in res2) if (((CharacterObject)x.origin.Troop) == ch && ReferenceEquals(x.origin.BattleCombatant, main) && x.formationIndex != target) swap = false;
            info = "ludzi " + origins.Count + ", przypietych " + (pinned != null ? pinned.Count : 0) + ", reszta " + (rest != null ? rest.Count : 0) + ", wynik " + res.Count
                   + ", zamiana bez dubla " + T(swap) + (other == null ? ", bez drugiego typu" : "");
            return ok && swap;
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
