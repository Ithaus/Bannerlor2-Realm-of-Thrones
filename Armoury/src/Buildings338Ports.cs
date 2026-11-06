using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// 338 grupa 9 - PORTY BK (ryby) I KOPALNIE ZAMKOW (rudy).
    /// Zamki bez dobowego ticku budynkow BK: BK wpina RunMines / RunStuds / RunMaterials / RunPorts tylko w DailyTickTownEvent (BKBuildingsBehavior.OnTownDailyTick),
    /// a gra odpala je wylacznie dla Town.AllTowns (zamki w osobnej liscie). Naprawa: wlasny tick dobowy zamkow (Armoury OnDailyTick).
    ///  - PORT zamku: wolamy BK RunPorts dla zamku z Portem/Harbor (poziom &gt; 0) - wzor BK z pracy ludnosci (poziom x max(dzierzawcy/300, rzemieslnicy/200)), wszystkie
    ///    latki Armoury na tej metodzie dzialaja jak dla miast (A17 PortsPre/PortsFin: zaplata z kasy do kiesy ludu - place rybakow, ramka ksiegi; StartCalibration BkPort).
    ///  - 338c PORTY POD KS (miasta i zamki): dotad A17 PortsPre blokowal port w krainie pod KS ("ryby z niczego za kase w nicosc"), choc ryby portu to praca ludzi
    ///    za zloto kasy. Teraz (KsOpen = Buildings338Ports) port lowi takze pod KS: zaplata zawsze do kiesy ludu (A17 PortsFin), ksiega - wlasna grupa LabKs (nie
    ///    "ryby z niczego", nie lodzie Planu), Plan KS liczy te podaz (StartCalibration.Plan.PortFishExo -&gt; PlanFishPerDay - ten sam wzor i warunek kasy).
    ///  - 338c KOPALNIE ZAMKOW (Castle Mines, klucz Buildings338CastleMines, domyslnie TAK): NIE BK RunMines (tam kasa placi w nicosc, a gdy zamku nie stac - ruda do
    ///    Stash za darmo). Wlasna produkcja z pracy jak wies: losowanie BK z opisu ("zloza i bogactwo gruntu": udzial zloza / (bogactwo + 0.5), poziom = sztuki), ale
    ///    tylko z wolnych rak zamku (BK LandData.WorkforceExcess - ludzie ponad prace pol i pastwisk), rece na sztuke = kg sztuki x sigma KS / wydajnosc gornika z
    ///    tabeli KS (KsTab.P0: zelazo 2, srebro 15, sol 60, glina 800, wapien 300, marmur 150, zloto 0.3 kg/prac./d). Urobek: gdy kasa zamku stac na cene targu
    ///    (jak BK) - kasa placi gornikom (TownFolk.Earn KMine - kiesa podzamcza; czego kiesa nie przyjmie, wraca do kasy), ruda na targ zamku; inaczej - do Stash
    ///    zamku (opis: "stored in Stash otherwise"), bez zlota. Ksiega towarow - ramka FFish z wlasna grupa (LabMines). Bez zapisu w grze.
    /// Straznik dubla: prefiks-licznik na BK OnTownDailyTick - gdy BK kiedys zacznie tykac zamki (wywolania z zamkiem &gt; 0), nasz tick sie nie wykonuje.
    /// </summary>
    internal static class Buildings338Ports
    {
        internal const string PortId = "building_port", HarborId = "building_harbor", CastleMinesId = "building_castle_mines";
        internal const string LabKs = "porty BK - praca rybakow (338)|";
        internal const string LabMines = "kopalnie zamkow BK - praca gornikow (338)|";
        private static Type _bkb;
        private static MethodInfo _runPorts;
        private static bool _hooked;
        private static int _bkCastleCalls, _bkCastleCallsDay;
        // doba
        private static int _portCastles, _portListed, _portKsCastles, _ticks, _mineCastles, _mineOut, _mineNoHands;
        private static long _fish, _oreMarket, _oreStash, _oreWage;
        private static long _ksFish; private static int _ksFishN;      // porty pod KS (miasta i zamki) - z A17 PortsFin (NoteKsFish)
        private static double _planFish; private static int _planN;    // ostatni Plan KS: ryby portow szt./d (PortFishExo)
        // bieg (sesja)
        private static long _sumTicks, _sumFish, _sumOre, _sumKsFish, _sumWage; private static int _sumDays;
        private static ItemObject _fishItem;

        /// <summary>Porty BK pod KS (miasta i zamki): lowia z pracy rybakow (A17 PortsPre nie blokuje).</summary>
        internal static bool KsOpen { get { return Buildings338.PortsOn; } }

        internal static void SessionStart()
        {
            _bkCastleCalls = _bkCastleCallsDay = 0;
            _fishItem = null; _getBeh = null;
            _sumTicks = _sumFish = _sumOre = _sumKsFish = _sumWage = 0; _sumDays = 0;
            _ksFish = 0; _ksFishN = 0; _planFish = 0; _planN = 0;
            _bkb = AccessTools.TypeByName("BannerKings.Behaviours.BKBuildingsBehavior");
            _runPorts = _bkb != null ? AccessTools.Method(_bkb, "RunPorts", new[] { typeof(Town) }) : null;
            var tick = _bkb != null ? AccessTools.Method(_bkb, "OnTownDailyTick", new[] { typeof(Town) }) : null;
            if (!_hooked && tick != null)
            {
                Buildings338.H.Patch(tick, prefix: new HarmonyMethod(typeof(Buildings338Ports), nameof(TickPrefix)) { priority = Priority.First });
                _hooked = true;
            }
            Buildings338.Hook("zamki: BK RunPorts " + (_runPorts != null ? "jest" : "BRAK") + ", kopalnie zamkow - wlasna produkcja z pracy (338c), licznik ticku BK " + (_hooked ? "wpiety" : "BRAK"));
        }

        public static void TickPrefix(Town __0) { if (__0 != null && __0.IsCastle) Interlocked.Increment(ref _bkCastleCalls); }

        /// <summary>Czy BK tyka zamki sam (od poprzedniej doby) - wtedy nasz tick zamkow (porty, kopalnie, stadniny) nie biegnie.</summary>
        internal static bool BkTicksCastles { get { return _bkCastleCallsDay > 0; } }

        private static MethodInfo _getBeh;   // recenzja 338: GetCampaignBehavior<BKBuildingsBehavior> raz na sesje (nie MakeGenericMethod co dobe)
        internal static object Behavior()
        {
            if (_bkb == null || Campaign.Current == null) return null;
            if (_getBeh == null) { var mi = typeof(Campaign).GetMethod("GetCampaignBehavior"); _getBeh = mi != null ? mi.MakeGenericMethod(_bkb) : null; }
            return _getBeh != null ? _getBeh.Invoke(Campaign.Current, null) : null;
        }

        private static int PortLevel(Town t) { return Math.Max(Buildings338.Level(t, PortId), Buildings338.Level(t, HarborId)); }

        /// <summary>A17 PortsFin: ryby portu pod KS (miasto albo zamek) - do linii doby.</summary>
        internal static void NoteKsFish(int n) { if (n > 0) { Interlocked.Add(ref _ksFish, n); Interlocked.Increment(ref _ksFishN); } }

        /// <summary>StartCalibration.Plan (PortFishExo): ryby portu BK osady na dobe - wzor BK RunPorts (pierwszy z Harbor/Port, poziom x max(dzierzawcy/300,
        /// rzemieslnicy/200), tylko gdy kasa stac na cene targu) - ten sam, ktorym port lowi w ticku. 0 - brak portu, klucz wyl., kraina wrocona (tam port jak dzis -
        /// Plan krainy wroconej nie liczy) albo brak danych BK.</summary>
        internal static double PlanFishPerDay(Settlement st)
        {
            if (st == null || st.Town == null || !KsOpen) return 0.0;
            try
            {
                if (StartCalibration.LandBackOf(st)) return 0.0;
                var t = st.Town;
                Building b = null;
                foreach (var x in t.Buildings) if (x != null && x.BuildingType != null && (x.BuildingType.StringId == HarborId || x.BuildingType.StringId == PortId)) { b = x; break; }   // jak BK: FirstOrDefault
                if (b == null || b.CurrentLevel <= 0) return 0.0;
                var pop = Buildings338.PopData(st);
                if (pop == null) return 0.0;
                int n = (int)Math.Max(b.CurrentLevel * Buildings338.TypeCount(pop, "Tenants") / 300f, b.CurrentLevel * Buildings338.TypeCount(pop, "Craftsmen") / 200f);
                if (n <= 0) return 0.0;
                if (_fishItem == null) _fishItem = MBObjectManager.Instance.GetObject<ItemObject>("fish");
                if (_fishItem == null) return 0.0;
                int price = t.GetItemPrice(_fishItem, null, false);
                if (t.Gold < price * n) return 0.0;
                _planFish += n; _planN++;
                return n;
            }
            catch (Exception e) { Buildings338.Stumble(Buildings338.GPorts, "Ports.PlanFishPerDay", e); return 0.0; }
        }

        /// <summary>StartCalibration.Plan: poczatek skladania wejscia Planu - zeruje sume ryb portow do linii doby.</summary>
        internal static void PlanBegin() { _planFish = 0; _planN = 0; }

        // ------------------------------------------------------------ kopalnie zamkow (338c)
        private static readonly Dictionary<string, double> GornikKg = new Dictionary<string, double>(StringComparer.Ordinal)
        { { "iron", 2.0 }, { "silver", 15.0 }, { "salt", 60.0 }, { "clay", 800.0 }, { "limestone", 300.0 }, { "marble", 150.0 }, { "gold_ore", 0.3 } };   // KsTab.P0 (kopalnie, kamieniolomy)

        /// <summary>Rece na sztuke mineralu: kg sztuki x sigma KS / kg gornika na dobe (KsTab.P0); mineral spoza tabeli - wydajnosc rudy zelaza (najwolniejsza poza zlotem).</summary>
        private static double HandsPerUnit(ItemObject it)
        {
            double p0;
            if (it == null || !GornikKg.TryGetValue(it.StringId ?? "", out p0)) p0 = 2.0;
            double kg = it != null && it.Weight > 0.05f ? it.Weight : 10.0;
            return kg * Math.Max(1, DailyLife.KsSigma) / p0;
        }

        private static void MineCastle(Town t, int lvl)
        {
            var st = t.Settlement;
            var pop = Buildings338.PopData(st);
            if (pop == null) return;
            int rich;
            var mins = Buildings338.Minerals(pop, out rich);
            if (mins.Count == 0) return;
            int free = Buildings338.WorkforceExcess(pop);
            if (free == int.MinValue) return;
            double hands = Math.Max(0, free);
            var fr = GoodsLedger.Begin(GoodsLedger.FFish, st);
            if (fr != null) fr.Info = LabMines;
            try
            {
                bool any = false, noHands = false;
                foreach (var kv in mins)
                {
                    if (!(MBRandom.RandomFloat * (rich + 0.5f) < kv.Value)) continue;   // losowanie BK RunMines (opis: zloza i bogactwo gruntu)
                    double hpu = HandsPerUnit(kv.Key);
                    int n = lvl;                                                       // poziom = sztuki (BK)
                    if (hpu > 0) n = (int)Math.Min(n, Math.Floor(hands / hpu));
                    if (n <= 0) { noHands = true; continue; }
                    hands -= n * hpu;
                    int price = t.GetItemPrice(kv.Key, null, false);
                    long cost = (long)price * n;
                    if (cost > 0 && t.Gold >= cost && st.ItemRoster != null)
                    {
                        // kasa zamku placi gornikom (kiesa podzamcza; czego nie przyjmie - wraca do kasy) - zloto zostaje w swiecie
                        t.ChangeGold(-(int)cost);
                        int folk = Math.Max(0, Math.Min((int)cost, TownFolk.Earn(st, (int)cost, TownFolk.KMine)));
                        if ((int)cost - folk > 0) t.ChangeGold((int)cost - folk);
                        st.ItemRoster.AddToCounts(kv.Key, n);
                        _oreMarket += n; _oreWage += folk;
                    }
                    else if (st.Stash != null)
                    {
                        st.Stash.AddToCounts(kv.Key, n);   // opis: "stored in Stash otherwise" - urobek rak zamku, bez zlota
                        _oreStash += n;
                    }
                    else continue;
                    any = true;
                }
                if (any) _mineOut++;
                if (noHands) _mineNoHands++;
            }
            finally { if (fr != null) { try { GoodsLedger.End(fr); } catch { } } }
        }

        /// <summary>Z Buildings338.Daily: tick dobowy zamkow z Portem (BK RunPorts) i Castle Mines (wlasna produkcja z pracy).</summary>
        internal static void CastleTick()
        {
            _bkCastleCallsDay = Interlocked.Exchange(ref _bkCastleCalls, 0);
            _portCastles = _portListed = _portKsCastles = _mineCastles = _ticks = _mineOut = _mineNoHands = 0; _fish = _oreMarket = _oreStash = _oreWage = 0;
            if (BkTicksCastles || Campaign.Current == null) return;
            bool ports = Buildings338.PortsOn && _runPorts != null, mines = Buildings338.CastleMinesOn;
            object beh = ports ? Behavior() : null;
            if (_fishItem == null) _fishItem = MBObjectManager.Instance.GetObject<ItemObject>("fish");
            foreach (var t in Town.AllCastles)
            {
                if (t == null || t.Settlement == null) continue;
                bool any = false;
                try
                {
                    if (Buildings338.FindBuilding(t, PortId) != null || Buildings338.FindBuilding(t, HarborId) != null) _portListed++;
                    if (ports && beh != null && PortLevel(t) > 0)
                    {
                        any = true; _portCastles++;
                        if (StartCalibration.Applied && !StartCalibration.LandBackOf(t.Settlement)) _portKsCastles++;
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
                    int lvl = Buildings338.Level(t, CastleMinesId);
                    if (mines && lvl > 0) { any = true; _mineCastles++; MineCastle(t, lvl); }
                }
                catch (Exception e) { Buildings338.Stumble(Buildings338.GPorts, "Ports.Mines(" + t.Name + ")", e); }
                if (any) _ticks++;
            }
        }

        internal static void Daily(int day)
        {
            int withMines = 0;
            foreach (var t in Town.AllCastles) { try { if (Buildings338.Level(t, CastleMinesId) > 0) withMines++; } catch (Exception e) { Buildings338.Stumble(Buildings338.GPorts, "Ports.Daily", e); } }
            long ksFish = Interlocked.Exchange(ref _ksFish, 0); int ksN = Interlocked.Exchange(ref _ksFishN, 0);
            long ore = _oreMarket + _oreStash;
            _sumDays++; _sumTicks += _ticks; _sumFish += _fish; _sumOre += ore; _sumKsFish += ksFish; _sumWage += _oreWage;
            var inv = Buildings338.Inv;
            Log.Info("Budynki (338) zamki: dzien " + day + " | tick BK dla zamkow " + _bkCastleCallsDay + " wywolan (0 = gra nie tyka zamkow) | zamkow tykanych przez 338 dzis " + _ticks
                     + " (w biegu " + _sumTicks + " w " + _sumDays + " dobach)"
                     + " | porty zamkow: klucz " + Buildings338.YN(Buildings338.PortsOn) + ", zamkow z Portem zbudowanym " + _portCastles + " (Port na liscie budowy " + _portListed + ", pod KS " + _portKsCastles
                     + "), ryb dzis +" + _fish + ", w biegu +" + _sumFish
                     + " | porty pod KS (miasta i zamki): osad " + ksN + ", ryb dzis +" + ksFish + ", w biegu +" + _sumKsFish + ", Plan KS ryb portow " + _planFish.ToString("0", inv) + " szt./d w " + _planN + " osadach"
                     + " | kopalnie zamkow: klucz " + Buildings338.YN(Buildings338.CastleMinesOn) + ", zamkow z kopalnia " + withMines + ", tykanych " + _mineCastles + ", z urobkiem " + _mineOut
                     + ", rudy dzis +" + ore + " (targ zamku " + _oreMarket + ", Stash " + _oreStash + "), place gornikow " + _oreWage + " zl, bez wolnych rak " + _mineNoHands + ", w biegu rudy +" + _sumOre
                     + Buildings338.Tail(Buildings338.GPorts));
        }
    }
}
