using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;

namespace Armoury
{
    /// <summary>
    /// f7-renty (decyzja Jeffa 18.10 ok. 07:28 PDT, doslownie: "Renty wedlug potrzeby (moja rada): ta sama zasada co przy zwrocie. Bogaty rod nie dostaje renty,
    /// sredni dostaje czesc, biedny cala. Co zostanie, zostaje w skarbcu krolestwa na wojny i trudne czasy - to robimy.") - RENTY 180 WEDLUG POTRZEBY RODU
    /// (klucz CrownRentsByNeed, domyslnie TAK).
    ///
    /// Dowod (recenzja f6, fin91 - D:/Backup-Bannerlord/logi-testow/Armoury-2026-10-18_06-29-09.log, nzwrot NZR-7): zwrot wedlug potrzeby (f6) zmniejszyl zwrot,
    /// a roznica tego samego dnia szla w renty 180 wedlug lenn do wszystkich rodow (takze bogatych): renty 284 tys./d, skarbce dalej -43 tys./d. CSV fin91
    /// (28 dob): ponad 2R 43 rody / 84 tys./d rent, R-2R 67 rodow / 134 tys./d, ponizej R 12 / 26 tys., bez miary 24 / 39 tys.
    ///
    /// Regula (CrownRents.Daily, przy wyplacie, PO podziale puli wedlug lenn i warunkach zalogi / sluzby - E5/F9 bez zmian): renta rodu z warunkiem = (int)(udzial x f),
    /// f = clamp((2R - G) / R, 0, 1) - TEN SAM wzor (KingdomTreasury.NeedBand), te same G i R i ta sama pamiec doby co zwrot zoldu f6 (KingdomTreasury.NeedOfClan:
    /// budzet 166 z wczoraj - ChestOf, pierwsza doba sesji PendingChestOf; bez miary - f = 1: rod bez budzetu, dluznik i zajecie 168, Straz i Wolni Ludzie bez
    /// zoldu, najemnik). Rod gracza: w wojnie jak f6 (PlayerCaps), w pokoju R tym samym wzorem z ClanBudget.PlayerReserve (renty ida i w pokoju; R nie zalezy od
    /// wojny), G - kiesy rodziny w chwili kroku; dluznik / w zajeciu albo pierwsza doba sesji - bez miary (pelna renta), jak f6.
    /// Czego rod nie dostanie (udzial - renta), ZOSTAJE W SKARBCU KROLESTWA: nie wraca do puli, nie idzie do innych rodow (Jeff), nie jest wydawane tego dnia
    /// (CrownIncome.Spent tylko na wyplacone - jak udzialy wstrzymane rodow bez warunku); od jutra jest w zapasie korony (Stock, 1/CrownReserveReleaseDays na dobe)
    /// i czytaja go regula zapasu: W5b (skarbiec ponad zapas wojny dla biednych panow w wojnie), K5 (brak zwrotu ze skarbca ponad zapas), flaga 184 "skarbiec
    /// < zapas" (mniej podatku wojennego ludu), korona2 (mniej pozyczek od miast), lup stolicy (CapitalTreasury). Zapas wojny sam (pierscien 184) bez zmian.
    /// Zloto nie powstaje i nie znika: tozsamosc w linii - skarbce w kroku rent ubyly dokladnie o renty 180; reszta wplywow dnia = renty + udzialy rodow bez
    /// warunku + zatrzymane wedlug potrzeby + zaokraglenia.
    /// Klucz wylaczony (albo budzet 166 wylaczony) = renty co do zlotowki jak dotad; linia "Korona: renty wedlug potrzeby (f7)" liczy wtedy skutek na sucho.
    /// Stan tylko w pamieci doby (bez zapisu - nowe klucze SyncData niepotrzebne); Reset czysci wszystko i pasmo gracza. Kolumny budzet-rodow.csv (ostatnie -
    /// czytniki po nazwach): renta_f7_mnoznik, renta_f7_g, renta_f7_r, renta_f7_pasmo, renta_f7_pelna, renta_f7_wedlug_potrzeby.
    /// </summary>
    internal static partial class CrownRents
    {
        internal static bool NeedOn { get { var s = Settings.Current; return s != null && s.CrownRentsByNeed && ClanBudget.On && On; } }   // On: renty 180 czynne (napisy linii f6 / W5 tylko wtedy)

        private const int RnBelow = 0, RnMid = 1, RnAbove = 2, RnNone = 3;   // pasma jak f6: ponizej R, R-2R, ponad 2R, bez miary
        private sealed class RnRow { public double F = 1, G, R; public int Band = RnNone; public long Full, Need, Paid, Fail; public bool Counted; }
        private static readonly Dictionary<Clan, RnRow> _rn = new Dictionary<Clan, RnRow>();
        private static int _rnDay = int.MinValue;
        private static bool _rnApply, _rnMeasure;          // klucz czynny dzis / pomiar mozliwy (budzet 166 wlaczony)
        private static readonly long[] _rnFull = new long[4], _rnNeed = new long[4], _rnPaidB = new long[4];
        private static readonly int[] _rnN = new int[4];
        private static readonly double[] _rnG = new double[4];
        private static long _rnFail, _rnW0, _rnW1; private static int _rnWK;
        private static int _rnStumbles, _rnStumblesSess; private static bool _rnErr;
        private static int _rnPlayerBand = -1;              // pasmo renty gracza przy ostatnim kroku (komunikat tylko przy zmianie)
        /// <summary>f7: udzialy rodow z warunkiem zatrzymane dzis w skarbcach wedlug potrzeby rodu (linia 180, linia f7, dopisek linii 165).</summary>
        internal static long LastKeptNeed;

        /// <summary>Reset (nowa kampania i kazde wczytanie): stan doby, potkniecia sesji i pasmo gracza.</summary>
        private static void NeedReset() { _rn.Clear(); _rnDay = int.MinValue; NeedZero(); _rnStumblesSess = 0; _rnErr = false; _rnPlayerBand = -1; _rnApply = false; _rnMeasure = false; }

        private static void NeedZero()
        {
            for (int i = 0; i < 4; i++) { _rnFull[i] = 0; _rnNeed[i] = 0; _rnPaidB[i] = 0; _rnN[i] = 0; _rnG[i] = 0; }
            _rnFail = 0; _rnW0 = 0; _rnW1 = 0; _rnWK = 0; _rnStumbles = 0;
        }

        private static void NeedStumble(string where, Exception e)
        {
            _rnStumbles++; _rnStumblesSess++;
            if (!_rnErr) { _rnErr = true; try { Log.Error("CrownRents.Need." + where, e); } catch { } }
        }

        /// <summary>Daily, na samym poczatku: wiersze i liczniki doby od zera, klucz i pomiar tej doby. Nie rzuca.</summary>
        private static void NeedBegin()
        {
            _rn.Clear(); NeedZero();
            try
            {
                _rnDay = Campaign.Current != null ? (int)CampaignTime.Now.ToDays : int.MinValue;
                _rnMeasure = ClanBudget.On;
                _rnApply = NeedOn;
            }
            catch (Exception e) { _rnApply = false; _rnMeasure = false; NeedStumble("Begin", e); }
        }

        /// <summary>Wiersz rodu (pamiec doby): f, pasmo, G, R - z KingdomTreasury.NeedOfClan (ta sama regula i pamiec co zwrot f6), rod gracza w pokoju - R
        /// z ClanBudget.PlayerReserve (ten sam wzor). Nie rzuca (wyjatek - pelna renta, potkniecie).</summary>
        private static RnRow NeedRowOf(Clan c)
        {
            if (c == null) return null;
            if (Campaign.Current != null && _rnDay != (int)CampaignTime.Now.ToDays) NeedBegin();   // wolanie poza krokiem doby rent (nie powinno) - stan nowej doby
            RnRow n;
            if (_rn.TryGetValue(c, out n)) return n;
            n = new RnRow();
            _rn[c] = n;
            if (!_rnMeasure) return n;
            try
            {
                double f, G, R;
                int b = KingdomTreasury.NeedOfClan(c, out f, out G, out R);
                // rod gracza w pokoju: PlayerCaps (miara f6 / W5 / W6) liczy tylko w wojnie - ta sama rezerwa R z PlayerReserve; dluznik albo w zajeciu (168) - bez miary jak AI
                if (b == RnNone && c == Clan.PlayerClan && !(DebtLadder.On && (DebtLadder.IsDebtor(c) || DebtLadder.IsSeized(c))))
                {
                    double r;
                    if (ClanBudget.PlayerReserve(c, out r))
                    {
                        G = ClanIncomeBook.FamilyGoldOf(c); R = r;
                        if (!double.IsNaN(G) && !double.IsInfinity(G)) b = KingdomTreasury.NeedBand(G, R, out f);
                    }
                }
                if (b >= RnBelow && b <= RnAbove && R > 0) { n.F = Math.Max(0.0, Math.Min(1.0, f)); n.G = G; n.R = R; n.Band = b; }
            }
            catch (Exception e) { n.F = 1; n.Band = RnNone; NeedStumble("Of", e); }
            return n;
        }

        /// <summary>M1 (KingdomTreasury.RentAidSplit - tylko pomiar linii W5): mnoznik renty rodu przy czynnym kluczu (wylaczony - 1).</summary>
        internal static double NeedFactor(Clan c)
        {
            try
            {
                if (!_rnApply || c == null) return 1.0;
                var n = NeedRowOf(c);
                return n != null ? n.F : 1.0;
            }
            catch (Exception e) { NeedStumble("Factor", e); return 1.0; }
        }

        /// <summary>Daily, wyplata: udzial x rodu z warunkiem -> renta wedlug potrzeby (int)(x x f) przy czynnym kluczu (wylaczony - x bez zmian); liczniki pasm
        /// i rodu takze na sucho. Nie rzuca (wyjatek - x bez zmian, potkniecie).</summary>
        private static int NeedCut(Clan c, int x)
        {
            try
            {
                if (x <= 0) return x;
                var n = NeedRowOf(c);
                if (n == null) return x;
                int need = n.F >= 1.0 ? x : (int)(x * n.F);
                if (need < 0) need = 0;
                if (need > x) need = x;
                int b = n.Band;
                _rnFull[b] += x; _rnNeed[b] += need;
                n.Full += x; n.Need += need;
                if (!n.Counted) { n.Counted = true; _rnN[b]++; _rnG[b] += Math.Max(0, n.G); }
                return _rnApply ? need : x;
            }
            catch (Exception e) { NeedStumble("Cut", e); return x; }
        }

        /// <summary>Daily, po wyplacie rodu z wierszem f7: wyplacone (albo nieudana wyplata - zostaje w skarbcu jak dotad).</summary>
        private static void NeedPaid(Clan c, int paid, int failed)
        {
            try
            {
                RnRow n;
                if (c == null || !_rn.TryGetValue(c, out n) || n == null || !n.Counted) return;
                long p = Math.Max(0, paid), f = Math.Max(0, failed);
                n.Paid += p; n.Fail += f;
                _rnPaidB[n.Band] += p; _rnFail += f;
            }
            catch (Exception e) { NeedStumble("Paid", e); }
        }

        /// <summary>Daily, krolestwo z wyplata: skarbiec przed i po wyplacie rent (tozsamosc linii: ubylo = renty 180).</summary>
        private static void NeedWallet(long w0, long w1) { _rnW0 += w0; _rnW1 += w1; _rnWK++; }

        /// <summary>Linia 165: dopisek do "wstrzymane w skarbcach" (tylko przy czynnym kluczu - wylaczony: napis jak dotad).</summary>
        internal static string NeedNote165()
        {
            try { return On && _rnApply && Campaign.Current != null && _rnDay == (int)CampaignTime.Now.ToDays ? "; w tym zatrzymane wedlug potrzeby rodu (f7) " + LastKeptNeed : ""; }
            catch { return ""; }
        }

        /// <summary>Daily, po linii 180: linia "Korona: renty wedlug potrzeby (f7)" i komunikat gracza przy zmianie pasma. Nie rzuca.</summary>
        private static void NeedLine(Settings s, int today)
        {
            try
            {
                var inv = CultureInfo.InvariantCulture;
                long full = 0, need = 0, paidB = 0; int cut = 0;
                for (int i = 0; i < 4; i++) { full += _rnFull[i]; need += _rnNeed[i]; paidB += _rnPaidB[i]; }
                foreach (var kv in _rn) if (kv.Value != null && kv.Value.Counted && kv.Value.Need < kv.Value.Full) cut++;
                string[] name = { "ponizej R (cala)", "R-2R (czesc, liniowo)", "ponad 2R (nic)", "bez miary (cala: bez budzetu, dluznik, zajecie, Straz bez zoldu, gracz bez pomiaru)" };
                string[] shortName = { "ponizej R", "R-2R", "ponad 2R", "bez miary" };
                if (s != null && s.LogEnabled)
                {
                    var sb = new StringBuilder(1400);
                    sb.Append("Korona: renty wedlug potrzeby (f7): dzien ").Append(today).Append(" | Crown Rents By Need ")
                      .Append(_rnApply ? "tak (czynny)" : (_rnMeasure ? "NIE (na sucho - renty pelne)" : "NIE (budzet 166 wylaczony - renty pelne, bez pomiaru)"))
                      .Append(" | f = (2R - G) / R w 0..1 (G - zloto rodziny, R - rezerwa wojny; budzet 166 z wczoraj) - ta sama regula i pamiec doby co zwrot zoldu (f6),")
                      .Append(" udzial rodu z warunkiem po podziale wedlug lenn");
                    for (int b = 0; b < 4; b++)
                        sb.Append(" | ").Append(name[b]).Append(": rodow ").Append(_rnN[b]).Append(" (zloto rodzin ").Append((_rnG[b] / 1e6).ToString("0.00", inv)).Append(" mln)")
                          .Append(", pelna ").Append(_rnFull[b]).Append(", wedlug potrzeby ").Append(_rnNeed[b]).Append(", wyplacone ").Append(_rnPaidB[b]);
                    sb.Append(" | razem pelna ").Append(full).Append(", wedlug potrzeby ").Append(need).Append(", wyplacone ").Append(paidB)
                      .Append(" (renty 180 ").Append(LastPaid).Append("), nieudane wyplaty ").Append(_rnFail)
                      .Append(_rnApply ? "; zatrzymane w skarbcach " : "; zatrzymane bylyby (na sucho) ").Append(full - need)
                      .Append(" zl/d (zostaje w skarbcu krolestwa - zapas na wojny i trudne czasy; nie idzie do innych rodow)")
                      .Append(" | skarbce w kroku rent (krolestw ").Append(_rnWK).Append("): przed ").Append(_rnW0).Append(", po ").Append(_rnW1).Append(", ubylo ").Append(_rnW0 - _rnW1)
                      .Append(" (= renty 180; zatrzymane wedlug potrzeby ").Append(LastKeptNeed).Append(" zostaly w skarbcach)")
                      .Append(" | reszta wplywow dnia po zwrocie ").Append(LastPool).Append(" = renty ").Append(LastPaid).Append(" + udzialy rodow bez warunku ").Append(LastHeldFail)
                      .Append(" + zatrzymane wedlug potrzeby ").Append(LastKeptNeed).Append(" + zaokraglenia ").Append(LastRound)
                      .Append(" | rodow z ucieta renta ").Append(cut);
                    RnRow p0;
                    var pc0 = Clan.PlayerClan;
                    if (pc0 != null && _rn.TryGetValue(pc0, out p0) && p0 != null && p0.Counted)
                        sb.Append(" | rod gracza: pasmo ").Append(shortName[p0.Band]).Append(", f ").Append(p0.F.ToString("0.00", inv))
                          .Append(", G ").Append(Math.Round(p0.G).ToString("0", inv)).Append(", R ").Append(Math.Round(p0.R).ToString("0", inv))
                          .Append(", pelna ").Append(p0.Full).Append(", wedlug potrzeby ").Append(p0.Need);
                    else sb.Append(" | rod gracza: bez renty dzis");
                    sb.Append(" | potkniecia ").Append(_rnStumbles).Append(" (sesja ").Append(_rnStumblesSess).Append(").");
                    Log.Info(sb.ToString());
                }
                // komunikat gracza - tylko przy zmianie pasma (klucz czynny, rod gracza z renta dzis)
                RnRow p;
                var pc = Clan.PlayerClan;
                if (_rnApply && pc != null && _rn.TryGetValue(pc, out p) && p != null && p.Counted && p.Band != RnNone && p.Band != _rnPlayerBand)
                {
                    if (p.Band == RnAbove)
                        Log.Player("The crown no longer pays your house its fief rents: your house holds more than twice its war reserve. Your share stays in the realm's treasury for wars and hard times.");
                    else if (p.Band == RnMid)
                        Log.Player("The crown now pays your house only part of its fief rents: your house holds more than its war reserve - the richer the house, the smaller the rent. The rest stays in the realm's treasury.");
                    else if (_rnPlayerBand == RnMid || _rnPlayerBand == RnAbove)
                        Log.Player("The crown again pays your house its full fief rents: your house's gold is below its war reserve.");
                    _rnPlayerBand = p.Band;
                }
            }
            catch (Exception e) { NeedStumble("Line", e); }
        }

        /// <summary>Kolumny budzet-rodow.csv (za zwrot_f6_* - czytniki po nazwach): renta wedlug potrzeby tej doby (rody z warunkiem i udzialem > 0).</summary>
        internal const string NeedCsvHeader = ";renta_f7_mnoznik;renta_f7_g;renta_f7_r;renta_f7_pasmo;renta_f7_pelna;renta_f7_wedlug_potrzeby";

        internal static string NeedCsvCols(Clan c)
        {
            try
            {
                RnRow n;
                if (c == null || Campaign.Current == null || _rnDay != (int)CampaignTime.Now.ToDays || !_rn.TryGetValue(c, out n) || n == null || !n.Counted) return ";;;;;;";
                var inv = CultureInfo.InvariantCulture;
                return ";" + n.F.ToString("0.0000", inv) + ";" + Math.Round(n.G).ToString("0", inv) + ";" + Math.Round(n.R).ToString("0", inv) + ";" + n.Band.ToString(inv)
                       + ";" + n.Full.ToString(inv) + ";" + n.Need.ToString(inv);
            }
            catch { return ";;;;;;"; }
        }
    }
}
