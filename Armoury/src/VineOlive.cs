using System;
using System.Collections.Generic;
using System.Globalization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// B6 (PLAN rozdz. 2 pyt. J "Proporcje winnic i oliwek? - przed 6.9"; Jeff: "rzeczywiste proporcje ustal"; liczby decyduje Claude - zasada Jeffa) - WINNICE,
    /// GAJE OLIWNE I PALMY DAKTYLOWE POLUDNIA WEDLUG KLIMATU I KANONU. Klucz VineOliveByClimate.
    ///
    /// JAK JEST (settlements.xml ROT po T8 i GL-5): winnic 23 (Dorzecze 6, Ziemie Burzy 5, Reach 2 - obie na Arbor, Dorne 1, Lys 0, Volantis 0), gajow 19
    /// (Dorne 6, Reach 5, Volantis 2, Ziemie Burzy 2, Lys / Tyrosh / Myr po 1), palm daktylowych 14 (Dorne 4, Lys 3, Tyrosh 3, Volantis 1). Reach ma wiecej
    /// gajow niz winnic, choc w kanonie to kraina wina (Arbor - "most Westerosi wine comes from the south", Redwyne), a oliwki sa "chiefly from Dorne"
    /// (koncordancja westeros.org, gaje nawadniane z rzek). Lys i Volantis maja w kanonie wina (Lys - slodkie czerwone i biale, Volantis - slodkie czerwone;
    /// AWOIAF Wine / Strongwine), a nie maja ani jednej winnicy; maja za to palmy daktylowe.
    ///
    /// KLIMAT (realne odpowiedniki, audyt 02 rozdz. 2.4): oliwka ginie przy mrozie ok. -8 C i wiosennych przymrozkach - tylko cieple wybrzeze i niziny;
    /// palma daktylowa chce > 2 000 jednostek ciepla i srednia najzimniejszego miesiaca > 2 C - pustynne niziny i oazy (w Europie srodziemnomorskiej tylko gaj
    /// Elche); winorosl dojrzewa od Morza Srodziemnego po pas Burgundii i Nadrenii, na stokach i wzgorzach. Triada srodziemnomorska: zboze, winorosl, oliwka
    /// (Hoffman, Environmental History of Medieval Europe) - liczb udzialu dla sredniowiecza nie znalazlem, wiec regula jest jakosciowa: zboze bez zmian
    /// ("spichlerz swiata" - audyt 02 C5), zmieniamy tylko upraw, ktore stoja w zlym klimacie albo wbrew kanonowi.
    ///
    /// REGULA (8 wsi; zboze, ryby, konie, rudy, drewno, len, bawelna bez zmian; liczba gajow swiata bez zmian - oliwki maja juz niedobor):
    ///  - Reach: 2 z 5 gajow -> winnice: Dunstonbury (pod Hornhill, y 210 - najdalej na polnoc i w glebi ladu, gdzie oliwke bierze mroz) i Redwyne Bay (pod Oldtown,
    ///    nazwa i rod Redwyne'ow z Arbor); zostaja 3 gaje na wybrzezu (Westshore, Whispering Sound, Sunflower). Reach: winnice 2 -> 4, gaje 5 -> 3.
    ///  - Dorne: 2 palmy daktylowe na przeleczach Gor Czerwonych (Boneway pod Kingsgrave, Vulture's Roost pod Wyl - oba y 181, najwyzej w Dorne; palma w gorach
    ///    nie owocuje, winorosl na stoku tak - "Dornish red", strongwine) -> winnice; daktyle zostaja na nizinnym wybrzezu (Seaspray, Water Gardens), gaje
    ///    oliwne Dorne 6 bez zmian (kanon: oliwki glownie z Dorne). Dorne: winnice 1 -> 3, gaje 6, daktyle 4 -> 2.
    ///  - Lys: 2 z 3 palm - Mussum (pod Lys) -> winnica (wino Lys), Asmait (pod Alnor) -> gaj oliwny; zostaje Abar. Lys: winnice 0 -> 1, gaje 1 -> 2, daktyle 3 -> 1.
    ///  - Volantis: jedyna palma (Krysaro pod Demongate) -> winnica (wino Volantis). Volantis: winnice 0 -> 1, gaje 2, daktyle 1 -> 0.
    ///  - Tyrosh (wyspa; w kanonie gruszkowka, nie wino): 1 z 3 palm (Waltas pod Tyrosh) -> gaj oliwny. Tyrosh: winnice 1, gaje 1 -> 2, daktyle 3 -> 2.
    ///  - Myr (zielone wina w kanonie) bez zmian: nie ma tam uprawy w zlym klimacie (zboze, ryby, konie, drewno w deficycie, bawelna po GL-5 na styk);
    ///    Pentos (wina "pale ambers", 2 winnice), Ziemie Burzy i Dorzecze - bez zmian (poza zakresem pytania J).
    /// Swiat: winnice 23 -> 29 (suma (poziom ognisk + 1) / 2: 23.5 -> 29.5, x1.26), gaje 19 -> 19 (bez zmian), palmy 14 -> 8 (14.0 -> 8.0, x0.57).
    /// Bieg 120 dob (Armoury-2026-10-14_02-50-28; zapas swiata d1 -> d120, plon wsi srednio): winogrona 20.3 -> 11.0 tys. (ok. -78/d), wsie 271/d - NIEDOBOR;
    /// oliwki 18.1 -> 13.6 tys. (-37/d), wsie 260/d; daktyle 7.8 -> 10.5 tys. (+23/d), wsie 109/d - NADMIAR. Oczekiwane z B6 (srednio 11.8 winogron, 7.8 daktyli
    /// na wies): winogrona ok. 340/d (bilans ok. -9/d), oliwki bez zmian, daktyle ok. 62/d (bilans ok. -24/d - zapas 10.5 tys. na ponad rok). Winogrona, oliwki
    /// i daktyle to zywnosc - regulator wsi (HarvestValve) ich nie rusza, zmiana plonu jest pelna.
    ///
    /// MECHANIZM jak wsie lniane GL-5 (GoodsRates): typ wsi nie idzie do zapisu gry (gra ustawia go z settlements.xml przy kazdym wczytaniu) - podmiana raz przy
    /// starcie sesji, PO VillageClimate.Apply i GoodsRates.Apply, PRZED kalibracja plonu P1 w 1. dobie nowej kampanii; klasa wsi BK wyrownana do typu
    /// (VillageClimate.BkClass). TYLKO KAMPANIA ZACZETA Z KLUCZEM (flaga arm_vineolive, SaveText.Sync): P1 (VillageByPeople) trzyma g(typ) z kalibracji -
    /// podmiana na starym zapisie zmienilaby sume swiata typow; w kampanii zaczetej z kluczem podmiana NA STALE (takze po wylaczeniu klucza - jak len GL-5).
    /// Stary zapis (brak pola) i kampania zaczeta bez klucza - nigdy. Wies z innym typem niz w tabeli (inny mod, T8 wylaczone) - nie ruszamy.
    /// Dymek przysiolka: zdanie zajecia starego typu (gaj, palmy) -> zdanie nowego typu (plik lore zajecia.csv: winnica Reach, Dorne, niewolnicy Essos; gaj ogolny).
    /// Warsztaty miast nowej kampanii gra buduje przed startem sesji (z typow mapy) - tlocznie wina przy nowych winnicach nie powstaja; winogrona ida na targ.
    /// Obieg: zloto nie rusza; inny towar tych samych ludzi wsi (plon P1 od ludzi), sprzedaz wozem jak dotad.
    /// </summary>
    internal static class VineOlive
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private const string Vine = "vineyard", Olive = "olive_trees", Date = "date_farm";
        private const string LoreReach = "Vintners: the vines of the Reach and a wine press.";
        private const string LoreDorne = "Vintners: the vines that make Dornish red.";
        private const string LoreSlaves = "Slaves in the vineyards and a master's press.";
        private const string LoreOlive = "Olive growers: groves and an oil press.";

        // id wsi, typ dzis (settlements.xml ROT, po T8 i GL-5), typ nowy, kraina, nazwa i powod (log), zdanie zajecia nowego typu (dymek przysiolka; plik zajecia.csv)
        private static readonly string[][] Table =
        {
            new[] { "castle_village_V1_1",   Olive, Vine,  "The Reach", "Dunstonbury (Hornhill, y 210 - w glebi ladu)",     LoreReach },
            new[] { "village_V6_3",          Olive, Vine,  "The Reach", "Redwyne Bay (Oldtown, rod Redwyne)",               LoreReach },
            new[] { "castle_village_A4_1",   Date,  Vine,  "Dorne",     "Boneway (Kingsgrave, przelecz Gor Czerwonych)",    LoreDorne },
            new[] { "castle_village_A7_1",   Date,  Vine,  "Dorne",     "Vulture's Roost (Wyl, przelecz Gor Czerwonych)",   LoreDorne },
            new[] { "village_A4_2",          Date,  Vine,  "Lys",       "Mussum (Lys)",                                     LoreSlaves },
            new[] { "castle_village_A2_2",   Date,  Olive, "Lys",       "Asmait (Alnor)",                                   LoreOlive },
            new[] { "ROT_castle38_village2", Date,  Vine,  "Volantis",  "Krysaro (Demongate)",                              LoreSlaves },
            new[] { "village_A5_3",          Date,  Olive, "Tyrosh",    "Waltas (Tyrosh, wyspa)",                           LoreOlive },
        };

        // stan kampanii (zapis arm_vineolive "1|stan|dzien"): 0 = zapis sprzed paczki - bez podmian; 1 = kampania zaczeta z kluczem - podmiany na stale; 2 = zaczeta bez klucza - nigdy
        private static int _state, _stateDay = -1;
        private static bool _newGame;
        private static int _applied, _stumbles, _loreN, _loreStumbles;
        // wsie podmienione w tej sesji: id wsi -> (typ przed, typ nowy, zdanie nowego typu); jedna referencja, slownik nie zmieniany po publikacji
        private static volatile Dictionary<string, string[]> _swapped = new Dictionary<string, string[]>(StringComparer.Ordinal);

        internal static void Reset()
        {
            _state = 0; _stateDay = -1; _newGame = false; _applied = 0; _stumbles = 0; _loreN = 0; _loreStumbles = 0;
            _swapped = new Dictionary<string, string[]>(StringComparer.Ordinal);
        }

        internal static bool KeyOn { get { var s = Settings.Current; return s != null && s.VineOliveByClimate; } }

        private static int Today() { return (int)CampaignTime.Now.ToDays; }

        /// <summary>OnNewGameCreatedEvent: nowa kampania - stan ustali start sesji (klucz w tej chwili).</summary>
        internal static void OnNewGame() { _newGame = true; }

        internal static string Export() { return "1|" + _state.ToString(Inv) + "|" + _stateDay.ToString(Inv); }

        /// <summary>Wczytanie: brak pola (zapis sprzed paczki), inny format, blad - kampania w toku: bez podmian.</summary>
        internal static void Import(string v)
        {
            _state = 0; _stateDay = -1;
            if (string.IsNullOrEmpty(v)) return;
            try
            {
                var p = v.Split('|');
                if (p.Length < 3 || p[0] != "1") return;
                int st = int.Parse(p[1], Inv);
                _state = st >= 0 && st <= 2 ? st : 0;
                _stateDay = int.Parse(p[2], Inv);
            }
            catch (Exception e) { _state = 0; _stateDay = -1; _stumbles++; Log.Error("VineOlive.Import", e); }
        }

        private static VillageType Type(string id) { return MBObjectManager.Instance != null ? MBObjectManager.Instance.GetObject<VillageType>(id) : null; }

        /// <summary>Start sesji (watek glowny, PO VillageClimate.Apply i GoodsRates.Apply, PRZED kalibracja P1): podmiana typow z tabeli w kampanii zaczetej z kluczem.</summary>
        internal static void Apply()
        {
            try
            {
                if (_newGame) { _newGame = false; _state = KeyOn ? 1 : 2; _stateDay = Today(); }
                bool swap = _state == 1;
                var bk = new VillageClimate.BkClass();
                var done = new List<string>();
                var already = new List<string>();
                var other = new List<string>();
                var missing = new List<string>();
                var swapped = new Dictionary<string, string[]>(StringComparer.Ordinal);
                int n = 0;
                if (swap)
                {
                    foreach (var row in Table)
                    {
                        try
                        {
                            var st = Settlement.Find(row[0]);
                            if (st == null || st.Village == null) { missing.Add(row[0]); continue; }
                            var cur = st.Village.VillageType;
                            string curId = cur != null ? cur.StringId : "null";
                            var from = Type(row[1]); var to = Type(row[2]);
                            if (from == null || to == null) { missing.Add("typ " + row[1] + "/" + row[2]); continue; }
                            if (curId == row[2]) { already.Add(row[4]); bk.Align(st, row[4], from, cur); swapped[row[0]] = new[] { row[1], row[2], row[5] }; n++; continue; }
                            if (curId != row[1]) { other.Add(row[4] + " ma " + curId); continue; }   // ktos inny zmienil typ - nie ruszamy
                            st.Village.VillageType = to;
                            bk.Align(st, row[4], cur, to);
                            swapped[row[0]] = new[] { row[1], row[2], row[5] };
                            done.Add(row[4] + " (" + row[0] + ", " + row[3] + "): " + row[1] + " -> " + row[2]);
                            n++;
                        }
                        catch (Exception e) { _stumbles++; if (_stumbles <= 3) Log.Error("VineOlive.Apply(" + row[0] + ")", e); }
                    }
                }
                _swapped = swapped;
                _applied = n;
                Log.Info("Winnice i gaje (B6): start sesji, dzien " + Today() + " - Vine Olive By Climate " + (KeyOn ? "tak" : "nie") + "; " + StateText() + "; "
                         + (swap ? "podmienione teraz " + done.Count + (done.Count > 0 ? " - " + string.Join("; ", done.ToArray()) : "")
                                 + (already.Count > 0 ? "; juz z nowym typem: " + string.Join(", ", already.ToArray()) : "")
                                 + (other.Count > 0 ? "; inny typ niz w tabeli (NIE ruszane): " + string.Join(", ", other.ToArray()) : "")
                               : "bez podmian (typy z mapy)")
                         + (missing.Count > 0 ? "; brak w tej kampanii: " + string.Join(", ", missing.ToArray()) : "")
                         + "; " + MapText() + "; z tabeli podmienione " + n + " z " + Table.Length + "; " + bk.Summary() + "; potkniecia " + (_stumbles + bk.Stumbles)
                         + ". Typ wsi nie idzie do zapisu gry, klasa wsi BK idzie (wyrownana do typu).");
            }
            catch (Exception e) { _stumbles++; Log.Error("VineOlive.Apply", e); }
        }

        /// <summary>Liczba wsi trzech typow na mapie i w krainach poludnia (kultura osady: reach, aserai = Dorne, lyseni, volantine).</summary>
        private static string MapText()
        {
            int vine = 0, olive = 0, date = 0;
            var by = new SortedDictionary<string, int[]>(StringComparer.Ordinal);
            try
            {
                foreach (var v in Village.All)
                {
                    var t = v != null ? v.VillageType : null;
                    if (t == null) continue;
                    int k = t.StringId == Vine ? 0 : t.StringId == Olive ? 1 : t.StringId == Date ? 2 : -1;
                    if (k < 0) continue;
                    if (k == 0) vine++; else if (k == 1) olive++; else date++;
                    string c = v.Settlement != null && v.Settlement.Culture != null ? v.Settlement.Culture.StringId : "-";
                    if (c != "reach" && c != "aserai" && c != "lyseni" && c != "volantine") continue;
                    int[] a; if (!by.TryGetValue(c, out a)) by[c] = a = new int[3];
                    a[k]++;
                }
            }
            catch (Exception e) { _stumbles++; if (_stumbles <= 3) Log.Error("VineOlive.MapText", e); }
            var parts = new List<string>();
            foreach (var kv in by) parts.Add(kv.Key + " " + kv.Value[0] + "/" + kv.Value[1] + "/" + kv.Value[2]);
            return "na mapie winnice " + vine + ", gaje oliwne " + olive + ", palmy daktylowe " + date + " (krainy winnice/gaje/palmy: " + string.Join(", ", parts.ToArray()) + ")";
        }

        private static string StateText()
        {
            switch (_state)
            {
                case 1: return "kampania zaczeta z kluczem (dzien " + _stateDay + ") - CZYNNE na stale (P1 zmierzyl plon typow z tymi wsiami)";
                case 2: return "kampania zaczeta z kluczem wylaczonym (dzien " + _stateDay + ") - NIGDY (plon wsi od ludzi P1 ma g typow z kalibracji)";
                default: return "kampania sprzed paczki (stary zapis) - NIGDY (podmiana teraz zmienilaby sume swiata typow P1)";
            }
        }

        /// <summary>Zdanie zajecia przysiolka (lore E7 z typow mapy): we wsi podmienionej w tej sesji pierwsze zdanie gaju albo palm -> zdanie nowego typu
        /// (po angielsku). Wies niepodmieniona, inne zdanie (mlyn, spichlerz, rolnicy), blad - tekst bez zmian (blad liczony).</summary>
        internal static string Livelihood(Settlement district, string text)
        {
            try
            {
                var m = _swapped;
                string[] row;
                if (string.IsNullOrEmpty(text) || district == null || m.Count == 0 || !m.TryGetValue(district.StringId, out row)) return text;
                var t = district.Village != null ? district.Village.VillageType : null;
                if (t == null || t.StringId != row[1]) return text;
                int cut = text.IndexOf(". ", StringComparison.Ordinal);
                string main = cut >= 0 ? text.Substring(0, cut + 1) : text;
                string rest = cut >= 0 ? text.Substring(cut + 1) : "";
                bool hit = row[0] == Olive ? main.StartsWith("Olive growers", StringComparison.Ordinal)
                         : row[0] == Date && (main.StartsWith("Date growers", StringComparison.Ordinal) || main.StartsWith("Date palms", StringComparison.Ordinal));
                if (!hit) return text;
                _loreN++;
                return row[2] + rest;
            }
            catch (Exception e) { if (++_loreStumbles == 1) { try { Log.Error("VineOlive.Livelihood", e); } catch { } } return text; }
        }

        /// <summary>Raz na dobe (tylko log): stan i zdania lore. Bez linii, gdy w tej kampanii nie ma podmian (stan 0 / 2) - linia startu sesji wystarcza.</summary>
        internal static void Daily()
        {
            if (_state != 1 && _loreStumbles == 0) return;
            try
            {
                int ln = _loreN, ls = _loreStumbles; _loreN = 0; _loreStumbles = 0;
                Log.Info("Winnice i gaje (B6): dzien " + Today() + " - " + StateText() + " | z tabeli podmienione " + _applied + " z " + Table.Length + " | " + MapText()
                         + " | zdan lore przysiolkow podmienionych " + ln + " | potkniecia " + (_stumbles + ls) + ".");
                _stumbles = 0;
            }
            catch (Exception e) { _stumbles++; Log.Error("VineOlive.Daily", e); }
        }
    }
}
