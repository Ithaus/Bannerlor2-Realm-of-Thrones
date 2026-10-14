using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Extensions;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// P3-P5 (OBIEG KIESY LUDU, docs/kiesa-ludu/PROJEKT-OBIEG-KIESY-LUDU-2026-10-13.md rozdz. 3.1 szczebel 2, 3.2, 5, 6, 8 wiersze P3-P5; PROJEKT-WYDATKI R-A2..R-A4;
    /// decyzja Jeffa 13.10 "zbytki P3-P5 TAK") - ZBYTKI: co lud robi z pieniedzmi ponad zapas na czarna godzine obok budowy (P2) i daniny (P1).
    ///  P3 LISTA DOSTATKU (klucz FolkLuxuryList): oferta nadwyzki na jutro = 1/FolkSurplusSpendDays (60) kiesy ponad zapas dni PO daninie (W calosc, R i P 30%;
    ///     ich 70% dalej idzie jako place wyrobnikom w DOTYCHCZASOWYM tempie FolkSurplusSpendShare - projekt rozdz. 1 poz. 11); podzamcze 1/60 zamiast 7%.
    ///     Lista z wagami: sukno i odzienie 55%, srebro-klejnoty-korzenie 25%, lepszy stol 12%, dom 8%; w grupie wedlug budzetu gry kategorii w slowniku dnia (jak
    ///     dotad AddOffer). Grupa bez kategorii do kupienia - ta czesc zostaje w kiesie. Poprawka Predict / Share (A 9.9): przewidywany wydatek gry bez kotwicy
    ///     odejmuje oferte KUPIONA (licznik OfferClose), a udzial klas w zaplacie liczy oferte x czesc kupiona w ostatniej konsumpcji tego miasta.
    ///  P4 TARG WIDZI SAKIEWKE (klucz MarketSeesPurse): oferta ludu miasta (i podzamczy oraz wsi, ktore kupuja w nim z listy - P5) na kategorie z sygnalem (lista bez
    ///     lepszego stolu, plotna i skory) dopisana do SZACUNKU popytu targu (GetEstimatedDemandForCategory - z niego gra wygladza popyt rynku 15%/d, a z popytu
    ///     rynku karawana BK liczy, ile wolno jej sprzedac i gdzie jest drogo). Szacunek gry jest juz x m_t (HistoricalPrices.DemandPostfix na
    ///     GetDailyDemandForCategory - PeopleMarket), a oferta ludu idzie za glowami kiesy ludu wedlug ludzi rynku: dopisek jest PO m_t i bez niego (m_t liczone
    ///     raz). Cena graniczna FolkLadderPriceCeiling (1.5): dopisek najwyzej taki, by indeks kategorii (wzor czynnego modelu cen przy dzisiejszej polce) nie
    ///     przekroczyl 1.5; kategoria z indeksem juz ponad 1.5 - bez dopisku i lud jej z nadwyzki nie kupuje (jej czesc idzie na inne kategorie grupy).
    ///     Dopisek tylko w zewnetrznym wywolaniu (BEE_SettlementEconomyModel wola w srodku model gry - licznik zagniezdzenia).
    ///  P5 PODZAMCZA I WSIE NA TARGU MIASTA (klucz WardsAndVillagesShop): podzamcze (miasto najblizsze - jak material budow P2) i wies (miasto targowe) wydaja
    ///     1/60 nadwyzki ponad zapas (10 dni potrzeby / 15 dni zakupow) z tej samej listy, podzialem miasta z jego ostatniej konsumpcji, ta sama droga co zakupy
    ///     wsi (BuyGoods: zaplata kasie kupcow, sztuki z polki - najwyzej ponad 5 ostatnich; ramka ksiegi towarow F26). Wies: koszyk dnia (sol, narzedzia, len,
    ///     garnki, piwo) bez zmian, nadwyzka idzie na liste dostatku zamiast na koszyk 15%/d.
    /// Bez nowych pol zapisu (pamiec sesji w Folk). Kazda osada we wlasnym try, licznik potkniec (CLAUDE.md 7). Wylaczone klucze = stan ed74f40.
    /// </summary>
    internal static partial class TownFolk
    {
        private const int OfLx = 8;                                  // liczniki oferty P1 (OfferNote): 0-7 BetterIds (stara regula), od 8 lista dostatku
        private const int LxTable = 2;                               // grupa "lepszy stol" - zywnosc: bez sygnalu popytu i bez ceny granicznej
        private static readonly string[][] LxIds =
        {
            new[] { "felt", "cotton", "velvet", "linen", "leather", "fur", "garment" },
            new[] { "jewelry", "silver", "spice", "walrus_tusk" },
            new[] { "meat", "cheese", "butter", "beer", "wine", "mead", "oil", "honey" },
            new[] { "pottery", "whale_oil", "papyrus" }
        };
        private static readonly double[] LxWeight = { 0.55, 0.25, 0.12, 0.08 };
        private static readonly string[] LxGroupName = { "sukno i odzienie", "srebro, klejnoty, korzenie", "lepszy stol", "dom" };
        private static readonly string[] LxNoSignal = { "linen", "leather" };   // wsad zbrojowni i wojska (RawPrice liczy ich popyt z prawdziwego zuzycia)

        private static List<ItemCategory> _lxCats; private static int[] _lxGroup; private static bool[] _lxSig;
        private static Dictionary<ItemCategory, int> _lxIdx; private static string _lxMissing = "-";
        private static double[] _lxW = new double[0];
        private static readonly double[] _lxGs = new double[4];
        [ThreadStatic] private static int _lxDepth;
        private static int _lxModels; private static string _lxHook = "nie wpiety";

        // okno (miedzy dobami ludu): konsumpcje miast i zamkow, sygnal popytu w ticku miasta
        private sealed class LxWin
        {
            public readonly double[] OffG = new double[4], BuyG = new double[4];
            public double Off, NoCat, Buy, COff, CBuy, PhiSum; public int Places, Over, Phi, CPlaces;
            public int SigN, SigRoom, SigOver; public double SigOff, SigAdd, SigCut; public long Ticks;
        }
        private static LxWin _lxw = new LxWin();
        // doba ludu (P5)
        private static int _lxCN, _lxCNo, _lxCNoSplit, _lxCNoTown, _lxVN, _lxVNo, _lxVNoSplit, _lxVNoTown, _lxMismatch, _lxSt;
        private static double _lxCWant, _lxVWant; private static long _lxCBuy, _lxVBuy, _lxCPcs, _lxVPcs, _lxDayTicks;
        private static readonly Dictionary<Settlement, List<EquipmentElement>[]> _lxShelf = new Dictionary<Settlement, List<EquipmentElement>[]>();

        // ------------------------------------------------------------ klucze
        /// <summary>P3: lista dostatku czynna (kiesa ludu czynna i klucz FolkLuxuryList).</summary>
        internal static bool LadderOn { get { var s = Settings.Current; return s != null && s.FolkLuxuryList && Live; } }
        /// <summary>P4: targ widzi sakiewke (wymaga P3).</summary>
        private static bool SignalOn { get { var s = Settings.Current; return s != null && s.MarketSeesPurse && LadderOn; } }
        /// <summary>P5: podzamcza i wsie na targu miasta (wymaga P3).</summary>
        private static bool WardsShopOn { get { var s = Settings.Current; return s != null && s.WardsAndVillagesShop && LadderOn; } }
        private static double LxDays(Settings s) { double t = s != null ? s.FolkSurplusSpendDays : 60; return double.IsNaN(t) ? 60 : Math.Max(1, Math.Min(3650, t)); }
        private static double LxCeil(Settings s) { double c = s != null ? s.FolkLadderPriceCeiling : 1.5; return double.IsNaN(c) ? 1.5 : Math.Max(1.0, Math.Min(10.0, c)); }

        private static void LxStumble(string where, Exception e) { _lxSt++; Stumble(where, e); }

        private static void ResetLadder()
        {
            _lxCats = null; _lxGroup = null; _lxSig = null; _lxIdx = null; _lxMissing = "-"; _lxW = new double[0];
            _lxw = new LxWin(); _lxShelf.Clear();
            _lxCN = _lxCNo = _lxCNoSplit = _lxCNoTown = _lxVN = _lxVNo = _lxVNoSplit = _lxVNoTown = _lxMismatch = _lxSt = 0;
            _lxCWant = _lxVWant = 0; _lxCBuy = _lxVBuy = _lxCPcs = _lxVPcs = _lxDayTicks = 0;
        }

        /// <summary>Kategorie listy dostatku z ItemCategories.All (raz na sesje; id bez rozroznienia wielkosci liter - gra ma "Papyrus", "Spice").</summary>
        private static void EnsureLx()
        {
            if (_lxCats != null) return;
            var cats = new List<ItemCategory>(); var grp = new List<int>(); var sig = new List<bool>(); var seen = new HashSet<string>();
            try
            {
                foreach (var c in ItemCategories.All)
                {
                    if (c == null || c.StringId == null) continue;
                    string id = c.StringId.ToLowerInvariant();
                    for (int g = 0; g < LxIds.Length; g++)
                        if (Array.IndexOf(LxIds[g], id) >= 0 && seen.Add(id) && OfLx + cats.Count < OfMax)
                        { cats.Add(c); grp.Add(g); sig.Add(g != LxTable && Array.IndexOf(LxNoSignal, id) < 0); }
                }
            }
            catch (Exception e) { LxStumble("EnsureLx", e); }
            var miss = new List<string>();
            foreach (var arr in LxIds) foreach (var id in arr) if (!seen.Contains(id)) miss.Add(id);
            _lxMissing = miss.Count > 0 ? string.Join(", ", miss.ToArray()) : "-";
            _lxGroup = grp.ToArray(); _lxSig = sig.ToArray(); _lxW = new double[cats.Count];
            var idx = new Dictionary<ItemCategory, int>();
            for (int i = 0; i < cats.Count; i++) idx[cats[i]] = i;
            _lxIdx = idx;
            _lxCats = cats;
        }

        /// <summary>Nazwa kategorii licznika oferty P1 nr i (linia "Kupione z oferty (P1)"): 0-7 BetterIds, od 8 lista dostatku.</summary>
        private static string OfName(int i)
        {
            if (i < OfLx) return _better != null && i < _better.Count && _better[i] != null ? _better[i].StringId : "?";
            int li = i - OfLx;
            return _lxCats != null && li < _lxCats.Count && _lxCats[li] != null ? _lxCats[li].StringId : "?";
        }

        // ============================================================ P3: oferta na liste dostatku (nawias konsumpcji - AddOffer)
        /// <summary>
        /// AddOffer przy kluczu FolkLuxuryList: podzial `off` na liste dostatku - waga grupy x budzet gry kategorii w slowniku dnia / budzet grupy; przy P4 kategoria
        /// z sygnalem i indeksem ponad cene graniczna wypada z podzialu (lud jej nie kupuje). Podzial zapamietany w osadzie (sygnal popytu P4, zakupy P5) - liczony
        /// takze przy ofercie 0. Dopisek do slownika jak dotad: zloto / (indeks^0.15 x czesc domowa). Zwraca dopisany popyt (do G0a).
        /// </summary>
        private static double LadderOffer(Folk f, Town town, Dictionary<ItemCategory, float> dict, long off)
        {
            long t0 = Stopwatch.GetTimestamp();
            try
            {
                EnsureLx();
                var s = Settings.Current;
                int n = _lxCats.Count;
                if (n == 0) { if (off > 0) OfferNoCat(off); return 0; }
                bool sig = s.MarketSeesPurse; double ceil = LxCeil(s);
                if (f.LxFrac == null || f.LxFrac.Length != n) f.LxFrac = new double[n];
                var w = _lxW; if (w.Length != n) { w = new double[n]; _lxW = w; }
                Array.Clear(_lxGs, 0, 4);
                int over = 0;
                for (int i = 0; i < n; i++)
                {
                    w[i] = 0;
                    float v;
                    if (!dict.TryGetValue(_lxCats[i], out v) || !(v > 0)) continue;
                    if (sig && _lxSig[i] && town.GetItemCategoryPriceIndex(_lxCats[i]) > ceil) { over++; continue; }
                    w[i] = v; _lxGs[_lxGroup[i]] += v;
                }
                double alloc = 0;
                for (int i = 0; i < n; i++)
                {
                    int g = _lxGroup[i];
                    double q = _lxGs[g] > 0 ? LxWeight[g] * w[i] / _lxGs[g] : 0;
                    f.LxFrac[i] = q; alloc += q;
                }
                f.LxFracDay = (int)CampaignTime.Now.ToDays;
                if (f.Kind == 0) _lxw.Over += over;
                if (off <= 0) return 0;
                double added = 0;
                for (int i = 0; i < n; i++)
                {
                    double q = f.LxFrac[i];
                    if (!(q > 0)) continue;
                    var c = _lxCats[i];
                    float v = dict[c];
                    double x = off * q;
                    double idx = Math.Max(0.01, town.GetItemCategoryPriceIndex(c));
                    double hs = Math.Max(0.05, HistoricalPrices.HouseShare(c));
                    double add = x / (Math.Pow(idx, OfferIdxPow) * hs);
                    dict[c] = (float)(v + add); added += add;
                    OfferNote(OfLx + i, c, x, add, v);                   // licznik kupionego z oferty (OfferClose) - takze bez pomiaru P1 (poprawka Predict / Share)
                    if (GroupOf(c) == 0) f.OfferFood += x; else f.OfferRest += x;
                    if (f.Kind == 0) _lxw.OffG[_lxGroup[i]] += x;
                }
                double rest = off * Math.Max(0, 1 - alloc);
                if (f.Kind == 0) { _lxw.Off += off; _lxw.NoCat += rest; _lxw.Places++; }
                else { _lxw.COff += off; _lxw.CPlaces++; }
                return added;
            }
            catch (Exception e) { LxStumble("LadderOffer", e); return 0; }
            finally { _lxw.Ticks += Stopwatch.GetTimestamp() - t0; }
        }

        /// <summary>OfferClose: kupione z oferty listy w kategorii li (jedzenie / reszta - poprawka Predict; grupy - linia).</summary>
        private static void LxBought(Folk f, int li, ItemCategory c, double bought)
        {
            if (f == null || _lxGroup == null || li < 0 || li >= _lxGroup.Length || !(bought > 0)) return;
            if (c != null && GroupOf(c) == 0) f.LxBF += bought; else f.LxBR += bought;
            if (f.Kind == 0) _lxw.BuyG[_lxGroup[li]] += bought; else _lxw.CBuy += bought;
        }

        /// <summary>OfferClose: oferta listy tej konsumpcji i ile z niej kupiono - czesc kupiona (poprawka Share), liczby okna People of this land.</summary>
        private static void LxClose(Folk f, double sx, double sb)
        {
            if (f == null || !(sx > 0)) return;
            f.LxPhi = Math.Max(0, Math.Min(1, sb / sx));
            f.DLxOff = sx; f.DLxBuy = sb; f.LxSeen = (int)CampaignTime.Now.ToDays;
            if (f.Kind == 0) { _lxw.Buy += sb; _lxw.Phi++; _lxw.PhiSum += f.LxPhi; }
        }

        /// <summary>Predict (poprawka A 9.9): czesc wydatku nawiasu, ktora byla oferta nadwyzki - przy liscie dostatku KUPIONA (licznik OfferClose), inaczej
        /// oferowana (jak dotad).</summary>
        private static void OfferSpent(Folk f, out double food, out double rest)
        {
            food = f.OfferFood; rest = f.OfferRest;
            if (!LadderOn) return;
            food = Math.Max(0, f.LxBF); rest = Math.Max(0, f.LxBR);
        }

        /// <summary>Anchor (poprawka Share): oferta klasy w udziale zaplaty x czesc kupiona w ostatniej konsumpcji miasta (nieznana - calosc, jak dotad).</summary>
        private static double OfferPayPart(Folk f)
        {
            if (!LadderOn || !(f.LxPhi >= 0)) return 1.0;
            return f.LxPhi;
        }

        // ============================================================ P3: oferta na jutro (krok 5 doby ludu)
        /// <summary>TownLive, koniec kroku 5: oferta na liste dostatku = 1/T kiesy ponad zapas dni po daninie (W calosc; R, P - 30%, ktore nie ida na place).
        /// Place R/P z nadwyzki wyzej - bez zmian (dotychczasowe tempo).</summary>
        private static void LadderOffers(Folk f)
        {
            if (!LadderOn) return;
            long t0 = Stopwatch.GetTimestamp();
            double t = LxDays(Settings.Current);
            f.Offer[0] = (long)(Math.Max(0, f.DOvC[0]) / t);
            f.Offer[1] = (long)((1 - SurpWage) * Math.Max(0, f.DOvC[1]) / t);
            f.Offer[2] = (long)((1 - SurpWage) * Math.Max(0, f.DOvC[2]) / t);
            _lxDayTicks += Stopwatch.GetTimestamp() - t0;
        }

        /// <summary>CastleLiveDay, krok 5: podzamcze - 1/T nadwyzki po daninie: P3 bez P5 - oferta w konsumpcji zamku (lista dostatku), P5 - zakup w miescie najblizszym
        /// (jak material budow zamku) z kiesy podzamcza; oferta zamku 0.</summary>
        private static void CastleLadder(Folk f, LDay d)
        {
            if (!LadderOn) return;
            long t0 = Stopwatch.GetTimestamp();
            try
            {
                double want = Math.Max(0, f.DOvC[0]) / LxDays(Settings.Current);
                f.Offer[0] = 0;
                if (!WardsShopOn) { f.Offer[0] = (long)want; return; }
                f.DLxOff = want; f.DLxBuy = 0; f.LxSeen = _liveDay;
                if (want < 1) return;
                _lxCN++; _lxCWant += want;
                var mk = BuildFunding.NearestTown(f.St);
                if (mk == null || mk.Town == null) { _lxCNoTown++; return; }
                long pcs = 0;
                long got = BuyLadder(null, f, mk, want, ref pcs);
                if (got == -2) { _lxCNoSplit++; return; }
                if (got <= 0) { _lxCNo++; return; }
                _lxCBuy += got; _lxCPcs += pcs; f.DLxBuy = got;
            }
            catch (Exception e) { LxStumble("CastleLadder", e); }
            finally { _lxDayTicks += Stopwatch.GetTimestamp() - t0; }
        }

        // ============================================================ P5: wsie
        /// <summary>Villages: tempo nadwyzki wsi na koszyk dnia - przy P5 0 (nadwyzka idzie na liste dostatku), inaczej FolkSurplusSpendShare (jak dotad).</summary>
        private static double VillExtraRate(Settings s) { return WardsShopOn ? 0 : SpendRate(s); }

        /// <summary>Villages (po koszyku dnia): wies wydaje 1/T kiesy ponad 15 dni zakupow (liczonej przed koszykiem, jak dotad nadwyzka) z listy dostatku na polce
        /// miasta targowego. `gold` - kiesa wsi przed koszykiem.</summary>
        private static void VillageLadder(Folk f, Village vil, Settlement mk, int gold, double buy1, double nv)
        {
            if (!WardsShopOn) return;
            long t0 = Stopwatch.GetTimestamp();
            try
            {
                double want = Math.Max(0, gold - buy1 - RdV * nv) / LxDays(Settings.Current);
                f.DLxOff = want; f.DLxBuy = 0; f.LxSeen = _liveDay;
                if (want < 1) return;
                _lxVN++; _lxVWant += want;
                if (mk == null || mk.Town == null || !mk.IsTown) { _lxVNoTown++; return; }
                long pcs = 0;
                long got = BuyLadder(vil, null, mk, want, ref pcs);
                if (got == -2) { _lxVNoSplit++; return; }
                if (got <= 0) { _lxVNo++; return; }
                _lxVBuy += got; _lxVPcs += pcs; f.DLxBuy = got;
            }
            catch (Exception e) { LxStumble("VillageLadder", e); }
            finally { _lxDayTicks += Stopwatch.GetTimestamp() - t0; }
        }

        /// <summary>Sztuki listy dostatku na polce miasta (najwyzej ponad 5 ostatnich), wedlug kategorii listy - raz na dobe na targ.</summary>
        private static List<EquipmentElement>[] LxShelfOf(Settlement mk)
        {
            List<EquipmentElement>[] a;
            if (_lxShelf.TryGetValue(mk, out a)) return a;
            a = new List<EquipmentElement>[_lxCats.Count];
            var r = mk.ItemRoster;
            if (r != null)
                for (int i = 0; i < r.Count; i++)
                {
                    var el = r.GetElementCopyAtIndex(i);
                    var it = el.EquipmentElement.Item;
                    int li;
                    if (it == null || el.Amount <= ShelfKeep || it.ItemCategory == null || !_lxIdx.TryGetValue(it.ItemCategory, out li)) continue;
                    if (a[li] == null) a[li] = new List<EquipmentElement>();
                    a[li].Add(el.EquipmentElement);
                }
            _lxShelf[mk] = a;
            return a;
        }

        /// <summary>
        /// P5: zakup z listy dostatku na polce miasta `mk` za najwyzej `want` zl - podzialem tego miasta z jego ostatniej konsumpcji (te same wagi, ta sama cena
        /// graniczna), cena targu, najwyzej sztuki ponad 5 ostatnich; najpierw zloto (kiesa wsi albo podzamcza -> kasa kupcow), potem towar z polki (jak BuyGoods).
        /// Oferta liczy sie do sygnalu popytu tego targu (P4). Zwraca zaplacone; -2 - miasto bez podzialu (pierwsza doba, bez konsumpcji), -1 - brak polki.
        /// </summary>
        private static long BuyLadder(Village vil, Folk cf, Settlement mk, double want, ref long pieces)
        {
            Folk mf;
            if (!_f.TryGetValue(mk, out mf) || mf == null || mf.Kind != 0 || mf.LxFrac == null || mf.LxFrac.Length != _lxCats.Count
                || (int)CampaignTime.Now.ToDays - mf.LxFracDay > 2) return -2;
            var town = mk.Town; var roster = mk.ItemRoster;
            if (town == null || roster == null) return -1;
            mf.LxOut += want;                                            // P4: popyt kupujacych z zewnatrz tego targu
            var shelf = LxShelfOf(mk);
            var s = Settings.Current;
            bool sig = s.MarketSeesPurse; double ceil = LxCeil(s);
            long spent = 0;
            for (int i = 0; i < _lxCats.Count; i++)
            {
                double q = mf.LxFrac[i];
                var list = shelf[i];
                if (!(q > 0) || list == null) continue;
                if (sig && _lxSig[i] && town.GetItemCategoryPriceIndex(_lxCats[i]) > ceil) continue;
                double left = want * q;
                foreach (var e in list)
                {
                    if (left < 1) break;
                    int ix = roster.FindIndexOfElement(e);
                    if (ix < 0) continue;
                    int have = roster.GetElementNumber(ix) - ShelfKeep;
                    if (have <= 0) continue;
                    int price = town.MarketData.GetPrice(e);
                    if (price <= 0) continue;
                    int n = (int)Math.Min(have, Math.Floor(left / price));
                    if (n <= 0) continue;
                    long purse = vil != null ? vil.Gold : cf.KW;
                    if ((long)n * price > purse) { n = (int)(purse / price); if (n <= 0) { if (spent > 0) mf.VillWin += spent; return spent; } }
                    int cost = n * price;
                    int got;
                    if (vil != null) { int had = vil.Gold; vil.ChangeGold(-cost); got = had - vil.Gold; }
                    else { got = cost; cf.KW -= cost; _ctlOut += cost; cf.LDayOut += cost; }
                    if (got != cost) _lxMismatch++;
                    if (got <= 0) { if (spent > 0) mf.VillWin += spent; return spent; }
                    town.ChangeGold(got);
                    roster.AddToCounts(e, -n);
                    spent += got; left -= got; pieces += n;
                }
            }
            if (spent > 0) mf.VillWin += spent;                          // podstawa robocizny targu (jak zakupy wsi)
            return spent;
        }

        // ============================================================ doba ludu: poczatek i linia
        /// <summary>DailyLive, przed przejsciem osad: liczniki P5 doby, polki listy od nowa, oferta kupujacych z zewnatrz (P4) od zera.</summary>
        private static void LadderBegin()
        {
            _lxCN = _lxCNo = _lxCNoSplit = _lxCNoTown = _lxVN = _lxVNo = _lxVNoSplit = _lxVNoTown = _lxMismatch = 0;
            _lxCWant = _lxVWant = 0; _lxCBuy = _lxVBuy = _lxCPcs = _lxVPcs = 0;
            _lxShelf.Clear();
            for (int i = 0; i < _fk.Count; i++) if (_fk[i] != null) _fk[i].LxOut = 0;
        }

        /// <summary>DailyLive, po wsiach: linia "Zbytki ludu (P3-P5)" (okno konsumpcji i sygnalu od poprzedniej doby + P5 tej doby); kupione z oferty okna
        /// (poprawka Predict) od zera.</summary>
        private static void LadderDay(int day, LDay d)
        {
            long t0 = Stopwatch.GetTimestamp();
            var w = _lxw; _lxw = new LxWin();
            foreach (var f in _f.Values) { f.LxBF = f.LxBR = 0; }
            var s = Settings.Current;
            if (s == null) return;
            bool on = LadderOn;
            int st = _lxSt; _lxSt = 0;
            try
            {
                EnsureLx();
                // oferta na jutro miast; 20 miast z najwieksza oferta (z kupujacymi z zewnatrz): indeks kategorii z sygnalem i zywnosci; kiesy 31 najbiedniejszych miast
                double offW = 0, offRP = 0; var towns = new List<KeyValuePair<double, Folk>>(); var purses = new List<long>();
                for (int i = 0; i < _fk.Count; i++)
                {
                    var f = _fk[i];
                    if (f == null || f.Kind != 0 || f.St == null || f.St.Town == null) continue;
                    offW += Math.Max(0, f.Offer[0]); offRP += Math.Max(0, f.Offer[1]) + Math.Max(0, f.Offer[2]);
                    towns.Add(new KeyValuePair<double, Folk>(Math.Max(0, f.Offer[0] + f.Offer[1] + f.Offer[2]) + Math.Max(0, f.LxOut), f));
                    purses.Add(Math.Max(0, f.KW) + Math.Max(0, f.KR) + Math.Max(0, f.KP));
                }
                towns.Sort((a, b) => b.Key.CompareTo(a.Key));
                purses.Sort();
                long poor = 0; for (int i = 0; i < purses.Count && i < 31; i++) poor += purses[i];
                double ceil = LxCeil(s), iSig = 0, iFood = 0, topOff = 0; int nSig = 0, nFood = 0, nOver = 0, top = Math.Min(20, towns.Count);
                for (int k = 0; k < top; k++)
                {
                    var town = towns[k].Value.St.Town; topOff += towns[k].Key;
                    for (int i = 0; i < _lxCats.Count; i++)
                    {
                        double ix = town.GetItemCategoryPriceIndex(_lxCats[i]);
                        if (_lxSig[i]) { iSig += ix; nSig++; if (ix > ceil) nOver++; }
                        else if (_lxGroup[i] == LxTable) { iFood += ix; nFood++; }
                    }
                }
                var sb = new StringBuilder(2000);
                sb.Append("Zbytki ludu (P3-P5): dzien ").Append(day)
                  .Append(" | lista dostatku (P3) ").Append(on ? "tak" : "nie").Append(", tempo 1/").Append(N0(LxDays(s))).Append(", place R/P z nadwyzki w tempie ").Append(F2(SpendRate(s)))
                  .Append(" | targ widzi sakiewke (P4) ").Append(s.MarketSeesPurse && on ? "tak" : "nie").Append(", cena graniczna ").Append(F2(ceil))
                  .Append(" | podzamcza i wsie na targu (P5) ").Append(s.WardsAndVillagesShop && on ? "tak" : "nie")
                  .Append(" | miasta: oferta ").Append(N0(w.Off)).Append(" zl w ").Append(w.Places).Append(" konsumpcjach (grupy: ");
                for (int g = 0; g < 4; g++) sb.Append(g > 0 ? ", " : "").Append(LxGroupName[g]).Append(' ').Append(N0(w.OffG[g]));
                sb.Append("; grupa bez kategorii do kupienia - zostaje w kiesie ").Append(N0(w.NoCat)).Append(')')
                  .Append(", kupione z oferty ").Append(N0(w.Buy)).Append(" zl (").Append(Pct(w.Buy, w.Off)).Append("; grupy: ");
                for (int g = 0; g < 4; g++) sb.Append(g > 0 ? ", " : "").Append(LxGroupName[g]).Append(' ').Append(N0(w.BuyG[g]));
                sb.Append("), par miasto-kategoria ponad cena graniczna (lud nie kupuje) ").Append(w.Over)
                  .Append(" | udzial oferty w zaplacie (Share): miast ").Append(w.Phi).Append(", kupione / oferta srednio ").Append(w.Phi > 0 ? F2(w.PhiSum / w.Phi) : "-")
                  .Append(" | oferta na jutro: miasta ").Append(N0(offW + offRP)).Append(" (W ").Append(N0(offW)).Append(", R i P ").Append(N0(offRP)).Append(')')
                  .Append(" | sygnal popytu (P4): par z sygnalem ").Append(w.SigN).Append(", oferta do sygnalu ").Append(N0(w.SigOff)).Append(" zl/d, dopisane do szacunku popytu ")
                  .Append(N0(w.SigAdd)).Append(", obciete cena graniczna ").Append(N0(w.SigCut)).Append(", par bez miejsca (indeks juz >= cena graniczna) ").Append(w.SigRoom)
                  .Append("; kontrola: sygnal ponad cene graniczna ").Append(w.SigOver)
                  .Append(" | ").Append(top).Append(" miast z najwieksza oferta (").Append(N0(topOff)).Append(" zl/d): indeks kategorii z sygnalem srednio ").Append(nSig > 0 ? F2(iSig / nSig) : "-")
                  .Append(" (ponad cena graniczna ").Append(nOver).Append(" z ").Append(nSig).Append("), indeks lepszego stolu srednio ").Append(nFood > 0 ? F2(iFood / nFood) : "-")
                  .Append(" | zamki w konsumpcji zamku (P3 bez P5): oferta ").Append(N0(w.COff)).Append(" w ").Append(w.CPlaces).Append(", kupione ").Append(N0(w.CBuy))
                  .Append(" | podzamcza (P5): z nadwyzka ").Append(_lxCN).Append(", chciane ").Append(N0(_lxCWant)).Append(" zl, kupione ").Append(_lxCBuy).Append(" zl (sztuk ").Append(_lxCPcs)
                  .Append("), bez towaru ").Append(_lxCNo).Append(", bez podzialu targu ").Append(_lxCNoSplit).Append(", bez miasta ").Append(_lxCNoTown)
                  .Append(" | wsie (P5): z nadwyzka ").Append(_lxVN).Append(", chciane ").Append(N0(_lxVWant)).Append(" zl, kupione ").Append(_lxVBuy).Append(" zl (sztuk ").Append(_lxVPcs)
                  .Append("), bez towaru ").Append(_lxVNo).Append(", bez podzialu targu ").Append(_lxVNoSplit).Append(", bez miasta targowego ").Append(_lxVNoTown)
                  .Append(" | kiesy 31 najbiedniejszych miast ").Append(poor)
                  .Append(" | kontrola: zaplata rozna od ceny x sztuk ").Append(_lxMismatch)
                  .Append(" | latka szacunku popytu: ").Append(_lxHook).Append(" | kategorie nieznalezione: ").Append(_lxMissing)
                  .Append(" | czas: doba ").Append(MsOf(_lxDayTicks + Stopwatch.GetTimestamp() - t0, "0.000")).Append(" ms, konsumpcja i sygnal ").Append(MsOf(w.Ticks, "0.000"))
                  .Append(" ms | potkniecia ").Append(st).Append('.');
                Log.Info(sb.ToString());
            }
            catch (Exception e) { LxStumble("LadderDay", e); }
            finally { _lxDayTicks = 0; }
        }

        // ============================================================ P4: oferta ludu w szacunku popytu targu
        /// <summary>Prefiks GetEstimatedDemandForCategory: licznik zagniezdzenia (BEE wola w srodku model gry - dopisek tylko w zewnetrznym wywolaniu).</summary>
        public static void LxEstPrefix() { _lxDepth++; }
        public static Exception LxEstFinalizer(Exception __exception) { if (_lxDepth > 0) _lxDepth--; return __exception; }

        /// <summary>
        /// Postfiks GetEstimatedDemandForCategory(miasto, dane rynku, kategoria) - zewnetrzne wywolanie: kategoria z sygnalem z podzialu miasta (ostatnia konsumpcja)
        /// - dopisek = (oferta ludu na jutro + oferta podzamczy i wsi tego targu) x czesc kategorii, obciety tak, by indeks czynnego modelu cen przy dzisiejszej
        /// polce i popycie (szacunek + dopisek) nie przekroczyl ceny granicznej; indeks juz ponad nia - bez dopisku. Szacunek gry jest juz x m_t (bez drugiego m_t).
        /// Wyjatek - szacunek bez zmian.
        /// </summary>
        public static void LxEstPostfix(Town __0, ItemCategory __2, ref float __result)
        {
            if (_lxDepth != 1 || __0 == null || __2 == null) return;
            long t0 = 0;
            try
            {
                var map = _lxIdx;
                if (map == null) { if (!SignalOn) return; EnsureLx(); map = _lxIdx; if (map == null) return; }
                int i;
                if (!map.TryGetValue(__2, out i) || !_lxSig[i] || !SignalOn || OffMain()) return;
                t0 = Stopwatch.GetTimestamp();
                Folk f;
                if (__0.Settlement == null || !_f.TryGetValue(__0.Settlement, out f) || f == null || f.Kind != 0 || f.LxFrac == null || i >= f.LxFrac.Length) return;
                if ((int)CampaignTime.Now.ToDays - f.LxFracDay > 2) return;
                double q = f.LxFrac[i];
                if (!(q > 0)) return;
                double e = (Math.Max(0, f.Offer[0] + f.Offer[1] + f.Offer[2]) + Math.Max(0, f.LxOut)) * q;
                if (!(e >= 1)) return;
                var model = Campaign.Current.Models.TradeItemPriceFactorModel;
                var md = __0.MarketData;
                if (model == null || md == null) return;
                var cd = md.GetCategoryData(__2);
                double ceil = LxCeil(Settings.Current);
                float d0 = Math.Max(0f, __result);
                _lxw.SigOff += e;
                float i0 = model.GetBasePriceFactor(__2, cd.InStoreValue, cd.Supply, d0, false, 0);
                if (i0 >= ceil) { _lxw.SigRoom++; _lxw.SigCut += e; return; }
                double ep = e;
                if (model.GetBasePriceFactor(__2, cd.InStoreValue, cd.Supply, (float)(d0 + e), false, 0) > ceil)
                {
                    double lo = 0, hi = e;
                    for (int k = 0; k < 16; k++)
                    {
                        double mid = 0.5 * (lo + hi);
                        if (model.GetBasePriceFactor(__2, cd.InStoreValue, cd.Supply, (float)(d0 + mid), false, 0) <= ceil) lo = mid; else hi = mid;
                    }
                    ep = lo; _lxw.SigCut += e - ep;
                    if (ep > 0 && model.GetBasePriceFactor(__2, cd.InStoreValue, cd.Supply, (float)(d0 + ep), false, 0) > ceil + 1e-3) _lxw.SigOver++;
                }
                if (ep > 0) { __result = (float)(d0 + ep); _lxw.SigN++; _lxw.SigAdd += ep; }
            }
            catch (Exception ex) { LxStumble("LxEstPostfix", ex); }
            finally { if (t0 != 0) _lxw.Ticks += Stopwatch.GetTimestamp() - t0; }
        }

        /// <summary>SubModuleMain (jak RawPrice - modele gry i BetterEconomy bez pol statycznych z tekstow): prefiks / postfiks / finalizer na kazdym modelu
        /// ekonomii osad, ktory deklaruje GetEstimatedDemandForCategory. Wpiete zawsze, licza przy kluczu MarketSeesPurse.</summary>
        internal static void LadderApplyAll(Harmony h)
        {
            var names = new List<string>();
            try
            {
                foreach (var t in RawPrice.ModelsDeclaring(typeof(SettlementEconomyModel), "GetEstimatedDemandForCategory"))
                {
                    try
                    {
                        var m = t.GetMethod("GetEstimatedDemandForCategory", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly);
                        var p = m != null ? m.GetParameters() : null;
                        if (m == null || m.ReturnType != typeof(float) || p.Length != 3 || p[0].ParameterType != typeof(Town) || p[2].ParameterType != typeof(ItemCategory)) continue;
                        h.Patch(m, prefix: new HarmonyMethod(typeof(TownFolk), nameof(LxEstPrefix)) { priority = Priority.First },
                                   postfix: new HarmonyMethod(typeof(TownFolk), nameof(LxEstPostfix)) { priority = Priority.Last },
                                   finalizer: new HarmonyMethod(typeof(TownFolk), nameof(LxEstFinalizer)));
                        _lxModels++; names.Add(t.Name);
                    }
                    catch (Exception e) { Log.Error("TownFolk.LadderApplyAll (" + t.FullName + ")", e); }
                }
            }
            catch (Exception e) { Log.Error("TownFolk.LadderApplyAll", e); }
            _lxHook = _lxModels > 0 ? "wpieta w " + _lxModels + " (" + string.Join(", ", names.ToArray()) + ")" : "BRAK";
            var s = Settings.Current;
            Log.Info("Zbytki ludu (P3-P5): targ widzi sakiewke - oferta ludu w szacunku popytu targu " + _lxHook + "; klucze przy starcie: lista dostatku "
                     + (s != null && s.FolkLuxuryList ? "tak" : "nie") + ", targ widzi sakiewke " + (s != null && s.MarketSeesPurse ? "tak" : "nie")
                     + ", podzamcza i wsie na targu " + (s != null && s.WardsAndVillagesShop ? "tak" : "nie") + " (stan - linia doby).");
        }

        // ============================================================ okno People of this land
        /// <summary>
        /// P3/P5 (rozdz. 7): zdanie "Better living: ..." dla osady, w ktorej stoi gracz - miasto: oferta z oszczednosci w ostatniej konsumpcji i ile znalazlo towar;
        /// zamek i wies (P5): ile wydali na targu. Tylko odczyt. null - bez zdania (klucz wylaczony, brak liczb ostatniej doby, nic ponad zapas).
        /// </summary>
        internal static string LuxuryLine(Settlement st, out string log)
        {
            log = null;
            if (st == null || !LadderOn) return null;
            Folk f;
            if (!_f.TryGetValue(st, out f) || f == null || f.Kind < 0 || f.Kind > 2) return null;
            int now = (int)CampaignTime.Now.ToDays;
            if (f.LxSeen < 0 || now - f.LxSeen > 2 || !(f.DLxOff >= 1)) return null;
            string head = "PeopleMenu (P3 Better living): dzien " + (now - 1) + " | " + Clean(st.Name != null ? st.Name.ToString() : st.StringId) + " (" + st.StringId + ")";
            log = head + " | oferta z listy dostatku " + N0(f.DLxOff) + ", kupione " + N0(f.DLxBuy) + " (doba " + f.LxSeen + ")";
            const string what = "cloth, silver and a better table";
            if (f.Kind == 0)
            {
                double none = Math.Max(0, f.DLxOff - f.DLxBuy);
                return "Better living: at the last market day the townsfolk offered " + Coins(f.DLxOff) + " of their savings for " + what + "; "
                       + (f.DLxBuy >= 1 ? Coins(f.DLxBuy) + " of it found goods" : "none of it found goods") + (none >= 1 && f.DLxBuy >= 1 ? ", " + Coins(none) + " found none" : "") + ".";
            }
            if (!WardsShopOn) return null;
            string who = f.Kind == 1 ? "castle folk" : "villagers";
            Settlement mk = f.Kind == 1 ? BuildFunding.NearestTown(st) : (st.Village != null ? (st.Village.TradeBound ?? st.Village.Bound) : null);
            string at = mk != null && mk.Name != null ? " at the market of " + mk.Name : "";
            if (f.DLxBuy >= 1)
                return "Better living: the " + who + " spent " + Coins(f.DLxBuy) + " of their savings" + at + " on " + what + "; " + Coins(Math.Max(0, f.DLxOff - f.DLxBuy)) + " found no goods.";
            return "Better living: the " + who + " would spend " + Coins(f.DLxOff) + " of their savings" + at + " on " + what + ", but found no goods.";
        }
    }
}
