using System;
using System.Collections.Generic;
using System.IO;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>
    /// m109-przesluch - PROBA AUTOTESTU (tylko DLL autotestu CrashScribe - LogGate.Mode 1/2 - i plik Documents\Mount and Blade II Bannerlord\CrashScribe\
    /// armoury-przesluch.flag; plik tylko czytany, nie zuzyty; w zwyklej grze nic nie robi). Raz na sesje, od 2. doby sesji (najpozniej do 10. - inaczej
    /// "bez proby (powod)"), gdy partia gracza nie jest w bitwie ani spotkaniu. Przez Act i Target (bez okien):
    ///  T0 kryjowka H1 (najblizsza zajeta, niewypatrzona, z klanem bandytow i jego zolnierzem kultury kryjowki), H2 (inna takiej kultury, brak - H1); jency testowi:
    ///     A (3 zdrowych), B (inny rodzaj: 1 zdrowy i 1 ranny; brak - pominiete), L (zolnierz kultury bez kryjowek); pamiec gracza na czas proby odlozona.
    ///  T2 tortury A rzutami 0.99 / 0.0 (wpis pamieci {C, H2, doba -3}): milczy i umiera - A -1, Mercy -60, plotka (przy Inn Gossip);
    ///  T1 rozmowa A rzutem 0: H2 ujawniona "skad banda" (+ flaga przy HideoutFlagEnabled); T3 rozmowa A drugi raz - odmowa (limit);
    ///  T3b rozmowa B (inny stos, ten sam rodzaj bandy) - odmowa (limit rodzaju bandy, recenzja R3); potem slot rodzaju zwolniony (jak nowa doba);
    ///  T7 bohater BK (wzor bannerkings_bandithero, ur. w H1, klan C) - tortury 0 / 0: mowi "skad bohater" i umiera (brak wzoru albo H1 = H2 - POMINIETY);
    ///  T4/T5 bez wpisu pamieci rozmowa i tortury B zwyklymi rzutami - zgodnosc szansy, rzutu i skutku ("skad kultura" albo odmowa bez kryjowki);
    ///  T6 Target(L) - "kultura bez kryjowek"; T8 miara perswazji wobec ludzi najblizszego miasta = suma skladnikow, plotek na kufel; T9 zapis tam i z powrotem.
    ///  Sprzatanie: jency testowi zdjeci (stan lochu jak przed proba), pamiec, uzycia i kajdany gracza wrocone (na czas proby wpisy kajdan rodzajow A, B, L zdjete -
    ///  jency testowi "pojmani dzis"). Mercy, relacje, ujawnione kryjowki i plotka zostaja (bieg testowy). Ksiega ludzi: jency testowi bez Give, zgon przez Take
    ///  zdejmuje udzialy lochu gracza (recenzja kod R3 - artefakt biegu testowego, opis w planie autotestu).
    /// m110-okolica (klucz InterrogationAreaOnly TAK): T0 - H1/H2 takze bez czynnej wskazowki; T1 - wskazowka H2 czynna, H2 NIE wypatrzona (IsVisible jak przed),
    ///  znacznik obszaru przy flagach (przypiety), wiedza (HideoutSight.Knows) przy Hideout Scouting Chance i Hideout Rumour Reveals, opis z nazwa osady odniesienia;
    ///  T1b (oba tryby) - Target(A) nie wskazuje H2 drugi raz; T7 - wskazowka H1, H1 niewypatrzona (najpierw probna przyblizona H1 - herszt ja doprecyzowuje,
    ///  recenzja R3); T12 - zapis: BeforeSaveHints zdejmuje wszystkie znaczniki (odpiety H1 albo H2), SaveOverHints oddaje je z przypieciem, kazdy odpiety
    ///  ogloszony grze drugi raz (kafelek trackera, recenzja R1); T13 - znacznik H2 zdjety jak przy flagach wylaczonych, przeglad godzinny stawia go (R4); T10 - H1 IsSpotted na chwile -> HintsHour "znaleziona", znacznik zdjety, suma +1;
    ///  T11 - probna wskazowka H1 po terminie -> "po terminie"; T4 - MOWI => wskazowka czynna i niewypatrzona; T9 - zapis tam i z powrotem z wskazowkami.
    ///  Sprzatanie: probne wskazowki zdjete, wskazowki prob (H2 z T1, ewentualne z T4/T5) i wiedza H1/H2 zostaja jako slad biegu (jak w 104 wypatrzona H2 z flaga).
    /// Linia "Przesluchanie (m109): test - ... - wynik TAK|NIE." (grupa m109przes PRZES-7, m110okol OKOL-8).
    /// </summary>
    internal static partial class CampInterrogation
    {
        internal const string AutoFlagName = "armoury-przesluch.flag";
        private const int TestFirstDay = 2, TestLastDay = 10;
        private static bool _testDone, _testFlag;
        private static int _testFlagDay = -1;

        private static void TestReset() { _testDone = false; _testFlag = false; _testFlagDay = -1; }

        private static bool TestWanted(int today)
        {
            int m = LogGate.Mode;
            if (m != 1 && m != 2) return false;   // tylko DLL autotestu CrashScribe
            if (_testFlagDay != today)
            {
                _testFlagDay = today;
                try
                {
                    var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                    _testFlag = File.Exists(Path.Combine(Path.Combine(Path.Combine(docs, "Mount and Blade II Bannerlord"), "CrashScribe"), AutoFlagName));
                }
                catch { _testFlag = false; }
            }
            return _testFlag;
        }

        private static void TestStep(int today)
        {
            if (_testDone || !TestWanted(today) || _sessionDays < TestFirstDay) return;
            string why = null;
            var mp = MobileParty.MainParty;
            if (!On) why = "klucz Camp Interrogation NIE";
            else if (mp == null || Hero.MainHero == null) why = "brak partii gracza";
            else if (mp.MapEvent != null || mp.SiegeEvent != null || (PlayerEncounter.Current != null && PlayerEncounter.EncounterSettlement == null)) why = "bitwa albo spotkanie";   // 23.10 test 109d: postoj w osadzie (PlayerEncounter z osada) nie blokuje - autotest stoi w miescie
            else if (!Hero.MainHero.IsAlive || Hero.MainHero.IsPrisoner) why = "gracz w niewoli";
            if (why != null)
            {
                if (!On || _sessionDays >= TestLastDay) { _testDone = true; Log.Info(P + "test - bez proby (" + why + (On ? ", do " + TestLastDay + ". doby sesji" : "") + ")."); }
                return;
            }
            _testDone = true;
            TestRun(today);
        }

        private struct Cnt { public int H, W; }

        private static Cnt CountOf(CharacterObject t)
        {
            var c = new Cnt();
            var r = MobileParty.MainParty.PrisonRoster;
            int i = t != null ? r.FindIndexOfTroop(t) : -1;
            if (i >= 0) { var el = r.GetElementCopyAtIndex(i); c.W = Math.Max(0, el.WoundedNumber); c.H = Math.Max(0, el.Number - c.W); }
            return c;
        }

        /// <summary>LimitKey stosu rodzaju t (jak LimitKey(Group) dla zwyklych jencow: Group.Culture = t.Culture, Key = Clean(t.StringId)).</summary>
        private static string TestLimitKey(CharacterObject t)
        {
            return t.Culture != null && !string.IsNullOrEmpty(t.Culture.StringId) ? "c:" + Clean(t.Culture.StringId) : Clean(t.StringId);
        }

        private static bool NoLairCulture(CultureObject c)
        {
            if (c == null) return false;
            foreach (var h in Hideout.All) if (h != null && h.Settlement != null && h.Settlement.Culture == c) return false;
            return true;
        }

        private static void TestRun(int today)
        {
            var mp = MobileParty.MainParty;
            var r = mp.PrisonRoster;
            var s = Settings.Current;
            var pts = new List<string>();
            bool ok = true;
            // 23.10 test 109f: znacznik poczatku - proby testu (do linii wyniku) nie sa gra: test zwalnia slot rodzaju bandy po T3b (PRZES-5 je pomija)
            Log.Info(P + "test - start (proby testu do linii wyniku; slot rodzaju bandy zwalniany po T3b jak nowa doba).");
            // ---------------- T0: kryjowki, kultura, rodzaje jencow
            Settlement h1 = null, h2 = null; Clan clan = null; CultureObject c = null; CharacterObject ta = null, tb = null, tl = null;
            float best = float.MaxValue;
            Vec2 at = mp.Position.ToVec2();
            foreach (var h in Hideout.All)
            {
                var st = h != null ? h.Settlement : null;
                if (st == null || st.Culture == null || !h.IsInfested || h.IsSpotted || Hinted(st)) continue;   // m110-okolica: bez czynnej wskazowki (bieg c - zapis z wskazowka)
                var k = h.MapFaction as Clan;
                if (k == null || !k.IsBanditFaction || k.DefaultPartyTemplate == null || k.DefaultPartyTemplate.Stacks == null) continue;
                CharacterObject a = null, b = null;
                foreach (var ps in k.DefaultPartyTemplate.Stacks)
                {
                    var t = ps.Character;
                    if (!BanditTroop(t) || t.Culture != st.Culture) continue;
                    if (a == null) a = t; else if (b == null && t != a) b = t;
                }
                if (a == null) continue;
                float d = st.GetPosition2D.Distance(at);
                if (d < best) { best = d; h1 = st; clan = k; c = st.Culture; ta = a; tb = b; }
            }
            if (h1 == null) { Log.Info(P + "test - bez proby (brak zajetej, niewypatrzonej kryjowki z klanem bandytow i jego zolnierzem kultury kryjowki)."); return; }
            float b2 = float.MaxValue;
            foreach (var h in Hideout.All)
            {
                var st = h != null ? h.Settlement : null;
                if (st == null || st == h1 || st.Culture != c || !h.IsInfested || h.IsSpotted || Hinted(st)) continue;
                float d = st.GetPosition2D.Distance(h1.GetPosition2D);
                if (d < b2) { b2 = d; h2 = st; }
            }
            if (h2 == null) h2 = h1;
            foreach (var k in Clan.BanditFactions)
            {
                if (tl != null) break;
                if (k == null || k.DefaultPartyTemplate == null || k.DefaultPartyTemplate.Stacks == null) continue;
                foreach (var ps in k.DefaultPartyTemplate.Stacks)
                    if (BanditTroop(ps.Character) && NoLairCulture(ps.Character.Culture)) { tl = ps.Character; break; }
            }
            Cnt a0 = CountOf(ta), bb0 = CountOf(tb), l0 = CountOf(tl);
            int mercy0 = MercyXp();
            var stash = new List<Band>(_bands);
            string kA = TestLimitKey(ta);   // A i B - ten sam rodzaj bandy (kultura kryjowki H1)
            var keys = new List<string> { kA };
            if (tb != null && !keys.Contains(TestLimitKey(tb))) keys.Add(TestLimitKey(tb));
            if (tl != null && !keys.Contains(TestLimitKey(tl))) keys.Add(TestLimitKey(tl));
            var usedStash = new Dictionary<string, int[]>(StringComparer.Ordinal);
            foreach (var k in keys) { int[] u; if (_used.TryGetValue(k, out u)) { usedStash[k] = u; _used.Remove(k); } }
            var seenKeys = new List<string> { Clean(ta.StringId) };
            if (tb != null) seenKeys.Add(Clean(tb.StringId));
            if (tl != null) seenKeys.Add(Clean(tl.StringId));
            var seenStash = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var k in seenKeys) { int d; if (_seen.TryGetValue(k, out d)) { seenStash[k] = d; _seen.Remove(k); } }
            Hero hero = null;
            bool area = AreaOn;   // m110-okolica: tryb okolicy (klucz Interrogation Area Only)
            bool flagWant = s != null && s.HideoutFlagEnabled && s.HideoutFlagDays > 0;
            var vt = Campaign.Current.VisualTrackerManager;
            pts.Add("T0 TAK (H1 " + h1.StringId + ", H2 " + h2.StringId + (h2 == h1 ? " = H1" : "") + ", kultura " + c.StringId + ", klan " + clan.StringId + ", A " + ta.StringId
                    + ", B " + (tb != null ? tb.StringId : "-") + ", L " + (tl != null ? tl.StringId : "-") + ", od obozu " + best.ToString("0.0", Inv) + " jedn.)");
            try
            {
                _bands.Clear();
                r.AddToCounts(ta, 3);
                if (tb != null) r.AddToCounts(tb, 2, false, 1);
                if (tl != null) r.AddToCounts(tl, 1);
                var mem = new Band { Day = today - 3, Culture = Clean(c.StringId), Hideout = Clean(h2.StringId), X = h2.GetPosition2D.x, Y = h2.GetPosition2D.y, Party = "test" };
                _bands.Add(mem);

                // ---------------- T2: tortury A (0.99 / 0.0) - milczy i umiera
                {
                    var before = CountOf(ta); int m0 = MercyXp();
                    var res = Act(Find(Clean(ta.StringId)), true, 0.99f, 0f);
                    var after = CountOf(ta); int dm = MercyXp() - m0;
                    bool rumourWant = Rumours.On;
                    bool t = !res.Refused && !res.Talked && res.Died && after.H == before.H - 1 && after.W == before.W && dm <= 0 && dm >= MercyTorture + MercyDeath && res.Rumour == rumourWant;
                    ok &= t;
                    pts.Add("T2 " + (t ? "TAK" : "NIE") + " (odmowa " + (res.Refused ? res.RefuseWhy : "-") + ", szansa " + F4(res.Chance) + ", mowi " + (res.Talked ? "TAK" : "NIE") + ", smierc "
                            + (res.Died ? "TAK" : "NIE") + ", A " + before.H + "/" + before.W + " -> " + after.H + "/" + after.W + ", Mercy xp " + dm + ", plotka " + (res.Rumour ? "TAK" : "NIE")
                            + " (Inn Gossip " + (rumourWant ? "TAK" : "NIE") + "))");
                }
                // ---------------- T1: rozmowa A rzutem 0 - H2 ze "skad banda" i flaga (m110-okolica: wskazowka, H2 niewypatrzona, znacznik obszaru, wiedza jak plotka)
                {
                    bool vis0 = h2.IsVisible;
                    var res = Act(Find(Clean(ta.StringId)), false, 0f);
                    if (!area)
                    {
                        bool t = !res.Refused && res.Talked && res.Lair == h2 && res.Source == "banda" && h2.Hideout.IsSpotted && h2.IsVisible && (!flagWant || res.Flag);
                        ok &= t;
                        pts.Add("T1 " + (t ? "TAK" : "NIE") + " (odmowa " + (res.Refused ? res.RefuseWhy : "-") + ", szansa " + F4(res.Chance) + ", kryjowka " + (res.Lair != null ? res.Lair.StringId : "-")
                                + ", skad " + (res.Source ?? "-") + ", wypatrzona " + (h2.Hideout.IsSpotted ? "TAK" : "NIE") + ", flaga " + (res.Flag ? "TAK" : "NIE") + (flagWant ? "" : " (flagi wylaczone)") + ")");
                    }
                    else
                    {
                        Hint hh;
                        _hints.TryGetValue(h2.StringId ?? "", out hh);
                        bool knowWant = s != null && s.HideoutScoutingChance && s.HideoutRumourReveals;
                        bool know = HideoutSight.Knows(h2.StringId);
                        bool mark = hh != null && hh.Marker != null, pin = mark && vt != null && vt.CheckTracked(hh.Marker);
                        var rf = hh != null ? Settlement.Find(hh.Ref) : null;
                        string rn = rf != null && rf.Name != null ? rf.Name.ToString() : null;
                        bool refOk = rn != null && res.Where != null && res.Where.Contains(rn);
                        bool t = !res.Refused && res.Talked && res.Lair == h2 && res.Source == "banda" && hh != null && !h2.Hideout.IsSpotted && h2.IsVisible == vis0 && !res.Flag
                                 && (flagWant ? mark && pin && res.Marker : !mark) && (!knowWant || know) && refOk;
                        ok &= t;
                        pts.Add("T1 " + YN(t) + " (odmowa " + (res.Refused ? res.RefuseWhy : "-") + ", szansa " + F4(res.Chance) + ", kryjowka " + (res.Lair != null ? res.Lair.StringId : "-")
                                + ", skad " + (res.Source ?? "-") + ", wskazowka " + YN(hh != null) + ", wypatrzona " + YN(h2.Hideout.IsSpotted) + ", widoczna jak przed " + YN(h2.IsVisible == vis0)
                                + ", flaga " + YN(res.Flag) + ", znacznik " + YN(mark) + " (przypiety " + YN(pin) + ")" + (flagWant ? "" : " (flagi wylaczone)") + ", wiedza " + YN(know)
                                + " (oczekiwana " + YN(knowWant) + "), opis \"" + (res.Where ?? "-") + "\", odniesienie " + (rn ?? "-") + ")");
                    }
                }
                // ---------------- T1b (m110-okolica, oba tryby): A drugi raz nie wskaze H2 (wskazana albo wypatrzona) - Target(A) inna kryjowka albo brak
                {
                    var gA = Find(Clean(ta.StringId));
                    string src = null, w = null, wl = null; Band m = null;
                    var lt = gA != null ? Target(gA, out src, out m, out w, out wl) : null;
                    bool t = gA != null && lt != h2;
                    ok &= t;
                    pts.Add("T1b " + YN(t) + " (grupa " + YN(gA != null) + ", Target(A) " + (lt != null ? lt.StringId + " skad " + src : "brak: " + (wl ?? "-")) + ", H2 "
                            + (area ? "wskazana " + YN(Hinted(h2)) : "wypatrzona " + YN(h2.Hideout.IsSpotted)) + ")");
                }
                // ---------------- T3: rozmowa A drugi raz - limit doby
                {
                    var res = Act(Find(Clean(ta.StringId)), false, 0f);
                    bool t = res.Refused && res.RefuseWhy == "limit";
                    ok &= t;
                    pts.Add("T3 " + (t ? "TAK" : "NIE") + " (odmowa " + (res.Refused ? res.RefuseWhy : "-") + ")");
                }
                // ---------------- T3b: rozmowa B (inny stos, ten sam rodzaj bandy) - limit rodzaju bandy
                if (tb == null) pts.Add("T3b POMINIETY (brak drugiego rodzaju)"); else
                {
                    var res = Act(Find(Clean(tb.StringId)), false, 0f);
                    bool t = res.Refused && res.RefuseWhy == "limit";
                    ok &= t;
                    pts.Add("T3b " + (t ? "TAK" : "NIE") + " (B, klucz limitu " + kA + ": odmowa " + (res.Refused ? res.RefuseWhy : "-") + ")");
                }
                // ---------------- T7: bohater BK (opcjonalny)
                {
                    string skip = null;
                    CharacterObject tpl = null;
                    if (h2 == h1) skip = area ? "jedna kryjowka kultury - H1 juz wskazana" : "jedna kryjowka kultury - H1 juz ujawniona";
                    else
                    {
                        foreach (var co in CharacterObject.All) if (co != null && co.StringId != null && co.StringId.Contains("bannerkings_bandithero")) { tpl = co; break; }
                        if (tpl == null) skip = "brak wzoru bannerkings_bandithero";
                    }
                    if (skip != null) pts.Add("T7 POMINIETY (" + skip + ")");
                    else
                    {
                        hero = HeroCreator.CreateSpecialHero(tpl, h1, clan, null, 30);
                        TakePrisonerAction.Apply(mp.Party, hero);
                        var g = Find(Clean("h:" + hero.StringId));
                        // m110-okolica (recenzja R3): klucz TAK - H1 najpierw z probna wskazowka przyblizona ("kultura"); herszt ja doprecyzowuje (Target jej nie pomija)
                        Hint pre = null, cur = null;
                        if (area && !Hinted(h1)) { string pw, pl; pre = Give(h1, "kultura", ProbeHow, "-", out pw, out pl); if (pl != null) Log.Info(pl); }
                        var res = Act(g, true, 0f, 0f);
                        bool gone = Find(Clean("h:" + hero.StringId)) == null;
                        if (area) _hints.TryGetValue(h1.StringId ?? "", out cur);
                        bool refine = pre == null || (pre.Ended == "doprecyzowana" && cur != null && cur != pre && cur.Src == "bohater" && !cur.Probe);
                        bool t = g != null && !res.Refused && res.Talked && res.Source == "bohater" && res.Lair == h1 && ((res.Died && !hero.IsAlive && gone) || res.Blocked)
                                 && (!area || (Hinted(h1) && !h1.Hideout.IsSpotted && refine));   // m110-okolica: wskazowka H1 (doprecyzowana), H1 niewypatrzona
                        ok &= t;
                        pts.Add("T7 " + (t ? "TAK" : "NIE") + " (wzor " + tpl.StringId + ", grupa " + (g != null ? "TAK" : "NIE") + ", odmowa " + (res.Refused ? res.RefuseWhy : "-") + ", szansa " + F4(res.Chance)
                                + ", skad " + (res.Source ?? "-") + ", smierc " + (res.Died ? "TAK" : res.Blocked ? "zablokowana" : "NIE") + ", zyje " + (hero.IsAlive ? "TAK" : "NIE")
                                + ", w lochu " + (gone ? "NIE" : "TAK") + (area ? ", wskazowka H1 " + YN(Hinted(h1)) + ", H1 wypatrzona " + YN(h1.Hideout.IsSpotted)
                                + ", doprecyzowanie " + (pre == null ? "-" : YN(refine) + " (probna " + (pre.Ended ?? "czynna") + ", nowa skad " + (cur != null ? cur.Src : "-") + ")") : "") + ")");
                    }
                }
                // ---------------- T12 (m110-okolica): zapis - znaczniki zdjete przed zapisem (odpiety H1, inaczej H2), oddane po zapisie z przypieciem
                if (!area || !flagWant) pts.Add("T12 POMINIETY (" + (!area ? "klucz Interrogation Area Only NIE" : "flagi wylaczone") + ")");
                else
                {
                    Hint a1 = null, a2;
                    if (h1 != h2) _hints.TryGetValue(h1.StringId ?? "", out a1);
                    _hints.TryGetValue(h2.StringId ?? "", out a2);
                    var un = a1 ?? a2;
                    var withMarker = new List<Hint>();
                    foreach (var h in _hints.Values) if (h.Marker != null) withMarker.Add(h);
                    bool unOk0 = un != null && un.Marker != null;
                    if (unOk0 && vt.CheckTracked(un.Marker)) vt.RemoveTrackedObject(un.Marker, true);
                    BeforeSaveHints();
                    int left = MarkersInGame();
                    bool nulls = true;
                    foreach (var h in _hints.Values) if (h.Marker != null) nulls = false;
                    int loose = 0, redo0 = _vmRedo;
                    foreach (var h in withMarker) if (!h.Pinned) loose++;
                    SaveOverHints(true);
                    int redo = _vmRedo - redo0;   // recenzja R1: kazdy odpiety ogloszony grze drugi raz (kafelek od nowa jako odpiety)
                    int back = 0; bool pins = true;
                    foreach (var h in withMarker)
                    {
                        if (h.Marker == null) continue;
                        back++;
                        if (vt.CheckTracked(h.Marker) != (h != un)) pins = false;
                    }
                    bool t = unOk0 && left == 0 && nulls && back == withMarker.Count && pins && loose >= 1 && redo == loose;
                    if (un != null && un.Marker != null && !vt.CheckTracked(un.Marker)) vt.RegisterObject(un.Marker);   // przypiecie z powrotem (slad biegu jak przed krokiem)
                    ok &= t;
                    pts.Add("T12 " + YN(t) + " (odpiety " + (un == a1 && a1 != null ? "H1" : un != null ? "H2" : "-") + ", ze znacznikiem " + withMarker.Count + ", po zdjeciu w grze " + left
                            + ", wpisy bez znacznika " + YN(nulls) + ", odtworzone " + back + ", przypiecie zgodne " + YN(pins) + ", kafelek odpietych odswiezony " + redo + "/" + loose + ")");
                }
                // ---------------- T13 (m110-okolica, recenzja R4): wskazowka bez znacznika (jak przy flagach wylaczonych) - przeglad godzinny stawia go, przypiecie jak przed
                if (!area || !flagWant) pts.Add("T13 POMINIETY (" + (!area ? "klucz Interrogation Area Only NIE" : "flagi wylaczone") + ")");
                else
                {
                    Hint a2;
                    _hints.TryGetValue(h2.StringId ?? "", out a2);
                    bool had = a2 != null && a2.Marker != null, pin0 = had && vt.CheckTracked(a2.Marker);
                    bool t = false, off = false, back = false, inGame = false, pinOk = false;
                    if (had)
                    {
                        var old = a2.Marker;
                        a2.Pinned = pin0;
                        TakeMarker(a2);
                        off = a2.Marker == null && !MarkerInGame(old);
                        _markersOwed = true;
                        HintsHour();
                        back = a2.Marker != null;
                        inGame = back && MarkerInGame(a2.Marker);
                        pinOk = back && vt.CheckTracked(a2.Marker) == pin0;
                        t = off && back && inGame && pinOk && !_markersOwed && Hinted(h2);
                    }
                    ok &= t;
                    pts.Add("T13 " + YN(t) + " (wskazowka H2 ze znacznikiem " + YN(had) + ", zdjety " + YN(off) + ", po przegladzie znacznik " + YN(back) + ", w grze " + YN(inGame)
                            + ", przypiecie jak przed " + YN(pinOk) + ")");
                }
                // ---------------- T10 (m110-okolica): wypatrzenie wskazanej H1 - przeglad godzinny konczy wskazowke "znaleziona", znacznik zdjety, suma znalezionych +1
                if (!area || h1 == h2) pts.Add("T10 POMINIETY (" + (!area ? "klucz Interrogation Area Only NIE" : "H1 = H2") + ")");
                else
                {
                    Hint a1;
                    bool probe = false;
                    if (!_hints.TryGetValue(h1.StringId ?? "", out a1))   // T7 pominiety - wskazowka probna
                    {
                        string w, l;
                        a1 = Give(h1, "kultura", ProbeHow, "-", out w, out l);
                        if (l != null) Log.Info(l);
                        probe = true;
                    }
                    bool t = false, hadM = false, mGone = false;
                    string got = "-";
                    int f0 = _sum[7];
                    if (a1 != null)
                    {
                        var mk = a1.Marker;
                        hadM = mk != null;
                        bool sp0 = h1.Hideout.IsSpotted;
                        h1.Hideout.IsSpotted = true;
                        try { HintsHour(); }
                        finally { h1.Hideout.IsSpotted = sp0; }   // w tym samym wywolaniu doby - HideoutSpotter i gra tego nie widza
                        got = a1.Ended ?? "-";
                        mGone = !hadM || (a1.Marker == null && !MarkerInGame(mk));
                        t = !Hinted(h1) && got == "znaleziona (droga przeglad)" && mGone && _sum[7] == f0 + 1 && !h1.Hideout.IsSpotted;
                    }
                    ok &= t;
                    pts.Add("T10 " + YN(t) + " (wskazowka H1 " + (a1 == null ? "BRAK" : probe ? "probna" : "z T7") + ", koniec " + got + ", znacznik " + (hadM ? (mGone ? "zdjety" : "STOI") : "-")
                            + ", znalezione " + f0 + " -> " + _sum[7] + ", H1 wypatrzona " + YN(h1.Hideout.IsSpotted) + ")");
                }
                // ---------------- T11 (m110-okolica): probna wskazowka H1 z terminem godzine temu - przeglad godzinny "po terminie", znacznik zdjety
                if (!area || h1 == h2) pts.Add("T11 POMINIETY (" + (!area ? "klucz Interrogation Area Only NIE" : "H1 = H2") + ")");
                else
                {
                    Hint a = null;
                    string w = null, l = null;
                    if (!Hinted(h1)) a = Give(h1, "kultura", ProbeHow, "-", out w, out l, CampaignTime.Now.ToHours - 1.0);
                    if (l != null) Log.Info(l);
                    bool t = false, hadM = false, mGone = false;
                    if (a != null)
                    {
                        var mk = a.Marker;
                        hadM = mk != null;
                        HintsHour();
                        mGone = !hadM || (a.Marker == null && !MarkerInGame(mk));
                        t = a.Ended == "po terminie" && !Hinted(h1) && mGone && (hadM || !flagWant);
                    }
                    ok &= t;
                    pts.Add("T11 " + YN(t) + " (wskazowka probna " + YN(a != null) + ", koniec " + (a != null ? a.Ended ?? "-" : "-") + ", znacznik " + (hadM ? (mGone ? "zdjety" : "STOI") : "-") + ")");
                }
                // ---------------- T4/T5: B zwyklymi rzutami bez wpisu pamieci (slot rodzaju bandy wolny - jak nowa doba)
                _bands.Clear();
                _used.Remove(kA);
                if (tb == null) pts.Add("T4 POMINIETY (brak drugiego rodzaju)"); else
                {
                    var res = Act(Find(Clean(tb.StringId)), false);
                    bool t = res.Refused ? res.RefuseWhy != "limit" && res.RefuseWhy != "brak grupy"
                                         : res.Talked == (res.Roll < res.Chance) && (!res.Talked || (res.Source == "kultura" && res.Lair != null
                                           && (area ? Hinted(res.Lair) && !res.Lair.Hideout.IsSpotted : res.Lair.Hideout.IsSpotted)));   // m110-okolica: MOWI => wskazowka, nie wypatrzenie
                    ok &= t;
                    pts.Add("T4 " + (t ? "TAK" : "NIE") + " (" + (res.Refused ? "odmowa: " + res.RefuseWhy : "szansa " + F4(res.Chance) + ", rzut " + F4(res.Roll) + " -> " + (res.Talked ? "MOWI, skad " + res.Source : "MILCZY"))
                            + (area && res.Talked ? ", wskazowka " + YN(Hinted(res.Lair)) + ", wypatrzona " + YN(res.Lair != null && res.Lair.Hideout.IsSpotted) : "") + ")");
                }
                if (tb == null) pts.Add("T5 POMINIETY (brak drugiego rodzaju)"); else
                {
                    var before = CountOf(tb);
                    var res = Act(Find(Clean(tb.StringId)), true);
                    var after = CountOf(tb);
                    bool t;
                    if (res.Refused) t = res.RefuseWhy != "limit" && res.RefuseWhy != "brak grupy";
                    else
                    {
                        bool col = res.Died ? (before.H > 0 ? after.H == before.H - 1 && after.W == before.W : after.W == before.W - 1 && after.H == before.H) : after.H == before.H && after.W == before.W;
                        t = res.Talked == (res.Roll < res.Chance) && res.Died == (res.DeathRoll < res.DeathChance) && col && (!res.Talked || res.Source == "kultura");
                    }
                    ok &= t;
                    pts.Add("T5 " + (t ? "TAK" : "NIE") + " (" + (res.Refused ? "odmowa: " + res.RefuseWhy : "szansa " + F4(res.Chance) + ", rzut " + F4(res.Roll) + " -> " + (res.Talked ? "MOWI" : "MILCZY")
                            + ", smierc " + F4(res.DeathChance) + " / " + F4(res.DeathRoll) + " -> " + (res.Died ? "TAK" : "NIE") + ", B " + before.H + "/" + before.W + " -> " + after.H + "/" + after.W) + ")");
                }
                // ---------------- T6: L - kultura bez kryjowek
                if (tl == null) pts.Add("T6 POMINIETY (brak zolnierza kultury bez kryjowek)"); else
                {
                    var g = Find(Clean(tl.StringId));
                    string src = null, w = null, wl = null; Band m = null;
                    var lair = g != null ? Target(g, out src, out m, out w, out wl) : null;
                    bool t = g != null && lair == null && w == WhyNoKind;
                    ok &= t;
                    pts.Add("T6 " + (t ? "TAK" : "NIE") + " (grupa " + (g != null ? "TAK" : "NIE") + ", kryjowka " + (lair != null ? lair.StringId : "-") + ")");
                }
            }
            catch (Exception e) { ok = false; pts.Add("WYJATEK " + e.GetType().Name); Stumble("TestRun", e); }
            // ---------------- sprzatanie
            bool clean = true;
            try
            {
                foreach (var t in new[] { ta, tb, tl })
                {
                    if (t == null) continue;
                    Cnt was = t == ta ? a0 : t == tb ? bb0 : l0, now = CountOf(t);
                    int dh = Math.Max(0, now.H - was.H), dw = Math.Max(0, now.W - was.W);
                    if (dh + dw > 0) r.AddToCounts(t, -(dh + dw), false, -dw);
                    var fin = CountOf(t);
                    if (fin.H > was.H || fin.W > was.W) clean = false;
                }
                if (hero != null && hero.IsAlive)
                {
                    KillCharacterAction.ApplyByRemove(hero);
                    if (hero.IsAlive) clean = false;
                }
            }
            catch (Exception e) { clean = false; Stumble("TestClean", e); }
            _bands.Clear(); _bands.AddRange(stash);
            foreach (var k in keys) _used.Remove(k);
            if (hero != null) _used.Remove(Clean("h:" + hero.StringId));
            foreach (var kv in usedStash) _used[kv.Key] = kv.Value;
            foreach (var k in seenKeys) _seen.Remove(k);
            foreach (var kv in seenStash) _seen[kv.Key] = kv.Value;
            // m110-okolica: probne wskazowki zdjete; wskazowki prob (H2 z T1, ewentualne z T4/T5) zostaja jako slad biegu (jak w 104 wypatrzona H2 z flaga)
            int probes = 0;
            var keepHints = new List<string>();
            try
            {
                var pl = new List<Hint>();
                foreach (var h in _hints.Values) if (h.Probe) pl.Add(h);
                foreach (var h in pl) { End(h, "zdjeta (sprzatanie testu)", Settlement.Find(h.Lair)); probes++; }
                foreach (var h in _hints.Values) if (keepHints.Count < 5) keepHints.Add(h.Lair + " (termin godz. " + Fh(h.Due) + ")");
            }
            catch (Exception e) { clean = false; HintStumble("HintTestClean", e); }
            ok &= clean;
            // ---------------- T8: miara w karczmie najblizszego miasta
            try
            {
                Settlement town = null; float td = float.MaxValue;
                foreach (var t in Town.AllTowns) { if (t == null || t.Settlement == null) continue; float d = t.Settlement.GetPosition2D.Distance(at); if (d < td) { td = d; town = t.Settlement; } }
                var p = Persuasion.Measure(Hero.MainHero, town != null ? town.Culture : null);
                int ch = InnDrink.Charm();
                bool t8 = p.Total == p.Charm + p.Stature + p.Favors + p.Leader + p.Master + p.Difficulty && p.Charm == ch;
                ok &= t8;
                pts.Add("T8 " + (t8 ? "TAK" : "NIE") + " (miasto " + (town != null ? town.StringId : "-") + ", " + Persuasion.LogText(p) + ", plotek na kufel: miara " + InnDrink.PerDrink(p.Total, 99)
                        + ", Urok " + InnDrink.PerDrink(ch, 99) + ")");
            }
            catch (Exception e) { ok = false; pts.Add("T8 NIE (wyjatek " + e.GetType().Name + ")"); Stumble("TestT8", e); }
            // ---------------- T9: zapis tam i z powrotem
            try
            {
                string ex = Write(_bands, _used, _sum, _seen, _hints);
                var bands = new List<Band>(); var used = new Dictionary<string, int[]>(StringComparer.Ordinal); var sum = new int[_sum.Length]; int bad, badH; bool hp;
                var seen = new Dictionary<string, int>(StringComparer.Ordinal);
                var hints = new Dictionary<string, Hint>(StringComparer.Ordinal);
                bool parsed = Parse(ex, bands, used, sum, seen, hints, out bad, out badH, out hp);
                bool same = parsed && bad == 0 && bands.Count == _bands.Count && used.Count == _used.Count && seen.Count == _seen.Count;
                for (int i = 0; same && i < sum.Length; i++) same = sum[i] == _sum[i];
                foreach (var kv in _seen) { int d; if (!seen.TryGetValue(kv.Key, out d) || d != kv.Value) same = false; }
                // m110-okolica: wskazowki (kryjowka, odniesienie, termin i wskazanie +-1e-3 godz., punkt +-1e-3, skad, przypiecie) i 8 sum
                bool hs = hp && badH == 0 && hints.Count == _hints.Count;
                foreach (var kv in _hints)
                {
                    Hint q;
                    var h = kv.Value;
                    if (!hints.TryGetValue(kv.Key, out q) || q.Ref != h.Ref || q.Src != h.Src || Math.Abs(q.Due - h.Due) > 1e-3 || Math.Abs(q.Start - h.Start) > 1e-3
                        || Math.Abs(q.X - h.X) > 1e-3f || Math.Abs(q.Y - h.Y) > 1e-3f || q.Pinned != PinNow(h)) hs = false;
                }
                same &= hs && sum.Length == 8;
                ok &= same;
                pts.Add("T9 " + (same ? "TAK" : "NIE") + " (" + ex.Length + " znakow, pamiec " + bands.Count + "/" + _bands.Count + ", uzycia " + used.Count + "/" + _used.Count
                        + ", kajdany " + seen.Count + "/" + _seen.Count + ", wskazowki " + hints.Count + "/" + _hints.Count + " (zgodne " + YN(hs) + "), sum " + sum.Length + ", zlych " + (bad + badH) + ")");
            }
            catch (Exception e) { ok = false; pts.Add("T9 NIE (wyjatek " + e.GetType().Name + ")"); Stumble("TestT9", e); }
            pts.Add("sprzatanie " + (clean ? "TAK" : "NIE") + " (Mercy xp przed proba " + mercy0 + ", teraz " + MercyXp() + ")"
                    + (area || probes > 0 || keepHints.Count > 0 ? " (wskazowki probne zdjete " + probes + "; wskazowki zostaja: " + (keepHints.Count > 0 ? string.Join(", ", keepHints.ToArray()) : "-") + ")" : ""));
            Log.Info(P + "test - " + string.Join(" | ", pts.ToArray()) + " - wynik " + (ok ? "TAK" : "NIE") + ".");
        }
    }
}
