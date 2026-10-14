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
    ///      Kazda kampania (to stawka, nie zmiana mapy); klucz decyduje na zywo.
    ///  (b) LEN Z RAK LUDZI: 16 wsi w ludnych krainach dostaje typ flax_plant (z 20 na 36 wsi lnianych; suma (poziom ognisk + 1) / 2 wsi lnianych 20.5 -> 36.5
    ///      = x1.78; P1 trzyma sume swiata typu przy sumie (poziom + 1) / 2, wiec len swiata x1.78 przy tym samym regulatorze: bieg 43 bez paczki 399 szt./d
    ///      na x1.0 regulatora -> ok. 710 (x1.0) - 925 (sufit x1.3) wobec wzorca 870). Wybor (poprawka po recenzji GL-B - zaden targ nie traci calej
    ///      zywnosci wsi): wsie towaru, ktorego swiat ma za duzo WEDLUG REGULATORA biegu 43 (decyzje 2-11):
    ///       - bawelna (silk_plant): regulator x0.45 caly rok, stos 104-121 dni zuzycia - zuzycie ok. 40% bazy; 7 z 14 wsi (wszystkie w Reach, Volantis,
    ///         Qarth; suma poziomow 14 -> 7) - baza 129 -> ok. 64 szt./d przy zuzyciu 52-58 (regulator bawelny podejdzie z x0.45 do ok. x0.9, sufit x1.3);
    ///       - srebro: regulator na podlodze x0.50 caly rok, stos 114 -> 38 dni; 4 z 30 kopalni (suma 31 -> 27); BK dopisuje kopalniom srebra marmur 0.8
    ///         i rude zlota 0.2 (BKVillageTypes) - marmur -13% (regulator marmuru na suficie od d196: marmuru bedzie mniej, budowy BK wolniej);
    ///       - ryby: 5 z 66 wsi rybakow (suma 68 -> 63), tylko tam, gdzie targ ma jeszcze inna wies z jedzeniem (zboze albo druga wies rybakow);
    ///      NIE drwale: drewno na suficie regulatora x1.30 caly rok (stos 5-17 dni, 34 miasta bez drewna w d336) - pierwotne "drewno bez ujscia" bylo bledne.
    ///      Zywnosc wsi swiata -1.5% (model recenzji: zboze + ryby typow), najwieksza strata targu -35% (Rhyos - zostaje farma zboza Giasos).
    ///      Mechanizm jak klimat wsi T8 (VillageClimate): typ wsi nie idzie do zapisu gry (gra ustawia go z settlements.xml przy kazdym wczytaniu), wiec
    ///      podmiana raz przy kazdym starcie sesji, PO VillageClimate.Apply (6 wsi bawelny z tabeli to wsie T8 - przy wylaczonym VillageClimateFix maja typ
    ///      z mapy i NIE sa ruszane, linia startu je wypisze); klasa wsi BK (w zapisie BK) wyrownana do biezacego typu (VillageClimate.BkClass). Wies z innym
    ///      typem niz w tabeli (inny mod) - nie ruszamy. Dymek gry pokazuje sam nowy towar glowny (typ); ikona wsi zalezy od kultury, nie od typu; nazwy bez zmian;
    ///      zdanie zajecia w naszym dymku przysiolka (lore E7, plik generowany z typow mapy) - Livelihood podmienia zdanie starego typu na len (po angielsku).
    ///      TYLKO KAMPANIA ZACZETA Z KLUCZEM (flaga w zapisie arm_goodsrates, SaveText.Sync): plon wsi od ludzi P1 trzyma g(typ) z kalibracji (zapis
    ///      arm_villagepeople) - podmiana PO kalibracji na starym zapisie dalaby inna sume swiata typow. Z tego samego powodu w kampanii zaczetej z kluczem
    ///      wsie lniane zostaja NA STALE, takze po wylaczeniu klucza (recenzja GL-B: powrot typow przy g z kalibracji dawal ryby +55%, len -62%) - klucz
    ///      decyduje wtedy tylko o soli. Stary zapis (brak pola) i kampania zaczeta z kluczem wylaczonym - nigdy (sama sol x2 przy kluczu).
    /// Klucz wylaczony: krok soli nic nie dopisuje (stan 97dd474); wsie lniane tylko w kampanii zaczetej z kluczem (mapa tej kampanii).
    /// Obieg: zloto nie rusza; towar z pracy ludzi wsi (inny towar tych samych ludzi), sprzedaz wozem jak dotad.
    /// </summary>
    internal static class GoodsRates
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        internal const float SaltRate = 2f;
        private const string Flax = "flax_plant", SaltType = "salt_mine", SaltItem = "salt";
        private static readonly TextObject SaltText = new TextObject("{=!}Armoury: salt rate of the realm (x2)");
        // zdanie zajecia przysiolka wsi lnianej (dymek lore, po Alt) - jak zajecia.csv (flax_plant); przysiolek z obrazkiem rybackim - len na sieci i zagle
        private const string FlaxLore = "Flax growers and linen weavers.";
        private const string FlaxLoreFishing = "Flax growers and net-makers: linen thread for nets and sails.";

        // id wsi, typ dzis (settlements.xml ROT, po T8), krolestwo z dnia startu biegu 43, nazwa (log) - wybor i liczby: CHANGELOG (GL-B poprawki)
        private static readonly string[][] Table =
        {
            new[] { "ROT_town27_village2",  "fisherman",   "The Reach",      "Greenshield" },
            new[] { "village_ES7_2",        "fisherman",   "The Reach",      "Rosewood" },
            new[] { "village_V1_1",         "silk_plant",  "The Reach",      "Cider Hall" },
            new[] { "village_V5_3",         "silk_plant",  "The Reach",      "Berrybush" },
            new[] { "village_ES3_3",        "silk_plant",  "Volantis",       "Zestea" },
            new[] { "village_ES5_1",        "silk_plant",  "Volantis",       "Lanthas" },
            new[] { "village_ES4_1",        "silk_plant",  "Volantis",       "Sagora" },
            new[] { "ROT_town35_village2",  "silk_plant",  "Qarth",          "Shirosi" },
            new[] { "ROT_town37_village2",  "silk_plant",  "Qarth",          "Port Yhos" },
            new[] { "ROT_town29_village3",  "silver_mine", "King's Landing", "Manderstone" },
            new[] { "castle_village_EN6_2", "silver_mine", "King's Landing", "Pendric Mines" },
            new[] { "castle_village_V4_2",  "silver_mine", "King's Landing", "Minetown" },
            new[] { "ROT_town14_village2",  "fisherman",   "Braavos",        "Theria" },
            new[] { "ROT_town14_village1",  "silver_mine", "Braavos",        "Galos" },
            new[] { "castle_village_ES4_1", "fisherman",   "Stormlands",     "Breaker's Bay" },
            new[] { "ROT_town28_village2",  "fisherman",   "Stormlands",     "Cape Wrath" },
        };

        // ------------------------------------------------------------ stan kampanii (zapis: arm_goodsrates "1|stan|dzien")
        // stan lnu: 0 = kampania sprzed paczki (stary zapis) - bez podmian; 1 = kampania zaczeta z kluczem - podmiany zawsze (na stale); 2 = zaczeta bez klucza - nigdy
        private static int _flax;
        private static int _flaxDay = -1;
        private static bool _newGame;
        private static int _applied, _stumbles, _readStumbles, _loreN, _loreStumbles;
        private static string _start = "";
        private static bool _offLogged;
        private static long _saltCalls;
        // wsie podmienione w tej sesji: id wsi -> typ przed (dymek lore); jedna referencja, slownik nie zmieniany po publikacji
        private static volatile Dictionary<string, string> _swapped = new Dictionary<string, string>(StringComparer.Ordinal);

        internal static void Reset()
        {
            _flax = 0; _flaxDay = -1; _newGame = false; _applied = 0; _stumbles = 0; _readStumbles = 0; _loreN = 0; _loreStumbles = 0; _start = ""; _offLogged = false; _saltCalls = 0;
            _swapped = new Dictionary<string, string>(StringComparer.Ordinal);
        }

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
                bool swap = _flax == 1;                             // kampania zaczeta z kluczem: na stale (P1 ma g typow z tymi wsiami), klucz juz nie decyduje
                var bk = new VillageClimate.BkClass();
                var flax = Type(Flax);
                var done = new List<string>();
                var already = new List<string>();
                var other = new List<string>();
                var missing = new List<string>();
                var delta = new SortedDictionary<string, int>(StringComparer.Ordinal);
                var swapped = new Dictionary<string, string>(StringComparer.Ordinal);
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
                            var from = Type(row[1]);
                            if (from == null || flax == null) { missing.Add("typ " + row[1]); continue; }
                            if (curId == Flax) { already.Add(row[3]); bk.Align(st, row[3], from, cur); swapped[row[0]] = row[1]; n++; continue; }
                            if (curId != row[1]) { other.Add(row[3] + " ma " + curId); continue; }   // ktos inny zmienil typ (albo T8 wylaczone) - nie ruszamy
                            st.Village.VillageType = flax;
                            bk.Align(st, row[3], cur, flax);
                            swapped[row[0]] = row[1];
                            done.Add(row[3] + " (" + row[0] + ", " + row[2] + "): " + row[1] + " -> " + Flax);
                            int v; delta.TryGetValue(row[1], out v); delta[row[1]] = v - 1;
                            n++;
                        }
                        catch (Exception e) { _stumbles++; if (_stumbles <= 3) Log.Error("GoodsRates.Apply(" + row[0] + ")", e); }
                    }
                }
                _swapped = swapped;                                 // jedna referencja (dymek lore)
                _applied = n;
                if (done.Count > 0) delta[Flax] = done.Count;
                int onMap = 0;
                try { foreach (var v in Village.All) if (v != null && v.VillageType != null && v.VillageType.StringId == Flax) onMap++; } catch { }
                var parts = new List<string>();
                foreach (var kv in delta) if (kv.Value != 0) parts.Add(kv.Key + " " + (kv.Value > 0 ? "+" : "") + kv.Value);
                _start = "wsie lniane na mapie " + onMap + " (z tabeli z lnem " + n + " z " + Table.Length + ")";
                Log.Info("Stawki towarow (GL5): start sesji, dzien " + Today() + " - Goods Rates By Pattern " + (KeyOn ? "tak" : "nie") + "; sol x" + SaltRate.ToString("0.0", Inv)
                         + " (stawka wsi solnych, kazda kampania, " + (KeyOn ? "czynna" : "wylaczona") + "); len - nowe wsie lniane: " + StateText() + "; "
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
                case 1: return "kampania zaczeta z kluczem (dzien " + _flaxDay + ") - CZYNNE na stale (P1 zmierzyl plon typow z tymi wsiami; klucz decyduje tylko o soli)";
                case 2: return "kampania zaczeta z kluczem wylaczonym (dzien " + _flaxDay + ") - NIGDY (plon wsi od ludzi P1 ma g typow z kalibracji)";
                default: return "kampania sprzed paczki (stary zapis) - NIGDY (plon wsi od ludzi P1 ma g typow z kalibracji; podmiana teraz zmienilaby sume swiata typow)";
            }
        }

        // ============================================================ dymek lore przysiolka (MapVillageTooltip.Build, watek glowny)

        /// <summary>Zdanie zajecia przysiolka (lore E7 generowane z typow mapy): we wsi podmienionej na len w tej sesji pierwsze zdanie starego typu (rybacy,
        /// gornicy srebra, tkacze bawelny) -> zdanie lnu po angielsku; przysiolek z obrazkiem rybackim -> len na sieci i zagle. Reszta tekstu bez zmian.
        /// Wies niepodmieniona, inne zdanie (rolnicy, pasterze), blad - tekst bez zmian (blad liczony).</summary>
        internal static string Livelihood(Settlement district, string uid, string text)
        {
            try
            {
                var m = _swapped;
                string from;
                if (string.IsNullOrEmpty(text) || district == null || m.Count == 0 || !m.TryGetValue(district.StringId, out from)) return text;
                var t = district.Village != null ? district.Village.VillageType : null;
                if (t == null || t.StringId != Flax) return text;
                int cut = text.IndexOf(". ", StringComparison.Ordinal);
                string main = cut >= 0 ? text.Substring(0, cut + 1) : text;
                string rest = cut >= 0 ? text.Substring(cut + 1) : "";
                bool fishing = false, hit;
                switch (from)
                {
                    case "fisherman": hit = main.StartsWith("Fishers", StringComparison.Ordinal); fishing = hit && VillageBook.FishingHamlet(uid); break;
                    case "silver_mine": hit = main.StartsWith("Silver miners", StringComparison.Ordinal) || main.StartsWith("Miners in the silver", StringComparison.Ordinal); break;
                    case "silk_plant": hit = main.StartsWith("Dyers and weavers", StringComparison.Ordinal) || main.StartsWith("Silk growers", StringComparison.Ordinal); break;
                    default: hit = false; break;
                }
                if (!hit) return text;
                _loreN++;
                return (fishing ? FlaxLoreFishing : FlaxLore) + rest;
            }
            catch (Exception e) { if (++_loreStumbles == 1) { try { Log.Error("GoodsRates.Livelihood", e); } catch { } } return text; }
        }

        // ============================================================ doba (watek glowny)

        internal static void Daily()
        {
            try
            {
                if (!KeyOn)
                {
                    if (!_offLogged) { _offLogged = true; Log.Info("Stawki towarow (GL5): dzien " + Today() + " - WYLACZONE (Goods Rates By Pattern = off) - sol jak dotad; len: " + StateText() + ", z tabeli z lnem " + _applied + " z " + Table.Length + "."); }
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
                int ln = _loreN, ls = _loreStumbles; _loreN = 0; _loreStumbles = 0;
                Log.Info("Stawki towarow (GL5): dzien " + Today() + " - Goods Rates By Pattern tak | sol x" + SaltRate.ToString("0.0", Inv) + ": wsi solnych " + salt + ", wywolan modelu z czynnikiem " + calls
                         + " | len: wsie lniane na mapie " + flax + ", z tabeli z lnem " + _applied + " z " + Table.Length + " (" + StateText() + "), zdan lore przysiolkow podmienionych " + ln
                         + " | potkniecia " + (_stumbles + rs + ls) + ".");
                _stumbles = 0;
            }
            catch (Exception e) { _stumbles++; Log.Error("GoodsRates.Daily", e); }
        }
    }
}
