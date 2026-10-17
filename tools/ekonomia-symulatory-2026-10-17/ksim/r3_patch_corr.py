import sys
p = sys.argv[1]
s = open(p, encoding='utf-8').read()


def rep(a, b, cnt=1):
    global s
    n = s.count(a)
    assert n == cnt, (a[:80], n)
    s = s.replace(a, b)


rep("""            R.Kraina.AddRange(kraina);
            // tlumienie i gradient skuteczny
            double dmax = 0, gmax = 1.0; int dbig = 0;""", """            R.Kraina.AddRange(kraina);
            // KS-R3 runda 3 symulatora: psujace sie na poziomie miast (przewoz miast w 7 dobach, gradient miasta, niedowiezione do reguly zasiegu)
            double[] tgraw = null;
            try { tgraw = TownPerish(x, p, prod); } catch (Exception te) { tgraw = null; x.TownPerishDone = false; R.Kraina.Add("blad przewozu miast " + te.GetType().Name); }
            // tlumienie i gradient skuteczny
            double dmax = 0, gmax = 1.0; int dbig = 0;""")
rep("""                        x.GradBase[i] = nv;
                        if (Math.Abs(nv - 1.0) > Math.Abs(gmax - 1.0)) gmax = nv;
                    }
                RefreshGradEff(x);""", """                        x.GradBase[i] = nv;
                        if (Math.Abs(nv - 1.0) > Math.Abs(gmax - 1.0)) gmax = nv;
                    }
                // KS-R3 runda 3 symulatora: gradient miast psujacych sie - to samo tlumienie (0.5 poprzedni + 0.5 nowy); blizej 1 niz 0.5% - 1 (miasto bez dodatku)
                x.TGMin = x.TGMax = 1.0; x.TGN = 0;
                if (tgraw != null)
                    for (int t = 0; t < x.NT; t++)
                        foreach (int g in Food)
                        {
                            if (!PerishG[g]) continue;
                            int i = t * NG + g;
                            double nv = Math.Max(CorrMinG, Math.Min(CorrMaxG, 0.5 * x.TGradBase[i] + 0.5 * tgraw[i]));
                            if (Math.Abs(nv - 1.0) < 0.005) nv = 1.0;
                            double dd = Math.Abs(nv - x.TGradBase[i]) / Math.Max(1e-9, x.TGradBase[i]);
                            if (dd > dmax) dmax = dd;
                            if (dd > 0.05) dbig++;
                            x.TGradBase[i] = nv;
                            if (nv != 1.0) { x.TGN++; if (nv < x.TGMin) x.TGMin = nv; if (nv > x.TGMax) x.TGMax = nv; }
                            if (Math.Abs(nv - 1.0) > Math.Abs(gmax - 1.0)) gmax = nv;
                        }
                RefreshGradEff(x);""")
rep("""                    gd[x.Lands[l]] = a;
                }
                R.Grad = gd;""", """                    gd[x.Lands[l]] = a;
                }
                for (int t = 0; t < x.NT; t++)
                {
                    bool any = false; foreach (int g in Food) if (PerishG[g] && x.TGradBase[t * NG + g] != 1.0) { any = true; break; }
                    if (!any) continue;
                    var a = new double[NG]; for (int g = 0; g < NG; g++) a[g] = PerishG[g] && FoodMask[g] ? x.TGradBase[t * NG + g] : 1.0;
                    gd["@" + inp.Towns[t].Id] = a;
                }
                R.Grad = gd;""")
TP = open(sys.argv[2], encoding='utf-8').read()
rep("""        /// <summary>Pozycje koszyka ZC4 towaru psujacego sie KsTab g (mieso - mieso i mieso wieloryba).</summary>""",
    TP + """
        /// <summary>Pozycje koszyka ZC4 towaru psujacego sie KsTab g (mieso - mieso i mieso wieloryba).</summary>""")
rep("""                    if (!(dm > 1.0) || sup[t * NG + g] >= 0.05 * dm) continue;
                    bool ok = false;
                    for (int u = 0; u < nt && !ok; u++)""", """                    if (!(dm > 1.0) || sup[t * NG + g] >= 0.05 * dm) continue;
                    bool ok = false;
                    // KS-R3 runda 3 symulatora: po przewozie miast (TownPerish) - wyciete, gdy ponad polowa popytu miasta zostaje bez dowozu w 7 dobach (dotad: jakiekolwiek
                    // miasto z nadwyzka w 7 dobach - jedno male miasto rybackie "zasilalo" kilkanascie miast w glebi ladu); bez przewozu miast - jak dotad
                    if (x.TUnmet != null && x.TownPerishDone) ok = x.TUnmet[t * NG + g] <= 0.5;
                    else
                    for (int u = 0; u < nt && !ok; u++)""")
rep("""            public double TGMin = 1.0, TGMax = 1.0; public int TGN;""", """            public double TGMin = 1.0, TGMax = 1.0; public int TGN; public bool TownPerishDone;""")
open(p, 'w', encoding='utf-8', newline='\n').write(s)
print('ok')
