using System;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// 585 (16.11, Jeff: "i jak moja karawana? zarabia" -> propozycja "2. Konie: karawana kupuje konie tylko wtedy, gdy na jakims znanym targu da sie je sprzedac
    /// z zyskiem po mycie i drodze" -> "2. tak") - ZWIERZETA Z GABKI BK W KARAWANACH GRACZA TYLKO Z ZYSKIEM OSIAGALNYM (klucz PlayerCaravanHorsesNeedProfit).
    /// Dowod [L] (Armoury-2026-11-15_23-13-58.log, 'Convoy of Amanda', kapital 30000): gabka BK przy wyjezdzie kupila konie 109232 (15 koni sturgijskich za 6030,
    /// l.20542/20563) i 109235 (vlandyjskie i imperialne za 10340, l.23297/23319); 109236 konie to 14762 z 16723 towaru (68% wartosci, l.24179); kazda sprzedaz
    /// w Blacktyde / Pebbleton / Lonely Light ponizej kosztu (sturgia 402 -> 381-388, vlandia 365 -> 355, empire 122 -> 118, myto ok. 7/szt.); zakup z 109235 zbil
    /// kiese do 4916 (doplata BK 200 z kiesy rodu). Dopiero w Seagard (indeks koni 0.47, 109248) konie zeszly z zyskiem.
    /// Przyczyna [K] (dekompilacja BannerKings.dll, BKPartyBehavior.OnSettlementLeft :303-346): gabka kupuje KAZDY przedmiot z polki tanszy niz 0.33 x ItemValue,
    /// do 80% udzwigu, wprost (ItemRoster.AddToCounts, PartyTradeGold -= cena, Town.ChangeGold(+)) - bez SellItemsAction i bez pytania o zbyt. Konie w ROT sa tanie
    /// prawie wszedzie (indeks ok. 0.32), wiec 'okazja' bywa ponizej kosztu wszedzie. Cofniecia gabki 284 (CaravanTrade284.LeftPostfix), CaravanBulk i CaravanAmmo
    /// pomijaja zwierzeta (it.HasHorseComponent), straz 'nie ponizej kosztu' (CaravanTrade284.SellGuardPrefix) tez - BK SellGoods (BKCaravansBehavior :1552,
    /// petla i = 1: konie i juczne) i sprzedaz gry sprzedawaly konie bez kosztu.
    /// Regula (tylko karawany rodu gracza - Jeff pytal o swoja; AI: gabka bez zmian, linia doby liczy jej zakupy zwierzat na sucho):
    ///  A) zakup: zwierze z gabki (HasHorseComponent, bez jucznych - PackAnimal potrzebne karawanie; konie i bydlo) zostaje tylko w tylu sztukach, ile da sie sprzedac
    ///     na JAKIMS miescie (nie wrogim) po cenie po mycie (CaravanGoods.MaxAtCost - ta sama miara ceny k-tej sztuki i myta co straz 284) nie nizszej niz koszt sztuki
    ///     + koszt dni drogi (CaravanWait139.Carry: udzial sztuki w koszcie dnia karawany x dni drogi CaravanWait139.Days - ta sama miara co regula 139 A/C), w kolejce
    ///     za sztukami tego zwierzecia juz w jukach. Reszta wraca na polke, zloto z kasy miasta do kiesy (cena polki po zwrocie - wzor cofniecia gabki 284; niezwrocone
    ///     przy pustej kasie - SpongeUndo307). Koszt sztuki = cena polki tuz przed gabka (prefiks). Ksiega 307a (SpongePost, Priority.Last) widzi tylko to, co zostalo.
    ///  B) sprzedaz: kon (bez jucznych i bez bydla - BK sprzedaje nadmiar bydla ponad 0.6 x ludzi bez wzgledu na cene, przeciazenie stada) z kosztem z ksiegi (nie
    ///     z oszacowania) nie schodzi w sprzedazy BK / gry ponizej kosztu po mycie (MaxAtCost, Withhold jak towar); wyjscia na stracie T1 (LossReason: rozwiazanie,
    ///     brak na zold, ponad udzwig, > 30 dob bez rynku z zyskiem) - bez zmian. Konie juz w jukach (Amanda 14762) zostaja i schodza tam, gdzie placa ponad koszt.
    /// Kto placi, kto dostaje: nic nowego - cofniecie = zwrot tej samej ceny (towar -> polka, zloto kasy miasta -> kiesa), wstrzymanie = mniej sprzedazy.
    /// Linia doby "Konie gabki BK w karawanach gracza (585): dzien N". Wyjatek per wywolanie - licznik potkniec (gabka i sprzedaz jak w BK).
    /// </summary>
    internal static class CaravanHorse585
    {
        private static bool On { get { var s = Settings.Current; return s != null && s.PlayerCaravanHorsesNeedProfit; } }

        private static bool _wLeft;
        private static long _uN, _uG, _uLost, _keepN, _keepG, _visits, _sellHeldN, _sellHeldC, _aiN, _aiG, _aiCalls;
        private static int _stumbles, _stumbleSess;
        private static readonly HashSet<string> _err = new HashSet<string>();

        public sealed class State
        {
            public MobileParty Mp; public Settlement St; public int Gold0;
            public Dictionary<EquipmentElement, int> El0;          // zwierzeta w jukach przed gabka (wedlug elementu)
            public Dictionary<ItemObject, int> N0;                  // ... wedlug przedmiotu (kolejka sprzedazy)
            public Dictionary<EquipmentElement, int> Price0;        // cena polki przed gabka (koszt sztuki)
        }

        internal static void Reset()
        {
            ZeroDay(); _stumbleSess = 0; _err.Clear();
        }

        private static void ZeroDay()
        {
            _uN = _uG = _uLost = _keepN = _keepG = _visits = _sellHeldN = _sellHeldC = _aiN = _aiG = _aiCalls = 0; _stumbles = 0;
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++; _stumbleSess++;
            try { if (_err.Add(where)) Log.Error("CaravanHorse585." + where, e); } catch { }
        }

        /// <summary>Zwierze pod regula zakupu A: HasHorseComponent, bez jucznych (PackAnimal - potrzebne karawanie do niesienia).</summary>
        private static bool TradeAnimal(ItemObject it)
        {
            return it != null && it.HasHorseComponent && it.ItemCategory != null && it.ItemCategory != DefaultItemCategories.PackAnimal;
        }

        private static bool Players(MobileParty mp)
        {
            try { return mp != null && mp.ActualClan != null && mp.ActualClan == Clan.PlayerClan; } catch { return false; }
        }

        /// <summary>Karawana pod zasadami handlu (bez kontraktu 174, bez rozkazu gracza BK) - ta sama bramka co cofniecie gabki 284.</summary>
        private static bool Rules(MobileParty mp)
        {
            return CaravanBulk.TradesForT1(mp) && !MaterialOrders.HasContract(mp) && !CaravanBulk.HasOrders(mp);
        }

        // ------------------------------------------------------------ A: gabka BK przy wyjezdzie (BKPartyBehavior.OnSettlementLeft)
        /// <summary>Prefiks gabki: zwierzeta w jukach i cena polki zwierzat przed gabka (karawana gracza); AI - tylko kiesa do pomiaru na sucho.</summary>
        public static void LeftPrefix(MobileParty __0, Settlement __1, out State __state)
        {
            __state = null;
            try
            {
                if (__0 == null || __1 == null || !__0.IsCaravan || __1.Town == null || __0.ItemRoster == null || __1.Town.Owner == null) return;
                var cg = Campaign.Current;
                if (cg == null || !cg.GameStarted) return;
                bool pl = Players(__0);
                if (pl && (!On || !Rules(__0))) return;
                var st = new State { Mp = __0, St = __1, Gold0 = __0.PartyTradeGold, El0 = new Dictionary<EquipmentElement, int>(), N0 = new Dictionary<ItemObject, int>() };
                var r = __0.ItemRoster;
                for (int i = 0; i < r.Count; i++)
                {
                    var el = r.GetElementCopyAtIndex(i);
                    var it = el.EquipmentElement.Item;
                    if (!TradeAnimal(it) || el.Amount <= 0) continue;
                    int x; st.El0.TryGetValue(el.EquipmentElement, out x); st.El0[el.EquipmentElement] = x + el.Amount;
                    int y; st.N0.TryGetValue(it, out y); st.N0[it] = y + el.Amount;
                }
                if (pl)
                {
                    st.Price0 = new Dictionary<EquipmentElement, int>();
                    var shelf = __1.Town.Owner.ItemRoster;
                    for (int i = 0; i < shelf.Count; i++)
                    {
                        var el = shelf.GetElementCopyAtIndex(i);
                        if (!TradeAnimal(el.EquipmentElement.Item) || el.Amount <= 0 || st.Price0.ContainsKey(el.EquipmentElement)) continue;
                        st.Price0[el.EquipmentElement] = __1.Town.GetItemPrice(el.EquipmentElement, __0, false);
                    }
                    if (st.Price0.Count == 0) return;                // na polce nie ma zwierzat - gabka ich nie kupi
                }
                else st.Price0 = null;
                __state = st;
            }
            catch (Exception e) { __state = null; Stumble("LeftPrefix", e); }
        }

        /// <summary>Postfiks gabki (domyslny priorytet - przed SpongePost 307a, Priority.Last): zwierzeta bez zysku osiagalnego wracaja na polke, zloto do kiesy.</summary>
        public static void LeftPostfix(State __state)
        {
            if (__state == null) return;
            try
            {
                var mp = __state.Mp; var stl = __state.St; var town = stl.Town;
                var pack = mp.ItemRoster;
                // przybyle zwierzeta wedlug elementu
                List<KeyValuePair<EquipmentElement, int>> got = null;
                HashSet<EquipmentElement> seen = null;
                for (int i = 0; i < pack.Count; i++)
                {
                    var el = pack.GetElementCopyAtIndex(i);
                    var ee = el.EquipmentElement;
                    if (!TradeAnimal(ee.Item)) continue;
                    if (seen == null) seen = new HashSet<EquipmentElement>();
                    if (!seen.Add(ee)) continue;
                    int now = 0;
                    for (int j = 0; j < pack.Count; j++) { var e2 = pack.GetElementCopyAtIndex(j); if (e2.EquipmentElement.Equals(ee)) now += e2.Amount; }
                    int was; __state.El0.TryGetValue(ee, out was);
                    if (now - was <= 0) continue;
                    if (got == null) got = new List<KeyValuePair<EquipmentElement, int>>();
                    got.Add(new KeyValuePair<EquipmentElement, int>(ee, now - was));
                }
                if (got == null) return;
                if (__state.Price0 == null)
                {
                    // AI: tylko pomiar (gabka bez zmian) - sztuki i zloto netto (jak 307a: zloto rozdzielone nie jest, cale zeszle w tym wywolaniu)
                    _aiCalls++;
                    foreach (var kv in got) _aiN += kv.Value;
                    _aiG += Math.Max(0, (long)__state.Gold0 - mp.PartyTradeGold);
                    return;
                }
                _visits++;
                // grupy wedlug przedmiotu: sztuki przybyle, sredni koszt (cena polki przed gabka)
                var items = new Dictionary<ItemObject, List<KeyValuePair<EquipmentElement, int>>>();
                foreach (var kv in got)
                {
                    List<KeyValuePair<EquipmentElement, int>> l;
                    if (!items.TryGetValue(kv.Key.Item, out l)) { l = new List<KeyValuePair<EquipmentElement, int>>(); items[kv.Key.Item] = l; }
                    l.Add(kv);
                }
                double dayCost = -1, cargo = -1;
                Dictionary<Town, double> days = null;
                var sb = (StringBuilder)null;
                foreach (var kv in items)
                {
                    var it = kv.Key;
                    int extra = 0; double costSum = 0;
                    foreach (var p in kv.Value)
                    {
                        int pr;
                        if (!__state.Price0.TryGetValue(p.Key, out pr) || pr <= 0) pr = Math.Max(1, town.GetItemPrice(p.Key, mp, false));
                        extra += p.Value; costSum += (double)pr * p.Value;
                    }
                    if (extra <= 0) continue;
                    double unit = costSum / extra;
                    int was; __state.N0.TryGetValue(it, out was);
                    if (dayCost < 0)
                    {
                        dayCost = CaravanWait139.DayCost(mp);
                        cargo = CaravanWait139.CargoRO(mp);
                        foreach (var a in __state.N0) if (a.Key != null) cargo += CaravanGoods.HeldCost(mp, a.Key, a.Value);   // zwierzeta sprzed gabki po koszcie z ksiegi
                        foreach (var g in got) { int pr; if (!__state.Price0.TryGetValue(g.Key, out pr)) pr = 0; cargo += (double)Math.Max(0, pr) * g.Value; }
                        days = new Dictionary<Town, double>();
                    }
                    Town best;
                    int keep = Keep(mp, town, it, was, extra, unit, dayCost, cargo, days, out best);
                    int back = extra - keep;
                    _keepN += keep; _keepG += (long)Math.Round(keep * unit);
                    if (back <= 0) continue;
                    // zwrot: od ostatniego elementu (kolejnosc bez znaczenia - ta sama pozycja z polki)
                    int left = back; long refundAll = 0;
                    for (int q = kv.Value.Count - 1; q >= 0 && left > 0; q--)
                    {
                        var ee = kv.Value[q].Key;
                        int n = Math.Min(left, kv.Value[q].Value);
                        if (n <= 0) continue;
                        pack.AddToCounts(ee, -n);
                        town.Owner.ItemRoster.AddToCounts(ee, n);
                        int due = (int)(n * (float)town.GetItemPrice(ee, mp, false));   // ta sama cena co cofniecie gabki 284 (polka po zwrocie)
                        int refund = Math.Min(town.Gold, due);
                        CaravanGoods.SpongeUndo307(mp, due, refund);   // 307d: niezwrocona czesc - strata gabki, nie koszt zwierzat, ktore zostaly
                        if (refund > 0)
                        {
                            town.ChangeGold(-refund); mp.PartyTradeGold += refund;
                            MoneyLedger.Note169(MoneyLedger.N169Other, stl, -refund, TownFolk.BBack);   // jak CaravanTrade284.LeftPostfix: linia kas (tylko licznik)
                        }
                        if (due > refund) _uLost += due - Math.Max(0, refund);
                        refundAll += Math.Max(0, refund);
                        left -= n;
                    }
                    int done = back - left;
                    _uN += done; _uG += refundAll;
                    if (done > 0)
                    {
                        if (sb == null) sb = new StringBuilder(200);
                        sb.Append(sb.Length > 0 ? ", " : "").Append(it.StringId ?? "?").Append(" x").Append(done).Append(" (koszt/szt ").Append(Math.Round(unit))
                          .Append(", zostaje ").Append(keep).Append(keep > 0 && best != null ? " - zysk w " + best.Name : "").Append(')');
                    }
                }
                if (sb != null)
                    Log.Info("Karawana gracza '" + (mp.Name != null ? mp.Name.ToString() : "?") + "' (585): konie gabki cofniete w " + (stl.Name != null ? stl.Name.ToString() : "?")
                             + " (bez zysku osiagalnego) - " + sb + "; kiesa teraz " + mp.PartyTradeGold + ".");
            }
            catch (Exception e) { Stumble("LeftPostfix", e); }
        }

        /// <summary>Sztuki z `extra` (przybyle z gabki), ktore jakies miasto (nie wrogie, nie to) kupi po cenie po mycie >= koszt + koszt dni drogi, w kolejce za `was`
        /// sztukami tego zwierzecia juz w jukach (MaxAtCost na was + extra, minus was). best - miasto z najwieksza liczba sztuk.</summary>
        private static int Keep(MobileParty mp, Town here, ItemObject it, int was, int extra, double unit, double dayCost, double cargo, Dictionary<Town, double> days, out Town best)
        {
            best = null;
            int keep = 0;
            var el = new EquipmentElement(it);
            foreach (var t in Town.AllTowns)
            {
                if (t == null || t == here || !t.IsTown || t.MarketData == null || t.Settlement == null) continue;
                try
                {
                    if (mp.MapFaction != null && t.Settlement.MapFaction != null && FactionManager.IsAtWarAgainstFaction(mp.MapFaction, t.Settlement.MapFaction)) continue;
                    double d;
                    if (!days.TryGetValue(t, out d)) { d = CaravanWait139.Days(mp, t.Settlement); days[t] = d; }
                    double need = unit + CaravanWait139.Carry(dayCost, unit, d, cargo);
                    int k = CaravanGoods.MaxAtCost(t, t.Settlement, mp, el, was + extra, need) - was;
                    if (k > keep) { keep = Math.Min(extra, k); best = t; if (keep >= extra) break; }
                }
                catch (Exception e) { Stumble("Keep", e); }
            }
            return keep;
        }

        // ------------------------------------------------------------ B: sprzedaz koni nie ponizej kosztu (CaravanTrade284.SellGuardPrefix)
        /// <summary>CaravanTrade284.SellGuardPrefix (sprzedaz BK / gry, karawana do miasta): true - przedmiot to kon karawany gracza pod regula B (n juz przyciete,
        /// held - sztuki wstrzymane do Withhold); false - nie dotyczy (straz 284 idzie dalej jak dotad).</summary>
        internal static bool SellGuard(MobileParty mp, Settlement st, EquipmentElement el, ref int n, out int held, out double unit)
        {
            held = 0; unit = 0;
            var it = el.Item;
            if (n <= 0 || it == null || !it.HasHorseComponent || it.ItemCategory == null || it.ItemCategory == DefaultItemCategories.PackAnimal) return false;
            if (it.HorseComponent != null && it.HorseComponent.IsLiveStock) return false;   // bydlo: BK zrzuca nadmiar stada bez wzgledu na cene - bez zmian
            if (!On || !Players(mp) || st == null || st.Town == null || !Rules(mp)) return false;
            try
            {
                if (CaravanGoods.CostEstimated(mp, it)) return true;      // koszt z oszacowania (nie z kiesy) - bez progu
                if (CaravanGoods.LossReason(mp, it, st) != 0) return true; // wyjscia na stracie T1 (rozwiazanie, brak na zold, udzwig, > 30 dob bez rynku) - bez zmian
                unit = CaravanGoods.UnitCost(mp, it);
                if (!(unit > 0.0)) return true;
                int k = Math.Max(0, CaravanGoods.MaxAtCost(st.Town, st, mp, el, n, unit));
                if (k < n) { held = n - k; n = k; _sellHeldN += held; _sellHeldC += (long)Math.Round(held * unit); }
                return true;
            }
            catch (Exception e) { Stumble("SellGuard", e); return true; }
        }

        internal static void Apply(Harmony h)
        {
            try
            {
                var bkp = AccessTools.TypeByName("BannerKings.Behaviours.BKPartyBehavior");
                var left = bkp != null ? AccessTools.DeclaredMethod(bkp, "OnSettlementLeft", new[] { typeof(MobileParty), typeof(Settlement) }) : null;
                if (left != null)
                {
                    h.Patch(left, prefix: new HarmonyMethod(typeof(CaravanHorse585), nameof(LeftPrefix)), postfix: new HarmonyMethod(typeof(CaravanHorse585), nameof(LeftPostfix)));
                    _wLeft = true;
                }
                Log.Info("Konie gabki BK w karawanach gracza (585): gabka BK przy wyjezdzie (BKPartyBehavior.OnSettlementLeft) " + (_wLeft ? "wpieta" : "NIE (brak metody BK)")
                         + " - zwierzeta tylko z zyskiem osiagalnym (miasto, ktore placi po mycie ponad koszt + droge), sprzedaz koni nie ponizej kosztu (straz 284); AI bez zmian.");
            }
            catch (Exception e) { Log.Error("CaravanHorse585.Apply", e); }
        }

        /// <summary>ArmouryBehavior (po Daily307): linia doby 585 i zerowanie licznikow.</summary>
        internal static void Daily()
        {
            var s = Settings.Current;
            if (s == null || Campaign.Current == null) return;
            try
            {
                int day = (int)CampaignTime.Now.ToDays - 1;
                var sb = new StringBuilder(400);
                sb.Append("Konie gabki BK w karawanach gracza (585): dzien ").Append(day)
                  .Append(" | klucz Player Caravan Horses Need Profit ").Append(s.PlayerCaravanHorsesNeedProfit ? "TAK" : "NIE")
                  .Append(" | latka gabki ").Append(_wLeft ? "wpieta" : "NIE")
                  .Append(" | wyjazdy z koniem z gabki ").Append(_visits)
                  .Append(" | konie gabki cofniete ").Append(_uN).Append(" (bez zysku osiagalnego), zwrot ").Append(_uG).Append(" zl, niezwrocone (pusta kasa miasta) ").Append(_uLost).Append(" zl")
                  .Append(" | zostawione ").Append(_keepN).Append(" szt. za ok. ").Append(_keepG).Append(" zl (zysk osiagalny)")
                  .Append(" | sprzedaz koni ponizej kosztu wstrzymana ").Append(_sellHeldN).Append(" szt. (koszt ").Append(_sellHeldC).Append(" zl)")
                  .Append(" | AI bez zmian (na sucho): gabka kupila zwierzat ").Append(_aiN).Append(" szt. w ").Append(_aiCalls).Append(" wyjazdach, zloto netto calej gabki w tych wyjazdach ").Append(_aiG).Append(" zl")
                  .Append(" | potkniecia ").Append(_stumbles).Append(" (sesja ").Append(_stumbleSess).Append(").");
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Stumble("Daily", e); }
            ZeroDay();
        }
    }
}
