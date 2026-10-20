using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>
    /// r3-jazda (PLAN 6.14, "wiecej jazdy u AI"; klucz GarrisonHorsesToLords) - WOLNE KONIE ZALOGI POD JEZDZCOW LORDA.
    /// Dowod (WGRANIE 90, bieg 160 dob z zapisu, linie "Konie AI (175)" i "Jezdzcy i uprzaz (E8/H)" d109236): konni bez konia 3743 z 16568, a jednoczesnie
    /// wolne konie w zbrojowniach 2975 - w ZALOGACH (zbrojownie zalog: konie 5610 przy 3501 jezdzcach wedlug typu, z koniem 2703), podczas gdy w partiach rodow AI
    /// "bez konia" 2066. Rachunek koni (Stables.Balance) jest per partia, wiec kon zalogi nigdy nie trafial pod jezdzca lorda: zalogi nie sprzedaja koni
    /// (MenPurse.SellArmorySurplus - galaz koni tylko partii lordow), a lord zabierajacy jezdzcow z zalogi zostawia tam ich konie (Stajnia przenosi tylko ludzi).
    /// Teraz przy postoju lorda rodu AI w miescie albo zamku swojego krolestwa (nie oblezonym), po dosadzeniu z taboru (Stables.Remount):
    ///  (1) jezdzcy bez konia dostaja do zbrojowni DTE wolne konie zbrojowni zalogi - ta sama kolejnosc kategorii co Remount (z prawem do bojowego: bojowy,
    ///      szlachetny, zwykly; reszta: zwykly, bojowy, szlachetny; w kategorii najtansze);
    ///  (2) przy zapasie wedlug kategorii (AiSellsSpareHorses, E6/K2) gotowi do awansu na jezdnego dostaja do taboru konie swojej kategorii - tylko brak
    ///      (Reserve.Short - Have, bez luzu), najwyzej AiMountMaxPerVisit na postoj; awans placi tym koniem jak kazdym z taboru (PayInHorses).
    /// Wolne konie zalogi = po jej jezdzcach i jezdzcach jej patroli BK (ich konie leza w twierdzy) minus luz zalogi: max(AiMountSpareBuffer,
    /// AiSpareHorseShare x jej jezdzcy), luz najpierw ze zwyklych. Zaloga z jezdzcem bez konia nie ma wolnych (Balance), wiec nic nie oddaje.
    /// ZAMKNIETA EKONOMIA: twierdza wlasnego rodu - kon zostaje we wlasnosci rodu (przesuniecie bez zlota); twierdza innego rodu tego samego krolestwa - pan
    /// partii placi glowie rodu pana twierdzy cene tego konia na targu (Stables.MarketPrice; bez targu wartosc sztuki) GiveGoldAction miedzy bohaterami,
    /// z tego samego budzetu co zwykly zakup koni (F5: przydzial sprzetu rodu, najwyzej AiMountGearPurseShare kiesy ponad AiGearGoldReserve; rod bez
    /// przydzialu i najemnicy - AiMountPurseShare kiesy) i schodzi z przydzialu (ClanBudget.GearSpent). Targ NIE jest dotykany (polki, AiMountMarketSharePercent,
    /// AiMountShelfFloor bez zmian) - mniej brakow po tym kroku to mniej zakupow z polek tego samego postoju (zakup liczy brak po nim).
    /// Twierdze gracza i rod gracza - bez zmian. Bez pompy: zalogi koni nie sprzedaja, a lord sprzedaje dopiero ponad 1.5 x zapas (krok (2) daje najwyzej brak).
    /// Stan: tylko liczniki doby (bez zapisu). Watek: nasluch postoju (watek gry), bez wspoldzielenia. Potkniecia liczone per postoj, nic sie nie gasi.
    /// </summary>
    internal static class GarrisonHorses
    {
        private static int _dStamp = -1, _dStops, _dStopsFree, _dFree, _dLackBefore, _dLackAfter, _dToRiders, _dToQueue, _dOwn, _dPaidN, _dGold, _dNoBudget, _dRefused;
        private static readonly int[] _dCat = new int[3];
        private static int _stumbles, _stumblesAll;
        private static readonly int[] HighOrder = { 1, 2, 0 }, LowOrder = { 0, 1, 2 }, QueueOrder = { 1, 0, 2 };

        internal static bool On { get { var s = Settings.Current; return s != null && s.GarrisonHorsesToLords && GarrisonKit.OwnKitOn; } }

        /// <summary>Nowa kampania / wczytanie (Stables.Reset - StateHygiene): liczniki od zera, bez linii z poprzedniej kampanii.</summary>
        internal static void Reset()
        {
            ClearDay(); _dStamp = -1; _stumblesAll = 0;
        }

        private static void ClearDay()
        {
            _dStops = _dStopsFree = _dFree = _dLackBefore = _dLackAfter = _dToRiders = _dToQueue = _dOwn = _dPaidN = _dGold = _dNoBudget = _dRefused = 0;
            _dCat[0] = _dCat[1] = _dCat[2] = 0; _stumbles = 0;
        }

        private static void Flush()
        {
            int d = (int)CampaignTime.Now.ToDays;
            if (d == _dStamp) return;
            if (_dStamp >= 0 && _dStops + _stumbles > 0)
            {
                try
                {
                    Log.Info("Konie zalog dla lordow (r3-jazda): dzien " + _dStamp + " | " + (On ? "CZYNNE" : "WYLACZONE")
                             + " | postoje lordow rodow AI w twierdzach swojego krolestwa " + _dStops + " (z wolnymi konmi zalogi ponad jej luz " + _dStopsFree
                             + ", tych koni razem " + _dFree + ") | jezdzcy bez konia przy tych postojach: przed " + _dLackBefore + ", po " + _dLackAfter
                             + " (dostali konia z zalogi " + _dToRiders + ") | do kolejki awansow na jezdnego (tabor) " + _dToQueue
                             + " | razem wedlug kategorii (zwykly/bojowy/szlachetny) " + _dCat[0] + "/" + _dCat[1] + "/" + _dCat[2]
                             + " | z zalog wlasnego rodu " + _dOwn + ", od innych rodow krolestwa " + _dPaidN + " za " + _dGold + " zl (glowom rodow panow twierdz; bez budzetu "
                             + _dNoBudget + " postojow) | z targu 0 (polki nietkniete) | DTE nie przyjal " + _dRefused + " | potkniecia dzis " + _stumbles + " (razem " + _stumblesAll + ").");
                }
                catch { }
            }
            ClearDay();
            _dStamp = d;
        }

        /// <summary>Z Stables.OnSettlementEntered, zaraz po Remount (lack - jezdzcy bez konia po dosadzeniu z taboru). Zwraca brak po tym kroku.</summary>
        internal static int Give(MobileParty party, Settlement st, int lack)
        {
            if (!On || party == null || st == null) return lack;
            try
            {
                if (!CavalryTemplate.Steered(party) || party.MapEvent != null || Undead.Party(party)) return lack;
                if (!(st.IsTown || st.IsCastle) || st.Town == null || st.IsUnderSiege) return lack;
                var gar = st.Town.GarrisonParty;
                var owner = st.OwnerClan;
                var clan = CavalryTemplate.ClanOf(party);
                if (gar == null || gar == party || owner == null || clan == null || owner == Clan.PlayerClan) return lack;
                bool own = owner == clan;
                if (!own && (clan.Kingdom == null || owner.Kingdom != clan.Kingdom)) return lack;
                var c = Settings.Current;
                if (lack < 0) lack = 0;
                bool queueOn = c.CavalryNeedsMounts && SpareHorses.On;
                if (lack <= 0 && !queueOn) return lack;
                var all = AiGear.Armories();
                Dictionary<ItemObject, int> garArm;
                if (all == null || !all.TryGetValue(gar.Id, out garArm) || garArm == null || garArm.Count == 0) return lack;
                Flush();
                _dStops++;
                _dLackBefore += lack;

                // wolne konie zalogi: po jej jezdzcach i jezdzcach jej patroli BK, minus luz zalogi (najpierw ze zwyklych)
                var gb = Stables.Balance(gar, GarrisonArmory.PatrolRiders(st));
                int spare = Math.Max(Math.Max(0, c.AiMountSpareBuffer), (int)Math.Ceiling(MBMath.ClampFloat(c.AiSpareHorseShare, 0f, 1f) * gb.Riders - 1e-4));
                var avail = new int[3];
                for (int k = 0; k < 3; k++) { int t = Math.Min(spare, gb.Free[k]); avail[k] = gb.Free[k] - t; spare -= t; }
                int freeAll = avail[0] + avail[1] + avail[2];
                if (freeAll <= 0) { _dLackAfter += lack; return lack; }
                _dStopsFree++; _dFree += freeAll;

                // inny rod krolestwa - kupno za zloto, budzet jak zwykly zakup koni (F5)
                var lord = party.LeaderHero;
                Hero payee = null;
                int budget = int.MaxValue;
                bool gearRule = false;
                if (!own)
                {
                    payee = owner.Leader;
                    if (payee == null || !payee.IsAlive || payee == lord) { _dLackAfter += lack; return lack; }
                    int gearLeft = 0;
                    gearRule = c.AiMountGearPurseShare > 0f && !clan.IsUnderMercenaryService && ClanBudget.GearCap(clan, out gearLeft);
                    int purse = Math.Max(0, lord.Gold);
                    if (gearRule)
                        budget = Math.Min(gearLeft, (int)(Math.Max(0, purse - Math.Max(0, c.AiGearGoldReserve)) * MBMath.ClampFloat(c.AiMountGearPurseShare, 0f, 1f)));
                    else
                        budget = (int)(purse * MBMath.ClampFloat(c.AiMountPurseShare, 0f, 1f));
                    if (budget < 50) { _dNoBudget++; _dLackAfter += lack; return lack; }
                }

                int gold = 0, movedArm = 0, movedBag = 0;
                // (1) jezdzcy bez konia - do zbrojowni DTE partii
                if (lack > 0)
                {
                    int needHigh = Math.Min(lack, Math.Max(0, Stables.Balance(party, null, true).UnhorsedHigh));
                    int guard = 0;
                    while (lack > 0 && guard++ < 12)
                    {
                        bool high = needHigh > 0;
                        int k = Pick(avail, high ? HighOrder : LowOrder);
                        if (k < 0) break;
                        int want = Math.Min(avail[k], high ? needHigh : lack - needHigh);
                        if (want <= 0) break;
                        int got = Move(party, st, garArm, k, want, true, own, ref budget, ref gold);
                        if (got <= 0) { avail[k] = 0; continue; }
                        avail[k] -= got; lack -= got; movedArm += got;
                        if (high) needHigh -= got;
                    }
                }
                _dLackAfter += lack;
                _dToRiders += movedArm;

                // (2) gotowi do awansu na jezdnego - konie swojej kategorii do taboru (tylko brak, bez luzu)
                if (queueOn && avail[0] + avail[1] + avail[2] > 0 && budget >= 50)
                {
                    var rsv = Stables.Reserve(party);
                    int cap = Math.Max(1, c.AiMountMaxPerVisit);
                    foreach (int k in QueueOrder)
                    {
                        if (cap <= 0) break;
                        int want = Math.Min(Math.Min(avail[k], cap), Math.Max(0, rsv.Short[k] - rsv.Have[k]));
                        if (want <= 0) continue;
                        int got = Move(party, st, garArm, k, want, false, own, ref budget, ref gold);
                        if (got <= 0) continue;
                        avail[k] -= got; cap -= got; movedBag += got;
                    }
                }
                _dToQueue += movedBag;

                int moved = movedArm + movedBag;
                if (moved > 0)
                {
                    if (own) _dOwn += moved;
                    else
                    {
                        _dPaidN += moved;
                        if (gold > 0)
                        {
                            TaleWorlds.CampaignSystem.Actions.GiveGoldAction.ApplyBetweenCharacters(lord, payee, gold, true);
                            _dGold += gold;
                            if (gearRule) ClanBudget.GearSpent(clan, gold);
                        }
                    }
                    HorseState.Refresh(party);
                    HorseState.Refresh(gar);
                }
                return lack;
            }
            catch (Exception e)
            {
                _stumbles++; _stumblesAll++;
                if (_stumbles == 1) Log.Error("GarrisonHorses.Give", e);
                return lack;
            }
        }

        private static int Pick(int[] avail, int[] order)
        {
            foreach (int k in order) if (avail[k] > 0) return k;
            return -1;
        }

        /// <summary>Przenosi do n wierzchowcow kategorii k (najtansze najpierw) ze zbrojowni zalogi: toArmory - do zbrojowni DTE partii (odmowa DTE - kon
        /// wraca do zalogi), inaczej do taboru. Inny rod - kazda sztuka po cenie targu z budzetu (budget, gold). Zwraca, ile przeszlo.</summary>
        private static int Move(MobileParty party, Settlement st, Dictionary<ItemObject, int> garArm, int k, int n, bool toArmory, bool own, ref int budget, ref int gold)
        {
            int done = 0;
            while (done < n)
            {
                ItemObject best = null; int bestCnt = 0;
                foreach (var kv in garArm)
                {
                    var it = kv.Key;
                    if (it == null || kv.Value <= 0 || !Stables.IsPlainMount(it) || Stables.Level(it.ItemCategory) != k) continue;
                    if (best == null || it.Value < best.Value) { best = it; bestCnt = kv.Value; }
                }
                if (best == null) break;
                int take = Math.Min(n - done, bestCnt);
                int price = 0;
                if (!own)
                {
                    price = Stables.MarketPrice(st, new EquipmentElement(best));
                    if (price <= 0) price = Math.Max(1, best.Value);
                    int afford = budget / price;
                    if (afford <= 0) break;
                    take = Math.Min(take, afford);
                }
                if (take <= 0) break;
                if (bestCnt - take > 0) garArm[best] = bestCnt - take; else garArm.Remove(best);
                if (toArmory)
                {
                    if (!AiGear.AddToArmory(party, best, take))
                    {
                        int back; garArm.TryGetValue(best, out back); garArm[best] = back + take;   // DTE nie przyjal - kon zostaje w zalodze
                        _dRefused += take;
                        break;
                    }
                }
                else party.ItemRoster.AddToCounts(new EquipmentElement(best), take);
                if (!own) { budget -= price * take; gold += price * take; }
                done += take; _dCat[k] += take;
            }
            return done;
        }
    }
}
