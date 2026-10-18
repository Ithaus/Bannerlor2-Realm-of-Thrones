using System;
using System.Text;
using TaleWorlds.CampaignSystem;

namespace Armoury
{
    /// <summary>
    /// W6b (decyzja Jeffa 13.10 o tarczowym: "rod gracza tez dotyczy, ale pod warunkiem, ze mam ziemie i sluze jakiemus krolowi") - ROD GRACZA W TARCZOWYM (W6,
    /// KingdomTreasury.Scutage.cs) I W DOPLACIE WEDLUG POTRZEBY (W5, KingdomTreasury.NeedAid.cs) na zasadach rodu AI. Wylacznik ScutagePlayerClan (wylaczony = stan
    /// sprzed W6b co do monety: rod gracza pominiety w obu krokach, jak dotad).
    ///  - WARUNKI (wszystkie naraz; brak ktoregos = ani nie placi, ani nie dostaje): (1) ZIEMIA - co najmniej jedno miasto albo zamek (Clan.Fiefs; sama wies sie
    ///    nie liczy: w ksiedze gry wies nalezy do pana swojego miasta albo zamku, a tytul wsi BK - GetActualVillages - daje tylko podatek wsi); (2) SLUZBA KROLOWI -
    ///    wasal krolestwa: nie wladca (korona to jego skarbiec - placilby sam sobie) i nie najemnik (renty 180 i tak ich nie licza); (3) krolestwo w wojnie
    ///    (krok W5 biegnie tylko w wojnie - jak AI). W5 wymaga jeszcze warunku rent 180 (zaloga i sluzba) - jak u AI; W6 go nie wymaga - jak u AI.
    ///  - PULAP WLASNY gracza (ClanBudget.PlayerCaps): ten sam wzor co u AI (D staly z ksiegi dochodu, kiesy rodziny, rezerwa z doroslych, skrzynia wojenna), TYLKO
    ///    miara - gracz nie ma budzetu 166, nic go nie ogranicza w werbunku. W5 - pulap z udzialem dworu ustepujacym zoldowi (jak PartyCapOwn AI), W6 - bez
    ///    udzialu dworu (jak ScutageCap AI); zold zalog i karawan rodu odjety.
    ///  - TARCZOWE gracza: jak AI - podstawa = pulap bez dworu ponad zold druzyn przy celu W5, 25%, tylko ze zlota rodziny ponad rezerwe, najwyzej 1/45 nadwyzki na
    ///    dobe, kiesa glowy (gracza) nie ponizej podlogi; korona bierze tylko niepokryta potrzebe W5. Kiesa glowy gracza -> skarbiec (ScCollect, ta sama droga co AI).
    ///  - DOPLATA W5 gracza: jak AI w kolejce "od najslabszych", ale SAMA GOTOWKA za ludzi, ktorzy sa w druzynach ponad pulap wlasny - bez "miejsca w pulapie" na
    ///    brakujacych (gracz nie ma pulapu 166 - werbuje sam). Skarbiec -> kiesa gracza; zwrot 70% nastepnej doby bez tej czesci (NeedAidRefundCut - jak AI).
    ///  - PLATNIK I ODBIORCA NIGDY NARAZ: podstawa > 0 znaczy, ze pulap bez dworu starcza na cel - z dworem tym bardziej, wiec potrzeby W5 nie ma (jak AI).
    ///  - KOMUNIKATY (po angielsku, kolor Armoury): wyjasnienie przy pierwszej dobie z poborem / z doplata, potem najwyzej raz na 30 dob gry w procesie (po audycie
    ///    13/14.10; bez pola zapisu - pamiec procesu kluczowana kampania, wczytanie jej nie czysci),
    ///    sumy raz na 7 dob na rodzaj (jak renty 180). Linie: segment " | gracz: ..." w liniach "Doplata wedlug potrzeby (W5)" i "Tarczowe (W6)".
    /// Zapis gry: nic nowego (stan doby i tydzien komunikatow w pamieci sesji). Wyjatek - licznik potkniec W6 (pierwszy do pliku), bez globalnych wylacznikow.
    /// </summary>
    internal static partial class KingdomTreasury
    {
        // stan doby rodu gracza (czyszczony z doba kroku W5 - ScClearDay); pulap liczony raz na dobe (PlayerCaps) - W5 i W6 widza te same liczby
        private static int _plCapDay = int.MinValue, _plRent = -1;        // _plRent: warunek rent 180 w kroku W5 (-1 nieznany, 0 nie, 1 tak)
        private static bool _plCapOk, _plEval5, _plEval6;
        private static double _plCapW5, _plCapW6, _plCapRes, _plD, _plNeed, _plBase = double.NaN, _plSur = double.NaN;
        private static long _plWage, _plDue; private static int _plMen, _plLim;
        private static string _plWhy, _plWhy6;
        // komunikaty: suma 7 dob (jak renty 180) i wyjasnienie
        private static long _plWeekPaid, _plWeekGot; private static int _plWeekStart = -1; private static string _plWeekK;
        // poprawka po audycie (gracz 10): wyjasnienie nie czesciej niz raz na PlToldEvery dob gry w sesji (dotad po kazdym wczytaniu). Pamiec PROCESU - bez pola
        // zapisu i bez czyszczenia przy wczytaniu (PlReset jej nie rusza); inna kampania (Campaign.UniqueGameId) - od nowa
        private const int PlToldEvery = 30;
        private static string _plToldGame;
        private static int _plToldScutDay = int.MinValue, _plToldAidDay = int.MinValue, _plToldLevyDay = int.MinValue;   // drobne2: + W7 (LvPlayer)

        internal static bool PlayerClanOn { get { var s = Settings.Current; return s != null && s.ScutagePlayerClan; } }

        private static void PlClearDay()
        {
            _plCapDay = int.MinValue; _plRent = -1; _plCapOk = false; _plEval5 = false; _plEval6 = false;
            _plCapW5 = _plCapW6 = _plCapRes = _plD = _plNeed = 0; _plBase = double.NaN; _plSur = double.NaN;
            _plWage = 0; _plDue = 0; _plMen = 0; _plLim = 0; _plWhy = null; _plWhy6 = null;
        }

        private static void PlReset()
        {
            PlClearDay();
            _plWeekPaid = _plWeekGot = 0; _plWeekStart = -1; _plWeekK = null;   // wyjasnienia (_plTold*) - pamiec procesu, patrz PlTellDue
        }

        /// <summary>Wyjasnienie W6b/W5 gracza: true najwyzej raz na PlToldEvery dob gry tej kampanii w tym procesie (takze po wczytaniu, takze wczesniejszego zapisu).</summary>
        private static bool PlTellDue(ref int last, int today)
        {
            string gid = null;
            try { gid = Campaign.Current != null ? Campaign.Current.UniqueGameId : null; } catch { gid = null; }
            if (!string.Equals(gid, _plToldGame, StringComparison.Ordinal)) { _plToldGame = gid; _plToldScutDay = _plToldAidDay = _plToldLevyDay = int.MinValue; }
            if (last != int.MinValue && Math.Abs((long)today - last) < PlToldEvery) return false;
            last = today;
            return true;
        }

        /// <summary>Warunki 1-3 decyzji Jeffa dla krolestwa k (null - krolestwo gracza): miasto albo zamek, wasal tego krolestwa (nie wladca, nie najemnik),
        /// krolestwo w wojnie. Tylko odczyt.</summary>
        private static bool PlConds(Kingdom k, out bool land, out bool vassal, out bool war)
        {
            land = false; vassal = false; war = false;
            var c = Clan.PlayerClan;
            if (c == null || c.IsEliminated) return false;
            if (k == null) k = c.Kingdom;
            land = c.Fiefs != null && c.Fiefs.Count > 0;                 // Fiefs = miasta i zamki (Town); sama wies nie
            vassal = k != null && !k.IsEliminated && c.Kingdom == k && !c.IsUnderMercenaryService && k.RulingClan != c;
            war = k != null && !k.IsEliminated && AtWar(k);
            return land && vassal && war;
        }

        /// <summary>Krok W5 krolestwa k: czy rod gracza jest kandydatem (klucz, warunki 1-3, warunek rent jak AI). Zapisuje wynik do linii.</summary>
        private static bool PlW5Gate(Kingdom k, bool rentOk)
        {
            if (!PlayerClanOn) return false;
            bool land, vassal, war;
            if (!PlConds(k, out land, out vassal, out war)) return false;
            _plEval5 = true; _plRent = rentOk ? 1 : 0;
            if (!rentOk) { _plWhy = "warunek rent 180 niespelniony (zaloga albo sluzba)"; return false; }
            return true;
        }

        /// <summary>Krok W6 krolestwa k: czy rod gracza moze byc platnikiem (klucz i warunki 1-3; warunek rent nie - jak AI).</summary>
        private static bool PlW6Gate(Kingdom k)
        {
            if (!PlayerClanOn) return false;
            bool land, vassal, war;
            if (!PlConds(k, out land, out vassal, out war)) return false;
            _plEval6 = true;
            return true;
        }

        /// <summary>Pulap wlasny gracza raz na dobe (ClanBudget.PlayerCaps - ten sam wzor co AI). false - bez miary (powod w _plWhy).</summary>
        private static bool PlCaps()
        {
            int today = (int)CampaignTime.Now.ToDays;
            if (_plCapDay != today)
            {
                _plCapDay = today;
                _plCapOk = ClanBudget.PlayerCaps(Clan.PlayerClan, out _plCapW5, out _plCapW6, out _plCapRes, out _plD);
            }
            return _plCapOk;
        }

        /// <summary>W5: pulap wlasny druzyn gracza (z udzialem dworu ustepujacym zoldowi - jak PartyCapOwn AI).</summary>
        private static bool PlCapW5(out double pc0)
        {
            pc0 = 0;
            if (!PlCaps())
            {
                _plWhy = ClanBudget.Pending ? "budzet 166 jeszcze nie policzony w tej sesji (od jutra, jak AI)" : "bez miary pulapu (166 wylaczone, Straz bez zoldu albo rod nie w wojnie)";
                return false;
            }
            pc0 = _plCapW5;
            return true;
        }

        /// <summary>W6: pulap wlasny gracza bez udzialu dworu i rezerwa wojenna (jak ScutageCap AI); dluznik albo rod w zajeciu (168) - nie placi (jak AI).</summary>
        private static bool PlCapW6(out double pc, out double reserve)
        {
            pc = 0; reserve = 0;
            var c = Clan.PlayerClan;
            if (DebtLadder.On && (DebtLadder.IsDebtor(c) || DebtLadder.IsSeized(c))) { _plWhy6 = "dluznik albo zajecie (168)"; return false; }
            if (!PlCaps()) { _plWhy6 = ClanBudget.Pending ? "budzet 166 jeszcze nie policzony w tej sesji" : "bez miary pulapu"; return false; }
            pc = _plCapW6; reserve = _plCapRes;
            return true;
        }

        /// <summary>W5: druzyny i potrzeba gracza w chwili kroku (do linii).</summary>
        private static void PlNote(NaClan x, double need)
        {
            _plWage = (long)x.W; _plMen = x.M; _plLim = x.L; _plNeed = need;
            if (!(need > 0)) _plWhy = "pulap wlasny starcza na druzyny do celu - bez doplaty";
        }

        private static string PlTn(bool b) { return b ? "tak" : "nie"; }
        private static string PlN(double v) { return double.IsNaN(v) ? "-" : Math.Round(v).ToString("0", WtInv); }

        /// <summary>Segment linii W5 (w6 = false) albo W6 (w6 = true): " | gracz: ..." - klucz, warunki 1-3, pulap wlasny, podstawa, dostal / zaplacil tej doby.</summary>
        private static string PlSeg(int today, bool w6)
        {
            try
            {
                var c = Clan.PlayerClan;
                long got = 0, paid = 0;
                if (c != null && _naDay == today) { _naPaid.TryGetValue(c, out got); _scPaid.TryGetValue(c, out paid); }
                var sb = new StringBuilder(320);
                sb.Append(" | gracz: klucz Scutage Player Clan ");
                if (!PlayerClanOn) return sb.Append("nie (ani nie placi, ani nie dostaje - jak przed W6b), dostal ").Append(got).Append(", zaplacil ").Append(paid).ToString();
                bool land, vassal, war;
                bool all = PlConds(null, out land, out vassal, out war);
                sb.Append("tak, ziemia (miasto albo zamek) ").Append(PlTn(land)).Append(", wasal krolestwa (nie wladca, nie najemnik) ").Append(PlTn(vassal))
                  .Append(", krolestwo w wojnie ").Append(PlTn(war)).Append(" -> warunki ").Append(all ? "tak" : "nie spelnione (ani nie placi, ani nie dostaje)");
                if (all)
                {
                    bool cap = _plCapDay == today && _plCapOk;
                    if (!w6)
                    {
                        sb.Append(", warunek rent ").Append(_plRent < 0 ? "-" : PlTn(_plRent > 0))
                          .Append(", pulap wlasny ").Append(cap ? PlN(_plCapW5) : "-").Append(" (D ").Append(cap ? PlN(_plD) : "-").Append(')');
                        if (_plEval5 && _plLim > 0)
                            sb.Append(", zold druzyn ").Append(_plWage).Append(" (ludzi ").Append(_plMen).Append(" / limit ").Append(_plLim).Append("), potrzeba W5 ").Append(PlN(_plNeed));
                        if (!_plEval5) sb.Append(" (krok W5 nie ocenial rodu gracza: W5 nieczynne, pierwsza doba sesji albo bez wiersza rent 180)");
                        else if (_plWhy != null && got <= 0) sb.Append(" (").Append(_plWhy).Append(')');
                    }
                    else
                    {
                        sb.Append(", pulap wlasny bez dworu ").Append(cap ? PlN(_plCapW6) : "-").Append(", podstawa gracza ").Append(PlN(_plBase))
                          .Append(", nadwyzka kiesy rodziny ponad rezerwe ").Append(PlN(_plSur)).Append(", nalezne gracza ").Append(_plDue);
                        if (!_plEval6) sb.Append(" (krok W6 nie ocenial rodu gracza: W6 nieczynne, pierwsza doba sesji albo bez wiersza rent 180)");
                        else if (_plWhy6 != null && paid <= 0) sb.Append(" (").Append(_plWhy6).Append(')');
                        else if (_plDue > 0 && paid <= 0) sb.Append(" (korona dzis nie brala - bez niepokrytej potrzeby W5 po daninie i skarbcu)");
                    }
                }
                return sb.Append(", dostal ").Append(got).Append(", zaplacil ").Append(paid).ToString();
            }
            catch (Exception e) { ScStumble("PlSeg", e); return " | gracz: blad odczytu"; }
        }

        /// <summary>Komunikaty dla gracza (ScutageDay, raz na dobe, niezaleznie od LogEnabled): wyjasnienie przy pierwszym poborze i pierwszej doplacie w sesji,
        /// sumy raz na 7 dob na rodzaj (jak renty 180 - suma tylko w pamieci, po wczytaniu liczy od nowa).</summary>
        private static void PlWeek(int today)
        {
            try
            {
                var c = Clan.PlayerClan;
                if (c == null) return;
                long got = 0, paid = 0;
                if (_naDay == today) { _naPaid.TryGetValue(c, out got); _scPaid.TryGetValue(c, out paid); }
                var s = Settings.Current;
                if (paid > 0 && PlTellDue(ref _plToldScutDay, today))
                {
                    Log.Player("Scutage: in war a vassal whose lands could pay for more soldiers than he keeps in the field pays the crown "
                               + (100 * (s != null ? ScRate(s) : 0.25)).ToString("0", WtInv) + "% of the wages he spares, and the crown gives it the same day to the lords who cannot pay their men. "
                               + "It is taken only from coin above your war reserve and only while the crown's war aid still falls short. Field more men and you pay less.");
                }
                if (got - MidAidCashOf(c) > 0 && PlTellDue(ref _plToldAidDay, today))   // f15: sama gotowka W5c ma wlasne wyjasnienie (MidAidDay) - to jest wyjasnienie W5 (cel 60%)
                {
                    Log.Player("War aid: in war the crown helps pay the wages of vassals whose lands cannot keep their companies at "
                               + (100 * (s != null ? NaTarget(s) : 0.6)).ToString("0", WtInv) + "% of their size. It pays only for men already in your companies, and only while you keep the crown's terms for rents (garrisons and service).");
                }
                // recenzja W6b: suma dob start..start+6 podana w dobie start+7 PRZED dopisaniem tej doby (bylo: pierwszy tydzien 8 dob)
                if (_plWeekStart < 0 || today < _plWeekStart) _plWeekStart = today;
                if (today - _plWeekStart >= 7)
                {
                    string kn = _plWeekK ?? "your realm";
                    if (_plWeekPaid > 0) Log.Player("Scutage: you paid " + _plWeekPaid + " denars to the crown of " + kn + " this week - your lands can support more soldiers than you field.");
                    if (_plWeekGot > 0) Log.Player("War aid: the crown of " + kn + " paid you " + _plWeekGot + " denars this week toward your soldiers' wages.");
                    _plWeekPaid = _plWeekGot = 0; _plWeekStart = today; _plWeekK = null;
                }
                if ((paid > 0 || got > 0) && c.Kingdom != null && c.Kingdom.Name != null) _plWeekK = c.Kingdom.Name.ToString();
                _plWeekPaid += paid; _plWeekGot += got;
            }
            catch (Exception e) { ScStumble("PlWeek", e); }
        }
    }
}
