using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// L7 - TOWAR I KONIE Z POLEK (projekt lupu miast rozdz. 3.1 tabela, 3.7 krok 4, rozdz. 6 L7; decyzja Jeffa 13.10 pkt 5a "TAK - drugi etap"). Klucz SackTakesGoods.
    /// Ta sama czesc co moneta (ShareOf: grabiez 1/3, spustoszenie 2/3, laska 0) z kazdego stosu polki osady (Settlement.ItemRoster - targ miasta, polka zamku;
    /// schowek gracza nietkniety), MBRandom.RoundRandomized(czesc x sztuki) - bez straty malych stosow, nie wiecej niz stos. Odbiorcy - ci sami co przy monecie
    /// (Recipients: druzyny z listy udzialow gry, bez Innych), podzial wedlug ludzi:
    ///  1. UNIKATY (UniqueSpoils.Is - spis ROT i stal valyrianska): ceil(czesc x sztuki) do taboru druzyny wodza armii - najcenniejsze rzeczy w miescie bierze sie
    ///     najpierw; nie plona (kronika unikatow widzi je w taborze; lord zaklada je zwykla droga UniqueSpoils).
    ///  2. KONIE I ZYWY INWENTARZ (HasHorseComponent, IsAnimal): ida same - bez udzwigu, wedlug ludzi, najwyzej do granicy stada bez kary "Herding" gry
    ///     (po recenzji); czego nie poprowadza - zostaje na polce (konie nie plona).
    ///  3. ZYWNOSC (IsFood): najwyzej do 10 dob racji kazdej druzyny (1 sztuka = 20 czlowieko-dni, jak DistrictRaid; liczone z tym, co juz wiezie), w udzwigu;
    ///     reszta czesci zostaje na polce - jedzenie nigdy nie plonie (spalenie to glod bez odbiorcy, rozdz. 3.1).
    ///  4. UZBROJENIE I TOWAR (reszta): najcenniejsze na kilogram najpierw, wedlug ludzi do wolnego udzwigu druzyny, potem reszta do kazdej, ktora ma miejsce.
    ///     Czego nie uniosa: grabiez - zostaje na polce; spustoszenie - plonie (towar: ksiega towarow, ramka FSack "spalone przy spustoszeniu miasta";
    ///     uzbrojenie: ArmsBook.Sink KSack). Uzbrojenie wziete przez druzyny AI idzie z taboru do ich zbrojowni DTE (AiGear.AddToArmory; DTE nie przyjal - zostaje
    ///     w taborze, do sprzedazy); gracz - w taborze (sam decyduje).
    /// Obieg: sztuka zdjeta z polki = sztuka w taborze (albo w zbrojowni) + sztuka spalona; niezalezny bilans sztuk w linii (zdjete - do taborow - spalone = 0).
    /// Zlota nie rusza (lup monety - L2). Wyjatek - licznik potkniec; co zdjete przed wyjatkiem, zostaje u odbiorcow (ramka ksiegi zamknieta w finally).
    /// </summary>
    internal static partial class CitySack
    {
        internal static bool GoodsOn { get { var s = Settings.Current; return s != null && s.SackTakesGoods; } }

        private const double FoodDays = 10.0;            // rozdz. 3.1: zywnosc najwyzej do 10 dob racji armii
        private const double ManDaysPerFood = 20.0;      // skala gry: zuzycie partii = ludzie / 20 sztuk na dobe (DistrictRaid.RationsPerGrain)

        private static int _d7Sacks;
        private static long _d7Took, _d7TookV, _d7Horses, _d7Food, _d7Arms, _d7Armory, _d7Uniq, _d7Burn, _d7BurnV, _d7Left, _d7Bal;

        private static bool Walks7(ItemObject it) { return it != null && (it.HasHorseComponent || it.IsAnimal); }
        private static bool Arms7(ItemObject it) { return it != null && !it.IsFood && !Walks7(it) && (it.HasWeaponComponent || it.HasArmorComponent); }
        private static double PerKg7(ItemObject it) { return it == null ? 0.0 : it.Value / Math.Max(0.1, (double)it.Weight); }
        private static long Pieces7(ItemRoster r) { long n = 0; if (r == null) return 0; for (int i = 0; i < r.Count; i++) { int a = r.GetElementCopyAtIndex(i).Amount; if (a > 0) n += a; } return n; }
        private static double FreeKg(MobileParty mp) { try { return Math.Max(0.0, mp.InventoryCapacity - mp.TotalWeightCarried); } catch { return 0.0; } }

        /// <summary>Ile zwierzat druzyna jeszcze poprowadzi bez kary "Herding" gry: ludzie - (juczne + zywiec + wierzchowce ponad pieszych); 0 przy bledzie.</summary>
        private static int HerdRoom(MobileParty mp)
        {
            try
            {
                var r = mp.ItemRoster;
                int men = mp.MemberRoster != null ? mp.MemberRoster.TotalManCount : 0;
                int herd = r.NumberOfPackAnimals + r.NumberOfLivestockAnimals + Math.Max(0, r.NumberOfMounts - mp.Party.NumberOfMenWithoutHorse);
                return Math.Max(0, men - herd);
            }
            catch { return 0; }
        }

        private static double FoodHave(MobileParty mp)
        {
            double n = 0;
            var r = mp.ItemRoster;
            if (r == null) return 0;
            for (int i = 0; i < r.Count; i++) { var el = r.GetElementCopyAtIndex(i); if (el.Amount > 0 && el.EquipmentElement.Item != null && el.EquipmentElement.Item.IsFood) n += el.Amount; }
            return n;
        }

        private sealed class Lot { public EquipmentElement El; public int Want; }

        private static void AftermathGoods(MobileParty a, Settlement st, int kind, Dictionary<MobileParty, float> contrib)
        {
            if (!GoodsOn) return;
            try { SackGoods(a, st, kind, contrib); }
            catch (Exception e) { Stumble("SackGoods", e); }
        }

        private static void SackGoods(MobileParty a, Settlement st, int kind, Dictionary<MobileParty, float> contrib)
        {
            var roster = st.ItemRoster;
            double share = ShareOf(kind);
            int men, uMen;
            var recs = Recipients(a, contrib, out men, out uMen);
            var live = new List<Rec>();
            foreach (var r in recs) if (!r.Undead && r.Mp != null && r.Mp.ItemRoster != null) live.Add(r);
            live.Sort((x, y) => y.Men.CompareTo(x.Men));
            long shelf0 = Pieces7(roster);
            if (roster == null || share <= 0 || live.Count == 0 || men <= 0)
            {
                if (MeasureOn && kind != KMercy)
                    Log.Info("Lup miast (L7): dzien " + (int)CampaignTime.Now.ToDays + " | osada " + st.StringId + " | wybor " + KName[kind] + " | polka " + shelf0 + " szt. | bez odbiorcow (Inni albo pusto) - towar zostaje.");
                return;
            }
            // plan ze stanu polki (kopia - roster zmienia sie w petli)
            var uniq = new List<Lot>(); var walk = new List<Lot>(); var food = new List<Lot>(); var rest = new List<Lot>();
            for (int i = 0; i < roster.Count; i++)
            {
                var el = roster.GetElementCopyAtIndex(i);
                var it = el.EquipmentElement.Item;
                if (it == null || el.Amount <= 0) continue;
                bool u = false;
                try { u = UniqueSpoils.Is(it); } catch { u = false; }
                int want = u ? (int)Math.Ceiling(share * el.Amount) : MBRandom.RoundRandomized((float)(share * el.Amount));
                if (want > el.Amount) want = el.Amount;
                if (want <= 0) continue;
                var lot = new Lot { El = el.EquipmentElement, Want = want };
                if (u) uniq.Add(lot); else if (Walks7(it)) walk.Add(lot); else if (it.IsFood) food.Add(lot); else rest.Add(lot);
            }
            rest.Sort((x, y) => PerKg7(y.El.Item).CompareTo(PerKg7(x.El.Item)));
            var lead = live[0];
            foreach (var r in live) if (r.Mp == a) { lead = r; break; }
            long p0 = 0; foreach (var r in live) p0 += Pieces7(r.Mp.ItemRoster);
            long took = 0, tookV = 0, horses = 0, foodN = 0, armsN = 0, uniqN = 0, burn = 0, burnV = 0, left = 0;
            var armsTo = new List<KeyValuePair<Rec, KeyValuePair<EquipmentElement, int>>>();
            var gf = GoodsLedger.Begin(GoodsLedger.FSack, st);
            try
            {
                // 1. unikaty - wodz armii
                foreach (var l in uniq)
                {
                    roster.AddToCounts(l.El, -l.Want);
                    lead.Mp.ItemRoster.AddToCounts(l.El, l.Want);
                    uniqN += l.Want; took += l.Want; tookV += (long)l.El.Item.Value * l.Want;
                }
                // 2. konie i zywy inwentarz - same ida, wedlug ludzi; recenzja (kod, drobne): najwyzej tyle, ile druzyna prowadzi bez kary "Herding" gry
                //    (stado = juczne + zywiec + konie ponad pieszych <= ludzie: DefaultPartySpeedCalculatingModel.GetHerdingModifier); reszta zostaje na polce (nie plonie)
                var room = new int[live.Count];
                for (int p = 0; p < live.Count; p++) room[p] = HerdRoom(live[p].Mp);
                foreach (var l in walk)
                {
                    int given = 0;
                    var gv = new int[live.Count];
                    for (int p = 0; p < live.Count; p++)
                    {
                        int q = Math.Min((int)((long)l.Want * live[p].Men / men), room[p]);
                        if (q <= 0) continue;
                        gv[p] += q; room[p] -= q; given += q;
                    }
                    for (int p = 0; p < live.Count && given < l.Want; p++)
                    {
                        int q = Math.Min(l.Want - given, room[p]);
                        if (q <= 0) continue;
                        gv[p] += q; room[p] -= q; given += q;
                    }
                    if (given > 0) roster.AddToCounts(l.El, -given);
                    for (int p = 0; p < live.Count; p++) if (gv[p] > 0) live[p].Mp.ItemRoster.AddToCounts(l.El, gv[p]);
                    horses += given; took += given; tookV += (long)l.El.Item.Value * given;
                    left += l.Want - given;                                         // konie nie plona - czego nie poprowadza, zostaje
                }
                // wolny udzwig PO koniach (juczne niosa)
                var free = new double[live.Count];
                for (int p = 0; p < live.Count; p++) free[p] = FreeKg(live[p].Mp);
                // 3. zywnosc - do 10 dob racji kazdej druzyny (z tym, co juz wiezie), w udzwigu; reszta zostaje
                var need = new double[live.Count];
                for (int p = 0; p < live.Count; p++) need[p] = Math.Max(0.0, FoodDays * live[p].Men / ManDaysPerFood - FoodHave(live[p].Mp));
                foreach (var l in food)
                {
                    int have = l.Want;
                    double w = Math.Max(0f, l.El.Item.Weight);
                    for (int p = 0; p < live.Count && have > 0; p++)
                    {
                        int fit = (int)Math.Min(have, Math.Floor(need[p]));
                        if (w > 1e-6) fit = (int)Math.Min(fit, Math.Floor(free[p] / w));
                        if (fit <= 0) continue;
                        roster.AddToCounts(l.El, -fit);
                        live[p].Mp.ItemRoster.AddToCounts(l.El, fit);
                        need[p] -= fit; free[p] -= fit * w; have -= fit;
                        foodN += fit; took += fit; tookV += (long)l.El.Item.Value * fit;
                    }
                    left += have;                                                   // jedzenie nie plonie - zostaje na polce
                }
                // 4. uzbrojenie i towar - najcenniejsze na kilogram najpierw; wedlug ludzi w udzwigu, potem reszta do kazdej z miejscem
                foreach (var l in rest)
                {
                    var it = l.El.Item;
                    double w = Math.Max(0f, it.Weight);
                    int have = l.Want;
                    var got = new int[live.Count];
                    for (int p = 0; p < live.Count && have > 0; p++)
                    {
                        int q = (int)((long)l.Want * live[p].Men / men);
                        if (w > 1e-6) q = (int)Math.Min(q, Math.Floor(free[p] / w));
                        q = Math.Min(q, have);
                        if (q <= 0) continue;
                        got[p] += q; free[p] -= q * w; have -= q;
                    }
                    for (int p = 0; p < live.Count && have > 0; p++)
                    {
                        int q = w > 1e-6 ? (int)Math.Min(have, Math.Floor(free[p] / w)) : have;
                        if (q <= 0) continue;
                        got[p] += q; free[p] -= q * w; have -= q;
                    }
                    int carried = l.Want - have;
                    bool fire = kind == KDevastate && have > 0;
                    int off = carried + (fire ? have : 0);
                    if (off > 0) roster.AddToCounts(l.El, -off);
                    for (int p = 0; p < live.Count; p++)
                    {
                        if (got[p] <= 0) continue;
                        live[p].Mp.ItemRoster.AddToCounts(l.El, got[p]);
                        if (Arms7(it)) armsTo.Add(new KeyValuePair<Rec, KeyValuePair<EquipmentElement, int>>(live[p], new KeyValuePair<EquipmentElement, int>(l.El, got[p])));
                    }
                    took += carried; tookV += (long)it.Value * carried;
                    if (Arms7(it)) armsN += carried;
                    if (fire)
                    {
                        burn += have; burnV += (long)it.Value * have;
                        if (Arms7(it)) ArmsBook.Sink(ArmsBook.KSack, it, have);   // uzbrojenie - ujscie z przyczyna; towar handlowy - ramka FSack ksiegi towarow
                    }
                    else left += have;
                }
            }
            finally { GoodsLedger.End(gf); }
            // niezalezny bilans sztuk (przed zbrojowniami): ubytek polki = przyrost taborow + spalone
            long shelf1 = Pieces7(roster), p1 = 0;
            foreach (var r in live) p1 += Pieces7(r.Mp.ItemRoster);
            long bal = (shelf0 - shelf1) - (p1 - p0) - burn;
            // 5. uzbrojenie druzyn AI z taboru do zbrojowni DTE (poza ramka ksiegi towarow: stan ksiegi uzbrojenia - tabory + zbrojownie - bez zmian)
            long armory = 0;
            foreach (var kv in armsTo)
            {
                var mp = kv.Key.Mp; var el = kv.Value.Key; int n = kv.Value.Value;
                if (mp == MobileParty.MainParty || mp.LeaderHero == null || n <= 0 || el.ItemModifier != null) continue;
                try
                {
                    if (!AiGear.DteTakes(el.Item)) continue;
                    int idx = mp.ItemRoster.FindIndexOfElement(el);
                    if (idx < 0 || mp.ItemRoster.GetElementNumber(idx) < n) continue;
                    if (AiGear.AddToArmory(mp, el.Item, n)) { mp.ItemRoster.AddToCounts(el, -n); armory += n; }
                }
                catch (Exception e) { Stumble("SackGoods.Armory", e); }
            }
            _d7Sacks++; _d7Took += took; _d7TookV += tookV; _d7Horses += horses; _d7Food += foodN; _d7Arms += armsN; _d7Armory += armory; _d7Uniq += uniqN;
            _d7Burn += burn; _d7BurnV += burnV; _d7Left += left; _d7Bal += Math.Abs(bal);
            if (MeasureOn)
            {
                var sb = new StringBuilder(500);
                sb.Append("Lup miast (L7): dzien ").Append((int)CampaignTime.Now.ToDays).Append(" | osada ").Append(st.StringId).Append(" | wybor ").Append(KName[kind])
                  .Append(" | czesc ").Append(share.ToString("0.000", Inv)).Append(" | polka ").Append(shelf0).Append(" -> ").Append(shelf1).Append(" szt.")
                  .Append(" | do taborow ").Append(took).Append(" szt. za ").Append(tookV).Append(" (unikaty ").Append(uniqN).Append(", konie i zwierzeta ").Append(horses)
                  .Append(", zywnosc ").Append(foodN).Append(", uzbrojenie ").Append(armsN).Append(" - do zbrojowni DTE ").Append(armory).Append(')')
                  .Append(" | spalone ").Append(burn).Append(" szt. za ").Append(burnV).Append(" | zostalo z czesci na polce ").Append(left)
                  .Append(" | druzyn ").Append(live.Count).Append(" (ludzie ").Append(men).Append(')')
                  .Append(" | bilans sztuk: zdjete ").Append(shelf0 - shelf1).Append(" - do taborow ").Append(p1 - p0).Append(" - spalone ").Append(burn).Append(" = ").Append(bal).Append('.');
                Log.Info(sb.ToString());
            }
            try
            {
                string town = st.Name != null ? st.Name.ToString() : st.StringId;
                foreach (var r in live)
                    if (r.Mp == MobileParty.MainParty)
                    {
                        Log.Player("From the shelves of " + town + " your men carried off what they could: the army took " + took.ToString("N0", Inv) + " goods, horses and arms"
                                   + (burn > 0 ? ", and the fire took " + burn.ToString("N0", Inv) + " more" : "") + ".");
                        break;
                    }
            }
            catch (Exception e) { Stumble("SackGoods.Message", e); }
        }

        private static void L7Part(StringBuilder sb)
        {
            sb.Append(" | L7: ");
            if (!GoodsOn) sb.Append("WYLACZONE (Sack Takes Goods) - polki nietkniete; ");
            sb.Append("lupow polek ").Append(_d7Sacks).Append(", do taborow ").Append(_d7Took).Append(" szt. za ").Append(_d7TookV).Append(" (unikaty ").Append(_d7Uniq)
              .Append(", konie ").Append(_d7Horses).Append(", zywnosc ").Append(_d7Food).Append(", uzbrojenie ").Append(_d7Arms).Append(" - zbrojownie DTE ").Append(_d7Armory)
              .Append("), spalone ").Append(_d7Burn).Append(" szt. za ").Append(_d7BurnV).Append(", zostalo na polkach ").Append(_d7Left).Append("; kontrola sztuk (prog 0) ").Append(_d7Bal);
        }

        private static void L7Clear() { _d7Sacks = 0; _d7Took = _d7TookV = _d7Horses = _d7Food = _d7Arms = _d7Armory = _d7Uniq = _d7Burn = _d7BurnV = _d7Left = _d7Bal = 0; }

        private static bool L7Any { get { return _d7Sacks > 0; } }
    }
}
