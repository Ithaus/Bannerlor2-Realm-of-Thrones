using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;

namespace Armoury
{
    /// <summary>
    /// f6-zwrot (decyzja Jeffa 18.10: "tak, zwrot wedlug potrzeby"; koncowy przeglad ekonomii 18.10, uwaga krytyczna "Korona zwraca 70% zoldu takze najbogatszym
    /// rodom" - docs/PRZEGLAD-EKONOMII-2026-10-18.md) - ZWROT ZOLDU WEDLUG POTRZEBY RODU (klucz CrownRefundByNeed, domyslnie TAK). Odwoluje decyzje 15.10 "zwrot
    /// zoldu z korony do rodow dalej BEZ ZMIAN" (STAN-PRAC).
    ///
    /// Dowod (fin86/fin88, budzet-rodow.csv doba 109235): ok. 63-67 rodow ponad 2R siedzi na 37-38 mln (kiesa glowy) i dostaje ok. 90-96 tys./d zwrotu (26%
    /// wszystkich), a skarbce koron chudna o 65-80 tys./d i krolowie biora kredyt w Banku, zaliczki mieszczan i skarbiec ponad zapas na brak zwrotu.
    ///
    /// Regula (KingdomTreasury.WageRefund, przy liczeniu naleznego, PO cieciach W5/W7/K3 w petli platnikow): nalezny = (int)(pelny x f), pelny = (int)(zold x 70%),
    /// f = clamp((2R - G) / R, 0, 1); G - zloto calej rodziny, R - rezerwa wojny (max(War Reserve Floor; War Reserve Days x D) + War Reserve Per Adult x dorosli) -
    /// oba z WCZORAJSZEGO budzetu 166 (ClanBudget.ChestOf; krok korony biegnie przed ClanBudget.Daily - ArmouryBehavior: WageRefund ... ClanBudget.Daily; pierwsza
    /// doba sesji - PendingChestOf: G dzis, R z D zapisu). Ponizej R - calosc (f = 1), ponad 2R - nic, pomiedzy - liniowo. Rod gracza (Jeff 18.10 przyjal): G - kiesy
    /// rodziny w chwili kroku, R z pomiaru W6b (ClanBudget.PlayerCaps - ten sam wzor co AI; gracz nie ma budzetu z wczoraj). Bez miary (f = 1, jak dotad): rod bez
    /// budzetu policzonego, dluznik i rod w zajeciu (168 - kiese bierze wierzyciel), Straz i Wolni Ludzie bez zoldu, gracz bez pomiaru (pierwsza doba sesji).
    /// Recenzja f6: rod gracza dluznik albo w zajeciu (DebtLadder.On, IsDebtor / IsSeized - z Player Same Ladder) tez bez miary - ta sama regula co AI (ChestOf) i W6b
    /// gracza (ScutagePlayer: 'dluznik albo zajecie (168)').
    /// Recenzja f6: zapas wojny korony (pierscien 184 - KingdomTreasury.WarReserveOf, W5b, K5, DS, L5b-1, korona2) liczy sie z PELNEGO zwrotu (KDay.RefundFull) -
    /// zostaje jak przed f6; f6 zmienia tylko nalezny zwrot i jego bezposrednich czytelnikow (RefundDue: 165, flaga 184 'zwrot ponad wplywy', potrzeba DS, M2b).
    /// Kaskada braku (skarbiec ponad zapas K5, zaliczka mieszczan L1, kredyt Banku 186), podzial Split, wyplata, liczniki CrownIncome - bez zmian: dostaja mniejszy
    /// nalezny. Renty 180 NIETKNIETE (nie byly czescia decyzji Jeffa) - uwaga: roznica zwrotu zostaje w reszcie wplywow dnia, z ktorej po W5/M1/M3/E3-P1 tej
    /// samej doby biora renty 180 wedlug lenn (CrownRents.Daily - LeftFor), wiec czesc wraca do rodow z lennami, takze bogatych. Regula dla rent - decyzja Jeffa.
    ///
    /// Klucz wylaczony (albo budzet 166 wylaczony) = pelny zwrot co do zlotowki jak dotad; linia "Korona: zwrot wedlug potrzeby (f6)" liczy wtedy skutek na sucho.
    /// Zloto nie powstaje i nie znika: mniej przelewu skarbiec -> kiesa platnika. Stan tylko w pamieci doby (bez zapisu); Reset czysci wszystko i pasmo gracza.
    /// Kolumny budzet-rodow.csv (ostatnie - czytniki po nazwach): zwrot_f6_mnoznik, zwrot_f6_g, zwrot_f6_r, zwrot_f6_pasmo, zwrot_f6_pelny, zwrot_f6_wedlug_potrzeby.
    /// </summary>
    internal static partial class KingdomTreasury
    {
        private const int NbBelow = 0, NbMid = 1, NbAbove = 2, NbNone = 3;   // pasma: ponizej R, R-2R, ponad 2R, bez miary
        private sealed class NeedRow { public double F = 1, G, R; public int Band = NbNone; public long Full, Need; public bool Counted; }
        private static readonly Dictionary<Clan, NeedRow> _rbn = new Dictionary<Clan, NeedRow>();
        private static int _rbnDay = int.MinValue;
        private static bool _rbnApply, _rbnMeasure;            // klucz czynny przy ostatnim kroku / pomiar mozliwy (budzet 166 wlaczony)
        private static readonly long[] _rbnFull = new long[4], _rbnNeed = new long[4];
        private static readonly int[] _rbnN = new int[4];
        private static readonly double[] _rbnG = new double[4];
        private static long _rbnRichPaid; private static int _rbnRichK;   // krolestwa w wojnie bez naleznego zwrotu (same rody ponad 2R) - ich zold
        private static int _rbnStumbles, _rbnStumblesSess; private static bool _rbnErr;
        private static int _rbnPlayerBand = -1;                 // pasmo gracza przy ostatnim kroku (komunikat tylko przy zmianie)

        internal static bool RefundByNeedOn { get { var s = Settings.Current; return s != null && s.CrownRefundByNeed && ClanBudget.On; } }

        /// <summary>Reset (KingdomTreasury.Reset - nowa kampania i kazde wczytanie): stan doby, potkniecia sesji i pasmo gracza.</summary>
        private static void RbnReset() { _rbn.Clear(); _rbnDay = int.MinValue; RbnZero(); _rbnStumblesSess = 0; _rbnErr = false; _rbnPlayerBand = -1; }

        private static void RbnZero()
        {
            for (int i = 0; i < 4; i++) { _rbnFull[i] = 0; _rbnNeed[i] = 0; _rbnN[i] = 0; _rbnG[i] = 0; }
            _rbnRichPaid = 0; _rbnRichK = 0; _rbnStumbles = 0;
        }

        private static void RbnStumble(string where, Exception e)
        {
            _rbnStumbles++; _rbnStumblesSess++;
            if (!_rbnErr) { _rbnErr = true; try { Log.Error("KingdomTreasury.RefundNeed." + where, e); } catch { } }
        }

        /// <summary>WageRefund, na poczatku liczenia (po zdjeciu licznikow zoldu): stan doby od zera.</summary>
        private static void RbnBegin()
        {
            _rbn.Clear(); RbnZero();
            _rbnDay = (int)CampaignTime.Now.ToDays;
            _rbnMeasure = ClanBudget.On;
            _rbnApply = RefundByNeedOn;
        }

        /// <summary>Mnoznik f i pasmo rodu - raz na krok (pamiec doby). Nie rzuca (wyjatek - pelny zwrot, potkniecie).</summary>
        private static NeedRow RbnOf(Clan c)
        {
            NeedRow n;
            if (c == null) return null;
            if (_rbn.TryGetValue(c, out n)) return n;
            n = new NeedRow();
            _rbn[c] = n;
            if (!_rbnMeasure) return n;
            try
            {
                double G, R; bool war;
                bool ok = ClanBudget.ChestOf(c, out G, out R, out war) || ClanBudget.PendingChestOf(c, out G, out R, out war);
                // recenzja f6: rod gracza dluznik albo w zajeciu (168, z Player Same Ladder) - bez miary jak AI (ChestOf: b.Debtor / b.Seized) i W6b gracza
                if (!ok && c == Clan.PlayerClan && !(DebtLadder.On && (DebtLadder.IsDebtor(c) || DebtLadder.IsSeized(c))))
                {
                    double capW5, capW6, d;
                    ok = ClanBudget.PlayerCaps(c, out capW5, out capW6, out R, out d);   // W6b: ten sam wzor rezerwy co AI (gracz bez budzetu z wczoraj)
                    if (ok) G = ClanIncomeBook.FamilyGoldOf(c);
                }
                if (ok && R > 0 && !double.IsNaN(G) && !double.IsInfinity(G))
                {
                    n.G = G; n.R = R;
                    n.F = Math.Max(0.0, Math.Min(1.0, (2.0 * R - G) / R));
                    n.Band = G <= R ? NbBelow : (G >= 2.0 * R ? NbAbove : NbMid);
                    if (n.Band == NbAbove) n.F = 0;
                    else if (n.Band == NbBelow) n.F = 1;
                }
            }
            catch (Exception e) { n.F = 1; n.Band = NbNone; RbnStumble("Of", e); }
            return n;
        }

        /// <summary>WageRefund, krolestwo w wojnie, wiersz platnika (zold v po cieciach W5/W7/K3): pelny = (int)(v x pct), wedlug potrzeby = (int)(pelny x f).
        /// Zwraca nalezny do wyplaty (klucz czynny - wedlug potrzeby, inaczej pelny). Liczniki pasm - dopiero RbnCommit (wiersze krolestw, ktore doszly do wyplaty).</summary>
        private static int RbnDue(Clan c, int v, float pct, out int full, out NeedRow row)
        {
            full = (int)(v * pct); row = null;
            try
            {
                row = RbnOf(c);
                if (row == null || full <= 0) return full;
                int need = row.F >= 1.0 ? full : (int)(full * row.F);
                return _rbnApply ? need : full;
            }
            catch (Exception e) { RbnStumble("Due", e); row = null; return full; }
        }

        /// <summary>WageRefund: wiersze krolestwa do licznikow pasm i rodow (wolane dla krolestwa, ktore doszlo do wyplaty, i dla krolestwa w wojnie bez naleznego -
        /// same rody ponad 2R). Nie rzuca.</summary>
        private static void RbnCommit(int[] full, NeedRow[] rows, long paidSum, bool noDue)
        {
            try
            {
                if (full == null || rows == null) return;
                long fsum = 0;
                for (int i = 0; i < full.Length && i < rows.Length; i++)
                {
                    var n = rows[i];
                    int b = n != null ? n.Band : NbNone;
                    long f = Math.Max(0, full[i]);
                    long need = n == null || n.F >= 1.0 ? f : (long)(int)(f * n.F);   // ten sam wzor co RbnDue - suma pasm = nalezny linii zwrotu
                    _rbnFull[b] += f; _rbnNeed[b] += need; fsum += f;
                    if (n != null && f > 0)   // rod liczony w pasmie tylko z pelnym zwrotem > 0 (wiersz bez zoldu do zwrotu - poza licznikami i CSV)
                    {
                        n.Full += f; n.Need += need;
                        if (!n.Counted) { n.Counted = true; _rbnN[b]++; _rbnG[b] += Math.Max(0, n.G); }
                    }
                }
                if (noDue && fsum > 0) { _rbnRichK++; _rbnRichPaid += paidSum; }   // pelny > 0, a nalezny 0 - same rody ponad 2R
            }
            catch (Exception e) { RbnStumble("Commit", e); }
        }

        /// <summary>Recenzja f6: suma pelnego zwrotu wierszy krolestwa (rc.Full) - KDay.RefundFull, do zapasu wojny jak przed f6 (rowna 'nalezne' sprzed paczki).</summary>
        private static long RbnSum(int[] full)
        {
            long x = 0;
            if (full != null) for (int i = 0; i < full.Length; i++) x += Math.Max(0, full[i]);
            return x;
        }

        /// <summary>Pelny zwrot doby (suma pasm) - do linii zwrotu.</summary>
        private static long RbnFullTotal() { long x = 0; for (int i = 0; i < 4; i++) x += _rbnFull[i]; return x; }

        /// <summary>WageRefund, po linii zwrotu: linia "Korona: zwrot wedlug potrzeby (f6)" i komunikat gracza przy zmianie pasma. totalDue - nalezny z linii zwrotu.</summary>
        private static void RbnLine(long totalDue)
        {
            try
            {
                var inv = CultureInfo.InvariantCulture;
                long full = 0, need = 0; int cut = 0;
                for (int i = 0; i < 4; i++) { full += _rbnFull[i]; need += _rbnNeed[i]; }
                foreach (var kv in _rbn) if (kv.Value != null && kv.Value.Counted && kv.Value.Need < kv.Value.Full) cut++;
                string[] name = { "ponizej R (pelny)", "R-2R (czesc, liniowo)", "ponad 2R (nic)", "bez miary (pelny: bez budzetu, dluznik, zajecie, Straz bez zoldu, gracz bez pomiaru)" };
                string[] shortName = { "ponizej R", "R-2R", "ponad 2R", "bez miary" };
                var sb = new StringBuilder(900);
                sb.Append("Korona: zwrot wedlug potrzeby (f6): dzien ").Append(_rbnDay).Append(" | Crown Refund By Need ")
                  .Append(_rbnApply ? "tak (czynny)" : (_rbnMeasure ? "NIE (na sucho - zwrot pelny)" : "NIE (budzet 166 wylaczony - zwrot pelny, bez pomiaru)"))
                  .Append(" | f = (2R - G) / R w 0..1 (G - zloto rodziny, R - rezerwa wojny; budzet 166 z wczoraj), po cieciach W5/W7/K3, krolestwa w wojnie");
                for (int b = 0; b < 4; b++)
                    sb.Append(" | ").Append(name[b]).Append(": rodow ").Append(_rbnN[b]).Append(" (zloto rodzin ").Append((_rbnG[b] / 1e6).ToString("0.00", inv)).Append(" mln)")
                      .Append(", pelny ").Append(_rbnFull[b]).Append(", wedlug potrzeby ").Append(_rbnNeed[b]);
                sb.Append(" | razem pelny ").Append(full).Append(", wedlug potrzeby ").Append(need).Append(", nalezny dzis ").Append(totalDue)
                  .Append(_rbnApply ? "; zwrot mniejszy o " : "; zwrot bylby mniejszy o (na sucho) ").Append(full - need)   // recenzja f6: to nie oszczednosc skarbca - roznica zostaje w reszcie wplywow dnia
                  .Append(" zl/d (zostaje w reszcie wplywow dnia - prawdziwa zmiana skarbcow w linii 'Skarbce:')")
                  .Append(" | rodow z ucietym zwrotem ").Append(cut)
                  .Append(" | krolestw w wojnie bez naleznego (same rody ponad 2R) ").Append(_rbnRichK).Append(" (zold ").Append(_rbnRichPaid).Append(")");
                NeedRow p = null;
                var pc = Clan.PlayerClan;
                if (pc != null && _rbn.TryGetValue(pc, out p) && p != null && p.Counted)
                    sb.Append(" | rod gracza: pasmo ").Append(shortName[p.Band])
                      .Append(", f ").Append(p.F.ToString("0.00", inv)).Append(", G ").Append(Math.Round(p.G).ToString("0", inv)).Append(", R ").Append(Math.Round(p.R).ToString("0", inv))
                      .Append(", pelny ").Append(p.Full).Append(", wedlug potrzeby ").Append(p.Need);
                else sb.Append(" | rod gracza: bez zwrotu dzis");
                sb.Append(" | renty 180 bez zmian (roznica zwrotu zostaje w reszcie wplywow dnia - W5, M1, M3, dwor krola, potem renty wedlug lenn)")
                  .Append(" | zapas wojny korony (184, W5b, K5, DS, L5b-1, korona2) z pelnego zwrotu - jak przed f6")
                  .Append(" | potkniecia ").Append(_rbnStumbles).Append(" (sesja ").Append(_rbnStumblesSess).Append(").");
                Log.Info(sb.ToString());
                // komunikat gracza - tylko przy zmianie pasma (klucz czynny, rod gracza z naleznym dzis)
                if (_rbnApply && p != null && p.Counted && p.Band != NbNone && p.Band != _rbnPlayerBand)
                {
                    if (p.Band == NbAbove)
                        Log.Player("The crown no longer repays your wages: your house holds more than twice its war reserve and pays for its own war.");
                    else if (p.Band == NbMid)
                        Log.Player("The crown now repays only part of your wages: your house holds more than its war reserve - the richer the house, the smaller the share, and nothing above twice the reserve.");
                    else if (_rbnPlayerBand == NbMid || _rbnPlayerBand == NbAbove)
                        Log.Player("The crown again repays your wages in full: your house's gold is below its war reserve.");
                    _rbnPlayerBand = p.Band;
                }
            }
            catch (Exception e) { RbnStumble("Line", e); }
        }

        /// <summary>Kolumny budzet-rodow.csv (za lup_na_zaloge_ww2 - czytniki po nazwach): krok zwrotu tej doby.</summary>
        internal const string RefundNeedCsvHeader = ";zwrot_f6_mnoznik;zwrot_f6_g;zwrot_f6_r;zwrot_f6_pasmo;zwrot_f6_pelny;zwrot_f6_wedlug_potrzeby";

        internal static string RefundNeedCsv(Clan c)
        {
            try
            {
                NeedRow n;
                if (c == null || Campaign.Current == null || _rbnDay != (int)CampaignTime.Now.ToDays || !_rbn.TryGetValue(c, out n) || n == null || !n.Counted) return ";;;;;;";
                var inv = CultureInfo.InvariantCulture;
                return ";" + n.F.ToString("0.0000", inv) + ";" + Math.Round(n.G).ToString("0", inv) + ";" + Math.Round(n.R).ToString("0", inv) + ";" + n.Band.ToString(inv)
                       + ";" + n.Full.ToString(inv) + ";" + n.Need.ToString(inv);
            }
            catch { return ";;;;;;"; }
        }
    }
}
