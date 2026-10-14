using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// GL-5 / P5 (projekt docs/gospodarka-od-ludzi/PROJEKT-GOSPODARKA-OD-LUDZI-2026-10-13.md rozdz. 4A "Rachunek stawki lnu i soli", 8 P5; decyzja Jeffa pyt. 6:
    /// "len - NOWE SYMBOLE WSI LNIANYCH w ludnych krainach, nie mnoznik stawki; sol x2 jako kopalina") - STAWKI TOWAROW WEDLUG WZORCA. Klucz GoodsRatesByPattern.
    ///  (a) SOL x2.0: stawka wydobycia wsi solnych (typ salt_mine, towar salt) - czynnik w MaterialLaw.ProdPostfix wzorem "czynnik x suma", zaraz po czynniku
    ///      ludzi P1 (VillageByPeople.ProdStep), przed rekami spustoszenia i regulatorem swiata (HarvestValve 0.1-1.3 dalej dziala na wynik). Wzorzec w skali
    ///      targu 2 280 szt./d, dzis 511 - x2 to polowa drogi, reszte pokaze popyt (wsie kupuja 95% soli, zapas 5-8 dob, regulator na suficie od doby 57).
    ///      Kazda kampania (to stawka, nie zmiana mapy).
    ///  (b) LEN Z RAK LUDZI: 16 wsi w ludnych krainach bez lnu dostaje typ flax_plant (z 20 na 36 wsi lnianych; suma (poziom ognisk + 1) / 2 wsi lnianych
    ///      20.5 -> 37.0 = x1.80 - tyle, ile wzorzec: plotno cywilne 190 + wojsko 80 + warsztaty zbrojne 22 = ok. 290 szt./d = 870 lnu wobec 484). Wybor:
    ///      wsie, ktorych towar jest w nadmiarze - ryby (6.5 x wzorzec; 10 wsi rybakow), srebro (4 kopalnie: klejnoty bez odbiorcy, srebro na 67 dob zapasu),
    ///      drwale (2: drewno x14 w rok, brak ujscia) - przy sumie zywnosci wsi swiata -2.6% (ryby typu -15%). Ten sam mechanizm co klimat wsi T8
    ///      (VillageClimate): typ wsi nie idzie do zapisu gry (gra ustawia go z settlements.xml przy kazdym wczytaniu), wiec podmiana raz przy kazdym starcie
    ///      sesji, PO VillageClimate.Apply; klasa wsi BK (w zapisie BK) wyrownana do biezacego typu (VillageClimate.BkClass). Wies z innym typem niz w tabeli
    ///      (inny mod) - nie ruszamy. Dymek wsi pokazuje sam nowy towar glowny (typ); ikona wsi na mapie zalezy od kultury, nie od typu; nazwy bez zmian.
    ///      TYLKO NOWA KAMPANIA (flaga w zapisie arm_goodsrates, SaveText.Sync): plon wsi od ludzi P1 trzyma g(typ) z kalibracji (zapis arm_villagepeople) -
    ///      podmiana PO kalibracji na starym zapisie dalaby len x2.6 i ryby -34% (zywnosc -7%) zamiast x1.8 i -15%; w nowej kampanii kalibracja P1 (1. doba)
    ///      widzi juz nowe typy. Stary zapis (brak pola) i kampania zaczeta z kluczem wylaczonym - nigdy (sama sol x2 dziala).
    /// Klucz wylaczony: krok soli nic nie dopisuje, wsie wracaja do typu z mapy przy nastepnym wczytaniu (klasa BK wraca do typu z mapy), stan 97dd474.
    /// Obieg: zloto nie rusza; towar z pracy ludzi wsi (inny towar tych samych ludzi), sprzedaz wozem jak dotad.
    /// </summary>
    internal static class GoodsRates
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        internal const float SaltRate = 2f;
        private const string Flax = "flax_plant", SaltType = "salt_mine", SaltItem = "salt";
        private static readonly TextObject SaltText = new TextObject("{=!}Armoury: salt rate of the realm (x2)");

        // id wsi, typ dzis (settlements.xml ROT, po T8), krolestwo z dnia startu biegu 43, nazwa (log) - wybor i liczby: CHANGELOG (GL-5)
        private static readonly string[][] Table =
        {
            new[] { "ROT_town27_village2",   "fisherman",   "The Reach",      "Greenshield" },
            new[] { "village_ES7_2",         "fisherman",   "The Reach",      "Rosewood" },
            new[] { "village_V7_3",          "fisherman",   "The Reach",      "Redwine Straight" },
            new[] { "ROT_town27_village3",   "lumberjack",  "The Reach",      "Oakendale" },
            new[] { "village_ES3_2",         "fisherman",   "Volantis",       "Tevea" },
            new[] { "ROT_castle54_village1", "fisherman",   "Volantis",       "Ryona" },
            new[] { "ROT_town37_village3",   "silver_mine", "Qarth",          "Tolarra" },
            new[] { "ROT_town35_village3",   "silver_mine", "Qarth",          "Qalosh (Qarth)" },
            new[] { "village_V8_3",          "fisherman",   "King's Landing", "Feastfires" },
            new[] { "ROT_castle59_village2", "fisherman",   "King's Landing", "Straits" },
            new[] { "ROT_town29_village3",   "silver_mine", "King's Landing", "Manderstone" },
            new[] { "ROT_town14_village2",   "fisherman",   "Braavos",        "Theria" },
            new[] { "ROT_town14_village1",   "silver_mine", "Braavos",        "Galos" },
            new[] { "ROT_castle46_village2", "lumberjack",  "Pentos",         "Vasmo" },
            new[] { "castle_village_ES4_1",  "fisherman",   "Stormlands",     "Breaker's Bay" },
            new[] { "ROT_town28_village2",   "fisherman",   "Stormlands",     "Cape Wrath" },
        };

        // ------------------------------------------------------------ stan kampanii (zapis: arm_goodsrates "1|stan|dzien")
        // stan lnu: 0 = kampania sprzed paczki (stary zapis) - bez podmian; 1 = kampania zaczeta z kluczem - podmiany przy kluczu; 2 = zaczeta bez klucza - nigdy
        private static int _flax;
        private static int _flaxDay = -1;
        private static bool _newGame;
        private static int _applied, _stumbles, _readStumbles;
        private static string _start = "";
        private static bool _offLogged;
        private static long _saltCalls;

        internal static void Reset() { _flax = 0; _flaxDay = -1; _newGame = false; _applied = 0; _stumbles = 0; _readStumbles = 0; _start = ""; _offLogged = false; _saltCalls = 0; }

        internal static bool KeyOn { get { var s = Settings.Current; return s != null && s.GoodsRatesByPattern; } }

        private static int Today() { return (int)CampaignTime.Now.ToDays; }

        /// <summary>OnNewGameCreatedEvent: nowa kampania - stan lnu ustali start sesji (klucz w tej chwili).</summary>
        internal static void OnNewGame() { _newGame = true; }

        internal static string Export() { return "1|" + _flax.ToString(Inv) + "|" + _flaxDay.ToString(Inv); }

        /// <summary>Wczytanie: brak pola (zapis sprzed paczki), inny format, blad - kampania w toku: bez podmian lnu (sol x2 dziala przy kluczu).</summary>
        internal static void Import(string v)
        {
            _flax = 0; _flaxDay = -1;
            if (string.IsNullOrEmpty(v)) return;
            try
            {
                var p = v.Split('|');
                if (p.Length < 3 || p[0] != "1") return;
                int st = int.Parse(p[1], Inv);
                _flax = st >= 0 && st <= 2 ? st : 0;
                _flaxDay = int.Parse(p[2], Inv);
            }
            catch (Exception e) { _flax = 0; _flaxDay = -1; _stumbles++; Log.Error("GoodsRates.Import", e); }
        }

        // ============================================================ (a) sol - MaterialLaw.ProdPostfix (dowolny watek)

        /// <summary>MaterialLaw.ProdPostfix, zaraz po kroku P1: wies solna, towar sol - czynnik x2.0 wzorem "czynnik x suma". Klucz wylaczony - nic. Nie rzuca.</summary>
        internal static void ProdStep(Village village, ItemObject item, ref ExplainedNumber r)
        {
            if (!KeyOn) return;                                     // klucz wylaczony: nic (stan 97dd474)
            try
            {
                if (village == null || item == null || item.StringId != SaltItem) return;
                var t = village.VillageType;
                if (t == null || t.StringId != SaltType) return;
                float sum = 1f + r.SumOfFactors;
                if (sum > 0f) r.AddFactor((SaltRate - 1f) * sum, SaltText);
                Interlocked.Increment(ref _saltCalls);
            }
            catch (Exception e) { if (Interlocked.Increment(ref _readStumbles) == 1) { try { Log.Error("GoodsRates.ProdStep", e); } catch { } } }
        }

        // ============================================================ (b) len - start sesji (watek glowny, PO VillageClimate.Apply)

        private static VillageType Type(string id) { return MBObjectManager.Instance != null ? MBObjectManager.Instance.GetObject<VillageType>(id) : null; }

        internal static void Apply()
        {
            try
            {
                if (_newGame) { _newGame = false; _flax = KeyOn ? 1 : 2; _flaxDay = Today(); }
                bool swap = KeyOn && _flax == 1;
                var bk = new VillageClimate.BkClass();
                var flax = Type(Flax);
                var done = new List<string>();
                var already = new List<string>();
                var other = new List<string>();
                var missing = new List<string>();
                var delta = new SortedDictionary<string, int>(StringComparer.Ordinal);
                int n = 0;
                foreach (var row in Table)
                {
                    try
                    {
                        var st = Settlement.Find(row[0]);
                        if (st == null || st.Village == null) { missing.Add(row[0]); continue; }
                        var cur = st.Village.VillageType;
                        string curId = cur != null ? cur.StringId : "null";
                        var from = Type(row[1]);
                        if (from == null || flax == null) { missing.Add("typ " + row[1]); continue; }
                        if (swap)
                        {
                            if (curId == Flax) { already.Add(row[3]); bk.Align(st, row[3], from, cur); n++; continue; }
                            if (curId != row[1]) { other.Add(row[3] + " ma " + curId); continue; }   // ktos inny zmienil typ - nie ruszamy
                            st.Village.VillageType = flax;
                            bk.Align(st, row[3], cur, flax);
                            done.Add(row[3] + " (" + row[0] + ", " + row[2] + "): " + row[1] + " -> " + Flax);
                            int v; delta.TryGetValue(row[1], out v); delta[row[1]] = v - 1;
                            n++;
                        }
                        else if (curId == row[1]) bk.Align(st, row[3], flax, cur);         // typ z mapy; klasa BK z sesji z podmiana wraca do typu z mapy
                    }
                    catch (Exception e) { _stumbles++; if (_stumbles <= 3) Log.Error("GoodsRates.Apply(" + row[0] + ")", e); }
                }
                _applied = n;
                if (n > 0) delta[Flax] = done.Count;
                int onMap = 0;
                try { foreach (var v in Village.All) if (v != null && v.VillageType != null && v.VillageType.StringId == Flax) onMap++; } catch { }
                var parts = new List<string>();
                foreach (var kv in delta) if (kv.Value != 0) parts.Add(kv.Key + " " + (kv.Value > 0 ? "+" : "") + kv.Value);
                _start = "wsie lniane na mapie " + onMap + " (z tabeli z lnem " + n + " z " + Table.Length + ")";
                Log.Info("Stawki towarow (GL5): start sesji, dzien " + Today() + " - Goods Rates By Pattern " + (KeyOn ? "tak" : "nie") + "; sol x" + SaltRate.ToString("0.0", Inv)
                         + " (stawka wsi solnych, kazda kampania); len - nowe wsie lniane: " + StateText() + "; "
                         + (swap ? "podmienione teraz " + done.Count + (done.Count > 0 ? " - " + string.Join("; ", done.ToArray()) : "") + (already.Count > 0 ? "; juz z lnem: " + string.Join(", ", already.ToArray()) : "")
                                 + (other.Count > 0 ? "; inny typ niz w tabeli (NIE ruszane): " + string.Join(", ", other.ToArray()) : "")
                                 + "; bilans typow (dzis podmienione): " + (parts.Count > 0 ? string.Join(", ", parts.ToArray()) : "bez zmian")
                               : "bez podmian (typy z mapy)")
                         + (missing.Count > 0 ? "; brak w tej kampanii: " + string.Join(", ", missing.ToArray()) : "")
                         + "; " + _start + "; " + bk.Summary() + "; potkniecia " + (_stumbles + bk.Stumbles) + ". Typ wsi nie idzie do zapisu gry, klasa wsi BK idzie (wyrownana do typu).");
            }
            catch (Exception e) { _stumbles++; Log.Error("GoodsRates.Apply", e); }
        }

        private static string StateText()
        {
            switch (_flax)
            {
                case 1: return "kampania zaczeta z kluczem (dzien " + _flaxDay + ") - " + (KeyOn ? "CZYNNE" : "klucz wylaczony - typy z mapy");
                case 2: return "kampania zaczeta z kluczem wylaczonym (dzien " + _flaxDay + ") - NIGDY (plon wsi od ludzi P1 ma g typow z kalibracji)";
                default: return "kampania sprzed paczki (stary zapis) - NIGDY (plon wsi od ludzi P1 ma g typow z kalibracji; podmiana teraz dalaby len x2.6 i ryby -34%)";
            }
        }

        // ============================================================ doba (watek glowny)

        internal static void Daily()
        {
            try
            {
                if (!KeyOn)
                {
                    if (!_offLogged) { _offLogged = true; Log.Info("Stawki towarow (GL5): dzien " + Today() + " - WYLACZONE (Goods Rates By Pattern = off) - sol i len wedlug typow z mapy, jak dotad."); }
                    return;
                }
                _offLogged = false;
                int salt = 0, flax = 0;
                foreach (var v in Village.All)
                {
                    var t = v != null ? v.VillageType : null;
                    if (t == null) continue;
                    if (t.StringId == SaltType) salt++; else if (t.StringId == Flax) flax++;
                }
                long calls = Interlocked.Exchange(ref _saltCalls, 0);
                int rs = Interlocked.Exchange(ref _readStumbles, 0);
                Log.Info("Stawki towarow (GL5): dzien " + Today() + " - Goods Rates By Pattern tak | sol x" + SaltRate.ToString("0.0", Inv) + ": wsi solnych " + salt + ", wywolan modelu z czynnikiem " + calls
                         + " | len: wsie lniane na mapie " + flax + ", z tabeli z lnem " + _applied + " z " + Table.Length + " (" + StateText() + ") | potkniecia " + (_stumbles + rs) + ".");
                _stumbles = 0;
            }
            catch (Exception e) { _stumbles++; Log.Error("GoodsRates.Daily", e); }
        }
    }
}
