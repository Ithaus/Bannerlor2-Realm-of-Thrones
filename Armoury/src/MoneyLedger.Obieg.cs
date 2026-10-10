using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using CW = Armoury.CirculationWindows;

namespace Armoury
{
    /// <summary>
    /// PACZKA 169 - KSIEGA OBIEGU (sam log): nowe linie dzienne ksiegi pieniadza, wypisywane zaraz po starych (stare linie bajt w bajt
    /// jak przed paczka - D3). Czesc ksiegi (partial), bo druk czyta prywatne liczniki MoneyLedger (_wage, _cons, _mark...) bez
    /// wystawiania ich na zewnatrz (D1, D2).
    ///  - "Pieniadz swiata (bilans - przyczyny)": suma przyczyn + reszta = zmiana zlota swiata (ta sama doba, ci sami posiadacze);
    ///    reszta rozbita na nasz tick dobowy (RB) i reszte doby (RP), srednia 28 dob wobec progu 10 000;
    ///  - "Pieniadz swiata (rody - przyczyny)": linie modelu finansow rodow, dlug wobec korony i skarbce w trakcie rozliczen;
    ///  - "Przeplywy osad (przyczyny)": reszta kas miast, zamkow i wsi rozbita na pozycje nazwane w oknach;
    ///  - "Obieg: dzien", "Obieg: notable i karawany", "Obieg: BK i BetterEconomy", "Obieg: okna (kontrolka)".
    /// Tylko odczyt i liczniki - niczego nie zmienia w grze.
    /// </summary>
    internal static partial class MoneyLedger
    {
        // ------------------------------------------------------------ nasze przeplywy kas bez licznika (Note169, D13)
        internal const int N169Repair = 0, N169Surplus = 1, N169Kit = 2, N169PurseGone = 3, N169Other = 4;
        internal const int N169CartPurse = 5;                // 112: sakwa rozwiazanego taboru (albo bez zwyciezcy) wraca do kiesy wsi
        private const int N169 = 6;
        private static readonly string[] N169Name = { "naprawy", "skup nadwyzek zbrojowni", "komplety gracza", "sakiewki rozbitych partii", "inne", "sakwy taborow wsi (112)" };
        private static readonly long[,] _n169In = new long[Classes, N169], _n169Out = new long[Classes, N169];
        private static long _purseGoneWin, _purseGoneTown;  // sakiewki rozbitych partii: do zwyciezcow (i ich lordow) / do kas miast
        private static readonly long[] _classRest = new long[Classes];   // reszta starej linii kas (ClassLine) - do linii przyczyn

        // ------------------------------------------------------------ okno rodu (O41) i jego finalizer (D16)
        internal static bool ClanFinalizerWired;
        private static int _clanDebt0, _clanWallet0;
        private static Kingdom _clanKingdom;
        private static long _debtNew, _debtPaid, _walletInClan;
        private static int _debtNewN, _debtPaidN, _clanAborted;

        // ------------------------------------------------------------ nasz tick dobowy (D10) i pierscien reszty 28 dob
        private static long _blockWorld0 = long.MinValue;
        private static readonly long[] _restRing = new long[28];
        private static int _restHead, _restFilled;
        private static readonly long[] _tmRing = new long[28];    // reszta kas miast po nowym pomiarze (TM1) - cel 4.2 to tez srednia 28 dob
        private static int _tmHead, _tmFilled;
        private static readonly HashSet<string> _err169 = new HashSet<string>();
        private static long _repGotSess, _repDebtSess, _tribPaidSess;   // 169b: odszkodowania Diplomacy i trybut od startu sesji (linia rodow)

        private static void Stumble169(string where, Exception e)
        {
            CW.Stumbles++;
            try { if (_err169.Add(where)) Log.Error("MoneyLedger." + where, e); } catch { }
        }

        private static void Reset169()
        {
            try
            {
                _blockWorld0 = long.MinValue;
                Array.Clear(_restRing, 0, _restRing.Length); _restHead = 0; _restFilled = 0;
                Array.Clear(_tmRing, 0, _tmRing.Length); _tmHead = 0; _tmFilled = 0;
                _clanKingdom = null; _err169.Clear();
                _repGotSess = _repDebtSess = _tribPaidSess = 0;
                ClearDay169();
            }
            catch { }
        }

        private static void ClearDay169()
        {
            try
            {
                Array.Clear(_n169In, 0, _n169In.Length); Array.Clear(_n169Out, 0, _n169Out.Length);
                _purseGoneWin = _purseGoneTown = 0;
                Array.Clear(_classRest, 0, Classes);
                _debtNew = _debtPaid = _walletInClan = 0; _debtNewN = _debtPaidN = 0; _clanAborted = 0;
            }
            catch { }
        }

        /// <summary>Zloto swiata teraz (probki swiata wokol okien - 2.4): ta sama definicja co linia "Pieniadz swiata:".</summary>
        internal static long WorldNow() { return WorldTotal(); }

        /// <summary>D20: liczby dnia Last* modulow bloku zeruje JEDNO miejsce - pierwsza instrukcja BlockOpen (kazdy modul osobno).</summary>
        private static void ClearLast169()
        {
            try { KingdomTreasury.ZeroLast(); } catch (Exception e) { Stumble169("ClearLast169(KingdomTreasury)", e); }
            try { IronBank.ZeroLast(); } catch (Exception e) { Stumble169("ClearLast169(IronBank)", e); }
            try { DebtLadder.ZeroLast(); } catch (Exception e) { Stumble169("ClearLast169(DebtLadder)", e); }   // 168: liczniki doby drabiny (Bank przed ksiega)
            try { SoldierPay.ZeroLast(); } catch (Exception e) { Stumble169("ClearLast169(SoldierPay)", e); }
            try { PopulationLaw.ZeroDay169(); } catch (Exception e) { Stumble169("ClearLast169(PopulationLaw)", e); }
            try { CastlePurse.ZeroLast(); } catch (Exception e) { Stumble169("ClearLast169(CastlePurse)", e); }   // 110: zawor kas zamkow (Last*)
            try { CrownIncome.ZeroLast(); } catch (Exception e) { Stumble169("ClearLast169(CrownIncome)", e); }   // 165: wplywy dnia korony (Last*)
            try { CrownGifts.ZeroLast(); } catch (Exception e) { Stumble169("ClearLast169(CrownGifts)", e); }     // 182: dary koron (Last*)
            try { ClanBudget.ZeroLast(); } catch (Exception e) { Stumble169("ClearLast169(ClanBudget)", e); }     // 166: budzet rodow (Last*)
            try { MercContract.ZeroLast(); } catch (Exception e) { Stumble169("ClearLast169(MercContract)", e); } // 185: kontrakty najemnikow (Last*)
            try { CrownRents.ZeroLast(); } catch (Exception e) { Stumble169("ClearLast169(CrownRents)", e); }     // 180: renty korony (Last*)
            try { GentryService.ZeroLast(); } catch (Exception e) { Stumble169("ClearLast169(GentryService)", e); } // 179: zold rycerzy (Last*)
        }

        // ------------------------------------------------------------ 169b: RB wedlug odcinkow naszego ticku (granice: Mark renty, budowy, korona, paser band)
        // Zloto swiata i pozycje nazwane w bloku na granicy modulow - RB rozbity na odcinki pokazuje, ktory modul gubi zloto bez nazwy.
        // Koszt: 4 przeglady posiadaczy na dobe (migawka kas juz jest z Mark), tylko przy wlaczonej ksiedze obiegu.
        private static readonly int[] SegMarks = { MRent, MBuild, MCrown, MFence };
        private static readonly string[] SegName = { "renty", "budowy", "od budow do korony wlacznie", "od korony do pasera band", "po paserze (Bank, budzet rodow, handel bronia)" };
        private static readonly long[] _segW = new long[4], _segN = new long[4];
        private static readonly bool[] _segOk = new bool[4];
        internal static long SegTicks;

        private static void MarkWorld169(int kind, long[] snap)
        {
            try
            {
                if (_blockWorld0 == long.MinValue || snap == null || !CW.On) return;
                int i = -1;
                for (int k = 0; k < SegMarks.Length; k++) if (SegMarks[k] == kind) { i = k; break; }
                if (i < 0 || _segOk[i]) return;
                long t0 = Stopwatch.GetTimestamp();
                var h = ReadHolders(snap);
                long t = 0;
                for (int k = 0; k < Holders; k++) t += h[k];
                _segW[i] = t; _segN[i] = CW.BlockNamed; _segOk[i] = true;
                SegTicks += Stopwatch.GetTimestamp() - t0;
            }
            catch (Exception e) { Stumble169("MarkWorld169", e); }
        }

        /// <summary>D10: zloto swiata przy otwarciu naszego ticku (ta sama migawka kas co _blockSnap - jeden przeglad posiadaczy).</summary>
        private static void BlockWorld169()
        {
            _blockWorld0 = long.MinValue;
            try { Array.Clear(_segOk, 0, _segOk.Length); SegTicks = 0; } catch { }
            try
            {
                if (!CW.On || !_inBlock || _blockSnap == null) return;
                var h = ReadHolders(_blockSnap);
                long t = 0;
                for (int i = 0; i < Holders; i++) t += h[i];
                _blockWorld0 = t;
            }
            catch (Exception e) { _blockWorld0 = long.MinValue; Stumble169("BlockWorld169", e); }
        }

        // ------------------------------------------------------------ O41: dopisek do okna rodu (odczyty O(1))
        private static void ClanTickOpen169(Clan c)
        {
            try
            {
                _clanDebt0 = c.DebtToKingdom; _clanKingdom = c.Kingdom;
                _clanWallet0 = _clanKingdom != null ? _clanKingdom.KingdomBudgetWallet : 0;
            }
            catch (Exception e) { _clanKingdom = null; Stumble169("ClanTickOpen169", e); }
            ClanIncomeBook.ClanTickOpen(c);
        }

        private static void ClanTickClose169(Clan c)
        {
            try
            {
                long dd = (long)c.DebtToKingdom - _clanDebt0;
                if (dd > 0) { _debtNew += dd; _debtNewN++; }
                else if (dd < 0) { _debtPaid -= dd; _debtPaidN++; }
                if (_clanKingdom != null) _walletInClan += (long)_clanKingdom.KingdomBudgetWallet - _clanWallet0;
                _clanKingdom = null;
            }
            catch (Exception e) { Stumble169("ClanTickClose169", e); }
            ClanIncomeBook.ClanTickClose(c);
        }

        /// <summary>
        /// D16: finalizer na ClanVariablesCampaignBehavior.DailyTickClan. Bez wyjatku nic nie robi (postfiks juz zamknal okno). Po wyjatku
        /// w cudzym kodzie zamyka okno rodu od razu (dotad zostawalo otwarte do nastepnego rodu); void - wyjatek leci dalej bez zmian.
        /// </summary>
        public static void ClanTickFinalizer(Clan __0, Exception __exception)
        {
            try
            {
                if (__exception == null || _clanNow == null || !ReferenceEquals(_clanNow, __0)) return;
                _clanNow = null; _clanStale++; _clanAborted++;
                ClanIncomeBook.ClanTickAbort();
            }
            catch { }
        }

        // ------------------------------------------------------------ nasze przeplywy kas bez licznika (D13) - tylko liczniki
        /// <summary>Nasz modul zmienil kase osady poza tickiem dobowym, bez licznika starej ksiegi (kwota ze znakiem jak ChangeGold).
        /// Wlasna tablica - stara linia "Przeplywy osad (kasy miast)" bez zmian; pokazuje to linia "Przeplywy osad (przyczyny)".</summary>
        internal static void Note169(int kind, Settlement st, int amount)
        {
            try
            {
                if (_inBlock || amount == 0 || kind < 0 || kind >= N169 || !CW.On) return;
                int c = ClassOf(st);
                if (c < 0) return;
                if (amount > 0) _n169In[c, kind] += amount; else _n169Out[c, kind] -= amount;
            }
            catch (Exception e) { Stumble169("Note169", e); }
        }

        /// <summary>Sakiewka rozbitej partii: do zwyciezcy (z trzecia dla jego lorda) albo do kasy najblizszego miasta. Tylko licznik.</summary>
        internal static void NotePurseGone(int purse, bool toTown)
        {
            try
            {
                if (purse <= 0 || !CW.On) return;
                if (toTown) _purseGoneTown += purse; else _purseGoneWin += purse;
            }
            catch (Exception e) { Stumble169("NotePurseGone", e); }
        }

        // ------------------------------------------------------------ pomocnicy druku
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>Liczba albo "-" gdy okno niewpiete (nigdy 0 udajace pomiar).</summary>
        private static string Wv(long v, int w) { return CW.Wired[w] ? v.ToString(Inv) : "-"; }
        private static string WvS(long v, int w) { return CW.Wired[w] ? S(v) : "-"; }
        private static string WvNeg(long v, int w) { return CW.Wired[w] ? Neg(v) : "-"; }
        private static string Ms(long ticks) { return (ticks * 1000.0 / Stopwatch.Frequency).ToString("0.0", Inv); }

        private static readonly int[] InOrder = { CW.KNotableBand, CW.KNotableNew, CW.KNotableIncome, CW.KPrisonersParty, CW.KPrisonersFort, CW.KSiege,
                                                  CW.KTournament, CW.KRaidCapture, CW.KShips, CW.KShipsOther, CW.KBattle,
                                                  CW.KBkEstates, CW.KBeeEstateRent, CW.KBeePayout, CW.KReparations };   // 169b: majatki BK, BEE, odszkodowania
        private static readonly int[] OutOrder = { CW.KNotableBand, CW.KUpgrade, CW.KRecruitTavern, CW.KRecruitNotable, CW.KRecruitMap, CW.KShips, CW.KBattle };
        private static readonly int[] BeeKinds = { CW.KBeeContribute, CW.KBeeCamp, CW.KBeeInvest, CW.KBeeMarket };
        private static readonly int[] LordItems = { CW.ULordClaim, CW.ULordCreate, CW.ULordRevoke, CW.ULordUsurp, CW.ULordKnightClan, CW.ULordCourt, CW.ULordDilemma,
                                                    CW.ULordMoveCourt, CW.ULordRecruitKnight, CW.ULordImperial, CW.ULordBuildMaterial, CW.ULordCaravanBuy };

        private static bool Listed(int[] order, int k) { for (int i = 0; i < order.Length; i++) if (order[i] == k) return true; return false; }

        private static string KindValue(long v, int k) { int w = CW.KindWin[k]; return w >= 0 ? Wv(v, w) : v.ToString(Inv); }

        // ------------------------------------------------------------ 6.2 "Pieniadz swiata (bilans - przyczyny)"
        private static string BalanceCausesLine(int day, long[] now, long[] last)
        {
            var Sm = CW.Sum;
            long z, s0, u0;
            OldBalance(now, last, out z, out s0, out u0);
            long r0 = z - s0 + u0;
            long from, to; OldFromTo(out from, out to);
            long cons = _cons[CTown] + _cons[CCastle], regIn = _regIn[CTown] + _regIn[CCastle], regOut = _regOut[CTown] + _regOut[CCastle];
            long vanished = _vHanded - _vKept - _vTax - _vEstates;
            long routed = _wageToPurses + _wageToCoffers;
            long fromW = 0, toW = 0;
            for (int k = 1; k < CW.Kinds; k++) { fromW += CW.In[k]; toW += CW.Out[k]; }
            // nowe zrodla bez zdarzenia (Z), korekta zrodel (K1), nowe ujscia (U), netto (N)
            long z1 = Sm[CW.ZLoot], z2n = Sm[CW.ZCaravanCapitalNotable], z2l = Sm[CW.ZCaravanCapitalLord], z2 = z2n + z2l, z3 = Sm[CW.ZSlaves],
                 z4 = Sm[CW.ZHeroesBack], z5 = Sm[CW.ZTolls], k1 = Sm[CW.K1NotableAssets];
            long u1c = Sm[CW.UCaravanFromCaravan], u1n = Sm[CW.UCaravanFromNotable], u1 = u1c + u1n;
            long u2v = Sm[CW.UCommissionVillage], u2t = Sm[CW.UCommissionTownLost], u2 = u2v + u2t;
            long u3 = Sm[CW.UPorts], u4 = Sm[CW.UMinesNet], u5 = Sm[CW.UConvoys];
            long u6 = 0; for (int i = 0; i < LordItems.Length; i++) u6 += Sm[LordItems[i]];
            long u6dil = Sm[CW.ULordDilemma], u6buy = Sm[CW.ULordCaravanBuy], u6rest = u6 - u6dil - u6buy;
            long u7 = Sm[CW.UHeroesLeft];
            long u8c = Sm[CW.UPartyCaravan], u8v = Sm[CW.UPartyVillager], u8b = Sm[CW.UPartyBandit], u8g = Sm[CW.UPartyGarrison], u8o = Sm[CW.UPartyOther];
            long u8 = u8c + u8v + u8b + u8g + u8o;
            long u9 = KingdomTreasury.LastCustomsTaken;
            // 169b: karawany BetterEconomy - eskorta, awanse zalogi, straty na drogach (kasa karawany bez zdarzenia i bez odbiorcy)
            long u10e = Sm[CW.UBeeEscort], u10p = Sm[CW.UBeePromote], u10d = Sm[CW.UBeeDanger], u10 = u10e + u10p + u10d;
            long n1 = Sm[CW.NMarketKasa] + Sm[CW.NMarketNotables], n2 = Sm[CW.NVillageMarketVillage] + Sm[CW.NVillageMarketTown];
            long n3 = Sm[CW.NWorkshopOutKasa] + Sm[CW.NWorkshopOutCap] + Sm[CW.NWorkshopInKasa] + Sm[CW.NWorkshopInCap];
            long n4 = Sm[CW.NBattleNoEvent], n5 = Sm[CW.NDeadTransfers], n6 = Sm[CW.NKingdomGone], n7 = Sm[CW.NWorkshopBuyCap];
            long n8 = Sm[CW.NReparations], n9 = Sm[CW.NBeeDeliver];   // 169b: odszkodowania Diplomacy (skarbce), dostawy kontraktow karawan BEE
            long zs = z1 + z2 + z3 + z4 + z5, us = u1 + u2 + u3 + u4 + u5 + u6 + u7 + u8 + u9 + u10;
            long sTot = s0 - k1 + zs, uTot = u0 + us, nTot = n1 + n2 + n3 + n4 + n5 + n6 + n7 + n8 + n9;
            long R = z - sTot + uTot - nTot;
            long ar1 = z - sTot + uTot - nTot - R, ar2 = (r0 - R) - (zs - us + nTot - k1);
            // reszta w naszym ticku (RB): dwa odczyty calego swiata wokol bloku minus pozycje nazwane w bloku (clo z licznika cel - w bloku)
            bool haveRb = _blockWorld0 != long.MinValue;
            long holdSum = 0; for (int i = 0; i < Holders; i++) holdSum += now[i];
            long trim = TrimNow();                          // 110: dar startowy kas zamkow - nazwane ujscie w naszym ticku (w u0 przez OldBalance)
            long rb = haveRb ? (holdSum - _blockWorld0) - (CW.BlockNamed - u9) + trim : 0;
            long rp = R - rb;
            // 169b: RB wedlug odcinkow (granice Mark); odcinek bez granicy laczy sie z nastepnym; clo z licznika cel (u9) - w odcinku z korona
            var segTxt = new StringBuilder(256); var segKeys = new StringBuilder(64);
            long segSum = 0;
            if (haveRb)
            {
                long pw = _blockWorld0, pn = 0; bool u9Done = false, trimDone = false; string merged = null;
                for (int i = 0; i <= SegMarks.Length; i++)
                {
                    bool atEnd = i == SegMarks.Length;
                    if (!atEnd && !_segOk[i]) { merged = merged == null ? SegName[i] : merged + " + " + SegName[i]; segKeys.Append(" RB").Append(i + 1).Append("=-"); continue; }
                    long w = atEnd ? holdSum : _segW[i], n = atEnd ? CW.BlockNamed : _segN[i];
                    long seg = (w - pw) - (n - pn);
                    if (!u9Done && i >= 2) { seg += u9; u9Done = true; }
                    if (!trimDone && i >= 1) { seg += trim; trimDone = true; }   // 110: CastlePurse.Daily miedzy migawka rent a migawka budow
                    segSum += seg;
                    if (segTxt.Length > 0) segTxt.Append(", ");
                    segTxt.Append(merged != null ? merged + " + " : "").Append(SegName[i]).Append(' ').Append(S(seg));
                    segKeys.Append(" RB").Append(i + 1).Append('=').Append(seg);
                    merged = null; pw = w; pn = n;
                }
            }
            // pierscien reszty 28 dob
            _restRing[_restHead] = R; _restHead = (_restHead + 1) % _restRing.Length; if (_restFilled < _restRing.Length) _restFilled++;
            long ringSum = 0; for (int i = 0; i < _restFilled; i++) ringSum += _restRing[i];
            long avg = _restFilled > 0 ? ringSum / _restFilled : 0;
            var sb = new StringBuilder(4096);
            sb.Append("Pieniadz swiata (bilans - przyczyny): dzien ").Append(day)
              .Append(" | zmiana sumy ").Append(S(z)).Append(" [P] = zrodla ").Append(S(sTot)).Append(" - ujscia ").Append(uTot).Append(" + netto ").Append(S(nTot))
              .Append(" + reszta ").Append(S(R)).Append(" [R]");
            // ZRODLA
            sb.Append(" | ZRODLA z niczego ").Append(S(sTot)).Append(" [P]: \"zakupy\" mieszkancow ").Append(cons).Append(", regulator kas dosypal ").Append(regIn)
              .Append(", rozliczenia rodow na plus ").Append(_clanUp).Append(", zold, ktory nasze rozliczenie oddalo do sakiewek ludzi i kas osad ").Append(routed)
              .Append(", zloto dane przez gre z niczego poza rozliczeniami rodow ").Append(from).Append(" (w tym: ");
            bool first = true;
            for (int i = 0; i < InOrder.Length; i++) { int k = InOrder[i]; if (!first) sb.Append(", "); first = false; sb.Append(CW.KindIn[k]).Append(' ').Append(KindValue(CW.In[k], k)); }
            for (int k = 1; k < CW.Kinds; k++) if (!Listed(InOrder, k) && CW.In[k] != 0) sb.Append(", ").Append(CW.KindIn[k]).Append(' ').Append(CW.In[k]);
            sb.Append(", inne ").Append(from - fromW).Append(')')
              .Append(", minus przelew z kapitalu warsztatow i kas karawan notabli zgloszony jako zloto z niczego ").Append(Wv(k1, CW.WNotableIncome))
              .Append(", lup z cial dla AI ").Append(Wv(z1, CW.WLoot))
              .Append(", kapital nadany nowym karawanom ").Append(Both(z2, CW.WCaravanCap, CW.WCaravanNew, false)).Append(" (notable ").Append(z2n).Append(", lordowie ").Append(z2l).Append("; ")
              .Append(Sm[CW.ICaravanNewN]).Append(" karawan)")
              .Append(", BK sprzedaz niewolnikow ").Append(Wv(z3, CW.WSlaves))
              .Append(", zloto bohaterow, ktorzy wrocili do swiata ").Append(Wv(z4, CW.WHeroState))
              .Append(", myto (perk Tollgates zarzadcow) ").Append(Wv(z5, CW.WTolls));
            // UJSCIA
            sb.Append(" | UJSCIA w nicosc ").Append(uTot).Append(" [P]: rozliczenia rodow na minus ").Append(_clanDown).Append(", regulator kas skasowal ").Append(regOut)
              .Append(", z utargu wsi zniklo ").Append(vanished).Append(", zloto zabrane przez gre w nicosc poza rozliczeniami rodow ").Append(to).Append(" (w tym: ");
            first = true;
            for (int i = 0; i < OutOrder.Length; i++)
            {
                int k = OutOrder[i];
                if (!first) sb.Append(", "); first = false;
                sb.Append(CW.KindOut[k]).Append(' ').Append(KindValue(CW.Out[k], k));
                if (k == CW.KShips)
                {
                    long bee = 0; for (int b = 0; b < BeeKinds.Length; b++) bee += CW.Out[BeeKinds[b]];
                    sb.Append(", BetterEconomy ").Append(Wv(bee, CW.WBee)).Append(" (");
                    for (int b = 0; b < BeeKinds.Length; b++) { if (b > 0) sb.Append(", "); sb.Append(CW.KindOut[BeeKinds[b]]).Append(' ').Append(CW.Out[BeeKinds[b]]); }
                    sb.Append(')');
                }
            }
            for (int k = 1; k < CW.Kinds; k++) if (!Listed(OutOrder, k) && !Listed(BeeKinds, k) && CW.Out[k] != 0) sb.Append(", ").Append(CW.KindOut[k]).Append(' ').Append(CW.Out[k]);
            sb.Append(", inne ").Append(to - toW).Append(") minus zaplata za werbunek, ktora nasze rozliczenie oddalo sprzedajacym ").Append(_levyBack);
            if (trim != 0) sb.Append(", dar startowy kas zamkow przyciety (110) ").Append(trim);
            sb
              .Append(", zold karawan notabli ").Append(Wv(u1, CW.WCaravanWage)).Append(" (z kas karawan ").Append(u1c).Append(", z kies notabli ").Append(u1n).Append(')')
              .Append(", prowizja od sprzedazy partiom ").Append(Wv(u2, CW.WSell)).Append(" (wsie ").Append(u2v).Append(", miasta i zamki ").Append(u2t).Append(')')
              .Append(", BK porty ").Append(Wv(u3, CW.WPorts)).Append(", BK kopalnie ").Append(Wv(u4, CW.WMines)).Append(", BK konwoje ludnosci ").Append(Wv(u5, CW.WConvoys))
              .Append(", BK wydatki lordow ").Append(u6).Append(" (dylematy ").Append(Wv(u6dil, CW.WDilemma)).Append(", kupno karawan ").Append(Wv(u6buy, CW.WLordBuy))
              .Append(", pozostale ").Append(u6rest).Append(')')
              .Append(", zloto bohaterow, ktorzy opuscili swiat ").Append(Wv(u7, CW.WHeroState)).Append(" (zgony notabli ").Append(Sm[CW.IHeroesLeftNotableN]).Append(": ")
              .Append(Sm[CW.IHeroesLeftNotableGold]).Append("; zgony lordow ").Append(Sm[CW.IHeroesLeftLordN]).Append(": ").Append(Sm[CW.IHeroesLeftLordGold])
              .Append(" - zloto przekazane przed smiercia; inni, w tym Disabled, ").Append(Sm[CW.IHeroesLeftOtherN]).Append(": ").Append(Sm[CW.IHeroesLeftOtherGold]).Append(')')
              .Append(", kiesy partii bez wodza, ktore zniknely z mapy ").Append(u8).Append(" (karawany ").Append(u8c).Append(", tabory ").Append(u8v).Append(", bandy ").Append(u8b)
              .Append(", garnizony ").Append(u8g).Append(", inne ").Append(u8o).Append(')')
              .Append(", clo zdjete z licznika cel ").Append(u9).Append(" (cala kwota; skarbce dostaly ").Append(KingdomTreasury.LastCustoms).Append(" z kas miast - to przelew)")
              .Append(", karawany BetterEconomy (kasa karawany bez odbiorcy) ").Append(CW.Wired[CW.WBeeEscort] || CW.Wired[CW.WBeeDanger] ? u10.ToString(Inv) : "-")
              .Append(" (najem eskorty ").Append(u10e).Append(" w ").Append(Sm[CW.IBeeEscortN]).Append(" karawanach, awanse zalogi ").Append(u10p)
              .Append(", straty na niebezpiecznych drogach ").Append(u10d).Append(" w ").Append(Sm[CW.IBeeDangerN]).Append(" karawanach)");
            // NETTO
            sb.Append(" | NETTO (zmiana swiata) ").Append(S(nTot)).Append(" [P]: BK rynek osady ").Append(WvS(n1, CW.WMarket)).Append(", BK rynek wsi ").Append(WvS(n2, CW.WVillageMarket))
              .Append(", warsztaty BK bez pokrycia w kasie ").Append(Wired2(n3, CW.WWorkshopOut, CW.WWorkshopIn))
              .Append(", bitwy - zloto partii bez wodza ").Append(WvS(n4, CW.WBattle))
              .Append(", przelewy od i do bohaterow poza swiatem ").Append(S(n5)).Append(" (w tym nasze moduly bez zdarzenia - korona, Bank, budowy - ")
              .Append(S(Sm[CW.IDeadDirect])).Append(" w ").Append(Sm[CW.IDeadDirectN]).Append(" przelewach)")
              .Append(", skarbce upadlych krolestw ").Append(WvS(n6, CW.WKingdomGone))
              .Append(", warsztaty kupione przez lordow BK (kapital od nowa) ").Append(WvS(n7, CW.WLordBuy))
              .Append(", odszkodowania wojenne Diplomacy - skarbce krolestw bez zdarzenia ").Append(WvS(n8, CW.WReparations))
              .Append(" (do skarbcow odbiorcow +").Append(Sm[CW.IReparationsIn]).Append(", ze skarbcow placacych -").Append(Sm[CW.IReparationsOut])
              .Append("; placacym dopisany dlug trybutu ").Append(Sm[CW.IReparationsDebt]).Append(" - rody splacaja go potem w rozliczeniach jako \"trybut zaplacony\")")
              .Append(", dostawy kontraktow karawan BetterEconomy (zloto ze skarbcow BEE) ").Append(WvS(n9, CW.WBeeDeliver)).Append(" (").Append(Sm[CW.IBeeDeliverN]).Append(" dostaw)");
            // RESZTA
            sb.Append(" | RESZTA ").Append(S(R)).Append(" [R] (w naszym ticku dobowym ").Append(haveRb ? S(rb) : "-")
              .Append(segTxt.Length > 0 ? " - wedlug odcinkow: " + segTxt.ToString() : "")
              .Append(", poza nim ").Append(haveRb ? S(rp) : "-")
              .Append("); bez nowego pomiaru byloby ").Append(S(r0)).Append("; srednia 28 dob ").Append(S(avg)).Append(_restFilled < _restRing.Length ? " (z " + _restFilled + " dob)" : "")
              .Append(" - prog 10000: ").Append(Math.Abs(avg) < 10000 ? "OK" : "PONAD");
            // nazwy z kodu (dla nas, nie dla gracza) - przed kontrola
            sb.Append(" | tech: zold do obiegu = SoldierPay, zloto z niczego / w nicosc = zdarzenie GiveGoldAction (from / to), zwrot za werbunek = LevyGold");
            // kontrola (klucz=wartosc - dla tools/obieg169_sprawdz.py)
            sb.Append(" | kontrola: Z=").Append(z).Append(" S=").Append(sTot).Append(" U=").Append(uTot).Append(" N=").Append(nTot).Append(" R=").Append(R).Append(" R0=").Append(r0)
              .Append(" RB=").Append(haveRb ? rb.ToString(Inv) : "-").Append(" from=").Append(from).Append(" fromW=").Append(fromW).Append(" to=").Append(to).Append(" toW=").Append(toW)
              .Append(" S0=").Append(s0).Append(" U0=").Append(u0).Append(" K1=").Append(k1)
              .Append(" Z1=").Append(z1).Append(" Z2=").Append(z2).Append(" Z3=").Append(z3).Append(" Z4=").Append(z4).Append(" Z5=").Append(z5)
              .Append(" U1=").Append(u1).Append(" U2=").Append(u2).Append(" U3=").Append(u3).Append(" U4=").Append(u4).Append(" U5=").Append(u5).Append(" U6=").Append(u6)
              .Append(" U7=").Append(u7).Append(" U8=").Append(u8).Append(" U9=").Append(u9).Append(" U10=").Append(u10)
              .Append(" N1=").Append(n1).Append(" N2=").Append(n2).Append(" N3=").Append(n3).Append(" N4=").Append(n4).Append(" N5=").Append(n5).Append(" N6=").Append(n6).Append(" N7=").Append(n7)
              .Append(" N8=").Append(n8).Append(" N9=").Append(n9)
              .Append(segKeys.ToString()).Append(haveRb && segSum != rb ? " RBSUMA=" + segSum : "")
              .Append(ar1 == 0 && ar2 == 0 ? " ARYTMETYKA OK." : " ARYTMETYKA BLAD " + (ar1 != 0 ? ar1 : ar2) + ".");
            return sb.ToString();
        }

        private static string Wired2(long v, int w1, int w2) { return CW.Wired[w1] || CW.Wired[w2] ? S(v) : "-"; }
        /// <summary>Pozycja z dwoch latek, ktore musza dzialac razem (O17: okno CreateCaravanParty + odczyt modelu) - brak jednej = "-", nie 0.</summary>
        private static string Both(long v, int w1, int w2, bool sign) { return CW.Wired[w1] && CW.Wired[w2] ? (sign ? S(v) : v.ToString(Inv)) : "-"; }

        // ------------------------------------------------------------ 6.3 "Pieniadz swiata (rody - przyczyny)"
        private static string ClanCausesLine(int day)
        {
            var M = CW.Model;
            bool ml = CW.Wired[CW.WModelLines];
            long change = _clanUp - _clanDown;
            int settled = _clanUpN + _clanDownN + _clanFlatN;
            long debtAll = 0; int debtClans = 0;
            foreach (var c in Clan.All)
                if (c != null && !c.IsEliminated && c.DebtToKingdom > 0) { debtAll += c.DebtToKingdom; debtClans++; }
            // 169b: odszkodowania Diplomacy i trybut od startu sesji (raz na dobe - ta linia biegnie raz)
            _repGotSess += CW.Sum[CW.IReparationsIn] + CW.In[CW.KReparations];
            _repDebtSess += CW.Sum[CW.IReparationsDebt];
            if (ml) _tribPaidSess += -M[CW.MTributeOut];
            var sb = new StringBuilder(1024);
            sb.Append("Pieniadz swiata (rody - przyczyny): dzien ").Append(day)
              .Append(" | rozliczenia rodow zmienily zloto swiata o ").Append(S(change)).Append(" [P] w ").Append(settled).Append(" rozliczeniach")
              .Append(" | w tym naliczone w modelu finansow (odczytane ").Append(ml ? M[CW.MClansRead].ToString(Inv) : "-")
              .Append(" rozliczen): zold partii wedlug ksiegi (kazde wywolanie modelu, takze naliczenia <= 0; zbior linii Zold i uzgodnienie - linia Obieg) -").Append(_wage[WLord])
              .Append(", zalog -").Append(_wage[WGarrison]).Append(", karawan lordow -").Append(_wage[WCaravan])
              .Append(", trybut zaplacony ").Append(ml ? S(M[CW.MTributeOut]) : "-").Append(", trybut przyjety ").Append(ml ? S(M[CW.MTributeIn]) : "-")
              .Append(" (trybut zaplacony to splata dlugu trybutu krolestwa - takze odszkodowan wojennych Diplomacy, ktore odbiorca dostal z gory w chwili pokoju:")
              .Append(" dzis do skarbcow +").Append(CW.Sum[CW.IReparationsIn]).Append(" i do glow krolestw i najemnikow +").Append(CW.In[CW.KReparations])
              .Append(", placacym dopisany dlug ").Append(CW.Sum[CW.IReparationsDebt]).Append("; od startu sesji: odszkodowania przyjete ").Append(_repGotSess)
              .Append(", dlug trybutu z odszkodowan ").Append(_repDebtSess).Append(", trybut zaplacony w rozliczeniach ").Append(_tribPaidSess).Append(')')
              .Append(", udzial w kosztach najemnikow krolestwa ").Append(ml ? S(M[CW.MMercOut]) : "-").Append(", kontrakty najemnikow ").Append(ml ? S(M[CW.MMercIn]) : "-")
              .Append(", wezwanie do wojny zaplacone ").Append(ml ? S(M[CW.MCallWarOut]) : "-").Append(", przyjete ").Append(ml ? S(M[CW.MCallWarIn]) : "-")
              .Append(", splata dlugu wobec korony ").Append(ml ? S(M[CW.MDebt]) : "-")
              .Append(", dochod za tier (z niczego) ").Append(ml ? S(M[CW.MTier]) : "-").Append(" (").Append(M[CW.MTierN]).Append(" rodow, w tym najemnicy ").Append(M[CW.MTierMerc]).Append(')')
              .Append(", zapomoga ze skarbca ").Append(ml ? S(M[CW.MSupport]) : "-").Append(" (oczekiwane 0 - BK ja wylacza)")
              .Append(", podatek BK od bogatych do skarbcow ").Append(ml ? S(M[CW.MBkTax]) : "-").Append(" (przelew)")
              .Append(", rada: place ").Append(ml ? S(M[CW.MCouncilPay]) : "-").Append(" / urzad radnego ").Append(ml ? S(M[CW.MCouncilGet]) : "-").Append(" [P]")
              .Append(" (nazwy linii modelu: ").Append(CW.LineNamesFound).Append(" z 11; kwoty naliczone - z pustej kiesy schodzi mniej)")
              .Append(" | dlug wobec korony w trakcie rozliczen: nowy +").Append(_debtNew).Append(" (").Append(_debtNewN).Append(" rodow), splacony ").Append(Neg(_debtPaid))
              .Append(" (").Append(_debtPaidN).Append(" rodow), razem u ").Append(debtClans).Append(" rodow ").Append(debtAll)
              .Append(" | skarbce krolestw w trakcie rozliczen ").Append(S(_walletInClan)).Append(" (podatek BK minus zapomoga i inne)")
              .Append(" | kontrola: model=").Append(ml ? M[CW.MClansRead].ToString(Inv) : "-").Append(" rozliczenia=").Append(settled).Append('.');
            return sb.ToString();
        }

        // ------------------------------------------------------------ 6.4 "Przeplywy osad (przyczyny)"
        private static readonly string[] ClsHead = { "kasy miast", "kasy zamkow", "kiesy wsi" };

        private static string ClassCausesLine(int day)
        {
            var C = CW.Cls;
            var sb = new StringBuilder(2048);
            sb.Append("Przeplywy osad (przyczyny): dzien ").Append(day);
            var keys = new StringBuilder();
            string[] tag = { "TM", "TZ", "TW" };
            for (int c = 0; c < Classes; c++)
            {
                long tm0 = _classRest[c];
                var parts = new List<string>();
                long fresh = 0; int zeros = 0;
                bool town = c == CTown;
                void Add(string name, long v, bool wired)
                {
                    fresh += v;
                    if (town || v != 0) parts.Add(name + " " + (wired ? S(v) : "-"));
                    else zeros++;
                }
                long com = C[c, CW.LCommission], toCtr = C[c, CW.LCommissionToCounter];
                // 112: czesc ceny skasowanej przez gre wraca do kiesy wsi (w "com") i na licznik podatku wsi (toCtr) - w nicosc tylko reszta
                if (c == CVill) Add("prowizja od zakupow we wsi" + (com != 0 || toCtr != 0 ? " (w nicosc " + (-com - toCtr) + (toCtr != 0 ? ", na licznik panow (112) " + toCtr : "") + ")" : ""), com, CW.Wired[CW.WSell]);
                else Add("prowizja od sprzedazy partiom" + (com != 0 || town ? " (do licznika cel " + toCtr + ", w nicosc " + (-com - toCtr) + ")" : ""), com, CW.Wired[CW.WSell]);
                Add("warsztaty BK - kasa placi za wyroby", C[c, CW.LWorkshopOut], CW.Wired[CW.WWorkshopOut]);
                Add("warsztaty placa za wsad", C[c, CW.LWorkshopIn], CW.Wired[CW.WWorkshopIn]);
                Add("BK porty", C[c, CW.LPorts], CW.Wired[CW.WPorts]);
                Add("BK kopalnie", C[c, CW.LMines], CW.Wired[CW.WMines]);
                Add("BK konwoje ludnosci", C[c, CW.LConvoys], CW.Wired[CW.WConvoys]);
                Add("BK rynek osady", C[c, CW.LMarket], CW.Wired[CW.WMarket]);
                Add(c == CVill ? "BK rynek wsi" : "BK rynek wsi (wsie placa miastom)", C[c, CW.LVillageMarket], CW.Wired[CW.WVillageMarket]);
                var arm = new List<string>();
                long armSum = 0;
                for (int k = 0; k < N169; k++)
                {
                    long v = _n169In[c, k] - _n169Out[c, k];
                    armSum += v;
                    if (c == CTown || v != 0) arm.Add(N169Name[k] + " " + S(v));
                }
                fresh += armSum;
                if (arm.Count > 0) parts.Add("Armoury bez licznika: " + string.Join(", ", arm.ToArray()));
                long tm1 = tm0 - fresh;
                sb.Append(" | ").Append(ClsHead[c]).Append(": reszta przed nowym pomiarem ").Append(S(tm0)).Append(", nowe pozycje ").Append(S(fresh)).Append(": ")
                  .Append(parts.Count > 0 ? string.Join(", ", parts.ToArray()) : "brak").Append(zeros > 0 && c != CTown ? " (pozostale 0)" : "")
                  .Append("; reszta po nowym pomiarze ").Append(S(tm1)).Append(" [R]");
                if (town)
                {
                    // cel (projekt 4.2): reszta kas miast ponizej 10 000 dziennie, srednio z 28 dob - jak bilans swiata
                    _tmRing[_tmHead] = tm1; _tmHead = (_tmHead + 1) % _tmRing.Length; if (_tmFilled < _tmRing.Length) _tmFilled++;
                    long tmSum = 0; for (int i = 0; i < _tmFilled; i++) tmSum += _tmRing[i];
                    long tmAvg = _tmFilled > 0 ? tmSum / _tmFilled : 0;
                    sb.Append("; srednia 28 dob ").Append(S(tmAvg)).Append(_tmFilled < _tmRing.Length ? " (z " + _tmFilled + " dob)" : "")
                      .Append(" - prog 10000: ").Append(Math.Abs(tmAvg) < 10000 ? "OK" : "PONAD");
                }
                keys.Append(' ').Append(tag[c]).Append("0=").Append(tm0).Append(' ').Append(tag[c]).Append("1=").Append(tm1);
            }
            sb.Append(" | kontrola:").Append(keys.ToString()).Append('.');
            return sb.ToString();
        }

        // ------------------------------------------------------------ 6.5 "Obieg: dzien N"
        private static string Dl(long[] now, long[] last, int i) { return now[i] + (last != null ? " (" + S(now[i] - last[i]) + ")" : ""); }
        private static string Opt(long v) { return v < 0 ? "-" : v.ToString(Inv); }

        private static string ObiegLine(int day, long[] now, long[] last)
        {
            var M = CW.Model;
            bool ml = CW.Wired[CW.WModelLines];
            long build = 0; for (int c = 0; c < Classes; c++) build += _mark[c, MBuild];
            long life = 0, repair = 0; for (int c = 0; c < Classes; c++) { life += _noteIn[c, NLife]; repair += _n169In[c, N169Repair]; }
            // kasy miast: zapas kupcow jak regulator (10000 + 12 x dobrobyt)
            long reserve = 0, over = 0; int overN = 0, lowN = 0;
            foreach (var t in Town.AllTowns)
            {
                if (t == null || !t.IsTown) continue;
                long z = 10000 + (long)(12f * t.Prosperity);
                reserve += z;
                long g = t.Gold;
                if (g > z) { over += g - z; overN++; }
                if (g < z / 2) lowN++;
            }
            int kAll = 0, kPoor = 0;
            foreach (var k in Kingdom.All) { if (k == null || k.IsEliminated) continue; kAll++; if (k.KingdomBudgetWallet < 500000) kPoor++; }
            var s = Settings.Current;
            string pct = s != null ? Math.Max(0f, Math.Min(100f, s.CrownWageRefundPercent)).ToString("0.#", Inv) : "-";
            bool watch = SoldierPay.LastWatching;
            long evt = ClanIncomeBook.LastEvtNone + ClanIncomeBook.LastEvtSettl + ClanIncomeBook.LastEvtOther;
            bool cib = ClanIncomeBook.LastModelIncomeSum >= 0;
            // 169b: zold partii i zalog z JEDNEGO zbioru partii - tego samego co linia "Zold:" (SoldierPay: rekord na partie w oknie rozliczenia
            // rodu, rodzaj partii przy rozdziale); licznik ksiegi pieniadza (kazde wywolanie modelu, rodzaj w chwili naliczenia) obok, z uzgodnieniem
            long lordAcc = watch ? SoldierPay.LastLordAcc : _wage[WLord], garAcc = watch ? SoldierPay.LastGarAcc : _wage[WGarrison];
            long lordN = watch ? SoldierPay.LastLordN : _wageN[WLord], garN = watch ? SoldierPay.LastGarN : _wageN[WGarrison];
            long ledgerFromSp = SoldierPay.LastLordAcc - SoldierPay.LastToLordGold + SoldierPay.LastFromLordGold + SoldierPay.LastDupLordGold + SoldierPay.LastOutLordGold
                                + SoldierPay.LastNonPosLordGold;   // 169b po recenzji: ksiega liczy tez naliczenia <= 0 (ujemne - budzet partii z kiesa < 500 przy ubogim rodzie)
            var sb = new StringBuilder(3072);
            sb.Append("Obieg: dzien ").Append(day)
              .Append(" | rody wydaly [P]: zold partii ").Append(lordAcc).Append(" (").Append(lordN).Append(" partii; z kies zeszlo ").Append(watch ? SoldierPay.LastLordTaken.ToString(Inv) : "-")
              .Append("), zold zalog ").Append(garAcc).Append(" (").Append(garN).Append(" zalog; zeszlo ").Append(watch ? SoldierPay.LastGarTaken.ToString(Inv) : "-")
              .Append(")");
            if (watch)
                sb.Append(" [zbior partii jak linia Zold; licznik ksiegi pieniadza: partie ").Append(_wage[WLord]).Append(", zalogi ").Append(_wage[WGarrison])
                  .Append(" - uzgodnienie partii: ").Append(SoldierPay.LastLordAcc).Append(" - zmienily rodzaj na partie rodu w trakcie rozliczenia ").Append(SoldierPay.LastToLordGold)
                  .Append(" (").Append(SoldierPay.LastToLordN).Append(") + z partii rodu na inny ").Append(SoldierPay.LastFromLordGold).Append(" (").Append(SoldierPay.LastFromLordN)
                  .Append(") + powtorzone wyplaty ").Append(SoldierPay.LastDupLordGold).Append(" (").Append(SoldierPay.LastDupLordN).Append(") + poza oknem rozliczenia ")
                  .Append(SoldierPay.LastOutLordGold).Append(" (").Append(SoldierPay.LastOutLordN).Append(") + naliczenia <= 0: ").Append(SoldierPay.LastNonPosLordGold)
                  .Append(" (").Append(SoldierPay.LastNonPosLordN).Append(") = ").Append(ledgerFromSp)
                  .Append(ledgerFromSp == _wage[WLord] ? " - zgodne" : " - roznica " + S(_wage[WLord] - ledgerFromSp)).Append(']');
            sb.Append(", zold karawan lordow ").Append(_wage[WCaravan]).Append(", powinnosci do korony ").Append(KingdomTreasury.LastDues)
              .Append(", budowy do kas osad ").Append(build).Append(", dwor (166/162m) do kas siedzib ").Append(ClanBudget.LastCourt).Append(" (w tym miasta pod tarcza dworu ").Append(ClanBudget.LastCourtTown)
              .Append("), kiesa rodziny czlonek -> glowa ").Append(ClanBudget.LastFamily).Append(", zold rycerzy (179) pan -> rycerz ").Append(GentryService.LastPaid).Append(", sprzet i werbunek -")
              .Append(" | rody dostaly [P]: renta wsi ").Append(PopulationLaw.DayVillageRent).Append(", zawor miast ").Append(PopulationLaw.DayTownRent)
              .Append(", zawor zamkow ").Append(CastlePurse.LastLordPaid)
              .Append(", zwrot zoldu od korony ").Append(KingdomTreasury.LastRefundGiven).Append(" (nalezny ").Append(KingdomTreasury.LastRefundDue).Append(')')
              .Append(", mennica i monopole krolow ").Append(KingdomTreasury.LastMint + KingdomTreasury.LastMonopoly)
              .Append(", trzecia lordow z nadwyzek ludzi ").Append(cib ? ClanIncomeBook.LastThird.ToString(Inv) : "-")
              .Append(", wplywy spoza rodu poza rozliczeniem ").Append(cib ? evt.ToString(Inv) : "-")
              .Append(" (z niczego ").Append(cib ? ClanIncomeBook.LastEvtNone.ToString(Inv) : "-").Append(", od osad ").Append(cib ? ClanIncomeBook.LastEvtSettl.ToString(Inv) : "-")
              .Append(", od innych ").Append(cib ? ClanIncomeBook.LastEvtOther.ToString(Inv) : "-").Append(')')
              .Append(", renty od korony (180) ").Append(CrownRents.LastPaid).Append(", zapomoga ").Append(ml ? M[CW.MSupport].ToString(Inv) : "-")
              .Append(", dochod modelu gry (naliczony, do D) ").Append(Opt(ClanIncomeBook.LastModelIncomeSum))
              .Append(" | sakiewki ludzi [P]: stan ").Append(Dl(now, last, HPurses)).Append(", zold wplynal ").Append(SoldierPay.LastToPurse)
              .Append(", wydaly w miastach na zycie ").Append(life).Append(", naprawy ").Append(repair)
              .Append(", z rozbitych partii ").Append(_purseGoneWin + _purseGoneTown).Append(" (do zwyciezcow ").Append(_purseGoneWin).Append(", do miast ").Append(_purseGoneTown)
              .Append("), markietani -, ze zwolnionymi do kies wsi (166) ").Append(ClanBudget.LastReleasedPurse).Append(" (ludzi ").Append(ClanBudget.LastReleasedMen).Append(")")
              .Append(" | kasy miast [P]: stan ").Append(Dl(now, last, HTowns)).Append(", zapas kupcow ").Append(reserve).Append(", nadwyzka ponad zapas ").Append(over).Append(" w ")
              .Append(overN).Append(" miastach, ponizej polowy zapasu ").Append(lowN).Append(" miast, zawor do pana ").Append(PopulationLaw.DayTownRent).Append(", do korony -")
              .Append(", zold zalog do kas miast ").Append(SoldierPay.LastToTowns)
              .Append(", utarg taborow wsi zaplacony ").Append(_vPaid[CTown]).Append(" (").Append(_vVisits[CTown]).Append(" wizyt; towar niesprzedany ").Append(_vUnsold[CTown])
              .Append(" szt.), dosypka regulatora ").Append(_regIn[CTown]).Append(" (tryb 1 -), bezpiecznik korony -")
              .Append(" | kasy zamkow [P]: stan ").Append(Dl(now, last, HCastles)).Append(", zold zalog do kas ").Append(SoldierPay.LastToCastles)
              .Append(", zawor do panow ").Append(CastlePurse.LastLordPaid).Append(", do skarbcow (114) ").Append(CastlePurse.LastCrownPaid)
              .Append(", regulator: kasowanie zablokowane (110) ").Append(CastlePurse.LastRegDown).Append(", dosypka do zapasu (tryb 1) ").Append(CastlePurse.LastRegUp)
              .Append(", \"zakupy\" z niczego cofniete ").Append(CastlePurse.LastConsBack)
              .Append(" | kiesy wsi [P]: stan ").Append(Dl(now, last, HVillages)).Append(", renta do panow ").Append(PopulationLaw.DayVillageRent)
              // 112-p: przeplywy paczki 112 (liczniki VillageTakings tej doby - jej Daily biegnie po nas) i "bez odbiorcy" z progiem 0 (2.0b)
              .Append(", 112: utarg do kies ").Append(VillageTakings.TakingsToPurse).Append(" i na liczniki panow ").Append(VillageTakings.TakingsToTax)
              .Append(", towar kupiony we wsi do kies ").Append(VillageTakings.FoodToPurse)
              .Append(", sakwy taborow: wsiom ").Append(VillageTakings.CartToVillages).Append(", wodzom zwyciezcow ").Append(VillageTakings.CartToLords)
              .Append(", bandom ").Append(VillageTakings.CartToBands).Append(", zapasowemu odbiorcy ").Append(VillageTakings.CartToSpare)
              .Append(", bez odbiorcy (prog 0) ").Append(VillageTakings.Unrouted)
              .Append(" | korona [P]: wplywy - powinnosci ").Append(KingdomTreasury.LastDues).Append(", danina wojenna ").Append(KingdomTreasury.LastSubsidy)
              .Append(", udzial z zaworu zamkow (114) ").Append(CastlePurse.LastCrownPaid)
              .Append(", clo: z licznika cel zdjeto ").Append(KingdomTreasury.LastCustomsTaken).Append(" (w nicosc w calosci - licznik jest posiadaczem), skarbce dostaly ")
              .Append(KingdomTreasury.LastCustoms).Append(" z kas miast (przelew), podatek BK ").Append(ml ? (-M[CW.MBkTax]).ToString(Inv) : "-")
              .Append(", trybut przyjety ").Append(ml ? M[CW.MTributeIn].ToString(Inv) : "-")
              .Append(", odszkodowania wojenne Diplomacy przyjete ").Append(CW.Wired[CW.WReparations] ? (CW.Sum[CW.IReparationsIn] + CW.In[CW.KReparations]).ToString(Inv) : "-")
              .Append(" (do skarbcow ").Append(CW.Sum[CW.IReparationsIn]).Append(", do glow krolestw, najemnikow i gracza ").Append(CW.In[CW.KReparations])
              .Append("; ze skarbcow placacych ").Append(CW.Sum[CW.IReparationsOut]).Append(", dlug trybutu placacych +").Append(CW.Sum[CW.IReparationsDebt]).Append(')')
              // 165: korona z biezacych wplywow - wplywy dnia, 1/360 zapasu i wydatki w kolejnosci (przelewy skarbiec -> skarbiec / glowy rodow; licznik CrownIncome)
              .Append("; 165: wplywy dnia ").Append(CrownIncome.LastMeasured).Append(" + 1/360 zapasu ").Append(CrownIncome.LastRelease).Append(" = do wydania ").Append(CrownIncome.LastSpend)
              .Append(", dary (182) skarbce dawcow -> glowy rodow Strazy ").Append(CrownGifts.LastToWatch).Append(" i Dothrakow ").Append(CrownGifts.LastToDothraki)
              .Append(" (ze skarbcow dawcow ").Append(CrownGifts.LastNorth + CrownGifts.LastFree).Append(", reszta w skarbcach odbiorcow ").Append(CrownGifts.LastKept).Append(")")
              .Append(", raty reparacji skarbiec -> skarbiec ").Append(CrownIncome.LastRepOut).Append(" (przyjete ").Append(CrownIncome.LastRepIn).Append(")")
              .Append(", kontrakty najemnikow AI (185) skarbce -> glowy kompanii ").Append(MercContract.LastPaid).Append(" (nalezne ").Append(MercContract.LastDue)
              .Append("; gra dla AI: za tier wylaczone ").Append(MercContract.LastTierOff).Append(", kontrakt gry wylaczony ").Append(MercContract.LastGameOff).Append(")")
              .Append(", wezwania do wojny (168) skarbce wzywajacych -> portfele wezwanych ").Append(CrownCallToWar.LastPaid).Append(" (zerwane z braku wplywow ").Append(CrownCallToWar.LastEndedN)
              .Append(", reszta cen skreslona ").Append(CrownCallToWar.LastEnded).Append(")")
              .Append(", splata dlugu wobec korony z kies rodow do skarbcow ").Append(CrownIncome.LastDebtRepaid)
              .Append(" (splata zaliczki gry - portfel uznany z niczego - w nicosc ").Append(CrownIncome.LastAdvanceRepaid).Append(")")   // recenzja C1 (OBIEG-1)
              .Append(", clo jednym poborem (licznik cel -> skarbiec) ").Append(KingdomTreasury.LastCustomsSingle)
              .Append(", zwrot przyciety o wydatki ludzi we wlasnych miastach (K3) ").Append(KingdomTreasury.LastOwnCut)
              .Append(", 1/9 (178) z kies odbiorcow okupow i lupu do skarbcow ").Append(Ransom178.LastThirds).Append(", okupy wielkich jencow do skarbcow zdobywcow ").Append(Ransom178.LastToCrown)
              .Append(", raty okupu krola skarbiec -> porywacz ").Append(CrownIncome.LastKingRansomPaid).Append(", nagrody za wielkich jencow skarbiec -> zdobywca ").Append(Ransom178.LastRewards)
              .Append("; wyplaty - zwrot zoldu ").Append(KingdomTreasury.LastRefundGiven).Append(" (").Append(pct).Append("% z ").Append(KingdomTreasury.LastRefundPaid)
              .Append(" zaplaconego zoldu), zapomoga ").Append(ml ? M[CW.MSupport].ToString(Inv) : "-")
              .Append(", dochod za tier najemnikow (dzis z niczego) ").Append(ml ? M[CW.MTierMerc].ToString(Inv) : "-")
              .Append(", renty wedlug lenn (180) skarbce -> glowy rodow ").Append(CrownRents.LastPaid).Append(" (z reszty wplywow dnia ").Append(CrownRents.LastPool)
              .Append("; wstrzymane w skarbcach ").Append(CrownRents.LastHeld).Append(")")
              .Append("; skarbce razem ").Append(Dl(now, last, HKingdoms)).Append(", ponizej 0.5 mln: ").Append(kPoor).Append(" z ").Append(kAll)
              .Append(" | Bank [P]: pozyczki ").Append(IronBank.LastLent).Append(" na ").Append(IronBank.LastLentSum).Append(" zl, splaty ").Append(IronBank.LastPaidN)
              .Append(" na ").Append(IronBank.LastPaidSum).Append(" zl, spoznienia ").Append(IronBank.LastMissed).Append(", bankructwa ").Append(IronBank.LastDefaults)
              .Append(", kapital ").Append(IronBank.CapitalNow).Append(" zl")
              // 168: drabina dlugu - kredyt wojenny (kapital -> glowy), splaty (glowy, rodziny, zajete wsie i wyprzedaze -> Bank i wierzyciele), zysk Banku -> kasa Braavos
              .Append("; 168: kredyt wojenny kapital -> glowy ").Append(ClanBudget.LastKwLent).Append(" (rodow ").Append(ClanBudget.LastKwLentN).Append(")")
              .Append(", splata z jednorazowych ").Append(DebtLadder.LastLootRepaid).Append(", raty ").Append(DebtLadder.LastInstalments)
              .Append(", zajete (dochod wsi i kiesy ponad podloge) ").Append(DebtLadder.LastSeized).Append(", wyprzedaz ").Append(DebtLadder.LastSold)
              .Append(", do sakiewek ludzi (dlug zoldu) ").Append(DebtLadder.LastMenPaid).Append(", do skarbcow (stary dlug wobec korony) ").Append(DebtLadder.LastCrownPaid)
              .Append(", zaliczka gry w nicosc (OBIEG-1) ").Append(DebtLadder.LastAdvNothing).Append(", okupy do porywaczy ").Append(DebtLadder.LastRansomPaid)
              .Append(", portfele gry ponownie otwarte (bez zaliczki) ").Append(DebtLadder.LastReopened).Append(", zysk Banku do kasy Braavos ").Append(DebtLadder.LastProfit).Append('.');
            return sb.ToString();
        }

        // ------------------------------------------------------------ 6.6 "Obieg: notable i karawany"
        private static string ObiegNotablesLine(int day, long[] now, long[] last)
        {
            var Sm = CW.Sum;
            long bandIn = CW.In[CW.KNotableBand], bandOut = CW.Out[CW.KNotableBand], fresh = CW.In[CW.KNotableNew], inc = CW.In[CW.KNotableIncome];
            long k1 = Sm[CW.K1NotableAssets];
            long u1c = Sm[CW.UCaravanFromCaravan], u1n = Sm[CW.UCaravanFromNotable];
            long newN = Sm[CW.ICaravanNewN], newNot = Sm[CW.ICaravanNewNotableN];
            long z2 = Sm[CW.ZCaravanCapitalNotable] + Sm[CW.ZCaravanCapitalLord];
            var sb = new StringBuilder(1536);
            sb.Append("Obieg: notable i karawany: dzien ").Append(day)
              .Append(" | notable [P]: kiesy razem ").Append(Dl(now, last, HNotables))
              .Append(" | pasmo 4500-10500: nadwyzka w nicosc ").Append(WvNeg(bandOut, CW.WNotableBand)).Append(", dosypka z niczego ").Append(WvS(bandIn, CW.WNotableBand))
              .Append(" (okien ").Append(CW.Calls[CW.WNotableBand]).Append(')')
              .Append(" | nowi notable ").Append(WvS(fresh, CW.WNotableNew))
              .Append(" | majatki z niczego (do wlascicieli - notabli i lordow): BK dochod z produkcji ").Append(WvS(CW.In[CW.KBkEstates], CW.WBkEstates))
              .Append(" (w tym do notabli ").Append(WvS(Sm[CW.IEstatesNotBk], CW.WBkEstates)).Append("), BetterEconomy renta majatkow ").Append(WvS(CW.In[CW.KBeeEstateRent], CW.WBeeEstates))
              .Append(" (w tym do notabli ").Append(WvS(Sm[CW.IEstatesNotBee], CW.WBeeEstates)).Append(')')
              .Append(" | dochod z aktywow: wyplata zgloszona jako z niczego ").Append(WvS(inc, CW.WNotableIncome)).Append(" (").Append(Sm[CW.INotableIncomeN]).Append(" notabli)")
              .Append(", z kapitalu warsztatow i kas karawan zeszlo ").Append(WvNeg(k1, CW.WNotableIncome))
              .Append(", zloto z niczego netto (zaulki, majatki BK, minus podatek warsztatow BK) ").Append(WvS(inc - k1, CW.WNotableIncome))
              .Append(" | karawany notabli [P]: ").Append(Sm[CW.ICaravanN]).Append(" karawan u ").Append(Sm[CW.ICaravanNotables]).Append(" notabli, zold nalezny ")
              .Append(Sm[CW.ICaravanDue]).Append(", zold w nicosc ").Append(WvNeg(u1c + u1n, CW.WCaravanWage)).Append(" (z kas karawan ").Append(Neg(u1c)).Append(", z kies notabli ")
              .Append(Neg(u1n)).Append("), niedoplata ").Append(Sm[CW.ICaravanShort]).Append(" (karawan, ktorych kasa nie starczyla na zold: ").Append(Sm[CW.ICaravanShortN])
              .Append(" - placil notabl), doplaty notabli do 5000 ").Append(S(Sm[CW.ICaravanTopup])).Append(" (przelew); po planowanej zmianie: zold do sakiewek ludzi karawan")
              .Append(" | karawany lordow: zold naliczony ").Append(Neg(_wage[WCaravan])).Append(" (").Append(_wageN[WCaravan]).Append(" karawan)")
              .Append(" | nowe karawany: ").Append(newN).Append(" (notable ").Append(newNot).Append(", lordowie ").Append(newN - newNot).Append(", w tym kupione przez lordow BK ")
              .Append(Sm[CW.ICaravanBuyN]).Append("), kapital nadany z niczego ").Append(Both(z2, CW.WCaravanCap, CW.WCaravanNew, true)).Append(CW.Wired[CW.WCaravanCap] && CW.Wired[CW.WCaravanNew] ? " zl" : "").Append(" (w kasach zaraz po wejsciu do osady ")
              .Append(Sm[CW.ICaravanCapAfter]).Append(" - reszta to przelewy tej samej chwili: dochod BK do wlasciciela, oplaty BK do licznika cel, werbunek,")
              .Append(" oraz najem eskorty i awanse zalogi BetterEconomy - te znikaja, pozycja ujsc)")
              .Append(" | karawany, ktore zniknely z mapy: ").Append(Sm[CW.IPartyGoneCaravanN]).Append(", ich kiesy przepadly ").Append(Neg(Sm[CW.UPartyCaravan]))
              .Append(" | zmarli notable: ").Append(Sm[CW.IHeroesLeftNotableN]).Append(", zloto przepadlo ").Append(Neg(Sm[CW.IHeroesLeftNotableGold]))
              .Append(" (zmarli lordowie ").Append(Sm[CW.IHeroesLeftLordN]).Append(": zloto oddane nastepcom przed smiercia, przy zgonie ").Append(Sm[CW.IHeroesLeftLordGold]).Append(").");
            return sb.ToString();
        }

        // ------------------------------------------------------------ 6.7 "Obieg: BK i BetterEconomy"
        private static string ObiegBkLine(int day)
        {
            var Sm = CW.Sum; var C = CW.Cls;
            long u3 = Sm[CW.UPorts], u4 = Sm[CW.UMinesNet], back = Sm[CW.IMinesBack], u5 = Sm[CW.UConvoys];
            long mk = Sm[CW.NMarketKasa], mn = Sm[CW.NMarketNotables];
            long vv = Sm[CW.NVillageMarketVillage], vt = Sm[CW.NVillageMarketTown];
            long titles = Sm[CW.ULordClaim] + Sm[CW.ULordCreate] + Sm[CW.ULordRevoke] + Sm[CW.ULordUsurp];
            string food = "-";
            try { var fm = Campaign.Current.Models.SettlementFoodModel; food = fm != null ? fm.GetType().FullName : "brak"; } catch { }
            var sb = new StringBuilder(1536);
            sb.Append("Obieg: BK i BetterEconomy: dzien ").Append(day)
              .Append(" | osady BK [P]: porty ").Append(WvNeg(u3, CW.WPorts)).Append(" (").Append(Sm[CW.IPortsN]).Append(" miast)")
              .Append(", kopalnie ").Append(WvNeg(u4, CW.WMines)).Append(" (kasa zaplacila ").Append(u4 + back).Append(", wrocilo jako place gornikow ").Append(back).Append(')')
              .Append(", konwoje ludnosci ").Append(WvNeg(u5, CW.WConvoys)).Append(" (").Append(Sm[CW.IConvoysN]).Append(" konwojow)")
              .Append(", rynek osady ").Append(WvS(mk + mn, CW.WMarket)).Append(" (kasy ").Append(S(mk)).Append(": miasta ").Append(S(C[CTown, CW.LMarket])).Append(", zamki ")
              .Append(S(C[CCastle, CW.LMarket])).Append(", wsie ").Append(S(C[CVill, CW.LMarket])).Append("; notable ").Append(S(mn)).Append(')')
              .Append(", rynek wsi ").Append(WvS(vv + vt, CW.WVillageMarket)).Append(" (wsie ").Append(S(vv)).Append(", miasta i zamki ").Append(S(vt)).Append(')')
              .Append(", sprzedaz niewolnikow ").Append(Wv(Sm[CW.ZSlaves], CW.WSlaves))
              .Append(" | lordowie BK [P] (w nicosc): tytuly ").Append(Wv(titles, CW.WTitles)).Append(" (roszczenia ").Append(Sm[CW.ULordClaim]).Append(", nadania ").Append(Sm[CW.ULordCreate])
              .Append(", odebrania ").Append(Sm[CW.ULordRevoke]).Append(", uzurpacje ").Append(Sm[CW.ULordUsurp]).Append(')')
              .Append(", pasowanie z nowym rodem ").Append(Wv(Sm[CW.ULordKnightClan], CW.WKnight)).Append(", koszty laski dworu ").Append(Wv(Sm[CW.ULordCourt], CW.WCourt))
              .Append(", dylematy ").Append(Wv(Sm[CW.ULordDilemma], CW.WDilemma)).Append(", przeniesienie dworu (gracz) ").Append(Wv(Sm[CW.ULordMoveCourt], CW.WMoveCourt))
              .Append(", wyposazenie rycerza (BKROTPatch) ").Append(Wv(Sm[CW.ULordRecruitKnight], CW.WRecruitKnight)).Append(", donatywa imperium ").Append(Wv(Sm[CW.ULordImperial], CW.WImperial))
              .Append(", material na koniec budowy ").Append(BuildFunding.On ? "- (BuildFunding wlaczony)" : Wv(Sm[CW.ULordBuildMaterial], CW.WBuildMat))
              .Append(", kupno karawan ").Append(Wv(Sm[CW.ULordCaravanBuy], CW.WLordBuy)).Append(" (").Append(Sm[CW.ICaravanBuyN]).Append(" karawan; ich kapital jest w zrodlach)")
              .Append(" | warsztaty kupione przez lordow BK [P]: ").Append(Wv(Sm[CW.IWorkshopBuyN], CW.WLordBuy)).Append(" (kapital od nowa ").Append(S(Sm[CW.NWorkshopBuyCap]))
              .Append("; zaplata dawnemu wlascicielowi to przelew)")
              .Append(" | BetterEconomy [P] (w nicosc): skarbce zamkow ").Append(Wv(CW.Out[CW.KBeeContribute], CW.WBee)).Append(", obozy ").Append(Wv(CW.Out[CW.KBeeCamp], CW.WBee))
              .Append(", inwestycje we wsie ").Append(Wv(CW.Out[CW.KBeeInvest], CW.WBee)).Append(", dostep wsi do targu ").Append(Wv(CW.Out[CW.KBeeMarket], CW.WBee))
              .Append("; skarbce zamkow BEE poza swiatem")
              .Append(" | BetterEconomy karawany [P] (kasa karawany bez zdarzenia): najem eskorty ").Append(Wv(Sm[CW.UBeeEscort], CW.WBeeEscort)).Append(" (").Append(Sm[CW.IBeeEscortN])
              .Append(" karawan), awanse zalogi ").Append(Wv(Sm[CW.UBeePromote], CW.WBeePromote)).Append(", straty na niebezpiecznych drogach ").Append(Wv(Sm[CW.UBeeDanger], CW.WBeeDanger))
              .Append(" (").Append(Sm[CW.IBeeDangerN]).Append(" karawan) - w nicosc; dostawy kontraktow netto ").Append(WvS(Sm[CW.NBeeDeliver], CW.WBeeDeliver)).Append(" (")
              .Append(Sm[CW.IBeeDeliverN]).Append(" dostaw, zysk ze skarbcow BEE)")
              .Append(" | BetterEconomy z niczego [P]: renta majatkow ").Append(Wv(CW.In[CW.KBeeEstateRent], CW.WBeeEstates)).Append(", wyplaty ze skarbcow miast i umow handlowych ")
              .Append(Wv(CW.In[CW.KBeePayout], CW.WBeePayout)).Append(" | BK majatki z niczego [P]: dochod z produkcji ").Append(Wv(CW.In[CW.KBkEstates], CW.WBkEstates))
              .Append(" | nieczynne w tym zestawie modow: rzemieslnicy BK (stary kod), turnieje BK, nadwyzka zywnosci BK (czynny jest inny model zywnosci - nazwa w tech), zaopatrzenie majatkow BK (bez Economy Overhaul)")
              .Append(" | tech: WorkshopData.DoProduction, BKTournamentManager, czynny model zywnosci ").Append(food).Append('.');
            return sb.ToString();
        }

        // ------------------------------------------------------------ 6.8 "Obieg: okna (kontrolka)"
        /// <summary>t169 = czas nowych linii tej doby przed ta linia (ta linia dolicza swoj).</summary>
        private static string ObiegWindowsLine(int day, long t169)
        {
            long tSelf = Stopwatch.GetTimestamp();
            var Sm = CW.Sum;
            var sb = new StringBuilder(2048);
            sb.Append("Obieg: okna (kontrolka): dzien ").Append(day).Append(" (przy oknie: wywolan/trafien) | ");
            double winTicks = 0;
            for (int w = 0; w < CW.Windows; w++)
            {
                if (w > 0) sb.Append(", ");
                sb.Append(CW.WinName[w]).Append(' ');
                if (CW.Wired[w]) sb.Append(CW.Calls[w]).Append('/').Append(CW.Hits[w]);
                else sb.Append(CW.Missing[w] ?? "BRAK");
                if (CW.Sampled[w] > 0) winTicks += (double)CW.Ticks[w] * Math.Max(CW.Opened[w], 1) / CW.Sampled[w];
            }
            sb.Append(" | poza watkiem ").Append(CW.OffThread).Append(", potkniecia ").Append(CW.Stumbles).Append(", zagniezdzone ").Append(CW.Nested)
              .Append(", pominiete w rozliczeniach rodow ").Append(CW.InClanSkipped).Append(", rozliczenia rodow przerwane wyjatkiem ").Append(_clanAborted)
              .Append(", nowe karawany bez odczytu kapitalu ").Append(Sm[CW.ICaravanNoCap]);
            // probki swiata
            sb.Append(" | probki swiata wokol okien [P]: ");
            if (!CW.ProbeOn) sb.Append('-');
            else
            {
                int n = 0, bad = 0, sess = 0, sessBadW = 0;
                var badList = new List<string>(); var sessList = new List<string>(); var perWin = new List<string>();
                for (int w = 0; w < CW.Windows; w++)
                {
                    n += CW.ProbeN[w]; bad += CW.ProbeBad[w]; sess += CW.ProbeNSess[w];
                    if (CW.ProbeBad[w] > 0) badList.Add(CW.WinName[w] + " " + S(CW.ProbeDiff[w]));
                    if (CW.ProbeBadSess[w] > 0) { sessBadW++; sessList.Add(CW.WinName[w] + " " + CW.ProbeBadSess[w]); }
                    if (CW.ProbeNSess[w] > 0) perWin.Add(CW.WinName[w] + " " + CW.ProbeNSess[w]);
                }
                sb.Append(n).Append(" (zgodne ").Append(n - bad).Append("; rozjazd ").Append(bad).Append(badList.Count > 0 ? ": " + string.Join(", ", badList.ToArray()) : "").Append(')')
                  .Append(", od startu ").Append(sess).Append(" probek, rozjazdy w ").Append(sessBadW).Append(" oknach").Append(sessList.Count > 0 ? " (" + string.Join(", ", sessList.ToArray()) + ")" : "")
                  .Append("; od startu wg okien: ").Append(perWin.Count > 0 ? string.Join(", ", perWin.ToArray()) : "-");
                if (CW.ProbeStale > 0) sb.Append(", porzucone ").Append(CW.ProbeStale);
            }
            sb.Append(" | zgony [P]: notable ").Append(Sm[CW.IHeroesLeftNotableN]).Append(" (zloto przepadlo ").Append(Sm[CW.IHeroesLeftNotableGold]).Append("), lordowie ")
              .Append(Sm[CW.IHeroesLeftLordN]).Append(" (zloto przekazane przed smiercia, przy zgonie ").Append(Sm[CW.IHeroesLeftLordGold]).Append("), inni ")
              .Append(Sm[CW.IHeroesLeftOtherN]).Append(" (").Append(Sm[CW.IHeroesLeftOtherGold]).Append(')');
            // bohaterowie Disabled (informacja do R6) - raz na dobe
            int dis = 0; long disGold = 0;
            try
            {
                var list = Campaign.Current.CampaignObjectManager.DeadOrDisabledHeroes;
                for (int i = 0; i < list.Count; i++) { var h = list[i]; if (h != null && h.HeroState == Hero.CharacterStates.Disabled) { dis++; disGold += h.Gold; } }
            }
            catch { dis = -1; }
            long dayTicks = t169 + (Stopwatch.GetTimestamp() - tSelf) + ClanIncomeBook.LastTicks + SegTicks;
            string perScan = CW.ProbeScans > 0 ? (CW.ProbeTicks * 1000.0 / Stopwatch.Frequency / CW.ProbeScans).ToString("0.00", Inv) : "-";
            double goldTicks = CW.GoldSampled > 0 ? (double)CW.GoldTicks * CW.GoldCalls / CW.GoldSampled : 0.0;
            sb.Append(" | koszt [P]: przeliczenie doby ").Append(Ms(dayTicks)).Append(" ms (same nowe linie, budzet rodow i odcinki RB; w tym budzet rodow ").Append(Ms(ClanIncomeBook.LastTicks))
              .Append(" ms: dochod modelu ").Append(Ms(ClanIncomeBook.LastTicksModel)).Append(" ms - wyliczen ").Append(ClanIncomeBook.LastOwnCalls)
              .Append(", u ").Append(ClanIncomeBook.LastCmpN).Append(" rodow liczba z chwili powinnosci byla juz w KingdomTreasury - jej wziecie oszczedziloby ok. ")
              .Append(Ms(ClanIncomeBook.LastTicksCmp)).Append(" ms, ale zmienia D (rozna u ").Append(ClanIncomeBook.LastCmpNe).Append(" rodow)")
              .Append("; raport ").Append(Ms(ClanIncomeBook.LastTicksReport)).Append(" ms, plik CSV ").Append(Ms(ClanIncomeBook.LastTicksCsv))
              .Append(" ms; odcinki RB ").Append(Ms(SegTicks)).Append(" ms), okna ok. ").Append((winTicks * 1000.0 / Stopwatch.Frequency).ToString("0.0", Inv))
              .Append(" ms (same nasze latki - bez metod gry i cudzych latek; probka 1/16), nasluch zdarzen zlota ok. ")
              .Append((goldTicks * 1000.0 / Stopwatch.Frequency).ToString("0.0", Inv)).Append(" ms (").Append(CW.GoldCalls).Append(" zdarzen; probka 1/256), probki swiata ")
              .Append(Ms(CW.ProbeTicks)).Append(" ms (").Append(CW.ProbeScans).Append(" przegladow, ").Append(perScan).Append(" ms na przeglad)")
              .Append(" | bohaterowie Disabled: ").Append(dis >= 0 ? dis.ToString(Inv) : "-").Append(", zloto ").Append(disGold).Append(" (poza swiatem, informacja).");
            return sb.ToString();
        }
    }
}
