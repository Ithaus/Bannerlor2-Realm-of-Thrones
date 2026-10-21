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
            // ks-koszt T5 (klucz Ks Cost Fast): mapa koszyka miasta T rozwiazana raz na wywolanie (null - miasto poza ZC4) - ZRow / ZRowSet / ZPrice bez szukania miasta;
            // T4: progi drabiny 2 z pamieci doby
            public bool Fast; public StartCalibration.ZTown Z; public Town P2T; public double[] P2Row;
            // m103 wsie (klucz Ks Village Basket At Shelf): budzet drabiny 1 z biezacego wplywu (Cap, zl - zostalo), ziarno i sol poza budzetem (NoCap; ich koszt idzie z budzetu),
            // miasta obok w drabinie 1 (InF2 - przyciete schodzi z braku towaru), przyciete budzetem (szt., zl), zaplacone za ziarno i sol (zl)
            public bool CapOn, NoCap, InF2; public double Cap, CapPcs, CapZl, GsPaid;
            // recenzja W1: przyciete rzeczy (szczebel 2 - przedza, len, filc, skora, narzedzia, garnki, tran, futra; szt.), narzedzia drabiny 1 kupione i przyciete (szt.)
            public double CapPcs2, ToolsGot, ToolsCut;
            // m105 gosp4 (czesc 4, segment linii ZC4): kcal i zl jedzenia drabiny 1 po ziarnie i soli (G1 - kcal na 1 zl)
            public double K1Kcal, K1Zl;
            // m105 gosp4 G2 (Ks Village Grain Spike From Surplus): wies DS ponad skrzynia - skok ceny ziarna i soli ponad cene Planu z nadwyzki (zostalo w tempie kroku 5, zaplacone)
            public bool Spike; public double SpikeRoom, SpikePaid;
            // m105 gosp4 G3 (Ks Village Own Grain First): ziarno i sol ponad reszte budzetu najpierw z magazynu wsi - sztuki wedlug pozycji (polowa towaru liczona razem z G3 po
            // drabinie), kcal, sztuki i wartosc przy cenie polki (linia)
            public bool OwnGs; public int[] OwnEarly; public double OwnK, OwnPcs, OwnZl;
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
            if (b.Fast) return ZcPriceFast(b, z, it, e, rc, out have);
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

        /// <summary>ks-koszt T5 (klucz Ks Cost Fast): ZcPrice z mapa miasta z wywolania (b.Z - to samo, co ZTownOf w ZRow, ZRowSet i ZPrice) i jedna kopia wiersza polki
        /// zamiast dwoch - te same decyzje i liczby co ZcPrice wyzej.</summary>
        private static int ZcPriceFast(ZcBuy b, int z, ItemObject it, EquipmentElement e, int rc, out int have)
        {
            have = 0;
            var zt = b.Z;
            // ZRow: poza mapa -2; -1 tylko przy tej samej liczbie wierszy co przy sprawdzeniu braku
            int ix = zt == null ? -2 : zt.Ix[z];
            if (ix == -1 && zt.IxCnt[z] != rc) ix = -2;
            if (ix == -1 && StartCalibration.SwBuyEvery2) return 0;
            bool bad = ix < 0 || ix >= rc;
            if (!bad) { var el = b.R.GetElementCopyAtIndex(ix); bad = !ReferenceEquals(el.EquipmentElement.Item, it) || el.EquipmentElement.ItemModifier != null; }
            if (bad)
            {
                ix = b.R.FindIndexOfElement(e);
                if (zt != null) { zt.Ix[z] = ix; zt.IxCnt[z] = rc; }       // ZRowSet
            }
            if (ix < 0) return 0;
            int keep = ShelfKeep;
            if (z == StartCalibration.ZLinen && VillClothOn) { int r = MaterialOrders.ShelfReserve(b.T, it); if (r > keep) keep = r; }
            have = b.R.GetElementNumber(ix) - keep;
            int p = zt != null ? zt.Px[z] : 0;                               // ZPrice
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
            int pw = price; if (pw <= 0) { var iw = StartCalibration.ZItem(z); pw = iw != null ? Math.Max(1, iw.Value) : 0; }
            if (count)
            {
                b.Want += n; b.Rw[rung] += n;
                b.KcalW += n * StartCalibration.ZKg(z) * KsBasket.Kcal[z];
                b.ZlW += (double)n * pw;
            }
            // m103 wsie (Ks Village Basket At Shelf): drabina 1 kupuje towary po ziarnie i soli najwyzej za to, co zostalo z budzetu z biezacego wplywu (Zc4BuyCore) - reszta
            // czeka (to nie brak kiesy ani towaru: wies tego dzis nie kupi). Recenzja W1 (niska): budzet przed brakiem towaru i kiesy - brak towaru (Miss: miasta obok F2,
            // sterowanie rynku KsUnmet) i brak kiesy licza sie tylko do sztuk, na ktore starczyloby reszty budzetu przy cenie polki (bez ceny - wartosc towaru, jak ZlW);
            // reszta to przyciete - ta sama potrzeba liczona tak samo bez wzgledu na polke miasta obok. Bez budzetu (klucz NIE, ziarno i sol) allow = n - jak 101.
            bool cap = b.CapOn && !b.NoCap && pw > 0;
            int allow = cap ? (int)Math.Min(n, Math.Floor(Math.Max(0.0, b.Cap) / pw)) : n;
            if (price <= 0 || have <= 0)
            {
                b.NoGoods++;
                if (count) { b.NoGoodsPcs += allow; b.Miss[z] += allow; }
                ZcCut(b, z, n - allow, pw, rung, count);
                return 0;
            }
            int hv = Math.Min(n, have);                                              // na polce
            int mi = Math.Min(n - hv, Math.Max(0, allow - hv));                      // bez towaru w granicy budzetu (bez budzetu: n - hv)
            if (b.Broke)
            {
                int ug = Math.Min(hv, allow);
                if (count) { b.UnGold += ug; if (mi > 0) { b.NoGoodsPcs += mi; b.Miss[z] += mi; } }
                ZcCut(b, z, n - ug - mi, pw, rung, count);
                return 0;
            }
            if (count && mi > 0) { b.NoGoodsPcs += mi; b.Miss[z] += mi; }
            ZcCut(b, z, n - hv - mi, pw, rung, count);                               // brak towaru ponad budzet
            int k = hv;
            if (cap)
            {
                double cr = Math.Max(0.0, b.Cap);
                int kc = cr >= price ? (int)Math.Min(k, Math.Floor(cr / price)) : 0;
                if (kc < k)
                {
                    ZcCut(b, z, k - kc, price, rung, count);
                    k = kc;
                    if (k <= 0) return 0;
                }
            }
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
            if (b.CapOn)
            {
                // m103 wsie: ziarno i sol tez z budzetu (ponad budzet - z oszczednosci). m105 gosp4 G2: ziarno i sol obciazaja budzet najwyzej po cenie Planu - roznice placi
                // nadwyzka ponad skrzynie (SpikeRoom); bez nadwyzki (albo klucz NIE) - jak dotad
                double fromS = b.NoCap && b.Spike && b.SpikeRoom >= 0.5 ? SpikeFrom(b, z, k, paid) : 0.0;
                b.Cap -= paid - fromS; if (b.NoCap) b.GsPaid += paid - fromS;
            }
            if (b.CapOn && z == StartCalibration.ZTools && (count || b.InF2)) b.ToolsGot += k;   // recenzja W1: narzedzia drabiny 1 (segment linii ZC4)
            if (b.CapOn && b.P1 && !b.NoCap && KsBasket.Kcal[z] > 0) { b.K1Kcal += k * StartCalibration.ZKg(z) * KsBasket.Kcal[z]; b.K1Zl += paid; }   // m105 gosp4: jedzenie po ziarnie i soli (G1, linia)
            b.KcalG += k * StartCalibration.ZKg(z) * KsBasket.Kcal[z]; b.ZlG += paid;
            b.D.VBuy += paid; b.D.VPieces += k;
            if (b.P1) StartCalibration.Zc4NotePass1Zl(b.Mk, paid);        // recenzja runda 3: wartosc kupiona drabiny 1 (NKS-13)
            NoteVillBought(b.Mk, e.Item.ItemCategory, paid);
            return k;
        }

        /// <summary>m105 gosp4 G2 (Ks Village Grain Spike From Surplus): z k sztuk ziarna albo soli zaplaconych `paid` - czesc ponad cene Planu (StartCalibration.ZPlanPrice miasta
        /// zakupu) placi nadwyzka ponad skrzynie, najwyzej to, co z niej zostalo na ten zakup (SpikeRoom). Zwraca zl z nadwyzki (budzet z wplywu obciaza reszta). Nie rzuca.</summary>
        private static double SpikeFrom(ZcBuy b, int z, int k, int paid)
        {
            try
            {
                double pp = StartCalibration.ZPlanPrice(b.T, z);
                if (!(pp > 0) || k <= 0) return 0.0;
                double over = paid - k * pp;
                if (!(over > 0)) return 0.0;
                double t = Math.Min(b.SpikeRoom, over);
                b.SpikeRoom -= t; b.SpikePaid += t;
                return t;
            }
            catch (Exception e) { Stumble("Zc4BuyVillage(skok ceny z nadwyzki)", e); return 0.0; }
        }

        /// <summary>m105 gosp4 G2: nadwyzka wsi DS na skok ceny ziarna i soli w tym zakupie - w tempie kroku 5: lam x rampa DS x (kiesa - skrzynia) / DsSurplusSpendDays (lam =
        /// lambda x doby zakupu, jak budzet W1). Wies nie DS, na skrzyni albo ponizej, rampa 0, DsSurplusSpendDays 0 - bez nadwyzki (placi jak dotad). Nie rzuca.</summary>
        private static void SpikeRoomOf(ZcBuy b, Village vil, double lam)
        {
            b.Spike = false; b.SpikeRoom = 0; b.SpikePaid = 0;
            try
            {
                var st = vil != null ? vil.Settlement : null;
                if (st == null || !DsVillage.On(st)) return;
                int sd = DsVillage.SpendDaysNow; double ramp = DsVillage.Ramp;
                if (sd <= 0 || !(ramp > 0) || !(lam > 0)) return;
                double over = vil.Gold - DsVillage.Chest(vil);
                if (!(over >= 1)) return;
                double r = lam * ramp * over / sd;
                if (!(r >= 0.5) || double.IsInfinity(r)) return;
                b.Spike = true; b.SpikeRoom = r;
            }
            catch (Exception e) { Stumble("Zc4BuyVillage(nadwyzka na skok ceny)", e); b.Spike = false; b.SpikeRoom = 0; }
        }

        /// <summary>m103 wsie (Ks Village Basket At Shelf): c sztuk towaru z przycietych budzetem z biezacego wplywu (cena pw za sztuke) - liczone tylko przy pozycji koszyka
        /// (count) i w miastach obok (zakup zastepczy - piwo za wino, ziarno na piwo - bez drugiego liczenia); w miastach obok przyciete schodzi z braku towaru (nie liczy sie
        /// drugi raz ani jako popyt bez towaru). Recenzja W1: osobno rzeczy (szczebel 2) i narzedzia.</summary>
        private static void ZcCut(ZcBuy b, int z, int c, int pw, int rung, bool count)
        {
            if (c <= 0) return;
            if (count || b.InF2)
            {
                b.CapPcs += c; b.CapZl += (double)c * pw;
                if (rung == 2) b.CapPcs2 += c;
                if (z == StartCalibration.ZTools) b.ToolsCut += c;
            }
            if (b.InF2) { int mc = Math.Min(c, b.Miss[z]); b.Miss[z] -= mc; b.NoGoodsPcs = Math.Max(0, b.NoGoodsPcs - mc); }
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
            if (b.Fast && !ReferenceEquals(b.P2T, b.T)) { b.P2T = b.T; b.P2Row = StartCalibration.T4P2Row(b.T); }   // ks-koszt T4: progi miasta z pamieci doby
            if (price > (b.Fast ? StartCalibration.ZPass2PriceT4(b.T, z, b.P2Row) : StartCalibration.ZPass2Price(b.T, z))) { b.P2Shelf += n; return 0; }
            int above = (int)Math.Floor(have + ShelfKeep - (b.Fast ? StartCalibration.ZPass2FloorT4(b.T, z, b.P2Row) : StartCalibration.ZPass2Floor(b.T, z)));   // have = polka - 5
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
            if (StartCalibration.SwCostFast) { b.Fast = true; b.Z = StartCalibration.ZTownFor(town); StartCalibration.T4Begin(); }   // ks-koszt T4/T5
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
            // m103 wsie (klucz Ks Village Basket At Shelf): budzet drabiny 1 z biezacego wplywu (ShelfBudget) - tylko drabina 1 koszyka zaplaconego z Planu
            double shC = 0, shB = 0, shPlan = 0, shNet = -1;
            if (qp != null && StartCalibration.SwBasketAtShelf && ShelfBudget(b, vil, n, lam, budgetDay, out shC, out shB, out shPlan, out shNet))
            { b.CapOn = true; b.Cap = shB; b.CapPcs = b.CapZl = b.GsPaid = b.CapPcs2 = b.ToolsGot = b.ToolsCut = 0; b.K1Kcal = b.K1Zl = 0; }
            if (b.CapOn && StartCalibration.SwGrainSpike) SpikeRoomOf(b, vil, lam);     // m105 gosp4 G2: kiesa przed zakupem (jak skrzynia kroku 5)
            b.OwnGs = b.CapOn && StartCalibration.SwOwnGrainFirst && StartCalibration.SwOwnFirst; b.OwnEarly = null; b.OwnK = b.OwnPcs = b.OwnZl = 0;   // m105 gosp4 G3
            try { ZcLadder(b, vil, mk, n, resGS); }
            finally
            {
                // recenzja W1: narzedzia drabiny 1 - chciane, kupione (z miastami obok), przyciete, bez towaru po miastach obok (w granicy budzetu)
                int zt = StartCalibration.ZTools;
                if (b.CapOn)
                {
                    StartCalibration.Zc4NoteShelf(mk, shC, shB, shPlan, shNet, b.CapZl, b.CapPcs, Math.Max(0.0, b.GsPaid - shB),
                                                  b.CapPcs2, zt < n.Length ? n[zt] : 0, b.ToolsGot, b.ToolsCut, zt < b.Miss.Length ? b.Miss[zt] : 0);
                    StartCalibration.Zc4NoteM105(mk, b.K1Kcal, b.K1Zl, b.SpikePaid, b.OwnPcs, b.OwnZl, -1);   // m105 gosp4: segment linii ZC4
                    if (b.SpikePaid > 0) DsVillage.SpikeNote(vil.Settlement, b.SpikePaid);     // m105 gosp4 G2: krok 5 tej i nastepnych dob mniej o to samo
                }
                b.CapOn = false; b.NoCap = false; b.InF2 = false; b.Spike = false; b.SpikeRoom = 0;
            }
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
                // KS runda 4 (fin78: kiesy wsi -0.37%/d, mediana -30%): wies DS kupuje ponad koszyk zaplacony tylko z kiesy ponad SKRZYNIE na czarna godzine
                // (DsVillage.Chest: 21 dob koszyka i czynszu) - jak sredniowieczny chlop: najpierw zapas, potem lepszy stol
                if (vil.Settlement != null && DsVillage.On(vil.Settlement)) b.Lim = Math.Max(b.Lim, (long)Math.Ceiling(Math.Max(0.0, DsVillage.Chest(vil))));
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
                            var keepT = b.T; var keepR = b.R; var keepM = b.Mk; var keepZ = b.Z;
                            b.T = alt.Town; b.R = alt.ItemRoster; b.Mk = alt; b.Alt = true;
                            if (b.Fast) b.Z = StartCalibration.ZTownFor(b.T);      // ks-koszt T5
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
                                GoodsLedger.End(gf2); b.T = keepT; b.R = keepR; b.Mk = keepM; b.Alt = false; b.Z = keepZ;
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
                    // m105 gosp4 G3: brak kcal juz bez zjedzonego z magazynu przed zakupem ziarna i soli (OwnK; klucz NIE - 0, jak dotad)
                    double shortK = paidK - b.KcalG - b.OwnK, kr = 0; long krPcs = 0;
                    int[] own = shortK > 0 ? new int[NZq] : null;   // f14-rynek (recenzja): sztuki zjedzone swoje wedlug pozycji koszyka
                    if (shortK > 0) kr = OwnFirst(vil, shortK, own, b.OwnEarly, out krPcs);
                    if (kr + b.OwnK > 0) StartCalibration.KsOwnEaten(vil, kr + b.OwnK, krPcs + (long)b.OwnPcs, b.KcalW);   // racje spichlerza i linia "KS: wies je swoje"
                    StartCalibration.KsOwnDay(vil, kr + b.OwnK, days);
                    // f14-rynek (recenzja, Ks Ctl Own Food): brak drabiny 1 pokryty swoim tym samym towarem - sterowanie rynku nie liczy go drugi raz jako brak
                    StartCalibration.KsOwnCovered(vil, mk, b.Miss, own, days);
                }
                catch (Exception e) { Stumble("Zc4BuyVillage(wies je swoje)", e); }
            }
            return b.Spent;
        }

        /// <summary>
        /// m103 wsie (klucz Ks Village Basket At Shelf; diagnoza P2 Armoury-2026-10-21_10-08-39.log): Plan wycenia koszyk zaplacony wsi przy cenach startu (przyciete 0.7-1.5,
        /// x gradient krainy - StartCalibration.Core krok 8), a wies placi cene polki: ten sam koszyk kosztowal 1.32-1.68 x budzet Planu, po rampie wsie wydaly 22.54 mln
        /// przy budzecie 17.25 mln, kiesy wsi 8.47 -> 3.91 mln w 193 dobach KS (bez KS 8.58 -> 8.12), roznica do kas miast. REGULA: wies nie wydaje na drabine 1 wiecej,
        /// niz biezaco zarabia - budzet tego zakupu = lam x min(budzet Planu na dobe, wplyw netto na dobe) (wplyw netto: (1 - zapas) x (do kiesy + fracht KS-H - wydatki
        /// spoza koszyka), srednie 28 dob - ta sama miara co alfa netto KS-R3 P2; bez pomiaru - budzet Planu). Ziarno i sol zawsze w calosci (glod; ich koszt idzie
        /// z budzetu, ponad budzet - z oszczednosci, jak dotad do pustej kiesy); reszta po kolei drabiny (projekt ZC 8.4: napoj, bialko najtansze za kcal, reszta, miasta obok)
        /// do konca budzetu - jak przy pustej kiesie dalsze szczeble czekaja (ZcTake). Przyciete sztuki nie przechodza na jutro (to nie zaleglosc - pieniadz zostaje w kiesie);
        /// sa w chcianych drabiny 1 i pelnego koszyka (Want, KcalW, ZlW - bez pokrycia i racje pracy domowej licza pelny koszyk), osobno od braku kiesy i towaru. Wies je swoje
        /// (G3) dalej wobec kcal zaplaconych z Planu - brak kcal z ceny pokrywa ta sama regula co brak towaru. cost - koszt sztuk n przy cenach polki miasta targowego (linia).
        /// false - bez budzetu (wyjatek - licznik potkniec; drabina jak w kodzie 101). Nie rzuca.
        /// </summary>
        private static bool ShelfBudget(ZcBuy b, Village vil, int[] n, double lam, double budgetDay, out double cost, out double bud, out double plan, out double net)
        {
            cost = 0; bud = 0; plan = 0; net = -1;
            try
            {
                for (int z = 0; z < n.Length && z < StartCalibration.ZN; z++)
                {
                    if (n[z] <= 0) continue;
                    int p = b.Z != null ? b.Z.Px[z] : StartCalibration.ZPrice(b.T, z);   // cena polki miasta targowego z doby (ta sama co ZcPrice)
                    if (p <= 0) { var it = StartCalibration.ZItem(z); p = it != null ? Math.Max(1, it.Value) : 0; }
                    if (p > 0) cost += (double)n[z] * p;
                }
                plan = double.IsNaN(budgetDay) || double.IsInfinity(budgetDay) ? 0.0 : Math.Max(0.0, budgetDay) * lam;
                double nd;
                if (StartCalibration.Zc4VillageNetDay(vil, out nd)) { net = nd * lam; bud = Math.Min(plan, net); }
                else bud = plan;
                return true;
            }
            catch (Exception e) { Stumble("Zc4BuyVillage(budzet z wplywu)", e); return false; }
        }

        private static readonly Dictionary<Village, float[]> _zcCarry2 = new Dictionary<Village, float[]>();
        private static readonly int[] OwnOrder = { StartCalibration.ZGrain, StartCalibration.ZFish, StartCalibration.ZMeat, StartCalibration.ZCheese, StartCalibration.ZButter, StartCalibration.ZEgg,
                                                   StartCalibration.ZDate, StartCalibration.ZOlives, StartCalibration.ZGrape, StartCalibration.ZApple, StartCalibration.ZCarrot, StartCalibration.ZOrange, StartCalibration.ZHoney };

        /// <summary>G3: wies je z magazynu (towar czekajacy na woz) do braku kcal: ziarno, potem wlasne jedzenie wedlug kcal, najwyzej polowa kazdego towaru.
        /// own (moze byc null) - sztuki zjedzone wedlug pozycji koszyka (f14-rynek, sterowanie rynku). m105 gosp4 G3: early (moze byc null) - sztuki zjedzone juz przed zakupem
        /// ziarna i soli (ZcOwnGs) - polowa towaru liczona razem z nimi (najwyzej polowa na zakup, jak dotad). pcs - sztuki zjedzone tu. Licznik KsOwnEaten u wolajacego (raz na
        /// zakup, z zjedzonym przed zakupem).</summary>
        private static double OwnFirst(Village vil, double shortK, int[] own, int[] early, out long pcs)
        {
            pcs = 0;
            var st = vil.Settlement; var ro = st != null ? st.ItemRoster : null;
            if (ro == null || !(shortK > 0)) return 0;
            double kr = 0;
            var gf = GoodsLedger.Begin(GoodsLedger.FKsOwn, vil);
            try
            {
                foreach (int z in OwnOrder)
                {
                    if (shortK <= 0) break;
                    var it = StartCalibration.ZItem(z); if (it == null) continue;
                    double kc = StartCalibration.ZKg(z) * KsBasket.Kcal[z]; if (!(kc > 0)) continue;
                    int have = ro.GetItemNumber(it);
                    int e0 = early != null && z >= 0 && z < early.Length ? early[z] : 0;
                    int cap = (have + e0) / 2 - e0;
                    if (cap <= 0) continue;
                    int k = (int)Math.Min(cap, Math.Ceiling(shortK / kc));
                    if (k <= 0) continue;
                    ro.AddToCounts(it, -k);
                    int took = have - ro.GetItemNumber(it);
                    if (took <= 0) continue;
                    kr += took * kc; pcs += took; shortK -= took * kc;
                    if (own != null && z >= 0 && z < own.Length) own[z] += took;
                }
            }
            finally { GoodsLedger.End(gf); }
            return kr;
        }

        /// <summary>
        /// m105 gosp4 G3 (klucz Ks Village Own Grain First, z Ks Own Food First i budzetem W1; n32: ziarno i sol ponad budzet 6.4 tys. zl/d z oszczednosci, a wsie W i E
        /// produkujace zywnosc maja ziarno czekajace na woz): ziarno albo sol (z), na ktore nie starcza reszty budzetu z wplywu (przy G2 - z nadwyzka na skok ceny), wies bierze
        /// najpierw z wlasnego magazynu (towar czekajacy na woz; najwyzej polowa towaru na zakup - razem z G3 po drabinie), z oszczednosci kupuje tylko reszte. Zjedzone liczy
        /// sie w chcianych drabiny 1 (Want, kcal i wartosc chciane - jak kupione) i jako "z magazynu" (KsOwnEaten u wolajacego). Ksiega towarow: ramka FKsOwn (jak G3).
        /// Zwraca sztuki zjedzone (wolajacy kupuje n - zwrot). Nie rzuca.
        /// </summary>
        private static int ZcOwnGs(ZcBuy b, Village vil, int z, int n)
        {
            if (n <= 0 || vil == null) return 0;
            try
            {
                EquipmentElement e; int have;
                int price = ZcPrice(b, z, out e, out have);
                int pw = price; if (pw <= 0) { var iw = StartCalibration.ZItem(z); pw = iw != null ? Math.Max(1, iw.Value) : 0; }
                if (pw <= 0) return 0;
                int excess = n - GsAfford(b, z, pw);
                if (excess <= 0) return 0;
                if (b.OwnEarly == null) b.OwnEarly = new int[StartCalibration.ZN];
                int took = OwnTake(vil, z, excess, b.OwnEarly);
                if (took <= 0) return 0;
                double kc = took * StartCalibration.ZKg(z) * KsBasket.Kcal[z];
                b.Want += took; b.Rw[0] += took; b.KcalW += kc; b.ZlW += (double)took * pw;   // chciane drabiny 1 jak przy zakupie (ZcTake count)
                b.OwnK += kc; b.OwnPcs += took; b.OwnZl += (double)took * pw;
                return took;
            }
            catch (Exception ex) { Stumble("Zc4BuyVillage(swoje ziarno przed oszczednosciami)", ex); return 0; }
        }

        /// <summary>m105 gosp4 G3: ile sztuk ziarna albo soli (cena polki pw) pokryje reszta budzetu z wplywu - przy G2 (nadwyzka na skok ceny) budzet obciaza najwyzej cena Planu,
        /// dopoki starcza nadwyzki (ta sama regula co ZcTake / SpikeFrom).</summary>
        private static int GsAfford(ZcBuy b, int z, int pw)
        {
            double cap = Math.Max(0.0, b.Cap);
            double k = cap / pw;
            if (b.Spike && b.SpikeRoom >= 0.5)
            {
                double pp = StartCalibration.ZPlanPrice(b.T, z);
                if (pp > 0 && pp < pw)
                {
                    double kA = cap / pp, kB = b.SpikeRoom / (pw - pp);
                    k = kA <= kB ? kA : (cap + b.SpikeRoom) / pw;
                }
            }
            return k >= int.MaxValue ? int.MaxValue : (int)Math.Floor(Math.Max(0.0, k));
        }

        /// <summary>m105 gosp4 G3: zdejmuje z magazynu wsi najwyzej `want` sztuk towaru z (najwyzej polowa towaru razem z tym, co juz zjedzone w tym zakupie - early), ramka ksiegi
        /// towarow FKsOwn (ujscie "wies je wlasny towar (KS)", bez zlota). Zwraca zdjete sztuki (dopisuje do early).</summary>
        private static int OwnTake(Village vil, int z, int want, int[] early)
        {
            var st = vil.Settlement; var ro = st != null ? st.ItemRoster : null;
            var it = StartCalibration.ZItem(z);
            if (ro == null || it == null || want <= 0 || early == null || z < 0 || z >= early.Length) return 0;
            int have = ro.GetItemNumber(it);
            int cap = (have + early[z]) / 2 - early[z];
            int k = Math.Min(cap, want);
            if (k <= 0) return 0;
            int took;
            var gf = GoodsLedger.Begin(GoodsLedger.FKsOwn, vil);
            try { ro.AddToCounts(it, -k); took = have - ro.GetItemNumber(it); }
            finally { GoodsLedger.End(gf); }
            if (took <= 0) return 0;
            early[z] += took;
            return took;
        }

        /// <summary>Drabina zakupow wsi (jedna przejscie): ziarno i sol, napoj (najtanszy za litr; brak wina - piwo, brak piwa - ziarno na piwo domowe), bialko (najtansze
        /// za kcal), reszta drabiny; brak na polce - miasta obok (F2). lim - zapas kiesy dla towarow po ziarnie i soli (KS-R3 G2; 0 - bez zapasu).</summary>
        private static void ZcLadder(ZcBuy b, Village vil, Settlement mk, int[] n, long lim)
        {
            var done = new bool[StartCalibration.ZN];
            // (1) przetrwanie: ziarno, sol
            b.Lim = 0; b.NoCap = true;                                           // m103 wsie: ziarno i sol poza budzetem z wplywu (glod)
            // m105 gosp4 G3 (Ks Village Own Grain First): czesc, na ktora nie starcza reszty budzetu, najpierw z wlasnego magazynu (klucz NIE - OwnGs falsz, jak dotad)
            int ng = n[StartCalibration.ZGrain]; if (b.OwnGs) ng -= ZcOwnGs(b, vil, StartCalibration.ZGrain, ng);
            ZcTake(b, StartCalibration.ZGrain, ng, 0); done[StartCalibration.ZGrain] = true;
            int ns = n[StartCalibration.ZSalt]; if (b.OwnGs) ns -= ZcOwnGs(b, vil, StartCalibration.ZSalt, ns);
            ZcTake(b, StartCalibration.ZSalt, ns, 0); done[StartCalibration.ZSalt] = true;
            b.Lim = lim; b.NoCap = false;
            // m105 gosp4 G1 (klucz Ks Village Cheap Kcal First, tylko z budzetem W1): napoje, bialko i reszta jedzenia jedna lista wedlug zl za 1000 kcal przy cenie polki doby
            // (bez towaru na koncu), zamiana wino -> piwo -> piwo domowe po przejsciu (ten sam budzet); rzeczy jak dotad po calym jedzeniu (4). Plan (KsBasket.Rungs) bez zmian.
            if (b.CapOn && StartCalibration.SwCheapKcalFirst) ZcCheapKcal(b, n, done);
            else
            {
                // (2) napoj - najtanszy za litr najpierw; brak wina -> piwo, brak piwa -> ziarno na piwo domowe (0.4 kg na litr)
                var dr = new List<int>(ZcDrinks);
                var unit = new double[StartCalibration.ZN];
                foreach (int z in dr) { EquipmentElement e; int h; int p = ZcPrice(b, z, out e, out h); unit[z] = p > 0 && h > 0 ? p / StartCalibration.ZKg(z) : double.MaxValue; }
                dr.Sort((x, y) => unit[x].CompareTo(unit[y]));
                var un = new int[StartCalibration.ZN];
                foreach (int z in dr) { un[z] = n[z] - ZcTake(b, z, n[z], 1); done[z] = true; }
                ZcDrinkSwap(b, un);
                // (3) bialko - najtansze za 1000 kcal najpierw
                var pr = new List<int>(ZcProtein);
                foreach (int z in pr) { EquipmentElement e; int h; int p = ZcPrice(b, z, out e, out h); unit[z] = p > 0 && h > 0 ? p / StartCalibration.ZKg(z) / (ZcKcal[z] / 1000.0) : double.MaxValue; }
                pr.Sort((x, y) => unit[x].CompareTo(unit[y]));
                foreach (int z in pr) { ZcTake(b, z, n[z], 1); done[z] = true; }
            }
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
                        var keepT = b.T; var keepR = b.R; var keepM = b.Mk; var keepZ = b.Z;
                        b.T = alt.Town; b.R = alt.ItemRoster; b.Mk = alt;
                        if (b.Fast) b.Z = StartCalibration.ZTownFor(b.T);          // ks-koszt T5
                        var gf2 = GoodsLedger.Begin(GoodsLedger.FFolkVill, alt);
                        long s0 = b.Spent;
                        try
                        {
                            b.InF2 = true;
                            foreach (int z in StartCalibration.ZLadder)
                            {
                                if (b.Miss[z] <= 0) continue;
                                b.Lim = z == StartCalibration.ZGrain || z == StartCalibration.ZSalt ? 0 : lim;
                                b.NoCap = z == StartCalibration.ZGrain || z == StartCalibration.ZSalt;   // m103 wsie: jak w miescie targowym
                                int got = ZcTake(b, z, b.Miss[z], StartCalibration.ZFood(z) ? 1 : 2, false);
                                if (got > 0) { b.Miss[z] -= got; b.Elsewhere += got; }
                                if (b.Broke) break;
                            }
                        }
                        finally
                        {
                            GoodsLedger.End(gf2); b.T = keepT; b.R = keepR; b.Mk = keepM; b.Z = keepZ; b.InF2 = false; b.NoCap = false;
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

        /// <summary>Zakup zastepczy napoju (recenzja 17.10 - wydzielone bez zmian z drabiny w m105 gosp4): un - niekupione sztuki napojow; brak wina - piwo, brak piwa - ziarno
        /// na piwo domowe (KsTab.BeerGrain kg ziarna na litr). Napoj zastepczy zmniejsza brak do szukania w miastach obok.</summary>
        private static void ZcDrinkSwap(ZcBuy b, int[] un)
        {
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
        }

        /// <summary>
        /// m105 gosp4 G1 (klucz Ks Village Cheap Kcal First; projekt czesci 4, sim_drabina na n31/n32: kcal czesci zaplaconej W +2.7..+3.9 pp przy tym samym pieniadzu): po ziarnie
        /// i soli JEDZENIE drabiny 1 (napoje, bialko, reszta jedzenia z ZLadder) jedna lista wedlug zl za 1000 kcal przy cenie polki miasta targowego z doby - gdy budzet z wplywu
        /// (W1) sie konczy, czekaja najdrozsze kalorie, nie zawsze bialko i owoce za napojem. Kolejnosc: towar z cena i kcal (najtanszy za 1000 kcal pierwszy), towar bez kcal
        /// (przyprawy), towar bez polki (szukany potem w miastach obok, F2) - przy rownej cenie kolejnosc drabiny. Zakup zastepczy napoju (wino -> piwo -> piwo domowe) po
        /// przejsciu listy, w tym samym budzecie. Rzeczy (przedza, len, skora, narzedzia, garnki) dalej po calym jedzeniu (krok (4) w ZcLadder). Wolac tylko z budzetem W1.
        /// </summary>
        private static void ZcCheapKcal(ZcBuy b, int[] n, bool[] done)
        {
            int nz = StartCalibration.ZN;
            var fl = new List<int>(24);
            foreach (int z in StartCalibration.ZLadder) if (!done[z] && StartCalibration.ZFood(z)) fl.Add(z);
            var key = new double[nz]; var cls = new int[nz]; var pos = new int[nz];
            for (int i = 0; i < fl.Count; i++)
            {
                int z = fl[i]; pos[z] = i;
                EquipmentElement e; int h; int p = ZcPrice(b, z, out e, out h);
                double kc = StartCalibration.ZKg(z) * KsBasket.Kcal[z];
                if (p <= 0 || h <= 0) cls[z] = 2;                                    // bez towaru na polce - na koncu (miasta obok)
                else if (!(kc > 0)) cls[z] = 1;                                      // bez kcal (przyprawy) - po jedzeniu z kcal
                else key[z] = p / (kc / 1000.0);                                     // zl za 1000 kcal przy cenie polki doby
            }
            fl.Sort((x, y) => { int c = cls[x].CompareTo(cls[y]); if (c != 0) return c; c = key[x].CompareTo(key[y]); return c != 0 ? c : pos[x].CompareTo(pos[y]); });
            var un = new int[nz];
            foreach (int z in fl) { un[z] = n[z] - ZcTake(b, z, n[z], 1); done[z] = true; }
            ZcDrinkSwap(b, un);
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
                // ks-koszt T3 (klucz Ks Cost Fast): droga ladowa wies -> miasto z pamieci sesji (tablica drog osada -> osada gry jest stala w sesji; ta sama przebudowa
                // co 7 dob, ten sam filtr i sortowanie - tylko bez ponownego pytania gry o te sama pare; blad zapytania nie jest pamietany)
                Dictionary<Settlement, float> rdMemo = null;
                if (dm != null && StartCalibration.SwCostFast && !_ntDist.TryGetValue(vil, out rdMemo)) { rdMemo = new Dictionary<Settlement, float>(); _ntDist[vil] = rdMemo; }
                foreach (var t in Town.AllTowns)
                {
                    if (t == null || !t.IsTown || t.Settlement == null || ReferenceEquals(t.Settlement, mk)) continue;
                    float dd = pos.Distance(t.Settlement.GetPosition2D);
                    if (dd > 250f) continue;
                    if (dm != null)
                    {
                        float rd = -1f;
                        if (rdMemo != null && rdMemo.TryGetValue(t.Settlement, out rd)) { }
                        else
                        {
                            bool ok = true;
                            try { rd = dm.GetDistance(vil.Settlement, t.Settlement, false, false, TaleWorlds.CampaignSystem.Party.MobileParty.NavigationType.Default); } catch { rd = -1f; ok = false; }
                            if (ok && rdMemo != null) rdMemo[t.Settlement] = rd;
                        }
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
        private static void KsReset() { _nearTowns.Clear(); _nearDay = -1; _zcCarry.Clear(); _zcCarry2.Clear(); _ntDist.Clear(); }
        private static readonly Dictionary<Village, Dictionary<Settlement, float>> _ntDist = new Dictionary<Village, Dictionary<Settlement, float>>();   // ks-koszt T3: droga wies -> miasto (sesja)

        /// <summary>ks-koszt (kontrola T3): najwyzej max par z pamieci drog wies -> miasto wobec ponownego pytania gry (tylko odczyt; blad zapytania - pominiety).</summary>
        internal static void KsNtCheck(int max, out int ok, out int bad)
        {
            ok = 0; bad = 0;
            TaleWorlds.CampaignSystem.ComponentInterfaces.MapDistanceModel dm = null;
            try { dm = TaleWorlds.CampaignSystem.Campaign.Current.Models.MapDistanceModel; } catch { dm = null; }
            if (dm == null) return;
            int n = 0;
            foreach (var kv in _ntDist)
            {
                if (kv.Key == null || kv.Key.Settlement == null || kv.Value == null) continue;
                foreach (var e in kv.Value)
                {
                    if (n++ >= max) return;
                    float rd;
                    try { rd = dm.GetDistance(kv.Key.Settlement, e.Key, false, false, TaleWorlds.CampaignSystem.Party.MobileParty.NavigationType.Default); } catch { continue; }
                    if (BitConverter.ToInt32(BitConverter.GetBytes(rd), 0) == BitConverter.ToInt32(BitConverter.GetBytes(e.Value), 0)) ok++; else bad++;
                }
            }
        }
        private static long _zcBrewV;
    }
}
