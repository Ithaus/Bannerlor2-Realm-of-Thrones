using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// KS (kalibracja startu, StartCalibration.Live.cs): PLON POL MIESZCZAN - kasa miasta (kupcy) placi ludowi miasta za towar z jego pol (chlopi miasta, niewolni
    /// latyfundiow - ich czesc do wlascicieli R40/P60 jak kazda praca niewolnych, wyrobnicy przy zniwach). Ta sama droga co place z kasy (FromCoffer): kasa placi najwyzej
    /// tyle, ile ma; zloto nie powstaje (kasa -> kiesy ludu). Wolac tylko z bloku doby (MoneyLedger: pozycja MKs).
    /// KS etap 2 - ZC4 KOSZYK WSI (Zc4BuyVillage, z Villages): wies zastosowana kupuje w miescie targowym to, czego nie zjada z wlasnego pola (StartCalibration.Zc4VillageNeed)
    /// x lambda, DRABINA ZAKUPOW (projekt ZC rozdz. 8.4): (1) ziarno i sol, (2) napoj - najtanszy za litr najpierw; czego brak: wino -> piwo, piwo -> ziarno na piwo domowe
    /// (0.4 kg na litr), (3) bialko - najtansze za 1000 kcal najpierw (mieso, mieso wieloryba, ryby, ser, maslo, jaja), (4) reszta jedzenia, przedza i rzeczy po kolei;
    /// po cenie targu, najwyzej sztuki ponad 5 na polce i najwyzej do kiesy wsi - pusta kiesa konczy zakupy (dalsze szczeble czekaja). Kiesa wsi -> kasa miasta (jak
    /// BuyGoods), towar z polki zuzyty we wsi (ramka ksiegi towarow "zakupy wsi w miastach"). Ulamek sztuki przechodzi na jutro (bez zaleglosci ponad sztuke).
    /// </summary>
    internal static partial class TownFolk
    {
        private static long _ksPaid; private static int _ksPayN;
        private static readonly Dictionary<Village, float[]> _zcCarry = new Dictionary<Village, float[]>();
        private static readonly int[] ZcDrinks = { StartCalibration.ZBeer, StartCalibration.ZWine, StartCalibration.ZMead };
        private static readonly int[] ZcProtein = { StartCalibration.ZMeat, StartCalibration.ZWhale, StartCalibration.ZFish, StartCalibration.ZCheese, StartCalibration.ZButter, StartCalibration.ZEgg };
        private static readonly double[] ZcKcal = BuildZcKcal();
        private static double[] BuildZcKcal()
        {
            var k = new double[StartCalibration.ZN];
            k[StartCalibration.ZMeat] = 2200; k[StartCalibration.ZWhale] = 2200; k[StartCalibration.ZFish] = 1100; k[StartCalibration.ZCheese] = 3600; k[StartCalibration.ZButter] = 7200; k[StartCalibration.ZEgg] = 1450;
            return k;
        }

        /// <summary>KS: kasa miasta st -> kiesy ludu (podzial "inne - wyrobnicy", niewolni do wlascicieli). Zwraca to, co naprawde zeszlo z kasy (0 - kiesa nieczynna,
        /// nie miasto, poza watkiem glownym, kasa pusta). Nie rzuca.</summary>
        internal static long KsFieldPay(Settlement st, long x)
        {
            try
            {
                if (x <= 0 || st == null || !st.IsTown || !Live || OffMain()) return 0;
                EnsureImported();
                var f = Get(st);
                if (f == null || f.Kind != 0) return 0;
                long got = FromCoffer(f, x, -1);
                if (got <= 0) return 0;
                _win.FromTown += got;
                _ksPaid += got; _ksPayN++;
                return got;
            }
            catch (Exception e) { Stumble("KsFieldPay", e); return 0; }
        }

        /// <summary>KS: zaplacone od ostatniego odczytu (zl, przelewy) - do linii KS; zeruje.</summary>
        internal static long KsTakePaid(out int n) { long p = _ksPaid; n = _ksPayN; _ksPaid = 0; _ksPayN = 0; return p; }

        /// <summary>KS-R3 KS-H (fracht morzem): kasa miasta celu `payer` -> kiesa wyrobnikow miasta zrodla `src` (zaloga portu). Zwraca to, co naprawde przeszlo
        /// (0 - kiesa nieczynna, nie miasto, poza watkiem glownym - wolajacy placi wtedy kasie zrodla). Nie rzuca.</summary>
        internal static long KsCrewPay(Town payer, Settlement src, long x)
        {
            try
            {
                if (x <= 0 || payer == null || src == null || !src.IsTown || !Live || OffMain()) return 0;
                EnsureImported();
                var f = Get(src);
                if (f == null || f.Kind != 0) return 0;
                int ask = (int)Math.Min(int.MaxValue, Math.Min(x, Math.Max(0, payer.Gold)));
                if (ask <= 0) return 0;
                int had = payer.Gold;
                payer.ChangeGold(-ask);
                long got = had - payer.Gold;
                if (got <= 0) return 0;
                Credit(f, got, -1);                                  // cala kwota do wyrobnikow (W)
                _ctlIn += got; _win.FromTown += got;
                return got;
            }
            catch (Exception e) { Stumble("KsCrewPay", e); return 0; }
        }

        /// <summary>Zakupy jednego koszyka wsi (stan na czas wywolania Zc4BuyVillage).</summary>
        private sealed class ZcBuy
        {
            public Village V; public Settlement Mk; public Town T; public TaleWorlds.CampaignSystem.Roster.ItemRoster R; public LDay D;
            public long Spent, Pcs, UnGold; public double Want, Got; public int NoGold, NoGoods; public bool Broke;   // UnGold: sztuki niekupione z braku kiesy
            public readonly double[] Rw = new double[3], Rg = new double[3];
            public readonly int[] Miss = new int[StartCalibration.ZN];   // F2 (poprawka po autotescie 16.10): sztuki bez towaru na polce miasta targowego - szukane w miastach obok
            public long NoGoodsPcs, Elsewhere;
            public double KcalW, KcalG, ZlW, ZlG;                        // recenzja 17.10: kcal i wartosc (ceny polki) chciane / kupione - jedzenie robione w domu, linia ZC4
            // KS-R3 G2: drabina 2 - kupuje tylko z kiesy ponad Lim i tylko ponad cel polki miasta (Pass2); liczniki drabiny 2 (INFO)
            public long Lim; public bool Pass2; public double P2Got, P2Shelf, P2Gold;
            // KS-R3 (port symulatora 17.10, runda 2): drabina 2 w miastach obok (F2) - kcal i wartosc chciane juz policzone w miescie targowym (bez drugiego liczenia)
            public bool Alt;
            public bool P1;                                              // recenzja runda 3: drabina 1 koszyka zaplaconego z Planu (wartosc kupiona do NKS-13)
        }

        /// <summary>Cena sztuki towaru koszyka na polce miasta (0 - brak wiersza albo ceny): cena miasta z doby (StartCalibration.ZPrice - recenzja 16.10, koszt),
        /// bez niej - gra. Plotno: tylko ponad prog polki miasta (MaterialOrders.ShelfReserve - zapas wojska), jak BuyGoods przy VillageClothAboveReserve.</summary>
        private static int ZcPrice(ZcBuy b, int z, out EquipmentElement e, out int have)
        {
            e = default(EquipmentElement); have = 0;
            var it = StartCalibration.ZItem(z);
            if (it == null) return 0;
            e = new EquipmentElement(it, null, null, false);
            // koszt (poprawka po autotescie 16.10: ok. 45 szukan liniowych na wies, 3.5 ms/d): wiersz z doby (Zc4Build), sprawdzony; inaczej szukanie
            // KS-R3 H2: -1 z mapy doby = towaru nie bylo na polce (ufamy na cala dobe - bez szukania); przyjazd KS-H zmienia wiersz na -2 (szukanie)
            // recenzja runda 3 (wazne, H2): -1 tylko przy tej samej liczbie wierszy polki co przy ostatnim sprawdzeniu braku (nowy wiersz - towar przyjechal - szukany);
            // wynik szukania zapisany w mapie doby (nastepne wsie bez szukania)
            int rc = b.R.Count;
            int ix = StartCalibration.ZRow(b.T, z, rc);
            if (ix == -1 && StartCalibration.SwBuyEvery2) return 0;
            if (ix < 0 || ix >= rc || !ReferenceEquals(b.R.GetElementCopyAtIndex(ix).EquipmentElement.Item, it) || b.R.GetElementCopyAtIndex(ix).EquipmentElement.ItemModifier != null)
            {
                ix = b.R.FindIndexOfElement(e);
                StartCalibration.ZRowSet(b.T, z, ix, rc);
            }
            if (ix < 0) return 0;
            int keep = ShelfKeep;
            if (z == StartCalibration.ZLinen && VillClothOn) { int r = MaterialOrders.ShelfReserve(b.T, it); if (r > keep) keep = r; }
            have = b.R.GetElementNumber(ix) - keep;
            int p = StartCalibration.ZPrice(b.T, z);
            if (p <= 0) p = b.T.MarketData.GetPrice(e);
            return p > 0 ? p : 0;
        }

        /// <summary>Kupuje najwyzej n sztuk towaru z (szczebel rung do licznikow); zwraca kupione. Pusta kiesa - Broke (dalsze szczeble czekaja).</summary>
        private static int ZcTake(ZcBuy b, int z, int n, int rung, bool count = true)
        {
            if (n <= 0) return 0;
            if (b.Pass2) return ZcTake2(b, z, n, rung);
            // KS-R3 H4: jedna cena z mapy doby na wywolanie (ZcPrice) - wartosc chciana przy tej cenie; G2: polka przed pusta kiesa (bez towaru to nie brak pieniedzy)
            EquipmentElement e; int have;
            int price = ZcPrice(b, z, out e, out have);
            if (count)
            {
                b.Want += n; b.Rw[rung] += n;
                b.KcalW += n * StartCalibration.ZKg(z) * KsBasket.Kcal[z];
                int pw = price; if (pw <= 0) { var iw = StartCalibration.ZItem(z); pw = iw != null ? Math.Max(1, iw.Value) : 0; }
                b.ZlW += (double)n * pw;
            }
            if (price <= 0 || have <= 0) { b.NoGoods++; if (count) { b.NoGoodsPcs += n; b.Miss[z] += n; } return 0; }
            if (b.Broke) { if (count) b.UnGold += Math.Min(n, have); if (count && have < n) { b.NoGoodsPcs += n - have; b.Miss[z] += n - have; } return 0; }
            if (count && have < n) { b.NoGoodsPcs += n - have; b.Miss[z] += n - have; }
            int k = Math.Min(n, have);
            long room = (long)b.V.Gold - Math.Max(0, b.Lim);                    // G2: zapas kiesy (towary po ziarnie i soli - ponad 7 dob ziarna i soli z Planu)
            if (room < price) { if (count) b.UnGold += k; if (b.Lim <= 0) { b.NoGold++; b.Broke = true; } return 0; }
            int had = b.V.Gold;
            if ((long)k * price > room) { if (count) b.UnGold += k - (int)(room / price); k = (int)(room / price); if (k <= 0) { b.NoGold++; if (b.Lim <= 0) b.Broke = true; return 0; } }
            b.V.ChangeGold(-k * price);
            int paid = had - b.V.Gold;
            if (paid <= 0) { b.NoGold++; b.Broke = true; return 0; }
            b.T.ChangeGold(paid);
            b.R.AddToCounts(e, -k);
            b.Spent += paid; b.Pcs += k; b.Got += k; b.Rg[rung] += k;
            b.KcalG += k * StartCalibration.ZKg(z) * KsBasket.Kcal[z]; b.ZlG += paid;
            b.D.VBuy += paid; b.D.VPieces += k;
            if (b.P1) StartCalibration.Zc4NotePass1Zl(b.Mk, paid);        // recenzja runda 3: wartosc kupiona drabiny 1 (NKS-13)
            NoteVillBought(b.Mk, e.Item.ItemCategory, paid);
            return k;
        }

        /// <summary>KS-R3 G2 drabina 2 (reszta pelnego koszyka ponad zaplacone z Planu): tylko z kiesy ponad Lim (7 dob budzetu Planu) i tylko ze stanu polki ponad 1.5 x cel
        /// polki miasta (T* x popyt zaplacony; KS-R3 runda 3 - pas do 1.5 x celu dla kupcow KS-H) - liczniki INFO, bez "chcianych" i bez miary bledu drabiny 1.</summary>
        private static int ZcTake2(ZcBuy b, int z, int n, int rung)
        {
            EquipmentElement e; int have;
            int price = ZcPrice(b, z, out e, out have);
            // KS-R3 (port symulatora 17.10, runda 2): chciane kcal i wartosc tylko w miescie targowym - reszta szukana w miastach obok (F2) to ta sama potrzeba (dotad
            // kazde miasto obok dopisywalo ja jeszcze raz: kcal chciane wsi do 2-3 razy koszyka, "kupione" w linii ZC4 i NKS-8c o polowe za nisko, czesc kupiona wsi
            // _zvBought za niska - racje z pracy domowej za wysokie; symulator: bez pokrycia Westeros 14.4% -> 7.6%, Essos 11.3% -> 3.4% przy tych samych zakupach)
            if (!b.Alt)
            {
                b.KcalW += n * StartCalibration.ZKg(z) * KsBasket.Kcal[z];
                b.ZlW += (double)n * (price > 0 ? price : 0);
            }
            if (price <= 0 || have <= 0) { b.P2Shelf += n; return 0; }
            // recenzja runda 3 (drobne): drabine 2 racjonuje CENA - wies z oszczednosciami kupuje ponad plan, dopoki cena polki dzis jest najwyzej 0.8 x ceny startu (krainy)
            // i najwyzej do polki, przy ktorej cena doszlaby do 0.8 x startu (dni polki przy indeksie 0.8 x startu x popyt zaplacony - ok. 1.45 x cel); bez stalego pasa dla kupcow
            if (price > StartCalibration.ZPass2Price(b.T, z)) { b.P2Shelf += n; return 0; }
            int above = (int)Math.Floor(have + ShelfKeep - StartCalibration.ZPass2Floor(b.T, z));   // have = polka - 5
            int k = Math.Min(n, Math.Min(have, above));
            if (k <= 0) { b.P2Shelf += n; return 0; }
            if (k < n) b.P2Shelf += n - k;
            long room = (long)b.V.Gold - Math.Max(0, b.Lim);
            if (room < price) { b.P2Gold += k; return 0; }
            if ((long)k * price > room) { b.P2Gold += k - (int)(room / price); k = (int)(room / price); if (k <= 0) return 0; }
            int had = b.V.Gold;
            b.V.ChangeGold(-k * price);
            int paid = had - b.V.Gold;
            if (paid <= 0) return 0;
            b.T.ChangeGold(paid);
            b.R.AddToCounts(e, -k);
            b.Spent += paid; b.Pcs += k; b.P2Got += k;
            b.KcalG += k * StartCalibration.ZKg(z) * KsBasket.Kcal[z]; b.ZlG += paid;
            b.D.VBuy += paid; b.D.VPieces += k;
            NoteVillBought(b.Mk, e.Item.ItemCategory, paid);
            StartCalibration.KsTakeP2(b.Mk, e.Item, k);           // KS-R3 runda 3: poza odbiorem 7 dob zamowienia KS-H (ponad plan)
            return k;
        }

        /// <summary>ZC4: koszyk wsi vil (sztuki/d przy pelnej lambdzie, ZIds) x lam kupiony w miescie targowym mk - drabina zakupow, kiesa wsi -> kasa miasta. Zwraca zloto.
        /// KS-R3 (H1): days - za ile dob (zakupy co 2. doba: lam juz x 2).</summary>
        private static long Zc4BuyVillage(Village vil, Settlement mk, float[] q, double lam, LDay d, int days = 1)
        {
            var town = mk.Town; var roster = mk.ItemRoster;
            if (town == null || roster == null || q == null) return 0;
            float[] carry;
            if (!_zcCarry.TryGetValue(vil, out carry) || carry == null || carry.Length != StartCalibration.ZN) { carry = new float[StartCalibration.ZN]; _zcCarry[vil] = carry; }
            var b = new ZcBuy { V = vil, Mk = mk, T = town, R = roster, D = d };
            long spent;
            var gf = GoodsLedger.Begin(GoodsLedger.FFolkVill, mk);   // F0: ksiega towarow na rynek miasta targowego (dotad ramka bez osady - "bez rynku")
            try { spent = Zc4BuyCore(vil, mk, q, lam, d, b, carry, Math.Max(1, days)); }
            finally { GoodsLedger.End(gf); }
            return spent;
        }

        /// <summary>
        /// KS-R3 G2 (klucz Ks Village Paid First): DRABINA 1 - koszyk zaplacony z Planu (ziarno, sol; napoj najtanszy za litr; bialko najtansze za kcal; reszta), towary po
        /// ziarnie i soli tylko z kiesy ponad 7 dob zaplaconego ziarna i soli przy cenach doby; miasta obok (F2) tak samo; brak towaru i brak kiesy drabiny 1 - miary bledu;
        /// DRABINA 2 - reszta pelnego koszyka tylko z kiesy ponad 7 dob budzetu Planu (ceny startu) i tylko ze stanu polki ponad cel polki miasta (T* x popyt zaplacony),
        /// takze w miastach obok (INFO). Bez klucza (albo bez koszyka zaplaconego) - pelny koszyk jedna drabina, jak runda 3.
        /// G3 (klucz Ks Own Food First): kupione kcal mniej niz zaplacone z Planu - wies je z wlasnego magazynu (towar czekajacy na woz) do braku: ziarno, potem
        /// wlasne jedzenie wedlug kcal, najwyzej polowa kazdego towaru na dobe (ksiega towarow: ujscie "wies je wlasny towar (KS)"; bez zlota).
        /// </summary>
        private static long Zc4BuyCore(Village vil, Settlement mk, float[] q, double lam, LDay d, ZcBuy b, float[] carry, int days)
        {
            int NZq = StartCalibration.ZN;
            double budgetDay = 0;
            float[] qp = StartCalibration.SwPaidFirst ? StartCalibration.Zc4VillagePaid(vil, out budgetDay) : null;
            var n = new int[NZq]; int[] n2 = null;
            long resGS = 0;
            if (qp == null)
            {
                for (int z = 0; z < NZq; z++)
                {
                    double w = q[z] * lam + carry[z];
                    int k = (int)Math.Floor(w);
                    carry[z] = (float)Math.Max(0.0, Math.Min(1.0, w - k));
                    n[z] = Math.Max(0, k);
                }
            }
            else
            {
                float[] carry2;
                if (!_zcCarry2.TryGetValue(vil, out carry2) || carry2 == null || carry2.Length != NZq) { carry2 = new float[NZq]; _zcCarry2[vil] = carry2; }
                n2 = new int[NZq];
                for (int z = 0; z < NZq; z++)
                {
                    double p1 = z < qp.Length ? Math.Max(0f, Math.Min(q[z], qp[z])) : q[z];
                    double w = p1 * lam + carry[z];
                    int k = (int)Math.Floor(w);
                    carry[z] = (float)Math.Max(0.0, Math.Min(1.0, w - k));
                    n[z] = Math.Max(0, k);
                    double w2 = Math.Max(0.0, q[z] - p1) * lam + carry2[z];
                    int k2 = (int)Math.Floor(w2);
                    carry2[z] = (float)Math.Max(0.0, Math.Min(1.0, w2 - k2));
                    n2[z] = Math.Max(0, k2);
                }
                // zapas kiesy na ziarno i sol: 7 dob zaplaconego ziarna i soli przy cenach doby (towary po nich kupuje tylko kiesa ponad nim)
                int pg = StartCalibration.ZPrice(b.T, StartCalibration.ZGrain), ps = StartCalibration.ZPrice(b.T, StartCalibration.ZSalt);
                if (pg <= 0) { var it = StartCalibration.ZItem(StartCalibration.ZGrain); pg = it != null ? Math.Max(1, it.Value) : 1; }
                if (ps <= 0) { var it = StartCalibration.ZItem(StartCalibration.ZSalt); ps = it != null ? Math.Max(1, it.Value) : 1; }
                double perDay = lam / Math.Max(1, days);
                resGS = (long)Math.Round(7.0 * perDay * ((StartCalibration.ZGrain < qp.Length ? qp[StartCalibration.ZGrain] : 0f) * pg + (StartCalibration.ZSalt < qp.Length ? qp[StartCalibration.ZSalt] : 0f) * ps));
            }
            b.P1 = qp != null;
            ZcLadder(b, vil, mk, n, resGS);
            b.P1 = false;
            if (qp != null)
            {
                // drabina 1: bez towaru po miastach obok i bez kiesy - miary bledu (brak zaplaconego popytu do sterowania rynku P4)
                double miss = 0;
                int m = StartCalibration.MarketOfSettlement(mk);
                for (int z = 0; z < NZq; z++)
                {
                    if (b.Miss[z] <= 0) continue;
                    miss += b.Miss[z];
                    if (StartCalibration.ZFood(z) && StartCalibration.SwMarketControl) StartCalibration.KsUnmet(m, z, b.Miss[z]);
                }
                StartCalibration.Zc4NotePass1(b.Want, miss, b.UnGold, m);
                // drabina 2 (ponad plan)
                b.Pass2 = true; b.Lim = (long)Math.Round(7.0 * Math.Max(0.0, budgetDay)); b.Broke = false;
                var left = new int[NZq]; bool any = false;
                foreach (int z in StartCalibration.ZLadder)
                {
                    if (n2[z] <= 0) continue;
                    int got = ZcTake(b, z, n2[z], StartCalibration.ZFood(z) ? 1 : 2);
                    left[z] = n2[z] - got; if (left[z] > 0) any = true;
                }
                if (any)
                {
                    try
                    {
                        foreach (var alt in NearTowns(vil, mk))
                        {
                            if (alt.Town == null || alt.ItemRoster == null) continue;
                            var keepT = b.T; var keepR = b.R; var keepM = b.Mk;
                            b.T = alt.Town; b.R = alt.ItemRoster; b.Mk = alt; b.Alt = true;
                            var gf2 = GoodsLedger.Begin(GoodsLedger.FFolkVill, alt);
                            long s0 = b.Spent; bool rest = false;
                            try
                            {
                                foreach (int z in StartCalibration.ZLadder)
                                {
                                    if (left[z] <= 0) continue;
                                    int got = ZcTake(b, z, left[z], StartCalibration.ZFood(z) ? 1 : 2);
                                    if (got > 0) { left[z] -= got; b.Elsewhere += got; }
                                    if (left[z] > 0) rest = true;
                                }
                            }
                            finally
                            {
                                GoodsLedger.End(gf2); b.T = keepT; b.R = keepR; b.Mk = keepM; b.Alt = false;
                                long sp = b.Spent - s0;
                                if (sp > 0) { Folk ma, mm; if (_f.TryGetValue(alt, out ma)) ma.VillWin += sp; if (_f.TryGetValue(mk, out mm)) mm.VillWin -= sp; }
                            }
                            if (!rest) break;
                        }
                    }
                    catch (Exception e) { Stumble("Zc4BuyVillage(drabina 2, inne miasto)", e); }
                }
                b.Pass2 = false;
                StartCalibration.Zc4NotePass2(b.P2Got, b.P2Shelf, b.P2Gold);
            }
            StartCalibration.Zc4NoteVillage(b.Spent, b.Pcs, b.Want, b.Got, b.NoGold, b.NoGoods, b.Rw, b.Rg, _zcBrewV, b.UnGold);
            StartCalibration.Zc4NoteTown(mk, b.Want, b.Got, b.UnGold, Math.Max(0, b.NoGoodsPcs - b.Elsewhere), b.Elsewhere);
            StartCalibration.Zc4NoteBought(vil, mk, lam, b.KcalW, b.KcalG, b.ZlW, b.ZlG);
            StartCalibration.KsBoughtKcal(vil, b.KcalG, days);
            _zcBrewV = 0;
            // G3: wies je swoje, gdy kupila mniej kcal niz zaplacone z Planu
            if (StartCalibration.SwOwnFirst && qp != null)
            {
                try
                {
                    double paidK = 0;
                    for (int z = 0; z < NZq && z < qp.Length; z++) if (qp[z] > 0 && KsBasket.Kcal[z] > 0) paidK += Math.Min(q[z], qp[z]) * lam * StartCalibration.ZKg(z) * KsBasket.Kcal[z];
                    double shortK = paidK - b.KcalG, kr = 0;
                    if (shortK > 0) kr = OwnFirst(vil, shortK, b.KcalW);
                    StartCalibration.KsOwnDay(vil, kr, days);
                }
                catch (Exception e) { Stumble("Zc4BuyVillage(wies je swoje)", e); }
            }
            return b.Spent;
        }

        private static readonly Dictionary<Village, float[]> _zcCarry2 = new Dictionary<Village, float[]>();
        private static readonly int[] OwnOrder = { StartCalibration.ZGrain, StartCalibration.ZFish, StartCalibration.ZMeat, StartCalibration.ZCheese, StartCalibration.ZButter, StartCalibration.ZEgg,
                                                   StartCalibration.ZDate, StartCalibration.ZOlives, StartCalibration.ZGrape, StartCalibration.ZApple, StartCalibration.ZCarrot, StartCalibration.ZOrange, StartCalibration.ZHoney };

        /// <summary>G3: wies je z magazynu (towar czekajacy na woz) do braku kcal: ziarno, potem wlasne jedzenie wedlug kcal, najwyzej polowa kazdego towaru.</summary>
        private static double OwnFirst(Village vil, double shortK, double basketK)
        {
            var st = vil.Settlement; var ro = st != null ? st.ItemRoster : null;
            if (ro == null || !(shortK > 0)) return 0;
            double kr = 0; long pcs = 0;
            var gf = GoodsLedger.Begin(GoodsLedger.FKsOwn, vil);
            try
            {
                foreach (int z in OwnOrder)
                {
                    if (shortK <= 0) break;
                    var it = StartCalibration.ZItem(z); if (it == null) continue;
                    double kc = StartCalibration.ZKg(z) * KsBasket.Kcal[z]; if (!(kc > 0)) continue;
                    int have = ro.GetItemNumber(it);
                    int cap = have / 2;
                    if (cap <= 0) continue;
                    int k = (int)Math.Min(cap, Math.Ceiling(shortK / kc));
                    if (k <= 0) continue;
                    ro.AddToCounts(it, -k);
                    int took = have - ro.GetItemNumber(it);
                    if (took <= 0) continue;
                    kr += took * kc; pcs += took; shortK -= took * kc;
                }
            }
            finally { GoodsLedger.End(gf); }
            if (kr > 0) StartCalibration.KsOwnEaten(vil, kr, pcs, basketK);
            return kr;
        }

        /// <summary>Drabina zakupow wsi (jedna przejscie): ziarno i sol, napoj (najtanszy za litr; brak wina - piwo, brak piwa - ziarno na piwo domowe), bialko (najtansze
        /// za kcal), reszta drabiny; brak na polce - miasta obok (F2). lim - zapas kiesy dla towarow po ziarnie i soli (KS-R3 G2; 0 - bez zapasu).</summary>
        private static void ZcLadder(ZcBuy b, Village vil, Settlement mk, int[] n, long lim)
        {
            var done = new bool[StartCalibration.ZN];
            // (1) przetrwanie: ziarno, sol
            b.Lim = 0;
            ZcTake(b, StartCalibration.ZGrain, n[StartCalibration.ZGrain], 0); done[StartCalibration.ZGrain] = true;
            ZcTake(b, StartCalibration.ZSalt, n[StartCalibration.ZSalt], 0); done[StartCalibration.ZSalt] = true;
            b.Lim = lim;
            // (2) napoj - najtanszy za litr najpierw; brak wina -> piwo, brak piwa -> ziarno na piwo domowe (0.4 kg na litr)
            var dr = new List<int>(ZcDrinks);
            var unit = new double[StartCalibration.ZN];
            foreach (int z in dr) { EquipmentElement e; int h; int p = ZcPrice(b, z, out e, out h); unit[z] = p > 0 && h > 0 ? p / StartCalibration.ZKg(z) : double.MaxValue; }
            dr.Sort((x, y) => unit[x].CompareTo(unit[y]));
            var un = new int[StartCalibration.ZN];
            foreach (int z in dr) { un[z] = n[z] - ZcTake(b, z, n[z], 1); done[z] = true; }
            // recenzja 17.10 (wazne): napoj zastepczy zmniejsza brak do szukania w miastach obok (dotad F2 kupowalo tam jeszcze raz to samo piwo i wino - wies placila
            // dwa razy za jedna potrzebe napoju)
            if (un[StartCalibration.ZWine] > 0)
            {
                int bw = ZcTake(b, StartCalibration.ZBeer, un[StartCalibration.ZWine], 1, false);
                un[StartCalibration.ZBeer] += un[StartCalibration.ZWine] - bw;
                MissCovered(b, StartCalibration.ZWine, bw);
                un[StartCalibration.ZWine] = 0;
            }
            if (un[StartCalibration.ZBeer] > 0)
            {
                double gk = StartCalibration.ZKg(StartCalibration.ZGrain), bk = StartCalibration.ZKg(StartCalibration.ZBeer);
                int g = (int)Math.Floor(un[StartCalibration.ZBeer] * bk * KsTab.BeerGrain / gk);
                if (g > 0)
                {
                    int gb = ZcTake(b, StartCalibration.ZGrain, g, 1, false); _zcBrewV += gb;
                    // litry piwa domowego z kupionego ziarna - najpierw brak piwa, potem reszta braku wina
                    int brew = (int)Math.Floor(gb * gk / (KsTab.BeerGrain * bk) + 1e-9);
                    brew -= MissCovered(b, StartCalibration.ZBeer, brew);
                    if (brew > 0) MissCovered(b, StartCalibration.ZWine, brew);
                }
            }
            // (3) bialko - najtansze za 1000 kcal najpierw
            var pr = new List<int>(ZcProtein);
            foreach (int z in pr) { EquipmentElement e; int h; int p = ZcPrice(b, z, out e, out h); unit[z] = p > 0 && h > 0 ? p / StartCalibration.ZKg(z) / (ZcKcal[z] / 1000.0) : double.MaxValue; }
            pr.Sort((x, y) => unit[x].CompareTo(unit[y]));
            foreach (int z in pr) { ZcTake(b, z, n[z], 1); done[z] = true; }
            // (4) reszta po kolei drabiny
            foreach (int z in StartCalibration.ZLadder)
            {
                if (done[z]) continue;
                done[z] = true;
                ZcTake(b, z, n[z], StartCalibration.ZFood(z) ? 1 : 2);
            }
            // F2 (poprawka po autotescie 16.10): czego brak na polce miasta targowego - w najblizszych miastach (najwyzej 2, do 250 jedn., nie wrogich), kiesa wsi -> kasa tamtego miasta
            if (!b.Broke && b.NoGoodsPcs > 0)
            {
                try
                {
                    foreach (var alt in NearTowns(vil, mk))
                    {
                        if (b.Broke) break;
                        bool any = false; for (int z = 0; z < StartCalibration.ZN; z++) if (b.Miss[z] > 0) { any = true; break; }
                        if (!any) break;
                        if (alt.Town == null || alt.ItemRoster == null) continue;
                        var keepT = b.T; var keepR = b.R; var keepM = b.Mk;
                        b.T = alt.Town; b.R = alt.ItemRoster; b.Mk = alt;
                        var gf2 = GoodsLedger.Begin(GoodsLedger.FFolkVill, alt);
                        long s0 = b.Spent;
                        try
                        {
                            foreach (int z in StartCalibration.ZLadder)
                            {
                                if (b.Miss[z] <= 0) continue;
                                b.Lim = z == StartCalibration.ZGrain || z == StartCalibration.ZSalt ? 0 : lim;
                                int got = ZcTake(b, z, b.Miss[z], StartCalibration.ZFood(z) ? 1 : 2, false);
                                if (got > 0) { b.Miss[z] -= got; b.Elsewhere += got; }
                                if (b.Broke) break;
                            }
                        }
                        finally
                        {
                            GoodsLedger.End(gf2); b.T = keepT; b.R = keepR; b.Mk = keepM;
                            // podstawa robocizny targu (VillWin): zakupy w tamtym miescie jemu, nie miastu targowemu (wolajacy dopisze calosc miastu targowemu)
                            long sp = b.Spent - s0;
                            if (sp > 0) { Folk ma, mm; if (_f.TryGetValue(alt, out ma)) ma.VillWin += sp; if (_f.TryGetValue(mk, out mm)) mm.VillWin -= sp; }
                        }
                    }
                }
                catch (Exception e) { Stumble("Zc4BuyVillage(inne miasto)", e); }
            }
            b.Lim = 0;
        }

        /// <summary>Brak towaru z na polce pokryty zakupem zastepczym (n sztuk): mniej do szukania w miastach obok i mniej "bez towaru" w liczbach. Zwraca pokryte.</summary>
        private static int MissCovered(ZcBuy b, int z, int n)
        {
            if (n <= 0 || b.Miss[z] <= 0) return 0;
            int c = Math.Min(n, b.Miss[z]);
            b.Miss[z] -= c; b.NoGoodsPcs = Math.Max(0, b.NoGoodsPcs - c);
            return c;
        }

        private static readonly Dictionary<Village, Settlement[]> _nearTowns = new Dictionary<Village, Settlement[]>();
        private static int _nearDay = -1;

        /// <summary>F2: najwyzej 2 miasta poza targowym w zasiegu 250 jedn. mapy od wsi, nie wrogie miastu targowemu dzis. Recenzja 17.10 (drobne): droga ladowa gry
        /// (MapDistanceModel osada -> osada, NavigationType.Default - nie przez morze i rzeki bez brodu; do 375 jedn. drogi przy 250 w linii prostej), pamiec zerowana
        /// przy nowej sesji (TownFolk.Reset) i co 7 dob (zmiany granic i miast targowych).</summary>
        private static IEnumerable<Settlement> NearTowns(Village vil, Settlement mk)
        {
            int today = (int)TaleWorlds.CampaignSystem.CampaignTime.Now.ToDays;
            if (_nearDay < 0 || today - _nearDay >= 7 || today < _nearDay) { _nearTowns.Clear(); _nearDay = today; }
            Settlement[] l;
            if (!_nearTowns.TryGetValue(vil, out l))
            {
                var all = new List<KeyValuePair<float, Settlement>>();
                var pos = vil.Settlement.GetPosition2D;
                TaleWorlds.CampaignSystem.ComponentInterfaces.MapDistanceModel dm = null;
                try { dm = TaleWorlds.CampaignSystem.Campaign.Current.Models.MapDistanceModel; } catch { dm = null; }
                foreach (var t in Town.AllTowns)
                {
                    if (t == null || !t.IsTown || t.Settlement == null || ReferenceEquals(t.Settlement, mk)) continue;
                    float dd = pos.Distance(t.Settlement.GetPosition2D);
                    if (dd > 250f) continue;
                    if (dm != null)
                    {
                        float rd = -1f;
                        try { rd = dm.GetDistance(vil.Settlement, t.Settlement, false, false, TaleWorlds.CampaignSystem.Party.MobileParty.NavigationType.Default); } catch { rd = -1f; }
                        if (!(rd >= 0f) || rd > 375f) continue;          // bez drogi ladowej (morze, rzeka bez brodu) albo za daleko droga
                        dd = rd;
                    }
                    all.Add(new KeyValuePair<float, Settlement>(dd, t.Settlement));
                }
                all.Sort((a, c) => a.Key.CompareTo(c.Key));
                l = new Settlement[Math.Min(2, all.Count)];
                for (int i = 0; i < l.Length; i++) l[i] = all[i].Value;
                _nearTowns[vil] = l;
            }
            foreach (var s in l)
            {
                if (s == null || ReferenceEquals(s, mk)) continue;            // miasto targowe wsi zmienilo sie od zbudowania pamieci
                bool war = false;
                try { war = mk != null && mk.MapFaction != null && s.MapFaction != null && mk.MapFaction.IsAtWarWith(s.MapFaction); } catch { war = false; }
                if (!war && !s.IsUnderSiege) yield return s;
            }
        }

        private static uint KsFnv(string s) { uint h = 2166136261; if (s != null) unchecked { foreach (char c in s) h = (h ^ c) * 16777619; } return h; }

        /// <summary>Nowa sesja (TownFolk.Reset): pamiec miast obok i ulamki koszyka wsi od nowa.</summary>
        private static void KsReset() { _nearTowns.Clear(); _nearDay = -1; _zcCarry.Clear(); _zcCarry2.Clear(); }
        private static long _zcBrewV;
    }
}
