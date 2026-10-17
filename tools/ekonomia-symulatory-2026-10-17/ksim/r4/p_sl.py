import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from edlib import rep
P = r'C:/Users/GAME/AppData/Local/Temp/claude/C--Users-GAME-Bannerlor2-Realm-of-Thrones/ae222cb7-a7c2-473a-8fc1-1dc1d6f688e3/scratchpad/zb/n-ks/tools/sprawdz_logi.py'
pairs = []
# ---- regexes
pairs.append(('''NKS_R3 = {"nks5": 40, "nks6": 36, "nks7": 45, "nks7b": 11, "nks23": 0.37, "nks8c": 34.2}''',
'''NKS_R3 = {"nks5": 40, "nks6": 36, "nks7": 45, "nks7b": 11, "nks23": 0.37, "nks8c": {"Westeros": 34.2, "Essos": 40.0}}
# recenzja rundy 3: kupcy KS-H - wynik lotow i kasy miast z brakiem; drabina 1 wedlug rynku i wartosc kupiona; rynek wsi BK; korytarze bez sufitu dowozu; bez ujscia;
# czas tabeli dob KS-H w linii doby KS; rozmiar arm_ks_r3 i arm_ks_kupcy
NKS_KSHMAR = re.compile(r"\\| kupcy: dowiezione loty (?P<n>\\d+), wartosc u celu (?P<v>-?\\d+) zl, zaplata \\+ fracht (?P<c>-?\\d+) zl, wynik (?P<r>-?\\d+) zl, mediana marzy (?P<m>-?[\\d.]+|-)%?, ze strata (?P<l>\\d+), pominiete bez zysku (?P<np>\\d+)")
NKS_KSHCOF = re.compile(r"\\| kasy miast z brakiem / zapas kupcow: q10 (?P<q10>[\\d.]+), mediana (?P<qm>[\\d.]+) \\((?P<n>\\d+) miast\\)")
NKS_P1MK = re.compile(r"\\| drabina 1 wedlug rynku \\(chciane/bez towaru/bez kiesy szt\\.\\): Westeros (?P<ww>-?\\d+)/(?P<wg>-?\\d+)/(?P<wm>-?\\d+), Essos (?P<ew>-?\\d+)/(?P<eg>-?\\d+)/(?P<em>-?\\d+)")
NKS_P1ZL = re.compile(r"\\| drabina 1 kupiona wartosc: Westeros (?P<w>-?\\d+) zl, Essos (?P<e>-?\\d+) zl")
NKS_BKV = re.compile(r"\\| rynek wsi BK \\([^)]*\\): Westeros (?P<w>-?\\d+) zl w (?P<wn>\\d+) wsiach, Essos (?P<e>-?\\d+) zl w (?P<en>\\d+) wsiach")
NKS_CORRFREE = re.compile(r"\\| korytarze bez sufitu dowozu >= 20 szt\\./d:(?P<body>[^|]*)")
NKS_CORRNOOUT = re.compile(r"\\| bez ujscia \\([^)]*\\): (?P<body>[^|]*)")
NKS_DAYTAB = re.compile(r"\\| tabela dob KS-H (?P<t>[\\d.]+) ms")
NKS_SAVEKH = re.compile(r"KS: zapis arm_ks_kupcy doba (?P<day>-?\\d+) \\| (?P<kb>[\\d.]+) KB")
NKS_SAVER3 = re.compile(r"arm_ks_r3 \\(stan rundy 3, osobny klucz\\) (?P<kb>[\\d.]+) KB, razem (?P<tot>[\\d.]+) KB")'''))
# ---- NKS-22 strict + state, merchants margin and coffers
pairs.append(('''    kw = {d: k for d, k in ksh.items() if d >= a}
    if kw:
        days = sorted(kw)
        miss = [d for d in range(a, (lastp or days[-1]) + 1) if d not in kw and d - 1 not in kw and d + 1 not in kw]''',
'''    kw = {d: k for d, k in ksh.items() if d >= a}
    if kw:
        days = sorted(kw)
        # recenzja rundy 3 (drobne): linia KAZDEJ doby od A (bez tolerancji +-1) i stan CZYNNE (klucz wylaczony - NIE)
        miss = [d for d in range(a, (lastp or days[-1]) + 1) if d not in kw]
        off_days = [d for d in days if kw[d].get("state") != "CZYNNE"]'''))
pairs.append(('''        ok22 = not rest_bad and (bought == 0 or back <= 0.01 * bought) and (ordered == 0 or poor <= 0.05 * ordered) and (sem is None or sem <= 1.0) and stum == 0
        rep.add("NKS-22: kupcy zywnosci KS-H (linia co dobe od A, kontrola towaru i zlota, zwroty, kasa celu, probka wyceny)",
                f"linii {len(days)} (od doby {days[0]}), dob bez linii {len(miss)}; kupione {bought} szt., zawrocone {back} ({100.0 * back / max(1, bought):.2f}%), kasa celu za mala {poor} z {ordered} zamowionych "
                f"({100.0 * poor / max(1, ordered):.1f}%), probka wyceny max {sem if sem is not None else '-'}%, doby z reszta != 0: {len(rest_bad)}" + (f" ({rest_bad[:6]})" if rest_bad else "") + f", potkniecia {stum}",
                "reszta towaru i zlota 0 co dobe, zawrocone <= 1% szt., kasa celu za mala <= 5% zamowionych, probka <= 1%, potkniecia 0", ok22)''',
'''        ok22 = not rest_bad and not miss and not off_days and (bought == 0 or back <= 0.01 * bought) and (ordered == 0 or poor <= 0.05 * ordered) and (sem is None or sem <= 1.0) and stum == 0
        rep.add("NKS-22: kupcy zywnosci KS-H (linia co dobe od A, stan CZYNNE, kontrola towaru i zlota, zwroty, kasa celu, probka wyceny)",
                f"linii {len(days)} (od doby {days[0]}), dob bez linii {len(miss)}" + (f" ({miss[:6]})" if miss else "") + f", dob nie CZYNNE {len(off_days)}; kupione {bought} szt., zawrocone {back} ({100.0 * back / max(1, bought):.2f}%), kasa celu za mala {poor} z {ordered} zamowionych "
                f"({100.0 * poor / max(1, ordered):.1f}%), probka wyceny max {sem if sem is not None else '-'}%, doby z reszta != 0: {len(rest_bad)}" + (f" ({rest_bad[:6]})" if rest_bad else "") + f", potkniecia {stum}",
                "linia kazdej doby od A w stanie CZYNNE, reszta towaru i zlota 0 co dobe, zawrocone <= 1% szt., kasa celu za mala <= 5% zamowionych, probka <= 1%, potkniecia 0", ok22)
        # NKS-22b (recenzja rundy 3, wazne): wynik kupcow - mediana dziennych median marzy lotow dowiezionych w oknie >= 0
        mw = [kw[d] for d in days if w0 <= d <= w1 and kw[d].get("mar_m") not in (None, "-")]
        if mw:
            med_m = nks_med([float(x["mar_m"]) for x in mw]); res_sum = sum(int(x["mar_r"]) for x in mw); loss = sum(int(x["mar_l"]) for x in mw); lots = sum(int(x["mar_n"]) for x in mw)
            nop = sum(int(x["mar_np"]) for x in mw)
            rep.add("NKS-22b: wynik kupcow KS-H (wartosc u celu - zaplata - fracht, loty dowiezione w oknie)", f"mediana marzy {med_m:.1f}%, wynik {res_sum} zl, lotow {lots} (ze strata {loss}), pominiete bez zysku {nop}",
                    "mediana marzy >= 0", med_m >= 0.0)
        else:
            rep.add("NKS-22b: wynik kupcow KS-H", "brak liczb 'kupcy: dowiezione loty' w oknie", "mediana marzy >= 0", None)
        # NKS-16b (recenzja rundy 3, wazne): kasy miast z pusta polka (cele KS-H) wobec zapasu kupcow - q10 dzienny, mediana w oknie >= 0.5 (KS-H nie schodzi ponizej polowy zapasu)
        cw16 = [float(kw[d]["cof_q10"]) for d in days if w0 <= d <= w1 and kw[d].get("cof_q10") not in (None, "")]
        if cw16:
            m16 = nks_med(cw16)
            rep.add("NKS-16b: kasy miast z brakiem wobec zapasu kupcow (linia KS-H, q10 dzienny)", f"mediana q10 w oknie {m16:.2f}, najmniej {min(cw16):.2f} ({len(cw16)} dob)", "mediana q10 >= 0.5", m16 >= 0.5)
        else:
            rep.add("NKS-16b: kasy miast z brakiem wobec zapasu kupcow", "brak liczb w linii KS-H", "q10 >= 0.5", None)'''))
# ---- NKS-23 exemption + every weekly row
pairs.append(('''    cut_lands = set()
    for d in sorted(corr):
        if d <= w1 and corr[d].get("cut"):
            cut_lands = set(re.findall(r"([A-Za-z_]+) [A-Za-z_]+ x[\\d.]+", corr[d]["cut"]))''',
'''    # recenzja rundy 3 (drobne): zwolnione z "> 30 dni" tylko krainy z nadwyzka zboza BEZ UJSCIA w linii korytarzy (wojna albo poza zasiegiem) - nie krainy z cieciem nadmiaru
    cut_lands = set()
    for d in sorted(corr):
        if d <= w1 and corr[d].get("noout"):
            cut_lands = set(re.findall(r"[WE] ([A-Za-z_]+) grain \\d+%", corr[d]["noout"]))'''))
pairs.append(('''        if rows:
            last = max(int(r["dzien"]) for r in rows)
            lr = [r for r in rows if int(r["dzien"]) == last]
            vals, lo, lo2, far30 = [], 0, 0, []
            tg = {}
            for r in lr:''', '''        if rows:
          # recenzja rundy 3 (drobne): kazdy tygodniowy wiersz okna - wynik najgorszego (dotad tylko ostatni)
          worst = None
          for last in sorted(set(int(r["dzien"]) for r in rows)):
            lr = [r for r in rows if int(r["dzien"]) == last]
            vals, lo, lo2, far30 = [], 0, 0, []
            tg = {}
            for r in lr:'''))
pairs.append(('''            n = len(vals)
            if n:
                vals.sort()
                q90 = vals[int(0.9 * (n - 1))]
                sh_lo = lo / n
                ok23 = sh_lo <= 0.05 and lo2 / n <= 0.10 and q90 <= 3.0 and not far30
                rep.add("NKS-23: polki zboza miast (ks-miasta.csv, ostatni wiersz okna)", f"doba {last}: miast {n}, < 0.5 dnia {100 * sh_lo:.0f}%, < 2 dni {100 * lo2 / n:.0f}%, q90 {q90:.2f} x T*, > 30 dni poza krainami bez ujscia {len(far30)}"
                        + (f" ({', '.join(far30[:8])})" if far30 else "") + (f" | krainy bez ujscia: {', '.join(sorted(cut_lands))}" if cut_lands else ""),
                        "< 0.5 dnia <= 5%, < 2 dni <= 10%, q90 <= 3 x T*, zadne > 30 dni poza krainami bez ujscia", ok23, etap=sh_lo < NKS_R3["nks23"])
            else:
                rep.add("NKS-23: polki zboza miast", "brak liczb w ks-miasta.csv w oknie", "< 0.5 dnia <= 5%", None)''',
'''            n = len(vals)
            if n:
                vals.sort()
                q90 = vals[int(0.9 * (n - 1))]
                sh_lo = lo / n
                ok23 = sh_lo <= 0.05 and lo2 / n <= 0.10 and q90 <= 3.0 and not far30
                score = (0 if ok23 else 1, sh_lo + lo2 / n + q90 / 30.0 + len(far30))
                if worst is None or score > worst[0]:
                    worst = (score, last, n, sh_lo, lo2, q90, far30, ok23)
          if worst is not None:
                _, last, n, sh_lo, lo2, q90, far30, ok23 = worst
                rep.add("NKS-23: polki zboza miast (ks-miasta.csv, najgorszy tygodniowy wiersz okna)", f"doba {last}: miast {n}, < 0.5 dnia {100 * sh_lo:.0f}%, < 2 dni {100 * lo2 / n:.0f}%, q90 {q90:.2f} x T*, > 30 dni poza krainami bez ujscia {len(far30)}"
                        + (f" ({', '.join(far30[:8])})" if far30 else "") + (f" | krainy bez ujscia (zboze): {', '.join(sorted(cut_lands))}" if cut_lands else ""),
                        "< 0.5 dnia <= 5%, < 2 dni <= 10%, q90 <= 3 x T*, zadne > 30 dni poza krainami bez ujscia (linia korytarzy)", ok23, etap=sh_lo < NKS_R3["nks23"])
          else:
                rep.add("NKS-23: polki zboza miast", "brak liczb w ks-miasta.csv w oknie", "< 0.5 dnia <= 5%", None)'''))
# ---- NKS-24: free flow after D
pairs.append(('''    if pc and dc:
        plan = corr[pc[-1]]["big"]; dl = ksh_cor[dc[-1]]''', '''    if pc and dc:
        # recenzja rundy 3 (wazne): po D wzorzec bez sufitu dowozu KS-H (budzet kasy importera) - dotad Plan po D <= 1.2 x dowoz z definicji (wynik kolowy)
        free = corr[pc[-1]].get("free") or {}
        plan = free if free else corr[pc[-1]]["big"]; dl = ksh_cor[dc[-1]]'''))
pairs.append(('''        rep.add("NKS-24: korytarze planu (dowiezione KS-H / Plan, korytarze >= 20 szt./d)", f"plan doba {pc[-1]}, dowoz doba {dc[-1]}: korytarzy {nn}, ponizej 0.8 {len(bad)}" + (f" ({', '.join(bad[:10])})" if bad else "")''',
'''        rep.add("NKS-24: korytarze planu (dowiezione KS-H / przewoz Planu " + ("bez sufitu dowozu" if free else "(przed D)") + ", korytarze >= 20 szt./d)", f"plan doba {pc[-1]}, dowoz doba {dc[-1]}: korytarzy {nn}, ponizej 0.8 {len(bad)}" + (f" ({', '.join(bad[:10])})" if bad else "")'''))
# ---- parse: corridor free list and no-outlet
pairs.append(('''                        mg2 = NKS_CORRGR.search(s)
                        if mg2:
                            dc["g"] = (float(mg2.group("g")), float(mg2.group("d")))''', '''                        mg2 = NKS_CORRGR.search(s)
                        if mg2:
                            dc["g"] = (float(mg2.group("g")), float(mg2.group("d")))
                        mf2 = NKS_CORRFREE.search(s)
                        if mf2:
                            dc["free"] = {(it.group("a"), it.group("b"), it.group("g")): float(it.group("x")) for it in re.finditer(r"(?P<a>[A-Za-z_]+)>(?P<b>[A-Za-z_]+) (?P<g>[A-Za-z_]+) (?P<x>[\\d.]+) \\(", mf2.group("body"))}
                        mo3 = NKS_CORRNOOUT.search(s)
                        if mo3:
                            dc["noout"] = mo3.group("body").strip()'''))
# ---- parse: KS-H extra numbers
pairs.append(('''                mm = NKS_KSH.match(s)
                if mm:
                    ksh[int(mm.group("day"))] = mm.groupdict()''', '''                mm = NKS_KSH.match(s)
                if mm:
                    ksh[int(mm.group("day"))] = mm.groupdict()
                    mmr = NKS_KSHMAR.search(s)
                    if mmr:
                        ksh[int(mm.group("day"))].update({"mar_n": mmr.group("n"), "mar_r": mmr.group("r"), "mar_m": mmr.group("m"), "mar_l": mmr.group("l"), "mar_np": mmr.group("np")})
                    mcf = NKS_KSHCOF.search(s)
                    if mcf:
                        ksh[int(mm.group("day"))]["cof_q10"] = mcf.group("q10")'''))
# ---- parse: ZC4 pass1 per market, value, BK
pairs.append(('''                    mq1 = NKS_P1.search(s)
                    if mq1:
                        p1[d] = (int(mq1.group("w")), int(mq1.group("ng")), int(mq1.group("nm")))''', '''                    mq1 = NKS_P1.search(s)
                    if mq1:
                        p1[d] = (int(mq1.group("w")), int(mq1.group("ng")), int(mq1.group("nm")))
                    mq2 = NKS_P1MK.search(s)
                    if mq2:
                        p1mk[d] = {"Westeros": (int(mq2.group("ww")), int(mq2.group("wg")), int(mq2.group("wm"))), "Essos": (int(mq2.group("ew")), int(mq2.group("eg")), int(mq2.group("em")))}
                    mq3 = NKS_P1ZL.search(s)
                    if mq3:
                        p1zl[d] = {"Westeros": int(mq3.group("w")), "Essos": int(mq3.group("e"))}
                    mq4 = NKS_BKV.search(s)
                    if mq4:
                        bkv[d] = {"Westeros": int(mq4.group("w")), "Essos": int(mq4.group("e"))}'''))
pairs.append(('''    p1 = {}            # doba -> (chciane, bez towaru, bez kiesy) drabina 1 wsi''', '''    p1 = {}            # doba -> (chciane, bez towaru, bez kiesy) drabina 1 wsi
    p1mk = {}          # recenzja rundy 3: doba -> {rynek: (chciane, bez towaru, bez kiesy)}
    p1zl = {}          # doba -> {rynek: zl kupione w drabinie 1}
    bkv = {}           # doba -> {rynek: zl rynku wsi BK u wsi z koszykiem ZC4}
    daytab = {}        # doba -> ms tabeli dob KS-H (linia doby KS)
    saves_kh, saves_r3 = [], []'''))
# ---- parse: KS day line table ms, save sizes
pairs.append(('''                    mm = NKS_DAY.match(s)
                    if mm:
                        days_ms.append((int(mm.group("day")), float(mm.group("ms")), int(mm.group("b"))))''', '''                    mm = NKS_DAY.match(s)
                    if mm:
                        days_ms.append((int(mm.group("day")), float(mm.group("ms")), int(mm.group("b"))))
                        mtb = NKS_DAYTAB.search(s)
                        if mtb:
                            daytab[int(mm.group("day"))] = float(mtb.group("t"))'''))
pairs.append(('''                    mm = NKS_SAVE.match(s)
                    if mm:
                        saves.append((int(mm.group("day")), float(mm.group("kb")), int(mm.group("n"))))''', '''                    mm = NKS_SAVE.match(s)
                    if mm:
                        saves.append((int(mm.group("day")), float(mm.group("kb")), int(mm.group("n"))))
                        mr3 = NKS_SAVER3.search(s)
                        if mr3:
                            saves_r3.append((int(mm.group("day")), float(mr3.group("kb")), float(mr3.group("tot"))))
                    mk3 = NKS_SAVEKH.match(s)
                    if mk3:
                        saves_kh.append((int(mk3.group("day")), float(mk3.group("kb"))))'''))
# ---- NKS-8c per market baselines
pairs.append(('''                worse = any(v is not None and v > NKS_R3["nks8c"] for v in kzm.values())''',
'''                worse = any(v is not None and v > NKS_R3["nks8c"].get(mk, 34.2) for mk, v in kzm.items())   # recenzja rundy 3: baza rundy 3 wedlug rynku (W 34.2, E 40.0)'''))
# ---- NKS-13: pass-1 bought value, budget/inflow INFO
pairs.append(('''            ww = sum(int(v["w"]) for v in vals); wg = sum(int(v["g"]) for v in vals); wp = sum(int(v["p"]) for v in vals)
            fs = wg / ww if ww > 0 else None
            if fs is not None:
                # KS-R3 (G2): wsie kupuja najpierw koszyk zaplacony z Planu - kupione >= 90% wartosci zaplaconej z Planu (przy cenach polki)
                fp = wg / wp if wp > 0 else None''', '''            ww = sum(int(v["w"]) for v in vals); wg = sum(int(v["g"]) for v in vals); wp = sum(int(v["p"]) for v in vals)
            fs = wg / ww if ww > 0 else None
            # recenzja rundy 3 (wazne): kupione z DRABINY 1 (koszyk zaplacony) wobec zaplaconego z Planu - drabina 2 (z oszczednosci) nie zakrywa braku drabiny 1
            w1zl = [p1zl[d][mk] for d in k8w if d in p1zl and mk in p1zl[d]]
            if w1zl and len(w1zl) == len(vals):
                wg = sum(w1zl)
            if fs is not None:
                # KS-R3 (G2): wsie kupuja najpierw koszyk zaplacony z Planu - kupione (drabina 1) >= 90% wartosci zaplaconej z Planu (przy cenach polki)
                fp = wg / wp if wp > 0 else None'''))
pairs.append(('''                if not (0.85 <= rb <= 1.05):
                    bud_ok = False''', '''                if not (0.85 <= rb <= 1.05):
                    bud_txt[-1] += " (INFO - spojnosc: alfa netto liczona z tego wplywu)"'''))
pairs.append(('''                "bez luk, kupione >= 90% wartosci zaplaconej z Planu (KS-R3), budzet Planu / wplyw netto do kies 0.85-1.05, kiesy wsi -0.1..+0.5%/d, stac na jedzenie >= 95% miast, potkniecia 0",''',
'''                "bez luk, kupione w drabinie 1 >= 90% wartosci zaplaconej z Planu, kiesy wsi -0.1..+0.5%/d, stac na jedzenie >= 95% miast, potkniecia 0 (budzet Planu / wplyw netto - INFO, spojnosc)",'''))
# NKS-13b pass 1 per market + NKS-29 BK market: insert after NKS-13 row
pairs.append(('''        # NKS-7e (recenzja 17.10): kontrola wczesna D+7 - wiekszosc cen poza pasmem = bieg przerwac (wynik NKS-7 znany z gory)''',
'''        # NKS-13b (recenzja rundy 3, wazne): drabina 1 (koszyk zaplacony z Planu) wedlug rynku - bez towaru i bez kiesy to miary bledu
        pw1 = [p1mk[d] for d in p1mk if w0 <= d <= w1]
        if pw1:
            txt13, ok13 = [], True
            for mk in ("Westeros", "Essos"):
                wn = sum(x[mk][0] for x in pw1); ng = sum(x[mk][1] for x in pw1); nm = sum(x[mk][2] for x in pw1)
                if wn <= 0:
                    continue
                txt13.append(f"{mk} chciane {wn} szt., bez towaru {100.0 * ng / wn:.1f}%, bez kiesy {100.0 * nm / wn:.1f}%")
                if ng > 0.05 * wn or nm > 0.05 * wn:
                    ok13 = False
            rep.add("NKS-13b: drabina 1 wsi (koszyk zaplacony z Planu) wedlug rynku", "; ".join(txt13) if txt13 else "brak", "bez towaru <= 5% i bez kiesy <= 5% chcianych sztuk na rynku", ok13 if txt13 else None)
        else:
            rep.add("NKS-13b: drabina 1 wsi wedlug rynku", "brak liczb 'drabina 1 wedlug rynku' w linii ZC4", "<= 5%", None)
        # NKS-29 (recenzja rundy 3, wazne): rynek wsi BK u wsi z koszykiem ZC4 <= 2% wplywu do kies wsi na rynku
        bw = [d for d in bkv if w0 <= d <= w1]
        if bw and k8w:
            txt29, ok29 = [], True
            for mk in ("Westeros", "Essos"):
                bz = sum(bkv[d][mk] for d in bw); inf = sum(int(zc4_k8[d][mk]["i"]) for d in k8w if mk in zc4_k8[d])
                if inf <= 0:
                    continue
                txt29.append(f"{mk} {bz} zl / wplyw {inf} zl ({100.0 * bz / inf:.2f}%)")
                if bz > 0.02 * inf:
                    ok29 = False
            rep.add("NKS-29: rynek wsi BK u wsi z koszykiem ZC4 (bez jedzenia, ponad 7 dob budzetu)", "; ".join(txt29) if txt29 else "brak", "<= 2% wplywu do kies wsi na rynku", ok29 if txt29 else None)
        else:
            rep.add("NKS-29: rynek wsi BK u wsi z koszykiem ZC4", "brak liczb w linii ZC4", "<= 2%", None)
        # NKS-7e (recenzja 17.10): kontrola wczesna D+7 - wiekszosc cen poza pasmem = bieg przerwac (wynik NKS-7 znany z gory)'''))
# ---- NKS-20 incl. r3 and kupcy
pairs.append(('''        if saves:
            mx = max(saves, key=lambda x: x[1])
            rep.add("NKS-20: rozmiar arm_ks (projekt 12)", f"najwiekszy {mx[1]:.1f} KB ({mx[2]} paczek SaveText, doba {mx[0]}), zapisow {len(saves)}", "<= 150 KB", mx[1] <= 150.0)''',
'''        if saves:
            mx = max(saves, key=lambda x: x[1])
            # recenzja rundy 3 (drobne): arm_ks + arm_ks_r3 (osobny klucz stanu rundy 3) <= 150 KB; arm_ks_kupcy (wozy KS-H w drodze) <= 64 KB
            tot = max([x[2] for x in saves_r3] + [mx[1]])
            kh = max([x[1] for x in saves_kh] + [0.0])
            rep.add("NKS-20: rozmiar arm_ks + arm_ks_r3 (projekt 12) i arm_ks_kupcy", f"arm_ks najwiekszy {mx[1]:.1f} KB ({mx[2]} paczek SaveText, doba {mx[0]}), z arm_ks_r3 razem {tot:.1f} KB, arm_ks_kupcy najwiekszy {kh:.1f} KB, zapisow {len(saves)}",
                    "arm_ks + arm_ks_r3 <= 150 KB, arm_ks_kupcy <= 64 KB", tot <= 150.0 and kh <= 64.0)'''))
# ---- NKS-9b: same day and days table
pairs.append(('''        kshm = {}
        for d, k in ksh.items():
            try:
                kshm[d + 1] = float(k["ms"])     # linia KS-H drukuje dobe zakonczona (dzien - 1)
            except (TypeError, ValueError):
                pass
        tot2 = [dm[d] + zc4_hook[d] + kshm.get(d, kshm.get(d - 1, 0.0)) for d in zc4_hook if d in dm]''',
'''        kshm = {}
        for d, k in ksh.items():
            try:
                kshm[d] = float(k["ms"])         # recenzja rundy 3 (drobne): linia KS-H i linia doby KS drukuja te sama dobe (dzien - 1) w tym samym ticku
            except (TypeError, ValueError):
                pass
        tot2 = [dm[d] + zc4_hook[d] + kshm.get(d, 0.0) + daytab.get(d, 0.0) for d in zc4_hook if d in dm]   # + tabela dob KS-H (MapDistanceModel)'''))
# ---- final: ETAP counted as not passed
pairs.append(('''        rep.add("NKS-21: popyt miast w nowej monecie - modele", "brak linii HistoricalPrices", "= baza", None)
    rep.show()
    return 0''', '''        rep.add("NKS-21: popyt miast w nowej monecie - modele", "brak linii HistoricalPrices", "= baza", None)
    rep.show()
    # recenzja rundy 3 (wazne): do odbioru i do raportu dla Jeffa ETAP = NIE PRZESZLO (ETAP mowi tylko "lepiej niz runda 3" - pasma 95-105% nie spelnia)
    n_fail = sum(1 for r in rep.rows if r[3] in ("NIE", "ETAP", "DECYZJA"))
    print(f"-- ODBIOR nks: nie przeszlo {n_fail} z {len(rep.rows)} (NIE + ETAP + DECYZJA; ETAP = lepiej niz runda 3, ale prog niespelniony)")
    return 0'''))
rep(P, pairs)
