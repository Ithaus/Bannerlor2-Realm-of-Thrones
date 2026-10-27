using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// 268 (Jeff 27.10 "czemu wojsko czesto nie ma pancerza", "tak zacznij od tych pieciu" - "zamowienia zbroi bez limitu 1 dziennie") - ZAMOWIENIE ZBROI NA TULOW I HELMOW
    /// = CALY BRAK W GRANICACH KIESY. Klucz AiArmourOrderFull (MCM "AI Armour Order Full").
    ///
    /// Dowod (audyt 27.10, log Jeffa 26.10 i logi audytu): linia "Dozbrajanie" d109002 "partia zamowila juz dzis 11132, bez miasta w wizycie 5975" - to limit E3-Z1
    /// (MenUpgradeOrders: zamowienie tier+1 dla ludzi, ktorzy JUZ maja sztuke; "bez miasta" = zalogi zamkow poza dniem zamowienia). Gola czesc wojska (bez korpusu)
    /// zamawia inna droga - AiGear.TryBuyCore przy pustej polce - i tam zamowienie z wizyty bylo najwyzej 10 szt. na koszyk (wpis 67, AmmoOrders.Amount), raz na
    /// SupplyDemandOrderRepeatDays na kupca, a lord stojacy w zamku nie zamawial wcale. Warsztaty planuja wedlug popytu koszyka (SupplyDemandBase x zamoznosc x waga
    /// tieru + zamowienia, sufit SupplyDemandOrderCap) - "platnerz 946 (wedlug braku)" rak przeniesionych z cechow bez roboty przy ok. 85% korpusu i 56% helmow lordow.
    ///
    /// Regula (z biezacego zysku, jedna dla lordow AI, zalog miast i zamowien zamkow - jak tb dla amunicji):
    ///  (2a) kupiec, ktory po zakupach ma brak korpusu albo helmu, a na polce nie ma sztuki tego tieru ani o jeden nizszej (AiGear.OnShelf - jak dotad), zamawia
    ///       max(dawne min(10, brak), min(brak koszyka, SupplyDemandOrderCap, sztuk, ktore pokrywa kiesa wizyty)) - kiesa: budzet wizyty TryBuyCore (GoldPart pana +
    ///       sakiewka ludzi, przyciete przydzialem sprzetu rodu), malejacy o zamowione sztuki x wczorajsza srednia cena sztuki tej grupy u tego kupujacego
    ///       (Measure174b.AvgYesterday; brak - srednia wszystkich zakupow AI; brak obu - tylko dawne 10). Nikt nie placi w chwili zamowienia (zamowienie to popyt
    ///       koszyka, arm_unmet w zapisie); placi kupiec przy zakupie ta sama droga co dzis (sakiewka -> kiesa pana -> kasa miasta). Wizyta bez budzetu nic nie zamawia
    ///       (TryBuyCore wychodzi wczesniej) - "nic z niczego". Dedup wpisu 81 (NoteUnmetOnce) bez zmian.
    ///  (2b) lord AI stojacy w ZAMKU (zakupy tylko w miastach - 171 C2.3) raz na dobe liczy braki korpusu i helmu (AiGear.Deficit) i zamawia je w miescie zamku
    ///       (GarrisonCarts.MarketFor - to samo miasto, z ktorego jada wozy zalogi), ta sama regula 2a, kiesa = AiGear.BudgetPreview(lord, miasto). Nic nie kupuje zdalnie.
    ///  (2c) E3-Z1 (MenUpgradeOrders.Note): zbroja i helm bez limitu 1 zamowienia na partie na dobe; zaloga zamku poza dniem zamowienia - w miescie zamku.
    /// Linia doby "Zbroja AI - zamowienia (268)" (z AiGear.BardingDaily, zaraz po linii amunicji tb). Klucz NIE - dawne 10 szt., bez zamowien z zamku, Z1 raz na dobe.
    /// </summary>
    internal static class ArmourOrders
    {
        internal const int WLord = 0, WGarrison = 1, WCastle = 2, WCastleLord = 3, Who = 4;
        private static readonly string[] WhoName = { "lordowie AI", "zalogi miast", "zamowienia zamkow (w miescie)", "lordowie w zamku (miasto zamku)" };
        private static readonly string[] KindName = { "korpus", "helm" };
        private const int OldCap = 10;   // wpis 67: dawne zamowienie z jednej wizyty

        internal static bool On { get { var s = Settings.Current; return s != null && s.AiArmourOrderFull; } }

        internal static bool IsArmour(ItemObject.ItemTypeEnum t) { return t == ItemObject.ItemTypeEnum.BodyArmor || t == ItemObject.ItemTypeEnum.HeadArmor; }
        private static int KindOf(ItemObject.ItemTypeEnum t) { return t == ItemObject.ItemTypeEnum.HeadArmor ? 1 : 0; }

        // liczniki doby (tylko log) [kupiec, rodzaj]
        private static readonly int[] _visits = new int[Who], _visitsShort = new int[Who];
        private static readonly long[,] _short = new long[Who, 2], _emptyB = new long[Who, 2], _emptyPcs = new long[Who, 2], _ordered = new long[Who, 2], _orderedPcs = new long[Who, 2],
                                        _over10 = new long[Who, 2], _cut = new long[Who, 2], _repeat = new long[Who, 2], _leftB = new long[Who, 2], _leftPcs = new long[Who, 2];
        private static int _cNoTown, _cNoPurse, _cWar, _cStumbles, _stumbles;
        private static long _goldMoved;   // zloto kupca zmienione w chwili zamowienia (cel 0 - zamowienie to tylko popyt)
        private static readonly Dictionary<MobileParty, int> _castleDay = new Dictionary<MobileParty, int>();

        internal static void Reset() { ClearDay(); _stumbles = 0; _castleDay.Clear(); }

        private static void ClearDay()
        {
            Array.Clear(_visits, 0, Who); Array.Clear(_visitsShort, 0, Who);
            foreach (var a in new[] { _short, _emptyB, _emptyPcs, _ordered, _orderedPcs, _over10, _cut, _repeat, _leftB, _leftPcs }) Array.Clear(a, 0, a.Length);
            _cNoTown = _cNoPurse = _cWar = _cStumbles = 0; _goldMoved = 0;
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            if (_stumbles <= 3) Log.Error("ArmourOrders." + where + " (potkniecie " + _stumbles + ")", e);
        }

        /// <summary>268 (czysta funkcja, autotest T5): sztuk, ktore pokrywa kiesa - min(brak, sufit koszyka, floor(kiesa / cena sztuki)); brak, kiesa albo cena &lt;= 0 - 0.
        /// Zamowienie z wizyty to max(dawne min(10, brak), Amount) - nigdy mniej niz przed 268.</summary>
        internal static int Amount(int deficit, float cap, int budget, int unit)
        {
            if (deficit <= 0 || budget <= 0 || unit <= 0) return 0;
            long backed = budget / unit;
            int c = Math.Max(1, (int)cap);
            return (int)Math.Min(Math.Min(deficit, c), backed);
        }

        /// <summary>Wczorajsza srednia cena sztuki grupy (korpus / helm) u kupujacego (Measure174b), inaczej srednia wszystkich zakupow AI; 0 - nieznana.</summary>
        internal static int UnitOf(int buyerClass, ItemObject.ItemTypeEnum ty)
        {
            int u = Measure174b.AvgYesterday(buyerClass, ty == ItemObject.ItemTypeEnum.HeadArmor ? Measure174b.GHelm : Measure174b.GBody);
            return u > 0 ? u : Math.Max(0, AiGear.AvgPiecePriceYesterday);
        }

        private static long GoldOf(MobileParty mp)
        {
            try { long g = MenPurse.Get(mp); var h = mp != null ? mp.LeaderHero : null; if (h != null) g += h.Gold; return g; } catch { return 0; }
        }

        /// <summary>Koszyk zbroi (korpus albo helm) z brakiem po zakupach, polka bez tieru t ani t-1: zamowienie (2a). Klucz NIE - dawne min(10, brak). backLeft - kiesa
        /// wizyty jeszcze niezajeta zamowieniami (maleje o zamowione sztuki x cena). Zwraca, czy zamowienie weszlo (false - ten kupiec zamowil to w ostatnich dobach).</summary>
        internal static bool Order(int who, MobileParty buyer, Settlement market, ItemObject.ItemTypeEnum ty, int tr, int deficit, ref int backLeft, int buyerClass, out float amount)
        {
            amount = 0f;
            if (deficit <= 0 || market == null || buyer == null) return false;
            int k = KindOf(ty);
            int old = Math.Min(OldCap, deficit);
            int a = old, unit = 0, capped = old;
            try
            {
                if (On)
                {
                    var s = Settings.Current;
                    float cap = Math.Max(1f, s != null ? s.SupplyDemandOrderCap : 60f);
                    unit = UnitOf(buyerClass, ty);
                    capped = (int)Math.Min(deficit, Math.Max(1, (int)cap));
                    a = Math.Max(old, Amount(deficit, cap, backLeft, unit));
                }
            }
            catch (Exception e) { Stumble("Order", e); a = old; }
            long g0 = GoldOf(buyer);
            bool ordered = SupplyDemand.NoteUnmetOnce(buyer, market, ty, tr, a);
            _goldMoved += Math.Abs(GoldOf(buyer) - g0);
            amount = a;
            if (who < 0 || who >= Who) return ordered;
            _short[who, k] += deficit; _emptyB[who, k]++; _emptyPcs[who, k] += deficit;
            if (ordered)
            {
                _ordered[who, k]++; _orderedPcs[who, k] += a;
                if (a > OldCap) _over10[who, k] += a - OldCap;
                if (On && unit > 0 && a < capped) _cut[who, k] += capped - a;   // cena znana - przyciecie kiesa (cena nieznana - dawne 10, bez licznika)
                if (unit > 0) backLeft = (int)Math.Max(0L, backLeft - (long)a * unit);
            }
            else _repeat[who, k]++;
            return ordered;
        }

        /// <summary>Koszyk zbroi z brakiem, ale na polce zostala sztuka tieru t albo t-1 (nie kupiona: budzet albo limit sztuk wizyty) - bez zamowienia. Tylko licznik.</summary>
        internal static void NoteLeft(int who, ItemObject.ItemTypeEnum ty, int deficit)
        {
            if (who < 0 || who >= Who || deficit <= 0) return;
            int k = KindOf(ty);
            _short[who, k] += deficit; _leftB[who, k]++; _leftPcs[who, k] += deficit;
        }

        /// <summary>Wizyta kupca doszla do zamowien po zakupach (shortArmour - zostal brak korpusu albo helmu). Tylko licznik.</summary>
        internal static void NoteVisit(int who, bool shortArmour)
        {
            if (who < 0 || who >= Who) return;
            _visits[who]++;
            if (shortArmour) _visitsShort[who]++;
        }

        /// <summary>(2b) Lord AI w ZAMKU (AiGear.TryBuy przed filtrem WouldBuy - zakupy brakow tylko w miastach): raz na dobe na partie braki korpusu i helmu
        /// (AiGear.Deficit) zamawiane w miescie zamku (GarrisonCarts.MarketFor), regula 2a z kiesa AiGear.BudgetPreview(lord, miasto). Nic nie kupuje, zlota nie rusza.
        /// Nie rzuca.</summary>
        internal static void CastleVisit(MobileParty mp, Settlement st)
        {
            try
            {
                if (!On || mp == null || st == null || !st.IsCastle || !AiGear.On || !GarrisonCarts.On) return;
                if (mp.IsMainParty || !mp.IsLordParty || !mp.IsActive || mp.MapEvent != null || mp.LeaderHero == null || !mp.LeaderHero.IsAlive) return;
                if (mp.CurrentSettlement != st) return;
                int day = (int)CampaignTime.Now.ToDays, last;
                if (_castleDay.TryGetValue(mp, out last) && last == day) return;
                _castleDay[mp] = day;
                if (FactionManager.IsAtWarAgainstFaction(mp.MapFaction, st.MapFaction)) { _cWar++; return; }
                var all = AiGear.Armories();
                if (all == null) return;
                Dictionary<ItemObject, int> armory;
                all.TryGetValue(mp.Id, out armory);
                var need = AiGear.Deficit(mp, armory);
                bool any = false;
                foreach (var kv in need) if (kv.Value > 0 && IsArmour((ItemObject.ItemTypeEnum)(kv.Key / 10))) { any = true; break; }
                if (!any) { NoteVisit(WCastleLord, false); return; }
                float dist; string why;
                var market = GarrisonCarts.MarketFor(st, out dist, out why, false);
                if (market == null || market.Town == null || FactionManager.IsAtWarAgainstFaction(mp.MapFaction, market.MapFaction)) { _cNoTown++; return; }
                int back = AiGear.BudgetPreview(mp, market);
                if (back <= 0) { _cNoPurse++; return; }   // nic z niczego - bez kiesy bez zamowienia (jak TryBuyCore przy budzecie 0)
                NoteVisit(WCastleLord, true);
                foreach (var kv in need)
                {
                    if (kv.Value <= 0) continue;
                    var ty = (ItemObject.ItemTypeEnum)(kv.Key / 10); int tr = kv.Key % 10;
                    if (!IsArmour(ty)) continue;
                    if (AiGear.OnShelfFor(market, ty, tr, mp)) { NoteLeft(WCastleLord, ty, kv.Value); continue; }
                    float amt;
                    Order(WCastleLord, mp, market, ty, tr, kv.Value, ref back, Measure174b.BLord, out amt);
                }
            }
            catch (Exception e) { _cStumbles++; Stumble("CastleVisit", e); }
        }

        /// <summary>Linia doby "Zbroja AI - zamowienia (268)" i zerowanie licznikow (z AiGear.BardingDaily, po linii amunicji tb).</summary>
        internal static void Daily()
        {
            try
            {
                var s = Settings.Current;
                if (s == null) return;
                var inv = CultureInfo.InvariantCulture;
                int day = (int)CampaignTime.Now.ToDays;
                var sb = new StringBuilder(1400);
                sb.Append("Zbroja AI - zamowienia (268): dzien ").Append(day).Append(" | korpus i helm przy pustej polce ")
                  .Append(On ? "[WL caly brak w granicach kiesy, najwyzej " + Math.Max(1f, s.SupplyDemandOrderCap).ToString("0", inv) + " szt.]" : "W CIENIU (AiArmourOrderFull) - najwyzej 10 szt.");
                for (int w = 0; w < Who; w++)
                {
                    sb.Append(" | ").Append(WhoName[w]).Append(": wizyt ").Append(_visits[w]).Append(", z brakiem zbroi po zakupach ").Append(_visitsShort[w]);
                    if (w == WCastleLord) sb.Append(", bez miasta zamku ").Append(_cNoTown).Append(", bez kiesy ").Append(_cNoPurse).Append(", zamek w wojnie ").Append(_cWar);
                    for (int k = 0; k < 2; k++)
                    {
                        sb.Append("; ").Append(KindName[k]).Append(" - brak ").Append(_short[w, k]).Append(" szt., polka bez tieru i nizszego ").Append(_emptyB[w, k]).Append(" koszykow / ")
                          .Append(_emptyPcs[w, k]).Append(" szt. - zamowione ").Append(_ordered[w, k]).Append(" (").Append(_orderedPcs[w, k]).Append(" szt., ponad dawne 10: ")
                          .Append(_over10[w, k]).Append(", przyciete kiesa ").Append(_cut[w, k]).Append(" szt.), juz zamowione ").Append(_repeat[w, k])
                          .Append(", na polce zostala sztuka ").Append(_leftB[w, k]).Append(" koszykow / ").Append(_leftPcs[w, k]).Append(" szt.");
                    }
                }
                int bb, hb;
                float ob = SupplyDemand.OpenOrders(ItemObject.ItemTypeEnum.BodyArmor, out bb), oh = SupplyDemand.OpenOrders(ItemObject.ItemTypeEnum.HeadArmor, out hb);
                sb.Append(" | otwarte zamowienia w osadach: korpus ").Append((long)Math.Round(ob)).Append(" szt. (").Append(bb).Append(" koszykow), helm ")
                  .Append((long)Math.Round(oh)).Append(" szt. (").Append(hb).Append(" koszykow)")
                  .Append(" | cena sztuki wczoraj (lordowie): korpus ").Append(Measure174b.AvgYesterday(Measure174b.BLord, Measure174b.GBody))
                  .Append(" zl, helm ").Append(Measure174b.AvgYesterday(Measure174b.BLord, Measure174b.GHelm)).Append(" zl, wszystkie sztuki AI ").Append(AiGear.AvgPiecePriceYesterday).Append(" zl")
                  .Append(" | zloto przy zamowieniu ").Append(_goldMoved)
                  .Append(" | potkniecia ").Append(_stumbles).Append(" (zamek ").Append(_cStumbles).Append(").");
                Log.Info(sb.ToString());
                // pamiec doby zamkow: tylko dzisiejsze i czynne partie
                if (_castleDay.Count > 0)
                {
                    List<MobileParty> rm = null;
                    foreach (var kv in _castleDay) if (kv.Key == null || !kv.Key.IsActive || kv.Value != day) { if (rm == null) rm = new List<MobileParty>(); rm.Add(kv.Key); }
                    if (rm != null) foreach (var p in rm) _castleDay.Remove(p);
                }
            }
            catch (Exception e) { Stumble("Daily", e); }
            ClearDay();
        }
    }
}
