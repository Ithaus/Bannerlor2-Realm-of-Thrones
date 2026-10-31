using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// 308a (Jeff 31.10 "karawany, aby dzialaly i zarabialy") - KARAWANA ZE STATKAMI WYBIERA CEL WEDLUG ZYSKU NA DOBE PRAWDZIWEJ DROGI MORSKIEJ.
    /// Dowod [L] (autotest 143c/143d, linie 259 '(305a)' i 307a): karawana morska gracza z Driftwood Hall plynie do Braavos, Pentos, Qarth, Elyria - rejsy 12-35 dob,
    /// 64 doby bez handlu (143c), wynik handlu bez pierwszej partii -14.7k w 90 dobach (zold 16055). Zrodla celow: BK ThinkNextDestination (Qarth, droga z decyzji
    /// 29.9 doby) i nasze kontrakty surowcow MaterialOrders (Driftwood Hall -> Braavos / Pentos, Pentos -> Elyria; stos Order -> Place -> Move).
    /// Przyczyna [K] (dekompilacja BannerKings.dll BKCaravansBehavior :1293-1400, NavalDLC NavalDLCMapDistanceModel :86-200):
    ///  1. GetTradeScoreForTown liczy odleglosc karawany do miasta ZAWSZE ladem (MapDistanceModel.GetDistance(partia, miasto, false, NavigationType.Default)),
    ///     takze dla karawany ze statkami (BK filtruje ja tylko do miast z portem i plynie Naval). NavalDLC daje Default 1e8 dla partii na morzu i dla miast bez
    ///     drogi ladowej (wyspy: Driftwood Hall, Pebbleton, ...; Essos od Westeros). Ocena = zysk x 1 / (droga + kara dalekiej drogi + 8): przy 1e8 wszystkie miasta
    ///     maja ten sam mianownik - pierwszy przebieg (distanceCut) odrzuca wszystkie, drugi wybiera najlepszy handel swiata BEZ wzgledu na droge (Qarth).
    ///  2. Nasza regula C (CaravanWait139.Route, zysk na dobe drogi) dziala tylko w pierwszym przebiegu - dla takiej karawany nigdy; jej dni drogi
    ///     (CaravanEscort.RoadDaysTo) to linia prosta, nie droga morska.
    ///  3. Kontrakt surowca (MaterialOrders.Order) liczy zysk karawany bez kosztu dni drogi (zold + jedzenie) - karawana gracza (zold 271/d) plynie 12-22 doby po
    ///     marze kilkuset zl.
    /// Regula (klucz CaravanSeaTrueDistance; jedna dla wszystkich karawan ze statkami - gracza i AI; NIE = stare zachowanie co do bitu):
    ///  a) w ocenie miast BK (transpiler GetTradeScoreForTown) karawana ze statkami mierzy droge morska z pamieci drog NavalDLC (port - port; z morza: partia -
    ///     port), takze droge miasta do domu karawany; bez drogi morskiej - odleglosc BK jak dotad. Karawana bez statkow - wywolanie BK bez zmian;
    ///  b) dni drogi karawany ze statkami (CaravanEscort.RoadDaysTo: regula C 139, D1 294c, limit trzymania 307a) = droga morska / dzien drogi taboru (jak kontrakt
    ///     surowca TradeCarts.DaysFor);
    ///  c) limit odcinka: rejs dluzszy niz Caravan Sea Near Days (z towarem handlowym) tylko wtedy, gdy zysk na dobe R = (zysk sprzedazy - koszt dnia x dni) /
    ///     max(1, dni) (ten sam rachunek co C, CaravanWait139.SaleValue) jest wiekszy niz najlepszy port w zasiegu Caravan Sea Near Days (10 najblizszych
    ///     dozwolonych jak u BK); inaczej cel = ten port;
    ///  d) kontrakt surowca karawana (lad i morze): marza minus koszt dnia karawany x dni drogi musi byc > 0; wybor wedlug zysku netto na kg i dobe.
    /// Pomiar (takze przy kluczu NIE): linia doby "Karawany morskie - cel wedlug zysku na dobe (308a): dzien N" - rozklad dni drogi decyzji karawan ze statkami
    /// (droga morska), drugi przebieg BK, zysk na dobe wybranego i najlepszego bliskiego, zamiany, odrzucone kontrakty, decyzje karawan gracza.
    /// </summary>
    internal static class CaravanSea308
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private const int NearTowns = 10;     // portow ocenianych w (c) - najblizsze dozwolone (jak RouteTowns w C)
        private const int DetMax = 6;

        private static bool _wScore;
        private static int _rep1, _rep2;      // podmienione wywolania odleglosci w GetTradeScoreForTown
        [ThreadStatic] private static MobileParty _car;   // karawana biezacej oceny (pierwsze wywolanie odleglosci w metodzie BK ustawia ja przed drugim)

        // liczniki doby (oceny BK moga biec z watkow BK - Interlocked)
        private static long _sSea, _sLand, _sHome;
        private static int _dec, _decFar, _decNoSea, _decPlayer;
        private static readonly int[] _bucket = new int[5];   // <=5, 5-10, 10-20, 20-30, >30 dob
        private static double _daysSum, _daysMax;
        private static int _rN, _rWorse, _rWorseBefore, _swap;
        private static double _rChosen, _rNear;
        private static int _moLand, _moSea;
        private static readonly List<string> _det = new List<string>();
        private static readonly HashSet<MobileParty> _far = new HashSet<MobileParty>();
        private static int _stumbles, _stumbleSess;
        private static readonly HashSet<string> _err = new HashSet<string>();

        internal static bool On(Settings s) { return s != null && s.CaravanSeaTrueDistance; }

        internal static void Reset()
        {
            _car = null; _far.Clear();
            ZeroDay();
            _stumbles = _stumbleSess = 0; _err.Clear();
        }

        private static void ZeroDay()
        {
            Interlocked.Exchange(ref _sSea, 0); Interlocked.Exchange(ref _sLand, 0); Interlocked.Exchange(ref _sHome, 0);
            _dec = _decFar = _decNoSea = _decPlayer = 0; Array.Clear(_bucket, 0, _bucket.Length); _daysSum = _daysMax = 0;
            _rN = _rWorse = _rWorseBefore = _swap = 0; _rChosen = _rNear = 0; _moLand = _moSea = 0; _det.Clear();
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++; _stumbleSess++;
            try { if (_err.Add(where)) Log.Error("CaravanSea308." + where, e); } catch { }
        }

        private static bool Naval(MobileParty mp) { try { return mp != null && mp.HasNavalNavigationCapability; } catch { return false; } }

        private static bool Valid(float d) { return d >= 0f && d < CartTownExit.BkLimit && !float.IsNaN(d); }

        /// <summary>Droga morska karawany ze statkami do miasta z portem (mapa): z miasta - port do portu (pamiec drog Naval), z morza - partia do portu;
        /// -1 = nie dotyczy (bez statkow, cel bez portu, partia na ladzie poza miastem, brak drogi morskiej). Bez klucza (pomiar).</summary>
        internal static float SeaDistRaw(MobileParty mp, Settlement to)
        {
            if (mp == null || to == null || !to.HasPort || !Naval(mp)) return -1f;
            var c = Campaign.Current;
            var dm = c != null && c.Models != null ? c.Models.MapDistanceModel : null;
            if (dm == null) return -1f;
            float d = -1f;
            var cs = mp.CurrentSettlement;
            if (cs != null)
            {
                if (cs == to) return 0f;
                if (!cs.HasPort) return -1f;
                d = dm.GetDistance(cs, to, true, true, MobileParty.NavigationType.Naval);
            }
            else if (mp.IsCurrentlyAtSea)
            {
                float lr;
                d = dm.GetDistance(mp, to, true, MobileParty.NavigationType.Naval, out lr);
            }
            return Valid(d) ? d : -1f;
        }

        /// <summary>Dni drogi morskiej (droga morska / dzien drogi taboru, jak TradeCarts.DaysFor bez zaokraglenia); -1 = nie dotyczy.</summary>
        internal static double SeaDaysRaw(MobileParty mp, Settlement to)
        {
            float d = SeaDistRaw(mp, to);
            if (d < 0f) return -1.0;
            return d / Math.Max(1.0, MarketCarts.PerDay(Settings.Current));
        }

        /// <summary>(b) CaravanEscort.RoadDaysTo: dni drogi karawany ze statkami przy kluczu; -1 = miara dotychczasowa.</summary>
        internal static double SeaDays(MobileParty mp, Settlement to)
        {
            if (!On(Settings.Current)) return -1.0;
            try { return SeaDaysRaw(mp, to); }
            catch (Exception e) { Stumble("SeaDays", e); return -1.0; }
        }

        /// <summary>RiverBarges: ocena BK karawany ze statkami liczy juz droge morska - mnoznik barek (droga ladowa) jej nie dotyczy.</summary>
        internal static bool ScoresBySea(MobileParty mp) { return _wScore && On(Settings.Current) && Naval(mp); }

        // ------------------------------------------------------------ (a) odleglosc w ocenie BK
        /// <summary>Zamiast MapDistanceModel.GetDistance(partia, osada, port, nav, out lr) w GetTradeScoreForTown.</summary>
        public static float Dist1(MapDistanceModel m, MobileParty p, Settlement s, bool port, MobileParty.NavigationType nav, out float lr)
        {
            _car = p;
            try
            {
                if (On(Settings.Current) && Naval(p))
                {
                    float d = SeaDistRaw(p, s);
                    if (d >= 0f) { Interlocked.Increment(ref _sSea); lr = 0f; return d; }
                    Interlocked.Increment(ref _sLand);
                }
            }
            catch (Exception e) { Stumble("Dist1", e); }
            return m.GetDistance(p, s, port, nav, out lr);
        }

        /// <summary>Zamiast MapDistanceModel.GetDistance(osada, osada, port, port, nav) w GetTradeScoreForTown (miasto - dom karawany).</summary>
        public static float Dist2(MapDistanceModel m, Settlement a, Settlement b, bool fp, bool tp, MobileParty.NavigationType nav)
        {
            try
            {
                var p = _car;
                if (p != null && a != null && b != null && a.HasPort && b.HasPort && On(Settings.Current) && Naval(p))
                {
                    float d = a == b ? 0f : m.GetDistance(a, b, true, true, MobileParty.NavigationType.Naval);
                    if (Valid(d)) { Interlocked.Increment(ref _sHome); return d; }
                }
            }
            catch (Exception e) { Stumble("Dist2", e); }
            return m.GetDistance(a, b, fp, tp, nav);
        }

        private static MethodInfo _m1, _m2;

        public static IEnumerable<CodeInstruction> ScoreTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            var list = instructions.ToList();
            var d1 = AccessTools.Method(typeof(CaravanSea308), nameof(Dist1));
            var d2 = AccessTools.Method(typeof(CaravanSea308), nameof(Dist2));
            int n1 = 0, n2 = 0;
            var outl = new List<CodeInstruction>(list.Count);
            foreach (var c in list)
            {
                if (_m1 != null && c.Calls(_m1)) { outl.Add(new CodeInstruction(OpCodes.Call, d1) { labels = c.labels, blocks = c.blocks }); n1++; continue; }
                if (_m2 != null && c.Calls(_m2)) { outl.Add(new CodeInstruction(OpCodes.Call, d2) { labels = c.labels, blocks = c.blocks }); n2++; continue; }
                outl.Add(c);
            }
            _rep1 = n1; _rep2 = n2;
            if (n1 < 1) { _rep1 = _rep2 = 0; return list; }   // nie ta metoda BK - bez zmian
            return outl;
        }

        // ------------------------------------------------------------ (c) limit odcinka i pomiar decyzji (z CaravanRoute294c.ThinkPostfix)
        /// <summary>Z CaravanRoute294c.FindPostfix: drugi przebieg BK (bez limitu odleglosci) dla tej karawany.</summary>
        internal static void NoteFar(MobileParty mp) { if (mp != null && Naval(mp)) _far.Add(mp); }

        private static bool Allowed(MobileParty mp, Town t)
        {
            var st = t != null ? t.Settlement : null;
            if (st == null || !t.IsTown || !st.HasPort || st == mp.LastVisitedSettlement || st == mp.CurrentSettlement || st.IsUnderSiege) return false;
            if (t.MapFaction != null && mp.MapFaction != null && t.MapFaction.IsAtWarWith(mp.MapFaction)) return false;
            var main = MobileParty.MainParty;
            if (main != null && main.CurrentSettlement == st && main.MapFaction != null && mp.MapFaction != null && main.MapFaction.IsAtWarWith(mp.MapFaction)) return false;
            return true;
        }

        /// <summary>Wynik ThinkNextDestination (po obejsciu 294c, przed wpisem Inbound): karawana ze statkami - pomiar dni drogi; z towarem handlowym - zysk na
        /// dobe wybranego i najlepszego portu w zasiegu Caravan Sea Near Days; przy kluczu rejs dalszy niz ten zasieg tylko z lepszym zyskiem na dobe (c).</summary>
        internal static void Decide(MobileParty mp, ref Town result)
        {
            if (mp == null) return;
            bool far = _far.Remove(mp);
            if (result == null || result.Settlement == null || !Naval(mp)) return;
            var s = Settings.Current;
            if (s == null) return;
            try
            {
                _dec++;
                if (far) _decFar++;
                double dc = SeaDaysRaw(mp, result.Settlement);
                if (dc < 0) { _decNoSea++; return; }
                Bucket(dc);
                bool player = mp.ActualClan == Clan.PlayerClan;
                if (player) _decPlayer++;
                var pantry = CaravanTrade284.PantryCopy(mp);
                if (!CaravanWait139.HasTrade(mp, pantry)) { if (player) Det(mp, result, dc, null, 0, 0, double.NaN, far, false); return; }
                double near = Math.Max(1.0, s.CaravanSeaNearDays);
                var cand = new List<KeyValuePair<Town, double>>();
                foreach (var t in Town.AllTowns)
                {
                    if (t == result || !Allowed(mp, t)) continue;
                    double d = SeaDaysRaw(mp, t.Settlement);
                    if (d >= 0 && d <= near) cand.Add(new KeyValuePair<Town, double>(t, d));
                }
                cand.Sort((a, b) => a.Value.CompareTo(b.Value));
                double rc = CaravanWait139.PerDayNet(mp, result, dc, pantry);
                Town best = null; double rb = double.NegativeInfinity, db = 0;
                for (int i = 0; i < cand.Count && i < NearTowns; i++)
                {
                    double r = CaravanWait139.PerDayNet(mp, cand[i].Key, cand[i].Value, pantry);
                    if (r > rb) { rb = r; best = cand[i].Key; db = cand[i].Value; }
                }
                CaravanWait139.ClearDecision();
                bool worse = dc > near && best != null && rb > 0 && rb > rc;
                if (worse) _rWorseBefore++;
                bool swap = worse && On(s);
                if (best != null) { _rN++; _rChosen += rc; _rNear += rb; }
                if (player) Det(mp, result, dc, best, db, rb, rc, far, swap);
                if (swap)
                {
                    _swap++;
                    result = best;
                }
                else if (worse) _rWorse++;
            }
            catch (Exception e) { Stumble("Decide", e); }
        }

        private static void Bucket(double d)
        {
            int k = d <= 5 ? 0 : d <= 10 ? 1 : d <= 20 ? 2 : d <= 30 ? 3 : 4;
            _bucket[k]++; _daysSum += d; if (d > _daysMax) _daysMax = d;
        }

        private static string Nm(Settlement s) { return s == null ? "-" : s.Name != null ? s.Name.ToString() : s.StringId; }

        private static void Det(MobileParty mp, Town to, double d, Town best, double db, double rb, double rc, bool far, bool swap)
        {
            if (_det.Count >= DetMax) return;
            var from = mp.CurrentSettlement ?? mp.LastVisitedSettlement;
            var sb = new StringBuilder(160);
            sb.Append('\'').Append(mp.Name != null ? mp.Name.ToString() : mp.StringId).Append("' ").Append(Nm(from)).Append(" -> ").Append(Nm(to.Settlement))
              .Append(' ').Append(d.ToString("0.0", Inv)).Append(" dob morzem").Append(far ? " (drugi przebieg BK)" : "");
            if (double.IsNaN(rc)) sb.Append(", bez towaru handlowego");
            else
            {
                sb.Append(", zysk/dobe ").Append(rc.ToString("0", Inv));
                if (best != null) sb.Append(", najlepszy bliski ").Append(Nm(best.Settlement)).Append(' ').Append(db.ToString("0.0", Inv)).Append(" dob ").Append(rb.ToString("0", Inv));
                else sb.Append(", bliskich portow brak");
                if (swap) sb.Append(" -> ZAMIANA na ").Append(Nm(best.Settlement));
            }
            _det.Add(sb.ToString());
        }

        // ------------------------------------------------------------ (d) kontrakt surowca (MaterialOrders.Order)
        /// <summary>Zysk netto kontraktu karawany: marza - koszt dnia karawany x dni drogi; przy kluczu NIE - marza (jak dotad).</summary>
        internal static float ContractNet(MobileParty car, float margin, int days, bool naval)
        {
            if (car == null || !On(Settings.Current)) return margin;
            double net = margin - CaravanWait139.DayCost(car) * Math.Max(1, days);
            if (!(net > 0)) { if (naval) _moSea++; else _moLand++; }
            return (float)net;
        }

        // ------------------------------------------------------------ wpiecie i linia doby
        internal static void ApplyAll(Harmony h)
        {
            string why = null;
            try
            {
                _m1 = AccessTools.Method(typeof(MapDistanceModel), "GetDistance", new[] { typeof(MobileParty), typeof(Settlement), typeof(bool), typeof(MobileParty.NavigationType), typeof(float).MakeByRefType() });
                _m2 = AccessTools.Method(typeof(MapDistanceModel), "GetDistance", new[] { typeof(Settlement), typeof(Settlement), typeof(bool), typeof(bool), typeof(MobileParty.NavigationType) });
                var bkc = AccessTools.TypeByName("BannerKings.Behaviours.BKCaravansBehavior");
                var m = bkc != null ? AccessTools.DeclaredMethod(bkc, "GetTradeScoreForTown") : null;
                if (_m1 == null) why = "brak MapDistanceModel.GetDistance(MobileParty, Settlement, bool, NavigationType, out float)";
                else if (m == null || m.ReturnType != typeof(float)) why = "brak BKCaravansBehavior.GetTradeScoreForTown";
                else
                {
                    h.Patch(m, transpiler: new HarmonyMethod(typeof(CaravanSea308), nameof(ScoreTranspiler)));
                    _wScore = _rep1 >= 1;
                    if (!_wScore) why = "w GetTradeScoreForTown brak wywolania odleglosci partia - miasto";
                }
            }
            catch (Exception e) { why = "wyjatek " + e.GetType().Name; Log.Error("CaravanSea308.ApplyAll", e); }
            Log.Info("Karawany morskie - cel wedlug zysku na dobe (308a): ocena miast BK " + (_wScore ? "wpieta (odleglosc partia - miasto " + _rep1 + ", miasto - dom " + _rep2 + ")" : "NIE (" + why + ")") + ".");
        }

        internal static void Daily()
        {
            var s = Settings.Current;
            if (s == null || Campaign.Current == null) return;
            try
            {
                _far.RemoveWhere(p => p == null || !p.IsActive);
                int day = (int)CampaignTime.Now.ToDays - 1;
                int nd = _bucket[0] + _bucket[1] + _bucket[2] + _bucket[3] + _bucket[4];
                var sb = new StringBuilder(900);
                sb.Append("Karawany morskie - cel wedlug zysku na dobe (308a): dzien ").Append(day)
                  .Append(" | klucz Caravan Sea True Distance ").Append(On(s) ? "TAK" : "NIE").Append(", blisko <= ").Append(Math.Max(1.0, s.CaravanSeaNearDays).ToString("0.#", Inv)).Append(" dob")
                  .Append(" | ocena BK: ").Append(_wScore ? "wpieta" : "NIE").Append(", odleglosc morska ").Append(Interlocked.Read(ref _sSea)).Append(", bez drogi morskiej (BK ladem) ")
                  .Append(Interlocked.Read(ref _sLand)).Append(", dom morzem ").Append(Interlocked.Read(ref _sHome))
                  .Append(" | decyzje karawan ze statkami ").Append(_dec).Append(" (drugi przebieg BK ").Append(_decFar).Append(", cel bez drogi morskiej ").Append(_decNoSea)
                  .Append(", gracza ").Append(_decPlayer).Append("), dni drogi morzem: <=5 ").Append(_bucket[0]).Append(", 5-10 ").Append(_bucket[1]).Append(", 10-20 ").Append(_bucket[2])
                  .Append(", 20-30 ").Append(_bucket[3]).Append(", >30 ").Append(_bucket[4]).Append(", srednio ").Append((nd > 0 ? _daysSum / nd : 0).ToString("0.0", Inv))
                  .Append(", najwiecej ").Append(_daysMax.ToString("0.0", Inv))
                  .Append(" | z towarem i bliskim portem ").Append(_rN).Append(": zysk/dobe wybranego srednio ").Append((_rN > 0 ? _rChosen / _rN : 0).ToString("0", Inv))
                  .Append(", najlepszego bliskiego srednio ").Append((_rN > 0 ? _rNear / _rN : 0).ToString("0", Inv))
                  .Append(", daleki rejs gorszy niz bliski port: przed regula ").Append(_rWorseBefore).Append(", zamienione na bliski ").Append(_swap).Append(", zostalo ").Append(_rWorse)
                  .Append(" | kontrakty surowcow karawana bez zysku po koszcie dni: lad ").Append(_moLand).Append(", morze ").Append(_moSea)
                  .Append(" | gracza: ").Append(_det.Count == 0 ? "-" : string.Join("; ", _det.ToArray()))
                  .Append(" | potkniecia ").Append(_stumbles).Append(" (sesja ").Append(_stumbleSess).Append(").");
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Stumble("Daily", e); }
            ZeroDay(); _stumbles = 0;
        }
    }
}
