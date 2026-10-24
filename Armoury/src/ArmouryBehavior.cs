using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    public class ArmouryBehavior : CampaignBehaviorBase
    {
        public static ArmouryBehavior Instance;

        // kondycja: "slot|condition|originalModifierId"
        private List<string> _condition = new List<string>();
        private int _hpBeforeBattle = -1;
        private List<string> _projects = new List<string>();
        private Dictionary<string,int> _lootSnapshot = new Dictionary<string,int>();
        private CampaignTime _spoilsWindow = CampaignTime.Zero;
        private CampaignTime _lootFlushDue = CampaignTime.Zero;
        private CampaignTime _shortageShoutDue = CampaignTime.Zero;   // meldunek brakow PO odebraniu lupow
        // legendy wykute raz na zawsze (StringId) - drugiej takiej swiat nie ujrzy
        internal static readonly List<string> Legends = new List<string>();
        // wybrany pomocnik kowala (Jeff 30.08: "wybieram postac, ona sie uczy
        // i mi pomaga"): "" / "auto" = najlepszy z druzyny, "none" = pracuje
        // sam, inaczej StringId bohatera
        internal static string SmithHelperId = "";

        /// <summary>Nauczone wzory unikatow (id itemow) - przetopienie zdobytego
        /// egzemplarza uczy kuznie go odtwarzac (Jeff 30.08).</summary>
        internal static System.Collections.Generic.List<string> UniqueLore =
            new System.Collections.Generic.List<string>();

        private static bool _restitution0831;

        /// <summary>RESTYTUCJA 31.08: czystka sakw zjadla wziete lupy i wlasny
        /// zuzyty sprzet, zanim wycielismy jej prog kondycji. Trzy sztuki znane
        /// z logu wracaja do sakw - raz na kampanie.</summary>
        private static void Restitution0831()
        {
            try
            {
                if (_restitution0831) return;
                _restitution0831 = true;
                int given = 0;
                // 3 wlasne sztuki znane z logu + lupy z 19 Broken Men
                // (Jeff: "12 i potem 7 osob") - reprezentatywny sprzet looterow
                var back = new[] {
                    new[] { "battania_civil_cape", "ripped_cloth_unarmoured" },
                    new[] { "bandit_saddle_steppe", "ripped_cloth" },
                    new[] { "sturgia_sword_1_t2", "rusty_sword" },
                    // Jeff: "oni mieli COMMON ARMOUR, plytowe" - DTE ubral bande
                    // w pospolite plyty ROT (diff 30-70); oddajemy wlasnie te
                    new[] { "common_armor", null }, new[] { "common_armor", null }, new[] { "common_armor", null },
                    new[] { "common_barbute", null }, new[] { "common_barbute", null },
                    new[] { "common_kettle", null }, new[] { "common_spangen", null },
                    new[] { "common_pauldrons", null },
                    new[] { "common_boots", null }, new[] { "common_boots", null },
                    new[] { "common_bracers", null }, new[] { "common_gloves", null }
                };
                var ro = MobileParty.MainParty != null ? MobileParty.MainParty.ItemRoster : null;
                if (ro == null) return;
                foreach (var pair in back)
                {
                    var it = TaleWorlds.ObjectSystem.MBObjectManager.Instance.GetObject<ItemObject>(pair[0]);
                    if (it == null) continue;
                    var mod = pair[1] != null ? TaleWorlds.ObjectSystem.MBObjectManager.Instance.GetObject<ItemModifier>(pair[1]) : null;
                    ro.AddToCounts(new EquipmentElement(it, mod), 1);
                    given++;
                }
                if (given > 0)
                    Log.Player("The quartermaster returns what the purge wrongly burned - " + given + " pieces back in your bags.");
            }
            catch (Exception e) { Log.Error("Restitution0831", e); }
        }

        internal static void LearnUnique(ItemObject it)
        {
            try
            {
                if (it == null || UniqueLore.Contains(it.StringId)) return;
                UniqueLore.Add(it.StringId);
                Log.Player("You have studied the make of " + it.Name + " - the CRAFT bench can now reproduce it" + (PartyWords.NoTroops ? "." : ", and your men may wear it."));   // m110-sam: bez zolnierzy bez "your men"
                Log.Info("UniqueLore: nauczono " + it.StringId + " (razem " + UniqueLore.Count + ").");
            }
            catch (Exception e) { Log.Error("LearnUnique", e); }
        }
        // KSIEGA WKLADOW GRACZA (Jeff 29.08: "w Armoury widze i ruszam TYLKO to,
        // co sam wrzucilem - lupy 60% to wlasnosc wojska"): itemId -> ile sztuk
        // nalezy do gracza; ekran zbrojowni pokazuje wylacznie te sztuki
        private Dictionary<string,int> _playerStock = new Dictionary<string,int>();
        // K1 (przeglad): licznik zmian ksiegi (MenPurse.SettlePlayerBook liczy porzadek tylko po zmianie) - tylko w pamieci
        internal static int StockVersion;

        internal static int StockOf(string id)
        {
            try
            {
                var self = Instance;
                if (self == null || string.IsNullOrEmpty(id)) return 0;
                int v; return self._playerStock.TryGetValue(id, out v) ? v : 0;
            }
            catch { return 0; }
        }

        internal static void StockDeposit(string id, int n)
        {
            try
            {
                var self = Instance;
                if (self == null || string.IsNullOrEmpty(id) || n <= 0) return;
                int v; self._playerStock.TryGetValue(id, out v);
                self._playerStock[id] = v + n;
                StockVersion++;
            }
            catch { }
        }

        // STANY MAGAZYNU PRZEZYWAJA SAVE (Jeff 29.08: "load automatycznie
        // naprawia sprzet wojska!" - DOWOD: DTE zapisuje magazyn jako
        // Dictionary<string,int> = id -> liczba, BEZ modifierow; kazdy load
        // odtwarzal wszystko czyste). My dopisujemy wlasna ksiege stanow:
        // przy zapisie zrzut "itemId|modifierId|ile", przy wczytaniu zbite
        // sztuki wracaja na miejsce czystych.
        private List<string> _armoryWear = new List<string>();
        private bool _wearRestorePending;

        // ZWROT PO MOIM BLEDZIE (29.08, Jeff: "teraz pozera kazda ilosc
        // strzal!"). Przez jeden build kwatermistrz przejmowal na wojsko
        // kazdy wklad, ktorego nie mial czym wymienic - sztuki zostawaly
        // fizycznie w magazynie, ale znikaly z ksiegi gracza. Log podaje
        // dokladne ilosci; oddajemy je raz, po czym flaga gasi zwrot.
        private bool _ammoRefundDone;
        private bool _lorePurged;

        /// <summary>SANACJA WIEDZY (Jeff 01.09: swieza kampania znala 26/26
        /// wzorow lukow T6 - statyczna lista przeciekla ze starej kampanii
        /// w tym samym procesie i sie ZAPISALA). Jednorazowo: mloda kampania
        /// (do 30 dni) ze wzorami T4+ w ksiedze = przeciek; reset do tieru 1.
        /// Starych, uczciwie wygranych kampanii nie tykamy.</summary>
        private void LorePurgeOnce()
        {
            try
            {
                if (_lorePurged) return;
                _lorePurged = true;
                if (CampaignTime.Now.ToDays >= 30) return;
                if (!RangedLore.HasHighTierKnowledge(4)) return;
                RangedLore.ResetToVirgin();
                Log.Player("The bowyer's ledger was found forged - patterns above tier 1 are struck out. Learn them anew: smelt or study captured pieces.", true);
                Log.Info("Sanacja wiedzy: mloda kampania znala wzory T4+ (przeciek miedzy kampaniami) - reset do T1.");
            }
            catch (Exception e) { Log.Error("LorePurgeOnce", e); }
        }
        private static readonly string[][] AmmoRefund =
        {
            new[] { "ravens_teeth_arrows", "87" },
            new[] { "blunt_arrows", "12" },
            new[] { "range_arrows", "5" },
        };

        private void RefundSeizedAmmo()
        {
            try
            {
                if (_ammoRefundDone) return;
                _ammoRefundDone = true;
                var armory = QuartermasterLaw.DteArmory();
                if (armory == null) { return; }
                int back = 0;
                foreach (var row in AmmoRefund)
                {
                    int n; if (!int.TryParse(row[1], out n) || n <= 0) continue;
                    // nie oddajemy wiecej, niz naprawde lezy na polkach
                    int onShelf = 0;
                    for (int i = 0; i < armory.Count; i++)
                    {
                        var el = armory.GetElementCopyAtIndex(i);
                        var it = el.EquipmentElement.Item;
                        if (it != null && it.StringId == row[0]) onShelf += el.Amount;
                    }
                    int give = Math.Min(n, Math.Max(0, onShelf - StockOf(row[0])));
                    if (give <= 0) continue;
                    StockDeposit(row[0], give);
                    back += give;
                }
                if (back > 0)
                {
                    Log.Info("Zwrot amunicji przejetej przez blad: " + back + " szt. wrocilo do ksiegi gracza.");
                    Log.Player("Quartermaster: a miscount is set right - " + back
                               + " quivers are back on YOUR shelf, yours to take.", true);
                }
            }
            catch (Exception e) { Log.Error("RefundSeizedAmmo", e); }
        }

        private void OnPlayerUpgradedTroops(CharacterObject from, CharacterObject to, int num)
        {
            try
            {
                var armory = QuartermasterLaw.DteArmory();
                int total = 0;
                if (armory != null)
                    for (int i = 0; i < armory.Count; i++) total += armory.GetElementCopyAtIndex(i).Amount;
                // od 14.09 dopisujemy, CZY ten awans w ogole mial kosztowac rumaka -
                // inaczej "kon przy awansie kawalerii" jest hipoteza, nie pomiarem
                string mount;
                try
                {
                    bool srcMounted = from != null && !from.IsHero && from.IsMounted;
                    var cat = to != null ? to.UpgradeRequiresItemFromCategory : null;
                    mount = " | zrodlo konne: " + srcMounted
                          + ", wymog celu: " + (cat != null ? cat.StringId : "brak");
                }
                catch { mount = ""; }
                Log.Info("AWANS: " + (from != null ? from.StringId : "?") + " -> "
                    + (to != null ? to.StringId : "?") + " x" + num + " | magazyn: " + total + " szt." + mount);
            }
            catch (Exception e) { Log.Error("OnPlayerUpgradedTroops", e); }
        }

        private List<string> BuildArmoryWearSnapshot()
        {
            var list = new List<string>();
            try
            {
                var armory = QuartermasterLaw.DteArmory();
                if (armory == null) return list;
                for (int i = 0; i < armory.Count; i++)
                {
                    var el = armory.GetElementCopyAtIndex(i);
                    var it = el.EquipmentElement.Item;
                    var mod = el.EquipmentElement.ItemModifier;
                    if (it == null || mod == null || el.Amount <= 0) continue;
                    list.Add(it.StringId + "|" + mod.StringId + "|" + el.Amount);
                }
            }
            catch (Exception e) { Log.Error("BuildArmoryWearSnapshot", e); }
            return list;
        }

        private void TryRestoreArmoryWear(string why)
        {
            try
            {
                if (!_wearRestorePending) return;
                var armory = QuartermasterLaw.DteArmory();
                if (armory == null || armory.Count == 0) return;   // DTE jeszcze nie odtworzyl - czekamy
                _wearRestorePending = false;
                int restored = 0;
                foreach (var line in _armoryWear)
                {
                    var parts = (line ?? "").Split('|');
                    if (parts.Length != 3) continue;
                    var it = TaleWorlds.ObjectSystem.MBObjectManager.Instance.GetObject<ItemObject>(parts[0]);
                    var mod = TaleWorlds.ObjectSystem.MBObjectManager.Instance.GetObject<ItemModifier>(parts[1]);
                    int n; if (!int.TryParse(parts[2], out n) || n <= 0) continue;
                    if (it == null || mod == null) continue;
                    // ile czystych sztuk tego itemu jest do "zbicia" z powrotem
                    int clean = 0;
                    for (int i = 0; i < armory.Count; i++)
                    {
                        var el = armory.GetElementCopyAtIndex(i);
                        if (el.EquipmentElement.Item == it && el.EquipmentElement.ItemModifier == null)
                        { clean = el.Amount; break; }
                    }
                    int take = Math.Min(n, clean);
                    if (take <= 0) continue;
                    armory.AddToCounts(new EquipmentElement(it), -take);
                    armory.AddToCounts(new EquipmentElement(it, mod), take);
                    restored += take;
                }
                _armoryWear.Clear();
                if (restored > 0)
                    Log.Info("ArmoryWear (" + why + "): odtworzono stany " + restored + " szt. magazynu (save DTE gubi modifiery).");
            }
            catch (Exception e) { Log.Error("TryRestoreArmoryWear", e); }
        }

        internal static void StockWithdraw(string id, int n)
        {
            try
            {
                var self = Instance;
                if (self == null || string.IsNullOrEmpty(id) || n <= 0) return;
                int v; self._playerStock.TryGetValue(id, out v);
                v -= n;
                if (v <= 0) self._playerStock.Remove(id); else self._playerStock[id] = v;
                StockVersion++;
                if (!ClipHold && BookModOn()) ClipMod(id);   // ZB-1: suma rozbicia id nigdy ponad ksiege id (klucz wylaczony - bez zmian; porzadek ustawia rozbicie sam - ClipHold)
            }
            catch { }
        }

        // ===== ZB-1 (noc 17/18.10, przeglad 16.10 - STAN-PRAC 46; klucz ArmouryBookPerModifier): ROZBICIE KSIEGI NA EGZEMPLARZE =====
        // Ksiega wkladow (_playerStock) jest per id, a SwapMath.AllocateOwn kladl czesc gracza na NAJGORSZE egzemplarze id: przy dwoch egzemplarzach tego samego
        // id z innym modyfikatorem gracz przy nastepnym porzadku dostawal gorszy (jego "Fine X" nienoszony, obok zwykly X z zapasu ludzi - po porzadku jego czescia
        // byl zwykly X, a "Fine X" przechodzil na ludzi). Teraz obok sumy per id (zostaje: jedyny zapis dla starszej DLL i odczyt wszystkich "czy gracz ma cos
        // z tego id") jest rozbicie: klucz id|mod -> ile sztuk gracza TEGO egzemplarza. Niezmiennik: suma rozbicia id <= ksiega id; reszta ksiegi id bez rozbicia
        // (stary zapis, wklad bez egzemplarza - polegli) idzie jak dotad na najgorsze egzemplarze. Porzadek (QuartermasterLaw.PurgeUnusable) ustawia rozbicie
        // na czesc gracza z dopasowania (Target) - stary zapis migruje przy pierwszym porzadku dokladnie do tego, co gracz ma teraz (bez zmiany wlasnosci).
        // Zapis: nowy klucz arm_player_stock_mod ("1|id|mod=n;..."); stary zapis bez klucza - puste rozbicie; starsza DLL klucz pomija. Same etykiety wlasnosci -
        // zadna sztuka ani moneta nie powstaje ani nie znika (ksiega per id zmienia sie dokladnie tak jak dotad).
        private Dictionary<string, int> _playerStockMod = new Dictionary<string, int>();
        internal static bool ClipHold;          // porzadek (PurgeUnusable) w trakcie zmian ksiegi per id - rozbicie ustawi BookSetSplit, bez przycinania po drodze
        private int _bookModSeen = -1;          // stan klucza przy ostatnim odczycie: -1 nieznany (po wczytaniu / nowej grze), 0 wylaczony, 1 wlaczony
        private bool _bookModMigrate;           // wczytany zapis bez klucza arm_player_stock_mod - pierwszy porzadek z rozbiciem to migracja (linia raz)
        private const int BookModMaxKeys = 20000;   // straz rozmiaru zapisu (realnie dziesiatki-setki)

        /// <summary>ZB-1: magazyn DTE po wczytaniu jest jeszcze bez stanow (save DTE gubi modyfikatory) - rozbicie nie moze sie wtedy dopasowywac do polek.</summary>
        internal static bool WearRestorePending { get { var self = Instance; return self != null && self._wearRestorePending; } }

        /// <summary>ZB-1: czy ksiega jest rozbita na egzemplarze (klucz ArmouryBookPerModifier). Przelaczenie klucza w trakcie gry czysci rozbicie (przy wylaczonym
        /// nie bylo prowadzone) - przy najblizszym porzadku migracja z ksiegi per id, jak ze starego zapisu.</summary>
        internal static bool BookModOn()
        {
            var s = Settings.Current;
            bool on = s != null && s.ArmouryBookPerModifier;
            var self = Instance;
            if (self == null) return on;
            int v = on ? 1 : 0;
            if (self._bookModSeen != v)
            {
                if (self._bookModSeen >= 0 && self._playerStockMod.Count > 0)
                {
                    ArmouryZb.NoteModeReset(self._playerStockMod.Count);
                    self._playerStockMod.Clear();
                    if (on) self._bookModMigrate = true;
                }
                else if (self._bookModSeen >= 0 && on) self._bookModMigrate = true;
                else if (self._bookModSeen < 0 && !on && self._playerStockMod.Count > 0)
                {
                    // poprawka po recenzji: wczytane rozbicie przy wylaczonym kluczu nie bylo czyszczone (warunek wyzej wymaga stanu znanego), a przy wylaczonym
                    // nikt go nie prowadzi - linia doby i sprawdzarka (NZB-3) widzialy stare klucze i "rozbicie ponad ksiege". Gry to nie zmienialo (kazdy odczyt
                    // pod BookModOn), wiec czyscimy tak samo jak przy przelaczeniu: ksiega per id bez zmian, po wlaczeniu klucza migracja przy porzadku.
                    ArmouryZb.NoteLoadedOff(self._playerStockMod.Count);
                    self._playerStockMod.Clear();
                }
                self._bookModSeen = v;
            }
            return on;
        }

        /// <summary>ZB-1: czesc gracza egzemplarza (klucz id|mod) z rozbicia.</summary>
        internal static int StockOfKey(string key)
        {
            try
            {
                var self = Instance;
                if (self == null || string.IsNullOrEmpty(key)) return 0;
                int v; return self._playerStockMod.TryGetValue(key, out v) ? v : 0;
            }
            catch { return 0; }
        }

        private static string IdOfKey(string key) { int i = key != null ? key.IndexOf('|') : -1; return i >= 0 ? key.Substring(0, i) : (key ?? ""); }

        /// <summary>ZB-1: reszta ksiegi id bez rozbicia (najgorsze egzemplarze, jak dotad).</summary>
        internal static int StockRestOf(string id)
        {
            try
            {
                var self = Instance;
                if (self == null || string.IsNullOrEmpty(id)) return 0;
                int book; self._playerStock.TryGetValue(id, out book);
                return Math.Max(0, book - ModSumOf(self, id));
            }
            catch { return 0; }
        }

        private static int ModSumOf(ArmouryBehavior self, string id)
        {
            int sum = 0; string pre = id + "|";
            foreach (var kv in self._playerStockMod) if (kv.Key.StartsWith(pre, StringComparison.Ordinal)) sum += kv.Value;
            return sum;
        }

        /// <summary>ZB-1: wklad gracza ZE STANEM (ekran zbrojowni, ksiega musztry, zamowienie u kowala) - ksiega per id jak StockDeposit, a przy kluczu takze rozbicie.</summary>
        internal static void StockDepositKey(EquipmentElement el, int n)
        {
            try
            {
                var it = el.Item;
                if (it == null || string.IsNullOrEmpty(it.StringId) || n <= 0) return;
                StockDeposit(it.StringId, n);
                if (!BookModOn()) return;
                var self = Instance;
                if (self == null) return;
                string key = QuartermasterLaw.KeyOf(el);
                int v; self._playerStockMod.TryGetValue(key, out v); self._playerStockMod[key] = v + n;
                ArmouryZb.NoteKeyMove(n, true);
            }
            catch (Exception e) { ArmouryZb.Stumble("StockDepositKey", e); }
        }

        /// <summary>ZB-1: wyjecie sztuki gracza ZE STANEM - najpierw z rozbicia tego egzemplarza, potem ksiega per id (z przycieciem rozbicia do ksiegi).</summary>
        internal static void StockWithdrawKey(EquipmentElement el, int n)
        {
            try
            {
                var it = el.Item;
                if (it == null || string.IsNullOrEmpty(it.StringId) || n <= 0) return;
                var self = Instance;
                if (self != null && BookModOn())
                {
                    string key = QuartermasterLaw.KeyOf(el);
                    int v; self._playerStockMod.TryGetValue(key, out v);
                    int cut = Math.Min(v, n);
                    if (v - cut > 0) self._playerStockMod[key] = v - cut; else self._playerStockMod.Remove(key);
                    ArmouryZb.NoteKeyMove(n, false);
                }
                StockWithdraw(it.StringId, n);
            }
            catch (Exception e) { ArmouryZb.Stumble("StockWithdrawKey", e); }
        }

        /// <summary>ZB-1: suma rozbicia id ponad ksiege id (przyciecie ksiegi, wyjecie spoza rozbicia) - zdejmujemy roszczenia od NAJGORSZEGO egzemplarza
        /// (wrak, potem nizsza jakosc); zdjete sztuki to te, ktore gracz stracil. Zwraca zdjete.</summary>
        private static int ClipMod(string id)
        {
            var self = Instance;
            if (self == null || string.IsNullOrEmpty(id) || self._playerStockMod.Count == 0) return 0;
            int book; self._playerStock.TryGetValue(id, out book);
            string pre = id + "|";
            var keys = new List<string>();
            int sum = 0;
            foreach (var kv in self._playerStockMod) if (kv.Key.StartsWith(pre, StringComparison.Ordinal)) { keys.Add(kv.Key); sum += kv.Value; }
            int over = sum - Math.Max(0, book);
            if (over <= 0) return 0;
            keys.Sort((a, b) =>
            {
                float qa, qb; bool wa = ModWreck(a.Substring(pre.Length), out qa), wb = ModWreck(b.Substring(pre.Length), out qb);
                int d = (wb ? 1 : 0).CompareTo(wa ? 1 : 0); if (d != 0) return d;
                d = qa.CompareTo(qb); if (d != 0) return d;
                return string.CompareOrdinal(a, b);
            });
            int cut = 0;
            foreach (var k in keys)
            {
                if (over <= 0) break;
                int v = self._playerStockMod[k];
                int c = Math.Min(v, over);
                if (v - c > 0) self._playerStockMod[k] = v - c; else self._playerStockMod.Remove(k);
                over -= c; cut += c;
            }
            ArmouryZb.NoteClip(cut);
            return cut;
        }

        /// <summary>ZB-1: wrak i jakosc (PriceMultiplier) modyfikatora po id; "" - bez modyfikatora (1.0). Blad odczytu - jak bez modyfikatora.</summary>
        private static bool ModWreck(string modId, out float q)
        {
            q = 1f;
            if (string.IsNullOrEmpty(modId)) return false;
            try
            {
                var m = TaleWorlds.ObjectSystem.MBObjectManager.Instance.GetObject<ItemModifier>(modId);
                if (m == null) return false;
                q = m.PriceMultiplier;
                return LootPrices.IsWreck(m);
            }
            catch { return false; }
        }

        /// <summary>ZB-1: porzadek (PurgeUnusable) - rozbicie id z tych egzemplarzy = czesc gracza z dopasowania (Target); klucze tych id spoza listy (egzemplarza
        /// juz nie ma na polce) znikaja. Ksiege per id porzadek zmienil chwile wczesniej o sume (Target - Own), wiec suma Target <= ksiega id. Zwraca sume Target.</summary>
        internal static int BookSetSplit(List<SwapMath.Piece> pieces)
        {
            var self = Instance;
            if (self == null || pieces == null || pieces.Count == 0) return 0;
            var ids = new HashSet<string>();
            foreach (var p in pieces) ids.Add(p.Id ?? "");
            foreach (var k in new List<string>(self._playerStockMod.Keys))
                if (ids.Contains(IdOfKey(k))) self._playerStockMod.Remove(k);
            int sum = 0;
            foreach (var p in pieces)
            {
                if (p.Target <= 0) continue;
                int v; self._playerStockMod.TryGetValue(p.Key, out v);
                self._playerStockMod[p.Key] = v + p.Target;
                sum += p.Target;
            }
            foreach (var id in ids) ClipMod(id);
            return sum;
        }

        /// <summary>ZB-1: czy wczytany zapis nie mial rozbicia (migracja przy pierwszym porzadku) - zdejmuje flage.</summary>
        internal static bool TakeBookMigrate() { var self = Instance; if (self == null || !self._bookModMigrate) return false; self._bookModMigrate = false; return true; }

        /// <summary>ZB-1: stan do linii doby - ksiega per id (id, szt.), rozbicie (klucze, szt. w granicach ksiegi), reszta bez rozbicia, rozbicie ponad ksiege.</summary>
        internal static void BookModStats(out int ids, out int pcs, out int keys, out int modPcs, out int rest, out int over)
        {
            ids = 0; pcs = 0; keys = 0; modPcs = 0; rest = 0; over = 0;
            var self = Instance;
            if (self == null) return;
            BookModOn();   // poprawka po recenzji: stan klucza przed odczytem (wczytane rozbicie przy wylaczonym kluczu - wyczyszczone, nie liczone w linii doby)
            var sumBy = new Dictionary<string, int>();
            foreach (var kv in self._playerStockMod) { keys++; int v; sumBy.TryGetValue(IdOfKey(kv.Key), out v); sumBy[IdOfKey(kv.Key)] = v + kv.Value; }
            foreach (var kv in self._playerStock)
            {
                if (kv.Value <= 0) continue;
                ids++; pcs += kv.Value;
                int m; sumBy.TryGetValue(kv.Key, out m);
                modPcs += Math.Min(m, kv.Value); rest += Math.Max(0, kv.Value - m); over += Math.Max(0, m - kv.Value);
            }
            foreach (var kv in sumBy) if (!self._playerStock.ContainsKey(kv.Key)) over += kv.Value;
        }

        /// <summary>ZB-1: kopia rozbicia na czas sesji ekranu (Reset/Cancel ekranu cofa ja razem z ksiega per id).</summary>
        internal static Dictionary<string, int> StockModSnapshot()
        {
            try { var self = Instance; return self != null ? new Dictionary<string, int>(self._playerStockMod) : null; }
            catch { return null; }
        }

        internal static void StockModRestore(Dictionary<string, int> snap)
        {
            try { var self = Instance; if (self == null || snap == null) return; self._playerStockMod = new Dictionary<string, int>(snap); }
            catch { }
        }

        /// <summary>ZB-1: zapis rozbicia "1|id|mod=n;..." (klucz wylaczony albo puste - ""); straz rozmiaru BookModMaxKeys.</summary>
        private string ExportStockMod()
        {
            if (!BookModOn() || _playerStockMod.Count == 0) return "";
            if (_playerStockMod.Count > BookModMaxKeys)
            {
                Log.Info("Zbrojownia (ZB-1): rozbicie ksiegi ma " + _playerStockMod.Count + " kluczy (> " + BookModMaxKeys + ") - nie zapisane, po wczytaniu migracja z ksiegi per id.");
                return "";
            }
            var sb = new System.Text.StringBuilder("1|");
            bool first = true;
            foreach (var kv in _playerStockMod)
            {
                if (kv.Value <= 0 || string.IsNullOrEmpty(kv.Key) || kv.Key.IndexOf(';') >= 0 || kv.Key.IndexOf('=') >= 0) continue;
                if (!first) sb.Append(';');
                sb.Append(kv.Key).Append('=').Append(kv.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
                first = false;
            }
            return sb.ToString();
        }

        /// <summary>ZB-1: odczyt rozbicia. null/"" - stary zapis albo klucz byl wylaczony (migracja przy pierwszym porzadku); inny format albo za duzy - pominiety
        /// (tez migracja). Rozbicie przyciete do ksiegi per id wczytanej wczesniej (arm_player_stock).</summary>
        private void ImportStockMod(string data)
        {
            _playerStockMod = new Dictionary<string, int>();
            _bookModSeen = -1;
            if (string.IsNullOrEmpty(data)) { _bookModMigrate = true; return; }
            if (!data.StartsWith("1|", StringComparison.Ordinal) || data.Length > 4000000)
            {
                _bookModMigrate = true;
                Log.Info("Zbrojownia (ZB-1): zapis rozbicia ksiegi w nieznanym formacie albo za duzy (" + data.Length + " znakow) - pominiety, migracja z ksiegi per id.");
                return;
            }
            int bad = 0;
            foreach (var part in data.Substring(2).Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                int eq = part.LastIndexOf('=');
                int n;
                if (eq <= 0 || part.IndexOf('|') <= 0 || !int.TryParse(part.Substring(eq + 1), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out n) || n <= 0)
                { bad++; continue; }
                if (_playerStockMod.Count >= BookModMaxKeys) { bad++; continue; }
                string key = part.Substring(0, eq);
                int v; _playerStockMod.TryGetValue(key, out v); _playerStockMod[key] = v + n;
            }
            // przyciecie do ksiegi per id (bez jakosci modyfikatora - przy wczytaniu tylko niespojny zapis; od klucza o najwiekszej czesci)
            var byId = new Dictionary<string, List<string>>();
            foreach (var k in _playerStockMod.Keys) { List<string> l; string id = IdOfKey(k); if (!byId.TryGetValue(id, out l)) byId[id] = l = new List<string>(); l.Add(k); }
            int cut = 0;
            foreach (var kv in byId)
            {
                int book; if (_playerStock == null || !_playerStock.TryGetValue(kv.Key, out book)) book = 0;
                int sum = 0; foreach (var k in kv.Value) sum += _playerStockMod[k];
                int over = sum - Math.Max(0, book);
                if (over <= 0) continue;
                kv.Value.Sort(string.CompareOrdinal);
                foreach (var k in kv.Value)
                {
                    if (over <= 0) break;
                    int v = _playerStockMod[k]; int c = Math.Min(v, over);
                    if (v - c > 0) _playerStockMod[k] = v - c; else _playerStockMod.Remove(k);
                    over -= c; cut += c;
                }
            }
            if (bad > 0 || cut > 0) Log.Info("Zbrojownia (ZB-1): wczytane rozbicie ksiegi - kluczy " + _playerStockMod.Count + ", wpisy bledne " + bad + ", przyciete do ksiegi per id " + cut + " szt.");
        }

        /// <summary>Kopia ksiegi wkladow (na czas sesji ekranu zbrojowni -
        /// Reset/Cancel ekranu cofa rostery hurtem bez TransferItem, wiec
        /// ksiege trzeba umiec cofnac razem z nimi).</summary>
        internal static Dictionary<string, int> StockSnapshot()
        {
            try
            {
                var self = Instance;
                return self != null ? new Dictionary<string, int>(self._playerStock) : null;
            }
            catch { return null; }
        }

        internal static void StockRestore(Dictionary<string, int> snap)
        {
            try
            {
                var self = Instance;
                if (self == null || snap == null) return;
                self._playerStock = new Dictionary<string, int>(snap);
                StockVersion++;
            }
            catch { }
        }

        /// <summary>
        /// KSIEGA-DUCH. Sztuki gracza schodza z polek roznymi drzwiami bez
        /// StockWithdraw: DTE zdejmuje kolczan przy spawnie lucznika,
        /// wystrzelany do zera przepada, CleanseTrashInBags i sanacja tna
        /// stosy. Ksiega puchnie ponad polki, a wtedy WarOwnedOf zaniza stan
        /// wojska i HoldReserve pokazuje graczowi cudze sztuki jako wlasne.
        /// Przed kazdym otwarciem zbrojowni przycinamy ksiege per id do tego,
        /// co NAPRAWDE lezy na polkach.
        /// </summary>
        internal static void ReconcileStock(string why)
        {
            try
            {
                var self = Instance;
                var armory = QuartermasterLaw.DteArmory();
                if (self == null || armory == null || QuartermasterEscrow.Active) return;
                // pusty magazyn = DTE jeszcze nie odtworzyl rostera po load
                // (por. TryRestoreArmoryWear) - przyciecie teraz wyzerowaloby
                // CALA ksiege gracza; czekamy na prawdziwe polki
                if (armory.Count == 0) return;
                // DTE trzyma tez pozycje NIEROZWIAZANE (id, ktorych nie umial
                // znalezc przy load) i doklada je na polki dopiero pozniej
                // (RestoreReadyArmoryItems) - poki cos wisi, ksiegi nie tykamy,
                // zeby nie przycinac wkladu, ktory za chwile wroci
                try
                {
                    var tBeh = QuartermasterLaw.FindType("DynamicTroopEquipmentReupload.ArmyArmoryBehavior");
                    if (tBeh != null)
                    {
                        object beh = null;
                        var mGet = typeof(Campaign).GetMethod("GetCampaignBehavior");
                        if (mGet != null && Campaign.Current != null)
                            beh = mGet.MakeGenericMethod(tBeh).Invoke(Campaign.Current, null);
                        var fU = beh != null ? HarmonyLib.AccessTools.Field(tBeh, "_unresolvedArmoryItemCounts") : null;
                        var dict = fU != null ? fU.GetValue(beh) as System.Collections.IDictionary : null;
                        if (dict != null && dict.Count > 0)
                        {
                            Log.Info("ReconcileStock(" + why + "): DTE ma " + dict.Count
                                     + " nierozwiazanych pozycji - rekonsyliacja odlozona.");
                            return;
                        }
                    }
                }
                catch { }
                var shelf = new Dictionary<string, int>();
                for (int i = 0; i < armory.Count; i++)
                {
                    var el = armory.GetElementCopyAtIndex(i);
                    var it = el.EquipmentElement.Item;
                    if (it == null || it.StringId == null || el.Amount <= 0) continue;
                    int v; shelf.TryGetValue(it.StringId, out v);
                    shelf[it.StringId] = v + el.Amount;
                }
                // ZB-1: rozbicie na egzemplarze przyciete do polek PER EGZEMPLARZ (zuzyty, naprawiony albo zabrany egzemplarz zmienil klucz) - nadmiar wraca do reszty
                // ksiegi id (najgorsze egzemplarze); nie przy magazynie jeszcze bez stanow po wczytaniu (TryRestoreArmoryWear w toku)
                int ghostKey = 0;
                if (self._playerStockMod.Count > 0 && BookModOn() && !self._wearRestorePending)
                {
                    try
                    {
                        var shelfKey = new Dictionary<string, int>();
                        for (int i = 0; i < armory.Count; i++)
                        {
                            var el = armory.GetElementCopyAtIndex(i);
                            if (el.EquipmentElement.Item == null || el.Amount <= 0) continue;
                            string k = QuartermasterLaw.KeyOf(el.EquipmentElement);
                            int v; shelfKey.TryGetValue(k, out v); shelfKey[k] = v + el.Amount;
                        }
                        foreach (var k in new List<string>(self._playerStockMod.Keys))
                        {
                            int mine = self._playerStockMod[k];
                            int have; shelfKey.TryGetValue(k, out have);
                            if (mine <= have) continue;
                            ghostKey += mine - have;
                            if (have <= 0) self._playerStockMod.Remove(k); else self._playerStockMod[k] = have;
                        }
                    }
                    catch (Exception e) { ArmouryZb.Stumble("ReconcileStock.Mod", e); }
                }
                int ghost = 0;
                var ids = new List<string>(self._playerStock.Keys);
                foreach (var id in ids)
                {
                    int book = self._playerStock[id];
                    int have; shelf.TryGetValue(id, out have);
                    if (book <= have) continue;
                    ghost += book - have;
                    if (have <= 0) self._playerStock.Remove(id); else self._playerStock[id] = have;
                    StockVersion++;
                    if (self._playerStockMod.Count > 0 && BookModOn()) ClipMod(id);   // ZB-1: niezmiennik rozbicie <= ksiega id
                }
                if (ghost > 0)
                    Log.Info("ReconcileStock(" + why + "): ksiega gracza przycieta o " + ghost
                             + " szt. ducha (ubytki z polek poza ekranem zbrojowni).");
                if (ghostKey > 0)
                {
                    ArmouryZb.NoteGhostKey(ghostKey);
                    Log.Info("ReconcileStock(" + why + "): rozbicie ksiegi na egzemplarze (ZB-1) przyciete o " + ghostKey
                             + " szt. (egzemplarza o tym stanie nie ma na polkach - zostaja w ksiedze id na najgorszych egzemplarzach).");
                }
            }
            catch (Exception e) { Log.Error("ReconcileStock", e); }
        }
        private Dictionary<string,int> _prisonerBaseline;
        // p6-okup (R3-03a): ochrony wsi po okupie od zera przy nowej kampanii / przed wczytaniem (osobna linia z dala od linii fali 1 - scalenie fali 2 bez konfliktu)
        private readonly bool _p6OkupReset = P6OkupReset();
        private static bool P6OkupReset() { try { RaidTribute.Reset(); } catch (Exception e) { Log.Error("P6OkupReset", e); } return true; }
        // m97-wioski: wjazd do wiosek pobocznych - stan wejscia, okupy i rabunek w toku od zera (osobna linia, jak p6-okup)
        private readonly bool _m97WioskiReset = M97WioskiReset();
        private static bool M97WioskiReset() { try { SideVillageVisit.Reset(); } catch (Exception e) { Log.Error("M97WioskiReset", e); } return true; }

        // r4-mur (danina zbozowa Muru, glod Wolnych Ludzi -> najazdy): reset stanu kampanii w inicjalizatorze pola (biegnie przy kazdym konstruktorze ArmouryBehavior,
        // przed jego cialem) - osobna linia zamiast dopisku do jednolinijkowego konstruktora (scalenia paczek nocy)
        private readonly bool _r4MurReset = R4MurReset();
        private static bool R4MurReset() { WallTribute.Reset(); FreeFolkRaids.Reset(); return true; }

        public ArmouryBehavior() { Instance = this; HistoricalPrices.Reset(); MaterialLaw.Reset(); WesterosClimate.Reset(); OutlawLaw.Reset(); PopulationLaw.Reset(); WorkshopLaw.Reset(); IronBank.Reset(); MarketGlut.Reset(); ArmsPricing.Reset(); StartKit.Reset(); AiGear.Reset(); Levy.Reset(); VolunteerKit.Reset(); LevyGold.Reset(); WearKeep.Reset(); UniqueSpoils.Reset(); BuildDiary.Reset(); BuildFunding.Reset(); WarFront.Reset(); KingdomLedger.Reset(); ColdStart.Reset(); MenPurse.Reset(); ArmyClothing.Reset(); AiWear.Reset(); SmithHours.Reset(); RecruitKit.Reset(); OreLedger.Reset(); GoodsLedger.Reset(); FreeSupplies.Reset(); VillageWoodlot.Reset(); SpoilsSeal.Reset(); MarketRoad.Reset(); MarketCarts.Reset(); VillageClogDiag.Reset(); CartTownExit.Reset(); CartRisk.Reset(); CartGuard.Reset(); MoneyLedger.Reset(); PeopleLedger.Reset(); CaravanBulk.Reset(); StartStock.Reset(); MineralOnce.Reset(); SoldierPay.Reset(); KingdomTreasury.Reset(); RecruitCost.Reset(); NightRest.ResetWorld(); WorldMeasure.Reset(); CirculationWindows.Reset(); ClanIncomeBook.Reset(); LosersFlee.Reset(); StragglersRally.Reset(); PrisonerLaw.Reset(); RecruitSources.Reset(); GarrisonCarts.Reset(); GarrisonArmory.Reset(); ArmsDrill.Reset(); CaravanAmmo.Reset(); ArmsLeaks.Reset(); SupplyDemand.ResetOrders(); MaterialOrders.Reset(); RawNoRot.Reset(); ArmsScrap.Reset(); MenUpgrade.Reset(); GarrisonKit.Reset(); QuartermasterLaw.Reset(); ShelfBuy.Reset(); Measure174b.Reset(); ShelfIndex.Reset(); Cost174.Reset(); DayProfile.Reset(); SkillLedger.Reset(); Drill.Reset(); HorseCensus.Reset(); KingdomBalance.Reset(); NorthHomeEdge.Reset(); ArmoryFit.Reset(); CastleSteel.Reset(); ValyrianBlades.Reset(); QohorMasters.Reset(); Measure169c.Reset(); Measure40.Reset(); CastlePurse.Reset(); VillageTakings.Reset(); BeatenPurse.Reset(); DefeatMorale.Reset(); CrownIncome.Reset(); CrownGifts.Reset(); ClanBudget.Reset(); MercContract.Reset(); CrownRents.Reset(); GentryService.Reset(); DebtLadder.Reset(); CrownCallToWar.Reset(); Ransom178.Reset(); CrownBorrow.Reset(); PeopleBook.Reset(); PeopleUnit.Reset(); OthersRise.Reset(); Devastation.Reset(); ScorchedEarth.Reset(); ArmsFromPeople.Reset(); TownGrowth.Reset(); VillageBook.Reset(); WatchLaw.Reset(); ThrallLaw.Reset(); TownFolk.Reset(); Sutlers.Reset(); NotableGold.Reset(); SmallGold.Reset(); BkDipGold.Reset(); BesiegedPay.Reset(); GarrisonRecruitFee.Reset(); Stage6Measure.Reset(); WorkshopInputs.Reset(); CamelLaw.Reset(); LordsEvenUp.Reset(); CastleMarket.Reset(); VetPurse.Reset(); HorseState.Reset(); HamletKinds.Reset(); HorseFate.Reset(); SpareHorses.Reset(); PackMarket.Reset(); PackSources.Reset(); AlleyProbe.Reset(); Bloomery.Reset(); TradeCarts.Reset(); ToolsByUse.Reset(); WorkshopRecipes.Reset(); SlaughterHides.Reset(); WorkshopInputsFix.Reset(); HarvestValve.Reset(); CavalryTemplate.Reset(); WorldClock7.Reset(); TerrainEase.Reset(); WorldTime7.Reset(); TwinsCrossing.Reset(); VillageNews.Reset(); DistrictRaid.Reset(); MapVillageLore.Reset(); VillageHolders.Reset(); DistrictMenu.Reset(); MapRoadsProbe.Reset(); CitySack.Reset(); PeopleScale.Reset(); VillageByPeople.Reset(); PeopleMarket.Reset(); PeopleLevy.Reset(); GoodsRates.Reset(); GoodsPaths.Reset(); TownInns.Reset(); BulkUnit.Reset(); NightEcon.Reset(); GentryNames.Reset(); FiefGrants.Reset(); ReaverLaw.Reset(); DoomedPartyMen.Reset(); UniqueShelf.Reset(); OthersDuel.Reset(); A17Diag.Reset(); SiegeRansom.Reset(); VineOlive.Reset(); DayHeld.Reset(); FallenCrown.Reset(); RockGold.Reset(); DragonFeed.Reset(); MercTenure.Reset(); RansomRepeat.Reset(); PeaceHold.Reset(); TaxCounterGuard.Reset(); CaptiveArmory.Reset(); VetKit.Reset(); GearFit.Reset(); CaravanGoods.Reset(); TownToll.Reset(); CourtGoods.Reset(); MenUpgradeOrders.Reset(); DailyLife.Reset(); DsVillage.Reset(); DsTowns.Reset(); DsSeason.Reset(); StartCalibration.Reset(); KsFoodCarts.Reset(); RoadsideInns.Reset(); Rumours.Reset(); InnDrink.Reset(); TownInns.RoadDayReset(); SideVillageFief.Reset(); VillageClimate.Reset(); ForcedMarch.Reset(); ArmyRestDay.Reset(); }

        // inwest (noc 17/18.10): stan kampanii zajec wiosek, ikon i prac okregu od zera przy kazdym nowym zachowaniu (jak Reset w konstruktorze; osobna linia - bez zmiany
        // wspolnej linii konstruktora, ktora dopisuja inne paczki)
        private readonly bool _inwestReset = InwestReset();
        private static bool InwestReset() { try { VillageCrafts.Reset(); DistrictIcon.Reset(); DistrictWorks.Reset(); FiefInvestments.Reset(); } catch (Exception e) { Log.Error("InwestReset", e); } return true; }
        // f5-bitwa (noc 17/18.10): zestaw bitwy autotestu - stan poprzedniej kampanii od zera (osobna linia, jak inwest)
        private readonly bool _f5BitwaReset = F5BitwaReset();
        private static bool F5BitwaReset() { try { AutotestBattleKit.Reset(); } catch (Exception e) { Log.Error("F5BitwaReset", e); } return true; }
        // f9-predkosc: liczniki linii "Predkosc mapy (f9)" od zera przy nowej kampanii / wczytaniu (osobna linia, jak inwest)
        private readonly bool _f9Reset = F9Reset();
        private static bool F9Reset() { try { MapSpeedProfile.Reset(); } catch (Exception e) { Log.Error("F9Reset", e); } return true; }
        // m110-stan krok 2: rejestry misji ochrony z dokladnego HP i ksiegi sprzetu towarzyszy od zera przy nowej kampanii / wczytaniu (osobna linia, jak inwest)
        private readonly bool _m110HpReset = M110HpReset();
        private static bool M110HpReset() { try { HeroHp.Reset(); HeroBook.Reset(); } catch (Exception e) { Log.Error("M110HpReset", e); } return true; }
        // r4-przetop: skrzynka resztek przetopu od zera przy nowej kampanii / przed wczytaniem (osobna linia, jak inwest)
        private readonly bool _r4ScrapsReset = R4ScrapsReset();
        private static bool R4ScrapsReset() { try { SmeltScraps.Reset(); } catch (Exception e) { Log.Error("R4ScrapsReset", e); } return true; }
        // m96-receptury: otwarte ladunki lnu i skory od zera przy nowej kampanii / przed wczytaniem (osobna linia, jak r4-przetop)
        private readonly bool _m96RecepturyReset = M96RecepturyReset();
        private static bool M96RecepturyReset() { try { SmithStock.Reset(); } catch (Exception e) { Log.Error("M96RecepturyReset", e); } return true; }
        // m96-czaskuzni: sesja ekranu kuzni, powrot z paska i liczniki robot gracza od zera przy nowej kampanii / przed wczytaniem (osobna linia, jak r4-przetop)
        private readonly bool _m96CzasKuzniReset = M96CzasKuzniReset();
        private static bool M96CzasKuzniReset() { try { ForgeJob.Reset(); } catch (Exception e) { Log.Error("M96CzasKuzniReset", e); } return true; }
        // p5-handel (R3-05a/b): jarmarki i barki od zera przy nowej kampanii / przed wczytaniem (osobna linia, jak r4-przetop)
        private readonly bool _p5HandelReset = P5HandelReset();
        private static bool P5HandelReset() { try { HarvestFairs.Reset(); RiverBarges.Reset(); } catch (Exception e) { Log.Error("P5HandelReset", e); } return true; }
        // p5-urodzaj (R3-05e/f, recenzja): rok urodzaju i uboj jesienny - stan i przedmioty od zera przy nowej kampanii / przed wczytaniem (osobna linia, jak inwest)
        private readonly bool _p5UrodzajReset = P5UrodzajReset();
        private static bool P5UrodzajReset() { try { HarvestYear.Reset(); HerdCull.Reset(); } catch (Exception e) { Log.Error("P5UrodzajReset", e); } return true; }
        // p5-kruki (R3-05c): kruki w drodze od zera przy nowej kampanii / przed wczytaniem (osobna linia, jak inwest)
        private readonly bool _p5RavensReset = P5RavensReset();
        private static bool P5RavensReset() { try { Ravens.Reset(); } catch (Exception e) { Log.Error("P5RavensReset", e); } return true; }
        // p6-szubienice (R3-07a): szubienice przy rozstajach od zera przy nowej kampanii / przed wczytaniem (osobna linia, jak p5-handel)
        private readonly bool _p6SzubieniceReset = P6SzubieniceReset();
        private static bool P6SzubieniceReset() { try { Gallows.Reset(); } catch (Exception e) { Log.Error("P6SzubieniceReset", e); } return true; }
        // p6-sztormy (R3-05d): liczniki zimowych sztormow i lista burz od zera przy nowej kampanii / przed wczytaniem (osobna linia, jak p5-urodzaj)
        private readonly bool _p6SztormyReset = P6SztormyReset();
        private static bool P6SztormyReset() { try { SeaStorms.Reset(); } catch (Exception e) { Log.Error("P6SztormyReset", e); } return true; }
        // p6-drogi (R3-07d): AI wybiera drogi - pamiec planow partii i liczniki od zera przy nowej kampanii / przed wczytaniem (osobna linia, jak p5-urodzaj)
        private readonly bool _p6DrogiReset = P6DrogiReset();
        private static bool P6DrogiReset() { try { AiRoadRoutes.Reset(); } catch (Exception e) { Log.Error("P6DrogiReset", e); } return true; }
        // p6-spichlerz (R3-06f): zakup dnia i liczniki spichlerza miejskiego od zera przy nowej kampanii / przed wczytaniem (osobna linia, jak inwest)
        private readonly bool _p6SpichlerzReset = P6SpichlerzReset();
        private static bool P6SpichlerzReset() { try { TownGranary.Reset(); } catch (Exception e) { Log.Error("P6SpichlerzReset", e); } return true; }
        // p6-a7 (A7 + odplyw kas miast): pulapy kas miast robot z rak okregu od zera przy nowej kampanii / przed wczytaniem (osobna linia, jak p5)
        private readonly bool _p6a7Reset = P6a7Reset();
        private static bool P6a7Reset() { try { TownHire.Reset(); } catch (Exception e) { Log.Error("P6a7Reset", e); } return true; }
        // m96-dlugi: ksiegi dlugow rad miast od zera przy nowej kampanii / przed wczytaniem (osobna linia, jak p6-a7)
        private readonly bool _m96DlugiReset = M96DlugiReset();
        private static bool M96DlugiReset() { try { TownDebt.Reset(); } catch (Exception e) { Log.Error("M96DlugiReset", e); } return true; }
        // m102-tablica: tablica ogloszen - flagi z tablicy, listy gonczne kapitanow i soltysow od zera przy nowej kampanii / przed wczytaniem (osobna linia, jak m96-dlugi)
        private readonly bool _m102TablicaReset = M102TablicaReset();
        private static bool M102TablicaReset() { try { NoticeBoard.Reset(); } catch (Exception e) { Log.Error("M102TablicaReset", e); } return true; }
        // m103-lektor: lektor - klip, strona plotek i liczniki doby od zera przy nowej kampanii / przed wczytaniem (osobna linia, jak m102-tablica)
        private readonly bool _m103LektorReset = M103LektorReset();
        private static bool M103LektorReset() { try { Lektor.Reset(); } catch (Exception e) { Log.Error("M103LektorReset", e); } return true; }

        public override void SyncData(IDataStore dataStore)
        {
            // zapas startowy rudy i drewna przeliczony na ladunki raz na kampanie (StartStock); brak klucza = stary zapis.
            // Osobny try PRZED reszta: wyjatek innego klucza nie moze zgubic tej flagi - zgubiona flaga i wczytanie w pierwszej
            // dobie kampanii to drugie dzielenie zapasu przez przelicznik ladunku.
            try
            {
                string startStock = StartStock.Export();
                SaveText.Sync(dataStore, "arm_startstock", ref startStock);
                if (dataStore.IsLoading) StartStock.Import(startStock);
            }
            catch (Exception e) { Log.Error("SyncData.StartStock", e); }
            // N11 (noc 16.10): jednostka ladunku rudy i drewna tej kampanii "1|czynnik" - brak klucza = stary zapis (10); osobny try jak arm_startstock
            try
            {
                string bulkUnit = dataStore.IsSaving ? BulkUnit.Export() : null;
                SaveText.Sync(dataStore, "arm_bulk_unit", ref bulkUnit);
                if (dataStore.IsLoading) BulkUnit.Import(bulkUnit);
            }
            catch (Exception e) { Log.Error("SyncData.arm_bulk_unit", e); }
            // m97-oboz: wozy druzyny gracza "1|wozy" - brak klucza = stary zapis (0 wozow); osobny try
            try
            {
                string campCart = dataStore.IsSaving ? CampCart.Export() : null;
                SaveText.Sync(dataStore, "arm_campcart", ref campCart);
                if (dataStore.IsLoading) CampCart.Import(campCart);
            }
            catch (Exception e) { Log.Error("SyncData.arm_campcart", e); }
            // m97-szkolenie: godziny cwiczen gracza z menu obozu "1|dzis|doba switu|do policzenia" - brak klucza = stary zapis (0); osobny try
            try
            {
                string campDrill = dataStore.IsSaving ? CampDrill.Export() : null;
                SaveText.Sync(dataStore, "arm_campdrill", ref campDrill);
                if (dataStore.IsLoading) CampDrill.Import(campDrill);
            }
            catch (Exception e) { Log.Error("SyncData.arm_campdrill", e); }
            // m99-markietani: stan dnia rzadu markietanow gracza "1|doba|zabawa|opatrzeni|id:n,..." - brak klucza = stary zapis (pusty dzien); osobny try
            try
            {
                string sutlerRow = dataStore.IsSaving ? CampSutlers.Export() : null;
                SaveText.Sync(dataStore, "arm_sutlerrow", ref sutlerRow);
                if (dataStore.IsLoading) CampSutlers.Import(sutlerRow);
            }
            catch (Exception e) { Log.Error("SyncData.arm_sutlerrow", e); }
            // m109-przesluch: pamiec band z bitew gracza, uzycia doby i sumy przesluchan "1|pamiec|uzycia|sumy" - brak klucza = stary zapis (stan pusty); osobny try
            try
            {
                string interrogation = dataStore.IsSaving ? CampInterrogation.Export() : null;
                SaveText.Sync(dataStore, "arm_interrogation", ref interrogation);
                if (dataStore.IsLoading) CampInterrogation.Import(interrogation);
            }
            catch (Exception e) { Log.Error("SyncData.arm_interrogation", e); }
            // R3-05e: rok urodzaju (doba losowania, siatka i wezly pola pogody) - brak klucza = stary zapis (x1 do nastepnego okna zniw); osobny try
            try
            {
                string harvestYear = dataStore.IsSaving ? HarvestYear.Export() : null;
                SaveText.Sync(dataStore, "arm_harvest_year", ref harvestYear);
                if (dataStore.IsLoading) HarvestYear.Import(harvestYear);
            }
            catch (Exception e) { Log.Error("SyncData.arm_harvest_year", e); }
            // R3-05f: dlug stad wsi po uboju jesiennym - brak klucza = stary zapis (bez dlugu); starsza DLL ignoruje klucz (dlug przepada - zwierzeta zostaja na targu)
            try
            {
                string herdCull = dataStore.IsSaving ? HerdCull.Export() : null;
                SaveText.Sync(dataStore, "arm_herd_cull", ref herdCull);
                if (dataStore.IsLoading) HerdCull.Import(herdCull);
            }
            catch (Exception e) { Log.Error("SyncData.arm_herd_cull", e); }
            // 110 (zawor kasy zamku): dar startowy w kasach zamkow rozliczony raz na kampanie; brak klucza = stary zapis (o przycieciu
            // decyduje wiek kampanii). Osobny try PRZED reszta, jak przy zapasie startowym: wyjatek innego klucza nie moze zgubic tej flagi -
            // zgubiona flaga to drugie przyciecie, ktore zdjeloby z kas zamkow prawdziwy doplyw
            try
            {
                string castlePurse = CastlePurse.Export();
                SaveText.Sync(dataStore, "arm_castlepurse", ref castlePurse);
                if (dataStore.IsLoading) CastlePurse.Import(castlePurse);
            }
            catch (Exception e) { Log.Error("SyncData.CastlePurse", e); }
            // E5/A1 (111'): dar startowy w kasach miast rozliczony raz na kampanie (arm_townpurse: "done" | "on:<wiek>" | ""); brak klucza = stary zapis
            // (o przycieciu decyduje wiek kampanii). Osobny try PRZED glownym blokiem - zgubiona flaga to drugie przyciecie prawdziwego doplywu kas miast
            try
            {
                string townPurse = TownPurse.Export();
                SaveText.Sync(dataStore, "arm_townpurse", ref townPurse);
                if (dataStore.IsLoading) TownPurse.Import(townPurse);
            }
            catch (Exception e) { Log.Error("SyncData.TownPurse", e); }
            // E5/B1 (173): kiesy ludu (W/R/P, gmina, podzamcze, kredyt dworu), kalibracja D1 i flagi zapasow startowych - wlasny try PRZED glownym blokiem
            // (zgubiona kiesa = zloto znikajace z sumy swiata; starszy DLL ignoruje klucz - cofniecie DLL po B1 w trwajacej grze = utrata kies, CHANGELOG)
            try
            {
                string townFolk = dataStore.IsSaving ? TownFolk.Export() : null;
                SaveText.Sync(dataStore, "arm_townfolk", ref townFolk);
                if (dataStore.IsLoading) TownFolk.Import(townFolk);
            }
            catch (Exception e) { Log.Error("SyncData.TownFolk", e); }
            // obieg2 (TownFolk.Guard.cs / TownFolk.Loans.cs): dlugi rodow u ludu (rod + osada), zakazy po niesplacie, mnoznik straznika obiegu i doplyw EMA - wlasny try
            // (zgubiony dlug = zloto u glow rodow bez roszczenia ludu; starszy DLL ignoruje klucz - cofniecie DLL w trakcie gry = dlugi przepadaja, CHANGELOG)
            try
            {
                string obieg2 = dataStore.IsSaving ? TownFolk.ExportObieg2() : null;
                SaveText.Sync(dataStore, "arm_obieg2", ref obieg2);
                if (dataStore.IsLoading) TownFolk.ImportObieg2(obieg2);
            }
            catch (Exception e) { Log.Error("SyncData.Obieg2", e); }
            // N4 (noc 16.10): pula wyrobow miast wykonanych, a jeszcze niesprzedanych (doplata do 35% przy zejsciu z polki) - wlasny try; brak klucza = pula od zera
            try
            {
                string craftSale = dataStore.IsSaving ? TownFolk.ExportCraftSale() : null;
                SaveText.Sync(dataStore, "arm_craft_sale", ref craftSale);
                if (dataStore.IsLoading) TownFolk.ImportCraftSale(craftSale);
            }
            catch (Exception e) { Log.Error("SyncData.arm_craft_sale", e); }
            // korona2 (TownFolk.CrownLoans.cs): dlugi koron u miast i mieszczan (krolestwo + osada + rodzaj), zakazy po niesplacie, srednie wplywy - wlasny try
            // (starszy DLL ignoruje klucz - cofniecie DLL w trakcie gry = dlugi przepadaja, zloto zostaje w skarbcach, CHANGELOG)
            try
            {
                string korona2 = dataStore.IsSaving ? TownFolk.ExportKorona2() : null;
                SaveText.Sync(dataStore, "arm_korona2", ref korona2);
                if (dataStore.IsLoading) TownFolk.ImportKorona2(korona2);
            }
            catch (Exception e) { Log.Error("SyncData.Korona2", e); }
            // DS (F1, daniny sredniowieczne wsi): ksiega zaleglosci czynszu wsi, pierwsza doba DS (rampa 28 dob), udzial korony przy wozie przed wplywem dnia - wlasny try;
            // brak klucza (stary zapis, Ironman Jeffa) = zera: rampa od pierwszej doby z nowa DLL; starszy DLL ignoruje klucz (zaleglosci przepadaja, zloto zostaje w kiesach)
            try
            {
                string daniny = dataStore.IsSaving ? DsVillage.Export() : null;
                SaveText.Sync(dataStore, "arm_daniny", ref daniny);
                if (dataStore.IsLoading) DsVillage.Import(daniny);
            }
            catch (Exception e) { Log.Error("SyncData.arm_daniny", e); }
            // DS (F3, daniny miast, zamkow i korony): okno pomiaru i firmy (zaleglosci, dziesiata kas), nadania wojenne krolestw, dziesiata ludu - wlasny try; brak klucza
            // (stary zapis, Ironman Jeffa) = zera: przy wlaczonym Ds Towns okno pomiaru od pierwszej doby; starszy DLL ignoruje klucz (zaleglosci i wymiar przepadaja, zloto zostaje)
            try
            {
                string miasta = dataStore.IsSaving ? DsTowns.Export() : null;
                SaveText.Sync(dataStore, "arm_daniny_miasta", ref miasta);
                if (dataStore.IsLoading) DsTowns.Import(miasta);
            }
            catch (Exception e) { Log.Error("SyncData.arm_daniny_miasta", e); }
            // ds-akumulator (recenzja, miekki start DsHoardDue): doba poczatku rampy daniny od skarbca ludu - wlasny try; brak klucza (stary zapis, Ironman Jeffa) = -1: rampa od
            // pierwszej doby z kluczem przy firmach F3 (klucz od ustalenia firm - rampa F3 co do doby); starszy DLL ignoruje klucz (wtedy danina i tak nie biegnie)
            try
            {
                string dsakum = dataStore.IsSaving ? TownFolk.ExportDsAkum() : null;
                SaveText.Sync(dataStore, "arm_dsakum", ref dsakum);
                if (dataStore.IsLoading) TownFolk.ImportDsAkum(dsakum);
            }
            catch (Exception e) { Log.Error("SyncData.arm_dsakum", e); }
            // unikaty ROT: kopie startowe zdjete raz na kampanie (wpis 62) - 177-2: we wlasnym try (krytyka 177 pkt 6: zgubiona flaga = druga
            // czystka startowa; od 177 omija ona stal valyrianska, ale inne unikaty sprzedane na polke zdjelaby znowu)
            try
            {
                string uniq = UniqueSpoils.Export();
                SaveText.Sync(dataStore, "arm_uniq_init", ref uniq);
                if (dataStore.IsLoading) UniqueSpoils.Import(uniq);
            }
            catch (Exception e) { Log.Error("SyncData.arm_uniq_init", e); }
            // 177-2: stal valyrianska - znacznik (zapis z 177) i rejestr (ile sztuk kazdego wzoru jest w swiecie), kazdy klucz we wlasnym try;
            // znacznik bez rejestru = bezpieczny domysl przy starcie sesji (nic nie zamieniane)
            try { string vm = dataStore.IsSaving ? ValyrianBlades.ExportMark() : null; SaveText.Sync(dataStore, "arm_vs_mark", ref vm); if (dataStore.IsLoading) ValyrianBlades.ImportMark(vm); }
            catch (Exception e) { Log.Error("SyncData.arm_vs_mark", e); }
            try { string vr = dataStore.IsSaving ? ValyrianBlades.Export() : null; SaveText.Sync(dataStore, "arm_vs_registry", ref vr); if (dataStore.IsLoading) ValyrianBlades.Import(vr); }
            catch (Exception e) { Log.Error("SyncData.arm_vs_registry", e); }
            // 177-3: zlecenia u mistrzow Qohoru (klingi wejscia zyja tylko w tym napisie az do wydania) - wlasny try
            try { string qo = dataStore.IsSaving ? QohorMasters.Export() : null; SaveText.Sync(dataStore, "arm_qohor_orders", ref qo); if (dataStore.IsLoading) QohorMasters.Import(qo); }
            catch (Exception e) { Log.Error("SyncData.arm_qohor_orders", e); }
            // E4/2 (190): ksiega ludzi - konto CIALA (pula Innych wedlug regionu) i flaga bilansu otwarcia; stary zapis bez klucza = pula od 0.
            // Osobny try PRZED glownym blokiem (recenzja E4/2 pkt 3, wzor arm_startstock/arm_castlepurse): wyjatek innego klucza nie moze zgubic
            // flagi - zgubiona flaga i wczytanie w pierwszych 2 dobach kampanii to drugi bilans otwarcia (616 cial z niczego)
            try { string people = dataStore.IsSaving ? PeopleBook.Export() : null; SaveText.Sync(dataStore, "arm_people", ref people); if (dataStore.IsLoading) PeopleBook.Import(people); }
            catch (Exception e) { Log.Error("SyncData.arm_people", e); }
            // sw-uchodzcy (noc 17/18.10): uchodzcy z najazdu w miastach - miasto konta, saldo kiesy W, migawki (format "v1|"); wlasny try, PO arm_people (konto UCHODZCY);
            // brak klucza (stary zapis) = pusto - przydzial w pierwszej dobie; starsza DLL klucz pomija
            try { string rr = dataStore.IsSaving ? RaidRefugees.Export() : null; SaveText.Sync(dataStore, "arm_raidref", ref rr); if (dataStore.IsLoading) RaidRefugees.Import(rr); }
            catch (Exception e) { Log.Error("SyncData.arm_raidref", e); }
            // E4/11 (193): ksiega wiosek pobocznych - n okregow, puste osady, stany wiosek (osobny klucz; Import tylko odklada napis do Resolve w OnSessionLaunched)
            try { string ds = dataStore.IsSaving ? VillageBook.Export() : null; SaveText.Sync(dataStore, "arm_district", ref ds); if (dataStore.IsLoading) VillageBook.Import(ds); }
            catch (Exception e) { Log.Error("SyncData.arm_district", e); }
            // R3-06c / R3-06d (p5-stada): stada wsi po rabunkach i baza ziemi okregow; brak klucza (stary zapis) = pelne stada, baza od pierwszego rachunku
            try { string hd = dataStore.IsSaving ? VillageHerds.Export() : null; SaveText.Sync(dataStore, "arm_herds", ref hd); if (dataStore.IsLoading) VillageHerds.Import(hd); }
            catch (Exception e) { Log.Error("SyncData.arm_herds", e); }
            // p6-okup (R3-03a): wsie po okupie chronione przed krolestwem napastnika; brak klucza = brak ochron (stary zapis, nowa kampania)
            try { string rt = dataStore.IsSaving ? RaidTribute.Export() : null; SaveText.Sync(dataStore, "arm_raidtribute", ref rt); if (dataStore.IsLoading) RaidTribute.Import(rt); }
            catch (Exception e) { Log.Error("SyncData.arm_raidtribute", e); }
            try { string tl = dataStore.IsSaving ? TiredLand.Export() : null; SaveText.Sync(dataStore, "arm_tiredland", ref tl); if (dataStore.IsLoading) TiredLand.Import(tl); }
            catch (Exception e) { Log.Error("SyncData.arm_tiredland", e); }
            // E6/K1 (211): obserwacje W (zgony koni / zgony jezdzcow w misjach) i pula koni weteranow w karczmach (Import odklada napis - przedmioty przy pierwszym uzyciu)
            try { string hf = dataStore.IsSaving ? HorseFate.Export() : null; SaveText.Sync(dataStore, "arm_horsefate", ref hf); if (dataStore.IsLoading) HorseFate.Import(hf); }
            catch (Exception e) { Log.Error("SyncData.arm_horsefate", e); }
            // C3 (Jeff 14.10): pula kompletow weteranow w karczmach (Import odklada napis - przedmioty przy pierwszym uzyciu); stary zapis bez klucza = pusta pula
            try { string vk = dataStore.IsSaving ? VetKit.Export() : null; SaveText.Sync(dataStore, "arm_vetkit", ref vk); if (dataStore.IsLoading) VetKit.Import(vk); }
            catch (Exception e) { Log.Error("SyncData.arm_vetkit", e); }
            // r4-przetop: skrzynka resztek przetopu kuzni gracza (tysiaczne sztaby 6 gatunkow, format "1|"); wlasny try; brak klucza (stary zapis) = pusta skrzynka
            try { string sc = dataStore.IsSaving ? SmeltScraps.Export() : null; SaveText.Sync(dataStore, "arm_smeltscraps", ref sc); if (dataStore.IsLoading) SmeltScraps.Import(sc); }
            catch (Exception e) { Log.Error("SyncData.arm_smeltscraps", e); }
            // m96-receptury: otwarte ladunki lnu i skory kuzni gracza w kg (SmithStock, format "1|id:kg"); wlasny try; brak klucza (stary zapis) = nic otwartego
            try { string st = dataStore.IsSaving ? SmithStock.Export() : null; SaveText.Sync(dataStore, "arm_smith_stock", ref st); if (dataStore.IsLoading) SmithStock.Import(st); }
            catch (Exception e) { Log.Error("SyncData.arm_smith_stock", e); }
            // GL-0: odniesienia skal "ludzie okregu" (srednia wies, sredni rynek, srednie podzamcze) i zamrozone przypisanie wsi do targu (format "1|"); wlasny try;
            // brak klucza (stary zapis), inny format albo blad = kalibracja przy pierwszym spisie po wczytaniu (PeopleScale.Import)
            try { string ps = dataStore.IsSaving ? PeopleScale.Export() : null; SaveText.Sync(dataStore, "arm_peoplescale", ref ps); if (dataStore.IsLoading) PeopleScale.Import(ps); }
            catch (Exception e) { Log.Error("SyncData.arm_peoplescale", e); }
            // GL-1 (P1): g typow wsi i lasu z kalibracji (z srednia wsia uslugi skal - kontrola zgodnosci) i stan "regula czynna" (format "1|"); wlasny try;
            // brak klucza (stary zapis albo kampania bez reguly), inny format albo blad = g od nowa przy pierwszej dobie z kluczem (VillageByPeople.Import)
            try { string vp = dataStore.IsSaving ? VillageByPeople.Export() : null; SaveText.Sync(dataStore, "arm_villagepeople", ref vp); if (dataStore.IsLoading) VillageByPeople.Import(vp); }
            catch (Exception e) { Log.Error("SyncData.arm_villagepeople", e); }
            // GL-2: flaga zlota startowego wedlug ludzi ("1|stan|dzien"); wlasny try; brak klucza (zapis sprzed paczki) = kampania w toku - zloto startowe nigdy
            try { string pm = dataStore.IsSaving ? PeopleMarket.Export() : null; SaveText.Sync(dataStore, "arm_peoplemarket", ref pm); if (dataStore.IsLoading) PeopleMarket.Import(pm); }
            catch (Exception e) { Log.Error("SyncData.arm_peoplemarket", e); }
            // GL-5 (P5): stan wsi lnianych kampanii "1|stan|dzien" - brak klucza (stary zapis) = kampania w toku, bez podmian lnu (GoodsRates.Import)
            try { string gr = dataStore.IsSaving ? GoodsRates.Export() : null; SaveText.Sync(dataStore, "arm_goodsrates", ref gr); if (dataStore.IsLoading) GoodsRates.Import(gr); }
            catch (Exception e) { Log.Error("SyncData.arm_goodsrates", e); }
            // ZC1 (GoodsPaths): starty przejsc 60 dob (towary uboczne BK, jedwab, ruda srebra, deski, woz, wieloryb, piwo), ladunki gliny i wapna kampanii (tylko zaczeta
            // z kluczem), marmur do rozdania, miod pitny drwali i miast z wczoraj - brak klucza = stary zapis
            try { string zc1 = dataStore.IsSaving ? GoodsPaths.Export() : null; SaveText.Sync(dataStore, "arm_zc1", ref zc1); if (dataStore.IsLoading) GoodsPaths.Import(zc1); }
            catch (Exception e) { Log.Error("SyncData.arm_zc1", e); }
            // B6: stan winnic i gajow kampanii "1|stan|dzien" - brak klucza (stary zapis) = kampania w toku, bez podmian (VineOlive.Import)
            try { string vo = dataStore.IsSaving ? VineOlive.Export() : null; SaveText.Sync(dataStore, "arm_vineolive", ref vo); if (dataStore.IsLoading) VineOlive.Import(vo); }
            catch (Exception e) { Log.Error("SyncData.arm_vineolive", e); }
            // r4-mur: danina zbozowa Muru - wozy w drodze i liczniki od poczatku (arm_mur_danina); bandy najazdu Wolnych Ludzi (arm_mur_najazdy). Wlasne try;
            // brak klucza (stary zapis, Ironman Jeffa) = pusto; starszy DLL ignoruje klucze (zboze w drodze przepada, bandy zostaja zwyklymi bandami)
            try { string md = dataStore.IsSaving ? WallTribute.Export() : null; SaveText.Sync(dataStore, "arm_mur_danina", ref md); if (dataStore.IsLoading) WallTribute.Import(md); }
            catch (Exception e) { Log.Error("SyncData.arm_mur_danina", e); }
            try { string mn = dataStore.IsSaving ? FreeFolkRaids.Export() : null; SaveText.Sync(dataStore, "arm_mur_najazdy", ref mn); if (dataStore.IsLoading) FreeFolkRaids.Import(mn); }
            catch (Exception e) { Log.Error("SyncData.arm_mur_najazdy", e); }
            // fix-mur: wynik marszu band najazdu od poczatku (doszly / zatrzymane i przez kogo) - osobny klucz PO arm_mur_najazdy (Import zeruje, ImportGoal czyta);
            // brak klucza (zapis sprzed fix-mur, Ironman Jeffa) = zera; starszy DLL klucz pomija
            try { string mc = dataStore.IsSaving ? FreeFolkRaids.ExportGoal() : null; SaveText.Sync(dataStore, "arm_mur_cel", ref mc); if (dataStore.IsLoading) FreeFolkRaids.ImportGoal(mc); }
            catch (Exception e) { Log.Error("SyncData.arm_mur_cel", e); }
            try
            {
                dataStore.SyncData("arm_condition", ref _condition);
                dataStore.SyncData("arm_projects", ref _projects);
                dataStore.SyncData("arm_player_stock", ref _playerStock);
                StockVersion++;
                dataStore.SyncData("arm_ammo_refund_v1", ref _ammoRefundDone);
                dataStore.SyncData("arm_lore_purge_v1", ref _lorePurged);
                if (dataStore.IsSaving) _armoryWear = BuildArmoryWearSnapshot();
                dataStore.SyncData("arm_armory_wear", ref _armoryWear);
                if (dataStore.IsLoading && _armoryWear != null && _armoryWear.Count > 0) _wearRestorePending = true;
                dataStore.SyncData("arm_smith_helper", ref SmithHelperId);
                dataStore.SyncData("arm_uniq_lore", ref UniqueLore);
                dataStore.SyncData("arm_restitution_0831", ref _restitution0831);
                dataStore.SyncData("arm_orders", ref Orders.Board);
                dataStore.SyncData("arm_order_cooldowns", ref Orders.Cooldowns);
                // dniowka w kuzni MUSI przezyc save/load - inaczej po wczytaniu
                // gra znow kaze placic 25/h za oplacona juz dobe
                string daypass = DayPass.Export();
                SaveText.Sync(dataStore, "arm_daypass", ref daypass);
                if (dataStore.IsLoading) DayPass.Import(daypass);
                string nightrest = NightRest.Export();
                SaveText.Sync(dataStore, "arm_nightrest", ref nightrest);
                if (dataStore.IsLoading) NightRest.Import(nightrest);
                // T10: ksiega snu partii AI (dlugi, odpoczynek doby, sen ciagly) - stary zapis bez klucza = AI bez dlugu
                string nightAi = NightRest.ExportAi(dataStore.IsSaving);
                SaveText.Sync(dataStore, "arm_nightrest_ai", ref nightAi);
                if (dataStore.IsLoading) NightRest.ImportAi(nightAi);
                // R3-08c / R3-08a (20.10): marsz forsowny gracza i dzien odpoczynku armii - stary zapis bez kluczy = stan zerowy
                try { string fm = dataStore.IsSaving ? ForcedMarch.Export() : null; SaveText.Sync(dataStore, "arm_forcedmarch", ref fm); if (dataStore.IsLoading) ForcedMarch.Import(fm); }
                catch (Exception e) { Log.Error("SyncData.arm_forcedmarch", e); }
                try { string ar = dataStore.IsSaving ? ArmyRestDay.Export() : null; SaveText.Sync(dataStore, "arm_armyrest", ref ar); if (dataStore.IsLoading) ArmyRestDay.Import(ar); }
                catch (Exception e) { Log.Error("SyncData.arm_armyrest", e); }
                // ludnosc krain: ludzi na punkt hearth/dobrobytu (Jeff 04.10)
                string popk = PopulationLaw.Export();
                SaveText.Sync(dataStore, "arm_population", ref popk);
                if (dataStore.IsLoading) PopulationLaw.Import(popk);
                // prawo wyrzutkow: pula ludzi w regionach (Jeff 04.10)
                string outlaws = OutlawLaw.Export();
                SaveText.Sync(dataStore, "arm_outlaws", ref outlaws);
                if (dataStore.IsLoading) OutlawLaw.Import(outlaws);
                // klimat Westeros: historia por roku (Jeff 04.10)
                // unikaty ROT: kopie startowe zdjete raz na kampanie (wpis 62)
                // dorobek stuleci: zbrojownie i zapas kupiecki raz na kampanie (wpis 79)
                // sakiewki ludzi (wpis 84)
                string purse = MenPurse.Export();
                SaveText.Sync(dataStore, "arm_menpurse", ref purse);
                if (dataStore.IsLoading) MenPurse.Import(purse);
                // K1 (przeglad, A3): ile z sakiewek ludzie odlozyli na lepszy sprzet (stary zapis bez klucza - zero, jak dotad)
                try
                {
                    string saved = MenPurse.ExportSaved();
                    SaveText.Sync(dataStore, "arm_mensaved", ref saved);
                    if (dataStore.IsLoading) MenPurse.ImportSaved(saved);
                }
                catch (Exception e) { Log.Error("SyncData.MenSaved", e); }
                // 150: odziez wojska - potrzeba czekajaca na zakup (partie i zalogi)
                string cloth = ArmyClothing.Export();
                SaveText.Sync(dataStore, "arm_armyclothing", ref cloth);
                if (dataStore.IsLoading) ArmyClothing.Import(cloth);
                // tarcza zoldu: znaczniki zoldu w kasach miast (paczka zold; puste, gdy tarcza wylaczona)
                string wagehold = SoldierPay.ExportHeld();
                SaveText.Sync(dataStore, "arm_wagehold", ref wagehold);
                if (dataStore.IsLoading) SoldierPay.ImportHeld(wagehold);
                string rk = RecruitKit.Export();
                SaveText.Sync(dataStore, "arm_recruitkit", ref rk);
                if (dataStore.IsLoading) RecruitKit.Import(rk);
                string aiw = AiWear.Export();
                SaveText.Sync(dataStore, "arm_aiwear", ref aiw);
                if (dataStore.IsLoading) AiWear.Import(aiw);
                string cold = ColdStart.Export();
                SaveText.Sync(dataStore, "arm_coldstart", ref cold);
                if (dataStore.IsLoading) ColdStart.Import(cold);
                // E8/T (263): wodzowie partii z dorobkiem startowym pierwszych dob nowej kampanii (po 3. dobie pusty; stary zapis bez sekcji = pusty zbior)
                string coldLate = ColdStart.ExportLate();
                SaveText.Sync(dataStore, "arm_e8_coldlate", ref coldLate);
                if (dataStore.IsLoading) ColdStart.ImportLate(coldLate);
                // E8/D1 (256): kwoty nagrod zamrozone przy przyjeciu zadan w toku (wersja "1|"; stary zapis bez sekcji = pusty slownik - kwota liczona przy wyplacie)
                string questPay = QuestPay8.Export();
                SaveText.Sync(dataStore, "arm_e8_questpay", ref questPay);
                if (dataStore.IsLoading) QuestPay8.Import(questPay);
                string climate = WesterosClimate.Export();
                SaveText.Sync(dataStore, "arm_climate", ref climate);
                if (dataStore.IsLoading) WesterosClimate.Import(climate);
                // Bank Zelazny: dlugi rodow i kapital Banku (Jeff 04.10)
                string bank = IronBank.Export();
                SaveText.Sync(dataStore, "arm_ironbank", ref bank);
                if (dataStore.IsLoading) IronBank.Import(bank);
                string glut = MarketGlut.Export();
                SaveText.Sync(dataStore, "arm_glut", ref glut);
                if (dataStore.IsLoading) MarketGlut.Import(glut);
                // wiedza luczarska: odkryte wzory lukow/kusz i punkty nauki
                string lore = RangedLore.Export();
                SaveText.Sync(dataStore, "arm_rangedlore", ref lore);
                if (dataStore.IsLoading) { RangedLore.Import(lore); LoreSeedFix.Snapshot("wczytanie"); }   // m96-wzory: tiery sprzed OnSessionLaunched (ROT-RBM zmienia luki po nas)
                // ksiega legend: raz wykuta legenda nigdy nie powstaje po raz drugi
                var legends = Legends;
                dataStore.SyncData("arm_legends", ref legends);
                if (dataStore.IsLoading && legends != null && !ReferenceEquals(legends, Legends))
                {
                    Legends.Clear();
                    Legends.AddRange(legends);
                }
                if (_projects == null) _projects = new List<string>();
                if (_condition == null) _condition = new List<string>();
                if (_playerStock == null) _playerStock = new Dictionary<string,int>();
            }
            catch (Exception e) { Log.Error("SyncData", e); }
            // zapis-d4: kolekcje zapisywane wprost przez gre (kontenery) - liczba wpisow do linii "Zapis (D4)"; tylko pomiar, wlasny try
            if (dataStore.IsSaving)
                try
                {
                    SaveSize.NoteList("arm_condition", _condition); SaveSize.NoteList("arm_projects", _projects); SaveSize.NoteDict("arm_player_stock", _playerStock);
                    SaveSize.NoteList("arm_armory_wear", _armoryWear); SaveSize.NoteList("arm_orders", Orders.Board); SaveSize.NoteList("arm_order_cooldowns", Orders.Cooldowns);
                    SaveSize.NoteList("arm_uniq_lore", UniqueLore); SaveSize.NoteList("arm_legends", Legends);
                }
                catch (Exception e) { Log.Error("SaveSize.Note", e); }
            if (dataStore.IsSaving) GearState.SaveLine();   // m110-stan: linia "Stany sprzetu (m110): zapis" - tylko pod flaga testu stanu (zgodnosc po wczytaniu)
            // m110-stan krok 2 (CompanionGearWear): ksiegi dokladnej wytrzymalosci sprzetu towarzyszy - osobny klucz; zapis tylko przy kluczu i ksiegach (inaczej klucza
            // w zapisie nie ma); stary zapis bez klucza = ksiegi puste (start od stanu); DLL 104 klucz pomija; arm_condition gracza bez zmian formatu
            try
            {
                string heroBook = dataStore.IsSaving ? HeroBook.Export() : null;
                if (dataStore.IsLoading || heroBook != null) SaveText.Sync(dataStore, "arm_condition_heroes", ref heroBook);
                if (dataStore.IsLoading) HeroBook.Import(heroBook);
            }
            catch (Exception e) { Log.Error("SyncData.arm_condition_heroes", e); }
            // ZB-1 (noc 17/18.10): rozbicie ksiegi gracza na egzemplarze (id|mod -> ile) - wlasny try, PO arm_player_stock (przyciecie do ksiegi id); brak klucza
            // (stary zapis) = puste rozbicie i migracja przy pierwszym porzadku; starsza DLL klucz pomija; klucz ArmouryBookPerModifier wylaczony - zapis pusty
            try
            {
                string bm = dataStore.IsSaving ? ExportStockMod() : null;
                SaveText.Sync(dataStore, "arm_player_stock_mod", ref bm);
                if (dataStore.IsLoading) ImportStockMod(bm);
            }
            catch (Exception e) { Log.Error("SyncData.arm_player_stock_mod", e); }
            // paczka 169: dochod staly rodow D (pierscien 28 dob, klucz Clan.StringId) - osobny try, dane tylko do logu (166/168 dopiero beda ich uzywac);
            // brak klucza (stary zapis) = pusta ksiega, start D = G/60 w pierwszym Daily
            try
            {
                string ci = ClanIncomeBook.Export();
                SaveText.Sync(dataStore, "arm_clanincome", ref ci);
                if (dataStore.IsLoading) ClanIncomeBook.Import(ci);
            }
            catch (Exception e) { Log.Error("SyncData.ClanIncome", e); }
            // f19-e3 (przeglad 18.10, uwagi 1 i 2): pamiec D stalego miedzy sesjami - na rod srednia 28 dob zatrzymanych wedlug potrzeby (f6/f7) i ziemia D stalego z chwili
            // zapisu (pierscienie czesci D zyja tylko w sesji); wlasny try, Export tylko przy zapisie; brak klucza (zapis sprzed paczki) = pusto, pierwsza doba jak dotad
            try { string sm = dataStore.IsSaving ? ClanIncomeBook.ExportStableMem() : null; SaveText.Sync(dataStore, "arm_stabled", ref sm); if (dataStore.IsLoading) ClanIncomeBook.ImportStableMem(sm); }
            catch (Exception e) { Log.Error("SyncData.arm_stabled", e); }
            // zapas kowali miast z napraw u kwatermistrza Spoils (MendMaterial: reszty calych sztuk materialu zdjetych z polki, kg i wartosc);
            // osobny try - wyjatek innego klucza go nie gubi; brak klucza (stary zapis) = pusty zapas
            try
            {
                string mend = MendMaterial.Export();
                SaveText.Sync(dataStore, "arm_mendstock", ref mend);
                if (dataStore.IsLoading) MendMaterial.Import(mend);
            }
            catch (Exception e) { Log.Error("SyncData.MendStock", e); }
            // E6/N1b: ulamki materialu napraw wlasnymi rekami (MendMaterial._frac); stary zapis bez klucza = pusta pamiec
            try { string mf = dataStore.IsSaving ? MendMaterial.ExportFrac() : null; SaveText.Sync(dataStore, "arm_mend_frac", ref mf); if (dataStore.IsLoading) MendMaterial.ImportFrac(mf); }
            catch (Exception e) { Log.Error("SyncData.MendFrac", e); }
            // 171: zamowienia zamkow w drodze i zbrojownie zalog (DTE ich nie zapisuje) - kazdy klucz we wlasnym try; Export tylko przy zapisie
            // sklad7 (scalenie K1): JEDEN zapis zbrojowni zalog - klucz "arm_garrisonarmory" prowadzi tylko GarrisonArmory (format v1|); K1c zapisywal
            // ten sam klucz drugi raz (GarrisonKit.ExportArmories, inny format) - usuniete; GarrisonArmory.Restore czyta tez dawny format K1c
            // (krytyka 8: przy wczytaniu Export przechodzilby osady i zbrojownie DTE w trakcie deserializacji); brak klucza = null
            try { string gc = dataStore.IsSaving ? GarrisonCarts.Export() : null; SaveText.Sync(dataStore, "arm_garrisoncarts", ref gc); if (dataStore.IsLoading) GarrisonCarts.Import(gc); }
            catch (Exception e) { Log.Error("SyncData.arm_garrisoncarts", e); }
            try { string ga = dataStore.IsSaving ? GarrisonArmory.Export() : null; SaveText.Sync(dataStore, "arm_garrisonarmory", ref ga); if (dataStore.IsLoading) GarrisonArmory.Import(ga); }
            catch (Exception e) { Log.Error("SyncData.arm_garrisonarmory", e); }
            // E6/R3 (215): wozy kupcow w drodze (hurt miedzy osadami, TradeCarts) - wlasny try; brak klucza = stary zapis (pusto); rozwiazanie w OnSessionLaunched
            try { string tc = dataStore.IsSaving ? TradeCarts.Export() : null; SaveText.Sync(dataStore, "arm_tradecarts", ref tc); if (dataStore.IsLoading) TradeCarts.Import(tc); }
            catch (Exception e) { Log.Error("SyncData.arm_tradecarts", e); }
            // 174.0b: robota w toku warsztatow zbrojnych (sztuki zaczete, dlug ulamkowy surowca) i zamowienia - kazdy klucz we wlasnym try; brak klucza = stary zapis (pusto)
            try { string ws = dataStore.IsSaving ? WorkshopLaw.Export() : null; SaveText.Sync(dataStore, "arm_workshops", ref ws); if (dataStore.IsLoading) WorkshopLaw.Import(ws); }
            catch (Exception e) { Log.Error("SyncData.arm_workshops", e); }
            try { string un = dataStore.IsSaving ? SupplyDemand.ExportOrders() : null; SaveText.Sync(dataStore, "arm_unmet", ref un); if (dataStore.IsLoading) SupplyDemand.ImportOrders(un); }
            catch (Exception e) { Log.Error("SyncData.arm_unmet", e); }
            // 174.2: kontrakty surowca w drodze (karawana, cel, zrodlo, surowiec, ilosc) - zawsze, takze przy wylaczonym wylaczniku (po wczytaniu karawana zwolniona)
            try { string mo = dataStore.IsSaving ? MaterialOrders.Export() : null; SaveText.Sync(dataStore, "arm_matorders", ref mo); if (dataStore.IsLoading) MaterialOrders.Import(mo); }
            catch (Exception e) { Log.Error("SyncData.arm_matorders", e); try { if (dataStore.IsLoading) MaterialOrders.ImportFailed(); } catch { } }   // recenzja 174: linia, ze kontrakty pominiete
            // 174b.6: zlom ze starego nadmiaru - rozgrzewka (doby pomiaru), ostatnia doba, srednie zakupow koszykow, ulamki skupu i rudy; Export tylko przy zapisie
            // (z probnym odczytem), brak klucza = zapis sprzed 174b (rozgrzewka od zera, jak dotad); rozwiazanie w OnSessionLaunched
            try { string sc = dataStore.IsSaving ? ArmsScrap.Export() : null; SaveText.Sync(dataStore, "arm_scrap", ref sc); if (dataStore.IsLoading) ArmsScrap.Import(sc); }
            catch (Exception e) { Log.Error("SyncData.arm_scrap", e); try { if (dataStore.IsLoading) ArmsScrap.ImportFailed(); } catch { } }
            // 165: migawka skarbcow z zamkniecia korony (wplywy dnia) i dlugi reparacji korona -> korona - wlasny try; brak klucza = stary zapis (bez dlugow,
            // pierwsza doba bez migawki - wplywy dnia tylko z naszych licznikow)
            try { string ci165 = dataStore.IsSaving ? CrownIncome.Export() : null; SaveText.Sync(dataStore, "arm_crown165", ref ci165); if (dataStore.IsLoading) CrownIncome.Import(ci165); }
            catch (Exception e) { Log.Error("SyncData.arm_crown165", e); }
            // 166: D uzyte wczoraj, doby ponad pulapem i niewydany przydzial sprzetu na rod, zold zalog w wojnie na twierdze; 162m: znaczniki tarczy dworu
            // w kasach miast - kazdy klucz we wlasnym try; brak klucza = stary zapis (D = G/60, zalogi wojenne ze stanu pierwszej doby, bez znacznikow)
            try { string cb = dataStore.IsSaving ? ClanBudget.Export() : null; SaveText.Sync(dataStore, "arm_clanbudget", ref cb); if (dataStore.IsLoading) ClanBudget.Import(cb); }
            catch (Exception e) { Log.Error("SyncData.arm_clanbudget", e); }
            try { string ch = dataStore.IsSaving ? SoldierPay.ExportCourt() : null; SaveText.Sync(dataStore, "arm_courthold", ref ch); if (dataStore.IsLoading) SoldierPay.ImportCourt(ch); }
            catch (Exception e) { Log.Error("SyncData.arm_courthold", e); }
            // E5 decyzja 9 (GarrisonRecruitFee): kolejka sprzedajacych oplaty za werbunek zalog - wlasny try; brak klucza = stary zapis (saldo gry bez znanego sprzedajacego)
            try { string gf = dataStore.IsSaving ? GarrisonRecruitFee.Export() : null; SaveText.Sync(dataStore, "arm_garfee", ref gf); if (dataStore.IsLoading) GarrisonRecruitFee.Import(gf); }
            catch (Exception e) { Log.Error("SyncData.arm_garfee", e); }
            // 185: umowy kompanii najemnych AI (korona, kontrakt, ludzie, przeglad, doby niedoplaty) - wlasny try; brak klucza = nowe umowy od pierwszej doby
            try { string mc = dataStore.IsSaving ? MercContract.Export() : null; SaveText.Sync(dataStore, "arm_merc185", ref mc); if (dataStore.IsLoading) MercContract.Import(mc); }
            catch (Exception e) { Log.Error("SyncData.arm_merc185", e); }
            // M6a (raport roczny 13.10, wiersze 17-19): jeden klucz paczki - pan sprzed odejscia kompanii (M6a-1), doba ostatniego okupu osoby (M6a-2),
            // pokoje krolestw czekajace na uwolnienie jencow (M6a-3) - wlasny try; brak klucza (zapis sprzed paczki) = pamiec pusta
            try
            {
                string m6 = dataStore.IsSaving ? "v1|" + MercTenure.Export() + "|" + RansomRepeat.Export() + "|" + PeaceHold.Export() : null;
                SaveText.Sync(dataStore, "arm_m6a", ref m6);
                if (dataStore.IsLoading)
                {
                    var f6 = (m6 ?? "").Split('|');
                    bool v6 = f6.Length >= 4 && f6[0] == "v1";
                    MercTenure.Import(v6 ? f6[1] : null); RansomRepeat.Import(v6 ? f6[2] : null); PeaceHold.Import(v6 ? f6[3] : null);
                }
            }
            catch (Exception e) { Log.Error("SyncData.arm_m6a", e); }
            // 180: sluzba rodow (maska ostatnich 60 dob wojny), normy zalog na krolestwo i rodzaj twierdzy, doby przejecia twierdz - wlasny try; brak klucza (stary
            // zapis) = sluzba i normy od zera (rod bez dob wojny - warunek sluzby nie obowiazuje; twierdze "od dawna" w rekach rodu)
            try { string rt = dataStore.IsSaving ? CrownRents.Export() : null; SaveText.Sync(dataStore, "arm_rent180", ref rt); if (dataStore.IsLoading) CrownRents.Import(rt); }
            catch (Exception e) { Log.Error("SyncData.arm_rent180", e); }
            // 168: drabina dlugu (szczeble, kredyt wojenny w ksiedze, roszczenia - dlug zoldu, dlug wobec korony, okupy), srednie lupow rodow, dlug bez platnika -
            // wlasny try; brak klucza (stary zapis) = pusta ksiega drabiny (dlug Banku zostaje w arm_ironbank; bankruci Banku przechodza w zajecie pierwszego dnia)
            try { string dl = dataStore.IsSaving ? DebtLadder.Export() : null; SaveText.Sync(dataStore, "arm_debt168", ref dl); if (dataStore.IsLoading) DebtLadder.Import(dl); }
            catch (Exception e) { Log.Error("SyncData.arm_debt168", e); }
            // f8-pozyczka: pozyczki miejskie (roszczenia KTown drabiny 168 - wierzyciel rada osady, udzialy kies ludu i kasy kupcow) - wlasny try, PO arm_debt168
            // (DebtLadder.Import czysci ksiege), straznik rozmiaru (4000 roszczen, 600 000 znakow); brak klucza (stary zapis, Ironman Jeffa) = brak pozyczek miejskich;
            // starsza DLL klucza nie czyta (roszczenia KTown nie ida do arm_debt168)
            try { string tl = dataStore.IsSaving ? DebtLadder.ExportTown() : null; SaveText.Sync(dataStore, "arm_townloan", ref tl); if (dataStore.IsLoading) DebtLadder.ImportTown(tl); }
            catch (Exception e) { Log.Error("SyncData.arm_townloan", e); }
            // 168 dodatek: porozumienia wezwania do wojny placone przez korone (reszta ceny, czesc dnia) - wlasny try; brak klucza = brak porozumien w toku
            // (dlug portfela wezwania sprzed paczki skreslany pierwszego dnia - wezwany dostal go juz od gry)
            // 178: srednie wplywow koron (okup krola), wielcy jency u koron zdobywcow, nagrody zdobywcow do wyplaty, zaleglosci 1/9 - wlasny try; brak klucza = puste
            try { string rs = dataStore.IsSaving ? Ransom178.Export() : null; SaveText.Sync(dataStore, "arm_rans178", ref rs); if (dataStore.IsLoading) Ransom178.Import(rs); }
            catch (Exception e) { Log.Error("SyncData.arm_rans178", e); }
            try { string cw = dataStore.IsSaving ? CrownCallToWar.Export() : null; SaveText.Sync(dataStore, "arm_ctw168", ref cw); if (dataStore.IsLoading) CrownCallToWar.Import(cw); }
            catch (Exception e) { Log.Error("SyncData.arm_ctw168", e); }
            // 186: kredyty koron w Banku (dlug, oprocentowanie, najwiekszy dlug, okno 28 dob rat, zaleglosc), srednie podatku krolestw, strata Banku - wlasny try;
            // brak klucza (stary zapis) = bez dlugow koron, sredni podatek od zera (7 dob pomiaru przed pierwszym kredytem)
            try { string cr = dataStore.IsSaving ? CrownBorrow.Export() : null; SaveText.Sync(dataStore, "arm_crown186", ref cr); if (dataStore.IsLoading) CrownBorrow.Import(cr); }
            catch (Exception e) { Log.Error("SyncData.arm_crown186", e); }
            // f20-d3: zysk Banku do Braavos - koszt pozyczek rodow i koron, pula zysku, migawka kapitalu, pierscien pozyczek (rezerwa) - wlasny try;
            // brak klucza (stary zapis, klucz wylaczony) = start od zera w pierwszej dobie (koszt = dzisiejszy dlug, pula 0)
            try { string bd = dataStore.IsSaving ? IronBank.DvExport() : null; SaveText.Sync(dataStore, "arm_bankdiv", ref bd); if (dataStore.IsLoading) IronBank.DvImport(bd); }
            catch (Exception e) { Log.Error("SyncData.arm_bankdiv", e); }
            // musztra: zapas cwiczebny gracza, maska godzin ruchu gracza, liczniki zuzycia i zlom czekajacy na kowali, flaga zasilenia autotestu - wlasny try;
            // Export tylko przy zapisie; brak klucza (stary zapis) = pusty zapas (linia "Musztra: start ... z zapisu: brak klucza")
            try { string dr = dataStore.IsSaving ? Drill.Export() : null; SaveText.Sync(dataStore, "arm_drill", ref dr); if (dataStore.IsLoading) Drill.Import(dr); }
            catch (Exception e) { Log.Error("SyncData.arm_drill", e); }
            // E5/A3 (163): markietani - stan rozruchu (doba startu albo "bez rozruchu") - wlasny try; brak klucza (stary zapis) = rozruch rozstrzyga pierwsza doba
            try { string su = dataStore.IsSaving ? Sutlers.Export() : null; SaveText.Sync(dataStore, "arm_sutlers", ref su); if (dataStore.IsLoading) Sutlers.Import(su); }
            catch (Exception e) { Log.Error("SyncData.arm_sutlers", e); }
            // m96 (SavingsByNeed): cele oszczedzania partii i rozruch - wlasny try; brak klucza (stary zapis) = cele 0, rozruch rozstrzyga pierwsza doba
            try { string sbn = dataStore.IsSaving ? SavingsByNeed.Export() : null; SaveText.Sync(dataStore, "arm_m96sak", ref sbn); if (dataStore.IsLoading) SavingsByNeed.Import(sbn); }
            catch (Exception e) { Log.Error("SyncData.arm_m96sak", e); }
            // E6/M2 (208): flaga nowej kampanii dla wielbladow tylko w Qarthu - wlasny try; brak klucza (stary zapis) = stadniny jak w grze
            try { string cm = dataStore.IsSaving ? CamelLaw.Export() : null; SaveText.Sync(dataStore, "arm_e6_camels", ref cm); if (dataStore.IsLoading) CamelLaw.Import(cm); }
            catch (Exception e) { Log.Error("SyncData.arm_e6_camels", e); }
            // E6/R1 (214): wytop przy kopalni - flaga nowej kampanii, drewno w dymarkach i potrzeba doby kopalni - wlasny try; brak klucza (stary zapis) = ruda jak dotad
            try { string bl = dataStore.IsSaving ? Bloomery.Export() : null; SaveText.Sync(dataStore, "arm_e6_bloom", ref bl); if (dataStore.IsLoading) Bloomery.Import(bl); }
            catch (Exception e) { Log.Error("SyncData.arm_e6_bloom", e); }
            // f5-ruda (noc 18.10): kredyt kupcow na drewno dymarek - wlasny try, PO arm_e6_bloom (Bloomery.Reset); brak klucza (stary zapis, Ironman Jeffa) = bez dlugow; starsza DLL klucza nie czyta
            try { string fc = dataStore.IsSaving ? Bloomery.F5Export() : null; SaveText.Sync(dataStore, "arm_f5_woodcredit", ref fc); if (dataStore.IsLoading) Bloomery.F5Import(fc); }
            catch (Exception e) { Log.Error("SyncData.arm_f5_woodcredit", e); }
            // E6/W3 (221): regulator wsi - mnozniki, kubelki produkcji i skasowanych, stosy na granicach kubelkow - wlasny try; brak klucza (stary zapis) = pomiar od pierwszej doby
            try { string hv = dataStore.IsSaving ? HarvestValve.Export() : null; SaveText.Sync(dataStore, "arm_harvestvalve", ref hv); if (dataStore.IsLoading) HarvestValve.Import(hv); }
            catch (Exception e) { Log.Error("SyncData.arm_harvestvalve", e); }
            // recenzja T3 (T3-K2): zwierzeta rzezne regulatora w osobnym kluczu - PO arm_harvestvalve (Import czysci stan); DLL bez T3-K2 klucza nie zna (cofniecie DLL:
            // zwierzeta jak model); brak klucza (stary zapis, zapis starej DLL) = pomiar zwierzat od nowa
            try { string hz = dataStore.IsSaving ? HarvestValve.ExportBeasts() : null; SaveText.Sync(dataStore, "arm_harvestvalve_zw", ref hz); if (dataStore.IsLoading) HarvestValve.ImportBeasts(hz); }
            catch (Exception e) { Log.Error("SyncData.arm_harvestvalve_zw", e); }
            // E5/B4 (199, recenzja uwaga 1): targ przy zamku - wozy w drodze do zamku (rezerwacje jedzenia), znaczniki "byl w zamku", straz BK - wlasny try;
            // brak klucza (zapis sprzed paczki) = stan pusty; rozwiazanie na wozy w OnSessionLaunched (w SyncData partii jeszcze nie ma)
            try { string cm = dataStore.IsSaving ? CastleMarket.Export() : null; SaveText.Sync(dataStore, "arm_castlemarket", ref cm); if (dataStore.IsLoading) CastleMarket.Import(cm); }
            catch (Exception e) { Log.Error("SyncData.arm_castlemarket", e); }
            // E5/B6 (201): pula sakiewek weteranow w karczmach (id miasta = zloto) - wlasny try; brak klucza (zapis sprzed paczki) = pula pusta
            try { string vp = dataStore.IsSaving ? VetPurse.Export() : null; SaveText.Sync(dataStore, "arm_vetpurse", ref vp); if (dataStore.IsLoading) VetPurse.Import(vp); }
            catch (Exception e) { Log.Error("SyncData.arm_vetpurse", e); }
            // N7 (decyzja Jeffa 13.10): rozbici czekajacy w osadach zbiorki (rod, osada, doba, gotowi, typy, udzialy pochodzenia) - wlasny try; wersja formatu "1";
            // brak klucza (zapis sprzed paczki) = pula pusta; obiekty (rod, osada, typy) rozwiazuje pierwsza doba (StragglersRally.Resolve)
            try { string sr = dataStore.IsSaving ? StragglersRally.Export() : null; SaveText.Sync(dataStore, "arm_n7_rally", ref sr); if (dataStore.IsLoading) StragglersRally.Import(sr); }
            catch (Exception e) { Log.Error("SyncData.arm_n7_rally", e); }
            // okup zamiast szturmu (O1, P9): rozejmy (osada, frakcja, doba konca) i szturmy bez zdobycia trwajacych oblezen - wlasny klucz (nie arm_sack: jego Import
            // odrzuca nieznana wersje w calosci); wersja formatu "1"; brak klucza (zapis sprzed paczki) = bez rozejmow
            try { string sr = dataStore.IsSaving ? SiegeRansom.Export() : null; SaveText.Sync(dataStore, "arm_siege_ransom", ref sr); if (dataStore.IsLoading) SiegeRansom.Import(sr); }
            catch (Exception e) { Log.Error("SyncData.arm_siege_ransom", e); }
            // mury oblezen OB2 (SiegeWalls.cs): przerwy po odparciu szturmu trwajacych oblezen (osada, poczatek oblezenia, doba konca, odparcia) - wlasny klucz, wersja "1";
            // brak klucza (zapis sprzed paczki) = bez przerw
            try { string sw = dataStore.IsSaving ? SiegeWalls.Export() : null; SaveText.Sync(dataStore, "arm_siege_walls", ref sw); if (dataStore.IsLoading) SiegeWalls.Import(sw); }
            catch (Exception e) { Log.Error("SyncData.arm_siege_walls", e); }
            // lup miast L3: osady po nastepstwach oblezen z ostatnich 30 dob (okno lupu, pomiar, napisy) - wlasny try; wersja formatu "1"; brak klucza = pusto
            try { string sk = dataStore.IsSaving ? CitySack.Export() : null; SaveText.Sync(dataStore, "arm_sack", ref sk); if (dataStore.IsLoading) CitySack.Import(sk); }
            catch (Exception e) { Log.Error("SyncData.arm_sack", e); }
            try { string rk = dataStore.IsSaving ? CitySack.ExportRuins() : null; SaveText.Sync(dataStore, "arm_ruins", ref rk); if (dataStore.IsLoading) CitySack.ImportRuins(rk); }   // LB lupu miast: zgliszcza budowli
            catch (Exception e) { Log.Error("SyncData.arm_ruins", e); }
            // A14 / G20 (drobne2): doba po wczytaniu ze srodka doby - punkty budow tej doby i gotowka W5/W7 wczorajszego kroku (DayHeld.cs); wersja "1"; brak klucza = jak dotad
            try { string dh = dataStore.IsSaving ? DayHeld.Export() : null; SaveText.Sync(dataStore, "arm_dayheld", ref dh); if (dataStore.IsLoading) DayHeld.Import(dh); }
            catch (Exception e) { Log.Error("SyncData.arm_dayheld", e); }
            // TP (16.10, Jeff "miasto nie moglo zaplacic mi 1300"): dlugi osad wobec gracza (nagroda turnieju, zaklad, arena, plansza) i flaga wyrownania Seagard - wlasny try;
            // wersja "v1" (flaga D3 0-3); brak klucza (zapis sprzed zmiany) = bez dlugow i znacznik "zapis sprzed zmiany" - tylko wtedy wyrownanie Seagard moze dotyczyc; Reset przed SyncData (PlayerFees8.Reset)
            try { string tp = dataStore.IsSaving ? TourneyPay.Export() : null; SaveText.Sync(dataStore, "arm_tp_debts", ref tp); if (dataStore.IsLoading) TourneyPay.Import(tp); }
            catch (Exception e) { Log.Error("SyncData.arm_tp_debts", e); }
            // KA (16.10, karczmy): stan noclegu gracza (oplacony sen / wypoczeci do zmierzchu, miasto ceny) - wlasny try; wersja "v1"; brak klucza (zapis sprzed zmiany) = brak stanu;
            // Reset przed SyncData (konstruktor)
            try { string inn = dataStore.IsSaving ? TownInns.Export() : null; SaveText.Sync(dataStore, "arm_inns", ref inn); if (dataStore.IsLoading) TownInns.Import(inn); }
            catch (Exception e) { Log.Error("SyncData.arm_inns", e); }
            // ZC0 (16.10, zycie codzienne - pomiar na sucho): skala targu sigma ustalona RAZ i srednia dochodu stanow osad ("v2|sigma|dzien|id:dni:y0,...,y6;..." - SaveText w kawalkach; v1 czytany) - wlasny try; brak klucza (zapis sprzed paczki
            // albo kampania z wylaczonym kluczem) = sigma z MCM przy pierwszej dobie z kluczem; Reset przed SyncData (konstruktor)
            try { string zc = dataStore.IsSaving ? DailyLife.Export() : null; SaveText.Sync(dataStore, "arm_dailylife", ref zc); if (dataStore.IsLoading) DailyLife.Import(zc); }
            catch (Exception e) { Log.Error("SyncData.arm_dailylife", e); }
            // ZC2 (16.10, stany w ksiedze): udzialy stanow miast i wsi w milionowych i pamiec Dostatku co 7 dob ("v1|baza zasiewu|doby pamieci|id:p0..p5:zasiew:pochylenie:d0..d4;..."
            // - SaveText w kawalkach) - wlasny try; brak klucza (zapis sprzed paczki) albo nieznany format = zasiew z pasm x BK w pierwszej dobie pomiaru z kluczem
            // DailyLifeEstates; Reset przed SyncData (konstruktor, DailyLife.Reset)
            try { string ze = dataStore.IsSaving ? DailyLife.ExportEstates() : null; SaveText.Sync(dataStore, "arm_estates", ref ze); if (dataStore.IsLoading) DailyLife.ImportEstates(ze); }
            catch (Exception e) { Log.Error("SyncData.arm_estates", e); }
            // KS (kalibracja startu): "ks1|stan|kampania|doba wlaczenia|plany|pierwszy plan|crc ziemi|crc ukladu|sigma|okregi..." - po wlaczeniu zajecia blokow (stala kolejnosc,
            // "=" jak poprzedni blok) - SaveText w kawalkach; brak klucza (zapis sprzed paczki) albo inny format = Plan od nowa, produkcja jak dzis; Reset przed SyncData (konstruktor)
            try { string ks = dataStore.IsSaving ? StartCalibration.Export() : null; SaveText.Sync(dataStore, "arm_ks", ref ks); if (dataStore.IsLoading) StartCalibration.Import(ks); }
            catch (Exception e) { Log.Error("SyncData.arm_ks", e); }
            // ks-arm (noc 17/18.10): samo uzbrojenie KS - srednia ksiegi towarow (pomiar przez sesje), dojrzaly Plan od doby, twarde bramki 7 dob, krainy wrocone przez
            // bramke krainy (Z + base64 deflate, <= 64 KB) - brak klucza (stary zapis) = pomiar od zera; starsza DLL go pomija
            try { string ka = dataStore.IsSaving ? StartCalibration.ExportArm() : null; SaveText.Sync(dataStore, "arm_ks_arm", ref ka); if (dataStore.IsLoading) StartCalibration.ImportArm(ka); }
            catch (Exception e) { Log.Error("SyncData.arm_ks_arm", e); }
            // KS-R3 G1: wozy kupcow zywnosci (KS-H) w drodze - brak klucza = brak wozow (stary zapis)
            try { string kh = dataStore.IsSaving ? KsFoodCarts.Export() : null; SaveText.Sync(dataStore, "arm_ks_kupcy", ref kh); if (dataStore.IsLoading) KsFoodCarts.Import(kh); }
            catch (Exception e) { Log.Error("SyncData.arm_ks_kupcy", e); }
            // KS-R4 r4-kupcy H5: rachunki kredytu kupcow KS-H ("kk1|zrodlo~cel~zostalo~doba|...", najwyzej 64 KB) - brak klucza (stary zapis) = brak rachunkow; starsza DLL
            // klucza nie czyta; Reset przed SyncData (konstruktor -> KsFoodCarts.Reset -> KsFoodCredit.Reset)
            try { string kc = dataStore.IsSaving ? KsFoodCredit.Export() : null; SaveText.Sync(dataStore, "arm_daniny_kupcy", ref kc); if (dataStore.IsLoading) KsFoodCredit.Import(kc); }
            catch (Exception e) { Log.Error("SyncData.arm_daniny_kupcy", e); }
            // KS-R3 (recenzja runda 3): stan rundy 3 kalibracji (gradient krain, sterowanie, kx i tabela dob KS-H, srednie dowozu, podloga, wydatki wsi ...) w osobnym kluczu -
            // arm_ks zostaje w formacie ks3 (starsza DLL czyta go jak dotad i pomija ten klucz); brak klucza = stan rundy 3 od zera
            try { string k3 = dataStore.IsSaving ? StartCalibration.ExportR3() : null; SaveText.Sync(dataStore, "arm_ks_r3", ref k3); if (dataStore.IsLoading) StartCalibration.ImportR3(k3); }
            catch (Exception e) { Log.Error("SyncData.arm_ks_r3", e); }
            // sw-zajazdy (InnGossip): ksiega plotek - doba narodzin band i napady, wiesci z wojen (v1, straznik rozmiaru) - wlasny try; brak klucza (stary zapis) = bandy
            // "od dawna", wiesci od zera; starsza DLL klucza nie czyta. Klucz wylaczony - "" (nic). Reset przed SyncData (konstruktor)
            try { string pl = dataStore.IsSaving ? Rumours.Export() : null; SaveText.Sync(dataStore, "arm_rumours", ref pl); if (dataStore.IsLoading) Rumours.Import(pl); }
            catch (Exception e) { Log.Error("SyncData.arm_rumours", e); }
            // m98-plotki: kufle gracza, pijanstwo, kac i ksiega uslyszanych plotek - wlasny klucz, starsza DLL go pomija; brak klucza = trzezwy, nic nie uslyszane
            try { string dk = dataStore.IsSaving ? InnDrink.Export() : null; SaveText.Sync(dataStore, "arm_drink", ref dk); if (dataStore.IsLoading) InnDrink.Import(dk); }
            catch (Exception e) { Log.Error("SyncData.arm_drink", e); }
            // p5-kruki (R3-05c): kruki w drodze (wiesci wojenne z opoznieniem) - wlasny klucz, starsza DLL go pomija; brak klucza = brak krukow w drodze
            try { string rv = dataStore.IsSaving ? Ravens.Export() : null; SaveText.Sync(dataStore, "arm_ravens", ref rv); if (dataStore.IsLoading) Ravens.Import(rv); }
            catch (Exception e) { Log.Error("SyncData.arm_ravens", e); }
            // KS r4 (r4-ceny): stan cen i sterowania rynku (doba restartu srednich cen, czynnik popytu targu do 1.25, brak miast C1) - osobny klucz, starsza DLL go pomija;
            // brak klucza (stary zapis) = stan od zera (po D+14 srednie cen uznane za juz zerowane)
            try { string k4 = dataStore.IsSaving ? StartCalibration.ExportR4c() : null; SaveText.Sync(dataStore, "arm_ks_r4c", ref k4); if (dataStore.IsLoading) StartCalibration.ImportR4c(k4); }
            catch (Exception e) { Log.Error("SyncData.arm_ks_r4c", e); }
            // r4-koszyk (17.10 noc): wskazniki plac za brak rak (KsWageHands) i dokupiona podstawa koszyka miast z Planu (Ks Cheap Basket) - "k1|miasto:wskaznik:doba,...|miasto:z=v/...";
            // wlasny try; brak klucza (stary zapis, Ironman Jeffa) = wskazniki 1 i bez dokupionej podstawy do pierwszego Planu; starszy DLL ignoruje klucz
            try { string kk = dataStore.IsSaving ? KsWageHands.ExportAll() : null; SaveText.Sync(dataStore, "arm_ks_koszyk", ref kk); if (dataStore.IsLoading) KsWageHands.ImportAll(kk); }
            catch (Exception e) { Log.Error("SyncData.arm_ks_koszyk", e); }
            // f4-place-ludu (noc 17/18.10): wskazniki plac ponizej 1 (Ks Wage Hands Down) i stawka plac W wedlug koszyka (kappa, Ks Labour To Basket) - "f4|miasto:wskaznik,...|miasto:kappa,...";
            // PO arm_ks_koszyk (nadpisuje 1 z tamtego klucza); pisany tylko z danymi (wszystko 1 - bez klucza, zapis jak dotad); wlasny try, straznik rozmiaru; brak klucza (stary zapis, Ironman Jeffa) = wskazniki >= 1 i kappa 1; starsza DLL ignoruje klucz
            try { string kp = dataStore.IsSaving ? KsFolkWage.ExportAll() : null; if (!dataStore.IsSaving || kp != null) SaveText.Sync(dataStore, "arm_ks_place", ref kp); if (dataStore.IsLoading) KsFolkWage.ImportAll(kp); }
            catch (Exception e) { Log.Error("SyncData.arm_ks_place", e); }
            // f5-lud (noc 17/18.10): kappy dochodu klas ludu miast z KS wedlug koszyka (W2 - zarobek W poza placami z kas i od domow, R, P) - "f5|W2:miasto:k,...|R:...|P:...";
            // pisany tylko z danymi (wszystko 1 - bez klucza, zapis jak dotad); wlasny try, straznik rozmiaru (Import: 600 000 znakow, 4000 miast na klase); brak klucza
            // (stary zapis, Ironman Jeffa) = kappy 1 (schodza krokiem dobowym od pierwszej doby - bez skoku dochodu); starsza DLL ignoruje klucz
            try { string kd = dataStore.IsSaving ? KsFolkIncome.Export() : null; if (!dataStore.IsSaving || kd != null) SaveText.Sync(dataStore, "arm_ks_dochod", ref kd); if (dataStore.IsLoading) KsFolkIncome.Import(kd); }
            catch (Exception e) { Log.Error("SyncData.arm_ks_dochod", e); }
            // inwest (noc 17/18.10): zajecia wiosek pobocznych drogi bez KS (arm_crafts: n neutralnego startu, zajecia zmienione, przebudowy, rece regionu w sezonie) i prace okregu
            // (arm_works: wiersze prac, utarg okregow, przelacznik odbudowy); wlasny try, brak klucza (stary zapis, Ironman Jeffa) = pusto, starsza DLL ignoruje klucze
            try { string zw = dataStore.IsSaving ? VillageCrafts.Export() : null; SaveText.Sync(dataStore, "arm_crafts", ref zw); if (dataStore.IsLoading) VillageCrafts.Import(zw); }
            catch (Exception e) { Log.Error("SyncData.arm_crafts", e); }
            // f5-zajecia (noc 17/18.10): n na towar neutralnego startu zajec (Side Village Craft Neutral Per Good) - "zg1|towar=n|..."; PO arm_crafts; pisany tylko z danymi
            // (bez n na towar - klucza nie ma, zapis jak dotad); wlasny try, straznik rozmiaru 64 KB; brak klucza (stary zapis, Ironman Jeffa) = n zajecia; starsza DLL ignoruje klucz
            try { string zg = dataStore.IsSaving ? VillageCrafts.ExportGoods() : null; if (!dataStore.IsSaving || zg != null) SaveText.Sync(dataStore, "arm_crafts_g", ref zg); if (dataStore.IsLoading) VillageCrafts.ImportGoods(zg); }
            catch (Exception e) { Log.Error("SyncData.arm_crafts_g", e); }
            try { string po = dataStore.IsSaving ? DistrictWorks.Export() : null; SaveText.Sync(dataStore, "arm_works", ref po); if (dataStore.IsLoading) DistrictWorks.Import(po); }
            catch (Exception e) { Log.Error("SyncData.arm_works", e); }
            // n20-wioska (noc 19/20.10): wioska poboczna gracza ("sf1|uid|okreg|nazwa|doba|wyplacone|srednia"); wlasny try, brak klucza (stary zapis, Ironman Jeffa) = brak wioski gracza
            try { string sf = dataStore.IsSaving ? SideVillageFief.Export() : null; SaveText.Sync(dataStore, "arm_side_fief", ref sf); if (dataStore.IsLoading) SideVillageFief.Import(sf); }
            catch (Exception e) { Log.Error("SyncData.arm_side_fief", e); }
            try { string sh = dataStore.IsSaving ? SideVillageHome.ExportSave() : null; SaveText.Sync(dataStore, "arm_side_home", ref sh); if (dataStore.IsLoading) SideVillageHome.Import(sh); }   // r4-dom: skrzynia domu w wiosce gracza (sklad do logu przy zapisie - starsza DLL klucza nie zapisze)
            catch (Exception e) { Log.Error("SyncData.arm_side_home", e); }
            try { string sv = dataStore.IsSaving ? SideVillageVisit.Export() : null; SaveText.Sync(dataStore, "arm_sidevil", ref sv); if (dataStore.IsLoading) SideVillageVisit.Import(sv); }   // m97-wioski: okupy wiosek pobocznych, rabunki gracza (10 dob), rabunek w toku; brak klucza = nic
            catch (Exception e) { Log.Error("SyncData.arm_sidevil", e); }
            // R3-09f (regale gornicze): konta wozow wsi z kruszcem sprzedanym, a jeszcze nie w domu ("1|idWozu~kruszec~ladunek~doba"); brak klucza (stary zapis) = pusto; wlasny try
            try { string rg = dataStore.IsSaving ? MineRegale.Export() : null; if (!dataStore.IsSaving || rg != null) SaveText.Sync(dataStore, "arm_regale", ref rg); if (dataStore.IsLoading) MineRegale.Import(rg); }
            catch (Exception e) { Log.Error("SyncData.arm_regale", e); }
            // f5-a7 (noc 17/18.10): sygnal KS reguly zmiany zajec A7 - srednie ceny sygnalu miast, rodzaj Held, liczniki sezonu ("a7s1|...", straznik 64 KB); pisany tylko
            // z danymi (bez sygnalu - bez klucza, zapis jak dotad); wlasny try; brak klucza (stary zapis, Ironman Jeffa) = srednia od pierwszego kroku, Held od zera;
            // starsza DLL klucz pomija. Reset przed SyncData (konstruktor -> StartCalibration.Reset -> RuleReset -> A7sReset)
            try { string a7 = dataStore.IsSaving ? StartCalibration.ExportA7s() : null; if (!dataStore.IsSaving || a7 != null) SaveText.Sync(dataStore, "arm_ks_a7", ref a7); if (dataStore.IsLoading) StartCalibration.ImportA7s(a7); }
            catch (Exception e) { Log.Error("SyncData.arm_ks_a7", e); }
            // p5-handel (R3-05a): jarmarki w toku i z ostatniego roku ("hf1|kraina,miasto,start,pominiety;..."); pisany tylko z danymi; wlasny try; brak klucza (stary zapis,
            // Ironman Jeffa) = brak jarmarkow w toku (kalendarz otworzy nastepne); starsza DLL klucz pomija. Reset przed SyncData (konstruktor -> P5HandelReset)
            try { string hf = dataStore.IsSaving ? HarvestFairs.Export() : null; if (!dataStore.IsSaving || hf != null) SaveText.Sync(dataStore, "arm_fairs", ref hf); if (dataStore.IsLoading) HarvestFairs.Import(hf); }
            catch (Exception e) { Log.Error("SyncData.arm_fairs", e); }
            // p6-szubienice (R3-07a): szubienice stojace ("g1|x;y;yaw;koniec;okreg;wisielcy;rodzaj;kryjowka~..."); pisany tylko z danymi; wlasny try; brak klucza (stary zapis,
            // save040 Jeffa) = brak szubienic; starsza DLL klucz pomija. Reset przed SyncData (konstruktor -> P6SzubieniceReset)
            try { string gw = dataStore.IsSaving ? Gallows.Export() : null; if (!dataStore.IsSaving || gw != null) SaveText.Sync(dataStore, "arm_gallows", ref gw); if (dataStore.IsLoading) Gallows.Import(gw); }
            catch (Exception e) { Log.Error("SyncData.arm_gallows", e); }
            // p6-a7: roboty miast z rak okregu ("th2|doba startu|pulap startu|S|doba S|id:zwrot,..."); pisany tylko z danymi; wlasny try; brak klucza (stary zapis,
            // kampania Jeffa) = pomiar od pierwszej doby z firmami DS, start przy pierwszych przebudowach A7; starsza DLL klucz pomija. Reset przed SyncData (konstruktor -> P6a7Reset)
            try { string th = dataStore.IsSaving ? TownHire.Export() : null; if (!dataStore.IsSaving || th != null) SaveText.Sync(dataStore, "arm_townhire", ref th); if (dataStore.IsLoading) TownHire.Import(th); }
            catch (Exception e) { Log.Error("SyncData.arm_townhire", e); }
            // m96-dlugi: dlugi rad miast ("td1|id;P;R;B;IP;IR;dzien podatku;od kiedy;ostatni podatek;notabl~kwota~odsetki,...;dni kwartalu z dlugiem"); pisany tylko z danymi; wlasny try; brak klucza
            // (stary zapis, kampania Jeffa z WGRANIA 94/95) = rady bez dlugow; starsza DLL klucz pomija. Reset przed SyncData (konstruktor -> M96DlugiReset)
            try { string td = dataStore.IsSaving ? TownDebt.Export() : null; if (!dataStore.IsSaving || td != null) SaveText.Sync(dataStore, "arm_towndebt", ref td); if (dataStore.IsLoading) TownDebt.Import(td); }
            catch (Exception e) { Log.Error("SyncData.arm_towndebt", e); }
            // m102-tablica: tablica ogloszen ("nb1|F=flagi|W=listy gonczne|H=szkody band"); pisany tylko z danymi; wlasny try; brak klucza (stary zapis) = bez flag i listow; starsza DLL
            // klucz pomija. Reset przed SyncData (konstruktor -> M102TablicaReset)
            // m107-gospodarka K6 (Notice Board Lean Save): nb3 z proba odczytu (niezgodnosc - nb2) i linia "Tablica (m107): zapis gry"; klucz wylaczony - Export jak dotad
            try { string nb = dataStore.IsSaving ? NoticeBoard.ExportSave() : null; if (!dataStore.IsSaving || nb != null) SaveText.Sync(dataStore, "arm_board", ref nb); if (dataStore.IsLoading) NoticeBoard.Import(nb); }
            catch (Exception e) { Log.Error("SyncData.arm_board", e); }
            try { string bs = dataStore.IsSaving ? NoticeBoard.ExportBandSpots() : null; SaveText.Sync(dataStore, "arm_bounty_seen", ref bs); if (dataStore.IsLoading) NoticeBoard.ImportBandSpots(bs); }   // m110-znacznik: miejsca 'last seen'
            catch (Exception e) { Log.Error("SyncData.arm_bounty_seen", e); }
            // m106-wozy W2 (Village Cart Route Risk): pamiec tras wozow wsi ("m106r1~doba~swiat~region|miasto:A:N:doba!..."); wlasny try; klucz wylaczony - napis z zapisu
            // oddawany bez zmian (nowa kampania bez napisu - klucz nie pisany); brak klucza (stary zapis) = pamiec pusta; starsza DLL klucz pomija. Reset w konstruktorze
            try { string cr = dataStore.IsSaving ? CartRisk.Export() : null; if (!dataStore.IsSaving || cr != null) SaveText.Sync(dataStore, "arm_cart_risk", ref cr); if (dataStore.IsLoading) CartRisk.Import(cr); }
            catch (Exception e) { Log.Error("SyncData.arm_cart_risk", e); }
        }

        public override void RegisterEvents()
        {
            // B-1 (krok B po tescie 120 dob): KOLEJNOSC NASLUCHOW. Gra wola nasluchy zdarzenia od OSTATNIO dopisanego (MbEvent.AddNonSerializedListener
            // wstawia na poczatek listy - dekompilacja TaleWorlds.CampaignSystem/MbEvent.cs, AddNonSerializedListener/InvokeList). Ksiega obiegu
            // (paczka 169: kiesy partii bez wodza, ktore znikaja z mapy) ma zobaczyc kiese PO wszystkich naszych przelewach przy zniszczeniu partii
            // (112: sakwa taboru do zwyciezcy albo do wsi - VillageTakings.OnPartyDestroyed), wiec jej nasluch dopisujemy PIERWSZY - biegnie ostatni.
            // Dotad byl dopisany po VillageTakings i biegl przed nim: kazda sakwe oddana przez 112 liczyl jeszcze jako zloto, ktore zniknelo (tylko log).
            CampaignEvents.MobilePartyDestroyed.AddNonSerializedListener(this, DayProfile.Destroyed("CirculationWindows.OnPartyDestroyed", CirculationWindows.OnPartyDestroyed));    // paczka 169: kiesy partii bez wodza, ktore znikaja z mapy (tylko log) - MUSI byc pierwszym nasluchem MobilePartyDestroyed
            Seal31.RegisterEvents(this);   // 3.1 (31): Reset stanu kampanii; nasluch MobilePartyDestroyed DRUGI (biegnie przedostatni - po 3.2 i 112, przed ksiega obiegu), OnPartyRemoved, upadek krolestw, zdarzenia zlota
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
            CampaignEvents.OnNewGameCreatedEvent.AddNonSerializedListener(this, _ => DayPass.Clear());   // przeglad 07.10: doby kuzni z poprzedniej gry
            CampaignEvents.OnNewGameCreatedEvent.AddNonSerializedListener(this, _ => CamelLaw.OnNewGame());   // E6/M2 (208): wielblady tylko w Qarthu - tylko nowa kampania (flaga w zapisie)
            CampaignEvents.OnNewGameCreatedEvent.AddNonSerializedListener(this, _ => Bloomery.OnNewGame());   // E6/R1 (214): wytop przy kopalni - tylko nowa kampania (flaga w zapisie)
            CampaignEvents.OnNewGameCreatedEvent.AddNonSerializedListener(this, _ => WinterTrial.OnNewGame());   // E4/1 (B): proba zimy autotestu - plik-przelacznik w Documents\...\CrashScribe, tylko nowa kampania, tylko w pamieci
            CampaignEvents.OnNewGameCreatedEvent.AddNonSerializedListener(this, _ => PeopleMarket.OnNewGame());   // GL-2: zloto startowe wedlug ludzi - tylko nowa kampania (flaga w zapisie arm_peoplemarket)
            CampaignEvents.OnNewGameCreatedEvent.AddNonSerializedListener(this, _ => GoodsRates.OnNewGame());   // GL-5 (P5): wsie lniane tylko w kampanii zaczetej z kluczem (flaga w zapisie arm_goodsrates)
            CampaignEvents.OnNewGameCreatedEvent.AddNonSerializedListener(this, _ => GoodsPaths.OnNewGame());   // ZC1: glina i wapien w ladunkach tylko w kampanii zaczetej z kluczem (flaga w zapisie arm_zc1)
            CampaignEvents.OnNewGameCreatedEvent.AddNonSerializedListener(this, _ => VineOlive.OnNewGame());   // B6: winnice i gaje tylko w kampanii zaczetej z kluczem (flaga w zapisie arm_vineolive)
            // C1 (PLAN 6.10, klucz ClimateBeforeWorkshops): typy wsi OSTATECZNE nowej kampanii (T8, len GL5, winnice B6) PRZED budowa warsztatow miast
            // (gra: OnNewGameCreatedPartialFollowUp i == 10 - caly OnNewGameCreatedEvent biegnie przed petla follow-up; ten nasluch PO GoodsRates.OnNewGame
            // i VineOlive.OnNewGame - PinNewGame widzi _newGame), linia warsztatow po budowie startowej (FollowUpEnd, tylko log)
            CampaignEvents.OnNewGameCreatedEvent.AddNonSerializedListener(this, _ => { try { VillageClimate.BeforeWorkshops(); } catch (Exception e) { Log.Error("VillageClimate.BeforeWorkshops", e); } });
            CampaignEvents.OnNewGameCreatedPartialFollowUpEndEvent.AddNonSerializedListener(this, _ => { try { VillageClimate.WorkshopsFit(); } catch (Exception e) { Log.Error("VillageClimate.WorkshopsFit", e); } });
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
            CampaignEvents.OnItemProducedEvent.AddNonSerializedListener(this, OreLedger.OnProduced);   // wpis 98: ksiega rudy i drewna - sztuki faktycznie dopisane osadom
            CampaignEvents.OnItemProducedEvent.AddNonSerializedListener(this, GoodsPaths.OnItemProduced);   // ZC1 (BkSideGoodsOrigin): miod pitny zrobiony w miastach - o tyle drwale robia go mniej
            CampaignEvents.OnItemProducedEvent.AddNonSerializedListener(this, TownFolk.OnItemProduced);   // E5/0 (195): wartosc wyrobow miast i zamkow - place rzemiosla 35% (kiesa ludu na sucho, tylko log)
            CampaignEvents.OnItemProducedEvent.AddNonSerializedListener(this, Stage6Measure.OnItemProduced);   // E6/0 (206): wyroby miast wedlug krainy i konie ze stadnin wsi (tylko log)
            CampaignEvents.OnItemProducedEvent.AddNonSerializedListener(this, HerdCull.OnItemProduced);   // R3-05f: uboj jesienny (mieso, skory, sol) i splata dlugu stada z przyrostu wsi
            CampaignEvents.OnItemProducedEvent.AddNonSerializedListener(this, VillageByPeople.OnItemProduced);   // GL-1 (P1): towar glowny wsi dopisany wsi - plon faktyczny do plon-wsi.csv (tylko licznik, przy kluczu VillageOutputByPeople)
            CampaignEvents.OnItemConsumedEvent.AddNonSerializedListener(this, OreLedger.OnConsumed);   // wpis 98: wsad linii towarowych (narzedzia, deski)
            // pelny system rynku uzbrojenia (Jeff 04.10, docs/MODEL-MATERIALOW.md)
            CampaignEvents.WarDeclared.AddNonSerializedListener(this, ArmsPricing.OnWarDeclared);
            // E7/0 (224): pomiar etapu 7 (tylko log) - wojna i pokoj z przyczyna, upadek krolestwa, start turnieju; sluchacze tylko licza
            CampaignEvents.WarDeclared.AddNonSerializedListener(this, WorldClock7.OnWarDeclared);
            CampaignEvents.MakePeace.AddNonSerializedListener(this, WorldClock7.OnMakePeace);
            CampaignEvents.KingdomDestroyedEvent.AddNonSerializedListener(this, WorldClock7.OnKingdomDestroyed);
            CampaignEvents.TournamentStarted.AddNonSerializedListener(this, WorldClock7.OnTournamentStarted);
            CampaignEvents.TournamentFinished.AddNonSerializedListener(this, TourneyFolkPurse.OnTournamentFinished);   // f8-sakiewka (18.10): sakiewka mieszczan miasta-gospodarza dla zwyciezcy (klucz TourneyFolkPurse; po GivePrizeToWinner - poza RealPayer i oknem O11)
            // CG (19.10, recenzja: MbEvent - dopisany pozniej = wolany wczesniej): bohaterowie rodu w druzynie gracza kupuja sprzet za swoje PO sprzedazy nadwyzek
            // i zakupach ludzi (MenPurse.OnEntered) - rejestracja PRZED nimi
            CampaignEvents.SettlementEntered.AddNonSerializedListener(this, DayProfile.Settle("CompanionGear.OnEntered", CompanionGear.OnEntered));
            CampaignEvents.SettlementEntered.AddNonSerializedListener(this, DayProfile.Settle("MenPurse.OnEntered", MenPurse.OnEntered));   // wpis 84: nadwyzki ludzi PRZED zakupami
            CampaignEvents.SettlementEntered.AddNonSerializedListener(this, DayProfile.Settle("AiGear.OnSettlementEntered", AiGear.OnSettlementEntered));
            CampaignEvents.SettlementEntered.AddNonSerializedListener(this, DayProfile.Settle("CaravanAmmo.OnEntered", CaravanAmmo.OnEntered));   // paczka 172b: amunicja z taborow karawany na polke miasta (cena rynkowa, kasa miasta); dopisana PRZED CaravanBulk, wiec idzie PO nim - surowce, ktorych miastu brakuje, pierwsze do kasy miasta
            CampaignEvents.SettlementEntered.AddNonSerializedListener(this, DayProfile.Settle("CaravanGoods.OnEntered", CaravanGoods.OnEntered));   // E3-T1 (noc-ekonomia3): towar handlowy tam, gdzie karawana ma zysk - dopisany PO CaravanAmmo, PRZED CaravanBulk, wiec biegnie PO surowcach masowych, PRZED amunicja i BK
            CampaignEvents.SettlementEntered.AddNonSerializedListener(this, DayProfile.Settle("CaravanBulk.OnEntered", CaravanBulk.OnEntered));   // wpis 103: karawana sprzedaje miastu surowiec masowy, ktorego mu brakuje
            CampaignEvents.SettlementEntered.AddNonSerializedListener(this, DayProfile.Settle("MaterialOrders.OnEntered", MaterialOrders.OnEntered));   // 174.2: dostawa kontraktu surowca - dopisana PO CaravanBulk, wiec idzie PRZED nim (najpierw zamowiony ladunek)
            CampaignEvents.OnSettlementLeftEvent.AddNonSerializedListener(this, MenPurse.OnLeft);
            // m106-wozy W2: dosypka magazynu przy wyjezdzie wozu z domu (Village Cart Top Up On Departure); wersja wojen bramy wroga (Village Cart Enemy Gate) - wojna, pokoj,
            // zmiana wlasciciela osady, zmiana krolestwa rodu (pamiec wynikow bramy od nowa)
            CampaignEvents.OnSettlementLeftEvent.AddNonSerializedListener(this, MarketCarts.OnLeft);
            CampaignEvents.WarDeclared.AddNonSerializedListener(this, CartRisk.OnWar);
            CampaignEvents.MakePeace.AddNonSerializedListener(this, CartRisk.OnPeace);
            CampaignEvents.OnSettlementOwnerChangedEvent.AddNonSerializedListener(this, CartRisk.OnOwner);
            CampaignEvents.OnClanChangedKingdomEvent.AddNonSerializedListener(this, CartRisk.OnClanKingdom);
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, DayProfile.Hr("TroopSelfMend.Hourly", delegate { try { TroopSelfMend.Hourly(); } catch { } }));
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, DayProfile.Hr("TourneyPay.Hourly", delegate { try { TourneyPay.Hourly(); } catch (Exception e) { Log.Error("TourneyPay.Hourly", e); } }));   // TP: splata dlugow osad w pierwszej godzinie po starcie sesji (potem nic)
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, DayProfile.Hr("TownInns.Hourly", delegate { try { TownInns.Hourly(); } catch (Exception e) { Log.Error("TownInns.Hourly", e); } }));   // KA: koniec premii karczmy o zmierzchu, godziny gojenia w dobrym domu
            CampaignEvents.PlayerInventoryExchangeEvent.AddNonSerializedListener(this, TownInns.OnPlayerExchange);   // KA: handel gracza w miescie noclegu przy premii cen (tylko licznik linii doby)
            // sw-zajazdy: dojazd do zajazdu przy szlaku (4 razy na sekunde, jedna partia - klucz RoadsideInns) i ksiega plotek (zdarzenia swiata - klucz InnGossip)
            CampaignEvents.TickEvent.AddNonSerializedListener(this, delegate (float dt) { RoadsideInns.Tick(); });
            CampaignEvents.TickEvent.AddNonSerializedListener(this, delegate (float dt) { SideVillageFief.Tick(); });
            CampaignEvents.TickEvent.AddNonSerializedListener(this, delegate (float dt) { SideVillageVisit.Tick(); });   // m97-wioski: wjazd do wioski pobocznej po kliku, oboz po menu, straznik rabunku (4 razy na sekunde, tylko gdy cos czeka)   // n20-wioska: wjazd do wioski gracza po kliku / postoju (4 razy na sekunde, jedno porownanie odleglosci)
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, DayProfile.Hr("SideVillageFief.Hourly", delegate { try { SideVillageFief.Hourly(); } catch (Exception e) { Log.Error("SideVillageFief.Hourly", e); } }));   // n20-wioska: oczekujace nadanie, furtka autotestu
            CampaignEvents.MobilePartyCreated.AddNonSerializedListener(this, Rumours.OnPartyCreated);
            CampaignEvents.MobilePartyDestroyed.AddNonSerializedListener(this, Rumours.OnPartyDestroyed);
            CampaignEvents.RaidCompletedEvent.AddNonSerializedListener(this, Rumours.OnRaidCompleted);
            CampaignEvents.WarDeclared.AddNonSerializedListener(this, Rumours.OnWarDeclared);
            CampaignEvents.MakePeace.AddNonSerializedListener(this, Rumours.OnMakePeace);
            CampaignEvents.OnSettlementOwnerChangedEvent.AddNonSerializedListener(this, Rumours.OnOwnerChanged);
            CampaignEvents.MapEventEnded.AddNonSerializedListener(this, Rumours.OnMapEventEnded);
            CampaignEvents.MapEventEnded.AddNonSerializedListener(this, SideVillageVisit.OnMapEventEnded);   // m97-wioski krok 2: wynik bitwy z obroncami wioski pobocznej (sam zapis zwyciezcy; tylko przy bitwie w toku)
            CampaignEvents.ConversationEnded.AddNonSerializedListener(this, NoticeBoard.OnConversationEnded);   // m102-tablica E3: osoba z tablicy (licznik "z tablicy do zadania"), kapitan strazy (E2) - flaga rozmowy zdjeta
            CampaignEvents.OnSettlementLeftEvent.AddNonSerializedListener(this, NoticeBoard.OnSettlementLeft);   // m102-tablica E3: wyjazd gracza z miasta - osoba z tablicy zapomniana
            // m102-tablica E2: listy gonczne - wyplata za rozbita bande i szkody band (karawany, wozy) w MapEventEnded (przed zniszczeniem partii), zlupione wsie, banda znikla
            CampaignEvents.MapEventEnded.AddNonSerializedListener(this, DayProfile.MapEnd("NoticeBoard.OnMapEventEnded", NoticeBoard.OnMapEventEnded));
            CampaignEvents.RaidCompletedEvent.AddNonSerializedListener(this, NoticeBoard.OnRaidCompleted);
            CampaignEvents.MobilePartyDestroyed.AddNonSerializedListener(this, DayProfile.Destroyed("NoticeBoard.OnPartyDestroyed", NoticeBoard.OnPartyDestroyed));
            NoticeBoard.NoteEventsWired(3);
            // m109-list: przyjety list gonczy jako zadanie w oknie Quests (BountyQuest bez typu w zapisie) - zdjete z QuestManager przed kazdym zapisem (ten sluchacz biegnie
            // przed SyncData zachowan: menedzer zachowan dopisal swoj wczesniej, MbEvent wola od ostatnio dopisanego), po zapisie z powrotem; hak bitwy autotestu (plik flagi)
            CampaignEvents.OnBeforeSaveEvent.AddNonSerializedListener(this, delegate { try { NoticeBoard.BandMarkersOff(); NoticeBoard.BountyBeforeSave(); } catch (Exception e) { Log.Error("NoticeBoard.BountyBeforeSave", e); } });   // m110-znacznik: znaczniki band poza zapisem
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, delegate { try { NoticeBoard.BandMarkersSync(null); } catch (Exception e) { Log.Error("NoticeBoard.BandMarkersSync", e); } });   // m110-znacznik: znacznik idzie za banda
            CampaignEvents.OnSaveOverEvent.AddNonSerializedListener(this, delegate (bool ok, string name) { try { NoticeBoard.BountySaveOver(ok, name); NoticeBoard.BandMarkersSync("po zapisie"); } catch (Exception e) { Log.Error("NoticeBoard.BountySaveOver", e); } });
            NoticeBoard.NoteBountyWired(2);
            CampaignEvents.MapEventStarted.AddNonSerializedListener(this, delegate (MapEvent me, PartyBase a, PartyBase d) { try { NoticeBoard.OnMapEventStartedTest(me, a, d); } catch (Exception e) { Log.Error("NoticeBoard.OnMapEventStartedTest", e); } });
            CampaignEvents.OnSiegeEventStartedEvent.AddNonSerializedListener(this, Rumours.OnSiegeStarted);
            Ravens.RegisterEvents(this);   // p5-kruki (R3-05c): latka czatu w starcie sesji, doreczenie krukow co godzine, linia doby
            CampaignEvents.DailyTickPartyEvent.AddNonSerializedListener(this, DayProfile.PartyDay("AiGear.OnDailyTickParty", AiGear.OnDailyTickParty));
            // CG (19.10, recenzja - kolejnosc odwrotna): doba postoju druzyny gracza w miescie - bohaterowie rodu za swoje PO ludziach (rejestracja PRZED MenPurse)
            CampaignEvents.DailyTickPartyEvent.AddNonSerializedListener(this, DayProfile.PartyDay("CompanionGear.OnDailyTickParty", CompanionGear.OnDailyTickParty));
            CampaignEvents.DailyTickPartyEvent.AddNonSerializedListener(this, DayProfile.PartyDay("MenPurse.OnDailyTickParty", MenPurse.OnDailyTickParty));   // K1 (A4): doba postoju druzyny gracza w miescie - braki i lepsze za swoje
            CampaignEvents.DailyTickPartyEvent.AddNonSerializedListener(this, DayProfile.PartyDay("UniqueSpoils.OnDailyTickParty", UniqueSpoils.OnDailyTickParty));
            CampaignEvents.DailyTickPartyEvent.AddNonSerializedListener(this, DayProfile.PartyDay("QohorMasters.OnDailyTickParty", QohorMasters.OnDailyTickParty));   // 177-3: lord AI w Qohorze - ta sama regula przekucia (gotowe odsyla QohorMasters.Daily)
            CampaignEvents.SettlementEntered.AddNonSerializedListener(this, DayProfile.Settle("QohorMasters.OnEntered", QohorMasters.OnEntered));           // 177-3: "Your Valyrian steel is ready" przy wejsciu do Qohoru
            CampaignEvents.DailyTickPartyEvent.AddNonSerializedListener(this, DayProfile.PartyDay("ArmyClothing.OnDailyTickParty", ArmyClothing.OnDailyTickParty));   // 150: zuzycie odziezy w partiach rodow
            // musztra: sprzet wyrzucony na ekranach z bialej listy -> zapas cwiczebny; zlom z cwiczen do kowali przy wejsciu do miasta; godziny ruchu partii;
            // zlom czekajacy w rozbitej partii przepada (licznik)
            CampaignEvents.OnItemsDiscardedByPlayerEvent.AddNonSerializedListener(this, Drill.OnDiscarded);
            CampaignEvents.SettlementEntered.AddNonSerializedListener(this, DayProfile.Settle("Drill.OnEntered", Drill.OnEntered));
            CampaignEvents.MobilePartyDestroyed.AddNonSerializedListener(this, DayProfile.Destroyed("Drill.OnPartyDestroyed", Drill.OnPartyDestroyed));
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, DayProfile.Hr("Drill.Hourly", delegate { try { Drill.Hourly(); } catch (Exception e) { Log.Error("Drill.Hourly", e); } }));
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, DayProfile.Hr("SkillLedger.Hourly", delegate { try { SkillLedger.Hourly(); } catch { } }));   // E8/P1 (243): ksiega umiejetnosci - wylacznik na zywo, znacznik zrodla zerwany wyjatkiem w metodzie gry (tylko log)
            // SUWAKI MCM NA ZYWO (Jeff 03.09: "nadal 1 predkosc, o co chodzi" -
            // World Pace Percent przestawiony w grze nie dzialal). McmSettings.Apply()
            // szlo TYLKO w OnGameStart, wiec kazda zmiana w Mod Options czekala
            // na restart. Nasze pola MCM to zwykle auto-property bez powiadomien,
            // wiec zamiast zgadywac zdarzenia MCM przepisujemy wartosci co godzine
            // gry - tanie (kilkaset przypisan), a suwak wchodzi po paru sekundach.
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, DayProfile.Hr("McmSettings.Apply+AmmoTracer",
                delegate { try { McmSettings.Apply(); } catch { } try { AmmoTracer.HourlyCheck(); } catch { } }));
            CampaignEvents.OnSettlementOwnerChangedEvent.AddNonSerializedListener(this, WarLedger.OnOwnerChanged);
            CampaignEvents.OnSettlementOwnerChangedEvent.AddNonSerializedListener(this, KingdomBalance.OnOwnerChanged);   // 175.0: twierdze zdobyte i stracone oblezeniem (tylko log)
            // L5b (decyzja Jeffa 13.10 "5 tak, i tak"): skarbiec korony przy zdobyciu stolicy (zmiana pana BySiege i blizniak ROT w oknie szturmu) - po KingdomBalance;
            // stan doby obu regul L5b od zera przy nowej kampanii / wczytaniu (nic w zapisie)
            CapitalTreasury.Reset(); EnemyCaravans.Reset();
            CampaignEvents.OnSettlementOwnerChangedEvent.AddNonSerializedListener(this, CapitalTreasury.OnOwnerChanged);
            // drobne14: diagnoza L5b-2 (0 karawan w murach w 39 zdobyciach) - spis karawan przy poczatku oblezenia i wyjazdy z oblezanych osad (tylko pamiec i log)
            CampaignEvents.OnSiegeEventStartedEvent.AddNonSerializedListener(this, EnemyCaravans.OnSiegeStarted);
            CampaignEvents.OnSiegeEventEndedEvent.AddNonSerializedListener(this, EnemyCaravans.OnSiegeEnded);
            CampaignEvents.OnSettlementLeftEvent.AddNonSerializedListener(this, EnemyCaravans.OnLeft);
            // okup zamiast szturmu (decyzja Jeffa 13.10 pyt. 5b; SiegeRansom*.cs): koniec oblezenia czysci stan i okup w toku; koniec szturmu zamyka cykl decyzji
            // (szturm bez zdobycia - powod P2). Stan doby i pamiec od zera przy nowej kampanii / wczytaniu (konstruktor)
            CampaignEvents.OnSiegeEventEndedEvent.AddNonSerializedListener(this, SiegeRansom.OnSiegeEnded);
            // O1: oblezenie wbrew rozejmowi po okupie - tylko linia i licznik; wykonanie umow okupu w ticku kampanii (plynacy czas, wewnatrz Campaign.Tick - P5)
            CampaignEvents.OnSiegeEventStartedEvent.AddNonSerializedListener(this, SiegeRansom.OnSiegeStarted);
            CampaignEvents.TickEvent.AddNonSerializedListener(this, delegate (float dt) { SiegeRansom.Tick(); });
            // n20-napisy: linia kontrolna napisow przyciskow menu - dopisana PRZED SiegeRansom.AddMenus, wiec gra wola ja PO nim (sluchacze od ostatnio dopisanego)
            CampaignEvents.OnAfterSessionLaunchedEvent.AddNonSerializedListener(this, ShortLabels.Audit);
            // O3: opcja "Demand a ransom" w menu oblezenia gry - po wszystkich OnSessionLaunched (menu gry juz jest; wstawiona przed "Leave")
            CampaignEvents.OnAfterSessionLaunchedEvent.AddNonSerializedListener(this, SiegeRansom.AddMenus);
            // m109-list: zadania przyjetych listow z arm_board (po QuestManager.OnGameLoaded); dopisane PRZED AddMenus - gra wola je PO nim (linia m109 za liniami m102)
            CampaignEvents.OnAfterSessionLaunchedEvent.AddNonSerializedListener(this, s => { try { NoticeBoard.BountySessionStart(); NoticeBoard.BandMarkersOff(); NoticeBoard.BandMarkersSync("start sesji"); } catch (Exception e) { Log.Error("NoticeBoard.BountySessionStart", e); } });
            CampaignEvents.OnAfterSessionLaunchedEvent.AddNonSerializedListener(this, s => { try { NoticeBoard.AddMenus(s); } catch (Exception e) { Log.Error("NoticeBoard.AddMenus", e); } });   // m102-tablica: "Read the notice board" w menu miasta - po wszystkich OnSessionLaunched (opcje gry "town" juz stoja; miejsce za karczma)
            // HT (15.10 noc, PatchTarget.cs): linia "Latki czynnego modelu (HT): start sesji" - po OnSessionLaunched wszystkich modow (latki modeli w kampanii)
            CampaignEvents.OnAfterSessionLaunchedEvent.AddNonSerializedListener(this, s => PatchTarget.Report());
            // m96-wzory: rozdanie wzorow tieru 1, sanacja, naprawa zapisu i ksiega PO OnSessionLaunched wszystkich modow (ROT-RBM przepisuje luki po nas); nowa kampania - ksiegi od zera
            CampaignEvents.OnAfterSessionLaunchedEvent.AddNonSerializedListener(this, s => LoreSeedFix.AfterSessionLaunched(LorePurgeOnce));
            CampaignEvents.OnAfterSessionLaunchedEvent.AddNonSerializedListener(this, s => RareMaterials.StartLine());   // m97-surowce: czardrzewo i smocza kosc - surowce, zrodla, doplyw (po cenach HistoricalPrices)
            CampaignEvents.OnAfterSessionLaunchedEvent.AddNonSerializedListener(this, s => Recipes.MassStartLine());   // m96-receptury: przepisy z masy po ostatecznych statystykach (ROT-RBM)
            CampaignEvents.OnAfterSessionLaunchedEvent.AddNonSerializedListener(this, s => { try { ForgeBill.SessionStart(); } catch (Exception e) { Log.Error("ForgeBill.SessionStart", e); } });   // (dopisany po MassStartLine i RareMaterials.StartLine - biegnie PRZED nimi; probki same robia Resolve surowcow) m109-kuznia: rachunek kuzni - latka przycisku broni, linia startu z probkami (po przepisach z masy)
            CampaignEvents.OnNewGameCreatedEvent.AddNonSerializedListener(this, _ => LoreSeedFix.OnNewGame());
            CampaignEvents.MapEventEnded.AddNonSerializedListener(this, DayProfile.MapEnd("SiegeRansom.OnMapEventEnded", SiegeRansom.OnMapEventEnded));
            // mury oblezen OB1-OB4 (SiegeWalls.cs): rejestr szturmow AI (prog odparcia OB2, bitwa z murow OB3), wynik szturmu, koniec oblezenia (przerwy, OB4),
            // pomiary do linii OB0 (armie rozwiazane przy oblezeniu, machiny AI, trafienia w mury, nowe oblezenia) - tylko pamiec i log poza OB2/OB3
            CampaignEvents.MapEventStarted.AddNonSerializedListener(this, SiegeWalls.OnMapEventStarted);
            CampaignEvents.MapEventEnded.AddNonSerializedListener(this, DayProfile.MapEnd("SiegeWalls.OnMapEventEnded", SiegeWalls.OnMapEventEnded));
            CampaignEvents.OnSiegeEventEndedEvent.AddNonSerializedListener(this, SiegeWalls.OnSiegeEnded);
            CampaignEvents.OnSiegeEventStartedEvent.AddNonSerializedListener(this, SiegeWalls.OnSiegeStarted);
            CampaignEvents.ArmyDispersed.AddNonSerializedListener(this, SiegeWalls.OnArmyDispersed);
            CampaignEvents.SiegeEngineBuiltEvent.AddNonSerializedListener(this, SiegeWalls.OnEngineBuilt);
            CampaignEvents.OnSiegeBombardmentWallHitEvent.AddNonSerializedListener(this, SiegeWalls.OnWallHit);
            // oblezenia 3 (W, SiegeWalls2.Oblezenia3.cs): trafienia trebuszy obozow AI w machiny obroncow - przy stojacym murze i po rozbiciu murow (licznik linii "Oblezenia 3 (doba)")
            CampaignEvents.OnSiegeBombardmentHitEvent.AddNonSerializedListener(this, SiegeWalls2.OnBombardHit);
            // oblezenia 2 (SiegeWalls2.cs): trebusze AI zniszczone przez obroncow (licznik linii "Oblezenia 2 (doba)")
            CampaignEvents.OnSiegeEngineDestroyedEvent.AddNonSerializedListener(this, SiegeWalls2.OnEngineDestroyed);
            // przeglad 175 (tylko log): przecieki wolnych koni zbrojowni - jezdzcy zostawieni w zalodze i dezerterzy; stare zbrojownie (T2) w 1. godzinie
            CampaignEvents.OnTroopGivenToSettlementEvent.AddNonSerializedListener(this, delegate (Hero g, Settlement st, TaleWorlds.CampaignSystem.Roster.TroopRoster r) { try { HorseCensus.OnTroopGiven(g, st, r); } catch { } });
            CampaignEvents.OnTroopsDesertedEvent.AddNonSerializedListener(this, delegate (MobileParty mp, TaleWorlds.CampaignSystem.Roster.TroopRoster r) { try { HorseCensus.OnTroopsDeserted(mp, r); } catch { } });
            // E8/O5 (269): nadwyzki zalog po godzinach - kazda zaloga w swojej godzinie tygodnia (Id % 168) zamiast ok. 32 zalog w takcie doby; wylacznik GarrisonSellHourly
            // na zywo (wylaczony = sluchacz nic nie robi, kolejka w GarrisonArmory.Daily jak dotad). Rejestracja PRZED ArmoryFit.HourlyOnce = wolanie PO nim (gra dopisuje
            // sluchacza na poczatek listy - MbEvent): przeglad zbrojowni pierwszej godziny sesji widzi stan sprzed sprzedazy, jak dotad
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, DayProfile.Hr("GarrisonArmory.Hourly", delegate { try { GarrisonArmory.Hourly(); } catch (Exception e) { Log.Error("GarrisonArmory.Hourly", e); } }));
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, DayProfile.Hr("ArmoryFit.HourlyOnce", delegate { try { ArmoryFit.HourlyOnce(); } catch (Exception e) { Log.Error("ArmoryFit.HourlyOnce", e); } }));
            // sprzet startowy dopasowany do umiejetnosci (Jeff 04.10)
            CampaignEvents.OnCharacterCreationIsOverEvent.AddNonSerializedListener(this, delegate { try { StartKit.OnCharacterCreationOver(); } catch { } });   // wpis 86 (audyt pkt 17): limit zlota startowego niezalezny od StartKitEnabled
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, DayProfile.Hr("StartKit.Hourly", delegate { try { StartKit.Hourly(); } catch { } }));
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, DayProfile.Hr("MapClock.ApplySpeed", delegate { MapClock.ApplySpeed(); }));   // gra wraca do x4 przy wczytaniu
            // 13.10 (czytelna data): dowod do autotestu - przez pierwsze 3 doby kampanii w sesji co godzine gry linia "Data na pasku (kalendarz)" (tylko log, wylacznik WorldMeasureLog)
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, DayProfile.Hr("CalendarText.Hourly", delegate { try { CalendarText.Hourly(); } catch (Exception e) { Log.Error("CalendarText.Hourly", e); } }));
            // prawo wyrzutkow (Jeff 04.10): pula ludzi wyjetych spod prawa
            CampaignEvents.OnTroopsDesertedEvent.AddNonSerializedListener(this, OutlawLaw.OnTroopsDeserted);
            CampaignEvents.OnLootDistributedToPartyEvent.AddNonSerializedListener(this, BandFood.OnLoot);   // m96-bandy: lup jedzenia z band i druzyn lordow (pomiar)
            CampaignEvents.MapEventEnded.AddNonSerializedListener(this, DayProfile.MapEnd("OutlawLaw.OnMapEventEnded", OutlawLaw.OnMapEventEnded));
            CampaignEvents.MapEventEnded.AddNonSerializedListener(this, DayProfile.MapEnd("BattleChronicle.OnMapEventEnded", BattleChronicle.OnMapEventEnded));
            CampaignEvents.MapEventEnded.AddNonSerializedListener(this, DayProfile.MapEnd("AiWear.OnMapEventEnded", AiWear.OnMapEventEnded));   // wpis 85
            CampaignEvents.VillageLooted.AddNonSerializedListener(this, OutlawLaw.OnVillageLooted);
            VillageHerds.RegisterEvents(this);   // R3-06c (p5-stada): reset nowej kampanii, Resolve zapisu, rabunek gry (VillageLooted)
            HideoutHomeGuard.RegisterEvents(this);   // m109-crash: banda bez kryjowki wchodzi do kryjowki - latka BanditSpawnCampaignBehavior.OnHomeHideoutChanged (wywrotka 22.10 23:59)
            RaidTribute.RegisterEvents(this);    // R3-03a (p6-okup): latki i opcja "Demand tribute" w starcie sesji, linia doby "Okup okolicy (R3-03a)"
            TiredLand.RegisterEvents(this);      // R3-06d (p5-stada): reset nowej kampanii
            CampInterrogation.RegisterEvents(this);   // m109-przesluch: "Question the prisoners" w menu obozu, pamiec band z bitew gracza (MapEventEnded), linia doby - dopisany PRZED CampWalk:
                                                      // sluchacze OnSessionLaunched biegna od ostatnio dopisanego, wiec AddMenus idzie po Walk/Drill/Sutlers i index 4 = przed "Break camp" (przeglad n34 U1)
            CampWalk.RegisterEvents(this);       // m97-oboz: "Walk through the camp" w menu obozu (scena obozu wedlug druzyny, tylko na zadanie gracza)
            CampDrill.RegisterEvents(this);      // m97-szkolenie: "Train the men" w menu obozu i menu cwiczen (marsz nie uczy)
            CampSutlers.RegisterEvents(this);    // m99-markietani: "Visit the sutlers' row" w menu obozu (duzy oboz) - cyrulicy, zabawa, sklep z polki miasta; linia doby
            CampCart.RegisterEvents(this);       // m97-oboz: woz taborowy i siodla transportowe - reset nowej kampanii, menu miasta (stolarz i garbarz), linia doby, wozy w niewoli
            CampaignEvents.MobilePartyDestroyed.AddNonSerializedListener(this, DayProfile.Destroyed("UniqueSpoils.OnPartyDestroyed", UniqueSpoils.OnPartyDestroyed));   // 177-2: unikaty i stal valyrianska z taboru znikajacej partii - zwyciezcy, wlascicielowi albo na polke (ksiega towarow liczy tylko towary - kolejnosc bez znaczenia)
            CampaignEvents.MobilePartyDestroyed.AddNonSerializedListener(this, DayProfile.Destroyed("PackSources.OnDestroyed", PackSources.OnDestroyed));   // m6b: znacznik "RemoveParty po zdarzeniu zniszczenia" (konie przy RemoveParty, towar partii usunietych bez zniszczenia)
            CampaignEvents.MobilePartyDestroyed.AddNonSerializedListener(this, DayProfile.Destroyed("GoodsLedger.OnPartyDestroyed", GoodsLedger.OnPartyDestroyed));  // paczka 146: towar, ktory przepada z partia (tylko log)
            CampaignEvents.MobilePartyDestroyed.AddNonSerializedListener(this, DayProfile.Destroyed("MaterialOrders.OnPartyDestroyed", MaterialOrders.OnPartyDestroyed));   // 174.2: kontrakt przepada z rozbita karawana (ladunek - jak w grze)
            CampaignEvents.MobilePartyDestroyed.AddNonSerializedListener(this, DayProfile.Destroyed("OutlawLaw.OnPartyDestroyed", OutlawLaw.OnPartyDestroyed));
            CampaignEvents.MobilePartyDestroyed.AddNonSerializedListener(this, DayProfile.Destroyed("PeopleBook.OnPartyDestroyed", PeopleBook.OnPartyDestroyed));   // E4/3 (R18): tabor albo lodz rozwiazane przez gre bez bitwy - sklad do swojej wsi (zdarzenie przed RemoveParty)
            CampaignEvents.MercenaryNumberChangedInTown.AddNonSerializedListener(this, Veterans.OnTavernNumber);   // E4/10 (167): ubytek karczmy nie przez nas = najem weterana z puli (AI, karawany, gracz)
            CampaignEvents.MobilePartyDestroyed.AddNonSerializedListener(this, DayProfile.Destroyed("MenPurse.OnPartyDestroyed", MenPurse.OnPartyDestroyed));   // wpis 89
            CampaignEvents.MobilePartyDestroyed.AddNonSerializedListener(this, DayProfile.Destroyed("ArmyClothing.OnPartyDestroyed", ArmyClothing.OnPartyDestroyed));   // 150
            CampaignEvents.MobilePartyDestroyed.AddNonSerializedListener(this, DayProfile.Destroyed("MarketRoad.OnPartyDestroyed", MarketRoad.OnPartyDestroyed));   // wpis 100: rozbite tabory wiesniakow (log)
            // 112: sakwa zniszczonego taboru wsi nie ginie z partia. B-1: biegnie PRZED ksiega obiegu, bo nasluch ksiegi (CirculationWindows.OnPartyDestroyed)
            // jest dopisany pierwszy na poczatku RegisterEvents, a gra wola nasluchy od ostatnio dopisanego
            CampaignEvents.MobilePartyDestroyed.AddNonSerializedListener(this, DayProfile.Destroyed("VillageTakings.OnPartyDestroyed", VillageTakings.OnPartyDestroyed));
            // 3.2: reszta kiesy rozbitej karawany albo bandy do niszczyciela (jego osady, najblizszego miasta) - dopisana PO VillageTakings, wiec biegnie przed nim,
            // przed OutlawLaw (licznik kies band, ktore znikaja) i przed ksiega obiegu (dopisana pierwsza)
            CampaignEvents.MobilePartyDestroyed.AddNonSerializedListener(this, DayProfile.Destroyed("BeatenPurse.OnPartyDestroyed", BeatenPurse.OnPartyDestroyed));
            // lup miast L1 (D3): kiesa zalogi rozbitej przez wroga do zwyciezcy - przed ksiega obiegu (dopisana pierwsza, biegnie ostatnia)
            CampaignEvents.MobilePartyDestroyed.AddNonSerializedListener(this, DayProfile.Destroyed("CitySack.OnPartyDestroyed", CitySack.OnPartyDestroyed));
            CampaignEvents.BanditPartyRecruited.AddNonSerializedListener(this, BeatenPurse.OnBanditRecruited);   // D11-2: banda przylaczana do gracza (gra wola to tuz przed DestroyPartyAction.Apply(MainParty, banda))
            CampaignEvents.HeroOrPartyTradedGold.AddNonSerializedListener(this, VillageTakings.OnGoldTraded);       // 112: wyplaty majatkow BK w oknie powrotu taboru (tylko odczyt)
            CampaignEvents.MobilePartyDestroyed.AddNonSerializedListener(this, DayProfile.Destroyed("VillageClogDiag.OnPartyDestroyed", VillageClogDiag.OnPartyDestroyed));   // diagnoza zatkanych wsi: kiedy wies stracila woz (tylko log)
            // ksiega pieniadza i przeplywow osad (K1) oraz ksiega ludzi (demografia, krok 1) - same nasluchy, tylko log
            CampaignEvents.BeforeSettlementEnteredEvent.AddNonSerializedListener(this, MoneyLedger.OnBeforeEntered);   // tabor wsi: stan PRZED sprzedaza / podzialem utargu
            CampaignEvents.AfterSettlementEntered.AddNonSerializedListener(this, MoneyLedger.OnAfterEntered);          // ... i PO
            CampaignEvents.HeroOrPartyTradedGold.AddNonSerializedListener(this, AlleyProbe.OnGold);                 // m6b (6): kasy zaulkow - przelewy gry z udzialem kasy miasta (tylko licznik)
            CampaignEvents.HeroOrPartyTradedGold.AddNonSerializedListener(this, MoneyLedger.OnGoldTraded);             // kazdy GiveGoldAction gry
            CampaignEvents.HeroOrPartyTradedGold.AddNonSerializedListener(this, OutlawLaw.OnGoldTraded);               // nskarbiec: przelewy gry z kasa kryjowki (okup gracza, RC) - kategoria linii kas kryjowek (tylko licznik)
            CampaignEvents.OnCaravanTransactionCompletedEvent.AddNonSerializedListener(this, TownFolk.OnCaravanTrade);   // P1 (obieg kiesy ludu): dostawy karawan do miast - marza kupcow na towarze z karawan (tylko log)
            // paczka 169: nasluch kies partii, ktore znikaja z mapy (CirculationWindows.OnPartyDestroyed) - B-1: przeniesiony na poczatek RegisterEvents
            CampaignEvents.KingdomDestroyedEvent.AddNonSerializedListener(this, CirculationWindows.OnKingdomDestroyed); // paczka 169: skarbce krolestw, ktore upadly (tylko log)
            // A14 / G14 (drobne2): skarbiec upadajacego krolestwa - dlug Banku, reszta zdobywcy albo dworowi. Dopisany PO ksiedze obiegu, wiec biegnie PRZED nia
            // (MbEvent: od ostatnio dopisanego) - ksiega widzi skarbiec juz oprozniony. Rod panujacy zapamietany przy wyjsciu rodow z krolestwa (DiscontinueKingdom)
            CampaignEvents.KingdomDestroyedEvent.AddNonSerializedListener(this, FallenCrown.OnKingdomDestroyed);
            CampaignEvents.OnClanChangedKingdomEvent.AddNonSerializedListener(this, FallenCrown.OnClanChangedKingdom);
            CampaignEvents.MakePeace.AddNonSerializedListener(this, FallenCrown.OnMakePeace);   // G14: druga strona ostatniego pokoju (upadek przy stole pokoju)
            CampaignEvents.PlayerInventoryExchangeEvent.AddNonSerializedListener(this, SellByCondition.OnPlayerExchange);   // cena sprzedazy sprzetu: kazda sprzedaz gracza do handel.log (tylko log)
            CampaignEvents.MapEventEnded.AddNonSerializedListener(this, DayProfile.MapEnd("PeopleLedger.OnMapEventEnded", PeopleLedger.OnMapEventEnded));
            CampaignEvents.OnTroopRecruitedEvent.AddNonSerializedListener(this, PeopleLedger.OnTroopRecruited);
            CampaignEvents.OnUnitRecruitedEvent.AddNonSerializedListener(this, PeopleLedger.OnUnitRecruited);
            CampaignEvents.OnTroopsDesertedEvent.AddNonSerializedListener(this, PeopleLedger.OnTroopsDeserted);
            // E4/0 (188): partie tworzone, niszczone i usuwane bez zniszczenia - do ksiegi ludzi (linia "Partie (E4/0)"), bez nowej klasy (U8; tylko log)
            CampaignEvents.MobilePartyCreated.AddNonSerializedListener(this, PeopleLedger.OnPartyCreated);
            CampaignEvents.MobilePartyCreated.AddNonSerializedListener(this, ArmsFromPeople.OnPartyCreated);   // E4/8 (8.2): ramka tworzenia wedrowcow i konwojow BK (tylko przy otwartej ramce)
            CampaignEvents.MobilePartyDestroyed.AddNonSerializedListener(this, DayProfile.Destroyed("PeopleLedger.OnPartyDestroyed", PeopleLedger.OnPartyDestroyed));
            CampaignEvents.OnPartyRemovedEvent.AddNonSerializedListener(this, PeopleLedger.OnPartyRemoved);
            // E4/7 (164b-1): polegli wedlug pochodzenia (zdarzenie przed usunieciem partii - slowniki jeszcze sa; kolejnosc nasluchow bez znaczenia - Take proporcjonalne),
            // koniec partii bez zniszczenia, wcieleni jency gracza (DoneLogic ekranu druzyny)
            CampaignEvents.MapEventEnded.AddNonSerializedListener(this, DayProfile.MapEnd("PeopleBook.OnMapEventEnded", PeopleBook.OnMapEventEnded));
            CampaignEvents.OnPartyRemovedEvent.AddNonSerializedListener(this, PeopleBook.OnPartyRemoved);
            CampaignEvents.OnMainPartyPrisonerRecruitedEvent.AddNonSerializedListener(this, PeopleBook.OnMainPartyPrisonerRecruited);
            CampaignEvents.RaidCompletedEvent.AddNonSerializedListener(this, WatchLaw.OnRaidCompleted);   // E4/9 (H1): rabunki z napastnikiem frakcji Strazy - tylko licznik
            CampaignEvents.OnPrisonerReleasedEvent.AddNonSerializedListener(this, PrisonerLaw.OnReleased);   // I1: gracz wypuscil jencow - w krainie bez niewoli do domu / na Mur (I1b: w krainie z niewola do domu)
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, DayProfile.Hr("OutlawLaw.Hourly", delegate { try { OutlawLaw.Hourly(); } catch { } }));
            // r4-mur (recenzja): flaga "bez nowych decyzji" band najazdu w marszu / powrocie nigdy w zapisie gry (MobilePartyAi.DoNotMakeNewDecisions jest zapisywana) -
            // zdjeta przed zapisem, przywrocona po nim i w pierwszej godzinie po wczytaniu; bez band najazdu - nic
            CampaignEvents.OnBeforeSaveEvent.AddNonSerializedListener(this, delegate { try { FreeFolkRaids.BeforeSave(); } catch (Exception e) { Log.Error("FreeFolkRaids.BeforeSave", e); } });
            CampaignEvents.OnSaveOverEvent.AddNonSerializedListener(this, delegate (bool ok, string name) { try { FreeFolkRaids.SaveOver(); } catch (Exception e) { Log.Error("FreeFolkRaids.SaveOver", e); } });
            // zapis-d4: pomiar kluczy zapisu - nowy pomiar przed zapisem, linia "Zapis (D4)" po nim (takze po nieudanym); tylko log
            CampaignEvents.OnBeforeSaveEvent.AddNonSerializedListener(this, delegate { try { SaveSize.BeforeSave(); } catch (Exception e) { Log.Error("SaveSize.BeforeSave", e); } });
            CampaignEvents.OnSaveOverEvent.AddNonSerializedListener(this, delegate (bool ok, string name) { try { SaveSize.SaveOver(ok, name); } catch (Exception e) { Log.Error("SaveSize.SaveOver", e); } });
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, DayProfile.Hr("FreeFolkRaids.Hourly", delegate { try { FreeFolkRaids.Hourly(); } catch (Exception e) { Log.Error("FreeFolkRaids.Hourly", e); } }));
            CampaignEvents.MobilePartyDestroyed.AddNonSerializedListener(this, DayProfile.Destroyed("FreeFolkRaids.OnPartyDestroyed", FreeFolkRaids.OnPartyDestroyed));   // fix-mur: kto rozbil bande najazdu (tylko linia "cel:"; bez band - nic)
            CampaignEvents.MapEventEnded.AddNonSerializedListener(this, DayProfile.MapEnd("FreeFolkRaids.OnMapEventEnded", FreeFolkRaids.OnMapEventEnded));   // recenzja fix-mur: kto pobil bande w marszu (tylko linia "cel:"; bez band w marszu - nic)
            CampaignEvents.MapEventEnded.AddNonSerializedListener(this, DayProfile.MapEnd("Gallows.OnMapEventEnded", Gallows.OnMapEventEnded));   // p6-szubienice (R3-07a): rozbita banda / oczyszczona kryjowka -> szubienica przy rozstajach (tylko wyglad; sam odczyt bitwy)
            CampaignEvents.MapEventEnded.AddNonSerializedListener(this, DayProfile.MapEnd("ArmouryBehavior.OnMapEventEnded", OnMapEventEnded));
            CampaignEvents.MapEventEnded.AddNonSerializedListener(this, DayProfile.MapEnd("HorseFate.OnMapEventEnded", HorseFate.OnMapEventEnded));   // E6/K1 (211): zwyciezcy i ich wklad dla pokonanych (dobytek rozbitych - przed HandleMapEventEnd)
            CampaignEvents.MapEventStarted.AddNonSerializedListener(this, OnMapEventStarted);
            // f5-bitwa (noc 17/18.10; recenzja 18.10 - MbEvent: dopisany wczesniej = wolany pozniej): zestaw bitwy autotestu zdjety PO delegacie
            // GatherFallen+WearKeep+AmmoRecovery (rejestracja PRZED nim), ksiega przycieta ReconcileStock; misja otwarta - pierwsza godzina; tylko autotest
            CampaignEvents.MapEventEnded.AddNonSerializedListener(this, DayProfile.MapEnd("AutotestBattleKit.OnMapEventEnded", AutotestBattleKit.OnMapEventEnded));
            // polegli oddaja rynsztunek na wozy zaraz po bitwie
            CampaignEvents.MapEventEnded.AddNonSerializedListener(this, DayProfile.MapEnd("GatherFallen+WearKeep+AmmoRecovery",
                delegate (MapEvent me) { try { if (me != null && me.IsPlayerMapEvent) GatherFallen(); } catch { } try { if (me != null && me.IsPlayerMapEvent) WearKeep.AfterBattle(); } catch { } try { if (me != null && me.IsPlayerMapEvent) AmmoRecovery.AfterBattle(); } catch { } }));
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, DayProfile.Hr("AutotestBattleKit.Hourly", AutotestBattleKit.Hourly));
            CampaignEvents.OnNewItemCraftedEvent.AddNonSerializedListener(this, OnNewItemCrafted);
            // DEPOZYT KWATERMISTRZA NIE MA PRAWA WEJSC DO SAVE'A: schowane
            // na czas ekranu zbrojowni sztuki zyja poza rosterem - zapis gry
            // w tym oknie utrwalilby save BEZ nich (27.08 bylo o wlos: save
            // 16:33, schowanie 1177 szt. o 16:34). Przed kazdym zapisem
            // wszystko wraca na polki.
            CampaignEvents.OnBeforeSaveEvent.AddNonSerializedListener(this,
                delegate { try { QuartermasterEscrow.SafetyRelease(); } catch { } });
            // jeniec wziety po bitwie zostaje obszukany - jego rynsztunek idzie do sakw
            CampaignEvents.OnPrisonerTakenEvent.AddNonSerializedListener(this, OnPrisonerTaken);
            CampaignEvents.HeroPrisonerTaken.AddNonSerializedListener(this, UniqueSpoils.OnPrisonerTaken);
            CampaignEvents.HeroPrisonerTaken.AddNonSerializedListener(this, CaptiveArmory.OnPrisonerTaken);   // C4 (Jeff 14.10): pojmany gracz - zbrojownia DTE do zdobywcy, jak u lorda AI
            // f5-bitwa (recenzja 18.10): pojmany gracz - zestaw bitwy autotestu zdjety PRZED CaptiveArmory (rejestracja PO nim = wolanie przed nim); tylko autotest
            CampaignEvents.HeroPrisonerTaken.AddNonSerializedListener(this, AutotestBattleKit.OnPrisonerTaken);
            CrownRents.RegisterEvents(this);    // 180 (C3): bitwy, rabunki i szturmy z wrogiem o kazdej porze doby - pomiar sluzby (tylko liczniki do ticku)
            WarFront.RegisterEvents(this);      // N5-E: te same zdarzenia z miejscem - warownie przy froncie (budowle cywilne z dala od frontu)
            Ransom178.RegisterEvents(this);     // 178: wielki jeniec przy pojmaniu przechodzi na korone zdobywcy (nagroda 1/10)
            Measure169c.RegisterEvents(this);   // 169c: niewola lordow, dezercja, sluby AI, Bliznaki - same liczniki (tylko log)
            AiDowry.RegisterEvents(this);       // R3-01c: posag przy slubach AI z AI (BeforeHeroesMarried)
            ArmyCohesionYear.RegisterEvents(this);   // AC (Jeff 14.10): armie zalozone / rozwiazane - wiek armii i przyczyny do linii doby
            PregnancyYear.RegisterEvents(this);   // CR (zasada Jeffa 14.10): poczecia i porody (OnChildConceived / OnGivenBirth) - same liczniki do linii doby
            FiefGrants.RegisterEvents(this);   // PROJEKT-LENNA (14.10): uczestnicy zwycieskiego szturmu (MapEventEnded), rodzaj zmiany pana twierdzy, decyzje "Owner of X" rozstrzygniete / anulowane
            // M6a: najkrotsza sluzba kompanii (pole gry przy najmie, pomiar konca sluzby), pamiec okupow (kazda droga okupu), rozstrzygniecie pokoju po 24 h
            CampaignEvents.OnClanChangedKingdomEvent.AddNonSerializedListener(this, MercTenure.OnClanChangedKingdom);
            CampaignEvents.WarDeclared.AddNonSerializedListener(this, MercTenure.OnWarDeclared);   // M6a-1: pole gry od wypowiedzenia wojny (nie od nastepnej doby)
            CampaignEvents.MakePeace.AddNonSerializedListener(this, MercTenure.OnMakePeace);       // M6a-1: wyjatek "pokoj" od zawarcia pokoju
            CampaignEvents.HeroPrisonerReleased.AddNonSerializedListener(this, RansomRepeat.OnReleased);
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, DayProfile.Hr("PeaceHold.Hourly", delegate { try { PeaceHold.Hourly(); } catch (Exception e) { Log.Error("PeaceHold.Hourly", e); } }));
            CampaignEvents.HeroKilledEvent.AddNonSerializedListener(this, UniqueSpoils.OnHeroKilled);
            CampaignEvents.OnBeforePlayerCharacterChangedEvent.AddNonSerializedListener(this, UniqueSpoils.OnBeforePlayerChanged);   // recenzja 177: stal valyrianska z zestawu ukrycia gracza (gra go nie przekazuje) ...
            CampaignEvents.OnPlayerCharacterChangedEvent.AddNonSerializedListener(this, UniqueSpoils.OnPlayerChanged);               // ... trafia do taboru nastepcy
            // DIAGNOSTYKA (Jeff 29.08: "awans wycina sprzet z magazynu?") -
            // ani DTE, ani my nie sluchamy awansow, wiec logujemy sume
            // magazynu przy kazdym awansie: jak suma spada miedzy wpisami,
            // zlodziej istnieje; jak stoi - to zmiana wzorca po awansie.
            CampaignEvents.PlayerUpgradedTroopsEvent.AddNonSerializedListener(this, OnPlayerUpgradedTroops);
            // klawisz O na mapie: szybki oboz (BannerKings) bez klikania przez ekrany
            CampaignEvents.TickEvent.AddNonSerializedListener(this, delegate (float dt)
            {
                NightRest.OnTick(dt);
                try { CrossingLaw.OnTick(dt); } catch { }
                try { ArmyRestDay.OnTick(); } catch { }   // R3-08a: straznik postoju armii co 0.1 h gry
            });
            CampaignEvents.SettlementEntered.AddNonSerializedListener(this, DayProfile.Settle("Orders+Stables+ElephantQuarantine",
                delegate (MobileParty mp, Settlement st, Hero h)
                {
                    try { Orders.OnSettlementEntered(mp, st); } catch { }
                    try { Stables.OnSettlementEntered(mp, st); } catch { }
                    try { SaddleLaw.OnSettlementEntered(mp, st); } catch { }   // E8/H (271): uprzaz z taboru do zbrojowni do zapasu siodel - po Stajni, przed sprzedaza nadwyzek i zakupami sprzetu
                    try { if (mp == MobileParty.MainParty) ElephantQuarantine.Sweep(st); } catch { }
                }));
            // f5-bitwa (recenzja 18.10 - MbEvent: dopisany pozniej = wolany wczesniej): zestaw bitwy autotestu czekajacy na zdjecie schodzi PRZED
            // Orders+Stables+SaddleLaw i PRZED sprzedaza nadwyzek ludzi (MenPurse.OnEntered) - rejestracja PO nich; tylko autotest
            CampaignEvents.SettlementEntered.AddNonSerializedListener(this, delegate (MobileParty p, Settlement st, Hero h) { try { if (p != null && p.IsMainParty) AutotestBattleKit.OnMainEntered(); } catch { } });
            CampaignEvents.DailyTickSettlementEvent.AddNonSerializedListener(this, DayProfile.TownDay("ElephantQuarantine+TroopSelfMend.Run",
                delegate (Settlement st)
                {
                    try { ElephantQuarantine.Sweep(st); } catch { }
                    try { TroopSelfMend.Run(st); } catch { }
                }));
            // menu kucia otwarte JAKAKOLWIEK droga (takze wznowione z save'a,
            // z pominieciem StartCraftingMenu) - dniowka kupuje sie od razu
            CampaignEvents.GameMenuOpened.AddNonSerializedListener(this, OnGameMenuOpened);
            // T6 (noc 08/09.10): miara marszu - pozycje partii lordow co godzine (tylko log, wylacznik WorldMeasureLog)
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, DayProfile.Hr("WorldMeasure.Hourly", delegate { try { WorldMeasure.Hourly(); } catch (Exception e) { Log.Error("WorldMeasure.Hourly", e); } }));
            // E8/P3 (245): postoj karawan AI w miastach - godzina wjazdu i wyjazdu (tylko pamiec i licznik, wylacznik Stage8WorldLog; kolejnosc wobec innych nasluchow bez znaczenia)
            CampaignEvents.SettlementEntered.AddNonSerializedListener(this, DayProfile.Settle("Stage8World.OnEntered", Stage8World.OnEntered));
            CampaignEvents.OnSettlementLeftEvent.AddNonSerializedListener(this, Stage8World.OnLeft);
            // E7/T (225): probka kar terenu - partie AI w ruchu w lesie / na pustyni / na rowninie i do 8 rozpisek gry na godzine (tylko log, Stage7MeasureLog)
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, DayProfile.Hr("TerrainEase.Hourly", delegate { try { TerrainEase.Hourly(); } catch (Exception e) { Log.Error("TerrainEase.Hourly", e); } }));
            // SW-DROGI (noc 17/18.10): spis partio-godzin na drogach ROT w dobach pomiaru (LogGate.Today; tylko log)
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, DayProfile.Hr("MapRoads.Hourly", delegate { try { MapRoads.Hourly(); } catch (Exception e) { Log.Error("MapRoads.Hourly", e); } }));
            // p5-bloto (R3-07c): spis partio-godzin w deszczu i w blocie w dobach pomiaru (LogGate.Today; tylko log)
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, DayProfile.Hr("MapMud.Hourly", delegate { try { MapMud.Hourly(); } catch (Exception e) { Log.Error("MapMud.Hourly", e); } }));
            // p6-sztormy (R3-05d): pora roku i lista burz NavalDLC do pamieci (mnoznik zimy), burze godziny; spis statkow w burzy w dobach pomiaru (LogGate.Today)
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, DayProfile.Hr("SeaStorms.Hourly", delegate { try { SeaStorms.Hourly(); } catch (Exception e) { Log.Error("SeaStorms.Hourly", e); } }));
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, DayProfile.Hr("AiRoadRoutes.Hourly", delegate { try { AiRoadRoutes.Hourly(); } catch (Exception e) { Log.Error("AiRoadRoutes.Hourly", e); } }));   // p6-drogi (R3-07d): spis partio-godzin rodow / karawan / wozow na drodze i na objezdzie (tylko log)
            // E8/Q (262): straz przeprawy zawraca wrogie partie lordow AI raz na godzine gry, niezaleznie od gracza (wylacznik CrossingLawAiClock). Rejestracja PRZED
            // probka E7/B = wolanie PO niej (gra dopisuje sluchacza na poczatek listy - MbEvent): licznik przejsc widzi przerwy sprzed rozkazu tej godziny
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, DayProfile.Hr("CrossingLaw.AiHourly", delegate { try { CrossingLaw.AiHourly(); } catch (Exception e) { Log.Error("CrossingLaw.AiHourly", e); } }));
            // E7/B (231): licznik przejsc przez Bliznaki - lokator mapy wokol bramy raz na godzine (tylko log, wylacznik TwinsCrossingCount)
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, DayProfile.Hr("TwinsCrossing.Hourly", delegate { try { TwinsCrossing.Hourly(); } catch (Exception e) { Log.Error("TwinsCrossing.Hourly", e); } }));
            // E7/W1 (232): wiesci z wiosek - zdarzenie gry tylko kolejkuje (odczyt ksiegi), komunikaty w ticku godzinnym (wylacznik VillageRaidMessages = 0)
            CampaignEvents.VillageStateChanged.AddNonSerializedListener(this, VillageNews.OnStateChanged);
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, DayProfile.Hr("VillageNews.Hourly", delegate { try { VillageNews.Hourly(); } catch (Exception e) { Log.Error("VillageNews.Hourly", e); } }));
            // pas bezpieczenstwa depozytu kwatermistrza: czas plynie = ekran
            // zbrojowni zamkniety; gdyby domkniecie nie oddalo sprzetu, oddajemy tu
            // f9-predkosc: znaczniki czasu miedzy czesciami tego sluchacza (najwiekszy w "Profil doby") - rozbicie w linii "Predkosc mapy (f9)", tylko log
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, DayProfile.Hr("Kwatermistrz+projekty+MaterialOrders.Hourly",
                delegate
                {
                    var qt = _qtSplit;
                    qt[0] = System.Diagnostics.Stopwatch.GetTimestamp();
                    try { QuartermasterEscrow.SafetyRelease(); } catch { }
                    qt[1] = System.Diagnostics.Stopwatch.GetTimestamp();
                    try { AdvanceProjects(); } catch (Exception e) { Log.Error("AdvanceProjects(h)", e); }
                    qt[2] = System.Diagnostics.Stopwatch.GetTimestamp();
                    try { TryStripNewCaptives("hourly"); } catch { }
                    qt[3] = System.Diagnostics.Stopwatch.GetTimestamp();
                    try { NightRest.OnHourly(); } catch { }
                    try { InnDrink.Hourly(); } catch (Exception e) { Log.Error("InnDrink.Hourly", e); }   // m98-plotki: PO ksiedze snu - koniec kaca, komunikat po pobudce
                    try { ArmyRestDay.Hourly(); } catch (Exception e) { Log.Error("ArmyRestDay.Hourly", e); }   // R3-08a: PO obozie swiata (swit oddaje rozkazy, potem postoj)
                    qt[4] = System.Diagnostics.Stopwatch.GetTimestamp();
                    try { MaterialOrders.Hourly(); } catch (Exception e) { Log.Error("MaterialOrders.Hourly", e); }   // 174b.1: kontrakt surowca trzyma cel - ZARAZ PO obozie tej godziny
                    qt[5] = System.Diagnostics.Stopwatch.GetTimestamp();
                    try
                    {
                        // ekran lupow nie odebral kolejki w godzine - do sakw z nia
                        if (_lootFlushDue != CampaignTime.Zero && CampaignTime.Now > _lootFlushDue)
                        {
                            _lootFlushDue = CampaignTime.Zero;
                            if (BattlefieldLaw.SharePending()) BattlefieldLaw.FlushShareToBaggage("ekran lupow nie przyszedl");
                        }
                    }
                    catch { }
                    try
                    {
                        // pobitewny meldunek kwatermistrza - juz PO wchlonieciu lupow
                        if (_shortageShoutDue != CampaignTime.Zero && CampaignTime.Now > _shortageShoutDue)
                        {
                            _shortageShoutDue = CampaignTime.Zero;
                            QuartermasterLaw.ShoutShortages("QM short (have/need):");
                        }
                    }
                    catch { }
                    qt[6] = System.Diagnostics.Stopwatch.GetTimestamp();
                    MapSpeedProfile.HourSplit(qt);
                }));
        }

        private void OnGameMenuOpened(TaleWorlds.CampaignSystem.GameMenus.MenuCallbackArgs args)
        {
            try
            {
                // gotowe wyroby z kuzni wydaja sie od progu, bez czekania na tick
                try { CollectReadyProjects(); } catch { }
                // stany magazynu wracaja PRZED czystkami (DTE odtwarza roster
                // dopiero po sesji - lapiemy pierwszy moment, gdy juz jest)
                try { TryRestoreArmoryWear("menu"); } catch { }
                // ZWROT WYLACZONY 30.08 - Jeff wyjasnil, ze wklad MA przechodzic
                // do wojska; oddawanie kolczanow na polke gracza byloby wbrew temu
                // po kazdej bitwie menu sie otwiera - smok wyleci zanim DTE go osiodla
                try { CleanseDragonStables(true); } catch { }
                // ...a smieci <=3% i slonie-towar zaraz za nim (Spoils naklada
                // stany PO naszym filtrze lupow - tu wymiatamy je od reki)
                try { CleanseTrashInBags(); } catch { }
                // CZYSTKA MAGAZYNU WYLACZONA (Jeff 03.09: "nie ma byc wymiana 1:1,
                // nic nie znika" - 2 z 3 zdobytych pancerzy przepadly, bo limit
                // TrimWarStores = liczba zolnierzy, a mial 2 ludzi). Zdjety sprzet
                // zostaje w magazynie DTE w calosci; TrimWarStores zostaje w kodzie
                // jako martwy na wypadek powrotu do pomyslu z 29.08.
                // try { if (CampaignTime.Now <= _spoilsWindow) TrimWarStores(); } catch { }
                // samonaprawa depozytu: otwarte menu gry = na pewno NIE ekran
                // zbrojowni; jesli cokolwiek wisi w depozycie (Release nie
                // odpalil przy zamykaniu ekranu), wraca na polki teraz
                try { QuartermasterEscrow.SafetyRelease(); } catch { }
                var gm = args != null && args.MenuContext != null ? args.MenuContext.GameMenu : null;
                if (gm != null && gm.StringId == "bannerkings_wait_crafting")
                {
                    DayPass.EnsureBought();
                    // BLAD CZASU (Jeff, throwing axes): menu odczekiwania godzin
                    // kuzni BK potrafi otworzyc sie z zatrzymanym czasem i starym
                    // zegarem - odpalamy czas OD RAZU i liczymy godziny od TERAZ,
                    // zeby nie doczekiwac ani chwili ponad wykupione godziny
                    try
                    {
                        gm.StartWait();
                        var tAct = QuartermasterLaw.FindType("BannerKings.Behaviours.BKSettlementActions");
                        if (tAct != null)
                        {
                            var f = HarmonyLib.AccessTools.Field(tAct, "actionStart");
                            if (f != null)
                            {
                                object beh = null;
                                if (!f.IsStatic)
                                {
                                    var mGet = typeof(Campaign).GetMethod("GetCampaignBehavior");
                                    if (mGet != null) beh = mGet.MakeGenericMethod(tAct).Invoke(Campaign.Current, null);
                                }
                                if (f.IsStatic || beh != null) f.SetValue(f.IsStatic ? null : beh, CampaignTime.Now);
                            }
                        }
                    }
                    catch (Exception e2) { Log.Error("BkWaitFix", e2); }
                }
                TryStripNewCaptives("menu");   // pierwsze menu po bitwie - jency juz przypisani
            }
            catch (Exception e) { Log.Error("OnGameMenuOpened", e); }
        }

        /// <summary>Waga wegla drzewnego. Do N2 (noc 16.10) klucz CharcoalWeight (0.5 w Armoury.json Jeffa) robil z 5 kg bryly gry pol kilo - przy cenach
        /// historycznych 0.5 kg kosztowalo 1 d (x28.6 historii), a przetop z wagi (MaterialLaw.RefinePostfix) robil z ladunku drewna za 4 d 40 wegli za 40 d.
        /// N2: waga z klucza CharcoalWeightKg (0 = waga gry); stary klucz nieczytany. Waga gry zapamietana przy pierwszym odczycie w procesie - przy
        /// CharcoalWeightKg 0 wraca, gdyby wczesniejsza sesja tego procesu ja zmienila. PRZED wycena (HistoricalPrices: cena = waga x cena za kg).</summary>
        private static float _coalGameKg = -1f;
        internal static float CoalGameKg { get { return _coalGameKg; } }
        private static void FixCharcoalWeight()
        {
            var c = Settings.Current;
            var coal = TaleWorlds.ObjectSystem.MBObjectManager.Instance.GetObject<ItemObject>("charcoal");
            if (coal == null) { Log.Info("Wegiel drzewny: brak przedmiotu charcoal - waga bez zmian."); return; }
            if (_coalGameKg < 0f) _coalGameKg = coal.Weight;
            float want = c.CharcoalWeightKg > 0f ? Math.Min(50f, c.CharcoalWeightKg) : _coalGameKg;
            if (Math.Abs(coal.Weight - want) >= 0.001f && want > 0f)
            {
                var f = typeof(ItemObject).GetProperty("Weight");
                if (f != null && f.CanWrite) { f.SetValue(coal, want, null); }
                else
                {
                    var bf = typeof(ItemObject).GetField("<Weight>k__BackingField",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                    if (bf != null) bf.SetValue(coal, want);
                }
            }
            Log.Info("Wegiel drzewny: waga " + coal.Weight.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + " kg/szt. (waga gry "
                     + _coalGameKg.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + " kg; Charcoal Weight Kg "
                     + c.CharcoalWeightKg.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + (c.CharcoalWeightKg > 0f ? "" : " = waga gry")
                     + "; stary klucz Charcoal Weight " + c.CharcoalWeight.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + " nieczytany - N2).");
        }

        /// <summary>
        /// SANACJA SAKW. Ujemny stos w ItemRoster (pozostalosc po bledzie
        /// przetopu z 27.08 - AddToCounts(-1) na nieistniejacym wpisie) wywraca
        /// caly ekran ekwipunku: kazda kategoria swieci pustka. Przy kazdym
        /// wczytaniu zerujemy takie wpisy w sakwach i w zbrojowni DTE.
        /// </summary>
        private static void CleanseNegativeStacks()
        {
            int fixedTotal = 0;
            foreach (var roster in new[] { MobileParty.MainParty != null ? MobileParty.MainParty.ItemRoster : null,
                                           QuartermasterLaw.DteArmory() })
            {
                if (roster == null) continue;
                try
                {
                    for (int i = roster.Count - 1; i >= 0; i--)
                    {
                        var el = roster.GetElementCopyAtIndex(i);
                        if (el.Amount >= 0) continue;
                        roster.AddToCounts(el.EquipmentElement, -el.Amount);   // do zera
                        fixedTotal++;
                        Log.Info("Sanacja sakw: " + (el.EquipmentElement.Item != null ? el.EquipmentElement.Item.StringId : "?")
                                 + " mial stan " + el.Amount + " - wyzerowany.");
                    }
                }
                catch (Exception e) { Log.Error("CleanseNegativeStacks", e); }
            }
            if (fixedTotal > 0)
                Log.Player("The quartermaster set the ledgers straight - " + fixedTotal + " ruined entries struck out.", true);
        }

        /// <summary>Smieci ponizej progu zniszczenia precz z sakw i magazynu
        /// (Jeff 29.08: "usun jednak te 3%, to nie ma sensu") - stare wraki
        /// sprzed progu; nowych prog nie wpuszcza do lupow.</summary>
        internal static void CleanseTrashInBags()
        {
            try
            {
                var s = Settings.Current;
                if (s == null || s.LootMinConditionPercent <= 0) return;
                int cut = 0, exoToBag = 0;
                // r2-konie (recenzja, klucz ExoticMountsNeverVanish): wielblad/rydwan/slon/mamut nie u swoich nie znika z sakw gracza - kupiony
                // na targu albo zdobyty zostaje towarem w sakwach; ze zbrojowni DTE gracza schodzi do sakw (DTE i tak go nie przydzieli).
                // Slonie-towar wedlug id - jak dotad (Jeff 29.08). Wylaczony - kasowanie jak dotad.
                bool exoticKeep = s.ExoticMountsNeverVanish;
                var bagRoster = MobileParty.MainParty != null ? MobileParty.MainParty.ItemRoster : null;
                foreach (var roster in new[] { bagRoster, QuartermasterLaw.DteArmory() })
                {
                    if (roster == null) continue;
                    for (int i = roster.Count - 1; i >= 0; i--)
                    {
                        var el = roster.GetElementCopyAtIndex(i);
                        var it = el.EquipmentElement.Item;
                        if (el.Amount <= 0 || it == null) continue;
                        if (exoticKeep && !(it.StringId != null && (it.StringId == "elephant" || it.StringId.StartsWith("rot_elephant")))
                            && MountLaw.IsExotic(it) && !MountLaw.AllowedForParty(MobileParty.MainParty, it))
                        {
                            if (roster != bagRoster && bagRoster != null)
                            {
                                roster.AddToCounts(el.EquipmentElement, -el.Amount);
                                bagRoster.AddToCounts(el.EquipmentElement, el.Amount);
                                exoToBag += el.Amount;
                            }
                            continue;
                        }
                        // slonie-towar precz (Jeff: "slonie ma Zlota Kompania")
                        if (it.StringId != null && (it.StringId == "elephant" || it.StringId.StartsWith("rot_elephant")))
                        {
                            roster.AddToCounts(el.EquipmentElement, -el.Amount);
                            cut += el.Amount;
                            continue;
                        }
                        // legendy zalegajace z dawnych lupow w NASZYM magazynie
                        // (screen Jeffa: [STORES] Lady Forlorn) - precz
                        // 177-2 (krytyka 177 pkt 1): NIE unikaty ROT i stal valyrianska - ta czystka idzie przy KAZDYM menu, wiec klinga zdjeta
                        // do taboru (np. Lod niesiony do Qohoru), zdobyta (Take) albo odziedziczona znikala w nicosc; kopie lapie spis ValyrianBlades
                        if (LegendaryLaw.IsLegend(it) && !ValyrianBlades.Is(it) && !UniqueSpoils.Is(it))
                        {
                            roster.AddToCounts(el.EquipmentElement, -el.Amount);
                            cut += el.Amount;
                            continue;
                        }
                        // sprzet olbrzymow w ludzkich sakwach/magazynie (Jeff 14.09) -
                        // precz, chyba ze w kompanii sluza olbrzymy
                        if ((GiantGear.Is(it) && !GiantGear.PartyHasGiants(MobileParty.MainParty))
                            || (MountLaw.IsExotic(it) && !MountLaw.AllowedForParty(MobileParty.MainParty, it)))
                        {
                            roster.AddToCounts(el.EquipmentElement, -el.Amount);
                            cut += el.Amount;
                            continue;
                        }
                        // PROG KONDYCJI WYCIETY (Jeff 31.08: "wzialem pancerz
                        // z bitwy i znika w inventory"). Zuzyty lup to surowiec
                        // do NAPRAWY, nie smiec - a czystka zjadala tez WLASNY
                        // sprzet wojska zuzyty w walce (rusty_sword stan 6).
                        // Prawdziwy zlom odsiewa prog wrakow NA POLU, zanim
                        // cokolwiek trafi na ekran lupow. Decyzji gracza
                        // ("biore") nikt nie nadpisuje.
                    }
                }
                if (exoToBag > 0)
                    Log.Info("CleanseTrashInBags (r2-konie): " + exoToBag + " egzotycznych wierzchowcow nie u swoich ze zbrojowni gracza do sakw jako towar, skasowane 0.");
                if (cut > 0)
                {
                    Log.Info("CleanseTrashInBags: " + cut + " szt. (slonie-towar, zalegle legendy) wyrzucono z sakw/magazynu.");
                    Log.Player("The ruined scraps are thrown out - " + cut + " pieces past any mending.", true);
                }
            }
            catch (Exception e) { Log.Error("CleanseTrashInBags", e); }
        }

        // sklad wlasnej partii na progu bitwy - roznica po bitwie = polegli
        private Dictionary<CharacterObject, int> _fallenSnapshot;

        internal void SnapshotOwnRanks()
        {
            try
            {
                var roster = MobileParty.MainParty != null ? MobileParty.MainParty.MemberRoster : null;
                if (roster == null) { _fallenSnapshot = null; return; }
                var map = new Dictionary<CharacterObject, int>();
                for (int i = 0; i < roster.Count; i++)
                {
                    var el = roster.GetElementCopyAtIndex(i);
                    if (el.Character == null || el.Character.IsHero) continue;
                    int v; map.TryGetValue(el.Character, out v);
                    map[el.Character] = v + el.Number;
                }
                _fallenSnapshot = map;
            }
            catch { _fallenSnapshot = null; }
        }

        /// <summary>
        /// ZBIERAMY POLEGLYCH (Jeff 29.08: "zabici zolnierze MOJEJ armii -
        /// ich ekwipunek ma zasilic armoury i moge go zabrac; teraz znika").
        /// Po bitwie roznica skladu = polegli; za kazdego jego rynsztunek
        /// (wedle szablonu, ze stanem bojowym, bez choragwi/smokow/legend)
        /// wraca na wozy i jest PRZEKSIEGOWANY NA GRACZA - dowodca dysponuje
        /// sprzetem poleglych, jak w kazdej kompanii tamtych czasow.
        /// </summary>
        internal void GatherFallen()
        {
            try
            {
                if (_fallenSnapshot == null) return;
                var before = _fallenSnapshot; _fallenSnapshot = null;
                var roster = MobileParty.MainParty != null ? MobileParty.MainParty.MemberRoster : null;
                var armory = QuartermasterLaw.DteArmory();
                if (roster == null || armory == null) return;

                var now = new Dictionary<CharacterObject, int>();
                for (int i = 0; i < roster.Count; i++)
                {
                    var el = roster.GetElementCopyAtIndex(i);
                    if (el.Character == null || el.Character.IsHero) continue;
                    int v; now.TryGetValue(el.Character, out v);
                    now[el.Character] = v + el.Number;
                }

                int men = 0, pieces = 0;
                foreach (var kv in before)
                {
                    int left; now.TryGetValue(kv.Key, out left);
                    int fallen = kv.Value - left;
                    for (int m = 0; m < fallen; m++)
                    {
                        Equipment eq = null; int n = 0;
                        foreach (var e in kv.Key.BattleEquipments) { n++; if (MBRandom.RandomInt(n) == 0) eq = e; }
                        if (eq == null) continue;
                        men++;
                        for (int slot = 0; slot < 12; slot++)
                        {
                            if (slot == 4) continue;
                            var item = eq[(EquipmentIndex)slot].Item;
                            if (item == null || item.ItemType == ItemObject.ItemTypeEnum.Banner) continue;
                            if (item.StringId != null && item.StringId.StartsWith("dragon_")) continue;
                            if (LegendaryLaw.IsLegend(item) || GiantGear.Is(item)) continue;
                            // PRAWO ZACHOWANIA LUPU (audyt 30.08): FIZYCZNE sztuki
                            // poleglych zwraca juz DTE (ItemsToRecover -> magazyn) -
                            // dosypywanie szablonu bylo DRUGIM kompletem tej samej
                            // zbroi. My tylko PRZEKSIEGOWUJEMY na gracza; gdyby DTE
                            // czegos nie zwrocil, ksiege-ducha przytnie ReconcileStock.
                            StockDeposit(item.StringId, 1);   // dowodca dysponuje sprzetem poleglych
                            pieces++;
                        }
                    }
                }
                if (pieces > 0)
                {
                    Log.Info("GatherFallen: " + men + " poleglych - " + pieces
                             + " sztuk przeksiegowanych na gracza (fizyczne sztuki zwraca DTE, bez dosypywania).");
                    Log.Player("The fallen are gathered - " + pieces + " pieces of their kit come back on the wagons, yours to claim.", true);
                }
            }
            catch (Exception e) { Log.Error("GatherFallen", e); }
        }

        /// <summary>
        /// MARTWE OD 03.09 (Jeff: "nie ma byc wymiana 1:1, nic nie znika") - nie
        /// wolane nigdzie; zostawione na wypadek powrotu do pomyslu z 29.08.
        /// WYMIENIONY SPRZET ZNIKA (Jeff 29.08: "wojsko przezbraja sie w lupy,
        /// a starocie po prostu znikaja"). Wojskowa czesc magazynu (ponad wklady
        /// gracza) trzyma per TYP najwyzej tylu sztuk, ilu ludzi w kompanii
        /// (amunicja x2) - najlepsze zostaja, gorsza nadwyzka idzie w niepamiec.
        /// Wklady gracza i sztuki przypisane w ksiedze - nietykalne.
        /// </summary>
        internal void TrimWarStores()
        {
            try
            {
                var armory = QuartermasterLaw.DteArmory();
                var roster = MobileParty.MainParty != null ? MobileParty.MainParty.MemberRoster : null;
                if (armory == null || roster == null) return;
                int men = 0;
                for (int i = 0; i < roster.Count; i++)
                {
                    var e = roster.GetElementCopyAtIndex(i);
                    if (e.Character != null && !e.Character.IsHero) men += e.Number;
                }
                if (men <= 0) men = 1;

                var byType = new Dictionary<ItemObject.ItemTypeEnum, List<ItemRosterElement>>();
                for (int i = 0; i < armory.Count; i++)
                {
                    var el = armory[i];
                    var it = el.EquipmentElement.Item;
                    if (it == null || el.Amount <= 0) continue;
                    List<ItemRosterElement> list;
                    if (!byType.TryGetValue(it.ItemType, out list))
                        byType[it.ItemType] = list = new List<ItemRosterElement>();
                    list.Add(el);
                }

                int trimmed = 0;
                foreach (var kv in byType)
                {
                    int keep = kv.Key == ItemObject.ItemTypeEnum.Arrows || kv.Key == ItemObject.ItemTypeEnum.Bolts
                        ? men * 2 : men;
                    // najgorsze na poczatek - te wylatuja pierwsze
                    kv.Value.Sort((a, b) => a.EquipmentElement.ItemValue.CompareTo(b.EquipmentElement.ItemValue));
                    int total = 0;
                    foreach (var el in kv.Value) total += el.Amount;
                    int over = total - keep;
                    if (over <= 0) continue;
                    foreach (var el in kv.Value)
                    {
                        if (over <= 0) break;
                        var it = el.EquipmentElement.Item;
                        var id = it.StringId ?? "";
                        int protectedHere = Math.Min(el.Amount, StockOf(id));      // wklad gracza swiety
                        if (MusterBook.IsPinnedItem(id)) protectedHere = el.Amount; // rozkaz z ksiegi swiety
                        int cuttable = el.Amount - protectedHere;
                        if (cuttable <= 0) continue;
                        int cut = Math.Min(cuttable, over);
                        armory.AddToCounts(el.EquipmentElement, -cut);
                        over -= cut; trimmed += cut;
                    }
                }
                if (trimmed > 0)
                    Log.Info("TrimWarStores: " + trimmed + " staroci wojskowych poszlo w niepamiec (wymienione znika).");
            }
            catch (Exception e) { Log.Error("TrimWarStores", e); }
        }

        /// <summary>Smoki precz z sakw i magazynu DTE - inaczej DTE wsadza kawalerzyste na smoka w bitwie.</summary>
        internal static void CleanseDragonStables(bool shout)
        {
            int gone = 0;
            foreach (var roster in new[] { MobileParty.MainParty != null ? MobileParty.MainParty.ItemRoster : null,
                                           QuartermasterLaw.DteArmory() })
            {
                if (roster == null) continue;
                try
                {
                    for (int i = roster.Count - 1; i >= 0; i--)
                    {
                        var el = roster.GetElementCopyAtIndex(i);
                        var it = el.EquipmentElement.Item;
                        if (it == null || it.StringId == null || el.Amount <= 0) continue;
                        if (!it.StringId.StartsWith("dragon_")) continue;
                        if (it.ItemType != ItemObject.ItemTypeEnum.Horse) continue;
                        roster.AddToCounts(el.EquipmentElement, -el.Amount);
                        gone += el.Amount;
                        Log.Info("Smocza stajnia: " + it.StringId + " x" + el.Amount + " usuniety z zapasow.");
                    }
                }
                catch (Exception e) { Log.Error("CleanseDragonStables", e); }
            }
            if (gone > 0 && shout)
                Log.Player("The dragons were never yours to stable - " + gone + " struck from the rolls.", true);
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            SaveText.ReportAfterLoad();   // 161: ile dlugich napisow uratowal ratunek przy wczytaniu
            try { CombatAiLevel.Apply(); } catch (Exception e) { Log.Error("CombatAiLevel.Apply", e); }   // Jeff 16.10: SI walki kampanii na Realistic (CombatAiRealistic) + linia opcji trudnosci
            try { RecruitKit.ResolvePending("wczytanie"); } catch (Exception e) { Log.Error("RecruitKit.ResolvePending", e); }   // 161: komplety rekrutow dopiero teraz (w SyncData bohaterow nie ma)
            try { PeopleBook.ResolveOrigin("wczytanie"); } catch (Exception e) { Log.Error("PeopleBook.ResolveOrigin", e); }     // E4/7: slowniki pochodzenia partii z zapisu (w SyncData partii nie ma)
            try { VillageBook.Resolve("wczytanie albo nowa kampania"); } catch (Exception e) { Log.Error("VillageBook.Resolve", e); }   // E4/11: plik ukladu wiosek, okregi, stan z zapisu (arm_district)
            try { MapVillageLore.SessionStart(); } catch (Exception e) { Log.Error("MapVillageLore.SessionStart", e); }   // E7/W2 (233): lore wiosek (raz na proces, bramka = uklad ksiegi wyzej) i linia "Dymek wioski (E7/W2): start"
            // 171: zamowienia zamkow w drodze; zbrojownie zalog z zapisu (DTE skonczyl OnGameLoaded) albo jednorazowe odtworzenie po starym zapisie - PRZED ColdStart.Run;
            // echo ROT, gdy typu nie bylo przy starcie; cwiczenia wlasna bronia - latki modeli w kampanii (modele BK czytaja singletony BK)
            try { GarrisonCarts.ResolvePending("wczytanie"); } catch (Exception e) { Log.Error("GarrisonCarts.ResolvePending", e); }
            try { TradeCarts.ResolvePending("wczytanie"); } catch (Exception e) { Log.Error("TradeCarts.ResolvePending", e); }   // E6/R3 (215): wozy kupcow w drodze z zapisu
            try { WorkshopLaw.ResolvePending("wczytanie"); } catch (Exception e) { Log.Error("WorkshopLaw.ResolvePending", e); }   // 174.0b: robota w toku warsztatow z zapisu (przed pierwszym cyklem)
            try { MaterialOrders.ResolvePending("wczytanie"); } catch (Exception e) { Log.Error("MaterialOrders.ResolvePending", e); }   // 174.2: kontrakty surowca na karawany z zapisu
            try { CastleMarket.ResolvePending("wczytanie"); } catch (Exception e) { Log.Error("CastleMarket.ResolvePending", e); }   // E5/B4 (199): wozy w drodze do zamku, znaczniki i straz z zapisu (arm_castlemarket)
            try { ArmsScrap.ResolvePending("wczytanie"); } catch (Exception e) { Log.Error("ArmsScrap.ResolvePending", e); }   // 174b.6: zlom - pomiar zakupow i rozgrzewka z zapisu
            try { GarrisonArmory.Restore("wczytanie"); } catch (Exception e) { Log.Error("GarrisonArmory.Restore", e); }   // sklad7: jedyne odtworzenie zbrojowni zalog (K1c GarrisonKit.RestoreArmories usuniete)
            try { RecruitSources.ApplyLate(); } catch (Exception e) { Log.Error("RecruitSources.ApplyLate", e); }
            try { ArmsDrill.EnsureHooks(); } catch (Exception e) { Log.Error("ArmsDrill.EnsureHooks", e); }
            try { DrMarchArrival.EnsureHooks(); } catch (Exception e) { Log.Error("DrMarchArrival.EnsureHooks", e); }   // m97-posilki: posilki DR marszem - latki raz na proces, linia "Posilki marszem (m97-posilki): start"
            try { SkillLedger.EnsureHooks(); } catch (Exception e) { Log.Error("SkillLedger.EnsureHooks", e); }   // E8/P1 (243): ksiega umiejetnosci - latki-liczniki w kampanii (raz na proces, tylko przy SkillLedgerLog), linia startu sesji
            try { SmithXp.EnsureHooks(); } catch (Exception e) { Log.Error("SmithXp.EnsureHooks", e); }   // m109-kuznia: kowalstwo wolniej - prefiks Low na HeroDeveloper.AddSkillXp (po ROT patch30; poziom postaci bez ciecia) (raz na proces), linia "Kowalstwo wolniej (m109): start sesji"
            try { XpFair.EnsureHooks(); } catch (Exception e) { Log.Error("XpFair.EnsureHooks", e); }   // E8/X (248): doswiadczenie za skutek - latki w kampanii (raz na proces, tylko sprawy wlaczone), linia "FairXp (E8/X): start sesji"
            try { LogGate.SessionLine(); } catch (Exception e) { Log.Error("LogGate.SessionLine", e); }   // NW O4: linia "Ciche logi (NW)" - stan klucza i tryb (gra / autotest / profil)
            try { DayProfile.EnsureHooks(); } catch (Exception e) { Log.Error("DayProfile.EnsureHooks", e); }   // Z.3 poz. 3: stopery wnetrza cudzych tikow (warsztaty gry, trening gry) - latki w kampanii (raz na proces, tylko przy Stage8ProfileLog), tylko log
            try { Drill.SessionStart(); } catch (Exception e) { Log.Error("Drill.SessionStart", e); }   // musztra: zapas z zapisu, opisy perkow kwatermistrza, linia startowa
            try { FixCharcoalWeight(); } catch (Exception e) { Log.Error("FixCharcoalWeight", e); }   // wpis 87 (audyt pkt 11d): waga wegla PRZED wycena
            try { GearState.Prepare(); } catch (Exception e) { Log.Error("GearState.Prepare", e); }   // m110-stan: kopie oryginalow modyfikatorow (ten sam zestaw obiektow - powrot do XML) PRZED LootPrices
            try { LootPrices.Apply(); } catch (Exception e) { Log.Error("LootPrices", e); }   // wpis 97: cena lupu = stan
            try { ItemModels.Apply(); } catch (Exception e) { Log.Error("ItemModels.Apply", e); }   // m110-modele: przedmioty bez modelu silnika (obrazek, wyglad) - klucz MissingModelFix
            try { GearState.Apply(); } catch (Exception e) { Log.Error("GearState.Apply", e); }   // m110-stan: cztery stany sprzetu (klucz GearFourStates) - PO LootPrices, PRZED wycena (McmSettings, ArmsPricing, StartStock)
            try { McmSettings.Apply(); MaterialLaw.Apply(); ArmsPricing.Build(); HistoricalPrices.Apply(); WorkshopRecipes.Apply(); SlaughterHides.Apply(); WorkshopInputsFix.Apply(); StartStock.Run(); ArmsPricing.ClearCostCache(); MapClock.ApplySpeed(); UniqueSpoils.OnSessionLaunched(); ColdStart.Run(); } catch (Exception e) { Log.Error("MaterialLaw/ArmsPricing", e); }   // surowce PRZED wycena uzbrojenia; StartStock zaraz PO Apply (przelicznik ladunku juz obowiazuje)
            try { VillageBook.PlacePendingAll(); } catch (Exception e) { Log.Error("VillageBook.PlacePendingAll", e); }   // Jeff 16.10 (D5, po recenzji): puste osady z zapisu z innym ukladem wiosek na wioski od razu - PO McmSettings.Apply (pulap DevastationMaxPercent z MCM), przed pierwszym tickiem
            try { VillageHamlets.SessionStart(); } catch (Exception e) { Log.Error("VillageHamlets.SessionStart", e); }   // sw-przysiolki: linia "Przysiolki (sw-przysiolki): start sesji" - uklad v6 wobec v5, najblizsza sasiadka, przysiolki na ladzie (po MCM)
            try { CartGuard.SessionStart(); } catch (Exception e) { Log.Error("CartGuard.SessionStart", e); }   // m106-wozy W2: linia "Wozy (m106): start sesji" (klucze i pamiec tras z zapisu) - po McmSettings.Apply
            try { M107Start.SessionStart(); } catch (Exception e) { Log.Error("M107Start.SessionStart", e); }   // m107-gospodarka: linia "Gospodarka (m107): start sesji" (klucze K1-K6) - po McmSettings.Apply
            try { CanonHouses.SessionStart(); } catch (Exception e) { Log.Error("CanonHouses.SessionStart", e); }   // po recenzji D2 (16.10): plik rodow kanonu (1.4 MB) przy ladowaniu sesji, nie w pierwszym ticku proporczykow - PO McmSettings.Apply (klucze VillageBannerPerHolder / VillageCanonHouses)
            try { FixedSums38.SessionStart(); } catch (Exception e) { Log.Error("FixedSums38.SessionStart", e); }   // 3.8: TXP (drugi raz) i linia "Stale kwoty (3.8)" - PO HistoricalPrices.Apply
            try { CraftOrders8.SessionStart(); } catch (Exception e) { Log.Error("CraftOrders8.SessionStart", e); }   // E8/K (255): zlecenia kowalskie sprzed sesji (zapis, start nowej kampanii) - projekt i nagroda w nowej monecie; PO HistoricalPrices.Apply
            try { CraftOrdersPurse.SessionStart(); } catch (Exception e) { Log.Error("CraftOrdersPurse.SessionStart", e); }   // NZ (16.10): zlecenia kowalskie z zapisu bez pokrycia w kiesie zleceniodawcy (suma jego zlecen) albo zmarlego zdjete; PO CraftOrders8.SessionStart (nagrody juz przeliczone)
            try { QuestPay8.SessionStart(this); } catch (Exception e) { Log.Error("QuestPay8.SessionStart", e); }   // E8/D1 (256): nagrody i wyplaty w zadaniach - nasluchy przyjecia zadan; skan metod zadan i latki raz na proces, W KAMPANII (CLAUDE.md 7)
            try { QuestQty28.SessionStart(this); } catch (Exception e) { Log.Error("QuestQty28.SessionStart", e); }   // f28-wyceny (A): ilosci w zadaniach od wartosci sprzed przeliczenia - latki raz na proces, W KAMPANII (CLAUDE.md 7); PO QuestPay8 i HistoricalPrices.Apply
            try { QuestGangAxes.SessionStart(); } catch (Exception e) { Log.Error("QuestGangAxes.SessionStart", e); }   // m105-drobne (T-4): sprawa broni gangu ze srednia wazona ceny siekier (gra: srednia 0 -> 20000 / 0) - latka konstruktora raz na proces W KAMPANII (CLAUDE.md 7), naprawa zepsutych spraw z zapisu
            try { RogueryXp28.SessionStart(); } catch (Exception e) { Log.Error("RogueryXp28.SessionStart", e); }   // f28-wyceny (B): XP Lotrostwa za najazd / rabunek / wymuszenie od wartosci towarow sprzed przeliczenia - latki raz na proces, W KAMPANII
            try { Wyceny20.SessionStart(); } catch (Exception e) { Log.Error("Wyceny20.SessionStart", e); }   // n20-wyceny: reszta audytu wycen (XP Handlu z warsztatow, lup AI, kryjowki, cel BK Acquire Book) - latki raz na proces, W KAMPANII, PO HistoricalPrices.Apply
            try { PlayerFees8.SessionStart(); } catch (Exception e) { Log.Error("PlayerFees8.SessionStart", e); }   // E8/D2 (257): nagroda turnieju gracza, zaklady, oplaty i drobne zrodla gracza - latki raz na proces, W KAMPANII (CLAUDE.md 7); PO QuestPay8
            try { TourneyPay.SessionStart(); } catch (Exception e) { Log.Error("TourneyPay.SessionStart", e); }   // TP (16.10): linia "Turniej-platnik (TP): start sesji", decyzja wyrownania Seagard (D3, raz: zapis sprzed zmiany, Ithauser, czas >= 109020) - bez przelewow (komunikat i splata w pierwszej godzinie)
            try { DailyLife.SessionStart(); } catch (Exception e) { Log.Error("DailyLife.SessionStart", e); }   // ZC0 (16.10): linia "Zycie (ZC0): start sesji" - klucze i skala targu sigma (z zapisu albo z MCM)
            try { DailyLife.EstatesSessionStart(); } catch (Exception e) { Log.Error("DailyLife.EstatesSessionStart", e); }   // ZC2 (16.10): linia "Stany (ZC2): start sesji" - klucz, ksiega stanow z zapisu, widok
            try { DailyLife.MobilitySessionStart(); } catch (Exception e) { Log.Error("DailyLife.MobilitySessionStart", e); }   // ZC6 (16.10): postfiks dobrobytu na czynnym modelu (raz na metode) i linia "Awans (ZC6): start sesji"
            try { DailyLife.WarSessionStart(); } catch (Exception e) { Log.Error("DailyLife.WarSessionStart", e); }   // ZC7 (16.10): linia "Blizna wojny (ZC7): start sesji" - klucz, dlug blizny z zapisu
            try { StartCalibration.SessionStart(); } catch (Exception e) { Log.Error("StartCalibration.SessionStart", e); }   // KS (kalibracja startu): plik ziemi, stan z zapisu (arm_ks), linia "Kalibracja startu (KS): start sesji" - PO VillageBook.Resolve i McmSettings.Apply
            try { KsFoodCarts.ResolvePending("wczytanie"); } catch (Exception e) { Log.Error("KsFoodCarts.ResolvePending", e); }   // KS-R3 G1: wozy KS-H w drodze z zapisu
            try { VillageCrafts.SessionStart(); } catch (Exception e) { Log.Error("VillageCrafts.SessionStart", e); }   // inwest: linia "Zajecia wiosek (ZW): start" - PO VillageBook.Resolve i StartCalibration.SessionStart (plik ziemi KS z nakladka v6)
            try { DistrictWorks.SessionStart(); } catch (Exception e) { Log.Error("DistrictWorks.SessionStart", e); }   // inwest: prace okregu z zapisu i dodatek czesci przed pierwszym rachunkiem KS doby
            try { TownInns.SessionStart(); } catch (Exception e) { Log.Error("TownInns.SessionStart", e); }   // KA (16.10): linia "Karczmy (KA): start sesji" - klucze, latki, stan noclegu z zapisu
            try { RoadsideInns.SessionStart(); } catch (Exception e) { Log.Error("RoadsideInns.SessionStart", e); }
            try { SideVillageVisit.SessionStart(); } catch (Exception e) { Log.Error("SideVillageVisit.SessionStart", e); }   // m97-wioski: linia "Wioski poboczne (WP): start sesji"   // sw-zajazdy: linia "Zajazdy (ZJ): start sesji" - plik, zajazdy wedlug ludzi, najblizszy sasiad
            try { HarvestFairs.SessionStart(); } catch (Exception e) { Log.Error("HarvestFairs.SessionStart", e); }   // R3-05a: latka popytu (raz na proces), kalendarz krain, linia "Jarmarki po zniwach (R3-05a): start sesji"
            try { RiverBarges.SessionStart(); } catch (Exception e) { Log.Error("RiverBarges.SessionStart", e); }   // R3-05b: plik arm_rivers.tsv, latka oceny miast BK (raz na proces), linia "Barki rzeczne (R3-05b): start sesji"
            try { Rumours.SessionStart(); } catch (Exception e) { Log.Error("Rumours.SessionStart", e); }   // sw-zajazdy: linia "Plotki (PL): start sesji" - klucz, ksiega z zapisu
            try { InnDrink.SessionStart(); } catch (Exception e) { Log.Error("InnDrink.SessionStart", e); }   // m98-plotki: linia "Kufle (m98): start sesji" - klucze, krzywa Uroku, prog upicia
            try { Lektor.SessionStart(); } catch (Exception e) { Log.Error("Lektor.SessionStart", e); }   // m103-lektor: linia "Lektor (m103): start sesji" - klucze, nagrania okien, latka, pamiec plotek, klucz API jest/brak, tryb
            try { BanditLimit.SessionStart(); } catch (Exception e) { Log.Error("BanditLimit.SessionStart", e); }   // f8-bandy (noc 18.10, decyzja Jeffa "7. b"): limit band ROT x2 z ludzi puli wyrzutkow, stopery kosztu band - latki W KAMPANII (raz na proces), linia "Bandy i limit (f8-bandy): start sesji"
            try { CompanionPay.SessionStart(); } catch (Exception e) { Log.Error("CompanionPay.SessionStart", e); }   // noc 15/16.10 (C): towarzysze - czynnik umiejetnosci morskich BK i cena werbunku wedlug zoldu; latki raz na proces, W KAMPANII, samosprawdzenie
            try { CompanionWage.SessionStart(); } catch (Exception e) { Log.Error("CompanionWage.SessionStart", e); }   // CW (19.10): zold towarzyszy gracza wedlug poziomu - latki TroopWage i GetHeroWage, PO CompanionPay (BaseWage BK)
            try { CompanionGear.SessionStart(); } catch (Exception e) { Log.Error("CompanionGear.SessionStart", e); }   // CG (19.10): bramka doby od nowa
            try { CompanionAutotest.SessionStart(); } catch (Exception e) { Log.Error("CompanionAutotest.SessionStart", e); }   // CW/CG (recenzja 19/20.10): proba autotestu od nowa
            try { LevelPoints.SessionStart(); } catch (Exception e) { Log.Error("LevelPoints.SessionStart", e); }   // RP (decyzja Jeffa 14.10): linia "Rozwoj postaci (RP): start sesji" - klucze z MCM (PO McmSettings.Apply), czynny model, przyklad punktow
            try { PregnancyYear.SessionStart(); } catch (Exception e) { Log.Error("PregnancyYear.SessionStart", e); }   // CR (zasada Jeffa 14.10): linia "Ciaza i plodnosc (CR): start sesji" - klucze z MCM (PO McmSettings.Apply), rok i mnoznik, czynny model, ciaze w toku
            try { SmithCoal.SessionStart(); } catch (Exception e) { Log.Error("SmithCoal.SessionStart", e); }   // N2 po recenzji (noc 16.10): wegiel broni i przetopu z ekranu kuzni w kg z otwartej bryly (po SmithWood - te same cele, wlasna instancja Harmony)
            try { SmeltTruth.SessionStart(); } catch (Exception e) { Log.Error("SmeltTruth.SessionStart", e); }   // n20-kuznia: linia "Kuznia przetop (n20-kuznia): start sesji" - klucze, samosprawdzenie przetopu broni wedlug czesci, pancerz liczony raz, kwit tarczy BK
            try { NightEcon.SessionStart(); } catch (Exception e) { Log.Error("NightEcon.SessionStart", e); }   // noc 16.10 (N2, N3, P8a, N9, N10, N11, N12): linie "Noc-ekonomia (NE): start sesji" - PO HistoricalPrices.Apply i SmithWood.SessionStart
            try { SmithWood.SessionStart(); } catch (Exception e) { Log.Error("SmithWood.SessionStart", e); }   // Jeff 15.10: drewno w kuzni w kg - latki na czynny model kosztow i SpendMaterials
            try { PlayerArmoryView.SessionStart(); } catch (Exception e) { Log.Error("PlayerArmoryView.SessionStart", e); }   // Jeff 15.10 (a/b/c): zbrojownia gracza - czyje co jest; linia "Zbrojownia gracza (15.10): start sesji" (PO McmSettings.Apply)
            try { ArmouryZb.SessionStart(); } catch (Exception e) { Log.Error("ArmouryZb.SessionStart", e); }   // ZB (noc 17/18.10, przeglad 16.10): linia "Zbrojownia gracza (ZB): start sesji" - klucze ZB-1..ZB-6 i samosprawdzenie ksiegi per modyfikator (PO McmSettings.Apply)
            try { GearFit.SessionStart(); } catch (Exception e) { Log.Error("GearFit.SessionStart", e); }   // GearFit (po recenzji): filtr kompletu t1 DTE dopiero od startu sesji (OnGameLoaded DTE biegnie przed prawami CrashScribe), pamiec wzorcow od nowa
            try { ArmyCohesionYear.SessionStart(); } catch (Exception e) { Log.Error("ArmyCohesionYear.SessionStart", e); }   // AC (Jeff 14.10): spojnosc armii x 84 / rok - postfiks na metode czynnego modelu armii (raz na proces), linia "Spojnosc armii (AC): start sesji"
            try { MissionTeams.SessionStart(); } catch (Exception e) { Log.Error("MissionTeams.SessionStart", e); }   // NM (noc 15/16.10): straznik druzyn w misji i ucieczka rannych tylko w bitwie - linia "Druzyny w misji (NM): start sesji" (PO McmSettings.Apply), liczniki sesji od zera
            try { RoutHold.SessionStart(); } catch (Exception e) { Log.Error("RoutHold.SessionStart", e); }   // NB2 (16.10): zlamani raz na bitwe i postura szeregowych wedlug tieru - linia "Bitwa (NB2): start sesji" (PO McmSettings.Apply), liczniki sesji od zera
            try { DuelAi.SessionStart(); } catch (Exception e) { Log.Error("DuelAi.SessionStart", e); }   // m111-walka (24.10): linia "DuelAi tabela" - profil AI w pojedynku przy umiejetnosci 60..250, klucze pojedynku, stan hakow (PO McmSettings.Apply)
            try { DayHeld.SessionStart(); } catch (Exception e) { Log.Error("DayHeld.SessionStart", e); }   // A14 / G20 (drobne2): doba po wczytaniu ze srodka doby - PO McmSettings.Apply (klucz z MCM), przed pierwszym tickiem osad
            try { VillageClimate.Apply(); } catch (Exception e) { Log.Error("VillageClimate.Apply", e); }   // T8: typ wsi wedlug klimatu (PO McmSettings.Apply; bez zapisu)
            try { GoodsRates.Apply(); } catch (Exception e) { Log.Error("GoodsRates.Apply", e); }   // GL-5 (P5): 16 wsi lnianych (tylko kampania zaczeta z kluczem, potem na stale) - PO VillageClimate.Apply, PRZED kalibracja plonu P1 (1. doba nowej kampanii)
            try { VineOlive.Apply(); } catch (Exception e) { Log.Error("VineOlive.Apply", e); }   // B6: 8 wsi poludnia -> winnice (tylko kampania zaczeta z kluczem, potem na stale) - PO GoodsRates.Apply, PRZED kalibracja plonu P1
            try { VillageFruit.SessionStart(); } catch (Exception e) { Log.Error("VillageFruit.SessionStart", e); }   // noc 15/16.10 (B1): owoce i miod wsi - linia startu PO zmianach typow wsi (VillageClimate, GoodsRates, VineOlive)
            try { VillageFruit.EggSessionStart(); } catch (Exception e) { Log.Error("VillageFruit.EggSessionStart", e); }   // noc 16.10 (noc-ekonomia2, N2-E1): jaja wsi rolniczych - linia startu (liczba wsi, oczekiwany plon) PO zmianach typow wsi
            try { VillageFruit.HandsSessionStart(); } catch (Exception e) { Log.Error("VillageFruit.HandsSessionStart", e); }   // E3-D1 (noc-ekonomia3 czesc 2): rece wsi za cena - przeliczenie f i c z dzisiejszych cen i linia startu, PO liniach owocow i jaj
            try { FoodSubstitute.SessionStart(); FoodThrift.SessionStart(); } catch (Exception e) { Log.Error("FoodSubstitute/FoodThrift.SessionStart", e); }   // noc 15/16.10 (B2, B3): linie startu sesji (klucze z MCM)
            try { NoMakerBudget.SessionStart(); } catch (Exception e) { Log.Error("NoMakerBudget.SessionStart", e); }   // noc 16.10 (N2-E2): budzet kategorii bez producenta na zbytki - linia startu (klucz z MCM)
            try { FibreToMarket.SessionStart(); } catch (Exception e) { Log.Error("FibreToMarket.SessionStart", e); }   // noc 16.10 (N2-E3): len, welna i skory do miasta targowego - linia startu, pamiec wozow od zera
            try { TownFolk.FirewoodSessionStart(); } catch (Exception e) { Log.Error("TownFolk.FirewoodSessionStart", e); }   // noc 16.10 (N2-E4): opal mieszczan - linia startu
            try { MapRoadsProbe.SessionStart(); } catch (Exception e) { Log.Error("MapRoadsProbe.SessionStart", e); }   // E7/D0 (239): linia "MapRoadsProbe: wylaczona" / "wlaczona" - PO McmSettings.Apply (wczesniej klucz z Armoury.settings.xml, nie z MCM)
            try { MapRoads.SessionStart(); } catch (Exception e) { Log.Error("MapRoads.SessionStart", e); }   // SW-DROGI (noc 17/18.10): plik drog ROT do siatki, pamiec partii od zera, linia "Drogi (DR): start sesji" - PO McmSettings.Apply
            try { MapMud.SessionStart(); } catch (Exception e) { Log.Error("MapMud.SessionStart", e); }   // p5-bloto (R3-07c): linia "Bloto (BL): start sesji" - PO MapRoads.SessionStart (siatka drog)
            try { Gallows.SessionStart(); } catch (Exception e) { Log.Error("Gallows.SessionStart", e); }   // p6-szubienice (R3-07a): trakty z pliku drog, przeterminowane z zapisu, linia "Szubienice (p6): start sesji"
            try { SeaStorms.SessionStart(); } catch (Exception e) { Log.Error("SeaStorms.SessionStart", e); }   // p6-sztormy (R3-05d): linia "Sztormy (MS): start sesji" - pora, lista burz, mnoznik zimy
            try { AiRoadRoutes.SessionStart(); } catch (Exception e) { Log.Error("AiRoadRoutes.SessionStart", e); }   // p6-drogi (R3-07d): siec drog dla tras AI, linia "AI drogi (AD): start sesji" - PO MapRoads.SessionStart (siatka drog)
            try { MapSideRoads.SessionStart(); } catch (Exception e) { Log.Error("MapSideRoads.SessionStart", e); }   // f11-drogi: linia "Drogi boczne (f11): wylaczone / wlaczone" i plik-przelacznik autotestu od nowa - PO McmSettings.Apply
            try { PrisonerLaw.SessionLine(); } catch (Exception e) { Log.Error("PrisonerLaw.SessionLine", e); }   // I1b: krainy z niewola (warownie, krolestwa H3) - tylko log
            try { VillageWoodlot.Calibrate(true); } catch (Exception e) { Log.Error("VillageWoodlot.Calibrate", e); }   // T8: stala lasu wedlug klimatu PO zmianie typow (drwale)
            try { Stables.BuildRiderMap(); } catch (Exception e) { Log.Error("Stables.BuildRiderMap", e); }
            try { HorseCensus.ApplyLate(); } catch (Exception e) { Log.Error("HorseCensus.ApplyLate", e); }   // 175.0: latka ROT, gdy typu nie bylo przy starcie gry
            try { NorthHomeEdge.BuildSet(); } catch (Exception e) { Log.Error("NorthHomeEdge.BuildSet", e); }   // 175.4b: zbior 43 (drzewa i szablony juz po latce CS 175.3)
            try { KingdomBalance.SessionStart(); } catch (Exception e) { Log.Error("KingdomBalance.SessionStart", e); }   // 175.0: twierdze na starcie sesji (tylko log)
            try { HorseCensus.SessionStart(); ArmoryFit.Arm(); } catch (Exception e) { Log.Error("HorseCensus.SessionStart", e); }   // przeglad 175: stan wylacznikow 175 i znacznikow CS (B0); stare zbrojownie w 1. godzinie
            try { MountedWage.EnsureContextHooks(); } catch (Exception e) { Log.Error("MountedWage.EnsureContextHooks", e); }   // paczka 160: zold partii - karawany bez premii konnego (latka w kampanii)
            try { CirculationWindows.EnsureModelHooks(); } catch (Exception e) { Log.Error("CirculationWindows.EnsureModelHooks", e); }   // paczka 169: linie modelu finansow i kapital nowych karawan (latki w kampanii, tylko log)
            try { Measure169c.EnsureHooks(); } catch (Exception e) { Log.Error("Measure169c.EnsureHooks", e); }
            try { CrownIncome.EnsureHooks(); } catch (Exception e) { Log.Error("CrownIncome.EnsureHooks", e); }
            try { GarrisonRecruitFee.EnsureHooks(); } catch (Exception e) { Log.Error("GarrisonRecruitFee.EnsureHooks", e); }   // E5 decyzja 9 - latka na DefaultClanFinanceModel dopiero w kampanii
            try { PeopleLevy.EnsureHooks(); } catch (Exception e) { Log.Error("PeopleLevy.EnsureHooks", e); }   // GL-4 (P4): latki przyrostu milicji gry i limitu milicji BK - w kampanii, raz na proces (pole statyczne GameTexts w DefaultSettlementMilitiaModel)
            try { Leaks310.EnsureHooks(); } catch (Exception e) { Log.Error("Leaks310.EnsureHooks", e); }   // 3.10: zold zaciagu ROT od pana (latka modelu finansow ROT - w kampanii)
            try { ClanBudget.EnsureHooks(); } catch (Exception e) { Log.Error("ClanBudget.EnsureHooks", e); }
            try { OverflowHome.EnsureHooks(); } catch (Exception e) { Log.Error("OverflowHome.EnsureHooks", e); }   // W4c: przepelnienie partii i zalog do domu (okno dezercji gry w kampanii)
            try { FolkAidLoyalty.EnsureHooks(); } catch (Exception e) { Log.Error("FolkAidLoyalty.EnsureHooks", e); }   // P1b: kara lojalnosci za danine - postfiks na czynny model lojalnosci (BK ma pola statyczne z GameTexts - tylko w kampanii)
            try { KsHunger.EnsureHooks(); } catch (Exception e) { Log.Error("KsHunger.EnsureHooks", e); }   // r4-glodbunt: glod miast KS -> "Hunger" w czynnych modelach lojalnosci i bezpieczenstwa (w kampanii), pomiar posilku; stan sesji glodu i KsLordFood od nowa
            try { TownGranary.EnsureHooks(); } catch (Exception e) { Log.Error("TownGranary.EnsureHooks", e); }   // p6-spichlerz (R3-06f): bezpiecznik BetterEconomy (typ BE w kampanii) i linia startu sesji
            try { GentryService.EnsureHooks(); } catch (Exception e) { Log.Error("GentryService.EnsureHooks", e); }   // 179: wezwanie rycerza i rozwiazanie jego partii (BK), zakupy majatkow BK w przydziale sprzetu 166
            try { Ransom178.EnsureHooks(); } catch (Exception e) { Log.Error("Ransom178.EnsureHooks", e); }   // 178: okupy wedlug majatku, kurier, posrednik, 1/9 lupu - latki w kampanii
            try { RotLeaks37.EnsureHooks(); } catch (Exception e) { Log.Error("RotLeaks37.EnsureHooks", e); }   // 3.7: podmiana rekruta ROT i rebelia bez zlota z niczego (transpilery raz na proces)
            try { CrownCallToWar.EnsureHooks(); } catch (Exception e) { Log.Error("CrownCallToWar.EnsureHooks", e); }   // 168 dodatek: wezwanie do wojny placi korona - latki modelu finansow i sojuszy w kampanii
            try { WorldTime7.EnsureHooks(); } catch (Exception e) { Log.Error("WorldTime7.EnsureHooks", e); }   // E7/K (226): turnieje co 3. tydzien, sojusz 364 / wezwanie 182, rozejm gry ok. 87 dni - latki w kampanii (raz na proces), linia "Czas swiata (E7/K)"
            try { CampScene.StartLine(); } catch (Exception e) { Log.Error("CampScene.StartLine", e); }   // E7/O2 (229): linia startowa "CampScene (E7/O2)" - prefaby obozu sprawdzone raz na proces (PrefabExists), tylko przy CampBattlePropsEnabled
            try { StateHygiene.SessionStart(); } catch (Exception e) { Log.Error("StateHygiene.SessionStart", e); }   // E8/S (246): linia startowa "Porzadek stanu (E8/S)" - ile klas wyzerowal konstruktor tej kampanii i co zostalo w pamieci po poprzedniej (tylko log)
            try { Seal31.EnsureHooks(); } catch (Exception e) { Log.Error("Seal31.EnsureHooks", e); }   // 3.1 (31): latki w kampanii (portfele wezwan, modele karawan, DTE) - PO CrownCallToWar i oknach ksiegi
            try { SiegePhantomLift.SessionStart(); } catch (Exception e) { Log.Error("SiegePhantomLift.SessionStart", e); }   // OB6 (noc-drobne): linia "Oboz bez sily (OB6): start sesji", pamiec serii od zera
            try { SiegeWalls.SessionStart(); } catch (Exception e) { Log.Error("SiegeWalls.SessionStart", e); }   // mury oblezen: linia startowa (modele, latki, wlasciciele latek Harmony) - PO Seal31.EnsureHooks
            try { DebtLadder.EnsureHooks(); } catch (Exception e) { Log.Error("DebtLadder.EnsureHooks", e); }   // 168: portfele gry bez zaliczki (OBIEG-1), B5 (karawany i warsztaty BK) - latki w kampanii
            try { MercTenure.EnsureHooks(); } catch (Exception e) { Log.Error("MercTenure.EnsureHooks", e); }   // M6a-1: najem gry i zwolnienie / przejscie BK (latki w kampanii)
            try { PeaceHold.EnsureHooks(); } catch (Exception e) { Log.Error("PeaceHold.EnsureHooks", e); }   // M6a-3: uwolnienie jencow przy pokoju krolestw po 24 h (latka w kampanii)
            try { MercContract.EnsureHooks(); } catch (Exception e) { Log.Error("MercContract.EnsureHooks", e); }   // 185: kontrakt gry i "za tier" AI = 0 (latki modelu finansow - w kampanii)   // 166: limity partii i zalog, dezercja z limitu, nowe partie, werbunek Strazy (latki w kampanii)   // 165: splata dlugu wobec korony do skarbca (latka modelu finansow - w kampanii)   // 169c: okna pomiaru etapu 2 (okupy, zalogi, rycerze, dezercja, konsumpcja, Inni) - latki w kampanii, tylko log
            try { CouncilWages35.EnsureHooks(); } catch (Exception e) { Log.Error("CouncilWages35.EnsureHooks", e); }   // 3.5: rada BK - platnik i urzad radnego (latki BK w kampanii), liczniki od zera
            try { EstateIncome187.EnsureHooks(); } catch (Exception e) { Log.Error("EstateIncome187.EnsureHooks", e); }
            try { SideVillageFief.EnsureHooks(); } catch (Exception e) { Log.Error("SideVillageFief.EnsureHooks", e); }   // n20-wioska: start BK "Indebted Lord" (TitleManager.GiveLordshipOnKingdomJoin) i linia podpowiedzi zlota - latki w kampanii (CLAUDE.md 7)   // 3.1/3.3 (31b) E3/2: majatki BK bez dochodu z produkcji (latki w kampanii), srednie i liczniki od zera
            try { Seal31B.EnsureHooks(); } catch (Exception e) { Log.Error("Seal31B.EnsureHooks", e); }   // 3.1/3.3 (31b) E3/4, E3/8, E3/9: latki w kampanii, liczniki od zera
            try { PerfD2.EnsureHooks(); } catch (Exception e) { Log.Error("PerfD2.EnsureHooks", e); }   // perf-d2d3: straznik i prefiks podatku miast BK (PerfTownTaxSkipBk) - PO PopulationLaw (SubModule), CouncilWages35 i Seal31B; linia startowa "Szybkosc doby (perf-d2d3)"   // 3.1/3.3 (31b) E3/4, E3/8, E3/9: latki w kampanii, liczniki od zera
            try { KingdomTreasury.OptimStartLine(); } catch (Exception e) { Log.Error("KingdomTreasury.OptimStartLine", e); }   // m99-optym: linia startu "Optym (m99): start" (wlaczone optymalizacje)
            try { FiefGrants.EnsureHooks(); } catch (Exception e) { Log.Error("FiefGrants.EnsureHooks", e); }   // PROJEKT-LENNA (14.10): regula N nadan zdobytych twierdz - latki na model BK / BKROT i decyzje gry w kampanii (raz na proces), linia "Nadania lenn (N)"
            try { BkrotChains.EnsureHooks(); } catch (Exception e) { Log.Error("BkrotChains.EnsureHooks", e); }   // P4 audytu (AM-13, AM-09, AM-19): lancuchy modeli pod BKROT (predkosc, mapa, morale w bitwie, zold) - latki raz na proces, linia "Lancuchy modeli BKROT (P4): start"
            try { ShipsToPort181.Reset(); } catch (Exception e) { Log.Error("ShipsToPort181.Reset", e); }   // 3.1/3.3 (33) 181: statki do portu - stan dzienny i konteksty od zera w kazdej kampanii (latki wpiete przy starcie gry)
            try { PeopleScale.SessionStart(); } catch (Exception e) { Log.Error("PeopleScale.SessionStart", e); }   // GL-0: pamiec skal od pierwszej godziny sesji (po wczytaniu; stary zapis - kalibracja tu) - PO McmSettings.Apply i ksiegach z zapisu, PRZED RawPrice.SeedNewCampaign (recenzja kodu GL W1: pamiec popytu nowej kampanii z m_t od pierwszej doby)
            try { VillageByPeople.SessionStart(); } catch (Exception e) { Log.Error("VillageByPeople.SessionStart", e); }   // GL-1 (P1): plon wsi od ludzi - g z zapisu albo kalibracja (stary zapis), przejscie stanu (regulator wsi od nowa) - PO PeopleScale.SessionStart
            try { PeopleLevy.SessionStart(); } catch (Exception e) { Log.Error("PeopleLevy.SessionStart", e); }   // GL-4 (P4): czynniki poboru i milicji od ludzi z pamieci skal sesji - PO PeopleScale.SessionStart
            try { RawPrice.SeedNewCampaign(); } catch (Exception e) { Log.Error("RawPrice.SeedNewCampaign", e); }   // cena surowcow: w nowej kampanii pamiec rynku z tickow startowych na nowa monete - PO HistoricalPrices.Apply i StartStock.Run
            try { TownCrafts.SessionStart(); } catch (Exception e) { Log.Error("TownCrafts.SessionStart", e); }   // paczka 148: proporcje rzemiosla miasta z wartosci - PO HistoricalPrices.Apply (linia startowa)
            try { CaravanAmmo.SessionStart(); } catch (Exception e) { Log.Error("CaravanAmmo.SessionStart", e); }   // paczka 172b: kategorie amunicji i stan latek (linia startowa)
            try { ArmsLeaks.SessionStart(); } catch (Exception e) { Log.Error("ArmsLeaks.SessionStart", e); }   // paczka 174.0: linia startowa (ujscia uzbrojenia zamkniete, BK bron i tarcze, konserwacja zapasu)
            try { WorkshopLaw.HandsStartLine(); } catch (Exception e) { Log.Error("WorkshopLaw.HandsStartLine", e); }   // 174.3: linia "Rece (174)" - po ColdStart (ten liczy dawnymi rekami)
            try { TownFletchers.SessionStart(); } catch (Exception e) { Log.Error("TownFletchers.SessionStart", e); }   // paczka 172: kandydaci strzelarzy, koszyki wzorcow, linia startowa - PO HistoricalPrices.Apply i ColdStart
            try { PriceList8.StartLine(); } catch (Exception e) { Log.Error("PriceList8.StartLine", e); }   // E8/C (253): linie startu cennika (tylko log) - PO HistoricalPrices.Apply i ClearCostCache
            try { Recipes.AmmoStartLine(); } catch (Exception e) { Log.Error("Recipes.AmmoStartLine", e); }   // sklad8-s S1: kwity amunicji kuzni gracza (tylko log) - PO HistoricalPrices.Apply (wartosci sztabek w pensach)
            // 124: kapital startowy warsztatow w nowej monecie - TU, po HistoricalPrices.Apply. Gra wola sluchaczy zdarzenia od ostatnio
            // dopisanego (MbEvent: lista z wstawianiem na poczatek), wiec WorkshopTradeBehavior (dodany po nas) szedl PRZED przeliczeniem cen
            try { var seed = WorkshopTrade.SeedNewCampaign(); if (seed != null) Log.Info("WorkshopTrade: " + seed); } catch (Exception e) { Log.Error("WorkshopTrade.SeedNewCampaign", e); }
            try { WearGroups.Fix(); } catch (Exception e) { Log.Error("WearGroups.Fix", e); }
            try { GearState.SessionLine(); } catch (Exception e) { Log.Error("GearState.SessionLine", e); }   // m110-stan (recenzja R3): linia "rynsztunek i sakwy gracza" PO WearGroups.Fix (uprzaz, luki i kusze z grupa) - stan z zapisu, przed wyrownaniem
            try { ResyncWornStates(); } catch (Exception e) { Log.Error("ResyncWornStates", e); }   // m110-stan (recenzja R6): nazwa i cena sztuk na grzbiecie wedlug ksiegi (progi 80/40/10) - PO WearGroups.Fix
            HeroHp.StartLine();   // m110-stan krok 2: linia "HP sprzetu (m110): start" (klucze, latka, ksiegi, kontrola krzywej) - PO wyrownaniu nazw; wlasny try w srodku
            try { RaceLaw.OnSessionLaunched(); } catch (Exception e) { Log.Error("RaceLaw.OnSessionLaunched", e); }   // Jeff 10.10: postfiks rasy takze na nadpisaniu w modelu slubu, ktory gra uzywa (ROT); linia "Rasy: start sesji"
            try { AiDowry.OnSessionLaunched(); } catch (Exception e) { Log.Error("AiDowry.OnSessionLaunched", e); }   // R3-01c: latka kontraktu BK i przechyl swatow na modelu slubu, ktory gra uzywa (ROT)
            try { SellByCondition.OnSessionLaunched(); } catch (Exception e) { Log.Error("SellByCondition.OnSessionLaunched", e); }   // cena sprzedazy sprzetu: wylacznik kary BK z MCM (PO McmSettings.Apply wyzej) i kontrola "Kara handlowa BK: x5.0"; ksiega skupu od zera
            try { CleanseNegativeStacks(); } catch (Exception e) { Log.Error("CleanseNegativeStacks", e); }
            try { TryRestoreArmoryWear("sesja"); } catch (Exception e) { Log.Error("TryRestoreArmoryWear", e); }
            try { if (!LoreSeedFix.On) LorePurgeOnce(); } catch (Exception e) { Log.Error("LorePurgeOnce", e); }   // m96-wzory: przy kluczu - w OnAfterSessionLaunched (LoreSeedFix)
            try { CleanseDragonStables(true); } catch (Exception e) { Log.Error("CleanseDragonStables", e); }
            try { CleanseTrashInBags(); } catch (Exception e) { Log.Error("CleanseTrashInBags", e); }
            try { QualityRich.Enrich(); } catch (Exception e) { Log.Error("QualityRich.Enrich", e); }
            try { CastleSteel.Rename(); } catch (Exception e) { Log.Error("CastleSteel.Rename", e); }   // 177-1: Iron6 = "Castle-forged Steel", perk Steel Maker 3, "Thamaskene" w nazwach
            try { Restitution0831(); } catch (Exception e) { Log.Error("Restitution0831", e); }
            try { IronBank.AddMenus(starter); } catch (Exception e) { Log.Error("IronBank.AddMenus", e); }
            try { GarrisonKit.AddMenus(starter); } catch (Exception e) { Log.Error("GarrisonKit.AddMenus", e); }   // K1 (B6): "Hand kit to the garrison"
            try { PeopleMenu.AddMenus(starter); } catch (Exception e) { Log.Error("PeopleMenu.AddMenus", e); }     // E4/7 (U21): "People of this land" - okno ksiegi ludzi regionu
            try { TownDebt.AddMenus(starter); } catch (Exception e) { Log.Error("TownDebt.AddMenus", e); }   // m96-dlugi: "The council's debts" w menu miasta (m95-menu przenosi do Town affairs)
            try { TownFolk.TownLoanMenus(starter); } catch (Exception e) { Log.Error("TownFolk.TownLoanMenus", e); }   // f8-pozyczka: "Borrow from the town council" / "Repay ..." w menu wlasnego miasta i zamku
            try { DistrictMenu.AddMenus(starter); } catch (Exception e) { Log.Error("DistrictMenu.AddMenus", e); }   // E7/W3 (234): "Look over the district" - okno wiosek okregu z herbami (za "People of this land")
            try { DistrictWorks.AddMenus(starter); } catch (Exception e) { Log.Error("DistrictWorks.AddMenus", e); }
            try { SideVillageVisit.AddMenus(starter); } catch (Exception e) { Log.Error("SideVillageVisit.AddMenus", e); }   // m97-wioski: menu kazdej wioski pobocznej (nocleg, oboz, handel, prace, wrogie akcje)
            try { SideVillageFief.AddMenus(starter); } catch (Exception e) { Log.Error("SideVillageFief.AddMenus", e); }   // n20-wioska: menu wioski gracza (nocleg, prace, przeglad) i "Visit X (your village)" w menu wsi gry   // inwest: "Invest in the district's works" za "Look over the district" (village, village_looted)
            try { DailyLife.AddMenus(starter); } catch (Exception e) { Log.Error("DailyLife.AddMenus", e); }   // ZC2 (16.10): "Realm ledger" w menu miasta i zamku - stany i Dostatek krolestwa, regionow i swiata (ShowInquiry)
            try { QohorMasters.AddMenus(starter); } catch (Exception e) { Log.Error("QohorMasters.AddMenus", e); }   // 177-3: mistrzowie Qohoru - przekuwanie stali valyrianskiej
            try { SmithMenu.Add(starter); Log.Info("Menu kowala dodane."); }
            catch (Exception e) { Log.Error("OnSessionLaunched", e); }
            try { CleanseAmmo(); } catch (Exception e) { Log.Error("CleanseAmmo", e); }
            try { NightRest.AddMenus(starter); } catch (Exception e) { Log.Error("NightRest.AddMenus", e); }
            try { TownInns.AddMenus(starter); } catch (Exception e) { Log.Error("TownInns.AddMenus", e); }   // KA (16.10): "Take lodgings at an inn" w menu miasta (sen - arm_sleep_wait z NightRest)
            try { RoadsideInns.AddMenus(starter); } catch (Exception e) { Log.Error("RoadsideInns.AddMenus", e); }   // sw-zajazdy: menu zajazdu przy szlaku (nocleg, plotki, dalej w droge)
            try { if (!LoreSeedFix.On) RangedLore.ReportLedger(false); } catch (Exception e) { Log.Error("RangedLore.ReportLedger", e); }   // m96-wzory: przy kluczu - po zmianach statystyk (LoreSeedFix)
            try { ArmyClothing.StartLine(); } catch (Exception e) { Log.Error("ArmyClothing.StartLine", e); }   // 150: stawki odziezy wojska i zaleznosci
            try { HorseState.OnSessionLaunched(); } catch (Exception e) { Log.Error("HorseState.OnSessionLaunched", e); }   // E6/K0 (209): licznik koni gry wedlug klucza (pelne przeliczenie przy zmianie), stan koni partii od nowa - PO porzadkach taborow wyzej
        }

        private static float Today { get { return (float)CampaignTime.Now.ToDays; } }
        private static float _lastIdleLogDay = -10f;

        // ---------------------------------------------------------- robota przy kowadle
        internal void StartProject(ItemObject item, int tempo, float days, Settlement where)
        { StartProject(item, tempo, days, where, "", ""); }

        internal void StartProject(ItemObject item, int tempo, float days, Settlement where, string kind, string modifierId)
        { StartProject(item, tempo, days, where, kind, modifierId, 1); }

        internal void StartProject(ItemObject item, int tempo, float days, Settlement where, string kind, string modifierId, int count)
        {
            try
            {
                var p = new Project
                {
                    Item = item, DaysLeft = days, Tempo = tempo,
                    SettlementId = where != null ? where.StringId : "",
                    Kind = kind ?? "", ModifierId = modifierId ?? "", Count = Math.Max(1, count)
                };
                _projects.Add(p.Serialize());
                Log.Info("Projekt dodany: " + p.Serialize());
            }
            catch (Exception e) { Log.Error("StartProject", e); }
        }

        internal string ProjectSummary()
        {
            try
            {
                if (_projects.Count == 0) return "";
                var sb = new System.Text.StringBuilder();
                foreach (var line in _projects)
                {
                    var p = Project.Parse(line);
                    if (!p.Valid) continue;   // m96-czaskuzni: praca bez sztuki (IsPrep) tez w spisie
                    if (sb.Length > 0) sb.Append("\n");
                    if (p.DaysLeft <= 0f)
                        sb.Append(p.Name + " - READY, waiting for collection at the forge");
                    else
                        sb.Append(p.Name + " - " + Project.TimeLabel(p.DaysLeft) + " of work left"
                                  + (ForgeJob.ClockNote(p) ?? (Settings.Current.ForgeWorksWithoutYou ? "" : " (clock runs only while you stay there)"))   // m96-czaskuzni: robota gracza tylko na pasku
                                  + (p.IsPrep ? "" : ", worked " + p.TempoName));
                }
                return sb.ToString();
            }
            catch (Exception e) { Log.Error("ProjectSummary", e); return ""; }
        }

        internal bool HasProjects { get { return _projects.Count > 0; } }

        /// <summary>m96-czaskuzni: kolejka robot gracza dla ForgeJob (wstawienie pracy bez sztuki przed sztuki z ekranu, spis do logu).</summary>
        internal List<string> ProjectLines { get { return _projects; } }

        private static void HandOver(Project p)
        {
            if (p.IsPrep) return;   // m96-czaskuzni: praca bez sztuki - nic do wydania (material dal ekran kuzni od razu)
            if (p.Kind == "van" || p.Kind == "bk") Forge.Deliver(p.Item, p.ModifierId, Math.Max(1, p.Count));   // sukces zapadl przy kowadle
            else Forge.Finish(p.Item, p.Tempo);
        }

        private void AdvanceProjects()
        {
            try
            {
                if (_projects.Count == 0) return;
                // SUFIT Z SUWAKA (Jeff 16.09: "czekam na cztery miecze ponad 169 godzin"):
                // zlecenie broni zlozone przy starym przeliczniku ma DaysLeft policzone
                // raz, przy starcie - obnizka WeaponDaysPerTier sama go nie skroci.
                // Kazdy tick przycina wiec DaysLeft broni do nowego wzoru (ten sam co
                // przy starcie: max(0.1, tier * WeaponDaysPerTier)). Pancerze bez zmian.
                int capped = 0;
                for (int i = 0; i < _projects.Count; i++)
                {
                    var q = Project.Parse(_projects[i]);
                    if (q.Item == null || q.Kind != "van" || ForgeClock.On || ForgeClock.LikeTown) continue;   // wpis 83: godziny z ekranu BK, bez przycinania; C5: dni kowala, bez przycinania
                    float cap = MathF.Max(0.1f, Recipes.Grade(q.Item) * Settings.Current.WeaponDaysPerTier) * ForgeClock.TimeMult;   // m109-czas (przeglad n34 Z1): sufit jak start (WorkHours x TimeMult)
                    if (q.DaysLeft > cap) { q.DaysLeft = cap; _projects[i] = q.Serialize(); capped++; }
                }
                if (capped > 0) Log.Info("Kuznia: " + capped + " zlecen broni w kolejce skrocono do nowego przelicznika (WeaponDaysPerTier " + Settings.Current.WeaponDaysPerTier + ").");
                var here = Settlement.CurrentSettlement;
                // KOWAL PRACUJE, KIEDY TY JEDZIESZ (Jeff 27.08: "wykulem miecz,
                // odczekalem dlugo i przepadl" - stary zegar tykal TYLKO, gdy
                // gracz stal w osadzie projektu, a on czekal na mapie obok).
                // Teraz robota idzie zawsze; XP za prace w trakcie dostajesz
                // tylko na miejscu (to twoje rece), a GOTOWY wyrob lezy
                // w warsztacie i czeka na odbior przy wejsciu do osady.
                // wpis 83 (Jeff 05.10: "nie kuje sie, jak mnie nie ma w miescie") - to Twoje rece przy kowadle
                bool remote = Settings.Current.ForgeWorksWithoutYou && !Settings.Current.ForgeOnlyWhileThere;
                var copy = new List<string>(_projects);
                bool idleWarned = false;
                // JEDNA LINIA CZASU U KOWALA (Jeff 28.08: "wytapiam miecze,
                // a moge dolozyc nastepne i ida rownolegle"). W kazdej osadzie
                // tyka TYLKO najstarszy niegotowy projekt - reszta czeka na
                // swoja kolej. Rozne miasta = rozni kowale, pracuja niezaleznie.
                var busyForge = new HashSet<string>();
                var spare = new Dictionary<string, float>();   // m96-czaskuzni: reszta godziny taktu po skonczonym wpisie - na nastepny wpis tej kuzni
                foreach (var line in copy)
                {
                    var p = Project.Parse(line);
                    if (!p.Valid) { _projects.Remove(line); continue; }   // m96-czaskuzni: praca bez sztuki (IsPrep) zostaje

                    bool atForge = here != null && here.StringId == p.SettlementId;

                    if (p.DaysLeft <= 0f)
                    {
                        // gotowy wyrob lezy w warsztacie - wydanie na miejscu
                        if (atForge) { _projects.Remove(line); HandOver(p); }
                        continue;
                    }

                    // m96-czaskuzni: praca bez sztuki czeka na rece gracza i nie blokuje kowala, ktory kuje bez ciebie (remote)
                    if (p.IsPrep && remote && !ForgeJob.HandsAt(p.SettlementId)) continue;

                    // kowal tej osady juz kuje wczesniejszy projekt - ten czeka
                    if (!busyForge.Add(p.SettlementId)) continue;

                    if (!atForge && (!remote || p.IsPrep))   // m96-czaskuzni: wytop i rafinacja to twoje rece - tylko na miejscu
                    {
                        if (!idleWarned && Today - _lastIdleLogDay > 0.99f)
                        {
                            idleWarned = true;
                            _lastIdleLogDay = Today;
                            Log.Info("Projekt stoi - gracz poza osada.");
                        }
                        continue;
                    }
                    // m96-czaskuzni: przy Forge One Job robota gracza idzie tylko na pasku przy kowadle - po "Step away" czeka, takze w miescie
                    if (atForge && !(remote && !p.IsPrep) && !ForgeJob.HandsAt(p.SettlementId)) continue;

                    // CZELADZ TEZ SPI (Jeff 31.08): warsztat stoi noca 23-5 -
                    // zadnego postepu przez szesc ciemnych godzin. "Dni pracy"
                    // projektu to dni PRZY KOWADLE, wiec robota trwa realnie
                    // ~1/3 dluzej kalendarza - jak w prawdziwej kuzni.
                    if (Settings.Current.WorkshopNightRest)
                    {
                        int hh = CampaignTime.Now.GetHourOfDay;
                        if (hh >= 23 || hh < 5) continue;
                    }

                    // kuznia za kazdy dzien roboty (Jeff 07.10): KAZDA robota, ktora idzie naprzod - wlasny projekt, sztuka z ekranu BK,
                    // bron kuta po vanillowemu; przy kowadle i pod nieobecnosc gracza (kowal pracuje bez ciebie) - placi dobe kuzni tej
                    // osady (jeden rejestr z karnetem BK); bez pieniedzy robota czeka. Przeglad 07.10: dotad tylko wlasny projekt, tylko
                    // przy kowadle i tylko przy jednym zegarze kuzni - reszta szla za darmo.
                    if (Settings.Current.ForgeHireHistorical)
                    {
                        bool rentOk = true;
                        try { rentOk = Forge.PayDayRent(atForge ? Settlement.CurrentSettlement : Settlement.Find(p.SettlementId)); } catch (Exception er) { Log.Error("PayDayRent", er); }
                        if (!rentOk) continue;
                    }

                    // KROK GODZINOWY: zegar konczy sie DOKLADNIE z robota, bez
                    // doczekiwania do polnocy (blad, ktory wkurzyl Jeffa przy mieczu)
                    float stepH = 1f, sp;   // m96-czaskuzni: reszta godziny po wczesniejszym wpisie tej kuzni (bez klucza zawsze pelna godzina)
                    if (spare.TryGetValue(p.SettlementId, out sp)) { stepH = sp; spare.Remove(p.SettlementId); }
                    p.DaysLeft -= stepH / 24f;
                    if (ForgeJob.On && p.DaysLeft < -0.0001f) { busyForge.Remove(p.SettlementId); spare[p.SettlementId] = -p.DaysLeft * 24f; }   // m96-czaskuzni: wpis skonczony w trakcie godziny - reszta dla nastepnego
                    if (!Settings.Current.ForgeHireHistorical && atForge && ForgeClock.On && (p.Kind == "bk" || p.Kind == "van" || p.IsPrep)) { try { DayPass.EnsureBought(); } catch { } }   // kuznia wynajeta na dobe
                    if (p.IsPrep) { ForgeJob.TickedPrep(_projects, line, p); continue; }   // m96-czaskuzni: praca bez sztuki - same godziny, bez XP i bez wydania
                    var rr = Recipes.For(p.Item);
                    // XP liczy sie od WLASCIWEGO czasu projektu: bron "van" ma swoj
                    // przelicznik (WeaponDaysPerTier), pancerze swoj (Jeff 29.08:
                    // "balagan z godzinami" - to byl jeden z rozjazdow)
                    // minimum 0.1 dnia (bylo 0.5 - Jeff 16.09: "kucie miecza za dlugie, -80%";
                    // przy 0.1 dnia/tier pol dnia podlogi zjadloby cala obnizke)
                    float totalDays = p.Kind == "van"
                        ? MathF.Max(0.1f, Recipes.Grade(p.Item) * Settings.Current.WeaponDaysPerTier)
                        : MathF.Max(1f, rr.Tier * Settings.Current.DaysPerTier * Project.TimeFactor(p.Tempo));
                    // C5: XP rozlozone na prawdziwa dlugosc roboty (dni kowala) - suma XP projektu bez zmian (inaczej 45 dob kolczugi dawaloby XP x 15)
                    if (ForgeClock.LikeTown && p.Kind != "bk")
                        totalDays = MathF.Max(1f / 24f, ForgeClock.WorkHours(p.Item, 1, totalDays * 24f) / 24f * (p.Kind == "van" ? 1f : Project.TimeFactor(p.Tempo)));
                    else
                        totalDays *= ForgeClock.TimeMult;   // m109-czas: robota trwa ForgeTimeMult razy dluzej - XP sztuki rozlozone na dluzsza robote, suma bez zmian
                    if (atForge && p.Kind != "bk")   // XP tylko za wlasna prace przy kowadle (bk: XP dal BK przy kliknieciu)
                        Hero.MainHero.HeroDeveloper.AddSkillXp(DefaultSkills.Crafting,
                            Forge.ProjectXp(rr) * Settings.Current.XpShareWhileWorking / totalDays / 24f * stepH * Forge.AmmoXpMul(p.Item));   // sklad8-s (przeglad, uwaga 2): amunicja x0.05 takze w trakcie pracy; m96: XP za przepracowana czesc godziny

                    // wpis 89 (audyt): zapis W MIEJSCU - dotad Remove + Add przesuwal tykajaca sztuke na koniec listy,
                    // wiec "najstarsza" zmieniala sie co godzine i cala kolejka szla na zmiane, konczac sie razem
                    int at = _projects.IndexOf(line);
                    if (p.DaysLeft > 0f) { if (at >= 0) _projects[at] = p.Serialize(); else _projects.Add(p.Serialize()); }
                    else if (atForge) { _projects.Remove(line); HandOver(p); }
                    else
                    {
                        // skonczone pod twoja nieobecnosc: wyrob czeka na polce
                        p.DaysLeft = 0f;
                        if (at >= 0) _projects[at] = p.Serialize(); else _projects.Add(p.Serialize());
                        var s = Settlement.Find(p.SettlementId);
                        Log.Player("The smith has finished your " + p.Item.Name + " - collect it at "
                                   + (s != null ? s.Name.ToString() : p.SettlementId) + ".");
                    }
                }
            }
            catch (Exception e) { Log.Error("AdvanceProjects", e); }
        }

        /// <summary>Odbior gotowych wyrobow zaraz przy wejsciu do osady - bez czekania na tick godzinowy.</summary>
        internal void CollectReadyProjects()
        {
            try
            {
                if (_projects.Count == 0) return;
                var here = Settlement.CurrentSettlement;
                if (here == null) return;
                var copy = new List<string>(_projects);
                foreach (var line in copy)
                {
                    var p = Project.Parse(line);
                    if (!p.Valid) { _projects.Remove(line); continue; }   // m96-czaskuzni: praca bez sztuki (IsPrep) zostaje
                    if (p.DaysLeft <= 0f && p.SettlementId == here.StringId)
                    { _projects.Remove(line); HandOver(p); }
                }
            }
            catch (Exception e) { Log.Error("CollectReadyProjects", e); }
        }

        // E8/0 (242): profil doby - linia za poprzedni okres i stoper calego taktu (jedna klatka); odcinki DayProfile.Lap po kazdym wywolaniu (tylko pomiar)
        private readonly long[] _qtSplit = new long[MapSpeedProfile.QParts + 1];   // f9-predkosc: znaczniki czasu czesci sluchacza godziny (tylko log)

        private void OnDailyTick()
        {
            MapSpeedProfile.Daily();   // f9-predkosc: linia "Predkosc mapy (f9)" za okres od poprzedniego taktu (tylko log) - przed profilem doby, poza jego stoperem
            DayProfile.TickBegin();
            try { OnDailyTickBody(); }
            finally { DayProfile.TickEnd(); }
        }

        private void OnDailyTickBody()
        {
            AlleyProbe.TickOpen();   // m6b (6): migawka kas miast przed naszym rozliczeniem doby (tylko log; TickClose na koncu)
            try { OnDailyTickBodyInner(); }
            finally { try { AlleyProbe.TickClose(); } catch (Exception e) { Log.Error("AlleyProbe.TickClose", e); } }
        }

        private void OnDailyTickBodyInner()
        {
            try { Cost174.Daily(); } catch (Exception e) { Log.Error("Cost174.Daily", e); } DayProfile.Lap("Cost174.Daily");   // 174b.5 F6: linia "Koszt 171-174 (doba)" za dobe od poprzedniego taktu (tylko log)
            try { PerfD2.Daily(); } catch (Exception e) { Log.Error("PerfD2.Daily", e); } DayProfile.Lap("PerfD2.Daily");   // perf-d2d3: linia "Szybkosc doby (perf-d2d3)" za okres od poprzedniego taktu (tylko log) i straznik podatku miast raz na dobe   // 174b.5 F6: linia "Koszt 171-174 (doba)" za dobe od poprzedniego taktu (tylko log)
            try { CampFever.OnDaily(); } catch (Exception e) { Log.Error("CampFever", e); } DayProfile.Lap("CampFever.OnDaily");
            try { RecruitKit.Reconcile("doba"); } catch (Exception e) { Log.Error("RecruitKit.Reconcile", e); } DayProfile.Lap("RecruitKit.Reconcile");   // 161: komplety bez ochotnika w puli (sieroty)
            // audyt predkosci STAD, nie z postfixa - w postfixie SpeedExplained
            // wchodzilo o poziom glebiej i rozpiska pokazywala gole liczby vanilli
            try { TerrainEase.DailyAudit(); } catch (Exception e) { Log.Error("SpeedAudit", e); } DayProfile.Lap("TerrainEase.DailyAudit");
            try { WorldMeasure.Daily(); } catch (Exception e) { Log.Error("WorldMeasure.Daily", e); } DayProfile.Lap("WorldMeasure.Daily");   // T6: linia "Miara: marsz" (tylko log)
            try { Stage8World.Daily(); } catch (Exception e) { Log.Error("Stage8World.Daily", e); } DayProfile.Lap("Stage8World.Daily");   // E8/P3 (245): linie pomiarow swiata (owce, postoj karawan, najemnicy BK, rozkazy morskie; raz na sesje model produkcji wsi) - tylko log
            try { CaravanFlow.Daily(); } catch (Exception e) { Log.Error("CaravanFlow.Daily", e); } DayProfile.Lap("CaravanFlow.Daily");   // E8/G1 (260): karawany bez drogi (wyspa z jednym miastem) - towar miastu z nadwyzki kasy i rozwiazanie; linia "Karawany (E8/G1)"; PO pomiarze postoju E8/P3, PRZED MoneyLedger.BlockOpen
            try { MercFeast.Daily(); } catch (Exception e) { Log.Error("MercFeast.Daily", e); } DayProfile.Lap("MercFeast.Daily");   // E8/G2 (261): linia "Najemnicy i uczta BK (E8/G2)" - zamiana BK u klanow najemnikow bez bohaterow, zakupy uczt BK zdjete z rynku; tylko log
            try { CraftOrders8.Daily(); } catch (Exception e) { Log.Error("CraftOrders8.Daily", e); } DayProfile.Lap("CraftOrders8.Daily");   // E8/K (255): linia "Kuznia (E8/K)" - zlecenia kowalskie i ksiegarz BK; w nowej kampanii raz wymiana zlecen startowych
            try { CraftOrdersPurse.Daily(); } catch (Exception e) { Log.Error("CraftOrdersPurse.Daily", e); } DayProfile.Lap("CraftOrdersPurse.Daily");   // NZ (16.10): przeglad zlecen kowalskich (kiesa zleceniodawcy nie pokrywa sumy jego zlecen albo zmarly - zdjete) i linia "Zlecenia kowalskie (NZ)"; PO CraftOrders8.Daily (wymiana startowa)
            try { QuestPay8.Daily(); } catch (Exception e) { Log.Error("QuestPay8.Daily", e); } DayProfile.Lap("QuestPay8.Daily");   // E8/D1 (256): linia "Nagrody zadan (E8/D1)" i porzadek w slowniku kwot zamrozonych
            try { RareMaterials.Daily(); } catch (Exception e) { Log.Error("RareMaterials.Daily", e); } DayProfile.Lap("RareMaterials.Daily");   // m97-surowce: doplyw czardrzewa i smoczej kosci, linia "Surowce rzadkie (m97-surowce): dzien N"
            try { RogueryXp28.Daily(); } catch (Exception e) { Log.Error("RogueryXp28.Daily", e); }   // f28-wyceny (B): linia "Lotrostwo (f28): dzien N" - tylko log
            try { Wyceny20.Daily(); } catch (Exception e) { Log.Error("Wyceny20.Daily", e); }   // n20-wyceny: linie "Handel z warsztatu (n20)", "Lup AI (n20)", "Kryjowki band (n20)" - tylko log
            try { PlayerFees8.Daily(); } catch (Exception e) { Log.Error("PlayerFees8.Daily", e); } DayProfile.Lap("PlayerFees8.Daily");   // E8/D2 (257): linia "Wydatki gracza (E8/D2)" - tylko log
            try { MenUpgrade.Touch(); } catch (Exception e) { Log.Error("MenUpgrade.Touch", e); } DayProfile.Lap("MenUpgrade.Touch");   // K1: linia "Dozbrajanie: dzien" (poprzednia doba, codziennie)
            try { ArmouryZb.Daily(); } catch (Exception e) { Log.Error("ArmouryZb.Daily", e); } DayProfile.Lap("ArmouryZb.Daily");   // ZB (noc 17/18.10): linia "Zbrojownia gracza (ZB): dzien N" - ksiega per modyfikator, konie zapasu, nadwyzki kazdej grupy, pytanie o nocleg (tylko log)
            try { PlagueWatch.DailyReport(); } catch (Exception e) { Log.Error("PlagueWatch", e); } DayProfile.Lap("PlagueWatch.DailyReport");
            try { InfluenceWatch.DailyReport(); } catch (Exception e) { Log.Error("InfluenceWatch", e); } DayProfile.Lap("InfluenceWatch.DailyReport");
            try { WesterosClimate.Daily(); } catch (Exception e) { Log.Error("WesterosClimate.Daily", e); } DayProfile.Lap("WesterosClimate.Daily");   // biale kruki: koniec pory roku
            try { StateHygiene.Daily(); } catch (Exception e) { Log.Error("StateHygiene.Daily", e); } DayProfile.Lap("StateHygiene.Daily");   // E8/S (246): linia "Porzadek stanu (E8/S)" - PO WesterosClimate.Daily (stan klimatu po ponowieniu tej doby); tylko log
            try { FoodChain.Daily(); } catch (Exception e) { Log.Error("FoodChain.Daily", e); } DayProfile.Lap("FoodChain.Daily");   // E4/6a: raz na kampanie linia "Lancuch modelu jedzenia (E4/6a)"
            try { BkrotChains.Daily(); } catch (Exception e) { Log.Error("BkrotChains.Daily", e); } DayProfile.Lap("BkrotChains.Daily");   // P4 audytu: linia "Lancuchy modeli BKROT (P4): dzien N" (predkosc na morzu, zold Innych / Nieskalanych / na morzu, wzrok)
            try { WinterBite.OnDaily(); } catch (Exception e) { Log.Error("WinterBite", e); } DayProfile.Lap("WinterBite.OnDaily");
            try { HarvestYear.Daily(); } catch (Exception e) { Log.Error("HarvestYear.Daily", e); } try { HerdCull.Daily(); } catch (Exception e) { Log.Error("HerdCull.Daily", e); } DayProfile.Lap("HarvestYear/HerdCull");   // R3-05e/f: rok urodzaju (losowanie w oknie zniw), okno uboju jesiennego i splata stad
            var gfForage = GoodsLedger.Begin(GoodsLedger.FForage);   // paczka 146: ksiega towarow - zboze z furazu armii (tylko licznik)
            try { ScorchedEarth.OnDaily(); } catch (Exception e) { Log.Error("ScorchedEarth", e); }
            finally { GoodsLedger.End(gfForage); } DayProfile.Lap("ScorchedEarth.OnDaily");
            try { Devastation.Daily(); } catch (Exception e) { Log.Error("Devastation.Daily", e); } DayProfile.Lap("Devastation.Daily");   // E4/5 (113n): smierc i powrot uchodzcow - PO zerowaniu armii (zegar regionu widzi dzisiejsze zerowanie)
            try { Famine.Daily(); } catch (Exception e) { Log.Error("Famine.Daily", e); } DayProfile.Lap("Famine.Daily");   // E4/6 (191): spichlerze okregow, glod, uchodzcy z glodu - PO spustoszeniu i powrocie uchodzcow, PRZED ksiega ludzi (PeopleBook.Daily zamyka dobe konta UCHODZCY)
            // r4-mur: danina zbozowa Muru (spichlerze Polnocy -> wozy -> spichlerze Strazy) - PO Famine.Daily (migawka spichlerzy tej doby)
            try { WallTribute.Daily(); } catch (Exception e) { Log.Error("WallTribute.Daily", e); } DayProfile.Lap("WallTribute.Daily");
            try { VillageBook.Daily(); } catch (Exception e) { Log.Error("VillageBook.Daily", e); } DayProfile.Lap("VillageBook.Daily");   // E4/11 (193, Z6): puste osady wiosek uzgodnione z kontem PO powrotach i ucieczkach z glodu tej doby; czyta licznik trafionych Devastation PRZED DayLine
            try { Leaks310.Daily(); } catch (Exception e) { Log.Error("Leaks310.Daily", e); } DayProfile.Lap("Leaks310.Daily");   // 3.10: zold zaciagu ROT od pana i linia "Drobne z niczego (3.10)" (furaz tej doby)
            try { WarLedger.OnDaily(); } catch (Exception e) { Log.Error("WarLedger", e); } DayProfile.Lap("WarLedger.OnDaily");
            try { DefeatMorale.Daily(); } catch (Exception e) { Log.Error("DefeatMorale.Daily", e); } DayProfile.Lap("DefeatMorale.Daily");   // D11-1: linia "Kara za przegrana (D11-1)" zaraz po liniach wojny (dezercja AI 183) - tylko log
            try { RaceLaw.Daily(); } catch (Exception e) { Log.Error("RaceLaw.Daily", e); } DayProfile.Lap("RaceLaw.Daily");   // Jeff 10.10: linia "Rasy: dzien N" - tylko gdy cos zablokowano
            try { PregnancyYear.Daily(); } catch (Exception e) { Log.Error("PregnancyYear.Daily", e); } DayProfile.Lap("PregnancyYear.Daily");   // CR (zasada Jeffa 14.10): linia "Ciaza i plodnosc (CR): dzien N" - ciaze w toku, poczecia, porody, rzuty szansy (tylko log)
            try { ArmyCohesionYear.Daily(); } catch (Exception e) { Log.Error("ArmyCohesionYear.Daily", e); } DayProfile.Lap("ArmyCohesionYear.Daily");   // AC: linia "Spojnosc armii (AC): dzien N"
            try { FiefGrants.Daily(); } catch (Exception e) { Log.Error("FiefGrants.Daily", e); } DayProfile.Lap("FiefGrants.Daily");   // PROJEKT-LENNA (14.10): linia "Nadania lenn (N) doba N" (gdy cos sie dzialo albo co 28 dob), pamiec szturmow i nadan 60 dob
            try { Orders.DailyTick(); }
            catch (Exception e) { Log.Error("OnDailyTick", e); } DayProfile.Lap("Orders.DailyTick");
            // wpis 86 (audyt pkt 18): kazdy system we wlasnym try - wyjatek jednego nie zatrzymuje reszty dnia
            try { HistoricalPrices.Recheck(); } catch (Exception e) { Log.Error("HistoricalPrices.Recheck", e); } DayProfile.Lap("HistoricalPrices.Recheck");
            try { UniqueSpoils.Daily(); } catch (Exception e) { Log.Error("UniqueSpoils.Daily", e); } DayProfile.Lap("UniqueSpoils.Daily");
            try { UniqueShelf.Daily(); } catch (Exception e) { Log.Error("UniqueShelf.Daily", e); }   // A17 (poz. 4): linia "Unikaty na polkach (A17)"
            try { BattleChronicle.Daily(); } catch (Exception e) { Log.Error("BattleChronicle.Daily", e); } DayProfile.Lap("BattleChronicle.Daily");
            try { CitySack.Daily(); } catch (Exception e) { Log.Error("CitySack.Daily", e); } DayProfile.Lap("CitySack.Daily");   // lup miast (L0-L4): linia doby i osady 30 dob po nastepstwach oblezen
            try { SiegeRansom.Daily(); } catch (Exception e) { Log.Error("SiegeRansom.Daily", e); } DayProfile.Lap("SiegeRansom.Daily");   // okup zamiast szturmu: linia doby, porzadek pamieci, rozejmy
            try { SiegeWalls.Daily(); } catch (Exception e) { Log.Error("SiegeWalls.Daily", e); } DayProfile.Lap("SiegeWalls.Daily");   // mury oblezen OB0: linia doby, porzadek pamieci
            try { SiegeWalls2.Daily(); } catch (Exception e) { Log.Error("SiegeWalls2.Daily", e); } DayProfile.Lap("SiegeWalls2.Daily");   // oblezenia 2: glod oblezenia (smierc, kapitulacja), linia "Oblezenia 2 (doba)"
            try { SiegePhantomLift.Daily(); } catch (Exception e) { Log.Error("SiegePhantomLift.Daily", e); } DayProfile.Lap("SiegePhantomLift.Daily");   // OB6 (noc-drobne): oboz bez sily zwija oblezenie, linia "Oboz bez sily (OB6): dzien"
            // N7 (decyzja Jeffa 13.10): rozbici pod choragiew - gotowosc, odbior przez druzyny rodu (limit i zold z budzetu wczoraj, jak W5), rozejscie po czasie czekania;
            // PRZED H3 i wyrzutkami: ci, ktorzy sie rozeszli, wchodza do linii "Przegrani (H3)" (domy) i "Pula wyrzutkow (E4/3)" (naplyw) tej samej doby
            try { StragglersRally.Daily(); } catch (Exception e) { Log.Error("StragglersRally.Daily", e); } DayProfile.Lap("StragglersRally.Daily");
            try { LosersFlee.Daily(); } catch (Exception e) { Log.Error("LosersFlee.Daily", e); } DayProfile.Lap("LosersFlee.Daily");   // H3: linia "Przegrani (H3):" zaraz po "Bitwy:" i plik h3-domy.csv (PRZED OutlawLaw.Daily)
            try { DoomedPartyMen.Daily(); } catch (Exception e) { Log.Error("DoomedPartyMen.Daily", e); }   // A17 (poz. 11): linia "Ludzie kasowanych partii (A17)" po linii H3
            try { PrisonerLaw.Daily(); } catch (Exception e) { Log.Error("PrisonerLaw.Daily", e); } DayProfile.Lap("PrisonerLaw.Daily");   // I1: linia "Prawo jenca (I1):" - do domu, na Mur, niewolnicy (ta sama doba co H3 "prawo jenca I1")
            try { SupplyDemand.DecayOrders(); } catch (Exception e) { Log.Error("SupplyDemand.DecayOrders", e); } DayProfile.Lap("SupplyDemand.DecayOrders");
            try { ArmsPricing.Daily(); } catch (Exception e) { Log.Error("ArmsPricing.Daily", e); } DayProfile.Lap("ArmsPricing.Daily");   // indeksy surowcow i premie wojenne PRZED handlem
            try { Seal31.BlockOpen(); } catch { } DayProfile.Lap("Seal31.BlockOpen");   // 3.1 (31, E3/0): kiesy Innych przed naszym tickiem (pomiar)
            try { MoneyLedger.BlockOpen(); } catch { } DayProfile.Lap("MoneyLedger.BlockOpen");   // ksiega przeplywow osad (tylko log): stan kas przed naszym rozliczeniem doby
            try { TownPurse.BeforeRents(); } catch (Exception e) { Log.Error("TownPurse.BeforeRents", e); } DayProfile.Lap("TownPurse.BeforeRents");
            try { DsTowns.DayStart(); } catch (Exception e) { Log.Error("DsTowns.DayStart", e); } DayProfile.Lap("DsTowns.DayStart");   // DS (F3): stan doby - okno pomiaru firm, ustalenie firm, dni kwartalu, odnowy (PO BeforeRents - doplyw netto kas, PRZED zaworem/firmami w PopulationLaw.Daily)   // E5/A1: liczniki doby kas miast, dar startowy (raz, migawka M12), zaulki - PRZED rentami i zaworem miast
            try { PeopleMarket.StartGold(); } catch (Exception e) { Log.Error("PeopleMarket.StartGold", e); } DayProfile.Lap("PeopleMarket.StartGold");   // GL-2: raz na nowa kampanie - zloto kas miast, kies ludu i wsi oraz skarbcow wedlug ludzi PO darach startowych (TrimStartGift, kiesy R5), PRZED zaworem miast (PopulationLaw.Daily)
            try { PopulationLaw.Daily(); } catch (Exception e) { Log.Error("PopulationLaw.Daily", e); } DayProfile.Lap("PopulationLaw.Daily");   // ludnosc i renty krain
            try { Seal31B.OwnerTaxDaily(); } catch (Exception e) { Log.Error("Seal31B.OwnerTaxDaily", e); } DayProfile.Lap("Seal31B.OwnerTaxDaily");   // D11-3: podatek BK warsztatow lordow i gracza w cudzych miastach - wlasciciel -> pan miasta (renta, RentMerge nizej) i kasa miasta (autonomia)
            try { Seal31B.RentMerge(); } catch (Exception e) { Log.Error("Seal31B.RentMerge", e); } DayProfile.Lap("Seal31B.RentMerge");   // 3.1/3.3 (31b) E3/8a: podatek warsztatow notabli przelany panom od wczoraj -> RentToday (po czyszczeniu w PopulationLaw.Daily)
            try { TownToll.RentMerge(); } catch (Exception e) { Log.Error("TownToll.RentMerge", e); } DayProfile.Lap("TownToll.RentMerge");   // E3-G3: myto panow od wczoraj -> RentToday (po czyszczeniu w PopulationLaw.Daily)
            try { MoneyLedger.Mark(MoneyLedger.MRent); } catch { }
            // 110 (projekt etapu 2 krok B): zawor kasy zamku - 7% nadwyzki ponad zapas kupcow do pana zamku, linia "Zawor zamkow (110)";
            // zaraz PO rentach (dopisuje sie do dziennych rent rodu), PRZED budowami i powinnosciami, ktore licza od dzisiejszego dochodu
            try { CastlePurse.Daily(); } catch (Exception e) { Log.Error("CastlePurse.Daily", e); } DayProfile.Lap("CastlePurse.Daily");
            try { MoneyLedger.Probe169("CastlePurse.Daily"); } catch { }   // f19-e1: bisekcja RB (tylko log)
            try { PeopleMarket.StartGoldCastles(); } catch (Exception e) { Log.Error("PeopleMarket.StartGoldCastles", e); } DayProfile.Lap("PeopleMarket.StartGoldCastles");   // GL-2 (recenzja obiegu D4): pula podzamczy zlota startowego - w dobie StartGold, PO darze zamkow (CastlePurse.TrimStartGift / SeedCastle)
            try { MoneyLedger.Mark(MoneyLedger.MCastle); } catch { }
            // E5/0 (195): kiesa ludu na sucho (tylko log) - po rentach i zaworze zamkow (RentOf, CastlePurse.DuesOf czytelne), przed budowami; E5/B1 (173): przy kiesie
            // czynnej prawdziwe przelewy (podatki panu -> RentToday przed budowami i powinnosciami, place z kas, zakupy wsi z polki - ramka ksiegi towarow F26) i migawka M18
            // sw-uchodzcy (RaidRefugeesToTowns): przydzial miast uchodzcom z najazdu, kiesa przybylych (kiesy wsi -> kiesy W miast) i powracajacych (z powrotem), udzial
            // salda w kiesie W, linia "Uchodzcy do miast (RT)" - PO powrocie i glodzie tej doby (Devastation, Famine), PRZED kiesa ludu (kotwica jutra liczy ich glowy),
            // w pozycji MFolk ksiegi pieniadza (Mark ponizej)
            try { RaidRefugees.Daily(); } catch (Exception e) { Log.Error("RaidRefugees.Daily", e); } DayProfile.Lap("RaidRefugees.Daily");
            try { MoneyLedger.Probe169("RaidRefugees.Daily"); } catch { }   // f19-e1: bisekcja RB (tylko log)
            var gfFolk = GoodsLedger.Begin(GoodsLedger.FFolkVill);
            try { TownFolk.Daily(); } catch (Exception e) { Log.Error("TownFolk.Daily", e); }
            finally { GoodsLedger.End(gfFolk); } DayProfile.Lap("TownFolk.Daily");
            try { MoneyLedger.Probe169("TownFolk.Daily"); } catch { }   // f19-e1: bisekcja RB (tylko log)
            // K1-3a krok 2: wymiana padlych koni roboczych miast i zamkow z polki osady za zloto kiesy ludu - PO kotwicy doby (pieniadze ponad jutrzejszy koszyk),
            // w tej samej pozycji MFolk ksiegi pieniadza (kiesa -> kasa osady); poza ramka zakupow wsi ksiegi towarow (kon nie jest towarem tej ksiegi)
            try { TownFolk.WorkHorsesDaily(); } catch (Exception e) { Log.Error("TownFolk.WorkHorsesDaily", e); } DayProfile.Lap("TownFolk.WorkHorsesDaily");
            try { MoneyLedger.Probe169("TownFolk.WorkHorsesDaily"); } catch { }   // f19-e1: bisekcja RB (tylko log)
            // E6/R1 (214): woz kopalni kupuje drewno dymarki z polki miasta handlowego za zloto wsi (na nastepna dobe) - zakup wsi w miescie jak E5/B1
            // (pozycja MFolk ksiegi pieniadza, wlasna ramka ksiegi towarow FBloom), PRZED ksiega zelaza i drewna (OreLedger.Daily)
            try { Bloomery.Daily(); } catch (Exception e) { Log.Error("Bloomery.Daily", e); } DayProfile.Lap("Bloomery.Daily");
            try { MoneyLedger.Probe169("Bloomery.Daily"); } catch { }   // f19-e1: bisekcja RB (tylko log)
            // N2-E4 (noc 16.10, noc-ekonomia2): opal mieszczan z polki miasta za zloto kiesy ludu ponad jutrzejszy koszyk - PO koniach roboczych (ta sama regula zaplaty)
            // i PO dymarkach (drewno dymarek pierwsze), w tej samej pozycji MFolk ksiegi pieniadza (kiesa -> kasa miasta); wlasna ramka ksiegi towarow FCons ("mieszczanie")
            try { TownFolk.FirewoodDaily(); } catch (Exception e) { Log.Error("TownFolk.FirewoodDaily", e); } DayProfile.Lap("TownFolk.FirewoodDaily");
            // p6-spichlerz (R3-06f): wsie miast sprzedaja miastu zboze ponad 98% sufitu spichlerza (migawka Famine.Daily tej doby) - kasa miasta -> kiesa wsi w pozycji MFolk
            try { TownGranary.Daily(); } catch (Exception e) { Log.Error("TownGranary.Daily", e); } DayProfile.Lap("TownGranary.Daily");
            // m96-dlugi: rady miast - pozyczka na zaleglosci firm DS tej doby (PopulationLaw.Daily wyzej), odsetki, splata z nadwyzki, podatek nadzwyczajny, dobrobyt; PO kiesie
            // ludu (potrzeba klas na dzis) i PRZED pozyczkami f8 (ClanBudget - wspolny pulap dzienny kies) i CrownIncome.Begin (czesc korony w wplywie dnia); pozycja MFolk ksiegi
            try { TownDebt.Daily(); } catch (Exception e) { Log.Error("TownDebt.Daily", e); } DayProfile.Lap("TownDebt.Daily");
            try { MoneyLedger.Mark(MoneyLedger.MFolk); } catch { }
            // m102-tablica: tablica ogloszen - flagi, listy gonczne kapitanow (wywieszenie z wolnej kasy miasta - sam zapis; wyplata dopiero przy rozbiciu bandy), linia doby
            try { NoticeBoard.Daily(); } catch (Exception e) { Log.Error("NoticeBoard.Daily", e); } DayProfile.Lap("NoticeBoard.Daily");
            // wpis 86 (audyt pkt 7): budowy PO rentach - 10% od dzisiejszego dochodu
            try { WarFront.Daily(); } catch (Exception e) { Log.Error("WarFront.Daily", e); } DayProfile.Lap("WarFront.Daily");   // N5-E: warownie przy froncie (skan doby) - PRZED rozliczeniem budow
            try { MoneyLedger.Probe169("WarFront.Daily"); } catch { }   // f19-e1: bisekcja RB (tylko log)
            var gfBuild = GoodsLedger.Begin(GoodsLedger.FBuild);     // paczka 146: ksiega towarow - material zdjety przez budowy (tylko licznik)
            try { BuildFunding.Daily(); } catch (Exception e) { Log.Error("BuildFunding.Daily", e); }
            finally { GoodsLedger.End(gfBuild); } DayProfile.Lap("BuildFunding.Daily");
            try { MoneyLedger.Probe169("BuildFunding.Daily"); } catch { }   // f19-e1: bisekcja RB (tylko log)
            // ds-akumulator (DsWorksRealMaterial): zlecenia robot gminy wsi i budowy septow - prawdziwy material z polek PO budowach pana i ludu tej doby (wlasne ramki FBuild
            // z osada targu), przed migawka MBuild (kiesy wsi i ludu -> kasy targow i lud targow); klucz wylaczony - kolejka pusta, nic
            try { TownFolk.DaWorksRun(); } catch (Exception e) { Log.Error("TownFolk.DaWorksRun", e); } DayProfile.Lap("TownFolk.DaWorksRun");
            try { MoneyLedger.Mark(MoneyLedger.MBuild); } catch { }
            try { BuildDiary.Daily(); } catch (Exception e) { Log.Error("BuildDiary.Daily", e); } DayProfile.Lap("BuildDiary.Daily");
            // 24c (14.10, klucz DragonsEatHerds): smoki jedza zwierzeta z taborow i stad wsi - PRZED ksiegami (ramka FDragon w tej samej dobie co linia "Smoki (24c)")
            Prologue.DragonsApply("doba");   // m104-wojny (3): wierzchowiec Daenerys wedlug etapu smokow - PRZED karmieniem (DragonFeed widzi biezacy slot); bledy liczone w srodku
            try { DragonFeed.Daily(); } catch (Exception e) { Log.Error("DragonFeed.Daily", e); } DayProfile.Lap("DragonFeed.Daily");
            try { DragonFeed.Line(); } catch (Exception e) { Log.Error("DragonFeed.Line", e); }
            try { OreLedger.Daily(); } catch (Exception e) { Log.Error("OreLedger.Daily", e); } DayProfile.Lap("OreLedger.Daily");   // wpis 94: ksiega rudy (tylko log)
            try { GoodsLedger.Daily(); } catch (Exception e) { Log.Error("GoodsLedger.Daily", e); } DayProfile.Lap("GoodsLedger.Daily");   // paczka 146: ksiega towarow (tylko log) - zaraz po ksiedze rudy (kontrola tymi samymi liczbami)
            try { A17Diag.Daily(); } catch (Exception e) { Log.Error("A17Diag.Daily", e); }   // A17 (D1, D2): linie "Konie wedlug ramek" i "Ryby poza ramkami" (tylko log)
            try { A17Fix.Daily(); } catch (Exception e) { Log.Error("A17Fix.Daily", e); }   // A17 F1-F5 (noc 17/18.10): linie "Konie partii BK", "Ryby z niczego", "Unikaty u band", "Wynik bitwy", "Stocznie"
            try { FreeSupplies.Daily(); } catch (Exception e) { Log.Error("FreeSupplies.Daily", e); } DayProfile.Lap("FreeSupplies.Daily");   // paczka 125: dosypka RBL zablokowana / przepuszczona (tylko log)
            try { RawNoRot.Daily(); } catch (Exception e) { Log.Error("RawNoRot.Daily", e); } DayProfile.Lap("RawNoRot.Daily");   // 174.3: linia "BK gnicie surowcow (174.3)" (tylko log)
            try { HarvestValve.Daily(); } catch (Exception e) { Log.Error("HarvestValve.Daily", e); } DayProfile.Lap("HarvestValve.Daily");   // E6/W3 (221): regulator wsi - start pomiaru, co 28 dob spis stosu i decyzja, linia "Regulator wsi (221)"
            try { CastleSteel.Daily(); } catch (Exception e) { Log.Error("CastleSteel.Daily", e); } DayProfile.Lap("CastleSteel.Daily");   // 177-1: linia "Wedrowcy BK" - sztaby i wegiel zdjete z ladunku wedrowcow
            try { TravellerGoods36.Daily(); } catch (Exception e) { Log.Error("TravellerGoods36.Daily", e); } DayProfile.Lap("TravellerGoods36.Daily");   // 3.6: linia "Wedrowcy BK - towar (3.6)"
            try { TravellerTrade36b.Daily(); } catch (Exception e) { Log.Error("TravellerTrade36b.Daily", e); } DayProfile.Lap("TravellerTrade36b.Daily");   // 36b: linia "Wedrowcy BK - prawdziwy towar (36b)"
            try { QohorMasters.Daily(); } catch (Exception e) { Log.Error("QohorMasters.Daily", e); } DayProfile.Lap("QohorMasters.Daily");   // 177-3: dzien pracy mistrzow Qohoru (rece kowali, wegiel z polki)
            try { VillageWoodlot.Daily(); } catch (Exception e) { Log.Error("VillageWoodlot.Daily", e); } DayProfile.Lap("VillageWoodlot.Daily");   // paczka 126: las wsi - drewno kazdej wsi bez drwali (tylko log)
            try { SpoilsSeal.Daily(); } catch (Exception e) { Log.Error("SpoilsSeal.Daily", e); } DayProfile.Lap("SpoilsSeal.Daily");   // paczka 128: Spoils of War bez sprzedazy automatycznej i bez zlota z niczego (tylko log)
            try { SpoilsCompany.Daily(); } catch (Exception e) { Log.Error("SpoilsCompany.Daily", e); } DayProfile.Lap("SpoilsCompany.Daily");   // klan najemnikow Spoils tylko z prawdziwych zolnierzy (tylko log)
            // NW O4 (ciche logi, LogGate): linia "Ceny surowcow" tylko w dobie pomiaru; w innej dobie same potkniecia doby (RawPrice.QuietDay)
            if (LogGate.Lines) { try { RawPrice.Daily(); } catch (Exception e) { Log.Error("RawPrice.Daily", e); } }
            else { try { RawPrice.QuietDay(); } catch (Exception e) { Log.Error("RawPrice.QuietDay", e); } }
            DayProfile.Lap("RawPrice.Daily");     // cena surowcow od niedoboru: linia "Ceny surowcow:" (tylko log)
            try { XbowPrice.Daily(); } catch (Exception e) { Log.Error("XbowPrice.Daily", e); } DayProfile.Lap("XbowPrice.Daily");   // kusze wedlug luku (noc 15/16.10): linia "Kusze wedlug luku (zakupy)" (tylko log)
            try { VillageFruit.Daily(); } catch (Exception e) { Log.Error("VillageFruit.Daily", e); } DayProfile.Lap("VillageFruit.Daily");       // placek i marchew (B1): linia "Owoce wsi" (tylko log)
            try { VillageFruit.EggDaily(); } catch (Exception e) { Log.Error("VillageFruit.EggDaily", e); } DayProfile.Lap("VillageFruit.EggDaily");   // N2-E1: linia "Jaja wsi (N2-E1)" (tylko log)
            try { VillageFruit.HandsDaily(); } catch (Exception e) { Log.Error("VillageFruit.HandsDaily", e); } DayProfile.Lap("VillageFruit.HandsDaily");   // E3-D1: przeliczenie f i c na nowa dobe i linia "Rece wsi za cena (E3-D1)"
            try { FoodSubstitute.Daily(); } catch (Exception e) { Log.Error("FoodSubstitute.Daily", e); } DayProfile.Lap("FoodSubstitute.Daily");   // placek i marchew (B2): linia "Zamienniki jedzenia (B2)" (tylko log)
            try { NoMakerBudget.Daily(); } catch (Exception e) { Log.Error("NoMakerBudget.Daily", e); } DayProfile.Lap("NoMakerBudget.Daily");   // N2-E2: linia "Budzet bez producenta (N2-E2)" (tylko log)
            try { FoodThrift.Daily(); } catch (Exception e) { Log.Error("FoodThrift.Daily", e); } DayProfile.Lap("FoodThrift.Daily");           // placek i marchew (B3): linia "Lordowie a drogie jedzenie (B3)" (tylko log)
            try { CompanionPay.Daily(); } catch (Exception e) { Log.Error("CompanionPay.Daily", e); } DayProfile.Lap("CompanionPay.Daily");       // towarzysze (C): linia "Towarzysze (C): dzien" (tylko log; ok. 3 wyceny na wolnego wedrowca)
            try { CompanionAutotest.Daily(); } catch (Exception e) { Log.Error("CompanionAutotest.Daily", e); } DayProfile.Lap("CompanionAutotest.Daily");   // CW/CG (recenzja): tylko autotest CrashScribe - najem wedrowca, wyplata probna
            try { CompanionWage.Daily(); } catch (Exception e) { Log.Error("CompanionWage.Daily", e); } DayProfile.Lap("CompanionWage.Daily");   // CW (19.10): linia "Towarzysze (CW): dzien" (zold druzyny gracza przed/po, 2 liczenia zoldu)
            try { MineralOnce.Daily(); } catch (Exception e) { Log.Error("MineralOnce.Daily", e); } DayProfile.Lap("MineralOnce.Daily");   // powtorzenia mineralu zdjete z list produkcji BK (tylko log)
            // NW O4: linie "Dowoz" tylko w dobie pomiaru; w innej dobie samo zerowanie licznikow (Daily konczy sie Reset - bez tego linia probna liczylaby N dob)
            try { if (LogGate.Lines) MarketRoad.Daily(); else MarketRoad.Reset(); } catch (Exception e) { Log.Error("MarketRoad.Daily", e); } DayProfile.Lap("MarketRoad.Daily");   // wpis 100: dowoz wsi zamkowych na targi (log)
            try { VillageClogDiag.Daily(); } catch (Exception e) { Log.Error("VillageClogDiag.Daily", e); } DayProfile.Lap("VillageClogDiag.Daily");   // diagnoza zatkanych wsi - zaraz po "Dowoz (skutki)" (tylko log)
            try { MarketCarts.Daily(); } catch (Exception e) { Log.Error("MarketCarts.Daily", e); } DayProfile.Lap("MarketCarts.Daily");   // poprawka 119: wozy wsi do najlepiej placacego miasta, cena ladunku sztuka po sztuce (log + porzadki wiesci z drogi)
            try { FibreToMarket.Daily(); } catch (Exception e) { Log.Error("FibreToMarket.Daily", e); } DayProfile.Lap("FibreToMarket.Daily");   // N2-E3: linia "Wlokno do miasta targowego (N2-E3)" i porzadki pamieci wozow
            try { CastleMarket.Daily(); } catch (Exception e) { Log.Error("CastleMarket.Daily", e); } DayProfile.Lap("CastleMarket.Daily");   // E5/B4 (199): targ przy zamku - porzadki rezerwacji i strazy, linia "Targ przy zamku (5.11)" (po wozach 119)
            try { CartTownExit.Daily(); } catch (Exception e) { Log.Error("CartTownExit.Daily", e); } DayProfile.Lap("CartTownExit.Daily");   // paczka 130: bezpiecznik (woz w miescie od N dob - do domu) i linia "Wozy w miastach:"
            try { CartGuard.Daily(); } catch (Exception e) { Log.Error("CartGuard.Daily", e); } DayProfile.Lap("CartGuard.Daily");   // m106-wozy W2: straznik zatkanego wozu, jednorazowe zawrocenie bez drogi, linia "Wozy (m106): dzien"
            try { CaravanBulk.Daily(); } catch (Exception e) { Log.Error("CaravanBulk.Daily", e); } DayProfile.Lap("CaravanBulk.Daily");   // wpis 103: surowce masowe w karawanach (przeliczenie swiata + log)
            try { CaravanAmmo.Daily(); } catch (Exception e) { Log.Error("CaravanAmmo.Daily", e); } DayProfile.Lap("CaravanAmmo.Daily");   // paczka 172b: linia "Karawany bez amunicji (172b)" (zablokowane, sprzedane z taborow, w taborach)
            try { CaravanGoods.Daily(); } catch (Exception e) { Log.Error("CaravanGoods.Daily", e); } DayProfile.Lap("CaravanGoods.Daily");   // E3-T1: linia "Karawany - towar tam, gdzie maja zysk (E3-T1)", porzadki ksiegi kosztu i blokad odkupienia
            try { TownToll.Daily(); } catch (Exception e) { Log.Error("TownToll.Daily", e); } DayProfile.Lap("TownToll.Daily");   // E3-G3: linia "Myto targowe (E3-G3)" - PRZED MoneyLedger.Daily (Pompy F czyta Last*)
            try { HarvestFairs.Daily(); } catch (Exception e) { Log.Error("HarvestFairs.Daily", e); } DayProfile.Lap("HarvestFairs.Daily");   // R3-05a: otwarcie/zamkniecie jarmarkow, linia "Jarmarki po zniwach (R3-05a)"
            try { RiverBarges.Daily(); } catch (Exception e) { Log.Error("RiverBarges.Daily", e); }   // R3-05b: linia "Barki rzeczne (R3-05b)"
            // 179 (projekt etapu 2, krok C3): rycerze bez lenna - zold za dobe w druzynie pana (przed zwrotem korony - w jego podstawie), powroty i wezwania;
            // linia "Rycerze (179)"; przed budzetem 166 (pulap pana liczy zold rycerzy w jego druzynie)
            try { GentryService.Daily(); } catch (Exception e) { Log.Error("GentryService.Daily", e); } DayProfile.Lap("GentryService.Daily");
            // 2.6 (projekt etapu 2; zasada wpisu 86): kazdy krok korony we wlasnym try - wyjatek w jednym nie zatrzymuje reszty rozliczenia korony
            try { KingdomTreasury.Daily(); } catch (Exception e) { Log.Error("KingdomTreasury.Daily", e); } DayProfile.Lap("KingdomTreasury.Daily");            // powinnosci wasali wobec korony (po rentach)
            try { KingdomTreasury.Levies(); } catch (Exception e) { Log.Error("KingdomTreasury.Levies", e); } DayProfile.Lap("KingdomTreasury.Levies");          // danina wojenna, clo, mennica, monopole
            try { Ransom178.ThirdsSettle(); } catch (Exception e) { Log.Error("Ransom178.ThirdsSettle", e); } DayProfile.Lap("Ransom178.ThirdsSettle");          // 178: 1/9 z licznikow doby do skarbcow koron w wojnie (prawo trzecich) - przed wplywami dnia
            // 165 (projekt etapu 2, 2.0b): wplywy dnia i 1/360 zapasu -> rata kredytu w Banku (186) -> dary (182) -> raty reparacji -> kontrakty najemnikow (185) -> zwrot zoldu -> renty (180); kazdy krok we wlasnym try
            try { CrownIncome.Begin(); } catch (Exception e) { Log.Error("CrownIncome.Begin", e); } DayProfile.Lap("CrownIncome.Begin");                    // wplywy dnia krolestw (po powinnosciach, daninie, cle i 1/3 zaworu zamkow)
            try { CrownBorrow.Instalments(); } catch (Exception e) { Log.Error("CrownBorrow.Instalments", e); } DayProfile.Lap("CrownBorrow.Instalments");        // 186: sredni podatek, odsetki i rata kredytu korony w Banku - pierwszy wydatek z wplywow dnia
            try { TownFolk.CrownLoansPay(); } catch (Exception e) { Log.Error("TownFolk.CrownLoansPay", e); } DayProfile.Lap("TownFolk.CrownLoansPay");          // korona2: odsetki i raty pozyczek koron od miast (po racie Banku 186, przed darami 182)
            try { CrownGifts.Daily(); } catch (Exception e) { Log.Error("CrownGifts.Daily", e); } DayProfile.Lap("CrownGifts.Daily");                      // 182: Polnoc -> Straz, Wolne Miasta -> Dothrakowie (linia "Dary koron (182)")
            try { CrownIncome.Reparations(); } catch (Exception e) { Log.Error("CrownIncome.Reparations", e); } DayProfile.Lap("CrownIncome.Reparations");        // raty reparacji korona -> korona
            try { MercContract.Daily(); } catch (Exception e) { Log.Error("MercContract.Daily", e); } DayProfile.Lap("MercContract.Daily");                  // 185: kontrakty najemnikow AI z wplywow dnia (linia "Kontrakty najemnikow (185)")
            try { CrownCallToWar.Daily(); } catch (Exception e) { Log.Error("CrownCallToWar.Daily", e); } DayProfile.Lap("CrownCallToWar.Daily");
            try { Ransom178.CrownStep(); } catch (Exception e) { Log.Error("Ransom178.CrownStep", e); } DayProfile.Lap("Ransom178.CrownStep");                // 178: nagroda za wielkiego jenca ze skarbca (wplywy dnia, potem zapas ponad rezerwe), srednia wplywow korony              // 168 dodatek: czesc dnia ceny wezwania sojusznika do wojny ze skarbca (linia "Wezwania do wojny (168)")
            try { KingdomTreasury.WageRefund(); } catch (Exception e) { Log.Error("KingdomTreasury.WageRefund", e); } DayProfile.Lap("KingdomTreasury.WageRefund");  // zwrot zoldu w wojnie (i linia niedoplaty 169c)
            // DS F3 (recenzja 17.10): po zwrocie zoldu (zawsze pierwszy z wydatkow wojska), przed W5 i rentami 180 - Bank bierze zastawione clo korony w zaleglosci, potem splata
            // zaliczek mieszczan z rat nadania; oba najwyzej z reszty wplywow dnia (bez F3 i bez dlugow - nic)
            try { CrownBorrow.SeizePledged(); } catch (Exception e) { Log.Error("CrownBorrow.SeizePledged", e); }
            try { TownFolk.AntRepayDay(); } catch (Exception e) { Log.Error("TownFolk.AntRepayDay", e); }   // (bez wlasnego okrazenia profilu - linia profilu jak WGRANIE 74)
            try { CrownRents.Daily(); } catch (Exception e) { Log.Error("CrownRents.Daily", e); } DayProfile.Lap("CrownRents.Daily");                    // 180: reszta wplywow dnia do rodow wedlug lenn, za sluzbe (linia "Renty korony (180)")
            try { TownFolk.BreadRest(); } catch (Exception e) { Log.Error("TownFolk.BreadRest", e); } DayProfile.Lap("TownFolk.BreadRest");              // M3: chleb - korony bez kroku rent, pan zastepczo, linia "Chleb biedoty (M3)" (przed CrownIncome.End)
            try { KingdomTreasury.NeedAidDay(); } catch (Exception e) { Log.Error("KingdomTreasury.NeedAidDay", e); } DayProfile.Lap("KingdomTreasury.NeedAidDay");        // W5: linia "Doplata wedlug potrzeby (W5)" - krok biegnie w CrownRents.Daily (po warunkach rodow, przed podzialem rent); tu tylko linia doby
            try { KingdomTreasury.ScutageDay(); } catch (Exception e) { Log.Error("KingdomTreasury.ScutageDay", e); } DayProfile.Lap("KingdomTreasury.ScutageDay");        // W6: linia "Tarczowe (W6)" - krok biegnie w kroku W5 (CrownRents.Daily); tu tylko linia doby, zaraz po linii W5
            try { LordsEvenUp.Daily(); } catch (Exception e) { Log.Error("LordsEvenUp.Daily", e); } DayProfile.Lap("LordsEvenUp.Daily");                  // E5/B3 (Z15-5): linia "Wyrownanie panow zamkow (E5/B3)" - stan, udzial zaworu i wagi rent czynne, zawor i renty dnia wedlug rodzaju rodu (zlota nie rusza)
            try { DsSeason.Daily(); } catch (Exception e) { Log.Error("DsSeason.Daily", e); } DayProfile.Lap("DsSeason.Daily");   // DS (F3): probki korony, sesje nadan wojennych (kwartal, przelaczenie, nowa wojna, pokoj), linie "Daniny korony (DS)" - PO W5 i rentach, PRZED 184 (s z nadania), DayOf czynne
            try { KingdomTreasury.WarTaxDaily(); } catch (Exception e) { Log.Error("KingdomTreasury.WarTaxDaily", e); } DayProfile.Lap("KingdomTreasury.WarTaxDaily");   // E5/B2 (184): potrzeba korony z tej doby (kredyt 186, zwrot ponad wplywy, zapas wojny), krok stawki co 7 dob, linia - przed CrownIncome.End (DayOf czynne)
            try { TownFolk.CrownProbe(); } catch (Exception e) { Log.Error("TownFolk.CrownProbe", e); } DayProfile.Lap("TownFolk.CrownProbe");   // E5/0 (195): sonda korony - CrownIncome.DayOf czynne tylko do End (tylko odczyt)
            // L5b: liczby doby skarbcow stolic (linia 165 i "Obieg" czytaja Last*) i karawan zajetych w zdobytych miastach ("Obieg: notable i karawany") - przed End
            try { CapitalTreasury.DayRoll(); } catch (Exception e) { Log.Error("CapitalTreasury.DayRoll", e); }
            try { EnemyCaravans.DayRoll(); } catch (Exception e) { Log.Error("EnemyCaravans.DayRoll", e); }
            try { CrownIncome.End(); } catch (Exception e) { Log.Error("CrownIncome.End", e); } DayProfile.Lap("CrownIncome.End");                        // migawka skarbcow i linia "Korona: wplywy dnia (165)"
            try { CrownBorrow.Report(); } catch (Exception e) { Log.Error("CrownBorrow.Report", e); } DayProfile.Lap("CrownBorrow.Report");                  // 186: linia "Kredyt korony (186)" (zlota nie rusza)
            try { TownFolk.CrownLoansLend(); } catch (Exception e) { Log.Error("TownFolk.CrownLoansLend", e); } DayProfile.Lap("TownFolk.CrownLoansLend");        // korona2: pozyczki koron od miast do zapasu wojny (po End - migawka 165 przesunieta), linia "Pozyczki koron (korona2)"; przed "Skarbce:" i MCrown
            try { KingdomLedger.Daily(); } catch (Exception e) { Log.Error("KingdomLedger.Daily", e); } DayProfile.Lap("KingdomLedger.Daily");                // linia "Skarbce:"
            try { MoneyLedger.Mark(MoneyLedger.MCrown); } catch { }
            // W7: stawka wojenna pana - rody, ktorym W5 (danina, skarbiec, tarczowe) nie pokrylo potrzeby, biora z nadwyzki ludu i wsi wlasnych lenn (po P1 i danine wsi);
            // po bloku korony (plan z kroku W5 w CrownRents.Daily; poza pozycja "korona" ksiegi osad), przed budzetem rodow 166 (podniesienie pulapu tej doby)
            try { KingdomTreasury.LordLevyDay(); } catch (Exception e) { Log.Error("KingdomTreasury.LordLevyDay", e); } DayProfile.Lap("KingdomTreasury.LordLevyDay");
            // P1b: migawka kary lojalnosci za danine P1 tej doby (czesc kiesy ludu zabrana) i kosztu W7 tej doby (wspolna podloga) - po W7; czyta ja model lojalnosci
            // w Town.DailyTick osad do nastepnego bloku doby; linia "Lojalnosc za danine (P1b)" i komunikat gracza (poborcy wyszli)
            try { FolkAidLoyalty.Daily(); } catch (Exception e) { Log.Error("FolkAidLoyalty.Daily", e); } DayProfile.Lap("FolkAidLoyalty.Daily");
            try { SoldierPay.Daily(); } catch (Exception e) { Log.Error("SoldierPay.Daily", e); } DayProfile.Lap("SoldierPay.Daily");   // zold do obiegu: linia "Zold:" i liczniki doby (po zwrocie ze skarbca)
            try { ArmyClothing.Daily(); } catch (Exception e) { Log.Error("ArmyClothing.Daily", e); } DayProfile.Lap("ArmyClothing.Daily");   // 150: linia "Odziez wojska (150):" (zlota nie rusza)
            // r4-mur: najazdy glodnych Wolnych Ludzi (migawka spichlerzy z Famine.Daily tej doby) - tuz PRZED OutlawLaw.Daily: ludzie z domow do puli i z puli do band
            // w tym samym miejscu doby co naplyw biedy OutlawLaw, a linia puli wyrzutkow liczy dzisiejsze narodziny i rozwiazania band najazdu
            try { FreeFolkRaids.Daily(); } catch (Exception e) { Log.Error("FreeFolkRaids.Daily", e); } DayProfile.Lap("FreeFolkRaids.Daily");
            try { OutlawLaw.Daily(); } catch (Exception e) { Log.Error("OutlawLaw.Daily", e); } DayProfile.Lap("OutlawLaw.Daily");   // wyrzutki: bieda, powroty, werbunek band
            try { BanditLimit.Daily(); } catch (Exception e) { Log.Error("BanditLimit.Daily", e); } DayProfile.Lap("BanditLimit.Daily");   // f8-bandy: linia "Bandy i limit (f8-bandy): dzien N" - bandy wobec celu, narodziny, koszt AI band, doba realna (PO OutlawLaw.Daily - czas jego odcinka z profilu doby)
            try { MoneyLedger.Probe169("BanditLimit.Daily"); } catch { }   // f19-e1: bisekcja RB (tylko log)
            try { IronBank.Daily(); } catch (Exception e) { Log.Error("IronBank.Daily", e); } DayProfile.Lap("IronBank.Daily");   // Bank Zelazny: pozyczki AI, raty, bankructwa
            // 166 + 162m (projekt etapu 2, 2.0b): budzet rodow po Banku, przed ksiega rodow (na D z wczoraj); osobna pozycja ksiegi pieniadza (dwor do kas siedzib,
            // sakiewki zwolnionych do kies wsi) - granica MRest przed, MBudget po
            try { MoneyLedger.Mark(MoneyLedger.MRest); } catch { }
            // E5/A3 (163): markietani - ludzie partii rodow w polu wydaja czesc wolnej sakiewki: kasa najblizszego niewrogiego miasta i kiesa wsi; osobna pozycja ksiegi
            // (MSutlers); po zwrocie korony i Banku (K3 licznik na jutro, jak OnLeft), przed budzetem rodow (projekt etapu 5, rozdz. 5.2 pkt 6)
            try { Sutlers.Daily(); } catch (Exception e) { Log.Error("Sutlers.Daily", e); } DayProfile.Lap("Sutlers.Daily");   // linia "Markietani (E5/A3)"
            try { SavingsByNeed.Daily(); } catch (Exception e) { Log.Error("SavingsByNeed.Daily", e); } DayProfile.Lap("SavingsByNeed.Daily");   // m96: zalogi wydaja wolna sakiewke w osadzie - ta sama pozycja ksiegi (MSutlers); linia "Sakiewki wedlug potrzeby (m96)"
            try { TownStay.Daily(); } catch (Exception e) { Log.Error("TownStay.Daily", e); } DayProfile.Lap("TownStay.Daily");   // m109-musztra: partie rodow na postoju w miescie i zamku wydaja czesc wolnej sakiewki - ta sama pozycja ksiegi (MSutlers); linia "Postoj w osadzie (m109): wydatki"
            try { MoneyLedger.Mark(MoneyLedger.MSutlers); } catch { }
            // E5/B6 (201): pula sakiewek weteranow wydana w miastach karczm (zycie, SutlerDailyShare; pule bez weteranow - cale); osobna pozycja ksiegi (MVetPurse);
            // przed budzetem rodow - sakiewki zwolnionych 166 tej doby wchodza do puli jutro wydawane
            try { VetPurse.Daily(); } catch (Exception e) { Log.Error("VetPurse.Daily", e); } DayProfile.Lap("VetPurse.Daily");
            try { MoneyLedger.Mark(MoneyLedger.MVetPurse); } catch { }
            try { ReaverLaw.Roll(); } catch (Exception e) { Log.Error("ReaverLaw.Roll", e); }   // WW2 (wyspy-wolni): lup doby (rabunki, dziesiecina korsarzy z OutlawLaw.Daily) do okna 28 dob - przed budzetem rodow
            try { MoneyLedger.Probe169("ReaverLaw.Roll"); } catch { }   // f19-e1: bisekcja RB (tylko log)
            try { ClanBudget.Daily(); } catch (Exception e) { Log.Error("ClanBudget.Daily", e); } DayProfile.Lap("ClanBudget.Daily");   // linia "Budzet rodow (166)"
            try { MoneyLedger.Probe169("ClanBudget.Daily"); } catch { }   // f19-e1: bisekcja RB (tylko log)
            try { CourtGoods.Daily(); } catch (Exception e) { Log.Error("CourtGoods.Daily", e); } DayProfile.Lap("CourtGoods.Daily");   // E3-P1 (noc-ekonomia3 czesc 2): linia "Dwor - towar z polki (E3-P1)" - po dworach pana (ClanBudget) i krola (CrownRents) tej doby
            try { VetPurse.Line(); } catch (Exception e) { Log.Error("VetPurse.Line", e); } DayProfile.Lap("VetPurse.Line");   // E5/B6 (201): linia "Weterani - pieniadze (E5/B6)" - ta sama doba co 166 (zloto nie rusza)
            try { DebtLadder.Report(); } catch (Exception e) { Log.Error("DebtLadder.Report", e); } DayProfile.Lap("DebtLadder.Report");   // 168: linia "Dlugi (168)" - po budzecie (kredyt wojenny dnia); zloto nie rusza
            try { TownFolk.TownLoanLine(); } catch (Exception e) { Log.Error("TownFolk.TownLoanLine", e); } DayProfile.Lap("TownFolk.TownLoanLine");   // f8-pozyczka: linia "Pozyczki miejskie (f8)" - po 168 (pozyczki dnia z 166); zloto nie rusza
            try { MoneyLedger.Mark(MoneyLedger.MBudget); } catch { }
            // TP (16.10, recenzja): splata dlugow osad wobec gracza (kasa osady, gospodarz, panujacy) i linia "Turniej-platnik (TP): dzien N" - PO ClanBudget.Daily,
            // zeby rody placily z budzetu 166 TEJ doby (takze pierwsza polnoc po wczytaniu); PO Mark(MBudget) - przelewy kas osad w "pozostale moduly ticku", nie w budzecie
            try { TourneyPay.Daily((int)CampaignTime.Now.ToDays - 1); } catch (Exception e) { Log.Error("TourneyPay.Daily", e); } DayProfile.Lap("TourneyPay.Daily");
            try { MoneyLedger.Probe169("TourneyPay.Daily"); } catch { }   // f19-e1: bisekcja RB (tylko log)
            try { TourneyFolkPurse.Daily((int)CampaignTime.Now.ToDays - 1); } catch (Exception e) { Log.Error("TourneyFolkPurse.Daily", e); } DayProfile.Lap("TourneyFolkPurse.Daily");   // f8-sakiewka (18.10): linia "Sakiewka turniejowa (f8): dzien N" (tylko log; przelewy biegna przy turnieju)
            try { MoneyLedger.Probe169("TourneyFolkPurse.Daily"); } catch { }   // f19-e1: bisekcja RB (tylko log)
            try { TownInns.Daily((int)CampaignTime.Now.ToDays - 1); } catch (Exception e) { Log.Error("TownInns.Daily", e); } DayProfile.Lap("TownInns.Daily");   // KA (16.10): linia "Karczmy (KA): dzien N" - noclegi, zaplata, kontrola obiegu, premia (tylko log)
            try { MoneyLedger.Probe169("TownInns.Daily"); } catch { }   // f19-e1: bisekcja RB (tylko log)
            try { RoadsideInns.Daily((int)CampaignTime.Now.ToDays - 1); } catch (Exception e) { Log.Error("RoadsideInns.Daily", e); } DayProfile.Lap("RoadsideInns.Daily");   // sw-zajazdy: linia "Zajazdy (ZJ): dzien N" - wejscia, noclegi, kiesy wsi, kontrola obiegu, widok
            try { MoneyLedger.Probe169("RoadsideInns.Daily"); } catch { }   // f19-e1: bisekcja RB (tylko log)
            try { MapChatLog.Daily((int)CampaignTime.Now.ToDays - 1); } catch (Exception e) { Log.Error("MapChatLog.Daily", e); } DayProfile.Lap("MapChatLog.Daily");   // m105-dziennik: linia "Dziennik (m105): dzien N" - przesuniecia, ramka, handel na tabliczkach (tylko log)
            try { SmithXp.Daily((int)CampaignTime.Now.ToDays - 1); } catch (Exception e) { Log.Error("SmithXp.Daily", e); }   // m109-kuznia: linia "Kowalstwo wolniej (m109): dzien N" i test autotestu (plik armoury-kuznia.flag)
            try { ForgeBill.Daily((int)CampaignTime.Now.ToDays - 1); } catch (Exception e) { Log.Error("ForgeBill.Daily", e); }   // m109-kuznia: linia "Kuznia wymagania (m109): dzien N" - odswiezenia ekranu, braki, przycisk
            try { ForgeBillTest.Daily((int)CampaignTime.Now.ToDays - 1); } catch (Exception e) { Log.Error("ForgeBillTest.Daily", e); }   // m109-kuznia: autotest rachunek wobec zuzycia (tylko plik armoury-kuznia.flag)
            try { Rumours.Daily((int)CampaignTime.Now.ToDays - 1); } catch (Exception e) { Log.Error("Rumours.Daily", e); } DayProfile.Lap("Rumours.Daily");   // sw-zajazdy: linia "Plotki (PL): dzien N" - sluchane, ksiega band, wiesci
            try { InnDrink.Daily((int)CampaignTime.Now.ToDays - 1); } catch (Exception e) { Log.Error("InnDrink.Daily", e); }   // m98-plotki: linia "Kufle (m98): dzien N"
            try { Lektor.Daily((int)CampaignTime.Now.ToDays - 1); } catch (Exception e) { Log.Error("Lektor.Daily", e); }   // m103-lektor: linia "Lektor (m103): dzien N" - okna, plotki, znaki, bledy API
            try { MoneyLedger.Probe169("Rumours.Daily"); } catch { }   // f19-e1: bisekcja RB (tylko log)
            try { ClanIncomeBook.Daily(); } catch (Exception e) { Log.Error("ClanIncomeBook.Daily", e); } DayProfile.Lap("ClanIncomeBook.Daily");   // paczka 169: D rodow, budzet i dlugi na sucho (tylko log) - po rentach, zwrocie, mennicy i Banku dnia
            try { MoneyLedger.Probe169("ClanIncomeBook.Daily"); } catch { }   // f19-e1: bisekcja RB (tylko log)
            try { Measure169c.Daily(); } catch (Exception e) { Log.Error("Measure169c.Daily", e); } DayProfile.Lap("Measure169c.Daily");         // 169c: linie pomiarow etapu 2 (tylko log) - po ksiedze rodow (D staly, wplyw doby), przed ksiega pieniadza
            try { OthersRise.Daily(); } catch (Exception e) { Log.Error("OthersRise.Daily", e); } DayProfile.Lap("OthersRise.Daily");           // E4/2 (190): linie "Inni z poleglych (E4/2)" i "Ludzie (ksiega)" - zaraz po "Wzrost Innych (169c)"
            try { ArmsFromPeople.Daily(); } catch (Exception e) { Log.Error("ArmsFromPeople.Daily", e); } DayProfile.Lap("ArmsFromPeople.Daily");   // E4/8 (164b-2): linie "Wojsko z ludzi (E4/8)" i "Miasta (E4/8 R2)" - po ksiedze ludzi (PeopleBook.Daily w OthersRise.Daily), przed Measure40
            try { MoneyLedger.Probe169("ArmsFromPeople.Daily"); } catch { }   // f19-e1: bisekcja RB (tylko log)
            // E4/9 (192): linia "Straz z Westeros" (odcinek Strazy zamkniety w PeopleBook.Daily, zwolnienia 166 doby z ClanBudget.Daily), budzet pul na nowa dobe; potem smierc
            // niewolnych i linia "Niewolni" - PO uzgodnieniu doby i FreeHands.Update, PRZED Measure40 (W11 czyta licznik pojmanych) i PeopleLedger (czyta WatchLaw.Last*)
            try { WatchLaw.Daily(); } catch (Exception e) { Log.Error("WatchLaw.Daily", e); } DayProfile.Lap("WatchLaw.Daily");
            try { ThrallLaw.Daily(); } catch (Exception e) { Log.Error("ThrallLaw.Daily", e); } DayProfile.Lap("ThrallLaw.Daily");
            try { MoneyLedger.Probe169("ThrallLaw.Daily"); } catch { }   // f19-e1: bisekcja RB (tylko log)
            try { PeopleScale.Daily(); } catch (Exception e) { Log.Error("PeopleScale.Daily", e); } DayProfile.Lap("PeopleScale.Daily");   // GL-0: skale "ludzie okregu" (u_v, m_t, podzamcze) - spis i pamiec doby PO zamknieciu doby ksiegi ludzi (OthersRise -> PeopleBook.Daily), linie P0 "Skale ludzi (GL0)" (tylko log)
            try { VillageByPeople.Daily(); } catch (Exception e) { Log.Error("VillageByPeople.Daily", e); } DayProfile.Lap("VillageByPeople.Daily");   // GL-1 (P1): plon wsi od ludzi - kalibracja g (nowa kampania), przejscia stanu, linia "Plon wsi od ludzi (GL1)", co 7 dob tabela krolestw i plon-wsi.csv - PO PeopleScale.Daily (pamiec skal tej doby)
            try { PeopleMarket.Daily(); } catch (Exception e) { Log.Error("PeopleMarket.Daily", e); } DayProfile.Lap("PeopleMarket.Daily");   // GL-2/GL-3: linia "Rynek od ludzi (GL2)" (okno nawiasow konsumpcji od wczoraj), co 7 dob tabela krolestw - PO PeopleScale.Daily i TownFolk.Daily (tylko log)
            try { MoneyLedger.Probe169("PeopleMarket.Daily"); } catch { }   // f19-e1: bisekcja RB (tylko log)
            try { PeopleLevy.Daily(); } catch (Exception e) { Log.Error("PeopleLevy.Daily", e); } DayProfile.Lap("PeopleLevy.Daily");   // GL-4 (P4): czynniki poboru i milicji od ludzi na jutro (pamiec skal tej doby), linia "Pobor i milicja od ludzi (GL4)", co 7 dob tabela krolestw - PO PeopleScale.Daily
            try { MoneyLedger.Probe169("PeopleLevy.Daily"); } catch { }   // f19-e1: bisekcja RB (tylko log)
            try { GoodsRates.Daily(); } catch (Exception e) { Log.Error("GoodsRates.Daily", e); } DayProfile.Lap("GoodsRates.Daily");   // GL-5 (P5): linia "Stawki towarow (GL5)" (tylko log)
            try { GoodsPaths.Daily(); } catch (Exception e) { Log.Error("GoodsPaths.Daily", e); } DayProfile.Lap("GoodsPaths.Daily");   // ZC1: sztaby z polek -> zelazo, budzet marmuru, linia "Towary (ZC1)"
            try { MoneyLedger.Probe169("GoodsPaths.Daily"); } catch { }   // f19-e1: bisekcja RB (tylko log)
            try { NightEcon.Daily(); } catch (Exception e) { Log.Error("NightEcon.Daily", e); } DayProfile.Lap("NightEcon.Daily");   // noc 16.10: linia "Noc-ekonomia (NE): dzien" (futra, kopalnie w miastach, drewno kuzni - tylko log)
            try { VineOlive.Daily(); } catch (Exception e) { Log.Error("VineOlive.Daily", e); } DayProfile.Lap("VineOlive.Daily");   // B6: linia "Winnice i gaje (B6)" (tylko log)
            // ZC0 (16.10, zycie codzienne - pomiar na sucho): linie "Zycie (ZC0)" i "Spizarnia (ZC)", co 7 dob krolestwa i stany-dostatek.csv - PO TownFolk.Daily (zarobek klas),
            // GoodsLedger.Daily (towar ludu doby), Famine.Daily (najedzeni), PopulationLaw.Daily (renta wsi), ClanIncomeBook.Daily (D rodow) i ThrallLaw.Daily (konto niewolnych)
            // ZC6 (16.10, Dostatek czynny): awans i spadek stanow w tym samym bloku - kupno ziemi z kies wsi do panow (GiveGoldAction) osobna pozycja "Przeplywy osad"
            // (MZc6); migawka MRest przed blokiem = te same liczby "pozostale moduly ticku" co dotad (dotad zlapalaby je migawka MRest przed wywozem kupcow)
            try { MoneyLedger.Mark(MoneyLedger.MRest); } catch { }
            try { DailyLife.Daily(); } catch (Exception e) { Log.Error("DailyLife.Daily", e); } DayProfile.Lap("DailyLife.Daily");
            try { MoneyLedger.Mark(MoneyLedger.MZc6); } catch { }
            // KS (kalibracja startu): PO DailyLife.Daily (stany doby) - wynik Planu z watku roboczego, Plan co 7 dob, w stanie zastosowanym stawki wsi na jutro, regula zmiany zajec
            // (przebudowa: kiesa wsi -> kasa miasta), pola mieszczan (kasa miasta -> kiesy ludu) - wlasna pozycja "Przeplywy osad" (MKs)
            try { DistrictWorks.BeforeProduction(); } catch (Exception e) { Log.Error("DistrictWorks.BeforeProduction", e); } DayProfile.Lap("DistrictWorks.BeforeProduction");   // inwest (recenzja): b prac okregu tej doby PRZED rachunkiem KS i planem zajec (ta sama doba co wyplaty przy wozach)
            try { StartCalibration.Daily(); } catch (Exception e) { Log.Error("StartCalibration.Daily", e); } DayProfile.Lap("StartCalibration.Daily");
            try { MoneyLedger.Probe169("StartCalibration.Daily"); } catch { }   // f19-e1: bisekcja RB (tylko log)
            // inwest (noc 17/18.10): PO KS (bloki KS doby) - zajecia wiosek bez KS (plany produkcji, przebudowy: kiesa wsi -> kasa miasta), ikona okregu (co 28 dob), prace okregu
            // (rampa, roznice ksiegi wiosek PO VillageBook.Daily, odbudowa, linia PO) - przed znacznikiem MKs (ta sama natura przeplywu: przebudowa zajec wsi)
            try { VillageCrafts.Daily(); } catch (Exception e) { Log.Error("VillageCrafts.Daily", e); } DayProfile.Lap("VillageCrafts.Daily");
            try { MoneyLedger.Probe169("VillageCrafts.Daily"); } catch { }   // f19-e1: bisekcja RB (tylko log)
            try { DistrictIcon.Daily(); } catch (Exception e) { Log.Error("DistrictIcon.Daily", e); } DayProfile.Lap("DistrictIcon.Daily");
            try { MoneyLedger.Probe169("DistrictIcon.Daily"); } catch { }   // f19-e1: bisekcja RB (tylko log)
            try { DistrictWorks.Daily(); } catch (Exception e) { Log.Error("DistrictWorks.Daily", e); } DayProfile.Lap("DistrictWorks.Daily");
            try { SideVillageFief.Daily(); } catch (Exception e) { Log.Error("SideVillageFief.Daily", e); }
            try { FiefInvestments.Daily(); } catch (Exception e) { Log.Error("FiefInvestments.Daily", e); }   // m112-inwest: komunikat wyplat prac i majatkow minionej doby, linia "Majatki i prace w Fiefs (m112)"
            try { SideVillageVisit.Daily(); } catch (Exception e) { Log.Error("SideVillageVisit.Daily", e); }   // m97-wioski: linia "Wioski poboczne (WP): dzien N" (tylko z ruchem)   // n20-wioska: linia "Wioska gracza (n20): dzien N" i srednia 7 dob udzialu
            try { MoneyLedger.Mark(MoneyLedger.MKs); } catch { }
            // p6-a7: roboty miast z rak okregu - PO KS i VillageCrafts (przebudowy A7 tej doby juz w kasach): miasto ponad pulapem 1/28 nadwyzki do kies wsi okregu; wlasna pozycja (MTownHire)
            try { TownHire.Daily(); TownHire.Line(); } catch (Exception e) { Log.Error("TownHire.Daily", e); } DayProfile.Lap("TownHire.Daily");
            try { MoneyLedger.Mark(MoneyLedger.MTownHire); } catch { }
            try { RansomFlows.Daily(); } catch (Exception e) { Log.Error("RansomFlows.Daily", e); } DayProfile.Lap("RansomFlows.Daily");         // 2.14: linia "Okupy (2.14)" i harness niewoli w autotescie (doby sesji 5 i 8)
            try { Ransom178.Report(); } catch (Exception e) { Log.Error("Ransom178.Report", e); } DayProfile.Lap("Ransom178.Report");           // 178: linia "Okupy (178)" (zloto nie rusza)
            try { VassalRansomAid.Report(); AiDowry.Report(); } catch (Exception e) { Log.Error("R3-01cd.Report", e); }   // R3-01d/R3-01c: linie "Pomoc wasali (R3-01d)" i "Posagi AI (R3-01c)"
            // M6a: linie "Okupy powtorne (M6a-2)", "Najemnicy - sluzba (M6a-1)" (z przegladem pola gry ShouldStayInKingdomUntil) i "Pokoj a jency (M6a-3)"
            try { RansomRepeat.Report(); } catch (Exception e) { Log.Error("RansomRepeat.Report", e); } DayProfile.Lap("RansomRepeat.Report");
            try { MercTenure.Daily(); } catch (Exception e) { Log.Error("MercTenure.Daily", e); } DayProfile.Lap("MercTenure.Daily");
            try { PeaceHold.Daily(); } catch (Exception e) { Log.Error("PeaceHold.Daily", e); } DayProfile.Lap("PeaceHold.Daily");
            try { MoneyLedger.Probe169("PeaceHold.Daily"); } catch { }   // f19-e1: bisekcja RB (tylko log)
            // 171: zawrocony towar i nadwyzki zalog na polkach PRZED handlem kupcow (kupcy wywioza nadwyzke zamkow tego samego dnia)
            try { GarrisonCarts.Daily(); } catch (Exception e) { Log.Error("GarrisonCarts.Daily", e); } DayProfile.Lap("GarrisonCarts.Daily");     // zamowienia zamkow w drodze: dostawy, zawrocenia, linia "Zaopatrzenie zamkow (171)"
            try { MoneyLedger.Probe169("GarrisonCarts.Daily"); } catch { }   // f19-e1: bisekcja RB (tylko log)
            try { GarrisonArmory.Daily(); } catch (Exception e) { Log.Error("GarrisonArmory.Daily", e); } DayProfile.Lap("GarrisonArmory.Daily");   // nadwyzki zalog raz w tygodniu, linia "Zbrojownie zalog (171): dzien"
            try { MoneyLedger.Probe169("GarrisonArmory.Daily"); } catch { }   // f19-e1: bisekcja RB (tylko log)
            try { GarrisonKit.ClanFillOnce(); } catch (Exception e) { Log.Error("GarrisonKit.ClanFillOnce", e); } DayProfile.Lap("GarrisonKit.ClanFillOnce");   // sklad7b-p: raz na sesje - wypelnienie zbrojowni partii Twojego rodu
            try { MoneyLedger.Probe169("GarrisonKit.ClanFillOnce"); } catch { }   // f19-e1: bisekcja RB (tylko log)
            try { ArmsDrill.Daily(); } catch (Exception e) { Log.Error("ArmsDrill.Daily", e); } DayProfile.Lap("ArmsDrill.Daily");             // linie "Cwiczenia (171)" i co 5 dob "Pokrycie zbrojowni AI (171)" (tylko log)
            try { MoneyLedger.Probe169("ArmsDrill.Daily"); } catch { }   // f19-e1: bisekcja RB (tylko log)
            try { Measure174b.Daily(); } catch (Exception e) { Log.Error("Measure174b.Daily", e); } DayProfile.Lap("Measure174b.Daily");         // 174b.0: linie "Zbroja na polkach (174b)" i "ZakupyAI wedlug kupujacego (174b)" (tylko log)
            try { Drill.Daily(); } catch (Exception e) { Log.Error("Drill.Daily", e); } DayProfile.Lap("Drill.Daily");                     // musztra: "Musztra (gracz)", "Musztra AI", co 5 dob "Musztra AI wedlug krolestw"; zasilenie zapasu w autotescie
            try { MoneyLedger.Probe169("Drill.Daily"); } catch { }   // f19-e1: bisekcja RB (tylko log)
            try { SkillLedger.Daily(); } catch (Exception e) { Log.Error("SkillLedger.Daily", e); } DayProfile.Lap("SkillLedger.Daily");         // E8/P1 (243): linie "Umiejetnosci (E8/P1)", "XP wojska (E8/P1)", co 7 dob "Poziomy lordow (E8/P1)" (tylko log)
            try { XpFair.Daily(); } catch (Exception e) { Log.Error("XpFair.Daily", e); } DayProfile.Lap("XpFair.Daily");                 // E8/X (248): linia "Doswiadczenie (E8/X)" - gra / po zmianie (strzal, jedzenie, zwiad, drobne, RBL)
            try { NightScout.Daily(); } catch (Exception e) { Log.Error("NightScout.Daily", e); } DayProfile.Lap("NightScout.Daily");   // R3-10a: linia "Zwiad noca (R3-10a)"
            try { MoneyLedger.Mark(MoneyLedger.MRest); } catch { }   // 174.2 / B-2: kasy osad przed wywozem kupcow - pozostale moduly osobno
            try { SupplyDemand.DailyTrade(); } catch (Exception e) { Log.Error("SupplyDemand.DailyTrade", e); } DayProfile.Lap("SupplyDemand.DailyTrade");   // E6/R3 (215): w srodku wozy kupcow (TradeCarts - dostawy, wywoz, linia "Przewoz kupcow wozem")
            try { MoneyLedger.Probe169("SupplyDemand.DailyTrade"); } catch { }   // f19-e1: bisekcja RB (tylko log)
            try { AiGear.BardingDaily(); } catch (Exception e) { Log.Error("AiGear.BardingDaily", e); } DayProfile.Lap("AiGear.BardingDaily");   // E6/R3 (215): linia "Uprzaz (215)" - po spisie polek w DailyTrade (tylko log)
            try { MoneyLedger.Probe169("AiGear.BardingDaily"); } catch { }   // f19-e1: bisekcja RB (tylko log)
            try { ToolsByUse.Daily(); } catch (Exception e) { Log.Error("ToolsByUse.Daily", e); } DayProfile.Lap("ToolsByUse.Daily");         // E6/R3 (215): linia "Narzedzia wedlug zuzycia (215)" (tylko log)
            try { MoneyLedger.Mark(MoneyLedger.MTrade); } catch { }  // B-2: wywoz kupcow osobna pozycja w "Przeplywy osad" (kasy zamkow placa za konie i uprzaz - tylko z nadwyzki)
            try { MaterialOrders.Daily(); } catch (Exception e) { Log.Error("MaterialOrders.Daily", e); } DayProfile.Lap("MaterialOrders.Daily");   // 174.2: kontrakty surowca dla prawdziwych karawan (po handlu bronia i wozach zamkow), linia "Kontrakty surowca (174)"
            try { MoneyLedger.Mark(MoneyLedger.MOrders); } catch { }
            try { ArmsScrap.Daily(); } catch (Exception e) { Log.Error("ArmsScrap.Daily", e); } DayProfile.Lap("ArmsScrap.Daily");   // 174 pytanie 4: pomiar popytu koszykow zawsze; skup na zlom tylko przy OldStockToScrap (domyslnie wylaczony)
            try { MoneyLedger.Probe169("ArmsScrap.Daily"); } catch { }   // f19-e1: bisekcja RB (tylko log)
            try { SellByCondition.Daily(); } catch (Exception e) { Log.Error("SellByCondition.Daily", e); } DayProfile.Lap("SellByCondition.Daily");   // cena sprzedazy sprzetu: linia "Skup sprzetu" wedlug sprzedajacego (tylko log)
            try { MoneyLedger.Probe169("SellByCondition.Daily"); } catch { }   // f19-e1: bisekcja RB (tylko log)
            try { RecruitCost.Daily(); } catch (Exception e) { Log.Error("RecruitCost.Daily", e); } DayProfile.Lap("RecruitCost.Daily");   // poprawka 157 / paczka 160: linia "Konie rekrutow" (tylko log)
            try { MoneyLedger.Probe169("RecruitCost.Daily"); } catch { }   // f19-e1: bisekcja RB (tylko log)
            try { RotLeaks37.Daily(); } catch (Exception e) { Log.Error("RotLeaks37.Daily", e); } DayProfile.Lap("RotLeaks37.Daily");   // 3.7: linia "ROT szczelnosc (3.7): dzien" (tylko gdy cos sie zdarzylo)
            try { ShelfBuy.Daily(); } catch (Exception e) { Log.Error("ShelfBuy.Daily", e); } DayProfile.Lap("ShelfBuy.Daily");   // ceny hurtu (Jeff 09.10 08:00): linia "Ceny hurtu (09.10)" - pomiar petli po sztukach i doplata (tylko log)
            try { MoneyLedger.Probe169("ShelfBuy.Daily"); } catch { }   // f19-e1: bisekcja RB (tylko log)
            try { ShelfIndex.DailyOwn(); } catch (Exception e) { Log.Error("ShelfIndex.DailyOwn", e); } DayProfile.Lap("ShelfIndex.DailyOwn");   // E8/O1 (265): linia "Polka i delegaty (E8/O1)" - pamiec polki po wlasnej zmianie, delegaty DTE/BK (tylko log)
            try { WorkshopLaw.DailyFast(); } catch (Exception e) { Log.Error("WorkshopLaw.DailyFast", e); } DayProfile.Lap("WorkshopLaw.DailyFast");   // E8/O2 (266): linia "Warsztaty bez powtorzen (E8/O2)" - pamieci i klucze warsztatow, probka stara / nowa droga (tylko log)
            try { MoneyLedger.Probe169("WorkshopLaw.DailyFast"); } catch { }   // f19-e1: bisekcja RB (tylko log)
            try { Log.DailyO6((int)CampaignTime.Now.ToDays - 1); } catch (Exception e) { Log.Error("Log.DailyO6", e); } DayProfile.Lap("Log.DailyO6");   // E8/O6 (270): linia "Log (E8/O6)" - pisarze logu, zapisy stracone, proba 3 dob: linie wyslane wobec linii w plikach (tylko log)
            try { MountedWage.Daily(); } catch (Exception e) { Log.Error("MountedWage.Daily", e); } DayProfile.Lap("MountedWage.Daily");   // paczka 160: linia "Zold konnych (160)" - sklad wojska i premia konnego (tylko log)
            try { CavalryTemplate.Daily(); } catch (Exception e) { Log.Error("CavalryTemplate.Daily", e); } DayProfile.Lap("CavalryTemplate.Daily");   // E6/K3 (219): linia "Rycerze i jazda (219)" - spis z petli MountedWage.Daily wyzej, PRZED HorseCensus.Daily (czyta jego stan "czeka" przed zerowaniem)
            try { HorseCensus.Daily(); } catch (Exception e) { Log.Error("HorseCensus.Daily", e); } DayProfile.Lap("HorseCensus.Daily");   // 175.0: linia "Konie AI (175)" + konie-krolestwa.csv (stan doby policzony w petli MountedWage.Daily wyzej)
            try { GoldenKits.Daily(); } catch (Exception e) { Log.Error("GoldenKits.Daily", e); } DayProfile.Lap("GoldenKits.Daily");   // PLAN 1.6 (14.10): linia "Zlota Kompania (PLAN 1.6)" - zestawy, potrzeba i pokrycie lukow kusznikow ZK (tylko log, tylko przy Army175GoldenBows)
            try { MoneyLedger.Probe169("GoldenKits.Daily"); } catch { }   // f19-e1: bisekcja RB (tylko log)
            try { RockGold.Daily(); } catch (Exception e) { Log.Error("RockGold.Daily", e); } DayProfile.Lap("RockGold.Daily");   // 24d (14.10): linia "Zloto Casterly Rock (24d)" - wydobycie kruszcow Skaly przed sufitem 0 (tylko log, tylko przy CasterlyGoldExhausted)
            try { SaddleLaw.Daily(); } catch (Exception e) { Log.Error("SaddleLaw.Daily", e); } DayProfile.Lap("SaddleLaw.Daily");   // E8/H (271): linia "Jezdzcy i uprzaz (E8/H)" - spis par kon + uprzaz (partie rodow AI, zalogi, rod gracza) i liczniki bramy, zakupow, bitwy (tylko log)
            try { MoneyLedger.Probe169("SaddleLaw.Daily"); } catch { }   // f19-e1: bisekcja RB (tylko log)
            try { KingdomBalance.Daily(); } catch (Exception e) { Log.Error("KingdomBalance.Daily", e); } DayProfile.Lap("KingdomBalance.Daily");   // 175.0: linia "Balans krolestw (175)" + balans-krolestw.csv (tylko log)
            try { MoneyLedger.Daily(); } catch (Exception e) { Log.Error("MoneyLedger.Daily", e); } DayProfile.Lap("MoneyLedger.Daily");     // K1: "Pieniadz swiata" i "Przeplywy osad" (tylko log) - po calym naszym rozliczeniu doby
            try { Stage6Measure.Daily(); } catch (Exception e) { Log.Error("Stage6Measure.Daily", e); } DayProfile.Lap("Stage6Measure.Daily");   // E6/0 (206): ksiega uzbrojenia, ksiega koni, warsztaty wedlug krainy (tylko log) - stan po wszystkich przeplywach dnia
            try { WorkshopInputs.Daily(); } catch (Exception e) { Log.Error("WorkshopInputs.Daily", e); } DayProfile.Lap("WorkshopInputs.Daily");   // E6/M2 (208): linia "Warsztaty wedlug surowca (208)" przy wyborach typu (tylko log)
            try { CamelLaw.Daily(); } catch (Exception e) { Log.Error("CamelLaw.Daily", e); } DayProfile.Lap("CamelLaw.Daily");   // E6/M2 (208): linia "Wierzchowce kanonu (208)" (tylko log)
            try { HorseState.Daily(); } catch (Exception e) { Log.Error("HorseState.Daily", e); } DayProfile.Lap("HorseState.Daily");   // E6/K0 (209): stan koni partii od nowa (po ksiegach dnia, PO Stage6Measure) i linia "Marsz - konie partii (E6/K0)"
            try { VetKit.Daily(); } catch (Exception e) { Log.Error("VetKit.Daily", e); } DayProfile.Lap("VetKit.Daily");   // C3 (Jeff 14.10): komplety weteranow - nadmiar puli na polki (do domu) i linia "Komplet weterana (C3)" - PO Stage6Measure, jak pula koni
            try { GearFit.Daily(); } catch (Exception e) { Log.Error("GearFit.Daily", e); } DayProfile.Lap("GearFit.Daily");   // GearFit (noc 15/16.10): pierwsza doba sesji - spis wzorcow i par krewnych (po prawach CrashScribe i SkillSinew), potem linia "GearFit: dzien N"
            try { HorseFate.Daily(); } catch (Exception e) { Log.Error("HorseFate.Daily", e); } DayProfile.Lap("HorseFate.Daily");   // E6/K1 (211): pula koni weteranow (nadmiar na polki), linia "Konie - smierc, lup, dobytek (E6/K1)" i zerowanie dnia - PO Stage6Measure (HorseBook czyta liczniki K1)
            try { LootTally.Daily(); } catch (Exception e) { Log.Error("LootTally.Daily", e); } DayProfile.Lap("LootTally.Daily");   // m109-lup: linia "Lup bitew (m109)" - konie, uprzaz i zbroje z bitew gracza (tylko log, przy bitwach doby)
            try { PackMarket.Daily(); } catch (Exception e) { Log.Error("PackMarket.Daily", e); } DayProfile.Lap("PackMarket.Daily");   // E6/K5 (213): linia "Juczne (213)" - nowe karawany z doby i luzaki pod jukami (spis partii na ladzie)
            try { PackSources.Daily(); } catch (Exception e) { Log.Error("PackSources.Daily", e); } DayProfile.Lap("PackSources.Daily");   // m6b (4): linia "Juczne z niczego (m6b)"
            try { HamletKinds.Daily(); } catch (Exception e) { Log.Error("HamletKinds.Daily", e); } DayProfile.Lap("HamletKinds.Daily");   // E6/W2 (210): linia "Wioski wedlug rodzaju (210)" za dobe wczorajsza (tylko log)
            try { WorldClock7.Daily(); } catch (Exception e) { Log.Error("WorldClock7.Daily", e); } DayProfile.Lap("WorldClock7.Daily");   // E7/0 (224): linie "Wojna i pokoj (E7/0)" i "Turnieje (E7/0)" (tylko log) - po ksiedze pieniadza
            try { TerrainEase.Stage7Daily(); } catch (Exception e) { Log.Error("TerrainEase.Stage7Daily", e); } DayProfile.Lap("TerrainEase.Stage7Daily");   // E7/T (225): linia "Kary terenu (E7/T)" i zerowanie probki doby (tylko log) - po E7/0
            try { MapRoads.Daily(); } catch (Exception e) { Log.Error("MapRoads.Daily", e); } DayProfile.Lap("MapRoads.Daily");   // SW-DROGI (noc 17/18.10): linia "Drogi (DR): dzien N" (spis, audyt premii, koszt) w dobie linii (tylko log)
            try { MapRoadKinds.Daily(); } catch (Exception e) { Log.Error("MapRoadKinds.Daily", e); } DayProfile.Lap("MapRoadKinds.Daily");   // m96-valyria: linia "Drogi Valyrii (VR): dzien N" (spis, liczenia) w dobie linii (tylko log)
            try { MapMud.Daily(); } catch (Exception e) { Log.Error("MapMud.Daily", e); } DayProfile.Lap("MapMud.Daily");   // p5-bloto (R3-07c): linia "Bloto (BL): dzien N" (spis deszczu i blota, audyt kary, koszt) w dobie linii (tylko log)
            try { Gallows.Daily(); } catch (Exception e) { Log.Error("Gallows.Daily", e); } DayProfile.Lap("Gallows.Daily");   // p6-szubienice (R3-07a): koniec czasu szubienic, linia "Szubienice (p6): dzien N" w dobie linii
            try { SeaStorms.Daily(); } catch (Exception e) { Log.Error("SeaStorms.Daily", e); } DayProfile.Lap("SeaStorms.Daily");   // p6-sztormy (R3-05d): linia "Sztormy (MS): dzien N" (burze, statki w burzy, zima / poza zima, audyt kary) w dobie linii (tylko log)
            try { AiRoadRoutes.Daily(); } catch (Exception e) { Log.Error("AiRoadRoutes.Daily", e); } DayProfile.Lap("AiRoadRoutes.Daily");   // p6-drogi (R3-07d): linia "AI drogi (AD): dzien N" (objazdy, odrzucone, spis, koszt) w dobie linii (tylko log)
            try { TwinsCrossing.Daily(); } catch (Exception e) { Log.Error("TwinsCrossing.Daily", e); } DayProfile.Lap("TwinsCrossing.Daily");   // E7/B (231): linia "Przeprawa Bliznaki (E7/B)" i zerowanie licznikow doby (tylko log)
            try { CrossingLaw.Daily(); } catch (Exception e) { Log.Error("CrossingLaw.Daily", e); } DayProfile.Lap("CrossingLaw.Daily");   // E8/Q (262): linia "Straz przeprawy (E8/Q)" i zerowanie licznikow doby - PO linii E7/B (jej dopisek czyta te same liczniki)
            try { VillageNews.Daily(); } catch (Exception e) { Log.Error("VillageNews.Daily", e); } DayProfile.Lap("VillageNews.Daily");   // E7/W1 (232): linia "Wioski - wiesci (E7/W1)" - PO VillageBook.Daily (spalone w ksiedze tej doby, T5) i zerowanie licznikow
            try { DistrictRaid.Daily(); } catch (Exception e) { Log.Error("DistrictRaid.Daily", e); } DayProfile.Lap("DistrictRaid.Daily");   // E7/R1 (235): linia "Rabunek osada po osadzie (E7/R1)", koniec czujnosci, sprzatanie - PO W1 (te same doby ksiegi), PRZED Devastation.DayLine
            try { VillageHerds.Daily(); } catch (Exception e) { Log.Error("VillageHerds.Daily", e); } DayProfile.Lap("VillageHerds.Daily");   // R3-06c (p5-stada): odrost stad i linia "Stada (R3-06c)"
            try { TiredLand.Daily(); } catch (Exception e) { Log.Error("TiredLand.Daily", e); } DayProfile.Lap("TiredLand.Daily");            // R3-06d (p5-stada): linia "Zmeczona ziemia (R3-06d)"
            try { ReaverLaw.Daily(); } catch (Exception e) { Log.Error("ReaverLaw.Daily", e); } DayProfile.Lap("ReaverLaw.Daily");   // wyspy-wolni: linie "Najezdzcy - decyzje AI (WW0)", "Zelazna cena (WW1)" - PO linii E7/R1 (te same sesje rabunku)
            try { MapVillageLore.Daily(); } catch (Exception e) { Log.Error("MapVillageLore.Daily", e); } DayProfile.Lap("MapVillageLore.Daily");   // E7/W2 (233): linia "Dymek wioski (E7/W2)" (dymki i potkniecia doby), w pierwszej dobie sesji probka wiary BK 20 wiosek (T4)
            try { DistrictMenu.Daily(); } catch (Exception e) { Log.Error("DistrictMenu.Daily", e); } DayProfile.Lap("DistrictMenu.Daily");   // E7/W3 (234): w pierwszej dobie sesji linia startowa "Menu okregu (E7/W3): start" (index pozycji po wszystkich modach), potem linia doby tylko przy uzyciu
            try { VillageHolders.Daily(); } catch (Exception e) { Log.Error("VillageHolders.Daily", e); } DayProfile.Lap("VillageHolders.Daily");   // Jeff 16.10 (D1): w pierwszej dobie sesji linia "Herby wiosek (D1): start" - gospodarze wiosek wedlug reguly, roznych herbow, przyklady
            try { VillageHamlets.Daily(); } catch (Exception e) { Log.Error("VillageHamlets.Daily", e); } DayProfile.Lap("VillageHamlets.Daily");   // sw-przysiolki: co 7 dob linia "Przysiolki (sw-przysiolki): dzien N" - ludzie przysiolkow, spalone, nakladka KS
            try { TownFolk.Lines(); } catch (Exception e) { Log.Error("TownFolk.Lines", e); } DayProfile.Lap("TownFolk.Lines");   // E5/0 (195): linie Kiesa ludu, Nadwyzka kas, Bitwy, Notable, Jedzenie z targu, Zadowolenie BK - po ksiedze pieniadza, przed Seal31.Daily (zeruje Measure31)
            // 3.2: linia "Kiesy rozbitych (3.2)" i zerowanie licznikow doby - PO ksiedze pieniadza, ktora je czyta, i PRZED VillageTakings.Daily
            // (czyta jego liczniki sakw taborow oddanych zwyciezcom - recenzja 3.2 (2))
            try { BeatenPurse.Daily(); } catch (Exception e) { Log.Error("BeatenPurse.Daily", e); } DayProfile.Lap("BeatenPurse.Daily");
            try { BattlePlunder.Daily(); } catch (Exception e) { Log.Error("BattlePlunder.Daily", e); } DayProfile.Lap("BattlePlunder.Daily");   // E5/A6 (198): linia "Lup w bitwie (A6)" i zerowanie licznikow doby - PO ksiedze pieniadza (zloto ksiegi nie rusza)
            try { Seal31.Daily(); } catch (Exception e) { Log.Error("Seal31.Daily", e); } DayProfile.Lap("Seal31.Daily");   // 3.1 (31): linia "Szczelnosc (164a)" i zerowanie pozycji - PO ksiedze pieniadza, ktora je czyta
            try { NotableGold.Daily(); } catch (Exception e) { Log.Error("NotableGold.Daily", e); } DayProfile.Lap("NotableGold.Daily");   // E5/A3 (164c): linia "Notable (E5/A3)" i zerowanie licznikow - PO ksiedze pieniadza (dopisek "Obieg: notable i karawany")
            try { SmallGold.Daily(); } catch (Exception e) { Log.Error("SmallGold.Daily", e); } DayProfile.Lap("SmallGold.Daily");   // E5/A7: linia "Drobne zrodla (E5/A7)" i zerowanie licznikow doby (zloto ksiegi nie rusza)
            try { BkDipGold.Daily(); } catch (Exception e) { Log.Error("BkDipGold.Daily", e); } DayProfile.Lap("BkDipGold.Daily");   // E5/A8: linia "BK i Diplomacy (E5/A8)" i zerowanie licznikow doby (zloto ksiegi nie rusza)
            try { GentryNames.Daily(); } catch (Exception e) { Log.Error("GentryNames.Daily", e); } DayProfile.Lap("GentryNames.Daily");   // trop gentry (audyt modow 14.10): linia "Nazwy gentry BK: dzien N" (nowe rody gentry wedlug krolestwa) i zerowanie licznikow doby
            try { BesiegedPay.Daily(); } catch (Exception e) { Log.Error("BesiegedPay.Daily", e); } DayProfile.Lap("BesiegedPay.Daily");   // E5/A4 (196): linia "Oblezone (E5/A4)" i zerowanie licznikow (renta tej doby, polityki od poprzedniej linii; zloto nie rusza)
            try { ShipsToPort181.Daily(); } catch (Exception e) { Log.Error("ShipsToPort181.Daily", e); } DayProfile.Lap("ShipsToPort181.Daily");   // 3.1/3.3 (33) 181: linia "Statki (181)" i zerowanie licznikow doby (zloto ksiegi nie rusza)
            try { BkSeal34.Daily(); } catch (Exception e) { Log.Error("BkSeal34.Daily", e); } DayProfile.Lap("BkSeal34.Daily");   // 3.4: linia "Szczelnosc BK (3.4)" i zerowanie licznikow - PO ksiedze pieniadza, ktora je czyta
            try { CouncilWages35.Daily(); } catch (Exception e) { Log.Error("CouncilWages35.Daily", e); } DayProfile.Lap("CouncilWages35.Daily");   // 3.5: linia "Rada (3.5)" i zerowanie licznikow - PO ksiedze pieniadza (to samo okno rozliczen rodow)
            try { EstateIncome187.Daily(); } catch (Exception e) { Log.Error("EstateIncome187.Daily", e); } DayProfile.Lap("EstateIncome187.Daily");   // 3.1/3.3 (31b) E3/2: linia "Majatki BK (187)" i zerowanie licznikow
            try { Seal31B.Daily(); } catch (Exception e) { Log.Error("Seal31B.Daily", e); } DayProfile.Lap("Seal31B.Daily");   // 3.1/3.3 (31b): linia "Szczelnosc (164a/31b)" i zerowanie licznikow - PO ksiedze pieniadza
            try { VillageTakings.Daily(); } catch (Exception e) { Log.Error("VillageTakings.Daily", e); } DayProfile.Lap("VillageTakings.Daily");   // 112: linia "Utarg wsi (112)" i zerowanie licznikow doby - PO ksiedze pieniadza, ktora je czyta
            try { DsVillage.Daily(); } catch (Exception e) { Log.Error("DsVillage.Daily", e); } DayProfile.Lap("DsVillage.Daily");
            try { MineRegale.Daily(); } catch (Exception e) { Log.Error("MineRegale.Daily", e); } DayProfile.Lap("MineRegale.Daily");   // R3-09f: linia "Regale gornicze (R3-09f)" i zerowanie licznikow doby
            try { DsTowns.Daily(); } catch (Exception e) { Log.Error("DsTowns.Daily", e); } DayProfile.Lap("DsTowns.Daily");   // DS (F3): linie "Firmy miast i zamkow (DS)" i "Pozyczki zaliczkowe (DS)" za okno od poprzedniej doby   // DS (F1): linia "Daniny wsi (DS)" za okno od poprzedniej doby (wozy, markietani, czynsz, skrzynia, stare pobory w rampie) i zerowanie licznikow
            try { TownFolk.DaDaily(); } catch (Exception e) { Log.Error("TownFolk.DaDaily", e); } DayProfile.Lap("TownFolk.DaDaily");   // ds-akumulator: linia "Akumulator kies (ds-akumulator)" (danina od skarbca ludu, roboty z prawdziwym materialem, lepszy stol, jednorazowe wplywy korony, kiesy)
            try { Measure40.Daily(); } catch (Exception e) { Log.Error("Measure40.Daily", e); } DayProfile.Lap("Measure40.Daily");   // E4/0 (188): linie "Hearth", "Dobrobyt", "Jency", "Zima (E4/0)" - po 169c (milicja), przed ksiega ludzi (tylko log)
            try { PeopleLedger.Daily(); } catch (Exception e) { Log.Error("PeopleLedger.Daily", e); } DayProfile.Lap("PeopleLedger.Daily");   // demografia krok 1: "Ludzie:" i plik regionow (tylko log)
            try { Devastation.DayLine(); } catch (Exception e) { Log.Error("Devastation.DayLine", e); } DayProfile.Lap("Devastation.DayLine");   // E4/5 (113n): linia "Ludzie (spustoszenie)" i zerowanie licznikow doby - PO ksiedze ludzi i linii Hearth, ktore je czytaja
            try { MarketGlut.DailyDigest(); }
            catch (Exception e) { Log.Error("GlutDigest", e); } DayProfile.Lap("MarketGlut.DailyDigest");
            try
            {
                // poranny meldunek: braki wykrzyczane na glos, zeby Jeff WIEDZIAL
                // bez otwierania zbrojowni ("czemu ja o tym nie wiem!")
                QuartermasterLaw.ShoutShortages("QM morning: short (have/need):");
            }
            catch (Exception e) { Log.Error("MorningReport", e); }
        }

        internal bool HasProjectsHere(Settlement here)
        {
            try
            {
                if (here == null) return false;
                foreach (var line in _projects)
                {
                    var p = Project.Parse(line);
                    if (p.Valid && p.SettlementId == here.StringId) return true;   // m96-czaskuzni: z praca bez sztuki
                }
            }
            catch { }
            return false;
        }

        internal float ProjectHoursLeftHere(Settlement here)
        {
            float h = 0f;
            try
            {
                if (here == null) return 0f;
                foreach (var line in _projects)
                {
                    var p = Project.Parse(line);
                    if (p.Valid && p.SettlementId == here.StringId) h += p.DaysLeft * 24f;   // m96-czaskuzni: z praca bez sztuki
                }
            }
            catch { }
            return h;
        }

        /// <summary>Natywne kucie broni tez nie moze byc natychmiastowe - zabieramy wyrob i oddajemy po czasie.</summary>
        private void OnNewItemCrafted(ItemObject item, ItemModifier modifier, bool isCraftingOrderItem)
        {
            try { HistoricalPrices.PriceOne(item); } catch { }   // wpis 90: wykuty miecz w pensach, jak reszta swiata
            try
            {
                var s = Settings.Current;
                if (!s.WeaponCraftingTakesTime || isCraftingOrderItem) return;   // zlecenia maja swoje terminy
                if (item == null) return;
                var here = Settlement.CurrentSettlement;
                if (here == null) return;

                int tier = Recipes.Grade(item);
                float days = MathF.Max(0.1f, tier * s.WeaponDaysPerTier);   // minimum 0.1 dnia (Jeff 16.09: -80%)
                // wpis 83: godziny z ekranu BK - koszt staminy tej sztuki / 6
                if (ForgeClock.On)
                    try { days = ForgeClock.HoursOf(Campaign.Current.Models.SmithingModel.GetEnergyCostForSmithing(item, Hero.MainHero)) / 24f; } catch { }
                // C5 (Jeff 14.10 "jak u kowali AI"): bron kuta po vanillowemu - tyle dni pracy kowala, ile liczy warsztat AI (wylaczone - jak wyzej)
                float oldDays = days;
                days = ForgeClock.WorkHours(item, 1, days * 24f) / 24f;

                var roster = MobileParty.MainParty.ItemRoster;
                var el = new EquipmentElement(item, modifier);   // takim lezy w sakwach
                if (roster.GetItemNumber(item) > 0) roster.AddToCounts(el, -1);

                // JAKOSC PRZY KOWADLE (Jeff 30.08, seria Albion: "popup nie
                // pokazuje +1/-1, nie ma Legendary ani spartaczonych"):
                // vanillowe CreateCraftedWeaponInFreeBuildMode przyjmuje
                // modyfikator Z ZEWNATRZ (domyslnie null) i nasza sciezka
                // dostawala golego - kazdy "van" wychodzil pospolity BEZ
                // rzutu. Rzucamy nasza wierna formula vanilla (kuznia-1do1
                // krok 1) RAZ, tutaj - dostawa po czasie tylko oddaje wynik.
                if (modifier == null)
                    try
                    {
                        modifier = Forge.RollQuality(item, Recipes.For(item));
                        if (modifier != null)
                            Log.Info("Jakosc przy kowadle: " + item.StringId + " -> " + modifier.StringId);
                    }
                    catch (Exception qe) { Log.Error("VanQuality", qe); }

                // "van": sukces i jakosc zapadly PRZY KOWADLE (vanilla) - dostawa
                // po czasie ma NIE rzucac drugi raz (Jeff: "wykulem, a potem fail
                // i miecza nie ma"). Modyfikator jedzie z projektem i wraca.
                StartProject(item, 1, days, here, "van", modifier != null ? modifier.StringId : "");
                ForgeClock.NoteQueued(item, 1, days * 24f, oldDays * 24f);
                Log.Player("The blade is roughed out. " + Project.TimeLabel(days) + " of finishing work remain at "
                           + here.Name + (ForgeJob.QueueNote() != null ? " - " + ForgeJob.QueueNote() : ForgeClock.On ? " - the work goes on only while you stay here." : " - he works it himself, wherever you ride."));   // m96-czaskuzni: tylko na pasku
                // gra przed chwila POKAZALA "dodano do ekwipunku" - bez glosnego
                // baneru wyglada to na zniknieciecie miecza
                try
                {
                    MBInformationManager.AddQuickInformation(new TaleWorlds.Localization.TextObject(
                        "{=!}The smith keeps the " + item.Name + " for finishing - " +
                        Project.TimeLabel(days) + " at " + here.Name + ". Stay or return to collect it."));
                }
                catch { }
                Log.Info("Bron w toku: " + item.StringId + " dni=" + days);
            }
            catch (Exception e) { Log.Error("OnNewItemCrafted", e); }
        }

        // ---------------------------------------------------------- zuzycie
        private void OnMapEventStarted(MapEvent mapEvent, PartyBase a, PartyBase b)
        {
            try
            {
                if (mapEvent == null || !mapEvent.IsPlayerMapEvent) return;
                // E8/A (250, PLAN 8.8e): ksiega ciosow bohatera zaczyna kazda bitwe od zera - to, co w niej zostalo (walka poza bitwa sprzed paczki albo przy
                // wylaczonym FieldCraft w tamtej misji, bitwa przegrana), nie obije rynsztunku po tym zwyciestwie
                try
                {
                    if (Settings.Current.ArenaNoWear)
                    {
                        float stale = FieldCraft.DropLedger();
                        if (stale > 0.01f) Log.Info("Zuzycie (E8/A): poczatek bitwy - ksiega ciosow wyzerowana, " + stale.ToString("0.0") + " pkt sprzed tej bitwy przepada (rynsztunek bez zmian).");
                        HeroBook.DropLedgers("poczatek bitwy");   // m110-stan krok 2 (CompanionGearWear): ksiegi ciosow towarzyszy - ta sama regula
                    }
                }
                catch (Exception e) { Log.Error("OnMapEventStarted.DropLedger", e); }
                _hpBeforeBattle = Hero.MainHero.HitPoints;
                _lootSnapshot = SnapshotBaggage();
                SnapshotOwnRanks();   // roznica po bitwie = polegli (GatherFallen)
                _prisonerBaseline = SnapshotPrisoners();
            }
            catch (Exception e) { Log.Error("OnMapEventStarted", e); }
        }

        private void OnMapEventEnded(MapEvent mapEvent)
        {
            try
            {
                if (mapEvent == null || !mapEvent.IsPlayerMapEvent) return;

                // 17.09: pekanie kolczanow po bitwie (AmmoAttrition, Jeff 02.09) WYWALONE
                // na zadanie Jeffa ("nic ma nie pekac") - amunicja ubywa tylko w polu

                // PRZEGRANA ALBO UCIECZKA = NIC Z POLA. Wraki zbierane w trakcie
                // misji lezaly w kolejce niezaleznie od wyniku i po godzinie
                // wsypywaly sie do sakw - takze wtedy, gdy gracz uciekl z pola
                // ("jak uciekam, to nie zdobywam gearu, przeciez ucieklem" - Jeff).
                bool won = false;
                try { won = mapEvent.WinningSide == mapEvent.PlayerSide; } catch { }
                if (!won)
                {
                    int dropped = BattlefieldLaw.DropShare();
                    _lootFlushDue = CampaignTime.Zero;
                    if (dropped > 0)
                        Log.Info("Pole przegrane/opuszczone - " + dropped + " sztuk lupu zostaje na ziemi.");
                    _hpBeforeBattle = -1;
                    // E8/A (250): kleska nie obija (jak dotad) - ciosy z przegranej albo opuszczonej bitwy nie czekaja juz w ksiedze na najblizsze zwyciestwo
                    // (zerowanie na poczatku bitwy nie lapie bitwy, do ktorej gracz DOLACZYL - MapEventStarted poszlo wtedy bez gracza)
                    try
                    {
                        if (Settings.Current.ArenaNoWear)
                        {
                            float lostWear = FieldCraft.DropLedger();
                            if (lostWear > 0.01f) Log.Info("Zuzycie (E8/A): bitwa przegrana albo opuszczona - " + lostWear.ToString("0.0") + " pkt z ksiegi ciosow przepada (kleska nie obija, jak dotad).");
                            HeroBook.DropLedgers("bitwa przegrana albo opuszczona");   // m110-stan krok 2: towarzysze jak gracz
                        }
                    }
                    catch (Exception e) { Log.Error("OnMapEventEnded.DropLedger", e); }
                    return;
                }

                // okno na obszukanie jencow: ekran "wez jencow" przychodzi tuz
                // po bitwie - tylko wtedy zdzieramy z nich rynsztunek (nie przy
                // zwyklym przekladaniu jencow z lochu czy garnizonu)
                // audyt pelny W3 (04.10): okna obszukania po bitwie NIE otwieramy - sprzet jencow z walki (nieprzytomnych)
                // DTE juz wlozyl do lupu z pola; obszukanie bylo druga zaplata za ten sam rynsztunek.
                // Obszukanie zostaje tylko dla kapitulantow bez walki (OnPrisonerTaken poza bitwa).

                // wdzieczne wioski: wygrana z bandytami cieszy okolice
                BanditCheer.AfterVictory(mapEvent);

                // KRYJOWKA: zaden ekran lupow nie przyjdzie - dzialka gracza
                // z kolejki BattlefieldLaw idzie prosto do sakw, a jency
                // (bandyci poddani po walce) sa obszukiwani od reki
                if (mapEvent.IsHideoutBattle)
                {
                    BattlefieldLaw.FlushShareToBaggage("kryjowka zdobyta");
                    try { _prisonerBaseline = SnapshotPrisoners(); } catch { }   // jency z walki - bez drugiego obszukania (W3)
                    _lootFlushDue = CampaignTime.Zero;
                }
                else if (BattlefieldLaw.SharePending())
                {
                    // zwykla bitwa: ekran Spoils powinien odebrac kolejke;
                    // jesli w godzine tego nie zrobi - wysypujemy do sakw
                    _lootFlushDue = CampaignTime.HoursFromNow(1f);
                }

                // po bitwie i rozliczeniu lupow kwatermistrz melduje braki na glos
                _shortageShoutDue = CampaignTime.HoursFromNow(1f);

                if (!Settings.Current.WearEnabled) return;

                int damage = 0;
                if (_hpBeforeBattle > 0) damage = Math.Max(0, _hpBeforeBattle - Hero.MainHero.HitPoints);
                _hpBeforeBattle = -1;

                if (Settings.Current.LootArrivesWorn) WearTheLoot(damage);
                if (Settings.Current.TroopWearEnabled) WearTheTroops();

                // Zuzycie CELOWANE: FieldCraft spisal w misji, gdzie padaly ciosy
                // (zbroja tam, gdzie trafiono; bron za celne uderzenia; tarcza za bloki).
                var ledger = new float[12];
                FieldCraft.TakeLedger(ledger);
                float sum = 0f; for (int i = 0; i < 12; i++) sum += ledger[i];
                float flat = Settings.Current.WearPerBattle;
                if (sum > 0.01f)
                {
                    Log.Info("Bitwa zakonczona: obrazenia " + damage + ", zuzycie celowane " + sum.ToString("0.0") + " pkt.");
                    ApplyWearPerSlot(ledger, flat);
                }
                else if (flat > 0.01f || damage > 0)
                {
                    // bitwa automatyczna (bez misji) - stara droga: od obrazen
                    float wear = flat + damage * Settings.Current.WearDamageFactor;
                    Log.Info("Bitwa zakonczona (auto): obrazenia " + damage + ", zuzycie " + wear.ToString("0.0"));
                    ApplyWear(wear);
                }
                // m110-stan krok 2 (CompanionGearWear): sprzet towarzyszy z ich ksiag ciosow (misja) - te same reguly co gracz; autobitwa bez trafien - bez zuzycia
                try { HeroBook.ApplyBattle(); } catch (Exception e) { Log.Error("HeroBook.ApplyBattle", e); }

                CleanseAmmo();   // lupy moga przyniesc "zuzyte" strzaly z cudzych systemow - czyscimy
            }
            catch (Exception e) { Log.Error("OnMapEventEnded", e); }
        }

        /// <summary>E8/L (252): dzialka z "Send troops" wchodzi do kolejki lupow w oknie DTE, czyli PO naszym sluchaczu MapEventEnded (sluchacze biegna od
        /// ostatnio zarejestrowanego - nasz przed DTE) - termin "ekran lupow nie przyszedl, do sakw" ustawia wtedy BattlefieldLaw.QueueSimShare.</summary>
        internal static void LootFlushIn(float hours)
        {
            try { var b = Instance; if (b != null) b._lootFlushDue = CampaignTime.HoursFromNow(hours); } catch { }
        }

        private Dictionary<string,int> SnapshotBaggage()
        {
            var map = new Dictionary<string,int>();
            try
            {
                var roster = MobileParty.MainParty.ItemRoster;
                for (int i = 0; i < roster.Count; i++)
                {
                    var el = roster[i];
                    if (el.EquipmentElement.Item == null) continue;
                    var key = Key(el.EquipmentElement);
                    map[key] = (map.ContainsKey(key) ? map[key] : 0) + el.Amount;
                }
            }
            catch (Exception e) { Log.Error("SnapshotBaggage", e); }
            return map;
        }

        private static string Key(EquipmentElement el)
        {
            return el.Item.StringId + "#" + (el.ItemModifier != null ? el.ItemModifier.StringId : "");
        }

        /// <summary>Sprzet zdarty z poleglych nie jest nieskazitelny. Ktos w nim wlasnie zginal.</summary>
        /// <summary>
        /// ZUZYCIE SPRZETU WOJSKA (Jeff 27.08: "zolnierze zawsze maja 100%,
        /// jakby sprzet sie nie psul"). Zuzycie tykalo dotad WYLACZNIE sprzet
        /// bohatera - zbrojownia DTE zyla wiecznie nowa i muster kwatermistrza
        /// klamal. Po kazdej bitwie czesc sztuk W UZYCIU (wg liczby zolnierzy
        /// noszacych dany typ) schodzi o JEDEN stopien drabinki modyfikatorow -
        /// ta sama, ktora brudzi lupy. Naprawa: "Mend the men's kit" u kowala.
        /// </summary>
        private void WearTheTroops()
        {
            try
            {
                var s = Settings.Current;
                var armory = QuartermasterLaw.DteArmory();
                if (armory == null) return;
                var needs = QuartermasterLaw.CountNeeds();
                float share = MBMath.ClampFloat(s.TroopWearPercent, 0f, 100f) / 100f;
                int worn = 0;
                // wpis 96 (Jeff: "nie na sztywno - z walki"): bitwa rozgrywana - z trafien; symulacja - wedlug strat (ranni / ludzie)
                var ledger = TroopWearLedger.TakeLast();
                float intensity = 1f;
                if (ledger == null)
                {
                    try
                    {
                        var r = MobileParty.MainParty.MemberRoster;
                        float men = Math.Max(1, r.TotalManCount - r.TotalHeroes);
                        intensity = MBMath.ClampFloat((r.TotalWounded / men) / Math.Max(0.01f, s.TroopWearBaseCasualtyShare), 0.1f, 3f);
                    }
                    catch { }
                }
                var report = new List<string>();

                foreach (var type in QuartermasterLaw.KitTypes)
                {
                    int inUse = QuartermasterLaw.WornFor(type, needs);
                    if (inUse <= 0) continue;
                    int hits;
                    if (ledger != null)
                    {
                        float h; ledger.TryGetValue(type, out h);
                        float chance = type == ItemObject.ItemTypeEnum.Shield ? s.TroopWearPerBlock
                                     : (type == ItemObject.ItemTypeEnum.Bow || type == ItemObject.ItemTypeEnum.Crossbow) ? s.TroopWearPerShot
                                     : (type == ItemObject.ItemTypeEnum.OneHandedWeapon || type == ItemObject.ItemTypeEnum.TwoHandedWeapon || type == ItemObject.ItemTypeEnum.Polearm) ? s.TroopWearPerStrike
                                     : s.TroopWearPerHit;
                        float exp = h * Math.Max(0f, chance);
                        hits = (int)Math.Floor(exp + MBRandom.RandomFloat);   // ulamek - losowo
                        hits = Math.Min(hits, inUse);
                        if (h > 0f) report.Add(type + " " + (int)h + "->" + hits);
                    }
                    else hits = (int)Math.Floor(inUse * share * intensity + MBRandom.RandomFloat);
                    if (hits <= 0) continue;

                    // kandydaci: sztuki tego typu podlegajace zuzyciu
                    var idx = new List<int>();
                    for (int i = 0; i < armory.Count; i++)
                    {
                        var el = armory.GetElementCopyAtIndex(i);
                        var it = el.EquipmentElement.Item;
                        if (it == null || it.ItemType != type || el.Amount <= 0) continue;
                        if (NoWear(it) || it.ItemComponent == null || it.ItemComponent.ItemModifierGroup == null) continue;
                        idx.Add(i);
                    }
                    if (idx.Count == 0) continue;

                    for (int n = 0; n < hits; n++)
                    {
                        // wpis 96 (audyt pkt 5): kazda SZTUKA ma rowna szanse (dotad kazdy rodzaj - pojedyncze sztuki psuly sie w kolko)
                        int totalAmt = 0; foreach (var ii in idx) totalAmt += armory.GetElementCopyAtIndex(ii).Amount;
                        int pickN = MBRandom.RandomInt(Math.Max(1, totalAmt)), pickI = idx[0];
                        foreach (var ii in idx) { int a0 = armory.GetElementCopyAtIndex(ii).Amount; if (pickN < a0) { pickI = ii; break; } pickN -= a0; }
                        var el = armory.GetElementCopyAtIndex(pickI).EquipmentElement;
                        var group = el.Item.ItemComponent.ItemModifierGroup;
                        ItemModifier gsNext;   // m110-stan (GearFourStates): o jeden stan az do wraku - takze bron, tarcze i luki
                        bool gsDone = GearState.TryNext(el.Item, el.ItemModifier, out gsNext);
                        if (gsDone && gsNext == null) continue;   // juz wrak - dno drabinki
                        var bad = new List<ItemModifier>();
                        if (!gsDone)
                            foreach (var m in group.ItemModifiers)
                                if (m != null && m.PriceMultiplier < 1f) bad.Add(m);
                        if (bad.Count == 0 && !gsDone) continue;
                        bad.Sort((x, y) => x.PriceMultiplier.CompareTo(y.PriceMultiplier));   // [0] = najgorszy

                        ItemModifier next;
                        var cur = el.ItemModifier;
                        if (gsDone) next = gsNext;
                        else if (cur == null) next = bad[bad.Count - 1];               // pierwsza rysa: najlzejszy zly stan
                        else
                        {
                            next = null;                                          // nastepny gorszy od obecnego
                            for (int b = bad.Count - 1; b >= 0; b--)
                                if (bad[b].PriceMultiplier < cur.PriceMultiplier) { next = bad[b]; break; }
                            if (next == null) continue;                           // juz na dnie drabinki
                        }
                        armory.AddToCounts(el, -1);
                        armory.AddToCounts(new EquipmentElement(el.Item, next), 1);
                        worn++;
                        // indeksy moga sie przesunac po podmianie - odswiez liste raz na sztuke
                        idx.Clear();
                        for (int i = 0; i < armory.Count; i++)
                        {
                            var e2 = armory.GetElementCopyAtIndex(i);
                            var it2 = e2.EquipmentElement.Item;
                            if (it2 == null || it2.ItemType != type || e2.Amount <= 0) continue;
                            if (NoWear(it2) || it2.ItemComponent == null || it2.ItemComponent.ItemModifierGroup == null) continue;
                            idx.Add(i);
                        }
                        if (idx.Count == 0) break;
                    }
                }

                if (worn > 0)
                {
                    Log.Info("Zuzycie wojska: " + worn + " sztuk zeszlo o stopien" + (ledger != null ? " (z trafien: " + string.Join(", ", report.ToArray()) + ")" : " (symulacja, natezenie " + intensity.ToString("0.00") + ")") + ".");
                    Log.Player("The battle wore the men's kit - " + worn + " pieces the worse for it.", true);
                }
            }
            catch (Exception e) { Log.Error("WearTheTroops", e); }
        }

        private void WearTheLoot(int damageTaken)
        {
            try
            {
                var s = Settings.Current;
                var roster = MobileParty.MainParty.ItemRoster;
                var after = SnapshotBaggage();
                var changes = new List<KeyValuePair<EquipmentElement,int>>();
                // m110-stan (LootStateFromWounds): sprawne sztuki lupu z obrazen (B) wysypane wlasnie do sakw (kryjowka) maja juz swoj stan - nie dostaja loterii
                var soundB = BattlefieldLaw.TakeSoundFlushed();

                for (int i = 0; i < roster.Count; i++)
                {
                    var el = roster[i];
                    var item = el.EquipmentElement.Item;
                    if (item == null) continue;
                    if (el.EquipmentElement.ItemModifier != null) continue;              // juz ma jakis stan
                    if (item.ItemComponent == null || item.ItemComponent.ItemModifierGroup == null) continue;
                    if (!item.HasArmorComponent && !item.HasWeaponComponent) continue;
                    if (NoWear(item)) continue;                                          // strzaly/belty bez stanow

                    var key = Key(el.EquipmentElement);
                    int before = _lootSnapshot.ContainsKey(key) ? _lootSnapshot[key] : 0;
                    int gained = el.Amount - before;
                    int sb0;
                    if (soundB != null && soundB.TryGetValue(item, out sb0) && sb0 > 0)
                    {
                        int k = Math.Min(sb0, Math.Max(0, gained));
                        gained -= k; soundB[item] = sb0 - k;
                    }
                    if (gained <= 0) continue;
                    changes.Add(new KeyValuePair<EquipmentElement,int>(el.EquipmentElement, gained));
                }

                int worn = 0;
                foreach (var c in changes)
                {
                    var group = c.Key.Item.ItemComponent.ItemModifierGroup;
                    bool gs = GearState.Handles(c.Key.Item);   // m110-stan: loteria w czterech stanach (dzisiejsze udzialy na rodzine)
                    var bad = new List<ItemModifier>();
                    if (!gs)
                        foreach (var m in group.ItemModifiers)
                            if (m != null && m.PriceMultiplier < 1f) bad.Add(m);
                    if (bad.Count == 0 && !gs) continue;
                    bad.Sort((x, y) => x.PriceMultiplier.CompareTo(y.PriceMultiplier));

                    for (int n = 0; n < c.Value; n++)
                    {
                        // im ciezsza byla bitwa, tym gorszy stan lupu
                        float cond = s.LootWearBase + (MBRandom.RandomFloat - 0.5f) * 2f * s.LootWearSpread
                                     - damageTaken * 0.15f;
                        ItemModifier pick;
                        if (gs) { GearState.TryLottery(c.Key.Item, cond, out pick); if (pick == null) continue; }
                        else
                        {
                            int idx;
                            if (cond > 55f) idx = bad.Count - 1;
                            else if (cond > 30f) idx = MathF.Max(0, bad.Count - 2);
                            else idx = 0;
                            pick = bad[idx];
                        }

                        roster.AddToCounts(c.Key, -1);
                        roster.AddToCounts(new EquipmentElement(c.Key.Item, pick), 1);
                        worn++;
                    }
                }

                if (worn > 0)
                {
                    Log.Info("Lup zuzyty: " + worn + " przedmiotow.");
                    Log.Player(worn + " pieces of plunder came off the field battered. Mend them or melt them down.", true);
                }
            }
            catch (Exception e) { Log.Error("WearTheLoot", e); }
        }

        /// <summary>
        /// LUPY Z JENCOW. Jeff: "pojmalem Ravens' Teeth, to powinienem miec jego
        /// pancerz". Vanilla i Spoils of War losuja lupy tylko z pola - jeniec
        /// zabieral caly swoj rynsztunek do niewoli. Jency wchodza do niewoli
        /// ROZNYMI drzwiami (ekran po bitwie, kryjowka, automat) - zdarzenie
        /// OnPrisonerTaken lapie tylko pierwsze, wiec liczymy ROZNICE: stan
        /// jencow sprzed bitwy kontra teraz. Kazdy NOWY szeregowy jeniec w oknie
        /// po bitwie zostaje obszukany: cale jego wyposazenie wpada do sakw,
        /// zuzyte jak lupy. Lordowie NIE - za nich bierze sie okup, nie plaszcz.
        /// </summary>
        private void OnPrisonerTaken(TaleWorlds.CampaignSystem.Roster.FlattenedTroopRoster roster)
        {
            // KONIEC PODWOJNEGO LICZENIA (Jeff 29.08: "lupy po bitwie, a potem
            // jeszcze rozbieram jencow - czy to nie dubel?"). BYL DUBEL: sprzet
            // pokonanych JUZ idzie do lupow po KAZDEJ bitwie (realna - DTE
            // zbiera z pola; symulowana - pelny drop), a jeniec z bitwy to jeden
            // z pokonanych. Obszukanie go byloby DRUGA nagroda za ten sam
            // rynsztunek. Jency z bitwy: tylko aktualizacja ksiegi (zeby
            // hourly/menu ich pozniej nie "doszukalo"). Obszukiwanie zostaje
            // WYLACZNIE dla kapitulantow bez walki (dialog na mapie, poddanie
            // band) - tam zaden lup z pola nie padl.
            bool fromBattle = false;
            try
            {
                fromBattle = MapEvent.PlayerMapEvent != null
                    || TaleWorlds.CampaignSystem.Encounters.PlayerEncounter.Battle != null;
            }
            catch { }
            if (fromBattle)   // audyt pelny W3: jeniec z walki oddal sprzet w lupie z pola (DTE liczy nieprzytomnych jak poleglych)
            {
                // stary lad (bez prawa zachowania lupu): vanilla loteria juz
                // zaplacila za rannych - obszukanie byloby DRUGA nagroda
                try { _prisonerBaseline = SnapshotPrisoners(); } catch { }
                return;
            }
            // PRAWO ZACHOWANIA LUPU: loteria szablonowa wycieta, wiec jeniec
            // z bitwy placi FIZYCZNIE przy obszukaniu - raz i do naga
            // (Jeff 30.08: "jak biore jencow, rozbieramy ich do naga")
            try
            {
                var half = CampaignTime.HoursFromNow(0.5f);
                if (_spoilsWindow < half) _spoilsWindow = half;
            }
            catch { }
            TryStripNewCaptives(fromBattle ? "bitwa" : "OnPrisonerTaken");
        }

        private Dictionary<string,int> SnapshotPrisoners()
        {
            var map = new Dictionary<string,int>();
            try
            {
                var r = MobileParty.MainParty != null ? MobileParty.MainParty.PrisonRoster : null;
                if (r == null) return map;
                for (int i = 0; i < r.Count; i++)
                {
                    var el = r.GetElementCopyAtIndex(i);
                    if (el.Character == null || el.Character.IsHero) continue;
                    var id = el.Character.StringId;
                    map[id] = (map.ContainsKey(id) ? map[id] : 0) + el.Number;
                }
            }
            catch (Exception e) { Log.Error("SnapshotPrisoners", e); }
            return map;
        }

        internal void TryStripNewCaptives(string source)
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !s.CaptiveSpoilsEnabled) return;
                if (CampaignTime.Now > _spoilsWindow)
                {
                    // poza oknem: ksiega jencow ma NADAZAC za sprzedazami i
                    // zwolnieniami - inaczej kapitulanci chowaliby sie pod
                    // starym stanem i wchodzili do lochu w pelnym rynsztunku
                    _prisonerBaseline = SnapshotPrisoners();
                    return;
                }
                var roster = MobileParty.MainParty != null ? MobileParty.MainParty.PrisonRoster : null;
                var bag = MobileParty.MainParty != null ? MobileParty.MainParty.ItemRoster : null;
                if (roster == null || bag == null) return;
                if (_prisonerBaseline == null) _prisonerBaseline = new Dictionary<string,int>();

                int pieces = 0, men = 0;
                for (int i = 0; i < roster.Count; i++)
                {
                    var el = roster.GetElementCopyAtIndex(i);
                    var troop = el.Character;
                    if (troop == null || troop.IsHero) continue;
                    int before = _prisonerBaseline.ContainsKey(troop.StringId) ? _prisonerBaseline[troop.StringId] : 0;
                    int fresh = el.Number - before;
                    for (int m = 0; m < fresh; m++)
                    {
                        // ktores z jego bitewnych wyposazen - tak chodzil, tak go wzieto
                        Equipment eq = null; int n = 0;
                        foreach (var e in troop.BattleEquipments) { n++; if (MBRandom.RandomInt(n) == 0) eq = e; }
                        if (eq == null) continue;
                        men++;
                        for (int slot = 0; slot < 12; slot++)
                        {
                            if (slot == 4) continue;                                     // choragiew zostaje przy sztandarze
                            if (!s.CaptiveSpoilsIncludeMounts && slot >= 10) continue;   // kon i rzad konski
                            var item = eq[(EquipmentIndex)slot].Item;
                            if (item == null || item.ItemType == ItemObject.ItemTypeEnum.Banner) continue;
                            // smoka nie poprowadzisz na powrozie - zadnych dragon_* w sakwach
                            if (item.StringId != null && item.StringId.StartsWith("dragon_")
                                && item.ItemType == ItemObject.ItemTypeEnum.Horse) continue;
                            bag.AddToCounts(new EquipmentElement(item, PickWornModifier(item)), 1);
                            pieces++;
                        }
                    }
                }

                _prisonerBaseline = SnapshotPrisoners();   // nowy stan = nowa baza, zadnego dublowania
                if (pieces > 0)
                {
                    Log.Info("Jency obszukani (" + source + "): " + men + " ludzi, " + pieces + " sztuk do sakw.");
                    Log.Player("The captives are stripped at the rope: " + pieces + " pieces of gear go into the baggage.", true);
                }
            }
            catch (Exception e) { Log.Error("TryStripNewCaptives", e); }
        }

        /// <summary>Stan sprzetu zdartego z jenca - ta sama loteria co lupy z pola.</summary>
        /// <summary>wpis 95: pierwszy (najlagodniejszy) stopien zuzycia - zuzycie walki, nie stan lupu.</summary>
        internal static ItemModifier MildWornModifier(ItemObject item)
        {
            try
            {
                if (item == null || NoWear(item) || item.ItemComponent == null || item.ItemComponent.ItemModifierGroup == null) return null;
                ItemModifier gs;   // m110-stan: pierwszy stopien = uszkodzony
                if (GearState.TryMild(item, out gs)) return gs;
                ItemModifier best = null;
                foreach (var m in item.ItemComponent.ItemModifierGroup.ItemModifiers)
                    if (m != null && m.PriceMultiplier < 1f && (best == null || m.PriceMultiplier > best.PriceMultiplier)) best = m;
                return best;
            }
            catch { return null; }
        }

        internal static ItemModifier PickWornModifier(ItemObject item)
        {
            try
            {
                var s = Settings.Current;
                if (NoWear(item)) return null;                                  // strzaly/belty poza systemem zuzycia
                if (!item.HasArmorComponent && !item.HasWeaponComponent) return null;
                if (item.ItemComponent == null || item.ItemComponent.ItemModifierGroup == null) return null;

                if (GearState.Handles(item))
                {
                    // m110-stan: ta sama loteria w czterech stanach (udzialy na rodzine jak dotad)
                    ItemModifier gs;
                    GearState.TryLottery(item, s.LootWearBase + (MBRandom.RandomFloat - 0.5f) * 2f * s.LootWearSpread, out gs);
                    return gs;
                }
                var bad = new List<ItemModifier>();
                foreach (var m in item.ItemComponent.ItemModifierGroup.ItemModifiers)
                    if (m != null && m.PriceMultiplier < 1f) bad.Add(m);
                if (bad.Count == 0) return null;
                bad.Sort((x, y) => x.PriceMultiplier.CompareTo(y.PriceMultiplier));

                float cond = s.LootWearBase + (MBRandom.RandomFloat - 0.5f) * 2f * s.LootWearSpread;
                if (cond > 55f) return bad[bad.Count - 1];
                if (cond > 30f) return bad[MathF.Max(0, bad.Count - 2)];
                return bad[0];
            }
            catch { return null; }
        }

        /// <summary>
        /// Strzaly i belty NIE podlegaja zuzyciu - to amunicja, zuzywa sie
        /// przez WYSTRZELENIE (znika z kolczanu), nie przez "niszczenie".
        /// </summary>
        internal static bool IsAmmo(ItemObject it)
        {
            return it != null && (it.ItemType == ItemObject.ItemTypeEnum.Arrows
                               || it.ItemType == ItemObject.ItemTypeEnum.Bolts);
        }

        /// <summary>
        /// ZYWY INWENTARZ - konie, muly, swinie, byczki - NIE MA zuzycia, jak
        /// jedzenie. Kulawy kon to kulawy kon (vanilla), nie "kon 50% do
        /// naprawy w kuzni". Zuzycie ma wylacznie pancerz i bron.
        /// </summary>
        internal static bool IsBeast(ItemObject it)
        {
            return it != null && (it.HasHorseComponent
                               || it.ItemType == ItemObject.ItemTypeEnum.Horse
                               || it.ItemType == ItemObject.ItemTypeEnum.Animal);
        }

        /// <summary>
        /// TOWAR TO NIE PANCERZ. Ryba, maslo, zboze, len, glina - to ladunek,
        /// nie rynsztunek. Zjada sie go albo przerabia, nie "niszczy do 14%"
        /// (Jeff: "fish i butter nie ma zuzycia, przeciez to jedzenie").
        /// Zuzycie ma wylacznie to, co ma pancerz albo ostrze.
        /// </summary>
        internal static bool IsGoods(ItemObject it)
        {
            try
            {
                if (it == null) return true;
                return !it.HasArmorComponent && !it.HasWeaponComponent;
            }
            catch { return false; }
        }

        /// <summary>
        /// E8/C (253, PLAN 8.4c, regula R3): JEDEN PREDYKAT "POCISK" - strzaly, belty i bron miotana (oszczep, topor, noz). Schodzi przez wystrzelenie albo rzut:
        /// odzysk po bitwie gracza (AmmoRecovery), pomiar i zuzycie amunicji AI (AiWear.AmmoKind). IsAmmo (Arrows/Bolts) zostaje dla CleanseAmmo - bron miotana
        /// ma stany, ktorych nie wolno zdjac za darmo (obity oszczep z lupu wracalby do stanu fabrycznego bez kowala).
        /// </summary>
        internal static bool IsMissile(ItemObject it)
        {
            return it != null && (it.ItemType == ItemObject.ItemTypeEnum.Arrows
                               || it.ItemType == ItemObject.ItemTypeEnum.Bolts
                               || it.ItemType == ItemObject.ItemTypeEnum.Thrown);
        }

        /// <summary>E8/C: sztuka BEZ STANU w ogole (strzaly i belty, zywy inwentarz, towar) - dla widoku stanu i naprawy: dopisek w nazwie, sekcja w dymku,
        /// lista napraw kowala, stan sprzetu wojska. To dawne NoWear: bron miotana, ktora ma juz stan (lup Spoils, stary zapis), dalej go pokazuje i da sie naprawic.
        /// Recenzja E8/C: takze ksiega stanu zbrojowni AI (AiWear: spis, zakup z polki, komplety od gracza i z powrotem, dar, zwrot niesprzedanej sztuki) - inaczej
        /// obity oszczep oddany zalodze z ludzmi wracal z nimi sprawny (naprawa bez kowala), a AI kupowalo obity z polki jako sprawny. Nowego stanu bron miotana
        /// nie dostaje nigdzie (NoWear).</summary>
        internal static bool NoState(ItemObject it)
        {
            return IsAmmo(it) || IsBeast(it) || IsGoods(it);
        }

        /// <summary>Poza systemem zuzycia: amunicja, zywy inwentarz i wszelki towar. E8/C (R3): przy ThrownAsMissile takze bron miotana - nie obija sie w walce
        /// i nie dostaje stanu lupu u gracza ani u AI (zuzywa ja rzut - AmmoRecovery); wylaczone = bron miotana obija sie jak bron, jak przed E8/C.</summary>
        internal static bool NoWear(ItemObject it)
        {
            if (it != null && it.ItemType == ItemObject.ItemTypeEnum.Thrown)
            {
                var s = Settings.Current;
                if (s != null && s.ThrownAsMissile) return true;
            }
            return IsAmmo(it) || IsBeast(it) || IsGoods(it);
        }

        /// <summary>
        /// Sprzatanie po starych zasadach: amunicja z "uszkodzonym" stanem
        /// (z lupow, ze starych bitew) wraca do stanu fabrycznego - w sakwach
        /// i w kolczanach na grzbiecie.
        /// POPRAWKA 159 (recenzja ceny sprzedazy 07.10): konie i zwierzeta (IsBeast) NIE - kon nie ma komponentu broni ani zbroi, wiec
        /// IsGoods bral go za towar i kulawy kon gracza (lame_horse) wracal zdrowy przy kazdym wczytaniu i po kazdej bitwie: kulawy kon
        /// kupiony tanio i sprzedany zdrowy dawal zarobek z niczego (Handel 300). Kulawy kon to kulawy kon (opis IsBeast) - stan zostaje.
        /// </summary>
        internal void CleanseAmmo()
        {
            try
            {
                int fixedUp = 0;
                var roster = MobileParty.MainParty.ItemRoster;
                for (int i = roster.Count - 1; i >= 0; i--)
                {
                    var el = roster[i].EquipmentElement;
                    // amunicja ORAZ towar (jedzenie, surowce) - stan im nie przystoi;
                    // inne mody potrafia nalozyc "uszkodzenie" na rybe i maslo,
                    // scinajac przy okazji ich cene do grosza
                    if (!IsAmmo(el.Item) && !IsGoods(el.Item)) continue;
                    if (IsBeast(el.Item)) continue;                            // poprawka 159: kon i zwierze zachowuja swoj stan (kulawy zostaje kulawy)
                    if (el.ItemModifier == null) continue;
                    if (el.ItemModifier.PriceMultiplier >= 1f) continue;      // dodatnie stany zostaja
                    int n = roster[i].Amount;
                    roster.AddToCounts(el, -n);
                    roster.AddToCounts(new EquipmentElement(el.Item), n);
                    fixedUp += n;
                }
                var eq = Hero.MainHero.BattleEquipment;
                for (int slot = 0; slot < 4; slot++)
                {
                    var el = eq[(EquipmentIndex)slot];
                    if (!IsAmmo(el.Item) || el.ItemModifier == null) continue;
                    if (el.ItemModifier.PriceMultiplier >= 1f) continue;
                    eq[(EquipmentIndex)slot] = new EquipmentElement(el.Item);
                    fixedUp++;
                }
                if (fixedUp > 0) Log.Info("Amunicja oczyszczona ze stanow: " + fixedUp + " szt. (konie i zwierzeta zachowuja stan - poprawka 159)");
            }
            catch (Exception e) { Log.Error("CleanseAmmo", e); }
        }

        /// <summary>
        /// Pula wytrzymalosci sztuki pancerza wedle wzoru Jeffa:
        /// (suma punktow ochrony) x DurabilityPerArmorPoint x tier.
        /// 61 pancerza przy tierze 3 = 61 x 20 x 3 = 3660 punktow.
        /// </summary>
        internal static int ArmorPool(ItemObject it)
        {
            try
            {
                if (it == null || !it.HasArmorComponent) return 0;
                var a = it.ArmorComponent;
                int pts = it.Type == ItemObject.ItemTypeEnum.HorseHarness
                    ? a.BodyArmor
                    : a.HeadArmor + a.BodyArmor + a.LegArmor + a.ArmArmor;
                int tier = Recipes.Grade(it);
                // wpis 95 (audyt zuzycia pkt 7): rekawice (10 pkt) i nogawice zuzywaly sie 5-8x szybciej niz napiersnik - podloga puli
                pts = Math.Max(pts, Math.Max(1, Settings.Current.ArmorPoolMinPoints));
                int pool = (int)(pts * Math.Max(1f, Settings.Current.DurabilityPerArmorPoint) * tier);
                return Math.Max(1, pool);
            }
            catch { return 0; }
        }

        /// <summary>m110-stan krok 2: ksiega sztuki po zuzyciu z ciosow (wydzielone z ApplyWearPerSlot - to samo wyrazenie, wynik bit w bit jak dotad; towarzysze -
        /// HeroBook.ApplyBattle). Pancerz: ksiega niesie SUROWE obrazenia, pula = pancerz x DurabilityPerArmorPoint x tier, kazdy punkt obrazen zdejmuje jeden punkt
        /// puli; reszta (bron, tarcza, luk): punkty przez solidnosc tieru.</summary>
        internal static float WearAfter(ItemObject it, float cond, float amount)
        {
            if (it.HasArmorComponent)
            {
                int pool = ArmorPool(it);
                if (pool > 0) cond = MathF.Max(0f, cond - amount * 100f / pool);
                return cond;
            }
            float sturdiness = Sturdiness(it);
            return MathF.Max(0f, cond - amount / sturdiness);
        }

        /// <summary>m110-stan krok 2: solidnosc tieru (1 + (tier - 1) x TierDurabilityFactor) - jak w ApplyWearPerSlot.</summary>
        internal static float Sturdiness(ItemObject it)
        {
            int tier = Recipes.Grade(it);
            return 1f + MathF.Max(0, tier - 1) * Settings.Current.TierDurabilityFactor;
        }

        /// <summary>Zuzycie per slot z ksiegi bitwy + ewentualna baza rozlozona po staremu.</summary>
        private void ApplyWearPerSlot(float[] perSlot, float flat)
        {
            try
            {
                var eq = Hero.MainHero.BattleEquipment;
                for (int slot = 0; slot < 12; slot++)
                {
                    float amount = (perSlot != null ? perSlot[slot] : 0f) + flat;
                    if (amount <= 0.01f) continue;
                    var el = eq[slot];
                    if (el.Item == null || NoWear(el.Item)) continue;
                    if (el.Item.ItemComponent == null || el.Item.ItemComponent.ItemModifierGroup == null) continue;
                    float cond = WearAfter(el.Item, GetCondition(slot, el), amount);   // m110-stan krok 2: arytmetyka wydzielona (ta sama dla towarzyszy - HeroBook)
                    SetCondition(slot, cond);
                    if (cond <= 0f && Settings.Current.BreakAtZeroCondition)
                    {
                        Log.Player(el.Item.Name + " gave out in the fight and is beyond saving.", true);
                        eq[slot] = new EquipmentElement(null);
                        SetCondition(slot, 100f);
                        continue;
                    }
                    ApplyModifierForCondition(slot, cond);
                }
            }
            catch (Exception e) { Log.Error("ApplyWearPerSlot", e); }
        }

        private void ApplyWear(float amount)
        {
            try
            {
                var eq = Hero.MainHero.BattleEquipment;
                for (int slot = 0; slot < 12; slot++)
                {
                    var el = eq[slot];
                    if (el.Item == null || NoWear(el.Item)) continue;
                    if (el.Item.ItemComponent == null || el.Item.ItemComponent.ItemModifierGroup == null) continue;

                    float cond = GetCondition(slot, el);
                    int tier = Recipes.Grade(el.Item);
                    float sturdiness = 1f + MathF.Max(0, tier - 1) * Settings.Current.TierDurabilityFactor;
                    float roll = amount * (0.6f + MBRandom.RandomFloat * 0.8f) / sturdiness;
                    cond = MathF.Max(0f, cond - roll);
                    SetCondition(slot, cond);
                    if (cond <= 0f && Settings.Current.BreakAtZeroCondition)
                    {
                        Log.Player(el.Item.Name + " gave out in the fight and is beyond saving.", true);
                        Log.Info("Przedmiot pekl: " + el.Item.StringId);
                        eq[slot] = new EquipmentElement(null);
                        SetCondition(slot, 100f);
                        continue;
                    }
                    ApplyModifierForCondition(slot, cond);
                }
            }
            catch (Exception e) { Log.Error("ApplyWear", e); }
        }

        /// <summary>
        /// Stan CZESCI, nie przegrodki. Ksiega trzyma teraz takze ID przedmiotu:
        /// bez tego swiezo zalozony kirys dziedziczyl zuzycie po poprzednim
        /// (slot 6 pamietal 30%, a nowka zaraz dostawala dopisek "Battered").
        /// Inna sztuka w slocie = nowy rachunek od 100%.
        /// </summary>
        private float GetCondition(int slot, EquipmentElement el)
        {
            string id = el.Item != null ? el.Item.StringId : "";
            for (int i = 0; i < _condition.Count; i++)
            {
                var p = _condition[i].Split('|');
                if (int.Parse(p[0]) != slot) continue;
                string had = p.Length > 3 ? p[3] : "";
                if (had.Length == 0)
                {
                    // stary zapis (bez ID) - przypisujemy go temu, co teraz lezy w slocie
                    _condition[i] = p[0] + "|" + p[1] + "|" + (p.Length > 2 ? p[2] : "") + "|" + id;
                    return float.Parse(p[1], System.Globalization.CultureInfo.InvariantCulture);
                }
                if (had == id)
                {
                    // ZBROJA Z KUZNI JAK BRON: ta sama nazwa, inny stan niz zostawilo zuzycie = INNA sztuka (dwie kopie tej samej
                    // zbroi z kuzni - legendarna i zardzewiala); bez tego naprawa dawala zamienionej sztuce stan poprzedniczki
                    if (ArmourQuality.On && !SamePiece(p, el)) { _condition[i] = BookLine(slot, el, id); return 100f; }
                    if (ArmourQuality.On && p.Length == 4) _condition[i] = _condition[i] + "|" + ModId(el);   // stary wpis: od teraz zna stan sztuki
                    return float.Parse(p[1], System.Globalization.CultureInfo.InvariantCulture);
                }
                // w slocie lezy INNA sztuka - stary rachunek jej nie dotyczy
                _condition[i] = ArmourQuality.On ? BookLine(slot, el, id) : slot + "|100|" + (el.ItemModifier != null ? el.ItemModifier.StringId : "") + "|" + id;
                return 100f;
            }
            // pierwszy raz - zapamietaj oryginalny modyfikator i sztuke
            _condition.Add(ArmourQuality.On ? BookLine(slot, el, id) : slot + "|100|" + (el.ItemModifier != null ? el.ItemModifier.StringId : "") + "|" + id);
            return 100f;
        }

        private static string ModId(EquipmentElement el) { return el.ItemModifier != null ? el.ItemModifier.StringId : ""; }

        /// <summary>
        /// ZBROJA Z KUZNI JAK BRON: czy w slocie lezy TA SAMA sztuka, ktora zna wpis ksiegi (ta sama nazwa nie wystarcza).
        /// Wpis z piatym polem - po stanie, w jakim ja zostawilismy. Stary wpis (4 pola, zapis sprzed paczki) stanu nie zna:
        /// ta sama sztuka ma swoj stan oryginalny albo dokladnie ten stan zuzycia, ktory ksiega daje przy tym stanie - kazdy
        /// inny stan to inna kopia tej zbroi. Bez tego swiezo wykuta lordly albo legendarna zalozona w miejsce zuzytej zwyklej
        /// przejmowala stary wpis (zuzycie i "oryginalny" stan zwyklej) i u kowala stawala sie zwykla, a zardzewiala kopia
        /// zalozona w miejsce lekko zuzytej legendy dostawala u kowala legende (podwojenie, jak w 127).
        /// </summary>
        private static bool SamePiece(string[] p, EquipmentElement el)
        {
            string now = ModId(el);
            if (p.Length > 4) return p[4] == now;
            string orig = p.Length > 2 ? p[2] : "";
            if (now == orig) return true;
            float cond;
            if (p.Length < 2 || !float.TryParse(p[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out cond))
                return true;   // nieczytelny wpis - jak dotad (po samej nazwie)
            var group = el.Item != null && el.Item.ItemComponent != null ? el.Item.ItemComponent.ItemModifierGroup : null;
            var worn = WornModifierFor(group, cond, orig);
            // m110-stan (GearFourStates): ta sama sztuka = ten sam STAN (stary modyfikator zuzycia sprzed paczki i kanoniczny arm_* tego stanu to jedno)
            if (worn != null && GearState.HandlesGroup(group)) return GearState.Of(worn) == GearState.Of(el.ItemModifier);
            return worn != null && worn == el.ItemModifier;
        }

        /// <summary>Nowy wpis ksiegi: slot|stan|stan oryginalny|sztuka|stan, ktory sztuka ma teraz (piate pole - zbroja z kuzni jak bron).</summary>
        private static string BookLine(int slot, EquipmentElement el, string id) { return slot + "|100|" + ModId(el) + "|" + id + "|" + ModId(el); }

        /// <summary>Piate pole wpisu: stan, w jakim zostawilismy sztuke w slocie (po zuzyciu albo naprawie).</summary>
        private void SetLast(int slot, string modId)
        {
            for (int i = 0; i < _condition.Count; i++)
            {
                var p = _condition[i].Split('|');
                if (int.Parse(p[0]) != slot) continue;
                _condition[i] = p[0] + "|" + (p.Length > 1 ? p[1] : "100") + "|" + (p.Length > 2 ? p[2] : "") + "|" + (p.Length > 3 ? p[3] : "") + "|" + (modId ?? "");
                return;
            }
        }

        /// <summary>
        /// Po naprawie zalozonej czesci stan wraca do 100 - inaczej Wear odlozylby modyfikator z powrotem - a ksiega zapamietuje
        /// NOWY stan sztuki jako oryginal. Przeglad 07.10: dotad zostawal modyfikator sprzed naprawy (np. Plundered), wiec
        /// pozniejsza naprawa calej uprzezy brala zaplate drugi raz i przywracala sztuce stan sprzed oplaconej naprawy.
        /// ZBROJA Z KUZNI JAK BRON (132): przy wlaczonej jakosci zbroi wpis z piatym polem (BookLine) - sztuka zaczyna ksiege od
        /// nowa w stanie, jaki ma teraz; przy wylaczonej - wpis 4 pola (ta sama regula, scalenie TOWARY 3).
        /// </summary>
        internal void ResetSlotCondition(int slot)
        {
            try
            {
                var el = Hero.MainHero.BattleEquipment[slot];
                string id = el.Item != null ? el.Item.StringId : "";
                string line = ArmourQuality.On ? BookLine(slot, el, id) : slot + "|100|" + ModId(el) + "|" + id;
                for (int i = 0; i < _condition.Count; i++)
                {
                    var p = _condition[i].Split('|');
                    if (int.Parse(p[0]) != slot) continue;
                    _condition[i] = line;
                    return;
                }
                _condition.Add(line);
            }
            catch (Exception e) { Log.Error("ResetSlotCondition", e); }
        }

        /// <summary>
        /// ZBROJA Z KUZNI JAK BRON: w jakim stanie lawka naprawcza oddaje zalozona sztuke. Zuzyta w boju sztuka, ktora wyszla
        /// z kuzni (albo z lupu) dobra, lordly albo legendarna, wraca do swojego stanu; reszta - zwykla (jak dotad). Dotad lawka
        /// zawsze dawala zwykla: zuzyta legenda po naprawie na lawce przestawala byc legenda.
        /// </summary>
        internal ItemModifier GoodOriginal(int slot)
        {
            try
            {
                if (!ArmourQuality.On || GetConditionQuiet(slot) >= 100f) return null;   // ksiega nie zna tej sztuki albo nie byla bita
                var origId = OriginalModifier(slot);
                var orig = string.IsNullOrEmpty(origId) ? null : MBObjectManager.Instance.GetObject<ItemModifier>(origId);
                return orig != null && orig.PriceMultiplier >= 1f ? orig : null;
            }
            catch (Exception e) { Log.Error("GoodOriginal", e); return null; }
        }

        private void SetCondition(int slot, float cond)
        {
            for (int i = 0; i < _condition.Count; i++)
            {
                var p = _condition[i].Split('|');
                if (int.Parse(p[0]) != slot) continue;
                _condition[i] = slot + "|" + cond.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)
                                + "|" + (p.Length > 2 ? p[2] : "") + "|" + (p.Length > 3 ? p[3] : "")
                                // zbroja z kuzni jak bron: stan sztuki zostaje w ksiedze; przy wylaczonym znika (wpis jak w 127 -
                                // po ponownym wlaczeniu stary wpis dostaje stan sztuki od nowa, zamiast uznac ja za obca)
                                + (ArmourQuality.On && p.Length > 4 ? "|" + p[4] : "");
                return;
            }
        }

        private string OriginalModifier(int slot)
        {
            foreach (var c in _condition)
            {
                var p = c.Split('|');
                if (int.Parse(p[0]) == slot) return p.Length > 2 ? p[2] : "";
            }
            return "";
        }

        /// <summary>
        /// Stan zuzycia, ktory ksiega daje sztuce przy tym stanie: modyfikator z wlasnej grupy przedmiotu - im gorszy stan, tym
        /// gorszy modyfikator; null = stan powyzej progu ThresholdWorn albo grupa bez stanow zuzycia. Jedna regula dla zuzycia
        /// (ApplyModifierForCondition) i dla rozpoznania sztuki ze starego wpisu ksiegi (SamePiece).
        /// </summary>
        private static ItemModifier WornModifierFor(ItemModifierGroup group, float cond)
        {
            return WornModifierFor(group, cond, "");
        }

        /// <summary>m110-stan (GearFourStates): ksiega trzyma dokladna pule od 100 (wzgledem stanu, w jakim sztuka weszla do ksiegi - origId); stan liczony
        /// z wytrzymalosci d = ksiega - (100 - srodek przedzialu stanu oryginalu), progi 80/40/10 (GearState.OfCondition). Wylaczone - progi Threshold* jak dotad.</summary>
        private static ItemModifier WornModifierFor(ItemModifierGroup group, float cond, string origId)
        {
            if (group == null) return null;
            if (GearState.HandlesGroup(group))
            {
                int st = GearState.OfCondition(EffectiveCondition(cond, origId));
                return st == GearState.Sound ? null : GearState.ModForGroup(group, st);
            }
            var s = Settings.Current;
            int step;
            if (cond > s.ThresholdWorn) step = 0;
            else if (cond > s.ThresholdDamaged) step = 1;
            else if (cond > s.ThresholdRuined) step = 2;
            else step = 3;
            if (step == 0) return null;

            // posortuj modyfikatory od najgorszego (najnizszy mnoznik ceny)
            var bad = new List<ItemModifier>();
            foreach (var m in group.ItemModifiers)
                if (m != null && m.PriceMultiplier < 1f) bad.Add(m);
            if (bad.Count == 0) return null;
            bad.Sort((a, b) => a.PriceMultiplier.CompareTo(b.PriceMultiplier));   // najgorszy pierwszy

            return step >= 3 ? bad[0] : bad[MathF.Min(bad.Count - 1, bad.Count - step)];
        }

        /// <summary>m110-stan: wytrzymalosc d sztuki z ksiegi - ksiega liczy od 100 wzgledem stanu, w jakim sztuka do niej weszla (origId: sprawna 100, uszkodzona
        /// 60, zmasakrowana 25, wrak 5 - srodek przedzialu), bez przeliczania starych wpisow (stan nigdy nie poprawia sie sam).</summary>
        internal static float EffectiveCondition(float cond, string origId)
        {
            int so = GearState.OfId(origId);
            if (so < 0) so = GearState.Sound;
            return cond - (100f - GearState.Mid[so]);
        }

        /// <summary>m110-stan (recenzja R6): po wczytaniu (klucz GearFourStates) nazwa i cena sztuk na grzbiecie wedlug ksiegi zuzycia. Zapis 104 trzymal
        /// sztuki z ksiega 70-80% bez modyfikatora (stary prog ThresholdWorn 70), a podpowiedz "Armoury Stats" liczy stan z ksiegi (progi 80/40/10) - do
        /// nastepnej bitwy nazwa byla czysta i cena pelna przy "Condition: Damaged". Tylko wpis tej samej sztuki (BookOf - jak GetConditionQuiet); stan
        /// tylko sie pogarsza (ApplyModifierForCondition), bez komunikatu dla gracza - linie "Zuzycie (m110)" i jedna linia sesji w logu.</summary>
        private void ResyncWornStates()
        {
            if (!GearState.On || _condition == null || Hero.MainHero == null) return;
            var eq = Hero.MainHero.BattleEquipment;
            int seen = 0, changed = 0, bad = 0;
            for (int slot = 0; slot < 12; slot++)
            {
                try
                {
                    var el = eq[slot];
                    if (el.Item == null || NoWear(el.Item) || !GearState.Handles(el.Item)) continue;
                    float cond;
                    if (!BookOf(slot, el, out cond)) continue;
                    seen++;
                    ApplyModifierForCondition(slot, cond, true);
                    if (eq[slot].ItemModifier != el.ItemModifier) changed++;
                }
                catch (Exception e) { if (++bad <= 3) Log.Error("ResyncWornStates(" + slot + ")", e); }
            }
            Log.Info("Stany sprzetu (m110): wyrownanie po wczytaniu | sztuk z ksiega " + seen + ", nowy stan " + changed + ", bledy " + bad + ".");
        }

        /// <summary>m110-stan (recenzja R6): wpis ksiegi TEJ sztuki w slocie (id i - przy jakosci zbroi z kuzni - stan jak SamePiece); stary wpis bez id albo
        /// inna czesc - false (bez wyrownania). Tylko odczyt.</summary>
        private bool BookOf(int slot, EquipmentElement cur, out float cond)
        {
            cond = 100f;
            string id = cur.Item != null ? cur.Item.StringId : "";
            foreach (var c in _condition)
            {
                if (string.IsNullOrEmpty(c)) continue;
                var p = c.Split('|');
                int sl;
                if (p.Length < 2 || !int.TryParse(p[0], out sl) || sl != slot) continue;
                string had = p.Length > 3 ? p[3] : "";
                if (had.Length == 0 || id.Length == 0 || had != id) return false;
                if (ArmourQuality.On && !SamePiece(p, cur)) return false;
                return float.TryParse(p[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out cond);
            }
            return false;
        }

        /// <summary>Dobiera modyfikator z wlasnej grupy przedmiotu - im gorszy stan, tym gorszy modyfikator. quiet - bez komunikatu dla gracza (wyrownanie po
        /// wczytaniu, m110-stan R6).</summary>
        private void ApplyModifierForCondition(int slot, float cond, bool quiet = false)
        {
            try
            {
                var eq = Hero.MainHero.BattleEquipment;
                var el = eq[slot];
                if (el.Item == null) return;
                var group = el.Item.ItemComponent != null ? el.Item.ItemComponent.ItemModifierGroup : null;
                if (group == null) return;

                if (GearState.HandlesGroup(group))
                {
                    // m110-stan (GearFourStates): stan z wytrzymalosci (progi 80/40/10); zuzycie nigdy nie poprawia sztuki - liczone po stanie i zawsze
                    // (dotad tylko przy jakosci zbroi z kuzni); dobra jakosc (fine, lordly, legendary) zostaje tylko na sprawnej
                    int so = GearState.OfId(OriginalModifier(slot));
                    if (so < 0) so = GearState.Sound;
                    float d = EffectiveCondition(cond, OriginalModifier(slot));
                    int st = GearState.OfCondition(d);
                    int was = GearState.Of(el.ItemModifier);
                    if (was < 0) was = GearState.Sound;
                    if (st <= was) return;
                    var gsMod = GearState.ModForGroup(group, st);
                    if (gsMod == null || el.ItemModifier == gsMod) return;
                    eq[slot] = new EquipmentElement(el.Item, gsMod);
                    if (ArmourQuality.On) SetLast(slot, gsMod.StringId);
                    Log.Info("Zuzycie (m110): slot " + slot + " " + el.Item.StringId + " -> " + gsMod.StringId + " | ksiega "
                             + cond.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "%, poczatek " + GearState.Pl[so] + ", liczony "
                             + d.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "% -> " + GearState.Pl[st] + " (byl " + GearState.Pl[was] + ").");
                    if (!quiet) Log.Player(el.Item.Name + " is now " + GearState.En[st] + ".", true);
                    return;
                }

                ItemModifier chosen = WornModifierFor(group, cond);
                if (chosen == null) return;
                if (el.ItemModifier == chosen) return;
                // ZBROJA Z KUZNI JAK BRON: zuzycie nigdy nie poprawia sztuki - zardzewiala zbroja z kuzni (stan 30%) przy 70%
                // ksiegi nie staje sie "tylko pogieta" (60%, lepsza ochrona); zostaje gorszy z dwoch stanow
                if (ArmourQuality.On && el.ItemModifier != null
                    && ConditionScaling.ConditionOf(el.ItemModifier) < 0.999f
                    && ConditionScaling.ConditionOf(el.ItemModifier) <= ConditionScaling.ConditionOf(chosen)) return;

                eq[slot] = new EquipmentElement(el.Item, chosen);
                if (ArmourQuality.On) SetLast(slot, chosen.StringId);
                Log.Info("Zuzycie: slot " + slot + " " + el.Item.StringId + " -> " + chosen.StringId + " (stan " + (int)cond + ")");
                Log.Player(el.Item.Name + " is showing hard use (" + chosen.Name + ", condition " + (int)cond + "%).", true);
            }
            catch (Exception e) { Log.Error("ApplyModifierForCondition", e); }
        }

        // ---------------------------------------------------------- naprawa
        /// <summary>Robota kowala przy jednej czesci uprzezy: dawniej wartosc x brak x RepairCostFactor; przy regule kowali miasta
        /// (MendMaterial.RuleOn) - ulamek wykonania (MendMaterialMaxShare x brak) po dniowce mistrza w tym miescie, jak lup i sztuka na lawie.</summary>
        private static int HarnessLabor(ItemObject it, float cond)
        {
            float missing = Math.Max(0f, (100f - cond) / 100f);
            // poprawka po audycie TOWARY 3: stara kwota starym wyrazeniem (n120) - inne wyrazenie float dawalo czasem 1 d roznicy przy wylaczonej regule
            int old = (int)(it.Value * (1f - cond / 100f) * Settings.Current.RepairCostFactor);
            if (!MendMaterial.RuleOn) return old;
            var s = Settings.Current;
            return MendMaterial.Labor(it, Math.Max(0f, s.MendMaterialMaxShare) * Math.Min(1f, missing), Settlement.CurrentSettlement, old);
        }

        internal int RepairCost()
        {
            int cost = 0;
            try
            {
                var eq = Hero.MainHero.BattleEquipment;
                int bulkN = GuildMend.HarnessBulkN(this);   // R3-02: rabat hurtowy zlecenia (0 = jak dotad)
                for (int slot = 0; slot < 12; slot++)
                {
                    var el = eq[slot];
                    if (el.Item == null) continue;
                    float cond = GetConditionQuiet(slot);
                    if (cond >= 100f) continue;
                    if (LootPrices.HarnessWreck(el.ItemModifier, cond)) continue;   // paczka 158: wrak na zlom - kowale miasta go nie odnawiaja
                    cost += GuildMend.Bulk(HarnessLabor(el.Item, cond), bulkN);
                }
            }
            catch (Exception e) { Log.Error("RepairCost", e); }
            return cost;
        }

        /// <summary>Paczka 158: ile czesci na grzbiecie to wraki (modyfikator wraku albo stan <= 10%) - kowale miasta ich nie odnawiaja.</summary>
        internal int HarnessWrecks()
        {
            int n = 0;
            try
            {
                if (!LootPrices.ScrapRule) return 0;
                var eq = Hero.MainHero.BattleEquipment;
                for (int slot = 0; slot < 12; slot++)
                {
                    if (eq[slot].Item == null) continue;
                    float cond = GetConditionQuiet(slot);
                    if (cond < 100f && LootPrices.HarnessWreck(eq[slot].ItemModifier, cond)) n++;
                }
            }
            catch (Exception e) { Log.Error("HarnessWrecks", e); }
            return n;
        }

        /// <summary>Poprawka po recenzji 158: czesc zalozona w tym slocie to wrak (modyfikator wraku albo stan w ksiedze <= 10%) - ta sama regula co
        /// HarnessWrecks / RepairCost, dla "Pick a piece" (SmithMenu.SmithRefusesHere). Wylaczone WrecksToScrap - false.</summary>
        internal bool SlotWreck(int slot)
        {
            try
            {
                if (!LootPrices.ScrapRule || slot < 0 || slot >= 12) return false;
                var el = Hero.MainHero.BattleEquipment[slot];
                if (el.Item == null) return false;
                return LootPrices.HarnessWreck(el.ItemModifier, GetConditionQuiet(slot));
            }
            catch (Exception e) { Log.Error("SlotWreck", e); return false; }
        }

        /// <summary>
        /// E8/B (258, LAWA 3): cena kowala za JEDNA zalozona sztuke wedlug ksiegi zuzycia - ta sama robocizna (HarnessLabor) i ten sam material
        /// (MendMaterial.NeedsFor), co "Mend everything you wear" (PlanRepair / RepairCost); dla SmithMenu "Pick a piece" (jedna cena tej samej
        /// naprawy). false = wycena wedlug modyfikatora jak dotad: ksiega nie zna zuzycia tej sztuki (stan 100 - np. obity lup zalozony przed
        /// chwila) albo sztuka weszla na grzbiet juz zuzyta (oryginalny stan ponizej pelnego - lawka "Pick a piece" oddaje ja czysta, a "Mend
        /// everything you wear" w stanie lupu: to dwie rozne uslugi, kazda ze swoja cena). Sam odczyt.
        /// </summary>
        internal bool SlotSmithQuote(int slot, out int labor, out float[] need)
        {
            labor = 0; need = null;
            try
            {
                if (slot < 0 || slot >= 12) return false;
                var el = Hero.MainHero.BattleEquipment[slot];
                if (el.Item == null) return false;
                float cond = GetConditionQuiet(slot);
                if (cond >= 100f) return false;
                var origId = OriginalModifier(slot);
                ItemModifier orig = string.IsNullOrEmpty(origId) ? null : MBObjectManager.Instance.GetObject<ItemModifier>(origId);
                if (orig != null && orig.PriceMultiplier < 0.999f) return false;
                labor = HarnessLabor(el.Item, cond);
                need = MendMaterial.NeedsFor(el.Item, (100f - cond) / 100f);
                return true;
            }
            catch (Exception e) { Log.Error("SlotSmithQuote", e); labor = 0; need = null; return false; }
        }

        /// <summary>Ile czesci na grzbiecie wymaga naprawy.</summary>
        internal int WornPieces()
        {
            int n = 0;
            try
            {
                var eq = Hero.MainHero.BattleEquipment;
                for (int slot = 0; slot < 12; slot++)
                    if (eq[slot].Item != null && GetConditionQuiet(slot) < 100f) n++;
            }
            catch (Exception e) { Log.Error("WornPieces", e); }
            return n;
        }

        /// <summary>Podglad stanu bez zapisu - takze pilnuje, ze to TA SAMA sztuka.</summary>
        /// <summary>
        /// STAN TEJ SZTUKI, ktora masz na sobie - prosto z ksiegi, nie z etykiety.
        /// Panel przedmiotu czytal dotad procent z MODYFIKATORA, a modyfikator
        /// dokladamy dopiero po przekroczeniu progu. Kirys zbity ze 100% na 82%
        /// wygladal wiec jak nowka ("ciagle mam 100%" - Jeff). Zwraca -1, gdy tej
        /// sztuki nie ma na grzbiecie i ksiega o niej nic nie wie.
        /// </summary>
        internal float WornCondition(ItemObject item)
        {
            try
            {
                if (item == null) return -1f;
                var eq = Hero.MainHero.BattleEquipment;
                for (int slot = 0; slot < 12; slot++)
                {
                    if (eq[slot].Item != item) continue;
                    return GetConditionQuiet(slot);
                }
            }
            catch { }
            return -1f;
        }

        internal float ConditionQuietOf(int slot) { return GetConditionQuiet(slot); }   // R3-02 (GuildMend): stan czesci uprzezy - godziny cechow i rabat zlecenia

        /// <summary>m110-stan krok 2 (WornExactHpProtection): dokladna wytrzymalosc d (procent puli) sztuki gracza w gniezdzie - ksiega tej sztuki bita (ponizej
        /// 100) wzgledem stanu, w jakim weszla do ksiegi (EffectiveCondition); false - ksiega jej nie zna albo swiezy wpis (ochrona wedlug stanu). Tylko odczyt.</summary>
        internal bool WornExactD(int slot, out float d)
        {
            d = 100f;
            float cond = GetConditionQuiet(slot);
            if (cond >= 100f) return false;
            d = EffectiveCondition(cond, OriginalModifier(slot));
            return true;
        }

        /// <summary>m110-stan: wytrzymalosc d (procent puli) tej sztuki na grzbiecie z ksiegi zuzycia (ArmouryStats); -1 - sztuki nie ma na grzbiecie albo ksiega
        /// jej nie bila (stan tylko z modyfikatora - wytrzymalosc nieznana dokladnie).</summary>
        internal float WornEffective(ItemObject item)
        {
            try
            {
                if (item == null) return -1f;
                var eq = Hero.MainHero.BattleEquipment;
                for (int slot = 0; slot < 12; slot++)
                {
                    if (eq[slot].Item != item) continue;
                    float cond = GetConditionQuiet(slot);
                    if (cond >= 100f) return -1f;
                    return Math.Max(0f, EffectiveCondition(cond, OriginalModifier(slot)));
                }
            }
            catch { }
            return -1f;
        }

        /// <summary>m110-stan: ksiega zuzycia do linii "Stany sprzetu (m110)" - wpisy, wpisy ze stanem oryginalu gorszym niz sprawny (ksiega przesunieta o srodek
        /// przedzialu), wpisy z modyfikatorem arm_* (oryginal albo ostatni stan).</summary>
        internal void BookStats(out int entries, out int shifted, out int armEntries)
        {
            entries = 0; shifted = 0; armEntries = 0;
            try
            {
                if (_condition == null) return;
                foreach (var c in _condition)
                {
                    if (string.IsNullOrEmpty(c)) continue;
                    entries++;
                    var p = c.Split('|');
                    string orig = p.Length > 2 ? p[2] : "";
                    if (GearState.OfId(orig) > GearState.Sound) shifted++;
                    if (orig.StartsWith("arm_") || (p.Length > 4 && p[4].StartsWith("arm_"))) armEntries++;
                }
            }
            catch { }
        }

        private float GetConditionQuiet(int slot)
        {
            string id = "";
            EquipmentElement cur = default(EquipmentElement);
            try
            {
                cur = Hero.MainHero.BattleEquipment[slot];
                var it = cur.Item;
                id = it != null ? it.StringId : "";
            }
            catch { }
            foreach (var c in _condition)
            {
                var p = c.Split('|');
                if (int.Parse(p[0]) != slot) continue;
                string had = p.Length > 3 ? p[3] : "";
                if (had.Length > 0 && id.Length > 0 && had != id) return 100f;   // inna czesc w slocie
                // zbroja z kuzni jak bron: ta sama nazwa w innym stanie, niz ja zostawilismy - inna sztuka (np. druga kopia z kuzni);
                // stary wpis bez stanu sztuki (zapis sprzed paczki) - SamePiece rozpoznaje ja wedle stanu oryginalnego i zuzycia
                if (ArmourQuality.On && id.Length > 0 && !SamePiece(p, cur)) return 100f;
                return float.Parse(p[1], System.Globalization.CultureInfo.InvariantCulture);
            }
            return 100f;
        }

        /// <summary>Naprawa przy wlasnym kowadle - material i wytrzymalosc zamiast zlota.</summary>
        internal void RepairAllSelf()
        {
            try
            {
                var eq = Hero.MainHero.BattleEquipment;
                int mended = 0;
                var blocked = new List<string>();
                for (int slot = 0; slot < 12; slot++)
                {
                    var el = eq[slot];
                    if (el.Item == null) continue;
                    float cond = GetConditionQuiet(slot);
                    if (cond >= 100f) continue;
                    float missing = (100f - cond) / 100f;
                    string why;
                    if (!Forge.SelfRepair(el.Item, missing, out why))
                    {
                        if (why != null && blocked.Count < 4) blocked.Add(el.Item.Name + ": needs " + why);
                        continue;
                    }

                    var origId = OriginalModifier(slot);
                    ItemModifier orig = string.IsNullOrEmpty(origId) ? null : MBObjectManager.Instance.GetObject<ItemModifier>(origId);
                    eq[slot] = new EquipmentElement(el.Item, orig);
                    SetCondition(slot, 100f);
                    if (ArmourQuality.On) SetLast(slot, origId);   // zbroja z kuzni jak bron: sztuka wrocila do swojego stanu
                    mended++;
                }
                if (mended > 0) Log.Player("You worked " + mended + " pieces back into shape yourself.");
                if (blocked.Count > 0)
                    foreach (var b in blocked) Log.Player(b, true);
                else if (mended == 0)
                    Log.Player("Your harness is sound - nothing on you needs mending.");
                Log.Info("Naprawa wlasnoreczna: " + mended + " szt., zablokowane: " + blocked.Count + MendMaterial.FracNote());
            }
            catch (Exception e) { Log.Error("RepairAllSelf", e); }
        }

        /// <summary>
        /// Plan naprawy uprzezy u kowali miasta (lawa naprawcza, SmithMendFromMarket): kazda czesc ze stanem ponizej 100 - robocizna
        /// jak dotad (wartosc x brak x RepairCostFactor), material wedle braku (MendMaterial.NeedsFor - ta sama regula co lup), brak
        /// materialu = ta czesc czeka. Wrakow tu nie ma: naprawa przywraca oryginalny modyfikator (stan lupu zostaje). slots - przegrodki,
        /// ktore kowale zrobia. Na probie - polke i zapas zmienia dopiero RepairAll (Commit).
        /// </summary>
        internal MendMaterial.Order PlanRepair(List<int> slots)
        {
            var o = new MendMaterial.Order(Settlement.CurrentSettlement);
            try
            {
                var eq = Hero.MainHero.BattleEquipment;
                int bulkN = GuildMend.HarnessBulkN(this);   // R3-02: rabat hurtowy zlecenia (0 = jak dotad)
                for (int slot = 0; slot < 12; slot++)
                {
                    var el = eq[slot];
                    if (el.Item == null) continue;
                    float cond = GetConditionQuiet(slot);
                    if (cond >= 100f) continue;
                    if (LootPrices.HarnessWreck(el.ItemModifier, cond)) { o.Wrecks++; continue; }   // paczka 158: wrak na zlom (AddLot widzi sztuke bez modyfikatora)
                    float missing = (100f - cond) / 100f;
                    int labor = GuildMend.Bulk(HarnessLabor(el.Item, cond), bulkN);   // ta sama robocizna co RepairCost (R3-02: po rabacie hurtowym)
                    if (o.AddLot(new EquipmentElement(el.Item), MendMaterial.NeedsFor(el.Item, missing), labor, 1, long.MaxValue, int.MaxValue) > 0 && slots != null)
                        slots.Add(slot);
                }
            }
            catch (Exception e) { Log.Error("PlanRepair", e); }
            return o;
        }

        /// <summary>Uprzaz u kowali miasta z materialem z targu (SmithMendFromMarket): czy zlecenie sie odbedzie - inaczej komunikat, nic nie
        /// zaplacone i nic nie zdjete z polki. Kowale robia tylko czesci, na ktore jest material (PlanRepair); reszta czeka.</summary>
        private static bool MarketRepairGo(MendMaterial.Order o, string town)
        {
            if (o.Pieces == 0 && o.Wait == 0 && o.NoSmith == 0 && o.Wrecks == 0) { Log.Player("Your gear is sound. Nothing to mend."); return false; }   // paczka 158: same wraki - ponizej, z napisem
            if (!o.Ok) { Log.Player("There are no town smiths here - nothing was mended.", true); return false; }
            if (o.Pieces == 0)
            {
                Log.Player("The smiths of " + town + " could mend nothing of your harness." + o.LeftEn(town), true);
                Log.Info("Lawa naprawcza (uprzaz) - kowale " + town + ": " + o.LogPl());
                return false;
            }
            if (Hero.MainHero.Gold < o.Total) { Log.Player("You cannot pay the smiths' price.", true); return false; }
            return true;
        }

        internal void RepairAll()
        {
            try
            {
                // SmithMendFromMarket: kowale miasta robia tylko czesci, na ktore jest material na targu (PlanRepair) - robota jak dotad
                // + material; reszta czeka. Przywracanie stanu - ta sama petla co dotad (jedna regula dla obu sciezek).
                List<int> only = null;
                int wrecksLeft = 0;   // paczka 158: wraki pominiete przy naprawie bez reguly kowali (z regula - order.Wrecks)
                MendMaterial.Order order = null;
                var here = Settlement.CurrentSettlement;
                string town = here != null && here.Name != null ? here.Name.ToString() : "this town";
                int cost;
                if (SmithMenu.MarketRule)
                {
                    only = new List<int>();
                    order = PlanRepair(only);
                    if (!MarketRepairGo(order, town)) return;
                    cost = order.Total;
                    order.Bench.Commit();
                }
                else
                {
                    cost = RepairCost();
                    if (cost <= 0 && HarnessWrecks() > 0) { Log.Player("The smith restores none of your harness for coin. " + HarnessWrecks() + LootPrices.WreckWhatEn(HarnessWrecks()), true); return; }   // paczka 158
                    if (cost <= 0) { Log.Player("Your gear is sound. Nothing to mend."); return; }
                    if (Hero.MainHero.Gold < cost) { Log.Player("You cannot pay the smith's price.", true); return; }
                }
                int coffer = MendMaterial.CofferNow(here);
                Pay.ToSettlement(cost);   // wpis 90 (audyt): zaplata kowalowi do kasy miasta, nie w nicosc
                if (order != null) MendMaterial.PlayerLaborToFolk(here, coffer, order.Labor);   // E8/B (258, R5): robocizna z kasy do kiesy ludu (jak N1), material zostaje w kasie

                var eq = Hero.MainHero.BattleEquipment;
                for (int slot = 0; slot < 12; slot++)
                {
                    if (only != null && !only.Contains(slot)) continue;   // SmithMendFromMarket: ta czesc czeka na material albo jest cala
                    var el = eq[slot];
                    if (el.Item == null) continue;
                    if (only == null && LootPrices.HarnessWreck(el.ItemModifier, GetConditionQuiet(slot))) { wrecksLeft++; continue; }   // paczka 158: bez reguly kowali - wrak tez zostaje
                    // ZBROJA Z KUZNI JAK BRON: kowal naprawia tylko sztuki zuzyte wedle ksiegi - te same, za ktore policzyl w RepairCost.
                    // Dotad kazda zalozona czesc dostawala "oryginalny" stan przegrodki: sztuka nigdy nie bita (zardzewiala zbroja
                    // z kuzni wychodzila zwykla za darmo, przy okazji naprawy butow) i sztuka o innej nazwie (dostawala stan
                    // poprzedniczki - takze legendarny, nawet z obcej grupy)
                    if (ArmourQuality.On && GetConditionQuiet(slot) >= 100f) continue;
                    var origId = OriginalModifier(slot);
                    ItemModifier orig = string.IsNullOrEmpty(origId) ? null : MBObjectManager.Instance.GetObject<ItemModifier>(origId);
                    eq[slot] = new EquipmentElement(el.Item, orig);
                    SetCondition(slot, 100f);
                    if (ArmourQuality.On) SetLast(slot, origId);
                }
                if (order != null)
                {
                    Log.Player("The smiths of " + town + " made " + order.Pieces + (order.Pieces == 1 ? " piece" : " pieces") + " of your harness whole again for " + cost
                               + " gold, paid into the town's coffers: " + order.Labor + " for their work and " + order.MatGold + " for materials from its market." + order.LeftEn(town));
                    Log.Info("Lawa naprawcza (uprzaz) - kowale " + town + ": sloty " + string.Join(",", only) + "; " + order.LogPl());
                    return;
                }
                Log.Player("The smith has made your harness whole again for " + cost + " gold." + (wrecksLeft > 0 ? " " + wrecksLeft + LootPrices.WreckWhatEn(wrecksLeft) : ""));
                Log.Info("Naprawa za " + cost + (wrecksLeft > 0 ? ", wraki na zlom " + wrecksLeft : ""));
            }
            catch (Exception e) { Log.Error("RepairAll", e); }
        }
    }
}
