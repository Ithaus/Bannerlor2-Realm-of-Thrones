using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using Armoury.Villages;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>
    /// Jeff 16.10 (D1, klucz VillageBannerPerHolder): GOSPODARZ KAZDEJ WIOSKI POBOCZNEJ - "herb do jakiego lorda nalezy, nie tylko do jakiego krolestwa ... wioska nalezy
    /// tylko do jednego Pana, ale sa jeszcze rycerze ... w kazdej wiosce gospodarzyl jakis lord, nawet jesli wioska nalezala do krola". Dotad proporczyk i dymek kazdej
    /// wioski okregu mialy herb wlasciciela wsi gry (Village.Bound.OwnerClan - dla rodu panujacego herb krolestwa), a okno okregu jednego rycerza z tytulu BK na caly okreg.
    /// Odtad wioska ma wlasnego gospodarza, wybranego na zywo (raz na dobe na okreg, przy zmianie pana albo jego krolestwa od razu; nic w zapisie):
    ///  (a) MAJATEK BK tej wsi gry: wlasciciel zywy, z rodem z herbem, rod pana albo rod tego samego krolestwa, nie glowa rodu pana (jak R12 - pan nie "trzyma wioski
    ///      dla siebie"); majatki w kolejnosci listy BK na wioski w kolejnosci (order, uid) - jeden majatek na wioske;
    ///  (a2) POSIADACZ TYTULU WSI Z BK (R12, po recenzji D1): gdy jego rodu nie ma jeszcze w (a) - pierwsza wioska bez majatku (najnizszy order); to dokladnie "lord, ktory
    ///      gospodarzyl, choc wies nalezy do krola" - dotad dymek W2 pokazywal go na kazdej wiosce okregu, wiec nie znika z mapy;
    ///  (b) RYCERZ Z LORE wioski (arm_map_village_lore.tsv) z waznym knight_banner - jak R12 w menu: zywy gospodarz z BK (a, a2) przed rycerzem z lore; wiersz lore
    ///      czytany bez klucza VillageTipLore (MapVillageLore.RowOf - herby nie zaleza od klucza dymka);
    ///  (c) RYCERZ KROLESTWA PANA: rody z majatkiem BK w tym albo jednym z 6 najblizszych okregow (wlasciciel jak w (a), rod inny niz pan i niz uzyte w (a) tego okregu),
    ///      w kolejnosci: ten okreg, potem okregi od najblizszego; do tego 3 najblizsze siedziba rody krolestwa pana nie wyzsze od niego (bez rodu panujacego, najemnikow,
    ///      rodow bez glowy i herbu; dla rodu panujacego - kazdy rod krolestwa); wioski bez (a) i (b) po kolei z tej listy (w kolko);
    ///  (d) ROD PANA (jak dotad) - gdy nic wyzej, albo pan bez krolestwa.
    /// Wies gry: posiadacz tytulu wsi z BK (R12 - MapVillageLore.BkHolder), inaczej pan. Po podboju gospodarz z innego krolestwa odpada sam (warunek krolestwa pana
    /// na zywo) i wioska bierze nastepna regule. Proporczyk (MapVillagePennants), dymek (MapVillageTooltip) i "Look over the district" (DistrictMenu) pytaja TEJ SAMEJ
    /// funkcji (Of), wiec pokazuja tego samego gospodarza: imie, rod, "sworn to" pan. Klucz off - herb pana i rycerz z tytulu BK jak dotad (wolajacy nie wola Of).
    /// BK przez odbicie (raz na proces), GetPopData tylko dla osady z danymi BK (IsSettlementPopulated - jak MapVillageLore). Bledy liczone per miejsce, nic nie gasnie.
    /// Po recenzji D1 (koszt): odswiezenie DOBOWE pamieci okregu najwyzej DayRebuildsPerSlice okregow na 100 ms (reszta czyta pamiec poprzedniej doby do nastepnego
    /// odczytu) - inaczej przy zmianie doby kazdy okreg przy kamerze przebudowywal sie w jednym ticku proporczykow (odbicie BK wsi i 6 sasiadow). Pierwsza budowa, zmiana
    /// pana albo krolestwa i gospodarz niewazny (smierc, zmiana strony) - od razu, bez limitu.
    /// Jeff 16.10 (D2, klucz VillageCanonHouses; "dane wez ze swiata gry o tron jesli to mozliwe", "nie wygladalo tak jednolicie"): krok (b) - zamiast rycerza z lore
    /// (wymyslone rody, np. House Weirley) ROD KANONU tej samej krainy (CanonHouses: awoiaf, kraina okregu z kultury w pliku mapy, nie z Settlement.Culture, ktora
    /// zmienia BK). Kolejnosc rodow okregu: (1) siedziba = zamek / miasto okregu albo sama wies gry, (2) 2 najblizsze siedziby do 60 jednostek mapy, (3) rody zaprzysiezone
    /// rodowi pana (liege rodu = rdzen rodu kanonu klanu pana), (4) reszta krainy; (3) i (4) w kolejnosci skrotu (okreg, rod) - inne w kazdym okregu. Wioski okregu bez
    /// (a) i (a2) po kolei z tej listy (w kolko): w jednym okregu sasiednie wioski maja rozne rody, a jeden rod trzyma wioski w kilku okregach obok (jak w kanonie).
    /// Rod z klanem ROT w grze tylko, gdy klan zyje, jest z krolestwa pana, nie jest panem ani rodem panujacym, nie wyzszy od pana (jak NearHousesOf) i nie trzyma
    /// juz majatku BK w tym okregu - wtedy herb tego klanu (Clan.Banner); inaczej herb z pliku (klan ROT - jego banner_key, rod bez klanu - herb zlozony z opisu).
    /// Imie: poczatek imienia rycerza z lore TEJ wioski ("Ser Howard", "Lady Barbrey", "Harwood", "Gorzhak zo") - "Kept by Ser Howard of House Florent, sworn to ...";
    /// wioska bez lore - imie rycerza z lore krainy (skrot uid). Rycerz z lore (stare (b)) tylko w krainie bez zadnego rodu kanonu. Klucz off - jak dotad.
    /// Po recenzji D2 (16.10): (0) rody z siedziba w tym okregu z KAZDEJ krainy (Penrose of Parchments, Seaworth - okregi kultury dragonstone); rod z
    /// punktem (siedziba z pliku, siedziba klanu ROT w grze, osada podkrainy - CanonHouses.RefPos) dalej niz FarRadius (150 = 2.5 x SeatRadius) od wsi gry
    /// nie wchodzi do (3) i (4) - tylko gdy lista bylaby pusta (symulacja startu: 144 z 599 przydzialow z siedziba dalej niz 150, np. Magnar ze Skagos
    /// 626 na Flint Cliffs); rod wyspy (Skagos, Three Sisters, Shield Islands) tylko na swojej wyspie, a okreg wyspy bierze najpierw (i, gdy sa, wylacznie)
    /// rody wyspy; Valid sprawdza klan rodu ta sama regula co CanonClanOk (rod panujacy, wyzszy od pana, najemnik, gracz w ciagu doby - okreg od nowa);
    /// galaz rodu pana (ten sam rdzen) - pelna nazwa ("Ser X of House Harlaw of Grey Garden, sworn to House Harlaw"); jeden rod najwyzej 2 wioski okregu,
    /// rod bez punktu ("z calej krainy") 1 - reszta do rodow krolestwa (c) (symulacja startu: Tagaros 34 -> 15 wiosek Volantis, sasiednie wioski z tym
    /// samym rodem 15 -> 0 par).
    /// </summary>
    internal static class VillageHolders
    {
        internal const int RuleEstate = 0, RuleLore = 1, RuleRealmEstate = 2, RuleRealmHouse = 3, RuleLord = 4, RuleTitle = 5, RuleCanon = 6;
        private static readonly string[] RuleName = { "majatek BK", "rycerz z lore", "rycerz krolestwa z majatku obok", "rod krolestwa", "pan", "tytul BK", "rod kanonu" };
        private const int NearDistricts = 6, NearHouses = 3;
        private const int NearSeats = 2;                            // D2: rody 2 najblizszych siedzib przed rodami zaprzysiezonymi panu
        private const float SeatRadius = 60f;                       // D2: ...ale tylko siedzib do 60 jednostek mapy od wsi gry (p90 najblizszej siedziby; symulacja startu: bez
                                                                    // promienia jeden rod Polnocy (Stout z Goldgrass) bral 22 wioski, z promieniem najwiecej 14 w Westeros)
        private const float FarRadius = 150f;                       // po recenzji D2: rod dalej niz 150 (2.5 x SeatRadius) - tylko gdy okreg nie ma innego rodu kanonu
        private const int MaxPerDistrict = 2, MaxPerDistrictAnywhere = 1;   // po recenzji D2: wiosek jednego rodu kanonu w okregu (rod bez punktu - 1)
        private static readonly HashSet<Clan> NoClans = new HashSet<Clan>();
        private const int DayRebuildsPerSlice = 4;                  // odswiezenia dobowe okregow na odcinek czasu (po recenzji D1 - koszt przy zmianie doby)
        private static readonly long SliceTicks = System.Diagnostics.Stopwatch.Frequency / 10;   // 100 ms
        private static long _sliceStart;
        private static int _sliceRebuilds, _dayRebuildsDeferred;
        private static bool _noBudget;                              // linia startowa (Daily) - caly przeglad bez limitu
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>Gospodarz jednej wioski (albo wsi gry): regula, bohater (null - rycerz z lore albo sam rod), rod, herb, pan (wlasciciel wsi gry), wiersz lore.</summary>
        internal sealed class Holder
        {
            public int Rule;
            public Hero Hero;
            public Clan Clan;              // rod gospodarza (null - rycerz z lore)
            public Banner Banner;          // herb (rod gospodarza, knight_banner z lore, herb pana)
            public Clan Lord;              // pan = Village.Bound.OwnerClan
            public MapVillageLore.Row Lore;
            public CanonHouses.House Canon; // D2: rod kanonu (RuleCanon); Clan = jego klan ROT w grze albo null
            public string Who;             // D2: "Ser Howard of House Florent" (RuleCanon)
        }

        private sealed class DistCache
        {
            public int Day;
            public bool Canon;             // D2: zbudowany z kluczem VillageCanonHouses (zmiana klucza = okreg od nowa)
            public Clan Lord;
            public Kingdom Realm;
            public Holder Main;
            public readonly Dictionary<string, Holder> ByUid = new Dictionary<string, Holder>(StringComparer.Ordinal);
        }

        private static readonly Dictionary<string, DistCache> _cache = new Dictionary<string, DistCache>(StringComparer.Ordinal);
        private static readonly Dictionary<Settlement, List<Hero>> _owners = new Dictionary<Settlement, List<Hero>>();   // wlasciciele majatkow BK wsi (doba)
        private static int _ownersDay = int.MinValue;
        private static List<Settlement> _villages;                                                                     // wsie gry (sesja; polozenie stale)
        private static readonly Dictionary<Settlement, Settlement[]> _near = new Dictionary<Settlement, Settlement[]>();
        private static int _st, _stEstates;
        private static bool _startDone;
        private static readonly HashSet<string> _errSites = new HashSet<string>(StringComparer.Ordinal);

        // BK (odbicie, raz na proces)
        private static bool _bkTried;
        private static PropertyInfo _pCfg, _pPopMgr, _pEstData, _pEstates, _pOwner;
        private static MethodInfo _mPopulated, _mGetPop;

        internal static bool On { get { var s = Settings.Current; return s != null && s.VillageBannerPerHolder; } }

        private static bool CanonOn { get { var s = Settings.Current; return s != null && s.VillageCanonHouses; } }

        /// <summary>Konstruktor ArmouryBehavior (kazda kampania): pamiec okregow, majatkow i sasiadow od nowa (obiekty gry innej kampanii nie zostaja).</summary>
        internal static void Reset()
        {
            _cache.Clear(); _owners.Clear(); _ownersDay = int.MinValue; _villages = null; _near.Clear();
            _st = _stEstates = 0; _startDone = false;
            _sliceStart = 0; _sliceRebuilds = _dayRebuildsDeferred = 0; _noBudget = false;
            lock (_errSites) _errSites.Clear();
            CanonHouses.ResetSession();
        }

        private static void Stumble(string where, Exception e)
        {
            _st++;
            bool first;
            lock (_errSites) first = _errSites.Add(where);
            if (first || _st % 500 == 0) Log.Error("VillageHolders." + where + " (potkniecie " + _st + ")", e);
        }

        private static int Today() { return (int)CampaignTime.Now.ToDays; }

        private static bool BannerOk(Banner b) { return b != null && !string.IsNullOrEmpty(b.BannerCode); }

        private static bool ClanOk(Clan c) { return c != null && !c.IsEliminated && BannerOk(c.Banner); }

        /// <summary>Rod "nasz" dla pana: rod pana albo rod tego samego krolestwa (pan bez krolestwa - tylko jego rod).</summary>
        private static bool Ours(Clan c, Clan lord, Kingdom realm)
        {
            if (c == null || lord == null) return false;
            if (ReferenceEquals(c, lord)) return true;
            return realm != null && ReferenceEquals(c.Kingdom, realm);
        }

        // ------------------------------------------------------------ odczyt (proporczyk, dymek, okno okregu)
        /// <summary>Gospodarz wioski okregu `district` (wies gry) o uid (null = sama wies gry). null - wies bez pana albo blad (wolajacy pokazuje wtedy to, co dotad).</summary>
        internal static Holder Of(Settlement district, string uid)
        {
            if (district == null || district.Village == null || district.StringId == null) return null;
            try
            {
                var c = CacheFor(district);
                if (c == null) return null;
                Holder h = uid == null ? c.Main : null;
                if (h == null && !c.ByUid.TryGetValue(uid, out h)) h = null;
                // gospodarz z pamieci doby, ktory w ciagu doby umarl albo jego rod odszedl z krolestwa pana (zmiana strony) - okreg od nowa (raz; Build daje tylko waznych)
                if (h != null && !Valid(h, c))
                {
                    c = Build(district, c.Lord, c.Realm, Today());
                    _cache[district.StringId] = c;
                    h = uid == null ? c.Main : null;
                    if (h == null && !c.ByUid.TryGetValue(uid, out h)) h = null;
                }
                if (uid == null || h != null) return h;
                // wioska spoza ukladu ksiegi (okreg ROT bez wlasnego Village albo uklad niewczytany): rod pana, jak dotad
                h = new Holder { Rule = RuleLord, Clan = c.Lord, Banner = c.Lord.Banner, Lord = c.Lord, Lore = LoreOf(uid) };
                c.ByUid[uid] = h;
                return h;
            }
            catch (Exception e) { Stumble("Of", e); return null; }
        }

        private static bool Valid(Holder h, DistCache c)
        {
            if (h.Rule == RuleLord || h.Rule == RuleLore) return true;
            // D2: rod kanonu bez klanu w grze - zawsze; z klanem - ta sama regula co przy budowie (CanonClanOk: zyje, nie pan, krolestwo pana, nie rod
            // panujacy, nie wyzszy od pana, nie najemnik, nie rod gracza) - po recenzji D2 takze rod, ktory w ciagu doby zostal rodem panujacym albo najemnikiem
            if (h.Rule == RuleCanon)
            {
                if (h.Clan == null) return true;
                bool ruler = c.Realm != null && ReferenceEquals(c.Realm.RulingClan, c.Lord);
                return c.Lord != null && CanonClanOk(h.Clan, c.Lord, c.Realm, ruler, NoClans);
            }
            if (h.Hero != null && !h.Hero.IsAlive) return false;
            return h.Clan != null && !h.Clan.IsEliminated && (ReferenceEquals(h.Clan, c.Lord) || (c.Realm != null && ReferenceEquals(h.Clan.Kingdom, c.Realm)));
        }

        /// <summary>Kod herbu gospodarza (proporczyk: porownanie raz na sekunde, wiec tylko odczyt pamieci okregu). "" - brak pana / herbu.</summary>
        internal static string Code(Settlement district, string uid)
        {
            var h = Of(district, uid);
            var b = h != null ? h.Banner : null;
            string code = b != null ? b.BannerCode : null;
            return code ?? "";
        }

        private static MapVillageLore.Row LoreOf(string uid)
        {
            try { return uid != null ? MapVillageLore.RowOf(uid) : null; }          // po recenzji D1: bez klucza VillageTipLore (herb rycerza z lore na proporczyku)
            catch (Exception e) { MapVillageLore.CountRowStumble(uid, e); return null; }
        }

        private static DistCache CacheFor(Settlement st)
        {
            Settlement bound = st.Village.Bound;
            Clan lord = bound != null ? bound.OwnerClan : null;
            if (lord == null) return null;
            Kingdom realm = lord.Kingdom;
            int today = Today();
            DistCache c;
            if (_cache.TryGetValue(st.StringId, out c) && ReferenceEquals(c.Lord, lord) && ReferenceEquals(c.Realm, realm) && c.Canon == CanonOn)
            {
                if (c.Day == today) return c;
                // ten sam pan i krolestwo, tylko nowa doba (majatki BK) - najwyzej kilka okregow na 100 ms, reszta z pamieci poprzedniej doby do nastepnego odczytu
                if (!_noBudget && !TakeDayRebuild()) { _dayRebuildsDeferred++; return c; }
            }
            c = Build(st, lord, realm, today);
            _cache[st.StringId] = c;
            return c;
        }

        private static bool TakeDayRebuild()
        {
            long now = System.Diagnostics.Stopwatch.GetTimestamp();
            if (now - _sliceStart >= SliceTicks || now < _sliceStart) { _sliceStart = now; _sliceRebuilds = 0; }
            if (_sliceRebuilds >= DayRebuildsPerSlice) return false;
            _sliceRebuilds++;
            return true;
        }

        // ------------------------------------------------------------ przydzial okregu
        private static DistCache Build(Settlement st, Clan lord, Kingdom realm, int today)
        {
            var c = new DistCache { Day = today, Lord = lord, Realm = realm, Canon = CanonOn };
            // wies gry: tytul wsi z BK (R12), inaczej pan
            Hero t = null;
            try { t = MapVillageLore.BkHolder(st, lord); } catch (Exception e) { Stumble("Build(tytul)", e); }
            c.Main = t != null && ClanOk(t.Clan)
                ? new Holder { Rule = RuleTitle, Hero = t, Clan = t.Clan, Banner = t.Clan.Banner, Lord = lord }
                : new Holder { Rule = RuleLord, Clan = lord, Banner = lord.Banner, Lord = lord };
            var uids = new List<string>();
            if (!VillageBook.DistrictUids(st.StringId, uids) || uids.Count == 0) return c;
            var left = new List<int>(uids.Count);
            var rows = new MapVillageLore.Row[uids.Count];
            for (int i = 0; i < uids.Count; i++) rows[i] = LoreOf(uids[i]);
            // (a) majatki BK tej wsi gry - po kolei na wioski w kolejnosci (order, uid)
            var used = new HashSet<Clan>();
            var own = EligibleOwners(st, lord, realm);
            int k = 0;
            for (int i = 0; i < uids.Count; i++)
            {
                if (k < own.Count)
                {
                    var h = own[k++];
                    c.ByUid[uids[i]] = new Holder { Rule = RuleEstate, Hero = h, Clan = h.Clan, Banner = h.Clan.Banner, Lord = lord, Lore = rows[i] };
                    used.Add(h.Clan);
                    continue;
                }
                left.Add(i);
            }
            // (a2) posiadacz tytulu wsi z BK (R12 - jak menu: tytul przed rycerzem z lore) - pierwsza wioska bez majatku, gdy jego rodu nie ma jeszcze w (a)
            if (c.Main.Rule == RuleTitle && c.Main.Clan != null && left.Count > 0 && !used.Contains(c.Main.Clan))
            {
                int i = left[0];
                left.RemoveAt(0);
                c.ByUid[uids[i]] = new Holder { Rule = RuleTitle, Hero = c.Main.Hero, Clan = c.Main.Clan, Banner = c.Main.Banner, Lord = lord, Lore = rows[i] };
                used.Add(c.Main.Clan);
            }
            // (b) D2: rod kanonu krainy (VillageCanonHouses) - wioski bez (a) i (a2) po kolei z listy rodow okregu (w kolko); rycerz z lore tylko, gdy w krainie
            // nie zostal zaden rod (kraina bez kanonu albo wszystkie odpadly). Po recenzji D2 (Tagaros 37 wiosek Volantis): jeden rod najwyzej MaxPerDistrict (2)
            // wioski okregu, rod "z calej krainy" (bez siedziby, klanu i podkrainy) najwyzej 1 - reszta wiosek do (c) rodow krolestwa (bez wymyslonych rycerzy z lore)
            bool canonDone = false;
            if (c.Canon && left.Count > 0)
            {
                List<CanonHouses.House> pool = null;
                try { pool = CanonPool(st, lord, realm, used); } catch (Exception e) { Stumble("Build(rody kanonu)", e); }
                if (pool != null && pool.Count > 0)
                {
                    var lordHouse = CanonHouses.OfClan(lord);
                    string lordBase = lordHouse != null ? lordHouse.Base : null;
                    var per = new Dictionary<CanonHouses.House, int>();
                    var over = new List<int>();
                    for (int j = 0; j < left.Count; j++)
                    {
                        int i = left[j];
                        var ch = pool[j % pool.Count];
                        Vec2 q;
                        int lim = CanonHouses.RefPos(ch, out q) ? MaxPerDistrict : MaxPerDistrictAnywhere;
                        int had;
                        per.TryGetValue(ch, out had);
                        if (had >= lim) { over.Add(i); continue; }
                        per[ch] = had + 1;
                        Clan live = CanonHouses.LiveClan(ch);
                        c.ByUid[uids[i]] = new Holder
                        {
                            Rule = RuleCanon, Canon = ch, Clan = live, Banner = CanonHouses.BannerOf(ch, live), Lord = lord, Lore = rows[i], Who = WhoOf(uids[i], rows[i], ch, lordBase)
                        };
                    }
                    if (over.Count == 0) return c;
                    left = over;
                    canonDone = true;
                }
            }
            // (b) rycerz z lore z waznym herbem (po rodach kanonu - nie: reszta ponad limit idzie do rodow krolestwa)
            var rest = new List<int>(left.Count);
            foreach (int i in left)
            {
                var r = rows[i];
                if (!canonDone && r != null && r.Knight.Length > 0 && r.KnightBanner != null)
                    c.ByUid[uids[i]] = new Holder { Rule = RuleLore, Banner = r.KnightBanner, Lord = lord, Lore = r };
                else rest.Add(i);
            }
            if (rest.Count == 0) return c;
            // (c) rycerze krolestwa pana: majatki BK reszty tej wsi i 6 najblizszych okregow, potem 3 najblizsze rody krolestwa nie wyzsze od pana
            var cand = new List<KeyValuePair<Hero, int>>();
            var inC = new HashSet<Clan>(used);
            inC.Add(lord);
            for (int j = k; j < own.Count; j++) if (inC.Add(own[j].Clan)) cand.Add(new KeyValuePair<Hero, int>(own[j], RuleRealmEstate));
            foreach (var nb in Near(st))
            {
                foreach (var h in EligibleOwners(nb, lord, realm))
                    if (inC.Add(h.Clan)) cand.Add(new KeyValuePair<Hero, int>(h, RuleRealmEstate));
            }
            foreach (var cl in NearHousesOf(st, lord, realm, inC))
                cand.Add(new KeyValuePair<Hero, int>(cl.Leader, RuleRealmHouse));
            for (int j = 0; j < rest.Count; j++)
            {
                int i = rest[j];
                if (cand.Count > 0)
                {
                    var kv = cand[j % cand.Count];
                    c.ByUid[uids[i]] = new Holder { Rule = kv.Value, Hero = kv.Key, Clan = kv.Key.Clan, Banner = kv.Key.Clan.Banner, Lord = lord, Lore = rows[i] };
                }
                else c.ByUid[uids[i]] = new Holder { Rule = RuleLord, Clan = lord, Banner = lord.Banner, Lord = lord, Lore = rows[i] };   // (d)
            }
            return c;
        }

        /// <summary>Wlasciciele majatkow BK wsi (kolejnosc listy BK), ktorzy moga gospodarzyc dla pana: zywi, z rodem z herbem, rod pana albo jego krolestwa, nie glowa
        /// rodu pana (po recenzji D1, jak R12 w MapVillageLore.BkHolder: inaczej "Kept for them by <sam pan>", u gracza "Kept for you by <gracz>").</summary>
        private static List<Hero> EligibleOwners(Settlement v, Clan lord, Kingdom realm)
        {
            var all = OwnersOf(v);
            var r = new List<Hero>(all.Count);
            Hero head = lord != null ? lord.Leader : null;
            foreach (var h in all) if (h != null && h.IsAlive && !ReferenceEquals(h, head) && ClanOk(h.Clan) && Ours(h.Clan, lord, realm)) r.Add(h);
            return r;
        }

        /// <summary>Wlasciciele majatkow BK wsi gry (pamiec doby; majatek bez wlasciciela - IsDisabled - pominiety). Pusta lista - BK brak albo wies bez danych BK.</summary>
        private static List<Hero> OwnersOf(Settlement v)
        {
            int today = Today();
            if (today != _ownersDay) { _owners.Clear(); _ownersDay = today; }
            List<Hero> list;
            if (_owners.TryGetValue(v, out list)) return list;
            list = new List<Hero>(4);
            _owners[v] = list;
            ResolveBk();
            if (_pCfg == null || _mPopulated == null || _mGetPop == null || _pEstData == null || _pEstates == null) return list;
            try
            {
                object cfg = _pCfg.GetValue(null, null);
                object pm = cfg != null ? _pPopMgr.GetValue(cfg, null) : null;
                if (pm == null || !(bool)_mPopulated.Invoke(pm, new object[] { v })) return list;   // GetPopData osady bez danych inicjuje je w BK (zmiana stanu gry)
                object pd = _mGetPop.Invoke(pm, new object[] { v });
                object ed = pd != null ? _pEstData.GetValue(pd, null) : null;
                var es = ed != null ? _pEstates.GetValue(ed, null) as IList : null;
                if (es == null) return list;
                for (int i = 0; i < es.Count; i++)
                {
                    var e = es[i];
                    if (e == null) continue;
                    if (_pOwner == null || !_pOwner.DeclaringType.IsInstanceOfType(e)) _pOwner = AccessTools.Property(e.GetType(), "Owner");
                    var h = _pOwner != null ? _pOwner.GetValue(e, null) as Hero : null;
                    if (h != null) list.Add(h);
                }
            }
            catch (Exception e) { _stEstates++; Stumble("OwnersOf", e); }
            return list;
        }

        private static void ResolveBk()
        {
            if (_bkTried) return;
            _bkTried = true;
            try
            {
                var cfgT = AccessTools.TypeByName("BannerKings.BannerKingsConfig");
                if (cfgT == null) return;
                _pCfg = AccessTools.Property(cfgT, "Instance");
                _pPopMgr = AccessTools.Property(cfgT, "PopulationManager");
                var pmT = _pPopMgr != null ? _pPopMgr.PropertyType : null;
                _mPopulated = pmT != null ? AccessTools.Method(pmT, "IsSettlementPopulated", new[] { typeof(Settlement) }) : null;
                _mGetPop = pmT != null ? AccessTools.Method(pmT, "GetPopData", new[] { typeof(Settlement) }) : null;
                _pEstData = _mGetPop != null ? AccessTools.Property(_mGetPop.ReturnType, "EstateData") : null;
                _pEstates = _pEstData != null ? AccessTools.Property(_pEstData.PropertyType, "Estates") : null;
                var esT = AccessTools.TypeByName("BannerKings.Managers.Populations.Estates.Estate");
                _pOwner = esT != null ? AccessTools.Property(esT, "Owner") : null;
            }
            catch (Exception e) { Stumble("ResolveBk", e); }
        }

        /// <summary>6 najblizszych innych wsi gry (polozenie stale - pamiec sesji).</summary>
        private static Settlement[] Near(Settlement st)
        {
            Settlement[] r;
            if (_near.TryGetValue(st, out r)) return r;
            if (_villages == null)
            {
                _villages = new List<Settlement>(600);
                var seen = new HashSet<Village>();
                foreach (var s in Settlement.All)
                    if (s != null && s.IsVillage && s.Village != null && seen.Add(s.Village)) _villages.Add(s);
            }
            var p = st.GetPosition2D;
            var best = new List<KeyValuePair<float, Settlement>>(NearDistricts + 1);
            foreach (var s in _villages)
            {
                if (ReferenceEquals(s, st) || ReferenceEquals(s.Village, st.Village)) continue;
                var q = s.GetPosition2D;
                float d = (q.x - p.x) * (q.x - p.x) + (q.y - p.y) * (q.y - p.y);
                if (best.Count == NearDistricts && d >= best[best.Count - 1].Key) continue;
                int at = best.Count;
                while (at > 0 && best[at - 1].Key > d) at--;
                best.Insert(at, new KeyValuePair<float, Settlement>(d, s));
                if (best.Count > NearDistricts) best.RemoveAt(best.Count - 1);
            }
            r = new Settlement[best.Count];
            for (int i = 0; i < best.Count; i++) r[i] = best[i].Value;
            _near[st] = r;
            return r;
        }

        /// <summary>3 najblizsze siedziba rody krolestwa pana nie wyzsze od niego (dla rodu panujacego - kazdy): z glowa, herbem i siedziba, bez rodu panujacego,
        /// najemnikow i rodow juz na liscie. Pan bez krolestwa - brak.</summary>
        private static List<Clan> NearHousesOf(Settlement st, Clan lord, Kingdom realm, HashSet<Clan> skip)
        {
            var r = new List<Clan>(NearHouses);
            if (realm == null) return r;
            bool ruler = ReferenceEquals(realm.RulingClan, lord);
            var p = st.GetPosition2D;
            var best = new List<KeyValuePair<float, Clan>>();
            foreach (var c in realm.Clans)
            {
                if (c == null || skip.Contains(c) || !ClanOk(c) || ReferenceEquals(c, realm.RulingClan)) continue;
                if (c.IsUnderMercenaryService || c.IsClanTypeMercenary || c.Leader == null || !c.Leader.IsAlive || c == Clan.PlayerClan && !ReferenceEquals(lord, Clan.PlayerClan)) continue;
                if (!ruler && c.Tier > lord.Tier) continue;
                var home = c.HomeSettlement;
                if (home == null) continue;
                var q = home.GetPosition2D;
                best.Add(new KeyValuePair<float, Clan>((q.x - p.x) * (q.x - p.x) + (q.y - p.y) * (q.y - p.y), c));
            }
            best.Sort((x, y) => { int k = x.Key.CompareTo(y.Key); return k != 0 ? k : string.CompareOrdinal(x.Value.StringId, y.Value.StringId); });
            for (int i = 0; i < best.Count && r.Count < NearHouses; i++) r.Add(best[i].Value);
            return r;
        }

        // ------------------------------------------------------------ D2: rody kanonu
        /// <summary>Rody kanonu dla wiosek okregu, w kolejnosci przydzialu: (0) siedziba w tym okregu (zamek / miasto okregu albo sama wies gry) z kazdej
        /// krainy, (1) okreg wyspy - rody tej wyspy (skrot), (2) 2 najblizsze siedziby (do 60), (3) rody zaprzysiezone rodowi pana (skrot okreg+rod), (4) reszta
        /// krainy (skrot); w (3) i (4) tylko rody bez punktu albo z punktem do FarRadius (150) - dalsze i rody innej wyspy tylko, gdy lista bylaby pusta
        /// (najblizsze najpierw). Okreg wyspy z rodami wyspy - tylko one (i siedziba w okregu). Bez rodu pana, rodow z klanem ROT spoza krolestwa pana /
        /// wyeliminowanym / panujacym / wyzszym od pana / juz z majatkiem BK w okregu i bez waznego herbu. Pusta lista - rycerz z lore jak dotad.</summary>
        private static List<CanonHouses.House> CanonPool(Settlement st, Clan lord, Kingdom realm, HashSet<Clan> used)
        {
            var pool = new List<CanonHouses.House>();
            var list = CanonHouses.InArea(CanonHouses.AreaOf(st.StringId));
            Settlement bound = st.Village != null ? st.Village.Bound : null;
            string boundId = bound != null ? bound.StringId : null;
            var atHere = CanonHouses.SeatedAt(st.StringId);
            var atBound = CanonHouses.SeatedAt(boundId);
            if ((list == null || list.Count == 0) && atHere.Count == 0 && atBound.Count == 0) return pool;
            var lordHouse = CanonHouses.OfClan(lord);
            string lordBase = lordHouse != null ? lordHouse.Base : null;
            bool ruler = realm != null && ReferenceEquals(realm.RulingClan, lord);
            string isle = CanonHouses.SubOf(st.StringId);
            var p = st.GetPosition2D;
            var here = new List<CanonHouses.House>();
            var island = new List<KeyValuePair<uint, CanonHouses.House>>();
            var seated = new List<KeyValuePair<float, CanonHouses.House>>();
            var sworn = new List<KeyValuePair<uint, CanonHouses.House>>();
            var rest = new List<KeyValuePair<uint, CanonHouses.House>>();
            var far = new List<KeyValuePair<float, CanonHouses.House>>();
            var taken = new HashSet<CanonHouses.House>();
            // (0) siedziba w okregu - z kazdej krainy (rod Stormlands z siedziba w okregu kultury dragonstone)
            foreach (var h in atHere) if (!taken.Contains(h) && CanonUsable(h, lordHouse, lord, realm, ruler, used)) { here.Add(h); taken.Add(h); }
            foreach (var h in atBound) if (!taken.Contains(h) && CanonUsable(h, lordHouse, lord, realm, ruler, used)) { here.Add(h); taken.Add(h); }
            if (list != null)
            {
                foreach (var h in list)
                {
                    if (taken.Contains(h) || !CanonUsable(h, lordHouse, lord, realm, ruler, used)) continue;
                    Vec2 q;
                    bool hasRef = CanonHouses.RefPos(h, out q);
                    float d2 = hasRef ? (q.x - p.x) * (q.x - p.x) + (q.y - p.y) * (q.y - p.y) : float.MaxValue;
                    if (h.Sub.Length > 0)
                    {
                        // rod wyspy: na swojej wyspie najpierw, poza nia tylko jako ostatni zapas
                        if (string.Equals(h.Sub, isle, StringComparison.Ordinal)) island.Add(new KeyValuePair<uint, CanonHouses.House>(Fnv(st.StringId, h.Key), h));
                        else far.Add(new KeyValuePair<float, CanonHouses.House>(d2, h));
                        continue;
                    }
                    if (hasRef && d2 > FarRadius * FarRadius) { far.Add(new KeyValuePair<float, CanonHouses.House>(d2, h)); continue; }
                    Vec2 s;
                    if (CanonHouses.SeatPos(h, out s))
                    {
                        float s2 = (s.x - p.x) * (s.x - p.x) + (s.y - p.y) * (s.y - p.y);
                        if (s2 <= SeatRadius * SeatRadius) seated.Add(new KeyValuePair<float, CanonHouses.House>(s2, h));
                    }
                    var kv = new KeyValuePair<uint, CanonHouses.House>(Fnv(st.StringId, h.Key), h);
                    if (lordBase != null && string.Equals(h.LiegeBase, lordBase, StringComparison.Ordinal)) sworn.Add(kv);
                    else rest.Add(kv);
                }
            }
            here.Sort((x, y) => string.CompareOrdinal(x.Key, y.Key));
            pool.AddRange(here);
            Comparison<KeyValuePair<uint, CanonHouses.House>> byHash = (x, y) => { int r = x.Key.CompareTo(y.Key); return r != 0 ? r : string.CompareOrdinal(x.Value.Key, y.Value.Key); };
            Comparison<KeyValuePair<float, CanonHouses.House>> byDist = (x, y) => { int r = x.Key.CompareTo(y.Key); return r != 0 ? r : string.CompareOrdinal(x.Value.Key, y.Value.Key); };
            island.Sort(byHash);
            foreach (var kv in island) if (taken.Add(kv.Value)) pool.Add(kv.Value);
            if (isle.Length == 0 || island.Count == 0)
            {
                seated.Sort(byDist);
                for (int i = 0, n = 0; i < seated.Count && n < NearSeats; i++) if (taken.Add(seated[i].Value)) { pool.Add(seated[i].Value); n++; }
                sworn.Sort(byHash);
                rest.Sort(byHash);
                foreach (var kv in sworn) if (taken.Add(kv.Value)) pool.Add(kv.Value);
                foreach (var kv in rest) if (taken.Add(kv.Value)) pool.Add(kv.Value);
            }
            if (pool.Count == 0)
            {
                far.Sort(byDist);
                foreach (var kv in far) if (taken.Add(kv.Value)) pool.Add(kv.Value);
            }
            return pool;
        }

        /// <summary>Rod kanonu moze gospodarzyc wioska tego pana: nie rod pana, klan ROT (jesli jest w grze) przechodzi CanonClanOk, wazny herb.</summary>
        private static bool CanonUsable(CanonHouses.House h, CanonHouses.House lordHouse, Clan lord, Kingdom realm, bool ruler, HashSet<Clan> used)
        {
            if (h == null || ReferenceEquals(h, lordHouse)) return false;
            Clan live = CanonHouses.LiveClan(h);
            if (live != null && !CanonClanOk(live, lord, realm, ruler, used)) return false;
            return CanonHouses.BannerOf(h, live) != null;
        }

        /// <summary>Klan ROT rodu kanonu moze gospodarzyc wioska pana: zyje, nie jest panem ani rodem panujacym, jest z krolestwa pana (pan bez krolestwa - nikt), nie wyzszy
        /// od pana (pan panujacy - kazdy), nie najemnik, nie rod gracza (chyba ze wioska gracza), bez majatku BK w tym okregu, z herbem.</summary>
        private static bool CanonClanOk(Clan c, Clan lord, Kingdom realm, bool ruler, HashSet<Clan> used)
        {
            if (c.IsEliminated || ReferenceEquals(c, lord) || used.Contains(c) || !ClanOk(c)) return false;
            if (!Ours(c, lord, realm)) return false;
            if (realm != null && ReferenceEquals(c, realm.RulingClan)) return false;
            if (!ruler && c.Tier > lord.Tier) return false;
            if (c.IsUnderMercenaryService || c.IsClanTypeMercenary) return false;
            if (ReferenceEquals(c, Clan.PlayerClan) && !ReferenceEquals(lord, Clan.PlayerClan)) return false;
            return true;
        }

        /// <summary>"Ser Howard of House Florent": poczatek imienia rycerza z lore tej wioski, inaczej imie rycerza z lore krainy (skrot uid+rod); Ghiscari "Gorzhak zo Loraq".
        /// Bez imienia - sama nazwa rodu. Po recenzji D2: galaz rodu pana (rdzen = rdzen rodu pana, np. Harlaw of Grey Garden u Harlawow z Ten Towers) - pelna
        /// nazwa galezi ("of House Harlaw of Grey Garden, sworn to House Harlaw"), nie sam rdzen.</summary>
        private static string WhoOf(string uid, MapVillageLore.Row row, CanonHouses.House h, string lordBase)
        {
            string start = row != null ? NameStart(row.Knight) : null;
            if (string.IsNullOrEmpty(start))
            {
                var names = CanonHouses.NamesOf(h.Area);
                if (names.Length > 0) start = names[(int)(Fnv(uid ?? "", h.Key) % (uint)names.Length)];
            }
            string house = h.Base.Length > 0 && !string.Equals(h.Base, lordBase, StringComparison.Ordinal) ? h.Base : h.Name;
            if (string.IsNullOrEmpty(start)) return house;
            if (start.EndsWith(" zo", StringComparison.Ordinal))
            {
                string core = house.StartsWith("House ", StringComparison.Ordinal) ? house.Substring(6) : house;
                return start + " " + core;
            }
            return VillageTexts.Make(VillageTexts.VilTipHolderOf, "HERO", start, "HOUSE", house).ToString();
        }

        /// <summary>"Ser Elwood Largent of Crossfield" -> "Ser Elwood"; "Gorzhak zo Gheghaz of Galamash" -> "Gorzhak zo"; "Harwood Icewood of Far Holt" -> "Harwood"
        /// (ta sama zasada co name_start w tools/herby/herby_kanon.py).</summary>
        internal static string NameStart(string knight)
        {
            if (string.IsNullOrEmpty(knight)) return null;
            string[] w = knight.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            int k = 0;
            string pre = "";
            if (w.Length > 0 && (w[0] == "Ser" || w[0] == "Lady" || w[0] == "Lord" || w[0] == "Maester" || w[0] == "Magister")) { pre = w[0] + " "; k = 1; }
            if (k >= w.Length) return null;
            string first = w[k];
            if (k + 1 < w.Length && w[k + 1] == "zo") first += " zo";
            return pre + first;
        }

        /// <summary>FNV-1a 32 z dwoch napisow (a + '|' + b) bez alokacji - stala kolejnosc rodow w okregu, inna w kazdym okregu.</summary>
        private static uint Fnv(string a, string b)
        {
            uint h = 2166136261u;
            for (int i = 0; i < a.Length; i++) { h ^= a[i]; h *= 16777619u; }
            h ^= '|'; h *= 16777619u;
            for (int i = 0; i < b.Length; i++) { h ^= b[i]; h *= 16777619u; }
            return h;
        }

        // ------------------------------------------------------------ tekst (dymek i okno okregu - jedna funkcja)
        /// <summary>Linia gospodarza: rod inny niz pan - "Kept by Ser X of House Y, sworn to House Z" (tribute z lore - "... takes its tribute, sworn to ..."), wioska rodu
        /// gracza - "Kept for you by ..."; gospodarz z rodu pana - "Kept for them by X"; sam pan (d) - kasztelan z lore jak dotad ("Kept for them by Ser X" bez herbu)
        /// albo null.</summary>
        internal static string Line(Holder h)
        {
            if (h == null) return null;
            Clan lord = h.Lord;
            bool own = lord != null && ReferenceEquals(lord, Clan.PlayerClan);
            var row = h.Lore;
            if (h.Rule == RuleLord) return MapVillageLore.KnightLine(row, lord, null);
            string who;
            bool sameHouse;
            if (h.Rule == RuleLore)
            {
                if (row == null || row.Knight.Length == 0) return null;
                who = WithHouse(row.Knight, row.House);
                sameHouse = false;
            }
            else if (h.Rule == RuleCanon)
            {
                // D2: "Ser Howard of House Florent" (WhoOf) - rod pana nie trafia do listy; galaz rodu pana z pelna nazwa (WhoOf), wiec tez "sworn to"
                if (string.IsNullOrEmpty(h.Who)) return null;
                who = h.Who;
                sameHouse = false;
            }
            else
            {
                if (h.Hero == null || h.Hero.Name == null) return null;
                who = h.Hero.Name.ToString();
                sameHouse = ReferenceEquals(h.Clan, lord);
                if (!sameHouse && h.Clan != null) who = WithHouse(who, MapVillagesView.HouseName(h.Clan));
            }
            bool tribute = (h.Rule == RuleLore || h.Rule == RuleTitle || h.Rule == RuleCanon) && row != null && row.Tribute;   // posiadacz tytulu i rod kanonu - forma z lore wioski (KnightLine)
            if (own) return VillageTexts.Make(tribute ? VillageTexts.VilTipTributeOwn : VillageTexts.VilTipKeptOwn, "KNIGHT", who).ToString();
            if (sameHouse || lord == null) return VillageTexts.Make(VillageTexts.VilTipKept, "KNIGHT", who).ToString();
            return VillageTexts.Make(tribute ? VillageTexts.VilTipTributeSworn : VillageTexts.VilTipKeptSworn, "KNIGHT", who, "LORD", MapVillagesView.HouseName(lord)).ToString();
        }

        /// <summary>"Ser Elwood Largent of Crossfield" + "House Largent" -> bez zmian (rod juz w imieniu); "Jorah" + "House Mormont" -> "Jorah of House Mormont".</summary>
        private static string WithHouse(string who, string house)
        {
            if (string.IsNullOrEmpty(house)) return who;
            string core = house.Trim();
            if (core.StartsWith("House ", StringComparison.OrdinalIgnoreCase)) core = core.Substring(6).Trim();
            if (core.StartsWith("the ", StringComparison.OrdinalIgnoreCase)) core = core.Substring(4).Trim();
            if (core.Length > 0 && who.IndexOf(core, StringComparison.OrdinalIgnoreCase) >= 0) return who;
            return VillageTexts.Make(VillageTexts.VilTipHolderOf, "HERO", who, "HOUSE", house.Trim()).ToString();
        }

        // ------------------------------------------------------------ log
        private static string AreaText(Dictionary<string, int> m)
        {
            if (m.Count == 0) return "-";
            var keys = new List<string>(m.Keys);
            keys.Sort(StringComparer.Ordinal);
            var sb = new StringBuilder();
            foreach (var k in keys) sb.Append(sb.Length > 0 ? ", " : "").Append(k).Append(' ').Append(m[k]);
            return sb.ToString();
        }

        /// <summary>ArmouryBehavior.OnDailyTick (po DistrictMenu.Daily): w pierwszej dobie sesji przeglad calej mapy - ile wiosek dostaje gospodarza z ktorej reguly,
        /// ile roznych herbow (dotad: herb pana okregu) i przyklady. Raz na sesje, ok. 570 okregow (odczyt majatkow BK z pamieci doby).</summary>
        internal static void Daily()
        {
            if (_startDone) return;
            _startDone = true;
            _noBudget = true;                               // przeglad calej mapy raz na sesje - bez limitu odswiezen dobowych
            try
            {
                var s = Settings.Current;
                if (s == null || !s.VillageBannerPerHolder) { Log.Info("Herby wiosek (D1): start | WYLACZONE w MCM (Village Banner Per Holder) - herb pana okregu na kazdej wiosce, jak dotad."); return; }
                var t0 = System.Diagnostics.Stopwatch.StartNew();
                var rules = new int[RuleName.Length];
                int dists = 0, noLord = 0, villages = 0, mainTitle = 0, mainLord = 0;
                var codes = new HashSet<string>(StringComparer.Ordinal);
                var lordCodes = new HashSet<string>(StringComparer.Ordinal);
                var samples = new List<string>();
                var seenRule = new HashSet<int>();
                var uids = new List<string>();
                var seen = new HashSet<Village>();
                // D2: rody kanonu - wioski, rozne rody, zrodlo herbu; dwa proporczyki (gospodarz inny niz pan)
                int cnLive = 0, cnRotFile = 0, cnGen = 0, cnGenCcs = 0, twoPen = 0, loreNoCanon = 0;
                var cnHouses = new HashSet<string>(StringComparer.Ordinal);
                var cnAreas = new Dictionary<string, int>(StringComparer.Ordinal);
                var cnSampleAreas = new HashSet<string>(StringComparer.Ordinal);
                int cnSamples = 0;
                foreach (var st in Settlement.All)
                {
                    if (st == null || !st.IsVillage || st.Village == null || !seen.Add(st.Village)) continue;
                    dists++;
                    var main = Of(st, null);
                    if (main == null) { noLord++; continue; }
                    if (main.Rule == RuleTitle) mainTitle++; else mainLord++;
                    uids.Clear();
                    if (!VillageBook.DistrictUids(st.StringId, uids)) continue;
                    if (BannerOk(main.Lord.Banner) && uids.Count > 0) lordCodes.Add(main.Lord.Banner.BannerCode);
                    string lordCode = BannerOk(main.Lord.Banner) ? main.Lord.Banner.BannerCode : null;
                    foreach (var u in uids)
                    {
                        var h = Of(st, u);
                        if (h == null) continue;
                        villages++;
                        rules[h.Rule]++;
                        if (BannerOk(h.Banner)) codes.Add(h.Banner.BannerCode);
                        if (lordCode != null && BannerOk(h.Banner) && !string.Equals(h.Banner.BannerCode, lordCode, StringComparison.Ordinal)) twoPen++;
                        if (h.Rule == RuleLore && s.VillageCanonHouses) loreNoCanon++;
                        if (h.Rule == RuleCanon && h.Canon != null)
                        {
                            cnHouses.Add(h.Canon.Key);
                            int na;
                            cnAreas.TryGetValue(h.Canon.Area, out na);
                            cnAreas[h.Canon.Area] = na + 1;
                            if (h.Clan != null) cnLive++;
                            else if (h.Canon.Rot) cnRotFile++;
                            else if (h.Canon.UsedCcs) cnGenCcs++;   // po recenzji D2: wariant CCs naprawde w uzyciu (zly wariant CCs = zwykly kod)
                            else cnGen++;
                        }
                        bool canonSample = h.Rule == RuleCanon && h.Canon != null && cnSamples < 6 && cnSampleAreas.Add(h.Canon.Area);
                        if (samples.Count < 8 && seenRule.Add(h.Rule) || samples.Count < 4 || canonSample)
                        {
                            if (canonSample) cnSamples++;
                            string line = Line(h);
                            samples.Add((st.Name != null ? st.Name.ToString() : st.StringId) + " [" + RuleName[h.Rule] + (h.Canon != null ? ", " + h.Canon.Key + " (" + h.Canon.Source + ")" : "") + "]: "
                                        + (line ?? "-") + " (pan " + MapVillagesView.HouseName(main.Lord) + ")");
                        }
                    }
                }
                var sb = new StringBuilder(1200);
                sb.Append("Herby wiosek (D1): start | wiosek pobocznych ").Append(villages).Append(" w ").Append(dists).Append(" okregach (bez pana ").Append(noLord).Append(')')
                  .Append(": ").Append(RuleName[RuleEstate]).Append(' ').Append(rules[RuleEstate]).Append(", ").Append(RuleName[RuleLore]).Append(' ').Append(rules[RuleLore])
                  .Append(", ").Append(RuleName[RuleRealmEstate]).Append(' ').Append(rules[RuleRealmEstate]).Append(", ").Append(RuleName[RuleRealmHouse]).Append(' ').Append(rules[RuleRealmHouse])
                  .Append(", ").Append(RuleName[RuleLord]).Append(' ').Append(rules[RuleLord])
                  .Append(", ").Append(RuleName[RuleTitle]).Append(' ').Append(rules[RuleTitle]).Append(" (posiadacz tytulu wsi na pierwszej wiosce bez majatku)")
                  .Append(", ").Append(RuleName[RuleCanon]).Append(' ').Append(rules[RuleCanon])
                  .Append(" | rody kanonu (D2, VillageCanonHouses ").Append(s.VillageCanonHouses ? "tak" : "NIE - rycerze z lore jak dotad").Append("): wiosek ").Append(rules[RuleCanon])
                  .Append(" z ").Append(cnHouses.Count).Append(" roznych rodow (herb zywego klanu ROT ").Append(cnLive).Append(", herb ROT z pliku ").Append(cnRotFile)
                  .Append(", herb zlozony z opisu ").Append(cnGen).Append(", w tym z ikonami CCsBanners ").Append(cnGenCcs).Append("), rycerz z lore w krainach bez rodow ").Append(loreNoCanon)
                  .Append(", krainy: ").Append(AreaText(cnAreas)).Append(" | ").Append(CanonHouses.StatusText(s.VillageCanonHouses))
                  .Append(" | dwa proporczyki (VillageTwoPennants ").Append(s.VillageTwoPennants ? "tak" : "NIE").Append("): wiosek z gospodarzem innym niz pan ").Append(twoPen)
                  .Append(" z ").Append(villages)
                  .Append(" | roznych herbow na wioskach ").Append(codes.Count).Append(" (dotad herb pana okregu: ").Append(lordCodes.Count).Append(')')
                  .Append(" | wsie gry: tytul BK ").Append(mainTitle).Append(", pan ").Append(mainLord)
                  .Append(" | potkniecia ").Append(_st).Append(" (majatki BK ").Append(_stEstates).Append(") | odswiezen dobowych odlozonych (limit ").Append(DayRebuildsPerSlice)
                  .Append(" na 100 ms) ").Append(_dayRebuildsDeferred).Append(" | czas ").Append(t0.Elapsed.TotalMilliseconds.ToString("0.0", Inv)).Append(" ms")
                  .Append(" | przyklady: ").Append(samples.Count > 0 ? string.Join(" | ", samples.ToArray()) : "-").Append('.');
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Stumble("Daily", e); }
            finally { _noBudget = false; }
        }
    }
}
