using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// T8 (noc 08/09.10): KLIMAT WSI - towar glowny wsi, ktory nie moze rosnac tam, gdzie wies stoi (Jeff: "czy rozklad produkcji ma sens
    /// i jest uzasadniony regionem"). Audyt 02 tabela D.1: 12 wsi z towarem niemozliwym w klimacie (bawelna przy Murze, w Braavos, w gorach
    /// Doliny, w Sarnorze, w Dorzeczu, w Krolewskim Lesie, w Norvos i Ziemiach Korony; winnica w Lorath; daktyle na Tarth) dostaje towar
    /// swojej krainy; D.2: 5 cieplych wsi (Qarth 2, delta Rhoyne, Lys, Tyrosh) przechodzi na bawelne. Poprawka (autotest S1): 5 wsi za 10
    /// zabranych to bylo za malo (bawelna ze wsi 75-80/d wobec 110-130/d, aksamit -60%) - D.3 doklada 5 cieplych wsi (Selhorys, Myr, Meereen,
    /// Chroyane, Sunspear) z towarow, ktorych D.1 dodala. Bawelna 13 -> 13 wsi (10 zabranych, 10 dodanych), wszystkie w strefie cieplej -
    /// suma swiata bez zmian. 4 farmy zboza za Murem z D.1 ZOSTAJA (pytanie do Jeffa 6.2-1).
    /// Gdzie: typ wsi (Village.VillageType) to zwykle pole BEZ zapisu w grze - gra ustawia je z settlements.xml przy kazdym wczytaniu
    /// (Village.Deserialize), a wszyscy czytaja je na biezaco (model produkcji gry, lista BK GetProductions, magazyn i tabor wsi,
    /// nasze GoodsLedger, VillageWoodlot, VillageClogDiag). Jedno miejsce: podmiana raz przy starcie sesji (ArmouryBehavior.OnSessionLaunched,
    /// po McmSettings.Apply), jak NorthernFare w CrashScribe (inne wsie - bez wspolnych wpisow). Podmieniamy tylko, gdy wies ma dzis typ
    /// z tabeli ("bylo") - jesli ktos inny juz go zmienil, nie ruszamy (linia logu to pokaze). Pliki ROT bez zmian.
    /// KLASA WSI BK (recenzja T8): BK trzyma w SWOIM zapisie klase wsi (LandData.VillageClass, [SaveableProperty(8)]) ustalona z typu wsi
    /// (DefaultVillageClasses.GetClass) i wpisuje ja tylko, gdy jest Unset - po zmianie typu klasa zostalaby stara (dochod majatkow BK,
    /// klastry, polityka majatkow AI). Dlatego przy kazdym starcie sesji, dla 22 wsi z tabeli, wyrownujemy ja do biezacego typu: gdy klasa
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
            // D.1 - towar niemozliwy w klimacie (bez 4 farm za Murem)
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
            // D.2 - wyrownanie bawelny w cieplych wsiach
            new[] { "ROT_town35_village2",   "lumberjack", "silk_plant",         "Shirosi (Qarth)" },
            new[] { "ROT_town37_village2",   "lumberjack", "silk_plant",         "Port Yhos (Qarth)" },
            new[] { "village_ES4_1",         "lumberjack", "silk_plant",         "Sagora (Volantis, delta Rhoyne)" },
            new[] { "ROT_town11_village1",   "date_farm",  "silk_plant",         "Abar (Lys)" },
            new[] { "ROT_castle47_village2", "date_farm",  "silk_plant",         "Tyrono (Tyrosh)" },
            // D.3 (poprawka noc 08/09.10, autotest S1): D.2 dawala 5 wsi za 10 zabranych - bawelna ze wsi 75-80/d wobec 110-130/d bez T8.
            // Jedna wies bawelny daje ok. 9.5-10 bel na dobe niezaleznie od miejsca (8 wsi 78/d, 13 wsi 122/d), wiec brakuje dokladnie 5 wsi.
            // Biore je z towarow, ktorych D.1 dodala (owce +3, len +3, bydlo +2, ryby +2) - bilans tych towarow wraca blizej zera.
            new[] { "village_ES5_1",         "sheep_farm", "silk_plant",         "Lanthas (Selhorys, dolina Rhoyne)" },
            new[] { "ROT_town12_village2",   "sheep_farm", "silk_plant",         "Tasko (Myr)" },
            new[] { "village_K1_2",          "flax_plant", "silk_plant",         "Ulaan (Meereen, Zatoka Niewolnicza)" },
            new[] { "castle_village_A8_1",   "cattle_farm","silk_plant",         "Tamnuh (Chroyane, Rhoyne)" },
            new[] { "village_A1_1",          "fisherman",  "silk_plant",         "Spottswood Village (Sunspear, ujscie Greenblood)" },
        };

        /// <summary>Raz przy starcie sesji: podmiana typu wsi wedlug tabeli (wylacznik VillageClimateFix), wyrownanie klasy wsi BK do
        /// biezacego typu i jedna linia logu "wies: bylo -> jest".</summary>
        internal static void Apply()
        {
            var s = Settings.Current;
            if (s == null) return;
            var bk = new BkClass();
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
                         + "wsi bawelny na mapie " + CountType(Cotton, ref st0) + "; " + bk.Summary() + "; potkniecia " + (st0 + bk.Stumbles) + ".");
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
            line += "; za Murem bez zmian (pytanie do Jeffa); " + bk.Summary() + "; potkniecia " + (stumbles + bk.Stumbles)
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
                int taken = 0, added = 0;
                var delta = new SortedDictionary<string, int>(StringComparer.Ordinal);
                foreach (var row in applied)
                {
                    if (row[1] == Cotton) taken++;
                    if (row[2] == Cotton) added++;
                    int v;
                    delta.TryGetValue(row[1], out v); delta[row[1]] = v - 1;
                    delta.TryGetValue(row[2], out v); delta[row[2]] = v + 1;
                }
                var parts = new List<string>();
                foreach (var kv in delta)
                    if (kv.Key != Cotton) parts.Add(kv.Key + " " + (kv.Value > 0 ? "+" : "") + kv.Value);
                return "BILANS bawelny: zabrano " + taken + " wsi, dodano " + added + " (netto " + (added - taken >= 0 ? "+" : "") + (added - taken)
                       + "; wsi bawelny na mapie teraz " + CountType(Cotton, ref stumbles) + ", ok. 9.5-10 bel na wies na dobe; plan 10 zabranych / 10 dodanych)"
                       + "; inne typy (liczba wsi): " + (parts.Count > 0 ? string.Join(", ", parts.ToArray()) : "bez zmian");
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

        private static VillageType Type(string id)
        {
            return MBObjectManager.Instance != null ? MBObjectManager.Instance.GetObject<VillageType>(id) : null;
        }

        /// <summary>Klasa wsi BK (LandData.VillageClass) przez refleksje - tylko przy starcie sesji, 22 wsie z tabeli. Bez BK nic nie robi.</summary>
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
