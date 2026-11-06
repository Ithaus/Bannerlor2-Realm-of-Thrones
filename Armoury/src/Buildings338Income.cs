using System;
using System.Threading;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;

namespace Armoury
{
    /// <summary>
    /// 338 grupy 5 i 6 - DOCHOD MIASTA.
    /// 5 (Buildings338TaxOffice) URZAD PODATKOWY MIASTA +5/10/15% (TaxPerDay, AddFactor): jedyny czytnik (DefaultSettlementTaxModel:146) wchodzi do podatku BK, ktory
    /// PopulationLaw.TownTaxPostfix (RentReplacesTownTax) zastepuje. U Jeffa (DsTowns, rampa F3 = 1.000) pan i korona dostaja pieniadze miasta tylko rata firmy
    /// DsTowns.Pay - tam jedna linia: rata pana i korony x (1 + efekt budynku) (Town.AddEffectOfBuildings - perki wchodza same). Dalej ta sama droga: zaplata z kasy
    /// miasta najwyzej z nadwyzki ponad zapas, brak = zaleglosc jak dla raty. Budynek przyspiesza pobor z kasy miasta, nie tworzy zlota. Nie ruszamy podatku BK ani
    /// zastapionego TownTaxPostfix (inaczej dubel w radzie CouncilWages35.TownBase). Zawor PopulationLaw.ValveTowns (gdy droga firm nieczynna) - bez zmian w 338.
    /// 6 (Buildings338Caravans) TARGOWISKO - WIZYTY KARAWAN (CaravanAccessibility 1.02/1.04/1.06, obietnica w podpowiedzi budynku): jedyny czytnik gry nie biegnie
    /// (BK HourlyTickParty_Skip), cel karawany wybiera BKCaravansBehavior.GetTradeScoreForTown bez budynkow. Postfiks Low (po postfiksach tras karawan Armoury):
    /// wynik x wartosc efektu targowiska (1.02/1.04/1.06 - liczby gry czytane jako mnoznik; AddFactor na bazie 1 dalby x2). Nic z niczego - przesuw ruchu karawan.
    /// </summary>
    internal static class Buildings338Income
    {
        private static int _taxTowns, _taxCalls;
        private static double _taxL, _taxC;
        private static readonly object Gate = new object();
        private static bool _hookedCar;
        private static long _carCalls, _carUp;
        private static double _carMul;

        internal static void SessionStart()
        {
            lock (Gate) { _taxTowns = _taxCalls = 0; _taxL = _taxC = 0; _carCalls = _carUp = 0; _carMul = 0; }
            if (!_hookedCar)
            {
                var t = AccessTools.TypeByName("BannerKings.Behaviours.BKCaravansBehavior");
                var m = t != null ? AccessTools.Method(t, "GetTradeScoreForTown") : null;
                var ps = m != null ? m.GetParameters() : null;
                if (m != null && m.ReturnType == typeof(float) && ps.Length >= 2 && ps[0].ParameterType == typeof(MobileParty) && ps[1].ParameterType == typeof(Town))
                {
                    Buildings338.H.Patch(m, postfix: new HarmonyMethod(typeof(Buildings338Income), nameof(ScorePostfix)) { priority = Priority.Low });
                    _hookedCar = true;
                }
            }
            Buildings338.Hook("karawany: " + (_hookedCar ? "BKCaravansBehavior.GetTradeScoreForTown wpiety" : "BRAK BKCaravansBehavior.GetTradeScoreForTown"));
        }

        // ------------------------------------------------------------ 5 urzad podatkowy (DsTowns.Pay)
        /// <summary>DsTowns.Pay (miasto, po wyliczeniu raty pana iL i korony iC, przed zaplata): rata x (1 + efekt urzedu).</summary>
        internal static void TaxOffice(Town t, ref double iL, ref double iC)
        {
            if (t == null || !t.IsTown || !Buildings338.TaxOfficeOn || !(iL + iC > 0)) return;
            try
            {
                var e = new ExplainedNumber(1f);
                t.AddEffectOfBuildings(BuildingEffectEnum.TaxPerDay, ref e);
                double f = e.ResultNumber;
                if (!(f > 1.0) || double.IsNaN(f) || double.IsInfinity(f)) return;
                double dL = iL * (f - 1.0), dC = iC * (f - 1.0);
                iL += dL; iC += dC;
                lock (Gate) { _taxCalls++; _taxL += dL; _taxC += dC; }
            }
            catch (Exception ex) { Buildings338.Stumble(Buildings338.GTax, "Income.TaxOffice", ex); }
        }

        // ------------------------------------------------------------ 6 targowisko - wizyty karawan
        public static void ScorePostfix(Town __1, ref float __result)
        {
            if (__1 == null || !(__result > 0f) || !Buildings338.CaravansOn) return;
            try
            {
                Interlocked.Increment(ref _carCalls);
                float m = Mult(__1);
                if (!(m > 1f)) return;
                __result *= m;
                lock (Gate) { _carUp++; _carMul += m; }
            }
            catch (Exception ex) { Buildings338.Stumble(Buildings338.GCaravan, "Income.ScorePostfix", ex); }
        }

        private static float Mult(Town t)
        {
            float best = 1f;
            foreach (var b in t.Buildings)
            {
                if (b == null || b.CurrentLevel <= 0 || b.BuildingType == null || !b.BuildingType.HasEffect(BuildingEffectEnum.CaravanAccessibility)) continue;
                float v = Campaign.Current.Models.BuildingEffectModel.GetBuildingEffect(b, BuildingEffectEnum.CaravanAccessibility).ResultNumber;
                float m = v >= 1f ? v : 1f + v;   // gra: 1.02/1.04/1.06; gdyby ktos podal sam ulamek (0.02) - ten sam mnoznik
                if (m > best && m < 2f) best = m;
            }
            return best;
        }

        internal static void Daily(int day)
        {
            int nTax = 0, nMk = 0;
            foreach (var t in Town.AllTowns)
            {
                try
                {
                    var e = new ExplainedNumber(1f);
                    t.AddEffectOfBuildings(BuildingEffectEnum.TaxPerDay, ref e);
                    if (e.ResultNumber > 1f) nTax++;
                    if (Mult(t) > 1f) nMk++;
                }
                catch (Exception ex) { Buildings338.Stumble(Buildings338.GTax, "Income.Daily", ex); }
            }
            int calls; double l, c; long cc, cu; double cm;
            lock (Gate) { calls = _taxCalls; l = _taxL; c = _taxC; _taxCalls = 0; _taxL = _taxC = 0; cc = _carCalls; cu = _carUp; cm = _carMul; _carCalls = _carUp = 0; _carMul = 0; }
            var inv = Buildings338.Inv;
            Log.Info("Budynki (338) urzad podatkowy: dzien " + day + " | miast " + nTax + " z urzedem, droga firm " + Buildings338.YN(DsTowns.FarmsLive)
                     + " | rata +" + (l + c).ToString("0", inv) + " zl (pan " + l.ToString("0", inv) + ", korona " + c.ToString("0", inv) + ") w " + calls + " ratach" + Buildings338.Tail(Buildings338.GTax));
            Log.Info("Budynki (338) karawany: dzien " + day + " | miast z Marketplace " + nMk + " | wywolan " + cc + ", podniesionych " + cu + ", sredni mnoznik "
                     + (cu > 0 ? (cm / cu).ToString("0.000", inv) : "1.000") + Buildings338.Tail(Buildings338.GCaravan));
        }
    }
}
