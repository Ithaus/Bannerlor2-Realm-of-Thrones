using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>
    /// Jeff 16.10 (D2, klucz VillageCanonHouses): "dane wez ze swiata gry o tron jesli to mozliwe" - RODY KANONU dla gospodarzy wiosek pobocznych zamiast rycerzy
    /// wymyslonych w lore (House Weirley). Plik ModuleData\arm_houses_canon.tsv (tools/herby/herby_kanon.py, raz na proces) - trzy czesci:
    ///  "#house": 536 rodow z awoiaf (kraina, siedziby = id osad ROT, pan i jego rdzen "House Tully", ranga, herb slowami, dewiza, klany ROT, kod herbu: klan ROT =
    ///    jego banner_key, inaczej herb zlozony z opisu z ikon gry; banner_code_ccs = ten sam herb z ikonami Bannerlord.CCsBanners, tylko gdy ten modul jest wczytany;
    ///    holder = yes - moze gospodarzyc wioska swojej krainy: nie wymarly, nie tylko z ADWD, nie wielki / suwerenny, nie pojedynczy lord);
    ///  "#district": wies gry -> kraina z kultury W PLIKU MAPY (BK zmienia Settlement.Culture w grze - kraina okregu ma byc stala, BKManagerBehavior / CultureData);
    ///  "#names": poczatki imion rycerzy z lore krainy (zapas imienia dla wioski bez lore).
    /// Na zywo (sesja): klan ROT rodu (pierwszy istniejacy z rot_clan_id), polozenie siedziby (Settlement.Find), czy CCsBanners wczytany (ikona 78201 w BannerManager).
    /// Kazdy kod sprawdzany w grze przed pierwszym uzyciem (raz na proces): Banner.TryGetBannerDataFromCode, tlo w grupie wzorow (GetBackgroundMeshName),
    /// kazda ikona ma material (GetIconDataFromIconId), kazda barwa w palecie (ReadOnlyColorPalette); zly kod = rod bez herbu z pliku (licznik), nie wywrotka.
    /// Nic w zapisie gry. Bledy liczone (pierwsze w logu), nic nie gasnie.
    /// </summary>
    internal static class CanonHouses
    {
        internal const string FileName = "arm_houses_canon.tsv";
        private const int CcsProbeIcon = 78201;        // CCsBanners "GoT 1": Targaryen - jest w BannerManager tylko, gdy modul wczytany

        internal sealed class House
        {
            public string Name = "", Key = "", Region = "", Area = "", Culture = "", Liege = "", LiegeBase = "", Base = "", Status = "", Blazon = "", Words = "";
            public string Code = "", CodeCcs = "", Source = "", Parse = "";
            public string[] Seats = NoIds, Clans = NoIds;
            public bool Holder, Rot;
            public string ArmsText = "";               // herb slowami (czesc przed nawiasem z blazonem technicznym) - "Hear more"
            // proces
            internal int Checked;                      // 0 - nie sprawdzony, 1 - wazny, -1 - zly
            internal bool CheckedCcs;                  // ktory kod sprawdzono (CCs albo zwykly)
            internal Banner Banner;                    // wlasny obiekt (kod z pliku) - jeden na rod
            // sesja
            internal int Stamp = -1;
            internal Clan Live;                        // klan ROT tego rodu w tej kampanii (moze byc wyeliminowany)
            internal bool HasSeat;
            internal Vec2 SeatPos;
        }

        private static readonly string[] NoIds = new string[0];
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static bool _tried;
        private static string _note = "nie wczytane";
        private static readonly List<House> _all = new List<House>(560);
        private static readonly Dictionary<string, List<House>> _byArea = new Dictionary<string, List<House>>(StringComparer.Ordinal);   // tylko holder = yes
        private static readonly Dictionary<string, House> _byClan = new Dictionary<string, House>(StringComparer.Ordinal);               // klan ROT -> rod (wszystkie)
        private static readonly Dictionary<string, string> _district = new Dictionary<string, string>(StringComparer.Ordinal);           // wies gry -> kraina
        private static readonly Dictionary<string, string[]> _names = new Dictionary<string, string[]>(StringComparer.Ordinal);
        private static int _holders, _rotN, _genN, _ccsN, _rejected;
        private static double _loadMs;
        // sesja
        private static int _session;
        private static Dictionary<string, Clan> _clans;
        private static int _ccs = -1;                  // -1 nie sprawdzone, 0 CCsBanners brak, 1 wczytany
        private static int _badCodes, _st;
        private static readonly HashSet<string> _errSites = new HashSet<string>(StringComparer.Ordinal);

        internal static bool Loaded { get { EnsureLoaded(); return _all.Count > 0; } }
        internal static string Note { get { return _note; } }
        internal static int BadCodes { get { return _badCodes; } }

        /// <summary>VillageHolders.Reset (konstruktor ArmouryBehavior, kazda kampania): stan sesji (klany, siedziby, CCs) od nowa; plik zostaje.</summary>
        internal static void ResetSession()
        {
            _session++;
            _clans = null;
            _ccs = -1;
            _st = 0;
            lock (_errSites) _errSites.Clear();
        }

        private static void Stumble(string where, Exception e)
        {
            _st++;
            bool first;
            lock (_errSites) first = _errSites.Add(where);
            if (first || _st % 500 == 0) Log.Error("CanonHouses." + where + " (potkniecie " + _st + ")", e);
        }

        // ------------------------------------------------------------ plik (raz na proces)
        private static void EnsureLoaded()
        {
            if (_tried) return;
            _tried = true;
            var sw = Stopwatch.StartNew();
            try
            {
                string path = MapVillageTooltip.ModulePath("ModuleData", FileName);
                if (path == null || !System.IO.File.Exists(path)) { _note = "brak pliku " + FileName; return; }
                Parse(System.IO.File.ReadAllText(path, Encoding.UTF8));
                _note = "";
            }
            catch (Exception e) { _all.Clear(); _byArea.Clear(); _byClan.Clear(); _district.Clear(); _names.Clear(); _note = "blad wczytania (" + e.GetType().Name + ")"; Log.Error("CanonHouses.Load", e); }
            finally { _loadMs = sw.Elapsed.TotalMilliseconds; }
        }

        private static void Parse(string text)
        {
            var col = new Dictionary<string, int>(StringComparer.Ordinal);
            string section = null;
            foreach (string raw in text.Split('\n'))
            {
                string line = raw.TrimEnd('\r');
                if (line.Length == 0) continue;
                if (line[0] == '#')
                {
                    string body = line.Substring(1);
                    int tab = body.IndexOf('\t');
                    string head = tab > 0 ? body.Substring(0, tab) : "";
                    if (head == "house" || head == "district" || head == "names")
                    {
                        section = head;
                        col.Clear();
                        string[] names = body.Split('\t');
                        for (int i = 0; i < names.Length; i++) col[names[i].Trim()] = i;
                    }
                    continue;
                }
                string[] f = line.Split('\t');
                if (section == "house")
                {
                    var h = new House
                    {
                        Name = Get(f, col, "house"), Key = Get(f, col, "key"), Region = Get(f, col, "region"), Area = Get(f, col, "area"), Culture = Get(f, col, "culture"),
                        Liege = Get(f, col, "liege"), LiegeBase = Get(f, col, "liege_base"), Base = Get(f, col, "base"), Status = Get(f, col, "status"),
                        Blazon = Get(f, col, "blazon"), Words = Get(f, col, "words"), Code = Get(f, col, "banner_code"), CodeCcs = Get(f, col, "banner_code_ccs"),
                        Source = Get(f, col, "source"), Parse = Get(f, col, "parse")
                    };
                    if (h.Name.Length == 0 || h.Code.Length == 0) { _rejected++; continue; }
                    if (h.Key.Length == 0) h.Key = h.Name;
                    h.Seats = Ids(Get(f, col, "seat_settlement_id"));
                    h.Clans = Ids(Get(f, col, "rot_clan_id"));
                    h.Rot = string.Equals(h.Source, "rot", StringComparison.Ordinal);
                    h.Holder = string.Equals(Get(f, col, "holder"), "yes", StringComparison.Ordinal);
                    h.ArmsText = Arms(h.Blazon);
                    _all.Add(h);
                    if (h.Rot) _rotN++; else _genN++;
                    if (h.CodeCcs.Length > 0) _ccsN++;
                    foreach (string c in h.Clans) if (!_byClan.ContainsKey(c)) _byClan[c] = h;
                    if (h.Holder && h.Area.Length > 0)
                    {
                        List<House> list;
                        if (!_byArea.TryGetValue(h.Area, out list)) _byArea[h.Area] = list = new List<House>();
                        list.Add(h);
                        _holders++;
                    }
                }
                else if (section == "district")
                {
                    string id = Get(f, col, "district"), area = Get(f, col, "area");
                    if (id.Length > 0 && area.Length > 0) _district[id] = area;
                }
                else if (section == "names")
                {
                    string area = Get(f, col, "area"), ns = Get(f, col, "names");
                    if (area.Length > 0 && ns.Length > 0) _names[area] = ns.Split('|');
                }
            }
        }

        private static string Get(string[] f, Dictionary<string, int> col, string name)
        {
            int i;
            return col.TryGetValue(name, out i) && i < f.Length ? f[i].Trim() : "";
        }

        private static string[] Ids(string s)
        {
            if (string.IsNullOrEmpty(s)) return NoIds;
            var r = new List<string>(3);
            foreach (string p in s.Split(';')) { string q = p.Trim(); if (q.Length > 0) r.Add(q); }
            return r.ToArray();
        }

        /// <summary>"A golden wreath on blue, a gold border (Azure, ...)" -> "A golden wreath on blue, a gold border"; "/ Formerly: ..." odciete; nieznany herb, "Colours: X" - pusto.</summary>
        private static string Arms(string blazon)
        {
            if (string.IsNullOrEmpty(blazon)) return "";
            string b = blazon;
            int cut = b.IndexOf(" / ", StringComparison.Ordinal);
            if (cut > 0) b = b.Substring(0, cut);
            cut = b.IndexOf("Formerly", StringComparison.Ordinal);
            if (cut > 0) b = b.Substring(0, cut);
            b = b.Trim();
            if (b.EndsWith(")", StringComparison.Ordinal))
            {
                int open = b.LastIndexOf(" (", StringComparison.Ordinal);
                if (open > 0) b = b.Substring(0, open);
            }
            b = b.Trim().TrimEnd('.', ',', ';').Trim();
            if (b.StartsWith("Unknown", StringComparison.OrdinalIgnoreCase) || b.StartsWith("...", StringComparison.Ordinal) || b.IndexOf("unknown", StringComparison.OrdinalIgnoreCase) >= 0) return "";
            if (b.StartsWith("Colours:", StringComparison.OrdinalIgnoreCase) || b.StartsWith("Colors:", StringComparison.OrdinalIgnoreCase)) return "";
            return b;
        }

        // ------------------------------------------------------------ odczyt
        /// <summary>Kraina okregu (wies gry) z pliku mapy albo null (kraina bez rodow kanonu: Mur, za Murem, Essos poza Braavos / Lys / Volantis / Zatoka).</summary>
        internal static string AreaOf(string districtId)
        {
            EnsureLoaded();
            string a;
            return districtId != null && _district.TryGetValue(districtId, out a) ? a : null;
        }

        /// <summary>Rody krainy, ktore moga gospodarzyc wioskami (kolejnosc pliku); null - brak.</summary>
        internal static List<House> InArea(string area)
        {
            EnsureLoaded();
            List<House> l;
            return area != null && _byArea.TryGetValue(area, out l) ? l : null;
        }

        /// <summary>Rod kanonu klanu gry (rot_clan_id; galezie - po nazwie glownego) albo null (gracz, najemnicy, klany Essos bez rodu).</summary>
        internal static House OfClan(Clan c)
        {
            EnsureLoaded();
            House h;
            return c != null && c.StringId != null && _byClan.TryGetValue(c.StringId, out h) ? h : null;
        }

        internal static string[] NamesOf(string area)
        {
            EnsureLoaded();
            string[] n;
            return area != null && _names.TryGetValue(area, out n) ? n : NoIds;
        }

        /// <summary>Klan ROT rodu w tej kampanii (moze byc wyeliminowany) albo null (rod bez klanu ROT albo klanu nie ma w grze).</summary>
        internal static Clan LiveClan(House h)
        {
            if (h == null || h.Clans.Length == 0) return null;
            Touch(h);
            return h.Live;
        }

        /// <summary>Polozenie pierwszej istniejacej siedziby rodu (sesja).</summary>
        internal static bool SeatPos(House h, out Vec2 pos)
        {
            Touch(h);
            pos = h.SeatPos;
            return h.HasSeat;
        }

        internal static bool SeatIs(House h, string settlementId)
        {
            if (h == null || settlementId == null) return false;
            for (int i = 0; i < h.Seats.Length; i++) if (string.Equals(h.Seats[i], settlementId, StringComparison.Ordinal)) return true;
            return false;
        }

        private static void Touch(House h)
        {
            if (h.Stamp == _session) return;
            h.Stamp = _session;
            h.Live = null;
            h.HasSeat = false;
            try
            {
                if (h.Clans.Length > 0)
                {
                    var map = Clans();
                    foreach (string id in h.Clans) { Clan c; if (map.TryGetValue(id, out c)) { h.Live = c; break; } }
                }
                foreach (string id in h.Seats)
                {
                    var s = Settlement.Find(id);
                    if (s == null) continue;
                    h.SeatPos = s.GetPosition2D;
                    h.HasSeat = true;
                    break;
                }
            }
            catch (Exception e) { Stumble("Touch", e); }
        }

        private static Dictionary<string, Clan> Clans()
        {
            if (_clans != null) return _clans;
            var m = new Dictionary<string, Clan>(StringComparer.Ordinal);
            foreach (var c in Clan.All) if (c != null && c.StringId != null && !m.ContainsKey(c.StringId)) m[c.StringId] = c;
            _clans = m;
            return m;
        }

        /// <summary>CCsBanners wczytany (ikona 78201 ma material w BannerManager) - raz na sesje.</summary>
        internal static bool CcsLoaded
        {
            get
            {
                if (_ccs < 0)
                {
                    _ccs = 0;
                    try
                    {
                        var bm = BannerManager.Instance;
                        if (bm != null && !string.IsNullOrEmpty(bm.GetIconDataFromIconId(CcsProbeIcon).MaterialName)) _ccs = 1;
                    }
                    catch (Exception e) { Stumble("CcsLoaded", e); }
                }
                return _ccs == 1;
            }
        }

        /// <summary>Herb rodu: klan ROT w grze - jego Clan.Banner (rod panujacy: herb krolestwa, jak wszedzie w grze); inaczej kod z pliku (CCs, gdy modul wczytany i jest
        /// wariant CCs), sprawdzony raz na proces. null - zly kod (rod pominiety).</summary>
        internal static Banner BannerOf(House h, Clan live)
        {
            if (h == null) return null;
            if (live != null && live.Banner != null && !string.IsNullOrEmpty(live.Banner.BannerCode)) return live.Banner;
            return Usable(h) ? h.Banner : null;
        }

        /// <summary>Kod z pliku wazny w tej grze (raz na proces; wariant CCs tylko przy wczytanym CCsBanners). Zly kod - licznik i rod bez herbu z pliku.</summary>
        internal static bool Usable(House h)
        {
            if (h == null) return false;
            bool ccs = h.CodeCcs.Length > 0 && CcsLoaded;
            if (h.Checked != 0 && h.CheckedCcs == ccs) return h.Checked > 0;
            h.CheckedCcs = ccs;
            string code = ccs ? h.CodeCcs : h.Code;
            bool ok = false;
            try { ok = CodeOk(code); }
            catch (Exception e) { Stumble("Usable " + h.Key, e); ok = false; }
            if (!ok && ccs)
            {
                // wariant CCs zly - zwykly kod
                code = h.Code;
                try { ok = CodeOk(code); } catch (Exception e) { Stumble("Usable " + h.Key, e); ok = false; }
            }
            h.Checked = ok ? 1 : -1;
            h.Banner = ok ? new Banner(code) : null;
            if (!ok) { _badCodes++; if (_badCodes <= 3) Log.Info("Rody kanonu (D2): zly kod herbu rodu " + h.Key + " (" + h.Source + ") - rod bez herbu z pliku."); }
            return ok;
        }

        /// <summary>Banner.TryGetBannerDataFromCode + tlo z grupy wzorow, ikony z materialem, barwy z palety (BannerManager tej gry).</summary>
        private static bool CodeOk(string code)
        {
            if (string.IsNullOrEmpty(code) || !Banner.IsValidBannerCode(code)) return false;
            List<BannerData> list;
            if (!Banner.TryGetBannerDataFromCode(code, out list) || list == null || list.Count < 2) return false;
            var bm = BannerManager.Instance;
            if (bm == null) return false;
            var pal = bm.ReadOnlyColorPalette;
            for (int i = 0; i < list.Count; i++)
            {
                var d = list[i];
                if (i == 0) { if (bm.GetBackgroundMeshName(d.MeshId) == null) return false; }
                else if (string.IsNullOrEmpty(bm.GetIconDataFromIconId(d.MeshId).MaterialName)) return false;
                if (pal != null && (!pal.ContainsKey(d.ColorId) || !pal.ContainsKey(d.ColorId2))) return false;
            }
            return true;
        }

        // ------------------------------------------------------------ log
        /// <summary>Do linii "Herby wiosek (D1)": stan pliku i rodow.</summary>
        internal static string StatusText()
        {
            EnsureLoaded();
            if (_all.Count == 0) return "plik " + FileName + ": " + (_note.Length > 0 ? _note : "pusty") + " - rycerze z lore jak dotad";
            int areas = _byArea.Count;
            return "plik: rodow " + _all.Count + " (herb klanu ROT " + _rotN + ", zlozony z opisu " + _genN + ", z wariantem CCs " + _ccsN + "), gospodarzy " + _holders
                   + " w " + areas + " krainach, okregow z kraina " + _district.Count + ", odrzucone wiersze " + _rejected + ", wczytanie " + _loadMs.ToString("0", Inv) + " ms"
                   + " | CCsBanners " + (CcsLoaded ? "wczytany (wariant CCs)" : "nie wczytany (ikony gry i ROT)") + " | zle kody " + _badCodes + " | potkniecia " + _st;
        }
    }
}
