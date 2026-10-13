using System;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Workshops;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// E6/0 (206) - KRAINA WYROBU I LANCUCH KUCIA, TRYB ODCZYTU (PLAN 6.19; Jeff 11.10 17:30: "kowale i rzemieslnicy kuja pancerze wedlug kultury swojej
    /// krainy - Polnoc nie kuje zbroi Lannisterow, nikt nie kuje unikatow"; projekt etapu 6 v2, rozdz. 3 "E6/0" i "E6/M1", narzedzie SCR\e6P\chain.py v4).
    /// Tylko liczy i pisze do logu - niczego nie zmienia w grze (czynny lancuch - E6/M1, klucz WorkshopCraftByRealm).
    ///
    /// KRAINA WYROBU (CultureOf - tylko do produkcji, Item.Culture zostaje nietkniety: czytaja go DTE, kaskada 175 w CS, UniqueLaw, HeroGear):
    ///  - nikt: unikaty (ArmsPricing.IsUnique - takze NotMerchandise), legendy (LegendaryLaw.IsLegend), zakazane warsztatom (WorkshopForbiddenIds),
    ///    stroje bandytow (id bandit_/tacky_bandit/looter, kultury looters/sea_raiders/wights/whitewalker), lion_sword (unikat z przydomkiem postaci -
    ///    w spisie RotUniques go nie ma, v2 KB), wildling_armor3 i wildling_sword (Wolni Ludzie nie kuja stali - przychodza z lupu), stal i lamelki
    ///    wanilii Dothrakow (khuzait z metalem, poza dothraki_* i arakhami - przywozi handel Vaes Dothrak);
    ///  - literowki XML: riverlands -> river, volantene -> volantine, Empire -> empire, Vlandia -> vlandia;
    ///  - bron rodow bez kultury wedlug prefiksu id (lannister_ -> vlandia, stark_ -> battania ...); reszta bez kultury = neutralna.
    /// ROD wyrobu (HouseOf): nazwa albo id zawiera rod lub instytucje (Stark, Lannister ... Golden Company) - wzor rodowy kuje tylko wlasna kraina.
    /// WSPOLNE strony morza: wyrob noszony (rynsztunek bojowy) przez jednostki Soldier/Mercenary co najmniej 4 kultur jednej strony.
    ///
    /// LANCUCH na koszyk typ x tier (koszyk jak WorkshopLaw.BasketOf), dla krainy miasta = FreeHands.RealmOf (kultura z dnia kalibracji -
    /// PeopleBook.CultureAtCalibration, zapas Measure40.CultureAtStart; ta sama definicja co wolne rece E4/7 i zima E4/1):
    ///  (1) wlasna kraina, (2) pokrewna (bez rodowych), (3) region lore (bez rodowych), (4) neutralne i wspolne tej strony morza (bez rodowych),
    ///  (5) luki, kusze i tarcze t1-2 dowolnej krainy tej strony (bez rodowych), (6) obce - nie kuje, (7) nikt.
    ///  Filtry lore v2: Wolni Ludzie - na kazdym poziomie tylko wyroby bez metalu (wyjatek braz Thennow: thenn_*, bronze_*); pule wanilii Braavos
    ///  (empire) i Lorath (nord) tylko dla nich samych (poziom 1) albo jako wspolne strony morza (4) - Volantis, Myr, Tyrosh nie kuja rzymskich i
    ///  nordyckich wzorow. W koszyku zostaje tylko najnizszy dostepny poziom (Allowed); pusto - koszyk nie powstaje (przywozi handel).
    /// Stan czyszczony w konstruktorze ArmouryBehavior (Reset); spis wyrobow raz na sesje (po HistoricalPrices.Apply - metal z ArmsPricing.CostOf).
    ///
    /// E6/M1 (207) - LANCUCH CZYNNY (klucz WorkshopCraftByRealm; Jeff 12.10 00:00: zakaz unikatow tylko dla kowali miast i AI, kuznia gracza bez zmian):
    /// ta sama funkcja Allowed w czterech drogach wyrobu miasta - warsztaty notabli (WorkshopLaw.Candidates: bez "pusta linia = cala obca pula"),
    /// strzelarze (TownFletchers.CandidatesOf), dorobek startowy (ColdStart.Markets) i droga gry (warsztaty gracza i linie towarowe z wyrobem
    /// uzbrojenia): postfiks GetRandomItemAux (WorkshopLaw.RandomItemPostfix) - wyrob spoza lancucha albo unikat losowany od nowa z tej samej
    /// kategorii wedlug lancucha (wagi gry 1/(max(100, wartosc)+100)), pusto - pusty wyrob (gra go pomija, cykl gracza bez utargu nie biegnie).
    /// Zapas gry wola GetRandomItemAux bez miasta, wiec miasto daje okno kontekstu: prefiks + finalizer na GetItemsToProduce ([ThreadStatic]).
    /// Kuznia gracza (SmithingModel, nasze receptury) - bez zmian.
    /// </summary>
    internal static class CraftRealm
    {
        internal const int LOwn = 1, LKin = 2, LRegion = 3, LCommon = 4, LSide = 5, LForeign = 6, LNobody = 7;

        private static readonly string[] West = { "battania", "vlandia", "aserai", "sturgia", "river", "vale", "crownlands", "stormlands", "reach", "dragonstone", "freefolk", "nightswatch", "skagosi" };
        private static readonly string[] Essos = { "empire", "nord", "khuzait", "volantine", "tyroshi", "qohorik", "ghiscari", "valyrian", "pentoshi", "lyseni", "norvos", "yiti", "ibbenese", "qartheen", "myrish", "summer", "sarnor" };
        // pokrewienstwo (projekt etapu 6, tabela M1 - lore; projekt decyduje, Jeff moze zmienic)
        private static readonly Dictionary<string, string[]> Kin = new Dictionary<string, string[]>
        {
            { "battania", new[] { "skagosi", "nightswatch" } }, { "skagosi", new[] { "battania" } }, { "nightswatch", new[] { "battania" } },
            { "freefolk", new string[0] }, { "sturgia", new string[0] },
            { "river", new[] { "vale", "crownlands" } }, { "vale", new[] { "river", "crownlands" } }, { "crownlands", new[] { "dragonstone", "stormlands", "river" } },
            { "stormlands", new[] { "crownlands", "reach" } }, { "reach", new[] { "stormlands", "vlandia" } }, { "vlandia", new[] { "reach", "river" } },
            { "dragonstone", new[] { "crownlands", "valyrian" } }, { "aserai", new[] { "stormlands", "reach" } },
            { "empire", new[] { "nord", "pentoshi" } }, { "nord", new[] { "empire", "ibbenese" } }, { "pentoshi", new[] { "empire", "myrish" } },
            { "myrish", new[] { "lyseni", "tyroshi" } }, { "lyseni", new[] { "myrish", "tyroshi", "volantine" } }, { "tyroshi", new[] { "myrish", "lyseni" } },
            { "norvos", new[] { "qohorik" } }, { "qohorik", new[] { "norvos" } }, { "volantine", new[] { "lyseni", "valyrian" } }, { "valyrian", new[] { "volantine" } },
            { "ghiscari", new[] { "volantine", "qartheen" } }, { "qartheen", new[] { "yiti", "ghiscari" } }, { "yiti", new[] { "qartheen" } },
            { "ibbenese", new[] { "nord", "empire" } }, { "sarnor", new[] { "qohorik", "khuzait" } }, { "khuzait", new string[0] }, { "summer", new string[0] }
        };
        private static readonly string[][] Regions =
        {
            new[] { "POLNOC", "battania", "skagosi", "nightswatch" },
            new[] { "ANDAL", "vlandia", "reach", "stormlands", "river", "vale", "crownlands", "dragonstone" },
            new[] { "DORNE", "aserai" }, new[] { "ZAMUR", "freefolk" }, new[] { "WYSPY", "sturgia" },
            new[] { "WOLNE", "empire", "nord", "pentoshi", "myrish", "lyseni", "tyroshi", "norvos", "qohorik", "volantine", "valyrian" },
            new[] { "WSCHOD", "ghiscari", "qartheen", "yiti" }, new[] { "IBBEN", "ibbenese" }, new[] { "STEP", "khuzait", "sarnor" }, new[] { "LETNIE", "summer" }
        };
        private static readonly Dictionary<string, string> Typo = new Dictionary<string, string> { { "riverlands", "river" }, { "volantene", "volantine" }, { "Empire", "empire" }, { "Vlandia", "vlandia" } };
        private static readonly string[][] HousePrefix =
        {
            new[] { "lannister_", "vlandia" }, new[] { "stark_", "battania" }, new[] { "martell_", "aserai" }, new[] { "yronwood_", "aserai" }, new[] { "dorne_", "aserai" },
            new[] { "baratheon_", "stormlands" }, new[] { "velaryon_", "dragonstone" }, new[] { "dragonstone_", "dragonstone" }, new[] { "unsullied_", "ghiscari" },
            new[] { "serpent_kopesh", "ghiscari" }, new[] { "kopesh", "ghiscari" }, new[] { "katana", "yiti" }, new[] { "yiti_", "yiti" }, new[] { "bronze_sword", "freefolk" },
            new[] { "rapier", "empire" }, new[] { "lyseni_", "lyseni" }, new[] { "golden_company_", "volantine" }
        };
        private static readonly string[] Houses =
        {
            "stark", "lannister", "bolton", "karstark", "glover", "cerwyn", "manderly", "mormont", "umber", "reed", "tully", "blackwood", "bracken", "mallister", "frey",
            "arryn", "royce", "grafton", "tyrell", "hightower", "tarly", "redwyne", "florent", "baratheon", "dondarrion", "tarth", "martell", "dayne", "yronwood",
            "greyjoy", "harlaw", "velaryon", "celtigar", "targaryen", "casterly", "westerling", "kingsguard", "city watch", "bloodrider", "golden company"
        };
        private static readonly string[] BanditIds = { "bandit_", "tacky_bandit", "looter" };
        private static readonly HashSet<string> BanditCultures = new HashSet<string> { "looters", "sea_raiders", "wights", "whitewalker" };
        private static readonly HashSet<string> NobodyIds = new HashSet<string> { "lion_sword", "wildling_armor3", "wildling_sword" };
        private static readonly string[] KhuzaitSteel = { "khuzait_heavy_armor", "brass_lamellar_over_mail", "eastern_lamellar_armor", "eastern_vendel_helmet", "spiked_helmet", "steel_round_shield", "khuzait_lance_" };

        private sealed class Info
        {
            public string Realm = "";          // kraina wyrobu; "" = neutralny
            public bool House, Nobody, Bandit, Unique, Metal, Bronze;
            public char Side;                  // 'W' / 'E' - wspolny tej strony morza; '\0' - nie
            public int Basket, Tier;
            public ItemObject.ItemTypeEnum Type;
        }

        private static readonly object Gate = new object();
        private static Dictionary<ItemObject, Info> _info;
        private static Dictionary<string, string> _region;
        private static readonly Dictionary<string, Dictionary<ItemObject, int>> _allowed = new Dictionary<string, Dictionary<ItemObject, int>>();
        private static readonly Dictionary<string, Dictionary<int, int>> _best = new Dictionary<string, Dictionary<int, int>>();
        private static int _nItems, _nPrefix, _nTypo, _nHouse, _nCommonW, _nCommonE, _nBandit, _nNobody, _nKhuzaitSteel, _nUnique, _nNeutral, _ffSteel;
        private static bool _startLogged;
        internal static bool CtxWired;     // E6/M1: okno kontekstu GetItemsToProduce wpiete

        // E6/0: wyroby miast wedlug poziomu lancucha (zdarzenie OnItemProduced - warsztaty notabli, strzelarze, droga gry), od ostatniej linii
        private static readonly long[] _day = new long[LNobody + 1];
        private static long _dayUnique, _daySide;
        private static readonly Dictionary<string, int> _foreignBy = new Dictionary<string, int>();
        private static int _stumbles;
        private static readonly HashSet<string> _errSites = new HashSet<string>();
        // P2/C1: zwykle siodlo w kazdej krainie - kraina -> { wzory zwyklego siodla juz w lancuchu, wzory dopisane }; wzor dopisany -> w ilu krainach (do linii startu)
        private static readonly Dictionary<string, int[]> _plain = new Dictionary<string, int[]>();
        private static readonly Dictionary<string, int> _plainIds = new Dictionary<string, int>();

        internal static void Reset()
        {
            lock (Gate) { _info = null; _region = null; _allowed.Clear(); _best.Clear(); _plain.Clear(); _plainIds.Clear(); }
            _nItems = _nPrefix = _nTypo = _nHouse = _nCommonW = _nCommonE = _nBandit = _nNobody = _nKhuzaitSteel = _nUnique = _nNeutral = _ffSteel = 0;
            _startLogged = false;
            Array.Clear(_day, 0, _day.Length); _dayUnique = _daySide = 0; _foreignBy.Clear();
            _lineDays = _lineLost = _gameRedrawn = _gameEmptied = _gameUnique = 0;
            _stumbles = 0; _errSites.Clear();
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            if (_errSites.Add(where)) Log.Error("CraftRealm." + where, e);
        }

        internal static bool MeasureOn { get { var s = Settings.Current; return s != null && s.Stage6MeasureLog; } }
        /// <summary>E6/M1: lancuch czynny (klucz) i spis gotowy - inaczej wszystkie drogi jak przed paczka.</summary>
        internal static bool Active { get { var s = Settings.Current; return s != null && s.WorkshopCraftByRealm && Ready; } }

        // E6/M1: liczniki (od ostatniej linii) - dni-linii warsztatow notabli, z nich bez wzoru krainy (stara regula dalaby obca pule); droga gry
        private static long _lineDays, _lineLost, _gameRedrawn, _gameEmptied, _gameUnique;
        [ThreadStatic] private static Town _ctx;
        private static System.Reflection.FieldInfo _itemsInCategory;

        /// <summary>Uzbrojenie (zbroja, tarcza, bron, amunicja, uprzaz) - wyroby, ktore lancuch dotyczy.</summary>
        internal static bool IsArms(ItemObject it)
        {
            if (it == null) return false;
            switch (it.ItemType)
            {
                case ItemObject.ItemTypeEnum.HeadArmor:
                case ItemObject.ItemTypeEnum.BodyArmor:
                case ItemObject.ItemTypeEnum.LegArmor:
                case ItemObject.ItemTypeEnum.HandArmor:
                case ItemObject.ItemTypeEnum.Cape:
                case ItemObject.ItemTypeEnum.ChestArmor:
                case ItemObject.ItemTypeEnum.Shield:
                case ItemObject.ItemTypeEnum.OneHandedWeapon:
                case ItemObject.ItemTypeEnum.TwoHandedWeapon:
                case ItemObject.ItemTypeEnum.Polearm:
                case ItemObject.ItemTypeEnum.Bow:
                case ItemObject.ItemTypeEnum.Crossbow:
                case ItemObject.ItemTypeEnum.Thrown:
                case ItemObject.ItemTypeEnum.Arrows:
                case ItemObject.ItemTypeEnum.Bolts:
                case ItemObject.ItemTypeEnum.Pistol:
                case ItemObject.ItemTypeEnum.Musket:
                case ItemObject.ItemTypeEnum.Bullets:
                case ItemObject.ItemTypeEnum.HorseHarness:
                    return true;
                default: return false;
            }
        }

        private static int TierOf(ItemObject it) { try { return Math.Max(1, Math.Min(6, (int)it.Tier + 1)); } catch { return 1; } }

        /// <summary>Kraina miasta (kultura z dnia kalibracji - jedna definicja z E4/7); "?" gdy brak.</summary>
        internal static string RealmOfTown(Town t)
        {
            return t == null || t.Settlement == null ? "?" : FreeHands.RealmOf(t.Settlement);
        }

        private static char SideOf(string realm)
        {
            if (string.IsNullOrEmpty(realm)) return '\0';
            if (Array.IndexOf(West, realm) >= 0) return 'W';
            if (Array.IndexOf(Essos, realm) >= 0) return 'E';
            return '\0';
        }

        /// <summary>Spis jest gotowy (albo da sie go zrobic teraz): kampania trwa, ceny historyczne nalozone (metal z ArmsPricing.CostOf).</summary>
        internal static bool Ready
        {
            get
            {
                if (_info != null) return true;
                if (Campaign.Current == null || !HistoricalPrices.Applied || MBObjectManager.Instance == null) return false;
                return Ensure() != null;
            }
        }

        private static Dictionary<ItemObject, Info> Ensure()
        {
            var d = _info;
            if (d != null) return d;
            lock (Gate)
            {
                if (_info != null) return _info;
                if (Campaign.Current == null || !HistoricalPrices.Applied || MBObjectManager.Instance == null) return null;
                var reg = new Dictionary<string, string>();
                foreach (var r in Regions) for (int i = 1; i < r.Length; i++) reg[r[i]] = r[0];
                var wear = Wearers();
                var map = new Dictionary<ItemObject, Info>();
                foreach (var it in MBObjectManager.Instance.GetObjectTypeList<ItemObject>())
                {
                    try
                    {
                        if (!IsArms(it)) continue;
                        map[it] = Build(it, wear);
                    }
                    catch (Exception e) { Stumble("Ensure(przedmiot)", e); }
                }
                _region = reg;
                _info = map;
                return map;
            }
        }

        /// <summary>Wyrob -> kultury jednostek Soldier/Mercenary, ktore go nosza w rynsztunku bojowym (jak chain.py: wspolne strony morza).</summary>
        private static Dictionary<ItemObject, HashSet<string>> Wearers()
        {
            var w = new Dictionary<ItemObject, HashSet<string>>();
            try
            {
                foreach (var ch in CharacterObject.All)
                {
                    if (ch == null || ch.IsHero || ch.Culture == null) continue;
                    if (ch.Occupation != Occupation.Soldier && ch.Occupation != Occupation.Mercenary) continue;
                    string c = ch.Culture.StringId;
                    foreach (var eq in ch.BattleEquipments)
                    {
                        if (eq == null) continue;
                        for (int sl = 0; sl < 12; sl++)
                        {
                            var x = eq[(EquipmentIndex)sl].Item;
                            if (x == null) continue;
                            HashSet<string> set;
                            if (!w.TryGetValue(x, out set)) { set = new HashSet<string>(); w[x] = set; }
                            set.Add(c);
                        }
                    }
                }
            }
            catch (Exception e) { Stumble("Wearers", e); }
            return w;
        }

        private static Info Build(ItemObject it, Dictionary<ItemObject, HashSet<string>> wear)
        {
            var f = new Info { Type = it.ItemType, Tier = TierOf(it) };
            f.Basket = (int)it.ItemType * 10 + f.Tier;
            _nItems++;
            string id = (it.StringId ?? "").ToLowerInvariant();
            string name = it.Name != null ? it.Name.ToString().ToLowerInvariant() : "";
            var cost = ArmsPricing.CostOf(it);
            f.Metal = cost != null && cost.MetalKg > 0.001f;
            f.Bronze = id.StartsWith("thenn_") || id.StartsWith("bronze_");
            f.Unique = ArmsPricing.IsUnique(it) || LegendaryLaw.IsLegend(it) || WorkshopLaw.Forbidden(it);
            if (f.Unique) { f.Nobody = true; _nUnique++; return f; }
            string c = it.Culture != null ? it.Culture.StringId : "";
            bool bandit = BanditCultures.Contains(c);
            if (!bandit) foreach (var b in BanditIds) if (id.StartsWith(b) || id.Contains(b)) { bandit = true; break; }
            if (bandit) { f.Nobody = f.Bandit = true; _nBandit++; return f; }
            if (NobodyIds.Contains(id)) { f.Nobody = true; _nNobody++; return f; }
            string t;
            if (c != null && Typo.TryGetValue(c, out t)) { c = t; _nTypo++; }
            if (string.IsNullOrEmpty(c) || c == "neutral_culture")
            {
                c = "";
                foreach (var hp in HousePrefix) if (id.StartsWith(hp[0])) { c = hp[1]; _nPrefix++; break; }
                if (c.Length == 0) _nNeutral++;
            }
            f.Realm = c;
            if (c == "khuzait" && (IsKhuzaitSteel(id) || !(id.StartsWith("dothraki") || id.Contains("arakh") || !f.Metal)))
            {
                f.Nobody = true; _nKhuzaitSteel++; return f;     // stal i lamelki wanilii Dothrakow - nikt (handel Vaes Dothrak)
            }
            string s = id + " " + name;
            foreach (var h in Houses) if (s.Contains(h.Replace(' ', '_')) || s.Contains(h)) { f.House = true; break; }
            if (f.House) _nHouse++;
            HashSet<string> ws;
            if (wear.TryGetValue(it, out ws))
            {
                int nw = 0, ne = 0;
                foreach (var x in ws) { char sd = SideOf(x); if (sd == 'W') nw++; else if (sd == 'E') ne++; }
                if (nw >= 4) { f.Side = 'W'; _nCommonW++; }
                else if (ne >= 4) { f.Side = 'E'; _nCommonE++; }
            }
            return f;
        }

        private static bool IsKhuzaitSteel(string id)
        {
            foreach (var k in KhuzaitSteel) if (id.StartsWith(k)) return true;
            return false;
        }

        private static int Level(string realm, Info f, bool countFf)
        {
            if (f.Nobody) return LNobody;
            if (realm == "freefolk" && f.Metal && !f.Bronze) { if (countFf) _ffSteel++; return LForeign; }   // Wolni Ludzie nie kuja stali
            string r = f.Realm;
            if (r.Length > 0 && r == realm) return LOwn;
            char side = SideOf(realm);
            if (r == "empire" || r == "nord") return !f.House && f.Side != '\0' && f.Side == side ? LCommon : LForeign;   // wanilia Braavos/Lorath - tylko u siebie albo jako wspolne
            if (f.House) return LForeign;                                                                                 // wzor rodowy - tylko wlasna kraina
            string[] kin;
            if (r.Length > 0 && Kin.TryGetValue(realm ?? "", out kin) && Array.IndexOf(kin, r) >= 0) return LKin;
            string rr, rm;
            if (r.Length > 0 && _region != null && _region.TryGetValue(r, out rr) && _region.TryGetValue(realm ?? "", out rm) && rr == rm) return LRegion;
            if (r.Length == 0 || (f.Side != '\0' && f.Side == side)) return LCommon;
            bool sideKind = f.Type == ItemObject.ItemTypeEnum.Bow || f.Type == ItemObject.ItemTypeEnum.Crossbow || (f.Type == ItemObject.ItemTypeEnum.Shield && f.Tier <= 2);
            if (sideKind && side != '\0' && SideOf(r) == side) return LSide;
            return LForeign;
        }

        /// <summary>Poziom lancucha wyrobu dla krainy (1 wlasne ... 6 obce, 7 nikt), bez porownania z koszykiem - do licznikow E6/0.</summary>
        internal static int RawLevel(string realm, ItemObject it)
        {
            var d = Ensure();
            Info f;
            if (d == null || it == null || !d.TryGetValue(it, out f)) return LForeign;
            return Level(realm, f, false);
        }

        /// <summary>Wyroby, ktore kraina kuje: poziom &lt;= 5 i najnizszy dostepny w swoim koszyku. Raz na kraine i sesje.</summary>
        private static Dictionary<ItemObject, int> AllowedMap(string realm)
        {
            realm = realm ?? "?";
            Dictionary<ItemObject, int> m;
            lock (Gate) { if (_allowed.TryGetValue(realm, out m)) return m; }
            var d = Ensure();
            if (d == null) return null;
            var best = new Dictionary<int, int>();
            var lv = new Dictionary<ItemObject, int>();
            bool countFf = realm == "freefolk";
            foreach (var kv in d)
            {
                int l = Level(realm, kv.Value, countFf);
                if (l > LSide) continue;
                lv[kv.Key] = l;
                int b;
                if (!best.TryGetValue(kv.Value.Basket, out b) || l < b) best[kv.Value.Basket] = l;
            }
            m = new Dictionary<ItemObject, int>();
            foreach (var kv in lv) if (kv.Value == best[d[kv.Key].Basket]) m[kv.Key] = kv.Value;
            // P2/C1 (bieg probny 2, TRIAZ-2 "GLOWNE", H-1, H-3): ZWYKLE SIODLO KUJE SIODLARZ KAZDEJ KRAINY - nazwany wyjatek od reguly "w koszyku tylko najnizszy
            // poziom lancucha" (E6/M1). Kropierz rodowy (poziom 1) albo siodlo slonia (region) wypieralo z koszyka uprzaz t1 neutralne siodlo (poziom 4) w 15 z 30
            // krain = 52 z 97 miast, a Reach mial pule pusta - po E8/H (jezdziec = kon + uprzaz) siodlarze robili 25.7 szt./d przy potrzebie 40+. Dopisywane tylko
            // wzory poziomu wspolnego (neutralne i wspolne strony morza: bez rodowych, bandyckich, unikatow; Wolni Ludzie dalej bez metalu - Level), rodziny konia
            // (SaddleLaw.Fits), z pancerzem konia do granicy "siodlo" cennika (PriceList8.SaddleMaxArmor). best / Cover bez zmian (linia pokrycia nie liczy uprzezy).
            int had = 0, added = 0;
            List<string> ids = null;
            var st = Settings.Current;
            if (st != null && st.PlainSaddleEveryRealm)
                foreach (var kv in lv)
                {
                    if (!IsPlainSaddle(kv.Key)) continue;
                    if (m.ContainsKey(kv.Key)) { had++; continue; }
                    if (kv.Value != LCommon) continue;
                    m[kv.Key] = LCommon; added++;
                    if (ids == null) ids = new List<string>();
                    ids.Add(kv.Key.StringId ?? "?");
                }
            lock (Gate)
            {
                if (!_allowed.ContainsKey(realm))
                {
                    _plain[realm] = new[] { had, added };
                    if (ids != null) foreach (var id in ids) { int c; _plainIds.TryGetValue(id, out c); _plainIds[id] = c + 1; }
                }
                _allowed[realm] = m; _best[realm] = best;
            }
            return m;
        }

        /// <summary>P2/C1: zwykle siodlo - uprzaz rodziny konia z pancerzem konia do granicy "siodlo" cennika E8/C (kropierze i bardy - nie).</summary>
        private static bool IsPlainSaddle(ItemObject it)
        {
            try
            {
                if (it == null || it.ItemType != ItemObject.ItemTypeEnum.HorseHarness) return false;
                var ac = it.ArmorComponent;
                return ac != null && ac.BodyArmor <= PriceList8.SaddleMaxArmor && SaddleLaw.Fits(it);
            }
            catch (Exception e) { Stumble("IsPlainSaddle", e); return false; }
        }

        /// <summary>P2/C1: dopisek linii startu - w ilu krainach zwykle siodlo bylo juz w lancuchu, w ilu dopisane i ktore wzory (wzor x liczba krain).</summary>
        private static string PlainText()
        {
            var s = Settings.Current;
            if (s == null || !s.PlainSaddleEveryRealm) return " | zwykle siodlo w kazdej krainie (P2/C1): klucz wylaczony";
            int own = 0, addedIn = 0, none = 0;
            var tp = new List<string>();
            lock (Gate)
            {
                foreach (var kv in _plain)
                {
                    if (kv.Key == "?") continue;
                    if (kv.Value[0] > 0) own++;
                    if (kv.Value[1] > 0) addedIn++;
                    if (kv.Value[0] + kv.Value[1] == 0) none++;
                }
                foreach (var kv in _plainIds) tp.Add(kv.Key + " x" + kv.Value);
            }
            tp.Sort(StringComparer.Ordinal);
            return " | zwykle siodlo w kazdej krainie (P2/C1): krain z wlasnym wzorem w lancuchu " + own + ", dopisane wspolne w " + addedIn + " krainach, bez zadnego wzoru " + none
                   + " [" + string.Join(", ", tp.ToArray()) + "]";
        }

        /// <summary>Czy kowale tej krainy kuja ten wyrob (lancuch na koszyk). False takze przy braku spisu.</summary>
        internal static bool Allowed(string realm, ItemObject it)
        {
            if (it == null) return false;
            var m = AllowedMap(realm);
            return m != null && m.ContainsKey(it);
        }

        // ------------------------------------------------------------ E6/M1: droga gry i liczniki linii
        /// <summary>WorkshopLaw.Candidates: jedna linia warsztatu notabla na dobe - lost = lancuch nie daje zadnego wzoru, a stara regula dalaby obca pule.</summary>
        internal static void NoteLine(bool lost) { _lineDays++; if (lost) _lineLost++; }

        public static void CtxPrefix(Workshop __1) { _ctx = __1 != null && __1.Settlement != null ? __1.Settlement.Town : null; }
        public static Exception CtxFinalizer(Exception __exception) { _ctx = null; return __exception; }

        /// <summary>Z WorkshopLaw.RandomItemPostfix (postfiks GetRandomItemAux gry): wyrob uzbrojenia spoza lancucha krainy miasta albo unikat - losowanie od nowa
        /// z tej samej kategorii wedlug lancucha; pusto - pusty wyrob. Miasto: argument gry albo okno kontekstu GetItemsToProduce.</summary>
        internal static void FilterRandom(WorkshopsCampaignBehavior beh, ItemCategory cat, Town arg, ref EquipmentElement res)
        {
            try
            {
                var it = res.Item;
                if (it == null || cat == null || !IsArms(it)) return;
                var town = arg ?? _ctx;
                if (town == null || !Active) return;
                string realm = RealmOfTown(town);
                var m = AllowedMap(realm);
                if (m == null || m.ContainsKey(it)) return;
                bool last = arg == null || _ctx == null;           // zapas gry (bez miasta) albo wywolanie spoza GetItemsToProduce - ostatnie losowanie tego wyrobu
                if (ArmsPricing.IsUnique(it)) _gameUnique++;      // recenzja T4-E6: kazde odciete losowanie unikatu (takze pierwsze, z miastem - unikat zastapiony od razu wyrobem lancucha tez byl odciety)
                if (_itemsInCategory == null) _itemsInCategory = AccessTools.Field(typeof(WorkshopsCampaignBehavior), "_itemsInCategory");
                var dict = beh != null && _itemsInCategory != null ? _itemsInCategory.GetValue(beh) as Dictionary<ItemCategory, List<ItemObject>> : null;
                List<ItemObject> items;
                var cands = new List<ItemObject>(); var ws = new List<float>(); float total = 0f;
                if (dict != null && dict.TryGetValue(cat, out items) && items != null)
                    foreach (var x in items)
                    {
                        if (x == null || x.ItemCategory != cat || !m.ContainsKey(x)) continue;
                        float w = 1f / (Math.Max(100, x.Value) + 100f);
                        cands.Add(x); ws.Add(w); total += w;
                    }
                if (cands.Count == 0 || total <= 0f)
                {
                    res = default(EquipmentElement);
                    if (last) _gameEmptied++;
                    return;
                }
                float r = MBRandom.RandomFloat * total;
                int k = 0;
                for (; k < cands.Count - 1; k++) { r -= ws[k]; if (r < 0f) break; }
                var pick = cands[k];
                ItemModifier mod = null;
                try { var g = pick.ItemComponent != null ? pick.ItemComponent.ItemModifierGroup : null; if (g != null) mod = g.GetRandomItemModifierProductionScoreBased(); } catch { }
                res = new EquipmentElement(pick, mod);
                _gameRedrawn++;
            }
            catch (Exception e) { Stumble("FilterRandom", e); }
        }

        internal static void ApplyAll(Harmony h)
        {
            try
            {
                var m = AccessTools.Method(typeof(WorkshopsCampaignBehavior), "GetItemsToProduce");
                if (m != null)
                {
                    h.Patch(m, prefix: new HarmonyMethod(typeof(CraftRealm), nameof(CtxPrefix)), finalizer: new HarmonyMethod(typeof(CraftRealm), nameof(CtxFinalizer)));
                    CtxWired = true;
                }
                Log.Info("CraftRealm (E6/M1): okno kontekstu drogi gry " + (CtxWired ? "GetItemsToProduce wpiete" : "BRAK GetItemsToProduce - droga gry filtruje tylko losowanie z miastem")
                         + "; lancuch czytany na zywo (Workshop Craft By Realm).");
            }
            catch (Exception e) { Log.Error("CraftRealm.ApplyAll", e); }
        }

        private static string ChainText()
        {
            if (!Active) return "";
            return " | lancuch E6/M1: dni-linii warsztatow " + _lineDays + ", bez wzoru krainy " + _lineLost
                   + (_lineDays > 0 ? " (" + (100.0 * _lineLost / _lineDays).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "%)" : "")
                   + "; droga gry: wylosowano od nowa " + _gameRedrawn + ", pusto " + _gameEmptied + ", unikatow odcietych " + _gameUnique;
        }

        // ------------------------------------------------------------ E6/0: liczniki i linie
        /// <summary>Nasluch OnItemProducedEvent (ArmouryBehavior): wyrob uzbrojenia dopisany miastu - poziom lancucha wobec krainy miasta (tylko licznik).</summary>
        internal static void OnItemProduced(ItemObject it, Settlement st, int n)
        {
            if (n <= 0 || st == null || !st.IsTown || st.Town == null || !IsArms(it) || !MeasureOn) return;
            try
            {
                if (!Ready) return;
                string realm = RealmOfTown(st.Town);
                int l = RawLevel(realm, it);
                _day[l] += n;
                if (l == LSide) _daySide += n;
                if (ArmsPricing.IsUnique(it)) _dayUnique += n;
                if (l == LForeign)
                {
                    string k = st.Name != null ? st.Name.ToString() : st.StringId;
                    int c; _foreignBy.TryGetValue(k, out c); _foreignBy[k] = c + n;
                }
            }
            catch (Exception e) { Stumble("OnItemProduced", e); }
        }

        private static string Cover(string realm, ItemObject.ItemTypeEnum t)
        {
            Dictionary<int, int> best;
            lock (Gate) _best.TryGetValue(realm, out best);
            var sb = new StringBuilder(6);
            for (int tier = 1; tier <= 6; tier++)
            {
                int l;
                if (best == null || !best.TryGetValue((int)t * 10 + tier, out l)) { sb.Append('.'); continue; }
                sb.Append(l == LOwn ? 'w' : l == LKin ? 'p' : l == LRegion ? 'r' : l == LCommon ? 'n' : 's');
            }
            return sb.ToString();
        }

        /// <summary>Linia startowa (raz na sesje, przy pierwszej dobie): spis krain i pokrycie koszykow korpusu, helmu i tarczy t1-6 wedlug lancucha.</summary>
        private static void StartLine()
        {
            _startLogged = true;
            var parts = new List<string>();
            int empty = 0;
            foreach (var arr in new[] { West, Essos })
                foreach (var r in arr)
                {
                    if (AllowedMap(r) == null) continue;
                    string T = Cover(r, ItemObject.ItemTypeEnum.BodyArmor), H = Cover(r, ItemObject.ItemTypeEnum.HeadArmor), S = Cover(r, ItemObject.ItemTypeEnum.Shield);
                    foreach (var ch in T + H + S) if (ch == '.') empty++;
                    parts.Add(r + " T " + T + " H " + H + " S " + S);
                }
            Log.Info("CraftRealm (E6/0): krain " + (West.Length + Essos.Length) + ", wyrobow uzbrojenia " + _nItems + " | kraina z prefiksu rodu " + _nPrefix + ", literowek " + _nTypo
                     + ", neutralnych " + _nNeutral + ", rodowych " + _nHouse + ", wspolnych strony morza W " + _nCommonW + " / E " + _nCommonE + ", bandyckich " + _nBandit
                     + ", unikatow/legend/zakazanych " + _nUnique + " | odciete lore: Wolni Ludzie stal " + _ffSteel + ", Dothrakowie stal wanilii " + _nKhuzaitSteel
                     + ", wanilia Braavos/Lorath tylko u siebie (regula), nikt (lion_sword, wildling) " + _nNobody + " | okno kontekstu drogi gry: " + (CtxWired ? "GetItemsToProduce OK" : "BRAK (E6/M1 niewpiete)")
                     + " | lancuch " + (Active ? "CZYNNY (E6/M1)" : "tylko pomiar") + PlainText() + ".");
            Log.Info("CraftRealm (E6/0) pokrycie koszykow korpusu T, helmu H, tarczy S t1-6 (w wlasne, p pokrewne, r region, n wspolne/neutralne, s strona morza, . nie kuje - przywozi handel), pustych "
                     + empty + ": " + string.Join("; ", parts.ToArray()) + ".");
        }

        /// <summary>Z ArmouryBehavior.OnDailyTick (Stage6Measure.Daily): linia "Warsztaty wedlug krainy (E6/0)" i zerowanie licznikow doby.</summary>
        internal static void Daily()
        {
            if (!MeasureOn) { Array.Clear(_day, 0, _day.Length); _dayUnique = _daySide = 0; _foreignBy.Clear(); _lineDays = _lineLost = _gameRedrawn = _gameEmptied = _gameUnique = 0; return; }
            if (!Ready) return;
            if (!_startLogged) StartLine();
            int day = (int)CampaignTime.Now.ToDays;
            var top = new List<KeyValuePair<string, int>>(_foreignBy);
            top.Sort((a, b) => b.Value.CompareTo(a.Value));
            var tp = new List<string>();
            for (int i = 0; i < top.Count && i < 5; i++) tp.Add(top[i].Key + ": " + top[i].Value);
            Log.Info("Warsztaty wedlug krainy (E6/0" + (Active ? ", lancuch E6/M1 CZYNNY" : ", na sucho") + "): dzien " + day + " | sztuk: wlasne " + _day[LOwn]
                     + ", pokrewne " + _day[LKin] + ", region " + _day[LRegion] + ", wspolne " + (_day[LCommon] + _day[LSide]) + " (w tym strona morza " + _daySide + "), OBCE " + _day[LForeign]
                     + ", bandyckie/nikt " + _day[LNobody] + " | unikatow " + _dayUnique + ChainText() + " | najwiecej obcych: [" + string.Join(", ", tp.ToArray()) + "]"
                     + (_stumbles > 0 ? " | potkniecia " + _stumbles : "") + ".");
            Array.Clear(_day, 0, _day.Length); _dayUnique = _daySide = 0; _foreignBy.Clear();
            _lineDays = _lineLost = _gameRedrawn = _gameEmptied = _gameUnique = 0;
        }
    }
}
