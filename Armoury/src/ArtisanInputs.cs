using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Workshops;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// RZEMIESLNICY BK: KAZDA SZTUKA Z WLASNEGO WSADU (paczka 147, plan K13 07.10 - koniec mnoznika z niczego).
    ///
    /// Stan przed paczka (gra 1.4.8 WorkshopsCampaignBehavior i BannerKings.dll EconomyPatches.WorkshopsCampaignBehaviorPatches):
    ///  - cykl ukrytego warsztatu "artisans" (TickOneProductionCycleForNotableWorkshop) bierze wsad linii RAZ (ConsumeInputFromTownMarket;
    ///    prefiks BK: warsztat placi kasie miasta cene miasta za kazda sztuke wsadu), potem gra woli ProduceAnOutputToTown raz na kazda sztuke
    ///    wyrobu z receptury (output_count) i po kazdym wywolaniu OnItemProduced(wyrob, miasto, 1);
    ///  - prefiks BK ProduceOutputPrefix pomija gre i dla "artisans" dopisuje polce miasta count = (int)max(1, 1 + rzemieslnicy/45/wartosc)
    ///    sztuk wyrobu (bez modyfikatora), kasa miasta placi warsztatowi cene kupna miasta za kazda (x mnoznik ceny losowej jakosci), potem
    ///    OnItemProduced(wyrob, miasto, count). Po nowej monecie (chleb 6 d, piwo 3, garnki 8, mieso 9, skory 10) to ok. 5 chlebow, 9 piw,
    ///    4 garnki, 3 miesa i 3 skory z JEDNEJ sztuki wsadu - reszta z niczego, a kasa miasta za nia placi;
    ///  - zysk rzemieslnikow (kapital ponad startowy) gra wyplaca co dobe notablowi-wlascicielowi (DefaultClanFinanceModel: ProfitMade / 5).
    ///
    /// Regula (wlacznik ArtisanOwnInputs): count zostaje jako MOC rzemieslnikow - ilu cykli moga dzis dokonac. Pierwszy cykl ma wsad gry;
    /// kazdy dodatkowy (count - 1) bierze SWOJ wsad z polki miasta ta sama droga co pierwszy (metoda gry ConsumeInputFromTownMarket - z BK
    /// zaplata warsztat -> kasa miasta po cenie miasta i zdarzenie OnItemConsumed), pod tym samym warunkiem co kazdy cykl ukrytego warsztatu
    /// w grze: wsad na polce (DetermineItemRosterHasSufficientInputs gry), po starcie gry utarg cyklu wedle cen sprzedazy > koszt wsadu,
    /// kapital >= koszt wsadu. Na cykle bez wsadu albo bez zysku sztuk nie ma: zdjete z polki, a warsztat oddaje kasie miasta to, co BK
    /// za nie zaplacil (czesc utargu tego wywolania wedle liczby sztuk). Recenzja: nadwyzka BK schodzi z polki PRZED decyzja, a kazdy
    /// dodatkowy cykl z wlasnym wsadem odklada na polke swoja sztuke - cykl ocenia rynek z tym, co naprawde powstalo (jak kolejne cykle gry),
    /// a nie z (count - 1) sztukami, ktorych nie bedzie (te zbijaly cene wyrobu i odcinaly cykle na plytkim rynku, np. piwo).
    /// Liczbe cykli ustala PIERWSZY wyrob cyklu; kazde nastepne wywolanie
    /// (wyrob x output_count) przyciete do tej liczby - krowa: 6 miesa i 2 skory na cykl, nie 6 x 3 + 2 x 3 z jednej krowy.
    /// Bez zmian: jakosc BK, linie bez wsadu (WorkshopNoFreeRaw), linie uzbrojenia (WorkshopLaw), kopalnie, warsztaty notabli i gracza.
    /// Zloto tylko miedzy dwiema kasami, ktore rusza BK (kapital rzemieslnikow i kasa miasta); MoneyLedger widzi to jako osobna pozycje.
    /// Wylaczone - gra i BK jak dotad (zadna latka nie zmienia stanu).
    /// </summary>
    internal static class ArtisanInputs
    {
        internal static bool On { get { var s = Settings.Current; return s != null && s.ArtisanOwnInputs && _wired; } }
        private static bool Live { get { var c = Campaign.Current; return c != null && c.GameStarted; } }

        // metody gry, przez ktore idzie dodatkowy cykl (te same, co pierwszy cykl gry) - raz na proces
        private static MethodInfo _sufficient, _consume;
        private static bool _wired;

        /// <summary>Paczka 169 (tylko log): true, gdy Decide kupuje wsad dodatkowych cykli przez ConsumeInputFromTownMarket. Okno O36
        /// (CirculationWindows.WorkshopInPost) nie dopisuje wtedy zmiany kasy miasta do linii kas - stara ksiega zna te kwote z NoteArtisans.</summary>
        internal static bool InDecide;

        // biezacy cykl rzemieslnikow - gra wola cykle po kolei w jednym watku (jak rachunek cyklu 123)
        private static Workshop _w;
        private static WorkshopType.Production _p;
        private static bool _eff, _open, _armed;
        private static int _cycles;                      // -1 = jeszcze nieustalone (pierwszy wyrob cyklu je ustala)
        private static ItemObject _item;                 // wyrob biezacego wywolania ProduceAnOutputToTown
        private static int _shelf0, _cap0;               // polka wyrobu (sztuki bez modyfikatora) i kapital przed prefiksem BK

        // linia dnia
        private static int _dCycles, _dMulti, _dCap, _dExtra, _dNoInput, _dNoProfit, _dNoGold, _dStumbles;
        private static long _dPaidIn, _dRefund, _dRefundShort;
        private static readonly Dictionary<string, int> _dUsed = new Dictionary<string, int>();
        private static readonly Dictionary<string, int[]> _dOut = new Dictionary<string, int[]>();   // wyrob -> [BK dopisal, zdjete]
        private static readonly HashSet<string> _errOnce = new HashSet<string>();

        /// <summary>Nowa gra / wczytanie (z WorkshopTrade.Reset, czyli z konstruktora ArmouryBehavior).</summary>
        internal static void Reset()
        {
            _w = null; _open = _armed = false; _cycles = -1; _item = null;
            _errOnce.Clear();
            ClearDay();
        }

        private static void ClearDay()
        {
            _dCycles = _dMulti = _dCap = _dExtra = _dNoInput = _dNoProfit = _dNoGold = _dStumbles = 0;
            _dPaidIn = _dRefund = _dRefundShort = 0;
            _dUsed.Clear(); _dOut.Clear();
        }

        /// <summary>Wyjatek przy jednym cyklu: pierwszy z danego miejsca do pliku, kazdy liczony w linii dnia (regula dziala dalej).</summary>
        private static void Stumble(string where, Exception e)
        {
            _dStumbles++;
            if (_errOnce.Add(where)) Log.Error(where, e);
        }

        /// <summary>Wolane z WorkshopTrade.ApplyAll: regula czynna tylko przy wpietych latkach cyklu i wyrobu (WorkshopTrade) i znanych
        /// metodach gry. Zwraca opis do linii startowej.</summary>
        internal static string Wire(bool cyclePatched, bool outPatched)
        {
            var b = typeof(WorkshopsCampaignBehavior);
            _sufficient = AccessTools.Method(b, "DetermineItemRosterHasSufficientInputs");
            _consume = AccessTools.Method(b, "ConsumeInputFromTownMarket");
            _wired = cyclePatched && outPatched && _sufficient != null && _consume != null;
            var s = Settings.Current;
            if (!_wired)
                return "Rzemieslnicy BK (147): NIECZYNNE - brak " + (!cyclePatched ? "latki cyklu notabla " : "") + (!outPatched ? "latki wyrobu do miasta " : "")
                       + (_sufficient == null ? "DetermineItemRosterHasSufficientInputs " : "") + (_consume == null ? "ConsumeInputFromTownMarket " : "") + "- mnoznik BK jak dotad.";
            return "Rzemieslnicy BK (147): kazda sztuka z wlasnego wsadu " + (s != null && s.ArtisanOwnInputs ? "WLACZONA" : "wylaczona w MCM (mnoznik BK jak dotad)")
                   + " - liczba sztuk BK = moc rzemieslnikow, kazdy dodatkowy cykl bierze swoj wsad z polki miasta (ConsumeInputFromTownMarket gry, warunek cyklu ukrytego warsztatu gry), reszta sztuk zdjeta, zaplata BK wraca do kasy miasta.";
        }

        // ------------------------------------------------------------ cykl (z WorkshopTrade.CycleStartPrefix / CyclePostfix)
        internal static void CycleStart(WorkshopType.Production p, Workshop w, bool eff)
        {
            _open = false; _w = null; _armed = false; _item = null;
            try
            {
                if (!On || w == null || w.WorkshopType == null || w.WorkshopType.StringId != "artisans") return;
                if (p.Inputs == null || p.Inputs.Count == 0 || p.Outputs == null || p.Outputs.Count == 0) return;   // linia bez wsadu - nie nasza (Production to struct)
                if (w.Settlement == null || w.Settlement.Town == null || w.Settlement.Town.Owner == null) return;
                _w = w; _p = p; _eff = eff; _cycles = -1; _open = true;
            }
            catch (Exception e) { Stumble("ArtisanInputs.CycleStart", e); }
        }

        internal static void CycleEnd()
        {
            _open = false; _w = null; _armed = false; _item = null;
        }

        // ------------------------------------------------------------ wyrob (z WorkshopTrade.OutPrefix / OutPostfix, pierwszenstwo First - przed BK)
        private static int Plain(ItemRoster r, ItemObject it)
        {
            int i = r.FindIndexOfElement(new EquipmentElement(it));
            return i >= 0 ? r.GetElementNumber(i) : 0;
        }

        internal static void OutPre(EquipmentElement e, Workshop w)
        {
            _armed = false;
            if (!_open || w == null || w != _w) return;
            try
            {
                if (e.Item == null) return;
                _item = e.Item;
                _shelf0 = Plain(w.Settlement.Town.Owner.ItemRoster, _item);
                _cap0 = w.Capital;
                _armed = true;
            }
            catch (Exception ex) { Stumble("ArtisanInputs.OutPre", ex); }
        }

        internal static void OutPost(WorkshopsCampaignBehavior beh, Workshop w)
        {
            if (!_armed) return;
            _armed = false;
            if (!_open || w == null || w != _w || _item == null) return;
            try
            {
                var town = w.Settlement.Town;
                var roster = town.Owner.ItemRoster;
                int added = Plain(roster, _item) - _shelf0;          // tyle sztuk dopisal prefiks BK (gra bez BK: 1)
                long credit = (long)w.Capital - _cap0;               // tyle kasa miasta zaplacila za nie (0 przed startem gry i w linii bez zlota)
                if (added <= 0) return;
                int[] o;
                if (!_dOut.TryGetValue(_item.StringId, out o)) { o = new int[2]; _dOut[_item.StringId] = o; }
                o[0] += added;
                int remove;
                if (_cycles < 0)
                {
                    // pierwszy wyrob cyklu: cala nadwyzka BK schodzi z polki PRZED decyzja, kazdy dodatkowy cykl z wlasnym wsadem
                    // odklada swoja sztuke z powrotem (Decide) - zostaje tyle sztuk, ile cykli
                    int surplus = Math.Min(added - 1, Plain(roster, _item));
                    if (surplus > 0) roster.AddToCounts(new EquipmentElement(_item), -surplus);
                    _cycles = 1;
                    _dCycles++;
                    remove = surplus;
                    if (surplus > 0)
                    {
                        _dMulti++; _dCap += surplus;
                        remove = surplus - Decide(beh, w, town, roster, surplus);
                    }
                }
                else
                {
                    remove = Math.Min(added - _cycles, Plain(roster, _item));
                    if (remove > 0) roster.AddToCounts(new EquipmentElement(_item), -remove);
                }
                if (remove <= 0) return;
                o[1] += remove;
                if (credit <= 0) return;
                long due = (long)Math.Round((double)credit * remove / added, MidpointRounding.AwayFromZero);
                int back = (int)Math.Min(due, (long)Math.Max(0, w.Capital));
                if (back > 0)
                {
                    w.ChangeGold(-back);
                    town.ChangeGold(back);
                    MoneyLedger.NoteArtisans(w.Settlement, 0, back);
                    _dRefund += back;
                }
                if (back < due) _dRefundShort += due - back;
            }
            catch (Exception ex) { Stumble("ArtisanInputs.OutPost", ex); }
        }

        /// <summary>Pierwszy wyrob cyklu (nadwyzka BK juz zdjeta z polki): ile z (count - 1) dodatkowych cykli rzemieslnicy naprawde robia.
        /// Kazdy bierze swoj wsad z polki ta sama droga i pod tym samym warunkiem, co cykl ukrytego warsztatu w grze, przy cenach z tej
        /// chwili (polka wyrobu z sztukami cykli juz zrobionych), i odklada na polke swoja sztuke. Zwraca liczbe zrobionych; wyjatek konczy
        /// liczenie (potkniecie w linii dnia), reszta nadwyzki zostaje zdjeta.</summary>
        private static int Decide(WorkshopsCampaignBehavior beh, Workshop w, Town town, ItemRoster roster, int extra)
        {
            int made = 0;
            InDecide = true;   // paczka 169: tylko znacznik dla okna O36 (niczego nie zmienia w cyklu)
            try
            {
                for (int j = 0; j < extra; j++)
                {
                    var args = new object[] { _p, roster, town, 0 };
                    bool enough = (bool)_sufficient.Invoke(beh, args);
                    int cost = (int)args[3];
                    if (!enough) { _dNoInput += extra - j; break; }
                    if (Live && CycleIncome(town) <= cost) { _dNoProfit += extra - j; break; }   // gra: ukryty warsztat rusza cykl, gdy utarg > wsad (po starcie gry)
                    if (w.Capital < cost) { _dNoGold += extra - j; break; }
                    int cap = w.Capital;
                    foreach (var i in _p.Inputs)
                    {
                        if (i.Item1 == null || i.Item2 <= 0) continue;
                        _consume.Invoke(beh, new object[] { i.Item1, i.Item2, town, w, _eff });
                        int n; _dUsed.TryGetValue(i.Item1.StringId, out n); _dUsed[i.Item1.StringId] = n + i.Item2;
                    }
                    int paid = cap - w.Capital;
                    if (paid > 0) { _dPaidIn += paid; MoneyLedger.NoteArtisans(w.Settlement, paid, 0); }
                    roster.AddToCounts(new EquipmentElement(_item), 1);   // sztuka tego cyklu wraca na polke
                    made++;
                    _cycles++;
                    _dExtra++;
                }
            }
            catch (Exception e) { Stumble("ArtisanInputs.Decide", e); }
            finally { InDecide = false; }
            return made;
        }

        /// <summary>Utarg jednego cyklu wedle cen sprzedazy miasta - jak GetItemsToProduce gry (wyrob x output_count); pierwszy wyrob to
        /// sztuka z tego wywolania, pozostale - najtanszy towar kategorii.</summary>
        private static int CycleIncome(Town town)
        {
            int sum = 0;
            foreach (var o in _p.Outputs)
            {
                if (o.Item1 == null || o.Item2 <= 0) continue;
                var it = _item != null && _item.ItemCategory == o.Item1 ? _item : WorkshopTrade.Sample(o.Item1);
                if (it == null) continue;
                sum += o.Item2 * town.GetItemPrice(new EquipmentElement(it), null, true);
            }
            return sum;
        }

        // ------------------------------------------------------------ linia dnia
        internal static void Daily()
        {
            try
            {
                if (!On || Campaign.Current == null) { ClearDay(); return; }
                int day = (int)CampaignTime.Now.ToDays - 1;
                int removed = 0, made = 0;
                var outs = new List<string>();
                foreach (var kv in _dOut)
                {
                    made += kv.Value[0]; removed += kv.Value[1];
                    outs.Add(kv.Key + " " + kv.Value[0] + "/" + (kv.Value[0] - kv.Value[1]) + "/" + kv.Value[1]);
                }
                outs.Sort(StringComparer.Ordinal);
                var used = new List<string>();
                foreach (var kv in _dUsed) used.Add(kv.Key + " " + kv.Value);
                used.Sort(StringComparer.Ordinal);
                var sb = new StringBuilder();
                sb.Append("Rzemieslnicy BK (147): dzien ").Append(day)
                  .Append(" - cykle linii z wsadem ").Append(_dCycles).Append(" (mnoznik BK > 1 w ").Append(_dMulti).Append(")")
                  .Append("; moc ponad pierwszy cykl ").Append(_dCap).Append(" cykli: z wlasnym wsadem ").Append(_dExtra)
                  .Append(", bez wsadu na polce ").Append(_dNoInput).Append(", bez zysku (wyrob <= wsad) ").Append(_dNoProfit)
                  .Append(", bez kapitalu ").Append(_dNoGold)
                  .Append("; wsad dodatkowych cykli zjedzony z polek [").Append(used.Count > 0 ? string.Join(", ", used.ToArray()) : "nic").Append("]")
                  .Append("; wyroby (BK dopisal / zostalo / zdjete) [").Append(outs.Count > 0 ? string.Join(", ", outs.ToArray()) : "nic").Append("]")
                  .Append(", razem dopisal ").Append(made).Append(", zdjetych ").Append(removed)
                  .Append("; do kas miast z kapitalu rzemieslnikow: za wsad +").Append(_dPaidIn).Append(", zwrot zaplaty BK za zdjete +").Append(_dRefund);
                if (_dRefundShort > 0) sb.Append(" (niepelny zwrot - pusty kapital: ").Append(_dRefundShort).Append(")");
                sb.Append("; potkniecia ").Append(_dStumbles).Append('.');
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Log.Error("ArtisanInputs.Daily", e); }
            ClearDay();
        }
    }
}
