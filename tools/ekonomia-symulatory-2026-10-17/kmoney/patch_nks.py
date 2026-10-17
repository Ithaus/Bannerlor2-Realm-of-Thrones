import sys
p = sys.argv[1]
s = open(p, encoding='utf-8').read()


def rep(a, b):
    global s
    assert s.count(a) == 1, a[:90]
    s = s.replace(a, b)


rep('''NKS_ZC4AFF = re.compile(''', '''NKS_ZC4PURSE = re.compile(r"kiesy wsi zastosowanych razem (?P<g>-?\\d+) zl")
NKS_MONEY8 = re.compile(r"KS: pieniadze planu \\(krok 8\\) doba (?P<day>-?\\d+) \\| rund punktu stalego (?P<it>\\d+)")
NKS_MONEY8MK = re.compile(r"\\| (?P<mk>Westeros|Essos): wsie (?P<n>\\d+) \\(koszyk ponad budzet (?P<sh>\\d+)\\), sprzedaz wozow (?P<s>-?\\d+), budzet (?P<b>-?\\d+), koszyk (?P<k>-?\\d+), zaplacony (?P<p>-?\\d+) "
                          r"\\((?P<pp>[\\d-]+)%\\), robione w domu (?P<h>-?\\d+), rece w pracy domowej (?P<hw>[\\d.-]+)%, kiesy wsi \\(budzet - zaplacone\\) (?P<dr>[+-]?\\d+); miasta: "
                          r"W (?P<w>[\\d.-]+)% \\([^)]*\\) R (?P<r>[\\d.-]+)% \\([^)]*\\) P (?P<pc>[\\d.-]+)%")
NKS_ZC4AFF = re.compile(''')

rep('''                    ma = NKS_ZC4AFF.search(s)
                    if ma:
                        zc4_aff[d] = (int(ma.group("k")), int(ma.group("n")), ma.group("mt"), ma.group("mv"))
                continue''', '''                    ma = NKS_ZC4AFF.search(s)
                    if ma:
                        zc4_aff[d] = (int(ma.group("k")), int(ma.group("n")), ma.group("mt"), ma.group("mv"))
                    mp8 = NKS_ZC4PURSE.search(s)
                    if mp8:
                        zc4_purse[d] = int(mp8.group("g"))
                continue''')

rep('''    zc4_days, zc4_stum, zc4_v, zc4_hook, zc4_aff = set(), 0, {}, {}, {}''', '''    zc4_days, zc4_stum, zc4_v, zc4_hook, zc4_aff, zc4_purse = set(), 0, {}, {}, {}, {}
    money8 = {}       # doba Planu -> {rynek: dict} (linia "KS: pieniadze planu (krok 8)")''')

rep('''                elif s.startswith(NKS_K + "zapis arm_ks"):''', '''                elif s.startswith(NKS_K + "pieniadze planu"):
                    mm = NKS_MONEY8.match(s)
                    if mm:
                        dd8 = {}
                        for mk8 in NKS_MONEY8MK.finditer(s):
                            dd8[mk8.group("mk")] = mk8.groupdict()
                        money8[int(mm.group("day"))] = dd8
                elif s.startswith(NKS_K + "zapis arm_ks"):''')

rep('''        rep.add("NKS-3b: wyjatki projektu poza pasmem (Plan)", ", ".join(exc) if exc else "brak", "INFO (marchew, jablka, futra, miod, przyprawy, tran, kiel, jaja, skory)", None)
    else:
        rep.add("NKS-3: bilans Planu", "brak linii 'KS: bilans'", "kazdy towar rak w 0.95-1.05", None)''', '''        rep.add("NKS-3b: wyjatki projektu poza pasmem (Plan)", ", ".join(exc) if exc else "brak", "INFO (marchew, jablka, futra, miod, przyprawy, tran, kiel, jaja, skory)", None)
    else:
        rep.add("NKS-3: bilans Planu", "brak linii 'KS: bilans'", "kazdy towar rak w 0.95-1.05", None)
    # NKS-3c (poprawka po autotescie 16.10): krok 8 Planu - popyt tylko zaplacony; przy cenach startu klasy R i P miast placa >= 95% koszyka, kiesy wsi bez spadku
    if money8:
        cand8 = sorted(money8)
        d8 = max([d for d in cand8 if on_day is None or d <= on_day] or cand8)
        bad8, inf8 = [], []
        for mk8 in ("Westeros", "Essos"):
            v8 = money8[d8].get(mk8)
            if not v8:
                bad8.append(f"{mk8} brak liczb")
                continue
            for cl8 in ("r", "pc"):
                try:
                    x8 = float(v8[cl8])
                except ValueError:
                    continue
                if x8 < 95.0:
                    bad8.append(f"{mk8} {'R' if cl8 == 'r' else 'P'} {x8:.1f}%")
            if int(v8["dr"]) < 0:
                bad8.append(f"{mk8} kiesy wsi {v8['dr']}")
            inf8.append(f"{mk8}: wsie zaplacony koszyk {v8['pp']}%, praca domowa {v8['hw']}% rak, W/R/P {v8['w']}/{v8['r']}/{v8['pc']}%")
        rep.add("NKS-3c: pieniadze Planu (krok 8; projekt 9, 11.1)", f"plan doba {d8}: " + "; ".join(inf8) + (f" | poza progiem: {', '.join(bad8)}" if bad8 else ""),
                "klasy R i P >= 95% koszyka przy cenach startu, kiesy wsi (budzet - zaplacone) >= 0", len(bad8) == 0)
    else:
        rep.add("NKS-3c: pieniadze Planu (krok 8)", "brak linii 'KS: pieniadze planu' (log sprzed poprawki po autotescie 16.10)", "R i P >= 95%, kiesy wsi >= 0", None)''')

rep('''            ub = window_market(w0, w1, 2)
            ub_txt = ", ".join(f"{g} {v[0]}/{v[1]}" for g, v in sorted(ub.items()) if g in NKS_MAIN and (v[0] or v[1]))[:300]
        else:
            ub_txt = "brak linii 'KS: rynki'"''', '''            ub = window_market(w0, w1, 2)
            ub_txt = ", ".join(f"{g} {v[0]}/{v[1]}" for g, v in sorted(ub.items()) if g in NKS_MAIN and (v[0] or v[1]))[:300]
            # NKS-5u (poprawka po autotescie 16.10 - bramka, dotad INFO): zuzycie ksiegowane "bez rynku" (ramki bez osady - zakupy wsi bez miasta, partie bez osady)
            # najwyzej 2% zuzycia na rynkach (kazdy towar rak z zuzyciem rynkow >= 1000 szt. w oknie) - inaczej NKS-5 na rynek mierzy podaz wobec czesci zuzycia
            aw0, ae0 = window_market(w0, w1, 0), window_market(w0, w1, 1)
            ub_bad = []
            for g in NKS_MAIN:
                cm = (aw0[g][1] if g in aw0 else 0) + (ae0[g][1] if g in ae0 else 0)
                cb = ub[g][1] if g in ub else 0
                if cm >= 1000 and cb > 0.02 * cm:
                    ub_bad.append(f"{g} {100.0 * cb / cm:.1f}%")
        else:
            ub_txt = "brak linii 'KS: rynki'"
            ub_bad = None''')

rep('''        rep.add("NKS-5u: bez rynku (partie poza osada, zrodla bez ramki) w oknie", ub_txt or "-", "INFO", None)''',
    '''        rep.add("NKS-5u: zuzycie bez rynku (ramki bez osady) w oknie", (ub_txt or "-") + (f" | ponad 2%: {', '.join(ub_bad)}" if ub_bad else ""),
                "zuzycie bez rynku <= 2% zuzycia rynkow (towary rak z >= 1000 szt.)", (len(ub_bad) == 0) if (ub_bad is not None and nd_win >= 7) else None)
        # NKS-5e (poprawka po autotescie 16.10): pierwszy tydzien po rampie (D+14..D+21 = A+28..A+35) - kazdy towar rak kazdego rynku w 0.95-1.05 (cel "w 28 dob od wlaczenia");
        # glowne okno odbioru zostaje D+22..D+49 (NKS-5)
        e0, e1 = dd + 14, dd + 21
        bad_e, n_e, nd_e = [], 0, 0
        for k in (0, 1):
            agg_e = window_market(e0, e1, k)
            nd_e = max([v[6] for v in agg_e.values()] + [nd_e])
            for g in NKS_MAIN:
                if g not in agg_e or agg_e[g][1] <= 0:
                    continue
                n_e += 1
                q_e = agg_e[g][0] / agg_e[g][1]
                if q_e < 0.95 or q_e > 1.05:
                    bad_e.append(f"{mk_names[k]} {g} {q_e:.2f}")
        rep.add("NKS-5e: podaz/zuzycie na rynek w pierwszym tygodniu po rampie (D+14..D+21)", f"doby {e0}..{e1} ({nd_e} dob): towarow rak x rynek {n_e}, poza 0.95-1.05 {len(bad_e)}" + (f" ({', '.join(bad_e)})" if bad_e else ""),
                "kazdy towar rak kazdego rynku w 0.95-1.05 (cel: zbilansowany rynek w 28 dob od wlaczenia)", (len(bad_e) == 0) if (n_e and nd_e >= 7) else None)''')

rep('''        share = vk / vw if vw > 0 else None
        afk = sum(v[0] for v in affw); afn = sum(v[1] for v in affw)
        aff = afk / afn if afn > 0 else None
        rep.add("NKS-13: koszyk ZC4 (linia co dobe, wsie kupuja, stac na jedzenie, potkniecia)",
                (f"linii {len(zc4_days)}, luk {len(zgaps)}; wsie w oknie: chciane {vw} szt., kupione {vk} ({(100 * share):.0f}%) za {vg} zl" if share is not None else f"linii {len(zc4_days)}, luk {len(zgaps)}; wsie w oknie: brak liczb")
                + (f"; miasta stac na cale jedzenie {100 * aff:.1f}%" if aff is not None else "") + f"; potkniecia {zc4_stum}",
                "bez luk, wsie >= 50% chcianego, stac na jedzenie >= 95% miast (ZC4), potkniecia 0",
                (not zgaps and zc4_stum == 0 and (share is None or share >= 0.5) and (aff is None or aff >= 0.95)) if zc4_days else None)''',
    '''        share = vk / vw if vw > 0 else None
        afk = sum(v[0] for v in affw); afn = sum(v[1] for v in affw)
        aff = afk / afn if afn > 0 else None
        # poprawka po autotescie 16.10 (krok 8): "chciane" to koszyk ZAPLACONY z Planu (budzet wsi z utargu wozow) - wsie kupuja >= 95% chcianego (dotad >= 50% pelnej
        # potrzeby, ktorej nikt nie oplacal), kiesy wsi zastosowanych bez spadku w oknie (dryf >= 0; tolerancja szumu dobowego 0.1% kies na dobe)
        pw = sorted((d, g) for d, g in zc4_purse.items() if w0 <= d <= w1)
        drift = None
        if len(pw) >= 2 and pw[-1][0] > pw[0][0]:
            drift = (pw[-1][1] - pw[0][1]) / (pw[-1][0] - pw[0][0])
        drift_ok = drift is None or drift >= -0.001 * max(1, pw[0][1])
        rep.add("NKS-13: koszyk ZC4 (linia co dobe, wsie kupuja koszyk zaplacony, kiesy wsi, stac na jedzenie, potkniecia)",
                (f"linii {len(zc4_days)}, luk {len(zgaps)}; wsie w oknie: chciane (zaplacone z Planu) {vw} szt., kupione {vk} ({(100 * share):.0f}%) za {vg} zl" if share is not None else f"linii {len(zc4_days)}, luk {len(zgaps)}; wsie w oknie: brak liczb")
                + (f"; kiesy wsi {pw[0][1]} -> {pw[-1][1]} zl ({drift:+.0f} zl/d)" if drift is not None else "; kiesy wsi: brak liczb (log sprzed kroku 8)")
                + (f"; miasta stac na cale jedzenie {100 * aff:.1f}%" if aff is not None else "") + f"; potkniecia {zc4_stum}",
                "bez luk, wsie >= 95% koszyka zaplaconego, kiesy wsi bez spadku (>= -0.1%/d), stac na jedzenie >= 95% miast (ZC4), potkniecia 0",
                (not zgaps and zc4_stum == 0 and (share is None or share >= 0.95) and drift_ok and (aff is None or aff >= 0.95)) if zc4_days else None)''')

rep('''                    (towary rak z miesem; 5b wyjatki +-10% Planu; 5c przetwory, 5u bez rynku, 5w swiat, 5d 28 dob po wlaczeniu - INFO), NKS-6 zapasy miast rynku +-2%''',
    '''                    (towary rak z miesem; 5b wyjatki +-10% Planu; 5e pierwszy tydzien po rampie D+14..D+21; 5u zuzycie bez rynku <= 2%; 5c przetwory, 5w swiat,
                    5d 28 dob po wlaczeniu - INFO), NKS-3c pieniadze Planu (krok 8: R i P >= 95%, kiesy wsi >= 0), NKS-6 zapasy miast rynku +-2%''')
rep('''                    (wsie >= 50%, stac na jedzenie >= 95%), NKS-14 przetwory ZC5 (INFO)''',
    '''                    (wsie >= 95% koszyka zaplaconego, kiesy wsi bez spadku, stac na jedzenie >= 95%), NKS-14 przetwory ZC5 (INFO)''')
open(p, 'w', encoding='utf-8', newline='').write(s)
print("ok")
