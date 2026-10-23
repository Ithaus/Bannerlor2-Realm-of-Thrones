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
    /// Linia "Przesluchanie (m109): test - ... - wynik TAK|NIE." (grupa m109przes PRZES-7).
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
            // ---------------- T0: kryjowki, kultura, rodzaje jencow
            Settlement h1 = null, h2 = null; Clan clan = null; CultureObject c = null; CharacterObject ta = null, tb = null, tl = null;
            float best = float.MaxValue;
            Vec2 at = mp.Position.ToVec2();
            foreach (var h in Hideout.All)
            {
                var st = h != null ? h.Settlement : null;
                if (st == null || st.Culture == null || !h.IsInfested || h.IsSpotted) continue;
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
                if (st == null || st == h1 || st.Culture != c || !h.IsInfested || h.IsSpotted) continue;
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
                // ---------------- T1: rozmowa A rzutem 0 - H2 ze "skad banda" i flaga
                {
                    bool flagWant = s != null && s.HideoutFlagEnabled && s.HideoutFlagDays > 0;
                    var res = Act(Find(Clean(ta.StringId)), false, 0f);
                    bool t = !res.Refused && res.Talked && res.Lair == h2 && res.Source == "banda" && h2.Hideout.IsSpotted && h2.IsVisible && (!flagWant || res.Flag);
                    ok &= t;
                    pts.Add("T1 " + (t ? "TAK" : "NIE") + " (odmowa " + (res.Refused ? res.RefuseWhy : "-") + ", szansa " + F4(res.Chance) + ", kryjowka " + (res.Lair != null ? res.Lair.StringId : "-")
                            + ", skad " + (res.Source ?? "-") + ", wypatrzona " + (h2.Hideout.IsSpotted ? "TAK" : "NIE") + ", flaga " + (res.Flag ? "TAK" : "NIE") + (flagWant ? "" : " (flagi wylaczone)") + ")");
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
                    if (h2 == h1) skip = "jedna kryjowka kultury - H1 juz ujawniona";
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
                        var res = Act(g, true, 0f, 0f);
                        bool gone = Find(Clean("h:" + hero.StringId)) == null;
                        bool t = g != null && !res.Refused && res.Talked && res.Source == "bohater" && res.Lair == h1 && ((res.Died && !hero.IsAlive && gone) || res.Blocked);
                        ok &= t;
                        pts.Add("T7 " + (t ? "TAK" : "NIE") + " (wzor " + tpl.StringId + ", grupa " + (g != null ? "TAK" : "NIE") + ", odmowa " + (res.Refused ? res.RefuseWhy : "-") + ", szansa " + F4(res.Chance)
                                + ", skad " + (res.Source ?? "-") + ", smierc " + (res.Died ? "TAK" : res.Blocked ? "zablokowana" : "NIE") + ", zyje " + (hero.IsAlive ? "TAK" : "NIE")
                                + ", w lochu " + (gone ? "NIE" : "TAK") + ")");
                    }
                }
                // ---------------- T4/T5: B zwyklymi rzutami bez wpisu pamieci (slot rodzaju bandy wolny - jak nowa doba)
                _bands.Clear();
                _used.Remove(kA);
                if (tb == null) pts.Add("T4 POMINIETY (brak drugiego rodzaju)"); else
                {
                    var res = Act(Find(Clean(tb.StringId)), false);
                    bool t = res.Refused ? res.RefuseWhy != "limit" && res.RefuseWhy != "brak grupy"
                                         : res.Talked == (res.Roll < res.Chance) && (!res.Talked || (res.Source == "kultura" && res.Lair != null && res.Lair.Hideout.IsSpotted));
                    ok &= t;
                    pts.Add("T4 " + (t ? "TAK" : "NIE") + " (" + (res.Refused ? "odmowa: " + res.RefuseWhy : "szansa " + F4(res.Chance) + ", rzut " + F4(res.Roll) + " -> " + (res.Talked ? "MOWI, skad " + res.Source : "MILCZY")) + ")");
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
                string ex = Write(_bands, _used, _sum, _seen);
                var bands = new List<Band>(); var used = new Dictionary<string, int[]>(StringComparer.Ordinal); var sum = new int[_sum.Length]; int bad;
                var seen = new Dictionary<string, int>(StringComparer.Ordinal);
                bool parsed = Parse(ex, bands, used, sum, seen, out bad);
                bool same = parsed && bad == 0 && bands.Count == _bands.Count && used.Count == _used.Count && seen.Count == _seen.Count;
                for (int i = 0; same && i < sum.Length; i++) same = sum[i] == _sum[i];
                foreach (var kv in _seen) { int d; if (!seen.TryGetValue(kv.Key, out d) || d != kv.Value) same = false; }
                ok &= same;
                pts.Add("T9 " + (same ? "TAK" : "NIE") + " (" + ex.Length + " znakow, pamiec " + bands.Count + "/" + _bands.Count + ", uzycia " + used.Count + "/" + _used.Count
                        + ", kajdany " + seen.Count + "/" + _seen.Count + ", zlych " + bad + ")");
            }
            catch (Exception e) { ok = false; pts.Add("T9 NIE (wyjatek " + e.GetType().Name + ")"); Stumble("TestT9", e); }
            pts.Add("sprzatanie " + (clean ? "TAK" : "NIE") + " (Mercy xp przed proba " + mercy0 + ", teraz " + MercyXp() + ")");
            Log.Info(P + "test - " + string.Join(" | ", pts.ToArray()) + " - wynik " + (ok ? "TAK" : "NIE") + ".");
        }
    }
}
