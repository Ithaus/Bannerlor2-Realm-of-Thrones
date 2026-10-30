using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// PACZKA 140 (30.10, Jeff "pracuj jeszcze nad karawana dobranoc"; wczesniej "Napraw karawany", "rob tak dlugo porawi az bedziesz zadowlony z wyniku") -
    /// ODCINEK KARAWAN I UTKNIETE KARAWANY. Dowod (autotest 139a = WGRANE 139 wobec 138c, 90 dob na tej samej kopii zapisu): odcinek 288 ostatnie 7 dob 4.47 -> 5.09,
    /// choc regula 139 C skraca kazdy wybor, ktory zmienia (1182 decyzje 5.11 -> 2.83 dnia drogi).
    ///  (1) MIARA: Stage8World.LegDays = karawany AI swiata / wyjazdy z miast (7 dob) liczy kazda karawane, takze stojaca w zamku albo we wsi - ona nie jedzie
    ///      zadnym odcinkiem i nie wyjezdza z miasta. 139a: karawan w osadach (linia 'Karawany (kierunek)') 38, w miastach (linia 'Postoj karawan') 19 - ok. 19 stoi
    ///      poza miastami (138c: ok. 0), limit postoju 12 tikow: 10.4 -> 30.7 karawan na dobe. To ok. +0.22 doby z +0.42 wydluzenia. Miara wraca przez spizarnie
    ///      eskorty (CaravanGoods.PantryDays = ceil(odcinek) + 2: 7 -> 8 dob po przekroczeniu 5.0; odmowy T1 'rezerwa jedzenia' 70 -> 92 na dobe), wspolczynnik
    ///      droga / prosta (CaravanEscort.Ratio 1.40 -> 1.54 - wszystkie dni drogi 139 C i jedzenia na droge 288) i przewoz w regule 139 A (Carry).
    ///      KLUCZ 1 CaravanLegMovingOnly: w liczniku odcinka karawany AI swiata bez stojacych w osadzie, ktora nie jest miastem (postoj w miescie - jak dotad czesc
    ///      odcinka miasto - miasto; wyjazdy bez zmian). Klucz NIE - miara 139 co do bitu. Przyczyny postoju poza miastami nie znamy - spis w linii 140.
    ///  (2) UTKNIETE: 116 karawan klasy 3 linii 298 ('towar z rynkiem z zyskiem - czeka') z towarem po koszcie 585 tys. zl, mediana 16 dob od ostatniej sprzedazy;
    ///      wlasna sonda 139 znajduje miasto placace koszt + droge dla 85% z nich, ok. 1.5 doby drogi, cena tam ok. 2.75 x koszt - to droga, nie cena. Regula 139 C
    ///      widzi tylko 11 decyzji utknietych na dobe (skierowane 3.6): Route biegnie tylko po pierwszym przebiegu BK z wynikiem (pusty pierwszy przebieg -> drugi
    ///      przebieg BK bez limitu odleglosci, daleko), kandydaci tylko z ocen BK > 0 tego przebiegu, dom karawany zawsze zostaje.
    ///      KLUCZ 2 CaravanStuckSeekPayingTown (tylko karawany utkniete - CaravanTrade284.Stuck7 - z towarem handlowym, bez statkow): kandydaci C ze wszystkich miast
    ///      (te same wykluczenia co BK FindNextDestinationForCaravan: nie miasto, ostatnio odwiedzone, to, w ktorym stoi, oblezone, wrog, gracz-wrog w miescie,
    ///      lista BK 'zmienione przez wroga w drodze'; plus droga ladowa jak IslandRoads); pusty pierwszy przebieg - wybor z tych miast w zasiegu odcinka x (1 + J)
    ///      (wtedy BK nie robi drugiego przebiegu); dom zostaje tylko, gdy R(dom) > 0. Ta sama ocena R = (SaleValue - koszt dnia x dni) / max(1, dni), zmiana tylko
    ///      przy R > 0 i R > R(b*). Klucz NIE - 139 co do bitu.
    /// Linia doby 'Karawany - odcinek i utkniete (140): dzien N' (po linii 139). Wyjatek - licznik potkniec.
    /// </summary>
    internal static class CaravanLeg140
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private const int TrackDays = 7;              // ocena skierowania utknietej: sprzedaz w tylu dobach (Stuck7)

        internal static bool LegOn(Settings s) { return s != null && s.CaravanLegMovingOnly; }
        internal static bool SeekOn(Settings s) { return s != null && s.CaravanStuckSeekPayingTown; }

        // ------------------------------------------------------------ spis postoju poza miastami (Stage8World.CaravanLine)
        private static int _pN, _pCastle, _pVillage, _pOther, _pAlert, _pHold, _pWound, _pPurse, _pGoods, _pStuck, _pTgtHere, _pTgtElse, _pTgtNone, _p1, _p7;
        private static int _pDay = int.MinValue;
        private static Dictionary<MobileParty, KeyValuePair<Settlement, int>> _since = new Dictionary<MobileParty, KeyValuePair<Settlement, int>>();
        private static Dictionary<MobileParty, KeyValuePair<Settlement, int>> _sinceNew = new Dictionary<MobileParty, KeyValuePair<Settlement, int>>();

        // ------------------------------------------------------------ liczniki doby (decyzje utknietych)
        private static int _q1, _q1Cand, _qOut, _qE, _qEE, _q2, _qH, _qHE;
        private static int _oN, _oSold, _oGone;
        private static readonly Dictionary<MobileParty, int> _track = new Dictionary<MobileParty, int>();
        private static long _ticks;
        private static int _stumbles, _stumbleSess;
        private static readonly HashSet<string> _err = new HashSet<string>();
        // BK BKCaravansBehavior._previouslyChangedCaravanTargetsDueToEnemyOnWay (Dictionary<MobileParty, List<Settlement>>) - ten sam wykluczony cel co BK
        private static FieldInfo _prevFi; private static bool _prevTried;

        internal static void Reset()
        {
            ZeroDay();
            _stumbleSess = 0; _err.Clear();
            _since.Clear(); _sinceNew.Clear(); _track.Clear();
            ZeroPark(); _pDay = int.MinValue;
        }

        private static void ZeroDay()
        {
            _q1 = _q1Cand = _qOut = _qE = _qEE = _q2 = _qH = _qHE = 0;
            _oN = _oSold = _oGone = 0;
            _ticks = 0; _stumbles = 0;
        }

        private static void ZeroPark()
        {
            _pN = _pCastle = _pVillage = _pOther = _pAlert = _pHold = _pWound = _pPurse = _pGoods = _pStuck = _pTgtHere = _pTgtElse = _pTgtNone = _p1 = _p7 = 0;
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++; _stumbleSess++;
            try { if (_err.Add(where)) Log.Error("CaravanLeg140." + where, e); } catch { }
        }

        /// <summary>Stage8World.CaravanLine przed petla po karawanach AI swiata.</summary>
        internal static void ParkedBegin(int day)
        {
            ZeroPark(); _pDay = day; _sinceNew.Clear();
        }

        /// <summary>Stage8World.CaravanLine: karawana AI stoi w osadzie, ktora nie jest miastem (zamek, wies) - spis cech (tylko odczyt).</summary>
        internal static void Parked(MobileParty c, Settlement at, int day)
        {
            try
            {
                _pN++;
                if (at.IsCastle) _pCastle++; else if (at.IsVillage) _pVillage++; else _pOther++;
                if (c.Ai != null && c.Ai.IsAlerted) _pAlert++;
                if (c.DefaultBehavior == AiBehavior.Hold || c.ShortTermBehavior == AiBehavior.Hold) _pHold++;
                var mr = c.MemberRoster;
                int men = mr != null ? mr.TotalManCount : 0, wnd = mr != null ? mr.TotalWounded : 0;
                if (men <= 0 || wnd > 0.4f * men) _pWound++;
                if (c.PartyTradeGold < 100) _pPurse++;
                bool goods = false;
                var r = c.ItemRoster;
                for (int i = 0; r != null && i < r.Count && !goods; i++)
                {
                    var el = r.GetElementCopyAtIndex(i);
                    if (el.Amount > 0 && el.EquipmentElement.Item != null && el.EquipmentElement.Item.IsTradeGood) goods = true;
                }
                if (goods) _pGoods++;
                if (goods && CaravanTrade284.Stuck7(c, day + 1)) _pStuck++;
                var tg = c.TargetSettlement;
                if (tg == null) _pTgtNone++; else if (tg == at) _pTgtHere++; else _pTgtElse++;
                KeyValuePair<Settlement, int> was;
                int first = _since.TryGetValue(c, out was) && was.Key == at ? was.Value : day;
                _sinceNew[c] = new KeyValuePair<Settlement, int>(at, first);
                if (day - first >= 1) _p1++;
                if (day - first >= 7) _p7++;
            }
            catch (Exception e) { Stumble("Parked", e); }
        }

        /// <summary>Stage8World.CaravanLine po petli: pamiec 'od kiedy stoi' tylko dla karawan widzianych dzis.</summary>
        internal static void ParkedEnd()
        {
            var t = _since; _since = _sinceNew; _sinceNew = t; _sinceNew.Clear();
        }

        // ------------------------------------------------------------ kandydaci C dla karawany utknietej (klucz 2)
        private static List<Settlement> PrevChanged(object bk, MobileParty mp)
        {
            if (bk == null) return null;
            try
            {
                if (!_prevTried)
                {
                    _prevTried = true;
                    _prevFi = AccessTools.Field(bk.GetType(), "_previouslyChangedCaravanTargetsDueToEnemyOnWay");
                    if (_prevFi != null && !typeof(Dictionary<MobileParty, List<Settlement>>).IsAssignableFrom(_prevFi.FieldType)) _prevFi = null;
                    if (_prevFi == null) Log.Info("CaravanLeg140 (140): BRAK BKCaravansBehavior._previouslyChangedCaravanTargetsDueToEnemyOnWay - kandydaci utknietych bez listy BK 'zmienione przez wroga'.");
                }
                if (_prevFi == null) return null;
                var d = _prevFi.GetValue(bk) as Dictionary<MobileParty, List<Settlement>>;
                List<Settlement> l;
                return d != null && d.TryGetValue(mp, out l) ? l : null;
            }
            catch (Exception e) { Stumble("PrevChanged", e); return null; }
        }

        /// <summary>Droga ladowa do osady - ta sama odleglosc, ktora liczy straznik BK (IslandRoads.Reach, CartTownExit.BkLimit).</summary>
        private static bool Land(MobileParty mp, Settlement st)
        {
            float lr;
            float d = Campaign.Current.Models.MapDistanceModel.GetDistance(mp, st, false, MobileParty.NavigationType.Default, out lr);
            return d >= 0f && d < CartTownExit.BkLimit;
        }

        /// <summary>Klucz 2: miasta dla karawany utknietej - wykluczenia BK FindNextDestinationForCaravan (poza oblezeniem, wojna, ostatnio odwiedzonym, biezacym,
        /// gracz-wrog w miescie, lista BK 'zmienione przez wroga w drodze') i droga ladowa; dni drogi <= dmax; najblizsze max wedlug dni (CaravanWait139.Days).</summary>
        internal static List<KeyValuePair<Town, double>> Towns(MobileParty mp, object bk, Town skip, double dmax, int max)
        {
            var all = new List<KeyValuePair<Town, double>>();
            var prev = PrevChanged(bk, mp);
            var main = MobileParty.MainParty;
            var mf = mp.MapFaction;
            bool mainWar = main != null && main.MapFaction != null && mf != null && main.MapFaction.IsAtWarWith(mf);
            foreach (var t in Town.AllTowns)
            {
                var st = t != null ? t.Settlement : null;
                if (st == null || !t.IsTown || t == skip || t.MarketData == null) continue;
                if (st == mp.LastVisitedSettlement || st == mp.CurrentSettlement || t.IsUnderSiege) continue;
                if (t.MapFaction != null && mf != null && t.MapFaction.IsAtWarWith(mf)) continue;
                if (mainWar && st.Parties != null && st.Parties.Contains(main)) continue;
                if (prev != null && prev.Contains(st)) continue;
                double d = CaravanWait139.Days(mp, st);
                if (d <= dmax + 1e-6) all.Add(new KeyValuePair<Town, double>(t, d));
            }
            all.Sort((a, b) => a.Value.CompareTo(b.Value));
            var res = new List<KeyValuePair<Town, double>>();
            for (int i = 0; i < all.Count && res.Count < max; i++)
            {
                try { if (!Land(mp, all[i].Key.Settlement)) continue; }
                catch (Exception e) { Stumble("Land", e); continue; }
                res.Add(all[i]);
            }
            return res;
        }

        // ------------------------------------------------------------ liczniki (CaravanWait139.Route / RouteEmpty, CaravanRoute294c.FindPostfix)
        internal static void NoteFirst(int cand) { _q1++; _q1Cand += cand; }
        internal static void NoteOutside() { _qOut++; }
        internal static void NoteEmpty(bool redirected) { _qE++; if (redirected) _qEE++; }
        internal static void NoteHomeNo(bool redirected) { _qH++; if (redirected) _qHE++; }
        internal static void AddTicks(long t) { _ticks += t; }

        /// <summary>Drugi przebieg BK (bez limitu odleglosci): karawana utknieta - licznik (pomiar takze przy kluczu NIE).</summary>
        internal static void NoteSecond(MobileParty mp)
        {
            try
            {
                var s = Settings.Current;
                if (mp != null && CaravanWait139.AOn(s) && CaravanTrade284.Stuck7(mp, (int)CampaignTime.Now.ToDays)) _q2++;
            }
            catch (Exception e) { Stumble("NoteSecond", e); }
        }

        /// <summary>Skierowana karawana utknieta (139 C albo 140): dzien skierowania - ocena po 7 dobach, czy sprzedala (pierwsze skierowanie w oknie).</summary>
        internal static void Track(MobileParty mp)
        {
            if (mp == null || _track.ContainsKey(mp)) return;
            _track[mp] = (int)CampaignTime.Now.ToDays;
        }

        // ------------------------------------------------------------ doba
        /// <summary>Linia doby 'Karawany - odcinek i utkniete (140): dzien N' (po linii 139, ArmouryBehavior).</summary>
        internal static void Daily()
        {
            var s = Settings.Current;
            if (s == null || Campaign.Current == null) return;
            long t0 = Stopwatch.GetTimestamp();
            int today = (int)CampaignTime.Now.ToDays, day = today - 1;
            var sb = new StringBuilder(900);
            try
            {
                List<MobileParty> done = null;
                foreach (var kv in _track)
                {
                    if (today - kv.Value < TrackDays) continue;
                    if (done == null) done = new List<MobileParty>();
                    done.Add(kv.Key);
                    var mp = kv.Key;
                    try
                    {
                        if (mp == null || !mp.IsActive) { _oGone++; continue; }
                        _oN++;
                        if (today - CaravanTrade284.DaysSinceSale(mp, today) >= kv.Value) _oSold++;
                    }
                    catch (Exception e) { Stumble("Daily(ocena)", e); }
                }
                if (done != null) foreach (var k in done) _track.Remove(k);
                double ms = (Stopwatch.GetTimestamp() - t0 + _ticks) * 1000.0 / Stopwatch.Frequency;
                bool park = _pDay == day;
                sb.Append("Karawany - odcinek i utkniete (140): dzien ").Append(day)
                  .Append(" | klucze: odcinek bez postoju poza miastami ").Append(LegOn(s) ? "TAK" : "NIE").Append(", utkniete szukaja miasta ").Append(SeekOn(s) ? "TAK" : "NIE")
                  .Append(" | odcinek: wszystkie karawany ").Append(Stage8World.LegDaysAll.ToString("0.00", Inv)).Append(", bez postoju poza miastami ")
                  .Append(Stage8World.LegDaysMoving.ToString("0.00", Inv)).Append(", uzywany ").Append(Stage8World.LegDays.ToString("0.00", Inv))
                  .Append(" dob (dob pomiaru ").Append(Stage8World.LegSamples).Append(")")
                  .Append(" | postoj poza miastami ").Append(park ? _pN.ToString(Inv) : "-").Append(" (zamki ").Append(_pCastle).Append(" / wsie ").Append(_pVillage).Append(" / inne ").Append(_pOther)
                  .Append("; alarm ").Append(_pAlert).Append(", Hold ").Append(_pHold).Append(", ranni > 40% ").Append(_pWound).Append(", kiesa < 100 ").Append(_pPurse)
                  .Append(", z towarem ").Append(_pGoods).Append(", utkniete ").Append(_pStuck).Append("; cel ta osada ").Append(_pTgtHere).Append(" / inna ").Append(_pTgtElse)
                  .Append(" / brak ").Append(_pTgtNone).Append("; stoja od 1+ dob ").Append(_p1).Append(", 7+ dob ").Append(_p7).Append(")")
                  .Append(" | utkniete: pierwszy przebieg ").Append(_q1).Append(" (kandydatow srednio ").Append(_q1 > 0 ? (_q1Cand / (double)_q1).ToString("0.0", Inv) : "-")
                  .Append("), pusty przebieg ").Append(_qE).Append(" (skierowane ").Append(_qEE).Append("), drugi przebieg ").Append(_q2)
                  .Append(", dom bez zysku ").Append(_qH).Append(" (skierowane ").Append(_qHE).Append("), skierowane do miasta spoza ocen BK ").Append(_qOut)
                  .Append(" | po skierowaniu utknietej (").Append(TrackDays).Append(" dob): ocenione ").Append(_oN).Append(", ze sprzedaza ").Append(_oSold)
                  .Append(", znikly ").Append(_oGone).Append(", w toku ").Append(_track.Count)
                  .Append(" | czas ").Append(ms.ToString("0.00", Inv)).Append(" ms | potkniecia ").Append(_stumbles).Append(" (sesja ").Append(_stumbleSess).Append(").");
            }
            catch (Exception e)
            {
                Stumble("Daily", e);
                sb.Clear();
                sb.Append("Karawany - odcinek i utkniete (140): dzien ").Append(day).Append(" | blad linii | potkniecia ").Append(_stumbles).Append(" (sesja ").Append(_stumbleSess).Append(").");
            }
            Log.Info(sb.ToString());
            ZeroDay();
        }
    }
}
