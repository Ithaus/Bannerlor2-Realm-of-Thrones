using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;

namespace Armoury
{
    /// <summary>
    /// f20-d1 (decyzja Jeffa 18.10, doslownie "1.b" na pytanie: "Czy bogate rody maja dostawac pomoc koronna do zoldu? ... b) ta sama miara co przy rentach: bogaty nic,
    /// sredni czesc, biedny calosc. Zloto zostaje w skarbcu na wojne.") - GOTOWKA DOPLATY W5 WEDLUG POTRZEBY RODU. Klucz CrownWarAidCashByNeed (domyslnie TAK;
    /// wylaczony = stan sprzed paczki co do monety - linia liczy wtedy skutek na sucho).
    ///
    /// Dowod (audyt gospodarki 18.10, soczewka 'pieniadz'; fin93 D:/Backup-Bannerlord/logi-testow/Armoury-2026-10-18_12-18-40.log, budzet-rodow.csv, ostatnie 28 dob
    /// 109209-109236): rody ponad 2R (pasmo zwrotu f6 = 2 - zwrot i renta 0) dostaja gotowke W5 33 664 zl/d (kolumna doplata_w5 to SUMA szczebli: w tym W5b 12 710,
    /// M1 1 749, W6 41 - audytowe "48,1 tys." liczylo W5b i M1 drugi raz); R-2R 69 183 zl/d (w tym W5b 18 420, M1 2 033). Na sucho z mnoznikow f6/f7 tych rodo-dob:
    /// zatrzymane ok. 72,8 tys. zl/d z 141 tys. zl/d gotowki szczebli z cieciem (ponad 2R ok. 33,6 tys., R-2R ok. 37,8 tys., dluznicy wedlug G i R ok. 1,4 tys.).
    ///
    /// REGULA: szczeble W5 licza jak dotad (potrzeba, garnki, poziomy wyrownania, przydzial w pulapie - bez zmian); GOTOWKA rodu na szczeblu daniny (W5), skarbca ponad
    /// zapas (W5b) i reszty wplywow dnia (M1) = floor(uprawnienie x f), f - ta sama miara potrzeby co zwrot f6, renty f7 i W5c (NeedOfClan: ta sama pamiec doby, G i R
    /// z wczorajszego budzetu 166, R miary bez ciec): ponizej R f = 1, R-2R f = (2R - G) / R, ponad 2R f = 0. Dluznik 168 ("bez miary" w f6/f7 - tam "bez ciecia") -
    /// jak W5c: pasmo i f z jego G i R (McDebtMeasure: NeedChestAnyOf, gracz PlCaps); rod bez G i R w ogole - bez ciecia (f = 1; to ciecie istniejacej wyplaty, jak f6/f7).
    ///  - CO ZOSTAJE: uprawnienie - wyplata zostaje W SKARBCU KROLESTWA: nie idzie do innych rodow tego szczebla (garnek schodzi o cale uprawnienie), nie podnosi poziomu
    ///    wyrownania, nie wraca w renty 180. Szczebel daniny i M1 sa z reszty wplywow dnia - zatrzymane jest z niej zdjete (CrownIncome.Spent bez przelewu, KDay.NeedAidKept,
    ///    linia 165), wiec M1, M3, E3-G5, dwor krola i renty 180 widza dokladnie te sama reszte wplywow co bez paczki; CrownRents.Daily odswieza pule rent, bo krok W5
    ///    zwraca kwote zdjeta z wplywow (gotowka + zatrzymane). W5b jest z zapasu (poza wplywami dnia) - zatrzymane po prostu nie wychodzi ze skarbca, a budzet W5c (1/30
    ///    nadwyzki minus pula W5b) jest bez zmian. Od jutra to zapas korony (Stock, 1/CrownReserveReleaseDays) - czytaja go W5b/W5c, K5, flaga 184, korona2, lup stolicy.
    ///  - MIEJSCE W PULAPIE (przydzial _naBoost -> b.Aid w ClanBudget.Daily) BEZ ZMIAN - to nie jest ukryty przelew zlota: podnosi tylko pulap partii i rodu tej doby (nie D:
    ///    dwor, sprzet, budowy, limit Banku go nie widza). Zold w tym pulapie rod placi z wlasnej kiesy poza czescia oplacona gotowka korony (b.AidCash = rzeczywista
    ///    gotowka, teraz mniejsza - dwor i niewydany sprzet ustepuja zoldowi, ktory rod naprawde placi sam). Ciecie przydzialu znaczyloby zwolnienia 166 u rodow, ktore maja
    ///    z czego placic (G > R), wbrew zasadzie "brak pieniedzy = pozyczka albo mniejsze wojsko" (bogaty ma pieniadze). Jedyne zloto zwiazane z tym zoldem: zwrot 70%
    ///    nastepnej doby x f6 - rod placi sam (1 - f) tego zoldu, a NeedAidRefundCut zdejmuje z podstawy tylko wyplacona gotowke; ponad 2R f6 = 0 (nic), R-2R f6 = f -
    ///    korona za tego samego czlowieka najwyzej f + 0.7 f (1 - f) <= 1 ("1 zl = 1 zl" trzyma). Pozyczki wojenne (KW, od ludu, miejska) tylko przy G < R, czyli f = 1.
    ///  - JEDNO UPRAWNIENIE NA DOBE (bez podwojnego ciecia i bez dopelniania): kazdy szczebel liczy swoj plaster od PELNEGO uprawnienia rodu tej doby (AnBase = _an.Base:
    ///    danina + W5b + W6 + M1 + W5c tak, jak bez paczki), nie od wyplaconej gotowki. Dlatego M1 (c1 - juz dane), W5c (f x (c1 - juz dane)) i W7 (pobor pana c7 - gotowka W5)
    ///    nie dopelniaja czesci zatrzymanej (W7 nie bierze od ludu tego, co zatrzymala korona), a f dziala raz na kazdy plaster: razem rod dostaje f x gotowka przy ostatnim
    ///    poziomie (f20 na W5/W5b/M1, wlasne f W5c - ta sama miara). Podloga przydzialu (nb >= juz dane) i "bez gotowki z daniny" w W6 - tez od uprawnienia: przydzial i
    ///    linia W6 co do zlotowki jak bez paczki. Klucz wylaczony - baza = _naPaid (kod jak dotad).
    ///  - NIE TNIE: W6 (tarczowe - przelew miedzy rodami tej samej doby, pobor = wyplata; poza decyzja), W5c (wlasne f, ta sama miara), W7 (pobor pana z wlasnych lenn).
    ///    Potrzeba W5, garnek, UncovPre (W5b regula 1 - danina z ludu, rezerwa wezwan M2b) i DsW5Need (nadanie DS) - bez zmian (licza pelna potrzebe - CHANGELOG).
    ///  - GRACZ: ta sama regula, gdy W5 go mierzy (W6b); komunikat (po angielsku) przy zmianie pasma z martwa strefa 5% R (CrownRents.MsgBand), tylko w dobie z
    ///    uprawnieniem; bez zapisu - po wczytaniu pierwszy komunikat pasma R-2R / ponad 2R wraca (raz).
    /// Platnik -> odbiorca: jak W5 (Kingdom.KingdomBudgetWallet -> kiesa glowy rodu), tylko mniej; nic nie powstaje i nic nie znika. Linia "Doplata wedlug potrzeby rodu
    /// (f20): dzien N" (z NeedAidDay, po linii W5): pasma (rodow, pelna, wedlug potrzeby, wyplacone, zatrzymane), zatrzymane wedlug szczebli, skarbce w krokach W5 (przed -
    /// po = wyplacone + tarczowe netto - dowod, ze zatrzymane zostalo w skarbcu), kontrole, gracz. Linia W5: "reszta do rent" po zatrzymanym i dopiski "f20" (klucz
    /// wylaczony - co do znaku jak dotad). Kolumny budzet-rodow.csv: doplata_w5_f20_mnoznik, _pasmo, _pelna, _wyplacona, _zatrzymana.
    /// Zapis gry: nic nowego (pamiec doby). Wyjatek - licznik potkniec (pierwszy do pliku), bez globalnych wylacznikow (CLAUDE.md 7).
    /// </summary>
    internal static partial class KingdomTreasury
    {
        private const int AnTierD = 0, AnTierB = 1, AnTierM = 2, AnTierX = 3;   // szczebel daniny, W5b, M1 (z cieciem f20); W5c (bez ciecia - tylko do uprawnienia)
        private const int AnTierW6 = 4;   // recenzja calosci noc-ks4: W6 (tarczowe) - bez ciecia jak AnTierX, ale liczone osobno (linia f20: ile W6 poszlo do rodow ponad 2R)
        private const int AnLines = 5;   // wiersze linii: 0 ponizej R, 1 R-2R, 2 ponad 2R, 3 dluznik 168 (f z jego G i R), 4 bez miary (bez ciecia)

        private sealed class AnRow
        {
            public double F = 1, G, R; public int Band = NbNone, Line = 4; public bool Debt, Pl;
            // Base - uprawnienie rodu ze WSZYSTKICH szczebli tej doby (baza planu nastepnych szczebli i W7, podloga przydzialu); Full / Need / Paid - szczeble z cieciem
            // (danina, W5b, M1): pelne uprawnienie, czesc wedlug potrzeby (floor(e x f) na szczebel), gotowka naprawde wyplacona
            public long Base, Full, Need, Paid;
            public long W6;   // recenzja calosci noc-ks4: gotowka W6 (tarczowe) tej doby - bez ciecia f20, tylko do linii (decyzja Jeffa, czy tarczowe to "pomoc koronna")
        }

        private static readonly Dictionary<Clan, AnRow> _an = new Dictionary<Clan, AnRow>();   // pamiec doby kroku W5 (czyszczona z NaRoll)
        private static readonly long[] _anKeptT = new long[3];                                 // zatrzymane tej doby na szczebel: danina, W5b, M1
        private static readonly HashSet<string> _anErr = new HashSet<string>();
        private static bool _anApply;                 // klucz czynny przy kroku tej doby (ustawiany raz na dobe w NaRoll - W7 tej samej doby czyta te sama baze)
        private static int _anStumbles, _anPlBand = -1;
        private static string _anPlMsg;               // komunikat gracza tej doby (pole linii)

        internal static bool AidCashByNeedOn { get { var s = Settings.Current; return s != null && s.CrownWarAidCashByNeed && NeedAidOn; } }

        /// <summary>Reset (NeedAidReset - nowa kampania i kazde wczytanie): pamiec doby, potkniecia i pasmo gracza.</summary>
        private static void AidNeedReset()
        {
            _an.Clear(); _anErr.Clear(); _anApply = false; _anStumbles = 0; _anPlBand = -1; _anPlMsg = null;
            for (int i = 0; i < _anKeptT.Length; i++) _anKeptT[i] = 0;
        }

        /// <summary>Nowa doba kroku W5 (NaRoll): pamiec doby od zera, klucz tej doby.</summary>
        private static void AnClearDay()
        {
            _an.Clear(); _anPlMsg = null;
            for (int i = 0; i < _anKeptT.Length; i++) _anKeptT[i] = 0;
            try { _anApply = AidCashByNeedOn; } catch { _anApply = false; }
        }

        private static void AnStumble(string where, Exception e)
        {
            _anStumbles++;
            try { if (_anErr.Add(where)) Log.Error("KingdomTreasury.AidNeed." + where, e); } catch { }
        }

        private static AnRow AnRowOf(NaClan x)
        {
            if (x == null || x.C == null) return null;
            if (x.A != null) return x.A;
            AnRow a;
            if (!_an.TryGetValue(x.C, out a) || a == null) { a = new AnRow { Pl = x.Pl }; _an[x.C] = a; }
            x.A = a;
            return a;
        }

        /// <summary>Krok W5 (NeedAidKingdom), rod z potrzeba W5 (kandydat szczebli): pasmo i f - ta sama miara co zwrot f6, renty f7 i W5c (NeedOfClan); dluznik 168
        /// (NbNone) - pasmo i f z jego G i R jak W5c (McDebtMeasure); bez G i R - bez ciecia (f = 1). Liczone zawsze (klucz wylaczony - linia na sucho). Nie rzuca.</summary>
        private static void AnMeasure(NaClan x)
        {
            var a = AnRowOf(x);
            if (a == null) return;
            a.F = 1; a.Band = NbNone; a.Line = 4; a.Debt = false; a.G = 0; a.R = 0;
            try
            {
                double f, G, R;
                int band = NeedOfClan(x.C, out f, out G, out R);
                if (band >= NbBelow && band <= NbAbove)
                {
                    a.F = Math.Max(0.0, Math.Min(1.0, f)); a.Band = band; a.Line = band; a.G = G; a.R = R;
                    return;
                }
                if (McDebtMeasure(x.C, out G, out R))
                {
                    band = NeedBand(G, R, out f);
                    a.F = Math.Max(0.0, Math.Min(1.0, f)); a.Band = band; a.Line = 3; a.Debt = true; a.G = G; a.R = R;
                }
            }
            catch (Exception e) { a.F = 1; a.Band = NbNone; a.Line = 4; a.Debt = false; AnStumble("Measure", e); }
        }

        /// <summary>Gotowka do wyplaty z uprawnienia e na szczeblu z cieciem: klucz czynny - floor(e x f), inaczej e (co do zlotowki jak dotad). Nie rzuca.</summary>
        private static long AnPay(NaClan x, long e)
        {
            if (e <= 0) return 0;
            try
            {
                if (!_anApply || x == null || x.A == null || x.A.F >= 1.0) return e;
                long p = (long)Math.Floor(e * x.A.F);
                return p < 0 ? 0 : (p > e ? e : p);
            }
            catch (Exception ex) { AnStumble("Pay", ex); return e; }
        }

        /// <summary>Po szczeblu (przelew juz udany albo bez przelewu przy f = 0): uprawnienie e i gotowka p rodu do pamieci doby. tier AnTierD / B / M - szczeble z cieciem
        /// (pelna, wedlug potrzeby, wyplacone, zatrzymane na szczebel); AnTierX - W6 i W5c (tylko uprawnienie - baza nastepnych szczebli). Zwraca zatrzymane (e - p). Nie rzuca.</summary>
        private static long AnNote(NaClan x, long e, long p, int tier)
        {
            try
            {
                if (e <= 0) return 0;
                var a = AnRowOf(x);
                if (a == null) return 0;
                if (p < 0) p = 0;
                if (p > e) p = e;
                a.Base += e;
                if (tier == AnTierW6) { a.W6 += e; return 0; }   // recenzja calosci noc-ks4: W6 jak AnTierX (uprawnienie bez ciecia) + licznik linii
                if (tier < AnTierD || tier > AnTierM) return 0;
                a.Full += e; a.Paid += p;
                a.Need += a.F >= 1.0 ? e : (long)Math.Floor(e * a.F);
                long kept = e - p;
                _anKeptT[tier] += kept;
                return kept;
            }
            catch (Exception ex) { AnStumble("Note", ex); return 0; }
        }

        /// <summary>"Co rod juz dostal tej doby" dla planu szczebla, podlogi przydzialu i W7: klucz czynny - pelne uprawnienie (_an.Base, wszystkie szczeble), inaczej
        /// rzeczywista gotowka (_naPaid - jak dotad). Nie rzuca.</summary>
        private static long AnBase(Clan c)
        {
            try
            {
                long v = 0;
                if (c == null) return 0;
                if (_anApply) { AnRow a; if (_an.TryGetValue(c, out a) && a != null) v = a.Base; }
                else _naPaid.TryGetValue(c, out v);
                return v > 0 ? v : 0;
            }
            catch (Exception e) { AnStumble("Base", e); long v; _naPaid.TryGetValue(c, out v); return v; }
        }

        /// <summary>A16 "W5 miejsce" (A16Signals.W5Line): gotowka W5 rodu tej doby bez ciecia f20 (pelne uprawnienie wszystkich szczebli) - miejsce na werbunek = przydzial
        /// ponad nia, jak przed paczka (czesc zatrzymana to zold, ktory rod placi sam, nie miejsce na nowych ludzi). Klucz wylaczony - NeedAidCashOf. 0 - krok dzis nie biegl.</summary>
        internal static long NeedAidFullCashOf(Clan c)
        {
            try
            {
                if (c == null || Campaign.Current == null || _naDay != (int)CampaignTime.Now.ToDays) return 0;
                if (!_anApply) return NeedAidCashOf(c);
                AnRow a;
                return _an.TryGetValue(c, out a) && a != null && a.Base > 0 ? a.Base : 0;
            }
            catch { return 0; }
        }

        /// <summary>Linia W5, wiersz krolestwa (na koncu - po dopisku M1): zatrzymane f20 krolestwa; klucz wylaczony i 0 - pusty (wiersz co do znaku jak dotad).</summary>
        private static string AnKingdomSeg(NaK r)
        {
            if (r == null || (!_anApply && r.Kept <= 0 && r.CrownKept <= 0 && r.RentKept <= 0)) return "";
            return ", zatrzymane f20 " + r.Kept.ToString(WtInv) + " (W5b " + r.CrownKept.ToString(WtInv) + ", M1 " + r.RentKept.ToString(WtInv) + ")";
        }

        /// <summary>Kolumny budzet-rodow.csv (ostatnie - czytniki po nazwach): mnoznik f, wiersz linii (0 ponizej R, 1 R-2R, 2 ponad 2R, 3 dluznik 168, 4 bez miary), pelne
        /// uprawnienie, wyplacona i zatrzymana gotowka szczebli z cieciem (danina, W5b, M1) tej doby. Rod bez uprawnienia - puste.</summary>
        internal const string AidNeedCsvHeader = ";doplata_w5_f20_mnoznik;doplata_w5_f20_pasmo;doplata_w5_f20_pelna;doplata_w5_f20_wyplacona;doplata_w5_f20_zatrzymana";

        internal static string AidNeedCsv(Clan c)
        {
            try
            {
                AnRow a;
                if (c == null || Campaign.Current == null || _naDay != (int)CampaignTime.Now.ToDays || !_an.TryGetValue(c, out a) || a == null || a.Full <= 0) return ";;;;;";
                return ";" + a.F.ToString("0.0000", WtInv) + ";" + a.Line.ToString(WtInv) + ";" + a.Full.ToString(WtInv) + ";" + a.Paid.ToString(WtInv) + ";" + (a.Full - a.Paid).ToString(WtInv);
            }
            catch { return ";;;;;"; }
        }

        /// <summary>Komunikat gracza przy zmianie pasma (klucz czynny, uprawnienie dzis, rod gracza zmierzony). Nie rzuca.</summary>
        private static void AnPlayer()
        {
            try
            {
                var pc = Clan.PlayerClan;
                AnRow p;
                if (!_anApply || pc == null || !_an.TryGetValue(pc, out p) || p == null || p.Full <= 0 || p.Line == 4 || p.Band < NbBelow || p.Band > NbAbove) return;
                int band = CrownRents.MsgBand(_anPlBand, p.G, p.R, p.Band);   // martwa strefa 5% R (jak komunikaty f6/f7); zloto liczy f bez strefy
                if (band == _anPlBand) return;
                string msg = null;
                if (band == NbAbove)
                    msg = "The crown no longer pays war aid toward your companies' wages: your house holds more than twice its war reserve and pays its soldiers from its own gold. The coin stays in the realm's treasury for war.";
                else if (band == NbMid)
                    msg = "The crown now pays only part of its war aid toward your companies' wages: your house holds more than its war reserve - the richer the house, the smaller the share, and nothing above twice the reserve. The rest stays in the realm's treasury for war.";
                else if (_anPlBand == NbMid || _anPlBand == NbAbove)
                    msg = "The crown again pays its full war aid toward your companies' wages: your house's gold is below its war reserve.";
                if (msg != null) { Log.Player(msg); _anPlMsg = band == NbAbove ? "ponad 2R" : (band == NbMid ? "R-2R" : "ponizej R (znow calosc)"); }
                _anPlBand = band;
            }
            catch (Exception e) { AnStumble("Player", e); }
        }

        // ------------------------------------------------------------ doba: linia "Doplata wedlug potrzeby rodu (f20)" - z NeedAidDay, zaraz po linii W5
        private static void AidNeedDay(Settings s, int today)
        {
            try
            {
                AnPlayer();   // komunikat przed linia - wyjatek w linii go nie gubi; takze bez logu
                if (s == null || !s.LogEnabled) return;
                var n = new int[AnLines]; var full = new long[AnLines]; var need = new long[AnLines]; var paid = new long[AnLines];
                var debtB = new int[3];
                int richPaidN = 0, offNeed = 0; long richPaid = 0;
                int w6N = 0, w6RichN = 0, w6MidN = 0; long w6Sum = 0, w6Rich = 0, w6Mid = 0;   // recenzja calosci noc-ks4: W6 (tarczowe, bez ciecia) wedlug pasma rodu
                foreach (var kv in _an)
                {
                    var a = kv.Value;
                    if (kv.Key != null && a != null && a.W6 > 0)
                    {
                        w6N++; w6Sum += a.W6;
                        if (a.Band == NbAbove) { w6RichN++; w6Rich += a.W6; } else if (a.Band == NbMid) { w6MidN++; w6Mid += a.W6; }
                    }
                    if (kv.Key == null || a == null || a.Full <= 0) continue;
                    int l = a.Line < 0 || a.Line >= AnLines ? 4 : a.Line;
                    n[l]++; full[l] += a.Full; need[l] += a.Need; paid[l] += a.Paid;
                    if (l == 3 && a.Band >= NbBelow && a.Band <= NbAbove) debtB[a.Band]++;
                    if (a.Band == NbAbove && a.Paid > 0 && _anApply) { richPaidN++; richPaid += a.Paid; }   // kontrola: ponad 2R (takze dluznik ponad 2R) nic
                    if (_anApply ? a.Paid != a.Need : a.Paid != a.Full) offNeed++;                     // kontrola: wyplacone = wedlug potrzeby (na sucho - pelna)
                }
                long fT = 0, nT = 0, pT = 0;
                for (int i = 0; i < AnLines; i++) { fT += full[i]; nT += need[i]; pT += paid[i]; }
                // wiersze krolestw: gotowka i zatrzymane szczebli (te same liczby co linia W5) oraz skarbce w krokach W5
                long rPaidD = 0, rPaidB = 0, rPaidM = 0, rKeptD = 0, rKeptB = 0, rKeptM = 0, w0 = 0, w1 = 0, outT = 0, scNet = 0; int kW = 0, kBad = 0, kNo = 0;
                for (int i = 0; i < _naRows.Count; i++)
                {
                    var r = _naRows[i];
                    rPaidD += r.Paid; rPaidB += r.CrownPaid; rPaidM += r.RentPaid; rKeptD += r.Kept; rKeptB += r.CrownKept; rKeptM += r.RentKept;
                    if (r.AnW0 < 0 || r.AnW1 < 0) { kNo++; continue; }
                    kW++;
                    long o = r.Paid + r.CrownPaid + r.RentPaid + r.ScNet;
                    w0 += r.AnW0; w1 += r.AnW1; outT += o; scNet += r.ScNet;
                    if (r.AnW0 - r.AnW1 != o) kBad++;
                }
                long kT = rKeptD + rKeptB + rKeptM;
                int keptOff = (rKeptD != _anKeptT[AnTierD] || rKeptB != _anKeptT[AnTierB] || rKeptM != _anKeptT[AnTierM] || kT != fT - pT) ? 1 : 0;
                int paidOff = (rPaidD + rPaidB + rPaidM != pT) ? 1 : 0;
                string[] nm = { "ponizej R (f = 1)", "R-2R (f = (2R - G) / R)", "ponad 2R (nic)", "dluznik 168 (f z jego G i R, jak W5c)", "bez miary (bez ciecia, f = 1)" };
                var sb = new StringBuilder(1800);
                sb.Append("Doplata wedlug potrzeby rodu (f20): dzien ").Append(today)
                  .Append(" | klucz Crown War Aid Cash By Need ").Append(_anApply ? "tak (czynny)" : (s.CrownWarAidCashByNeed ? "tak, ale W5 dzis nieczynne - 0" : "NIE (na sucho - gotowka pelna, jak przed paczka)"))
                  .Append(" | regula: gotowka szczebla daniny (W5), skarbca ponad zapas (W5b) i reszty wplywow dnia (M1) = uprawnienie x f, f = (2R - G) / R w 0..1 - ta sama miara i pamiec doby")
                  .Append(" co zwrot f6, renty f7 i W5c; dluznik 168 - f z jego G i R, bez G i R - bez ciecia; miejsce w pulapie (przydzial W5) bez zmian - reszte zoldu rod placi sam;")
                  .Append(" W6 i W5c bez tego ciecia; zatrzymane zostaje w skarbcu (nie do innych rodow, nie w renty)");
                for (int b = 0; b < AnLines; b++)
                {
                    sb.Append(" | ").Append(nm[b]).Append(": rodow ").Append(n[b]);
                    if (b == 3) sb.Append(" (ponizej R ").Append(debtB[0]).Append(", R-2R ").Append(debtB[1]).Append(", ponad 2R ").Append(debtB[2]).Append(')');
                    sb.Append(", pelna ").Append(full[b]).Append(", wedlug potrzeby ").Append(need[b]).Append(", wyplacone ").Append(paid[b]).Append(", zatrzymane ").Append(full[b] - paid[b]);
                }
                sb.Append(" | razem pelna ").Append(fT).Append(", wedlug potrzeby ").Append(nT).Append(", wyplacone ").Append(pT)
                  .Append(" (szczebel daniny ").Append(rPaidD).Append(", W5b ").Append(rPaidB).Append(", M1 ").Append(rPaidM).Append(')')
                  .Append(", zatrzymane ").Append(kT).Append(" (szczebel daniny ").Append(rKeptD).Append(" i M1 ").Append(rKeptM)
                  .Append(" - z reszty wplywow dnia, zdjete z wplywow: nie ida w renty; W5b ").Append(rKeptB).Append(" - zostaje w skarbcu ponad zapas)")
                  .Append(_anApply ? "" : "; na sucho zatrzymane byloby " + (fT - nT).ToString(WtInv))
                  .Append(" | skarbce w krokach W5 (danina, W5b, W6, M1; krolestw ").Append(kW).Append("): przed ").Append(w0).Append(", po ").Append(w1).Append(", ubylo ").Append(w0 - w1)
                  .Append(" = wyplacono W5 + W5b + M1 + tarczowe netto ").Append(outT).Append(" (tarczowe wyplacone - pobrane ").Append(scNet).Append("); rozjazd krolestw ").Append(kBad)
                  .Append(", bez pomiaru ").Append(kNo)
                  .Append(" | kontrola: ponad 2R wyplacone ").Append(richPaidN).Append(" rodow (").Append(richPaid).Append(" zl), wyplacone inne niz wedlug potrzeby ").Append(offNeed)
                  .Append(" rodow, sumy rodow inne niz krolestw (wyplacone ").Append(paidOff).Append(", zatrzymane ").Append(keptOff).Append(") (oczekiwane 0)")
                  // recenzja calosci noc-ks4: W6 jest poza decyzja 1.b (bez ciecia) - ile poszlo do rodow ponad 2R i R-2R (do decyzji Jeffa, czy tarczowe to "pomoc koronna")
                  .Append(" | W6 tarczowe (bez ciecia f20 - poza decyzja 1.b): wyplacone ").Append(w6Sum).Append(" zl do ").Append(w6N).Append(" rodow, w tym ponad 2R ").Append(w6Rich)
                  .Append(" zl do ").Append(w6RichN).Append(" rodow, R-2R ").Append(w6Mid).Append(" zl do ").Append(w6MidN).Append(" rodow");
                AnRow p; var pc = Clan.PlayerClan;
                if (pc != null && _an.TryGetValue(pc, out p) && p != null && p.Full > 0)
                    sb.Append(" | gracz: ").Append(nm[p.Line < 0 || p.Line >= AnLines ? 4 : p.Line]).Append(", f ").Append(p.F.ToString("0.00", WtInv))
                      .Append(", G ").Append(Math.Round(p.G).ToString("0", WtInv)).Append(", R ").Append(Math.Round(p.R).ToString("0", WtInv))
                      .Append(", pelna ").Append(p.Full).Append(", wyplacone ").Append(p.Paid).Append(", zatrzymane ").Append(p.Full - p.Paid);
                else sb.Append(" | gracz: bez uprawnienia W5 dzis");
                sb.Append(", komunikat: ").Append(_anPlMsg ?? "brak")
                  .Append(" | potkniecia ").Append(_anStumbles).Append('.');
                Log.Info(sb.ToString());
            }
            catch (Exception e) { AnStumble("Day", e); }
            finally { _anStumbles = 0; }
        }
    }
}
