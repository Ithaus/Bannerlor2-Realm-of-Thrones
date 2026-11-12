using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// 458-t - tabor partii z wodzem, ktory uszedl (Jeff 12.11: "jak uciekl to chyba z taborem"). Klucz Commander Withdraws Under Rearguard (NIE - bez zmian).
    ///
    /// Drogi lupu po wygranej gracza, ktore zabieraja dobra pokonanej partii (sprawdzone w dekompilacji, decout/cs_full.cs, full/dte.cs, full/sow.cs):
    ///  (1) GRA MapEvent.LootDefeatedPartyItems (cs 112342, wolane z CalculateAndCommitMapEventResults 112171): kazda sztuka ItemRoster pokonanej partii
    ///      (!NotMerchandise, nie quest, nie sztandar - towary, jedzenie, KONIE taboru) idzie do RosterToReceiveLootItems zwyciezcy i schodzi z taboru.
    ///      Prefiks First: partie z wodzem wyjete z listy defeatedParties (jak CapPre z CaptureDefeatedPartyMembers) - z ich taboru nic nie schodzi.
    ///  (2) DTE EveryoneCampaignBehavior.OnMapEventEnded (dte 3730) -> DistributeLootRandomly -> GetAllLootItems (dte 3970): KOPIA zbrojowni
    ///      (PartyArmories) kazdej pokonanej partii do zwyciezcow; nasze HorseFate.LootMove (postfiks Last, przy LootIsTransfer TAK) zdejmuje dopisane
    ///      u pokonanych. Prefiks (priorytet 1000 - przed HorseFate.DtePrefix i ArmedStrength.HoldPrefix, oba First = 800): zbrojownie partii z wodzem
    ///      wyjete z PartyArmories na czas okna, finalizer oddaje (to, co ktos dopisal w oknie, doliczone). DTE ich nie widzi (SanitizePartyArmory null),
    ///      HorseFate.LootSnap / LootMove / pomiar P2 tez nie (ArmOf null) - przy LootIsTransfer NIE kopia tez nie powstaje.
    ///  (3) Spoils of War (RealisticLoot.Models.BaggageTrainModel, sow 2260-2411): migawka taboru wroga (jedzenie, konie, towary, zwierzeta + PartyTradeGold)
    ///      z poczatku bitwy (LootCollectionBehavior._baggageSnapshot), "Search baggage" robi z niej lup gracza (kopia - z partii nic nie schodzi). Przy (1):
    ///      wklad partii z wodzem odjety z migawki; zapasowa migawka przy koncu bitwy (CaptureSnapshot, sow 4738) - prefiks wyjmuje te partie.
    /// Nie ruszane: lup cial (LootDefeatedPartyCasualties, DTE z poleglych) - ranni i polegli; jency (LootDefeatedPartyPrisoners, B7); zloto (gra
    /// CalculatePlunderedAndLostGoldAmounts); statki (LootDefeatedPartyShips, tylko bitwa morska - 458 to pole); rozliczenie misji DTE (sprzet wystawionych).
    /// 458-t-p (B1): w lupie przedmiotow gry DTE ItemRosterForPlayerLootSharePatch (OnCollectLootItems) bierze graczowi udzial ze zbrojowni pokonanych -
    /// zbrojownie partii z wodzem schowane takze na czas LootDefeatedPartyItems (finalizer oddaje).
    /// Zbior partii zyje od lupu przedmiotow do konca okna DTE tej bitwy (porownanie MapEvent), czyszczony przy nowej misji / Reset.
    /// </summary>
    internal static partial class CommanderWithdraw458Map
    {
        private const string PT = "Odwrot wodza (458) mapa: ";

        private sealed class Tabor
        {
            public MapEvent Me;
            public readonly Dictionary<PartyBase, int[]> Parties = new Dictionary<PartyBase, int[]>();   // [0] przedmioty przed, [1] konie przed, [2] przedmioty po, [3] konie po, [4] po policzone
            public readonly List<string> Dropped = new List<string>();
            public long DteHid, DteBack, DteAdded;
            public int DteParties;
            public long LootHid, LootBack, LootAdded;   // 458-t-p (B1): zbrojownia schowana na czas lupu przedmiotow gry (udzial gracza DTE w OnCollectLootItems)
            public int LootParties;
            public bool DteSeen;
            public int SowItems, SowGold;
            public string SowNote = "";
            public bool Logged;
        }

        private static Tabor _tab;
        private static List<KeyValuePair<MBGUID, Dictionary<ItemObject, int>>> _tabHidden;
        private static List<KeyValuePair<MBGUID, Dictionary<ItemObject, int>>> _tabLootHidden;   // 458-t-p (B1)
        private static Harmony _hT;
        private static bool _tabApplied;
        private static string _hookTabLoot = "nie wpieta", _hookTabDte = "nie wpieta", _hookTabSow = "nie wpieta";
        private static Type _sowColl;
        private static FieldInfo _sowSnapF;
        private static MethodInfo _sowIsBag;

        /// <summary>Start sesji (ArmouryBehavior.OnSessionLaunched, raz na proces): latki lupu przedmiotow gry, okna DTE i migawki taboru Spoils of War.
        /// Bez zbioru partii z wodzem kazda wychodzi przy pierwszym porownaniu.</summary>
        internal static void TaborSessionStart()
        {
            if (!_tabApplied)
            {
                _tabApplied = true;
                _hT = new Harmony("com.jeff.armoury.cw458tabor");
                try
                {
                    var m = AccessTools.DeclaredMethod(typeof(MapEvent), "LootDefeatedPartyItems");
                    if (m == null) _hookTabLoot = "BRAK MapEvent.LootDefeatedPartyItems";
                    else
                    {
                        _hT.Patch(m, prefix: new HarmonyMethod(typeof(CommanderWithdraw458Map), nameof(TabLootPre)) { priority = Priority.First },
                                  postfix: new HarmonyMethod(typeof(CommanderWithdraw458Map), nameof(TabLootPost)) { priority = Priority.Last },
                                  finalizer: new HarmonyMethod(typeof(CommanderWithdraw458Map), nameof(TabLootFin)));
                        _hookTabLoot = "wpieta";
                    }
                }
                catch (Exception e) { _hookTabLoot = "blad " + e.GetType().Name; Log.Error("CommanderWithdraw458Map.TaborSessionStart (lup przedmiotow)", e); }
                try
                {
                    var t = AccessTools.TypeByName("DynamicTroopEquipmentReupload.EveryoneCampaignBehavior");
                    var m = t != null ? AccessTools.DeclaredMethod(t, "OnMapEventEnded", new[] { typeof(MapEvent) }) : null;
                    if (m == null) _hookTabDte = "BRAK (DTE EveryoneCampaignBehavior.OnMapEventEnded)";
                    else
                    {
                        _hT.Patch(m, prefix: new HarmonyMethod(typeof(CommanderWithdraw458Map), nameof(TabDtePre)) { priority = 1000 },
                                  finalizer: new HarmonyMethod(typeof(CommanderWithdraw458Map), nameof(TabDteFin)));
                        _hookTabDte = "wpieta";
                    }
                }
                catch (Exception e) { _hookTabDte = "blad " + e.GetType().Name; Log.Error("CommanderWithdraw458Map.TaborSessionStart (DTE)", e); }
                try
                {
                    var bag = AccessTools.TypeByName("RealisticLoot.Models.BaggageTrainModel");
                    _sowColl = AccessTools.TypeByName("RealisticLoot.Behaviors.LootCollectionBehavior");
                    _sowSnapF = _sowColl != null ? AccessTools.DeclaredField(_sowColl, "_baggageSnapshot") : null;
                    _sowIsBag = bag != null ? AccessTools.DeclaredMethod(bag, "IsBaggageItem", new[] { typeof(ItemObject) }) : null;
                    var cap = bag != null ? AccessTools.DeclaredMethod(bag, "CaptureSnapshot", new[] { typeof(IEnumerable<MapEventParty>) }) : null;
                    if (bag == null) _hookTabSow = "Spoils of War nieobecny";
                    else if (cap == null || _sowSnapF == null || _sowIsBag == null)
                        _hookTabSow = "BRAK (" + (cap == null ? "CaptureSnapshot " : "") + (_sowSnapF == null ? "_baggageSnapshot " : "") + (_sowIsBag == null ? "IsBaggageItem" : "") + ")";
                    else
                    {
                        _hT.Patch(cap, prefix: new HarmonyMethod(typeof(CommanderWithdraw458Map), nameof(TabSowCapPre)) { priority = Priority.First });
                        _hookTabSow = "wpieta";
                    }
                }
                catch (Exception e) { _hookTabSow = "blad " + e.GetType().Name; Log.Error("CommanderWithdraw458Map.TaborSessionStart (Spoils of War)", e); }
            }
            try
            {
                Log.Info("Odwrot wodza (458-t): tabor partii z wodzem, ktory uszedl - lup przedmiotow gry " + _hookTabLoot + " (prefiks MapEvent.LootDefeatedPartyItems, First, zbrojownia DTE schowana na czas lupu - udzial gracza DTE w OnCollectLootItems), okno zbrojowni DTE "
                         + _hookTabDte + " (prefiks 1000 + finalizer EveryoneCampaignBehavior.OnMapEventEnded), migawka taboru Spoils of War " + _hookTabSow
                         + "; dziala tylko po misji z odwrotem wodza przy kluczu Commander Withdraws Under Rearguard.");
            }
            catch { }
        }

        /// <summary>Prefiks LootDefeatedPartyItems(winnerParties, defeatedParties): partie z wodzem (te same warunki co CapPre: WithCommander i zgodne liczniki)
        /// wyjete z listy - z ich taboru nic nie idzie do zwyciezcy. Liste podmieniamy dopiero na koncu (potkniecie = lista gry bez zmian).</summary>
        public static void TabLootPre(MapEvent __instance, ref MBReadOnlyList<MapEventParty> __1)
        {
            var p = _p;
            if (p == null || !ReferenceEquals(p.Me, __instance)) return;
            try
            {
                var cfg = Settings.Current;
                if (cfg == null || !cfg.CommanderWithdrawsUnderRearguard) return;
                if (__instance.DefeatedSide != p.Side || __1 == null) return;
                Tabor t = null;
                var rest = new MBList<MapEventParty>();
                foreach (var mep in __1)
                {
                    var pb = mep != null ? mep.Party : null;
                    if (pb != null && pb.MobileParty != null && !pb.MobileParty.IsMainParty && !TabHas(t, pb) && WithCommander(p, pb) && GameCheck(p, mep) == null)
                    {
                        if (t == null) t = new Tabor { Me = __instance };
                        int it, hs;
                        CountLoot(pb, out it, out hs);
                        t.Parties[pb] = new int[] { it, hs, 0, 0, 0 };
                    }
                    else rest.Add(mep);
                }
                if (t == null) return;
                _tab = t;
                __1 = rest;
                // 458-t-p (B1): DTE ItemRosterForPlayerLootSharePatch (prefiks CampaignEventDispatcher.OnCollectLootItems, wolany z wnetrza tej metody,
                // dte 7059-7125) bierze graczowi udzial ze zbrojowni KAZDEJ partii z PartiesOnSide(DefeatedSide) - pelnej listy, nie naszej __1.
                // Zbrojownie partii z wodzem schowane na czas lupu, TabLootFin oddaje (finalizer - takze przy wyjatku).
                TabHide(t, ref _tabLootHidden, ref t.LootHid, ref t.LootParties, "Tabor (zbrojownia na czas lupu)");
                SowTrim(t);
            }
            catch (Exception e) { Stumble("Tabor (lup przedmiotow)", e); }
        }

        /// <summary>Finalizer LootDefeatedPartyItems (458-t-p, B1): zbrojownie DTE partii z wodzem oddane po lupie przedmiotow. Wyjatek gry idzie dalej.</summary>
        public static Exception TabLootFin(Exception __exception)
        {
            if (_tabLootHidden != null)
            {
                var t = _tab;
                long back = 0, added = 0;
                TabRestoreList(ref _tabLootHidden, ref back, ref added);
                if (t != null) { t.LootBack += back; t.LootAdded += added; }
            }
            return __exception;
        }

        private static bool TabHas(Tabor t, PartyBase pb) { return t != null && t.Parties.ContainsKey(pb); }

        /// <summary>Postfiks LootDefeatedPartyItems: kontrola - tabor partii z wodzem po lupie (ma byc jak przed).</summary>
        public static void TabLootPost(MapEvent __instance)
        {
            var t = _tab;
            if (t == null || !ReferenceEquals(t.Me, __instance)) return;
            try
            {
                foreach (var kv in t.Parties)
                {
                    int it, hs;
                    CountLoot(kv.Key, out it, out hs);
                    kv.Value[2] = it; kv.Value[3] = hs; kv.Value[4] = 1;
                }
            }
            catch (Exception e) { Stumble("Tabor (kontrola po lupie)", e); }
        }

        /// <summary>Sztuki taboru, ktore gra lupi (ten sam warunek co LootDefeatedPartyItems): konie (IsMountable) osobno.</summary>
        private static void CountLoot(PartyBase pb, out int items, out int horses)
        {
            items = horses = 0;
            var r = pb != null ? pb.ItemRoster : null;
            if (r == null) return;
            for (int i = 0; i < r.Count; i++)
            {
                var el = r.GetElementCopyAtIndex(i);
                var it = el.EquipmentElement.Item;
                if (it == null || el.Amount <= 0 || it.NotMerchandise || el.EquipmentElement.IsQuestItem || it.IsBannerItem) continue;
                if (it.IsMountable) horses += el.Amount; else items += el.Amount;
            }
        }

        /// <summary>CapPre: partia z wodzem oddana grze (liczniki niezgodne / potkniecie przed zmianami) - zbrojownia DTE i migawka Spoils jak w grze
        /// (tabor przedmiotow juz zostal przy partii - lup przedmiotow gry minal). Tylko wpis do linii.</summary>
        private static void TaborDrop(PartyBase pb)
        {
            var t = _tab;
            if (t == null || pb == null || !t.Parties.ContainsKey(pb)) return;
            t.Parties.Remove(pb);
            t.Dropped.Add(Name(pb));
        }

        /// <summary>Spoils of War: wklad partii z wodzem (sztuki IsBaggageItem i PartyTradeGold) odjety z migawki taboru bitwy - lup "Search baggage" bez nich.
        /// 458-t-p (B4): odejmowany dzisiejszy ladunek (po bitwie) od migawki ze startu, wedlug rodzaju na cala strone - jesli partia z wodzem ma teraz wiecej
        /// sztuk danego rodzaju niz na starcie, moze odjac wklad innych partii (nic nie powstaje z niczego); flagi migawki (IsLordParty, IsCaravan) zostaja.
        /// Dziala tylko przy wylaczonym prawie pola bitwy (BattlefieldLaw gasi tabor Spoils).</summary>
        private static void SowTrim(Tabor t)
        {
            try
            {
                if (_hookTabSow != "wpieta") { t.SowNote = _hookTabSow; return; }
                object coll = null;
                foreach (var b in Campaign.Current.GetCampaignBehaviors<CampaignBehaviorBase>())
                    if (b != null && _sowColl.IsInstanceOfType(b)) { coll = b; break; }
                var snap = coll != null ? _sowSnapF.GetValue(coll) : null;
                if (snap == null)
                {
                    // 458-t-p (B3): przy prawie pola bitwy BattlefieldLaw.NoBaggageTrain gasi EnableBaggageTrain - migawki nie ma ani na starcie, ani zapasowej
                    var cfg = Settings.Current;
                    t.SowNote = cfg != null && cfg.BattlefieldLawEnabled ? "tabor Spoils wylaczony (BattlefieldLaw) - nic do odjecia"
                                                                         : "migawki brak (zapasowa przy koncu bitwy - bez tych partii)";
                    return;
                }
                var itemsP = AccessTools.Property(snap.GetType(), "Items");
                var goldP = AccessTools.Property(snap.GetType(), "TradeGold");
                var list = itemsP != null ? itemsP.GetValue(snap, null) as List<(ItemObject item, int count)> : null;
                if (list == null || goldP == null) { t.SowNote = "migawka nieczytelna"; return; }
                var want = new Dictionary<ItemObject, int>();
                int gold = 0;
                foreach (var kv in t.Parties)
                {
                    var mp = kv.Key.MobileParty;
                    if (mp != null) gold += Math.Max(0, mp.PartyTradeGold);
                    var r = kv.Key.ItemRoster;
                    if (r == null) continue;
                    for (int i = 0; i < r.Count; i++)
                    {
                        var el = r.GetElementCopyAtIndex(i);
                        var it = el.EquipmentElement.Item;
                        if (it == null || el.Amount <= 0 || !(bool)_sowIsBag.Invoke(null, new object[] { it })) continue;
                        int n; want.TryGetValue(it, out n); want[it] = n + el.Amount;
                    }
                }
                int cut = 0;
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    var e = list[i];
                    int n;
                    if (e.item == null || !want.TryGetValue(e.item, out n) || n <= 0) continue;
                    int k = Math.Min(n, e.count);
                    want[e.item] = n - k;
                    cut += k;
                    if (e.count - k > 0) list[i] = (e.item, e.count - k); else list.RemoveAt(i);
                }
                int g0 = (int)goldP.GetValue(snap, null);
                int gc = Math.Min(Math.Max(0, g0), gold);
                if (gc > 0) goldP.SetValue(snap, g0 - gc, null);
                t.SowItems = cut; t.SowGold = gc;
                t.SowNote = "z migawki taboru odjete " + cut + " szt. i " + gc + " zlota kupieckiego";
            }
            catch (Exception e) { t.SowNote = "potkniecie"; Stumble("Tabor (Spoils of War)", e); }
        }

        /// <summary>Prefiks BaggageTrainModel.CaptureSnapshot (zapasowa migawka Spoils przy koncu bitwy): bez partii z wodzem tej bitwy.</summary>
        public static void TabSowCapPre(ref IEnumerable<MapEventParty> __0)
        {
            var t = _tab;
            if (t == null || __0 == null || t.Parties.Count == 0) return;
            try
            {
                var keep = new List<MapEventParty>();
                int cut = 0;
                foreach (var mep in __0)
                {
                    var pb = mep != null ? mep.Party : null;
                    if (pb != null && t.Parties.ContainsKey(pb) && ReferenceEquals(pb.MapEvent, t.Me)) { cut++; continue; }
                    keep.Add(mep);
                }
                if (cut == 0) return;
                __0 = keep;
                t.SowNote = "zapasowa migawka przy koncu bitwy bez partii z wodzem (" + cut + ")";
            }
            catch (Exception e) { Stumble("Tabor (Spoils of War migawka)", e); }
        }

        /// <summary>Prefiks DTE EveryoneCampaignBehavior.OnMapEventEnded (priorytet 1000, przed HorseFate i ArmedStrength): zbrojownie partii z wodzem
        /// wyjete z PartyArmories na czas okna - DTE ich nie kopiuje zwyciezcom, HorseFate.LootMove z nich nie zdejmuje.</summary>
        public static void TabDtePre(MapEvent __0)
        {
            var t = _tab;
            if (t == null || __0 == null || !ReferenceEquals(t.Me, __0)) return;
            t.DteSeen = true;
            TabHide(t, ref _tabHidden, ref t.DteHid, ref t.DteParties, "Tabor (DTE schowanie)");
        }

        /// <summary>Zbrojownie DTE partii z wodzem wyjete z PartyArmories do listy (oddaje TabRestoreList). Potkniecie = od razu oddane.
        /// Lista z poprzedniego okna (nie powinna zostac - finalizery zawsze oddaja) najpierw oddana.</summary>
        private static void TabHide(Tabor t, ref List<KeyValuePair<MBGUID, Dictionary<ItemObject, int>>> slot, ref long hidPieces, ref int hidParties, string what)
        {
            long b0 = 0, a0 = 0;
            if (slot != null) TabRestoreList(ref slot, ref b0, ref a0);
            try
            {
                var all = AiGear.Armories();
                if (all == null) return;
                var hid = new List<KeyValuePair<MBGUID, Dictionary<ItemObject, int>>>();
                slot = hid;
                foreach (var kv in t.Parties)
                {
                    var mp = kv.Key.MobileParty;
                    Dictionary<ItemObject, int> a;
                    if (mp == null || !all.TryGetValue(mp.Id, out a) || a == null) continue;
                    all.Remove(mp.Id);
                    hid.Add(new KeyValuePair<MBGUID, Dictionary<ItemObject, int>>(mp.Id, a));
                    foreach (var x in a) if (x.Value > 0) hidPieces += x.Value;
                    hidParties++;
                }
            }
            catch (Exception e) { Stumble(what, e); TabRestoreList(ref slot, ref b0, ref a0); }
        }

        /// <summary>Finalizer okna DTE (po wszystkich postfiksach, takze HorseFate.LootMove): zbrojownie oddane, linia taboru. Wyjatek DTE idzie dalej.</summary>
        public static Exception TabDteFin(MapEvent __0, Exception __exception)
        {
            TabRestore();
            var t = _tab;
            if (t != null && __0 != null && ReferenceEquals(t.Me, __0)) TabLine("okno DTE");
            return __exception;
        }

        private static void TabRestore()
        {
            var t = _tab;
            long back = 0, added = 0;
            TabRestoreList(ref _tabHidden, ref back, ref added);
            if (t != null) { t.DteBack += back; t.DteAdded += added; }
        }

        /// <summary>Oddanie schowanych zbrojowni (to, co ktos dopisal partii w oknie, doliczone - nic nie ginie).</summary>
        private static void TabRestoreList(ref List<KeyValuePair<MBGUID, Dictionary<ItemObject, int>>> slot, ref long back, ref long added)
        {
            var hid = slot;
            slot = null;
            if (hid == null) return;
            try
            {
                var all = AiGear.Armories();
                if (all == null) return;
                foreach (var kv in hid)
                {
                    Dictionary<ItemObject, int> now;
                    if (all.TryGetValue(kv.Key, out now) && now != null && !ReferenceEquals(now, kv.Value))
                    {
                        // ktos dopisal tej partii w oknie (nie powinien - jest pokonana) - doliczone, nic nie ginie
                        foreach (var x in now)
                        {
                            if (x.Key == null || x.Value <= 0) continue;
                            int c; kv.Value.TryGetValue(x.Key, out c);
                            kv.Value[x.Key] = c + x.Value;
                            added += x.Value;
                        }
                    }
                    all[kv.Key] = kv.Value;
                    foreach (var x in kv.Value) if (x.Value > 0) back += x.Value;
                }
            }
            catch (Exception e) { Stumble("Tabor (zbrojownia oddanie)", e); }
        }

        /// <summary>Linia "tabor zachowany" - raz na bitwe: z finalizera okna DTE, a bez okna DTE z naszego MapEventEnded.</summary>
        private static void TabLine(string where)
        {
            var t = _tab;
            if (t == null || t.Logged) return;
            t.Logged = true;
            try
            {
                int items = 0, horses = 0, bad = 0, notChecked = 0;
                var names = new List<string>();
                foreach (var kv in t.Parties)
                {
                    var v = kv.Value;
                    items += v[0]; horses += v[1];
                    if (v[4] == 0) notChecked++;
                    else if (v[0] != v[2] || v[1] != v[3]) bad++;
                    names.Add(Name(kv.Key));
                }
                bool dteOk = t.DteBack == t.DteHid + t.DteAdded;
                bool lootOk = t.LootBack == t.LootHid + t.LootAdded;
                var sb = new StringBuilder(256);
                sb.Append(PT).Append("tabor zachowany: partii ").Append(t.Parties.Count).Append(", przedmiotow ").Append(items).Append(", koni ").Append(horses)
                  .Append(", zbrojownia DTE ").Append(t.LootParties > 0 ? t.LootHid : t.DteHid).Append(" szt.");
                if (names.Count > 0) sb.Append(" (").Append(string.Join(", ", names.ToArray())).Append(')');
                sb.Append(" | kontrola: tabor po lupie gry = przed - partii zgodnych ").Append(t.Parties.Count - bad - notChecked).Append(", niezgodnych ").Append(bad)
                  .Append(", niesprawdzonych ").Append(notChecked)
                  .Append(" | zbrojownia DTE na czas lupu gry (udzial gracza DTE): schowana ").Append(t.LootHid).Append(" (partii ").Append(t.LootParties).Append("), oddana ")
                  .Append(t.LootBack).Append(", dopisane w oknie ").Append(t.LootAdded).Append(lootOk ? " OK" : " NIEZGODNE")
                  .Append(" | zbrojownia DTE w oknie OnMapEventEnded: przed lupem ").Append(t.LootHid).Append(", schowana ").Append(t.DteHid).Append(" (partii ").Append(t.DteParties).Append("), oddana ").Append(t.DteBack)
                  .Append(", dopisane w oknie ").Append(t.DteAdded).Append(dteOk ? " OK" : " NIEZGODNE")
                  .Append(t.DteSeen ? "" : " (okna DTE nie bylo: " + _hookTabDte + ")")
                  .Append(" | Spoils of War: ").Append(t.SowNote.Length > 0 ? t.SowNote : _hookTabSow);
                if (t.Dropped.Count > 0) sb.Append(" | oddane grze po lupie przedmiotow (ludzie jak w grze; tabor przedmiotow i udzial gracza DTE ze zbrojowni zostaly, kopia DTE OnMapEventEnded jak w grze): ").Append(string.Join(", ", t.Dropped.ToArray()));
                sb.Append(" | ").Append(where).Append('.');
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Stumble("Tabor (linia)", e); }
        }
    }
}
