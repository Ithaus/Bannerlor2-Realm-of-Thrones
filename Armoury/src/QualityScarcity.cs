using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// m96-jakosc - RZADKOSC JAKOSCI (Jeff 20.10, WGRANIE 95: "za duzo jest legendary przedmiotow, legendarne przedmioty to kilka sztuk na swiecie,
    /// masterwork juz wiecej itp., a w sklepach za latwo mozna znalezc legendary i masterwork").
    ///
    /// Przyczyna (dekompilacja + item_modifiers.xml): kazda sztuka uzbrojenia z warsztatu AI (WorkshopLaw - ok. 1000-2000 szt. na dobe), z drogi gry
    /// (WorkshopsCampaignBehavior.GetRandomItemAux - warsztaty gracza, linie gry, zamiennik CraftRealm.FilterRandom) i zamiennik broni jenca ROT
    /// dostaje rzut ItemModifierGroup.GetRandomItemModifierProductionScoreBased. Wagi RBM (RBMCombat_item_modifiers.xml) przy no_modifier_production_score 75
    /// (Native item_modifiers_groups.xml): legendary 5, masterwork 10, fine 15, inferior 5, poor 5 - legendarna co 23. sztuka (4.3%), mistrzowska co 12.
    /// (8.7%), bez wzgledu na tier i na to, kto kuje. Lup gracza z poleglych (DefaultBattleRewardModel.GetRandomItem, wagi lupu) - legendarna 2.2%,
    /// mistrzowska 7.6%. Ludzie lordow i zalogi nie kupuja sztuk z dodatnim stanem (AiSkipFineGoods - zbrojownia DTE to "przedmiot: ile"), wiec takie
    /// sztuki zostaja na polkach na zawsze: linia "Zakupy i nadwyzki AI (E8/Z)" u Jeffa 20.10 - 3505 odrzuconych kandydatow z dodatnim stanem w 166
    /// wizytach jednej doby, kupionych 0.
    ///
    /// Regula (klucz QualityScarcity):
    ///  1. POWSTANIE sztuki (warsztat AI, droga gry, lup gracza, jeniec ROT, nagroda zaciagu ROT, nagroda turnieju zamowiona u rzemieslnikow): rzut gry
    ///     Legendary albo Masterwork przepada (sztuka zwykla - miasto i tak placi warsztatowi cene zwyklej sztuki, WorkshopLaw.Revenue; jakosc byla
    ///     wartoscia z niczego). Fine, slabe i zepsute - jak dzis. WLASNY rzut tylko tam, gdzie sztuke ktos naprawde robi i na pewno trafia do kogos
    ///     (warsztaty AI, zamiennik broni jenca ROT, nagroda turnieju zamowiona u rzemieslnikow miasta): legendarna tylko z tieru 5-6 (QualityLegendaryChance
    ///     na sztuke), mistrzowska tylko z tieru 4-6 (QualityMasterworkChance) - i tylko, gdy na swiecie jest mniej sztuk TEGO RODZAJU niz limit rodzaju
    ///     (m96-jakosc2, ponizej). Droga gry, lup i nagroda zaciagu - tylko odbior rzutu gry (recenzja 20.10: BK ProduceOutputPrefix i tak kladzie
    ///     wyrob drogi gry bez modyfikatora, Spoils of War bierze lup z trupa, MapEvent odrzuca czesc lupu - wlasny rzut zajmowal miejsce w limicie
    ///     sztukami, ktorych nikt nie dostal). Fuszerka (Inferior, Poor) nie staje sie arcydzielem.
    ///  2. SPIS raz na dobe: wszystkie polki osad (miasta, zamki, wsie), juki partii, rynsztunek bohaterow (bojowy, cywilny, skradania), wozy kupcow
    ///     w drodze (TradeCarts), gracz (sakwy, schowek, zbrojownia DTE wojska gracza, bohaterowie rodu; zuzyta legendarna/mistrzowska gracza - wedlug
    ///     stanu z ksiegi zuzycia). Zbrojownie DTE lordow i zalog nie maja jakosci (Dictionary ItemObject -> ile). Limit przy powstaniu = spis + nowe od
    ///     spisu - wszystko liczone osobno dla kazdego rodzaju przedmiotu.
    ///  3. NADMIAR (stary swiat Jeffa): tylko na POLKACH OSAD, przy rozliczeniu doby, rodzaj po rodzaju - sztuki ponad limit rodzaju (limit minus to, co
    ///     trzymaja juki, bohaterowie i gracz) schodza od razu do ZWYKLEJ (bez modyfikatora; po recenzji m96-jakosc2 - Jeff przyjal "Nadmiar w sklepach
    ///     stanieje do zwyklej jakosci", wczesniej o stopien: legendarna -> mistrzowska albo dobra, mistrzowska -> dobra). Zostaja najdrozsze. Nic nie
    ///     znika (ta sama liczba sztuk), zloto nie rusza - spada tylko wartosc towaru na polce.
    ///     Pociski i bron miotana (modyfikator zmienia liczbe w stosie, np. RBM large_bag_* +15 strzal): zwykla ma stos +0 - sztuka z dodatnim stosem
    ///     traci nadwyzke stosu (to obnizka), sztuka z ujemnym stosem zostaje (zwykla dalaby strzaly z niczego; w danych Native/RBM takiej legendarnej
    ///     ani mistrzowskiej nie ma) - licznik "pominiete".
    ///     Sakwy, schowek i bohaterowie gracza oraz bron wykuta przez gracza (IsCraftedByPlayer) - nigdy. Kuznia gracza bez zmian.
    /// LIMIT NA RODZAJ (m96-jakosc2, Jeff 20.10 17:20: "6 legendarnych danego typu, np. 6 legendarnych helmow, ale nie 6 - daj wariacje, np. od 1 do 6
    /// legendarnych danego typu. I to samo masterwork"): zamiast jednego limitu swiata (6 / 60 lacznie) kazdy rodzaj przedmiotu ma swoj limit.
    ///  - Rodzaj = ItemObject.ItemTypeEnum: helmy (HeadArmor), zbroje (BodyArmor, razem z ChestArmor - ten sam rodzaj, w danych gry nieuzywany), rekawice,
    ///    nogawice, peleryny, zbroje konskie (HorseHarness), bron jednoreczna, dwureczna, drzewcowa, luki, kusze, strzaly, belty, bron miotana, tarcze;
    ///    reszta typow razem jako "Other" (konie - grupa horse w Native i RBM nie ma etykiet Legendary/Masterwork, wiec jakosc koni tego nie dotyczy;
    ///    proce, bron palna, ksiegi, sztandary - w ROT bez jakosci). Sztuka nigdy nie zmienia rodzaju (obnizka zmienia tylko modyfikator).
    ///  - Limit losowany RAZ na kampanie dla kazdego rodzaju osobno: legendarne QualityLegendaryPerTypeMin..Max (1..6), mistrzowskie
    ///    QualityMasterworkPerTypeMin..Max (1..60; po recenzji od 1, bylo 6 - Jeff: "od 1 do 6 ... I to samo masterwork"), kazda liczba tak samo prawdopodobna (jak rzut kostka - Jeff chcial wariacji "od 1 do 6", rozklad
    ///    z przewaga srodka dawalby prawie same 3-4). Los z ziarna kampanii (Campaign.UniqueGameId + rodzaj, skrot FNV), wiec ponowne wczytanie daje
    ///    te same liczby; wylosowane liczby ida do zapisu (arm_qualitycaps, SaveText.Sync) - brak w zapisie (zapis sprzed paczki) = los przy wczytaniu.
    ///    Zmiana zakresu w MCM w trakcie kampanii przycina wylosowana liczbe do nowego zakresu (nie losuje od nowa).
    ///  - Bez sufitu globalnego (stare QualityLegendaryWorldCap / QualityMasterworkWorldCap usuniete): 15 rodzajow z jakoscia (Other w danych ROT
    ///    pusty) przy 1-6 i 1-60 daje lacznie 15-90 legendarnych (srednio ok. 52) i 15-900 mistrzowskich (srednio ok. 458); sufit 6 lacznie zjadalby
    ///    limity rodzajow (pierwsze 6 rodzajow zabraloby wszystko).
    /// Wylacznik QualityScarcity = false: wszystko jak przed paczka; spis i linia doby dalej (pomiar).
    /// Linia doby "Jakosc przedmiotow (m96jakosc): dzien N" (grupa sprawdz_logi m96jakosc) - z czescia "rodzaje: <rodzaj> L stan/limit (...) M stan/limit (...)".
    /// </summary>
    internal static class QualityScarcity
    {
        internal const int SrcWorkshop = 0, SrcGame = 1, SrcLoot = 2, SrcGank = 3, SrcEnlist = 4, SrcTourney = 5;
        private const int NSrc = 6;
        private static readonly string[] SrcName = { "warsztaty AI", "droga gry", "lup gracza", "jeniec ROT", "nagroda zaciagu ROT", "nagroda turnieju zamowiona" };

        // miejsca spisu: polki osad, juki partii AI, bohaterowie AI, gracz (sakwy, schowek, zbrojownia DTE gracza, bohaterowie rodu gracza), wozy kupcow w drodze
        private const int PShelf = 0, PBag = 1, PHero = 2, PPlayer = 3, PCart = 4, NPlace = 5;
        private static readonly string[] PlaceName = { "polki", "juki", "bohaterowie", "gracz", "wozy" };

        // jakosci (indeks = ItemQuality w grze 1.4.8: Poor 0, Inferior 1, Common 2, Fine 3, Masterwork 4, Legendary 5)
        private const int NQ = 6;

        internal static bool On { get { var s = Settings.Current; return s != null && s.QualityScarcity; } }

        // ------------------------------------------------------------ rodzaje przedmiotu (m96-jakosc2): indeks K; ItemTypeEnum -> rodzaj
        private const int NK = 16, KOther = 15;
        private static readonly string[] KindName = { "HeadArmor", "BodyArmor", "HandArmor", "LegArmor", "Cape", "HorseHarness", "OneHandedWeapon", "TwoHandedWeapon",
                                                      "Polearm", "Bow", "Crossbow", "Arrows", "Bolts", "Thrown", "Shield", "Other" };
        private static readonly int[] KindOfType = BuildKinds();

        private static int[] BuildKinds()
        {
            int max = 0;
            foreach (var v in (ItemObject.ItemTypeEnum[])Enum.GetValues(typeof(ItemObject.ItemTypeEnum))) if ((int)v > max) max = (int)v;
            var a = new int[max + 1];
            for (int i = 0; i < a.Length; i++) a[i] = KOther;
            a[(int)ItemObject.ItemTypeEnum.HeadArmor] = 0;
            a[(int)ItemObject.ItemTypeEnum.BodyArmor] = 1; a[(int)ItemObject.ItemTypeEnum.ChestArmor] = 1;   // ChestArmor - ta sama zbroja tulowia
            a[(int)ItemObject.ItemTypeEnum.HandArmor] = 2;
            a[(int)ItemObject.ItemTypeEnum.LegArmor] = 3;
            a[(int)ItemObject.ItemTypeEnum.Cape] = 4;
            a[(int)ItemObject.ItemTypeEnum.HorseHarness] = 5;
            a[(int)ItemObject.ItemTypeEnum.OneHandedWeapon] = 6;
            a[(int)ItemObject.ItemTypeEnum.TwoHandedWeapon] = 7;
            a[(int)ItemObject.ItemTypeEnum.Polearm] = 8;
            a[(int)ItemObject.ItemTypeEnum.Bow] = 9;
            a[(int)ItemObject.ItemTypeEnum.Crossbow] = 10;
            a[(int)ItemObject.ItemTypeEnum.Arrows] = 11;
            a[(int)ItemObject.ItemTypeEnum.Bolts] = 12;
            a[(int)ItemObject.ItemTypeEnum.Thrown] = 13;
            a[(int)ItemObject.ItemTypeEnum.Shield] = 14;
            return a;
        }

        private static int KindOf(ItemObject it) { int t = it != null ? (int)it.ItemType : -1; return t >= 0 && t < KindOfType.Length ? KindOfType[t] : KOther; }

        // zakres losowania z MCM (min > max - min = max); kod przycina do 0..50 i 0..500 (gen_mcm RANGES)
        private static void RangeL(out int lo, out int hi)
        {
            var s = Settings.Current;
            lo = s == null ? 1 : Math.Max(0, Math.Min(50, s.QualityLegendaryPerTypeMin)); hi = s == null ? 6 : Math.Max(0, Math.Min(50, s.QualityLegendaryPerTypeMax));
            if (lo > hi) lo = hi;
        }

        private static void RangeM(out int lo, out int hi)
        {
            var s = Settings.Current;
            lo = s == null ? 1 : Math.Max(0, Math.Min(500, s.QualityMasterworkPerTypeMin)); hi = s == null ? 60 : Math.Max(0, Math.Min(500, s.QualityMasterworkPerTypeMax));
            if (lo > hi) lo = hi;
        }

        // limit rodzaju: wylosowana liczba przycieta do biezacego zakresu MCM (brak losu - gorna granica; przed losem i tak nic nie powstaje - _censusDay < 0)
        private static int CapL(int k) { int lo, hi; RangeL(out lo, out hi); int v = _limL[k]; if (v < 0) v = hi; return v < lo ? lo : v > hi ? hi : v; }
        private static int CapM(int k) { int lo, hi; RangeM(out lo, out hi); int v = _limM[k]; if (v < 0) v = hi; return v < lo ? lo : v > hi ? hi : v; }

        private static float ChanceL { get { var s = Settings.Current; return s == null ? 0f : Math.Max(0f, Math.Min(0.05f, s.QualityLegendaryChance)); } }
        private static float ChanceM { get { var s = Settings.Current; return s == null ? 0f : Math.Max(0f, Math.Min(0.05f, s.QualityMasterworkChance)); } }

        // ------------------------------------------------------------ stan (czyszczony miedzy kampaniami - konstruktor zachowania)
        private static int _censusDay = -1;          // doba ostatniego spisu (-1 = jeszcze nie bylo: przy powstaniu bez legendarnych i mistrzowskich)
        // m96-jakosc2: limity rodzajow (wylosowane raz na kampanie, zapis arm_qualitycaps; -1 = jeszcze bez losu), swiat rodzaju ze spisu (po wyrownaniu),
        // nowe od spisu (rezerwa limitu rodzaju)
        private static readonly int[] _limL = new int[NK], _limM = new int[NK];
        private static int _limSaved, _limDrawn;     // rodzajow z zapisu / wylosowanych przy tym wczytaniu
        private static readonly int[] _wL = new int[NK], _wM = new int[NK];
        private static readonly int[] _newL = new int[NK], _newM = new int[NK];
        private static readonly int[] _lineNewL = new int[NK], _lineNewM = new int[NK];   // nowe z doby (do linii)
        private static readonly int[] _pieces = new int[NSrc], _gameL = new int[NSrc], _gameM = new int[NSrc], _madeL = new int[NSrc], _madeM = new int[NSrc];
        private static int _capL, _capM, _noCensus, _noMod;
        private static int _stumbles;
        private static bool _errLogged;
        private static readonly Dictionary<ItemModifierGroup, ItemModifier[]> _mods = new Dictionary<ItemModifierGroup, ItemModifier[]>();
        private static readonly object _modsLock = new object();

        // spis doby
        private static readonly int[,] _cnt = new int[NPlace, NQ];
        private static long _plainShelf;
        private struct Entry { public ItemRoster R; public EquipmentElement El; public int N; public int V; public int K; }
        private static readonly List<Entry> _shelfL = new List<Entry>(), _shelfM = new List<Entry>();
        private static readonly HashSet<Equipment> _seenEq = new HashSet<Equipment>();
        // m96-jakosc2: rodzaj po rodzaju, w rodzaju zostaja najdrozsze
        private static readonly Comparison<Entry> ByKindValue = (a, b) => a.K != b.K ? a.K.CompareTo(b.K) : b.V.CompareTo(a.V);
        // spis rodzaju: wszystkie miejsca (_kL/_kM), polki (_kLs/_kMs), polki nietykalne - wyroby gracza i pociski bez celu (_kLu/_kMu); po wyrownaniu
        private static readonly int[] _kL = new int[NK], _kM = new int[NK], _kLs = new int[NK], _kMs = new int[NK], _kLu = new int[NK], _kMu = new int[NK];
        private static int _crafted, _craftedL, _craftedM;   // wyroby gracza (IsCraftedByPlayer) legendarne / mistrzowskie na polkach - nietykalne
        private static int _dFromL, _dFromM, _dStacks; private static long _dValue;   // obnizki doby do zwyklej (legendarne, mistrzowskie, stosy, wartosc)
        private static int _dSkip, _dFail;             // obnizki doby: pominiete (pociski bez celu o nie wiekszym stosie), nieudane zdjecie z polki
        private static int _wornOrig;                  // zuzyte sztuki gracza liczone wedlug stanu z ksiegi zuzycia (legendarne / mistrzowskie)
        private static int _tourneyFixed;              // nagrody turnieju zamowione u rzemieslnikow, ktorym regula zmienila jakosc (od wczoraj)
        private static bool _wiredAux, _wiredLoot, _wiredEnlist;
        private static bool _dteLooked; private static System.Reflection.FieldInfo _dteArmory;   // DynamicTroopEquipmentReupload.ArmyArmory.Armory (ItemRoster gracza)
        private static readonly Action<Settlement, Settlement, EquipmentElement, int> CartLine = (a, b, el, n) => Add(PCart, null, el, n);
        // ostatnia sztuka, ktorej regula dala legendarna/mistrzowska - drugi przebieg tej samej sztuki (ROTGankBehavior.GetRandomItem wola sam siebie,
        // postfiks biegnie na obu poziomach) nie odbiera jej jak rzutu gry
        [ThreadStatic] private static ItemObject _lastItem;
        [ThreadStatic] private static ItemModifier _lastMod;
        private static int _lastLineDay = -1;
        private static double _startMs;

        /// <summary>m110-stan: modyfikatory jakosci grup od nowa po zamianie stanow w grupach (GearState.Apply) - liczniki zostaja.</summary>
        internal static void ClearModCache() { lock (_modsLock) _mods.Clear(); }

        internal static void Reset()
        {
            _censusDay = -1;
            for (int k = 0; k < NK; k++) { _limL[k] = -1; _limM[k] = -1; }
            _limSaved = _limDrawn = 0;
            Array.Clear(_wL, 0, NK); Array.Clear(_wM, 0, NK); Array.Clear(_newL, 0, NK); Array.Clear(_newM, 0, NK); Array.Clear(_lineNewL, 0, NK); Array.Clear(_lineNewM, 0, NK);
            ClearKinds();
            Array.Clear(_pieces, 0, NSrc); Array.Clear(_gameL, 0, NSrc); Array.Clear(_gameM, 0, NSrc); Array.Clear(_madeL, 0, NSrc); Array.Clear(_madeM, 0, NSrc);
            _capL = _capM = _noCensus = _noMod = 0; _stumbles = 0; _errLogged = false;
            lock (_modsLock) _mods.Clear();
            Array.Clear(_cnt, 0, _cnt.Length); _plainShelf = 0; _shelfL.Clear(); _shelfM.Clear(); _seenEq.Clear(); _crafted = _craftedL = _craftedM = 0;
            _dFromL = _dFromM = _dStacks = 0; _dValue = 0; _dSkip = _dFail = 0; _wornOrig = 0; _tourneyFixed = 0;
            _lastLineDay = -1; _lastItem = null; _lastMod = null;
        }

        private static void ClearKinds()
        {
            Array.Clear(_kL, 0, NK); Array.Clear(_kM, 0, NK); Array.Clear(_kLs, 0, NK); Array.Clear(_kMs, 0, NK); Array.Clear(_kLu, 0, NK); Array.Clear(_kMu, 0, NK);
        }

        private static void Stumble(string where, Exception e)
        {
            Interlocked.Increment(ref _stumbles);
            if (!_errLogged) { _errLogged = true; Log.Error("QualityScarcity." + where, e); }   // raz w logu, reszta w liczniku potkniec
        }

        // ------------------------------------------------------------ limity rodzajow (m96-jakosc2): los raz na kampanie, zapis arm_qualitycaps
        private static uint Hash(string s)
        {
            uint h = 2166136261;
            if (s != null) unchecked { foreach (char c in s) h = (h ^ c) * 16777619; }
            // domieszanie (murmur3 fmix32) - skrot FNV podobnych napisow rozni sie glownie w niskich bitach
            unchecked { h ^= h >> 16; h *= 0x85ebca6b; h ^= h >> 13; h *= 0xc2b2ae35; h ^= h >> 16; }
            return h;
        }

        /// <summary>Liczba lo..hi, kazda tak samo prawdopodobna, z ziarna kampanii i rodzaju (to samo ziarno = ta sama liczba przy kazdym wczytaniu).</summary>
        private static int DrawOne(string camp, string tag, int k, int lo, int hi)
        {
            if (hi <= lo) return lo;
            uint h = Hash(camp + "|jakosc|" + tag + "|" + KindName[k]);
            return lo + (int)(h % (uint)(hi - lo + 1));
        }

        /// <summary>Losuje brakujace limity rodzajow (nowa kampania albo zapis sprzed paczki) - start sesji, przed pierwszym spisem.</summary>
        private static void EnsureLimits()
        {
            string camp = "";
            try { camp = Campaign.Current != null ? Campaign.Current.UniqueGameId ?? "" : ""; } catch { camp = ""; }
            int loL, hiL, loM, hiM;
            RangeL(out loL, out hiL); RangeM(out loM, out hiM);
            for (int k = 0; k < NK; k++)
            {
                if (_limL[k] >= 0 && _limM[k] >= 0) continue;
                if (_limL[k] < 0) _limL[k] = DrawOne(camp, "L", k, loL, hiL);
                if (_limM[k] < 0) _limM[k] = DrawOne(camp, "M", k, loM, hiM);
                _limDrawn++;
            }
        }

        /// <summary>Zapis: "q1|HeadArmor:4,37;BodyArmor:2,51;..." (nazwa rodzaju, wylosowane legendarne, mistrzowskie). Rodzaj bez losu - pomijany.</summary>
        internal static string Export()
        {
            try
            {
                var sb = new StringBuilder("q1|");
                bool any = false;
                for (int k = 0; k < NK; k++)
                {
                    if (_limL[k] < 0 || _limM[k] < 0) continue;
                    if (any) sb.Append(';');
                    sb.Append(KindName[k]).Append(':').Append(_limL[k]).Append(',').Append(_limM[k]);
                    any = true;
                }
                return any ? sb.ToString() : "";
            }
            catch (Exception e) { Stumble("Export", e); return ""; }
        }

        /// <summary>Wczytanie: limity rodzajow z zapisu; brak klucza, nieznany format albo brak rodzaju - los przy starcie sesji (EnsureLimits).</summary>
        internal static void Import(string data)
        {
            try
            {
                for (int k = 0; k < NK; k++) { _limL[k] = -1; _limM[k] = -1; }
                _limSaved = 0;
                if (string.IsNullOrEmpty(data) || !data.StartsWith("q1|", StringComparison.Ordinal)) return;
                foreach (var part in data.Substring(3).Split(';'))
                {
                    int c = part.IndexOf(':');
                    if (c <= 0) continue;
                    int k = Array.IndexOf(KindName, part.Substring(0, c));
                    if (k < 0 || _limL[k] >= 0) continue;
                    var f = part.Substring(c + 1).Split(',');
                    int l, m;
                    if (f.Length < 2 || !int.TryParse(f[0], System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out l)
                        || !int.TryParse(f[1], System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out m)) continue;
                    if (l < 0 || m < 0 || l > 500 || m > 5000) continue;
                    _limL[k] = l; _limM[k] = m; _limSaved++;
                }
            }
            catch (Exception e) { Stumble("Import", e); }
        }

        private static string Limits()
        {
            var sb = new StringBuilder(NK * 24);
            for (int k = 0; k < NK; k++)
            {
                if (k > 0) sb.Append(", ");
                sb.Append(KindName[k]).Append(' ').Append(CapL(k)).Append('/').Append(CapM(k));
            }
            return sb.ToString();
        }

        private static int SumCap(bool leg) { int n = 0; for (int k = 0; k < NK; k++) n += leg ? CapL(k) : CapM(k); return n; }

        /// <summary>Najdrozszy modyfikator grupy z dana etykieta jakosci (grupa chain ma etykiety pomieszane tylko w stanach zuzycia i slabych -
        /// Legendary, Masterwork i Fine sa jednoznaczne w kazdej grupie broni i zbroi Native/RBM). null = grupa nie ma takiej jakosci (konie, BK).</summary>
        private static ItemModifier ModOf(ItemModifierGroup g, ItemQuality q)
        {
            if (g == null) return null;
            int i = (int)q;
            if (i < 0 || i >= NQ) return null;
            ItemModifier[] a;
            lock (_modsLock)
            {
                if (!_mods.TryGetValue(g, out a))
                {
                    a = new ItemModifier[NQ];
                    var all = g.ItemModifiers;
                    for (int k = 0; k < all.Count; k++)
                    {
                        var m = all[k];
                        if (m == null || m.PriceMultiplier <= 0f) continue;
                        int j = (int)m.ItemQuality;
                        if (j < 0 || j >= NQ) continue;
                        if (a[j] == null || m.PriceMultiplier > a[j].PriceMultiplier) a[j] = m;
                    }
                    _mods[g] = a;
                }
            }
            return a[i];
        }

        private static int QIdx(ItemModifier m) { if (m == null) return -1; int j = (int)m.ItemQuality; return j >= 0 && j < NQ ? j : -1; }

        // ------------------------------------------------------------ 1. powstanie sztuki
        /// <summary>
        /// Jakosc nowej sztuki: rzut gry (rolled) przechodzi przez regule. Legendary i Masterwork z rzutu gry -> zwykla; wlasny rzut: legendarna
        /// (tier 5-6) i mistrzowska (tier 4-6) w granicy limitu rodzaju przedmiotu (m96-jakosc2). Reszta rzutu gry bez zmian. Wylaczony klucz albo sztuka bez grupy - rolled.
        /// own = false: tylko odbior rzutu gry, bez wlasnego rzutu (droga gry, lup, nagroda zaciagu, pierwszy przebieg jenca ROT - sztuka moze nigdzie
        /// nie trafic, a wlasny rzut zajmowalby miejsce w limicie).
        /// </summary>
        internal static ItemModifier Roll(ItemObject item, ItemModifier rolled, int src) { return Roll(item, rolled, src, true); }

        private static ItemModifier Roll(ItemObject item, ItemModifier rolled, int src, bool own)
        {
            if (item == null || src < 0 || src >= NSrc || !On) return rolled;
            try
            {
                var grp = item.ItemComponent != null ? item.ItemComponent.ItemModifierGroup : null;
                if (grp == null) return rolled;
                Interlocked.Increment(ref _pieces[src]);
                ItemModifier baseMod = rolled;
                if (src == SrcGank)
                {
                    // tylko jeniec ROT i tylko nastepne wywolanie: ta sama sztuka z tym samym modyfikatorem prosto z poziomu nizej jest juz po regule
                    var li = _lastItem; var lm = _lastMod; _lastItem = null; _lastMod = null;
                    if (rolled != null && ReferenceEquals(item, li) && ReferenceEquals(rolled, lm)) return rolled;
                }
                if (rolled != null)
                {
                    var q = rolled.ItemQuality;
                    if (q == ItemQuality.Legendary) { Interlocked.Increment(ref _gameL[src]); baseMod = null; }
                    else if (q == ItemQuality.Masterwork) { Interlocked.Increment(ref _gameM[src]); baseMod = null; }
                    else if (q != ItemQuality.Fine) return rolled;   // fuszerka zostaje fuszerka; stany bez etykiety (konie, zuzycie, lup Spoils) - bez zmian
                }
                if (!own) return baseMod;
                int grade = Recipes.Grade(item);
                float pl = ChanceL, pm = ChanceM;
                int k = KindOf(item);   // m96-jakosc2: limit rodzaju (helmy osobno, luki osobno ...)
                if (grade >= 5 && pl > 0f && MBRandom.RandomFloat < pl)
                {
                    var m = ModOf(grp, ItemQuality.Legendary);
                    if (m == null) Interlocked.Increment(ref _noMod);
                    else if (_censusDay < 0) Interlocked.Increment(ref _noCensus);
                    else if (_wL[k] + Volatile.Read(ref _newL[k]) >= CapL(k)) Interlocked.Increment(ref _capL);
                    else { Interlocked.Increment(ref _newL[k]); Interlocked.Increment(ref _madeL[src]); if (src == SrcGank) { _lastItem = item; _lastMod = m; } return m; }
                }
                if (grade >= 4 && pm > 0f && MBRandom.RandomFloat < pm)
                {
                    var m = ModOf(grp, ItemQuality.Masterwork);
                    if (m == null) Interlocked.Increment(ref _noMod);
                    else if (_censusDay < 0) Interlocked.Increment(ref _noCensus);
                    else if (_wM[k] + Volatile.Read(ref _newM[k]) >= CapM(k)) Interlocked.Increment(ref _capM);
                    else { Interlocked.Increment(ref _newM[k]); Interlocked.Increment(ref _madeM[src]); if (src == SrcGank) { _lastItem = item; _lastMod = m; } return m; }
                }
                return baseMod;
            }
            catch (Exception e) { Stumble("Roll", e); return rolled; }
        }

        /// <summary>Gotowy element z postfiksu (droga gry, lup, nagroda zaciagu, jeniec ROT): tylko odbior rzutu gry Legendary/Masterwork, bez
        /// wlasnego rzutu - ten element moze nigdzie nie trafic (BK kladzie wyrob drogi gry bez modyfikatora, MapEvent odrzuca czesc lupu, HeroGear
        /// podmienia bron jenca na zamiennik z wlasnym rzutem).</summary>
        internal static void Filter(ref EquipmentElement el, int src)
        {
            try
            {
                var it = el.Item;
                if (it == null || !On) return;
                var m0 = el.ItemModifier;
                var m1 = Roll(it, m0, src, false);
                if (!ReferenceEquals(m0, m1)) el = new EquipmentElement(it, m1);
            }
            catch (Exception e) { Stumble("Filter", e); }
        }

        /// <summary>PlayerFees8.SettlePrize, sciezka TourneyPrizeCommissioned: nagrody nie bylo na polce, miasto zamowilo ja u rzemieslnikow i placi
        /// cene ZWYKLEJ sztuki - jakosc z rzutu RBMTournament (ok. 5% legendarna, 10% mistrzowska) idzie przez regule warsztatow (wlasny rzut w limicie
        /// rodzaju). Podmienia sztuke w sakwach gracza (ta sama liczba sztuk); el - sztuka po regule.</summary>
        internal static void Commissioned(ItemRoster mine, ref EquipmentElement el, Settlement st)
        {
            try
            {
                var it = el.Item;
                if (it == null || mine == null || !On || mine.FindIndexOfElement(el) < 0) return;
                var m0 = el.ItemModifier;
                int q0 = QIdx(m0);
                var m1 = Roll(it, m0, SrcTourney, true);
                if (ReferenceEquals(m0, m1)) return;
                var el1 = new EquipmentElement(it, m1);
                if (mine.AddToCounts(el, -1) < 0) return;
                mine.AddToCounts(el1, 1);
                el = el1;
                _tourneyFixed++;
                Log.Info("Jakosc przedmiotow (m96jakosc): nagroda turnieju zamowiona u rzemieslnikow " + (st != null && st.Name != null ? st.Name.ToString() : "?") + " - "
                         + it.StringId + " " + (m0 != null ? m0.StringId : "zwykla") + " -> " + (m1 != null ? m1.StringId : "zwykla") + ".");
                if ((q0 == (int)ItemQuality.Legendary || q0 == (int)ItemQuality.Masterwork) && QIdx(m1) < q0)   // RBM oglosil rzut - gracz wie, czemu sztuka inna
                    Log.Player("The craftsmen of " + (st != null && st.Name != null ? st.Name.ToString() : "the town") + " made your prize, " + (it.Name != null ? it.Name.ToString() : "the piece")
                               + ", as " + (m1 != null && m1.Name != null ? "a " + m1.ItemQuality.ToString().ToLowerInvariant() + " piece" : "a plain piece")
                               + " - legendary and masterwork pieces are rare in the world.");
            }
            catch (Exception e) { Stumble("Commissioned", e); }
        }

        /// <summary>Postfiks WorkshopsCampaignBehavior.GetRandomItemAux (Priority.Last - po WorkshopLaw.RandomItemPostfix i CraftRealm.FilterRandom).</summary>
        public static void AuxPostfix(ref EquipmentElement __result) { Filter(ref __result, SrcGame); }

        /// <summary>Postfiks DefaultBattleRewardModel.GetLootedItemFromTroop (Priority.Last - po BK LootScale): lup gracza z poleglych.</summary>
        public static void LootPostfix(ref EquipmentElement __result) { Filter(ref __result, SrcLoot); }

        /// <summary>Postfiks ROTEnlistmentBehavior.GetRandomItem(int, Settlement): sztuka z wagami lupu dla gracza w zaciagu (EnlistmentPatches).</summary>
        public static void EnlistPostfix(ref EquipmentElement __result) { Filter(ref __result, SrcEnlist); }

        internal static void ApplyAll(Harmony h)
        {
            try
            {
                var aux = AccessTools.DeclaredMethod(typeof(WorkshopsCampaignBehavior), "GetRandomItemAux");
                if (aux != null) { h.Patch(aux, postfix: new HarmonyMethod(typeof(QualityScarcity), nameof(AuxPostfix)) { priority = Priority.Last }); _wiredAux = true; }
                var loot = AccessTools.DeclaredMethod(typeof(DefaultBattleRewardModel), "GetLootedItemFromTroop");
                if (loot != null && loot.ReturnType == typeof(EquipmentElement))
                { h.Patch(loot, postfix: new HarmonyMethod(typeof(QualityScarcity), nameof(LootPostfix)) { priority = Priority.Last }); _wiredLoot = true; }
                // ROT 8.1.8: nagroda zaciagu (internal static GetRandomItem(int, Settlement) -> EquipmentElement); innej sygnatury nie ruszamy
                var tEn = QuartermasterLaw.FindType("ROT.CampaignBehaviors.ROTEnlistmentBehavior");
                var en = tEn != null ? AccessTools.DeclaredMethod(tEn, "GetRandomItem", new[] { typeof(int), typeof(Settlement) }) : null;
                if (en != null && en.ReturnType == typeof(EquipmentElement))
                { h.Patch(en, postfix: new HarmonyMethod(typeof(QualityScarcity), nameof(EnlistPostfix)) { priority = Priority.Last }); _wiredEnlist = true; }
                Log.Info("Jakosc przedmiotow (m96jakosc): wpiete - warsztaty AI (WorkshopLaw, w kodzie), jeniec ROT (HeroGear, w kodzie), nagroda turnieju zamowiona"
                         + " (PlayerFees8, w kodzie), tylko odbior rzutu gry: droga gry "
                         + (_wiredAux ? "GetRandomItemAux" : "BRAK GetRandomItemAux") + ", lup gracza " + (_wiredLoot ? "GetLootedItemFromTroop" : "BRAK GetLootedItemFromTroop")
                         + ", nagroda zaciagu ROT " + (_wiredEnlist ? "GetRandomItem" : tEn == null ? "(ROT nieobecny)" : "inna sygnatura - bez latki") + ".");
            }
            catch (Exception e) { Log.Error("QualityScarcity.ApplyAll", e); }
        }

        internal static void SessionStart()
        {
            try
            {
                // spis bez wyrownania: limit przy powstaniu sztuk zna swiat od pierwszej godziny (wyrownanie i linia - w dobie)
                int loL, hiL, loM, hiM;
                RangeL(out loL, out hiL); RangeM(out loM, out hiM);
                if (Campaign.Current != null)
                {
                    long t0 = Stopwatch.GetTimestamp();
                    EnsureLimits();   // m96-jakosc2: limity rodzajow z zapisu albo los z ziarna kampanii - przed pierwszym spisem
                    Census();
                    for (int k = 0; k < NK; k++) { _wL[k] = _kL[k]; _wM[k] = _kM[k]; _newL[k] = 0; _newM[k] = 0; }
                    _censusDay = (int)CampaignTime.Now.ToDays;
                    _shelfL.Clear(); _shelfM.Clear();
                    _startMs = (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency;
                }
                Log.Info("Jakosc przedmiotow (m96jakosc): start sesji | klucz Quality Scarcity " + (On ? "TAK" : "NIE (tylko spis)")
                         + " | limit na rodzaj: legendarne " + loL + "-" + hiL + " (suma " + SumCap(true) + "), mistrzowskie " + loM + "-" + hiM + " (suma " + SumCap(false) + ")"
                         + "; z zapisu " + _limSaved + " rodzajow, wylosowane z ziarna kampanii " + _limDrawn
                         + " | limity (legendarne/mistrzowskie): " + Limits()
                         + " | stan rodzajow przy wczytaniu: " + KindsNow()
                         + " | szansa na sztuke: legendarna " + (ChanceL * 100f).ToString("0.000") + "% (tier 5-6), mistrzowska " + (ChanceM * 100f).ToString("0.000") + "% (tier 4-6)"
                         + " | latki: droga gry " + (_wiredAux ? "tak" : "nie") + ", lup gracza " + (_wiredLoot ? "tak" : "nie") + ", nagroda zaciagu ROT " + (_wiredEnlist ? "tak" : "nie")
                         + " | spis przy wczytaniu: legendarne " + Sum((int)ItemQuality.Legendary) + " (" + Places((int)ItemQuality.Legendary) + "), mistrzowskie " + Sum((int)ItemQuality.Masterwork) + " (" + Places((int)ItemQuality.Masterwork)
                         + "), dobre " + Sum((int)ItemQuality.Fine) + ", zwykle uzbrojenie na polkach " + _plainShelf + ", koszt " + _startMs.ToString("0.0") + " ms.");
            }
            catch (Exception e) { Stumble("SessionStart", e); }
        }

        // ------------------------------------------------------------ 2. spis raz na dobe
        private static void Add(int place, ItemRoster r, EquipmentElement el, int n)
        {
            var it = el.Item;
            if (it == null || n <= 0) return;
            var m = el.ItemModifier;
            if (m == null)
            {
                if (place == PShelf && it.ItemComponent != null && it.ItemComponent.ItemModifierGroup != null && (it.WeaponComponent != null || it.ArmorComponent != null)) _plainShelf += n;
                return;
            }
            int q = QIdx(m);
            if (q < 0 || q == (int)ItemQuality.Common) return;
            _cnt[place, q] += n;
            if (q != (int)ItemQuality.Legendary && q != (int)ItemQuality.Masterwork) return;
            bool leg = q == (int)ItemQuality.Legendary;
            int k = KindOf(it);   // m96-jakosc2: spis rodzaju
            if (leg) _kL[k] += n; else _kM[k] += n;
            if (place != PShelf || r == null) return;
            if (leg) _kLs[k] += n; else _kMs[k] += n;
            if (it.IsCraftedByPlayer)   // bron wykuta przez gracza - nietykalna (liczona w spisie)
            {
                _crafted += n;
                if (leg) { _craftedL += n; _kLu[k] += n; } else { _craftedM += n; _kMu[k] += n; }
                return;
            }
            var e = new Entry { R = r, El = el, N = n, V = it.Value, K = k };
            if (leg) _shelfL.Add(e); else _shelfM.Add(e);
        }

        private static void Roster(int place, ItemRoster r)
        {
            if (r == null) return;
            for (int i = 0; i < r.Count; i++)
            {
                var el = r.GetElementCopyAtIndex(i);
                if (el.Amount > 0) Add(place, r, el.EquipmentElement, el.Amount);
            }
        }

        private static void Gear(int place, Equipment eq)
        {
            if (eq == null || !_seenEq.Add(eq)) return;   // wspolne obiekty (np. rynsztunek zmarlych gry) - raz
            for (int i = 0; i < (int)EquipmentIndex.NumEquipmentSetSlots; i++)
            {
                var el = eq[i];
                if (el.Item != null) Add(place, null, el, 1);
            }
        }

        /// <summary>Rynsztunek bojowy gracza: zuzycie Armoury podmienia modyfikator na stan zuzycia, a oryginal trzyma w ksiedze (GoodOriginal) - zuzyta
        /// legendarna/mistrzowska liczy sie wedlug oryginalu (po naprawie wraca), inaczej zwalnialaby miejsce w limicie.</summary>
        private static void PlayerBattleGear(Equipment eq)
        {
            if (eq == null || !_seenEq.Add(eq)) return;
            var ab = ArmouryBehavior.Instance;
            for (int i = 0; i < (int)EquipmentIndex.NumEquipmentSetSlots; i++)
            {
                var el = eq[i];
                if (el.Item == null) continue;
                ItemModifier orig = null;
                try { orig = ab != null ? ab.GoodOriginal(i) : null; } catch { orig = null; }
                int qo = QIdx(orig);
                if ((qo == (int)ItemQuality.Legendary || qo == (int)ItemQuality.Masterwork) && !ReferenceEquals(orig, el.ItemModifier))
                {
                    _wornOrig++;
                    Add(PPlayer, null, new EquipmentElement(el.Item, orig), 1);
                }
                else Add(PPlayer, null, el, 1);
            }
        }

        /// <summary>Zbrojownia DTE wojska gracza (DynamicTroopEquipmentReupload.ArmyArmory.Armory - statyczny ItemRoster z modyfikatorami); null bez DTE.</summary>
        private static ItemRoster DteArmory()
        {
            if (!_dteLooked)
            {
                _dteLooked = true;
                try { var t = QuartermasterLaw.FindType("DynamicTroopEquipmentReupload.ArmyArmory"); _dteArmory = t != null ? AccessTools.Field(t, "Armory") : null; }
                catch { _dteArmory = null; }
                if (_dteArmory != null && !_dteArmory.IsStatic) _dteArmory = null;
            }
            return _dteArmory != null ? _dteArmory.GetValue(null) as ItemRoster : null;
        }

        private static void Census()
        {
            Array.Clear(_cnt, 0, _cnt.Length); _plainShelf = 0; _shelfL.Clear(); _shelfM.Clear(); _seenEq.Clear(); _crafted = _craftedL = _craftedM = 0; _wornOrig = 0;
            ClearKinds();
            var setts = Settlement.All;
            for (int i = 0; i < setts.Count; i++)
            {
                var s = setts[i];
                if (s == null || s.Party == null) continue;
                try
                {
                    Roster(PShelf, s.ItemRoster);
                    if (s.Stash != null && s.Stash.Count > 0) Roster(PPlayer, s.Stash);
                }
                catch (Exception e) { Stumble("Census(osada)", e); }
            }
            var main = MobileParty.MainParty;
            var pc = Clan.PlayerClan;
            var parties = MobileParty.All;
            for (int i = 0; i < parties.Count; i++)
            {
                var p = parties[i];
                if (p == null) continue;
                try { Roster(ReferenceEquals(p, main) || (pc != null && p.ActualClan == pc) ? PPlayer : PBag, p.ItemRoster); }
                catch (Exception e) { Stumble("Census(partia)", e); }
            }
            // zbrojownia DTE wojska gracza i wozy kupcow w drodze (tylko liczone - wyrownanie tylko na polkach)
            try { Roster(PPlayer, DteArmory()); } catch (Exception e) { Stumble("Census(DTE gracza)", e); }
            try { TradeCarts.ForEachCartLine(CartLine); } catch (Exception e) { Stumble("Census(wozy)", e); }
            var heroes = Hero.AllAliveHeroes;
            var me = Hero.MainHero;
            for (int i = 0; i < heroes.Count; i++)
            {
                var h = heroes[i];
                if (h == null) continue;
                try
                {
                    int place = h == me || (pc != null && h.Clan == pc) ? PPlayer : PHero;
                    if (h == me) PlayerBattleGear(h.BattleEquipment); else Gear(place, h.BattleEquipment);
                    Gear(place, h.CivilianEquipment);
                    Gear(place, h.StealthEquipment);
                }
                catch (Exception e) { Stumble("Census(bohater)", e); }
            }
            _seenEq.Clear();
        }

        private static int Sum(int q) { int n = 0; for (int p = 0; p < NPlace; p++) n += _cnt[p, q]; return n; }

        // ------------------------------------------------------------ 3. nadmiar na polkach osad
        /// <summary>Obnizka n sztuk wpisu polki do ZWYKLEJ (bez modyfikatora; po recenzji m96-jakosc2 - wczesniej o stopien). Ta sama liczba sztuk na tej
        /// samej polce, zloto nie rusza; stan rodzaju i polek po obnizce.</summary>
        private static void Demote(ref Entry e, int n, int fromQ)
        {
            if (n <= 0) return;
            var r = e.R; var it = e.El.Item; var from = e.El.ItemModifier;
            if (r.AddToCounts(e.El, -n) < 0) { _dFail += n; return; }   // sztuki juz nie ma na polce - nic nie dopisujemy (nic z niczego)
            r.AddToCounts(new EquipmentElement(it, null), n);   // ta sama liczba sztuk na tej samej polce - nic nie znika
            float f0 = from != null ? from.PriceMultiplier : 1f;
            _dValue += (long)Math.Round((f0 - 1f) * it.Value * n);
            _dStacks++;
            int k = e.K;
            // stan rodzaju po obnizce (rodzaj sztuki sie nie zmienia - tylko modyfikator)
            if (fromQ == (int)ItemQuality.Legendary) { _dFromL += n; _kL[k] -= n; _kLs[k] -= n; } else { _dFromM += n; _kM[k] -= n; _kMs[k] -= n; }
            if (it.WeaponComponent != null || it.ArmorComponent != null) _plainShelf += n;   // linia: zwykle uzbrojenie na polkach po wyrownaniu
            e.N -= n;
        }

        /// <summary>Wyrownanie jednej jakosci rodzaj po rodzaju. W rodzaju: limit rodzaju minus to, czego regula nie rusza (juki, bohaterowie, gracz, wozy,
        /// wyroby gracza na polkach); ponad to sztuki z polek schodza do zwyklej, zostaja najdrozsze. Pocisk z ujemnym stosem zostaje (zwykla ma stos +0 -
        /// dalaby strzaly z niczego; w danych Native/RBM takiej legendarnej ani mistrzowskiej nie ma) - w spisie rodzaju jako nietykalny.</summary>
        private static void LevelQ(List<Entry> list, bool leg)
        {
            if (list.Count > 1) list.Sort(ByKindValue);
            int fromQ = leg ? (int)ItemQuality.Legendary : (int)ItemQuality.Masterwork;
            int[] world = leg ? _kL : _kM, untouch = leg ? _kLu : _kMu;
            for (int i = 0; i < list.Count; )
            {
                int k = list[i].K, j = i, shelf = 0;
                while (j < list.Count && list[j].K == k) { shelf += list[j].N; j++; }
                int allow = Math.Max(0, (leg ? CapL(k) : CapM(k)) - (world[k] - shelf));   // przed obnizkami tego rodzaju
                if (shelf > allow)
                {
                    int left = allow;
                    for (int x = i; x < j; x++)
                    {
                        var e = list[x];
                        int keep = Math.Min(e.N, left); left -= keep;
                        int d = e.N - keep;
                        if (d <= 0) continue;
                        var from = e.El.ItemModifier;
                        if (from != null && from.StackCount < 0) { _dSkip += d; untouch[k] += d; continue; }
                        Demote(ref e, d, fromQ);
                    }
                }
                i = j;
            }
        }

        /// <summary>m96-jakosc2: wyrownanie rodzaj po rodzaju, mistrzowskie i legendarne osobno - obnizka idzie do zwyklej, wiec jedna jakosc nie zajmuje
        /// miejsca w limicie drugiej (po recenzji m96-jakosc2; wczesniej legendarne schodzily na wolne miejsce mistrzowskich tego rodzaju).</summary>
        private static void Level()
        {
            LevelQ(_shelfM, false);
            LevelQ(_shelfL, true);
        }

        /// <summary>Doba (DailyTickEvent zachowania): spis, wyrownanie nadmiaru na polkach (przy kluczu), linia logu.</summary>
        internal static void Daily()
        {
            try
            {
                if (Settings.Current == null || Campaign.Current == null) return;
                int day = (int)CampaignTime.Now.ToDays;
                if (day == _lastLineDay) return;
                _lastLineDay = day;
                long t0 = Stopwatch.GetTimestamp();
                _dFromL = _dFromM = _dStacks = 0; _dValue = 0; _dSkip = _dFail = 0;
                Census();
                int before0 = Sum((int)ItemQuality.Legendary), before1 = Sum((int)ItemQuality.Masterwork);
                int shelfL0 = _cnt[PShelf, (int)ItemQuality.Legendary], shelfM0 = _cnt[PShelf, (int)ItemQuality.Masterwork];
                bool on = On;
                if (on)
                {
                    try { Level(); } catch (Exception e) { Stumble("Level", e); }
                }
                // po wyrownaniu: polki przesuniete o obnizki do zwyklej (sztuk tyle samo; zwykle - w "zwykle uzbrojenie na polkach", Demote)
                _cnt[PShelf, (int)ItemQuality.Legendary] -= _dFromL;
                _cnt[PShelf, (int)ItemQuality.Masterwork] -= _dFromM;
                // m96-jakosc2: swiat rodzaju po wyrownaniu (limit przy powstaniu) i nowe z doby do linii
                int newL = 0, newM = 0;
                for (int k = 0; k < NK; k++)
                {
                    _wL[k] = _kL[k]; _wM[k] = _kM[k];
                    _lineNewL[k] = Interlocked.Exchange(ref _newL[k], 0); _lineNewM[k] = Interlocked.Exchange(ref _newM[k], 0);
                    newL += _lineNewL[k]; newM += _lineNewM[k];
                }
                _censusDay = day;
                double ms = (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency;
                Line(day, on, before0, before1, shelfL0, shelfM0, newL, newM, ms);
                _shelfL.Clear(); _shelfM.Clear();
            }
            catch (Exception e) { Stumble("Daily", e); }
        }

        private static string Places(int q)
        {
            var sb = new StringBuilder();
            for (int p = 0; p < NPlace; p++) { if (p > 0) sb.Append(", "); sb.Append(PlaceName[p]).Append(' ').Append(_cnt[p, q]); }
            return sb.ToString();
        }

        private static string BySrc(int[] a)
        {
            var sb = new StringBuilder("[");
            for (int i = 0; i < NSrc; i++) { if (i > 0) sb.Append(", "); sb.Append(SrcName[i]).Append(' ').Append(a[i]); }
            return sb.Append(']').ToString();
        }

        private static int Tot(int[] a) { int n = 0; for (int i = 0; i < a.Length; i++) n += a[i]; return n; }

        /// <summary>m96-jakosc2: stan rodzajow - "HeadArmor L 3/4 (polki 1, nietykalne 0, nowe 0) M 40/52 (polki 30, nietykalne 2, nowe 1); ..."
        /// (stan/limit; polki - na polkach osad, nietykalne - z tego wyroby gracza i pociski bez celu, nowe - z reguly od poprzedniego spisu).</summary>
        private static void Kinds(StringBuilder sb, bool withNew)
        {
            for (int k = 0; k < NK; k++)
            {
                if (k > 0) sb.Append("; ");
                sb.Append(KindName[k]).Append(" L ").Append(_kL[k]).Append('/').Append(CapL(k)).Append(" (polki ").Append(_kLs[k]).Append(", nietykalne ").Append(_kLu[k]);
                if (withNew) sb.Append(", nowe ").Append(_lineNewL[k]);
                sb.Append(") M ").Append(_kM[k]).Append('/').Append(CapM(k)).Append(" (polki ").Append(_kMs[k]).Append(", nietykalne ").Append(_kMu[k]);
                if (withNew) sb.Append(", nowe ").Append(_lineNewM[k]);
                sb.Append(')');
            }
        }

        private static string KindsNow() { var sb = new StringBuilder(NK * 80); Kinds(sb, false); return sb.ToString(); }

        private static void Line(int day, bool on, int worldL0, int worldM0, int shelfL0, int shelfM0, int newL, int newM, double ms)
        {
            int loL, hiL, loM, hiM;
            RangeL(out loL, out hiL); RangeM(out loM, out hiM);
            var sb = new StringBuilder(3000);
            sb.Append("Jakosc przedmiotow (m96jakosc): dzien ").Append(day)
              .Append(" | regula ").Append(on ? "TAK" : "NIE (tylko spis)")
              .Append(", limit na rodzaj: legendarne ").Append(loL).Append('-').Append(hiL).Append(" (suma ").Append(SumCap(true)).Append(")")
              .Append(", mistrzowskie ").Append(loM).Append('-').Append(hiM).Append(" (suma ").Append(SumCap(false)).Append(")")
              .Append(" | spis przed wyrownaniem: legendarne ").Append(worldL0).Append(" (polki ").Append(shelfL0).Append(")")
              .Append(", mistrzowskie ").Append(worldM0).Append(" (polki ").Append(shelfM0).Append(")")
              .Append(" | swiat po: legendarne ").Append(Sum((int)ItemQuality.Legendary)).Append(" (").Append(Places((int)ItemQuality.Legendary)).Append(")")
              .Append(", mistrzowskie ").Append(Sum((int)ItemQuality.Masterwork)).Append(" (").Append(Places((int)ItemQuality.Masterwork)).Append(")")
              .Append(", dobre ").Append(Sum((int)ItemQuality.Fine)).Append(" (").Append(Places((int)ItemQuality.Fine)).Append(")")
              .Append(", slabe ").Append(Sum((int)ItemQuality.Inferior)).Append(", zepsute ").Append(Sum((int)ItemQuality.Poor))
              .Append(" | zwykle uzbrojenie na polkach ").Append(_plainShelf)
              .Append(" | nowe od wczoraj: legendarne ").Append(newL).Append(' ').Append(BySrc(_madeL))
              .Append(", mistrzowskie ").Append(newM).Append(' ').Append(BySrc(_madeM))
              .Append(" z sztuk z jakoscia ").Append(Tot(_pieces)).Append(' ').Append(BySrc(_pieces))
              .Append(" | rzut gry odebrany: legendarne ").Append(Tot(_gameL)).Append(' ').Append(BySrc(_gameL))
              .Append(", mistrzowskie ").Append(Tot(_gameM)).Append(' ').Append(BySrc(_gameM))
              .Append(" | zatrzymane limitem rodzaju: legendarne ").Append(_capL).Append(", mistrzowskie ").Append(_capM)
              .Append("; przed pierwszym spisem ").Append(_noCensus).Append("; grupa bez tej jakosci ").Append(_noMod)
              .Append(" | obnizki na polkach do zwyklej: legendarne ").Append(_dFromL).Append(", mistrzowskie ").Append(_dFromM)
              .Append(" (stosow ").Append(_dStacks).Append("), wartosc towaru na polkach -").Append(_dValue).Append(" zl (zloto nie rusza)")
              .Append("; pominiete (pociski bez celu) ").Append(_dSkip).Append(", nieudane zdjecie z polki ").Append(_dFail)
              .Append(" | wyroby gracza na polkach nietykalne ").Append(_crafted).Append(" (legendarne ").Append(_craftedL).Append(", mistrzowskie ").Append(_craftedM).Append(")")
              .Append(" | zuzyte rzeczy gracza wedlug ksiegi ").Append(_wornOrig).Append(", nagrody turnieju zamowione z jakoscia po regule ").Append(_tourneyFixed)
              .Append(" | rodzaje (stan/limit po wyrownaniu): ");
            Kinds(sb, true);
            sb.Append(" | koszt ").Append(ms.ToString("0.0")).Append(" ms | potkniecia ").Append(_stumbles).Append('.');
            Log.Info(sb.ToString());
            Array.Clear(_pieces, 0, NSrc); Array.Clear(_gameL, 0, NSrc); Array.Clear(_gameM, 0, NSrc); Array.Clear(_madeL, 0, NSrc); Array.Clear(_madeM, 0, NSrc);
            _capL = _capM = _noCensus = _noMod = 0; _tourneyFixed = 0;
        }
    }

    /// <summary>m96-jakosc: doba (spis, wyrownanie, linia) i start sesji. m96-jakosc2: w zapisie tylko limity rodzajow wylosowane na kampanie
    /// (arm_qualitycaps, napis - zapis wczytuje sie takze bez tej paczki); stan swiata dalej ze spisu.</summary>
    internal sealed class QualityScarcityBehavior : CampaignBehaviorBase
    {
        public QualityScarcityBehavior() { QualityScarcity.Reset(); }

        public override void RegisterEvents()
        {
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, DayProfile.Dx("QualityScarcity.Daily", QualityScarcity.Daily));
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, s => QualityScarcity.SessionStart());
        }

        public override void SyncData(IDataStore dataStore)
        {
            try
            {
                string s = dataStore.IsSaving ? QualityScarcity.Export() : null;
                SaveText.Sync(dataStore, "arm_qualitycaps", ref s);
                if (dataStore.IsLoading) QualityScarcity.Import(s);
            }
            catch (Exception e) { Log.Error("SyncData.arm_qualitycaps", e); }
        }
    }
}
