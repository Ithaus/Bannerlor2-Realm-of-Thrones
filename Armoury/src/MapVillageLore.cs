using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text;
using Armoury.Villages;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// E7/W2 (233, PLAN 7.9 a, R11/R12; projekt etapu 7 v2 rozdz. 3 "E7/W2") - DANE LORE WIOSEK I ODCZYTY BK DLA DYMKA WIOSKI.
    ///  - R11: jeden plik ModuleData\arm_map_village_lore.tsv (E7/L), wczytany raz na proces przy pierwszej potrzebie (start sesji albo pierwszy dymek), slownik uid -> wiersz;
    ///    bramka = naglowek "# layout_crc: X" == crc ukladu ksiegi wiosek (VillageBook.Layout - ten sam napis "uid:osady;" co "L:" zapisu); inny uklad, brak naglowka,
    ///    brak pliku = lore wylaczone (dymek bez linii lore) + jedna linia w logu. Kolumny wedlug naglowka "#uid" (kolejnosc dowolna), CRLF/LF obojetne, linie "#" pomijane.
    ///  - R12: pan i herb na zywo z gry (Village.Bound.OwnerClan - jak HeldLine); rycerz = posiadacz tytulu wsi z BK (TitleManager.GetTitle(wies).deJure, odbiciem),
    ///    gdy zyje, jest kim innym niz glowa rodu pana i nalezy do rodu pana albo jego krolestwa (deJure zostaje przy dawnym panu po podboju - wrog nie "trzyma wsi
    ///    dla pana"), inaczej rycerz z lore; wiara = dominujaca wiara BK wsi (PopulationData.ReligionData, udzial na zywo); zdanie lore o swietym miejscu tylko, gdy
    ///    faith_bk wiersza (id religii BK, "|" = kazda z nich) zawiera dominujaca wiare BK.
    /// Odczyty BK tylko przy dymku (raz na najechanie) i w probce raz na sesje; GetPopData wolany tylko dla osady z danymi BK (IsSettlementPopulated - inaczej BK
    /// inicjuje dane osady losowaniem, zmiana stanu gry). Bledy liczone per miejsce (licznik potkniec, pierwsze 3 w logu), nigdy nie gasza funkcji. Zero zapisu.
    /// </summary>
    internal static class MapVillageLore
    {
        internal const string FileName = "arm_map_village_lore.tsv";            // sw-przysiolki: lore ukladu v6 (bramka = crc ukladu v6; dotad v5)
        internal const string FileNameV5 = "arm_map_village_lore_v5.tsv";       // sw-przysiolki: lore ukladu v5 (VillageLayoutV6 = false)
        internal const string FileNameV4 = "arm_map_village_lore_v4.tsv";       // Jeff 16.10: lore ukladu v4 (VillageLayoutV6 = false i VillageLayoutV5 = false)
        private static string LoreFileName { get { int v = MapVillageData.LayoutVersion; return v >= 6 ? FileName : (v == 5 ? FileNameV5 : FileNameV4); } }
        private static string _loreUsed;                                         // sw-przysiolki: plik lore naprawde wczytany (zapas po bramce - nizej)
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static readonly string[] NoIds = new string[0];

        /// <summary>Jeden wiersz lore (gotowe zdania EN z E7/L). Herb rycerza (knight_banner), rod i godlo tekstem (knight_house, knight_arms) czyta menu okregu (E7/W3).</summary>
        internal sealed class Row
        {
            public string Knight = "", Livelihood = "", Faith = "", History = "", WarLore = "";
            public string BannerCode = "", House = "", Arms = "";   // E7/W3: knight_banner (tylko kod wazny wedlug Banner.IsValidBannerCode, pusty = herb pana), knight_house, knight_arms
            private Banner _banner;

            /// <summary>E7/W3: herb rycerza z lore albo null (brak kodu - herb pana); obiekt Banner budowany raz na wiersz (plik raz na proces).</summary>
            internal Banner KnightBanner
            {
                get { if (_banner == null && BannerCode.Length > 0) _banner = new Banner(BannerCode); return _banner; }
            }
            public bool Tribute;                 // knight_form = tribute ("{knight} takes its tribute"), inaczej kept
            public string[] FaithBk = NoIds;     // id religii BK, z ktorymi zgadza sie zdanie o wierze

            internal bool FaithMatches(string bkId)
            {
                if (string.IsNullOrEmpty(bkId)) return false;
                for (int i = 0; i < FaithBk.Length; i++) if (string.Equals(FaithBk[i], bkId, StringComparison.Ordinal)) return true;
                return false;
            }
        }

        // ---------- plik (raz na proces: plik i uklad w grze sie nie zmieniaja; Jeff 16.10: raz na uklad ksiegi - VillageLayoutV5 moze wybrac inny w nowej sesji) ----------
        private static bool _tried;
        private static uint _loadedFor;                     // crc ukladu ksiegi, dla ktorego wczytano lore
        private static readonly Dictionary<string, Row> _rows = new Dictionary<string, Row>(StringComparer.Ordinal);
        private static List<string> _layoutUids;
        private static uint _layoutCrc, _fileCrc;
        private static bool _gateOk, _hasFileCrc;
        private static int _layoutN, _rejected, _missing, _herbN, _herbBad;
        private static string _note = "nie wczytane";
        private static double _loadMs;

        // ---------- BK (odbicie, raz na proces; obiekty menedzerow na zywo przy kazdym odczycie - inne w kazdej kampanii) ----------
        private static bool _bkTried;
        private static PropertyInfo _pCfg, _pPopMgr, _pTitleMgr, _pRelData, _pDominant, _pReligions, _pFaith, _pDeJure;
        private static MethodInfo _mPopulated, _mGetPop, _mPercent, _mFaithName, _mGetTitle;

        // ---------- sesja ----------
        private static bool _sampleDone, _startLogged;
        private static int _stFaith, _stTitle, _stRow;   // potkniecia (sesja)

        /// <summary>Konstruktor ArmouryBehavior: stan sesji (probka, liczniki); plik zostaje (raz na proces).</summary>
        internal static void Reset()
        {
            _sampleDone = _startLogged = false;
            _stFaith = _stTitle = _stRow = 0;
            MapVillageTooltip.ResetSession();
        }

        internal static int Stumbles { get { return _stFaith + _stTitle + _stRow; } }

        // ------------------------------------------------------------ plik
        /// <summary>Wiersz lore wioski albo null (lore wylaczone - klucz, bramka, brak pliku; wioska bez wiersza). Pierwsze wywolanie wczytuje plik.</summary>
        internal static Row Of(string uid)
        {
            if (uid == null || !Settings.Current.VillageTipLore) return null;
            EnsureLoaded();
            Row r;
            return _gateOk && _rows.TryGetValue(uid, out r) ? r : null;
        }

        /// <summary>Jeff 16.10 (D1, po recenzji): wiersz lore BEZ klucza VillageTipLore (ta sama bramka crc ukladu) - przydzial gospodarzy wiosek (VillageHolders: rycerz
        /// z lore i jego herb na proporczyku) nie zalezy od klucza dymka. null - bramka, brak pliku albo wioska bez wiersza.</summary>
        internal static Row RowOf(string uid)
        {
            if (uid == null) return null;
            EnsureLoaded();
            Row r;
            return _gateOk && _rows.TryGetValue(uid, out r) ? r : null;
        }

        private static void EnsureLoaded()
        {
            uint now;
            if (_tried && VillageBook.LayoutCrcNow(out now) && now == _loadedFor) return;   // ten sam uklad co przy wczytaniu (O(1) przy kazdym dymku)
            uint crc;
            List<string> uids;
            if (!VillageBook.Layout(out crc, out uids))
            {
                // bez _tried: ksiega moze wczytac uklad pozniej; lore poprzedniego ukladu (inna sesja procesu) nie zostaje
                if (_tried) { _tried = false; _rows.Clear(); _gateOk = false; _layoutUids = null; _layoutN = 0; }
                _note = "brak ukladu ksiegi wiosek (plik wiosek nie wczytany albo z innej sceny)";
                return;
            }
            _tried = true;
            _loadedFor = crc;
            _rows.Clear(); _gateOk = false;
            var sw = Stopwatch.StartNew();
            try
            {
                _layoutCrc = crc; _layoutUids = uids; _layoutN = uids.Count;
                string name = LoreFileName;
                _loreUsed = name;
                string path = MapVillageTooltip.ModulePath("ModuleData", name);
                string text = path != null && System.IO.File.Exists(path) ? System.IO.File.ReadAllText(path, Encoding.UTF8) : null;
                // sw-przysiolki: plik ukladu wybrany zapasem (wgranie bez pliku wybranego ukladu) albo lore innego ukladu - pierwszy plik lore modulu z bramka = crc ukladu ksiegi
                if (text == null || HeadCrc(text) != crc)
                {
                    foreach (var alt in new[] { FileName, FileNameV5, FileNameV4 })
                    {
                        if (alt == name) continue;
                        string ap = MapVillageTooltip.ModulePath("ModuleData", alt);
                        if (ap == null || !System.IO.File.Exists(ap)) continue;
                        string at = System.IO.File.ReadAllText(ap, Encoding.UTF8);
                        if (HeadCrc(at) == crc) { text = at; _loreUsed = alt; break; }
                    }
                }
                if (text == null) { _note = "brak pliku " + name; return; }
                Parse(text, uids);
            }
            catch (Exception e) { _rows.Clear(); _gateOk = false; _note = "blad wczytania (" + e.GetType().Name + ")"; Log.Error("MapVillageLore.Load", e); }
            finally { _loadMs = sw.Elapsed.TotalMilliseconds; }
        }

        private static void Parse(string text, List<string> layout)
        {
            _rows.Clear(); _gateOk = false; _hasFileCrc = false; _rejected = _missing = _herbN = _herbBad = 0;
            var inLayout = new HashSet<string>(layout, StringComparer.Ordinal);
            var col = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (string raw in text.Split('\n'))
            {
                string line = raw.TrimEnd('\r');
                if (line.Length == 0) continue;
                if (line[0] == '#')
                {
                    string body = line.Substring(1).Trim();
                    if (body.StartsWith("uid\t", StringComparison.Ordinal))
                    {
                        col.Clear();
                        string[] names = line.Substring(1).Split('\t');
                        for (int i = 0; i < names.Length; i++) col[names[i].Trim()] = i;
                    }
                    else if (body.StartsWith("layout_crc:", StringComparison.Ordinal))
                    {
                        string v = body.Substring("layout_crc:".Length).Trim();
                        if (v.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) v = v.Substring(2);
                        uint c;
                        if (uint.TryParse(v, NumberStyles.HexNumber, Inv, out c)) { _fileCrc = c; _hasFileCrc = true; }
                    }
                    continue;
                }
                if (!col.ContainsKey("uid")) { _rejected++; continue; }
                string[] f = line.Split('\t');
                string uid = Get(f, col, "uid");
                if (uid.Length == 0 || !inLayout.Contains(uid) || _rows.ContainsKey(uid)) { _rejected++; continue; }
                var r = new Row
                {
                    Knight = Get(f, col, "knight"), Livelihood = Get(f, col, "livelihood"), Faith = Get(f, col, "faith"),
                    History = Get(f, col, "history"), WarLore = Get(f, col, "war_lore"),
                    Tribute = string.Equals(Get(f, col, "knight_form"), "tribute", StringComparison.Ordinal)
                };
                string bk = Get(f, col, "faith_bk");
                if (bk.Length > 0)
                {
                    var ids = new List<string>();
                    foreach (string p in bk.Split('|')) { string q = p.Trim(); if (q.Length > 0) ids.Add(q); }
                    r.FaithBk = ids.ToArray();
                }
                string herb = Get(f, col, "knight_banner");
                if (herb.Length > 0) { _herbN++; if (!Banner.IsValidBannerCode(herb)) _herbBad++; else r.BannerCode = herb; }   // E7/W3: herb rycerza w oknie okregu (niewazny - herb pana, licznik w linii startowej); dymek pokazuje herb pana
                r.House = Get(f, col, "knight_house");
                r.Arms = Get(f, col, "knight_arms");
                _rows[uid] = r;
            }
            foreach (string u in layout) if (!_rows.ContainsKey(u)) _missing++;
            if (!_hasFileCrc) { _note = "brak naglowka layout_crc - lore wylaczone"; _rows.Clear(); return; }
            if (_fileCrc != _layoutCrc) { _note = "inny uklad wiosek (bramka pliku " + _fileCrc.ToString("X8") + ", uklad ksiegi " + _layoutCrc.ToString("X8") + ") - lore wylaczone"; _rows.Clear(); return; }
            _gateOk = true;
            _note = "";
        }

        /// <summary>sw-przysiolki: crc z naglowka "# layout_crc:" pliku lore (0 - brak) - wybor pliku lore zgodnego z ukladem ksiegi.</summary>
        private static uint HeadCrc(string text)
        {
            int n = 0;
            foreach (string raw in text.Split('\n'))
            {
                string line = raw.TrimEnd('\r');
                if (line.Length == 0) continue;
                if (line[0] != '#' || ++n > 40) break;
                string body = line.Substring(1).Trim();
                if (!body.StartsWith("layout_crc:", StringComparison.Ordinal)) continue;
                string v = body.Substring("layout_crc:".Length).Trim();
                if (v.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) v = v.Substring(2);
                uint c;
                return uint.TryParse(v, NumberStyles.HexNumber, Inv, out c) ? c : 0u;
            }
            return 0u;
        }

        private static string Get(string[] f, Dictionary<string, int> col, string name)
        {
            int i;
            return col.TryGetValue(name, out i) && i < f.Length ? f[i].Trim() : "";
        }

        // ------------------------------------------------------------ BK
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
                _pRelData = _mGetPop != null ? AccessTools.Property(_mGetPop.ReturnType, "ReligionData") : null;
                var rdT = _pRelData != null ? _pRelData.PropertyType : null;
                _pDominant = rdT != null ? AccessTools.Property(rdT, "DominantReligion") : null;
                _pReligions = rdT != null ? AccessTools.Property(rdT, "Religions") : null;
                _mPercent = rdT != null && _pDominant != null ? AccessTools.Method(rdT, "GetReligionPercentage", new[] { _pDominant.PropertyType }) : null;
                _pFaith = _pDominant != null ? AccessTools.Property(_pDominant.PropertyType, "Faith") : null;
                _mFaithName = _pFaith != null ? AccessTools.Method(_pFaith.PropertyType, "GetFaithName", Type.EmptyTypes) : null;
                _pTitleMgr = AccessTools.Property(cfgT, "TitleManager");
                _mGetTitle = _pTitleMgr != null ? AccessTools.Method(_pTitleMgr.PropertyType, "GetTitle", new[] { typeof(Settlement) }) : null;
                _pDeJure = _mGetTitle != null ? AccessTools.Property(_mGetTitle.ReturnType, "deJure") : null;
            }
            catch (Exception e) { Log.Error("MapVillageLore.ResolveBk", e); }
        }

        private static bool BkFaithOk { get { return _pCfg != null && _mPopulated != null && _mGetPop != null && _pRelData != null && _pDominant != null && _mPercent != null && _mFaithName != null; } }
        private static bool BkTitleOk { get { return _pCfg != null && _pTitleMgr != null && _mGetTitle != null && _pDeJure != null; } }

        /// <summary>Dominujaca wiara BK wsi gry: id religii (Religion.StringId), udzial 0..1, nazwa wiary (Faith.GetFaithName, jak w BK). false - BK brak albo osada bez danych BK.</summary>
        internal static bool FaithOf(Settlement v, out string id, out float share, out string name)
        {
            id = name = null; share = 0f;
            ResolveBk();
            if (v == null || !BkFaithOk) return false;
            try
            {
                object cfg = _pCfg.GetValue(null, null);
                object pm = cfg != null ? _pPopMgr.GetValue(cfg, null) : null;
                if (pm == null || !(bool)_mPopulated.Invoke(pm, new object[] { v })) return false;   // czysty odczyt - GetPopData osady bez danych inicjuje je w BK
                object pd = _mGetPop.Invoke(pm, new object[] { v });
                object rd = pd != null ? _pRelData.GetValue(pd, null) : null;
                object rel = rd != null ? _pDominant.GetValue(rd, null) : null;
                var mbo = rel as TaleWorlds.ObjectSystem.MBObjectBase;
                if (mbo == null || string.IsNullOrEmpty(mbo.StringId)) return false;
                id = mbo.StringId;
                share = Convert.ToSingle(_mPercent.Invoke(rd, new[] { rel }), Inv);
                object faith = _pFaith.GetValue(rel, null);
                var t = faith != null ? _mFaithName.Invoke(faith, null) as TextObject : null;
                name = t != null ? t.ToString() : id;
                return true;
            }
            catch (Exception e)
            {
                _stFaith++;
                if (_stFaith <= 3 || _stFaith % 200 == 0) Log.Error("MapVillageLore.FaithOf " + v.StringId + " (potkniecie " + _stFaith + ")", e);
                return false;
            }
        }

        /// <summary>Do probki (T4): religia o najwiekszym udziale wprost ze slownika ReligionData.Religions - niezalezny odczyt od DominantReligion.</summary>
        private static string TopReligion(Settlement v)
        {
            if (!BkFaithOk || _pReligions == null) return null;
            object cfg = _pCfg.GetValue(null, null);
            object pm = cfg != null ? _pPopMgr.GetValue(cfg, null) : null;
            if (pm == null || !(bool)_mPopulated.Invoke(pm, new object[] { v })) return null;
            object pd = _mGetPop.Invoke(pm, new object[] { v });
            object rd = pd != null ? _pRelData.GetValue(pd, null) : null;
            var dict = rd != null ? _pReligions.GetValue(rd, null) as IDictionary : null;
            if (dict == null) return null;
            string best = null;
            float bestV = float.MinValue;
            foreach (DictionaryEntry kv in dict)
            {
                float x = Convert.ToSingle(kv.Value, Inv);
                var m = kv.Key as TaleWorlds.ObjectSystem.MBObjectBase;
                if (m != null && x > bestV) { bestV = x; best = m.StringId; }
            }
            return best;
        }

        /// <summary>R12: posiadacz tytulu wsi z BK, gdy zyje, nie jest glowa rodu pana i nalezy do rodu pana albo jego krolestwa; null - brak (rycerz z lore).
        /// E7/W3: okno okregu czyta go raz na otwarcie (ten sam dla wszystkich wiosek okregu - tytul jest na wies gry).</summary>
        internal static Hero BkHolder(Settlement v, Clan lord)
        {
            ResolveBk();
            if (v == null || lord == null || !BkTitleOk) return null;
            try
            {
                object cfg = _pCfg.GetValue(null, null);
                object tm = cfg != null ? _pTitleMgr.GetValue(cfg, null) : null;
                object title = tm != null ? _mGetTitle.Invoke(tm, new object[] { v }) : null;
                var h = title != null ? _pDeJure.GetValue(title, null) as Hero : null;
                if (h == null || !h.IsAlive || ReferenceEquals(h, lord.Leader)) return null;
                bool ours = ReferenceEquals(h.Clan, lord) || (h.MapFaction != null && ReferenceEquals(h.MapFaction, lord.MapFaction));
                return ours ? h : null;
            }
            catch (Exception e)
            {
                _stTitle++;
                if (_stTitle <= 3 || _stTitle % 200 == 0) Log.Error("MapVillageLore.BkHolder " + v.StringId + " (potkniecie " + _stTitle + ")", e);
                return null;
            }
        }

        /// <summary>Linia rycerza: "Kept for them by X" / "X takes its tribute" (knight_form wiersza), wies rodu gracza - "for you". X = posiadacz tytulu BK (R12) albo rycerz
        /// z lore; null - brak obu.</summary>
        internal static string KnightLine(Row row, Settlement v, Clan lord)
        {
            return KnightLine(row, lord, BkHolder(v, lord));
        }

        /// <summary>Ta sama linia rycerza z gotowym posiadaczem tytulu BK (h = BkHolder albo null) - E7/W3 czyta go raz na okno okregu.</summary>
        internal static string KnightLine(Row row, Clan lord, Hero h)
        {
            string who = null;
            if (h != null && h.Name != null)
            {
                who = h.Name.ToString();
                // rycerz z innego rodu niz pan: "Jorah of House Mormont" (nazwa rodu jak w linii pana - MapVillagesView.HouseName)
                if (h.Clan != null && !ReferenceEquals(h.Clan, lord) && h.Clan.Name != null)
                {
                    string house = h.Clan.Name.ToString();
                    int comma = house.IndexOf(',');
                    if (comma > 0) house = house.Substring(0, comma);
                    if (who.IndexOf(house.Trim(), StringComparison.Ordinal) < 0)
                        who = VillageTexts.Make(VillageTexts.VilTipHolderOf, "HERO", who, "HOUSE", MapVillagesView.HouseName(h.Clan)).ToString();
                }
            }
            else if (row != null && row.Knight.Length > 0) who = row.Knight;
            if (string.IsNullOrEmpty(who)) return null;
            bool own = lord != null && ReferenceEquals(lord, Clan.PlayerClan);
            bool tribute = row != null && row.Tribute;
            string tpl = tribute ? (own ? VillageTexts.VilTipTributeOwn : VillageTexts.VilTipTribute) : (own ? VillageTexts.VilTipKeptOwn : VillageTexts.VilTipKept);
            return VillageTexts.Make(tpl, "KNIGHT", who).ToString();
        }

        /// <summary>Linia wiary z BK: "Faith: The Seven (82%)"; null - BK brak albo osada bez danych.</summary>
        internal static string FaithLine(Settlement v, out string bkId)
        {
            float share;
            string name;
            if (!FaithOf(v, out bkId, out share, out name)) return null;
            return VillageTexts.Make(VillageTexts.VilTipFaith, "FAITH", name, "SHARE", (int)Math.Round(share * 100f)).ToString();
        }

        internal static void CountRowStumble(string uid, Exception e)
        {
            _stRow++;
            if (_stRow <= 3 || _stRow % 200 == 0) Log.Error("MapVillageLore (dymek " + uid + ", potkniecie " + _stRow + ")", e);
        }

        // ------------------------------------------------------------ log
        /// <summary>ArmouryBehavior.OnSessionLaunched po VillageBook.Resolve: wczytanie (raz na proces) i linia startowa "Dymek wioski (E7/W2): start".</summary>
        internal static void SessionStart()
        {
            if (_startLogged) return;
            _startLogged = true;
            if (!Settings.Current.VillageTipLore) { Log.Info("Dymek wioski (E7/W2): start | WYLACZONY w MCM (VillageTipLore) - dymek jak dotad (pan, ludzie, stan)."); return; }
            EnsureLoaded();
            ResolveBk();
            string lore = _gateOk
                ? "lore " + _rows.Count + "/" + _layoutN + " (bramka " + _fileCrc.ToString("X8") + " zgodna, odrzucone " + _rejected + ", bez lore " + _missing + ", wczytanie " + _loadMs.ToString("0", Inv) + " ms raz na uklad, plik " + (_loreUsed ?? LoreFileName) + ")"
                : "lore WYLACZONE: " + _note + (_layoutN > 0 ? " (wiosek ukladu " + _layoutN + ", odrzucone " + _rejected + ")" : "") + " - dymek bez linii lore";
            Log.Info("Dymek wioski (E7/W2): start | " + lore + ", dymek " + MapVillageTooltip.Movie + " " + MapVillageTooltip.StatusText
                     + " | BK: wiara " + (BkFaithOk ? "tak" : "nie") + ", tytuly " + (BkTitleOk ? "tak" : "nie")
                     + " | herby z lore niewazne " + _herbBad + " (z " + _herbN + " kodow knight_banner - dla menu okregu E7/W3; dymek pokazuje herb pana).");
        }

        /// <summary>ArmouryBehavior.OnDailyTick (po VillageNews.Daily): linia doby dymka i raz na sesje (pierwsza doba) probka wiary 20 wiosek (T4).</summary>
        internal static void Daily()
        {
            if (!Settings.Current.VillageTipLore) return;
            if (!_sampleDone) { _sampleDone = true; FaithSample(); }
            Log.Info("Dymek wioski (E7/W2): dzien " + ((int)CampaignTime.Now.ToDays).ToString(Inv) + " | " + MapVillageTooltip.DayText()
                     + " | potkniecia lore/BK: wiara " + _stFaith + ", tytul " + _stTitle + ", wiersz " + _stRow + " (sesja)");
            MapVillageTooltip.DayReset();
        }

        /// <summary>T4: 20 wiosek rowno po ukladzie - linia wiary dymka (FaithOf) obok religii o najwiekszym udziale ze slownika BK (odczyt niezalezny); cala mapa:
        /// ile wiosek pokaze zdanie lore o swietym miejscu (faith_bk = dominujaca wiara BK okregu). Raz na sesje, ok. 425 odczytow BK.</summary>
        private static void FaithSample()
        {
            try
            {
                EnsureLoaded();
                if (_layoutUids == null || _layoutUids.Count == 0) { Log.Info("Dymek wioski (E7/W2): probka wiary - brak ukladu (" + _note + ")."); return; }
                var byDist = new Dictionary<string, string>(StringComparer.Ordinal);   // okreg -> id dominujacej wiary BK ("" - brak danych)
                int rows = 0, match = 0, noBk = 0;
                foreach (string uid in _layoutUids)
                {
                    string dist = DistrictOf(uid);
                    string fid;
                    if (!byDist.TryGetValue(dist, out fid))
                    {
                        float sh; string nm;
                        fid = FaithOf(Settlement.Find(dist), out fid, out sh, out nm) ? fid : "";
                        byDist[dist] = fid;
                    }
                    Row r;
                    if (!_gateOk || !_rows.TryGetValue(uid, out r)) continue;
                    rows++;
                    if (fid.Length == 0) noBk++;
                    else if (r.FaithMatches(fid)) match++;
                }
                var sb = new StringBuilder();
                int n = 0, withBk = 0, same = 0, step = Math.Max(1, _layoutUids.Count / 20);
                for (int i = 0; i < _layoutUids.Count && n < 20; i += step)
                {
                    string uid = _layoutUids[i];
                    var v = Settlement.Find(DistrictOf(uid));
                    if (v == null) continue;
                    n++;
                    string bkId;
                    string line = FaithLine(v, out bkId);
                    string top = TopReligion(v);
                    if (line != null || top != null) withBk++;
                    if (line != null && top != null && string.Equals(bkId, top, StringComparison.Ordinal)) same++;
                    Row r;
                    string lore = _gateOk && _rows.TryGetValue(uid, out r) ? string.Join("|", r.FaithBk) + (r.FaithMatches(bkId) ? " (zdanie TAK)" : " (zdanie nie)") : "brak wiersza";
                    sb.Append(n == 1 ? "" : "; ").Append(uid).Append(" [").Append(v.Name).Append("]: dymek \"").Append(line ?? "-").Append("\", BK najwiekszy udzial ")
                      .Append(top ?? "-").Append(", lore ").Append(lore);
                }
                Log.Info("Dymek wioski (E7/W2): probka wiary | wiara w dymku = najwiekszy udzial BK " + same + "/" + withBk + " (wiosek probki " + n + ", bez danych BK " + (n - withBk) + ") | zdanie lore o swietym miejscu: "
                         + match + "/" + rows + " wiosek z lore (okreg bez danych BK " + noBk + ") | " + (sb.Length > 0 ? sb.ToString() : "-"));
            }
            catch (Exception e) { Log.Error("MapVillageLore.FaithSample", e); }
        }

        /// <summary>Wies gry wioski z uid ("ROT_castle15_village1#e747e32f" -> "ROT_castle15_village1"; uid = id wsi + skrot polozenia, MapVillageData.Parse).</summary>
        private static string DistrictOf(string uid)
        {
            int h = uid.IndexOf('#');
            return h > 0 ? uid.Substring(0, h) : uid;
        }
    }
}
