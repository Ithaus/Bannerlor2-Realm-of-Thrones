"""Progi etapu 2 z logu Armoury (docs/PROJEKT-ETAP2-BANKRUCTWA-2026-10-09.md rozdz. 1 i "169c"; uwaga M12: kazdy prog jedna regula odczytu).

Uzycie (Python 3, tylko odczyt):
    python -I tools/sprawdz_logi.py --grupa etap2 <Armoury-*.log> [--baza baza.json] [--zapisz-baze baza.json] [--csv budzet-rodow.csv] [--start-kampanii N]
--grupa etap2     : jedyna grupa na dzis (miejsce na kolejne).
--zapisz-baze P   : zapisuje plik bazowy z TEGO logu (bieg bazowy kroku A: autotest 120 dob na sklad8 + 10 + 11 + 169c) - srednie ostatnich 28 dob
                    na krolestwo i swiat; od niego licza sie progi "wobec dzis" (rozdz. 1, "Progi dodatkowe").
--baza P          : plik bazowy do progow "wobec dzis"; bez niego te progi sa INFO.
--csv P           : budzet-rodow.csv tego biegu (zalogi na krolestwo); domyslnie <katalog logu>/<znacznik czasu logu>/budzet-rodow.csv.
--start-kampanii N: doba gry startu kampanii (dzien z linii); domyslnie z linii "Pomiary 169c: ... start kampanii dzien N", a bez niej
                    pierwsza doba w logu (wtedy log kontynuacji z zapisu liczy doby od siebie - podaj N).
Wynik: tabela TAK / NIE / INFO z liczbami; brak linii w logu = "brak linii" (INFO), nigdy wyjatek.
Doba N = N-ta doba kampanii (dzien z linii - start + 1). Krolestwa biedne z lore (Q4a): Iron Islands, Dragonstone, Sarnor.
"""
import csv
import os
import json
import re
import statistics as st
import sys

LORE_POOR = ("iron islands", "dragonstone", "sarnor")
TS = re.compile(r"\[(\d\d:\d\d:\d\d)\] (.*)")


def read(path):
    out = []
    with open(path, encoding="utf-8", errors="replace") as f:
        for ln in f:
            m = TS.match(ln.rstrip("\n"))
            out.append(m.group(2) if m else ln.rstrip("\n"))
    return out


def num(pat, s, cast=int, default=None):
    m = re.search(pat, s)
    if not m:
        return default
    try:
        return cast(m.group(1))
    except ValueError:
        return default


def by_day(lines, prefix):
    """Ostatnia linia z prefiksem na kazda dobe gry -> {dzien: linia}."""
    d = {}
    for s in lines:
        if s.startswith(prefix):
            k = num(r"dzien (\d+)", s)
            if k is not None:
                d[k] = s
    return d


class Report:
    def __init__(self):
        self.rows = []

    def add(self, name, value, rule, ok):
        verdict = "INFO" if ok is None else ("TAK" if ok else "NIE")
        self.rows.append((name, value, rule, verdict))

    def show(self):
        w = max([len(r[0]) for r in self.rows] + [10])
        for name, value, rule, verdict in self.rows:
            print(f"{name.ljust(w)} | {verdict:4} | {value} | prog: {rule}")
        n_no = sum(1 for r in self.rows if r[3] == "NIE")
        print(f"-- razem progow {len(self.rows)}, NIE: {n_no}")


def run_day(day, start):
    return day - start + 1


def csv_path_of(logp):
    """<katalog>/Armoury-<znacznik>.log -> <katalog>/<znacznik>/budzet-rodow.csv (Log.Csv: katalog tematyczny sesji)."""
    d, f = os.path.split(logp)
    stem = os.path.splitext(f)[0].replace("Armoury-", "")
    return os.path.join(d, stem, "budzet-rodow.csv")


def read_csv(path):
    """Wiersze budzet-rodow.csv (rody AI) -> lista slownikow; brak pliku = []."""
    if not path or not os.path.isfile(path):
        return []
    out = []
    with open(path, encoding="utf-8", errors="replace", newline="") as f:
        for r in csv.DictReader(f, delimiter=";"):
            if r.get("rodzaj") == "gracz":
                continue
            out.append(r)
    return out


def garrisons_by_kingdom(rows, n=28):
    """Ludzie zalog na krolestwo w dobach WOJNY (krolestwo w wojnie = choc jeden jego rod z wojna=1), srednia ostatnich n dob CSV."""
    per = {}   # (dzien, krolestwo) -> [ludzi, wojna]
    for r in rows:
        k = (r.get("krolestwo") or "").strip()
        if not k:
            continue
        try:
            d = int(r.get("dzien") or 0)
            men = int(r.get("ludzi_zalogi") or 0)
        except ValueError:
            continue
        x = per.setdefault((d, k), [0, False])
        x[0] += men
        x[1] = x[1] or (r.get("wojna") == "1")
    days = last_window({d for d, _ in per}, n)
    sel = set(days)
    out = {}
    for (d, k), (men, war) in per.items():
        if d in sel and war:
            out.setdefault(k, []).append(men)
    return {k: st.mean(v) for k, v in out.items() if v}


def kingdom_war_series(lines):
    """Skarbce: dzien D - K (WOJNA): skarbiec X ... wojsko rodow N ludzi -> {K: [(dzien, wojna, skarbiec, wojsko)]}"""
    pat = re.compile(r"Skarbce: dzien (\d+) - (.+?)( \(WOJNA\))?: skarbiec (-?\d+).*?wojsko rodow (\d+) ludzi")
    out = {}
    for s in lines:
        if not s.startswith("Skarbce: dzien"):
            continue
        m = pat.match(s)
        if not m:
            continue
        out.setdefault(m.group(2), []).append((int(m.group(1)), bool(m.group(3)), int(m.group(4)), int(m.group(5))))
    return out


def last_window(days, n=28):
    ds = sorted(days)
    return ds[-n:] if ds else []


def desertion_morale_unpaid(s):
    """Dezercja AI z morale i z zaleglego zoldu (prog 183, projekt rozdz. 1) - bez limitu zoldu gry. Linia po przegladzie 169c ma sume;
    starsza - z czesci: lordowie AI (morale, morale w glodzie, zalegly zold) + zalogi AI (morale, morale w glodzie)."""
    v = num(r"AI morale i zalegly zold \(baza progu 183[^)]*\) (\d+)", s)
    if v is not None:
        return v
    tot, ok = 0, False
    for grp in ("partie lordow AI", "zalogi AI"):
        m = re.search(re.escape(grp) + r": morale (\d+), morale w glodzie (\d+)", s)
        if m:
            tot += int(m.group(1)) + int(m.group(2)); ok = True
    u = num(r"partie lordow AI: [^|]*?zalegly zold \(WarLedger\) (\d+)", s)
    if u is not None:
        tot += u
    return tot if ok else None


def baseline_of(lines, rows=None):
    """Srednie ostatnich 28 dob: wojsko rodow na krolestwo (doby w wojnie i w pokoju), zalogi (swiat i na krolestwo w wojnie - CSV),
    dezercja AI (morale + zalegly zold; i razem z limitem gry - INFO), lordowie > 60 dni, pieniadz swiata."""
    base = {"krolestwa": {}, "swiat": {}}
    ks = kingdom_war_series(lines)
    for k, ser in ks.items():
        days = last_window([d for d, _, _, _ in ser])
        sel = [x for x in ser if x[0] in set(days)]
        war = [x[3] for x in sel if x[1]]
        peace = [x[3] for x in sel if not x[1]]
        base["krolestwa"][k] = {"wojsko_wojna": st.mean(war) if war else None, "wojsko_pokoj": st.mean(peace) if peace else None}
    gar = by_day(lines, "Zalogi: przyrost bez werbunku (169c)")
    des = by_day(lines, "Dezercja AI wedlug przyczyny (169c)")
    cap = by_day(lines, "Niewola lordow i okupy (169c)")
    money = by_day(lines, "Pieniadz swiata: dzien")

    def mean_of(d, pat):
        vals = [num(pat, d[k]) for k in last_window(d)]
        vals = [v for v in vals if v is not None]
        return st.mean(vals) if vals else None

    base["swiat"]["zalogi"] = mean_of(gar, r"\| zalogi (\d+) ludzi")
    base["swiat"]["dezercja_ai"] = mean_of(des, r"AI razem (\d+)")
    dmu = [desertion_morale_unpaid(des[k]) for k in last_window(des)]
    dmu = [v for v in dmu if v is not None]
    base["swiat"]["dezercja_ai_morale_zold"] = st.mean(dmu) if dmu else None
    for k, v in garrisons_by_kingdom(rows or []).items():
        base["krolestwa"].setdefault(k, {"wojsko_wojna": None, "wojsko_pokoj": None})["zalogi_wojna"] = v
    base["swiat"]["lordowie_ponad_60"] = mean_of(cap, r"ponad 60 dni (\d+)")
    base["swiat"]["pieniadz_zmiana"] = mean_of(money, r"razem \d+ \(([+-]?\d+)\)")
    return base


def main(argv):
    args = argv[1:]
    group, logp, base_in, base_out, csvp, start_arg = None, None, None, None, None, None
    i = 0
    while i < len(args):
        a = args[i]
        if a == "--grupa":
            group = args[i + 1]; i += 2; continue
        if a == "--baza":
            base_in = args[i + 1]; i += 2; continue
        if a == "--zapisz-baze":
            base_out = args[i + 1]; i += 2; continue
        if a == "--csv":
            csvp = args[i + 1]; i += 2; continue
        if a == "--start-kampanii":
            start_arg = int(args[i + 1]); i += 2; continue
        logp = a; i += 1
    if group != "etap2" or not logp:
        print(__doc__)
        return 2
    lines = read(logp)
    rep = Report()
    budget = by_day(lines, "Budzet rodow (na sucho): dzien")
    alld = sorted(set(budget) | set(by_day(lines, "Pieniadz swiata: dzien")))
    first = alld[0] if alld else 0
    last = alld[-1] if alld else 0
    # doba kampanii: --start-kampanii, potem linia "Pomiary 169c: ... start kampanii dzien N", na koncu pierwsza doba logu
    start, how = start_arg, "--start-kampanii"
    if start is None:
        for s in lines:
            if s.startswith("Pomiary 169c: okna"):
                v = num(r"start kampanii dzien (\d+)", s)
                if v is not None:
                    start, how = v, "linia Pomiary 169c"
                    break
    if start is None or (alld and (start > first + 1 or first - start > 5000)):
        start, how = first, "pierwsza doba logu (brak linii startu - w logu kontynuacji podaj --start-kampanii)"
    if csvp is None:
        csvp = csv_path_of(logp)
    rows = read_csv(csvp)
    print(f"plik: {logp} | doby gry {first}-{last} ({len(alld)} dob) | start kampanii {start} ({how}): doby kampanii {run_day(first, start)}-{run_day(last, start)}"
          f" | CSV: {csvp if rows else 'brak (' + csvp + ')'}")

    # 1. glowy < 5 000 (doba kampanii 120, 364, 728; i ostatnia)
    heads = {run_day(d, start): num(r"glowy < 5000: (\d+)", s) for d, s in budget.items()}
    for n in (120, 364, 728):
        if n in heads:
            rep.add(f"glowy < 5000 (doba {n})", heads[n], "<= 10", heads[n] is not None and heads[n] <= 10)
    if heads:
        lastn = max(heads)
        rep.add("glowy < 5000 (ostatnia doba)", f"{heads[lastn]} (doba {lastn})", "<= 10 w dobach 120/364/728", None)
    else:
        rep.add("glowy < 5000", "brak linii", "<= 10", None)

    # 2. bankructwo do wgrania 168 (projekt rozdz. 1): K39 lub bankrut Banku. Linia "D staly" po przegladzie 169c ma liczbe rodow AI (bez podwojnego
    # liczenia); starsza - K39 z "D staly" + "bankrutow N" z linii IronBank tej samej doby (IronBank liczy tez gracza; rod moze byc w obu - gorna granica)
    sd = by_day(lines, "D staly (169c)")
    ib = by_day(lines, "IronBank: dzien")
    bank_rows = []
    for d in sorted(set(sd) | set(ib)):
        s = sd.get(d, "")
        m = re.search(r"bankruci do 168 \(K39 lub bankrut Banku, rody AI\): (\d+) \(K39 - [^:]*: (\d+), w tym z doba bez znanego salda (\d+); bankruci Banku: (\d+)", s)
        if m:
            bank_rows.append((d, int(m.group(1)), int(m.group(2)), int(m.group(3)), int(m.group(4)), "linia D staly"))
            continue
        k39 = num(r"z rzedu\): (\d+)", s) if s else None
        b = num(r"bankrutow (\d+)", ib.get(d, "")) if d in ib else None
        if k39 is None and b is None:
            continue
        bank_rows.append((d, (k39 or 0) + (b or 0), k39 or 0, 0, b or 0, "K39 + IronBank (suma)"))
    if bank_rows:
        worst = max(bank_rows, key=lambda x: x[1])
        rep.add("bankruci do 168 (K39 lub bankrut Banku)", f"{worst[1]} (doba {run_day(worst[0], start)}: K39 {worst[2]}, w tym bez znanego salda {worst[3]}; Bank {worst[4]}; {worst[5]})",
                "0 w kazdej dobie", worst[1] == 0)
    else:
        rep.add("bankruci do 168 (K39 lub bankrut Banku)", "brak linii", "0 w kazdej dobie", None)

    # 3. zajecie dochodu - dopiero 168
    rep.add("zajecie dochodu (D3)", "brak (168 niewgrane)", "<= 1 w 120 dobach, <= 5 w roku", None)

    # 4. skarbiec w wojnie < 0.25 mln dluzej niz 28 dob
    ks = kingdom_war_series(lines)
    worst, worstk = 0, "-"
    for k, ser in ks.items():
        run = best = 0
        for d, war, wallet, men in sorted(ser):
            run = run + 1 if (war and wallet < 250000) else 0
            best = max(best, run)
        if best > worst:
            worst, worstk = best, k
    rep.add("skarbiec w wojnie < 0.25 mln (najdluzsza seria)", f"{worst} dob ({worstk})" if ks else "brak linii", "0 serii > 28 dob", (worst <= 28) if ks else None)

    # 5. niedoplata korony (169c) - swiat i krolestwa w wojnie; rok 1 / rok 2 wedlug doby kampanii
    ar = by_day(lines, "Korona: niedoplata 28 dob (169c)")
    if ar:
        dl = max(ar)
        s = ar[dl]
        paid = num(r"wyplacone ([\d.]+)%", s, float)
        year = run_day(dl, start)
        rule = ">= 65% (rok 1)" if year <= 364 else ">= 50% (rok 2)"
        lim = 65.0 if year <= 364 else 50.0
        rep.add("korona: wyplacone z naleznego (28 dob, swiat)", f"{paid}% (doba {year})" if paid is not None else "-", rule, (paid >= lim) if paid is not None else None)
        bad = []
        for d in sorted(ar):
            part = ar[d].split("na krolestwo: ", 1)[-1]
            for m in re.finditer(r"([^,]+?) (\d+)%( \(pokoj\))?(?:,|\.$)", part):
                k, n, peace = m.group(1).strip(), int(m.group(2)), bool(m.group(3))
                if not peace and n > 50 and not any(p in k.lower() for p in LORE_POOR):
                    bad.append(f"{k} {n}% (doba {run_day(d, start)})")
        rep.add("korona: krolestwo w wojnie z niedoplata > 50% (28 dob)", ", ".join(bad[-5:]) if bad else "0", "zadne (poza biednymi z lore)", not bad)
    else:
        rep.add("korona: niedoplata 28 dob", "brak linii", ">= 65% / >= 50%", None)

    # 6. zalegly zold w zlocie - linia "Zold:"
    zold = by_day(lines, "Zold: dzien")
    shares = []
    for d in last_window(zold):
        s = zold[d]
        a = num(r"partie rodow: naliczony (\d+)", s) or 0
        g = num(r"garnizony: naliczony (\d+)", s) or 0
        cut = num(r"glowy: (\d+) w \d+ rodach", s) or 0
        if a + g > 0:
            shares.append(100.0 * cut / (a + g))
    rep.add("zalegly zold w zlocie (28 dob)", f"{st.mean(shares):.2f}%" if shares else "brak linii", "<= 2%", (st.mean(shares) <= 2.0) if shares else None)

    # 7. zalegly zold w partiach - linia "Budzet rodow"
    parts = []
    for d in last_window(budget):
        m = re.search(r"zalegly zold: (\d+) partii i zalog z (\d+)", budget[d])
        if m and int(m.group(2)) > 0:
            parts.append(100.0 * int(m.group(1)) / int(m.group(2)))
    rep.add("zalegly zold w partiach (28 dob)", f"{st.mean(parts):.2f}%" if parts else "brak linii", "<= 2%", (st.mean(parts) <= 2.0) if parts else None)

    # 8. wojsko w druzynach lordow W WOJNIE - suma krolestw z (WOJNA) ostatniej doby; cel 95-115 tys., twarda podloga 85 tys.
    tot, totall = {}, {}
    for k, ser in ks.items():
        for d, war, wallet, men in ser:
            totall[d] = totall.get(d, 0) + men
            if war:
                tot[d] = tot.get(d, 0) + men
    if totall:
        lastd = max(totall)
        v = tot.get(lastd, 0)
        nwar = sum(1 for k, ser in ks.items() for d, war, _, _ in ser if d == lastd and war)
        if v == 0:
            rep.add("wojsko w druzynach lordow (krolestwa w wojnie, ostatnia doba)", f"0 (zadne krolestwo w wojnie; wszystkie {totall[lastd]})", "95-115 tys. w wojnie", None)
        else:
            if 95000 <= v <= 115000:
                ok, note = True, ""
            elif 85000 <= v < 95000:
                ok, note = False, " - ponizej celu 95 tys. (nad podloga 85 tys.)"
            else:
                ok, note = False, (" - ponizej podlogi 85 tys." if v < 85000 else " - powyzej 115 tys.")
            rep.add("wojsko w druzynach lordow (krolestwa w wojnie, ostatnia doba)", f"{v} ({nwar} krolestw w wojnie; wszystkie {totall[lastd]}){note}",
                    "95-115 tys. w wojnie; twarda podloga 85 tys.", ok)

    # 9. zalogi, dezercja, okupy, pieniadz swiata - wobec bazy
    base = None
    if base_in:
        with open(base_in, encoding="utf-8") as f:
            base = json.load(f)
    now = baseline_of(lines, rows)
    sw, bw = now["swiat"], (base or {}).get("swiat", {})

    def vs(name, key, rule, test, info=False):
        v, b = sw.get(key), bw.get(key)
        if v is None:
            rep.add(name, "brak linii", rule, None)
        elif b is None or b == 0:
            rep.add(name, f"{v:.0f} (bez bazy)", rule, None)
        else:
            rep.add(name, f"{v:.0f} wobec bazy {b:.0f} ({100.0 * v / b - 100:+.0f}%)", rule, None if info else test(v, b))

    vs("zalogi swiata (28 dob)", "zalogi", "INFO - prog na krolestwo (wiersz nizej)", None, info=True)
    if base:
        low, have = [], 0
        for k, cur in now["krolestwa"].items():
            cg, bg = cur.get("zalogi_wojna"), base["krolestwa"].get(k, {}).get("zalogi_wojna")
            if cg is None or not bg:
                continue
            have += 1
            if cg < 0.95 * bg:
                low.append(f"{k} {100.0 * cg / bg:.0f}%")
        rep.add("zalogi w wojnie na krolestwo (28 dob, CSV)", (", ".join(low) if low else f"wszystkie w normie ({have} krolestw)") if have else "brak danych (CSV albo baza bez zalog)",
                ">= 95% bazy w wojnie", (not low) if have else None)
    else:
        ng = sum(1 for k in now["krolestwa"].values() if k.get("zalogi_wojna"))
        rep.add("zalogi w wojnie na krolestwo (28 dob, CSV)", f"{ng} krolestw (bez bazy)" if rows else "brak CSV", ">= 95% bazy w wojnie", None)
    vs("dezercja AI z morale i zaleglego zoldu (28 dob)", "dezercja_ai_morale_zold", "<= baza + 50% (prog 183)", lambda v, b: v <= 1.5 * b)
    vs("dezercja AI razem z limitem zoldu gry (28 dob)", "dezercja_ai", "INFO (166 wylacza limit gry)", None, info=True)
    vs("lordowie w niewoli > 60 dni (28 dob)", "lordowie_ponad_60", "<= baza + 20%", lambda v, b: v <= 1.2 * b)
    v, b = sw.get("pieniadz_zmiana"), bw.get("pieniadz_zmiana")
    rep.add("pieniadz swiata - zmiana na dobe (28 dob)", f"{v:+.0f}" + (f" wobec bazy {b:+.0f} (roznica {v - b:+.0f})" if b is not None and v is not None else "") if v is not None else "brak linii",
            "B: +-30 tys./dobe wobec biegu bez paczki; C/D: +-50 tys.", None if (v is None or b is None) else abs(v - b) <= 50000)

    # 10. krolestwa w wojnie wobec bazy (-25% / +20%; biedne z lore -35%)
    if base:
        worse = []
        for k, cur in now["krolestwa"].items():
            bk = base["krolestwa"].get(k, {})
            cw, bwv = cur.get("wojsko_wojna"), bk.get("wojsko_wojna")
            if cw is None or not bwv:
                continue
            ch = cw / bwv - 1
            low = -0.35 if any(p in k.lower() for p in LORE_POOR) else -0.25
            if ch < low or ch > 0.20:
                worse.append(f"{k} {100 * ch:+.0f}%")
        rep.add("krolestwa w wojnie: wojsko wobec bazy", ", ".join(worse) if worse else "wszystkie w normie", "-25%..+20% (lore -35%)", not worse)

    # 11. Bank - wolny kapital >= 1 mln
    caps = [num(r"kapital Banku (-?\d+)", s) for s in ib.values()]
    caps = [c for c in caps if c is not None]
    rep.add("Bank: kapital (minimum)", min(caps) if caps else "brak linii", ">= 1 mln w kazdej dobie", (min(caps) >= 1000000) if caps else None)

    # 12. D staly pana zamku (doba kampanii 40); linie modelu nierozpoznane (prog); zamkniecie sumy - INFO (z budowy)
    sdr = {run_day(d, start): s for d, s in sd.items()}
    if 40 in sdr:
        v = num(r"panowie samych zamkow \(\d+\): D staly mediana (-?\d+)", sdr[40])
        rep.add("D staly pana samych zamkow (doba 40)", v, "400-1300", (400 <= v <= 1300) if v is not None else None)
    unr = [(d, num(r"nierozpoznane: modul \d+ zl = ([\d.]+)% wplywu", s, float)) for d, s in sd.items()]
    unr = [(d, x) for d, x in unr if x is not None]
    if unr:
        dw, xw = max(unr, key=lambda t: t[1])
        rep.add("D staly: linie modelu nierozpoznane (najwiecej w dobie)", f"{xw:.2f}% wplywu D169 (doba {run_day(dw, start)})", "<= 2% swiata", xw <= 2.0)
    else:
        rep.add("D staly: linie modelu nierozpoznane", "brak linii (przed przegladem 169c)", "<= 2% swiata", None)
    clos = [num(r"roznica (-?[\d.]+)%", s, float) for s in sd.values()]
    clos = [c for c in clos if c is not None]
    rep.add("D staly: zamkniecie sumy (najwieksza roznica)", f"{max(abs(c) for c in clos):.2f}%" if clos else "brak linii", "INFO - z budowy (inne z modelu to reszta)", None)

    # 13. linie 169c obecne; ludnosc BK bez danych (pomiar nie inicjuje danych BK); koszt pomiaru
    need = ["Niewola lordow i okupy (169c)", "Zalogi: przyrost bez werbunku (169c)", "Kasy miast (169c)", "Wydatki rycerzy (169c)", "Dezercja AI wedlug przyczyny (169c)",
            "Ludnosc BK (169c)", "Sluby AI (169c)", "Towar wedrowcow BK (169c)", "Wzrost Innych (169c)", "Miara historyczna cz. 2 (169c)", "D staly (169c)",
            "Korona: niedoplata 28 dob (169c)", "Pomiary 169c: czas dzien"]
    missing = [n for n in need if not any(s.startswith(n) for s in lines)]
    rep.add("linie 169c obecne", f"{len(need) - len(missing)} z {len(need)}" + (" - brak: " + ", ".join(missing) if missing else ""), "wszystkie", not missing)
    pop = [num(r"bez danych BK (\d+)", s) for s in lines if s.startswith("Ludnosc BK (169c)")]
    pop = [x for x in pop if x is not None]
    rep.add("Ludnosc BK: osady bez danych BK (najwiecej)", max(pop) if pop else "brak linii", "0", (max(pop) == 0) if pop else None)
    vno = [num(r"wsie bez danych BK \(podatek pominiety\): (\d+)", s) for s in sd.values()]
    vno = [x for x in vno if x is not None]
    rep.add("D staly: wsie bez danych BK (najwiecej)", max(vno) if vno else "brak linii", "INFO (oczekiwane 0)", None)
    cost = [num(r"\| razem (\d+) ms na dobe", s) for s in lines if s.startswith("Pomiary 169c: czas dzien")]
    cost = [x for x in cost if x is not None]
    rep.add("koszt pomiarow 169c (srednio ms na dobe)", f"{st.mean(cost):.0f} (max {max(cost)})" if cost else "brak linii",
            "<= 390 ms (3% z 13.1 s/dobe; okna Harmony - czas doby biegu)", (st.mean(cost) <= 390) if cost else None)

    # 14. 2.6 - potkniecia korony
    kor = [s for s in lines if s.startswith("Korona: dzien") and "powinnosci wasali" in s]
    st26 = [num(r"rodow pominietych przez blad (\d+)", s) for s in kor]
    st26 = [x for x in st26 if x is not None]
    rep.add("2.6: rody pominiete przez blad (powinnosci)", max(st26) if st26 else "brak licznika (przed 2.6)", "0", (max(st26) == 0) if st26 else None)

    # 15. 2.14 - przeplywy okupu i harness niewoli (krok 1/1b - okup gracza i odbiorcy; krok 2 - kurier na sucho; 2b/2c - PRAWDZIWE AcceptRansomOffer)
    h = [s for s in lines if s.startswith("Okupy (2.14)")]
    void = [num(r"w nicosc (\d+)", s) for s in h]
    void = [x for x in void if x is not None]
    rep.add("2.14: okup gracza w nicosc / dosypka kuriera", max(void) if void else "brak linii (przed 2.14)", "0 zl", (max(void) == 0) if void else None)
    town = sum(num(r"zapasowy: kasa najblizszego miasta (\d+)", s) or 0 for s in h if s.startswith("Okupy (2.14): dzien"))
    hide = sum(num(r"kasa kryjowki bandy (\d+)", s) or 0 for s in h if s.startswith("Okupy (2.14): dzien"))
    rep.add("2.14: okup do kasy osady (kryjowka / miasto bez pana)", f"kryjowka {hide}, kasa miasta {town}" if h else "brak linii", "INFO (kasa miasta ponad cel regulatora gry jest kasowana)", None)
    harn = [s for s in lines if s.startswith("Harness niewoli (2.14)")]
    if not harn:
        rep.add("2.14: harness niewoli (krok 1 i 2)", "brak linii (nie autotest albo RansomHarnessInAutotest wylaczone)", "wynik OK w kazdym kroku", None)
        rep.add("2.14: kurier prawdziwy w harnessie (krok 2b, 2c)", "brak linii", "2b i 2c OK", None)
    else:
        base_steps = [s for s in harn if not s.startswith("Harness niewoli (2.14): krok 2b") and not s.startswith("Harness niewoli (2.14): krok 2c - ")
                      and not s.startswith("Harness niewoli (2.14): sprzatanie")]
        bad = [s for s in base_steps if " OK" not in s]
        rep.add("2.14: harness niewoli (krok 1, 1b, 2 na sucho)", f"{len(base_steps)} linii" + (" - " + bad[0][:160] if bad else ""), "wynik OK w kazdym kroku",
                (not bad) and len(base_steps) > 0)
        real = {k: [s for s in harn if s.startswith("Harness niewoli (2.14): krok " + k + " - ")] for k in ("2b", "2c")}
        skip = [s for s in harn if s.startswith("Harness niewoli (2.14): krok 2b/2c")]
        if real["2b"] and real["2c"]:
            okr = all(s.rstrip(".").endswith(" OK") for k in ("2b", "2c") for s in real[k])
            rep.add("2.14: kurier prawdziwy w harnessie (krok 2b, 2c)", " / ".join(k + ": ..." + real[k][-1][-120:] for k in ("2b", "2c")), "2b i 2c OK", okr)
        else:
            why = skip[-1][:160] if skip else ("tylko 2b" if real["2b"] else "brak linii 2b/2c")
            rep.add("2.14: kurier prawdziwy w harnessie (krok 2b, 2c)", why, "2b i 2c OK (pominiety = NIE)", False)

    # 16. bledy naszych modow
    errs = [s for s in lines if s.startswith("ERROR in ")]
    rep.add("bledy Armoury (ERROR in)", len(errs), "0", len(errs) == 0)

    rep.show()
    if base_out:
        with open(base_out, "w", encoding="utf-8") as f:
            json.dump(now, f, ensure_ascii=False, indent=1)
        print(f"plik bazowy zapisany: {base_out}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
