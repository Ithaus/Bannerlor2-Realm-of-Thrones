using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Extensions;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// E5/0 (195) - czesc pomiarowa kiesy ludu: latki tylko do odczytu (void, bez ref i bez __result do zapisu, bez return false), odczyt BK refleksja,
    /// kalibracja D1, modele czynne, sonda korony i linie: "Nadwyzka kas", "Bitwy", "Notable", "Jedzenie z targu", "Zadowolenie BK" (plan E5/0 rozdz. 4 i 7).
    ///  - nawias konsumpcji (K1/E5K1-09 - budzet i wydatek raz na osade i kategorie): postfiks na ItemConsumptionBehavior.UpdateDemandShift (nikt go nie lata)
    ///    otwiera, postfiks Priority.Last na BK EconomyPatches.CalculateBudget (po HistoricalPrices.BudgetPostfix) sledzi reszte budzetu wiersz po wierszu,
    ///    postfiks Priority.Last + finalizer na MakeConsumption zamyka. Wywolania CalculateBudget refleksja z RawPrice.HouseBudget sa poza nawiasem.
    ///  - bitwy: prefiks HigherThanNormal + postfiks + finalizer (odczyt) na MapEventParty.CommitGoldChanges - rzeczywista zmiana kies wodzow i partii (okno O14
    ///    bez zmian) i w nawiasie zdarzenia innych bohaterow (A6: obciazenie wodza zdjetego z partii - recenzja E5/0 (1-1));
    ///  - notable: prefiks + postfiks + finalizer (odczyt) na NotablePowerManagementBehavior.BalanceGoldAndPowerOfNotable - przelewy z kasa osady (A3) osobno
    ///    od "z niczego / w nicosc" (recenzja E5/0 (1-2)).
    /// Zakazane w calej paczce: ChangeGold, GiveGoldAction, AddToCounts, SetSoldItems, UpdateSatisfaction, PeopleOf przed kalibracja, GetPopData bez
    /// IsSettlementPopulated, GetPolicy dla osady bez polityk BK, CastlePurse.Spendable, Seal31.Spare, Town.FoodChange, zapis do Satisfactions.
    /// </summary>
    internal static partial class TownFolk
    {
        private static readonly string[] LuxIds = { "silver", "velvet", "jewelry", "spice", "fur" };

        // ------------------------------------------------------------ liczniki linii (od poprzedniego Lines)
        private static readonly long[] _bPaid = new long[BSrc], _bBelow = new long[BSrc];
        private static long _unsold; private static int _unsoldVisits;
        private static readonly long[] _take = new long[Takes], _takeBelow = new long[Takes];
        private static long _rentPaid, _rentBelow;
        private static int _noBkCons;
        // nawias konsumpcji
        private sealed class Track { public double Pending = double.NaN, Spent, Add, First = double.NaN; public int Rows; }   // First - P1 (pomiar): budzet pierwszego wiersza kategorii
        private static readonly Dictionary<ItemCategory, Track> _brTrack = new Dictionary<ItemCategory, Track>();
        private static readonly List<Track> _pool = new List<Track>();
        private static int _poolN;
        private static Town _brTown; private static bool _brOpen; private static int _brGold0;
        private static int _brUnclosed, _brMismatch, _brAborted;
        // recenzja E5/0 (2-1): dokup jedzenia BK (zapas warowni <= 10% gornej granicy w chwili otwarcia nawiasu - ten sam warunek co BK :532)
        private static bool _brLowFood; private static int _lowFoodN; private static double _lowFoodCut;
        private static int _brGoldMark; private static ItemCategory _brGoldCat; private static long _brFoodGold;   // recenzja B1 (1-5): zloto wierszy jedzenia w nawiasie (gmina w PayForGoods)
        // F3 (test zbiorczy 5-8, K1-3a krok 1 - tylko pomiar, klucz Stage6MeasureLog): konie zdjete z polki przez ludnosc osady w tym nawiasie - zloto wierszy
        // kategorii koni (wzor _brFoodGold) i zloto wszystkich wierszy przed ostatnim (ostatni = ConsumedValue BK - ta suma); sztuki z dziennika sprzedazy gry
        private static bool _brHorse; private static long _brRowGold;
        private static readonly long[] _brHorseGold = new long[HorseBook.MCats], _brHorseN = new long[HorseBook.MCats];
        private static readonly Dictionary<ItemCategory, int> _grp = new Dictionary<ItemCategory, int>();
        private static long _foodTown, _foodCastle;
        private static readonly double[] _satPlus = new double[4], _satMinus = new double[4];
        // bitwy (recenzja E5/0 (1-1): nawias CommitGoldChanges - zdarzenia bohaterow innych niz wodz partii, np. obciazenie A6 wodza zdjetego z partii)
        private static int _btN, _btN1, _btN2, _btN3, _btOffN;
        private static long _btA, _btB, _btC1, _btC2, _btC3, _btAOff, _btC1Off;
        private static bool _btOpen; private static Hero _btLead;
        // notable (recenzja E5/0 (1-2): przelewy notabl <-> kasa osady w nawiasie pasma - A3 - osobno od "z niczego / w nicosc")
        private static long _ntTownOut, _ntTownIn, _ntVillOut, _ntVillIn; private static int _ntAll, _ntVill, _ntCastleMarket;
        private static long _ntTownOutK, _ntTownInK, _ntVillOutK, _ntVillInK;
        private static Hero _ntHero; private static long _ntCurOutK, _ntCurInK;
        private static readonly Dictionary<Settlement, double[]> _ntDemand = new Dictionary<Settlement, double[]>();
        // ksiega pieniadza (MoneyLedger.VillageCashLine - przed jej ClearDay): "bitwy" i pasmo notabli w ksiedze obiegu
        private static bool _ledOk; private static long _ledBattleIn, _ledBattleOut, _ledBandIn, _ledBandOut;
        // korona (CrownProbe)
        private sealed class CrownRing { public readonly bool[] Loan = new bool[7]; public readonly long[] Due = new long[28]; public int H7, H28, F28; }
        private static readonly Dictionary<string, CrownRing> _crown = new Dictionary<string, CrownRing>();
        private static bool _cpOk, _cpIncome; private static long _cpLoan, _cpOver, _cpReserve; private static int _cpLoanK, _cpLoan7K, _cpOverK, _cpLow, _cpLowWar, _cpNeed, _cpK;
        // zaulki (Jeff 16:10): najdluzsza seria sesji
        private static int _streakMax; private static string _streakName;
        private static int _campStart = int.MinValue;
        private static string _wiredText = "-", _missText = "-", _ownersText = "-";

        private static void ResetProbe()
        {
            Array.Clear(_bPaid, 0, BSrc); Array.Clear(_bBelow, 0, BSrc); _unsold = 0; _unsoldVisits = 0;
            Array.Clear(_take, 0, Takes); Array.Clear(_takeBelow, 0, Takes); _rentPaid = _rentBelow = 0; _noBkCons = 0;
            _brTrack.Clear(); _pool.Clear(); _poolN = 0; _brTown = null; _brOpen = false; _brUnclosed = _brMismatch = _brAborted = 0;
            _brLowFood = false; _lowFoodN = 0; _lowFoodCut = 0; _brGoldMark = 0; _brGoldCat = null; _brFoodGold = 0;
            _brHorse = false; _brRowGold = 0; Array.Clear(_brHorseGold, 0, _brHorseGold.Length); Array.Clear(_brHorseN, 0, _brHorseN.Length);
            _grp.Clear(); _foodTown = _foodCastle = 0; Array.Clear(_satPlus, 0, 4); Array.Clear(_satMinus, 0, 4);
            _btN = _btN1 = _btN2 = _btN3 = _btOffN = 0; _btA = _btB = _btC1 = _btC2 = _btC3 = _btAOff = _btC1Off = 0; _btOpen = false; _btLead = null;
            _ntTownOut = _ntTownIn = _ntVillOut = _ntVillIn = 0; _ntAll = _ntVill = _ntCastleMarket = 0; _ntDemand.Clear();
            _ntTownOutK = _ntTownInK = _ntVillOutK = _ntVillInK = 0; _ntHero = null; _ntCurOutK = _ntCurInK = 0;
            _ledOk = false; _ledBattleIn = _ledBattleOut = _ledBandIn = _ledBandOut = 0;
            _crown.Clear(); _cpOk = false;
            _streakMax = 0; _streakName = null; _campStart = int.MinValue;
            _sHead = _sFill = 0; Array.Clear(_sTake, 0, 28); Array.Clear(_sIn, 0, 28); Array.Clear(_sIn2, 0, 28); _sDayOk = false; _csvFirst = false;
            _polMgr = null; _polDict = null;
        }

        private static string Ms(long ticks) { return (ticks * 1000.0 / Stopwatch.Frequency).ToString("0.0", Inv); }

        // ============================================================ latki (menu glowne)
        internal static void ApplyAll(Harmony h)
        {
            _main = Environment.CurrentManagedThreadId;
            var done = new List<string>(); var miss = new List<string>(); var targets = new List<MethodBase>(); var names = new List<string>();
            void Wire(string name, MethodBase m, string pre, string post, string fin, int postPrio, int prePrio = Priority.Normal)
            {
                try
                {
                    if (m == null) { miss.Add(name); return; }
                    h.Patch(m, prefix: pre != null ? new HarmonyMethod(typeof(TownFolk), pre) { priority = prePrio } : null,
                               postfix: post != null ? new HarmonyMethod(typeof(TownFolk), post) { priority = postPrio } : null,
                               finalizer: fin != null ? new HarmonyMethod(typeof(TownFolk), fin) : null);
                    done.Add(name); targets.Add(m); names.Add(name);
                }
                catch (Exception e) { miss.Add(name + " (" + e.Message + ")"); }
            }
            try
            {
                Wire("budzet dnia (UpdateDemandShift)", AccessTools.Method(typeof(ItemConsumptionBehavior), "UpdateDemandShift", new[] { typeof(Town), typeof(Dictionary<ItemCategory, float>) }),
                     null, nameof(DemandPostfix), null, Priority.Normal);
                Wire("wiersze polki BK (CalculateBudget, po HistoricalPrices)", AccessTools.Method("BannerKings.Patches.EconomyPatches:CalculateBudget"),
                     null, nameof(BudgetPostfix), null, Priority.Last);
                Wire("konsumpcja (MakeConsumption 2/2)", AccessTools.Method(typeof(ItemConsumptionBehavior), "MakeConsumption",
                     new[] { typeof(Town), typeof(Dictionary<ItemCategory, float>), typeof(Dictionary<ItemCategory, int>) }), null, nameof(ConsumePostfix), nameof(ConsumeFinalizer), Priority.Last);
                // recenzja E5/0 (1-1): prefiks HigherThanNormal - nawias otwarty przed prefiksami Normal (A6 BattlePlunder.ChargePrefix obciaza tam wodza
                // zdjetego z partii; A6 jest rejestrowane wczesniej, wiec przy rownym priorytecie biegloby przed nami), po oknie O14 (First); finalizer zamyka nawias
                Wire("bitwy (CommitGoldChanges 3/3)", AccessTools.Method(typeof(MapEventParty), "CommitGoldChanges"), nameof(BattlePre), nameof(BattlePost), nameof(BattleFin),
                     Priority.Normal, Priority.HigherThanNormal);
                Wire("pasmo notabli (BalanceGoldAndPowerOfNotable 3/3)", AccessTools.Method(typeof(NotablePowerManagementBehavior), "BalanceGoldAndPowerOfNotable", new[] { typeof(Hero) }),
                     nameof(NotablePre), nameof(NotablePost), nameof(NotableFin), Priority.Normal);
            }
            catch (Exception e) { miss.Add("ApplyAll (" + e.Message + ")"); }
            // E5/B1 (173): okna kiesy czynnej - osobne listy (linia startowa "Kiesa ludu (E5/B1)"; E5/0 bez zmian)
            int d0 = done.Count, m0 = miss.Count;
            try
            {
                Wire("kotwica budzetu wiersza (CalculateBudget, Low)", AccessTools.Method("BannerKings.Patches.EconomyPatches:CalculateBudget"), null, nameof(AnchorBudgetPostfix), null, Priority.Low);
                var bkTax = AccessTools.TypeByName("BannerKings.Models.Vanilla.BKTaxModel");
                Wire("podatek wsi BK (BKTaxModel.CalculateVillageTaxFromIncome x VillageLordScale)",
                     bkTax != null ? AccessTools.Method(bkTax, "CalculateVillageTaxFromIncome", new[] { typeof(Village), typeof(int) }) : null, null, nameof(VillageTaxPostfix), null, Priority.Normal);
            }
            catch (Exception e) { miss.Add("ApplyAll B1 (" + e.Message + ")"); }
            var doneB = done.GetRange(d0, done.Count - d0); var missB = miss.GetRange(m0, miss.Count - m0);
            done.RemoveRange(d0, done.Count - d0); miss.RemoveRange(m0, miss.Count - m0);
            targets.RemoveRange(d0, targets.Count - d0); names.RemoveRange(d0, names.Count - d0);
            _wiredText = done.Count > 0 ? string.Join(", ", done.ToArray()) : "-";
            _missText = miss.Count > 0 ? string.Join(", ", miss.ToArray()) : "-";
            _ownersText = Owners(targets, names);
            var s = Settings.Current;
            Log.Info("Kiesa ludu (E5/0): okna wpiete: " + _wiredText + "; nasluchy: wyroby (OnItemProduced), przelewy gry, tabory wsi, regulator kas, pobory korony - w ksiedze pieniadza i KingdomTreasury; BRAK: "
                     + _missText + "; wlasciciele latek celow: " + _ownersText + "; tryb przy starcie " + (s != null ? s.TownFolkMode : -1)
                     + " (stan gry - linia sesji). Tylko log: zadna latka nie zmienia gry.");
            Log.Info("Kiesa ludu (E5/B1): okna wpiete: budzet dnia (UpdateDemandShift - kotwica slownika w tym samym postfiksie), konsumpcja (zaplata przez CastlePurse 110 -> TownPurse.OnConsumed)"
                     + (doneB.Count > 0 ? ", " + string.Join(", ", doneB.ToArray()) : "") + "; BRAK: " + (missB.Count > 0 ? string.Join(", ", missB.ToArray()) : "-")
                     + "; klucz przy starcie TownFolkLive " + (s != null && s.TownFolkLive ? 1 : 0) + " (stan czynny - linia \"Kiesa ludu (E5/B1): CZYNNA\" pierwszej doby).");
        }

        private static string Owners(List<MethodBase> targets, List<string> names)
        {
            var parts = new List<string>();
            for (int i = 0; i < targets.Count; i++)
            {
                try
                {
                    var info = Harmony.GetPatchInfo(targets[i]);
                    var ow = new List<string>();
                    if (info != null) foreach (var o in info.Owners) if (!ow.Contains(o)) ow.Add(o);
                    string n = names[i]; int c = n.IndexOf(" ("); if (c > 0) n = n.Substring(0, c);
                    parts.Add(n + ": " + (ow.Count > 0 ? string.Join(" + ", ow.ToArray()) : "-"));
                }
                catch { parts.Add(names[i] + ": ?"); }
            }
            return parts.Count > 0 ? string.Join("; ", parts.ToArray()) : "-";
        }

        // ------------------------------------------------------------ 4.1 otwarcie nawiasu: budzet dnia osady (slownik kategorii)
        public static void DemandPostfix(Town __0, Dictionary<ItemCategory, float> __1)
        {
            try
            {
                if (__0 == null || __1 == null || !Run || OffMain()) return;
                long t = Stopwatch.GetTimestamp();
                if (_brOpen) _brUnclosed++;
                _brTown = __0; _brOpen = true; _brTrack.Clear(); _poolN = 0; _brGold0 = __0.Gold;
                _brGoldMark = __0.Gold; _brGoldCat = null; _brFoodGold = 0;   // recenzja B1 (1-5): zloto wierszy jedzenia od zera
                // F3: pomiar koni mieszczan w tym nawiasie (ksiega koni E6/0) - liczniki nawiasu od zera
                _brHorse = HorseBook.FolkOn; _brRowGold = 0;
                if (_brHorse) { Array.Clear(_brHorseGold, 0, _brHorseGold.Length); Array.Clear(_brHorseN, 0, _brHorseN.Length); }
                _brAnchor = false; _brFacFood = _brFacRest = 1;   // E5/B1: czynniki kotwicy tego nawiasu (AnchorDict nizej)
                OfferOpen();   // P1 (pomiar): oferta nadwyzki tego nawiasu od zera (AddOffer dopisuje, ConsumePostfix zamyka)
                // recenzja E5/0 (2-1): warunek dokupu jedzenia BK (EconomyPatches :532) - zapas sie w petli nie zmienia (polka i kasa tak; FoodStocksUpperLimit to odczyt modelu)
                _brLowFood = false;
                try { _brLowFood = __0.FoodStocks <= __0.FoodStocksUpperLimit() * 0.1f; } catch (Exception e) { Stumble("DemandPostfix(zapas)", e); }
                var f = Get(__0.Settlement);
                if (f != null)
                {
                    double sum = 0, si = 0;
                    foreach (var kv in __1)
                    {
                        if (kv.Key == null || !(kv.Value > 0)) continue;
                        sum += kv.Value; si += kv.Value * __0.GetItemCategoryPriceIndex(kv.Key);
                    }
                    f.G0 += sum;
                    if (sum > 0) f.Idx = si / sum;
                    // migawka zadowolenia BK przed konsumpcja (kopia wartosci - tablica BK nietknieta)
                    f.Sat0Ok = false;
                    var sat = Sats(f);
                    if (sat != null && sat.Length >= 4) { for (int i = 0; i < 4; i++) f.Sat0[i] = sat[i]; f.Sat0Ok = true; }
                    // E5/B1: kotwica raz na (osade, kategorie, dobe) - slownik x czynnik grupy i oferta nadwyzki ludu, PO pomiarze (G0, Idx, Sat0 - przed kotwica)
                    if (LiveKey) { try { AnchorDict(f, __0, __1, sum); } catch (Exception e) { _brAnchor = false; Stumble("AnchorDict", e); } }
                }
                _patchTicks += Stopwatch.GetTimestamp() - t;
            }
            catch (Exception e) { _brOpen = false; Stumble("DemandPostfix", e); }
        }

        // ------------------------------------------------------------ 4.2 wiersze polki BK: budzet wiersza i reszta po wierszu
        public static void BudgetPostfix(Town __0, float __1, ItemCategory __2, float __result)
        {
            try
            {
                if (!_brOpen || __2 == null || !ReferenceEquals(__0, _brTown)) return;
                long t = Stopwatch.GetTimestamp();
                // recenzja B1 (1-5): zloto wiersza = przyrost kasy miedzy kolejnymi wierszami (BK ChangeGold(num6) idzie po CalculateBudget tego wiersza) - czesc jedzenia dla gminy
                int g = __0.Gold;
                if (_brGoldCat != null && GroupOf(_brGoldCat) == 0) _brFoodGold += g - _brGoldMark;
                if (_brHorse && _brGoldCat != null)   // F3: zloto poprzedniego wiersza - do sumy wierszy, a wiersz kategorii koni do jej licznika
                {
                    int dg = g - _brGoldMark;
                    _brRowGold += dg;
                    if (dg != 0 && _brGoldCat.IsAnimal) { int hc = HorseBook.MarketCat(_brGoldCat); if (hc >= 0) _brHorseGold[hc] += dg; }
                }
                _brGoldMark = g; _brGoldCat = __2;
                Track tr;
                if (!_brTrack.TryGetValue(__2, out tr))
                {
                    if (_poolN < _pool.Count) { tr = _pool[_poolN]; tr.Pending = double.NaN; tr.Spent = tr.Add = 0; tr.Rows = 0; tr.First = double.NaN; }
                    else { tr = new Track(); _pool.Add(tr); }
                    _poolN++;
                    _brTrack[__2] = tr;
                }
                if (!double.IsNaN(tr.Pending)) tr.Spent += tr.Pending - __1;   // reszta po poprzednim wierszu = wejscie tego
                if (tr.Rows == 0) tr.First = __result;                       // P1 (pomiar): budzet pierwszego wiersza kategorii (z oferta nadwyzki)
                tr.Rows++;
                // zmiana budzetu w petli (BK dobrobyt/indeks razem z mnoznikami HistoricalPrices) - tylko na wierszach, ktore BK przetwarza (recenzja E5/0 (2-2):
                // wiersz z budzetem 0 - np. ArmsLeaks.ZeroTownBudget - nie zmienia slownika i nie wchodzi do wydatku, wiec nie wchodzi i tu)
                if (__result > 0.01f) tr.Add += __result - __1;
                tr.Pending = __result > 0.01f ? __result : double.NaN;       // BK przetwarza wiersz tylko przy budzecie > 0.01
                _patchTicks += Stopwatch.GetTimestamp() - t;
            }
            catch (Exception e) { Stumble("BudgetPostfix", e); }
        }

        // ------------------------------------------------------------ 4.3 zamkniecie nawiasu (Priority.Last - po CastlePurse i ksiedze pieniadza)
        public static void ConsumePostfix(Town __0, Dictionary<ItemCategory, float> __1, Dictionary<ItemCategory, int> __2)
        {
            if (!_brOpen) return;
            try
            {
                if (OffMain()) return;
                long t = Stopwatch.GetTimestamp();
                if (__0 == null || !ReferenceEquals(__0, _brTown)) { _brMismatch++; return; }
                var f = Get(__0.Settlement);
                if (f == null) return;
                double spent = 0, add = 0; int rows = 0;
                var grp = new double[3];
                foreach (var kv in _brTrack)
                {
                    var tr = kv.Value;
                    if (!double.IsNaN(tr.Pending))
                    {
                        float rest;
                        if (__1 != null && __1.TryGetValue(kv.Key, out rest)) tr.Spent += tr.Pending - rest;
                        tr.Pending = double.NaN;
                    }
                    spent += tr.Spent; add += tr.Add; rows += tr.Rows;
                    grp[GroupOf(kv.Key)] += tr.Spent;
                }
                OfferClose(f);   // P1 (pomiar, wlasny try): ile oferty nadwyzki tej osady znalazlo towar (wydatek kategorii ponad budzet gry)
                f.ConsGold += __0.Gold - _brGold0;
                double cv = double.NaN;
                var econ = EconOf(f);
                if (econ == null) _noBkCons++;
                else
                {
                    try { cv = Convert.ToDouble(_pConsumed.GetValue(econ, null)); f.ConsumedBk += cv; } catch (Exception e) { cv = double.NaN; Stumble("ConsumedValue", e); }
                    var sat = Sats(f);
                    if (sat != null && sat.Length >= 4 && f.Sat0Ok)
                        for (int i = 0; i < 4; i++) { double dl = sat[i] - f.Sat0[i]; if (dl > 0) _satPlus[i] += dl; else _satMinus[i] -= dl; }
                }
                // recenzja E5/0 (2-1): dokup jedzenia BK (zapas <= 10%, EconomyPatches :532-552) - petla zdejmuje z budzetu num3 x cena razem z dokupem,
                // a towar, zloto i ConsumedValue to num5 x cena obciete do polki; nadwyzka petli nad ConsumedValue tej osady zdjeta z wydatku jedzenia
                // (kotwica W, Grp, SpendFood bez "zjedzonego ponad polke"); ConsumedValue BK zeruje sie na poczatku kazdego wywolania - para tej osady
                if (_brLowFood && !double.IsNaN(cv))
                {
                    _lowFoodN++;
                    double ex = spent - cv;
                    if (ex > 0) { double cut = Math.Min(ex, grp[0]); grp[0] -= cut; spent -= cut; _lowFoodCut += cut; }
                }
                f.Grp[0] += grp[0]; f.Grp[1] += grp[1]; f.Grp[2] += grp[2]; f.SpendFood += grp[0];
                f.SpendGame += spent; f.G1 += add; f.Rows += rows;
                int food = 0;
                if (__2 != null) foreach (var kv in __2) if (kv.Key != null && kv.Value > 0 && GroupOf(kv.Key) == 0) food += kv.Value;
                if (__0.IsTown) _foodTown += food; else { _foodCastle += food; f.FoodSold += food; }
                if (_brHorse) HorseFolk(__0, __2, cv);   // F3: konie mieszczan do ksiegi koni (tylko liczniki)
                _patchTicks += Stopwatch.GetTimestamp() - t;
            }
            catch (Exception e) { Stumble("ConsumePostfix", e); }
            finally { _brOpen = false; _brTown = null; _brLowFood = false; _brAnchor = false; _brHorse = false; }
        }

        /// <summary>F3 (K1-3a krok 1, tylko pomiar): konie zdjete z polki tej osady w zamykanym nawiasie -> HorseBook ("ujscia poza K1: mieszczanie").
        /// Sztuki: dziennik sprzedazy gry (BK dopisuje w nim liczbe zdjeta z kazdego wiersza polki). Zloto: przyrost kasy miedzy kolejnymi wierszami BK
        /// (BudgetPostfix); ostatni wiersz nie ma nastepnego, a kasa jest juz po CastlePurse / TownPurse (First) - jego zloto to ConsumedValue BK tej osady
        /// (suma zlota wszystkich wierszy) minus wiersze juz policzone. Bez ConsumedValue albo bez wierszy BK (konsumpcja gry) - "bez pelnej wyceny".</summary>
        private static void HorseFolk(Town town, Dictionary<ItemCategory, int> sold, double cv)
        {
            try
            {
                bool part = false;
                if (_brGoldCat != null && _brGoldCat.IsAnimal)
                {
                    int hc = HorseBook.MarketCat(_brGoldCat);
                    if (hc >= 0)
                    {
                        long last = double.IsNaN(cv) ? -1 : (long)Math.Round(cv) - _brRowGold;
                        if (last < 0) part = true; else _brHorseGold[hc] += last;
                    }
                }
                long any = 0;
                if (sold != null)
                    foreach (var kv in sold)
                    {
                        if (kv.Key == null || kv.Value <= 0 || !kv.Key.IsAnimal) continue;
                        int hc = HorseBook.MarketCat(kv.Key);
                        if (hc < 0) continue;
                        _brHorseN[hc] += kv.Value; any += kv.Value;
                    }
                if (any > 0 && _brGoldCat == null) part = true;   // sztuki bez zadnego wiersza BK w nawiasie (osada bez ludnosci BK - petla gry): zloto nieznane
                HorseBook.NoteFolk(town.IsTown, _brHorseN, _brHorseGold, part);
            }
            catch (Exception e) { Stumble("ConsumePostfix(konie)", e); }
        }

        /// <summary>Wyjatek w cudzym kodzie konsumpcji - nawias zamkniety od razu (void: wyjatek leci dalej bez zmian).</summary>
        public static void ConsumeFinalizer(Exception __exception)
        {
            try { if (__exception != null && _brOpen) { _brAborted++; _brOpen = false; _brTown = null; _brAnchor = false; } } catch { }
        }

        /// <summary>Recenzja B1 (1-5): zloto jedzenia w otwartym nawiasie konsumpcji tej osady (przyrosty kasy po wierszach jedzenia, z ostatnim wierszem) - dla PayForGoods
        /// (CastlePurse First, przed zamknieciem nawiasu). -1 - nawias nie tej osady.</summary>
        internal static long BracketFoodGold(Town town)
        {
            if (!_brOpen || town == null || !ReferenceEquals(town, _brTown)) return -1;
            long x = _brFoodGold;
            if (_brGoldCat != null && GroupOf(_brGoldCat) == 0) x += town.Gold - _brGoldMark;
            return Math.Max(0, x);
        }

        private static int GroupOf(ItemCategory c)
        {
            int g;
            if (_grp.TryGetValue(c, out g)) return g;
            string id = c.StringId ?? "";
            if (IsFoodCategory(c)) g = 0;
            else if (ArmsLeaks.IsArmsCategory(id) || c.IsAnimal || Array.IndexOf(LuxIds, id) >= 0) g = 2;
            else g = 1;
            _grp[c] = g;
            return g;
        }

        /// <summary>Jedzenie (grupa 0 linii "Jedzenie z targu (E5/0)"): kategoria z BonusToFoodStores albo piwo, wino. Jedna definicja dla E5/0 i E5/B4
        /// (targ przy zamku, CastleMarket); bez stanu i bez pamieci podrecznej - bezpieczne z kazdego watku.</summary>
        internal static bool IsFoodCategory(ItemCategory c)
        {
            if (c == null) return false;
            string id = c.StringId ?? "";
            return c.Properties == ItemCategory.Property.BonusToFoodStores || id == "beer" || id == "wine";
        }

        // ------------------------------------------------------------ 4.4 bitwy: rzeczywista zmiana kies przy wyplacie
        public struct BState { public bool On; public Hero H; public MobileParty Mp; public int G0, Pl, Lost; }

        public static void BattlePre(MapEventParty __instance, out BState __state)
        {
            __state = default(BState);
            try
            {
                if (__instance == null || !Measure || OffMain()) return;
                long t = Stopwatch.GetTimestamp();
                __state.Pl = __instance.PlunderedGold; __state.Lost = __instance.GoldLost;
                var party = __instance.Party;
                // nawias (recenzja E5/0 (1-1)): zdarzenia "z niczego / w nicosc" bohaterow innych niz wodz tej partii licza sie w OnGold (A6: wodz zdjety z partii)
                _btOpen = true; _btLead = party != null ? party.LeaderHero : null;
                if (party != null && (__state.Pl != 0 || __state.Lost != 0))
                {
                    var hero = party.LeaderHero;
                    if (hero != null) { __state.H = hero; __state.G0 = hero.Gold; __state.On = true; }
                    else { __state.Mp = party.IsMobile ? party.MobileParty : null; __state.G0 = __state.Mp != null ? __state.Mp.PartyTradeGold : 0; __state.On = true; }
                }
                _patchTicks += Stopwatch.GetTimestamp() - t;
            }
            catch (Exception e) { __state.On = false; Stumble("BattlePre", e); }
        }

        public static void BattlePost(MapEventParty __instance, BState __state)
        {
            try
            {
                if (!__state.On) return;
                long t = Stopwatch.GetTimestamp();
                _btN++;
                if (__state.H != null)
                {
                    _btN1++;
                    long dl = (long)__state.H.Gold - __state.G0;
                    if (dl < 0) _btA += -dl; else _btC1 += dl;
                }
                else
                {
                    var mp = __state.Mp;
                    if (mp != null && mp.IsPartyTradeActive)
                    {
                        _btN2++;
                        long dl = (long)mp.PartyTradeGold - __state.G0;
                        if (dl < 0) _btB += -dl; else _btC2 += dl;
                    }
                    else
                    {
                        // partia lorda bez wodza (gra zdjela go w CaptureDefeatedPartyMembers - niewola, ucieczka) osobno od band i milicji (recenzja E5/0 (1-1))
                        if (mp != null && mp.IsLordParty) _btN3++; else _btN2++;
                        if (__state.Pl > 0) _btC3 += __state.Pl;   // partia bez kiesy handlowej - udzial przepada
                    }
                }
                _patchTicks += Stopwatch.GetTimestamp() - t;
            }
            catch (Exception e) { Stumble("BattlePost", e); }
        }

        /// <summary>Finalizer CommitGoldChanges: zamkniecie nawiasu bitwy (takze po wyjatku w metodzie gry; void - wyjatek leci dalej bez zmian).</summary>
        public static void BattleFin(Exception __exception)
        {
            _btOpen = false; _btLead = null;
        }

        /// <summary>
        /// Z TownFolk.OnGold (zdarzenie gry PO przelewie): w nawiasie bitwy - przelew "z niczego / w nicosc" bohatera innego niz wodz partii w chwili wyplaty
        /// (A6: wodz pokonanej partii zdjety z partii placi swoja czesc puli - GiveGoldAction wodz -> nic; wodz w partii liczony z roznicy kiesy, bez drugiego razu);
        /// w nawiasie pasma - przelew notabl <-> kasa osady (A3).
        /// </summary>
        private static void NoteBracketGold(Hero gh, Hero rh, Settlement gs, Settlement rs, int a, bool gNone, bool rNone)
        {
            if (_btOpen)
            {
                if (rNone && gh != null && !ReferenceEquals(gh, _btLead)) { _btAOff += a; _btOffN++; }
                else if (gNone && rh != null && !ReferenceEquals(rh, _btLead)) _btC1Off += a;
            }
            if (_ntHero != null)
            {
                if (ReferenceEquals(gh, _ntHero) && rs != null) _ntCurOutK += a;        // notabl -> kasa osady
                else if (ReferenceEquals(rh, _ntHero) && gs != null) _ntCurInK += a;    // kasa osady -> notabl
            }
        }

        // ------------------------------------------------------------ 4.5 notable: pasmo 4 500 - 10 500
        public static void NotablePre(Hero __0, out int __state)
        {
            __state = int.MinValue;
            try
            {
                if (__0 == null || !Measure || OffMain()) return;
                __state = __0.Gold;
                _ntHero = __0; _ntCurOutK = _ntCurInK = 0;   // nawias pasma (recenzja E5/0 (1-2)): przelewy z kasa osady licza sie w OnGold
                _ntAll++;
                var st = __0.CurrentSettlement ?? __0.HomeSettlement;
                if (st != null && st.IsVillage) _ntVill++;
            }
            catch (Exception e) { __state = int.MinValue; Stumble("NotablePre", e); }
        }

        public static void NotablePost(Hero __0, int __state)
        {
            try
            {
                if (__state == int.MinValue || __0 == null) return;
                long t = Stopwatch.GetTimestamp();
                long dl = (long)__0.Gold - __state;
                long outK = ReferenceEquals(_ntHero, __0) ? _ntCurOutK : 0, inK = ReferenceEquals(_ntHero, __0) ? _ntCurInK : 0;
                long rest = dl + outK - inK;   // zmiana kiesy bez przelewow z kasa osady (A3) = "z niczego / w nicosc" (jak okno O03 ksiegi)
                // scalenie porcji A (recenzja A3 R2-1 c): przy A3 "z niczego" ok. 0 - popyt "kasa miasta mogla dac" z kwoty naleznej A3 (zaplacone + uciete)
                long dueA3 = 0; try { if (NotableGold.On) dueA3 = NotableGold.BandDueNow(__0); } catch { }
                long want = Math.Max(0L, rest) + dueA3;
                if (dl != 0 || outK != 0 || inK != 0 || dueA3 > 0)
                {
                    var st = __0.CurrentSettlement ?? __0.HomeSettlement;
                    bool vill = st != null && st.IsVillage;
                    Settlement market = st;
                    if (vill) { var v = st.Village; market = v != null ? (v.TradeBound ?? v.Bound) : null; }
                    if (vill) { _ntVillOutK += outK; _ntVillInK += inK; } else { _ntTownOutK += outK; _ntTownInK += inK; }
                    if (rest < 0) { if (vill) _ntVillOut += -rest; else _ntTownOut += -rest; }
                    else if (rest > 0) { if (vill) _ntVillIn += rest; else _ntTownIn += rest; }
                    if (want > 0 && market != null && market.Town != null)
                    {
                        double[] dm;
                        if (!_ntDemand.TryGetValue(market, out dm)) { dm = new double[3]; _ntDemand[market] = dm; }
                        dm[vill ? 1 : 0] += want;
                        if (dueA3 > 0) dm[2] += inK;   // A3 juz zaplacil z tej kasy - wraca do "mogla dac" (kasa przed przelewem)
                    }
                    if (vill && market != null && market.IsCastle && (rest > 0 || inK > 0)) _ntCastleMarket++;
                }
                _patchTicks += Stopwatch.GetTimestamp() - t;
            }
            catch (Exception e) { Stumble("NotablePost", e); }
        }

        /// <summary>Finalizer BalanceGoldAndPowerOfNotable: zamkniecie nawiasu pasma (takze po wyjatku; void - wyjatek leci dalej bez zmian).</summary>
        public static void NotableFin(Exception __exception)
        {
            _ntHero = null; _ntCurOutK = _ntCurInK = 0;
        }

        /// <summary>MoneyLedger.VillageCashLine (przed ClearDay ksiegi): "bitwy" i pasmo notabli w ksiedze obiegu - kontrola linii Bitwy i Notable.</summary>
        internal static void TakeLedger(long battleIn, long battleOut, long bandIn, long bandOut)
        {
            _ledOk = true; _ledBattleIn = battleIn; _ledBattleOut = battleOut; _ledBandIn = bandIn; _ledBandOut = bandOut;
        }

        // ============================================================ odczyt BK (refleksja; wzor Measure169c.ResolvePop)
        private static bool _bkResolved;
        private static PropertyInfo _pCfg, _pPopMgr, _pPolMgrP, _pTotal, _pEcon, _pSat, _pConsumed, _pPolicies;
        private static MethodInfo _mGetPop, _mPopulated, _mTypeCount, _mGetPolicy;
        private static Type _popType;
        private static object _polMgr; private static IDictionary _polDict;

        private static void ResolveBk()
        {
            if (_bkResolved) return;
            _bkResolved = true;
            try
            {
                var cfgT = AccessTools.TypeByName("BannerKings.BannerKingsConfig");
                _pCfg = cfgT != null ? AccessTools.Property(cfgT, "Instance") : null;
                _pPopMgr = cfgT != null ? AccessTools.Property(cfgT, "PopulationManager") : null;
                _pPolMgrP = cfgT != null ? AccessTools.Property(cfgT, "PolicyManager") : null;
                var pmT = _pPopMgr != null ? _pPopMgr.PropertyType : null;
                _mGetPop = pmT != null ? AccessTools.Method(pmT, "GetPopData", new[] { typeof(Settlement) }) : null;
                _mPopulated = pmT != null ? AccessTools.Method(pmT, "IsSettlementPopulated", new[] { typeof(Settlement) }) : null;
                var pdT = _mGetPop != null ? _mGetPop.ReturnType : null;
                _mTypeCount = pdT != null ? AccessTools.Method(pdT, "GetTypeCount") : null;
                _popType = _mTypeCount != null && _mTypeCount.GetParameters().Length == 1 ? _mTypeCount.GetParameters()[0].ParameterType : null;
                _pTotal = pdT != null ? AccessTools.Property(pdT, "TotalPop") : null;
                _pEcon = pdT != null ? AccessTools.Property(pdT, "EconomicData") : null;
                _pSat = _pEcon != null ? AccessTools.Property(_pEcon.PropertyType, "Satisfactions") : null;
                _pConsumed = _pEcon != null ? AccessTools.Property(_pEcon.PropertyType, "ConsumedValue") : null;
                var polT = _pPolMgrP != null ? _pPolMgrP.PropertyType : null;
                _mGetPolicy = polT != null ? AccessTools.Method(polT, "GetPolicy", new[] { typeof(Settlement), typeof(string) }) : null;
                _pPolicies = polT != null ? AccessTools.Property(polT, "SettlementPolicies") : null;
            }
            catch (Exception e) { Stumble("ResolveBk", e); }
        }

        private static object Cfg() { try { return _pCfg != null ? _pCfg.GetValue(null, null) : null; } catch { return null; } }

        /// <summary>PopulationData osady - NAJPIERW IsSettlementPopulated (GetPopData dla osady bez danych losuje i zapisuje ludnosc w BK).</summary>
        private static object PopData(Settlement st)
        {
            ResolveBk();
            if (st == null || _mGetPop == null || _mPopulated == null || _pPopMgr == null) return null;
            var cfg = Cfg();
            var pm = cfg != null ? _pPopMgr.GetValue(cfg, null) : null;
            if (pm == null) return null;
            return (bool)_mPopulated.Invoke(pm, new object[] { st }) ? _mGetPop.Invoke(pm, new object[] { st }) : null;
        }

        private static object EconOf(Folk f)
        {
            if (f.Econ != null) return f.Econ;
            int today = (int)CampaignTime.Now.ToDays;
            if (f.EconDay == today) return null;   // brak danych BK - najwyzej jedna proba na dobe
            f.EconDay = today;
            try { var pd = PopData(f.St); f.Econ = pd != null && _pEcon != null ? _pEcon.GetValue(pd, null) : null; } catch (Exception e) { f.Econ = null; Stumble("EconOf", e); }
            return f.Econ;
        }

        private static float[] Sats(Folk f)
        {
            var e = EconOf(f);
            if (e == null || _pSat == null) return null;
            try { return _pSat.GetValue(e, null) as float[]; } catch (Exception x) { Stumble("Sats", x); return null; }
        }

        /// <summary>Polityka podatkowa BK osady - tylko dla osady, ktora ma juz polityki (GetPolicy dla osady bez polityk tworzy je w BK).</summary>
        private static void RefreshPolicies()
        {
            try
            {
                ResolveBk();
                var cfg = Cfg();
                var pm = cfg != null && _pPolMgrP != null ? _pPolMgrP.GetValue(cfg, null) : null;
                if (!ReferenceEquals(pm, _polMgr)) { _polMgr = pm; _polDict = pm != null && _pPolicies != null ? _pPolicies.GetValue(pm, null) as IDictionary : null; }
                if (_polMgr == null || _polDict == null || _mGetPolicy == null) return;
                foreach (var f in _f.Values)
                {
                    if (f.Kind != 2) continue;
                    try
                    {
                        if (!_polDict.Contains(f.St)) { f.Policy = 4; continue; }
                        var pol = _mGetPolicy.Invoke(_polMgr, new object[] { f.St, "tax" });
                        var type = pol != null ? Traverse.Create(pol).Property("Policy").GetValue() : null;
                        string n = type != null ? type.ToString() : null;
                        f.Policy = n == "Low" ? 0 : n == "Standard" ? 1 : n == "High" ? 2 : n == "Exemption" ? 3 : 4;
                    }
                    catch (Exception e) { f.Policy = 4; Stumble("RefreshPolicies(wies)", e); }
                }
            }
            catch (Exception e) { Stumble("RefreshPolicies", e); }
        }

        private static int CampaignStart(int day)
        {
            if (_campStart != int.MinValue) return _campStart;
            try { var ctm = Campaign.Current.Models.CampaignTimeModel; _campStart = ctm != null ? (int)ctm.CampaignStartTime.ToDays : day; } catch { _campStart = day; }
            return _campStart;
        }

        // ============================================================ kalibracja D1 (pierwsza doba sesji z PopulationLaw.Calibrated)
        private static void Calibrate(int day)
        {
            object[] types = PopTypes();
            int withBk = 0, bkTowns = 0, bkCastles = 0, bkVills = 0, noBk = 0;
            double tW = 0, tR = 0, tP = 0, tS = 0, tBook = 0;
            var slaveHit = new List<string>();
            var noData = new List<Folk>();
            foreach (var st in Settlement.All)
            {
                try
                {
                    if (st == null || !(st.IsTown || st.IsCastle || st.IsVillage)) continue;
                    var f = Get(st);
                    if (f == null || f.Cal) continue;
                    f.PeopleCal = f.Kind == 1 ? 0 : PopulationLaw.PeopleOf(st);
                    if (!ReadBk(f, types, slaveHit)) { noData.Add(f); f.Cal = true; noBk++; continue; }
                    withBk++;
                    if (f.Kind == 0) { bkTowns++; tW += f.HW; tR += f.HR; tP += f.HP; tS += f.HS; tBook += f.PeopleCal; }
                    else if (f.Kind == 1) bkCastles++;
                    else bkVills++;
                    f.Cal = true;
                }
                catch (Exception e) { Stumble("Calibrate(osada)", e); }
            }
            // osady bez danych BK (O4): mediana skali BK / ksiega osad tego samego rodzaju w krolestwie (zamek: mediana ludzi BK zamkow krolestwa), udzialy KL 1.2
            foreach (var f in noData)
            {
                try
                {
                    var k = f.St.OwnerClan != null ? f.St.OwnerClan.Kingdom : null;
                    var vals = new List<double>();
                    foreach (var g in _f.Values)
                    {
                        if (g.NoBk || !g.Cal || g.Kind != f.Kind) continue;
                        var gk = g.St.OwnerClan != null ? g.St.OwnerClan.Kingdom : null;
                        if (!ReferenceEquals(gk, k)) continue;
                        double bk = g.HW + g.HR + g.HP + g.HS;
                        if (f.Kind == 1) vals.Add(bk); else if (g.PeopleCal > 0) vals.Add(bk / g.PeopleCal);
                    }
                    if (vals.Count == 0) continue;
                    vals.Sort();
                    double med = vals[vals.Count / 2], total = f.Kind == 1 ? med : med * f.PeopleCal;
                    if (f.Kind == 0) { f.HW = total * (0.730 + (f.Slave ? 0 : 0.169)); f.HR = total * 0.079; f.HP = total * 0.022; f.HS = f.Slave ? total * 0.169 : 0; tW += f.HW; tR += f.HR; tP += f.HP; tS += f.HS; tBook += f.PeopleCal; }
                    else if (f.Kind == 1) f.HW = total * (0.80 + (f.Slave ? 0 : 0.12));
                    else f.HW = total * (0.77 + (f.Slave ? 0 : 0.20));
                }
                catch (Exception e) { Stumble("Calibrate(bez danych)", e); }
            }
            // zapasy startowe na sucho (R5, KL 5.3, sim seed 'dar'): kiesa klasy = dni zapasu pelnego koszyka (bez kotwicy - wydatku gry jeszcze nie ma)
            double sW = 0, sR = 0, sP = 0, sC = 0, sV = 0;
            foreach (var f in _f.Values)
            {
                if (!f.Cal) continue;
                double idx = f.Idx > 0 && !double.IsNaN(f.Idx) ? f.Idx : 1.0;
                if (f.Kind == 0)
                {
                    double bR = f.HR * BR * idx, bP = f.HP * BP * idx;
                    if (f.Slave) { double sl = f.HS * BS * idx; bR += sl * OwnR; bP += sl * OwnP; }
                    f.LW = RdW * f.HW * BW * idx / KeepW; f.LR = RdR * bR / KeepR; f.LP = RdP * bP / KeepP;
                    sW += f.LW; sR += f.LR; sP += f.LP;
                }
                else if (f.Kind == 1) { f.LW = RdC * f.HW * BC * idx; sC += f.LW; }
                else { f.LW = RdV * BV * (f.HW + f.HR); sV += f.LW; }
            }
            _calDone = true; _calDay = day;
            var s = Settings.Current;
            var foods = new List<string>();
            try { foreach (var c in ItemCategories.All) if (c != null && GroupOf(c) == 0) foods.Add(c.StringId); } catch { }
            int sTowns = 0, sCastles = 0, sVills = 0;
            foreach (var f in _f.Values) { if (!f.Cal) continue; if (f.Kind == 0) sTowns++; else if (f.Kind == 1) sCastles++; else sVills++; }
            bool liveNow = Live;   // E5/B1: kiesa czynna - kalibracja w zapisie (arm_townfolk), tu tylko osady bez kalibracji
            _lineSession = "Kiesa ludu (E5/0): sesja od dnia " + day + " - tryb " + (liveNow ? "kiesa czynna (E5/B1)" : "na sucho") + " (TownFolkMode " + (s != null ? s.TownFolkMode : -1) + ") | kalibracja D1: osad " + (sTowns + sCastles + sVills)
                + " (miasta " + sTowns + ", zamki " + sCastles + ", wsie " + sVills + "), z danymi BK " + withBk + " (miasta " + bkTowns + ", zamki " + bkCastles + ", wsie " + bkVills
                + "), bez danych " + noBk + " (mediana skali BK/ksiega) | ludzie BK miast " + N0(tW + tR + tP + tS) + " (W " + N0(tW) + " / R " + N0(tR) + " / P " + N0(tP) + " / niewolni utrzymywani " + N0(tS)
                + "), ksiega miast " + N0(tBook) + " | kultury z niewola (lista I1, osady): " + slaveHit.Count + " (" + (slaveHit.Count > 0 ? string.Join(", ", slaveHit.ToArray()) : "-")
                + ") | kategorie jedzenia: " + (foods.Count > 0 ? string.Join(", ", foods.ToArray()) : "-") + " | modele czynne: " + ActiveModels()
                + " | zapasy startowe na sucho " + N0(sW + sR + sP + sC + sV) + " zl (W " + N0(sW) + " / R " + N0(sR) + " / P " + N0(sP) + " / podzamcze " + N0(sC) + " / wsie " + N0(sV)
                + "; pelny koszyk bez kotwicy) | wlasciciele latek celow (sesja): " + Owners2() + (liveNow ? " | kalibracja i kiesy w zapisie (arm_townfolk); zapasy startowe na sucho tylko liczba." : " | stan tylko w pamieci sesji (bez zapisu).");
        }

        /// <summary>
        /// Klasy BK osady (faza a D1 - bez PeopleOf): kultura z niewola, ludzie wedlug klas BK. E5/B1: wolane tez przy zapasach startowych (R5) przed kalibracja
        /// ksiegi ludzi. false - osada bez danych BK (NoBk). Indeksy BK PopType: 0 szlachta, 1 rzemieslnicy, 2 chlopi, 3 niewolni, 5 dzierzawcy (Measure169c.ClassName).
        /// </summary>
        private static bool ReadBk(Folk f, object[] types, List<string> slaveHit)
        {
            var st = f.St;
            f.Slave = SlaveOf(st, slaveHit);
            long[] c = new long[6];
            object pd = null;
            try { pd = PopData(st); } catch (Exception e) { pd = null; Stumble("Calibrate(PopData)", e); }
            if (pd != null && types != null && _mTypeCount != null)
                for (int i = 0; i < 6; i++) { if (i == 4 || types[i] == null) continue; try { c[i] = Convert.ToInt64(_mTypeCount.Invoke(pd, new[] { types[i] })); } catch (Exception e) { Stumble("Calibrate(GetTypeCount)", e); } }
            long tot = c[0] + c[1] + c[2] + c[3] + c[5];
            if (pd == null || tot <= 0) { f.NoBk = true; return false; }
            f.NoBk = false;
            if (f.Kind == 0) { f.HP = c[0]; f.HR = c[1]; f.HW = c[2] + c[5] + (f.Slave ? 0 : c[3]); f.HS = f.Slave ? c[3] : 0; }
            else if (f.Kind == 1) f.HW = c[1] + c[2] + c[5] + (f.Slave ? 0 : c[3]);   // podzamcze: bez dworu pana (szlachta) i niewolnych kultur z niewola
            else { f.HW = c[2] + c[5] + (f.Slave ? 0 : c[3]); f.HR = c[1]; f.HP = c[0]; }   // wies: wolni (szlachta = majatki BK)
            return true;
        }

        /// <summary>Typy BK PopType (0..5) do GetTypeCount - raz na sesje.</summary>
        private static object[] PopTypes()
        {
            ResolveBk();
            if (_popType == null) return null;
            var types = new object[6];
            for (int i = 0; i < 6; i++) { try { types[i] = Enum.ToObject(_popType, i); } catch { types[i] = null; } }
            return types;
        }

        /// <summary>E5/B1 (plan pkt 0.13): osada kultury z niewola (lista I1/H3, LosersFlee.SlaveCulture - kultura biezaca osady); hit - id kultur trafionych (linia sesji).</summary>
        private static bool SlaveOf(Settlement st, List<string> hit)
        {
            var c = st != null ? st.Culture : null;
            if (!LosersFlee.SlaveCulture(c)) return false;
            if (hit != null && !hit.Contains(c.StringId)) hit.Add(c.StringId);
            return true;
        }

        private static string Owners2()
        {
            try
            {
                var t = new List<MethodBase>(); var n = new List<string>();
                void A(string name, MethodBase m) { if (m != null) { t.Add(m); n.Add(name); } }
                A("UpdateDemandShift", AccessTools.Method(typeof(ItemConsumptionBehavior), "UpdateDemandShift", new[] { typeof(Town), typeof(Dictionary<ItemCategory, float>) }));
                A("CalculateBudget", AccessTools.Method("BannerKings.Patches.EconomyPatches:CalculateBudget"));
                A("MakeConsumption", AccessTools.Method(typeof(ItemConsumptionBehavior), "MakeConsumption", new[] { typeof(Town), typeof(Dictionary<ItemCategory, float>), typeof(Dictionary<ItemCategory, int>) }));
                A("CommitGoldChanges", AccessTools.Method(typeof(MapEventParty), "CommitGoldChanges"));
                A("BalanceGoldAndPowerOfNotable", AccessTools.Method(typeof(NotablePowerManagementBehavior), "BalanceGoldAndPowerOfNotable", new[] { typeof(Hero) }));
                return Owners(t, n);
            }
            catch { return "?"; }
        }

        /// <summary>Modele czynne (RB 7 pkt 2 i 4): lojalnosc BK, warstwa dobrobytu BK na lancuchu klas (RBL wola base - k = 2 to delty BK liczone dwa razy), zywnosc.</summary>
        private static string ActiveModels()
        {
            try
            {
                var m = Campaign.Current.Models;
                object loy = m.SettlementLoyaltyModel, pro = m.SettlementProsperityModel, food = m.SettlementFoodModel;
                bool bkLoy = false;
                for (var t = loy != null ? loy.GetType() : null; t != null && t != typeof(object); t = t.BaseType) if (t.Name == "BKLoyaltyModel") { bkLoy = true; break; }
                int k = 0;
                for (var t = pro != null ? pro.GetType() : null; t != null && t != typeof(object); t = t.BaseType)
                {
                    bool hit = false;
                    foreach (var mi in t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                    {
                        if (mi.Name != "CalculateProsperityChange") continue;
                        var info = Harmony.GetPatchInfo(mi);
                        if (info != null) foreach (var p in info.Postfixes) if (p.owner == "BannerKings.BetterEconomyLayering") hit = true;
                    }
                    if (hit) k++;
                }
                bool def = food is DefaultSettlementFoodModel;
                return "lojalnosc " + (loy != null ? loy.GetType().FullName : "brak") + " (BKLoyaltyModel: " + (bkLoy ? "tak" : "nie") + "), dobrobyt " + (pro != null ? pro.GetType().FullName : "brak")
                       + " (postfiksow warstwy BK \"BannerKings.BetterEconomyLayering\" na lancuchu: " + k + "), zywnosc " + (food != null ? food.GetType().FullName : "brak")
                       + " (liczy jedzenie z targu: " + (def ? "tak" : "nie") + ")";
            }
            catch (Exception e) { return "blad odczytu (" + e.Message + ")"; }
        }

        // ============================================================ sonda korony (przed CrownIncome.End - DayOf czynne tylko do End)
        internal static void CrownProbe()
        {
            long t0 = Stopwatch.GetTimestamp();
            try
            {
                _cpOk = false;
                if (!On || Campaign.Current == null) return;
                var s = Settings.Current;
                _cpIncome = CrownIncome.On;
                _cpLoan = _cpOver = _cpReserve = 0; _cpLoanK = _cpLoan7K = _cpOverK = _cpLow = _cpLowWar = _cpNeed = _cpK = 0;
                foreach (var k in Kingdom.All)
                {
                    try
                    {
                        if (k == null || k.IsEliminated || k.StringId == null) continue;
                        _cpK++;
                        var kd = CrownIncome.DayOf(k);
                        long loan = kd != null ? kd.LoanIn : 0, due = kd != null ? kd.RefundDue : 0, meas = kd != null ? kd.Measured : 0;
                        CrownRing r;
                        if (!_crown.TryGetValue(k.StringId, out r)) { r = new CrownRing(); _crown[k.StringId] = r; }
                        r.Loan[r.H7] = loan > 0; r.H7 = (r.H7 + 1) % 7;
                        r.Due[r.H28] = due; r.H28 = (r.H28 + 1) % 28; if (r.F28 < 28) r.F28++;
                        long dsum = 0; for (int i = 0; i < r.F28; i++) dsum += r.Due[i];
                        double davg = r.F28 > 0 ? (double)dsum / r.F28 : 0;
                        long reserve = Math.Max((long)Math.Max(0, s != null ? s.CrownReserveGold : 0), (long)(60 * davg));
                        bool loan7 = false; for (int i = 0; i < 7; i++) if (r.Loan[i]) loan7 = true;
                        bool war = KingdomTreasury.AtWar(k);
                        long wallet = k.KingdomBudgetWallet;
                        _cpLoan += loan; if (loan > 0) _cpLoanK++; if (loan7) _cpLoan7K++;
                        if (due > meas) { _cpOver += due - meas; _cpOverK++; }
                        _cpReserve += reserve;
                        if (wallet < reserve) { _cpLow++; if (war) _cpLowWar++; }
                        if (war && (loan7 || due > meas || wallet < reserve)) _cpNeed++;
                    }
                    catch (Exception e) { Stumble("CrownProbe(krolestwo)", e); }
                }
                _cpOk = true;
            }
            catch (Exception e) { Stumble("CrownProbe", e); }
            finally { _dayTicks += Stopwatch.GetTimestamp() - t0; }
        }

        // ============================================================ linie dnia (po MoneyLedger.Daily, przed BeatenPurse.Daily i Seal31.Daily)
        internal static void Lines()
        {
            long t0 = Stopwatch.GetTimestamp();
            try
            {
                if ((!On && !LiveKey && _stateKey == null) || Campaign.Current == null) return;
                int day = (int)CampaignTime.Now.ToDays - 1;
                string l2 = null, l3 = null, l4 = null, l5 = null, l6 = null;
                var live = TakeLiveLines();   // E5/B1: stan, zapasy startowe, zwrot, robocizna, prawa panskie, R14
                if (!On) { foreach (var l in live) Log.Info(l); if (_lineFolk != null) Log.Info(_lineFolk + " | potkniecia " + _stumbles + " (sesja " + _stumbleSess + ")."); return; }
                try { l2 = SurplusLine(day); } catch (Exception e) { Stumble("SurplusLine", e); }
                try { l3 = BattleLine(day); } catch (Exception e) { Stumble("BattleLine", e); }
                try { l4 = NotableLine(day); } catch (Exception e) { Stumble("NotableLine", e); }
                try { l5 = FoodLine(day); } catch (Exception e) { Stumble("FoodLine", e); }
                try { l6 = SatLine(day); } catch (Exception e) { Stumble("SatLine", e); }
                long own = Stopwatch.GetTimestamp() - t0;
                if (_lineSession != null) { Log.Info(_lineSession); _lineSession = null; }
                if (_lineFolk != null)
                    Log.Info(_lineFolk + " | czas: latki " + Ms(_patchTicks) + " ms, doba " + Ms(_dayTicks + own) + " ms | potkniecia " + _stumbles
                             + " (sesja " + _stumbleSess + "; poza watkiem glownym " + _offThread + "; nawias konsumpcji: niezamkniete " + _brUnclosed + ", obca osada " + _brMismatch
                             + ", przerwane wyjatkiem " + _brAborted + ").");
                foreach (var l in live) Log.Info(l);
                foreach (var l in new[] { l2, l3, l4, l5, l6 }) if (l != null) Log.Info(l);
            }
            catch (Exception e) { Stumble("Lines", e); }
            finally { ZeroLineDay(); }
        }

        private static void ZeroLineDay()
        {
            _patchTicks = _dayTicks = 0; _stumbles = 0; _offThread = 0; _brUnclosed = _brMismatch = _brAborted = 0;
            _lineFolk = null;
            Array.Clear(_bPaid, 0, BSrc); Array.Clear(_bBelow, 0, BSrc); _unsold = 0; _unsoldVisits = 0;
            Array.Clear(_take, 0, Takes); Array.Clear(_takeBelow, 0, Takes); _rentPaid = _rentBelow = 0;
            _foodTown = _foodCastle = 0; Array.Clear(_satPlus, 0, 4); Array.Clear(_satMinus, 0, 4);
            _btN = _btN1 = _btN2 = _btN3 = _btOffN = 0; _btA = _btB = _btC1 = _btC2 = _btC3 = _btAOff = _btC1Off = 0;
            _ntTownOut = _ntTownIn = _ntVillOut = _ntVillIn = 0; _ntAll = _ntVill = _ntCastleMarket = 0; _ntDemand.Clear();
            _ntTownOutK = _ntTownInK = _ntVillOutK = _ntVillInK = 0;
            _ledOk = false; _cpOk = false;
            ClearVillageCash();
        }

        private static string SurplusLine(int day)
        {
            var s = Settings.Current;
            long floor = (long)Math.Max(0f, s != null ? s.TownRentFloorGold : 20000f);
            long gold = 0, res = 0, over = 0, held = 0, court = 0; int overN = 0, lowN = 0, towns = 0, below = 0, b1000 = 0, b0 = 0, w7 = 0, w28 = 0;
            foreach (var t in Town.AllTowns)
            {
                if (t == null || !t.IsTown || t.Settlement == null) continue;
                towns++;
                long g = t.Gold, r = Seal31.TownReserve(t);
                gold += g; res += r;
                if (g > r) { over += g - r; overN++; }
                if (g < floor) lowN++;
                held += SoldierPay.HeldOf(t.Settlement); court += SoldierPay.CourtOf(t.Settlement);
                // zaulki (Jeff 16:10): doby z rzedu ponizej zapasu kupcow (od startu sesji)
                var f = Get(t.Settlement);
                if (g < r) { below++; if (f != null) { f.Streak++; if (f.Streak > _streakMax) { _streakMax = f.Streak; _streakName = t.Settlement.Name != null ? t.Settlement.Name.ToString() : t.Settlement.StringId; } } }
                else if (f != null) f.Streak = 0;
                if (g < 1000) b1000++;
                if (g <= 0) b0++;
                if (f != null && f.Streak >= 7) w7++;
                if (f != null && f.Streak >= 28) w28++;
            }
            long cGold = 0, cRes = 0, cOver = 0; int castles = 0;
            foreach (var st in Settlement.All)
            {
                if (st == null || !st.IsCastle || st.Town == null) continue;
                castles++;
                long g = st.Town.Gold, r = CastlePurse.Reserve(st.Town);
                cGold += g; cRes += r; if (g > r) cOver += g - r;
            }
            var sb = new StringBuilder(2048);
            sb.Append("Nadwyzka kas (E5/0): dzien ").Append(day)
              .Append(" | miasta ").Append(towns).Append(": kasy ").Append(gold).Append(", zapas kupcow (D4) ").Append(res).Append(", ponad zapasem ").Append(over).Append(" w ").Append(overN)
              .Append(" miastach, ponizej dna ").Append(floor).Append(": ").Append(lowN).Append(", pod tarczami ").Append(held + court).Append(" (zold ").Append(held).Append(", dwor ").Append(court).Append(")")
              .Append(" | zamki ").Append(castles).Append(": kasy ").Append(cGold).Append(", zapas (CastlePurse.Reserve) ").Append(cRes).Append(", ponad ").Append(cOver)
              .Append(" | z pasa ponizej zapasu (miasta): renta ").Append(_rentBelow).Append(" z ").Append(_rentPaid);
            for (int k = 1; k < Takes; k++) sb.Append(", ").Append(TakeName[k]).Append(' ').Append(_takeBelow[k]).Append(" z ").Append(_take[k]);
            sb.Append(", RealPayer i porty 0 z definicji (Seal31.Spare)")
              .Append(" | zakupy kas miast z pasa ponizej zapasu: nasze sciezki ").Append(_bBelow[BOurs]).Append(" z ").Append(_bPaid[BOurs]).Append(" (po A1 = 0)").Append(", zwroty i likwidacje (bez reguly A1) ").Append(_bBelow[BBack]).Append(" z ").Append(_bPaid[BBack])
              .Append(", gry/BK ").Append(_bBelow[BCarts] + _bBelow[BCaravans]).Append(" (tabory ").Append(_bBelow[BCarts]).Append(" z ").Append(_bPaid[BCarts])
              .Append(", karawany ").Append(_bBelow[BCaravans]).Append(" z ").Append(_bPaid[BCaravans]).Append(")")
              .Append(", inne przelewy poza naszym tickiem ").Append(_bBelow[BOther]).Append(" z ").Append(_bPaid[BOther]);
            long cut = KingdomTreasury.LastOwnCut;
            sb.Append(" | K3: ciecie zwrotu dzis ").Append(cut).Append(" (CrownRefundOwnTownsCut ").Append(s != null ? s.CrownRefundOwnTownsCut.ToString("0.##", Inv) : "-")
              .Append("), przy A1 ok. ").Append((long)(cut * 0.67)).Append(", czesc pana z zaworu A1 (7% D4 x 0.67) ").Append((long)(0.07 * over * 0.67));
            sb.Append(" | korona: ");
            if (!_cpOk) sb.Append("- (sonda korony nie biegla)");
            else
                sb.Append(_cpIncome ? "" : "(165 wylaczone - wplywy i zwrot 0) ").Append("kredyt 186 dzis ").Append(_cpLoan).Append(" (krolestw ").Append(_cpLoanK).Append(", w 7 dobach ").Append(_cpLoan7K).Append(")")
                  .Append(", zwrot nalezny ponad wplywy (bez 1/360) ").Append(_cpOver).Append(" (krolestw ").Append(_cpOverK).Append(")")
                  .Append(", zapas wojny (max(").Append(s != null ? s.CrownReserveGold : 0).Append(", 60 dob sr28 zwrotu)) razem ").Append(_cpReserve)
                  .Append(", krolestw ze skarbcem ponizej: ").Append(_cpLow).Append(" z ").Append(_cpK).Append(" (w wojnie ").Append(_cpLowWar).Append(")")
                  .Append(", potrzeba R15 dzis: ").Append(_cpNeed).Append(" krolestw");
            sb.Append(" | zaulki (od startu sesji): kas ponizej zapasu ").Append(below).Append(", ponizej 1 000 ").Append(b1000).Append(", przy 0 ").Append(b0)
              .Append("; najdluzej ponizej zapasu ").Append(_streakMax).Append(" dob z rzedu (").Append(_streakName ?? "-").Append("), miast >= 7 dob ").Append(w7).Append(", >= 28 dob ").Append(w28)
              .Append("; towar wsi niesprzedany w miastach ").Append(_unsold).Append(" szt. w ").Append(_unsoldVisits).Append(" wizytach");
            // udzial zysku kupcow zabierany przez pana i korone (Jeff 16:10) - okno Daily - Daily, liczby z TownFolk.Daily tej doby
            double t28 = 0, i28 = 0, i28b = 0;
            for (int i = 0; i < _sFill; i++) { t28 += _sTake[i]; i28 += _sIn[i]; i28b += _sIn2[i]; }
            sb.Append(" | udzial zysku kupcow zabierany przez pana i korone: ");
            if (!_sDayOk) sb.Append("dzis - (pierwsza doba sesji albo przed kalibracja)");
            else sb.Append("dzis ").Append(Pct(_sDayTake, _sDayIn)).Append(" (pobrano ").Append(N0(_sDayTake)).Append(" z doplywu netto ").Append(N0(_sDayIn)).Append("; miast ").Append(_sDayN).Append(")");
            sb.Append(", srednia 28 dob ").Append(Pct(t28, i28)).Append(" (z ").Append(_sFill).Append(" dob)")
              .Append("; z \"zakupami\" mieszczan jako sprzedaza (stan po B1 - placi lud): dzis ").Append(_sDayOk ? Pct(_sDayTake, _sDayIn2) : "-").Append(", srednia 28 dob ").Append(Pct(t28, i28b))
              .Append(" (doplyw = zmiana kasy + pobrane - \"zakupy\" z niczego ").Append(_sDayOk ? N0(_sDayCons) : "-").Append(" - regulator netto ").Append(_sDayOk ? N0(_sDayReg) : "-")
              .Append("; pobrane: renta, danina, clo z kasy, mennica, Crown Duty, War Tax, Road Tolls) - odniesienie historyczne ok. 10% (INFO)")
              .Append('.');
            return sb.ToString();
        }

        private static string BattleLine(int day)
        {
            // a i c1: wodz w partii z roznicy kiesy + zdarzenia innych bohaterow w nawiasie (A6: wodz zdjety z partii placi swoja czesc puli)
            long a = _btA + _btAOff, c1 = _btC1 + _btC1Off;
            long c = c1 + _btC2, d = Measure31.DayShipBattleGold, e = c - a - _btB - d;
            var sb = new StringBuilder(640);
            sb.Append("Bitwy (E5/0): dzien ").Append(day).Append(" | partii ").Append(_btN).Append(" (wodzowie ").Append(_btN1).Append(", partie lordow z wodzem zdjetym (niewola, ucieczka) ").Append(_btN3)
              .Append(", bez wodza ").Append(_btN2).Append(")")
              .Append(" | pula z pokonanych (zeszlo z kies): wodzowie ").Append(a).Append(" (w partii ").Append(_btA).Append(", zdjeci z partii - A6 - ").Append(_btAOff).Append(" w ").Append(_btOffN)
              .Append("), partie bez wodza ").Append(_btB)
              .Append(" | zwyciezcy dostali ").Append(c).Append(" (wodzowie ").Append(c1).Append(", partie z kiesa ").Append(_btC2).Append("), przepadlo ").Append(_btC3)
              .Append(" | statki zatopione ").Append(d)
              .Append(" | z niczego netto na ladzie ").Append(e).Append(" = c - a - b - d (morze ").Append(d).Append(" osobno)")
              .Append(" | kontrola z ksiega: ");
            if (_ledOk) sb.Append("zrodla \"bitwy\" ").Append(_ledBattleIn).Append(" (c1 - x = ").Append(c1 - _ledBattleIn).Append("), ujscia ").Append(_ledBattleOut).Append(" (a - y = ").Append(a - _ledBattleOut).Append(")");
            else sb.Append("- (ksiega obiegu nie wypisala linii)");
            return sb.Append('.').ToString();
        }

        private static string NotableLine(int day)
        {
            double b1 = 0, e = 0;
            foreach (var kv in _ntDemand)
            {
                var st = kv.Key; var dm = kv.Value;
                if (st == null || st.Town == null) continue;
                long g = st.Town.Gold, r = st.IsTown ? Seal31.TownReserve(st.Town) : CastlePurse.Reserve(st.Town);
                double spare = Math.Max(0, g - r) + dm[2], want = dm[0] + dm[1];   // scalenie porcji A: + to, co A3 juz zaplacil z tej kasy dzis
                if (want <= 0) continue;
                double cover = Math.Min(spare, want);
                b1 += cover * dm[0] / want; e += cover * dm[1] / want;
            }
            var sb = new StringBuilder(512);
            // recenzja E5/0 (1-2): "w nicosc / z niczego" = zmiana kiesy notabla bez przelewow z kasa osady; przelewy z kasa (A3) osobno
            sb.Append("Notable (E5/0): dzien ").Append(day).Append(" | pasmo 4 500-10 500 - miasta: w nicosc ").Append(_ntTownOut).Append(" / z niczego ").Append(_ntTownIn)
              .Append(", do kasy osady ").Append(_ntTownOutK).Append(" / z kasy osady ").Append(_ntTownInK)
              .Append(" (kasa miasta mogla dac ").Append(N0(b1)).Append("); wsie: w nicosc ").Append(_ntVillOut).Append(" / z niczego ").Append(_ntVillIn)
              .Append(", do kasy osady targowej ").Append(_ntVillOutK).Append(" / z kasy osady targowej ").Append(_ntVillInK)
              .Append(" (kasa miasta targowego mogla dac ").Append(N0(e)).Append("; targ - zamek: ").Append(_ntCastleMarket).Append(" notabli)")
              .Append(" | notable wiejscy ").Append(_ntVill).Append(" z ").Append(_ntAll)
              .Append(" | kontrola z ksiega (Obieg: notable - tylko z niczego / w nicosc): ");
            if (_ledOk) sb.Append("w nicosc ").Append(_ntTownOut + _ntVillOut).Append(" wobec ").Append(_ledBandOut).Append(", z niczego ").Append(_ntTownIn + _ntVillIn).Append(" wobec ").Append(_ledBandIn);
            else sb.Append("- (ksiega obiegu nie wypisala linii)");
            return sb.Append('.').ToString();
        }

        private static string FoodLine(int day)
        {
            double stocks = 0; int forts = 0;
            long shelf = 0; double use = 0; int low = 0, noUse = 0, castles = 0;
            var days = new List<double>();
            foreach (var st in Settlement.All)
            {
                if (st == null || st.Town == null || !(st.IsTown || st.IsCastle)) continue;
                forts++; stocks += st.Town.FoodStocks;
                if (!st.IsCastle) continue;
                castles++;
                long n = 0;
                try
                {
                    var r = st.ItemRoster;
                    for (int i = 0; r != null && i < r.Count; i++)
                    {
                        var el = r.GetElementCopyAtIndex(i);
                        var it = el.EquipmentElement.Item;
                        if (it != null && el.Amount > 0 && it.ItemCategory != null && GroupOf(it.ItemCategory) == 0) n += el.Amount;
                    }
                }
                catch (Exception e) { Stumble("FoodLine(polka)", e); }
                shelf += n;
                Folk f; double avg = 0;
                if (_f.TryGetValue(st, out f) && f.Food7Fill > 0) { long sum = 0; for (int i = 0; i < f.Food7Fill; i++) sum += f.Food7[i]; avg = (double)sum / f.Food7Fill; }
                use += avg;
                if (avg <= 0) { noUse++; continue; }
                double dd = n / avg; days.Add(dd);
                if (dd < 10) low++;
            }
            days.Sort();
            return "Jedzenie z targu (E5/0): dzien " + day + " | do zapasu warowni z konsumpcji: miasta " + _foodTown + ", zamki " + _foodCastle + " (szt.; SoldItems jedzenia)"
                   + " | zapas warowni (FoodStocks) razem " + N0(stocks) + ", warowni " + forts
                   + " | zamki: jedzenie na polce targu " + shelf + " szt., zuzycie podzamcza " + F1(use) + " szt./d (srednia 7 dob), polka < 10 dni zuzycia: " + low + " z " + castles
                   + " (mediana " + (days.Count > 0 ? F1(days[days.Count / 2]) : "-") + " dni; zamki bez zuzycia " + noUse + ").";
        }

        private static readonly int[] SatOrder = { 3, 1, 2, 0 };   // BK ConsumptionType: Luxury 0, Industrial 1, General 2, Food 3 - w linii Food / Industrial / General / Luxury
        private static readonly string[] SatName = { "Luxury", "Industrial", "General", "Food" };

        private static string SatLine(int day)
        {
            var sT = new double[4]; var sC = new double[4]; var low = new int[4]; int nT = 0, nC = 0, nLow = 0;
            foreach (var f in _f.Values)
            {
                if (f.Kind == 2) continue;
                var sat = Sats(f);
                if (sat == null || sat.Length < 4) continue;
                bool any = false;
                for (int i = 0; i < 4; i++) { if (f.Kind == 0) sT[i] += sat[i]; else sC[i] += sat[i]; if (sat[i] < 0.85f) { low[i]++; any = true; } }
                if (f.Kind == 0) nT++; else nC++;
                if (any) nLow++;
            }
            var sb = new StringBuilder(512);
            sb.Append("Zadowolenie BK (E5/0): dzien ").Append(day).Append(" | srednie Food / Industrial / General / Luxury (0..1) miasta ");
            for (int j = 0; j < 4; j++) { if (j > 0) sb.Append(" / "); sb.Append(nT > 0 ? (sT[SatOrder[j]] / nT).ToString("0.000", Inv) : "-"); }
            sb.Append(" (").Append(nT).Append(") / zamki ");
            for (int j = 0; j < 4; j++) { if (j > 0) sb.Append(" / "); sb.Append(nC > 0 ? (sC[SatOrder[j]] / nC).ToString("0.000", Inv) : "-"); }
            sb.Append(" (").Append(nC).Append("), osad ponizej 0.85: ").Append(nLow).Append(" (");
            for (int j = 0; j < 4; j++) { if (j > 0) sb.Append(", "); sb.Append(SatName[SatOrder[j]]).Append(' ').Append(low[SatOrder[j]]); }
            sb.Append(") | zmiana dnia w konsumpcji: ");
            for (int j = 0; j < 4; j++) { if (j > 0) sb.Append(", "); int i = SatOrder[j]; sb.Append(SatName[i]).Append(" +").Append(_satPlus[i].ToString("0.000", Inv)).Append(" / -").Append(_satMinus[i].ToString("0.000", Inv)); }
            sb.Append(" (BK +0.001 za wiersz z budzetem, -0.0015 tylko w dokupie jedzenia - przyblizone przez obciecie 0..1).");
            return sb.ToString();
        }
    }
}
