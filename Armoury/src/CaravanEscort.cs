using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>
    /// 288 (28.10, Jeff: "no to bez sensu jest obniz koszta karawany o 50% dla calego swiata, czyli kupiec zbrojny 3 straznik 4 weteran 6 i kto pokrywa
    /// wyzywienie i nocleg w karawanie?") - ZOLD ESKORTY KARAWAN I JEDZENIE W DRODZE.
    ///
    /// ZOLD (klucz CaravanWageHalf): sama obnizka siedzi w modelu zoldu (MountedWage.CaravanHalf - postfiks GetTotalWage, linia podpowiedzi "Merchant escort wage"),
    /// tu tylko linia doby: zold karawan swiata przed (MountedWage.FullTotalWage) i po (TotalWage - to, co gra pobiera), na czlowieka i stawki na czlowieka wedlug
    /// tieru (kupiec zbrojny t3, straznik t4, weteran t5; najemnicy karczmy osobno) w karawanach bez bohatera.
    ///
    /// JEDZENIE I NOCLEG - stan przed 288 (dekompilacja 1.4.8): DefaultMobilePartyFoodConsumptionModel.DoesPartyConsumeFood = false dla IsCaravan (BK
    /// BKPartyConsumptionModel, BEE, NavalDLC, ROT, BKROT tylko opakowuja - FoodChain.cs) - ludzie karawany w drodze nie jedli nic i nikt za to nie placil.
    /// W miescie: zold ludzi idzie do ich sakiewki (E3/4 Seal31B, klucz CaravanWageToPurse - notable i lordowie, takze gracz), a przy wyjezdzie karawana wydaje
    /// cala sakiewke "na zycie" (karczma, jedzenie, nocleg - Seal31B.CaravanSpends) do kasy miasta i kiesy ludu. Nocleg i jedzenie w miescie placa wiec straznicy
    /// z wlasnego zoldu - tak zostaje (sredniowiecze: najemnik z dniowki placil gospode).
    /// REGULA (klucz CaravanRoadFood) - kupiec karmi eskorte w drodze (jak w sredniowieczu: kupiec wiozl strawe dla swoich ludzi):
    ///  1. JEDZENIE DOBY (raz na dobe, karawana poza miastem): (ludzie + jency/2) / 20 sztuk zywnosci - ta sama miara co gra (NumberOfMenOnMapToEatOneFood) i bandy
    ///     (BandFood), ulamek na jutro; z jukow karawany, najtansze najpierw (wlasny towar zywnosciowy tez - koszt to towar kupca); ramka ksiegi towarow
    ///     "jedzenie partii" (FFood) z osada = najblizsze miasto (rynek KS krainy). W miescie (CurrentSettlement miasto) ludzie jedza w karczmie z zoldu - bez jukow.
    ///  2. ZAPAS NA DROGE (przy wyjezdzie z miasta, OnSettlementLeft): potrzeba doby x dni drogi do celu (prosta odleglosc / dzien drogi taboru MarketCarts.PerDay -
    ///     wolniejszy niz karawana, wiec z zapasem; bez celu - do najblizszego innego miasta, najmniej 1 dzien) minus zywnosc w jukach; brak dokupuje z polki
    ///     miasta (ShelfBuy.Take - sztuka po sztuce po cenie targu dla tej karawany), najtansza zywnosc najpierw, za kiese karawany (PartyTradeGold) -> kasa miasta
    ///     (+ licznik sprzedazy kupcow TownFolk.MixFood). Nic z niczego: towar schodzi z polki, zloto z kiesy do miasta, zjedzone wychodzi w ramce FFood.
    ///  3. GLOD: brak jedzenia w drodze (pusta kiesa albo polka) - tylko licznik linii doby (bez morale i odejsc: nowa kara byla by nowa regula).
    /// Poza regula: konwoje DTE (nie handluja - Measure31.IsDteConvoy), karawany w bitwie, rozwiazywane, bez ludzi.
    /// Stan (dlug ulamka) tylko w sesji. Linia doby "Karawany - zold eskorty i jedzenie w drodze (288): dzien N".
    /// </summary>
    internal static class CaravanEscort
    {
        private const float MenPerFood = 20f;   // jak gra (DefaultMobilePartyFoodConsumptionModel.NumberOfMenOnMapToEatOneFood) i bandy (BandFood)
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        internal static bool FoodOn { get { var s = Settings.Current; return s != null && s.CaravanRoadFood; } }

        private static readonly Dictionary<MobileParty, float> _owed = new Dictionary<MobileParty, float>();
        private static readonly List<MobileParty> _gone = new List<MobileParty>();

        // liczniki od ostatniej linii doby (zakupy przy wyjezdzie i jedzenie doby)
        private static int _dDep, _dEnough, _dBuyCar, _dNoGoods, _dNoPurse, _dSkipConvoy;
        private static long _dBought, _dGold, _dWantUnits, _dRoadDays, _dStraight;   // 294b: _dStraight - dni drogi wedlug samej prostej (bez pomiaru odcinka)
        private static readonly HashSet<Settlement> _dTowns = new HashSet<Settlement>();
        // 294b (recenzja): droga / prosta - zmierzony odcinek (Stage8World.LegDays) wobec sredniej prostej (dni bez zaokraglenia) wyjazdow z celem, ostatnie 7 dob;
        // jedzenie na droge = prosta do celu x ten wspolczynnik (blad prostej to dlugosc drogi, nie dolna granica)
        private const int RatioRing = 7;
        private static readonly double[] _rsS = new double[RatioRing];
        private static readonly int[] _rsN = new int[RatioRing];
        private static int _rsI, _rsCount, _dRawN;
        private static double _dRawS;
        private static int _stumbles, _sessStumbles;
        private static bool _err;

        internal static void Reset()
        {
            _owed.Clear(); _gone.Clear(); _dTowns.Clear();
            Array.Clear(_rsS, 0, RatioRing); Array.Clear(_rsN, 0, RatioRing); _rsI = _rsCount = _dRawN = 0; _dRawS = 0;   // 294b
            _dDep = _dEnough = _dBuyCar = _dNoGoods = _dNoPurse = _dSkipConvoy = 0;
            _dBought = _dGold = _dWantUnits = _dRoadDays = _dStraight = 0;
            _hDep = _hSkipHungry = 0; _hUnits = _hGold = 0;   // 294c
            _stumbles = _sessStumbles = 0; _err = false;
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++; _sessStumbles++;
            if (!_err) { _err = true; try { Log.Error("CaravanEscort." + where, e); } catch { } }
        }

        /// <summary>Zywnosc doby karawany (sztuki, ulamkowo): ludzie i jency po pol na 20 - jak gra.</summary>
        private static float Need(MobileParty p)
        {
            int men = p.MemberRoster != null ? p.MemberRoster.TotalManCount : 0;
            int pris = p.PrisonRoster != null ? p.PrisonRoster.TotalManCount : 0;
            return Math.Max(1, men + pris / 2) / MenPerFood;
        }

        private static bool Skip(MobileParty p)
        {
            return p == null || !p.IsActive || !p.IsCaravan || p.IsDisbanding || p.MemberRoster == null || p.MemberRoster.TotalManCount <= 0 || p.ItemRoster == null;
        }

        // ------------------------------------------------------------ 2. zapas na droge przy wyjezdzie z miasta
        /// <summary>OnSettlementLeftEvent: karawana wyjezdza z miasta - dokupuje jedzenie na droge do celu za kiese karawany (polka miasta, cena targu).</summary>
        internal static void OnLeft(MobileParty mp, Settlement st)
        {
            try
            {
                if (!FoodOn || Skip(mp) || st == null || !st.IsTown || st.Town == null || st.ItemRoster == null) return;
                if (Campaign.Current == null || !Campaign.Current.GameStarted) return;
                if (Measure31.IsDteConvoy(mp)) { _dSkipConvoy++; return; }
                var s = Settings.Current;
                _dDep++;
                int straight;
                int days = RoadDays(mp, st, s, out straight);
                _dRoadDays += days; _dStraight += straight;
                float need = Need(mp);
                int have = BandFood.FoodUnits(mp.ItemRoster);
                int leg = (int)Math.Ceiling(need * days);
                int want = leg - have;
                // 294c H2 (klucz CaravanRoadFoodTopUp): w miescie bez glodu (migawka KsHunger - czesc kary F = 0) zapas eskorty do spizarni, ktora T1 i (H1) BK / gra
                // trzymaja w jukach (CaravanGoods.PantryUnits = ceil(-FoodChange x 10)); w miescie glodnym tylko na odcinek (jak 288) - karawana nie wyjada glodnego miasta
                bool top = false;
                if (s != null && s.CaravanRoadFoodTopUp)
                {
                    if (KsHunger.IsHungry(st.Town)) _hSkipHungry++;
                    else
                    {
                        int pan = CaravanGoods.PantryUnits(mp);
                        if (pan - have > want) { want = pan - have; top = true; }
                    }
                }
                if (want <= 0) { _dEnough++; return; }
                _dWantUnits += want;
                if (mp.PartyTradeGold <= 0) { _dNoPurse++; return; }
                bool poor;
                long g0 = _dGold;
                int got = Buy(mp, st, want, out poor);
                if (got > 0) { _dBuyCar++; _dTowns.Add(st); }
                if (got < want) { if (poor) _dNoPurse++; else _dNoGoods++; }
                if (top)
                {
                    int extra = Math.Max(0, got - Math.Max(0, leg - have));   // ponad potrzebe odcinka (ta sama cena - Buy kupuje sztuka po sztuce)
                    if (extra > 0) { _hDep++; _hUnits += extra; _hGold += got > 0 ? (long)Math.Round((_dGold - g0) * (double)extra / got) : 0; }
                }
            }
            catch (Exception e) { Stumble("OnLeft", e); }
        }

        // 294c H2: zapas eskorty w miastach bez glodu - wyjazdy z dokupionym zapasem ponad odcinek, sztuki, zloto (czesc ceny zakupu), pominiete w miastach glodnych
        private static int _hDep, _hSkipHungry;
        private static long _hUnits, _hGold;

        /// <summary>294c (CaravanRoute294c, D1): dni drogi karawany do miasta - ta sama miara co jedzenie na droge (RoadDays): prosta odleglosc z miejsca karawany
        /// (miasto, w ktorym stoi, albo pozycja na mapie) / dzien drogi taboru x wspolczynnik droga / prosta (294b, 1..2); bez pomiaru - sama prosta. Bez zaokraglenia.</summary>
        internal static double RoadDaysTo(MobileParty mp, Settlement to)
        {
            if (mp == null || to == null) return 0.0;
            var s = Settings.Current;
            double perDay = Math.Max(1.0, MarketCarts.PerDay(s));
            var cs = mp.CurrentSettlement;
            Vec2 from = cs != null ? cs.GetPosition2D : mp.GetPosition2D;
            double raw = from.Distance(to.GetPosition2D) / perDay;
            double leg = s != null && s.CaravanRoadFoodMeasuredLeg ? Stage8World.LegDays : 0.0;
            double r = leg > 0.0 && !double.IsInfinity(leg) ? Ratio(leg) : 0.0;
            return r > 0.0 ? raw * r : raw;
        }

        /// <summary>Dni drogi do celu: prosta odleglosc miasto - cel / dzien drogi taboru (MarketCarts.PerDay), w gore; bez celu (albo cel = to miasto) - do
        /// najblizszego innego miasta. Najmniej 1. 294b (klucz CaravanRoadFoodMeasuredLeg): prosta do celu x wspolczynnik droga / prosta (Ratio: zmierzony odcinek
        /// karawan Stage8World.LegDays - karawany AI swiata / wyjazdy z miast, 7 dob - wobec sredniej prostej wyjazdow z celem, 7 dob; 296a: 4.2-4.4 doby przy prostej
        /// 3.3-3.5), w gore (recenzja 294b: nie max(prosta, srednia) - krotki skok nie kupuje na sredni odcinek, dlugi nie kupuje za malo); bez celu - zmierzony odcinek
        /// zamiast najblizszego miasta; bez pomiaru (pierwsze doby sesji) - jak 288. straight - dni wedlug samej prostej (linia doby).</summary>
        private static int RoadDays(MobileParty mp, Settlement st, Settings s, out int straight)
        {
            double perDay = Math.Max(1.0, MarketCarts.PerDay(s));
            Vec2 from = st.GetPosition2D;
            float dist = -1f;
            var tg = mp.TargetSettlement;
            bool target = tg != null && tg != st;
            if (target) dist = from.Distance(tg.GetPosition2D);
            else
            {
                float best = float.MaxValue;
                foreach (var t in Town.AllTowns)
                {
                    var ts = t != null ? t.Settlement : null;
                    if (ts == null || ts == st) continue;
                    float d = from.Distance(ts.GetPosition2D);
                    if (d < best) best = d;
                }
                if (best < float.MaxValue) dist = best;
            }
            double raw = dist > 0f ? dist / perDay : 0.0;
            straight = dist <= 0f ? 1 : Math.Max(1, (int)Math.Ceiling(raw));
            if (target && raw > 0.0) { _dRawS += raw; _dRawN++; }
            double leg = s != null && s.CaravanRoadFoodMeasuredLeg ? Stage8World.LegDays : 0.0;
            if (!(leg > 0.0) || double.IsInfinity(leg)) return straight;
            if (!target) return Math.Max(1, (int)Math.Ceiling(Math.Min(60.0, leg)));
            double r = Ratio(leg);
            if (!(r > 0.0) || raw <= 0.0) return straight;
            return Math.Max(1, (int)Math.Ceiling(Math.Min(60.0, raw * r)));
        }

        /// <summary>294b (recenzja): droga / prosta = zmierzony odcinek / srednia prosta (dni) wyjazdow z celem ostatnich 7 dob, w granicach 1..2; 0 - bez pomiaru.</summary>
        private static double Ratio(double leg)
        {
            if (!(leg > 0.0) || _rsCount <= 0) return 0.0;
            double sS = 0; long sN = 0;
            for (int i = 0; i < _rsCount; i++) { sS += _rsS[i]; sN += _rsN[i]; }
            if (sN <= 0 || !(sS > 0.0)) return 0.0;
            return Math.Max(1.0, Math.Min(2.0, leg / (sS / sN)));
        }

        /// <summary>294b: probka doby sredniej prostej wyjazdow z celem (linia doby 288).</summary>
        private static void NoteRatioDay()
        {
            if (_dRawN <= 0) return;
            _rsS[_rsI] = _dRawS; _rsN[_rsI] = _dRawN;
            _rsI = (_rsI + 1) % RatioRing;
            if (_rsCount < RatioRing) _rsCount++;
        }

        /// <summary>Zakup z polki miasta: najtansza zywnosc (wartosc), sztuka po sztuce po cenie targu dla tej karawany, za kiese karawany -> kasa miasta.
        /// poor - zabraklo kiesy (a nie towaru).</summary>
        private static int Buy(MobileParty mp, Settlement town, int want, out bool poor)
        {
            poor = false;
            var shelf = town.ItemRoster;
            var tw = town.Town;
            int got = 0;
            for (int round = 0; round < 6 && got < want; round++)
            {
                int budget = mp.PartyTradeGold;
                if (budget <= 0) { poor = true; break; }
                int best = -1, bv = int.MaxValue;
                for (int i = 0; i < shelf.Count; i++)
                {
                    var el = shelf.GetElementCopyAtIndex(i);
                    var it = el.EquipmentElement.Item;
                    if (it == null || el.Amount <= 0 || !it.IsFood || it.NotMerchandise || el.EquipmentElement.IsQuestItem) continue;
                    if (it.Value < bv) { bv = it.Value; best = i; }
                }
                if (best < 0) break;
                var cel = shelf.GetElementCopyAtIndex(best).EquipmentElement;
                int cost, first, last;
                int n = ShelfBuy.Take(shelf, cel, want - got, budget, () => Math.Max(1, tw.GetItemPrice(cel, mp, false)), out cost, out first, out last, 0, false);
                if (n <= 0) { poor = budget < Math.Max(1, tw.GetItemPrice(cel, mp, false)); break; }
                mp.ItemRoster.AddToCounts(cel, n);
                if (cost > 0)
                {
                    mp.PartyTradeGold -= cost;
                    town.SettlementComponent.ChangeGold(cost);                        // kiesa karawany -> kasa miasta (kupcy sprzedali strawe na droge)
                    try { TownFolk.Sale(town, cost, TownFolk.MixFood); } catch { }    // sprzedaz kupcow miasta (podstawa robocizny handlu) - jak bandy i zaopatrzenie BK
                }
                got += n; _dBought += n; _dGold += cost;
            }
            return got;
        }

        // ------------------------------------------------------------ 1. jedzenie doby i linia doby
        private sealed class Tier { public long Men; public double After, Before; }

        internal static void Daily()
        {
            var s = Settings.Current;
            if (s == null || Campaign.Current == null) return;
            bool food = FoodOn;
            int day = (int)CampaignTime.Now.ToDays;
            int cars = 0, carsHero = 0, eatN = 0, inTown = 0, battle = 0, convoy = 0, fed = 0, hungry = 0, empty = 0;
            long men = 0, wAfter = 0, wBefore = 0, eaten = 0, lack = 0, stock = 0;
            double needSum = 0;
            var tiers = new[] { new Tier(), new Tier(), new Tier() };
            var merc = new Tier();
            foreach (var mp in MobileParty.AllCaravanParties)
            {
                if (Skip(mp)) continue;
                try
                {
                    cars++;
                    int m = mp.MemberRoster.TotalManCount;
                    men += m;
                    // zold: po (TotalWage) i przed (bez polowy eskorty)
                    int after = Math.Max(0, mp.TotalWage), before = Math.Max(0, MountedWage.FullTotalWage(mp));
                    wAfter += after; wBefore += before;
                    if (mp.MemberRoster.TotalHeroes > 0) carsHero++;
                    else
                    {
                        // stawki na czlowieka: podstawa BK jednostki x (wynik partii / podstawa partii) - czynniki BK partii po rowno na kazdego
                        float tb = MountedWage.TroopBase(mp);
                        if (tb > 0f)
                        {
                            float bw = CompanionWage.BaseWageNow();
                            var r = mp.MemberRoster;
                            for (int i = 0; i < r.Count; i++)
                            {
                                var el = r.GetElementCopyAtIndex(i); var ch = el.Character;
                                if (ch == null || ch.IsHero || el.Number <= 0) continue;
                                double pm = CompanionWage.BkPerMan(MountedWage.InParty(ch, mp), bw);
                                var t = ch.Occupation == Occupation.Mercenary ? merc : tiers[ch.Tier <= 3 ? 0 : ch.Tier == 4 ? 1 : 2];
                                t.Men += el.Number; t.After += el.Number * pm * after / tb; t.Before += el.Number * pm * before / tb;
                            }
                        }
                    }
                    if (!food) continue;
                    if (Measure31.IsDteConvoy(mp)) { convoy++; continue; }
                    if (mp.MapEvent != null) { battle++; continue; }
                    var cs = mp.CurrentSettlement;
                    if (cs != null && cs.IsTown) { inTown++; continue; }   // w miescie ludzie jedza i spia w karczmie z zoldu (E3/4 "na zycie")
                    eatN++;
                    float need = Need(mp);
                    needSum += need;
                    float owed; _owed.TryGetValue(mp, out owed);
                    owed += need;
                    int units = (int)owed;
                    int have = BandFood.FoodUnits(mp.ItemRoster);
                    int eat = Math.Min(units, have);
                    if (eat > 0)
                    {
                        Settlement town = null;
                        try { town = OutlawLaw.NearestTownOf(mp.Position.ToVec2()); } catch { town = null; }
                        eat = Eat(mp, eat, town);
                    }
                    _owed[mp] = Math.Max(0f, owed - units);   // brak dzis nie przechodzi na jutro (glod liczony), ulamek tak
                    eaten += eat; have -= eat; stock += Math.Max(0, have);
                    if (eat >= units || have > 0) fed++;
                    else { hungry++; lack += units - eat; if (have <= 0 && eat <= 0) empty++; }
                }
                catch (Exception e) { Stumble("Daily", e); }
            }
            // pamiec ulamkow - karawany, ktorych juz nie ma
            if (_owed.Count > 0)
            {
                _gone.Clear();
                foreach (var kv in _owed) if (kv.Key == null || !kv.Key.IsActive) _gone.Add(kv.Key);
                foreach (var g in _gone) _owed.Remove(g);
                _gone.Clear();
            }
            Func<Tier, string> R = t => t.Men > 0 ? (t.After / t.Men).ToString("0.00", Inv) + " (przed " + (t.Before / t.Men).ToString("0.00", Inv) + ", ludzi " + t.Men + ")" : "-";
            var sb = new StringBuilder(900);
            sb.Append("Karawany - zold eskorty i jedzenie w drodze (288): dzien ").Append(day)
              .Append(" | klucze: polowa zoldu eskorty ").Append(MountedWage.CaravanHalfOn ? "TAK" : "NIE").Append(", jedzenie w drodze ").Append(food ? "TAK" : "NIE")
              .Append(" | zold karawan swiata: karawan ").Append(cars).Append(" (z bohaterem ").Append(carsHero).Append("), ludzi ").Append(men)
              .Append(", zl/d przed ").Append(wBefore).Append(", po ").Append(wAfter)
              .Append(" (x").Append(wBefore > 0 ? ((double)wAfter / wBefore).ToString("0.000", Inv) : "-").Append(")")
              .Append(", na czlowieka przed ").Append(men > 0 ? ((double)wBefore / men).ToString("0.00", Inv) : "-").Append(", po ").Append(men > 0 ? ((double)wAfter / men).ToString("0.00", Inv) : "-")
              .Append(" | stawki na czlowieka po (karawany bez bohatera): kupiec zbrojny t1-3 ").Append(R(tiers[0])).Append(", straznik t4 ").Append(R(tiers[1]))
              .Append(", weteran t5+ ").Append(R(tiers[2])).Append(", najemnicy karczmy ").Append(R(merc))
              .Append(" | jedzenie: w drodze ").Append(eatN).Append(" karawan, w miescie (karczma z zoldu) ").Append(inTown).Append(", w bitwie ").Append(battle).Append(", konwoje DTE ").Append(convoy)
              .Append("; potrzeba ").Append(needSum.ToString("0.0", Inv)).Append(" szt., zjedzone ").Append(eaten).Append(", najedzone ").Append(fed).Append(", glodne ").Append(hungry)
              .Append(" (brak ").Append(lack).Append(" szt., bez jedzenia ").Append(empty).Append("), zywnosc w jukach po jedzeniu ").Append(stock)
              .Append(" | zapas na droge przy wyjezdzie: wyjazdow ").Append(_dDep).Append(" (dni drogi srednio ").Append(_dDep > 0 ? ((double)_dRoadDays / _dDep).ToString("0.0", Inv) : "-")
              .Append(", prosta ").Append(_dDep > 0 ? ((double)_dStraight / _dDep).ToString("0.0", Inv) : "-")                                     // 294b
              .Append(", zmierzona ").Append(Stage8World.LegDays > 0.0 ? Stage8World.LegDays.ToString("0.0", Inv) : "-")
              .Append(", droga/prosta ").Append(Ratio(Stage8World.LegDays) > 0.0 ? Ratio(Stage8World.LegDays).ToString("0.00", Inv) : "-")      // 294b recenzja
              .Append("), z zapasem w jukach ").Append(_dEnough).Append(", kupilo ").Append(_dBuyCar).Append(" karawan ").Append(_dBought).Append(" z ").Append(_dWantUnits)
              .Append(" szt. za ").Append(_dGold).Append(" zl z kies karawan do kas ").Append(_dTowns.Count).Append(" miast, bez towaru ").Append(_dNoGoods).Append(", bez kiesy ").Append(_dNoPurse)
              .Append(", konwoje DTE ").Append(_dSkipConvoy);
            // 294c: spizarnia eskorty w sprzedazy BK i gry (H1), zapas eskorty w miastach bez glodu (H2) - segment przed potknieciami (wyrazenie K288_RE z opcjonalna grupa)
            long h1b, h1v;
            CaravanTrade284.TakePantryHeld(out h1b, out h1v);
            sb.Append(" | 294c: spizarnia - BK ").Append(h1b).Append(" szt., gra ").Append(h1v).Append(" szt. wstrzymane (klucz ").Append(s.CaravanPantryAllSales ? "TAK" : "NIE")
              .Append("); zapas w miastach bez glodu: wyjazdow ").Append(_hDep).Append(", szt. ").Append(_hUnits).Append(", zl ").Append(_hGold)
              .Append("; pominiete glodne ").Append(_hSkipHungry).Append(" (klucz ").Append(s.CaravanRoadFoodTopUp ? "TAK" : "NIE").Append(')');
            sb.Append(" | potkniecia ").Append(_stumbles).Append(" (zold 288 ").Append(MountedWage.CarStumbles).Append("), w sesji ").Append(_sessStumbles);
            Log.Info(sb.ToString());
            _hDep = _hSkipHungry = 0; _hUnits = _hGold = 0;
            NoteRatioDay(); _dRawS = 0; _dRawN = 0;   // 294b
            _dDep = _dEnough = _dBuyCar = _dNoGoods = _dNoPurse = _dSkipConvoy = 0;
            _dBought = _dGold = _dWantUnits = _dRoadDays = _dStraight = 0;
            _dTowns.Clear();
            _stumbles = 0;
        }

        /// <summary>Zjada n sztuk z jukow, najtansze najpierw, w ramce ksiegi towarow "jedzenie partii" z osada = najblizsze miasto (jak BandFood.Eat).</summary>
        private static int Eat(MobileParty p, int n, Settlement town)
        {
            var pack = p.ItemRoster;
            int done = 0;
            var gf = town != null ? GoodsLedger.Begin(GoodsLedger.FFood, town, p) : GoodsLedger.Begin(GoodsLedger.FFood, p);
            try
            {
                for (int round = 0; round < 8 && done < n; round++)
                {
                    int best = -1, bv = int.MaxValue;
                    for (int i = 0; i < pack.Count; i++)
                    {
                        var el = pack.GetElementCopyAtIndex(i);
                        var it = el.EquipmentElement.Item;
                        if (it == null || el.Amount <= 0 || !it.IsFood || it.NotMerchandise || el.EquipmentElement.IsQuestItem) continue;
                        if (it.Value < bv) { bv = it.Value; best = i; }
                    }
                    if (best < 0) break;
                    var e0 = pack.GetElementCopyAtIndex(best);
                    int k = Math.Min(n - done, e0.Amount);
                    if (k <= 0) break;
                    pack.AddToCounts(e0.EquipmentElement, -k);
                    done += k;
                }
            }
            finally { GoodsLedger.End(gf); }
            return done;
        }
    }
}
