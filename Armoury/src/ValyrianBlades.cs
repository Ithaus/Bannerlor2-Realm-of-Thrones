using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// 177-2 STAL VALYRIANSKA - SKONCZONY ZASOB (Jeff 09.10, decyzja 24a; projekt docs/PROJEKT-177-STAL-VALYRIANSKA-2026-10-09.md rozdz. 4
    /// i "Krytyka i odpowiedzi"). Prawdziwa stal valyrianska jest tylko w istniejacych klingach ROT; nikt nie robi nowej.
    ///  1. SPIS: 29 wzorow (19 nazwanych + 10 seryjnych "Valyrian Steel Sword"; bez Euron's Axe - ani ksiazki, ani ROT nie nazywaja go
    ///     valyrianskim, krytyka pkt 27). MIARA stali z klingi, nie z typu (krytyka pkt 18): miecz dwureczny z klinga co najmniej
    ///     ValyrianGreatswordMinCm (105 cm) = 2 miary - Lod 140.3, Heartsbane 114.5, Blackfyre 111, Brightroar (oba) 108; reszta 1 miara
    ///     (Longclaw 103 - poltorak, Despair 100, Orphanmaker 96.9, Nightfall 92, type 8 89.7 - ta sama klinga co type 2-4).
    ///  2. REJESTR (ile sztuk kazdego wzoru jest w swiecie): stan startowy ROT, liczony RAZ z szablonow postaci (BasicCharacterObject.
    ///     _equipmentRoster, zestaw bojowy i cywilny) - kazdy bohater zywy, nieaktywny albo zmarly PO starcie kampanii, ktorego szablon
    ///     niesie klinge; szablon wspolny (wedrowiec odrodzony z tego samego wzoru) liczy sie raz. Nazwana legenda - najwyzej JEDNA
    ///     (Vigilance: ROT dal jeden szablon trzem Hightowerom - Jeff 30.08 "jedna na swiecie"; krytyka pkt 19). Odczyt PRZED
    ///     LegendaryLaw.SweepTemplates (ta podmienia klingi w szablonach w pamieci sesji). Rejestr w zapisie (SaveText, klucz
    ///     arm_vs_registry, wlasny try); zmienia go tylko przekucie w Qohorze (177-3).
    ///  3. STRAZNIK (codziennie, po czystce magazynow AI): kazda sztuka w swiecie - bohaterowie (zestaw bojowy, cywilny i ukrycia; recenzja 177:
    ///     rod gracza - kazdy slot to osobna sztuka, bohater spoza rodu gracza - najwiecej w jednym zestawie, bo szablon ROT powtarza klinge
    ///     w zestawie cywilnym; Pieces), tabory, targi, schowki gracza, magazyny DTE (gracza i AI), magazyn wojenny Spoils, u mistrzow Qohoru.
    ///     Ponad rejestr = kopia z niczego (dowolne zrodlo) -> zwykly odpowiednik (LegendaryLaw.ReplacementFor: liczba sztuk ta sama, stal
    ///     zwykla), linia "Z NICZEGO"; zostaja najpierw: u mistrzow Qohoru (przyjete po straznika - GuardNow), wlasciciel ze stanu startowego,
    ///     inni bohaterowie, rzeczy gracza (z magazynem wojennym), tabory, polki, magazyny DTE AI. Ponizej rejestru = UBYTEK -> ERROR w logu
    ///     (bramka testu; krytyka pkt 20). Klinga w magazynie DTE partii AI idzie do wodza partii (zaklada), bez wodza - na polke miasta
    ///     najblizszego partii (krytyka pkt 3-4: wlasciciela szukamy przez UniqueLaw.FindParty).
    ///  4. PIERWSZE WCZYTANIE Z 177 (nowa kampania albo zapis Jeffa; znacznik arm_vs_mark): przy pierwszym spisie dobowym (magazyn DTE
    ///     gracza DTE odtwarza dopiero po sesji) klingi brakujace wobec stanu startowego wracaja: najpierw z poleglych (do dziedzica), potem
    ///     odtworzone u wlasciciela ze stanu startowego albo jego dziedzica (klingi zgubione przez dawne bledy - Truth u zywego Tregara,
    ///     Longclaw u Jona; krytyka pkt 16 i 19). Zgubiony klucz rejestru przy obecnym znaczniku = bezpieczny domysl: rejestr = wiecej
    ///     z (stan startowy, obecny stan) - niczego nie zamieniamy (krytyka pkt 6).
    ///  5. Przetop VS zablokowany w zwyklej kuzni (ValyrianNoSmelt): lista Smelt bez VS, prefiks DoSmelting (Priority.First) "Only the
    ///     masters of Qohor can work Valyrian steel."; SmeltTab.DoSmeltingPostfix nie odkrywa czesci VS (krytyka pkt 5). Wylaczone - przetop
    ///     niszczy klinge i rejestr maleje (recenzja 177; postfiks DoSmelting liczy sztuki w taborze przed i po).
    ///  6. Kopie z niczego zamkniete u zrodla: karawana posilkow DTE (GetRandomGearItems -> zamienniki), nagroda turniejowa (postfiks
    ///     FightTournamentGame.GetTournamentPrize - TournamentsXPanded bierze nagrode bez filtra NotMerchandise; krytyka pkt 2 i 23).
    /// </summary>
    internal static class ValyrianBlades
    {
        private static readonly string[] Named = {
            "ice_sword", "longclaw_sword", "brightroar", "brightroar2", "blackfyre", "heartsbane", "nightfall", "whyt_sword", "assist_sword",
            "oathkeeper_sword", "ww2_sword", "darksister", "lady_forlorn2", "lady_forlorn", "lamentation", "red_rain", "vigilance_sword", "truth",
            "celtigar_axe"
        };
        private static readonly string[] Serial = {
            "koa_sword_tier_5", "val_steel_sword_2", "val_steel_sword_3", "val_steel_sword_4", "val_steel_sword_5", "val_steel_sword_6",
            "val_steel_sword_7", "val_steel_sword_8", "val_steel_sword_blue", "val_steel_sword_red"
        };
        private static readonly HashSet<string> NamedSet = new HashSet<string>(Named);
        private static readonly HashSet<string> AllSet = new HashSet<string>(Named.Concat(Serial));

        internal static IEnumerable<string> AllIds { get { return Named.Concat(Serial); } }
        internal static IEnumerable<string> SerialIds { get { return Serial; } }
        internal static bool IsId(string id) { return id != null && AllSet.Contains(id); }
        internal static bool Is(ItemObject it) { return it != null && IsId(it.StringId); }
        internal static bool IsNamed(string id) { return id != null && NamedSet.Contains(id); }

        // ------------------------------------------------------------ miara
        private static readonly Dictionary<ItemObject, int> _measure = new Dictionary<ItemObject, int>();

        /// <summary>Dlugosc klingi w cm (WeaponDesign, kawalek Blade, ze skala); 0 - brak projektu.</summary>
        internal static float BladeCm(ItemObject it)
        {
            try
            {
                var d = it != null ? it.WeaponDesign : null;
                if (d == null || d.UsedPieces == null) return 0f;
                foreach (var el in d.UsedPieces)
                    if (el != null && el.CraftingPiece != null && el.CraftingPiece.PieceType == CraftingPiece.PieceTypes.Blade)
                        return el.ScaledLength * 100f;
            }
            catch { }
            return 0f;
        }

        /// <summary>Ile stali niesie klinga: 2 = wielki miecz dwureczny (klinga >= ValyrianGreatswordMinCm), 1 = reszta; 0 - nie VS.</summary>
        internal static int Measures(ItemObject it)
        {
            if (!Is(it)) return 0;
            int m;
            if (_measure.TryGetValue(it, out m)) return m;
            float min = Settings.Current != null ? Settings.Current.ValyrianGreatswordMinCm : 105f;
            m = it.ItemType == ItemObject.ItemTypeEnum.TwoHandedWeapon && BladeCm(it) >= min ? 2 : 1;
            _measure[it] = m;
            return m;
        }

        internal static ItemObject Item(string id)
        {
            try { return id != null ? MBObjectManager.Instance.GetObject<ItemObject>(id) : null; } catch { return null; }
        }

        // ------------------------------------------------------------ rejestr i zapis
        private static Dictionary<string, int> _reg;                                         // null = brak (pierwsze wczytanie z 177 / zgubiony klucz)
        private static Dictionary<string, string> _owner = new Dictionary<string, string>();  // id -> StringId wlasciciela ze stanu startowego
        private static bool _markLoaded, _recoverPending, _guardOffLogged;
        private static Dictionary<string, int> _tplCount;                                    // sesja: szablony z klinga (przed SweepTemplates)
        private static Dictionary<string, List<Hero>> _tplHeroes;
        private static Dictionary<string, string> _where = new Dictionary<string, string>();  // ostatni spis: id -> gdzie
        private static Dictionary<string, int> _lastCount = new Dictionary<string, int>();
        private static readonly HashSet<string> _lossLogged = new HashSet<string>();
        private static string _lastSummary = "";
        private static int _stumbles;

        internal static void Reset()
        {
            _reg = null; _owner = new Dictionary<string, string>(); _markLoaded = false; _recoverPending = false; _guardOffLogged = false; _smeltId = null;
            _tplCount = null; _tplHeroes = null; _where = new Dictionary<string, string>(); _lastCount = new Dictionary<string, int>();
            _lossLogged.Clear(); _lastSummary = ""; _stumbles = 0; _measure.Clear(); _dteGone = 0; _dteWeapon = 0; _dteOther = 0; _prizeSwaps = 0; _smeltBlocked = 0;
        }

        /// <summary>"v1|R|id:liczba:wlasciciel;..." (R = odzysk po pierwszym wczytaniu jeszcze czeka). Pusto - rejestru brak.</summary>
        internal static string Export()
        {
            if (_reg == null) return "";
            var sb = new StringBuilder("v1|").Append(_recoverPending ? "R" : "-").Append('|');
            foreach (var kv in _reg)
            {
                string o; _owner.TryGetValue(kv.Key, out o);
                sb.Append(kv.Key).Append(':').Append(kv.Value.ToString(CultureInfo.InvariantCulture)).Append(':').Append(o ?? "").Append(';');
            }
            return sb.ToString();
        }

        internal static void Import(string s)
        {
            _reg = null; _owner = new Dictionary<string, string>(); _recoverPending = false;
            if (string.IsNullOrEmpty(s) || !s.StartsWith("v1|")) return;
            var parts = s.Split('|');
            if (parts.Length < 3) return;
            _recoverPending = parts[1] == "R";
            var reg = new Dictionary<string, int>();
            foreach (var rec in parts[2].Split(';'))
            {
                if (rec.Length == 0) continue;
                var f = rec.Split(':');
                if (f.Length < 2 || !IsId(f[0])) continue;
                int n; if (!int.TryParse(f[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out n)) continue;
                reg[f[0]] = Math.Max(0, n);
                if (f.Length > 2 && f[2].Length > 0) _owner[f[0]] = f[2];
            }
            foreach (var id in AllIds) if (!reg.ContainsKey(id)) reg[id] = 0;
            _reg = reg;
        }

        internal static string ExportMark() { return _reg != null ? "1" : ""; }
        internal static void ImportMark(string s) { _markLoaded = s == "1"; }

        /// <summary>Wlasciciel klingi ze stanu startowego (StringId bohatera) - Qohor (177-3): czy klinga jest "obca" dla rodu.</summary>
        internal static Hero StartOwner(string id)
        {
            string o;
            if (id == null || !_owner.TryGetValue(id, out o) || string.IsNullOrEmpty(o)) return null;
            return FindHero(o);
        }

        internal static Hero FindHero(string stringId)
        {
            if (string.IsNullOrEmpty(stringId)) return null;
            try
            {
                foreach (var h in Hero.AllAliveHeroes) if (h != null && h.StringId == stringId) return h;
                foreach (var h in Hero.DeadOrDisabledHeroes) if (h != null && h.StringId == stringId) return h;
            }
            catch { }
            return null;
        }

        internal static int Registered(string id) { int n; return _reg != null && id != null && _reg.TryGetValue(id, out n) ? n : 0; }

        /// <summary>Przekucie w Qohorze (177-3): z rejestru schodzi wejscie, dochodzi wynik - stal ani nie przybywa, ani nie ubywa (miary).</summary>
        internal static void RegistryMove(IEnumerable<string> from, IEnumerable<string> to)
        {
            if (_reg == null) return;
            foreach (var id in from) if (IsId(id)) { int n; _reg.TryGetValue(id, out n); _reg[id] = Math.Max(0, n - 1); }
            foreach (var id in to) if (IsId(id)) { int n; _reg.TryGetValue(id, out n); _reg[id] = n + 1; }
        }

        internal static int BaseMeasures()
        {
            int m = 0;
            if (_reg == null) return 0;
            foreach (var kv in _reg) m += kv.Value * Measures(Item(kv.Key));
            return m;
        }

        // ------------------------------------------------------------ szablony (PRZED LegendaryLaw.SweepTemplates)
        private static readonly FieldInfo FRoster = AccessTools.Field(typeof(BasicCharacterObject), "_equipmentRoster");

        /// <summary>Kto ma klinge w szablonie (stan startowy ROT). Bohaterowie zywi, nieaktywni i zmarli PO starcie kampanii (zmarli przed
        /// startem ROT - nie). Szablon wspolny liczy sie raz.</summary>
        internal static void CaptureTemplates()
        {
            _tplCount = new Dictionary<string, int>(); _tplHeroes = new Dictionary<string, List<Hero>>();
            try
            {
                if (FRoster == null) { Log.Info("Stal valyrianska: brak pola BasicCharacterObject._equipmentRoster - stanu startowego nie odczytam."); return; }
                double start = 0;
                try { start = Campaign.Current.Models.CampaignTimeModel.CampaignStartTime.ToDays; } catch { }
                var seenRoster = new HashSet<object>();
                var heroes = new List<Hero>();
                foreach (var h in Hero.AllAliveHeroes) if (h != null) heroes.Add(h);
                foreach (var h in Hero.DeadOrDisabledHeroes) if (h != null) heroes.Add(h);
                foreach (var h in heroes)
                {
                    if (h.IsDead)
                    {
                        double dd = h.DeathDay.ToDays;
                        if (dd < start - 0.5 || dd > 1e9) continue;   // martwy od startu ROT (albo bez daty)
                    }
                    var ro = h.CharacterObject != null ? FRoster.GetValue(h.CharacterObject) as MBEquipmentRoster : null;
                    if (ro == null) continue;
                    // poprawka recenzji 177: sztuk na szablon = najwiecej w JEDNYM zestawie (ta sama regula co spis bohatera spoza rodu gracza -
                    // zestaw cywilny i ukrycia powtarza bojowy; dwie klingi w dwoch slotach jednego zestawu to dwie sztuki)
                    var ids = new Dictionary<string, int>();
                    foreach (var eq in ro.AllEquipments)
                    {
                        if (eq == null) continue;
                        var inSet = new Dictionary<string, int>();
                        for (int s = 0; s <= (int)EquipmentIndex.ExtraWeaponSlot; s++)
                        {
                            var it = eq[(EquipmentIndex)s].Item;
                            if (!Is(it)) continue;
                            int c; inSet.TryGetValue(it.StringId, out c); inSet[it.StringId] = c + 1;
                        }
                        foreach (var kv in inSet) { int was; if (!ids.TryGetValue(kv.Key, out was) || kv.Value > was) ids[kv.Key] = kv.Value; }
                    }
                    if (ids.Count == 0) continue;
                    bool firstRoster = seenRoster.Add(ro);
                    foreach (var kv in ids)
                    {
                        List<Hero> l; if (!_tplHeroes.TryGetValue(kv.Key, out l)) _tplHeroes[kv.Key] = l = new List<Hero>();
                        l.Add(h);
                        if (firstRoster) { int n; _tplCount.TryGetValue(kv.Key, out n); _tplCount[kv.Key] = n + kv.Value; }
                    }
                }
            }
            catch (Exception e) { Log.Error("ValyrianBlades.CaptureTemplates", e); }
        }

        private static Dictionary<string, int> TemplateRegistry()
        {
            var reg = new Dictionary<string, int>();
            foreach (var id in AllIds)
            {
                int n = 0;
                if (_tplCount != null) _tplCount.TryGetValue(id, out n);
                reg[id] = IsNamed(id) ? Math.Min(1, n) : n;
            }
            return reg;
        }

        /// <summary>Wlasciciel nazwanej klingi ze stanu startowego: zywy posiadacz szablonu, ktory ja nosi i jest glowa rodu; potem nosi;
        /// potem glowa rodu; potem pierwszy zywy; potem pierwszy.</summary>
        private static string ChooseOwner(string id)
        {
            List<Hero> l;
            if (_tplHeroes == null || !_tplHeroes.TryGetValue(id, out l) || l.Count == 0) return "";
            Func<Hero, bool> alive = h => h != null && h.IsAlive && !h.IsDisabled;
            Func<Hero, bool> holds = h => Holds(h, id);
            Func<Hero, bool> leader = h => h.Clan != null && h.Clan.Leader == h;
            var pick = l.FirstOrDefault(h => alive(h) && holds(h) && leader(h)) ?? l.FirstOrDefault(h => alive(h) && holds(h))
                       ?? l.FirstOrDefault(h => alive(h) && leader(h)) ?? l.FirstOrDefault(alive) ?? l[0];
            return pick != null ? pick.StringId : "";
        }

        internal static bool Holds(Hero h, string id)
        {
            if (h == null) return false;
            foreach (var eq in Sets(h))
                for (int s = 0; s <= (int)EquipmentIndex.ExtraWeaponSlot; s++) { var it = eq[(EquipmentIndex)s].Item; if (it != null && it.StringId == id) return true; }
            return false;
        }

        // ------------------------------------------------------------ sztuki na bohaterze (poprawka recenzji 177)
        /// <summary>Zestawy bohatera z bronia: bojowy, cywilny i ukrycia (Hero.StealthEquipment, gra 1.4.8; bez wspolnego zestawu domyslnego
        /// Campaign.DefaultStealthEquipment - bohater bez wlasnego dostaje ten jeden obiekt dla wszystkich, zmiana w nim zmienilaby kazdego).</summary>
        internal static List<Equipment> Sets(Hero h)
        {
            var l = new List<Equipment>(3);
            if (h == null) return l;
            if (h.BattleEquipment != null) l.Add(h.BattleEquipment);
            if (h.CivilianEquipment != null && !ReferenceEquals(h.CivilianEquipment, h.BattleEquipment)) l.Add(h.CivilianEquipment);
            try
            {
                var st = h.StealthEquipment;
                Equipment def = null;
                try { def = Campaign.Current != null ? Campaign.Current.DefaultStealthEquipment : null; } catch { }
                if (st != null && !ReferenceEquals(st, def) && !l.Any(x => ReferenceEquals(x, st))) l.Add(st);
            }
            catch { }
            return l;
        }

        /// <summary>Rod gracza (gracz, towarzysze, rodzina): kazdy slot to osobna sztuka - gracz zaklada je z ekwipunku, zestaw cywilny i ukrycia biora
        /// przedmiot z taboru jak bojowy. Bohater spoza rodu gracza: zestawy z szablonu ROT - ta sama klinga w bojowym i cywilnym to JEDNA sztuka
        /// (Despair; recenzja 177 sprawdzila XML), wiec sztuk tyle, ile najwiecej w jednym zestawie.</summary>
        internal static bool Physical(Hero h)
        {
            return h != null && (h == Hero.MainHero || (h.Clan != null && h.Clan == Clan.PlayerClan));
        }

        /// <summary>Jedna sztuka stali valyrianskiej na bohaterze: sloty, w ktorych ja widac (u bohatera spoza rodu gracza - k-ta w bojowym, k-ta
        /// w cywilnym i k-ta w ukryciu to ta sama sztuka).</summary>
        internal sealed class Piece
        {
            public string Id; public EquipmentElement El; public bool Battle;
            public readonly List<KeyValuePair<Equipment, int>> Slots = new List<KeyValuePair<Equipment, int>>();
        }

        internal static List<Piece> Pieces(Hero h, string onlyId)
        {
            var res = new List<Piece>();
            if (h == null) return res;
            var sets = Sets(h);
            bool phys = Physical(h);
            var perId = new Dictionary<string, List<KeyValuePair<Equipment, int>>[]>();
            var order = new List<string>();
            for (int k = 0; k < sets.Count; k++)
            {
                var eq = sets[k];
                for (int s = 0; s <= (int)EquipmentIndex.ExtraWeaponSlot; s++)
                {
                    var el = eq[(EquipmentIndex)s];
                    if (el.IsEmpty || !Is(el.Item) || (onlyId != null && el.Item.StringId != onlyId)) continue;
                    if (phys)
                    {
                        var p = new Piece { Id = el.Item.StringId, El = el, Battle = ReferenceEquals(eq, h.BattleEquipment) };
                        p.Slots.Add(new KeyValuePair<Equipment, int>(eq, s));
                        res.Add(p);
                        continue;
                    }
                    List<KeyValuePair<Equipment, int>>[] lists;
                    if (!perId.TryGetValue(el.Item.StringId, out lists))
                    {
                        perId[el.Item.StringId] = lists = new List<KeyValuePair<Equipment, int>>[sets.Count];
                        for (int j = 0; j < sets.Count; j++) lists[j] = new List<KeyValuePair<Equipment, int>>();
                        order.Add(el.Item.StringId);
                    }
                    lists[k].Add(new KeyValuePair<Equipment, int>(eq, s));
                }
            }
            foreach (var id in order)
            {
                var lists = perId[id];
                int n = lists.Max(x => x.Count);
                for (int j = 0; j < n; j++)
                {
                    var p = new Piece { Id = id };
                    for (int k = 0; k < lists.Length; k++)
                    {
                        if (j >= lists[k].Count) continue;
                        var sl = lists[k][j];
                        if (p.Slots.Count == 0) p.El = sl.Key[(EquipmentIndex)sl.Value];
                        if (ReferenceEquals(sl.Key, h.BattleEquipment)) p.Battle = true;
                        p.Slots.Add(sl);
                    }
                    res.Add(p);
                }
            }
            return res;
        }

        /// <summary>Zdejmuje z bohatera sztuki stali valyrianskiej (wszystkie albo tylko id, najwyzej max) i zwraca kazda raz - z modyfikatorem.</summary>
        internal static List<EquipmentElement> TakePieces(Hero h, string onlyId, int max)
        {
            var got = new List<EquipmentElement>();
            foreach (var p in Pieces(h, onlyId))
            {
                if (got.Count >= max) break;
                foreach (var sl in p.Slots) sl.Key[(EquipmentIndex)sl.Value] = EquipmentElement.Invalid;
                got.Add(p.El);
            }
            return got;
        }

        // ------------------------------------------------------------ start sesji (koniec LegendaryLaw.OnSession)
        internal static void OnSession()
        {
            try
            {
                var tpl = TemplateRegistry();
                int tplBlades = tpl.Values.Sum();
                if (_reg == null)
                {
                    // zabezpieczenie: szablony bez zadnej klingi przy klingach w swiecie = odczyt stanu startowego zawiodl (np. szablony juz
                    // przeczyszczone w tym procesie) - rejestr z niego zamienilby KAZDA klinge w zwykla stal; wtedy bezpieczny domysl jak nizej
                    int present = Gather().Values.Sum(x => x.Count);
                    bool tplFailed = tplBlades == 0 && present > 0;
                    if (tplFailed) Log.Error("ValyrianBlades.OnSession", new InvalidOperationException("Stal valyrianska: szablony postaci bez zadnej klingi przy " + present + " klingach w swiecie - stanu startowego nie odczytano; bezpieczny domysl (rejestr = obecny stan), nic nie zamieniane"));
                    if (_markLoaded || tplFailed)
                    {
                        // zgubiony klucz rejestru przy obecnym znaczniku 177: bezpieczny domysl - nic nie zamieniamy
                        var now = Gather();
                        var reg = new Dictionary<string, int>();
                        foreach (var id in AllIds) { List<Copy> l; int have = now.TryGetValue(id, out l) ? l.Count : 0; reg[id] = Math.Max(tpl[id], have); }
                        _reg = reg; _recoverPending = false;
                        Log.Info("Stal valyrianska: UWAGA - zapis ma znacznik 177, ale rejestru nie wczytano; bezpieczny domysl (wiecej ze stanu startowego i obecnego), nic nie zamieniane.");
                    }
                    else
                    {
                        _reg = tpl; _recoverPending = true;
                        Log.Info("Stal valyrianska: pierwsze wczytanie z 177 - rejestr ze stanu startowego ROT (szablony postaci): " + tplBlades + " klng, "
                                 + BaseMeasures() + " miar; nadwyzka i braki przy pierwszym spisie dobowym (kopie -> zwykla stal, brakujace -> wlasciciel albo dziedzic).");
                    }
                    foreach (var id in AllIds) if (!_owner.ContainsKey(id) || string.IsNullOrEmpty(_owner[id])) _owner[id] = ChooseOwner(id);
                }
                var copies = Gather();
                Summary(copies, true);
                var miss = new List<string>();
                foreach (var id in AllIds)
                {
                    int have = copies.ContainsKey(id) ? copies[id].Count : 0;
                    if (have != Registered(id)) miss.Add(id + " " + have + "/" + Registered(id));
                }
                Log.Info("Stal valyrianska: stan startowy wedlug szablonow " + tplBlades + " klng; rejestr " + _reg.Values.Sum() + " klng, " + BaseMeasures() + " miar"
                         + (miss.Count > 0 ? "; roznice do spisu dobowego (sztuk/rejestr): " + string.Join(", ", miss.ToArray()) : "; spis zgodny z rejestrem") + ".");
            }
            catch (Exception e) { Log.Error("ValyrianBlades.OnSession", e); }
        }

        // ------------------------------------------------------------ spis
        private enum K { Hero, Roster, Armory, Stock, Fixed }
        private sealed class Copy
        {
            public string Id; public K Kind; public int Pri; public string Where; public string PlayerWhere;
            public Hero Hero; public ItemRoster Roster; public EquipmentElement El; public IDictionary Armory; public object ArmoryKey; public object Stock; public bool Player;
        }

        private static Dictionary<string, List<Copy>> Gather()
        {
            var map = new Dictionary<string, List<Copy>>();
            Action<Copy> add = c => { List<Copy> l; if (!map.TryGetValue(c.Id, out l)) map[c.Id] = l = new List<Copy>(); l.Add(c); };
            var seen = new HashSet<Hero>();
            foreach (var h in Hero.AllAliveHeroes) HeroCopies(h, add, seen);
            foreach (var h in Hero.DeadOrDisabledHeroes) if (h != null && h.IsDisabled) HeroCopies(h, add, seen);
            foreach (var mp in MobileParty.All)
            {
                if (mp == null || mp.ItemRoster == null) continue;
                bool pl = mp == MobileParty.MainParty;
                // tabor AI: tylko prawdziwe (gra nie lupi NotMerchandise; tam odklada Wear)
                RosterCopies(mp.ItemRoster, pl ? 3 : 5, pl ? "tabor gracza" : "tabor " + mp.Name, pl ? "in your baggage" : null, add);
            }
            foreach (var st in Settlement.All)
            {
                if (st == null) continue;
                if (st.ItemRoster != null) RosterCopies(st.ItemRoster, 6, "targ " + st.Name, null, add);
                if (st.Stash != null) RosterCopies(st.Stash, 3, "schowek gracza " + st.Name, "in your stash at " + st.Name, add);
            }
            var dte = QuartermasterLaw.DteArmory();
            if (dte != null) RosterCopies(dte, 3, "magazyn DTE gracza", "in your armoury", add);
            ArmoryCopies(add);
            StockpileCopies(add);
            // 177-3: klingi u mistrzow (wejscie do wydania) - nie do zmiany; przyjete dopiero po straznika dla tego wzoru (GuardNow)
            QohorMasters.AddCensus((id, where) => add(new Copy { Id = id, Kind = K.Fixed, Pri = 4, Where = where }));
            return map;
        }

        /// <summary>Sztuki na bohaterze (poprawka recenzji 177 - dawniej jeden wpis na id): kazda sztuka osobno wedlug Pieces - rod gracza kazdy
        /// slot (bojowy, cywilny, ukrycia), reszta najwiecej w jednym zestawie (zestaw cywilny szablonu ROT powtarza bojowy).</summary>
        private static void HeroCopies(Hero h, Action<Copy> add, HashSet<Hero> seen)
        {
            if (h == null || !seen.Add(h)) return;
            var pieces = Pieces(h, null);
            if (pieces.Count == 0) return;
            bool player = Physical(h);
            foreach (var p in pieces)
            {
                string o; _owner.TryGetValue(p.Id, out o);
                int pri = o == h.StringId ? 0 : (h == Hero.MainHero || (h.Clan != null && h.Clan.Leader == h)) ? 1 : 2;
                add(new Copy
                {
                    Id = p.Id, Kind = K.Hero, Pri = pri, Hero = h, Player = player,
                    Where = (h.IsDisabled ? "nieaktywny " : "nosi ") + h.Name + (p.Battle ? "" : " (zestaw cywilny/ukrycia)"),
                    PlayerWhere = h == Hero.MainHero ? "you carry" : "carried by " + h.Name
                });
            }
        }

        private static void RosterCopies(ItemRoster r, int pri, string where, string playerWhere, Action<Copy> add)
        {
            for (int i = 0; i < r.Count; i++)
            {
                var el = r.GetElementCopyAtIndex(i);
                if (el.Amount <= 0 || !Is(el.EquipmentElement.Item)) continue;
                for (int n = 0; n < el.Amount; n++)
                    add(new Copy { Id = el.EquipmentElement.Item.StringId, Kind = K.Roster, Pri = pri, Roster = r, El = el.EquipmentElement, Player = playerWhere != null, PlayerWhere = playerWhere, Where = where });
            }
        }

        private static IDictionary PartyArmories()
        {
            try
            {
                var t = AccessTools.TypeByName("DynamicTroopEquipmentReupload.EveryoneCampaignBehavior");
                var f = t != null ? AccessTools.Field(t, "PartyArmories") : null;
                return f != null ? f.GetValue(null) as IDictionary : null;
            }
            catch { return null; }
        }

        private static void ArmoryCopies(Action<Copy> add)
        {
            var map = PartyArmories();
            if (map == null) return;
            foreach (DictionaryEntry e in map)
            {
                var inner = e.Value as IDictionary;
                if (inner == null) continue;
                foreach (DictionaryEntry kv in inner)
                {
                    var it = kv.Key as ItemObject;
                    if (!Is(it)) continue;
                    int n = 0; try { n = Convert.ToInt32(kv.Value); } catch { }
                    // magazyn DTE partii AI - ostatni w kolejce: trafiaja tam tylko kopie (lup DTE z poleglych bohaterow, dawne karawany posilkow)
                    for (int k = 0; k < n; k++) add(new Copy { Id = it.StringId, Kind = K.Armory, Pri = 7, Armory = inner, ArmoryKey = e.Key, El = new EquipmentElement(it), Where = "magazyn DTE partii AI" });
                }
            }
        }

        // magazyn wojenny Spoils of War (gracz): QuartermasterBehavior._stockpileManager._stockpiles -> WarStockpile.ItemIds / ItemCounts /
        // ItemIsLooted (zwykle listy, rownolegle - krytyka pkt 12). Recenzja 177: to RZECZY GRACZA (Pri 3, do zamiany jak tabor), nie miejsce
        // "nie do zmiany" - przed 177 loteria Spoils w symulacji kopiowala tu klingi pokonanych bohaterow; kopia nie moze wypierac prawdziwej klingi.
        private static Type _tQm;
        private static void StockpileCopies(Action<Copy> add)
        {
            try
            {
                if (_tQm == null) _tQm = AccessTools.TypeByName("RealisticLoot.Behaviors.QuartermasterBehavior");
                if (_tQm == null || Campaign.Current == null) return;
                var get = typeof(Campaign).GetMethod("GetCampaignBehavior");
                var qm = get != null ? get.MakeGenericMethod(_tQm).Invoke(Campaign.Current, null) : null;
                var mgr = qm != null ? AccessTools.Field(_tQm, "_stockpileManager").GetValue(qm) : null;
                var dict = mgr != null ? AccessTools.Field(mgr.GetType(), "_stockpiles").GetValue(mgr) as IDictionary : null;
                if (dict == null) return;
                foreach (DictionaryEntry e in dict)
                {
                    var sp = e.Value;
                    if (sp == null) continue;
                    var ids = AccessTools.Property(sp.GetType(), "ItemIds").GetValue(sp, null) as List<string>;
                    var counts = AccessTools.Property(sp.GetType(), "ItemCounts").GetValue(sp, null) as List<int>;
                    if (ids == null || counts == null) continue;
                    string town = null;
                    try { var st = MBObjectManager.Instance.GetObject<Settlement>(e.Key as string); if (st != null) town = st.Name.ToString(); } catch { }
                    for (int i = 0; i < ids.Count && i < counts.Count; i++)
                        if (IsId(ids[i]))
                            for (int k = 0; k < counts[i]; k++)
                                add(new Copy { Id = ids[i], Kind = K.Stock, Pri = 3, Player = true, Stock = sp, Where = "magazyn wojenny Spoils " + e.Key,
                                               PlayerWhere = "in your war stockpile" + (town != null ? " at " + town : "") });
                }
            }
            catch (Exception ex) { if (_stumbles++ < 2) Log.Error("ValyrianBlades.Stockpile", ex); }
        }

        /// <summary>Magazyn wojenny: jedna sztuka id -> zamiennik (ta sama pozycja "z lupu" albo nie; listy rownolegle jak WarStockpile.AddItem
        /// i sprzedaz automatyczna Spoils - pusta pozycja znika ze wszystkich trzech list).</summary>
        private static void StockSwap(object sp, string id, ItemObject repl)
        {
            try
            {
                if (sp == null) return;
                var t = sp.GetType();
                var ids = AccessTools.Property(t, "ItemIds").GetValue(sp, null) as List<string>;
                var counts = AccessTools.Property(t, "ItemCounts").GetValue(sp, null) as List<int>;
                var pLoot = AccessTools.Property(t, "ItemIsLooted");
                var looted = pLoot != null ? pLoot.GetValue(sp, null) as List<bool> : null;
                if (ids == null || counts == null) return;
                int i = -1;
                for (int j = 0; j < ids.Count && j < counts.Count; j++) if (ids[j] == id && counts[j] > 0) { i = j; break; }
                if (i < 0) return;
                bool lo = looted != null && i < looted.Count && looted[i];
                counts[i]--;
                if (counts[i] <= 0)
                {
                    ids.RemoveAt(i); counts.RemoveAt(i);
                    if (looted != null && i < looted.Count) looted.RemoveAt(i);
                }
                if (repl == null) return;
                for (int j = 0; j < ids.Count && j < counts.Count; j++)
                    if (ids[j] == repl.StringId && (looted == null || j >= looted.Count || looted[j] == lo)) { counts[j]++; return; }
                ids.Add(repl.StringId); counts.Add(1);
                if (looted != null) { while (looted.Count < ids.Count - 1) looted.Add(false); looted.Add(lo); }
            }
            catch (Exception e) { if (_stumbles++ < 3) Log.Error("ValyrianBlades.StockSwap", e); }
        }

        // ------------------------------------------------------------ straznik dobowy
        /// <summary>Wolane z doby LegendaryLaw, zaraz po czystce magazynow AI (jedno przejscie po swiecie na dobe).</summary>
        internal static void Daily()
        {
            if (_reg == null) return;
            try
            {
                var s = Settings.Current;
                var copies = Gather();
                int fromNothing = 0, recovered = 0, fromDead = 0, moved = 0, smelted = 0;
                var notes = new List<string>();
                // recenzja 177: wylaczony straznik tylko liczy - pierwszy spis z 177 (zamiana kopii, odzysk brakujacych) czeka na wlaczenie
                bool recover = _recoverPending && s.ValyrianGuard;
                foreach (var id in AllIds)
                {
                    List<Copy> l; copies.TryGetValue(id, out l);
                    if (l == null) l = new List<Copy>();
                    int allowed = Registered(id);
                    if (l.Count > allowed && s.ValyrianGuard) fromNothing += Surplus(id, l, notes);
                    else if (l.Count < allowed && recover)
                    {
                        int need = allowed - l.Count;
                        fromDead += FromDead(id, ref need, notes);
                        if (need > 0 && SmeltedByPlayer(Item(id)))
                        {
                            // recenzja 177: przed 177 przetop VS byl dozwolony, a SmeltTab odkrywal wtedy WSZYSTKIE czesci klingi (jedyna droga do
                            // nich - SmeltTab.DoSmeltingPostfix); czesci odkryte w kuzni gracza = klinga przetopiona, jej stal jest juz sztabami -
                            // nie odtwarzamy (nic z niczego), rejestr w dol jak przy przetopie z ValyrianNoSmelt wylaczonym
                            int was = Registered(id);
                            _reg[id] = Math.Max(0, was - need);
                            notes.Add(id + " przetopiona przez gracza przed 177 (czesci klingi odkryte w kuzni) - nie odtwarzam, rejestr " + was + " -> " + _reg[id]);
                            smelted += need;
                        }
                        else recovered += Restore(id, need, notes);
                    }
                    // klinga w magazynie DTE partii AI - do wodza tej partii (zaklada), bez wodza - na polke
                    foreach (var c in l.Where(x => x.Kind == K.Armory).ToList()) { if (MoveFromArmory(c, notes)) moved++; }
                }
                if (recover)
                {
                    _recoverPending = false;
                    Log.Info("Stal valyrianska: pierwszy spis z 177 - kopii ponad stan startowy zamienionych na zwykla stal " + fromNothing + ", odzysk z poleglych " + fromDead
                             + ", odtworzone u wlasciciela ze stanu startowego albo dziedzica " + recovered + ", przetopione przez gracza przed 177 (bez odtwarzania, rejestr w dol) " + smelted + ".");
                }
                else if (_recoverPending && !_guardOffLogged)
                {
                    _guardOffLogged = true;
                    Log.Info("Stal valyrianska: straznik wylaczony (Valyrian Guard) - tylko spis; pierwszy spis z 177 (kopie -> zwykla stal, brakujace -> wlasciciel) czeka na wlaczenie.");
                }
                if (notes.Count > 0) Log.Info("Stal valyrianska: dzien " + (int)CampaignTime.Now.ToDays + " - " + string.Join(" | ", notes.ToArray()) + ".");
                Summary(fromNothing + recovered + fromDead + moved + smelted > 0 ? Gather() : copies, false);
            }
            catch (Exception e) { Log.Error("ValyrianBlades.Daily", e); }
        }

        /// <summary>Nadwyzka ponad rejestr -> zwykly odpowiednik w tym samym miejscu. Kolejnosc zostawiania: u mistrzow Qohoru (nie do zmiany - klinga
        /// wchodzi tam dopiero po straznika dla swojego wzoru, GuardNow, wiec to prawdziwa stal), potem wedlug Pri: wlasciciel startowy 0, gracz i glowy
        /// rodow 1, inni bohaterowie 2, rzeczy gracza 3 (tabor, schowki, magazyn DTE gracza, magazyn wojenny Spoils), tabory AI 5, targi 6, magazyny
        /// DTE AI 7. Zamienione sztuki schodza z listy l.</summary>
        private static int Surplus(string id, List<Copy> l, List<string> notes)
        {
            int allowed = Registered(id);
            if (l == null || l.Count <= allowed) return 0;
            var order = l.OrderBy(c => c.Kind == K.Fixed ? 0 : 1).ThenBy(c => c.Pri).ToList();
            int n = 0;
            for (int i = allowed; i < order.Count; i++)
            {
                var c = order[i];
                if (c.Kind == K.Fixed) { notes.Add(id + " nadwyzka w miejscu nie do zmiany (" + c.Where + ")"); continue; }
                var repl = Forge(id, c);
                n++;
                notes.Add("Z NICZEGO +1 " + id + " " + c.Where + " -> " + (repl != null ? repl.StringId : "nic"));
                l.Remove(c);
            }
            return n;
        }

        /// <summary>Straznik dla jednego wzoru, teraz (recenzja 177): przed przyjeciem klingi przez mistrzow Qohoru - kopia z niczego nie przechodzi
        /// przez Qohor jako "prawdziwa" (tam jest nie do zmiany, a przekucie przesuwa rejestr). True - cos zamienione na zwykla stal.</summary>
        internal static bool GuardNow(string id)
        {
            if (_reg == null || !IsId(id) || Settings.Current == null || !Settings.Current.ValyrianGuard) return false;
            try
            {
                var copies = Gather();
                List<Copy> l; copies.TryGetValue(id, out l);
                var notes = new List<string>();
                int n = Surplus(id, l, notes);
                if (notes.Count > 0) Log.Info("Stal valyrianska: dzien " + (int)CampaignTime.Now.ToDays + " (przed przyjeciem w Qohorze) - " + string.Join(" | ", notes.ToArray()) + ".");
                return n > 0;
            }
            catch (Exception e) { Log.Error("ValyrianBlades.GuardNow", e); return false; }
        }

        /// <summary>Kopia ponad rejestr: w tym miejscu zostaje zwykly odpowiednik (liczba sztuk ta sama, stal zwykla). Jedna kopia = jedna sztuka.</summary>
        private static ItemObject Forge(string id, Copy c)
        {
            var it = Item(id);
            ItemObject repl = null;
            try { repl = LegendaryLaw.ReplacementFor(it); } catch { }
            var replEl = repl != null ? new EquipmentElement(repl) : EquipmentElement.Invalid;
            switch (c.Kind)
            {
                case K.Hero:
                    {
                        // recenzja 177: JEDNA sztuka (dawniej kazdy slot z tym id - dwie klingi za jedna nadwyzke); najpierw sztuka spoza zestawu
                        // bojowego (cywilny/ukrycia), zeby bohater zostal z bronia w reku; sztuka bohatera spoza rodu gracza - wraz z blizniakiem
                        var p = Pieces(c.Hero, id).OrderBy(x => x.Battle ? 1 : 0).FirstOrDefault();
                        if (p != null) foreach (var sl in p.Slots) sl.Key[(EquipmentIndex)sl.Value] = replEl;
                    }
                    break;
                case K.Roster:
                    c.Roster.AddToCounts(c.El, -1);
                    if (repl != null) c.Roster.AddToCounts(replEl, 1);
                    break;
                case K.Armory:
                    {
                        int n = 0; try { n = Convert.ToInt32(c.Armory[it]); } catch { }
                        if (n <= 1) c.Armory.Remove(it); else c.Armory[it] = n - 1;
                        if (repl != null) { int r = 0; try { if (c.Armory.Contains(repl)) r = Convert.ToInt32(c.Armory[repl]); } catch { } c.Armory[repl] = r + 1; }
                    }
                    break;
                case K.Stock:
                    StockSwap(c.Stock, id, repl);
                    break;
            }
            // recenzja 177: nazwa posiadacza i miejsca (dawniej "you hold" - takze o klindze towarzysza albo w magazynie)
            if (c.Player && it != null)
                Log.Player("There is more " + it.Name + " in the world than was ever forged: the one " + (c.PlayerWhere ?? "you keep") + " is a fine forgery"
                           + (repl != null ? " - only " + repl.Name + "." : ", worth nothing."), true);
            return repl;
        }

        /// <summary>Odzysk z poleglych (pierwsze wczytanie): klinga na bohaterze zmarlym PO starcie kampanii idzie do jego dziedzica - kazda sztuka.</summary>
        private static int FromDead(string id, ref int need, List<string> notes)
        {
            int n = 0;
            try
            {
                double start = 0;
                try { start = Campaign.Current.Models.CampaignTimeModel.CampaignStartTime.ToDays; } catch { }
                foreach (var h in Hero.DeadOrDisabledHeroes)
                {
                    if (need <= 0) break;
                    if (h == null || !h.IsDead || h.DeathDay.ToDays < start - 0.5 || !Holds(h, id)) continue;
                    var got = TakePieces(h, id, need);
                    if (got.Count == 0) continue;
                    var heir = UniqueSpoils.HeirOf(h);
                    foreach (var el in got)
                    {
                        UniqueSpoils.Give(heir, el, h, null, "odzysk z poleglego " + h.Name, "recovered from the fallen " + h.Name);
                        need--; n++;
                    }
                    notes.Add("odzysk " + id + (got.Count > 1 ? " x" + got.Count : "") + " z poleglego " + h.Name + " -> " + (heir != null ? heir.Name.ToString() : "polka miasta"));
                }
            }
            catch (Exception e) { Log.Error("ValyrianBlades.FromDead", e); }
            return n;
        }

        /// <summary>Odtworzenie wedlug stanu startowego (pierwsze wczytanie): klinga zgubiona przez dawne bledy kodu wraca do wlasciciela ze
        /// stanu startowego (gdy zyje i jej nie ma), inaczej do jego dziedzica, na koniec na polke.</summary>
        private static int Restore(string id, int need, List<string> notes)
        {
            int n = 0;
            var it = Item(id);
            if (it == null || need <= 0) return 0;
            var cands = new List<Hero>();
            var o = StartOwner(id);
            if (o != null) cands.Add(o);
            List<Hero> l;
            if (_tplHeroes != null && _tplHeroes.TryGetValue(id, out l)) foreach (var h in l) if (!cands.Contains(h)) cands.Add(h);
            foreach (var h in cands)
            {
                if (need <= 0) break;
                if (h == null || Holds(h, id)) continue;
                Hero to = h.IsAlive && !h.IsDisabled ? h : UniqueSpoils.HeirOf(h);
                if (to != null && Holds(to, id)) continue;
                UniqueSpoils.Give(to, new EquipmentElement(it), h, null, "stan startowy (" + h.Name + ")", "returned to its rightful line");
                notes.Add("odtworzona " + id + " -> " + (to != null ? to.Name.ToString() : "polka miasta") + " (wlasciciel startowy " + h.Name + ")");
                need--; n++;
            }
            if (need > 0) notes.Add("UWAGA: " + id + " brakuje " + need + " - nikt ze stanu startowego (rejestr bez zmian)");
            return n;
        }

        private static readonly FieldInfo FTemplate = AccessTools.Field(typeof(WeaponDesign), "Template");

        /// <summary>Czy gracz rozebral ten wzor w kuzni przed 177: wszystkie czesci projektu klingi (poza danymi z gory) odkryte u gracza. Czesci
        /// stali valyrianskiej sa ukryte w projektancie (losowe odkrycie gry ich nie bierze - CraftingCampaignBehavior.OpenNewPart pomija
        /// IsHiddenOnDesigner); odkrywal je tylko SmeltTab.DoSmeltingPostfix przy przetopie legendy (do 177 takze VS).</summary>
        private static bool SmeltedByPlayer(ItemObject it)
        {
            try
            {
                var d = it != null ? it.WeaponDesign : null;
                var beh = Campaign.Current != null ? Campaign.Current.GetCampaignBehavior<CraftingCampaignBehavior>() : null;
                var tpl = d != null && FTemplate != null ? FTemplate.GetValue(d) as CraftingTemplate : null;
                if (beh == null || tpl == null || d.UsedPieces == null) return false;
                int n = 0;
                foreach (var el in d.UsedPieces)
                {
                    var piece = el != null ? el.CraftingPiece : null;
                    if (piece == null || piece.IsEmptyPiece || piece.IsGivenByDefault) continue;
                    if (!beh.IsOpened(piece, tpl)) return false;
                    n++;
                }
                return n > 0;
            }
            catch { return false; }
        }

        private static bool MoveFromArmory(Copy c, List<string> notes)
        {
            try
            {
                var it = c.El.Item;
                int n = 0; try { n = Convert.ToInt32(c.Armory[it]); } catch { }
                if (n <= 0) return false;
                if (n <= 1) c.Armory.Remove(it); else c.Armory[it] = n - 1;
                MobileParty mp = null;
                try { if (c.ArmoryKey is MBGUID) mp = UniqueLaw.FindParty((MBGUID)c.ArmoryKey); } catch { }
                Hero lead = mp != null ? mp.LeaderHero : null;
                if (lead == null && mp != null && mp.Party != null) lead = mp.Party.Owner;
                string name = mp != null ? mp.Name.ToString() : null;
                // recenzja 177: bez wodza i wlasciciela - miasto najblizsze partii (at = mp), nie pierwsze miasto swiata
                UniqueSpoils.Give(lead, new EquipmentElement(it), lead, null, "magazyn DTE " + (name ?? "partii"), name != null ? "from the stores of " + name : "from a war band's stores", mp);
                notes.Add(it.StringId + " z magazynu DTE " + (name ?? "nieznanej partii") + " -> " + (lead != null ? lead.Name.ToString() : "polka miasta"));
                return true;
            }
            catch (Exception e) { if (_stumbles++ < 3) Log.Error("ValyrianBlades.MoveFromArmory", e); return false; }
        }

        /// <summary>Linia spisu: przy starcie sesji zawsze, potem tylko gdy cos sie zmienilo. Ubytek wobec rejestru = ERROR (raz na wzor,
        /// do nastepnej zmiany).</summary>
        private static void Summary(Dictionary<string, List<Copy>> copies, bool start)
        {
            int blades = 0, measures = 0, onHeroes = 0, bags = 0, shelves = 0, stash = 0, dte = 0, stock = 0, qohor = 0;
            var where = new Dictionary<string, string>();
            var changes = new List<string>();
            foreach (var id in AllIds)
            {
                List<Copy> l; copies.TryGetValue(id, out l);
                int n = l != null ? l.Count : 0;
                int m = Measures(Item(id));
                blades += n; measures += n * m;
                if (l != null)
                    foreach (var c in l)
                    {
                        if (c.Kind == K.Hero) onHeroes++;
                        else if (c.Kind == K.Armory || c.Where.StartsWith("magazyn DTE")) dte++;
                        else if (c.Where.StartsWith("targ")) shelves++;
                        else if (c.Where.StartsWith("schowek")) stash++;
                        else if (c.Where.StartsWith("magazyn wojenny")) stock++;
                        else if (c.Where.StartsWith("u mistrzow")) qohor++;
                        else bags++;
                    }
                string w = l != null && l.Count > 0 ? string.Join("; ", l.Select(c => c.Where).ToArray()) : "";
                if (w.Length > 0) where[id] = w;
                int was; _lastCount.TryGetValue(id, out was);
                string ww; _where.TryGetValue(id, out ww);
                if (!start && (was != n || (ww ?? "") != w)) changes.Add(id + ": " + (w.Length > 0 ? w : "nigdzie"));
                int reg = Registered(id);
                if (n < reg && !_recoverPending && !start)   // przy starcie sesji nie: magazyn DTE gracza DTE odtwarza dopiero po sesji
                {
                    if (_lossLogged.Add(id + "|" + n))
                        Log.Error("ValyrianBlades.Census", new InvalidOperationException("Stal valyrianska: UBYTEK -" + (reg - n) + " - " + id + " (jest " + n + ", rejestr " + reg + "), ostatnio: " + ((ww ?? "").Length > 0 ? ww : "nieznane")));
                }
                else _lossLogged.RemoveWhere(x => x.StartsWith(id + "|"));
                _lastCount[id] = n;
            }
            _where = where;
            string sum = "Stal valyrianska: dzien " + (int)CampaignTime.Now.ToDays + " - " + blades + " klng, " + measures + " miar (rejestr " + (_reg != null ? _reg.Values.Sum() : 0)
                         + " klng, baza " + BaseMeasures() + " miar): na bohaterach " + onHeroes + ", tabory " + bags + ", targi " + shelves + ", schowki gracza " + stash
                         + ", magazyny DTE " + dte + ", magazyn wojenny " + stock + ", u mistrzow Qohoru " + qohor;
            if (start || changes.Count > 0 || sum != _lastSummary)
                Log.Info(sum + (changes.Count > 0 ? "; zmiany: " + string.Join(" | ", changes.ToArray()) : "") + ".");
            _lastSummary = sum;
        }

        /// <summary>Dla kroniki unikatow: gdzie jest kazda klinga wedlug ostatniego spisu (takze miejsca, ktorych kronika nie widzi).</summary>
        internal static Dictionary<string, string> ElsewhereFromCensus() { return new Dictionary<string, string>(_where); }

        // ------------------------------------------------------------ latki: przetop, karawana DTE, nagroda turniejowa
        private static int _smeltBlocked, _dteWeapon, _dteOther, _dteGone, _prizeSwaps;

        internal static void ApplyAll(Harmony h)
        {
            var got = new List<string>();
            try
            {
                var mDo = AccessTools.Method(typeof(CraftingCampaignBehavior), "DoSmelting");
                if (mDo != null)
                {
                    h.Patch(mDo, prefix: new HarmonyMethod(typeof(ValyrianBlades), nameof(SmeltPrefix)) { priority = Priority.First },
                                 postfix: new HarmonyMethod(typeof(ValyrianBlades), nameof(SmeltPostfix)) { priority = Priority.Last });
                    got.Add("przetop");
                }
                var tVm = QuartermasterLaw.FindType("TaleWorlds.CampaignSystem.ViewModelCollection.WeaponCrafting.Smelting.SmeltingVM");
                var mRefresh = tVm != null ? AccessTools.Method(tVm, "RefreshList") : null;
                if (mRefresh != null) { h.Patch(mRefresh, postfix: new HarmonyMethod(typeof(ValyrianBlades), nameof(SmeltListPostfix)) { priority = Priority.Last }); got.Add("lista Smelt"); }
            }
            catch (Exception e) { Log.Error("ValyrianBlades.ApplyAll(przetop)", e); }
            try
            {
                var t = AccessTools.TypeByName("DynamicTroopEquipmentReupload.CutTheirSupplyBehavior");
                var m = t != null ? AccessTools.Method(t, "GetRandomGearItems") : null;
                if (m != null) { h.Patch(m, postfix: new HarmonyMethod(typeof(ValyrianBlades), nameof(DteGearPostfix))); got.Add("karawana DTE"); }
            }
            catch (Exception e) { Log.Error("ValyrianBlades.ApplyAll(DTE)", e); }
            try
            {
                var m = AccessTools.Method(typeof(TaleWorlds.CampaignSystem.TournamentGames.FightTournamentGame), "GetTournamentPrize");
                if (m != null) { h.Patch(m, postfix: new HarmonyMethod(typeof(ValyrianBlades), nameof(PrizePostfix)) { priority = Priority.Last }); got.Add("nagroda turniejowa"); }
            }
            catch (Exception e) { Log.Error("ValyrianBlades.ApplyAll(turniej)", e); }
            Log.Info("ValyrianBlades: latki wpiete - " + (got.Count > 0 ? string.Join(", ", got.ToArray()) : "ZADNA") + ".");
        }

        // przetop dozwolony (ValyrianNoSmelt wylaczone): ile sztuk tej klingi bylo w taborze przed przetopem (recenzja 177)
        private static string _smeltId;
        private static int _smeltBefore;

        /// <summary>Zwykla kuznia nie rusza stali valyrianskiej (kanon: przekuwac umie tylko Qohor) - niezaleznie od CraftingEnabled.</summary>
        public static bool SmeltPrefix(EquipmentElement equipmentElement)
        {
            _smeltId = null;
            try
            {
                if (!Is(equipmentElement.Item)) return true;
                if (!Settings.Current.ValyrianNoSmelt)
                {
                    var r = MobileParty.MainParty != null ? MobileParty.MainParty.ItemRoster : null;
                    if (r != null && _reg != null) { _smeltId = equipmentElement.Item.StringId; _smeltBefore = r.GetItemNumber(equipmentElement.Item); }
                    return true;
                }
                _smeltBlocked++;
                Log.Player("Only the masters of Qohor can work Valyrian steel.", true);
                Log.Info("ValyrianBlades: przetop " + equipmentElement.Item.StringId + " zablokowany (zwykla kuznia).");
                return false;
            }
            catch { return true; }
        }

        /// <summary>Recenzja 177: przetop dozwolony (ValyrianNoSmelt wylaczone) niszczy klinge - gra daje sztaby, a stal valyrianska ginie; rejestr
        /// maleje o tyle sztuk, ile ubylo z taboru (niezaleznie od tego, ktory prefiks wykonal przetop), inaczej codzienny falszywy UBYTEK.</summary>
        public static void SmeltPostfix(EquipmentElement equipmentElement)
        {
            var id = _smeltId;
            _smeltId = null;
            if (id == null) return;
            try
            {
                var r = MobileParty.MainParty != null ? MobileParty.MainParty.ItemRoster : null;
                if (r == null || equipmentElement.Item == null || equipmentElement.Item.StringId != id) return;
                int gone = _smeltBefore - r.GetItemNumber(equipmentElement.Item);
                if (gone <= 0) return;
                int was = Registered(id);
                for (int i = 0; i < gone; i++) RegistryMove(new[] { id }, new string[0]);
                Log.Info("Kronika unikatow: " + id + " przetopiona w kuzni gracza (Valyrian No Smelt wylaczone) - stal valyrianska stracona, rejestr " + was + " -> " + Registered(id) + ".");
            }
            catch (Exception e) { if (_stumbles++ < 3) Log.Error("ValyrianBlades.SmeltPostfix", e); }
        }

        /// <summary>Lista Smelt bez klng valyrianskich (tylko odejmuje).</summary>
        public static void SmeltListPostfix(object __instance)
        {
            try
            {
                if (!Settings.Current.ValyrianNoSmelt) return;
                var list = Traverse.Create(__instance).Property("SmeltableItemList").GetValue() as IList;
                if (list == null) return;
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    var vm = list[i];
                    if (vm == null) continue;
                    object el = null;
                    try { el = Traverse.Create(vm).Property("EquipmentElement").GetValue(); } catch { }
                    if (el is EquipmentElement && Is(((EquipmentElement)el).Item)) list.RemoveAt(i);
                }
            }
            catch (Exception e) { if (_stumbles++ < 3) Log.Error("ValyrianBlades.SmeltList", e); }
        }

        /// <summary>Karawana posilkow DTE losuje NOWY sprzet z puli kultury (CutTheirSupplyBehavior.GetRandomGearItems) - kazda legenda i unikat
        /// -> zwykly odpowiednik (bron: LegendaryLaw.ReplacementFor; reszta: UniqueLaw.StandInFor w kulturze karawany; krytyka pkt 11);
        /// bez zamiennika - wypada.</summary>
        public static void DteGearPostfix(BasicCultureObject culture, List<ItemObject> __result)
        {
            try
            {
                if (__result == null || !Settings.Current.NoConjuredLegends) return;
                for (int i = __result.Count - 1; i >= 0; i--)
                {
                    var it = __result[i];
                    if (it == null || !(LegendaryLaw.IsLegend(it) || ArmsPricing.IsUnique(it) || Is(it))) continue;
                    ItemObject repl = null;
                    if (it.HasWeaponComponent) { try { repl = LegendaryLaw.ReplacementFor(it); } catch { } }
                    if (repl == null) { try { repl = UniqueLaw.StandInFor(it, culture); } catch { } }
                    if (repl == null) { __result.RemoveAt(i); _dteGone++; continue; }
                    __result[i] = repl;
                    if (it.HasWeaponComponent) _dteWeapon++; else _dteOther++;
                }
            }
            catch (Exception e) { if (_stumbles++ < 3) Log.Error("ValyrianBlades.DteGear", e); }
        }

        /// <summary>Nagroda turniejowa (wanilia albo TournamentsXPanded - jego prefiks bierze nagrode z Items.All bez filtra NotMerchandise,
        /// pula elitarna to kazdy przedmiot tieru 5+): legenda, unikat albo VS -> zwykly odpowiednik (postfiks idzie po kazdym prefiksie).</summary>
        public static void PrizePostfix(TaleWorlds.CampaignSystem.TournamentGames.FightTournamentGame __instance, ref ItemObject __result)
        {
            try
            {
                var it = __result;
                if (it == null || !Settings.Current.NoConjuredLegends || !(LegendaryLaw.IsLegend(it) || ArmsPricing.IsUnique(it) || Is(it))) return;
                ItemObject repl = null;
                if (it.HasWeaponComponent) { try { repl = LegendaryLaw.ReplacementFor(it); } catch { } }
                if (repl == null)
                {
                    BasicCultureObject cul = null;
                    try { cul = __instance != null && __instance.Town != null ? __instance.Town.Culture : null; } catch { }
                    try { repl = UniqueLaw.StandInFor(it, cul); } catch { }
                }
                if (repl == null) return;
                __result = repl;
                _prizeSwaps++;
                Log.Info("ValyrianBlades: nagroda turniejowa " + it.StringId + " -> " + repl.StringId + " (unikat nie powstaje z niczego).");
            }
            catch (Exception e) { if (_stumbles++ < 3) Log.Error("ValyrianBlades.Prize", e); }
        }

        /// <summary>Linia doby latek (tylko gdy cos zlapaly).</summary>
        internal static void DailyPatches()
        {
            if (_smeltBlocked + _dteWeapon + _dteOther + _dteGone + _prizeSwaps == 0) return;
            Log.Info("ValyrianBlades: dzien " + (int)CampaignTime.Now.ToDays + " - karawany posilkow DTE: legend/unikatow -> zamienniki " + (_dteWeapon + _dteOther)
                     + " (bron " + _dteWeapon + ", reszta " + _dteOther + "), bez zamiennika " + _dteGone + "; nagrody turniejowe zamienione " + _prizeSwaps
                     + "; przetop VS zablokowany " + _smeltBlocked + ".");
            _smeltBlocked = _dteWeapon = _dteOther = _dteGone = _prizeSwaps = 0;
        }
    }
}
