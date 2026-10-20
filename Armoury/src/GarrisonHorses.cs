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
    /// (MenPurse.SellArmorySurplus - galaz koni tylko partii lordow) ani nie kupuja, a ani Remount, ani Stajnia nie siegaja do zbrojowni zalogi. Konie zostaja
    /// w zalogach po poleglych jezdzcach zalog, po dorobku startowym (ColdStart) i po przeniesieniach A7 (MoveKits przenosi konie z ludzmi, ale pula koni
    /// dawcy jest dzielona proporcjonalnie - PlanKits share - wiec nie wyrownuje kont koni miedzy partiami).
    /// MIARA (recenzja r3-jazda): krok (1) daje konia jezdzcom, ktorzy SA konni wedlug typu - podnosi jazde rzeczywista (E8/H "z koniem"), a nie linie
    /// "Rycerze i jazda (219)" / SZ-A8-1 (te licza konnych wedlug typu, ch.IsMounted). Na linie 219 dziala tylko krok (2) (kolejka awansow) i posrednio
    /// zakup Stajni tego postoju (Reserve.Short zawiera jezdzcow bez konia - mniej braku, wiecej budzetu i limitu postoju na konie dla awansow).
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
        private static int _dStamp = -1, _dStops, _dStopsFree, _dFree, _dLackBefore, _dLackAfter, _dToRiders, _dToQueue, _dOwn, _dPaidN, _dGold, _dNoBudget, _dRefused, _dHStops, _dHNeed, _dHarness, _dHOwn, _dHGold, _dHNoBudget, _dHRefused;
        private static readonly int[] _dCat = new int[3];
        private static int _stumbles, _stumblesAll;
        private static readonly int[] HighOrder = { 1, 2, 0 }, LowOrder = { 0, 1, 2 }, QueueOrder = { 1, 0, 2 };
        // recenzja r3-jazda (miara): spis partii rodow AI z linii E8/H (jazda wedlug typu i z koniem) i polki koni z linii E6/0 - pierwszy w biegu i ostatni
        // (tylko odczyt cudzych spisow doby, bez wlasnej petli po partiach); tablice polek to swieze tablice tamtej linii (bez kopii)
        private static long _cavMen0 = -1, _cavType0, _cavHorse0, _cavMen = -1, _cavType, _cavHorse;
        private static int _cavDay0 = -1, _cavDay = -1, _shelfDay0 = -1, _shelfDay = -1;
        private static long[] _shelf0, _shelf;

        internal static bool On { get { var s = Settings.Current; return s != null && s.GarrisonHorsesToLords && GarrisonKit.OwnKitOn; } }

        /// <summary>Nowa kampania / wczytanie (Stables.Reset - StateHygiene): liczniki od zera, bez linii z poprzedniej kampanii.</summary>
        internal static void Reset()
        {
            ClearDay(); _dStamp = -1; _stumblesAll = 0;
            _cavMen0 = _cavMen = -1; _cavType0 = _cavHorse0 = _cavType = _cavHorse = 0; _cavDay0 = _cavDay = _shelfDay0 = _shelfDay = -1; _shelf0 = _shelf = null;
        }

        /// <summary>Z SaddleLaw.Daily (spis E8/H, partie rodow AI): ludzie, jazda wedlug typu, z koniem - do linii "Konie zalog dla lordow" (przed/po biegu).</summary>
        internal static void NoteCavalry(long men, long typeMounted, long withHorse)
        {
            if (men <= 0) return;
            int d = (int)CampaignTime.Now.ToDays;
            if (_cavMen0 < 0) { _cavMen0 = men; _cavType0 = typeMounted; _cavHorse0 = withHorse; _cavDay0 = d; }
            _cavMen = men; _cavType = typeMounted; _cavHorse = withHorse; _cavDay = d;
        }

        /// <summary>Z ArmsBook (linia "Konie (E6/0)"): polki miast i zamkow wedlug kategorii ksiegi (swieza tablica tej doby) - do linii (targi nie pustoszeja).</summary>
        internal static void NoteShelf(long[] shelf)
        {
            if (shelf == null || shelf.Length < 3) return;
            int d = (int)CampaignTime.Now.ToDays;
            if (_shelf0 == null) { _shelf0 = shelf; _shelfDay0 = d; }
            _shelf = shelf; _shelfDay = d;
        }

        private static string Pct(long a, long b) { return b > 0 ? (100.0 * a / b).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) : "0.00"; }

        private static string CavPart()
        {
            if (_cavMen < 0) return " | jazda partii rodow AI: brak spisu E8/H";
            return " | jazda partii rodow AI (spis E8/H doby " + _cavDay + "): wedlug typu " + Pct(_cavType, _cavMen) + "% (to licza linia 219 i SZ-A8-1), z koniem "
                   + Pct(_cavHorse, _cavMen) + "% (" + Pct(_cavHorse, _cavType) + "% jazdy wedlug typu ma konia - na to dziala krok (1)); na starcie biegu (doba " + _cavDay0
                   + "): wedlug typu " + Pct(_cavType0, _cavMen0) + "%, z koniem " + Pct(_cavHorse0, _cavMen0) + "% (" + Pct(_cavHorse0, _cavType0) + "%)";
        }

        private static string ShelfPart()
        {
            if (_shelf == null) return " | polki koni: brak linii E6/0";
            return " | polki koni miast i zamkow (E6/0, zwykle/bojowe/szlachetne): doba " + _shelfDay + " " + _shelf[0] + "/" + _shelf[1] + "/" + _shelf[2]
                   + " (razem " + (_shelf[0] + _shelf[1] + _shelf[2]) + "), na starcie biegu (doba " + _shelfDay0 + ") " + _shelf0[0] + "/" + _shelf0[1] + "/" + _shelf0[2]
                   + " (razem " + (_shelf0[0] + _shelf0[1] + _shelf0[2]) + ")";
        }

        private static void ClearDay()
        {
            _dStops = _dStopsFree = _dFree = _dLackBefore = _dLackAfter = _dToRiders = _dToQueue = _dOwn = _dPaidN = _dGold = _dNoBudget = _dRefused = _dHStops = _dHNeed = _dHarness = _dHOwn = _dHGold = _dHNoBudget = _dHRefused = 0;
            _dCat[0] = _dCat[1] = _dCat[2] = 0; _stumbles = 0;
        }

        private static void Flush()
        {
            int d = (int)CampaignTime.Now.ToDays;
            if (d == _dStamp) return;
            if (_dStamp >= 0 && _dStops + _dHStops + _stumbles > 0)
            {
                try
                {
                    Log.Info("Konie zalog dla lordow (r3-jazda): dzien " + _dStamp + " | " + (On ? "CZYNNE" : "WYLACZONE")
                             + " | postoje lordow rodow AI w twierdzach swojego krolestwa " + _dStops + " (z wolnymi konmi zalogi ponad jej luz " + _dStopsFree
                             + ", tych koni razem " + _dFree + ") | jezdzcy bez konia przy tych postojach: przed " + _dLackBefore + ", po " + _dLackAfter
                             + " (dostali konia z zalogi " + _dToRiders + ") | do kolejki awansow na jezdnego (tabor) " + _dToQueue
                             + " | razem wedlug kategorii (zwykly/bojowy/szlachetny) " + _dCat[0] + "/" + _dCat[1] + "/" + _dCat[2]
                             + " | z zalog wlasnego rodu " + _dOwn + ", od innych rodow krolestwa " + _dPaidN + " za " + _dGold + " zl (glowom rodow panow twierdz; bez budzetu "
                             + _dNoBudget + " postojow)"
                             + " | uprzaz zalog (GarrisonHarnessToLords " + (Settings.Current != null && Settings.Current.GarrisonHarnessToLords ? "tak" : "nie") + "): postoje z brakiem siodel "
                             + _dHStops + " (brak przed " + _dHNeed + "), wzieli " + _dHarness + " szt. (z zalog wlasnego rodu " + _dHOwn + ", od innych rodow za " + _dHGold + " zl; bez budzetu "
                             + _dHNoBudget + " postojow; DTE nie przyjal " + _dHRefused + ")"
                             + " | z targu 0 (polki nietkniete) | DTE nie przyjal koni " + _dRefused + CavPart() + ShelfPart()
                             + " | potkniecia dzis " + _stumbles + " (razem " + _stumblesAll + ").");
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
                try
                {
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
                        int got = Move(party, gar, st, garArm, k, want, true, own, ref budget, ref gold);
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
                        int got = Move(party, gar, st, garArm, k, want, false, own, ref budget, ref gold);
                        if (got <= 0) continue;
                        avail[k] -= got; cap -= got; movedBag += got;
                    }
                }
                }
                finally
                {
                    // zaplata za to, co naprawde przeszlo - takze gdy przenoszenie przerwal wyjatek (zamknieta ekonomia: kon od innego rodu zawsze oplacony)
                    if (!own && gold > 0 && Pay(lord, payee, clan, gearRule, gold)) _dGold += gold;
                }
                _dToQueue += movedBag;

                int moved = movedArm + movedBag;
                if (moved > 0)
                {
                    if (own) _dOwn += moved; else _dPaidN += moved;
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

        /// <summary>
        /// r3-jazda (3), klucz GarrisonHarnessToLords - UPRZAZ ZALOGI POD JEZDZCOW LORDA (brama E8/H "jezdziec = kon + uprzaz"). Z SaddleLaw.OnSettlementEntered,
        /// po uprzezy z taboru, przed zakupami siodel (AiGear): lord rodu AI w twierdzy swojego krolestwa bierze uprzaz rodziny konia ze zbrojowni zalogi ponad
        /// jej jezdzcow z koniem (SaddleLaw.Pairs z jezdzcami patroli BK) i jej luz, najwyzej brak do zapasu siodel (SaddleLaw.BuyNeed - ta sama miara co zakup).
        /// Te same warunki, kolejnosc (najtansze najpierw) i rozliczenie co konie (wlasny rod - bez zlota; inny rod - cena targu glowie rodu pana twierdzy z budzetu F5).
        /// </summary>
        internal static void GiveHarness(MobileParty party, Settlement st)
        {
            var c = Settings.Current;
            // recenzja r3-jazda: wlasny klucz (bez klucza koni GarrisonHorsesToLords) + wojsko tylko z wlasnym sprzetem, jak konie
            if (c == null || !c.GarrisonHarnessToLords || !GarrisonKit.OwnKitOn || !SaddleLaw.On || party == null || st == null) return;
            try
            {
                if (!CavalryTemplate.Steered(party) || party.MapEvent != null || Undead.Party(party)) return;
                if (!(st.IsTown || st.IsCastle) || st.Town == null || st.IsUnderSiege) return;
                var gar = st.Town.GarrisonParty;
                var owner = st.OwnerClan;
                var clan = CavalryTemplate.ClanOf(party);
                if (gar == null || gar == party || owner == null || clan == null || owner == Clan.PlayerClan) return;
                bool own = owner == clan;
                if (!own && (clan.Kingdom == null || owner.Kingdom != clan.Kingdom)) return;
                var all = AiGear.Armories();
                Dictionary<ItemObject, int> garArm;
                if (all == null || !all.TryGetValue(gar.Id, out garArm) || garArm == null || garArm.Count == 0) return;
                int need = SaddleLaw.BuyNeed(party);
                if (need <= 0) return;
                Flush();
                _dHStops++; _dHNeed += need;
                var gp = SaddleLaw.Pairs(gar, GarrisonArmory.PatrolRiders(st));
                int keep = Math.Max(Math.Max(0, c.AiMountSpareBuffer), (int)Math.Ceiling(MBMath.ClampFloat(c.AiSpareHorseShare, 0f, 1f) * gp.Riders - 1e-4));
                int n = Math.Min(need, gp.FreeHarness - keep);
                if (n <= 0) return;
                var lord = party.LeaderHero;
                Hero payee = null;
                int budget = int.MaxValue;
                bool gearRule = false;
                if (!own)
                {
                    payee = owner.Leader;
                    if (payee == null || !payee.IsAlive || payee == lord) return;
                    int gearLeft = 0;
                    gearRule = c.AiMountGearPurseShare > 0f && !clan.IsUnderMercenaryService && ClanBudget.GearCap(clan, out gearLeft);
                    int purse = Math.Max(0, lord.Gold);
                    budget = gearRule ? Math.Min(gearLeft, (int)(Math.Max(0, purse - Math.Max(0, c.AiGearGoldReserve)) * MBMath.ClampFloat(c.AiMountGearPurseShare, 0f, 1f)))
                                      : (int)(purse * MBMath.ClampFloat(c.AiMountPurseShare, 0f, 1f));
                    if (budget < 50) { _dHNoBudget++; return; }
                }
                int done = 0, gold = 0, guard = 0;
                try
                {
                while (done < n && guard++ < 64)
                {
                    ItemObject best = null; int bestCnt = 0;
                    foreach (var kv in garArm)
                    {
                        var it = kv.Key;
                        // recenzja r3-jazda: sztuki, ktorych DTE nie przyjmie (zbior odmow sesji / czarna lista), pomijane - nie blokuja reszty uprzezy
                        if (it == null || kv.Value <= 0 || !SaddleLaw.Fits(it) || !AiGear.DteTakes(it)) continue;
                        if (best == null || it.Value < best.Value) { best = it; bestCnt = kv.Value; }
                    }
                    if (best == null) break;
                    int take = Math.Min(n - done, bestCnt);
                    int price = 0;
                    if (!own)
                    {
                        price = Stables.MarketPrice(st, new EquipmentElement(best));
                        if (price <= 0) price = Math.Max(1, best.Value);
                        take = Math.Min(take, budget / price);
                    }
                    if (take <= 0) break;
                    if (!AiGear.AddToArmory(party, best, take))
                    {
                        // DTE nie przyjal - uprzaz zostaje w zalodze; sztuka trafila do zbioru odmow (DteTakes = false), wiec nastepny obieg bierze kolejna
                        // najtansza; odmowa bez wpisu do zbioru (blad wywolania) - koniec jak dotad
                        _dHRefused += take;
                        if (AiGear.DteTakes(best)) break;
                        continue;
                    }
                    try { AiWear.MoveWorn(gar, party, best, take, bestCnt); } catch { _stumbles++; _stumblesAll++; }   // stan sztuk idzie z uprzeza (jak A7)
                    if (bestCnt - take > 0) garArm[best] = bestCnt - take; else garArm.Remove(best);
                    if (!own) { budget -= price * take; gold += price * take; }
                    done += take;
                }
                }
                finally
                {
                    if (!own && gold > 0 && Pay(lord, payee, clan, gearRule, gold)) _dHGold += gold;
                }
                if (done <= 0) return;
                _dHarness += done;
                if (own) _dHOwn += done;
            }
            catch (Exception e)
            {
                _stumbles++; _stumblesAll++;
                if (_stumbles == 1) Log.Error("GarrisonHorses.GiveHarness", e);
            }
        }

        /// <summary>Inny rod krolestwa: pan partii placi glowie rodu pana twierdzy (przelew miedzy bohaterami), kwota schodzi z przydzialu sprzetu rodu (F5).
        /// true - przelew przeszedl (licznik zlota w linii tylko wtedy).</summary>
        private static bool Pay(Hero lord, Hero payee, Clan clan, bool gearRule, int gold)
        {
            try
            {
                TaleWorlds.CampaignSystem.Actions.GiveGoldAction.ApplyBetweenCharacters(lord, payee, gold, true);
            }
            catch (Exception e) { _stumbles++; _stumblesAll++; if (_stumbles == 1) Log.Error("GarrisonHorses.Pay", e); return false; }
            try { if (gearRule) ClanBudget.GearSpent(clan, gold); }
            catch (Exception e) { _stumbles++; _stumblesAll++; if (_stumbles == 1) Log.Error("GarrisonHorses.Pay", e); }
            return true;
        }

        private static int Pick(int[] avail, int[] order)
        {
            foreach (int k in order) if (avail[k] > 0) return k;
            return -1;
        }

        /// <summary>Przenosi do n wierzchowcow kategorii k (najtansze najpierw) ze zbrojowni zalogi: toArmory - do zbrojowni DTE partii (odmowa DTE - kon
        /// wraca do zalogi), inaczej do taboru. Inny rod - kazda sztuka po cenie targu z budzetu (budget, gold). Zwraca, ile przeszlo.</summary>
        private static int Move(MobileParty party, MobileParty gar, Settlement st, Dictionary<ItemObject, int> garArm, int k, int n, bool toArmory, bool own, ref int budget, ref int gold)
        {
            int done = 0, guard = 0;
            while (done < n && guard++ < 64)
            {
                ItemObject best = null; int bestCnt = 0;
                foreach (var kv in garArm)
                {
                    var it = kv.Key;
                    if (it == null || kv.Value <= 0 || !Stables.IsPlainMount(it) || Stables.Level(it.ItemCategory) != k) continue;
                    // recenzja r3-jazda: do zbrojowni DTE tylko sztuki, ktore DTE przyjmie (zbior odmow sesji) - odrzucona nie blokuje reszty kategorii
                    if (toArmory && !AiGear.DteTakes(it)) continue;
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
                if (toArmory)
                {
                    // jak A7 (GarrisonArmory.MoveKits): najpierw do zbrojowni odbiorcy, potem zapis stanu sztuk (AiWear - u koni tylko spis "nie lup"), potem zdjecie u dawcy
                    if (!AiGear.AddToArmory(party, best, take))
                    {
                        // DTE nie przyjal - kon zostaje w zalodze; sztuka jest juz w zbiorze odmow (DteTakes = false) - nastepny obieg bierze kolejna najtansza
                        // tej kategorii; odmowa bez wpisu do zbioru (blad wywolania) - koniec jak dotad
                        _dRefused += take;
                        if (AiGear.DteTakes(best)) break;
                        continue;
                    }
                    try { AiWear.MoveWorn(gar, party, best, take, bestCnt); } catch { _stumbles++; _stumblesAll++; }
                }
                else party.ItemRoster.AddToCounts(new EquipmentElement(best), take);
                if (bestCnt - take > 0) garArm[best] = bestCnt - take; else garArm.Remove(best);
                if (!own) { budget -= price * take; gold += price * take; }
                done += take; _dCat[k] += take;
            }
            return done;
        }
    }
}
