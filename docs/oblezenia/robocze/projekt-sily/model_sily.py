# -*- coding: utf-8 -*-
# PROJEKT-SILY (14.10): szacunek skutku regul S1-S6 na 113 decyzjach o szturmie fin30 (linie O0).
# Tylko odczyt: robocze\sily\okup-O0-fin30.tsv, robocze\sily\zdobycia-szczegoly.tsv, robocze\sily\bitwy-pod-murami.tsv,
# tabele fin30 ludzie-regiony.csv (zaloga, milicja*, ludzie d1). Wynik: stdout + model-wyniki.tsv obok skryptu.
# Uruchom: python -I model_sily.py
import csv, math, os, statistics as st, sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROB = os.path.dirname(HERE)
TAB = r"D:\Backup-Bannerlord\logs\2026-10-14-noc\tabele-06-01-15"
D0 = 108835

def num(x, d=0.0):
    try:
        return float(x)
    except Exception:
        return d

# ---------------------------------------------------------------- dane
o0 = list(csv.DictReader(open(os.path.join(ROB, "sily", "okup-O0-fin30.tsv"), encoding="utf-8"), delimiter="\t"))
wyn = list(csv.DictReader(open(os.path.join(ROB, "mody", "fin30-decyzje-szturmu-O0-wynik.csv"), encoding="utf-8"), delimiter=";"))
wmap = {(r["dzien"], r["osada"]): r["wynik_w_10_dobach"] for r in wyn}
zd = list(csv.DictReader(open(os.path.join(ROB, "sily", "zdobycia-szczegoly.tsv"), encoding="utf-8"), delimiter="\t"))
bpm = list(csv.DictReader(open(os.path.join(ROB, "sily", "bitwy-pod-murami.tsv"), encoding="utf-8"), delimiter="\t"))

# ludzie-regiony: zaloga / milicja* wedlug (dzien, region) tylko dla potrzebnych regionow; ludzie d1 wszystkich twierdz
need = set(r["osada_id"] for r in o0)
garmil = {}
people1 = {}
kind1 = {}
with open(os.path.join(TAB, "ludzie-regiony.csv"), encoding="utf-8") as f:
    rd = csv.reader(f, delimiter=";")
    h = next(rd); ix = {k: i for i, k in enumerate(h)}
    for row in rd:
        d = row[ix["dzien"]]; rid = row[ix["region_id"]]
        if d == "108836":
            people1[rid] = num(row[ix["ludzie"]]); kind1[rid] = row[ix["rodzaj"]]
        if rid in need:
            garmil[(int(d), rid)] = (num(row[ix["zaloga"]]), num(row[ix["milicja"]]))

# czynnik ludzi osady wzgledem sredniej rodzaju (przyblizenie GL4 c) [S]
mean_k = {}
for k in ("miasto", "zamek"):
    v = [people1[r] for r in people1 if kind1[r] == k]
    mean_k[k] = sum(v) / len(v)
PLO, PHI = 0.9, 1.4
def pfac(rid, lo=None, hi=None):
    lo = PLO if lo is None else lo; hi = PHI if hi is None else hi
    if rid not in people1:
        return 1.0
    c = people1[rid] / mean_k[kind1[rid]]
    return min(hi, max(lo, math.sqrt(c)))

# ---------------------------------------------------------------- symulacja szturmu (wzory gry, wartosci oczekiwane) [K DefaultCombatSimulationModel.cs:241-281, SimulateHit :5-14, MapEventSide.cs:970-1000]
HP = 120.0
def assault(A, apow, D, dpow, adv, town, walls=True, maxh=24 * 60):
    thr = 100 if town else 30
    q = apow / dpow
    pA = 0.75 * 40 * q ** 0.7 * 0.9 / HP          # cios oblegajacego wylacza obronce (oblegajacy -10% w szturmie)
    pD = 0.75 * 40 * q ** -0.7 / HP
    A0 = A; h = 0
    while D > 0.5 and A > 0.5 and h < maxh:
        if walls and D > thr:
            Ha = 2 * round(1.5 + D ** 0.3)
            Hd = 2 * round(0.5 + max(1 + D ** 0.3 * 0.7 * adv, (D + 1) / (A + 1)))
        elif D <= 10:
            Hd = max(round(min(A * 3, D * 0.3)), 1); Ha = max(round(min(D * 3, A * 0.3)), 1)
        else:
            Hd = round(min(A * 2, D ** 0.6)); Ha = round(min(D * 2, A ** 0.6))
        dD = min(D, Ha * pA); dA = min(A, Hd * pD)
        D -= dD; A -= dA; h += 1
    win = D <= 0.5
    return win, h / 24.0, A0 - A

GPOW = 1.13   # sila czlowieka zalogi tier 2.5 [K DefaultMilitaryPowerModel.cs:244-253; P tier_zalog 2.3-2.6 balans-krolestw.csv]
MPOW = 0.96   # milicjant tier 2

zdmap = {}
for r in zd:
    zdmap[r["osada"]] = zdmap.get(r["osada"], []) + [r]
so = [r for r in bpm if r["typ"] == "SiegeOutside" and r["wynik"] == "wygrywa atakujacy"]

def run(base_castle, base_town, capw=2.0, vill=0.0, s6=False, start_thr=2.8, fill=1.0, label="", czas=False):
    out = []
    for r in o0:
        dd = int(r["d"]) + D0
        town = r["rodzaj"] == "miasto"
        rid = r["osada_id"]
        cap = "stolica" in r["wynik"]
        w10 = wmap.get((str(dd), rid), "?")
        apow = num(r["obl_sila"]); dpow = num(r["obr_sila"]); adv = num(r["przewaga"])
        amen = num(r["ludzi_druzyn"])
        gm = garmil.get((dd - 1, rid)) or garmil.get((dd, rid)) or (0.0, 0.0)
        gar, mil = gm
        N = (base_town if town else base_castle) * (capw if cap else 1.0) * pfac(rid) * fill
        addg = max(0.0, N - gar) if base_castle > 0 else 0.0
        addm = vill * mil
        dpow2 = dpow + addg * GPOW + addm * MPOW
        mury = num(r["mury"]); mury2 = mury * dpow / dpow2 if dpow2 > 0 else mury
        ratio2 = apow / dpow2 if dpow2 > 0 else 99
        # czy oblezenie w ogole by sie zaczelo (prog wyboru celu; kalibracja: najmniejszy zmierzony stosunek 2.82) [K DefaultTargetScoreCalculatingModel.cs:226-243]
        started = ratio2 >= start_thr
        # okup (SiegeRansom: mury <= 1 obronca nie placi, 1-1.5 okup, >= 1.5 AI nie bierze; stolica wykluczona; brak monety)
        cant_pay = "doba zoldu" in r["wynik"]
        res = None; days = None; lossA = 0.0
        if not started:
            res = "nie zaczete"
        elif w10 == "inne":
            res = "inne"
        else:
            if (not cap) and (not cant_pay) and 1.0 < mury2 < 1.5:
                res = "okup"
            elif czas and mury2 <= 1.0:
                res = "czeka"   # projekt CZAS: AI nie szturmuje, gdy mury (bez czlonu czasu) <= 1
            else:
                # szturm: atak - ludzie z B175, jesli to zdobycie (dolaczaja druzyny), inaczej z O0
                A = amen
                zz = [z for z in zdmap.get(r["osada"], []) if abs(int(z["d"]) - int(r["d"])) <= 12 and z["atak_ludzi"] not in ("", "-")]
                if zz:
                    A = max(A, num(zz[0]["atak_ludzi"]))
                apm = apow / amen if amen > 0 else 1.4
                D = dpow2 / GPOW
                walls = True
                # bitwa pod murami (SiegeOutside) przed zdobyciem: bez S6 zaloga ginie w polu - zdobycie jak w fin30
                sx = [b for b in so if b["osada"] == r["osada"] and 0 <= int(b["d"]) - int(r["d"]) <= 12]
                if sx and not s6 and w10 == "zdobyte":
                    res = "zdobyte"; days = num(r["oblezenie_dob"]) + 4; lossA = 0
                else:
                    if sx and s6:
                        b = max(sx, key=lambda b: num(b["obr_ludzi"]))
                        D = max(D, num(b["obr_ludzi"]) + addg + addm)
                        A = max(A, num(b["atak_ludzi"]))
                    win, dur, lossA = assault(A, apm, D, GPOW, adv, town, walls)
                    res = "zdobyte" if win else "odparty"
                    days = num(r["oblezenie_dob"]) + dur
        out.append(dict(d=r["d"], osada=r["osada"], rodzaj=r["rodzaj"], stolica=cap, w10=w10, gar=gar, mil=mil, N=round(N),
                        dpow=dpow, dpow2=round(dpow2), apow=apow, ratio2=round(ratio2, 2), mury2=round(mury2, 2), res=res,
                        dni=None if days is None else round(days, 1), strata_atak=round(lossA)))
    return out

def summary(out, label):
    c = {}
    for o in out:
        c[o["res"]] = c.get(o["res"], 0) + 1
    zd_ = [o for o in out if o["res"] == "zdobyte" and o["dni"] is not None]
    dni = [o["dni"] for o in zd_]
    loss = [o["strata_atak"] for o in zd_ if o["strata_atak"] > 0]
    dpow2 = [o["dpow2"] for o in out]
    print("%-34s zdobyte %3d | odparte %3d | okup %3d | inne %3d | czeka %3d | nie zaczete %3d | dni do zdobycia med %s p90 %s | strata ataku na zdobycie med %s | obrona sila med %d"
          % (label, c.get("zdobyte", 0), c.get("odparty", 0), c.get("okup", 0), c.get("inne", 0), c.get("czeka", 0), c.get("nie zaczete", 0),
             round(st.median(dni), 1) if dni else "-", round(sorted(dni)[int(0.9 * (len(dni) - 1))], 1) if dni else "-",
             round(st.median(loss)) if loss else "-", st.median(dpow2)))
    return c

def world_cost(bc, bt, capw=2.0, fill=1.0, ncap=22):
    tot = 0.0
    for rid, k in kind1.items():
        if k not in ("miasto", "zamek"):
            continue
        tot += (bt if k == "miasto" else bc) * pfac(rid) * fill
    tot += ncap * bt * (capw - 1.0)
    return tot

if __name__ == "__main__":
    print("czynnik ludzi P (sqrt, 0.9-1.4): miasta med %.2f, zamki med %.2f" % (
        st.median([pfac(r) for r in kind1 if kind1[r] == "miasto"]), st.median([pfac(r) for r in kind1 if kind1[r] == "zamek"])))
    variants = [
        ("V0 model bez zmian", dict(base_castle=0, base_town=0)),
        ("V0+S6 (odsiecz za murami)", dict(base_castle=0, base_town=0, s6=True)),
        ("S1 300/600", dict(base_castle=300, base_town=600)),
        ("S1 400/800", dict(base_castle=400, base_town=800)),
        ("S1 500/1000", dict(base_castle=500, base_town=1000)),
        ("S1 400/800 +S5", dict(base_castle=400, base_town=800, vill=0.4)),
        ("S1 400/800 +S5 +S6", dict(base_castle=400, base_town=800, vill=0.4, s6=True)),
        ("S1 400/800 x0.8 +S5 +S6", dict(base_castle=400, base_town=800, vill=0.4, s6=True, fill=0.8)),
        ("S1 500/1000 +S5 +S6", dict(base_castle=500, base_town=1000, vill=0.4, s6=True)),
        ("S1 400/800 +S5+S6 prog 3.3", dict(base_castle=400, base_town=800, vill=0.4, s6=True, start_thr=3.3)),
        ("V0 + CZAS (mury<=1 czeka)", dict(base_castle=0, base_town=0, czas=True)),
        ("S1 400/800 +S5+S6 + CZAS", dict(base_castle=400, base_town=800, vill=0.4, s6=True, czas=True)),
        ("glowny, AI zaczyna wszystkie", dict(base_castle=400, base_town=800, vill=0.4, s6=True, start_thr=0.0)),
        ("glowny, wszystkie + CZAS", dict(base_castle=400, base_town=800, vill=0.4, s6=True, start_thr=0.0, czas=True)),
    ]
    allout = {}
    for lab, kw in variants:
        out = run(**kw)
        summary(out, lab)
        allout[lab] = out
    # krolestwo obroncy: Dorne (aserai) - zdobycia przed i po
    for lab in ("V0 model bez zmian", "S1 400/800 +S5 +S6"):
        dz = [o for o, r in zip(allout[lab], o0) if r["krol_obr"] == "aserai"]
        print("Dorne %-30s decyzji %d, zdobyte %d, nie zaczete %d, okup %d" % (lab, len(dz), sum(o["res"] == "zdobyte" for o in dz), sum(o["res"] == "nie zaczete" for o in dz), sum(o["res"] == "okup" for o in dz)))
    # zastepstwo: czy armia, ktora nie zaczelaby oblezenia, ma sile na typowy zamek po normie (sila zalogi 400 x P med + milicja 150) x 2.8
    main = allout["S1 400/800 +S5 +S6"]
    typ_castle = 400 * 0.95 * GPOW + 150 * 1.4 * MPOW
    ns = [o for o in main if o["res"] == "nie zaczete"]
    print("nie zaczete %d: armia ma >= 2.8 x typowy zamek po normie (sila %.0f): %d" % (len(ns), typ_castle, sum(o["apow"] >= 2.8 * typ_castle for o in ns)))
    print("mury <= 1 (AI czekalaby przy regule CZAS): dzis %d, po S1+S5 %d z %d" % (sum(num(r["mury"]) <= 1 for r in o0), sum(o["mury2"] <= 1 for o in main), len(main)))
    # S7 przebicie do murow - przyklady: najwiekszy oddzial obroncow w regionie w oknie oblezenia (robocze\sily\gdzie-armie.txt) wchodzi do murow
    for name, extra in (("King's Landing", 1323), ("Yronwood", 2085), ("Skyreach", 2007), ("Wyl", 1050)):
        r = [x for x in o0 if x["osada"] == name][-1]
        o = [x for x in main if x["osada"] == name][-1]
        apow = num(r["obl_sila"]); amen = num(r["ludzi_druzyn"]); adv = num(r["przewaga"])
        dpow2 = o["dpow2"] + extra * 1.3
        zz = [z for z in zd if z["osada"] == name and z["atak_ludzi"] not in ("", "-")]
        A = max(amen, max(num(z["atak_ludzi"]) for z in zz)) if zz else amen
        win, dni, loss = assault(A, apow / amen, dpow2 / GPOW, GPOW, adv, r["rodzaj"] == "miasto")
        win0, dni0, loss0 = assault(A, apow / amen, num(r["obr_sila"]) / GPOW, GPOW, adv, r["rodzaj"] == "miasto")
        print("S7 %-15s atak %d ludzi (sila %d) | dzis obrona sila %d -> %s po %.1f dobach szturmu, strata ataku %d | S1+S5 i %d ludzi w murach: sila %d, stosunek %.2f -> %s po %.1f dobach, strata ataku %d"
              % (name, A, apow, num(r["obr_sila"]), "zdobyte" if win0 else "odparty", dni0, loss0, extra, dpow2, apow / dpow2, "zdobyte" if win else "odparty", dni, loss))
    for bc, bt in ((300, 600), (400, 800), (500, 1000)):
        print("koszt normy %d/%d: ludzie w zalogach swiata przy pelnej normie %d" % (bc, bt, world_cost(bc, bt)))
    # zapis szczegolow wariantu glownego
    main = allout["S1 400/800 +S5 +S6"]; base = allout["V0 model bez zmian"]
    with open(os.path.join(HERE, "model-wyniki.tsv"), "w", encoding="utf-8", newline="") as f:
        w = csv.writer(f, delimiter="\t")
        w.writerow(["d", "osada", "rodzaj", "stolica", "wynik_fin30_10dob", "zaloga", "milicja*", "norma", "sila_obr", "sila_obr_nowa", "sila_obl",
                    "stosunek_nowy", "mury_nowe", "wynik_model_bez_zmian", "dni_bez_zmian", "wynik_model_nowy", "dni_nowe", "strata_ataku_nowa"])
        for o, b in zip(main, base):
            w.writerow([o["d"], o["osada"], o["rodzaj"], o["stolica"], o["w10"], o["gar"], o["mil"], o["N"], o["dpow"], o["dpow2"], o["apow"],
                        o["ratio2"], o["mury2"], b["res"], b["dni"], o["res"], o["dni"], o["strata_atak"]])
