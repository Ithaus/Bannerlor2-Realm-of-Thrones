using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Extensions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// P1 (OBIEG KIESY LUDU, docs/kiesa-ludu/PROJEKT-OBIEG-KIESY-LUDU-2026-10-13.md rozdz. 3.1, 5, 7, 8 wiersz P1) - strona ludu:
    ///  A. DANINA Z OSZCZEDNOSCI: PayAid - w kroku 5 doby ludu (TownLive / CastleLiveDay), po robotach gminy, przed oferta nadwyzki na jutro: zdjecie z kiesy
    ///     klasy tego, co KingdomTreasury.FolkWarAid dopisal skarbcowi krolestwa (przelew: kiesa -> skarbiec; kontrola zamknieta kies - "wydatki").
    ///  B. Zdanie "Savings: ..." w oknie "People of this land" (PeopleMenu, klucz PeopleMenuShowsSavings) - liczby ostatniej doby osady.
    ///  C. POMIARY (klucz FolkLadderMeasureLog) - SAM LOG, zadne zloto, towar, cena, popyt ani lojalnosc nie sa ruszane:
    ///     co dobe "Lojalnosc osad (P1)", "Kupione z oferty (P1)", "Marza kupcow z karawan (P1)", "Rody z luzem pulapu (P1)";
    ///     co 7 dob (razem z CSV kiesy) "Zbytki na sucho (P1)", "Warsztaty zbytkow (P1)", "Lokata a budowy (P1)", "Sklad zarobku miast (P1)" i plik kiesa-obieg.csv.
    /// Stan tylko w pamieci sesji (bez pol zapisu). Kazdy pomiar we wlasnym try, licznik potkniec TownFolk (CLAUDE.md 7).
    /// </summary>
    internal static partial class TownFolk
    {
        private const int OfMax = 8;                          // kategorie "lepsze" (BetterIds) - rozmiar licznikow oferty
        private const int CvGroups = 4;                       // marza z karawan: 0 zywnosc, 1 zbytki BK (LuxIds), 2 sukno i dom z listy dostatku, 3 reszta
        private const double DrySpendDays = 60, DryCeiling = 1.5, DryRpGoods = 0.3;   // regula "na sucho" z projektu (szczebel 2): 1/60 nadwyzki, cena graniczna 1.5, R i P 30% na towar
        private static readonly double[] DryWeight = { 0.55, 0.25, 0.08 };            // sukno i odzienie / srebro, klejnoty, korzenie / dom (lepszy stol 12% - zywnosc, bez sygnalu)
        private static readonly string[][] DryIds =
        {
            new[] { "felt", "cotton", "velvet", "linen", "leather", "fur", "garment" },
            new[] { "jewelry", "silver", "spice", "walrus_tusk" },
            new[] { "pottery", "whale_oil", "papyrus" }
        };
        private static readonly string[] DryNoSignal = { "linen", "leather" };       // wsad zbrojowni i wojska - bez sygnalu popytu (projekt A R-A3)
        private static readonly string[] DryGroupName = { "sukno i odzienie", "srebro, klejnoty, korzenie", "dom" };

        // ---- danina (doba)
        private static long _aidTicks;
        private static int _aidDay = -1;
        private static readonly Dictionary<Kingdom, double[]> _aidOvK = new Dictionary<Kingdom, double[]>();   // [0] wszystkie klasy i podzamcza, [1] W miast (lokata)
        // ---- pomiar: oferta nadwyzki w otwartym nawiasie konsumpcji
        private static readonly ItemCategory[] _ofCat = new ItemCategory[OfMax];
        private static readonly int[] _ofIdx = new int[OfMax];
        private static readonly double[] _ofX = new double[OfMax], _ofAdd = new double[OfMax], _ofBase = new double[OfMax], _ofEff = new double[OfMax];
        private static int _ofN; private static bool _ofOn;
        // ---- pomiar: marza kupcow na towarze z karawan (od poprzedniej linii)
        private static readonly Dictionary<Settlement, long> _cvPend = new Dictionary<Settlement, long>();
        private static readonly double[] _cvPaidG = new double[CvGroups], _cvValG = new double[CvGroups], _cvWorthG = new double[CvGroups];
        private static long _cvPaid, _cvPieces; private static double _cvVal; private static int _cvN, _cvNoPay;
        private static readonly Dictionary<ItemCategory, int> _cvGrp = new Dictionary<ItemCategory, int>();
        // ---- pomiar: kategorie listy dostatku (na sucho)
        private static List<ItemCategory> _dryCats; private static List<int> _dryGroup; private static List<bool> _drySignal; private static string _dryMissing = "-";
        // ---- linie i okno
        private static string _lineLoy, _lineOffer, _lineCar, _lineDry, _lineShops, _lineBuild, _lineEarn;
        private static int _p1WinDays, _p1Day = -1;
        private static long _p1DayTicks;

        private static void ResetP1()
        {
            _aidTicks = 0; _aidDay = -1; _aidOvK.Clear();
            _ofN = 0; _ofOn = false; Array.Clear(_ofCat, 0, OfMax);
            _cvPend.Clear(); CvClear(); _cvGrp.Clear();
            _dryCats = null; _dryGroup = null; _drySignal = null; _dryMissing = "-";
            _lineLoy = _lineOffer = _lineCar = _lineDry = _lineShops = _lineBuild = _lineEarn = null;
            _p1WinDays = 0; _p1Day = -1; _p1DayTicks = 0;
        }

        /// <summary>Pomiary P1 czynne: klucz FolkLadderMeasureLog i log (same linie - bez logu nie ma czego liczyc).</summary>
        private static bool MeasureP1 { get { var s = Settings.Current; return s != null && s.FolkLadderMeasureLog && s.LogEnabled; } }

        private static string MsOf(long ticks, string fmt) { return (ticks * 1000.0 / Stopwatch.Frequency).ToString(fmt, Inv); }

        // ============================================================ A. danina z oszczednosci (krok 5 doby ludu)
        private static void AidBegin() { _aidTicks = 0; _aidOvK.Clear(); }

        /// <summary>
        /// P1 (szczebel 4): danina wojenna z kiesy klasy `c` PONAD zapas dni (`ov` - policzone przez wolajacego ta sama stala co oferta nadwyzki). Bramki, stawka
        /// i przelew do skarbca krolestwa - KingdomTreasury.FolkWarAid (nie rzuca; zwraca CALE zloto); tu zdjecie dokladnie tej kwoty z kiesy klasy (kiesa ->
        /// skarbiec, to samo zloto) i ksiega kies (_ctlOut, LDayOut). Zwraca zaplacone - wolajacy liczy oferte i place z nadwyzki od reszty.
        /// </summary>
        private static long PayAid(Folk f, int c, int cls, double ov, LDay d)
        {
            long t = 0;
            if (ov >= 1)
            {
                long t0 = Stopwatch.GetTimestamp();
                t = KingdomTreasury.FolkWarAid(f.St, cls, ov, Math.Max(0, PurseOf(f, c)));
                if (t > 0) { AddPurse(f, c, -t); _ctlOut += t; f.LDayOut += t; }
                _aidTicks += Stopwatch.GetTimestamp() - t0;
            }
            try
            {
                double left = ov - t;
                f.DAid += t; f.Aid7 += t; f.DOver += left; if (c >= 0 && c < 3) f.DOvC[c] = left;
                // P1b (FolkAidLoyalty): czesc calej kiesy ludu zabrana w daninie tej doby - kiesy klas TERAZ + danina doby = stan przed danina (ostatnia klasa
                // nadpisuje wynik pelnym rachunkiem; DAid zerowane z liczbami doby osady - przy DAid = 0 wartosc nie jest czytana)
                if (f.DAid > 0) { long all = f.Kind == 1 ? f.KW : f.KW + f.KR + f.KP; f.AidFrac = f.DAid / Math.Max(1.0, (double)Math.Max(0, all) + f.DAid); }
                d.Aid[cls] += t; d.Ov0[cls] += ov; d.Ov1[cls] += left;
                var k = f.St.OwnerClan != null ? f.St.OwnerClan.Kingdom : null;
                if (k != null)
                {
                    double[] x;
                    if (!_aidOvK.TryGetValue(k, out x)) { x = new double[2]; _aidOvK[k] = x; }
                    x[0] += left; if (cls == KingdomTreasury.WtW) x[1] += left;
                }
            }
            catch (Exception e) { Stumble("PayAid(liczniki)", e); }
            return t;
        }

        /// <summary>Linia korony "Danina z oszczednosci (P1)": kiesa ponad zapas dni przed / po daninie wedlug klasy (W, R, P, podzamcze) - false, gdy doba kiesy `day` nie biegla.</summary>
        internal static bool AidDayOf(int day, double[] ov0, double[] ov1)
        {
            var d = _ld;
            if (d == null || _aidDay != day || ov0 == null || ov1 == null || ov0.Length < 4 || ov1.Length < 4) return false;
            for (int i = 0; i < 4; i++) { ov0[i] = d.Ov0[i]; ov1[i] = d.Ov1[i]; }
            return true;
        }

        /// <summary>Kiesa ludu ponad zapas dni po daninie w osadach rodow tego krolestwa (doba kiesy `day`; 0 - nie biegla albo brak nadwyzki): wszystkie klasy
        /// i podzamcza; wTown - z tego wyrobnicy miast (czesc "lokaty" z linii Kiesa ludu przypadajaca na to krolestwo).</summary>
        internal static double AidExcessOf(Kingdom k, int day, out double wTown)
        {
            wTown = 0;
            double[] x;
            if (k == null || _aidDay != day || !_aidOvK.TryGetValue(k, out x)) return 0;
            wTown = x[1];
            return x[0];
        }

        /// <summary>Czas poboru daniny w dobie kiesy `day` (ms; "-" - doba nie biegla).</summary>
        internal static string AidMs(int day) { return _aidDay == day ? MsOf(_aidTicks, "0.000") : "-"; }

        /// <summary>P1b (FolkAidLoyalty): miasta i zamki, ktorych lud oddal koronie danine w ostatniej dobie kiesy (DAid > 0 i liczby tej doby - ADay), z czescia
        /// calej kiesy ludu zabrana w daninie (0..1). Tylko odczyt; kiesa nieczynna - lista pusta. Zamek tylko przy zaworze zamkow (CastleLive).</summary>
        internal static void AidPlaces(List<KeyValuePair<Settlement, double>> into)
        {
            if (into == null) return;
            into.Clear();
            if (!Live || _liveDay < 0) return;
            for (int i = 0; i < _fk.Count; i++)
            {
                var f = _fk[i];
                if (f == null || f.St == null || f.Kind > 1 || f.DAid <= 0 || f.ADay != _liveDay) continue;
                if (f.Kind == 1 && !CastleLive) continue;
                double q = f.AidFrac;
                if (double.IsNaN(q) || double.IsInfinity(q)) q = 0;
                into.Add(new KeyValuePair<Settlement, double>(f.St, Math.Max(0, Math.Min(1, q))));
            }
        }

        // ============================================================ B. zdanie "Savings" w oknie People of this land
        private static string DaysText(double days)
        {
            if (double.IsNaN(days) || days < 1) return "less than a day";
            long n = (long)Math.Floor(days + 1e-9);
            return n == 1 ? "1 day" : n.ToString("N0", Inv) + " days";
        }

        private static string Coins(double x) { long n = (long)Math.Floor(Math.Max(0, x) + 1e-9); return n == 1 ? "1 coin" : n.ToString("N0", Inv) + " coins"; }

        /// <summary>
        /// P1 (rozdz. 7): zdanie "Savings: ..." do okna "People of this land" dla osady, w ktorej stoi gracz - na ile dni zycia lud ma monety, ile z tego lezy
        /// ponad zapasem na czarna godzine i ile oddal koronie w daninie wojennej o ostatnim swicie (inne szczeble drabiny wejda w kolejnych paczkach - zdanie
        /// mowi tylko o tym, co juz dziala). Liczby z ostatniej doby kiesy tej osady; tylko odczyt (bez Get, bez zlota, bez stanu). null - bez zdania (kiesa
        /// nieczynna, brak liczb tej doby - wtedy zdanie "Bread:" mowi juz "not reckoned yet", osada bez potrzeby). log - linia do logu przy otwarciu okna.
        /// </summary>
        internal static string SavingsLine(Settlement st, out string log)
        {
            log = null;
            if (st == null || !Live) return null;
            Folk f;
            if (!_f.TryGetValue(st, out f) || f == null || f.Kind < 0 || f.Kind > 2) return null;
            if (f.Kind == 1 && !CastleLive) return null;
            int now = (int)CampaignTime.Now.ToDays - 1;
            if (_ld == null || _liveDay < 0 || f.ADay != _liveDay || now - _liveDay > 1) return null;
            string head = "PeopleMenu (P1 Savings): dzien " + _liveDay + " | " + Clean(st.Name != null ? st.Name.ToString() : st.StringId) + " (" + st.StringId + ", "
                          + (f.Kind == 0 ? "miasto" : f.Kind == 1 ? "zamek" : "wies") + ")";
            if (f.Kind == 2)
            {
                var vil = st.Village;
                if (vil == null || !(f.DVNeed > 0)) return null;
                double vd = Math.Max(0, vil.Gold) / f.DVNeed;
                var mk = vil.TradeBound ?? vil.Bound;
                log = head + " | kiesa wsi " + vil.Gold + ", potrzeba zakupow dnia " + F2(f.DVNeed) + ", dni " + F1(vd);
                return "Savings: the villagers hold coin for " + DaysText(vd) + " of their purchases"
                       + (mk != null && mk.Name != null ? " at the market of " + mk.Name : "") + ".";
            }
            double need = f.Kind == 1 ? f.Need[0] : f.Need[0] + f.Need[1] + f.Need[2];
            long purse = f.Kind == 1 ? f.KW : f.KW + f.KR + f.KP;
            if (!(need > 0)) return null;
            double days = Math.Max(0, purse) / need;
            string who = f.Kind == 0 ? "townsfolk" : "castle folk";
            int why;
            double rate = KingdomTreasury.FolkWarAidRate(st, out why);
            log = head + " | kiesy " + purse + ", potrzeba dnia " + F2(need) + ", dni " + F1(days) + " | ponad zapas dni po daninie " + N0(f.DOver) + ", danina ostatniej doby " + f.DAid
                  + " | stawka dnia dzis " + (100 * rate).ToString("0.000", Inv) + "% (powod " + why + ": 0 placi, 1 wylaczone, 2 bez krolestwa, 3 pokoj albo s = 0, 4 oblezenie, 6 lojalnosc na progu P1b)";
            string s0 = "Savings: the " + who + " hold coin for " + DaysText(days) + " of living";
            if (f.DAid > 0)
                return s0 + ". At the last dawn they gave the crown " + Coins(f.DAid) + " in war aid; " + Coins(f.DOver) + " still lie above their reserve for hard times, which is not touched.";
            if (f.DOver < 1) return s0 + " - nothing to spare.";
            string tail = why == KingdomTreasury.WaNoNeed ? " The crown asks no war aid of them now."
                        : why == KingdomTreasury.WaSiege ? " Under siege they pay no war aid."
                        : why == KingdomTreasury.WaLowLoy ? " The tax gatherers have left: the " + (f.Kind == 0 ? "town" : "castle") + " is too close to revolt to pay the war aid." : "";
            return s0 + "; " + Coins(f.DOver) + " of it lie above their reserve for hard times." + tail;
        }

        // ============================================================ C1. kupione z oferty nadwyzki (nawias konsumpcji)
        /// <summary>DemandPostfix (otwarcie nawiasu konsumpcji osady): oferta tego nawiasu od zera.</summary>
        private static void OfferOpen() { _ofN = 0; _ofOn = MeasureP1; }

        /// <summary>AddOffer: oferta `x` zl dopisana do budzetu kategorii "lepszej" nr `bi` jako `add` jednostek budzetu przy budzecie gry `v` (tylko licznik).</summary>
        private static void OfferNote(int bi, ItemCategory c, double x, double add, double v)
        {
            if (!_ofOn || _ofN >= OfMax || bi < 0 || bi >= OfMax || c == null || !(x > 0)) return;
            // oferta, ktora naprawde dochodzi do budzetu wiersza: AddOffer liczy czesc domowa nie mniejsza niz 0.05, a budzet wiersza mnozy ja prawdziwa (plotno 0.02)
            double hs = HistoricalPrices.HouseShare(c);
            _ofCat[_ofN] = c; _ofIdx[_ofN] = bi; _ofX[_ofN] = x; _ofAdd[_ofN] = add; _ofBase[_ofN] = v; _ofEff[_ofN] = hs >= 0.05 ? x : x * Math.Max(0, hs) / 0.05; _ofN++;
        }

        /// <summary>AddOffer: w slowniku budzetu osady nie ma zadnej kategorii "lepszej" - cala oferta zostaje w kiesie (tylko licznik).</summary>
        private static void OfferNoCat(long off)
        {
            if (!_ofOn || off <= 0) return;
            _win.OfNoCat += off; _win.OfNoCatN++;
        }

        /// <summary>
        /// ConsumePostfix (zamkniecie nawiasu, po zliczeniu wydatku kategorii): ile oferty nadwyzki znalazlo towar. Regula pomiaru: oferta wydawana OSTATNIA -
        /// kupione z oferty = wydatek kategorii ponad budzet gry (budzet pierwszego wiersza bez oferty + dodatnie dopisy kolejnych wierszy BK), najwyzej oferta.
        /// Kategoria bez wiersza na polce - cala oferta "bez wiersza". Tylko liczniki okna; wlasny try (nie zaburza zamkniecia nawiasu).
        /// </summary>
        private static void OfferClose(Folk f)
        {
            if (_ofN <= 0) return;
            try
            {
                double sx = 0, sb = 0;
                for (int i = 0; i < _ofN; i++)
                {
                    double x = _ofX[i], eff = _ofEff[i], bought = 0; int bi = _ofIdx[i];
                    Track tr;
                    if (!_brTrack.TryGetValue(_ofCat[i], out tr) || tr == null || tr.Rows == 0 || double.IsNaN(tr.First)) { _win.OfNoRow[bi] += x; _win.OfNoRowN[bi]++; }
                    else
                    {
                        double game = Math.Max(0, tr.First - eff) + Math.Max(0, tr.Add - (tr.First - (_ofBase[i] + _ofAdd[i])));
                        bought = tr.Spent - game;
                        if (bought < 0) bought = 0; else if (bought > eff) bought = eff;
                    }
                    _win.OfX[bi] += x; _win.OfBought[bi] += bought; _win.OfN[bi]++;
                    sx += x; sb += bought;
                }
                _win.OfPlaces++;
                if (f != null) { f.Of7X += sx; f.Of7B += sb; }
            }
            catch (Exception e) { Stumble("OfferClose", e); }
            finally { _ofN = 0; }
        }

        private static string OfferLine(int day, LDay d, LWin w)
        {
            if (_better == null) _better = CatsOf(BetterIds, out _betterText);
            double x = 0, b = 0, nr = 0;
            var sb = new StringBuilder(640);
            var parts = new StringBuilder(320);
            for (int i = 0; i < OfMax && i < _better.Count; i++)
            {
                x += w.OfX[i]; b += w.OfBought[i]; nr += w.OfNoRow[i];
                if (w.OfN[i] == 0) continue;
                if (parts.Length > 0) parts.Append(" ; ");
                parts.Append(_better[i].StringId).Append(' ').Append(N0(w.OfX[i])).Append(" / ").Append(N0(w.OfBought[i])).Append(" / ").Append(N0(w.OfNoRow[i]))
                     .Append("; ").Append(w.OfN[i]).Append(" (").Append(w.OfNoRowN[i]).Append(')');
            }
            sb.Append("Kupione z oferty (P1): dzien ").Append(day)
              .Append(" | oferta nadwyzki dopisana do budzetu kategorii lepszych ").Append(N0(x)).Append(" zl w ").Append(w.OfPlaces).Append(" konsumpcjach osad")
              .Append(" | kupione z oferty ").Append(N0(b)).Append(" zl (").Append(Pct(b, x)).Append("), bez towaru ").Append(N0(Math.Max(0, x - b)))
              .Append(" zl (w tym kategoria bez wiersza na polce ").Append(N0(nr)).Append(')')
              .Append(" | oferta bez kategorii w slowniku budzetu (cala oferta osady - pieniadz zostaje w kiesie) ").Append(N0(w.OfNoCat)).Append(" zl w ").Append(w.OfNoCatN).Append(" konsumpcjach")
              .Append(" | wedlug kategorii (oferta / kupione / bez wiersza na polce; konsumpcji (bez wiersza)): ").Append(parts.Length > 0 ? parts.ToString() : "-")
              .Append(" | oferta ludu na jutro (linia Kiesa ludu): miasta ").Append(d.OffT).Append(", podzamcza ").Append(d.OffC)
              .Append(" | regula pomiaru: kupione z oferty = wydatek kategorii ponad budzet gry (oferta wydawana ostatnia), najwyzej oferta; sam log.");
            return sb.ToString();
        }

        // ============================================================ C2. marza kupcow na towarze z karawan
        private static void CvClear()
        {
            Array.Clear(_cvPaidG, 0, CvGroups); Array.Clear(_cvValG, 0, CvGroups); Array.Clear(_cvWorthG, 0, CvGroups);
            _cvPaid = 0; _cvPieces = 0; _cvVal = 0; _cvN = 0; _cvNoPay = 0;
        }

        /// <summary>TownFolk.OnGold (zdarzenie gry PO przelewie): kasa miasta zaplacila karawanie - czeka na zdarzenie dostawy tej samej osady (tylko licznik).</summary>
        private static void CaravanPaidNote(Settlement town, int a)
        {
            if (a <= 0 || town == null || !MeasureP1) return;
            long x; _cvPend.TryGetValue(town, out x); _cvPend[town] = x + a;
        }

        private static int CvGroupOf(ItemCategory c)
        {
            int g;
            if (_cvGrp.TryGetValue(c, out g)) return g;
            string id = (c.StringId ?? "").ToLowerInvariant();
            if (IsFoodCategory(c)) g = 0;
            else if (Array.IndexOf(LuxIds, id) >= 0) g = 1;
            else if (Array.IndexOf(DryIds[0], id) >= 0 || Array.IndexOf(DryIds[1], id) >= 0 || Array.IndexOf(DryIds[2], id) >= 0) g = 2;
            else g = 3;
            _cvGrp[c] = g;
            return g;
        }

        /// <summary>
        /// Nasluch CampaignEvents.OnCaravanTransactionCompletedEvent (BK po SellGoods / BuyGoods karawany, nasze CaravanBulk i CaravanAmmo): sztuki, ktore karawana
        /// SPRZEDALA miastu (ilosc dodatnia), wycenione po cenie miasta zaraz po dostawie (cena, ktora placi lud i kazdy kupujacy - TownMarketData.GetPrice) wobec
        /// zlota, ktore kasa miasta zaplacila karawanie od poprzedniej dostawy (CaravanPaidNote). Tylko liczniki - nic nie zmienia.
        /// </summary>
        internal static void OnCaravanTrade(MobileParty caravan, Town town, List<(EquipmentElement, int)> items)
        {
            try
            {
                if (town == null || items == null || town.Settlement == null || !town.IsTown || !MeasureP1 || OffMain()) return;
                long t0 = Stopwatch.GetTimestamp();
                var st = town.Settlement;
                double val = 0; long pcs = 0;
                double[] vg = null, wg = null;
                for (int i = 0; i < items.Count; i++)
                {
                    var el = items[i].Item1; int n = items[i].Item2;
                    var it = el.Item;
                    if (n <= 0 || it == null || it.ItemCategory == null) continue;
                    if (vg == null) { vg = new double[CvGroups]; wg = new double[CvGroups]; }
                    double v = (double)n * Math.Max(0, town.MarketData.GetPrice(el));
                    int g = CvGroupOf(it.ItemCategory);
                    vg[g] += v; wg[g] += (double)n * Math.Max(0, el.ItemValue); val += v; pcs += n;
                }
                if (vg == null) return;                                // sam zakup karawany (ilosci ujemne) - nie dostawa
                long paid; _cvPend.TryGetValue(st, out paid);
                if (paid > 0) _cvPend[st] = 0; else _cvNoPay++;
                _cvN++; _cvPaid += paid; _cvVal += val; _cvPieces += pcs;
                for (int g = 0; g < CvGroups; g++)
                {
                    _cvValG[g] += vg[g]; _cvWorthG[g] += wg[g];
                    if (val > 0) _cvPaidG[g] += paid * vg[g] / val;    // zaplata dostawy rozlozona na grupy wedlug wartosci po cenie miasta
                }
                _p1DayTicks += Stopwatch.GetTimestamp() - t0;
            }
            catch (Exception e) { Stumble("OnCaravanTrade", e); }
        }

        private static string Margin(double val, double paid) { return paid > 0 ? ((val / paid - 1.0) * 100.0).ToString("+0.0;-0.0", Inv) + "%" : "-"; }

        private static string CaravanLine(int day)
        {
            long pend = 0; int pendN = 0;
            foreach (var kv in _cvPend) if (kv.Value > 0) { pend += kv.Value; pendN++; }
            _cvPend.Clear();
            string[] gn = { "zywnosc", "zbytki (silver, velvet, jewelry, spice, fur)", "sukno i dom z listy dostatku", "reszta" };
            var sb = new StringBuilder(640);
            sb.Append("Marza kupcow z karawan (P1): dzien ").Append(day)
              .Append(" | dostaw karawan do miast ").Append(_cvN).Append(" (sztuk ").Append(_cvPieces).Append("), kasy miast zaplacily karawanom ").Append(_cvPaid)
              .Append(" zl, ten towar po cenie miasta zaraz po dostawie (cena dla ludu) ").Append(N0(_cvVal)).Append(" zl = marza ").Append(Margin(_cvVal, _cvPaid))
              .Append(" (przed robocizna handlu: kasa placi ludowi 10% obrotu karawany i 4-20% przy sprzedazy)")
              .Append(" | wedlug grupy (zaplacone / po cenie miasta / wartosc sztuk = marza): ");
            for (int g = 0; g < CvGroups; g++)
            {
                if (g > 0) sb.Append(" ; ");
                sb.Append(gn[g]).Append(' ').Append(N0(_cvPaidG[g])).Append(" / ").Append(N0(_cvValG[g])).Append(" / ").Append(N0(_cvWorthG[g])).Append(" = ").Append(Margin(_cvValG[g], _cvPaidG[g]));
            }
            sb.Append(" | dostaw bez zaplaty w oknie (zaplata przypisana innej dostawie tej osady) ").Append(_cvNoPay)
              .Append(", zaplacone karawanom bez zdarzenia dostawy ").Append(pend).Append(" zl w ").Append(pendN).Append(" miastach (poza marza)")
              .Append(" | sam log.");
            return sb.ToString();
        }

        // ============================================================ C3. lojalnosc osad
        private static string LoyaltyLine(int day)
        {
            // [0] miasta, [1] zamki, [2] placace danine dzis, [3] nieplacace
            var n = new int[4]; var sum = new double[4]; var l30 = new int[4]; var l25 = new int[4]; var reb = new int[4];
            int payT = 0, payC = 0;
            string lowName = null, lowPayName = null; double low = double.MaxValue, lowPay = double.MaxValue;
            for (int i = 0; i < _fk.Count; i++)
            {
                var f = _fk[i];
                var town = f.St != null ? f.St.Town : null;
                if (town == null || (f.Kind == 1 && !CastleLive)) continue;
                double loy = town.Loyalty;
                bool pays = f.DAid > 0 && f.ADay == _liveDay, rb = town.InRebelliousState;
                int a = f.Kind == 0 ? 0 : 1, b = pays ? 2 : 3;
                n[a]++; sum[a] += loy; if (loy <= 30) l30[a]++; if (loy <= 25) l25[a]++; if (rb) reb[a]++;
                n[b]++; sum[b] += loy; if (loy <= 30) l30[b]++; if (loy <= 25) l25[b]++; if (rb) reb[b]++;
                if (pays) { if (f.Kind == 0) payT++; else payC++; }
                string nm = f.St.Name != null ? f.St.Name.ToString() : f.St.StringId;
                if (loy < low) { low = loy; lowName = nm + (pays ? " (placi)" : " (nie placi)"); }
                if (pays && loy < lowPay) { lowPay = loy; lowPayName = nm; }
            }
            Func<int, string> seg = i => (n[i] > 0 ? (sum[i] / n[i]).ToString("0.0", Inv) : "-") + ", <= 30: " + l30[i] + ", <= 25: " + l25[i] + ", w stanie buntu " + reb[i];
            return "Lojalnosc osad (P1): dzien " + day + " | miasta " + n[0] + ": srednia " + seg(0) + " | zamki " + n[1] + ": srednia " + seg(1)
                   + " | placace danine z oszczednosci dzis " + n[2] + " (miasta " + payT + ", zamki " + payC + "): srednia " + seg(2)
                   + " | nieplacace " + n[3] + ": srednia " + seg(3)
                   + " | najnizsza: " + (lowName != null ? lowName + " " + low.ToString("0.0", Inv) : "-")
                   + ", wsrod placacych: " + (lowPayName != null ? lowPayName + " " + lowPay.ToString("0.0", Inv) : "-")
                   + " | sam odczyt Town.Loyalty (kara lojalnosci za danine to osobny krok P1b).";
        }

        // ============================================================ C4. zloto rodow z luzem pulapu zoldu (po ClanBudget.Daily - z TownFolk.Lines)
        private static string SlackLine(int day)
        {
            var n = new int[5]; var g = new long[5];
            ClanBudget.SlackGold(n, g);
            return "Rody z luzem pulapu (P1): dzien " + day + " | rody AI z budzetem dzis " + (n[0] + n[1] + n[3]) + " (najemnicy 185 osobno)"
                   + " | na pulapie zoldu (zold partii, zalog i karawan >= 95% pulapu) " + n[0] + " rodow, zloto glow " + g[0]
                   + " | z luzem " + n[1] + " rodow, zloto glow " + g[1] + " (w tym w wojnie " + n[2] + " rodow, zloto " + g[2] + ")"
                   + " | bez pulapu (Straz bez zoldu albo pulap 0) " + n[3] + " rodow, zloto " + g[3]
                   + " | kompanie najemne z umowa " + n[4] + ", zloto glow " + g[4] + " | sam log (budzet 166 tej doby).";
        }

        // ============================================================ C5. zbytki "na sucho" - co by bylo, gdyby targ widzial sakiewke (co 7 dob)
        private static void EnsureDryCats()
        {
            if (_dryCats != null) return;
            _dryCats = new List<ItemCategory>(); _dryGroup = new List<int>(); _drySignal = new List<bool>();
            var seen = new HashSet<string>();
            try
            {
                foreach (var c in ItemCategories.All)
                {
                    if (c == null || c.StringId == null) continue;
                    string id = c.StringId.ToLowerInvariant();
                    for (int g = 0; g < DryIds.Length; g++)
                        if (Array.IndexOf(DryIds[g], id) >= 0 && seen.Add(id)) { _dryCats.Add(c); _dryGroup.Add(g); _drySignal.Add(Array.IndexOf(DryNoSignal, id) < 0); }
                }
            }
            catch (Exception e) { Stumble("EnsureDryCats", e); }
            var miss = new List<string>();
            foreach (var arr in DryIds) foreach (var id in arr) if (!seen.Contains(id)) miss.Add(id);
            _dryMissing = miss.Count > 0 ? string.Join(", ", miss.ToArray()) : "-";
        }

        private sealed class DryTown { public double Offer, I0, I1; public int N, Over; public string Name; }

        private static string DryLine(int day)
        {
            long t0 = Stopwatch.GetTimestamp();
            EnsureDryCats();
            var model = Campaign.Current.Models.TradeItemPriceFactorModel;
            int nc = _dryCats.Count;
            var dem = new double[nc]; var ext = new double[nc]; var i0 = new double[nc]; var i1 = new double[nc]; var over = new int[nc]; var nT = new int[nc];
            var pcs = new long[nc]; var val = new long[nc];
            var sumD = new double[DryIds.Length];
            var towns = new List<DryTown>();
            double offerAll = 0, offerSig = 0; int withOffer = 0, nTowns = 0;
            foreach (var f in _f.Values)
            {
                if (f.Kind != 0 || !f.Cal || f.St == null || f.St.Town == null) continue;
                try
                {
                    var md = f.St.Town.MarketData;
                    if (md == null) continue;
                    nTowns++;
                    double offer = (f.DOvC[0] + DryRpGoods * (f.DOvC[1] + f.DOvC[2])) / DrySpendDays;
                    Array.Clear(sumD, 0, sumD.Length);
                    for (int j = 0; j < nc; j++) sumD[_dryGroup[j]] += Math.Max(0f, md.GetDemand(_dryCats[j]));
                    DryTown dt = null;
                    if (offer >= 1) { withOffer++; offerAll += offer; dt = new DryTown { Offer = offer, Name = f.St.Name != null ? f.St.Name.ToString() : f.St.StringId }; towns.Add(dt); }
                    for (int j = 0; j < nc; j++)
                    {
                        var c = _dryCats[j];
                        var cd = md.GetCategoryData(c);
                        pcs[j] += cd.InStore; val[j] += cd.InStoreValue; dem[j] += Math.Max(0f, cd.Demand);
                        if (dt == null || !_drySignal[j]) continue;
                        int g = _dryGroup[j];
                        double e = sumD[g] > 0 ? offer * DryWeight[g] * Math.Max(0f, cd.Demand) / sumD[g] : 0;
                        if (!(e > 0)) continue;
                        float a = model.GetBasePriceFactor(c, cd.InStoreValue, cd.Supply, cd.Demand, false, 0);
                        float b = model.GetBasePriceFactor(c, cd.InStoreValue, cd.Supply, (float)(cd.Demand + e), false, 0);
                        ext[j] += e; i0[j] += a; i1[j] += b; nT[j]++; offerSig += e;
                        dt.I0 += a; dt.I1 += b; dt.N++;
                        if (b > DryCeiling) { over[j]++; dt.Over++; }
                    }
                }
                catch (Exception e) { Stumble("DryLine(miasto)", e); }
            }
            towns.Sort((x, y) => y.Offer.CompareTo(x.Offer));
            double t0i = 0, t1i = 0, tOffer = 0; int tn = 0, tOver = 0, top = Math.Min(20, towns.Count);
            for (int i = 0; i < top; i++) { t0i += towns[i].I0; t1i += towns[i].I1; tn += towns[i].N; tOver += towns[i].Over; tOffer += towns[i].Offer; }
            var parts = new StringBuilder(900);
            for (int j = 0; j < nc; j++)
            {
                if (parts.Length > 0) parts.Append(" ; ");
                parts.Append(_dryCats[j].StringId).Append(_drySignal[j] ? "" : " (bez sygnalu)").Append(' ').Append(N0(dem[j])).Append(" / +").Append(N0(ext[j])).Append(" / ")
                     .Append(nT[j] > 0 ? F2(i0[j] / nT[j]) : "-").Append(" -> ").Append(nT[j] > 0 ? F2(i1[j] / nT[j]) : "-").Append(" / ").Append(over[j]).Append(" z ").Append(nT[j])
                     .Append(" / ").Append(pcs[j]).Append(" szt. ").Append(val[j]).Append(" zl");
            }
            var sb = new StringBuilder(1800);
            sb.Append("Zbytki na sucho (P1): dzien ").Append(day)
              .Append(" | regula na sucho (projekt, szczebel 2 i targ widzi sakiewke): oferta miasta = (kiesa W ponad zapas + ").Append(F2(DryRpGoods)).Append(" x kiesy R i P ponad zapas, po daninie) / ")
              .Append(N0(DrySpendDays)).Append("; grupy: sukno i odzienie 55%, srebro-klejnoty-korzenie 25%, dom 8% (lepszy stol 12% i plotno, skora - bez sygnalu); w grupie wedlug popytu targu;")
              .Append(" indeks = wzor ceny czynnego modelu (GetBasePriceFactor) z popytem targu i z popytem + oferta; cena graniczna ").Append(F2(DryCeiling))
              .Append(" | miast ").Append(nTowns).Append(", z oferta ").Append(withOffer).Append(": oferta na sucho ").Append(N0(offerAll)).Append(" zl/d, z tego na kategorie z sygnalem ").Append(N0(offerSig))
              .Append(" | kategoria: popyt targu swiata / + oferta / indeks dzis -> na sucho (srednia miast z oferta) / miast ponad ").Append(F2(DryCeiling)).Append(" / na polkach miast: ")
              .Append(parts.Length > 0 ? parts.ToString() : "-")
              .Append(" | ").Append(top).Append(" miast z najwieksza oferta (").Append(N0(tOffer)).Append(" zl/d): indeks dzis ").Append(tn > 0 ? F2(t0i / tn) : "-")
              .Append(" -> na sucho ").Append(tn > 0 ? F2(t1i / tn) : "-").Append(", par miasto-kategoria ponad ").Append(F2(DryCeiling)).Append(": ").Append(tOver).Append(" z ").Append(tn)
              .Append(top > 0 ? "; najwieksza oferta: " + Clean(towns[0].Name) + " " + N0(towns[0].Offer) + " zl/d" : "")
              .Append(" | kategorie nieznalezione w grze: ").Append(_dryMissing)
              .Append(" | sam log - popyt, ceny i zakupy bez zmian | czas ").Append(MsOf(Stopwatch.GetTimestamp() - t0, "0.00")).Append(" ms.");
            return sb.ToString();
        }

        // ============================================================ C6. miasta z warsztatami zbytkow i wsie z surowcem (co 7 dob)
        private static string ShopsLine(int day)
        {
            long t0 = Stopwatch.GetTimestamp();
            EnsureDryCats();
            int nc = _dryCats.Count;
            var shops = new int[nc]; var townsN = new int[nc]; var names = new List<string>[nc]; var vills = new int[nc]; var vk = new Dictionary<string, int>[nc];
            var idx = new Dictionary<ItemCategory, int>();
            for (int j = 0; j < nc; j++) { idx[_dryCats[j]] = j; names[j] = new List<string>(); vk[j] = new Dictionary<string, int>(); }
            int townsWith = 0, townsAll = 0, luxTowns = 0;
            var per = new int[nc];
            foreach (var f in _f.Values)
            {
                if (f.Kind != 0 || f.St == null || f.St.Town == null) continue;
                try
                {
                    townsAll++;
                    Array.Clear(per, 0, nc);
                    int any = 0, lux = 0;
                    var ws = f.St.Town.Workshops;
                    if (ws != null)
                        foreach (var w in ws)
                        {
                            var type = w != null ? w.WorkshopType : null;
                            if (type == null || type.IsHidden || type.Productions == null) continue;
                            bool hit = false, hitLux = false;
                            foreach (var p in type.Productions)
                            {
                                if (p.Outputs == null) continue;                 // Production to struct
                                foreach (var o in p.Outputs)
                                {
                                    int j;
                                    if (o.Item1 == null || !idx.TryGetValue(o.Item1, out j)) continue;
                                    per[j]++; hit = true;
                                    if (Array.IndexOf(LuxIds, (o.Item1.StringId ?? "").ToLowerInvariant()) >= 0) hitLux = true;
                                }
                            }
                            if (hit) any++;
                            if (hitLux) lux++;
                        }
                    f.LuxShops = lux;
                    if (any > 0) townsWith++;
                    if (lux > 0) luxTowns++;
                    string nm = Clean(f.St.Name != null ? f.St.Name.ToString() : f.St.StringId);
                    for (int j = 0; j < nc; j++)
                        if (per[j] > 0) { shops[j] += per[j]; townsN[j]++; if (names[j].Count < 12) names[j].Add(nm + (per[j] > 1 ? " " + per[j] : "")); }
                }
                catch (Exception e) { Stumble("ShopsLine(miasto)", e); }
            }
            try
            {
                foreach (var v in Village.All)
                {
                    if (v == null || v.VillageType == null || v.Settlement == null) continue;
                    var prod = v.VillageType.PrimaryProduction;
                    var cat = prod != null ? prod.ItemCategory : null;
                    int j;
                    if (cat == null || !idx.TryGetValue(cat, out j)) continue;
                    vills[j]++;
                    var k = v.Settlement.OwnerClan != null ? v.Settlement.OwnerClan.Kingdom : null;
                    string kn = Clean(k != null && k.Name != null ? k.Name.ToString() : "bez krolestwa");
                    int x; vk[j].TryGetValue(kn, out x); vk[j][kn] = x + 1;
                }
            }
            catch (Exception e) { Stumble("ShopsLine(wsie)", e); }
            var a = new StringBuilder(900); var b = new StringBuilder(500);
            for (int j = 0; j < nc; j++)
            {
                if (shops[j] > 0)
                {
                    if (a.Length > 0) a.Append(" ; ");
                    a.Append(_dryCats[j].StringId).Append(": miast ").Append(townsN[j]).Append(", linii produkcji ").Append(shops[j]).Append(" (").Append(string.Join(", ", names[j].ToArray()))
                     .Append(townsN[j] > names[j].Count ? ", ..." : "").Append(')');
                }
                if (vills[j] > 0)
                {
                    if (b.Length > 0) b.Append(" ; ");
                    var l = new List<KeyValuePair<string, int>>(vk[j]);
                    l.Sort((x, y) => y.Value.CompareTo(x.Value));
                    b.Append(_dryCats[j].StringId).Append(": wsi ").Append(vills[j]).Append(" (");
                    for (int i = 0; i < l.Count && i < 6; i++) b.Append(i > 0 ? ", " : "").Append(l[i].Key).Append(' ').Append(l[i].Value);
                    b.Append(l.Count > 6 ? ", ..." : "").Append(')');
                }
            }
            return "Warsztaty zbytkow (P1): dzien " + day + " | miast " + townsAll + ", z warsztatem towaru z listy dostatku " + townsWith + ", w tym z warsztatem zbytkow BK (silver, velvet, jewelry, spice, fur) " + luxTowns
                   + " | wedlug wyrobu (typ warsztatu w miescie - wyjscia linii produkcji): " + (a.Length > 0 ? a.ToString() : "-")
                   + " | wsie z towarem z listy jako produkcja glowna, wedlug krolestwa pana: " + (b.Length > 0 ? b.ToString() : "-")
                   + " | sam log | czas " + MsOf(Stopwatch.GetTimestamp() - t0, "0.00") + " ms.";
        }

        // ============================================================ C7. lokata miast z budowa w kolejce i materialem na polce (co 7 dob)
        private static string BuildQueueLine(int day)
        {
            long t0 = Stopwatch.GetTimestamp();
            // [rodzaj 0 miasto / 1 zamek][0 z nadwyzka, 1 kolejka i material, 2 kolejka bez materialu, 3 bez kolejki, 4 oblezone, 5 cywilna w wojnie]
            var n = new int[2, 6]; var gold = new double[2, 6];
            var war = new Dictionary<Kingdom, bool>();
            foreach (var f in _f.Values)
            {
                if (f.Kind > 1 || !f.Cal || f.St == null || f.St.Town == null || (f.Kind == 1 && !CastleLive)) continue;
                try
                {
                    bool mil; int mat;
                    bool q = BuildFunding.QueueHead(f.St, out mil, out mat);
                    f.BQ = q ? (mil ? 2 : 1) : 0; f.BMat = mat;
                    if (f.DOver < 1) continue;
                    int k = f.Kind;
                    n[k, 0]++; gold[k, 0] += f.DOver;
                    if (f.St.IsUnderSiege) { n[k, 4]++; gold[k, 4] += f.DOver; continue; }
                    if (!q) { n[k, 3]++; gold[k, 3] += f.DOver; continue; }
                    int i = mat > 0 ? 1 : 2;
                    n[k, i]++; gold[k, i] += f.DOver;
                    var kg = f.St.OwnerClan != null ? f.St.OwnerClan.Kingdom : null;
                    bool w = false;
                    if (kg != null && !war.TryGetValue(kg, out w))
                    {
                        foreach (var o in Kingdom.All) if (o != null && o != kg && !o.IsEliminated && kg.IsAtWarWith(o)) { w = true; break; }
                        war[kg] = w;
                    }
                    if (w && !mil) { n[k, 5]++; gold[k, 5] += f.DOver; }
                }
                catch (Exception e) { Stumble("BuildQueueLine(osada)", e); }
            }
            var sb = new StringBuilder(700);
            sb.Append("Lokata a budowy (P1): dzien ").Append(day);
            for (int k = 0; k < 2; k++)
                sb.Append(k == 0 ? " | miasta" : " | podzamcza").Append(" z kiesa ponad zapas dni ").Append(n[k, 0]).Append(" (").Append(N0(gold[k, 0])).Append(" zl)")
                  .Append(": budowa w kolejce i material na polce targu ").Append(n[k, 1]).Append(" (").Append(N0(gold[k, 1])).Append(" zl)")
                  .Append(", budowa w kolejce bez materialu ").Append(n[k, 2]).Append(" (").Append(N0(gold[k, 2])).Append(" zl)")
                  .Append(", bez budowy w kolejce ").Append(n[k, 3]).Append(" (").Append(N0(gold[k, 3])).Append(" zl)")
                  .Append(", oblezone ").Append(n[k, 4]).Append(" (").Append(N0(gold[k, 4])).Append(" zl)")
                  .Append("; z budowa cywilna w krolestwie w wojnie (pan jej dzis nie placi) ").Append(n[k, 5]).Append(" (").Append(N0(gold[k, 5])).Append(" zl)");
            sb.Append(" | material: wapien, drewno, glina, narzedzia, marmur, deski na polce miasta (zamek - najblizsze miasto); kiesa ponad zapas po daninie tej doby | sam log | czas ")
              .Append(MsOf(Stopwatch.GetTimestamp() - t0, "0.00")).Append(" ms.");
            return sb.ToString();
        }

        // ============================================================ C8. sklad zarobku miasta (co 7 dob) i plik kiesa-obieg.csv
        private const int EarnCols = 9;
        private static readonly string[] EarnName = { "wojsko", "dwor", "budowy", "kopalnie", "warsztaty i doplata do 35%", "robocizna handlu", "obrot P", "zawor", "inne" };

        private static void EarnSplit(Folk f, long[] to)
        {
            var e = f.EarnK;
            to[0] += e[KLife] + e[KLifeCaravan] + e[KLifeBand] + e[KGarrison];
            to[1] += e[KCourt]; to[2] += e[KBuildLabour]; to[3] += e[KMine];
            to[4] += e[KCraftPaid] + e[ETopUp]; to[5] += e[ELabour]; to[6] += e[ETurn]; to[7] += e[EValve];
            to[8] += e[KShopKeep] + e[KCraftFair] + e[KReleased] + e[EPublic];
        }

        private const string ObiegCsvHead = "dzien;dzien_gry;dob_okna;osada_id;osada;rodzaj;krolestwo;stawka_daniny_proc;powod;lojalnosc;kiesa_W;kiesa_R;kiesa_P;gmina;potrzeba_W;potrzeba_R;potrzeba_P;"
                                            + "ponad_zapas_W;ponad_zapas_R;ponad_zapas_P;danina_dzis;danina_okna;oferta_okna;kupione_z_oferty_okna;"
                                            + "zarobek_okna;z_wojska;z_dworu;z_budow;z_kopaln;z_warsztatow;z_robocizny;z_obrotu;z_zaworu;z_inne;budowa_kolejka;material_szt;warsztaty_zbytkow";

        private static string EarnLine(int day)
        {
            long t0 = Stopwatch.GetTimestamp();
            int days = Math.Max(1, _p1WinDays);
            // [0] miasta z kiesa ponad zapas dni, [1] miasta bez
            var grp = new long[2][]; grp[0] = new long[EarnCols]; grp[1] = new long[EarnCols];
            var gn = new int[2]; var one = new long[EarnCols];
            var top = new List<KeyValuePair<double, string>>();
            var csv = new StringBuilder(48 * 1024);
            foreach (var f in _f.Values)
            {
                if (f.Kind > 1 || !f.Cal || f.St == null || f.St.Town == null || (f.Kind == 1 && !CastleLive)) continue;
                try
                {
                    Array.Clear(one, 0, EarnCols);
                    EarnSplit(f, one);
                    long all = 0; for (int i = 0; i < EarnCols; i++) all += one[i];
                    if (f.Kind == 0)
                    {
                        int g = f.DOver >= 1 ? 0 : 1;
                        gn[g]++; for (int i = 0; i < EarnCols; i++) grp[g][i] += one[i];
                        if (f.DOver >= 1)
                            top.Add(new KeyValuePair<double, string>(f.DOver, Clean(f.St.Name != null ? f.St.Name.ToString() : f.St.StringId) + " " + N0(f.DOver) + " zl (zarobek " + N0((double)all / days)
                                    + " zl/d: wojsko " + Pct(one[0], all) + ", robocizna " + Pct(one[5], all) + ", warsztaty " + Pct(one[4], all) + ", dwor " + Pct(one[1], all) + ")"));
                    }
                    int why; double rate = KingdomTreasury.FolkWarAidRate(f.St, out why);
                    var k = f.St.OwnerClan != null ? f.St.OwnerClan.Kingdom : null;
                    csv.Append(day).Append(';').Append(day + 1).Append(';').Append(days).Append(';').Append(f.St.StringId).Append(';').Append(Clean(f.St.Name != null ? f.St.Name.ToString() : f.St.StringId)).Append(';')
                       .Append(f.Kind == 0 ? "miasto" : "zamek").Append(';').Append(Clean(k != null && k.Name != null ? k.Name.ToString() : "-")).Append(';')
                       .Append((100 * rate).ToString("0.000", Inv)).Append(';').Append(why).Append(';').Append(f.St.Town.Loyalty.ToString("0.0", Inv)).Append(';')
                       .Append(f.KW).Append(';').Append(f.KR).Append(';').Append(f.KP).Append(';').Append(f.KG).Append(';')
                       .Append(F2(f.Need[0])).Append(';').Append(F2(f.Need[1])).Append(';').Append(F2(f.Need[2])).Append(';')
                       .Append(N0(f.DOvC[0])).Append(';').Append(N0(f.DOvC[1])).Append(';').Append(N0(f.DOvC[2])).Append(';')
                       .Append(f.DAid).Append(';').Append(f.Aid7).Append(';').Append(N0(f.Of7X)).Append(';').Append(N0(f.Of7B)).Append(';').Append(all);
                    for (int i = 0; i < EarnCols; i++) csv.Append(';').Append(one[i]);
                    csv.Append(';').Append(f.BQ).Append(';').Append(f.BMat).Append(';').Append(f.LuxShops).Append(Environment.NewLine);
                }
                catch (Exception e) { Stumble("EarnLine(osada)", e); }
                finally { Array.Clear(f.EarnK, 0, f.EarnK.Length); f.Aid7 = 0; f.Of7X = f.Of7B = 0; }
            }
            if (csv.Length > 0) { try { Log.Csv("kiesa-obieg.csv", ObiegCsvHead, csv.ToString()); } catch (Exception e) { Stumble("Csv(kiesa-obieg)", e); } }
            top.Sort((x, y) => y.Key.CompareTo(x.Key));
            var sb = new StringBuilder(1500);
            sb.Append("Sklad zarobku miast (P1): dzien ").Append(day).Append(" | okno ").Append(days).Append(" dob (zarobek ludu z zewnatrz i z kas - Credit; bez przelewow wewnatrz ludu)");
            for (int g = 0; g < 2; g++)
            {
                long all = 0; for (int i = 0; i < EarnCols; i++) all += grp[g][i];
                sb.Append(g == 0 ? " | miasta z kiesa ponad zapas dni " : " | miasta bez nadwyzki ").Append(gn[g]).Append(": zarobek ").Append(N0((double)all / days)).Append(" zl/d");
                for (int i = 0; i < EarnCols; i++) sb.Append(i == 0 ? " - " : ", ").Append(EarnName[i]).Append(' ').Append(Pct(grp[g][i], all));
            }
            sb.Append(" | 10 miast z najwieksza kiesa ponad zapas: ");
            for (int i = 0; i < top.Count && i < 10; i++) sb.Append(i > 0 ? " ; " : "").Append(top[i].Value);
            if (top.Count == 0) sb.Append('-');
            sb.Append(" | osada po osadzie: kiesa-obieg.csv | sam log | czas ").Append(MsOf(Stopwatch.GetTimestamp() - t0, "0.00")).Append(" ms.");
            _p1WinDays = 0;
            return sb.ToString();
        }

        // ============================================================ doba pomiarow (z DailyLive, przed BuildLines) i linie (TownFolk.Lines)
        private static void MeasureP1Day(int day, LDay d, LWin w, bool first, bool weekly)
        {
            _p1Day = -1;
            if (!MeasureP1)
            {
                // pomiar wylaczony: bez linii i bez licznikow okna - zarobek wedlug rodzaju (Credit dopisuje zawsze), danina i oferta okna zerowane co dobe,
                // zeby po wlaczeniu klucza w trakcie sesji pierwsza linia tygodnia nie dzielila zarobku calej sesji przez dni od wlaczenia (recenzja P1)
                CvClear(); _cvPend.Clear(); _p1DayTicks = 0; _p1WinDays = 0;
                foreach (var f in _f.Values) { Array.Clear(f.EarnK, 0, f.EarnK.Length); f.Aid7 = 0; f.Of7X = f.Of7B = 0; }
                return;
            }
            long t0 = Stopwatch.GetTimestamp();
            _p1Day = day; _p1WinDays++;
            try { _lineLoy = LoyaltyLine(day); } catch (Exception e) { Stumble("P1.LoyaltyLine", e); }
            try { _lineOffer = OfferLine(day, d, w); } catch (Exception e) { Stumble("P1.OfferLine", e); }
            try { _lineCar = CaravanLine(day); } catch (Exception e) { Stumble("P1.CaravanLine", e); }
            finally { CvClear(); }
            _p1DayTicks += Stopwatch.GetTimestamp() - t0;
            if (!weekly) return;
            try { _lineDry = DryLine(day); } catch (Exception e) { Stumble("P1.DryLine", e); }
            try { _lineShops = ShopsLine(day); } catch (Exception e) { Stumble("P1.ShopsLine", e); }
            try { _lineBuild = BuildQueueLine(day); } catch (Exception e) { Stumble("P1.BuildQueueLine", e); }
            try { _lineEarn = EarnLine(day); } catch (Exception e) { Stumble("P1.EarnLine", e); }
        }

        /// <summary>TownFolk.Lines (po ksiedze pieniadza i po ClanBudget.Daily tej doby): linie pomiarow P1 - zeruje. Linia rodow z luzem pulapu liczona tutaj
        /// (budzet 166 tej doby jest juz policzony); czas pomiarow doby dopisany do niej.</summary>
        private static void TakeP1Lines(List<string> l)
        {
            if (_p1Day >= 0)
            {
                long t0 = Stopwatch.GetTimestamp();
                string sl = null;
                try { sl = SlackLine(_p1Day); } catch (Exception e) { Stumble("P1.SlackLine", e); }
                _p1DayTicks += Stopwatch.GetTimestamp() - t0;
                foreach (var x in new[] { _lineLoy, _lineOffer, _lineCar }) if (x != null) l.Add(x);
                if (sl != null) l.Add(sl + " | czas pomiarow doby P1 (lojalnosc, oferta, karawany, rody) " + MsOf(_p1DayTicks, "0.00") + " ms.");
                foreach (var x in new[] { _lineDry, _lineShops, _lineBuild, _lineEarn }) if (x != null) l.Add(x);
            }
            _lineLoy = _lineOffer = _lineCar = _lineDry = _lineShops = _lineBuild = _lineEarn = null;
            _p1Day = -1; _p1DayTicks = 0;
        }
    }
}
