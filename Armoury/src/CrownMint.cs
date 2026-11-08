using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// 311b (31.10, klucz CrownMint; raport rozp311/mennica-raport.md i krytyka mennica-krytyk.md) - MENNICA KORONY I ZUZYCIE MONETY.
    ///
    /// Pomiar (Armoury-2026-10-31_17-28-59.log, 89 dob): suma pieniadza swiata stala (219.79M -> 219.76M), mennicy nie ma nigdzie - Debasement to przelew 0.5%
    /// kasy miasta do krola (KingdomTreasury.Levies), BK i BEE nie bija monety. Ruda srebra 216.5/d wydobyta ~ zuzyta (polowa u zlotnikow, jewelry spada 95 -> 52),
    /// ruda zlota 6.9/d zjadaja mieszczanie, nikt jej nie przerabia.
    ///
    /// Zasady:
    ///  - GDZIE: kazde miasto krolestwa (nie oblezone) ma mennice - jak w Anglii XI-XIII w. (kilkadziesiat miast menniczych, prawo krola wykonywane w grodach).
    ///    Miast ROT jest malo i sa duze, wiec "kazde miasto" jest blizsze prawdy niz "tylko stolica"; zlotnikow i ludu chroni zapas, nie lista miast.
    ///  - CO: ruda zlota i srebro z polki miasta TYLKO ponad zapas = MintSilverReserveDays x (dzienny wsad rudy srebra linii warsztatow miasta (GoodsPaths.
    ///    SilverNeedOf) + zuzycie swiata tego kruszcu z ostatniej linii ksiegi towarow bez wczorajszej mennicy / liczba miast). Jedna regula dla obu kruszcow
    ///    (ruda zlota: wsad warsztatow 0). Mieszczanie kupuja wczesniej w tej samej dobie (TownFolk.Daily przed nami), wiec mennica bierze tylko reszte.
    ///  - ZA ILE: wartosc kruszcu po cenie miasta (Town.GetItemPrice - ta sama cena co FolkCap). Kasa miasta (wlasciciel polki) dostaje wartosc - oplata mennicza
    ///    (MintSeigniorage), skarbiec krolestwa dostaje oplate. Przy polityce Debasement mennica bije DebasementMintExtra x wartosc wiecej z tego samego kruszcu -
    ///    nadwyzka do skarbca (prawdziwe psucie monety); stary przelew 0.5% kasy miasta do krola (Levies) zostaje bez zmian - to inny przeplyw (kasa -> krol,
    ///    bez nowej monety), wiec nic nie liczy sie dwa razy.
    ///  - KSIEGI: kruszec w ramce FMint ksiegi towarow (koszyk "mennica (311b)" - ujscie), nowa moneta (wartosc + psucie) w Seal31.ZMint (zrodlo Z6 linii przyczyn);
    ///    zuzycie monety w Seal31.UCoinWear (ujscie U11). Zloto przez ChangeGold / KingdomBudgetWallet bez zdarzenia gry - poza "z niczego" (from) i w RB tylko
    ///    raz, przez pozycje nazwana.
    ///  - ZUZYCIE: CoinWearPerYear / dni roku na dobe z kies ludu (TownFolk.CoinWear) i kas miast i zamkow, ulamki przechodza na jutro. Domyslnie 0 (recenzja 311b):
    ///    na liczbach 31.10 polki maja 3-4 szt. srebra i <1 szt. rudy zlota na miasto - mennica bije prawie nic, a zuzycie od pierwszej doby zmniejszaloby pieniadz
    ///    swiata (odwrotnie niz chcial Jeff); wlaczyc dopiero, gdy linia "Mennica (311b)" pokaze realna monete (decyzja Jeffa).
    ///    312a (Jeff 01.11 "wlacz mennice"): domyslnie 0.003 - autotest 311x: mennica bila 86573 w 90 dob (ok. 0.16%/rok calego pieniadza), zuzycie
    ///    0.3%/rok liczy sie tylko od kies ludu i kas miast/zamkow (ok. 1/6 pieniadza), wiec netto swiata zostaje dodatnie.
    ///  - Wsie, sakiewki panow, skarbce i Bank sie nie zuzywaja (moneta tam lezy, nie krazy na targu).
    ///  - KONTROLA: zloto swiata przed i po (MoneyLedger.WorldNow) - roznica musi byc rowna moneta - zuzycie (rozjazd 0), linia "Mennica (311b)".
    ///  - Potkniecia liczone na miasto, pierwszy wyjatek z miejsca do logu; nic nie gasi funkcji. Raz na dobe, wlasna pozycja MMint ksiegi pieniadza (po Bloomery.Daily,
    ///    przed KingdomTreasury.Levies - oplata jest juz w skarbcu, gdy korona liczy dzien).
    /// </summary>
    internal static class CrownMint
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private const int MSilver = 0, MGold = 1, Metals = 2;
        private static readonly string[] MetalId = { "silver", "gold_ore" };
        private static readonly string[] MetalName = { "srebro", "ruda zlota" };
        private const double FallbackPerTown0 = 2.2, FallbackPerTown1 = 0.07;   // zuzycie swiata na miasto (szt./d), gdy ksiega towarow nie dala liczb (pomiar 31.10: 215/97, 6.8/97)

        private static bool On { get { var s = Settings.Current; var c = Campaign.Current; return s != null && s.CrownMint && c != null && c.GameStarted; } }

        // ------------------------------------------------------------ stan sesji (bez zapisu)
        private static Campaign _camp;
        private static int _lastDay = -1;
        private static readonly ItemObject[] _item = new ItemObject[Metals];
        private static readonly Dictionary<Settlement, double> _wearTown = new Dictionary<Settlement, double>();   // ulamek zuzycia kasy miasta na jutro
        private static readonly Dictionary<string, double[]> _ks = new Dictionary<string, double[]>(StringComparer.Ordinal);
        private static long _sessCoin, _sessWear; private static int _sessDays;
        // 360c (StartCalibration.Sur360): kruszec zdjety przez mennice w ostatniej dobie wedlug rynku miasta [rynek W/E, kruszec] - tylko odczyt
        private static readonly long[,] _mkPcs = new long[2, Metals];
        private static int _mkDay = -1;

        /// <summary>360c: kopia sztuk mennicy ostatniej doby wedlug rynku [0 Westeros / 1 Essos, 0 srebro / 1 ruda zlota]; zwraca dzien (-1 - mennica nie bila w tej
        /// kampanii albo klucz wylaczony). Nie rzuca.</summary>
        internal static int MintByMarket(long[,] dst)
        {
            try
            {
                if (dst == null) return -1;
                for (int m = 0; m < 2; m++) for (int k = 0; k < Metals; k++) dst[m, k] = _mkPcs[m, k];
                return _mkDay;
            }
            catch { return -1; }
        }

        internal static int Stumbles;
        private static readonly HashSet<string> _err = new HashSet<string>();

        private static void Stumble(string where, Exception e)
        {
            Stumbles++;
            try { if (_err.Add(where)) Log.Error("CrownMint." + where, e); } catch { }
        }

        private static void ResetIfNew()
        {
            var c = Campaign.Current;
            if (ReferenceEquals(c, _camp)) return;
            _camp = c; _lastDay = -1;
            for (int m = 0; m < Metals; m++) _item[m] = null;
            _wearTown.Clear(); _ks.Clear(); _sessCoin = _sessWear = 0; _sessDays = 0; _err.Clear();
            Array.Clear(_mkPcs, 0, _mkPcs.Length); _mkDay = -1;   // 360c
        }

        private static ItemObject ItemOf(int m)
        {
            if (_item[m] != null) return _item[m];
            try { var om = MBObjectManager.Instance; _item[m] = om != null ? om.GetObject<ItemObject>(MetalId[m]) : null; } catch { _item[m] = null; }
            return _item[m];
        }

        private sealed class KRow { public string Name; public readonly long[] Pcs = new long[Metals]; public long Coin, Seig, Extra; public int Towns; public bool Debase; }

        /// <summary>Raz na dobe (ArmouryBehavior, po Bloomery.Daily, wlasna pozycja MMint ksiegi pieniadza). Nie rzuca.</summary>
        internal static void Daily()
        {
            try
            {
                ResetIfNew();
                if (!On) return;
                int today = (int)CampaignTime.Now.ToDays;
                if (today == _lastDay) return;
                _lastDay = today;
                Stumbles = 0;
                Array.Clear(_mkPcs, 0, _mkPcs.Length);   // 360c: sztuki mennicy tej doby wedlug rynku
                var s = Settings.Current;
                long t0 = Stopwatch.GetTimestamp();
                long w0 = MoneyLedger.WorldNow();
                double seigR = Math.Max(0.0, Math.Min(0.5, s.MintSeigniorage));
                double extraR = Math.Max(0.0, Math.Min(1.0, s.DebasementMintExtra));
                double days = Math.Max(1, Math.Min(120, s.MintSilverReserveDays));
                int yearDays = Math.Max(1, LongYearTimeModel.DaysPerYear(s));
                double wearDay = Math.Max(0.0, Math.Min(0.2, s.CoinWearPerYear)) / yearDays;

                // zuzycie swiata na miasto z ostatniej linii ksiegi towarow (KsCons jest bez mennicy - GoodsLedger.KsNote, recenzja 311b)
                int nTowns = 0;
                foreach (var st in Settlement.All) if (st != null && st.IsTown && st.Town != null) nTowns++;
                var perTown = new double[Metals];
                int ksDay = -1;
                try { ksDay = GoodsLedger.KsTake(_ks); } catch { ksDay = -1; }
                for (int m = 0; m < Metals; m++)
                {
                    double[] v;
                    if (ksDay >= 0 && _ks.TryGetValue(MetalId[m], out v) && v != null && v.Length > GoodsLedger.KsCons)
                        perTown[m] = Math.Max(0.0, v[GoodsLedger.KsCons]) / Math.Max(1, nTowns);
                    else perTown[m] = ksDay >= 0 ? 0.0 : (m == MSilver ? FallbackPerTown0 : FallbackPerTown1);   // towar bez ruchu w ksiedze - zuzycie 0
                }

                // ---- mennice
                var pcs = new long[Metals]; var val = new long[Metals]; var kept = new long[Metals];
                long coin = 0, seig = 0, extra = 0, toTowns = 0;
                int mints = 0, minted = 0, smithTowns = 0, debK = 0;
                var rows = new List<KRow>();
                foreach (var k in Kingdom.All)
                {
                    if (k == null || k.IsEliminated) continue;
                    bool debase = false;
                    try { debase = k.ActivePolicies.Contains(DefaultPolicies.DebasementOfTheCurrency); } catch { }
                    if (debase) debK++;
                    KRow row = null;
                    foreach (var st in k.Settlements)
                    {
                        if (st == null || !st.IsTown || st.Town == null || st.IsUnderSiege || st.SettlementComponent == null) continue;
                        mints++;
                        try
                        {
                            var town = st.Town;
                            var shelf = town.Owner != null ? town.Owner.ItemRoster : null;
                            if (shelf == null) continue;
                            float smith = GoodsPaths.SilverNeedOf(town);
                            if (smith > 0f) smithTowns++;
                            bool any = false;
                            for (int m = 0; m < Metals; m++)
                            {
                                var it = ItemOf(m);
                                if (it == null) continue;
                                int have = shelf.GetItemNumber(it);
                                if (have <= 0) continue;
                                // 311b-2 (autotest 311a: srebro do warsztatu gracza w Seagard 416 -> 78 szt. w 90 dob, do linii zlotnikow swiata 58 -> 48/d): zlotnik bierze rude
                                // cyklem po 25 szt., a zapas z dziennego wsadu tego nie widzi - w miescie ze zlotnikiem mennica srebra nie bierze wcale (zloto jak dotad)
                                if (m == MSilver && smith > 0f && s.MintSilverSkipSmithTowns) { kept[m] += have; continue; }
                                double need = (m == MSilver ? smith : 0f) + perTown[m];
                                int reserve = (int)Math.Ceiling(days * need);
                                // recenzja 311b: mennica to ujscie zbytkowe - jak dwory (CourtGoods) tylko ponad prog polki (MaterialOrders.ShelfReserve: prog zrodla + pas
                                // wywozu kontraktow 174, wozow i karawan), inaczej miasto kopalniane bez zlotnikow nie ma czego wyslac miastom zlotnikow (285)
                                int shelfKeep = MaterialOrders.ShelfReserve(town, it);
                                if (shelfKeep > reserve) reserve = shelfKeep;
                                int take = have - reserve;
                                kept[m] += Math.Min(have, Math.Max(0, reserve));
                                if (take <= 0) continue;
                                int price = town.GetItemPrice(it);
                                if (price <= 0) continue;
                                long cap = int.MaxValue / 4;
                                if ((long)take * price > cap) take = (int)(cap / price);
                                if (take <= 0) continue;
                                long value = (long)take * price;
                                long fee = (long)Math.Floor(value * seigR);
                                long ext = debase ? (long)Math.Floor(value * extraR) : 0;
                                // recenzja 311b: skarbiec (int) bez przepelnienia - oplata ponad miejsce w skarbcu zostaje w kasie miasta, psucie ponad miejsce nie powstaje
                                long room = Math.Max(0L, (long)int.MaxValue - Math.Max(0, k.KingdomBudgetWallet));
                                if (fee > room) fee = room;
                                if (ext > room - fee) ext = room - fee;
                                long toTown = value - fee;
                                // kruszec z polki do mennicy (ramka ksiegi towarow), nowa moneta do kasy kupcow i skarbca
                                var gf = GoodsLedger.Begin(GoodsLedger.FMint, town);
                                try { shelf.AddToCounts(it, -take); }
                                finally { GoodsLedger.End(gf); }
                                if (toTown > 0) st.SettlementComponent.ChangeGold((int)toTown);
                                if (fee + ext > 0) k.KingdomBudgetWallet += (int)(fee + ext);
                                Seal31.AddWorld(Seal31.ZMint, value + ext);
                                try { TownFolk.Sale(st, (int)toTown, TownFolk.MixGoods); } catch { }   // sprzedaz kupcow (podstawa robocizny handlu - jak dymarki): tyle, ile dostala kasa
                                pcs[m] += take; val[m] += value; coin += value + ext; seig += fee; extra += ext; toTowns += toTown;
                                try { int mk = StartCalibration.TownMarketOf(town); if (mk == 0 || mk == 1) _mkPcs[mk, m] += take; } catch { }   // 360c: tylko licznik
                                if (row == null) { row = new KRow { Name = k.Name != null ? k.Name.ToString() : k.StringId, Debase = debase }; rows.Add(row); }
                                row.Pcs[m] += take; row.Coin += value + ext; row.Seig += fee; row.Extra += ext;
                                any = true;
                            }
                            if (any) { minted++; row.Towns++; }
                        }
                        catch (Exception e) { Stumble("Town", e); }
                    }
                }

                _mkDay = today;   // 360c: sztuki mennicy tej doby gotowe (takze 0)

                // ---- zuzycie monety: kiesy ludu (miasta i podzamcza) i kasy miast i zamkow - ta sama regula po obu stronach (recenzja 311b)
                int folkPurses = 0, wearTowns = 0;
                long wearFolk = 0, wearTown = 0;
                if (wearDay > 0)
                {
                    wearFolk = TownFolk.CoinWear(wearDay, out folkPurses);
                    foreach (var st in Settlement.All)
                    {
                        if (st == null || !(st.IsTown || st.IsCastle) || st.SettlementComponent == null) continue;
                        try
                        {
                            int g = st.SettlementComponent.Gold;
                            if (g <= 0) { _wearTown.Remove(st); continue; }
                            double c0; _wearTown.TryGetValue(st, out c0);
                            double w = g * wearDay + c0;
                            long x = (long)Math.Floor(w);
                            _wearTown[st] = w - x;
                            if (x <= 0) continue;
                            if (x > g) x = g;
                            st.SettlementComponent.ChangeGold(-(int)x);
                            wearTown += x; wearTowns++;
                        }
                        catch (Exception e) { Stumble("WearTown", e); }
                    }
                }
                long wear = wearFolk + wearTown;
                if (wear > 0) Seal31.AddWorld(Seal31.UCoinWear, wear);

                long w1 = MoneyLedger.WorldNow();
                long net = coin - wear;
                _sessCoin += coin; _sessWear += wear; _sessDays++;
                double ms = (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency;
                if (s.LogEnabled) Line(today - 1, s, mints, minted, smithTowns, debK, pcs, val, kept, coin, seig, extra, toTowns, seigR, extraR, days, perTown, ksDay,
                                       wearFolk, folkPurses, wearTown, wearTowns, wear, wearDay * yearDays, net, w0, w1, yearDays, rows, ms);
            }
            catch (Exception e) { Stumble("Daily", e); }
        }

        private static string F(double v, string f) { return v.ToString(f, Inv); }

        private static void Line(int day, Settings s, int mints, int minted, int smithTowns, int debK, long[] pcs, long[] val, long[] kept, long coin, long seig, long extra,
                                 long toTowns, double seigR, double extraR, double days, double[] perTown, int ksDay, long wearFolk, int folkPurses, long wearTown, int wearTowns,
                                 long wear, double wearYear, long net, long w0, long w1, int yearDays, List<KRow> rows, double ms)
        {
            try
            {
                var sb = new StringBuilder(1600);
                sb.Append("Mennica (311b): dzien ").Append(day).Append(" | klucz TAK | mennic ").Append(mints).Append(" (bily dzis ").Append(minted)
                  .Append(", miast ze zlotnikami ").Append(smithTowns).Append(", krolestw z psuciem monety ").Append(debK).Append(')');
                sb.Append(" | kruszec:");
                for (int m = 0; m < Metals; m++)
                    sb.Append(m > 0 ? ";" : "").Append(' ').Append(MetalName[m]).Append(' ').Append(pcs[m]).Append(" szt. za ").Append(val[m])
                      .Append(" (zapas zostawiony na polkach ").Append(kept[m]).Append(", zuzycie swiata na miasto ").Append(F(perTown[m], "0.00")).Append("/d)");
                sb.Append(" | zapas dni ").Append(F(days, "0")).Append(", ksiega towarow z dnia ").Append(ksDay >= 0 ? ksDay.ToString(Inv) : "- (zapas ze stalej)");
                sb.Append(" | nowa moneta ").Append(coin).Append(" = do kas miast ").Append(toTowns).Append(" + oplata mennicza do skarbcow ").Append(seig)
                  .Append(" (").Append(F(seigR * 100, "0.0")).Append("%) + psucie monety do skarbcow ").Append(extra).Append(" (").Append(F(extraR * 100, "0.0")).Append("%)");
                sb.Append(" | zuzycie monety ").Append(wear).Append(" = kiesy ludu ").Append(wearFolk).Append(" (").Append(folkPurses).Append(" kies) + kasy miast i zamkow ").Append(wearTown)
                  .Append(" (").Append(wearTowns).Append(" osad); stawka ").Append(F(wearYear * 100, "0.000")).Append("%/rok");
                double pct = w0 > 0 ? net * (double)yearDays * 100.0 / w0 : 0.0;
                double sessPct = w0 > 0 && _sessDays > 0 ? (_sessCoin - _sessWear) / (double)_sessDays * yearDays * 100.0 / w0 : 0.0;
                sb.Append(" | netto swiata ").Append(net >= 0 ? "+" : "").Append(net).Append(" (").Append(F(pct, "0.0000")).Append("%/rok; od wczytania ").Append(_sessDays)
                  .Append(" dob: moneta ").Append(_sessCoin).Append(", zuzycie ").Append(_sessWear).Append(", ").Append(F(sessPct, "0.0000")).Append("%/rok; rok ").Append(yearDays).Append(" dni)");
                long diff = w1 - w0;
                sb.Append(" | kontrola swiata: przed ").Append(w0).Append(", po ").Append(w1).Append(", roznica ").Append(diff).Append(", oczekiwane ").Append(net)
                  .Append(", rozjazd ").Append(diff - net);
                rows.Sort((a, b) => b.Coin.CompareTo(a.Coin));
                sb.Append(" | krolestwa:");
                if (rows.Count == 0) sb.Append(" -");
                for (int i = 0; i < rows.Count && i < 8; i++)
                {
                    var r = rows[i];
                    sb.Append(i > 0 ? ";" : "").Append(' ').Append(r.Name).Append(" [miast ").Append(r.Towns).Append(", srebro ").Append(r.Pcs[MSilver]).Append(", ruda zlota ").Append(r.Pcs[MGold])
                      .Append(", moneta ").Append(r.Coin).Append(", oplata ").Append(r.Seig).Append(", psucie ").Append(r.Extra).Append(r.Debase ? ", Debasement" : "").Append(']');
                }
                if (rows.Count > 8) sb.Append("; i ").Append(rows.Count - 8).Append(" innych");
                sb.Append(" | koszt ").Append(F(ms, "0.0")).Append(" ms");
                if (Stumbles > 0) sb.Append(" | potkniecia ").Append(Stumbles).Append(" (pierwszy wyjatek z miejsca w logu jako ERROR)");
                sb.Append('.');
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Stumble("Line", e); }
        }
    }
}
