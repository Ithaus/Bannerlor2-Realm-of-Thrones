using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>
    /// PACZKA 286 (28.10, Jeff: "napraw to, moze zold jest za wysoki ochrony? kolejna runda poprawek i test 90 dniowy") - STRAZ WEDLUG SILY DROGI, NAJDROZSI ZA SILE
    /// ODCHODZA PIERWSI.
    /// Dowod (autotest 134a, 30 dob na kopii zapisu Jeffa z 285, Armoury-2026-10-28_13-06-07.log):
    ///  - rozbite karawany 3.83/d (ostatnie 10 dob 4.00) wobec 2.23/d w 133a; doby z lupem band > 0: 26 z 30 (133a: 17) - straz 285 liczy bande tylko w dniu drogi
    ///    od miasta i od celu, a karawana idzie miedzy nimi; udzial majatku v (srednio 0.74-0.78) dobiera straz SLABSZA niz banda, a AI bandy napada wedlug sily
    ///    (DefaultMobilePartyAIModel: atak tylko przy przewadze lokalnej > 1 - ladunek nie gra roli);
    ///  - zold: 'Zold konnych (160)' 109055 - stawki jednostek karawan 108 517 zl/d na 11 208 ludzi = 9.68 zl na czlowieka, partie rodow 630 865 / 119 904 = 5.26,
    ///    garnizony 427 091 / 93 581 = 4.56 (przed -50% BK). Stawka jednostki to model gry (tier 1/2/3/5/8/12/17/23, najemnik x1.5), BK mnozy partie karawany x0.75
    ///    - straznik karawany NIE jest oplacany drozej niz zolnierz tego samego tieru. Drogo jest, bo karawana ma same tiery 3-5 (szablon gry: kupiec zbrojny
    ///    tier 3 (5 zl), straznik karawan tier 4 konny (8), weteran tier 5 konny (12); najemnik karczmy x1.5), a zwolnienie 285 (ReleaseFrom) odprawialo od
    ///    NAJNIZSZEGO tieru albo losowo - zostawali najdrozsi. Zwolnieni 109054: 235 ludzi za 1 887 zl/d = 8.0 zl na czlowieka, mniej niz srednia karawany 8.9.
    /// REGULA (klucz CaravanGuardByRoadStrength, przy CaravanGuardByRisk): kupiec placi za sile, ktorej wymaga droga.
    ///   zagrozenie R = najsilniejsza banda (PartyBase.EstimatedStrength) w promieniu dnia drogi wozu (MarketCarts.PerDay) od ODCINKA miasto - cel (cala droga, nie
    ///   tylko konce); bez celu (postoj) - banda w dniu drogi od miasta. Sila karawany - EstimatedStrength (ta sama miara, ktora AI bandy liczy przewage).
    ///   Najem: ludzi mniej niz podstawa szablonu albo sila < R - z karczmy tylu, ilu wnosi brakujaca sile (moc jednostki z MilitaryPowerModel.GetDefaultTroopPower x
    ///   mnoznik partii k = sila / suma mocy zdrowych), na warunkach gry (cena + 2000 w kiesie, ApplyRecruitMercenary).
    ///   Zwolnienie (przy wyjezdzie, cel znany, raz na dobe): ponad podstawe szablonu i ponad R - odchodza najpierw ci, ktorzy kosztuja najwiecej za jednostke sily
    ///   (stawka jednostki MountedWage.InParty / moc jednostki gry), dopoki sila zostaje >= R. Ta sama droga do domu co 285/166 (wies okregu, karczma, sakiewka).
    ///   Karawany gracza (klucz CaravanGuardPlayerAfterNotice): ta sama regula, ale dopiero od doby PO komunikacie na ekranie (komunikat raz na sesje); kazde
    ///   zwolnienie z komunikatem. Klucz NIE - jak 285 (karawana gracza tylko najem).
    /// Nic z niczego: mniej ludzi = mniej zoldu z kiesy karawany; najem placony jak w grze; zwolnieni z czescia sakiewki do wsi (jak 285).
    /// </summary>
    internal static partial class CaravanGuards285
    {
        private static readonly Dictionary<string, float> _route = new Dictionary<string, float>();   // najsilniejsza banda w dniu drogi od odcinka (pamiec doby)
        private static readonly Dictionary<MobileParty, int> _depDay = new Dictionary<MobileParty, int>();  // doba pierwszej oceny przy wyjezdzie (liczniki 286)
        private static int _noticeDay = -1;   // doba komunikatu graczowi (pamiec sesji; po wczytaniu komunikat jeszcze raz)

        // liczniki doby 286
        private static int _r6Eval, _r6NoStr, _r6Dep, _r6Below, _r6Hire, _r6RelCar, _r6RelMen, _r6Kept, _r6Notice, _r6PlRel, _r6PlWait;
        private static double _r6SumRoute, _r6SumTown, _r6SumStr, _r6RelStr;
        private static long _r6RelWage, _r6RelUnit;

        internal static bool Road286On(Settings s) { return s != null && s.CaravanGuardByRisk && s.CaravanGuardByRoadStrength; }

        private static void Reset286()
        {
            _route.Clear(); _depDay.Clear(); _noticeDay = -1;
            Zero286();
        }

        private static void Zero286()
        {
            _r6Eval = _r6NoStr = _r6Dep = _r6Below = _r6Hire = _r6RelCar = _r6RelMen = _r6Kept = _r6Notice = _r6PlRel = _r6PlWait = 0;
            _r6SumRoute = _r6SumTown = _r6SumStr = _r6RelStr = 0; _r6RelWage = _r6RelUnit = 0;
        }

        /// <summary>Odleglosc punktu p od odcinka a-b (mapa 2D).</summary>
        private static float DistSeg(Vec2 p, Vec2 a, Vec2 b)
        {
            var ab = b - a; float l2 = ab.LengthSquared;
            if (!(l2 > 1e-6f)) return p.Distance(a);
            float t = Vec2.DotProduct(p - a, ab) / l2;
            if (t < 0f) t = 0f; else if (t > 1f) t = 1f;
            return p.Distance(a + ab * t);
        }

        /// <summary>286: najsilniejsza banda (EstimatedStrength) w promieniu dnia drogi od odcinka a - b (cala droga karawany); banda na morzu tylko dla karawany z
        /// okretami (karawana ladowa jej nie spotka). Pamiec doby (para osad + morze).</summary>
        private static float BandRoute(Settlement a, Settlement b, double radius, bool naval)
        {
            if (a == null) return 0f;
            if (b == null || b == a) return BandNear(a, radius);
            string key = a.StringId + ">" + b.StringId + (naval ? "|m" : "");
            float v;
            if (_route.TryGetValue(key, out v)) return v;
            v = 0f;
            var pa = a.GetPosition2D; var pb = b.GetPosition2D;
            float r = (float)radius;
            foreach (var x in MobileParty.AllBanditParties)
            {
                if (x == null || !x.IsActive || x.Party == null) continue;
                if (!naval && x.IsCurrentlyAtSea) continue;
                if (DistSeg(x.GetPosition2D, pa, pb) > r) continue;
                float e = x.Party.EstimatedStrength;
                if (e > v) v = e;
            }
            _route[key] = v;
            return v;
        }

        private sealed class Stack6 { public CharacterObject Ch; public int N; public double Per, Cost; public int W; }

        /// <summary>286: plan zwolnienia - najpierw najdrozsi za jednostke sily (stawka jednostki / moc jednostki gry), dopoki sila zostaje >= R i ludzi >= podstawa.</summary>
        private static Dictionary<CharacterObject, int> Plan286(MobileParty mp, double k, float str, float need, int men, int baseN, out double relStr, out long relUnit)
        {
            relStr = 0; relUnit = 0;
            var r = mp.MemberRoster;
            var pm = Campaign.Current.Models.MilitaryPowerModel;
            CharacterObject master = null;
            try { var o = mp.Party.Owner; master = o != null && o.Culture != null ? o.Culture.CaravanMaster : null; } catch { master = null; }
            var list = new List<Stack6>();
            for (int i = 0; i < r.Count; i++)
            {
                var el = r.GetElementCopyAtIndex(i); var ch = el.Character;
                if (ch == null || ch.IsHero || el.Number <= 0 || (master != null && ch == master)) continue;
                double dp = Math.Max(0.01, pm.GetDefaultTroopPower(ch));
                int w = Math.Max(0, MountedWage.InParty(ch, mp));
                list.Add(new Stack6 { Ch = ch, N = el.Number, W = w, Cost = w / dp, Per = k * dp * Math.Max(0, el.Number - el.WoundedNumber) / el.Number });
            }
            list.Sort((x, y) => { int c = y.Cost.CompareTo(x.Cost); return c != 0 ? c : y.W.CompareTo(x.W); });
            double left = str - need; int free = men - baseN;
            Dictionary<CharacterObject, int> plan = null;
            foreach (var c in list)
            {
                if (free <= 0 || left < 0) break;
                int n = Math.Min(c.N, free);
                if (c.Per > 1e-9) n = Math.Min(n, (int)Math.Floor(left / c.Per + 1e-9));
                if (n <= 0) continue;
                if (plan == null) plan = new Dictionary<CharacterObject, int>();
                plan[c.Ch] = n; left -= n * c.Per; free -= n; relStr += n * c.Per; relUnit += (long)n * c.W;
            }
            return plan;
        }

        /// <summary>286 (z Recruit, klucz CaravanGuardByRoadStrength): najem do sily drogi, zwolnienie najdrozszych za sile przy wyjezdzie. false - cialo gry pominiete,
        /// true - gra jak dotad (karawana bez sily - regula nie dziala).</summary>
        private static bool Recruit286(MobileParty mp, Settlement st, Settings s, int today, bool first, bool player, bool dep, int baseN, int lim)
        {
            var r = mp.MemberRoster;
            var pm = Campaign.Current.Models.MilitaryPowerModel;
            float str = mp.Party.EstimatedStrength;
            double dpSum = 0;
            for (int i = 0; i < r.Count; i++)
            {
                var el = r.GetElementCopyAtIndex(i);
                int h = el.Number - el.WoundedNumber;
                if (el.Character != null && h > 0) dpSum += h * (double)pm.GetDefaultTroopPower(el.Character);
            }
            if (first) _r6Eval++;
            if (!(str > 0f) || !(dpSum > 0)) { if (first) _r6NoStr++; return true; }
            double k = str / dpSum;
            double radius = Math.Max(1.0, MarketCarts.PerDay(s));
            var tgt = mp.TargetSettlement;
            float need = dep ? BandRoute(st, tgt, radius, mp.HasNavalNavigationCapability) : BandNear(st, radius);
            int men = r.TotalManCount;
            if (dep)
            {
                int d0;
                if (!_depDay.TryGetValue(mp, out d0) || d0 != today)
                {
                    _depDay[mp] = today; _r6Dep++;
                    _r6SumRoute += need; _r6SumStr += str;
                    _r6SumTown += Math.Max(BandNear(st, radius), tgt.IsTown ? BandNear(tgt, radius) : 0f);
                    if (str < need) _r6Below++;
                    if (_depDay.Count > 2000) _depDay.Clear();
                }
            }
            if (men < baseN || str < need)
            {
                int h0 = _hired;
                Hire(mp, st, Math.Max(0, baseN - men), Math.Max(0.0, need - str), k);
                if (_hired > h0) _r6Hire += _hired - h0;
                return false;
            }
            if (men <= baseN) { if (men < lim) _blocked++; return false; }
            if (!dep) { if (first && !player) _relWait++; if (men < lim) _blocked++; return false; }
            int r0;
            if (_relDay.TryGetValue(mp, out r0) && r0 == today) return false;
            double relStr; long relUnit;
            var plan = Plan286(mp, k, str, need, men, baseN, out relStr, out relUnit);
            if (plan == null) { _relDay[mp] = today; _r6Kept++; if (men < lim) _blocked++; return false; }
            if (player)
            {
                if (!s.CaravanGuardPlayerAfterNotice) { _relDay[mp] = today; _relPlayerSkip++; return false; }
                if (_noticeDay < 0)
                {
                    _noticeDay = today; _r6Notice++; _relDay[mp] = today;
                    Log.Player("Caravan guards: from tomorrow your caravan masters keep only the guards their road needs - enough to match the strongest bandit band near the road "
                        + "to their next town - and pay off the costliest of the rest in town, like any merchant. (Armoury option: Player Caravans Follow Road Guard Rule)", false, PlayerNews.NotArmy);
                    Log.Info("Straz karawan (286): komunikat graczowi w dobie " + today + " - zwolnienia strazy karawan gracza od doby " + (today + 1) + ".");
                    return false;
                }
                if (today <= _noticeDay) { _relDay[mp] = today; _r6PlWait++; return false; }
            }
            _relDay[mp] = today;
            int wage0 = Math.Max(0, mp.TotalWage);
            int got = ClanBudget.ReleaseCaravanGuards(mp, st, plan);
            if (got <= 0) { _relNone++; return false; }
            int wage1 = Math.Max(0, mp.TotalWage);
            _relCar++; _relMen += got; _relWage += Math.Max(0, wage0 - wage1);   // linia 285 (zwolnieni do domu)
            _r6RelCar++; _r6RelMen += got; _r6RelWage += Math.Max(0, wage0 - wage1); _r6RelStr += relStr; _r6RelUnit += relUnit;
            if (player)
            {
                _r6PlRel += got;
                var inv = CultureInfo.InvariantCulture;
                Log.Player((mp.Name != null ? mp.Name.ToString() : "Your caravan") + ": the caravan master paid off " + got + " of the costliest guards in " + st.Name
                    + " - the road to " + (tgt != null ? tgt.Name.ToString() : "the next town") + " needs strength " + need.ToString("0", inv) + " (strongest bandit band near it), the caravan keeps "
                    + mp.Party.EstimatedStrength.ToString("0", inv) + ". Wages " + wage0 + " -> " + wage1 + " denars a day.", false, PlayerNews.NotArmy);
            }
            return false;
        }

        /// <summary>286: linia doby "Straz karawan wedlug sily drogi (286): dzien N" (po linii 285) - ocena drogi, zwolnienia najdrozszych za sile, karawany gracza,
        /// sklad strazy swiata (stawki jednostek wedlug tieru - odpowiedz na pytanie Jeffa o zold), stary towar w przesycie (CaravanGoods.Seg286).</summary>
        private static void Daily286(Settings s, int day)
        {
            var inv = CultureInfo.InvariantCulture;
            long men = 0, unit = 0; int cars = 0;
            var tn = new long[3]; var tw = new long[3]; long merc = 0, mercW = 0, mounted = 0;
            foreach (var mp in MobileParty.AllCaravanParties)
            {
                if (mp == null || !mp.IsActive || mp.MemberRoster == null) continue;
                cars++;
                var r = mp.MemberRoster;
                for (int i = 0; i < r.Count; i++)
                {
                    var el = r.GetElementCopyAtIndex(i); var ch = el.Character;
                    if (ch == null || ch.IsHero || el.Number <= 0) continue;
                    int w = Math.Max(0, MountedWage.InParty(ch, mp));
                    int b = ch.Tier <= 3 ? 0 : ch.Tier == 4 ? 1 : 2;
                    men += el.Number; unit += (long)el.Number * w; tn[b] += el.Number; tw[b] += (long)el.Number * w;
                    if (ch.Occupation == Occupation.Mercenary) { merc += el.Number; mercW += (long)el.Number * w; }
                    if (ch.IsMounted) mounted += el.Number;
                }
            }
            int nd = Math.Max(1, _r6Dep);
            var sb = new StringBuilder(1200);
            sb.Append("Straz karawan wedlug sily drogi (286): dzien ").Append(day).Append(" | klucze: sila drogi ").Append(Road286On(s) ? "TAK" : "NIE")
              .Append(", karawany gracza po komunikacie ").Append(s.CaravanGuardPlayerAfterNotice ? "TAK" : "NIE")
              .Append(", stary towar w przesycie bez czekania ").Append(s.CaravanAgedGlutSells ? "TAK" : "NIE")
              .Append(", ruda - miasto targowe pelne ").Append(s.KsIronLocalGlut ? "TAK" : "NIE")
              .Append(" | ocen ").Append(_r6Eval).Append(", bez sily ").Append(_r6NoStr).Append(" | przy wyjezdzie ").Append(_r6Dep).Append(": zagrozenie drogi srednio ")
              .Append((_r6SumRoute / nd).ToString("0.0", inv)).Append(" sily (konce drogi 285: ").Append((_r6SumTown / nd).ToString("0.0", inv)).Append("), sila karawany srednio ")
              .Append((_r6SumStr / nd).ToString("0.0", inv)).Append(", slabszych niz banda drogi ").Append(_r6Below)
              .Append(" | najem do sily: ludzi ").Append(_r6Hire)
              .Append(" | zwolnieni najdrozsi za sile: ").Append(_r6RelMen).Append(" ludzi z ").Append(_r6RelCar).Append(" karawan, zold partii -").Append(_r6RelWage)
              .Append(" zl/d, stawki jednostek ").Append(_r6RelUnit).Append(" zl/d (").Append(_r6RelMen > 0 ? ((double)_r6RelUnit / _r6RelMen).ToString("0.00", inv) : "-")
              .Append(" na czlowieka), sila -").Append(_r6RelStr.ToString("0.0", inv)).Append("; nadwyzka ludzi zostaje dla sily drogi ").Append(_r6Kept)
              .Append(" | karawany gracza: komunikat ").Append(_r6Notice).Append(" (doba komunikatu ").Append(_noticeDay >= 0 ? _noticeDay.ToString(inv) : "-")
              .Append("), czeka do doby po komunikacie ").Append(_r6PlWait).Append(", zwolnieni ").Append(_r6PlRel)
              .Append(" | sklad strazy karawan swiata (").Append(cars).Append(" karawan): ludzi ").Append(men).Append(", stawki jednostek ").Append(unit).Append(" zl/d (")
              .Append(men > 0 ? ((double)unit / men).ToString("0.00", inv) : "-").Append(" na czlowieka); tier <=3 ").Append(tn[0]).Append(" (").Append(tw[0])
              .Append(" zl), tier 4 ").Append(tn[1]).Append(" (").Append(tw[1]).Append(" zl), tier 5+ ").Append(tn[2]).Append(" (").Append(tw[2]).Append(" zl), najemnicy ")
              .Append(merc).Append(" (").Append(mercW).Append(" zl), konni ").Append(mounted)
              .Append(" | ").Append(CaravanGoods.Seg286())
              .Append(" | potkniecia ").Append(_stumbles).Append(" (sesja ").Append(_stumbleSess).Append(").");
            Log.Info(sb.ToString());
            Zero286();
        }
    }
}
