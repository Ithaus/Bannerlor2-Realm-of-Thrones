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
    /// 307a (Jeff 31.10 "karawany, aby dzialaly i zarabialy"; krytyk 307 karawany wagi 9 i 4) - KAZDY ZAKUP Z KIESY KARAWANY W KSIEDZE E3-T1 I W DZIENNIKU 305a.
    /// Dowod [L] (autotest 142b, sonda 306b): karawana gracza 'Convoy of Ser Cristal Hightower' - kiesa 30000 przy tworzeniu w Driftwood Hall, nastepnej doby 8034
    /// przy zakupach w dzienniku 875 (wool x11); brakuje 21091 (142a: co do zlotowki to samo). W dobie 108898 w Pentos sprzedaje konie (charger x4, t2_battania_horse
    /// x11, hunter, battania_horse, aserai_horse) za ok. 56 tys., a koszt/szt w dzienniku to oszacowanie ksiegi (Restore: ok. 0.26 x Value, rowno dla kazdego konia)
    /// - konie nie przeszly przez TradePostfix. "towar w jukach ... w tym zwierzeta i inne 0" przez 20 dob (WorthOf liczyl zwierzeta tylko z wpisem w ksiedze).
    /// Przyczyna [K] (dekompilacja BannerKings.dll, BKPartyBehavior.OnSettlementLeft :303-346): "gabka" BK przy wyjezdzie karawany z miasta kupuje KAZDY przedmiot
    /// z polki tanszy niz 0.33 x ItemValue (konie bojowe w Driftwood Hall - indeks war_horse ok. 0.26), do 80% udzwigu, wprost: ItemRoster.AddToCounts,
    /// PartyTradeGold -= cena, Town.ChangeGold(+) - BEZ SellItemsAction, wiec latka TradePrefix/TradePostfix (SellItemsAction.ApplyInternal) jej nie widzi.
    /// CaravanTrade284.LeftPostfix i CaravanBulk.LeftPostfix cofaja z gabki tylko towar handlowy bez koni (kategorie z zamknietym kupnem / surowce masowe).
    /// BK BuyCategory (BKCaravansBehavior :1742-1865) idzie przez SellItemsAction (ksiega widzi), z limitem 1500 zl na kategorie - 21 tys. w koniach to nie on.
    /// Regula (klucz CaravanSpongeToBook; jedna dla karawan gracza i AI): prefiks (Priority.Last - po prefiksach CaravanBulk / CaravanTrade284 / CaravanAmmo, tuz
    /// przed gabka) zapamietuje juki i kiese, postfiks (Priority.Last - po cofnieciach CaravanBulk / CaravanTrade284) ksieguje to, co netto zostalo: sztuki
    /// przybyle i zloto netto zeszle z kiesy, rozdzielone na przedmioty wedlug sztuki x Value x indeks kategorii w tym miescie (gabka placi jedna cene polki
    /// na pozycje). Wpis ksiegi jak zakup z TradePostfix (Sync, koszt, doba), karawana gracza - dziennik 305a (CaravanSettle.NoteTrade kupno). Bez zmiany handlu BK.
    /// POLOW BEZ ZAKUPU (klucz CaravanCatchAtZeroCost; krytyk waga 4): ryby z polowu zalogi na morzu (NavalDLC NavalFishingCampaignBehaviour.OnHourlyTickParty,
    /// ramka A17 F2 (c) - A17Fix.CrewFin) wchodza do ksiegi karawany z kosztem 0 (nic nie zaplacono) - WorthOf liczy je po min(0, rynek) = 0, wiec wyplata 259
    /// nie wychodzi z gotowki za towar, ktory jeszcze nie zostal sprzedany; zysk dopiero ze sprzedazy. Dotad: ryby bez wpisu wyceniane wartosc x min(1, indeks),
    /// a przy sprzedazy dostawaly koszt z oszacowania (142b: fish x44 koszt/szt 15 bez zakupu).
    /// Linia doby "Karawany - zakupy poza handlem gry (307a): dzien N". Nic w zapisie. Wyjatek per wywolanie - licznik potkniec.
    /// </summary>
    internal static partial class CaravanGoods
    {
        private static bool Sponge307 { get { var s = Settings.Current; return s != null && s.CaravanSpongeToBook; } }
        private static bool Catch307 { get { var s = Settings.Current; return s != null && s.CaravanCatchAtZeroCost; } }
        internal static bool Animals307 { get { var s = Settings.Current; return s != null && s.CaravanWorthTradeAnimals; } }

        private static bool _w307Left;
        private static long _w307Unk;                                       // WorthOf: zwierzeta bez wpisu wycenione (sztuko-wyceny od linii)
        private static long _s307N, _s307G, _s307Calls, _s307Horse, _s307HorseG, _s307PlayerG, _s307Neg, _s307Shadow, _s307ShadowG;
        private static long _c307N, _c307Cars;
        private static int _s307Stumbles, _s307StumbleSess;
        private static readonly Dictionary<string, long> _s307By = new Dictionary<string, long>(StringComparer.Ordinal);

        public sealed class Left307 { public MobileParty Mp; public Settlement St; public int Gold0; public Dictionary<ItemObject, int> N0; }

        private static void Reset307()
        {
            _w307Unk = 0; _s307N = _s307G = _s307Calls = _s307Horse = _s307HorseG = _s307PlayerG = _s307Neg = _s307Shadow = _s307ShadowG = 0;
            _c307N = _c307Cars = 0; _s307Stumbles = _s307StumbleSess = 0; _s307By.Clear();
        }

        private static void Stumble307(string where, Exception e)
        {
            _s307Stumbles++; _s307StumbleSess++;
            try { if (_err.Add("307." + where)) Log.Error("CaravanGoods307." + where, e); } catch { }
        }

        /// <summary>Prefiks BKPartyBehavior.OnSettlementLeft (Priority.Last): stan jukow i kiesy karawany tuz przed gabka BK (ta sama bramka co gabka: karawana, miasto).</summary>
        public static void SpongePre(MobileParty __0, Settlement __1, out Left307 __state)
        {
            __state = null;
            try
            {
                if (__0 == null || __1 == null || !__0.IsCaravan || __1.Town == null || __0.ItemRoster == null) return;
                if (Campaign.Current == null || !Campaign.Current.GameStarted) return;
                var r = __0.ItemRoster;
                var d = new Dictionary<ItemObject, int>();
                for (int i = 0; i < r.Count; i++)
                {
                    var it = r.GetElementCopyAtIndex(i).EquipmentElement.Item;
                    if (it != null && !d.ContainsKey(it)) d[it] = r.GetItemNumber(it);
                }
                __state = new Left307 { Mp = __0, St = __1, Gold0 = __0.PartyTradeGold, N0 = d };
            }
            catch (Exception e) { __state = null; Stumble307("SpongePre", e); }
        }

        /// <summary>Postfiks gabki (Priority.Last - po cofnieciach CaravanBulk / CaravanTrade284): netto przybyle sztuki i netto zeszle zloto - do ksiegi i dziennika.</summary>
        public static void SpongePost(Left307 __state)
        {
            if (__state == null) return;
            try
            {
                var mp = __state.Mp; var st = __state.St; var town = st.Town;
                var r = mp.ItemRoster;
                long paid = (long)__state.Gold0 - mp.PartyTradeGold;
                List<KeyValuePair<ItemObject, int>> got = null;
                double wsum = 0;
                for (int i = 0; i < r.Count; i++)
                {
                    var it = r.GetElementCopyAtIndex(i).EquipmentElement.Item;
                    if (it == null) continue;
                    int was; __state.N0.TryGetValue(it, out was);
                    int extra = r.GetItemNumber(it) - was;
                    if (extra <= 0) continue;
                    if (got == null) got = new List<KeyValuePair<ItemObject, int>>();
                    bool dup = false;
                    foreach (var kv in got) if (kv.Key == it) { dup = true; break; }
                    if (dup) continue;
                    got.Add(new KeyValuePair<ItemObject, int>(it, extra));
                    wsum += extra * Weight307(town, it);
                }
                if (got == null) return;
                _s307Calls++;
                if (paid <= 0 || !(wsum > 0)) { _s307Neg++; return; }   // sztuki bez zaplaty netto (inna droga w tym samym wywolaniu) - bez wpisu
                if (!Sponge307)
                {
                    _s307Shadow++; _s307ShadowG += paid;                  // klucz NIE: tylko pomiar (jak dotad - poza ksiega)
                    return;
                }
                long left = paid;
                for (int k = 0; k < got.Count; k++)
                {
                    var it = got[k].Key; int n = got[k].Value;
                    long part = k == got.Count - 1 ? left : (long)Math.Round(paid * (n * Weight307(town, it)) / wsum);
                    if (part > left) part = left;
                    left -= part;
                    int n0 = Math.Max(0, r.GetItemNumber(it) - n);
                    BookBuy307(mp, it, n0, n, part, st);
                    _s307N += n; _s307G += part;
                    if (it.HasHorseComponent) { _s307Horse += n; _s307HorseG += part; }
                    var id = it.StringId ?? "?";
                    long x; _s307By.TryGetValue(id, out x); _s307By[id] = x + part;
                    if (mp.ActualClan == Clan.PlayerClan) _s307PlayerG += part;
                }
            }
            catch (Exception e) { Stumble307("SpongePost", e); }
        }

        /// <summary>Waga podzialu zaplaty gabki: Value x indeks kategorii w tym miescie (gabka placi cene polki jednej pozycji - ta sama miara z dokladnoscia do zmiany indeksu).</summary>
        private static double Weight307(Town town, ItemObject it)
        {
            double v = Math.Max(1, it.Value);
            try { if (town != null && it.ItemCategory != null) { float ix = town.GetItemCategoryPriceIndex(it.ItemCategory); if (ix > 0f && !float.IsInfinity(ix)) v *= ix; } } catch { }
            return v;
        }

        /// <summary>Wpis zakupu do ksiegi jak TradePostfix (n0 - sztuki przed zakupem, got - przybyle, paid - zaplacone) i dziennik karawany gracza (305a).</summary>
        private static void BookBuy307(MobileParty mp, ItemObject it, int n0, int got, long paid, Settlement st)
        {
            if (mp == null || it == null || got <= 0 || paid < 0) return;
            lock (_worthLock)
            {
                var l = Sync(mp, it, n0, true);
                double now = Now;
                l.Day = l.N > 0 ? (l.Day * l.N + now * got) / (l.N + got) : now;
                l.Cost += paid; l.N += got;
            }
            _bookBuys++; _bookBuyUnits += got;
            if (mp.ActualClan == Clan.PlayerClan) CaravanSettle.NoteTrade(mp, 1, paid, 0, 0.0, it, st, got);
        }

        /// <summary>A17Fix.CrewFin: polow zalogi na morzu (n0 - sztuki przed polowem, d - przybyle) - karawana: wpis ksiegi z kosztem 0 (klucz CaravanCatchAtZeroCost).</summary>
        internal static void NoteFreeCatch(MobileParty mp, ItemObject it, int n0, int d)
        {
            if (mp == null || it == null || d <= 0 || !mp.IsCaravan || !Catch307) return;
            try
            {
                lock (_worthLock)
                {
                    var l = Sync(mp, it, n0, true);
                    double now = Now;
                    l.Day = l.N > 0 ? (l.Day * l.N + now * d) / (l.N + d) : now;
                    l.N += d;                                              // koszt 0 - nic nie zaplacono
                }
                _c307N += d; _c307Cars++;
            }
            catch (Exception e) { Stumble307("NoteFreeCatch", e); }
        }

        internal static void Apply307(Harmony h)
        {
            try
            {
                var bkp = AccessTools.TypeByName("BannerKings.Behaviours.BKPartyBehavior");
                var left = bkp != null ? AccessTools.DeclaredMethod(bkp, "OnSettlementLeft", new[] { typeof(MobileParty), typeof(Settlement) }) : null;
                if (left != null)
                {
                    h.Patch(left, prefix: new HarmonyMethod(typeof(CaravanGoods), nameof(SpongePre)) { priority = Priority.Last },
                            postfix: new HarmonyMethod(typeof(CaravanGoods), nameof(SpongePost)) { priority = Priority.Last });
                    _w307Left = true;
                }
                Log.Info("Karawany - zakupy poza handlem gry (307a): gabka BK przy wyjezdzie (BKPartyBehavior.OnSettlementLeft) " + (_w307Left ? "wpieta" : "NIE (brak metody BK)")
                         + " - zakup do ksiegi kosztu E3-T1 i dziennika 305a; polow zalogi (A17 F2 c) z kosztem 0 w ksiedze; wycena zwierzat bez wpisu w WorthOf (259).");
            }
            catch (Exception e) { Log.Error("CaravanGoods.Apply307", e); }
        }

        /// <summary>ArmouryBehavior (po CaravanGoods.Daily): linia doby 307a i zerowanie licznikow.</summary>
        internal static void Daily307()
        {
            var s = Settings.Current;
            if (s == null || Campaign.Current == null) return;
            try
            {
                int day = (int)CampaignTime.Now.ToDays - 1;
                var sb = new StringBuilder(600);
                sb.Append("Karawany - zakupy poza handlem gry (307a): dzien ").Append(day)
                  .Append(" | klucze: gabka do ksiegi (Caravan Sponge To Book) ").Append(s.CaravanSpongeToBook ? "TAK" : "NIE")
                  .Append(", polow po 0 (Caravan Catch At Zero Cost) ").Append(s.CaravanCatchAtZeroCost ? "TAK" : "NIE")
                  .Append(", zwierzeta w wycenie (Caravan Worth Trade Animals) ").Append(s.CaravanWorthTradeAnimals ? "TAK" : "NIE")
                  .Append(" | latka gabki ").Append(_w307Left ? "wpieta" : "NIE")
                  .Append(" | gabka BK: wyjazdow z zakupem ").Append(_s307Calls).Append(", zaksiegowane ").Append(_s307N).Append(" szt. za ").Append(_s307G).Append(" zl (konie i zwierzeta ")
                  .Append(_s307Horse).Append(" szt. za ").Append(_s307HorseG).Append(" zl; karawany gracza ").Append(_s307PlayerG).Append(" zl), bez zaplaty netto ").Append(_s307Neg)
                  .Append(", poza ksiega przy kluczu NIE ").Append(_s307Shadow).Append(" wyjazdow za ").Append(_s307ShadowG).Append(" zl [");
                var l = new List<KeyValuePair<string, long>>(_s307By);
                l.Sort((a, b) => b.Value.CompareTo(a.Value));
                for (int i = 0; i < l.Count && i < 8; i++) sb.Append(i > 0 ? ", " : "").Append(l[i].Key).Append(' ').Append(l[i].Value);
                sb.Append(l.Count == 0 ? "-]" : "]");
                sb.Append(" | polow zalogi do ksiegi po 0: ").Append(_c307N).Append(" szt. w ").Append(_c307Cars).Append(" godzinach karawan")
                  .Append(" | wycena 259: zwierzat bez wpisu ").Append(_w307Unk).Append(" sztuko-wycen (suma po wszystkich wywolaniach WorthOf, takze podgladach finansow - nie liczba sztuk)")
                  .Append(" | potkniecia ").Append(_s307Stumbles).Append(" (sesja ").Append(_s307StumbleSess).Append(").");
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Stumble307("Daily307", e); }
            _w307Unk = 0; _s307N = _s307G = _s307Calls = _s307Horse = _s307HorseG = _s307PlayerG = _s307Neg = _s307Shadow = _s307ShadowG = 0;
            _c307N = _c307Cars = 0; _s307Stumbles = 0; _s307By.Clear();
        }
    }
}
