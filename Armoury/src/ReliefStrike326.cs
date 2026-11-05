using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;

namespace Armoury
{
    /// <summary>
    /// 326c - ODSIECZ ATAKUJE, GDY MA PRZEWAGE (Jeff 05.11: "nie musza czekac na 90% pokrycia skoro maja 7k zolnierzy a przeciwnik 3k - atakuja"; odpoczynek bez zmian).
    /// Klucze ReliefEndsGatheringOnAdvantage (C1) i ReliefAttacksOnAdvantage (C2).
    ///
    /// DLACZEGO (log Armoury-2026-11-04_23-31-36, dekompilacja gry 1.4.8):
    ///  - Armia odsieczy Deep Den (Joffrey, 34 druzyny, ok. 8000 ludzi, sila z wezwanymi 10.7k wobec obozu 4.5-5.1k) stala w zbiorce 109326-109340: wodz czekajacej armii
    ///    nie zmienia celu (AiMilitaryBehavior.AiHourlyTick: IsWaitingForArmyMembers -> DoNotChangeBehavior), a zbiorka konczy sie przy 90% sily wezwanych
    ///    (Army.IsWaitingForArmyMembers), zegar zbiorki rusza dopiero, gdy wodz z rozkazem GoToPoint jest blisko punktu (Army.CheckAndSetArmyGatheringTime).
    ///    Odpoczynek NIE odsyla wodza z marszu na zbiorke: NightRest.ApplyOrder oddaje DefendSettlement po nocy, ArmyRestDay nie zaczyna postoju przy DefendSettlement,
    ///    a w zbiorce jedynym celem wodza i tak jest punkt zbiorki (Army.MoveLeaderToGatheringLocationIfNeeded) - wiec odpoczynku nie ruszamy (decyzja Jeffa B).
    ///  - Pod murami gra rusza na oboz, gdy druzyny z obrona tej osady w 9 jedn. od wodza obozu (SettlementDefendingNearbyPartyCheckRadius = 3 x 3) maja sile
    ///    (EstimatedStrength) > 1.2 x sila bojowa obozu jako obroncy (GetCustomStrength Defender) - MobilePartyAi.GetNearbyPartyDataWhileDefendingSettlement.
    ///    Ta druga miara bywa wyzsza od EstimatedStrength obozu: Joffrey 109341-109345 stal pod Deep Den z ok. 10.7k (krotko GoToPoint wokol wodza obozu), oboz 5.1k - zamek padl.
    ///
    /// REGULA (kazde krolestwo, takze armie gry, BK i OB5; druzyna i armia gracza nigdy):
    ///  C1 armia w zbiorce z celem odsieczy (Army.Gather z typem Defender - zapamietane; OB5 - osada odsieczy; po wczytaniu - najblizsza oblezona twierdza krolestwa)
    ///     konczy zbiorke (postfiks Army.IsWaitingForArmyMembers -> false, na stale do rozwiazania armii), gdy: oblezenie trwa, oblegajacy w wojnie z krolestwem,
    ///     wodz jest w zasiegu obrony gry (8.02 doby marszu w linii prostej - dalej gra nie daje oceny obrony), a sila przy wodzu (armia: wodz + dolaczeni;
    ///     wezwani w 9 jedn. od wodza) + odsiecz pod murami (jak gra: druzyny z obrona tej osady w 9 jedn. od wodza obozu) >= 1.2 x sila obozu (suma EstimatedStrength
    ///     druzyn obozu - ta sama miara co OB5). Dalej rozkaz daje mysl gry (DefendSettlement tej osady - ocena gry i podbicie OB5); wezwani doganiaja armie (eskorta gry).
    ///  C2 osada, przy ktorej odsiecz pod murami (j.w.) >= 1.2 x sila obozu (EstimatedStrength): postfiks GetNearbyPartyDataWhileDefendingSettlement daje
    ///     shouldEngage (i strone ladowa) kazdej druzynie z obrona tej osady w promieniu gry - najsilniejsza rusza na oboz (EngageParty), reszta idzie do niej i wchodzi
    ///     do bitwy, gdy ta juz walczy (droga gry). Bez przewagi - nic (bez samobojczych atakow). Oboz na morzu - bez zmian.
    ///  1.2 = prog gry na atak odsieczy twierdzy (num15) i OB5 GameEngage. Ocena co godzine gry (zdarzenie godzinowe), w mysli tylko odczyt zbiorow.
    /// Linie: "Odsiecz z przewaga (326c): start sesji", "... koniec zbiorki", "... atak pod murami" (raz na osade i dobe), "... dzien N" (doba). Bez zapisu.
    /// </summary>
    internal static class ReliefStrike
    {
        internal const string Tag = "Odsiecz z przewaga (326c): ";
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        internal const float Edge = 1.2f;   // gra: MobilePartyAi.GetNearbyPartyDataWhileDefendingSettlement num15 (twierdza); OB5 GameEngage

        private static readonly Dictionary<Army, Settlement> _gatherFor = new Dictionary<Army, Settlement>();   // Army.Gather typu Defender -> osada odsieczy
        private static readonly HashSet<Army> _released = new HashSet<Army>();                                    // armie po koncu zbiorki z przewagi
        private static readonly HashSet<Settlement> _strike = new HashSet<Settlement>();                         // osady z przewaga odsieczy pod murami (ta godzina)
        private sealed class Info { public float Bes, Walls; public int N; public SiegeEvent Ev; public MobileParty Lead; }
        private static readonly Dictionary<Settlement, Info> _info = new Dictionary<Settlement, Info>();
        private static bool _guard;
        private static double _hour = -1.0;
        private static int _dReleased, _dHeldHours, _dFlips, _stumbles, _dStumbles;
        private static readonly List<string> _dRelNames = new List<string>();
        private static readonly HashSet<Settlement> _dStrikeSt = new HashSet<Settlement>();
        private static readonly HashSet<string> _err = new HashSet<string>();
        private static string _wGather = "BRAK", _wWait = "BRAK", _wNear = "BRAK";
        private static bool _patched;

        private static Settings S { get { return Settings.Current; } }
        internal static bool EndOn { get { var s = S; return s != null && s.ReliefEndsGatheringOnAdvantage; } }
        internal static bool StrikeOn { get { var s = S; return s != null && s.ReliefAttacksOnAdvantage; } }
        private static int Today { get { return (int)CampaignTime.Now.ToDays; } }

        internal static void Reset()
        {
            _gatherFor.Clear(); _released.Clear(); _strike.Clear(); _info.Clear(); _guard = false; _hour = -1.0;
            _stumbles = 0; ClearDay();
        }

        private static void ClearDay() { _dReleased = _dHeldHours = _dFlips = _dStumbles = 0; _dRelNames.Clear(); _dStrikeSt.Clear(); }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++; _dStumbles++;
            try { if (_err.Add(where)) Log.Error("ReliefStrike." + where, e); } catch { }
        }

        private static string Nm(Settlement st) { try { return st != null && st.Name != null ? st.Name.ToString() : "-"; } catch { return "?"; } }
        private static string Nm(IFaction f) { try { return f != null && f.Name != null ? f.Name.ToString() : "-"; } catch { return "?"; } }
        private static string Nm(MobileParty mp) { try { return mp != null && mp.LeaderHero != null ? mp.LeaderHero.Name.ToString() : (mp != null && mp.Name != null ? mp.Name.ToString() : "-"); } catch { return "?"; } }
        private static string F0(float v) { return v.ToString("0", Inv); }

        // ------------------------------------------------------------ latki
        internal static void ApplyAll(Harmony h)
        {
            if (h == null || _patched) return;
            _patched = true;
            try
            {
                var m = AccessTools.Method(typeof(Army), "Gather", new[] { typeof(Settlement), typeof(TaleWorlds.Library.MBReadOnlyList<MobileParty>) });
                if (m != null) { h.Patch(m, prefix: new HarmonyMethod(typeof(ReliefStrike), nameof(GatherPrefix))); _wGather = "TAK"; } else _wGather = "BRAK (Army.Gather)";
            }
            catch (Exception e) { _wGather = "BLAD (" + e.GetType().Name + ")"; Stumble("ApplyAll.Gather", e); }
            try
            {
                var m = AccessTools.Method(typeof(Army), "IsWaitingForArmyMembers", Type.EmptyTypes);
                if (m != null) { h.Patch(m, postfix: new HarmonyMethod(typeof(ReliefStrike), nameof(WaitPostfix))); _wWait = "TAK"; } else _wWait = "BRAK (Army.IsWaitingForArmyMembers)";
            }
            catch (Exception e) { _wWait = "BLAD (" + e.GetType().Name + ")"; Stumble("ApplyAll.Wait", e); }
            try
            {
                var m = AccessTools.Method(typeof(MobilePartyAi), "GetNearbyPartyDataWhileDefendingSettlement");
                if (m != null) { h.Patch(m, postfix: new HarmonyMethod(typeof(ReliefStrike), nameof(NearbyPostfix))); _wNear = "TAK"; } else _wNear = "BRAK (MobilePartyAi.GetNearbyPartyDataWhileDefendingSettlement)";
            }
            catch (Exception e) { _wNear = "BLAD (" + e.GetType().Name + ")"; Stumble("ApplyAll.Near", e); }
        }

        /// <summary>Prefiks Army.Gather: osada odsieczy armii typu Defender (AiBehaviorObject dostaje potem miejsce zbiorki). Nie rzuca.</summary>
        public static void GatherPrefix(Army __instance, Settlement initialHostileSettlement)
        {
            try { if (__instance != null && initialHostileSettlement != null && __instance.ArmyType == Army.ArmyTypes.Defender) _gatherFor[__instance] = initialHostileSettlement; }
            catch { }
        }

        /// <summary>Postfiks Army.IsWaitingForArmyMembers: armia z koncem zbiorki z przewagi nie czeka. Tylko odczyt zbioru. Nie rzuca.</summary>
        public static void WaitPostfix(Army __instance, ref bool __result)
        {
            if (!__result || _guard || _released.Count == 0 || __instance == null) return;
            try { if (_released.Contains(__instance) && EndOn) __result = false; } catch { }
        }

        /// <summary>Postfiks MobilePartyAi.GetNearbyPartyDataWhileDefendingSettlement: odsiecz z przewaga pod murami rusza na oboz (najsilniejsza prowadzi). Nie rzuca.</summary>
        public static void NearbyPostfix(Settlement targetSettlement, ref bool shouldJoinLandSide, ref bool shouldEngage, bool __result)
        {
            if (!__result || shouldEngage || _strike.Count == 0 || targetSettlement == null) return;
            try
            {
                if (!StrikeOn || !_strike.Contains(targetSettlement)) return;
                shouldEngage = true;
                shouldJoinLandSide = true;
                _dFlips++;
            }
            catch { }
        }

        /// <summary>OB5 / D: osada odsieczy armii (zapamietana przy Army.Gather albo armia OB5) albo null.</summary>
        internal static Settlement GatherTarget(Army a)
        {
            Settlement st;
            if (a != null && _gatherFor.TryGetValue(a, out st)) return st;
            return null;
        }

        internal static void OnArmyDispersed(Army army)
        {
            try { if (army != null) { _gatherFor.Remove(army); _released.Remove(army); } }
            catch (Exception e) { Stumble("OnArmyDispersed", e); }
        }

        // ------------------------------------------------------------ godzina
        /// <summary>Druzyny z obrona osady w promieniu gry od wodza obozu (kryteria gry: nie doczepione, DefendSettlement tej osady albo krotko EngageParty na
        /// ostatniego atakujacego; armia - sila armii). Pomija druzyny armii skip (C1 liczy armie osobno).</summary>
        private static float AtWalls(Settlement st, MobileParty campLead, Army skip, out int n)
        {
            n = 0;
            float sum = 0f;
            float r = Campaign.Current.Models.MobilePartyAIModel.SettlementDefendingNearbyPartyCheckRadius;
            float r2 = r * r;
            var v = campLead.GetPosition2D;
            var all = MobileParty.AllLordParties;
            for (int i = 0; i < all.Count; i++)
            {
                var p = all[i];
                if (p == null || !p.IsActive || p.AttachedTo != null || p.IsCurrentlyAtSea || p.CurrentSettlement != null) continue;
                if (skip != null && p.Army == skip) continue;
                bool def = (p.DefaultBehavior == AiBehavior.DefendSettlement && p.TargetSettlement == st)
                           || (p.ShortTermBehavior == AiBehavior.EngageParty && p.ShortTermTargetParty != null && p.ShortTermTargetParty == st.LastAttackerParty);
                if (!def) continue;
                if (p.GetPosition2D.DistanceSquared(v) > r2) continue;
                sum += p.Army != null && p.Army.LeaderParty == p ? p.Army.EstimatedStrength : p.Party.EstimatedStrength;
                n++;
            }
            return sum;
        }

        /// <summary>Zdarzenie godzinowe (ReliefBehavior): przewaga pod murami (C2) i koniec zbiorek (C1). Nie rzuca.</summary>
        internal static void Hourly()
        {
            try
            {
                if (Campaign.Current == null) return;
                double h = Math.Floor(CampaignTime.Now.ToHours);
                if (h == _hour) return;
                _hour = h;
                _strike.Clear(); _info.Clear();
                bool c1 = EndOn, c2 = StrikeOn;
                if (!c1 && !c2) return;
                var mgr = Campaign.Current.SiegeEventManager;
                if (mgr == null) return;
                var evs = mgr.SiegeEvents;
                for (int i = 0; i < evs.Count; i++)
                {
                    var ev = evs[i];
                    try
                    {
                        if (ev == null || ev.ReadyToBeRemoved) continue;
                        var st = ev.BesiegedSettlement;
                        var camp = ev.BesiegerCamp;
                        var lead = camp != null ? camp.LeaderParty : null;
                        if (st == null || !st.IsFortification || lead == null || !lead.IsActive || st.MapFaction == null || camp.MapFaction == null || !camp.MapFaction.IsAtWarWith(st.MapFaction)) continue;
                        bool und;
                        try { und = Undead.Party(lead) || (st.OwnerClan != null && ClanIncomeBook.IsUndeadClan(st.OwnerClan)); } catch { und = false; }
                        if (und) continue;   // Inni - CrashScribe
                        float bes = 0f;
                        foreach (var pb in camp.GetInvolvedPartiesForEventType()) if (pb != null) bes += pb.EstimatedStrength;
                        if (bes <= 0f) continue;
                        int n;
                        float walls = AtWalls(st, lead, null, out n);
                        _info[st] = new Info { Bes = bes, Walls = walls, N = n, Ev = ev, Lead = lead };
                        if (c2 && !lead.IsCurrentlyAtSea && walls >= Edge * bes)
                        {
                            _strike.Add(st);
                            if (_dStrikeSt.Add(st))
                                Log.Info(Tag + "atak pod murami | dzien " + Today + " | " + Nm(st) + " (" + st.StringId + ", " + Nm(st.MapFaction) + ") | odsiecz pod murami " + F0(walls) + " (druzyn " + n
                                         + ", w " + Campaign.Current.Models.MobilePartyAIModel.SettlementDefendingNearbyPartyCheckRadius.ToString("0", Inv) + " jedn. od wodza obozu) >= " + Edge.ToString("0.0", Inv)
                                         + " x oboz " + F0(bes) + " (" + Nm(camp.MapFaction) + ", wodz " + Nm(lead) + ") - najsilniejsza prowadzi na oboz, reszta za nia.");
                        }
                    }
                    catch (Exception e) { Stumble("Hourly.siege", e); }
                }
                if (!c1 || _info.Count == 0) return;
                foreach (var k in Kingdom.All)
                {
                    if (k == null || k.IsEliminated) continue;
                    var armies = k.Armies;
                    for (int i = 0; i < armies.Count; i++)
                    {
                        var a = armies[i];
                        try { Consider(a, k); }
                        catch (Exception e) { Stumble("Hourly.army", e); }
                    }
                }
            }
            catch (Exception e) { Stumble("Hourly", e); }
        }

        private static Settlement TargetOf(Army a, Kingdom k, MobileParty lead)
        {
            var st = GatherTarget(a);
            if (st == null) st = BesiegedRelief.ArmyFor(a);
            if (st != null) return st;
            if (a.ArmyType != Army.ArmyTypes.Defender) return null;
            // po wczytaniu (bez zapisu Gather): najblizsza oblezona twierdza krolestwa
            Settlement best = null; float bd = float.MaxValue;
            var pos = lead.GetPosition2D;
            foreach (var kv in _info)
            {
                if (kv.Key.MapFaction != k) continue;
                float d = kv.Key.GetPosition2D.DistanceSquared(pos);
                if (d < bd) { bd = d; best = kv.Key; }
            }
            return best;
        }

        private static void Consider(Army a, Kingdom k)
        {
            if (a == null || _released.Contains(a)) return;
            var lead = a.LeaderParty;
            if (lead == null || !lead.IsActive || lead.IsMainParty || lead.MapEvent != null || lead.SiegeEvent != null) return;
            var st = TargetOf(a, k, lead);
            if (st == null) return;
            Info inf;
            if (!_info.TryGetValue(st, out inf)) return;                                       // osada nie oblegana (albo Inni)
            if (st.MapFaction == null || st.MapFaction.IsAtWarWith(k) || !inf.Ev.BesiegerCamp.MapFaction.IsAtWarWith(k)) return;
            bool wait;
            _guard = true;
            try { wait = a.IsWaitingForArmyMembers(); }
            finally { _guard = false; }
            if (!wait) return;
            // zasieg obrony gry: dalej gra nie daje oceny obrony (8.02 doby, AiMilitaryBehavior.GetDistanceScoreForDefending) - armia bez celu rozwiazalaby sie w mysli
            float speed = Campaign.Current.EstimatedAverageLordPartySpeed;
            float days = speed > 0f ? lead.GetPosition2D.Distance(st.GetPosition2D) / (speed * CampaignTime.HoursInDay) : 99f;
            if (days > CanonWarBalance.DefendRangeDays) return;
            float r = Campaign.Current.Models.MobilePartyAIModel.SettlementDefendingNearbyPartyCheckRadius, r2 = r * r;
            float present = a.EstimatedStrength;   // wodz + dolaczeni
            int near = 0, far = 0;
            var lp = lead.GetPosition2D;
            var ps = a.Parties;
            for (int i = 0; i < ps.Count; i++)
            {
                var p = ps[i];
                if (p == null || p == lead || p.AttachedTo == lead || !p.IsActive) continue;
                if (p.GetPosition2D.DistanceSquared(lp) <= r2) { present += p.Party.EstimatedStrength; near++; }
                else far++;
            }
            int nw;
            float walls = AtWalls(st, inf.Lead, a, out nw);
            if (present + walls < Edge * inf.Bes) { _dHeldHours++; return; }
            _released.Add(a);
            _dReleased++;
            if (_dRelNames.Count < 8) _dRelNames.Add(Nm(lead) + " -> " + Nm(st));
            Log.Info(Tag + "koniec zbiorki | dzien " + Today + " | armia " + Nm(lead) + " (" + Nm(k) + ", typ " + a.ArmyType + ") -> odsiecz " + Nm(st) + " (" + st.StringId + ")"
                     + " | przy wodzu " + F0(present) + " (dolaczeni " + a.LeaderPartyAndAttachedPartiesCount + " druzyn, wezwani w promieniu " + near + "), pod murami " + F0(walls) + " (druzyn " + nw + ")"
                     + ", razem " + F0(present + walls) + " >= " + Edge.ToString("0.0", Inv) + " x oboz " + F0(inf.Bes) + " | wezwanych dalej " + far + " (doganiaja armie)"
                     + " | droga " + days.ToString("0.0", Inv) + " doby w linii prostej - nie czeka na 90% wezwanych.");
        }

        // ------------------------------------------------------------ linie
        internal static void SessionStart()
        {
            try
            {
                Log.Info(Tag + "start sesji | koniec zbiorki z przewaga " + (EndOn ? "TAK" : "NIE") + ", atak pod murami z przewaga " + (StrikeOn ? "TAK" : "NIE")
                         + " | latki: Army.Gather " + _wGather + ", Army.IsWaitingForArmyMembers " + _wWait + ", GetNearbyPartyDataWhileDefendingSettlement " + _wNear
                         + " | prog " + Edge.ToString("0.0", Inv) + " x sila obozu (EstimatedStrength), promien gry pod murami i przy wodzu "
                         + (Campaign.Current != null ? Campaign.Current.Models.MobilePartyAIModel.SettlementDefendingNearbyPartyCheckRadius.ToString("0", Inv) : "-") + " jedn.; odpoczynek bez zmian.");
            }
            catch (Exception e) { Stumble("SessionStart", e); }
        }

        internal static void Daily()
        {
            try
            {
                var s = S;
                if (s != null && s.LogEnabled)
                {
                    var sb = new StringBuilder(600);
                    sb.Append(Tag).Append("dzien ").Append(Today).Append(" | koniec zbiorki ").Append(EndOn ? "TAK" : "NIE").Append(", atak pod murami ").Append(StrikeOn ? "TAK" : "NIE")
                      .Append(" | zbiorki zakonczone z przewagi dzis ").Append(_dReleased).Append(_dRelNames.Count > 0 ? " [" + string.Join(", ", _dRelNames.ToArray()) + "]" : "")
                      .Append(", godzin armii odsieczy w zbiorce bez przewagi ").Append(_dHeldHours)
                      .Append(", armie po koncu zbiorki teraz ").Append(_released.Count)
                      .Append(" | osady z atakiem pod murami dzis ").Append(_dStrikeSt.Count).Append(", przelaczen druzyn na atak ").Append(_dFlips)
                      .Append(" | oblezenia teraz (bez Innych) ").Append(_info.Count)
                      .Append(" | potkniecia ").Append(_dStumbles).Append(" (sesja ").Append(_stumbles).Append(").");
                    Log.Info(sb.ToString());
                }
            }
            catch (Exception e) { Stumble("Daily", e); }
            ClearDay();
        }
    }
}
