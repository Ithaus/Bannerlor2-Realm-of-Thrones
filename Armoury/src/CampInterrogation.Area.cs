using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Map;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// m110-okolica (Jeff 23.10 ok. 15:50, na pytanie "czy jeniec ma podawac dokladna kryjowke, czy tylko okolice, ktora trzeba jeszcze sprawdzic zwiadem":
    /// "zwiadem") - klucz InterrogationAreaOnly (TAK; NIE = dokladna kryjowka wypatrzona i z flaga jak b49bdf78). Jeniec, ktory mowi, podaje TYLKO OKOLICE:
    ///  - OPIS: strona swiata (8, co 45 st.) i odleglosc od najblizszego miasta / zamku / wsi gry (ta sama petla co HideoutSpotter.Where). Precyzja wedlug tego,
    ///    co jeniec wie (zrodlo wiedzy z Target), nie wedlug rzutu: herszt i czlowiek z bandy (skad bohater | banda) - km co AreaKmStep (najmniej AreaKmMin);
    ///    kultura - slowa pasm jak Rumours.Days ("half a day's ride" ...), punkt w srodku pasma. Kryjowka NIE jest ujawniona (bez IsSpotted, IsVisible i flagi).
    ///  - ZNACZNIK OBSZARU: znacznik mapy gry (MapMarkerManager.CreateMapMarker, QuestId MarkerTag, herb klanu kryjowki) w punkcie opisanym (przyciety do
    ///    Campaign.Min/MaxSettlement*), przy HideoutFlagEnabled i HideoutFlagDays &gt; 0. Zdejmowany PRZED kazdym zapisem (OnBeforeSaveEvent) i stawiany z powrotem
    ///    po zapisie (OnSaveOverEvent, takze nieudanym) oraz po wczytaniu (start sesji) z wpisow wskazowek - zapis gry nigdy nie ma naszego znacznika (po cofnieciu
    ///    DLL nic nie zostaje; wzor NoticeBoard.BountyBeforeSave / FreeFolkRaids.BeforeSave). Odpiecie przez gracza (kulka trackera) pamietane (Pinned).
    ///    Odpiety stawiany znowu (po zapisie / wczytaniu) - ogloszony grze drugi raz (OnMapMarkerRemoved + OnMapMarkerCreated): kafelek trackera budowany od nowa
    ///    jako odpiety (recenzja R1). Flagi wylaczone przy wskazowce, zapisie albo starcie sesji - znaczniki stawia pierwszy przeglad godzinny po ich wlaczeniu (R4).
    ///  - WIEDZA: HideoutSight.Heard - jak plotka z karczmy (przy HideoutScoutingChance i HideoutRumourReveals wypatrzenie jak w grze, gdy gracz podejdzie).
    ///  - KONIEC: zdarzenie gry OnHideoutSpottedEvent (od razu - gra zeruje IsSpotted przy oproznieniu kryjowki) albo przeglad co godzine (IsSpotted bez
    ///    zdarzenia, termin, kryjowki juz nie ma). Termin = HideoutFlagDays dob (co najmniej 1). Meldunek "Hideout spotted" i flage na samej kryjowce daje jak
    ///    dotad HideoutSpotter (bez zmian). Kryjowka z czynna wskazowka pomijana w Target (HintBlocks; ta sama grupa nie wskaze jej drugi raz, dopoki wskazowka trwa)
    ///    z wyjatkiem doprecyzowania: herszt i czlowiek bandy podaja km na kryjowke z wskazowka przyblizona (stara konczy sie "doprecyzowana", recenzja R3);
    ///    klucz NIE - bez pomijania (dokladna kryjowka jak b49bdf78 takze przy wskazowkach z czasu klucza TAK; wskazowke konczy przeglad godzinny).
    ///  - ZAPIS: szosta czesc arm_interrogation "kryjowka,odniesienie,termin,x,y,skad,wskazanie,przypiety;..." (godziny kampanii) i sumy 6-7 (wskazane,
    ///    znalezione); DLL 104 czyta czesci 1-5 i pierwsze 6 sum. Bez nowych typow zapisu.
    /// Log "Przesluchanie (m110): " - start sesji, wskazowka (po linii proby m109), koniec wskazowki, zapis (pierwsze 5 na proces), doba; grupa progow m110okol.
    /// Bez latek Harmony - same zdarzenia kampanii. Bledy per miejsce (Hint*) liczone przez Stumble i osobny licznik m110, nic nie gasimy.
    /// </summary>
    internal static partial class CampInterrogation
    {
        internal const string P10 = "Przesluchanie (m110): ";
        internal const int AreaKmStep = 25, AreaKmMin = 25;
        // pasma slow jak Rumours.Days (gorna granica w jedn. mapy) i punkt w srodku pasma (16 - miedzy HideoutMinDistSettlement 10 a 22); dalej 25 jedn. za progiem
        private static readonly float[] AreaBandHi = { 22f, 60f, 110f, 160f };
        private static readonly float[] AreaBandMid = { 16f, 41f, 85f, 135f };
        private static readonly string[] AreaBandWords = { "half a day's ride", "a day's ride", "two days' ride", "three days' ride" };
        private const float AreaFarMid = 185f;
        private const string AreaFarWords = "many days' ride";
        private static readonly string[] AreaDirs = { "east", "north-east", "north", "north-west", "west", "south-west", "south", "south-east" };   // jak HideoutSpotter.Compass
        internal const string MarkerTag = "arm_lair_hint";
        internal const string MarkerName = "Outlaw lair - roughly here";
        internal const string WhyAllKnownArea = "Every lair of his people that he could know of is already known to you, or you are already searching where he would send you.";
        internal const string ProbeHow = "proba testu";
        private const int SaveLinesMax = 5, StartIdsMax = 20;

        internal static bool AreaOn { get { var s = Settings.Current; return s != null && s.CampInterrogation && s.InterrogationAreaOnly; } }

        /// <summary>Wskazowka okolicy kryjowki (pamiec przesluchan, zapis arm_interrogation czesc 6).</summary>
        internal sealed class Hint
        {
            public string Lair = "", Ref = "", Src = "";
            public double Due, Start;      // godziny kampanii (CampaignTime.ToHours): termin i chwila wskazania
            public float X, Y;             // punkt opisany (znacznik)
            public bool Pinned = true;     // znacznik przypiety do brzegu ekranu (VisualTrackerManager) - gracz moze go odpiac
            public MapMarker Marker;       // tylko w pamieci (zdejmowany przed zapisem)
            public bool Off;               // zdjety na czas zapisu - do odtworzenia w SaveOverHints
            public bool Probe;             // tylko test autotestu (nie idzie do zapisu jako osobne pole - test zdejmuje probne przed koncem)
            public string Ended;           // jak sie skonczyla (test)
        }

        private static readonly Dictionary<string, Hint> _hints = new Dictionary<string, Hint>(StringComparer.Ordinal);   // id kryjowki -> wskazowka
        private static string _hintNote = "brak klucza (stary zapis albo nowa kampania)";
        private static int _staleMarkers, _saveLines, _hStumbles, _vmRedo;   // _vmRedo - odpiete ogloszone grze drugi raz (test T12)
        private static bool _saveLogged, _markersOwed;   // _markersOwed - wskazowka bez znacznika przy flagach wylaczonych (recenzja R4)
        // liczniki doby m110
        private static int _dHHero, _dHBand, _dHKin, _dHFound, _dHDue, _dHGone, _dHSaveOff, _dHSaveOn, _dHSaveSkip, _dHStumbles;

        private static void HintDayReset() { _dHHero = _dHBand = _dHKin = _dHFound = _dHDue = _dHGone = _dHSaveOff = _dHSaveOn = _dHSaveSkip = _dHStumbles = 0; }

        private static void HintsClear() { _hints.Clear(); _staleMarkers = 0; _saveLogged = false; _markersOwed = false; }

        private static void HintStumble(string where, Exception e) { _hStumbles++; _dHStumbles++; Stumble(where, e); }

        private static string YN(bool b) { return b ? "TAK" : "NIE"; }
        private static string F2(float v) { return v.ToString("0.00", Inv); }
        private static string Fh(double v) { return v.ToString("0.0", Inv); }

        /// <summary>Czy kryjowka ma czynna wskazowke (Target i Open ja pomijaja).</summary>
        internal static bool Hinted(Settlement st) { return st != null && _hints.Count > 0 && _hints.ContainsKey(st.StringId ?? ""); }

        /// <summary>Czy czynna wskazowka zamyka kryjowke dla Target / Open. Klucz NIE - nigdy (recenzja R2: NIE 1:1 jak b49bdf78 takze przy wskazowkach z czasu
        /// klucza TAK). exact (skad bohater | banda) - tylko wskazowka dokladna; przyblizona ("kultura") herszt i czlowiek bandy doprecyzuja (recenzja R3).</summary>
        internal static bool HintBlocks(Settlement st, bool exact)
        {
            if (st == null || _hints.Count == 0 || !AreaOn) return false;
            Hint h;
            if (!_hints.TryGetValue(st.StringId ?? "", out h)) return false;
            return !exact || h.Src != "kultura";
        }

        // ------------------------------------------------------------ opis okolicy
        internal sealed class AreaFix
        {
            public Settlement Ref;
            public string RefKind = "-", Text = "", Words = "";
            public float D, Ang, Dq, X, Y, Err;
            public int Dir;
            public bool Exact, Clipped;
        }

        /// <summary>Okolica kryjowki: najblizsza osada gry (miasto, zamek, wies), strona swiata (8) i odleglosc wedlug precyzji (exact - km co AreaKmStep,
        /// najmniej AreaKmMin; inaczej pasma dni drogi), punkt opisany (przyciety do obszaru osad) i jego blad. null - brak osady.</summary>
        internal static AreaFix Describe(Settlement lair, bool exact)
        {
            var f = new AreaFix { Exact = exact };
            Vec2 lp = lair.GetPosition2D;
            float best = float.MaxValue;
            foreach (var st in Settlement.All)
            {
                if (st == null || !(st.IsTown || st.IsCastle || st.IsVillage)) continue;
                float d = st.GetPosition2D.Distance(lp);
                if (d < best) { best = d; f.Ref = st; }
            }
            if (f.Ref == null) return null;
            f.RefKind = f.Ref.IsTown ? "miasto" : f.Ref.IsCastle ? "zamek" : "wies";
            Vec2 rp = f.Ref.GetPosition2D;
            Vec2 dv = lp - rp;
            f.D = dv.Length;
            double a = Math.Atan2(dv.y, dv.x) * 180.0 / Math.PI;   // 0 = wschod, 90 = polnoc (+Y to polnoc na mapie)
            if (a < 0) a += 360.0;
            f.Ang = (float)a;
            f.Dir = (int)Math.Round(a / 45.0) % 8;
            if (exact)
            {
                int km = Math.Max(AreaKmMin, (int)Math.Round(f.D * Wayfinder.KmPerUnit / AreaKmStep) * AreaKmStep);
                f.Dq = km / Wayfinder.KmPerUnit;
                f.Words = km + " km";
            }
            else
            {
                f.Dq = AreaFarMid; f.Words = AreaFarWords;
                for (int i = 0; i < AreaBandHi.Length; i++) if (f.D < AreaBandHi[i]) { f.Dq = AreaBandMid[i]; f.Words = AreaBandWords[i]; break; }
            }
            f.Text = "about " + f.Words + " " + AreaDirs[f.Dir] + " of " + (f.Ref.Name != null ? f.Ref.Name.ToString() : f.Ref.StringId);
            double aq = f.Dir * 45.0 * Math.PI / 180.0;
            float x = rp.x + f.Dq * (float)Math.Cos(aq), y = rp.y + f.Dq * (float)Math.Sin(aq);
            var c = Campaign.Current;
            if (c != null && c.MinSettlementX <= c.MaxSettlementX && c.MinSettlementY <= c.MaxSettlementY)
            {
                float cx = Math.Max(c.MinSettlementX, Math.Min(c.MaxSettlementX, x)), cy = Math.Max(c.MinSettlementY, Math.Min(c.MaxSettlementY, y));
                f.Clipped = cx != x || cy != y;
                x = cx; y = cy;
            }
            f.X = x; f.Y = y;
            f.Err = new Vec2(x, y).Distance(lp);
            return f;
        }

        private static string PrecisionText()
        {
            var hi = new List<string>(); var mid = new List<string>();
            for (int i = 0; i < AreaBandHi.Length; i++) { hi.Add(AreaBandHi[i].ToString("0.##", Inv)); mid.Add(AreaBandMid[i].ToString("0.##", Inv)); }
            return "bohater i banda co " + AreaKmStep + " km (najmniej " + AreaKmMin + " km), kultura pasma " + string.Join("/", hi.ToArray()) + " (punkt " + string.Join("/", mid.ToArray())
                   + ", dalej " + AreaFarMid.ToString("0.##", Inv) + ")";
        }

        // ------------------------------------------------------------ wskazowka
        /// <summary>Wskazowka okolicy kryjowki: opis, wpis _hints, sumy i liczniki doby, wiedza (HideoutSight.Heard), znacznik obszaru. how: rozmowa | tortury |
        /// ProbeHow; due &lt; 0 - termin HideoutFlagDays dob (co najmniej 1). Zwraca wpis (null - brak osady odniesienia); line - linia logu "wskazowka"
        /// (loguje ja wolajacy: Act po linii proby m109, test od razu).</summary>
        internal static Hint Give(Settlement lair, string source, string how, string group, out string where, out string line, double due = -1.0)
        {
            where = null; line = null;
            var s = Settings.Current;
            bool exact = source == "bohater" || source == "banda";
            var f = Describe(lair, exact);
            if (f == null) return null;
            where = f.Text;
            double now = CampaignTime.Now.ToHours;
            int days = s != null ? Math.Max(1, s.HideoutFlagDays) : 1;
            var h = new Hint { Lair = lair.StringId ?? "", Ref = f.Ref.StringId ?? "", Src = source, Start = now, Due = due >= 0.0 ? due : now + days * 24.0, X = f.X, Y = f.Y, Probe = how == ProbeHow };
            Hint old;
            if (_hints.TryGetValue(h.Lair, out old))   // recenzja R3: doprecyzowanie (herszt / czlowiek bandy na wskazowke przyblizona) - stara konczy sie linia konca, znacznik zdjety
            {
                try { End(old, "doprecyzowana", lair); }
                catch (Exception e) { _hints.Remove(h.Lair); HintStumble("HintEnd", e); }
            }
            _hints[h.Lair] = h;
            if (source == "bohater") _dHHero++; else if (source == "banda") _dHBand++; else _dHKin++;
            _sum[6]++;
            bool know = false;
            try { HideoutSight.Heard(lair.StringId, "obozie (przesluchanie m110)"); know = HideoutSight.Knows(lair.StringId); }
            catch (Exception e) { HintStumble("HintKnow", e); }
            bool mark = false, tracked = false;
            if (s != null && s.HideoutFlagEnabled && s.HideoutFlagDays > 0)
            {
                try
                {
                    mark = PutMarker(h);
                    var vt = Campaign.Current != null ? Campaign.Current.VisualTrackerManager : null;
                    tracked = mark && vt != null && vt.CheckTracked(h.Marker);
                }
                catch (Exception e) { HintStumble("HintMarker", e); }
            }
            else _markersOwed = true;   // recenzja R4: znacznik postawi przeglad godzinny po wlaczeniu flag
            line = P10 + "wskazowka | doba " + Today + " | zrodlo " + how + " | grupa " + (string.IsNullOrEmpty(group) ? "-" : group) + " | kryjowka " + h.Lair
                   + " (kultura " + (lair.Culture != null ? lair.Culture.StringId : "-") + ", skad " + source + ", wypatrzona " + YN(lair.Hideout != null && lair.Hideout.IsSpotted) + ")"
                   + " | odniesienie " + h.Ref + " (" + f.Ref.Name + ", " + f.RefKind + ") | prawdziwa " + F2(f.D) + " jedn. na " + F2(f.Ang) + " st."
                   + " | opis \"" + f.Text + "\" = " + F2(f.Dq) + " jedn. na " + (f.Dir * 45) + " st. (" + (exact ? "dokladna" : "przyblizona") + ")"
                   + " | punkt " + F2(f.X) + "," + F2(f.Y) + " (przyciety " + YN(f.Clipped) + "), blad " + F2(f.Err) + " jedn."
                   + " | znacznik " + YN(mark) + " (sledzony " + YN(tracked) + ") | wiedza " + YN(know)
                   + " | termin godz. " + Fh(h.Due) + " (" + (due >= 0.0 ? "proba testu" : days + " dob") + " od godz. " + Fh(now) + ") | aktywne " + _hints.Count + ".";
            return h;
        }

        // ------------------------------------------------------------ znacznik obszaru
        /// <summary>Znacznik obszaru w punkcie wskazowki (herb klanu kryjowki, inaczej pierwszego klanu bandytow z herbem); odpiety przez gracza - znowu odpiety.
        /// true - stoi.</summary>
        private static bool PutMarker(Hint h)
        {
            if (h.Marker != null) return true;
            var c = Campaign.Current;
            var mm = c != null ? c.MapMarkerManager : null;
            var vt = c != null ? c.VisualTrackerManager : null;
            if (mm == null || vt == null) return false;
            Banner b = null;
            var st = Settlement.Find(h.Lair);
            var cl = st != null && st.Hideout != null ? st.Hideout.MapFaction as Clan : null;
            if (cl != null) b = cl.Banner;
            if (b == null) foreach (var k in Clan.BanditFactions) if (k != null && k.Banner != null) { b = k.Banner; break; }
            if (b == null) return false;
            var pos = new CampaignVec2(new Vec2(h.X, h.Y), true).AsVec3();   // wysokosc z mapy (brak trafienia - 0, bez wyjatku)
            h.Marker = mm.CreateMapMarker(b, new TextObject("{=!}" + MarkerName), pos, true, MarkerTag);   // gra od razu RegisterObject - przypiety
            if (h.Marker != null && !h.Pinned && vt.CheckTracked(h.Marker))
            {
                vt.RemoveTrackedObject(h.Marker, true);
                // recenzja R1: kafelek trackera (MapMarkerTrackerItemVM) powstal w CreateMapMarker jako przypiety, a IsTracked zmienia tylko klik kulki - znacznik
                // ogloszony drugi raz, MapTrackerProvider buduje kafelek od nowa jako odpiety (tych zdarzen sluchaja tylko CampaignSystem i SandBox.ViewModelCollection)
                try
                {
                    var ed = CampaignEventDispatcher.Instance;
                    if (ed != null) { ed.OnMapMarkerRemoved(h.Marker); ed.OnMapMarkerCreated(h.Marker); _vmRedo++; }
                }
                catch (Exception e) { HintStumble("HintMarkerTile", e); }
            }
            return h.Marker != null;
        }

        /// <summary>Zdejmuje znacznik wskazowki z mapy (MapMarkerManager i VisualTrackerManager - forceRemove czysci tez licznik).</summary>
        private static bool TakeMarker(Hint h)
        {
            var m = h.Marker;
            h.Marker = null;
            var c = Campaign.Current;
            if (m == null || c == null) return false;
            var mm = c.MapMarkerManager;
            var vt = c.VisualTrackerManager;
            if (mm != null) mm.RemoveMapMarker(m);
            if (vt != null && vt.CheckTracked(m)) vt.RemoveTrackedObject(m, true);
            return mm != null;
        }

        /// <summary>Czy znacznik z naszym znakiem jest jeszcze w grze (test).</summary>
        private static bool MarkerInGame(MapMarker m)
        {
            var c = Campaign.Current;
            if (m == null || c == null || c.MapMarkerManager == null) return false;
            foreach (var x in c.MapMarkerManager.GetMapMarkersByQuestId(MarkerTag)) if (x == m) return true;
            return false;
        }

        private static int MarkersInGame()
        {
            var c = Campaign.Current;
            int n = 0;
            if (c != null && c.MapMarkerManager != null) foreach (var x in c.MapMarkerManager.GetMapMarkersByQuestId(MarkerTag)) n++;
            return n;
        }

        // ------------------------------------------------------------ koniec wskazowki
        /// <summary>OnHideoutSpottedEvent: wypatrzona kryjowka z czynna wskazowka - koniec od razu ("znaleziona").</summary>
        private static void OnLairSpotted(PartyBase party, PartyBase hideout)
        {
            if (_hints.Count == 0) return;
            var st = hideout != null ? hideout.Settlement : null;
            Hint h;
            if (st == null || !_hints.TryGetValue(st.StringId ?? "", out h)) return;
            End(h, "znaleziona (droga zdarzenie gry)", st);
        }

        /// <summary>Przeglad co godzine: IsSpotted bez zdarzenia gry (zadania), termin, kryjowki juz nie ma.</summary>
        internal static void HintsHour()
        {
            if (_hints.Count == 0) return;
            double now = CampaignTime.Now.ToHours;
            List<Hint> end = null; List<string> how = null;
            foreach (var h in _hints.Values)
            {
                string w = null;
                try
                {
                    var st = Settlement.Find(h.Lair);
                    if (st == null || !st.IsHideout || st.Hideout == null) w = "brak kryjowki";
                    else if (st.Hideout.IsSpotted) w = "znaleziona (droga przeglad)";
                    else if (now >= h.Due) w = "po terminie";
                }
                catch (Exception e) { HintStumble("HintHour", e); }
                if (w == null) continue;
                if (end == null) { end = new List<Hint>(); how = new List<string>(); }
                end.Add(h); how.Add(w);
            }
            if (end != null)
                for (int i = 0; i < end.Count; i++)
                {
                    try { End(end[i], how[i], Settlement.Find(end[i].Lair)); }
                    catch (Exception e) { _hints.Remove(end[i].Lair); HintStumble("HintEnd", e); }
                }
            if (_markersOwed) MarkersOwed();   // recenzja R4: flagi wlaczone po wskazowce bez znacznika
        }

        /// <summary>Recenzja R4: wskazowki bez znacznika, bo flagi byly wylaczone (Give, zapis, start sesji) - przy flagach wlaczonych znaczniki stawiane raz
        /// (pierwszy przeglad godzinny po wlaczeniu); linia logu, gdy jakiegos brakowalo.</summary>
        private static void MarkersOwed()
        {
            var s = Settings.Current;
            if (s == null || !s.HideoutFlagEnabled || s.HideoutFlagDays <= 0) return;
            _markersOwed = false;
            int miss = 0, put = 0;
            foreach (var h in _hints.Values)
            {
                if (h.Marker != null || h.Off) continue;
                miss++;
                try { if (PutMarker(h)) put++; } catch (Exception e) { HintStumble("HintFlagsOn", e); }
            }
            if (miss > 0) Log.Info(P10 + "flagi wlaczone - znaczniki postawione " + put + " z " + miss + " (wskazowki bez znacznika) | aktywne " + _hints.Count + ".");
        }

        private static void End(Hint h, string how, Settlement st)
        {
            _hints.Remove(h.Lair);
            h.Ended = how;
            string took = "-";
            if (h.Marker != null)
            {
                bool ok = false;
                try { ok = TakeMarker(h); } catch (Exception e) { HintStumble("HintMarker", e); }
                took = YN(ok);
            }
            if (how.StartsWith("znaleziona", StringComparison.Ordinal)) { _dHFound++; _sum[7]++; }
            else if (how == "po terminie") _dHDue++;
            else if (how == "brak kryjowki") _dHGone++;
            double now = CampaignTime.Now.ToHours;
            var hd = st != null ? st.Hideout : null;
            Log.Info(P10 + "koniec wskazowki | kryjowka " + h.Lair + " (skad " + h.Src + ", wypatrzona " + YN(hd != null && hd.IsSpotted) + ", zajeta " + YN(hd != null && hd.IsInfested) + ") | "
                     + how + " | od wskazania " + Fh(now - h.Start) + " godz., termin godz. " + Fh(h.Due) + ", teraz godz. " + Fh(now) + " | znacznik zdjety " + took
                     + " | aktywne " + _hints.Count + ".");
        }

        // ------------------------------------------------------------ zapis gry: znaczniki poza zapisem
        /// <summary>OnBeforeSaveEvent: zapamietuje przypiecie i zdejmuje znaczniki wskazowek (zapis gry ich nie dostaje), sprzata zablakane z naszym znakiem.</summary>
        internal static void BeforeSaveHints()
        {
            var c = Campaign.Current;
            var mm = c != null ? c.MapMarkerManager : null;
            var vt = c != null ? c.VisualTrackerManager : null;
            if (mm == null) return;
            int took = 0, pinned = 0, stray = 0;
            foreach (var h in _hints.Values)
            {
                if (h.Marker == null) continue;
                try
                {
                    h.Pinned = vt == null || vt.CheckTracked(h.Marker);
                    if (h.Pinned) pinned++;
                    TakeMarker(h);
                    h.Off = true;
                    took++;
                }
                catch (Exception e) { HintStumble("HintSave", e); }
            }
            foreach (var m in new List<MapMarker>(mm.GetMapMarkersByQuestId(MarkerTag)))
            {
                try
                {
                    mm.RemoveMapMarker(m);
                    if (vt != null && vt.CheckTracked(m)) vt.RemoveTrackedObject(m, true);
                    stray++;
                }
                catch (Exception e) { HintStumble("HintSave", e); }
            }
            if (took + stray == 0) return;
            _dHSaveOff += took;
            int left = MarkersInGame();
            if (_saveLines < SaveLinesMax)
            {
                _saveLines++; _saveLogged = true;
                Log.Info(P10 + "zapis - znaczniki zdjete " + took + " (sledzonych " + pinned + ", zablakanych " + stray + "), zostalo w grze " + left + ".");
            }
        }

        /// <summary>OnSaveOverEvent (takze nieudany zapis): znaczniki zdjete przed zapisem wracaja (przy HideoutFlagEnabled i dobach &gt; 0), odpiete - odpiete.
        /// Recenzja R2 (kod): nieodtworzone liczone z powodem - flagi wylaczone (wroca po wlaczeniu, R4) i brak kampanii ("Save And Exit": MapScreen konczy gre
        /// w swoim sluchaczu OnSaveOver, wolanym przed naszym).</summary>
        internal static void SaveOverHints(bool ok)
        {
            var s = Settings.Current;
            bool flags = s != null && s.HideoutFlagEnabled && s.HideoutFlagDays > 0;
            bool gone = Campaign.Current == null;
            int back = 0, loose = 0, offFlags = 0, offGone = 0;
            foreach (var h in _hints.Values)
            {
                if (!h.Off) continue;
                h.Off = false;
                if (!flags) { offFlags++; continue; }
                if (gone) { offGone++; continue; }
                try { if (PutMarker(h)) { back++; if (!h.Pinned) loose++; } }
                catch (Exception e) { HintStumble("HintSaveOver", e); }
            }
            if (offFlags > 0) _markersOwed = true;
            _dHSaveOn += back; _dHSaveSkip += offFlags + offGone;
            if (_saveLogged)
            {
                _saveLogged = false;
                Log.Info(P10 + "po zapisie (udany " + YN(ok) + ") - znaczniki odtworzone " + back + " (niesledzone " + loose + "), nieodtworzone: flagi wylaczone " + offFlags
                         + ", brak kampanii " + offGone + ".");
            }
        }

        /// <summary>Start sesji (po wczytaniu): zablakane znaczniki z naszym znakiem (zapis ze zlego zapisu) zdjete i policzone, znaczniki wskazowek postawione.</summary>
        private static void RestoreMarkers()
        {
            _staleMarkers = 0;
            var c = Campaign.Current;
            var mm = c != null ? c.MapMarkerManager : null;
            var vt = c != null ? c.VisualTrackerManager : null;
            if (mm == null) return;
            foreach (var m in new List<MapMarker>(mm.GetMapMarkersByQuestId(MarkerTag)))
            {
                mm.RemoveMapMarker(m);
                if (vt != null && vt.CheckTracked(m)) vt.RemoveTrackedObject(m, true);
                _staleMarkers++;
            }
            var s = Settings.Current;
            foreach (var h in _hints.Values) { h.Marker = null; h.Off = false; }
            if (s == null || !s.HideoutFlagEnabled || s.HideoutFlagDays <= 0) { if (_hints.Count > 0) _markersOwed = true; return; }   // recenzja R4
            foreach (var h in _hints.Values)
            {
                try { PutMarker(h); } catch (Exception e) { HintStumble("HintRestore", e); }
            }
        }

        // ------------------------------------------------------------ start sesji, doba
        private static void HintsSessionStart()
        {
            try { RestoreMarkers(); } catch (Exception e) { HintStumble("HintRestore", e); }
            var s = Settings.Current;
            int marked = 0;
            var ids = new List<string>();
            foreach (var h in _hints.Values) { if (h.Marker != null) marked++; if (ids.Count < StartIdsMax) ids.Add(h.Lair); }
            Log.Info(P10 + "start sesji | klucz Interrogation Area Only " + YN(s != null && s.InterrogationAreaOnly) + " (Camp Interrogation " + YN(On) + ")"
                     + " | znacznik: Hideout Flag Enabled " + YN(s != null && s.HideoutFlagEnabled) + ", Hideout Flag Days " + (s != null ? s.HideoutFlagDays : 0)
                     + " | wiedza: Hideout Scouting Chance " + YN(s != null && s.HideoutScoutingChance) + ", Hideout Rumour Reveals " + YN(s != null && s.HideoutRumourReveals)
                     + " | precyzja: " + PrecisionText()
                     + " | wskazowki aktywne " + _hints.Count + " (ze znacznikiem " + marked + "): " + (ids.Count > 0 ? string.Join(", ", ids.ToArray()) + (_hints.Count > ids.Count ? ", ..." : "") : "-")
                     + " | stare znaczniki w zapisie " + _staleMarkers + " | zapis: " + _hintNote + " | potkniecia " + _hStumbles + ".");
        }

        private static void HintsDaily(int today)
        {
            double now = CampaignTime.Now.ToHours;
            int marked = 0, late = 0;
            foreach (var h in _hints.Values) { if (h.Marker != null) marked++; if (now > h.Due + 1.0) late++; }
            int fresh = _dHHero + _dHBand + _dHKin;
            bool act = fresh + _dHFound + _dHDue + _dHGone + _dHSaveOff + _dHSaveOn + _dHSaveSkip + _dHStumbles > 0 || _hints.Count > 0;
            if (!AreaOn && !act) return;
            Log.Info(P10 + "doba " + today + " | godz. " + Fh(now) + " | klucz " + YN(AreaOn) + " | wskazowki nowe " + fresh + " (bohater " + _dHHero + ", banda " + _dHBand + ", kultura " + _dHKin + ")"
                     + ", znalezione " + _dHFound + ", po terminie " + _dHDue + ", bez kryjowki " + _dHGone + ", aktywne " + _hints.Count + " (ze znacznikiem " + marked
                     + ", po terminie ponad godzine " + late + ") | zapisy: zdjete " + _dHSaveOff + ", odtworzone " + _dHSaveOn + ", nieodtworzone " + _dHSaveSkip
                     + " | potkniecia dzis " + _dHStumbles + ".");
        }

        // ------------------------------------------------------------ zapis "arm_interrogation" - szosta czesc
        private static bool PinNow(Hint h)
        {
            if (h.Marker == null) return h.Pinned;
            var vt = Campaign.Current != null ? Campaign.Current.VisualTrackerManager : null;
            return vt == null || vt.CheckTracked(h.Marker);
        }

        private static void WriteHints(StringBuilder sb, Dictionary<string, Hint> hints)
        {
            int n = 0;
            foreach (var h in hints.Values)
            {
                if (n++ > 0) sb.Append(';');
                sb.Append(Clean(h.Lair)).Append(',').Append(Clean(h.Ref)).Append(',').Append(h.Due.ToString("0.###", Inv)).Append(',').Append(h.X.ToString("0.###", Inv)).Append(',')
                  .Append(h.Y.ToString("0.###", Inv)).Append(',').Append(Clean(h.Src)).Append(',').Append(h.Start.ToString("0.###", Inv)).Append(',').Append(PinNow(h) ? '1' : '0');
            }
        }

        /// <summary>Odczyt szostej czesci (wskazowki); zle wpisy pominiete i policzone.</summary>
        private static int ParseHints(string part, Dictionary<string, Hint> hints)
        {
            int bad = 0;
            foreach (var e in part.Split(';'))
            {
                if (e.Length == 0) continue;
                var a = e.Split(',');
                double due, start; float x, y;
                if (a.Length < 8 || a[0].Length == 0 || !double.TryParse(a[2], NumberStyles.Float, Inv, out due) || !float.TryParse(a[3], NumberStyles.Float, Inv, out x)
                    || !float.TryParse(a[4], NumberStyles.Float, Inv, out y) || !double.TryParse(a[6], NumberStyles.Float, Inv, out start) || (a[7] != "0" && a[7] != "1")) { bad++; continue; }
                hints[a[0]] = new Hint { Lair = a[0], Ref = a[1], Due = due, X = x, Y = y, Src = a[5], Start = start, Pinned = a[7] == "1" };
            }
            return bad;
        }
    }
}
