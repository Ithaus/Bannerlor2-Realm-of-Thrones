using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// POBOR OD LUDNOSCI - krok 3 (docs/PLAN-POBOR.md pkt 2.5, 2.6; audyt ekonomii 04.10):
    /// 1. ZLOTO ZA WERBUNEK: vanilla RecruitmentCampaignBehavior.ApplyInternal placi za ochotnika
    ///    i za najemnika z karczmy `GiveGoldAction(lord, null)` - zloto znika. U gracza BK placi
    ///    notablowi. Teraz AI tez: za ochotnika - notablowi, ktory go wystawil; za najemnika z karczmy
    ///    (i z kasy karawany) - miastu; E5/B6 (201): przy VeteranPurseFollowsMan - do sakiewki najetych (partia, ktora najela), miastu tylko kon z polki (157).
    /// 2. DARMOWY KOMPLET NOWEJ PARTII AI: DTE EveryoneCampaignBehavior.OnMobilePartyCreated wklada do
    ///    zbrojowni kazdej nowej partii AI komplet dla calego skladu - z niczego. Po starcie gry
    ///    pomijamy to dla partii AI (gracz bez zmian); lord kupuje sprzet na targu (AiGear).
    /// </summary>
    internal static class LevyGold
    {
        private static int _toNotables, _toTowns, _skippedKits, _dayStamp = -1;
        private static int _skippedLords;   // E8/T (263): z _skippedKits - partie lordow (linia "Dorobek startowy (E8/T)")
        private static long _toPurses;      // E5/B6 (201): oplata za najem z karczmy do sakiewek najetych (lordowie AI i karawany)
        // 3.1 (31, E3/6 - paczka 164a-5): zwrot = dokladnie to, co gra zdjela z kiesy wodza (zdarzenie GiveGoldAction wodz -> nic, po obcieciu do kiesy),
        // nie koszt liczony od nowa po werbunku (gra liczy go PRZED dodaniem rekruta - RecruitmentCampaignBehavior.cs:608 - i obcina do kiesy :625);
        // notabl brak/zmarly - kasa osady; ochotnik z mapy - kiesa najblizszej wsi
        private static long _toSettl, _toVillages, _unrouted, _oldWay, _newWay;
        private static bool _rcOpen;
        private static Hero _rcLeader;
        private static long _rcTaken;

        internal static void Reset() { _toNotables = _toTowns = _skippedKits = 0; _skippedLords = 0; _dayStamp = -1; _toSettl = _toVillages = _unrouted = _oldWay = _newWay = 0; _toPurses = 0; _rcOpen = false; _rcLeader = null; _rcTaken = 0; }

        public struct RcState { public bool Prev; public Hero PrevLeader; public long PrevTaken; public int Car0; }

        /// <summary>3.1 (E3/6): okno werbunku - wodz platnik i kasa karawany przed (gra placi z niej bez zdarzenia).</summary>
        public static void ApplyInternalPrefix(MobileParty __0, out RcState __state)
        {
            __state = new RcState { Prev = _rcOpen, PrevLeader = _rcLeader, PrevTaken = _rcTaken };
            try
            {
                _rcOpen = true; _rcLeader = __0 != null ? __0.LeaderHero : null; _rcTaken = 0;
                __state.Car0 = __0 != null && __0.IsCaravan ? __0.PartyTradeGold : 0;
            }
            catch { }
        }

        /// <summary>3.1 (E3/6): okno zamkniete takze po wyjatku w metodzie gry (poprzednie przywrocone - zagniezdzenie).</summary>
        public static void ApplyInternalFinalizer(RcState __state) { _rcOpen = __state.Prev; _rcLeader = __state.PrevLeader; _rcTaken = __state.PrevTaken; }

        /// <summary>3.1 (E3/6): nasluch zdarzen zlota (Seal31) - w oknie werbunku kwota, ktora gra naprawde zdjela wodzowi (wodz -> nic).</summary>
        internal static void OnGold(Hero gh, PartyBase gp, Hero rh, PartyBase rp, int a)
        {
            if (!_rcOpen || _rcLeader == null || a <= 0) return;
            if (ReferenceEquals(gh, _rcLeader) && gp == null && rh == null && rp == null) _rcTaken += a;
        }

        private static Settlement NearestVillage(MobileParty mp)
        {
            Settlement best = null; float bd = float.MaxValue;
            var p = mp.GetPosition2D;
            foreach (var st in Settlement.All)
            {
                if (st == null || !st.IsVillage || st.SettlementComponent == null) continue;
                float d = p.DistanceSquared(st.GetPosition2D);
                if (d < bd) { bd = d; best = st; }
            }
            return best;
        }

        public static void ApplyInternalPostfix(MobileParty __0, Settlement __1, Hero __2, CharacterObject __3, int __4, object __6, RcState __state)
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !s.RecruitGoldToSeller || __0 == null || __3 == null) return;
                Tick();
                string detail = __6 != null ? __6.ToString() : "";
                var model = Campaign.Current.Models.PartyWageModel;
                int cost = model.GetTroopRecruitmentCost(__3, __0.LeaderHero).RoundedResultNumber;
                if (detail == "VolunteerFromMap")
                {
                    // 3.1 (E3/6): ochotnik z mapy (dzis zablokowany latka CS T2b) - zaplata do kiesy najblizszej wsi (regula na 100% przypadkow)
                    if (__0 == MobileParty.MainParty || _rcTaken <= 0) return;
                    int tk = (int)Math.Min(int.MaxValue, _rcTaken);
                    var v = NearestVillage(__0);
                    if (v == null) { _unrouted += tk; return; }
                    v.SettlementComponent.ChangeGold(tk);
                    MoneyLedger.Note169(MoneyLedger.N169Other, v, tk);   // kiesa wsi poza GiveGoldAction (tylko licznik)
                    MoneyLedger.NoteLevyBack(tk);
                    _toVillages += tk;
                    return;
                }
                if (cost <= 0 && detail != "VolunteerFromIndividual") return;
                if (detail == "VolunteerFromIndividual")
                {
                    if (__0 == MobileParty.MainParty) return;   // gracz: BK juz placi notablowi
                    // 3.1 (E3/6): dokladnie to, co zeszlo z kiesy wodza; notabl brak/zmarly - kasa osady (dotad: koszt od nowa i notabl martwy - w nicosc)
                    int taken = (int)Math.Min(int.MaxValue, Math.Max(0L, _rcTaken));
                    if (cost > 0) _oldWay += cost;
                    _newWay += taken;
                    if (taken <= 0) return;
                    if (__2 != null && __2.IsAlive) { __2.ChangeHeroGold(taken); _toNotables += taken; }
                    else if (__1 != null && __1.SettlementComponent != null)
                    {
                        __1.SettlementComponent.ChangeGold(taken);
                        MoneyLedger.Note169(MoneyLedger.N169Other, __1, taken);   // kasa osady poza GiveGoldAction (tylko licznik)
                        _toSettl += taken;
                    }
                    else { _unrouted += taken; return; }
                    MoneyLedger.NoteLevyBack(taken);   // ksiega pieniadza (tylko licznik): gra skasowala te zaplate przez GiveGoldAction
                    try
                    {
                        if (__3.IsMounted && RecruitCost.OwnHorseOn) RecruitCost.NoteOwnHorse(false, __1, 1);   // paczka 160: ochotnik z wlasnym koniem - w koszcie konia nie ma (tylko licznik)
                        else if (__3.IsMounted && Settings.Current.HistoricalRecruitCost && Settings.Current.HorsesAtMarketPrice) RecruitCost.NoteVolunteerHorse((int)RecruitCost.HorseCost(__3, __0.LeaderHero));   // linia "Konie rekrutow (157)" (tylko licznik)
                    }
                    catch { }
                }
                else if (detail == "MercenaryFromTavern")
                {
                    if (__1 == null || __1.SettlementComponent == null) return;
                    int n = Math.Max(1, __4);
                    // POPRAWKA 157 (kon najemnika z karczmy): w koszcie siedzi cena konia tylko wtedy, gdy na polce tego miasta stoi kon tej rasy
                    // (RecruitCost.HorseCost) - teraz ten kon naprawde schodzi z polki (po jednym na najemnika), a gdy koni na polce jest mniej niz
                    // najemnikow, nadplata za brakujace wraca do kiesy, ktora placila (lord - gra skasowala jego zaplate; karawana - kiesa partii).
                    // Polka bez takiego konia: w koszcie konia nie ma (najemnik z wlasnym koniem), nic nie schodzi, nic nie wraca.
                    // PACZKA 160 (RecruitsOwnHorse): kon jest wlasnoscia zolnierza zawsze - w koszcie nie ma konia (RecruitCost), MercShelfOn
                    // nieczynne, miasto dostaje caly koszt (dni zoldu, z premia konnego - MountedWage), nic nie schodzi z polki, nic nie wraca.
                    // Poprawka po recenzji 157: kon w koszcie to cena polki RAZY mnoznik kupujacego (perki, kultura, prawa i ranga klanu BK) - do kasy
                    // za konia z polki i z powrotem za konia, ktorego nie bylo, idzie ta sama kwota, ktora kupujacy za konia zaplacil (per = koszt ze
                    // sprzetem - koszt bez sprzetu); dotad zwrot liczyl sama cene polki: przy mnozniku < 1 kasa miasta oddawala lordowi za konie,
                    // ktorych nie bylo (przy 0.5 i 10 najemnikach - na minus), przy > 1 zatrzymywala doplate za nie. Kon "bez targu" (stala gry
                    // w koszcie, polka nieznana) - jak kon spoza polki: wraca cala doplata, miasto nie bierze nic za konia, ktory z niej nie zszedl.
                    int horse = 0, per = 0, took = 0, refund = 0;
                    Settlement market = null;
                    if (__3.IsMounted && RecruitCost.IsMerc(__3) && RecruitCost.MercShelfOn)
                    {
                        horse = RecruitCost.MercHorseQuote(__3, __0.LeaderHero, out market);
                        per = RecruitCost.HorseShareOfCost(__3, __0.LeaderHero, cost, horse);
                        if (horse > 0 && per > 0) took = RecruitCost.TakeShelfHorses(market ?? __1, __3, n);
                        refund = (n - took) * per;
                        RecruitCost.NoteMercHorses(__1, took, n - took, per, refund, market == null || market.Town == null, false);
                    }
                    else if (__3.IsMounted && RecruitCost.OwnHorseOn) RecruitCost.NoteOwnHorse(true, __1, n);   // paczka 160: najemnik z wlasnym koniem - w koszcie konia nie ma, z polki nic nie schodzi (tylko licznik)
                    // 3.1 (E3/6): rozdzielamy to, co naprawde zeszlo (wodz: zdarzenie gry po obcieciu do kiesy; karawana: spadek kasy - gra odejmuje bez
                    // zdarzenia i nie schodzi ponizej 0), nie koszt x liczba - przy pustej kiesie miasto dostawalo zloto, ktorego nikt nie zaplacil
                    long gross = __0.IsCaravan ? Math.Min((long)cost * n, Math.Max(0L, (long)__state.Car0 - __0.PartyTradeGold)) : Math.Max(0L, _rcTaken);
                    if (refund > gross) refund = (int)gross;
                    if (refund > 0)
                    {
                        if (__0.IsCaravan) __0.PartyTradeGold += refund;
                        else if (__0.LeaderHero != null) __0.LeaderHero.ChangeHeroGold(refund);
                    }
                    int toTown = (int)Math.Min(int.MaxValue, gross - refund);
                    // E5/B6 (201, R19): oplata za najem idzie do sakiewki najetych (partia, ktora ich najela - lord albo karawana), nie do kasy miasta: najmuje sie
                    // prawdziwego czlowieka (weterana E4/10). Za konia z polki (157, MercShelfOn) miasto dostaje dalej to, co kupujacy za niego zaplacil.
                    // Klucz VeteranPurseFollowsMan wylaczony albo bez sakiewki ludzi - cala kwota do kasy miasta jak dotad (HireFee zwraca 0).
                    int horseTown = (int)Math.Min(toTown, Math.Max(0L, (long)Math.Max(0, took) * Math.Max(0, per)));
                    int toPurse = VetPurse.HireFee(__0, toTown - horseTown, horseTown);
                    if (toPurse > 0) { toTown -= toPurse; _toPurses += toPurse; }
                    if (toTown > 0) __1.SettlementComponent.ChangeGold(toTown);
                    _toTowns += toTown;
                    MoneyLedger.Note(MoneyLedger.NMerc, __1, toTown);   // ksiega przeplywow osad (tylko licznik)
                    // karawana placi z kiesy partii bez GiveGoldAction (nic nie znika) - "zwrot skasowanego" liczymy tylko dla wodza (miastu + sakiewce najetych E5/B6 + zwrot = cala zaplata)
                    if (!__0.IsCaravan && __0.LeaderHero != null && gross > 0) MoneyLedger.NoteLevyBack((int)Math.Min(int.MaxValue, gross));
                }
            }
            catch { }
        }

        public static bool PartyCreatedPrefix(MobileParty __0)
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !s.NoFreeKitForNewParties || __0 == null) return true;
                if (Campaign.Current == null || !Campaign.Current.GameStarted) return true;   // start gry - swiat dostaje sprzet
                if (__0 == MobileParty.MainParty) return true;
                Tick();
                // E8/T (263, PLAN 8.10c): partia lorda AI z pierwszych dob nowej kampanii dostaje dorobek stuleci jak partie sprzed startu (ColdStart.LateLord) -
                // zamiast kompletu DTE, ktory dalej jest zablokowany; po oknie nowej gry i dla kazdej innej partii - jak dotad
                if (ColdStart.LateLord(__0)) return false;
                _skippedKits++;
                try { if (__0.IsLordParty) _skippedLords++; } catch { }
                return false;
            }
            catch { return true; }
        }

        private static void Tick()
        {
            int day = (int)CampaignTime.Now.ToDays;
            if (_dayStamp == day) return;
            if (_dayStamp >= 0 && _toNotables + _toTowns + _skippedKits + _toSettl + _toVillages + _unrouted + _toPurses > 0)
                Log.Info("Werbunek: dzien " + _dayStamp + " - zloto AI za ochotnikow do notabli " + _toNotables + ", za najemnikow do miast " + _toTowns
                         + " (E5/B6: oplata za najem do sakiewek najetych " + _toPurses + ")"
                         + "; nowe partie AI bez darmowego kompletu DTE: " + _skippedKits + "."
                         // 3.1 (E3/6): zwrot = dokladnie zdjete z kiesy; rozjazd z kosztem liczonym od nowa (stara regula) - informacja
                         + " 3.1 (E3/6): notabl brak/zmarly - do kas osad " + _toSettl + ", ochotnicy z mapy - do kies wsi " + _toVillages + ", bez odbiorcy (prog 0) " + _unrouted
                         + "; ochotnicy od notabli: zdjete z kies wodzow " + _newWay + " (koszt liczony od nowa po werbunku dalby " + _oldWay + ").");
            if (_dayStamp >= 0) ColdStart.LateFlush(_dayStamp, _skippedKits, _skippedLords);   // E8/T (263): linia doby dorobku partii lordow (te same liczniki, przed zerowaniem)
            _toNotables = _toTowns = _skippedKits = 0; _toSettl = _toVillages = _unrouted = _oldWay = _newWay = 0; _toPurses = 0; _skippedLords = 0;
            _dayStamp = day;
        }

        internal static void ApplyAll(Harmony h)
        {
            string a = "BRAK", b = "BRAK";
            try
            {
                var m = AccessTools.Method(typeof(RecruitmentCampaignBehavior), "ApplyInternal");
                if (m != null)
                {
                    // 3.1 (E3/6): prefiks (okno werbunku) i finalizer (zamkniecie okna) - zwrot dokladnie tego, co zeszlo z kiesy
                    h.Patch(m, prefix: new HarmonyMethod(typeof(LevyGold), nameof(ApplyInternalPrefix)), postfix: new HarmonyMethod(typeof(LevyGold), nameof(ApplyInternalPostfix)),
                            finalizer: new HarmonyMethod(typeof(LevyGold), nameof(ApplyInternalFinalizer)));
                    a = "wpiete (3.1 E3/6: zwrot = dokladnie zdjete z kiesy)";
                }
            }
            catch (Exception e) { Log.Error("LevyGold.ApplyInternal", e); }
            try
            {
                var t = AccessTools.TypeByName("DynamicTroopEquipmentReupload.EveryoneCampaignBehavior");
                var m = t != null ? AccessTools.Method(t, "OnMobilePartyCreated") : null;
                if (m != null) { h.Patch(m, prefix: new HarmonyMethod(typeof(LevyGold), nameof(PartyCreatedPrefix))); b = "wpiete"; }
            }
            catch (Exception e) { Log.Error("LevyGold.PartyCreated", e); }
            Log.Info("LevyGold: zloto za werbunek AI do sprzedajacego " + a + "; koniec darmowego kompletu nowej partii AI (DTE) " + b + ".");
            // E8/T (263, PLAN 1.12): rekrut gracza ta sama sciezka co AI - latki na DTE i oknach werbunku gracza (we wlasnym try; SubModuleMain bez zmian)
            try { PlayerRecruit.ApplyAll(h); } catch (Exception e) { Log.Error("PlayerRecruit.ApplyAll", e); }
        }
    }
}
