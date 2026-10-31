using System;
using System.Globalization;
using System.IO;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ScreenSystem;

namespace Armoury
{
    /// <summary>
    /// AUTOTEST ZAKLADKI FIEFS (m112-inwest, Jeff 24.10: "ma byc to pokazane w Fiefs, zeby widzial, gdzie mam majatek i inwestycje") - tylko pod plikiem
    /// CrashScribe\armoury-fiefs-test.flag i DLL autotestu (LogGate 1/2); w zwyklej grze nic nie robi. Na mapie, po 60 s czasu mapy od wczytania (prace okregu
    /// wczytuja sie w pierwszej dobie), otwiera ekran rodu, wybiera zakladke Fiefs (ClanManagementVM.SetSelectedCategory(2) odbiciem), robi zdjecie silnika
    /// i zamyka ekran. Linie "Fiefs (test m112): ...".
    /// </summary>
    internal static class FiefsShotTest
    {
        private const string P = "Fiefs (test m112): ";
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static int _want = -1, _stage;
        private static float _clock, _acc, _at, _ready;
        private static bool _done;

        private static string Dir()
        {
            var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            return Path.Combine(Path.Combine(docs, "Mount and Blade II Bannerlord"), "CrashScribe");
        }

        private static bool Wanted()
        {
            if (_want >= 0) return _want == 1;
            _want = 0;
            int m = LogGate.Mode;
            if (m != 1 && m != 2) return false;
            try { if (File.Exists(Path.Combine(Dir(), "armoury-fiefs-test.flag"))) _want = 1; } catch { }
            return _want == 1;
        }

        /// <summary>SubModuleMain.OnApplicationTick: co 0,5 s; bez pliku flagi - od razu wyjscie.</summary>
        internal static void AppTick(float dt)
        {
            if (_done || !Wanted()) return;
            _clock += dt;
            _acc += dt;
            if (_acc < 0.5f) return;
            float step = _acc;
            _acc = 0f;
            try { Step(step); }
            catch (Exception e)
            {
                Log.Error("FiefsShotTest.Step", e);
                try { if (Game.Current != null && Game.Current.GameStateManager.ActiveState is ClanState) Game.Current.GameStateManager.PopState(); } catch { }
                Log.Info(P + "przerwany wyjatkiem " + e.GetType().Name + ".");
                _done = true;
            }
        }

        private static void Step(float step)
        {
            var gsm = Game.Current != null ? Game.Current.GameStateManager : null;
            if (_stage == 0)
            {
                if (Campaign.Current == null || gsm == null || Mission.Current != null || !(gsm.ActiveState is MapState) || InformationManager.IsAnyInquiryActive()) { return; }
                _ready += step;
                if (_ready < 60f) return;
                gsm.PushState(gsm.CreateState<ClanState>());
                _stage = 1; _at = _clock + 3f;
                Log.Info(P + "ekran rodu otwarty.");
                return;
            }
            if (_clock < _at) return;
            if (_stage == 1)
            {
                var scr = ScreenManager.TopScreen;
                var pi = scr != null ? scr.GetType().GetProperty("_dataSource") : null;
                var vm = pi != null ? pi.GetValue(scr, null) : null;
                var mi = vm != null ? vm.GetType().GetMethod("SetSelectedCategory", new[] { typeof(int) }) : null;
                if (mi != null) mi.Invoke(vm, new object[] { 2 });
                Log.Info(P + "zakladka Fiefs " + (mi != null ? "wybrana" : "NIE wybrana (ekran " + (scr != null ? scr.GetType().Name : "brak") + ")") + ".");
                _stage = 2; _at = _clock + 3f;
                return;
            }
            if (_stage == 2)
            {
                FinanceCheck();   // m115: panel Finance i podpowiedz zlota - linia prac gracza (sam odczyt, nie rzuca)
                string dir = Path.Combine(Dir(), "zdjecia");
                Directory.CreateDirectory(dir);
                string name = "fiefs-m115-" + DateTime.Now.ToString("HHmmss", Inv) + ".png";
                TaleWorlds.Engine.Utilities.TakeScreenshot(Path.Combine(dir, name).Replace('\\', '/'));
                Log.Info(P + "zdjecie zdjecia\\" + name + ".");
                _stage = 3; _at = _clock + 1f;
                return;
            }
            if (gsm != null && gsm.ActiveState is ClanState) gsm.PopState();
            Log.Info(P + "koniec.");
            _done = true;
        }

        private static int VmInt(object vm, string prop)
        {
            var pi = vm != null ? vm.GetType().GetProperty(prop) : null;
            object v = pi != null ? pi.GetValue(vm, null) : null;
            return v is int ? (int)v : int.MinValue;
        }

        /// <summary>m115 (Jeff 24.10: "skoro wioksa mi placi to ma byc na zielon w inocme i na drugim zdjeicuy"): panel Finance (ClanManagementVM po RefreshDailyValues
        /// odbiciem - ta sama droga co gra), podpowiedz zlota (CalculateClanGoldChange(rod, true, false, false) - jak GetDenarTooltip) i przychod poza panelem
        /// (bez flagi FinanceBox - bez linii prac). OK: Total Income - poza panelem = prace w widoku, linie "Village works"
        /// podpowiedzi sumuja sie do prac w widoku, a przy wyplatach ksiegi w 28 dobach (liczonych osobno, nie przez widok) linia prac jest; bez potkniec widoku
        /// i z wczytanymi arm_works. Linia "[FiefsShotTest] panel Finance: ...".</summary>
        private static void FinanceCheck()
        {
            try
            {
                var scr = ScreenManager.TopScreen;
                var pi = scr != null ? scr.GetType().GetProperty("_dataSource") : null;
                var vm = pi != null ? pi.GetValue(scr, null) : null;
                var rd = vm != null ? vm.GetType().GetMethod("RefreshDailyValues", Type.EmptyTypes) : null;
                if (rd != null) rd.Invoke(vm, null);
                int x = VmInt(vm, "TotalIncome"), y = VmInt(vm, "TotalExpenses"), z = VmInt(vm, "DailyChange"), ex = VmInt(vm, "ExpectedGold");
                var s = Settings.Current;
                bool on = s != null && s.WorksIncomeShown;
                int st0 = DistrictWorks._vwStumble;
                System.Collections.Generic.List<DistrictWorks.ViewLine> vl; int days;
                int w = DistrictWorks.ViewWorks(out vl, out days);
                int wExp = on ? w : 0;
                // po recenzji m115: oczekiwanie NIE przez ViewWorks - przy w = 0 porownania wyzej to 0 == 0 (blad widoku, zgubione linie, niewczytane arm_works
                // dawaly OK). Wyplaty ksiegi w 28 dobach / max(7, doby historii), suma zaokraglona raz (jak widok); > 0 przy kluczu - linia prac musi byc
                double expRaw = 0;
                foreach (var pw in DistrictWorks.PlayerWorks())
                    if (pw != null && pw.WarWith == null && pw.Paused == null && pw.Eff > 1e-6 && pw.Paid28 > 0) expRaw += pw.Paid28 / DistrictWorks.ViewDays(pw);   // 305e: ten sam dzielnik co widok (od zakupu przy WorksPaidSinceBuy)
                long expLed = DistrictWorks.ViewRound(expRaw);                                                      // 305e: to samo zaokraglenie co widok
                bool pend = DistrictWorks.ImportPending;
                var per = new System.Text.StringBuilder();
                if (vl != null) foreach (var l in vl) per.Append(per.Length > 0 ? ", " : "").Append(l.Name).Append(' ').Append(l.Amt);
                var fin = Campaign.Current.Models.ClanFinanceModel;
                var tip = fin.CalculateClanGoldChange(Clan.PlayerClan, true, false, false);
                var tl = new System.Text.StringBuilder();
                long tipWorks = 0; int tipWorksN = 0;
                foreach (var ln in tip.GetLines())
                {
                    string n = ln.name ?? "?";
                    tl.Append(n).Append('=').Append(ln.number.ToString("0.##", Inv)).Append("; ");
                    if (n.StartsWith("Village works", StringComparison.Ordinal)) { tipWorks += (long)Math.Round(ln.number); tipWorksN++; }
                }
                int o = (int)fin.CalculateClanIncome(Clan.PlayerClan, false, false, false).ResultNumber;
                bool okBox = x != int.MinValue && x - o == wExp;
                // po recenzji m115: bez sprawdzenia Daily Change = |Income| - |Expenses| - RefreshDailyValues liczy go wlasnie tak, zawsze prawda
                bool okTip = tipWorks == wExp && (wExp == 0 || tipWorksN > 0);
                bool okLed = !on || expLed <= 0 || (w > 0 && x - o == w && tipWorksN > 0);
                bool okSt = DistrictWorks._vwStumble == st0;
                bool ok = rd != null && okBox && okTip && okLed && okSt && !pend;
                Log.Info("[FiefsShotTest] panel Finance: Total Income " + x + ", Total Expenses " + y + ", Daily Change " + z + ", Expected " + ex
                         + " | prace w widoku " + w + " (" + (per.Length > 0 ? per.ToString() : "brak") + "; dni " + days + "; klucz Works Income Shown " + (on ? "TAK" : "WYL") + ")"
                         + " | z ksiegi " + expLed + "/dobe"
                         + " | podpowiedz zlota: " + tl + "razem " + tip.ResultNumber.ToString("0.##", Inv) + " (linie prac " + tipWorksN + " = " + tipWorks + ")"
                         + " | przychod poza panelem " + o + " -> " + (ok ? "OK" : "BLAD (" + (rd == null ? "brak RefreshDailyValues; " : "") + (okBox ? "" : "panel - poza panelem != prace; ")
                         + (okTip ? "" : "podpowiedz != prace; ") + (okLed ? "" : "brak linii prac przy wyplatach w 28 dobach; ")
                         + (okSt ? "" : "potkniecie widoku w czasie sprawdzenia; ") + (pend ? "arm_works z zapisu jeszcze niewczytane - nic nie sprawdzone; " : "")
                         + "panel Finance " + FinanceBox.State + ")"));
            }
            catch (Exception e) { Log.Error("FiefsShotTest.FinanceCheck", e); }
        }
    }
}
