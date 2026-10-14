using System;
using System.Diagnostics;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// PACZKA K1-3a KROK 2 (M) - MIESZCZANIE KUPUJA TYLKO KONIE ROBOCZE W MIEJSCE PADLYCH ZE STAROSCI (decyzja Jeffa 12.10 00:00 UTC i 13.10 "tak robocze
    /// i to dobra zasada"; specyfikacja docs/test-zbiorczy/TRIAZ-zb58-120-2026-10-12.md "K1-3a" - poprawka ostateczna krok 2). Klucz TownsfolkBuyOnlyWorkHorses.
    /// Dotad ludnosc miast i zamkow zdejmowala z polki w dziennym koszyku BK (EconomyPatches.ItemConsumptionPatch - kazda kategoria z budzetem) konie wszystkich
    /// czterech kategorii targu; kon znikal jak zjedzony chleb. Pomiar F3 (bieg roczny 13.10, srednio 363 doby): miasta 76.1 zwyklych / 28.7 bojowych /
    /// 3.6 szlachetnych / 160.4 jucznych, zamki 23.7 / 7.2 / 1.0 / 42.8 na dobe - razem 343 konie/d za 194 tys. zl/d.
    ///  (a) Koszyk BK: budzet wiersza polki = 0 dla horse, war_horse, noble_horse i sumpter_horse (NoHorseBasket - z HistoricalPrices.BudgetPostfix PRZED
    ///      "if (!_applied) return" i z ProsperityPart, zeby kotwica kiesy liczyla ten sam czlon). BK pomija wiersz z budzetem <= 0.01, slownik popytu zostaje.
    ///  (b) Wlasny zakup raz na dobe (WorkHorsesDaily - z ArmouryBehavior zaraz PO TownFolk.Daily, w tej samej pozycji MFolk ksiegi pieniadza): licznik osady
    ///      += ludzie BK osady / 25 / 15 lat / 364 doby (1 kon roboczy na 25 ludzi, zyje 15 lat; swiat ok. 1.85 mln ludzi BK = 13.6 konia/d; od scalenia z paczka A
    ///      "ludzie" = glowy kiesy ludu wedlug ludzi rynku / wsi zamku - te same glowy, ktore placa; suma swiata bez zmian). Przy liczniku >= 1:
    ///      najtanszy kon juczny (kategoria sumpter_horse - ta sama definicja co juczne karawan K5) z polki TEJ osady, gdy go nie ma - najtanszy zwykly (horse,
    ///      rodzina konia); bojowych i szlachetnych lud nie kupuje wcale. Cena polki jak w koszyku BK (TownMarketData.GetPrice), sztuka przez ShelfBuy.Take.
    ///      Placi kiesa ludu (klasy W/R/P proporcjonalnie do wolnych pieniedzy) albo kiesa podzamcza - TYLKO z pieniedzy ponad jutrzejszy koszyk klasy (Need
    ///      z kotwicy tej doby): kon nie odbiera chleba, zakup nie rusza Afford ani licznika glodu E5/C2. Miasto: akcyza TownFolkExcise do gminy, reszta do
    ///      kasy miasta (ta sama regula co PayForGoods E5/B1); zamek: cala cena do kasy zamku (bez kredytu dworu - kredyt jest na kuchnie). Sztuka znika ze
    ///      swiata z nazwanym ujsciem "starosc koni roboczych" w ksiedze koni (HorseBook.NoteWork).
    ///  Sufit: licznik osady najwyzej 3 (WhMax) - tyle tez najwyzej sztuk na dobe. Pusta polka albo za mala kiesa - licznik czeka; ponad 3 zaleglosc przepada
    ///  (lud obywa sie bez konia: to licznik, nie zloto ani sztuka - nic nie powstaje i nic nie ginie). Powod liczby: najwieksze miasto dolicza ok. 0.4 konia
    ///  na dobe, wiec 3 = tydzien zaleglosci najwiekszego miasta i miesiace malego zamku; wiekszy sufit dawalby skok popytu w dniu dostawy (cala dostawa
    ///  jucznych znika z polki jednego dnia - tych samych, ktore kupuja nowe karawany K5), a nie wymiane padlych.
    ///  Dziala tylko przy czynnej kiesie ludu (Live; zamek - CastleLive): bez niej nie ma platnika, wiec koszyk BK kupuje konie jak dotad (budzet nie jest
    ///  zerowany - nic nie liczy sie dwa razy). Zapis: pole 18 rekordu osady w arm_townfolk (stary zapis bez pola = licznik 0). Wylaczony klucz - liczniki 0.
    ///  Linia doby "Konie robocze (K1-3a)". Kazda osada we wlasnym try, licznik potkniec (CLAUDE.md 7).
    /// </summary>
    internal static partial class TownFolk
    {
        private const double WhPeople = 25.0, WhYears = 15.0;   // jeden kon roboczy na 25 ludzi BK, zyje 15 lat (decyzja Jeffa)
        private const int WhMax = 3;                              // sufit licznika osady i zakupu na dobe (sztuki)

        private static int _whZero;                               // wiersze polki z budzetem BK koni wyzerowanym (od poprzedniej linii)
        private static double _whLostAll;                         // zaleglosc ponad sufit od poczatku sesji (tylko linia)
        private static string _whState;                           // stan ostatniej linii jednorazowej (wylaczone / nieczynne)
        private static int _whStumbles;
        private static readonly long[] _whA = new long[3], _whP = new long[3];

        private static void ResetHorses() { _whZero = 0; _whLostAll = 0; _whState = null; _whStumbles = 0; }

        /// <summary>Regula czynna dla tej osady: klucz i czynna kiesa ludu (zamek: takze zawor zamku 110). Sam odczyt ustawien - z kazdego watku.</summary>
        internal static bool WorkHorsesOn(Town town)
        {
            var s = Settings.Current;
            if (s == null || town == null || !s.TownsfolkBuyOnlyWorkHorses) return false;
            return town.IsCastle ? CastleLive : Live;
        }

        /// <summary>(a) Budzet BK tej kategorii w tej osadzie = 0? Cztery kategorie targu koni przy czynnej regule. count - licznik wierszy polki (tylko BudgetPostfix).</summary>
        internal static bool NoHorseBasket(Town town, ItemCategory cat, bool count)
        {
            if (HorseBook.MarketCat(cat) < 0 || !WorkHorsesOn(town)) return false;
            if (count) _whZero++;
            return true;
        }

        private static bool WhPack(ItemObject it) { return it != null && it.ItemCategory == DefaultItemCategories.PackAnimal && it.HorseComponent != null; }
        private static bool WhPlain(ItemObject it)
        {
            return it != null && it.ItemType == ItemObject.ItemTypeEnum.Horse && it.ItemCategory == DefaultItemCategories.Horse && HorseBook.Cat(it) == 0;
        }

        /// <summary>Najtanszy kon juczny na polce, a gdy zadnego nie ma - najtanszy zwykly. Cena polki jak w koszyku BK (bez partii handlujacej).</summary>
        private static bool WhCheapest(Town town, ItemRoster shelf, out EquipmentElement best, out int price, out bool pack)
        {
            best = default(EquipmentElement); price = 0; pack = false;
            if (shelf == null) return false;
            var md = town.MarketData;
            for (int pass = 0; pass < 2; pass++)
            {
                int bp = int.MaxValue;
                for (int i = 0; i < shelf.Count; i++)
                {
                    var el = shelf.GetElementCopyAtIndex(i);
                    if (el.Amount <= 0) continue;
                    var it = el.EquipmentElement.Item;
                    if (pass == 0 ? !WhPack(it) : !WhPlain(it)) continue;
                    int pr = md.GetPrice(el.EquipmentElement);
                    if (pr > 0 && pr < bp) { bp = pr; best = el.EquipmentElement; }
                }
                if (bp != int.MaxValue) { price = bp; pack = pass == 0; return true; }
            }
            return false;
        }

        /// <summary>Wolne pieniadze klas na konia: kiesa ponad jutrzejszy koszyk klasy (Need z kotwicy tej doby). Zamek: [0] = podzamcze.</summary>
        private static long WhAvail(Folk f)
        {
            long sum = 0;
            int n = f.Kind == 1 ? 1 : 3;
            for (int c = 0; c < 3; c++)
            {
                _whA[c] = 0;
                if (c >= n) continue;
                double need = f.Need[c];
                if (double.IsNaN(need) || need < 0) need = 0;
                long x = PurseOf(f, c) - (long)Math.Ceiling(need);
                if (x > 0) { _whA[c] = x; sum += x; }
            }
            return sum;
        }

        /// <summary>Podzial ceny na klasy proporcjonalnie do wolnych pieniedzy (reszta zaokraglen - z klas, ktore jeszcze maja). false - nie starcza.</summary>
        private static bool WhSplit(long cost, long avail)
        {
            _whP[0] = _whP[1] = _whP[2] = 0;
            if (cost <= 0 || avail < cost) return false;
            long rem = cost;
            for (int c = 0; c < 3; c++)
            {
                long x = Math.Min(_whA[c], (long)((double)cost * _whA[c] / avail));
                _whP[c] = x; rem -= x;
            }
            for (int c = 0; c < 3 && rem > 0; c++)
            {
                long x = Math.Min(rem, _whA[c] - _whP[c]);
                if (x > 0) { _whP[c] += x; rem -= x; }
            }
            return rem == 0;
        }

        /// <summary>
        /// (b) Raz na dobe, zaraz po TownFolk.Daily (kotwica na jutro policzona, okna doby wyczyszczone): licznik koni roboczych kazdego miasta i zamku
        /// i zakup z polki tej osady. Przelew kiesa -> kasa osady liczy sie w kontroli zamknietej kies nastepnej doby (_ctlOut) i w podstawie robocizny
        /// handlu (PaidKeep) - jak zaplata ludu za towar.
        /// </summary>
        internal static void WorkHorsesDaily()
        {
            long t0 = Stopwatch.GetTimestamp();
            try
            {
                var s = Settings.Current;
                if (s == null || Campaign.Current == null || !Campaign.Current.GameStarted) return;
                int day = (int)CampaignTime.Now.ToDays - 1;
                if (!s.TownsfolkBuyOnlyWorkHorses || !Live)
                {
                    string why = !s.TownsfolkBuyOnlyWorkHorses ? "WYLACZONE (Townsfolk Buy Only Work Horses = off)" : "NIECZYNNE (kiesa ludu nieczynna - brak platnika)";
                    if (!s.TownsfolkBuyOnlyWorkHorses) { EnsureImported(); foreach (var f in _fk) f.HorseOwed = 0; }   // wylaczony klucz: liczniki od zera
                    if (_whState != why)
                    {
                        _whState = why;
                        Log.Info("Konie robocze (K1-3a): dzien " + day + " | " + why + " - koszyk BK kupuje konie wszystkich kategorii jak dotad (linia Konie (E6/0): mieszczanie).");
                    }
                    _whZero = 0;
                    return;
                }
                _whState = null;
                EnsureImported();
                double exc = Math.Max(0f, Math.Min(0.2f, s.TownFolkExcise));
                bool book = HorseBook.FolkOn;
                int towns = 0, castles = 0, castleOff = 0, noCal = 0, waitShelf = 0, waitPurse = 0, owing = 0;
                double peopleT = 0, peopleC = 0, accrT = 0, accrC = 0, lost = 0, owed0 = 0, owed1 = 0;
                long packT = 0, plainT = 0, packC = 0, plainC = 0, goldT = 0, goldC = 0, excise = 0, waitShelfN = 0, waitPurseN = 0;
                foreach (var f in _fk)
                {
                    try
                    {
                        var town = f.St != null ? f.St.Town : null;
                        if (town == null) continue;
                        if (f.Kind == 1 && !CastleLive) { castleOff++; f.HorseOwed = 0; continue; }   // zamek bez zaworu 110: koszyk BK jak dotad
                        if (!f.Cal) { noCal++; continue; }
                        // scalenie z paczka A (GL-2/GL-3): ludzie = glowy kiesy ludu, ktore placa za konia - miasto: glowy BK x Scale (wedlug ludzi rynku,
                        // MarketByPeople), zamek: CastleHeads (wedlug ludzi wsi zamku, VillageHeadsByPeople; klucz wylaczony - f.HW jak dotad). Suma swiata
                        // glow w dniu kalibracji = glowy BK (mianownik HeadNorm), wiec stado swiata bez skoku - zmienia sie tylko podzial na osady
                        double people = f.Kind == 1 ? Math.Max(0, CastleHeads(f))
                                                    : Math.Max(0, f.HW + f.HR + f.HP + f.HS) * (f.ADay == _liveDay ? Math.Max(0, f.DR) : Scale(f));
                        if (double.IsNaN(people) || double.IsInfinity(people)) people = 0;
                        double add = people / WhPeople / WhYears / YearDays;
                        double owed = Math.Max(0, f.HorseOwed);
                        owed0 += owed;
                        owed += add;
                        if (owed > WhMax) { lost += owed - WhMax; owed = WhMax; }
                        if (f.Kind == 0) { towns++; peopleT += people; accrT += add; } else { castles++; peopleC += people; accrC += add; }
                        int want = (int)Math.Floor(owed + 1e-9);
                        var shelf = f.St.ItemRoster;
                        for (int k = 0; k < want; k++)
                        {
                            EquipmentElement el; int price; bool pack;
                            if (!WhCheapest(town, shelf, out el, out price, out pack)) { waitShelf++; waitShelfN += want - k; break; }
                            long avail = WhAvail(f);
                            if (!WhSplit(price, avail)) { waitPurse++; waitPurseN += want - k; break; }
                            int cost, first, last, p0 = price;
                            if (ShelfBuy.Take(shelf, el, 1, p0, () => p0, out cost, out first, out last, p0, false) <= 0) { waitShelf++; waitShelfN += want - k; break; }
                            // zaplata: klasy -> kasa osady (miasto: akcyza do gminy); cost == price (jedna sztuka po cenie znanej)
                            long ex = f.Kind == 0 ? (long)Math.Round(cost * exc) : 0, keep = cost - ex;
                            if (f.Kind == 0) { f.KW -= _whP[0]; f.KR -= _whP[1]; f.KP -= _whP[2]; f.KG += ex; }
                            else f.KW -= _whP[0];
                            if (keep > 0) town.ChangeGold((int)keep);
                            _ctlOut += keep; f.PaidKeep += keep; f.LDayOut += cost;
                            owed -= 1.0;
                            if (f.Kind == 0) { if (pack) packT++; else plainT++; goldT += cost; excise += ex; }
                            else { if (pack) packC++; else plainC++; goldC += cost; }
                            if (book) HorseBook.NoteWork(f.Kind == 0, HorseBook.Cat(el.Item), 1, cost);
                        }
                        f.HorseOwed = Math.Max(0, owed);
                        owed1 += f.HorseOwed;
                        if (f.HorseOwed >= 1.0) owing++;
                    }
                    catch (Exception e) { _whStumbles++; Stumble("WorkHorsesDaily(osada)", e); }
                }
                _whLostAll += lost;
                long bought = packT + plainT + packC + plainC;
                double ctl = owed1 - (owed0 + accrT + accrC - bought - lost);
                var sb = new StringBuilder(900);
                sb.Append("Konie robocze (K1-3a): dzien ").Append(day).Append(" | CZYNNE | miasta ").Append(towns).Append(": glowy kiesy ludu (wedlug ludzi rynku) ").Append(N0(peopleT))
                  .Append(", zamki ").Append(castles).Append(": podzamcza ").Append(N0(peopleC))
                  .Append(" | stado robocze (1 kon na ").Append(WhPeople.ToString("0", Inv)).Append(" ludzi, zyje ").Append(WhYears.ToString("0", Inv)).Append(" lat) ")
                  .Append(N0((peopleT + peopleC) / WhPeople))
                  .Append(" | przyrost licznika dzis ").Append(F2(accrT + accrC)).Append(" (miasta ").Append(F2(accrT)).Append(", zamki ").Append(F2(accrC)).Append(")")
                  .Append(" | kupione dzis ").Append(bought).Append(": miasta juczne ").Append(packT).Append(" / zwykle ").Append(plainT).Append(" za ").Append(goldT)
                  .Append(" zl (akcyza do gmin ").Append(excise).Append("), zamki juczne ").Append(packC).Append(" / zwykle ").Append(plainC).Append(" za ").Append(goldC).Append(" zl")
                  .Append(" | czeka: pusta polka ").Append(waitShelf).Append(" osad (").Append(waitShelfN).Append(" szt.), za mala kiesa ponad jutrzejszy koszyk ")
                  .Append(waitPurse).Append(" osad (").Append(waitPurseN).Append(" szt.)")
                  .Append(" | licznik razem ").Append(F2(owed1)).Append(" (wczoraj ").Append(F2(owed0)).Append("; osad z licznikiem >= 1: ").Append(owing)
                  .Append("; sufit ").Append(WhMax).Append(" na osade), przepadlo ponad sufit dzis ").Append(F2(lost)).Append(" (sesja ").Append(F2(_whLostAll)).Append(")")
                  .Append(" | kontrola licznika r = ").Append(F2(Math.Abs(ctl) < 0.005 ? 0 : ctl))
                  .Append(" | koszyk BK: wiersze polki z budzetem koni wyzerowanym ").Append(_whZero)
                  .Append(" | poza regula: zamki bez zaworu 110 ").Append(castleOff).Append(", osady nieskalibrowane ").Append(noCal)
                  .Append(" | ksiega koni (Stage6MeasureLog): ").Append(book ? "starosc koni roboczych" : "wylaczona")
                  .Append(" | czas ").Append(Ms(Stopwatch.GetTimestamp() - t0)).Append(" ms | potkniecia ").Append(_whStumbles).Append('.');
                Log.Info(sb.ToString());
                _whZero = 0;
            }
            catch (Exception e) { _whStumbles++; Stumble("WorkHorsesDaily", e); }
        }
    }
}
