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
    /// f7-renty (decyzja Jeffa 18.10 ok. 07:28 PDT "Renty wedlug potrzeby ... to robimy"): renty 180 ta sama regula (klucz CrownRentsByNeed, CrownRents.Need.cs) -
    /// NeedBand (wzor) i NeedOfClan (ta sama pamiec doby _rbn); czego rod nie dostanie, zostaje w skarbcu krolestwa.
    /// Recenzja f7: przy czynnym kluczu f7 komunikat gracza o zwrocie idzie przez CrownRents.QueueWageMsg (jeden wspolny z komunikatem renty w tym samym pasmie)
    /// i pasmo komunikatu ma martwa strefe 5% R (CrownRents.MsgBand); klucz f7 wylaczony - komunikat jak dotad.
    ///
    /// Klucz wylaczony (albo budzet 166 wylaczony) = pelny zwrot co do zlotowki jak dotad; linia "Korona: zwrot wedlug potrzeby (f6)" liczy wtedy skutek na sucho.
    /// Zloto nie powstaje i nie znika: mniej przelewu skarbiec -> kiesa platnika. Stan tylko w pamieci doby (bez zapisu); Reset czysci wszystko i pasmo gracza.
    /// Kolumny budzet-rodow.csv (ostatnie - czytniki po nazwach): zwrot_f6_mnoznik, zwrot_f6_g, zwrot_f6_r, zwrot_f6_pasmo, zwrot_f6_pelny, zwrot_f6_wedlug_potrzeby.
    /// f23-korona (klucz NeedMeasureAll; decyzja Jeffa 19.10, odpowiedz 4a "mierzyc ich wlasnym zlotem jak wszystkich innych"): koniec pasma "bez miary" dla dluznikow 168
    /// i rodow bez zoldu (Straz, Wolni Ludzie) - po nieudanej zwyklej drodze RbnOf pyta ClanBudget.NeedChestAllOf (dluznik - G i R jak W5c; bez zoldu - R z prawdziwych kosztow);
    /// ten sam wiersz pamieci doby czytaja renty f7, gotowka W5 f20 i W5c (NeedOfClan - jedna regula). Wiersz zna rodzaj miary (Kind) - f20 i W5c dalej licza dluznika w
    /// swoim wierszu "dluznik" (te same f co dotad z McDebtMeasure). Ciecie tych rodow tez idzie do R miary (NoteNeedCut - sprzezenie D -> R -> f trzyma). Bez miary zostaja:
    /// rod w zajeciu, bez budzetu, najemnik, gracz bez pomiaru. Linia f6: pole "f23 miara wszystkich"; klucz wylaczony - jak dotad co do zlotowki.
    /// </summary>
    internal static partial class KingdomTreasury
    {
        private const int NbBelow = 0, NbMid = 1, NbAbove = 2, NbNone = 3;   // pasma: ponizej R, R-2R, ponad 2R, bez miary
        private sealed class NeedRow { public double F = 1, G, R; public int Band = NbNone; public long Full, Need; public bool Counted; public Clan C; public int Kind; }   // C - recenzja calosci 18.10 (ciecie do pierscienia miary); Kind - f23 (0 zwykla miara, 1 bez zoldu, 2 dluznik)
        private static readonly Dictionary<Clan, NeedRow> _rbn = new Dictionary<Clan, NeedRow>();
        private static int _rbnDay = int.MinValue;
        private static bool _rbnApply, _rbnMeasure;            // klucz czynny przy ostatnim kroku / pomiar mozliwy (budzet 166 wlaczony)
        private static bool _rbnAll;                           // f23-korona: klucz Need Measure All tej doby (dluznicy i rody bez zoldu mierzone wlasnym zlotem)
        private static readonly int[] _rbnKN = new int[3];      // f23: rody zmierzone NeedChestAllOf (1 bez zoldu, 2 dluznik) z pelnym zwrotem > 0
        private static readonly long[] _rbnKFull = new long[3], _rbnKNeed = new long[3];
        private static readonly int[,] _rbnKB = new int[3, 3];   // f23: [rodzaj, pasmo 0..2]
        private static readonly long[] _rbnFull = new long[4], _rbnNeed = new long[4];
        private static readonly int[] _rbnN = new int[4];
        private static readonly double[] _rbnG = new double[4];
        private static long _rbnRichPaid; private static int _rbnRichK;   // krolestwa w wojnie bez naleznego zwrotu (same rody ponad 2R) - ich zold
        private static int _rbnStumbles, _rbnStumblesSess; private static bool _rbnErr;
        private static int _rbnPlayerBand = -1;                 // pasmo gracza przy ostatnim kroku (komunikat tylko przy zmianie)

        internal static bool RefundByNeedOn { get { var s = Settings.Current; return s != null && s.CrownRefundByNeed && ClanBudget.On; } }

        /// <summary>f23-korona: klucz Need Measure All (z miara potrzeby f6 albo f7 - inaczej nikt nie czyta pasma).</summary>
        internal static bool NeedMeasureAllOn { get { var s = Settings.Current; return s != null && s.NeedMeasureAll && (s.CrownRefundByNeed || s.CrownRentsByNeed) && ClanBudget.On; } }

        /// <summary>Reset (KingdomTreasury.Reset - nowa kampania i kazde wczytanie): stan doby, potkniecia sesji i pasmo gracza.</summary>
        private static void RbnReset() { _rbn.Clear(); _rbnDay = int.MinValue; RbnZero(); _rbnStumblesSess = 0; _rbnErr = false; _rbnPlayerBand = -1; }

        private static void RbnZero()
        {
            for (int i = 0; i < 4; i++) { _rbnFull[i] = 0; _rbnNeed[i] = 0; _rbnN[i] = 0; _rbnG[i] = 0; }
            _rbnRichPaid = 0; _rbnRichK = 0; _rbnStumbles = 0;
            for (int i = 0; i < 3; i++) { _rbnKN[i] = 0; _rbnKFull[i] = 0; _rbnKNeed[i] = 0; for (int j = 0; j < 3; j++) _rbnKB[i, j] = 0; }   // f23
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
            try { _rbnAll = NeedMeasureAllOn; } catch { _rbnAll = false; }   // f23-korona: raz na dobe (zwrot, renty, f20 i W5c tej doby - ten sam wiersz)
        }

        /// <summary>Mnoznik f i pasmo rodu - raz na krok (pamiec doby). Nie rzuca (wyjatek - pelny zwrot, potkniecie).</summary>
        private static NeedRow RbnOf(Clan c)
        {
            NeedRow n;
            if (c == null) return null;
            if (_rbn.TryGetValue(c, out n)) return n;
            n = new NeedRow { C = c };
            _rbn[c] = n;
            if (!_rbnMeasure) return n;
            try
            {
                double G, R; bool war;
                // recenzja calosci 18.10: R miary potrzeby (NeedChestOf - z D z cieciami f6/f7 dodanymi z powrotem; klucz Need Reserve Uncut wylaczony = ChestOf)
                // f19-e3: pierwsza doba sesji tak samo (PendingNeedChestOf - R przez NeedReserve z D z zapisu; klucz Need Reserve Uncut Saved wylaczony = PendingChestOf)
                bool ok = ClanBudget.NeedChestOf(c, out G, out R, out war) || ClanBudget.PendingNeedChestOf(c, out G, out R, out war);
                // recenzja f6: rod gracza dluznik albo w zajeciu (168, z Player Same Ladder) - bez miary jak AI (ChestOf: b.Debtor / b.Seized) i W6b gracza
                if (!ok && c == Clan.PlayerClan && !(DebtLadder.On && (DebtLadder.IsDebtor(c) || DebtLadder.IsSeized(c))))
                {
                    double capW5, capW6, d;
                    ok = ClanBudget.PlayerCaps(c, out capW5, out capW6, out R, out d);   // W6b: ten sam wzor rezerwy co AI (gracz bez budzetu z wczoraj)
                    if (ok) { G = ClanIncomeBook.FamilyGoldOf(c); R = ClanBudget.NeedReserve(c, R, d); }   // recenzja calosci 18.10: R miary potrzeby jak AI
                }
                // f23-korona (Need Measure All): dluznik 168 i rod bez zoldu (Straz, Wolni Ludzie) - wlasnym zlotem (dluznik jak W5c, bez zoldu - R z prawdziwych kosztow); rod gracza tak samo
                if (!ok && _rbnAll)
                {
                    int kind;
                    ok = ClanBudget.NeedChestAllOf(c, out G, out R, out kind);
                    if (ok) n.Kind = kind;
                }
                if (ok && R > 0 && !double.IsNaN(G) && !double.IsInfinity(G))
                {
                    n.G = G; n.R = R;
                    double f; n.Band = NeedBand(G, R, out f); n.F = f;   // f7-renty: wzor w jednym miejscu (NeedBand) - ten sam dla zwrotu i rent
                }
            }
            catch (Exception e) { n.F = 1; n.Band = NbNone; RbnStumble("Of", e); }
            return n;
        }

        /// <summary>f7-renty: JEDNA regula mnoznika potrzeby (zwrot f6 i renty f7): f = clamp((2R - G) / R, 0, 1); pasmo 0 ponizej R (f = 1), 1 R-2R (liniowo),
        /// 2 ponad 2R (f = 0). R > 0 (wolajacy sprawdza). Te same liczby co dotad w RbnOf co do bitu.</summary>
        internal static int NeedBand(double G, double R, out double f)
        {
            f = Math.Max(0.0, Math.Min(1.0, (2.0 * R - G) / R));
            int band = G <= R ? NbBelow : (G >= 2.0 * R ? NbAbove : NbMid);
            if (band == NbAbove) f = 0;
            else if (band == NbBelow) f = 1;
            return band;
        }

        /// <summary>f7-renty (CrownRents.Need.cs): mnoznik f, G i R rodu z TEJ SAMEJ pamieci doby co zwrot zoldu (RbnOf - budzet 166 z wczoraj: ChestOf / PendingChestOf,
        /// te same wykluczenia: bez budzetu, dluznik i zajecie 168, Straz bez zoldu, najemnik; gracz w wojnie z PlayerCaps). Krok rent biegnie po zwrocie, wiec rod
        /// policzony przy zwrocie ma te same liczby. Doba bez kroku zwrotu (Crown Wage Refund wylaczony) - pamiec doby od zera tutaj (liczniki f6 dzis i tak bez linii).
        /// Zwraca pasmo: 0 ponizej R, 1 R-2R, 2 ponad 2R, 3 bez miary (f = 1). Nie rzuca.</summary>
        internal static int NeedOfClan(Clan c, out double f, out double G, out double R)
        {
            int kind;
            return NeedOfClan(c, out f, out G, out R, out kind);
        }

        /// <summary>f23-korona: NeedOfClan z rodzajem miary wiersza - 0 zwykla, ClanBudget.NkZero (bez zoldu, R z kosztow), ClanBudget.NkDebt (dluznik 168 - G i R jak W5c).
        /// f20 (AnMeasure) i W5c licza dluznika dalej w swoim wierszu "dluznik" (te same f, co dotad z McDebtMeasure). Nie rzuca.</summary>
        internal static int NeedOfClan(Clan c, out double f, out double G, out double R, out int kind)
        {
            f = 1; G = 0; R = 0; kind = 0;
            try
            {
                if (c == null || Campaign.Current == null) return NbNone;
                if (_rbnDay != (int)CampaignTime.Now.ToDays) RbnBegin();
                var n = RbnOf(c);
                if (n == null) return NbNone;
                f = n.F; G = n.G; R = n.R; kind = n.Band != NbNone ? n.Kind : 0;
                return n.Band;
            }
            catch (Exception e) { RbnStumble("NeedOfClan", e); f = 1; G = 0; R = 0; kind = 0; return NbNone; }
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
                    if (_rbnApply && n != null && n.C != null && need < f) ClanIncomeBook.NoteNeedCut(n.C, f - need);   // recenzja calosci 18.10: zatrzymane wedlug potrzeby - do miary R
                    if (n != null && f > 0)   // rod liczony w pasmie tylko z pelnym zwrotem > 0 (wiersz bez zoldu do zwrotu - poza licznikami i CSV)
                    {
                        n.Full += f; n.Need += need;
                        bool first = !n.Counted;
                        if (!n.Counted) { n.Counted = true; _rbnN[b]++; _rbnG[b] += Math.Max(0, n.G); }
                        if (n.Kind > 0 && n.Kind < 3 && b >= NbBelow && b <= NbAbove)   // f23: zmierzone wlasnym zlotem (dotad bez miary)
                        {
                            _rbnKFull[n.Kind] += f; _rbnKNeed[n.Kind] += need;
                            if (first) { _rbnKN[n.Kind]++; _rbnKB[n.Kind, b]++; }
                        }
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
                string[] name = { "ponizej R (pelny)", "R-2R (czesc, liniowo)", "ponad 2R (nic)",
                                  _rbnAll ? "bez miary (pelny: bez budzetu, zajecie, najemnik, gracz bez pomiaru)" : "bez miary (pelny: bez budzetu, dluznik, zajecie, Straz bez zoldu, gracz bez pomiaru)" };
                string[] shortName = { "ponizej R", "R-2R", "ponad 2R", "bez miary" };
                var sb = new StringBuilder(900);
                sb.Append("Korona: zwrot wedlug potrzeby (f6): dzien ").Append(_rbnDay).Append(" | Crown Refund By Need ")
                  .Append(_rbnApply ? "tak (czynny)" : (_rbnMeasure ? "NIE (na sucho - zwrot pelny)" : "NIE (budzet 166 wylaczony - zwrot pelny, bez pomiaru)"))
                  .Append(" | f = (2R - G) / R w 0..1 (G - zloto rodziny, R - rezerwa wojny; budzet 166 z wczoraj), po cieciach W5/W7/K3, krolestwa w wojnie");
                for (int b = 0; b < 4; b++)
                    sb.Append(" | ").Append(name[b]).Append(": rodow ").Append(_rbnN[b]).Append(" (zloto rodzin ").Append((_rbnG[b] / 1e6).ToString("0.00", inv)).Append(" mln)")
                      .Append(", pelny ").Append(_rbnFull[b]).Append(", wedlug potrzeby ").Append(_rbnNeed[b]);
                sb.Append(NeedAllSeg(_rbnAll, _rbnKN, _rbnKFull, _rbnKNeed, _rbnKB, "pelny"));   // f23: " | f23 miara wszystkich (...)" - klucz wylaczony: pusty
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
                sb.Append(CrownRents.NeedOn   // f7-renty: renty 180 ta sama regula - czesc rodow bogatych zostaje w skarbcu (linia "Korona: renty wedlug potrzeby (f7)")
                    ? " | renty 180 wedlug potrzeby (f7, ta sama regula; roznica zwrotu zostaje w reszcie wplywow dnia - W5, M1, M3, dwor krola, potem renty wedlug lenn x f, reszta w skarbcu)"
                    : " | renty 180 bez zmian (roznica zwrotu zostaje w reszcie wplywow dnia - W5, M1, M3, dwor krola, potem renty wedlug lenn)")
                  .Append(WarReserveByNeedOn ? " | zapas wojny korony (184, W5b, K5, DS, L5b-1, korona2) z naleznego zwrotu wedlug potrzeby (f23, War Reserve By Need)"
                                             : " | zapas wojny korony (184, W5b, K5, DS, L5b-1, korona2) z pelnego zwrotu - jak przed f6")
                  .Append(" | potkniecia ").Append(_rbnStumbles).Append(" (sesja ").Append(_rbnStumblesSess).Append(").");
                Log.Info(sb.ToString());
                // komunikat gracza - tylko przy zmianie pasma (klucz czynny, rod gracza z naleznym dzis)
                // recenzja f7: przy czynnych rentach wedlug potrzeby (CrownRents.NeedOn) pasmo komunikatu ma martwa strefe (CrownRents.MsgBand - 5% R przy progach R
                // i 2R), a komunikat idzie do kroku rent tej doby (CrownRents.QueueWageMsg): to samo pasmo renty tego dnia - JEDEN wspolny komunikat zamiast dwoch
                // (w wojnie pasmo zwrotu i renty liczy ten sam wiersz RbnOf). Klucz f7 wylaczony - warunek, napisy i kolejnosc jak dotad co do znaku.
                if (_rbnApply && p != null && p.Counted && p.Band != NbNone)
                {
                    bool f7 = CrownRents.NeedOn;
                    int band = f7 ? CrownRents.MsgBand(_rbnPlayerBand, p.G, p.R, p.Band) : p.Band;
                    if (band != _rbnPlayerBand)
                    {
                        string msg = null;
                        if (band == NbAbove)
                            msg = "The crown no longer repays your wages: your house holds more than twice its war reserve and pays for its own war.";
                        else if (band == NbMid)
                            msg = "The crown now repays only part of your wages: your house holds more than its war reserve - the richer the house, the smaller the share, and nothing above twice the reserve.";
                        else if (_rbnPlayerBand == NbMid || _rbnPlayerBand == NbAbove)
                            msg = "The crown again repays your wages in full: your house's gold is below its war reserve.";
                        if (msg != null) { if (f7) CrownRents.QueueWageMsg(band, msg); else Log.Player(msg); }
                        _rbnPlayerBand = band;
                    }
                }
            }
            catch (Exception e) { RbnStumble("Line", e); }
        }

        /// <summary>f23-korona: pole linii f6 / f7 - rody zmierzone wlasnym zlotem dzieki Need Measure All (dotad "bez miary"): bez zoldu (Straz, Wolni Ludzie - R z kosztow) i dluznicy
        /// 168 - liczba rodow wedlug pasma, pelny i wedlug potrzeby (te kwoty SA w pasmach 0..2 linii). Bez slow "rodow N (zloto rodzin" - czytniki pasm licza 4 pasma. Klucz wylaczony - "".</summary>
        internal static string NeedAllSeg(bool on, int[] kn, long[] kf, long[] kd, int[,] kb, string fullWord)
        {
            try
            {
                if (!on || kn == null || kf == null || kd == null || kb == null) return "";
                var sb = new StringBuilder(320);
                sb.Append(" | f23 miara wszystkich (Need Measure All tak - dotad bez miary, teraz wlasnym zlotem; kwoty w pasmach wyzej): ");
                string[] nm = { "", "bez zoldu (Straz, Wolni Ludzie; R z kosztow: dwor, sprzet, budowy i zold w monecie)", "dluznicy 168 (G i R jak W5c)" };
                for (int k = 1; k <= 2; k++)
                {
                    if (k == 2) sb.Append("; ");
                    sb.Append(nm[k]).Append(' ').Append(kn[k]).Append(" rodow (ponizej R ").Append(kb[k, 0]).Append(", R-2R ").Append(kb[k, 1]).Append(", ponad 2R ").Append(kb[k, 2])
                      .Append("), ").Append(fullWord).Append(' ').Append(kf[k]).Append(", wedlug potrzeby ").Append(kd[k]);
                }
                return sb.ToString();
            }
            catch { return ""; }
        }

        /// <summary>f24-korona (NZR-5 i NDP-3 testu fin97): mnoznik potrzeby f w budzet-rodow.csv (zwrot f6, renty f7, gotowka f20, W5c) - TA SAMA liczba double, ktora mnozy
        /// kod ("G17" wraca zawsze do tego samego bitu; "R" w .NET Framework x64 nie zawsze), zamiast "0.0000". Dotad f 0,99994 (dluznik G 53 704, R 53 701) szlo do CSV jako
        /// 1.0000 - wiersz wygladal na "bez ciecia", choc kod slusznie zatrzymal 1 zl; a f 0,393862 jako 0,3939 dawalo w CSV pelny x f o 1 zl wiecej niz floor kodu.
        /// Kwoty, pasma i linie bez zmian - tylko zapis liczby. Czytniki (checker) biora float - "1", "0.5" i "0.39386194418848662" czytaja tak samo.</summary>
        internal static string NeedFCsv(double f) { return f.ToString("G17", CultureInfo.InvariantCulture); }

        /// <summary>Kolumny budzet-rodow.csv (za lup_na_zaloge_ww2 - czytniki po nazwach): krok zwrotu tej doby.</summary>
        internal const string RefundNeedCsvHeader = ";zwrot_f6_mnoznik;zwrot_f6_g;zwrot_f6_r;zwrot_f6_pasmo;zwrot_f6_pelny;zwrot_f6_wedlug_potrzeby";

        internal static string RefundNeedCsv(Clan c)
        {
            try
            {
                NeedRow n;
                if (c == null || Campaign.Current == null || _rbnDay != (int)CampaignTime.Now.ToDays || !_rbn.TryGetValue(c, out n) || n == null || !n.Counted) return ";;;;;;";
                var inv = CultureInfo.InvariantCulture;
                return ";" + NeedFCsv(n.F) + ";" + Math.Round(n.G).ToString("0", inv) + ";" + Math.Round(n.R).ToString("0", inv) + ";" + n.Band.ToString(inv)
                       + ";" + n.Full.ToString(inv) + ";" + n.Need.ToString(inv);
            }
            catch { return ";;;;;;"; }
        }
    }
}
