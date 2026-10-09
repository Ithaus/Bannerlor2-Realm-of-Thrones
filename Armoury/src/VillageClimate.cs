using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// T8 (noc 08/09.10): KLIMAT WSI - towar glowny wsi, ktory nie moze rosnac tam, gdzie wies stoi (Jeff: "czy rozklad produkcji ma sens
    /// i jest uzasadniony regionem"). Audyt 02 tabela D.1: 12 wsi z towarem niemozliwym w klimacie (bawelna przy Murze, w Braavos, w gorach
    /// Doliny, w Sarnorze, w Dorzeczu, w Krolewskim Lesie, w Norvos i Ziemiach Korony; winnica w Lorath; daktyle na Tarth) dostaje towar
    /// swojej krainy; D.2: 5 cieplych wsi (Qarth 2, delta Rhoyne, Reach 2) przechodzi na bawelne. Poprawka (autotest S1): 5 wsi za 10
    /// zabranych to bylo za malo (bawelna ze wsi 75-80/d wobec 116-124/d, aksamit -60%) - D.3 doklada 6 cieplych wsi (Selhorys, Myr, Meereen,
    /// Chroyane, Oros, Mantarys) z towarow, ktorych D.1 dodala. Bawelna 13 -> 14 wsi (10 zabranych, 11 dodanych: dwie zabrane maja poziom
    /// ognisk 2), baza produkcji 112 -> 112, wszystkie dodane w strefie cieplej. Bilans liczby wsi po tabeli: bawelna +1, owce 0, bydlo 0,
    /// konie stepowe 0, len +1, ryby +2 (zywnosci przybywa), drwale -2, daktyle -1 (Tarth), winnice -1 (Gelina).
    /// D.4 (D1 noc 09/10.10, Jeff 09.10 "za Murem bez zboza -> myslistwo i ryby"): 4 farmy zboza za Murem (audyt 02 D.1) -> traperzy w lesie,
    /// rybacy nad woda. Polozenie z danych mapy ROT (a-teren\teren.npz: siatka nawigacyjna + terrain.bin + flora; odleglosc w jedn. mapy):
    /// Crowgrave morze 49.5 / rzeka 53.3, las 100% (puszcza) -> traperzy; Storrold port, morze 2.0 -> rybacy; Ghostcreek morze 7.7 / rzeka
    /// 19.3, las 100% (puszcza) -> traperzy; Frostbank morze 6.0 / rzeka 8.7, bez lasu (sniezna rownina) -> rybacy. Miara: 62 wsie rybakow ROT
    /// maja wode (blizsze z morza i rzeki) mediana 4.0, 75% do 6.4; 23 wsie traperow mediana 12.7. Za Murem po D.4: traperzy 5, rybacy 4,
    /// farm zboza 0 - ale NIE "bez zboza": gra daje "zboze 3" KAZDEMU typowi wsi (18 wsi x 3 = ok. 54/d wobec ok. 242/d przed D.4), a Skirling
    /// (castle_S5, flax_plant) dalej uprawia len; zdjecie "zboza 3" z wsi za Murem to osobny krok (audyt 02 C4/N3, filtr w MineralOnce).
    /// Co daja typy (gra DefaultVillageTypes + BK PopulationManager.GetProductions): trapper = zboze 3 + futra 1.4 + miod BK 0.5 (wiecej z pasieka
    /// Skeps); fisherman = zboze 3 + ryby 28; wheat_farm = zboze 50 + krowa 0.2, owca 0.4, swinia 0.8 (gra) + jablka 2, marchew 2 i drob
    /// (kurczak 1, ges 1 - klasa Cropland) z BK; chleb BK = 10% pierwszego zboza listy (farma 5, inne 0.3). Odrzucone whaler / walrus_hunter
    /// (NavalDLC: ryby 5 + tran 1.8 albo kly morsa 1.4) - rybacy daja 28 ryb. Miesa z polowan nie daje zaden typ wsi (traperzy sprzedaja futra).
    /// Zywnosc warowni: gra liczy wsie z poziomu ognisk, nie z typu (DefaultSettlementFoodModel, x6 na wies) - zmienia sie tylko jedzenie
    /// sprzedane na targu (BonusToFoodStores: mniej zboza, jablek, marchwi i chleba; wiecej ryb i miodu; futra to nie jedzenie). Na farme:
    /// -> traperzy ok. -55/d, -> rybacy ok. -28/d (poziom ognisk 1): Thenn ok. -55/d, Hardhome ok. -83/d, Frostfang's Camp ok. -28/d, przy
    /// bilansie warowni +130..+200/d w najgorszej dobie 4 autotestow sprzed D.4 (przy poziomie 2 x1.5 - dalej na plusie).
    /// Gdzie: typ wsi (Village.VillageType) to zwykle pole BEZ zapisu w grze - gra ustawia je z settlements.xml przy kazdym wczytaniu
    /// (Village.Deserialize), a wszyscy czytaja je na biezaco (model produkcji gry, lista BK GetProductions, magazyn i tabor wsi,
    /// nasze GoodsLedger, VillageWoodlot, VillageClogDiag). Jedno miejsce: podmiana raz przy starcie sesji (ArmouryBehavior.OnSessionLaunched,
    /// po McmSettings.Apply), jak NorthernFare w CrashScribe (inne wsie - bez wspolnych wpisow). Podmieniamy tylko, gdy wies ma dzis typ
    /// z tabeli ("bylo") - jesli ktos inny juz go zmienil, nie ruszamy (linia logu to pokaze). Pliki ROT bez zmian.
    /// KLASA WSI BK (recenzja T8): BK trzyma w SWOIM zapisie klase wsi (LandData.VillageClass, [SaveableProperty(8)]) ustalona z typu wsi
    /// (DefaultVillageClasses.GetClass) i wpisuje ja tylko, gdy jest Unset - po zmianie typu klasa zostalaby stara (dochod majatkow BK,
    /// klastry, polityka majatkow AI). Dlatego przy kazdym starcie sesji, dla wsi z tabeli (i wierszy wycofanych - Retired), wyrownujemy ja do biezacego typu: gdy klasa
    /// = klasa typu "bylo" -> klasa typu "jest" (przy wylaczonym VillageClimateFix odwrotnie: typ wrocil z mapy, klasa wraca do typu z mapy).
    /// Klasy innej niz obie (dekret BK ClassTransition, inny mod) nie ruszamy. Klasa Unset albo brak danych BK - BK ustawi ja sam z
    /// biezacego typu. Zapis BK trzyma wiec tylko klase zgodna z typem; wylaczenie dziala od nastepnego wczytania (typy i klasy wracaja).
    /// Czego NIE robimy: warsztatow miast (aksamit w miastach, ktore straca bawelne z bliska, kupuje ja dalej na targu) i nowej kampanii
    /// przed rozdaniem warsztatow (gra buduje je w OnNewGameCreated, przed OnSessionLaunched) - to krok C1 audytu 02 na pozniej.
    /// </summary>
    internal static class VillageClimate
    {
        // id wsi, typ dzis (settlements.xml ROT), typ nowy, nazwa i powod (log). Typy: silk_plant = bawelna (gra), reszta jak w settlements.xml.
        private static readonly string[][] Table =
        {
            // D.1 - towar niemozliwy w klimacie (4 farmy za Murem - D.4 nizej)
            new[] { "castle_village_B7_1",   "silk_plant", "sheep_farm",         "Ornstead (Nocna Straz, mroz)" },
            new[] { "castle_village_EN2_1",  "silk_plant", "sheep_farm",         "Durlston (gory Doliny)" },
            new[] { "castle_village_EN5_1",  "silk_plant", "cattle_farm",        "Rushing Falls (Dorzecze, laki)" },
            new[] { "village_EN5_4",         "silk_plant", "flax_plant",         "Samatha (Braavos, plotno zaglowe)" },
            new[] { "castle_village_ES2_2",  "silk_plant", "flax_plant",         "Metachia (Norvos, wzgorza)" },
            new[] { "village_K5_3",          "silk_plant", "steppe_horse_ranch", "Ispantar (Sarnor, step)" },
            new[] { "village_K6_1",          "silk_plant", "sheep_farm",         "Karahan (Sarnor, step)" },
            new[] { "village_K6_3",          "silk_plant", "cattle_farm",        "Danara (Sarnor, step)" },
            new[] { "ROT_castle17_village2", "silk_plant", "lumberjack",         "King's Mountain (Krolewski Las)" },
            new[] { "village_EW5_3",         "silk_plant", "flax_plant",         "Old Stonebridge (Ziemie Korony)" },
            new[] { "ROT_castle49_village1", "vineyard",   "fisherman",          "Gelina (Lorath, zimno)" },
            new[] { "ROT_town8_village1",    "date_farm",  "fisherman",          "Tarth (wyspa umiarkowana)" },
            // D.2 - wyrownanie bawelny w cieplych wsiach. Poprawka po recenzji (noc 08/09.10): Abar i Tyrono (daktyle) wycofane - daktyli
            // zabraklo 3 wsi (Tarth + te dwie); w ich miejsce dwie wsie Reach z nadwyzki D.1 (len, bydlo), miasta z dwiema farmami zboza.
            new[] { "ROT_town35_village2",   "lumberjack", "silk_plant",         "Shirosi (Qarth)" },
            new[] { "ROT_town37_village2",   "lumberjack", "silk_plant",         "Port Yhos (Qarth)" },
            new[] { "village_ES4_1",         "lumberjack", "silk_plant",         "Sagora (Volantis, delta Rhoyne)" },
            new[] { "village_V1_1",          "flax_plant", "silk_plant",         "Cider Hall (Ashford, Reach)" },
            new[] { "village_V5_3",          "cattle_farm","silk_plant",         "Berrybush (Highgarden, Reach)" },
            // D.3 (poprawka noc 08/09.10, autotest S1): D.2 dawala 5 wsi za 10 zabranych - bawelna ze wsi 75-80/d wobec 116-124/d bez T8.
            // Bawelna jednej wsi zalezy od ognisk, nie od miejsca: model gry 8 x (poziom ognisk + 1) x 0.5 (poziom 0/1/2 od 200/600 ognisk),
            // do tego budynki miasta i postfiksy BK/BE. Zabrane Ornstead (809) i Rushing Falls (621) maja poziom 2, wszystkie dodane poziom 1,
            // wiec 10 za 10 dawaloby baze 104 zamiast 112 - stad 11 dodanych (baza 112 = 112). Wsie z nadwyzki D.1 (owce +3, len +3, bydlo +2,
            // konie stepowe +1). Miasta Selhorys, Myr, Meereen, Ashford, Highgarden maja dalej farme zboza; Mantarys traci konie (nie zywnosc);
            // Tamnuh i Usek naleza do zamkow (zywnosc zamku gra liczy z ognisk wsi, nie z jej typu). Spottswood Village (jedyni rybacy Sunspear)
            // wycofana po recenzji - ryby to zywnosc miasta (DefaultSettlementFoodModel dolicza zywnosc sprzedana na targu), w jej miejsce Usek.
            new[] { "village_ES5_1",         "sheep_farm", "silk_plant",         "Lanthas (Selhorys, dolina Rhoyne)" },
            new[] { "ROT_town12_village2",   "sheep_farm", "silk_plant",         "Tasko (Myr)" },
            new[] { "village_K1_2",          "flax_plant", "silk_plant",         "Ulaan (Meereen, Zatoka Niewolnicza)" },
            new[] { "castle_village_A8_1",   "cattle_farm","silk_plant",         "Tamnuh (Chroyane, Rhoyne)" },
            new[] { "castle_village_K1_1",   "sheep_farm", "silk_plant",         "Usek (Oros, Valyria)" },
            new[] { "village_K2_1",          "steppe_horse_ranch", "silk_plant", "Karakalat (Mantarys, Valyria)" },
            // D.4 (D1 noc 09/10.10) - za Murem bez rolnictwa: w lesie traperzy, nad woda rybacy (polozenie - komentarz klasy)
            new[] { "village_S6_2",          "wheat_farm", "trapper",            "Crowgrave (za Murem, Thenn, puszcza)" },
            new[] { "village_S7_2",          "wheat_farm", "fisherman",          "Storrold (za Murem, Hardhome, port)" },
            new[] { "village_S7_3",          "wheat_farm", "trapper",            "Ghostcreek (za Murem, Hardhome, puszcza)" },
            new[] { "village_S4_2",          "wheat_farm", "fisherman",          "Frostbank (za Murem, Frostfang's Camp, morze i rzeka)" },
        };

        // Wiersze wycofane z tabeli (byly w starszej wersji T8): typ wsi wraca z mapy sam (typ nie idzie do zapisu gry), ale klasa wsi BK
        // (w zapisie BK) moze miec jeszcze klase typu "jest" - przy kazdym starcie sesji wraca do klasy typu z mapy (jak przy wylaczonym T8).
        private static readonly string[][] Retired =
        {
            new[] { "ROT_town11_village1",   "date_farm",  "silk_plant",         "Abar (Lys)" },
            new[] { "ROT_castle47_village2", "date_farm",  "silk_plant",         "Tyrono (Tyrosh)" },
            new[] { "village_A1_1",          "fisherman",  "silk_plant",         "Spottswood Village (Sunspear)" },
        };

        /// <summary>Raz przy starcie sesji: podmiana typu wsi wedlug tabeli (wylacznik VillageClimateFix), wyrownanie klasy wsi BK do
        /// biezacego typu i jedna linia logu "wies: bylo -> jest".</summary>
        internal static void Apply()
        {
            var s = Settings.Current;
            if (s == null) return;
            var bk = new BkClass();
            int retStumbles;
            string retired = AlignRetired(out retStumbles);
            if (!s.VillageClimateFix)
            {
                // typy wrocily z mapy ROT; klasa BK z poprzedniej sesji (typ "jest") wraca do typu z mapy
                int st0 = 0;
                foreach (var row in Table)
                {
                    try
                    {
                        var st = Settlement.Find(row[0]);
                        if (st == null || st.Village == null) continue;
                        var cur = st.Village.VillageType;
                        if (cur == null || cur.StringId != row[1]) continue;
                        bk.Align(st, row[3], Type(row[2]), cur);
                    }
                    catch (Exception e) { st0++; if (st0 <= 3) Log.Error("VillageClimate.Apply(" + row[0] + ")", e); }
                }
                Log.Info("Klimat wsi (T8): WYLACZONY w ustawieniach (Village Climate Fix) - typy wsi z mapy ROT (" + Table.Length + " wsi z tabeli bez zmian; po wylaczeniu w trwajacej sesji typy wracaja przy nastepnym wczytaniu); "
                         + "wsi bawelny na mapie " + CountType(Cotton, ref st0) + ", " + LiveCotton(ref st0) + "; " + BeyondWall(false, ref st0) + "; " + bk.Summary() + "; " + retired
                         + "; potkniecia " + (st0 + bk.Stumbles + retStumbles) + ".");
                return;
            }
            var done = new List<string>();
            var already = new List<string>();
            var other = new List<string>();
            var missing = new List<string>();
            var applied = new List<string[]>();   // wiersze z typem "jest" po przebiegu (podmienione teraz albo juz wczesniej) - do bilansu
            int stumbles = 0;
            foreach (var row in Table)
            {
                try
                {
                    var st = Settlement.Find(row[0]);
                    if (st == null || st.Village == null) { missing.Add(row[0]); continue; }
                    var cur = st.Village.VillageType;
                    string curId = cur != null ? cur.StringId : "null";
                    if (curId == row[2]) { already.Add(row[3]); applied.Add(row); bk.Align(st, row[3], Type(row[1]), cur); continue; }   // klasa BK takze tu (stary zapis, nowa kampania)
                    if (curId != row[1]) { other.Add(row[3] + " ma " + curId); continue; }   // ktos juz zmienil - nie ruszamy
                    var vt = Type(row[2]);
                    if (vt == null) { missing.Add("typ " + row[2]); continue; }
                    st.Village.VillageType = vt;
                    done.Add(row[3] + ": " + row[1] + " -> " + row[2]);
                    applied.Add(row);
                    bk.Align(st, row[3], cur, vt);
                }
                catch (Exception e)
                {
                    stumbles++;
                    if (stumbles <= 3) Log.Error("VillageClimate.Apply(" + row[0] + ")", e);
                }
            }
            var line = "Klimat wsi (T8): podmienione " + done.Count + " z " + Table.Length + " wsi z tabeli"
                       + (done.Count > 0 ? " - " + string.Join("; ", done.ToArray()) : "");
            if (already.Count > 0) line += "; juz z nowym typem: " + string.Join(", ", already.ToArray());
            if (other.Count > 0) line += "; inny typ niz w tabeli (NIE ruszane): " + string.Join(", ", other.ToArray());
            if (missing.Count > 0) line += "; brak w tej kampanii: " + string.Join(", ", missing.ToArray());
            line += "; " + Balance(applied, ref stumbles);
            line += "; " + BeyondWall(true, ref stumbles) + "; " + bk.Summary() + "; " + retired + "; potkniecia " + (stumbles + bk.Stumbles + retStumbles)
                    + ". Typ wsi nie idzie do zapisu gry, klasa wsi BK idzie (wyrownana do typu).";
            Log.Info(line);
        }

        private const string Cotton = "silk_plant";

        /// <summary>Bilans podmian, ktore dzis obowiazuja: ile wsi bawelny zabrano i ile dodano, wsie bawelny na mapie teraz,
        /// i zmiana liczby wsi kazdego innego typu (0 = towar bez zmian w sumie swiata).</summary>
        private static string Balance(List<string[]> applied, ref int stumbles)
        {
            try
            {
                int planTaken = 0, planAdded = 0;
                foreach (var row in Table)
                {
                    if (row[1] == Cotton) planTaken++;
                    if (row[2] == Cotton) planAdded++;
                }
                // baza gry na wies: stawka typu bawelny x (poziom ognisk + 1) x 0.5 (DefaultVillageProductionCalculatorModel), bez budynkow i postfiksow
                var cotton = CottonItem();
                var ct = Type(Cotton);
                float rate = (cotton != null && ct != null) ? ct.GetProductionPerDay(cotton) : 0f;
                int taken = 0, added = 0;
                float baseTaken = 0f, baseAdded = 0f;
                var delta = new SortedDictionary<string, int>(StringComparer.Ordinal);
                foreach (var row in applied)
                {
                    float b = 0f;
                    try
                    {
                        var st = Settlement.Find(row[0]);
                        if (st != null && st.Village != null) b = rate * (st.Village.GetHearthLevel() + 1) * 0.5f;
                    }
                    catch (Exception e) { stumbles++; if (stumbles <= 3) Log.Error("VillageClimate.Balance(" + row[0] + ")", e); }
                    if (row[1] == Cotton) { taken++; baseTaken += b; }
                    if (row[2] == Cotton) { added++; baseAdded += b; }
                    int v;
                    delta.TryGetValue(row[1], out v); delta[row[1]] = v - 1;
                    delta.TryGetValue(row[2], out v); delta[row[2]] = v + 1;
                }
                var parts = new List<string>();
                foreach (var kv in delta)
                    if (kv.Key != Cotton && kv.Value != 0) parts.Add(kv.Key + " " + (kv.Value > 0 ? "+" : "") + kv.Value);
                float net = baseAdded - baseTaken;
                return "BILANS bawelny: zabrano " + taken + " wsi, dodano " + added + " (netto " + (added - taken >= 0 ? "+" : "") + (added - taken)
                       + "; plan z tabeli " + planTaken + " zabranych / " + planAdded + " dodanych)"
                       + "; baza gry (" + rate.ToString("0.#") + " x (poziom ognisk + 1) x 0.5, bez budynkow i postfiksow BK/BE): zabrano "
                       + baseTaken.ToString("0.#") + "/d, dodano " + baseAdded.ToString("0.#") + "/d (netto " + (net >= 0f ? "+" : "") + net.ToString("0.#") + "/d)"
                       + "; wsi bawelny na mapie teraz " + CountType(Cotton, ref stumbles) + ", " + LiveCotton(ref stumbles)
                       + "; inne typy (liczba wsi, tylko zmienione): " + (parts.Count > 0 ? string.Join(", ", parts.ToArray()) : "bez zmian");
            }
            catch (Exception e)
            {
                stumbles++;
                if (stumbles <= 3) Log.Error("VillageClimate.Balance", e);
                return "BILANS bawelny: blad liczenia";
            }
        }

        /// <summary>Ile wsi na mapie ma dzis dany typ (-1 = nie da sie policzyc).</summary>
        private static int CountType(string id, ref int stumbles)
        {
            try
            {
                var all = Village.All;
                if (all == null) return -1;
                int n = 0;
                foreach (var v in all)
                    if (v != null && v.VillageType != null && v.VillageType.StringId == id) n++;
                return n;
            }
            catch (Exception e)
            {
                stumbles++;
                if (stumbles <= 3) Log.Error("VillageClimate.CountType", e);
                return -1;
            }
        }

        // warownie za Murem w settlements.xml ROT (Frostfang's Camp, Thenn, Hardhome, Craster's Keep, Fist of the First Men, Hornfoot,
        // Frozen Shore) - 18 wsi; po warowni, nie po kulturze (BK zmienia kulture wsi przy starcie sesji i asymilacji)
        private static readonly HashSet<string> WallBounds = new HashSet<string>(StringComparer.Ordinal)
            { "town_S4", "town_S6", "town_S7", "castle_S5", "castle_S6", "castle_N6", "ROT_castle45" };

        /// <summary>D.4: wsie za Murem (18 wsi 7 warowni z WallBounds) wedlug typu, przy starcie sesji. Po D.4: farm zboza 0,
        /// traperzy 5, rybacy 4 (przy wylaczonym VillageClimateFix: zboze 4, traperzy 3, rybacy 2). Naglowek zalezy od wylacznika
        /// (on = "D.4 czynne"), zeby autotest nie bral linii WYLACZONY za potwierdzenie; autotest sprawdza "D.4 czynne" i "farm zboza 0".</summary>
        private static string BeyondWall(bool on, ref int stumbles)
        {
            string head = on ? "ZA MUREM (D.4 czynne, myslistwo i ryby)" : "ZA MUREM (D.4 wylaczone, typy z mapy)";
            try
            {
                var all = Village.All;
                if (all == null) return head + ": brak listy wsi";
                var by = new SortedDictionary<string, int>(StringComparer.Ordinal);
                int n = 0;
                foreach (var v in all)
                {
                    var b = v != null ? v.Bound : null;
                    if (b == null || b.StringId == null || !WallBounds.Contains(b.StringId)) continue;
                    n++;
                    string t = v.VillageType != null ? v.VillageType.StringId : "null";
                    int c;
                    by.TryGetValue(t, out c);
                    by[t] = c + 1;
                }
                int wheat;
                by.TryGetValue("wheat_farm", out wheat);
                var parts = new List<string>();
                foreach (var kv in by) parts.Add(kv.Key + " " + kv.Value);
                return head + ": wsi " + n + " (" + string.Join(", ", parts.ToArray()) + "), farm zboza " + wheat
                       + " (zboze 3 z kazdej wsi zostaje - osobny krok N3)";
            }
            catch (Exception e)
            {
                stumbles++;
                if (stumbles <= 3) Log.Error("VillageClimate.BeyondWall", e);
                return head + ": blad liczenia";
            }
        }

        private static ItemObject CottonItem()
        {
            return MBObjectManager.Instance != null ? MBObjectManager.Instance.GetObject<ItemObject>("cotton") : null;
        }

        /// <summary>Bawelna wszystkich wsi swiata teraz wedlug modelu produkcji wsi wpietego w kampanie (gra z postfiksami BK/BE, budynki,
        /// wies zlupiona = 0). Jedno liczenie przy starcie sesji, bez losowania (cotton to nie wierzchowiec).</summary>
        private static string LiveCotton(ref int stumbles)
        {
            try
            {
                var cotton = CottonItem();
                var model = Campaign.Current != null && Campaign.Current.Models != null ? Campaign.Current.Models.VillageProductionCalculatorModel : null;
                var all = Village.All;
                if (cotton == null || model == null || all == null) return "model produkcji: brak (nie policzone)";
                float sum = 0f;
                int n = 0, bad = 0;
                foreach (var v in all)
                {
                    try
                    {
                        if (v == null || v.VillageType == null || v.VillageType.GetProductionPerDay(cotton) <= 0f) continue;
                        sum += model.CalculateDailyProductionAmount(v, cotton).ResultNumber;
                        n++;
                    }
                    catch (Exception e) { bad++; stumbles++; if (stumbles <= 3) Log.Error("VillageClimate.LiveCotton", e); }
                }
                return "wedlug modelu produkcji teraz " + sum.ToString("0.#") + "/d z " + n + " wsi" + (bad > 0 ? " (bez " + bad + " - blad)" : "");
            }
            catch (Exception e)
            {
                stumbles++;
                if (stumbles <= 3) Log.Error("VillageClimate.LiveCotton", e);
                return "model produkcji: blad liczenia";
            }
        }

        /// <summary>Wiersze wycofane z tabeli: typ wsi wrocil z mapy (nie idzie do zapisu), klasa wsi BK z poprzedniej sesji (klasa typu "jest")
        /// wraca do klasy typu z mapy. Niezaleznie od wylacznika - to sprzatanie po starszej wersji T8.</summary>
        private static string AlignRetired(out int stumbles)
        {
            var bk = new BkClass();
            int st0 = 0;
            stumbles = 0;
            foreach (var row in Retired)
            {
                try
                {
                    var st = Settlement.Find(row[0]);
                    if (st == null || st.Village == null) continue;
                    var cur = st.Village.VillageType;
                    if (cur == null || cur.StringId != row[1]) continue;   // ktos inny zmienil typ - nie ruszamy
                    bk.Align(st, row[3], Type(row[2]), cur);
                }
                catch (Exception e) { st0++; if (st0 <= 3) Log.Error("VillageClimate.AlignRetired(" + row[0] + ")", e); }
            }
            var names = new List<string>();
            foreach (var row in Retired) names.Add(row[3]);
            stumbles = st0 + bk.Stumbles;
            return "wycofane z tabeli (typ z mapy ROT): " + string.Join(", ", names.ToArray()) + " - " + bk.Summary();
        }

        private static VillageType Type(string id)
        {
            return MBObjectManager.Instance != null ? MBObjectManager.Instance.GetObject<VillageType>(id) : null;
        }

        /// <summary>Klasa wsi BK (LandData.VillageClass) przez refleksje - tylko przy starcie sesji, wsie z tabeli i wycofane. Bez BK nic nie robi.</summary>
        private sealed class BkClass
        {
            private readonly object _pm;
            private readonly MethodInfo _getPop, _getClass;
            private readonly bool _ok;
            private PropertyInfo _land, _cls;
            private readonly List<string> _changed = new List<string>();
            private int _noData, _unset, _foreign, _same;
            internal int Stumbles;

            internal BkClass()
            {
                try
                {
                    var tCfg = QuartermasterLaw.FindType("BannerKings.BannerKingsConfig");
                    var tCls = QuartermasterLaw.FindType("BannerKings.CampaignContent.Economy.Layered.DefaultVillageClasses");
                    if (tCfg == null || tCls == null) return;
                    var inst = AccessTools.Property(tCfg, "Instance")?.GetValue(null, null);
                    _pm = inst != null ? (AccessTools.Property(tCfg, "PopulationManager")?.GetValue(inst, null) ?? AccessTools.Field(tCfg, "PopulationManager")?.GetValue(inst)) : null;
                    _getPop = _pm != null ? AccessTools.Method(_pm.GetType(), "GetPopData", new[] { typeof(Settlement) }) : null;
                    _getClass = AccessTools.Method(tCls, "GetClass", new[] { typeof(VillageType) });
                    _ok = _pm != null && _getPop != null && _getClass != null && _getClass.IsStatic;
                }
                catch (Exception e) { _ok = false; Stumbles++; Log.Error("VillageClimate.BkClass", e); }
            }

            /// <summary>Klasa wsi = klasa typu "from" -> klasa typu "to". Inna klasa (dekret BK, inny mod) - bez zmian.</summary>
            internal void Align(Settlement st, string name, VillageType from, VillageType to)
            {
                if (!_ok || st == null || from == null || to == null) return;
                try
                {
                    var data = _getPop.Invoke(_pm, new object[] { st });
                    if (data == null) { _noData++; return; }
                    if (_land == null) _land = AccessTools.Property(data.GetType(), "LandData");
                    var land = _land != null ? _land.GetValue(data, null) : null;
                    if (land == null) { _noData++; return; }
                    if (_cls == null) _cls = AccessTools.Property(land.GetType(), "VillageClass");
                    if (_cls == null || !_cls.CanWrite) { _noData++; return; }
                    var cur = _cls.GetValue(land, null);
                    var cFrom = _getClass.Invoke(null, new object[] { from });
                    var cTo = _getClass.Invoke(null, new object[] { to });
                    if (cur == null || cFrom == null || cTo == null) { _noData++; return; }
                    if (Convert.ToInt32(cur) == 0) { _unset++; return; }      // Unset - BK ustawi sam z biezacego typu
                    if (cur.Equals(cTo)) { _same++; return; }
                    if (!cur.Equals(cFrom)) { _foreign++; return; }            // dekret BK albo inny mod - nie ruszamy
                    _cls.SetValue(land, cTo, null);
                    _changed.Add(name + " " + cur + " -> " + cTo);
                }
                catch (Exception e) { Stumbles++; if (Stumbles <= 3) Log.Error("VillageClimate.BkClass.Align", e); }
            }

            internal string Summary()
            {
                if (!_ok) return "klasa wsi BK: BRAK (BannerKings nieobecny albo inna postac PopulationManager / DefaultVillageClasses - klasa nie wyrownana)";
                return "klasa wsi BK: wyrownana " + _changed.Count + (_changed.Count > 0 ? " (" + string.Join("; ", _changed.ToArray()) + ")" : "")
                       + ", juz zgodna " + _same + ", Unset (BK ustawi z typu) " + _unset + ", bez danych BK " + _noData
                       + ", inna (dekret BK albo inny mod - NIE ruszane) " + _foreign;
            }
        }
    }
}
