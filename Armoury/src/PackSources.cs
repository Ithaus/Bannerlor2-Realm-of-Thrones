using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// m6b (4) KONIE JUCZNE Z NICZEGO (raport roczny 13.10, wiersz 22: +56 478 jucznych w roku, staly doplyw 116-155 na dobe, oba biegi).
    /// Przyczyna (kod): (a) G VillagerPartyComponent.cs:17-34 InitializationArgs.InitializeVillagerPartyProperties - kazdy NOWY tabor wsi dostaje
    ///     z niczego (int)(0.5 x ludzie) najtanszego zwierzecia jucznego (Value > 40); tabor zniszczony przez bande oddaje juczne zwyciezcy (HorseFate.Spoils),
    ///     a gra stawia wsi nowy tabor z nowymi jucznymi (bieg roczny: 4 523 nowe tabory i lodzie, lodzie NavalDLC bez jucznych - InitializationArgs null);
    ///     (b) konwoj DTE (CutTheirSupplyBehavior): AddReinforcementSupplies daje 17 sumpter_horse z niczego, gra daje mu juczne karawany (PackMarket je
    ///     zostawia - "juczne gry zostaja"), a TransferCargoToTargetStash KOPIUJE caly ladunek do zbrojowni celu (slownik DTE, bez zdjecia z taboru),
    ///     po czym CleanupCaravan usuwa konwoj przez MobileParty.RemoveParty bez zdarzenia zniszczenia - juczne zostaja w zbrojowni drugi raz
    ///     (zbrojownie DTE: juczne 24 -> 9 749 w roku).
    /// Regula (jedna na zjawisko):
    ///  VillagerPacksFromHerd - postfiks InitializeVillagerPartyProperties: to, co gra dala (po starcie kampanii), schodzi z taboru; tabor bierze tyle samo
    ///     jucznych ze stada wsi (magazyn wsi - hodowla wsi, przeniesienie bez zlota); czego w stadzie nie ma - tabor jedzie z mniejsza liczba jucznych i
    ///     dobiera je ze stada przy kolejnych wyjazdach z domu (G VillagerCampaignBehavior.cs:205-236 MoveItemsToVillagerParty - zwierzeta bez limitu wagi).
    ///     Bez zakupu na targu (DECYZJA m6b: zakup za kiese wsi przesuwalby zloto wsi -> kasy miast i chwilowo zmniejszal danine korony ze wsi - prog 85 tys.).
    ///     Mniej jucznych nie zatrzymuje taboru: woz zabiera caly magazyn (MarketCarts.CapFloor, udzwig na miare ladunku), a mniej zwierzat nie zwalnia
    ///     (G DefaultPartySpeedCalculatingModel - kara stada tylko przy nadmiarze zwierzat nad ludzi).
    ///  DteConvoyPacksStayWithConvoy - prefiks/postfiks TransferCargoToTargetStash: juczne skopiowane do zbrojowni celu zdjete z niej (zostaja z konwojem i
    ///     odchodza z nim); zapas drogi konwoju (z niczego) i jego odejscie (RemoveParty) nazwane w ksiedze koni - netto 0 na konwoj.
    /// Ksiega koni (HorseBook, "Konie (E6/0)") dostaje nazwane pozycje (tylko licznik, przy Stage6MeasureLog): zrodla z niczego - konwoje DTE (DTE + gra) i partie BK
    /// (BannerKingsComponent.GiveMounts, PopulationPartyComponent.GivePackAnimals - zapas drogi, odchodzi z partia); ujscie - konie, ktore zostaly w taborze
    /// partii w chwili MobileParty.RemoveParty (po zdarzeniu zniszczenia - np. wedrowcy BK pomijani przez HorseFate.Spoils; bez zdarzenia - konwoje DTE).
    /// Linia doby "Juczne z niczego (m6b)". Bez pol zapisu. Potkniecia per obiekt (licznik), bez globalnych wylacznikow.
    /// </summary>
    internal static class PackSources
    {
        internal static bool HerdOn { get { var s = Settings.Current; return s != null && s.VillagerPacksFromHerd; } }
        internal static bool DteOn { get { var s = Settings.Current; return s != null && s.DteConvoyPacksStayWithConvoy; } }
        private static bool Live { get { var c = Campaign.Current; return c != null && c.GameStarted; } }
        private const int Cats = 5;

        // ------------------------------------------------------------ liczniki doby (linia "Juczne z niczego (m6b)")
        private static long _vNew, _vGiven, _vTaken, _vHerd, _vShort, _vStart, _vStartN, _vOff, _vOffN;
        private static long _dConv, _dDte, _dGame, _dUndone, _dKept, _bkN, _bk, _gone, _removed, _removedN;
        private static int _stumbles, _stumblesDay;
        private static readonly HashSet<string> _errSites = new HashSet<string>();
        private static string _hooks = "nie wpiete";

        // ------------------------------------------------------------ pozycje ksiegi koni (HorseBook zabiera raz na dobe)
        private static readonly long[] _srcDte = new long[Cats], _srcBk = new long[Cats], _sinkGone = new long[Cats], _sinkRemoved = new long[Cats];

        // partie po zdarzeniu MobilePartyDestroyed - zaraz po nim DestroyPartyAction wola RemoveParty (G DestroyPartyAction.cs:23-25); czyszczone co dobe
        private static readonly HashSet<MobileParty> _destroyed = new HashSet<MobileParty>(new PartyRefEq());

        private sealed class PartyRefEq : IEqualityComparer<MobileParty>
        {
            public bool Equals(MobileParty a, MobileParty b) { return ReferenceEquals(a, b); }
            public int GetHashCode(MobileParty o) { return RuntimeHelpers.GetHashCode(o); }
        }

        internal static void Reset()
        {
            DayReset();
            Array.Clear(_srcDte, 0, Cats); Array.Clear(_srcBk, 0, Cats); Array.Clear(_sinkGone, 0, Cats); Array.Clear(_sinkRemoved, 0, Cats);
            _destroyed.Clear();
            _stumbles = 0;
            _errSites.Clear();
        }

        private static void DayReset()
        {
            _vNew = _vGiven = _vTaken = _vHerd = _vShort = _vStart = _vStartN = _vOff = _vOffN = 0;
            _dConv = _dDte = _dGame = _dUndone = _dKept = _bkN = _bk = _gone = _removed = _removedN = 0;
            _stumblesDay = 0;
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++; _stumblesDay++;
            if (_errSites.Add(where)) Log.Error("PackSources." + where, e);
        }

        private static bool IsPack(ItemObject it) { return it != null && it.ItemCategory == DefaultItemCategories.PackAnimal && it.HorseComponent != null; }

        /// <summary>Konie i juczne rosteru wedlug kategorii ksiegi koni (HorseBook.Cat; -1 = nie kon).</summary>
        private static long[] Horses(ItemRoster r)
        {
            var a = new long[Cats];
            if (r == null) return a;
            for (int i = 0; i < r.Count; i++)
            {
                var el = r.GetElementCopyAtIndex(i);
                if (el.Amount <= 0) continue;
                int c = HorseBook.Cat(el.EquipmentElement.Item);
                if (c >= 0) a[c] += el.Amount;
            }
            return a;
        }

        // ================================================================ (a) juczne nowego taboru wsi
        /// <summary>Postfiks VillagerPartyComponent.InitializationArgs.InitializeVillagerPartyProperties(MobileParty, Village): roster jest swiezy
        /// (InitializeMobilePartyAroundPosition wypelnia tylko ludzi), wiec kazde zwierze juczne w nim dala gra (G VillagerPartyComponent.cs:20-33).</summary>
        public static void VillPost(MobileParty __0, Village __1)
        {
            try
            {
                var mp = __0; var v = __1;
                if (mp == null || v == null || mp.ItemRoster == null) return;
                var r = mp.ItemRoster;
                List<KeyValuePair<EquipmentElement, int>> got = null;
                int added = 0;
                for (int i = 0; i < r.Count; i++)
                {
                    var el = r.GetElementCopyAtIndex(i);
                    if (el.Amount <= 0 || !IsPack(el.EquipmentElement.Item)) continue;
                    if (got == null) got = new List<KeyValuePair<EquipmentElement, int>>(1);
                    got.Add(new KeyValuePair<EquipmentElement, int>(el.EquipmentElement, el.Amount));
                    added += el.Amount;
                }
                if (added <= 0) return;
                if (!Live) { _vStart++; _vStartN += added; return; }      // nowa kampania - tabory startowe jak w grze (stan wyjsciowy ksiegi koni)
                if (!HerdOn) { _vOff++; _vOffN += added; return; }
                _vNew++; _vGiven += added;
                foreach (var kv in got)
                {
                    int at = r.FindIndexOfElement(kv.Key);
                    int have = at >= 0 ? r.GetElementNumber(at) : 0;
                    int take = Math.Min(kv.Value, have);
                    if (take <= 0) continue;
                    r.AddToCounts(kv.Key, -take);
                    _vTaken += take;
                }
                int need = added;
                var wh = v.Settlement != null ? v.Settlement.ItemRoster : null;
                if (wh != null && need > 0) { int h = FromHerd(wh, r, need); need -= h; _vHerd += h; }
                _vShort += need;   // dobierze ze stada przy kolejnych wyjazdach z domu
            }
            catch (Exception e) { Stumble("VillPost", e); }
        }

        /// <summary>Juczne z magazynu wsi (hodowla wsi) do taboru - przeniesienie, bez zlota (zwierzeta wsi, tabor wsi).</summary>
        private static int FromHerd(ItemRoster wh, ItemRoster r, int need)
        {
            int moved = 0;
            for (int guard = 0; guard < 8 && need > 0; guard++)
            {
                int best = -1;
                for (int i = 0; i < wh.Count; i++)
                {
                    var el = wh.GetElementCopyAtIndex(i);
                    if (el.Amount > 0 && IsPack(el.EquipmentElement.Item)) { best = i; break; }
                }
                if (best < 0) break;
                var e = wh.GetElementCopyAtIndex(best);
                int n = Math.Min(need, e.Amount);
                wh.AddToCounts(e.EquipmentElement, -n);
                r.AddToCounts(e.EquipmentElement, n);
                moved += n; need -= n;
            }
            return moved;
        }

        // ================================================================ (b) konwoj DTE
        /// <summary>Prefiks / postfiks DTE CutTheirSupplyBehavior.AddReinforcementSupplies(MobileParty): zapas drogi konwoju (17 sumpter_horse) - zrodlo nazwane.</summary>
        public static void DteSupPre(MobileParty __0, out long[] __state)
        {
            __state = null;
            try { if (__0 != null && __0.ItemRoster != null) __state = Horses(__0.ItemRoster); } catch (Exception e) { Stumble("DteSupPre", e); }
        }

        public static void DteSupPost(MobileParty __0, long[] __state)
        {
            if (__state == null || __0 == null) return;
            try
            {
                var now = Horses(__0.ItemRoster);
                _dConv++;
                for (int c = 0; c < Cats; c++) { long d = now[c] - __state[c]; if (d > 0) { _srcDte[c] += d; _dDte += d; } }
            }
            catch (Exception e) { Stumble("DteSupPost", e); }
        }

        /// <summary>PackMarket.CreatePost (galaz konwoju DTE): juczne karawany od gry, ktore zostaja konwojowi - zrodlo nazwane w ksiedze koni.</summary>
        internal static void NoteDteGame(List<KeyValuePair<EquipmentElement, int>> got)
        {
            try
            {
                if (got == null) return;
                foreach (var kv in got)
                {
                    int c = HorseBook.Cat(kv.Key.Item);
                    if (c < 0 || kv.Value <= 0) continue;
                    _srcDte[c] += kv.Value; _dGame += kv.Value;
                }
            }
            catch (Exception e) { Stumble("NoteDteGame", e); }
        }

        /// <summary>Stan zbrojowni celu przed kopia ladunku: sztuki zwierzat jucznych, ktore konwoj wiezie.</summary>
        public static void CopyPre(MobileParty __0, MobileParty __1, out Dictionary<ItemObject, int> __state)
        {
            __state = null;
            try
            {
                if (__0 == null || __1 == null || __0.ItemRoster == null) return;
                var r = __0.ItemRoster;
                Dictionary<ItemObject, int> arm = null;
                var all = AiGear.Armories();
                if (all != null) all.TryGetValue(__1.Id, out arm);
                for (int i = 0; i < r.Count; i++)
                {
                    var el = r.GetElementCopyAtIndex(i);
                    var it = el.EquipmentElement.Item;
                    if (el.Amount <= 0 || !IsPack(it)) continue;
                    if (__state == null) __state = new Dictionary<ItemObject, int>();
                    if (__state.ContainsKey(it)) continue;
                    int had = 0;
                    if (arm != null) arm.TryGetValue(it, out had);
                    __state[it] = had;
                }
            }
            catch (Exception e) { __state = null; Stumble("CopyPre", e); }
        }

        /// <summary>Po kopii ladunku do zbrojowni celu: przyrost zwierzat jucznych (najwyzej tyle, ile konwoj wiezie) zdjety ze zbrojowni - zostaja z konwojem.</summary>
        public static void CopyPost(MobileParty __0, MobileParty __1, Dictionary<ItemObject, int> __state)
        {
            if (__state == null || __0 == null || __1 == null) return;
            try
            {
                var all = AiGear.Armories();
                Dictionary<ItemObject, int> arm;
                if (all == null || !all.TryGetValue(__1.Id, out arm) || arm == null) return;
                var r = __0.ItemRoster;
                foreach (var kv in __state)
                {
                    int now; if (!arm.TryGetValue(kv.Key, out now)) continue;
                    int carried = 0;
                    for (int i = 0; i < r.Count; i++) { var el = r.GetElementCopyAtIndex(i); if (el.Amount > 0 && el.EquipmentElement.Item == kv.Key) carried += el.Amount; }
                    int take = Math.Min(Math.Max(0, now - kv.Value), carried);
                    if (take <= 0) continue;
                    if (!DteOn) { _dKept += take; continue; }
                    int left = now - take;
                    if (left > 0) arm[kv.Key] = left; else arm.Remove(kv.Key);
                    _dUndone += take;
                }
            }
            catch (Exception e) { Stumble("CopyPost", e); }
        }

        // ================================================================ zapas drogi partii BK (tylko ksiega)
        public static void BkPre(ref MobileParty party, out long[] __state)
        {
            __state = null;
            try { if (party != null && party.ItemRoster != null) __state = Horses(party.ItemRoster); } catch (Exception e) { Stumble("BkPre", e); }
        }

        public static void BkPost(ref MobileParty party, long[] __state)
        {
            if (__state == null || party == null) return;
            try
            {
                var now = Horses(party.ItemRoster);
                _bkN++;
                for (int c = 0; c < Cats; c++) { long d = now[c] - __state[c]; if (d > 0) { _srcBk[c] += d; _bk += d; } }
            }
            catch (Exception e) { Stumble("BkPost", e); }
        }

        // ================================================================ konie, ktore odchodza z partia (RemoveParty)
        /// <summary>Nasluch MobilePartyDestroyed: partia za chwile przejdzie przez RemoveParty (DestroyPartyAction).</summary>
        internal static void OnDestroyed(MobileParty mp, PartyBase destroyer)
        {
            try { if (mp != null) _destroyed.Add(mp); } catch { }
        }

        /// <summary>Czy RemoveParty tej partii idzie po zdarzeniu zniszczenia (odczyt zdejmuje znacznik).</summary>
        internal static bool TakeDestroyed(MobileParty mp)
        {
            try { return mp != null && _destroyed.Remove(mp); } catch { return false; }
        }

        /// <summary>Prefiks MobileParty.RemoveParty: konie, ktore zostaly w taborze - RemoveParty czysci roster (G MobileParty.cs:3525) bez sladu.
        /// destroyed - po zdarzeniu zniszczenia (HorseFate.Spoils oddal juz, co oddaje), inaczej usunieta bez zniszczenia (konwoje DTE).</summary>
        internal static void OnRemoving(MobileParty mp, bool destroyed)
        {
            try
            {
                if (mp == null || mp.ItemRoster == null) return;
                var h = Horses(mp.ItemRoster);
                long n = 0;
                for (int c = 0; c < Cats; c++) { n += h[c]; if (destroyed) _sinkGone[c] += h[c]; else _sinkRemoved[c] += h[c]; }
                if (n <= 0) return;
                if (destroyed) _gone += n; else { _removed += n; _removedN++; }
            }
            catch (Exception e) { Stumble("OnRemoving", e); }
        }

        /// <summary>HorseBook.Daily: pozycje nazwane od ostatniego odczytu (zrodla z niczego: konwoje DTE, partie BK; ujscia: przy RemoveParty po zniszczeniu
        /// i bez zniszczenia) - kopiuje i zeruje.</summary>
        internal static void TakeBook(long[] dte, long[] bk, long[] gone, long[] removed)
        {
            for (int c = 0; c < Cats; c++)
            {
                if (dte != null) dte[c] = _srcDte[c];
                if (bk != null) bk[c] = _srcBk[c];
                if (gone != null) gone[c] = _sinkGone[c];
                if (removed != null) removed[c] = _sinkRemoved[c];
            }
            Array.Clear(_srcDte, 0, Cats); Array.Clear(_srcBk, 0, Cats); Array.Clear(_sinkGone, 0, Cats); Array.Clear(_sinkRemoved, 0, Cats);
        }

        // ================================================================ wpiecia
        internal static void ApplyAll(Harmony h)
        {
            var parts = new List<string>();
            try
            {
                var inner = AccessTools.Inner(typeof(VillagerPartyComponent), "InitializationArgs");
                var m = inner != null ? AccessTools.Method(inner, "InitializeVillagerPartyProperties", new[] { typeof(MobileParty), typeof(Village) }) : null;
                if (m == null) parts.Add("BRAK VillagerPartyComponent.InitializationArgs.InitializeVillagerPartyProperties - juczne taborow wsi jak w grze");
                else { h.Patch(m, postfix: new HarmonyMethod(typeof(PackSources), nameof(VillPost))); parts.Add("tabory wsi wpiete"); }
            }
            catch (Exception e) { parts.Add("tabory wsi: blad " + e.GetType().Name); Log.Error("PackSources.ApplyAll(tabory)", e); }
            try
            {
                var t = AccessTools.TypeByName("DynamicTroopEquipmentReupload.CutTheirSupplyBehavior");
                var sup = t != null ? AccessTools.Method(t, "AddReinforcementSupplies", new[] { typeof(MobileParty) }) : null;
                var cpy = t != null ? AccessTools.Method(t, "TransferCargoToTargetStash", new[] { typeof(MobileParty), typeof(MobileParty) }) : null;
                if (sup != null) { h.Patch(sup, prefix: new HarmonyMethod(typeof(PackSources), nameof(DteSupPre)), postfix: new HarmonyMethod(typeof(PackSources), nameof(DteSupPost))); parts.Add("zapas konwoju DTE wpiety"); }
                else parts.Add("BRAK DTE AddReinforcementSupplies (ksiega bez zapasu konwojow)");
                if (cpy != null) { h.Patch(cpy, prefix: new HarmonyMethod(typeof(PackSources), nameof(CopyPre)), postfix: new HarmonyMethod(typeof(PackSources), nameof(CopyPost))); parts.Add("kopia ladunku DTE wpieta"); }
                else parts.Add("BRAK DTE TransferCargoToTargetStash - juczne konwojow jak w DTE");
            }
            catch (Exception e) { parts.Add("DTE: blad " + e.GetType().Name); Log.Error("PackSources.ApplyAll(DTE)", e); }
            try
            {
                int bk = 0;
                var c1 = AccessTools.TypeByName("BannerKings.Components.BannerKingsComponent");
                var c2 = AccessTools.TypeByName("BannerKings.Components.PopulationPartyComponent");
                foreach (var m in new[] { c1 != null ? AccessTools.Method(c1, "GiveMounts") : null, c2 != null ? AccessTools.Method(c2, "GivePackAnimals") : null })
                {
                    if (m == null) continue;
                    var ps = m.GetParameters();
                    if (ps.Length != 1 || !ps[0].ParameterType.IsByRef || ps[0].Name != "party") continue;
                    h.Patch(m, prefix: new HarmonyMethod(typeof(PackSources), nameof(BkPre)), postfix: new HarmonyMethod(typeof(PackSources), nameof(BkPost)));
                    bk++;
                }
                parts.Add("zapas drogi BK " + bk + "/2");
            }
            catch (Exception e) { parts.Add("BK: blad " + e.GetType().Name); Log.Error("PackSources.ApplyAll(BK)", e); }
            try
            {
                var rm = AccessTools.Method(typeof(MobileParty), "RemoveParty", Type.EmptyTypes);
                if (rm != null) { h.Patch(rm, prefix: new HarmonyMethod(typeof(PackSources), nameof(RemovePre)) { priority = Priority.First }); parts.Add("RemoveParty wpiete"); }
                else parts.Add("BRAK MobileParty.RemoveParty");
            }
            catch (Exception e) { parts.Add("RemoveParty: blad " + e.GetType().Name); Log.Error("PackSources.ApplyAll(RemoveParty)", e); }
            _hooks = string.Join(", ", parts.ToArray());
            Log.Info("PackSources (m6b): " + _hooks + "; klucze VillagerPacksFromHerd (teraz " + (HerdOn ? "TAK" : "NIE") + "), DteConvoyPacksStayWithConvoy (teraz " + (DteOn ? "TAK" : "NIE")
                     + "), ksiega towarow przy RemoveParty FoodSourcesInLedger (teraz " + (GoodsLedger.SourcesOn ? "TAK" : "NIE") + ").");
        }

        /// <summary>Prefiks MobileParty.RemoveParty (First, tylko odczyt): konie (ksiega koni) i towar (ksiega towarow - tylko bez zdarzenia zniszczenia,
        /// bo po zdarzeniu ksiega towarow policzyla ten towar w "przepadlo z rozbitymi partiami").</summary>
        public static void RemovePre(MobileParty __instance)
        {
            if (__instance == null) return;
            bool destroyed = TakeDestroyed(__instance);
            OnRemoving(__instance, destroyed);
            if (!destroyed) { try { GoodsLedger.OnPartyRemoved(__instance); } catch (Exception e) { Stumble("RemovePre(towary)", e); } }
        }

        // ================================================================ linia doby
        internal static void Daily()
        {
            var inv = CultureInfo.InvariantCulture;
            int day = (int)CampaignTime.Now.ToDays;
            _destroyed.Clear();   // znaczniki bez RemoveParty (nie powinno sie zdarzyc) nie przechodza na nastepna dobe
            Log.Info("Juczne z niczego (m6b): dzien " + day
                     + " | tabory wsi (Villager Packs From Herd " + (HerdOn ? "TAK" : "NIE") + "): nowe " + _vNew + ", gra dala " + _vGiven + ", zdjete " + _vTaken
                     + ", ze stada wsi " + _vHerd + ", brak " + _vShort + " (dobiora ze stada wsi przy kolejnych wyjazdach z domu)"
                     + "; startowe " + _vStart + " z " + _vStartN + " jucznymi gry (stan wyjsciowy); przy wylaczonym kluczu " + _vOff + " z " + _vOffN + " (z niczego)"
                     + " | konwoje DTE (Dte Convoy Packs Stay With Convoy " + (DteOn ? "TAK" : "NIE") + "): zapas drogi " + _dConv + " konwojow, juczne DTE " + _dDte + " + gry " + _dGame
                     + " (z niczego, odchodza z konwojem), kopia do zbrojowni celu zdjeta " + _dUndone + ", zostawiona " + _dKept
                     + " | partie BK: zapas drogi " + _bkN + " partii, " + _bk + " koni i jucznych (z niczego, odchodza z partia)"
                     + " | konie przepadle przy RemoveParty: po zniszczeniu " + _gone + ", usuniete bez zniszczenia " + _removed + " (" + _removedN + " partii)"
                     + " | wpiecia: " + _hooks + " | potkniecia dzis " + _stumblesDay + " (razem " + _stumbles + ").");
            DayReset();
        }
    }
}
