using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// m96-wzory (20.10, Jeff ze zrzutem kuzni: "[VI] 200 Pound Goldenheart Longbow - PATTERN KNOWN" przy Smithing 30, dzien kilka nowej kampanii).
    ///
    /// PRZYCZYNA (log Jeffa Armoury-2026-10-20_13-28-19.log: linia 495 "BOWS 19/65 | t1 19/19 | t6 0/12", linia 6231 w tej samej sesji
    /// "BOWS 19/65 | t1 15/15 | t6 4/18"): ItemObject.Tier liczy sie na zywo (ItemValueModel.CalculateTier; RBM: luk z MissileSpeed,
    /// strzaly z obrazen i wagi). RangedLore.Seed rozdawal wzory tieru 1 przy pierwszym odczycie ksiegi - w ArmouryBehavior.OnSessionLaunched
    /// (ReportLedger), a ROT.CampaignBehaviors.ROTRBMCompatibility.OnSessionLaunched -> ModifyBowsAndArrows przepisuje luki i strzaly
    /// PO nas (Armoury-2026-10-20_10-24-16.log: rozdanie linia 525, ROT-RBM linia 539, ta sama sekunda). Goldenheart, Giant Bow,
    /// Ravens' Teeth Longbow i Ravens' Teeth Arrows weszly do ksiegi jako tier 1 i od tej samej sekundy sa tierem 6.
    ///
    /// NAPRAWA (klucz LoreSeedAfterFinalStats; wylaczony = stara kolejnosc bez naprawy zapisu):
    ///  - rozdanie (Seed), sanacja (LorePurgeOnce) i linie ksiegi dopiero w OnAfterSessionLaunched - po OnSessionLaunched wszystkich modow;
    ///  - JEDNORAZOWA naprawa zapisu (znacznik RepairMark w liscie Known - format "id|id;pkt" bez zmian, build 95 znacznik pomija,
    ///    bo GetObject zwraca null): z bloku rozdania (miedzy "#seeded2" a "__seed_ammo_v1" - tam stoi WYLACZNIE to, co dal Seed w kampanii
    ///    od 02.09; wzory zdobyte potem stoja za "__seed_ammo_v1") zdejmujemy wzor, ktory jest dzis tierem 2+, a przy wczytaniu byl tierem 1
    ///    (migawka sprzed ROT) albo jest na liscie ROT-RBM; w kazdej sesji dopisujemy brakujace wzory tieru 1 po ostatecznych statystykach;
    ///  - nowa kampania w tym samym procesie gry: ksiega wzorow, punkty nauki, UniqueLore i Legends od zera (statyczne listy przeciekaly);
    ///  - klik Craft bez kucia (wzor nieznany, odmowa) nie uczy (FletchForge.BkCraftPostfix czyta __runOriginal).
    /// PO RECENZJACH (20.10): naprawa i rozdanie PRZED sanacja (blednie rozdany wzor to nie przeciek); migawka i licznik zmian - tier GRY
    /// (Recipes.Grade); kusze wedlug sily (XbowPatternTierByPower, PatternGate.LoreTier) - osobna jednorazowa naprawa ze znacznikiem XbowMark,
    /// gdy XbowPrice obowiazuje w tej sesji; w zapisach sprzed 02.09 takze ciag amunicji tieru 1 tuz za "__seed_ammo_v1" (migracja amunicji).
    /// Wszystko na watku glownym, raz na sesje, poza gorace sciezki.
    /// </summary>
    internal static class LoreSeedFix
    {
        internal const string RepairMark = "#m96seedfix1";
        internal const string XbowMark = "#m96xbowfix1";   // po recenzjach: kusze wedlug sily zdjete z rozdania (XbowPatternTierByPower) - osobno, bo zalezy od XbowPrice

        // ROT.CampaignBehaviors.ROTRBMCompatibility.ModifyBowsAndArrows (dekompilacja ROT.dll 8.1.8) - przedmioty, ktorym ROT przy RBM
        // przepisuje statystyki w OnSessionLaunched; zapas na wypadek, gdyby migawka przy wczytaniu byla juz po zmianie
        private static readonly string[] RotRbmIds =
        {
            "goldenheart_longbow", "giant_bow", "ravens_teeth_longbow", "weirwood_bow", "dragonbone_bow", "ravens_teeth_arrows", "giant_arrows"
        };

        internal static bool On { get { var s = Settings.Current; return s != null && s.LoreSeedAfterFinalStats; } }

        private static Campaign _finalFor;

        /// <summary>Seed wolno rozdawac: klucz wylaczony (jak dotad) albo statystyki tej kampanii sa juz ostateczne.</summary>
        internal static bool SeedAllowed
        {
            get
            {
                if (!On) return true;
                var c = Campaign.Current;
                return c != null && ReferenceEquals(c, _finalFor);
            }
        }

        // migawka tierow sprzed OnSessionLaunched (wczytanie: SyncData; nowa kampania: OnNewGameCreated)
        private static readonly Dictionary<ItemObject, int> _snap = new Dictionary<ItemObject, int>();
        private static bool _snapFresh;
        private static string _snapWhere = "-";
        private static int _stumbles, _refusedClicks;

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            if (_stumbles <= 5) Log.Error(where, e);
        }

        /// <summary>Tiery wyrobow szkol nauki przed OnSessionLaunched - raz na sesje (pierwsze wywolanie wygrywa).</summary>
        internal static void Snapshot(string where)
        {
            try
            {
                if (_snapFresh) return;
                _snap.Clear();
                foreach (var item in MBObjectManager.Instance.GetObjectTypeList<ItemObject>())
                {
                    if (item == null || RangedLore.SchoolOf(item) == RangedLore.SchoolNone) continue;
                    _snap[item] = Recipes.Grade(item);   // tier GRY (statystyki z XML) - tier wzoru kuszy zalezy od XbowPrice, liczonego dopiero w OnSessionLaunched
                }
                _snapFresh = true;
                _snapWhere = where;
            }
            catch (Exception e) { Stumble("LoreSeedFix.Snapshot", e); }
        }

        /// <summary>Nowa kampania: statyczne ksiegi z poprzedniej gry w tym procesie - od zera (OnNewGameCreated; zapisu nie ma, SyncData nie biegnie).</summary>
        internal static void OnNewGame()
        {
            try
            {
                if (!On) return;
                int k = 0, u = 0, l = 0;
                foreach (var id in RangedLore.Known) if (!string.IsNullOrEmpty(id) && id[0] != '#' && !id.StartsWith("__", StringComparison.Ordinal)) k++;
                RangedLore.Known.Clear();
                for (int i = 0; i < RangedLore.Research.Length; i++) RangedLore.Research[i] = 0f;
                if (ArmouryBehavior.UniqueLore != null) { u = ArmouryBehavior.UniqueLore.Count; ArmouryBehavior.UniqueLore.Clear(); }
                l = ArmouryBehavior.Legends.Count; ArmouryBehavior.Legends.Clear();
                Snapshot("nowa kampania");
                Log.Info("Wiedza (m96-wzory): nowa kampania - ksiega wzorow, punkty nauki, unikaty i legendy od zera (z poprzedniej gry w tym procesie: wzorow "
                         + k + ", unikatow " + u + ", legend " + l + "); rozdanie tieru 1 po ostatecznych statystykach (start sesji).");
            }
            catch (Exception e) { Stumble("LoreSeedFix.OnNewGame", e); }
        }

        /// <summary>FletchForge.BkCraftPostfix: klik Craft, po ktorym BK nic nie kul (prefiks odmowil) - bez nauki.</summary>
        internal static void NoteRefusedClick(ItemObject item)
        {
            _refusedClicks++;
            if (_refusedClicks <= 10)
                Log.Info("Wiedza (m96-wzory): klik Craft bez kucia (wzor nieznany albo odmowa) - bez nauki i bez okna wyniku: "
                         + (item != null ? item.StringId : "-") + " | od startu sesji " + _refusedClicks + ".");
        }

        /// <summary>OnAfterSessionLaunched (ArmouryBehavior): statystyki ostateczne, sanacja, naprawa zapisu albo rozdanie, linie ksiegi.</summary>
        internal static void AfterSessionLaunched(Action purge)
        {
            try
            {
                _finalFor = Campaign.Current;
                _refusedClicks = 0;
                PatternGate.ResetCounters();
                bool fresh = _snapFresh;
                _snapFresh = false;
                if (!On)
                {
                    Log.Info("Wiedza (m96-wzory): wylaczone (LoreSeedAfterFinalStats NIE) - rozdanie i ksiega jak dotad (OnSessionLaunched) | " + PatternGate.Keys() + ".");
                    return;
                }

                // 1. ktore tiery GRY zmienily sie od migawki (ROT-RBM i inne zmiany statystyk w OnSessionLaunched)
                int changed = 0, up = 0;
                var upIds = new List<string>();
                if (fresh)
                {
                    foreach (var kv in _snap)
                    {
                        int now = Recipes.Grade(kv.Key);
                        if (now == kv.Value) continue;
                        changed++;
                        if (kv.Value == 1 && now > 1) { up++; if (upIds.Count < 12) upIds.Add(kv.Key.StringId + " t1->t" + now); }
                    }
                }
                // 1b. (po recenzjach) kusze, ktorych tier wzoru (sila, XbowPatternTierByPower) jest wyzszy niz tier gry
                bool xbowReady = PatternGate.XbowTierOn && XbowPrice.Active;
                string xbowLine = XbowLine(xbowReady);

                // 2. naprawa zapisu albo rozdanie - PRZED sanacja (po recenzjach: blednie rozdany wzor to nie przeciek miedzy kampaniami)
                var K = RangedLore.Known;
                string book;
                if (!K.Contains(RangedLore.SeedMark))
                {
                    int n0 = RealCount(K);
                    RangedLore.Seed();
                    if (!K.Contains(RepairMark)) K.Add(RepairMark);   // swieze rozdanie po ostatecznych statystykach - nic do naprawy
                    if (xbowReady && !K.Contains(XbowMark)) K.Add(XbowMark);
                    book = "nowa ksiega - rozdanie po ostatecznych statystykach (" + Math.Max(0, RealCount(K) - n0) + " wzorow tieru 1), kusze wedlug sily: "
                           + (xbowReady ? "w rozdaniu" : "nieczynne w tej sesji");
                }
                else
                {
                    if (!K.Contains(RepairMark))
                    {
                        var removed = new List<string>();
                        Repair(K, fresh, removed);
                        K.Add(RepairMark);
                        book = "naprawa zapisu: zdjete " + removed.Count + " [" + string.Join(", ", removed.ToArray()) + "]";
                    }
                    else book = "zapis juz naprawiony (znacznik " + RepairMark + ")";
                    if (!xbowReady) book += ", kusze wedlug sily: nieczynne w tej sesji";
                    else if (K.Contains(XbowMark)) book += ", kusze wedlug sily: juz naprawione (znacznik " + XbowMark + ")";
                    else
                    {
                        var rx = new List<string>();
                        RepairXbows(K, rx);
                        K.Add(XbowMark);
                        book += ", kusze wedlug sily: zdjete " + rx.Count + " [" + string.Join(", ", rx.ToArray()) + "]";
                    }
                    RangedLore.Seed();   // migracja amunicji starych zapisow, jak dotad
                }

                // 3. sanacja przecieku (stara regula, teraz po ostatecznych statystykach i po naprawie); reset do tieru 1 gubi znaczniki - z powrotem
                if (purge != null) { try { purge(); } catch (Exception e) { Stumble("LoreSeedFix.purge", e); } }
                if (K.Contains(RangedLore.SeedMark))
                {
                    if (!K.Contains(RepairMark)) K.Add(RepairMark);
                    if (xbowReady && !K.Contains(XbowMark)) K.Add(XbowMark);
                }

                // kazda sesja: brakujace wzory tieru 1 po ostatecznych statystykach (wzor, ktory stal sie wzorem do nauki dopiero po rozdaniu, np.
                // przy ForgeUniquesByStudy NIE unikat ROT noszony przez wiecej niz 3 postaci - UniqueSpoils nie daje mu NotMerchandise)
                var added = TopUpT1(K);
                book += ", dopisane tieru 1 " + added.Count + " [" + string.Join(", ", (added.Count > 12 ? added.GetRange(0, 12) : added).ToArray())
                        + (added.Count > 12 ? ", ..." : "") + "]";

                Log.Info("Wiedza (m96-wzory): start sesji | statystyki ostateczne TAK | migawka tierow " + (fresh ? _snapWhere : "brak (bez wczytania w tej sesji)")
                         + " | tiery zmienione od migawki " + changed + " (t1->wyzej " + up + (upIds.Count > 0 ? ": " + string.Join(", ", upIds.ToArray()) : "") + ")"
                         + " | " + xbowLine + " | " + book + " | " + PatternGate.Keys() + " | potkniecia " + _stumbles + ".");

                // 4. linie ksiegi - juz po zmianach statystyk
                RangedLore.ReportLedger(false);
            }
            catch (Exception e) { Stumble("LoreSeedFix.AfterSessionLaunched", e); }
        }

        /// <summary>Jednorazowa naprawa ksiegi z zapisu (kampanie zaczete przed m96). Patrz opis klasy. Kusze - osobno (RepairXbows).</summary>
        private static void Repair(List<string> K, bool fresh, List<string> removed)
        {
            int iSeed = K.IndexOf(RangedLore.SeedMark);
            int iAmmo = K.IndexOf(RangedLore.SeedMarkAmmo);
            // bez znacznika amunicji za rozdaniem nie wiadomo, gdzie konczy sie blok rozdania - nic nie zdejmujemy
            if (iSeed >= 0 && iAmmo > iSeed)
            {
                for (int i = iAmmo - 1; i > iSeed; i--) TryStrike(K, i, fresh, removed);
                // po recenzjach (luka zapisow sprzed 02.09): migracja amunicji dopisala kolczany i sajdaki tieru 1 ZA znacznikiem "__seed_ammo_v1"
                // (Seed: znacznik, potem amunicja) - ciagly ciag amunicji tuz za znacznikiem to tez rozdanie; w zapisie od 02.09 ten ciag jest pusty
                // albo to amunicja zdobyta pozniej - a zdobyta nie byla tierem 1 w migawce (tier 1 rozdawano w calosci), wiec warunek jej nie zdejmie
                int j = iAmmo + 1;
                while (j < K.Count)
                {
                    ItemObject it = null;
                    try { var id = K[j]; if (!string.IsNullOrEmpty(id) && id[0] != '#' && !id.StartsWith("__", StringComparison.Ordinal)) it = MBObjectManager.Instance.GetObject<ItemObject>(id); }
                    catch { }
                    if (it == null || (it.ItemType != ItemObject.ItemTypeEnum.Arrows && it.ItemType != ItemObject.ItemTypeEnum.Bolts)) break;
                    if (!TryStrike(K, j, fresh, removed)) j++;
                }
            }
        }

        /// <summary>Zdejmuje K[i], gdy to wzor rozdany jako tier 1, ktory po ostatecznych statystykach jest tierem 2+. true = zdjety.</summary>
        private static bool TryStrike(List<string> K, int i, bool fresh, List<string> removed)
        {
            try
            {
                var id = K[i];
                if (string.IsNullOrEmpty(id) || id[0] == '#' || id.StartsWith("__", StringComparison.Ordinal)) return false;
                var it = MBObjectManager.Instance.GetObject<ItemObject>(id);
                if (it == null || it.ItemType == ItemObject.ItemTypeEnum.Crossbow) return false;   // kusze - RepairXbows (osobny znacznik)
                if (ArmouryBehavior.UniqueLore != null && ArmouryBehavior.UniqueLore.Contains(id)) return false;   // opanowany unikat - zdobyty
                int now = RangedLore.TierOf(it);
                if (now <= 1) return false;
                int pre;
                bool wasT1 = (fresh && _snap.TryGetValue(it, out pre) && pre == 1) || Array.IndexOf(RotRbmIds, id) >= 0;
                if (!wasT1) return false;   // tier 2+ juz przy wczytaniu: wzor zdobyty przed 02.09 w bloku starego zapisu - zostaje
                K.RemoveAt(i);
                removed.Add(id + " t" + now);
                return true;
            }
            catch (Exception e) { Stumble("LoreSeedFix.Repair", e); return false; }
        }

        /// <summary>
        /// Po recenzjach (XbowPatternTierByPower): jednorazowo, gdy przeliczenie kusz (XbowPrice) obowiazuje w tej sesji - zdejmuje kusze tieru GRY 1,
        /// ktorych tier wzoru (sila) jest 2+. Kazda kusza tieru gry 1 byla rozdana przez stary Seed (rozdawal WSZYSTKIE wzory tieru 1), wiec jej wpis
        /// pochodzi z rozdania, gdziekolwiek stoi; uczciwie wylosowanej nie ma (byla juz znana). Znacznik XbowMark - format bez zmian, build 95 go pomija.
        /// </summary>
        private static void RepairXbows(List<string> K, List<string> removed)
        {
            for (int i = K.Count - 1; i >= 0; i--)
            {
                try
                {
                    var id = K[i];
                    if (string.IsNullOrEmpty(id) || id[0] == '#' || id.StartsWith("__", StringComparison.Ordinal)) continue;
                    var it = MBObjectManager.Instance.GetObject<ItemObject>(id);
                    if (it == null || it.ItemType != ItemObject.ItemTypeEnum.Crossbow) continue;
                    if (ArmouryBehavior.UniqueLore != null && ArmouryBehavior.UniqueLore.Contains(id)) continue;
                    if (Recipes.Grade(it) != 1) continue;
                    int lt = RangedLore.TierOf(it);
                    if (lt <= 1) continue;
                    K.RemoveAt(i);
                    removed.Add(id + " t" + lt);
                }
                catch (Exception e) { Stumble("LoreSeedFix.RepairXbows", e); }
            }
        }

        /// <summary>Do linii startu: kusze, ktorych tier wzoru jest wyzszy niz tier gry (raz na sesje).</summary>
        private static string XbowLine(bool ready)
        {
            if (!PatternGate.XbowTierOn) return "kusze wedlug sily NIE (tier gry)";
            if (!ready) return "kusze wedlug sily TAK, ale nieczynne (XbowPrice nie przeliczone w tej sesji albo XbowForgeEquivTier NIE) - tier gry";
            int n = 0, up = 0;
            var ids = new List<string>();
            try
            {
                foreach (var item in MBObjectManager.Instance.GetObjectTypeList<ItemObject>())
                {
                    if (item == null || item.ItemType != ItemObject.ItemTypeEnum.Crossbow) continue;
                    int g = Recipes.Grade(item), lt = RangedLore.TierOf(item);
                    if (lt <= g) continue;
                    n++;
                    if (g == 1) up++;
                    if (ids.Count < 12) ids.Add(item.StringId + " t" + g + "->t" + lt);
                }
            }
            catch (Exception e) { Stumble("LoreSeedFix.XbowLine", e); }
            return "kusze wedlug sily: tier wzoru ponad tier gry " + n + " (z tieru 1 " + up + (ids.Count > 0 ? ": " + string.Join(", ", ids.ToArray()) : "") + ")";
        }

        private static int RealCount(List<string> K)
        {
            int n = 0;
            for (int i = 0; i < K.Count; i++)
            {
                var id = K[i];
                if (!string.IsNullOrEmpty(id) && id[0] != '#' && !id.StartsWith("__", StringComparison.Ordinal)) n++;
            }
            return n;
        }

        /// <summary>Brakujace wzory tieru 1 (do nauki, po ostatecznych statystykach) - raz na sesje; zbior na czas petli zamiast List.Contains.</summary>
        private static List<string> TopUpT1(List<string> K)
        {
            var added = new List<string>();
            try
            {
                var have = new HashSet<string>(K);
                foreach (var item in MBObjectManager.Instance.GetObjectTypeList<ItemObject>())
                {
                    try
                    {
                        if (item == null || item.StringId == null || have.Contains(item.StringId)) continue;
                        if (!RangedLore.Teachable(item) || RangedLore.TierOf(item) != 1) continue;
                        K.Add(item.StringId);
                        have.Add(item.StringId);
                        added.Add(item.StringId);
                    }
                    catch (Exception e) { Stumble("LoreSeedFix.TopUpT1", e); }
                }
            }
            catch (Exception e) { Stumble("LoreSeedFix.TopUpT1", e); }
            return added;
        }
    }
}
