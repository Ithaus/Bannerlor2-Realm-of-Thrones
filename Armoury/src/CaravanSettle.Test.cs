using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// 259 - PROBA AUTOTESTU (tylko DLL autotestu CrashScribe - LogGate.Mode 1/2 - i plik Documents\Mount and Blade II Bannerlord\CrashScribe\armoury-karawany.flag;
    /// plik tylko czytany; w zwyklej grze nic nie robi). Raz na sesje, od 2. do 10. doby sesji. Cel: rozliczana karawana gracza (zapis Jeffa ma jego karawane),
    /// bez niej - karawana lorda (testy gracza wtedy POMINIETY):
    ///  T1 regula liczona tu (Compute - tylko odczyt) == linia "caravan income" modelu rodu (CalculateClanIncome(rod, true, false, true)) i kiesa bez zmian;
    ///  T2 nastepne rozliczenie tego rodu: spadek kiesy == wyplata == wzor na wejsciach zapamietanych w prefiksie, kontrola doby 0;
    ///  T3 nasz prefiks pierwszy na AddIncomeFromParty; BK AddRealisticIncome (odbicie, siedziba wlasciciela) nie zmienia kiesy;
    ///  T4 zapis tam i z powrotem (Export -> Import -> Export) identyczny;
    ///  T5 straznik m105 (KtGuard) dzis null - nasza latka na liscie znanych;
    ///  T6 AddIncomeFromParty gry (odbicie, applyWithdrawals = false) zwraca nasza wartosc (przy kiesie ponad 10 000 - nie 10% gry), kiesa bez zmian.
    /// Linie "Karawany rodow (259): test - Tn ... - wynik TAK|NIE|POMINIETY." (grupa karawany259, K259-7).
    /// </summary>
    internal static partial class CaravanSettle
    {
        internal const string AutoFlagName = "armoury-karawany.flag";
        private const string PT = "Karawany rodow (259): test - ";
        private const int TestFirstDay = 2, TestLastDay = 10;
        private static bool _testDone, _testFlag, _t2Open;
        private static int _testFlagDay = -1, _sessDays, _t2Day = -1;
        private static MobileParty _tTarget;
        private static bool _tHave; private static Calc _tCalc; private static int _tBefore, _tAfter, _tMoved;

        private static void TestReset()
        {
            _testDone = false; _testFlag = false; _t2Open = false; _testFlagDay = -1; _sessDays = 0; _t2Day = -1;
            _tTarget = null; _tHave = false; _tCalc = new Calc(); _tBefore = _tAfter = _tMoved = 0;
        }

        private static void TLine(string t, string what, string res) { Log.Info(PT + t + " " + what + " - wynik " + res + "."); }

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

        /// <summary>IncomePrefix (z wyplata): wejscia reguly celu T2.</summary>
        private static void TestCapture(MobileParty mp, Calc c, int moved)
        {
            if (!_t2Open || !ReferenceEquals(mp, _tTarget)) return;
            _tHave = true; _tCalc = c; _tBefore = c.Purse; _tAfter = mp.PartyTradeGold; _tMoved = moved;
        }

        private static void TestStep(int day)
        {
            _sessDays++;
            if (_t2Open && _tHave) T2Check();
            else if (_t2Open && _t2Day >= 0 && day - _t2Day >= 3) { _t2Open = false; TLine("T2", "brak rozliczenia celu przez 3 doby", "NIE"); }
            if (_testDone || !TestWanted(day) || _sessDays < TestFirstDay) return;
            if (!_wIncome || !On)
            {
                if (_sessDays >= TestLastDay || !On) { _testDone = true; TLine("T0", "bez proby (" + (!On ? "klucz Caravan Clan Settlement NIE" : "brak latki AddIncomeFromParty") + ")", "POMINIETY"); }
                return;
            }
            _testDone = true;
            TestRun(day);
        }

        private static MobileParty PickTarget(out bool player)
        {
            player = false;
            MobileParty best = null;
            foreach (var mp in MobileParty.AllCaravanParties)
            {
                if (mp == null || !Settled(mp)) continue;
                if (mp.ActualClan == Clan.PlayerClan) { player = true; return mp; }
                if (best == null && mp.MapEvent == null && !mp.IsDisbanding) best = mp;
            }
            return best;
        }

        private static object DefaultFin()
        {
            object m = Campaign.Current.Models.ClanFinanceModel;
            for (int i = 0; i < 12 && m != null; i++)
            {
                if (m is DefaultClanFinanceModel) return m;
                object next = null;
                var t = m.GetType();
                try { var bp = AccessTools.Property(t, "BaseModel"); next = bp != null ? bp.GetValue(m, null) : null; } catch { next = null; }
                if (next == null || ReferenceEquals(next, m))
                {
                    next = null;
                    for (var tt = t; tt != null && next == null; tt = tt.BaseType)
                        foreach (var f in tt.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                        {
                            if (!typeof(ClanFinanceModel).IsAssignableFrom(f.FieldType)) continue;
                            var v = f.GetValue(m);
                            if (v != null && !ReferenceEquals(v, m)) { next = v; break; }
                        }
                }
                m = next;
            }
            return null;
        }

        private static void TestRun(int day)
        {
            bool player;
            var mp = PickTarget(out player);
            if (mp == null) { TLine("T0", "brak rozliczanej karawany", "POMINIETY"); return; }
            var owner = mp.Owner; var clan = owner.Clan;
            TLine("T0", "cel: '" + NameOf(mp) + "' (" + (player ? "karawana gracza" : "karawana lorda - testy gracza POMINIETY") + ", wlasciciel " + owner.Name + ", rod " + clan.Name + ")", "TAK");
            // T1
            try
            {
                var c = Compute(mp, owner);
                int g0 = mp.PartyTradeGold;
                var en = Campaign.Current.Models.ClanFinanceModel.CalculateClanIncome(clan, true, false, true);
                bool same = mp.PartyTradeGold == g0;
                bool found = c.Payout == 0 || mp.MapEvent != null || mp.IsDisbanding;
                string line = "-";
                foreach (var l in en.GetLines())
                    if (Math.Abs(Math.Round(l.number) - c.Payout) < 0.5 && c.Payout > 0) { found = true; line = l.name; break; }
                int indep = Formula(c.Worth, c.Capital, c.Purse, c.Floor);
                TLine("T1", "regula: kiesa " + c.Purse + ", towar " + c.Goods + ", wartosc " + c.Worth + ", kapital " + c.Capital + ", podloga " + c.Floor + " -> wyplata " + c.Payout
                      + " (wzor niezaleznie " + indep + "); linia modelu rodu " + (found ? "TAK (" + line + ")" : "NIE") + ", kiesa po modelu " + (same ? "bez zmian" : g0 + " -> " + mp.PartyTradeGold),
                      found && same && indep == c.Payout ? "TAK" : "NIE");
            }
            catch (Exception e) { TLine("T1", "wyjatek " + e.GetType().Name + ": " + e.Message, "NIE"); }
            // T2 - przy nastepnym rozliczeniu rodu celu
            _tTarget = mp; _t2Open = true; _t2Day = day; _tHave = false;
            // T3
            try
            {
                string note;
                bool first = IncomeFirst(out note);
                string bk = "BK AddRealisticIncome - brak typu";
                bool bkOk = true;
                var bpT = AccessTools.TypeByName("BannerKings.Behaviours.BKPartyBehavior");
                var mReal = bpT != null ? AccessTools.Method(bpT, "AddRealisticIncome", new[] { typeof(MobileParty), typeof(Settlement) }) : null;
                var gen = AccessTools.Method(typeof(Campaign), "GetCampaignBehavior");
                object beh = null;
                if (bpT != null && gen != null && gen.IsGenericMethodDefinition) beh = gen.MakeGenericMethod(bpT).Invoke(Campaign.Current, null);
                var target = owner.HomeSettlement;
                if (mReal != null && beh != null && target != null)
                {
                    int g0 = mp.PartyTradeGold, h0 = owner.Gold;
                    mReal.Invoke(beh, new object[] { mp, target });
                    bkOk = mp.PartyTradeGold == g0 && owner.Gold == h0;
                    bk = "BK AddRealisticIncome w " + target.Name + ": kiesa " + g0 + " -> " + mp.PartyTradeGold + ", wlasciciel " + h0 + " -> " + owner.Gold;
                }
                TLine("T3", "prefiks pierwszy " + Tn(first) + " (" + note + "); " + bk, first && bkOk ? "TAK" : "NIE");
            }
            catch (Exception e) { TLine("T3", "wyjatek " + e.GetType().Name + ": " + (e.InnerException != null ? e.InnerException.Message : e.Message), "NIE"); }
            // T4
            try
            {
                string a = Export();
                Import(a);
                string b = Export();
                TLine("T4", "zapis tam i z powrotem: rekordow karawan " + _cars.Count + ", warsztatow " + _wss.Count + ", dlugosc " + a.Length + " znakow, identyczny " + Tn(a == b) + ", bledne " + _impBad,
                      a == b && _impBad == 0 ? "TAK" : "NIE");
            }
            catch (Exception e) { TLine("T4", "wyjatek " + e.GetType().Name + ": " + e.Message, "NIE"); }
            // T5
            try
            {
                string g = KingdomTreasury.KtGuardProbe();
                TLine("T5", "straznik m105: " + (g ?? "null (pominiecie dozwolone)") + ", latka na liscie znanych " + Tn(KingdomTreasury.KtKnows("Armoury:CaravanSettle.IncomePrefix")), g == null ? "TAK" : "NIE");
            }
            catch (Exception e) { TLine("T5", "wyjatek " + e.GetType().Name + ": " + e.Message, "NIE"); }
            // T6
            try
            {
                var fin = DefaultFin();
                if (fin == null || _mIncome == null) TLine("T6", "brak DefaultClanFinanceModel w lancuchu albo metody", "POMINIETY");
                else
                {
                    MobileParty rich = null;
                    foreach (var p in MobileParty.AllCaravanParties) if (p != null && Settled(p) && p.PartyTradeGold > BkKeep && p.MapEvent == null && !p.IsDisbanding) { rich = p; break; }
                    var q = rich ?? mp;
                    var qc = Compute(q, q.Owner);
                    int g0 = q.PartyTradeGold;
                    var args = new object[] { q, q.Owner.Clan, new TaleWorlds.CampaignSystem.ExplainedNumber(0f, false), false };
                    int got = (int)_mIncome.Invoke(fin, args);
                    int van = g0 > BkKeep ? (g0 - BkKeep) / 10 : 0;
                    bool ok = got == qc.Payout && q.PartyTradeGold == g0;
                    TLine("T6", "AddIncomeFromParty gry (podglad) dla '" + NameOf(q) + "': wynik " + got + ", regula 259 " + qc.Payout + ", gra 10% bylo by " + van
                          + (rich == null ? " (zadna rozliczana karawana nie ma kiesy ponad 10 000 - porownanie z 10% niemozliwe)" : "") + ", kiesa " + (q.PartyTradeGold == g0 ? "bez zmian" : "ZMIENIONA"),
                          ok ? "TAK" : "NIE");
                }
            }
            catch (Exception e) { TLine("T6", "wyjatek " + e.GetType().Name + ": " + (e.InnerException != null ? e.InnerException.Message : e.Message), "NIE"); }
        }

        private static void T2Check()
        {
            _t2Open = false;
            var c = _tCalc;
            int f = Formula(c.Worth, c.Capital, c.Purse, c.Floor);
            bool ok = _tMoved == f && _tBefore - _tAfter == _tMoved;
            TLine("T2", "rozliczenie rodu celu: kiesa " + _tBefore + " -> " + _tAfter + ", wyplata " + _tMoved + ", wzor na wejsciach (wartosc " + c.Worth + ", kapital " + c.Capital
                  + ", kiesa " + c.Purse + ", podloga " + c.Floor + ") " + f, ok ? "TAK" : "NIE");
        }
    }
}
