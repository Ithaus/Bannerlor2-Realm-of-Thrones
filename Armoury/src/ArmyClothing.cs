using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// ODZIEZ, BUTY I PLOTNO WOJSKA (paczka 150, plan K13 07.10 - docs/PLAN-K13-2026-10-07.md krok "130" - roboczy).
    ///
    /// Stan przed paczka: wojsko (dzis ok. 166 tys. ludzi: partie rodow ok. 102-106 tys., garnizony ok. 61 tys.) nie zuzywa ani
    /// skory, ani sukna, ani plotna. Zold idzie do sakiewek ludzi (SoldierPay -> MenPurse) i przy wyjezdzie z miasta prawie caly
    /// znika "na zycie" (165-200 tys. d dziennie) bez zadnego towaru; zaopatrzenie BK w tekstylia (PartySupplies, kategorie welna,
    /// plotno, len - 0.01 sztuki na zolnierza dziennie) prawie nic nie kupuje (BuyItems odejmuje od potrzeby zapas MIASTA) i placi
    /// z kiesy lorda w nicosc. Polki skory, plotna i sukna puchna, bo jedynym odbiorca sa mieszczanie (budzet BK).
    ///
    /// Regula (wlacznik ArmyClothingEnabled; jedna regula odziezy dla 100% ludzi na zoldzie):
    ///  - kazdy zolnierz partii rodu (takze gracza i jego rodu; bez bohaterow i bez nieumarlych) zdziera codziennie odziez wedle stawki
    ///    "w polu", kazdy zolnierz garnizonu (bez nieumarlych), ktoremu dzis naprawde zaplacono zold do kasy osady (SoldierPay.Route),
    ///    wedle stawki "w garnizonie" (ulamek: zaplacone / naliczone). Stawki w kg na czlowieka na rok, przeliczone na sztuki przez wage sztuki z gry
    ///    (skora, filc = sukno, plotno - po 10 kg) i rok 364 dni (zuzycie to fizyka dnia, nie dlugosc roku kalendarza);
    ///  - potrzeba narasta w ulamkach, kupuje sie cale sztuki. Partia: w miescie przy wyjezdzie (MenPurse.OnLeft), z polki miasta,
    ///    sztuka po sztuce po cenie targu dla tej partii (cena rosnie, gdy polka sie oproznia - gra liczy ja od zapasu), najtansza sztuka
    ///    towaru, kolejno skora / sukno / plotno po jednej, z SAKIEWKI LUDZI ponad rezerwe na zalegle naprawy i PRZED wydatkiem "na
    ///    zycie" - to samo zloto, mniej idzie "na zycie"; zaplata do kasy miasta (z tarcza zoldu, jak "zycie");
    ///  - garnizon miasta bierze z polki SWOJEGO miasta bez zlota: jego zold wplynal juz dzis do kasy tego miasta (SoldierPay.Route:
    ///    town.ChangeGold(zaplacone) zaraz przed nami) - kupcy maja zloto, zaloga sukno;
    ///  - garnizon zamku: kasa zamku placi kasie miasta, z ktorym handluja wsie zamku (TradeBound gry; brak - najblizsze miasto,
    ///    z ktorym zamek nie jest w wojnie), po cenie targu tego miasta; zamek albo miasto oblezone - czeka;
    ///  - brak towaru albo pieniedzy: potrzeba czeka, najwyzej ArmyClothingMaxWaitDays (120) dob zuzycia przy dzisiejszej liczbie
    ///    ludzi; nadwyzka ponad ten sufit przepada (ludzie chodza w lachmanach - tylko wpis w logu, bez kary w grze);
    ///  - zaopatrzenie BK w tekstylia = 0 (BkSupplyTemper.ClothZeroPostfix): jedna regula odziezy, bez drugiego liczenia.
    /// Sztuki znikaja (zdarte ubranie zastepuje nowe - nic nie trafia do jukow). Zloto tylko miedzy posiadaczami (sakiewki ludzi ->
    /// kasy miast, kasy zamkow -> kasy miast) - "Pieniadz swiata" bez zmian sumy; MoneyLedger widzi to jako pozycje "odziez wojska".
    /// Wylaczone: nic nie narasta i nic nie jest kupowane (zapisana potrzeba zostaje w zapisie), BK tekstylia jak dotad.
    /// </summary>
    internal static class ArmyClothing
    {
        internal static bool On { get { var s = Settings.Current; return s != null && s.ArmyClothingEnabled; } }

        internal const int Leather = 0, Cloth = 1, Linen = 2, Goods = 3;
        private static readonly string[] Ids = { "leather", "felt", "linen" };
        private static readonly string[] PlName = { "skora", "sukno", "plotno" };
        private static readonly string[] EnOne = { "crate of leather", "roll of felt", "roll of linen" };
        private static readonly string[] EnMany = { "crates of leather", "rolls of felt", "rolls of linen" };
        private const float DaysPerYear = 364f;          // rok historyczny (13 tygodni na pore x 4 pory); zuzycie liczone na dobe fizycznie
        private const string GarrisonKey = "@";          // klucz potrzeby garnizonu: "@" + id osady (zaloga ginie i powstaje, osada zostaje)

        private static ItemObject[] _items;               // skora, filc, plotno - raz na sesje (Reset czysci)
        private static readonly Dictionary<string, float[]> _need = new Dictionary<string, float[]>();   // sztuki czekajace na zakup

        // ------------------------------------------------------------ liczniki doby (linia "Odziez wojska (150):")
        private static long _dFieldMen, _dGarMen;
        private static int _dFieldN, _dGarTownN, _dGarCastleN;
        private static readonly float[] _dAccrued = new float[Goods], _dRagged = new float[Goods];
        private static readonly int[] _dParty = new int[Goods], _dTown = new int[Goods], _dCastle = new int[Goods];
        private static long _dPartyGold, _dTownWorth, _dCastleGold;
        private static int _dVisits, _dPlayerVisits, _dNoGoods, _dNoPurse, _dCastleNoGold, _dCastleWait, _dCastleNoTown, _dStumbles, _dGarUndead;
        private static readonly HashSet<string> _dCastleMarkets = new HashSet<string>(), _dRaggedKeys = new HashSet<string>();
        private static readonly HashSet<string> _errOnce = new HashSet<string>();
        internal static int BkClothZeroed;                // BkSupplyTemper: zapisana potrzeba tekstyliow BK wyzerowana (partii dzis)

        internal static void Reset()
        {
            _items = null; _need.Clear(); _errOnce.Clear();
            ClearDay();
        }

        private static void ClearDay()
        {
            _dFieldMen = _dGarMen = 0; _dFieldN = _dGarTownN = _dGarCastleN = 0;
            Array.Clear(_dAccrued, 0, Goods); Array.Clear(_dRagged, 0, Goods);
            Array.Clear(_dParty, 0, Goods); Array.Clear(_dTown, 0, Goods); Array.Clear(_dCastle, 0, Goods);
            _dPartyGold = _dTownWorth = _dCastleGold = 0;
            _dVisits = _dPlayerVisits = _dNoGoods = _dNoPurse = _dCastleNoGold = _dCastleWait = _dCastleNoTown = 0; _dStumbles = 0; _dGarUndead = 0;
            _dCastleMarkets.Clear(); _dRaggedKeys.Clear();
            BkClothZeroed = 0;
        }

        /// <summary>Wyjatek przy jednej partii albo osadzie: pierwszy z danego miejsca do pliku, kazdy liczony w linii dnia (regula dziala dalej).</summary>
        internal static void Stumble(string where, Exception e)
        {
            _dStumbles++;
            if (_errOnce.Add(where)) Log.Error(where, e);
        }

        private static bool Items()
        {
            if (_items != null) return true;
            var mgr = MBObjectManager.Instance;
            if (mgr == null) return false;
            var a = new ItemObject[Goods];
            for (int g = 0; g < Goods; g++) a[g] = mgr.GetObject<ItemObject>(Ids[g]);
            _items = a;
            return true;
        }

        // ------------------------------------------------------------ rachunek (czyste funkcje)
        /// <summary>Sztuk dziennie dla `men` ludzi przy stawce kg na czlowieka na rok i wadze sztuki (kg).</summary>
        internal static float PerDay(float kgPerYear, int men, float weight)
        {
            if (kgPerYear <= 0f || men <= 0) return 0f;
            return kgPerYear * men / DaysPerYear / (weight > 0f ? weight : 10f);
        }

        private static float Rate(Settings s, int g, bool field)
        {
            if (field) return g == Leather ? s.ArmyClothingFieldLeatherKg : g == Cloth ? s.ArmyClothingFieldClothKg : s.ArmyClothingFieldLinenKg;
            return g == Leather ? s.ArmyClothingGarrisonLeatherKg : g == Cloth ? s.ArmyClothingGarrisonClothKg : s.ArmyClothingGarrisonLinenKg;
        }

        private static float Weight(int g) { var it = _items != null ? _items[g] : null; return it != null && it.Weight > 0f ? it.Weight : 10f; }

        /// <summary>Doba zuzycia: potrzeba rosnie o stawke x ludzi x czesc doby na zoldzie; ponad sufit (MaxWaitDays dob przy dzisiejszej liczbie ludzi) przepada.</summary>
        private static float[] Accrue(string key, int men, bool field, float share)
        {
            var s = Settings.Current;
            float[] need;
            if (!_need.TryGetValue(key, out need)) { need = new float[Goods]; _need[key] = need; }
            float wait = Math.Max(1, s.ArmyClothingMaxWaitDays);
            bool ragged = false;
            for (int g = 0; g < Goods; g++)
            {
                if (_items[g] == null) continue;
                float day = PerDay(Rate(s, g, field), men, Weight(g));
                float add = day * share;
                need[g] += add; _dAccrued[g] += add;
                float cap = day * wait;
                if (need[g] > cap) { _dRagged[g] += need[g] - cap; need[g] = cap; ragged = true; }
            }
            if (ragged) _dRaggedKeys.Add(key);
            return need;
        }

        // ------------------------------------------------------------ partie (DailyTickPartyEvent)
        internal static void OnDailyTickParty(MobileParty mp)
        {
            try
            {
                if (!On || mp == null || !mp.IsActive || !(mp.IsLordParty || mp.IsMainParty) || mp.StringId == null) return;
                if (Undead.Party(mp)) return;                                   // trup nie zdziera butow (i nie ma sakiewki)
                int men = mp.MemberRoster != null ? mp.MemberRoster.TotalRegulars : 0;   // bez bohaterow - lord ubiera sie z wlasnej kiesy
                if (men <= 0 || !Items()) return;
                Accrue(mp.StringId, men, true, 1f);
                _dFieldMen += men; _dFieldN++;
            }
            catch (Exception e) { Stumble("ArmyClothing.OnDailyTickParty", e); }
        }

        internal static void OnPartyDestroyed(MobileParty mp, PartyBase destroyer)
        {
            try { if (mp != null && mp.StringId != null) _need.Remove(mp.StringId); }
            catch (Exception e) { Stumble("ArmyClothing.OnPartyDestroyed", e); }
        }

        /// <summary>
        /// MenPurse.OnLeft: partia wyjezdza z miasta - kupuje cale sztuki potrzeby z polki miasta po cenie targu, z sakiewki ludzi,
        /// najwyzej `budget` (sakiewka ponad rezerwe na naprawy). Zwraca wydane zloto (juz zdjete z sakiewki i wplacone kasie miasta).
        /// </summary>
        internal static int BuyForParty(MobileParty mp, Settlement st, int budget)
        {
            try
            {
                if (!On || mp == null || st == null || !st.IsTown || st.Town == null || budget <= 0 || mp.StringId == null) return 0;
                float[] need;
                if (!_need.TryGetValue(mp.StringId, out need) || !Wants(need) || !Items()) return 0;
                var got = new int[Goods];
                bool lacked, poor;
                int spent = Buy(st, mp, need, budget, got, false, out lacked, out poor);
                _dVisits++;
                if (lacked) _dNoGoods++;
                if (poor) _dNoPurse++;
                if (spent <= 0) return 0;
                int taken = MenPurse.Take(mp, spent);                            // spent <= budget <= sakiewka - taken == spent
                st.Town.ChangeGold(taken);                                      // kupcy miasta dostaja zloto, ktore inaczej poszloby "na zycie"
                MoneyLedger.Note(MoneyLedger.NCloth, st, taken);                // ksiega przeplywow osad (tylko licznik)
                SoldierPay.Hold(st, taken);                                     // tarcza zoldu: jak wydatek "na zycie"
                for (int g = 0; g < Goods; g++) _dParty[g] += got[g];
                _dPartyGold += taken;
                if (mp.IsMainParty)
                {
                    _dPlayerVisits++;
                    Log.Player("Your men bought " + List(got) + " in " + st.Name + " for " + taken + " denars from their purse - shoes, clothes and tents worn out on the march.");
                    Log.Info("Odziez wojska (150): gracz w " + st.Name + " - kupili " + Pl(got) + " za " + taken + " z sakiewki ludzi; czeka " + Pl(need) + ".");
                }
                return taken;
            }
            catch (Exception e) { Stumble("ArmyClothing.BuyForParty", e); return 0; }
        }

        // ------------------------------------------------------------ garnizony (SoldierPay.Route - zold zaloga juz w kasie osady)
        /// <summary>
        /// SoldierPay.Route: zaloga `garrison` dostala dzis `paid` z naliczonych `wage` - jej zold juz wplynal do kasy osady `st`.
        /// Doba zuzycia (wedle czesci zaplaconej), potem: miasto - z polki swojego miasta bez zlota; zamek - kasa zamku kupuje w miescie handlowym.
        /// </summary>
        internal static void OnGarrisonPaid(MobileParty garrison, Settlement st, int paid, int wage)
        {
            try
            {
                if (!On || garrison == null || st == null || st.Town == null || st.StringId == null || paid <= 0 || wage <= 0) return;
                if (!st.IsTown && !st.IsCastle) return;
                int men = garrison.MemberRoster != null ? garrison.MemberRoster.TotalRegulars : 0;
                if (men <= 0 || !Items()) return;
                // recenzja: ta sama regula co w partiach - trup nie zdziera butow. Zold zalogi Innych SoldierPay wplaca do kasy osady
                // jak kazdej (107); odziezy ta zaloga nie zuzywa - nic z polki miasta i nic z kasy zamku
                if (Undead.Party(garrison)) { _dGarUndead++; return; }
                float share = Math.Min(1f, (float)paid / wage);
                var need = Accrue(GarrisonKey + st.StringId, men, false, share);
                _dGarMen += men;
                var got = new int[Goods];
                bool lacked, poor;
                if (st.IsTown)
                {
                    _dGarTownN++;
                    if (!Wants(need)) return;
                    int worth = Buy(st, garrison, need, int.MaxValue, got, true, out lacked, out poor);
                    for (int g = 0; g < Goods; g++) _dTown[g] += got[g];
                    _dTownWorth += worth;
                    return;
                }
                _dGarCastleN++;
                if (!Wants(need)) return;
                if (st.IsUnderSiege) { _dCastleWait++; return; }
                var market = MarketTown(st);
                if (market == null) { _dCastleNoTown++; return; }
                if (market.IsUnderSiege) { _dCastleWait++; return; }
                int budget = Math.Max(0, st.Town.Gold);
                int spent = Buy(market, garrison, need, budget, got, false, out lacked, out poor);
                if (poor) _dCastleNoGold++;
                if (spent <= 0) return;
                st.Town.ChangeGold(-spent);                                     // kasa zamku placi ...
                market.Town.ChangeGold(spent);                                  // ... kupcom miasta
                MoneyLedger.Note(MoneyLedger.NCloth, st, -spent);
                MoneyLedger.Note(MoneyLedger.NCloth, market, spent);
                SoldierPay.Hold(market, spent);                                 // zold zalogi wydany w miescie - jak wydatek "na zycie"
                for (int g = 0; g < Goods; g++) _dCastle[g] += got[g];
                _dCastleGold += spent;
                _dCastleMarkets.Add(market.StringId);
            }
            catch (Exception e) { Stumble("ArmyClothing.OnGarrisonPaid", e); }
        }

        /// <summary>Miasto, w ktorym zamek kupuje: miasto handlowe jego wsi (TradeBound gry), inaczej najblizsze miasto, z ktorym zamek nie jest w wojnie.</summary>
        internal static Settlement MarketTown(Settlement castle)
        {
            var f = castle.MapFaction;
            if (castle.BoundVillages != null)
                foreach (var v in castle.BoundVillages)
                {
                    var tb = v != null ? v.TradeBound : null;
                    if (tb != null && tb.IsTown && tb.Town != null && !(f != null && tb.MapFaction != null && FactionManager.IsAtWarAgainstFaction(f, tb.MapFaction))) return tb;
                }
            Settlement best = null; float bd = float.MaxValue;
            var p = castle.GetPosition2D;
            foreach (var t in Settlement.All)
            {
                if (t == null || !t.IsTown || t.Town == null) continue;
                if (f != null && t.MapFaction != null && FactionManager.IsAtWarAgainstFaction(f, t.MapFaction)) continue;
                float d = p.DistanceSquared(t.GetPosition2D);
                if (d < bd) { bd = d; best = t; }
            }
            return best;
        }

        // ------------------------------------------------------------ zakup sztuka po sztuce
        private static bool Wants(float[] need) { return need[Leather] >= 1f || need[Cloth] >= 1f || need[Linen] >= 1f; }

        /// <summary>
        /// Cale sztuki z polki miasta `st`: po jednej z kazdego towaru po kolei (skora, sukno, plotno), najtansza sztuka tego towaru,
        /// cena targu liczona od nowa przed kazda sztuka. `free` = garnizon swojego miasta (bez zlota; wynik = wartosc wedlug cen targu,
        /// tylko do logu). Zwraca zaplacone zloto (albo wartosc przy `free`); `lacked` - towaru zabraklo na polce, `poor` - pieniedzy.
        /// </summary>
        private static int Buy(Settlement st, MobileParty buyer, float[] need, int budget, int[] got, bool free, out bool lacked, out bool poor)
        {
            lacked = false; poor = false;
            var shelf = st.ItemRoster;
            var town = st.Town;
            int spent = 0;
            var stop = new bool[Goods];
            bool any = true;
            var gf = GoodsLedger.Begin(GoodsLedger.FArmyCloth, st);   // ksiega towarow (146): zdjete sztuki jako ujscie "odziez wojska (150)" (tylko licznik)
            try
            {
                while (any)
                {
                    any = false;
                    for (int g = 0; g < Goods; g++)
                    {
                        if (stop[g]) continue;
                        if (need[g] < 1f || _items[g] == null) { stop[g] = true; continue; }
                        EquipmentElement el; int price;
                        if (!Cheapest(shelf, _items[g], town, st, buyer, out el, out price)) { stop[g] = true; lacked = true; continue; }
                        if (!free && price > budget - spent) { stop[g] = true; poor = true; continue; }
                        shelf.AddToCounts(el, -1);                              // zdarte ubranie zastapione - sztuka znika
                        need[g] -= 1f; got[g]++;
                        spent += price;
                        any = true;
                    }
                }
            }
            // recenzja: wyjatek w polowie zakupu (np. w cenie kolejnej sztuki) - sztuki juz zdjete z polki zostaja zaplacone przez
            // wolajacego (zwracamy, ile kosztowaly), zamiast zniknac bez platnika; zakup konczy sie na tej wizycie, potkniecie liczone
            catch (Exception e) { Stumble("ArmyClothing.Buy", e); }
            finally { GoodsLedger.End(gf); }
            return spent;
        }

        private static bool Cheapest(ItemRoster shelf, ItemObject item, Town town, Settlement st, MobileParty buyer, out EquipmentElement el, out int price)
        {
            el = default(EquipmentElement); price = int.MaxValue;
            bool found = false;
            for (int i = 0; i < shelf.Count; i++)
            {
                var e = shelf.GetElementCopyAtIndex(i);
                if (e.Amount <= 0 || e.EquipmentElement.Item != item) continue;
                int p = Math.Max(1, town.MarketData.GetPrice(e.EquipmentElement, buyer, false, st.Party));
                if (!found || p < price) { price = p; el = e.EquipmentElement; found = true; }
            }
            return found;
        }

        // ------------------------------------------------------------ napisy
        private static string List(int[] got)
        {
            var parts = new List<string>();
            for (int g = 0; g < Goods; g++) if (got[g] > 0) parts.Add(got[g] + " " + (got[g] == 1 ? EnOne[g] : EnMany[g]));
            if (parts.Count <= 1) return parts.Count == 1 ? parts[0] : "";
            return string.Join(", ", parts.GetRange(0, parts.Count - 1).ToArray()) + " and " + parts[parts.Count - 1];
        }

        private static string Pl(int[] n)
        {
            return PlName[0] + " " + n[0] + ", " + PlName[1] + " " + n[1] + ", " + PlName[2] + " " + n[2];
        }

        private static string Pl(float[] n)
        {
            return PlName[0] + " " + F(n[0]) + ", " + PlName[1] + " " + F(n[1]) + ", " + PlName[2] + " " + F(n[2]);
        }

        private static string F(float v) { return v.ToString(v >= 100f ? "0" : v >= 10f ? "0.#" : "0.##", CultureInfo.InvariantCulture); }

        // ------------------------------------------------------------ zapis gry
        internal static string Export()
        {
            var sb = new StringBuilder();
            foreach (var kv in _need)
            {
                var n = kv.Value;
                if (n[0] <= 0.001f && n[1] <= 0.001f && n[2] <= 0.001f) continue;
                if (sb.Length > 0) sb.Append(';');
                sb.Append(kv.Key).Append('=')
                  .Append(n[0].ToString("0.###", CultureInfo.InvariantCulture)).Append(',')
                  .Append(n[1].ToString("0.###", CultureInfo.InvariantCulture)).Append(',')
                  .Append(n[2].ToString("0.###", CultureInfo.InvariantCulture));
            }
            return sb.ToString();
        }

        internal static void Import(string s)
        {
            _need.Clear();
            if (string.IsNullOrEmpty(s)) return;
            foreach (var p in s.Split(';'))
            {
                int eq = p.IndexOf('=');
                if (eq <= 0) continue;
                var v = p.Substring(eq + 1).Split(',');
                if (v.Length != Goods) continue;
                var n = new float[Goods];
                bool ok = true;
                for (int g = 0; g < Goods; g++)
                    if (!float.TryParse(v[g], NumberStyles.Float, CultureInfo.InvariantCulture, out n[g]) || n[g] < 0f || float.IsNaN(n[g]) || float.IsInfinity(n[g])) { ok = false; break; }
                if (ok) _need[p.Substring(0, eq)] = n;
            }
        }

        // ------------------------------------------------------------ linia startowa i linia dnia
        internal static void StartLine()
        {
            try
            {
                var s = Settings.Current;
                if (s == null) return;
                if (!s.ArmyClothingEnabled) { Log.Info("Odziez wojska (150): wylaczona w MCM (Army Clothing Enabled) - wojsko nie zuzywa odziezy, zaopatrzenie BK w tekstylia jak dotad."); return; }
                Items();
                var miss = new List<string>();
                for (int g = 0; g < Goods; g++) if (_items == null || _items[g] == null) miss.Add(Ids[g]);
                var sb = new StringBuilder();
                sb.Append("Odziez wojska (150): WLACZONA - na 1000 ludzi dziennie w polu: ");
                for (int g = 0; g < Goods; g++) sb.Append(g > 0 ? ", " : "").Append(PlName[g]).Append(' ').Append(PerDay(Rate(s, g, true), 1000, Weight(g)).ToString("0.00", CultureInfo.InvariantCulture));
                sb.Append(" szt.; w garnizonie: ");
                for (int g = 0; g < Goods; g++) sb.Append(g > 0 ? ", " : "").Append(PlName[g]).Append(' ').Append(PerDay(Rate(s, g, false), 1000, Weight(g)).ToString("0.00", CultureInfo.InvariantCulture));
                sb.Append(" szt. (kg na czlowieka na rok: w polu ").Append(F(s.ArmyClothingFieldLeatherKg)).Append('/').Append(F(s.ArmyClothingFieldClothKg)).Append('/').Append(F(s.ArmyClothingFieldLinenKg))
                  .Append(", w garnizonie ").Append(F(s.ArmyClothingGarrisonLeatherKg)).Append('/').Append(F(s.ArmyClothingGarrisonClothKg)).Append('/').Append(F(s.ArmyClothingGarrisonLinenKg))
                  .Append("); potrzeba czeka najwyzej ").Append(Math.Max(1, s.ArmyClothingMaxWaitDays)).Append(" dob")
                  .Append("; partie kupuja przy wyjezdzie z miasta z sakiewki ludzi ").Append(s.MenPurseEnabled ? "(sakiewka wlaczona)" : "- SAKIEWKA LUDZI WYLACZONA w MCM: partie nic nie kupia, potrzeba przepadnie po sufit")
                  .Append("; garnizony na zoldzie zaplaconym do kasy osady ").Append(s.GarrisonPayToCoffers ? "(wlaczony)" : "- ZOLD GARNIZONOW DO KAS WYLACZONY w MCM: garnizony nie zuzywaja odziezy")
                  .Append("; zaopatrzenie BK w tekstylia: ").Append(BkSupplyTemper.ClothHooked ? "0 (wpiete" + (BkSupplyTemper.ClothResetReady ? ", zapisana potrzeba BK zerowana" : ", BEZ zerowania zapisanej potrzeby BK") + ")" : "BRAK latki - BK kupuje tekstylia jak dotad");
                if (miss.Count > 0) sb.Append("; BRAK przedmiotow ").Append(string.Join(", ", miss.ToArray())).Append(" - ten towar pominiety");
                sb.Append("; zapisanej potrzeby: ").Append(_need.Count).Append(" partii i zalog.");
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Stumble("ArmyClothing.StartLine", e); }
        }

        internal static void Daily()
        {
            try
            {
                if (Campaign.Current == null) { ClearDay(); return; }
                if (!On && _dFieldN + _dGarTownN + _dGarCastleN + _dVisits + BkClothZeroed + _dStumbles == 0) { ClearDay(); return; }
                // potrzeby partii i osad, ktorych juz nie ma (rozbite bez zdarzenia, stary zapis)
                int gone = 0;
                var alive = new HashSet<string>();
                try { foreach (var mp in MobileParty.All) if (mp != null && mp.StringId != null) alive.Add(mp.StringId); } catch { }
                if (alive.Count > 0)
                    foreach (var k in new List<string>(_need.Keys))
                    {
                        bool keep = k.StartsWith(GarrisonKey, StringComparison.Ordinal) ? Settlement.Find(k.Substring(GarrisonKey.Length)) != null : alive.Contains(k);
                        if (!keep) { _need.Remove(k); gone++; }
                    }
                var wait = new float[Goods];
                int waiting = 0;
                foreach (var n in _need.Values) { bool w = false; for (int g = 0; g < Goods; g++) { wait[g] += n[g]; if (n[g] >= 1f) w = true; } if (w) waiting++; }
                int day = (int)CampaignTime.Now.ToDays - 1;
                var sb = new StringBuilder();
                sb.Append("Odziez wojska (150): dzien ").Append(day).Append(On ? "" : " (WYLACZONA)")
                  .Append(" - ludzie na zoldzie: w partiach ").Append(_dFieldMen).Append(" (").Append(_dFieldN).Append(" partii), w garnizonach ").Append(_dGarMen)
                  .Append(" (zalogi miast ").Append(_dGarTownN).Append(", zamkow ").Append(_dGarCastleN).Append(")")
                  .Append(" | potrzeba narosla: ").Append(Pl(_dAccrued))
                  .Append(" | partie kupily: ").Append(Pl(_dParty)).Append(" za ").Append(_dPartyGold).Append(" (sakiewki ludzi -> kasy miast; wizyt z potrzeba ").Append(_dVisits)
                  .Append(", w tym gracz ").Append(_dPlayerVisits).Append(")")
                  .Append(" | garnizony miast wziely z polki bez zlota: ").Append(Pl(_dTown)).Append(" (wartosc wedlug cen targu ").Append(_dTownWorth).Append(")")
                  .Append(" | garnizony zamkow kupily: ").Append(Pl(_dCastle)).Append(" za ").Append(_dCastleGold).Append(" (kasy zamkow -> kasy ").Append(_dCastleMarkets.Count).Append(" miast)")
                  .Append(" | czeka: ").Append(Pl(wait)).Append(" (").Append(waiting).Append(" partii i zalog z cala sztuka do kupienia, zapisanych ").Append(_need.Count).Append(")")
                  .Append(" | przepadlo ponad sufit ").Append(Settings.Current != null ? Math.Max(1, Settings.Current.ArmyClothingMaxWaitDays) : 120).Append(" dob (lachmany, bez kary): ").Append(Pl(_dRagged)).Append(" w ").Append(_dRaggedKeys.Count).Append(" partiach i zalogach")
                  .Append(" | braki przy zakupie: wizyt bez towaru na polce ").Append(_dNoGoods).Append(", za chuda sakiewka ").Append(_dNoPurse)
                  .Append(", zamek bez zlota ").Append(_dCastleNoGold).Append(", zamek albo miasto oblezone ").Append(_dCastleWait).Append(", zamek bez miasta ").Append(_dCastleNoTown)
                  .Append(" | zaopatrzenie BK w tekstylia: zapisana potrzeba wyzerowana w ").Append(BkClothZeroed).Append(" partiach");
                if (_dGarUndead > 0) sb.Append(" | zalogi nieumarlych pominiete (trup nie zdziera butow): ").Append(_dGarUndead);
                if (gone > 0) sb.Append(" | potrzeby po zniklych partiach i osadach usuniete: ").Append(gone);
                sb.Append(" | potkniecia ").Append(_dStumbles).Append('.');
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Log.Error("ArmyClothing.Daily", e); }
            ClearDay();
        }
    }
}
