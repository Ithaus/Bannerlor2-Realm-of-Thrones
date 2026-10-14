using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// E8/P3 (245, PLAN 8.15, 8.12c; stan "przed" dla E8/G1 i E8/G2) - POMIARY SWIATA. Sam log: zadnego zlota, towaru, ludzi, rozkazu ani warunku gry
    /// (latki tylko czytaja stan przed i po cudzej metodzie; wynik oryginalu i kolejnosc bez zmian). Wylacznik Stage8WorldLog (wylaczony = zadnej linii,
    /// latki koncza na pierwszym porownaniu). Stan tylko w pamieci, czyszczony w Reset (z WorldMeasure.Reset - konstruktor ArmouryBehavior).
    ///
    ///  1. PRODUKCJA WSI POD NavalDLC (raz na sesje, pierwszy takt doby): lancuch modeli VillageProductionCalculatorModel od czynnego w dol
    ///     (MBGameModel.BaseModel - wlasciwosc chroniona, refleksja) i iloraz wynik czynnego / wynik modelu pod nim dla 10 najliczniejszych typow wsi
    ///     (jedna wies Normal na typ, towar o najwyzszej stawce, bez wierzchowcow - model gry losuje przy koniach t2 z perkiem Shepherd).
    ///     Linia "Produkcja wsi - model (E8/P3)".
    ///  2. KARA "UNDEREQUIPPED" DTE: PartyEquipmentDistributor.MarkUnderEquippedAssignmentsIfEnabled daje kazdemu przydzialowi kare 2 x brakujace tiery
    ///     (najwyzej 20), ale SpawnAgentPatch.Postfix NAKLADA ja na morale tylko ludziom druzyny gracza (IsMainParty) - u pozostalych partii przydzial jest
    ///     tylko oznaczony. Postfiks DynamicTroopMissionLogic.RegisterSpawnedAgentAssignment (kazdy wystawiony z przydzialem) liczy kare 0 / 2-10 / 12-20
    ///     u druzyny gracza, sojusznikow i przeciwnika; postfiks OnEndMission pisze linie "Underequipped (E8/P3)" (tylko bitwy gracza - misje).
    ///  3. OWCE: wsie typu sheep_farm - owce i welna w magazynach i na wozach, produkcja wedlug czynnego modelu (owca i welna to nie wierzchowce - bez
    ///     losowania), utarg wozow tych wsi z ramki sprzedazy wozu ksiegi towarow (GoodsLedger FWagon - ten sam podzial utargu co ksiega). Linia "Owce (E8/P3)".
    ///  4. POSTOJ KARAWAN AI W MIASTACH: sluchacze SettlementEntered / OnSettlementLeftEvent zapamietuja godzine wjazdu; wyjazd = postoj w godzinach
    ///     (srednia, mediana, p90, rozklad), do tego karawany stojace teraz. Ta sama populacja co dlawik BKROT (IsCaravan, nie rod gracza). Pamiec wjazdow
    ///     zeruje sie przy wczytaniu (wyjazd bez wjazdu - osobny licznik). Linia "Postoj karawan (E8/P3)".
    ///  5. NAJEMNICY BK: licznik w jedynym prefiksie na BK ConvertTroopsMercenaries (SpoilsCompany.BkConvertPrefix / Postfix wola MercBefore / MercAfter dla
    ///     klanow najemnikow innych niz klan Spoils): bohaterowie zamienieni na zolnierza szablonu (TotalHeroes przed - po) i ludzie dosypani (TotalManCount
    ///     po - przed). Wolajacy BK (DailyClanTickImpl) wychodzi przy Kingdom == null, wiec galaz dosypki powinna dac 0 - linia to pokaze.
    ///     Linia "Najemnicy BK (E8/P3)".
    ///  6. ZRODLA I UJSCIA BK BEZ PLATNIKA (dopisek do linii "BK i Diplomacy (E5/A8)" - BkDipGold.Daily wola BkSegment): zywnosc BannerKingsComponent.GiveFood
    ///     dla band hersztow (BanditHeroComponent; sztuki i wartosc z roznicy jukow), ujscia zlota: lambda Demand.FinancialCompromise (szukana po IL -
    ///     jedyna metoda "FinancialCompromise" (Hero) -> bool z wywolaniem ChangeHeroGold), BKCourtierBehavior.DrainGold, BuyGoodsAction.BuyGoods
    ///     (kupujacy placi w nicosc, towar z polki miasta do schowka).
    ///  7. WSIE BEZ WOZU: VillageClogDiag.Daily podaje liczby (zatkane Normal bez wozu ponad 7 dob) - linia "Wsie bez wozu (E8/P3)" (prog P3-2, zamyka 8.12c).
    ///  8. ROZKAZY PARTII ZE STATKAMI PO SWICIE: NightRest.RememberOrder zapamietuje tylko zachowanie i cel; swit oddaje rozkaz droga ladowa bez portu
    ///     (NightOrder.Nav = Default). Licznik przy zasnieciu: partie ze statkami, ktorych rozkaz przed snem mial inna droge albo port. Linia "Rozkazy morskie (E8/P3)".
    /// Wyjatek w jednym pomiarze - licznik potkniec (pierwszy z miejsca jako ERROR), reszta liczy dalej; zadnego globalnego wylacznika.
    /// </summary>
    internal static class Stage8World
    {
        internal static bool On { get { var s = Settings.Current; return s != null && s.Stage8WorldLog; } }

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private const string SheepType = "sheep_farm";
        private const double IdleHours = 168.0;        // 7 dob postoju (jak IslandRoads.IdleDays)
        private const double CartDays = 7.0;           // prog P3-2: wies Normal bez wozu ponad 7 dob

        private static int _stumbles, _stumblesDay;
        private static readonly HashSet<string> _errSites = new HashSet<string>();
        private static readonly List<string> _wired = new List<string>(), _missing = new List<string>();
        private static int _wiredN, _wantedN;

        private static void Stumble(string where, Exception e)
        {
            _stumbles++; _stumblesDay++;
            try { if (_errSites.Add(where)) Log.Error("Stage8World." + where, e); } catch { }
        }

        private static string F1(double v) { return v.ToString("0.0", Inv); }
        private static string F2(double v) { return v.ToString("0.00", Inv); }

        // ------------------------------------------------------------ stan
        private static bool _modelDone;

        // 2. Underequipped: [druzyna gracza, sojusznicy, przeciwnik][kara 0, 2-10, 12-20]
        private static MethodInfo _uePenalty;
        private static readonly int[,] _ue = new int[3, 3];
        private static readonly double[] _ueSum = new double[3];
        private static int _ueNoParty;
        private static bool _ueAny;

        // 3. owce
        private static ItemObject _sheep, _wool, _meat;
        private static bool _itemsSought;
        private static long _shWoolU, _shWoolG, _shSheepU, _shSheepG, _shMeatU, _shMeatG, _shOtherU, _shOtherG, _shGold;
        private static int _shSales;

        // 4. postoj karawan
        private struct Stay { public double T0; public Settlement At; }
        private static readonly Dictionary<MobileParty, Stay> _carIn = new Dictionary<MobileParty, Stay>();
        private static readonly List<float> _stays = new List<float>();
        private static readonly int[] _stayBin = new int[6];
        private static int _carEnter, _carLeaveUnknown;

        // 5. najemnicy BK
        private static Clan _mcClan;
        private static int _mcHeroes, _mcMen;
        private static bool _mcKingdom;
        private static int _mcCalls, _mcCallsKingdom, _mcHeroesOut, _mcMenAdded;
        private static readonly Dictionary<string, int[]> _mcBy = new Dictionary<string, int[]>();   // klan -> [wywolan, bohaterow zamienionych, ludzi dosypanych]

        // 6. zrodla i ujscia BK
        private static int _bfU0;
        private static long _bfV0;
        private static int _bfN, _bfOther;
        private static long _bfUnits, _bfValue;
        private static int _fcN, _drN, _bgN, _bgCalls;
        private static long _fcGold, _fcPlayer, _drGold, _bgGold, _bgPlayer;

        // 8. rozkazy partii ze statkami
        private static int _ordAll, _ordShips, _ordGiven, _ordSame, _ordDiff, _ordNavAll, _ordNavNaval, _ordNavNone, _ordPort, _ordAtSea, _ordAi;

        /// <summary>Nowa kampania i kazde wczytanie (WorldMeasure.Reset - konstruktor ArmouryBehavior).</summary>
        internal static void Reset()
        {
            _modelDone = false;
            _stumbles = 0; _stumblesDay = 0; _errSites.Clear();
            ClearUnder();
            _itemsSought = false; _sheep = _wool = _meat = null;
            ClearSheep();
            _carIn.Clear(); ClearCaravans();
            _mcClan = null; ClearMerc();
            ClearBk();
            ClearOrders();
        }

        private static void ClearUnder() { Array.Clear(_ue, 0, _ue.Length); Array.Clear(_ueSum, 0, _ueSum.Length); _ueNoParty = 0; _ueAny = false; }
        private static void ClearSheep() { _shWoolU = _shWoolG = _shSheepU = _shSheepG = _shMeatU = _shMeatG = _shOtherU = _shOtherG = _shGold = 0; _shSales = 0; }
        private static void ClearCaravans() { _stays.Clear(); Array.Clear(_stayBin, 0, _stayBin.Length); _carEnter = 0; _carLeaveUnknown = 0; }
        private static void ClearMerc() { _mcCalls = _mcCallsKingdom = _mcHeroesOut = _mcMenAdded = 0; _mcBy.Clear(); }
        private static void ClearBk() { _bfN = _bfOther = 0; _bfUnits = _bfValue = 0; _fcN = _drN = _bgN = _bgCalls = 0; _fcGold = _fcPlayer = _drGold = _bgGold = _bgPlayer = 0; }
        private static void ClearOrders() { _ordAll = _ordShips = _ordGiven = _ordSame = _ordDiff = _ordNavAll = _ordNavNaval = _ordNavNone = _ordPort = _ordAtSea = _ordAi = 0; }

        // ------------------------------------------------------------ 1. produkcja wsi: lancuch modeli i iloraz wynik / baza

        private static object BaseOf(object model)
        {
            if (model == null) return null;
            try { var bp = AccessTools.Property(model.GetType(), "BaseModel"); return bp != null ? bp.GetValue(model, null) : null; }
            catch { return null; }
        }

        private static void ModelLine(int day)
        {
            var active = Campaign.Current.Models.VillageProductionCalculatorModel;
            var chain = new List<VillageProductionCalculatorModel>();
            object m = active;
            for (int guard = 0; m != null && guard < 8; guard++)
            {
                var vm = m as VillageProductionCalculatorModel;
                if (vm == null || chain.Contains(vm)) break;
                chain.Add(vm);
                m = BaseOf(m);
            }
            var sb = new StringBuilder(900);
            sb.Append("Produkcja wsi - model (E8/P3): dzien ").Append(day).Append(" | lancuch modeli (czynny -> pod nim): ");
            if (chain.Count == 0) sb.Append("brak");
            for (int i = 0; i < chain.Count; i++) sb.Append(i > 0 ? " -> " : "").Append(chain[i].GetType().Name);
            var under = chain.Count > 1 ? chain[1] : null;
            sb.Append(" | pod czynnym (BaseModel): ").Append(under != null ? under.GetType().FullName : "brak");
            if (active == null) { sb.Append('.'); Log.Info(sb.ToString()); return; }

            // 10 najliczniejszych typow wsi; na typ jedna wies Normal z miastem handlu i wlascicielem
            var count = new Dictionary<VillageType, int>();
            var sample = new Dictionary<VillageType, Village>();
            foreach (var v in Village.All)
            {
                if (v == null || v.VillageType == null || v.Settlement == null || v.Bound == null) continue;
                int c; count.TryGetValue(v.VillageType, out c); count[v.VillageType] = c + 1;
                if (!sample.ContainsKey(v.VillageType) && v.VillageState == Village.VillageStates.Normal && v.TradeBound != null && v.Settlement.OwnerClan != null && v.Settlement.OwnerClan.Leader != null)
                    sample[v.VillageType] = v;
            }
            var types = new List<KeyValuePair<VillageType, int>>(count);
            types.Sort((a, b) => a.Value != b.Value ? b.Value.CompareTo(a.Value) : string.CompareOrdinal(a.Key.StringId, b.Key.StringId));
            sb.Append(" | typ wsi (liczba wsi) towar: czynny / pod nim = iloraz, bez mnoznikow Armoury (nasz postfiks pominiety) -");
            int shown = 0; double rSum = 0; int rN = 0;
            // nasz ProdPostfix ma pamiec (W2: zboze / ryby wiosek pobocznych; ksiega rudy) - na czas pytania modelu jest pomijany, pomiar niczego nie rusza
            MaterialLaw.ProbeBegin();
            try
            {
                for (int i = 0; i < types.Count && shown < 10; i++)
                {
                    var t = types[i].Key;
                    try
                    {
                        Village v;
                        ItemObject item = null; float rate = 0f;
                        if (t.Productions != null)
                            foreach (var pr in t.Productions)
                                if (pr.Item1 != null && !pr.Item1.IsMountable && pr.Item2 > rate) { item = pr.Item1; rate = pr.Item2; }
                        sb.Append(shown > 0 ? ";" : "").Append(' ').Append(t.StringId).Append(" (").Append(types[i].Value).Append(") ");
                        shown++;
                        if (item == null) { sb.Append("bez towaru poza wierzchowcami"); continue; }
                        if (!sample.TryGetValue(t, out v)) { sb.Append(item.StringId).Append(": brak wsi Normal"); continue; }
                        float top = active.CalculateDailyProductionAmount(v, item).ResultNumber;
                        sb.Append(item.StringId).Append(": ").Append(F2(top));
                        if (under == null) continue;
                        float low = under.CalculateDailyProductionAmount(v, item).ResultNumber;
                        sb.Append(" / ").Append(F2(low)).Append(" = ").Append(low > 0f ? F2(top / low) : "-");
                        if (low > 0f) { rSum += top / low; rN++; }
                    }
                    catch (Exception e) { sb.Append("blad"); Stumble("ModelLine(typ)", e); }
                }
            }
            finally { MaterialLaw.ProbeEnd(); }
            if (shown == 0) sb.Append(" brak wsi");
            sb.Append(" | iloraz sredni ").Append(rN > 0 ? F2(rSum / rN) : "-").Append(" z ").Append(rN).Append(" typow.");
            Log.Info(sb.ToString());
        }

        // ------------------------------------------------------------ 2. kara "Underequipped" DTE

        /// <summary>Postfiks DTE DynamicTroopMissionLogic.RegisterSpawnedAgentAssignment(Agent, Assignment): kara przydzialu wystawionego czlowieka.</summary>
        public static void UnderRegPostfix(Agent __0, object __1)
        {
            try
            {
                if (__0 == null || __1 == null || _uePenalty == null || !On) return;
                float p = (float)_uePenalty.Invoke(__1, null);
                var org = __0.Origin;
                var pb = org != null ? org.BattleCombatant as PartyBase : null;
                var main = PartyBase.MainParty;
                if (pb == null || main == null) { _ueNoParty++; _ueAny = true; return; }
                int g = pb == main ? 0 : (pb.MapEventSide != null && pb.MapEventSide == main.MapEventSide) ? 1 : 2;
                _ue[g, p <= 0f ? 0 : p <= 10f ? 1 : 2]++;
                _ueSum[g] += p;
                _ueAny = true;
            }
            catch (Exception e) { Stumble("UnderReg", e); }
        }

        /// <summary>Postfiks DTE DynamicTroopMissionLogic.OnEndMission: linia misji i zerowanie.</summary>
        public static void UnderEndPostfix()
        {
            if (!_ueAny) return;
            try
            {
                var sb = new StringBuilder(500);
                sb.Append("Underequipped (E8/P3): dzien ").Append((int)CampaignTime.Now.ToDays).Append(", koniec misji");
                string[] who = { "druzyna gracza (DTE naklada kare na morale)", "sojusznicy (DTE tylko oznacza - kary nie naklada)", "przeciwnik (DTE tylko oznacza - kary nie naklada)" };
                for (int g = 0; g < 3; g++)
                {
                    int n = _ue[g, 0] + _ue[g, 1] + _ue[g, 2];
                    sb.Append(" | ").Append(who[g]).Append(": wystawieni ").Append(n).Append(", kara 0: ").Append(_ue[g, 0]).Append(", 2-10: ").Append(_ue[g, 1])
                      .Append(", 12-20: ").Append(_ue[g, 2]).Append(", srednia kara ").Append(n > 0 ? F1(_ueSum[g] / n) : "-");
                }
                sb.Append(" | bez partii w pochodzeniu ").Append(_ueNoParty).Append('.');
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Stumble("UnderEnd", e); }
            finally { ClearUnder(); }
        }

        // ------------------------------------------------------------ 3. owce

        private static void EnsureItems()
        {
            if (_itemsSought) return;
            var om = MBObjectManager.Instance;
            if (om == null) return;
            _itemsSought = true;
            _sheep = om.GetObject<ItemObject>("sheep");
            _wool = om.GetObject<ItemObject>("wool");
            _meat = om.GetObject<ItemObject>("meat");
        }

        private static bool IsSheepVillage(Village v)
        {
            var t = v != null ? v.VillageType : null;
            return t != null && t.StringId == SheepType;
        }

        /// <summary>GoodsLedger, ramka sprzedazy wozu (FWagon): czy to woz wsi owczej; liczy sprzedaz i utarg tej wizyty (tylko licznik).</summary>
        internal static bool WagonSale(MobileParty mp, long gold)
        {
            try
            {
                if (mp == null || !On) return false;
                var hs = mp.HomeSettlement;
                if (hs == null || !IsSheepVillage(hs.Village)) return false;
                if (gold > 0) { _shSales++; _shGold += gold; }
                return true;
            }
            catch (Exception e) { Stumble("WagonSale", e); return false; }
        }

        /// <summary>GoodsLedger, ramka sprzedazy wozu wsi owczej: sztuki towaru i jego czesc utargu - ten sam podzial co ksiega (sztuki x cena pierwszej sztuki).</summary>
        internal static void WagonItem(ItemObject it, int sold, int price, long gold, double weight, double units)
        {
            try
            {
                if (it == null || sold <= 0) return;
                long share = 0;
                if (gold > 0 && weight > 0 && price > 0) share = (long)Math.Round(gold * ((double)sold * price / weight));
                else if (gold > 0 && weight <= 0 && units > 0) share = (long)Math.Round(gold * (sold / units));
                EnsureItems();
                if (ReferenceEquals(it, _wool)) { _shWoolU += sold; _shWoolG += share; }
                else if (ReferenceEquals(it, _sheep)) { _shSheepU += sold; _shSheepG += share; }
                else if (ReferenceEquals(it, _meat)) { _shMeatU += sold; _shMeatG += share; }
                else { _shOtherU += sold; _shOtherG += share; }
            }
            catch (Exception e) { Stumble("WagonItem", e); }
        }

        private static void SheepLine(int day)
        {
            try
            {
                EnsureItems();
                var model = Campaign.Current.Models.VillageProductionCalculatorModel;
                int n = 0, normal = 0, bad = 0;
                long herd = 0, woolStock = 0, cartSheep = 0, cartWool = 0;
                double mSheep = 0, mWool = 0;
                foreach (var v in Village.All)
                {
                    try
                    {
                        if (!IsSheepVillage(v) || v.Settlement == null) continue;
                        n++;
                        if (v.VillageState == Village.VillageStates.Normal) normal++;
                        var r = v.Settlement.ItemRoster;
                        if (r != null)
                        {
                            if (_sheep != null) herd += r.GetItemNumber(_sheep);
                            if (_wool != null) woolStock += r.GetItemNumber(_wool);
                        }
                        var cart = v.VillagerPartyComponent != null ? v.VillagerPartyComponent.MobileParty : null;
                        var cr = cart != null ? cart.ItemRoster : null;
                        if (cr != null)
                        {
                            if (_sheep != null) cartSheep += cr.GetItemNumber(_sheep);
                            if (_wool != null) cartWool += cr.GetItemNumber(_wool);
                        }
                        if (model != null && v.Bound != null && v.Settlement.OwnerClan != null && v.Settlement.OwnerClan.Leader != null)
                        {
                            if (_sheep != null) mSheep += model.CalculateDailyProductionAmount(v, _sheep).ResultNumber;
                            if (_wool != null) mWool += model.CalculateDailyProductionAmount(v, _wool).ResultNumber;
                        }
                    }
                    catch (Exception e) { bad++; Stumble("SheepLine(wies)", e); }
                }
                var s = Settings.Current;
                bool ledger = s != null && s.GoodsLedgerEnabled;
                var sb = new StringBuilder(600);
                sb.Append("Owce (E8/P3): dzien ").Append(day).Append(" | wsi owczych (").Append(SheepType).Append(") ").Append(n).Append(", w stanie Normal ").Append(normal)
                  .Append(" | stada: owiec w magazynach wsi ").Append(herd).Append(" szt., na wozach tych wsi ").Append(cartSheep).Append(" szt.")
                  .Append(" | welna: w magazynach wsi ").Append(woolStock).Append(" szt., na wozach ").Append(cartWool).Append(" szt.")
                  .Append(" | produkcja wedlug czynnego modelu: owce ").Append(F1(mSheep)).Append(" szt./d, welna ").Append(F1(mWool)).Append(" szt./d")
                  .Append(" | utarg wozow wsi owczych tej doby (").Append(ledger ? "z ramek ksiegi towarow" : "ksiega towarow WYLACZONA - brak danych").Append("): sprzedazy ").Append(_shSales)
                  .Append(", razem ").Append(_shGold).Append(" zl; welna ").Append(_shWoolU).Append(" szt. / ").Append(_shWoolG).Append(" zl, owce ").Append(_shSheepU).Append(" szt. / ").Append(_shSheepG)
                  .Append(" zl, mieso ").Append(_shMeatU).Append(" szt. / ").Append(_shMeatG).Append(" zl, inne towary ").Append(_shOtherU).Append(" szt. / ").Append(_shOtherG).Append(" zl")
                  .Append(" | wsie z bledem ").Append(bad).Append('.');
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Stumble("SheepLine", e); }
            finally { ClearSheep(); }
        }

        // ------------------------------------------------------------ 4. postoj karawan AI w miastach

        /// <summary>Ta sama populacja co dlawik BKROT (BKCaravansHourlyThrottlePatch.Prefix): karawana, nie rod gracza.</summary>
        private static bool AiCaravan(MobileParty mp) { return mp.IsCaravan && mp.ActualClan != Clan.PlayerClan; }

        /// <summary>Sluchacz SettlementEntered: godzina wjazdu karawany AI do miasta (tylko pamiec).</summary>
        internal static void OnEntered(MobileParty mp, Settlement st, Hero h)
        {
            if (mp == null || st == null || !mp.IsCaravan || !st.IsTown) return;
            try
            {
                if (!On || !AiCaravan(mp)) return;
                _carEnter++;
                _carIn[mp] = new Stay { T0 = CampaignTime.Now.ToHours, At = st };
            }
            catch (Exception e) { Stumble("OnEntered", e); }
        }

        /// <summary>Sluchacz OnSettlementLeftEvent: postoj karawany AI w miescie w godzinach (tylko licznik).</summary>
        internal static void OnLeft(MobileParty mp, Settlement st)
        {
            if (mp == null || st == null || !mp.IsCaravan || !st.IsTown) return;
            try
            {
                if (!On || !AiCaravan(mp)) return;
                Stay s;
                bool known = _carIn.TryGetValue(mp, out s);
                if (known) _carIn.Remove(mp);
                if (!known || s.At != st) { _carLeaveUnknown++; return; }
                float hrs = (float)Math.Max(0.0, CampaignTime.Now.ToHours - s.T0);
                _stays.Add(hrs);
                _stayBin[hrs <= 3f ? 0 : hrs <= 12f ? 1 : hrs <= 24f ? 2 : hrs <= 72f ? 3 : hrs <= 168f ? 4 : 5]++;
            }
            catch (Exception e) { Stumble("OnLeft", e); }
        }

        private static void CaravanLine(int day)
        {
            try
            {
                double now = CampaignTime.Now.ToHours;
                int standing = 0, idle = 0, swept = 0;
                double sum = 0, max = 0;
                List<MobileParty> gone = null;
                foreach (var kv in _carIn)
                {
                    bool ok = false;
                    try { var c = kv.Key; ok = c != null && c.IsActive && c.CurrentSettlement == kv.Value.At; } catch { }
                    if (!ok) { if (gone == null) gone = new List<MobileParty>(); gone.Add(kv.Key); continue; }
                    double d = Math.Max(0.0, now - kv.Value.T0);
                    standing++; sum += d; if (d > max) max = d; if (d >= IdleHours) idle++;
                }
                if (gone != null) { swept = gone.Count; foreach (var k in gone) _carIn.Remove(k); }
                int world = 0, inTown = 0;
                var all = MobileParty.AllCaravanParties;
                if (all != null)
                    foreach (var c in all)
                    {
                        if (c == null || !c.IsActive || !AiCaravan(c)) continue;
                        world++;
                        var at = c.CurrentSettlement;
                        if (at != null && at.IsTown) inTown++;
                    }
                int left = _stays.Count;
                string mean = "-", med = "-", p90 = "-", top = "-";
                if (left > 0)
                {
                    _stays.Sort();
                    double t = 0; foreach (var x in _stays) t += x;
                    mean = F1(t / left); med = F1(_stays[left / 2]); p90 = F1(_stays[Math.Min(left - 1, (int)(left * 0.9))]); top = F1(_stays[left - 1]);
                }
                var sb = new StringBuilder(600);
                sb.Append("Postoj karawan (E8/P3): dzien ").Append(day).Append(" | karawany AI swiata ").Append(world).Append(", w miastach teraz ").Append(inTown)
                  .Append(" (z wjazdem w pamieci ").Append(standing).Append(": stoja srednio ").Append(standing > 0 ? F1(sum / standing) : "-").Append(" h, najdluzej ")
                  .Append(standing > 0 ? F1(max) : "-").Append(" h, 7+ dob ").Append(idle).Append(")")
                  .Append(" | wjazdy do miast tej doby ").Append(_carEnter)
                  .Append(" | wyjazdy z miast tej doby ").Append(left).Append(": postoj srednio ").Append(mean).Append(" h, mediana ").Append(med).Append(" h, p90 ").Append(p90)
                  .Append(" h, najdluzszy ").Append(top).Append(" h; rozklad <=3 h ").Append(_stayBin[0]).Append(", 3-12 h ").Append(_stayBin[1]).Append(", 12-24 h ").Append(_stayBin[2])
                  .Append(", 1-3 doby ").Append(_stayBin[3]).Append(", 3-7 dob ").Append(_stayBin[4]).Append(", 7+ dob ").Append(_stayBin[5])
                  .Append(" | wyjazdy bez wjazdu w pamieci (stan sprzed wczytania, nowa karawana) ").Append(_carLeaveUnknown)
                  .Append(" | wpisy sprzatniete (karawana znikla w miescie) ").Append(swept).Append('.');
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Stumble("CaravanLine", e); }
            finally { ClearCaravans(); }
        }

        // ------------------------------------------------------------ 5. najemnicy BK (licznik w prefiksie SpoilsCompany na ConvertTroopsMercenaries)

        private static void Count(Clan c, out int heroes, out int men, out int parties)
        {
            heroes = 0; men = 0; parties = 0;
            var w = c != null ? c.WarPartyComponents : null;
            if (w == null) return;
            foreach (var wp in w)
            {
                var mp = wp != null ? wp.MobileParty : null;
                var r = mp != null ? mp.MemberRoster : null;
                if (r == null) continue;
                parties++; heroes += r.TotalHeroes; men += r.TotalManCount;
            }
        }

        /// <summary>Z SpoilsCompany.BkConvertPrefix (klan inny niz klan Spoils): stan druzyn klanu najemnikow przed zamiana BK. Tylko odczyt.</summary>
        internal static void MercBefore(Clan clan)
        {
            _mcClan = null;
            try
            {
                if (clan == null || !On || !clan.IsClanTypeMercenary) return;
                int p;
                Count(clan, out _mcHeroes, out _mcMen, out p);
                _mcKingdom = clan.Kingdom != null;
                _mcClan = clan;
            }
            catch (Exception e) { _mcClan = null; Stumble("MercBefore", e); }
        }

        /// <summary>Z SpoilsCompany.BkConvertPostfix: ilu bohaterow BK zamienil na zolnierza szablonu i ilu ludzi dosypal. Tylko licznik.</summary>
        internal static void MercAfter(Clan clan)
        {
            var c = _mcClan;
            _mcClan = null;
            if (c == null || !ReferenceEquals(c, clan)) return;
            try
            {
                int h, m, p;
                Count(clan, out h, out m, out p);
                int outH = Math.Max(0, _mcHeroes - h), added = Math.Max(0, m - _mcMen);
                _mcCalls++; if (_mcKingdom) _mcCallsKingdom++;
                _mcHeroesOut += outH; _mcMenAdded += added;
                string id = clan.StringId ?? "?";
                int[] a;
                if (!_mcBy.TryGetValue(id, out a)) { a = new int[3]; _mcBy[id] = a; }
                a[0]++; a[1] += outH; a[2] += added;
            }
            catch (Exception e) { Stumble("MercAfter", e); }
        }

        private static void MercLine(int day)
        {
            try
            {
                int clans = 0, inK = 0, parties = 0, men = 0, heroes = 0;
                var all = Clan.All;
                if (all != null)
                    foreach (var c in all)
                    {
                        try
                        {
                            if (c == null || c.IsEliminated || !c.IsClanTypeMercenary || SpoilsCompany.IsSpoilsClan(c)) continue;
                            clans++; if (c.Kingdom != null) inK++;
                            int h, m, p;
                            Count(c, out h, out m, out p);
                            heroes += h; men += m; parties += p;
                        }
                        catch (Exception e) { Stumble("MercLine(klan)", e); }
                    }
                var sb = new StringBuilder(500);
                sb.Append("Najemnicy BK (E8/P3): dzien ").Append(day).Append(" | klany najemnikow (bez klanu Spoils) ").Append(clans).Append(", w sluzbie krolestwa ").Append(inK)
                  .Append(", bez sluzby ").Append(clans - inK).Append("; druzyn ").Append(parties).Append(", ludzi ").Append(men).Append(", bohaterow w druzynach ").Append(heroes)
                  .Append(" | zamiana BK ConvertTroopsMercenaries od poprzedniej linii: wywolan ").Append(_mcCalls).Append(" (w sluzbie ").Append(_mcCallsKingdom).Append(", bez sluzby ")
                  .Append(_mcCalls - _mcCallsKingdom).Append("), bohaterow zamienionych na zolnierza z niczego ").Append(_mcHeroesOut).Append(", ludzi dosypanych z niczego ").Append(_mcMenAdded)
                  .Append(" | wedlug klanu (wywolan/bohaterow/dosypanych):");
                if (_mcBy.Count == 0) sb.Append(" -");
                else
                {
                    var ids = new List<string>(_mcBy.Keys);
                    ids.Sort(string.CompareOrdinal);
                    for (int i = 0; i < ids.Count && i < 12; i++)
                    {
                        var a = _mcBy[ids[i]];
                        sb.Append(i > 0 ? "," : "").Append(' ').Append(ids[i]).Append(' ').Append(a[0]).Append('/').Append(a[1]).Append('/').Append(a[2]);
                    }
                }
                sb.Append(" | latka zamiany BK (prefiks SpoilsCompany): ").Append(SpoilsCompany.BkConvertWired ? "wpieta" : "BRAK - bez Spoils of War albo bez BK licznik stoi").Append('.');
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Stumble("MercLine", e); }
            finally { ClearMerc(); }
        }

        // ------------------------------------------------------------ 6. zrodla i ujscia BK bez platnika (dopisek do linii "BK i Diplomacy (E5/A8)")

        private static bool BanditHeroParty(MobileParty mp)
        {
            var pc = mp != null ? mp.PartyComponent : null;
            return pc != null && pc.GetType().Name == "BanditHeroComponent";
        }

        private static void Food(ItemRoster r, out int units, out long value)
        {
            units = 0; value = 0;
            if (r == null) return;
            for (int i = 0; i < r.Count; i++)
            {
                var el = r.GetElementCopyAtIndex(i);
                var it = el.EquipmentElement.Item;
                if (it == null || !it.IsFood || el.Amount <= 0) continue;
                units += el.Amount; value += (long)it.Value * el.Amount;
            }
        }

        /// <summary>Prefiks BK BannerKingsComponent.GiveFood(ref MobileParty): zywnosc w jukach bandy herszta przed darem BK. Tylko odczyt.</summary>
        public static void FoodPre(ref MobileParty __0, out bool __state)
        {
            __state = false;
            try
            {
                var mp = __0;
                if (mp == null || !On) return;
                if (!BanditHeroParty(mp)) { _bfOther++; return; }
                Food(mp.ItemRoster, out _bfU0, out _bfV0);
                __state = true;
            }
            catch (Exception e) { __state = false; Stumble("FoodPre", e); }
        }

        public static void FoodPost(ref MobileParty __0, bool __state)
        {
            if (!__state) return;
            try
            {
                var mp = __0;
                if (mp == null) return;
                int u; long v;
                Food(mp.ItemRoster, out u, out v);
                _bfN++;
                if (u > _bfU0) _bfUnits += u - _bfU0;
                if (v > _bfV0) _bfValue += v - _bfV0;
            }
            catch (Exception e) { Stumble("FoodPost", e); }
        }

        /// <summary>Prefiks i postfiks lambdy BK Demand.FinancialCompromise (fulfiller placi ChangeHeroGold(-koszt) bez odbiorcy). Tylko odczyt kiesy.</summary>
        public static void CompPre(Hero __0, out long __state)
        {
            __state = -1;
            try { if (__0 != null && On) __state = __0.Gold; } catch (Exception e) { __state = -1; Stumble("CompPre", e); }
        }

        public static void CompPost(Hero __0, long __state)
        {
            if (__state < 0 || __0 == null) return;
            try
            {
                long paid = __state - __0.Gold;
                if (paid <= 0) return;
                _fcN++; _fcGold += paid;
                if (__0 == Hero.MainHero) _fcPlayer += paid;
            }
            catch (Exception e) { Stumble("CompPost", e); }
        }

        /// <summary>Prefiks BK BKCourtierBehavior.DrainGold(Hero): cale zloto dworzanina znika (ChangeHeroGold(-Gold)). Tylko licznik.</summary>
        public static void DrainPre(Hero __0)
        {
            try { if (__0 != null && __0.Gold > 0 && On) { _drN++; _drGold += __0.Gold; } } catch (Exception e) { Stumble("DrainPre", e); }
        }

        /// <summary>Prefiks i postfiks BK BuyGoodsAction.BuyGoods(stash, market, buyer, ...): kupujacy placi ChangeHeroGold(-suma), miasto zlota nie dostaje. Tylko odczyt kiesy.</summary>
        public static void BuyPre(Hero __2, out long __state)
        {
            __state = -1;
            try { if (__2 != null && On) __state = __2.Gold; } catch (Exception e) { __state = -1; Stumble("BuyPre", e); }
        }

        public static void BuyPost(Hero __2, long __state)
        {
            if (__state < 0 || __2 == null) return;
            try
            {
                _bgCalls++;
                long paid = __state - __2.Gold;
                if (paid <= 0) return;
                _bgN++; _bgGold += paid;
                if (__2 == Hero.MainHero) _bgPlayer += paid;
            }
            catch (Exception e) { Stumble("BuyPost", e); }
        }

        /// <summary>Z BkDipGold.Daily, przed kropka linii "BK i Diplomacy (E5/A8)": dopisek pomiaru i zerowanie licznikow. Wylaczony klucz - linia jak dotad.</summary>
        internal static void BkSegment(StringBuilder sb)
        {
            if (sb == null || !On) return;
            try
            {
                sb.Append(" | pomiar E8/P3 (gra bez zmian): zywnosc BK dla band hersztow (GiveFood) ").Append(_bfN).Append(" razy, ").Append(_bfUnits).Append(" szt. zywnosci wartej ").Append(_bfValue)
                  .Append(" (inne wywolania GiveFood ").Append(_bfOther).Append("); ujscia BK bez odbiorcy: ugoda z grupa interesu (FinancialCompromise) ").Append(_fcN).Append(" razy / ").Append(_fcGold)
                  .Append(" zl (gracz ").Append(_fcPlayer).Append("), zloto dworzan zerowane (DrainGold) ").Append(_drN).Append(" razy / ").Append(_drGold)
                  .Append(" zl, zakupy laski dworu (BuyGoods) ").Append(_bgN).Append(" razy / ").Append(_bgGold).Append(" zl (gracz ").Append(_bgPlayer).Append("; wywolan ").Append(_bgCalls)
                  .Append("); latki pomiaru wpiete ").Append(_wiredN).Append('/').Append(_wantedN);
            }
            catch (Exception e) { Stumble("BkSegment", e); }
            finally { ClearBk(); }
        }

        // ------------------------------------------------------------ 7. wsie bez wozu (liczby z VillageClogDiag.Daily)

        /// <summary>Z VillageClogDiag.Daily po linii "Zatkane wsie (diagnoza)": prog P3-2 - zatkane wsie Normal bez wozu ponad 7 dob. Tylko log.</summary>
        internal static void CartLine(int day, int clog, int clogNormal, int clogNoCart, int clogNormNoCart7, int clogNormNoCart7Low, double clogNormNoCart7Max,
                                      int allNoCart, int allNormNoCart7, int minMen, string longestWho = null)
        {
            if (!On) return;
            try
            {
                Log.Info("Wsie bez wozu (E8/P3): dzien " + day + " | zatkane Normal bez wozu > " + CartDays.ToString("0", Inv) + " dob: " + clogNormNoCart7
                         + " (w tym hearth <= " + minMen + " - gra nie wystawi nowego: " + clogNormNoCart7Low + "), najdluzej " + (clogNormNoCart7 > 0 ? F1(clogNormNoCart7Max) + (longestWho != null ? " (" + longestWho + ")" : "") : "-") + " dob"
                         + " | zatkane wsie " + clog + " (Normal " + clogNormal + "), z nich bez wozu " + clogNoCart
                         + " | wszystkie wsie bez wozu " + allNoCart + ", z nich Normal bez wozu > " + CartDays.ToString("0", Inv) + " dob (tez niezatkane - gra wystawia woz dopiero przy pelnym magazynie) "
                         + allNormNoCart7 + ".");
            }
            catch (Exception e) { Stumble("CartLine", e); }
        }

        internal static double CartDaysLimit { get { return CartDays; } }

        // ------------------------------------------------------------ 8. rozkazy partii ze statkami: przed snem a po switcie

        /// <summary>Z NightRest.RememberOrder (rozkaz zapamietany przy zasnieciu): swit odda go droga ladowa bez portu (NightOrder.Nav = Default, Port = false) -
        /// tu liczymy partie ze statkami, ktorych rozkaz przed snem mial inna droge albo port. Tylko odczyt.</summary>
        internal static void NoteSleepOrder(MobileParty mp)
        {
            try
            {
                if (mp == null || !On) return;
                _ordAll++;
                if (!mp.HasNavalNavigationCapability) return;
                _ordShips++;
                var b = mp.DefaultBehavior;
                bool given;
                switch (b)
                {
                    case AiBehavior.GoToSettlement:
                    case AiBehavior.BesiegeSettlement:
                    case AiBehavior.RaidSettlement:
                    case AiBehavior.DefendSettlement:
                        given = mp.TargetSettlement != null; break;
                    case AiBehavior.PatrolAroundPoint:
                        given = mp.TargetSettlement != null; break;      // patrol wokol punktu bez osady - swit zostawia AI (jak dotad)
                    case AiBehavior.EngageParty:
                    case AiBehavior.EscortParty:
                    case AiBehavior.GoAroundParty:
                        given = mp.TargetParty != null; break;
                    default:
                        given = false; break;
                }
                if (!given) { _ordAi++; return; }
                _ordGiven++;
                var nav = mp.DesiredAiNavigationType;
                bool port = mp.IsTargetingPort;
                if (nav == MobileParty.NavigationType.Default && !port) { _ordSame++; return; }
                _ordDiff++;
                if (nav == MobileParty.NavigationType.All) _ordNavAll++;
                else if (nav == MobileParty.NavigationType.Naval) _ordNavNaval++;
                else if (nav == MobileParty.NavigationType.None) _ordNavNone++;
                if (port) _ordPort++;
                if (mp.IsCurrentlyAtSea) _ordAtSea++;
            }
            catch (Exception e) { Stumble("NoteSleepOrder", e); }
        }

        private static void OrdersLine(int day)
        {
            try
            {
                Log.Info("Rozkazy morskie (E8/P3): dzien " + day + " | rozkazy zapamietane przed snem (RememberOrder) " + _ordAll + ", w tym partie ze statkami " + _ordShips
                         + " | partie ze statkami: rozkaz oddawany o swicie " + _ordGiven + ", z tego po switcie ta sama droga (lad, bez portu) " + _ordSame
                         + ", INNA niz przed snem " + _ordDiff + " (przed snem: lad i morze " + _ordNavAll + ", tylko morze " + _ordNavNaval + ", bez drogi " + _ordNavNone
                         + "; cel w porcie " + _ordPort + "; zasypialy na morzu " + _ordAtSea + ")"
                         + " | bez rozkazu do oddania (postoj, punkt - o swicie decyduje AI) " + _ordAi + ".");
            }
            catch (Exception e) { Stumble("OrdersLine", e); }
            finally { ClearOrders(); }
        }

        // ------------------------------------------------------------ takt doby

        /// <summary>Raz na dobe (ArmouryBehavior.OnDailyTick, po WorldMeasure.Daily): linie pomiarow i zerowanie licznikow doby. Tylko log.</summary>
        internal static void Daily()
        {
            if (!On) { if (_carIn.Count > 0) _carIn.Clear(); return; }
            if (Campaign.Current == null) return;
            long t0 = Stopwatch.GetTimestamp();
            int day = (int)CampaignTime.Now.ToDays - 1;
            if (!_modelDone)
            {
                _modelDone = true;
                try { ModelLine(day); } catch (Exception e) { Stumble("ModelLine", e); }
            }
            SheepLine(day);
            CaravanLine(day);
            MercLine(day);
            OrdersLine(day);
            try
            {
                double ms = (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency;
                Log.Info("Pomiary swiata (E8/P3): dzien " + day + " | koszt linii " + F2(ms) + " ms | latki pomiaru wpiete " + _wiredN + "/" + _wantedN
                         + (_missing.Count > 0 ? " (BRAK: " + string.Join(", ", _missing.ToArray()) + ")" : "")
                         + " | potkniecia dzis " + _stumblesDay + " (od wczytania " + _stumbles + ").");
            }
            catch (Exception e) { Stumble("Daily", e); }
            finally { _stumblesDay = 0; }
        }

        // ------------------------------------------------------------ wpiecie (SubModuleMain - po BkDipGold)

        private static void Patch(Harmony h, string label, MethodBase m, string pre, string post)
        {
            _wantedN++;
            try
            {
                if (h == null) { _missing.Add(label + " (brak Harmony)"); return; }
                if (m == null) { _missing.Add(label); return; }
                h.Patch(m, prefix: pre != null ? new HarmonyMethod(typeof(Stage8World), pre) : null,
                           postfix: post != null ? new HarmonyMethod(typeof(Stage8World), post) : null);
                _wired.Add(label); _wiredN++;
            }
            catch (Exception e) { _missing.Add(label + " (blad: " + e.Message + ")"); }
        }

        /// <summary>Lambda wyplaty z BK Demand.FinancialCompromise: jedyna metoda typu Demand (albo jego typow zagniezdzonych) o nazwie z "FinancialCompromise",
        /// (Hero) -> bool, ktora wola ChangeHeroGold (nazwa nadana przez kompilator - szukana po IL). 0 albo wiecej niz 1 trafienie = brak.</summary>
        private static MethodBase FindCompromise()
        {
            var t = AccessTools.TypeByName("BannerKings.Behaviours.Diplomacy.Groups.Demands.Demand");
            if (t == null) return null;
            var types = new List<Type> { t };
            try { types.AddRange(t.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic)); } catch { }
            MethodBase hit = null; int hits = 0;
            foreach (var ty in types)
            {
                if (ty == null || ty.ContainsGenericParameters) continue;
                MethodInfo[] ms;
                try { ms = ty.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly); } catch { continue; }
                foreach (var m in ms)
                {
                    try
                    {
                        if (m.Name.IndexOf("FinancialCompromise", StringComparison.Ordinal) < 0 || m.ReturnType != typeof(bool) || m.IsAbstract || m.ContainsGenericParameters) continue;
                        var ps = m.GetParameters();
                        if (ps.Length != 1 || ps[0].ParameterType != typeof(Hero) || m.GetMethodBody() == null) continue;
                        bool pays = false;
                        foreach (var ci in PatchProcessor.GetOriginalInstructions(m))
                        {
                            var callee = ci.operand as MethodInfo;
                            if (callee != null && callee.Name == "ChangeHeroGold") { pays = true; break; }
                        }
                        if (!pays) continue;
                        hits++; if (hit == null) hit = m;
                    }
                    catch { }
                }
            }
            return hits == 1 ? hit : null;
        }

        internal static void ApplyAll(Harmony h)
        {
            _wired.Clear(); _missing.Clear(); _wiredN = 0; _wantedN = 0;
            try
            {
                // DTE (laduje sie przed Armoury): wystawiony z przydzialem i koniec misji
                var tLogic = AccessTools.TypeByName("DynamicTroopEquipmentReupload.DynamicTroopMissionLogic");
                var tAssign = AccessTools.TypeByName("DynamicTroopEquipmentReupload.Assignment");
                var getP = tAssign != null ? AccessTools.PropertyGetter(tAssign, "UnderEquippedMoralePenalty") : null;
                if (getP != null && getP.ReturnType != typeof(float)) getP = null;
                MethodInfo reg = null, end = null;
                if (tLogic != null && getP != null)
                {
                    reg = AccessTools.Method(tLogic, "RegisterSpawnedAgentAssignment");
                    var rp = reg != null ? reg.GetParameters() : null;
                    if (rp == null || rp.Length != 2 || rp[0].ParameterType != typeof(Agent) || rp[1].ParameterType != tAssign) reg = null;
                    end = AccessTools.Method(tLogic, "OnEndMission", Type.EmptyTypes);
                    if (end != null && end.DeclaringType != tLogic) end = null;
                }
                if (reg != null && end != null) _uePenalty = getP; else { reg = null; end = null; }   // polowa pomiaru bylaby falszywa - obie latki albo zadna
                Patch(h, "DTE wystawiony z przydzialem (DynamicTroopMissionLogic.RegisterSpawnedAgentAssignment)", reg, null, nameof(UnderRegPostfix));
                Patch(h, "DTE koniec misji (DynamicTroopMissionLogic.OnEndMission)", end, null, nameof(UnderEndPostfix));

                // BK (laduje sie przed Armoury)
                var bkc = AccessTools.TypeByName("BannerKings.Components.BannerKingsComponent");
                var food = bkc != null ? AccessTools.Method(bkc, "GiveFood") : null;
                var fp = food != null ? food.GetParameters() : null;
                if (fp == null || fp.Length != 1 || fp[0].ParameterType != typeof(MobileParty).MakeByRefType()) food = null;
                Patch(h, "BK zywnosc z niczego (BannerKingsComponent.GiveFood)", food, nameof(FoodPre), nameof(FoodPost));
                Patch(h, "BK ugoda z grupa interesu (lambda Demand.FinancialCompromise)", FindCompromise(), nameof(CompPre), nameof(CompPost));
                var cb = AccessTools.TypeByName("BannerKings.Behaviours.BKCourtierBehavior");
                var drain = cb != null ? AccessTools.Method(cb, "DrainGold", new[] { typeof(Hero) }) : null;
                Patch(h, "BK zloto dworzan (BKCourtierBehavior.DrainGold)", drain, nameof(DrainPre), null);
                var ba = AccessTools.TypeByName("BannerKings.Actions.BuyGoodsAction");
                var buy = ba != null ? AccessTools.Method(ba, "BuyGoods") : null;
                var bp = buy != null ? buy.GetParameters() : null;
                if (bp == null || bp.Length < 3 || bp[2].ParameterType != typeof(Hero)) buy = null;
                Patch(h, "BK zakupy laski dworu (BuyGoodsAction.BuyGoods)", buy, nameof(BuyPre), nameof(BuyPost));
            }
            catch (Exception e) { Log.Error("Stage8World.ApplyAll", e); }
            Log.Info("Pomiary swiata (E8/P3): latki przy starcie gry - wpiete " + _wiredN + "/" + _wantedN + ": " + (_wired.Count > 0 ? string.Join(", ", _wired.ToArray()) : "-")
                     + "; BRAK: " + (_missing.Count > 0 ? string.Join(", ", _missing.ToArray()) : "-")
                     + " | tylko pomiar - zadna latka nie zmienia wyniku cudzej metody; licznik zamiany BK najemnikow siedzi w prefiksie SpoilsCompany (linia \"Najemnicy BK (E8/P3)\").");
        }
    }
}
