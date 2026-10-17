import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from edlib import rep
P = r'C:/Users/GAME/AppData/Local/Temp/claude/C--Users-GAME-Bannerlor2-Realm-of-Thrones/ae222cb7-a7c2-473a-8fc1-1dc1d6f688e3/scratchpad/zb/n-ks/Armoury/src/StartCalibration.Core.cs'
pairs = []
# --- land-priced village basket (review r3 #4)
pairs.append(("""                    double bask = 0; for (int z = 0; z < NZ; z++) bask += q[z] * pz[m * NZ + z];
                    double budget = v.Alpha * salesB;
                    double pv;
                    if (v.Cut != null)
                    {
                        // KS-R3 regula zasiegu: psujacego sie towaru nikt nie dowiezie - pozycja poza drabina (budzet idzie na reszte), czesc zaplacona 0
                        Array.Copy(q, qq, NZ);
                        for (int z = 0; z < NZ && z < v.Cut.Length; z++) if (v.Cut[z]) qq[z] = 0.0;
                        pv = KsBasket.Ladder(qq, pz, m * NZ, budget, paid);
                        for (int z = 0; z < NZ && z < v.Cut.Length; z++) if (v.Cut[z]) paid[z] = 0.0;
                    }
                    else pv = KsBasket.Ladder(q, pz, m * NZ, budget, paid);""",
"""                    // recenzja runda 3 (wazne): koszyk przy cenie KRAINY okregu (cena startu rynku x gradient krainy - KS-H dowozi tam po koszcie drogi); bez gradientu - jak dotad
                    int lv = x.GradOn ? LandOfD(x, d) : -1;
                    double[] pzu = pz; int po = m * NZ;
                    if (lv >= 0) { for (int z = 0; z < NZ; z++) pzv[z] = pz[m * NZ + z] * GradZ(x, lv, z); pzu = pzv; po = 0; }
                    double bask = 0; for (int z = 0; z < NZ; z++) bask += q[z] * pzu[po + z];
                    double budget = v.Alpha * salesB;
                    double pv;
                    if (v.Cut != null)
                    {
                        // KS-R3 regula zasiegu: psujacego sie towaru nikt nie dowiezie - pozycja poza drabina (budzet idzie na reszte), czesc zaplacona 0
                        Array.Copy(q, qq, NZ);
                        for (int z = 0; z < NZ && z < v.Cut.Length; z++) if (v.Cut[z]) qq[z] = 0.0;
                        pv = KsBasket.Ladder(qq, pzu, po, budget, paid);
                        for (int z = 0; z < NZ && z < v.Cut.Length; z++) if (v.Cut[z]) paid[z] = 0.0;
                    }
                    else pv = KsBasket.Ladder(q, pzu, po, budget, paid);"""))
# --- physical substitute 8b/8c (review r3 #12)
pairs.append(("""                    v.Sur = hw2 > hwc ? Math.Max(0.0, svs) / (hw2 - hwc) : 0.0;
                    for (int z = 0; z < NZ; z++)
                    {
                        u[z] = (1.0 - paid[z]) * q[z];
                        if (!(u[z] > 0)) continue;
                        double r = u[z] * RawZ(sz, m * NZ, z);
                        if (v.Ok[z]) { raw += r; if (KsBasket.Kcal[z] > 0) rawF += r; }
                        else if (v.Sub >= 0 && KsBasket.Kcal[z] > 0 && KsBasket.Kcal[v.Sub] > 0)
                        {
                            // KS-R3 krok 8b: kcal pozycji, ktorej okreg sam nie zrobi, robi praca domowa jako najtanszy wlasny towar okregu (kcal za kcal)
                            double kz = u[z] * KsBasket.Kcal[z];
                            double rs = kz / KsBasket.Kcal[v.Sub] * RawZ(sz, m * NZ, v.Sub);
                            raw += rs; rawF += rs;
                            subK += kz;
                        }
                        else rawNo += r;
                    }
                    // KS-R3 krok 8c (port symulatora 17.10, runda 2; klucz Ks Alpha Net jak 8b): gdy rak na wlasny niezaplacony koszyk nie starcza (surowiec > HomeMax x V0),
                    // praca domowa robi kcal najpierw najtanszym wlasnym towarem zastepczym (zboze, ryby, dziczyzna, daktyle) zamiast pozycji drozszych za kcal (mieso, ser,
                    // wino...) - kcal za kcal, od najdrozszej, tylko tyle, ile trzeba, zeby rece starczyly. Biedna wies je chleb zamiast miesa, a nie glodzi sie przy pelnym
                    // koszyku (harness: 54 okregi z bez pokrycia > 20%, Westeros Plan bez pokrycia 7.5% - oliwnik w Reach z koszykiem 4 x sprzedaz, raw / V0 1.31)
                    if (v.Sub >= 0 && v0 > 0 && rawF > inp.HomeMax * v0 && KsBasket.Kcal[v.Sub] > 0)
                    {
                        double subPer = RawZ(sz, m * NZ, v.Sub) / KsBasket.Kcal[v.Sub];
                        int nc = 0;
                        for (int z = 0; z < NZ; z++)
                        {
                            if (!v.Ok[z] || z == v.Sub || !(u[z] > 0) || !(KsBasket.Kcal[z] > 0)) continue;
                            double per = RawZ(sz, m * NZ, z) / KsBasket.Kcal[z];
                            if (per > subPer * 1.0001) { cvZ[nc] = z; cvP[nc] = per; nc++; }
                        }
                        Array.Sort(cvP, cvZ, 0, nc);
                        for (int j = nc - 1; j >= 0 && rawF > inp.HomeMax * v0; j--)
                        {
                            int z = cvZ[j];
                            double kz = u[z] * KsBasket.Kcal[z], save = kz * (cvP[j] - subPer);
                            if (!(save > 0)) continue;
                            double fr = Math.Min(1.0, (rawF - inp.HomeMax * v0) / save);
                            raw -= fr * save; rawF -= fr * save; stK += fr * kz;
                        }
                    }""",
"""                    v.Sur = hw2 > hwc ? Math.Max(0.0, svs) / (hw2 - hwc) : 0.0;
                    // recenzja runda 3 (wazne): TOWAR ZASTEPCZY 8b/8c Z PLONU RAK - rece pracy domowej x P0 zajecia towaru zastepczego x ziemia bloku (zboze: ziemia zboza), najwyzej
                    // to, co urodzi wolna ziemia bloku (zboze, daktyle; ryby i dziczyzna - bez ziemi); dotad parytet wartosci (reka w domu robila tyle wartosci, ile w zajeciu -
                    // oliwnik w Reach "siał" w domu wiecej zboza, niz da jego ziemia). Wydajnosc ta sama co zajecia w tabeli swiata
                    double ySub = 0, capSub = double.PositiveInfinity, kcSub = v.Sub >= 0 ? KsBasket.Kcal[v.Sub] : 0.0, subVal = 0;
                    if (v.Sub >= 0 && kcSub > 0) SubPhys(d, v.Parts, v.Sub, out ySub, out capSub);
                    bool phys = v.Sub >= 0 && kcSub > 0 && ySub > 0 && v0 > 0;
                    for (int z = 0; z < NZ; z++)
                    {
                        u[z] = (1.0 - paid[z]) * q[z];
                        if (!(u[z] > 0)) continue;
                        double r = u[z] * RawZ(sz, m * NZ, z);
                        if (v.Ok[z]) { raw += r; if (KsBasket.Kcal[z] > 0) rawF += r; }
                        else if (phys && KsBasket.Kcal[z] > 0)
                        {
                            // KS-R3 krok 8b: kcal pozycji, ktorej okreg sam nie zrobi, robi praca domowa jako najtanszy wlasny towar okregu (kcal za kcal, z plonu)
                            double kz = u[z] * KsBasket.Kcal[z];
                            subVal += kz / kcSub * RawZ(sz, m * NZ, v.Sub);
                            subK += kz;
                        }
                        else rawNo += r;
                    }
                    double subKeff = 0;
                    if (phys)
                    {
                        // rece (czesc rak zajec) w jednostkach pracy domowej: pozycje okregu z parytetu wartosci (V0 - jak dotad), towar zastepczy z plonu
                        double hwOk = raw / v0, hwOkF = rawF / v0, kgS = Math.Min(subK / kcSub, capSub);
                        subKeff = kgS * kcSub;
                        if (subK > subKeff + 1e-9) M.SubLandShort[m] += subK - subKeff;        // kcal zastepcze bez wolnej ziemi pod plon - zostaja bez pokrycia
                        double hwS = kgS / ySub;
                        if (subK > 0 && subVal / v0 < hwS * 0.999) M.SubPhysOver[m]++;      // parytet wartosci dawal wiecej towaru, niz rece urodza
                        // KS-R3 krok 8c (klucz Ks Alpha Net jak 8b): rak na wlasny niezaplacony koszyk nie starcza (hw > HomeMax) - pozycje drozsze w rekach za kcal niz towar
                        // zastepczy (z plonu) robione jako towar zastepczy, od najdrozszej, tylko tyle, ile trzeba (i ile urodzi wolna ziemia). Biedna wies je chleb zamiast miesa
                        double hwPerKcalSub = 1.0 / (kcSub * ySub);
                        if (hwOkF + hwS > inp.HomeMax)
                        {
                            int nc = 0;
                            for (int z = 0; z < NZ; z++)
                            {
                                if (!v.Ok[z] || z == v.Sub || !(u[z] > 0) || !(KsBasket.Kcal[z] > 0)) continue;
                                double per = RawZ(sz, m * NZ, z) / KsBasket.Kcal[z] / v0;
                                if (per > hwPerKcalSub * 1.0001) { cvZ[nc] = z; cvP[nc] = per; nc++; }
                            }
                            Array.Sort(cvP, cvZ, 0, nc);
                            for (int j = nc - 1; j >= 0 && hwOkF + hwS > inp.HomeMax; j--)
                            {
                                int z = cvZ[j];
                                double kz = u[z] * KsBasket.Kcal[z], save = kz * (cvP[j] - hwPerKcalSub);
                                if (!(save > 0)) continue;
                                double roomKg = capSub - kgS; if (!(roomKg > 1e-9)) break;
                                double fr = Math.Min(Math.Min(1.0, (hwOkF + hwS - inp.HomeMax) / save), roomKg * kcSub / kz);
                                if (!(fr > 0)) continue;
                                hwOk -= fr * kz * cvP[j]; hwOkF -= fr * kz * cvP[j];
                                kgS += fr * kz / kcSub; hwS = kgS / ySub;
                                stK += fr * kz;
                            }
                        }
                        raw = (hwOk + hwS) * v0; rawF = (hwOkF + hwS) * v0;                 // w jednostkach V0 (cel = raw / V0)
                    }"""))
pairs.append(("""                        hkc = cov * (hkc + subK);                                 // KS-R3 krok 8b: kcal zastepcze tez robione w domu (ta sama czesc pokryta)
                        R.DistHomeKcal[d.Id] = fk > 0 ? (float)Math.Max(0.0, Math.Min(1.0, hkc / fk)) : 0f;
                        M.VFullKcal[m] += fk; M.VUnpaidKcal[m] += uk; M.VHomeKcal[m] += hkc; M.VSubKcal[m] += cov * (subK + stK);   // 8b + 8c (kcal robione towarem zastepczym)""",
"""                        hkc = cov * (hkc + subKeff);                              // KS-R3 krok 8b: kcal zastepcze tez robione w domu (ta sama czesc pokryta; z plonu - recenzja runda 3)
                        R.DistHomeKcal[d.Id] = fk > 0 ? (float)Math.Max(0.0, Math.Min(1.0, hkc / fk)) : 0f;
                        M.VFullKcal[m] += fk; M.VUnpaidKcal[m] += uk; M.VHomeKcal[m] += hkc; M.VSubKcal[m] += cov * (subKeff + stK);   // 8b + 8c (kcal robione towarem zastepczym)
                        // recenzja runda 3: Plan bez pokrycia wedlug krainy (kraina miasta targowego okregu) - linia pieniedzy Planu
                        string lnm = x.LandIx != null && LandOfD(x, d) >= 0 ? x.Lands[LandOfD(x, d)] : (d.Land ?? "?");
                        double[] lk; if (!M.LandKcal.TryGetValue(lnm, out lk)) { lk = new double[3]; M.LandKcal[lnm] = lk; }
                        lk[0] += fk; lk[1] += Math.Max(0.0, uk - hkc); lk[2] = m;"""))
# town baskets at land price
pairs.append(("""                // ---- miasta (klasy W/R/P)
                foreach (var t in inp.Towns)
                {""", """                // ---- miasta (klasy W/R/P)
                int ti = -1;
                foreach (var t in inp.Towns)
                {
                    ti++;"""))
pairs.append(("""                    int m = t.Market == 0 ? 0 : 1;
                    Array.Clear(tqs, 0, NZ); Array.Clear(tus, 0, NZ);
                    bool[] tcut = null; if (x.ReachT != null) x.ReachT.TryGetValue(t.Id, out tcut);""",
"""                    int m = t.Market == 0 ? 0 : 1;
                    Array.Clear(tqs, 0, NZ); Array.Clear(tus, 0, NZ);
                    bool[] tcut = null; if (x.ReachT != null) x.ReachT.TryGetValue(t.Id, out tcut);
                    // recenzja runda 3: koszyk klas przy cenie krainy miasta (jak wsie)
                    int lt = x.GradOn && x.TownLand != null && ti < x.TownLand.Length ? x.TownLand[ti] : -1;
                    double[] tpz = pz; int tpo = m * NZ;
                    if (lt >= 0) { for (int z = 0; z < NZ; z++) pzv[z] = pz[m * NZ + z] * GradZ(x, lt, z); tpz = pzv; tpo = 0; }"""))
pairs.append(("""                        double cost = 0; for (int z = 0; z < NZ; z++) cost += qc[z] * pz[m * NZ + z];
                        double pv;
                        if (tcut != null)
                        {
                            // KS-R3 regula zasiegu: psujace sie bez zrodla w 7 dobach - poza drabina miasta (budzet na reszte), czesc zaplacona 0 (odpada z popytu)
                            Array.Copy(qc, qq, NZ);
                            for (int z = 0; z < NZ && z < tcut.Length; z++) if (tcut[z]) qq[z] = 0.0;
                            pv = KsBasket.Ladder(qq, pz, m * NZ, Math.Max(0.0, t.Budget[c]), paid);
                            for (int z = 0; z < NZ && z < tcut.Length; z++) if (tcut[z]) paid[z] = 0.0;
                        }
                        else pv = KsBasket.Ladder(qc, pz, m * NZ, Math.Max(0.0, t.Budget[c]), paid);""",
"""                        double cost = 0; for (int z = 0; z < NZ; z++) cost += qc[z] * tpz[tpo + z];
                        double pv;
                        if (tcut != null)
                        {
                            // KS-R3 regula zasiegu: psujace sie bez zrodla w 7 dobach - poza drabina miasta (budzet na reszte), czesc zaplacona 0 (odpada z popytu)
                            Array.Copy(qc, qq, NZ);
                            for (int z = 0; z < NZ && z < tcut.Length; z++) if (tcut[z]) qq[z] = 0.0;
                            pv = KsBasket.Ladder(qq, tpz, tpo, Math.Max(0.0, t.Budget[c]), paid);
                            for (int z = 0; z < NZ && z < tcut.Length; z++) if (tcut[z]) paid[z] = 0.0;
                        }
                        else pv = KsBasket.Ladder(qc, tpz, tpo, Math.Max(0.0, t.Budget[c]), paid);"""))
pairs.append(("""            var qq = new double[NZ];                                       // KS-R3: koszyk bez pozycji wycietych regula zasiegu (drabina zastepuje)""",
"""            var qq = new double[NZ];                                       // KS-R3: koszyk bez pozycji wycietych regula zasiegu (drabina zastepuje)
            var pzv = new double[NZ];                                      // recenzja runda 3: ceny koszyka krainy okregu / miasta (cena startu x gradient krainy)"""))
rep(P, pairs)
