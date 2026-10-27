using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// 274 - DZWIGNIE SLABSZEJ STRONY (W1, W2) I ZBIORKA OBU STRON. Tylko lordowie AI rodow z lennem (miasto albo zamek) - nigdy rod gracza, nigdy rody rycerskie.
    ///  W1a (postfiks 250 mysli AI): partia slabszej strony ponizej celu wypelnienia, poza armia, gotowa do bitwy (p9), nie glodna - najlepsza ocena gry "wizyta
    ///      w osadzie" (GoToSettlement) w osadzie strony z ochotnikami u notabli albo weteranami w karczmie podniesiona do 1.05 x najlepsza inna (OB5 Low po nas
    ///      dalej daje obronie oblezonej twierdzy co najmniej 1.1 x - obrona wygrywa). Werbunek przy wejsciu robi gra (OnBeforeSettlementEntered).
    ///  W1b (CampaignEvents.SettlementEntered, po werbunku gry): w miescie strony najem weteranow z karczmy prywatna metoda gry
    ///      RecruitmentCampaignBehavior.ApplyRecruitMercenary (odbiciem) - ksiega weteranow E4/10 (pula karczmy, kolejka FIFO), oplata do sakiewek najetych
    ///      E5/B6 i kon z polki 157 dzialaja bez zmian (latki na ApplyInternal). Liczba = min(miejsce do celu, ludzie w karczmie, (zloto pana - 5 000) / cena gry,
    ///      budzet zoldu partii / zold). Tylko rod ze zlotem G ponad rezerwa wojenna R (166).
    ///  W2 (doba): rod slabszej strony na swoim limicie druzyn gry, z wolnym doroslym lordem (wybor gry GetBestAvailableCommander), dostaje jedna druzyne wiecej -
    ///      prywatne SpawnLordParty + GiveInitialItemsToParty gry (odbiciem): 166 (miejsce w pulapie na 30 ludzi), 159 (tylko uzbrojeni), E4/8 (ludzie z zalogi)
    ///      dzialaja jak dla gry. Nigdy ponad limit gry przy najwyzszym tierze rodu (3), najwyzej 1 na rod na 7 dob; nic nie jest rozwiazywane przy wylaczeniu.
    ///  Zbiorka (obie strony, klucz CanonWarBalanceMuster): strona bez bitwy lordow ani oblezenia z druga przez 7 dob - wladca (wolny, z druzynami do wezwania)
    ///      dostaje "zbierz armie i oblegaj najlepszy cel frontu (nie siedziba)" z ocena 1.05 x najlepsza; lord dalej niz zasieg obrony gry (8.02 doby droga gry)
    ///      od najblizszej twierdzy frontu swojej strony - PatrolAroundPoint wokol niej z ocena 1.1 x max(najlepsza, 0.5) (jak OB5 dla zamku). Rody z twierdza
    ///      przy trzecim wrogu (R3-08g, 60) zostaja w domu.
    /// </summary>
    internal static partial class CanonWarBalance
    {
        // ================================================================ odbicia metod gry
        private static MethodInfo _miHire, _miSpawn, _miGifts, _miCommander;

        private static string ReflectHire()
        {
            try
            {
                _miHire = AccessTools.Method(typeof(RecruitmentCampaignBehavior), "ApplyRecruitMercenary", new[] { typeof(MobileParty), typeof(Settlement), typeof(CharacterObject), typeof(int) });
                return _miHire != null ? "TAK" : "BRAK (RecruitmentCampaignBehavior.ApplyRecruitMercenary)";
            }
            catch (Exception e) { Stumble("ReflectHire", e); return "BLAD (" + e.GetType().Name + ")"; }
        }

        private static string ReflectSpawn()
        {
            try
            {
                var t = typeof(HeroSpawnCampaignBehavior);
                _miSpawn = AccessTools.Method(t, "SpawnLordParty", new[] { typeof(Hero), typeof(bool) });
                _miGifts = AccessTools.Method(t, "GiveInitialItemsToParty", new[] { typeof(MobileParty) });
                _miCommander = AccessTools.Method(t, "GetBestAvailableCommander", new[] { typeof(Clan) });
                if (_miSpawn != null && _miGifts != null && _miCommander != null) return "TAK";
                return "BRAK (" + (_miSpawn == null ? "SpawnLordParty " : "") + (_miGifts == null ? "GiveInitialItemsToParty " : "") + (_miCommander == null ? "GetBestAvailableCommander" : "") + ")";
            }
            catch (Exception e) { Stumble("ReflectSpawn", e); return "BLAD (" + e.GetType().Name + ")"; }
        }

        /// <summary>Partia, ktorej dotycza dzwignie i zbiorka: druzyna lorda AI, rod z lennem, nie rod gracza, nie najemnik, nie rod rycerski.</summary>
        private static bool AiLanded(MobileParty mp, out Clan clan)
        {
            clan = mp != null ? mp.ActualClan : null;
            if (clan == null || clan == Clan.PlayerClan || clan.IsUnderMercenaryService || !Landed(clan)) return false;
            try { if (GentryService.IsKnightClan(clan)) return false; } catch { }
            return mp.LeaderHero != null && mp.LeaderHero.IsAlive;
        }

        private static float Fill(MobileParty mp)
        {
            int lim = mp.Party.PartySizeLimit;
            return lim > 0 ? (float)mp.Party.NumberOfAllMembers / lim : 1f;
        }

        // ================================================================ postfiks mysli AI (250): W1a i zbiorka
        /// <summary>Postfiks (priorytet 250) CampaignEventDispatcher.AiHourlyTick. Nie rzuca.</summary>
        public static void BoostPostfix(MobileParty party, PartyThinkParams partyThinkParams)
        {
            if (!_active || party == null || partyThinkParams == null) return;
            try
            {
                if (!party.IsLordParty || party.IsMainParty) return;
                int side = SideOf(party.MapFaction);
                if (side == 0) return;
                if (party.Army != null && party.Army.LeaderParty != party) return;
                if (partyThinkParams.DoNotChangeBehavior) return;
                if (party.MapEvent != null || party.BesiegedSettlement != null) return;
                if (party.CurrentSettlement != null && party.CurrentSettlement.SiegeEvent != null) return;
                if (party.Party.IsStarving) return;
                var scores = partyThinkParams.AIBehaviorScores;
                if (scores == null || scores.Count == 0) return;
                Clan clan;
                if (!AiLanded(party, out clan)) return;
                if (CanonWarClock.OverNow()) return;
                if (ArmsFirst.Unready(party)) return;
                if (_levers && side == _weak && party.Army == null && Fill(party) < _target)
                {
                    if (Recruit(party, partyThinkParams)) return;
                }
                // recenzja 274 (3): bezczynnosc krolestwa partii (Polnoc stoi, gdy walczy tylko Dorzecze), nie calej koalicji
                if (MusterOn && IdleK(party.MapFaction as Kingdom) && !_homeGuard.Contains(clan)) Muster(party, partyThinkParams, side, clan);
            }
            catch (Exception e) { Stumble("BoostPostfix", e); }
        }

        /// <summary>Osada ma rekrutow dla tego lorda: weterani (najemnik) w karczmie miasta albo ochotnik u notabla w zasiegu wskaznika gry.</summary>
        private static bool HasRecruits(Settlement st, Hero buyer)
        {
            try
            {
                if (st.IsTown)
                {
                    var rcb = Campaign.Current.GetCampaignBehavior<RecruitmentCampaignBehavior>();
                    var md = rcb != null ? rcb.GetMercenaryData(st.Town) : null;
                    if (md != null && md.HasAvailableMercenary(Occupation.Mercenary)) return true;
                }
                var vm = Campaign.Current.Models.VolunteerModel;
                foreach (var n in st.Notables)
                {
                    if (n == null || !n.IsAlive || n.VolunteerTypes == null) continue;
                    int max = vm.MaximumIndexHeroCanRecruitFromHero(buyer, n);
                    for (int i = 0; i < max && i < n.VolunteerTypes.Length; i++) if (n.VolunteerTypes[i] != null) return true;
                }
            }
            catch { }
            return false;
        }

        /// <summary>W1a: najlepsza wizyta (GoToSettlement) w osadzie strony z rekrutami -> 1.05 x najlepsza inna. true = podbite.</summary>
        private static bool Recruit(MobileParty party, PartyThinkParams p)
        {
            // bramki werbunku gry (RecruitmentCampaignBehavior.CheckRecruiting / RecruitVolunteersFromNotable) - bez nich gra w osadzie nikogo nie wezmie, a podbicie
            // trzymaloby partie w osadzie z ochotnikami, na ktorych jej nie stac
            var lead = party.LeaderHero;
            if (lead == null || lead.Clan == null || party.PartyTradeGold <= HeroHelper.StartRecruitingMoneyLimit(lead)) return false;
            if (lead != lead.Clan.Leader && lead.Clan.Gold <= HeroHelper.StartRecruitingMoneyLimitForClanLeader(lead)) return false;
            if (party.IsWageLimitExceeded() || party.GetAvailableWageBudget() <= 0) return false;
            var scores = p.AIBehaviorScores;
            int side = SideOf(party.MapFaction);
            int best = -1; float bestV = -1f, other = 0f;
            for (int i = 0; i < scores.Count; i++)
            {
                var e = scores[i];
                var st = e.Item1.Party as Settlement;
                bool cand = e.Item1.AiBehavior == AiBehavior.GoToSettlement && !e.Item1.WillGatherArmy && st != null && !st.IsUnderSiege && !st.IsRaided && !st.IsUnderRaid
                            && SideOf(st.MapFaction) == side && HasRecruits(st, party.LeaderHero);
                if (cand) { if (e.Item2 > bestV) { bestV = e.Item2; best = i; } }
                else if (e.Item2 > other) other = e.Item2;
            }
            if (best < 0) return false;
            float nv = Edge * Math.Max(other, 0.01f);
            if (bestV >= nv) return true;   // juz najlepsza - gra i tak tam idzie
            var d = scores[best].Item1;
            p.SetBehaviorScore(in d, nv);
            _dBoost++;
            return true;
        }

        // ---- zbiorka
        private sealed class Far { public int Hour; public Settlement Front; public bool IsFar; public MobileParty.NavigationType Nav; public bool Port; }
        private static readonly Dictionary<MobileParty, Far> _farCache = new Dictionary<MobileParty, Far>();
        private const int FarCacheHours = 6;   // zegar mysli AI gry (DefaultThinkingPeriodInHours = 6, OB5)

        private static void Muster(MobileParty party, PartyThinkParams p, int side, Clan clan)
        {
            var k = party.MapFaction as Kingdom;
            if (k == null) return;
            var scores = p.AIBehaviorScores;
            // wladca: zbierz armie na najlepszy cel frontu (nie siedziba)
            if (party.Army == null && party.LeaderHero != null && ReferenceEquals(party.LeaderHero, k.Leader))
            {
                int bi = -1; float bv = -1f, other = 0f; bool army = false;
                for (int i = 0; i < scores.Count; i++)
                {
                    var e = scores[i];
                    var st = e.Item1.Party as Settlement;
                    bool cand = e.Item1.AiBehavior == AiBehavior.BesiegeSettlement && st != null && e.Item2 > -1f && SideOf(st.MapFaction) != 0 && SideOf(st.MapFaction) != side
                                && !SeatHeld(st) && IsFrontTarget(k, st);   // recenzja 274 (1): wlasna siedziba w rekach wroga moze byc celem odbicia
                    if (cand)
                    {
                        // armia pierwsza, potem ocena
                        if (bi < 0 || (e.Item1.WillGatherArmy && !army) || (e.Item1.WillGatherArmy == army && e.Item2 > bv)) { bi = i; bv = e.Item2; army = e.Item1.WillGatherArmy; }
                    }
                    else if (e.Item2 > other) other = e.Item2;
                }
                if (bi >= 0)
                {
                    float nv = Edge * Math.Max(other, FloorBase);
                    var d = scores[bi].Item1;
                    var tgt = d.Party as Settlement;
                    if (army) { if (bv < nv) p.SetBehaviorScore(in d, nv); }
                    else
                    {
                        var members = p.PossibleArmyMembersUponArmyCreation;
                        if (members != null && members.Count > 0 && !clan.IsUnderMercenaryService)
                        {
                            var a = new AIBehaviorData(tgt, AiBehavior.BesiegeSettlement, d.NavigationType, true, d.IsFromPort, d.IsTargetingPort);
                            p.AddBehaviorScore((a, nv));
                        }
                        else if (bv < nv) p.SetBehaviorScore(in d, nv);   // bez druzyn do wezwania - oblezenie wlasna druzyna
                    }
                    _dMusterKing++;
                    _musterKingNote = "-> " + (army || (p.PossibleArmyMembersUponArmyCreation != null && p.PossibleArmyMembersUponArmyCreation.Count > 0) ? "armia na " : "oblezenie ") + Nm(tgt);
                    if (Say("zbiorka|k|" + k.StringId))
                        Log.Info(Tag + "zbiorka | dzien " + Today + " | " + Nm(k) + " bez walki " + IdleOfK(k, Today) + " dob (strona " + SideName(side) + " " + IdleOf(side, Today) + " dob): wladca " + Nm(party.LeaderHero)
                             + " (" + Nm(k) + ") " + _musterKingNote + " (ocena " + F2(nv) + ", najlepsza inna " + F2(other) + ").");
                    return;
                }
            }
            if (party.Army != null) return;   // wodz armii - jego cele daje gra i SCA (weta i tak stoja)
            int now = (int)Math.Floor(CampaignTime.Now.ToHours);
            Far f;
            if (!_farCache.TryGetValue(party, out f) || now - f.Hour >= FarCacheHours)
            {
                f = new Far { Hour = now };
                var list = _frontOwn[side];
                Settlement near = null; float nd = float.MaxValue;
                var pos = party.GetPosition2D;
                for (int i = 0; i < list.Count; i++) { float dd = pos.DistanceSquared(list[i].GetPosition2D); if (dd < nd) { nd = dd; near = list[i]; } }
                f.Front = near;
                if (near != null)
                {
                    MobileParty.NavigationType nav; float dist; bool port;
                    AiHelper.GetBestNavigationTypeAndAdjustedDistanceOfSettlementForMobileParty(party, near, false, out nav, out dist, out port);
                    f.Nav = nav; f.Port = port;
                    float speed = nav == MobileParty.NavigationType.Naval ? Campaign.Current.EstimatedAverageLordPartyNavalSpeed : Campaign.Current.EstimatedAverageLordPartySpeed;
                    if (nav == MobileParty.NavigationType.All) speed = (Campaign.Current.EstimatedAverageLordPartyNavalSpeed + Campaign.Current.EstimatedAverageLordPartySpeed) * 0.5f;
                    f.IsFar = nav != MobileParty.NavigationType.None && speed > 0f && dist / (speed * CampaignTime.HoursInDay) > DefendRangeDays;
                }
                _farCache[party] = f;
            }
            if (f.Front == null || !f.IsFar) return;
            float best = 0f;
            for (int i = 0; i < scores.Count; i++) if (scores[i].Item2 > best) best = scores[i].Item2;
            float v = FrontPull * Math.Max(best, FloorBase);
            var pa = new AIBehaviorData(f.Front, AiBehavior.PatrolAroundPoint, f.Nav, false, f.Port, false);
            p.AddBehaviorScore((pa, v));
            _dMusterLords++;
            if (Say("zbiorka|l|" + party.StringId))
                Log.Info(Tag + "zbiorka | dzien " + Today + " | " + Nm(k) + " bez walki " + IdleOfK(k, Today) + " dob (strona " + SideName(side) + " " + IdleOf(side, Today) + " dob): " + Nm(party) + " (" + Nm(clan)
                     + ", " + Nm(party.MapFaction) + ") idzie na front - patrol wokol " + Nm(f.Front) + " (dalej niz " + DefendRangeDays.ToString("0.00", Inv) + " doby; ocena " + F2(v) + ").");
        }

        /// <summary>Linia zdarzenia raz na klucz i dobe (zbiorka, weto, najem, nowa druzyna). Recenzja 274 (M4): pytanie PRZED skladaniem tekstu.</summary>
        private static bool Say(string key)
        {
            var s = S;
            return s != null && s.LogEnabled && _dSaid.Add(key);
        }

        // ================================================================ W1b: najem z karczmy przy wejsciu do miasta strony
        /// <summary>CampaignEvents.SettlementEntered (po OnBeforeSettlementEntered gry - jej werbunek juz byl). Nie rzuca.</summary>
        internal static void OnSettlementEntered(MobileParty mp, Settlement st, Hero hero)
        {
            if (!_active || !_levers || _weak == 0 || mp == null || st == null) return;
            try
            {
                if (!st.IsTown || st.Town == null || !mp.IsLordParty || mp.IsMainParty || mp.MapEvent != null || st.IsUnderSiege) return;
                int side = SideOf(mp.MapFaction);
                if (side != _weak || SideOf(st.MapFaction) != side) return;
                Clan clan;
                if (!AiLanded(mp, out clan) || CanonWarClock.OverNow()) return;
                Hire(mp, st, clan);
            }
            catch (Exception e) { Stumble("OnSettlementEntered", e); }
        }

        private static void Hire(MobileParty mp, Settlement st, Clan clan)
        {
            if (_miHire == null) return;
            int lim = mp.Party.PartySizeLimit, men0 = mp.Party.NumberOfAllMembers;
            int room = (int)Math.Floor(_target * lim) - men0;
            if (room <= 0) return;
            var rcb = Campaign.Current.GetCampaignBehavior<RecruitmentCampaignBehavior>();
            var md = rcb != null ? rcb.GetMercenaryData(st.Town) : null;
            if (md == null || !md.HasAvailableMercenary(Occupation.Mercenary)) return;
            var troop = md.TroopType;
            var lead = mp.LeaderHero;
            double g, r;
            if (!ClanBudget.Reserve(clan, out g, out r) || g <= r) return;   // rod bez budzetu 166 albo ponizej rezerwy wojennej R
            int price = Campaign.Current.Models.PartyWageModel.GetTroopRecruitmentCost(troop, lead).RoundedResultNumber;
            int wage = Math.Max(1, Campaign.Current.Models.PartyWageModel.GetCharacterWage(troop));
            int budget = mp.GetAvailableWageBudget();
            if (price <= 0) return;
            int byGold = (lead.Gold - FamilyFloor) / price;
            int byReserve = (int)Math.Min(int.MaxValue, Math.Floor((g - r) / price));   // recenzja 274 (M5): wydatek rodu najwyzej G - R (tylko zloto ponad rezerwa wojenna 166)
            int n = Math.Min(Math.Min(room, md.Number), Math.Min(Math.Min(byGold, byReserve), budget / wage));
            if (n <= 0) return;
            int tav0 = md.Number, gold0 = lead.Gold;
            _miHire.Invoke(rcb, new object[] { mp, st, troop, n });
            int got = mp.Party.NumberOfAllMembers - men0, paid = gold0 - lead.Gold;
            if (got <= 0) return;
            _hired += got; _dHireN++; _dHireMen += got; _goldHired += paid; _dHireGold += paid;
            if (Say("najem|" + mp.StringId))
                Log.Info(Tag + "najem z karczmy | dzien " + Today + " | " + Nm(mp) + " (" + Nm(clan) + ", " + Nm(mp.MapFaction) + ") w " + Nm(st) + ": " + got + " x " + troop.StringId
                     + " za " + paid + " zl (cena " + price + ", zold " + wage + ") | ludzi " + men0 + " -> " + mp.Party.NumberOfAllMembers + " z limitu " + lim + ", cel " + F2(_target)
                     + " | zloto pana " + gold0 + " -> " + lead.Gold + ", rod " + F0(g) + " ponad rezerwa " + F0(r) + ", budzet zoldu " + budget + " | karczma " + tav0 + " -> " + md.Number + ".");
        }

        // ================================================================ W2: nowa druzyna rodu na limicie (doba)
        private static void SpawnDaily(int side, int today)
        {
            if (_miSpawn == null || _miGifts == null || _miCommander == null) return;
            var beh = Campaign.Current.GetCampaignBehavior<HeroSpawnCampaignBehavior>();
            if (beh == null) return;
            var tier = Campaign.Current.Models.ClanTierModel;
            var clans = new List<Clan>();
            foreach (var kv in _side) if (kv.Value == side) foreach (var c in kv.Key.Clans) clans.Add(c);
            foreach (var c in clans)
            {
                try
                {
                    if (c == null || c.IsEliminated || c == Clan.PlayerClan || c.IsUnderMercenaryService || !Landed(c) || c.WarPartyComponents == null) continue;
                    try { if (GentryService.IsKnightClan(c)) continue; } catch { }
                    int last;
                    if (c.StringId != null && _clanSpawnDay.TryGetValue(c.StringId, out last) && today - last < SpawnEveryDays) continue;
                    int cnt = c.WarPartyComponents.Count;
                    int lim = tier.GetPartyLimitForTier(c, c.Tier);
                    if (cnt < lim) continue;   // ponizej limitu - gra wystawia sama (ConsiderSpawningLordParties)
                    int cap = Math.Min(lim + 1, tier.GetPartyLimitForTier(c, tier.MaxClanTier));
                    if (cnt >= cap) continue;
                    var hero = _miCommander.Invoke(beh, new object[] { c }) as Hero;
                    if (hero == null || !hero.CanLeadParty()) continue;
                    var mp = _miSpawn.Invoke(beh, new object[] { hero, false }) as MobileParty;
                    if (mp == null) { _dSpawnBlock++; continue; }   // 166 SpawnPrefix (miejsce w pulapie) albo 179
                    _miGifts.Invoke(beh, new object[] { mp });
                    _clanSpawnDay[c.StringId] = today;
                    _spawned++;
                    _dSpawnNames.Add(Nm(c));
                    if (Say("druzyna|" + c.StringId))
                        Log.Info(Tag + "nowa druzyna | dzien " + today + " | " + Nm(c) + " (" + Nm(c.Kingdom) + ", strona " + SideName(side) + "): " + Nm(hero)
                             + " prowadzi " + Nm(mp) + " - ludzi " + mp.MemberRoster.TotalManCount + " z limitu " + mp.Party.PartySizeLimit + " | druzyn rodu " + cnt + " -> " + c.WarPartyComponents.Count
                             + " (limit gry " + lim + ", najwyzej " + cap + ") | zloto rodu " + c.Gold + ".");
                }
                catch (Exception e) { Stumble("SpawnDaily", e); }
            }
        }
    }
}
