using System;
using System.IO;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Election;

namespace Armoury
{
    /// <summary>
    /// 330 - PROBA AUTOTESTU (wzor CanonPeaceLockTest: tylko DLL autotestu CrashScribe - LogGate.Mode 1/2 - i plik Documents\Mount and Blade II Bannerlord\CrashScribe\
    /// armoury-wojna330.flag; w zwyklej grze nic nie robi). Na kopii zapisu autotestu, nigdy na zapisach Jeffa. Od 2. godziny sesji poza bitwa:
    ///  TA tabela prawdy Locked330 (F3 blokuje zawsze, F2 / wesele w toku / gracz / klucz NIE - wynik 264) i Gate (faza 2, wojna &lt; 84, karencja, prog kapitulacji, gracz);
    ///  TB liczniki i progi (Step, K1Cond, DueKind - korona bez K3 wg poprawki G, WhoSurrenders - nizszy stosunek);
    ///  TC przedluzenie zimowe (WinterExtra: lato 0, zima 63 maks., zima krotsza - do wiosny);
    ///  TD zapis: Export -> Import -> Export identyczne (T11 bez zapisu gry); stan przywrocony;
    ///  T1 (bieg) para w wojnie, gracz nie rzadzi: StartTruce("test") - pokoj, ROT nie wymusza (H4), AddDecision(DeclareWarDecision) zablokowane (H5, T7),
    ///     potem koniec rozejmu ustawiony na dzis; T2 nastepne doby - wojna wraca, danina StanceLink 0; T6 MakePeaceAction.Apply z zewnatrz - rozejm H6, koniec na dzis,
    ///     wojna wraca. Linie "Wojna do kapitulacji (330): test - Tn ... - wynik TAK|NIE|POMINIETY."
    /// </summary>
    internal static class WarTruceTest
    {
        internal const string AutoFlagName = "armoury-wojna330.flag";
        private const int WaitHours = 72;
        private static bool _done, _flag, _live;
        private static int _flagDay = -1, _hours, _stage;
        private static long _liveHour;
        internal static bool Armed;
        internal static int Hits;

        internal static void Reset() { _done = false; _flag = false; _live = false; _flagDay = -1; _hours = 0; _stage = 0; Armed = false; Hits = 0; }

        private static string P { get { return WarTruce.Tag + "test - "; } }

        private static string FlagPath()
        {
            var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            return Path.Combine(Path.Combine(Path.Combine(docs, "Mount and Blade II Bannerlord"), "CrashScribe"), AutoFlagName);
        }

        private static bool Wanted()
        {
            int m = LogGate.Mode;
            if (m != 1 && m != 2) return false;
            int today = (int)CampaignTime.Now.ToDays;
            if (_flagDay != today)
            {
                _flagDay = today;
                try { _flag = File.Exists(FlagPath()); } catch { _flag = false; }
            }
            return _flag;
        }

        internal static string StartText()
        {
            int m = LogGate.Mode;
            if (m != 1 && m != 2) return "";
            _flagDay = -1;
            return " | proba autotestu: flaga " + AutoFlagName + " " + (Wanted() ? "TAK" : "NIE");
        }

        private static string T(bool b) { return b ? "TAK" : "NIE"; }

        private static void Line(string text, bool? ok) { Log.Info(P + text + " - wynik " + (ok == null ? "POMINIETY" : ok.Value ? "TAK" : "NIE") + "."); }

        internal static void Hourly()
        {
            if (_done && !_live) return;
            _hours++;
            if (!_done)
            {
                if (_hours < 2 || !Wanted()) return;
                var mp = TaleWorlds.CampaignSystem.Party.MobileParty.MainParty;
                if (mp != null && (mp.MapEvent != null || mp.SiegeEvent != null)) return;
                _done = true;
                try { Run(); } catch (Exception e) { WarTruce.Stumble("Test.Run", e); Line("T0 bieg przerwany (" + e.GetType().Name + ")", false); }
                return;
            }
            try { Live(); } catch (Exception e) { _live = false; WarTruce.Stumble("Test.Live", e); Line("T2/T6 bieg przerwany (" + e.GetType().Name + ")", false); }
        }

        // ---------------------------------------------------------------- tabele prawdy

        private static bool L330(bool on, bool key, bool over, bool p2, int rw, bool pc, bool el)
        { string w = "-"; int ph = 0; return CanonPeaceLock.Locked330(on, key, over, p2, rw, pc, el, ref w, ref ph); }

        private static int G(int ph, bool war, bool player, bool due, int wd, int today, int grace, bool exh)
        { string w; return WarTruce.Gate(ph, war, player, due, wd, 84, today, grace, exh, out w); }

        private static bool TruthA(out string text)
        {
            bool f3 = L330(false, true, true, false, 2, false, false), f3on = L330(true, true, true, false, 2, false, false), f2 = L330(false, true, true, true, 2, false, false);
            bool rw1 = L330(false, true, true, false, 1, false, false), pl = L330(false, true, true, false, 2, true, false), off = L330(false, false, true, false, 2, false, false);
            bool f0 = L330(true, true, false, false, 0, false, false), el = L330(false, true, true, false, 2, false, true);
            int gT = G(3, true, false, false, 100, 50, 40, true), gF0 = G(0, true, false, false, 100, 50, 40, true), gF2 = G(2, true, false, false, 100, 50, 40, true);
            int gShort = G(3, true, false, false, 30, 50, 40, true), gGrace = G(3, true, false, false, 100, 30, 40, true), gDue = G(3, true, false, true, 10, 30, 40, false);
            int gPl = G(0, true, true, false, 100, 50, 40, true), gNoWar = G(0, false, false, false, 100, 50, 40, true), gExh = G(0, true, false, false, 100, 50, 40, false);
            text = "Locked330 F3 " + T(f3) + ", F3 ROT wymusza " + T(f3on) + ", F2 " + T(f2) + ", wesele w toku " + T(rw1) + ", gracz " + T(pl) + ", klucz NIE " + T(off) + ", F0 " + T(f0) + ", zniszczone " + T(el)
                   + " | Gate F3 " + gT + ", F0 " + gF0 + ", F2 " + gF2 + ", wojna < 84 " + gShort + ", karencja " + gGrace + ", prog kapitulacji " + gDue + ", gracz " + gPl + ", bez wojny " + gNoWar + ", zmeczenie nisko " + gExh;
            return f3 && f3on && !f2 && !rw1 && !pl && !off && f0 && !el
                   && gT == WarTruce.GateTruce && gF0 == WarTruce.GateTruce && gF2 == WarTruce.GateNone && gShort == WarTruce.GateNone && gGrace == WarTruce.GateNone
                   && gDue == WarTruce.GateSurrender && gPl == WarTruce.GateNone && gNoWar == WarTruce.GateNone && gExh == WarTruce.GateNone;
        }

        private static bool TruthB(out string text)
        {
            int s = 0; for (int i = 0; i < 42; i++) s = WarTruce.Step(s, true);
            int r = WarTruce.Step(s, false);
            bool k1a = WarTruce.K1Cond(false, 3, 30, 3, 0.5, 0.67), k1b = WarTruce.K1Cond(false, 10, 30, 3, 0.5, 0.67), k1c = WarTruce.K1Cond(true, 1, 30, 3, 0.5, 0.67);
            bool k1d = WarTruce.K1Cond(false, 3, 6, 3, 0.7, 0.67), k1e = WarTruce.K1Cond(false, 9, 27, 3, 0.5, 0.67);
            string dN3 = WarTruce.DueKind(0, 0, 30, true, 42, 21, 30), dC3 = WarTruce.DueKind(0, 0, 30, false, 42, 21, 30), dK1 = WarTruce.DueKind(42, 0, 0, false, 42, 21, 30);
            string dK2 = WarTruce.DueKind(0, 21, 0, true, 42, 21, 30), dNo = WarTruce.DueKind(41, 20, 29, true, 42, 21, 30);
            int both = WarTruce.WhoSurrenders(true, true, 0.4, 2.5), bothC = WarTruce.WhoSurrenders(true, true, 0.9, 0.8), onlyC = WarTruce.WhoSurrenders(false, true, 2, 0.5);
            // recenzja 330: wznowienie - rozejm: odbiorca daniny (brak - korona); zatrzask Z: zwyciezca kapitulacji
            bool rTn = WarTruce.ResumeByNorth(true, WarTruce.NorthId, ""), rTc = WarTruce.ResumeByNorth(true, "", "KAPITULACJA korony (K1 42/42)");
            bool rZc = WarTruce.ResumeByNorth(false, "", "KAPITULACJA korony (K2 21/21)"), rZn = WarTruce.ResumeByNorth(false, "", "KAPITULACJA Polnocy (K3 30/30)");
            text = "Step 42 dob " + s + ", po peknieciu " + r + " | K1 3 tw./30 " + T(k1a) + ", 10 tw. " + T(k1b) + ", z siedziba " + T(k1c) + ", sila 0.7 " + T(k1d) + ", 9 tw./27 " + T(k1e)
                   + " | Polnoc K3 " + (dN3 ?? "-") + ", korona K3 " + (dC3 ?? "-") + ", K1 " + (dK1 ?? "-") + ", K2 " + (dK2 ?? "-") + ", ponizej progow " + (dNo ?? "-")
                   + " | obie strony 0.4/2.5 -> " + both + ", 0.9/0.8 -> " + bothC + ", tylko korona -> " + onlyC
                   + " | wznowienie Polnoc: rozejm odbiorca Polnoc " + T(rTn) + ", rozejm bez daniny " + T(rTc) + ", Z po kapitulacji korony " + T(rZc) + ", Z po kapitulacji Polnocy " + T(rZn);
            return s == 42 && r == 0 && k1a && !k1b && !k1c && !k1d && k1e && dN3 != null && dC3 == null && dK1 != null && dK2 != null && dNo == null && both == 1 && bothC == 2 && onlyC == 2
                   && rTn && !rTc && rZc && !rZn;
        }

        private static bool TruthC(out string text)
        {
            int summer = WarTruce.WinterExtra(false, 300, 21), longW = WarTruce.WinterExtra(true, 300, 21), shortW = WarTruce.WinterExtra(true, 5, 21), unk = WarTruce.WinterExtra(true, -1, 21), off = WarTruce.WinterExtra(true, 300, 0);
            text = "lato +" + summer + ", zima dluga +" + longW + " (razem " + (42 + longW) + "), zima 5 dob +" + shortW + ", zima nie wiadomo +" + unk + ", limit 0 +" + off;
            return summer == 0 && longW == 21 && 42 + longW <= 63 && shortW == 5 && unk == 21 && off == 0;
        }

        private static void Run()
        {
            string t;
            bool a = TruthA(out t); Line("TA tabela prawdy Locked330 i Gate: " + t, a);
            bool b = TruthB(out t); Line("TB liczniki i progi kapitulacji (korona bez K3 - poprawka G): " + t, b);
            bool c = TruthC(out t); Line("TC przedluzenie zimowe: " + t, c);
            string e1 = WarTruce.Export();
            WarTruce.Import(e1);
            string e2 = WarTruce.Export();
            Line("TD zapis Export/Import (" + e1.Length + " znakow)", e1 == e2);
            // bieg
            var n = WarTruce.North; var k = WarTruce.Crown;
            string skip = null;
            if (!WarTruce.On) skip = "klucz NIE albo KONIEC 330";
            else if (n == null || k == null || n.IsEliminated || k.IsEliminated) skip = "brak krolestwa";
            else if (!n.IsAtWarWith(k)) skip = "Polnoc-korona nie w wojnie";
            else if (Prologue.RulesRealm(n) || Prologue.RulesRealm(k)) skip = "gracz rzadzi strona";
            else if (WarTruce.TruceActive || WarTruce.ZLatched) skip = "rozejm albo zatrzask juz trwa";
            if (skip != null) { Line("T1 rozejm Polnoc-korona (" + skip + ")", null); return; }
            bool ok = WarTruce.StartTruce(n, k, "test", WarTruce.TruceDays);
            bool peace = !n.IsAtWarWith(k);
            bool rot = Prologue.RotWarForced(n, k);
            Armed = true; Hits = 0;
            int pending = 0;
            try
            {
                if (k.RulingClan != null) k.AddDecision(new DeclareWarDecision(k.RulingClan, n), false);
                foreach (var d in k.UnresolvedDecisions) { var dw = d as DeclareWarDecision; if (dw != null && dw.FactionToDeclareWarOn == n) pending++; }
            }
            finally { Armed = false; }
            bool blocks = WarTruce.BlocksWar(k, n);
            Line("T1 rozejm: pokoj " + T(peace) + ", ROT wymusza " + T(rot) + " (H4 - ma byc NIE) | T7 AddDecision wojna: czekajace " + pending + ", blokad H5 " + Hits + ", ocena BK blokuje " + T(blocks),
                 ok && peace && !rot && pending == 0 && Hits >= 1 && blocks);
            if (!ok) return;
            WarTruce.TestEndTruceToday();
            _stage = 1; _live = true;
            _liveHour = (long)Math.Floor(CampaignTime.Now.ToHours);
        }

        private static void Live()
        {
            if (!_live) return;
            var n = WarTruce.North; var k = WarTruce.Crown;
            long el = (long)Math.Floor(CampaignTime.Now.ToHours) - _liveHour;
            if (n == null || k == null) { _live = false; return; }
            if (_stage == 1)
            {
                if (n.IsAtWarWith(k))
                {
                    var link = n.GetStanceWith(k);
                    int left = link != null ? link.GetRemainingTributePaymentCount() : -1;
                    bool truce = WarTruce.TruceActive;
                    Line("T2 koniec rozejmu po " + el + " h: wojna TAK, danina rat " + left + ", rozejm w stanie " + T(truce), left == 0 && !truce);
                    // T6: pokoj z zewnatrz -> rozejm H6
                    MakePeaceAction.Apply(k, n);
                    bool h6 = WarTruce.TruceActive && !n.IsAtWarWith(k);
                    Line("T6 pokoj z zewnatrz (MakePeaceAction.Apply): rozejm H6 " + T(h6) + ", koniec d." + WarTruce.TruceEnd, h6);
                    if (h6) { WarTruce.TestEndTruceToday(); _stage = 2; _liveHour = (long)Math.Floor(CampaignTime.Now.ToHours); }
                    else _live = false;
                }
                else if (el > WaitHours) { Line("T2 koniec rozejmu: po " + el + " h wojna nie wrocila", false); _live = false; }
                return;
            }
            if (_stage == 2)
            {
                if (n.IsAtWarWith(k)) { Line("T6b po rozejmie H6 po " + el + " h wojna wraca", true); _live = false; }
                else if (el > WaitHours) { Line("T6b po rozejmie H6: po " + el + " h wojna nie wrocila", false); _live = false; }
            }
        }
    }
}
