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
    ///    `ChangeHeroGold(15 000)` z niczego. Teraz: (a) cena majatku = 0 (prefiks, `ref cost`) - majatek przechodzi bez ceny, jak w praktyce zawsze (BK placil
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
    /// Wyjatek per zdarzenie - licznik potkniec, bez globalnego wylacznika. Nic do zapisu (stan doby od zera po wczytaniu).
    /// </summary>
    internal static class BkDipGold
    {
        internal static bool GentryOn { get { var s = Settings.Current; return s != null && s.GentryPurseFromTown; } }
        internal static bool BanditOn { get { var s = Settings.Current; return s != null && s.BanditHeroPurseFromHideout; } }
        internal static bool FeastOn { get { var s = Settings.Current; return s != null && s.FeastTownGetsPaid; } }
        internal static bool PactOn { get { var s = Settings.Current; return s != null && s.PactCostToOtherRealm; } }
        private static bool Live { get { var c = Campaign.Current; return c != null && c.GameStarted; } }

        private const int KGentryGift = 15000;      // BK BKGentryBehavior.cs:522
        private const int KBanditGift = 10000;      // BK BanditHeroComponent.cs:218

        internal sealed class GentryCtx { public Settlement S; public object Estate; public Hero Owner0; public bool Off; }
        internal sealed class HeroCtx { public Hero H; public int G0; }
        internal sealed class PackCtx { public List<KeyValuePair<EquipmentElement, int>> Stash; public int G0; public bool Off; }
        internal sealed class FeastCtx { public Hero H; public Town T; public long H0, T0; }

        // okna paktow Diplomacy: druga strona traktatu
        private static readonly Kingdom[] _pact = new Kingdom[4];
        private static int _pactDepth;

        // odczyt typow BK i Diplomacy (bez referencji kompilacji)
        private static PropertyInfo _estOwner, _feastHost, _feastTown, _gcValue, _gcGiver, _gcReceiver;
        private static FieldInfo _elimOther;        // Diplomacy EliminationOnPeace.OtherKingdom (recenzja)

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
                var payer = TradeTownOf(__state.S);
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
            }
            catch (Exception e) { __state = null; Stumble("FeastPre", e); }
        }

        public static void FeastFin(FeastCtx __state)
        {
            if (__state == null) return;
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
                    Log.Info("BK i Diplomacy (E5/A8): start | wylaczniki: Gentry Purse From Town " + (GentryOn ? "tak" : "nie") + ", Bandit Hero Purse From Hideout " + (BanditOn ? "tak" : "nie")
                             + ", Feast Town Gets Paid " + (FeastOn ? "tak" : "nie") + ", Pact Cost To Other Realm " + (PactOn ? "tak" : "nie") + " | latki: " + _wiredText + "; BRAK: " + _missingText + ".");
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
