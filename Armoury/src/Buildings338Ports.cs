using System;
using System.Reflection;
using System.Threading;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// 338 grupa 9 - ZAMKI BEZ DOBOWEGO TICKU BUDYNKOW BK (Port zamku - ryby; Castle Mines - rudy).
    /// BK wpina RunMines / RunStuds / RunMaterials / RunPorts tylko w DailyTickTownEvent (BKBuildingsBehavior.OnTownDailyTick), a gra odpala je wylacznie dla
    /// Town.AllTowns (CampaignPeriodicEventManager - zamki w osobnej liscie). Port zamku nie lowi nigdy, Castle Mines nie kopie nigdy - niezaleznie od KS.
    /// Naprawa: wlasny tick dobowy zamkow (Armoury OnDailyTick) wola TE SAME metody BK dla zamku z budynkiem - wiec wszystkie istniejace latki Armoury na nich
    /// dzialaja jak dla miast (A17 PortsPre: blokada KS i ramka ksiegi, zaplata kasy -> kiesa ludu PortsFin; BuildFunding MinesPrefix/MineRevenuePostfix: place
    /// gornikow do kiesy ludu; StartCalibration BkPort - nazwa w ksiedze). RunMaterials nie wolamy (BuildFunding i tak ja pomija), RunStuds - wlasna wersja (grupa 12).
    /// Straznik dubla: prefiks-licznik na BK OnTownDailyTick - gdy BK kiedys zacznie tykac zamki (wywolania z zamkiem &gt; 0), nasz tick sie nie wykonuje.
    /// Klucze: Buildings338Ports (porty zamkow, domyslnie TAK), Buildings338CastleMines (kopalnie zamkow, domyslnie NIE - audyt: "do pomiaru", decyzja na liczbach).
    /// Porty miast pod KS (blokada A17 PortsPre i plan KS ryb portow) - bez zmian w 338 (osobny projekt: plan ryb portow w StartCalibration.Plan i jedna nazwa w ksiedze).
    /// </summary>
    internal static class Buildings338Ports
    {
        internal const string PortId = "building_port", HarborId = "building_harbor", CastleMinesId = "building_castle_mines";
        private static Type _bkb;
        private static MethodInfo _runPorts, _runMines;
        private static bool _hooked;
        private static int _bkCastleCalls, _bkCastleCallsDay;
        private static int _portCastles, _portKs, _mineCastles, _ticks;
        private static long _fish;
        private static ItemObject _fishItem;

        internal static void SessionStart()
        {
            _bkCastleCalls = _bkCastleCallsDay = 0;
            _fishItem = null;
            _bkb = AccessTools.TypeByName("BannerKings.Behaviours.BKBuildingsBehavior");
            _runPorts = _bkb != null ? AccessTools.Method(_bkb, "RunPorts", new[] { typeof(Town) }) : null;
            _runMines = _bkb != null ? AccessTools.Method(_bkb, "RunMines", new[] { typeof(Town) }) : null;
            var tick = _bkb != null ? AccessTools.Method(_bkb, "OnTownDailyTick", new[] { typeof(Town) }) : null;
            if (!_hooked && tick != null)
            {
                Buildings338.H.Patch(tick, prefix: new HarmonyMethod(typeof(Buildings338Ports), nameof(TickPrefix)) { priority = Priority.First });
                _hooked = true;
            }
            Buildings338.Hook("zamki: BK RunPorts " + (_runPorts != null ? "jest" : "BRAK") + ", RunMines " + (_runMines != null ? "jest" : "BRAK") + ", licznik ticku BK " + (_hooked ? "wpiety" : "BRAK"));
        }

        public static void TickPrefix(Town __0) { if (__0 != null && __0.IsCastle) Interlocked.Increment(ref _bkCastleCalls); }

        /// <summary>Czy BK tyka zamki sam (od poprzedniej doby) - wtedy nasz tick zamkow (porty, kopalnie, stadniny) nie biegnie.</summary>
        internal static bool BkTicksCastles { get { return _bkCastleCallsDay > 0; } }

        internal static object Behavior()
        {
            if (_bkb == null || Campaign.Current == null) return null;
            var mi = typeof(Campaign).GetMethod("GetCampaignBehavior");
            return mi != null ? mi.MakeGenericMethod(_bkb).Invoke(Campaign.Current, null) : null;
        }

        /// <summary>Z Buildings338.Daily: tick dobowy zamkow z Portem / Castle Mines - te same metody BK co dla miast.</summary>
        internal static void CastleTick()
        {
            _bkCastleCallsDay = Interlocked.Exchange(ref _bkCastleCalls, 0);
            _portCastles = _portKs = _mineCastles = _ticks = 0; _fish = 0;
            if (BkTicksCastles || Campaign.Current == null) return;
            bool ports = Buildings338.PortsOn && _runPorts != null, mines = Buildings338.CastleMinesOn && _runMines != null;
            if (!ports && !mines) return;
            var beh = Behavior();
            if (beh == null) return;
            if (_fishItem == null) _fishItem = MBObjectManager.Instance.GetObject<ItemObject>("fish");
            foreach (var t in Town.AllCastles)
            {
                if (t == null || t.Settlement == null) continue;
                bool any = false;
                try
                {
                    if (ports && (Buildings338.Level(t, PortId) > 0 || Buildings338.Level(t, HarborId) > 0))
                    {
                        any = true; _portCastles++;
                        if (StartCalibration.Applied && !StartCalibration.LandBackOf(t.Settlement)) _portKs++;   // A17 PortsPre zablokuje (pod KS porty czekaja na plan ryb)
                        var ro = t.Owner != null ? t.Owner.ItemRoster : null;
                        int f0 = ro != null && _fishItem != null ? ro.GetItemNumber(_fishItem) : 0;
                        _runPorts.Invoke(beh, new object[] { t });
                        int f1 = ro != null && _fishItem != null ? ro.GetItemNumber(_fishItem) : 0;
                        if (f1 > f0) _fish += f1 - f0;
                    }
                }
                catch (Exception e) { Buildings338.Stumble(Buildings338.GPorts, "Ports.RunPorts(" + t.Name + ")", e); }
                try
                {
                    if (mines && Buildings338.Level(t, CastleMinesId) > 0)
                    {
                        any = true; _mineCastles++;
                        _runMines.Invoke(beh, new object[] { t });
                    }
                }
                catch (Exception e) { Buildings338.Stumble(Buildings338.GPorts, "Ports.RunMines(" + t.Name + ")", e); }
                if (any) _ticks++;
            }
        }

        internal static void Daily(int day)
        {
            int withMines = 0;
            foreach (var t in Town.AllCastles) { try { if (Buildings338.Level(t, CastleMinesId) > 0) withMines++; } catch { } }
            Log.Info("Budynki (338) zamki: dzien " + day + " | tick BK dla zamkow " + _bkCastleCallsDay + " wywolan (0 = gra nie tyka zamkow), nasz tick " + _ticks + " zamkow"
                     + " | porty: klucz " + Buildings338.YN(Buildings338.PortsOn) + ", zamkow z Portem " + _portCastles + " (pod KS zablokowane " + _portKs + "), ryb +" + _fish
                     + " | kopalnie: klucz " + Buildings338.YN(Buildings338.CastleMinesOn) + ", zamkow z kopalnia " + withMines + ", wolanych " + _mineCastles + Buildings338.Tail(Buildings338.GPorts));
        }
    }
}
