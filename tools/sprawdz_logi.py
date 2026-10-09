"""Progi etapu 2 z logu Armoury (docs/PROJEKT-ETAP2-BANKRUCTWA-2026-10-09.md rozdz. 1 i "169c"; uwaga M12: kazdy prog jedna regula odczytu).

Uzycie (Python 3, tylko odczyt):
    python -I tools/sprawdz_logi.py --grupa etap2 <Armoury-*.log> [--baza baza.json] [--zapisz-baze baza.json]
--grupa etap2     : jedyna grupa na dzis (miejsce na kolejne).
--zapisz-baze P   : zapisuje plik bazowy z TEGO logu (bieg bazowy kroku A: autotest 120 dob na sklad8 + 10 + 11 + 169c) - srednie ostatnich 28 dob
                    na krolestwo i swiat; od niego licza sie progi "wobec dzis" (rozdz. 1, "Progi dodatkowe").
--baza P          : plik bazowy do progow "wobec dzis"; bez niego te progi sa INFO.
Wynik: tabela TAK / NIE / INFO z liczbami; brak linii w logu = "brak linii" (INFO), nigdy wyjatek.
Doba N = N-ta doba biegu (od pierwszej doby w logu). Krolestwa biedne z lore (Q4a): Iron Islands, Dragonstone, Sarnor.
"""
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


def run_day(day, first):
    return day - first + 1


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


def baseline_of(lines):
    """Srednie ostatnich 28 dob: wojsko rodow na krolestwo (doby w wojnie i w pokoju), zalogi, dezercja AI, lordowie > 60 dni, pieniadz swiata."""
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
    base["swiat"]["lordowie_ponad_60"] = mean_of(cap, r"ponad 60 dni (\d+)")
    base["swiat"]["pieniadz_zmiana"] = mean_of(money, r"razem \d+ \(([+-]?\d+)\)")
    return base


def main(argv):
    args = argv[1:]
    group, logp, base_in, base_out = None, None, None, None
    i = 0
    while i < len(args):
        a = args[i]
        if a == "--grupa":
            group = args[i + 1]; i += 2; continue
        if a == "--baza":
            base_in = args[i + 1]; i += 2; continue
        if a == "--zapisz-baze":
            base_out = args[i + 1]; i += 2; continue
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
    print(f"plik: {logp} | doby gry {first}-{last} ({len(alld)} dob)")

    # 1. glowy < 5 000 (doba 120, 364, 728; i ostatnia)
    heads = {run_day(d, first): num(r"glowy < 5000: (\d+)", s) for d, s in budget.items()}
    for n in (120, 364, 728):
        if n in heads:
            rep.add(f"glowy < 5000 (doba {n})", heads[n], "<= 10", heads[n] is not None and heads[n] <= 10)
    if heads:
        lastn = max(heads)
        rep.add("glowy < 5000 (ostatnia doba)", f"{heads[lastn]} (doba {lastn})", "<= 10 w dobach 120/364/728", None)
    else:
        rep.add("glowy < 5000", "brak linii", "<= 10", None)

    # 2. bankructwo (K39) - linia "D staly (169c)"
    sd = by_day(lines, "D staly (169c)")
    bank = [num(r"z rzedu\): (\d+)", s) for s in sd.values()]
    bank = [b for b in bank if b is not None]
    rep.add("bankruci (kiesa 0 i zold przyciety >= 7 dob)", max(bank) if bank else "brak linii", "0 w kazdej dobie", (max(bank) == 0) if bank else None)

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

    # 5. niedoplata korony (169c) - swiat i krolestwa w wojnie
    ar = by_day(lines, "Korona: niedoplata 28 dob (169c)")
    if ar:
        dl = max(ar)
        s = ar[dl]
        paid = num(r"wyplacone ([\d.]+)%", s, float)
        year = run_day(dl, first)
        rule = ">= 65% (rok 1)" if year <= 364 else ">= 50% (rok 2)"
        lim = 65.0 if year <= 364 else 50.0
        rep.add("korona: wyplacone z naleznego (28 dob, swiat)", f"{paid}% (doba {year})" if paid is not None else "-", rule, (paid >= lim) if paid is not None else None)
        bad = []
        for d in sorted(ar):
            part = ar[d].split("na krolestwo: ", 1)[-1]
            for m in re.finditer(r"([^,]+?) (\d+)%( \(pokoj\))?(?:,|\.$)", part):
                k, n, peace = m.group(1).strip(), int(m.group(2)), bool(m.group(3))
                if not peace and n > 50 and not any(p in k.lower() for p in LORE_POOR):
                    bad.append(f"{k} {n}% (doba {run_day(d, first)})")
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

    # 8. wojsko w druzynach lordow (wojna) - swiat
    tot = {}
    for k, ser in ks.items():
        for d, war, wallet, men in ser:
            tot[d] = tot.get(d, 0) + men
    if tot:
        lastd = max(tot)
        v = tot[lastd]
        rep.add("wojsko w druzynach lordow (swiat, ostatnia doba)", f"{v}", "95-115 tys. w wojnie; podloga 85 tys.", 85000 <= v and v <= 115000 if v else None)

    # 9. zalogi, dezercja, okupy, pieniadz swiata - wobec bazy
    base = None
    if base_in:
        with open(base_in, encoding="utf-8") as f:
            base = json.load(f)
    now = baseline_of(lines)
    sw, bw = now["swiat"], (base or {}).get("swiat", {})

    def vs(name, key, rule, test):
        v, b = sw.get(key), bw.get(key)
        if v is None:
            rep.add(name, "brak linii", rule, None)
        elif b is None or b == 0:
            rep.add(name, f"{v:.0f} (bez bazy)", rule, None)
        else:
            rep.add(name, f"{v:.0f} wobec bazy {b:.0f} ({100.0 * v / b - 100:+.0f}%)", rule, test(v, b))

    vs("zalogi (28 dob)", "zalogi", ">= 95% bazy w wojnie", lambda v, b: v >= 0.95 * b)
    vs("dezercja AI na dobe (28 dob)", "dezercja_ai", "<= baza + 50%", lambda v, b: v <= 1.5 * b)
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
    bank = by_day(lines, "IronBank: dzien")
    caps = [num(r"kapital Banku (-?\d+)", s) for s in bank.values()]
    caps = [c for c in caps if c is not None]
    rep.add("Bank: kapital (minimum)", min(caps) if caps else "brak linii", ">= 1 mln w kazdej dobie", (min(caps) >= 1000000) if caps else None)

    # 12. D staly pana zamku (doba 40) i zamkniecie sumy (169c)
    sdr = {run_day(d, first): s for d, s in sd.items()}
    if 40 in sdr:
        v = num(r"panowie samych zamkow \(\d+\): D staly mediana (-?\d+)", sdr[40])
        rep.add("D staly pana samych zamkow (doba 40)", v, "400-1300", (400 <= v <= 1300) if v is not None else None)
    clos = [num(r"roznica (-?[\d.]+)%", s, float) for s in sd.values()]
    clos = [c for c in clos if c is not None]
    rep.add("D staly: zamkniecie sumy (najwieksza roznica)", f"{max(abs(c) for c in clos):.2f}%" if clos else "brak linii", "co do 1%", (max(abs(c) for c in clos) <= 1.0) if clos else None)

    # 13. linie 169c obecne
    need = ["Niewola lordow i okupy (169c)", "Zalogi: przyrost bez werbunku (169c)", "Kasy miast (169c)", "Wydatki rycerzy (169c)", "Dezercja AI wedlug przyczyny (169c)",
            "Ludnosc BK (169c)", "Sluby AI (169c)", "Towar wedrowcow BK (169c)", "Wzrost Innych (169c)", "Miara historyczna cz. 2 (169c)", "D staly (169c)",
            "Korona: niedoplata 28 dob (169c)"]
    missing = [n for n in need if not any(s.startswith(n) for s in lines)]
    rep.add("linie 169c obecne", f"{len(need) - len(missing)} z {len(need)}" + (" - brak: " + ", ".join(missing) if missing else ""), "wszystkie", not missing)

    # 14. 2.6 - potkniecia korony
    kor = [s for s in lines if s.startswith("Korona: dzien") and "powinnosci wasali" in s]
    st26 = [num(r"rodow pominietych przez blad (\d+)", s) for s in kor]
    st26 = [x for x in st26 if x is not None]
    rep.add("2.6: rody pominiete przez blad (powinnosci)", max(st26) if st26 else "brak licznika (przed 2.6)", "0", (max(st26) == 0) if st26 else None)

    # 15. 2.14 - harness niewoli i przeplywy okupu
    h = [s for s in lines if s.startswith("Okupy (2.14)")]
    void = [num(r"w nicosc (\d+)", s) for s in h]
    void = [x for x in void if x is not None]
    rep.add("2.14: okup gracza w nicosc / dosypka kuriera", max(void) if void else "brak linii (przed 2.14)", "0 zl", (max(void) == 0) if void else None)
    harn = [s for s in lines if s.startswith("Harness niewoli (2.14)")]
    rep.add("2.14: harness niewoli w autotescie", f"{len(harn)} linii" + (" - " + harn[-1][:160] if harn else ""), "wynik OK w kazdym kroku", (all(" OK" in s for s in harn) and len(harn) > 0) if harn else None)

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
