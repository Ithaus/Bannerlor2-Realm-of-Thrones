"""Progi paczki 174b (docs/PROJEKT-174B-DOWOZ-2026-10-09.md rozdz. 6 po krytyce) z logu Armoury.

Uzycie (Python 3, tylko odczyt):
    python -I tools/p174b_progi.py <Armoury-*.log> [--zapis] [--baza <Armoury log sklad6>]
--zapis : bieg na zapisie 362 (8 dob) - progi w wersji "zapis".
--baza  : log porownawczy dla P6 (domyslnie stale sklad6 z ANALIZA.md: partie 71.7%, razem 79.8% - srednia spisow d30/35/40).
Tempo (P7/P8) - osobno: tools/p174b_doby.py; koszt (P13) liczony tutaj z linii "Koszt 171-174 (doba)".
Wynik: tabela TAK / NIE / INFO z liczbami. Bez wyjatku przy braku linii - wtedy "brak linii".
"""
import re
import sys
import statistics as st


def lines_of(path, prefix):
    out = []
    with open(path, encoding="utf-8", errors="replace") as f:
        for ln in f:
            m = re.match(r"\[(\d\d:\d\d:\d\d)\] (.*)", ln.rstrip("\n"))
            if m and m.group(2).startswith(prefix):
                out.append(m.group(2))
    return out


def all_lines(path):
    with open(path, encoding="utf-8", errors="replace") as f:
        return f.read().splitlines()


def num(pat, s, cast=int, default=None):
    m = re.search(pat, s)
    return cast(m.group(1)) if m else default


def by_day(lines, pat=r"dzien (\d+)"):
    d = {}
    for s in lines:
        k = num(pat, s)
        if k is not None:
            d.setdefault(k, s)
    return [d[k] for k in sorted(d)]


def coverage(s):
    """(ludzi partii, cokolwiek partie %, korpus szczebla partie %, ludzi zalog, cokolwiek zalogi %, korpus zalogi %, razem cokolwiek, razem korpus)."""
    p = re.search(r"partie rodow AI \((\d+) ludzi\): korpus (\d+)%.*?dowolny szczebel: korpus (\d+)%", s)
    g = re.search(r"zalogi AI \((\d+) ludzi\): korpus (\d+)%.*?dowolny szczebel: korpus (\d+)%", s)
    if not p or not g:
        return None
    pm, pk, pa = int(p.group(1)), int(p.group(2)), int(p.group(3))
    gm, gk, ga = int(g.group(1)), int(g.group(2)), int(g.group(3))
    ra = num(r"cokolwiek na tulowiu ([0-9.]+)%", s, float)
    rk = num(r"korpus szczebla ([0-9.]+)%", s, float)
    if ra is None:   # stara linia (sklad6): wzor "razem" = srednia partii i zalog wazona liczba ludzi
        ra = (pa * pm + ga * gm) / max(1, pm + gm)
        rk = (pk * pm + gk * gm) / max(1, pm + gm)
    return pm, pa, pk, gm, ga, gk, ra, rk


def mean(v):
    return sum(v) / len(v) if v else None


def fmt(x, d=1):
    return "-" if x is None else (("%." + str(d) + "f") % x)


def main():
    args = sys.argv[1:]
    if not args:
        print(__doc__)
        return
    path = args[0]
    zapis = "--zapis" in args
    baza = args[args.index("--baza") + 1] if "--baza" in args else None
    R = []

    def out(p, ok, txt):
        R.append((p, "TAK" if ok is True else ("NIE" if ok is False else "INFO"), txt))

    lines = all_lines(path)
    # P1 bledy i potkniecia
    errs = [l for l in lines if "] ERROR in " in l]
    stumbles = 0
    for l in lines:
        for m in re.finditer(r"potkniecia (\d+)", l):
            stumbles += int(m.group(1))
    out("P1", len(errs) == 0, "ERROR %d (pierwszy: %s); suma licznikow 'potkniecia N' %d (INFO - czesc to stare liczniki)" % (len(errs), errs[0][:120] if errs else "-", stumbles))

    # kontrakty
    K = by_day(lines_of(path, "Kontrakty surowca (174): dzien"))
    if K:
        bez_rudy = [num(r"miast bez rudy (\d+) z", s) for s in K]
        bez_rudy = [x for x in bez_rudy if x is not None]
        done = sum(num(r"dojechalo (\d+)", s) or 0 for s in K)
        rel = sum(num(r"zwolnione (\d+) \(", s) or 0 for s in K)
        lost = sum(num(r"rozbite/pojmane (\d+)", s) or 0 for s in K)
        rej = sum(num(r"rozkaz odrzucony (\d+)", s) or 0 for s in K)
        ret = sum(num(r"cel zmieniany przez innych (\d+)", s) or 0 for s in K)
        fin = done + rel + lost
        sea = sum(num(r"morzem zawarto (\d+)", s) or 0 for s in K)
        packs = sum(num(r"z jukow zawarto (\d+)", s) or 0 for s in K)
        ahead = sum(num(r"z wyprzedzeniem \(punkt zamowienia\) (\d+)", s) or 0 for s in K)
        audit = sum(num(r"audyt ilosci: rozjazdy (\d+)", s) or 0 for s in K)
        if not zapis:
            last10 = bez_rudy[-10:]
            out("P2", bool(last10) and mean(last10) <= 25, "miast bez rudy sr. ostatnich 10 dob %s (d40 %s; prog <= 25)" % (fmt(mean(last10)), last10[-1] if last10 else "-"))
        else:
            first, lastp = bez_rudy[:2], bez_rudy[-3:]
            trend = bool(first and lastp) and mean(lastp) <= 0.8 * mean(first)
            names = lines_of(path, "Miasta bez rudy i strzal (174b): dzien")
            lista = re.search(r"bez rudy \d+ \[([^\]]*)\]", names[0]).group(1).split(", ") if names else []
            got = set()
            for s in names[1:]:
                m = re.search(r"ruda dostarczona kontraktami od [^(]*\(\d+ miast\): (.*?); zrodla rudy", s)
                if m and m.group(1) != "-":
                    for part in m.group(1).split(", "):
                        got.add(part.rsplit(" ", 1)[0])
            pol = sum(1 for t in lista if t in got)
            ok = trend and lista and pol * 2 >= len(lista)
            out("P2", ok, "miast bez rudy sr. d1-2 %s -> sr. ostatnich 3 %s (prog spadek >= 20%%); dostawa rudy do %d z %d miast z pierwszej listy (prog >= polowa)"
                % (fmt(mean(first)), fmt(mean(lastp)), pol, len(lista)))
        out("P4", (fin > 0 and done / fin >= 0.8 and rej == 0 and ret <= 0.05 * fin) if not zapis else None,
            "dojechalo %d / zakonczone %d = %s%%; rozkaz odrzucony %d; cel zmieniany przez innych %d (%s%%)" % (done, fin, fmt(100.0 * done / fin if fin else None), rej, ret, fmt(100.0 * ret / fin if fin else None)))
        out("P11a", audit == 0, "audyt ilosci kontraktow: rozjazdy %d" % audit)
        out("INFO", None, "kontrakty morzem %d, z jukow %d, z wyprzedzeniem %d (pomocniczo: morzem > 0 i z jukow > 0)" % (sea, packs, ahead))
    else:
        out("P2/P4", None, "brak linii 'Kontrakty surowca (174)'")

    # P3 strzaly
    S = by_day(lines_of(path, "Strzelarze (172): dzien"))
    bez_strzal = [num(r"miast bez strzal (\d+) z", s) for s in S]
    bez_strzal = [x for x in bez_strzal if x is not None]
    if bez_strzal:
        if not zapis:
            last10 = bez_strzal[-10:]
            out("P3", mean(last10) <= 24, "miast bez strzal sr. ostatnich 10 dob %s (prog <= 24)" % fmt(mean(last10)))
        else:
            out("P3", None, "miast bez strzal: pierwsze %s, ostatnie %s (zapis - informacyjnie, trend)" % (bez_strzal[:2], bez_strzal[-3:]))
    W = by_day(lines_of(path, "Warsztaty: dzien"))
    ruda = [num(r"brak surowca \d+ \[ruda (\d+)", s) for s in W]
    ruda = [x for x in ruda if x is not None]
    held = [num(r"czeka na strzelarzy \(ruda dla strzelarzy, 174b\.3\) (\d+)", s) or 0 for s in W]
    out("INFO", None, "brak rudy w warsztatach sr. ostatnich 10 dob %s (pomocniczo <= 500); czeka na strzelarzy sr. %s" % (fmt(mean(ruda[-10:])), fmt(mean(held[-10:]))))

    # P5 zbroja na polkach - nowa sztuka w 7 dobach
    Z = by_day(lines_of(path, "Zbroja na polkach (174b): dzien"))
    start = 7 if zapis else 14
    bad = []
    for i, s in enumerate(Z):
        if i < start:
            continue
        m = re.search(r"korpus: t1-2 \d+ szt\./\d+ miast/nowa w 7 dob (\d+), t3-4 \d+ szt\./\d+ miast/nowa w 7 dob (\d+)", s)
        if m and (int(m.group(1)) < 49 or int(m.group(2)) < 24):
            bad.append((num(r"dzien (\d+)", s), int(m.group(1)), int(m.group(2))))
    if Z:
        rez = num(r"rezerwa kramu: sztuk (\d+)", Z[-1])
        out("P5", len(Z) > start and not bad, "dni ponizej progu (t1-2 >= 49, t3-4 >= 24, od %d. spisu): %s; w rezerwie (ostatni spis) %s szt. (INFO, <= 1164)" % (start + 1, bad[:5] if bad else "brak", rez))
    else:
        out("P5", None, "brak linii 'Zbroja na polkach (174b)'")

    # P6 pokrycie
    C = [coverage(s) for s in by_day(lines_of(path, "Pokrycie zbrojowni AI (171): dzien"))]
    C = [c for c in C if c]
    if C:
        if not zapis:
            b_pa, b_ra = 71.7, 79.8
            if baza:
                CB = [coverage(s) for s in by_day(lines_of(baza, "Pokrycie zbrojowni AI (171): dzien"))]
                CB = [c for c in CB if c][-3:]
                if CB:
                    b_pa, b_ra = mean([c[1] for c in CB]), mean([c[6] for c in CB])
            last3 = C[-3:]
            pa, ra, rk = mean([c[1] for c in last3]), mean([c[6] for c in last3]), mean([c[7] for c in last3])
            out("P6", pa >= b_pa - 1.5 and ra >= b_ra - 1.5,
                "srednia 3 ostatnich spisow: partie cokolwiek %s%% (prog >= %s), razem cokolwiek %s%% (prog >= %s), korpus szczebla razem %s%% (INFO)"
                % (fmt(pa), fmt(b_pa - 1.5), fmt(ra), fmt(b_ra - 1.5), fmt(rk)))
        else:
            f, l = C[0], C[-1]
            out("P6", l[6] >= f[6] - 1.0 and l[7] >= f[7] - 1.0, "razem cokolwiek %s -> %s%%, korpus szczebla razem %s -> %s%% (prog: nie nizej niz pierwszy spis - 1 pp)"
                % (fmt(f[6]), fmt(l[6]), fmt(f[7]), fmt(l[7])))

    # P9 / P13 koszt i pamiec polki
    KO = lines_of(path, "Koszt 171-174 (doba): dzien")
    if KO:
        pct = [num(r"nasze 171-174b [0-9.]+ ms \(([0-9.]+)% doby", s, float) for s in KO]
        pct = [p for p in pct if p is not None]
        mis = num(r"rozjazdow od startu sesji (\d+)", KO[-1])
        rozj = len(lines_of(path, "Pamiec polki (174b.5): ROZJAZD"))
        out("P9", (mis == 0) and rozj == 0, "pamiec polki: rozjazdow od startu sesji %s, linii ROZJAZD %d" % (mis, rozj))
        out("P13", bool(pct) and mean(pct) <= 1.0, "nasze 171-174b: srednio %s%% doby, mediana %s%%, maks. %s%% (prog srednio <= 1%%)" % (fmt(mean(pct), 2), fmt(st.median(pct), 2) if pct else "-", fmt(max(pct), 2) if pct else "-"))
    else:
        out("P9/P13", None, "brak linii 'Koszt 171-174 (doba)'")

    # P10 zlom
    ZL = lines_of(path, "Zlom z nadmiaru (zapis 174b):")
    zap = [s for s in ZL if "zapisano" in s]
    badp = [s for s in zap if "probny odczyt zgodny" not in s]
    load = [s for s in ZL if ("odtworzono" in s or "brak klucza" in s or "nieczytelny" in s)]
    out("P10", not badp and (not zapis or any("brak klucza" in s for s in load) or any("odtworzono" in s for s in load)),
        "zapisow %d, bez 'probny odczyt zgodny' %d; po wczytaniu: %s" % (len(zap), len(badp), "; ".join(s.split(": ", 1)[1][:90] for s in load) or "-"))

    # P11 nic z niczego
    RU = by_day([s for s in lines_of(path, "Towary: dzien") if " - iron (" in s])   # ksiega towarow, pozycja rudy (krytyka 19 - te same liczby co w uwadze)
    bw = [num(r"bez wyjasnienia ([+-]?\d+)", s) for s in RU]
    bw = [x for x in bw if x is not None]
    pos = [x for x in bw if x > 0]
    PB = by_day(lines_of(path, "Pieniadz swiata (bilans - przyczyny): dzien"))
    rr = [num(r"reszta ([+-]\d+) \[R\]", s) for s in PB]
    rr = [x for x in rr if x is not None]
    rpos = sum(x for x in rr if x > 0)
    TB = lines_of(path, "Towary (bilans): dzien")
    niezg = [s for s in TB if re.search(r"ruda (?!ZGODNA)[A-Z]+", s) or re.search(r"drewno (?!ZGODNA)[A-Z]+", s)]
    lim_r = 25000 if zapis else 10000
    out("P11", sum(pos) <= 15 and (max(pos) if pos else 0) <= 10 and rpos <= lim_r and not niezg,
        "ruda bez wyjasnienia: suma dodatnich dob %d (prog <= 15), maks. %s (<= 10); reszta R: suma dodatnich dob %d (prog <= %d); dob z ruda/drewnem NIEZGODNA %d"
        % (sum(pos), max(pos) if pos else 0, rpos, lim_r, len(niezg)))

    # P12 linia startowa
    MS = lines_of(path, "MaterialOrders (174.2):")
    if MS:
        s = MS[-1]
        ok = ("RouteCaravanHopByHop wpiety" in s and "InvalidateRedirectCache znaleziony" in s and "BRAK" not in s
              and "HourlyTickParty karawan (BK) wpiety" in s and "ReleaseCaravanFromHold (cel po oblezeniu) wpiety" in s)
        out("P12", ok, s[:220])
    else:
        out("P12", False, "brak linii startowej MaterialOrders")

    w = max(len(p) for p, _, _ in R)
    for p, v, t in R:
        print("%-*s %-4s %s" % (w, p, v, t))


if __name__ == "__main__":
    main()
