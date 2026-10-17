import io, sys
p = sys.argv[1]
s = io.open(p, encoding='utf-8').read()
n0 = len(s)


def rep(a, b, cnt=1):
    global s
    c = s.count(a)
    if c != cnt:
        raise SystemExit("anchor count %d != %d: %r" % (c, cnt, a[:120]))
    s = s.replace(a, b)


# ---------------------------------------------------------------- regexy
rep('''NKS_MKTIT = re.compile(r"(?P<g>[A-Za-z_]+) (?P<p>-?\\d+)/(?P<c>-?\\d+)(?:/(?P<s>-?\\d+))?")''',
    '''NKS_MKTIT = re.compile(r"(?P<g>[A-Za-z_]+) (?P<p>-?\\d+)/(?P<c>-?\\d+)(?:/(?P<s>-?\\d+))?(?:/(?P<t>-?\\d+))?")   # KS-R3: czwarta liczba - w drodze KS-H''')
rep('''NKS_ZC4K8 = re.compile(r"(?P<mk>Westeros|Essos) wartosc przy cenach polki chciana (?P<w>-?\\d+) / zaplacona z Planu (?P<p>-?\\d+) / kupiona (?P<g>-?\\d+) zl, kcal koszyka kupione (?P<kb>[\\d.]+)% / "
                       r"robione w domu (?P<kh>[\\d.]+)% / bez pokrycia (?P<kz>[\\d.]+)%, wplyw do kies wsi \\(utarg doby\\) (?P<i>-?\\d+) zl")''',
    '''NKS_ZC4K8 = re.compile(r"(?P<mk>Westeros|Essos) wartosc przy cenach polki chciana (?P<w>-?\\d+) / zaplacona z Planu (?P<p>-?\\d+) / kupiona (?P<g>-?\\d+) zl, kcal koszyka kupione (?P<kb>[\\d.]+)% / "
                       r"robione w domu (?P<kh>[\\d.]+)%(?: / z magazynu (?P<kr>[\\d.]+)%)? / bez pokrycia (?P<kz>[\\d.]+)%, wplyw do kies wsi \\(utarg doby\\) (?P<i>-?\\d+) zl"
                       r"(?: \\(netto z frachtem KS-H i bez wydatkow spoza koszyka (?P<inet>-?\\d+) zl\\))?")
# KS-R3 (17.10, "kupcy i krainy"): kupcy zywnosci KS-H, korytarze planu, sterowanie rynku, podloga spichlerza, wies je swoje, drabina 1 wsi, Plan bez pokrycia
NKS_KSH = re.compile(r"Kupcy zywnosci \\(KS-H\\) dzien (?P<day>-?\\d+) \\| (?P<state>CZYNNE|poza stanem[^|]*|WYLACZONE[^|]*) \\| wozow w (?P<lots>\\d+) \\(lad (?P<land>\\d+), morze (?P<sea>\\d+)\\), "
                     r"kupione (?P<bought>\\d+) szt\\. za (?P<gold>-?\\d+) zl .*?dowiezione (?P<arr>\\d+), zawrocone (?P<back>\\d+) \\(zwrot (?P<ref>-?\\d+) zl\\), czeka pod oblezeniem (?P<wait>\\d+), "
                     r"kasa celu za mala (?P<poor>\\d+) szt\\. \\(zamowione (?P<ord>\\d+)\\) \\| w drodze (?P<tr>\\d+) szt\\. w (?P<carts>\\d+) wozach"
                     r"(?: \\| kontrola towaru reszta (?P<gr>-?\\d+) .*?zlota reszta (?P<zr>-?\\d+))?.*?\\| probka wyceny 1/64 (?P<se>[\\d.]+|-)%?.*?\\| czas (?P<ms>[\\d.]+) ms \\| potkniecia (?P<stum>\\d+)")
NKS_KSHCOR = re.compile(r"\\| korytarze \\(dowiezione, srednia 28 dob, szt\\./d\\):(?P<body>.*)$")
NKS_CORITEM = re.compile(r"(?P<a>[A-Za-z_]+)>(?P<b>[A-Za-z_]+) (?P<g>[A-Za-z_]+) (?P<x>[\\d.]+)")
NKS_CORR = re.compile(r"KS: korytarze planu doba (?P<day>-?\\d+) ")
NKS_CORRBIG = re.compile(r"\\| korytarze >= 20 szt\\./d:(?P<body>[^|]*)")
NKS_CORRKR = re.compile(r"\\| kraina-tryb: (?P<body>[^|]*)")
NKS_CORRCUT = re.compile(r"\\| ciecie nadmiaru krain:(?P<body>[^|]*)")
NKS_CORRGR = re.compile(r"\\| gradient max (?P<g>[\\d.]+) \\(zmiana ostatniej rundy (?P<d>[\\d.]+)%")
NKS_CTL = re.compile(r"KS: sterowanie rynku doba (?P<day>-?\\d+) ")
NKS_CTLIT = re.compile(r"(?P<g>[A-Za-z_]+) (?P<c>-?\\d+)/(?P<u>-?\\d+)/(?P<d>-?\\d+) x(?P<k>[\\d.]+)")
NKS_FLOOR = re.compile(r"KS: podloga spichlerza dzien (?P<day>-?\\d+) \\| wsi z podloga (?P<on>\\d+) z (?P<n>\\d+) \\([^)]*\\), kcal podlogi (?P<kf>[\\d.]+|-)% potrzeby wsi, powroty (?P<back>\\d+)")
NKS_OWN = re.compile(r"KS: wies je swoje dzien (?P<day>-?\\d+) \\| wsi (?P<v>\\d+), szt\\. (?P<p>\\d+), kcal (?P<k>[\\d.]+|-)% koszyka.*?wsi > 30% koszyka 14 dob z rzedu: (?P<s>\\d+)")
NKS_P1 = re.compile(r"\\| drabina 1 \\(zaplacone z Planu\\): chciane (?P<w>-?\\d+) szt\\., bez towaru (?P<ng>-?\\d+) \\([^)]*\\), bez kiesy (?P<nm>-?\\d+)")
NKS_PLANUNC = re.compile(r"\\| (?P<mk>Westeros|Essos): wsie \\d+ .*?KS-R3: kcal zastepcze 8b (?P<sub>[\\d.-]+)%, Plan bez pokrycia (?P<unc>[\\d.-]+)%")
# KS-R3: wyniki rundy 3 (autotest 2026-10-17_01-15-30, symulator ksim/sim/merged.py wedlug spec. KS-R3) - prog ETAP "nie gorzej niz runda 3"
NKS_R3 = {"nks5": 25, "nks6": 27, "nks7": 26, "nks7b": 8, "nks23": 0.40, "nks23b2": 0.50, "nks8c": 40.0}''')

# ---------------------------------------------------------------- zbieranie
rep('''    zc5 = {}
    goods = {}         # (dzien, towar) -> (zapas, produkcja, zuzycie) - swiat''', '''    zc5 = {}
    ksh = {}           # KS-R3: doba -> liczby linii KS-H
    ksh_cor = {}       # doba -> {(a, b, towar): szt./d dowiezione (srednia 28 dob)}
    corr = {}          # doba Planu -> {"big": {(a, b, towar): szt./d}, "kraina": tekst, "cut": tekst, "g": (gmax, zmiana)}
    ctl = {}           # doba Planu -> {towar: (zuzycie, brak, podaz, czynnik)}
    floor = {}         # doba -> (z podloga, wsi, kcal %, powroty)
    own = {}           # doba -> (wsi, szt., kcal %, wsi > 30% 14 dob)
    p1 = {}            # doba -> (chciane, bez towaru, bez kiesy) drabina 1 wsi
    planunc = {}       # doba Planu -> {rynek: (zastepcze %, bez pokrycia %)}
    goods = {}         # (dzien, towar) -> (zapas, produkcja, zuzycie) - swiat''')
rep('''                    k8 = {m8.group("mk"): m8.groupdict() for m8 in NKS_ZC4K8.finditer(s)}
                    if k8:
                        zc4_k8[d] = k8
                continue''', '''                    k8 = {m8.group("mk"): m8.groupdict() for m8 in NKS_ZC4K8.finditer(s)}
                    if k8:
                        zc4_k8[d] = k8
                    mq1 = NKS_P1.search(s)
                    if mq1:
                        p1[d] = (int(mq1.group("w")), int(mq1.group("ng")), int(mq1.group("nm")))
                continue
            if s.startswith("Kupcy zywnosci (KS-H) dzien "):
                mm = NKS_KSH.match(s)
                if mm:
                    ksh[int(mm.group("day"))] = mm.groupdict()
                    mc2 = NKS_KSHCOR.search(s)
                    if mc2:
                        ksh_cor[int(mm.group("day"))] = {(it.group("a"), it.group("b"), it.group("g")): float(it.group("x")) for it in NKS_CORITEM.finditer(mc2.group("body"))}
                continue''')
rep('''                            for it in NKS_MKTIT.finditer(sg.group("body")):
                                dd[it.group("g")] = (int(it.group("p")), int(it.group("c")), int(it.group("s")) if it.group("s") else None)
                            mkt[(d, k)] = dd''', '''                            for it in NKS_MKTIT.finditer(sg.group("body")):
                                dd[it.group("g")] = (int(it.group("p")), int(it.group("c")), int(it.group("s")) if it.group("s") else None, int(it.group("t")) if it.group("t") else 0)
                            mkt[(d, k)] = dd''')
rep('''                        dd8["_head"] = mm.groupdict()
                        money8[int(mm.group("day"))] = dd8''', '''                        dd8["_head"] = mm.groupdict()
                        money8[int(mm.group("day"))] = dd8
                        pu = {mq.group("mk"): (mq.group("sub"), mq.group("unc")) for mq in NKS_PLANUNC.finditer(s)}
                        if pu:
                            planunc[int(mm.group("day"))] = pu
                elif s.startswith(NKS_K + "korytarze planu"):
                    mm = NKS_CORR.match(s)
                    if mm:
                        dc = {"big": {}, "kraina": "", "cut": "", "g": None}
                        mb2 = NKS_CORRBIG.search(s)
                        if mb2:
                            for it in re.finditer(r"(?P<a>[A-Za-z_]+)>(?P<b>[A-Za-z_]+) (?P<g>[A-Za-z_]+) (?P<x>[\\d.]+) \\(", mb2.group("body")):
                                dc["big"][(it.group("a"), it.group("b"), it.group("g"))] = float(it.group("x"))
                        mk2 = NKS_CORRKR.search(s)
                        if mk2:
                            dc["kraina"] = mk2.group("body").strip()
                        mu2 = NKS_CORRCUT.search(s)
                        if mu2:
                            dc["cut"] = mu2.group("body").strip()
                        mg2 = NKS_CORRGR.search(s)
                        if mg2:
                            dc["g"] = (float(mg2.group("g")), float(mg2.group("d")))
                        corr[int(mm.group("day"))] = dc
                elif s.startswith(NKS_K + "sterowanie rynku"):
                    mm = NKS_CTL.match(s)
                    if mm:
                        ctl[int(mm.group("day"))] = {(seg, it.group("g")): (int(it.group("c")), int(it.group("u")), int(it.group("d")), float(it.group("k")))
                                                     for seg, body in re.findall(r"\\| (Westeros|Essos):([^|]*)", s) for it in NKS_CTLIT.finditer(body)}
                elif s.startswith(NKS_K + "podloga spichlerza"):
                    mm = NKS_FLOOR.match(s)
                    if mm:
                        floor[int(mm.group("day"))] = (int(mm.group("on")), int(mm.group("n")), mm.group("kf"), int(mm.group("back")))
                elif s.startswith(NKS_K + "wies je swoje"):
                    mm = NKS_OWN.match(s)
                    if mm:
                        own[int(mm.group("day"))] = (int(mm.group("v")), int(mm.group("p")), mm.group("k"), int(mm.group("s")))''')

# ---------------------------------------------------------------- NKS-6: polki + w drodze KS-H
rep('''            for g, (p, c, st) in dd.items():
                a = agg.setdefault(g, [0, 0, None, None, None, None, 0])
                a[0] += p; a[1] += c; a[6] += 1
                if st is not None:''', '''            for g, (p, c, st, tr) in dd.items():
                a = agg.setdefault(g, [0, 0, None, None, None, None, 0])
                a[0] += p; a[1] += c; a[6] += 1
                if st is not None:
                    st = st + (tr or 0)     # KS-R3 (NKS-6): zapas miast rynku + jedzenie w drodze KS-H''')
rep('''        rep.add("NKS-6: zapasy miast rynku (zmiana na dobe / zuzycie dobowe, okno odbioru)", f"poza +-2%: {len(stk_bad)}" + (f" ({', '.join(stk_bad)})" if stk_bad else ""), "<= 2% na dobe",
                (len(stk_bad) == 0) if (n and nd_win >= 7) else None)''', '''        rep.add("NKS-6: zapasy miast rynku + w drodze KS-H (zmiana na dobe / zuzycie dobowe, okno odbioru)", f"poza +-2%: {len(stk_bad)}" + (f" ({', '.join(stk_bad)})" if stk_bad else ""), "<= 2% na dobe",
                (len(stk_bad) == 0) if (n and nd_win >= 7) else None, etap=len(stk_bad) < NKS_R3["nks6"])''')
rep('''        ok5 = (len(bad) == 0) if (n and nd_win >= 7) else None
        rep.add("NKS-5: podaz/zuzycie NA RYNEK w oknie odbioru KS-M (D+22..D+49)", f"{win_txt} ({nd_win} dob z linia 'KS: rynki'): towarow rak x rynek {n}, poza 0.95-1.05 {len(bad)}" + (f" ({', '.join(bad)})" if bad else ""),
                "kazdy towar rak kazdego rynku w 0.95-1.05 (okno < 7 dob - INFO)", ok5)''', '''        ok5 = (len(bad) == 0) if (n and nd_win >= 7) else None
        rep.add("NKS-5: podaz/zuzycie NA RYNEK w oknie odbioru KS-M (D+22..D+49)", f"{win_txt} ({nd_win} dob z linia 'KS: rynki'): towarow rak x rynek {n}, poza 0.95-1.05 {len(bad)}" + (f" ({', '.join(bad)})" if bad else ""),
                "kazdy towar rak kazdego rynku w 0.95-1.05 (okno < 7 dob - INFO; budowy ksiegowane na osade - KS-R3 G7)", ok5, etap=len(bad) < NKS_R3["nks5"])''')
rep('''            rep.add("NKS-7: ceny na rynek (mediana linii okna w pasmie ceny startu +-0.3)", f"linii {len(pr)}: poza pasmem {len(far)}" + (f" ({', '.join(far[:12])})" if far else "")
                    + ("" if start_px else " (bez linii cen startu - pasmo 0.7-1.3)"), "towary rak kazdego rynku w pasmie", len(far) == 0)
            rep.add("NKS-7b: sufit / podloga wzoru ceny", ", ".join(cf) if cf else "<= 20% miast", "zaden towar w > 20% miast", len(cf) == 0 if per_mk else None)''', '''            rep.add("NKS-7: ceny na rynek (mediana linii okna w pasmie ceny startu +-0.3)", f"linii {len(pr)}: poza pasmem {len(far)}" + (f" ({', '.join(far[:12])})" if far else "")
                    + ("" if start_px else " (bez linii cen startu - pasmo 0.7-1.3)"), "towary rak kazdego rynku w pasmie", len(far) == 0, etap=len(far) < NKS_R3["nks7"])
            rep.add("NKS-7b: sufit / podloga wzoru ceny", ", ".join(cf) if cf else "<= 20% miast", "zaden towar w > 20% miast", len(cf) == 0 if per_mk else None, etap=len(cf) < NKS_R3["nks7b"])''')

# ---------------------------------------------------------------- NKS-8c: 1 - kb - kh - kr; Plan bez pokrycia (INFO)
rep('''            if any(v is not None for v in kzm.values()):
                okv = all(v is None or v <= 5.0 for v in kzm.values())
                rep.add("NKS-8c: koszyk niekupiony z braku pieniedzy (projekt 9.4; recenzja 17.10)",
                        f"miasta {mtm:.1f}% budzetu jedzenia; wsie: niekupione {mvm:.1f}% chcianych sztuk, kcal BEZ POKRYCIA (ani kupione, ani robione w domu) "
                        + ", ".join(f"{mk} {v:.1f}%" for mk, v in kzm.items() if v is not None) + " (srednio w oknie)",
                        "miasta <= 5%, wsie kcal bez pokrycia <= 5% na rynku", mtm <= 5.0 and okv)''', '''            if any(v is not None for v in kzm.values()):
                okv = all(v is None or v <= 5.0 for v in kzm.values())
                worse = any(v is not None and v > NKS_R3["nks8c"] for v in kzm.values())
                # KS-R3: bez pokrycia = 1 - kupione - robione w domu - zjedzone z magazynu (linia ZC4 od 17.10 liczy to sama); Plan - INFO
                pu_w = [planunc[d] for d in sorted(planunc) if w0 - 28 <= d <= w1]
                pu_txt = (" | Plan bez pokrycia (INFO, cel <= 3.5%): " + ", ".join(f"{mk} {v[1]}% (zastepcze 8b {v[0]}%)" for mk, v in sorted(pu_w[-1].items()))) if pu_w else ""
                rep.add("NKS-8c: koszyk niekupiony z braku pieniedzy (projekt 9.4; recenzja 17.10; KS-R3: z magazynu wsi)",
                        f"miasta {mtm:.1f}% budzetu jedzenia; wsie: niekupione {mvm:.1f}% chcianych sztuk, kcal BEZ POKRYCIA (ani kupione, ani robione w domu, ani z magazynu) "
                        + ", ".join(f"{mk} {v:.1f}%" for mk, v in kzm.items() if v is not None) + " (srednio w oknie)" + pu_txt,
                        "miasta <= 5%, wsie kcal bez pokrycia (1 - kupione - dom - magazyn) <= 5% na rynku", mtm <= 5.0 and okv, etap=not worse)''')

# ---------------------------------------------------------------- NKS-13: kupione >= 90% zaplaconego z Planu, budzet / wplyw netto 0.85-1.05
rep('''            ww = sum(int(v["w"]) for v in vals); wg = sum(int(v["g"]) for v in vals); wp = sum(int(v["p"]) for v in vals)
            fs = wg / ww if ww > 0 else None
            if fs is not None:
                floor_txt.append(f"{mk} kupione {100 * fs:.0f}% wartosci pelnego koszyka (zaplacony z Planu {100 * wp / ww:.0f}%)")
                if fs < 0.4:
                    floor_ok = False
            inflow = sum(int(v["i"]) for v in vals) / len(vals)
            bp = [int(money8[d][mk]["b"]) for d in p8 if mk in money8[d] and money8[d][mk].get("b") not in (None, "-")]
            if bp and inflow > 0:
                rb = (sum(bp) / len(bp)) / inflow
                bud_txt.append(f"{mk} budzet Planu {sum(bp) / len(bp):.0f} / wplyw do kies {inflow:.0f} zl/d ({rb:.2f})")
                if not (0.75 <= rb <= 1.25):
                    bud_ok = False''', '''            ww = sum(int(v["w"]) for v in vals); wg = sum(int(v["g"]) for v in vals); wp = sum(int(v["p"]) for v in vals)
            fs = wg / ww if ww > 0 else None
            if fs is not None:
                # KS-R3 (G2): wsie kupuja najpierw koszyk zaplacony z Planu - kupione >= 90% wartosci zaplaconej z Planu (przy cenach polki)
                fp = wg / wp if wp > 0 else None
                floor_txt.append(f"{mk} kupione {100 * fs:.0f}% wartosci pelnego koszyka, {100 * fp:.0f}% zaplaconego z Planu (zaplacony {100 * wp / ww:.0f}% pelnego)" if fp is not None else f"{mk} kupione {100 * fs:.0f}% wartosci pelnego koszyka")
                if fp is not None and fp < 0.9:
                    floor_ok = False
            nets = [int(v["inet"]) for v in vals if v.get("inet") not in (None, "")]
            inflow = (sum(nets) / len(nets)) if nets else sum(int(v["i"]) for v in vals) / len(vals)
            bp = [int(money8[d][mk]["b"]) for d in p8 if mk in money8[d] and money8[d][mk].get("b") not in (None, "-")]
            if bp and inflow > 0:
                rb = (sum(bp) / len(bp)) / inflow
                bud_txt.append(f"{mk} budzet Planu {sum(bp) / len(bp):.0f} / wplyw do kies {'netto ' if nets else ''}{inflow:.0f} zl/d ({rb:.2f})")
                if not (0.85 <= rb <= 1.05):
                    bud_ok = False''')
rep('''                "bez luk, kupione >= 40% wartosci pelnego koszyka na rynku, budzet Planu / wplyw do kies 0.75-1.25, kiesy wsi -0.1..+0.5%/d, stac na jedzenie >= 95% miast, potkniecia 0",''',
    '''                "bez luk, kupione >= 90% wartosci zaplaconej z Planu (KS-R3), budzet Planu / wplyw netto do kies 0.85-1.05, kiesy wsi -0.1..+0.5%/d, stac na jedzenie >= 95% miast, potkniecia 0",''')

# ---------------------------------------------------------------- NKS-22..27 (po NKS-20, wewnatrz galezi z wlaczeniem)
rep('''        else:
            rep.add("NKS-20: rozmiar arm_ks", "brak linii 'KS: zapis arm_ks' (bez zapisu w stanie zastosowanym)", "<= 150 KB", None)''', '''        else:
            rep.add("NKS-20: rozmiar arm_ks", "brak linii 'KS: zapis arm_ks' (bez zapisu w stanie zastosowanym)", "<= 150 KB", None)
        nks_r3_rows(rep, logp, a, dd, w0, w1, lastp, ksh, ksh_cor, corr, ctl, floor, own, zc4_k8, start_px)''')

# ---------------------------------------------------------------- NKS-9: tylko doby zastosowane, q95; NKS-9b z KS-H; NKS-28
rep('''    if days_ms:
        v = sorted(x[1] for x in days_ms)
        med = v[len(v) // 2]
        rep.add("NKS-9: koszt doby KS (bez watku roboczego)", f"mediana {med:.2f} ms, max {v[-1]:.2f} ms ({len(v)} dob)", "mediana <= 3 ms", med <= 3.0)
        dm = {d: ms for d, ms, _ in days_ms}
        tot2 = [dm[d] + zc4_hook[d] for d in zc4_hook if d in dm]
        if tot2:
            m2 = nks_med(tot2)
            rep.add("NKS-9b: doba KS + haki ZC4/ZC5 (popyt ceny, slowniki, drabina, wsie, przetwory)", f"mediana {m2:.2f} ms ({len(tot2)} dob)", "<= 10 ms", m2 <= 10.0)''', '''    if days_ms:
        # KS-R3: mediana tylko dob ZASTOSOWANYCH (od wlaczenia) i q95 <= 15 ms; doby Planu - INFO w tekscie
        ap = [x for x in days_ms if on_day is not None and x[0] >= on_day]
        v = sorted(x[1] for x in (ap or days_ms))
        med = v[len(v) // 2]
        q95 = v[min(len(v) - 1, int(0.95 * (len(v) - 1)))]
        rep.add("NKS-9: koszt doby KS (bez watku roboczego; doby zastosowane)", f"mediana {med:.2f} ms, q95 {q95:.2f} ms, max {v[-1]:.2f} ms ({len(v)} dob{' zastosowanych' if ap else ' - bez wlaczenia: wszystkie'})",
                "mediana <= 3 ms, q95 <= 15 ms", med <= 3.0 and q95 <= 15.0)
        dm = {d: ms for d, ms, _ in days_ms}
        kshm = {}
        for d, k in ksh.items():
            try:
                kshm[d + 1] = float(k["ms"])     # linia KS-H drukuje dobe zakonczona (dzien - 1)
            except (TypeError, ValueError):
                pass
        tot2 = [dm[d] + zc4_hook[d] + kshm.get(d, kshm.get(d - 1, 0.0)) for d in zc4_hook if d in dm]
        if tot2:
            m2 = nks_med(tot2)
            rep.add("NKS-9b: doba KS + haki ZC4/ZC5 + kupcy zywnosci KS-H (popyt ceny, slowniki, drabina, wsie, przetwory, wozy)", f"mediana {m2:.2f} ms ({len(tot2)} dob)", "<= 10 ms", m2 <= 10.0)''')
rep('''    rep.add("NKS-10: ERROR KS i potkniecia", f"ERROR {errs}, potkniecia sesji {stum_sess}, haki {stum_hooks}", "0", errs == 0 and stum_sess == 0 and stum_hooks == 0)''',
    '''    rep.add("NKS-10: ERROR KS i potkniecia", f"ERROR {errs}, potkniecia sesji {stum_sess}, haki {stum_hooks}", "0", errs == 0 and stum_sess == 0 and stum_hooks == 0)
    # NKS-28 (KS-R3): czas kupcow zywnosci KS-H (linia doby) - mediana dob czynnych
    km = sorted(float(k["ms"]) for k in ksh.values() if k.get("state") == "CZYNNE")
    if km:
        mk28 = km[len(km) // 2]
        rep.add("NKS-28: czas kupcow zywnosci KS-H (doby czynne)", f"mediana {mk28:.2f} ms, max {km[-1]:.2f} ms ({len(km)} dob)", "mediana <= 1.5 ms", mk28 <= 1.5)
    else:
        rep.add("NKS-28: czas kupcow zywnosci KS-H", "brak linii 'Kupcy zywnosci (KS-H)' w stanie czynnym", "mediana <= 1.5 ms", None)''')
rep('''NKS_ERR = ("ERROR in StartCalibration", "ERROR in SyncData.arm_ks", "ERROR in MineralOnce.KS", "ERROR in StartCalibration.Zc4ApplyAll")''',
    '''NKS_ERR = ("ERROR in StartCalibration", "ERROR in SyncData.arm_ks", "ERROR in MineralOnce.KS", "ERROR in StartCalibration.Zc4ApplyAll", "ERROR in KsFoodCarts", "ERROR in SyncData.arm_ks_kupcy")''')

# ---------------------------------------------------------------- funkcja NKS-22..27 (przed nks_side)
rep('''def nks_side(logp):''', '''def nks_r3_rows(rep, logp, a, dd, w0, w1, lastp, ksh, ksh_cor, corr, ctl, floor, own, zc4_k8, start_px):
    """KS-R3 (17.10, "kupcy i krainy"; StartCalibration.Corridors.cs, KsFoodCarts.cs, StartCalibration.R3.cs): NKS-22 kupcy zywnosci KS-H (linia co dobe od A, reszta towaru
    i zlota 0, zawrocone <= 1% szt., kasa celu za mala <= 5% zamowionych, probka wyceny <= 1%, potkniecia 0), NKS-23 polki zboza miast (ks-miasta.csv: bez zboza < 0.5 dnia
    <= 5% miast, < 2 dni <= 10%, q90 <= 3 x T*, zadne > 30 dni poza krainami bez ujscia), NKS-23b reszta towarow rdzenia (ks-miasta-towary.csv: popyt zaplacony w miastach
    z polka < 0.5 T* <= 10%), NKS-24 korytarze (dowiezione / Plan >= 0.8 na kazdym korytarzu >= 20 szt./d; kraina-tryb - INFO), NKS-25 wies je swoje (<= 8% kcal koszyka na
    rynku, INFO ponad 5%; zadna wies > 30% przez 14 dob), NKS-26 podloga spichlerza (wsi z podloga <= 20% w D+49, kcal podlogi <= 2% potrzeby; zgony - NKS-15), NKS-27
    sterowanie rynku (|czynnik - 1| <= 0.10 w D+49, zmiana <= 7% na Plan)."""
    # NKS-22
    kw = {d: k for d, k in ksh.items() if d >= a}
    if kw:
        days = sorted(kw)
        miss = [d for d in range(a, (lastp or days[-1]) + 1) if d not in kw and d - 1 not in kw and d + 1 not in kw]
        rest_bad = [d for d in days if kw[d].get("gr") not in (None, "0") or kw[d].get("zr") not in (None, "0")]
        bought = sum(int(kw[d]["bought"]) for d in days); back = sum(int(kw[d]["back"]) for d in days)
        ordered = sum(int(kw[d]["ord"]) for d in days); poor = sum(int(kw[d]["poor"]) for d in days)
        se = [float(kw[d]["se"]) for d in days if kw[d].get("se") not in (None, "-")]
        stum = sum(int(kw[d]["stum"]) for d in days)
        sem = max(se) if se else None
        ok22 = not rest_bad and (bought == 0 or back <= 0.01 * bought) and (ordered == 0 or poor <= 0.05 * ordered) and (sem is None or sem <= 1.0) and stum == 0
        rep.add("NKS-22: kupcy zywnosci KS-H (linia co dobe od A, kontrola towaru i zlota, zwroty, kasa celu, probka wyceny)",
                f"linii {len(days)} (od doby {days[0]}), dob bez linii {len(miss)}; kupione {bought} szt., zawrocone {back} ({100.0 * back / max(1, bought):.2f}%), kasa celu za mala {poor} z {ordered} zamowionych "
                f"({100.0 * poor / max(1, ordered):.1f}%), probka wyceny max {sem if sem is not None else '-'}%, doby z reszta != 0: {len(rest_bad)}" + (f" ({rest_bad[:6]})" if rest_bad else "") + f", potkniecia {stum}",
                "reszta towaru i zlota 0 co dobe, zawrocone <= 1% szt., kasa celu za mala <= 5% zamowionych, probka <= 1%, potkniecia 0", ok22)
    else:
        rep.add("NKS-22: kupcy zywnosci KS-H", "brak linii 'Kupcy zywnosci (KS-H)' od doby A", "linia co dobe od A", None)
    # NKS-23 / 23b - pliki sesji
    csvp = session_file_find(logp, "ks-miasta.csv")
    cut_lands = set()
    for d in sorted(corr):
        if d <= w1 and corr[d].get("cut"):
            cut_lands = set(re.findall(r"([A-Za-z_]+) [A-Za-z_]+ x[\\d.]+", corr[d]["cut"]))
    if os.path.isfile(csvp):
        rows = []
        with open(csvp, encoding="utf-8", errors="replace") as fh:
            hdr = None
            for ln in fh:
                f = ln.rstrip("\\n").split(";")
                if hdr is None:
                    hdr = f
                    continue
                if len(f) < len(hdr) - 2:
                    continue
                r = dict(zip(hdr, f))
                try:
                    dy = int(r["dzien"])
                except (KeyError, ValueError):
                    continue
                if w0 <= dy <= w1:
                    rows.append(r)
        if rows:
            last = max(int(r["dzien"]) for r in rows)
            lr = [r for r in rows if int(r["dzien"]) == last]
            vals, lo, lo2, far30 = [], 0, 0, []
            tg = {}
            for r in lr:
                try:
                    dd_ = float(r["zboze_dni_polki"])
                except (KeyError, ValueError):
                    continue
                mk = 0 if r.get("rynek") == "Westeros" else 1
                sp = start_px.get((mk, "grain"))
                T = 7.0 * (sp if sp else 1.0) ** (-1 / 0.6)
                vals.append(dd_ / T)
                if dd_ < 0.5:
                    lo += 1
                if dd_ < 2.0:
                    lo2 += 1
                if dd_ > 30.0 and r.get("kraina", "?") not in cut_lands:
                    far30.append(f"{r.get('miasto', r.get('miasto_id'))} {dd_:.0f} d")
            n = len(vals)
            if n:
                vals.sort()
                q90 = vals[int(0.9 * (n - 1))]
                sh_lo = lo / n
                ok23 = sh_lo <= 0.05 and lo2 / n <= 0.10 and q90 <= 3.0 and not far30
                rep.add("NKS-23: polki zboza miast (ks-miasta.csv, ostatni wiersz okna)", f"doba {last}: miast {n}, < 0.5 dnia {100 * sh_lo:.0f}%, < 2 dni {100 * lo2 / n:.0f}%, q90 {q90:.2f} x T*, > 30 dni poza krainami bez ujscia {len(far30)}"
                        + (f" ({', '.join(far30[:8])})" if far30 else "") + (f" | krainy bez ujscia: {', '.join(sorted(cut_lands))}" if cut_lands else ""),
                        "< 0.5 dnia <= 5%, < 2 dni <= 10%, q90 <= 3 x T*, zadne > 30 dni poza krainami bez ujscia", ok23, etap=sh_lo < NKS_R3["nks23"])
            else:
                rep.add("NKS-23: polki zboza miast", "brak liczb w ks-miasta.csv w oknie", "< 0.5 dnia <= 5%", None)
        else:
            rep.add("NKS-23: polki zboza miast", "brak wierszy ks-miasta.csv w oknie odbioru", "< 0.5 dnia <= 5%", None)
    else:
        rep.add("NKS-23: polki zboza miast", f"brak pliku {csvp}", "< 0.5 dnia <= 5%", None)
    csvg = session_file_find(logp, "ks-miasta-towary.csv")
    if os.path.isfile(csvg):
        agg = {}
        with open(csvg, encoding="utf-8", errors="replace") as fh:
            hdr = None
            for ln in fh:
                f = ln.rstrip("\\n").split(";")
                if hdr is None:
                    hdr = f
                    continue
                r = dict(zip(hdr, f))
                try:
                    dy = int(r["dzien"]); sh = float(r["polka_szt"]); dp = float(r["popyt_zaplacony_szt_d"]); T = float(r["cel_dni"])
                except (KeyError, ValueError):
                    continue
                if not (w0 <= dy <= w1) or dp <= 0 or r.get("towar") == "grain":
                    continue
                a2 = agg.setdefault((r.get("rynek"), r.get("towar")), [0.0, 0.0])
                a2[0] += dp
                if sh < 0.5 * T * dp:
                    a2[1] += dp
        bad = [f"{mk} {g} {100 * v[1] / v[0]:.0f}%" for (mk, g), v in sorted(agg.items()) if v[0] > 0 and v[1] > 0.10 * v[0]]
        rep.add("NKS-23b: reszta towarow rdzenia (ks-miasta-towary.csv): popyt zaplacony w miastach z polka < 0.5 T*", ", ".join(bad) if bad else ("<= 10% kazdego towaru" if agg else "brak wierszy w oknie"),
                "<= 10% popytu zaplaconego (miasta wyciete regula zasiegu - popyt 0)", (len(bad) == 0) if agg else None)
    else:
        rep.add("NKS-23b: reszta towarow rdzenia", f"brak pliku {csvg}", "<= 10%", None)
    # NKS-24 korytarze
    pc = [d for d in sorted(corr) if w0 - 7 <= d <= w1]
    dc = [d for d in sorted(ksh_cor) if w0 <= d <= w1 + 7]
    if pc and dc:
        plan = corr[pc[-1]]["big"]; dl = ksh_cor[dc[-1]]
        bad, nn = [], 0
        for (x, y, g), q in sorted(plan.items(), key=lambda kv: -kv[1]):
            if q < 20:
                continue
            nn += 1
            got = dl.get((x, y, g), 0.0)
            if got < 0.8 * q:
                bad.append(f"{x}>{y} {g} {got:.0f}/{q:.0f}")
        rep.add("NKS-24: korytarze planu (dowiezione KS-H / Plan, korytarze >= 20 szt./d)", f"plan doba {pc[-1]}, dowoz doba {dc[-1]}: korytarzy {nn}, ponizej 0.8 {len(bad)}" + (f" ({', '.join(bad[:10])})" if bad else "")
                + f" | kraina-tryb (INFO): {corr[pc[-1]].get('kraina') or '-'}", ">= 0.8 na kazdym korytarzu >= 20 szt./d", (len(bad) == 0) if nn else None)
    else:
        rep.add("NKS-24: korytarze planu", "brak linii 'KS: korytarze planu' albo korytarzy KS-H w oknie", ">= 0.8", None)
    # NKS-25 wies je swoje
    krw = {mk: [float(zc4_k8[d][mk]["kr"]) for d in zc4_k8 if w0 <= d <= w1 and mk in zc4_k8[d] and zc4_k8[d][mk].get("kr") not in (None, "")] for mk in ("Westeros", "Essos")}
    krm = {mk: (sum(v) / len(v) if v else None) for mk, v in krw.items()}
    ow = [own[d] for d in own if w0 <= d <= w1]
    long14 = max([o[3] for o in ow] + [0])
    if any(v is not None for v in krm.values()):
        mx = max(v for v in krm.values() if v is not None)
        rep.add("NKS-25: wies je swoje (z magazynu, % kcal koszyka na rynku)", ", ".join(f"{mk} {v:.1f}%" for mk, v in krm.items() if v is not None) + f"; wsi > 30% koszyka 14 dob z rzedu (max w oknie) {long14}",
                "<= 8% na rynku (5-8% - INFO), zadna wies > 30% przez 14 dob", (mx <= 8.0 and long14 == 0) if (mx <= 5.0 or mx > 8.0 or long14 > 0) else None)
    else:
        rep.add("NKS-25: wies je swoje", "brak liczb 'z magazynu' w linii ZC4", "<= 8%", None)
    # NKS-26 podloga spichlerza
    fw = [(d, v) for d, v in sorted(floor.items()) if d <= dd + 49]
    if fw:
        d, v = fw[-1]
        sh = v[0] / max(1, v[1])
        try:
            kf = float(v[2])
        except ValueError:
            kf = None
        rep.add("NKS-26: podloga spichlerza (G6)", f"doba {d}: wsi z podloga {v[0]} z {v[1]} ({100 * sh:.0f}%), kcal podlogi {v[2]}% potrzeby, powroty {v[3]}",
                "wsi z podloga <= 20% w D+49, kcal podlogi <= 2% potrzeby (zgony - NKS-15)", (sh <= 0.20 and kf is not None and kf <= 2.0) if d >= dd + 42 else None)
    else:
        rep.add("NKS-26: podloga spichlerza", "brak linii 'KS: podloga spichlerza'", "<= 20% wsi", None)
    # NKS-27 sterowanie rynku
    cw = [d for d in sorted(ctl) if d <= dd + 49 and ctl[d]]
    if cw:
        lastc = ctl[cw[-1]]
        far = [f"{mk} {g} x{v[3]:.3f}" for (mk, g), v in sorted(lastc.items()) if abs(v[3] - 1.0) > 0.10 + 1e-9]
        jump = []
        for i in range(1, len(cw)):
            p0, p1_ = ctl[cw[i - 1]], ctl[cw[i]]
            for k2, v in p1_.items():
                if k2 in p0 and p0[k2][3] > 0 and abs(v[3] / p0[k2][3] - 1.0) > 0.07 + 1e-6:
                    jump.append(f"{k2[0]} {k2[1]} {p0[k2][3]:.3f}->{v[3]:.3f}")
        rep.add("NKS-27: sterowanie rynku (czynnik popytu targu)", f"plan doba {cw[-1]}: poza 0.90-1.10 {len(far)}" + (f" ({', '.join(far[:8])})" if far else "") + f"; skoki > 7% na Plan {len(jump)}" + (f" ({', '.join(jump[:6])})" if jump else ""),
                "|czynnik - 1| <= 0.10 w D+49, zmiana <= 7% na Plan", not far and not jump)
    else:
        rep.add("NKS-27: sterowanie rynku", "brak linii 'KS: sterowanie rynku' z czynnikami (do D+14 czynnik 1.03)", "|czynnik - 1| <= 0.10", None)


def nks_side(logp):''')

io.open(p, 'w', encoding='utf-8', newline='\n').write(s)
print("ok", n0, len(s))
