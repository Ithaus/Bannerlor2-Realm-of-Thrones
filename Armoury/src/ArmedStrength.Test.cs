using System;
using System.Collections.Generic;
using System.IO;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// 268 - PROBA AUTOTESTU (tylko DLL autotestu CrashScribe - LogGate.Mode 1/2 - i plik Documents\Mount and Blade II Bannerlord\CrashScribe\armoury-zbroja268.flag;
    /// plik tylko czytany; w zwyklej grze nic nie robi). Raz na sesje, od 2. doby sesji (po pelnym przegladzie p9), z linii doby ArmedStrength.Daily:
    ///  T1 czysta regula LowRule - tabela prawdy (Lydden: gotowa, R 67, B 39 -&gt; niegotowa; histereza: niegotowa R 70 B 55 -&gt; dalej, R 70 B 60 -&gt; gotowa;
    ///     gotowa R 55 B 55 -&gt; gotowa; klucz NIE - B bez znaczenia);
    ///  T2 B przeliczone niezaleznie (MenOf z MenTemplate + SwapMath.Fit, bez FillPercent) dla do 20 partii lordow = B z przeliczenia p9 i z publikacji (Info.B);
    ///     przyczyna zgodna z B (B ponizej progu wejscia - bit zbroi);
    ///  T3 partia niegotowa przez zbroje: syntetyczna mysl (PartyThinkParams z GoToSettlement 0.4 i oblezeniem 0.6) - ArmsFirst.ThinkPostfix gasi oblezenie (-1),
    ///     wyjazd do miasta (GoToSettlement &gt;= 0.5) albo powod bez wyjazdu; brak takiej partii - POMINIETY;
    ///  T4 E3-Z1: dwa zamowienia korpusu (tier 1 i 2) tej samej partii tej samej doby przechodza, pierwsze zamowienie broni jednorecznej po nich tez (zbroja nie
    ///     zabiera doby), drugie broni jednorecznej - limit doby;
    ///  T5 ArmourOrders.Amount: (47, 60, 4795, 48) -&gt; 47; (47, 60, 500, 48) -&gt; 10; brak 0 -&gt; 0; kiesa 0 -&gt; 0;
    ///  T6 zapis: ExportWait -&gt; ImportWait partii niegotowej przez zbroje (wpis usuniety z pamieci jak po wczytaniu) - po przeliczeniu dalej niegotowa, ten sam zegar.
    /// Linie "Zbroja (268): test - Tn ... - wynik TAK|NIE|POMINIETY." (sprawdz_logi --grupa z268, Z268-7).
    /// </summary>
    internal static partial class ArmedStrength
    {
        internal const string AutoFlagName = "armoury-zbroja268.flag";
        private const string TP = "Zbroja (268): ";
        private const int TestFirstDay = 2;
        private static bool _testDone, _testFlag;
        private static int _testFlagDay = -1;

        private static void TestReset() { _testDone = false; _testFlag = false; _testFlagDay = -1; _sessDays = 0; }

        private static void TLine(string t, string what, string res) { Log.Info(TP + "test - " + t + " " + what + " - wynik " + res + "."); }

        private static bool TestWanted(int today)
        {
            int m = LogGate.Mode;
            if (m != 1 && m != 2) return false;
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

        private static void TestStep(int today)
        {
            if (_testDone || !TestWanted(today) || _sessDays < TestFirstDay) return;
            var mp = MobileParty.MainParty;
            if (mp != null && (mp.MapEvent != null || (PlayerEncounter.Current != null && PlayerEncounter.EncounterSettlement == null))) return;   // bitwa albo spotkanie - nastepna doba
            _testDone = true;
            var s = Settings.Current;
            if (s == null || !s.AiArmsBeforeBattle || !GarrisonKit.OwnKitOn) { TLine("T0", "bez proby (AI Arms Before Battle albo Troops Fight With Own Kit Only NIE)", "POMINIETY"); return; }
            TLine("T0", "start proby, dzien " + today + ", AI Arms Counts Body Armour " + (BodyOn(s) ? "TAK" : "NIE") + ", AI Armour Order Full " + (ArmourOrders.On ? "TAK" : "NIE"), "TAK");
            try { T1(); } catch (Exception e) { TLine("T1", "wyjatek " + e.GetType().Name, "NIE"); }
            try { T2(s); } catch (Exception e) { TLine("T2", "wyjatek " + e.GetType().Name, "NIE"); }
            try { T3(s); } catch (Exception e) { TLine("T3", "wyjatek " + e.GetType().Name, "NIE"); }
            try { T4(); } catch (Exception e) { TLine("T4", "wyjatek " + e.GetType().Name, "NIE"); }
            try { T5(); } catch (Exception e) { TLine("T5", "wyjatek " + e.GetType().Name, "NIE"); }
            try { T6(s); } catch (Exception e) { TLine("T6", "wyjatek " + e.GetType().Name, "NIE"); }
        }

        private static void T1()
        {
            var bad = new List<string>();
            // (prev, R, B, body) -> oczekiwane; progi 50 / 60 (domyslne p9 - liczby z modelu, nie z MCM gracza)
            var cases = new[]
            {
                new object[] { false, 67, 39, true, true, "Lydden" },
                new object[] { true, 70, 55, true, true, "histereza: dalej niegotowa" },
                new object[] { true, 70, 60, true, false, "histereza: gotowa od 60" },
                new object[] { false, 55, 55, true, false, "gotowa zostaje przy 55" },
                new object[] { false, 67, 39, false, false, "klucz NIE - tylko R" },
                new object[] { false, 45, 90, true, true, "sloty ponizej 50" },
            };
            foreach (var c in cases)
            {
                bool got = LowRule((bool)c[0], (int)c[1], (int)c[2], 50, 60, (bool)c[3]);
                if (got != (bool)c[4]) bad.Add((string)c[5] + " " + got);
            }
            int cz = CauseOf(false, 67, 39, 50, 60, true), cs = CauseOf(false, 45, 90, 50, 60, true), cb = CauseOf(false, 40, 30, 50, 60, true);
            if (cz != CBody || cs != CSlots || cb != (CSlots | CBody)) bad.Add("przyczyna " + cz + "/" + cs + "/" + cb);
            TLine("T1", "regula LowRule i przyczyna: przypadkow " + (cases.Length + 1) + ", zle " + bad.Count + (bad.Count > 0 ? " (" + string.Join("; ", bad.ToArray()) + ")" : ""), bad.Count == 0 ? "TAK" : "NIE");
        }

        private static void T2(Settings s)
        {
            var all = AiGear.Armories();
            if (all == null) { TLine("T2", "zbrojownie DTE nieczytelne", "POMINIETY"); return; }
            int n = 0, bad = 0, pubBad = 0, causeBad = 0, low = 0, lowBody = 0;
            string ex = "";
            var list = new List<MobileParty>();
            foreach (var mp in MobileParty.AllLordParties) { if (mp != null && !mp.IsMainParty && Kind(mp) == KLord) list.Add(mp); if (list.Count >= 20) break; }
            foreach (var mp in list)
            {
                Compute(mp, KLord, s);
                Ent e;
                if (!_ent.TryGetValue(mp.Id, out e) || !e.Known) continue;
                Dictionary<ItemObject, int> arm;
                all.TryGetValue(mp.Id, out arm);
                var pieces = MenUpgrade.AiPieces(arm, ItemObject.ItemTypeEnum.BodyArmor);
                List<CharacterObject> troops; int[] men; SkillObject skill;
                QuartermasterLaw.MenOf(mp.MemberRoster, ItemObject.ItemTypeEnum.BodyArmor, pieces, out troops, out men, out skill, QuartermasterLaw.MenTemplate, true);
                int unfit = men.Length == 0 ? 0 : (pieces.Count > 0 ? SwapMath.Fit(men, troops.Count, QuartermasterLaw.MeetsFitOf(troops), pieces) : men.Length);
                int bi = BodyPercent(men.Length, men.Length - unfit);
                n++;
                if (bi != e.B || men.Length != e.BodyMen) { bad++; if (ex.Length < 200) ex += Nm(mp) + " B " + e.B + " vs " + bi + "; "; }
                if (e.Low) { low++; if ((e.Cause & CBody) != 0) lowBody++; }
                if (BodyOn(s) && e.Kind == KLord && e.B < Below(s) && (e.Cause & CBody) == 0) causeBad++;
            }
            Publish();
            foreach (var mp in list)
            {
                Ent e; var i = InfoOf(mp);
                if (i == null || !_ent.TryGetValue(mp.Id, out e)) continue;
                if (i.B != e.B || i.Cause != e.Cause || i.Low != e.Low) pubBad++;
            }
            if (n == 0) { TLine("T2", "brak partii lordow ze zbrojownia", "POMINIETY"); return; }
            TLine("T2", "B niezaleznie (MenOf + SwapMath.Fit) dla " + n + " partii: zgodne " + (n - bad) + ", rozne " + bad + (ex.Length > 0 ? " (" + ex.TrimEnd(' ', ';') + ")" : "")
                  + "; publikacja (Info.B, przyczyna, Low) rozna " + pubBad + "; B ponizej progu bez bitu zbroi " + causeBad + "; niegotowe " + low + ", w tym przez zbroje " + lowBody,
                  bad == 0 && pubBad == 0 && causeBad == 0 ? "TAK" : "NIE");
        }

        private static void T3(Settings s)
        {
            MobileParty pick = null;
            foreach (var e in _ent.Values)
            {
                var p = e.P;
                if (p == null || !p.IsActive || e.Kind != KLord || !e.Low || e.Free || (e.Cause & CBody) == 0) continue;
                if (p.Army != null || p.MapEvent != null || p.BesiegedSettlement != null || p.IsCurrentlyAtSea || p.Party.IsStarving) continue;
                if (p.CurrentSettlement != null && p.CurrentSettlement.SiegeEvent != null) continue;
                var i = InfoOf(p);
                if (i == null || !i.Low || !i.ByBody) continue;
                pick = p; break;
            }
            if (pick == null) { TLine("T3", "brak partii niegotowej przez zbroje poza armia, bitwa i oblezeniem", "POMINIETY"); return; }
            var pt = new PartyThinkParams(pick);
            Settlement town = null, fort = null; float bd = float.MaxValue, fd = float.MaxValue;
            foreach (var st in Settlement.All)
            {
                if (st == null) continue;
                float d = st.GetPosition2D.Distance(pick.GetPosition2D);
                if (st.IsTown && d < bd) { bd = d; town = st; }
                if (st.IsFortification && st.OwnerClan != pick.ActualClan && d < fd) { fd = d; fort = st; }
            }
            if (town == null || fort == null) { TLine("T3", "brak miasta albo cudzej twierdzy", "POMINIETY"); return; }
            var g = (new AIBehaviorData(town, AiBehavior.GoToSettlement, MobileParty.NavigationType.Default, false, false, false), 0.4f);
            pt.AddBehaviorScore(in g);
            var bs = (new AIBehaviorData(fort, AiBehavior.BesiegeSettlement, MobileParty.NavigationType.Default, false, false, false), 0.6f);
            pt.AddBehaviorScore(in bs);
            ArmsFirst.ThinkPostfix(pick, pt);
            float siege = 0f, go = -2f; string goTo = "-";
            foreach (var e in pt.AIBehaviorScores)
            {
                if (e.Item1.AiBehavior == AiBehavior.BesiegeSettlement && ReferenceEquals(e.Item1.Party, fort)) siege = e.Item2;
                if (e.Item1.AiBehavior == AiBehavior.GoToSettlement && e.Item2 > go) { var ts = e.Item1.Party as Settlement; if (ts != null && ts.IsTown) { go = e.Item2; goTo = ts.Name.ToString(); } }
            }
            var info = InfoOf(pick);
            int b, need; bool can = ArmsFirst.CanAfford(pick, info != null ? info.Short : 0, null, out b, out need, true);
            bool trip = go >= ArmsFirst.FloorScore;
            TLine("T3", Nm(pick) + " (R " + (info != null ? info.R : -1) + "%, B " + (info != null ? info.B : -1) + "%): oblezenie " + siege.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)
                  + ", wyjazd " + (trip ? "do " + goTo + " (" + go.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + ")" : "brak (stac " + (can ? "TAK" : "NIE") + ", budzet " + b + " z " + need + ", cel p9 " + ArmsFirst.TargetName(pick) + ")"),
                  siege <= -1f && (trip || !can || ArmsFirst.TargetName(pick) == "-") ? "TAK" : "NIE");
        }

        private static void T4()
        {
            if (!MenUpgradeOrders.On || !ArmourOrders.On) { TLine("T4", "Men Upgrade Orders albo AI Armour Order Full NIE", "POMINIETY"); return; }
            MobileParty pick = null; Settlement town = null;
            foreach (var mp in MobileParty.AllLordParties) { if (mp != null && mp.IsActive && !mp.IsMainParty && mp.LeaderHero != null) { pick = mp; break; } }
            if (pick != null)
            {
                float bd = float.MaxValue;
                foreach (var tw in Town.AllTowns) { var st = tw != null ? tw.Settlement : null; if (st == null) continue; float d = st.GetPosition2D.Distance(pick.GetPosition2D); if (d < bd) { bd = d; town = st; } }
            }
            if (pick == null || town == null) { TLine("T4", "brak partii lorda albo miasta", "POMINIETY"); return; }
            MenUpgradeOrders.TestForget(pick);
            int a = MenUpgradeOrders.Note(pick, town, null, ItemObject.ItemTypeEnum.BodyArmor, 1, 3, 2);
            int b = MenUpgradeOrders.Note(pick, town, null, ItemObject.ItemTypeEnum.BodyArmor, 2, 4, 2);
            int c = MenUpgradeOrders.Note(pick, town, null, ItemObject.ItemTypeEnum.OneHandedWeapon, 1, 3, 2);
            int d2 = MenUpgradeOrders.Note(pick, town, null, ItemObject.ItemTypeEnum.OneHandedWeapon, 2, 4, 2);
            bool ok = a == MenUpgradeOrders.ROrdered && b == MenUpgradeOrders.ROrdered && c == MenUpgradeOrders.ROrdered && d2 == MenUpgradeOrders.RPartyCap;
            TLine("T4", Nm(pick) + " w " + town.Name + ": korpus t2 " + a + ", korpus t3 " + b + ", bron jednoreczna t2 " + c + ", druga bron jednoreczna t3 " + d2
                  + " (0 zamowione, 5 limit doby, 6 juz zamowione)", ok ? "TAK" : "NIE");
        }

        private static void T5()
        {
            int a = ArmourOrders.Amount(47, 60f, 4795, 48), b = ArmourOrders.Amount(47, 60f, 500, 48), c = ArmourOrders.Amount(0, 60f, 4795, 48), d = ArmourOrders.Amount(47, 60f, 0, 48);
            bool ok = a == 47 && b == 10 && c == 0 && d == 0;
            TLine("T5", "Amount(47, 60, 4795, 48) " + a + " (47), Amount(47, 60, 500, 48) " + b + " (10), brak 0 -> " + c + ", kiesa 0 -> " + d, ok ? "TAK" : "NIE");
        }

        private static void T6(Settings s)
        {
            if (_savedWait.Count > 0) { TLine("T6", "zegary z zapisu jeszcze nieprzeliczone (" + _savedWait.Count + ")", "POMINIETY"); return; }
            Ent pick = null;
            foreach (var e in _ent.Values)
                if (e.P != null && e.P.IsActive && e.Kind == KLord && e.Low && e.Cause == CBody && e.LowSinceH >= 0 && e.P.Army == null && !string.IsNullOrEmpty(e.P.StringId)) { pick = e; break; }
            if (pick == null) { TLine("T6", "brak partii niegotowej tylko przez zbroje (z zegarem)", "POMINIETY"); return; }
            var mp = pick.P;
            double lowH = pick.LowSinceH; bool free = pick.Free;
            string exp = ExportWait();
            bool inExp = exp.Contains(mp.StringId + ",");
            ImportWait(exp);
            _ent.Remove(mp.Id);   // jak po wczytaniu: partii nie ma w pamieci, stan z zapisu czeka na pierwsze przeliczenie
            Compute(mp, KLord, s);
            Ent e2;
            bool got = _ent.TryGetValue(mp.Id, out e2);
            bool ok = inExp && got && e2.Low && Math.Abs(e2.LowSinceH - lowH) < 0.01 && e2.Free == free && !_savedWait.ContainsKey(mp.StringId);
            _savedWait.Clear(); _waitLoaded = 0;
            Publish();
            TLine("T6", Nm(mp) + ": w zapisie " + (inExp ? "TAK" : "NIE") + ", po przeliczeniu niegotowa " + (got && e2.Low ? "TAK" : "NIE") + " (przyczyna " + (got ? CauseText(e2.Cause) : "-")
                  + "), zegar " + (got ? (e2.LowSinceH - lowH).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) : "-") + " h roznicy", ok ? "TAK" : "NIE");
        }
    }
}
