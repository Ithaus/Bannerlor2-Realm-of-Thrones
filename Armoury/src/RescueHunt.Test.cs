using System;
using System.IO;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>
    /// 222 - PROBA AUTOTESTU (tylko DLL autotestu CrashScribe - LogGate.Mode 1/2 - i plik Documents\Mount and Blade II Bannerlord\CrashScribe\armoury-odbicie.flag;
    /// plik tylko czytany; w zwyklej grze nic nie robi). Raz na sesje, od 2. do 10. doby sesji, gdy gracz nie jest w bitwie ani spotkaniu:
    ///  T0 wybor: wolna glowa rodu krolestwa w wojnie z graczem (nie jeniec sceny prologu, nie druzyna sceny, nie Inni), najblizsza graczowi; bez wojny - POMINIETY
    ///     (wojny proba nie wypowiada);
    ///  T1 TakePrisonerAction.Apply(druzyna gracza, glowa rodu) - lowy otwarte, plan wiesci = wzor liczony tu niezaleznie (jazda do twierdzy, kruk do siedziby; 222c - szybsza z: twierdza krolestwa jenca, najblizsza nieoblegana twierdza dowolnej frakcji);
    ///  T2 bramka kuriera zamknieta przed wiescia (plan 0 h - POMINIETY);
    ///  T3 zapis tam i z powrotem (Export -> Import -> Export) identyczny;
    ///  T4 (godzinny przeglad) "wiesc doszla" przy pierwszym przegladzie po planie (poprzedni przeglad przed planem - przeskok zegara gry nie myli);
    ///  T5 syntetyczna mysl wybranego lowcy (nowe PartyThinkParams z jedna opcja gry 0.4): rozkaz z migawki daje opcje lowow (poscig / szukanie / armia) o ocenie
    ///     &gt;= P x 0.5; brak lowcow - POMINIETY;
    ///  T6 loch: gracz z twierdza - jeniec przeniesiony do lochu, przeglad - cel lochu, potem z powrotem do druzyny; bez twierdzy - POMINIETY;
    ///  T7 EndCaptivityAction.ApplyByReleasedByChoice - "koniec ... wypuszczony", bramka kuriera otwarta;
    ///  T8 (recenzja 222, bez obiektow gry) slady wedlug wieku: pojmanie + 48 sladow co godzine przy twierdzy - lowca 130 jedn. od niej ma znany slad,
    ///     pojmanie trzymane do ColdHours, po 80 h zaden slad starszy niz ColdHours.
    /// Linie "Odbicie (222): test - Tn ... - wynik TAK|NIE|POMINIETY" (grupa odbicie222, ODB-6).
    /// </summary>
    internal static partial class RescueHunt
    {
        internal const string AutoFlagName = "armoury-odbicie.flag";
        private const int TestFirstDay = 2, TestLastDay = 10;
        private const double TestWaitHours = 300.0;   // dluzej niz najdluzszy kruk (288 h) + jazda - inaczej T4 NIE
        private static bool _testDone, _testFlag;
        private static int _testFlagDay = -1;
        private static Hero _tHero;
        private static double _tPlan = -1.0, _tStart = -1.0;
        private static string _tEnded;

        private static void TestReset() { _testDone = false; _testFlag = false; _testFlagDay = -1; _tHero = null; _tPlan = -1.0; _tStart = -1.0; _tEnded = null; }

        private static void TLine(string t, string what, string res) { Log.Info(P + "test - " + t + " " + what + " - wynik " + res + "."); }

        private static bool TestWanted(int today)
        {
            int m = LogGate.Mode;
            if (m != 1 && m != 2) return false;
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
            if (!On) why = "klucz Rescue Hunt NIE";
            else if (mp == null || Hero.MainHero == null) why = "brak partii gracza";
            else if (mp.MapEvent != null || mp.SiegeEvent != null || (PlayerEncounter.Current != null && PlayerEncounter.EncounterSettlement == null)) why = "bitwa albo spotkanie";
            else if (!Hero.MainHero.IsAlive || Hero.MainHero.IsPrisoner) why = "gracz w niewoli";
            if (why != null)
            {
                if (!On || _sessionDays >= TestLastDay) { _testDone = true; TLine("T0", "bez proby (" + why + ")", "POMINIETY"); }
                return;
            }
            _testDone = true;
            TestRun();
        }

        private static Hero TestPick()
        {
            var me = MobileParty.MainParty;
            var mf = Hero.MainHero.MapFaction;
            Vec2 pos = me.GetPosition2D;
            Hero best = null; float bd = float.MaxValue;
            foreach (var k in Kingdom.All)
            {
                if (k == null || k.IsEliminated || mf == null || !k.IsAtWarWith(mf)) continue;
                foreach (var c in k.Clans)
                {
                    var h = c != null && !c.IsEliminated ? c.Leader : null;
                    if (h == null || !h.IsAlive || h.IsPrisoner || !h.IsLord || c == Clan.PlayerClan) continue;
                    if (WhyNot(PartyBase.MainParty, h) != null) continue;
                    var hp = h.PartyBelongedTo;
                    if (hp != null && (hp.MapEvent != null || hp.SiegeEvent != null || Prologue.Scripted(hp) || hp.Army != null)) continue;
                    Vec2 at = hp != null ? hp.GetPosition2D : (h.CurrentSettlement != null ? h.CurrentSettlement.GetPosition2D : (h.HomeSettlement != null ? h.HomeSettlement.GetPosition2D : pos));
                    float d = at.Distance(pos);
                    if (d < bd) { bd = d; best = h; }
                }
            }
            return best;
        }

        private static void TestRun()
        {
            try { TestSights(); } catch (Exception e) { Stumble("TestSights", e); TLine("T8", "wyjatek " + e.GetType().Name, "NIE"); }
            try
            {
                var h = TestPick();
                if (h == null) { TLine("T0", "brak glowy rodu krolestwa w wojnie z graczem (wojny proba nie wypowiada)", "POMINIETY"); return; }
                TLine("T0", "wybrany " + Nm(h) + " (" + h.StringId + ") rodu " + Nm(h.Clan) + " krolestwa " + Nm(h.Clan.Kingdom), "TAK");
                // T1: plan liczony tu niezaleznie (te same wejscia co Open: miejsce = druzyna gracza, siedziba = SeatOf)
                Vec2 at = MobileParty.MainParty.GetPosition2D;
                double start = NowH;
                TakePrisonerAction.Apply(PartyBase.MainParty, h);
                Hunt t;
                if (!_hunts.TryGetValue(h, out t)) { TLine("T1", "lowy nie otwarte po TakePrisonerAction", "NIE"); return; }
                _tHero = h; _tStart = start;
                var k = h.Clan.Kingdom;
                Settlement best = null; float bd = float.MaxValue;
                foreach (var tw in k.Fiefs) { var st = tw != null ? tw.Settlement : null; if (st == null || !st.IsFortification) continue; float d = st.GetPosition2D.Distance(at); if (d < bd) { bd = d; best = st; } }
                Vec2 seat = t.Seat;
                Vec2 from = best != null ? best.GetPosition2D : at;
                double ride = best != null && bd > 40f ? bd / 2.5 : 0.0;
                float d2 = from.Distance(seat);
                double raven = d2 > 40f ? Math.Min(288.0, 3.0 + d2 / 6.25) : 0.0;
                // 222c: druga droga - najblizsza nieoblegana twierdza dowolnej frakcji; wybrana szybsza (remis - twierdza krolestwa)
                Settlement anyF = null; float ad = float.MaxValue;
                foreach (var st in Settlement.All) { if (st == null || !st.IsFortification || st.SiegeEvent != null) continue; float d = st.GetPosition2D.Distance(at); if (d < ad) { ad = d; anyF = st; } }
                if (anyF != null)
                {
                    double ride2 = ad > 40f ? ad / 2.5 : 0.0;
                    float d3 = anyF.GetPosition2D.Distance(seat);
                    double raven2 = d3 > 40f ? Math.Min(288.0, 3.0 + d3 / 6.25) : 0.0;
                    if (ride2 + raven2 < ride + raven) { ride = ride2; raven = raven2; }
                }
                double plan = start + ride + raven;
                _tPlan = t.SeatH;
                bool ok1 = Math.Abs(plan - t.SeatH) < 0.01;
                TLine("T1", "pojmanie " + Nm(h) + ": plan lowow " + F2(t.SeatH - start) + " h, wzor niezalezny " + F2(plan - start) + " h (jazda " + F2(ride) + ", kruk " + F2(raven) + ")", ok1 ? "TAK" : "NIE");
                // T2
                if (t.SeatH <= NowH) TLine("T2", "bramka kuriera - wiesc od razu (plan 0 h)", "POMINIETY");
                else
                {
                    bool held = HoldCourier(h);
                    TLine("T2", "bramka kuriera przed wiescia: " + (held ? "wstrzymany" : "przepuszczony"), held ? "TAK" : "NIE");
                }
                // T3
                string e1 = Export(); Import(e1); Resolve(); string e2 = Export();
                bool ok3 = e1 == e2 && _hunts.ContainsKey(h);
                TLine("T3", "zapis tam i z powrotem (" + e1.Length + " znakow, lowow " + _hunts.Count + ")", ok3 ? "TAK" : "NIE");
            }
            catch (Exception e) { Stumble("TestRun", e); TLine("T1", "wyjatek " + e.GetType().Name, "NIE"); }
        }

        /// <summary>Godzinny ciag proby: T4 (wiesc), T5 (mysl lowcy), T6 (loch), T7 (wypuszczenie). Watek kampanii.</summary>
        private static void TestHour()
        {
            var h = _tHero;
            if (h == null) return;
            Hunt t;
            double now = NowH;
            if (!_hunts.TryGetValue(h, out t))
            {
                _tHero = null;
                TLine("T4", "lowy " + Nm(h) + " skonczone przed wiescia (" + (_tEnded ?? "?") + ")", "NIE");
                return;
            }
            if (!t.Told)
            {
                if (now - _tStart > TestWaitHours) { _tHero = null; TLine("T4", "wiesc nie doszla w " + F0(TestWaitHours) + " h (plan " + F1(_tPlan - _tStart) + " h)", "NIE"); TestRelease(h); }
                return;
            }
            _tHero = null;
            // recenzja 222: przeglad godzinny trafia troche po pelnej godzinie - wiesc ma dojsc przy pierwszym przegladzie po planie, nie "w 1.0 h"
            // plan 0 h: przeglad godzinny tej samej chwili szedl przed dzienna proba (pojmanie po nim) - pierwszy przeglad po pojmaniu jest pierwszym po planie
            bool ok4 = now >= _tPlan - 1e-6 && (_hourPrevH < _tPlan || _hourPrevH <= _tStart + 1e-6);
            TLine("T4", "wiesc doszla po " + F1(now - _tStart) + " h, plan " + F1(_tPlan - _tStart) + " h, poprzedni przeglad " + F2(_hourPrevH - _tStart) + " h", ok4 ? "TAK" : "NIE");
            TestThink(t);
            TestDungeon(t);
            TestRelease(h);
        }

        /// <summary>T8: slady wedlug wieku (AddSight/KnownAt na sztucznych lowach - bez obiektow gry). Przed recenzja (8 sladow) dalecy lowcy nie mieli zadnego.</summary>
        private static void TestSights()
        {
            var t = new Hunt();
            var fort = new Vec2(0f, 0f);
            const double h0 = 1000.0;
            t.Sights.Add(new Sight { H = h0, X = 0f, Y = 0f, Kind = SCapture, FH = h0, FX = fort.X, FY = fort.Y });
            for (int i = 1; i <= 48; i++) AddSight(t, new Sight { H = h0 + i, X = 1f, Y = 1f, Kind = SSettlement, FH = h0 + i, FX = fort.X, FY = fort.Y });
            var q = new Vec2(130f, 0f);
            double now = h0 + 48.0;
            int known = 0;
            foreach (var s in t.Sights) if (now - s.H <= ColdHours && KnownAt(s, null, q) <= now) known++;
            bool capKept = t.Sights.Count > 0 && t.Sights[0].Kind == SCapture;
            for (int i = 49; i <= 80; i++) AddSight(t, new Sight { H = h0 + i, X = 1f, Y = 1f, Kind = SSettlement, FH = h0 + i, FX = fort.X, FY = fort.Y });
            double oldest = t.Sights.Count > 0 ? h0 + 80.0 - t.Sights[0].H : -1.0;
            bool ok = known > 0 && capKept && oldest <= ColdHours && t.Sights.Count <= (int)ColdHours + 1;
            TLine("T8", "slady wedlug wieku: lowca 130 jedn. od twierdzy po 48 h zna " + known + " sladow (kruk " + F1(KnownAt(t.Sights[t.Sights.Count - 1], null, q) - (h0 + 80.0)) + " h), pojmanie po 48 h "
                  + (capKept ? "jest" : "BRAK") + ", po 80 h sladow " + t.Sights.Count + " najstarszy " + F0(oldest) + " h", ok ? "TAK" : "NIE");
        }

        private static void TestThink(Hunt t)
        {
            try
            {
                var snap = System.Threading.Volatile.Read(ref _snap);
                MobileParty mp = null; Order o = null;
                foreach (var kv in snap) if (kv.Value.H == t && kv.Value.Kind != OSiege) { mp = kv.Key; o = kv.Value; break; }
                if (mp == null) { TLine("T5", "brak wybranego lowcy (tryb " + t.Mode + ")", "POMINIETY"); return; }
                var pt = new PartyThinkParams(mp);
                Settlement town = null; float bd = float.MaxValue;
                foreach (var s in Settlement.All) { if (s == null || !s.IsTown) continue; float d = s.GetPosition2D.Distance(mp.GetPosition2D); if (d < bd) { bd = d; town = s; } }
                if (town != null) { var g = (new AIBehaviorData(town, AiBehavior.GoToSettlement, MobileParty.NavigationType.Default, false, false, false), 0.4f); pt.AddBehaviorScore(in g); }
                if (o.Kind == OArmy)
                {
                    TLine("T5", "rozkaz armii u " + Nm(mp) + " - opcja zalezy od druzyn do wezwania gry (nie sprawdzana syntetycznie)", "POMINIETY");
                    return;
                }
                ThinkPostfix(mp, pt);
                float want = o.P * 0.5f, got = -1f; string kind = "-";
                foreach (var e in pt.AIBehaviorScores)
                {
                    var d = e.Item1;
                    if ((o.Kind == OChase && d.AiBehavior == AiBehavior.GoAroundParty && ReferenceEquals(d.Party, o.Target))
                        || (o.Kind == OPatrol && d.AiBehavior == AiBehavior.PatrolAroundPoint && d.Party == null))
                    { got = e.Item2; kind = d.AiBehavior.ToString(); }
                }
                bool atSea = mp.IsCurrentlyAtSea;
                bool ok = got >= want - 1e-4 || (o.Kind == OPatrol && atSea);
                TLine("T5", "mysl " + Nm(mp) + " rozkaz " + (o.Kind == OChase ? "poscig" : "szukanie") + ": opcja " + kind + " " + F2(got) + " (oczekiwane >= " + F2(want) + ")" + (atSea ? " (na morzu - bez opcji)" : ""), ok ? "TAK" : "NIE");
            }
            catch (Exception e) { Stumble("TestThink", e); TLine("T5", "wyjatek " + e.GetType().Name, "NIE"); }
        }

        private static void TestDungeon(Hunt t)
        {
            try
            {
                var h = t.Captive;
                Settlement x = null;
                foreach (var tw in Clan.PlayerClan.Fiefs) { var st = tw != null ? tw.Settlement : null; if (st != null && st.IsFortification && st.Party != null) { x = st; break; } }
                var src = h.PartyBelongedToAsPrisoner;
                if (x == null || src == null || !src.IsMobile) { TLine("T6", "loch - gracz bez twierdzy", "POMINIETY"); return; }
                src.PrisonRoster.AddToCounts(h.CharacterObject, -1);
                x.Party.PrisonRoster.AddToCounts(h.CharacterObject, 1);
                _lastHour = -1.0; Hourly();
                bool ok = _hunts.ContainsKey(h) && t.DungeonId == x.StringId;
                x.Party.PrisonRoster.AddToCounts(h.CharacterObject, -1);
                src.PrisonRoster.AddToCounts(h.CharacterObject, 1);
                _lastHour = -1.0; Hourly();
                bool back = _hunts.ContainsKey(h) && t.DungeonId.Length == 0;
                TLine("T6", "loch " + Nm(x) + ": cel lochu " + (ok ? "tak" : "nie") + ", powrot do druzyny " + (back ? "tak" : "nie"), ok && back ? "TAK" : "NIE");
            }
            catch (Exception e) { Stumble("TestDungeon", e); TLine("T6", "wyjatek " + e.GetType().Name, "NIE"); }
        }

        private static void TestRelease(Hero h)
        {
            try
            {
                if (h == null || !h.IsPrisoner) { TLine("T7", "jeniec juz wolny", "POMINIETY"); return; }
                _tEnded = null;
                EndCaptivityAction.ApplyByReleasedByChoice(h);
                bool ended = !_hunts.ContainsKey(h) && _tEnded == "wypuszczony";
                bool open = !HoldCourier(h);
                TLine("T7", "wypuszczenie: koniec " + (_tEnded ?? "-") + ", bramka kuriera " + (open ? "otwarta" : "zamknieta"), ended && open ? "TAK" : "NIE");
            }
            catch (Exception e) { Stumble("TestRelease", e); TLine("T7", "wyjatek " + e.GetType().Name, "NIE"); }
        }

        private static void TestEnded(Hero h, string why)
        {
            if (h != null && (h == _tHero || _tHero == null)) _tEnded = why;
        }
    }
}
