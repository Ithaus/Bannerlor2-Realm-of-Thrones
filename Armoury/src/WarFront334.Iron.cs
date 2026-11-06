using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using HarmonyLib;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>
    /// 334 B9 - ZELAZNI LUDZIE: KANONICZNA WOJNA Z ZACHODU (klucz IronbornCanonReave334; Z0 - pomiar bez klucza).
    /// DOWOD (log 04-06.11, 243 doby): Wyspy armia 0, oblezenie 0, rabunek z lupem 0, opcji rabunku prawie zawsze 0, jedzenie mediana 1-20 dni (prog gry 8).
    /// PRZYCZYNY (P7): (a) ForcePeace SCA z calego swiata - B2/B3; (b) jedzenie ponizej NeededFoodsInDaysThresholdForRaid - mysl gry nie daje opcji rabunku;
    /// (c) zasieg morski - AiHelper mnozy odcinek morski przez CalculateShipDistanceAmplifier: 0.35-1.25 przy zdrowych statkach, x1.5-3.5 przy zalodze ponad
    /// pojemnosc statkow z kadlubem > 20%, a gdy GetEstimatedSafeSailDuration (srednio po statkach: kadlub / zuzycie na otwartym morzu) jest krotszy od rejsu -
    /// mnoznik = pol przekatnej mapy (cel poza zasiegiem); (d) weto 274 na Winterfell (Z4 - Wyspy maja strone 0 w 274, weto siedzib ich nie dotyczy; linia startu).
    ///  Z0 (pomiar, linia "Zelazni" raz na dobe; "Zelazni partia" w pierwszej dobie sesji i co 7 dob, najwyzej 60): na partie lordow Wysp - polozenie (wyspa / lad /
    ///     morze), statki, pojemnosc zalog zdrowych statkow wobec ludzi, kadlub min / srednio, safe-sail wobec rejsu do najblizszej wsi Polnocy, droga skorygowana gry
    ///     wobec progu 5 x srednia odleglosc dwoch najblizszych miast (All), dni jedzenia wobec progow gry, opcje RaidSettlement / BesiegeSettlement z mysli (doba),
    ///     powod postoju. Sita w kolejnosci gry: jedzenie -> armia -> stan wsi -> zasieg.
    ///  Z1 + Z2a (mysl AI, priorytet 48, klucz): partia Wysp poza armia (albo wodz), nie w bitwie ani oblezeniu, nie w porcie, gdy (1) jedzenie ponizej progu najazdu gry
    ///     a pan ma zloto ponad 5 000 (kiesa rodziny 166), (2) statek z kadlubem <= 20% (prog gry) a pan ma zloto na naprawe (ShipCostModel - cena gry), albo (3) safe-sail
    ///     nie starcza na rejs do najblizszej wsi Polnocy (Z0) - idzie do najblizszego przyjaznego portu (twierdza z portem; dla jedzenia miasto z jedzeniem na rynku):
    ///     GoToSettlement z ocena 1.05 x max(najlepsza inna, 0.5) (OB5 Edge / FloorBase - jak W1a 274). Jedzenie kupuje gra przy wejsciu do miasta, statki naprawia
    ///     NavalDLC przy wejsciu do twierdzy z portem (RepairLordPartyShips: PartyTradeGold > koszt naprawy) - ceny gry, zloto pana; nic z niczego. Ten sam port nie
    ///     wczesniej niz po dobie od ostatniej wizyty (bez petli, gdy gra nie naprawi z braku zlota).
    ///  Z2b (przydzial statkow z floty 326a) - NIE wdrozony w tej paczce: statki rodu sa na jego partiach (zadnej puli w porcie), przeniesienie miedzy partiami w roznych
    ///     miejscach bylo by teleportem. Z0 mierzy "zaloga > pojemnosc"; zakup statkow do idealu robi gra i 326a jak dotad.
    ///  Z3 (przyczolek): bez nowej stalej - pierwsza twierdza na ladzie Polnocy daje baze, progi gry licza sie od niej.
    /// </summary>
    internal static partial class WarFront334
    {
        private sealed class IronThinkRec { public int Day, Raid, Siege; }
        private static readonly Dictionary<MobileParty, IronThinkRec> _ironThink = new Dictionary<MobileParty, IronThinkRec>();
        private sealed class PortRec { public int Hour; public Settlement Port; public string Why; public MobileParty.NavigationType Nav; public bool FromPort, ToPort; }
        private static readonly Dictionary<MobileParty, PortRec> _portCache = new Dictionary<MobileParty, PortRec>();
        private static readonly Dictionary<MobileParty, int> _portVisit = new Dictionary<MobileParty, int>();            // godzina ostatniej wizyty w porcie
        private sealed class Hull { public float Hp; public int Gold; public int Day; }
        private static readonly Dictionary<MobileParty, Hull> _hullAtPort = new Dictionary<MobileParty, Hull>();
        private static readonly HashSet<MobileParty> _safeBad = new HashSet<MobileParty>();                               // Z0: safe-sail < rejs (doba)
        private static int _dToPort, _dToPortFood, _dToPortHull, _dToPortSafe, _dRepairs, _dIronSiegesN, _dLootN;
        private static long _dRepairGold;
        private static readonly List<string> _dIronSieges = new List<string>(), _dLoot = new List<string>();
        private static int _ironDetailDay = -1;
        private static MethodInfo _miAmp;
        private static string _wAmp = "BRAK";

        private static void ResetIron()
        {
            _ironThink.Clear(); _portCache.Clear(); _portVisit.Clear(); _hullAtPort.Clear(); _safeBad.Clear(); _ironDetailDay = -1;
            ZeroIronDay();
        }

        private static void ZeroIronDay()
        {
            _dToPort = _dToPortFood = _dToPortHull = _dToPortSafe = _dRepairs = _dIronSiegesN = _dLootN = 0; _dRepairGold = 0;
            _dIronSieges.Clear(); _dLoot.Clear();
        }

        private static string ReflectAmp()
        {
            try
            {
                _miAmp = AccessTools.Method(typeof(AiHelper), "CalculateShipDistanceAmplifier", new[] { typeof(MobileParty), typeof(float) });
                return _miAmp != null ? "ok" : "BRAK (AiHelper.CalculateShipDistanceAmplifier)";
            }
            catch (Exception e) { Stumble("ReflectAmp", e); return "BLAD (" + e.GetType().Name + ")"; }
        }

        private static float Amp(MobileParty mp, float naval)
        {
            if (_miAmp == null || !(naval > 0f)) return 1f;
            try { return Convert.ToSingle(_miAmp.Invoke(null, new object[] { mp, naval })); } catch { return -1f; }
        }

        private static bool AtWarWithNorth(IFaction f)
        {
            var n = FindKingdom(NorthId);
            try { return n != null && f != null && FactionManager.IsAtWarAgainstFaction(f, n); } catch { return false; }
        }

        // ================================================================ mysl AI: opcje (Z0) i port (Z1/Z2)
        private static void IronOptions(MobileParty party, PartyThinkParams p)
        {
            var scores = p.AIBehaviorScores;
            int raid = 0, siege = 0;
            for (int i = 0; i < scores.Count; i++)
            {
                var a = scores[i].Item1.AiBehavior;
                if (scores[i].Item2 <= 0f) continue;
                if (a == AiBehavior.RaidSettlement) raid++; else if (a == AiBehavior.BesiegeSettlement) siege++;
            }
            int day = Today;
            IronThinkRec r;
            if (!_ironThink.TryGetValue(party, out r)) { r = new IronThinkRec(); _ironThink[party] = r; }
            if (r.Day != day) { r.Day = day; r.Raid = 0; r.Siege = 0; }
            if (raid > r.Raid) r.Raid = raid;
            if (siege > r.Siege) r.Siege = siege;
        }

        private static float WorstHull(MobileParty mp, out Ship worst)
        {
            worst = null; float w = 2f;
            var ships = mp.Ships;
            if (ships == null) return w;
            foreach (var sh in ships)
            {
                if (sh == null || !(sh.MaxHitPoints > 0f)) continue;
                float r = sh.HitPoints / sh.MaxHitPoints;
                if (r < w) { w = r; worst = sh; }
            }
            return w;
        }

        private static void IronPort(MobileParty party, PartyThinkParams p)
        {
            if (!IronOn || p.DoNotChangeBehavior) return;
            if (party.Army != null && party.Army.LeaderParty != party) return;
            if (party.MapEvent != null || party.BesiegedSettlement != null || party.SiegeEvent != null) return;
            var cs = party.CurrentSettlement;
            if (cs != null && cs.HasPort) return;                      // juz w porcie - gra kupuje i naprawia przy wejsciu
            if (cs != null && cs.SiegeEvent != null) return;
            if (!AtWarWithNorth(party.MapFaction)) return;
            var lead = party.LeaderHero;
            if (lead == null) return;
            int hour = (int)CampaignTime.Now.ToHours;
            PortRec pr;
            if (!_portCache.TryGetValue(party, out pr) || hour - pr.Hour >= 6)
            {
                pr = new PortRec { Hour = hour };
                string why = null;
                float need = 8f;
                try { need = Campaign.Current.Models.MobilePartyAIModel.NeededFoodsInDaysThresholdForRaid; } catch { }
                Ship worst;
                float wh = WorstHull(party, out worst);
                if (ReaverLaw.FoodDays(party, 0) < need && lead.Gold > CanonWarBalance.FamilyFloor) why = "jedzenie";
                else if (worst != null && wh <= 0.2f)
                {
                    float cost = float.MaxValue;
                    try { cost = Campaign.Current.Models.ShipCostModel.GetShipRepairCost(worst, party.Party); } catch { }
                    if (party.PartyTradeGold > cost) why = "kadlub";
                }
                else if (_safeBad.Contains(party)) why = "safe-sail";
                if (why != null)
                {
                    pr.Why = why;
                    pr.Port = NearestPort(party, why == "jedzenie");
                    if (pr.Port != null)
                    {
                        int last;
                        if (_portVisit.TryGetValue(party, out last) && hour - last < 24) pr.Port = null;   // ten sam port nie wczesniej niz po dobie
                    }
                    if (pr.Port != null)
                    {
                        MobileParty.NavigationType nav; float dist; bool fromPort;
                        AiHelper.GetBestNavigationTypeAndAdjustedDistanceOfSettlementForMobileParty(party, pr.Port, false, out nav, out dist, out fromPort);
                        if (nav == MobileParty.NavigationType.None) pr.Port = null;
                        else { pr.Nav = nav; pr.FromPort = fromPort; pr.ToPort = false; }
                    }
                }
                _portCache[party] = pr;
            }
            if (pr.Port == null) return;
            var scores = p.AIBehaviorScores;
            float best = 0f;
            for (int i = 0; i < scores.Count; i++)
            {
                var e = scores[i];
                if (e.Item1.AiBehavior == AiBehavior.DefendSettlement && e.Item1.Party is Settlement ds && ds.MapFaction == party.MapFaction && ds.IsUnderSiege) return;   // odsiecz wlasnej twierdzy wygrywa
                if (e.Item2 > best) best = e.Item2;
            }
            float v = CanonWarBalance.Edge * Math.Max(best, CanonWarBalance.FloorBase);
            var d = new AIBehaviorData(pr.Port, AiBehavior.GoToSettlement, pr.Nav, false, pr.FromPort, pr.ToPort);
            p.AddBehaviorScore((d, v));
            _dToPort++;
            if (pr.Why == "jedzenie") _dToPortFood++; else if (pr.Why == "kadlub") _dToPortHull++; else _dToPortSafe++;
            if (Say("port|" + party.StringId))
                Log.Info(Tag + "Zelazni do portu | dzien " + Today + " | " + Nm(party) + " -> " + Nm(pr.Port) + " (" + pr.Why + "; jedzenie " + ReaverLaw.FoodDays(party, 0).ToString("0.0", Inv)
                         + " dni, zloto pana " + lead.Gold + ", ocena " + F2(v) + ") - jedzenie z rynku i naprawa statkow robi gra przy wejsciu, za zloto pana.");
        }

        /// <summary>Najblizszy przyjazny port (twierdza z portem, swoja frakcja albo nie w wojnie; nieoblezona). food - tylko miasto z jedzeniem na rynku.</summary>
        private static Settlement NearestPort(MobileParty party, bool food)
        {
            var pos = party.GetPosition2D;
            var f = party.MapFaction;
            Settlement best = null; float bd = float.MaxValue; bool bestOwn = false;
            foreach (var t in Town.AllFiefs)
            {
                var st = t != null ? t.Settlement : null;
                if (st == null || !st.HasPort || st.IsUnderSiege || st.MapFaction == null) continue;
                bool own = st.MapFaction == f;
                bool war;
                try { war = FactionManager.IsAtWarAgainstFaction(st.MapFaction, f); } catch { war = true; }
                if (!own && war) continue;
                if (food && (!st.IsTown || st.ItemRoster == null || st.ItemRoster.TotalFood <= 0)) continue;
                float d = pos.DistanceSquared(st.GetPosition2D);
                // najpierw porty wlasnego krolestwa, potem neutralne (Lannisport, Braavos...)
                if (best == null || (own && !bestOwn) || (own == bestOwn && d < bd)) { best = st; bd = d; bestOwn = own; }
            }
            return best;
        }

        // ================================================================ zdarzenia
        internal static void OnSettlementEntered(MobileParty mp, Settlement st, Hero hero)
        {
            try
            {
                if (mp == null || st == null || !st.HasPort || !st.IsFortification || !mp.IsLordParty || mp.IsMainParty) return;
                if (mp.MapFaction == null || mp.MapFaction.StringId != IronId) return;
                _portVisit[mp] = (int)CampaignTime.Now.ToHours;
                _portCache.Remove(mp);
                float hp = 0;
                if (mp.Ships != null) foreach (var sh in mp.Ships) if (sh != null) hp += sh.HitPoints;
                _hullAtPort[mp] = new Hull { Hp = hp, Gold = mp.LeaderHero != null ? mp.LeaderHero.Gold : 0, Day = Today };
            }
            catch (Exception e) { Stumble("OnSettlementEntered", e); }
        }

        internal static void OnVillageLooted(Village v)
        {
            try
            {
                var st = v != null ? v.Settlement : null;
                var att = st != null ? st.LastAttackerParty : null;
                if (att == null || att.MapFaction == null || att.MapFaction.StringId != IronId) return;
                _dLootN++;
                if (_dLoot.Count < 10) _dLoot.Add(Nm(st) + " (" + (CultureOf(st) ?? "-") + ")");
            }
            catch (Exception e) { Stumble("OnVillageLooted", e); }
        }

        private static void NoteIronSiege(MobileParty lead, Settlement st)
        {
            _dIronSiegesN++;
            if (_dIronSieges.Count < 10) _dIronSieges.Add(Nm(st) + " (" + (CultureOf(st) ?? "-") + ", " + Nm(st.MapFaction) + ")");
        }

        // ================================================================ Z0: linia doby
        private static string RangeText()
        {
            try
            {
                var c = Campaign.Current;
                float da = c.GetAverageDistanceBetweenClosestTwoTownsWithNavigationType(MobileParty.NavigationType.All);
                float dn = c.GetAverageDistanceBetweenClosestTwoTownsWithNavigationType(MobileParty.NavigationType.Naval);
                float dd = c.GetAverageDistanceBetweenClosestTwoTownsWithNavigationType(MobileParty.NavigationType.Default);
                var pyke = Settlement.Find("town_S3"); var ff = Settlement.Find("ROT_town34");
                string d = "-";
                if (pyke != null && ff != null) { float x = c.Models.MapDistanceModel.GetDistance(pyke, ff, pyke.HasPort, ff.HasPort, MobileParty.NavigationType.All); d = x >= 0f && x < 1e7f ? F0(x) : "-"; }
                return "prog najazdu (All/Naval/Default) " + F0(5 * da) + "/" + F0(5 * dn) + "/" + F0(5 * dd) + ", Pyke->Flint's Finger droga " + d + ", mnoznik statkow gry " + _wAmp
                       + ", weto siedzib 274 dla Wysp: strona " + CanonWarBalance.SideOf(FindKingdom(IronId)) + (CanonWarBalance.SideOf(FindKingdom(IronId)) == 0 ? " (Winterfell bez weta - Z4)" : " (UWAGA: Wyspy w koalicji 274)");
            }
            catch (Exception e) { Stumble("RangeText", e); return "?"; }
        }

        private static double Med(List<float> xs)
        {
            if (xs.Count == 0) return double.NaN;
            var a = new List<float>(xs); a.Sort();
            int n = a.Count;
            return (n % 2) == 1 ? a[n / 2] : 0.5 * (a[n / 2 - 1] + a[n / 2]);
        }

        private static void IronDaily(int today, bool log)
        {
            var iron = FindKingdom(IronId);
            if (iron == null) return;
            var c = Campaign.Current;
            float thrRaid = 8f, thrSiege = 8f;
            try { thrRaid = c.Models.MobilePartyAIModel.NeededFoodsInDaysThresholdForRaid; thrSiege = c.Models.MobilePartyAIModel.NeededFoodsInDaysThresholdForSiege; } catch { }
            float range = 5f * c.GetAverageDistanceBetweenClosestTwoTownsWithNavigationType(MobileParty.NavigationType.All);
            float naval = c.EstimatedAverageLordPartyNavalSpeed;
            // wsie Polnocy (cele) - raz na dobe
            var nvill = new List<Settlement>();
            var north = FindKingdom(NorthId);
            if (north != null) foreach (var st in north.Settlements) if (st != null && st.IsVillage) nvill.Add(st);
            bool detail = log && (_ironDetailDay < 0 || today - _ironDetailDay >= 7);
            if (detail) _ironDetailDay = today;
            int P = 0, isl = 0, landN = 0, sea = 0, other = 0, sFood = 0, sArmy = 0, sVill = 0, sRange = 0, withRaid = 0, opts = 0, withSiege = 0, safeBad = 0, crewOver = 0, hungry = 0;
            float hullMin = 2f;
            var amps = new List<float>();
            int armies = 0;
            try { armies = iron.Armies.Count; } catch { }
            var sieges = new List<string>();
            var details = new List<string>();
            _safeBad.Clear();
            foreach (var mp in MobileParty.AllLordParties)
            {
                try
                {
                    if (mp == null || !mp.IsActive || mp.IsMainParty || mp.MapFaction != iron) continue;
                    P++;
                    // polozenie
                    string loc;
                    if (mp.IsCurrentlyAtSea) { loc = "morze"; sea++; }
                    else
                    {
                        var ns = NearestAny(mp.GetPosition2D);
                        string cu = CultureOf(ns);
                        if (cu == IronId) { loc = "wyspa"; isl++; }
                        else if (cu == NorthId) { loc = "lad Polnocy"; landN++; }
                        else { loc = "inne (" + (cu ?? "-") + ")"; other++; }
                    }
                    if (mp.BesiegedSettlement != null) sieges.Add(Nm(mp.BesiegedSettlement));
                    // statki
                    int ships = 0, cap = 0, men = mp.MemberRoster != null ? mp.MemberRoster.TotalManCount : 0;
                    float hsum = 0, hmin = 2f;
                    if (mp.Ships != null)
                        foreach (var sh in mp.Ships)
                        {
                            if (sh == null || !(sh.MaxHitPoints > 0f)) continue;
                            ships++;
                            float r = sh.HitPoints / sh.MaxHitPoints;
                            hsum += r; if (r < hmin) hmin = r;
                            if (r > 0.2f) cap += sh.TotalCrewCapacity;
                        }
                    if (mp.AttachedParties != null) foreach (var ap in mp.AttachedParties) if (ap != null && ap.MemberRoster != null) men += ap.MemberRoster.TotalManCount;
                    if (ships > 0 && hmin < hullMin) hullMin = hmin;
                    if (ships > 0 && cap < men) crewOver++;
                    // jedzenie
                    float fd = ReaverLaw.FoodDays(mp, 0);
                    bool foodBad = fd < thrRaid;
                    if (foodBad) { sFood++; hungry++; }
                    bool follower = mp.Army != null && mp.Army.LeaderParty != mp;
                    if (!foodBad && follower) sArmy++;
                    // najblizsza wies Polnocy
                    Settlement v = null; float vd = float.MaxValue;
                    var pos = mp.GetPosition2D;
                    for (int i = 0; i < nvill.Count; i++) { float d = pos.DistanceSquared(nvill[i].GetPosition2D); if (d < vd) { vd = d; v = nvill[i]; } }
                    float adj = -1f, navalPart = 0f, amp = 1f, safe = -1f, sail = -1f;
                    bool villBad = false;
                    if (v != null)
                    {
                        try { villBad = v.Village != null && v.Village.VillageState != Village.VillageStates.Normal; } catch { }
                        if (!foodBad && !follower && villBad) sVill++;
                        MobileParty.NavigationType nav; bool fp;
                        AiHelper.GetBestNavigationTypeAndAdjustedDistanceOfSettlementForMobileParty(mp, v, false, out nav, out adj, out fp);
                        if (nav == MobileParty.NavigationType.None) adj = -1f;
                        float lr;
                        float raw = c.Models.MapDistanceModel.GetDistance(mp, v, false, MobileParty.NavigationType.All, out lr);
                        if (raw >= 0f && raw < 1e7f && lr >= 0f) navalPart = raw * (1f - lr);
                        if (ships > 0 && navalPart > 0f)
                        {
                            amp = Amp(mp, navalPart);
                            if (amp >= 0f) amps.Add(amp);
                            try { safe = c.Models.CampaignShipDamageModel.GetEstimatedSafeSailDuration(mp); } catch { safe = -1f; }
                            sail = naval > 0f ? navalPart / naval : -1f;
                            if (safe >= 0f && sail > 0f && safe <= sail) { safeBad++; _safeBad.Add(mp); }
                        }
                        if (!foodBad && !follower && !villBad && (adj < 0f || adj > range)) sRange++;
                    }
                    IronThinkRec tr;
                    int ro = 0, so = 0;
                    if (_ironThink.TryGetValue(mp, out tr) && tr.Day >= today - 1) { ro = tr.Raid; so = tr.Siege; }
                    if (ro > 0) { withRaid++; opts += ro; }
                    if (so > 0) withSiege++;
                    // naprawa w porcie (gra przy wejsciu): kadlub wzrosl, zloto pana spadlo
                    Hull h;
                    if (_hullAtPort.TryGetValue(mp, out h))
                    {
                        float hp = 0;
                        if (mp.Ships != null) foreach (var sh in mp.Ships) if (sh != null) hp += sh.HitPoints;
                        int g = mp.LeaderHero != null ? mp.LeaderHero.Gold : 0;
                        if (hp > h.Hp + 1f) { _dRepairs++; _dRepairGold += Math.Max(0, h.Gold - g); }
                        _hullAtPort.Remove(mp);
                    }
                    if (detail && details.Count < 60)
                        details.Add(Nm(mp) + ": " + loc + ", statki " + ships + " (zaloga " + men + "/" + cap + "), kadlub min/sr " + (ships > 0 ? F2(hmin) + "/" + F2(hsum / ships) : "-")
                                    + ", jedzenie " + (fd >= 999f ? "-" : fd.ToString("0.0", Inv)) + " dni (prog " + thrRaid.ToString("0.#", Inv) + "/" + thrSiege.ToString("0.#", Inv) + ")"
                                    + ", wies " + Nm(v) + (villBad ? " (zlupiona)" : "") + " droga skor. " + (adj >= 0f ? F0(adj) : "-") + "/" + F0(range) + ", morze " + F0(navalPart)
                                    + " x" + (ships > 0 && navalPart > 0f ? F2(amp) : "-") + ", safe-sail " + (safe >= 0f ? safe.ToString("0.0", Inv) : "-") + " h / rejs " + (sail >= 0f ? sail.ToString("0.0", Inv) : "-")
                                    + " h, opcje rabunek/oblezenie " + ro + "/" + so + (follower ? ", w armii" : "") + ", postoj: " + mp.DefaultBehavior + " -> " + Nm(mp.TargetSettlement)
                                    + (mp.CurrentSettlement != null ? " (w " + Nm(mp.CurrentSettlement) + ")" : ""));
                }
                catch (Exception e) { Stumble("IronDaily.party", e); }
            }
            if (!log) return;
            double am = Med(amps);
            float amax = 0f; foreach (var a in amps) if (a > amax) amax = a;
            var sb = new StringBuilder(1200);
            sb.Append(Tag).Append("Zelazni | dzien ").Append(today).Append(" | klucz ").Append(Key01(IronOn)).Append(", wojna z Polnoca ").Append(AtWarWithNorth(iron) ? "TAK" : "NIE")
              .Append(" | partii ").Append(P).Append(": wyspa ").Append(isl).Append(", lad Polnocy ").Append(landN).Append(", morze ").Append(sea).Append(", inne ").Append(other)
              .Append(" | sita: jedzenie ").Append(sFood).Append(", armia ").Append(sArmy).Append(", stan wsi ").Append(sVill).Append(", zasieg ").Append(sRange)
              .Append(" | z opcja rabunku ").Append(withRaid).Append(" (opcji ").Append(opts).Append("), z opcja oblezenia ").Append(withSiege)
              .Append(" | mnoznik statkow med/max ").Append(double.IsNaN(am) ? "-" : am.ToString("0.00", Inv)).Append('/').Append(amps.Count > 0 ? F2(amax) : "-")
              .Append(", kadlub min ").Append(hullMin <= 1f ? F2(hullMin) : "-").Append(", safe-sail<rejs ").Append(safeBad).Append(", zaloga>pojemnosc ").Append(crewOver)
              .Append(", ponizej progu jedzenia ").Append(hungry).Append('/').Append(P)
              .Append(" | Z1 do portu ").Append(_dToPort).Append(" (jedzenie ").Append(_dToPortFood).Append(", kadlub ").Append(_dToPortHull).Append(", safe-sail ").Append(_dToPortSafe).Append(')')
              .Append(", Z2 naprawy ").Append(_dRepairs).Append(" (").Append(_dRepairGold).Append(" zl), statki z puli 0 (Z2b nie wdrozony)")
              .Append(" | armie ").Append(armies).Append(", oblezenia ").Append(sieges.Count).Append(sieges.Count > 0 ? " [" + string.Join(", ", sieges.ToArray()) + "]" : "")
              .Append(", nowe oblezenia dzis ").Append(_dIronSiegesN).Append(_dIronSieges.Count > 0 ? " [" + string.Join(", ", _dIronSieges.ToArray()) + "]" : "")
              .Append(", rabunki z lupem ").Append(_dLootN).Append(_dLoot.Count > 0 ? " [" + string.Join(", ", _dLoot.ToArray()) + "]" : "")
              .Append(", wsi Polnocy ").Append(nvill.Count).Append('.');
            Log.Info(sb.ToString());
            foreach (var d in details) Log.Info(Tag + "Zelazni partia | dzien " + today + " | " + d + ".");
        }

        private static Settlement NearestAny(Vec2 pos)
        {
            Settlement best = null; float bd = float.MaxValue;
            foreach (var t in Town.AllFiefs) { var st = t != null ? t.Settlement : null; if (st == null) continue; float d = pos.DistanceSquared(st.GetPosition2D); if (d < bd) { bd = d; best = st; } }
            return best;
        }
    }
}
