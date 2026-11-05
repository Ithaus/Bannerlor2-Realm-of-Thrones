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
    ///  C3 (Jeff 05.11 pkt 3, klucz ReliefEndsGatheringAfterDay): armia w zbiorce z celem odsieczy stojaca co najmniej dobe od zalozenia (Army._creationTime - w zapisie gry)
    ///     konczy zbiorke bez warunku przewagi (te same warunki oblezenia, wojny i zasiegu co C1). Idzie pod mury droga gry; na oboz rusza tylko z przewaga (C2 / gra).
    ///  Recenzja 326c: koniec zbiorki trzyma tylko, dopoki osada celu jest oblegana (R1-4: inaczej armia "nie czeka" bez celu i gra ja rozwiazuje); po wczytaniu cel
    ///     armii bez wpisu - najblizsza miejscu zbiorki oblegana twierdza krolestwa w 2 x sredniej odleglosci dwoch najblizszych miast gry (R1-7 / R3-9);
    ///     C2 tylko na ladzie, bez blokady morskiej i tylko dla tego samego oblezenia, co policzone w tej godzinie (R1-6 / R3-10).
    /// Linie: "Odsiecz z przewaga (326c): start sesji", "... koniec zbiorki" (powod: przewaga albo czas), "... atak pod murami" (raz na osade i dobe; z miara gry obozu),
    /// "... dzien N" (doba; oblezenia z sila pod murami, miara gry i odsiecza poza promieniem gry). Bez zapisu.
    /// </summary>
    internal static class ReliefStrike
    {
        internal const string Tag = "Odsiecz z przewaga (326c): ";
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        internal const float Edge = 1.2f;   // gra: MobilePartyAi.GetNearbyPartyDataWhileDefendingSettlement num15 (twierdza); OB5 GameEngage

        private static readonly Dictionary<Army, Settlement> _gatherFor = new Dictionary<Army, Settlement>();   // Army.Gather typu Defender -> osada odsieczy
        private static readonly Dictionary<Army, Settlement> _released = new Dictionary<Army, Settlement>();      // armie po koncu zbiorki (przewaga / czas) -> osada odsieczy
        private static readonly HashSet<Settlement> _strike = new HashSet<Settlement>();                         // osady z przewaga odsieczy pod murami (ta godzina)
        private sealed class Info { public float Bes, Walls, Game, Out, OutDist = -1f; public int N, NOut; public SiegeEvent Ev; public MobileParty Lead; }
        private static readonly Dictionary<Settlement, Info> _info = new Dictionary<Settlement, Info>();
        private static bool _guard;
        private static double _hour = -1.0;
        private static int _dReleased, _dReleasedTime, _dHeldHours, _dFlips, _dSeaSkip, _dOutAdvHours, _stumbles, _dStumbles;
        private static readonly List<string> _dSieges = new List<string>();   // linia doby: oblezenia z ostatniej godziny doby (do 6)
        private static AccessTools.FieldRef<MobilePartyAi, MobileParty> _aiParty;
        private static AccessTools.FieldRef<Army, CampaignTime> _armyCreated;
        private static readonly List<string> _dRelNames = new List<string>();
        private static readonly HashSet<Settlement> _dStrikeSt = new HashSet<Settlement>();
        private static readonly HashSet<string> _err = new HashSet<string>();
        private static string _wGather = "BRAK", _wWait = "BRAK", _wNear = "BRAK";
        private static bool _patched;

        private static Settings S { get { return Settings.Current; } }
        internal static bool EndOn { get { var s = S; return s != null && s.ReliefEndsGatheringOnAdvantage; } }
        internal static bool StrikeOn { get { var s = S; return s != null && s.ReliefAttacksOnAdvantage; } }
        internal static bool TimeOn { get { var s = S; return s != null && s.ReliefEndsGatheringAfterDay; } }
        internal const float GatherMaxHours = 24f;   // Jeff 05.11 pkt 3: stoi na zbiorce dluzej niz dobe - rusza
        private static int Today { get { return (int)CampaignTime.Now.ToDays; } }

        internal static void Reset()
        {
            _gatherFor.Clear(); _released.Clear(); _strike.Clear(); _info.Clear(); _guard = false; _hour = -1.0;
            _stumbles = 0; ClearDay();
        }

        private static void ClearDay() { _dReleased = _dReleasedTime = _dHeldHours = _dFlips = _dSeaSkip = _dOutAdvHours = _dStumbles = 0; _dRelNames.Clear(); _dStrikeSt.Clear(); }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++; _dStumbles++;
            try { if (_err.Add(where)) Log.Error("ReliefStrike." + where, e); } catch { }
        }

        private static string Nm(Settlement st) { try { return st != null && st.Name != null ? st.Name.ToString() : "-"; } catch { return "?"; } }
        private static string Nm(IFaction f) { try { return f != null && f.Name != null ? f.Name.ToString() : "-"; } catch { return "?"; } }
        private static string Nm(MobileParty mp) { try { return mp != null && mp.LeaderHero != null ? mp.LeaderHero.Name.ToString() : (mp != null && mp.Name != null ? mp.Name.ToString() : "-"); } catch { return "?"; } }
        private static string F0(float v) { return v.ToString("0", Inv); }
        private static string F(double v) { return v.ToString("0.0", Inv); }

        // ------------------------------------------------------------ latki
        internal static void ApplyAll(Harmony h)
        {
            if (h == null || _patched) return;
            _patched = true;
            try { _aiParty = AccessTools.FieldRefAccess<MobilePartyAi, MobileParty>("_mobileParty"); } catch (Exception e) { _aiParty = null; Stumble("ApplyAll.aiParty", e); }
            try { _armyCreated = AccessTools.FieldRefAccess<Army, CampaignTime>("_creationTime"); } catch (Exception e) { _armyCreated = null; Stumble("ApplyAll.armyCreated", e); }
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
            try
            {
                Settlement st;
                // recenzja R1-4: tylko dopoki osada celu jest oblegana - potem armia znow czeka jak w grze (nie rozwiazuje sie bez celu)
                if (_released.TryGetValue(__instance, out st) && (EndOn || TimeOn) && st != null && st.SiegeEvent != null) __result = false;
            }
            catch { }
        }

        /// <summary>Postfiks MobilePartyAi.GetNearbyPartyDataWhileDefendingSettlement: odsiecz z przewaga pod murami rusza na oboz (najsilniejsza prowadzi). Nie rzuca.</summary>
        public static void NearbyPostfix(MobilePartyAi __instance, Settlement targetSettlement, ref bool shouldJoinLandSide, ref bool shouldEngage, bool __result)
        {
            if (!__result || shouldEngage || _strike.Count == 0 || targetSettlement == null) return;
            try
            {
                if (!StrikeOn || !_strike.Contains(targetSettlement)) return;
                // recenzja R1-6 / R3-10: to samo oblezenie co policzone w tej godzinie, bez blokady morskiej, druzyna na ladzie (na morzu nie liczona do sily)
                var ev = targetSettlement.SiegeEvent;
                Info inf;
                if (ev == null || ev.IsBlockadeActive || !_info.TryGetValue(targetSettlement, out inf) || !ReferenceEquals(inf.Ev, ev)) return;
                var mp = _aiParty != null && __instance != null ? _aiParty(__instance) : null;
                if (mp == null || mp.IsCurrentlyAtSea) { _dSeaSkip++; return; }
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
            float o, od; int no;
            return AtWalls(st, campLead, skip, out n, out o, out no, out od);
        }

        /// <summary>j.w.; dodatkowo (diagnostyka R3-3) odsiecz tej osady poza promieniem gry, ale w 3 x promien: sila, liczba i najblizszy dystans do wodza obozu.</summary>
        private static float AtWalls(Settlement st, MobileParty campLead, Army skip, out int n, out float outSum, out int nOut, out float outDist)
        {
            n = 0; nOut = 0; outSum = 0f; outDist = -1f;
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
                float d2 = p.GetPosition2D.DistanceSquared(v);
                float str = p.Army != null && p.Army.LeaderParty == p ? p.Army.EstimatedStrength : p.Party.EstimatedStrength;
                if (d2 > r2)
                {
                    if (d2 <= 9f * r2) { outSum += str; nOut++; float d = (float)Math.Sqrt(d2); if (outDist < 0f || d < outDist) outDist = d; }
                    continue;
                }
                sum += str;
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
                bool c1 = EndOn, c2 = StrikeOn, c3 = TimeOn;
                // recenzja R1-4: wypuszczenie zdejmowane, gdy oblezenia celu juz nie ma (osada moze byc oblegana znowu - armia wtedy oceniana od nowa)
                if (_released.Count > 0)
                {
                    List<Army> drop = null;
                    foreach (var kv in _released) if (kv.Value == null || kv.Value.SiegeEvent == null) (drop ?? (drop = new List<Army>())).Add(kv.Key);
                    if (drop != null) for (int i = 0; i < drop.Count; i++) _released.Remove(drop[i]);
                }
                if (!c1 && !c2 && !c3) return;
                bool lastHour = ((int)h % CampaignTime.HoursInDay) == CampaignTime.HoursInDay - 1;
                if (lastHour) _dSieges.Clear();
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
                        int n, nOut; float outSum, outDist;
                        float walls = AtWalls(st, lead, null, out n, out outSum, out nOut, out outDist);
                        // diagnostyka R3-3: miara gry obozu (MobilePartyAi.GetNearbyPartyDataWhileDefendingSettlement num3 - GetCustomStrength jako obronca, pole)
                        float game = 0f;
                        try { foreach (var pb in camp.GetInvolvedPartiesForEventType()) if (pb != null) game += pb.GetCustomStrength(TaleWorlds.Core.BattleSideEnum.Defender, TaleWorlds.CampaignSystem.MapEvents.MapEvent.PowerCalculationContext.PlainBattle); }
                        catch { game = -1f; }
                        _info[st] = new Info { Bes = bes, Walls = walls, N = n, Ev = ev, Lead = lead, Game = game, Out = outSum, NOut = nOut, OutDist = outDist };
                        if (walls < Edge * bes && walls + outSum >= Edge * bes && nOut > 0) _dOutAdvHours++;
                        if (lastHour && _dSieges.Count < 6)
                            _dSieges.Add(Nm(st) + " oboz " + F0(bes) + " (miara gry " + F0(game) + ", " + F(ev.SiegeStartTime.ElapsedDaysUntilNow) + " dob) pod murami " + F0(walls) + " (" + n + ")"
                                         + (nOut > 0 ? " poza promieniem " + F0(outSum) + " (" + nOut + ", najblizej " + F(outDist) + " jedn.)" : ""));
                        if (c2 && !lead.IsCurrentlyAtSea && !ev.IsBlockadeActive && walls >= Edge * bes)
                        {
                            _strike.Add(st);
                            if (_dStrikeSt.Add(st))
                                Log.Info(Tag + "atak pod murami | dzien " + Today + " | " + Nm(st) + " (" + st.StringId + ", " + Nm(st.MapFaction) + ") | odsiecz pod murami " + F0(walls) + " (druzyn " + n
                                         + ", w " + Campaign.Current.Models.MobilePartyAIModel.SettlementDefendingNearbyPartyCheckRadius.ToString("0", Inv) + " jedn. od wodza obozu) >= " + Edge.ToString("0.0", Inv)
                                         + " x oboz " + F0(bes) + " (miara gry obozu " + F0(game) + "; " + Nm(camp.MapFaction) + ", wodz " + Nm(lead) + ") - najsilniejsza prowadzi na oboz, reszta za nia.");
                        }
                    }
                    catch (Exception e) { Stumble("Hourly.siege", e); }
                }
                if ((!c1 && !c3) || _info.Count == 0) return;
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
            // po wczytaniu (bez zapisu Gather): oblegana twierdza krolestwa najblizsza MIEJSCU ZBIORKI (gra wybiera je przy osadzie odsieczy - waga odleglosci od celu
            // w Army.FindBestGatheringSettlementAndMoveTheLeader), najwyzej 2 x srednia odleglosc dwoch najblizszych miast gry; dalej - cel nieznany
            // (recenzja R1-7 / R3-9: armia zebrana dla innej osady nie jest wypuszczana do cudzego oblezenia)
            var gp = a.AiBehaviorObject as Settlement;
            if (gp == null) return null;
            Settlement best = null; float bd = float.MaxValue;
            var pos = gp.GetPosition2D;
            foreach (var kv in _info)
            {
                if (kv.Key.MapFaction != k) continue;
                float d = kv.Key.GetPosition2D.DistanceSquared(pos);
                if (d < bd) { bd = d; best = kv.Key; }
            }
            if (best == null) return null;
            float lim = 2f * Campaign.Current.GetAverageDistanceBetweenClosestTwoTownsWithNavigationType(lead.NavigationCapability);
            return lim > 0f && bd <= lim * lim ? best : null;
        }

        private static void Consider(Army a, Kingdom k)
        {
            if (a == null || _released.ContainsKey(a)) return;
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
            bool adv = EndOn && present + walls >= Edge * inf.Bes;
            // C3: zbiorka trwa co najmniej dobe od zalozenia armii (zegar gry w zapisie - dziala tez po wczytaniu)
            double hrs = -1.0;
            try { if (_armyCreated != null) { var ct = _armyCreated(a); if (ct != default(CampaignTime)) hrs = (CampaignTime.Now - ct).ToHours; } } catch { hrs = -1.0; }
            bool late = TimeOn && hrs >= GatherMaxHours;
            if (!adv && !late) { _dHeldHours++; return; }
            _released[a] = st;
            _dReleased++;
            if (!adv) _dReleasedTime++;
            if (_dRelNames.Count < 8) _dRelNames.Add(Nm(lead) + " -> " + Nm(st) + (adv ? "" : " (czas)"));
            Log.Info(Tag + "koniec zbiorki | dzien " + Today + " | armia " + Nm(lead) + " (" + Nm(k) + ", typ " + a.ArmyType + ") -> odsiecz " + Nm(st) + " (" + st.StringId + ")"
                     + " | powod " + (adv ? "przewaga" : "czas - zbiorka " + F(hrs) + " h >= " + GatherMaxHours.ToString("0", Inv) + " h (bez przewagi: pod mury, na oboz tylko z przewaga)")
                     + " | przy wodzu " + F0(present) + " (dolaczeni " + a.LeaderPartyAndAttachedPartiesCount + " druzyn, wezwani w promieniu " + near + "), pod murami " + F0(walls) + " (druzyn " + nw + ")"
                     + ", razem " + F0(present + walls) + (adv ? " >= " : " < ") + Edge.ToString("0.0", Inv) + " x oboz " + F0(inf.Bes) + " | wezwanych dalej " + far + " (doganiaja armie)"
                     + " | droga " + days.ToString("0.0", Inv) + " doby w linii prostej - nie czeka na 90% wezwanych.");
        }

        // ------------------------------------------------------------ linie
        internal static void SessionStart()
        {
            try
            {
                Log.Info(Tag + "start sesji | koniec zbiorki z przewaga " + (EndOn ? "TAK" : "NIE") + ", koniec zbiorki po dobie " + (TimeOn ? "TAK" : "NIE") + ", atak pod murami z przewaga " + (StrikeOn ? "TAK" : "NIE")
                         + " | odbicia: MobilePartyAi._mobileParty " + (_aiParty != null ? "TAK" : "BRAK") + ", Army._creationTime " + (_armyCreated != null ? "TAK" : "BRAK")
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
                    sb.Append(Tag).Append("dzien ").Append(Today).Append(" | koniec zbiorki ").Append(EndOn ? "TAK" : "NIE").Append(", po dobie ").Append(TimeOn ? "TAK" : "NIE").Append(", atak pod murami ").Append(StrikeOn ? "TAK" : "NIE")
                      .Append(" | zbiorki zakonczone dzis ").Append(_dReleased).Append(" (w tym po dobie bez przewagi ").Append(_dReleasedTime).Append(')').Append(_dRelNames.Count > 0 ? " [" + string.Join(", ", _dRelNames.ToArray()) + "]" : "")
                      .Append(", godzin armii odsieczy w zbiorce bez przewagi ").Append(_dHeldHours)
                      .Append(", armie po koncu zbiorki teraz ").Append(_released.Count)
                      .Append(" | osady z atakiem pod murami dzis ").Append(_dStrikeSt.Count).Append(", przelaczen druzyn na atak ").Append(_dFlips).Append(" (pominiete na morzu ").Append(_dSeaSkip).Append(')')
                      .Append(", godzin z przewaga tylko z odsiecza poza promieniem gry ").Append(_dOutAdvHours)
                      .Append(" | oblezenia teraz (bez Innych) ").Append(_info.Count).Append(_dSieges.Count > 0 ? ": " + string.Join("; ", _dSieges.ToArray()) : "")
                      .Append(" | potkniecia ").Append(_dStumbles).Append(" (sesja ").Append(_stumbles).Append(").");
                    Log.Info(sb.ToString());
                }
            }
            catch (Exception e) { Stumble("Daily", e); }
            ClearDay();
        }
    }
}
