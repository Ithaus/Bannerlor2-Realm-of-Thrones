using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// E5/A8 (audyt zlota z niczego 11.10, rozdz. 1b wiersze 17, 18 i 23, rozdz. 2b "Diplomacy - koszt paktu", rozdz. 5b "E5/A8") - BK i Diplomacy bez platnika
    /// albo odbiorcy. Kazdy kawalek ma wlasny wylacznik; linia doby "BK i Diplomacy (E5/A8): dzien N" z kwotami przed (co dawal mod) i po (co zaplacone).
    ///
    /// 1. NOWY ROD SZLACHTY BK (GentryPurseFromTown; BK BKGentryBehavior.CreateGentryClan :485-530): poza startem kampanii BK daje glowie nowego rodu
    ///    `ChangeHeroGold(cost)` z niczego, przelewa cost `vacantEstate.Owner` - a wolny majatek to z definicji majatek bez wlasciciela (Estate.IsDisabled =>
    ///    Owner == null), wiec GiveGoldAction(glowa, null) zabiera cost w nicosc (ksiega: falszywa para R +cost / "w nicosc - inne" cost) - i na koniec
    ///    `ChangeHeroGold(15 000)` z niczego. DECYZJA JEFFA 12.10 ok. 18:40 UTC: nowy rod startuje z 0 - punkt (b) tylko przy GentryStartFromTown (domyslnie nie).
    ///    Teraz: (a) cena majatku = 0 (prefiks, `ref cost`) - majatek przechodzi bez ceny, jak w praktyce zawsze (BK placil
    ///    ja nikomu), falszywa para znika; gdy majatek ma juz wlasciciela (zapytanie gracza odlozone w czasie) - cena jak dotad (licznik); (b) 15 000 placi KASA
    ///    MIASTA, W KTORYM WIES MAJATKU HANDLUJE (regula R10 z E5/A3 - nowy zamozny miejscowy: Village.TradeBound, brak albo nie miasto - Bound, zamek przez
    ///    CastlePurse) z nadwyzki Seal31.Spare, najwyzej tyle, ile jej jest - brak nadwyzki = rod startuje z mniej albo z 0. Kiesa wsi nie jest strona (R10).
    ///    Finalizer zdejmuje glowie rodu to, co BK dopisal (najwyzej 15 000), i oddaje to, co zaplacila kasa. Tworzenie swiata (przed GameStarted, sciezka
    ///    campaignStart z nadaniem przez EstateAction) - regula BK (bilans otwarcia).
    /// 2. HERSZT BANDYTOW BK (BanditHeroPurseFromHideout; BK BanditHeroComponent.CreateParty :211-224 i ConsiderLeaveHideout :111-168):
    ///    (a) `leader.ChangeHeroGold(10 000)` z niczego - teraz z NADWYZKI KASY KRYJOWKI (to, co lezy ponad skarbiec - ta sama regula co kiesa startowa nowej
    ///        bandy w OutlawLaw.StartGoldPostfix; skarbca nie rusza nikt, to lup dla tego, kto kryjowke oczysci), najwyzej tyle, ile jej jest, przez
    ///        OutlawLaw.TakeHoardSurplus (suma _hoardSeen - wejscia band do kryjowki nie wezma tego za dosypke gry);
    ///    (b) przy wyjsciu z kryjowki BK "sprzedaje" wszystko poza zywnoscia po cenie najblizszego miasta - zloto dla herszta z niczego, towar znika w nicosc.
    ///        Teraz prefiks zdejmuje z jukow towar niebedacy zywnoscia na czas wywolania (petla BK nie ma czego sprzedac), finalizer kladzie go z powrotem,
    ///        a gdy banda wyszla (BK wolal LeaveSettlementAction - jego petla biegla) - od razu sprzedaje juki przez KASY MIAST paserem OutlawLaw
    ///        (OutlawLaw.FenceNow -> FenceSell: miasta w promieniu placa z kasy ponad rezerwe, towar trafia na polke, dzialka pasera zostaje w kasie,
    ///        zaplata do kiesy bandy; bron i sprzet "nie na sprzedaz" zostaja w jukach jak u kazdej bandy). Jedna regula na jedno zjawisko (sprzedaz lupu
    ///        band), bez drugiego handlu. Dzienny paser tej bandy nie obsluguje (bez kiesy handlu - IsPartyTradeActive), stad wywolanie w chwili wyjscia.
    /// 3. UCZTA BK (FeastTownGetsPaid; BK Feast.Automanage :184-220): gospodarz placi `ChangeHeroGold(-cena)` (kiesa obcina do 0), a miasto dostaje
    ///    `Town.ChangeGold(cena)` w calosci - biedny gospodarz robil zloto z niczego. Teraz finalizer (prefiks zapamietuje kiese gospodarza i kase miasta)
    ///    cofa z kasy miasta to, co dostala ponad zaplate gospodarza (miasto dostaje tyle, ile gospodarz zaplacil).
    /// 4. KOSZT PAKTU DIPLOMACY (PactCostToOtherRealm; Diplomacy GoldCost.ApplyCost :84): koszt paktu o nieagresji (DetermineGoldCostForFormingNonAggressionPact:
    ///    500 + 100 x skala) i pokoju (DetermineGoldCostForMakingPeace: do 500) to GoldCost bez odbiorcy - `_giver.ChangeHeroGold(-Value)` w nicosc. Teraz
    ///    okna (prefiks + finalizer) na FormNonAggressionPactAction.AssessCosts i KingdomPeaceAction.AcceptPeace zapamietuja DRUGA STRONE (otherKingdom),
    ///    a prefiks GoldCost.ApplyCost (tylko bez odbiorcy i tylko w oknie) przelewa kwote od dajacego (krol albo gracz; tyle, ile ma - jak gra) do SKARBCA
    ///    KROLESTWA DRUGIEJ STRONY (KingdomBudgetWallet - w sumie ksiegi "skarbce krolestw"). Druga strona wyeliminowana albo brak - nic nie pobrane (nic
    ///    w nicosc). Recenzja: takze druga strona, ktora upada RAZEM z tym pokojem (EliminationOnPeace.OtherKingdom - AcceptPeace wola ApplyCost PRZED
    ///    SoftlyDestroyKingdomAction, a skarbiec upadlego krolestwa wypada ze swiata) - nic nie pobrane. Koszt poslanca (tylko gracz, poza oknami) i koszty z odbiorca - bez zmian; odszkodowania (KingdomWalletCost) - CrownIncome (165).
    /// 5. ZAKUPY DWORU BK (f19-e1, CourtShoppingPaysTown; przeglad 18.10 uwaga 1): BK CourtGrace.Update (1. dzien pory roku, rody z rada; co 7 dob przy AutoPurchase)
    ///    wola BuyGoodsAction.BuyBestToWorst(schowek siedziby, miasto targu, glowa rodu, towary, budzet = kiesa glowy) -> BuyGoods: towar schodzi z polki miasta do schowka,
    ///    glowa placi ChangeHeroGold(-suma), a miasto NIE dostaje nic (brak market.ChangeGold) - zloto w nicosc, towar za darmo (fin93: 109110 -249 283, 109201 -235 486;
    ///    tak samo w kazdym logu 160/364 dob od 17.10). Teraz prefiks zapamietuje kiese glowy, finalizer daje kasie miasta targu (parametr market) dokladnie to, co zeszlo
    ///    glowie (migawka), z ta sama podstawa robocizny handlu co kazda sprzedaz z polki (TownFolk.Sale, MixGoods - jak dwor krola CourtGoods.Crown) i pozycja ksiegi
    ///    kas osad poza tickiem (Note169 N169BkCourt). Towar idzie do schowka siedziby (Settlement.Stash - zapas ksiegi towarow), wiec ramki ksiegi towarow nie dokladamy
    ///    (przewoz polka -> schowek znosi sie do zera). Gracz tak samo. Zakup BK dalej biegnie obok naszego dworu E3-P1 (decyzja Jeffa, czy go zostawic).
    /// 6. UGODA Z GRUPA INTERESU BK (f19-e1, GroupCompromisePaysGroup; przeglad 18.10 uwaga 2): lambda Demand.FinancialCompromise (fulfil; w BK instancyjna metoda
    ///    &lt;get_FinancialCompromise&gt;b__30_2 typu Demand) robi fulfiller.ChangeHeroGold(-koszt) i nikomu nie placi, choc tekst BK mowi "paid out to the group, mostly
    ///    to the leader" (fin93 133 188 w 160 dob; sesja Jeffa 81b: 60 600 + 15 944 + 9 944 w 11 dob = reszta swiata poza tickiem co do zlotowki). Teraz finalizer:
    ///    to, co zeszlo platnikowi (migawka) - 2/3 przywodcy grupy (Group.Leader), reszta po rowno pozostalym czlonkom (Group.Members bez przywodcy i platnika; ostatni
    ///    bierze reszte z dzielenia); bez czlonkow - calosc przywodcy; bez zywego przywodcy - calosc czlonkom; bez nikogo - kasa siedziby rodu platnika (ClanBudget.Seat,
    ///    Note169 "inne"), bez siedziby - wraca platnikowi (nic nie znika). Bezposrednio ChangeHeroGold (+ CirculationWindows.NoteHeroGold - jak DsTowns), bez zdarzenia
    ///    gry: platnik juz zaplacil bez zdarzenia, wiec swiat w tej metodzie ma zmiane 0.
    /// 7. ZLOTO DWORZAN BK (322a, CourtierGoldStaysInRealm; audyt ekonomii 03.11 wyciek 1): BK BKCourtierBehavior.OnWeeklyTick -> MaintainKingdom wola dla kazdego
    ///    zywego, doroslego, nieozenionego lorda rodu "Courtiers of X" (bk_courtiers_*) prywatna DrainGold(Hero) = ChangeHeroGold(-Gold) - cale zloto w nicosc
    ///    (log Jeffa 2026-11-03_00-19-16: reszta R ksiegi 109215 -45 608 przy DrainGold 45 681, 109222 -62 599 przy 62 347, 109229 -17 864, 109243 -22 412).
    ///    Nowi dworzanie (HeroCreator.CreateSpecialHero, ClanActions.CreateNewClan bez removeGold) maja 0 - zloto dworzan to zawsze zloto swiata (np. sakiewka
    ///    turniejowa). Teraz prefiks zapamietuje kiese, finalizer daje to, co zeszlo, KASIE STOLICY krolestwa rodu dworzan (CapitalTreasury.CapitalOfKingdom - BK
    ///    BKCapitalBehavior, ta sama, w ktorej BK trzyma dwor; Note169 "inne"), czesc, ktorej kasa nie przyjela, i calosc bez stolicy - SKARBCOWI KROLESTWA
    ///    (KingdomBudgetWallet, migawka korony przesunieta - zapas, nie wplyw dnia), bez zywego krolestwa - wraca dworzaninowi (nic nie znika).
    /// 8. WOLNY MAJATEK BK (323a, VacantEstatePriceToLord; Jeff 03.11 "majatek naprawa"): BK EstateAction.TakeAction (Buy, Estate.Owner == null, GoldCost &gt; 0;
    ///    cena BKEstatesModel.GetBuy = max(500, 10% EstateValue), tylko gracz z EstateVM.ExecuteBuy) robi ActionTaker.ChangeHeroGold(-GoldCost) - zloto w nicosc
    ///    (tekst BK "no seller"). Wolna ziemia wsi w BK nalezy do PANA WSI: ten sam GetBuy daje rodowi pana wioski wolny majatek za 50 wplywow bez zlota, a pana
    ///    BK liczy przez VillageExtensions.GetActualOwner (glowa rodu wlasciciela; posiadacz tytulu de jure, gdy jest w tej samej frakcji) - wiec on jest sprzedawca.
    ///    Teraz prefiks zapamietuje kiese kupca, finalizer daje to, co zeszlo (najwyzej GoldCost) PANU WSI (ChangeHeroGold bez zdarzenia - kupiec zaplacil bez
    ///    zdarzenia, swiat w tej metodzie ma zmiane 0, jak CompFin; cena ziemi nie jest dochodem rodu, jak w LandSale); bez zywego pana (albo pan = kupiec) -
    ///    kasa miasta handlu wsi (TradeTownOf, Note169 "inne"); bez nikogo albo gdy majatek nie przeszedl na kupca - wraca kupcowi (nic nie znika). Linia na kazdy
    ///    zakup "Wolny majatek BK (323a): dzien N" + segment doby w linii E5/A8.
    /// Wyjatek per zdarzenie - licznik potkniec, bez globalnego wylacznika. Nic do zapisu (stan doby od zera po wczytaniu).
    /// </summary>
    internal static class BkDipGold
    {
        internal static bool GentryOn { get { var s = Settings.Current; return s != null && s.GentryPurseFromTown; } }
        // decyzja Jeffa 12.10 ok. 18:40 UTC: nowy rod szlachty startuje z 0 - kasa miasta placi tylko przy GentryStartFromTown (domyslnie wylaczone)
        internal static bool GentryTownPays { get { var s = Settings.Current; return s != null && s.GentryStartFromTown; } }
        internal static bool BanditOn { get { var s = Settings.Current; return s != null && s.BanditHeroPurseFromHideout; } }
        internal static bool FeastOn { get { var s = Settings.Current; return s != null && s.FeastTownGetsPaid; } }
        internal static bool PactOn { get { var s = Settings.Current; return s != null && s.PactCostToOtherRealm; } }
        internal static bool CourtShopOn { get { var s = Settings.Current; return s != null && s.CourtShoppingPaysTown; } }       // f19-e1 (5)
        internal static bool CompromiseOn { get { var s = Settings.Current; return s != null && s.GroupCompromisePaysGroup; } }   // f19-e1 (6)
        internal static bool CourtierOn { get { var s = Settings.Current; return s != null && s.CourtierGoldStaysInRealm; } }     // 322a (7)
        internal static bool VacantOn { get { var s = Settings.Current; return s != null && s.VacantEstatePriceToLord; } }        // 323a (8)
        private static bool Live { get { var c = Campaign.Current; return c != null && c.GameStarted; } }

        private const int KGentryGift = 15000;      // BK BKGentryBehavior.cs:522
        private const int KBanditGift = 10000;      // BK BanditHeroComponent.cs:218

        internal sealed class GentryCtx { public Settlement S; public object Estate; public Hero Owner0; public bool Off; }
        internal sealed class HeroCtx { public Hero H; public int G0; }
        internal sealed class PackCtx { public List<KeyValuePair<EquipmentElement, int>> Stash; public int G0; public bool Off; }
        internal sealed class FeastCtx { public Hero H; public Town T; public long H0, T0; public MercFeast.Snap G; }   // G - E8/G2 (261): stan rynku i spizarni sprzed zakupow
        internal sealed class ShopCtx { public Hero H; public Town T; public int G0; }        // f19-e1 (5): glowa rodu i miasto targu zakupu dworu BK
        internal sealed class CompCtx { public Hero H; public object D; public int G0; }      // f19-e1 (6): platnik ugody i zadanie BK (Demand)
        internal sealed class VacCtx { public Hero H; public object E; public Settlement S; public int G0, Cost; }   // 323a (8): kupiec, majatek, osada, kiesa przed, cena

        private const double GroupLeaderShare = 2.0 / 3.0;   // f19-e1 (6): "mostly to the leader" (tekst BK) - 2/3 przywodcy, reszta czlonkom

        // okna paktow Diplomacy: druga strona traktatu
        private static readonly Kingdom[] _pact = new Kingdom[4];
        private static int _pactDepth;

        // odczyt typow BK i Diplomacy (bez referencji kompilacji)
        private static PropertyInfo _estOwner, _feastHost, _feastTown, _gcValue, _gcGiver, _gcReceiver;
        private static FieldInfo _elimOther;        // Diplomacy EliminationOnPeace.OtherKingdom (recenzja)
        private static Type _demandT;               // f19-e1 (6): BK Demand
        private static PropertyInfo _dGroup, _gLeader, _gMembers;   // f19-e1 (6): Demand.Group, DiplomacyGroup.Leader / Members
        private static PropertyInfo _eaType, _eaEstate, _eaGold, _eaTaker, _estData, _edSettl;   // 323a (8): EstateAction.Type / Estate / GoldCost / ActionTaker, Estate.EstatesData, EstateData.Settlement
        private static MethodInfo _actualOwner;                     // 323a (8): BK VillageExtensions.GetActualOwner(Village)

        // liczniki doby
        private static long _gDue, _gPaid, _gCost, _gOff, _gOwnedCost;
        private static int _gN, _gShortN, _gCostN, _gOffN, _gOwnedN;
        private static long _hDue, _hPaid, _hOff;
        private static int _hN, _hShortN, _hOffN;
        private static long _pkValue, _pkOff, _pkPaid;
        private static int _pkLeft, _pkUnits, _pkOffN, _pkSold, _pkNoFence;
        private static long _fHostPaid, _fTownGot, _fCut, _fOff;
        private static int _fN, _fCutN, _fOffN;
        private static long _kDue, _kPaid, _kPlayer, _kNoRecv, _kOff;
        private static int _kN, _kNoRecvN, _kOffN, _kFallsN;
        private static long _sPaid, _sToTown, _sPlayer, _sOff;                  // f19-e1 (5)
        private static int _sCalls, _sN, _sOffN, _sBroke;
        private static int _sDrained;                                          // recenzja calosci 18/19.10: budzet BK doszedl do 0 (kiesa glowy wyczerpana) - dalsze pozycje BK bez limitu
        private static long _cPaid, _cPlayer, _cLeader, _cMembers, _cSeat, _cKept, _cOff, _cToPlayer;   // f19-e1 (6)
        private static int _cN, _cMembersN, _cSeatN, _cKeptN, _cOffN, _cNoLeaderN;
        private static long _dPaid, _dTown, _dCrown, _dBack, _dOff;                                    // 322a (7)
        private static int _dN, _dTownN, _dCrownN, _dBackN, _dOffN;
        private static long _vPaid, _vLord, _vTown, _vBack, _vUndo, _vOff, _vPlayer;                    // 323a (8)
        private static int _vN, _vLordN, _vTownN, _vBackN, _vUndoN, _vOffN;
        private static int _overflow, _stumbles;
        private static readonly HashSet<string> _err = new HashSet<string>();
        private static readonly List<string> _wired = new List<string>(), _missing = new List<string>();
        private static string _wiredText = "-", _missingText = "-";
        private static bool _startLogged;

        /// <summary>Konstruktor ArmouryBehavior (nowa kampania i kazde wczytanie).</summary>
        internal static void Reset()
        {
            Array.Clear(_pact, 0, _pact.Length); _pactDepth = 0;
            _startLogged = false;
            _stumbles = 0; _err.Clear();
            ClearDay();
        }

        private static void ClearDay()
        {
            _gDue = _gPaid = _gCost = _gOff = _gOwnedCost = 0; _gN = _gShortN = _gCostN = _gOffN = _gOwnedN = 0;
            _hDue = _hPaid = _hOff = 0; _hN = _hShortN = _hOffN = 0;
            _pkValue = _pkOff = _pkPaid = 0; _pkLeft = _pkUnits = _pkOffN = _pkSold = _pkNoFence = 0;
            _fHostPaid = _fTownGot = _fCut = _fOff = 0; _fN = _fCutN = _fOffN = 0;
            _kDue = _kPaid = _kPlayer = _kNoRecv = _kOff = 0; _kN = _kNoRecvN = _kOffN = _kFallsN = 0;
            _sPaid = _sToTown = _sPlayer = _sOff = 0; _sCalls = _sN = _sOffN = _sBroke = 0; _sDrained = 0;
            _cPaid = _cPlayer = _cLeader = _cMembers = _cSeat = _cKept = _cOff = _cToPlayer = 0; _cN = _cMembersN = _cSeatN = _cKeptN = _cOffN = _cNoLeaderN = 0;
            _dPaid = _dTown = _dCrown = _dBack = _dOff = 0; _dN = _dTownN = _dCrownN = _dBackN = _dOffN = 0;
            _vPaid = _vLord = _vTown = _vBack = _vUndo = _vOff = _vPlayer = 0; _vN = _vLordN = _vTownN = _vBackN = _vUndoN = _vOffN = 0;
            _overflow = 0;
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            try { if (_err.Add(where)) Log.Error("BkDipGold." + where, e); } catch { }
        }

        private static Hero OwnerOf(object estate)
        {
            if (estate == null) return null;
            var p = _estOwner;
            if (p == null || !p.DeclaringType.IsInstanceOfType(estate)) p = _estOwner = AccessTools.Property(estate.GetType(), "Owner");
            return p != null ? p.GetValue(estate, null) as Hero : null;
        }

        /// <summary>Kasa miasta, w ktorym wies handluje (regula R10 E5/A3 - jak NotableGold.TradeTown dla wsi): TradeBound (miasto), inaczej Bound. null - brak.</summary>
        private static Settlement TradeTownOf(Settlement st)
        {
            if (st == null) return null;
            if (st.IsTown || st.IsCastle) return st.Town != null ? st : null;
            var v = st.Village;
            if (v == null) return null;
            var tb = v.TradeBound;
            if (tb != null && tb.IsTown && tb.Town != null) return tb;
            var b = v.Bound;
            return b != null && b.Town != null ? b : null;
        }

        // ------------------------------------------------------------ 1. nowy rod szlachty BK: BKGentryBehavior.CreateGentryClan
        // parametry: (Settlement settlement, Estate vacantEstate, TextObject clanName, Equipment equipment, bool campaignStart, int cost, CharacterObject template)
        public static void GentryPre(Settlement __0, object __1, bool __4, ref int __5, out GentryCtx __state)
        {
            __state = null;
            try
            {
                if (!Live || __0 == null || __1 == null) return;             // tworzenie swiata - regula BK (bilans otwarcia)
                var owner0 = OwnerOf(__1);
                __state = new GentryCtx { S = __0, Estate = __1, Owner0 = owner0, Off = !GentryOn };
                if (__state.Off || __4 || __5 <= 0) return;                  // campaignStart: BK nadaje majatek przez EstateAction, ceny nie uzywa
                if (owner0 != null) { _gOwnedN++; _gOwnedCost += __5; return; }   // majatek ma juz wlasciciela - cena jak dotad (BK: z niczego do wlasciciela)
                _gCostN++; _gCost += __5;
                __5 = 0;                                                     // majatek bez ceny - BK placil ja nikomu (GiveGoldAction do null)
            }
            catch (Exception e) { __state = null; Stumble("GentryPre", e); }
        }

        public static void GentryFin(GentryCtx __state)
        {
            if (__state == null) return;
            try
            {
                var h = OwnerOf(__state.Estate);
                if (h == null || ReferenceEquals(h, __state.Owner0) || !h.IsAlive) return;   // rod nie powstal (BK usunal bohaterow) - BK nic nie dal
                long due = Math.Min(KGentryGift, Math.Max(0, h.Gold));                         // BK dopisal 15 000 na koniec (cena = 0 albo przelana dalej)
                if (due <= 0) return;
                if (__state.Off) { _gOffN++; _gOff += due; return; }
                h.ChangeHeroGold(-(int)due);
                var payer = GentryTownPays ? TradeTownOf(__state.S) : null;                     // domyslnie rod startuje z 0 (decyzja Jeffa 12.10)
                long pay = payer != null && payer.SettlementComponent != null ? Math.Min(due, Seal31.Spare(payer)) : 0;
                if (pay > 0)
                {
                    payer.SettlementComponent.ChangeGold(-(int)pay);
                    h.ChangeHeroGold((int)pay);
                }
                _gN++; _gDue += due; _gPaid += pay;
                if (pay < due) _gShortN++;
            }
            catch (Exception e) { Stumble("GentryFin", e); }
        }

        // ------------------------------------------------------------ 2a. herszt BK: BanditHeroComponent.CreateParty(Hideout origin, Hero leader, PartyTemplateObject template)
        public static void HeroPre(Hero __1, out HeroCtx __state)
        {
            __state = null;
            try
            {
                if (!Live || __1 == null) return;
                __state = new HeroCtx { H = __1, G0 = __1.Gold };
            }
            catch (Exception e) { __state = null; Stumble("HeroPre", e); }
        }

        public static void HeroFin(Hideout __0, HeroCtx __state)
        {
            if (__state == null) return;
            try
            {
                var h = __state.H;
                long due = Math.Min(KBanditGift, (long)h.Gold - __state.G0);   // partia o tym id juz jest - BK wraca przed dopisaniem (0)
                if (due <= 0) return;
                if (!BanditOn) { _hOffN++; _hOff += due; return; }
                h.ChangeHeroGold(-(int)due);
                int pay = __0 != null ? OutlawLaw.TakeHoardSurplus(__0.Settlement, (int)due) : 0;
                if (pay > 0) h.ChangeHeroGold(pay);
                _hN++; _hDue += due; _hPaid += pay;
                if (pay < due) _hShortN++;
            }
            catch (Exception e) { Stumble("HeroFin", e); }
        }

        // ------------------------------------------------------------ 2b. juki herszta BK: BanditHeroComponent.ConsiderLeaveHideout(MobileParty party)
        public static void PackPre(MobileParty __0, out PackCtx __state)
        {
            __state = null;
            try
            {
                if (!Live || __0 == null || !__0.IsBandit || __0.LeaderHero == null) return;
                var cs = __0.CurrentSettlement;
                if (cs == null || !cs.IsHideout) return;                       // BK sprzedaje tylko przy wyjsciu z kryjowki
                var r = __0.ItemRoster;
                if (r == null) return;
                if (!BanditOn) { __state = new PackCtx { G0 = __0.LeaderHero.Gold, Off = true }; return; }
                List<KeyValuePair<EquipmentElement, int>> stash = null;
                for (int i = 0; i < r.Count; i++)
                {
                    var el = r.GetElementCopyAtIndex(i);
                    var it = el.EquipmentElement.Item;
                    if (it == null || el.Amount <= 0 || it.IsFood) continue;  // ten sam wybor co BK (!IsFood)
                    if (stash == null) stash = new List<KeyValuePair<EquipmentElement, int>>();
                    stash.Add(new KeyValuePair<EquipmentElement, int>(el.EquipmentElement, el.Amount));
                }
                if (stash == null) return;
                __state = new PackCtx { Stash = new List<KeyValuePair<EquipmentElement, int>>(stash.Count) };
                foreach (var kv in stash)
                {
                    r.AddToCounts(kv.Key, -kv.Value);                          // na czas wywolania - petla BK nie ma czego "sprzedac"
                    __state.Stash.Add(kv);                                     // finalizer oddaje tylko to, co naprawde zdjete (wyjatek w polowie)
                }
            }
            catch (Exception e) { Stumble("PackPre", e); }
        }

        public static void PackFin(MobileParty __0, PackCtx __state)
        {
            if (__state == null) return;
            try
            {
                bool left = __0 != null && __0.CurrentSettlement == null;     // BK wyprowadzil bande (LeaveSettlementAction) - jego petla sprzedazy biegla
                if (__state.Off)
                {
                    var lh = __0 != null ? __0.LeaderHero : null;
                    long made = lh != null ? (long)lh.Gold - __state.G0 : 0;
                    if (left && made > 0) { _pkOffN++; _pkOff += made; }
                    return;
                }
                var r = __0 != null ? __0.ItemRoster : null;
                if (r == null || __state.Stash == null) return;
                long value = 0; int units = 0;
                foreach (var kv in __state.Stash)
                {
                    r.AddToCounts(kv.Key, kv.Value);                           // juki wracaja do bandy - takze po wyjatku BK
                    units += kv.Value; value += (long)kv.Key.ItemValue * kv.Value;
                }
                if (!left) return;
                _pkLeft++; _pkUnits += units; _pkValue += value;
                int g0 = __0.PartyTradeGold, u0 = Units(r);
                if (!OutlawLaw.FenceNow(__0)) { _pkNoFence++; return; }    // bez prawa wyrzutkow albo skupu lupu - juki zostaja u bandy
                _pkPaid += Math.Max(0, __0.PartyTradeGold - g0); _pkSold += Math.Max(0, u0 - Units(r));   // paser sprzedaje tez zywnosc (OutlawNoFreeFood) - liczone wszystkie sztuki
            }
            catch (Exception e) { Stumble("PackFin", e); }
        }

        private static int Units(ItemRoster r)
        {
            int n = 0;
            for (int i = 0; i < r.Count; i++) { var el = r.GetElementCopyAtIndex(i); if (el.Amount > 0) n += el.Amount; }
            return n;
        }

        // ------------------------------------------------------------ 3. uczta BK: Feast.Automanage(float food, float variety, float alcohol)
        public static void FeastPre(object __instance, out FeastCtx __state)
        {
            __state = null;
            try
            {
                if (!Live || __instance == null || _feastHost == null || _feastTown == null) return;
                var h = _feastHost.GetValue(__instance, null) as Hero;
                var t = _feastTown.GetValue(__instance, null) as Town;
                if (h == null || t == null) return;
                __state = new FeastCtx { H = h, T = t, H0 = h.Gold, T0 = t.Gold };
                __state.G = MercFeast.FeastBefore(t);   // E8/G2 (261): stan rynku i spizarni uczty (zywnosc, wino, piwo) - sam odczyt, wlasny licznik potkniec
            }
            catch (Exception e) { __state = null; Stumble("FeastPre", e); }
        }

        /// <summary>E8/G2 (261): czy okno uczty (prefiks i finalizer na BK Feast.Automanage) jest wpiete - MercFeast poprawia w nim towar rynku.</summary>
        internal static bool FeastWired
        {
            get { foreach (var w in _wired) if (w != null && w.StartsWith("uczta", StringComparison.Ordinal)) return true; return false; }
        }

        public static void FeastFin(FeastCtx __state)
        {
            if (__state == null) return;
            MercFeast.FeastAfter(__state.G);            // E8/G2 (261): zakup schodzi z rynku miasta (BK dopisywal rynkowi +n); przed rozliczeniem zlota - niezalezne od niego
            try
            {
                long paid = Math.Max(0L, __state.H0 - __state.H.Gold);
                long got = (long)__state.T.Gold - __state.T0;
                if (got <= 0 && paid <= 0) return;
                _fN++; _fHostPaid += paid; _fTownGot += got;
                long excess = got - paid;
                if (excess <= 0) return;
                if (!FeastOn) { _fOffN++; _fOff += excess; return; }
                __state.T.ChangeGold(-(int)Math.Min(int.MaxValue, excess)); // miasto dostaje tyle, ile gospodarz zaplacil
                _fCutN++; _fCut += excess;
            }
            catch (Exception e) { Stumble("FeastFin", e); }
        }

        // ------------------------------------------------------------ 4. koszt paktu Diplomacy: okna AssessCosts (NAP) i AcceptPeace (pokoj), potem GoldCost.ApplyCost
        public static void PactPre(Kingdom __1, out bool __state)
        {
            __state = false;
            if (_pactDepth >= _pact.Length) { _overflow++; return; }
            _pact[_pactDepth++] = __1;
            __state = true;
        }

        // okno pokoju: AcceptPeace(kingdomMakingPeace, otherKingdom, cost, tribute, fiefs, EliminationOnPeace shouldBeDestroyed) - ApplyCost biegnie PRZED
        // SoftlyDestroyKingdomAction(otherKingdom); druga strona, ktora upada z tym pokojem, nie jest odbiorca (jej skarbiec wypada ze swiata) - jak brak strony
        public static void PeacePre(Kingdom __1, object __5, out bool __state)
        {
            var k = __1;
            try
            {
                if (k != null && __5 != null && _elimOther != null && PactOn && (bool)_elimOther.GetValue(__5)) { k = null; _kFallsN++; }
            }
            catch (Exception e) { Stumble("PeacePre", e); }
            PactPre(k, out __state);
        }

        public static void PactFin(bool __state)
        {
            if (__state && _pactDepth > 0) _pact[--_pactDepth] = null;
        }

        public static bool CostPre(object __instance)
        {
            if (_pactDepth == 0 || __instance == null) return true;           // poza oknem paktu (poslaniec gracza) - bez zmian
            try
            {
                if (_gcValue == null || _gcGiver == null || _gcReceiver == null) return true;
                if (_gcReceiver.GetValue(__instance, null) != null) return true;   // koszt z odbiorca - przelewa sam Diplomacy
                float v = Convert.ToSingle(_gcValue.GetValue(__instance, null));
                var giver = _gcGiver.GetValue(__instance, null) as Hero;
                if (!(v > 0f) || giver == null) return true;
                int due = (int)v;
                if (due <= 0) return true;
                long can = Math.Min(due, Math.Max(0, giver.Gold));               // gra: ChangeHeroGold(-Value), kiesa obcina do 0
                if (!PactOn) { _kOffN++; _kOff += can; return true; }
                var other = _pact[_pactDepth - 1];
                if (other == null || other.IsEliminated) { _kNoRecvN++; _kNoRecv += due; return false; }   // bez drugiej strony - nie pobrane (nic w nicosc)
                if (can > 0)
                {
                    giver.ChangeHeroGold(-(int)can);
                    other.KingdomBudgetWallet += (int)can;
                }
                _kN++; _kDue += due; _kPaid += can;
                if (giver == Hero.MainHero) _kPlayer += can;
                return false;
            }
            catch (Exception e) { Stumble("CostPre", e); return true; }
        }

        // ------------------------------------------------------------ 5. zakupy dworu BK: BuyGoodsAction.BuyGoods(ItemRoster stash, Town market, Hero buyer, ...)
        public static void ShopPre(Town __1, Hero __2, out ShopCtx __state)
        {
            __state = null;
            try
            {
                if (!Live || __1 == null || __2 == null) return;
                __state = new ShopCtx { H = __2, T = __1, G0 = __2.Gold };
            }
            catch (Exception e) { __state = null; Stumble("ShopPre", e); }
        }

        /// <summary>Finalizer (biegnie takze po wyjatku BK): kasa miasta targu dostaje to, co naprawde zeszlo glowie rodu (BK placi ChangeHeroGold(-suma) w nicosc).</summary>
        public static void ShopFin(ShopCtx __state)
        {
            if (__state == null) return;
            try
            {
                _sCalls++;
                if (__state.G0 <= 0) _sBroke++;                            // BK: budzet = kiesa glowy, przy 0 bez limitu - towar schodzi, zaplata obcieta do 0 (tylko licznik)
                long paid = (long)__state.G0 - __state.H.Gold;
                // recenzja calosci 18/19.10 (decyzja 3 f19-e1): CourtGrace podaje budzet = kiesa glowy (G0), a BuyGoods ogranicza zakup tylko przy budget > 0 i odejmuje
                // pelne ceny (int) - budzet dochodzi do DOKLADNIE 0, gdy kiesa jest wielokrotnoscia ceny ograniczajacego lotu; od tej chwili kolejne pozycje ida bez limitu,
                // a ChangeHeroGold(-suma) obcina kiese do 0 (Hero.Gold = Max(0, v)). Zaplacone == G0 przy G0 > 0 <=> budzet doszedl do 0 (inaczej suma = G0 - reszta budzetu < G0):
                // towar ponad zaplate MOZLIWY (BK nie mowi, ile kupil po zerze). Tylko licznik - obok _sBroke (G0 <= 0) do pomiaru decyzji 3
                if (__state.G0 > 0 && paid == __state.G0 && __state.H.Gold == 0) _sDrained++;
                if (paid <= 0) return;                                     // nic nie kupil (albo kiesa pusta - BK obcina zaplate do 0)
                _sN++; _sPaid += paid;
                if (__state.H == Hero.MainHero) _sPlayer += paid;
                if (!CourtShopOn) { _sOffN++; _sOff += paid; return; }     // klucz wylaczony - jak dotad (zloto w nicosc)
                var t = __state.T;
                var st = t.Settlement;
                int x = (int)Math.Min(int.MaxValue, paid);
                int had = t.Gold;
                t.ChangeGold(x);
                long got = (long)t.Gold - had;
                if (got <= 0) return;
                _sToTown += got;
                int g = (int)Math.Min(int.MaxValue, got);
                MoneyLedger.Note169(MoneyLedger.N169BkCourt, st, g);        // "Przeplywy osad (przyczyny)": kasa miasta poza tickiem - nazwane
                try { TownFolk.Sale(st, g, TownFolk.MixGoods); } catch { }  // sprzedaz kupcow z polki - podstawa robocizny handlu (jak CourtGoods.Crown)
            }
            catch (Exception e) { Stumble("ShopFin", e); }
        }

        // ------------------------------------------------------------ 6. ugoda z grupa interesu BK: lambda Demand.FinancialCompromise (Hero fulfiller) -> bool
        public static void CompPre(object __instance, Hero __0, out CompCtx __state)
        {
            __state = null;
            try
            {
                if (!Live || __0 == null) return;
                __state = new CompCtx { H = __0, D = __instance, G0 = __0.Gold };
            }
            catch (Exception e) { __state = null; Stumble("CompPre", e); }
        }

        private static bool Takes(Hero h) { return h != null && h.IsAlive && !CirculationWindows.OutOfWorld(h); }

        /// <summary>Grupa zadania BK: przywodca i czlonkowie (refleksja; lambda jest metoda instancji Demand, w innym razie pole &lt;&gt;4__this).</summary>
        private static object GroupOf(object d)
        {
            if (d == null || _dGroup == null) return null;
            if (_demandT != null && !_demandT.IsInstanceOfType(d))
            {
                var f = AccessTools.Field(d.GetType(), "<>4__this");
                d = f != null ? f.GetValue(d) : null;
                if (d == null || !_demandT.IsInstanceOfType(d)) return null;
            }
            return _dGroup.GetValue(d, null);
        }

        /// <summary>Wplata czlonkowi grupy; zwraca, ile dostal gracz (0 - inny bohater).</summary>
        private static long GiveHero(Hero h, long amt)
        {
            int x = (int)Math.Min(int.MaxValue, amt);
            h.ChangeHeroGold(x);
            CirculationWindows.NoteHeroGold(h, x);                         // bohater w swiecie - nic (jak DsTowns); poza swiatem nie trafia (Takes)
            if (h != Hero.MainHero) return 0;
            _cToPlayer += x;
            return x;
        }

        /// <summary>Finalizer: to, co zeszlo platnikowi (BK ChangeHeroGold(-koszt) bez odbiorcy) - grupie: 2/3 przywodcy, reszta czlonkom (ostatni bierze reszte z dzielenia).</summary>
        public static void CompFin(CompCtx __state)
        {
            if (__state == null) return;
            try
            {
                var payer = __state.H;
                long paid = (long)__state.G0 - payer.Gold;
                if (paid <= 0) return;
                _cN++; _cPaid += paid;
                if (payer == Hero.MainHero) _cPlayer += paid;
                if (!CompromiseOn) { _cOffN++; _cOff += paid; return; }    // klucz wylaczony - jak dotad (zloto w nicosc)
                Hero leader = null; var others = new List<Hero>();
                var grp = GroupOf(__state.D);
                if (grp != null)
                {
                    leader = _gLeader != null ? _gLeader.GetValue(grp, null) as Hero : null;
                    var ms = _gMembers != null ? _gMembers.GetValue(grp, null) as System.Collections.IEnumerable : null;
                    if (ms != null)
                        foreach (var o in ms)
                        {
                            var h = o as Hero;
                            if (Takes(h) && h != payer && h != leader && !others.Contains(h)) others.Add(h);
                        }
                }
                bool lead = Takes(leader) && leader != payer;
                if (!lead) _cNoLeaderN++;
                long toLeader = lead ? (others.Count > 0 ? (long)Math.Floor(paid * GroupLeaderShare) : paid) : 0;
                long rest = paid - toLeader, mine = 0;
                if (toLeader > 0) { mine += GiveHero(leader, toLeader); _cLeader += toLeader; }
                if (rest > 0 && others.Count > 0)
                {
                    long each = rest / others.Count;
                    for (int i = 0; i < others.Count; i++)
                    {
                        long amt = i == others.Count - 1 ? rest - each * (others.Count - 1) : each;
                        if (amt <= 0) continue;
                        mine += GiveHero(others[i], amt); _cMembers += amt; _cMembersN++;
                    }
                    rest = 0;
                }
                if (mine > 0)
                {
                    string gname = null;
                    try { var pn = AccessTools.Property(grp.GetType(), "Name"); var nm = pn != null ? pn.GetValue(grp, null) : null; gname = nm != null ? nm.ToString() : null; } catch { gname = null; }
                    Log.Player("You receive " + mine + " denars as your share of the financial compromise paid to " + (string.IsNullOrEmpty(gname) ? "your interest group" : "the " + gname) + ".");
                }
                if (rest > 0)
                {
                    // nikt z grupy nie moze wziac - kasa siedziby rodu platnika; bez siedziby wraca platnikowi (nic nie znika)
                    Settlement seat = null;
                    try { seat = payer.Clan != null ? ClanBudget.Seat(payer.Clan) : null; } catch { seat = null; }
                    int x = (int)Math.Min(int.MaxValue, rest);
                    if (seat != null && seat.Town != null)
                    {
                        seat.Town.ChangeGold(x);
                        MoneyLedger.Note169(MoneyLedger.N169Other, seat, x);   // kasa osady poza tickiem - nazwane ("inne")
                        _cSeat += x; _cSeatN++;
                    }
                    else { payer.ChangeHeroGold(x); _cKept += x; _cKeptN++; }
                }
            }
            catch (Exception e) { Stumble("CompFin", e); }
        }

        // ------------------------------------------------------------ 7. zloto dworzan BK: BKCourtierBehavior.DrainGold(Hero) (prywatna, instancyjna)
        public static void CourtierPre(Hero __0, out HeroCtx __state)
        {
            __state = null;
            try
            {
                if (!Live || __0 == null || __0.Gold <= 0) return;
                __state = new HeroCtx { H = __0, G0 = __0.Gold };
            }
            catch (Exception e) { __state = null; Stumble("CourtierPre", e); }
        }

        /// <summary>Finalizer: to, co BK zdjal dworzaninowi (ChangeHeroGold(-Gold) bez odbiorcy) - do kasy stolicy jego krolestwa, reszta i bez stolicy - do skarbca krolestwa.</summary>
        public static void CourtierFin(HeroCtx __state)
        {
            if (__state == null) return;
            try
            {
                var h = __state.H;
                long paid = (long)__state.G0 - h.Gold;
                if (paid <= 0) return;
                _dN++; _dPaid += paid;
                if (!CourtierOn) { _dOffN++; _dOff += paid; return; }     // klucz wylaczony - jak dotad (zloto w nicosc)
                Kingdom k = null;
                try { k = h.Clan != null ? h.Clan.Kingdom : null; if (k == null) k = h.MapFaction as Kingdom; } catch { k = null; }
                if (k != null && k.IsEliminated) k = null;
                long rest = paid;
                var cap = k != null ? CapitalTreasury.CapitalOfKingdom(k) : null;
                if (cap != null && cap.Town != null)
                {
                    var t = cap.Town;
                    int had = t.Gold;
                    t.ChangeGold((int)Math.Min(int.MaxValue, rest));
                    long got = (long)t.Gold - had;
                    if (got > 0)
                    {
                        int g = (int)Math.Min(int.MaxValue, got);
                        MoneyLedger.Note169(MoneyLedger.N169Other, cap, g);   // "Przeplywy osad (przyczyny)": kasa osady poza tickiem - nazwane ("inne")
                        _dTown += got; _dTownN++; rest -= got;
                    }
                }
                if (rest > 0 && k != null)
                {
                    int x = (int)Math.Min(int.MaxValue, rest);
                    k.KingdomBudgetWallet += x;
                    CrownIncome.ShiftSnapshot(k, x);                          // 165: zapas korony, nie wplyw dnia (jak TownToll)
                    _dCrown += x; _dCrownN++; rest -= x;
                }
                if (rest > 0)
                {
                    h.ChangeHeroGold((int)Math.Min(int.MaxValue, rest));       // bez zywego krolestwa - zostaje u dworzanina (nic nie znika)
                    _dBack += rest; _dBackN++;
                }
            }
            catch (Exception e) { Stumble("CourtierFin", e); }
        }

        // ------------------------------------------------------------ 8. wolny majatek BK: EstateAction.TakeAction(Hero receiver) - Buy bez wlasciciela
        public static void VacantPre(object __instance, out VacCtx __state)
        {
            __state = null;
            try
            {
                if (!Live || __instance == null || _eaType == null) return;
                var t = _eaType.GetValue(__instance, null);
                if (t == null || t.ToString() != "Buy") return;
                var est = _eaEstate.GetValue(__instance, null);
                if (est == null || OwnerOf(est) != null) return;            // majatek z wlascicielem - BK placi wlascicielowi (GiveGoldAction)
                int cost = (int)_eaGold.GetValue(__instance, null);
                var h = _eaTaker.GetValue(__instance, null) as Hero;
                if (cost <= 0 || h == null) return;                          // wolny majatek we wsi wlasnego rodu - 50 wplywow, bez zlota
                var data = _estData.GetValue(est, null);
                var st = data != null ? _edSettl.GetValue(data, null) as Settlement : null;
                __state = new VacCtx { H = h, E = est, S = st, G0 = h.Gold, Cost = cost };
            }
            catch (Exception e) { __state = null; Stumble("VacantPre", e); }
        }

        /// <summary>Pan wsi wedlug BK (GetActualOwner - ten, ktory wolny majatek bierze za wplyw); zapasowo glowa rodu wlasciciela osady.</summary>
        private static Hero LordOf(Settlement st)
        {
            if (st == null) return null;
            Hero h = null;
            try { if (st.IsVillage && st.Village != null && _actualOwner != null) h = _actualOwner.Invoke(null, new object[] { st.Village }) as Hero; } catch { h = null; }
            if (h == null) { try { h = st.IsVillage ? (st.OwnerClan != null ? st.OwnerClan.Leader : null) : st.Owner; } catch { h = null; } }
            return h;
        }

        /// <summary>Finalizer: to, co BK zdjal kupcowi wolnego majatku (ChangeHeroGold(-GoldCost) bez odbiorcy) - panu wsi; bez pana - kasie miasta handlu wsi; inaczej zwrot.</summary>
        public static void VacantFin(VacCtx __state)
        {
            if (__state == null) return;
            try
            {
                var h = __state.H;
                long paid = Math.Min((long)__state.Cost, (long)__state.G0 - h.Gold);
                if (paid <= 0) return;
                _vN++; _vPaid += paid;
                if (h == Hero.MainHero) _vPlayer += paid;
                int day = (int)CampaignTime.Now.ToDays;
                string vname = __state.S != null ? __state.S.Name.ToString() : "the village";
                string where = __state.S != null ? __state.S.Name.ToString() + " (" + __state.S.StringId + ")" : "?";
                if (!VacantOn)
                {
                    _vOffN++; _vOff += paid;                                  // klucz wylaczony - jak dotad (zloto w nicosc)
                    Log.Info("Wolny majatek BK (323a): dzien " + day + " | kupiec " + h.Name + " | " + where + " | cena " + paid + " | KLUCZ WYLACZONY (Vacant Estate Price To Lord) - zloto w nicosc jak dotad.");
                    return;
                }
                int x = (int)Math.Min(int.MaxValue, paid);
                if (OwnerOf(__state.E) != h)
                {
                    // majatek nie przeszedl na kupca (wyjatek w BK po zaplacie) - zwrot kupcowi, nic nie znika
                    h.ChangeHeroGold(x);
                    _vUndo += paid; _vUndoN++;
                    Log.Info("Wolny majatek BK (323a): dzien " + day + " | kupiec " + h.Name + " | " + where + " | cena " + paid + " | majatek NIE przeszedl na kupca - zwrot " + paid + " kupcowi.");
                    return;
                }
                var lord = LordOf(__state.S);
                if (Takes(lord) && lord != h)
                {
                    lord.ChangeHeroGold(x);
                    CirculationWindows.NoteHeroGold(lord, x);                // bohater w swiecie - nic (jak CompFin); poza swiatem nie trafia (Takes)
                    _vLord += paid; _vLordN++;
                    Log.Info("Wolny majatek BK (323a): dzien " + day + " | kupiec " + h.Name + " | " + where + " | cena " + paid + " -> pan wsi " + lord.Name
                             + " (" + (lord.Clan != null ? lord.Clan.Name.ToString() : "-") + ") " + paid + " | kiesa kupca " + __state.G0 + " -> " + h.Gold + ".");
                    if (h == Hero.MainHero)
                        Log.Player("You paid " + paid + " denars for the vacant estate in " + vname + " to its lord, " + lord.Name + ".");
                    return;
                }
                long rest = paid;
                var tt = TradeTownOf(__state.S);
                if (tt != null && tt.Town != null)
                {
                    int had = tt.Town.Gold;
                    tt.Town.ChangeGold(x);
                    long got = (long)tt.Town.Gold - had;
                    if (got > 0)
                    {
                        MoneyLedger.Note169(MoneyLedger.N169Other, tt, (int)Math.Min(int.MaxValue, got));   // kasa osady poza tickiem - nazwane ("inne")
                        _vTown += got; _vTownN++; rest -= got;
                    }
                }
                if (rest > 0) { h.ChangeHeroGold((int)Math.Min(int.MaxValue, rest)); _vBack += rest; _vBackN++; }   // bez odbiorcy - wraca kupcowi (nic nie znika)
                Log.Info("Wolny majatek BK (323a): dzien " + day + " | kupiec " + h.Name + " | " + where + " | cena " + paid + " | bez zywego pana wsi (" + (lord != null ? lord.Name.ToString() : "brak")
                         + (lord == h ? " = kupiec" : "") + ") -> kasa miasta " + (tt != null ? tt.Name.ToString() : "brak") + " " + (paid - rest) + ", wrocilo kupcowi " + rest + ".");
                if (h == Hero.MainHero && paid - rest > 0 && tt != null)
                    Log.Player("You paid " + (paid - rest) + " denars for the vacant estate in " + vname + " to the treasury of " + tt.Name + ".");
            }
            catch (Exception e) { Stumble("VacantFin", e); }
        }

        // ------------------------------------------------------------ wpiecia (SubModuleMain - po SmallGold)
        private static void Patch(Harmony h, string label, MethodBase m, string pre, string fin)
        {
            try
            {
                if (h == null) { _missing.Add(label + " (brak Harmony)"); return; }
                if (m == null) { _missing.Add(label); return; }
                h.Patch(m, prefix: pre != null ? new HarmonyMethod(typeof(BkDipGold), pre) { priority = Priority.Normal } : null,
                           finalizer: fin != null ? new HarmonyMethod(typeof(BkDipGold), fin) { priority = Priority.High } : null);
                _wired.Add(label);
            }
            catch (Exception e) { _missing.Add(label + " (blad: " + e.Message + ")"); }
        }

        internal static void ApplyAll(Harmony h)
        {
            _wired.Clear(); _missing.Clear();
            var gb = AccessTools.TypeByName("BannerKings.Behaviours.BKGentryBehavior");
            var est = AccessTools.TypeByName("BannerKings.Managers.Populations.Estates.Estate");
            _estOwner = est != null ? AccessTools.Property(est, "Owner") : null;
            Patch(h, "nowy rod szlachty (BK BKGentryBehavior.CreateGentryClan)", gb != null && _estOwner != null ? AccessTools.Method(gb, "CreateGentryClan") : null, nameof(GentryPre), nameof(GentryFin));
            var bh = AccessTools.TypeByName("BannerKings.Components.BanditHeroComponent");
            Patch(h, "herszt (BK BanditHeroComponent.CreateParty)", bh != null ? AccessTools.Method(bh, "CreateParty") : null, nameof(HeroPre), nameof(HeroFin));
            Patch(h, "juki herszta (BK BanditHeroComponent.ConsiderLeaveHideout)", bh != null ? AccessTools.Method(bh, "ConsiderLeaveHideout", new[] { typeof(MobileParty) }) : null, nameof(PackPre), nameof(PackFin));
            var ft = AccessTools.TypeByName("BannerKings.Behaviours.Feasts.Feast");
            _feastHost = ft != null ? AccessTools.Property(ft, "Host") : null;
            _feastTown = ft != null ? AccessTools.Property(ft, "Town") : null;
            Patch(h, "uczta (BK Feast.Automanage)", ft != null && _feastHost != null && _feastTown != null ? AccessTools.Method(ft, "Automanage") : null, nameof(FeastPre), nameof(FeastFin));
            var gc = AccessTools.TypeByName("Diplomacy.Costs.GoldCost");
            _gcValue = gc != null ? AccessTools.Property(gc, "Value") : null;
            _gcGiver = gc != null ? AccessTools.Property(gc, "Giver") : null;
            _gcReceiver = gc != null ? AccessTools.Property(gc, "Receiver") : null;
            var nap = AccessTools.TypeByName("Diplomacy.DiplomaticAction.NonAggressionPact.FormNonAggressionPactAction");
            var peace = AccessTools.TypeByName("Diplomacy.DiplomaticAction.WarPeace.KingdomPeaceAction");
            var napM = nap != null ? AccessTools.Method(nap, "AssessCosts") : null;
            var peaceM = peace != null ? AccessTools.Method(peace, "AcceptPeace") : null;
            bool napOk = napM != null && napM.GetParameters().Length >= 2 && napM.GetParameters()[1].ParameterType == typeof(Kingdom);
            bool peaceOk = peaceM != null && peaceM.GetParameters().Length >= 2 && peaceM.GetParameters()[1].ParameterType == typeof(Kingdom);
            bool gcOk = gc != null && _gcValue != null && _gcGiver != null && _gcReceiver != null;
            Patch(h, "okno paktu o nieagresji (Diplomacy FormNonAggressionPactAction.AssessCosts)", gcOk && napOk ? napM : null, nameof(PactPre), nameof(PactFin));
            // recenzja: 6. parametr AcceptPeace - EliminationOnPeace (struktura; Harmony podaje ja jako object w pudelku), pole OtherKingdom
            var pp = peaceOk ? peaceM.GetParameters() : null;
            _elimOther = pp != null && pp.Length >= 6 && pp[5].ParameterType.IsValueType ? AccessTools.Field(pp[5].ParameterType, "OtherKingdom") : null;
            if (_elimOther != null && _elimOther.FieldType != typeof(bool)) _elimOther = null;
            Patch(h, "okno pokoju (Diplomacy KingdomPeaceAction.AcceptPeace)", gcOk && peaceOk ? peaceM : null, _elimOther != null ? nameof(PeacePre) : nameof(PactPre), nameof(PactFin));
            if (gcOk && peaceOk && _elimOther == null) _missing.Add("upadek drugiej strony przy pokoju (Diplomacy EliminationOnPeace.OtherKingdom)");
            Patch(h, "koszt bez odbiorcy (Diplomacy GoldCost.ApplyCost)", gcOk && (napOk || peaceOk) ? AccessTools.Method(gc, "ApplyCost") : null, nameof(CostPre), null);
            // f19-e1 (5): zakupy dworu BK - prywatna statyczna BuyGoods(ItemRoster stash, Town market, Hero buyer, ...); ta sama metoda co pomiar E8/P3 (Stage8World.BuyPre/BuyPost)
            var ba = AccessTools.TypeByName("BannerKings.Actions.BuyGoodsAction");
            var buy = ba != null ? AccessTools.DeclaredMethod(ba, "BuyGoods") : null;
            var bp = buy != null ? buy.GetParameters() : null;
            if (bp == null || bp.Length < 3 || bp[1].ParameterType != typeof(Town) || bp[2].ParameterType != typeof(Hero)) buy = null;
            Patch(h, "zakupy dworu (BK BuyGoodsAction.BuyGoods)", buy, nameof(ShopPre), nameof(ShopFin));
            // f19-e1 (6): ugoda z grupa interesu - lambda Demand.FinancialCompromise (ta sama metoda co pomiar E8/P3: Stage8World.FindCompromise - po IL)
            _demandT = AccessTools.TypeByName("BannerKings.Behaviours.Diplomacy.Groups.Demands.Demand");
            var dgT = AccessTools.TypeByName("BannerKings.Behaviours.Diplomacy.Groups.DiplomacyGroup");
            _dGroup = _demandT != null ? AccessTools.Property(_demandT, "Group") : null;
            _gLeader = dgT != null ? AccessTools.Property(dgT, "Leader") : null;
            _gMembers = dgT != null ? AccessTools.Property(dgT, "Members") : null;
            if (_gLeader != null && _gLeader.PropertyType != typeof(Hero)) _gLeader = null;
            MethodBase comp = null;
            if (_dGroup != null && _gLeader != null && _gMembers != null) { try { comp = Stage8World.FindCompromise(); } catch { comp = null; } }
            Patch(h, "ugoda z grupa interesu (BK lambda Demand.FinancialCompromise)", comp, nameof(CompPre), nameof(CompFin));
            // 322a (7): zloto dworzan - prywatna DrainGold(Hero) zadeklarowana w BKCourtierBehavior (DeclaredMethod; ta sama metoda co pomiar E8/P3 Stage8World.DrainPre)
            var cbT = AccessTools.TypeByName("BannerKings.Behaviours.BKCourtierBehavior");
            var drain = cbT != null ? AccessTools.DeclaredMethod(cbT, "DrainGold", new[] { typeof(Hero) }) : null;
            if (drain != null && (drain.IsStatic || drain.ReturnType != typeof(void))) drain = null;
            Patch(h, "zloto dworzan (BK BKCourtierBehavior.DrainGold)", drain, nameof(CourtierPre), nameof(CourtierFin));
            // 323a (8): wolny majatek - TakeAction zadeklarowana (override) w EstateAction (DeclaredMethod; metoda bazowa jest abstrakcyjna)
            var eaT = AccessTools.TypeByName("BannerKings.Managers.Populations.Estates.EstateAction");
            var edT = AccessTools.TypeByName("BannerKings.Managers.Populations.Estates.EstateData");
            var veT = AccessTools.TypeByName("BannerKings.Extensions.VillageExtensions");
            _eaType = eaT != null ? AccessTools.Property(eaT, "Type") : null;
            _eaEstate = eaT != null ? AccessTools.Property(eaT, "Estate") : null;
            _eaGold = eaT != null ? AccessTools.Property(eaT, "GoldCost") : null;
            _eaTaker = eaT != null ? AccessTools.Property(eaT, "ActionTaker") : null;
            _estData = est != null ? AccessTools.Property(est, "EstatesData") : null;
            _edSettl = edT != null ? AccessTools.Property(edT, "Settlement") : null;
            _actualOwner = veT != null ? AccessTools.DeclaredMethod(veT, "GetActualOwner", new[] { typeof(Village) }) : null;
            if (_actualOwner != null && _actualOwner.ReturnType != typeof(Hero)) _actualOwner = null;
            var take = eaT != null ? AccessTools.DeclaredMethod(eaT, "TakeAction", new[] { typeof(Hero) }) : null;
            bool vacOk = take != null && _eaType != null && _eaEstate != null && _eaGold != null && _eaGold.PropertyType == typeof(int) && _eaTaker != null
                         && _eaTaker.PropertyType == typeof(Hero) && _estOwner != null && _estData != null && _edSettl != null && _edSettl.PropertyType == typeof(Settlement);
            Patch(h, "wolny majatek (BK EstateAction.TakeAction)", vacOk ? take : null, nameof(VacantPre), nameof(VacantFin));
            if (vacOk && _actualOwner == null) _missing.Add("pan wsi wedlug BK (VillageExtensions.GetActualOwner) - zapasowo glowa rodu wlasciciela");
            if (vacOk && h != null)
            {
                try
                {
                    var pi = Harmony.GetPatchInfo(take);
                    if (pi != null)
                    {
                        var own = new List<string>();
                        foreach (var q in pi.Prefixes) if (q.owner != h.Id && !own.Contains(q.owner)) own.Add(q.owner);
                        foreach (var q in pi.Postfixes) if (q.owner != h.Id && !own.Contains(q.owner)) own.Add(q.owner);
                        foreach (var q in pi.Transpilers) if (q.owner != h.Id && !own.Contains(q.owner)) own.Add(q.owner);
                        foreach (var q in pi.Finalizers) if (q.owner != h.Id && !own.Contains(q.owner)) own.Add(q.owner);
                        if (own.Count > 0) Log.Info("BK i Diplomacy (E5/A8): wolny majatek (323a) - EstateAction.TakeAction latany tez przez: " + string.Join(", ", own.ToArray()) + ".");
                    }
                }
                catch { }
            }
            _wiredText = _wired.Count > 0 ? string.Join(", ", _wired.ToArray()) : "-";
            _missingText = _missing.Count > 0 ? string.Join(", ", _missing.ToArray()) : "-";
            Log.Info("BK i Diplomacy (E5/A8): latki przy starcie gry - wpiete: " + _wiredText + "; BRAK: " + _missingText + ".");
        }

        // ------------------------------------------------------------ linia doby (ArmouryBehavior - po SmallGold.Daily)
        internal static void Daily()
        {
            try
            {
                var s = Settings.Current;
                if (Campaign.Current == null || s == null || !s.LogEnabled) return;
                int day = (int)CampaignTime.Now.ToDays - 1;
                if (!_startLogged)
                {
                    _startLogged = true;
                    Log.Info("BK i Diplomacy (E5/A8): start | wylaczniki: Gentry Purse From Town " + (GentryOn ? "tak" : "nie") + " (start rodu: " + (GentryTownPays ? "z kasy miasta" : "0") + ")" + ", Bandit Hero Purse From Hideout " + (BanditOn ? "tak" : "nie")
                             + ", Feast Town Gets Paid " + (FeastOn ? "tak" : "nie") + ", Pact Cost To Other Realm " + (PactOn ? "tak" : "nie")
                             + ", Court Shopping Pays Town " + (CourtShopOn ? "tak" : "nie") + ", Group Compromise Pays Group " + (CompromiseOn ? "tak" : "nie")   // f19-e1
                             + ", Courtiers' Gold Stays In The Realm " + (CourtierOn ? "tak" : "nie")   // 322a
                             + ", Vacant Estate Price To Lord " + (VacantOn ? "tak" : "nie")   // 323a
                             + " | latki: " + _wiredText + "; BRAK: " + _missingText + ".");
                }
                var sb = new StringBuilder(900);
                sb.Append("BK i Diplomacy (E5/A8): dzien ").Append(day);
                sb.Append(" | nowe rody szlachty BK").Append(GentryOn ? "" : " WYLACZONE (Gentry Purse From Town - 15000 z niczego jak dotad)")
                  .Append(": ").Append(_gN).Append(", BK dalby ").Append(_gDue).Append(", z kas miast handlu wsi ").Append(_gPaid).Append(", uciete ").Append(_gDue - _gPaid)
                  .Append(" (kasa nie starczyla u ").Append(_gShortN).Append("); cena majatku bez odbiorcy niepowstala ").Append(_gCost).Append(" (").Append(_gCostN).Append(')');
                if (_gOwnedN > 0) sb.Append(", majatek z wlascicielem - cena z niczego jak dotad ").Append(_gOwnedCost).Append(" (").Append(_gOwnedN).Append(')');
                if (_gOffN > 0) sb.Append("; z niczego jak dotad ").Append(_gOff).Append(" u ").Append(_gOffN);
                sb.Append(" | herszt BK").Append(BanditOn ? "" : " WYLACZONE (Bandit Hero Purse From Hideout)").Append(": nowi ").Append(_hN).Append(", BK dalby ").Append(_hDue)
                  .Append(", z nadwyzki kryjowek ").Append(_hPaid).Append(", uciete ").Append(_hDue - _hPaid).Append(" (kryjowka nie starczyla u ").Append(_hShortN).Append(')');
                if (_hOffN > 0) sb.Append("; z niczego jak dotad ").Append(_hOff).Append(" u ").Append(_hOffN);
                sb.Append("; juki przy wyjsciu z kryjowki (BK sprzedawal z niczego): wyjsc ").Append(_pkLeft).Append(", ").Append(_pkUnits).Append(" szt. o wartosci ").Append(_pkValue)
                  .Append(" -> paser (kasy miast) kupil ").Append(_pkSold).Append(" szt. (z zywnoscia, gdy ja skupuje) za ").Append(_pkPaid).Append(", reszta w jukach (bron i sprzet jak u kazdej bandy); bez pasera ").Append(_pkNoFence);
                if (_pkOffN > 0) sb.Append("; sprzedane z niczego jak dotad ").Append(_pkOff).Append(" (").Append(_pkOffN).Append(')');
                sb.Append(" | uczty BK").Append(FeastOn ? "" : " WYLACZONE (Feast Town Gets Paid)").Append(": zakupow ").Append(_fN).Append(", gospodarze zaplacili ").Append(_fHostPaid)
                  .Append(", miasta dostalyby ").Append(_fTownGot).Append(", cofniete z kas miast ").Append(_fCut).Append(" (").Append(_fCutN).Append(')');
                if (_fOffN > 0) sb.Append("; z niczego jak dotad ").Append(_fOff).Append(" (").Append(_fOffN).Append(')');
                sb.Append(" | pakty Diplomacy (nieagresja, pokoj)").Append(PactOn ? "" : " WYLACZONE (Pact Cost To Other Realm - w nicosc jak dotad)").Append(": ").Append(_kN)
                  .Append(", koszt ").Append(_kDue).Append(", do skarbcow drugiej strony ").Append(_kPaid).Append(" (w tym gracz ").Append(_kPlayer).Append("), bez drugiej strony nie pobrane ")
                  .Append(_kNoRecv).Append(" (").Append(_kNoRecvN).Append("), w tym okna pokoju z upadkiem drugiej strony ").Append(_kFallsN);
                if (_kOffN > 0) sb.Append("; w nicosc jak dotad ").Append(_kOff).Append(" (").Append(_kOffN).Append(')');
                // f19-e1 (5, 6): po paktach, przed kontrola (zadna fraza wierszy E5/A8 sprawdzarki nie powtarza sie tu - "bez odbiorcy (klucz wylaczony)" zamiast "w nicosc jak dotad")
                sb.Append(" | zakupy dworu BK (BuyGoods)").Append(CourtShopOn ? "" : " WYLACZONE (Court Shopping Pays Town)").Append(": wywolan ").Append(_sCalls)
                  .Append(", glowy rodow zaplacily ").Append(_sPaid).Append(" (").Append(_sN).Append(" zakupow; gracz ").Append(_sPlayer).Append(") -> do kas miast targu ").Append(_sToTown).Append("; wywolan przy pustej kiesie glowy (BK bierze towar bez zaplaty - tylko licznik) ").Append(_sBroke)
                  .Append(", wywolan z kiesa glowy wyczerpana do 0 (budzet BK doszedl do 0 - dalsze pozycje bez limitu, towar ponad zaplate mozliwy - tylko licznik) ").Append(_sDrained);
                if (_sOffN > 0) sb.Append(", bez odbiorcy (klucz wylaczony) ").Append(_sOff).Append(" (").Append(_sOffN).Append(')');
                sb.Append(" | ugoda z grupa interesu BK (FinancialCompromise)").Append(CompromiseOn ? "" : " WYLACZONE (Group Compromise Pays Group)").Append(": ").Append(_cN)
                  .Append(", platnicy zaplacili ").Append(_cPaid).Append(" (gracz ").Append(_cPlayer).Append(") -> przywodcom grup ").Append(_cLeader).Append(", czlonkom ").Append(_cMembers)
                  .Append(" (").Append(_cMembersN).Append(" wyplat; gracz dostal ").Append(_cToPlayer).Append("), kasom siedzib platnika ").Append(_cSeat).Append(" (").Append(_cSeatN)
                  .Append("), zostalo u platnika ").Append(_cKept).Append(" (").Append(_cKeptN).Append("); grup bez przywodcy-odbiorcy (martwy, brak albo sam platnik) ").Append(_cNoLeaderN);
                if (_cOffN > 0) sb.Append(", bez odbiorcy (klucz wylaczony) ").Append(_cOff).Append(" (").Append(_cOffN).Append(')');
                // 322a (7): po ugodzie, przed kontrola; frazy inne niz w pomiarze E8/P3 ("(DrainGold) N razy / N zl") i w wierszach E5/A8 sprawdzarki
                sb.Append(" | kiesy dworzan BK (322a)").Append(CourtierOn ? "" : " WYLACZONE (Courtiers' Gold Stays In The Realm)").Append(": oproznionych ").Append(_dN)
                  .Append(", BK zdjal ").Append(_dPaid).Append(" -> kasy stolic ").Append(_dTown).Append(" (").Append(_dTownN).Append("), skarbce krolestw ").Append(_dCrown)
                  .Append(" (").Append(_dCrownN).Append("), wrocilo dworzanom ").Append(_dBack).Append(" (").Append(_dBackN).Append(')');
                if (_dOffN > 0) sb.Append(", zniklo (klucz wylaczony) ").Append(_dOff).Append(" (").Append(_dOffN).Append(')');
                // 323a (8): po dworzanach, przed kontrola; frazy wlasne (sprawdzarka --grupa majatek323a)
                sb.Append(" | wolne majatki BK (323a)").Append(VacantOn ? "" : " WYLACZONE (Vacant Estate Price To Lord)").Append(": kupionych ").Append(_vN)
                  .Append(", kupcy zaplacili ").Append(_vPaid).Append(" (gracz ").Append(_vPlayer).Append(") -> panom wsi ").Append(_vLord).Append(" (").Append(_vLordN)
                  .Append("), kasom miast handlu wsi ").Append(_vTown).Append(" (").Append(_vTownN).Append("), wrocilo kupcom ").Append(_vBack + _vUndo).Append(" (").Append(_vBackN + _vUndoN)
                  .Append("; w tym majatek nie przeszedl ").Append(_vUndoN).Append(')');
                if (_vOffN > 0) sb.Append(", przepadlo (klucz wylaczony) ").Append(_vOff).Append(" (").Append(_vOffN).Append(')');
                sb.Append(" | kontrola (prog 0): stos paktow przepelniony ").Append(_overflow).Append(", otwarte okna paktow ").Append(_pactDepth);
                if (_stumbles > 0) sb.Append(" | potkniecia ").Append(_stumbles).Append(" (pierwszy wyjatek z miejsca w logu jako ERROR)");
                Stage8World.BkSegment(sb);   // E8/P3 (245): dopisek pomiaru - zywnosc BK dla band hersztow i ujscia BK bez odbiorcy (tylko log; klucz Stage8WorldLog wylaczony = linia jak dotad)
                sb.Append('.');
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Stumble("Daily", e); }
            finally { ClearDay(); }
        }
    }
}
