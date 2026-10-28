using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// PACZKA 285 (28.10, Jeff: "Rob to!" o "karawane nadal moze zjadac zold straznikow") - STRAZ KARAWANY WEDLUG DROGI I MAJATKU.
    /// Dowod (autotest 133a, 30 dob na kopii zapisu Jeffa, Armoury-2026-10-28_02-13-05.log, doba 109053-109054): 451-453 karawan, 16 625 ludzi (36.9 na karawane,
    /// konnych 33.9%), zold karawan notabli nalezny 132 148 zl/d + lordow 16 415 zl/d = ok. 148.6 tys. zl/d wobec wyniku handlu +87 111 zl/d ("Karawany - wynik
    /// handlowy (f4-pomiary)"); "Wielkosc karawan (284, na sucho)": zysk handlu/d ponizej zoldu/d 412 z 438 karawan. Postoj w miastach srednio 2.2 h (20 z 450 karawan
    /// w miescie) - zold za "bezczynne doby w miescie" NIE jest przyczyna. Przyczyna: gra werbuje karawanie najemnikow z karczmy az do limitu partii
    /// (RecruitmentCampaignBehavior.CheckRecruiting: notabl 20 + 10/20/30 wedlug wplywow, gracz 10/30) bez wzgledu na droge i ladunek; zysk handlu tego nie unosi.
    /// REGULA (klucz CaravanGuardByRisk; wolane z CaravanTrade284.SizePrefix - prefiks CheckRecruiting karawany w miescie, co godzine i przy wjezdzie):
    ///   podstawa = bohaterowie + dolna granica szablonu karawany kultury (PartyTemplateObject.GetLowerTroopLimit - z tym karawana wyrusza w swiat, model gry);
    ///   zagrozenie = najsilniejsza banda (PartyBase.EstimatedStrength - ta sama miara, ktora AI gry ocenia napad) w promieniu dnia drogi (MarketCarts.PerDay - model
    ///   wozow) od tego miasta i od miasta celu karawany; ludzi na nie = ceil(sila bandy / sila karawany na glowe);
    ///   udzial majatku v = min(1, (kiesa + towar w jukach wedlug wartosci gry) / kapital karawany (CaravanSettle.CapFloor - te same liczby co 259));
    ///   potrzeba = podstawa + ceil(max(0, zagrozenie - podstawa) x v), najwyzej limit partii gry. Wojska wrogich krolestw straz nie powstrzyma (gra kaze karawanie
    ///   uciekac) - nie liczone.
    /// Najem: tylko do potrzeby, ta sama karczma, cena i zapas kiesy co gra (2000 zl ponad cene - CheckRecruiting), ta sama akcja gry (ApplyRecruitMercenary -
    /// latki weteranow E4/10, oplata do sakiewek najetych E5/B6 i kon z polki 157 dzialaja bez zmian); cialo gry pominiete (false). Zwolnienie: raz na dobe na
    /// karawane AI, nadwyzka ponad potrzebe do domu ta sama droga co zwolnieni 166 (ClanBudget.ReleaseCaravanGuards - ludzie do wsi okregu tego miasta, czesc do
    /// karczmy E4/10, sakiewka z ludzmi). Karawany gracza: najem do potrzeby, bez zwolnienia (straz kupil gracz). Zold placi wlasciciel jak dotad - zmienia sie
    /// tylko liczba ludzi; nic z niczego, nic nie znika (zold mniejszy = mniej zlota z kies karawan do sakiewek strazy).
    /// Recenzja: przy wjezdzie celem karawany jest to miasto, a BK wybiera nastepny cel i rusza w jednym tiku (po CheckRecruiting tej godziny) - ocena z banda
    /// celu idzie w postfiksie BK HourlyTickPartyImpl (karawana jeszcze w miescie, cel ustawiony); zwolnienie tylko wtedy (cel znany), bez celu nadwyzka czeka.
    /// Klucz NIE - tylko pomiar (potrzeba liczona, gra werbuje jak dotad). Linia doby "Straz karawan (285): dzien N". Wyjatek - licznik potkniec, gra jak dotad.
    /// </summary>
    internal static partial class CaravanGuards285
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private const int GameKeepGold = 2000;   // RecruitmentCampaignBehavior.CheckRecruiting: karawana werbuje tylko przy kiesie > cena + 2000 (stala gry)

        private static int _day = -1;
        private static readonly Dictionary<Settlement, float> _band = new Dictionary<Settlement, float>();     // najsilniejsza banda w dniu drogi (pamiec doby)
        private static readonly Dictionary<MobileParty, int> _seen = new Dictionary<MobileParty, int>();       // doba pierwszej oceny karawany (liczniki doby)
        private static readonly Dictionary<MobileParty, int> _relDay = new Dictionary<MobileParty, int>();     // recenzja: doba zwolnienia karawany (raz na dobe, przy wyjezdzie)
        private static MethodInfo _hire; private static bool _hireTried;
        private static readonly HashSet<string> _err = new HashSet<string>();
        internal static bool DepartWired;   // recenzja: postfiks BK BKCaravansBehavior.HourlyTickPartyImpl wpiety (ocena przy wyjezdzie z celem)

        // liczniki doby (linia "Straz karawan (285)")
        private static int _eval, _evalPlayer, _noRule, _atLimitBase, _hireVisits, _hired, _blocked, _relCar, _relMen, _relNone, _relPlayerSkip, _short, _stumbles, _stumbleSess;
        private static int _dep, _depTgt, _relWait;   // recenzja: oceny przy wyjezdzie (cel znany), w tym banda celu silniejsza niz miasta; nadwyzka bez celu - zwolnienie odlozone
        private static long _hireCost, _relWage;
        private static double _sNeed, _sBase, _sThreat, _sV, _sLim, _sMen;
        private static long _ticks;

        internal static void Reset()
        {
            _day = -1; _band.Clear(); _seen.Clear(); _relDay.Clear(); _err.Clear(); _stumbleSess = 0;
            ZeroDay();
            Reset286();
        }

        private static void ZeroDay()
        {
            _eval = _evalPlayer = _noRule = _atLimitBase = _hireVisits = _hired = _blocked = _relCar = _relMen = _relNone = _relPlayerSkip = _short = _stumbles = 0;
            _dep = _depTgt = _relWait = 0;
            _hireCost = _relWage = 0; _sNeed = _sBase = _sThreat = _sV = _sLim = _sMen = 0; _ticks = 0;
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++; _stumbleSess++;
            try { if (_err.Add(where)) Log.Error("CaravanGuards285." + where, e); } catch { }
        }

        private static void NewDayIfNeeded(int today)
        {
            if (today == _day) return;
            _day = today; _band.Clear(); _route.Clear();
            if (_seen.Count > 2000) _seen.Clear();
            if (_relDay.Count > 2000) _relDay.Clear();
        }

        /// <summary>Najsilniejsza banda (EstimatedStrength) w promieniu dnia drogi od osady - pamiec doby.</summary>
        private static float BandNear(Settlement st, double radius)
        {
            float v;
            if (st == null) return 0f;
            if (_band.TryGetValue(st, out v)) return v;
            v = 0f;
            var pos = st.GetPosition2D;
            float r = (float)radius;
            foreach (var b in MobileParty.AllBanditParties)
            {
                if (b == null || !b.IsActive || b.Party == null) continue;
                if (b.GetPosition2D.Distance(pos) > r) continue;
                float x = b.Party.EstimatedStrength;
                if (x > v) v = x;
            }
            _band[st] = v;
            return v;
        }

        /// <summary>Dolna granica szablonu karawany kultury domu karawany (zwykla albo zbrojna, z okretami albo ladowa - jak przy tworzeniu); -1 - brak szablonu.</summary>
        private static int TemplateBase(MobileParty mp, Settlement at)
        {
            var home = mp.HomeSettlement ?? at;
            var cu = home != null ? home.Culture : null;
            if (cu == null) return -1;
            bool elite = mp.CaravanPartyComponent != null && mp.CaravanPartyComponent.IsElite;
            var list = elite ? cu.EliteCaravanPartyTemplates : cu.CaravanPartyTemplates;
            if (list == null || list.Count == 0) list = cu.CaravanPartyTemplates;
            if (list == null || list.Count == 0) return -1;
            bool naval = mp.HasNavalNavigationCapability;
            int best = -1, any = -1;
            foreach (var t in list)
            {
                if (t == null || t.Stacks == null) continue;
                int lo = t.GetLowerTroopLimit();
                if (any < 0 || lo < any) any = lo;
                bool tNaval = t.ShipHulls != null && t.ShipHulls.Count > 0;
                if (tNaval == naval && (best < 0 || lo < best)) best = lo;
            }
            return best >= 0 ? best : any;
        }

        /// <summary>Potrzeba strazy (ludzie razem z bohaterami); -1 - regula nie dziala dla tej karawany (brak szablonu albo sily) - gra jak dotad.</summary>
        internal static int Need(MobileParty mp, Settlement at, Settings s, out int baseN, out int threat, out double v, out int lim)
        {
            baseN = 0; threat = 0; v = 0; lim = 0;
            if (mp == null || at == null || mp.Party == null || mp.MemberRoster == null) return -1;
            lim = mp.Party.PartySizeLimit;
            int tb = TemplateBase(mp, at);
            if (tb < 0) return -1;
            baseN = mp.MemberRoster.TotalHeroes + tb;
            if (baseN >= lim) return lim;
            int healthy = mp.Party.NumberOfHealthyMembers;
            float str = mp.Party.EstimatedStrength;
            if (healthy <= 0 || !(str > 0f)) return -1;
            double perMan = str / healthy;
            double radius = Math.Max(1.0, MarketCarts.PerDay(s));
            float band;
            var tgt = mp.TargetSettlement;
            bool r286 = Road286On(s);
            // 286 (klucz CaravanGuardByRoadStrength): przy znanym celu - najsilniejsza banda w dniu drogi od CALEJ drogi (odcinek miasto - cel), nie tylko od jej koncow
            if (r286 && tgt != null && tgt != at) band = BandRoute(at, tgt, radius, mp.HasNavalNavigationCapability);
            else
            {
                band = BandNear(at, radius);
                if (tgt != null && tgt != at && tgt.IsTown) band = Math.Max(band, BandNear(tgt, radius));
            }
            threat = band > 0f ? (int)Math.Ceiling(band / perMan) : 0;
            int cap, floor;
            CaravanSettle.CapFloor(mp, out cap, out floor);
            long wealth = Math.Max(0, mp.PartyTradeGold);
            var r = mp.ItemRoster;
            if (r != null)
                for (int i = 0; i < r.Count; i++)
                {
                    var el = r.GetElementCopyAtIndex(i);
                    if (el.Amount > 0 && el.EquipmentElement.Item != null) wealth += (long)el.Amount * Math.Max(0, el.EquipmentElement.ItemValue);
                }
            v = cap > 0 ? Math.Min(1.0, wealth / (double)cap) : 1.0;
            // 286: bez udzialu majatku - banda AI napada wedlug sily (przewaga lokalna > 1, DefaultMobilePartyAIModel), nie wedlug ladunku; v zostaje w linii (pomiar)
            int need = baseN + (int)Math.Ceiling(Math.Max(0, threat - baseN) * (r286 ? 1.0 : v));
            return Math.Min(need, lim);
        }

        private static bool PlayerOwned(MobileParty mp)
        {
            try { return mp.Party.Owner == Hero.MainHero || mp.ActualClan == Clan.PlayerClan; } catch { return false; }
        }

        /// <summary>CaravanTrade284.SizePrefix (prefiks CheckRecruiting, karawana w miescie). true - cialo gry jak dotad (klucz NIE albo regula nie dziala dla tej karawany).</summary>
        internal static bool Recruit(MobileParty mp, Settlement st)
        {
            var s = Settings.Current;
            if (s == null || mp == null || st == null || st.Town == null) return true;
            long t0 = Stopwatch.GetTimestamp();
            try
            {
                int today = (int)CampaignTime.Now.ToDays;
                NewDayIfNeeded(today);
                int baseN, threat, lim; double v;
                int need = Need(mp, st, s, out baseN, out threat, out v, out lim);
                bool first;
                int d0;
                first = !_seen.TryGetValue(mp, out d0) || d0 != today;
                if (first) _seen[mp] = today;
                if (need < 0) { if (first) _noRule++; return true; }
                int men = mp.MemberRoster.TotalManCount;
                bool player = PlayerOwned(mp);
                if (first)
                {
                    _eval++; if (player) _evalPlayer++;
                    _sNeed += need; _sBase += baseN; _sThreat += threat; _sV += v; _sLim += lim; _sMen += men;
                    if (baseN >= lim) _atLimitBase++;
                    if (men < need) _short++;
                }
                // recenzja: cel nastepnego odcinka znany (BK ustawil go w miescie - postfiks HourlyTickPartyImpl, albo gra po ThinkNextDestination) - ocena
                // przy wyjezdzie; przy wjezdzie celem jest to miasto i potrzeba widzi tylko jego bande
                var tgt = mp.TargetSettlement;
                bool dep = tgt != null && tgt != st;
                if (dep)
                {
                    _dep++;
                    if (tgt.IsTown && BandNear(tgt, Math.Max(1.0, MarketCarts.PerDay(s))) > BandNear(st, Math.Max(1.0, MarketCarts.PerDay(s)))) _depTgt++;
                }
                if (!s.CaravanGuardByRisk) return true;   // klucz NIE - tylko pomiar
                if (Road286On(s)) return Recruit286(mp, st, s, today, first, player, dep, baseN, lim);   // 286: sila drogi, najdrozsi za sile odchodza pierwsi
                if (men > need)
                {
                    // recenzja: zwolnienie tylko przy wyjezdzie (potrzeba z banda miasta i celu), raz na dobe; bez celu (wjazd, postoj) straz czeka - inaczej odcinek
                    // do groznego celu szedl ze straza dobrana do bandy samego miasta
                    int r0;
                    if (!dep) { if (first && !player) _relWait++; }
                    else if (!_relDay.TryGetValue(mp, out r0) || r0 != today)
                    {
                        _relDay[mp] = today;
                        if (player) _relPlayerSkip++;
                        else
                        {
                            int wage0 = Math.Max(0, mp.TotalWage);
                            int got = ClanBudget.ReleaseCaravanGuards(mp, st, men - need);
                            if (got > 0) { _relCar++; _relMen += got; if (men > 0) _relWage += (long)Math.Round((double)wage0 * got / men); }
                            else _relNone++;
                        }
                    }
                    return false;
                }
                if (men == need) { if (men < lim) _blocked++; return false; }
                Hire(mp, st, need - men);
                return false;
            }
            catch (Exception e) { Stumble("Recruit", e); return true; }
            finally { _ticks += Stopwatch.GetTimestamp() - t0; }
        }

        /// <summary>Recenzja 285: postfiks BK BKCaravansBehavior.HourlyTickPartyImpl - BK w miescie robi BuyGoods -> ThinkNextDestination -> SetMoveGoToSettlement(cel)
        /// w jednym tiku, a CheckRecruiting gry szedl w tej godzinie wczesniej (cel = to miasto) i po wyjezdzie juz nie (karawana dalej niz 1 od miasta). Tu karawana
        /// jeszcze stoi w miescie z nowym celem - ta sama ocena co w CheckRecruiting (warunki gry: miasto bez oblezenia, bez bitwy), z banda celu.</summary>
        public static void DepartPostfix(MobileParty __0)
        {
            try
            {
                var mp = __0;
                if (mp == null || !mp.IsCaravan || !mp.IsActive || mp.MapEvent != null || mp == MobileParty.MainParty) return;
                var st = mp.CurrentSettlement;
                if (st == null || !st.IsTown || st.Town == null || st.IsUnderSiege) return;
                var tgt = mp.TargetSettlement;
                if (tgt == null || tgt == st) return;
                Recruit(mp, st);
            }
            catch (Exception e) { Stumble("DepartPostfix", e); }
        }

        internal static void Apply(Harmony h)
        {
            try
            {
                var bkc = AccessTools.TypeByName("BannerKings.Behaviours.BKCaravansBehavior");
                var m = bkc != null ? AccessTools.DeclaredMethod(bkc, "HourlyTickPartyImpl", new[] { typeof(MobileParty) }) : null;
                if (m != null) { h.Patch(m, postfix: new HarmonyMethod(typeof(CaravanGuards285), nameof(DepartPostfix))); DepartWired = true; }
            }
            catch (Exception e) { Log.Error("CaravanGuards285.Apply", e); }
        }

        /// <summary>Najem z karczmy do potrzeby - warunki gry (karczma z najemnikami straz karawan albo najemnik, kiesa > cena + 2000), akcja gry ApplyRecruitMercenary.</summary>
        private static void Hire(MobileParty mp, Settlement st, int want, double strGap = 0, double k = 0)
        {
            if (want <= 0 && !(strGap > 0)) return;
            if (!_hireTried)
            {
                _hireTried = true;
                try { _hire = AccessTools.Method(typeof(RecruitmentCampaignBehavior), "ApplyRecruitMercenary", new[] { typeof(MobileParty), typeof(Settlement), typeof(CharacterObject), typeof(int) }); }
                catch (Exception e) { Stumble("Hire(odbicie)", e); }
                if (_hire == null) Log.Info("Straz karawan (285): BRAK RecruitmentCampaignBehavior.ApplyRecruitMercenary - karawany nie werbuja przy kluczu (zwolnienie dziala).");
            }
            if (_hire == null) return;
            var rcb = Campaign.Current != null ? Campaign.Current.GetCampaignBehavior<RecruitmentCampaignBehavior>() : null;
            var md = rcb != null ? rcb.GetMercenaryData(st.Town) : null;
            if (md == null || (!md.HasAvailableMercenary(Occupation.CaravanGuard) && !md.HasAvailableMercenary(Occupation.Mercenary))) return;
            var troop = md.TroopType;
            if (troop == null) return;
            // 286: brak sily wobec bandy drogi - ludzi tylu, ilu wnosi te sile jednostka karczmy (moc jednostki z modelu sily gry, mnoznik partii k)
            if (strGap > 0 && k > 0)
            {
                double dp = Campaign.Current.Models.MilitaryPowerModel.GetDefaultTroopPower(troop);
                if (dp > 0) want = Math.Max(want, (int)Math.Ceiling(strGap / (k * dp)));
                want = Math.Min(want, Math.Max(0, mp.Party.PartySizeLimit - mp.MemberRoster.TotalManCount));
            }
            if (want <= 0) return;
            int price = Campaign.Current.Models.PartyWageModel.GetTroopRecruitmentCost(troop, mp.LeaderHero).RoundedResultNumber;
            int n = Math.Min(want, md.Number);
            int got = 0; long paid = 0;
            for (int i = 0; i < n; i++)
            {
                if (mp.PartyTradeGold <= price + GameKeepGold) break;
                int g0 = mp.PartyTradeGold, m0 = mp.MemberRoster.TotalManCount;
                _hire.Invoke(rcb, new object[] { mp, st, troop, 1 });
                if (mp.MemberRoster.TotalManCount <= m0) break;
                got++; paid += Math.Max(0, g0 - mp.PartyTradeGold);
            }
            if (got > 0) { _hireVisits++; _hired += got; _hireCost += paid; }
        }

        /// <summary>ArmouryBehavior.OnDailyTick (po CaravanTrade284.Daily): linia "Straz karawan (285): dzien N" i zerowanie licznikow doby.</summary>
        internal static void Daily()
        {
            var s = Settings.Current;
            if (s == null) return;
            long t0 = Stopwatch.GetTimestamp();
            int day = (int)CampaignTime.Now.ToDays;
            long men = 0, wage = 0; int cars = 0;
            try
            {
                foreach (var mp in MobileParty.AllCaravanParties)
                {
                    if (mp == null || !mp.IsActive || mp.MemberRoster == null) continue;
                    cars++; men += mp.MemberRoster.TotalManCount; wage += Math.Max(0, mp.TotalWage);
                }
            }
            catch (Exception e) { Stumble("Daily(swiat)", e); }
            _ticks += Stopwatch.GetTimestamp() - t0;
            double ms = _ticks * 1000.0 / Stopwatch.Frequency;
            int n = Math.Max(1, _eval);
            var sb = new StringBuilder(900);
            sb.Append("Straz karawan (285): dzien ").Append(day).Append(" | klucz Caravan Guards By Road And Cargo ").Append(s.CaravanGuardByRisk ? "TAK" : "NIE (tylko pomiar)")
              .Append(" | ocenione karawany w miastach ").Append(_eval).Append(" (gracza ").Append(_evalPlayer).Append("), bez reguly (brak szablonu albo sily) ").Append(_noRule)
              .Append(" | potrzeba strazy srednio ").Append((_sNeed / n).ToString("0.0", Inv)).Append(" (podstawa szablonu ").Append((_sBase / n).ToString("0.0", Inv))
              .Append(", zagrozenie bandy w dniu drogi ").Append((_sThreat / n).ToString("0.0", Inv)).Append(" ludzi, udzial majatku ").Append((_sV / n).ToString("0.00", Inv))
              .Append(", limit gry ").Append((_sLim / n).ToString("0.0", Inv)).Append(", podstawa >= limit ").Append(_atLimitBase).Append("), ludzi przy ocenie srednio ")
              .Append((_sMen / n).ToString("0.0", Inv)).Append(", ponizej potrzeby ").Append(_short)
              .Append(" | najem do potrzeby: wizyt ").Append(_hireVisits).Append(", ludzi ").Append(_hired).Append(" za ").Append(_hireCost).Append(" zl; najem ponad potrzebe wstrzymany ")
              .Append(_blocked).Append(" | zwolnieni do domu (droga 166): ").Append(_relMen).Append(" ludzi z ").Append(_relCar).Append(" karawan (zold dzienny ok. ").Append(_relWage)
              .Append(" zl), bez wsi z danymi BK ").Append(_relNone).Append(", karawany gracza bez zwolnienia ").Append(_relPlayerSkip)
              .Append(" | ocena przy wyjezdzie (cel znany, recenzja 285; BK ").Append(DepartWired ? "wpiety" : "BRAK").Append("): ").Append(_dep)
              .Append(", w tym banda celu silniejsza niz miasta ").Append(_depTgt).Append(", nadwyzka bez celu - zwolnienie odlozone do wyjazdu ").Append(_relWait)
              .Append(" | karawany swiata ").Append(cars).Append(": ludzi ").Append(men).Append(" (").Append(cars > 0 ? ((double)men / cars).ToString("0.0", Inv) : "-")
              .Append(" na karawane), zold z modelu ").Append(wage).Append(" zl/d")
              .Append(" | czas ").Append(ms.ToString("0.00", Inv)).Append(" ms | potkniecia ").Append(_stumbles).Append(" (sesja ").Append(_stumbleSess).Append(").");
            Log.Info(sb.ToString());
            try { Daily286(s, day); } catch (Exception e) { Stumble("Daily286", e); }   // 286: linia "Straz karawan wedlug sily drogi (286)" (przed zerowaniem licznikow potkniec)
            ZeroDay();
        }
    }
}
