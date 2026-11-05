using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>
    /// 320a (recenzja + Jeff 02.11) - stan Krwawych Godow w zapisie (arm_k320) i dwie rzeczy na nim oparte:
    /// (1) FAZA 2 BLOKADY 264 (CanonPeaceLock) rusza dopiero, gdy Krwawe Gody NAPRAWDE zaszly (RwMark z RwPeace / postfiksu ExecuteRobb, idempotentne; stary zapis -
    ///     odtworzenie z ROT: RedWeddingEvent >= 1, Robb martwy i rod Boltonow w koronie albo Winterfell u Boltonow). Smierc Robba bez wesela faze 2 omija.
    ///     Wyjscie fazy 2 niezalezne od samego BastardsSiegeEvent (recenzja: wojna, ktora trwa, pozwala Polnocy odbic Winterfell i Bekarci nigdy nie rusza):
    ///     Bekarci niemozliwi - rod Snow wyeliminowany, Jon nie zyje, Winterfell nie u Boltonow przez WfDays dob z rzedu - albo limit CapRotDays x skala osi od
    ///     Krwawych Godow (os ROT: Gody 220, oblezenie Bekartow 357, +30 zapasu). Zatrzask w zapisie, linia raz. To samo wyjscie otwiera bramke 312c
    ///     (CanonThroneWars, "Polnoc po Bekartach"); po koncu kanonu bez wesela (Robb zginal inaczej) liczy sie od pierwszej doby konca kanonu.
    /// (2) ZWROT ZAMKOW (Jeff 02.11 "ziemie, ktore stracili Lannisterowie, i zamki sa oddawane pierwotnym wlascicielom"): po ExecuteRobb kazda twierdza (miasto/zamek
    ///     z wioskami - wioski ida za twierdza) rodu, ktory przeszedl do korony, a ktorej pan z poczatku kampanii (ROT-Map settlements.xml owner=) byl rodem
    ///     korony (w danych ROT super_faction Kingdom.vlandia - recenzja 320a: tylko to; "dzis w koronie" wybiera jedynie odbiorce), wraca: do rodu pierwotnego, gdy zyje i jest w koronie; inaczej do rodu
    ///     panujacego korony (domena krola - jak gra, gdy lenno nie ma pana). ChangeOwnerOfSettlementAction.ApplyByKingDecision: zaloga zostaje (nie BySiege),
    ///     namiestnik zdjety (gra), BK bez przeniesienia tytulu (BK przenosi tylko ByBarter/ByGift - tytul de jure zostaje u dawnego pana), nasze sluchacze
    ///     (FiefGrants/KingdomBalance/WarLedger/CapitalTreasury/Rumours) licza tylko BySiege. Twierdze korony u lordow Polnocy, ktorzy nie przeszli - zostaja
    ///     (korona musi je odbic). Krolestwo gracza (korona) - bez zwrotu (jego decyzja). Klucz RedWeddingReturnsStolenCastles. Raz na kampanie (zapis).
    /// Nie rzuca na zewnatrz (wolajacy maja try), potkniecia liczone (Stumble).
    /// </summary>
    internal static partial class KingInTheNorth
    {
        private const string Tag320 = "Krwawe Gody (320a): ";
        internal const int WfDays = 120;            // Winterfell nie u Boltonow tyle dob z rzedu = Bekarci niemozliwi
        internal const int CapRotDays = 167;        // dni osi ROT od Krwawych Godow (220) do oblezenia Bekartow (357) + 30 zapasu; x Prologue.StoryScale()
        private const string WinterfellId = "town_B2", JonId = "lord_5_17";

        // ---- zapis arm_k320
        private static bool _rwDone;                // Krwawe Gody zaszly (ExecuteRobb albo odtworzone)
        private static double _rwDayC = -1.0;       // dzien kampanii Godow
        private static string _rwHow = "";          // ExecuteRobb / odtworzone (...)
        private static double _overSinceC = -1.0;   // pierwsza doba konca kanonu (bez wesela - baza limitu)
        private static double _wfSince = -1.0;      // od kiedy Winterfell nie u Boltonow (po koncu kanonu)
        private static string _foEnd = "";          // wyjscie fazy 2 / bramki 312c (zatrzask); pusty = brak
        private static double _foEndDayC = -1.0;
        private static bool _retDone;               // zwrot zamkow zrobiony (raz na kampanie)
        private static int _import320Bad;

        // ---- sesja
        private static readonly List<string> _northSnap = new List<string>();   // rody Polnocy w chwili pokoju ExecuteRobb (przed przejsciami ROT)

        internal static bool RwHappened { get { return _rwDone; } }
        /// <summary>Wyjscie fazy 2 (Bekarci niemozliwi albo limit); null - brak.</summary>
        internal static string FightsOnEnd { get { return string.IsNullOrEmpty(_foEnd) ? null : _foEnd; } }

        private static void Reset320()
        {
            _rwDone = false; _rwDayC = -1.0; _rwHow = ""; _overSinceC = -1.0; _wfSince = -1.0; _foEnd = ""; _foEndDayC = -1.0; _retDone = false; _import320Bad = 0;
            _northSnap.Clear();
        }

        internal static string Export320()
        {
            return "v1|" + (_rwDone ? "1" : "0") + "|" + _rwDayC.ToString("0.###", Inv) + "|" + Clean320(_rwHow) + "|" + _overSinceC.ToString("0.###", Inv) + "|"
                   + _wfSince.ToString("0.###", Inv) + "|" + Clean320(_foEnd) + "|" + _foEndDayC.ToString("0.###", Inv) + "|" + (_retDone ? "1" : "0");
        }

        internal static void Import320(string v)
        {
            Reset320();
            if (string.IsNullOrEmpty(v)) return;
            try
            {
                var f = v.Split('|');
                if (f.Length < 9 || f[0] != "v1") { _import320Bad++; return; }
                _rwDone = f[1] == "1";
                if (!double.TryParse(f[2], NumberStyles.Float, Inv, out _rwDayC)) _rwDayC = -1.0;
                _rwHow = f[3];
                if (!double.TryParse(f[4], NumberStyles.Float, Inv, out _overSinceC)) _overSinceC = -1.0;
                if (!double.TryParse(f[5], NumberStyles.Float, Inv, out _wfSince)) _wfSince = -1.0;
                _foEnd = f[6];
                if (!double.TryParse(f[7], NumberStyles.Float, Inv, out _foEndDayC)) _foEndDayC = -1.0;
                _retDone = f[8] == "1";
            }
            catch (Exception e) { _import320Bad++; Stumble("Import320", e); }
        }

        private static string Clean320(string s) { return string.IsNullOrEmpty(s) ? "" : s.Replace('|', '/'); }

        private static double NowDays() { try { return CampaignTime.Now.ToDays; } catch { return 0.0; } }

        /// <summary>Krwawe Gody zaszly - z RwPeace (przed decyzja o pokoju, zeby faza 2 trzymala juz w chwili ExecuteRobb), z prefiksu zapasu i z postfiksu
        /// ExecuteRobb (pokoj ROT nie wolany, gdy Polnoc nie byla w wojnie). snap - migawka rodow Polnocy (tylko PRZED przejsciami ROT: RwPeace, prefiks zapasu;
        /// postfiks - false, inaczej migawka bez rodow, ktore juz przeszly). Idempotentne. Nie rzuca.</summary>
        private static void RwMark(string how, bool snap)
        {
            try
            {
                if (snap && _northSnap.Count == 0)
                {
                    var north = K(NorthId);
                    if (north != null) foreach (var c in north.Clans) if (c != null && c.StringId != null && !c.IsEliminated) _northSnap.Add(c.StringId);
                }
                if (_rwDone) return;
                _rwDone = true; _rwDayC = NowDays(); _rwHow = how;
                _foEnd = ""; _foEndDayC = -1.0; _wfSince = -1.0;
                Log.Info(Tag320 + "zaszly (" + how + ") | dzien kampanii " + F1(_rwDayC) + ", dzien opowiesci " + F1(StoryDay()) + " | rody Polnoc w chwili Godow " + _northSnap.Count
                         + " | faza 2 blokady Polnoc-korona do Bitwy Bekartow albo do wyjscia (Bekarci niemozliwi: Snow / Jon / Winterfell nie u Boltonow " + WfDays + " dob; limit "
                         + F1(CapDays()) + " dob).");
            }
            catch (Exception e) { Stumble("RwMark", e); }
        }

        private static double CapDays()
        {
            float sc = 4.33f;
            try { sc = Prologue.StoryScale(); } catch { sc = 4.33f; }
            if (sc <= 0f) sc = 4.33f;
            return CapRotDays * (double)sc;
        }

        private static Hero RotHero(string prop, string id)
        {
            try
            {
                var t = AccessTools.TypeByName("ROT.Misc.ROTLords");
                var pi = t != null ? AccessTools.Property(t, prop) : null;
                var h = pi != null ? pi.GetValue(null, null) as Hero : null;
                if (h != null) return h;
            }
            catch { }
            return HeroOf(id);
        }

        /// <summary>Raz na dobe (Daily): odtworzenie Godow ze starego zapisu, sledzenie wyjscia fazy 2 / bramki 312c (zatrzask). Nie rzuca.</summary>
        private static void Rw320Daily()
        {
            try
            {
                int rw = SafeRot(RedWeddingEvent);
                var robb = HeroOf(RobbId);
                bool robbDead = robb != null && !robb.IsAlive;
                var bolton = RotClan("Bolton");
                Settlement wf = null;
                try { wf = Settlement.Find(WinterfellId); } catch { wf = null; }
                var crown = K(CrownId);
                if (!_rwDone && rw >= 1 && robbDead && bolton != null && !bolton.IsEliminated
                    && ((crown != null && bolton.Kingdom == crown) || (wf != null && wf.OwnerClan == bolton)))
                {
                    RwMark("odtworzone ze stanu ROT: RedWeddingEvent " + rw + ", Robb martwy, Boltonowie " + (bolton.Kingdom == crown ? "w koronie" : "z Winterfell"), false);
                }
                bool over = _rwDone || rw >= 2 || robbDead;
                if (!over) return;
                double now = NowDays();
                if (_overSinceC < 0.0) _overSinceC = _rwDone && _rwDayC >= 0.0 ? _rwDayC : now;
                if (!string.IsNullOrEmpty(_foEnd)) return;
                int bs = SafeRot(CanonPeaceLock.FightsOnEvent);
                if (bs >= 2) return;   // Bitwa Bekartow skonczona - koniec naturalny (CanonPeaceLock / 312c czytaja stan ROT)
                var north = K(NorthId);
                if (north == null || north.IsEliminated) return;   // blokada i bramka i tak koncza sie z krolestwem
                string end = null;
                var snow = RotClan("Snow");
                var jon = RotHero("JonSnow", JonId);
                if (snow != null && snow.IsEliminated) end = "rod Snow wyeliminowany - Bekarci niemozliwi";
                else if (jon != null && !jon.IsAlive) end = "Jon Snow nie zyje - Bekarci niemozliwi";
                if (end == null && wf != null)
                {
                    bool boltonWf = bolton != null && wf.OwnerClan == bolton;
                    if (boltonWf) _wfSince = -1.0;
                    else
                    {
                        if (_wfSince < 0.0) _wfSince = now;
                        if (now - _wfSince >= WfDays)
                            end = "Winterfell u " + (wf.OwnerClan != null ? Nm(wf.OwnerClan) + " (" + wf.OwnerClan.StringId + ")" : "-") + " od " + (int)(now - _wfSince) + " dob, nie u Boltonow - Bekarci niemozliwi";
                    }
                }
                double baseDay = _rwDone && _rwDayC >= 0.0 ? _rwDayC : _overSinceC;
                double cap = CapDays();
                if (end == null && now - baseDay >= cap)
                    end = "limit " + F1(cap) + " dob po " + (_rwDone ? "Krwawych Godach" : "koncu kanonu") + " bez Bitwy Bekartow (BastardsSiegeEvent " + bs + ")";
                if (end == null) return;
                _foEnd = end; _foEndDayC = now;
                Log.Info(Tag320 + "faza 2 blokady Polnoc-korona i bramka 312c Polnoc po Bekartach - WYJSCIE: " + end + " | Krwawe Gody " + (_rwDone ? "zaszly (" + _rwHow + ")" : "nie zaszly")
                         + " | dzien kampanii " + F1(now) + ", dzien opowiesci " + F1(StoryDay()) + ".");
            }
            catch (Exception e) { Stumble("Rw320Daily", e); }
        }

        /// <summary>Tekst do linii startu sesji 272.</summary>
        private static string Rw320Text()
        {
            return "Krwawe Gody 320a: " + (_rwDone ? "zaszly (" + _rwHow + ", dzien kampanii " + F1(_rwDayC) + ")" : "nie zaszly") + ", wyjscie fazy 2 " + (string.IsNullOrEmpty(_foEnd) ? "brak" : _foEnd)
                   + ", zwrot zamkow " + (_retDone ? "zrobiony" : "nie") + (_import320Bad > 0 ? ", bledy zapisu 320 " + _import320Bad : "");
        }

        // ---------------------------------------------------------------- (2) zwrot zamkow korony

        private static Dictionary<string, string> _origOwner;    // osada -> rod z poczatku kampanii (ROT-Map settlements.xml)
        private static Dictionary<string, string> _origKingdom;  // rod -> krolestwo z danych ROT (ROT-Content spclans.xml super_faction)
        private static string _origErr = "";

        private static string ModulePath(string mod, string file)
        {
            return Path.Combine(Path.Combine(Path.Combine(Path.Combine(BasePath.Name, "Modules"), mod), "ModuleData"), file);
        }

        /// <summary>Atrybuty elementu z pliku XML modulu (bez DTD). Klucz - id, wartosc - atrybut val bez przedrostka "Faction." / "Kingdom.".</summary>
        private static Dictionary<string, string> ReadXmlMap(string path, string element, string attr, string prefix)
        {
            var d = new Dictionary<string, string>(StringComparer.Ordinal);
            if (!File.Exists(path)) { _origErr += (path.Length > 0 ? " brak " + Path.GetFileName(path) : ""); return d; }
            var xs = new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, IgnoreComments = true, XmlResolver = null };
            using (var r = XmlReader.Create(path, xs))
            {
                while (r.Read())
                {
                    if (r.NodeType != XmlNodeType.Element || r.Name != element) continue;
                    string id = r.GetAttribute("id"), v = r.GetAttribute(attr);
                    if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(v)) continue;
                    if (v.StartsWith(prefix, StringComparison.Ordinal)) v = v.Substring(prefix.Length);
                    if (!d.ContainsKey(id)) d[id] = v;
                }
            }
            return d;
        }

        private static void LoadOrig()
        {
            if (_origOwner != null) return;
            _origErr = "";
            try { _origOwner = ReadXmlMap(ModulePath("ROT-Map", "settlements.xml"), "Settlement", "owner", "Faction."); }
            catch (Exception e) { Stumble("LoadOrig.settlements", e); _origOwner = new Dictionary<string, string>(StringComparer.Ordinal); _origErr += " settlements.xml " + e.GetType().Name; }
            try { _origKingdom = ReadXmlMap(ModulePath("ROT-Content", "spclans.xml"), "Faction", "super_faction", "Kingdom."); }
            catch (Exception e) { Stumble("LoadOrig.spclans", e); _origKingdom = new Dictionary<string, string>(StringComparer.Ordinal); _origErr += " spclans.xml " + e.GetType().Name; }
        }

        /// <summary>326b (NorthConquest): pan osady z poczatku kampanii (ROT-Map settlements.xml) i krolestwo tego rodu w danych ROT (spclans.xml); null - brak danych.</summary>
        internal static string OrigOwnerOf(string stId, out string origKingdom)
        {
            origKingdom = null;
            LoadOrig();
            string c;
            if (_origOwner == null || stId == null || !_origOwner.TryGetValue(stId, out c)) return null;
            if (_origKingdom != null) _origKingdom.TryGetValue(c, out origKingdom);
            return c;
        }

        internal static string OrigDataText() { return "osad " + (_origOwner != null ? _origOwner.Count : 0) + ", rodow " + (_origKingdom != null ? _origKingdom.Count : 0) + (_origErr.Length > 0 ? " (" + _origErr.Trim() + ")" : ""); }

        internal static Clan ClanByIdFor(string id) { return ClanById(id); }

        private static Clan ClanById(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var c in Clan.All) if (c != null && c.StringId == id) return c;
            return null;
        }

        /// <summary>Po ExecuteRobb (postfiks, po przejsciach rodow): twierdze rodow, ktore przeszly do korony, a byly z poczatku kampanii rodow korony - wracaja.
        /// Raz na kampanie (zatrzask w zapisie). Nie rzuca.</summary>
        private static void ReturnStolen()
        {
            if (_retDone) return;
            try
            {
                var s = Settings.Current;
                if (s != null && !s.RedWeddingReturnsStolenCastles) { _retDone = true; Log.Info(Tag320 + "zwrot zamkow korony - klucz Red Wedding Returns Stolen Castles NIE."); return; }
                var crown = K(CrownId);
                if (!Alive(crown)) { _retDone = true; Log.Info(Tag320 + "zwrot zamkow korony NIE - korony brak."); return; }
                if (PlayerRules(crown)) { _retDone = true; Log.Info(Tag320 + "zwrot zamkow korony NIE - korona to krolestwo gracza (jego decyzja)."); return; }
                _retDone = true;
                LoadOrig();
                var pc = Clan.PlayerClan;
                // rody, ktore przeszly: z migawki Polnocy w chwili Godow, teraz w koronie; bez migawki (pokoj ROT nie wolany) - lista ROT + nasze cztery
                var defect = new List<Clan>();
                var src = _northSnap.Count > 0 ? (IEnumerable<string>)_northSnap : null;
                if (src != null)
                {
                    foreach (var id in src) { var c = ClanById(id); if (c != null && !c.IsEliminated && c != pc && c.Kingdom == crown && !defect.Contains(c)) defect.Add(c); }
                }
                else
                {
                    foreach (var name in new[] { "Bolton", "Dustin", "Whitehill", "Ryswell", "Stout", "Umber", "Frey", "Mooton", "Cox", "Bracken" })
                    { var c = RotClan(name); if (c != null && !c.IsEliminated && c != pc && c.Kingdom == crown && !defect.Contains(c)) defect.Add(c); }
                }
                int n = 0, kept = 0, noData = 0, skip = 0;
                var keptL = new List<string>();
                foreach (var c in defect)
                {
                    var fiefs = c.Fiefs.ToList();
                    foreach (var town in fiefs)
                    {
                        var st = town != null ? town.Settlement : null;
                        if (st == null || !st.IsFortification) continue;
                        try
                        {
                            string origId;
                            if (!_origOwner.TryGetValue(st.StringId, out origId)) { noData++; continue; }
                            var orig = ClanById(origId);
                            if (orig == c) continue;   // wlasna siedziba
                            string origK;
                            bool startCrown = _origKingdom != null && _origKingdom.TryGetValue(origId, out origK) && origK == CrownId;
                            bool nowCrown = orig != null && !orig.IsEliminated && orig.Kingdom == crown;
                            if (!startCrown) { kept++; if (keptL.Count < 12) keptL.Add(Nm(st) + " (pierwotnie " + origId + ")"); continue; }
                            Clan to; string why;
                            if (nowCrown && orig != pc && orig.Leader != null && orig.Leader.IsAlive && !defect.Contains(orig)) { to = orig; why = "rod pierwotny"; }   // recenzja 320a: bez zwrotu miedzy zdrajcami
                            else
                            {
                                to = crown.RulingClan;
                                why = "rod panujacy korony - " + (orig == null ? "rodu pierwotnego brak" : orig.IsEliminated ? "rod pierwotny wyeliminowany" : orig == pc ? "rod pierwotny to gracz"
                                      : orig.Kingdom != crown ? "rod pierwotny poza korona (" + (orig.Kingdom != null ? orig.Kingdom.StringId : "-") + ")" : "rod pierwotny bez glowy");
                            }
                            if (to == null || to.IsEliminated || to == pc || to.Leader == null || !to.Leader.IsAlive || to == c) { skip++; Log.Info(Tag320 + "zwrot " + Nm(st) + " [" + st.StringId + "] od " + Nm(c) + " POMINIETY (brak odbiorcy: " + why + ")."); continue; }
                            int vill = st.BoundVillages != null ? st.BoundVillages.Count : 0;
                            bool siege = st.IsUnderSiege;
                            ChangeOwnerOfSettlementAction.ApplyByKingDecision(to.Leader, st);
                            bool ok = st.OwnerClan == to;
                            if (ok) n++;
                            Log.Info(Tag320 + "zwrot " + Nm(st) + " [" + st.StringId + "] od " + Nm(c) + " (" + c.StringId + ") do " + Nm(to) + " (" + to.StringId + ", " + why + "; pierwotnie " + origId + ") "
                                     + (ok ? "TAK" : "NIE") + " | wiosek " + vill + (siege ? " | w oblezeniu" : "") + " | zaloga " + (st.Town != null && st.Town.GarrisonParty != null ? st.Town.GarrisonParty.MemberRoster.TotalManCount : 0) + ".");
                        }
                        catch (Exception e) { Stumble("ReturnStolen.Fief", e); }
                    }
                }
                Log.Info(Tag320 + "zwrot zamkow korony | rodow, ktore przeszly " + defect.Count + " (" + (src != null ? "migawka Polnocy" : "lista ROT") + "): " + string.Join(", ", defect.Select(x => Nm(x)).ToArray())
                         + " | zwroconych " + n + ", zostaje (nie korony z poczatku) " + kept + (keptL.Count > 0 ? " [" + string.Join(", ", keptL.ToArray()) + "]" : "") + ", bez danych ROT " + noData
                         + ", bez odbiorcy " + skip + " | dane: osad " + (_origOwner != null ? _origOwner.Count : 0) + ", rodow " + (_origKingdom != null ? _origKingdom.Count : 0) + (_origErr.Length > 0 ? " (" + _origErr.Trim() + ")" : "")
                         + " | twierdze korony u lordow Polnocy, ktorzy zostali - korona musi je odbic.");
                if (n > 0) Log.Player("The crown restores to their rightful lords the castles the turncloaks had taken from the Lannisters and their vassals.");
            }
            catch (Exception e) { Stumble("ReturnStolen", e); }
        }
    }
}
