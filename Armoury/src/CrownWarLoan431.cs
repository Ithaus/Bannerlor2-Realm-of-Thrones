using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>
    /// 431 - ZOLD KROLEWSKI W WOJNIE WEDLUG POTRZEBY RODU I JAKO POZYCZKA KORONY (audyt ekonomii 11.11 poz. B4; decyzja Jeffa 4, doslownie: "tak dopplaca
    /// tylko gdy rody sa w potrzebie i to nie za darmo moze pozyczka na malenki procent?"). Zmienia regule 314c z 01.11 (dar p x zold kazdemu rodowi w armii
    /// po 40 dniach sluzby, takze siedzacemu na milionach) - na wyrazna decyzje Jeffa. Klucz RoyalWarWageLoan (domyslnie TAK); NIE = 314c co do zlotowki jak dotad.
    /// Czynny tylko z zoldem 314c (RoyalWarWageEnabled), drabina dlugu 168 (DebtLadder.On - ksiega) i budzetem 166 (miara potrzeby).
    ///
    /// REGULA (CrownWar314.PayWages wola Want przed podzialem skarbca i Lent po przelewie):
    ///  - pelny = to, co 314c placilo dotad (p x zold szeregowych w armii krolestwa po dniach darmowych, minus gotowka pana 277);
    ///  - wedlug potrzeby = floor(pelny x f), f = clamp((2R - G) / R, 0, 1) - TA SAMA miara i pamiec doby co zwrot f6, renty f7 "Crown Rents By Need", W5c
    ///    (KingdomTreasury.NeedOfClan): rod ponad 2R - nic, R-2R - czesc, ponizej R - calosc; bez miary - calosc (jak renty);
    ///  - LIMIT: dlug rodu z pozyczek koron najwyzej RoyalWarWageLoanLimitDays x D (D rodu z drabiny 168 - DebtLadder.DebtD); ponad limit korona nie pozycza;
    ///  - rod w zaleglosci, w zajeciu albo w czarnej ksiedze Banku (DebtLadder.FolkLoanBlocked - ta sama regula co pozyczki ludu i rad) - bez pozyczki;
    ///  - to, co skarbiec wyplaci (reszta wplywow dnia ponad garnek W5, potem ponad zapas wojny - bez zmian, CrownWar314.PurseOf / Pay), jest POZYCZKA:
    ///    roszczenie KCrownLoan w ksiedze drabiny 168 (nie drugi bank - ta sama ksiega co dlug zoldu, okupy i pozyczki rad), wierzyciel = korona.
    ///  - CENA: RoyalWarWageLoanRate rocznie (3%; rok kalendarza gry), odsetki tylko zapisem dlugu (jak Bank i rady).
    ///  - SPLATA (istniejace reguly drabiny): w wojnie z D nic (odsetki narastaja - sama pozyczka nie robi zaleglosci w wojnie); rod AI oddaje z jednorazowych
    ///    wplywow (lup, okupy, dar krola) 50% w wojnie i 100% w pokoju - po Banku i radach; po wojnie rata z D = narosle odsetki albo 1/RoyalWarWageLoanRepayDays
    ///    (728) dlugu z dnia ostatniej pozyczki (wieksze z dwojga), w limicie AllInstalmentsMaxShare (15% D) razem z innymi wierzycielami. Niezaplacona rata = zaleglosc, trzecia = zajecie dochodu wsi (D2 / D3 / D4 drabiny, bez nowych regul). Zloto raty:
    ///    kiesa glowy -> skarbiec korony-wierzyciela (nie ma jej - korona, ktorej rod sluzy; inaczej zapasowa kasa miasta).
    ///  - Sama pozyczka korony NIE robi z rodu "dluznika 166" (jak pozyczka rady): miara potrzeby dalej z G i R.
    ///  - Rodzina W5 bez zmian: pozyczka tej doby to gotowka na tych samych ludzi (CrownWar314.PaidToday - pulap W5, b.Aid budzetu, ciecie zwrotu 70% jutro).
    ///  - GRACZ-WASAL: te same zasady; przy pierwszej pozyczce w roku (po dniach darmowych) okno wyboru - przyjac albo odmowic (klucz RoyalWarWageLoanAskPlayer;
    ///    NIE = gracz bierze jak AI). Odpowiedz obowiazuje do konca roku kalendarzowego u tej korony (zapis); bez odpowiedzi - bez pozyczki. Gracz poza drabina
    ///    (PlayerSameLadder wylaczone) - bez pozyczki (nie ma ksiegi, ktora by ja sciagnela).
    /// Zloto nie powstaje: skarbiec -> kiesa glowy (pozyczka), kiesa -> skarbiec (splata); czego rod nie dostal (bogaty, limit, odmowa) - zostaje w skarbcu.
    /// Linia doby "Pozyczka korony na zold (431)" z KONTROLA ksiegi: dlug dzis = dlug z poprzedniej linii + pozyczone + odsetki - splacone - bez platnika (roznica 0)
    /// i kontrola zlota (pozyczone w ksiedze = wyplacone ze skarbca). Zapis "arm_zold431" (brak klucza = brak pozyczek i brak decyzji gracza); Reset w konstruktorze
    /// ArmouryBehavior. Wyjatki per rod liczone (potkniecia), bez globalnego wylacznika.
    /// </summary>
    internal static class CrownWarLoan431
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        // decyzja gracza: korona i rok, ktorych dotyczy; 0 brak, 1 przyjal, 2 odmowil
        private static string _plKingdom = "";
        private static int _plYear = int.MinValue, _plAns;
        private static bool _plAsked;             // okno pokazane w tej sesji (bez zapisu - po wczytaniu pytanie wraca raz, gdy dalej brak odpowiedzi)
        private static int _plAskFails;           // 431-p: nieudane proby pokazania okna (wyjatek) - najwyzej 3 ponowienia w sesji
        // 431-p (recenzja N3): kapital splacony koronie od ostatniego CrownIncome.Begin (id krolestwa -> zl) - do zapasu korony, nie do wplywow dnia. Bez zapisu
        // (wczytanie w srodku okna: najwyzej jedna doba splat policzona jako wplyw, jak przed 431-p).
        private static readonly Dictionary<string, long> _back = new Dictionary<string, long>(StringComparer.Ordinal);
        private static long _dBackStock, _dRefCap; private static int _dRefCapN;

        private static long _prevSum = -1;         // dlug ksiegi przy poprzedniej linii (zapis); -1 = brak punktu odniesienia
        private static int _stumbles, _importN = -1, _importBad;
        private static readonly HashSet<string> _err = new HashSet<string>();

        // liczniki od poprzedniej linii (zerowane po linii)
        private static readonly int[] _bN = new int[4];
        private static readonly long[] _bFull = new long[4], _bNeed = new long[4];
        private static long _dLimitCut, _dBlocked, _dPlHeld, _dNoLadder, _dWant, _dLent, _dPaidOut, _dUnbooked;
        private static int _dLimitN, _dBlockedN, _dLentN;
        private static long _dInterest, _dRepaid, _dRepInt, _dToCrown, _dSpare, _dLoot, _dInst, _dOrphan;
        private static long _dPlLent; private static double _dPlF = -1; private static int _dPlBand = -1;

        internal static bool On
        {
            get { var s = Settings.Current; return s != null && s.RoyalWarWageLoan && s.RoyalWarWageEnabled && DebtLadder.On && ClanBudget.On; }
        }

        internal static void Reset()
        {
            _plKingdom = ""; _plYear = int.MinValue; _plAns = 0; _plAsked = false; _prevSum = -1;
            _stumbles = 0; _importN = -1; _importBad = 0; _err.Clear(); _back.Clear(); _plAskFails = 0;
            ZeroDay();
        }

        private static void ZeroDay()
        {
            for (int i = 0; i < 4; i++) { _bN[i] = 0; _bFull[i] = 0; _bNeed[i] = 0; }
            _dLimitCut = _dBlocked = _dPlHeld = _dNoLadder = _dWant = _dLent = _dPaidOut = _dUnbooked = 0;
            _dLimitN = _dBlockedN = _dLentN = 0;
            _dInterest = _dRepaid = _dRepInt = _dToCrown = _dSpare = _dLoot = _dInst = _dOrphan = 0;
            _dPlLent = 0; _dPlF = -1; _dPlBand = -1;
            _dBackStock = 0; _dRefCap = 0; _dRefCapN = 0;
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            try { if (_err.Add(where)) Log.Error("CrownWarLoan431." + where, e); } catch { }
        }

        private static string Name(IFaction f) { try { return f != null && f.Name != null ? f.Name.ToString() : "-"; } catch { return "?"; } }

        private static float Rate(Settings s) { return Math.Max(0f, Math.Min(0.5f, s.RoyalWarWageLoanRate)); }
        private static int LimitDays(Settings s) { return Math.Max(0, Math.Min(728, s.RoyalWarWageLoanLimitDays)); }

        // ------------------------------------------------------------ ksiega drabiny -> liczniki linii (wola DebtLadder; nie rzucaja)
        internal static void NoteInterest(long x) { if (x > 0) _dInterest += x; }
        internal static void NoteLoot(long x) { if (x > 0) _dLoot += x; }
        internal static void NoteInstalment(long x) { if (x > 0) _dInst += x; }
        internal static void NoteOrphan(long x) { if (x > 0) _dOrphan += x; }
        /// <summary>Splata x (w tym odsetki ip) dotarla do skarbca korony (spare = false) albo do zapasowej kasy miasta (spare = true).</summary>
        internal static void NoteRepaid(long x, long ip, bool spare)
        {
            if (x <= 0) return;
            _dRepaid += x; _dRepInt += Math.Max(0, ip);
            if (spare) _dSpare += x; else _dToCrown += x;
        }

        /// <summary>431-p: DebtLadder.Deliver - kapital (bez odsetek) pozyczki wrocil do skarbca korony k.</summary>
        internal static void NoteCapitalBack(Kingdom k, long x)
        {
            try { if (k == null || k.StringId == null || x <= 0) return; long v; _back.TryGetValue(k.StringId, out v); _back[k.StringId] = v + x; }
            catch (Exception e) { Stumble("NoteCapitalBack", e); }
        }

        /// <summary>431-p: CrownIncome.Begin - splacony kapital od poprzedniego Begin, najwyzej `room` (tyle "inne" zostalo po jednorazowych). Zeruje okno. Nie rzuca.</summary>
        internal static long TakeCapitalBack(Kingdom k, long room)
        {
            try
            {
                long v;
                if (k == null || k.StringId == null || !_back.TryGetValue(k.StringId, out v)) return 0;
                _back.Remove(k.StringId);
                long x = Math.Max(0, Math.Min(v, room));
                _dBackStock += x;
                return x;
            }
            catch (Exception e) { Stumble("TakeCapitalBack", e); return 0; }
        }

        // ------------------------------------------------------------ regula (wola CrownWar314.PayWages)
        /// <summary>Ile z pelnego zoldu 314c `full` korona k chce dzis POZYCZYC rodowi c: wedlug potrzeby f, w limicie dlugu, bez rodow w zaleglosci, gracz po zgodzie.
        /// 0 - nic. Liczniki linii. Nie rzuca (wyjatek - 0 i potkniecie: lepiej nie pozyczyc, niz dac z niczego regule).</summary>
        internal static long Want(Clan c, Kingdom k, long full, int year, long wageServing = long.MaxValue)
        {
            try
            {
                var s = Settings.Current;
                if (s == null || c == null || k == null || full <= 0) return 0;
                bool player = c == Clan.PlayerClan;
                if (player && !DebtLadder.PlayerOnLadder) { _dNoLadder += full; return 0; }
                double f, G, R;
                int band = KingdomTreasury.NeedOfClan(c, out f, out G, out R);
                if (band < 0 || band > 3) band = 3;
                if (band == 3 || double.IsNaN(f)) f = 1;
                f = Math.Max(0.0, Math.Min(1.0, f));
                long need = f >= 1.0 ? full : (long)Math.Floor(full * f);
                _bN[band]++; _bFull[band] += full; _bNeed[band] += need;
                if (player) { _dPlF = f; _dPlBand = band; }
                if (need <= 0) return 0;
                // 431-p (recenzja S1, klucz Royal War Wage Loan Keeps Refund): pozyczka nie tnie juz jutrzejszego zwrotu 70% (CrownWar314.RefundCut), wiec nie moze
                // razem z dzisiejszym zwrotem przekroczyc zoldu sluzacych - rod pozycza najwyzej na te czesc zoldu, ktorej zwrot nie pokryl
                if (s.RoyalWarWageLoanKeepsRefund && wageServing != long.MaxValue)
                {
                    long uncovered = Math.Max(0, wageServing - KingdomTreasury.RefundGivenToday(c));
                    if (need > uncovered) { _dRefCap += need - uncovered; _dRefCapN++; need = uncovered; }
                    if (need <= 0) return 0;
                }
                double D = DebtLadder.DebtD(c);
                long limit = (long)Math.Floor(LimitDays(s) * Math.Max(0, D));
                long room = Math.Max(0, limit - DebtLadder.CrownLoanOf(c));
                long want = Math.Min(need, room);
                if (want < need) { _dLimitCut += need - want; _dLimitN++; }
                if (want <= 0) return 0;
                if (DebtLadder.FolkLoanBlocked(c)) { _dBlocked += want; _dBlockedN++; return 0; }
                if (player && !PlayerGate(s, k, year, want, limit)) { _dPlHeld += want; return 0; }
                _dWant += want;
                return want;
            }
            catch (Exception e) { Stumble("Want", e); return 0; }
        }

        /// <summary>Skarbiec k wyplacil rodowi c `x` zl (CrownWar314.Pay) - wpis pozyczki do ksiegi drabiny. Nie rzuca (nieudany wpis - licznik kontroli).</summary>
        internal static void Lent(Clan c, Kingdom k, long x)
        {
            if (x <= 0) return;
            _dPaidOut += x;
            try
            {
                if (c != null && k != null && DebtLadder.AddCrownLoan(c, k.StringId, x))
                {
                    _dLent += x; _dLentN++;
                    if (c == Clan.PlayerClan) _dPlLent += x;
                    return;
                }
            }
            catch (Exception e) { Stumble("Lent", e); }
            _dUnbooked += x;
        }

        /// <summary>Komunikat doby dla gracza (CrownWar314.PayWages) - po angielsku.</summary>
        internal static string PlayerDayText(long lent)
        {
            long owed = 0;
            try { owed = DebtLadder.CrownLoanOf(Clan.PlayerClan); } catch { }
            return "The crown lends your house " + lent + " denars toward the wages of your men in the king's army. You now owe the crown " + owed + " denars.";
        }

        // ------------------------------------------------------------ gracz: okno wyboru
        private static bool PlayerGate(Settings s, Kingdom k, int year, long want, long limit)
        {
            if (!s.RoyalWarWageLoanAskPlayer) return true;
            string kid = k.StringId ?? "";
            if (_plKingdom != kid || _plYear != year) { _plKingdom = kid; _plYear = year; _plAns = 0; _plAsked = false; }
            if (_plAns == 1) return true;
            if (_plAns == 0 && !_plAsked)
            {
                _plAsked = true;
                try { Ask(s, k, kid, year, want, limit); }
                catch (Exception e) { Stumble("Ask", e); if (++_plAskFails <= 3) _plAsked = false; }   // 431-p (recenzja N2): wyjatek nie wycisza okna na cala sesje
            }
            return false;
        }

        private static void Ask(Settings s, Kingdom k, string kid, int year, long want, long limit)
        {
            string text = "Your house has served its free days in the armies of " + Name(k) + " this year. The crown no longer gives war wages away - it lends them, "
                          + "and only to houses in need.\n\nIt offers your house a loan toward the wages of your men in the king's army: about " + want
                          + " denars today if the treasury can spare it, less as your house grows richer and nothing once it holds twice its war reserve. The debt grows by "
                          + (Rate(s) * 100).ToString("0.#", Inv) + "% a year and the crown lends no more once you owe it " + limit + " denars ("
                          + LimitDays(s) + " days of your steady income).\n\nWhile the realm is at war and you serve it you pay only the interest, day by day, out of your steady "
                          + "income; the loan itself cannot be paid back early. After the war - or at once, should you leave this crown - it is taken back in daily "
                          + "instalments out of your steady income, spread over about " + Math.Max(28, Math.Min(1456, s.RoyalWarWageLoanRepayDays))
                          + " days; three missed instalments and your creditors seize the income of your villages.\n\n"
                          + "Your answer stands until the end of the year.";
            InformationManager.ShowInquiry(new InquiryData("A loan from the crown", text, true, true, "Accept the loan", "Refuse",
                delegate { Answer(kid, year, 1); }, delegate { Answer(kid, year, 2); }), true);
        }

        private static void Answer(string kid, int year, int ans)
        {
            try
            {
                if (_plKingdom != kid || _plYear != year) return;   // inna korona albo rok od czasu pytania - odpowiedz niewazna
                _plAns = ans;
                Log.Player(ans == 1 ? "You accepted the crown's loan toward your war wages. It is paid at the daily settlement while your men serve in the king's army."
                                    : "You refused the crown's loan. Your house pays its own war wages this year.");
                var s = Settings.Current;
                if (s != null && s.LogEnabled) Log.Info("Pozyczka korony na zold (431): gracz " + (ans == 1 ? "PRZYJAL" : "ODMOWIL") + " pozyczki korony " + kid + " na rok " + year + ".");
            }
            catch (Exception e) { Stumble("Answer", e); }
        }

        // ------------------------------------------------------------ linia doby (po DebtLadder.Report - po ratach i zajeciach tej doby; zloto nie rusza)
        internal static void Report()
        {
            try
            {
                var s = Settings.Current;
                if (s == null || Campaign.Current == null) { ZeroDay(); return; }
                long debt, acc; int clans, arr, seized;
                var by = new Dictionary<string, long[]>();
                DebtLadder.CrownLoanBook(out debt, out acc, out clans, out arr, out seized, by);
                bool any = debt > 0 || _dPaidOut > 0 || _dRepaid > 0 || _dInterest > 0 || _dOrphan > 0 || _bN[0] + _bN[1] + _bN[2] + _bN[3] > 0 || _dNoLadder > 0;
                if (s.LogEnabled && (On || any || _importN >= 0))
                {
                    int today = (int)CampaignTime.Now.ToDays;
                    long full = 0, need = 0; for (int i = 0; i < 4; i++) { full += _bFull[i]; need += _bNeed[i]; }
                    string[] bn = { "ponizej R (calosc)", "R-2R (czesc)", "ponad 2R (nic)", "bez miary (calosc)" };
                    var sb = new StringBuilder(1400);
                    sb.Append("Pozyczka korony na zold (431): dzien ").Append(today)
                      .Append(" | Royal War Wage Loan ").Append(On ? "tak (czynny)" : (s.RoyalWarWageLoan ? "tak, ale NIECZYNNY (wymaga zoldu 314c, drabiny 168 i budzetu 166) - zold 314c darem jak dotad" : "NIE - zold 314c darem jak dotad"))
                      .Append(" (").Append((Rate(s) * 100).ToString("0.##", Inv)).Append("% rocznie, limit ").Append(LimitDays(s)).Append(" dni D, splata po wojnie w ")
                      .Append(Math.Max(28, Math.Min(1456, s.RoyalWarWageLoanRepayDays))).Append(" dob)")
                      .Append(" | rody z naleznym zoldem 314c wedlug f = (2R - G) / R:");
                    for (int i = 0; i < 4; i++)
                        sb.Append(i == 0 ? " " : "; ").Append(bn[i]).Append(" rodow ").Append(_bN[i]).Append(", pelny ").Append(_bFull[i]).Append(", wedlug potrzeby ").Append(_bNeed[i]);
                    sb.Append(" | razem pelny (dar sprzed 431) ").Append(full).Append(", wedlug potrzeby ").Append(need)
                      .Append(", ponad limit dlugu ").Append(_dLimitCut).Append(" (rodow ").Append(_dLimitN).Append(')')
                      .Append(", rody w zaleglosci / zajeciu / czarnej ksiedze ").Append(_dBlocked).Append(" (rodow ").Append(_dBlockedN).Append(')')
                      .Append(", ponad zold niepokryty dzisiejszym zwrotem f6 (431-p, klucz Royal War Wage Loan Keeps Refund ").Append(s.RoyalWarWageLoanKeepsRefund ? "tak" : "nie").Append(") ")
                      .Append(_dRefCap).Append(" (rodow ").Append(_dRefCapN).Append(')')
                      .Append(", gracz bez zgody ").Append(_dPlHeld).Append(", gracz poza drabina ").Append(_dNoLadder)
                      .Append(" | do pozyczenia ").Append(_dWant).Append(", POZYCZONE ").Append(_dLent).Append(" (rodow ").Append(_dLentN)
                      .Append("; reszta - skarbiec bez zlota ponad zapas wojny) | zostalo w skarbcach wobec daru sprzed 431: ").Append(Math.Max(0, full - _dLent))
                      .Append(" | KONTROLA zlota: wyplacone ze skarbcow ").Append(_dPaidOut).Append(" - wpisane do ksiegi ").Append(_dLent).Append(" = ").Append(_dPaidOut - _dLent)
                      .Append(" (oczekiwane 0; bez wpisu ").Append(_dUnbooked).Append(')')
                      .Append(" | splaty od poprzedniej linii ").Append(_dRepaid).Append(" (w tym odsetki ").Append(_dRepInt).Append("; z jednorazowych ").Append(_dLoot)
                      .Append(", raty z D ").Append(_dInst).Append(", reszta - zajecie, wyprzedaz, cena ziemi) -> skarbce ").Append(_dToCrown).Append(", zapasowe kasy miast ").Append(_dSpare)
                      .Append(" | splacony kapital do zapasu koron (nie wplyw dnia, 431-p) ").Append(_dBackStock)
                      .Append(" | odsetki narosle ").Append(_dInterest).Append(", do dlugu bez platnika (rod wymarl bez wsi) ").Append(_dOrphan)
                      .Append(" | KSIEGA: dlug ").Append(debt).Append(" (w tym odsetki niezaplacone ").Append(acc).Append(") u ").Append(clans).Append(" rodow, w zaleglosci ").Append(arr)
                      .Append(", w zajeciu ").Append(seized);
                    if (_prevSum >= 0)
                    {
                        long exp = _prevSum + _dLent + _dInterest - _dRepaid - _dOrphan;
                        sb.Append(" | KONTROLA ksiegi: poprzednio ").Append(_prevSum).Append(" + pozyczone ").Append(_dLent).Append(" + odsetki ").Append(_dInterest)
                          .Append(" - splacone ").Append(_dRepaid).Append(" - bez platnika ").Append(_dOrphan).Append(" = ").Append(exp).Append(", w ksiedze ").Append(debt)
                          .Append(", roznica ").Append(debt - exp).Append(" (oczekiwane 0)");
                    }
                    else sb.Append(" | KONTROLA ksiegi: brak punktu odniesienia (pierwsza linia kampanii albo zapis sprzed 431)");
                    if (by.Count > 0)
                    {
                        var list = new List<KeyValuePair<string, long[]>>(by);
                        list.Sort((a, b) => b.Value[0].CompareTo(a.Value[0]));
                        sb.Append(" | wierzyciele:");
                        for (int i = 0; i < list.Count && i < 8; i++)
                        {
                            string nm = list[i].Key;
                            try { foreach (var k in Kingdom.All) if (k != null && k.StringId == list[i].Key) { nm = Name(k) + (k.IsEliminated ? " (zniszczone)" : ""); break; } } catch { }
                            sb.Append(i == 0 ? " " : "; ").Append(nm).Append(' ').Append(list[i].Value[0]).Append(" (roszczen ").Append(list[i].Value[1]).Append(')');
                        }
                        if (list.Count > 8) sb.Append("; i ").Append(list.Count - 8).Append(" innych");
                    }
                    long plOwed = 0; try { plOwed = DebtLadder.CrownLoanOf(Clan.PlayerClan); } catch { }
                    sb.Append(" | gracz: dlug ").Append(plOwed).Append(", pozyczone dzis ").Append(_dPlLent)
                      .Append(", f ").Append(_dPlF >= 0 ? _dPlF.ToString("0.00", Inv) : "-")
                      .Append(", decyzja ").Append(!s.RoyalWarWageLoanAskPlayer ? "bez pytania (jak AI)" : _plAns == 1 ? "przyjal (rok " + _plYear + ")" : _plAns == 2 ? "odmowil (rok " + _plYear + ")" : _plAsked ? "okno pokazane, brak odpowiedzi" : "brak (okno przy pierwszej pozyczce roku)")
                      .Append(" | potkniecia ").Append(_stumbles).Append('.');
                    if (_importN >= 0) { sb.Append(" Wczytano z zapisu: roszczen ").Append(_importN).Append(" (bledne ").Append(_importBad).Append(")."); _importN = -1; }
                    Log.Info(sb.ToString());
                }
                _prevSum = debt;
            }
            catch (Exception e) { Stumble("Report", e); }
            finally { ZeroDay(); }
        }

        // ------------------------------------------------------------ zapis (SaveText, "arm_zold431"; po arm_debt168 - DebtLadder.Import czysci ksiege)
        private static readonly char[] Bad = { '|', ';', '^', ',', ':' };

        /// <summary>"cw1|dlug poprzedniej linii|korona decyzji gracza|rok|odpowiedz|rod^korona^reszta^calosc^doba^odsetki^ulamek;..."; pusty napis - nic do zapisania.</summary>
        internal static string Export()
        {
            try
            {
                string claims = DebtLadder.ExportCrownLoans();
                bool dec = _plAns != 0 && !string.IsNullOrEmpty(_plKingdom) && _plKingdom.IndexOfAny(Bad) < 0;
                if (string.IsNullOrEmpty(claims) && !dec && _prevSum <= 0) return "";   // 431-p (recenzja N5): bez roszczen i decyzji - brak klucza w zapisie (takze przy kluczu NIE)
                return "cw1|" + _prevSum.ToString(Inv) + "|" + (dec ? _plKingdom : "") + "|" + (dec ? _plYear : 0).ToString(Inv) + "|" + (dec ? _plAns : 0).ToString(Inv) + "|" + claims;
            }
            catch (Exception e) { Stumble("Export", e); return ""; }
        }

        internal static void Import(string data)
        {
            _plKingdom = ""; _plYear = int.MinValue; _plAns = 0; _plAsked = false; _prevSum = -1; _importN = -1; _importBad = 0; _back.Clear(); _plAskFails = 0;
            try
            {
                if (string.IsNullOrEmpty(data)) return;   // zapis sprzed 431 - brak pozyczek, brak decyzji
                _importN = 0;
                var f = data.Split('|');
                if (f.Length < 6 || f[0] != "cw1") { _importBad++; return; }
                long prev; int y, a;
                if (long.TryParse(f[1], NumberStyles.Integer, Inv, out prev)) _prevSum = Math.Max(-1, prev); else _importBad++;
                if (f[2].Length > 0 && int.TryParse(f[3], NumberStyles.Integer, Inv, out y) && int.TryParse(f[4], NumberStyles.Integer, Inv, out a) && (a == 1 || a == 2))
                { _plKingdom = f[2]; _plYear = y; _plAns = a; }
                _importN = DebtLadder.ImportCrownLoans(f[5], ref _importBad);
            }
            catch (Exception e) { Stumble("Import", e); }
        }
    }
}
