using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// E3-G3 (noc-ekonomia3 16.10, czesc 1; klucze TownMarketToll, TownMarketTollRate, TownMarketTollCaravanRate) - MYTO TARGOWE: jedna regula dla wozow wsi
    /// i karawan. Jeff 15.10: "pieniadz nie moze sie zbierac u ludu ani wsi ... lordowie i krolestwa zbiednieja"; kryterium serii: zwrot do panow ma isc
    /// NATURALNIE od obrotu, a nie z coraz wiekszego podatku od oszczednosci.
    /// Dowod [L] (test noc-ekonomia2 21-25-46 d109195): utarg wozow wsi ok. 216-269 tys./d, kasy miast placa karawanom 176 tys./d ("Marza kupcow z karawan (P1)");
    /// gra NIE bierze zadnego podatku, gdy miasto kupuje od partii (SellItemsAction.ApplyInternal - galaz buyer IsSettlement: tylko GiveGoldAction), wiec myto
    /// nie dubluje cla (clo placi strona kupujaca od miasta - galaz seller IsSettlement).
    /// Regula: kazda sprzedaz towaru miastu albo zamkowi przez woz wsi (stawka TownMarketTollRate, 4%) albo karawane (TownMarketTollCaravanRate, 2% - karawany
    /// placa juz clo przy zakupie, marza cienka; uwaga recenzji 2) placi czesc utargu NETTO (woz: po zwrocie nadplaty 119):
    ///  - polowa do glowy rodu pana osady (ChangeHeroGold; dochod ziemi D przez RentToday - wpisany po PopulationLaw.Daily nastepnej doby, RentMerge, bo
    ///    PopulationLaw.Daily czysci RentToday na poczatku doby - uwaga recenzji 2),
    ///  - polowa do zapasu skarbca krolestwa osady (KingdomBudgetWallet; migawka 165 przesunieta - CrownIncome.ShiftSnapshot: nie wplyw dnia, wiec nie renty
    ///    180 tego dnia; wzor korona2 A, TownFolk.Guard.cs GuardCrownReserve),
    ///  - bez krolestwa - calosc panu; bez zywego pana - jego polowa do kasy osady.
    /// Jedna regula dla wszystkich partii, takze rodu gracza (112-p, 2.0b - uwaga recenzji 1; licznik "w tym rod gracza"). Zwolnione: karawany z kontraktem
    /// surowca 174 (MaterialOrders.HasContract - marza kontraktu liczona bez myta, Cost174) i wyprzedaz karawany przed rozwiazaniem (CaravanFlow.SellOut) oraz
    /// dostawa kontraktu (MaterialOrders.Deliver) - zakres ExemptBegin/End.
    /// Przelew jak PayHoardVillage: najpierw zdjecie z sakwy platnika, potem wplaty; wyjatek przy wplacie oddaje zloto platnikowi (nic z niczego, nic w nicosc).
    /// Liczniki istniejacych linii (CaravanBulk, CaravanAmmo, T1) licza utarg PRZED mytem: LastTaken - myto ostatniej sprzedazy karawany w ApplyInternal.
    /// Linie: "Myto targowe (E3-G3): start sesji" i "Myto targowe (E3-G3): dzien N"; Pompy F (MoneyLedger.Pumps.cs). Nic w zapisie (pending RentToday - sesja,
    /// zloto juz u pana). Wyjatek per sprzedaz - licznik potkniec, bez wylacznika.
    /// POPRAWKI po recenzjach (uwaga 1, powazna - ksiega kas osad): myto wozu schodzi z sakwy W SRODKU okna MoneyLedger "tabor wsi wchodzi do osady" - CartEnd
    /// przesuwa migawke okna o myto (MoneyLedger.CartTollInWindow), wiec "zaplata taborom wsi" jest brutto, a reszta kas miast TM1 i zamkow TZ1 nie spada o myto;
    /// polowa pana bez zywego pana wplacona do kasy osady jest nazwana (Note169 "myto do kas osad (E3-G3)"). CaravanRateFor - stawka dla progu zysku T1.
    /// </summary>
    internal static class TownToll
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        internal static bool On { get { var s = Settings.Current; return s != null && s.TownMarketToll; } }
        private static double RateCart { get { var s = Settings.Current; return s == null ? 0 : Math.Max(0.0, Math.Min(0.2, s.TownMarketTollRate)); } }
        private static double RateCaravan { get { var s = Settings.Current; return s == null ? 0 : Math.Max(0.0, Math.Min(0.2, s.TownMarketTollCaravanRate)); } }

        /// <summary>Myto zdjete przy ostatniej sprzedazy karawany w SellItemsAction.ApplyInternal (CaravanGoods.TradePostfix) - wolajacy licza utarg przed mytem.</summary>
        internal static int LastTaken;
        /// <summary>Czesc myta ostatniej sprzedazy, ktora wrocila do kasy tej osady (pan nie zyje) - kontrola kas linii T1 (CaravanGoods.Sell). Zeruje wolajacy.</summary>
        internal static int LastToTown;
        private static int _exempt;                                   // zakres zwolnienia (CaravanFlow.SellOut, MaterialOrders.Deliver)
        internal static void ExemptBegin() { _exempt++; }
        internal static void ExemptEnd() { if (_exempt > 0) _exempt--; }

        // woz wsi: migawka sakwy przed sprzedaza (MarketCarts.SellPrefix - wywolanie synchroniczne, bez zagniezdzen)
        private static MobileParty _cart; private static int _cart0; private static bool _cartHooked, _caravanHooked;

        // dochod ziemi D panow - do RentToday po PopulationLaw.Daily (RentMerge)
        private static readonly Dictionary<Clan, long> _rentPend = new Dictionary<Clan, long>();

        // liczniki doby
        private static int _cN, _kN, _exN, _exFlowN, _plN, _noKdN, _noLordN, _stumbles, _stumbleSess;
        private static long _cGross, _cToll, _kGross, _kToll, _plToll, _exGross, _exFlowGross, _toLord, _toCrown, _toTown, _taken, _merged, _mergedDay;
        private static bool _startDone;
        private static readonly HashSet<string> _err = new HashSet<string>();

        /// <summary>Pompy F (MoneyLedger.Daily, ta sama doba - TownToll.Daily biegnie wczesniej): myto wozow i karawan tej doby; -1 = klucz wylaczony.</summary>
        internal static long LastCart = -1, LastCaravan = -1;

        internal static void Reset()
        {
            _cart = null; _cart0 = 0; LastTaken = 0; LastToTown = 0; _exempt = 0;
            _rentPend.Clear(); _startDone = false; _stumbleSess = 0; _err.Clear(); _merged = 0;
            LastCart = LastCaravan = -1;
            ZeroDay();
        }

        private static void ZeroDay()
        {
            _cN = _kN = _exN = _exFlowN = _plN = _noKdN = _noLordN = _stumbles = 0;
            _cGross = _cToll = _kGross = _kToll = _plToll = _exGross = _exFlowGross = _toLord = _toCrown = _toTown = _taken = _mergedDay = 0;
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++; _stumbleSess++;
            try { if (_err.Add(where)) Log.Error("TownToll." + where, e); } catch { }
        }

        internal static void NoteHooks(bool cart, bool caravan) { if (cart) _cartHooked = true; if (caravan) _caravanHooked = true; }

        // ------------------------------------------------------------ woz wsi (MarketCarts.SellPrefix / SellPostfix)
        /// <summary>MarketCarts.SellPrefix (przed BK i gra): sakwa wozu przed sprzedaza. Nie rzuca.</summary>
        internal static void CartBegin(MobileParty cart)
        {
            try { _cart = cart; _cart0 = cart != null ? cart.PartyTradeGold : 0; }
            catch (Exception e) { _cart = null; Stumble("CartBegin", e); }
        }

        /// <summary>MarketCarts.SellPostfix, po zwrocie nadplaty 119 i liczniku wlokna: myto od utargu netto wozu. Nie rzuca.</summary>
        internal static void CartEnd(Settlement st, MobileParty cart)
        {
            try
            {
                if (cart == null || !ReferenceEquals(_cart, cart)) return;
                _cart = null;
                if (st == null || st.Town == null || !Live) return;
                int net = cart.PartyTradeGold - _cart0;
                if (net <= 0) return;
                int fee = HarvestFairs.CartFee(st, cart, net);            // R3-05a: oplata targowa jarmarku (1% do kasy miasta) - dziala takze bez myta
                if (!On) { MoneyLedger.CartTollInWindow(cart, fee); return; }
                int t = Take(st, cart, net, RateCart);
                MoneyLedger.CartTollInWindow(cart, t + fee);              // poprawki (uwaga recenzji 1): okno ksiegi kas osad liczy zaplate taborom brutto (R3-05a: z oplata jarmarku)
                LastToTown = 0;                                            // licznik kontroli T1 dotyczy tylko karawan
                _cN++; _cGross += net; _cToll += t;
                if (Players(cart)) { _plN++; _plToll += t; }
            }
            catch (Exception e) { _cart = null; Stumble("CartEnd", e); }
        }

        // ------------------------------------------------------------ karawana (CaravanGoods.TradePostfix na SellItemsAction.ApplyInternal)
        /// <summary>Karawana sprzedala miastu albo zamkowi towar za `gross` (przyrost kiesy w ApplyInternal). Zwraca zdjete myto. Nie rzuca.</summary>
        internal static int TakeCaravan(Settlement st, MobileParty mp, int gross)
        {
            try
            {
                if (mp == null || st == null || st.Town == null || gross <= 0 || !Live) return 0;
                if (_exempt > 0) { if (On) { _exFlowN++; _exFlowGross += gross; } return 0; }
                if (MaterialOrders.HasContract(mp)) { if (On) { _exN++; _exGross += gross; } return 0; }
                // R3-05a: oplata targowa jarmarku (1% do kasy miasta) - te same zwolnienia co myto; czesc wracajaca do kasy osady (kontrola T1 w CaravanGoods.Sell)
                int fee = HarvestFairs.CaravanFee(st, mp, gross);
                if (fee > 0) LastToTown += fee;
                if (!On) return fee;
                int t = Take(st, mp, gross, RateCaravan);
                _kN++; _kGross += gross; _kToll += t;
                if (Players(mp)) { _plN++; _plToll += t; }
                return t + fee;
            }
            catch (Exception e) { Stumble("TakeCaravan", e); return 0; }
        }

        private static bool Live { get { var c = Campaign.Current; return c != null && c.GameStarted; } }

        /// <summary>Stawka myta, ktora zejdzie z utargu tej karawany przy sprzedazy miastu teraz (0 - klucz wylaczony, zakres zwolnienia, kontrakt 174) - prog zysku T1
        /// (CaravanGoods.Sell; uwaga recenzji 1: marza 0-2% bez myta byla strata netto). Nie rzuca.</summary>
        internal static double CaravanRateFor(MobileParty mp)
        {
            try
            {
                if (!On || mp == null || _exempt > 0 || MaterialOrders.HasContract(mp)) return 0;
                return RateCaravan;
            }
            catch { return 0; }
        }

        private static bool Players(MobileParty mp)
        {
            try { return mp != null && mp.ActualClan != null && mp.ActualClan == Clan.PlayerClan; } catch { return false; }
        }

        /// <summary>Wspolny przelew: sakwa platnika -> pan osady (polowa) + zapas korony (polowa); bez krolestwa calosc panu, bez zywego pana jego polowa do kasy.</summary>
        private static int Take(Settlement st, MobileParty payer, int gross, double rate)
        {
            int toll = (int)Math.Floor(gross * rate);
            if (toll <= 0) return 0;
            toll = Math.Min(toll, Math.Max(0, payer.PartyTradeGold));
            if (toll <= 0) return 0;
            var clan = st.OwnerClan;
            var lord = clan != null ? clan.Leader : null;
            bool lordOk = lord != null && lord.IsAlive;
            var kd = clan != null ? clan.Kingdom : null;
            if (kd != null && kd.IsEliminated) kd = null;
            long cp = kd != null ? toll / 2 : 0;
            long lp = toll - cp;
            long tp = 0;
            if (!lordOk) { tp = lp; lp = 0; _noLordN++; }
            if (kd == null) _noKdN++;
            // ---- przelew: najpierw zdjecie z platnika, potem wplaty; wyjatek przy wplacie oddaje zloto platnikowi
            int had = payer.PartyTradeGold;
            payer.PartyTradeGold = had - toll;
            int got = had - payer.PartyTradeGold;
            if (got != toll) { payer.PartyTradeGold = had; return 0; }
            if (lp > 0)
            {
                try { lord.ChangeHeroGold((int)lp); }
                catch (Exception e) { payer.PartyTradeGold += (int)lp; toll -= (int)lp; lp = 0; Stumble("Take(pan)", e); }
            }
            if (tp > 0)
            {
                try { st.Town.ChangeGold((int)tp); }
                catch (Exception e) { payer.PartyTradeGold += (int)tp; toll -= (int)tp; tp = 0; Stumble("Take(kasa)", e); }
                if (tp > 0)
                {
                    LastToTown += (int)tp;
                    MoneyLedger.Note169(MoneyLedger.N169Toll, st, (int)tp);   // poprawki (uwaga recenzji 1): wplata do kasy osady poza tickiem - nazwana w "Przeplywy osad (przyczyny)"
                }
            }
            if (cp > 0)
            {
                try { kd.KingdomBudgetWallet += (int)cp; }
                catch (Exception e) { payer.PartyTradeGold += (int)cp; toll -= (int)cp; cp = 0; Stumble("Take(korona)", e); }
                if (cp > 0) CrownIncome.ShiftSnapshot(kd, cp);          // zapas korony, nie wplyw dnia (nie renty 180 tego dnia)
            }
            try
            {
                if (lp > 0)
                {
                    long r0; _rentPend.TryGetValue(clan, out r0); _rentPend[clan] = r0 + lp;   // dochod ziemi D - RentToday po PopulationLaw.Daily (RentMerge)
                    CirculationWindows.NoteHeroGold(lord, lp);
                }
                _toLord += lp; _toTown += tp; _toCrown += cp; _taken += lp + tp + cp;
            }
            catch (Exception e) { Stumble("Take(liczniki)", e); }
            return Math.Max(0, toll);
        }

        /// <summary>ArmouryBehavior, zaraz po Seal31B.RentMerge (PopulationLaw.Daily czysci RentToday): myto panow od wczoraj jako dochod ziemi D (powinnosci 165, budowy,
        /// ksiega D rodow czytaja RentToday).</summary>
        internal static void RentMerge()
        {
            try
            {
                foreach (var kv in _rentPend)
                {
                    if (kv.Key == null || kv.Key.IsEliminated || kv.Value <= 0) continue;
                    int r0; PopulationLaw.RentToday.TryGetValue(kv.Key, out r0);
                    PopulationLaw.RentToday[kv.Key] = (int)Math.Min(int.MaxValue, (long)r0 + kv.Value);
                    _merged += kv.Value; _mergedDay += kv.Value;
                }
            }
            catch (Exception e) { Stumble("RentMerge", e); }
            finally { _rentPend.Clear(); }
        }

        // ------------------------------------------------------------ doba
        internal static void Daily()
        {
            var s = Settings.Current;
            if (s == null || Campaign.Current == null) return;
            try
            {
                int day = (int)CampaignTime.Now.ToDays - 1;
                if (!_startDone)
                {
                    _startDone = true;
                    Log.Info("Myto targowe (E3-G3): start sesji | klucz Town Market Toll " + (s.TownMarketToll ? "tak" : "nie") + ", stawka wozy wsi " + (100 * RateCart).ToString("0.0", Inv)
                             + "% (Town Market Toll Rate), karawany " + (100 * RateCaravan).ToString("0.0", Inv) + "% (Town Market Toll Caravan Rate) | od utargu netto sprzedazy miastu i zamkowi:"
                             + " polowa glowie rodu pana osady (dochod ziemi D od nastepnej doby), polowa do zapasu skarbca krolestwa osady (migawka 165 przesunieta - nie renty tego dnia);"
                             + " bez krolestwa calosc panu, bez zywego pana jego polowa do kasy osady | jedna regula dla wszystkich, takze rodu gracza; zwolnione: kontrakty 174, wyprzedaz"
                             + " przed rozwiazaniem (CaravanFlow), dostawy kontraktow | latki: sprzedaz wozow " + (_cartHooked ? "wpieta" : "NIE") + " (MarketCarts SellGoodsForTradeAction),"
                             + " sprzedaz karawan " + (_caravanHooked ? "wpieta" : "NIE") + " (CaravanGoods SellItemsAction.ApplyInternal).");
                }
                LastCart = s.TownMarketToll ? _cToll : -1; LastCaravan = s.TownMarketToll ? _kToll : -1;
                long diff = _cToll + _kToll - _taken;
                var sb = new StringBuilder(900);
                sb.Append("Myto targowe (E3-G3): dzien ").Append(day).Append(" | ").Append(s.TownMarketToll ? "CZYNNE" : "WYLACZONE (MCM)")
                  .Append(" | stawka wozy ").Append((100 * RateCart).ToString("0.0", Inv)).Append("% / karawany ").Append((100 * RateCaravan).ToString("0.0", Inv)).Append('%')
                  .Append(" | wozy wsi: sprzedazy ").Append(_cN).Append(", utarg netto ").Append(_cGross).Append(", myto ").Append(_cToll)
                  .Append(" | karawany: sprzedazy ").Append(_kN).Append(", utarg ").Append(_kGross).Append(", myto ").Append(_kToll)
                  .Append(" | w tym rod gracza: sprzedazy ").Append(_plN).Append(", myto ").Append(_plToll)
                  .Append(" | do glow rodow ").Append(_toLord).Append(" (dochod ziemi D jutro; wpisane dzis z wczoraj ").Append(_mergedDay).Append("), do zapasu koron ").Append(_toCrown)
                  .Append(", do kas osad (bez zywego pana) ").Append(_toTown).Append(" | osad bez krolestwa ").Append(_noKdN).Append(", bez zywego pana ").Append(_noLordN)
                  .Append(" | zwolnione: kontrakty 174 ").Append(_exN).Append(" (utarg ").Append(_exGross).Append("), wyprzedaz i dostawy ").Append(_exFlowN).Append(" (utarg ").Append(_exFlowGross).Append(')')
                  .Append(" | kontrola: pobrano ").Append(_cToll + _kToll).Append(" = pan + korona + kasa ").Append(_taken).Append(" (roznica ").Append(diff).Append(')')
                  .Append(" | wplyw na skrzynie: tabory wsi -").Append(_cToll).Append(", karawany -").Append(_kToll).Append(", glowy rodow +").Append(_toLord)
                  .Append(", skarbce koron +").Append(_toCrown).Append(", kasy osad +").Append(_toTown)
                  .Append(" | potkniecia ").Append(_stumbles).Append(" (sesja ").Append(_stumbleSess).Append(").");
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Stumble("Daily", e); }
            finally { ZeroDay(); }
        }
    }
}
