using System;
using System.Diagnostics;
using System.Globalization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// CENY HURTU (decyzja Jeffa 09.10 ok. 08:00): jedna zasada dla wszystkich - kazda kupowana sztuka po swojej cenie z popytu i podazy,
    /// cena liczona od nowa po kazdej sztuce, jak u gracza na ekranie handlu; bez ukrytego rabatu hurtowego AI. Dotad AiGear (braki i sztuki
    /// zastepcze lordow i zalog), Stajnia AI i budowy placily cene PIERWSZEJ sztuki za cala partie (n = min(...), zaplata cena x n).
    ///
    /// Sposob: ten sam, ktorym gra sprzedaje partii towar z polki (SellItemsAction.ApplyInternal, dekompilacja 1.4.8): w petli po sztukach
    /// najpierw cena (Town.GetItemPrice / TownMarketData.GetPrice), potem ItemRoster.AddToCounts(sztuka, -1), nastepna sztuka. Odjecie sztuki
    /// z polki PRZED kolejna wycena wystarcza - gra i my liczymy cene z biezacego zapasu:
    ///  - gra: AddToCounts -> ItemRoster.OnRosterUpdated -> RosterUpdatedEvent -> Town.OnInventoryUpdated -> TownMarketData.OnTownInventoryUpdated
    ///    (InStoreValue kategorii od razu mniejsza; paczka 121 - HistoricalPrices.ShelfPostfix poprawia to samo zdarzenie na wage sztuki);
    ///  - Armoury: SupplyDemand.Stock liczy koszyk (typ x tier) z zywej polki przy kazdej wycenie (zamrozona polka tylko przy otwartym ekranie handlu gracza).
    /// Zadnego rachunku "polka minus wziete" po naszej stronie (jak SupplyDemand.Hold przy wycenie zamowienia kowala) - polka naprawde maleje.
    ///
    /// Wydajnosc: petla po sztukach = jedna wycena wiecej na kazda sztuke ponad pierwsza w partii (pierwsza - cena z przegladu polki, "known").
    /// Linia doby "Ceny hurtu (09.10)" mierzy: partie, sztuki, wyceny dodatkowe, czas w petli (z AddToCounts) i doplate wobec dawnej ceny
    /// pierwszej sztuki. Wolajacy (AiGear, Stajnia, budowy) ograniczaja partie jak dotad (limit sztuk na wizyte, brak, budzet).
    /// </summary>
    internal static class ShelfBuy
    {
        // liczniki doby (tylko log)
        private static int _dBatches, _dMulti, _dPieces, _dEvals, _dBack;
        private static long _dCost, _dFlat, _dTicks;

        internal static void Reset() { _dBatches = _dMulti = _dPieces = _dEvals = _dBack = 0; _dCost = _dFlat = _dTicks = 0; }

        // E8/P2 (244): slad cen kolejnych sztuk OSTATNIEJ partii Take (zerowany na wejsciu kazdego Take) - czyta go pomiar "ZakupyAI cena do wartosci"
        // (Measure174b.NoteBatch) zaraz po Take; tylko zapis do tablicy, bez wplywu na partie
        private static int[] _trace = new int[32];
        private static int _traceN, _traceSum;
        internal static int TraceCount { get { return _traceN; } }
        internal static int TraceSum { get { return _traceSum; } }
        internal static int TraceAt(int i) { return i >= 0 && i < _traceN ? _trace[i] : 0; }

        /// <summary>
        /// Zdejmuje z polki do maxN sztuk el, sztuka po sztuce: przed kazda cena od nowa (price - ta sama funkcja ceny, ktorej miejsce
        /// uzywalo dotad), sztuka tylko gdy cena miesci sie w reszcie budzetu. known &gt; 0 - cena pierwszej sztuki policzona przed chwila
        /// przy tej samej polce (przeglad kandydatow), bez drugiej wyceny. Zwraca liczbe zdjetych sztuk; cost - ich suma, first i last -
        /// cena pierwszej i ostatniej. Wyjatek w wycenie konczy partie na sztukach juz zdjetych (wolajacy je oplaca). Sztuki zostaja zdjete -
        /// przy nieudanej dostawie wolajacy oddaje je przez PutBack. tally = false (sklad8-p: wywoz kupcow SupplyDemand.DailyTrade) - bez licznikow
        /// linii "Ceny hurtu" (ta linia mierzy zakupy AI, Stajni i budow; wywoz ma swoja linie "PodazPopyt"). cap &gt; 0 (E8/Z, 251: sufit ceny zakupow
        /// brakow AI - AiBuyRules.CapPrice) - sztuka drozsza niz cap konczy partie tak jak brak budzetu (takze pierwsza, z ceny known); 0 - bez sufitu.
        /// </summary>
        internal static int Take(ItemRoster shelf, EquipmentElement el, int maxN, int budget, Func<int> price, out int cost, out int first, out int last, int known = 0, bool tally = true, int cap = 0)
        {
            cost = 0; first = 0; last = 0;
            _traceN = 0; _traceSum = 0;   // E8/P2: slad cen tej partii (tylko pomiar)
            if (shelf == null || el.Item == null || price == null || maxN <= 0 || budget <= 0) return 0;
            long t0 = Stopwatch.GetTimestamp();
            int n = 0;
            try
            {
                int at = shelf.FindIndexOfElement(el);
                int have = at >= 0 ? shelf.GetElementNumber(at) : 0;   // nie wiecej, niz naprawde lezy (AddToCounts sztuki, ktorej nie ma = assert gry)
                maxN = Math.Min(maxN, have);
                while (n < maxN)
                {
                    int p;
                    if (n == 0 && known > 0) p = known;
                    else
                    {
                        try { p = price(); } catch { break; }
                        if (n > 0 && tally) _dEvals++;   // wycena po zdjeciu sztuki - tej dawna regula (cena pierwszej x n) nie robila
                    }
                    if (p <= 0 || p > budget - cost || (cap > 0 && p > cap)) break;   // E8/Z (251): cap - sufit ceny sztuki (0 = bez sufitu)
                    bool gone = true;
                    object idx0 = ShelfIndex.OwnBefore(shelf);   // E8/O1 (265): pamiec polki aktualna tuz przed wlasnym zdjeciem (albo null)
                    try { shelf.AddToCounts(el, -1); ShelfIndex.NoteOwn(shelf); ShelfIndex.OwnAfter(shelf, idx0, el, -1); }   // E8/0: zrodlo przebudowy (licznik); E8/O1: indeks -1 zamiast przebudowy
                    catch
                    {
                        // wyjatek w zdarzeniu albo podsluchu polki: sztuka liczy sie tylko, jesli naprawde zeszla (wolajacy ja oplaca), i partia sie konczy
                        int i2 = shelf.FindIndexOfElement(el);
                        gone = (i2 >= 0 ? shelf.GetElementNumber(i2) : 0) < have - n;
                        maxN = n + (gone ? 1 : 0);
                    }
                    if (!gone) break;
                    if (n == 0) first = p;
                    last = p; cost += p; n++;
                    if (_traceN == _trace.Length) Array.Resize(ref _trace, _traceN * 2);   // E8/P2: slad cen (tylko pomiar)
                    _trace[_traceN++] = p; _traceSum += p;
                }
            }
            finally { if (tally) _dTicks += Stopwatch.GetTimestamp() - t0; }
            if (n > 0 && tally)
            {
                _dBatches++; _dPieces += n; _dCost += cost; _dFlat += (long)first * n;
                if (n > 1) _dMulti++;
            }
            return n;
        }

        /// <summary>Sztuki zdjete przez Take wracaja na polke (dostawa sie nie udala - nikt nie placi); licznik doby bez nich.</summary>
        internal static void PutBack(ItemRoster shelf, EquipmentElement el, int n, int cost, int first)
        {
            if (shelf == null || n <= 0) return;
            object idx0 = ShelfIndex.OwnBefore(shelf);   // E8/O1 (265): pamiec polki aktualna tuz przed wlasnym oddaniem (albo null)
            shelf.AddToCounts(el, n);
            ShelfIndex.NoteOwn(shelf);   // E8/0: zrodlo przebudowy pamieci polki (tylko licznik)
            ShelfIndex.OwnAfter(shelf, idx0, el, n);   // E8/O1: indeks +n zamiast przebudowy (nowy stos = skok wersji o 2 - zwykla przebudowa)
            _dBatches--; _dPieces -= n; _dCost -= cost; _dFlat -= (long)first * n; _dBack += n;
            if (n > 1) _dMulti--;
        }

        /// <summary>Do logu: "120" albo "120-131 x5" (cena pierwszej i ostatniej sztuki partii).</summary>
        internal static string Prices(int n, int first, int last)
        {
            return n <= 1 ? first.ToString(CultureInfo.InvariantCulture) : first + "-" + last + " x" + n;
        }

        /// <summary>Linia doby (ArmouryBehavior, tick dobowy): pomiar petli po sztukach i doplata wobec dawnej ceny pierwszej sztuki za cala partie.</summary>
        internal static void Daily()
        {
            if (_dBatches <= 0 && _dBack <= 0) { Reset(); return; }
            var inv = CultureInfo.InvariantCulture;
            double ms = _dTicks * 1000.0 / Stopwatch.Frequency;
            long extra = _dCost - _dFlat;
            Log.Info("Ceny hurtu (09.10, kazda sztuka po swojej cenie - AiGear, Stajnia, budowy): doba do dnia " + ((int)CampaignTime.Now.ToDays - 1)
                     + " - partie " + _dBatches + " (wielosztukowe " + _dMulti + "), sztuk " + _dPieces + " za " + _dCost.ToString(inv)
                     + "; wobec dawnej ceny pierwszej sztuki za cala partie +" + extra.ToString(inv)
                     + (_dFlat > 0 ? " (+" + (100.0 * extra / _dFlat).ToString("0.0", inv) + "%)" : "")
                     + "; wyceny dodatkowe (po zdjeciu sztuki) " + _dEvals + ", czas petli (wyceny i zdjecia z polki) " + ms.ToString("0.0", inv) + " ms"
                     + (_dPieces > 0 ? " (" + (ms * 1000.0 / _dPieces).ToString("0", inv) + " us na sztuke)" : "")
                     + (_dBack > 0 ? "; oddane na polke (DTE nie przyjal) " + _dBack : "") + ".");
            Reset();
        }
    }
}
