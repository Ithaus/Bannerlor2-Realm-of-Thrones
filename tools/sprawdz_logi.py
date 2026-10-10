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
--koniec-etapu    : test konca etapu 2 (rozdz. E): warunki etapu (glowy < 5 000, bankruci, wojsko 95-115 tys., krolestwa i zalogi na
                    krolestwo, skarbce buntow) wiazace bez wyjatkow. Bez tej flagi (test kroku: B, C1-C3, D) warunek etapu, ktorego bieg
                    nie spelnia, ale nie jest w nim gorszy niz bieg bazowy (regula w kolumnie progu), dostaje werdykt ETAP - liczy sie osobno od NIE.
Wynik: tabela TAK / NIE / ETAP / DECYZJA / INFO z liczbami; brak linii w logu = "brak linii" (INFO), nigdy wyjatek. DECYZJA (B-4): prog projektu
niespelniony z przyczyny, ktorej krok nie usuwa (opis w wierszu) - nie TAK i nie INFO, liczony osobno.
Doba N = N-ta doba kampanii (dzien z linii - start + 1). Krolestwa biedne z lore (Q4a): Iron Islands, Dragonstone, Sarnor.
B-3 (krok B po tescie 120 dob): pomiary wobec bazy, ktore mieszaly skutek paczki z losem jednej kampanii, liczone uczciwiej - pieniadz swiata
w oknie dob 31+ bez wojennych zrodel z niczego i z reszta ksiegi wobec bazy, wojsko jako srednia 28 dob, bunty razem z krolestwem macierzystym,
zalogi na twierdze (balans-krolestw.csv), sila band zamiast samego zlota (decyzja D), dochod pana zamku z zaworu jako INFO (opis przy wierszu).
Plik bazowy zapisany przed B-3 nie ma nowych kluczy - zapisac go jeszcze raz z logu biegu bazowego (--zapisz-baze); bez nich wiersze B-3 = brak bazy.
B-4 (przeglad B-3): ETAP tylko, gdy bieg nie jest gorszy niz baza (wojsko w wojnie wobec bazy w tych samych dobach; zmiana krolestwa o tym samym
znaku we wszystkich oknach 28 dob = DECYZJA); regulator kas miast rozdzielony na dosypke i kasowanie (plik bazowy sprzed B-4 - zapisac jeszcze raz);
reszta ksiegi wiaze na kazdej dlugosci biegu; Z8 jako gorna granica z 1 zl (licznik linii "Zold:" od B-4); pan zamku z zaworu - prog projektu.
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

    def add(self, name, value, rule, ok, etap=False, decide=False):
        # etap=True: warunek etapu 2, ktorego bieg nie spelnia, ale nie jest gorszy niz bieg bazowy (regula w progu) - werdykt ETAP zamiast NIE
        # decide=True (B-4): prog projektu niespelniony z przyczyny, ktorej krok nie usuwa (opis w wierszu) - werdykt DECYZJA zamiast NIE;
        # nigdy TAK ani INFO - liczony osobno, zeby nie zginal
        verdict = "INFO" if ok is None else ("TAK" if ok else ("ETAP" if etap else ("DECYZJA" if decide else "NIE")))
        self.rows.append((name, value, rule, verdict))

    def show(self):
        w = max([len(r[0]) for r in self.rows] + [10])
        for name, value, rule, verdict in self.rows:
            print(f"{name.ljust(w)} | {verdict:7} | {value} | prog: {rule}")
        n_no = sum(1 for r in self.rows if r[3] == "NIE")
        n_et = sum(1 for r in self.rows if r[3] == "ETAP")
        n_de = sum(1 for r in self.rows if r[3] == "DECYZJA")
        print(f"-- razem progow {len(self.rows)}, NIE: {n_no}, ETAP (warunek etapu niespelniony, nie gorzej niz baza): {n_et}"
              f", DECYZJA (prog projektu niespelniony - przyczyna poza krokiem, do decyzji): {n_de}")


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


# ------------------------------------------------------------ B-3: pomiary wobec bazy oddzielone od losu jednej kampanii
REB = re.compile(r"^.*\((.+?) Rebels\)$")


def merge_rebels(k):
    """Krolestwo buntownikow ("Volentin League (Braavos Rebels)") liczone razem z krolestwem macierzystym ("Braavos") - bunt dzieli wojsko
    i twierdze jednego krolestwa, a w drugim biegu buntu moze nie byc."""
    m = REB.match(k or "")
    return m.group(1) if m else k


# wojenne zrodla zlota z niczego, ktorych kroki etapu 2 nie dotykaja (statki NavalDLC, bitwy, jency, lupy) - ich roznica miedzy dwiema
# kampaniami to inne wojny (bieg 120 dob kroku B: statki +158 tys./dobe w dobach 93-120 z trzech dob bitew morskich, -75 tys. w dobach 61-92)
WAR_KEYS = ("statki_inne", "statki_sprz", "bitwy", "jency", "jency_tw", "lup_obl")


def money_days(lines, start):
    """Na dobe kampanii: zmiana sumy i reszta ksiegi (kontrola linii przyczyn), wojenne zrodla z niczego, ujscia zamykane przez B, regulator
    kas miast i zamkow, sakwy taborow, ktore naprawde zniknely (linia 112)."""
    cau = by_day(lines, "Pieniadz swiata (bilans - przyczyny): dzien")
    cas = by_day(lines, "Przeplywy osad (kasy zamkow): dzien")
    town = by_day(lines, "Przeplywy osad (kasy miast): dzien")
    ut = by_day(lines, "Utarg wsi (112): dzien")
    out = {}
    for d, s in cau.items():
        kv = dict((a, int(b)) for a, b in re.findall(r"\b([A-Z]+\d*)=(-?\d+)", s.split("kontrola:", 1)[-1]))
        if "Z" not in kv or "R" not in kv:
            continue
        g = lambda p: num(p, s, int, 0) or 0
        x = {"Z": kv["Z"], "R": kv["R"], "lup_cial": kv.get("Z1", 0),
             "statki_inne": g(r"statki NavalDLC inne (\d+)"), "statki_sprz": g(r"statki: sprzedaz osadom i premie zarzadcow (\d+)"),
             "bitwy": g(r", bitwy (\d+), majatki"), "jency": g(r"jency sprzedani przez partie (\d+)"), "jency_tw": g(r"jency do kas twierdz (\d+)"),
             "lup_obl": g(r"lup z oblezen (\d+)"),
             "utarg_zniklo": g(r"z utargu wsi zniklo (\d+)"), "prow_wsie": g(r"prowizja od sprzedazy partiom (?:-?\d+|-) \(wsie (-?\d+)"),
             "u8_tabory": g(r"zniknely z mapy -?\d+ \(karawany -?\d+, tabory (-?\d+)")}
        c = cas.get(d)
        if c:
            x["zamki_zakupy"] = num(r'"zakupy" mieszkancow ([+-]?\d+)', c, int, 0) or 0
            x["zamki_dosypal"] = num(r"dosypal (\d+), skasowal", c, int, 0) or 0
            x["zamki_skasowal"] = num(r"skasowal (\d+);", c, int, 0) or 0
        t = town.get(d)
        if t:
            m = re.search(r"regulator kasy [+-]?\d+ \[P\] \(dosypal (\d+), skasowal (\d+)", t)
            if m:
                x["miasta_reg"] = int(m.group(1)) - int(m.group(2))
                # B-4: osobno dosypka (zloto z niczego) i kasowanie (zloto w nicosc) - mniej dosypki to zamkniete zrodlo, wiecej kasowania to nowe ujscie
                x["miasta_dos"], x["miasta_kas"] = int(m.group(1)), int(m.group(2))
        u = ut.get(d)
        if u:
            seg = ""
            for part in u.split(" | "):
                if part.startswith("tabory zniszczone"):
                    seg = part
            z = re.findall(r"zniklo (-?\d+)", seg)
            if z:
                x["sakwy_zniklo_112"] = int(z[-1])
        out[run_day(d, start)] = x
    return out


def kingdom_days(lines, start):
    """Skarbce na dobe kampanii, bunty razem z krolestwem macierzystym: {krolestwo: {doba: [w wojnie 0/1, wojsko rodow]}}."""
    out = {}
    for k, ser in kingdom_war_series(lines).items():
        mk = merge_rebels(k)
        for d, war, wallet, men in ser:
            x = out.setdefault(mk, {}).setdefault(run_day(d, start), [0, 0])
            x[0] = 1 if (x[0] or war) else 0
            x[1] += men
    return out


def fortress_days(path, start, lines):
    """balans-krolestw.csv (175.0): twierdze i ludzie zalog na krolestwo (id) i dobe kampanii (kolumna dzien - ta sama numeracja co linie logu)
    -> {id: {doba: [twierdze, ludzie zalog, w wojnie 1/0/-1]}}. Wojna z linii "Skarbce" tej doby: krolestwo, ktorego "wojsko rodow" rowna sie
    "ludzie_partie" z CSV (jedno dopasowanie; inaczej -1 = nie wiadomo). Bez buntow (new_kingdom*) i bez "bez_krolestwa" - na twierdze
    liczymy krolestwa, ktore sa w obu biegach; twierdze stracone na rzecz buntu i tak wypadaja z mianownika."""
    out = {}
    if not path or not os.path.isfile(path):
        return out
    army = {}   # (dzien, wojsko) -> [w wojnie, ...]
    for k, ser in kingdom_war_series(lines).items():
        for d, war, wallet, men in ser:
            army.setdefault((d, men), []).append(1 if war else 0)
    with open(path, encoding="utf-8", errors="replace", newline="") as f:
        for r in csv.DictReader(f, delimiter=";"):
            k = (r.get("krolestwo") or "").strip()
            if not k or k == "bez_krolestwa" or k.startswith("new_kingdom"):
                continue
            try:
                day = int(r.get("dzien") or 0)
                tw, men, lp = int(r.get("twierdze") or 0), int(r.get("ludzie_zalogi") or 0), int(r.get("ludzie_partie") or 0)
            except ValueError:
                continue
            w = army.get((day, lp))
            out.setdefault(k, {})[run_day(day, start)] = [tw, men, w[0] if w and len(w) == 1 else -1]
    return out


def wins28(hi, lo=31):
    """Kolejne pelne okna 28 dob konczace sie na dobie hi, nie wczesniej niz od doby lo (120 dob: 37-64, 65-92, 93-120)."""
    w = []
    e = hi
    while e - 27 >= lo:
        w.insert(0, (e - 27, e))
        e -= 28
    return w


def mean_days(series, days, key=None):
    """Srednia wartosci z dob `days` (series: {doba: liczba} albo {doba: {klucz: liczba}}); None, gdy brak."""
    vals = []
    for d in days:
        v = series.get(d)
        if v is None:
            continue
        if key is not None:
            v = v.get(key)
            if v is None:
                continue
        vals.append(v)
    return st.mean(vals) if vals else None


def intkeys(d):
    """JSON ma klucze-napisy - doby z powrotem na liczby (takze w slownikach zagniezdzonych o jeden poziom)."""
    if not isinstance(d, dict):
        return d
    out = {}
    for k, v in d.items():
        try:
            out[int(k)] = v
        except (TypeError, ValueError):
            out[k] = intkeys(v) if isinstance(v, dict) else v
    return out


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


def baseline_of(lines, rows=None, start=None, balp=None):
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

    def mean_of(d, pat, cast=int):
        vals = [num(pat, d[k], cast) for k in last_window(d)]
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
    # krok B (110, 112, klucz 114): ujscia i zrodla, ktore B zamyka - z linii ksiegi pieniadza obecnych takze w biegu bazowym
    # (plik bazowy zapisany przed B nie ma tych kluczy - zapisac go jeszcze raz z logu biegu bazowego: --zapisz-baze)
    cas = by_day(lines, "Przeplywy osad (kasy zamkow): dzien")
    base["swiat"]["zamki_zakupy"] = mean_of(cas, r'"zakupy" mieszkancow ([+-]?\d+)')
    base["swiat"]["zamki_dosypal"] = mean_of(cas, r"dosypal (\d+), skasowal")
    base["swiat"]["zamki_skasowal"] = mean_of(cas, r"skasowal (\d+);")
    bal = by_day(lines, "Pieniadz swiata (bilans): dzien")
    base["swiat"]["wsie_utarg_zniklo"] = mean_of(bal, r"z utargu wsi zniklo (-?\d+)")
    cau = by_day(lines, "Pieniadz swiata (bilans - przyczyny): dzien")
    base["swiat"]["wsie_prowizja"] = mean_of(cau, r"prowizja od sprzedazy partiom (?:-?\d+|-) \(wsie (-?\d+)")
    base["swiat"]["tabory_kiesy_zniknely"] = mean_of(cau, r"kiesy partii bez wodza, ktore zniknely z mapy -?\d+ \(karawany -?\d+, tabory (-?\d+)")
    vil = by_day(lines, "Przeplywy osad: dzien")
    base["swiat"]["utarg_miasta"] = mean_of(vil, r"miasta zaplacily (-?\d+)")
    # 112 (S16, R9): bandy - awanse u pasera (linia "Wyrzutki"), rozbite tabory (linia "Dowoz"), kiesy band (linia "Pieniadz swiata"),
    # sredni tier (linia "Utarg wsi (112)" - w biegu bazowym jej nie ma)
    wyr = by_day(lines, "Wyrzutki: dzien")
    base["swiat"]["bandy_awanse_paser"] = mean_of(wyr, r"od pasera (\d+) \(\d+ zl\)")
    base["swiat"]["bandy_paser_zl"] = mean_of(wyr, r"od pasera \d+ \((\d+) zl\)")
    dow = by_day(lines, "Dowoz: dzien")
    base["swiat"]["tabory_rozbite_bandy"] = mean_of(dow, r"w tym przez bandy (\d+)")
    base["swiat"]["bandy_zloto"] = mean_of(money, r", bandy (\d+)")
    utw = by_day(lines, "Utarg wsi (112): dzien")
    base["swiat"]["bandy_tier"] = mean_of(utw, r"sredni tier ([\d.]+)", float)
    # B-3: sila band (liczba i ludzie - linia "Wyrzutki") i ujscie ich zlota (wydatki w miastach - linia "Paser", wobec kies band)
    base["swiat"]["bandy_liczba"] = mean_of(wyr, r"\| band (\d+), ludzi \d+")
    base["swiat"]["bandy_ludzie"] = mean_of(wyr, r"\| band \d+, ludzi (\d+)")
    pas = by_day(lines, "Paser: dzien")
    base["swiat"]["bandy_wydaly"] = mean_of(pas, r"bandy wydaly (\d+) zl")
    if start is not None:
        # B-3: szeregi dobowe (doba kampanii) do okien wobec bazy: pieniadz swiata, wojsko krolestw (bunty z macierzystym), zalogi na twierdze
        base["dni"] = money_days(lines, start)
        base["krolestwa_dni"] = kingdom_days(lines, start)
        base["twierdze_dni"] = fortress_days(balp, start, lines)
        bud = by_day(lines, "Budzet rodow (na sucho): dzien")
        heads = {run_day(d, start): num(r"glowy < 5000: (\d+)", s) for d, s in bud.items()}
        base["swiat"]["glowy_120"] = heads.get(120)
        sd = by_day(lines, "D staly (169c)")
        bank = [num(r"bankruci do 168 \(K39 lub bankrut Banku, rody AI\): (\d+)", s) for s in sd.values()]
        bank = [b for b in bank if b is not None]
        base["swiat"]["bankruci_max"] = max(bank) if bank else None
        d40 = [s for d, s in sd.items() if run_day(d, start) == 40]
        base["swiat"]["d_staly_zamki_d40"] = num(r"panowie samych zamkow \(\d+\): D staly mediana (-?\d+)", d40[0]) if d40 else None
        # wojsko wszystkich krolestw i krolestw w wojnie - srednia ostatnich 28 dob (dzienne sumy)
        kd = base["krolestwa_dni"]
        days = sorted({d for s in kd.values() for d in s})
        tot = {d: sum(s[d][1] for s in kd.values() if d in s) for d in days}
        war = {d: sum(s[d][1] for s in kd.values() if d in s and s[d][0]) for d in days}
        lw = last_window(days)
        base["swiat"]["wojsko_swiat"] = mean_days(tot, lw)
        base["swiat"]["wojsko_wojna_swiat"] = mean_days(war, lw)
    return base


def main(argv):
    args = argv[1:]
    group, logp, base_in, base_out, csvp, start_arg = None, None, None, None, None, None
    final = False
    i = 0
    while i < len(args):
        a = args[i]
        if a == "--koniec-etapu":
            final = True; i += 1; continue
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
    balp = os.path.join(os.path.dirname(csvp), "balans-krolestw.csv")
    print(f"plik: {logp} | doby gry {first}-{last} ({len(alld)} dob) | start kampanii {start} ({how}): doby kampanii {run_day(first, start)}-{run_day(last, start)}"
          f" | CSV: {csvp if rows else 'brak (' + csvp + ')'}" + (" | TEST KONCA ETAPU (--koniec-etapu)" if final else ""))
    # plik bazowy i liczby tego biegu (B-3: na poczatku - wiersze warunkow etapu porownuja sie z baza)
    base = None
    if base_in:
        with open(base_in, encoding="utf-8") as f:
            base = json.load(f)
        for k in ("dni", "krolestwa_dni", "twierdze_dni"):
            if k in base:
                base[k] = intkeys(base[k])
    now = baseline_of(lines, rows, start, balp)
    sw, bw = now["swiat"], (base or {}).get("swiat", {})
    b_on = bool(by_day(lines, "Zawor zamkow (110): dzien"))   # krok B w biegu (linia 110)
    etap_rule = "" if final else "; ETAP, gdy nie gorzej niz baza"

    # 1. glowy < 5 000 (doba kampanii 120, 364, 728; i ostatnia). B-3: bez --koniec-etapu ETAP, gdy nie wiecej niz baza +20% (min. +2) w dobie 120.
    # B-4 (przeglad B-3): +20% za luzne dla kroku, ktory tylko doklada panom - ETAP tylko do bazy + 2 glowy (rozrzut kampanii jeszcze niezmierzony;
    # druga para biegow przed C1)
    heads = {run_day(d, start): num(r"glowy < 5000: (\d+)", s) for d, s in budget.items()}
    for n in (120, 364, 728):
        if n in heads:
            v = heads[n]
            ok = v is not None and v <= 10
            gb, et, note = bw.get("glowy_120") if n == 120 else None, False, ""
            if not ok and not final and v is not None and gb is not None:
                lim = gb + 2
                et, note = v <= lim, f" (baza {gb}; nie gorzej niz baza: <= {lim:.0f})"
            rep.add(f"glowy < 5000 (doba {n})", f"{v}{note}", "<= 10 (warunek etapu 2 - C/D)" + ("" if final else "; ETAP, gdy nie wiecej niz baza + 2"), ok, et)
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
        bb = bw.get("bankruci_max")
        et = (not final) and bb is not None and worst[1] <= bb
        rep.add("bankruci do 168 (K39 lub bankrut Banku)", f"{worst[1]} (doba {run_day(worst[0], start)}: K39 {worst[2]}, w tym bez znanego salda {worst[3]}; Bank {worst[4]}; {worst[5]})"
                + (f" (baza najwiecej {bb})" if bb is not None else ""),
                "0 w kazdej dobie (warunek etapu 2 - 168)" + ("" if final else "; ETAP, gdy najwiecej nie wiecej niz w bazie"), worst[1] == 0, et)
    else:
        rep.add("bankruci do 168 (K39 lub bankrut Banku)", "brak linii", "0 w kazdej dobie", None)

    # 3. zajecie dochodu - dopiero 168
    rep.add("zajecie dochodu (D3)", "brak (168 niewgrane)", "<= 1 w 120 dobach, <= 5 w roku", None)

    # 4. skarbiec w wojnie < 0.25 mln dluzej niz 28 dob
    # B-3: krolestwa, ktore powstaly w trakcie biegu (bunty - "X (Y Rebels)", a takze kazde krolestwo bez linii w pierwszej dobie "Skarbce"),
    # rodza sie z pustym skarbcem - osobny wiersz (INFO; wiazacy z --koniec-etapu). Bieg 120 dob kroku B: Volentin League (Braavos Rebels)
    # od doby 62 i Moharis League (Pentos Rebels) od doby 79 - skarbiec 1 zl od narodzin; w bazie buntow nie bylo
    ks = kingdom_war_series(lines)
    d0 = min((d for ser in ks.values() for d, _, _, _ in ser), default=None)
    worst, worstk, nworst, nworstk = 0, "-", 0, "-"
    for k, ser in ks.items():
        run = best = 0
        for d, war, wallet, men in sorted(ser):
            run = run + 1 if (war and wallet < 250000) else 0
            best = max(best, run)
        born = REB.match(k) is not None or (d0 is not None and min(d for d, _, _, _ in ser) > d0)
        if born:
            if best > nworst:
                nworst, nworstk = best, k
        elif best > worst:
            worst, worstk = best, k
    rep.add("skarbiec w wojnie < 0.25 mln (najdluzsza seria, krolestwa z poczatku biegu)", f"{worst} dob ({worstk})" if ks else "brak linii", "0 serii > 28 dob",
            (worst <= 28) if ks else None)
    if ks:
        rep.add("skarbiec w wojnie < 0.25 mln (krolestwa powstale w biegu - bunty)", f"{nworst} dob ({nworstk})",
                "0 serii > 28 dob" + (" (koniec etapu)" if final else " - INFO: bunt rodzi sie z pustym skarbcem (wiazace z --koniec-etapu)"),
                (nworst <= 28) if final else None)

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

    # 8. wojsko w druzynach lordow W WOJNIE - cel 95-115 tys., twarda podloga 85 tys. B-3: srednia 28 dob (dotad ostatnia doba - jedna doba
    # wojny wiecej albo mniej przesuwala wynik o kilka tysiecy: bieg 120 dob kroku B 125.4 tys. w dobie 120 przy 118.0 tys. sredniej 28 dob).
    # B-4 (przeglad B-3): ETAP tylko, gdy bieg NIE JEST GORSZY niz baza w tych samych dobach - nie dalej od pasma 95-115 tys. niz baza, albo
    # (los kampanii: inne krolestwa w wojnie) wojsko tych samych krolestw w tych samych dobach wojny obu biegow, przeniesione na wojsko w wojnie
    # bazy (baza x bieg/baza na wspolnych dobach wojny), nie dalej od pasma niz baza - i wojsko swiata w pasmie bazy (+-10%). Dotad sam
    # warunek swiata: baza 111.5 tys. w pasmie, bieg 118.0 tys. poza - dostawal ETAP.
    tot, totall, nwar = {}, {}, {}
    for k, ser in ks.items():
        for d, war, wallet, men in ser:
            totall[d] = totall.get(d, 0) + men
            if war:
                tot[d] = tot.get(d, 0) + men
                nwar[d] = nwar.get(d, 0) + 1
    world_ok = None
    if totall:
        lw = last_window(totall)
        v = mean_days(tot, lw) or 0
        allm = mean_days(totall, lw)
        nw = mean_days(nwar, lw) or 0
        lastd = max(totall)
        # wobec bazy w TYCH SAMYCH dobach kampanii (wojsko rosnie przez kampanie - bieg 40 dob nie porownuje sie z dobami 93-120 bazy);
        # plik bazowy sprzed B-3 (bez szeregow dobowych) - ostatnie 28 dob bazy
        bwa, bwv, allm_c, bwin = bw.get("wojsko_swiat"), bw.get("wojsko_wojna_swiat"), allm, "ostatnie 28 dob bazy"
        vwc, like, nkn, nkb = None, None, None, None
        kn0, kb0 = now.get("krolestwa_dni") or {}, (base or {}).get("krolestwa_dni") or {}
        if kn0 and kb0:
            hi0 = min(max(d for x in kn0.values() for d in x), max(d for x in kb0.values() for d in x))
            cw = list(range(max(1, hi0 - 27), hi0 + 1))

            def sums(T, war_only):
                return {d: sum(x[d][1] for x in T.values() if d in x and (x[d][0] or not war_only)) for d in cw}
            allm_c, bwa, bwv = mean_days(sums(kn0, False), cw), mean_days(sums(kb0, False), cw), mean_days(sums(kb0, True), cw)
            vwc = mean_days(sums(kn0, True), cw)
            bwin = f"doby {cw[0]}-{cw[-1]} obu biegow"
            # B-4: te same krolestwa w tych samych dobach wojny obu biegow (bunty z macierzystym) - bieg / baza; liczba krolestw w wojnie (srednio)
            cr = cb = 0
            for d in cw:
                for k in set(kn0) & set(kb0):
                    a, c = kn0[k].get(d), kb0[k].get(d)
                    if a and c and a[0] and c[0]:
                        cr += a[1]; cb += c[1]
            like = cr / cb if cb > 0 else None
            nkn = mean_days({d: sum(1 for x in kn0.values() if d in x and x[d][0]) for d in cw}, cw)
            nkb = mean_days({d: sum(1 for x in kb0.values() if d in x and x[d][0]) for d in cw}, cw)
        if bwa and allm_c:
            ch = allm_c / bwa - 1
            # B-4 (przeglad B-3): +-10% - suma ok. 30 krolestw usrednia los pojedynczych (pasmo -25%..+20% jest na krolestwo)
            world_ok = -0.10 <= ch <= 0.10
            rep.add("wojsko swiata wobec bazy (wszystkie krolestwa, srednia 28 dob)", f"{allm_c:.0f} wobec bazy {bwa:.0f} ({100 * ch:+.1f}%; {bwin})",
                    "+-10% (krok paczki nie zmienia wojska swiata ponad to; rozrzut kampanii do zmierzenia druga para biegow)", world_ok)
        if v == 0:
            rep.add("wojsko w druzynach lordow (krolestwa w wojnie, srednia 28 dob)", f"0 (zadne krolestwo w wojnie; wszystkie {allm:.0f})", "95-115 tys. w wojnie", None)
        else:
            if 95000 <= v <= 115000:
                ok, note = True, ""
            elif 85000 <= v < 95000:
                ok, note = False, " - ponizej celu 95 tys. (nad podloga 85 tys.)"
            else:
                ok, note = False, (" - ponizej podlogi 85 tys." if v < 85000 else " - powyzej 115 tys.")
            # bwv - wojsko w wojnie bazy w tych samych dobach (wyzej); bez szeregow w bazie - ostatnie 28 dob bazy
            def gap(x):
                return 0.0 if 95000 <= x <= 115000 else (95000 - x if x < 95000 else x - 115000)
            et, etxt = False, ""
            if not final and world_ok is True and bwv:
                vv = vwc if vwc is not None else v
                if gap(vv) <= gap(bwv):
                    et, etxt = True, f"; nie dalej od pasma niz baza ({vv:.0f} wobec {bwv:.0f})"
                elif like is not None and gap(bwv * like) <= gap(bwv):
                    et, etxt = True, (f"; te same krolestwa w tych samych dobach wojny: {100 * (like - 1):+.1f}% wobec bazy -> {bwv * like:.0f}"
                                      f" (reszta z krolestw w wojnie tylko w biegu; krolestw w wojnie srednio {nkn:.1f} wobec {nkb:.1f}, bunty z macierzystym)")
                else:
                    etxt = f"; gorzej niz baza: {vv:.0f} wobec {bwv:.0f}" + (f", te same krolestwa i doby wojny {100 * (like - 1):+.1f}% -> {bwv * like:.0f}" if like is not None else "")
            rep.add("wojsko w druzynach lordow (krolestwa w wojnie, srednia 28 dob)",
                    f"{v:.0f} (srednio {nw:.1f} krolestw w wojnie; wszystkie {allm:.0f}; ostatnia doba {tot.get(lastd, 0)})" + (f"; baza w wojnie {bwv:.0f}" if bwv else "") + note + etxt,
                    "95-115 tys. w wojnie; twarda podloga 85 tys. (warunek etapu 2 - budzet 166)"
                    + ("" if final else "; ETAP, gdy wojsko swiata w pasmie bazy i bieg nie dalej od pasma niz baza (te same doby albo te same krolestwa w tych samych dobach wojny)"),
                    ok, et)

    # 9. zalogi, dezercja, okupy, pieniadz swiata - wobec bazy (plik bazowy i liczby biegu - na poczatku main)
    def vs(name, key, rule, test, info=False):
        v, b = sw.get(key), bw.get(key)
        if v is None:
            rep.add(name, "brak linii", rule, None)
        elif b is None or b == 0:
            rep.add(name, f"{v:.0f} (bez bazy)", rule, None)
        else:
            rep.add(name, f"{v:.0f} wobec bazy {b:.0f} ({100.0 * v / b - 100:+.0f}%)", rule, None if info else test(v, b))

    vs("zalogi swiata (28 dob)", "zalogi", "INFO - prog na twierdze i na krolestwo (wiersze nizej)", None, info=True)
    # B-3: zalogi NA TWIERDZE (balans-krolestw.csv) - spadek zalog krolestwa, ktore stracilo twierdze, to nie mniejsza zaloga (krok B: Pentos 2 twierdze
    # wobec 6, The Reach 15 wobec 17 - na twierdze -2..+5%); doby wojny z linii "Skarbce". Swiat wiazacy; na krolestwo - warunek etapu (166).
    # B-4 (przeglad B-3): los jednej kampanii odwraca znak zmiany miedzy oknami 28 dob - spadek, ktory we WSZYSTKICH oknach ma ten sam znak, to
    # znak skutku, nie losu: DECYZJA (dotad tylko opis przy ETAP - Qohor -11/-16/-7%, Tyrosh -11/-12/-13% dostaly ETAP). Nie NIE wprost: trzy
    # okna jednej pary biegow daja ten sam znak tez z losu (krolestwo, ktore wczesnie stracilo twierdze, zostaje slabsze) - rozstrzyga druga para
    # biegow (rozrzut kampanii) przed C1. ETAP tylko dla zmian, ktore zmieniaja znak miedzy oknami, i przy jednym oknie (bieg < 58 dob - nie da sie
    # odroznic; wiaze bieg 120 dob), gdy swiat w normie; swiat poza norma albo --koniec-etapu - NIE
    tn, tb = now.get("twierdze_dni") or {}, (base or {}).get("twierdze_dni") or {}
    garr_ok = None
    if tn and tb:
        hi = min(max(d for x in tn.values() for d in x), max(d for x in tb.values() for d in x))
        lw = list(range(hi - 27, hi + 1))
        ids = sorted(set(tn) & set(tb))

        def per_fort(T, keys, days):
            men = tw = 0
            for k in keys:
                for d in days:
                    x = T.get(k, {}).get(d)
                    if not x or x[0] <= 0 or x[2] == 0:   # bez twierdz albo w pokoju (wojna nieznana -1 liczona jak wojna)
                        continue
                    men += x[1]; tw += x[0]
            return men / tw if tw > 0 else None
        wn, wb = per_fort(tn, ids, lw), per_fort(tb, ids, lw)
        if wn and wb:
            garr_ok = wn >= 0.95 * wb
            rep.add("zalogi swiata na twierdze w wojnie (28 dob, balans-krolestw.csv)", f"{wn:.0f} wobec bazy {wb:.0f} ({100 * (wn / wb - 1):+.1f}%; {len(ids)} krolestw w obu biegach)",
                    ">= 95% bazy", garr_ok)
        ws = wins28(hi) or [(max(1, hi - 27), hi)]          # bieg krotszy niz 58 dob - jedno okno (ostatnie 28 dob)
        low = []
        for k in ids:
            r = []
            for a, b in ws:
                pn, pb = per_fort(tn, [k], range(a, b + 1)), per_fort(tb, [k], range(a, b + 1))
                r.append(pn / pb - 1 if pn and pb else None)
            if r and r[-1] is not None and r[-1] < -0.05:
                low.append((k, r, len(r) >= 2 and all(x is not None and x < 0 for x in r)))
        pers_n = sum(1 for _, _, pers in low if pers)
        txt = ", ".join(f"{k} {100 * r[-1]:+.0f}%" + ((" (ten sam znak we wszystkich oknach " if pers else " (okna ") + "/".join("-" if x is None else f"{100 * x:+.0f}" for x in r) + ")"
                                                      if len(r) >= 2 else "") for k, r, pers in low)
        one = "; jedno okno 28 dob (bieg < 58 dob) - skutku od losu nie da sie odroznic" if len(ws) < 2 else ""
        rep.add("zalogi na twierdze w wojnie na krolestwo (28 dob)", (txt + (f" - ten sam znak we wszystkich oknach: {pers_n}" if pers_n else "") + one) if low else f"wszystkie w normie ({len(ids)} krolestw)",
                ">= 95% bazy (warunek etapu 2 - 166)" + ("" if final else "; swiat (wiersz wyzej) w normie: spadek o tym samym znaku we wszystkich oknach 28 dob - DECYZJA"
                                                       " (skutek albo trwaly los - druga para biegow), znak zmienia sie miedzy oknami (albo jedno okno - bieg < 58 dob) - ETAP"),
                not low, (not final) and garr_ok is True and pers_n == 0, decide=(not final) and garr_ok is True and pers_n > 0)
    elif base:
        low, have = [], 0
        for k, cur in now["krolestwa"].items():
            cg, bg = cur.get("zalogi_wojna"), base["krolestwa"].get(k, {}).get("zalogi_wojna")
            if cg is None or not bg:
                continue
            have += 1
            if cg < 0.95 * bg:
                low.append(f"{k} {100.0 * cg / bg:.0f}%")
        rep.add("zalogi w wojnie na krolestwo (28 dob, CSV)", (", ".join(low) if low else f"wszystkie w normie ({have} krolestw)") if have else "brak danych (CSV albo baza bez zalog)",
                ">= 95% bazy w wojnie (baza bez twierdz - zapisac baze jeszcze raz)", (not low) if have else None)
    else:
        ng = sum(1 for k in now["krolestwa"].values() if k.get("zalogi_wojna"))
        rep.add("zalogi w wojnie na krolestwo (28 dob, CSV)", f"{ng} krolestw (bez bazy)" if rows else "brak CSV", ">= 95% bazy w wojnie", None)
    vs("dezercja AI z morale i zaleglego zoldu (28 dob)", "dezercja_ai_morale_zold", "<= baza + 50% (prog 183)", lambda v, b: v <= 1.5 * b)
    vs("dezercja AI razem z limitem zoldu gry (28 dob)", "dezercja_ai", "INFO (166 wylacza limit gry)", None, info=True)
    vs("lordowie w niewoli > 60 dni (28 dob)", "lordowie_ponad_60", "<= baza + 20%", lambda v, b: v <= 1.2 * b)
    v, b = sw.get("pieniadz_zmiana"), bw.get("pieniadz_zmiana")
    dn0, db0 = now.get("dni") or {}, (base or {}).get("dni") or {}
    if dn0 and db0:
        # B-3: te same doby kampanii obu biegow (ostatnie 28 wspolnych dob) - bieg 40 dob nie porownuje sie z dobami 93-120 bazy
        hi0 = min(max(dn0), max(db0))
        cw = list(range(max(1, hi0 - 27), hi0 + 1))
        v0, b0 = mean_days(dn0, cw, "Z"), mean_days(db0, cw, "Z")
        if v0 is not None and b0 is not None:
            v, b = v0, b0
    rep.add("pieniadz swiata - zmiana na dobe (28 dob)", f"{v:+.0f}" + (f" wobec bazy {b:+.0f} (roznica {v - b:+.0f})" if b is not None and v is not None else "") if v is not None else "brak linii",
            "C/D: +-50 tys./dobe wobec biegu bez paczki" + (" - w kroku B INFO: wiazace wiersze 'B: pieniadz swiata' nizej (okno dob 31+, bez wojny)" if b_on else ""),
            None if (v is None or b is None or b_on) else abs(v - b) <= 50000)

    # 10. krolestwa w wojnie wobec bazy (-25% / +20%; biedne z lore -35%). B-3: bunty razem z krolestwem macierzystym (Braavos + Volentin League),
    # okna 28 dob od doby 37; bez --koniec-etapu ETAP, gdy wojsko swiata w pasmie bazy - z jedna para biegow znak zmiany krolestwa odwraca sie
    # miedzy oknami (krok B: Lys +29/-10/-34%, Tyrosh -17/-12/+44%), a wojsko idzie za dochodem rodow do budzetu 166 (projekt 2.0a pkt 6).
    # B-4 (przeglad B-3): krolestwo poza pasmem w ostatnim oknie, ktorego zmiana ma ten sam znak we WSZYSTKICH oknach - DECYZJA (znak skutku albo
    # trwalego losu - druga para biegow; jak zalogi wyzej); ETAP tylko dla zmian zmieniajacych znak (i przy jednym oknie - bieg < 58 dob)
    kn, kb = now.get("krolestwa_dni") or {}, (base or {}).get("krolestwa_dni") or {}
    if kn and kb:
        hi = min(max(d for x in kn.values() for d in x), max(d for x in kb.values() for d in x))
        ws = wins28(hi) or [(max(1, hi - 27), hi)]

        def war_mean(T, k, a, b):
            v = [T[k][d][1] for d in range(a, b + 1) if d in T.get(k, {}) and T[k][d][0]]
            return st.mean(v) if v else None
        worse, other = [], 0
        for k in sorted(set(kn) & set(kb)):
            low = -0.35 if any(p in k.lower() for p in LORE_POOR) else -0.25
            r = []
            for a, b in ws:
                cn, cb = war_mean(kn, k, a, b), war_mean(kb, k, a, b)
                r.append(cn / cb - 1 if cn and cb else None)
            if r[-1] is None:
                if war_mean(kn, k, *ws[-1]) or war_mean(kb, k, *ws[-1]):
                    other += 1                                    # w wojnie tylko w jednym biegu (krok B: Riverlands)
                continue
            if r[-1] < low or r[-1] > 0.20:
                up = r[-1] > 0.20
                pers = len(r) >= 2 and all(x is not None and ((x > 0) if up else (x < 0)) for x in r)
                worse.append((f"{k} {100 * r[-1]:+.0f}%" + ((" (ten sam znak we wszystkich oknach " if pers else " (okna ") + "/".join("-" if x is None else f"{100 * x:+.0f}" for x in r) + ")"
                                                            if len(r) >= 2 else ""), pers))
        pers_n = sum(1 for _, pers in worse if pers)
        rep.add("krolestwa w wojnie: wojsko wobec bazy (bunty z macierzystym, 28 dob)",
                (", ".join(w for w, _ in worse) + (f" - ten sam znak we wszystkich oknach: {pers_n}" if pers_n else "")
                 + ("; jedno okno 28 dob (bieg < 58 dob) - skutku od losu nie da sie odroznic" if len(ws) < 2 else "") if worse else "wszystkie w normie")
                + (f"; w wojnie tylko w jednym biegu: {other}" if other else ""),
                "-25%..+20% (lore -35%; warunek etapu 2 - 166)" + ("" if final else "; wojsko swiata w pasmie bazy: zmiana o tym samym znaku we wszystkich oknach 28 dob - DECYZJA"
                                                                  " (skutek albo trwaly los - druga para biegow), znak zmienia sie miedzy oknami (albo jedno okno - bieg < 58 dob) - ETAP"),
                not worse, (not final) and world_ok is True and pers_n == 0, decide=(not final) and world_ok is True and pers_n > 0)
    elif base:
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
        b40 = bw.get("d_staly_zamki_d40")
        if b_on and b40 is not None and v is not None:
            # B-3: 400-1300 to sprawdzenie miary 169c bez paczek; krok B z projektu tylko DOKLADA panom zamkow (wies ok. +130, zamek do +310 zl/dobe),
            # wiec po B prog wobec bazy: nie mniej niz baza i nie wiecej niz baza + 440. B-4 (przeglad B-3): dolna granica baza (dotad baza -10% - spadek
            # o kilkanascie procent przechodzil jako TAK; rozrzut mediany do zmierzenia druga para biegow)
            rep.add("D staly pana samych zamkow (doba 40) wobec bazy", f"{v} wobec bazy {b40} ({v - b40:+d})", "baza .. baza +440 (krok B tylko doklada panom zamkow)",
                    b40 <= v <= b40 + 440)
            rep.add("D staly pana samych zamkow (doba 40)", v, "INFO po kroku B (169c bez paczek: 400-1300)", None)
        else:
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

    # 16b. krok B (projekt etapu 2, "110 + 112 + klucz 114"; progi rozdz. 1 i test kroku B) - linie paczek, regulator zamkow, zawor, pieniadz swiata
    zz = by_day(lines, "Zawor zamkow (110): dzien")
    b_on = bool(zz)
    rep.add("110: linia Zawor zamkow (110)", f"{len(zz)} dob" if zz else "brak (przed 110 albo Castle Purse Enabled wylaczone)", "obecna po wgraniu B", True if zz else None)
    cas_b = by_day(lines, "Przeplywy osad (kasy zamkow): dzien")
    sk = [num(r"skasowal (\d+);", cas_b[k]) for k in sorted(cas_b)]
    sk = [x for x in sk if x is not None]
    rep.add("110: regulator kas zamkow - skasowane (najwiecej w dobie)", max(sk) if sk else "brak linii", "0 (kasowanie zablokowane)",
            (max(sk) == 0) if (sk and b_on) else None)
    dos = [num(r"dosypka do zapasu \(tryb 1, zostaje do etapu 3\) (\d+)", zz[k]) for k in last_window(zz)]
    dos = [x for x in dos if x is not None]
    # 110-p: prog zamiast INFO. Projekt liczyl dosypke ok. 3.7 tys./dobe; bieg bazowy (kopia-baza120, doby 31-120) ma w kasach zamkow "zakupy" +55 tys.,
    # dosypke 26 tys. i kasowanie 34 tys., a prawdziwe przeplywy netto -40 tys./dobe - po 110 dosypka trybu 1 moze przejac role "zakupow" (zloto z niczego
    # w zamkach bez spadku). TAK, gdy dosypka <= 10 tys./dobe (Z9) albo zloto z niczego netto w kasach zamkow ("zakupy" + dosypka - kasowanie, linia
    # "Przeplywy osad (kasy zamkow)") nie wyzsze niz w biegu bazowym; inaczej NIE
    def cas_net(w):
        if any(w.get(k) is None for k in ("zamki_zakupy", "zamki_dosypal", "zamki_skasowal")):
            return None
        return w["zamki_zakupy"] + w["zamki_dosypal"] - w["zamki_skasowal"]
    net_now, net_base = cas_net(sw), cas_net(bw)
    if dos:
        dm = st.mean(dos)
        ok_dos = dm <= 10000 or (net_now is not None and net_base is not None and net_now <= net_base)
        rep.add("110: dosypka regulatora do zapasu zamkow (tryb 1, 28 dob)",
                f"{dm:.0f} na dobe; zloto z niczego netto w kasach zamkow " + (f"{net_now:+.0f}" if net_now is not None else "-")
                + (f" wobec bazy {net_base:+.0f}" if net_base is not None else " (bez bazy)"),
                "<= 10 tys./dobe (Z9) albo z niczego netto w kasach zamkow <= baza (projekt: ok. 3.7 tys.)", ok_dos if b_on else None)
    else:
        rep.add("110: dosypka regulatora do zapasu zamkow (tryb 1, 28 dob)", "brak linii", "<= 10 tys./dobe (Z9) albo z niczego netto w kasach zamkow <= baza", None)
    pz = [num(r"z zaworu srednio (\d+)", zz[k]) for k in last_window(zz)]
    pz = [x for x in pz if x is not None]
    # 200-350 zl/dobe projekt liczyl z kasowania regulatora ok. 105 tys./dobe (stare logi: zold zalog 60 tys., sprzet AI 23 tys.); bieg bazowy
    # (kopia-baza120) ma w kasach zamkow kasowanie 34 tys., zold zalog 35-38 tys., sprzet AI ok. 0 (171 C8 - zamek nie jest targiem broni) i prawdziwe
    # przeplywy netto -30 tys./dobe: kupcy przywoza do zamkow konie i uprzaz (BK: ludnosc zamku je zuzywa, popyt kategorii koni 0.14 x dobrobyt).
    # Po B-2 zamek kupuje tylko z nadwyzki, wiec zawor bierze 7% doplywu doby (szacunek B-2: 15-25 zl/dobe na pana samych zamkow).
    # B-4 (przeglad B-3): B-3 zrobil z progu INFO bez decyzji, a zaden inny prog nie pilnuje tego dochodu (D staly pomija zawor z wlasnego zoldu -
    # "wlasne" 169c; wiersz Z8 nizej byl prawdziwy z definicji). Prog projektu zostaje: TAK w 200-350, ponizej - DECYZJA (przyczyna poza krokiem B:
    # konie zjadane przez ludnosc zamkow wbrew "kon ginie jak ginie" - paczka koni P1/P2; albo nowy cel z rachunku E2 przeliczonego z zaworem
    # zmierzonym po B-2 - poprawka projektu przed C1), powyzej - NIE
    if pz:
        pm = st.mean(pz)
        rep.add("110: pan samych zamkow - wplyw z zaworu (srednio, 28 dob)", f"{pm:.0f} zl/dobe",
                "200-350 (projekt, test kroku B); ponizej - DECYZJA: konie zjadane w zamkach (paczka koni P1/P2) albo nowy cel z E2 po biegu B-2 (poprawka projektu przed C1)",
                (200 <= pm <= 350) if b_on else None, decide=pm < 200)
    else:
        rep.add("110: pan samych zamkow - wplyw z zaworu (srednio, 28 dob)", "brak linii", "200-350 (projekt, test kroku B)", None)
    # B-3 (Z8): to, co pan dostaje z zaworu, minus polowa zoldu zalog zamkow, ktorego korona mu nie zwraca, bo "wraca zaworem". Bieg 120 dob kroku B
    # (114-p HomePart z przewidywania): zawor panom 5.9 tys., bez zwrotu 17.7 tys. -> -3 tys./dobe. B-4 (przeglad B-3): po B-2 ciecie rodu to
    # min(zold zalog zamkow, zawor rodu dzis, zold rodu) - suma ciec nie przekracza zaworu panom z tej samej doby, wiec wiersz jest prawdziwy z
    # definicji: INFO (kontrola zgodnosci - ciecie <= zawor). Wiazacy Z8 - wiersz nizej (gorna granica z 1 zl, rody w wojnie)
    zo = by_day(lines, "Zold: dzien")
    hv, lv, hv_b2 = [], [], False
    for k in last_window(zo):
        h = num(r"bez zwrotu korony \((?:114-p|Z8, B-2[^)]*)\) (\d+)", zo[k])
        if h is not None:
            hv.append(h)
            hv_b2 = hv_b2 or "bez zwrotu korony (Z8, B-2" in zo[k]
    for k in last_window(zz):
        a1 = num(r"zawor: (\d+) do panow", zz[k])
        if a1 is not None:
            lv.append(a1)
    if hv and lv:
        net = st.mean(lv) - 0.5 * st.mean(hv)
        rep.add("110/Z8: zawor zamkow do panow minus utracony zwrot korony (28 dob)", f"{net:+.0f} na dobe (zawor panom {st.mean(lv):.0f}, zold bez zwrotu {st.mean(hv):.0f} x 50%)"
                + (("; ciecie <= zawor: " + ("tak" if st.mean(hv) <= st.mean(lv) else "NIE - blad TakePaid")) if hv_b2
                   else "; log sprzed B-2 (114-p: ciecie z przewidywania - bez kontroli zgodnosci)"),
                "po B-2 INFO - kontrola zgodnosci (ciecie <= zawor z definicji), wiazacy Z8 - wiersz nizej; log sprzed B-2 (114-p) >= 0",
                (net >= 0) if (b_on and not hv_b2) else None)
    else:
        rep.add("110/Z8: zawor zamkow do panow minus utracony zwrot korony (28 dob)", "brak linii", "INFO - kontrola zgodnosci", None)
    # B-4 (Z8 2.0b, gorna granica): rody, ktorym korona zwraca zold (krolestwo w wojnie, nie najemnik), z zoldem zalog wplaconym do kas WLASNYCH zamkow -
    # z 1 zl tego zoldu wraca do rodu nalezny zwrot korony od reszty zoldu (po cieciu) + zawor panom tych rodow < 1. Moze pasc: ciecie ma gorna granice
    # w zoldzie zalog, a zawor rodu bierze tez inne wplaty do kasy zamku (place budow, zakupy na targu zamku). Licznik z linii "Zold:" (od B-4)
    z8p = z8d = z8r = 0
    z8n = 0
    for k in last_window(zo):
        m8 = re.search(r"Z8 rody w wojnie z zoldem zalog we wlasnych zamkach: zold do kas zamkow (\d+), zawor panom (\d+), nalezny zwrot korony od reszty (\d+)", zo[k])
        if m8:
            z8p += int(m8.group(1)); z8d += int(m8.group(2)); z8r += int(m8.group(3)); z8n += 1
    if z8n and z8p > 0:
        back = (z8d + z8r) / z8p
        rep.add("Z8: z 1 zl zoldu zalog we wlasnych zamkach wraca (rody w wojnie, 28 dob)", f"{back:.3f} (zawor {z8d / z8p:.3f} + nalezny zwrot korony {z8r / z8p:.3f}; zold {z8p / z8n:.0f} na dobe)",
                "< 1 (Z8 2.0b - nikt nie zarabia na wlasnym wydatku; gorna granica: zwrot nalezny, nie wyplacony)", (back < 1.0) if b_on else None)
    else:
        rep.add("Z8: z 1 zl zoldu zalog we wlasnych zamkach wraca (rody w wojnie, 28 dob)", "brak licznika (log sprzed B-4)" if not z8n else "0 zoldu", "< 1", None)
    # B-3: pieniadz swiata - zmiana tempa wobec bazy minus zamkniete ujscia, uczciwiej (diagnoza biegu 120 dob kroku B):
    #  (a) okno dob 31+ obu biegow (przy biegu >= 58 dob), nie ostatnie 28 dob - w dobach 1-30 przyciecie daru startowego zamkow (110) przesuwa
    #      w czasie to, co w bazie kasowal regulator (ok. 170 tys./dobe), a ostatnie 28 dob to los wojny (krok B: +212 tys. w 93-120, -50 tys. w 61-92);
    #  (b) bez wojennych zrodel z niczego, ktorych B nie dotyka (statki NavalDLC, bitwy, jency, lup z oblezen, minus lup z cial): krok B +41.5 tys.;
    #  (c) zamkniete ujscie taborow: baza "U8 tabory" minus sakwy, ktore naprawde zniknely (linia 112), nie U8 biegu B - do B-1 ksiega liczyla
    #      sakwe oddana przez 112 jako zniknieta (U8 = sakwy 112 w 119 z 119 dob);
    #  (d) B-4 (przeglad B-3): regulator kas miast ROZDZIELONY - dotad odejmowana byla zmiana netto (dosypal - skasowal), ktora laczyla dwie
    #      przeciwne rzeczy: mniej dosypki (zamkniete zrodlo z niczego - oczekiwane) i wiecej kasowania (zloto, ktore B zatrzymal we wsiach, regulator
    #      miast kasuje w nicosc - nowe ujscie). Teraz: spadek dosypki odejmowany jako oczekiwany, wzrost dosypki NIGDY (zostaje w wyniku - zloto
    #      z niczego); zmiana kasowania wylaczona z wyniku, ale jej wzrost ma WLASNY wiersz z progiem <= 10 tys./dobe (powyzej - DECYZJA: zamyka to
    #      dopiero 111' w etapie 5, a czesc kasowania to odpowiedz regulatora na wojenne zloto z niczego z (b) - jedna para biegow tego nie rozdziela).
    #      Bez rozbicia w bazie (plik sprzed B-4) - wynik jak dotad, ale INFO.
    # Do tego reszta ksiegi (niezmierzone) wobec bazy: paczka, ktora tworzy albo kasuje zloto bez nazwy, przesuwa reszte. Reszta B poprawiona o ten
    # sam blad ksiegi (U8 tabory minus sakwy zniklo z linii 112; po B-1 = 0). B-4: reszta wiaze na kazdej dlugosci biegu (ostatnie 28 wspolnych dob,
    # od doby 31 przy biegu >= 58 dob) - nie zalezy od przyciecia daru w dobach 1-30 (osobna pozycja ksiegi), wiec zloto bez nazwy wykryje juz
    # autotest 40 dob; wiersz "bez wojny" przy biegu < 58 dob - INFO (wiaze bieg 120 dob).
    dn, db = now.get("dni") or {}, (base or {}).get("dni") or {}
    if dn and db and b_on:
        hi = min(max(dn), max(db))
        binding = hi >= 58
        days = list(range(31, hi + 1)) if binding else list(range(max(1, hi - 27), hi + 1))

        def m(T, key, dflt=0.0):
            v = mean_days(T, days, key)
            return dflt if v is None else v

        def war(T):
            return sum(m(T, k) for k in WAR_KEYS) - m(T, "lup_cial")

        def cas_nothing(T):
            return m(T, "zamki_zakupy") + m(T, "zamki_dosypal") - m(T, "zamki_skasowal")
        has112 = mean_days(dn, days, "sakwy_zniklo_112") is not None
        lost_now = m(dn, "sakwy_zniklo_112") if has112 else m(dn, "u8_tabory")
        fix_r = (m(dn, "u8_tabory") - lost_now) if has112 else 0.0          # B-1: sakwy 112 liczone jako znikniete (stary log) - po B-1 ok. 0
        dz = m(dn, "Z") - m(db, "Z")
        dwar = war(dn) - war(db)
        e_cas = cas_nothing(dn) - cas_nothing(db)
        e_vil = (m(db, "utarg_zniklo") - m(dn, "utarg_zniklo")) + (m(db, "prow_wsie") - m(dn, "prow_wsie")) + (m(db, "u8_tabory") - lost_now)
        dreg = m(dn, "miasta_reg") - m(db, "miasta_reg")
        split_reg = mean_days(dn, days, "miasta_dos") is not None and mean_days(db, days, "miasta_dos") is not None
        wtxt = f"doby {days[0]}-{days[-1]}"
        if split_reg:
            d_dos = m(dn, "miasta_dos") - m(db, "miasta_dos")
            d_kas = m(dn, "miasta_kas") - m(db, "miasta_kas")
            exp_dos = min(d_dos, 0.0)                     # tylko spadek dosypki (zamkniete zrodlo z niczego) jest oczekiwany
            res = dz - dwar - e_cas - e_vil - exp_dos + d_kas
            rtxt = (f"regulator kas miast: dosypka {d_dos:+.0f} (odjete {exp_dos:+.0f} - tylko spadek), kasowanie {d_kas:+.0f} (poza wynikiem - wiersz nizej)")
        else:
            res = dz - dwar - e_cas - e_vil - dreg
            rtxt = f"regulator kas miast netto {dreg:+.0f} - baza bez rozbicia dosypka/kasowanie (zapisz baze jeszcze raz), wiersz INFO"
        rep.add("B: pieniadz swiata bez wojny - zmiana tempa wobec bazy minus zamkniete ujscia i spadek dosypki miast",
                f"{res:+.0f} ({wtxt}: zmiana tempa {dz:+.0f}, wojenne zrodla z niczego {dwar:+.0f}, zamki - zloto z niczego netto {e_cas:+.0f}, "
                f"wsie i tabory - zamkniete ujscia {e_vil:+.0f}, {rtxt})",
                "+-30 tys./dobe" + ("" if binding else " (INFO - wiaze bieg 120 dob: okno dob 31+ przy biegu >= 58 dob)"),
                (abs(res) <= 30000) if (binding and split_reg) else None)
        if split_reg:
            rep.add("B: ujscie w nicosc przeniesione do regulatora kas miast (wzrost kasowania wobec bazy)",
                    f"{d_kas:+.0f} na dobe ({wtxt}: skasowal {m(dn, 'miasta_kas'):.0f} wobec bazy {m(db, 'miasta_kas'):.0f}; zatrzymane przez B we wsiach i tabory {e_vil:+.0f})",
                    "<= 10 tys./dobe; powyzej - DECYZJA: regulator miast kasuje zloto, ktore B zatrzymal (zamyka 111' w etapie 5 - wczesniej?; czesc to wojenne zloto"
                    " z niczego, ktore regulator tez kasuje - rozrzut druga para biegow)" + ("" if binding else " (INFO - wiaze bieg 120 dob)"),
                    (d_kas <= 10000) if binding else None, decide=True)
        rn, rb = m(dn, "R") - fix_r, m(db, "R")
        rep.add("B: reszta ksiegi pieniadza (niezmierzone) wobec bazy", f"{rn - rb:+.0f} ({wtxt}: bieg {rn:+.0f}" + (f" po poprawce sakw 112 {-fix_r:+.0f}" if fix_r else "")
                + f", baza {rb:+.0f})", "+-10 tys./dobe (paczka nie tworzy ani nie kasuje zlota bez nazwy; te same doby obu biegow - wiaze tez autotest 40 dob)",
                abs(rn - rb) <= 10000)
    elif b_on:
        rep.add("B: pieniadz swiata bez wojny - zmiana tempa wobec bazy minus zamkniete ujscia i spadek dosypki miast", "brak bazy albo szeregow dobowych w bazie (zapisz baze jeszcze raz z logu biegu bazowego)",
                "+-30 tys./dobe", None)
    # dawny wiersz (28 dob, bez wojny i bez regulatora miast) - INFO dla porownania z wczesniejszymi raportami
    keys = ("zamki_zakupy", "zamki_dosypal", "zamki_skasowal", "wsie_utarg_zniklo", "wsie_prowizja", "tabory_kiesy_zniknely")
    if base and all(bw.get(k) is not None for k in keys) and all(sw.get(k) is not None for k in keys) and sw.get("pieniadz_zmiana") is not None \
            and bw.get("pieniadz_zmiana") is not None:
        e_cas = (sw["zamki_zakupy"] + sw["zamki_dosypal"] - sw["zamki_skasowal"]) - (bw["zamki_zakupy"] + bw["zamki_dosypal"] - bw["zamki_skasowal"])
        e_vil = (bw["wsie_utarg_zniklo"] - sw["wsie_utarg_zniklo"]) + (bw["wsie_prowizja"] - sw["wsie_prowizja"]) \
                + (bw["tabory_kiesy_zniknely"] - sw["tabory_kiesy_zniknely"])
        dv = sw["pieniadz_zmiana"] - bw["pieniadz_zmiana"]
        rep.add("B: pieniadz swiata - zmiana tempa wobec bazy minus zamkniete ujscia (ostatnie 28 dob, z wojna)",
                f"{dv - e_cas - e_vil:+.0f} (zmiana tempa {dv:+.0f}; zamkniete: zamki {e_cas:+.0f}, wsie i tabory {e_vil:+.0f})",
                "INFO - wiersz sprzed B-3 (los wojny ostatnich 28 dob, U8 z bledem kolejnosci ksiegi); wiazacy wiersz wyzej", None)
    # 112: utarg wsi, towar kupiony we wsi, sakwy taborow; bandy najwyzej +20% wobec bazy (S16)
    ut = by_day(lines, "Utarg wsi (112): dzien")
    rep.add("112: linia Utarg wsi (112)", f"{len(ut)} dob" if ut else "brak (przed 112)", "obecna po wgraniu B", True if ut else None)
    # 112-p (2.0b: licznik "bez odbiorcy" z progiem 0): kazda doba linii 112 - utarg nieprzypisany, towar kupiony we wsi i sakwy taborow, ktore zniknely
    def seg_of(line, head):
        for part in line.split(" | "):
            if part.startswith(head):
                return part
        return ""
    for name, head, pat in (("nieprzypisane (utarg)", "powroty taborow z utargiem", r"nieprzypisane (-?\d+)"),
                            ("zniklo (towar kupiony we wsi)", "towar kupiony we wsiach", r"zniklo (-?\d+)"),
                            ("zniklo (sakwy zniszczonych taborow)", "tabory zniszczone", r"zniklo (-?\d+)")):
        vals = []
        for k in sorted(ut):
            m = re.findall(pat, seg_of(ut[k], head))
            if m:
                vals.append(int(m[-1]))
        if not vals:
            rep.add(f"112: {name} - suma wszystkich dob", "brak linii", "0 (2.0b: bez odbiorcy - prog 0)", None)
            continue
        tot, bad = sum(vals), sum(1 for v in vals if v != 0)
        rep.add(f"112: {name} - suma wszystkich dob", f"{tot} ({bad} z {len(vals)} dob ponad 0)", "0 (2.0b: bez odbiorcy - prog 0)", tot == 0 and bad == 0)
    zn = [num(r"z utargu wsi zniklo (-?\d+)", s2) for s2 in by_day(lines, "Pieniadz swiata (bilans): dzien").values()]
    zn = [x for x in zn if x is not None]
    rep.add("112: z utargu wsi zniklo (srednio na dobe)", f"{st.mean(zn):.0f}" + (f" (baza {bw['wsie_utarg_zniklo']:.0f})" if bw.get("wsie_utarg_zniklo") is not None else "") if zn else "brak linii",
            "INFO - po 112 ok. 0 (reszta nieprzypisana w linii 112)", None)
    zam = [num(r"zamkniete ujscia razem \(dopisane wsiom, panom i zwyciezcom\): (\d+)", ut[k]) for k in last_window(ut)]
    zam = [x for x in zam if x is not None]
    rep.add("112: zamkniete ujscia (oddane wsiom, panom i zwyciezcom, 28 dob)", f"{st.mean(zam):.0f} na dobe" if zam else "brak linii",
            "INFO - projekt: ok. +47 tys./dobe do kies wsi + zywnosc i sakwy", None)
    # B-3: S16 pilnuje, czy bandy sie nie wzmacniaja (Jeff 09.10 04:55, Q3b: "autotest pilnuje, czy bandy sie nie wzmacniaja; zloto band ma platnika
    # i ujscie"). Samo zloto band nie jest miara sily: decyzja D (04:25 - zwyciezca bierze cala sakwe taboru) daje bandom doplyw (krok B: 0 -> 17 tys./dobe,
    # 99.5% sakw), a zapas ustala sie na doplyw / tempo wydatkow (ok. 10% kiesy dziennie w miastach) - kazde wykonanie D to ok. +100% zlota band.
    # Kiesy band - INFO; wiazace: sila (tier, liczba band, ludzie, awanse i zloto u pasera, tabory rozbite) <= baza +20% i ujscie zlota (wydatki
    # w miastach na dobe / kiesy band) nie mniej niz 80% bazy
    for key, name in (("bandy_tier", "sredni tier ludzi band"), ("bandy_liczba", "liczba band"), ("bandy_ludzie", "ludzie band"),
                      ("bandy_awanse_paser", "awanse band u pasera"), ("bandy_paser_zl", "zloto band u pasera"), ("tabory_rozbite_bandy", "tabory rozbite przez bandy na dobe"),
                      ("bandy_zloto", "kiesy band")):
        v, b = sw.get(key), bw.get(key)
        info = key == "bandy_zloto"
        rule = "INFO - decyzja D (zwyciezca bierze cala sakwe): zapas = doplyw / tempo wydatkow; wiazace sila i ujscie" if info else "<= baza + 20% (S16 - bandy sie nie wzmacniaja)"
        if v is None:
            rep.add(f"112: bandy - {name} (28 dob)", "brak linii", rule, None)
        elif b is None or b == 0:
            rep.add(f"112: bandy - {name} (28 dob)", f"{v:.3f}" if key == "bandy_tier" else f"{v:.0f}", rule + "; brak w bazie - porownac z biegiem z Villager Purse Survives wylaczonym", None)
        else:
            rep.add(f"112: bandy - {name} (28 dob)", (f"{v:.3f}" if key == "bandy_tier" else f"{v:.0f}") + f" wobec bazy " + (f"{b:.3f}" if key == "bandy_tier" else f"{b:.0f}")
                    + f" ({100.0 * v / b - 100:+.0f}%)", rule, None if info else ((v <= 1.2 * b) if b_on else None))
    sn, sb = sw.get("bandy_wydaly"), bw.get("bandy_wydaly")
    gn, gb = sw.get("bandy_zloto"), bw.get("bandy_zloto")
    if sn is not None and gn:
        qn = 100.0 * sn / gn
        qb = (100.0 * sb / gb) if (sb is not None and gb) else None
        rep.add("112: bandy - ujscie zlota (wydatki w miastach / kiesy band na dobe, 28 dob)", f"{qn:.1f}%" + (f" wobec bazy {qb:.1f}%" if qb is not None else " (bez bazy)"),
                ">= 80% bazy (zloto band ma ujscie - Q3b)", (qn >= 0.8 * qb) if (qb is not None and b_on) else None)
    else:
        rep.add("112: bandy - ujscie zlota (wydatki w miastach / kiesy band na dobe, 28 dob)", "brak linii", ">= 80% bazy", None)
    # klucz 114: udzial korony w zaworze zamkow = 1 - CastleDuesLordShare (domyslnie 0.33) - suma 28 dob z linii 110
    lp = cp = 0
    for k in last_window(zz):
        a1, c1 = num(r"zawor: (\d+) do panow", zz[k]), num(r"skarbcom krolestw (\d+) z", zz[k])
        if a1 is not None and c1 is not None:
            lp += a1; cp += c1
    rep.add("114: udzial korony w zaworze zamkow (28 dob)", f"{100.0 * cp / (lp + cp):.1f}% (korona {cp}, panowie {lp})" if lp + cp > 0 else "brak linii (przed 114 albo podzial wylaczony)",
            "ok. 33% (1 - Castle Dues Lord Share; zamki rodow bez krolestwa - calosc panu)", (28.0 <= 100.0 * cp / (lp + cp) <= 34.0) if (lp + cp > 0 and b_on) else None)
    v, b = sw.get("utarg_miasta"), bw.get("utarg_miasta")
    rep.add("B: zakupy plonu przez miasta (utarg taborow, 28 dob)", f"{v:.0f}" + (f" wobec bazy {b:.0f} ({100.0 * v / b:.0f}%)" if b else " (bez bazy)") if v is not None else "brak linii",
            ">= 95% bazy (bez zmian)", (v >= 0.95 * b) if (v is not None and b and b_on) else None)

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
