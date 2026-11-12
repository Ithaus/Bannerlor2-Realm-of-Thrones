using System;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// 472 MURY HISTORYCZNIE DROGIE I DLUGIE (Jeff 12.11: "okres budowy murow zamek 1 2 lub 3 powinny byc odpowiednio drogie i dlugie"; klucz WallWorksHistorical,
    /// wylaczony = co do bitu jak dotad). Badanie 472: koszt poziomu murow w grze 1.4.8 to miasto {0, 6000, 12000}, zamek {0, 1400, 2800} punktow (poziom 1 startowy,
    /// przejscia 1->2 i 2->3; Castle Charters x0.8), a punkty mur kupowal po 48 d jak kazda budowle wojskowa - zamek 2->3 ok. 2 lata i 174 tys. d, miasto 2->3 ok. 4 lata.
    /// Historycznie (Edward I w Walii: Harlech 8 190 funtow w 7 sezonow, Conwy ok. 15 tys., Beaumaris 11+ tys. nieukonczony; mury miast - dziesieciolecia) mury
    /// byly najdrozsza i najdluzsza budowa pana. Regula (wariant B - koszt poziomu w grze i postep w zapisie bez zmian; budowy w toku zachowuja swoj ulamek):
    ///  - cena punktu murow: BuildPencePerPointMilitary x WallCastlePointPriceFactor (zamek, 3.0 = 144 d) albo x WallTownPointPriceFactor (miasto, 2.0 = 96 d) -
    ///    u pana (BuildFunding.Daily, takze R3-11a OneBudget/OneNeed - ta sama ppp) i u ludu (P2 FolkWorks); przyspieszenie zlotem (gra zabiera zloto gracza co dobe)
    ///    na murach dzielone przez ten sam czynnik (Boost), lenno gracza bez PaidConstructionPlayer - moc gry dzielona przez czynnik (jedna zasada na jedno zjawisko);
    ///  - najkrotszy czas poziomu: dzienny limit punktow murow = koszt poziomu / (WallMinYears x doby roku Calendar.DaysPerYear); obejmuje razem pana, lud, Masona,
    ///    zarzadce i przyspieszenie zlotem. Zloto ograniczone PRZED zakupem (pan: budzet najwyzej na punkty do limitu przez EngPaidNeed; lud: reszta limitu po
    ///    punktach pana z Masonem), wiec nikt nie placi za punkty, ktore przepadna; twarda granica w PowerPostfix (LimitMax) pilnuje reszty (kontrola w linii).
    ///    Czego pan nie wydal - zostaje w kiesie, a przy R3-11a reszta przydzialu idzie na nastepna budowe rodu (OnePaid odejmuje tylko wydane).
    /// Zamknieta ekonomia bez zmian: kiesa pana / kiesy ludu -> kasa targu (material) i kasa osady / kiesa ludu (place). Bez zimowej przerwy (WesterosClimate: zimy
    /// 3-5 lat). Stan tylko doby (bez pol zapisu). Linie: start sesji "Mury (472): start sesji" (tabela poziomow), dobowo "Mury (472): dzien N".
    /// </summary>
    internal static partial class BuildFunding
    {
        internal static bool WallOn { get { var s = Settings.Current; return s != null && s.WallWorksHistorical && s.PaidConstruction; } }

        private static TextObject _wallCapTxt, _wallGameTxt;
        // liczniki doby (blok dobowy, watek glowny)
        private static int _wLordN, _wLordCap, _wLordOver, _wFolkN, _wFolkCap, _wStumbles, _wPowerErr;
        private static long _wLordGold, _wLordMat, _wLordWage, _wFolkGold, _wReleased;
        private static double _wLordPts, _wFolkPts;
        private static bool _wErrLogged;

        /// <summary>Mury (fortyfikacje miasta albo zamku) - te same id co P2 (WallIds).</summary>
        internal static bool IsWall(BuildingType bt) { return bt != null && bt.StringId != null && Array.IndexOf(WallIds, bt.StringId) >= 0; }

        /// <summary>Czynnik ceny punktu murow tej osady (1 - klucz wylaczony albo nie mury).</summary>
        internal static float WallFactor(Town t, BuildingType bt)
        {
            if (!WallOn || t == null || !IsWall(bt)) return 1f;
            var s = Settings.Current;
            float f = t.IsCastle ? s.WallCastlePointPriceFactor : s.WallTownPointPriceFactor;
            return float.IsNaN(f) ? 1f : MBMath.ClampFloat(f, 1f, 10f);
        }

        /// <summary>Najkrotszy czas poziomu `next` (2 albo 3) w dobach; 0 - bez limitu.</summary>
        private static double WallMinDays(bool castle, int next)
        {
            var s = Settings.Current;
            if (s == null) return 0;
            float y = castle ? (next <= 2 ? s.WallMinYearsCastle2 : s.WallMinYearsCastle3) : (next <= 2 ? s.WallMinYearsTown2 : s.WallMinYearsTown3);
            if (float.IsNaN(y) || y <= 0f) return 0;
            return Math.Min(20f, y) * Math.Max(1, LongYearTimeModel.DaysPerYear(s));
        }

        /// <summary>Dzienny limit punktow murow budowli b (koszt biezacego poziomu / najkrotszy czas); float.MaxValue - bez limitu (klucz wylaczony, nie mury, poziom 3).</summary>
        internal static float WallCap(Town t, Building b)
        {
            if (!WallOn || t == null || b == null || !IsWall(b.BuildingType) || b.CurrentLevel >= 3) return float.MaxValue;
            int cost = b.GetConstructionCost();
            double days = WallMinDays(t.IsCastle, b.CurrentLevel + 1);
            if (cost <= 0 || days <= 0) return float.MaxValue;
            return (float)(cost / days);
        }

        /// <summary>Mury na czole kolejki osady (poziom ponizej 3) - null, gdy nie albo klucz wylaczony.</summary>
        private static Building WallHead(Town t)
        {
            if (!WallOn || t == null) return null;
            var q = t.BuildingsInProgress;
            var b = q != null && q.Count > 0 ? q.Peek() : null;
            return b != null && b.CurrentLevel < 3 && IsWall(b.BuildingType) ? b : null;
        }

        /// <summary>Boost: przyspieszenie zlotem na murach na czole - podzielone przez czynnik ceny (te same pensy kupuja mniej muru). Klucz wylaczony - amount bez zmian.</summary>
        private static float WallBoost(Town t, float amount)
        {
            if (!(amount > 0f)) return amount;
            var b = WallHead(t);
            return b != null ? amount / WallFactor(t, b.BuildingType) : amount;
        }

        /// <summary>PowerPostfix: twarda granica dziennej mocy osady przy murach na czole (LimitMax). Klucz wylaczony / nie mury - nic.</summary>
        private static void WallLimit(Town t, ref ExplainedNumber r)
        {
            try
            {
                var b = WallHead(t);
                if (b == null) return;
                float cap = WallCap(t, b);
                if (cap >= float.MaxValue || cap >= r.LimitMaxValue) return;
                if (_wallCapTxt == null) _wallCapTxt = new TextObject("{=!}Walls rise slowly (shortest time for this stage)");
                r.LimitMax(cap, _wallCapTxt);
            }
            catch { _wPowerErr++; }
        }

        /// <summary>PowerIntPostfix: ta sama granica dla mocy calkowitej.</summary>
        private static int WallLimitInt(Town t, float v)
        {
            try
            {
                var b = WallHead(t);
                if (b != null) { float cap = WallCap(t, b); if (v > cap) v = cap; }
            }
            catch { _wPowerErr++; }
            return (int)v;
        }

        /// <summary>Lenno gracza bez PaidConstructionPlayer (moc gry): mury na czole - moc gry podzielona przez czynnik ceny (pozycja dymku).</summary>
        private static void WallScaleGame(Town t, ref ExplainedNumber r)
        {
            try
            {
                var b = WallHead(t);
                if (b == null) return;
                float f = WallFactor(t, b.BuildingType);
                float v = r.ResultNumber, k = 1f + r.SumOfFactors;
                if (!(f > 1f) || !(v > 0f) || !(k > 0.01f)) return;
                if (_wallGameTxt == null) _wallGameTxt = new TextObject("{=!}Stone and mortar for the walls cost more");
                r.Add(-(v - v / f) / k, _wallGameTxt);
            }
            catch { _wPowerErr++; }
        }

        private static int WallScaleGameInt(Town t, int v)
        {
            try { var b = WallHead(t); if (b != null && v > 0) return (int)(v / WallFactor(t, b.BuildingType)); }
            catch { _wPowerErr++; }
            return v;
        }

        private static void WallStumble(string where, Exception e)
        {
            _wStumbles++;
            try { if (!_wErrLogged) { _wErrLogged = true; Log.Error("BuildFunding.Walls." + where, e); } } catch { }   // licznik potkniec, blad raz na sesje (CLAUDE.md 7)
        }

        /// <summary>Daily (pan), po OneBudget: budzet murow najwyzej na punkty do dziennego limitu (reszta poziomu, limit minus przyspieszenie, zarzadca i Mason -
        /// EngPaidNeed), w pensach przy pelnym budzecie (material BuildMaterialShare, place wedle miasta targowego - jak OneNeed, bez zapasu). Reszta - w kiesie
        /// albo (R3-11a) na nastepna budowe rodu. Nie mury / klucz wylaczony - budget bez zmian.</summary>
        internal static float WallBudget(Settlement st, Building b, float budget, float ppp, Settlement market)
        {
            if (!WallOn || st == null || st.Town == null || b == null || !IsWall(b.BuildingType)) return budget;
            try
            {
                _wLordN++;
                float cap = WallCap(st.Town, b);
                if (cap >= float.MaxValue) return budget;
                int cost = b.GetConstructionCost();
                double tgt = Math.Min(Math.Max(0.0, cost - b.BuildingProgress), cap);
                double paid = EngPaidNeed(st.Town, tgt);
                double ms = MBMath.ClampFloat(Settings.Current.BuildMaterialShare, 0f, 1f);
                double per = (ms + (1.0 - ms) / WageIdx(market)) / Math.Max(1f, ppp);
                if (!(per > 0)) return budget;
                double max = paid / per;
                if (budget <= max) return budget;
                _wLordCap++;
                _wReleased += (long)(budget - max);
                return (float)Math.Max(0.0, max);
            }
            catch (Exception e) { WallStumble("WallBudget", e); return budget; }
        }

        /// <summary>Daily (pan), po zaplacie za mury: liczniki doby i kontrola (punkty pana ponad limit dnia - oczekiwane 0, poza zaokragleniem placy i jedna sztuka materialu).</summary>
        internal static void WallPaid(Settlement st, Building b, int mat, int wage, float pts)
        {
            if (!WallOn || st == null || b == null || !IsWall(b.BuildingType)) return;
            try
            {
                _wLordGold += mat + wage; _wLordMat += mat; _wLordWage += wage; _wLordPts += pts;
                float cap = WallCap(st.Town, b);
                if (cap < float.MaxValue && pts > cap + 0.5f) _wLordOver++;
            }
            catch (Exception e) { WallStumble("WallPaid", e); }
        }

        /// <summary>FolkWorks (lud): najwiecej punktow ludu na mury dzis. where 0 (czolo) - reszta limitu po mocy osady tej doby cp (pan, przyspieszenie, zarzadca,
        /// Mason); kazdy punkt ludu ponizej punktow Masona dociaga punkt Masona (Extra = min(Mason, praca)), wiec wtedy liczy sie podwojnie. Poza czolem - caly limit.
        /// Nie mury / klucz wylaczony - double.MaxValue.</summary>
        internal static double WallFolkCap(Town t, Building b, int where, double cp)
        {
            if (!WallOn || t == null || b == null || !IsWall(b.BuildingType)) return double.MaxValue;
            try
            {
                float cap = WallCap(t, b);
                if (cap >= float.MaxValue) return double.MaxValue;
                if (where != 0) return cap;
                double room = Math.Max(0.0, cap - Math.Max(0.0, cp));
                if (Covered(t))
                {
                    float f0; _funded.TryGetValue(t, out f0);
                    double mr = Math.Max(0.0, Buildings338Mason.Points(t) - (f0 + Boost(t)));
                    room = room <= 2 * mr ? room / 2 : room - mr;
                }
                return room;
            }
            catch (Exception e) { WallStumble("WallFolkCap", e); return 0.0; }
        }

        /// <summary>FolkWorks: liczniki ludu na murach (atCap - lud dzis na limicie albo bez miejsca pod limitem).</summary>
        internal static void WallFolkNote(BuildingType bt, long gold, double pts, bool atCap)
        {
            if (!WallOn || !IsWall(bt)) return;
            if (atCap) _wFolkCap++;
            if (gold > 0 || pts > 0) { _wFolkN++; _wFolkGold += gold; _wFolkPts += pts; }
        }

        private static void WallDayStart()
        {
            _wLordN = _wLordCap = _wLordOver = _wFolkN = _wFolkCap = _wStumbles = 0; _wPowerErr = 0;
            _wLordGold = _wLordMat = _wLordWage = _wFolkGold = _wReleased = 0; _wLordPts = _wFolkPts = 0;
        }

        /// <summary>Linia doby (koniec BuildFunding.Daily, po robotach ludu i R3-10b): mury w toku, postep, zloto, osady na limicie, kontrola. log_grep: "Mury (472): dzien".</summary>
        private static void WallLine()
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !s.LogEnabled) return;
                int day = (int)CampaignTime.Now.ToDays;
                if (!WallOn) { Log.Info("Mury (472): dzien " + day + " | Wall Works Historical " + (s.WallWorksHistorical ? "tak" : "nie") + ", Paid Construction " + (s.PaidConstruction ? "tak" : "nie") + " - mury jak dotad."); WallDayStart(); return; }
                var inv = CultureInfo.InvariantCulture;
                int c2 = 0, c3 = 0, t2 = 0, t3 = 0, atCap = 0, clampN = 0, siege = 0; double powSum = 0, capSum = 0, clampPts = 0, fracSum = 0;
                foreach (var st in Settlement.All)
                {
                    if (st == null || st.Town == null || (!st.IsTown && !st.IsCastle) || st.OwnerClan == null) continue;
                    try
                    {
                        var t = st.Town;
                        var b = WallHead(t);
                        if (b == null) continue;
                        int next = b.CurrentLevel + 1;
                        if (st.IsCastle) { if (next <= 2) c2++; else c3++; } else { if (next <= 2) t2++; else t3++; }
                        if (st.IsUnderSiege) { siege++; continue; }
                        float cap = WallCap(t, b);
                        int cost = b.GetConstructionCost();
                        if (cost > 0) fracSum += Math.Min(1.0, b.BuildingProgress / cost);
                        float pow = t.Construction;   // moc doby z granica LimitMax
                        powSum += pow; if (cap < float.MaxValue) capSum += cap;
                        if (cap < float.MaxValue && pow >= cap - 0.01f) atCap++;
                        if (Covered(t))
                        {
                            float f0; _funded.TryGetValue(t, out f0);
                            float bo = Boost(t), fl = FolkHead(t);
                            float pre = f0 + fl + bo + EngExtra(t, f0, bo, false) + Buildings338Mason.Extra(t, f0 + fl + bo);
                            if (cap < float.MaxValue && pre > cap + 0.01f) { clampN++; clampPts += pre - cap; }
                        }
                    }
                    catch (Exception e) { WallStumble("WallLine(osada)", e); }
                }
                int all = c2 + c3 + t2 + t3, live = all - siege;
                var sb = new StringBuilder(900);
                sb.Append("Mury (472): dzien ").Append(day).Append(" | Wall Works Historical tak (cena punktu x").Append(s.WallCastlePointPriceFactor.ToString("0.0#", inv)).Append(" zamek, x")
                  .Append(s.WallTownPointPriceFactor.ToString("0.0#", inv)).Append(" miasto; najkrotszy czas lat: zamek 2/3 = ").Append(s.WallMinYearsCastle2.ToString("0.#", inv)).Append('/')
                  .Append(s.WallMinYearsCastle3.ToString("0.#", inv)).Append(", miasto 2/3 = ").Append(s.WallMinYearsTown2.ToString("0.#", inv)).Append('/').Append(s.WallMinYearsTown3.ToString("0.#", inv)).Append(')')
                  .Append(" | mury w toku na czole kolejki ").Append(all).Append(" (zamki ->2 ").Append(c2).Append(", ->3 ").Append(c3).Append("; miasta ->2 ").Append(t2).Append(", ->3 ").Append(t3)
                  .Append("; oblezone ").Append(siege).Append("), sredni postep poziomu ").Append(live > 0 ? (100.0 * fracSum / live).ToString("0.0", inv) : "-").Append('%')
                  .Append(" | moc doby razem ").Append(powSum.ToString("0.0", inv)).Append(" pkt (limit razem ").Append(capSum.ToString("0.0", inv)).Append("), osad na limicie ").Append(atCap)
                  .Append(" | pan: murow w rozliczeniu ").Append(_wLordN).Append(", wydano ").Append(_wLordGold).Append(" (material ").Append(_wLordMat).Append(", place ").Append(_wLordWage)
                  .Append("), punktow ").Append(_wLordPts.ToString("0.0", inv)).Append(", budzet obciety do limitu ").Append(_wLordCap).Append(" osad (oddane dalej albo w kiesie ").Append(_wReleased).Append(" d)")
                  .Append(" | lud: murow ").Append(_wFolkN).Append(", zaplacone ").Append(_wFolkGold).Append(", punktow ").Append(_wFolkPts.ToString("0.0", inv)).Append(", na limicie ").Append(_wFolkCap)
                  .Append(" | kontrola: punkty ponad limit przed granica ").Append(clampN).Append(" osad (").Append(clampPts.ToString("0.0", inv)).Append(" pkt obciete), pan ponad limit ").Append(_wLordOver)
                  .Append(" | potkniecia ").Append(_wStumbles).Append(_wPowerErr > 0 ? ", bledy mocy " + _wPowerErr : "").Append('.');
                Log.Info(sb.ToString());
            }
            catch (Exception e) { WallStumble("WallLine", e); }
            WallDayStart();
        }

        /// <summary>Start sesji (OnSessionLaunched): tabela poziomow murow - koszt w punktach, cena punktu, zloto na poziom przy dniowce 1.0, najkrotszy czas i limit dnia.
        /// log_grep: "Mury (472): start sesji".</summary>
        internal static void WallSessionLine()
        {
            try
            {
                var s = Settings.Current;
                if (s == null) return;
                _wErrLogged = false;
                var inv = CultureInfo.InvariantCulture;
                var sb = new StringBuilder(900);
                sb.Append("Mury (472): start sesji | Wall Works Historical ").Append(s.WallWorksHistorical ? "tak" : "nie").Append(", Paid Construction ").Append(s.PaidConstruction ? "tak" : "nie")
                  .Append(", rok ").Append(LongYearTimeModel.DaysPerYear(s)).Append(" dob, cena punktu wojskowego ").Append(s.BuildPencePerPointMilitary.ToString("0.#", inv)).Append(" d");
                bool on = s.WallWorksHistorical && s.PaidConstruction;
                var types = new[] { DefaultBuildingTypes.CastleFortifications, DefaultBuildingTypes.SettlementFortifications };
                for (int k = 0; k < 2; k++)
                {
                    var bt = types[k];
                    bool castle = k == 0;
                    if (bt == null) continue;
                    double fct = on ? MBMath.ClampFloat(castle ? s.WallCastlePointPriceFactor : s.WallTownPointPriceFactor, 1f, 10f) : 1.0;
                    double ppp = Math.Max(1f, s.BuildPencePerPointMilitary) * fct;
                    for (int lvl = 2; lvl <= 3; lvl++)
                    {
                        int cost = bt.GetProductionCost(lvl - 1);
                        double days = on ? WallMinDays(castle, lvl) : 0;
                        sb.Append(" | ").Append(castle ? "zamek" : "miasto").Append(' ').Append(lvl - 1).Append("->").Append(lvl).Append(": ").Append(cost).Append(" pkt")
                          .Append(castle ? " (Castle Charters " + (int)(cost * 0.8f) + ")" : "")
                          .Append(", ").Append(ppp.ToString("0", inv)).Append(" d/pkt, zloto ok. ").Append((cost * ppp).ToString("0", inv)).Append(" d (").Append((cost * ppp / 240.0).ToString("0", inv)).Append(" L)")
                          .Append(", najkrocej ").Append(days > 0 ? days.ToString("0", inv) + " dob (" + (days / Math.Max(1, LongYearTimeModel.DaysPerYear(s))).ToString("0.#", inv) + " r.), limit " + (cost / days).ToString("0.00", inv) + " pkt/d" : "bez limitu");
                    }
                }
                int c2 = 0, c3 = 0, t2 = 0, t3 = 0;
                foreach (var st in Settlement.All)
                {
                    if (st == null || st.Town == null || (!st.IsTown && !st.IsCastle)) continue;
                    try
                    {
                        var q = st.Town.BuildingsInProgress;
                        var b = q != null && q.Count > 0 ? q.Peek() : null;
                        if (b == null || b.CurrentLevel >= 3 || !IsWall(b.BuildingType)) continue;
                        int next = b.CurrentLevel + 1;
                        if (st.IsCastle) { if (next <= 2) c2++; else c3++; } else { if (next <= 2) t2++; else t3++; }
                    }
                    catch { }
                }
                sb.Append(" | mury na czole kolejki teraz: zamki ->2 ").Append(c2).Append(", ->3 ").Append(c3).Append("; miasta ->2 ").Append(t2).Append(", ->3 ").Append(t3)
                  .Append(" (postep w zapisie bez zmian - budowy w toku zachowuja ulamek poziomu).");
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Log.Error("BuildFunding.WallSessionLine", e); }
        }
    }
}
