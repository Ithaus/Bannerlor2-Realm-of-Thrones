using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// E8/R (254, PLAN 8.5b, 8.5c, 8.7b; projekt etapu 8 v2 rozdz. 3 "E8/R") - RYNEK I KORONA. Trzy zmiany, kazda z wylacznikiem; w tym pliku tylko linia doby
    /// "Rynek i korona (E8/R)" i proba kup-sprzedaj konia (sam log - nie zmienia zlota, polek ani ksiag):
    /// (1) WarExpectationEnabled domyslnie false (Settings; kod oczekiwan w ArmsPricing zostaje): popyt wojenny liczyl sie dwa razy - premia oczekiwan przy
    ///     wypowiedzeniu wojny (Z33: do +63% popytu) i prawdziwe braki armii jako zamowienia (SupplyDemand.NoteUnmet). Linia: ile frakcji ma premie.
    /// (2) HorseSellFromWorth (SupplyDemand.PricePostfix / HorseSaleBase): kon przy sprzedazy - podstawa bez czynnika podazy gry, polka raz. Linia: wyceny
    ///     sprzedazy koni od poprzedniej linii (stara droga / nowa) i PROBA - w ProbePerDay osadach na dobe (kolejno po wszystkich miastach i zamkach) kupno
    ///     jednej sztuki konia z polki i jej sprzedaz z powrotem, obie drogi w jednej linii (R10); zysk <= 0 to prog R-2 (jak proba paczki 155).
    /// (3) MonopolyFromDailyProfit (KingdomTreasury.Levies / MonopolyDue): monopole korony 5% dziennej wyplaty wlascicielowi warsztatu, nie 5% stanu zysku.
    ///     Linia: wzieto, z ilu warsztatow, dzienna wyplata tych warsztatow (prog R-3: wzieto <= 1/20 wyplaty), ile wzialby stary wzor.
    /// Wolane na koncu KingdomTreasury.Levies (takt doby, watek gry); Reset z KingdomTreasury.Reset (konstruktor ArmouryBehavior). Wyjatek w jednej osadzie
    /// proby = potkniecie (licznik), nigdy wylacznik.
    /// </summary>
    internal static class MarketAndCrown
    {
        /// <summary>Ile osad dziennie sprawdza proba kup-sprzedaj (3 wyceny na osade; wszystkie miasta i zamki w kilkanascie dob).</summary>
        private const int ProbePerDay = 24;

        private static int _stumbles;
        private static readonly HashSet<string> _errSites = new HashSet<string>();
        private static List<Settlement> _places;
        private static int _cursor;
        // monopole tej doby (z KingdomTreasury.Levies)
        private static bool _mSet, _mNew;
        private static long _mTaken, _mShops, _mPayout, _mOld;

        internal static void Reset()
        {
            _stumbles = 0; _places = null; _cursor = 0;
            lock (_errSites) _errSites.Clear();
            _mSet = _mNew = false; _mTaken = _mShops = _mPayout = _mOld = 0;
            long a, b, c; SupplyDemand.TakeHorseTally(out a, out b, out c);
        }

        /// <summary>Potkniecie (moze przyjsc z watku wyceny): licznik sesji, pierwszy wyjatek z danego miejsca do logu bledow.</summary>
        internal static void Stumble(string where, Exception e)
        {
            System.Threading.Interlocked.Increment(ref _stumbles);
            bool first;
            lock (_errSites) first = _errSites.Add(where);
            if (first) Log.Error("MarketAndCrown." + where, e);
        }

        /// <summary>Monopole tej doby: czy nowy wzor, ile wzieto, z ilu warsztatow, dzienna wyplata ich wlascicielom (0 przy starym wzorze), ile dalby stary wzor.</summary>
        internal static void NoteMonopoly(bool newWay, long taken, long shops, long payout, long oldWay)
        {
            _mSet = true; _mNew = newWay; _mTaken = taken; _mShops = shops; _mPayout = payout; _mOld = oldWay;
        }

        internal static void Daily()
        {
            var s = Settings.Current;
            if (s == null || Campaign.Current == null) return;
            var ci = CultureInfo.InvariantCulture;
            var sb = new StringBuilder(720);
            sb.Append("Rynek i korona (E8/R): dzien ").Append((int)CampaignTime.Now.ToDays);

            // (1) premie oczekiwan wojennych
            float wmax = 0f; int wn = 0;
            try { wn = ArmsPricing.WarPremiumCount(out wmax); } catch (Exception e) { Stumble("WarPremiumCount", e); }
            sb.Append(" | premie wojenne [").Append(s.WarExpectationEnabled ? "WL" : "wyl").Append("]: frakcji z premia ").Append(wn)
              .Append(", najwyzsza +").Append((wmax * 100f).ToString("0", ci)).Append('%');

            // (2) kon przy sprzedazy: wyceny od poprzedniej linii i proba kup-sprzedaj
            long hn, ho, hw;
            SupplyDemand.TakeHorseTally(out hn, out ho, out hw);
            sb.Append(" | kon przy sprzedazy [").Append(SupplyDemand.HorseSellOn ? "WL" : (s.HorseSellFromWorth ? "wyl - kupno nie z wartosci" : "wyl"))
              .Append("]: wycen sprzedazy koni nowa droga ").Append(hn).Append(" (stara droga dalaby razem ").Append(ho).Append(" zl, nowa ").Append(hw).Append(" zl)");
            long t0 = Stopwatch.GetTimestamp();
            try { Probe(sb, ci); }
            catch (Exception e) { Stumble("Probe", e); sb.Append(" | proba kup-sprzedaj konia: przerwana wyjatkiem"); }
            double ms = (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency;

            // (3) monopole korony
            sb.Append(" | monopole [").Append(s.MonopolyFromDailyProfit ? "WL" : "wyl").Append("]: ");
            if (!_mSet) sb.Append("brak danych tej doby");
            else
            {
                sb.Append("wzieto ").Append(_mTaken).Append(" zl z ").Append(_mShops).Append(" warsztatow");
                if (_mNew) sb.Append(", dzienna wyplata wlascicielom tych warsztatow ").Append(_mPayout).Append(" zl");
                else sb.Append(", dzienna wyplata wlascicielom - nie liczona (stary wzor)");
                sb.Append(", stary wzor (5% stanu zysku) wzialby ").Append(_mOld).Append(" zl");
            }
            _mSet = false;

            sb.Append(" | potkniecia (sesja) ").Append(_stumbles).Append(", koszt proby ").Append(ms.ToString("0.0", ci)).Append(" ms.");
            Log.Info(sb.ToString());
        }

        /// <summary>
        /// Proba kup-sprzedaj: w kolejnych ProbePerDay miastach i zamkach pierwsza sztuka konia (ItemType Horse - takze zwierze juczne) z polki; kupno
        /// i sprzedaz z powrotem liczy SupplyDemand.ProbeHorse (lancuch cen gry, polka pomniejszona o kupiona sztuke). Klient = druzyna gracza (gdy ma
        /// dowodce), inaczej bez partii. Tylko odczyt.
        /// </summary>
        private static void Probe(StringBuilder sb, CultureInfo ci)
        {
            if (_places == null)
            {
                var l = new List<Settlement>();
                foreach (var st in Settlement.All)
                    if (st != null && (st.IsTown || st.IsCastle) && st.Town != null && st.ItemRoster != null) l.Add(st);
                _places = l;
            }
            int n = _places.Count;
            if (n == 0) { sb.Append(" | proba kup-sprzedaj konia: brak miast i zamkow"); return; }
            MobileParty client = null;
            try { client = MobileParty.MainParty; if (client != null && client.LeaderHero == null) client = null; } catch { client = null; }

            int seen = Math.Min(ProbePerDay, n), probed = 0, noHorse = 0, outside = 0, failed = 0, towns = 0;
            int maxNew = 0, maxOld = 0, gainNew = 0, gainOld = 0;
            string worst = null;
            var rNew = new List<float>(); var rOld = new List<float>();
            for (int k = 0; k < seen; k++)
            {
                var st = _places[(_cursor + k) % n];
                try
                {
                    var shelf = st.ItemRoster;
                    EquipmentElement el = default(EquipmentElement);
                    bool found = false;
                    for (int i = 0; i < shelf.Count; i++)
                    {
                        var e = shelf.GetElementCopyAtIndex(i);
                        var it = e.EquipmentElement.Item;
                        if (e.Amount > 0 && it != null && it.ItemType == ItemObject.ItemTypeEnum.Horse) { el = e.EquipmentElement; found = true; break; }
                    }
                    if (!found) { noHorse++; continue; }
                    int buy, sellOld, sellNew;
                    if (!SupplyDemand.ProbeHorse(st, el, client, out buy, out sellOld, out sellNew)) { outside++; continue; }
                    if (buy <= 0) { failed++; continue; }
                    int pn = sellNew - buy, po = sellOld - buy;
                    if (probed == 0 || pn > maxNew) { maxNew = pn; worst = st.Name + " " + el.Item.StringId + " kupno " + buy + " / sprzedaz " + sellNew; }
                    if (probed == 0 || po > maxOld) maxOld = po;
                    if (pn > 0) gainNew++;
                    if (po > 0) gainOld++;
                    rNew.Add(sellNew / (float)buy); rOld.Add(sellOld / (float)buy);
                    probed++;
                    if (st.IsTown) towns++;
                }
                catch (Exception e) { failed++; Stumble("Probe(osada)", e); }
            }
            _cursor = (_cursor + seen) % n;

            sb.Append(" | proba kup-sprzedaj konia (klient ").Append(client != null ? "gracz" : "bez partii").Append("): osad ").Append(seen).Append(" z ").Append(n)
              .Append(", z koniem na polce ").Append(probed).Append(" (miasta ").Append(towns).Append(", zamki ").Append(probed - towns).Append("), bez konia ").Append(noHorse)
              .Append(", poza prawem podazy ").Append(outside).Append(", nieudane ").Append(failed);
            if (probed > 0)
            {
                rNew.Sort(); rOld.Sort();
                sb.Append("; nowa droga: zysk najwyzszy ").Append(maxNew).Append(" zl, z zyskiem ").Append(gainNew).Append(", sprzedaz/kupno mediana ")
                  .Append(rNew[rNew.Count / 2].ToString("0.00", ci)).Append(" (").Append(rNew[0].ToString("0.00", ci)).Append('-').Append(rNew[rNew.Count - 1].ToString("0.00", ci)).Append(')')
                  .Append("; stara droga: zysk najwyzszy ").Append(maxOld).Append(" zl, z zyskiem ").Append(gainOld).Append(", sprzedaz/kupno mediana ")
                  .Append(rOld[rOld.Count / 2].ToString("0.00", ci)).Append(" (").Append(rOld[0].ToString("0.00", ci)).Append('-').Append(rOld[rOld.Count - 1].ToString("0.00", ci)).Append(')')
                  .Append("; najblizej zysku nowa droga: ").Append(worst);
            }
        }
    }
}
