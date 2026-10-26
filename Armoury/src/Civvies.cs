using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// m117-ubranie - WLASNE UBRANIE GOLYCH (Jeff 24.10 ok. 23:05, po zrzucie bitwy pod Riverrun z polnagimi zolnierzami: "jak nie maja to wiadomo
    /// ze nikt nagi nie chodzi to niech dostana ubranie cywilne najtansze buty tunike ... chociaz niech maja danej kultury przecie nie chodza
    /// w gaciach"). Regula "tylko to, co ma" (sklad7b) zostawia slot pusty, gdy zbrojownia partii nie ma sztuki - pod Riverrun 19 ludzi Sylwy
    /// Paege bez pancerza tulowia (Mends "93 slotow PUSTYCH ... BodyArmor 19") = gole torsy. Teraz czlowiek objety ta regula, ktoremu zbrojownia
    /// nie dala tulowia albo nog, a wzorzec jednostki ma tam sztuke (Dothrakowie i dzicy z gola klata we wzorcu zostaja jak sa), idzie
    /// w cywilnej tunice / butach swojej kultury (GarrisonKit.CivilDress, z OwnKitSpawn - po wszystkich strazach).
    /// To jego ubranie z domu, POZA gospodarka zbrojowni: sztuka tymczasowa DTE (Assignment.MarkSlotAsTemporary) - spawn jej nie zdejmuje
    /// (CreateEquipmentForArmoryConsumption czysci sloty tymczasowe), zwrot po bitwie i lup z ciala jej nie oddaja (ProcessAgentEquipment-
    /// RespectingTemporarySlots), autorozstrzygniecie jej nie zna (bez spawnu). Jedyny kanal, ktory czyta sprzet agenta bez filtra tymczasowych -
    /// DTE AgentExtension.GetAgentArmors (czesc rozbita ciosem i "dodatkowo peknieta"; stad BattlefieldLaw.QueueWreck bierze wraki na ekran
    /// lupu) - dostaje liste bez ubrania (ArmorsPostfix); QueueWreck ma zapas (IsWorn). Bez tej latki nikogo nie ubieramy (ArmorsHooked).
    ///
    /// WYBOR v2 (Jeff 25.10 ok. 00:25: "ubrania maja byc rozne aby wszyscy nie wygladali tak samo i maja to byc ubrania cywilne zadne zbroje za 33
    /// pancerza no tier 1 jednostek jak laza recruit kazdej kultury"). v1 dawal kazdemu tej kultury te sama najtansza sztuke, a najtansze tuniki
    /// kultur ROT to *_garb (12d, w grze body 33 po skalowaniu pancerzy - ochrona przeszywanicy). Leniwie, raz na kampanie (Reset):
    /// - OCHRONA SZTUKI (v3, recenzja 25.10) = glowa + tulow + rece + nogi tej sztuki (Total), nie sam slot: v2 liczyl tylko ochrone slotu i przepuszczal
    ///   sztuki, ktore lacznie chronia bardziej niz odrzucony garb (33/11/11 = 55), np. "Mariner Gambeson" 27/30/27 = 84, "Woodland Garments" 20/14/22 = 56.
    /// - SITO Fits: przeszywanice i zbroje z nazwy (ArmourWords w id albo nazwie: gambeson, padded, armor, mail... - heurystyka jak ubrania kobiece) odpadaja
    ///   (Jeff: "zadne zbroje"); unikaty tylko wedlug spisu (RotUniques) i strazy CrashScribe - bez NotMerchandise (ubrania z szablonow ludzi bywaja poza
    ///   handlem: andal_civ_boots/2 Westerosu); NotMerchandise sprawdzaja tylko zapasy (c)/(d).
    /// - GRANICA z danych gry, bez nowej stalej, dla slotu MNIEJSZA z dwoch (Jeff: "ubrania cywilne" i "jak laza recruit"):
    ///   (A) "nie mocniej niz chlop" - najwyzsza ochrona sztuki z flaga Civilian, ktora nosi Villager kultur glownych (bez umarlych), przez sito, i ktorej
    ///       NIE ma w BattleEquipments zaden zolnierz (Occupation.Soldier, nie bohater, nie szablon, dowolny tier). Bez Townsman (v3): szaty mieszczan
    ///       Westerosu andal_robe1-6 (26-30 na tulowiu, 42-46 lacznie, 3 kg) dawaly granice tuz pod garbem; pobor idzie ze wsi.
    ///   (B) "nie mocniej niz rekrut" - najwyzsza ochrona sztuki, ktora w tym slocie nosza w bitwie rekruci tieru 1 kultur glownych (Occupation.Soldier),
    ///       przez sito. Bez rekrutow w danych - sama (A).
    ///   ROT 8.1.8 (symulacja 25.10 na XML + items-dump): tulow A 31 (battania_civil_b "Villager Leather Tunic" 13/9/9), B 55 (garb) -> 31; nogi A 35
    ///   (wrapped_shoes), B 32 (leather_cavalier_boots rekrutow Braavos) -> 32. Odcina garby 55, szaty andal_robe 42-46, przeszywanice, wrapped_shoes i
    ///   battania_fur_boots 35. Bandyci sie tu nie licza (looter nosi wrapped_shoes - granica nog spadlaby do 17 i odcielaby buty rekrutow 18).
    ///   Granica 0 = slot bez ubrania (nigdy bez granicy).
    /// - PULA kultury, osobno tulow i nogi: to, co nosza jej jednostki TIERU 1 (Occupation Soldier albo Bandit, nie bohater, nie szablon, CharacterObject.Tier
    ///   == 1; zestawy Battle i Civilian; bez takich - Culture.BasicTroop), RAZEM z tym, co nosza jej Villager i Townsman (ROT czesci kultur daje cywilow
    ///   innej: Essos - Braavos, Ghis i Sarnor - Dorne, Skagos - Zelazne Wyspy, Yi Ti - Dothrakowie). Tylko sztuki z flaga Civilian, w granicy, przez
    ///   sito Fits. W 27 z 30 kultur glownych ROT rekruci maja na tulowiu tylko sztuki ponad granica (*_garb), wiec tulow idzie zwykle od cywilow.
    /// - Przy wystawieniu: LOSOWO jedna z puli (tunika i buty osobno) sposrod tych, ktore czlowiek udzwignie (ItemReq.Meets - Atletyka; kobieta - bez
    ///   NotUsableByFemale). Pula pusta albo nic w Atletyce: (c) neutralne z flaga, losowo; (d) dowolne z flaga, najtansza - oba w granicy i w handlu;
    ///   zadna - slot pusty (licznik linii bitwy).
    ///
    /// m160 BARWY ZIEMI (Jeff 26.10, po bitwie: "strasznie maja spotkate te koszule, to ludzie z pola ze wsi powinni miec kolory ziemi szare, brazowe
    /// a nie czerwone" i "trudno beda mniej zroznicowani ale nie dawaj kolorow szlachetnych w sredniowieczu chlop nie chodzil w czerwonej czy innej
    /// zlotej koszuli"). Dwie przyczyny czerwieni (CHANGELOG 160): (1) sztuki z flaga UseTeamColor (prawie wszystkie tuniki chlopskie gry: sackcloth_tunic,
    /// long_woolen_tunic, cloth_tunic, northern_tunic...) gra barwi na Team.Color / Color2 strony (Mission.SpawnTroop: ClothingColor1(agentTeam.Color)) -
    /// Lannisterowie = karmazyn i zloto; (2) ubrania ROT bez tej flagi sa farbowane w teksturze (andal_clothes2 czerwona, 3 zielona z czerwonym pasem, 8 zolta,
    /// fm_clothes1-5/7/8 zielone, niebieskie, sliwkowe, rhoynar_clothes1-4 czerwone i ze zlota lamowka). Teraz TULOW tylko z tabeli barw ziemi (Tables -
    /// kazda sztuka sprawdzona na teksturze rozproszenia z paczek gry), wedlug kultury (CultureTable; reszta - ogolna), przy wystawieniu losowo to, co
    /// udzwignie, potem ogolna, potem najprostsza (wymog 1); nigdy zapasy (c)/(d) - tam kolorowe. Sztuka tabeli przechodzi to samo sito, granice i flage
    /// Civilian co dotad, plus straz barwy (ColourOk). NOGI - jak dotad z puli kultury i zapasow, ale bez sztuk z barwami rodu i farbowanych (ColourOk:
    /// UseTeamColor, slowo barwy albo stroju szlachty w id / nazwie, spis Dyed z tekstur - rhoynar_civ_boots z czerwonymi owijaczami).
    /// </summary>
    internal static class Civvies
    {
        private static readonly ItemObject[] None = new ItemObject[0];
        private static readonly EquipmentIndex[] Slots = { EquipmentIndex.Body, EquipmentIndex.Leg };

        /// <summary>Pula jednej kultury: [0] tulow, [1] nogi.</summary>
        private sealed class Pool
        {
            internal readonly ItemObject[][] Items = { None, None };
            internal readonly int[] Rec = new int[2], Civ = new int[2], Max = new int[2], RecOut = new int[2];
            internal readonly List<string>[] Over = { new List<string>(), new List<string>() };   // sztuki rekrutow ponad granica ("id ochrona")
            internal readonly int[] ColOut = new int[2];                                           // m160: odrzucone kolorowe / z barwami rodu (nogi)
            internal string Src;                                                                   // skad pula (do logu)
            internal string Table;                                                                 // m160: tabela barw ziemi tulowia (do logu)
        }

        private static readonly Dictionary<string, Pool> _pool = new Dictionary<string, Pool>();   // klucz - id kultury
        private static readonly List<string> _order = new List<string>();                           // kolejnosc kultur w logu
        private static readonly ItemObject[][] _neutral = { None, None }, _any = { None, None };
        private static readonly int[] _cap = new int[2];
        private static readonly string[] _capBy = new string[2];
        // skladniki granicy (v3): (A) chlopi - Villager, (B) rekruci tieru 1 - do logu
        private static readonly int[] _capV = new int[2], _capR = new int[2];
        private static readonly string[] _capVBy = new string[2], _capRBy = new string[2];
        // werdykt sita na sztuke i slot - kazda sztuka oceniana (i liczona w odrzuconych) raz; tylko na czas Build
        private static readonly Dictionary<ItemObject, bool>[] _judged = { new Dictionary<ItemObject, bool>(), new Dictionary<ItemObject, bool>() };
        private static bool _built;
        private static int _cutFemale, _cutTier, _cutBarred, _cutArmour;
        private static readonly int[] _cutCap = new int[2];

        // ------------------------------------------------------------ m160: barwy ziemi
        // Tabela tulowia. Kazda sztuka sprawdzona na teksturze rozproszenia z paczek gry (material siatki z ItemObject mesh, slot 0; CHANGELOG 160 - plik:linia
        // i srednia barwa ubrania): bez flagi UseTeamColor, barwa szara, brazowa albo niebielony len. Dluzsza lista = wiecej roznorodnosci, ale Jeff 26.10:
        // "trudno beda mniej zroznicowani".
        private const string TAndal = "andal", TFirstMen = "first men", TBandit = "bandits", TGeneric = "generic", TPlain = "plainest";
        private static readonly Dictionary<string, string[]> Tables = new Dictionary<string, string[]>
        {
            // Westeros poludniowy: andal_clothes4 len niebielony (#645e55), andal_clothes5 szara (#413d3a), andal_clothes6 jasnoszara (#827f7c),
            // nord_poor_civil "Old Linen Tunic" (#715e50); andal_clothes1/2/3/7/8 farbowane (turkus, czerwien, zielen z czerwonym pasem, zielen, zolc)
            { TAndal, new[] { "andal_clothes4", "andal_clothes5", "andal_clothes6", "nord_poor_civil" } },
            // Polnoc i Pierwsi Ludzie: fm_clothes6 brazowa dluga tunika (#382d20) - jedyna z fm_clothes1-8 bez farby
            { TFirstMen, new[] { "fm_clothes6", "andal_clothes5", "nord_poor_civil" } },
            // bandyci: tattered_rags szare lachmany (#57524b)
            { TBandit, new[] { "tattered_rags", "nord_poor_civil", "fm_clothes6" } },
            // reszta (Zelazne Wyspy, Dorne, Essos, kultury innych modow): wlasne ubrania Dorne / Essos w ROT farbowane albo z barwami rodu - len i szarosc
            { TGeneric, new[] { "nord_poor_civil", "andal_clothes4", "andal_clothes5", "fm_clothes6" } },
            // ostatni zapas (nic z tabeli w Atletyce albo sztuk brak w danych): najprostsze, wymog Atletyki 1
            { TPlain, new[] { "fm_clothes6", "tattered_rags" } },
        };
        private static readonly Dictionary<string, string> CultureTable = new Dictionary<string, string>
        {
            // ROT: vlandia = Westerlands, battania = Polnoc, sturgia = Zelazne Wyspy (ogolna), aserai = Dorne (ogolna), empire = Braavos (ogolna)
            { "vlandia", TAndal }, { "reach", TAndal }, { "river", TAndal }, { "vale", TAndal }, { "stormlands", TAndal }, { "crownlands", TAndal },
            { "dragonstone", TAndal },
            { "battania", TFirstMen }, { "skagosi", TFirstMen }, { "nightswatch", TFirstMen }, { "freefolk", TFirstMen }, { "wildlings", TFirstMen },
            { "looters", TBandit }, { "sea_raiders", TBandit }, { "mountain_bandits", TBandit }, { "forest_bandits", TBandit }, { "desert_bandits", TBandit },
            { "steppe_bandits", TBandit }, { "hilltribesmen", TBandit }, { "kingswood_outlaw", TBandit }, { "yiti_bandits", TBandit }, { "robber_knights", TBandit },
        };
        // Farbowane wedlug tekstur (nazwa tego nie mowi): tulow - straz i licznik (tulow i tak tylko z tabeli); nogi - rhoynar_civ_boots (czerwone owijacze, 65% pikseli)
        private static readonly HashSet<string> Dyed = new HashSet<string>
        {
            "andal_clothes1", "andal_clothes2", "andal_clothes3", "andal_clothes7", "andal_clothes8",
            "fm_clothes1", "fm_clothes2", "fm_clothes3", "fm_clothes4", "fm_clothes5", "fm_clothes7", "fm_clothes8",
            "rhoynar_clothes1", "rhoynar_clothes2", "rhoynar_clothes3", "rhoynar_clothes4",
            "rhoynar_civ_boots",
        };
        // slowa barwy albo stroju szlachty (cale slowa id albo nazwy): "Fine Town Boots", andal_noble_boots, "Red Northern Garb"...
        private static readonly string[] DyeWords = { "red", "crimson", "scarlet", "gold", "golden", "yellow", "purple", "violet", "blue", "azure", "green",
                                                      "royal", "noble", "lord", "lady", "king", "queen", "prince", "fine", "silk", "velvet", "embroidered",
                                                      "brocade", "fancy", "rich", "merchant" };
        private static readonly Dictionary<ItemObject, bool>[] _colour = { new Dictionary<ItemObject, bool>(), new Dictionary<ItemObject, bool>() };
        private static readonly int[] _cutTeam = new int[2], _cutDyed = new int[2];       // odrzucone: z barwami rodu / kolorowe (kazda sztuka raz na slot)
        private static readonly Dictionary<string, ItemObject[]> _tableRes = new Dictionary<string, ItemObject[]>();   // tabela -> sztuki po sitach
        private static readonly List<string> _tableOut = new List<string>();               // sztuki tabeli odrzucone ("id przyczyna", kazda raz)
        private static ItemObject[] _generic = None, _plainest = None;
        private static readonly int[] _bLv = new int[3];   // bitwa: tunika z [0] tabeli kultury, [1] ogolnej, [2] najprostszej

        // agent -> ubranie [0] tulow, [1] nogi (null - slot bez ubrania); wpis z GarrisonKit.RegisterPostfix, czyszczony na koniec misji
        private static readonly Dictionary<Agent, ItemObject[]> _agents = new Dictionary<Agent, ItemObject[]>();
        private static int _bCut, _bWreck, _bStumbles;

        /// <summary>Latka na DTE GetAgentArmors wpieta (GarrisonKit.ApplyAll) - bez niej nikogo nie ubieramy (ubranie trafialoby do wrakow).</summary>
        internal static bool ArmorsHooked;

        internal static void Reset()
        {
            _pool.Clear(); _order.Clear();
            for (int s = 0; s < 2; s++)
            {
                _neutral[s] = None; _any[s] = None; _cap[s] = 0; _capBy[s] = null; _cutCap[s] = 0; _judged[s].Clear();
                _capV[s] = _capR[s] = 0; _capVBy[s] = _capRBy[s] = null;
                _colour[s].Clear(); _cutTeam[s] = _cutDyed[s] = 0;   // m160
            }
            _built = false; _cutFemale = _cutTier = _cutBarred = _cutArmour = 0;
            _tableRes.Clear(); _tableOut.Clear(); _generic = None; _plainest = None;   // m160
            ClearMission(); ClearCounters();
        }

        internal static void ClearMission() { _agents.Clear(); }

        internal static void ClearCounters() { _bCut = _bWreck = _bStumbles = 0; _bLv[0] = _bLv[1] = _bLv[2] = 0; }

        private static void Stumble(string where, Exception e) { if (++_bStumbles <= 3) Log.Error("Civvies." + where, e); }

        private static int SlotIx(EquipmentIndex slot) { return slot == EquipmentIndex.Body ? 0 : slot == EquipmentIndex.Leg ? 1 : -1; }

        /// <summary>Ochrona w samym slocie (czy sztuka naprawde okrywa tulow / nogi).</summary>
        private static int Prot(ItemObject it, int s)
        {
            var ac = it != null ? it.ArmorComponent : null;
            return ac == null ? 0 : s == 0 ? ac.BodyArmor : ac.LegArmor;
        }

        /// <summary>Ochrona calej sztuki: glowa + tulow + rece + nogi (v3) - tym liczy sie granica, pula, kolejnosc i log.</summary>
        private static int Total(ItemObject it)
        {
            var ac = it != null ? it.ArmorComponent : null;
            return ac == null ? 0 : ac.HeadArmor + ac.BodyArmor + ac.ArmArmor + ac.LegArmor;
        }

        private static bool TypeFits(ItemObject it, int s)
        {
            return it != null && it.ItemType == (s == 0 ? ItemObject.ItemTypeEnum.BodyArmor : ItemObject.ItemTypeEnum.LegArmor);
        }

        /// <summary>Suknia i obuwie kobiece (ROT nie ma NotUsableByMale - heurystyka nazw, jak w rozpoznaniu m117).</summary>
        private static readonly string[] FemaleWords = { "dress", "female", "lady", "gown", "woman", "maiden", "wench", "bride" };

        private static bool Female(ItemObject it)
        {
            if ((it.ItemFlags & ItemFlags.NotUsableByMale) != 0) return true;
            string id = (it.StringId ?? "").ToLowerInvariant();
            string nm = it.Name != null ? it.Name.ToString().ToLowerInvariant() : "";
            foreach (var w in FemaleWords) if (id.Contains(w) || nm.Contains(w)) return true;
            return false;
        }

        /// <summary>Przeszywanica albo zbroja z nazwy (v3, Jeff: "zadne zbroje") - heurystyka nazw jak Female: ROT daje cywilom Zelaznych Wysp, Ibbenu
        /// i straznikom neutral_culture przeszywanice z flaga Civilian ("Short Gambeson over Tunic", "Infantryman Long Gambeson", "Corsair Cloth Armor").</summary>
        private static readonly string[] ArmourWords = { "gambeson", "padded", "quilted", "armor", "armour", "mail", "plate", "lamellar", "scale", "brigandine", "cuirass", "hauberk" };

        private static bool Armour(ItemObject it)
        {
            string id = (it.StringId ?? "").ToLowerInvariant();
            string nm = it.Name != null ? it.Name.ToString().ToLowerInvariant() : "";
            foreach (var w in ArmourWords) if (id.Contains(w) || nm.Contains(w)) return true;
            return false;
        }

        /// <summary>Czy sztuka nadaje sie na ubranie dla slotu s (0 tulow, 1 nogi) - wspolne sito wszystkich poziomow wyboru (bez granicy, flagi
        /// Civilian i handlu - te sprawdza Build wedlug zrodla). Liczy odrzucone - wolac tylko przez Ok (raz na sztuke).
        /// Unikaty: spis ROT (RotUniques) i straz bitewna CrashScribe (QuartermasterLaw.BarredInBattle) - to samo co ArmsPricing.IsUnique BEZ NotMerchandise
        /// (v3: IsUnique liczy NotMerchandise jako unikat, a ubrania z szablonow ludzi bywaja poza handlem - andal_civ_boots/2).</summary>
        private static bool Fits(ItemObject it, int s)
        {
            if (it == null || it.StringId == null || it.Value <= 0 || !TypeFits(it, s)) return false;
            if (Prot(it, s) <= 0) return false;                                  // slot naprawde okrywa (baggy_trunks: b0)
            if (Recipes.Grade(it) > 3) { _cutTier++; return false; }               // ubranie, nie zbroja (pulapka Tier1 == 0 - Grade)
            if (RotUniques.Ids.Contains(it.StringId) || QuartermasterLaw.BarredInBattle(it) || UniqueGear.Is(it) || GiantGear.Is(it) || UniqueLaw.IsDead(it))
            { _cutBarred++; return false; }
            if (Female(it)) { _cutFemale++; return false; }
            if (Armour(it)) { _cutArmour++; return false; }
            return true;
        }

        private static bool Ok(ItemObject it, int s)
        {
            if (it == null) return false;
            bool r;
            if (_judged[s].TryGetValue(it, out r)) return r;
            r = Fits(it, s);
            _judged[s][it] = r;
            return r;
        }

        /// <summary>m160: czy w tekscie (id albo nazwie) jest ktores ze slow - calymi slowami (litery a-z; "_" i cyfry dziela).</summary>
        private static bool HasWord(string text, string[] words)
        {
            if (string.IsNullOrEmpty(text)) return false;
            var sb = new StringBuilder(16);
            string t = text.ToLowerInvariant();
            for (int i = 0; i <= t.Length; i++)
            {
                char ch = i < t.Length ? t[i] : ' ';
                if (ch >= 'a' && ch <= 'z') { sb.Append(ch); continue; }
                if (sb.Length == 0) continue;
                string w = sb.ToString(); sb.Length = 0;
                foreach (var x in words) if (x == w) return true;
            }
            return false;
        }

        /// <summary>m160: barwa ziemi? false - sztuka z barwami rodu (UseTeamColor: gra barwi ja na Team.Color / Color2 strony, w ROT np. karmazyn i zloto
        /// Lannisterow) albo farbowana (spis Dyed z tekstur, slowo barwy / stroju szlachty w id albo nazwie). Osobno od sita Fits (granica ochrony liczy sie
        /// jak dotad - barwa to nie ochrona). Liczy odrzucone, kazda sztuka raz na slot; tylko w czasie Build.</summary>
        private static bool ColourOk(ItemObject it, int s)
        {
            if (it == null) return false;
            bool r;
            if (_colour[s].TryGetValue(it, out r)) return r;
            if (it.IsUsingTeamColor) { _cutTeam[s]++; r = false; }
            else if (Dyed.Contains(it.StringId ?? "") || HasWord(it.StringId, DyeWords) || HasWord(it.Name != null ? it.Name.ToString() : null, DyeWords))
            { _cutDyed[s]++; r = false; }
            else r = true;
            _colour[s][it] = r;
            return r;
        }

        /// <summary>m160: tabele barw ziemi -> sztuki gry. Sztuka musi istniec, byc tulowiem z flaga Civilian, przejsc sito Fits, granice ochrony (jak pula
        /// dotad) i straz barwy; odrzucona idzie do logu z przyczyna (kazde id raz).</summary>
        private static void ResolveTables()
        {
            var told = new HashSet<string>();
            foreach (var kv in Tables)
            {
                var l = new List<ItemObject>();
                foreach (var id in kv.Value)
                {
                    ItemObject it;
                    try { it = MBObjectManager.Instance.GetObject<ItemObject>(id); } catch { it = null; }
                    string why = it == null ? "brak w grze" : !TypeFits(it, 0) ? "nie tulow" : !it.IsCivilian ? "bez flagi Civilian"
                                 : !Ok(it, 0) ? "sito (tier / unikat / kobiece / zbroja)" : Total(it) > _cap[0] ? "ponad granica " + Total(it) + " > " + _cap[0]
                                 : !ColourOk(it, 0) ? (it.IsUsingTeamColor ? "z barwami rodu" : "kolorowa") : null;
                    if (why != null) { if (told.Add(id)) _tableOut.Add(id + " " + why); continue; }
                    if (!l.Contains(it)) l.Add(it);
                }
                _tableRes[kv.Key] = l.ToArray();
            }
            ItemObject[] g;
            _generic = _tableRes.TryGetValue(TGeneric, out g) ? g : None;
            _plainest = _tableRes.TryGetValue(TPlain, out g) ? g : None;
        }

        private static ItemObject[] Sorted(List<ItemObject> l, int s)
        {
            l.Sort((a, b) =>
            {
                int c = a.Value.CompareTo(b.Value);
                if (c != 0) return c;
                c = Total(a).CompareTo(Total(b));
                return c != 0 ? c : string.CompareOrdinal(a.StringId, b.StringId);
            });
            return l.ToArray();
        }

        /// <summary>Kultura umarlych (Inni) - bez puli i bez wplywu na granice (CivilDress umarlych i tak nie ubiera).</summary>
        private static bool DeadCulture(CultureObject cu)
        {
            string id = cu.StringId ?? "";
            return id.Contains("walker") || id.Contains("wight") || (cu.BasicTroop != null && Undead.Character(cu.BasicTroop))
                   || (cu.Villager != null && Undead.Character(cu.Villager));
        }

        private static IEnumerable<Equipment> Sets(CharacterObject co)
        {
            foreach (var e in co.BattleEquipments) if (e != null) yield return e;
            foreach (var e in co.CivilianEquipments) if (e != null) yield return e;
        }

        /// <summary>Listy wyboru - raz na kampanie: przebieg po postaciach (zolnierze, tier 1), granica, pule kultur, zapasy. Wywrotka: listy puste
        /// (nikogo nie ubieramy), linia bledu.</summary>
        private static void Build()
        {
            if (_built) return;
            _built = true;
            try
            {
                int bad = 0;
                // (1) co nosza zolnierze w bitwie (do granicy) i kto jest tierem 1 w swojej kulturze
                var soldierWorn = new[] { new HashSet<ItemObject>(), new HashSet<ItemObject>() };
                var t1 = new Dictionary<string, List<CharacterObject>>();
                foreach (var co in MBObjectManager.Instance.GetObjectTypeList<CharacterObject>())
                {
                    try
                    {
                        if (co == null || co.IsHero || co.IsTemplate || co.Culture == null || co.Culture.StringId == null) continue;
                        var occ = co.Occupation;
                        if (occ != Occupation.Soldier && occ != Occupation.Bandit) continue;
                        if (Undead.Character(co)) continue;
                        if (occ == Occupation.Soldier)
                            foreach (var eq in co.BattleEquipments)
                            {
                                if (eq == null) continue;
                                for (int s = 0; s < 2; s++) { var it = eq[Slots[s]].Item; if (it != null) soldierWorn[s].Add(it); }
                            }
                        if (co.Tier != 1) continue;
                        // (B) granicy: najmocniejsza sztuka, ktora w bitwie nosza rekruci tieru 1 kultur glownych (przez sito)
                        if (occ == Occupation.Soldier && co.Culture.IsMainCulture)
                            foreach (var eq in co.BattleEquipments)
                            {
                                if (eq == null) continue;
                                for (int s = 0; s < 2; s++)
                                {
                                    var it = eq[Slots[s]].Item;
                                    if (it == null || !Ok(it, s)) continue;
                                    int p = Total(it);
                                    if (p > _capR[s] || (p == _capR[s] && _capRBy[s] != null && string.CompareOrdinal(it.StringId, _capRBy[s]) < 0)) { _capR[s] = p; _capRBy[s] = it.StringId; }
                                }
                            }
                        List<CharacterObject> l;
                        if (!t1.TryGetValue(co.Culture.StringId, out l)) t1[co.Culture.StringId] = l = new List<CharacterObject>();
                        l.Add(co);
                    }
                    catch { bad++; }
                }
                var cultures = new List<CultureObject>();
                foreach (var cu in MBObjectManager.Instance.GetObjectTypeList<CultureObject>())
                {
                    try { if (cu != null && cu.StringId != null && !DeadCulture(cu)) cultures.Add(cu); }
                    catch { bad++; }
                }
                // (2) granica: (A) najmocniejsze ubranie chlopow (Villager) kultur glownych, ktorego nie nosi zaden zolnierz; granica = mniejsza z (A) i (B)
                foreach (var cu in cultures)
                {
                    try
                    {
                        if (!cu.IsMainCulture) continue;
                        var man = cu.Villager;
                        if (man == null || Undead.Character(man)) continue;
                        foreach (var eq in Sets(man))
                            for (int s = 0; s < 2; s++)
                            {
                                var it = eq[Slots[s]].Item;
                                if (it == null || !it.IsCivilian || soldierWorn[s].Contains(it) || !Ok(it, s)) continue;
                                int p = Total(it);
                                if (p > _capV[s] || (p == _capV[s] && _capVBy[s] != null && string.CompareOrdinal(it.StringId, _capVBy[s]) < 0)) { _capV[s] = p; _capVBy[s] = it.StringId; }
                            }
                    }
                    catch { bad++; }
                }
                for (int s = 0; s < 2; s++)
                {
                    bool byRecruit = _capR[s] > 0 && _capR[s] < _capV[s];   // bez rekrutow w danych - sama (A)
                    _cap[s] = byRecruit ? _capR[s] : _capV[s];
                    _capBy[s] = byRecruit ? _capRBy[s] : _capVBy[s];
                }
                // (3a) m160: tulow - tabele barw ziemi (po granicy, przed pulami kultur)
                ResolveTables();
                // (3) pule kultur: nogi - rekruci tieru 1 (albo BasicTroop) razem z Villager i Townsman, w granicy, bez kolorowych (m160);
                // tulow - tabela barw ziemi kultury (bez wpisu albo pusta - ogolna)
                foreach (var cu in cultures)
                {
                    try
                    {
                        var p = new Pool();
                        var src = new StringBuilder();
                        List<CharacterObject> rec;
                        if (t1.TryGetValue(cu.StringId, out rec) && rec.Count > 0)
                        {
                            src.Append("rekruci T1 ").Append(rec.Count).Append(" (");
                            for (int i = 0; i < rec.Count && i < 4; i++) src.Append(i > 0 ? ", " : "").Append(rec[i].StringId);
                            src.Append(rec.Count > 4 ? ", ...)" : ")");
                        }
                        else
                        {
                            rec = null;
                            var b = cu.BasicTroop;
                            if (b != null && !b.IsHero && !Undead.Character(b))
                            {
                                rec = new List<CharacterObject> { b };
                                int tb = -1;
                                try { tb = b.Tier; } catch { }
                                src.Append("bez tieru 1 - BasicTroop ").Append(b.StringId).Append(" (tier ").Append(tb).Append(')');
                            }
                            else src.Append("bez rekrutow");
                        }
                        var civ = new List<CharacterObject>();
                        foreach (var man in new[] { cu.Villager, cu.Townsman }) if (man != null && !man.IsHero && !Undead.Character(man) && !civ.Contains(man)) civ.Add(man);
                        src.Append(civ.Count > 0 ? ", cywile " : ", bez cywilow");
                        for (int i = 0; i < civ.Count; i++) src.Append(i > 0 ? "/" : "").Append(civ[i].StringId);
                        p.Src = src.ToString();
                        var seen = new[] { new HashSet<ItemObject>(), new HashSet<ItemObject>() };
                        var l = new[] { new List<ItemObject>(), new List<ItemObject>() };
                        if (rec != null) foreach (var man in rec) Gather(man, true, p, seen, l);
                        foreach (var man in civ) Gather(man, false, p, seen, l);
                        p.Items[1] = l[1].ToArray();
                        string tn;
                        ItemObject[] arr;
                        if (!CultureTable.TryGetValue(cu.StringId, out tn)) tn = TGeneric;
                        if (!_tableRes.TryGetValue(tn, out arr) || arr.Length == 0) { tn = TGeneric; arr = _generic; }
                        p.Items[0] = arr; p.Table = tn;
                        _pool[cu.StringId] = p;
                        _order.Add(cu.StringId);
                    }
                    catch { bad++; }
                }
                // (4) zapasy nog: (c) neutralne i (d) dowolne - z flaga Civilian, w handlu, w granicy, bez kolorowych (m160). Tulow tu tylko liczony
                // (odrzucone kolorowe / z barwami rodu wsrod ubran cywilnych w granicy - do logu): m160 - tulow wylacznie z tabeli barw ziemi
                var neu = new[] { new List<ItemObject>(), new List<ItemObject>() };
                var any = new[] { new List<ItemObject>(), new List<ItemObject>() };
                foreach (var it in MBObjectManager.Instance.GetObjectTypeList<ItemObject>())
                {
                    try
                    {
                        if (it == null) continue;
                        int s = it.ItemType == ItemObject.ItemTypeEnum.BodyArmor ? 0 : it.ItemType == ItemObject.ItemTypeEnum.LegArmor ? 1 : -1;
                        if (s < 0 || !it.IsCivilian || it.NotMerchandise || !Ok(it, s)) continue;
                        if (Total(it) > _cap[s]) { _cutCap[s]++; continue; }
                        if (!ColourOk(it, s) || s == 0) continue;   // m160
                        string c = it.Culture != null ? it.Culture.StringId ?? "" : "";
                        if (c.Length == 0 || c == "neutral_culture") neu[s].Add(it);
                        any[s].Add(it);
                    }
                    catch { bad++; }
                }
                for (int s = 0; s < 2; s++) { _neutral[s] = Sorted(neu[s], s); _any[s] = Sorted(any[s], s); }
                StartLines(bad);
            }
            catch (Exception e) { Log.Error("Civvies.Build", e); }
            finally { for (int s = 0; s < 2; s++) { _judged[s].Clear(); _colour[s].Clear(); } }
        }

        /// <summary>Nogi zestawow jednej postaci do puli kultury: kazda sztuka oceniana raz na kulture; ponad granica - do listy "ponad"
        /// (tylko rekruci, do logu), bez flagi Civilian albo przez sito - odpada; kolorowa / z barwami rodu - odpada (m160, licznik ColOut).
        /// m160: tulow juz nie stad - z tabeli barw ziemi (ResolveTables).</summary>
        private static void Gather(CharacterObject man, bool recruit, Pool p, HashSet<ItemObject>[] seen, List<ItemObject>[] l)
        {
            foreach (var eq in Sets(man))
                for (int s = 1; s < 2; s++)
                {
                    var it = eq[Slots[s]].Item;
                    if (it == null || !seen[s].Add(it)) continue;
                    int pr = Total(it);
                    if (pr > _cap[s]) { if (recruit && TypeFits(it, s)) p.Over[s].Add(it.StringId + " " + pr); continue; }
                    if (!it.IsCivilian || !Ok(it, s)) { if (recruit) p.RecOut[s]++; continue; }
                    if (!ColourOk(it, s)) { p.ColOut[s]++; continue; }   // m160
                    l[s].Add(it);
                    if (recruit) p.Rec[s]++; else p.Civ[s]++;
                    if (pr > p.Max[s]) p.Max[s] = pr;
                }
        }

        /// <summary>Ubranie dla czlowieka i slotu (Body albo Leg): pula kultury (losowo) -> (c) neutralne (losowo) -> (d) dowolne (najtansza); zawsze to,
        /// co udzwignie. skill - byly sztuki, ale zadna w jego Atletyce (slot zostaje pusty).
        /// m160: tulow tylko z tabel barw ziemi - tabela kultury (losowo) -> ogolna (losowo) -> najprostsza (pierwsza, ktora udzwignie); nigdy (c)/(d).</summary>
        internal static ItemObject Pick(CharacterObject co, EquipmentIndex slot, out bool skill)
        {
            skill = false;
            int s = SlotIx(slot);
            if (co == null || s < 0) return null;
            Build();
            string c = co.Culture != null ? co.Culture.StringId ?? "" : "";
            Pool p;
            ItemObject r;
            if (s == 0)
            {
                if (_pool.TryGetValue(c, out p) && (r = RandomFit(co, p.Items[0], ref skill)) != null) { _bLv[0]++; return r; }
                if ((r = RandomFit(co, _generic, ref skill)) != null) { _bLv[1]++; return r; }
                if ((r = First(co, _plainest, ref skill)) != null) { _bLv[2]++; return r; }
                return null;
            }
            if (_pool.TryGetValue(c, out p) && (r = RandomFit(co, p.Items[s], ref skill)) != null) return r;   // pula kultury
            if ((r = RandomFit(co, _neutral[s], ref skill)) != null) return r;                                   // (c)
            return First(co, _any[s], ref skill);                                                            // (d)
        }

        private static bool Wearable(CharacterObject co, ItemObject it, ref bool skill)
        {
            if (co.IsFemale && (it.ItemFlags & ItemFlags.NotUsableByFemale) != 0) return false;
            if (!ItemReq.Meets(co, it)) { skill = true; return false; }
            return true;
        }

        /// <summary>Losowo, rowno sposrod sztuk, ktore czlowiek udzwignie (dwa przebiegi, bez alokacji).</summary>
        private static ItemObject RandomFit(CharacterObject co, ItemObject[] l, ref bool skill)
        {
            if (l == null || l.Length == 0) return null;
            int m = 0;
            for (int i = 0; i < l.Length; i++) if (Wearable(co, l[i], ref skill)) m++;
            if (m == 0) return null;
            int j = m > 1 ? MBRandom.RandomInt(m) : 0;
            for (int i = 0; i < l.Length; i++) if (Wearable(co, l[i], ref skill) && j-- == 0) return l[i];
            return null;
        }

        private static ItemObject First(CharacterObject co, ItemObject[] l, ref bool skill)
        {
            if (l == null) return null;
            foreach (var it in l) if (Wearable(co, it, ref skill)) return it;
            return null;
        }

        /// <summary>Raz na kampanie (przy budowie list): naglowek z granica i zapasami, potem linia na kulture - pula przed wymogiem Atletyki.</summary>
        private static void StartLines(int bad)
        {
            var empty = new List<string>();
            int withPool = 0;
            foreach (var c in _order)
            {
                Pool p;
                if (!_pool.TryGetValue(c, out p)) continue;
                if (p.Items[0].Length == 0 && p.Items[1].Length == 0) empty.Add(c); else withPool++;
            }
            var sb = new StringBuilder(1600);
            sb.Append("Ubranie cywilne (m117 v4, m160 barwy ziemi): raz na kampanie; TULOW tylko z tabeli barw ziemi wedlug kultury (sztuki sprawdzone na teksturach")
              .Append(" - szare, brazowe, niebielony len; bez UseTeamColor), przy wystawieniu losowo to, co czlowiek udzwignie (Atletyka), potem tabela ogolna, potem")
              .Append(" najprostsza; tabele po sitach: ").Append(TablesText())
              .Append("; odrzucone z tabel: ").Append(_tableOut.Count > 0 ? string.Join(", ", _tableOut.ToArray()) : "zadna")
              .Append("; NOGI - pula kultury = sztuki z flaga Civilian, ktore nosza jej jednostki tieru 1 (bez nich Culture.BasicTroop) i jej Villager / Townsman,")
              .Append(" w granicy, bez kolorowych i z barwami rodu; przy wystawieniu losowo z puli to, co czlowiek udzwignie")
              .Append("; ochrona sztuki = glowa+tulow+rece+nogi; granica = mniejsza z (A) najmocniejsze ubranie chlopow (Villager) kultur glownych, ktorego nie nosi")
              .Append(" zaden zolnierz, i (B) najmocniejsza sztuka rekrutow tieru 1 kultur glownych: tulow ").Append(_cap[0])
              .Append(" (").Append(_capBy[0] ?? "BRAK - tulow bez ubrania").Append("; A ").Append(_capV[0]).Append(' ').Append(_capVBy[0] ?? "-")
              .Append(", B ").Append(_capR[0]).Append(' ').Append(_capRBy[0] ?? "-").Append("), nogi ").Append(_cap[1]).Append(" (").Append(_capBy[1] ?? "BRAK - nogi bez ubrania")
              .Append("; A ").Append(_capV[1]).Append(' ').Append(_capVBy[1] ?? "-").Append(", B ").Append(_capR[1]).Append(' ').Append(_capRBy[1] ?? "-")
              .Append("); zapasy nog w granicy: (c) neutralne ").Append(_neutral[1].Length).Append(" (losowo), (d) dowolne ").Append(_any[1].Length).Append(" (najtansza)")
              .Append("; odrzucone sztuki: ponad granica (z flaga, w handlu) tulow ").Append(_cutCap[0]).Append(" / nogi ").Append(_cutCap[1])
              .Append(", ponad tier 3 ").Append(_cutTier).Append(", unikaty/olbrzymy/umarli ").Append(_cutBarred).Append(", kobiece ").Append(_cutFemale)
              .Append(", przeszywanice/zbroje z nazwy ").Append(_cutArmour)
              .Append("; odrzucone kolorowe / z barwami rodu (m160; ubrania cywilne w granicy, pule nog, tabele): tulow ").Append(_cutDyed[0]).Append(" / ").Append(_cutTeam[0])
              .Append(", nogi ").Append(_cutDyed[1]).Append(" / ").Append(_cutTeam[1])
              .Append(bad > 0 ? ", bledy odczytu " + bad : "").Append("; kultur z pula ").Append(withPool);
            if (empty.Count > 0) sb.Append(", bez puli (nogi: zapas c/d) ").Append(empty.Count).Append(": ").Append(string.Join(", ", empty.ToArray()));
            Log.Info(sb.Append('.').ToString());
            foreach (var c in _order)
            {
                Pool p;
                if (!_pool.TryGetValue(c, out p) || (p.Items[0].Length == 0 && p.Items[1].Length == 0)) continue;
                Log.Info("Ubranie cywilne (m117 v4) | " + c + ": tulow " + BodyText(p) + " | nogi " + SlotText(p, 1) + " | skad nogi: " + p.Src);
            }
        }

        /// <summary>m160: tabele barw ziemi po sitach do logu ("andal: andal_clothes4, ...; ...").</summary>
        private static string TablesText()
        {
            var sb = new StringBuilder(400);
            foreach (var kv in Tables)
            {
                ItemObject[] l;
                if (!_tableRes.TryGetValue(kv.Key, out l)) l = None;
                if (sb.Length > 0) sb.Append("; ");
                sb.Append(kv.Key).Append(": ");
                if (l.Length == 0) { sb.Append("PUSTA"); continue; }
                for (int i = 0; i < l.Length; i++) sb.Append(i > 0 ? ", " : "").Append(l[i].StringId);
            }
            return sb.ToString();
        }

        /// <summary>m160: tulow kultury - nazwa tabeli i wybrane sztuki (ochrona sztuki / w slocie).</summary>
        private static string BodyText(Pool p)
        {
            var l = p.Items[0];
            var sb = new StringBuilder(160);
            sb.Append(l.Length).Append(" (tabela ").Append(p.Table ?? "-").Append(')');
            if (l.Length == 0) return sb.Append(" - ogolna / najprostsza").ToString();
            sb.Append(": ");
            for (int i = 0; i < l.Length; i++) sb.Append(i > 0 ? ", " : "").Append(l[i].StringId).Append(' ').Append(Total(l[i])).Append('/').Append(Prot(l[i], 0));
            return sb.ToString();
        }

        private static string SlotText(Pool p, int s)
        {
            var l = p.Items[s];
            var sb = new StringBuilder(200);
            sb.Append(l.Length).Append(" (rekruci ").Append(p.Rec[s]).Append(", cywile ").Append(p.Civ[s]);
            if (p.Over[s].Count > 0)
            {
                sb.Append("; rekrutow ponad granica ").Append(p.Over[s].Count).Append(": ");
                for (int i = 0; i < p.Over[s].Count && i < 3; i++) sb.Append(i > 0 ? ", " : "").Append(p.Over[s][i]);
                if (p.Over[s].Count > 3) sb.Append(", ...");
            }
            if (p.RecOut[s] > 0) sb.Append("; rekrutow bez flagi Civilian / przez sito ").Append(p.RecOut[s]);
            if (p.ColOut[s] > 0) sb.Append("; odrzucone kolorowe / z barwami rodu ").Append(p.ColOut[s]);   // m160
            sb.Append(')');
            if (l.Length == 0) return sb.Append(" - zapas c/d").ToString();
            sb.Append(" max ").Append(p.Max[s]).Append(", (ochrona sztuki / w slocie) ");
            for (int i = 0; i < l.Length && i < 6; i++) sb.Append(i > 0 ? ", " : "").Append(l[i].StringId).Append(' ').Append(Total(l[i])).Append('/').Append(Prot(l[i], s));
            if (l.Length > 6) sb.Append(", ... (+").Append(l.Length - 6).Append(')');
            return sb.ToString();
        }

        // ------------------------------------------------------------ rejestr agentow w misji

        internal static void NoteAgent(Agent a, ItemObject[] worn) { if (a != null && worn != null) _agents[a] = worn; }

        /// <summary>Czy ta sztuka to wlasne ubranie tego agenta (m117) - zapas w BattlefieldLaw.QueueWreck.</summary>
        internal static bool IsWorn(Agent a, ItemObject it)
        {
            if (a == null || it == null || _agents.Count == 0) return false;
            ItemObject[] w;
            return _agents.TryGetValue(a, out w) && w != null && (ReferenceEquals(w[0], it) || ReferenceEquals(w[1], it));
        }

        /// <summary>QueueWreck zatrzymal wrak z ubrania (latka GetAgentArmors nie zadzialala dla tego agenta) - licznik linii bitwy.</summary>
        internal static void NoteWreckStopped() { _bWreck++; }

        /// <summary>Postfiks DTE AgentExtension.GetAgentArmors(Agent) - jedyne wywolanie: DynamicTroopMissionLogic.OnAgentRemoved (czesc rozbita
        /// ciosem i "dodatkowo peknieta" u wroga tieru 3+). Lista bez wlasnego ubrania agenta: ubranie nie zabiera trafienia prawdziwej sztuce
        /// (DTE liczylby ja wtedy do lupu zamiast do rozbitych) i nie idzie do wrakow BattlefieldLaw (mennica). Lista jest swieza (DTE tworzy ja
        /// przy kazdym wywolaniu), wiec usuwamy w miejscu.</summary>
        public static void ArmorsPostfix(Agent __0, List<ItemObject> __result)
        {
            if (__0 == null || __result == null || __result.Count == 0 || _agents.Count == 0) return;
            try
            {
                ItemObject[] w;
                if (!_agents.TryGetValue(__0, out w) || w == null) return;
                for (int k = 0; k < w.Length; k++)
                    if (w[k] != null && __result.Remove(w[k])) _bCut++;
            }
            catch (Exception e) { Stumble("ArmorsPostfix", e); }
        }

        /// <summary>Dopisek do linii bitwy GarrisonKit: ile razy lista zbroi DTE byla bez ubrania, ile wrakow zatrzymal zapas, potkniecia.
        /// m160: skad tunika (tabela kultury / ogolna / najprostsza) i odrzucone przy budowie list kolorowe / z barwami rodu.</summary>
        internal static string BattleNote()
        {
            return "tunika z tabeli barw ziemi (m160): kultury " + _bLv[0] + ", ogolnej " + _bLv[1] + ", najprostszej " + _bLv[2]
                   + "; odrzucone kolorowe / z barwami rodu (budowa list) tulow " + _cutDyed[0] + " / " + _cutTeam[0] + ", nogi " + _cutDyed[1] + " / " + _cutTeam[1]
                   + "; z listy zbroi DTE (rozbite ciosem) wyjete " + _bCut + ", wrak z ubrania zatrzymany (zapas) " + _bWreck + ", potkniecia listy " + _bStumbles;
        }
    }
}
